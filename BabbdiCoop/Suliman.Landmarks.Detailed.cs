using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace BabbdiCoop
{
    // v0.10.63 — the eight reference objects rebuilt at 2x footprint with a much denser
    // structural kit: tiers, rings, stairs, catwalks, pipes, rubble and interior shells.
    internal static partial class Suliman
    {
        private static Quaternion Q(float yaw) => Quaternion.Euler(0f, yaw, 0f);

        private static void RB(SGeo g, Vector3 c, Vector3 s, float yaw = 0f)
            => g.Box(c, s, Q(yaw));

        private static void Rail(SGeo g, Vector3 a, Vector3 b, float h, int posts)
        {
            Vector3 aa = a + Vector3.up * h, bb = b + Vector3.up * h;
            g.Beam(aa, bb, 0.07f, 0.07f, Vector3.up);
            for (int i = 0; i <= posts; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, i / (float)Mathf.Max(1, posts));
                g.Box(new Vector3(p.x, p.y + h * 0.5f, p.z), new Vector3(0.09f, h, 0.09f), Quaternion.identity);
            }
        }

        private static void Steps(SGeo g, SGeo col, Vector3 start, Vector3 dir, float width,
                                  int n, float rise, float run)
        {
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = start + dir * (i * run);
                var r = Quaternion.LookRotation(dir, Vector3.up);
                g.Box(new Vector3(p.x, p.y - i * rise, p.z), new Vector3(width, 0.24f, run * 1.08f), r);
                if (col != null)
                    col.Box(new Vector3(p.x, p.y - i * rise - 0.3f, p.z), new Vector3(width, 0.8f, run * 1.08f), r);
            }
        }

        private static void Ring(SGeo g, Vector3 c, Vector2 plane, float r0, float r1, int seg, float depth)
        {
            // plane.x = horizontal axis, plane.y = vertical axis, ring lies in that plane
            for (int i = 0; i < seg; i++)
            {
                float a0 = Mathf.PI * 2f * i / seg;
                float a1 = Mathf.PI * 2f * (i + 1) / seg;
                float am = (a0 + a1) * 0.5f;
                float arc = (r0 + r1) * 0.5f * Mathf.PI * 2f / seg * 1.08f;
                Vector3 p = c + new Vector3(Mathf.Cos(am) * (r0 + r1) * 0.5f * plane.x,
                                            Mathf.Sin(am) * (r0 + r1) * 0.5f * plane.y, 0f);
                g.Box(p, new Vector3(arc, (r1 - r0) * 0.9f, depth),
                      Quaternion.Euler(0f, 0f, am * Mathf.Rad2Deg));
            }
        }

        private static void Rubble(SGeo g, SGeo col, Vector3 c, float radius, int n, System.Random sr, float scale = 1f)
        {
            for (int i = 0; i < n; i++)
            {
                float a = (float)sr.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Sqrt((float)sr.NextDouble()) * radius;
                float s = (0.4f + (float)sr.NextDouble() * 2.4f) * scale;
                Vector3 p = c + new Vector3(Mathf.Cos(a) * r, s * 0.28f, Mathf.Sin(a) * r);
                var q = Quaternion.Euler(sr.Next(-25, 25), sr.Next(0, 90), sr.Next(-25, 25));
                g.Box(p, new Vector3(s, s * 0.6f, s * 0.8f), q);
                if (col != null && i % 3 == 0)
                    col.Box(p, new Vector3(s, s * 0.6f, s * 0.8f), q);
            }
        }

        // ------------------------------------------------------------------ 1 stadium

        private static void StadiumDetail()
        {
            Vector3 c0 = OnGround(900f, 580f);
            float gy = c0.y;
            var conc = new SGeo { UvScale = 0.25f };
            var snow = new SGeo { UvScale = 0.25f };
            var ice = new SGeo { UvScale = 0.1f };
            var dark = new SGeo { UvScale = 1f };
            var metal = new SGeo { UvScale = 0.4f };
            var glow = new SGeo { UvScale = 1f };
            var col = new SGeo { UvScale = 1f };

            const float rIn = 24f;
            // ice arena
            Vector3 ic = new Vector3(c0.x, gy + 0.10f, c0.z);
            for (int i = 0; i < 48; i++)
            {
                float a0 = Mathf.PI * 2f * i / 48f, a1 = Mathf.PI * 2f * (i + 1) / 48f;
                Vector3 p0 = ic + new Vector3(Mathf.Cos(a0) * rIn, 0f, Mathf.Sin(a0) * rIn);
                Vector3 p1 = ic + new Vector3(Mathf.Cos(a1) * rIn, 0f, Mathf.Sin(a1) * rIn);
                ice.Tri(ic, p0, p1, Vector3.up,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(Mathf.Cos(a0) * 0.5f + 0.5f, Mathf.Sin(a0) * 0.5f + 0.5f),
                    new Vector2(Mathf.Cos(a1) * 0.5f + 0.5f, Mathf.Sin(a1) * 0.5f + 0.5f));
            }
            // hockey markings: blue lines, faceoff circles and a centre dot
            dark.Box(ic + Vector3.up * 0.03f, new Vector3(44f, 0.02f, 0.3f), Quaternion.identity);
            dark.Box(ic + new Vector3(0f, 0.03f, 0f), new Vector3(0.3f, 0.02f, 44f), Quaternion.identity);
            for (int k = -1; k <= 1; k += 2)
                for (int i = 0; i < 24; i++)
                {
                    float a0 = Mathf.PI * 2f * i / 24f;
                    Vector3 p = ic + new Vector3(k * 11f + Mathf.Cos(a0) * 5f, 0.035f,
                                                Mathf.Sin(a0) * 5f);
                    dark.Box(p, new Vector3(2f, 0.015f, 0.12f), Q(-a0 * Mathf.Rad2Deg));
                }

            // eight tiers with concrete seat rows and parapet
            const int tiers = 9;
            const int seg = 56;
            for (int t = 0; t < tiers; t++)
            {
                float rad = rIn + 2.2f + t * 1.9f;
                float yy = gy + 0.45f + t * 0.52f;
                for (int i = 0; i < seg; i++)
                {
                    float a0 = Mathf.PI * 2f * i / seg;
                    // four radial aisles
                    float aa = a0 * Mathf.Rad2Deg;
                    if (Mathf.Abs(Mathf.DeltaAngle(aa, 45f)) < 5.6f ||
                        Mathf.Abs(Mathf.DeltaAngle(aa, 135f)) < 5.6f ||
                        Mathf.Abs(Mathf.DeltaAngle(aa, 225f)) < 5.6f ||
                        Mathf.Abs(Mathf.DeltaAngle(aa, 315f)) < 5.6f) continue;
                    float am = a0 + Mathf.PI / seg;
                    Vector3 p = c0 + new Vector3(Mathf.Cos(am) * (rad + 0.95f), 0f, Mathf.Sin(am) * (rad + 0.95f));
                    var r = Q(-am * Mathf.Rad2Deg + 90f);
                    conc.Box(new Vector3(p.x, yy, p.z), new Vector3(4.2f, 0.54f, 2.0f), r);
                    snow.Box(new Vector3(p.x, yy + 0.28f, p.z), new Vector3(4.15f, 0.06f, 1.95f), r);
                    // three seat blocks on each bay
                    for (int s = -1; s <= 1; s++)
                        conc.Box(new Vector3(p.x + Mathf.Cos(am + Mathf.PI / 2f) * s * 0.62f,
                                             yy + 0.38f,
                                             p.z + Mathf.Sin(am + Mathf.PI / 2f) * s * 0.62f),
                                 new Vector3(0.55f, 0.20f, 0.42f), r);
                    col.Box(new Vector3(p.x, yy - 2f, p.z), new Vector3(4.2f, 4f, 2.0f), r);
                }
            }
            // outer wall, buttresses and entrance portals
            int os = 52;
            for (int i = 0; i < os; i++)
            {
                float a0 = Mathf.PI * 2f * i / os;
                float am = a0 + Mathf.PI / os;
                bool entrance = Mathf.Abs(Mathf.DeltaAngle(am * Mathf.Rad2Deg, 0f)) < 6f ||
                                Mathf.Abs(Mathf.DeltaAngle(am * Mathf.Rad2Deg, 180f)) < 6f;
                if (entrance) continue;
                float rad = rIn + 2.2f + tiers * 1.9f + 1.2f;
                Vector3 p = c0 + new Vector3(Mathf.Cos(am) * rad, 0f, Mathf.Sin(am) * rad);
                var r = Q(-am * Mathf.Rad2Deg + 90f);
                conc.Box(new Vector3(p.x, gy + 3.6f, p.z), new Vector3(4.2f, 6.9f, 0.7f), r);
                snow.Box(new Vector3(p.x, gy + 7.1f, p.z), new Vector3(4.25f, 0.10f, 0.8f), r);
                col.Box(new Vector3(p.x, gy + 2.0f, p.z), new Vector3(4.2f, 6.0f, 0.7f), r);
                if (i % 4 == 0)
                {
                    Vector3 b = c0 + new Vector3(Mathf.Cos(am) * (rad + 1.4f), 0f, Mathf.Sin(am) * (rad + 1.4f));
                    conc.Box(new Vector3(b.x, gy + 2.8f, b.z), new Vector3(1.1f, 5.6f, 3.0f), r);
                    col.Box(new Vector3(b.x, gy + 2.8f, b.z), new Vector3(1.1f, 5.6f, 3.0f), r);
                }
            }
            // two monumental entrances
            for (int e = 0; e < 2; e++)
            {
                float ang = e == 0 ? 0f : 180f;
                Vector3 n = new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad), 0f, Mathf.Cos(ang * Mathf.Deg2Rad));
                Vector3 baseP = c0 + n * (rIn + 2.2f + tiers * 1.9f + 2.4f);
                var r = Q(ang);
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 p = baseP + new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad), 0f, -Mathf.Sin(ang * Mathf.Deg2Rad)) * (s * 5.4f);
                    conc.Box(new Vector3(p.x, gy + 4.0f, p.z), new Vector3(1.8f, 8f, 3.6f), r);
                    col.Box(new Vector3(p.x, gy + 4f, p.z), new Vector3(1.8f, 8f, 3.6f), r);
                }
                conc.Box(new Vector3(baseP.x, gy + 8.1f, baseP.z), new Vector3(13f, 1.4f, 4.4f), r);
                snow.Box(new Vector3(baseP.x, gy + 8.85f, baseP.z), new Vector3(13.2f, 0.12f, 4.6f), r);
                AddLampSpot(baseP + Vector3.up * 5.5f, 18f);
            }
            // radial stairs in the four aisles — they climb outward with the tiers
            for (int a = 0; a < 4; a++)
            {
                float ang = 45f + a * 90f;
                Vector3 dir = new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad), 0f, Mathf.Sin(ang * Mathf.Deg2Rad));
                Vector3 tangent = new Vector3(-dir.z, 0f, dir.x);
                var r = Quaternion.LookRotation(dir, Vector3.up);
                for (int t = 0; t < tiers; t++)
                {
                    Vector3 p = c0 + dir * (rIn + 2.2f + t * 1.9f + 0.95f);
                    float yy = gy + 0.45f + t * 0.52f;
                    conc.Box(new Vector3(p.x, yy, p.z), new Vector3(4.2f, 0.54f, 2.0f), r);
                    snow.Box(new Vector3(p.x, yy + 0.28f, p.z), new Vector3(4.15f, 0.06f, 1.95f), r);
                    col.Box(new Vector3(p.x, yy - 2f, p.z), new Vector3(4.2f, 4f, 2.0f), r);
                }
                float r0 = rIn + 2.2f, r1 = rIn + 2.2f + tiers * 1.9f;
                Vector3 b0 = c0 + dir * r0; b0.y = gy + 0.45f + 0.27f + 0.95f;
                Vector3 b1 = c0 + dir * r1; b1.y = gy + 0.45f + (tiers - 1) * 0.52f + 0.27f + 0.95f;
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 off = tangent * (2.0f * s);
                    metal.Beam(b0 + off, b1 + off, 0.06f, 0.06f, Vector3.up);
                    for (int t = 0; t <= tiers; t += 2)
                    {
                        Vector3 p = c0 + dir * (rIn + 2.2f + t * 1.9f) + off;
                        p.y = gy + 0.45f + Mathf.Min(t, tiers - 1) * 0.52f + 0.27f;
                        metal.Box(new Vector3(p.x, p.y + 0.48f, p.z), new Vector3(0.08f, 0.96f, 0.08f), Quaternion.identity);
                    }
                }
            }
            // six floodlight masts
            for (int i = 0; i < 6; i++)
            {
                float ang = i * 60f;
                Vector3 p = c0 + new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad), 0f, Mathf.Sin(ang * Mathf.Deg2Rad)) * 48f;
                p.y = HeightAt(p.x, p.z);
                metal.Cylinder(p, 0.28f, 18f, 6, null);
                Vector3 head = p + Vector3.up * 18.2f;
                metal.Box(head, new Vector3(3.6f, 0.3f, 0.6f), Q(-ang));
                for (int k = -1; k <= 1; k++)
                    glow.Box(head + new Vector3(0f, -0.18f, k * 0.42f), new Vector3(3.2f, 0.07f, 0.28f), Q(-ang));
                AddLampSpot(head + Vector3.down * 0.4f, 42f);
            }
            // scoreboard / stage at the north side
            Vector3 sb = c0 + new Vector3(0f, 0f, -31f);
            conc.Box(new Vector3(sb.x, gy + 5.0f, sb.z), new Vector3(20f, 8f, 1.4f), Quaternion.identity);
            snow.Box(new Vector3(sb.x, gy + 9.1f, sb.z), new Vector3(20.2f, 0.12f, 1.6f), Quaternion.identity);
            for (int s = -1; s <= 1; s += 2)
                conc.Box(new Vector3(sb.x + s * 7.5f, gy + 2.2f, sb.z), new Vector3(1.4f, 4.4f, 2.4f), Quaternion.identity);
            dark.Box(new Vector3(sb.x, gy + 5.4f, sb.z - 0.78f), new Vector3(17f, 4.8f, 0.12f), Quaternion.identity);
            // service blockhouses around the ring
            for (int i = 0; i < 4; i++)
            {
                float ang = 22f + i * 90f;
                Vector3 p = c0 + new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad), 0f, Mathf.Sin(ang * Mathf.Deg2Rad)) * 37f;
                p.y = HeightAt(p.x, p.z);
                conc.Box(p + Vector3.up * 2.0f, new Vector3(10f, 4f, 7f), Q(-ang));
                snow.Box(p + Vector3.up * 4.05f, new Vector3(10.2f, 0.1f, 7.2f), Q(-ang));
                col.Box(p + Vector3.up * 2.0f, new Vector3(10f, 4f, 7f), Q(-ang));
                glow.Box(p + new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad) * 5.1f, 2.2f,
                                          -Mathf.Sin(ang * Mathf.Deg2Rad) * 5.1f),
                         new Vector3(0.08f, 1.1f, 1.8f), Q(-ang));
                AddLampSpot(p + Vector3.up * 2f, 12f);
            }
            // snow drifts at the outer ring
            for (int i = 0; i < 28; i++)
            {
                float a = i * Mathf.PI * 2f / 28f;
                Vector3 p = c0 + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 54f;
                p.y = HeightAt(p.x, p.z);
                snow.Box(p + Vector3.up * 0.2f, new Vector3(8f, 0.5f, 2.5f), Q(-a * Mathf.Rad2Deg));
            }

            conc.Emit(Root, "stadium2_concrete", ConcreteMat, true);
            snow.Emit(Root, "stadium2_snow", SnowBankMat);
            ice.Emit(Root, "stadium2_ice", IceMat, true, ShadowCastingMode.Off);
            dark.Emit(Root, "stadium2_dark", GlassDarkMat, false, ShadowCastingMode.Off);
            metal.Emit(Root, "stadium2_metal", MetalMat, true);
            glow.Emit(Root, "stadium2_glow", GlowWarmMat, false, ShadowCastingMode.Off);
            col.EmitCollider(Root, "stadium2_col");
            Log("[SULIMAN] detailed landmark: stadium amphitheatre (r=56 m, 9 tiers)");
        }

        // ------------------------------------------------------------------ 2 ruins

        private static void RuinedTowersDetail()
        {
            Vector3 c = OnGround(630f, 400f);
            var conc = new SGeo { UvScale = 0.22f };
            var snow = new SGeo { UvScale = 0.2f };
            var rust = new SGeo { UvScale = 0.3f };
            var glass = new SGeo { UvScale = 1f };
            var col = new SGeo { UvScale = 1f };
            var sr = new System.Random(4411);

            RuinTowerDetail(conc, snow, rust, glass, col, c + new Vector3(-17f, 0f, 0f), 30f, 26f, 46f, 13);
            RuinTowerDetail(conc, snow, rust, glass, col, c + new Vector3(18f, 0f, 7f), 24f, 22f, 34f, 8);
            RuinTowerDetail(conc, snow, rust, glass, col, c + new Vector3(0f, 0f, -24f), 17f, 18f, 24f, 5);

            // broken skybridge between the two big towers
            for (int s = 0; s < 2; s++)
            {
                float z = s == 0 ? c.z + 1f : c.z + 5f;
                conc.Box(new Vector3(c.x, c.y + 26f, z), new Vector3(26f, 0.7f, 4f), Quaternion.identity);
                snow.Box(new Vector3(c.x, c.y + 26.4f, z), new Vector3(25f, 0.08f, 3.8f), Quaternion.identity);
                col.Box(new Vector3(c.x, c.y + 26f, z), new Vector3(26f, 0.7f, 4f), Quaternion.identity);
            }
            for (int i = 0; i < 7; i++)
                rust.Beam(new Vector3(c.x - 12f + i * 4f, c.y + 20f, c.z + 3f),
                          new Vector3(c.x - 12f + i * 4f, c.y + 26f, c.z + 3f), 0.12f, 0.12f, Vector3.forward);

            // two collapsed silos
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 p = c + new Vector3(s * 40f, 0f, 30f);
                p.y = HeightAt(p.x, p.z);
                rust.Cylinder(p, 7f, 19f, 14, null);
                col.Cylinder(p, 7f, 19f, 14, null);
                col.Disc(p + Vector3.up * 19f, 7f, 14);
                rust.Disc(p + Vector3.up * 19f, 7f, 14);
                rust.Cylinder(p + Vector3.up * 19f, 5.5f, 1.4f, 14, null);
                rust.Disc(p + Vector3.up * 20.4f, 5.5f, 14);
                snow.Disc(p + Vector3.up * 19.06f, 6.9f, 14);
                snow.Disc(p + Vector3.up * 20.46f, 5.4f, 14);
                for (int i = 0; i < 5; i++)
                    rust.Beam(p + new Vector3(0f, 2f + i * 3.6f, 0f),
                              p + new Vector3(Mathf.Cos(i * 1.3f) * 12f, 3f + i * 3.6f, Mathf.Sin(i * 1.3f) * 12f),
                              0.08f, 0.08f, Vector3.forward);
            }
            // gantry crane skeleton
            Vector3 g0 = c + new Vector3(-30f, 0f, 32f);
            g0.y = HeightAt(g0.x, g0.z);
            for (int s = -1; s <= 1; s += 2)
            {
                rust.Beam(g0 + new Vector3(s * 10f, 0f, 0f), g0 + new Vector3(s * 10f, 30f, 0f), 0.8f, 0.8f, Vector3.forward);
                col.Beam(g0 + new Vector3(s * 10f, 0f, 0f), g0 + new Vector3(s * 10f, 30f, 0f), 0.8f, 0.8f, Vector3.forward);
            }
            rust.Beam(g0 + new Vector3(-10f, 30f, 0f), g0 + new Vector3(10f, 30f, 0f), 1.0f, 1.0f, Vector3.forward);
            for (int i = 0; i < 5; i++)
                rust.Beam(g0 + new Vector3(-10f + i * 5f, 30f, 0f), g0 + new Vector3(-10f + i * 5f, 22f, 0f), 0.12f, 0.12f, Vector3.forward);
            rust.Beam(g0 + new Vector3(0f, 30f, 0f), g0 + new Vector3(0f, 12f, 0f), 0.10f, 0.10f, Vector3.forward);

            // broken perimeter wall, rubble field, snow
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI * 2f / 24f;
                float rr = 55f;
                Vector3 p = c + new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr);
                p.y = HeightAt(p.x, p.z);
                if (sr.NextDouble() < 0.25) continue;
                float h = 1.5f + (float)sr.NextDouble() * 3f;
                conc.Box(p + Vector3.up * h * 0.5f, new Vector3(8f, h, 0.5f), Q(-a * Mathf.Rad2Deg));
                snow.Box(p + Vector3.up * (h + 0.04f), new Vector3(8.1f, 0.08f, 0.6f), Q(-a * Mathf.Rad2Deg));
            }
            Rubble(conc, col, c, 50f, 120, sr, 1.4f);
            for (int i = 0; i < 22; i++)
            {
                Vector3 p = c + new Vector3((float)sr.NextDouble() * 70f - 35f, 0f, (float)sr.NextDouble() * 60f - 30f);
                p.y = HeightAt(p.x, p.z);
                rust.Beam(p, p + new Vector3((float)sr.NextDouble() * 5f - 2.5f, 3f + (float)sr.NextDouble() * 5f,
                                            (float)sr.NextDouble() * 5f - 2.5f), 0.05f, 0.05f, Vector3.forward);
            }
            conc.Emit(Root, "ruins2_concrete", ConcreteDarkMat, true);
            snow.Emit(Root, "ruins2_snow", SnowBankMat);
            rust.Emit(Root, "ruins2_rust", RustMat, true);
            glass.Emit(Root, "ruins2_glass", WindowDarkMat, false, ShadowCastingMode.Off);
            col.EmitCollider(Root, "ruins2_col");
            Log("[SULIMAN] detailed landmark: ruined tower complex (3 towers, silos, gantry)");
        }

        private static void RuinTowerDetail(SGeo conc, SGeo snow, SGeo rust, SGeo glass, SGeo col,
                                            Vector3 c, float w, float d, float h, int seed)
        {
            var sr = new System.Random(seed);
            float gy = c.y;
            int floors = Mathf.FloorToInt(h / 4.4f);
            for (int f = 0; f < floors; f++)
            {
                float y = gy + f * 4.4f;
                // perimeter panels with missing fragments
                for (int side = 0; side < 4; side++)
                {
                    bool xSide = side < 2;
                    float len = xSide ? d : w;
                    Vector3 n = xSide ? new Vector3(side == 0 ? -1f : 1f, 0f, 0f) : new Vector3(0f, 0f, side == 2 ? -1f : 1f);
                    Vector3 mid = c + n * (xSide ? w * 0.5f : d * 0.5f);
                    int panels = Mathf.Max(2, Mathf.FloorToInt(len / 2.4f));
                    for (int p = 0; p < panels; p++)
                    {
                        if (sr.NextDouble() < 0.18) continue;
                        if (f == 0 && p == panels / 2) continue;
                        float along = -len * 0.5f + (p + 0.5f) * len / panels;
                        Vector3 pos = mid + (xSide ? new Vector3(0f, 0f, along) : new Vector3(along, 0f, 0f));
                        pos.y = y + 2.2f;
                        var rot = xSide ? Q(90f) : Quaternion.identity;
                        var sz = new Vector3(len / panels - 0.06f, 4.4f, 0.6f);
                        conc.Box(pos, sz, rot);
                        col.Box(pos, sz, rot);
                    }
                }
                if (f % 2 == 0)
                {
                    conc.Box(new Vector3(c.x, y + 0.22f, c.z), new Vector3(w, 0.44f, d), Quaternion.identity);
                    col.Box(new Vector3(c.x, y + 0.22f, c.z), new Vector3(w, 0.44f, d), Quaternion.identity);
                    snow.Box(new Vector3(c.x, y + 0.47f, c.z), new Vector3(w - 0.8f, 0.06f, d - 0.8f), Quaternion.identity);
                    // a few glassless window holes
                    for (int q = 0; q < 3; q++)
                    {
                        float gx = c.x + (float)sr.NextDouble() * (w - 4f) - w * 0.5f + 2f;
                        float gz = c.z - d * 0.5f - 0.31f;
                        glass.Box(new Vector3(gx, y + 2.2f, gz), new Vector3(1.3f, 1.7f, 0.05f), Quaternion.identity);
                    }
                }
            }
            // broken crown and rebar
            for (int i = 0; i < 18; i++)
            {
                float x = c.x + (float)sr.NextDouble() * w - w * 0.5f;
                float z = c.z + (float)sr.NextDouble() * d - d * 0.5f;
                float top = gy + h + (float)sr.NextDouble() * 4f;
                conc.Box(new Vector3(x, top, z), new Vector3(1.4f, 3f, 1.2f),
                         Quaternion.Euler(sr.Next(-10, 10), sr.Next(0, 90), sr.Next(-10, 10)));
                rust.Beam(new Vector3(x, top + 1f, z), new Vector3(x + 0.5f, top + 4.5f, z + 0.3f),
                          0.05f, 0.05f, Vector3.forward);
            }
            // central stair core
            Vector3 sc = c + new Vector3(w * 0.18f, 0f, 0f);
            for (int f = 0; f < floors; f++)
            {
                float y = gy + f * 4.4f;
                conc.Box(new Vector3(sc.x, y + 2.2f, sc.z), new Vector3(4.2f, 4.4f, 4.2f), Quaternion.identity);
                col.Box(new Vector3(sc.x, y + 2.2f, sc.z), new Vector3(4.2f, 4.4f, 4.2f), Quaternion.identity);
            }
        }

        // ------------------------------------------------------------------ 3 pump

        private static void PumpStationDetail()
        {
            Vector3 c = OnGround(568f, 1286f);
            float gy = c.y;
            var conc = new SGeo { UvScale = 0.3f };
            var snow = new SGeo { UvScale = 0.26f };
            var rust = new SGeo { UvScale = 0.3f };
            var glow = new SGeo { UvScale = 1f };
            var dark = new SGeo { UvScale = 1f };
            var metal = new SGeo { UvScale = 0.4f };
            var col = new SGeo { UvScale = 1f };

            // machine hall
            conc.Box(new Vector3(c.x, gy + 6f, c.z), new Vector3(32f, 12f, 24f), Quaternion.identity);
            col.Box(new Vector3(c.x, gy + 6f, c.z), new Vector3(32f, 12f, 24f), Quaternion.identity);
            snow.Box(new Vector3(c.x, gy + 12.1f, c.z), new Vector3(32.2f, 0.12f, 24.2f), Quaternion.identity);
            rust.Box(new Vector3(c.x, gy + 0.9f, c.z), new Vector3(32.4f, 1.8f, 24.4f), Quaternion.identity);
            // control cabin
            conc.Box(new Vector3(c.x - 9f, gy + 15f, c.z), new Vector3(13f, 6f, 8f), Quaternion.identity);
            snow.Box(new Vector3(c.x - 9f, gy + 18.1f, c.z), new Vector3(13.2f, 0.10f, 8.2f), Quaternion.identity);
            for (int i = -1; i <= 1; i++)
            {
                glow.Box(new Vector3(c.x - 9f + i * 4f, gy + 15.2f, c.z - 4.05f), new Vector3(2.8f, 1.5f, 0.05f), Quaternion.identity);
                AddLampSpot(new Vector3(c.x - 9f + i * 4f, gy + 14.5f, c.z - 5.4f), 16f);
            }
            // chimney / vent stack
            rust.Cylinder(new Vector3(c.x + 12f, gy + 12f, c.z + 8f), 0.9f, 22f, 12, null);
            rust.Disc(new Vector3(c.x + 12f, gy + 34f, c.z + 8f), 0.9f, 12);
            snow.Disc(new Vector3(c.x + 12f, gy + 34.05f, c.z + 8f), 0.86f, 12);
            // fuel tanks
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 p = c + new Vector3(s * 19f, 0f, -13f);
                p.y = HeightAt(p.x, p.z);
                rust.Cylinder(p, 4.2f, 10f, 16, null);
                col.Cylinder(p, 4.2f, 10f, 16, null);
                col.Disc(p + Vector3.up * 10f, 4.2f, 16);
                rust.Disc(p + Vector3.up * 10f, 4.2f, 16);
                snow.Disc(p + Vector3.up * 10.06f, 4.05f, 16);
            }
            // intake channel, trash racks and outfall pipes toward the river (west)
            Vector3 ch = c + new Vector3(-23f, 0f, 0f);
            ch.y = HeightAt(ch.x, ch.z);
            for (int s = -1; s <= 1; s += 2)
            {
                conc.Box(ch + new Vector3(0f, 1.6f, s * 6f), new Vector3(22f, 3.2f, 1.2f), Quaternion.identity);
                col.Box(ch + new Vector3(0f, 1.6f, s * 6f), new Vector3(22f, 3.2f, 1.2f), Quaternion.identity);
            }
            conc.Box(ch + Vector3.up * 0.2f, new Vector3(22f, 0.4f, 13f), Quaternion.identity);
            for (int i = 0; i < 15; i++)
                metal.Beam(ch + new Vector3(-10f + i * 1.4f, 0f, -6f), ch + new Vector3(-10f + i * 1.4f, 3.4f, -6f),
                           0.09f, 0.09f, Vector3.forward);
            for (int i = 0; i < 2; i++)
            {
                Vector3 a = c + new Vector3(-16f + i * 3f, gy + 2.2f + i * 1.5f, -12f);
                Vector3 b = ch + new Vector3(-8f, 1.2f - i * 0.6f, -10f);
                rust.Beam(a, b, 0.75f, 0.75f, Vector3.up);
                rust.Cylinder(b, 0.75f, 7f, 10, null);
            }
            // crane rail over the hall
            for (int s = -1; s <= 1; s += 2)
            {
                metal.Beam(c + new Vector3(s * 13f, gy + 12.5f, -9f), c + new Vector3(s * 13f, gy + 12.5f, 9f),
                           0.3f, 0.3f, Vector3.up);
                col.Beam(c + new Vector3(s * 13f, gy + 12.5f, -9f), c + new Vector3(s * 13f, gy + 12.5f, 9f),
                         0.3f, 0.3f, Vector3.up);
            }
            metal.Box(c + new Vector3(0f, gy + 12.5f, 0f), new Vector3(26f, 0.4f, 1.1f), Quaternion.identity);
            metal.Box(c + new Vector3(0f, gy + 10.8f, 0f), new Vector3(1.2f, 3.8f, 1.2f), Quaternion.identity);
            // external stairs, ladders, railings
            Vector3 st = c + new Vector3(-18f, 0f, -7f);
            st.y = HeightAt(st.x, st.z) + 1.0f;
            Steps(conc, col, st, new Vector3(-1f, 0f, 0f), 4f, 10, 0.55f, 1.0f);
            Rail(metal, st, st + new Vector3(-10f, 0f, 0f), 1.0f, 6);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 p = c + new Vector3(17f, 0f, s * 8f);
                p.y = HeightAt(p.x, p.z);
                rust.Cylinder(p, 0.08f, 12f, 6, null);
                for (int i = 0; i < 8; i++)
                    metal.Box(p + Vector3.up * (1.5f + i * 1.4f), new Vector3(0.7f, 0.06f, 0.06f), Quaternion.identity);
            }
            // beacon and floodlight
            var bgo = new GameObject("pump2_beacon");
            bgo.transform.SetParent(Root, false);
            bgo.transform.localPosition = new Vector3(c.x + 12f, gy + 36f, c.z + 8f);
            var bl = bgo.AddComponent<Light>();
            bl.type = LightType.Point; bl.color = new Color(1f, 0.10f, 0.05f);
            bl.intensity = 2.6f; bl.range = 14f; bl.shadows = LightShadows.None;

            conc.Emit(Root, "pump2_concrete", ConcreteMat, true);
            snow.Emit(Root, "pump2_snow", SnowBankMat);
            rust.Emit(Root, "pump2_rust", RustMat, true);
            glow.Emit(Root, "pump2_glow", GlowWarmMat, false, ShadowCastingMode.Off);
            dark.Emit(Root, "pump2_dark", GlassDarkMat, false, ShadowCastingMode.Off);
            metal.Emit(Root, "pump2_metal", MetalMat, true);
            col.EmitCollider(Root, "pump2_col");
            Log("[SULIMAN] detailed landmark: pump station (32x24 hall, channel, tanks, crane)");
        }

        // ------------------------------------------------------------------ 4 porthole gallery

        private static void PortholeGalleryDetail()
        {
            Vector3 c = OnGround(1180f, 1380f);
            float gy = c.y;
            var conc = new SGeo { UvScale = 0.3f };
            var snow = new SGeo { UvScale = 0.24f };
            var dark = new SGeo { UvScale = 1f };
            var metal = new SGeo { UvScale = 0.4f };
            var col = new SGeo { UvScale = 1f };
            const float L = 70f, H = 10f, D = 12f;
            const int holes = 9;
            const float step = 7.4f;
            float x0 = c.x - L * 0.5f;

            // wall columns between the circular openings
            for (int i = 0; i <= holes; i++)
            {
                float x = x0 + i * step;
                float w = i == 0 || i == holes ? 3.2f : 2.6f;
                conc.Box(new Vector3(x, gy + H * 0.5f, c.z), new Vector3(w, H, D), Quaternion.identity);
                col.Box(new Vector3(x, gy + H * 0.5f, c.z), new Vector3(w, H, D), Quaternion.identity);
            }
            // spandrels above and below each opening + circular ring
            for (int i = 0; i < holes; i++)
            {
                float x = x0 + (i + 0.5f) * step;
                float cy = gy + 6.2f;
                const float r = 2.25f;
                float below = cy - r - gy;
                float above = gy + H - (cy + r);
                conc.Box(new Vector3(x, gy + below * 0.5f, c.z), new Vector3(step - 2.7f, below, D), Quaternion.identity);
                conc.Box(new Vector3(x, cy + r + above * 0.5f, c.z), new Vector3(step - 2.7f, above, D), Quaternion.identity);
                col.Box(new Vector3(x, gy + below * 0.5f, c.z), new Vector3(step - 2.7f, below, D), Quaternion.identity);
                col.Box(new Vector3(x, cy + r + above * 0.5f, c.z), new Vector3(step - 2.7f, above, D), Quaternion.identity);
                // ring voussoirs in the front plane
                for (int j = 0; j < 20; j++)
                {
                    float a0 = Mathf.PI * 2f * j / 20f;
                    float a1 = Mathf.PI * 2f * (j + 1) / 20f;
                    float am = (a0 + a1) * 0.5f;
                    float arc = (r + 0.7f) * Mathf.PI * 2f / 20f * 1.1f;
                    Vector3 p = new Vector3(x + Mathf.Cos(am) * (r + 0.35f),
                                            cy + Mathf.Sin(am) * (r + 0.35f), c.z - D * 0.5f - 0.05f);
                    conc.Box(p, new Vector3(arc, 0.7f, 1.4f), Quaternion.Euler(0f, 0f, am * Mathf.Rad2Deg));
                }
                // dark interior corridor behind the hole
                dark.Box(new Vector3(x, cy, c.z + 0.5f), new Vector3(4.4f, 4.4f, D - 1.2f), Quaternion.identity);
                metal.Box(new Vector3(x, cy - r - 0.06f, c.z - D * 0.5f - 0.1f), new Vector3(4.6f, 0.12f, 0.5f), Quaternion.identity);
                snow.Box(new Vector3(x, cy - r - 0.18f, c.z - D * 0.5f - 0.25f), new Vector3(4.7f, 0.06f, 0.6f), Quaternion.identity);
            }
            // roof, parapet, buttresses
            snow.Box(new Vector3(c.x, gy + H + 0.10f, c.z), new Vector3(L + 0.4f, 0.12f, D + 0.4f), Quaternion.identity);
            for (int i = 0; i < 16; i++)
            {
                float x = x0 + i * (L / 15f);
                for (int s = -1; s <= 1; s += 2)
                {
                    conc.Box(new Vector3(x, gy + H + 0.45f, c.z + s * (D * 0.5f - 0.2f)),
                             new Vector3(0.5f, 0.7f, 0.6f), Quaternion.identity);
                    col.Box(new Vector3(x, gy + H + 0.45f, c.z + s * (D * 0.5f - 0.2f)),
                            new Vector3(0.5f, 0.7f, 0.6f), Quaternion.identity);
                }
            }
            for (int i = 0; i < 5; i++)
            {
                float x = x0 - 2f + i * (L + 4f) / 4f;
                conc.Box(new Vector3(x, gy + 2.0f, c.z + D * 0.5f + 1.2f), new Vector3(2.2f, 4f, 2.4f), Quaternion.identity);
                snow.Box(new Vector3(x, gy + 4.1f, c.z + D * 0.5f + 1.2f), new Vector3(2.3f, 0.1f, 2.5f), Quaternion.identity);
            }
            // stairs to the roof at both ends, with handrails
            for (int e = -1; e <= 1; e += 2)
            {
                Vector3 st = new Vector3(c.x + e * (L * 0.5f + 2.4f), gy + 0.1f, c.z - D * 0.5f + 1.2f);
                Steps(conc, col, st, new Vector3(e == -1 ? -1f : 1f, 0f, 0f), 3.2f, 16, 0.62f, 0.9f);
                Rail(metal, st, st + new Vector3(e == -1 ? -14f : 14f, 0f, 0f), 1.0f, 9);
            }
            // vents and service boxes
            for (int i = 0; i < 7; i++)
            {
                float x = c.x - 24f + i * 8f;
                metal.Box(new Vector3(x, gy + H + 1.0f, c.z + 3f), new Vector3(1.3f, 2f, 1.3f), Quaternion.identity);
                snow.Box(new Vector3(x, gy + H + 2.05f, c.z + 3f), new Vector3(1.4f, 0.08f, 1.4f), Quaternion.identity);
            }
            conc.Emit(Root, "gallery2_concrete", ConcreteMat, true);
            snow.Emit(Root, "gallery2_snow", SnowBankMat);
            dark.Emit(Root, "gallery2_dark", GlassDarkMat, false, ShadowCastingMode.Off);
            metal.Emit(Root, "gallery2_metal", MetalMat, true);
            col.EmitCollider(Root, "gallery2_col");
            Log("[SULIMAN] detailed landmark: porthole gallery (70 m, 9 real openings)");
        }

        // ------------------------------------------------------------------ 5 underpass

        private static void UnderpassDetail()
        {
            Vector3 c = OnGround(930f, 920f);
            float gy = c.y;
            var conc = new SGeo { UvScale = 0.28f };
            var snow = new SGeo { UvScale = 0.24f };
            var dark = new SGeo { UvScale = 1f };
            var metal = new SGeo { UvScale = 0.4f };
            var glow = new SGeo { UvScale = 1f };
            var col = new SGeo { UvScale = 1f };
            const float W = 24f, L = 50f, H = 9f;

            // two tubes, central wall, floor and roof
            for (int s = -1; s <= 1; s += 2)
            {
                conc.Box(new Vector3(c.x + s * 9.5f, gy + H * 0.5f, c.z), new Vector3(5f, H, L), Quaternion.identity);
                col.Box(new Vector3(c.x + s * 9.5f, gy + H * 0.5f, c.z), new Vector3(5f, H, L), Quaternion.identity);
            }
            conc.Box(new Vector3(c.x, gy + H * 0.5f, c.z), new Vector3(4f, H, L), Quaternion.identity);
            col.Box(new Vector3(c.x, gy + H * 0.5f, c.z), new Vector3(4f, H, L), Quaternion.identity);
            conc.Box(new Vector3(c.x, gy + H, c.z), new Vector3(W + 5f, 1.2f, L), Quaternion.identity);
            col.Box(new Vector3(c.x, gy + H, c.z), new Vector3(W + 5f, 1.2f, L), Quaternion.identity);
            snow.Box(new Vector3(c.x, gy + H + 0.68f, c.z), new Vector3(W + 5.2f, 0.12f, L + 0.2f), Quaternion.identity);
            // portal rings at both ends
            for (int e = -1; e <= 1; e += 2)
            {
                float z = c.z + e * L * 0.5f;
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 pc = new Vector3(c.x + s * 6.5f, gy + 4.2f, z);
                    for (int j = 0; j < 16; j++)
                    {
                        float a0 = Mathf.PI * 2f * j / 16f;
                        float am = a0 + Mathf.PI / 16f;
                        float arc = 4.4f * Mathf.PI * 2f / 16f * 1.1f;
                        Vector3 p = pc + new Vector3(Mathf.Cos(am) * 4.2f, Mathf.Sin(am) * 4.2f, 0f);
                        conc.Box(p, new Vector3(arc, 0.9f, 1.5f), Quaternion.Euler(0f, 0f, am * Mathf.Rad2Deg));
                    }
                    dark.Box(new Vector3(pc.x, pc.y, z + e * 0.1f), new Vector3(7.2f, 7.2f, 0.8f), Quaternion.identity);
                }
            }
            // stair towers to the roof and side ramps
            for (int e = -1; e <= 1; e += 2)
            {
                Vector3 st = new Vector3(c.x + e * (W * 0.5f + 3f), gy + 0.1f, c.z - L * 0.5f + 3f);
                Steps(conc, col, st, new Vector3(e, 0f, 0f), 3.6f, 15, 0.6f, 0.9f);
                Rail(metal, st, st + new Vector3(e * 13f, 0f, 0f), 1.0f, 8);
                Vector3 ramp = new Vector3(c.x + e * (W * 0.5f + 1f), gy + 0.2f, c.z + 8f);
                conc.Box(ramp, new Vector3(7f, 0.4f, 22f), Q(-e * 6f));
                col.Box(ramp, new Vector3(7f, 0.4f, 22f), Q(-e * 6f));
            }
            // interior lights, pipes and wall bands
            for (int i = -2; i <= 2; i++)
            {
                glow.Box(new Vector3(c.x, gy + H - 0.35f, c.z + i * 9f), new Vector3(3.0f, 0.10f, 0.35f), Quaternion.identity);
                AddLampSpot(new Vector3(c.x, gy + H - 0.7f, c.z + i * 9f), 20f);
            }
            for (int s = -1; s <= 1; s += 2)
            {
                metal.Beam(new Vector3(c.x + s * 4.2f, gy + 0.6f, c.z - L * 0.4f),
                           new Vector3(c.x + s * 4.2f, gy + 0.6f, c.z + L * 0.4f), 0.22f, 0.22f, Vector3.up);
                metal.Box(new Vector3(c.x + s * 11.5f, gy + 2.2f, c.z), new Vector3(0.08f, 0.9f, L - 2f), Quaternion.identity);
            }
            // roof vents and railings
            for (int i = -1; i <= 1; i++)
            {
                metal.Box(new Vector3(c.x + i * 7f, gy + H + 1.4f, c.z), new Vector3(1.8f, 2.4f, 1.8f), Quaternion.identity);
                snow.Box(new Vector3(c.x + i * 7f, gy + H + 2.65f, c.z), new Vector3(1.9f, 0.08f, 1.9f), Quaternion.identity);
            }
            Rail(metal, new Vector3(c.x - W * 0.5f, gy + H + 0.7f, c.z - L * 0.5f),
                 new Vector3(c.x - W * 0.5f, gy + H + 0.7f, c.z + L * 0.5f), 1.1f, 12);
            Rail(metal, new Vector3(c.x + W * 0.5f, gy + H + 0.7f, c.z - L * 0.5f),
                 new Vector3(c.x + W * 0.5f, gy + H + 0.7f, c.z + L * 0.5f), 1.1f, 12);
            conc.Emit(Root, "underpass2_concrete", ConcreteMat, true);
            snow.Emit(Root, "underpass2_snow", SnowBankMat);
            dark.Emit(Root, "underpass2_dark", GlassDarkMat, false, ShadowCastingMode.Off);
            metal.Emit(Root, "underpass2_metal", MetalMat, true);
            glow.Emit(Root, "underpass2_glow", GlowWarmMat, false, ShadowCastingMode.Off);
            col.EmitCollider(Root, "underpass2_col");
            Log("[SULIMAN] detailed landmark: twin-tube underpass (24x50 m, stairs, ramps)");
        }

        // ------------------------------------------------------------------ 6 collapsed field

        private static void CollapsedBlocksDetail()
        {
            Vector3 c = OnGround(760f, 300f);
            var conc = new SGeo { UvScale = 0.28f };
            var snow = new SGeo { UvScale = 0.24f };
            var rust = new SGeo { UvScale = 0.35f };
            var dark = new SGeo { UvScale = 1f };
            var col = new SGeo { UvScale = 1f };
            var sr = new System.Random(9911);

            // standing ruin of a nine-storey panel block
            for (int i = 0; i < 5; i++)
            {
                float x = c.x - 25f + i * 7f;
                float h = 8f + i % 3 * 3f;
                conc.Box(new Vector3(x, c.y + h * 0.5f, c.z - 10f), new Vector3(5f, h, 3f), Q(sr.Next(-8, 8)));
                snow.Box(new Vector3(x, c.y + h + 0.05f, c.z - 10f), new Vector3(5.1f, 0.08f, 3.1f), Quaternion.identity);
            }
            for (int f = 0; f < 4; f++)
                conc.Box(new Vector3(c.x, c.y + 2.2f + f * 4.4f, c.z - 10f), new Vector3(28f, 0.4f, 13f), Q(f * 2f));
            // six fallen slabs
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = c + new Vector3(-30f + i * 11f, 0f, -3f + (i % 2) * 9f);
                p.y = HeightAt(p.x, p.z) + 0.8f;
                var r = Quaternion.Euler(sr.Next(-28, 28), sr.Next(0, 180), sr.Next(-28, 28));
                conc.Box(p, new Vector3(23f, 0.9f, 12f), r);
                snow.Box(p + Vector3.up * 0.5f, new Vector3(22f, 0.08f, 11f), r);
                col.Box(p, new Vector3(23f, 0.9f, 12f), r);
            }
            // foundation grid and broken columns
            for (int i = 0; i < 9; i++)
            {
                Vector3 p = c + new Vector3(-32f + i * 8f, 0f, 18f);
                p.y = HeightAt(p.x, p.z) + 0.3f;
                conc.Box(p, new Vector3(1.3f, 3.5f, 1.3f), Q(i * 7f));
                rust.Beam(p + Vector3.up * 1.5f, p + Vector3.up * 4.5f, 0.06f, 0.06f, Vector3.forward);
            }
            Rubble(conc, col, c, 55f, 160, sr, 1.6f);
            for (int i = 0; i < 50; i++)
            {
                Vector3 p = c + new Vector3((float)sr.NextDouble() * 90f - 45f, 0f, (float)sr.NextDouble() * 70f - 35f);
                p.y = HeightAt(p.x, p.z);
                rust.Beam(p, p + new Vector3((float)sr.NextDouble() * 8f - 4f, 4f + (float)sr.NextDouble() * 6f,
                                            (float)sr.NextDouble() * 8f - 4f), 0.05f, 0.05f, Vector3.forward);
            }
            // jagged glassless windows in the standing fragment
            for (int i = 0; i < 12; i++)
                dark.Box(new Vector3(c.x - 24f + i * 4.2f, c.y + 3.2f + (i % 3) * 3.4f, c.z - 11.55f),
                         new Vector3(1.5f, 1.9f, 0.05f), Quaternion.identity);
            conc.Emit(Root, "collapse2_concrete", ConcreteDarkMat, true);
            snow.Emit(Root, "collapse2_snow", SnowBankMat);
            rust.Emit(Root, "collapse2_rust", RustMat, true);
            dark.Emit(Root, "collapse2_dark", GlassDarkMat, false, ShadowCastingMode.Off);
            col.EmitCollider(Root, "collapse2_col");
            Log("[SULIMAN] detailed landmark: collapsed panel block field (90x70 m)");
        }

        // ------------------------------------------------------------------ 7 river stairs

        private static void RiverStairsDetail()
        {
            // Reference 7, rebuilt square to the river. A concrete waterfront complex:
            // a promenade slab cantilevered over the lower quay, a wide central stair
            // between them, a T-head pier jutting into the frozen river with twin stairs
            // down to the ice, a mooring arm, two service buildings on the bank and a
            // control cabin on the promenade. Everything is grounded and carries collision.
            Vector3 bank = OnGround(444f, 1133f);
            var conc = new SGeo { UvScale = 0.3f };
            var snow = new SGeo { UvScale = 0.26f };
            var metal = new SGeo { UvScale = 0.4f };
            var dark = new SGeo { UvScale = 1f };
            var glassGlow = new SGeo { UvScale = 1f };
            Vector3 dir = new Vector3(-0.82506f, 0f, 0.56504f).normalized;   // square to the river
            Vector3 side = new Vector3(-dir.z, 0f, dir.x);
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            float bankY = bank.y;
            float deckY = bankY + 2.00f;                 // promenade
            float quayY = deckY - 2.50f;                 // lower quay
            float iceY = HeightAt(bank.x + dir.x * 28f, bank.z + dir.z * 28f) + 1.15f;

            // ---------------------------------------------------------- bank buildings
            // left warehouse
            Vector3 lw = bank + dir * -1f + side * -14f;
            lw.y = HeightAt(lw.x, lw.z);
            conc.Box(new Vector3(lw.x, lw.y + 3f, lw.z), new Vector3(11f, 6f, 9f), rot);
            snow.Box(new Vector3(lw.x, lw.y + 6.05f, lw.z), new Vector3(11.2f, 0.10f, 9.2f), rot);
            // right warehouse
            Vector3 rw = bank + dir * -0.5f + side * 14f;
            rw.y = HeightAt(rw.x, rw.z);
            conc.Box(new Vector3(rw.x, rw.y + 2.5f, rw.z), new Vector3(9f, 5f, 8f), rot);
            snow.Box(new Vector3(rw.x, rw.y + 5.05f, rw.z), new Vector3(9.2f, 0.10f, 8.2f), rot);
            // loading doors and lit windows facing the promenade
            for (int i = -1; i <= 1; i += 2)
            {
                Vector3 w = lw + side * 5.52f + dir * (i * 2.2f);
                glassGlow.Box(new Vector3(w.x, lw.y + 3.4f, w.z), new Vector3(0.06f, 1.5f, 2.2f), rot);
                AddLampSpot(new Vector3(w.x + side.x * 1f, lw.y + 3.0f, w.z + side.z * 1f), 12f);
                Vector3 w2 = rw + side * -4.52f + dir * (i * 1.8f);
                glassGlow.Box(new Vector3(w2.x, rw.y + 2.9f, w2.z), new Vector3(0.06f, 1.3f, 1.9f), rot);
                AddLampSpot(new Vector3(w2.x - side.x * 1f, rw.y + 2.6f, w2.z - side.z * 1f), 11f);
                // the outer faces are lit too, so the blocks never read as blank boxes
                Vector3 w3 = lw - side * 5.52f + dir * (i * 2.0f);
                glassGlow.Box(new Vector3(w3.x, lw.y + 4.1f, w3.z), new Vector3(0.06f, 1.2f, 2.0f), rot);
                Vector3 w4 = rw + side * 4.52f + dir * (i * 1.7f);
                glassGlow.Box(new Vector3(w4.x, rw.y + 3.4f, w4.z), new Vector3(0.06f, 1.1f, 1.7f), rot);
            }
            // doors
            dark.Box(new Vector3(lw.x + side.x * 5.48f, lw.y + 1.2f, lw.z + side.z * 5.48f),
                     new Vector3(0.08f, 2.4f, 3.4f), rot);
            dark.Box(new Vector3(rw.x - side.x * 4.48f, rw.y + 1.1f, rw.z - side.z * 4.48f),
                     new Vector3(0.08f, 2.2f, 3.0f), rot);

            // ---------------------------------------------------------- promenade slab
            Vector3 deck = bank + dir * 2.8f;
            conc.Box(new Vector3(deck.x, deckY - 0.5f, deck.z), new Vector3(14f, 1.0f, 15.6f), rot);
            // entry apron where the bank meets the slab
            Vector3 ea = bank + dir * -5.2f;
            conc.Box(new Vector3(ea.x, deckY - 0.22f, ea.z), new Vector3(14f, 0.44f, 1.4f), rot);
            // control cabin on the promenade
            Vector3 cb = bank + dir * -1.5f + side * -4.2f;
            conc.Box(new Vector3(cb.x, deckY + 2.1f, cb.z), new Vector3(6.5f, 4.2f, 5f), rot);
            snow.Box(new Vector3(cb.x, deckY + 4.25f, cb.z), new Vector3(6.7f, 0.10f, 5.2f), rot);
            for (int i = -1; i <= 1; i += 2)
            {
                Vector3 w = cb + dir * 2.52f + side * (i * 1.5f);
                glassGlow.Box(new Vector3(w.x, deckY + 2.6f, w.z), new Vector3(1.6f, 1.3f, 0.06f), rot);
            }
            AddLampSpot(new Vector3(cb.x + dir.x * 3.4f, deckY + 2.4f, cb.z + dir.z * 3.4f), 12f);

            // heavy pier and two columns under the overhang
            Vector3 pier = bank + dir * 8.8f + side * -2.2f;
            float pierH = deckY - 1.0f - (quayY - 0.5f);
            conc.Box(new Vector3(pier.x, (quayY - 0.5f) + pierH * 0.5f, pier.z),
                     new Vector3(2.8f, pierH, 2.4f), rot);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 p = bank + dir * 9.6f + side * (5.9f * s);
                conc.Box(new Vector3(p.x, (quayY - 0.5f) + pierH * 0.5f, p.z),
                         new Vector3(1.3f, pierH, 1.3f), rot);
            }
            // deck parapets, split around the wide central stair
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 dp = bank + dir * 10.6f + side * (5.1f * s);
                conc.Box(new Vector3(dp.x, deckY + 0.42f, dp.z), new Vector3(3.8f, 0.84f, 0.45f), rot);
                snow.Box(new Vector3(dp.x, deckY + 0.87f, dp.z), new Vector3(3.9f, 0.05f, 0.55f), rot);
            }
            // thin snow strips on the promenade
            Vector3 ds1 = bank + dir * -3.8f + side * -6.2f;
            snow.Box(new Vector3(ds1.x, deckY + 0.03f, ds1.z), new Vector3(2.2f, 0.06f, 3.2f), rot);
            Vector3 ds2 = bank + dir * -4.4f + side * 1.2f;
            snow.Box(new Vector3(ds2.x, deckY + 0.03f, ds2.z), new Vector3(3.4f, 0.06f, 1.6f), rot);

            // ---------------------------------------------------------- lower quay
            Vector3 quay = bank + dir * 10.5f;
            conc.Box(new Vector3(quay.x, quayY - 1.3f, quay.z), new Vector3(17f, 2.6f, 13.8f), rot);
            Vector3 qs = bank + dir * 15.2f + side * -4.2f;
            snow.Box(new Vector3(qs.x, quayY + 0.03f, qs.z), new Vector3(2.4f, 0.06f, 2.8f), rot);
            Vector3 qs2 = bank + dir * 15.6f + side * 4.4f;
            snow.Box(new Vector3(qs2.x, quayY + 0.03f, qs2.z), new Vector3(3.0f, 0.06f, 1.8f), rot);

            // wide centre stair from the promenade down to the quay
            const int cs = 12;
            const float cRun = 0.5f;
            float cRise = (deckY - quayY) / cs;
            for (int i = 0; i < cs; i++)
            {
                Vector3 p = bank + dir * (10.6f + (i + 0.5f) * cRun);
                float top = deckY - (i + 1) * cRise;
                conc.Box(new Vector3(p.x, top - 0.14f, p.z), new Vector3(6f, 0.28f, cRun * 1.04f), rot);
                conc.Box(new Vector3(p.x, (top - 0.28f + quayY - 0.5f) * 0.5f, p.z),
                         new Vector3(6f, top - 0.28f - quayY + 0.5f, cRun * 0.96f), rot);
                snow.Box(new Vector3(p.x, top + 0.02f, p.z), new Vector3(5.8f, 0.04f, cRun * 0.88f), rot);
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 q = p + side * (3.2f * s);
                    conc.Box(new Vector3(q.x, (top + 0.95f + quayY - 0.5f) * 0.5f, q.z),
                             new Vector3(0.42f, top + 0.95f - quayY + 0.5f, cRun * 1.04f), rot);
                    snow.Box(new Vector3(q.x, top + 0.99f, q.z), new Vector3(0.52f, 0.05f, cRun * 1.06f), rot);
                }
            }

            // ---------------------------------------------------------- T-head pier
            Vector3 head = bank + dir * 20.1f;
            conc.Box(new Vector3(head.x, quayY - 1.3f, head.z), new Vector3(20f, 2.6f, 6.4f), rot);
            // parapet along the head edge, with two gaps for the water stairs
            float[] pw = { 2.6f, 5.4f, 2.6f };
            float[] pc = { -8.7f, 0f, 8.7f };
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = bank + dir * 23.2f + side * pc[i];
                conc.Box(new Vector3(p.x, quayY + 0.42f, p.z), new Vector3(pw[i], 0.84f, 0.45f), rot);
                snow.Box(new Vector3(p.x, quayY + 0.87f, p.z), new Vector3(pw[i] + 0.1f, 0.05f, 0.55f), rot);
            }
            // bollards along the head and the quay
            for (int s = -1; s <= 1; s += 2)
            {
                for (int k = 0; k < 2; k++)
                {
                    Vector3 p = bank + dir * (17.8f + k * 1.2f) + side * (7.9f * s);
                    p.y = quayY;
                    metal.Cylinder(p, 0.20f, 1.2f, 8, null);
                    dark.Cylinder(p + Vector3.up * 1.2f, 0.25f, 0.14f, 8, null);
                }
            }
            // mooring arm and dolphins out on the ice
            Vector3 arm = bank + dir * 26.5f + side * -9f;
            conc.Box(new Vector3(arm.x, quayY - 1.3f, arm.z), new Vector3(3.2f, 2.6f, 7f), rot);
            snow.Box(new Vector3(arm.x, quayY + 0.03f, arm.z), new Vector3(3.0f, 0.05f, 6.6f), rot);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 p = bank + dir * 25.4f + side * (8.6f * s);
                p.y = iceY;
                conc.Cylinder(p, 0.85f, quayY - iceY + 1.1f, 10, null);
                conc.Disc(new Vector3(p.x, quayY + 1.1f, p.z), 0.85f, 10);
                snow.Disc(new Vector3(p.x, quayY + 1.13f, p.z), 0.80f, 10);
            }
            // small crane on the head
            Vector3 cr = bank + dir * 22.6f + side * -8.4f;
            cr.y = quayY;
            metal.Cylinder(cr, 0.22f, 6.2f, 8, null);
            metal.Beam(cr + Vector3.up * 6.1f, cr + Vector3.up * 5.4f + dir * 5.5f, 0.28f, 0.28f, Vector3.up);
            metal.Beam(cr + Vector3.up * 6.1f, cr + Vector3.up * 7.4f, 0.16f, 0.16f, Vector3.up);
            dark.Box(cr + Vector3.up * 6.7f, new Vector3(0.7f, 0.7f, 0.7f), rot);
            // snow patches and cargo crates break up the T-head deck
            Vector3 hs1 = bank + dir * 19.6f + side * -6.2f;
            snow.Box(new Vector3(hs1.x, quayY + 0.03f, hs1.z), new Vector3(3.4f, 0.06f, 2.0f), rot);
            Vector3 hs2 = bank + dir * 21.0f + side * 4.6f;
            snow.Box(new Vector3(hs2.x, quayY + 0.03f, hs2.z), new Vector3(2.6f, 0.06f, 1.8f), rot);
            Vector3 hs3 = bank + dir * 22.2f + side * 0.8f;
            snow.Box(new Vector3(hs3.x, quayY + 0.03f, hs3.z), new Vector3(1.8f, 0.06f, 1.4f), rot);
            for (int i = 0; i < 2; i++)
            {
                Vector3 bc = bank + dir * (21.4f + i * 1.6f) + side * -6.8f;
                metal.Box(new Vector3(bc.x, quayY + 0.75f, bc.z), new Vector3(1.6f, 1.5f, 1.6f), rot);
                dark.Box(new Vector3(bc.x, quayY + 1.51f, bc.z), new Vector3(1.5f, 0.04f, 1.5f), rot);
            }

            // twin water stairs off the head edge
            const int ws = 10;
            const float wRun = 0.46f;
            float wRise = (quayY - iceY) / ws;
            for (int fl = -1; fl <= 1; fl += 2)
            {
                for (int i = 0; i < ws; i++)
                {
                    Vector3 p = bank + dir * (23.2f + (i + 0.5f) * wRun) + side * (5.0f * fl);
                    float top = quayY - (i + 1) * wRise;
                    float bottom = iceY - 0.6f;
                    conc.Box(new Vector3(p.x, top - 0.15f, p.z), new Vector3(4.6f, 0.30f, wRun * 1.05f), rot);
                    conc.Box(new Vector3(p.x, (top - 0.30f + bottom) * 0.5f, p.z),
                             new Vector3(4.6f, top - 0.30f - bottom, wRun * 0.98f), rot);
                    snow.Box(new Vector3(p.x, top + 0.02f, p.z), new Vector3(4.4f, 0.04f, wRun * 0.9f), rot);
                    // inner cheek, solid from ice to above the tread
                    Vector3 q = p + side * (-2.5f * fl);
                    conc.Box(new Vector3(q.x, (top + 1.0f + bottom) * 0.5f, q.z),
                             new Vector3(0.45f, top + 1.0f - bottom, wRun * 1.05f), rot);
                    snow.Box(new Vector3(q.x, top + 1.04f, q.z), new Vector3(0.55f, 0.05f, wRun * 1.07f), rot);
                    // outer curb
                    Vector3 o = p + side * (2.35f * fl);
                    conc.Box(new Vector3(o.x, top + 0.09f, o.z), new Vector3(0.3f, 0.22f, wRun * 1.02f), rot);
                }
                // outer cheek wall of each flight, from ice up to the head parapet
                Vector3 cw = bank + dir * 23.2f + side * (7.45f * fl);
                conc.Box(new Vector3(cw.x, (quayY + 1.0f + iceY - 0.6f) * 0.5f, cw.z),
                         new Vector3(0.5f, quayY + 1.0f - iceY + 0.6f, 4.9f), rot);
            }

            // ---------------------------------------------------------- lights
            Vector3 lp = bank + dir * -1f + side * 6.2f;
            lp.y = deckY;
            metal.Cylinder(lp, 0.13f, 5.4f, 6, null);
            Vector3 lh = lp + Vector3.up * 5.45f;
            conc.Box(lh, new Vector3(0.66f, 0.20f, 0.36f), Q(0f));
            AddLampSpot(lh, 22f);
            for (int i = 0; i < 2; i++)
            {
                Vector3 p = bank + dir * (12.5f + i * 6.4f) + side * (7.4f * (i == 0 ? -1 : 1));
                p.y = quayY;
                metal.Cylinder(p, 0.11f, 4.8f, 6, null);
                Vector3 h2 = p + Vector3.up * 4.9f;
                conc.Box(h2, new Vector3(0.58f, 0.17f, 0.32f), Q(0f));
                AddLampSpot(h2, 18f);
            }
            Vector3 hp = bank + dir * 22.9f + side * 0f;
            hp.y = quayY;
            metal.Cylinder(hp, 0.12f, 5.2f, 6, null);
            Vector3 hh = hp + Vector3.up * 5.3f;
            conc.Box(hh, new Vector3(0.6f, 0.18f, 0.34f), Q(0f));
            AddLampSpot(hh, 20f);
            Vector3 ul = bank + dir * 9.6f + side * 0.6f;
            ul.y = deckY - 1.2f;
            AddLampSpot(ul, 16f);
            Vector3 uw = bank + dir * 22.8f + side * 0f;
            uw.y = iceY + 2.4f;
            AddLampSpot(uw, 13f);

            conc.Emit(Root, "stairs2_concrete", ConcreteMat, true);
            snow.Emit(Root, "stairs2_snow", SnowBankMat);
            metal.Emit(Root, "stairs2_metal", MetalMat, true);
            dark.Emit(Root, "stairs2_dark", GlassDarkMat, false, ShadowCastingMode.Off);
            glassGlow.Emit(Root, "stairs2_glow", GlowWarmMat, false, ShadowCastingMode.Off);
            Log($"[SULIMAN] detailed landmark: river quay complex (deck {deckY:0.0}, quay {quayY:0.0}, ice {iceY:0.0}, T-head, twin stairs)");
        }

        // small local helper: a lit box without another SGeo bank
        private static void glowBox(Vector3 p)
        {
            var go = new GameObject("lm_lamp");
            go.transform.SetParent(Root, false);
            go.transform.localPosition = p;
            var g = new SGeo { UvScale = 1f };
            g.Box(p, new Vector3(0.6f, 0.16f, 0.3f), Quaternion.identity);
            var mesh = g.ToMesh("lm_lamp");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = LampHeadMat;
        }

        // ------------------------------------------------------------------ 8 cliff house

        private static void CliffHouseDetail()
        {
            Vector3 c = OnGround(1560f, 700f);
            float top = Mathf.Max(HeightAt(1560f, 700f), HeightAt(1530f, 700f)) + 1.2f;
            var conc = new SGeo { UvScale = 0.3f };
            var snow = new SGeo { UvScale = 0.24f };
            var metal = new SGeo { UvScale = 0.4f };
            var glow = new SGeo { UvScale = 1f };
            var dark = new SGeo { UvScale = 1f };
            var col = new SGeo { UvScale = 1f };

            // four support columns, main cantilevered volume, annex and roof
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 p = new Vector3(c.x + sx * 9.5f, 0f, c.z + sz * 5.5f);
                    float gy = HeightAt(p.x, p.z);
                    conc.Box(new Vector3(p.x, (gy + top) * 0.5f, p.z), new Vector3(2.2f, top - gy, 2.2f), Quaternion.identity);
                    col.Box(new Vector3(p.x, (gy + top) * 0.5f, p.z), new Vector3(2.2f, top - gy, 2.2f), Quaternion.identity);
                }
            conc.Box(new Vector3(c.x, top + 4.2f, c.z), new Vector3(36f, 8.4f, 22f), Quaternion.identity);
            col.Box(new Vector3(c.x, top + 4.2f, c.z), new Vector3(36f, 8.4f, 22f), Quaternion.identity);
            snow.Box(new Vector3(c.x, top + 8.45f, c.z), new Vector3(36.2f, 0.12f, 22.2f), Quaternion.identity);
            // glazed bands on both long faces
            for (int i = -4; i <= 4; i++)
            {
                glow.Box(new Vector3(c.x + i * 3.6f, top + 5.0f, c.z - 11.05f), new Vector3(2.6f, 2.0f, 0.06f), Quaternion.identity);
                glow.Box(new Vector3(c.x + i * 3.6f, top + 5.0f, c.z + 11.05f), new Vector3(2.6f, 2.0f, 0.06f), Quaternion.identity);
                AddLampSpot(new Vector3(c.x + i * 3.6f, top + 4.2f, c.z - 12.3f), 14f);
            }
            // cantilevered viewing deck and two terraces
            conc.Box(new Vector3(c.x, top + 0.65f, c.z - 16.5f), new Vector3(30f, 0.45f, 12f), Quaternion.identity);
            snow.Box(new Vector3(c.x, top + 0.92f, c.z - 16.5f), new Vector3(29.8f, 0.08f, 11.8f), Quaternion.identity);
            conc.Box(new Vector3(c.x, top + 0.65f, c.z + 17.5f), new Vector3(26f, 0.45f, 10f), Quaternion.identity);
            snow.Box(new Vector3(c.x, top + 0.92f, c.z + 17.5f), new Vector3(25.8f, 0.08f, 9.8f), Quaternion.identity);
            for (int s = -1; s <= 1; s += 2)
            {
                metal.Beam(new Vector3(c.x + s * 14.5f, top + 1.0f, c.z - 22f),
                           new Vector3(c.x + s * 14.5f, top + 1.0f, c.z - 11f), 0.1f, 0.1f, Vector3.up);
                for (int i = 0; i < 7; i++)
                {
                    Vector3 p = new Vector3(c.x + s * 14.5f, top + 1.4f, c.z - 22f + i * 1.9f);
                    metal.Box(p, new Vector3(0.08f, 1.0f, 0.08f), Quaternion.identity);
                }
            }
            // stair down the eastern slope
            Steps(conc, col, new Vector3(c.x - 23f, HeightAt(c.x - 23f, c.z + 8f) + 0.2f, c.z + 8f),
                  new Vector3(-1f, 0f, 0.25f), 4.2f, 23, 0.50f, 0.9f);
            Rail(metal, new Vector3(c.x - 23f, HeightAt(c.x - 23f, c.z + 8f) + 1f, c.z + 8f),
                 new Vector3(c.x - 43f, HeightAt(c.x - 43f, c.z + 13f) + 1f, c.z + 13f), 1.1f, 12);
            // antenna mast and red beacon
            metal.Cylinder(new Vector3(c.x, top + 8.5f, c.z), 0.12f, 13f, 6, null);
            var bg = new GameObject("cliff2_beacon");
            bg.transform.SetParent(Root, false);
            bg.transform.localPosition = new Vector3(c.x, top + 22f, c.z);
            var bl = bg.AddComponent<Light>();
            bl.type = LightType.Point; bl.color = new Color(1f, 0.1f, 0.05f);
            bl.intensity = 2.5f; bl.range = 16f; bl.shadows = LightShadows.None;
            // retaining wall and rock outcrops
            for (int i = 0; i < 8; i++)
            {
                float x = c.x - 20f + i * 6f;
                float z = c.z + 12f;
                float y = HeightAt(x, z);
                conc.Box(new Vector3(x, y + 1.0f, z), new Vector3(5.5f, 2f, 1.2f), Quaternion.identity);
                col.Box(new Vector3(x, y + 1.0f, z), new Vector3(5.5f, 2f, 1.2f), Quaternion.identity);
            }
            var sr = new System.Random(778);
            Rubble(conc, col, new Vector3(c.x + 4f, 0f, c.z - 24f), 18f, 35, sr, 1.5f);

            conc.Emit(Root, "cliff2_concrete", ConcreteMat, true);
            snow.Emit(Root, "cliff2_snow", SnowBankMat);
            metal.Emit(Root, "cliff2_metal", MetalMat, true);
            glow.Emit(Root, "cliff2_glow", GlowWarmMat, false, ShadowCastingMode.Off);
            dark.Emit(Root, "cliff2_dark", GlassDarkMat, false, ShadowCastingMode.Off);
            col.EmitCollider(Root, "cliff2_col");
            Log("[SULIMAN] detailed landmark: cantilevered cliff house (36x22 m, 2 terraces, antenna)");
        }
    }
}
