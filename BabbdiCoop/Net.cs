using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace BabbdiCoop
{
    public enum NetRole { None, Host, Client }

    /// <summary>
    /// Dumb UDP plumbing for a 2-player coop MVP.
    /// Host binds a port; client sends to it. Host learns client's endpoint from its first packet.
    /// Both sides broadcast their pose at ~20 Hz.
    /// Packet layout (31 bytes): [type=1][pos.x][pos.y][pos.z][rot.x][rot.y][rot.z][rot.w][itemId][flags]
    ///   pos = sender's FEET position (v0.5.0+), itemId = ItemProperties.id (0xFF = empty hands)
    ///   flags (v0.7.0): bit0 = crouching, bit1 = airborne, bit2 = using held item
    /// weaponIdx: 0..N = WeaponSwitching child index, or 0xFF = unknown.
    /// </summary>
    public class Net : IDisposable
    {
        public const byte FlagCrouch = 1;
        public const byte FlagAir = 2;
        public const byte FlagUse = 4;
        // v0.8.0 tricks: handstand is a held state, flip is a latched edge (≈1 s)
        public const byte FlagHandstand = 8;
        public const byte FlagFlip = 16;

        /// Shared clock for timestamps; Unity time APIs are main-thread-only and
        /// calling them from the RX thread throws. Never use Time.* here.
        public static readonly System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();

        /// Internal trace sink, wired to BepInEx logger from the plugin.
        public static Action<string> Trace;

        public NetRole role = NetRole.None;
        public int port = 7777;

        UdpClient udp;
        IPEndPoint remoteEP; // client's EP on host, server's EP on client
        Thread rxThread;
        volatile bool running;

        /// v0.10.6: `peer` is 0 for the host/client's host, 1 for the host's first client,
        /// 2 for the second. The 2-player pipeline uses the primary peer; a second remote avatar
        /// renders the other one, so two clients no longer share one model.
        public readonly ConcurrentQueue<(Vector3 pos, Quaternion rot, int weaponIdx, int flags, float clock, int peer)> incoming = new();
        public readonly List<IPEndPoint> clients = new();   // host only: up to two
        readonly List<float> clientLastSeen = new();         // parallel to clients

        // v0.10.0: the air-hockey mini-game's own packets (type 2). Kept separate from the pose
        // stream so a missing mini-game peer never affects the avatar sync.
        public const byte MiniKindMallet = 1;   // one peer's mallet (table-local x,z)
        public const byte MiniKindState = 2;    // host-authoritative puck + scores + flags
        public const byte MiniKindLeave = 3;    // the peer left the table
        public struct MiniPacket
        {
            public int kind, player;
            public float x, z, vx, vz;
            public int score0, score1, flags;
            public float clock;
            public int peer;
            // v0.10.7: side-based match state. `activePeer` is the client currently holding
            // the north mallet (0 = none), `winner` is 0/1 or 255, `target` is 11 or 100,
            // `recipient` is the peer id this copy was addressed to (host sends one copy per
            // client), and `northX/northZ` are the active north mallet for spectators.
            public int activePeer, winner, target, matchOver, recipient;
            public float northX, northZ;
            // v0.10.11: the goal-replay animation and sound event counters.
            public float puckY, puckAlpha, puckTilt;
            public int clickSeq, goalSeq;
            // v0.10.14: which table this packet belongs to, and the side each player chose.
            public int tableId, hostSide, clientSide;
        }
        public readonly ConcurrentQueue<MiniPacket> miniIncoming = new();

        // v0.10.42: cabinet duels use their own generic packet (type 4) so Snake can send a
        // whole board state and Arkanoid can send attacks/status without squeezing into the
        // air-hockey layout.
        public struct CabinetPacket
        {
            public int game, msg, peer;
            public byte[] data;
        }
        public readonly ConcurrentQueue<CabinetPacket> cabinetIncoming = new();

        // v0.10.45: version handshake. `peerVersion` is the string the other side announced
        // ("0.10.42"); `peerVersionMismatch` is true while it differs from this build.
        public string peerVersion;
        public bool peerVersionMismatch;

        // diagnostics
        public long sentPackets;
        public long recvPackets;
        public float lastRecvClock = -1f; // in Clock seconds
        public float ClockNow => (float)Clock.Elapsed.TotalSeconds;
        public bool remoteEndpointKnown => remoteEP != null;

        public void StartHost(int port)
        {
            Stop();
            this.port = port;
            role = NetRole.Host;
            udp = new UdpClient(port);
            running = true;
            rxThread = new Thread(RxLoop) { IsBackground = true };
            rxThread.Start();
        }

        public void StartClient(string hostIp, int port)
        {
            Stop();
            this.port = port;
            role = NetRole.Client;
            // Bind explicitly RIGHT NOW. An unbound UdpClient.Receive fails with
            // WSAEINVAL until the first Send implicit-binds it, and our RX thread
            // used to die silently when Receive lost that race. Binding to an
            // ephemeral port at startup fixes the one-way-flow bug.
            udp = new UdpClient(0);
            remoteEP = new IPEndPoint(IPAddress.Parse(hostIp), port);
            running = true;
            rxThread = new Thread(RxLoop) { IsBackground = true };
            rxThread.Start();
            Trace?.Invoke($"client started, local EP {(udp.Client.LocalEndPoint as IPEndPoint)}, target {remoteEP}");
        }

        void RxLoop()
        {
            try
            {
                var ep = new IPEndPoint(IPAddress.Any, 0);
                while (running && udp != null)
                {
                    var data = udp.Receive(ref ep);

                    // v0.10.6: the host supports two clients. Each sender gets a stable peer id
                    // (1, 2); the first one stays the primary for the 2-player pipeline, the second
                    // is rendered by the extra remote avatar. A third sender is ignored.
                    int peer = 0;
                    if (role == NetRole.Host)
                    {
                        lock (clients)
                        {
                            int idx = clients.FindIndex(x => x.Equals(ep));
                            if (idx < 0)
                            {
                                // v0.10.7: a client that restarted must be able to rejoin
                                // after its old endpoint went silent. Evict a slot that has
                                // not been heard from for 3 s; otherwise the host is full.
                                if (clients.Count >= 2)
                                {
                                    int oldest = 0;
                                    for (int i = 1; i < clientLastSeen.Count; i++)
                                        if (clientLastSeen[i] < clientLastSeen[oldest]) oldest = i;
                                    if (ClockNow - clientLastSeen[oldest] > 3f)
                                    {
                                        Trace?.Invoke($"host dropped stale client {oldest + 1}: {clients[oldest]}");
                                        clients.RemoveAt(oldest);
                                        clientLastSeen.RemoveAt(oldest);
                                    }
                                    else
                                    {
                                        continue; // three live clients: ignore
                                    }
                                }
                                clients.Add(ep);
                                clientLastSeen.Add(ClockNow);
                                idx = clients.Count - 1;
                                Trace?.Invoke($"host accepted client {idx + 1}: {ep}");
                            }
                            else
                            {
                                clientLastSeen[idx] = ClockNow;
                            }
                            peer = idx + 1;
                            remoteEP = clients[0];
                        }
                    }

                    if (data.Length >= 4 && data[0] == 5)
                    {
                        // v0.10.45: version announcement — [5][major][minor][patch].
                        // A peer on another build used to connect and then behave strangely
                        // (different packet fields, different rules) with no hint why.
                        string v = data[1] + "." + data[2] + "." + data[3];
                        if (peerVersion != v)
                        {
                            peerVersion = v;
                            peerVersionMismatch = v != BabbdiCoopPlugin.PluginVersion;
                        }
                        recvPackets++;
                        lastRecvClock = (float)Clock.Elapsed.TotalSeconds;
                    }
                    else if (data.Length >= 30 && data[0] == 1)
                    {
                        var pos = new Vector3(
                            BitConverter.ToSingle(data, 1),
                            BitConverter.ToSingle(data, 5),
                            BitConverter.ToSingle(data, 9));
                        var rot = new Quaternion(
                            BitConverter.ToSingle(data, 13),
                            BitConverter.ToSingle(data, 17),
                            BitConverter.ToSingle(data, 21),
                            BitConverter.ToSingle(data, 25));
                        int wIdx = data[29] == 0xFF ? -1 : data[29];
                        int flags = data.Length >= 31 ? data[30] : 0;
                        // v0.8.0: timestamp the packet HERE, on the receiving thread. The
                        // main thread used to back-date a whole batch by the average interval,
                        // which silently compressed (or stretched) the real arrival spacing
                        // whenever the queue held more than one packet — the knots then ran
                        // at the wrong speed and the avatar sped up / slowed down in bursts.
                        float now = (float)Clock.Elapsed.TotalSeconds;
                        incoming.Enqueue((pos, rot, wIdx, flags, now, peer));
                        recvPackets++;
                        lastRecvClock = now;
                        if (role == NetRole.Host) RelayPose(peer, pos, rot, wIdx, (byte)flags, ep);
                    }
                    else if (data.Length >= 32 && data[0] == 3)
                    {
                        // v0.10.6 relay: [type=3][source][pos 12][rot 16][item][flags]
                        if (role != NetRole.Host)
                        {
                            int source = data[1];
                            var pos = new Vector3(
                                BitConverter.ToSingle(data, 2),
                                BitConverter.ToSingle(data, 6),
                                BitConverter.ToSingle(data, 10));
                            var rot = new Quaternion(
                                BitConverter.ToSingle(data, 14),
                                BitConverter.ToSingle(data, 18),
                                BitConverter.ToSingle(data, 22),
                                BitConverter.ToSingle(data, 26));
                            int wIdx = data[30] == 0xFF ? -1 : data[30];
                            int flags = data[31];
                            float now = (float)Clock.Elapsed.TotalSeconds;
                            incoming.Enqueue((pos, rot, wIdx, flags, now, source));
                            recvPackets++;
                            lastRecvClock = now;
                        }
                    }
                    else if (data.Length >= 22 && data[0] == 2)
                    {
                        // v0.10.0 air-hockey: [type=2][kind][player][x][z][vx][vz][score0][score1][flags]
                        // v0.10.7 extends the host's state packet to 35 bytes with the side-based
                        // match state; v0.10.11 adds the puck replay animation and sound counters
                        // (49 bytes); v0.10.14 appends hostSide/clientSide/tableId (52 bytes).
                        // Mallet/leave packets carry tableId at byte 22 (23 bytes).
                        int kind = data[1];
                        int tableId = kind == MiniKindState
                            ? (data.Length >= 52 ? data[51] : 0)
                            : (data.Length >= 23 ? data[22] : 0);
                        int hostSide = kind == MiniKindState && data.Length >= 50 ? data[49] : 0;
                        int clientSide = kind == MiniKindState && data.Length >= 51 ? data[50] : 255;
                        var mp = new MiniPacket
                        {
                            kind = kind,
                            player = data[2],
                            x = BitConverter.ToSingle(data, 3),
                            z = BitConverter.ToSingle(data, 7),
                            vx = BitConverter.ToSingle(data, 11),
                            vz = BitConverter.ToSingle(data, 15),
                            score0 = data[19],
                            score1 = data[20],
                            flags = data[21],
                            clock = (float)Clock.Elapsed.TotalSeconds,
                            peer = peer,
                            activePeer = data.Length >= 23 ? data[22] : 0,
                            winner = data.Length >= 24 ? data[23] : 255,
                            target = data.Length >= 25 ? data[24] : 11,
                            matchOver = data.Length >= 26 ? data[25] : 0,
                            recipient = data.Length >= 27 ? data[26] : 0,
                            northX = data.Length >= 31 ? BitConverter.ToSingle(data, 27) : 0f,
                            northZ = data.Length >= 35 ? BitConverter.ToSingle(data, 31) : 0f,
                            puckY = data.Length >= 39 ? BitConverter.ToSingle(data, 35) : 0f,
                            puckAlpha = data.Length >= 43 ? BitConverter.ToSingle(data, 39) : 1f,
                            puckTilt = data.Length >= 47 ? BitConverter.ToSingle(data, 43) : 0f,
                            clickSeq = data.Length >= 48 ? data[47] : 0,
                            goalSeq = data.Length >= 49 ? data[48] : 0,
                            tableId = tableId,
                            hostSide = hostSide,
                            clientSide = clientSide
                        };
                        miniIncoming.Enqueue(mp);
                        recvPackets++;
                        lastRecvClock = mp.clock;
                    }
                    else if (data.Length >= 4 && data[0] == 4)
                    {
                        // v0.10.42 cabinet duel: [type=4][game][msg][len][payload...]
                        int game = data[1];
                        int msg = data[2];
                        int len = Math.Min((int)data[3], data.Length - 4);
                        var payload = new byte[len];
                        Buffer.BlockCopy(data, 4, payload, 0, len);
                        cabinetIncoming.Enqueue(new CabinetPacket
                        {
                            game = game,
                            msg = msg,
                            peer = peer,
                            data = payload
                        });
                        recvPackets++;
                        lastRecvClock = (float)Clock.Elapsed.TotalSeconds;
                    }
                }
            }
            catch (SocketException e) { Trace?.Invoke($"rx socket closed: {e.SocketErrorCode}"); }
            catch (ObjectDisposedException) { }
            catch (ThreadAbortException) { }
            catch (Exception e) { Trace?.Invoke($"rx died: {e.GetType().Name}: {e.Message}"); }
        }

        public void SendOwn(Vector3 pos, Quaternion rot, int weaponIdx, byte flags = 0)
        {
            var u = udp;
            if (u == null || role == NetRole.None) return;

            var data = new byte[31];
            data[0] = 1;
            Buffer.BlockCopy(BitConverter.GetBytes(pos.x), 0, data, 1, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(pos.y), 0, data, 5, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(pos.z), 0, data, 9, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(rot.x), 0, data, 13, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(rot.y), 0, data, 17, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(rot.z), 0, data, 21, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(rot.w), 0, data, 25, 4);
            data[29] = weaponIdx == -1 ? (byte)0xFF : (byte)weaponIdx;
            data[30] = flags;

            // v0.10.6: the host broadcasts its own pose to both clients.
            if (role == NetRole.Host)
            {
                lock (clients)
                    foreach (var ep in clients)
                        try { u.Send(data, data.Length, ep); sentPackets++; } catch { }
                return;
            }
            if (remoteEP == null) return;
            u.Send(data, data.Length, remoteEP);
            sentPackets++;
        }

        /// <summary>v0.10.6 — the host forwards client A's pose to B and B's to A.</summary>
        void RelayPose(int source, Vector3 pos, Quaternion rot, int weaponIdx, byte flags, IPEndPoint from)
        {
            if (role != NetRole.Host) return;
            var u = udp;
            if (u == null) return;
            var data = new byte[32];
            data[0] = 3;
            data[1] = (byte)source;
            Buffer.BlockCopy(BitConverter.GetBytes(pos.x), 0, data, 2, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(pos.y), 0, data, 6, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(pos.z), 0, data, 10, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(rot.x), 0, data, 14, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(rot.y), 0, data, 18, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(rot.z), 0, data, 22, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(rot.w), 0, data, 26, 4);
            data[30] = weaponIdx == -1 ? (byte)0xFF : (byte)weaponIdx;
            data[31] = flags;
            lock (clients)
                foreach (var ep in clients)
                {
                    if (ep.Equals(from)) continue;
                    try { u.Send(data, data.Length, ep); sentPackets++; } catch { }
                }
        }

        // ---------- v0.10.0 air-hockey mini-game ----------

        void SendMini(byte[] data)
        {
            var u = udp;
            if (u == null || role == NetRole.None) return;
            // v0.10.7: on the host the mini stream must reach whichever client is at the
            // table. Broadcast mallets/leave to every connected client; a client sends to
            // the host only.
            if (role == NetRole.Host)
            {
                lock (clients)
                    foreach (var ep in clients)
                        try { u.Send(data, data.Length, ep); sentPackets++; } catch { }
                return;
            }
            if (remoteEP == null) return;
            u.Send(data, data.Length, remoteEP);
            sentPackets++;
        }

        public void SendMiniMallet(int player, float x, float z, int tableId)
        {
            var d = new byte[23];
            d[0] = 2; d[1] = MiniKindMallet; d[2] = (byte)player;
            Buffer.BlockCopy(BitConverter.GetBytes(x), 0, d, 3, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(z), 0, d, 7, 4);
            d[22] = (byte)Mathf.Clamp(tableId, 0, 255);
            SendMini(d);
        }

        public void SendMiniState(float x, float z, float vx, float vz, int score0, int score1, byte flags,
                                  int activePeer, int winner, int target, bool matchOver,
                                  float northX, float northZ,
                                  float puckY, float puckAlpha, float puckTilt,
                                  byte clickSeq, byte goalSeq,
                                  int hostSide, int clientSide, int tableId)
        {
            var u = udp;
            if (u == null || role != NetRole.Host) return;

            // 52-byte state: the original 22-byte body + side-based match state + the puck
            // respawn animation + the click/goal event counters + the chosen sides and table
            // id. The host sends one copy per client so byte 26 tells each client its own peer id.
            var d = new byte[52];
            d[0] = 2; d[1] = MiniKindState; d[2] = 0;
            Buffer.BlockCopy(BitConverter.GetBytes(x), 0, d, 3, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(z), 0, d, 7, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(vx), 0, d, 11, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(vz), 0, d, 15, 4);
            d[19] = (byte)Mathf.Clamp(score0, 0, 255);
            d[20] = (byte)Mathf.Clamp(score1, 0, 255);
            d[21] = flags;
            d[22] = (byte)activePeer;
            d[23] = (byte)(winner < 0 ? 255 : winner);
            d[24] = (byte)Mathf.Clamp(target, 1, 255);
            d[25] = (byte)(matchOver ? 1 : 0);
            Buffer.BlockCopy(BitConverter.GetBytes(northX), 0, d, 27, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(northZ), 0, d, 31, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(puckY), 0, d, 35, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(puckAlpha), 0, d, 39, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(puckTilt), 0, d, 43, 4);
            d[47] = clickSeq;
            d[48] = goalSeq;
            d[49] = (byte)Mathf.Clamp(hostSide, 0, 1);
            d[50] = (byte)(clientSide < 0 ? 255 : Mathf.Clamp(clientSide, 0, 1));
            d[51] = (byte)Mathf.Clamp(tableId, 0, 255);

            lock (clients)
            {
                for (int i = 0; i < clients.Count; i++)
                {
                    d[26] = (byte)(i + 1);
                    try { u.Send(d, d.Length, clients[i]); sentPackets++; } catch { }
                }
            }
        }

        public void SendMiniLeave(int player, int tableId)
        {
            var d = new byte[23];
            d[0] = 2; d[1] = MiniKindLeave; d[2] = (byte)player;
            d[22] = (byte)Mathf.Clamp(tableId, 0, 255);
            SendMini(d);
        }

        // ---------- v0.10.42 cabinet duels ----------

        /// <summary>v0.10.45: announces this build's version. Sent on start and retried until the
        /// peer answers, because the first UDP datagram of a session is the one most likely to be
        /// dropped (the host may not even have the client's endpoint yet).</summary>
        public void SendVersion()
        {
            if (role == NetRole.None) return;
            var parts = BabbdiCoopPlugin.PluginVersion.Split('.');
            var d = new byte[4];
            d[0] = 5;
            d[1] = ParsePart(parts, 0);
            d[2] = ParsePart(parts, 1);
            d[3] = ParsePart(parts, 2);
            SendMini(d);
        }

        static byte ParsePart(string[] parts, int i)
        {
            if (parts.Length <= i) return 0;
            return byte.TryParse(parts[i], out var v) ? v : (byte)0;
        }

        public void SendCabinet(int game, int msg, byte[] payload = null)
        {
            var u = udp;
            if (u == null || role == NetRole.None) return;
            int len = payload == null ? 0 : Mathf.Min(payload.Length, 1200);
            var d = new byte[4 + len];
            d[0] = 4;
            d[1] = (byte)Mathf.Clamp(game, 1, 3);
            d[2] = (byte)Mathf.Clamp(msg, 1, 255);
            d[3] = (byte)len;
            if (len > 0) Buffer.BlockCopy(payload, 0, d, 4, len);
            SendMini(d);
        }

        public void Stop()
        {
            running = false;
            role = NetRole.None;
            remoteEP = null;
            try { rxThread?.Abort(); } catch { }
            rxThread = null;
            try { udp?.Close(); } catch { }
            udp = null;
            incoming.Clear();
            miniIncoming.Clear();
            peerVersion = null;
            peerVersionMismatch = false;
            lock (clients) { clients.Clear(); clientLastSeen.Clear(); }
        }

        public void Dispose() => Stop();
    }
}
