using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace BabbdiCoop
{
    // Part of the SovietDistrict builder - see SovietDistrict.cs.
    internal static partial class SovietDistrict
    {

        /// <summary>Building-local sample point above the arrival landing of the first storey.</summary>
        private static Vector3 StairLanding(Bldg b)
        {
            // v0.9.16: every stair runs along z (StairsZ), so the arrival landing is the strip next to
            // the corridor doorway and the half-landing is the one against the outer wall
            return new Vector3((b.stairX0 + b.stairX1) * 0.5f, b.baseY + b.fh + 1.2f,
                               b.stairZ0 + b.stairLand * 0.5f);
        }

        /// <summary>Building-local sample point above the half-landing.</summary>
        private static Vector3 StairMid(Bldg b)
        {
            return new Vector3((b.stairX0 + b.stairX1) * 0.5f, b.baseY + b.fh * 0.5f + 1.2f,
                               b.stairZ1 - b.stairLand * 0.5f);
        }

        /// <summary>
        /// v0.9.16 вЂ” the user's "the stairs run into a blind ceiling". A ray straight **up** from just
        /// above each flight must leave the shaft: the same hole is cut in every floor slab, so a hit
        /// roughly one storey above means a solid plate over the flights. The arrival landing and the
        /// half-landing are checked separately (`stair landing`/`half landing` drops) вЂ” those *are*
        /// meant to be solid, they are the floor you walk on.
        /// </summary>
        private static void StairProbes(Bldg b, string tag)
        {
            var rot = Quaternion.Euler(0f, b.yaw, 0f);
            float fh = b.fh;
            float w = b.stairX1 - b.stairX0;
            float zMid = b.stairZ0 + b.stairLand + b.stairRun * 0.5f;
            float y = b.baseY + fh + 0.45f;                 // in the open shaft above the 1st floor
            System.Func<Vector3, string> up = local =>
            {
                Vector3 o = root.TransformPoint(b.p + rot * local);
                if (!Physics.Raycast(o, Vector3.up, out var h, fh * 2f - 0.5f, ~0, QueryTriggerInteraction.Ignore))
                    return $"OPEN over 2 storeys ({fh * 2f - 0.5f:0.0} m checked)";
                return string.Format("{0:0.00} m above the shaft floor -> {1}", h.point.y - b.baseY - fh, h.collider.name);
            };
            log.LogInfo($"[PROBE] {tag} stair flight 1 ({b.stairRun:0.00} m long) above: " +
                        up(new Vector3(b.stairX0 + w * 0.25f, y, zMid)));
            log.LogInfo($"[PROBE] {tag} stair flight 2 above: " +
                        up(new Vector3(b.stairX0 + w * 0.75f, y, zMid)));
        }

        /// <summary>
        /// v0.9.16 вЂ” headroom over every flight of every block, measured. A vertical ray from 1,84 m
        /// above the middle tread of each flight must be clear for another 0,2 m (a 2 m person); the
        /// shaft is cut through every slab, so nothing may be hit at all. This is the numeric form of
        /// the user's "the stairs run into a blind ceiling".
        /// </summary>
        private static void Headroom()
        {
            int blocks = 0, rays = 0, blocked = 0;
            float least = 99f;
            string leastWhat = "";
            bool dumped = false;
            foreach (var b in bldgs)
            {
                if (!b.interior) continue;
                var rot = Quaternion.Euler(0f, b.yaw, 0f);
                float zLow = b.stairZ0 + b.stairLand;
                float xm1 = b.stairX0 + (b.stairX1 - b.stairX0) * 0.25f;
                float xm2 = b.stairX0 + (b.stairX1 - b.stairX0) * 0.75f;
                int worst = -1;
                float blockMin = 99f;
                string blockWhat = "";
                // v0.9.35: a block on legs has no shell below its first floor вЂ” its own stairs start
                // there, so sample from that storey (the street core is checked separately below)
                int f0 = b.legs ? 3 : 0;
                for (int f = f0; f + 1 < b.floors && f < f0 + 3; f++)
                    for (int s = 0; s < 2; s++)
                        for (int i = 1; i <= 8; i++)
                        {
                            float t = i / 8f;
                            // flight 1 rises with +z (yf .. yf+fh/2), flight 2 rises back towards -z
                            // (yf+fh/2 .. yf+fh) вЂ” v0.9.36: the old formula used flight 1's profile for
                            // both, so "flight 2" was sampled *inside* the stairs themselves
                            float y = b.baseY + f * b.fh +
                                      (s == 0 ? b.fh * 0.5f * t : b.fh * (1f - 0.5f * t)) + 1.84f;
                            Vector3 o = root.TransformPoint(b.p + rot * new Vector3(
                                s == 0 ? xm1 : xm2, y, zLow + b.stairRun * t));
                            rays++;
                            // v0.9.36: measure the **real** clearance to the first thing above, not a
                            // 0,2 m sniff вЂ” the user's "the flight butts into the slab" needs a number
                            float clear = 5.6f;
                            string what = "nothing within 5,6 m";
                            if (Physics.Raycast(o, Vector3.up, out var hh, 5.6f, ~0, QueryTriggerInteraction.Ignore))
                            {
                                clear = 1.84f + hh.distance;
                                what = hh.collider.name;
                                if (clear < 2.05f) { blocked++; if (worst < 0) worst = f; }
                            }
                            if (clear < blockMin)
                            {
                                blockMin = clear;
                                blockWhat = $"storey {f}, flight {(s == 0 ? 1 : 2)}, tread {i}/8, " +
                                            $"{what} at {clear:0.00} m above it";
                            }
                            if (clear < least) { least = clear; leastWhat = $"{b.w:0.0}x{b.d:0.0} m block at " +
                                                $"{b.p.x:0.0},{b.p.z:0.0}: {blockWhat}"; }
                        }
                blocks++;
                if (worst >= 0)
                    log.LogWarning($"[STAIR] headroom below 2 m over a flight: block {b.w:0.0}x{b.d:0.0} m " +
                                   $"at {b.p.x:0.0},{b.p.z:0.0}, first bad storey {worst}");
                if (blockMin < 4.0f)
                    log.LogWarning($"[STAIR] low ceiling over a flight: {b.w:0.0}x{b.d:0.0} m block at " +
                                   $"{b.p.x:0.0},{b.p.z:0.0}: {blockMin:0.00} m ({blockWhat})");
                if (blockMin < 1.9f && !dumped)
                {
                    dumped = true;
                    log.LogInfo($"[STAIR] geometry above flight 2 of {b.w:0.0}x{b.d:0.0} m at " +
                                $"{b.p.x:0.0},{b.p.z:0.0} (corr {b.CorrHalf:0.00}, zLow " +
                                $"{b.stairZ0 + b.stairLand:0.00}, zHigh {b.stairZ0 + b.stairLand + b.stairRun:0.00}, " +
                                $"zi {b.stairZ1:0.00}, fh {b.fh:0.0}):");
                    for (int s = 0; s < 2; s++)
                        for (int i = 1; i <= 4; i++)
                        {
                            float t = i / 4f;
                            float y = b.baseY + 2 * b.fh +
                                      (s == 0 ? b.fh * 0.5f * t : b.fh * (1f - 0.5f * t)) + 1.84f;
                            Vector3 o = root.TransformPoint(b.p + rot * new Vector3(
                                s == 0 ? xm1 : xm2, y, zLow + b.stairRun * t));
                            string r = "nothing within 5,6 m";
                            if (Physics.Raycast(o, Vector3.up, out var hh, 5.6f, ~0, QueryTriggerInteraction.Ignore))
                            {
                                Vector3 lp = Quaternion.Inverse(rot) * (root.InverseTransformPoint(hh.point) - b.p);
                                r = $"{(1.84f + hh.distance):0.00} m -> {hh.collider.name} at local " +
                                    $"({lp.x:0.00}, {lp.y:0.00}, {lp.z:0.00}), origin y {y:0.00}";
                            }
                            log.LogInfo($"[STAIR]   flight {(s == 0 ? 1 : 2)} t={t:0.00} (z {zLow + b.stairRun * t:0.00}, " +
                                        $"y {y:0.00}): {r}");
                        }
                }
                if (blocks == 1)   // v0.9.36: one block in full, so the geometry above a flight is named
                    for (int s = 0; s < 2; s++)
                        for (int i = 1; i <= 4; i++)
                        {
                            float t = i / 4f;
                            float y = b.baseY + 1 * b.fh +
                                      (s == 0 ? b.fh * 0.5f * t : b.fh * (1f - 0.5f * t)) + 1.84f;
                            Vector3 o = root.TransformPoint(b.p + rot * new Vector3(
                                s == 0 ? xm1 : xm2, y, zLow + b.stairRun * t));
                            string r = "nothing within 5,6 m";
                            if (Physics.Raycast(o, Vector3.up, out var hh, 5.6f, ~0, QueryTriggerInteraction.Ignore))
                                r = $"{(1.84f + hh.distance):0.00} m above the tread -> {hh.collider.name} " +
                                    $"at y+{hh.point.y - (b.baseY + b.fh):0.00}";
                            log.LogInfo($"[STAIR] above flight {(s == 0 ? 1 : 2)}, storey 1, t={t:0.00}: {r}");
                        }
            }
            log.LogInfo($"[STAIR] headroom: {blocks} blocks, {rays} rays, {blocked} under 2,05 m; " +
                        $"least clearance {least:0.00} m ({leastWhat})");

            // v0.9.35: the street cores of the blocks on legs. A ray up from just under the block's
            // first floor, inside the core's stair lane, must leave the shaft: before this the last
            // flight ended **inside** the solid slab (`coreHoleZ0` is where the headroom drops under
            // 2 m, so the ray there has to pass through the new hole).
            foreach (var b in bldgs)
            {
                if (!b.legs) continue;
                var rot = Quaternion.Euler(0f, b.yaw, 0f);
                float yTop = b.baseY + 3f * b.fh;
                float x = (b.coreX0 + b.coreX1) * 0.5f;
                for (int i = 0; i < 2; i++)
                {
                    float z = b.coreHoleZ0 + 0.6f + i * 1.4f;
                    Vector3 o = root.TransformPoint(b.p + rot * new Vector3(x, yTop - 0.32f, z));
                    string r;
                    if (!Physics.Raycast(o, Vector3.up, out var hit, 1.2f, ~0, QueryTriggerInteraction.Ignore))
                        r = "OPEN through the first-floor opening (1,2 m)";
                    else
                        r = string.Format("{0:0.00} m over the stair -> {1}", hit.point.y - (yTop - 0.32f),
                                          hit.collider.name);
                    log.LogInfo($"[PROBE] legs core stair under the first floor at z {z:0.0}: {r}");
                }
            }
            StairSkin();
        }

        /// <summary>
        /// v0.9.40 вЂ” the user's "прозрачная текстура" seen from below a flight. A vertical ray from just
        /// above the floor **under a flight** must hit that flight's soffit; if it passes through, the
        /// plate really has a hole, and the log says exactly where (flight, position along the run, the
        /// height the soffit should have, and what the ray hit instead).
        /// </summary>
        private static void StairSkin()
        {
            int n = 0, through = 0, wrong = 0;
            foreach (var b in bldgs)
            {
                if (!b.interior) continue;
                var rot = Quaternion.Euler(0f, b.yaw, 0f);
                float y0 = b.baseY + (b.legs ? 3f * b.fh : 0f);   // the house's own first floor
                float zLow = b.stairZ0 + b.stairLand, run = b.stairRun;
                float fw = b.stairFw, well = b.stairWell;
                float slope = b.fh * 0.5f / run;
                for (int s = 0; s < 2; s++)
                {
                    float x = s == 0 ? b.stairX0 + fw * 0.5f : b.stairX0 + fw + well + fw * 0.5f;
                    for (int i = 1; i <= 10; i++)
                    {
                        float t = i / 11f;
                        float z = zLow + run * t;
                        // flight 1 rises with +z, flight 2 comes back towards -z
                        float ySof = s == 0
                            ? slope * run * t - StairPlate
                            : b.fh * 0.5f + slope * run * (1f - t) - StairPlate;
                        if (ySof < 0.30f) continue;                 // still inside the floor slab
                        Vector3 o = root.TransformPoint(b.p + rot * new Vector3(x, y0 + 0.28f, z));
                        n++;
                        if (!Physics.Raycast(o, Vector3.up, out var h, 6.5f, ~0, QueryTriggerInteraction.Ignore))
                        {
                            through++;
                            log.LogWarning($"[SKIN] {b.w:0.0}x{b.d:0.0} at {b.p.x:0.0},{b.p.z:0.0}: flight {s + 1}, " +
                                           $"{t:0.00} of the run — **nothing** over the soffit " +
                                           $"(expected at {ySof:0.00} m): a hole in the plate");
                            continue;
                        }
                        float hy = root.InverseTransformPoint(h.point).y - y0;
                        if (hy < ySof - 0.30f || hy > ySof + 0.55f)
                        {
                            wrong++;
                            log.LogWarning($"[SKIN] {b.w:0.0}x{b.d:0.0} at {b.p.x:0.0},{b.p.z:0.0}: flight {s + 1}, " +
                                           $"{t:0.00} of the run — hit {hy:0.00} m ({h.collider.name}), " +
                                           $"the soffit is at {ySof:0.00} m");
                        }
                    }
                }
            }
            log.LogInfo($"[SKIN] stair undersides: {n} vertical rays under the flights, {through} passed through, " +
                        $"{wrong} hit the wrong surface");
            DoorHeads();
        }

        /// <summary>
        /// v0.9.41 вЂ” the user's "прозрачная текстура" over every doorway: `Piece` used to draw a wall
        /// segment as two faces plus its two jambs, with **no head and no sill**, so a lintel was an open
        /// slot through the wall's thickness and looking up at a door showed the neighbouring room's
        /// ceiling as a thin dark strip. A ray from just under a door head must hit that head immediately.
        /// </summary>
        private static void DoorHeads()
        {
            var b = bldgs.Find(x => x.interior);
            if (b == null) return;
            var rot = Quaternion.Euler(0f, b.yaw, 0f);
            float fh = b.fh, corr = b.CorrHalf, segLen = b.stairX1 - b.stairX0;
            int ok = 0, open = 0;
            for (int f = 0; f < 2 && f + 1 < b.floors; f++)
                for (int s = -1; s <= 1; s += 2)
                    foreach (float x in new[] { s > 0 ? b.stairX0 + segLen - 0.95f : b.stairX0 + segLen * 0.5f })
                    {
                        Vector3 o = root.TransformPoint(b.p + rot * new Vector3(x, b.baseY + f * fh + 2.70f,
                                                                                 s * corr));
                        if (Physics.Raycast(o, Vector3.up, out var h, 1.2f, ~0, QueryTriggerInteraction.Ignore)
                            && h.distance < 0.35f) ok++;
                        else
                        {
                            open++;
                            log.LogWarning($"[DOOR] open head over a doorway at {x:0.00},{s * corr:0.00} " +
                                           $"storey {f}: " +
                                           (h.collider != null ? $"{h.distance:0.00} m -> {h.collider.name}" : "nothing"));
                        }
                    }
            log.LogInfo($"[DOOR] door heads: {ok + open} rays just under a lintel, {ok} hit the head, {open} open");
            SeamScan();
        }

        /// <summary>
        /// v0.9.44 вЂ” "the small dark structure on the dam": list the level's **own** meshes that sit near
        /// the crest line (district-local z в€’80вЂ¦+30), so the one the user means can be named and hidden.
        /// </summary>
        private static void SeamScan()
        {
            if (root == null) return;
            int shown = 0;
            foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (r == null || r.transform == null) continue;
                if (!r.gameObject.scene.IsValid()) continue;              // scene objects only
                if (r.transform.IsChildOf(root)) continue;                // our own district
                var b = r.bounds;
                Vector3 c = root.InverseTransformPoint(b.center);
                if (c.z < -40f || c.z > 40f || Mathf.Abs(c.x) > 470f) continue;
                // only things that actually reach the crest band (local y в€’6вЂ¦+6, z в€’25вЂ¦+25)
                var lo = root.InverseTransformPoint(b.min);
                var hi = root.InverseTransformPoint(b.max);
                if (hi.y < -6f || lo.y > 6f || hi.z < -25f || lo.z > 25f) continue;
                int verts = -1;
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) verts = mf.sharedMesh.vertexCount;
                log.LogInfo($"[SEAM] {r.name} [{r.GetType().Name}]: local ({c.x:0.0}, {c.y:0.0}, {c.z:0.0}), size " +
                            $"({b.size.x:0.0}, {b.size.y:0.0}, {b.size.z:0.0}), y {lo.y:0.0}..{hi.y:0.0}, " +
                            $"z {lo.z:0.0}..{hi.z:0.0}, verts {verts}");
                if (++shown > 80) break;
            }
            log.LogInfo($"[SEAM] listed {shown} level renderers near the crest line");
            int mine = 0;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var b = r.bounds;
                Vector3 c = root.InverseTransformPoint(b.center);
                if (c.z < -10f || c.z > 30f) continue;
                var lo = root.InverseTransformPoint(b.min);
                var hi = root.InverseTransformPoint(b.max);
                if (hi.y < -3f || lo.y > 4f) continue;
                log.LogInfo($"[SEAM-OURS] {r.name}: local ({c.x:0.0}, {c.y:0.0}, {c.z:0.0}), size " +
                            $"({b.size.x:0.0}, {b.size.y:0.0}, {b.size.z:0.0}), y {lo.y:0.0}..{hi.y:0.0}, " +
                            $"z {lo.z:0.0}..{hi.z:0.0}");
                if (++mine > 40) break;
            }
            log.LogInfo($"[SEAM-OURS] listed {mine} of our meshes in the crest band");
            CornerMeshScan();
        }

        /// <summary>
        /// v0.9.51 вЂ” the user's corner shot: something of ours (or of the level) protrudes past the end
        /// of the vertical wall at the south-east corner. List every mesh вЂ” ours and the level's вЂ” whose
        /// bounds reach that corner, so the offender is named with numbers instead of guessed.
        /// </summary>
        private static void CornerMeshScan()
        {
            if (root == null) return;
            // the district is rotated, so a world AABB does NOT give the local extents: take the
            // eight corners of the world bounds, transform each, and rebuild the box in local space
            System.Func<Bounds, Vector3, Vector3> localBox = (b, unused) => Vector3.zero;
            int n = 0;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var b = r.bounds;
                Vector3 lo = new Vector3(1e4f, 1e4f, 1e4f), hi = new Vector3(-1e4f, -1e4f, -1e4f);
                for (int i = 0; i < 8; i++)
                {
                    var w = new Vector3(
                        (i & 1) == 0 ? b.min.x : b.max.x,
                        (i & 2) == 0 ? b.min.y : b.max.y,
                        (i & 4) == 0 ? b.min.z : b.max.z);
                    var p = root.InverseTransformPoint(w);
                    lo = Vector3.Min(lo, p);
                    hi = Vector3.Max(hi, p);
                }
                if (hi.x < 320f || lo.x > 392f) continue;
                if (hi.z < -10f || lo.z > 30f) continue;
                if (hi.y < -3f || lo.y > 10f) continue;
                log.LogInfo($"[CORNER] ours {r.name}: x {lo.x:0.0}..{hi.x:0.0}, y {lo.y:0.0}..{hi.y:0.0}, " +
                            $"z {lo.z:0.0}..{hi.z:0.0}");
                if (++n > 40) break;
            }
            int lv = 0;
            foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (r == null || r.transform == null) continue;
                if (!r.gameObject.scene.IsValid()) continue;
                if (r.transform.IsChildOf(root)) continue;
                var b = r.bounds;
                Vector3 lo = new Vector3(1e4f, 1e4f, 1e4f), hi = new Vector3(-1e4f, -1e4f, -1e4f);
                for (int i = 0; i < 8; i++)
                {
                    var w = new Vector3(
                        (i & 1) == 0 ? b.min.x : b.max.x,
                        (i & 2) == 0 ? b.min.y : b.max.y,
                        (i & 4) == 0 ? b.min.z : b.max.z);
                    var p = root.InverseTransformPoint(w);
                    lo = Vector3.Min(lo, p);
                    hi = Vector3.Max(hi, p);
                }
                if (hi.x < 300f || lo.x > 410f) continue;
                if (hi.z < -20f || lo.z > 45f) continue;
                if (hi.y < -20f || lo.y > 80f) continue;
                log.LogInfo($"[CORNER] level {r.name}: x {lo.x:0.0}..{hi.x:0.0}, y {lo.y:0.0}..{hi.y:0.0}, " +
                            $"z {lo.z:0.0}..{hi.z:0.0}");
                if (++lv > 40) break;
            }
            log.LogInfo($"[CORNER] listed {n} of our meshes and {lv} level renderers at the south-east corner");
        }

        public static void ProbeWalkability()        {
            if (root == null) return;
            int ok = 0, bad = 0;
            System.Func<Vector3, float, float, string> down = (local, from, len) =>
            {
                Vector3 w = root.TransformPoint(local) + Vector3.up * from;
                if (!Physics.Raycast(w, Vector3.down, out var h, len, ~0, QueryTriggerInteraction.Ignore))
                    return "MISS";
                return string.Format("{0:0.00} m ({1})", h.point.y - root.position.y, h.collider.name);
            };
            System.Action<string, string> report = (what, hit) =>
            {
                bool good = hit != "MISS";
                if (good) ok++; else bad++;
                log.LogInfo($"[PROBE] {what}: {hit}");
            };
            // v0.9.57: the same ray, upward вЂ” the House's horizontal plates are checked from both sides
            System.Func<Vector3, float, float, string> up = (local, from, len) =>
            {
                Vector3 w = root.TransformPoint(local) + Vector3.up * from;
                if (!Physics.Raycast(w, Vector3.up, out var h, len, ~0, QueryTriggerInteraction.Ignore))
                    return "MISS";
                return string.Format("{0:0.00} m ({1})", h.point.y - root.position.y, h.collider.name);
            };
            // v0.9.21: the height of the first surface under a district-local point, for the corner's
            // numeric profile (a step shows up as a jump between neighbouring samples)
            System.Func<Vector3, string> Col = local =>
            {
                if (!Physics.Raycast(root.TransformPoint(local), Vector3.down, out var h, 60f, ~0,
                                     QueryTriggerInteraction.Ignore))
                    return "MISS";
                return string.Format("{0:0.00}", h.point.y - root.position.y);
            };
            // v0.9.12: a ray in an arbitrary direction, given in the district's local space
            System.Func<Vector3, Vector3, float, string> shoot = (localFrom, localDir, len) =>
            {
                if (!Physics.Raycast(root.TransformPoint(localFrom), root.rotation * localDir,
                                     out var h, len, ~0, QueryTriggerInteraction.Ignore))
                    return "MISS";
                return string.Format("{0:0.00} m ({1})", h.distance, h.collider.name);
            };

            if (bldgs.Count > 0)
            {
                var b = bldgs[0];
                var rot = Quaternion.Euler(0f, b.yaw, 0f);
                float fh = b.fh;
                report("street floor", down(b.p + rot * new Vector3(0f, 0f, b.d * 0.5f + 6f), 2f, 6f));
                for (int f = 0; f < Mathf.Min(3, b.floors); f++)
                    report($"house floor {f}", down(b.p + rot * new Vector3(0f, b.baseY + f * fh + 1.2f, 0f), 0.5f, 3f));
                report("stair landing 1", down(b.p + rot * StairLanding(b), 0.5f, 3f));
                report("half landing", down(b.p + rot * StairMid(b), 0.5f, 3f));
                var mc = b.go != null ? b.go.GetComponentInChildren<MeshCollider>() : null;
                var bm = mc != null ? mc.sharedMesh : null;
                log.LogInfo("[PROBE] bldg mesh: " + (bm == null ? "no collider" :
                    $"{bm.vertexCount} verts, {bm.subMeshCount} subs, tris0 {bm.GetTriangles(0).Length / 3}" +
                    (bm.subMeshCount > 1 ? $", tris1 {bm.GetTriangles(1).Length / 3}" : "") + $", convex {mc.convex}"));
                WindowProbes(b, "house 0");
                StairProbes(b, "house 0");
                // v0.9.13: the corridor is really 2x/3x/4x wide вЂ” measured across the hall, not read
                // from the config. The rays run 4.6 m above the floor: below that they would pass
                // through room doors (2.15 m) or out of a window (sill 1.1 m) and never hit a wall
                float cy = b.baseY + 2f * fh + 4.6f;
                report($"corridor {b.corrMul}x (={2f * b.CorrHalf:0.0} m clear) +z",
                    shoot(b.p + rot * new Vector3(0f, cy, 0f), rot * Vector3.forward, 12f));
                report($"corridor {b.corrMul}x -z",
                    shoot(b.p + rot * new Vector3(0f, cy, 0f), rot * Vector3.back, 12f));
            }
            // v0.9.12: every block now has the same tall storey, so the second sample is simply the next
            // building вЂ” it proves the layout is not a one-off (different width, different floor count)
            if (bldgs.Count > 1)
            {
                var s = bldgs[1];
                var sr = Quaternion.Euler(0f, s.yaw, 0f);
                float sh = s.fh;
                const string tag = "house 1";
                report(tag + " corridor floor", down(s.p + sr * new Vector3(0f, s.baseY + 2f * sh + 1.2f, 0f), 0.5f, 3f));
                report(tag + " stair landing", down(s.p + sr * StairLanding(s), 0.5f, 3f));
                report(tag + " half landing", down(s.p + sr * StairMid(s), 0.5f, 3f));
                report(tag + $" corridor {s.corrMul}x (={2f * s.CorrHalf:0.0} m clear)",
                    shoot(s.p + sr * new Vector3(0f, s.baseY + 2f * sh + 4.6f, 0f), sr * Vector3.forward, 12f));
                WindowProbes(s, tag);
                StairProbes(s, tag);
            }
            // v0.9.16: the block whose stair has the least room вЂ” the case that used to have no
            // flights at all (wide corridor + shallow footprint) or a plate over them
            var tight = bldgs.Find(x => x.interior && x.stairRun <= 2.05f);
            if (tight != null)
            {
                var tr = Quaternion.Euler(0f, tight.yaw, 0f);
                var tm = (tight.stairX0 + tight.stairX1) * 0.5f;
                log.LogInfo($"[PROBE] tightest stair: flight {tight.stairRun:0.00} m, landings " +
                            $"{tight.stairLand:0.00} m, corridor {2f * tight.CorrHalf:0.0} m clear");
                report("tight stair arrival landing",
                    down(tight.p + tr * new Vector3(tm, tight.baseY + tight.fh + 1.2f, tight.stairZ0 + tight.stairLand * 0.5f), 0.5f, 3f));
                report("tight stair half-landing",
                    down(tight.p + tr * new Vector3(tm, tight.baseY + tight.fh * 0.5f + 1.2f, tight.stairZ1 - tight.stairLand * 0.5f), 0.5f, 3f));
                StairProbes(tight, "tightest stair");
            }
            // v0.9.12: a balcony must physically exist in front of the wall вЂ” shoot from 6 m out at the
            // height of the first balcony: the parapet must stop the ray at ~4.85 m (1.15 m of
            // projection), the wall alone would be 6 m. Then drop onto the slab from above it.
            var bb = bldgs.Find(x => x.hasBalc);
            if (bb != null)
            {
                var rb = Quaternion.Euler(0f, bb.yaw, 0f);
                report("balcony parapet (1,15 m out)",
                    shoot(bb.p + rb * (bb.balcSample + new Vector3(0f, 0f, 6f)), rb * Vector3.back, 9f));
                report("balcony slab",
                    down(bb.p + rb * (bb.balcSample + new Vector3(0f, -0.5f, 0f)), 2f, 4f));
                var bmc = bb.go != null ? bb.go.GetComponentInChildren<MeshCollider>() : null;
                var bmm = bmc != null ? bmc.sharedMesh : null;
                log.LogInfo("[PROBE] balcony bldg mesh: " + (bmm == null ? "none" :
                    $"{bmm.vertexCount} verts, {bmm.subMeshCount} subs, {bmm.indexFormat}"));
                // v0.9.12: a horizontal scan across the +z facade, 3 m out, at parapet height and at
                // window height вЂ” 1,9 m = a balcony rail, 3,0 m = plain wall, more = the ray went
                // through an opening. Proves where the stacks sit in x, without trusting a render.
                float hdz = bb.d * 0.5f;
                float[] scanY = { bb.baseY + 2f * bb.fh + 0.5f, bb.baseY + 2f * bb.fh + 1.6f };
                foreach (float sy in scanY)
                {
                    var sb = new System.Text.StringBuilder();
                    for (float x = -bb.w * 0.5f; x <= bb.w * 0.5f + 0.01f; x += 1f)
                    {
                        string s = shoot(bb.p + rb * new Vector3(x, sy, hdz + 3f), rb * Vector3.back, 30f);
                        int sp = s.IndexOf(' ');
                        sb.Append(s == "MISS" ? "  .  " : s.Substring(0, sp).PadLeft(5) + " ");
                    }
                    log.LogInfo($"[PROBE] facade scan y={sy:0.0} (x {-bb.w * 0.5f:0.0}..{bb.w * 0.5f:0.0}, " +
                                $"hd={hdz:0.0}): {sb}");
                }
            }
            // v0.9.19: the two blocks on legs вЂ” the deck between the pylons must be walkable, the house
            // must really start on the girder three storeys up, and the girder must overhang the facade.
            // Measured instead of trusted: a ray straight down under the block, a ray down onto the first
            // floor, a ray across the core's stair landing and a horizontal ray into the girder.
            var lg = bldgs.Find(x => x.legs);
            if (lg != null)
            {
                var lr = Quaternion.Euler(0f, lg.yaw, 0f);
                float lift = 3f * lg.fh;
                float cxl = Mathf.Min(3.4f, lg.w * 0.18f);
                Vector3 under = lg.p + lr * new Vector3(cxl + 4.6f, 0f, 0f);
                report("under a block on legs (deck)", down(under, 5f, 9f));
                report("first floor over the legs", down(under + Vector3.up * (lift + 0.6f), 0.5f, 4f));
                report("core stair landing 1",
                    down(lg.p + lr * new Vector3(1.6f, lg.fh + 1.2f, 0f), 0.5f, 4f));
                report("transfer girder",
                    shoot(lg.p + lr * new Vector3(0f, lift - 1.5f, lg.d * 0.5f + 9f), lr * Vector3.back, 14f));
                log.LogInfo($"[PROBE] legs block: {lg.w:0.0}x{lg.d:0.0} m at {lg.p.x:0.0},{lg.p.z:0.0}, " +
                            $"first floor {lg.baseY + lift:0.0} m, the deck open between the pylons");
            }
            // v0.9.21: the corner's surface as a **numeric profile** instead of a render вЂ” a ray every
            // 1,5 m from the crest across the tongue, the rise and the flat plate (z), and every 1,5 m
            // down the west bevel (x). Two neighbouring samples may differ by a couple of centimetres
            // (a 2-4 % slope), never by a step.
            {
                var sbp = new System.Text.StringBuilder();
                for (float z = -2f; z <= 14.01f; z += 1.5f)
                    sbp.Append(Col(new Vector3(340f, 8f, z))).Append(' ');
                log.LogInfo($"[PROBE] corner profile along z at x=340 (z -2..14, 1,5 m): {sbp}");
                sbp = new System.Text.StringBuilder();
                for (float x = 316f; x <= 336.01f; x += 1.5f)
                    sbp.Append(Col(new Vector3(x, 8f, 5f))).Append(' ');
                log.LogInfo($"[PROBE] corner profile along x at z=5 (x 316..336, 1,5 m): {sbp}");
            }
            // v0.9.21: the five figures on the House's roof вЂ” a ray down at the middle figure's feet
            // must land on the podium's roof ring (they stand on it, not inside it), and a ray at chest
            // height from the city side must be stopped by the figure's own body
            if (FigFeet.Count == 7)
            {
                var f2 = FigFeet[2];
                report("figure roof, at the middle figure's feet",
                       down(new Vector3(f2.x, f2.y + 0.2f, f2.z), 0.3f, 2.5f));
                var c5 = FigFeet[4] + Vector3.up * (0.63f * FigHi(4));
                report("figure 5 (chest, from the roof edge)",
                       shoot(new Vector3(c5.x, c5.y, c5.z - 6f), Vector3.forward, 9f));
            }
            if (bridges.Count > 0)
            {
                var s = bridges[0];
                Vector3 mid = (s.a + s.b) * 0.5f;
                report("bridge deck", down(mid + Vector3.up * s.y, 1.2f, 3f));
                report("street under a bridge", down(new Vector3(mid.x, 0f, mid.z), 1.0f, 3f));
            }
            report("square", down(new Vector3(232f, PlinthH, 55f), 2f, 5f));

            // v0.9.59: the **rebuilt** House of Soviets вЂ” measured, not judged from a render. Down-rays:
            // the hall floor (stylobate slab), the stylobate roof plate, each tower's roof plate and the
            // entrance pavilion; an up-ray from the hall must stop on the roof's underside; and a ray from
            // the esplanade at 60 m must be stopped by the central loggia volume.
            {
                var hp = new Vector3(230f, PlinthH, 84f);
                var hr = Quaternion.Euler(0f, 4f, 0f);
                System.Func<float, float, float, Vector3> H = (x, y, z) => hp + hr * new Vector3(x, y, z);
                report("House hall floor (expect 10,15 m)", down(H(0f, 2f, 0f), 0f, 4f));
                report("House stylobate roof (expect 22,95 m)", down(H(-50f, 15f, -20f), 0f, 4f));
                report("House lower-tower top (expect 61,4 m)", down(H(30.5f, 58f, 2f), 0f, 6f));
                report("House upper-tower roof (expect 100,0 m)", down(H(30.5f, 94f, 3f), 0f, 6f));
                report("House rear block roof (expect 105,0 m)", down(H(-30.5f, 100f, 20f), 0f, 6f));
                report("House hall ceiling (expect 22,17 m)", up(H(0f, 1.2f, 0f), 0f, 40f));
                report("House gallery floor (expect 48,55 m)", down(H(0f, 39.5f, 2f), 0f, 1f));
                report("House thin bridge floor (expect 74,15 m)", down(H(0f, 64.8f, 0f), 0f, 1f));
                report("House core top (expect 94,0 m)", down(H(0f, 90f, 6f), 0f, 6f));
                report("House entrance wall at 60 m (expect ~35 m)",
                       shoot(H(20f, 3f, -60f), hr * Vector3.forward, 60f));
                // v0.9.68: **the ground-floor passage** вЂ” a ray at knee height (0,35 m over the floor)
                // must run from the doorway all the way to the rear wall (~41 m), not stop at a 0,7 m
                // threshold. And a down-ray right behind the entrance must land on the hall floor.
                report("House ground-floor passage (0,35 m up, expect the rear wall, ~41 m)",
                       shoot(H(0f, BASE + 0.35f, -22f), hr * Vector3.forward, 60f));
                report("House hall floor behind the entrance (expect 10,15 m)",
                       down(H(0f, 2f, -17f), 0f, 4f));
                // v0.9.69: the **towers' ground-floor passage** вЂ” a ray from the hall through the
                // east tower's 3,4 m portal, its corridor and its outer portal must clear both walls
                // (~38,7 m to the hall's outer wall). The 61 m stepped fin used to stop it at ~5 m.
                report("House tower portal (hall -> tower -> side gallery)",
                       shoot(H(10f, BASE + 1.2f, 2f), hr * Vector3.right, 45f));
                report("House tower doorway floor (expect 10,15 m)",
                       down(H(16.5f, BASE + 2f, 2f), 0f, 4f));
                // every collider on the entrance axis at knee and chest height, with the distance вЂ”
                // a still is ambiguous, a distance is not
                foreach (float eye in new[] { BASE + 0.35f, BASE + 1.15f })
                {
                    var w0 = root.TransformPoint(H(0f, eye, -34f));
                    var hits = Physics.RaycastAll(w0, root.rotation * hr * Vector3.forward, 62f, ~0,
                                                  QueryTriggerInteraction.Ignore);
                    System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                    var sb = new System.Text.StringBuilder();
                    foreach (var h in hits) sb.Append($"{h.distance:0.0} ({h.collider.name}) ");
                    log.LogInfo($"[PASS] entrance axis, {eye - BASE:0.00} m over the floor: {sb}");
                }
                // and a 2 m grid over the hall: anything standing on the ground floor is named
                {
                    var sb = new System.Text.StringBuilder();
                    const float floorY = PlinthH + BASE;
                    for (float z = -20f; z <= 20.01f; z += 2f)
                        for (float x = -46f; x <= 46.01f; x += 2f)
                        {
                            var w = root.TransformPoint(H(x, 4f, z));
                            if (!Physics.Raycast(w, Vector3.down, out var hh, 8f, ~0,
                                                 QueryTriggerInteraction.Ignore)) continue;
                            float ly = root.InverseTransformPoint(hh.point).y;
                            if (ly > floorY + 0.55f)
                                sb.Append($"({x:0},{z:0}) {ly - floorY:0.00} m {hh.collider.name}; ");
                        }
                    log.LogInfo($"[HALL] things over 0,55 m on the ground floor: {sb}");
                }
            }

            // v0.9.98: the air-hockey table on the House's top floor вЂ” three measurements, no render:
            // a ray down onto the rink (0,72 m over the annex floor), a ray down beside it (the floor
            // itself, district-local 112,55 m), and a horizontal ray from 8,3 m that must be stopped by
            // the table rather than the hall's west wall.
            if (airHockeyPlaced)
            {
                var hr2 = Quaternion.Euler(0f, 4f, 0f);
                Vector3 ah = airHockeyFeet;
                report("air-hockey table top (body box, expect 1,14 m over the annex floor)",
                    down(ah + hr2 * new Vector3(0.9f, 0f, 0f), 3f, 4f));
                report("air-hockey table floor beside it (expect 112,55 m)",
                    down(ah + hr2 * new Vector3(0f, 0f, 4.2f), 1f, 3f));
                report("air-hockey table side shot from 8,3 m (expect the table at ~5,7 m)",
                    shoot(ah + hr2 * new Vector3(-8.3f, 0.8f, 0f), hr2 * Vector3.right, 14f));
            }

            // v0.9.18: the clock tower on the block on the peninsula вЂ” measured, not judged from a render.
            // A ray down inside the drum must land on the roof plate (the tower's floor); a ray shot at
            // the clock storey from outside must be stopped by the drum's solid wall (the dial is only a
            // disc 4 cm off it), and the mansard's platform must be solid from above.
            if (pentBlock != null)
            {
                var pr = Quaternion.Euler(0f, pentBlock.yaw, 0f);
                report("penthouse floor (inside the drum)",
                    down(pentBlock.p + pr * new Vector3(0f, pentDeckY, 0f), 2f, 4f));
                report("penthouse clock storey wall (+z)",
                    shoot(pentBlock.p + pr * new Vector3(3f, pentDeckY + 5.6f + 5.6f + 4.5f, pentAd + 9f),
                          pr * Vector3.back, 14f));
                report("penthouse lantern roof",
                    down(pentBlock.p + pr * new Vector3(0f, pentTopY, 0f), 4f, 12f));
                report("penthouse roof platform",
                    down(pentBlock.p + pr * new Vector3(pentAw * 0.22f, pentApexY, pentAd * 0.22f), 6f, 14f));
            }

            // v0.9.9: every floor of the terraced platform, the middle of two ramps and a stair tread
            report("terrace 3 floor (+8)", down(new Vector3(190f, 8f, 250f), 2f, 5f));
            report("terrace 2 floor (+5)", down(new Vector3(370f, 5f, 190f), 2f, 5f));
            report("terrace 4 floor (+11)", down(new Vector3(10f, 11f, 100f), 2f, 5f));
            report("terrace 5 floor (+14)", down(new Vector3(-40f, 14f, 265f), 2f, 5f));
            report("plinth of the House (+10)", down(new Vector3(200f, PlinthH, 120f), 2f, 5f));
            report("ramp ring_west (middle)", down(new Vector3(56f, 4f, 175f), 2f, 6f));
            report("ramp plinth (middle)", down(new Vector3(132f, 5f, 79f), 2f, 6f));
            report("stair to terrace 3", down(new Vector3(137f, 4f, 201f), 2f, 6f));
            report("base deck between the terraces", down(new Vector3(150f, 0f, 150f), 2f, 5f));
            // v0.9.20: the corner next to the round hole вЂ” the pad is flat and stands on the crest's own
            // level, the terrace's wall is CornerApron metres inland, and the base deck is where it was
            report("corner pad (its top, over our own deck)",
                down(new Vector3(330f, 0.4f, 6f), 2f, 5f));
            report("corner pad over the granite shelf (10 mm above the stone, was 25)",
                down(new Vector3(356f, 0.4f, 4f), 2f, 5f));
            // (the v0.9.57 House plate probes were replaced by the v0.9.59 block above вЂ” the House has
            // been rebuilt and its geometry moved)
            report("south-east corner, behind the pad (+5)", down(new Vector3(356f, 5f, 26f), 2f, 5f));
            report("east terrace still at +5 (mid band)", down(new Vector3(356f, 5f, 120f), 2f, 5f));
            report("base deck west of the ramp's foot", down(new Vector3(314f, 0f, 4f), 2f, 5f));
            // v0.9.22: the corner's *numeric height field* вЂ” the pad's own collider is switched off for
            // the scan, so the rows show the surface **under** it (the level's own granite crest and its
            // exact extent). A suffix tells who owns the hit: L = the level, d = our deck, t = terraces,
            // s = stairs, r = ramps. The design of the smooth skirt is read off these numbers.
            CornerScan();
            log.LogInfo($"[PROBE] done: {ok} ok, {bad} misses");
            Headroom();
            DumpPlan();
        }

        /// <summary>
        /// v0.9.22 вЂ” the corner as a height field, measured with the pad's collider switched off, so the
        /// rows show what is **under** it: the level's own granite crest, where it begins and ends, and
        /// the deck around it. Each value is `height` + an owner letter (`L` level, `d` deck, `t`
        /// terrace, `s` stair, `r` ramp). The skirt is designed from these rows, not from a render.
        /// </summary>
        private static void CornerScan()
        {
            if (root == null) return;
            Transform pad = root.Find("corner_pad");
            Collider pc = pad != null ? pad.GetComponent<Collider>() : null;
            bool was = pc != null && pc.enabled;
            if (pc != null) pc.enabled = false;
            try
            {
                System.Func<float, float, string> V = (x, z) =>
                {
                    Vector3 w = root.TransformPoint(new Vector3(x, 40f, z));
                    if (!Physics.Raycast(w, Vector3.down, out var h, 120f, ~0, QueryTriggerInteraction.Ignore))
                        return "MISS";
                    string n = h.collider.name;
                    char c =
                        n.StartsWith("deck") ? 'd' :
                        n.StartsWith("terrac") ? 't' :
                        n.StartsWith("stairs") ? 's' :
                        n.StartsWith("ramp") ? 'r' :
                        n.StartsWith("streets") ? 'd' : 'L';
                    return string.Format("{0:0.00}{1}", h.point.y - root.position.y, c);
                };
                foreach (float x in new[] { 318f, 322f, 326f, 330f, 334f, 342f, 350f, 360f, 370f, 378f })
                {
                    var sb = new System.Text.StringBuilder();
                    for (float z = -4f; z <= 14.01f; z += 2f)
                        sb.Append(V(x, z).PadLeft(8)).Append(' ');
                    log.LogInfo($"[PROBE] corner z-profile x={x:0} (z -4..14, 2 m): {sb}");
                }
                foreach (float z in new[] { -3f, -2f, -1f, 0f, 1f, 3f, 6f, 10f })
                {
                    var sb = new System.Text.StringBuilder();
                    for (float x = 300f; x <= 380.01f; x += 4f)
                        sb.Append(V(x, z).PadLeft(8)).Append(' ');
                    log.LogInfo($"[PROBE] corner x-profile z={z:0} (x 300..380, 4 m): {sb}");
                }
            }
            finally
            {
                if (pc != null) pc.enabled = was;
            }

            // ---- v0.9.22: the same corner with the pad **on**, sampled every 0.5 m. This is the surface
            // the player walks on, so a step is a jump between two neighbours; the biggest 0,5 m step is
            // printed with it. (A 4-7 % ramp is ~2-3,5 cm per 0,5 m, anything above that is a lip.)
            System.Func<float, float, float> Top = (x, z) =>
            {
                Vector3 w = root.TransformPoint(new Vector3(x, 40f, z));
                return Physics.Raycast(w, Vector3.down, out var h, 120f, ~0, QueryTriggerInteraction.Ignore)
                    ? h.point.y - root.position.y : float.NaN;
            };
            System.Action<string, bool, float, float, float> line = (tag, alongX, fixedC, from, to) =>
            {
                var sb = new System.Text.StringBuilder();
                float prevY = 0f, worst = 0f, at = 0f;
                int walls = 0;
                bool have = false;
                for (float t = from; t <= to + 0.001f; t += 0.5f)
                {
                    float y = alongX ? Top(t, fixedC) : Top(fixedC, t);
                    if (float.IsNaN(y)) { sb.Append("  MISS"); have = false; continue; }
                    if (have && Mathf.Abs(y - prevY) > worst)
                    {
                        if (Mathf.Abs(y - prevY) > 1f) walls++;        // a terrace wall / a parapet, not a lip
                        else { worst = Mathf.Abs(y - prevY); at = t; }
                    }
                    prevY = y; have = true;
                    sb.Append(y.ToString("0.00").PadLeft(6));
                }
                log.LogInfo($"[PROBE] corner {tag}: {sb}");
                log.LogInfo($"[PROBE] corner {tag}: biggest 0,5 m step {worst * 100f:0.0} cm at {at:0.0} m" +
                            (walls > 0 ? $" ({walls} jump(s) over 1 m: a wall, not the floor)" : ""));
            };
            line("walk z-profile x=340 (z -3..13)", false, 340f, -3f, 13f);
            line("walk x-profile z=6 (x 312..345)", true, 6f, 312f, 345f);
            // v0.9.23: the south bevel вЂ” the row that used to be a 0,25-0,36 m wall at z = 12,3
            line("south bevel z-profile x=325 (z 8..20)", false, 325f, 8f, 20f);
            line("south bevel z-profile x=316 (z 8..20)", false, 316f, 8f, 20f);
            CornerSteps();
        }

        /// <summary>
        /// v0.9.23 вЂ” the corner as a **map of the walked surface**, not a profile: every 2 m over
        /// x 296..388 / z -20..32 the first thing a ray from above hits is printed as `height|owner`
        /// (d = deck, p = corner pad, t = terrace, s = stairs, r = ramp, L = the level's own mesh).
        /// The rows are the picture the player sees: a raised plate that blocks passage shows up as its
        /// own height with a sharp change to the neighbours, and `step` lines name the two colliders.
        /// This exists because a render cannot tell 0,32 m of the level's granite from 0,365 m of our
        /// own concrete вЂ” the numbers can.
        /// </summary>
        private static void CornerSteps()
        {
            if (root == null) return;
            const float x0 = 296f, x1 = 388f, z0 = -20f, z1 = 32f, step = 2f;
            int nx = Mathf.RoundToInt((x1 - x0) / step) + 1;
            int nz = Mathf.RoundToInt((z1 - z0) / step) + 1;
            var ys = new float[nx, nz];
            var own = new char[nx, nz];
            var nom = new string[nx, nz];
            for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                {
                    Vector3 w = root.TransformPoint(new Vector3(x0 + ix * step, 40f, z0 + iz * step));
                    if (!Physics.Raycast(w, Vector3.down, out var h, 120f, ~0, QueryTriggerInteraction.Ignore))
                    { ys[ix, iz] = float.NaN; own[ix, iz] = '?'; nom[ix, iz] = "MISS"; continue; }
                    ys[ix, iz] = h.point.y - root.position.y;
                    string n = h.collider.name;
                    nom[ix, iz] = n;
                    own[ix, iz] = n.StartsWith("corner_pad") ? 'p'
                                : n.StartsWith("deck") ? 'd'
                                : n.StartsWith("terrac") ? 't'
                                : n.StartsWith("stairs") ? 's'
                                : n.StartsWith("ramp") ? 'r'
                                : n.StartsWith("streets") ? 'd' : 'L';
                }

            // ---- the picture: height in cm (2 chars) + owner, one line per z row --------------------
            for (int iz = nz - 1; iz >= 0; iz--)
            {
                var sb = new System.Text.StringBuilder();
                for (int ix = 0; ix < nx; ix++)
                {
                    if (float.IsNaN(ys[ix, iz])) { sb.Append("  .  "); continue; }
                    sb.Append((Mathf.RoundToInt(ys[ix, iz] * 100f)).ToString().PadLeft(4)).Append(own[ix, iz]).Append(' ');
                }
                log.LogInfo($"[PROBE] corner map z={z0 + iz * step,6:0} (x 296..388, 2 m): {sb}");
            }

            // ---- every real step between two neighbouring samples -----------------------------------
            int found = 0, walls = 0;
            for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                {
                    if (float.IsNaN(ys[ix, iz])) continue;
                    for (int k = 0; k < 2; k++)
                    {
                        int jx = ix + (k == 0 ? 1 : 0), jz = iz + (k == 1 ? 1 : 0);
                        if (jx >= nx || jz >= nz || float.IsNaN(ys[jx, jz])) continue;
                        float dy = ys[jx, jz] - ys[ix, iz];
                        if (Mathf.Abs(dy) < 0.20f || Mathf.Abs(dy) > 1.6f) continue;   // not a lip / a wall
                        // the report is about the **walkable corner**: the platform's own rim (z < 0,3, an
                        // 8,58 m drop into the basin), the sea parapet (x > 374) and the terrace's wall
                        // (z > 11,9) are the designed boundary, not a lip in the way
                        float sx = x0 + ix * step, sz = z0 + iz * step;
                        if (sx < 310f || sx > 374f || sz < 0.3f || sz > 11.9f) { walls++; continue; }
                        // a designed wall is not a lip either: the terrace's parapet and the sea coping
                        bool designed = nom[ix, iz].Contains("rail") || nom[jx, jz].Contains("rail")
                                     || nom[ix, iz].Contains("parapet") || nom[jx, jz].Contains("parapet");
                        if (designed) { walls++; continue; }
                        found++;
                        if (found > 60) continue;
                        log.LogInfo($"[PROBE] corner step {Mathf.Abs(dy) * 100f,5:0.0} cm at " +
                                    $"({sx:0},{sz:0}) -> ({x0 + jx * step:0},{z0 + jz * step:0}) " +
                                    $"{ys[ix, iz]:0.00}{own[ix, iz]} ({nom[ix, iz]}) / " +
                                    $"{ys[jx, jz]:0.00}{own[jx, jz]} ({nom[jx, jz]})");
                    }
                }
            log.LogInfo($"[PROBE] corner steps: {found} lip(s) of 0,20-1,60 m inside the walkable corner " +
                        $"(x 310..374, z 0,3..11,9) between 2 m samples; {walls} samples are the designed " +
                        $"boundary (the platform's rim over the drop, the terrace's rail, the sea parapet). " +
                        $"A lip over 0,30 m blocks the player (CharacterController stepOffset)");
            CornerFaces();
        }

        /// <summary>
        /// v0.9.23 вЂ” the **proof** that the corner's walking surface has no vertical face left. The
        /// 2 m `CornerSteps` grid cannot tell a 0,20 m wall from a 10 % ramp (two metres of a bevel are
        /// 0,2 m of height), so this samples the walkable part on a **0,25 m** grid вЂ” clear of the
        /// platform's edge, the terrace's wall and the sea parapet вЂ” and prints the biggest jump between
        /// two neighbours. 0,08 m over 0,25 m is a 32 % slope: anything above that is a face, not a ramp.
        /// </summary>
        private static void CornerFaces()
        {
            if (root == null) return;
            const float st = 0.25f, xa = 312f, xb = 373f, za = 0.3f, zb = 11.8f;
            int nx = Mathf.RoundToInt((xb - xa) / st) + 1, nz = Mathf.RoundToInt((zb - za) / st) + 1;
            var y = new float[nx, nz];
            for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                {
                    Vector3 w = root.TransformPoint(new Vector3(xa + ix * st, 40f, za + iz * st));
                    y[ix, iz] = Physics.Raycast(w, Vector3.down, out var h, 120f, ~0, QueryTriggerInteraction.Ignore)
                        ? h.point.y - root.position.y : float.NaN;
                }
            float worst = 0f, wx = 0f, wz = 0f;
            int faces = 0, n = 0;
            for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                {
                    if (float.IsNaN(y[ix, iz])) continue;
                    for (int k = 0; k < 2; k++)
                    {
                        int jx = ix + (k == 0 ? 1 : 0), jz = iz + (k == 1 ? 1 : 0);
                        if (jx >= nx || jz >= nz || float.IsNaN(y[jx, jz])) continue;
                        n++;
                        float dy = Mathf.Abs(y[jx, jz] - y[ix, iz]);
                        if (dy > worst) { worst = dy; wx = xa + ix * st; wz = za + iz * st; }
                        if (dy > 0.08f) faces++;
                    }
                }
            log.LogInfo($"[PROBE] corner faces: {n} rays ({nx} x {nz} at 0,25 m over x {xa:0}..{xb:0} / z {za:0.##}..{zb:0.#}), " +
                        $"biggest jump between neighbours {worst * 100f:0.0} cm at ({wx:0.0},{wz:0.0}), " +
                        $"{faces} jump(s) over 8 cm (= a 32 % slope, i.e. a vertical face) — a walkable corner has 0");
        }

        /// <summary>
        /// v0.9.11 вЂ” look inward through the recorded windows of one building and report the first
        /// thing behind the opening. A sensible window shows the room's far side (>= 3 m); a partition
        /// or a stair flight right behind it shows up as ~1 m and is the bug the user photographed.
        /// </summary>
        private static void WindowProbes(Bldg b, string tag)
        {
            var rot = Quaternion.Euler(0f, b.yaw, 0f);
            for (int i = 0; i < b.winPos.Count && i < b.winDir.Count; i++)
            {
                // world space: the district root is itself rotated, so the building yaw is not enough
                Vector3 n = root.rotation * (rot * b.winDir[i]);
                Vector3 lp = b.p + rot * b.winPos[i];
                Vector3 p = root.TransformPoint(lp) + n * 2.5f;
                string s = Physics.Raycast(p, -n, out var h, 40f, ~0, QueryTriggerInteraction.Ignore)
                    ? string.Format("first hit {0:0.00} m ({1})", h.distance - 2.5f, h.collider.name)
                    : "open all the way through";
                log.LogInfo($"[PROBE] {tag} window {i}: {s}");
            }
        }
    }
}
