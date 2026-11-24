using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.Rendering;

namespace BabbdiCoop
{
    /// <summary>
    /// v0.10.22 — the playable "Dinozavrik" test on the arcade cabinets.
    ///
    /// E at a cabinet moves the camera to its screen and freezes the game; the first Space starts it.
    /// The foreman runs in place on the left, the ground scrolls left at 1X and the skyline at 0.6X.
    /// Space jumps, Ctrl crouches, E leaves. Crates/debris spawn in random order and end the run on
    /// contact (Space restarts).
    ///
    /// v0.10.22 fixes: the ground/skyline did not visibly move because the material's texture
    /// scale/offset was ignored by the fallback shader (Unlit/Texture is stripped from this build, so
    /// Sprites/Default ran and ignores _MainTex_ST). The scroll now writes the UVs of the two quads
    /// directly, which works with every shader. The run frames also step at 0.24 s (the user asked
    /// for half speed).
    /// </summary>
    internal class ArcadeGame : MonoBehaviour
    {
        // ---- the screen on the cabinet face (world metres; the cabinet is 2.88 m tall) --------
        const float ScreenW = 0.76f;
        const float ScreenH = 0.57f;
        const float ScreenCenterLocalY = 1.12f;   // model-local, before the cabinet's 1.6 scale
        const float ScreenFrontLocalZ = 0.99f;

        const float ScrollPx = 240f;              // v0.10.23: 2x the old 120 px/s
        const float BgFactor = 0.6f;
        // v0.10.30: the jump lasts 30 % longer (0,375 s -> 0,4875 s) at the same height:
        // speed / 1,3, gravity / 1,3².
        const float JumpSpeed = 1.5f / 1.3f;
        const float Gravity = 8f / (1.3f * 1.3f);
        const float RunFrameTime = 0.24f;         // v0.10.22: half the old 0.12 s rate
        const float ObstacleSpawnMin = 1.0f;
        const float ObstacleSpawnMax = 2.2f;

        ManualLogSource log;
        Net net;
        public bool autoTest;
        public int autoTestKind;          // v0.10.36: 1 Arkanoid, 2 Pong, 3 Snake (0 = Dino)
        public int duelTestKind;          // v0.10.42: debug host-only duel screenshot
        bool autoTried;
        bool duelTestTried;

        bool active;
        int currentKind;                  // v0.10.36: kind of the cabinet being played
        GameObject cabinet;
        GameObject root;
        Camera cam;
        FirstPersonController fpc;
        ArcadeMiniGame mini;
        Material miniMat;

        // v0.10.42: two-cabinet duels
        ArcadeDuelGame duel;
        Material duelMat;
        bool duelLobby, duelClientWait, duelActive, duelHost, duelPending;
        int duelKind, duelSeed;
        GameObject duelCabinet;
        float lastDuelSend, lastDuelRecv, duelJoinTimer;

        Transform camParent;
        Vector3 camLocalPos;
        Quaternion camLocalRot;
        bool camOrtho;
        float camSize, camFov, camNear, camFar;
        CursorLockMode savedLock;
        bool savedVisible;
        bool savedFpc;
        float savedListenerVolume = 1f;
        readonly List<MonoBehaviour> savedPost = new List<MonoBehaviour>();
        readonly List<bool> savedPostEnabled = new List<bool>();
        readonly List<GameObject> hiddenHeld = new List<GameObject>();
        readonly List<Renderer> hiddenRenderers = new List<Renderer>();
        readonly List<bool> hiddenRendererEnabled = new List<bool>();

        // ---- assets --------------------------------------------------------------------------
        bool assetsLoaded;
        Mesh quadMesh, bgMesh, groundMesh, scoreMesh;
        Texture2D bgTex, groundTex, run1Tex, run2Tex, jumpTex, fallTex, crouchTex, idleTex;
        Texture2D scoreTex;
        Material bgMat, groundMat, playerMat, scoreMat;
        // v0.10.31: the idle cabinet screen — a 20 s gameplay flipbook (10 fps, 160x120),
        // 10x20 cells in one atlas. v0.10.36: one atlas per game kind (Dino, Arkanoid, Pong,
        // Snake), plus a marquee poster per cabinet file.
        readonly Texture2D[] attractTex = new Texture2D[4];
        readonly Material[] attractMats = new Material[4];
        readonly Dictionary<string, Material> posterMats = new Dictionary<string, Material>();
        Material screenBackMat;
        Mesh screenBackMesh, screenAnimMesh, posterMesh;
        Vector2[] screenBaseUVs;
        bool attractTried, screenMeshReady, screenMeshWarned;
        bool posterReady, posterWarned;
        const int AttractColumns = 10;
        const int AttractRows = 20;
        const int AttractFrames = AttractColumns * AttractRows;
        const float AttractFps = 10f;
        static readonly string[] ObstacleNames =
        {
            "crate", "barrel", "rubble", "barrier", "cone", "pipe", "block", "rebar"
        };
        // v0.10.30: weathered, logical materials for the grey/black debris — beige concrete,
        // khaki, moss green, brick and sand (replaces the v0.10.29 pastels).
        static readonly Color RepaintBeige = new Color(0.85f, 0.78f, 0.64f);
        static readonly Color RepaintKhaki = new Color(0.64f, 0.60f, 0.40f);
        static readonly Color RepaintGreen = new Color(0.42f, 0.55f, 0.33f);
        static readonly Color RepaintBrick = new Color(0.64f, 0.34f, 0.24f);
        static readonly Color RepaintSand  = new Color(0.82f, 0.73f, 0.54f);
        Texture2D[] obstacleTex;
        Material[] obstacleMat;
        float[] obstacleHalfW;
        float[] obstacleCenterX;
        float[] obstacleTop;
        bool[] obstacleLogged;
        // v0.10.27: pixel-accurate player hitbox, one set per frame (run 1/2, jump, fall,
        // crouch, idle). All values are fractions of the 64-px sprite cell.
        Texture2D[] playerFrames;
        float[] playerHalfWf;
        float[] playerCenterXf;
        float[] playerTopFrac;
        float[] playerBottomFrac;
        float curPlayerHalfW, curPlayerCenterX, curPlayerBottom;

        // ---- game state ----------------------------------------------------------------------
        bool started;
        bool grounded = true;
        bool testCrouch;
        bool testGhost;                 // debug only: let the auto-test pass an obstacle
        bool hit;
        int score;                      // obstacles passed in this run (shown as 0000)
        float vy, jumpY;
        float bgOffset, groundOffset;
        float frameTimer;
        int frame;
        float playerX, playerBaseY, playerSize, groundTop, groundSpeed;
        Transform playerT;
        float spawnTimer;
        readonly List<Obstacle> obstacles = new List<Obstacle>();

        class Obstacle
        {
            public GameObject go;
            public Mesh mesh;
            public MeshRenderer mr;
            public float x;
            public float w, h;
            public float halfW;
            public float centerX;
            public float top;
            public bool passed;
        }

        public void Init(ManualLogSource l, Net n = null)
        {
            log = l;
            net = n;
        }

        // ------------------------------------------------------------------ update
        void Update()
        {
            EnsureAttractAssets();
            UpdateAttractScreens();
            DrainCabinetPackets();
            if (duelLobby && !duelActive) TickDuelLobby();

            if (!active)
            {
                if (autoTest && !autoTried && Time.time > 3f)
                {
                    autoTried = true;
                    StartCoroutine(AutoTest());
                }
                if (duelTestKind > 0 && !duelTestTried && Time.time > 4f)
                {
                    duelTestTried = true;
                    StartCoroutine(DuelTestRoutine());
                }
                if (duelLobby && Input.GetKeyDown(KeyCode.E))
                {
                    CancelDuelLobby();
                    return;
                }
                if (Input.GetKeyDown(KeyCode.E) && FindNearCabinet(out var cab))
                    Enter(cab);
                else if (Input.GetKeyDown(KeyCode.F) && FindNearCabinet(out var fcab))
                    TryOpenDuel(fcab);
                return;
            }

            if (cabinet == null || cam == null || fpc == null)
            {
                Exit();
                return;
            }
            if (Input.GetKeyDown(KeyCode.E))
            {
                Exit();
                return;
            }
            if (duelActive) { UpdateDuel(); return; }
            if (currentKind == 0) UpdateGame();
            else UpdateMiniGame();
        }

        // v0.10.29: after every gadget/ambience Update, keep the wind muted for the whole game.
        void LateUpdate()
        {
            if (active)
            {
                CoopPlayerAudio.Enforce();
                AudioListener.volume = 0f;
            }
        }

        /// <summary>v0.10.31: load the gameplay flipbooks once, before the player enters any
        /// cabinet, so the idle screens already play when the district is built. v0.10.36: one
        /// atlas per game kind.
        /// </summary>
        void EnsureAttractAssets()
        {
            if (attractTried) return;
            attractTried = true;
            string dir = Path.Combine(Path.GetDirectoryName(GlbFigureBuilder.Dir) ?? string.Empty,
                                      "arcade_game");
            string[] files =
            {
                "arcade_attract_atlas.png", "arkanoid_attract_atlas.png",
                "pong_attract_atlas.png", "snake_attract_atlas.png"
            };
            string[] names = { "Dinozavrik", "Arkanoid", "Pong", "Zmejka" };
            for (int k = 0; k < files.Length; k++)
            {
                string path = Path.Combine(dir, files[k]);
                attractTex[k] = LoadAttractAtlas(path);
                if (attractTex[k] == null) continue;
                attractMats[k] = MakeOpaqueMat(attractTex[k], 2100);
                if (attractMats[k].HasProperty("_Cull")) attractMats[k].SetFloat("_Cull", 0f);
                log?.LogInfo($"[ARCADE] attract atlas {names[k]}: {files[k]} " +
                             $"{attractTex[k].width}x{attractTex[k].height}, " +
                             $"{AttractColumns}x{AttractRows} cells, {AttractFrames} frames @ " +
                             $"{AttractFps:0} fps = {AttractFrames / AttractFps:0} s loop");
            }
            if (attractMats[0] == null)
                log?.LogWarning("[ARCADE] attract atlas missing — cabinet screens stay static");
            var blackTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            blackTex.SetPixel(0, 0, Color.black);
            blackTex.Apply(false);
            blackTex.name = "CoopScreenBlack";
            screenBackMat = MakeOpaqueMat(blackTex, 2050);
            if (screenBackMat.HasProperty("_Cull")) screenBackMat.SetFloat("_Cull", 0f);
        }

