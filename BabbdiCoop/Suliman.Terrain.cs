using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BabbdiCoop
{
    // Terrain, roads, river ice, ponds and the map boundary.
    internal static partial class Suliman
    {
        private static Mesh terrainMesh;

        private static void BuildTerrain()
        {
            int nx = SulimanData.NX, nz = SulimanData.NZ;
            float cx = SulimanData.CellX, cz = SulimanData.CellZ;
            var v = new Vector3[nx * nz];
            var n = new Vector3[nx * nz];
            var uv = new Vector2[nx * nz];
            for (int j = 0; j < nz; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    int k = j * nx + i;
                    float x = i * cx, z = j * cz;
                    float h = HeightAt(x, z);
                    v[k] = new Vector3(x, h, z);
                    uv[k] = new Vector2(x * 0.12f, z * 0.12f);
                    float hl = HeightAt(Mathf.Max(0f, x - cx), z);
                    float hr = HeightAt(Mathf.Min(SulimanData.W, x + cx), z);
                    float hd = HeightAt(x, Mathf.Max(0f, z - cz));
                    float hu = HeightAt(x, Mathf.Min(SulimanData.D, z + cz));
                    n[k] = new Vector3(hl - hr, 2f * cx, hd - hu).normalized;
                }
            }
            var tris = new int[(nx - 1) * (nz - 1) * 6];
            int t = 0;
            for (int j = 0; j < nz - 1; j++)
                for (int i = 0; i < nx - 1; i++)
                {
                    int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                    tris[t++] = a; tris[t++] = c; tris[t++] = b;
                    tris[t++] = b; tris[t++] = c; tris[t++] = d;
                }
            var mesh = new Mesh { name = "SulimanTerrain", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = v;
            mesh.normals = n;
            mesh.uv = uv;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            terrainMesh = mesh;

            var go = new GameObject("terrain");
            go.transform.SetParent(Root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = SnowMat;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;

            // unlit skirt so the map never shows a hard edge against the sky
            var skirt = new SGeo { UvScale = 0.08f };
            float yb = -80f;
            for (int i = 0; i < nx - 1; i += 4)
            {
                float x0 = i * cx, x1 = Mathf.Min(SulimanData.W, (i + 4) * cx);
                float h0 = HeightAt(x0, 0f), h1 = HeightAt(x1, 0f);
                skirt.Quad(new Vector3(x0, h0, 0f), new Vector3(x1, h1, 0f),
                           new Vector3(x1, yb, -60f), new Vector3(x0, yb, -60f), new Vector3(0f, 0f, -1f));
                float g0 = HeightAt(x0, SulimanData.D), g1 = HeightAt(x1, SulimanData.D);
                skirt.Quad(new Vector3(x0, yb, SulimanData.D + 60f), new Vector3(x1, yb, SulimanData.D + 60f),
                           new Vector3(x1, g1, SulimanData.D), new Vector3(x0, g0, SulimanData.D), new Vector3(0f, 0f, 1f));
            }
            for (int j = 0; j < nz - 1; j += 4)
            {
                float z0 = j * cz, z1 = Mathf.Min(SulimanData.D, (j + 4) * cz);
                float h0 = HeightAt(0f, z0), h1 = HeightAt(0f, z1);
                skirt.Quad(new Vector3(-60f, yb, z0), new Vector3(-60f, yb, z1),
                           new Vector3(0f, h1, z1), new Vector3(0f, h0, z0), new Vector3(-1f, 0f, 0f));
                float g0 = HeightAt(SulimanData.W, z0), g1 = HeightAt(SulimanData.W, z1);
                skirt.Quad(new Vector3(SulimanData.W, h0, z0), new Vector3(SulimanData.W, h1, z1),
                           new Vector3(SulimanData.W + 60f, yb, z1), new Vector3(SulimanData.W + 60f, yb, z0),
                           new Vector3(1f, 0f, 0f));
            }
            skirt.Emit(Root, "terrain_skirt", SnowMat, false, ShadowCastingMode.Off);

            // hard boundary so a walk off the edge is impossible
            AddWall("edge_n", new Vector3(SulimanData.W * 0.5f, 40f, -2f), new Vector3(SulimanData.W + 120f, 120f, 2f));
            AddWall("edge_s", new Vector3(SulimanData.W * 0.5f, 40f, SulimanData.D + 2f), new Vector3(SulimanData.W + 120f, 120f, 2f));
            AddWall("edge_w", new Vector3(-2f, 40f, SulimanData.D * 0.5f), new Vector3(2f, 120f, SulimanData.D + 120f));
            AddWall("edge_e", new Vector3(SulimanData.W + 2f, 40f, SulimanData.D * 0.5f), new Vector3(2f, 120f, SulimanData.D + 120f));

            Log($"[SULIMAN] terrain {nx}x{nz} ({mesh.vertexCount} verts, {mesh.triangles.Length / 3} tris), " +
                $"height {HeightAt(0, 0):0.0}..{HeightAt(SulimanData.W * 0.5f, SulimanData.D * 0.5f):0.0} m");
        }

        private static void AddWall(string name, Vector3 c, Vector3 s)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root, false);
            go.transform.localPosition = c;
            var bc = go.AddComponent<BoxCollider>();
            bc.size = s;
        }

        // ------------------------------------------------------------------ roads

        private static bool NearOtherRoad(float x, float z, int own, float margin)
        {
            float m2 = margin * margin;
            for (int ri = 0; ri < SulimanData.RoadCount; ri++)
            {
                if (ri == own) continue;
                int a = SulimanData.RoadOff[ri], b = SulimanData.RoadOff[ri + 1];
                for (int i = a; i + 1 < b; i++)
                {
                    float x1 = SulimanData.RoadPts[i * 2], z1 = SulimanData.RoadPts[i * 2 + 1];
                    float x2 = SulimanData.RoadPts[(i + 1) * 2], z2 = SulimanData.RoadPts[(i + 1) * 2 + 1];
                    float dx = x2 - x1, dz = z2 - z1;
                    float L2 = dx * dx + dz * dz;
                    float t = L2 < 1e-6f ? 0f : Mathf.Clamp01(((x - x1) * dx + (z - z1) * dz) / L2);
                    float px = x1 + dx * t, pz = z1 + dz * t;
                    if ((x - px) * (x - px) + (z - pz) * (z - pz) < m2) return true;
                }
            }
            return false;
        }

        private static void BuildRoads()
        {            var asphalt = new SGeo { UvScale = 0.2f };
            var packed = new SGeo { UvScale = 0.12f };
            var banks = new SGeo { UvScale = 0.16f };
            var kerb = new SGeo { UvScale = 0.3f };
            var skirtA = new SGeo { UvScale = 0.2f };
            var skirtP = new SGeo { UvScale = 0.2f };
            int bridgeIdx = 0;

            for (int ri = 0; ri < SulimanData.RoadCount; ri++)
            {
                int a = SulimanData.RoadOff[ri], b = SulimanData.RoadOff[ri + 1];
                int cnt = b - a;
                if (cnt < 2) continue;
                var pts = new Vector3[cnt];
                for (int i = 0; i < cnt; i++)
                {
                    float x = SulimanData.RoadPts[(a + i) * 2];
                    float z = SulimanData.RoadPts[(a + i) * 2 + 1];
                    pts[i] = new Vector3(x, HeightAt(x, z), z);
                }
                float w = SulimanData.RoadW[ri];
                byte cls = SulimanData.RoadCls[ri];
                bool main = cls <= 2;
                var g = (cls <= 2 && w >= 6.5f) ? asphalt : packed;
                float lift = 0.015f + ri * 0.0002f;
                float acc = 0f;
                for (int i = 0; i < cnt - 1; i++)
                {
                    Vector3 p0 = pts[i], p1 = pts[i + 1];
                    Vector3 d = p1 - p0; d.y = 0f;
                    float L = d.magnitude;
                    if (L < 0.05f) continue;
                    d /= L;
                    Vector3 side = new Vector3(-d.z, 0f, d.x);
                    float w0 = w * 0.5f, w1 = w * 0.5f;
                    Vector3 l0 = p0 - side * w0, r0 = p0 + side * w0;
                    Vector3 l1 = p1 - side * w1, r1 = p1 + side * w1;
                    float y0 = Mathf.Max(p0.y, HeightAt(l0.x, l0.z), HeightAt(r0.x, r0.z)) + lift;
                    float y1 = Mathf.Max(p1.y, HeightAt(l1.x, l1.z), HeightAt(r1.x, r1.z)) + lift;
                    bool junction = NearOtherRoad(p0.x, p0.z, ri, 5.0f) ||
                                    NearOtherRoad(p1.x, p1.z, ri, 5.0f) ||
                                    NearOtherRoad((p0.x + p1.x) * 0.5f, (p0.z + p1.z) * 0.5f, ri, 5.0f);
                    float v0 = acc / 8f, v1 = (acc + L) / 8f;
                    g.QuadUv(new Vector3(l0.x, y0, l0.z), new Vector3(l1.x, y1, l1.z),
                             new Vector3(r1.x, y1, r1.z), new Vector3(r0.x, y0, r0.z), Vector3.up,
                             new Vector2(0f, v0), new Vector2(0f, v1), new Vector2(1f, v1), new Vector2(1f, v0));
                    acc += L;

                    // the road sheet is only 1.5 cm above the terrain; fill the remaining edge gap
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector3 e0 = s < 0 ? l0 : r0;
                        Vector3 e1 = s < 0 ? l1 : r1;
                        float gy0 = HeightAt(e0.x, e0.z);
                        float gy1 = HeightAt(e1.x, e1.z);
                        if (y0 > gy0 + 0.004f || y1 > gy1 + 0.004f)
                        {
                            var sk = (cls <= 2 && w >= 6.5f) ? skirtA : skirtP;
                            Vector3 n = (side * s).normalized;
                            sk.QuadUv(new Vector3(e0.x + n.x * 0.02f, y0, e0.z + n.z * 0.02f),
                                      new Vector3(e1.x + n.x * 0.02f, y1, e1.z + n.z * 0.02f),
                                      new Vector3(e1.x + n.x * 0.02f, gy1 - 0.03f, e1.z + n.z * 0.02f),
                                      new Vector3(e0.x + n.x * 0.02f, gy0 - 0.03f, e0.z + n.z * 0.02f),
                                      n, new Vector2(0, 0), new Vector2(0, 1),
                                      new Vector2(1, 1), new Vector2(1, 0));
                        }
                    }

                    if (main && !junction)
                    {
                        // low snow banks at both edges (packed snow pushed by the plough)
                        SnowBank(banks, p0, p1, side, w0, 0.95f);
                        SnowBank(banks, p0, p1, -side, w0, 0.95f);
                        // kerb / pavement strip on the wider streets — removed: the road stays flat
                        // for driving; the snow bank starts outside the asphalt edge.
                    }
                }
            }

            asphalt.Emit(Root, "roads_asphalt", AsphaltMat, true);
            packed.Emit(Root, "roads_packed", PackedMat, true);
            banks.Emit(Root, "snow_banks", SnowBankMat);
            kerb.Emit(Root, "kerbs", ConcreteMat);
            skirtA.Emit(Root, "road_skirts_asphalt", AsphaltMat, false, ShadowCastingMode.Off);
            skirtP.Emit(Root, "road_skirts_packed", PackedMat, false, ShadowCastingMode.Off);

            BuildRiverIce();
            BuildPonds();
            BuildBridges();

            Log($"[SULIMAN] roads {SulimanData.RoadCount} pieces, banks + kerbs; river ice and ponds");
        }

        private static void AddStrip(SGeo g, Vector3 p0, Vector3 p1, Vector3 side,
                                     float inner, float outer, float y0, float y1, float width)
        {
            Vector3 a = p0 + side * inner, b = p1 + side * inner;
            Vector3 c = p1 + side * outer, d = p0 + side * outer;
            g.QuadUv(new Vector3(a.x, y0, a.z), new Vector3(b.x, y1, b.z),
                     new Vector3(c.x, y1 + 0.05f, c.z), new Vector3(d.x, y0 + 0.05f, d.z), Vector3.up,
                     new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
        }

        private static void SnowBank(SGeo g, Vector3 p0, Vector3 p1, Vector3 side, float edge, float width)
        {
            Vector3 a = p0 + side * edge, b = p1 + side * edge;
            Vector3 c = p1 + side * (edge + width), d = p0 + side * (edge + width);
            float h = 0.12f;
            float ya = Mathf.Max(HeightAt(a.x, a.z), p0.y) + 0.06f;
            float yb = Mathf.Max(HeightAt(b.x, b.z), p1.y) + 0.06f;
            float yc = HeightAt(c.x, c.z), yd = HeightAt(d.x, d.z);
            // slope up from the road
            g.QuadUv(new Vector3(a.x, ya, a.z), new Vector3(b.x, yb, b.z),
                     new Vector3(b.x, yb + h, b.z), new Vector3(a.x, ya + h, a.z),
                     (side + Vector3.up).normalized,
                     new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
            // outer slope down to the terrain
            g.QuadUv(new Vector3(a.x, ya + h, a.z), new Vector3(b.x, yb + h, b.z),
                     new Vector3(c.x, yc, c.z), new Vector3(d.x, yd, d.z),
                     (side * 0.6f + Vector3.up * 0.5f).normalized,
                     new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
        }

        // ------------------------------------------------------------------ water

        /// <summary>Widens the real DEM valley so a 3x river has a real bed instead of a canyon.
        /// Runs once when the height grid is decoded; only lowers terrain, never raises it.</summary>
        private static void CarveRiverValley(float[] h)
        {
            int nx = SulimanData.NX, nz = SulimanData.NZ;
            float cx = SulimanData.CellX, cz = SulimanData.CellZ;
            var src = (float[])h.Clone();
            for (int wi = 0; wi < SulimanData.WaterCount; wi++)
            {
                if (SulimanData.WaterKind[wi] != 0) continue;   // only the main river
                int a = SulimanData.WaterOff[wi], b = SulimanData.WaterOff[wi + 1];
                if (b - a < 2) continue;
                float half = SulimanData.WaterW[wi] * 0.5f;
                for (int iz = 0; iz < nz; iz++)
                {
                    float z = iz * cz;
                    for (int ix = 0; ix < nx; ix++)
                    {
                        float x = ix * cx;
                        float best = 1e9f, px = 0f, pz = 0f;
                        for (int i = a; i + 1 < b; i++)
                        {
                            float x1 = SulimanData.WaterPts[i * 2], z1 = SulimanData.WaterPts[i * 2 + 1];
                            float x2 = SulimanData.WaterPts[(i + 1) * 2], z2 = SulimanData.WaterPts[(i + 1) * 2 + 1];
                            float dx = x2 - x1, dz = z2 - z1;
                            float l2 = dx * dx + dz * dz;
                            float t = l2 < 1e-6f ? 0f : Mathf.Clamp01(((x - x1) * dx + (z - z1) * dz) / l2);
                            float qx = x1 + dx * t, qz = z1 + dz * t;
                            float d = (x - qx) * (x - qx) + (z - qz) * (z - qz);
                            if (d < best) { best = d; px = qx; pz = qz; }
                        }
                        float dist = Mathf.Sqrt(best);
                        if (dist > half + 24f) continue;
                        // local water level follows the old river bed
                        float hc = SampleGrid(src, nx, cx, cz, px, pz);
                        float bed = hc + 1.05f - 1.30f;
                        float t2 = Mathf.Clamp01((dist - (half - 4f)) / 24f);
                        float target = Mathf.Lerp(bed, h[iz * nx + ix], t2);
                        if (target < h[iz * nx + ix]) h[iz * nx + ix] = target;
                    }
                }
            }
        }

        private static float SampleGrid(float[] g, int nx, float cx, float cz, float x, float z)
        {
            float fx = Mathf.Clamp(x / cx, 0f, nx - 1.001f);
            float fz = Mathf.Clamp(z / cz, 0f, SulimanData.NZ - 1.001f);
            int ix = (int)fx, iz = (int)fz;
            float tx = fx - ix, tz = fz - iz;
            int i0 = iz * nx + ix, i1 = i0 + nx;
            float a = Mathf.Lerp(g[i0], g[i0 + 1], tx);
            float b = Mathf.Lerp(g[i1], g[i1 + 1], tx);
            return Mathf.Lerp(a, b, tz);
        }

        /// <summary>True if this map point is inside a river/stream ribbon or a pond.</summary>
        internal static bool OnWater(float x, float z)
        {
            for (int wi = 0; wi < SulimanData.WaterCount; wi++)
            {
                float half = SulimanData.WaterW[wi] * 0.5f + 1.5f;
                int a = SulimanData.WaterOff[wi], b = SulimanData.WaterOff[wi + 1];
                for (int i = a; i + 1 < b; i++)
                {
                    float x1 = SulimanData.WaterPts[i * 2], z1 = SulimanData.WaterPts[i * 2 + 1];
                    float x2 = SulimanData.WaterPts[(i + 1) * 2], z2 = SulimanData.WaterPts[(i + 1) * 2 + 1];
                    float dx = x2 - x1, dz = z2 - z1;
                    float L2 = dx * dx + dz * dz;
                    float t = L2 < 1e-6f ? 0f : Mathf.Clamp01(((x - x1) * dx + (z - z1) * dz) / L2);
                    float px = x1 + dx * t, pz = z1 + dz * t;
                    if ((x - px) * (x - px) + (z - pz) * (z - pz) < half * half) return true;
                }
            }
            for (int pi = 0; pi < SulimanData.PondCount; pi++)
            {
                int a = SulimanData.PondOff[pi], b = SulimanData.PondOff[pi + 1];
                if (PointInPond(a, b, x, z, 1.5f)) return true;
            }
            return false;
        }

        private static bool PointInPond(int a, int b, float x, float z, float margin)
        {
            int n = b - a;
            if (n < 3) return false;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = SulimanData.PondPts[(a + i) * 2], zi = SulimanData.PondPts[(a + i) * 2 + 1];
                float xj = SulimanData.PondPts[(a + j) * 2], zj = SulimanData.PondPts[(a + j) * 2 + 1];
                if (((zi > z) != (zj > z)) && (x < (xj - xi) * (z - zi) / (zj - zi + 1e-9f) + xi))
                    inside = !inside;
                // near an edge counts as water for relocation
                float dx = xj - xi, dz = zj - zi;
                float L2 = dx * dx + dz * dz;
                float t = L2 < 1e-6f ? 0f : Mathf.Clamp01(((x - xi) * dx + (z - zi) * dz) / L2);
                float px = xi + dx * t, pz = zi + dz * t;
                if ((x - px) * (x - px) + (z - pz) * (z - pz) < margin * margin) return true;
            }
            return inside;
        }

        private static Vector2 FindDryOffset(Vector2[] pts, float cx, float cz)
        {
            for (int r = 5; r <= 90; r += 5)
            {
                for (int a = 0; a < 16; a++)
                {
                    float ang = a * Mathf.PI * 2f / 16f;
                    float dx = Mathf.Cos(ang) * r, dz = Mathf.Sin(ang) * r;
                    bool ok = true;
                    for (int i = 0; i < pts.Length && ok; i++)
                    {
                        float px = pts[i].x + dx, pz = pts[i].y + dz;
                        if (!InMap(px, pz) || OnWater(px, pz)) ok = false;
                    }
                    if (ok) return new Vector2(dx, dz);
                }
            }
            return Vector2.zero;
        }

        private static void BuildRiverIce()
        {
            if (SulimanData.WaterCount == 0) return;
            var ice = new SGeo { UvScale = 0.08f };
            var banks = new SGeo { UvScale = 0.12f };
            for (int wi = 0; wi < SulimanData.WaterCount; wi++)
            {
                int a = SulimanData.WaterOff[wi], b = SulimanData.WaterOff[wi + 1];
                int cnt = b - a;
                if (cnt < 2) continue;
                float w = SulimanData.WaterW[wi];
                bool stream = SulimanData.WaterKind[wi] == 1;
                var pts = new Vector3[cnt];
                for (int i = 0; i < cnt; i++)
                {
                    float x = SulimanData.WaterPts[(a + i) * 2];
                    float z = SulimanData.WaterPts[(a + i) * 2 + 1];
                    pts[i] = new Vector3(x, HeightAt(x, z) + (stream ? 0.55f : 1.15f), z);
                }
                float acc = 0f;
                for (int i = 0; i < cnt - 1; i++)
                {
                    Vector3 p0 = pts[i], p1 = pts[i + 1];
                    Vector3 d = p1 - p0; d.y = 0f;
                    float L = d.magnitude;
                    if (L < 0.05f) continue;
                    d /= L;
                    Vector3 side = new Vector3(-d.z, 0f, d.x);
                    float hw = w * 0.5f;
                    Vector3 l0 = p0 - side * hw, r0 = p0 + side * hw;
                    Vector3 l1 = p1 - side * hw, r1 = p1 + side * hw;
                    float y0 = Mathf.Min(p0.y, HeightAt(l0.x, l0.z) + 1.0f, HeightAt(r0.x, r0.z) + 1.0f);
                    float y1 = Mathf.Min(p1.y, HeightAt(l1.x, l1.z) + 1.0f, HeightAt(r1.x, r1.z) + 1.0f);
                    ice.QuadUv(new Vector3(l0.x, y0, l0.z), new Vector3(l1.x, y1, l1.z),
                               new Vector3(r1.x, y1, r1.z), new Vector3(r0.x, y0, r0.z), Vector3.up,
                               new Vector2(0, acc * 0.05f), new Vector2(0, (acc + L) * 0.05f),
                               new Vector2(1, (acc + L) * 0.05f), new Vector2(1, acc * 0.05f));
                    // snow banks rising from the ice to the valley floor
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector3 e0 = s < 0 ? l0 : r0;
                        Vector3 e1 = s < 0 ? l1 : r1;
                        Vector3 o0 = p0 + side * (s * (hw + 10f));
                        Vector3 o1 = p1 + side * (s * (hw + 10f));
                        float oy0 = HeightAt(o0.x, o0.z) + 0.04f;
                        float oy1 = HeightAt(o1.x, o1.z) + 0.04f;
                        banks.QuadUv(new Vector3(e0.x, y0, e0.z), new Vector3(e1.x, y1, e1.z),
                                     new Vector3(o1.x, oy1, o1.z), new Vector3(o0.x, oy0, o0.z),
                                     (side * s * 0.5f + Vector3.up * 0.7f).normalized,
                                     new Vector2(0, acc * 0.05f), new Vector2(0, (acc + L) * 0.05f),
                                     new Vector2(1, (acc + L) * 0.05f), new Vector2(1, acc * 0.05f));
                    }
                    acc += L;
                }
            }
            var go = ice.Emit(Root, "river_ice", IceMat, true, ShadowCastingMode.Off);
            banks.Emit(Root, "river_banks", SnowBankMat, false, ShadowCastingMode.Off);
            if (go != null) Log($"[SULIMAN] frozen river ribbon: {SulimanData.WaterCount} piece(s)");
        }

        private static void BuildPonds()
        {
            if (SulimanData.PondCount == 0) return;
            var ice = new SGeo { UvScale = 0.1f };
            for (int pi = 0; pi < SulimanData.PondCount; pi++)
            {
                int a = SulimanData.PondOff[pi], b = SulimanData.PondOff[pi + 1];
                int n = b - a;
                if (n < 3) continue;
                var pts = new List<Vector3>();
                Vector3 c = Vector3.zero;
                for (int i = 0; i < n; i++)
                {
                    float x = SulimanData.PondPts[(a + i) * 2];
                    float z = SulimanData.PondPts[(a + i) * 2 + 1];
                    pts.Add(new Vector3(x, HeightAt(x, z) + 0.3f, z));
                    c += pts[i];
                }
                c /= n;
                c.y = HeightAt(c.x, c.z) + 0.3f;
                for (int i = 0; i < n; i++)
                {
                    var p0 = pts[i]; var p1 = pts[(i + 1) % n];
                    ice.Tri(c, p0, p1, Vector3.up,
                        new Vector2(c.x * 0.1f, c.z * 0.1f),
                        new Vector2(p0.x * 0.1f, p0.z * 0.1f),
                        new Vector2(p1.x * 0.1f, p1.z * 0.1f));
                }
            }
            ice.Emit(Root, "pond_ice", IceMat, true, ShadowCastingMode.Off);
        }

        private static void BuildBridges()
        {
            if (SulimanData.BridgeCount == 0) return;
            var deck = new SGeo { UvScale = 0.3f };
            var rail = new SGeo { UvScale = 0.4f };
            for (int i = 0; i < SulimanData.BridgeCount; i++)
            {
                float x1 = SulimanData.Bridges[i * 5], z1 = SulimanData.Bridges[i * 5 + 1];
                float x2 = SulimanData.Bridges[i * 5 + 2], z2 = SulimanData.Bridges[i * 5 + 3];
                float w = SulimanData.Bridges[i * 5 + 4];
                Vector2 d2 = new Vector2(x2 - x1, z2 - z1);
                if (d2.sqrMagnitude < 1f) continue;
                d2.Normalize();
                float mx = (x1 + x2) * 0.5f, mz = (z1 + z2) * 0.5f;
                float half = 22f;
                Vector3 a = new Vector3(mx - d2.x * half, 0f, mz - d2.y * half);
                Vector3 b = new Vector3(mx + d2.x * half, 0f, mz + d2.y * half);
                float y = Mathf.Max(HeightAt(a.x, a.z), HeightAt(b.x, b.z), HeightAt(mx, mz)) + 0.55f;
                if (y > 24f)
                {
                    Log($"[SULIMAN] bridge at {mx:0},{mz:0} skipped: the crossing is on the hills (y={y:0.0})");
                    continue;
                }
                a.y = y; b.y = y;
                Vector3 d = b - a; d.y = 0;
                float L = d.magnitude; d /= Mathf.Max(0.01f, L);
                Vector3 side = new Vector3(-d.z, 0f, d.x);
                Vector3 c = (a + b) * 0.5f;
                deck.Box(new Vector3(c.x, y - 0.35f, c.z), new Vector3(w, 0.7f, L + 1f),
                         Quaternion.LookRotation(d, Vector3.up));
                // railings
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 off = side * (w * 0.5f - 0.2f) * s;
                    rail.Box(new Vector3(c.x + off.x, y + 0.55f, c.z + off.z),
                             new Vector3(0.12f, 0.10f, L), Quaternion.LookRotation(d, Vector3.up));
                    for (int k = 0; k <= 6; k++)
                    {
                        Vector3 p = Vector3.Lerp(a, b, k / 6f) + off;
                        rail.Box(new Vector3(p.x, y + 0.28f, p.z),
                                 new Vector3(0.10f, 0.65f, 0.10f), Quaternion.identity);
                    }
                }
                Log($"[SULIMAN] bridge {i}: {L:0} m at {c.x:0},{c.z:0} y={y:0.0}");
            }
            deck.Emit(Root, "bridge_deck", ConcreteMat, true);
            rail.Emit(Root, "bridge_rail", MetalMat);
        }

        // ------------------------------------------------------------------ helpers

        private static Vector3 OnGround(float x, float z, float up = 0.1f)
            => new Vector3(x, HeightAt(x, z) + up, z);
    }
}
