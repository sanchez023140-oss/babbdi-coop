using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BabbdiCoop
{
    /// <summary>v0.10.42: the two-cabinet duel versions of Pong, Arkanoid and Zmejka.
    /// Pong and Zmejka are host-authoritative (the host simulates and sends a state), while the
    /// Arkanoid duel runs two independent fields locally and only synchronises scores, lives and
    /// the "junk row" attacks (every 5 bricks sends a row to the opponent).</summary>
    public class ArcadeDuelGame
    {
        public const int W = 320, H = 240;
        public Texture2D Texture { get; private set; }
        public readonly int Kind;      // 1 Arkanoid, 2 Pong, 3 Zmejka
        public readonly bool Host;
        public bool Over => Kind == 2 ? pongOver : Kind == 3 ? snakeOver : arOver;
        public bool Dirty;             // set when a status/attack/state should be sent now

        readonly System.Random rng;
        readonly Color32[] px = new Color32[W * H];
        readonly Color32[] bg = new Color32[W * H];
        bool hasBg;
        float clock;

        // ---------------------------------------------------------------- Pong
        float selfY = 120f, oppY = 120f, remoteTargetY = 120f;
        float ballX = 160f, ballY = 120f, ballVX, ballVY;
        int myScore, oppScore;
        bool pongOver;
        float serveDelay = 0.45f;
        readonly List<Vector2> trail = new List<Vector2>();

        // --------------------------------------------------------------- Snake
        const int Cell = 12, Cols = 24, Rows = 17, Ox = 16, Oy = 30;
        readonly List<Vector2Int> selfSnake = new List<Vector2Int>();
        readonly List<Vector2Int> oppSnake = new List<Vector2Int>();
        Vector2Int selfDir, oppDir, selfFood, oppFood;
        // v0.10.43: one buffered turn per step, validated against the direction the head
        // actually moved in. `selfDir`/`oppDir` are never overwritten by a queued turn.
        Vector2Int selfDirQueued, oppDirQueued;
        bool selfDirHasQueued, oppDirHasQueued;
        int selfSnakeScore, oppSnakeScore, selfRounds, oppRounds;
        bool selfAlive, oppAlive, roundPause, snakeOver;
        float pauseT, stepT;
        const float StepTime = 0.14f;
        bool dirDirty;

        // ------------------------------------------------------------- Arkanoid
        readonly int[,] hp = new int[8, 10];
        const int BrickRows = 8;
        float paddleX = 160f, bX = 160f, bY = 196f, bVX, bVY;
        bool held = true, cleared, oppCleared, arOver;
        int lives = 3, arScore, oppArScore, oppLives = 3;
        int attackSeq, oppAttackSeq, appliedAttacks, destroyedSinceAttack;
        readonly List<Vector2> arTrail = new List<Vector2>();

        // backgrounds / palette (same brutal language as the solo cabinets)
        static readonly Color32[] PalA =
        {
            new Color32(176, 66, 40, 255), new Color32(216, 201, 166, 255),
            new Color32(26, 84, 82, 255), new Color32(178, 208, 58, 255),
            new Color32(206, 58, 120, 255), new Color32(16, 14, 12, 255)
        };
        static readonly Color32[] PalB =
        {
            new Color32(62, 40, 86, 255), new Color32(214, 160, 30, 255),
            new Color32(120, 206, 196, 255), new Color32(138, 44, 28, 255),
            new Color32(222, 210, 182, 255), new Color32(12, 10, 10, 255)
        };
        static readonly Color32 White = new Color32(255, 255, 255, 255);
        static readonly Color32 Dark = new Color32(8, 6, 8, 255);
        static readonly Color32 SelfCol = new Color32(150, 220, 70, 255);
        static readonly Color32 OppCol = new Color32(236, 64, 180, 255);

        public ArcadeDuelGame(int kind, bool host, int seed)
        {
            Kind = kind;
            Host = host;
            rng = new System.Random(seed);
            Texture = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                name = "CoopDuelScreen",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0
            };
            LoadBackground();
            Reset();
            Render();
        }

        public void Destroy()
        {
            if (Texture != null) UnityEngine.Object.Destroy(Texture);
            Texture = null;
        }

        void LoadBackground()
        {
            try
            {
                string name = Kind == 1 ? "slop_arkanoid.png" : Kind == 2 ? "slop_pong.png" : "slop_snake.png";
                string dir = Path.Combine(Path.GetDirectoryName(GlbFigureBuilder.Dir) ?? "", "arcade_game");
                string file = Path.Combine(dir, name);
                if (!File.Exists(file)) return;
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!t.LoadImage(File.ReadAllBytes(file))) { UnityEngine.Object.Destroy(t); return; }
                var src = t.GetPixels32();
                for (int y = 0; y < H; y++)
                {
                    int sy = Mathf.Clamp(t.height - 1 - y * t.height / H, 0, t.height - 1);
                    for (int x = 0; x < W; x++)
                    {
                        int sx = Mathf.Clamp(x * t.width / W, 0, t.width - 1);
                        bg[(H - 1 - y) * W + x] = src[sy * t.width + sx];
                    }
                }
                hasBg = true;
                UnityEngine.Object.Destroy(t);
            }
            catch (System.Exception swallowed) { CoopDiag.Swallowed(swallowed); }
        }

        public void Reset()
        {
            if (Kind == 2) ResetPong();
            else if (Kind == 3) ResetSnake();
            else ResetArkanoid();
        }

        // ================================================================ update ===
        public void Update(float dt)
        {
            dt = Mathf.Min(dt, 0.05f);
            clock += dt;
            if (Kind == 2) UpdatePong(dt);
            else if (Kind == 3) UpdateSnake(dt);
            else UpdateArkanoid(dt);
            Render();
        }

        // ================================================================== Pong ===
        void ResetPong()
        {
            selfY = oppY = 120f;
            remoteTargetY = 120f;
            ballX = 160f; ballY = 120f; ballVX = ballVY = 0f;
            myScore = oppScore = 0;
            pongOver = false;
            serveDelay = 0.45f;
            trail.Clear();
            Serve(1);
        }

        void Serve(int dir)
        {
            float ang = (float)(rng.NextDouble() * 1.1 - 0.55);
            float sp = 168f;
            ballX = 160f; ballY = 120f;
            ballVX = dir * sp * Mathf.Cos(ang);
            ballVY = sp * Mathf.Sin(ang);
            serveDelay = 0.45f;
            trail.Clear();
        }

        void UpdatePong(float dt)
        {
            float dir = 0f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) dir -= 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) dir += 1f;
            selfY = Mathf.Clamp(selfY + dir * 230f * dt, 42f, 198f);

            if (!Host)
            {
                // The client's own paddle is the right one on the host; state updates correct it.
                return;
            }

            if (!pongOver && serveDelay > 0f) serveDelay -= dt;
            oppY += Mathf.Clamp(remoteTargetY - oppY, -230f * dt, 230f * dt);
            oppY = Mathf.Clamp(oppY, 42f, 198f);

            if (pongOver || serveDelay > 0f) return;
            ballX += ballVX * dt;
            ballY += ballVY * dt;
            trail.Add(new Vector2(ballX, ballY));
            if (trail.Count > 9) trail.RemoveAt(0);
            if (ballY < 40f) { ballY = 40f; ballVY = Mathf.Abs(ballVY); }
            if (ballY > 208f) { ballY = 208f; ballVY = -Mathf.Abs(ballVY); }
            if (ballX < 46f && ballX > 30f && Mathf.Abs(ballY - selfY) < 27f && ballVX < 0f)
            {
                ballX = 46f; ballVX = Mathf.Abs(ballVX) * 1.03f;
                ballVY += (ballY - selfY) * 3.2f;
            }
            if (ballX > 274f && ballX < 290f && Mathf.Abs(ballY - oppY) < 27f && ballVX > 0f)
            {
                ballX = 274f; ballVX = -Mathf.Abs(ballVX) * 1.03f;
                ballVY += (ballY - oppY) * 3.2f;
            }
            float sp = Mathf.Sqrt(ballVX * ballVX + ballVY * ballVY);
            if (sp > 260f) { ballVX *= 260f / sp; ballVY *= 260f / sp; }
            if (ballX < 24f)
            {
                oppScore++;
                if (oppScore >= 7) pongOver = true; else Serve(1);
            }
            if (ballX > 296f)
            {
                myScore++;
                if (myScore >= 7) pongOver = true; else Serve(-1);
            }
        }

        public byte[] BuildPongState()
        {
            if (!Host) return null;
            var d = new byte[22];
            Buffer.BlockCopy(BitConverter.GetBytes(ballX), 0, d, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(ballY), 0, d, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(selfY), 0, d, 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(oppY), 0, d, 12, 4);
            d[16] = (byte)Mathf.Clamp(myScore, 0, 255);
            d[17] = (byte)Mathf.Clamp(oppScore, 0, 255);
            d[18] = (byte)Mathf.Clamp((int)(serveDelay * 100f), 0, 255);
            d[19] = (byte)(pongOver ? 1 : 0);
            d[20] = (byte)(ballVX < 0 ? 1 : 0);
            d[21] = (byte)(ballVY < 0 ? 1 : 0);
            return d;
        }

        public void ApplyPongState(byte[] d)
        {
            if (d == null || d.Length < 20) return;
            ballX = BitConverter.ToSingle(d, 0);
            ballY = BitConverter.ToSingle(d, 4);
            float hostY = BitConverter.ToSingle(d, 8);
            float clientY = BitConverter.ToSingle(d, 12);
            // the client sees itself on the left: its score is the host's oppScore
            myScore = d[17];
            oppScore = d[16];
            selfY = clientY;
            oppY = hostY;
            pongOver = d[19] != 0;
        }

        public byte[] BuildPongInput()
        {
            var d = new byte[4];
            Buffer.BlockCopy(BitConverter.GetBytes(selfY), 0, d, 0, 4);
            return d;
        }

        public void ApplyPongInput(byte[] d)
        {
            if (d == null || d.Length < 4) return;
            remoteTargetY = Mathf.Clamp(BitConverter.ToSingle(d, 0), 42f, 198f);
        }

        // ================================================================= Snake ===
        void ResetSnake()
        {
            selfRounds = oppRounds = 0;
            snakeOver = false;
            ResetRound();
        }

        void ResetRound()
        {
            selfSnake.Clear(); oppSnake.Clear();
            selfSnake.Add(new Vector2Int(4, 4)); selfSnake.Add(new Vector2Int(3, 4));
            selfSnake.Add(new Vector2Int(2, 4)); selfSnake.Add(new Vector2Int(1, 4));
            oppSnake.Add(new Vector2Int(19, 12)); oppSnake.Add(new Vector2Int(20, 12));
            oppSnake.Add(new Vector2Int(21, 12)); oppSnake.Add(new Vector2Int(22, 12));
            selfDir = new Vector2Int(1, 0);
            oppDir = new Vector2Int(-1, 0);
            selfDirQueued = selfDir;
            oppDirQueued = oppDir;
            selfDirHasQueued = false;
            oppDirHasQueued = false;
            selfSnakeScore = oppSnakeScore = 0;
            selfAlive = oppAlive = true;
            roundPause = false;
            stepT = 0f;
            PlaceFood(selfSnake, out selfFood);
            PlaceFood(oppSnake, out oppFood);
            dirDirty = true;
        }

        void PlaceFood(List<Vector2Int> body, out Vector2Int f)
        {
            for (int i = 0; i < 400; i++)
            {
                var c = new Vector2Int(rng.Next(Cols), rng.Next(Rows));
                if (!body.Contains(c)) { f = c; return; }
            }
            f = new Vector2Int(Cols / 2, Rows / 2);
        }

        void UpdateSnake(float dt)
        {
            // local direction input
            Vector2Int want = selfDir;
            bool pressed = false;
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) { want = new Vector2Int(0, -1); pressed = true; }
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) { want = new Vector2Int(0, 1); pressed = true; }
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) { want = new Vector2Int(-1, 0); pressed = true; }
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) { want = new Vector2Int(1, 0); pressed = true; }
            if (pressed)
            {
                TrySetDir(ref selfDir, ref selfDirQueued, ref selfDirHasQueued, want);
                dirDirty = true;
            }

            if (!Host) return;
            if (snakeOver) return;
            if (roundPause)
            {
                pauseT -= dt;
                if (pauseT > 0f) return;
                if (selfRounds >= 3 || oppRounds >= 3) { snakeOver = true; return; }
                ResetRound();
                return;
            }
            stepT += dt;
            if (stepT < StepTime) return;
            stepT = 0f;
            ApplyQueuedDirs();
            StepSnakes();
        }

        /// <summary>v0.10.44: delegates to <see cref="SnakeTurn"/> — the single turn rule shared
        /// with the solo cabinet. `dir` is the direction the head last moved in; `queued` holds
        /// at most one turn for the next step.</summary>
        static void TrySetDir(ref Vector2Int dir, ref Vector2Int queued, ref bool has, Vector2Int want)
        {
            int qx = queued.x, qy = queued.y;
            if (SnakeTurn.Accept(dir.x, dir.y, ref qx, ref qy, ref has, want.x, want.y))
                queued = new Vector2Int(qx, qy);
        }

        void ApplyQueuedDirs()
        {
            if (selfDirHasQueued) { selfDir = selfDirQueued; selfDirHasQueued = false; }
            if (oppDirHasQueued) { oppDir = oppDirQueued; oppDirHasQueued = false; }
        }

        /// <summary>v0.10.43: deterministic proof of the turn rule in the duel build, driven
        /// by `[Debug] DuelTest = snake`. The live match state is restored afterwards.</summary>
        public string RunSnakeTurnTest()
        {
            if (Kind != 3) return "not a snake cabinet";
            var s0 = new List<Vector2Int>(selfSnake);
            var o0 = new List<Vector2Int>(oppSnake);
            var sd = selfDir; var sq = selfDirQueued; bool sh = selfDirHasQueued;
            var od = oppDir; var oq = oppDirQueued; bool oh = oppDirHasQueued;
            var sf = selfFood; var of = oppFood;
            bool a0 = selfAlive, b0 = oppAlive, rp0 = roundPause;
            float pt0 = pauseT, st0 = stepT;
            int rs0 = selfRounds, ro0 = oppRounds, ss0 = selfSnakeScore, os0 = oppSnakeScore;

            selfSnake.Clear();
            selfSnake.Add(new Vector2Int(4, 8)); selfSnake.Add(new Vector2Int(3, 8));
            selfSnake.Add(new Vector2Int(2, 8)); selfSnake.Add(new Vector2Int(1, 8));
            oppSnake.Clear();
            oppSnake.Add(new Vector2Int(20, 2)); oppSnake.Add(new Vector2Int(21, 2));
            oppSnake.Add(new Vector2Int(22, 2)); oppSnake.Add(new Vector2Int(23, 2));
            selfDir = new Vector2Int(1, 0); oppDir = new Vector2Int(-1, 0);
            selfDirHasQueued = false; oppDirHasQueued = false;
            selfAlive = oppAlive = true;
            roundPause = false;
            selfFood = new Vector2Int(10, 15); oppFood = new Vector2Int(10, 1);

            TrySetDir(ref selfDir, ref selfDirQueued, ref selfDirHasQueued, new Vector2Int(0, -1));
            bool upQueued = selfDirHasQueued;
            TrySetDir(ref selfDir, ref selfDirQueued, ref selfDirHasQueued, new Vector2Int(-1, 0));
            bool rejected = selfDirQueued == new Vector2Int(0, -1);
            ApplyQueuedDirs();
            StepSnakes();
            bool alive1 = selfAlive;
            var h1 = selfSnake[0];

            TrySetDir(ref selfDir, ref selfDirQueued, ref selfDirHasQueued, new Vector2Int(-1, 0));
            bool accepted = selfDirHasQueued;
            ApplyQueuedDirs();
            StepSnakes();
            bool alive2 = selfAlive;
            var h2 = selfSnake[0];

            string r = $"[SNAKE] duel turn test: up buffered={upQueued}, same-tick left rejected={rejected}, " +
                       $"head {h1.x},{h1.y} alive={alive1}; later left accepted={accepted}, " +
                       $"head {h2.x},{h2.y} alive={alive2}";

            selfSnake.Clear(); selfSnake.AddRange(s0);
            oppSnake.Clear(); oppSnake.AddRange(o0);
            selfDir = sd; selfDirQueued = sq; selfDirHasQueued = sh;
            oppDir = od; oppDirQueued = oq; oppDirHasQueued = oh;
            selfFood = sf; oppFood = of;
            selfAlive = a0; oppAlive = b0; roundPause = rp0;
            pauseT = pt0; stepT = st0;
            selfRounds = rs0; oppRounds = ro0; selfSnakeScore = ss0; oppSnakeScore = os0;
            return r;
        }

        void StepSnakes()
        {
            var n1 = selfSnake[0] + selfDir;
            var n2 = oppSnake[0] + oppDir;
            bool g1 = n1 == selfFood;
            bool g2 = n2 == oppFood;
            // v0.10.43: the snake's own last segment moves away on a normal step, so its cell
            // is free to enter; on a growing step it stays and counts as a hit. Checking the
            // whole body (the old code) made tight play die for no visible reason.
            bool d1 = !Inside(n1) || HitBody(selfSnake, n1, g1) || oppSnake.Contains(n1);
            bool d2 = !Inside(n2) || HitBody(oppSnake, n2, g2) || selfSnake.Contains(n2);
            if (n1 == n2) { d1 = true; d2 = true; }
            if (d1) selfAlive = false;
            if (d2) oppAlive = false;
            if (selfAlive) selfSnake.Insert(0, n1);
            if (oppAlive) oppSnake.Insert(0, n2);
            if (selfAlive)
            {
                if (g1) { selfSnakeScore += 10; PlaceFood(selfSnake, out selfFood); }
                else selfSnake.RemoveAt(selfSnake.Count - 1);
            }
            if (oppAlive)
            {
                if (g2) { oppSnakeScore += 10; PlaceFood(oppSnake, out oppFood); }
                else oppSnake.RemoveAt(oppSnake.Count - 1);
            }
            if (!selfAlive || !oppAlive)
            {
                if (selfAlive && !oppAlive) selfRounds++;
                else if (oppAlive && !selfAlive) oppRounds++;
                roundPause = true;
                pauseT = 1.3f;
            }
        }

        /// <summary>v0.10.43: body hit test that treats the tail as free only when it moves.</summary>
        static bool HitBody(List<Vector2Int> body, Vector2Int cell, bool grows)
        {
            int solid = grows ? body.Count : body.Count - 1;
            for (int i = 0; i < solid; i++)
                if (body[i] == cell) return true;
            return false;
        }

        static bool Inside(Vector2Int c) => c.x >= 0 && c.x < Cols && c.y >= 0 && c.y < Rows;

        public byte[] BuildSnakeState()
        {
            if (!Host) return null;
            var b = new List<byte>(256);
            b.Add((byte)selfRounds); b.Add((byte)oppRounds);
            b.Add((byte)Mathf.Clamp(selfSnakeScore / 10, 0, 255));
            b.Add((byte)Mathf.Clamp(oppSnakeScore / 10, 0, 255));
            b.Add((byte)selfFood.x); b.Add((byte)selfFood.y);
            b.Add((byte)oppFood.x); b.Add((byte)oppFood.y);
            b.Add((byte)(selfAlive ? 1 : 0)); b.Add((byte)(oppAlive ? 1 : 0));
            b.Add((byte)Mathf.Clamp(selfSnake.Count, 1, 64));
            b.Add((byte)Mathf.Clamp(oppSnake.Count, 1, 64));
            for (int i = 0; i < Mathf.Min(64, selfSnake.Count); i++)
            { b.Add((byte)selfSnake[i].x); b.Add((byte)selfSnake[i].y); }
            for (int i = 0; i < Mathf.Min(64, oppSnake.Count); i++)
            { b.Add((byte)oppSnake[i].x); b.Add((byte)oppSnake[i].y); }
            return b.ToArray();
        }

        public void ApplySnakeState(byte[] d)
        {
            if (d == null || d.Length < 12) return;
            int p = 0;
            int hostRounds = d[p++];
            int clientRounds = d[p++];
            int hostScore = d[p++] * 10;
            int clientScore = d[p++] * 10;
            var hostFood = new Vector2Int(d[p++], d[p++]);
            var clientFood = new Vector2Int(d[p++], d[p++]);
            bool hostAlive = d[p++] != 0;
            bool clientAlive = d[p++] != 0;
            int l1 = Mathf.Clamp(d[p++], 1, 64);
            int l2 = Mathf.Clamp(d[p++], 1, 64);
            var a = new List<Vector2Int>(l1);
            var b = new List<Vector2Int>(l2);
            for (int i = 0; i < l1 && p + 1 < d.Length; i++) a.Add(new Vector2Int(d[p++], d[p++]));
            for (int i = 0; i < l2 && p + 1 < d.Length; i++) b.Add(new Vector2Int(d[p++], d[p++]));
            // the client sees itself first: its state is the host's client* half
            selfRounds = clientRounds;
            oppRounds = hostRounds;
            selfSnakeScore = clientScore;
            oppSnakeScore = hostScore;
            selfFood = clientFood;
            oppFood = hostFood;
            selfAlive = clientAlive;
            oppAlive = hostAlive;
            selfSnake.Clear(); selfSnake.AddRange(b);
            oppSnake.Clear(); oppSnake.AddRange(a);
            snakeOver = selfRounds >= 3 || oppRounds >= 3;
        }

        public byte[] BuildSnakeInput(bool force = false)
        {
            if (!dirDirty && !force) return null;
            dirDirty = false;
            // v0.10.43: send the queued turn, not the stale executed one, or the host would
            // only learn about a turn after it had already happened locally.
            var d = selfDirHasQueued ? selfDirQueued : selfDir;
            int code = d == new Vector2Int(0, -1) ? 0 :
                       d == new Vector2Int(1, 0) ? 1 :
                       d == new Vector2Int(0, 1) ? 2 : 3;
            return new[] { (byte)code };
        }

        public void ApplySnakeInput(byte[] d)
        {
            if (d == null || d.Length < 1) return;
            var v = d[0] == 0 ? new Vector2Int(0, -1) :
                    d[0] == 1 ? new Vector2Int(1, 0) :
                    d[0] == 2 ? new Vector2Int(0, 1) : new Vector2Int(-1, 0);
            // v0.10.43: the host rejects the same illegal turn it rejects for its own snake.
            TrySetDir(ref oppDir, ref oppDirQueued, ref oppDirHasQueued, v);
        }

        // ============================================================== Arkanoid ===
        void ResetArkanoid()
        {
            for (int r = 0; r < BrickRows; r++)
                for (int c = 0; c < 10; c++)
                    hp[r, c] = r < 5 ? 1 : 0;
            paddleX = 160f; bX = 160f; bY = 196f; bVX = bVY = 0f;
            held = true; lives = 3; arScore = 0; cleared = false; oppCleared = false;
            arOver = false; attackSeq = 0; oppAttackSeq = 0; appliedAttacks = 0;
            destroyedSinceAttack = 0;
            arTrail.Clear();
        }

        void UpdateArkanoid(float dt)
        {
            float target = paddleX;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) target -= 300f * dt;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) target += 300f * dt;
            paddleX += Mathf.Clamp(target - paddleX, -280f * dt, 280f * dt);
            paddleX = Mathf.Clamp(paddleX, 22f, 298f);
            if (arOver) return;
            if (Input.GetKeyDown(KeyCode.Space)) LaunchArkanoid();
            if (held)
            {
                bX = paddleX; bY = 196f;
                return;
            }
            bX += bVX * dt; bY += bVY * dt;
            arTrail.Add(new Vector2(bX, bY));
            if (arTrail.Count > 6) arTrail.RemoveAt(0);
            const float r = 4f;
            if (bX < 14 + r) { bX = 14 + r; bVX = Mathf.Abs(bVX); }
            if (bX > 306 - r) { bX = 306 - r; bVX = -Mathf.Abs(bVX); }
            if (bY < 32 + r) { bY = 32 + r; bVY = Mathf.Abs(bVY); }
            if (bVY > 0 && bY > 208 && bY < 224 && Mathf.Abs(bX - paddleX) < 31f)
            {
                bY = 208f;
                float off = (bX - paddleX) / 27f;
                float sp = Mathf.Min(250f, Mathf.Sqrt(bVX * bVX + bVY * bVY) * 1.015f);
                bVX = off * sp * 0.72f;
                bVY = -Mathf.Abs(sp);
            }
            for (int row = 0; row < BrickRows; row++)
                for (int col = 0; col < 10; col++)
                {
                    if (hp[row, col] <= 0) continue;
                    float bx = 16f + col * 28.8f, by = 42f + row * 14f;
                    if (bX + r < bx || bX - r > bx + 25.8f ||
                        bY + r < by || bY - r > by + 10f) continue;
                    hp[row, col]--;
                    if (hp[row, col] <= 0)
                    {
                        arScore += 10;
                        destroyedSinceAttack++;
                        if (destroyedSinceAttack >= 5)
                        {
                            destroyedSinceAttack = 0;
                            attackSeq++;
                            Dirty = true;
                        }
                    }
                    float cx = bx + 12.9f, cy = by + 5f;
                    if (Mathf.Abs(bX - cx) / 12.9f > Mathf.Abs(bY - cy) / 5f) bVX = -bVX;
                    else bVY = -bVY;
                    row = BrickRows; break;
                }
            if (bY > 238f)
            {
                lives--;
                if (lives <= 0) arOver = true;
                else { held = true; bX = paddleX; bY = 196f; bVX = bVY = 0f; }
            }
            bool any = false;
            for (int rr = 0; rr < BrickRows; rr++)
                for (int cc = 0; cc < 10; cc++) if (hp[rr, cc] > 0) any = true;
            if (!any) { cleared = true; arOver = true; }
        }

        void LaunchArkanoid()
        {
            if (!held) return;
            held = false;
            bVX = (rng.Next(2) == 0 ? -1f : 1f) * 118f;
            bVY = -148f;
        }

        public byte[] BuildArkanoidStatus()
        {
            return new[]
            {
                (byte)Mathf.Clamp(arScore / 10, 0, 255),
                (byte)Mathf.Clamp(lives, 0, 255),
                (byte)(cleared ? 1 : 0),
                (byte)(arOver ? 1 : 0),
                (byte)Mathf.Clamp(attackSeq, 0, 255)
            };
        }

        public void ApplyArkanoidStatus(byte[] d)
        {
            if (d == null || d.Length < 5) return;
            oppArScore = d[0] * 10;
            oppLives = d[1];
            oppCleared = d[2] != 0;
            if (d[4] > oppAttackSeq)
            {
                int add = d[4] - oppAttackSeq;
                for (int i = 0; i < add; i++) AddJunkRow();
                oppAttackSeq = d[4];
            }
        }

        void AddJunkRow()
        {
            for (int r = BrickRows - 1; r > 0; r--)
                for (int c = 0; c < 10; c++) hp[r, c] = hp[r - 1, c];
            for (int c = 0; c < 10; c++) hp[0, c] = 2;
        }

        // ================================================================ render ===
        void Render()
        {
            if (hasBg) Array.Copy(bg, px, px.Length);
            else Fill(new Color32(10, 13, 20, 255));
            if (Kind == 2) DrawPong();
            else if (Kind == 3) DrawSnake();
            else DrawArkanoid();
            SlopPost();
            Texture.SetPixels32(px);
            Texture.Apply(false);
        }

        void DrawPong()
        {
            float t = clock;
            TintAll(Pal(t, 0), 0.10f);
            DarkenRect(0, 0, W, 38, 0.62f);
            TextShadow(80, 8, myScore.ToString(), Pal(t, 1), 4, Dark);
            TextShadow(214, 8, oppScore.ToString(), Pal(t, 3), 4, Dark);
            TextShadow(10, 10, "VY", SelfCol, 2, Dark);
            TextShadow(252, 10, "SOP", OppCol, 2, Dark);
            int off = (int)(t * 10f) % 16;
            for (int y = 40; y < 212; y += 16)
                Rect(158, y, 4, 9, ((y + off) / 16) % 2 == 0 ? Pal(t, 0) : Pal(t, 2));

            int sym = Mathf.RoundToInt(selfY), opy = Mathf.RoundToInt(oppY);
            HazardSlab(26, sym, Pal(t, 1), Pal(t, 3));
            HazardSlab(278, opy, Pal(t, 3), Pal(t, 1));
            for (int i = 0; i < trail.Count - 1; i++)
            {
                int s = i < 4 ? 3 : 2;
                Rect((int)trail[i].x - s, (int)trail[i].y - s, s * 2 + 1, s * 2 + 1,
                     Darken(Pal(t, 4), 0.35f + i * 0.05f));
            }
            ChromeBall(Mathf.RoundToInt(ballX), Mathf.RoundToInt(ballY), Pal(t, 1), Pal(t, 4));
            if (pongOver)
                TextShadow(W / 2 - TextWidth(myScore >= 7 ? "POBEDA" : "PORAZHENIE", 3) / 2, 105,
                           myScore >= 7 ? "POBEDA" : "PORAZHENIE", Pal(t, 4), 3, Dark);
        }

        void DrawSnake()
        {
            float t = clock;
            DarkenRect(Ox - 2, Oy - 2, Cols * Cell + 4, Rows * Cell + 4, 0.72f);
            for (int gx = 0; gx <= Cols; gx++) Rect(Ox + gx * Cell, Oy, 1, Rows * Cell, new Color32(64, 22, 86, 255));
            for (int gy = 0; gy <= Rows; gy++) Rect(Ox, Oy + gy * Cell, Cols * Cell, 1, new Color32(64, 22, 86, 255));
            Rect(Ox - 2, Oy - 2, Cols * Cell + 4, 1, Pal(t, 4));
            Rect(Ox - 2, Oy + Rows * Cell + 1, Cols * Cell + 4, 1, Pal(t, 4));
            Rect(Ox - 2, Oy - 2, 1, Rows * Cell + 4, Pal(t, 4));
            Rect(Ox + Cols * Cell + 1, Oy - 2, 1, Rows * Cell + 4, Pal(t, 4));
            TextShadow(10, 8, "VY " + selfRounds, SelfCol, 2, Dark);
            TextShadow(250, 8, oppRounds + " SOP", OppCol, 2, Dark);
            for (int i = 0; i < selfSnake.Count; i++)
                DrawSnakeCell(selfSnake[i], i == 0 ? SelfCol : Darken(SelfCol, i % 2 == 0 ? 0.0f : 0.22f), i == 0);
            for (int i = 0; i < oppSnake.Count; i++)
                DrawSnakeCell(oppSnake[i], i == 0 ? OppCol : Darken(OppCol, i % 2 == 0 ? 0.0f : 0.22f), i == 0);
            DrawFood(selfFood, SelfCol);
            DrawFood(oppFood, OppCol);
            if (snakeOver)
                TextShadow(W / 2 - TextWidth(selfRounds >= 3 ? "POBEDA" : "PORAZHENIE", 3) / 2, 105,
                           selfRounds >= 3 ? "POBEDA" : "PORAZHENIE", OppCol, 3, Dark);
            if (roundPause && !snakeOver)
                TextShadow(W / 2 - TextWidth("RAUND", 3) / 2, 105, "RAUND", Pal(t, 1), 3, Dark);
        }

        void DrawSnakeCell(Vector2Int c, Color32 col, bool head)
        {
            int x = Ox + c.x * Cell, y = Oy + c.y * Cell;
            Rect(x - 3, y - 3, Cell + 5, Cell + 5, Dim(col, 0.16f));
            Rect(x + 1, y + 1, Cell - 2, Cell - 2, col);
            if (head)
            {
                Rect(x + 1, y + 1, Cell - 2, 1, White);
                Rect(x + 3, y + 3, 2, 2, Pal(clock, 4));
                Rect(x + 7, y + 3, 2, 2, Pal(clock, 2));
            }
        }

        void DrawFood(Vector2Int c, Color32 col)
        {
            int x = Ox + c.x * Cell, y = Oy + c.y * Cell;
            int pulse = 2 + Mathf.RoundToInt(2f * (0.5f + 0.5f * Mathf.Sin(clock * 8f)));
            FillCircle(x + 6, y + 6, 4 + pulse, Dim(col, 0.5f));
            FillCircle(x + 6, y + 6, 3, col);
        }

        void DrawArkanoid()
        {
            float t = clock;
            DarkenRect(0, 0, W, 30, 0.62f);
            TextShadow(8, 8, "VY " + (arScore / 10).ToString("000"), SelfCol, 2, Dark);
            TextShadow(176, 8, "SOP " + (oppArScore / 10).ToString("000"), OppCol, 2, Dark);
            TextShadow(8, 22, "ZH " + lives + " : " + oppLives, Pal(t, 3), 1, Dark);
            for (int row = 0; row < BrickRows; row++)
                for (int col = 0; col < 10; col++)
                {
                    int v = hp[row, col];
                    if (v <= 0) continue;
                    int x = Mathf.RoundToInt(16f + col * 28.8f);
                    int y = Mathf.RoundToInt(42f + row * 14f);
                    Color32 fill = Pal(t, row);
                    Color32 acc = Pal(t, (row + 3) % 6);
                    if (v == 2) { fill = Mix(fill, Dark, 0.35f); acc = Pal(t, 4); }
                    Rect(x, y, 26, 10, Dark);
                    Rect(x + 1, y + 1, 24, 8, fill);
                    Rect(x + 1, y + 1, 24, 1, Lighten(fill, 0.35f));
                    Rect(x + 4, y + 3, 3, 4, acc);
                    Rect(x + 18, y + 3, 3, 4, acc);
                    if (v == 2) Rect(x + 2, y + 7, 22, 1, Pal(t, 1));
                }
            int px0 = Mathf.RoundToInt(paddleX);
            Rect(px0 - 30, 208, 60, 14, Dark);
            Rect(px0 - 28, 210, 56, 10, Pal(t, 1));
            Rect(px0 - 28, 210, 56, 1, Lighten(Pal(t, 1), 0.55f));
            int off = (int)(t * 24f) % 12;
            for (int x = px0 - 27; x < px0 + 27; x++)
                if (((x + off) / 6) % 2 == 0) Rect(x, 213, 1, 4, Pal(t, 3));
            for (int i = 0; i < arTrail.Count - 1; i++)
            {
                int s = i < 3 ? 3 : 2;
                Rect((int)arTrail[i].x - s, (int)arTrail[i].y - s, s * 2 + 1, s * 2 + 1,
                     Darken(Pal(t, 4), 0.4f + i * 0.06f));
            }
            FillCircle(Mathf.RoundToInt(bX), Mathf.RoundToInt(bY), 12, Darken(Pal(t, 4), 0.78f));
            FillCircle(Mathf.RoundToInt(bX), Mathf.RoundToInt(bY), 8, Darken(Pal(t, 4), 0.55f));
            FillCircle(Mathf.RoundToInt(bX), Mathf.RoundToInt(bY), 5, Dark);
            FillCircle(Mathf.RoundToInt(bX), Mathf.RoundToInt(bY), 4, Pal(t, 1));
            FillCircle(Mathf.RoundToInt(bX), Mathf.RoundToInt(bY), 1, White);
            if (held && !arOver)
                TextShadow(W / 2 - TextWidth("SPACE - START", 2) / 2, 170, "SPACE - START", Pal(t, 3), 2, Dark);
            if (cleared || oppCleared || arOver || oppLives <= 0)
            {
                bool win = (cleared && !oppCleared) || oppLives <= 0;
                string s = win ? "POBEDA" : "PORAZHENIE";
                TextShadow(W / 2 - TextWidth(s, 3) / 2, 105, s, win ? SelfCol : OppCol, 3, Dark);
            }
        }

        // ================================================================ helpers ===
        static Color32 Pal(float t, int i)
        {
            float k = 0.5f - 0.5f * Mathf.Cos(t * 2f * Mathf.PI / 8f);
            return Mix(PalA[i % 6], PalB[i % 6], k);
        }

        static Color32 ScaleCol(Color32 c, float k) => new Color32(
            (byte)Mathf.Clamp(c.r * k, 0, 255), (byte)Mathf.Clamp(c.g * k, 0, 255),
            (byte)Mathf.Clamp(c.b * k, 0, 255), 255);

        static Color32 Mix(Color32 a, Color32 b, float k) => new Color32(
            (byte)(a.r * (1 - k) + b.r * k), (byte)(a.g * (1 - k) + b.g * k),
            (byte)(a.b * (1 - k) + b.b * k), 255);

        static Color32 Dim(Color32 c, float k) => new Color32(
            (byte)(c.r * k), (byte)(c.g * k), (byte)(c.b * k), 255);

        static Color32 Darken(Color32 c, float k) => new Color32(
            (byte)(c.r * (1 - k)), (byte)(c.g * (1 - k)), (byte)(c.b * (1 - k)), 255);

        static Color32 Lighten(Color32 c, float k) => new Color32(
            (byte)(c.r + (255 - c.r) * k), (byte)(c.g + (255 - c.g) * k),
            (byte)(c.b + (255 - c.b) * k), 255);

        void TintAll(Color32 tint, float k)
        {
            for (int i = 0; i < px.Length; i++) px[i] = Mix(px[i], tint, k);
        }

        void Fill(Color32 c) { for (int i = 0; i < px.Length; i++) px[i] = c; }

        int Index(int x, int y)
        {
            x = Mathf.Clamp(x, 0, W - 1); y = Mathf.Clamp(y, 0, H - 1);
            return (H - 1 - y) * W + x;
        }

        void Set(int x, int y, Color32 c)
        {
            if (x < 0 || x >= W || y < 0 || y >= H) return;
            px[(H - 1 - y) * W + x] = c;
        }

        void Rect(int x, int y, int w, int h, Color32 c)
        {
            for (int yy = y; yy < y + h; yy++) for (int xx = x; xx < x + w; xx++) Set(xx, yy, c);
        }

        void FillCircle(int cx, int cy, int r, Color32 c)
        {
            for (int y = -r; y <= r; y++) for (int x = -r; x <= r; x++)
                if (x * x + y * y <= r * r) Set(cx + x, cy + y, c);
        }

        void ChromeBall(int cx, int cy, Color32 bone, Color32 acc)
        {
            FillCircle(cx, cy, 12, Dim(acc, 0.22f));
            FillCircle(cx, cy, 8, Dim(acc, 0.45f));
            FillCircle(cx, cy, 5, Dark);
            FillCircle(cx, cy, 4, bone);
            FillCircle(cx, cy, 1, White);
        }

        void HazardSlab(int x, int cy, Color32 b, Color32 a)
        {
            int x1 = x + 16;
            Rect(x, cy - 25, x1 - x + 1, 51, Dark);
            Rect(x + 2, cy - 23, x1 - x - 3, 47, b);
            Rect(x + 2, cy - 23, x1 - x - 3, 1, Lighten(b, 0.5f));
            Rect(x + 2, cy + 23, x1 - x - 3, 1, Darken(b, 0.55f));
            int off = (int)(clock * 20f) % 10;
            for (int y = cy - 21; y < cy + 21; y++)
                if (((y + off) / 5) % 2 == 0) Rect(x + 5, y, x1 - x - 9, 1, a);
            Rect(x + 3, cy - 20, 3, 3, Darken(b, 0.7f));
            Rect(x + 3, cy + 17, 3, 3, Darken(b, 0.7f));
        }

        void DarkenRect(int x, int y, int w, int h, float k)
        {
            float m = 1f - k;
            for (int yy = y; yy < y + h; yy++)
                for (int xx = x; xx < x + w; xx++)
                {
                    if (xx < 0 || xx >= W || yy < 0 || yy >= H) continue;
                    int i = Index(xx, yy);
                    Color32 c = px[i];
                    px[i] = new Color32((byte)(c.r * m), (byte)(c.g * m), (byte)(c.b * m), 255);
                }
        }

        void SlopPost()
        {
            for (int y = 1; y < H; y += 2)
                for (int x = 0; x < W; x++)
                {
                    int i = Index(x, y);
                    Color32 c = px[i];
                    px[i] = new Color32((byte)(c.r * 0.8f), (byte)(c.g * 0.8f), (byte)(c.b * 0.8f), 255);
                }
            int tick = (int)(clock * 10f);
            if (tick % 4 == 0)
            {
                var nr = new System.Random(31 + tick);
                for (int i = 0; i < 70; i++) Set(nr.Next(0, W), nr.Next(0, H), White);
            }
        }

        // ---- 3x5 font (copied from ArcadeMiniGame) --------------------------------
        static readonly Dictionary<char, string[]> Glyphs = BuildFont();

        static Dictionary<char, string[]> BuildFont()
        {
            var g = new Dictionary<char, string[]>();
            void A(char c, string a, string b, string d, string e, string f) => g[c] = new[] { a, b, d, e, f };
            A('A', "010", "101", "111", "101", "101"); A('B', "110", "101", "110", "101", "110");
            A('C', "011", "100", "100", "100", "011"); A('D', "110", "101", "101", "101", "110");
            A('E', "111", "100", "110", "100", "111"); A('F', "111", "100", "110", "100", "100");
            A('G', "011", "100", "101", "101", "011"); A('H', "101", "101", "111", "101", "101");
            A('I', "111", "010", "010", "010", "111"); A('J', "001", "001", "001", "101", "010");
            A('K', "101", "101", "110", "101", "101"); A('L', "100", "100", "100", "100", "111");
            A('M', "101", "111", "111", "101", "101"); A('N', "101", "111", "111", "111", "101");
            A('O', "010", "101", "101", "101", "010"); A('P', "110", "101", "110", "100", "100");
            A('Q', "010", "101", "101", "111", "011"); A('R', "110", "101", "110", "101", "101");
            A('S', "011", "100", "010", "001", "110"); A('T', "111", "010", "010", "010", "010");
            A('U', "101", "101", "101", "101", "111"); A('V', "101", "101", "101", "101", "010");
            A('W', "101", "101", "111", "111", "101"); A('X', "101", "101", "010", "101", "101");
            A('Y', "101", "101", "010", "010", "010"); A('Z', "111", "001", "010", "100", "111");
            A('0', "111", "101", "101", "101", "111"); A('1', "010", "110", "010", "010", "111");
            A('2', "110", "001", "010", "100", "111"); A('3', "110", "001", "010", "001", "110");
            A('4', "101", "101", "111", "001", "001"); A('5', "111", "100", "110", "001", "110");
            A('6', "011", "100", "111", "101", "111"); A('7', "111", "001", "010", "010", "010");
            A('8', "111", "101", "111", "101", "111"); A('9', "111", "101", "111", "001", "110");
            A(':', "000", "010", "000", "010", "000"); A('-', "000", "000", "111", "000", "000");
            A(' ', "000", "000", "000", "000", "000"); A('.', "000", "000", "000", "000", "010");
            A('!', "010", "010", "010", "000", "010");
            return g;
        }

        int TextWidth(string s, int sc) => s.Length * 4 * sc - sc;

        void Text(int x, int y, string s, Color32 c, int sc)
        {
            int cx = x;
            foreach (char raw in s.ToUpperInvariant())
            {
                if (!Glyphs.TryGetValue(raw, out var gl)) gl = Glyphs[' '];
                for (int ry = 0; ry < 5; ry++)
                    for (int rx = 0; rx < 3; rx++)
                        if (gl[ry][rx] == '1') Rect(cx + rx * sc, y + ry * sc, sc, sc, c);
                cx += 4 * sc;
            }
        }

        void TextShadow(int x, int y, string s, Color32 c, int sc, Color32 shadow)
        {
            Text(x + sc, y + sc, s, shadow, sc);
            Text(x, y, s, c, sc);
        }
    }
}
