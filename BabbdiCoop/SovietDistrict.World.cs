using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace BabbdiCoop
{
    // Part of the SovietDistrict builder - see SovietDistrict.cs.
    internal static partial class SovietDistrict
    {

        // ------------------------------------------------------------------ the deck and the new dam

        private static void ClearInvisibleWalls(Vector3 origin, Quaternion rot)
        {
            var b = new Bounds(origin + rot * new Vector3(Len * 0.5f, -7f, Wid * 0.5f), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var lp = new Vector3(((i & 1) == 0) ? -Axw : Len,
                                     ((i & 2) == 0) ? -16f : 2f,
                                     ((i & 4) == 0) ? 0f : Wid);
                b.Encapsulate(origin + rot * lp);
            }
            int off = 0;
            var all = Resources.FindObjectsOfTypeAll<Collider>();
            for (int i = 0; i < all.Length; i++)
            {
                var c = all[i];
                if (c == null || !c.gameObject.scene.IsValid()) continue;
                bool wall = false;
                for (var p = c.transform; p != null; p = p.parent)
                {
                    if (p.name.IndexOf("INVISIBLE", StringComparison.OrdinalIgnoreCase) >= 0) { wall = true; break; }
                }
                if (!wall) continue;
                if (!c.bounds.Intersects(b)) continue;
                c.enabled = false;
                off++;
            }
            if (off > 0)
                log.LogInfo($"[DISTRICT] opened the sea boundary: {off} invisible wall colliders inside the new platform disabled");
        }

        // ------------------------------------------------------------------ terraces, ramps, stairs (v0.9.9)

        /// <summary>
        /// One raised floor of the platform вЂ” the pink outlines of the user's terraced plan. The base
        /// deck is 0 m and each zone is lifted by its own amount: 2 в†’ +5, 3 в†’ +8, 4 в†’ +11, 5 в†’ +14,
        /// 6 в†’ +10 m. The zones tile the platform: the seaward band, the two flanks, the western wing
        /// and the plinth under the House of Soviets; everything else stays on the base deck.
        /// </summary>
        private struct Tier
        {
            public string name;
            public float x0, x1, z0, z1, h;
        }

        /// <summary>The plinth of the House of Soviets: the esplanade and the House stand on it.</summary>
        private const float PlinthH = 10f;

        private static readonly Tier[] tiers =
        {
            new Tier { name = "3", x0 = 0f,   x1 = Len, z0 = 210f, z1 = Wid, h = 8f },    // seaward band
            new Tier { name = "2", x0 = 330f, x1 = Len, z0 = 0f,   z1 = 210f, h = 5f },   // east band
            new Tier { name = "4", x0 = 0f,   x1 = 48f, z0 = 0f,   z1 = 210f, h = 11f },  // west band
            new Tier { name = "5", x0 = -Axw, x1 = 0f,  z0 = Az0,  z1 = Wid, h = 14f },   // western wing
            new Tier { name = "6", x0 = 160f, x1 = 305f, z0 = 40f, z1 = 132f, h = PlinthH }, // House plinth
        };

        /// <summary>Height of the walkable surface at a local point: 0 on the base deck.</summary>
        private static float TierAt(float x, float z)
        {
            float h = 0f;
            for (int i = 0; i < tiers.Length; i++)
            {
                var t = tiers[i];
                if (x >= t.x0 && x <= t.x1 && z >= t.z0 && z <= t.z1 && t.h > h) h = t.h;
            }
            return h;
        }

        /// <summary>A yellow bar of the plan (an inclined road) or a green one (a street stair):
        /// it runs from (x0,z0) at height y0 to (x1,z1) at height y1.</summary>
        private struct Link
        {
            public string name;
            public float x0, z0, x1, z1, w, y0, y1;
        }

        // v0.9.9 вЂ” the yellow bars: roads that climb from one floor to the next (10-18 %), wide
        // enough for the game's vehicles; each one lands exactly on the edge of the upper terrace
        private static readonly Link[] ramps =
        {
            new Link { name = "ring_west", x0 = 56f,  z0 = 140f, x1 = 56f,  z1 = 210f, w = 11f, y0 = 0f,  y1 = 8f },
            new Link { name = "ring_east", x0 = 324f, z0 = 140f, x1 = 324f, z1 = 210f, w = 11f, y0 = 0f,  y1 = 8f },
            new Link { name = "east_band", x0 = 352f, z0 = 180f, x1 = 352f, z1 = 210f, w = 9f,  y0 = 5f,  y1 = 8f },
            new Link { name = "west_band", x0 = 8f,   z0 = 235f, x1 = 8f,   z1 = 210f, w = 9f,  y0 = 8f,  y1 = 11f },
            new Link { name = "wing",      x0 = 40f,  z0 = 255f, x1 = 0f,   z1 = 255f, w = 9f,  y0 = 8f,  y1 = 14f },
            new Link { name = "plinth",    x0 = 105f, z0 = 79f,  x1 = 160f, z1 = 79f,  w = 9f,  y0 = 0f,  y1 = 10f },
        };

        // the green bars: pedestrian stairs at the same edges (rise <= 0.23 m, the character
        // controller's step offset is 0.3 m)
        private static readonly Link[] stairs =
        {
            new Link { name = "stair_z3",     x0 = 137f, z0 = 192f, x1 = 137f, z1 = 210f, w = 6f,  y0 = 0f,  y1 = 8f },
            new Link { name = "stair_plinth", x0 = 230f, z0 = 12f,  x1 = 230f, z1 = 40f,  w = 22f, y0 = 0f,  y1 = 10f },
            new Link { name = "stair_z4",     x0 = 44f,  z0 = 222f, x1 = 44f,  z1 = 210f, w = 5f,  y0 = 8f,  y1 = 11f },
            new Link { name = "stair_wing",   x0 = 12f,  z0 = 167f, x1 = 0f,   z1 = 167f, w = 5f,  y0 = 11f, y1 = 14f },
        };

        /// <summary>Is a point inside a ramp's or stair's footprint (widened by `pad`)?</summary>
        private static bool InLink(Link l, float x, float z, float pad)
        {
            float mx = 0.5f * l.w + pad;
            return x > Mathf.Min(l.x0, l.x1) - mx && x < Mathf.Max(l.x0, l.x1) + mx &&
                   z > Mathf.Min(l.z0, l.z1) - mx && z < Mathf.Max(l.z0, l.z1) + mx;
        }

        private static bool Linked(float x, float z, float pad)
        {
            for (int i = 0; i < ramps.Length; i++) if (InLink(ramps[i], x, z, pad)) return true;
            for (int i = 0; i < stairs.Length; i++) if (InLink(stairs[i], x, z, pad)) return true;
            return false;
        }

        /// <summary>A box whose local +Z runs along aв†’b; it spans [line+lift, line+lift+t] measured
        /// along the box's own up, so a ramp can be laid exactly through two given points.</summary>
        private static void Beam(Geo g, Vector3 a, Vector3 b, float w, float t, float lift, Vector3 off)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.05f) return;
            Quaternion r = Quaternion.LookRotation(d / len, Vector3.up);
            g.Box(0, (a + b) * 0.5f + off + r * Vector3.up * (lift + t * 0.5f), new Vector3(w, t, len), r, 0.4f);
        }

        /// <summary>The raised floors themselves: one concrete box per zone, plus the parapets along
        /// every edge that drops to a lower floor (the pink lines of the plan read as walls).</summary>
        private static void Terraces()
        {
            var g = new Geo { FloorMul = PlatformGrain };
            for (int i = 0; i < tiers.Length; i++)
            {
                var t = tiers[i];
                g.Box(0, new Vector3((t.x0 + t.x1) * 0.5f, (t.h - 0.5f) * 0.5f, (t.z0 + t.z1) * 0.5f),
                      new Vector3(t.x1 - t.x0, t.h + 0.5f, t.z1 - t.z0), Quaternion.identity, DeckUv);
            }
            var go = g.Emit(root, "terraces", mRough, null);
            for (int i = 0; i < tiers.Length; i++)
            {
                var t = tiers[i];
                var bc = go.AddComponent<BoxCollider>();
                bc.center = new Vector3((t.x0 + t.x1) * 0.5f, (t.h - 0.5f) * 0.5f, (t.z0 + t.z1) * 0.5f);
                bc.size = new Vector3(t.x1 - t.x0, t.h + 0.5f, t.z1 - t.z0);
                log.LogInfo($"[DISTRICT] terrace {t.name}: {t.x1 - t.x0:0}x{t.z1 - t.z0:0} m at +{t.h:0} m");
            }

            var r = new Geo();
            int rails = 0;
            for (int i = 0; i < tiers.Length; i++)
            {
                var t = tiers[i];
                rails += Rail(r, t, t.x0, t.z0, t.x1, t.z0);
                rails += Rail(r, t, t.x0, t.z1, t.x1, t.z1);
                rails += Rail(r, t, t.x0, t.z0, t.x0, t.z1);
                rails += Rail(r, t, t.x1, t.z0, t.x1, t.z1);
            }
            var rgo = r.Emit(root, "terrace_rails", mRough, null);
            AddMeshCollider(rgo, "terrace_rails");
            log.LogInfo($"[DISTRICT] terrace edges: {rails} parapet segments (open where a ramp or a stair arrives)");
        }

        /// <summary>One terrace edge: 1.05 m parapet segments wherever the other side is lower.</summary>
        private static int Rail(Geo g, Tier t, float ax, float az, float bx, float bz)
        {
            Vector3 a = new Vector3(ax, 0f, az), b = new Vector3(bx, 0f, bz);
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1.5f) return 0;
            d /= len;
            Vector3 mid = (a + b) * 0.5f;
            Vector3 outw = mid - new Vector3((t.x0 + t.x1) * 0.5f, 0f, (t.z0 + t.z1) * 0.5f);
            outw.y = 0f;
            if (outw.sqrMagnitude < 0.01f) return 0;
            outw = (outw - Vector3.Dot(outw, d) * d).normalized;

            int n = Mathf.Max(1, Mathf.CeilToInt(len / 3f));
            float step = len / n;
            int made = 0;
            for (int i = 0; i < n; i++)
            {
                Vector3 c = a + d * ((i + 0.5f) * step);
                // a higher or equally high neighbour is its own wall вЂ” no parapet on this side
                if (TierAt(c.x + outw.x * 0.9f, c.z + outw.z * 0.9f) > t.h - 0.4f) continue;
                if (Linked(c.x, c.z, 2.5f)) continue;     // the ramp / stair arrives here
                g.Box(0, c - outw * 0.2f + Vector3.up * (t.h + 0.525f),
                      new Vector3(Mathf.Abs(d.x) * step + Mathf.Abs(d.z) * 0.4f, 1.05f,
                                  Mathf.Abs(d.z) * step + Mathf.Abs(d.x) * 0.4f),
                      Quaternion.identity, 0.5f);
                made++;
            }
            return made;
        }

        /// <summary>The inclined roads: a slab through the two end points plus a parapet on each side.</summary>
        private static void Ramps()
        {
            for (int i = 0; i < ramps.Length; i++)
            {
                var l = ramps[i];
                Vector3 a = new Vector3(l.x0, l.y0, l.z0), b = new Vector3(l.x1, l.y1, l.z1);
                Vector3 d = b - a;
                float len = d.magnitude;
                if (len < 1f) continue;
                d /= len;
                Vector3 side = Vector3.Cross(Vector3.up, d).normalized;
                float o = l.w * 0.5f - 0.16f;
                var g = new Geo { FloorMul = PlatformGrain };
                Beam(g, a, b, l.w, 0.7f, -0.7f, Vector3.zero);                       // the deck
                Beam(g, a, b, 0.32f, 1.02f, 0f, side * o);                            // parapets
                Beam(g, a, b, 0.32f, 1.02f, 0f, -side * o);
                // fill under the high end, so the ramp does not read as a floating plate
                float tuck = 4f;
                Vector3 tail = a + d * Mathf.Max(0f, len - tuck);
                Beam(g, tail, b, l.w, 1.4f, -1.4f, Vector3.zero);
                var go = g.Emit(root, "ramp_" + l.name, mRough, null);
                AddMeshCollider(go, "ramp");
                log.LogInfo($"[DISTRICT] ramp {l.name}: {l.y0:0.0} -> {l.y1:0.0} m over {len:0.0} m " +
                            $"({(l.y1 - l.y0) / len * 100f:0}% slope), {l.w:0.0} m wide");
            }
        }

        /// <summary>The street stairs: solid treads from the lower floor to the edge of the upper one.</summary>
        private static void TierStairs()
        {
            for (int i = 0; i < stairs.Length; i++)
            {
                var l = stairs[i];
                Vector3 a = new Vector3(l.x0, l.y0, l.z0), b = new Vector3(l.x1, l.y1, l.z1);
                Vector3 d = b - a;
                float run = d.magnitude;
                if (run < 0.5f) continue;
                d /= run;
                float dh = l.y1 - l.y0;
                int n = Mathf.Max(3, Mathf.RoundToInt(dh / 0.22f));
                float rise = dh / n, tread = run / n;
                float bot = l.y0 - 0.5f;
                var g = new Geo { FloorMul = PlatformGrain };
                for (int s = 0; s < n; s++)
                {
                    float top = l.y0 + (s + 1) * rise;
                    Vector3 p = a + d * ((s + 0.5f) * tread);
                    g.Box(0, new Vector3(p.x, (top + bot) * 0.5f, p.z),
                          new Vector3(Mathf.Abs(d.z) * l.w + Mathf.Abs(d.x) * tread, top - bot,
                                      Mathf.Abs(d.x) * l.w + Mathf.Abs(d.z) * tread),
                          Quaternion.identity, 0.5f);
                }
                Vector3 side = Vector3.Cross(Vector3.up, d).normalized;
                float o = l.w * 0.5f - 0.15f;
                Beam(g, a, b, 0.3f, 0.95f, 0f, side * o);                             // cheek walls
                Beam(g, a, b, 0.3f, 0.95f, 0f, -side * o);
                var go = g.Emit(root, "stairs_" + l.name, mRough, null);
                AddMeshCollider(go, "stairs");
                log.LogInfo($"[DISTRICT] stairs {l.name}: {n} steps of {rise:0.00} m (tread {tread:0.00} m), " +
                            $"{l.y0:0.0} -> {l.y1:0.0} m, {l.w:0.0} m wide");
            }
        }

        /// <summary>
        /// v0.9.22 вЂ” the south-east corner, rebuilt as **one smooth height field** instead of four boxes.
        ///
        /// What the v0.9.20/21 build actually looked like was measured, not guessed (`[PROBE] corner
        /// z-profile` / `x-profile`, `CornerScan`): the level's own granite shelf sits at **+0,32 m over
        /// the whole corner** (x в‰€ 330-376, z -4 вЂ¦ 10), and our deck is at 0. The old pad was a 0,40 m
        /// plate on top of it, made of four `Beam` boxes вЂ” a flat plate, a 4 m ramp, a tongue and a 10 m
        /// bevel. Each of those boxes is a *closed* box with its own 0,9 m skirt, and they met only along
        /// their edges, so one box's top could stand up to 8 cm proud of the next one's (the flat plate's
        /// 0,40 against the ramp's 0,32 at the same z) вЂ” that is the step in the user's screenshot 1,
        /// and the seam kerb of `Deck()` (a 12 cm box) was the hard edge of screenshot 2.
        ///
        /// Now the corner is a **single triangulated surface** h(x,z) with no internal edges at all:
        ///  - the flat top is **0,345 m** вЂ” 2,5 cm over the granite, so the granite is buried and the
        ///    two never z-fight, but the concrete reads as the crest's own level (v0.9.23; it was 0,365);
        ///  - the **west** side is a 16 m smoothstep down to -0,35 m and the **south** side a 4,5 m one
        ///    (v0.9.23): both cross the deck's plane and disappear **inside** the deck slab, so the ramp's
        ///    foot has no edge вЂ” it just fades into the floor (the user's screenshot 3);
        ///  - the **north** side is a tongue that sinks to 0,30 m under the granite at z = -2,4, i.e.
        ///    the concrete comes out from **under** the crest's own stone instead of lying on top of it;
        ///  - the terrace's wall (z = CornerApron) and the sea parapet (x = Len) bury the other two
        ///    edges, so the surface has no visible boundary anywhere.
        /// The southern bevel is the v0.9.23 fix: with the west bevel only, the pad ended in a **bare
        /// 0,25-0,36 m vertical face** along z в‰€ 12,3 for x в‰€ 320-330 вЂ” taller than the controller's
        /// 0,30 m stepOffset, so it was an unclimbable plate that blocked the way north from the deck
        /// (measured by `CornerSteps`: `25,3-36,5 cm at (324,12) -> (324,14)`).
        /// The `Deck()` seam kerb is gone in this area (see `Deck`), because a 12 cm kerb is exactly the
        /// hard edge the user asked to remove.
        /// </summary>
        private static void CornerPad()
        {
            if (CornerApron <= 0.01f) return;
            float z1 = Mathf.Clamp(CornerApron, 0f, 60f);
            const float H = 0.345f;     // the corner's top where it stands on our own deck
            const float Z0 = 0.15f;     // the deck's own north edge (the platform's edge)
            const float XF = 328f;      // where the flat top ends and the west bevel begins
            const float XW = 312f;      // the bevel's tip вЂ” buried inside the deck slab
            const float HW = -0.35f;    // the bevel's tip height (the deck slab spans -0,5 вЂ¦ 0)
            const float DZS = 4.5f;     // v0.9.23: the length of the **south** bevel
            float zS = z1 + 0.3f;       // where the flat top ends (east of x = 330 the terrace's wall buries it)
            float zE = zS + DZS;        // вЂ¦ and where the south bevel is buried inside the deck slab

            Func<float, float> smooth = u => { u = Mathf.Clamp01(u); return u * u * (3f - 2f * u); };
            // v0.9.50 вЂ” **the level's granite wins.** Measured with the pad's collider off, the dam
            // crest's granite shelf (0,32 m) runs diagonally under this whole corner: at z = 6 it starts
            // at x в‰€ 336 and from there to the terrace the ground *is* the granite. The v0.9.45 tongue
            // and the v0.9.47 north ramp both left the pad **25 mm** proud of that stone вЂ” the thin plate
            // the user walks around and climbs onto in their video. Now every vertex asks the scene what
            // is under it and, over the granite shelf, the surface is clamped to **10 mm** above the
            // stone: the concrete still covers the corner (the pad's look is unchanged), but the edge
            // is a hairline instead of a ledge вЂ” nothing to climb on, nothing to see under.
            Func<float, float, float> under = (x, z) =>
            {
                Vector3 w = root.TransformPoint(new Vector3(x, 40f, z));
                if (Physics.Raycast(w, Vector3.down, out var hit, 120f, ~0, QueryTriggerInteraction.Ignore))
                {
                    float gy = hit.point.y - root.position.y;
                    if (gy > 0.25f && gy < 0.80f) return gy + 0.010f;    // the granite shelf
                }
                return H;
            };
            Func<float, float, float> height = (x, z) =>
            {
                float wx = smooth((XF - x) / (XF - XW));
                float wz = smooth((z - zS) / DZS);
                float h = HW + (H - HW) * (1f - wx) * (1f - wz);
                return Mathf.Min(h, under(x, z));
            };

            var g = new Geo { FloorMul = PlatformGrain };
            int quads = 0;

            // ---- the top surface: one continuous grid, no internal seam ----------------------------
            const float DX = 2f;
            var zRows = new List<float>();
            float dzA = (zS - Z0) / 9f;
            for (int i = 0; i <= 9; i++) zRows.Add(Z0 + i * dzA);               // the flat top
            for (int i = 1; i <= 6; i++) zRows.Add(zS + i * (DZS / 6f));        // the south bevel
            int nx = Mathf.RoundToInt((Len - XW) / DX);
            for (int ix = 0; ix < nx; ix++)
            {
                float x0 = XW + ix * DX, x1 = x0 + DX;
                for (int iz = 0; iz < zRows.Count - 1; iz++)
                {
                    float z0 = zRows[iz], zr1 = zRows[iz + 1];
                    float cx = (x0 + x1) * 0.5f, cz = (z0 + zr1) * 0.5f;
                    float gx = (height(cx + 0.5f, cz) - height(cx - 0.5f, cz));
                    float gz = (height(cx, cz + 0.5f) - height(cx, cz - 0.5f));
                    Vector3 n = new Vector3(-gx, 1f, -gz).normalized;
                    g.Quad(0,
                           new Vector3(x0, height(x0, z0), z0),
                           new Vector3(x1, height(x1, z0), z0),
                           new Vector3(x1, height(x1, zr1), zr1),
                           new Vector3(x0, height(x0, zr1), zr1), n, 0.5f);
                    quads++;
                }
            }

            // ---- the three free edges get a skirt down into the deck, so the surface reads as a
            //      solid slab from the water side and never as a paper-thin sheet -------------------
            const float bot = -0.6f;
            for (float x = XW; x < Len - 0.01f; x += DX)          // the platform's edge (north face)
            {
                float x1 = Mathf.Min(x + DX, Len);
                float t0 = height(x, Z0), t1 = height(x1, Z0);
                g.Quad(0, new Vector3(x, t0, Z0 - 0.01f), new Vector3(x1, t1, Z0 - 0.01f),
                       new Vector3(x1, bot, Z0 - 0.01f), new Vector3(x, bot, Z0 - 0.01f),
                       Vector3.back, 0.5f);
                quads++;
            }

            var go = g.Emit(root, "corner_pad", mRough, null);
            AddMeshCollider(go, "corner_pad");
            log.LogInfo($"[DISTRICT] corner pad: {Len - XW:0} x {zE:0.#} m as ONE smooth height field " +
                        $"(grid {DX:0.#} m): flat {H:0.000} m from x={XF:0} to the sea parapet and z={Z0:0.##} " +
                        $"to the terrace's wall, a 16 m bevel down to {HW:0.00} m on the west and a {DZS:0.#} m " +
                        $"bevel down to {HW:0.00} m on the south, both of which dive inside the deck slab; " +
                        $"v0.9.50: the inland tongue is gone and over the level's granite shelf the surface " +
                        $"is clamped 10 mm above the stone, so its edge is a hairline instead of a ledge " +
                        $"({quads} quads)");
        }

        private static void Deck()
        {
            // slab: the main platform hangs off the crest into the sea; a western tongue (the white
            // zone on the plan) reaches past it and carries the outlying tower
            var deck = new Geo { FloorMul = PlatformGrain };
            deck.Box(0, new Vector3(Len * 0.5f, -1.3f, Wid * 0.5f + 0.1f),
                     new Vector3(Len, 2.6f, Wid - 0.1f), Quaternion.identity, DeckUv);
            deck.Box(0, new Vector3(-Axw * 0.5f, -1.3f, (Az0 + Wid) * 0.5f + 0.05f),
                     new Vector3(Axw, 2.6f, Wid - Az0 - 0.1f), Quaternion.identity, DeckUv);
            var go = deck.Emit(root, "deck", mRough, null);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(Len * 0.5f, -1.3f, Wid * 0.5f);   // must match the mesh, or the far half has no floor
            col.size = new Vector3(Len, 2.6f, Wid);
            var col2 = go.AddComponent<BoxCollider>();
            col2.center = new Vector3(-Axw * 0.5f, -1.3f, (Az0 + Wid) * 0.5f);
            col2.size = new Vector3(Axw, 2.6f, Wid - Az0);

            // outer dam wall: the old crest continues as a 16 m wall standing in the sea
            var wall = new Geo();
            const float wallTop = 1.15f;    // parapet height above the deck
            const float wallBot = -15.0f;   // down to below the sea surface
            float wallH = wallTop - wallBot;
            float wy = wallBot + wallH * 0.5f;
            float ncx = (Len - Axw) * 0.5f;                 // centre of the full-width seaward wall
            // seaward (north) face, from the tip of the tongue to the east end
            wall.Box(0, new Vector3(ncx, wy, Wid - 2.5f),
                     new Vector3(Len + Axw, wallH, 5f), Quaternion.identity, 0.22f);
            // east end of the platform.
            // v0.9.51: at the south-east corner the parapet is **cut** вЂ” over the corner apron its
            // top is lowered to the pad's own level (0,35 m), so the +5 m terrace's south wall runs
            // its full length with nothing of the platform's foundation standing in front of it.
            // This is a real cut in the geometry (two boxes instead of one), not a patch laid over it.
            float cutZ = Mathf.Clamp(CornerApron, 0f, 60f) + 0.3f;
            const float cutTop = 0.34f;
            float cutH = cutTop - wallBot;
            float cutY = wallBot + cutH * 0.5f;
            wall.Box(0, new Vector3(Len - 2.5f, cutY, cutZ * 0.5f),
                     new Vector3(5f, cutH, cutZ), Quaternion.identity, 0.22f);
            wall.Box(0, new Vector3(Len - 2.5f, wy, (cutZ + Wid) * 0.5f),
                     new Vector3(5f, wallH, Wid - cutZ), Quaternion.identity, 0.22f);
            // west end of the tongue
            wall.Box(0, new Vector3(-Axw + 2.5f, wy, (Az0 + Wid) * 0.5f),
                     new Vector3(5f, wallH, Wid - Az0), Quaternion.identity, 0.22f);
            // the tongue's inland face
            wall.Box(0, new Vector3(-Axw * 0.5f, wy, Az0 + 2.5f),
                     new Vector3(Axw, wallH, 5f), Quaternion.identity, 0.22f);
            // west end of the main platform, from the crest to the tongue
            wall.Box(0, new Vector3(2.5f, wy, Az0 * 0.5f),
                     new Vector3(5f, wallH, Az0), Quaternion.identity, 0.22f);
            // buttresses on the seaward faces, like the old dam
            for (float x = -Axw + 10f; x < Len - 4f; x += 15f)
                wall.Box(0, new Vector3(x, wy, Wid - 0.4f), new Vector3(2.2f, wallH, 1.8f), Quaternion.identity, 0.22f);
            for (float z = Az0 + 7f; z < Wid - 4f; z += 15f)
                wall.Box(0, new Vector3(-Axw + 0.4f, wy, z), new Vector3(1.8f, wallH, 2.2f), Quaternion.identity, 0.22f);
            for (float z = 8f; z < Az0 - 4f; z += 15f)
                wall.Box(0, new Vector3(0.4f, wy, z), new Vector3(1.8f, wallH, 2.2f), Quaternion.identity, 0.22f);
            // heavy coping on top of the parapets
            wall.Box(0, new Vector3(ncx, wallTop + 0.12f, Wid - 2.5f),
                     new Vector3(Len + Axw + 0.6f, 0.24f, 5.4f), Quaternion.identity, 0.30f);
            wall.Box(0, new Vector3(Len - 2.5f, wallTop + 0.12f, (cutZ + Wid) * 0.5f),
                     new Vector3(5.4f, 0.24f, Wid - cutZ), Quaternion.identity, 0.30f);
            wall.Box(0, new Vector3(-Axw + 2.5f, wallTop + 0.12f, (Az0 + Wid) * 0.5f),
                     new Vector3(5.4f, 0.24f, Wid - Az0), Quaternion.identity, 0.30f);
            wall.Box(0, new Vector3(-Axw * 0.5f, wallTop + 0.12f, Az0 + 2.5f),
                     new Vector3(Axw + 0.6f, 0.24f, 5.4f), Quaternion.identity, 0.30f);
            wall.Box(0, new Vector3(2.5f, wallTop + 0.12f, Az0 * 0.5f),
                     new Vector3(5.4f, 0.24f, Az0), Quaternion.identity, 0.30f);
            for (int i = 0; i <= 24; i++)   // drain slots / pilaster rhythm on the inside face
            {
                float x = -Axw + 6f + i * (Len + Axw - 12f) / 24f;
                wall.Box(0, new Vector3(x, 0.55f, Wid - 5.3f), new Vector3(1.4f, 1.1f, 0.35f), Quaternion.identity, 0.5f);
            }
            go = wall.Emit(root, "dam_wall", mRough, null);
            var bc = go.AddComponent<BoxCollider>();
            bc.center = new Vector3(ncx, wy, Wid - 2.5f);
            bc.size = new Vector3(Len + Axw, wallH, 5f);
            // every other wall gets its own collider so the player cannot walk off the deck
            AddWallCollider("parapet_sea", new Vector3(ncx, 0.6f, Wid - 2.5f), new Vector3(Len + Axw, 1.2f, 5f));
            AddWallCollider("parapet_east", new Vector3(Len - 2.5f, 0.6f, (cutZ + Wid) * 0.5f), new Vector3(5f, 1.2f, Wid - cutZ));
            AddWallCollider("parapet_west_annex", new Vector3(-Axw + 2.5f, 0.6f, (Az0 + Wid) * 0.5f), new Vector3(5f, 1.2f, Wid - Az0));
            AddWallCollider("parapet_annex_inland", new Vector3(-Axw * 0.5f, 0.6f, Az0 + 2.5f), new Vector3(Axw, 1.2f, 5f));
            AddWallCollider("parapet_west_main", new Vector3(2.5f, 0.6f, Az0 * 0.5f), new Vector3(5f, 1.2f, Az0));

            // v0.9.22: the 12 cm "seam kerb" that used to run along the junction (a 1,1 x 0,12 m box at
            // z=0,55) is gone. The user's screenshot 2 was exactly that hard edge, and the corner's new
            // height field fades into the deck on its own вЂ” a kerb would only put a step back.
        }

        private static void AddWallCollider(string name, Vector3 c, Vector3 s)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var bc = go.AddComponent<BoxCollider>();
            bc.center = c;
            bc.size = s;
        }

        // ------------------------------------------------------------------ ground: streets, yards

        private static void Streets()
        {
            var g = new Geo { FloorMul = PlatformGrain };
            // the ring road, laid out around the terraces (v0.9.9): the two legs hug the +11 m west
            // band (x <= 48) and the +5 m east band (x >= 330), so the roads stay on the base deck
            // until the ramps lift them onto the +8 m seaward terrace
            const float rw = 10f;                 // road width
            const float xw = 56f, xe = 324f;      // the two legs of the ring
            const float zs = 52f, zn = Wid - 52f; // 52 (crest side) and 248 (on the seaward terrace)
            // crest-side leg: interrupted by the plinth of the House of Soviets (x 160..305)
            Road(g, xw - rw * 0.5f, 160f, zs - rw * 0.5f, zs + rw * 0.5f, 0f);
            Road(g, 305f, xe + rw * 0.5f, zs - rw * 0.5f, zs + rw * 0.5f, 0f);
            // seaward leg: on top of the +8 m terrace
            Road(g, xw - rw * 0.5f, xe + rw * 0.5f, zn - rw * 0.5f, zn + rw * 0.5f, 8f);
            // the legs: base level up to the foot of the ramp, then on the terrace
            Road(g, xw - rw * 0.5f, xw + rw * 0.5f, zs, 140f, 0f);
            Road(g, xw - rw * 0.5f, xw + rw * 0.5f, 210f, zn, 8f);
            Road(g, xe - rw * 0.5f, xe + rw * 0.5f, zs, 140f, 0f);
            Road(g, xe - rw * 0.5f, xe + rw * 0.5f, 210f, zn, 8f);
            // approach avenue from the crest to the grand stair of the House of Soviets
            Road(g, 218f, 242f, 4f, 14f, 0f);
            var sgo = g.Emit(root, "streets", mRough, null);
            AddMeshCollider(sgo, "streets");

            // v0.9.4: no grass/dirt patches on the deck any more вЂ” the platform must read as one concrete
            // field like the dam (the coloured "patches" in the plan view were these). The draws are kept
            // so the deterministic layout (seed 20261023) does not shift between builds: 44 grass plots
            // and 10 dirt plots, 5 doubles each.
            for (int i = 0; i < (44 + 10) * 5; i++) rng.NextDouble();
        }

        /// <summary>One flat road box at a given height (the streets follow the terraces now).</summary>
        private static void Road(Geo g, float x0, float x1, float z0, float z1, float y)
        {
            if (x1 - x0 < 0.5f || z1 - z0 < 0.5f) return;
            g.Box(0, new Vector3((x0 + x1) * 0.5f, y + 0.025f, (z0 + z1) * 0.5f),
                  new Vector3(x1 - x0, 0.05f, z1 - z0), Quaternion.identity, DeckUv);
        }

        private static void AddMeshCollider(GameObject go, string tag)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = false;
        }
    }
}
