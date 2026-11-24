using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace BabbdiCoop
{
    // Part of the SovietDistrict builder - see SovietDistrict.cs.
    internal static partial class SovietDistrict
    {

        /// <summary>v0.9.73 вЂ” plan points 5/8: a dark abstract bas-relief on the outward long wall of
        /// the middle east-flank block. The wall (local +z) is built solid, and the ornament is a field
        /// of raised charcoal bars: frames, a ringed medallion with a sunburst, zigzag ribbons, glyph
        /// friezes and bosses. Stage 1 = the composition; the second pass adds the labyrinth and the
        /// interlacing arcs.</summary>
        private static void BuildRelief(Bldg b)
        {
            if (b == null || b.go == null) return;
            float W = b.w - 4f;
            float yBot = b.baseY + Mathf.Max(10f, b.fh * 2f);
            float yTop = b.Top - 8f;
            float H = yTop - yBot;
            if (W < 10f || H < 30f)
            {
                log.LogInfo($"[RELIEF] skipped: field {W:0.0} x {H:0.0} m is too small on this block");
                return;
            }
            var g = new Geo();
            float z = b.d * 0.5f + 0.01f;
            float cy = (yBot + yTop) * 0.5f;
            float hw = W * 0.5f, hh = H * 0.5f;
            Vector2 V(float x, float y) => new Vector2(x, cy + y);

            // outer and inner frame
            RBar(g, z, V(-hw, -hh), V(hw, -hh), 0.70f, 0.55f);
            RBar(g, z, V(-hw, hh), V(hw, hh), 0.70f, 0.55f);
            RBar(g, z, V(-hw, -hh), V(-hw, hh), 0.70f, 0.55f);
            RBar(g, z, V(hw, -hh), V(hw, hh), 0.70f, 0.55f);
            RBar(g, z, V(-hw + 2.2f, -hh + 2.2f), V(hw - 2.2f, -hh + 2.2f), 0.30f, 0.34f);
            RBar(g, z, V(-hw + 2.2f, hh - 2.2f), V(hw - 2.2f, hh - 2.2f), 0.30f, 0.34f);
            RBar(g, z, V(-hw + 2.2f, -hh + 2.2f), V(-hw + 2.2f, hh - 2.2f), 0.30f, 0.34f);
            RBar(g, z, V(hw - 2.2f, -hh + 2.2f), V(hw - 2.2f, hh - 2.2f), 0.30f, 0.34f);

            // the medallion: a heavy ring, a 24-ray sunburst, a diamond and a central boss
            RRing(g, z, V(0f, 0f), 6.2f, 7.6f, 0.60f, 32);
            RRing(g, z, V(0f, 0f), 4.1f, 4.5f, 0.40f, 24);
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI * 2f / 24f;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                RBar(g, z, V(0f, 0f) + d * 8.2f, V(0f, 0f) + d * 11.4f, 0.42f, 0.42f);
            }
            RBar(g, z, V(-3.4f, 0f), V(0f, 3.4f), 0.55f, 0.50f);
            RBar(g, z, V(0f, 3.4f), V(3.4f, 0f), 0.55f, 0.50f);
            RBar(g, z, V(3.4f, 0f), V(0f, -3.4f), 0.55f, 0.50f);
            RBar(g, z, V(0f, -3.4f), V(-3.4f, 0f), 0.55f, 0.50f);
            RBar(g, z, V(0f, -4.7f), V(0f, 4.7f), 0.80f, 0.45f);
            RDisc(g, z, V(0f, 0f), 1.9f, 0.48f, 24);

            // the zigzag ribbons on the outside of the sunburst
            for (int sd = -1; sd <= 1; sd += 2)
            {
                float bx = sd * (hw - 3.2f);
                float y = -hh + 4f;
                bool right = true;
                while (y < hh - 4.1f)
                {
                    float y2 = Mathf.Min(y + 2.6f, hh - 4f);
                    float x1 = bx + (right ? 1.25f : -1.25f);
                    float x2 = bx + (right ? -1.25f : 1.25f);
                    RBar(g, z, V(x1, y), V(x2, y2), 0.46f, 0.34f);
                    y = y2;
                    right = !right;
                }
            }

            // glyph friezes top and bottom
            for (int row = -1; row <= 1; row += 2)
            {
                float gy = row * (hh - 4.0f);
                for (int i = 0; i < 11; i++)
                {
                    float gx = -hw + 3.6f + i * (W - 7.2f) / 10f;
                    RGlyph(g, z, V(gx, gy), 1.05f, i % 6, 0.32f);
                }
            }

            // four corner bosses and a few scattered studs
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    RDisc(g, z, V(sx * (hw - 4.4f), sy * (hh - 5.4f)), 1.15f, 0.36f, 16);
                    RDisc(g, z, V(sx * (hw - 7.2f), sy * (hh - 7.6f)), 0.52f, 0.30f, 12);
                }
            for (int i = -3; i <= 3; i++)
                RDisc(g, z, V(i * (hw / 4.2f), -hh + 1.0f), 0.28f, 0.24f, 10);

            // v2 (point 8): the abstract layers вЂ” three broken concentric rings around the medallion,
            // a second offset fan of short rays, two spirals in the tall empty thirds, a constellation
            // and a set of asymmetric shards. Deliberately not mirror-symmetric.
            for (int ring = 0; ring < 3; ring++)
            {
                float rr = 10.5f + ring * 3.0f;
                for (int a = 0; a < 3; a++)
                    RArc(g, z, V(0f, 0f), rr, a * 120f + 8f, a * 120f + 112f, 10, 0.28f, 0.30f);
            }
            for (int i = 0; i < 24; i++)
            {
                float a = (i * 15f + 7.5f) * Mathf.Deg2Rad;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                RBar(g, z, V(0f, 0f) + d * 13.0f, V(0f, 0f) + d * 14.4f, 0.18f, 0.22f);
            }
            RSpiral(g, z, V(-hw * 0.52f, hh * 0.56f), 1.1f, 5.8f, 2.2f, 44, 0.24f, 0.30f, 0f);
            RSpiral(g, z, V(hw * 0.52f, -hh * 0.56f), 1.1f, 5.8f, 2.2f, 44, 0.24f, 0.30f, Mathf.PI);
            // constellation: studs joined by thin rods (upper right, lower left)
            {
                var cd = new[] {
                    new Vector2(0.55f, 0.72f), new Vector2(0.62f, 0.65f), new Vector2(0.58f, 0.55f),
                    new Vector2(0.48f, 0.58f), new Vector2(0.45f, 0.68f), new Vector2(0.66f, 0.78f) };
                for (int i = 0; i < cd.Length; i++)
                    RDisc(g, z, V(cd[i].x * hw, cd[i].y * hh), 0.30f + (i % 3) * 0.08f, 0.26f, 10);
                for (int i = 0; i + 1 < cd.Length; i++)
                    RBar(g, z, V(cd[i].x * hw, cd[i].y * hh),
                         V(cd[i + 1].x * hw, cd[i + 1].y * hh), 0.10f, 0.18f);
                for (int i = 0; i < 5; i++)
                    RDisc(g, z, V(-(0.45f + i * 0.04f) * hw, -(0.80f - i * 0.03f) * hh),
                          0.22f + (i % 2) * 0.10f, 0.24f, 10);
                RBar(g, z, V(-0.45f * hw, -0.80f * hh), V(-0.57f * hw, -0.68f * hh), 0.08f, 0.16f);
                RBar(g, z, V(-0.57f * hw, -0.68f * hh), V(-0.50f * hw, -0.62f * hh), 0.08f, 0.16f);
            }
            // asymmetric shards, lengths and depths varied
            {
                var sh = new[] {
                    new Vector4(-0.72f, 0.34f, -0.58f, 0.42f), new Vector4(0.70f, 0.18f, 0.55f, 0.28f),
                    new Vector4(-0.66f, -0.36f, -0.48f, -0.44f), new Vector4(0.62f, -0.28f, 0.44f, -0.18f),
                    new Vector4(-0.40f, 0.86f, -0.30f, 0.78f), new Vector4(0.38f, -0.86f, 0.28f, -0.76f),
                    new Vector4(-0.28f, 0.14f, -0.18f, 0.22f), new Vector4(0.24f, -0.10f, 0.16f, -0.02f) };
                for (int i = 0; i < sh.Length; i++)
                    RBar(g, z, V(sh[i].x * hw, sh[i].y * hh), V(sh[i].z * hw, sh[i].w * hh),
                         0.24f + (i % 3) * 0.06f, 0.22f + (i % 2) * 0.10f);
            }

            var m = ReliefMaterial();
            var go = new GameObject("relief");
            go.transform.SetParent(b.go.transform, false);
            g.Apply(go, m);
            AddMeshCollider(go, "relief");
            log.LogInfo($"[RELIEF] dark ornament on the outward wall of the block at {b.p.x:0.0},{b.p.z:0.0} " +
                        $"({b.w:0.0}x{b.d:0.0} m, {b.floors} fl): field {W:0.0} x {H:0.0} m, " +
                        $"medallion r 7,6 m + 24 rays, 2 zigzag ribbons, 22 glyphs, 12 bosses; " +
                        $"{g.V.Count} verts, charcoal {N(m)}");
        }

        private static void RBar(Geo g, float z, Vector2 a, Vector2 b, float w, float d)
        {
            Vector2 mid = (a + b) * 0.5f;
            float len = (b - a).magnitude;
            if (len < 0.02f) return;
            float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            g.Box(0, new Vector3(mid.x, mid.y, z + d * 0.5f - 0.01f),
                  new Vector3(len, w, d), Quaternion.Euler(0f, 0f, ang), 0.55f);
        }

        private static void RArc(Geo g, float z, Vector2 c, float r, float a0, float a1, int seg,
                                 float w, float d)
        {
            Vector2 Prev(float t) => c + new Vector2(Mathf.Cos(t * Mathf.Deg2Rad),
                                                     Mathf.Sin(t * Mathf.Deg2Rad)) * r;
            Vector2 prev = Prev(a0);
            for (int i = 1; i <= seg; i++)
            {
                Vector2 q = Prev(Mathf.Lerp(a0, a1, i / (float)seg));
                RBar(g, z, prev, q, w, d);
                prev = q;
            }
        }

        private static void RSpiral(Geo g, float z, Vector2 c, float r0, float r1, float turns,
                                    int seg, float w, float d, float phase)
        {
            Vector2 P(float t) => c + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * Mathf.Lerp(r0, r1, t / (turns * Mathf.PI * 2f));
            Vector2 prev = P(phase);
            for (int i = 1; i <= seg; i++)
            {
                float t = phase + turns * Mathf.PI * 2f * i / seg;
                Vector2 q = P(t);
                RBar(g, z, prev, q, w, d);
                prev = q;
            }
        }

        private static void RRing(Geo g, float z, Vector2 c, float r0, float r1, float d, int seg)
        {
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                var p0 = c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r1;
                var p1 = c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r1;
                Vector2 mid = (p0 + p1) * 0.5f;
                float chord = (p1 - p0).magnitude;
                float ang = Mathf.Atan2(p1.y - p0.y, p1.x - p0.x) * Mathf.Rad2Deg;
                g.Box(0, new Vector3(mid.x, mid.y, z + d * 0.5f - 0.01f),
                      new Vector3(chord, r1 - r0, d), Quaternion.Euler(0f, 0f, ang), 0.55f);
            }
        }

        private static void RDisc(Geo g, float z, Vector2 c, float r, float d, int seg)
        {
            var uv = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                Vector3 p0 = new Vector3(c.x + Mathf.Cos(a0) * r, c.y + Mathf.Sin(a0) * r, z + d);
                Vector3 p1 = new Vector3(c.x + Mathf.Cos(a1) * r, c.y + Mathf.Sin(a1) * r, z + d);
                g.Tri(0, new Vector3(c.x, c.y, z + d), p0, p1, Vector3.forward, uv, uv, uv);
                var n = new Vector3(Mathf.Cos((a0 + a1) * 0.5f), Mathf.Sin((a0 + a1) * 0.5f), 0f);
                g.Quad(0, new Vector3(p0.x, p0.y, z), new Vector3(p1.x, p1.y, z), p1, p0, n, 0.5f);
            }
        }

        private static void RGlyph(Geo g, float z, Vector2 c, float s, int kind, float d)
        {
            switch (kind)
            {
                case 0:
                    RRing(g, z, c, 0.55f * s, 0.88f * s, d, 16);
                    break;
                case 1:
                    {
                        Vector2 a = c + new Vector2(0f, s), b = c + new Vector2(-0.9f * s, -0.8f * s),
                                e = c + new Vector2(0.9f * s, -0.8f * s);
                        RBar(g, z, a, b, 0.22f * s, d); RBar(g, z, b, e, 0.22f * s, d);
                        RBar(g, z, e, a, 0.22f * s, d);
                        break;
                    }
                case 2:
                    RBar(g, z, c + new Vector2(-s, 0f), c + new Vector2(s, 0f), 0.24f * s, d);
                    RBar(g, z, c + new Vector2(0f, -s), c + new Vector2(0f, s), 0.24f * s, d);
                    break;
                case 3:
                    RBar(g, z, c + new Vector2(-s, -s), c + new Vector2(s, -s), 0.22f * s, d);
                    RBar(g, z, c + new Vector2(s, -s), c + new Vector2(s, s), 0.22f * s, d);
                    RBar(g, z, c + new Vector2(s, s), c + new Vector2(-s, s), 0.22f * s, d);
                    RBar(g, z, c + new Vector2(-s, s), c + new Vector2(-s, -s), 0.22f * s, d);
                    break;
                case 4:
                    RBar(g, z, c + new Vector2(-s, 0.7f * s), c + new Vector2(0f, -0.7f * s), 0.24f * s, d);
                    RBar(g, z, c + new Vector2(0f, -0.7f * s), c + new Vector2(s, 0.7f * s), 0.24f * s, d);
                    break;
                default:
                    g.Box(0, new Vector3(c.x, c.y, z + d * 0.5f - 0.01f),
                          new Vector3(0.62f * s, 0.62f * s, d), Quaternion.identity, 0.5f);
                    break;
            }
        }

        private static Material mRelief;

        private static Material ReliefMaterial()
        {
            if (mRelief != null) return mRelief;
            const int S = 256;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, true)
            { name = "CoopReliefCharcoal", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float n = FH01(x, y, 17) * 0.6f + FH01(x / 8, y / 8, 23) * 0.4f;
                    float v = 0.085f + 0.050f * n;
                    float vein = Mathf.Abs(Mathf.Sin(x * 0.052f + Mathf.Sin(y * 0.031f) * 2.2f));
                    if (vein < 0.075f) v *= 0.55f;
                    if (FH01(x / 16, y / 16, 41) > 0.94f) v *= 1.55f;   // sparse pale chips
                    px[y * S + x] = new Color(v * 1.07f, v * 0.99f, v * 0.92f);
                }
            tex.SetPixels32(px);
            tex.Apply(true);
            var m = new Material(mRough) { name = "CoopRelief" };
            m.SetTexture("_BC", tex);
            m.SetTextureScale("_BC", Vector2.one);
            mRelief = m;
            log.LogInfo("[RELIEF] material: CoopRelief on " + m.shader.name + " (charcoal 256x256, mean ~0,11)");
            return m;
        }

        private static Material mBanner;

        /// <summary>v0.9.76 вЂ” a dark-red woven fabric for the House's two banners (no furniture atlas:
        /// the room furniture was removed at the user's request, the banners stay).</summary>
        private static Material BannerMaterial()
        {
            if (mBanner != null) return mBanner;
            const int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, true)
            { name = "CoopBannerRed", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float n = FH01(x, y, 31);
                    float fold = 0.88f + 0.18f * Mathf.Sin(x * 0.14f + Mathf.Sin(y * 0.03f) * 1.5f);
                    Color c = new Color(0.38f, 0.065f, 0.050f) * (0.86f + 0.22f * n) * fold;
                    px[y * S + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(true);
            var m = new Material(mRough) { name = "CoopBanner" };
            m.SetTexture("_BC", tex);
            m.SetTextureScale("_BC", Vector2.one);
            mBanner = m;
            log.LogInfo("[HOUSE] banner material: CoopBanner 128x128 (dark red woven fabric)");
            return m;
        }

        /// <summary>v0.9.72 вЂ” a flat five-pointed star, double-sided, for the mast on the core.</summary>
        private static void Star(Geo g, Vector3 c, float r, Vector3 n)
        {
            n = n.normalized;
            Vector3 ex = Mathf.Abs(n.z) > 0.5f ? Vector3.right : Vector3.forward;
            Vector3 ey = Vector3.up;
            var uv = new Vector2(0.5f, 0.30f);   // the deep-red part of the glow gradient
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 nn = n * side;
                for (int i = 0; i < 10; i++)
                {
                    float a0 = (90f + i * 36f) * Mathf.Deg2Rad;
                    float a1 = (90f + (i + 1) * 36f) * Mathf.Deg2Rad;
                    float r0 = (i % 2 == 0) ? r : r * 0.42f;
                    float r1 = (i % 2 == 0) ? r * 0.42f : r;
                    Vector3 p0 = c + (ex * Mathf.Cos(a0) + ey * Mathf.Sin(a0)) * r0;
                    Vector3 p1 = c + (ex * Mathf.Cos(a1) + ey * Mathf.Sin(a1)) * r1;
                    g.Tri(0, c, p0, p1, nn, uv, uv, uv);
                }
            }
        }

        private static void TowerDetail(Geo g, Bldg b, Shell sh, float hw, float hd,
                                        float y0, float yTop, int floors, float fh)
        {
            int f0 = Mathf.Max(1, b.winFromFloor);
            for (int f = f0; f < floors; f++)
            {
                float y = y0 + f * fh;
                // a string course at every floor line, 0,30 m proud, and a thin sill under the windows
                g.Box(0, new Vector3(0f, y - 0.11f, hd + 0.12f),
                      new Vector3(b.w, 0.22f, 0.36f), Quaternion.identity, 0.4f);
                g.Box(0, new Vector3(0f, y - 0.11f, -hd - 0.12f),
                      new Vector3(b.w, 0.22f, 0.36f), Quaternion.identity, 0.4f);
                g.Box(0, new Vector3(hw + 0.12f, y - 0.11f, 0f),
                      new Vector3(0.36f, 0.22f, b.d), Quaternion.identity, 0.4f);
                g.Box(0, new Vector3(-hw - 0.12f, y - 0.11f, 0f),
                      new Vector3(0.36f, 0.22f, b.d), Quaternion.identity, 0.4f);
                float ys = y + b.sill - 0.06f;
                g.Box(0, new Vector3(0f, ys, hd + 0.06f),
                      new Vector3(b.w, 0.12f, 0.20f), Quaternion.identity, 0.4f);
                g.Box(0, new Vector3(0f, ys, -hd - 0.06f),
                      new Vector3(b.w, 0.12f, 0.20f), Quaternion.identity, 0.4f);
            }
            // the cornice: the tower's cap, 0,4 m tall and 0,7 m proud
            g.Box(0, new Vector3(0f, yTop - 0.2f, hd + 0.2f),
                  new Vector3(b.w + 0.6f, 0.4f, 0.7f), Quaternion.identity, 0.4f);
            g.Box(0, new Vector3(0f, yTop - 0.2f, -hd - 0.2f),
                  new Vector3(b.w + 0.6f, 0.4f, 0.7f), Quaternion.identity, 0.4f);
            g.Box(0, new Vector3(hw + 0.2f, yTop - 0.2f, 0f),
                  new Vector3(0.7f, 0.4f, b.d + 0.4f), Quaternion.identity, 0.4f);
            g.Box(0, new Vector3(-hw - 0.2f, yTop - 0.2f, 0f),
                  new Vector3(0.7f, 0.4f, b.d + 0.4f), Quaternion.identity, 0.4f);

            // vertical ribs on the two long facades, on the room-cell boundaries вЂ” the deep piers
            // of the honeycomb; they start above the blank base band.
            float yr0 = y0 + f0 * fh, yr1 = yTop - 0.45f;
            if (yr1 - yr0 < 1f) return;
            for (int k = 1; k < sh.seg; k++)
            {
                float x = sh.x0 + k * sh.segLen;
                for (int s = -1; s <= 1; s += 2)
                    g.Box(0, new Vector3(x, (yr0 + yr1) * 0.5f, s * (hd + 0.10f)),
                          new Vector3(0.20f, yr1 - yr0, 0.30f), Quaternion.identity, 0.4f);
            }
        }

        /// <summary>
        /// v0.9.16 вЂ” the stair of **every** block (tall stalinkas and the House of Soviets' towers
        /// alike). Two flights run along z, side by side in x, with the landing from `PlanStair` at
        /// each end: up northwards, across the half-landing, back south to the corridor door. The
        /// flight is 2-3 m long and every number here comes from the plan the slab hole uses.
        /// </summary>
        private static void StairsZ(Geo g, Shell sh, float xs, float yf)
        {
            float fh = sh.fh;
            float fw = sh.stFw, well = sh.stWell;
            float xa0 = xs, xa1 = xs + fw;                    // the west flight
            float xb0 = xs + fw + well, xb1 = xs + sh.segLen;  // the east flight
            float rl = 1.00f;                                  // handrail height above the tread line

            FlightZ(g, xa0, xa1, sh.stZLow, sh.stZHigh, yf, sh.stN, sh.stRise, false,
                    SLAB, StairPlate);                                                    // up, northwards
            Landing(g, xs, xs + sh.segLen, sh.stZHigh, sh.zi, yf + fh * 0.5f);          // half-landing
            FlightZ(g, xb0, xb1, sh.stZLow, sh.stZHigh,                                // back, south
                    yf + fh * 0.5f, sh.stN, sh.stRise, true, StairPlate, SLAB);

            // v0.9.35: the railings of the open well вЂ” a handrail and three posts along each flight's
            // well-side edge, plus a guard across the well at the floor and at the half-landing (the
            // two places where you would otherwise walk straight into the shaft).
            Vector3 A(float x, float y, float z) => new Vector3(x, y, z);
            float half = yf + fh * 0.5f;
            RailRun(g, A(xa1 - 0.03f, yf + sh.stRise * 0.5f, sh.stZLow + 0.03f),
                       A(xa1 - 0.03f, half + sh.stRise * 0.5f, sh.stZHigh - 0.03f),
                       1f, rl, sh.stN, sh.stTread, true, yf);
            RailRun(g, A(xb0 + 0.03f, half + sh.stRise * 0.5f, sh.stZHigh - 0.03f),
                       A(xb0 + 0.03f, yf + fh + sh.stRise * 0.5f, sh.stZLow + 0.03f),
                       1f, rl, sh.stN, sh.stTread, true, half);
            RailRun(g, A(xa1, yf, sh.stZLow), A(xb0, yf, sh.stZLow), 1f, rl, 0, 0f, false, yf);
            RailRun(g, A(xa1, half, sh.stZHigh), A(xb0, half, sh.stZHigh), 1f, rl, 0, 0f, false, half);
        }

        /// <summary>
        /// v0.9.35 вЂ” one run of a thin metal railing: a handrail at `h` above the two base points,
        /// a mid rail and `posts` vertical posts. `stepped` snaps the posts onto the treads of a
        /// flight (the base points are then the first and last tread's nosing line, `tread` apart,
        /// `count` risers), so no post floats over a step. `yBase` is the floor the flight leaves.
        /// </summary>
        private static void RailRun(Geo g, Vector3 a, Vector3 b, float midFrac, float h,
                                    int count, float tread, bool stepped, float yBase)
        {
            const float t = 0.045f;
            Vector3 ha = a + Vector3.up * h, hb = b + Vector3.up * h;
            Bar(g, ha, hb, t);
            if (midFrac > 0.01f)
                Bar(g, a + Vector3.up * (h * 0.52f), b + Vector3.up * (h * 0.52f), t * 0.8f);
            int posts = count > 0 ? 3 : 2;
            for (int i = 0; i < posts; i++)
            {
                float u = i / (float)(posts - 1);
                Vector3 p = Vector3.Lerp(a, b, u);
                float top = Mathf.Lerp(ha.y, hb.y, u);
                if (stepped && count > 0)          // stand the post **on** the tread under it
                {
                    float along = (p.z - a.z) / (Mathf.Abs(b.z - a.z) > 0.01f ? b.z - a.z : 0.01f);
                    int k = Mathf.Clamp(Mathf.FloorToInt(along * count), 0, count - 1);
                    p.y = Mathf.Max(p.y, yBase + (k + 1) * (b.y - a.y) / count);
                }
                Post(g, p, Mathf.Max(0.2f, top - p.y), t);
            }
        }

        /// <summary>One metal bar between two points (a handrail or a sloped rail).</summary>
        private static void Bar(Geo g, Vector3 a, Vector3 b, float t)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.02f) return;
            g.Box(1, (a + b) * 0.5f, new Vector3(t, t, len + t),
                  Quaternion.LookRotation(d / len, Vector3.up), 0.6f);
        }

        /// <summary>One vertical post standing on `p` (its base), `h` tall.</summary>
        private static void Post(Geo g, Vector3 p, float h, float t)
            => g.Box(1, p + Vector3.up * (h * 0.5f), new Vector3(t, h, t), Quaternion.identity, 0.6f);

        /// <summary>
        /// v0.9.37 вЂ” a flight as a **concrete plate** (the user's reference photos), not the triangular
        /// prism of v0.9.36: the soffit runs from the floor at the bottom end up to the **underside of the
        /// landing** at the top, the side faces are only the thin band between that soffit and the treads,
        /// and the body is closed вЂ” so a flight reads as a slab with steps on it and you cannot see
        /// through it from below (the old side faces ran all the way down to the floor, which left the
        /// underside open: the "transparent" stairs the user photographed).
        /// `toSouth` = the flight rises towards -z (its lowest tread is at `z1`).
        /// </summary>
        private static void FlightZ(Geo g, float x0, float x1, float z0, float z1, float yBase,
                                    int count, float rise, bool toSouth,
                                    float backLow = 0f, float backHigh = 0f)
        {
            const float plate = StairPlate;                  // v0.9.38: 2,5x the floor slab = 0,65 m (the user: too thin)
            float tread = (z1 - z0) / count;
            float run = Mathf.Abs(z1 - z0);
            float zLow = toSouth ? z1 : z0, zHigh = toSouth ? z0 : z1;
            // v0.9.38: the soffit is **parallel to the flight** at a constant vertical thickness, like the
            // reference photos вЂ” the old one tapered from the floor to the landing, which read as a wedge
            float slope = (count * rise) / Mathf.Max(0.01f, run);
            float Sof(float z) => yBase + slope * Mathf.Abs(z - zLow) - plate;
            // v0.9.39: the plate's sides are built as **closed per-tread cells** (bottom = the soffit
            // strip, top = the tread, front = the riser, back = the face below the next riser, both
            // sides) instead of an open shell. Every cell is a closed solid, so no view вЂ” not even
            // through a seam вЂ” can reach the empty inside: that inside was the "прозрачные торцы".
            for (int i = 0; i < count; i++)
            {
                float a = toSouth ? z1 - (i + 1) * tread : z0 + i * tread;
                float top = yBase + (i + 1) * rise;
                float rz = toSouth ? a + tread : a;      // the riser on the edge walked up to
                float sa = Sof(a), sb = Sof(a + tread);
                g.Quad(0, new Vector3(x0, top, a), new Vector3(x1, top, a),
                          new Vector3(x1, top, a + tread), new Vector3(x0, top, a + tread), Vector3.up, 0.6f);
                g.Quad(0, new Vector3(x0, top - rise, rz), new Vector3(x1, top - rise, rz),
                          new Vector3(x1, top, rz), new Vector3(x0, top, rz),
                          toSouth ? Vector3.forward : Vector3.back, 0.6f);
                // the soffit strip of this cell
                g.Quad(0, new Vector3(x0, sa, a), new Vector3(x1, sa, a),
                          new Vector3(x1, sb, a + tread), new Vector3(x0, sb, a + tread), Vector3.down, 0.6f);
                // the cell's back face (the plane below the *next* riser) вЂ” closes the volume
                if (i + 1 < count)
                {
                    float rb = toSouth ? a : a + tread;
                    float yb2 = Sof(rb);
                    g.Quad(0, new Vector3(x0, yb2, rb), new Vector3(x1, yb2, rb),
                              new Vector3(x1, top, rb), new Vector3(x0, top, rb),
                              toSouth ? Vector3.back : Vector3.forward, 0.6f);
                }
                // both sides of this cell
                g.Quad(0, new Vector3(x0, sa, a), new Vector3(x0, sb, a + tread),
                          new Vector3(x0, top, a + tread), new Vector3(x0, top, a), Vector3.left, 0.6f);
                g.Quad(0, new Vector3(x1, sa, a), new Vector3(x1, sb, a + tread),
                          new Vector3(x1, top, a + tread), new Vector3(x1, top, a), Vector3.right, 0.6f);
            }
            // The two ends, closed **only where the neighbouring solid does not already close them**
            // (`backLow` / `backHigh` = that solid's thickness, 0 = nothing behind). Never coincident
            // with the floor slab's shaft rim or the landing's face вЂ” coincident faces from the same
            // concrete z-fight, which reads as a dithered "transparent" patch.
            float lowTop = yBase - backLow;
            if (lowTop > Sof(zLow) + 0.005f)
                g.Quad(0, new Vector3(x0, Sof(zLow), zLow), new Vector3(x1, Sof(zLow), zLow),
                          new Vector3(x1, lowTop, zLow), new Vector3(x0, lowTop, zLow),
                          toSouth ? Vector3.forward : Vector3.back, 0.6f);
            float highY = yBase + count * rise, highTop = highY - backHigh;
            if (highTop > Sof(zHigh) + 0.005f)
                g.Quad(0, new Vector3(x0, Sof(zHigh), zHigh), new Vector3(x1, Sof(zHigh), zHigh),
                          new Vector3(x1, highTop, zHigh), new Vector3(x0, highTop, zHigh),
                          toSouth ? Vector3.back : Vector3.forward, 0.6f);
        }

        /// <summary>
        /// v0.9.37 вЂ” a landing as a closed slab (a box): the old `Slab` drew only its top and bottom, so
        /// its exposed edge over the well was an open hole you could see into ("transparent plates").
        /// </summary>
        private static void Landing(Geo g, float x0, float x1, float z0, float z1, float yTop)
        {
            if (x1 - x0 < 0.02f || z1 - z0 < 0.02f) return;
            g.Box(0, new Vector3((x0 + x1) * 0.5f, yTop - StairPlate * 0.5f, (z0 + z1) * 0.5f),
                  new Vector3(x1 - x0, StairPlate, z1 - z0), Quaternion.identity, 0.5f);
        }

        /// <summary>
        private static void Roof(Geo g, Bldg b, float hw, float hd, float yTop)
        {
            // roof plate with a small overhang and a parapet
            g.Box(0, new Vector3(0f, yTop + 0.13f, 0f), new Vector3(b.w + 0.6f, 0.26f, b.d + 0.6f), Quaternion.identity, 0.4f);
            float y = yTop + 0.26f;
            g.Box(0, new Vector3(0f, y + 0.35f, hd - 0.15f), new Vector3(b.w, 0.7f, 0.3f), Quaternion.identity, 0.5f);
            g.Box(0, new Vector3(0f, y + 0.35f, -hd + 0.15f), new Vector3(b.w, 0.7f, 0.3f), Quaternion.identity, 0.5f);
            g.Box(0, new Vector3(hw - 0.15f, y + 0.35f, 0f), new Vector3(0.3f, 0.7f, b.d - 0.6f), Quaternion.identity, 0.5f);
            g.Box(0, new Vector3(-hw + 0.15f, y + 0.35f, 0f), new Vector3(0.3f, 0.7f, b.d - 0.6f), Quaternion.identity, 0.5f);
            if (rng.NextDouble() < 0.6 && !b.memorial && !b.penthouse && !b.heli)   // the draw is kept, so the seed layout does not shift
                g.Box(0, new Vector3(J(b.w * 0.25f), y + 1.0f, J(b.d * 0.2f)),
                      new Vector3(3.2f, 2.0f, 2.6f), Quaternion.identity, 0.5f);
        }

        // -- wall with openings -------------------------------------------------

        /// <summary>
        /// One facade: exterior and interior faces with real window openings and splayed reveals.
        /// Windows stay empty (no panes anywhere in the district). Holes are
        /// (xMin, yMin, xMax, yMax) in wall space, y measured in building space.
        ///
        /// v0.9.11: `cols` carries the **room grid** of this facade (see `Shell`/`LongSpans`/
        /// `EndSpans`): one span per room cell, one window centred in it. `null` keeps the old even
        /// pitch (`bay`), which is what the House of Soviets' podium uses. Returns the centre of the
        /// first window of the first storey above ground, in building-local space, so the physics
        /// probe can look through it.
        /// </summary>
        private static Vector3? Wall(Geo g, Vector3 o, Vector3 u, Vector3 n, float len,
                                     float y0, float y1, int floors, float fh,
                                     float winW, float winH, float sill, float splay,
                                     float bay, List<Vector4> doors, List<Vector2> cols = null,
                                     int skip = 1, float pad = 0f, bool windows = true, int fromFloor = 0,
                                     int mask = -1)
        {
            o -= u * (len * 0.5f);          // the caller passes the wall's midpoint
            var holes = new List<Vector4>(doors);
            Vector3? first = null;
            if (windows)                    // v0.9.52: a solid facade (the House towers' inner walls)
            for (int f = Mathf.Clamp(fromFloor, 0, floors); f < floors; f++)
            {
                if ((mask & (1 << Mathf.Clamp(f, 0, 30))) == 0) continue;   // v0.9.60: per-floor mask
                float fy = y0 + f * fh;
                float wa = fy + sill, wb = wa + winH;
                if (wb > y1 - 0.2f) continue;
                if (cols == null)
                {
                    int c0 = Mathf.Max(1, Mathf.RoundToInt(len / bay));
                    // v0.9.17: `skip = 2` drops every second window of the pitch (the House of
                    // Soviets' podium) and stretches the rest evenly over the same wall
                    if (skip > 1) c0 = Mathf.Max(1, (c0 + skip - 1) / skip);
                    float pitch = len / c0;
                    for (int c = 0; c < c0; c++)
                    {
                        float cx = pitch * (c + 0.5f);
                        var r = new Vector4(cx - winW * 0.5f, wa, cx + winW * 0.5f, wb);
                        if (Overlaps(r, doors)) continue;
                        holes.Add(r);
                        if (f == 1 && first == null) first = o + u * cx + Vector3.up * ((wa + wb) * 0.5f);
                    }
                    continue;
                }
                for (int c = 0; c < cols.Count; c += Mathf.Max(1, skip))
                {
                    float a = cols[c].x, b = cols[c].y;
                    float w = Mathf.Min(winW, b - a + 2f * pad - 0.5f);
                    if (w < 0.9f) continue;
                    float cx = (a + b) * 0.5f;
                    var r = new Vector4(cx - w * 0.5f, wa, cx + w * 0.5f, wb);
                    if (Overlaps(r, doors)) continue;
                    holes.Add(r);
                    if (f == 1 && first == null) first = o + u * cx + Vector3.up * ((wa + wb) * 0.5f);
                }
            }

            // v0.9.56: the wall's two faces are cut from **two** hole lists вЂ” the exterior with the
            // window's outer rect, the interior with the same rect **inset by `splay`**. That brings the
            // splayed window frames back (v0.9.55 had flattened every reveal, which made each opening
            // 0,56 m wider and left it unframed on the inside) while keeping it watertight: the reveals
            // connect the outer rect to the inset one on both faces, so the open ring through which
            // the sky used to shine is closed.
            int nDoor = doors.Count;
            var holesIn = new List<Vector4>(holes.Count);
            for (int i = 0; i < holes.Count; i++)
            {
                Vector4 r0 = holes[i];
                if (i < nDoor || splay <= 0.001f) { holesIn.Add(r0); continue; }
                var ri = new Vector4(r0.x + splay, r0.y + splay, r0.z - splay, r0.w - splay);
                if (ri.z - ri.x < 0.1f || ri.w - ri.y < 0.1f) ri = r0;
                holesIn.Add(ri);
            }

            int EmitFaces(List<Vector4> hs, Vector3 fo, Vector3 fn)
            {
                var xs = new List<float> { 0f, len };
                var ys = new List<float> { y0, y1 };
                foreach (var r in hs) { xs.Add(r.x); xs.Add(r.z); ys.Add(r.y); ys.Add(r.w); }
                xs.Sort(); ys.Sort();
                xs = Clean(xs); ys = Clean(ys);
                int cells = xs.Count - 1;
                for (int j = 0; j + 1 < ys.Count; j++)
                {
                    float ya = ys[j], yb = ys[j + 1];
                    if (yb - ya < 0.002f) continue;
                    int i = 0;
                    while (i < cells)
                    {
                        if (!CellSolid(xs[i], xs[i + 1], ya, yb, hs)) { i++; continue; }
                        int k = i;
                        while (k < cells && CellSolid(xs[k], xs[k + 1], ya, yb, hs)) k++;
                        float xa = xs[i], xb = xs[k];
                        g.Quad(0, fo + u * xa + Vector3.up * ya, fo + u * xb + Vector3.up * ya,
                                  fo + u * xb + Vector3.up * yb, fo + u * xa + Vector3.up * yb, fn, 0.25f);
                        i = k;
                    }
                }
                return cells;
            }
            int cells = EmitFaces(holes, o, n)                     // exterior face
                      + EmitFaces(holesIn, o - n * T, -n);         // interior face, one thickness behind

            // reveals: they close the wall's thickness from the outer rect to the **inner** one
            for (int hi = 0; hi < holes.Count; hi++)
            {
                Vector4 r = holes[hi], ri = holesIn[hi];
                Vector3 oi = o - n * T;
                Vector3 e1 = o + u * r.x + Vector3.up * r.y;
                Vector3 e2 = o + u * r.x + Vector3.up * r.w;
                Vector3 e3 = o + u * r.z + Vector3.up * r.w;
                Vector3 e4 = o + u * r.z + Vector3.up * r.y;
                Vector3 i1 = oi + u * ri.x + Vector3.up * ri.y;
                Vector3 i2 = oi + u * ri.x + Vector3.up * ri.w;
                Vector3 i3 = oi + u * ri.z + Vector3.up * ri.w;
                Vector3 i4 = oi + u * ri.z + Vector3.up * ri.y;
                g.Quad(0, e1, e2, i2, i1, u, 0.5f);              // left jamb
                g.Quad(0, e4, e3, i3, i4, -u, 0.5f);             // right jamb
                g.Quad(0, e2, e3, i3, i2, Vector3.down, 0.5f);   // head
                g.Quad(0, e1, e4, i4, i1, Vector3.up, 0.5f);     // sill
                // v0.9.64: the **same four faces, reversed**. A single-sided reveal disappears when you
                // look into an opening from the far side (the jamb's normal points into the opening), and
                // that is the "прозрачная текстура" at windows seen at an angle / at the corners. Two
                // coplanar faces with opposite normals cannot z-fight вЂ” culling picks one.
                g.Quad(0, e2, e1, i1, i2, -u, 0.5f);
                g.Quad(0, e3, e4, i4, i3, u, 0.5f);
                g.Quad(0, e3, e2, i2, i3, Vector3.up, 0.5f);
                g.Quad(0, e4, e1, i1, i4, Vector3.down, 0.5f);
                // v0.9.64: and the wall's **end faces**, so a wall is a closed box and its hollow corner
                // cannot be looked into from outside
                g.Quad(0, o + Vector3.up * y0, o + Vector3.up * y1,
                          o - n * T + Vector3.up * y1, o - n * T + Vector3.up * y0, -u, 0.5f);
                g.Quad(0, o + u * len + Vector3.up * y0, o + u * len + Vector3.up * y1,
                          o + u * len - n * T + Vector3.up * y1, o + u * len - n * T + Vector3.up * y0, u, 0.5f);
            }
            // no glass anywhere: every opening stays empty
            // v0.9.57: the wall is a 0,94 m thick **skin** of two faces вЂ” nothing closed its top or
            // bottom, so from above it was hollow: at the podium's roof edge (the roof ring stopped
            // 0,94 m short of the facade) you looked straight down into the cavity, i.e. through the
            // wall вЂ” the "transparent facade in the horizontal plane" in the user's screenshot. These
            // two caps make every wall watertight; under a roof plate / floor slab they are hidden
            // (they sit 5 mm inside the wall so they can never be coplanar with those slabs)
            g.Quad(0, o + Vector3.up * (y0 + 0.005f), o + u * len + Vector3.up * (y0 + 0.005f),
                      o + u * len - n * T + Vector3.up * (y0 + 0.005f), o - n * T + Vector3.up * (y0 + 0.005f),
                      Vector3.down, 0.5f);
            g.Quad(0, o + Vector3.up * (y1 - 0.005f), o + u * len + Vector3.up * (y1 - 0.005f),
                      o + u * len - n * T + Vector3.up * (y1 - 0.005f), o - n * T + Vector3.up * (y1 - 0.005f),
                      Vector3.up, 0.5f);
            quadCount += holes.Count * 4 + cells + 2;
            return first;
        }

        private static List<float> Clean(List<float> l)
        {
            var r = new List<float>(l.Count);
            for (int i = 0; i < l.Count; i++)
                if (r.Count == 0 || l[i] - r[r.Count - 1] > 0.002f) r.Add(l[i]);
            return r;
        }

        private static bool Overlaps(Vector4 a, List<Vector4> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var b = list[i];
                if (a.x < b.z - 0.01f && a.z > b.x + 0.01f && a.y < b.w - 0.01f && a.w > b.y + 0.01f) return true;
            }
            return false;
        }

        private static bool CellSolid(float x0, float x1, float y0, float y1, List<Vector4> holes)
        {
            float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f;
            for (int i = 0; i < holes.Count; i++)
            {
                var h = holes[i];
                if (cx > h.x && cx < h.z && cy > h.y && cy < h.w) return false;
            }
            return true;
        }

        private static void AddDoor(List<Vector4> list, float len, float y, float w, float h)
            => list.Add(new Vector4(len - w * 0.5f, y, len + w * 0.5f, y + h));

        /// <summary>Two faces only (no sides) вЂ” for slab pieces whose edges are closed by something else.</summary>
        private static void RectFaces(Geo g, float x0, float x1, float z0, float z1, float yb, float yt)
        {
            if (x1 - x0 < 0.002f || z1 - z0 < 0.002f) return;
            g.Quad(0, new Vector3(x0, yt, z0), new Vector3(x1, yt, z0), new Vector3(x1, yt, z1), new Vector3(x0, yt, z1), Vector3.up, 0.2f);
            g.Quad(0, new Vector3(x0, yb, z0), new Vector3(x1, yb, z0), new Vector3(x1, yb, z1), new Vector3(x0, yb, z1), Vector3.down, 0.2f);
        }

        /// <summary>Slab with an optional rectangular hole: top and bottom faces plus the hole's sides.</summary>
        private static void Slab(Geo g, float x0, float x1, float z0, float z1, float yb, float yt, Vector4? hole)
        {
            if (hole == null)
            {
                Rect2(g, x0, x1, z0, z1, yb, yt);
                return;
            }
            var h = hole.Value;
            // v0.9.63: two-face pieces here вЂ” Rect2 is a closed box now and would z-fight with the rim
            RectFaces(g, x0, h.x, z0, z1, yb, yt);
            RectFaces(g, h.z, x1, z0, z1, yb, yt);
            RectFaces(g, h.x, h.z, z0, h.y, yb, yt);
            RectFaces(g, h.x, h.z, h.w, z1, yb, yt);
            // v0.9.42: the hole's rim must face **into the hole** (that is the side you see it from вЂ”
            // standing in the stairwell). The two z-edges had their normals the other way round, so the
            // floor plate's end face over the shaft was culled: you looked straight through the slab's
            // thickness into the storey below ("невидимый торец").
            g.Quad(0, new Vector3(h.x, yb, h.y), new Vector3(h.x, yb, h.w), new Vector3(h.x, yt, h.w), new Vector3(h.x, yt, h.y), Vector3.right, 0.5f);
            g.Quad(0, new Vector3(h.z, yb, h.y), new Vector3(h.z, yb, h.w), new Vector3(h.z, yt, h.w), new Vector3(h.z, yt, h.y), Vector3.left, 0.5f);
            g.Quad(0, new Vector3(h.x, yb, h.y), new Vector3(h.z, yb, h.y), new Vector3(h.z, yt, h.y), new Vector3(h.x, yt, h.y), Vector3.forward, 0.5f);
            g.Quad(0, new Vector3(h.x, yb, h.w), new Vector3(h.z, yb, h.w), new Vector3(h.z, yt, h.w), new Vector3(h.x, yt, h.w), Vector3.back, 0.5f);
        }

        private static void Rect2(Geo g, float x0, float x1, float z0, float z1, float yb, float yt)
        {
            if (x1 - x0 < 0.002f || z1 - z0 < 0.002f) return;
            // v0.9.63: a **closed box**, not two faces. The two-face version left every slab's side edge
            // open, so at exterior corners and along the roof edge you looked *through* the slab (the
            // user's "прозрачные текстуры" on vertical and horizontal outside corners).
            g.Box(0, new Vector3((x0 + x1) * 0.5f, (yb + yt) * 0.5f, (z0 + z1) * 0.5f),
                  new Vector3(x1 - x0, yt - yb, z1 - z0), Quaternion.identity, 0.2f);
        }

        private static void Piece(Geo g, Vector3 o, Vector3 u, Vector3 n, float s0, float s1, float y0, float y1)
        {
            if (s1 - s0 < 0.01f || y1 - y0 < 0.01f) return;
            Vector3 a = o + u * s0 + n * (PT * 0.5f);
            Vector3 b = o + u * s1 + n * (PT * 0.5f);
            g.Quad(0, a + Vector3.up * y0, b + Vector3.up * y0, b + Vector3.up * y1, a + Vector3.up * y1, n, 0.5f);
            Vector3 c = o + u * s0 - n * (PT * 0.5f);
            Vector3 d = o + u * s1 - n * (PT * 0.5f);
            g.Quad(0, c + Vector3.up * y0, d + Vector3.up * y0, d + Vector3.up * y1, c + Vector3.up * y1, -n, 0.5f);
            // the cut faces of a doorway are visible: close both ends
            g.Quad(0, a + Vector3.up * y0, c + Vector3.up * y0, c + Vector3.up * y1, a + Vector3.up * y1, -u, 0.5f);
            g.Quad(0, b + Vector3.up * y0, d + Vector3.up * y0, d + Vector3.up * y1, b + Vector3.up * y1, u, 0.5f);
            // v0.9.40: **the head and the sill**. Without them every lintel was an open slot through the
            // wall's thickness вЂ” looking up at any doorway you saw the ceiling of the room beyond as a
            // thin dark strip, i.e. exactly the "прозрачная текстура" the user kept photographing.
            g.Quad(0, a + Vector3.up * y1, b + Vector3.up * y1, d + Vector3.up * y1, c + Vector3.up * y1,
                   Vector3.up, 0.5f);
            g.Quad(0, a + Vector3.up * y0, b + Vector3.up * y0, d + Vector3.up * y0, c + Vector3.up * y0,
                   Vector3.down, 0.5f);
        }

        /// <summary>Outdoor entrance steps.</summary>
        private static void Steps(Geo g, Vector3 o, Vector3 u, Vector3 n, float width, float rise, int count)
        {
            float h = rise / count;
            for (int i = 0; i < count; i++)
            {
                float top = h * (i + 1);
                g.Box(0, o + n * (0.45f - i * 0.32f) + Vector3.up * (top * 0.5f),
                      new Vector3(width, top, 0.34f), Quaternion.LookRotation(n, Vector3.up), 0.6f);
            }
        }

        /// <summary>
        /// Balconies on the window columns of the +z facade. v0.9.12: they form **vertical stacks** on
        /// every other room bay (`c = 1, 3, 5 вЂ¦`) and sit exactly on the window column, with a real
        /// balcony door cut in the wall behind them (see `AddDoor` in `BuildBuilding`). Before, the
        /// column was chosen by `(c + f) % 3`, i.e. a diagonal scatter, and there was no door at all вЂ”
        /// which is what the user saw as "balconies in the wrong places".
        /// </summary>
        private static void Balconies(Geo g, Bldg b, float hd, float y0, List<Vector2> cols)
        {
            const float proj = 1.15f;      // how far the slab projects
            float top = y0 + b.floors * b.fh;
            int stacks = 0, made = 0;
            for (int c = 1; c < cols.Count; c += 2)
            {
                float span = cols[c].y - cols[c].x;
                float w = Mathf.Clamp(span + 0.55f, 2.0f, 2.9f);
                // `cols` are *wall* coordinates (0 = the wall's left end), the boxes are built in
                // building-local space вЂ” v0.9.12 fixes the half-facade shift this caused
                float x = (cols[c].x + cols[c].y) * 0.5f - b.w * 0.5f;
                if (Mathf.Abs(x) + w * 0.5f > b.w * 0.5f - 0.2f) continue;   // never past the corner
                stacks++;
                for (int f = 1; f < b.floors; f++)
                {
                    float y = y0 + f * b.fh;                    // slab top flush with the floor
                    if (y + 2.4f > top) continue;
                    made++;
                    if (!b.hasBalc) { b.hasBalc = true; b.balcSample = new Vector3(x, y + 0.5f, hd + proj * 0.5f); }
                    g.Box(0, new Vector3(x, y - 0.09f, hd + proj * 0.5f),
                          new Vector3(w, 0.18f, proj), Quaternion.identity, 0.6f);
                    // parapet: front rail plus two cheeks, 1.0 m above the slab
                    g.Box(0, new Vector3(x, y + 0.5f, hd + proj - 0.06f),
                          new Vector3(w, 1.0f, 0.12f), Quaternion.identity, 0.8f);
                    g.Box(0, new Vector3(x - w * 0.5f + 0.06f, y + 0.5f, hd + proj * 0.5f),
                          new Vector3(0.12f, 1.0f, proj), Quaternion.identity, 0.8f);
                    g.Box(0, new Vector3(x + w * 0.5f - 0.06f, y + 0.5f, hd + proj * 0.5f),
                          new Vector3(0.12f, 1.0f, proj), Quaternion.identity, 0.8f);
                }
            }
            log.LogInfo($"[BALC] {b.w:0.0}x{b.d:0.0} m block at {b.p.x:0.0},{b.p.z:0.0}: " +
                        $"{stacks} stacks over {cols.Count} window bays, {made} balconies, {b.floors} floors");
        }

        /// <summary>
        /// Stalinka: pilasters on the mullions of both long facades. v0.9.11 replaced the old
        /// 1.1 m deep projecting central bay here вЂ” it stood in front of the facade's middle windows
        /// and its "own windows" were quads on submesh 1, which this mesh no longer has, so all it did
        /// was block three windows per storey with a blank box.
        /// </summary>
        private static void Pilasters(Geo g, Bldg b, Shell sh, float hd, float y0, float yTop)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                float z = s * (hd + 0.06f);
                for (int k = 1; k < sh.seg; k++)
                {
                    float x = sh.x0 + k * sh.segLen;
                    g.Box(0, new Vector3(x, (y0 + yTop) * 0.5f, z),
                          new Vector3(0.55f, yTop - y0, 0.28f), Quaternion.identity, 0.3f);
                }
            }
        }

        // ------------------------------------------------------------------ covered bridges

        private static void PlanBridges()
        {
            int max = Mathf.Clamp(Mathf.RoundToInt(40 * Density), 8, 44);
            var rot = new List<Quaternion>(bldgs.Count);
            for (int i = 0; i < bldgs.Count; i++) rot.Add(Quaternion.Euler(0f, bldgs[i].yaw, 0f));
            for (int i = 0; i < bldgs.Count; i++)
            {
                for (int j = 0; j < bldgs.Count; j++)
                {
                    if (i == j || bridgeCount >= max) continue;
                    var A = bldgs[i];
                    var B = bldgs[j];
                    if (Mathf.Abs(A.baseY - B.baseY) > 0.6f) continue;   // v0.9.9: different terraces
                    Vector3 dir = B.p - A.p;
                    dir.y = 0f;
                    float dist = dir.magnitude;
                    if (dist < 10f || dist > 70f) continue;
                    dir /= dist;
                    // the corridor end of A must point at B, and B's +x must point away from A
                    if (Vector3.Dot(rot[i] * Vector3.right, dir) < 0.6f) continue;
                    if (Vector3.Dot(rot[j] * Vector3.right, dir) < 0.6f) continue;

                    Vector3 ea = A.p + rot[i] * new Vector3(A.w * 0.5f, 0f, 0f);
                    Vector3 eb = B.p + rot[j] * new Vector3(-B.w * 0.5f, 0f, 0f);
                    float span = Vector3.Distance(ea, eb);
                    if (span < 3f || span > 34f) continue;

                    // v0.9.3: every bridge crosses at the 11th storey, so the whole ring shares one level.
                    // v0.9.11: a stalinka's storey is 5.6 m instead of 3.2 m, so the two sides of a
                    // bridge look for a pair of floors that lands on the *same* height (4 x 5.6 =
                    // 7 x 3.2 = 22.4 m); the pair closest to the old 32.15 m deck wins.
                    int fa = -1, fb = -1;
                    float y = 0f, best = float.MaxValue;
                    for (int a2 = 2; a2 < Mathf.Min(A.floors - 1, 44); a2++)
                    {
                        float target = A.baseY + a2 * A.fh;
                        int b2 = Mathf.RoundToInt((target - B.baseY) / B.fh);
                        if (b2 < 2 || b2 >= Mathf.Min(B.floors - 1, 44)) continue;
                        float err = Mathf.Abs(B.baseY + b2 * B.fh - target);
                        if (err > 0.02f) continue;
                        float score = Mathf.Abs(target - (A.baseY + BridgeFloor * FH)) + err;
                        if (score < best) { best = score; fa = a2; fb = b2; y = target; }
                    }
                    if (fa < 0) continue;

                    if (A.endPlus == null) A.endPlus = new bool[A.floors];
                    if (B.endMinus == null) B.endMinus = new bool[B.floors];
                    if (A.endPlus[fa] || B.endMinus[fb]) continue;
                    A.endPlus[fa] = true;
                    B.endMinus[fb] = true;

                    bridges.Add(new BridgeSpec { a = ea, b = eb, y = y, ia = i, ib = j, floor = fa, fa = fa, fb = fb });
                    bridgeCount++;
                }
            }
            // rebuild the two door flags we just wrote (Bldg is a class, so they are shared)
        }

        private static void BuildBridge(BridgeSpec s)
        {
            Vector3 d = s.b - s.a;
            float len = d.magnitude;
            if (len < 1f) return;
            Vector3 dir = d / len;

            var go = new GameObject("bridge");
            go.transform.SetParent(root, false);
            go.transform.localPosition = s.a;
            go.transform.localRotation = Quaternion.LookRotation(dir, Vector3.up);

            var g = new Geo();
            float zc = len * 0.5f + 0.2f;
            len += 0.4f;
            // deck, roof, solid wall on the north side, parapet on the south side
            g.Box(0, new Vector3(0f, s.y - 0.15f, zc), new Vector3(2.7f, 0.3f, len), Quaternion.identity, 0.4f);
            g.Box(0, new Vector3(0f, s.y + 2.95f, zc), new Vector3(2.95f, 0.35f, len + 0.3f), Quaternion.identity, 0.4f);
            g.Box(0, new Vector3(1.35f, s.y + 1.45f, zc), new Vector3(0.26f, 2.9f, len), Quaternion.identity, 0.5f);
            g.Box(0, new Vector3(-1.35f, s.y + 0.55f, zc), new Vector3(0.26f, 1.1f, len), Quaternion.identity, 0.5f);
            // a beam under the roof so the span reads as a covered gallery
            for (float t = 1.5f; t < len - 1f; t += 2.4f)
                g.Box(0, new Vector3(0f, s.y + 2.72f, t), new Vector3(2.7f, 0.22f, 0.22f), Quaternion.identity, 0.6f);
            g.Apply(go, mRough);
            AddMeshCollider(go, "bridge");
        }
    }
}
