using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;

namespace BabbdiCoop
{
    internal struct LobbyEntry
    {
        public ulong Id;
        public string Name;
        public string Version;
        public int Members;
        public int Max;
        public bool SameVersion;
    }

    /// <summary>v0.10.48: a Steam lobby as the rendezvous for the P2P transport.
    ///
    /// This Facepunch build exposes **no lobby events** (no `OnLobbyEntered`), so every async
    /// call is stored as a `Task` and its result is picked up in `Tick()` on the main thread.
    /// Nothing here may touch Unity from a continuation: `Tick()` is the only place that runs
    /// after `CreateLobbyAsync`/`JoinLobbyAsync`/`RequestAsync` complete.
    /// </summary>
    internal static class SteamLobby
    {
        public const string ModKey = "babbdi-coop";
        public const int MaxPlayers = 3;

        public static bool Hosting;
        public static ulong LobbyId;
        public static ulong JoinedOwner;      // owner of the lobby we entered (0 = none)
        public static string Status = "";
        public static readonly List<LobbyEntry> Found = new List<LobbyEntry>();
        public static bool ListOpen;
        public static int Selected;

        public static Action<string> Log;
        public static Action<string> Toast;

        static Task<Lobby?> pendingCreate;
        static Task<Lobby?> pendingJoin;
        static Task<Lobby[]> pendingQuery;
        static Lobby current;
        static bool hasCurrent;

        public static bool Busy => pendingCreate != null || pendingJoin != null || pendingQuery != null;

        public static ulong SelectedId => Found.Count == 0 ? 0 : Found[Math.Max(0, Math.Min(Selected, Found.Count - 1))].Id;

        // ------------------------------------------------------------------ actions

        public static void Create()
        {
            if (Busy || Hosting) return;
            CloseList();
            pendingCreate = SteamMatchmaking.CreateLobbyAsync(MaxPlayers);
            Status = "создаю лобби…";
            Log?.Invoke(Status);
        }

        public static void Invite()
        {
            if (!Hosting)
            {
                Say("сначала открой лобби (F5)");
                return;
            }
            try
            {
                SteamFriends.OpenGameInviteOverlay(LobbyId);
                // The overlay is only injected when Steam itself launched the game. For a
                // directly-started Babbdi.exe this call succeeds but shows nothing — the lobby
                // browser (F10) is the reliable path.
                Status = "окно приглашения (оверлей работает при запуске через Steam)";
                Log?.Invoke("invite overlay requested");
            }
            catch (Exception e) { Swallow("invite", e); }
        }

        public static void RefreshList()
        {
            if (Busy) return;
            pendingQuery = new LobbyQuery()
                .WithKeyValue("mod", ModKey)
                .WithSlotsAvailable(1)
                .WithMaxResults(20)
                .RequestAsync();
            Status = "ищу лобби…";
            Log?.Invoke(Status);
        }

        public static void Join(ulong id)
        {
            if (Busy || id == 0) return;
            pendingJoin = SteamMatchmaking.JoinLobbyAsync(id);
            Status = "вхожу в лобби…";
            Log?.Invoke($"{Status} {id}");
        }

        public static void Leave()
        {
            if (hasCurrent)
            {
                try { current.Leave(); } catch (Exception e) { Swallow("leave", e); }
                hasCurrent = false;
            }
            Hosting = false;
            LobbyId = 0;
            JoinedOwner = 0;
            Status = "";
            CloseList();
        }

        public static void CloseList()
        {
            ListOpen = false;
        }

        public static void MoveSelection(int delta)
        {
            if (Found.Count == 0) return;
            Selected = ((Selected + delta) % Found.Count + Found.Count) % Found.Count;
        }

        public static void JoinSelected()
        {
            ulong id = SelectedId;
            if (id != 0) Join(id);
        }

        // --------------------------------------------------------------------- tick

