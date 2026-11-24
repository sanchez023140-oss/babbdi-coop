using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace BabbdiCoop
{
    // Part of the SovietDistrict builder - see SovietDistrict.cs.
    internal static partial class SovietDistrict
    {

        /// <summary>
        /// v0.9.16 вЂ” the stair of one block, planned before anything is built.
        ///
        /// The user's two reports were the same bug: the flights were laid out inside
        /// `StairsTall`/`Stairs` from their own constants (`corr + 1.5`, `zi в€’ corr в€’ 3`) while the
        /// slab hole was cut from another set, so a block with a **wide corridor and a shallow
        /// footprint** got a flight crammed into 1,16 m вЂ” or, once `run &lt; 2`, no stair at all вЂ” and a
        /// floor plate above it either way ("СѓРїРёСЂР°СЋС‚СЃСЏ РІ РіР»СѓС…РѕР№ РїРѕС‚РѕР»РѕРє"). And the flights themselves
        /// came out 1,2вЂ¦4,9 m long instead of the 2вЂ“3 m the user asked for.
        ///
        /// Now the plan is computed once and both the hole and the flights read it:
        ///
        ///     z:  [ wall ] corridor | arrival landing | в”Ђв”Ђ flight в”Ђв”Ђ | half-landing | [ wall ]
        ///                            stLand            stRun            stLand
        ///
        /// `stZHigh + stLand == zi` **exactly**, so the hole always ends on the outer wall and the
        /// half-landing always reaches it (no unrailed platform hanging in the room).
        /// </summary>
        private static void PlanStair(Shell sh)
        {
            const float runMin = 2.0f, runMax = 3.0f;   // the user's spec, every building
            const float landMin = 1.1f;                 // a landing you can actually stand on
            float depth = sh.zi - sh.corr;              // corridor wall в†’ facade, inside
            float run = Mathf.Clamp(depth - 2f * landMin, runMin, runMax);
            if (run > depth - 1.4f)                     // a hall too shallow for that: it wins
                run = Mathf.Max(1.2f, depth - 1.4f);
            float land = Mathf.Max(0.7f, (depth - run) * 0.5f);
            sh.stRun = run;
            sh.stLand = land;
            sh.stZLow = sh.corr + land;
            sh.stZHigh = sh.stZLow + run;
            // two flights share the storey, so each rises half of it; ~0.175 m risers like a real stair
            sh.stN = Mathf.Max(4, Mathf.RoundToInt(sh.fh * 0.5f / 0.175f));
            sh.stRise = sh.fh * 0.5f / sh.stN;
            sh.stTread = run / sh.stN;
            // v0.9.35: a real open well between the flights (the user's reference: a vertical slot
            // between the railings). It is part of the slab hole, so it runs the whole height of the
            // house; the flights keep their 2 m width.
            sh.stWell = Mathf.Clamp(sh.segLen * 0.15f, 0.55f, 0.75f);
            sh.stFw = (sh.segLen - sh.stWell) * 0.5f;

            // evidence вЂ” one line per block, against what v0.9.15 built there
            float oldRun = sh.zi - sh.corr - 3f;
            string old = oldRun < 2f
                ? $"v0.9.15 built NO stair here (old run {oldRun:0.00} m)"
                : $"old flight {oldRun:0.00} m";
            log.LogInfo($"[STAIR] {sh.hw * 2f:0.0}x{sh.hd * 2f:0.0} m, corridor {2f * sh.corr:0.0} m clear: " +
                        $"{old} -> flight {run:0.00} m, {sh.stN} risers of {sh.stRise:0.000} m, " +
                        $"tread {sh.stTread:0.000} m, landings {land:0.00} m, " +
                        $"well {sh.stWell:0.00} m (flights {sh.stFw:0.00} m), shaft z {sh.stZLow:0.00}..{sh.zi:0.00}");
        }

        /// <summary>
        /// v0.9.35 вЂ” the plan of the **street-level core** of a block on legs (`Legs`), computed before
        /// anything is built so the stair and the hole it needs in the block's first-floor slab cannot
        /// disagree. That is exactly the bug PlanStair fixed for the houses' own stairs, and it is the
        /// user's "РІ РЅРµРєРѕС‚РѕСЂС‹С… РґРѕРјР°С… РїСЂРѕР»РµС‚С‹ СѓРїРёСЂР°СЋС‚СЃСЏ РІ РїРѕС‚РѕР»РѕС‡РЅС‹Рµ РїРµСЂРµРєСЂС‹С‚РёСЏ": the core's three
        /// flights climbed 16,8 m and the last one ended **inside** the block's solid ground slab, so
        /// the houses on pylons could not be entered from the street at all.
        ///
        ///     z:  [ core wall ] | exit landing | в”Ђ flight B в”Ђ | half-landing | в”Ђ flight A в”Ђ | [ wall ]
        ///          4,31 .. 4,69    4,21 .. 2,71     2,71 .. -0,9      -0,9 .. 0,6 вЂ¦
        ///
        /// The stair now runs **northwards to its exit** (`AtticStair` with `zSign = -1`), so its exit
        /// landing lands under the block's stair hall вЂ” the middle-north room, whose first-flight foot
        /// is at the same x. `coreHoleZ0` is where the top flight's headroom would drop under 2 m.
        /// </summary>
        private static void PlanCore(Bldg b)
        {
            float cz = Mathf.Min(4.9f, b.d * 0.30f);
            const float cwt = 0.40f;
            b.coreX0 = -1.9f;
            b.coreX1 = -0.2f;
            b.coreZ0 = cz - cwt - PT * 0.5f - 0.10f;   // clear of the core's north wall
            b.coreHoleZ0 = b.coreZ0 - 4.5f;            // below this the flight has < 2 m of headroom
            b.coreHoleZ1 = b.coreZ0 + 0.02f;           // 2 cm past the landing: no coplanar slab/landing faces
            log.LogInfo($"[CORE] {b.w:0.0}x{b.d:0.0} m block on legs: street stairlane x {b.coreX0:0.00}" +
                        $"..{b.coreX1:0.00} m, exit landing z {b.coreZ0 - 1.5f:0.00}..{b.coreZ0:0.00} m, " +
                        $"first-floor opening z {b.coreHoleZ0:0.00}..{b.coreZ0:0.00} m (tops out in the " +
                        $"stair hall, 3 flights of {b.fh:0.0} m)");
        }

        /// <summary>Clear spans (building-local x) of a long facade: one per room cell, none over the
        /// stairwell (a window there looks straight into the flights).</summary>
        private static List<Vector2> LongSpans(Shell sh, bool north)
        {
            var l = new List<Vector2>(sh.seg);
            for (int k = 0; k < sh.seg; k++)
            {
                if (north && k == sh.kS) continue;
                float a = sh.x0 + k * sh.segLen + WinClear;
                float b = sh.x0 + (k + 1) * sh.segLen - WinClear;
                if (b - a > 0.9f) l.Add(new Vector2(a, b));
            }
            return l;
        }

        /// <summary>Clear spans (building-local z) of an end facade: the two room bands, split so a
        /// 38 m tower does not end up with two enormous single windows.</summary>
        private static List<Vector2> EndSpans(Shell sh)
        {
            var l = new List<Vector2>(4);
            float a = sh.corr + PT * 0.5f + WinClear;    // clear of the corridor walls
            float b = sh.zi - WinClear;
            if (b - a < 1.2f) return l;
            Columns(l, a, b);
            Columns(l, -b, -a);
            return l;
        }

        private static void Columns(List<Vector2> l, float a, float b)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt((b - a) / 4.2f), 1, 4);
            float step = (b - a) / n;
            for (int i = 0; i < n; i++) l.Add(new Vector2(a + i * step, a + (i + 1) * step));
        }

        /// <summary>Room-grid spans в†’ wall coordinates of one facade (see `Wall`'s `u`).</summary>
        private static List<Vector2> Spans(List<Vector2> src, float shift, bool mirror)
        {
            var l = new List<Vector2>(src.Count);
            for (int i = 0; i < src.Count; i++)
                l.Add(mirror ? new Vector2(shift - src[i].y, shift - src[i].x)
                             : new Vector2(src[i].x + shift, src[i].y + shift));
            return l;
        }

        private static void BuildBuilding(Bldg b)
        {
            var go = new GameObject("bldg");
            go.transform.SetParent(root, false);
            go.transform.localPosition = b.p;
            go.transform.localRotation = Quaternion.Euler(0f, b.yaw, 0f);
            b.go = go;

            var g = new Geo();
            float hw = b.w * 0.5f, hd = b.d * 0.5f;
            // v0.9.19: a block **on legs** вЂ” the first three storeys are replaced by pylons (the user's
            // reference photo 2), so the shell is built from the third floor up and the volume below is
            // left open. `base0`/`nf0` keep the block's own numbers for the doors, the legs and the
            // restore at the end of this method.
            float base0 = b.baseY;
            int nf0 = b.floors;
            int wantLegs = b.legs ? 3 : 0;
            float legsLift = wantLegs * b.fh;
            if (b.legs) { b.baseY += legsLift; b.floors -= wantLegs; PlanCore(b); }
            float y0 = b.baseY;
            float yTop = b.Top;
            int floors = b.floors;
            float fh = b.fh;

            // the interior is planned first: the windows are then cut on the real room grid
            var sh = ShellOf(b, hw, hd);
            var colsZP = Spans(LongSpans(sh, true), hw, false);    // +z: the stair-hall facade
            var colsZM = Spans(LongSpans(sh, false), hw, true);    // -z
            var colsXP = Spans(EndSpans(sh), hd, false);           // +x
            var colsXM = Spans(EndSpans(sh), hd, true);            // -x

            // plinth: a wider skirt that also hides the seam with the deck (never under a block on legs вЂ”
            // there the deck is left open between the pylons)
            if (!b.legs && b.plinth && y0 > 0.05f)
                g.Box(0, new Vector3(0f, y0 * 0.5f, 0f), new Vector3(b.w + 1.2f, y0, b.d + 1.2f), Quaternion.identity, 0.5f);

            // openings per facade: ground entrances on both short ends, bridge doorways above
            var doorsZP = new List<Vector4>();
            var doorsZM = new List<Vector4>();
            var doorsXP = new List<Vector4>();
            var doorsXM = new List<Vector4>();
            if (!b.legs)     // on legs the street entrance is the core's, under the block
            {
                float ew = b.entranceW;
                float eh = b.entranceH > 0.01f ? b.entranceH : 2.5f * b.dh;
                if (b.doorPlusX) AddDoor(doorsXP, b.d * 0.5f, y0, ew, eh);      // main entrance, +x
                if (b.doorMinusX) AddDoor(doorsXM, b.d * 0.5f, y0, ew, eh);     // main entrance, -x
            }
            // v0.9.12: a balcony door behind every balcony вЂ” the opening runs from the floor up to
            // 2.2 m, so the balcony is a real balcony and not a slab glued in front of a window. It sits
            // on the very column the window would use, so `Wall` skips that window (see `Overlaps`).
            if (b.balconies)
                for (int f = 1; f < floors; f++)
                    for (int c = 1; c < colsZP.Count; c += 2)
                    {
                        float bw = Mathf.Min(b.winW, colsZP[c].y - colsZP[c].x - 0.5f);
                        if (bw < 0.9f) continue;
                        AddDoor(doorsZP, (colsZP[c].x + colsZP[c].y) * 0.5f, y0 + f * fh, bw, 2.2f);
                    }
            if (b.endPlus != null)
                for (int f = wantLegs; f < nf0; f++)
                    if (b.endPlus[f]) AddDoor(doorsXP, b.d * 0.5f, base0 + f * fh, 2.2f, 2.75f);
            if (b.endMinus != null)
                for (int f = wantLegs; f < nf0; f++)
                    if (b.endMinus[f]) AddDoor(doorsXM, b.d * 0.5f, base0 + f * fh, 2.2f, 2.75f);

            Vector3? wp;
            wp = Wall(g, new Vector3(0f, 0f, hd), Vector3.right, Vector3.forward, b.w, y0, yTop, floors, fh,
                      b.winW, b.winH, b.sill, b.splay, b.bay, doorsZP, colsZP, b.winSkip, b.winPad, b.winPlusZ, b.winFromFloor, b.winMaskPlusZ);
            if (wp.HasValue) { b.winPos.Add(wp.Value); b.winDir.Add(Vector3.forward); }
            wp = Wall(g, new Vector3(0f, 0f, -hd), Vector3.left, Vector3.back, b.w, y0, yTop, floors, fh,
                      b.winW, b.winH, b.sill, b.splay, b.bay, doorsZM, colsZM, b.winSkip, b.winPad, b.winMinusZ, b.winFromFloor, b.winMaskMinusZ);
            if (wp.HasValue) { b.winPos.Add(wp.Value); b.winDir.Add(Vector3.back); }
            wp = Wall(g, new Vector3(hw, 0f, 0f), Vector3.forward, Vector3.right, b.d, y0, yTop, floors, fh,
                      b.winW, b.winH, b.sill, b.splay, b.bay, doorsXP, colsXP, b.winSkip, b.winPad, b.winPlusX, b.winFromFloor, b.winMaskPlusX);
            if (wp.HasValue) { b.winPos.Add(wp.Value); b.winDir.Add(Vector3.right); }
            wp = Wall(g, new Vector3(-hw, 0f, 0f), Vector3.back, Vector3.left, b.d, y0, yTop, floors, fh,
                      b.winW, b.winH, b.sill, b.splay, b.bay, doorsXM, colsXM, b.winSkip, b.winPad, b.winMinusX, b.winFromFloor, b.winMaskMinusX);
            if (wp.HasValue) { b.winPos.Add(wp.Value); b.winDir.Add(Vector3.left); }

            // entrance steps in front of both ends (a block on legs has no street-level facade)
            if (!b.legs)
            {
                // v0.9.69: the steps rise from the block's **own ground** (`y0 - BASE`) by one
                // threshold (BASE), not from y=0 by the whole `baseY`. Passing `y0` built the House
                // towers a 61 m stepped fin that covered their 1,7 m doorway вЂ” the user's "the
                // passage is still closed". On terraces the step now sits on the terrace, not at the
                // deck 8-14 m below.
                Vector3 sb = Vector3.up * (y0 - BASE);
                Steps(g, new Vector3(hw, 0f, 0f) + sb, Vector3.forward, Vector3.right, 2.6f, BASE, 3);
                Steps(g, new Vector3(-hw, 0f, 0f) + sb, Vector3.back, Vector3.left, 2.6f, BASE, 3);
            }

            if (b.interior) Interior(g, b, sh);

            // cornice / bands / balconies on the outside
            if (b.risalit)
            {
                g.Box(0, new Vector3(0f, yTop - 0.35f, 0f), new Vector3(b.w + 1.3f, 0.7f, b.d + 1.3f), Quaternion.identity, 0.4f);
                Pilasters(g, b, sh, hd, y0, yTop);
            }
            if (b.bands)
                for (int f = 1; f < floors; f++)
                {
                    // v0.9.36: the band is a **facade ring**, not a solid box. As a box it also capped
                    // the whole interior at every floor вЂ” the stairwell hole included вЂ” which is why
                    // the stairs of the point towers (and of the two blocks on legs, and the clock
                    // tower) ended 0,24 m under the floor: the user's "Р»РµСЃС‚РЅРёС†С‹ СѓРїРёСЂР°СЋС‚СЃСЏ РІ РїРµСЂРµРєСЂС‹С‚РёСЏ".
                    float y = y0 + f * fh;
                    float bw = b.w + 0.35f, bd = b.d + 0.35f;
                    const float bt = 0.35f;                 // band thickness, half in the wall
                    g.Box(0, new Vector3(0f, y - 0.12f, hd), new Vector3(bw, 0.24f, bt), Quaternion.identity, 0.5f);
                    g.Box(0, new Vector3(0f, y - 0.12f, -hd), new Vector3(bw, 0.24f, bt), Quaternion.identity, 0.5f);
                    g.Box(0, new Vector3(hw, y - 0.12f, 0f), new Vector3(bt, 0.24f, bd), Quaternion.identity, 0.5f);
                    g.Box(0, new Vector3(-hw, y - 0.12f, 0f), new Vector3(bt, 0.24f, bd), Quaternion.identity, 0.5f);
                }
            if (b.balconies) Balconies(g, b, hd, y0, colsZP);

            Roof(g, b, hw, hd, yTop);

            // v0.9.18: the penthouse of the block on the peninsula вЂ” the attic drum, the cornice, the
            // hipped metal roof (submesh 1) and the clock dials (its own small object)
            if (b.penthouse) Penthouse(g, b, hw, hd, yTop);

            // v0.9.19: the pylons, the transfer girder and the street-level core, then the block's own
            // numbers are put back вЂ” everything after this point (probes, bridges, the roof) sees the
            // building as it was declared
            if (b.legs)
            {
                Legs(g, b, hw, hd, base0, legsLift, nf0);
                b.baseY = base0;
                b.floors = nf0;
            }

            if (b.towerDetail) TowerDetail(g, b, sh, hw, hd, y0, yTop, floors, fh);
            g.Apply(go, b.wall, mMetal);
            AddMeshCollider(go, "bldg");
            builtCount++;
        }

        /// <summary>
        /// v0.9.19 вЂ” **the legs** (the user's reference photo 2): the first three storeys of a block are
        /// replaced by the pylons the house stands on. Read off the photo:
        ///  1. a **thick transfer girder** under the first floor, overhanging the footprint by 0,55 m on
        ///     every side вЂ” the block's own mass starts above it, not at the deck;
        ///  2. a ring of **splayed pylons**, each a tapered prism that starts narrow on the deck
        ///     (2,2 x 1,7 m) a metre inside the facade and widens to 3,2 x 2,0 m where it meets the
        ///     girder, so the legs lean out and the underside reads as a row of V-shaped haunches;
        ///  3. the **street-level core** between them: a concrete shaft with the block's own door on
        ///     `В±x` and a switchback stair up to the first floor, so the house is still entered from the
        ///     street вЂ” the reference's dark base under the block.
        /// Colliders are the block's own mesh collider: the legs are part of that mesh.
        /// </summary>
        private static void Legs(Geo g, Bldg b, float hw, float hd, float y0, float lift, int floors)
        {
            const float gh = 3.0f;                        // the transfer girder
            float yTop = y0 + lift;                       // the first floor (the shell's base)
            float yg = yTop - gh;
            g.Box(0, new Vector3(0f, yg + gh * 0.5f, 0f),
                  new Vector3(b.w + 1.7f, gh, b.d + 1.7f), Quaternion.identity, 0.4f);

            // the pylons: a perimeter ring, inset 2,2 m from the edges
            int nx = Mathf.Max(1, Mathf.RoundToInt((b.w - 4.4f) / 7f));
            int nz = Mathf.Max(1, Mathf.RoundToInt((b.d - 4.4f) / 7f));
            int made = 0;
            for (int i = 0; i <= nx; i++)
            {
                float x = -hw + 2.2f + (b.w - 4.4f) * i / nx;
                Pylon(g, new Vector2(x, hd - 2.2f), new Vector2(0f, 1f), y0, yg);
                Pylon(g, new Vector2(x, -hd + 2.2f), new Vector2(0f, -1f), y0, yg);
                made += 2;
            }
            for (int i = 1; i < nz; i++)
            {
                float z = -hd + 2.2f + (b.d - 4.4f) * i / nz;
                if (b.legsShut <= 0) { Pylon(g, new Vector2(hw - 2.2f, z), new Vector2(1f, 0f), y0, yg); made++; }
                if (b.legsShut >= 0) { Pylon(g, new Vector2(-hw + 2.2f, z), new Vector2(-1f, 0f), y0, yg); made++; }
            }

            // the street-level core
            float cx = Mathf.Min(3.4f, b.w * 0.18f), cz = Mathf.Min(4.9f, b.d * 0.30f);
            const float cwt = 0.40f;
            float xin = cx - cwt - 0.05f, zin = cz - cwt - 0.05f;
            foreach (int s in new[] { -1, 1 })
            {
                var n = new Vector3(s, 0f, 0f);
                var o = new Vector3(s * (cx - cwt), 0f, -cz);
                Piece(g, o, Vector3.forward, n, 0f, cz - 1.3f, y0, yTop);
                Piece(g, o, Vector3.forward, n, cz + 1.3f, 2f * cz, y0, yTop);
                Piece(g, o, Vector3.forward, n, cz - 1.3f, cz + 1.3f, y0 + 2.95f, yTop);
            }
            foreach (int s in new[] { -1, 1 })
            {
                var n = new Vector3(0f, 0f, s);
                var o = new Vector3(-cx, 0f, s * (cz - cwt));
                Piece(g, o, Vector3.right, n, 0f, 2f * cx, y0, yTop);
            }
            // three flights in the shaft, each 5,6 m, and the two landing slabs between them.
            // v0.9.35: the stair runs **southwards from the exit landing** (`zSign = -1`), so its top
            // flight arrives at the north end of the core вЂ” directly under the stair hall of the
            // block, whose first-floor slab now has the matching hole (`Interior`, `PlanCore`).
            for (int k = 0; k < 3; k++)
                AtticStair(g, y0 + k * b.fh, b.fh, b.coreX0, b.coreX1, -b.coreZ0, b.coreX1, -1f);
            for (int k = 1; k < 3; k++)
            {
                float yk = y0 + k * b.fh;
                Slab(g, -xin, xin, -zin, zin, yk - SLAB, yk,
                     new Vector4(b.coreX0, b.coreHoleZ0, b.coreX1, b.coreHoleZ1));
            }
            // the opening in the first floor is a real stairwell now, so it gets the same railings
            // as the houses' own wells (1,0 m, part 1 = the district's metal)
            float ry = yTop;
            float rz = b.coreZ0 - 1.5f;                    // the exit landing's south edge
            RailRun(g, new Vector3(b.coreX0 - 0.06f, ry, b.coreHoleZ0),
                       new Vector3(b.coreX0 - 0.06f, ry, rz), 0f, 1.0f, 0, 0f, false, ry);
            RailRun(g, new Vector3(b.coreX1 + 0.06f, ry, b.coreHoleZ0),
                       new Vector3(b.coreX1 + 0.06f, ry, rz), 0f, 1.0f, 0, 0f, false, ry);
            RailRun(g, new Vector3(b.coreX0 - 0.06f, ry, b.coreHoleZ0),
                       new Vector3(b.coreX1 + 0.06f, ry, b.coreHoleZ0), 0f, 1.0f, 0, 0f, false, ry);

            // the deck under the block is open to the sky (no sun in this level), so it needs its own light
            Lamp(b.go.transform, new Vector3(cx + 2.8f, y0 + 4.6f, 0f), 1.4f, 21f);
            Lamp(b.go.transform, new Vector3(-cx - 2.8f, y0 + 4.6f, 0f), 1.4f, 21f);
            Lamp(b.go.transform, new Vector3(0f, y0 + b.fh + 3.0f, 0f), 0.9f, 14f);

            log.LogInfo($"[LEGS] {b.w:0.0}x{b.d:0.0} m block at {b.p.x:0.0},{b.p.z:0.0}: {made} pylons " +
                        $"(1,8 x 1,5 m on the deck 2,1 m inside the facade -> 3,6 x 2,4 m at the girder), " +
                        $"girder {gh:0.0} m at y={yg:0.0}, core {2f * cx:0.0}x{2f * cz:0.0} m with the " +
                        $"street door and 3 flights, first floor y={yTop:0.0}, floors {floors} -> {floors - 3}");
        }

        /// <summary>One pylon: a tapered prism that splays outward as it rises (see `Legs`).</summary>
        private static void Pylon(Geo g, Vector2 at, Vector2 outw, float yb, float yt)
        {
            var along = new Vector2(-outw.y, outw.x);
            Frustum(g, yb, yt, at - outw * 2.1f, at + outw * 0.05f,
                    1.8f, 1.5f, 3.6f, 2.4f, along, outw);
        }

        /// <summary>
        /// A tapered prism (v0.9.19): the bottom rectangle (`wb` x `db`) at `yb` on `cb`, the top one
        /// (`wt` x `dt`) at `yt` on `ct`, with `along`/`across` its local axes. `Quad` flips the winding
        /// onto the normal, so the caller only has to hand in outward-facing normals.
        /// </summary>
        private static void Frustum(Geo g, float yb, float yt, Vector2 cb, Vector2 ct,
                                   float wb, float db, float wt, float dt, Vector2 along, Vector2 across)
        {
            Vector3 B(float u, float v) => new Vector3(
                cb.x + (along.x * u * wb + across.x * v * db) * 0.5f, yb,
                cb.y + (along.y * u * wb + across.y * v * db) * 0.5f);
            Vector3 T(float u, float v) => new Vector3(
                ct.x + (along.x * u * wt + across.x * v * dt) * 0.5f, yt,
                ct.y + (along.y * u * wt + across.y * v * dt) * 0.5f);
            var nA = new Vector3(across.x, 0f, across.y);
            var nC = new Vector3(along.x, 0f, along.y);
            g.Quad(0, B(-1f, 1f), B(1f, 1f), T(1f, 1f), T(-1f, 1f), nA, 0.4f);
            g.Quad(0, B(1f, -1f), B(-1f, -1f), T(-1f, -1f), T(1f, -1f), -nA, 0.4f);
            g.Quad(0, B(1f, 1f), B(1f, -1f), T(1f, -1f), T(1f, 1f), nC, 0.4f);
            g.Quad(0, B(-1f, -1f), B(-1f, 1f), T(-1f, 1f), T(-1f, -1f), -nC, 0.4f);
            g.Quad(0, B(-1f, -1f), B(1f, -1f), B(1f, 1f), B(-1f, 1f), Vector3.down, 0.4f);
        }

        private static void Interior(Geo g, Bldg b, Shell sh)
        {
            float fh = sh.fh;
            float x0 = sh.x0, x1 = sh.x1;        // interior faces of the short walls
            float zi = sh.zi;                    // interior face of the long walls
            float corr = sh.corr;
            int seg = sh.seg;
            float segLen = sh.segLen;
            int kS = sh.kS;                      // the stair hall is the middle-north room
            float xs = sh.xs;
            float land = sh.stLand;              // landing depth at both ends of the flights (PlanStair)
            // v0.9.11: the stalinka's openings are +30 % taller (its storey is 5.6 m)
            float doorH = 2.15f * sh.dh;
            float stairDoorH = 2.2f * sh.dh;
            float crossDoorH = 2.1f * sh.dh;
            b.stairX0 = xs;
            b.stairX1 = xs + segLen;
            b.stairZ0 = corr;
            b.stairZ1 = zi;
            b.stairLand = sh.stLand;
            b.stairRun = sh.stRun;
            b.stairWell = sh.stWell;
            b.stairFw = sh.stFw;

            for (int f = 0; f < b.floors; f++)
            {
                float yf = sh.y0 + f * fh;
                // floor slab with the stair shaft cut out; the arrival landing stays solid, so the
                // top of the upper flight always ends on real floor (see PlanStair, v0.9.16)
                if (f > 0)
                    Slab(g, x0, x1, -zi, zi, yf - SLAB, yf,
                         new Vector4(xs, corr + land, xs + segLen, zi));
                else if (b.legs)   // v0.9.35: the street core's stair tops out through this slab
                    Slab(g, x0, x1, -zi, zi, yf - SLAB, yf,
                         new Vector4(b.coreX0, b.coreHoleZ0, b.coreX1, b.coreHoleZ1));
                else
                    Slab(g, x0, x1, -zi, zi, yf - SLAB, yf, null);

                // corridor walls: a door into every room, a wide doorway into the stair hall
                for (int s = -1; s <= 1; s += 2)
                {
                    float z = s * corr;
                    Vector3 o = new Vector3(x0, 0f, z);
                    Vector3 u = Vector3.right;
                    Vector3 n = Vector3.Cross(Vector3.up, u);
                    for (int k = 0; k < seg; k++)
                    {
                        float xa = k * segLen;
                        if (s > 0 && k == kS)
                        {
                            // v0.9.35: a block on legs also needs a doorway here, at the west end of
                            // the stair hall, for its street core's stair вЂ” the exit landing sits
                            // under this wall, so a wall across it would seal the stair off
                            if (b.legs && f == 0)
                            {
                                float ca = b.coreX0 - x0, cb = b.coreX1 - x0;
                                Piece(g, o, u, n, xa, ca, yf, yf + fh);
                                Piece(g, o, u, n, ca, cb, yf + stairDoorH, yf + fh);
                                Piece(g, o, u, n, cb, xa + segLen - 1.85f, yf, yf + fh);
                            }
                            else
                                Piece(g, o, u, n, xa, xa + segLen - 1.85f, yf, yf + fh);
                            Piece(g, o, u, n, xa + segLen - 0.05f, xa + segLen, yf, yf + fh);
                            Piece(g, o, u, n, xa + segLen - 1.85f, xa + segLen - 0.05f, yf + stairDoorH, yf + fh);
                        }
                        else
                        {
                            float c = xa + segLen * 0.5f;
                            Piece(g, o, u, n, xa, c - 0.65f, yf, yf + fh);
                            Piece(g, o, u, n, c + 0.65f, xa + segLen, yf, yf + fh);
                            Piece(g, o, u, n, c - 0.65f, c + 0.65f, yf + doorH, yf + fh);
                        }
                    }
                }

                // cross walls: split the rooms along the row (with a door, so the rooms connect);
                // the two beside the stair hall are solid вЂ” they are its side walls.
                // v0.9.11: no partitions flush with the end walls (k = 0 and k = seg): they filled the
                // whole first and last room with a blank wall right behind the end windows
                for (int s = -1; s <= 1; s += 2)
                {
                    float za = s * corr, zb = s * zi;
                    float len = Mathf.Abs(zb - za);
                    Vector3 u = Vector3.forward;
                    Vector3 n = Vector3.Cross(Vector3.up, u);
                    for (int k = 1; k < seg; k++)
                    {
                        Vector3 o = new Vector3(x0 + k * segLen, 0f, Mathf.Min(za, zb));
                        bool solid = s > 0 && (k == kS || k == kS + 1);
                        if (solid) { Piece(g, o, u, n, 0f, len, yf, yf + fh); continue; }
                        Piece(g, o, u, n, 0f, len * 0.5f - 0.65f, yf, yf + fh);
                        Piece(g, o, u, n, len * 0.5f + 0.65f, len, yf, yf + fh);
                        Piece(g, o, u, n, len * 0.5f - 0.65f, len * 0.5f + 0.65f, yf + crossDoorH, yf + fh);
                    }
                }
            }

            // stairs: one switchback per storey, inside the stair hall (same plan as the slab hole)
            for (int f = 0; f + 1 < b.floors; f++)
                StairsZ(g, sh, xs, sh.y0 + f * fh);
        }

        private static float FH01(int x, int y, int s)
        {
            int h = x * 374761393 + y * 668265263 + s * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0x7fffffff) / 2147483647f;
        }
    }
}
