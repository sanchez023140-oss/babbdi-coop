using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace BabbdiCoop
{
    // Part of the SovietDistrict builder - see SovietDistrict.cs.
    internal static partial class SovietDistrict
    {

        private static Bldg PickHeliBlock()
        {
            Bldg best = null;
            foreach (var b in bldgs)
            {
                if (b.memorial || b.penthouse) continue;
                if (b.w < 42f) continue;      // the 39,6 m fuselage must fit along the long axis
                if (best == null || b.floors > best.floors ||
                    (b.floors == best.floors && b.w > best.w))
                    best = b;
            }
            if (best != null)
                log.LogInfo($"[HELI] roof chosen: {best.w:0.0}x{best.d:0.0} m, {best.floors} floors, " +
                            $"top {best.Top:0.0} m at {best.p.x:0.0},{best.p.z:0.0} — the tallest free " +
                            $"block wide enough for the 39,6 m fuselage; its vent box is skipped");
            return best;
        }

        private static void Helicopter(Bldg b)
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(typeof(SovietDistrict).Assembly.Location);
                string path = System.IO.Path.Combine(dir ?? string.Empty, "BabbdiCoop", "mi26.glb");
                if (!System.IO.File.Exists(path))
                {
                    log.LogWarning("[HELI] mi26.glb is missing — skipped (" + path + ")");
                    return;
                }
                float roofTop = b.Top + 0.26f;
                // v0.9.89: park it centred on the dam's crest — the previous fixed offset left a
                // third of the machine hanging over the water. Measure the crest's width here and
                // drop the wheels exactly onto it.
                Vector3 spot = HeliSpot;
                Vector3 w0 = root.TransformPoint(spot);
                Vector3 dzL = root.rotation * Vector3.forward;
                float lo = 0f, hi = 0f;
                for (float t = -12f; t <= 12f; t += 0.5f)
                {
                    var p = w0 + dzL * t + Vector3.up * 8f;
                    if (Physics.Raycast(p, Vector3.down, out var hit, 30f, ~0, QueryTriggerInteraction.Ignore)
                        && Mathf.Abs(hit.point.y - w0.y) < 0.9f)
                    {
                        lo = Mathf.Min(lo, t);
                        hi = Mathf.Max(hi, t);
                    }
                }
                spot.z += (lo + hi) * 0.5f;
                Vector3 w1 = root.TransformPoint(spot);
                if (Physics.Raycast(w1 + Vector3.up * 8f, Vector3.down, out var gh, 30f, ~0, QueryTriggerInteraction.Ignore))
                    spot.y += gh.point.y - w0.y + 0.02f;
                Vector3 pos = spot;
                const float HeliScale = 1.00f;   // the GLB already carries the reference Г—1.20 dimensions
                var go = GlbFigureBuilder.Load(path, pos, HeliYaw, root, Shader.Find("Standard"),
                                               log, "heli_mi26", "HELI", true, true);
                go.transform.localScale = Vector3.one * HeliScale;
                var mf = go.GetComponentInChildren<MeshFilter>();
                var mr = go.GetComponentInChildren<Renderer>();
                var mb = mf != null ? mf.sharedMesh.bounds : new Bounds();
                var wb = mr != null ? mr.bounds : mb;
                Vector3 modelSize = mb.size * HeliScale;
                // v0.9.105: the eye/target are exact model-space points, framed and checked
                // offline in render_cam.py; the mesh's own transform maps them to the world,
                // so the bay camera lands inside the bay no matter where the machine stands.
                var mft = mf.transform;
                heliMesh = mft;
                heliInA = mft.TransformPoint(new Vector3(0f, 6.2f, -14f) * 1.3f);   // inside the cargo bay
                heliInB = mft.TransformPoint(new Vector3(0f, 3.2f, 10f) * 1.3f);    // aft, down at the ramp
                heliLowA = mft.TransformPoint(new Vector3(0f, 3.2f, 30f) * 1.3f);    // on the crest behind the ramp
                heliLowB = mft.TransformPoint(new Vector3(0f, 4.2f, 12f) * 1.3f);    // up at the ramp's underside
                heliSideA = mft.TransformPoint(new Vector3(-11f, 1.3f, 25f) * 1.3f);
                heliSideB = mft.TransformPoint(new Vector3(0f, 2.3f, 12f) * 1.3f);
                heliCabA = mft.TransformPoint(new Vector3(0.6f, 9.9f, -30.5f));  // inside the cockpit
                heliCabB = mft.TransformPoint(new Vector3(0f, 9.2f, -38.0f));
                heliRampA = mft.TransformPoint(new Vector3(-17f, 4.0f, 23f) * 1.3f);
                heliRampB = mft.TransformPoint(new Vector3(0f, 4.2f, 10f));
                heliEdgeA = mft.TransformPoint(new Vector3(-1.6f, 9.6f, 11.5f));
                heliEdgeB = mft.TransformPoint(new Vector3(-4.03f, 8.4f, 14.2f));
                // v0.9.113: the cargo hatch = two clamshell leaves + the ramp, each on its
                // own hinge, played in the real order: the leaves swing out, then the ramp
                // goes down; closing runs the other way.
                try
                {
                    string hatchDir = System.IO.Path.GetDirectoryName(path) ?? "";
                    var hingePos = new Dictionary<string, Vector3>();
                    var doorAxes = new Dictionary<string, (Vector3 axis, float sign)>();
                    string hfile = System.IO.Path.Combine(hatchDir, "mi26_hinges.txt");
                    if (System.IO.File.Exists(hfile))
                        foreach (var ln in System.IO.File.ReadAllLines(hfile))
                        {
                            var q = ln.Split(' ');
                            float F(int k) => float.Parse(q[k], System.Globalization.CultureInfo.InvariantCulture);
                            if (q.Length == 4)
                                hingePos[q[0]] = new Vector3(F(1), F(2), F(3));
                            else if (q.Length == 8)
                            {
                                hingePos[q[0]] = new Vector3(F(1), F(2), F(3));
                                doorAxes[q[0]] = (new Vector3(F(4), F(5), F(6)), F(7));
                            }
                        }
                    Vector3 HP(string k, Vector3 dflt) =>
                        hingePos.TryGetValue(k, out var v) ? v : dflt;
                    Transform Hinge(string nm, Vector3 local, string file, out Transform tilt)
                    {
                        var hg = new GameObject(nm);
                        hg.transform.position = mft.TransformPoint(local);
                        // v0.9.116: the hinge frame IS the mesh frame — no yaw assumption,
                        // so a closed leaf lands exactly on the skin it was cut from
                        hg.transform.rotation = mft.rotation;
                        var tl = new GameObject("tilt");
                        tl.transform.SetParent(hg.transform, false);
                        GlbFigureBuilder.Load(System.IO.Path.Combine(hatchDir, file), Vector3.zero, 0f,
                                              tl.transform, Shader.Find("Standard"), log, nm, "HELI", true, false);
                        tilt = tl.transform;
                        return hg.transform;
                    }
                    if (System.IO.File.Exists(System.IO.Path.Combine(hatchDir, "mi26_ramp.glb")))
                    {
                        var hatch = new GameObject("mi26_hatch").AddComponent<HeliHatch>();
                        Transform tRamp, tL, tR;
                        Hinge("mi26_ramp_hinge", HP("ramp", new Vector3(0f, 3.317f, 6.643f)),
                              "mi26_ramp.glb", out tRamp);
                        Hinge("mi26_door_l_hinge", HP("door_l", new Vector3(-2.655f, 5.064f, 11.647f)),
                              "mi26_door_l.glb", out tL);
                        Hinge("mi26_door_r_hinge", HP("door_r", new Vector3(2.655f, 5.064f, 11.647f)),
                              "mi26_door_r.glb", out tR);
                        hatch.Ramp = tRamp;
                        hatch.DoorL = tL;
                        hatch.DoorR = tR;
                        if (doorAxes.TryGetValue("door_l", out var al)) { hatch.AxisL = al.axis; hatch.SignL = al.sign; }
                        if (doorAxes.TryGetValue("door_r", out var ar)) { hatch.AxisR = ar.axis; hatch.SignR = ar.sign; }
                        // v0.9.116: the leaves' trim — the red-framed window of reference 1
                        foreach (var tr in new[] { (tL, "mi26_trim_l.glb", "heli_trim_l"),
                                                   (tR, "mi26_trim_r.glb", "heli_trim_r") })
                        {
                            string tp = System.IO.Path.Combine(hatchDir, tr.Item2);
                            if (false && System.IO.File.Exists(tp))      // trim parked: it does not follow the leaf yet
                                GlbFigureBuilder.Load(tp, Vector3.zero, 0f, tr.Item1, Shader.Find("Standard"),
                                                      log, tr.Item3, "HELI", false, false);
                        }
                        hatch.SetPose(1f);     // shipped open, as the ТЗ asks
                        heliHatch = hatch;
                        log.LogInfo("[HELI] cargo hatch: two clamshell leaves (3,5 x 5,0 m each) on " +
                                    "fore-aft hinges + the 10,3 x 7,4 m ramp; H plays the sequence " +
                                    $"leaves -> ramp over {hatch.Duration:0} s (closing is reversed); " +
                                    $"hinges read from mi26_hinges.txt ({hingePos.Count} entries)");
                    }
                    else log.LogWarning("[HELI] mi26_ramp.glb is missing — the hatch stays static");
                }
                catch (Exception e)
                {
                    log.LogError("[HELI] hatch failed: " + e);
                }
                float groundWorld = root.TransformPoint(spot).y;
                log.LogInfo($"[HELI] Mi-26 on the dam's crest, 50 m west of the district (local " +
                            $"{spot.x:0.0},{spot.z:0.0}; crest width measured {hi - lo:0.0} m): " +
                            $"wheels at y={wb.min.y:0.00} m (crest {groundWorld:0.00} world), nose along the dam, " +
                            $"model {modelSize.x:0.0} x {modelSize.y:0.0} x {modelSize.z:0.0} m " +
                            $"(reference 40,0 x 32,0 x 8,15 m x1,20 = 48,0 x 38,4 x 9,77; " +
                            $"reshaped nose/tail/rotor, {HeliScale:0.00}x), " +
                            $"{mf?.sharedMesh.vertexCount ?? 0} verts, hollow hull from the user's purple outline " +
                            "(side profile exact, +40 %, +25 % width, +15 % height, real window/porthole " +
                            "openings, cabin space inside, cockpit bulkhead, open cargo ramp, staggered " +
                            "portholes so the cabin is not see-through) + " +
                            "the ORIGINAL engine deck, intakes, gearbox fairing, main rotor and landing gear " +
                            "from the source model (x/z 1,20; belly 0,95 m off the ground; 1,5 deg nose-up; " +
                            $"track {2 * 4.1f:0.0} m; main axle {17.36f:0.00} m and nose axle {6.5f:0.00} m behind the nose)");
            }
            catch (Exception e)
            {
                log.LogError("[HELI] failed: " + e);
            }
        }

        /// <summary>
        /// v0.9.61 вЂ” one of the House of Soviets' bridges: a two-storey slab spanning between the
        /// towers, with a ribbon of small windows near its top and a solid parapet below, plus a
        /// floor and a ceiling plate. Its ends are the towers' own walls (the doorways are cut
        /// there), so it is open at both ends and can be walked through.
        /// </summary>
        private static void Bridge(GameObject parent, string name,
                                   float x0, float x1, float z0, float z1, float y0, float y1,
                                   List<Vector4> frontDoors = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var g = new Geo();
            float w = x1 - x0, cx = (x0 + x1) * 0.5f, h = y1 - y0;
            Rect2(g, x0, x1, z0, z1, y0 - 0.6f, y0);
            Rect2(g, x0, x1, z0, z1, y1 - 0.6f, y1);
            Wall(g, new Vector3(cx, 0f, z0), Vector3.left, Vector3.back, w, y0, y1, 1, h,
                 1.4f, 1.6f, h - 4.2f, 0.12f, 1.8f, frontDoors ?? new List<Vector4>(), null, 1, 0f);
            Wall(g, new Vector3(cx, 0f, z1), Vector3.right, Vector3.forward, w, y0, y1, 1, h,
                 1.4f, 1.6f, h - 4.2f, 0.12f, 1.8f, new List<Vector4>(), null, 1, 0f);
            g.Apply(go, mRough);
            AddMeshCollider(go, name);
        }

        /// <summary>v0.9.62 вЂ” an n-sided concrete column (the round supports under the House's podium).</summary>
        private static void Cylinder(Geo g, float x, float z, float r, float y0, float y1, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                var p0 = new Vector3(x + Mathf.Cos(a0) * r, 0f, z + Mathf.Sin(a0) * r);
                var p1 = new Vector3(x + Mathf.Cos(a1) * r, 0f, z + Mathf.Sin(a1) * r);
                float am = (a0 + a1) * 0.5f;
                var nrm = new Vector3(Mathf.Cos(am), 0f, Mathf.Sin(am)).normalized;
                g.Quad(0, p0 + Vector3.up * y0, p1 + Vector3.up * y0,
                          p1 + Vector3.up * y1, p0 + Vector3.up * y1, nrm, 0.5f);
                g.Tri(0, new Vector3(x, y1, z),
                          new Vector3(x + Mathf.Cos(a0) * r, y1, z + Mathf.Sin(a0) * r),
                          new Vector3(x + Mathf.Cos(a1) * r, y1, z + Mathf.Sin(a1) * r),
                          Vector3.up, Vector2.zero, Vector2.zero, Vector2.zero);
                g.Tri(0, new Vector3(x, y0, z),
                          new Vector3(x + Mathf.Cos(a1) * r, y0, z + Mathf.Sin(a1) * r),
                          new Vector3(x + Mathf.Cos(a0) * r, y0, z + Mathf.Sin(a0) * r),
                          Vector3.down, Vector2.zero, Vector2.zero, Vector2.zero);
            }
        }

        /// <summary>v0.9.63 вЂ” a straight concrete flight: `n` steps of `rise`/`n` along `dir`, 0.30 m treads.</summary>
        private static void Flight(Geo g, Vector3 o, Vector3 dir, int n, float rise, float width)
        {
            dir = dir.normalized;
            float h = rise / n;
            for (int i = 0; i < n; i++)
            {
                var c = o + dir * ((i + 0.5f) * 0.30f) + Vector3.up * (h * (i + 0.5f));
                g.Box(0, c, new Vector3(width, h * (i + 1), 0.30f), Quaternion.LookRotation(dir, Vector3.up), 0.6f);
            }
        }

        /// <summary>v0.9.65 вЂ” a round **blind** porthole borrowed from reference 1: a concrete collar
        /// (protruding ring) around a recessed concrete bottom with a shallow cast boss. v0.9.69: no
        /// glass вЂ” the old black disc read as a window pane, so the niche is plain concrete now.</summary>
        private static void Porthole(Geo g, Vector3 c, Vector3 n, float r, float depth = 0.26f, int seg = 12)
        {
            n = n.normalized;
            Vector3 ex = Vector3.Cross(Vector3.up, n);
            if (ex.sqrMagnitude < 0.01f) ex = Vector3.right;
            ex.Normalize();
            Vector3 ey = Vector3.Cross(n, ex).normalized;
            Vector3 o = c + n * depth;
            Vector3 disc = c - n * 0.07f;            // the blind bottom, inside the wall
            Vector3 boss = c - n * 0.01f;            // its shallow concrete relief
            const float rim = 0.22f;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * 2f * Mathf.PI / seg, a1 = (i + 1) * 2f * Mathf.PI / seg;
                Vector3 d0 = ex * Mathf.Cos(a0) + ey * Mathf.Sin(a0);
                Vector3 d1 = ex * Mathf.Cos(a1) + ey * Mathf.Sin(a1);
                float R = r + rim;
                g.Quad(0, o + d0 * r, o + d0 * R, o + d1 * R, o + d1 * r, n, 0.6f);
                g.Quad(0, c + d0 * R, o + d0 * R, o + d1 * R, c + d1 * R, (d0 + d1).normalized, 0.6f);
                g.Quad(0, c + d0 * r, o + d0 * r, o + d1 * r, c + d1 * r, -(d0 + d1).normalized, 0.6f);
                g.Tri(0, disc + d0 * r, disc + d1 * r, disc, n, Vector2.zero, Vector2.zero, Vector2.zero);
                g.Tri(0, boss + d0 * (r * 0.45f), boss + d1 * (r * 0.45f), boss, n,
                      Vector2.zero, Vector2.zero, Vector2.zero);
                g.Quad(0, disc + d0 * (r * 0.45f), disc + d1 * (r * 0.45f),
                          boss + d1 * (r * 0.45f), boss + d0 * (r * 0.45f), (d0 + d1).normalized, 0.6f);
            }
        }

        /// <summary>v0.9.67 вЂ” a **brutalist gargoyle** for the entrance canopy's corners: a crouched,
        /// hunched beast in raw concrete вЂ” long snout, recessed black eyes and mouth, horns, angular folded
        /// wings, clawed forelimbs, a curled tail. Mirrored by `s` (the pair faces the same way).</summary>
        private static void Gargoyle(Geo g, float x, float y, float z)
        {
            g.Box(0, new Vector3(x, y + 0.3f, z), new Vector3(1.8f, 0.6f, 1.8f), Quaternion.identity, 0.5f);
            g.Box(0, new Vector3(x, y + 1.35f, z), new Vector3(1.3f, 1.5f, 1.9f), Quaternion.Euler(-8f, 0f, 0f), 0.5f);
            g.Box(0, new Vector3(x, y + 2.15f, z - 0.2f), new Vector3(1.75f, 0.7f, 1.25f), Quaternion.Euler(-12f, 0f, 0f), 0.5f);
            g.Box(0, new Vector3(x, y + 2.52f, z - 1.05f), new Vector3(0.95f, 0.85f, 1.0f), Quaternion.Euler(10f, 0f, 0f), 0.5f);
            g.Box(0, new Vector3(x, y + 2.36f, z - 1.72f), new Vector3(0.65f, 0.45f, 0.62f), Quaternion.identity, 0.5f);
            g.Box(1, new Vector3(x, y + 2.22f, z - 1.94f), new Vector3(0.55f, 0.18f, 0.22f), Quaternion.identity, 0.5f);
            g.Box(1, new Vector3(x - 0.22f, y + 2.64f, z - 1.44f), new Vector3(0.16f, 0.12f, 0.16f), Quaternion.identity, 0.5f);
            g.Box(1, new Vector3(x + 0.22f, y + 2.64f, z - 1.44f), new Vector3(0.16f, 0.12f, 0.16f), Quaternion.identity, 0.5f);
            g.Box(0, new Vector3(x - 0.38f, y + 2.98f, z - 0.72f), new Vector3(0.14f, 0.52f, 0.14f), Quaternion.Euler(18f, 0f, -22f), 0.5f);
            g.Box(0, new Vector3(x + 0.38f, y + 2.98f, z - 0.72f), new Vector3(0.14f, 0.52f, 0.14f), Quaternion.Euler(18f, 0f, 22f), 0.5f);
            g.Box(0, new Vector3(x - 0.98f, y + 2.1f, z + 0.3f), new Vector3(0.18f, 1.6f, 0.95f), Quaternion.Euler(0f, 6f, 30f), 0.5f);
            g.Box(0, new Vector3(x + 0.98f, y + 2.1f, z + 0.3f), new Vector3(0.18f, 1.6f, 0.95f), Quaternion.Euler(0f, -6f, -30f), 0.5f);
            g.Box(0, new Vector3(x - 0.58f, y + 0.95f, z - 0.9f), new Vector3(0.34f, 1.2f, 0.34f), Quaternion.Euler(12f, 0f, 0f), 0.5f);
            g.Box(0, new Vector3(x + 0.58f, y + 0.95f, z - 0.9f), new Vector3(0.34f, 1.2f, 0.34f), Quaternion.Euler(12f, 0f, 0f), 0.5f);
            for (int i = -1; i <= 1; i++)
            {
                g.Box(0, new Vector3(x - 0.58f + i * 0.13f, y + 0.3f, z - 1.15f), new Vector3(0.1f, 0.3f, 0.32f), Quaternion.Euler(42f, 0f, 0f), 0.5f);
                g.Box(0, new Vector3(x + 0.58f + i * 0.13f, y + 0.3f, z - 1.15f), new Vector3(0.1f, 0.3f, 0.32f), Quaternion.Euler(42f, 0f, 0f), 0.5f);
            }
            g.Box(0, new Vector3(x, y + 1.2f, z + 1.15f), new Vector3(0.16f, 0.16f, 1.1f), Quaternion.Euler(-20f, 0f, 0f), 0.5f);
            g.Box(0, new Vector3(x, y + 1.55f, z + 1.62f), new Vector3(0.12f, 0.5f, 0.12f), Quaternion.Euler(-35f, 0f, 0f), 0.5f);
        }

        private static void Square()
        {
            // v0.9.9: the paved esplanade is the top of the House of Soviets' +10 m plinth
            var g = new Geo { FloorMul = PlatformGrain };
            g.Box(0, new Vector3(232.5f, PlinthH + 0.06f, 85f), new Vector3(135f, 0.12f, 80f), Quaternion.identity, DeckUv);
            var go = g.Emit(root, "square", mRough, null);
            AddMeshCollider(go, "square");
        }
    }
}
