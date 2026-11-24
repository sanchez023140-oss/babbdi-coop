using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace BabbdiCoop
{
    // Part of the SovietDistrict builder - see SovietDistrict.cs.
    internal static partial class SovietDistrict
    {

        public struct PreviewShot
        {
            public Vector3 Pos;
            public Vector3 Look;
            public string Name;   // v0.9.10: set by GrainShots(), unused by the district poses
            public float Ortho;   // v0.9.10: > 0 = orthographic half-size (plan-style), 0 = camera FOV
            public bool Lamp;     // v0.9.12: light this shot with a temporary sun (dark facades)
            public int RampState; // v0.9.112: 1 = raised, 2 = half, 3 = lowered (0 = leave)
        }

        // ------------------------------------------------------------------ mesh helper

        /// <summary>Vertex/index accumulator with two submeshes: 0 = concrete, 1 = window glass.</summary>
        private sealed class Geo
        {
            public readonly List<Vector3> V = new List<Vector3>(4096);
            public readonly List<Vector3> N = new List<Vector3>(4096);
            public readonly List<Vector2> UV = new List<Vector2>(4096);
            public readonly List<int> T0 = new List<int>(8192);
            public readonly List<int> T1 = new List<int>(1024);

            /// <summary>v0.9.13: extra grain density for this mesh's horizontal faces (the platform's
            /// own surfaces вЂ” deck, terraces, streets, ramps, stairs, esplanade вЂ” are 4x the houses').</summary>
            public float FloorMul = 1f;

            /// <summary>Tangent frame of a face: u runs along the face, v up вЂ” horizontal faces use
            /// world X/Z instead, so every face of one orientation samples the same place.</summary>
            private static void Frame(Vector3 n, out Vector3 tx, out Vector3 ty)
            {
                if (Mathf.Abs(n.y) > 0.7f)
                {
                    tx = Vector3.right;
                    ty = Vector3.forward;
                }
                else
                {
                    tx = Vector3.Cross(Vector3.up, n).normalized;
                    ty = Vector3.Cross(n, tx).normalized;
                }
            }

            /// <summary>
            /// Quad with **world-anchored UVs** (v0.9.4-r3). The old code started every quad at (0,0)
            /// and scaled by the quad's own size, so each wall cell, window reveal and box face
            /// restarted the texture. On a facade built from 1-5 m cells that restart is a visible
            /// grid of identical patches вЂ” it read as tiling/panelling (the user's "С„РѕС‚Рѕ 1"), and it
            /// is why lowering the density only made the patches bigger ("РєР°РјСѓС„Р»СЏР¶"). Projecting the
            /// corners onto the face's tangent frame gives one continuous UV field over the whole
            /// district, so `T_RoughConcreteMacro`'s 1-5 cm pits flow across a building exactly like
            /// the game's own blocks (photo 4) and across the deck like the dam (photo 2).
            /// </summary>
            public void Quad(int part, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, float uv)
            {
                Vector3 tx, ty;
                Frame(n, out tx, out ty);
                float k = GrainUv * (Mathf.Abs(n.y) > 0.7f ? FloorMul : 1f);   // v0.9.13: the platform's floors are grainier
                Quad(part, a, b, c, d, n,
                     new Vector2(Vector3.Dot(a, tx), Vector3.Dot(a, ty)) * k,
                     new Vector2(Vector3.Dot(b, tx), Vector3.Dot(b, ty)) * k,
                     new Vector2(Vector3.Dot(c, tx), Vector3.Dot(c, ty)) * k,
                     new Vector2(Vector3.Dot(d, tx), Vector3.Dot(d, ty)) * k);
            }

            public void Quad(int part, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n,
                             Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
            {
                int i = V.Count;
                V.Add(a); V.Add(b); V.Add(c); V.Add(d);
                N.Add(n); N.Add(n); N.Add(n); N.Add(n);
                UV.Add(ua); UV.Add(ub); UV.Add(uc); UV.Add(ud);
                var t = part == 0 ? T0 : T1;
                // Unity front faces are clockwise seen from the front: the geometric normal of the
                // triangle must point along the face normal.
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), n) > 0f)
                { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
                else
                { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
            }

            /// <summary>v0.9.18: one triangle вЂ” the clock dial's fan (winding matches `Quad`).</summary>
            public void Tri(int part, Vector3 a, Vector3 b, Vector3 c, Vector3 n,
                            Vector2 ua, Vector2 ub, Vector2 uc)
            {
                int i = V.Count;
                V.Add(a); V.Add(b); V.Add(c);
                N.Add(n); N.Add(n); N.Add(n);
                UV.Add(ua); UV.Add(ub); UV.Add(uc);
                var t = part == 0 ? T0 : T1;
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), n) > 0f)
                { t.Add(i); t.Add(i + 1); t.Add(i + 2); }
                else
                { t.Add(i); t.Add(i + 2); t.Add(i + 1); }
            }

            public void Box(int part, Vector3 c, Vector3 s, Quaternion r, float uv)
            {
                Vector3 h = s * 0.5f;
                var p = new Vector3[8];
                for (int i = 0; i < 8; i++)
                {
                    var lp = new Vector3(((i & 1) == 0) ? -h.x : h.x,
                                         ((i & 2) == 0) ? -h.y : h.y,
                                         ((i & 4) == 0) ? -h.z : h.z);
                    p[i] = c + r * lp;
                }
                Vector3 R = r * Vector3.right, U = r * Vector3.up, F = r * Vector3.forward;
                Quad(part, p[1], p[3], p[7], p[5], R, uv);
                Quad(part, p[0], p[4], p[6], p[2], -R, uv);
                Quad(part, p[2], p[6], p[7], p[3], U, uv);
                Quad(part, p[0], p[1], p[5], p[4], -U, uv);
                Quad(part, p[4], p[5], p[7], p[6], F, uv);
                Quad(part, p[0], p[2], p[3], p[1], -F, uv);
            }

            public GameObject Emit(Transform parent, string name, Material m0, Material m1 = null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                Apply(go, m0, m1);
                return go;
            }

            public Mesh Apply(GameObject go, Material m0, Material m1 = null)
            {
                var mesh = new Mesh { name = go.name };
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(V);
                mesh.SetNormals(N);
                mesh.SetUVs(0, UV);
                bool two = T1.Count > 0 && m1 != null;
                mesh.subMeshCount = two ? 2 : 1;
                mesh.SetTriangles(T0, 0);
                if (two) mesh.SetTriangles(T1, 1);
                mesh.RecalculateBounds();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = two ? new[] { m0, m1 } : new[] { m0 };
                return mesh;
            }
        }
    }
}