        /// <summary>v0.10.36: the marquee texture/material is per cabinet file (the five
        /// Dinozavrik machines still share poster 4, the six new ones each have their own).</summary>
        Material PosterMaterial(string file)
        {
            if (string.IsNullOrEmpty(file)) file = "arcade_poster.png";
            if (posterMats.TryGetValue(file, out var cached)) return cached;
            string dir = Path.Combine(Path.GetDirectoryName(GlbFigureBuilder.Dir) ?? string.Empty,
                                      "arcade_game");
            var tex = LoadPosterTex(Path.Combine(dir, file));
            if (tex == null) return null;
            var mat = MakeOpaqueMat(tex, 2110);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            posterMats[file] = mat;
            log?.LogInfo($"[ARCADE] marquee poster: {file} {tex.width}x{tex.height}");
            return mat;
        }

        Texture2D LoadAttractAtlas(string path)
        {
            if (!File.Exists(path))
            {
                log?.LogWarning("[ARCADE] attract texture missing: " + path);
                return null;
            }
            try
            {
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!t.LoadImage(File.ReadAllBytes(path))) return null;
                t.name = Path.GetFileNameWithoutExtension(path);
                t.wrapMode = TextureWrapMode.Clamp;
                t.filterMode = FilterMode.Point;   // pixel art, no atlas mip bleeding
                t.anisoLevel = 0;
                return t;
            }
            catch (Exception e)
            {
                log?.LogWarning("[ARCADE] attract texture failed: " + e.Message);
                return null;
            }
        }

        Texture2D LoadPosterTex(string path)
        {
            if (!File.Exists(path))
            {
                log?.LogWarning("[ARCADE] poster texture missing: " + path);
                return null;
            }
            try
            {
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (!t.LoadImage(File.ReadAllBytes(path))) return null;
                t.name = Path.GetFileNameWithoutExtension(path);
                t.wrapMode = TextureWrapMode.Clamp;
                t.filterMode = FilterMode.Trilinear;
                t.anisoLevel = 2;
                return t;
            }
            catch (Exception e)
            {
                log?.LogWarning("[ARCADE] poster texture failed: " + e.Message);
                return null;
            }
        }

        /// <summary>v0.10.32: the cabinet's screen is a **recessed, slanted mesh**, not a flat
        /// plane at z=0,99 — that is why the old 0,76x0,57 quad floated in front of it. This
        /// extracts the actual screen triangles from the loaded body submesh (UV island + front
        /// position filter), normalises their UVs and builds two conformal meshes: one black
        /// backing and one animation mesh a millimetre further out.</summary>
        void BuildScreenMeshes()
        {
            if (screenMeshReady) return;
            var list = SovietDistrict.ArcadeCabinetList;
            GameObject cab = null;
            for (int i = 0; i < list.Count; i++)
                if (list[i].Root != null) { cab = list[i].Root; break; }
            if (cab == null) return;
            var mf = cab.GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            var mesh = mf.sharedMesh;
            if (mesh.subMeshCount == 0) return;
            Vector3[] verts = mesh.vertices;
            Vector2[] uvs = mesh.uv;
            Vector3[] norms = mesh.normals;
            int[] tris = mesh.GetTriangles(0);

            // v0.10.35: the screen is the **recessed display island** in the 1024 body atlas.
            // GlbFigureBuilder flips glTF V (vUnity = 1 - vGlb), so the island u 0,078-0,258 /
            // v 0,588-0,718 becomes u 0,078-0,258 / v 0,282-0,412 here. The old box stopped at
            // u=0,22 and dropped the right-hand INSERT COIN/green-bars part — that was the
            // large right gap. Selecting the **whole** island (u 0,05-0,30 / v 0,24-0,46)
            // covers the complete picture; the thin strip on the cabinet's left side reuses
            // the same texels, so a position filter (x > -0,32) drops it. Because the mesh is
            // the real recess (z 0,765-0,846, tilted back at the top) the animation follows
            // the screen surface exactly and cannot spill over the bezel on any side.
            const float su0 = 0.05f, su1 = 0.30f, sv0 = 0.24f, sv1 = 0.46f;
            var selPos = new List<Vector3>();
            var selNrm = new List<Vector3>();
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                Vector2 ua = uvs[a], ub = uvs[b], uc = uvs[c];
                if (!InUvBox(ua, su0, su1, sv0, sv1) ||
                    !InUvBox(ub, su0, su1, sv0, sv1) ||
                    !InUvBox(uc, su0, su1, sv0, sv1)) continue;
                Vector3 pa = verts[a], pb = verts[b], pc = verts[c];
                Vector3 cen = (pa + pb + pc) / 3f;
                if (cen.x < -0.32f || cen.y < 0.99f || cen.z < 0.74f || cen.z > 0.87f) continue;
                Vector3 n = Vector3.Cross(pb - pa, pc - pa);
                if (n.sqrMagnitude < 0.0000001f) continue;
                n.Normalize();
                if (n.z < 0.20f) continue;
                selPos.Add(pa); selPos.Add(pb); selPos.Add(pc);
                selNrm.Add(norms[a]); selNrm.Add(norms[b]); selNrm.Add(norms[c]);
                minX = Mathf.Min(minX, Mathf.Min(pa.x, Mathf.Min(pb.x, pc.x)));
                maxX = Mathf.Max(maxX, Mathf.Max(pa.x, Mathf.Max(pb.x, pc.x)));
                minY = Mathf.Min(minY, Mathf.Min(pa.y, Mathf.Min(pb.y, pc.y)));
                maxY = Mathf.Max(maxY, Mathf.Max(pa.y, Mathf.Max(pb.y, pc.y)));
            }
            if (selPos.Count == 0)
            {
                if (!screenMeshWarned)
                {
                    screenMeshWarned = true;
                    log?.LogWarning("[ARCADE] screen mesh: no screen triangles found");
                }
                return;
            }
            float xr = Mathf.Max(0.0001f, maxX - minX);
            float yr = Mathf.Max(0.0001f, maxY - minY);
            var backVerts = new Vector3[selPos.Count];
            var animVerts = new Vector3[selPos.Count];
            var baseUV = new Vector2[selPos.Count];
            for (int i = 0; i < selPos.Count; i++)
            {
                Vector3 n = selNrm[i].normalized;
                backVerts[i] = selPos[i] + n * 0.0015f;
                animVerts[i] = selPos[i] + n * 0.0025f;
                // Planar projection over the display rectangle, not the atlas UVs: the recess
                // is tilted, and one 4:3 frame must cover it once. Local +x is screen-left for
                // the front camera, hence u = 1 - sx.
                float sx = (selPos[i].x - minX) / xr;
                float sy = (selPos[i].y - minY) / yr;
                baseUV[i] = new Vector2(1f - sx, sy);
            }
            var idx = new int[selPos.Count];
            for (int i = 0; i < idx.Length; i++) idx[i] = i;

            screenBackMesh = new Mesh { name = "coop_screen_back" };
            screenBackMesh.vertices = backVerts;
            screenBackMesh.uv = baseUV;
            screenBackMesh.triangles = idx;
            screenBackMesh.RecalculateBounds();

