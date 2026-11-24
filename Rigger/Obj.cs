using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;

namespace Rigger
{
    /// <summary>
    /// Minimal Wavefront OBJ reader (v / vt / vn / f, triangles and quads).
    /// Faces are expanded into an unindexed triangle soup with per-corner UVs,
    /// which is exactly how the Soul Reaver rips are stored (split verts).
    /// </summary>
    public class ObjMesh
    {
        public List<Vector3> Positions = new();   // OBJ vertex list (shared)
        public List<Vector2> Tcoords = new();     // OBJ vt list (v is bottom-up)
        public List<Vector3> Normals = new();
        public List<int> TriPos = new();          // 3 per triangle, index into Positions
        public List<int> TriUv = new();           // 3 per triangle, index into Tcoords (-1 = none)
        public string MtlLib;
        public string TexturePath;                // resolved map_Kd

        public int TriangleCount => TriPos.Count / 3;

        public static ObjMesh Load(string path)
        {
            var m = new ObjMesh();
            var ci = CultureInfo.InvariantCulture;
            string dir = Path.GetDirectoryName(Path.GetFullPath(path));
            string mtl = null;

            foreach (var raw in File.ReadLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var p = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length == 0) continue;
                switch (p[0])
                {
                    case "v":
                        m.Positions.Add(new Vector3(F(p[1]), F(p[2]), F(p[3])));
                        break;
                    case "vt":
                        m.Tcoords.Add(new Vector2(F(p[1]), p.Length > 2 ? F(p[2]) : 0f));
                        break;
                    case "vn":
                        m.Normals.Add(new Vector3(F(p[1]), F(p[2]), F(p[3])));
                        break;
                    case "mtllib":
                        mtl = string.Join(" ", p, 1, p.Length - 1);
                        break;
                    case "f":
                    {
                        var vi = new List<int>();
                        var ti = new List<int>();
                        for (int k = 1; k < p.Length; k++)
                        {
                            var parts = p[k].Split('/');
                            vi.Add(Resolve(parts[0], m.Positions.Count));
                            ti.Add(parts.Length > 1 && parts[1].Length > 0 ? Resolve(parts[1], m.Tcoords.Count) : -1);
                        }
                        // fan triangulation (convex quads/polygons)
                        for (int k = 2; k < vi.Count; k++)
                        {
                            m.TriPos.Add(vi[0]); m.TriPos.Add(vi[k - 1]); m.TriPos.Add(vi[k]);
                            m.TriUv.Add(ti[0]); m.TriUv.Add(ti[k - 1]); m.TriUv.Add(ti[k]);
                        }
                        break;
                    }
                }
            }

            m.MtlLib = mtl;
            if (mtl != null)
            {
                var mtlPath = Path.Combine(dir, mtl.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(mtlPath))
                    foreach (var raw in File.ReadLines(mtlPath))
                    {
                        var line = raw.Trim();
                        if (!line.StartsWith("map_Kd", StringComparison.OrdinalIgnoreCase)) continue;
                        var tex = line.Substring(6).Trim();
                        var texPath = Path.Combine(dir, tex.Replace('/', Path.DirectorySeparatorChar));
                        if (File.Exists(texPath)) { m.TexturePath = texPath; break; }
                    }
            }
            return m;

            float F(string s) => float.Parse(s, ci);
            int Resolve(string s, int count)
            {
                int i = int.Parse(s, ci);
                return i > 0 ? i - 1 : count + i; // 1-based, negative = relative
            }
        }

        /// <summary>Splits the soup into a unique vertex array + triangle indices.</summary>
        public void BuildUnique(out List<Vector3> pos, out List<Vector2> uv, out List<int> idx)
        {
            pos = new List<Vector3>();
            uv = new List<Vector2>();
            idx = new List<int>();
            var map = new Dictionary<(int, int), int>();
            for (int t = 0; t < TriangleCount; t++)
            {
                for (int c = 0; c < 3; c++)
                {
                    int pi = TriPos[t * 3 + c];
                    int ti = TriUv[t * 3 + c];
                    if (!map.TryGetValue((pi, ti), out int vi))
                    {
                        vi = pos.Count;
                        map[(pi, ti)] = vi;
                        pos.Add(Positions[pi]);
                        uv.Add(ti >= 0 ? Tcoords[ti] : Vector2.Zero);
                    }
                    idx.Add(vi);
                }
            }
        }
    }
}
