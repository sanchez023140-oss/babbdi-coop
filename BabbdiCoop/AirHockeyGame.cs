using System;
using System.Collections;
using System.IO;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.Networking;

namespace BabbdiCoop
{
    /// <summary>
    /// v0.10.0 — the air-hockey mini-game on the House of Soviets' top floor.
    ///
    /// Press **E** near the table: the local camera detaches from the first-person controller and
    /// becomes a perspective view above the local end, and the mouse moves the local mallet. The host
    /// is authoritative for the puck and the score; each peer sends its own mallet position. Since
    /// v0.10.7 the score belongs to the goal side (red/south vs teal/north), so any client can take
    /// the north mallet as a substitute without resetting the match. The central scoreboard arch of
    /// the source asset is hidden while playing (the HUD shows the score instead).
    /// </summary>
    public class AirHockeyGame : MonoBehaviour
    {
        public Net net;
        public ManualLogSource log;

        /// <summary>Debug only ([Debug] MiniTest): teleport to the table, enter, screenshot, leave.</summary>
        public bool autoTest;
        bool autoTried;

        public bool Active { get; private set; }

        // v0.10.5: the viewer's per-frame description of the OTHER player's table stance, read by
        // Plugin.AnimateRemote to place/pose the remote avatar at the table.
        public static bool RemoteMiniActive;
        public static Vector3 RemoteMiniFeet;
        public static float RemoteMiniYaw;
        public static Vector3 RemoteMiniHand;

        // v0.10.7: the same for the second remote avatar. The host may have two clients, and
        // the one currently holding the north mallet is the one that must stand at the table.
        public static bool ExtraMiniActive;
        public static Vector3 ExtraMiniFeet;
        public static float ExtraMiniYaw;
        public static Vector3 ExtraMiniHand;

        // ---- table / visuals -------------------------------------------------
        Transform table;
        GameObject arch;

        GameObject miniRoot;
        GameObject malletLocalGo, malletRemoteGo, malletExtraGo, puckGo;
        Material matHost, matClient, matPuck;

        // ---- camera / player -------------------------------------------------
        Camera cam;
        FirstPersonController fpc;
        Transform camParent;
        Vector3 camLocalPos;
        Quaternion camLocalRot;
        bool camOrtho;
        float camSize, camFov, camNear, camFar;
        CursorLockMode savedLock;
        bool savedVisible;
        bool savedFpc;
        // v0.10.35: while the table owns the camera the whole listener is muted, but the table's
        // own click/goal source bypasses the listener volume so the SFX stay audible.
        float savedListenerVolume = 1f;
        bool savedSfxIgnore;
        readonly System.Collections.Generic.List<MonoBehaviour> savedPost = new System.Collections.Generic.List<MonoBehaviour>();
        readonly System.Collections.Generic.List<bool> savedPostEnabled = new System.Collections.Generic.List<bool>();
        readonly System.Collections.Generic.List<GameObject> hiddenHeld = new System.Collections.Generic.List<GameObject>();

        // ---- rink, in table-local metres ------------------------------------
        // v0.10.1: the source's real rink face is x ±5.0, z ±2.3 (scaled to ±2.30 x ±1.3966);
        // the old physics was larger than that face and let the puck ride onto the thick rail.
        // The dark contour is drawn at the frame's inner edge; the physics stops just inside it.
        const float RinkHalfX = 2.16f;
        const float RinkHalfZ = 1.28f;
        const float GoalHalf = 0.42f;
        const float MalletR = 0.176f;         // v0.10.2: 45% smaller than the v0.10.1 0.32 m
        const float PuckR = 0.1164375f;       // v0.10.5: +15% over the v0.10.4 0.10125
        const float PuckLift = 0.029f;        // puck centre above the rink
        const float RinkY = SovietDistrict.AirHockeyRinkTop;
        const float StartX = 1.20f;
        const float TableHalfX = 2.576f;     // table end = goal trigger point
        const float StanceOffset = 0.45f;    // feet stand just behind the table end
        const float InteractRange = 1.0f;     // v0.10.5: trigger radius doubled
        const float OpponentIntrusion = 0.20f; // 20% of the opponent's half

        // ---- game state ------------------------------------------------------
        int localSide;                 // 0 = red (local x<0), 1 = blue/teal (x>0) — chosen by the goal you approach
        Vector2 localMallet, localPrev, localVel;
        Vector2 remoteMallet, remoteTarget, remotePrev;
        bool remotePresent;
        float remoteLastClock;
        // v0.10.7: a spectator client also sees the OTHER client's active mallet.
        Vector2 extraRemoteMallet;
        bool extraRemotePresent;

        Vector2 puck, puckVel;
        // v0.10.7: these are SIDE scores, not player scores. scoreHost = points of the red
        // (local -x) side, scoreClient = points of the blue/teal (local +x) side. A substitute
        // takes over the same side and inherits the running total.
        int scoreHost, scoreClient;
        int matchTarget = 11;
        bool matchExtended;
        bool matchOver;
        int winnerSide = -1;              // 0 = red, 1 = blue
        bool hostActive, clientPresent;

        // v0.10.7: the active client peer (host: 1 or 2, 0 = none). v0.10.14 also stores the
        // side that client chose, because the host is no longer always red.
        int northPeer;
        int clientSide = -1;
        int ownPeer;
        int stateNorthPeer;
        int stateHostSide = 0;
        int stateClientSide = -1;
        float stateNorthX, stateNorthZ;
        bool scoresInit;
        float roundPauseUntil;

        // v0.10.14: the table this session is bound to (the list index from SovietDistrict).
        int localTableId = -1;

        // v0.10.11: goal-replay animation (host state) and the same visual on clients.
        bool respawnActive;
        float respawnStart;
        float puckVisualY, puckVisualAlpha = 1f, puckTilt;
        bool respawnImpactPlayed;
        float remotePuckY, remotePuckAlpha = 1f, remotePuckTilt;

        // v0.10.11: per-collision / per-goal sound events. The host increments the counters;
        // clients watch them in the state packet, so both peers hear the same events.
        byte clickSeq, goalSeq;
        byte lastClickSeq, lastGoalSeq;
        bool soundSeqInit;
        int pendingClicks;
        float lastClickAt = -1f;

        // v0.10.11: the MP3 clips are loaded once from plugins/BabbdiCoop/sounds.
        AudioSource sfx;
        AudioClip clickClip, goalClip;
        bool soundsRequested;

        float lastMalletSend = -1f, lastStateSend = -1f;
        bool hostStateSeen;
        float lastHostStateClock;
        struct PuckState
        {
            public float t;
            public Vector2 p;
            public Vector2 v;
        }
        readonly System.Collections.Generic.List<PuckState> remotePucks = new System.Collections.Generic.List<PuckState>();
        string status;
        float statusUntil;

        bool IsHost => net == null || net.role != NetRole.Client;
        int LocalPlayer => IsHost ? 0 : 1;

        public void Init(Net n, ManualLogSource l)
        {
            net = n;
            log = l;
        }

        // ------------------------------------------------------------------ audio
        void EnsureAudio()
        {
            if (table == null) return;
            if (sfx == null)
            {
                var go = new GameObject("airhockey_sfx");
                go.transform.SetParent(table, false);
                go.transform.localPosition = new Vector3(0f, PuckLift, 0f);
                sfx = go.AddComponent<AudioSource>();
                sfx.playOnAwake = false;
                sfx.spatialBlend = 0.65f;
                sfx.minDistance = 2f;
                sfx.maxDistance = 20f;
                sfx.rolloffMode = AudioRolloffMode.Linear;
                sfx.dopplerLevel = 0f;
                sfx.volume = 1f;
            }
            else if (sfx.transform.parent != table)
            {
                sfx.transform.SetParent(table, false);
                sfx.transform.localPosition = new Vector3(0f, PuckLift, 0f);
            }
            if (!soundsRequested)
            {
                soundsRequested = true;
                StartCoroutine(LoadArcadeSounds());
            }
        }

        IEnumerator LoadArcadeSounds()
        {
            string dir = Path.Combine(Path.GetDirectoryName(GlbFigureBuilder.Dir) ?? string.Empty, "sounds");
            yield return LoadOneClip(Path.Combine(dir, "click-close-sonorous-noisy.mp3"), c => clickClip = c);
            yield return LoadOneClip(Path.Combine(dir, "avtomat--banka-padaet.mp3"), c => goalClip = c);
            log?.LogInfo($"[MINI] arcade sounds ready: click={(clickClip != null ? "ok" : "missing")}, " +
                         $"goal={(goalClip != null ? "ok" : "missing")}");
        }

