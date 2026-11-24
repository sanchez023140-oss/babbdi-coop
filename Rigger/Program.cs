using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json.Linq;

namespace Rigger
{
    static class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("usage:");
                Console.WriteLine("  Rigger static <in.obj> <out.glb>");
                Console.WriteLine("  Rigger rig <skeleton.glb> <in.obj> <out.glb>");
                return;
            }

            switch (args[0])
            {
                case "static":
                    Static(args[1], args[2]);
                    break;
                case "rig":
                {
                    var o = new Rigging.Options { OutPath = args.Length > 3 ? args[3] : "villager.glb" };
                    for (int i = 4; i < args.Length; i++)
                    {
                        var a = args[i];
                        float V(float def) => a.Contains('=') ? float.Parse(a.Split('=')[1], System.Globalization.CultureInfo.InvariantCulture) : def;
                        if (a == "dry") o.Dry = true;
                        else if (a.StartsWith("arm=")) o.ArmDownDeg = V(78);
                        else if (a.StartsWith("fwd=")) o.ArmForwardDeg = V(6);
                        else if (a.StartsWith("elbow=")) o.ElbowDeg = V(6);
                        else if (a.StartsWith("pow=")) o.WeightPower = V(4);
                        else if (a.StartsWith("margin=")) o.SideMargin = V(0.04f);
                        else if (a.StartsWith("inf=")) o.MaxInfluences = (int)V(4);
                        else if (a.StartsWith("smooth=")) o.SmoothIters = (int)V(2);
                        else if (a.StartsWith("alpha=")) o.SmoothAlpha = V(0.5f);
                        else if (a.StartsWith("drop=")) o.DropStretch = V(0f);
                        else if (a.StartsWith("armcut=")) o.ArmCut = V(0.21f);
                        else if (a.StartsWith("cloth=")) o.ClothAbove = V(0.38f);
                        else if (a.StartsWith("geod=")) o.GeodesicArm = V(1) != 0f;
                        else if (a == "nox") o.GeodesicArm = false;
                        else if (a == "strict" || a.StartsWith("strict=")) o.Strict = a == "strict" || V(1) != 0f;
                        else if (a.StartsWith("out=")) o.OutPath = a.Substring(4);
                    }
                    Rigging.Rig(args[1], args[2], o);
                    break;
                }
                case "views":
                    Views(args[1], args.Length > 2 ? args[2] : null, args.Length > 3 ? args[3] : "views");
                    break;
                case "profile":
                    Profile(args[1]);
                    break;
                case "info":
                    Info(args[1]);
                    break;
                case "components":
                    Components(args[1]);
                    break;
                case "verts":
                    Verts(args[1]);
                    break;
                case "check":
                    Check(args[1]);
                    break;
                case "geodesy":
                    Geodesy(args[1]);
                    break;
                case "stretch":
                    Stretch(args[1], args.Length > 2 ? args[2] : null);
                    break;
                case "whitetris":
                    WhiteTris(args[1]);
                    break;
                case "wcol":
                    WeightColors(args[1], args.Length > 2 ? args[2] : "wcol", args.Length > 3 ? args[3] : null);
                    break;
                case "vlabels":
                    VLabels(args[1], args.Length > 2 ? args[2] : "vlab",
                            args.Length > 3 ? float.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture) : float.MinValue,
                            args.Length > 4 ? float.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture) : float.MaxValue);
                    break;
                case "faces":
                    Faces(args[1], args.Length > 2 ? float.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) : float.MinValue,
                                 args.Length > 3 ? float.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture) : float.MaxValue);
                    break;
                case "bones":
                    Bones(args[1], args.Length > 2 ? args[2] : null);
                    break;
                default:
                    Console.WriteLine("unknown command " + args[0]);
                    break;
            }
        }

        /// <summary>OBJ → plain (unskinned) GLB, mostly for eyeballing the source model.</summary>
        static void Static(string objPath, string outPath)
        {
            var obj = ObjMesh.Load(objPath);
            obj.BuildUnique(out var pos, out var uv, out var idx);

            var g = new GlbOut();
            int aPos = g.AddVec3(pos.ToArray(), positions: true);
            int aUv = g.AddVec2(uv.ToArray());
            // OBJ v is bottom-up, glTF v is top-down
            for (int i = 0; i < uv.Count; i++) { }
            var uvFlipped = new Vector2[uv.Count];
            for (int i = 0; i < uv.Count; i++) uvFlipped[i] = new Vector2(uv[i].X, 1f - uv[i].Y);
            aUv = g.AddVec2(uvFlipped);
            int aIdx = g.AddIndices(idx.ToArray());

            int tex = -1;
            if (obj.TexturePath != null)
                tex = g.AddTexture(File.ReadAllBytes(obj.TexturePath), Path.GetFileName(obj.TexturePath));
            int mat = g.AddMaterial(tex, "VillagerMale3");

            g.Meshes.Add(new JObject
            {
                ["name"] = "VillagerMale3",
                ["primitives"] = new JArray(new JObject
                {
                    ["attributes"] = new JObject
                    {
                        ["POSITION"] = aPos,
                        ["TEXCOORD_0"] = aUv
                    },
                    ["indices"] = aIdx,
                    ["material"] = mat,
                    ["mode"] = 4
                })
            });

            int node = g.AddNode(new JObject { ["name"] = "VillagerMale3", ["mesh"] = 0 });
            g.Finalize(outPath, new[] { node });

            Vector3 mn = new Vector3(float.MaxValue), mx = new Vector3(float.MinValue);
            foreach (var p in pos) { mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p); }
            Console.WriteLine($"wrote {outPath}: {pos.Count} verts, {idx.Count / 3} tris, " +
                              $"bbox {mn} .. {mx} (h {mx.Y - mn.Y:F3})");
        }

        static void Views(string glb, string clip, string tag)
        {
            var parsed = BabbdiCoop.GlbModelLoader.Parse(glb);
            if (string.IsNullOrEmpty(clip) || clip == "-") clip = null;
            if (clip != null)
            {
                var parts = clip.Split(':');
                float t = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                PoseSampler.Apply(parsed, parts[0], t);
            }
            var tris = Render.FromParsed(parsed);
            var dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "harness_out");
            Directory.CreateDirectory(dir);
            var front = Render.View(tris, Path.Combine(dir, $"{tag}_front.png"), new Vector3(0, 0, 1), $"{tag} front (+Z)");
            Render.View(tris, Path.Combine(dir, $"{tag}_back.png"), new Vector3(0, 0, -1), $"{tag} back (-Z)");
            Render.View(tris, Path.Combine(dir, $"{tag}_side.png"), new Vector3(1, 0, 0), $"{tag} side (+X)");
            Console.WriteLine($"rendered {tag}: {front}");
            Console.WriteLine($"bounds Y {parsed.Min.Y:F3}..{parsed.Max.Y:F3} h={parsed.Max.Y - parsed.Min.Y:F3} " +
                              $"X {parsed.Min.X:F3}..{parsed.Max.X:F3} Z {parsed.Min.Z:F3}..{parsed.Max.Z:F3}");
        }

        /// <summary>Horizontal extent per Y band — tells where the arms/legs are.</summary>
        static void Profile(string objPath)
        {
            var obj = ObjMesh.Load(objPath);
            var pos = obj.Positions;
            float y0 = pos.Min(p => p.Y), y1 = pos.Max(p => p.Y);
            int bands = 40;
            Console.WriteLine($"height {y1 - y0:F4}, {pos.Count} verts");
            Console.WriteLine("   y     frac    xmin    xmax    width   zmin    zmax    n");
            for (int b = bands - 1; b >= 0; b--)
            {
                float ya = y0 + (y1 - y0) * b / bands;
                float yb = y0 + (y1 - y0) * (b + 1) / bands;
                var sel = pos.Where(p => p.Y >= ya && p.Y <= yb).ToList();
                if (sel.Count == 0) { Console.WriteLine($"{ya:F3}  {(ya - y0) / (y1 - y0):F2}   (empty)"); continue; }
                float xmin = sel.Min(p => p.X), xmax = sel.Max(p => p.X);
                float zmin = sel.Min(p => p.Z), zmax = sel.Max(p => p.Z);
                Console.WriteLine($"{ya:F3}  {(ya - y0) / (y1 - y0):F2}  {xmin,7:F3} {xmax,7:F3} {xmax - xmin,7:F3} {zmin,7:F3} {zmax,7:F3} {sel.Count,4}");
            }
        }

        /// <summary>Dumps the welded (deduplicated) vertex positions, top-down by Y.</summary>
        static void Verts(string objPath)
        {
            var obj = ObjMesh.Load(objPath);
            obj.BuildUnique(out var pos, out _, out _);
            var seen = new HashSet<(int, int, int)>();
            var uniq = new List<Vector3>();
            foreach (var p in pos)
            {
                var k = ((int)MathF.Round(p.X * 10000), (int)MathF.Round(p.Y * 10000), (int)MathF.Round(p.Z * 10000));
                if (seen.Add(k)) uniq.Add(p);
            }
            Console.WriteLine($"{uniq.Count} unique positions");
            int i = 0;
            foreach (var p in uniq.OrderByDescending(p => p.Y))
                Console.WriteLine($"{i++,4}: {p.X,7:F3} {p.Y,7:F3} {p.Z,7:F3}");
        }

        /// <summary>Verifies IBM * jointWorld == identity at bind and prints the mesh node world.</summary>
        static void Check(string glb)
        {
            var m = BabbdiCoop.GlbModelLoader.Parse(glb, "");
            var skin = m.Skins[0];
            // worlds (row-vector: local * parentWorld)
            var world = new Matrix4x4[m.Nodes.Count];
            var done = new bool[m.Nodes.Count];
            Matrix4x4 World(int i)
            {
                if (done[i]) return world[i];
                done[i] = true;
                var n = m.Nodes[i];
                var local = Matrix4x4.CreateScale(n.S) * Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(n.R)) * Matrix4x4.CreateTranslation(n.T);
                world[i] = n.Parent >= 0 ? local * World(n.Parent) : local;
                return world[i];
            }
            for (int i = 0; i < m.Nodes.Count; i++) World(i);

            for (int j = 0; j < Math.Min(4, skin.Joints.Length); j++)
            {
                var M = skin.InverseBind[j] * world[skin.Joints[j]];
                Console.WriteLine($"joint {j} ({m.Nodes[skin.Joints[j]].Name})");
                Console.WriteLine($"   IBM*W  [{M.M11:F4} {M.M12:F4} {M.M13:F4} {M.M14:F4}]");
                Console.WriteLine($"          [{M.M21:F4} {M.M22:F4} {M.M23:F4} {M.M24:F4}]");
                Console.WriteLine($"          [{M.M31:F4} {M.M32:F4} {M.M33:F4} {M.M34:F4}]");
                Console.WriteLine($"          [{M.M41:F4} {M.M42:F4} {M.M43:F4} {M.M44:F4}]");
                Console.WriteLine($"   joint world T = {world[skin.Joints[j]].Translation}");
            }
            foreach (var p in m.Primitives)
            {
                Console.WriteLine($"prim {p.Name}: raw0 {p.RawPositions[0]}, skinned0 {p.Positions[0]}, " +
                                  $"w0 [{p.BoneWeights[0]:F3} {p.BoneWeights[1]:F3} {p.BoneWeights[2]:F3} {p.BoneWeights[3]:F3}] " +
                                  $"j0 [{p.BoneIndices[0]} {p.BoneIndices[1]} {p.BoneIndices[2]} {p.BoneIndices[3]}]");
            }
            // which node carries the mesh?
            for (int i = 0; i < m.Nodes.Count; i++)
                if (m.Nodes[i].Mesh >= 0)
                    Console.WriteLine($"mesh node {i} '{m.Nodes[i].Name}' skin={m.Nodes[i].Skin} world T={world[i].Translation} " +
                                      $"scale~{world[i].GetDeterminant():F6}");
        }

        static void Info(string glb)        {
            var m = BabbdiCoop.GlbModelLoader.Parse(glb, "");
            Console.WriteLine($"nodes {m.Nodes.Count}, skins {m.Skins.Count}, prims {m.Primitives.Count}, anims {m.Animations.Count}");
            Console.WriteLine($"bind bounds Y {m.Min.Y:F3}..{m.Max.Y:F3}  X {m.Min.X:F3}..{m.Max.X:F3}  Z {m.Min.Z:F3}..{m.Max.Z:F3}");
            for (int s = 0; s < m.Skins.Count; s++)
            {
                var skin = m.Skins[s];
                Console.WriteLine($"skin {s}: {skin.Joints.Length} joints, ibm {skin.InverseBind.Length}");
                Console.WriteLine("   " + string.Join(", ", skin.Joints.Select(j => m.Nodes[j].Name)));
            }
            foreach (var p in m.Primitives)
                Console.WriteLine($"prim '{p.Name}': {p.RawPositions?.Length ?? 0} verts, {p.Indices?.Length / 3 ?? 0} tris, skin {p.SkinIndex}");
            foreach (var a in m.Animations)
                Console.WriteLine($"anim '{a.Name}': {a.Duration:F3}s, {a.Channels.Count} channels");
        }

        /// <summary>
        /// Mesh-graph distances: how far each vertex is along the surface from the
        /// hands. Used to tell the arm from the coat — they touch in space, so only
        /// connectivity separates them.
        /// </summary>
        static void Geodesy(string objPath)
        {
            var obj = ObjMesh.Load(objPath);
            obj.BuildUnique(out var pos, out var uv, out var idx);
            float y0 = pos.Min(p => p.Y), y1 = pos.Max(p => p.Y);

            var key = new Dictionary<(int, int, int), int>();
            var weld = new int[pos.Count];
            var uniq = new List<Vector3>();
            for (int i = 0; i < pos.Count; i++)
            {
                var k = ((int)MathF.Round(pos[i].X * 10000), (int)MathF.Round(pos[i].Y * 10000), (int)MathF.Round(pos[i].Z * 10000));
                if (!key.TryGetValue(k, out int u)) { u = uniq.Count; key[k] = u; uniq.Add(pos[i]); }
                weld[i] = u;
            }

            // edges of the welded graph, weighted by rest length
            var edges = new Dictionary<int, List<(int to, float w)>>();
            void Edge(int a, int b)
            {
                float w = (uniq[a] - uniq[b]).Length();
                if (!edges.TryGetValue(a, out var la)) edges[a] = la = new List<(int, float)>();
                la.Add((b, w));
                if (!edges.TryGetValue(b, out var lb)) edges[b] = lb = new List<(int, float)>();
                lb.Add((a, w));
            }
            for (int t = 0; t < idx.Count; t += 3)
            {
                int a = weld[idx[t]], b = weld[idx[t + 1]], c = weld[idx[t + 2]];
                Edge(a, b); Edge(b, c); Edge(c, a);
            }

            // seeds: the outer hand verts only (the tunic hem sits 2 cm away in space
            // but is a different surface branch, so |x| must be tight)
            var dist = Enumerable.Repeat(float.MaxValue, uniq.Count).ToArray();
            var q = new PriorityQueue<int, float>();
            for (int i = 0; i < uniq.Count; i++)
            {
                float yf = (uniq[i].Y - y0) / (y1 - y0);
                if (yf < 0.45f && MathF.Abs(uniq[i].X) > 0.06f) { dist[i] = 0f; q.Enqueue(i, 0f); }
            }
            while (q.TryDequeue(out int v, out float d))
            {
                if (d > dist[v] + 1e-6f) continue;
                if (!edges.TryGetValue(v, out var nb)) continue;
                foreach (var (to, w) in nb)
                    if (d + w < dist[to] - 1e-6f) { dist[to] = d + w; q.Enqueue(to, dist[to]); }
            }

            float h = y1 - y0;
            float Gate(float yf)
            {
                if (yf > 0.84f) return 0.058f;
                if (yf > 0.76f) return 0.101f;
                if (yf > 0.62f) return 0.070f;
                if (yf > 0.48f) return 0.066f;
                if (yf > 0.38f) return 0.074f;
                return 0.11f;
            }

            int seeds = dist.Count(d => d == 0f);
            Console.WriteLine($"{uniq.Count} welded verts, {seeds} hand seeds, height {h:F3}");
            Console.WriteLine("  yf     x      z    |x|   gate  gate?   geo   geo/h");
            foreach (var (i, p) in uniq.Select((p, i) => (i, p)).OrderByDescending(t => t.p.Y))
            {
                float yf = (p.Y - y0) / h;
                bool gated = MathF.Abs(p.X) > Gate(yf) * h;
                Console.WriteLine($"{yf,5:F2} {p.X,6:F3} {p.Z,6:F3} {MathF.Abs(p.X),5:F3} {Gate(yf),5:F3} " +
                                  $"{(gated ? "ARM" : "   ")} {dist[i],7:F3} {dist[i] / h,6:F2}");
            }
        }

        /// <summary>
        /// Worst stretched triangles in a posed skinned GLB: a triangle whose edges
        /// blow up is a weight discontinuity (one corner rides another bone).
        /// </summary>
        static void Stretch(string glb, string clip)
        {
            var m = BabbdiCoop.GlbModelLoader.Parse(glb);
            if (!string.IsNullOrEmpty(clip) && clip != "-")
            {
                var parts = clip.Split(':');
                PoseSampler.Apply(m, parts[0], float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
            }
            var skin = m.Skins[0];
            string BoneName(int j) => m.Nodes[skin.Joints[j]].Name;
            foreach (var p in m.Primitives)
            {
                if (p.Positions == null || p.Indices == null) continue;
                var rows = new List<(float len, int a, int b, int c, float la, float lb, float lc)>();
                for (int i = 0; i + 2 < p.Indices.Length; i += 3)
                {
                    int a = p.Indices[i], b = p.Indices[i + 1], c = p.Indices[i + 2];
                    var pa = p.Positions[a]; var pb = p.Positions[b]; var pc = p.Positions[c];
                    var ra = p.RawPositions[a]; var rb = p.RawPositions[b]; var rc = p.RawPositions[c];
                    float d = MathF.Max((pa - pb).Length(), MathF.Max((pb - pc).Length(), (pc - pa).Length()));
                    float r = MathF.Max((ra - rb).Length(), MathF.Max((rb - rc).Length(), (rc - ra).Length()));
                    rows.Add((d, a, b, c, r, 0f, 0f));
                }
                Console.WriteLine($"prim '{p.Name}': {rows.Count} tris");
                Console.WriteLine("  posed longest edge | rest longest edge | ratio | verts (posed) | bones");
                foreach (var r in rows.OrderByDescending(r => r.len).Take(48))
                {
                    var who = new List<string>();
                    foreach (int v in new[] { r.a, r.b, r.c })
                    {
                        var bs = new List<string>();
                        for (int k = 0; k < 4; k++)
                        {
                            float w = p.BoneWeights[v * 4 + k];
                            if (w <= 0.001f) continue;
                            bs.Add($"{BoneName(p.BoneIndices[v * 4 + k]).Replace("mixamorig:", "")}:{w:F2}");
                        }
                        who.Add($"[{v}] ({p.Positions[v].X:F2},{p.Positions[v].Y:F2},{p.Positions[v].Z:F2}) {{{string.Join(" ", bs)}}}");
                    }
                    Console.WriteLine($"  {r.len,6:F3} | {r.la,6:F3} | {r.len / MathF.Max(r.la, 1e-4f),5:F1}x | " +
                                      $"rest bbox? raw a=({p.RawPositions[r.a].X:F0},{p.RawPositions[r.a].Y:F0},{p.RawPositions[r.a].Z:F0})");
                    foreach (var s in who) Console.WriteLine("        " + s);
                }
            }
        }

        /// <summary>
        /// Triangles whose UVs land in the unused (white) part of the atlas: the PS1
        /// rip relies on them being hidden, and they flare up as white shards as soon
        /// as the skinning moves the boot.
        /// </summary>
        static void WhiteTris(string objPath)
        {
            var obj = ObjMesh.Load(objPath);
            obj.BuildUnique(out var pos, out var uv, out var idx);
            using var tex = new System.Drawing.Bitmap(obj.TexturePath);
            bool White(int x, int y)
            {
                var c = tex.GetPixel(Math.Clamp(x, 0, tex.Width - 1), Math.Clamp(y, 0, tex.Height - 1));
                return c.R > 245 && c.G > 245 && c.B > 245;
            }
            // glTF v is flipped vs OBJ
            bool WhiteUv(Vector2 t) => White((int)(t.X * (tex.Width - 1)), (int)((1f - t.Y) * (tex.Height - 1)));

            int white = 0;
            var y0 = pos.Min(p => p.Y);
            for (int t = 0; t + 2 < idx.Count; t += 3)
            {
                int a = idx[t], b = idx[t + 1], c = idx[t + 2];
                bool w = WhiteUv(uv[a]) || WhiteUv(uv[b]) || WhiteUv(uv[c]);
                if (!w) continue;
                white++;
                var cen = (pos[a] + pos[b] + pos[c]) / 3f;
                Console.WriteLine($"tri {t / 3}: centroid y={cen.Y - y0:F3} x={cen.X:F3} z={cen.Z:F3}  " +
                                  $"uv {uv[a]} {uv[b]} {uv[c]}  white[a,b,c]={WhiteUv(uv[a])},{WhiteUv(uv[b])},{WhiteUv(uv[c])}");
            }
            Console.WriteLine($"texture {tex.Width}x{tex.Height}, {white} of {idx.Count / 3} tris touch white");
        }

        /// <summary>
        /// Paints every triangle with the colour of its dominant bone. The quickest way
        /// to see where the auto-weights actually put the arm / the coat / the legs.
        /// </summary>
        static void WeightColors(string glb, string tag, string clip)
        {
            var m = BabbdiCoop.GlbModelLoader.Parse(glb);
            if (!string.IsNullOrEmpty(clip) && clip != "-")
            {
                var parts = clip.Split(':');
                PoseSampler.Apply(m, parts[0], float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
            }
            var skin = m.Skins[0];
            var palette = new Dictionary<string, System.Drawing.Color>();
            System.Drawing.Color Col(string bone)
            {
                if (palette.TryGetValue(bone, out var c)) return c;
                string b = bone.Replace("mixamorig:", "");
                bool left = b.StartsWith("Left", StringComparison.Ordinal);
                bool right = b.StartsWith("Right", StringComparison.Ordinal);
                if (b.Contains("Hand") || b.Contains("ForeArm") || b.EndsWith("Arm") || b.Contains("Shoulder"))
                    c = left ? System.Drawing.Color.FromArgb(255, 170, 40) : right ? System.Drawing.Color.FromArgb(255, 110, 0) : System.Drawing.Color.FromArgb(255, 220, 140);
                else if (b.Contains("Leg") || b.Contains("Foot") || b.Contains("Toe"))
                    c = left ? System.Drawing.Color.FromArgb(80, 130, 255) : right ? System.Drawing.Color.FromArgb(20, 70, 200) : System.Drawing.Color.FromArgb(120, 200, 255);
                else if (b.Contains("Head") || b.Contains("Neck"))
                    c = System.Drawing.Color.FromArgb(230, 70, 70);
                else if (b.Contains("Spine") || b.Contains("Hips"))
                    c = left ? System.Drawing.Color.FromArgb(120, 200, 90) : right ? System.Drawing.Color.FromArgb(40, 150, 60) : System.Drawing.Color.FromArgb(90, 190, 90);
                else c = System.Drawing.Color.FromArgb(200, 200, 200);
                palette[bone] = c;
                return c;
            }

            var tris = new List<Render.Tri>();
            foreach (var p in m.Primitives)
            {
                if (p.Positions == null || p.Indices == null) continue;
                var texFor = new Dictionary<int, System.Drawing.Bitmap>();
                for (int i = 0; i + 2 < p.Indices.Length; i += 3)
                {
                    int a = p.Indices[i], b = p.Indices[i + 1], c = p.Indices[i + 2];
                    // dominate by summed weight per bone
                    var acc = new Dictionary<int, float>();
                    foreach (int v in new[] { a, b, c })
                        for (int k = 0; k < 4; k++)
                        {
                            float w = p.BoneWeights[v * 4 + k];
                            if (w <= 0f) continue;
                            int j = p.BoneIndices[v * 4 + k];
                            acc[j] = acc.TryGetValue(j, out float cur) ? cur + w : w;
                        }
                    int best = acc.Count > 0 ? acc.OrderByDescending(kv => kv.Value).First().Key : -1;
                    string bone = best >= 0 ? (m.Nodes[skin.Joints[best]].Name ?? "?") : "?";
                    var col = Col(bone);
                    if (!texFor.TryGetValue(best, out var bmp))
                    {
                        bmp = new System.Drawing.Bitmap(1, 1);
                        bmp.SetPixel(0, 0, col);
                        texFor[best] = bmp;
                    }
                    var one = new Vector2(0.5f, 0.5f);
                    tris.Add(new Render.Tri { A = p.Positions[a], B = p.Positions[b], C = p.Positions[c], UA = one, UB = one, UC = one, Tex = bmp });
                }
            }
            var dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "harness_out");
            Directory.CreateDirectory(dir);
            foreach (var (name, d) in new[] { ("front", new Vector3(0, 0, -1)), ("back", new Vector3(0, 0, 1)), ("side", new Vector3(1, 0, 0)) })
                Render.View(tris, Path.Combine(dir, $"{tag}_{name}.png"), d, $"{tag} {name} (dominant bone)");
            Console.WriteLine($"wrote {tag}_front/back/side.png; legend: orange=arm/hand, blue=leg, green=torso, red=head");
            foreach (var kv in palette.OrderBy(kv => kv.Key))
                Console.WriteLine($"   {kv.Key,-32} #{kv.Value.R:X2}{kv.Value.G:X2}{kv.Value.B:X2}");
        }

        /// <summary>Prints joint world positions (row-matrix convention) for a posed rig.</summary>
        static void Bones(string glb, string clip)
        {
            var m = BabbdiCoop.GlbModelLoader.Parse(glb);
            if (!string.IsNullOrEmpty(clip) && clip != "-")
            {
                var parts = clip.Split(':');
                PoseSampler.Apply(m, parts[0], float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
            }
            var world = new Matrix4x4[m.Nodes.Count];
            var done = new bool[m.Nodes.Count];
            Matrix4x4 World(int i)
            {
                if (done[i]) return world[i];
                done[i] = true;
                var n = m.Nodes[i];
                var local = Matrix4x4.CreateScale(n.S) * Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(n.R)) * Matrix4x4.CreateTranslation(n.T);
                world[i] = n.Parent >= 0 ? local * World(n.Parent) : local;
                return world[i];
            }
            for (int i = 0; i < m.Nodes.Count; i++) World(i);
            foreach (var prim in m.Primitives)
            {
                if (prim.Positions == null) continue;
                foreach (var bn in new[] { "LeftHand", "LeftForeArm", "LeftArm", "Hips" })
                {
                    var sel = new List<Vector3>();
                    for (int i = 0; i < prim.Positions.Length; i++)
                    {
                        if (prim.BoneIndices == null) break;
                        if (!(m.Nodes[m.Skins[prim.SkinIndex].Joints[prim.BoneIndices[i * 4]]].Name ?? "").EndsWith(bn, StringComparison.Ordinal)) continue;
                        if (prim.BoneWeights[i * 4] < 0.5f) continue;
                        sel.Add(prim.Positions[i]);
                    }
                    if (sel.Count == 0) continue;
                    var c = Vector3.Zero;
                    foreach (var p in sel) c += p;
                    c /= sel.Count;
                    Console.WriteLine($"  dominant {bn,-12} n={sel.Count,4} centre ({c.X,7:F3},{c.Y,7:F3},{c.Z,7:F3})");
                }
            }
            Console.WriteLine($"{(clip ?? "bind")}:");
            foreach (var name in new[] { "Hips", "Spine", "Spine1", "Spine2", "Neck", "Head",
                                         "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand",
                                         "RightShoulder", "RightArm", "RightForeArm", "RightHand",
                                         "LeftUpLeg", "LeftLeg", "LeftFoot" })
                for (int i = 0; i < m.Nodes.Count; i++)
                    if ((m.Nodes[i].Name ?? "").EndsWith(name, StringComparison.Ordinal))
                    {
                        var p = world[i].Translation;
                        var r = Quaternion.CreateFromRotationMatrix(world[i]);
                        Console.WriteLine($"  {name,-14} ({p.X,7:F3},{p.Y,7:F3},{p.Z,7:F3})  rot ({r.X,6:F3},{r.Y,6:F3},{r.Z,6:F3},{r.W,6:F3})");
                        break;
                    }
        }

        /// <summary>Prints triangles (welded ids) that touch a Y window, one per line.</summary>
        static void Faces(string objPath, float yLo, float yHi)
        {
            var obj = ObjMesh.Load(objPath);
            obj.BuildUnique(out var pos, out _, out var idx);
            var key = new Dictionary<(int, int, int), int>();
            var weld = new int[pos.Count];
            var uniq = new List<Vector3>();
            for (int i = 0; i < pos.Count; i++)
            {
                var k = ((int)MathF.Round(pos[i].X * 10000), (int)MathF.Round(pos[i].Y * 10000), (int)MathF.Round(pos[i].Z * 10000));
                if (!key.TryGetValue(k, out int u)) { u = uniq.Count; key[k] = u; uniq.Add(pos[i]); }
                weld[i] = u;
            }
            var order = Enumerable.Range(0, uniq.Count).OrderByDescending(i => uniq[i].Y).ToList();
            var idOf = new int[uniq.Count];
            for (int i = 0; i < order.Count; i++) idOf[order[i]] = i;
            string P(int u) => $"{idOf[u],4}({uniq[u].X,6:F3},{uniq[u].Y,6:F3},{uniq[u].Z,6:F3})";

            int shown = 0;
            for (int t = 0; t + 2 < idx.Count; t += 3)
            {
                var a = weld[idx[t]]; var b = weld[idx[t + 1]]; var c = weld[idx[t + 2]];
                float ym = (uniq[a].Y + uniq[b].Y + uniq[c].Y) / 3f;
                if (ym < yLo || ym > yHi) continue;
                Console.WriteLine($"tri {t / 3,3}: {P(a)} {P(b)} {P(c)}");
                shown++;
            }
            Console.WriteLine($"{shown} of {idx.Count / 3} triangles in Y {yLo:F3}..{yHi:F3}");
        }

        /// <summary>Renders the source mesh with vertex indices drawn next to each welded vertex.</summary>
        static void VLabels(string objPath, string tag, float yLo, float yHi)
        {
            var obj = ObjMesh.Load(objPath);
            obj.BuildUnique(out var pos, out var uv, out var idx);

            // weld, keeping the numbering used by the `verts` command (top-down by Y)
            var key = new Dictionary<(int, int, int), int>();
            var weld = new int[pos.Count];
            var uniq = new List<Vector3>();
            for (int i = 0; i < pos.Count; i++)
            {
                var k = ((int)MathF.Round(pos[i].X * 10000), (int)MathF.Round(pos[i].Y * 10000), (int)MathF.Round(pos[i].Z * 10000));
                if (!key.TryGetValue(k, out int u)) { u = uniq.Count; key[k] = u; uniq.Add(pos[i]); }
                weld[i] = u;
            }
            var order = Enumerable.Range(0, uniq.Count).OrderByDescending(i => uniq[i].Y).ToList();
            var idOf = new int[uniq.Count];
            for (int i = 0; i < order.Count; i++) idOf[order[i]] = i;

            var tris = new List<Render.Tri>();
            System.Drawing.Bitmap tex = null;
            if (obj.TexturePath != null && File.Exists(obj.TexturePath)) tex = new System.Drawing.Bitmap(obj.TexturePath);
            for (int t = 0; t + 2 < idx.Count; t += 3)
            {
                int a = idx[t], b = idx[t + 1], c = idx[t + 2];
                tris.Add(new Render.Tri { A = pos[a], B = pos[b], C = pos[c], UA = uv[a], UB = uv[b], UC = uv[c], Tex = tex });
            }
            var labels = new List<(Vector3, string)>();
            foreach (int u in order)
            {
                var p = uniq[u];
                if (p.Y < yLo || p.Y > yHi) continue;
                labels.Add((p, idOf[u].ToString()));
            }
            var dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "harness_out");
            Directory.CreateDirectory(dir);
            Render.View(tris, Path.Combine(dir, $"{tag}_front.png"), new Vector3(0, 0, 1), $"{tag} front", 900, 1250, true, true, labels, true);
            Render.View(tris, Path.Combine(dir, $"{tag}_side.png"), new Vector3(1, 0, 0), $"{tag} side", 900, 1250, true, true, labels, true);
            Render.View(tris, Path.Combine(dir, $"{tag}_back.png"), new Vector3(0, 0, -1), $"{tag} back", 900, 1250, true, true, labels, true);
            Console.WriteLine($"wrote {tag}_front/side/back.png with {labels.Count} labels (Y {yLo:F3}..{yHi:F3})");
        }

        /// <summary>Welds split vertices by position and prints connected shells.</summary>
        static void Components(string objPath)        {
            var obj = ObjMesh.Load(objPath);
            obj.BuildUnique(out var pos, out var uv, out var idx);
            // weld by position (the rips duplicate every vertex per triangle)
            var key = new Dictionary<(int, int, int), int>();
            var weld = new int[pos.Count];
            var uniq = new List<Vector3>();
            for (int i = 0; i < pos.Count; i++)
            {
                var k = ((int)MathF.Round(pos[i].X * 10000), (int)MathF.Round(pos[i].Y * 10000), (int)MathF.Round(pos[i].Z * 10000));
                if (!key.TryGetValue(k, out int u)) { u = uniq.Count; key[k] = u; uniq.Add(pos[i]); }
                weld[i] = u;
            }
            var parent = Enumerable.Range(0, uniq.Count).ToArray();
            int Find(int a) { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; }
            void Union(int a, int b) { a = Find(a); b = Find(b); if (a != b) parent[b] = a; }
            for (int t = 0; t < idx.Count; t += 3)
            {
                Union(weld[idx[t]], weld[idx[t + 1]]);
                Union(weld[idx[t]], weld[idx[t + 2]]);
            }
            var groups = new Dictionary<int, List<int>>();
            for (int i = 0; i < uniq.Count; i++)
            {
                int r = Find(i);
                if (!groups.TryGetValue(r, out var g)) groups[r] = g = new List<int>();
                g.Add(i);
            }
            Console.WriteLine($"welded {uniq.Count} unique positions → {groups.Count} shells");
            int n = 0;
            foreach (var g in groups.OrderByDescending(g => g.Value.Count))
            {
                var min = new Vector3(float.MaxValue); var max = new Vector3(float.MinValue);
                foreach (var i in g.Value) { min = Vector3.Min(min, uniq[i]); max = Vector3.Max(max, uniq[i]); }
                Console.WriteLine($"  shell {n++}: {g.Value.Count,4} verts  X {min.X:F3}..{max.X:F3}  Y {min.Y:F3}..{max.Y:F3}  Z {min.Z:F3}..{max.Z:F3}");
            }
        }
    }
}