            screenAnimMesh = new Mesh { name = "coop_screen_anim" };
            screenAnimMesh.vertices = animVerts;
            screenAnimMesh.uv = baseUV;
            screenAnimMesh.triangles = idx;
            screenAnimMesh.RecalculateBounds();
            screenBaseUVs = baseUV;
            screenMeshReady = true;
            log?.LogInfo($"[ARCADE] screen mesh: {selPos.Count} verts / {selPos.Count / 3} tris, " +
                         $"display island x {minX:0.00}..{maxX:0.00}, y {minY:0.00}..{maxY:0.00}, " +
                         $"whole picture incl. right strip (recess-conformal, no spill)");
        }

        static bool InUvBox(Vector2 uv, float u0, float u1, float v0, float v1)
        {
            return uv.x >= u0 && uv.x <= u1 && uv.y >= v0 && uv.y <= v1;
        }

        void UpdateAttractScreens()
        {
            if (screenBackMat == null) return;
            BuildScreenMeshes();
            if (!screenMeshReady) return;
            BuildPosterMesh();
            var list = SovietDistrict.ArcadeCabinetList;
            string[] kindNames = { "Dinozavrik", "Arkanoid", "Pong", "Zmejka" };
            for (int i = 0; i < list.Count; i++)
            {
                var info = list[i];
                if (info.Root == null) continue;
                if (info.AttractBack == null)
                {
                    var back = new GameObject("CoopAttractBack");
                    back.transform.SetParent(info.Root.transform, false);
                    back.transform.localPosition = Vector3.zero;
                    back.transform.localRotation = Quaternion.identity;
                    back.transform.localScale = Vector3.one;
                    var bmf = back.AddComponent<MeshFilter>();
                    bmf.sharedMesh = screenBackMesh;
                    var bmr = back.AddComponent<MeshRenderer>();
                    bmr.sharedMaterial = screenBackMat;
                    bmr.shadowCastingMode = ShadowCastingMode.Off;
                    bmr.receiveShadows = false;
                    info.AttractBack = back;
                }

                // v0.10.33: every cabinet owns its mesh and a random phase, so the loops are
                // deliberately not in sync. The first cycle starts wherever the phase falls and
                // then runs full 20 s loops. v0.10.36: each kind has its own atlas.
                if (info.AttractScreen == null)
                {
                    var go = new GameObject("CoopAttractScreen");
                    go.transform.SetParent(info.Root.transform, false);
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localRotation = Quaternion.identity;
                    go.transform.localScale = Vector3.one;
                    var anim = new Mesh { name = "coop_screen_anim_" + info.Id };
                    anim.vertices = screenAnimMesh.vertices;
                    anim.triangles = screenAnimMesh.triangles;
                    anim.uv = screenBaseUVs;
                    anim.RecalculateBounds();
                    var mf = go.AddComponent<MeshFilter>();
                    mf.sharedMesh = anim;
                    var owner = go.AddComponent<AttractMeshOwner>();
                    owner.Mesh = anim;
                    var mr = go.AddComponent<MeshRenderer>();
                    int kind = Mathf.Clamp(info.Kind, 0, 3);
                    mr.sharedMaterial = attractMats[kind] != null ? attractMats[kind] : screenBackMat;
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    if (cabinet == info.Root) go.SetActive(false);
                    info.AttractScreen = go;
                    info.AttractMesh = anim;
                    info.AttractPhase = UnityEngine.Random.value *
                                        (AttractFrames / AttractFps);
                    info.AttractFrame = -1;
                    log?.LogInfo($"[ARCADE] attract screen {info.Id} ({kindNames[kind]}): " +
                                 $"conformal, desynced start frame " +
                                 $"{(int)(info.AttractPhase * AttractFps)}");
                }

                if (posterReady && info.Poster == null)
                {
                    var pmat = PosterMaterial(info.PosterFile);
                    if (pmat == null) continue;
                    var pgo = new GameObject("CoopMarqueePoster");
                    pgo.transform.SetParent(info.Root.transform, false);
                    pgo.transform.localPosition = Vector3.zero;
                    pgo.transform.localRotation = Quaternion.identity;
                    pgo.transform.localScale = Vector3.one;
                    var pmf = pgo.AddComponent<MeshFilter>();
                    pmf.sharedMesh = posterMesh;
                    var pmr = pgo.AddComponent<MeshRenderer>();
                    pmr.sharedMaterial = pmat;
                    pmr.shadowCastingMode = ShadowCastingMode.Off;
                    pmr.receiveShadows = false;
                    info.Poster = pgo;
                    log?.LogInfo($"[ARCADE] marquee '{info.PosterFile}' installed on " +
                                 $"cabinet {info.Id} ({kindNames[Mathf.Clamp(info.Kind, 0, 3)]})");
                }

                list[i] = info;
            }

            for (int i = 0; i < list.Count; i++)
            {
                var info = list[i];
                if (info.AttractMesh == null) continue;
                int frame = (int)(((Time.time + info.AttractPhase) * AttractFps)) % AttractFrames;
                if (frame == info.AttractFrame) continue;
                SetAttractUVs(info.AttractMesh, frame);
                info.AttractFrame = frame;
                list[i] = info;
            }
        }

        void SetAttractUVs(Mesh mesh, int frame)
        {
            int col = frame % AttractColumns;
            int row = frame / AttractColumns;
            float cu0 = col / (float)AttractColumns;
            float cu1 = (col + 1) / (float)AttractColumns;
            float cv1 = 1f - row / (float)AttractRows;
            float cv0 = 1f - (row + 1) / (float)AttractRows;
            float cw = cu1 - cu0, ch = cv1 - cv0;
            var auv = new Vector2[screenBaseUVs.Length];
            for (int i = 0; i < auv.Length; i++)
                auv[i] = new Vector2(cu0 + screenBaseUVs[i].x * cw,
                                     cv0 + screenBaseUVs[i].y * ch);
            mesh.uv = auv;
        }

        /// <summary>v0.10.33: the chosen poster 4 is mapped onto the real horizontal marquee
        /// panel (the top front face, y > 1,45, z > 0,84), again as a conformal mesh so it does
        /// not hover in front of the cabinet.</summary>
        void BuildPosterMesh()
        {
            if (posterReady) return;
            var list = SovietDistrict.ArcadeCabinetList;
            GameObject cab = null;
            for (int i = 0; i < list.Count; i++)
                if (list[i].Root != null) { cab = list[i].Root; break; }
            if (cab == null) return;
            var mf = cab.GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            var mesh = mf.sharedMesh;
            if (mesh.subMeshCount == 0) return;
            Vector3[] verts = mesh.vertices;
            Vector3[] norms = mesh.normals;
            int[] tris = mesh.GetTriangles(0);
            var selPos = new List<Vector3>();
            var selNrm = new List<Vector3>();
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                Vector3 pa = verts[a], pb = verts[b], pc = verts[c];
                Vector3 cen = (pa + pb + pc) / 3f;
                if (cen.y < 1.45f || cen.z < 0.84f) continue;
                Vector3 n = Vector3.Cross(pb - pa, pc - pa);
                if (n.sqrMagnitude < 0.0000001f) continue;
                n.Normalize();
                // only the front marquee face, not the roof plate or the inside back face
                if (n.z < 0.20f) continue;
                selPos.Add(pa); selPos.Add(pb); selPos.Add(pc);
                selNrm.Add(norms[a]); selNrm.Add(norms[b]); selNrm.Add(norms[c]);
                minX = Mathf.Min(minX, Mathf.Min(pa.x, Mathf.Min(pb.x, pc.x)));
                maxX = Mathf.Max(maxX, Mathf.Max(pa.x, Mathf.Max(pb.x, pc.x)));
                minY = Mathf.Min(minY, Mathf.Min(pa.y, Mathf.Min(pb.y, pc.y)));
                maxY = Mathf.Max(maxY, Mathf.Max(pa.y, Mathf.Max(pb.y, pc.y)));
            }
            if (selPos.Count == 0)
            {
                if (!posterWarned)
                {
                    posterWarned = true;
                    log?.LogWarning("[ARCADE] marquee poster: no marquee triangles found");
                }
                return;
            }
            float xr = Mathf.Max(0.0001f, maxX - minX);
            float yr = Mathf.Max(0.0001f, maxY - minY);
            var pv = new Vector3[selPos.Count];
            var puv = new Vector2[selPos.Count];
            for (int i = 0; i < selPos.Count; i++)
            {
                Vector3 n = selNrm[i].normalized;
                pv[i] = selPos[i] + n * 0.002f;
                float sx = (selPos[i].x - minX) / xr;
                float sy = (selPos[i].y - minY) / yr;
                puv[i] = new Vector2(1f - sx, sy);
            }
            var ptris = new int[selPos.Count];
            for (int i = 0; i < ptris.Length; i++) ptris[i] = i;
            posterMesh = new Mesh { name = "coop_marquee_poster" };
            posterMesh.vertices = pv;
            posterMesh.uv = puv;
            posterMesh.triangles = ptris;
            posterMesh.RecalculateBounds();
            posterReady = true;
            log?.LogInfo($"[ARCADE] marquee poster mesh: {selPos.Count} verts / " +
                         $"{selPos.Count / 3} tris on the top panel");
        }

        int CabinetKind(GameObject cab)
        {
            for (int i = 0; i < SovietDistrict.ArcadeCabinetList.Count; i++)
            {
                var info = SovietDistrict.ArcadeCabinetList[i];
                if (info.Root == cab) return info.Kind;
            }
            return 0;
        }

        bool FindNearCabinet(out GameObject found)
        {
            found = null;
            var p = FirstPersonController.instance;
            if (p == null) return false;
            Vector3 pos = p.transform.position;
            float best = 2.0f;
            for (int i = 0; i < SovietDistrict.ArcadeCabinetList.Count; i++)
            {
                var info = SovietDistrict.ArcadeCabinetList[i];
                if (info.Root == null || !info.Root.activeInHierarchy) continue;
                var tr = info.Root.transform;
                if (Mathf.Abs(pos.y - tr.position.y) > 2f) continue;
                Vector3 front = tr.TransformPoint(new Vector3(0f, 0.2f, ScreenFrontLocalZ));
                float d = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(front.x, front.z));
                if (d < best)
                {
                    best = d;
                    found = info.Root;
                }
            }
            return found != null;
        }

        // ------------------------------------------------------------------ duel lobby / net
        void TryOpenDuel(GameObject cab)
        {
            int kind = CabinetKind(cab);
            if (kind == 0)
            {
                log?.LogInfo("[DUEL] Dinozavrik has no duel version");
                return;
            }
            if (net == null || net.role == NetRole.None)
            {
                log?.LogWarning("[DUEL] duels require a LAN session");
                return;
            }
            if (duelLobby || duelActive) { CancelDuelLobby(); return; }
            duelKind = kind;
            duelCabinet = cab;
            duelLobby = true;
            duelHost = net.role == NetRole.Host;
            duelClientWait = !duelHost;
            duelJoinTimer = 0f;
            if (duelHost)
            {
                log?.LogInfo($"[DUEL] {GameName(kind)} lobby open — waiting for the second cabinet");
            }
            else
            {
                net.SendCabinet(duelKind, 1, null);
                log?.LogInfo($"[DUEL] {GameName(kind)} join request sent");
            }
        }

        void TickDuelLobby()
        {
            if (duelCabinet == null) { CancelDuelLobby(); return; }
            var f = FirstPersonController.instance;
            if (f != null)
            {
                Vector3 front = duelCabinet.transform.TransformPoint(
                    new Vector3(0f, 0.2f, ScreenFrontLocalZ));
                float d = Vector2.Distance(new Vector2(f.transform.position.x, f.transform.position.z),
                                           new Vector2(front.x, front.z));
                if (d > 2.6f) { CancelDuelLobby(); return; }
            }
            if (duelClientWait)
            {
                duelJoinTimer += Time.deltaTime;
                if (duelJoinTimer > 1.2f)
                {
                    duelJoinTimer = 0f;
                    net?.SendCabinet(duelKind, 1, null);
                }
            }
        }

        void CancelDuelLobby()
        {
            if (duelLobby && net != null) net.SendCabinet(duelKind, 6, null);
            duelLobby = false;
            duelClientWait = false;
            duelCabinet = null;
            duelKind = 0;
            log?.LogInfo("[DUEL] lobby closed");
        }

        void BeginDuel(bool host)
        {
            duelHost = host;
            duelPending = true;
            duelLobby = false;
            duelClientWait = false;
            Enter(duelCabinet);
            duelActive = duel != null;
            lastDuelRecv = Time.time;
            lastDuelSend = Time.time;
            log?.LogInfo($"[DUEL] {GameName(duelKind)} started ({(host ? "host" : "client")})");
        }

        void DrainCabinetPackets()
        {
            if (net == null || (!duelLobby && !duelActive)) return;
            while (net.cabinetIncoming.TryDequeue(out var p))
            {
                if (p.game != duelKind) continue;
                if (!duelActive)
                {
                    if (duelLobby && !duelHost && p.msg == 2)
                    {
                        duelSeed = p.data != null && p.data.Length >= 4
                            ? BitConverter.ToInt32(p.data, 0) : UnityEngine.Random.Range(1, 999999);
                        BeginDuel(false);
                    }
                    else if (duelLobby && duelHost && p.msg == 1)
                    {
                        duelSeed = UnityEngine.Random.Range(1, 999999);
                        net.SendCabinet(duelKind, 2, BitConverter.GetBytes(duelSeed));
                        BeginDuel(true);
                    }
                    else if (p.msg == 6)
                    {
                        CancelDuelLobby();
                    }
                    continue;
                }
                lastDuelRecv = Time.time;
                if (duel == null) continue;
                switch (p.msg)
                {
                    case 3:
                        if (!duelHost)
                        {
                            if (duelKind == 2) duel.ApplyPongState(p.data);
                            else if (duelKind == 3) duel.ApplySnakeState(p.data);
                        }
                        break;
                    case 4:
                        if (duelHost && duelKind == 2) duel.ApplyPongInput(p.data);
                        else if (duelHost && duelKind == 3) duel.ApplySnakeInput(p.data);
                        break;
                    case 5:
                        if (duelKind == 1) duel.ApplyArkanoidStatus(p.data);
                        break;
                    case 6:
                        EndDuel("СОПЕРНИК ВЫШЕЛ");
                        return;
                }
            }
        }

        void UpdateDuel()
        {
            if (duel == null) { EndDuel("ERROR"); return; }
            if (net != null && net.role != NetRole.None && Time.time - lastDuelRecv > 3f)
            {
                EndDuel("PEER TIMEOUT");
                return;
            }
            duel.Update(Time.deltaTime);
            if (duelHost)
            {
                if (duelKind == 2 && Time.time - lastDuelSend > 1f / 30f)
                {
                    lastDuelSend = Time.time;
                    net?.SendCabinet(duelKind, 3, duel.BuildPongState());
                }
                else if (duelKind == 3 && Time.time - lastDuelSend > 1f / 15f)
                {
                    lastDuelSend = Time.time;
                    net?.SendCabinet(duelKind, 3, duel.BuildSnakeState());
                }
                else if (duelKind == 1 && (Time.time - lastDuelSend > 0.35f || duel.Dirty))
                {
                    lastDuelSend = Time.time;
                    duel.Dirty = false;
                    net?.SendCabinet(duelKind, 5, duel.BuildArkanoidStatus());
                }
            }
            else
            {
                if (duelKind == 2 && Time.time - lastDuelSend > 1f / 30f)
                {
                    lastDuelSend = Time.time;
                    net?.SendCabinet(duelKind, 4, duel.BuildPongInput());
                }
                else if (duelKind == 3)
                {
                    bool force = Time.time - lastDuelSend > 0.5f;
                    var input = duel.BuildSnakeInput(force);
                    if (input != null)
                    {
                        lastDuelSend = Time.time;
                        net?.SendCabinet(duelKind, 4, input);
                    }
                }
                else if (duelKind == 1 && (Time.time - lastDuelSend > 0.35f || duel.Dirty))
                {
                    lastDuelSend = Time.time;
                    duel.Dirty = false;
                    net?.SendCabinet(duelKind, 5, duel.BuildArkanoidStatus());
                }
            }
        }

        void EndDuel(string reason)
        {
            log?.LogInfo("[DUEL] ended: " + reason);
            if (duelActive && net != null) net.SendCabinet(duelKind, 6, null);
            duelActive = false;
            duelLobby = false;
            duelClientWait = false;
            if (active) Exit();
        }

        static string GameName(int kind) =>
            kind == 1 ? "Арканоид" : kind == 2 ? "Понг" : kind == 3 ? "Змейка" : "Динозаврик";

        // ------------------------------------------------------------------ enter / exit
        void Enter(GameObject cab)
        {
            if (active || cab == null) { log?.LogInfo("[DUEL] Enter skipped: active=" + active + " cab=" + (cab != null)); return; }
            fpc = FirstPersonController.instance;
            if (fpc == null) { log?.LogInfo("[DUEL] Enter skipped: no FirstPersonController"); return; }
            cam = fpc.playerCamera != null ? fpc.playerCamera : fpc.GetComponentInChildren<Camera>();
            if (cam == null) return;

            cabinet = cab;
            int kind = 0;
            for (int i = 0; i < SovietDistrict.ArcadeCabinetList.Count; i++)
            {
                var info = SovietDistrict.ArcadeCabinetList[i];
                if (info.Root == cab)
                {
                    kind = info.Kind;
                    if (info.AttractScreen != null) info.AttractScreen.SetActive(false);
                }
            }
            currentKind = kind;
            savedFpc = fpc.enabled;
            fpc.enabled = false;
            // v0.10.24: the controller's fall wind freezes at its last volume when Update stops —
            // mute it for the mini-game (the controller restores its own volume when re-enabled).
            CoopPlayerAudio.MuteFallWind(fpc, log);
            CoopPlayerAudio.MuteAmbientWind(log);
            // v0.10.34: the cabinet game has no sounds of its own. Silence the whole listener
            // for its duration so no level wind/gadget loop can leak through, then restore the
            // exact previous listener volume on exit.
            savedListenerVolume = AudioListener.volume;
            AudioListener.volume = 0f;
            log?.LogInfo($"[COOP] cabinet audio muted (AudioListener {savedListenerVolume:0.00} -> 0,00)");

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
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            cam.transform.SetParent(null, true);

            HideHeldItems();
            CoopPlayerAudio.MuteHeldAudio(cam, log);
            DisablePost();

            if (duelPending)
            {
                BuildDuelGame(cab, kind, duelHost, duelSeed);
                duelPending = false;
            }
            else if (kind == 0)
            {
                BuildGame(cab);
                ResetRun(false);
            }
            else
            {
                BuildMiniGame(cab, kind);
            }
            active = true;

            string[] kindNames = { "Dinozavrik", "Arkanoid", "Pong", "Zmejka" };
            log?.LogInfo($"[ARCADE] entered cabinet {cab.name} ({kindNames[kind]}) — " +
                         $"press Space to start, E = leave");
        }

        void Exit()
        {
            if (!active) return;
            if (duelActive && net != null) net.SendCabinet(duelKind, 6, null);
            active = false;
            duelActive = false;
            duelLobby = false;
            duelPending = false;
            duelClientWait = false;
            ClearObstacles();
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
            RestorePost();
            ShowHeldItems();
            CoopPlayerAudio.RestoreAmbientWind(log);
            CoopPlayerAudio.RestoreHeldAudio(log);
            AudioListener.volume = savedListenerVolume;
            log?.LogInfo($"[COOP] cabinet audio restored (AudioListener {savedListenerVolume:0.00})");
            if (root != null) Destroy(root);
            root = null;
            if (scoreMesh != null) Destroy(scoreMesh);
            scoreMesh = null;
            if (miniMat != null) Destroy(miniMat);
            miniMat = null;
            if (mini != null) { mini.Destroy(); mini = null; }
            if (duelMat != null) Destroy(duelMat);
            duelMat = null;
            if (duel != null) { duel.Destroy(); duel = null; }
            currentKind = 0;
            if (cabinet != null)
            {
                for (int i = 0; i < SovietDistrict.ArcadeCabinetList.Count; i++)
                {
                    var info = SovietDistrict.ArcadeCabinetList[i];
                    if (info.Root == cabinet && info.AttractScreen != null)
                        info.AttractScreen.SetActive(true);
                }
            }
            playerT = null;
            cabinet = null;
            cam = null;
            fpc = null;
            log?.LogInfo("[ARCADE] left the arcade cabinet");
        }

        // ------------------------------------------------------------------ game build
        void BuildGame(GameObject cab)
        {
            if (quadMesh == null) quadMesh = MakeQuad();
            if (!assetsLoaded) LoadAssets();
            if (root != null) Destroy(root);
            ClearObstacles();

            Vector3 screen = cab.transform.TransformPoint(
                new Vector3(0f, ScreenCenterLocalY, ScreenFrontLocalZ));
            Vector3 fwd = cab.transform.forward;

            root = new GameObject("CoopDinoGame");
            root.transform.position = screen + fwd * 0.01f;
            // The camera looks along -fwd, so without this turn the whole 4:3 face would be
            // mirrored (the player ended up on the right and facing left in the first test).
            root.transform.rotation = cab.transform.rotation * Quaternion.Euler(0f, 180f, 0f);

            if (bgMat == null || groundMat == null || playerMat == null)
            {
                log?.LogWarning("[DINO] game materials are missing — the screen will be empty");
                return;
            }

            bgMesh = MakeQuad();
            groundMesh = MakeQuad();
            SetScrollUV(bgMesh, 0f, 320f / 1280f);
            SetScrollUV(groundMesh, 0f, 320f / 640f);

            AddQuad("bg", ScreenW, ScreenH, new Vector3(0f, 0f, -0.001f), bgMat, bgMesh);
            float groundH = ScreenH * 64f / 240f;
            groundTop = -ScreenH * 0.5f + groundH;
            AddQuad("ground", ScreenW, groundH,
                    new Vector3(0f, -ScreenH * 0.5f + groundH * 0.5f, -0.002f), groundMat, groundMesh);

            playerSize = ScreenW * 64f / 320f;
            playerX = -ScreenW * 0.5f + ScreenW * 0.16f;
            playerBaseY = groundTop + playerSize * 0.5f;
            var pg = AddQuad("player", playerSize, playerSize,
                             new Vector3(playerX, playerBaseY, -0.003f), playerMat);
            playerT = pg.transform;
            groundSpeed = ScrollPx / 320f * ScreenW;

            if (playerHalfWf != null && playerHalfWf.Length > 0)
            {
                float runH = (playerTopFrac[0] - playerBottomFrac[0]) * playerSize;
                float crH = (playerTopFrac[4] - playerBottomFrac[4]) * playerSize;
                log?.LogInfo($"[DINO] player pixel hitbox: run halfW=" +
                             $"{playerHalfWf[0] * playerSize:0.000} m, height={runH:0.000} m; " +
                             $"crouch halfW={playerHalfWf[4] * playerSize:0.000} m, " +
                             $"height={crH:0.000} m (alpha bbox, not the 64-px cell)");
            }
            log?.LogInfo($"[DINO] jump: v={JumpSpeed:0.000} m/s, g={Gravity:0.000}, " +
                         $"airtime={2f * JumpSpeed / Gravity:0.000} s, " +
                         $"height={JumpSpeed * JumpSpeed / (2f * Gravity):0.000} m " +
                         $"(v0.10.30: +30 % duration at the same height)");
            log?.LogInfo($"[DINO] speed: base {ScrollPx:0} px/s, +15 % after every 10 " +
                         $"passed obstacles; obstacles are 1,30x larger");

            BuildScoreDisplay();

            cam.transform.position = screen + fwd * 0.85f;
            cam.transform.rotation = Quaternion.LookRotation(-fwd, Vector3.up);
            cam.orthographic = false;
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 10f;
        }

        /// <summary>v0.10.28: four 16x20 px score cells in the screen's top-right corner, one
        /// mesh and one draw call. The atlas cells are keyed, so only the chrome digits show.</summary>
        void BuildScoreDisplay()
        {
            if (scoreMat == null) return;
            if (scoreMesh != null) Destroy(scoreMesh);
            scoreMesh = new Mesh { name = "dino_score" };

            const float cellW = ScreenW * 16f / 320f;
            const float cellH = ScreenH * 20f / 240f;
            float marginX = ScreenW * 8f / 320f;
            float marginY = ScreenH * 8f / 240f;
            float left = -ScreenW * 0.5f + marginX;      // v0.10.30: top-left corner
            float top = ScreenH * 0.5f - marginY;
            float bottom = top - cellH;

            var verts = new Vector3[16];
            var tris = new int[24];
            for (int d = 0; d < 4; d++)
            {
                float x0 = left + d * cellW;
                float x1 = x0 + cellW;
                int v = d * 4;
                verts[v + 0] = new Vector3(x0, bottom, 0f);
                verts[v + 1] = new Vector3(x1, bottom, 0f);
                verts[v + 2] = new Vector3(x1, top, 0f);
                verts[v + 3] = new Vector3(x0, top, 0f);
                int t = d * 6;
                tris[t + 0] = v + 0; tris[t + 1] = v + 2; tris[t + 2] = v + 1;
                tris[t + 3] = v + 0; tris[t + 4] = v + 3; tris[t + 5] = v + 2;
            }
            scoreMesh.vertices = verts;
            scoreMesh.triangles = tris;
            scoreMesh.RecalculateBounds();

            var go = new GameObject("score");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.004f);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = scoreMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = scoreMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            UpdateScoreDisplay();
            log?.LogInfo("[DINO] score display: 4 digits at the screen's top-left " +
                         $"(cells {16f}x{20f} px, margin 8 px, initial {score:0000})");
        }

        /// <summary>v0.10.28: zero-padded four digits, the atlas cell is digit/10.</summary>
        void UpdateScoreDisplay()
        {
            if (scoreMesh == null) return;
            int value = Mathf.Clamp(score, 0, 9999);
            var uvs = new Vector2[16];
            for (int d = 0; d < 4; d++)
            {
                int digit = d == 0 ? value / 1000 : d == 1 ? (value / 100) % 10 :
                            d == 2 ? (value / 10) % 10 : value % 10;
                float u0 = digit / 10f;
                float u1 = (digit + 1) / 10f;
                int v = d * 4;
                uvs[v + 0] = new Vector2(u0, 0f);
                uvs[v + 1] = new Vector2(u1, 0f);
                uvs[v + 2] = new Vector2(u1, 1f);
                uvs[v + 3] = new Vector2(u0, 1f);
            }
            scoreMesh.uv = uvs;
        }

        GameObject AddQuad(string name, float w, float h, Vector3 pos, Material mat, Mesh mesh = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(w, h, 1f);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh != null ? mesh : quadMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        Mesh MakeQuad()
        {
            var m = new Mesh { name = "dino_quad" };
            m.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f)
            };
            m.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(1f, 1f), new Vector2(0f, 1f)
            };
            // the quad faces local -z, towards the game camera (the root is turned 180 deg)
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>v0.10.22: scroll by writing the quad's UVs, not material texture offset/scale.
        /// `Unlit/Texture` is not in this build's shader list, so the fallback ignored _MainTex_ST
        /// and the ground/skyline stood still.</summary>
        void SetScrollUV(Mesh m, float offset, float scale)
        {
            if (m == null) return;
            m.uv = new[]
            {
                new Vector2(offset, 0f), new Vector2(offset + scale, 0f),
                new Vector2(offset + scale, 1f), new Vector2(offset, 1f)
            };
        }

        // ------------------------------------------------------------------ textures / materials
        void LoadAssets()
        {
            assetsLoaded = true;
            string dir = Path.Combine(Path.GetDirectoryName(GlbFigureBuilder.Dir) ?? string.Empty,
                                      "arcade_game");
            bgTex = LoadTex(Path.Combine(dir, "bg_loop.png"), true);
            groundTex = LoadTex(Path.Combine(dir, "ground_loop.png"), true);
            run1Tex = LoadTex(Path.Combine(dir, "player_run.png"), false);
            run2Tex = LoadTex(Path.Combine(dir, "player_run_2.png"), false);
            jumpTex = LoadTex(Path.Combine(dir, "player_jump.png"), false);
            fallTex = LoadTex(Path.Combine(dir, "player_fall.png"), false);
            crouchTex = LoadTex(Path.Combine(dir, "player_crouch.png"), false);
            idleTex = LoadTex(Path.Combine(dir, "player_idle.png"), false);
            // v0.10.25: the generated frames face left; the runner runs to the right, so mirror
            // every player frame once at load (the background/ground/obstacles are untouched).
            FlipTextureX(run1Tex);
            FlipTextureX(run2Tex);
            FlipTextureX(jumpTex);
            FlipTextureX(fallTex);
            FlipTextureX(crouchTex);
            FlipTextureX(idleTex);
            // v0.10.27: pixel hitboxes for the player, per frame, measured after the mirror.
            playerFrames = new[] { run1Tex, run2Tex, jumpTex, fallTex, crouchTex, idleTex };
            playerHalfWf = new float[playerFrames.Length];
            playerCenterXf = new float[playerFrames.Length];
            playerTopFrac = new float[playerFrames.Length];
            playerBottomFrac = new float[playerFrames.Length];
            for (int i = 0; i < playerFrames.Length; i++)
                AlphaBounds(playerFrames[i], out playerHalfWf[i], out playerCenterXf[i],
                            out playerTopFrac[i], out playerBottomFrac[i]);
            // The background and the ground must be **opaque** (ZWrite on): Sprites/Default is a
            // transparent shader with ZWrite off, so the farther skyline was drawn after the nearer
            // ground and painted over it — the light asphalt never appeared.
            bgMat = MakeOpaqueMat(bgTex, 2000);
            groundMat = MakeOpaqueMat(groundTex, 2000);
            playerMat = MakeMat("Unlit/Transparent", run1Tex, 3000);
            // v0.10.28: the four-digit score, packed into one 1280x160 atlas from the user's
            // `arcade_digits/v3_chrome` font (10 cells of 128x160, background keyed out).
            string scorePath = Path.Combine(dir, "score_digits_chrome.png");
            scoreTex = LoadScoreTex(scorePath);
            scoreMat = MakeMat("Unlit/Transparent", scoreTex, 3100);
            if (scoreTex != null)
                log?.LogInfo($"[DINO] score atlas: {Path.GetFileName(scorePath)} " +
                             $"{scoreTex.width}x{scoreTex.height}, 10 cells " +
                             $"(digits_v3_chrome, initial 0000)");
            else
                log?.LogWarning("[DINO] score atlas is missing — the counter will be empty");

            obstacleTex = new Texture2D[ObstacleNames.Length];
            obstacleMat = new Material[ObstacleNames.Length];
            obstacleHalfW = new float[ObstacleNames.Length];
            obstacleCenterX = new float[ObstacleNames.Length];
            obstacleTop = new float[ObstacleNames.Length];
            obstacleLogged = new bool[ObstacleNames.Length];
            for (int i = 0; i < ObstacleNames.Length; i++)
            {
                obstacleTex[i] = LoadTex(Path.Combine(dir, "obstacle_" + ObstacleNames[i] + ".png"),
                                         false);
                // v0.10.24: a gentle lift, not neon — the debris must read against the dark
                // skyline while staying in the game's muted palette.
                BrightenObstacle(obstacleTex[i]);
                // v0.10.29: debris that is mostly grey/black gets a pastel retint (>=50 % of the
                // visible pixels), preserving the original luminance and all coloured details.
                RepaintObstacle(obstacleTex[i], ObstacleNames[i], i);
                obstacleMat[i] = MakeMat("Unlit/Transparent", obstacleTex[i], 3000);
                // normalized: fractions of the sprite's width/height (see SpawnObstacle)
                AlphaBounds(obstacleTex[i], out obstacleHalfW[i], out obstacleCenterX[i],
                            out obstacleTop[i], out _);
            }
            for (int i = 0; i < ObstacleNames.Length; i++)
                log?.LogInfo($"[DINO] obstacle bbox '{ObstacleNames[i]}': halfW=" +
                             $"{obstacleHalfW[i]:0.000}, centre={obstacleCenterX[i]:+0.000;-0.000;0.000}, " +
                             $"top={obstacleTop[i]:0.000} of the 64-px cell");
        }

        Texture2D LoadTex(string path, bool repeat)
        {
            if (!File.Exists(path))
            {
                log?.LogWarning("[DINO] texture missing: " + path);
                return null;
            }
            try
            {
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!t.LoadImage(File.ReadAllBytes(path)))
                {
                    log?.LogWarning("[DINO] texture failed to decode: " + path);
                    return null;
                }
                t.name = Path.GetFileNameWithoutExtension(path);
                t.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                t.filterMode = FilterMode.Point;
                t.anisoLevel = 0;
                return t;
            }
            catch (Exception e)
            {
                log?.LogWarning("[DINO] texture failed: " + e.Message);
                return null;
            }
        }

        /// <summary>v0.10.28: the score atlas is filtered, not a pixel font — Trilinear with a
        /// mip chain keeps the chrome digits clean at their 16x20 px on-screen size.</summary>
        Texture2D LoadScoreTex(string path)
        {
            if (!File.Exists(path))
            {
                log?.LogWarning("[DINO] texture missing: " + path);
                return null;
            }
            try
            {
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (!t.LoadImage(File.ReadAllBytes(path)))
                {
                    log?.LogWarning("[DINO] score atlas failed to decode: " + path);
                    return null;
                }
                t.name = Path.GetFileNameWithoutExtension(path);
                t.wrapMode = TextureWrapMode.Clamp;
                t.filterMode = FilterMode.Trilinear;
                t.anisoLevel = 1;
                return t;
            }
            catch (Exception e)
            {
                log?.LogWarning("[DINO] score atlas failed: " + e.Message);
                return null;
            }
        }

        Material MakeMat(string shaderName, Texture2D tex, int queue)
        {
            var sh = Shader.Find(shaderName);
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            if (sh == null) return null;
            var m = new Material(sh) { name = "CoopDino_" + shaderName, mainTexture = tex };
            m.renderQueue = queue;
            return m;
        }

        /// <summary>
        /// Opaque, unlit-looking material for the background/ground. Standard is present and writes
        /// depth; the texture is fed through emission so the level's black ambient cannot darken it.
        /// </summary>
        Material MakeOpaqueMat(Texture2D tex, int queue)
        {
            var sh = Shader.Find("Standard");
            if (sh == null) return MakeMat("Unlit/Texture", tex, queue);
            var m = new Material(sh) { name = "CoopDino_opaque", mainTexture = tex };
            m.color = Color.white;
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.05f);
            if (m.HasProperty("_EmissionMap"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", tex);
                m.SetColor("_EmissionColor", Color.white);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            m.renderQueue = queue;
            return m;
        }

        // ------------------------------------------------------------------ v0.10.36 minigames
        /// <summary>Arkanoid / Pong / Snake: a single 320x240 pixel canvas drawn by
        /// ArcadeMiniGame and mapped onto the cabinet's 4:3 screen, with the same camera the
        /// Dinozavrik game uses.</summary>
        void BuildMiniGame(GameObject cab, int kind)
        {
            if (quadMesh == null) quadMesh = MakeQuad();
            if (root != null) Destroy(root);

            Vector3 screen = cab.transform.TransformPoint(
                new Vector3(0f, ScreenCenterLocalY, ScreenFrontLocalZ));
            Vector3 fwd = cab.transform.forward;

            root = new GameObject("CoopCabinetGame");
            root.transform.position = screen + fwd * 0.01f;
            root.transform.rotation = cab.transform.rotation * Quaternion.Euler(0f, 180f, 0f);

            if (mini != null) { mini.Destroy(); mini = null; }
            mini = new ArcadeMiniGame(kind, UnityEngine.Random.Range(1, 999999));
            if (miniMat != null) Destroy(miniMat);
            miniMat = MakeOpaqueMat(mini.Texture, 2100);
            if (miniMat.HasProperty("_Cull")) miniMat.SetFloat("_Cull", 0f);
            AddQuad("pixels", ScreenW, ScreenH, new Vector3(0f, 0f, -0.001f), miniMat, quadMesh);

            cam.transform.position = screen + fwd * 0.85f;
            cam.transform.rotation = Quaternion.LookRotation(-fwd, Vector3.up);
            cam.orthographic = false;
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 10f;

            string[] kindNames = { "Dinozavrik", "Arkanoid", "Pong", "Zmejka" };
            log?.LogInfo($"[ARCADE] {kindNames[kind]} screen: 320x240 pixel canvas, " +
                         $"Space = start / action, E = leave");
        }

        void UpdateMiniGame()
        {
            if (mini == null) return;
            mini.Update(Time.deltaTime);
        }

        /// <summary>v0.10.42: the duel screen is the same 320x240 canvas, but with the
        /// two-cabinet rules of ArcadeDuelGame.</summary>
        void BuildDuelGame(GameObject cab, int kind, bool host, int seed)
        {
            if (quadMesh == null) quadMesh = MakeQuad();
            if (root != null) Destroy(root);

            Vector3 screen = cab.transform.TransformPoint(
                new Vector3(0f, ScreenCenterLocalY, ScreenFrontLocalZ));
            Vector3 fwd = cab.transform.forward;

            root = new GameObject("CoopCabinetDuel");
            root.transform.position = screen + fwd * 0.01f;
            root.transform.rotation = cab.transform.rotation * Quaternion.Euler(0f, 180f, 0f);

            if (duel != null) { duel.Destroy(); duel = null; }
            duel = new ArcadeDuelGame(kind, host, seed);
            if (duelMat != null) Destroy(duelMat);
            duelMat = MakeOpaqueMat(duel.Texture, 2100);
            if (duelMat.HasProperty("_Cull")) duelMat.SetFloat("_Cull", 0f);
            AddQuad("pixels", ScreenW, ScreenH, new Vector3(0f, 0f, -0.001f), duelMat, quadMesh);

            cam.transform.position = screen + fwd * 0.85f;
            cam.transform.rotation = Quaternion.LookRotation(-fwd, Vector3.up);
            cam.orthographic = false;
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 10f;

            log?.LogInfo($"[DUEL] {kind} screen: 320x240 duel canvas, {(host ? "host" : "client")}");
        }

        /// <summary>v0.10.27: alpha bbox of a sprite, normalized to its own width/height:
        /// `halfW` from the bbox width, `centerX` its centre offset from the quad's centre,
        /// `top` the visible top above the quad's bottom and `bottom` the visible bottom padding.</summary>
        static void AlphaBounds(Texture2D tex, out float halfW, out float centerX, out float top,
                                out float bottom)
        {
            halfW = 0.32f;
            centerX = 0f;
            top = 0.62f;
            bottom = 0f;
            if (tex == null) return;
            var px = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            int minX = w, maxX = -1, minY = h, maxY = -1;
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    if (px[row + x].a < 32) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            if (maxX < minX || maxY < minY) return;
            halfW = (maxX - minX + 1) / (float)w * 0.5f;
            centerX = ((minX + maxX + 1) * 0.5f) / w - 0.5f;
            top = (maxY + 1) / (float)h;      // GetPixels32 y=0 is the bottom row
            bottom = minY / (float)h;
        }

        /// <summary>v0.10.24: lift the debris a little out of the dark skyline — a gamma lift plus
        /// 20 % saturation, no neon and no clipping of the light stripes.</summary>
        static void BrightenObstacle(Texture2D tex)
        {
            if (tex == null) return;
            var px = tex.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                if (px[i].a < 8) continue;
                float r = px[i].r / 255f, g = px[i].g / 255f, b = px[i].b / 255f;
                float lum = 0.299f * r + 0.587f * g + 0.114f * b;
                r = Mathf.Clamp01(lum + (r - lum) * 1.22f);
                g = Mathf.Clamp01(lum + (g - lum) * 1.22f);
                b = Mathf.Clamp01(lum + (b - lum) * 1.22f);
                px[i].r = (byte)(Mathf.Pow(r, 0.72f) * 255f);
                px[i].g = (byte)(Mathf.Pow(g, 0.72f) * 255f);
                px[i].b = (byte)(Mathf.Pow(b, 0.72f) * 255f);
            }
            tex.SetPixels32(px);
            tex.Apply(false);
        }

        /// <summary>v0.10.29: a grey or black texel is any visible pixel with saturation < 0.18
        /// or value < 0.22. If those cover more than half of the sprite, every such texel is
        /// remapped to a muted pastel chosen by a low-frequency Perlin patch; the pixel's own
        /// luminance is kept, so cracks, holes, stripes and the dark outline survive. Coloured
        /// details (rust, hazard stripes, the cone's white band) are left untouched.</summary>
        /// <summary>v0.10.30: replaces the v0.10.29 pastels with weathered materials. A grey or
        /// black texel (saturation &lt; 0.18 or value &lt; 0.22) is remapped to beige, khaki, moss
        /// green, brick or sand — the palette and banding are chosen per obstacle (rubble mixes
        /// brick chips, the barrier gets khaki/green/sand panels, the pipe gets moss at the
        /// bottom, the rebar rusty brick/khaki, the cone sand/beige/brick). The pixel's luminance,
        /// the dark silhouette and every coloured detail are preserved.</summary>
        void RepaintObstacle(Texture2D tex, string name, int seed)
        {
            if (tex == null) return;
            Color32[] px = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            int visible = 0, greyBlack = 0;
            for (int i = 0; i < px.Length; i++)
            {
                if (px[i].a < 32) continue;
                visible++;
                Color.RGBToHSV(new Color(px[i].r / 255f, px[i].g / 255f, px[i].b / 255f),
                               out _, out float s, out float v);
                if (s < 0.18f || v < 0.22f) greyBlack++;
            }
            if (visible == 0) return;
            float share = greyBlack / (float)visible;
            if (share <= 0.50f)
            {
                log?.LogInfo($"[DINO] obstacle texture '{name}': grey/black " +
                             $"{share * 100f:0.0} % — original kept");
                return;
            }

            Color[] pal;
            string palName;
            int mode;                       // 0 = patches, 1 = x bands, 2 = mossy bottom
            switch (name)
            {
                case "rubble":
                    pal = new[] { RepaintBeige, RepaintSand, RepaintBrick, RepaintKhaki };
                    palName = "beige/sand/brick/khaki"; mode = 0; break;
                case "barrier":
                    pal = new[] { RepaintKhaki, RepaintGreen, RepaintSand };
                    palName = "khaki/green/sand"; mode = 1; break;
                case "cone":
                    pal = new[] { RepaintSand, RepaintBeige, RepaintBrick };
                    palName = "sand/beige/brick"; mode = 0; break;
                case "pipe":
                    pal = new[] { RepaintBeige, RepaintSand, RepaintGreen, RepaintKhaki };
                    palName = "beige/sand/green/khaki"; mode = 2; break;
                case "block":
                    pal = new[] { RepaintBeige, RepaintSand, RepaintKhaki };
                    palName = "beige/sand/khaki"; mode = 0; break;
                default:                    // rebar
                    pal = new[] { RepaintBrick, RepaintKhaki, RepaintGreen };
                    palName = "brick/khaki/green"; mode = 0; break;
            }

            int recoloured = 0;
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    Color32 p = px[row + x];
                    if (p.a < 32) continue;
                    Color.RGBToHSV(new Color(p.r / 255f, p.g / 255f, p.b / 255f),
                                   out _, out float s, out float v);
                    if (s >= 0.18f && v >= 0.22f) continue;
                    float lum = (0.299f * p.r + 0.587f * p.g + 0.114f * p.b) / 255f;
                    int idx;
                    if (mode == 1)
                        idx = Mathf.Clamp((int)(x / (float)w * pal.Length), 0, pal.Length - 1);
                    else if (mode == 2)
                    {
                        // y = 0 is the bottom row in GetPixels32: moss at the foot of the pipe.
                        if (y < h * 0.38f) idx = 2;
                        else if (y < h * 0.55f) idx = 3;
                        else idx = Mathf.PerlinNoise((x + seed * 13.7f) * 0.11f,
                                                     (y + seed * 7.3f) * 0.11f) < 0.5f ? 0 : 1;
                    }
                    else
                    {
                        float n = Mathf.PerlinNoise((x + seed * 13.7f) * 0.11f,
                                                    (y + seed * 7.3f) * 0.11f);
                        idx = Mathf.Clamp((int)(n * pal.Length), 0, pal.Length - 1);
                    }
                    Color baseC = pal[idx];
                    // 1-px silhouette stays dark; the interior is lifted enough to read.
                    bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1 ||
                                px[row + x - 1].a < 32 || px[row + x + 1].a < 32 ||
                                px[row - w + x].a < 32 || px[row + w + x].a < 32;
                    float shade = edge ? (0.40f + 0.35f * lum) : (0.70f + 0.55f * lum);
                    px[row + x] = new Color32(
                        (byte)(Mathf.Clamp01(baseC.r * shade) * 255f + 0.5f),
                        (byte)(Mathf.Clamp01(baseC.g * shade) * 255f + 0.5f),
                        (byte)(Mathf.Clamp01(baseC.b * shade) * 255f + 0.5f), p.a);
                    recoloured++;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            log?.LogInfo($"[DINO] obstacle texture '{name}': grey/black " +
                         $"{share * 100f:0.0} % -> retexture " +
                         $"{recoloured * 100f / visible:0.0} % of visible pixels ({palName})");
        }

        /// <summary>v0.10.25: mirror a sprite left-to-right. The foreman frames were drawn facing
        /// left; the game runs to the right, so each player frame is flipped once at load.</summary>
        static void FlipTextureX(Texture2D tex)
        {
            if (tex == null) return;
            var px = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w / 2; x++)
                {
                    var tmp = px[row + x];
                    px[row + x] = px[row + w - 1 - x];
                    px[row + w - 1 - x] = tmp;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false);
        }

        void SetPlayerTex(Texture2D tex)
        {
            if (playerMat == null || tex == null) return;
            playerMat.mainTexture = tex;
            // v0.10.27: the collision box follows the visible pixels of the current frame.
            if (playerFrames != null)
            {
                for (int i = 0; i < playerFrames.Length; i++)
                {
                    if (playerFrames[i] != tex) continue;
                    curPlayerHalfW = playerHalfWf[i] * playerSize;
                    curPlayerCenterX = playerCenterXf[i] * playerSize;
                    curPlayerBottom = playerBottomFrac[i] * playerSize;
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ run / obstacles
        void ResetRun(bool start)
        {
            ClearObstacles();
            started = start;
            hit = false;
            grounded = true;
            vy = 0f;
            jumpY = 0f;
            bgOffset = 0f;
            groundOffset = 0f;
            frame = 0;
            frameTimer = 0f;
            spawnTimer = 1.2f;
            testCrouch = false;
            testGhost = false;
            score = 0;
            if (bgMesh != null) SetScrollUV(bgMesh, 0f, 320f / 1280f);
            if (groundMesh != null) SetScrollUV(groundMesh, 0f, 320f / 640f);
            if (playerT != null)
                playerT.localPosition = new Vector3(playerX, playerBaseY, -0.003f);
            SetPlayerTex(start ? run1Tex : idleTex);
            UpdateScoreDisplay();
        }

        void ClearObstacles()
        {
            for (int i = 0; i < obstacles.Count; i++)
            {
                if (obstacles[i].go != null) Destroy(obstacles[i].go);
                if (obstacles[i].mesh != null) Destroy(obstacles[i].mesh);
            }
            obstacles.Clear();
        }

        void SpawnObstacle()
        {
            if (obstacleMat == null || obstacleMat.Length == 0) return;
            int i = UnityEngine.Random.Range(0, obstacleMat.Length);
            if (obstacleMat[i] == null) return;
            // v0.10.23: 60 % narrower and 40 % lower than the old square cell.
            // v0.10.24: the wooden crate is 30 % taller, the fence barrier 30 % wider.
            // v0.10.30: every obstacle is 30 % larger in both axes.
            float cell = playerSize;
            float w = cell * 0.4f * 1.3f;
            float h = cell * 0.6f * 1.3f;
            string name = ObstacleNames[i];
            if (name == "crate") h *= 1.3f;
            else if (name == "barrier") w *= 1.3f;

            // The sprite starts fully beyond the right edge and is clipped to the screen as it
            // slides in (v0.10.24) — it must never be drawn on the cabinet's bezel.
            float x = ScreenW * 0.5f + w * 0.5f;
            var go = new GameObject("obstacle_" + name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.0025f);
            var mesh = MakeQuad();
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = obstacleMat[i];
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var o = new Obstacle
            {
                go = go,
                mesh = mesh,
                mr = mr,
                x = x,
                w = w,
                h = h,
                halfW = (obstacleHalfW != null && i < obstacleHalfW.Length
                         ? obstacleHalfW[i] : 0.32f) * w,
                centerX = (obstacleCenterX != null && i < obstacleCenterX.Length
                           ? obstacleCenterX[i] : 0f) * w,
                top = (obstacleTop != null && i < obstacleTop.Length
                       ? obstacleTop[i] : 0.62f) * h
            };
            if (obstacleLogged != null && i < obstacleLogged.Length && !obstacleLogged[i])
            {
                obstacleLogged[i] = true;
                log?.LogInfo($"[DINO] obstacle hitbox '{name}': visible " +
                             $"{o.halfW * 2f:0.000} x {o.top:0.000} m (alpha bbox of the " +
                             $"{w:0.000} x {h:0.000} m sprite)");
            }
            UpdateClip(o);
            obstacles.Add(o);
        }

        /// <summary>v0.10.24: crop the obstacle quad to the 4:3 screen rectangle. The sprite is
        /// anchored on the ground, and the part outside the screen simply is not emitted.</summary>
        void UpdateClip(Obstacle o)
        {
            if (o.mesh == null || o.mr == null) return;
            float screenL = -ScreenW * 0.5f;
            float screenR = ScreenW * 0.5f;
            float spriteL = o.x - o.w * 0.5f;
            float spriteR = o.x + o.w * 0.5f;
            float l = Mathf.Max(spriteL, screenL);
            float r = Mathf.Min(spriteR, screenR);
            if (r <= l + 0.0001f)
            {
                o.mr.enabled = false;
                return;
            }
            o.mr.enabled = true;
            float y0 = groundTop;
            float y1 = groundTop + o.h;
            float u0 = (l - spriteL) / o.w;
            float u1 = (r - spriteL) / o.w;
            o.mesh.vertices = new[]
            {
                new Vector3(l, y0, 0f), new Vector3(r, y0, 0f),
                new Vector3(r, y1, 0f), new Vector3(l, y1, 0f)
            };
            o.mesh.uv = new[]
            {
                new Vector2(u0, 0f), new Vector2(u1, 0f),
                new Vector2(u1, 1f), new Vector2(u0, 1f)
            };
            o.mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            o.mesh.RecalculateBounds();
        }

        void UpdateGame()
        {
            if (playerT == null) return;

            if (!started)
            {
                if (hit)
                {
                    if (Input.GetKeyDown(KeyCode.Space)) ResetRun(true);
                    return;
                }
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    started = true;
                    frame = 0;
                    frameTimer = 0f;
                    spawnTimer = 1.2f;
                    SetPlayerTex(run1Tex);
                }
                playerT.localPosition = new Vector3(playerX, playerBaseY, -0.003f);
                return;
            }

            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            // v0.10.30: +15 % of the base world speed after every 10 obstacles passed.
            float speedMul = 1f + 0.15f * (score / 10);
            groundOffset += ScrollPx * speedMul * dt / 640f;       // 1X
            bgOffset += BgFactor * ScrollPx * speedMul * dt / 1280f; // 0.6X
            SetScrollUV(bgMesh, bgOffset, 320f / 1280f);
            SetScrollUV(groundMesh, groundOffset, 320f / 640f);

            spawnTimer -= dt;
            if (spawnTimer <= 0f)
            {
                SpawnObstacle();
                spawnTimer = UnityEngine.Random.Range(ObstacleSpawnMin, ObstacleSpawnMax);
            }
            for (int i = obstacles.Count - 1; i >= 0; i--)
            {
                var o = obstacles[i];
                o.x -= groundSpeed * speedMul * dt;
                UpdateClip(o);
                // v0.10.28: the obstacle is "passed" once its visible right edge is behind the
                // foreman's visible left edge — a jump over it counts, a collision never does.
                if (!o.passed &&
                    o.x + o.centerX + o.halfW < playerX + curPlayerCenterX - curPlayerHalfW)
                {
                    o.passed = true;
                    score++;
                    UpdateScoreDisplay();
                    log?.LogInfo($"[DINO] obstacle passed: {o.go?.name}, score={score:0000}");
                    if (score % 10 == 0)
                        log?.LogInfo($"[DINO] speed x{1f + 0.15f * (score / 10):0.00} " +
                                     $"after {score} obstacles (base {ScrollPx:0} px/s)");
                }
                if (o.x + o.w * 0.5f < -ScreenW * 0.5f)
                {
                    if (o.go != null) Destroy(o.go);
                    if (o.mesh != null) Destroy(o.mesh);
                    obstacles.RemoveAt(i);
                }
            }

            if (grounded && Input.GetKeyDown(KeyCode.Space))
            {
                vy = JumpSpeed;
                grounded = false;
            }
            if (!grounded)
            {
                vy -= Gravity * dt;
                jumpY += vy * dt;
                if (jumpY <= 0f)
                {
                    jumpY = 0f;
                    vy = 0f;
                    grounded = true;
                }
            }

            // v0.10.27: pixel-accurate hit test. Both boxes are the visible alpha bboxes of
            // their current sprites (the player's follows run 1/2, jump, fall, crouch, idle),
            // each with its own centre offset. The foreman's visible feet are at jumpY plus the
            // frame's bottom padding; a jump above the obstacle's visible top clears it.
            float pCx = playerX + curPlayerCenterX;
            float pFeet = jumpY + curPlayerBottom;
            for (int i = 0; i < obstacles.Count; i++)
            {
                var o = obstacles[i];
                if (!testGhost &&
                    Mathf.Abs(o.x + o.centerX - pCx) < curPlayerHalfW + o.halfW &&
                    pFeet < o.top)
                {
                    hit = true;
                    started = false;
                    SetPlayerTex(run1Tex);
                    log?.LogInfo($"[DINO] collision with {o.go?.name} at x={o.x:0.00} " +
                                 $"(feet {pFeet:0.000} < top {o.top:0.000}); " +
                                 $"run ended (Space restarts)");
                    break;
                }
            }

            bool crouch = grounded && (testCrouch ||
                                      Input.GetKey(KeyCode.LeftControl) ||
                                      Input.GetKey(KeyCode.RightControl));
            if (!grounded)
            {
                SetPlayerTex(vy > 0f ? jumpTex : fallTex);
            }
            else if (crouch)
            {
                SetPlayerTex(crouchTex);
            }
            else
            {
                frameTimer += dt;
                if (frameTimer >= RunFrameTime)
                {
                    frameTimer -= RunFrameTime;
                    frame ^= 1;
                }
                SetPlayerTex(frame == 0 ? run1Tex : run2Tex);
            }

            playerT.localPosition = new Vector3(playerX, playerBaseY + jumpY, -0.003f);
        }

        // ------------------------------------------------------------------ HUD
        void OnGUI()
        {
            float ui = Mathf.Max(1f, Screen.height / 1440f);
            if (!active)
            {
                if (FindNearCabinet(out var near))
                {
                    var st = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = Mathf.RoundToInt(22f * ui),
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold
                    };
                    var r = new Rect(Screen.width * 0.5f - 260f * ui,
                                     Screen.height - 86f * ui, 520f * ui, 48f * ui);
                    GUI.color = new Color(0f, 0f, 0f, 0.65f);
                    GUI.Box(r, "");
                    GUI.color = Color.white;
                    string[] names = { "Динозаврик", "Арканоид", "Понг", "Змейка" };
                    int k = CabinetKind(near);
                    string label = "E — " + names[k] + (k != 0 ? "    F — ДУЭЛЬ" : "");
                    GUI.Label(r, label, st);
                }
                if (duelLobby)
                {
                    var st = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = Mathf.RoundToInt(24f * ui),
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold
                    };
                    var r = new Rect(Screen.width * 0.5f - 360f * ui,
                                     Screen.height - 146f * ui, 720f * ui, 50f * ui);
                    GUI.color = new Color(0f, 0f, 0f, 0.7f);
                    GUI.Box(r, "");
                    GUI.color = Color.white;
                    string s = duelClientWait
                        ? "ЗАПРОС НА ДУЭЛЬ ОТПРАВЛЕН — ЖДЁМ ХОЗЯИНА    (E — ОТМЕНА)"
                        : "ДУЭЛЬ " + GameName(duelKind).ToUpperInvariant() +
                          ": ЖДЁМ СОПЕРНИКА    (E — ОТМЕНА)";
                    GUI.Label(r, s, st);
                }
                return;
            }

            if (duelActive)
            {
                var st = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(22f * ui),
                    alignment = TextAnchor.MiddleCenter
                };
                var r = new Rect(Screen.width * 0.5f - 380f * ui, Screen.height - 76f * ui,
                                 760f * ui, 40f * ui);
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                GUI.Box(r, "");
                GUI.color = Color.white;
                string ctl = duelKind == 1 ? "A/D — БИТА" :
                             duelKind == 2 ? "W/S — БИТА" : "WASD — ЗМЕЙКА";
                GUI.Label(r, "E — ВЫЙТИ    ДУЭЛЬ    " + ctl, st);
                return;
            }

            if (currentKind == 0)
            {
                {
                    var st = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = Mathf.RoundToInt(22f * ui),
                        alignment = TextAnchor.MiddleCenter
                    };
                    var r = new Rect(Screen.width * 0.5f - 260f * ui, Screen.height - 76f * ui,
                                     520f * ui, 40f * ui);
                    GUI.color = new Color(0f, 0f, 0f, 0.55f);
                    GUI.Box(r, "");
                    GUI.color = Color.white;
                    GUI.Label(r, "E — выйти" + (started
                        ? "    ПРОБЕЛ — ПРЫЖОК    CTRL — ПРИСЕСТЬ" : ""), st);
                }

                if (!started)
                {
                    var st = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = Mathf.RoundToInt(40f * ui),
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold
                    };
                    var r = new Rect(Screen.width * 0.5f - 360f * ui, Screen.height * 0.5f - 60f * ui,
                                     720f * ui, 120f * ui);
                    GUI.color = new Color(0f, 0f, 0f, 0.72f);
                    GUI.Box(r, "");
                    GUI.color = Color.white;
                    GUI.Label(r, hit ? "СТОЛКНОВЕНИЕ — ПРОБЕЛ ЗАНОВО" : "ПРОБЕЛ — СТАРТ", st);
                }
            }
            else
            {
                var st = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(22f * ui),
                    alignment = TextAnchor.MiddleCenter
                };
                var r = new Rect(Screen.width * 0.5f - 380f * ui, Screen.height - 76f * ui,
                                 760f * ui, 40f * ui);
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                GUI.Box(r, "");
                GUI.color = Color.white;
                bool miniStarted = mini != null && mini.Started;
                string ctl = currentKind == 1 ? "A/D — БИТА" :
                             currentKind == 2 ? "W/S — БИТА" : "WASD — ЗМЕЙКА";
                string hint = miniStarted
                    ? "E — ВЫЙТИ    " + ctl
                    : "E — ВЫЙТИ    " + ctl + "    ПРОБЕЛ — СТАРТ";
                GUI.Label(r, hint, st);
            }
        }

        // ------------------------------------------------------------------ camera helpers
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
            // the propeller/gadget viewmodels are not all InteractObjects — hide every renderer
            // under the camera for the game view, otherwise the blades cover the screen
            foreach (var r in cam.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (root != null && r.transform.IsChildOf(root.transform)) continue;
                hiddenRenderers.Add(r);
                hiddenRendererEnabled.Add(r.enabled);
                r.enabled = false;
            }
        }

        void ShowHeldItems()
        {
            for (int i = 0; i < hiddenHeld.Count; i++)
                if (hiddenHeld[i] != null) hiddenHeld[i].SetActive(true);
            hiddenHeld.Clear();
            for (int i = 0; i < hiddenRenderers.Count; i++)
                if (hiddenRenderers[i] != null) hiddenRenderers[i].enabled = hiddenRendererEnabled[i];
            hiddenRenderers.Clear();
            hiddenRendererEnabled.Clear();
        }

        void DisablePost()
        {
            savedPost.Clear();
            savedPostEnabled.Clear();
            if (cam == null) return;
            foreach (var mb in cam.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                if (mb.GetType().Name != "PostProcessLayer") continue;
                savedPost.Add(mb);
                savedPostEnabled.Add(mb.enabled);
                mb.enabled = false;
            }
        }

        void RestorePost()
        {
            for (int i = 0; i < savedPost.Count; i++)
                if (savedPost[i] != null) savedPost[i].enabled = savedPostEnabled[i];
            savedPost.Clear();
            savedPostEnabled.Clear();
        }

        // ------------------------------------------------------------------ debug auto-test
        System.Collections.IEnumerator DuelTestRoutine()
        {
            log?.LogInfo("[DUEL] test routine start kind=" + duelTestKind);
            string[] names = { "", "arkanoid", "pong", "snake" };
            string tag = names[Mathf.Clamp(duelTestKind, 0, 3)];
            float t = 0f;
            while (SovietDistrict.ArcadeCabinetList.Count < 3 && t < 10f)
            {
                t += Time.deltaTime;
                yield return null;
            }
            GameObject cab = null;
            for (int i = 0; i < SovietDistrict.ArcadeCabinetList.Count; i++)
            {
                var info = SovietDistrict.ArcadeCabinetList[i];
                if (info.Duel && info.Kind == duelTestKind) { cab = info.Root; break; }
            }
            if (cab == null)
            {
                log?.LogWarning("[DUEL] test: no duel cabinet for kind " + duelTestKind);
                yield break;
            }
            var f0 = FirstPersonController.instance;
            Vector3 front = cab.transform.TransformPoint(new Vector3(0f, 0f, ScreenFrontLocalZ));
            Vector3 fwd = cab.transform.forward;
            Vector3 stand = front + fwd * 0.45f;
            stand.y = cab.transform.position.y;
            if (f0 != null)
            {
                f0.transform.position = stand;
                f0.transform.rotation = Quaternion.LookRotation(-fwd, Vector3.up);
            }
            yield return new WaitForSeconds(0.25f);
            duelKind = duelTestKind;
            duelCabinet = cab;
            duelSeed = 12345;
            BeginDuel(true);
            yield return new WaitForSeconds(0.5f);
            if (duelTestKind == 3 && duel != null)
                log?.LogInfo(duel.RunSnakeTurnTest());
            Shot(tag + "_duel_ready.png");
            yield return new WaitForSeconds(1.2f);
            Shot(tag + "_duel_test.png");
            yield return new WaitForSeconds(1.6f);
            Shot(tag + "_duel_test2.png");
            Exit();
        }

        IEnumerator AutoTest()
        {
            if (autoTestKind > 0)
            {
                yield return AutoTestMini();
                autoTest = false;
                yield break;
            }
            float t = 0f;
            while (SovietDistrict.ArcadeCabinetList.Count < 3 && t < 10f)
            {
                t += Time.deltaTime;
                yield return null;
            }
            if (SovietDistrict.ArcadeCabinetList.Count < 3)
            {
                log?.LogWarning("[DINO] DinoTest: no cabinets in the district");
                yield break;
            }
            var cab = SovietDistrict.ArcadeCabinetList[2].Root;
            var fpc0 = FirstPersonController.instance;
            if (cab == null || fpc0 == null)
            {
                log?.LogWarning("[DINO] DinoTest: no cabinet or player");
                yield break;
            }
            Vector3 front = cab.transform.TransformPoint(new Vector3(0f, 0f, ScreenFrontLocalZ));
            Vector3 fwd = cab.transform.forward;
            Vector3 stand = front + fwd * 0.45f;
            stand.y = cab.transform.position.y;
            fpc0.transform.position = stand;
            fpc0.transform.rotation = Quaternion.LookRotation(-fwd, Vector3.up);
            yield return new WaitForSeconds(0.2f);
            Shot("dino_prompt.png");
            yield return new WaitForSeconds(0.1f);
            Enter(cab);
            yield return new WaitForSeconds(0.4f);
            Shot("dino_ready.png");
            yield return new WaitForSeconds(0.1f);
            ResetRun(true);
            // v0.10.24: catch the first obstacle half-way across the right screen edge — it must
            // be cropped there, never drawn on the cabinet's bezel.
            yield return new WaitForSeconds(1.22f);
            Shot("dino_spawn.png");
            yield return new WaitForSeconds(0.18f);
            Shot("dino_test.png");
            testCrouch = true;
            yield return new WaitForSeconds(0.3f);
            Shot("dino_crouch.png");
            testCrouch = false;
            vy = JumpSpeed;
            grounded = false;
            yield return new WaitForSeconds(0.2f);
            Shot("dino_jump.png");
            // v0.10.28: let the first obstacle go past (ghost = no collision) so the score
            // really increments through the pass logic, then remove the ghost and let the next
            // obstacle end the run.
            testGhost = true;
            yield return new WaitForSeconds(1.4f);
            Shot("dino_score.png");
            testGhost = false;
            yield return new WaitForSeconds(2.0f);
            Shot("dino_hit.png");
            yield return new WaitForSeconds(0.2f);
            Exit();
            autoTest = false;
        }

        IEnumerator AutoTestMini()
        {
            string[] names = { "dino", "arkanoid", "pong", "snake" };
            string tag = names[Mathf.Clamp(autoTestKind, 0, 3)];
            float t = 0f;
            while (SovietDistrict.ArcadeCabinetList.Count < 3 && t < 10f)
            {
                t += Time.deltaTime;
                yield return null;
            }
            GameObject cab = null;
            for (int i = 0; i < SovietDistrict.ArcadeCabinetList.Count; i++)
                if (SovietDistrict.ArcadeCabinetList[i].Kind == autoTestKind)
                {
                    cab = SovietDistrict.ArcadeCabinetList[i].Root;
                    break;
                }
            var fpc0 = FirstPersonController.instance;
            if (cab == null || fpc0 == null)
            {
                log?.LogWarning($"[ARCADE] ArcadeTest: no {tag} cabinet or player");
                yield break;
            }
            Vector3 front = cab.transform.TransformPoint(new Vector3(0f, 0f, ScreenFrontLocalZ));
            Vector3 fwd = cab.transform.forward;
            Vector3 stand = front + fwd * 0.45f;
            stand.y = cab.transform.position.y;
            fpc0.transform.position = stand;
            fpc0.transform.rotation = Quaternion.LookRotation(-fwd, Vector3.up);
            yield return new WaitForSeconds(0.2f);
            Shot(tag + "_attract.png");
            yield return new WaitForSeconds(0.1f);
            Enter(cab);
            yield return new WaitForSeconds(0.35f);
            if (autoTestKind == 3 && mini != null)
                log?.LogInfo(mini.RunSnakeTurnTest());
            Shot(tag + "_ready.png");
            if (mini != null)
            {
                mini.Auto = true;
                mini.StartNow();
            }
            yield return new WaitForSeconds(0.9f);
            Shot(tag + "_test.png");
            yield return new WaitForSeconds(1.6f);
            Shot(tag + "_test2.png");
            yield return new WaitForSeconds(1.2f);
            Shot(tag + "_test3.png");
            Exit();
        }

        void Shot(string name)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", name));
            try
            {
                ScreenCapture.CaptureScreenshot(path);
                log?.LogInfo($"[ARCADE] test shot -> {path} (started={started})");
            }
            catch (Exception e)
            {
                log?.LogWarning("[DINO] screenshot failed: " + e.Message);
            }
        }
    }
}