        IEnumerator LoadOneClip(string path, Action<AudioClip> assign)
        {
            if (!File.Exists(path))
            {
                log?.LogWarning("[MINI] sound file missing: " + path);
                yield break;
            }
            string uri = new Uri(path).AbsoluteUri;
            var req = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.MPEG);
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                log?.LogWarning($"[MINI] sound load failed ({Path.GetFileName(path)}): {req.error}");
                req.Dispose();
                yield break;
            }
            var clip = DownloadHandlerAudioClip.GetContent(req);
            req.Dispose();
            if (clip == null)
            {
                log?.LogWarning("[MINI] sound decode returned null: " + Path.GetFileName(path));
                yield break;
            }
            clip.name = Path.GetFileNameWithoutExtension(path);
            assign(clip);
            log?.LogInfo($"[MINI] sound loaded: {clip.name} ({clip.length:0.00} s)");
        }

        void PlayClickSound(float volume = 1f)
        {
            if (sfx == null || clickClip == null) return;
            sfx.pitch = UnityEngine.Random.Range(0.92f, 1.08f);
            sfx.PlayOneShot(clickClip, 0.62f * Mathf.Clamp01(volume));
        }

        void PlayGoalSound()
        {
            if (sfx == null || goalClip == null) return;
            sfx.pitch = 1f;
            sfx.PlayOneShot(goalClip, 0.95f);
        }

        /// <summary>The host raises one sound event; the state packet forwards the counter to clients.</summary>
        void QueueClick(float volume = 1f)
        {
            // A mallet resting against the puck can resolve a collision every frame; cap the
            // click stream so it reads as impacts, not a buzz.
            if (Time.time - lastClickAt < 0.03f) return;
            lastClickAt = Time.time;
            clickSeq++;
            PlayClickSound(volume);
        }

        void PlayPendingSounds()
        {
            if (pendingClicks <= 0) return;
            int n = Mathf.Min(pendingClicks, 3);
            pendingClicks = 0;
            for (int i = 0; i < n; i++) PlayClickSound();
        }

        // ------------------------------------------------------------------ puck fade
        void SetPuckAlpha(float a)
        {
            if (matPuck == null) return;
            var c = matPuck.color;
            c.a = Mathf.Clamp01(a);
            matPuck.color = c;
        }

        // ------------------------------------------------------------------ table
        /// <summary>
        /// v0.10.14: every showroom table is in SovietDistrict.AirHockeyTables. Pick the nearest
        /// one the player is standing at (goal-point trigger) and infer the side from which they
        /// approached: the closer goal is their side (0 = red, 1 = blue/teal).
        /// </summary>
        bool FindNearbyTable(out SovietDistrict.AirHockeyTableInfo info, out int side)
        {
            info = default;
            side = -1;
            if (fpc == null || SovietDistrict.AirHockeyTables.Count == 0) return false;
            Vector3 p = fpc.transform.position;
            float r2 = InteractRange * InteractRange;
            float best = float.MaxValue;
            foreach (var t in SovietDistrict.AirHockeyTables)
            {
                if (t.Root == null) continue;
                Transform tr = t.Root.transform;
                if (Mathf.Abs(p.y - tr.position.y) > 1.5f) continue;
                Vector3 g0 = tr.TransformPoint(new Vector3(-TableHalfX, 0f, 0f));
                Vector3 g1 = tr.TransformPoint(new Vector3(TableHalfX, 0f, 0f));
                float d0 = (p - g0).sqrMagnitude;
                float d1 = (p - g1).sqrMagnitude;
                float d = Mathf.Min(d0, d1);
                if (d > r2 || d >= best) continue;
                best = d;
                info = t;
                side = d0 <= d1 ? 0 : 1;
            }
            return side >= 0;
        }

        bool AcquireTable()
        {
            if (!FindNearbyTable(out var info, out var side))
            {
                table = null;
                arch = null;
                localTableId = -1;
                return false;
            }
            table = info.Root.transform;
            arch = info.Arch;
            localTableId = info.Id;
            localSide = side;
            return true;
        }

        bool NearTable()
        {
            return FindNearbyTable(out _, out _);
        }

        // ------------------------------------------------------------------ main loop
        void Update()
        {
            if (!Active)
            {
                // drop stale mini packets while nobody is at the table (the host may be practising
                // solo and broadcasting at 30 Hz; the idle peer must not accumulate them forever)
                if (net != null)
                    while (net.miniIncoming.TryDequeue(out _)) { }
                if (fpc == null) fpc = FirstPersonController.instance;
                if (autoTest && !autoTried && fpc != null && fpc.enabled && Time.time > 3f)
                {
                    autoTried = true;
                    AutoStart();
                }
                if (AcquireTable())
                {
                    if (fpc != null && fpc.enabled && Input.GetKeyDown(KeyCode.E) && NearTable())
                        StartGame();
                }
                return;
            }

            // the scene was torn down under us (level change) — leave without touching dead objects
            if (table == null || fpc == null || cam == null)
            {
                ForceStop();
                return;
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                StopGame();
                return;
            }

            DrainMini();
            PlayPendingSounds();
            UpdateLocalMallet();

            if (IsHost) HostTick();
            else ClientTick();

            UpdateRemoteMallet();
            UpdateVisuals();
        }

        // v0.10.29: keep the wind muted after every gadget/ambience Update, for the whole game.
        // v0.10.35: the listener is held at 0 too — any source, including ones created after the
        // start, is silent — while the table's sfx bypasses the listener volume.
        void LateUpdate()
        {
            if (!Active) return;
            CoopPlayerAudio.Enforce();
            AudioListener.volume = 0f;
            if (sfx != null) sfx.ignoreListenerVolume = true;
        }

        // ------------------------------------------------------------------ audio isolation
        // v0.10.35: the table game owns the camera, so the whole listener is muted (this covers
        // any wind source, even one created after entry). The table's sfx source is exempt, so
        // mallet clicks and the goal banka remain audible.
        void MuteListenerForMiniGame()
        {
            savedListenerVolume = AudioListener.volume;
            AudioListener.volume = 0f;
            if (sfx != null)
            {
                savedSfxIgnore = sfx.ignoreListenerVolume;
                sfx.ignoreListenerVolume = true;
            }
            log?.LogInfo($"[COOP] air-hockey audio muted (AudioListener {savedListenerVolume:0.00} -> 0,00, " +
                         $"table sfx exempt: {(sfx != null ? sfx.gameObject.name : "none")})");
        }

        void RestoreListenerAfterMiniGame()
        {
            AudioListener.volume = savedListenerVolume;
            if (sfx != null) sfx.ignoreListenerVolume = savedSfxIgnore;
            log?.LogInfo($"[COOP] air-hockey audio restored (AudioListener {savedListenerVolume:0.00})");
        }

        // ------------------------------------------------------------------ start / stop
        void StartGame()
        {
            fpc = FirstPersonController.instance;
            if (fpc == null) return;
            if (!AcquireTable()) return;
            cam = fpc.playerCamera != null ? fpc.playerCamera : fpc.GetComponentInChildren<Camera>();
            if (cam == null) return;

            savedFpc = fpc.enabled;
            fpc.enabled = false;

            camParent = cam.transform.parent;
            camLocalPos = cam.transform.localPosition;
            camLocalRot = cam.transform.localRotation;
            camOrtho = cam.orthographic;
            camSize = cam.orthographicSize;
            camFov = cam.fieldOfView;
            camNear = cam.nearClipPlane;
            camFar = cam.farClipPlane;

            savedLock = Cursor.lockState;
            savedVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            cam.transform.SetParent(null, true);
            // v0.10.1: a perspective camera above and just behind the local player's end, not a
            // straight orthographic plan. The table recedes in the frame; the rails read as rails.
            cam.orthographic = false;
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.30f;
            cam.farClipPlane = 80f;

            Vector3 center = table.TransformPoint(new Vector3(0f, RinkY, 0f));
            float side = localSide == 0 ? -1f : 1f;
            // v0.10.2: the whole offset is 15% shorter — 3,4 m behind the end, 4,25 m above the rink
            Vector3 camPos = center + (table.right * (side * 4.0f) + Vector3.up * 5.0f) * 0.85f;
            cam.transform.position = camPos;
            cam.transform.rotation = Quaternion.LookRotation((center + Vector3.up * 0.15f) - camPos, Vector3.up);

            // v0.10.0: the game camera's post-processing (bloom/exposure) turned the pale rink into
            // one white sheet from directly above. The top-down game runs without it; restore on exit.
            savedPost.Clear();
            savedPostEnabled.Clear();
            foreach (var mb in cam.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                if (mb.GetType().Name != "PostProcessLayer") continue;
                savedPost.Add(mb);
                savedPostEnabled.Add(mb.enabled);
                mb.enabled = false;
            }

            // v0.10.1: the game's held item (the propeller is the usual one up here) is a child
            // of the main camera. The detached top-down camera saw it as a fan of huge blades
            // across the table. Hide the in-hand props; restore them on exit.
            HideHeldItems();

            if (arch != null) arch.SetActive(false);   // clear rink; the HUD shows the score
            remotePresent = false;                     // no stale mallet on re-entry
            extraRemotePresent = false;
            BuildVisuals();
            ApplyMalletSideColors();

            // v0.10.5: the local body stands at its table end, feet just behind the goal, facing
            // the table. The partner's viewer mirrors this stance for the remote avatar.
            {
                float stanceX = (localSide == 0 ? -1f : 1f) * (TableHalfX + StanceOffset);
                Vector3 stance = table.TransformPoint(new Vector3(stanceX, 0f, 0f));
                fpc.transform.position = stance;
                fpc.transform.rotation = Quaternion.LookRotation(
                    table.right * (localSide == 0 ? 1f : -1f), Vector3.up);
            }

            localMallet = new Vector2(localSide == 0 ? -StartX : StartX, 0f);
            localPrev = localMallet;
            localVel = Vector2.zero;
            remoteMallet = new Vector2(localSide == 0 ? StartX : -StartX, 0f);
            remoteTarget = remoteMallet;
            remotePrev = remoteMallet;
            remotePresent = false;
            extraRemotePresent = false;
            puck = Vector2.zero;
            puckVel = Vector2.zero;
            scoreHost = 0;
            scoreClient = 0;
            matchTarget = 11;
            matchExtended = false;
            // v0.10.15: the table remembers its score. Re-entering a table whose match is still
            // running continues from the board's score; a finished match starts a fresh one.
            if (AirHockeyScoreboard.GetScore(localTableId, out int savedRed, out int savedBlue,
                                             out bool savedOver, out int savedTarget) && !savedOver)
            {
                scoreHost = savedRed;
                scoreClient = savedBlue;
                matchTarget = savedTarget;
                matchExtended = savedTarget == 100;
            }
            matchOver = false;
            winnerSide = -1;
            northPeer = 0;
            stateNorthPeer = 0;
            scoresInit = false;
            hostActive = IsHost;
            clientPresent = false;
            hostStateSeen = false;
            remotePucks.Clear();
            respawnActive = false;
            respawnStart = 0f;
            puckVisualY = 0f;
            puckVisualAlpha = 1f;
            puckTilt = 0f;
            respawnImpactPlayed = false;
            remotePuckY = 0f;
            remotePuckAlpha = 1f;
            remotePuckTilt = 0f;
            clickSeq = 0;
            goalSeq = 0;
            lastClickSeq = 0;
            lastGoalSeq = 0;
            soundSeqInit = false;
            pendingClicks = 0;
            lastClickAt = -1f;
            clientSide = -1;
            stateHostSide = 0;
            stateClientSide = -1;
            roundPauseUntil = Time.time + 1.2f;
            lastMalletSend = -1f;
            lastStateSend = -1f;
            RemoteMiniActive = false;
            ExtraMiniActive = false;
            EnsureAudio();
            // v0.10.35: no wind during the mini-game. The listener is muted, which also covers
            // any ambient source created later; the table's own click/goal source is exempt
            // via ignoreListenerVolume, so the mallet and goal sounds stay audible. The held
            // prop/blower scripts are disabled so Space/Fire1 cannot spin them up.
            MuteListenerForMiniGame();
            CoopPlayerAudio.MuteHeldScripts(cam, log);
            Active = true;
            AirHockeyScoreboard.SetScore(localTableId, scoreHost, scoreClient, matchOver,
                                         matchTarget);
            // v0.10.16: the idle side markers give way to the live mallets.
            AirHockeyScoreboard.SetInUse(localTableId, true);

            ShowStatus(IsHost
                ? $"ВЫ ЗА {(localSide == 0 ? "КРАСНЫХ" : "СИНИХ")} — ЖДЁМ ПАРТНЁРА"
                : "ЖДЁМ ХОЗЯИНА СТОЛА", 3.5f);
            net?.SendMiniMallet(localSide, localMallet.x, localMallet.y, localTableId);
            if (IsHost) SendState();
            log?.LogInfo($"[MINI] {(IsHost ? "host" : "client")} joined table {localTableId} " +
                         $"(side {(localSide == 0 ? "red" : "blue")}, camera 3,4 m behind that end / " +
                         $"4,25 m up, FOV 55, rink {RinkHalfX * 2f:0.00} x {RinkHalfZ * 2f:0.00} m)");
        }

        void StopGame()
        {
            if (!Active) return;
            Active = false;
            int leftTable = localTableId;      // v0.10.16: the idle markers return to this table
            if (sfx != null) sfx.Stop();

            net?.SendMiniLeave(localSide, localTableId);

            if (cam != null)
            {
                cam.orthographic = camOrtho;
                cam.orthographicSize = camSize;
                cam.fieldOfView = camFov;
                cam.nearClipPlane = camNear;
                cam.farClipPlane = camFar;
                if (camParent != null)
                {
                    cam.transform.SetParent(camParent, false);
                    cam.transform.localPosition = camLocalPos;
                    cam.transform.localRotation = camLocalRot;
                }
            }
            if (fpc != null) fpc.enabled = savedFpc;
            Cursor.lockState = savedLock;
            Cursor.visible = savedVisible;
            for (int i = 0; i < savedPost.Count; i++)
                if (savedPost[i] != null) savedPost[i].enabled = savedPostEnabled[i];
            savedPost.Clear();
            savedPostEnabled.Clear();
            ShowHeldItems();
            RestoreListenerAfterMiniGame();
            CoopPlayerAudio.RestoreHeldScripts(log);

            if (arch != null) arch.SetActive(true);
            AirHockeyScoreboard.SetInUse(leftTable, false);
            if (malletLocalGo != null) malletLocalGo.SetActive(false);
            if (malletRemoteGo != null) malletRemoteGo.SetActive(false);
            if (malletExtraGo != null) malletExtraGo.SetActive(false);
            if (puckGo != null) puckGo.SetActive(false);
            hostActive = false;
            clientPresent = false;
            hostStateSeen = false;
            remotePucks.Clear();
            RemoteMiniActive = false;
            ExtraMiniActive = false;
            northPeer = 0;
            stateNorthPeer = 0;
            clientSide = -1;
            stateClientSide = -1;
            stateHostSide = 0;
            localTableId = -1;
            log?.LogInfo("[MINI] left the air-hockey table");
        }

        /// <summary>
        /// Debug only ([Debug] MiniTest = true): put the local player at the table, enter the game,
        /// capture the real screen after 1,5 s, then leave. Used to verify the camera detach, the
        /// folded arch, the mallets/puck and the HUD without hand input.
        /// </summary>
        void AutoStart()
        {
            fpc = FirstPersonController.instance;
            if (fpc == null) return;
            var tables = SovietDistrict.AirHockeyTables;
            if (tables.Count == 0) return;
            // v0.10.14 debug: use the last showroom table (the frog one) and approach its blue
            // end, so MiniTest exercises both a custom table and the approach-side rule.
            var info = tables[tables.Count - 1];
            table = info.Root.transform;
            arch = info.Arch;
            localTableId = info.Id;
            localSide = 1;
            Vector3 stand = table.TransformPoint(new Vector3(TableHalfX + 0.324f, 0.25f, 0f));
            fpc.transform.position = stand;
            Vector3 look = table.TransformPoint(new Vector3(0f, 1.14f, 0f)) - stand;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f)
                fpc.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
            log?.LogInfo($"[MINI] MiniTest: teleported to table {localTableId} blue end " +
                         $"(near check = {NearTable()}), entering the game");
            StartGame();
            // debug-only physics proof: drive the puck through the +x goal mouth so the
            // run exercises the "fully across the line" rule (score 1:0 in the log).
            puck = new Vector2(RinkHalfX - 0.45f, 0f);
            puckVel = new Vector2(3.5f, 0f);
            roundPauseUntil = Time.time + 0.1f;
            StartCoroutine(AutoShot());
        }

        IEnumerator AutoShot()
        {
            yield return new WaitForSeconds(1.5f);
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "mini_test.png"));
            try
            {
                ScreenCapture.CaptureScreenshot(path);
                log?.LogInfo($"[MINI] MiniTest screenshot -> {path} (active={Active}, " +
                             $"puck=({puck.x:0.00},{puck.y:0.00}), " +
                             $"mallet=({localMallet.x:0.00},{localMallet.y:0.00}), " +
                             $"scores {scoreHost}:{scoreClient})");
            }
            catch (Exception e)
            {
                log?.LogWarning("[MINI] MiniTest screenshot failed: " + e.Message);
            }
            // v0.10.11 debug: capture the 1 s blackout + 1.5 s respawn animation. The goal
            // happens ~0.1-0.2 s after StartGame; these six shots at 0.18 s intervals start
            // right after the 1.5 s frame and cover the fade/fall/bounce/settle timeline.
            for (int i = 0; i < 6; i++)
            {
                yield return new WaitForSeconds(0.18f);
                string rp = Path.GetFullPath(Path.Combine(Application.dataPath, "..",
                                                          $"mini_respawn_{i}.png"));
                try
                {
                    ScreenCapture.CaptureScreenshot(rp);
                    float rt = respawnActive ? (Time.time - respawnStart) : -1f;
                    log?.LogInfo($"[MINI] respawn shot {i}: t={rt:0.00} " +
                                 $"y={puckVisualY:0.000} a={puckVisualAlpha:0.00} " +
                                 $"tilt={puckTilt:0.0} active={respawnActive}");
                }
                catch (Exception e)
                {
                    log?.LogWarning("[MINI] respawn screenshot failed: " + e.Message);
                }
            }
            StopGame();

            // v0.10.15/16: the table is idle again — prove the physical board kept the score and
            // that the two side-marker mallets are standing on the rink.
            yield return new WaitForSeconds(0.3f);
            yield return StartCoroutine(IdleShot());
            yield return StartCoroutine(BoardShot());
            yield return new WaitForSeconds(0.2f);

            // v0.10.1 regression check: enter a SECOND time and make sure the local mallet is
            // re-armed (the old StopGame hid it and BuildVisuals only re-activated the parent).
            yield return new WaitForSeconds(0.8f);
            log?.LogInfo("[MINI] MiniTest: re-entering the table (score should still be on the board)");
            StartGame();
            yield return new WaitForSeconds(1.0f);
            string path2 = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "mini_test_reentry.png"));
            try
            {
                ScreenCapture.CaptureScreenshot(path2);
                log?.LogInfo($"[MINI] MiniTest re-entry screenshot -> {path2} " +
                             $"(mallet active={malletLocalGo != null && malletLocalGo.activeSelf}, " +
                             $"active={Active})");
            }
            catch (Exception e)
            {
                log?.LogWarning("[MINI] MiniTest re-entry screenshot failed: " + e.Message);
            }
            yield return new WaitForSeconds(0.5f);
            StopGame();
        }

        /// <summary>
        /// v0.10.16 debug only: after leaving the mini-game, take a 3/4 view of the idle table so
        /// `mini_idle.png` shows both side-marker mallets on the rink (blue near right, red far
        /// left). Restores the first-person camera afterwards.
        /// </summary>
        IEnumerator IdleShot()
        {
            if (cam == null || fpc == null || table == null) yield break;
            var parent = cam.transform.parent;
            var lp = cam.transform.localPosition;
            var lr = cam.transform.localRotation;
            bool fpcWas = fpc.enabled;
            fpc.enabled = false;
            cam.transform.SetParent(null, true);
            HideHeldItems();
            Vector3 target = table.TransformPoint(new Vector3(-0.10f, 1.25f, 0f));
            Vector3 pos = table.TransformPoint(new Vector3(4.35f, 3.45f, 2.55f));
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation((target - pos).normalized, Vector3.up);
            yield return new WaitForSeconds(0.15f);
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..",
                                                        "mini_idle.png"));
            try
            {
                ScreenCapture.CaptureScreenshot(path);
                log?.LogInfo($"[MINI] idle table shot -> {path} (side markers on the rink)");
            }
            catch (Exception e)
            {
                log?.LogWarning("[MINI] idle table shot failed: " + e.Message);
            }
            yield return new WaitForSeconds(0.2f);
            cam.transform.SetParent(parent, false);
            cam.transform.localPosition = lp;
            cam.transform.localRotation = lr;
            fpc.enabled = fpcWas;
            ShowHeldItems();
        }

        /// <summary>
        /// v0.10.15 debug only: after leaving the mini-game, park the camera square in front of the
        /// blue-end board and capture `mini_scoreboard.png` (the score from the goal that was just
        /// scored must be on it). Restores the first-person camera afterwards.
        /// </summary>
        IEnumerator BoardShot()
        {
            if (cam == null || fpc == null || table == null) yield break;
            var parent = cam.transform.parent;
            var lp = cam.transform.localPosition;
            var lr = cam.transform.localRotation;
            bool fpcWas = fpc.enabled;
            fpc.enabled = false;
            cam.transform.SetParent(null, true);
            HideHeldItems();                       // the propeller would fan across the shot
            Vector3 target = table.TransformPoint(new Vector3(1.700f, 2.752f, 0f));
            Vector3 pos = table.TransformPoint(new Vector3(2.900f, 2.520f, 0f));
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation((target - pos).normalized, Vector3.up);
            yield return new WaitForSeconds(0.15f);
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..",
                                                        "mini_scoreboard.png"));
            try
            {
                ScreenCapture.CaptureScreenshot(path);
                log?.LogInfo($"[MINI] score-board shot -> {path} " +
                             $"(scores {scoreHost}:{scoreClient}, target {matchTarget})");
            }
            catch (Exception e)
            {
                log?.LogWarning("[MINI] score-board shot failed: " + e.Message);
            }
            yield return new WaitForSeconds(0.2f);
            cam.transform.SetParent(parent, false);
            cam.transform.localPosition = lp;
            cam.transform.localRotation = lr;
            fpc.enabled = fpcWas;
            ShowHeldItems();
        }

        void ForceStop()
        {
            Active = false;
            if (localTableId >= 0) AirHockeyScoreboard.SetInUse(localTableId, false);
            if (sfx != null) sfx.Stop();
            try { net?.SendMiniLeave(localSide, localTableId); } catch (System.Exception swallowed) { CoopDiag.Swallowed(swallowed); }
            ShowHeldItems();
            RestoreListenerAfterMiniGame();
            CoopPlayerAudio.RestoreHeldScripts(log);
            if (malletLocalGo != null) malletLocalGo.SetActive(false);
            if (malletRemoteGo != null) malletRemoteGo.SetActive(false);
            if (malletExtraGo != null) malletExtraGo.SetActive(false);
            if (puckGo != null) puckGo.SetActive(false);
            table = null;
            arch = null;
            miniRoot = null;
            malletLocalGo = malletRemoteGo = malletExtraGo = puckGo = null;
            cam = null;
            fpc = null;
            camParent = null;
            savedPost.Clear();
            savedPostEnabled.Clear();
            hostActive = false;
            clientPresent = false;
            hostStateSeen = false;
            remotePucks.Clear();
            RemoteMiniActive = false;
            ExtraMiniActive = false;
            northPeer = 0;
            stateNorthPeer = 0;
            clientSide = -1;
            stateClientSide = -1;
            stateHostSide = 0;
            localTableId = -1;
        }

        // ------------------------------------------------------------------ held items
        void HideHeldItems()
        {
            ShowHeldItems();
            if (cam == null) return;
            foreach (var io in Resources.FindObjectsOfTypeAll<InteractObject>())
            {
                if (io == null || !io.inHands) continue;
                if (!io.gameObject.scene.IsValid()) continue;
                if (!io.gameObject.activeSelf) continue;
                Transform t = io.transform;
                if (t != cam.transform && !t.IsChildOf(cam.transform)) continue;
                hiddenHeld.Add(io.gameObject);
                io.gameObject.SetActive(false);
            }
            if (hiddenHeld.Count > 0)
                log?.LogInfo($"[MINI] hid {hiddenHeld.Count} held item(s) from the top-down camera");
        }

        void ShowHeldItems()
        {
            for (int i = 0; i < hiddenHeld.Count; i++)
                if (hiddenHeld[i] != null) hiddenHeld[i].SetActive(true);
            hiddenHeld.Clear();
        }

        // ------------------------------------------------------------------ visuals
        void BuildVisuals()
        {
            if (miniRoot != null && miniRoot.transform.parent == table)
            {
                // v0.10.1: StopGame hides the local mallet; on the next E the old code only
                // re-activated the parent and the mallet stayed invisible. Re-arm every piece.
                miniRoot.SetActive(true);
                if (malletLocalGo != null) malletLocalGo.SetActive(true);
                if (malletRemoteGo != null) malletRemoteGo.SetActive(remotePresent);
                if (malletExtraGo != null) malletExtraGo.SetActive(extraRemotePresent);
                if (puckGo != null) puckGo.SetActive(true);
                return;
            }

            miniRoot = new GameObject("air_hockey_play");
            miniRoot.transform.SetParent(table, false);
            miniRoot.transform.localPosition = new Vector3(0f, RinkY, 0f);
            miniRoot.transform.localRotation = Quaternion.identity;
            miniRoot.transform.localScale = Vector3.one;

            // v0.10.16: the side colours are shared with the idle markers on the rink, so the
            // blue you approach at the table is exactly the blue mallet you play with.
            matHost = MakeMat(AirHockeyScoreboard.RedMalletColor, 0.35f);
            matClient = MakeMat(AirHockeyScoreboard.BlueMalletColor, 0.35f);
            matPuck = MakePuckMat(new Color(0.07f, 0.065f, 0.06f), 0.20f);

            Material localMat = localSide == 0 ? matHost : matClient;
            Material remoteMat = localSide == 0 ? matClient : matHost;
            malletLocalGo = MakeMallet("mallet_local", localMat);
            malletRemoteGo = MakeMallet("mallet_remote", remoteMat);
            malletExtraGo = MakeMallet("mallet_extra", matClient);
            malletExtraGo.SetActive(false);
            puckGo = MakePuck("puck", matPuck);
        }

        Material MakeMat(Color c, float gloss)
        {
            var sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Legacy Shaders/Diffuse");
            var m = new Material(sh) { name = "air_hockey_mat" };
            m.color = c;
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);
            return m;
        }

        void ApplyMalletSideColors()
        {
            SetMalletMaterial(malletLocalGo, localSide == 0 ? matHost : matClient);
            SetMalletMaterial(malletRemoteGo, localSide == 0 ? matClient : matHost);
        }

        void SetMalletMaterial(GameObject root, Material mat)
        {
            if (root == null || mat == null) return;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
                r.sharedMaterial = mat;
        }

        /// <summary>
        /// v0.10.11: the puck fades in after a goal. Standard's transparent blend recipe, with
        /// `_Color.a` driven by the respawn animation; opaque at alpha 1.
        /// </summary>
        Material MakePuckMat(Color c, float gloss)
        {
            var sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Legacy Shaders/Transparent/Diffuse");
            var m = new Material(sh) { name = "air_hockey_puck_mat", color = c };
            if (m.HasProperty("_Mode"))
            {
                m.SetFloat("_Mode", 3f);
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.DisableKeyword("_ALPHATEST_ON");
                m.EnableKeyword("_ALPHABLEND_ON");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.renderQueue = 3000;
            }
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);
            return m;
        }

        GameObject MakeMallet(string name, Material mat)
        {
            var root = new GameObject(name);
            root.transform.SetParent(miniRoot.transform, false);
            root.transform.localPosition = new Vector3(0f, 0.02f, 0f);

            // v0.10.2: a thin disc (0.352 m dia, 0.05 m thick) with a short, thin capsule stick
            // (0.08 m dia, 0.18 m tall, rounded top) instead of the old massive sphere knob. The
            // whole mallet is 45% smaller than v0.10.1 (collision radius 0.176 m).
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(disc.GetComponent<Collider>());
            disc.name = name + "_disc";
            disc.transform.SetParent(root.transform, false);
            disc.transform.localScale = new Vector3(0.352f, 0.02475f, 0.352f); // 0.352 m dia, 0.0495 m thick
            disc.transform.localPosition = new Vector3(0f, 0.02475f, 0f);
            disc.GetComponent<Renderer>().sharedMaterial = mat;

            var stick = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(stick.GetComponent<Collider>());
            stick.name = name + "_stick";
            stick.transform.SetParent(root.transform, false);
            stick.transform.localScale = new Vector3(0.08f, 0.09f, 0.08f);    // 0.08 m dia, 0.18 m tall
            stick.transform.localPosition = new Vector3(0f, 0.09f, 0f);       // rounded top, bottom in the disc
            stick.GetComponent<Renderer>().sharedMaterial = mat;
            return root;
        }

        GameObject MakePuck(string name, Material mat)
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(p.GetComponent<Collider>());
            p.name = name;
            p.transform.SetParent(miniRoot.transform, false);
            p.transform.localScale = new Vector3(0.232875f, 0.02716875f, 0.232875f); // v0.10.5: +15%
            p.transform.localPosition = new Vector3(0f, PuckLift, 0f);
            p.GetComponent<Renderer>().sharedMaterial = mat;
            return p;
        }

        // ------------------------------------------------------------------ local input
        void UpdateLocalMallet()
        {
            if (cam == null || table == null) return;
            Vector2 target = localMallet;
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(table.up, table.TransformPoint(new Vector3(0f, RinkY, 0f)));
            if (plane.Raycast(ray, out float enter))
            {
                Vector3 w = ray.GetPoint(enter);
                Vector3 l = table.InverseTransformPoint(w);
                target = new Vector2(l.x, l.z);
            }

            target = ClampMallet(target, localSide);

            localPrev = localMallet;
            localMallet = Vector2.MoveTowards(localMallet, target, 40f * Time.deltaTime);
            localMallet = ClampMallet(localMallet, localSide);

            // v0.10.4: mallets are solid against each other. Each peer resolves its own mallet
            // against the other player's (both use the same rule, so they stop at contact).
            if (remotePresent)
            {
                Vector2 d = localMallet - remoteMallet;
                float dist = d.magnitude;
                float min = MalletR * 2f;
                if (dist < min)
                {
                    Vector2 n = dist > 0.0001f ? d / dist : Vector2.right;
                    localMallet = ClampMallet(remoteMallet + n * min, localSide);
                }
            }

            localVel = (localMallet - localPrev) / Mathf.Max(Time.deltaTime, 0.0001f);

            if (Time.time - lastMalletSend >= 0.033f)
            {
                lastMalletSend = Time.time;
                net?.SendMiniMallet(localSide, localMallet.x, localMallet.y, localTableId);
            }
        }

        Vector2 ClampMallet(Vector2 m, int side)
        {
            float clash = RinkHalfX * OpponentIntrusion;
            float minX = side == 0 ? -RinkHalfX + MalletR : -clash;
            float maxX = side == 0 ? clash : RinkHalfX - MalletR;
            m.x = Mathf.Clamp(m.x, minX, maxX);
            m.y = Mathf.Clamp(m.y, -RinkHalfZ + MalletR, RinkHalfZ - MalletR);
            return m;
        }

        /// <summary>Table stance for a side: feet behind its end, hand at the mallet.</summary>
        void TableStance(int side, Vector2 mallet, out Vector3 feet, out float yaw, out Vector3 hand)
        {
            float stanceX = (side == 0 ? -1f : 1f) * (TableHalfX + StanceOffset);
            float stanceZ = Mathf.Clamp(mallet.y * 0.45f, -0.85f, 0.85f);
            feet = table.TransformPoint(new Vector3(stanceX, 0f, stanceZ));
            yaw = Quaternion.LookRotation(table.right * (side == 0 ? 1f : -1f), Vector3.up).eulerAngles.y;
            hand = table.TransformPoint(new Vector3(mallet.x, RinkY + 0.06f, mallet.y));
        }

        void UpdateRemoteMallet()
        {
            ExtraMiniActive = false;
            extraRemotePresent = false;
            if (table == null)
            {
                RemoteMiniActive = false;
                return;
            }
            if (remotePresent && Time.time - remoteLastClock > 1.2f)
                remotePresent = false;

            if (remotePresent)
            {
                remotePrev = remoteMallet;
                remoteMallet = Vector2.Lerp(remoteMallet, remoteTarget,
                                            1f - Mathf.Exp(-Time.deltaTime / 0.08f));
            }

            if (IsHost)
            {
                // The host has both client avatars. Pose the one that currently owns the active
                // client mallet; the other keeps its ordinary pose.
                RemoteMiniActive = false;
                if (remotePresent && northPeer != 0)
                {
                    TableStance(clientSide, remoteMallet, out var feet, out var yaw, out var hand);
                    if (northPeer == 2)
                    {
                        ExtraMiniFeet = feet;
                        ExtraMiniYaw = yaw;
                        ExtraMiniHand = hand;
                        ExtraMiniActive = true;
                    }
                    else
                    {
                        RemoteMiniFeet = feet;
                        RemoteMiniYaw = yaw;
                        RemoteMiniHand = hand;
                        RemoteMiniActive = true;
                    }
                }
                return;
            }

            // The host stands on its own chosen side. If another client is the active player
            // (this client is only watching), pose that avatar on its side too.
            if (remotePresent)
            {
                TableStance(stateHostSide, remoteMallet, out var feet, out var yaw, out var hand);
                RemoteMiniFeet = feet;
                RemoteMiniYaw = yaw;
                RemoteMiniHand = hand;
                RemoteMiniActive = true;
            }
            else RemoteMiniActive = false;

            if (stateNorthPeer != 0 && ownPeer != 0 && stateNorthPeer != ownPeer)
            {
                extraRemoteMallet = new Vector2(stateNorthX, stateNorthZ);
                extraRemotePresent = true;
                TableStance(stateClientSide, extraRemoteMallet,
                            out var feet, out var yaw, out var hand);
                ExtraMiniFeet = feet;
                ExtraMiniYaw = yaw;
                ExtraMiniHand = hand;
                ExtraMiniActive = true;
            }
        }

        // ------------------------------------------------------------------ network
        void DrainMini()
        {
            if (net == null) return;
            while (net.miniIncoming.TryDequeue(out var mp))
            {
                if (mp.kind == Net.MiniKindMallet)
                {
                    if (IsHost)
                    {
                        if (mp.tableId != localTableId) continue;
                        // v0.10.14: the client must approach the free side. If it stands at the
                        // host's end, the host rejects it (the HUD explains which side is taken).
                        if (mp.player == localSide)
                        {
                            if (!clientPresent)
                                ShowStatus("ЭТА СТОРОНА ЗАНЯТА — ПОДОЙДИ С ДРУГОГО КРАЯ", 2.5f);
                            continue;
                        }
                        if (mp.peer <= 0) continue;
                        bool newPlayer = false;
                        if (northPeer == 0)
                        {
                            northPeer = mp.peer;
                            clientSide = mp.player;
                            newPlayer = true;
                        }
                        else if (northPeer != mp.peer || clientSide != mp.player) continue;

                        remoteTarget = new Vector2(mp.x, mp.z);
                        if (!remotePresent || newPlayer)
                        {
                            remotePresent = true;
                            remoteMallet = remoteTarget;
                            remotePrev = remoteTarget;
                        }
                        remoteLastClock = Time.time;

                        if (!clientPresent)
                        {
                            clientPresent = true;
                            puck = Vector2.zero;
                            puckVel = Vector2.zero;
                            roundPauseUntil = Mathf.Max(roundPauseUntil, Time.time + 1.0f);
                            ShowStatus(newPlayer
                                ? $"ИГРОК {mp.peer} ЗА {(clientSide == 0 ? "КРАСНЫХ" : "СИНИХ")} — СЧЁТ СОХРАНЁН"
                                : "ПАРТНЁР ЗА СТОЛОМ", 2.7f);
                            log?.LogInfo($"[MINI] peer {mp.peer} took the {(clientSide == 0 ? "red" : "blue")} side; " +
                                         $"side score kept at {scoreHost}:{scoreClient}");
                        }
                    }
                    else if (mp.tableId == localTableId && mp.player != localSide)
                    {
                        // host's mallet
                        remoteTarget = new Vector2(mp.x, mp.z);
                        if (!remotePresent)
                        {
                            remotePresent = true;
                            remoteMallet = remoteTarget;
                            remotePrev = remoteTarget;
                        }
                        remoteLastClock = Time.time;
                    }
                }
                else if (mp.kind == Net.MiniKindState && !IsHost)
                {
                    if (mp.tableId != localTableId)
                    {
                        if (hostActive) ShowStatus("ПАРТНЁР ЗА ДРУГИМ СТОЛОМ", 2.5f);
                        hostActive = false;
                        continue;
                    }
                    ownPeer = mp.recipient;
                    stateNorthPeer = mp.activePeer;
                    stateHostSide = mp.hostSide;
                    stateClientSide = mp.clientSide;
                    stateNorthX = mp.northX;
                    stateNorthZ = mp.northZ;
                    remotePuckY = mp.puckY;
                    remotePuckAlpha = mp.puckAlpha;
                    remotePuckTilt = mp.puckTilt;

                    // v0.10.11: forward the host's click/goal events. The sequence bytes make a
                    // dropped state packet harmless: the next one carries the delta.
                    if (!soundSeqInit)
                    {
                        lastClickSeq = (byte)mp.clickSeq;
                        lastGoalSeq = (byte)mp.goalSeq;
                        soundSeqInit = true;
                    }
                    else
                    {
                        int dc = ((byte)mp.clickSeq - lastClickSeq) & 0xFF;
                        if (dc > 0)
                        {
                            pendingClicks = Mathf.Min(pendingClicks + dc, 3);
                            lastClickSeq = (byte)mp.clickSeq;
                        }
                        int dg = ((byte)mp.goalSeq - lastGoalSeq) & 0xFF;
                        if (dg > 0)
                        {
                            lastGoalSeq = (byte)mp.goalSeq;
                            PlayGoalSound();
                        }
                    }

                    int prevH = scoreHost, prevC = scoreClient;
                    bool prevOver = matchOver, prevExt = matchExtended;

                    hostActive = (mp.flags & 1) != 0;
                    clientPresent = (mp.flags & 2) != 0;
                    scoreHost = mp.score0;
                    scoreClient = mp.score1;
                    matchTarget = mp.target >= 100 ? 100 : 11;
                    matchExtended = matchTarget == 100;
                    matchOver = mp.matchOver != 0;
                    winnerSide = mp.winner <= 1 ? mp.winner : -1;
                    // v0.10.15: the physical board mirrors the host's score on this peer too.
                    if (prevH != scoreHost || prevC != scoreClient || prevOver != matchOver)
                        AirHockeyScoreboard.SetScore(localTableId, scoreHost, scoreClient,
                                                     matchOver, matchTarget);

                    if (scoresInit)
                    {
                        if (!prevOver && matchOver)
                            ShowStatus($"МАТЧ ОКОНЧЕН: {(winnerSide == 0 ? "КРАСНЫЕ" : "БИРЮЗОВЫЕ")} " +
                                       $"{scoreHost}:{scoreClient}", 4f);
                        else if (!prevExt && matchExtended)
                            ShowStatus("РАЗРЫВ МЕНЬШЕ 2 — МАТЧ ИДЁТ ДО 100", 3f);
                        else if (prevH != scoreHost || prevC != scoreClient)
                            ShowStatus("ГОЛ!", 1.2f);
                    }
                    scoresInit = true;

                    var target = new Vector2(mp.x, mp.z);
                    var vel = new Vector2(mp.vx, mp.vz);
                    if (!hostStateSeen) puck = target;
                    // v0.10.4: keep a short state buffer and interpolate at a fixed 25 ms delay.
                    // The old per-frame Lerp chased a moving target, so fast shots lagged and curved.
                    remotePucks.Add(new PuckState { t = Time.time, p = target, v = vel });
                    if (remotePucks.Count > 32) remotePucks.RemoveAt(0);
                    puckVel = vel;
                    hostStateSeen = true;
                    lastHostStateClock = Time.time;
                }
                else if (mp.kind == Net.MiniKindLeave)
                {
                    if (IsHost)
                    {
                        if (mp.tableId == localTableId && mp.peer > 0 &&
                            (northPeer == 0 || northPeer == mp.peer))
                        {
                            log?.LogInfo($"[MINI] peer {mp.peer} left the {(clientSide == 0 ? "red" : "blue")} side; " +
                                         $"side score kept at {scoreHost}:{scoreClient}");
                            northPeer = 0;
                            clientSide = -1;
                            clientPresent = false;
                            remotePresent = false;
                            ShowStatus("ПАРТНЁР ВЫШЕЛ — СЧЁТ СОХРАНЁН", 2.7f);
                        }
                    }
                    else if (mp.tableId == localTableId && mp.player == 0)
                    {
                        hostActive = false;
                        remotePresent = false;
                        ShowStatus("ХОЗЯИН ВЫШЕЛ", 2.5f);
                    }
                }
            }
        }

        void HostTick()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (northPeer != 0 && Time.time - remoteLastClock > 1.2f)
            {
                log?.LogInfo($"[MINI] peer {northPeer} timed out; side score kept at {scoreHost}:{scoreClient}");
                northPeer = 0;
                clientSide = -1;
                clientPresent = false;
                remotePresent = false;
                ShowStatus("ПАРТНЁР ОТОШЁЛ — СЧЁТ СОХРАНЁН", 2.7f);
            }

            if (respawnActive) UpdatePuckRespawn();

            if (!matchOver && Time.time >= roundPauseUntil)
            {
                Vector2 p = puck;
                Vector2 v = puckVel;
                bool clicked = false;

                p += v * dt;
                v *= Mathf.Exp(-0.35f * dt);

                if (HitMallet(localMallet, localVel, ref p, ref v)) clicked = true;
                if (clientPresent && HitMallet(remoteMallet, remoteVel(), ref p, ref v)) clicked = true;

                // side walls
                if (p.y > RinkHalfZ - PuckR)
                {
                    p.y = RinkHalfZ - PuckR;
                    if (v.y > 0f) { v.y = -Mathf.Abs(v.y) * 0.92f; clicked = true; }
                }
                if (p.y < -RinkHalfZ + PuckR)
                {
                    p.y = -RinkHalfZ + PuckR;
                    if (v.y < 0f) { v.y = Mathf.Abs(v.y) * 0.92f; clicked = true; }
                }

                // v0.10.7: a goal counts only when the WHOLE puck is past the goal line
                // (`p.x - PuckR >= RinkHalfX`), not when it merely touches the mouth. While
                // it is still straddling the line it stays in the recess, between the posts;
                // the defender's mallet can just reach it there (max centre 2.2764 m).
                float mouth = GoalHalf - PuckR;
                if (p.x > RinkHalfX - PuckR)
                {
                    bool fits = Mathf.Abs(p.y) <= mouth;
                    if (fits && p.x - PuckR >= RinkHalfX) { ScoreGoal(0); return; }
                    if (!fits)
                    {
                        p.x = RinkHalfX - PuckR;
                        if (v.x > 0f) { v.x = -Mathf.Abs(v.x) * 0.92f; clicked = true; }
                    }
                    else
                    {
                        if (p.y > mouth) { p.y = mouth; if (v.y > 0f) { v.y = -Mathf.Abs(v.y) * 0.92f; clicked = true; } }
                        if (p.y < -mouth) { p.y = -mouth; if (v.y < 0f) { v.y = Mathf.Abs(v.y) * 0.92f; clicked = true; } }
                    }
                }
                if (p.x < -RinkHalfX + PuckR)
                {
                    bool fits = Mathf.Abs(p.y) <= mouth;
                    if (fits && -p.x - PuckR >= RinkHalfX) { ScoreGoal(1); return; }
                    if (!fits)
                    {
                        p.x = -RinkHalfX + PuckR;
                        if (v.x < 0f) { v.x = Mathf.Abs(v.x) * 0.92f; clicked = true; }
                    }
                    else
                    {
                        if (p.y > mouth) { p.y = mouth; if (v.y > 0f) { v.y = -Mathf.Abs(v.y) * 0.92f; clicked = true; } }
                        if (p.y < -mouth) { p.y = -mouth; if (v.y < 0f) { v.y = Mathf.Abs(v.y) * 0.92f; clicked = true; } }
                    }
                }

                if (clicked) QueueClick();
                if (v.sqrMagnitude > 144f) v = v.normalized * 12f;
                puck = p;
                puckVel = v;
            }

            if (Time.time - lastStateSend >= (matchOver ? 0.25f : 0.016f)) SendState();
        }

        void StartPuckRespawn()
        {
            respawnActive = true;
            respawnStart = Time.time + 1.0f;
            respawnImpactPlayed = false;
            puck = Vector2.zero;
            puckVel = Vector2.zero;
            puckVisualY = 0.10f;
            puckVisualAlpha = 0f;
            puckTilt = 0f;
            roundPauseUntil = respawnStart + 1.5f;
        }

        /// <summary>
        /// v0.10.11 — 1 s blackout after the goal, then 1.5 s of return: 0.5 s fade-in 10 cm
        /// above the rink, a 0.35 s drop, and two small edge bounces (3.5 cm then 1.8 cm),
        /// settling at 1.5 s. The host sends y/alpha/tilt in the state packet, so a client sees
        /// exactly the same animation.
        /// </summary>
        void UpdatePuckRespawn()
        {
            float t = Time.time - respawnStart;
            if (t < 0f)
            {
                puckVisualY = 0.10f;
                puckVisualAlpha = 0f;
                puckTilt = 0f;
                return;
            }
            if (t >= 1.5f)
            {
                respawnActive = false;
                puckVisualY = 0f;
                puckVisualAlpha = 1f;
                puckTilt = 0f;
                return;
            }

            if (t < 0.5f)
            {
                puckVisualY = 0.10f;
                puckVisualAlpha = t / 0.5f;
                puckTilt = 0f;
            }
            else if (t < 0.85f)
            {
                float u = (t - 0.5f) / 0.35f;
                puckVisualY = 0.10f * (1f - u * u);
                puckVisualAlpha = 1f;
                puckTilt = 0f;
            }
            else if (t < 1.15f)
            {
                float u = (t - 0.85f) / 0.30f;
                puckVisualY = 0.035f * Mathf.Sin(Mathf.PI * u);
                puckVisualAlpha = 1f;
                puckTilt = 14f * Mathf.Sin(Mathf.PI * u);
            }
            else if (t < 1.38f)
            {
                float u = (t - 1.15f) / 0.23f;
                puckVisualY = 0.018f * Mathf.Sin(Mathf.PI * u);
                puckVisualAlpha = 1f;
                puckTilt = -9f * Mathf.Sin(Mathf.PI * u);
            }
            else
            {
                puckVisualY = 0f;
                puckVisualAlpha = 1f;
                puckTilt = 0f;
            }

            if (!respawnImpactPlayed && t >= 0.85f)
            {
                respawnImpactPlayed = true;
                QueueClick(0.55f);   // the first landing on the rink
            }
        }

        Vector2 remoteVel()
        {
            return (remoteMallet - remotePrev) / Mathf.Max(Time.deltaTime, 0.0001f);
        }

        bool HitMallet(Vector2 mp, Vector2 mv, ref Vector2 p, ref Vector2 v)
        {
            Vector2 d = p - mp;
            float dist = d.magnitude;
            float min = PuckR + MalletR;
            if (dist >= min) return false;
            Vector2 n = dist > 0.0001f ? d / dist : Vector2.right;
            p = mp + n * (min + 0.001f);
            float vn = Vector2.Dot(v, n);
            if (vn < 0f) v -= (1.9f * vn) * n;
            v += mv * 0.7f;
            if (v.sqrMagnitude > 144f) v = v.normalized * 12f;
            return true;
        }

        /// <summary>
        /// v0.10.7: side-based scoring. `side` 0 = red/south scored in the north net,
        /// 1 = teal/north scored in the south net. The match is first to 11; if the score
        /// reaches 11 with a one-goal gap, the target becomes 100 instead of ending.
        /// </summary>
        void ScoreGoal(int side)
        {
            if (side == 0) scoreHost++;
            else scoreClient++;

            int hi = Mathf.Max(scoreHost, scoreClient);
            int gap = Mathf.Abs(scoreHost - scoreClient);
            if (!matchOver)
            {
                if (matchTarget < 100)
                {
                    if (hi >= 11 && gap >= 2)
                    {
                        matchOver = true;
                        winnerSide = scoreHost > scoreClient ? 0 : 1;
                    }
                    else if (hi >= 11)
                    {
                        matchTarget = 100;
                        matchExtended = true;
                    }
                }
                else if (hi >= 100)
                {
                    matchOver = true;
                    winnerSide = scoreHost > scoreClient ? 0 : 1;
                }
            }

            string msg;
            if (matchOver)
            {
                msg = $"МАТЧ ОКОНЧЕН: {(winnerSide == 0 ? "КРАСНЫЕ" : "БИРЮЗОВЫЕ")} " +
                      $"{scoreHost}:{scoreClient}";
            }
            else if (matchExtended)
            {
                msg = $"ГОЛ! {scoreHost}:{scoreClient} — РАЗРЫВ < 2, ИГРАЕМ ДО 100";
            }
            else
            {
                msg = $"ГОЛ! {scoreHost}:{scoreClient}";
            }

            // v0.10.15: the physical board at both lamp ends takes the new score at once.
            AirHockeyScoreboard.SetScore(localTableId, scoreHost, scoreClient, matchOver,
                                         matchTarget);

            goalSeq++;
            PlayGoalSound();
            ResetRally(msg);
            if (matchOver)
            {
                puck = Vector2.zero;
                puckVel = Vector2.zero;
            }
            log?.LogInfo($"[MINI] goal in the {(side == 0 ? "north" : "south")} net -> " +
                         $"{scoreHost}:{scoreClient}, target {matchTarget}" +
                         (matchOver ? " (match over)" : ""));
            SendState();
        }

        void ResetRally(string msg)
        {
            puck = Vector2.zero;
            puckVel = Vector2.zero;
            if (!matchOver)
            {
                StartPuckRespawn();
                ShowStatus(msg, 2.7f);
            }
            else
            {
                respawnActive = false;
                puckVisualY = 0f;
                puckVisualAlpha = 0f;
                puckTilt = 0f;
                roundPauseUntil = Time.time + 1.2f;
                ShowStatus(msg, 5f);
            }
        }

        void SendState()
        {
            if (!IsHost) return;
            lastStateSend = Time.time;
            byte flags = 0;
            if (hostActive) flags |= 1;
            if (clientPresent) flags |= 2;
            if (Time.time < roundPauseUntil) flags |= 4;
            if (matchOver) flags |= 8;
            // v0.10.7: north mallet for spectators (0 when the north side is empty).
            float nx = northPeer != 0 ? remoteTarget.x : 0f;
            float nz = northPeer != 0 ? remoteTarget.y : 0f;
            net?.SendMiniState(puck.x, puck.y, puckVel.x, puckVel.y, scoreHost, scoreClient, flags,
                               northPeer, winnerSide, matchTarget, matchOver, nx, nz,
                               puckVisualY, puckVisualAlpha, puckTilt, clickSeq, goalSeq,
                               localSide, clientSide, localTableId);
        }

        void ClientTick()
        {
            if (hostStateSeen && Time.time - lastHostStateClock > 1.5f)
            {
                hostStateSeen = false;
                hostActive = false;
                remotePucks.Clear();
            }

            if (remotePucks.Count == 0) return;

            // v0.10.4: render 25 ms in the past from the host's state buffer. The host sends at
            // 60 Hz; linear interpolation between the received positions follows the real path
            // (including bounces) instead of a lagging chase.
            float rt = Time.time - 0.025f;
            int n = remotePucks.Count;
            if (rt <= remotePucks[0].t)
            {
                puck = remotePucks[0].p;
            }
            else if (rt >= remotePucks[n - 1].t)
            {
                var last = remotePucks[n - 1];
                float dt = Mathf.Clamp(rt - last.t, 0f, 0.05f);
                puck = last.p + last.v * dt;
            }
            else
            {
                int i = 0;
                while (i + 1 < n && remotePucks[i + 1].t <= rt) i++;
                var a = remotePucks[i];
                var b = remotePucks[i + 1];
                float span = b.t - a.t;
                float f = span > 0.0001f ? Mathf.Clamp01((rt - a.t) / span) : 1f;
                puck = Vector2.Lerp(a.p, b.p, f);
            }
        }

        // ------------------------------------------------------------------ per-frame visuals
        void UpdateVisuals()
        {
            if (miniRoot == null) return;
            SetMalletMaterial(malletLocalGo, localSide == 0 ? matHost : matClient);
            if (malletLocalGo != null)
            {
                if (!malletLocalGo.activeSelf) malletLocalGo.SetActive(true);
                malletLocalGo.transform.localPosition = new Vector3(localMallet.x, 0.02f, localMallet.y);
            }
            if (malletRemoteGo != null)
            {
                malletRemoteGo.SetActive(remotePresent);
                int rs = IsHost ? clientSide : stateHostSide;
                if (rs >= 0) SetMalletMaterial(malletRemoteGo, rs == 0 ? matHost : matClient);
                if (remotePresent)
                    malletRemoteGo.transform.localPosition = new Vector3(remoteMallet.x, 0.02f, remoteMallet.y);
            }
            if (malletExtraGo != null)
            {
                malletExtraGo.SetActive(extraRemotePresent);
                if (extraRemotePresent)
                {
                    if (stateClientSide >= 0)
                        SetMalletMaterial(malletExtraGo,
                                          stateClientSide == 0 ? matHost : matClient);
                    malletExtraGo.transform.localPosition =
                        new Vector3(extraRemoteMallet.x, 0.02f, extraRemoteMallet.y);
                }
            }
            if (puckGo != null)
            {
                float vy = IsHost ? puckVisualY : remotePuckY;
                float va = IsHost ? puckVisualAlpha : remotePuckAlpha;
                float vt = IsHost ? puckTilt : remotePuckTilt;
                bool show = (IsHost || hostActive) && va > 0.01f;
                puckGo.SetActive(show);
                puckGo.transform.localPosition = new Vector3(puck.x, PuckLift + vy, puck.y);
                puckGo.transform.localRotation = Quaternion.Euler(vt, 0f, vt * 0.55f);
                SetPuckAlpha(va);
            }
        }

        // ------------------------------------------------------------------ HUD
        void OnGUI()
        {
            // the game runs at 5120 x 1440 on this machine; scale the HUD with the screen height
            float ui = Mathf.Clamp(Screen.height / 1080f, 1f, 3f);

            if (!Active)
            {
                if (table != null && fpc != null && fpc.enabled && NearTable())
                {
                    float pw = 360f * ui, ph = 38f * ui;
                    var st = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = Mathf.RoundToInt(18f * ui),
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = Color.white }
                    };
                    var r = new Rect(Screen.width * 0.5f - pw * 0.5f, Screen.height - 56f * ui, pw, ph);
                    GUI.Box(r, "");
                    GUI.Label(r, "E — аэрохоккей", st);
                }
                return;
            }

            // v0.10.14: side-based scoreboard; the player's side is chosen by the goal they
            // approached. Red = local -x, Blue = local +x.
            string line = $"КРАСНЫЕ   {scoreHost} : {scoreClient}   СИНИЕ";
            string myColor = localSide == 0 ? "КРАСНЫХ" : "СИНИХ";
            string sub;
            if (matchOver)
                sub = $"МАТЧ ОКОНЧЕН — {(winnerSide == 0 ? "КРАСНЫЕ" : "СИНИЕ")} " +
                      $"ПОБЕДИЛИ (ДО {matchTarget})";
            else if (matchExtended)
                sub = "РАЗРЫВ МЕНЬШЕ 2 — МАТЧ ИДЁТ ДО 100";
            else if (!IsHost)
            {
                if (!hostActive)
                    sub = "ждём хозяина стола";
                else if (localSide == stateHostSide)
                    sub = "ЭТА СТОРОНА У ХОЗЯИНА — ПОДОЙДИ С ДРУГОГО КРАЯ";
                else if (ownPeer != 0 && stateNorthPeer != 0 && stateNorthPeer != ownPeer)
                    sub = "МЕСТО ЗАНЯТО ДРУГИМ ИГРОКОМ — ЖДЁМ";
                else
                    sub = $"ВЫ ЗА {myColor} · СЧЁТ ПО ВОРОТАМ · ДО 11";
            }
            else
            {
                sub = clientPresent
                    ? $"ВЫ ЗА {myColor} · СЧЁТ ПО ВОРОТАМ · ДО 11"
                    : $"ВЫ ЗА {myColor} — ЖДЁМ ПАРТНЁРА";
            }

            float w = 540f * ui, x = Screen.width - w - 24f * ui, y = 24f * ui;
            var scoreStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(22f * ui),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Box(new Rect(x, y, w, 42f * ui), "");
            GUI.Label(new Rect(x, y, w, 42f * ui), line, scoreStyle);

            if (!string.IsNullOrEmpty(sub))
            {
                var subStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(16f * ui),
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.82f, 0.80f, 0.72f) }
                };
                GUI.Label(new Rect(x, y + 46f * ui, w, 28f * ui), sub, subStyle);
            }

            if (!string.IsNullOrEmpty(status) && Time.time < statusUntil)
            {
                var statusStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(18f * ui),
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1f, 0.82f, 0.55f) }
                };
                GUI.Label(new Rect(x, y + 78f * ui, w, 30f * ui), status, statusStyle);
            }

            var exitStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(16f * ui),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.85f, 0.8f) }
            };
            GUI.Label(new Rect(Screen.width * 0.5f - 300f * ui, Screen.height - 46f * ui, 600f * ui, 30f * ui),
                      "E — выйти из-за стола", exitStyle);
        }

        void ShowStatus(string s, float seconds)
        {
            status = s;
            statusUntil = Time.time + seconds;
        }
    }
}
