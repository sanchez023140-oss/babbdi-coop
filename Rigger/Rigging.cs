using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Rigger
{
    /// <summary>
    /// Re-skins a static, arms-down ("I-pose") mesh onto an existing skinned glTF
    /// character (the mixamo soldier) so it can play the soldier's clips unchanged.
    ///
    /// The core problem: clip frames are absolute local rotations authored against the
    /// soldier's T-pose bind. Applying "delta from T" to a mesh that is authored with
    /// its arms hanging down would swing the arms 75° further (behind the back). So the
    /// mesh is first baked from its own pose into the T-pose with linear blend skinning
    /// (same weights are then used for the real skinning), and only then written with
    /// the soldier's inverse bind matrices.
    /// </summary>
    public static class Rigging
    {
        public class Options
        {
            public float ArmDownDeg = 90f;      // how far the arm chain is rotated down for the fit skeleton
            public float ArmForwardDeg = 2f;    // tiny forward swing so the hands sit like the source
            public float ElbowDeg = 6f;
            public float WeightPower = 4f;      // inverse distance exponent
            public int MaxInfluences = 4;
            public float SideMargin = 0.04f;    // metres: bones further than this across the midline are ignored
            public float ArmGate = 0.15f;       // |x| beyond which a vertex belongs to the arm, not the body
            public float ArmCut = 0.21f;        // geodesic radius (fraction of height) of the arm region
            public bool GeodesicArm = true;     // pick the arm by surface distance from the hands, not by |x|
            public float ClothAbove = 0.38f;    // y fraction: above this the tunic rides the pelvis, not the thighs
            public int SmoothIters = 3;         // Laplacian weight passes over the welded mesh graph
            public float SmoothAlpha = 0.5f;
            public float DropStretch;           // > 0: drop triangles stretched by more than this in the bake
            public bool Strict;                 // re-apply the limb masks after smoothing
            public bool Dry;
            public string OutPath;

            /// <summary>
            /// Half-width (in source-model units per unit height) that separates the body
            /// from the arms at a given height fraction. Only a fallback (and a cheap bound):
            /// on this model the tunic hem (0.10 of the height out) and the fingers
            /// (0.13) hang at the SAME height, and the sleeve's inner face (0.094) sits
            /// inside the hip silhouette (0.092) — no |x| threshold can separate the
            /// arm from the coat. The geodesic test in Rig() does it by topology.
            /// </summary>
            public float ArmGateAt(float yFrac)
            {
                if (yFrac > 0.84f) return 0.058f;   // head/neck
                if (yFrac > 0.76f) return 0.088f;   // shoulder line (body 0.076, deltoid 0.097)
                if (yFrac > 0.62f) return 0.085f;   // chest (body 0.082, sleeve 0.094)
                if (yFrac > 0.53f) return 0.100f;   // waist (body 0.092, sleeve 0.086 — overlapping)
                if (yFrac > 0.47f) return 0.078f;   // hips (skirt 0.060, fingers 0.097)
                if (yFrac > 0.38f) return 0.117f;   // skirt hem 0.099 vs fingers 0.136
                return 0.11f;                        // legs: nothing here is an arm
            }
        }

        static (JObject root, byte[] bin) ReadGlb(string path)
        {
            var data = File.ReadAllBytes(path);
            if (BitConverter.ToInt32(data, 0) != 0x46546C67) throw new InvalidDataException("not a GLB");
            JObject json = null;
            byte[] bin = null;
            int off = 12;
            while (off + 8 <= data.Length)
            {
                uint len = BitConverter.ToUInt32(data, off);
                uint type = BitConverter.ToUInt32(data, off + 4);
                var chunk = new byte[len];
                Buffer.BlockCopy(data, off + 8, chunk, 0, (int)len);
                if (type == 0x4E4F534A) json = JObject.Parse(Encoding.UTF8.GetString(chunk));
                else if (type == 0x004E4942) bin = chunk;
                off += 8 + (int)((len + 3u) & ~3u);
            }
            return (json, bin);
        }

        /// <summary>World matrix per node (row-vector convention: local * parentWorld).</summary>
        static Matrix4x4[] Worlds(IReadOnlyList<BabbdiCoop.GlbModelLoader.NodeInfo> nodes,
                                  Dictionary<int, (Vector3? t, Quaternion? r, Vector3? s)> overrideTrs = null)
        {
            var world = new Matrix4x4[nodes.Count];
            var done = new bool[nodes.Count];

            Matrix4x4 World(int i)
            {
                if (done[i]) return world[i];
                done[i] = true;
                var n = nodes[i];
                var t = n.T; var r = n.R; var s = n.S;
                if (overrideTrs != null && overrideTrs.TryGetValue(i, out var ov))
                {
                    if (ov.t.HasValue) t = ov.t.Value;
                    if (ov.r.HasValue) r = ov.r.Value;
                    if (ov.s.HasValue) s = ov.s.Value;
                }
                var local = Matrix4x4.CreateScale(s)
                          * Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(r))
                          * Matrix4x4.CreateTranslation(t);
                world[i] = n.Parent >= 0 ? local * World(n.Parent) : local;
                return world[i];
            }

            for (int i = 0; i < nodes.Count; i++) World(i);
            return world;
        }

        // ---------------------------------------------------------------- rigging

        public static void Rig(string soldierGlb, string objPath, Options opt)
        {
            var soldier = BabbdiCoop.GlbModelLoader.Parse(soldierGlb, "");
            var (root, bin) = ReadGlb(soldierGlb);
            var skin = soldier.Skins[0];
            var bind = Worlds(soldier.Nodes);

            // joints of skin 0, by name
            var jointNode = new int[skin.Joints.Length];
            var jointName = new string[skin.Joints.Length];
            for (int j = 0; j < skin.Joints.Length; j++)
            {
                jointNode[j] = skin.Joints[j];
                jointName[j] = soldier.Nodes[skin.Joints[j]].Name ?? "?";
            }
            int J(string name) => Array.FindIndex(jointName, n => n.EndsWith(name, StringComparison.Ordinal));

            // ---- fit skeleton: bind T-pose with both arm chains rotated down ---------
            var fit = (Matrix4x4[])bind.Clone();
            var armChains = new List<(int pivotNode, Matrix4x4 post)>();
            foreach (var armName in new[] { "LeftArm", "RightArm" })
            {
                int armNode = jointNode[J(armName)];
                var pivot = bind[armNode].Translation;
                // the arm points outward along ±X in the bind pose: rotate it about the
                // body's forward axis (Z) so the hand ends up below the shoulder
                float side = MathF.Sign(pivot.X);           // +1 = anatomical side at +X
                float deg = opt.ArmDownDeg * (side > 0 ? -1f : 1f);
                var q = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, deg * MathF.PI / 180f);
                if (opt.ArmForwardDeg != 0f)
                {
                    // swing the arm slightly forward around the body's Y axis
                    var qf = Quaternion.CreateFromAxisAngle(Vector3.UnitY, opt.ArmForwardDeg * MathF.PI / 180f * side);
                    q = qf * q;
                }
                var post = Matrix4x4.CreateTranslation(-pivot) * Matrix4x4.CreateFromQuaternion(q)
                         * Matrix4x4.CreateTranslation(pivot);

                // every descendant of the arm node (forearm, hand, fingers) rides along
                var stack = new Stack<int>();
                stack.Push(armNode);
                var seen = new HashSet<int>();
                while (stack.Count > 0)
                {
                    int idx = stack.Pop();
                    if (!seen.Add(idx)) continue;
                    fit[idx] = bind[idx] * post;   // world-space rotation about the shoulder
                    for (int c = 0; c < soldier.Nodes.Count; c++)
                        if (soldier.Nodes[c].Parent == idx) stack.Push(c);
                }
                armChains.Add((armNode, post));
            }

            // ---- landmarks for logging --------------------------------------------
            float SoldierHeight() => soldier.Max.Y - soldier.Min.Y;
            foreach (var nm in new[] { "Hips", "Spine2", "Neck", "Head", "LeftArm", "LeftForeArm", "LeftHand", "LeftUpLeg", "LeftLeg", "LeftFoot" })
            {
                int j = J(nm);
                if (j < 0) continue;
                var b = bind[jointNode[j]].Translation;
                var f = fit[jointNode[j]].Translation;
                Console.WriteLine($"  {nm,-12} bind ({b.X,6:F3},{b.Y,6:F3},{b.Z,6:F3})  fitted ({f.X,6:F3},{f.Y,6:F3},{f.Z,6:F3})");
            }

            // ---- source mesh → soldier space --------------------------------------
            var obj = ObjMesh.Load(objPath);
            obj.BuildUnique(out var srcPos, out var srcUv, out var srcIdx);

            // Park any UV that sits in the unused (pure white) part of the atlas on the
            // nearest painted vertex: the boot soles of the rip point at a single white
            // texel and flare up as white shards as soon as the feet move.
            int repainted = 0;
            if (obj.TexturePath != null && File.Exists(obj.TexturePath))
            {
                using var atlas = new System.Drawing.Bitmap(obj.TexturePath);
                bool IsWhite(Vector2 t)
                {
                    int x = (int)(t.X * (atlas.Width - 1));
                    int y = (int)((1f - t.Y) * (atlas.Height - 1));
                    var c = atlas.GetPixel(Math.Clamp(x, 0, atlas.Width - 1), Math.Clamp(y, 0, atlas.Height - 1));
                    return c.R > 245 && c.G > 245 && c.B > 245;
                }
                var painted = Enumerable.Range(0, srcUv.Count).Where(i => !IsWhite(srcUv[i])).ToList();
                for (int i = 0; i < srcUv.Count; i++)
                {
                    if (!IsWhite(srcUv[i]) || painted.Count == 0) continue;
                    int best = painted[0];
                    float bd = float.MaxValue;
                    foreach (int j in painted)
                    {
                        float d = (srcPos[i] - srcPos[j]).LengthSquared();
                        if (d < bd) { bd = d; best = j; }
                    }
                    srcUv[i] = srcUv[best];
                    repainted++;
                }
                Console.WriteLine($"atlas: repainted {repainted} white UVs from the nearest painted vertex");
            }
            float srcMinY = srcPos.Min(p => p.Y), srcMaxY = srcPos.Max(p => p.Y);
            float srcCX = (srcPos.Min(p => p.X) + srcPos.Max(p => p.X)) * 0.5f;
            float srcCZ = (srcPos.Min(p => p.Z) + srcPos.Max(p => p.Z)) * 0.5f;
            float s = SoldierHeight() / (srcMaxY - srcMinY);
            float dstCX = (soldier.Min.X + soldier.Max.X) * 0.5f * 0f; // symmetric: centre on x=0
            float dstCZ = 0f;
            var rotY = Matrix4x4.CreateRotationY(MathF.PI); // source faces +Z, soldier faces -Z

            var verts = new Vector3[srcPos.Count];
            for (int i = 0; i < srcPos.Count; i++)
            {
                var p = new Vector3(srcPos[i].X - srcCX, srcPos[i].Y - srcMinY, srcPos[i].Z - srcCZ) * s;
                p = Vector3.Transform(p, rotY);
                verts[i] = p + new Vector3(dstCX, soldier.Min.Y, dstCZ);
            }
            Console.WriteLine($"source: height {srcMaxY - srcMinY:F3} → scale {s:F3}; " +
                              $"bbox after fit X {verts.Min(v => v.X):F3}..{verts.Max(v => v.X):F3} " +
                              $"Y {verts.Min(v => v.Y):F3}..{verts.Max(v => v.Y):F3}");

            // weld map for reporting / symmetric weights
            var weld = new int[verts.Length];
            var weldPos = new List<Vector3>();
            var weldKey = new Dictionary<(int, int, int), int>();
            for (int i = 0; i < verts.Length; i++)
            {
                var k = ((int)MathF.Round(verts[i].X * 2000), (int)MathF.Round(verts[i].Y * 2000), (int)MathF.Round(verts[i].Z * 2000));
                if (!weldKey.TryGetValue(k, out int u)) { u = weldPos.Count; weldKey[k] = u; weldPos.Add(verts[i]); }
                weld[i] = u;
            }

            // ---- weights: distance to the fitted bone segments ----------------------
            // A joint's segment runs along its bone (joint → its joint children, or
            // joint → parent for leaves) instead of joint → parent for everything:
            // the Hips' parent is the armature root, and that 1 m "bone" used to
            // swallow the whole torso and both thighs.
            var segA = new Vector3[skin.Joints.Length];
            var segB = new Vector3[skin.Joints.Length];
            var jointSet = new HashSet<int>(jointNode);
            for (int j = 0; j < skin.Joints.Length; j++)
            {
                int node = jointNode[j];
                segA[j] = fit[node].Translation;
                var kids = new List<int>();
                for (int c = 0; c < soldier.Nodes.Count; c++)
                    if (soldier.Nodes[c].Parent == node && jointSet.Contains(c)) kids.Add(c);
                if (kids.Count == 1)
                {
                    segB[j] = fit[kids[0]].Translation;
                }
                else if (kids.Count > 1)
                {
                    var avg = Vector3.Zero;
                    foreach (var c in kids) avg += fit[c].Translation;
                    segB[j] = avg / kids.Count;
                }
                else
                {
                    int parent = soldier.Nodes[node].Parent;
                    var ppos = parent >= 0 ? fit[parent].Translation : segA[j];
                    var dir = segA[j] - ppos;
                    float len = MathF.Min(dir.Length() * 2.5f, 0.14f);
                    segB[j] = segA[j] + (len > 1e-4f ? Vector3.Normalize(dir) * len : new Vector3(0f, 0.02f, 0f));
                }
                if ((segB[j] - segA[j]).LengthSquared() < 1e-8f)
                    segB[j] = segA[j] + new Vector3(0f, 0.01f, 0f);
            }
            if (Environment.GetEnvironmentVariable("RIG_DEBUG") == "1")
                for (int j = 0; j < skin.Joints.Length; j++)
                    Console.WriteLine($"  seg {jointName[j],-32} ({segA[j].X,6:F3},{segA[j].Y,6:F3},{segA[j].Z,6:F3}) → ({segB[j].X,6:F3},{segB[j].Y,6:F3},{segB[j].Z,6:F3})");
            // finger bones are noisy for a 150-vertex mesh: fold them into the hand
            int[] foldInto = Enumerable.Repeat(-1, skin.Joints.Length).ToArray();
            for (int j = 0; j < skin.Joints.Length; j++)
            {
                if (jointName[j].Contains("Hand", StringComparison.Ordinal) && !jointName[j].EndsWith("Hand", StringComparison.Ordinal))
                {
                    // e.g. mixamorig:LeftHandThumb1 → mixamorig:LeftHand
                    string handName = jointName[j].Substring(0, jointName[j].IndexOf("Hand", StringComparison.Ordinal) + 4);
                    int hj = Array.FindIndex(jointName, n => n == handName);
                    if (hj >= 0) foldInto[j] = hj;
                }
            }

            var weights = new float[verts.Length * opt.MaxInfluences];
            var boneIdx = new int[verts.Length * opt.MaxInfluences];
            var allowed = new bool[verts.Length * skin.Joints.Length];
            var legSideOf = new int[verts.Length];
            var perBone = new Dictionary<string, int>();

            bool IsArmBone(int j) =>
                jointName[j].Contains("Shoulder", StringComparison.Ordinal) ||
                jointName[j].Contains("Arm", StringComparison.Ordinal) ||
                jointName[j].Contains("Hand", StringComparison.Ordinal);
            bool IsLegBone(int j) =>
                jointName[j].Contains("UpLeg", StringComparison.Ordinal) ||
                jointName[j].EndsWith("Leg", StringComparison.Ordinal) ||
                jointName[j].Contains("Foot", StringComparison.Ordinal) ||
                jointName[j].Contains("Toe", StringComparison.Ordinal);
            bool IsLeft(int j) => jointName[j].StartsWith("mixamorig:Left", StringComparison.Ordinal);

            // Which vertices are arm? On this model |x| is useless: the fingers hang at
            // yf 0.40 with |x| 0.136 in a band where the tunic hem is 0.099 — and 2 cm
            // away in SPACE — while the sleeve's inner face at the chest (0.094) is
            // inside the hip silhouette (0.092). Both are 4-ring tubes joined at the
            // armpit, so the only honest separator is the surface: flood the mesh graph
            // from the fingertips and take everything within a geodesic radius that
            // stops at the shoulder. The hem is 0.66 h away along the surface, the
            // fingers 0.00 h, the elbow 0.19 h — a clean gap.
            var geo = Geodesic(srcPos, srcIdx, weld, weldPos.Count, srcMinY, srcMaxY);
            float hgtSrc = srcMaxY - srcMinY;
            bool[] inArm = new bool[srcPos.Count];
            bool[] rigidToPelvis = new bool[srcPos.Count];
            for (int i = 0; i < srcPos.Count; i++)
            {
                float yf = (srcPos[i].Y - srcMinY) / hgtSrc;
                bool byX = MathF.Abs(verts[i].X) > opt.ArmGateAt(yf) * SoldierHeight();
                bool byGeo = geo[weld[i]] / hgtSrc < opt.ArmCut && MathF.Abs(srcPos[i].X) > 0.01f * hgtSrc;
                inArm[i] = opt.GeodesicArm ? byGeo : byX;
                // the tunic below the belt is a skirt: it must ride the pelvis, or the
                // hem's centre verts get a 50/50 blend of both thighs and tear open in
                // the gap between the legs on every run cycle
                rigidToPelvis[i] = !inArm[i] && yf > opt.ClothAbove;
            }
            Console.WriteLine($"arm region: {inArm.Count(b => b)} of {inArm.Length} verts " +
                              $"({(opt.GeodesicArm ? $"geodesic < {opt.ArmCut:F2} h" : "by |x|")}); " +
                              $"skirt band locked to the pelvis: " +
                              $"{Enumerable.Range(0, srcPos.Count).Count(i => rigidToPelvis[i] && (srcPos[i].Y - srcMinY) / hgtSrc < 0.56f)}");

            for (int i = 0; i < verts.Length; i++)
            {
                var p = verts[i];
                bool arm = inArm[i];

                // legs/foot: a vertex belongs to the nearer leg unless both are about
                // equally close (crotch, seat). Without this the inner side of a boot
                // got ~35 % of the OTHER foot and the boot tore into a white sheet
                // between the steps.
                float dLegL = float.MaxValue, dLegR = float.MaxValue;
                for (int j = 0; j < skin.Joints.Length; j++)
                {
                    if (foldInto[j] >= 0 || !IsLegBone(j)) continue;
                    float dj = DistToSegment(p, segA[j], segB[j]);
                    if (IsLeft(j)) dLegL = MathF.Min(dLegL, dj); else dLegR = MathF.Min(dLegR, dj);
                }
                int legSide = 0;   // 0 = both, +1 = left, -1 = right
                if (dLegL < dLegR * 0.65f) legSide = 1;
                else if (dLegR < dLegL * 0.65f) legSide = -1;
                legSideOf[i] = legSide;

                var cand = new List<(float w, int j)>();
                for (int j = 0; j < skin.Joints.Length; j++)
                {
                    if (foldInto[j] >= 0) continue;
                    if (legSide != 0 && IsLegBone(j) && IsLeft(j) != (legSide > 0)) continue;
                    float midX = (segA[j].X + segB[j].X) * 0.5f;
                    if (p.X > opt.SideMargin && midX < -opt.SideMargin) continue;
                    if (p.X < -opt.SideMargin && midX > opt.SideMargin) continue;
                    // the source mesh is thin: |x| cleanly separates arms from the body,
                    // so keep leg/spine bones away from the hands (they hang next to the
                    // thighs) and arm bones away from the torso
                    if (arm)
                    {
                        if (!IsArmBone(j)) continue;
                        if (MathF.Sign(midX) != MathF.Sign(p.X) && MathF.Abs(midX) > 0.02f) continue;
                    }
                    else if (rigidToPelvis[i] && IsLegBone(j))
                        continue;   // the skirt: pelvis only, or it tears between the thighs
                    else if (IsArmBone(j) && !jointName[j].Contains("Shoulder", StringComparison.Ordinal))
                        continue;
                    allowed[i * skin.Joints.Length + j] = true;
                    float d = DistToSegment(p, segA[j], segB[j]);
                    float w = 1f / (MathF.Pow(d, opt.WeightPower) + 1e-4f);
                    cand.Add((w, j));
                }
                cand.Sort((a, b) => b.w.CompareTo(a.w));
                float sum = 0f;
                for (int k = 0; k < opt.MaxInfluences; k++)
                {
                    if (k >= cand.Count) break;
                    sum += cand[k].w;
                }
                for (int k = 0; k < opt.MaxInfluences; k++)
                {
                    if (k >= cand.Count || sum <= 0f) { weights[i * 4 + k] = 0f; boneIdx[i * 4 + k] = 0; continue; }
                    weights[i * 4 + k] = cand[k].w / sum;
                    boneIdx[i * 4 + k] = cand[k].j;
                }
                if (cand.Count > 0)
                {
                    var nm = jointName[cand[0].j];
                    perBone[nm] = perBone.TryGetValue(nm, out int c) ? c + 1 : 1;
                }
            }
            Console.WriteLine("dominant bone per vertex:");
            foreach (var kv in perBone.OrderByDescending(k => k.Value))
                Console.WriteLine($"   {kv.Key,-32} {kv.Value}");
            if (Environment.GetEnvironmentVariable("RIG_DEBUG") == "1")
            {
                Console.WriteLine("arm-bound vertices (source space):");
                for (int i = 0; i < verts.Length; i++)
                {
                    if (weights[i * 4] <= 0f) continue;
                    var nm = jointName[boneIdx[i * 4]];
                    if (!IsArmBone(boneIdx[i * 4])) continue;
                    Console.WriteLine($"   {nm,-24} w={weights[i * 4]:F2} src ({srcPos[i].X,7:F3},{srcPos[i].Y,7:F3},{srcPos[i].Z,7:F3}) " +
                                      $"yf={(srcPos[i].Y - srcMinY) / (srcMaxY - srcMinY):F2}");
                }
            }

            // ---- smooth the weights over the mesh graph ----------------------------
            // The source is a 147-vertex PS1 mesh: the sleeve and the coat share a
            // single edge, so a hard arm/no-arm split turns that edge into a paper
            // sheet when the arm swings. A couple of Laplacian passes spread the
            // transition over 2-3 vertices, which reads as cloth instead of a spike.
            // The BAKE below deliberately uses the unsmoothed weights: it must put
            // the arms exactly in the skeleton's T-pose, and it only decides where
            // the mesh sits at bind — the soft weights then control the deformation.
            var rawWeights = (float[])weights.Clone();
            var rawBones = (int[])boneIdx.Clone();
            if (opt.SmoothIters > 0)
            {
                int nv = weldPos.Count;
                var wd = new Dictionary<int, float>[nv];
                for (int i = 0; i < nv; i++) wd[i] = new Dictionary<int, float>();
                var counts = new int[nv];
                for (int i = 0; i < verts.Length; i++)
                {
                    var d = wd[weld[i]];
                    counts[weld[i]]++;
                    for (int k = 0; k < 4; k++)
                    {
                        float w = weights[i * 4 + k];
                        if (w <= 0f) continue;
                        int j = boneIdx[i * 4 + k];
                        d[j] = d.TryGetValue(j, out float cur) ? cur + w : w;
                    }
                }
                foreach (var kv0 in wd.Select((d, i) => (d, i)))
                    foreach (var k in kv0.d.Keys.ToList()) kv0.d[k] /= MathF.Max(1, counts[kv0.i]);

                var nb = new Dictionary<int, HashSet<int>>();
                for (int t = 0; t + 2 < srcIdx.Count; t += 3)
                {
                    int a = weld[srcIdx[t]], b = weld[srcIdx[t + 1]], c = weld[srcIdx[t + 2]];
                    void Link(int x, int y)
                    {
                        if (!nb.TryGetValue(x, out var l)) nb[x] = l = new HashSet<int>();
                        l.Add(y);
                    }
                    Link(a, b); Link(b, c); Link(c, a);
                }
                float alpha = opt.SmoothAlpha;
                for (int it = 0; it < opt.SmoothIters; it++)
                {
                    var next = new Dictionary<int, float>[nv];
                    for (int i = 0; i < nv; i++)
                    {
                        var res = new Dictionary<int, float>();
                        foreach (var kv in wd[i]) res[kv.Key] = kv.Value * (1f - alpha);
                        if (nb.TryGetValue(i, out var l) && l.Count > 0)
                        {
                            float share = alpha / l.Count;
                            foreach (int u in l)
                                foreach (var kv in wd[u])
                                    res[kv.Key] = res.TryGetValue(kv.Key, out float cur) ? cur + kv.Value * share : kv.Value * share;
                        }
                        next[i] = res;
                    }
                    wd = next;
                }
                for (int i = 0; i < verts.Length; i++)
                {
                    var d = wd[weld[i]];
                    bool Ok(int j)
                    {
                        if (opt.Strict && !allowed[i * skin.Joints.Length + j]) return false;
                        // smoothing must not smear a leg across the midline (that is what
                        // tore the boots apart), so the side rule survives the passes
                        if (legSideOf[i] != 0 && IsLegBone(j) && IsLeft(j) != (legSideOf[i] > 0)) return false;
                        // nor may it hand the skirt a thigh, or drag arm weights back onto
                        // the coat through the armpit — those two seams are what made the
                        // tunic flap behind the hands
                        if (rigidToPelvis[i] && IsLegBone(j)) return false;
                        if (!inArm[i] && IsArmBone(j) && !jointName[j].Contains("Shoulder", StringComparison.Ordinal)) return false;
                        return true;
                    }
                    var top = d.Where(kv => kv.Value > 1e-4f && Ok(kv.Key))
                               .OrderByDescending(kv => kv.Value).Take(4).ToList();
                    if (top.Count == 0) top = d.OrderByDescending(kv => kv.Value).Take(1).ToList();
                    float sum = top.Sum(kv => kv.Value);
                    for (int k = 0; k < 4; k++)
                    {
                        if (k < top.Count && sum > 0f) { weights[i * 4 + k] = top[k].Value / sum; boneIdx[i * 4 + k] = top[k].Key; }
                        else { weights[i * 4 + k] = 0f; boneIdx[i * 4 + k] = 0; }
                    }
                }
            }

            // ---- bake the mesh from the source pose into the T-pose -----------------
            // Uses the UNSMOOTHED weights (rawWeights): the bake must put the arms exactly
            // in the skeleton's T-pose, and the smoothed weights let a little of the
            // un-rotated shoulder bone leak down to the wrist (that is a ~6° droop over
            // the arm's length, i.e. every arm pose in the game sits that much too low).
            var baked = new Vector3[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 acc = Vector3.Zero;
                float wsum = 0f;
                for (int k = 0; k < 4; k++)
                {
                    float w = rawWeights[i * 4 + k];
                    if (w <= 0f) continue;
                    int j = rawBones[i * 4 + k];
                    var skinM = Matrix4x4.Invert(fit[jointNode[j]], out var invFit) ? invFit * bind[jointNode[j]] : Matrix4x4.Identity;
                    acc += w * Vector3.Transform(verts[i], skinM);
                    wsum += w;
                }
                baked[i] = wsum > 1e-5f ? acc / wsum : verts[i];
            }
            Console.WriteLine($"baked bbox X {baked.Min(v => v.X):F3}..{baked.Max(v => v.X):F3} " +
                              $"Y {baked.Min(v => v.Y):F3}..{baked.Max(v => v.Y):F3} " +
                              $"Z {baked.Min(v => v.Z):F3}..{baked.Max(v => v.Z):F3}");

            // Sanity check of the bake: the hands must end up in the skeleton's T-pose
            // (arms horizontal). If they hang, the arm weights were diluted by the
            // body/coat and every arm pose in the game will be that much too low.
            foreach (int side in new[] { 1, -1 })
            {
                var handVerts = Enumerable.Range(0, verts.Length)
                    .Where(i => weights[i * 4] > 0f && jointName[boneIdx[i * 4]].Contains("Hand", StringComparison.Ordinal)
                                && MathF.Sign(baked[i].X) == side).ToList();
                if (handVerts.Count == 0) continue;
                var c = Vector3.Zero;
                foreach (int i in handVerts) c += baked[i];
                c /= handVerts.Count;
                // matching shoulder joint (the rig is mirrored, so pick by x sign)
                int sh = -1;
                for (int j = 0; j < skin.Joints.Length; j++)
                    if (jointName[j].EndsWith("Arm", StringComparison.Ordinal) &&
                        MathF.Sign(bind[jointNode[j]].Translation.X) == side) { sh = j; break; }
                if (sh < 0) continue;
                var shPos = bind[jointNode[sh]].Translation;
                float deg = MathF.Atan2(c.Y - shPos.Y, MathF.Abs(c.X - shPos.X)) * 180f / MathF.PI;
                // the skeleton's own arm angle: that is the true target
                int hj = -1;
                for (int j = 0; j < skin.Joints.Length; j++)
                    if (jointName[j].EndsWith("Hand", StringComparison.Ordinal) &&
                        MathF.Sign(bind[jointNode[j]].Translation.X) == side) { hj = j; break; }
                string skel = "";
                if (hj >= 0)
                {
                    var hp = bind[jointNode[hj]].Translation;
                    skel = $"; skeleton {MathF.Atan2(hp.Y - shPos.Y, MathF.Abs(hp.X - shPos.X)) * 180f / MathF.PI:F1}°";
                }
                Console.WriteLine($"baked hand centre ({c.X:F2},{c.Y:F2},{c.Z:F2}) vs shoulder " +
                                  $"({shPos.X:F2},{shPos.Y:F2},{shPos.Z:F2}) = {deg:F1}° from horizontal{skel} " +
                                  $"({handVerts.Count} hand verts)");
            }

            // How much does the bake (arms-down → T-pose) tear the mesh? A triangle
            // that blows up here is a weight seam (a sleeve corner riding an arm and
            // its neighbour riding the hips) and will blow up in every wide pose too.
            var triRatio = new List<(float ratio, int t)>();
            for (int t = 0; t + 2 < srcIdx.Count; t += 3)
            {
                float rest = MaxEdge(verts, srcIdx, t);
                float bakedLen = MaxEdge(baked, srcIdx, t);
                triRatio.Add((bakedLen / MathF.Max(rest, 1e-4f), t / 3));
            }
            var sorted = triRatio.OrderByDescending(r => r.ratio).ToList();
            Console.WriteLine($"bake stretch: median {sorted[sorted.Count / 2].ratio:F2}x, " +
                              $">1.5x {triRatio.Count(r => r.ratio > 1.5f)}, >2x {triRatio.Count(r => r.ratio > 2f)}, " +
                              $">3x {triRatio.Count(r => r.ratio > 3f)}; worst " +
                              string.Join(", ", sorted.Take(6).Select(r => $"{r.ratio:F1}x")));
            if (Environment.GetEnvironmentVariable("RIG_DEBUG") == "1")
                foreach (var r in sorted.Take(12))
                {
                    Console.Write($"   tri {r.t} {r.ratio:F2}x:");
                    for (int c = 0; c < 3; c++)
                    {
                        int v = srcIdx[r.t * 3 + c];
                        Console.Write($"  v{v}(src {srcPos[v].X:F2},{srcPos[v].Y:F2},{srcPos[v].Z:F2} | " +
                                      $"{jointName[boneIdx[v * 4]].Replace("mixamorig:", "")}:{weights[v * 4]:F2})");
                    }
                    Console.WriteLine();
                }

            // The mesh is authored in the same "model space" as the soldier's POSITION
            // accessor, which is NOT world space: IBM * jointWorld is a fixed matrix A
            // for every joint (0.01 scale, Z-up mixamo orientation), and the skinning
            // maths expects raw == inverse(A) * world.
            var A = skin.InverseBind[0] * bind[skin.Joints[0]];
            float worst = 0f;
            for (int j = 1; j < skin.Joints.Length; j++)
            {
                var M = skin.InverseBind[j] * bind[skin.Joints[j]];
                for (int r = 0; r < 4; r++)
                    for (int c = 0; c < 4; c++)
                        worst = MathF.Max(worst, MathF.Abs(M[r, c] - A[r, c]));
            }
            Console.WriteLine($"IBM*bind is uniform: max deviation {worst:E2}");
            if (!Matrix4x4.Invert(A, out var invA)) throw new InvalidDataException("bind matrix not invertible");
            for (int i = 0; i < baked.Length; i++)
                baked[i] = Vector3.Transform(baked[i], invA);
            Console.WriteLine($"stored in model space: bbox X {baked.Min(v => v.X):F1}..{baked.Max(v => v.X):F1} " +
                              $"Y {baked.Min(v => v.Y):F1}..{baked.Max(v => v.Y):F1} " +
                              $"Z {baked.Min(v => v.Z):F1}..{baked.Max(v => v.Z):F1}");

            var keepIdx = srcIdx;
            if (opt.DropStretch > 0f)
            {
                keepIdx = new List<int>();
                for (int t = 0; t + 2 < srcIdx.Count; t += 3)
                {
                    if (triRatio[t / 3].ratio <= opt.DropStretch)
                    { keepIdx.Add(srcIdx[t]); keepIdx.Add(srcIdx[t + 1]); keepIdx.Add(srcIdx[t + 2]); }
                }
                Console.WriteLine($"dropped {srcIdx.Count / 3 - keepIdx.Count / 3} seam triangles " +
                                  $"(> {opt.DropStretch:F1}x in the bake); {keepIdx.Count / 3} left");
            }

            if (opt.Dry)
            {
                Console.WriteLine("dry run: no glb written");
                return;
            }

            // ---- write the skinned GLB ---------------------------------------------
            var g = GlbOut.FromExisting(root, bin);

            // strip the soldier's own meshes from the node tree
            foreach (var n in g.Nodes)
            {
                (n as JObject)?.Remove("mesh");
                (n as JObject)?.Remove("skin");
            }

            var uv = new Vector2[srcUv.Count];
            for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(srcUv[i].X, 1f - srcUv[i].Y);

            int aPos = g.AddVec3(baked, positions: true);
            int aUv = g.AddVec2(uv);
            int aJoints = g.AddUshort4(boneIdx);
            int aWeights = g.AddVec4(weights);
            int aIdx = g.AddIndices(keepIdx.ToArray());

            int tex = -1;
            if (obj.TexturePath != null)
                tex = g.AddTexture(File.ReadAllBytes(obj.TexturePath), Path.GetFileName(obj.TexturePath) ?? "villager.png");
            int mat = g.AddMaterial(tex, "VillagerMale3");

            g.Meshes.Add(new JObject
            {
                ["name"] = "VillagerMale3",
                ["primitives"] = new JArray(new JObject
                {
                    ["attributes"] = new JObject
                    {
                        ["POSITION"] = aPos,
                        ["TEXCOORD_0"] = aUv,
                        ["JOINTS_0"] = aJoints,
                        ["WEIGHTS_0"] = aWeights
                    },
                    ["indices"] = aIdx,
                    ["material"] = mat,
                    ["mode"] = 4
                })
            });

            int nodeIdx = g.AddNode(new JObject { ["name"] = "VillagerMale3", ["mesh"] = 0, ["skin"] = 0 });

            var sceneRoots = new List<int>();
            if (root["scenes"] is JArray scenes && scenes.Count > 0)
            {
                int si = root["scene"] != null ? (int)root["scene"] : 0;
                foreach (var t in (JArray)scenes[si]["nodes"]) sceneRoots.Add((int)t);
            }
            sceneRoots.Add(nodeIdx);
            g.Finalize(opt.OutPath, sceneRoots.ToArray(), "BabbdiCoop Rigger (soldier rig + villager mesh)");

            Console.WriteLine($"wrote {opt.OutPath} ({new FileInfo(opt.OutPath).Length / 1024} KB): " +
                              $"{baked.Length} verts, {keepIdx.Count / 3} tris, " +
                              $"{g.Nodes.Count} nodes, {g.Skins.Count} skins, {g.Animations.Count} clips");
        }

        static float MaxEdge(Vector3[] pos, List<int> idx, int t)
        {
            var a = pos[idx[t]]; var b = pos[idx[t + 1]]; var c = pos[idx[t + 2]];
            return MathF.Max((a - b).Length(), MathF.Max((b - c).Length(), (c - a).Length()));
        }

        /// <summary>
        /// Surface distance (in source units) from the fingertips to every welded vertex,
        /// by Dijkstra over the welded mesh graph. Used to tell the arm from the coat:
        /// they hang side by side in space but meet only at the armpit.
        /// </summary>
        static float[] Geodesic(List<Vector3> pos, List<int> idx, int[] weld, int nWeld,
                                float y0, float y1)
        {
            var dist = new float[nWeld];
            for (int i = 0; i < nWeld; i++) dist[i] = float.MaxValue;
            var nb = new List<(int to, float w)>[nWeld];
            void Edge(int a, int b, float w)
            {
                (nb[a] ??= new List<(int, float)>()).Add((b, w));
                (nb[b] ??= new List<(int, float)>()).Add((a, w));
            }
            for (int t = 0; t + 2 < idx.Count; t += 3)
            {
                int a = weld[idx[t]], b = weld[idx[t + 1]], c = weld[idx[t + 2]];
                Edge(a, b, (pos[idx[t]] - pos[idx[t + 1]]).Length());
                Edge(b, c, (pos[idx[t + 1]] - pos[idx[t + 2]]).Length());
                Edge(c, a, (pos[idx[t + 2]] - pos[idx[t]]).Length());
            }
            float hgt = y1 - y0;
            var pq = new PriorityQueue<int, float>();
            var seeded = new HashSet<int>();
            for (int i = 0; i < pos.Count; i++)
            {
                float yf = (pos[i].Y - y0) / hgt;
                if (yf < 0.45f && MathF.Abs(pos[i].X) > 0.11f * hgt && seeded.Add(weld[i]))
                {
                    dist[weld[i]] = 0f;
                    pq.Enqueue(weld[i], 0f);
                }
            }
            while (pq.TryDequeue(out int v, out float d))
            {
                if (d > dist[v] + 1e-6f) continue;
                if (nb[v] == null) continue;
                foreach (var (to, w) in nb[v])
                    if (d + w < dist[to] - 1e-6f) { dist[to] = d + w; pq.Enqueue(to, dist[to]); }
            }
            return dist;
        }

        static float DistToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            float len2 = ab.LengthSquared();
            float t = len2 > 1e-9f ? Math.Clamp(Vector3.Dot(p - a, ab) / len2, 0f, 1f) : 0f;
            return (p - (a + ab * t)).Length();
        }
    }
}
