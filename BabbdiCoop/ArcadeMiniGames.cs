using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BabbdiCoop
{
    /// <summary>v0.10.36: the three new cabinet games (Arkanoid, Pong, Snake) drawn on a
    /// 320x240 pixel canvas. The canvas is uploaded once per frame to a Point-filtered
    /// Texture2D which ArcadeGame maps onto the cabinet's 4:3 screen. The same rules were used
    /// to render the 20 s attract flipbooks (arkanoid/pong/snake_attract_atlas.png).</summary>
    public class ArcadeMiniGame
    {
        public const int W = 320, H = 240;
        public Texture2D Texture { get; private set; }
        public bool Started => started;
        public bool Auto;                       // debug: AI plays (used by ArcadeTest)

        readonly int kind;
        readonly System.Random rng;
        readonly Color32[] px = new Color32[W * H];
        readonly Color32[] bgSlop = new Color32[W * H];
        bool hasSlop;
        float clock;

        bool started, gameOver;
        float overDelay;
        int score, lives;

        // ---- Arkanoid
        float paddleX, ballX, ballY, ballVX, ballVY;
        bool ballHeld;
        readonly bool[,] bricks = new bool[5, 10];
        int bricksLeft;
        readonly List<Vector2> trail = new List<Vector2>();

        // ---- Pong
        float lpY, rpY, pbX, pbY, pbVX, pbVY;
        int sl, sr;
        float serveDelay;
        const int PongTarget = 7;

        // ---- Snake
        const int Cell = 12, Cols = 24, Rows = 17, Ox = 16, Oy = 30;
        readonly List<Vector2Int> snake = new List<Vector2Int>();
        Vector2Int sdir, food;
        // v0.10.43: the turn waiting for the next move. `sdir` is the direction the head
        // actually MOVED in, never a queued one - that mix-up was the 180-degree bug.
        Vector2Int sdirQueued;
        bool sdirHasQueued;
        float stepTimer, stepTime;
        int grow;

        static readonly Color32 Bg = new Color32(10, 13, 20, 255);
        static readonly Color32 Steel = new Color32(66, 76, 84, 255);
        static readonly Color32 Bone = new Color32(205, 198, 178, 255);
        static readonly Color32 Teal = new Color32(58, 132, 128, 255);
        static readonly Color32 Rust = new Color32(142, 66, 44, 255);
        static readonly Color32 Ochre = new Color32(156, 126, 58, 255);
        static readonly Color32 Magenta = new Color32(170, 54, 122, 255);
        static readonly Color32 Green = new Color32(108, 176, 74, 255);
        static readonly Color32 Lime = new Color32(154, 208, 96, 255);
        static readonly Color32 Red = new Color32(168, 46, 38, 255);
        static readonly Color32 White = new Color32(238, 232, 206, 255);

        public ArcadeMiniGame(int kind, int seed)
        {
            this.kind = kind;
            rng = new System.Random(seed);
            Texture = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                name = "CoopMiniScreen",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0
            };
            LoadSlopBackground();
            Reset();
            Render();
        }

        public void Destroy()
        {
            if (Texture != null) UnityEngine.Object.Destroy(Texture);
            Texture = null;
        }

        /// <summary>v0.10.38: the playable screen uses the same SD slop background as the
        /// attract GIF (slop_arkanoid/pong/snake.png, 320x240).</summary>
        void LoadSlopBackground()
        {
            try
            {
                string name = kind == 1 ? "slop_arkanoid.png" :
                              kind == 2 ? "slop_pong.png" : "slop_snake.png";
                string dir = Path.Combine(Path.GetDirectoryName(GlbFigureBuilder.Dir) ?? "",
                                          "arcade_game");
                string file = Path.Combine(dir, name);
                if (!File.Exists(file)) return;
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!t.LoadImage(File.ReadAllBytes(file))) { UnityEngine.Object.Destroy(t); return; }
                var src = t.GetPixels32();
                for (int y = 0; y < H; y++)
                {
                    // GetPixels32 is bottom-up; y here is the top-down canvas row, so the
                    // source row must be inverted or the playfield backdrop appears upside down.
                    int sy = Mathf.Clamp(t.height - 1 - y * t.height / H, 0, t.height - 1);
                    for (int x = 0; x < W; x++)
                    {
                        int sx = Mathf.Clamp(x * t.width / W, 0, t.width - 1);
                        bgSlop[(H - 1 - y) * W + x] = src[sy * t.width + sx];
                    }
                }
                hasSlop = true;
                UnityEngine.Object.Destroy(t);
            }
            catch { }
        }

        void Reset()
        {
            started = false;
            gameOver = false;
            overDelay = 0f;
            score = 0;
            lives = 3;
            if (kind == 1) ResetArkanoid();
            else if (kind == 2) ResetPong();
            else ResetSnake();
        }

        public void StartNow()
        {
            if (gameOver) Reset();
            if (!started)
            {
                started = true;
                if (kind == 1) LaunchBall();
                if (kind == 2) Serve();
            }
        }

        void LaunchBall()
        {
            if (!ballHeld) return;
            ballHeld = false;
            ballVX = (rng.Next(2) == 0 ? -1f : 1f) * 118f;
            ballVY = -148f;
        }

        // ============================================================== update ===
        public void Update(float dt)
        {
            dt = Mathf.Min(dt, 0.05f);
            clock += dt;
            if (!started)
            {
                if (Auto || Input.GetKeyDown(KeyCode.Space)) StartNow();
                if (!started) { Render(); return; }
            }
            if (gameOver)
            {
                overDelay -= dt;
                if (Input.GetKeyDown(KeyCode.Space) || (Auto && overDelay <= 0f))
                {
                    Reset();
                    StartNow();
                }
                Render();
                return;
            }
            if (kind == 1) UpdateArkanoid(dt);
            else if (kind == 2) UpdatePong(dt);
            else UpdateSnake(dt);
            Render();
        }

        // ============================================================ arkanoid ===
        void ResetArkanoid()
        {
            paddleX = 160f;
            ballX = 160f; ballY = 196f;
            ballVX = 0f; ballVY = 0f;
            ballHeld = true;
            trail.Clear();
            bricksLeft = 0;
            for (int r = 0; r < 5; r++)
                for (int c = 0; c < 10; c++)
                {
                    bricks[r, c] = true;
                    bricksLeft++;
                }
        }

        void UpdateArkanoid(float dt)
        {
            const float half = 27f;
            float target = paddleX;
            if (Auto)
            {
                target = ballX;
            }
            else
            {
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) target -= 300f * dt;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) target += 300f * dt;
            }
            float maxStep = 260f * dt;
            paddleX += Mathf.Clamp(target - paddleX, -maxStep, maxStep);
            paddleX = Mathf.Clamp(paddleX, 22f, 298f);

            if (Input.GetKeyDown(KeyCode.Space)) LaunchBall();
            if (ballHeld)
            {
                ballX = paddleX;
                ballY = 196f;
                if (Auto) LaunchBall();
                return;
            }
            ballX += ballVX * dt;
            ballY += ballVY * dt;
            trail.Add(new Vector2(ballX, ballY));
            if (trail.Count > 7) trail.RemoveAt(0);
            const float r = 4f;
            if (ballX < 14 + r) { ballX = 14 + r; ballVX = Mathf.Abs(ballVX); }
            if (ballX > 306 - r) { ballX = 306 - r; ballVX = -Mathf.Abs(ballVX); }
            if (ballY < 32 + r) { ballY = 32 + r; ballVY = Mathf.Abs(ballVY); }

            // paddle
            if (ballVY > 0 && ballY > 208 && ballY < 224 && Mathf.Abs(ballX - paddleX) < half + r)
            {
                ballY = 208f;
                float off = (ballX - paddleX) / half;
                float sp = Mathf.Min(250f, Mathf.Sqrt(ballVX * ballVX + ballVY * ballVY) * 1.015f);
                ballVX = off * sp * 0.72f;
                ballVY = -Mathf.Abs(sp);
            }

            // bricks: x0 16, y0 42, cell 28.8 x 14, brick 25.8 x 10
            for (int row = 0; row < 5; row++)
                for (int col = 0; col < 10; col++)
                {
                    if (!bricks[row, col]) continue;
                    float bx = 16f + col * 28.8f, by = 42f + row * 14f;
                    if (ballX + r < bx || ballX - r > bx + 25.8f ||
                        ballY + r < by || ballY - r > by + 10f) continue;
                    bricks[row, col] = false;
                    bricksLeft--;
                    score += 10;
                    float cx = bx + 12.9f, cy = by + 5f;
                    if (Mathf.Abs(ballX - cx) / 12.9f > Mathf.Abs(ballY - cy) / 5f)
                        ballVX = -ballVX;
                    else
                        ballVY = -ballVY;
                    row = 5; break;
                }

            if (ballY > 238f)
            {
                lives--;
                if (lives <= 0) EndGame();
                else { ballHeld = true; ballX = paddleX; ballY = 196f; ballVX = ballVY = 0f; }
            }
            if (bricksLeft == 0)
            {
                for (int rr = 0; rr < 5; rr++)
                    for (int cc = 0; cc < 10; cc++) { bricks[rr, cc] = true; bricksLeft++; }
                ballHeld = true;
                lives = Mathf.Min(3, lives + 1);
            }
        }

        // ================================================================= pong ===
        void ResetPong()
        {
            lpY = rpY = 120f;
            sl = sr = 0;
            pbX = 160f; pbY = 120f; pbVX = pbVY = 0f;
            serveDelay = 0.45f;
        }

        void Serve()
        {
            float ang = (float)(rng.NextDouble() * 1.1 - 0.55);
            float sp = 168f;
            float s = rng.Next(2) == 0 ? -1f : 1f;
            pbX = 160f; pbY = 120f;
            pbVX = s * sp * Mathf.Cos(ang);
            pbVY = sp * Mathf.Sin(ang);
            serveDelay = 0.45f;
            trail.Clear();
        }

        void UpdatePong(float dt)
        {
            const float ph = 22f;
            float dir = 0f;
            if (Auto)
            {
                dir = Mathf.Clamp(pbY - lpY, -1f, 1f);
            }
            else
            {
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) dir -= 1f;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) dir += 1f;
            }
            lpY = Mathf.Clamp(lpY + dir * 220f * dt, 44f, 196f);

            float tr = pbY - 9f * Mathf.Sin(pbX * 0.017f + 1.2f);
            rpY += Mathf.Clamp(tr - rpY, -205f * dt, 205f * dt);
            rpY = Mathf.Clamp(rpY, 44f, 196f);

            if (serveDelay > 0f) { serveDelay -= dt; return; }
            pbX += pbVX * dt;
            pbY += pbVY * dt;
            trail.Add(new Vector2(pbX, pbY));
            if (trail.Count > 9) trail.RemoveAt(0);
            if (pbY < 40f) { pbY = 40f; pbVY = Mathf.Abs(pbVY); }
            if (pbY > 208f) { pbY = 208f; pbVY = -Mathf.Abs(pbVY); }
            if (pbX < 46f && pbX > 30f && Mathf.Abs(pbY - lpY) < ph + 5f && pbVX < 0f)
            {
                pbX = 46f;
                pbVX = Mathf.Abs(pbVX) * 1.03f;
                pbVY += (pbY - lpY) * 3.2f;
            }
            if (pbX > 274f && pbX < 290f && Mathf.Abs(pbY - rpY) < ph + 5f && pbVX > 0f)
            {
                pbX = 274f;
                pbVX = -Mathf.Abs(pbVX) * 1.03f;
                pbVY += (pbY - rpY) * 3.2f;
            }
            float sp = Mathf.Sqrt(pbVX * pbVX + pbVY * pbVY);
            if (sp > 260f) { pbVX *= 260f / sp; pbVY *= 260f / sp; }
            if (pbX < 24f) { sr++; Serve(); CheckPongEnd(); }
            if (pbX > 296f) { sl++; Serve(); CheckPongEnd(); }
        }

        void CheckPongEnd()
        {
            if (sl >= PongTarget || sr >= PongTarget) EndGame();
        }

        // ================================================================ snake ===
        void ResetSnake()
        {
            snake.Clear();
            snake.Add(new Vector2Int(6, 8));
            snake.Add(new Vector2Int(5, 8));
            snake.Add(new Vector2Int(4, 8));
            snake.Add(new Vector2Int(3, 8));
            sdir = new Vector2Int(1, 0);
            sdirQueued = sdir;
            sdirHasQueued = false;
            stepTime = 0.125f;
            stepTimer = 0f;
            grow = 0;
            PlaceFood();
        }

        void PlaceFood()
        {
            for (int tries = 0; tries < 400; tries++)
            {
                var c = new Vector2Int(rng.Next(Cols), rng.Next(Rows));
                if (!snake.Contains(c)) { food = c; return; }
            }
            food = new Vector2Int(Cols - 2, Rows / 2);
        }

        void UpdateSnake(float dt)
        {
            if (!Auto)
            {
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) TryDir(0, -1);
                if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) TryDir(0, 1);
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) TryDir(-1, 0);
                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) TryDir(1, 0);
            }
            else
            {
                var d = AutoDir();
                if (d != Vector2Int.zero) TryDir(d.x, d.y);
            }
            stepTimer += dt;
            if (stepTimer < stepTime) return;
            stepTimer = 0f;
            AdvanceSnake();
        }

        /// <summary>v0.10.43: one move of the snake. The buffered turn is consumed HERE and
        /// nowhere else, so no burst of key presses can turn the head twice inside one cell.</summary>
        void AdvanceSnake()
        {
            if (gameOver) return;
            if (sdirHasQueued)
            {
                sdir = sdirQueued;
                sdirHasQueued = false;
            }
            var head = snake[0];
            var next = head + sdir;
            if (next.x < 0 || next.x >= Cols || next.y < 0 || next.y >= Rows)
            {
                EndGame();
                return;
            }
            bool growNow = next == food;
            // The last segment moves away on a normal step, so its cell is free to enter.
            // On a growing step it stays put and is a real hit - the old code never checked
            // that case, which allowed the head and the tail to share one cell.
            int solid = growNow ? snake.Count : snake.Count - 1;
            for (int i = 0; i < solid; i++)
                if (snake[i] == next) { EndGame(); return; }
            snake.Insert(0, next);
            if (growNow)
            {
                score += 10;
                stepTime = Mathf.Max(0.085f, stepTime - 0.002f);
                PlaceFood();
            }
            else
            {
                snake.RemoveAt(snake.Count - 1);
            }
        }

        void TryDir(int dx, int dy)
        {
            var d = new Vector2Int(dx, dy);
            if (d == Vector2Int.zero) return;
            // v0.10.43: the reported "impossible" deaths. `sdir` used to be overwritten by the
            // next key press, but the head only moves once per step, so right -> up -> left
            // inside one tick validated `left` against the *queued* `up` and produced a real
            // 180-degree step straight into the neck (a straight snake, instant death).
            // Now a turn is validated against the direction the head last MOVED in and at
            // most one turn is buffered per step.
            if (d == -sdir) return;                                 // never reverse
            if (d == sdir) { sdirHasQueued = false; return; }       // straight on cancels the buffered turn
            sdirQueued = d;
            sdirHasQueued = true;
        }

        /// <summary>v0.10.43: deterministic proof of the turn rule, driven by
        /// `[Debug] ArcadeTest = snake`. It reproduces the exact burst that used to kill the
        /// player and shows it is now rejected, then shows that the same turn one step later
        /// still works. The live game state is restored afterwards.</summary>
        public string RunSnakeTurnTest()
        {
            if (kind != 3) return "not a snake cabinet";
            var savedSnake = new List<Vector2Int>(snake);
            var savedDir = sdir;
            var savedQueued = sdirQueued;
            bool savedHas = sdirHasQueued;
            var savedFood = food;
            bool savedOver = gameOver;
            float savedStep = stepTime;

            snake.Clear();
            snake.Add(new Vector2Int(6, 8));
            snake.Add(new Vector2Int(5, 8));
            snake.Add(new Vector2Int(4, 8));
            snake.Add(new Vector2Int(3, 8));
            sdir = new Vector2Int(1, 0);
            sdirHasQueued = false;
            gameOver = false;
            food = new Vector2Int(20, 2);

            // (a) the reported burst: up then left inside one tick
            TryDir(0, -1);
            bool upQueued = sdirHasQueued && sdirQueued == new Vector2Int(0, -1);
            TryDir(-1, 0);
            bool rejected = sdirQueued == new Vector2Int(0, -1);
            AdvanceSnake();
            bool survived = !gameOver;
            var head1 = snake[0];

            // (b) the same turn one step later is legal
            TryDir(-1, 0);
            bool accepted = sdirHasQueued && sdirQueued == new Vector2Int(-1, 0);
            AdvanceSnake();
            bool survived2 = !gameOver;
            var head2 = snake[0];

            string r = $"[SNAKE] turn test: up buffered={upQueued}, same-tick left rejected={rejected}, " +
                       $"head {head1.x},{head1.y} alive={survived}; later left accepted={accepted}, " +
                       $"head {head2.x},{head2.y} alive={survived2}";

            snake.Clear();
            snake.AddRange(savedSnake);
            sdir = savedDir;
            sdirQueued = savedQueued;
            sdirHasQueued = savedHas;
            food = savedFood;
            gameOver = savedOver;
            stepTime = savedStep;
            return r;
        }

        Vector2Int AutoDir()
        {
            var head = snake[0];
            var d = food - head;
            var opts = new List<Vector2Int>();
            if (Mathf.Abs(d.x) > Mathf.Abs(d.y))
            {
                opts.Add(new Vector2Int(Math.Sign(d.x), 0));
                opts.Add(new Vector2Int(0, Math.Sign(d.y)));
            }
            else
            {
                opts.Add(new Vector2Int(0, Math.Sign(d.y)));
                opts.Add(new Vector2Int(Math.Sign(d.x), 0));
            }
            foreach (var o in opts)
            {
                if (o == Vector2Int.zero) continue;
                var n = head + o;
                if (n.x < 0 || n.x >= Cols || n.y < 0 || n.y >= Rows) continue;
                bool hit = false;
                for (int i = 0; i < snake.Count - 1; i++) if (snake[i] == n) { hit = true; break; }
                if (!hit && o != -sdir) return o;
            }
            return Vector2Int.zero;
        }

        void EndGame()
        {
            gameOver = true;
            overDelay = 1.2f;
        }

        // ================================================================ draw ===
        void Render()
        {
            // v0.10.38: the playable screen is the same colourful SD slop as the attract loop.
            if (hasSlop) Array.Copy(bgSlop, px, px.Length);
            else Fill(Bg);
            if (kind == 1) DrawArkanoid();
            else if (kind == 2) DrawPong();
            else DrawSnake();
            if (!started) Overlay("PRESS SPACE");
            if (gameOver) Overlay("GAME OVER");
            SlopPost();
            Texture.SetPixels32(px);
            Texture.Apply(false);
        }

        void Overlay(string msg)
        {
            DarkenRect(38, 92, 244, 58, 0.58f);
            Color32 col = gameOver ? Hsv(0.88f, 1f, 1f) : Hsv(0.16f, 1f, 1f);
            TextShadow(W / 2 - TextWidth(msg, 2) / 2, 105, msg, col, 2,
                       new Color32(0, 255, 230, 255));
        }

        void DrawArkanoid()
        {
            float t = clock;
            if (t % 7f < 0.45f) TintAll(Pal(t, 0), 0.16f);
            DarkenRect(0, 0, W, 32, 0.55f);
            TextShadow(14, 12, "SCORE " + Digits(score, 4), Pal(t, 1), 2,
                       new Color32(8, 6, 8, 255));
            TextShadow(236, 12, "LIVES " + Mathf.Max(0, lives), Pal(t, 3), 2,
                       new Color32(8, 6, 8, 255));

            int slideRow = (int)(t / 4f) % 5;
            int slide = (t % 4f) < 0.25f ? 3 : 0;
            int flip = (int)(t * 6f);
            for (int row = 0; row < 5; row++)
                for (int col = 0; col < 10; col++)
                {
                    if (!bricks[row, col]) continue;
                    int bx = Mathf.RoundToInt(16f + col * 28.8f);
                    int by = Mathf.RoundToInt(42f + row * 14f);
                    int x = bx + (row == slideRow ? slide : 0);
                    Color32 fill = Pal(t, row);
                    Color32 accent = Pal(t, (row + 3) % 6);
                    float wob = 0.88f + 0.12f * Mathf.Sin(t * 3f + row * 0.7f + bx * 0.05f);
                    fill = ScaleCol(fill, wob);
                    Rect(x, by, 26, 10, new Color32(7, 6, 8, 255));
                    Rect(x + 1, by + 1, 24, 8, fill);
                    Rect(x + 1, by + 1, 24, 1, Lighten(fill, 0.35f));
                    Rect(x + 1, by + 1, 1, 8, Lighten(fill, 0.18f));
                    Rect(x + 2, by + 9, 24, 1, Darken(fill, 0.5f));
                    Rect(x + 4, by + 3, 3, 4, accent);
                    Rect(x + 18, by + 3, 3, 4, accent);
                    Rect(x + 1, by + 1, 5, 2, Darken(fill, 0.55f));
                    Rect(x + 20, by + 8, 5, 1, Darken(fill, 0.55f));
                    int hsh = bx * 13 + by * 7;
                    if (hsh % 5 == 0)
                    {
                        int dx = x + 4 + hsh % 18;
                        Rect(dx, by + 10, 1, 2, accent);
                    }
                    if (hsh % 7 == 0) Rect(x + 20, by + 1, 5, 2, new Color32(7, 6, 8, 255));
                    if (hsh % 11 == 0) Set(x + 8 + hsh % 10, by + 2, Lighten(fill, 0.7f));
                    if ((bx * 7 + by * 13 + flip * 31) % 211 < 4)
                        Rect(x + 1, by + 1, 24, 8, accent);
                }

            Color32 acc = Pal(t, 4);
            for (int i = 0; i < trail.Count - 1; i++)
            {
                int s = i < 3 ? 3 : 2;
                int tx = Mathf.RoundToInt(trail[i].x);
                int ty = Mathf.RoundToInt(trail[i].y);
                Rect(tx - s, ty - s, s * 2 + 1, s * 2 + 1, Darken(acc, 0.35f + i * 0.07f));
            }

            int px0 = Mathf.RoundToInt(paddleX);
            int py = Mathf.RoundToInt(Mathf.Sin(t * 4f));
            Color32 pb = Pal(t, 1);
            Color32 pa = Pal(t, 3);
            Rect(px0 - 30, 208 + py, 60, 14, new Color32(6, 5, 7, 255));
            Rect(px0 - 28, 210 + py, 56, 10, pb);
            Rect(px0 - 28, 210 + py, 56, 1, Lighten(pb, 0.55f));
            Rect(px0 - 28, 220 + py, 56, 1, Darken(pb, 0.5f));
            int off = (int)(t * 24f) % 12;
            for (int x = px0 - 27; x < px0 + 27; x++)
                if (((x + off) / 6) % 2 == 0) Rect(x, 213 + py, 1, 4, pa);
            Rect(px0 - 26, 211 + py, 3, 3, Darken(pb, 0.65f));
            Rect(px0 + 23, 211 + py, 3, 3, Darken(pb, 0.65f));

            int cx = Mathf.RoundToInt(ballX), cy = Mathf.RoundToInt(ballY);
            FillCircle(cx, cy, 12, Darken(acc, 0.78f));
            FillCircle(cx, cy, 8, Darken(acc, 0.55f));
            FillCircle(cx, cy, 5, new Color32(16, 14, 16, 255));
            FillCircle(cx, cy, 4, pb);
            FillCircle(cx, cy, 1, White);
            if (ballHeld)
                TextShadow(W / 2 - TextWidth("SPACE TO LAUNCH", 2) / 2, 168, "SPACE TO LAUNCH",
                           Pal(t, 3), 2, new Color32(8, 6, 8, 255));
        }

        void DrawPong()
        {
            float t = clock;
            if (t % 6f < 0.45f) TintAll(Pal(t, 0), 0.16f);
            DarkenRect(0, 0, W, 40, 0.62f);
            int off = (int)(t * 10f) % 16;
            for (int y = 40; y < 212; y += 16)
            {
                Color32 c = ((y + off) / 16) % 2 == 0 ? Pal(t, 0) : Pal(t, 2);
                Rect(158, y, 4, 9, c);
                Rect(158, y, 4, 1, Lighten(c, 0.4f));
            }
            Rect(86, 6, 44, 30, new Color32(7, 6, 8, 255));
            Rect(190, 6, 44, 30, new Color32(7, 6, 8, 255));
            TextShadow(96, 10, sl.ToString(), Pal(t, 1), 4, new Color32(10, 8, 10, 255));
            TextShadow(200, 10, sr.ToString(), Pal(t, 3), 4, new Color32(10, 8, 10, 255));
            if ((int)(t * 9f) % 11 == 0)
            {
                TextShadow(98, 10, sl.ToString(), Pal(t, 4), 4, new Color32(10, 8, 10, 255));
                TextShadow(202, 10, sr.ToString(), Pal(t, 2), 4, new Color32(10, 8, 10, 255));
            }

            int swap = (int)(t / 4f) % 2;
            Color32 lb = Pal(t, swap == 0 ? 1 : 3);
            Color32 la = Pal(t, swap == 0 ? 2 : 4);
            Color32 rb = Pal(t, swap == 0 ? 3 : 1);
            Color32 ra = Pal(t, swap == 0 ? 4 : 2);
            int widen = Mathf.Sin(t * 7f) > 0f ? 1 : 0;

            void Slab(int x, int cy, Color32 b, Color32 a)
            {
                int x1 = x + 16 + widen;
                Rect(x, cy - 25, x1 - x + 1, 51, new Color32(6, 5, 7, 255));
                Rect(x + 2, cy - 23, x1 - x - 3, 47, b);
                Rect(x + 2, cy - 23, x1 - x - 3, 1, Lighten(b, 0.5f));
                Rect(x + 2, cy + 23, x1 - x - 3, 1, Darken(b, 0.55f));
                int off2 = (int)(t * 20f) % 10;
                for (int yy = cy - 21; yy < cy + 21; yy++)
                    if (((yy + off2) / 5) % 2 == 0) Rect(x + 5, yy, x1 - x - 9, 1, a);
                Rect(x + 3, cy - 20, 3, 3, Darken(b, 0.7f));
                Rect(x + 3, cy + 17, 3, 3, Darken(b, 0.7f));
            }

            Slab(26, Mathf.RoundToInt(lpY), lb, la);
            Slab(278, Mathf.RoundToInt(rpY), rb, ra);

            Color32 acc = Pal(t, 4);
            for (int i = 0; i < trail.Count - 1; i++)
            {
                int s = i < 4 ? 3 : 2;
                int tx = Mathf.RoundToInt(trail[i].x);
                int ty = Mathf.RoundToInt(trail[i].y);
                Rect(tx - s, ty - s, s * 2 + 1, s * 2 + 1, Darken(acc, 0.4f + i * 0.06f));
            }

            int cx = Mathf.RoundToInt(pbX), cy2 = Mathf.RoundToInt(pbY);
            FillCircle(cx, cy2, 12, Darken(acc, 0.78f));
            FillCircle(cx, cy2, 8, Darken(acc, 0.55f));
            FillCircle(cx, cy2, 5, new Color32(16, 14, 16, 255));
            FillCircle(cx, cy2, 4, Pal(t, 1));
            FillCircle(cx, cy2, 1, White);
        }

        void DrawSnake()
        {
            float t = clock;
            DarkenRect(Ox - 2, Oy - 2, Cols * Cell + 4, Rows * Cell + 4, 0.72f);
            for (int gx = 0; gx <= Cols; gx++)
                Rect(Ox + gx * Cell, Oy, 1, Rows * Cell, new Color32(64, 22, 86, 255));
            for (int gy = 0; gy <= Rows; gy++)
                Rect(Ox, Oy + gy * Cell, Cols * Cell, 1, new Color32(64, 22, 86, 255));
            Rect(Ox - 2, Oy - 2, Cols * Cell + 4, 1, Hsv(0.86f, 1f, 1f));
            Rect(Ox - 2, Oy + Rows * Cell + 1, Cols * Cell + 4, 1, Hsv(0.86f, 1f, 1f));
            Rect(Ox - 2, Oy - 2, 1, Rows * Cell + 4, Hsv(0.86f, 1f, 1f));
            Rect(Ox + Cols * Cell + 1, Oy - 2, 1, Rows * Cell + 4, Hsv(0.86f, 1f, 1f));

            TextShadow(14, 10, "SCORE " + Digits(score, 4),
                       new Color32(190, 255, 40, 255), 2, new Color32(120, 0, 90, 255));
            TextShadow(236, 10, "ZMEYKA", new Color32(255, 70, 220, 255), 2,
                       new Color32(0, 255, 190, 255));

            for (int i = 0; i < snake.Count; i++)
            {
                Color32 c = Hsv(i * 0.09f + t * 0.35f, 0.95f, i == 0 ? 1f : 0.55f);
                int x = Ox + snake[i].x * Cell, y = Oy + snake[i].y * Cell;
                Rect(x - 3, y - 3, Cell + 5, Cell + 5, Dim(c, 0.16f));
                Rect(x + 1, y + 1, Cell - 2, Cell - 2, c);
                if (i == 0)
                {
                    Rect(x + 1, y + 1, Cell - 2, 1, White);
                    Rect(x + 3, y + 3, 2, 2, new Color32(255, 0, 200, 255));
                    Rect(x + 7, y + 3, 2, 2, new Color32(0, 255, 230, 255));
                }
                else
                {
                    Set(x + 3, y + 4, White);
                    Set(x + 8, y + 8, new Color32(0, 0, 0, 255));
                }
            }
            int fx = Ox + food.x * Cell, fy = Oy + food.y * Cell;
            int pulse = 2 + Mathf.RoundToInt(2f * (0.5f + 0.5f * Mathf.Sin(t * 8f)));
            GlowCircle(fx + 6, fy + 6, 4 + pulse, new Color32(255, 40, 180, 255));
        }

        // ============================================================== helpers ===
        // v0.10.39: two brutal palettes that cross-fade over 8 s; no rainbow.
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

        static Color32 Pal(float t, int i)
        {
            float k = 0.5f - 0.5f * Mathf.Cos(t * 2f * Mathf.PI / 8f);
            return Mix(PalA[i % 6], PalB[i % 6], k);
        }

        static Color32 ScaleCol(Color32 c, float k)
        {
            return new Color32((byte)Mathf.Clamp(c.r * k, 0, 255),
                               (byte)Mathf.Clamp(c.g * k, 0, 255),
                               (byte)Mathf.Clamp(c.b * k, 0, 255), 255);
        }

        static Color32 Lighten(Color32 c, float k)
        {
            return new Color32((byte)(c.r + (255 - c.r) * k), (byte)(c.g + (255 - c.g) * k),
                               (byte)(c.b + (255 - c.b) * k), 255);
        }

        static Color32 Darken(Color32 c, float k)
        {
            return new Color32((byte)(c.r * (1f - k)), (byte)(c.g * (1f - k)),
                               (byte)(c.b * (1f - k)), 255);
        }

        void TintAll(Color32 tint, float k)
        {
            for (int i = 0; i < px.Length; i++) px[i] = Mix(px[i], tint, k);
        }

        static Color32 Hsv(float h, float s, float v)
        {
            Color c = Color.HSVToRGB(h - Mathf.Floor(h), Mathf.Clamp01(s), Mathf.Clamp01(v));
            return new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), 255);
        }

        static Color32 Dim(Color32 c, float k)
        {
            return new Color32((byte)(c.r * k), (byte)(c.g * k), (byte)(c.b * k), 255);
        }

        static Color32 Mix(Color32 a, Color32 b, float k)
        {
            return new Color32((byte)(a.r * (1 - k) + b.r * k),
                               (byte)(a.g * (1 - k) + b.g * k),
                               (byte)(a.b * (1 - k) + b.b * k), 255);
        }

        void GlowCircle(int cx, int cy, int r, Color32 c)
        {
            FillCircle(cx, cy, r + 5, Dim(c, 0.20f));
            FillCircle(cx, cy, r + 2, Dim(c, 0.35f));
            FillCircle(cx, cy, r, c);
            FillCircle(cx, cy, Mathf.Max(1, r / 2), White);
        }

        void TextShadow(int x, int y, string s, Color32 c, int sc, Color32 shadow)
        {
            Text(x + sc, y + sc, s, shadow, sc);
            Text(x, y, s, c, sc);
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

        /// <summary>Scanlines + periodic horizontal glitch + a few bright speckles, matching the
        /// offline attract renderer.</summary>
        void SlopPost()
        {
            for (int y = 1; y < H; y += 2)
                for (int x = 0; x < W; x++)
                {
                    int i = Index(x, y);
                    Color32 c = px[i];
                    px[i] = new Color32((byte)(c.r * 0.78f), (byte)(c.g * 0.78f),
                                        (byte)(c.b * 0.78f), 255);
                }
            int tick = (int)(clock * 10f);
            if (tick % 3 == 0)
            {
                var rng = new System.Random(11 + tick);
                var line = new Color32[W];
                for (int b = 0; b < 2; b++)
                {
                    int y0 = rng.Next(0, H - 8);
                    int hh = rng.Next(3, 12);
                    int sh = rng.Next(-5, 6);
                    if (sh == 0) continue;
                    for (int yy = y0; yy < y0 + hh; yy++)
                    {
                        for (int x = 0; x < W; x++) line[x] = px[Index(x, yy)];
                        for (int x = 0; x < W; x++)
                        {
                            int sx = x - sh;
                            px[Index(x, yy)] = (sx >= 0 && sx < W) ? line[sx] : line[x];
                        }
                    }
                }
            }
            var nr = new System.Random(131 + tick);
            for (int i = 0; i < 90; i++)
            {
                int x = nr.Next(0, W), y = nr.Next(0, H);
                Set(x, y, new Color32(255, 255, 255, 255));
            }
        }

        static string Digits(int n, int count)
        {
            string s = Mathf.Max(0, n).ToString();
            while (s.Length < count) s = "0" + s;
            if (s.Length > count) s = s.Substring(s.Length - count);
            return s;
        }

        void Fill(Color32 c)
        {
            for (int i = 0; i < px.Length; i++) px[i] = c;
        }

        int Index(int x, int y)
        {
            if (x < 0) x = 0; if (x >= W) x = W - 1;
            if (y < 0) y = 0; if (y >= H) y = H - 1;
            return (H - 1 - y) * W + x;
        }

        void Set(int x, int y, Color32 c)
        {
            if (x < 0 || x >= W || y < 0 || y >= H) return;
            px[(H - 1 - y) * W + x] = c;
        }

        void Rect(int x, int y, int w, int h, Color32 c)
        {
            for (int yy = y; yy < y + h; yy++)
                for (int xx = x; xx < x + w; xx++) Set(xx, yy, c);
        }

        void FillCircle(int cx, int cy, int r, Color32 c)
        {
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                    if (x * x + y * y <= r * r) Set(cx + x, cy + y, c);
        }

        void Frame()
        {
            Rect(6, 6, W - 12, 1, Steel);
            Rect(6, H - 7, W - 12, 1, Steel);
            Rect(6, 6, 1, H - 12, Steel);
            Rect(W - 7, 6, 1, H - 12, Steel);
        }

        // ---- 3x5 font ---------------------------------------------------------
        static readonly Dictionary<char, string[]> Glyphs = BuildFont();

        static Dictionary<char, string[]> BuildFont()
        {
            var g = new Dictionary<char, string[]>();
            void A(char c, string a, string b, string d, string e, string f) =>
                g[c] = new[] { a, b, d, e, f };
            A('A', "010", "101", "111", "101", "101");
            A('B', "110", "101", "110", "101", "110");
            A('C', "011", "100", "100", "100", "011");
            A('D', "110", "101", "101", "101", "110");
            A('E', "111", "100", "110", "100", "111");
            A('F', "111", "100", "110", "100", "100");
            A('G', "011", "100", "101", "101", "011");
            A('H', "101", "101", "111", "101", "101");
            A('I', "111", "010", "010", "010", "111");
            A('J', "001", "001", "001", "101", "010");
            A('K', "101", "101", "110", "101", "101");
            A('L', "100", "100", "100", "100", "111");
            A('M', "101", "111", "111", "101", "101");
            A('N', "101", "111", "111", "111", "101");
            A('O', "010", "101", "101", "101", "010");
            A('P', "110", "101", "110", "100", "100");
            A('Q', "010", "101", "101", "111", "011");
            A('R', "110", "101", "110", "101", "101");
            A('S', "011", "100", "010", "001", "110");
            A('T', "111", "010", "010", "010", "010");
            A('U', "101", "101", "101", "101", "111");
            A('V', "101", "101", "101", "101", "010");
            A('W', "101", "101", "111", "111", "101");
            A('X', "101", "101", "010", "101", "101");
            A('Y', "101", "101", "010", "010", "010");
            A('Z', "111", "001", "010", "100", "111");
            A('0', "111", "101", "101", "101", "111");
            A('1', "010", "110", "010", "010", "111");
            A('2', "110", "001", "010", "100", "111");
            A('3', "110", "001", "010", "001", "110");
            A('4', "101", "101", "111", "001", "001");
            A('5', "111", "100", "110", "001", "110");
            A('6', "011", "100", "111", "101", "111");
            A('7', "111", "001", "010", "010", "010");
            A('8', "111", "101", "111", "101", "111");
            A('9', "111", "101", "111", "001", "110");
            A(':', "000", "010", "000", "010", "000");
            A('-', "000", "000", "111", "000", "000");
            A(' ', "000", "000", "000", "000", "000");
            A('!', "010", "010", "010", "000", "010");
            return g;
        }

        int TextWidth(string s, int sc) => s.Length * 4 * sc - sc;

        void Text(int x, int y, string s, Color32 c, int sc)
        {
            int cx = x;
            foreach (char raw in s.ToUpperInvariant())
            {
                if (!Glyphs.TryGetValue(raw, out var glyph)) glyph = Glyphs[' '];
                for (int ry = 0; ry < 5; ry++)
                    for (int rx = 0; rx < 3; rx++)
                        if (glyph[ry][rx] == '1')
                            Rect(cx + rx * sc, y + ry * sc, sc, sc, c);
                cx += 4 * sc;
            }
        }

        void TextCentered(string s, int y, Color32 c, int sc)
        {
            Text((W - TextWidth(s, sc)) / 2, y, s, c, sc);
        }
    }
}