        /// <summary>Main thread only. Picks up whatever the Steam tasks finished last frame.</summary>
        public static void Tick()
        {
            if (pendingCreate != null && pendingCreate.IsCompleted)
            {
                var t = pendingCreate;
                pendingCreate = null;
                if (t.Status != TaskStatus.RanToCompletion || !t.Result.HasValue)
                {
                    Say("лобби не создалось");
                    return;
                }
                current = t.Result.Value;
                hasCurrent = true;
                try
                {
                    current.SetPublic();
                    current.SetData("mod", ModKey);
                    current.SetData("ver", BabbdiCoopPlugin.PluginVersion);
                    current.SetData("host", SteamClient.SteamId.Value.ToString());
                    current.SetData("name", SteamClient.Name ?? "host");
                }
                catch (Exception e) { Swallow("lobby data", e); }
                LobbyId = current.Id.Value;
                Hosting = true;
                Say("лобби открыто — F9 пригласить друга");
                return;
            }

            if (pendingJoin != null && pendingJoin.IsCompleted)
            {
                var t = pendingJoin;
                pendingJoin = null;
                if (t.Status != TaskStatus.RanToCompletion || !t.Result.HasValue)
                {
                    Say("не пустило в лобби");
                    return;
                }
                current = t.Result.Value;
                hasCurrent = true;
                LobbyId = current.Id.Value;
                try { JoinedOwner = current.Owner.Id.Value; }
                catch (Exception e) { Swallow("lobby owner", e); JoinedOwner = 0; }
                Hosting = false;
                CloseList();
                Say(JoinedOwner != 0 ? "лобби принято — подключаюсь" : "лобби без хозяина");
                return;
            }

            if (pendingQuery != null && pendingQuery.IsCompleted)
            {
                var t = pendingQuery;
                pendingQuery = null;
                Found.Clear();
                Selected = 0;
                Log?.Invoke($"query: status={t.Status}" +
                            (t.Exception != null ? ", error=" + t.Exception.GetBaseException().Message : ""));
                if (t.Status == TaskStatus.RanToCompletion && t.Result != null)
                {
                    Log?.Invoke($"query: Steam returned {t.Result.Length} lobby(ies)");
                    ulong me = 0;
                    try { me = SteamClient.SteamId.Value; } catch (Exception e) { Swallow("me", e); }
                    foreach (var l in t.Result)
                    {
                        try
                        {
                            if (l.Id.Value == 0) continue;
                            ulong owner = l.Owner.Id.Value;
                            if (owner == me) continue;   // our own lobby is not interesting
                            string ver = Data(l, "ver", "?");
                            Found.Add(new LobbyEntry
                            {
                                Id = l.Id.Value,
                                Name = Data(l, "name", "?"),
                                Version = ver,
                                Members = l.MemberCount,
                                Max = l.MaxMembers,
                                SameVersion = ver == BabbdiCoopPlugin.PluginVersion
                            });
                        }
                        catch (Exception e) { Swallow("lobby row", e); }
                    }
                }
                ListOpen = true;
                Say(Found.Count == 0 ? "лобби не найдено" : $"найдено лобби: {Found.Count}");
            }
        }

        // -------------------------------------------------------------------- helpers

        /// <summary>Steam appends `+connect_lobby &lt;id&gt;` when the game is launched from an
        /// invite. Only works when the game is started by that invite; if the game is already
        /// running, Steam has nowhere to deliver the request (this Facepunch build has no
        /// join-request callback) and the lobby browser is the way in.</summary>
        public static ulong ConnectLobbyFromCommandLine()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++)
                    if (string.Equals(args[i], "+connect_lobby", StringComparison.OrdinalIgnoreCase) &&
                        ulong.TryParse(args[i + 1], out var id))
                        return id;
            }
            catch (Exception e) { Swallow("cmdline", e); }
            return 0;
        }

        static string Data(Lobby l, string key, string def)
        {
            try
            {
                var v = l.GetData(key);
                return string.IsNullOrEmpty(v) ? def : v;
            }
            catch (Exception e)
            {
                Swallow("get " + key, e);
                return def;
            }
        }

        static void Say(string s)
        {
            Status = s;
            Log?.Invoke(s);
            Toast?.Invoke(s);
        }

        static void Swallow(string what, Exception e)
        {
            Log?.Invoke("ошибка: " + what + " — " + e.GetType().Name + ": " + e.Message);
        }
    }
}
