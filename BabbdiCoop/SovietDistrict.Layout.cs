using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace BabbdiCoop
{
    // Part of the SovietDistrict builder - see SovietDistrict.cs.
    internal static partial class SovietDistrict
    {

        // ------------------------------------------------------------------ the district

        private static void Layout()
        {
            // v0.9.8: the platform is 380 x 300 m (+80 m seaward, the contour of the user's plan B) and
            // the whole district sits 15 m west of the sea hole, so the ring has room for a full band
            // more. The order of the drawing is kept: a chain of blocks along every edge, the House of
            // Soviets and its square in the open middle, rows and columns inside, and a covered bridge
            // at every gap between neighbours.

            // --- the seaward edge (the promenade side): an outer row and a staggered inner row
            RowX(26f, Len - 26f, Wid - 26f, J(3f));
            RowX(74f, 312f, Wid - 74f, J(5f));

            // --- the band behind the House of Soviets: one continuous row right across the platform
            // (v0.9.9: it stops clear of the +11 m west band, the +5 m east band and both ramp roads)
            RowX(70f, 312f, 178f, J(4f));

            // --- the flanks: blocks turned 90 deg, so their corridors run seaward. They stand on the
            // raised bands (west +11 m, east +5 m) and stop clear of the ramps onto the seaward terrace
            RowZ(46f, Wid - 96f, 30f, 90f + J(3f));
            int ne = bldgs.Count;
            RowZ(46f, 168f, 354f, 90f + J(3f));
            // v0.9.73: the **bas-relief** block вЂ” the middle one of the east flank. Its outward long
            // wall (local +z, which faces world +x after the 90В° row yaw) is built solid and carries
            // the dark abstract ornament, so the wall that faces *out of the district* is the one.
            if (bldgs.Count > ne + 1)
            {
                var rb = bldgs[ne + 1];
                rb.winPlusZ = false;
                rb.balconies = false;
                rb.bands = false;
                rb.risalit = false;
                rb.relief = true;
                reliefBlock = rb;
            }

            // --- a column inside the west flank (the drawing has a row there as well), on the base deck
            RowZ(62f, 150f, 76f, 90f + J(3f));

            // --- and one point tower in the open square west of the House (it fills the gap between
            // the inner column and the House's plinth without crowding the approaches)
            Place(122f, 118f, 6f, 2, 0.85f);

            // --- the front along the dam crest, with the approach to the square left open
            RowX(56f, 168f, 26f, J(3f));
            // v0.9.19: **one house on legs** (the user's screenshots 1-2): the row's two blocks are joined
            // into a single rectangular mass вЂ” same depth, no gap, one yaw вЂ” whose western part stays the
            // lower one (the floor counts are never touched, only the plan)
            int nl = bldgs.Count;
            RowX(246f, 328f, 26f, J(3f));
            for (int i = nl; i < bldgs.Count; i++) bldgs[i].legs = true;
            if (bldgs.Count == nl + 2)
            {
                var west = bldgs[nl];
                var east = bldgs[nl + 1];
                float deep = Mathf.Max(west.d, east.d);
                west.d = east.d = deep;
                west.yaw = east.yaw = 0f;
                west.p = new Vector3(west.p.x, west.p.y, 26f);
                east.p = new Vector3(west.p.x + (west.w + east.w) * 0.5f, west.p.y, 26f);
                west.legsShut = 1;      // the shared face needs no pylons: the two houses are one
                east.legsShut = -1;
                log.LogInfo($"[LEGS] the pair is one house in plan: {west.w:0.0} + {east.w:0.0} = " +
                            $"{west.w + east.w:0.0} x {deep:0.0} m at x {west.p.x - west.w * 0.5f:0.0}.." +
                            $"{east.p.x + east.w * 0.5f:0.0}, z 26, yaw 0 (west {west.floors} floors, " +
                            $"east {east.floors} floors вЂ” heights unchanged)");
            }

            // --- the outlying tower on the western wing (the house marked on the plan), +14 m
            // v0.9.18: **this** is the block on the peninsula вЂ” the user's screenshot 1 вЂ” so its roof
            // gets the penthouse (see `Penthouse`). The flag is set on the block that was just placed,
            // not on the last one in the list, so a rejected plot cannot wear it by mistake.
            int nb = bldgs.Count;
            Place(-40f, 225f, 3f, 2, 1.25f);
            if (PenthouseOn && bldgs.Count > nb) bldgs[nb].penthouse = true;
        }

        private static float J(float a) => ((float)rng.NextDouble() * 2f - 1f) * a;

        private static float R() => (float)rng.NextDouble();

        private static float Gap() => 4f + R() * 8f;     // 4-12 m between neighbours in a row

        /// <summary>One row along x: the blocks are laid end to end, so no plot is ever rejected.</summary>
        private static void RowX(float x0, float x1, float z, float yaw)
        {
            int n = bldgs.Count;
            float x = x0;
            while (true)
            {
                int type = Kind();
                var s = RollStyle(type, 1f);
                if (x + s.w > x1) break;
                AddBlock(new Vector3(x + s.w * 0.5f, 0f, z), yaw, type, s);
                x += s.w + Gap();
            }
            log.LogInfo($"[DISTRICT] row along x at z={z:0.0} (x {x0:0}..{x1:0}): {bldgs.Count - n} blocks");
        }

        /// <summary>One row along z; the blocks are turned 90 deg, so their long axis runs seaward.</summary>
        private static void RowZ(float z0, float z1, float x, float yaw)
        {
            int n = bldgs.Count;
            float z = z0;
            while (true)
            {
                int type = Kind();
                var s = RollStyle(type, 1f);
                if (z + s.w > z1) break;
                AddBlock(new Vector3(x, 0f, z + s.w * 0.5f), yaw, type, s);
                z += s.w + Gap();
            }
            log.LogInfo($"[DISTRICT] row along z at x={x:0.0} (z {z0:0}..{z1:0}): {bldgs.Count - n} blocks");
        }

        private static int Kind()
        {
            double r = rng.NextDouble();
            return r < 0.44 ? 0 : (r < 0.68 ? 1 : (r < 0.86 ? 2 : 3));
        }

        private static void Place(float x, float z, float yaw, int type, float size = 1f)
            => MakeBuilding(new Vector3(x, 0f, z), yaw, type, size);

        /// <summary>True if a point is on the platform (main rectangle or the western tongue).</summary>
        private static bool OnDeck(float x, float z, float m)
        {
            if (x > 3f + m && x < Len - 3f - m && z > 3f + m && z < Wid - 6f - m) return true;
            if (x > -Axw + 3f + m && x < -3f - m && z > Az0 + 3f + m && z < Wid - 3f - m) return true;
            return false;
        }

        private static void Axes(float yaw, out Vector2 ex, out Vector2 ez)
        {
            float c = Mathf.Cos(yaw * Mathf.Deg2Rad), s = Mathf.Sin(yaw * Mathf.Deg2Rad);
            ex = new Vector2(c, -s);     // local +x in XZ
            ez = new Vector2(s, c);      // local +z in XZ
        }

        private static float Radius(Vector2 u, Vector2 ex, Vector2 ez, float w, float d)
            => Mathf.Abs(Vector2.Dot(u, ex)) * w * 0.5f + Mathf.Abs(Vector2.Dot(u, ez)) * d * 0.5f;

        /// <summary>Separating-axis test of two rotated footprints (exact, unlike a bounding box).</summary>
        private static bool Overlap(Vector3 pa, float wa, float da, float ya,
                                    Vector3 pb, float wb, float db, float yb, float margin)
        {
            Vector2 exa, eza, exb, ezb;
            Axes(ya, out exa, out eza);
            Axes(yb, out exb, out ezb);
            var d = new Vector2(pb.x - pa.x, pb.z - pa.z);
            for (int k = 0; k < 4; k++)
            {
                Vector2 u = k == 0 ? exa : (k == 1 ? eza : (k == 2 ? exb : ezb));
                float r = Radius(u, exa, eza, wa, da) + Radius(u, exb, ezb, wb, db) + margin;
                if (Mathf.Abs(Vector2.Dot(d, u)) >= r) return false;
            }
            return true;
        }

        /// <summary>Footprint test: on the platform, clear of the other blocks and of the square.</summary>
        private static bool Free(Vector3 p, float w, float d, float yaw, float margin)
        {
            // all four corners must be on the deck
            Vector2 ex, ez;
            Axes(yaw, out ex, out ez);
            for (int k = 0; k < 4; k++)
            {
                float sx = (k == 0 || k == 3) ? -1f : 1f;
                float sz = (k < 2) ? -1f : 1f;
                float cx = p.x + ex.x * sx * w * 0.5f + ez.x * sz * d * 0.5f;
                float cz = p.z + ex.y * sx * w * 0.5f + ez.y * sz * d * 0.5f;
                if (!OnDeck(cx, cz, 2f)) return false;
            }
            for (int i = 0; i < bldgs.Count; i++)
            {
                var b = bldgs[i];
                if (Overlap(p, w, d, yaw, b.p, b.w, b.d, b.yaw, margin)) return false;
            }
            // the House of Soviets and its square are reserved
            float c = Mathf.Abs(Mathf.Cos(yaw * Mathf.Deg2Rad)), s = Mathf.Abs(Mathf.Sin(yaw * Mathf.Deg2Rad));
            float hx = c * w * 0.5f + s * d * 0.5f;
            float hz = s * w * 0.5f + c * d * 0.5f;
            if (p.x + hx > 165f && p.x - hx < 300f && p.z + hz > 45f && p.z - hz < 125f) return false;
            return true;
        }

        /// <summary>Rolls the footprint of one block type (already +30 % in every plane).</summary>
        private static BlockStyle RollStyle(int type, float size)
        {
            float w, d;
            int baseFloors;
            switch (type)
            {
                case 0:   // khrushchevka: panel block
                    w = 24f + R() * 7f;
                    d = 12f + R() * 3f;
                    baseFloors = 5;
                    break;
                case 1:   // stalinka: heavier walls, cornice, risalit
                    w = 26f + R() * 8f;
                    d = 14f + R() * 3f;
                    baseFloors = 7;
                    break;
                case 2:   // point tower
                    w = 16f + R() * 4f;
                    d = w;
                    baseFloors = 9 + rng.Next(0, 4);
                    break;
                default:  // slab
                    w = 31f + R() * 7f;
                    d = 13f + R() * 3f;
                    baseFloors = 9;
                    break;
            }
            return new BlockStyle { w = w * size * Grow, d = d * size * Grow, baseFloors = baseFloors };
        }

        /// <summary>
        /// v0.9.13 вЂ” how many times the 2.2 m corridor is widened on this plot: 2, 3 or 4. Arbitrary per
        /// house, but a pure function of the plot coordinates, so the seeded layout stays reproducible
        /// and `AddBlock` never draws from the RNG for it.
        /// </summary>
        private static int CorridorMul(Vector3 p)
        {
            int h = Mathf.RoundToInt(p.x * 8f) * 73856093 ^ Mathf.RoundToInt(p.z * 8f) * 19349663;
            h = (h ^ (h >> 13)) * 1274126177;
            return 2 + Mathf.Abs((h >> 9) % 3);
        }

        private static void AddBlock(Vector3 p, float yaw, int type, BlockStyle s)
        {
            // v0.9.13: a wide corridor is taken out of the block's depth, so the rooms keep their size
            // and the house itself gets deeper вЂ” the user asked for corridors 2x/3x/4x as wide
            int cm = CorridorMul(p);
            float d = s.d + (cm - 1) * CORR;
            var b = new Bldg { p = p, yaw = yaw, w = s.w, d = d, corrMul = cm };
            // v0.9.4: every block is the city's own concrete (`M_ConcreteRough00`, the dam's material);
            // v0.9.76: **one tone for all** вЂ” the panel-block mix was removed (the user: some houses
            // were lighter than the others).
            b.wall = mRough;
            switch (type)
            {
                case 0: b.balconies = true; break;
                case 1: b.risalit = true; break;
                case 2: b.bands = true; break;
            }
            // v0.9.12: the tall storey is no longer a stalinka speciality вЂ” every block in the district
            // is built with it (the user asked for the stalinka's proportions everywhere): 5.6 m
            // ceilings, +30 % door openings, and windows grown to match. No RNG is drawn here on
            // purpose, so the seeded layout stays exactly the same as in v0.9.10.
            b.fh = FH * TallFh;        // 5.6 m ceilings
            b.dh = TallDoor;           // +30 % door openings
            b.winH = 2.2f;             // taller windows for the taller storey
            b.winW = 1.55f;
            b.sill = 1.1f;
            // the whole district was raised: six storeys on top, 3-10 more per house, and +30 % height
            b.floors = Mathf.RoundToInt((s.baseFloors + 6 + rng.Next(3, 11)) * Grow);
            rng.NextDouble();   // the old facade-variety roll вЂ” kept so the seeded layout does not shift

            // v0.9.9: the block stands on the terrace that covers it вЂ” and never on two of them,
            // otherwise its floor would float above the lower half of its footprint
            float th = TierAt(p.x, p.z);
            Vector2 ex, ez;
            Axes(yaw, out ex, out ez);

            // v0.9.13: the first multiplier that still fits the plot wins; a deep house that would
            // overlap a neighbour or straddle a terrace edge falls back to a narrower corridor instead
            // of leaving a hole in the ring
            bool fits = false;
            int cmOk = 1;
            float dOk = s.d;
            for (int m = cm; m >= 1; m--)
            {
                float dd = s.d + (m - 1) * CORR;
                if (!Free(p, s.w, dd, yaw, 2.2f)) continue;
                bool straddle = false;
                for (int k = 0; k < 4 && !straddle; k++)
                {
                    float sx = (k == 0 || k == 3) ? -1f : 1f;
                    float sz = (k < 2) ? -1f : 1f;
                    float cx = p.x + ex.x * sx * s.w * 0.5f + ez.x * sz * dd * 0.5f;
                    float cz = p.z + ex.y * sx * s.w * 0.5f + ez.y * sz * dd * 0.5f;
                    if (Mathf.Abs(TierAt(cx, cz) - th) > 0.01f) straddle = true;
                }
                if (straddle) continue;
                cmOk = m;
                dOk = dd;
                fits = true;
                break;
            }
            if (!fits)
            {
                skipCount++;
                log.LogInfo($"[DISTRICT] skipped a {s.w:0.0}x{s.d:0.0} m block at {p.x:0.0},{p.z:0.0}: no fit");
                return;
            }
            b.corrMul = cmOk;
            b.d = dOk;
            b.baseY = th + BASE;
            b.plinth = th < 0.01f;    // on a terrace the mass itself hides the seam

            bldgs.Add(b);
        }

        private static void MakeBuilding(Vector3 p, float yaw, int type, float size = 1f)
            => AddBlock(p, yaw, type, RollStyle(type, size));

        // ------------------------------------------------------------------ one block: shell, floors, corridor, stairs

        /// <summary>
        /// The interior of one block, measured before anything is built: room cells along x, the
        /// corridor band, the stair hall. The facades use the same numbers to place their windows, so
        /// an opening always sits in the middle of a room and never in front of a partition or a flight.
        /// </summary>
        private static Shell ShellOf(Bldg b, float hw, float hd)
        {
            var sh = new Shell
            {
                hw = hw,
                hd = hd,
                y0 = b.baseY,
                y1 = b.Top,
                fh = b.fh,
                dh = b.dh,
                tall = b.Tall,
                x0 = -hw + T,
                x1 = hw - T,
                zi = hd - T,
                corr = b.CorrHalf      // v0.9.13: 1.1 / 2.2 / 3.3 / 4.4 m half-width (2x/3x/4x)
            };
            sh.seg = Mathf.Max(2, Mathf.RoundToInt((sh.x1 - sh.x0) / 4.6f));
            sh.segLen = (sh.x1 - sh.x0) / sh.seg;
            sh.kS = sh.seg / 2;                       // the stair hall is the middle-north room
            sh.xs = sh.x0 + sh.kS * sh.segLen;
            PlanStair(sh);
            return sh;
        }
    }
}
