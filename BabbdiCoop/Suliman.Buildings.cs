using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BabbdiCoop
{
    // Detailed OSM building shells: facades, windows, balconies, roofs, snow, signs.
    internal static partial class Suliman
    {
        private struct BEdge
        {
            public Vector3 a, b;      // wall base points (y = 0 here; caller adds height)
            public Vector3 t;         // along the wall
            public Vector3 n;         // outward
            public float len;
        }

        private static readonly List<BEdge> edges = new List<BEdge>(32);
        private struct DoorProbe { public Vector3 p; public Vector3 d; public int bi; }
        private static readonly List<DoorProbe> doorProbes = new List<DoorProbe>();
        private static readonly List<Vector2[]> placedPts = new List<Vector2[]>();
        private static readonly List<Vector2> placedCenters = new List<Vector2>();
        private const float PlaceCell = 2f;
        private static bool[] placeGrid;
        private static int placeW, placeH;
        private static bool placeGridReady;
        private static Vector2[] interiorPoly;
        private static Mesh plankMesh;
        private static Material plankMat;
        private static Vector3 plankMeshSize;
        private static bool plankFound;
        private static int wetMoved, wetSkipped;
        private static int lmSkipped;

        private static void BuildBuildings()
        {
            var walls = new MaterialBank(Root, "bldg_wall");
            var glassLit = new MaterialBank(Root, "bldg_lit");
            var glassCold = new MaterialBank(Root, "bldg_cold");
            var glassDark = new MaterialBank(Root, "bldg_dark");
            var glassBoard = new MaterialBank(Root, "bldg_board");
            var glassShop = new MaterialBank(Root, "bldg_shop");
            var snow = new MaterialBank(Root, "bldg_snow");
            var trim = new MaterialBank(Root, "bldg_trim");
            var accents = new MaterialBank(Root, "bldg_accent");
            var woodD = new MaterialBank(Root, "bldg_wood");
            var metal = new MaterialBank(Root, "bldg_metal");
            var balconyCol = new SGeo { UvScale = 1f };

            int totalWindows = 0, totalBalconies = 0, totalDoors = 0, totalSigns = 0;
            wetMoved = wetSkipped = 0;
            lmSkipped = 0;
            doorProbes.Clear();
            placedPts.Clear();
            placedCenters.Clear();
            placeGridReady = false;
            placeGrid = null;
            placeMoved = placeSkipped = 0;
            FindPlankTemplate();
            for (int bi = 0; bi < SulimanData.BldCount; bi++)
            {
                try
                {
                    var st = BuildOne(bi, walls, glassLit, glassCold, glassDark, glassBoard, glassShop,
                                      snow, trim, accents, woodD, metal, balconyCol, ref totalWindows,
                                      ref totalBalconies, ref totalDoors, ref totalSigns);
                }
                catch (Exception e)
                {
                    Warn($"[SULIMAN] building {bi} failed: {e.Message}");
                }
            }

            // every abandoned house must have a passable doorway: probe the collision
            Physics.SyncTransforms();
            int blockedDoors = 0;
            for (int i = 0; i < doorProbes.Count; i++)
            {
                var pr = doorProbes[i];
                RaycastHit hit;
                if (Physics.Raycast(Root.TransformPoint(pr.p), pr.d, out hit, 1.2f, ~0,
                                    QueryTriggerInteraction.Ignore) &&
                    hit.collider != null && hit.collider.name == "bldg_col_" + pr.bi)
                    blockedDoors++;
            }
            Log($"[SULIMAN] abandoned doorways: {doorProbes.Count}, blocked: {blockedDoors}");

            walls.Emit();
            glassLit.Emit(false, ShadowCastingMode.Off);            glassCold.Emit(false, ShadowCastingMode.Off);
            glassDark.Emit(false, ShadowCastingMode.Off);
            glassBoard.Emit(false, ShadowCastingMode.Off);
            glassShop.Emit(false, ShadowCastingMode.Off);
            snow.Emit();
            trim.Emit();
            accents.Emit();
            woodD.Emit();
            metal.Emit();
            balconyCol.EmitCollider(Root, "balcony_col");

            Log($"[SULIMAN] {SulimanData.BldCount} buildings: {totalWindows} windows, " +
                $"{totalBalconies} balconies, {totalDoors} entrances, {totalSigns} signs; " +
                $"moved off water: {wetMoved}, skipped in water/landmarks: {wetSkipped}/{lmSkipped}; " +
                $"placement moved: {placeMoved}, no free spot: {placeSkipped}");
        }

        private static int placeMoved, placeSkipped;

        private static void EnsurePlaceGrid()
        {
            if (placeGridReady) return;
            placeGridReady = true;
            placeW = Mathf.CeilToInt(SulimanData.W / PlaceCell);
            placeH = Mathf.CeilToInt(SulimanData.D / PlaceCell);
            placeGrid = new bool[placeW * placeH];
            for (int ri = 0; ri < SulimanData.RoadCount; ri++)
            {
                int a = SulimanData.RoadOff[ri], b = SulimanData.RoadOff[ri + 1];
                float half = SulimanData.RoadW[ri] * 0.5f + 1.5f;
                for (int i = a; i + 1 < b; i++)
                {
                    float x1 = SulimanData.RoadPts[i * 2], z1 = SulimanData.RoadPts[i * 2 + 1];
                    float x2 = SulimanData.RoadPts[(i + 1) * 2], z2 = SulimanData.RoadPts[(i + 1) * 2 + 1];
                    float dx = x2 - x1, dz = z2 - z1;
                    float len = Mathf.Sqrt(dx * dx + dz * dz);
                    int steps = Mathf.Max(1, Mathf.CeilToInt(len / 2f));
                    for (int k = 0; k <= steps; k++)
                    {
                        float t = k / (float)steps;
                        MarkDisc(x1 + dx * t, z1 + dz * t, half);
                    }
                }
            }
            Log($"[SULIMAN] placement grid {placeW}x{placeH} ({PlaceCell:0.#} m), roads reserved");
        }

        private static void MarkDisc(float x, float z, float r)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt((x - r) / PlaceCell), 0, placeW - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((x + r) / PlaceCell), 0, placeW - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((z - r) / PlaceCell), 0, placeH - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((z + r) / PlaceCell), 0, placeH - 1);
            float r2 = r * r;
            for (int iz = z0; iz <= z1; iz++)
                for (int ix = x0; ix <= x1; ix++)
                {
                    float px = (ix + 0.5f) * PlaceCell, pz = (iz + 0.5f) * PlaceCell;
                    float dx = px - x, dz = pz - z;
                    if (dx * dx + dz * dz <= r2) placeGrid[iz * placeW + ix] = true;
                }
        }

        private static bool PlaceBuilding(Vector2[] pts, ref float cx, ref float cz)
        {
            EnsurePlaceGrid();
            var tryPts = new Vector2[pts.Length];
            var cells = new System.Collections.Generic.List<int>(256);
            for (int ring = 0; ring <= 40; ring++)
            {
                int n = ring == 0 ? 1 : 16;
                for (int a = 0; a < n; a++)
                {
                    float ang = Mathf.PI * 2f * a / n;
                    float dx = Mathf.Cos(ang) * ring * PlaceCell;
                    float dz = Mathf.Sin(ang) * ring * PlaceCell;
                    float ncx = cx + dx, ncz = cz + dz;
                    if (!InMap(ncx, ncz)) continue;

                    bool bad = false;
                    for (int i = 0; i < pts.Length; i++)
                    {
                        float px = pts[i].x + dx, pz = pts[i].y + dz;
                        tryPts[i] = new Vector2(px, pz);
                        if (!InMap(px, pz) || OnWater(px, pz) || InLandmarkClear(px, pz)) bad = true;
                    }
                    if (bad) continue;

                    float minX = 1e9f, maxX = -1e9f, minZ = 1e9f, maxZ = -1e9f;
                    for (int i = 0; i < tryPts.Length; i++)
                    {
                        minX = Mathf.Min(minX, tryPts[i].x); maxX = Mathf.Max(maxX, tryPts[i].x);
                        minZ = Mathf.Min(minZ, tryPts[i].y); maxZ = Mathf.Max(maxZ, tryPts[i].y);
                    }
                    int gx0 = Mathf.FloorToInt(minX / PlaceCell);
                    int gx1 = Mathf.CeilToInt(maxX / PlaceCell);
                    int gz0 = Mathf.FloorToInt(minZ / PlaceCell);
                    int gz1 = Mathf.CeilToInt(maxZ / PlaceCell);
                    if (gx0 < 0 || gz0 < 0 || gx1 >= placeW || gz1 >= placeH) continue;

                    cells.Clear();
                    bool free = true;
                    for (int iz = gz0; iz <= gz1 && free; iz++)
                        for (int ix = gx0; ix <= gx1; ix++)
                        {
                            float px = (ix + 0.5f) * PlaceCell, pz = (iz + 0.5f) * PlaceCell;
                            if (!PointInPoly(tryPts, px, pz)) continue;
                            int idx = iz * placeW + ix;
                            if (placeGrid[idx]) { free = false; break; }
                            cells.Add(idx);
                        }
                    // very thin footprints: fall back to their bounding box
                    if (free && cells.Count == 0)
                    {
                        for (int iz = gz0; iz <= gz1 && free; iz++)
                            for (int ix = gx0; ix <= gx1; ix++)
                            {
                                int idx = iz * placeW + ix;
                                if (placeGrid[idx]) { free = false; break; }
                                cells.Add(idx);
                            }
                    }
                    if (!free) continue;

                    if (ring > 0) placeMoved++;
                    for (int i = 0; i < pts.Length; i++) pts[i] = tryPts[i];
                    cx = ncx; cz = ncz;
                    for (int c = 0; c < cells.Count; c++)
                    {
                        int idx = cells[c];
                        int ix = idx % placeW, iz = idx / placeW;
                        for (int oz = -1; oz <= 1; oz++)
                            for (int ox = -1; ox <= 1; ox++)
                            {
                                int nx = ix + ox, nz = iz + oz;
                                if (nx < 0 || nz < 0 || nx >= placeW || nz >= placeH) continue;
                                placeGrid[nz * placeW + nx] = true;
                            }
                    }
                    return true;
                }
            }
            placeSkipped++;
            return false;
        }

        private static void ResolvePlacement(Vector2[] pts, ref float cx, ref float cz)
        {
            for (int iter = 0; iter < 10; iter++)
            {
                bool moved = false;
                for (int i = 0; i < placedPts.Count; i++)
                {
                    if (!AabbOverlap(pts, placedPts[i], 0.8f)) continue;
                    Vector2 dir = new Vector2(cx, cz) - placedCenters[i];
                    if (dir.sqrMagnitude < 0.01f) dir = Vector2.right;
                    dir.Normalize();
                    Shift(pts, dir * 1.8f);
                    cx += dir.x * 1.8f; cz += dir.y * 1.8f;
                    moved = true;
                }
                Vector2 rp; float half;
                if (NearestRoad(cx, cz, out rp, out half))
                {
                    float rad = FootprintRadius(pts, cx, cz);
                    float d = Vector2.Distance(new Vector2(cx, cz), rp);
                    float need = rad + half + 2.0f - d;
                    if (need > 0f)
                    {
                        Vector2 dir = new Vector2(cx, cz) - rp;
                        if (dir.sqrMagnitude < 0.01f) dir = Vector2.right;
                        dir.Normalize();
                        float push = need + 0.25f;
                        Shift(pts, dir * push);
                        cx += dir.x * push; cz += dir.y * push;
                        moved = true;
                    }
                }
                if (OnWater(cx, cz) || InLandmarkClear(cx, cz) || !InMap(cx, cz))
                {
                    Vector2 dir = new Vector2(SulimanData.W * 0.5f - cx, SulimanData.D * 0.5f - cz);
                    if (dir.sqrMagnitude > 0.01f)
                    {
                        dir.Normalize();
                        Shift(pts, dir * 3f);
                        cx += dir.x * 3f; cz += dir.y * 3f;
                    }
                    moved = true;
                }
                if (!moved) break;
            }
        }

        private static void Shift(Vector2[] pts, Vector2 d)
        {
            for (int i = 0; i < pts.Length; i++) pts[i] += d;
        }

        private static float FootprintRadius(Vector2[] pts, float cx, float cz)
        {
            float r = 0f;
            for (int i = 0; i < pts.Length; i++)
            {
                float dx = pts[i].x - cx, dz = pts[i].y - cz;
                r = Mathf.Max(r, Mathf.Sqrt(dx * dx + dz * dz));
            }
            return r;
        }

        private static bool AabbOverlap(Vector2[] a, Vector2[] b, float margin)
        {
            float ax0 = 1e9f, ax1 = -1e9f, az0 = 1e9f, az1 = -1e9f;
            for (int i = 0; i < a.Length; i++)
            {
                ax0 = Mathf.Min(ax0, a[i].x); ax1 = Mathf.Max(ax1, a[i].x);
                az0 = Mathf.Min(az0, a[i].y); az1 = Mathf.Max(az1, a[i].y);
            }
            float bx0 = 1e9f, bx1 = -1e9f, bz0 = 1e9f, bz1 = -1e9f;
            for (int i = 0; i < b.Length; i++)
            {
                bx0 = Mathf.Min(bx0, b[i].x); bx1 = Mathf.Max(bx1, b[i].x);
                bz0 = Mathf.Min(bz0, b[i].y); bz1 = Mathf.Max(bz1, b[i].y);
            }
            return !(ax1 + margin < bx0 || bx1 + margin < ax0 ||
                     az1 + margin < bz0 || bz1 + margin < az0);
        }

        private static bool NearestRoad(float x, float z, out Vector2 point, out float half)
        {
            bool found = false;
            float best = 1e9f;
            point = Vector2.zero; half = 0f;
            for (int ri = 0; ri < SulimanData.RoadCount; ri++)
            {
                int a = SulimanData.RoadOff[ri], b = SulimanData.RoadOff[ri + 1];
                for (int i = a; i + 1 < b; i++)
                {
                    float x1 = SulimanData.RoadPts[i * 2], z1 = SulimanData.RoadPts[i * 2 + 1];
                    float x2 = SulimanData.RoadPts[(i + 1) * 2], z2 = SulimanData.RoadPts[(i + 1) * 2 + 1];
                    float dx = x2 - x1, dz = z2 - z1;
                    float l2 = dx * dx + dz * dz;
                    float t = l2 < 1e-6f ? 0f : Mathf.Clamp01(((x - x1) * dx + (z - z1) * dz) / l2);
                    float px = x1 + dx * t, pz = z1 + dz * t;
                    float d = (x - px) * (x - px) + (z - pz) * (z - pz);
                    if (d < best) { best = d; point = new Vector2(px, pz); half = SulimanData.RoadW[ri] * 0.5f; found = true; }
                }
            }
            return found;
        }

        // ------------------------------------------------------------------ one building
        private static int BuildOne(int bi, MaterialBank walls, MaterialBank lit, MaterialBank cold,
            MaterialBank dark, MaterialBank board, MaterialBank shop, MaterialBank snow,
            MaterialBank trim, MaterialBank accent, MaterialBank wood, MaterialBank metal,
            SGeo balconyCol, ref int nWin, ref int nBal, ref int nDoor, ref int nSign)
        {
            int off = SulimanData.BldOff[bi], end = SulimanData.BldOff[bi + 1];
            int n = end - off;
            if (n < 3) return 0;
            var pts = new Vector2[n];
            for (int i = 0; i < n; i++)
                pts[i] = new Vector2(SulimanData.BldPts[(off + i) * 2], SulimanData.BldPts[(off + i) * 2 + 1]);

            int kind = SulimanData.BldKind[bi];
            int style = Mathf.Clamp(SulimanData.BldStyle[bi], 0, 13);
            int floors = Mathf.Clamp(SulimanData.BldFloors[bi], 1, 9);
            int litState = SulimanData.BldLit[bi];
            int sign = SulimanData.BldSign[bi];
            int seed = SulimanData.BldSeed[bi];
            var sr = new System.Random(seed);

            float cx = 0f, cz = 0f;
            for (int i = 0; i < n; i++) { cx += pts[i].x; cz += pts[i].y; }
            cx /= n; cz /= n;

            // v0.10.59: every building is 25 % larger in plan as well as in height
            const float BScale = 1.25f;
            for (int i = 0; i < n; i++)
                pts[i] = new Vector2(cx + (pts[i].x - cx) * BScale,
                                     cz + (pts[i].y - cz) * BScale);

            // OSM footprints that ended up in the river/pond are moved to the nearest dry bank
            bool wet = OnWater(cx, cz);
            if (!wet)
                for (int i = 0; i < n && !wet; i++) wet = OnWater(pts[i].x, pts[i].y);
            if (wet)
            {
                Vector2 sh = FindDryOffset(pts, cx, cz);
                if (sh.x != 0f || sh.y != 0f)
                {
                    for (int i = 0; i < n; i++) pts[i] += sh;
                    cx += sh.x; cz += sh.y;
                    wetMoved++;
                    if (wetMoved <= 12)
                        Log($"[SULIMAN] moved building {bi} off water by {sh.x:0},{sh.y:0} -> {cx:0},{cz:0}");
                }
                else
                {
                    wetSkipped++;
                    return 0;
                }
            }

            if (!PlaceBuilding(pts, ref cx, ref cz))
            {
                wetSkipped++;
                return 0;
            }

            float minX = 1e9f, maxX = -1e9f, minZ = 1e9f, maxZ = -1e9f;
            for (int i = 0; i < n; i++)
            {
                minX = Mathf.Min(minX, pts[i].x); maxX = Mathf.Max(maxX, pts[i].x);
                minZ = Mathf.Min(minZ, pts[i].y); maxZ = Mathf.Max(maxZ, pts[i].y);
            }
            if (!InMap(cx, cz)) return 0;
            if (InLandmarkClear(cx, cz))
            {
                lmSkipped++;
                return 0;
            }
            if (maxX - minX > 250f || maxZ - minZ > 250f)
            {
                wetSkipped++;
                Warn($"[SULIMAN] skipped oversized footprint {bi}: {maxX - minX:0}x{maxZ - minZ:0} m at {cx:0},{cz:0}");
                return 0;
            }
            placedPts.Add((Vector2[])pts.Clone());
            placedCenters.Add(new Vector2(cx, cz));

            float y0 = HeightAt(cx, cz);
            for (int i = 0; i < n; i++)
                y0 = Mathf.Min(y0, HeightAt(pts[i].x, pts[i].y));
            y0 -= 0.10f;

            float fh, plinth, roofRise = 0f;
            switch (kind)
            {
                case 0: fh = 2.95f; plinth = 0.55f; break;   // panel
                case 1: fh = 3.35f; plinth = 0.65f; break;   // stucco
                case 2: fh = 2.75f; plinth = 0.45f; break;   // wood
                case 3: fh = 2.65f; plinth = 0.35f; break;   // private
                case 4: fh = 2.80f; plinth = 0.25f; break;   // garage
                case 5: fh = 4.30f; plinth = 0.45f; break;   // industrial
                case 6: fh = 3.30f; plinth = 0.45f; break;   // shop
                case 7: fh = 3.65f; plinth = 0.70f; break;   // school / admin
                default: fh = 3.60f; plinth = 0.55f; break;  // church / ruin
            }
            if (kind == 5 && floors > 2) floors = 2;
            fh *= BScale;
            plinth *= BScale;
            float yTop = y0 + plinth + floors * fh;
            // v0.10.50: pitched roofs are disabled in Suliman — the axis-aligned gable of a
            // rotated/offset footprint did not meet the walls. Every building now gets the
            // same snow-capped flat roof, which always sits on its own parapet.
            bool pitched = false;
            if (pitched) roofRise = kind == 3 ? 1.5f : (kind == 2 ? 1.9f : (kind == 8 ? 3.0f : 2.6f));
            else yTop += 0.35f;

            bool abandoned = litState == 0 || (bi % 4 == 0);
            var wallMat = abandoned ? Facade[5] : Facade[style];
            var wg = walls.Geo(wallMat, 0.15f);

            // ---- edges ----
            edges.Clear();
            for (int i = 0; i < n; i++)
            {
                Vector2 a2 = pts[i], b2 = pts[(i + 1) % n];
                Vector3 a = new Vector3(a2.x, 0f, a2.y);
                Vector3 b = new Vector3(b2.x, 0f, b2.y);
                Vector3 d = b - a; d.y = 0f;
                float L = d.magnitude;
                if (L < 0.8f) continue;
                d /= L;
                var e = new BEdge
                {
                    a = a, b = b, t = d, n = new Vector3(d.z, 0f, -d.x), len = L
                };
                edges.Add(e);
            }
            if (edges.Count < 2) return 0;
            // make every wall normal point away from the footprint centre (some OSM rings are reversed)
            Vector3 cen = new Vector3(cx, 0f, cz);
            for (int i = 0; i < edges.Count; i++)
            {
                var e = edges[i];
                Vector3 mid = (e.a + e.b) * 0.5f;
                if (Vector3.Dot(e.n, mid - cen) < 0f)
                {
                    e.n = -e.n;
                    edges[i] = e;
                }
            }

            // longest edge decides the "front" (door / sign / balconies)
            int front = 0;
            for (int i = 1; i < edges.Count; i++) if (edges[i].len > edges[front].len) front = i;
            // the two shortest edges are the end walls of a block
            int endA = -1, endB = -1;
            float e1 = 1e9f, e2 = 1e9f;
            for (int i = 0; i < edges.Count; i++)
            {
                float len = edges[i].len;
                if (len < e1) { e2 = e1; endB = endA; e1 = len; endA = i; }
                else if (len < e2) { e2 = len; endB = i; }
            }
            int winIndex = 0;

            var wallTri = new SGeo { UvScale = 0.18f };   // collision prism

            float winH = Mathf.Min(1.62f, fh - 1.05f);
            float sillY = 0.92f;
            if (kind == 7) { winH = 2.0f; sillY = 0.85f; }
            if (kind == 6) { winH = 2.15f; sillY = 0.65f; }
            if (kind == 5) { winH = 2.0f; sillY = 1.1f; }

            for (int ei = 0; ei < edges.Count; ei++)
            {
                var e = edges[ei];
                bool realDoor = abandoned && ei == front;
                bool endEdge = abandoned && floors >= 3 && (ei == endA || ei == endB);
                float doorS = e.len * 0.5f;
                float doorGround = y0;
                var holes = abandoned ? new List<Vector4>() : null;
                if (abandoned)
                {
                    if (realDoor)
                    {
                        doorGround = HeightAt(e.a.x + e.t.x * doorS + e.n.x * 0.5f,
                                              e.a.z + e.t.z * doorS + e.n.z * 0.5f);
                        float yDoor = Mathf.Clamp(doorGround - 0.02f, y0, yTop - 2.6f);
                        float ds0 = Mathf.Clamp(doorS - 0.95f, 0.35f, e.len - 0.35f);
                        float ds1 = Mathf.Clamp(doorS + 0.95f, 0.35f, e.len - 0.35f);
                        if (ds1 - ds0 > 1.0f)
                            holes.Add(new Vector4(ds0, ds1, yDoor, Mathf.Min(yDoor + 2.4f, yTop - 0.2f)));
                        Vector3 th = e.a + e.t * doorS + e.n * (-WallT * 0.5f);
                        trim.Geo(ConcreteDarkMat, 0.2f).Box(
                            new Vector3(th.x, doorGround + 0.07f, th.z),
                            new Vector3(Mathf.Min(2.1f, e.len - 0.4f), 0.14f, WallT + 0.6f),
                            Quaternion.LookRotation(e.n, Vector3.up));
                        nDoor++;
                        Vector3 dp = e.a + e.t * doorS + e.n * 0.9f;
                        dp.y = doorGround + 1.25f;
                        doorProbes.Add(new DoorProbe { p = dp, d = -e.n, bi = bi });
                    }
                }
                else
                {
                    // wall (opaque shell)
                    wg.Quad(new Vector3(e.a.x, y0, e.a.z), new Vector3(e.b.x, y0, e.b.z),
                            new Vector3(e.b.x, yTop, e.b.z), new Vector3(e.a.x, yTop, e.a.z), e.n);
                    wallTri.Quad(new Vector3(e.a.x, y0 - 2f, e.a.z), new Vector3(e.b.x, y0 - 2f, e.b.z),
                                 new Vector3(e.b.x, yTop, e.b.z), new Vector3(e.a.x, yTop, e.a.z), e.n);
                }

                // plinth
                trim.Geo(ConcreteDarkMat, 0.2f).Quad(
                    new Vector3(e.a.x, y0 + 0.02f, e.a.z) + e.n * 0.03f,
                    new Vector3(e.b.x, y0 + 0.02f, e.b.z) + e.n * 0.03f,
                    new Vector3(e.b.x, y0 + plinth + 0.10f, e.b.z) + e.n * 0.03f,
                    new Vector3(e.a.x, y0 + plinth + 0.10f, e.a.z) + e.n * 0.03f, e.n);

                // snow drift against the base
                AddDrift(snow.Geo(SnowBankMat, 0.2f), e, y0);

                // floor bands (panel / stucco / school)
                if (kind == 0 || kind == 1 || kind == 7)
                {
                    var tg = trim.Geo(ConcreteMat, 0.2f);
                    for (int f = 1; f < floors; f++)
                    {
                        float y = y0 + plinth + f * fh;
                        tg.Quad(new Vector3(e.a.x, y - 0.075f, e.a.z) + e.n * 0.045f,
                                new Vector3(e.b.x, y - 0.075f, e.b.z) + e.n * 0.045f,
                                new Vector3(e.b.x, y + 0.075f, e.b.z) + e.n * 0.045f,
                                new Vector3(e.a.x, y + 0.075f, e.a.z) + e.n * 0.045f, e.n);
                    }
                }

                // windows
                bool frontEdge = ei == front;
                float margin = e.len > 8f ? 1.25f : 0.8f;
                int cols = Mathf.Max(0, Mathf.FloorToInt((e.len - margin * 2f) / 3.05f));
                if (cols == 0 && e.len > 2.4f) cols = 1;
                if (cols > 22) cols = 22;
                float spacing = cols > 1 ? (e.len - margin * 2f - 1.0f) / (cols - 1) : 0f;
                var doorCols = new List<int>();
                int doors = 0;
                if ((frontEdge || e.len > 18f) && (kind == 0 || kind == 1 || kind == 6 || kind == 7) && y0 > 0f)
                {
                    doors = kind == 0 ? Mathf.Clamp(Mathf.FloorToInt(e.len / 28f), 1, 3) : 1;
                    for (int d = 0; d < doors; d++)
                    {
                        int dc = cols <= 1 ? 0 : Mathf.RoundToInt((cols - 1) * (d + 1f) / (doors + 1f));
                        if (!doorCols.Contains(dc)) doorCols.Add(dc);
                    }
                }

                for (int f = 0; f < floors; f++)
                {
                    float floorBase = y0 + plinth + f * fh;
                    for (int c = 0; c < cols; c++)
                    {
                        float s = cols > 1 ? margin + 0.5f + c * spacing : e.len * 0.5f;
                        bool isDoor = f == 0 && doorCols.Contains(c);
                        if (isDoor) continue;
                        if (endEdge) continue;
                        if (realDoor && f == 0 && Mathf.Abs(s - doorS) < 1.9f) continue;
                        if (kind == 5 && sr.NextDouble() < 0.35) continue;
                        if (abandoned && sr.NextDouble() < 0.18) continue;

                        float ww = kind == 6 && f == 0 ? 2.2f : 1.35f;
                        if (kind == 5) ww = 1.9f;
                        if (spacing > 0f && ww > spacing - 0.45f) ww = Mathf.Max(0.8f, spacing - 0.45f);

                        if (abandoned)
                        {
                            // keep the window grid, but as an empty opening instead of glass
                            float yy0 = floorBase + sillY;
                            float s0 = Mathf.Clamp(s - ww * 0.5f, 0.3f, e.len - 0.3f);
                            float s1 = Mathf.Clamp(s + ww * 0.5f, 0.3f, e.len - 0.3f);
                            if (s1 - s0 > 0.5f)
                                holes.Add(new Vector4(s0, s1, yy0, Mathf.Min(yy0 + winH, yTop - 0.2f)));
                            if (winIndex % 3 == 0) AddBoards(e, s, yy0, s1 - s0, winH);
                            winIndex++;
                            nWin++;
                            continue;
                        }

                        // balcony on some columns of panel houses
                        bool balcony = (kind == 0 || kind == 1) && f >= 1 && (frontEdge || e.len > 13f) && (c % 2 == 0) && sr.NextDouble() < 0.55;
                        if (balcony)
                        {
                            AddBalcony(trim, snow, accent, balconyCol, e, s, floorBase, style, sr);
                            nBal++;
                        }

                        int cell;
                        var bank = lit;
                        if (kind == 6 && f == 0)
                        {
                            cell = 6; bank = shop;
                        }
                        else if (litState == 2 && sr.NextDouble() < 0.7)
                        {
                            cell = 3; bank = cold;
                        }
                        else if (sr.NextDouble() < (balcony ? 0.20 : 0.16))
                        {
                            cell = sr.Next(3); bank = lit;
                        }
                        else
                        {
                            cell = 4; bank = dark;
                        }

                        Vector3 wc = e.a + e.t * s + e.n * 0.035f;
                        float wy = floorBase + sillY;
                        AddWindow(bank.Geo(bank == lit ? WindowLitMat : bank == cold ? WindowColdMat :
                                           bank == board ? WindowBoardMat : bank == shop ? WindowShopMat : WindowDarkMat, 1f),
                                  wc, e.t, e.n, ww, winH, wy, cell);
                        trim.Geo(ConcreteMat, 0.2f).Box(
                            new Vector3(wc.x + e.n.x * 0.09f, wy - 0.07f, wc.z + e.n.z * 0.09f),
                            new Vector3(ww + 0.28f, 0.13f, 0.24f), Quaternion.LookRotation(e.n, Vector3.up));
                        snow.Geo(SnowBankMat, 0.4f).Box(
                            new Vector3(wc.x + e.n.x * 0.09f, wy - 0.005f, wc.z + e.n.z * 0.09f),
                            new Vector3(ww + 0.26f, 0.055f, 0.20f), Quaternion.LookRotation(e.n, Vector3.up));
                        nWin++;

                        // warm light spill from a lit ground-floor window
                        if (f == 0 && bank == lit && c % 2 == 0)
                        {
                            Vector3 lp = wc + e.n * 1.1f; lp.y = HeightAt(lp.x, lp.z) + 1.35f;
                            AddLampSpot(lp, 13f, new Color(1f, 0.72f, 0.42f));
                        }
                    }
                }

                // doors on this edge
                if (doors > 0 && !abandoned)
                {
                    for (int d = 0; d < doorCols.Count; d++)
                    {
                        float s = cols > 1 ? margin + 0.5f + doorCols[d] * spacing : e.len * 0.5f;
                        AddEntrance(trim, snow, metal, windowLit, e, s, y0, plinth, yTop, true, d, seed);
                        nDoor++;
                    }
                }

                // abandoned shell: build this wall with real door and window openings
                if (abandoned) WallWithHoles(wg, wallTri, e, y0, yTop, holes);

                // corner accent stripe on one short edge (the coloured panel ends of the reference)
                if (ei == front && (kind == 0 || kind == 1) && style >= 1 && style <= 5 && seed % 4 == 0)
                {
                    var ag = accent.Geo(Accent[(style + seed) % 5], 0.2f);
                    float aw = Mathf.Min(2.4f, e.len * 0.35f);
                    Vector3 baseP = e.a + e.t * (e.len - aw - 0.3f);
                    ag.Quad(new Vector3(baseP.x, y0 + plinth, baseP.z) + e.n * 0.06f,
                            new Vector3(baseP.x, y0 + plinth, baseP.z) + e.t * aw + e.n * 0.06f,
                            new Vector3(baseP.x, yTop, baseP.z) + e.t * aw + e.n * 0.06f,
                            new Vector3(baseP.x, yTop, baseP.z) + e.n * 0.06f, e.n);
                }
            }

            // ---- interior of an abandoned block: real walls with a doorway, a floor and a
            // first-floor ceiling, so every boarded house can actually be entered
            if (abandoned)
            {
                var fg = trim.Geo(ConcreteMat, 0.2f);
                var cg = trim.Geo(ConcreteDarkMat, 0.2f);
                if (floors >= 2)
                {
                    BuildAbandonedFloors(fg, cg, wallTri, pts, minX, maxX, minZ, maxZ,
                                         y0, plinth, fh, floors, yTop, cx, cz, edges[front].t);
                }
                else
                {
                    float maxGround = y0;
                    for (int i = 0; i < pts.Length; i++)
                        maxGround = Mathf.Max(maxGround, HeightAt(pts[i].x, pts[i].y));
                    maxGround = Mathf.Max(maxGround, HeightAt(cx, cz));
                    float ceilY = Mathf.Min(yTop - 0.15f, maxGround + fh - 0.15f);
                    AddInterior(fg, cg, pts, y0, ceilY);
                }
            }

            // ---- roof ----
            if (pitched)
            {
                AddPitchedRoof(walls.Geo(wallMat, 0.15f), snow, trim, metal, pts, yTop, roofRise, kind, sr);
                AddColliderRoof(wallTri, pts, yTop);
            }
            else
            {
                AddFlatRoof(walls.Geo(wallMat, 0.15f), snow, trim, metal, pts, cx, cz, yTop, kind, sr);
                AddColliderRoof(wallTri, pts, yTop);
            }

            // ---- chimney, vents, antennas ----
            if ((kind == 2 || kind == 3) && sr.NextDouble() < 0.75)
            {
                float chx = Mathf.Lerp(minX, maxX, 0.25f), chz = Mathf.Lerp(minZ, maxZ, 0.3f);
                AddChimney(trim, snow, new Vector3(chx, yTop + roofRise * 0.55f, chz), 3.4f);
            }
            if (kind == 0 && sr.NextDouble() < 0.55)
            {
                AddRoofTech(trim, metal, snow, cx, cz, yTop, minX, maxX, minZ, maxZ, sr);
            }
            if (kind == 5 && sr.NextDouble() < 0.6)
            {
                AddRoofVent(trim, snow, cx, cz, yTop, sr);
            }
            if (kind == 8)
            {
                float cy = yTop + roofRise + 0.35f;
                var mg = metal.Geo(MetalMat, 0.4f);
                mg.Beam(new Vector3(cx, cy, cz), new Vector3(cx, cy + 2.6f, cz), 0.13f, 0.13f, Vector3.forward);
                mg.Beam(new Vector3(cx - 0.75f, cy + 1.8f, cz), new Vector3(cx + 0.75f, cy + 1.8f, cz), 0.12f, 0.12f, Vector3.up);
            }

            // ---- sign ----
            // v0.10.50: all lettering on the map was removed at the user's request
            // (the boards/glow strips went with it; the OSM credit lives in CREDITS.md)

            wallTri.EmitCollider(Root, "bldg_col_" + bi);

            // yards are handled by the snow drifts; fences made the streets read as cluttered
            // if ((kind == 3 || kind == 4) && !abandoned && SulimanData.BldArea[bi] < 260f)
            //     AddYard(metal, snow, minX, maxX, minZ, maxZ, seed);
            return 1;
        }

        // ------------------------------------------------------------------ pieces

        private const float WallT = 0.35f;

        private static void WallQuad(SGeo g, BEdge e, float s0, float s1, float y0, float y1,
                                     float off, Vector3 n)
        {
            Vector3 a = e.a + e.t * s0 + e.n * off; a.y = y0;
            Vector3 b = e.a + e.t * s1 + e.n * off; b.y = y0;
            Vector3 c = b; c.y = y1;
            Vector3 d = a; d.y = y1;
            g.Quad(a, b, c, d, n);
        }

        private static void ColPanel(SGeo col, BEdge e, float s0, float s1, float y0, float yTop)
        {
            Vector3 a = e.a + e.t * s0; a.y = y0 - 2f;
            Vector3 b = e.a + e.t * s1; b.y = y0 - 2f;
            Vector3 c = b; c.y = yTop;
            Vector3 d = a; d.y = yTop;
            col.Quad(a, b, c, d, e.n);
            Vector3 ia = a - e.n * WallT, ib = b - e.n * WallT;
            Vector3 ic = c - e.n * WallT, id = d - e.n * WallT;
            col.Quad(ia, ib, ic, id, -e.n);
        }

        private static void WallSolid(SGeo wall, SGeo col, BEdge e, float y0, float yTop)
        {
            WallQuad(wall, e, 0f, e.len, y0, yTop, 0f, e.n);
            WallQuad(wall, e, 0f, e.len, y0, yTop, -WallT, -e.n);
            Vector3 oa = e.a, ob = e.b, ia = e.a - e.n * WallT, ib = e.b - e.n * WallT;
            oa.y = ob.y = ia.y = ib.y = yTop;
            wall.Quad(oa, ob, ib, ia, Vector3.up);
            ColPanel(col, e, 0f, e.len, y0, yTop);
        }

        private static void WallWithDoor(SGeo wall, SGeo col, BEdge e, float y0, float yTop,
                                         float sDoor, float wDoor, float hDoor, float yDoor)
        {
            wDoor = Mathf.Min(wDoor, e.len - 1.0f);
            if (wDoor < 1.0f) { WallSolid(wall, col, e, y0, yTop); return; }
            yDoor = Mathf.Clamp(yDoor, y0, yTop - hDoor - 0.2f);
            float s0 = Mathf.Clamp(sDoor - wDoor * 0.5f, 0.35f, e.len - 0.35f);
            float s1 = Mathf.Clamp(sDoor + wDoor * 0.5f, 0.35f, e.len - 0.35f);
            if (s1 - s0 < 1.2f) { s0 = e.len * 0.5f - 0.9f; s1 = s0 + 1.8f; }
            float hy = yDoor + hDoor;

            // outer and inner faces split around the opening
            WallQuad(wall, e, 0f, s0, y0, yTop, 0f, e.n);
            WallQuad(wall, e, s1, e.len, y0, yTop, 0f, e.n);
            WallQuad(wall, e, s0, s1, hy, yTop, 0f, e.n);
            WallQuad(wall, e, 0f, s0, y0, yTop, -WallT, -e.n);
            WallQuad(wall, e, s1, e.len, y0, yTop, -WallT, -e.n);
            WallQuad(wall, e, s0, s1, hy, yTop, -WallT, -e.n);
            // sill below the local ground line
            if (yDoor > y0 + 0.02f)
            {
                WallQuad(wall, e, s0, s1, y0, yDoor, 0f, e.n);
                WallQuad(wall, e, s0, s1, y0, yDoor, -WallT, -e.n);
                ColPanel(col, e, s0, s1, y0, yDoor);
            }

            // reveals: two jambs and the lintel underside
            Vector3 ao = e.a + e.t * s0, ai = ao - e.n * WallT;
            Vector3 bo = e.a + e.t * s1, bi = bo - e.n * WallT;
            wall.Quad(new Vector3(ao.x, yDoor, ao.z), new Vector3(ai.x, yDoor, ai.z),
                      new Vector3(ai.x, hy, ai.z), new Vector3(ao.x, hy, ao.z), e.t);
            wall.Quad(new Vector3(bo.x, yDoor, bo.z), new Vector3(bi.x, yDoor, bi.z),
                      new Vector3(bi.x, hy, bi.z), new Vector3(bo.x, hy, bo.z), -e.t);
            wall.Quad(new Vector3(ao.x, hy, ao.z), new Vector3(bo.x, hy, bo.z),
                      new Vector3(bi.x, hy, bi.z), new Vector3(ai.x, hy, ai.z), Vector3.down);

            // collision: only the solid parts, the doorway itself stays open
            ColPanel(col, e, 0f, s0, y0, yTop);
            ColPanel(col, e, s1, e.len, y0, yTop);
            Vector3 ca = e.a + e.t * s0; ca.y = hy;
            Vector3 cb = e.a + e.t * s1; cb.y = hy;
            Vector3 cd = cb; cd.y = yTop;
            Vector3 cc = ca; cc.y = yTop;
            col.Quad(ca, cb, cd, cc, e.n);
            Vector3 ica = ca - e.n * WallT, icb = cb - e.n * WallT;
            Vector3 icc = cc - e.n * WallT, icd = cd - e.n * WallT;
            col.Quad(ica, icb, icd, icc, -e.n);
        }

        private static void WallWithHoles(SGeo wall, SGeo col, BEdge e, float y0, float yTop,
                                          List<Vector4> holes)
        {
            // A wall punctured by any number of rectangular openings (door + window grid).
            // Decompose into horizontal strips and vertical panels; both faces and all reveals.
            col.Quad(new Vector3(e.a.x, y0 - 2f, e.a.z), new Vector3(e.b.x, y0 - 2f, e.b.z),
                     new Vector3(e.b.x, y0, e.b.z), new Vector3(e.a.x, y0, e.a.z), e.n);

            var hs = new List<Vector4>();
            for (int i = 0; i < holes.Count; i++)
            {
                var h = holes[i];
                float s0 = Mathf.Clamp(h.x, 0f, e.len);
                float s1 = Mathf.Clamp(h.y, 0f, e.len);
                float ya = Mathf.Clamp(h.z, y0, yTop);
                float yb = Mathf.Clamp(h.w, y0, yTop);
                if (s1 - s0 > 0.05f && yb - ya > 0.05f)
                    hs.Add(new Vector4(s0, s1, ya, yb));
            }

            var ys = new List<float> { y0, yTop };
            for (int i = 0; i < hs.Count; i++) { ys.Add(hs[i].z); ys.Add(hs[i].w); }
            ys.Sort();
            for (int i = 0; i + 1 < ys.Count; i++)
            {
                float ya = ys[i], yb = ys[i + 1];
                if (yb - ya < 0.012f) continue;
                var spans = new List<Vector2>();
                for (int h = 0; h < hs.Count; h++)
                    if (hs[h].z < yb - 0.005f && hs[h].w > ya + 0.005f)
                        spans.Add(new Vector2(hs[h].x, hs[h].y));
                spans.Sort((a, b) => a.x.CompareTo(b.x));
                float cur = 0f;
                for (int si = 0; si < spans.Count; si++)
                {
                    if (spans[si].x > cur + 0.012f) WallPanel(wall, col, e, cur, spans[si].x, ya, yb);
                    if (spans[si].y > cur) cur = spans[si].y;
                }
                if (cur < e.len - 0.012f) WallPanel(wall, col, e, cur, e.len, ya, yb);
            }

            // reveals: jambs, lintel and sill of every opening
            for (int i = 0; i < hs.Count; i++)
            {
                Vector4 h = hs[i];
                Vector3 ao = e.a + e.t * h.x, ai = ao - e.n * WallT;
                Vector3 bo = e.a + e.t * h.y, bi = bo - e.n * WallT;
                wall.Quad(new Vector3(ao.x, h.z, ao.z), new Vector3(ai.x, h.z, ai.z),
                          new Vector3(ai.x, h.w, ai.z), new Vector3(ao.x, h.w, ao.z), e.t);
                wall.Quad(new Vector3(bo.x, h.z, bo.z), new Vector3(bi.x, h.z, bi.z),
                          new Vector3(bi.x, h.w, bi.z), new Vector3(bo.x, h.w, bo.z), -e.t);
                wall.Quad(new Vector3(ao.x, h.w, ao.z), new Vector3(bo.x, h.w, bo.z),
                          new Vector3(bi.x, h.w, bi.z), new Vector3(ai.x, h.w, ai.z), Vector3.down);
                wall.Quad(new Vector3(ao.x, h.z, ao.z), new Vector3(bo.x, h.z, bo.z),
                          new Vector3(bi.x, h.z, bi.z), new Vector3(ai.x, h.z, ai.z), Vector3.up);
            }

            Vector3 oa = e.a, ob = e.b, ia = e.a - e.n * WallT, ib = e.b - e.n * WallT;
            oa.y = ob.y = ia.y = ib.y = yTop;
            wall.Quad(oa, ob, ib, ia, Vector3.up);
        }

        private static void WallPanel(SGeo wall, SGeo col, BEdge e, float s0, float s1,
                                      float ya, float yb)
        {
            WallQuad(wall, e, s0, s1, ya, yb, 0f, e.n);
            WallQuad(wall, e, s0, s1, ya, yb, -WallT, -e.n);
            Vector3 a = e.a + e.t * s0; a.y = ya;
            Vector3 b = e.a + e.t * s1; b.y = ya;
            Vector3 c = b; c.y = yb;
            Vector3 d = a; d.y = yb;
            col.Quad(a, b, c, d, e.n);
            Vector3 ia = a - e.n * WallT, ib = b - e.n * WallT;
            Vector3 ic = c - e.n * WallT, id = d - e.n * WallT;
            col.Quad(ia, ib, ic, id, -e.n);
        }

        private static void FindPlankTemplate()
        {
            if (plankFound) return;
            plankFound = true;
            var all = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < all.Length; i++)
            {
                var go = all[i];
                if (go == null || !go.scene.IsValid() || go.name != "WoodenPlank") continue;
                Transform child = go.transform.childCount > 0 ? go.transform.GetChild(0) : null;
                if (child == null) continue;
                var mf = child.GetComponent<MeshFilter>();
                var mr = child.GetComponent<MeshRenderer>();
                if (mf == null || mf.sharedMesh == null || mr == null || mr.sharedMaterial == null) continue;
                plankMesh = mf.sharedMesh;
                plankMat = mr.sharedMaterial;
                plankMeshSize = plankMesh.bounds.size;
                Log($"[SULIMAN] original plank mesh: {plankMesh.name} size={plankMeshSize} " +
                    $"mat={plankMat.name} (merged per window, no 19-child clone)");
                return;
            }
            Log("[SULIMAN] original WoodenPlank mesh not found; using fallback boards");
        }

        private static void AddPlankChild(GameObject parent, Vector3 pos, float width, float height,
                                          float depth, bool vertical)
        {
            var go = new GameObject("plank");
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = pos;
            bool longY = plankMeshSize.y >= plankMeshSize.x && plankMeshSize.y >= plankMeshSize.z;
            Quaternion rot;
            Vector3 scl;
            if (longY)
            {
                scl = new Vector3(height / plankMeshSize.x, width / plankMeshSize.y, depth / plankMeshSize.z);
                rot = vertical ? Quaternion.identity : Quaternion.Euler(0f, 0f, 90f);
            }
            else
            {
                scl = new Vector3(width / plankMeshSize.x, height / plankMeshSize.y, depth / plankMeshSize.z);
                rot = vertical ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
            }
            go.transform.localRotation = rot;
            go.transform.localScale = scl;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = plankMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = plankMat;
        }

        private static void AddBoards(BEdge e, float s, float yBottom, float w, float h)
        {
            if (w < 0.4f || h < 0.6f) return;
            if (plankMesh != null && plankMat != null &&
                plankMeshSize.x > 1e-5f && plankMeshSize.y > 1e-5f && plankMeshSize.z > 1e-5f)
            {
                var go = new GameObject("boards_original");
                go.transform.SetParent(Root, false);
                Vector3 c = e.a + e.t * s + e.n * 0.06f;
                c.y = yBottom + h * 0.5f;
                go.transform.localPosition = c;
                go.transform.localRotation = Quaternion.LookRotation(e.n, Vector3.up);

                float ph = h * 0.17f;
                for (int i = 0; i < 4; i++)
                {
                    float py = -h * 0.5f + 0.18f + i * (h - 0.36f) / 3f;
                    AddPlankChild(go, new Vector3(0f, py, 0f), w * 0.98f, ph, 0.07f, false);
                }
                AddPlankChild(go, new Vector3(-0.34f * w, 0f, 0f), h * 0.9f, 0.09f, 0.08f, true);
                AddPlankChild(go, new Vector3(0.34f * w, 0f, 0f), h * 0.9f, 0.09f, 0.08f, true);

                var bc = go.AddComponent<BoxCollider>();
                bc.size = new Vector3(w + 0.1f, h, 0.14f);
                bc.center = new Vector3(0f, 0f, 0.02f);
                go.AddComponent<SulimanBoards>();
                return;
            }

            var goFb = new GameObject("boards");
            goFb.transform.SetParent(Root, false);
            Vector3 cFb = e.a + e.t * s + e.n * 0.06f;
            cFb.y = yBottom + h * 0.5f;
            goFb.transform.localPosition = cFb;
            goFb.transform.localRotation = Quaternion.LookRotation(e.n, Vector3.up);
            var g = new SGeo { UvScale = 0.7f };
            for (int i = 0; i < 4; i++)
            {
                float py = -h * 0.5f + 0.16f + i * (h - 0.32f) / 3f;
                g.Box(new Vector3(0f, py, 0f), new Vector3(w * 0.98f, 0.24f, 0.07f), Quaternion.identity);
            }
            for (int sx = -1; sx <= 1; sx += 2)
                g.Box(new Vector3(sx * w * 0.34f, 0f, 0f), new Vector3(0.10f, h * 0.92f, 0.09f), Quaternion.identity);
            var meshFb = g.ToMesh("boards");
            goFb.AddComponent<MeshFilter>().sharedMesh = meshFb;
            var mrFb = goFb.AddComponent<MeshRenderer>();
            mrFb.sharedMaterial = WoodDarkMat != null ? WoodDarkMat : WoodMat;
            var bcFb = goFb.AddComponent<BoxCollider>();
            bcFb.size = new Vector3(w + 0.15f, h, 0.14f);
            bcFb.center = new Vector3(0f, 0f, 0.02f);
            goFb.AddComponent<SulimanBoards>();
        }

        private static bool PointInPoly(Vector2[] p, float x, float z)
        {
            bool inside = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            {
                if (((p[i].y > z) != (p[j].y > z)) &&
                    (x < (p[j].x - p[i].x) * (z - p[i].y) / (p[j].y - p[i].y) + p[i].x))
                    inside = !inside;
            }
            return inside;
        }

        private static bool RectInPoly(Vector2[] p, float x0, float z0, float x1, float z1)
            => PointInPoly(p, x0, z0) && PointInPoly(p, x1, z0) &&
               PointInPoly(p, x0, z1) && PointInPoly(p, x1, z1) &&
               PointInPoly(p, (x0 + x1) * 0.5f, (z0 + z1) * 0.5f);

        private static void Slab(SGeo mat, SGeo col, float x0, float z0, float x1, float z1,
                                 float top, float th)
        {
            if (x1 - x0 < 0.15f || z1 - z0 < 0.15f) return;
            var c = new Vector3((x0 + x1) * 0.5f, top - th * 0.5f, (z0 + z1) * 0.5f);
            var s = new Vector3(x1 - x0, th, z1 - z0);
            mat.Box(c, s, Quaternion.identity);
            if (col != null) col.Box(c, s, Quaternion.identity);
        }

        private static void WallBox(SGeo mat, SGeo col, Vector3 c, Vector3 s)
        {
            mat.Box(c, s, Quaternion.identity);
            if (col != null) col.Box(c, s, Quaternion.identity);
        }

        private static void PartitionZ(SGeo mat, SGeo col, float x, float z0, float z1,
                                       float ly, float fh, float doorH)
        {
            float doorC = (z0 + z1) * 0.5f, ds = doorC - 0.6f, de = doorC + 0.6f;
            if (ds > z0 + 0.1f)
                WallBox(mat, col, new Vector3(x, ly + fh * 0.5f, (z0 + ds) * 0.5f),
                         new Vector3(0.15f, fh, ds - z0));
            if (z1 > de + 0.1f)
                WallBox(mat, col, new Vector3(x, ly + fh * 0.5f, (de + z1) * 0.5f),
                         new Vector3(0.15f, fh, z1 - de));
            float lh = fh - doorH;
            if (lh > 0.1f)
                WallBox(mat, col, new Vector3(x, ly + doorH + lh * 0.5f, doorC),
                         new Vector3(0.15f, lh, de - ds));
        }

        private static void PartitionX(SGeo mat, SGeo col, float z, float x0, float x1,
                                       float ly, float fh, float doorH)
        {
            float doorC = (x0 + x1) * 0.5f, ds = doorC - 0.6f, de = doorC + 0.6f;
            if (ds > x0 + 0.1f)
                WallBox(mat, col, new Vector3((x0 + ds) * 0.5f, ly + fh * 0.5f, z),
                         new Vector3(ds - x0, fh, 0.15f));
            if (x1 > de + 0.1f)
                WallBox(mat, col, new Vector3((de + x1) * 0.5f, ly + fh * 0.5f, z),
                         new Vector3(x1 - de, fh, 0.15f));
            float lh = fh - doorH;
            if (lh > 0.1f)
                WallBox(mat, col, new Vector3(doorC, ly + doorH + lh * 0.5f, z),
                         new Vector3(de - ds, lh, 0.15f));
        }

        private static void StairFlight(SGeo mat, SGeo col, float r0, float r1,
                                        float w0, float w1, float fromY, float toY, int n, bool alongX)
        {
            float run = (r1 - r0) / n;
            float rise = (toY - fromY) / n;
            if (Mathf.Abs(run) < 0.01f) return;
            float bottom = Mathf.Min(fromY, toY) - 0.25f;
            for (int i = 0; i < n; i++)
            {
                float top = fromY + (i + 1) * rise;
                float h = top - bottom;
                if (h < 0.05f) continue;
                float rc = r0 + (i + 0.5f) * run;
                float wc = (w0 + w1) * 0.5f;
                Vector3 c = alongX ? new Vector3(rc, bottom + h * 0.5f, wc)
                                   : new Vector3(wc, bottom + h * 0.5f, rc);
                Vector3 s = alongX ? new Vector3(Mathf.Abs(run) * 1.02f, h, w1 - w0)
                                   : new Vector3(w1 - w0, h, Mathf.Abs(run) * 1.02f);
                mat.Box(c, s, Quaternion.identity);
                col.Box(c, s, Quaternion.identity);
            }
        }

        private static void BuildStair(SGeo mat, SGeo col, float x0, float x1, float z0, float z1,
                                       bool alongX, float baseY, float fh)
        {
            float midY = baseY + fh * 0.5f;
            const int n = 9;
            if (alongX)
            {
                StairFlight(mat, col, x0 + 1.4f, x1 - 0.2f, z0 + 0.1f, z0 + 1.4f, midY, baseY, n, true);
                Slab(mat, col, x0, z0, x0 + 1.4f, z1, midY, 0.2f);
                StairFlight(mat, col, x0 + 1.4f, x1 - 0.2f, z1 - 1.4f, z1 - 0.1f, midY, baseY + fh, n, true);
            }
            else
            {
                StairFlight(mat, col, z0 + 1.4f, z1 - 0.2f, x0 + 0.1f, x0 + 1.4f, midY, baseY, n, false);
                Slab(mat, col, x0, z0, x1, z0 + 1.4f, midY, 0.2f);
                StairFlight(mat, col, z0 + 1.4f, z1 - 0.2f, x1 - 1.4f, x1 - 0.1f, midY, baseY + fh, n, false);
            }
        }

        private static void BuildAbandonedFloors(SGeo floor, SGeo ceil, SGeo col,
            Vector2[] pts, float minX, float maxX, float minZ, float maxZ,
            float y0, float plinth, float fh, int floors, float yTop, float cx, float cz,
            Vector3 longDirIn)
        {
            Vector3 ld = new Vector3(longDirIn.x, 0f, longDirIn.z);
            if (ld.sqrMagnitude < 0.01f) ld = Vector3.right;
            ld.Normalize();
            interiorPoly = pts;
            Vector3 sd = new Vector3(-ld.z, 0f, ld.x);
            Vector3 center = new Vector3(cx, 0f, cz);

            float uMin = 1e9f, uMax = -1e9f, vMin = 1e9f, vMax = -1e9f;
            for (int i = 0; i < pts.Length; i++)
            {
                Vector3 d = new Vector3(pts[i].x - center.x, 0f, pts[i].y - center.z);
                float u = Vector3.Dot(d, ld), v = Vector3.Dot(d, sd);
                uMin = Mathf.Min(uMin, u); uMax = Mathf.Max(uMax, u);
                vMin = Mathf.Min(vMin, v); vMax = Mathf.Max(vMax, v);
            }
            float inset = 0.5f;
            float u0 = uMin + inset, u1 = uMax - inset, v0 = vMin + inset, v1 = vMax - inset;
            for (int k = 0; k < 16; k++)
            {
                if (RectGridInside(pts, center, ld, sd, u0, u1, v0, v1)) break;
                inset += 0.3f;
                u0 = uMin + inset; u1 = uMax - inset;
                v0 = vMin + inset; v1 = vMax - inset;
            }
            if (u1 - u0 < 5f || v1 - v0 < 5f) return;

            float vC = (v0 + v1) * 0.5f;
            float cu0 = u0 + 0.8f, cu1 = Mathf.Min(cu0 + 7.0f, u1);
            float cv0 = Mathf.Max(v0 + 0.4f, vC - 3.0f);
            float cv1 = Mathf.Min(v1 - 0.4f, cv0 + 6.0f);
            cv0 = Mathf.Max(v0 + 0.4f, cv1 - 6.0f);
            if (cu1 > u1 - 0.3f) cu1 = u1 - 0.3f;
            float hu1 = cu1 - 1.5f;                 // entry landing, door opens onto it
            if (hu1 - cu0 < 4.0f || cv1 - cv0 < 4.0f) return;
            Vector3 cc0 = center + ld * cu0 + sd * cv0;
            Vector3 cc1 = center + ld * cu1 + sd * cv1;
            Vector3 cc2 = center + ld * cu0 + sd * cv1;
            Vector3 cc3 = center + ld * cu1 + sd * cv0;
            if (!PointInPoly(pts, cc0.x, cc0.z) || !PointInPoly(pts, cc1.x, cc1.z) ||
                !PointInPoly(pts, cc2.x, cc2.z) || !PointInPoly(pts, cc3.x, cc3.z)) return;

            float groundY = y0 + 0.12f;
            float[] levels = new float[floors];
            levels[0] = groundY;
            for (int f = 1; f < floors; f++) levels[f] = y0 + plinth + f * fh;

            int[] gtri = Triangulate(pts);
            if (gtri != null)
            {
                for (int k = 0; k < gtri.Length; k += 3)
                {
                    Vector2 a2 = pts[gtri[k]], b2 = pts[gtri[k + 1]], c2 = pts[gtri[k + 2]];
                    Vector3 pa = new Vector3(a2.x, groundY, a2.y);
                    Vector3 pb = new Vector3(b2.x, groundY, b2.y);
                    Vector3 pc = new Vector3(c2.x, groundY, c2.y);
                    floor.Tri(pa, pb, pc, Vector3.up);
                    col.Tri(pa, pb, pc, Vector3.up);
                }
            }

            for (int f = 1; f < floors; f++)
            {
                AddRunFloor(floor, col, pts, levels[f], 0.5f, center, ld, sd,
                            cu0, hu1, cv0, cv1, minX, maxX, minZ, maxZ);
                AddCoreFrame(floor, col, center, ld, sd, cu0, hu1, cv0, cv1, levels[f], 0.35f);
                AddPerimeterFloor(floor, col, levels[f]);
            }

            AddPolyCeiling(ceil, pts, yTop - 0.15f);

            for (int f = 0; f < floors - 1; f++)
                BuildStairLocal(floor, col, center, ld, sd, cu0, hu1, cv0, cv1,
                                levels[f], levels[f + 1] - levels[f]);

            for (int f = 0; f < floors; f++)
            {
                float ly = levels[f];
                float ph = (f + 1 < floors) ? levels[f + 1] - levels[f] : fh;
                // stair enclosure: walls on all four sides, door to the corridor
                WallVLocal(floor, col, center, ld, sd, cu1, cv0, cv1, ly, ph, 1.6f, (cv0 + cv1) * 0.5f);
                WallVLocal(floor, col, center, ld, sd, cu0, cv0, cv1, ly, ph, 0f);
                WallULocal(floor, col, center, ld, sd, cv0, cu0, cu1, ly, ph, 0f);
                WallULocal(floor, col, center, ld, sd, cv1, cu0, cu1, ly, ph, 0f);
            }

            // corridor and rooms in the main block (skip when the block is too narrow)
            float vc = (v0 + v1) * 0.5f;
            if (v1 - v0 > 7f && u1 - (cu1 + 0.2f) > 7f)
            {
                for (int f = 0; f < floors; f++)
                {
                    float ly = levels[f];
                    float ph = (f + 1 < floors) ? levels[f + 1] - levels[f] : fh;
                    float wu0 = cu1 + 0.2f, wu1 = u1;
                    float[] doors =
                    {
                        wu0 + (wu1 - wu0) / 6f,
                        wu0 + (wu1 - wu0) * 0.5f,
                        wu0 + (wu1 - wu0) * 5f / 6f
                    };
                    WallULocal(floor, col, center, ld, sd, vc - 1.0f, wu0, wu1, ly, ph, 1.1f, doors);
                    WallULocal(floor, col, center, ld, sd, vc + 1.0f, wu0, wu1, ly, ph, 1.1f, doors);
                    for (int k = 1; k <= 2; k++)
                    {
                        float uu = wu0 + (wu1 - wu0) * k / 3f;
                        WallVLocal(floor, col, center, ld, sd, uu, v0, vc - 1.0f, ly, ph, 0f);
                        WallVLocal(floor, col, center, ld, sd, uu, vc + 1.0f, v1, ly, ph, 0f);
                    }
                }
            }
        }

        private static bool RectGridInside(Vector2[] pts, Vector3 center, Vector3 ld, Vector3 sd,
                                           float u0, float u1, float v0, float v1)
        {
            const int n = 10;
            for (int i = 0; i <= n; i++)
                for (int j = 0; j <= n; j++)
                {
                    float u = Mathf.Lerp(u0, u1, i / (float)n);
                    float v = Mathf.Lerp(v0, v1, j / (float)n);
                    Vector3 p = center + ld * u + sd * v;
                    if (!PointInPoly(pts, p.x, p.z)) return false;
                }
            return true;
        }

        private static void LocalBox(SGeo mat, SGeo col, Vector3 center, Vector3 ld, Vector3 sd,
                                     float u, float v, float y, Vector3 size)
        {
            Vector3 p = center + ld * u + sd * v;
            p.y = y;
            Quaternion rot = Quaternion.LookRotation(ld, Vector3.up);
            if (interiorPoly != null)
            {
                Vector3 h = size * 0.5f;
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sy = -1; sy <= 1; sy += 2)
                        for (int sz = -1; sz <= 1; sz += 2)
                        {
                            Vector3 q = p + rot * new Vector3(h.x * sx, h.y * sy, h.z * sz);
                            if (!PointInPoly(interiorPoly, q.x, q.z)) return;
                        }
            }
            mat.Box(p, size, rot);
            if (col != null) col.Box(p, size, rot);
        }

        private static void WallULocal(SGeo mat, SGeo col, Vector3 center, Vector3 ld, Vector3 sd,
                                       float v, float u0, float u1, float ly, float h,
                                       float doorW, params float[] doors)
        {
            if (u1 - u0 < 0.1f) return;
            var list = new System.Collections.Generic.List<float>(doors);
            list.Sort();
            float cur = u0;
            for (int i = 0; i < list.Count; i++)
            {
                float d = Mathf.Clamp(list[i], u0 + 0.3f, u1 - 0.3f);
                float d0 = d - doorW * 0.5f, d1 = d + doorW * 0.5f;
                if (d0 > cur + 0.05f)
                    LocalBox(mat, col, center, ld, sd, (cur + d0) * 0.5f, v, ly + h * 0.5f,
                             new Vector3(0.15f, h, d0 - cur));
                if (doorW > 0.1f && h > 2.4f)
                    LocalBox(mat, col, center, ld, sd, (d0 + d1) * 0.5f, v, ly + 2.4f + (h - 2.4f) * 0.5f,
                             new Vector3(0.15f, h - 2.4f, d1 - d0));
                cur = Mathf.Max(cur, d1);
            }
            if (u1 > cur + 0.05f)
                LocalBox(mat, col, center, ld, sd, (cur + u1) * 0.5f, v, ly + h * 0.5f,
                         new Vector3(0.15f, h, u1 - cur));
        }

        private static void WallVLocal(SGeo mat, SGeo col, Vector3 center, Vector3 ld, Vector3 sd,
                                       float u, float v0, float v1, float ly, float h,
                                       float doorW, float doorV = 0f)
        {
            if (v1 - v0 < 0.1f) return;
            if (doorW > 0.1f)
            {
                float d0 = Mathf.Clamp(doorV - doorW * 0.5f, v0 + 0.3f, v1 - 0.3f);
                float d1 = Mathf.Clamp(doorV + doorW * 0.5f, v0 + 0.3f, v1 - 0.3f);
                if (d0 > v0 + 0.05f)
                    LocalBox(mat, col, center, ld, sd, u, (v0 + d0) * 0.5f, ly + h * 0.5f,
                             new Vector3(d0 - v0, h, 0.15f));
                if (v1 > d1 + 0.05f)
                    LocalBox(mat, col, center, ld, sd, u, (d1 + v1) * 0.5f, ly + h * 0.5f,
                             new Vector3(v1 - d1, h, 0.15f));
                if (h > 2.4f)
                    LocalBox(mat, col, center, ld, sd, u, (d0 + d1) * 0.5f, ly + 2.4f + (h - 2.4f) * 0.5f,
                             new Vector3(d1 - d0, h - 2.4f, 0.15f));
            }
            else
            {
                LocalBox(mat, col, center, ld, sd, u, (v0 + v1) * 0.5f, ly + h * 0.5f,
                         new Vector3(v1 - v0, h, 0.15f));
            }
        }

        private static void StairFlightLocal(SGeo mat, SGeo col, Vector3 center, Vector3 ld, Vector3 sd,
                                             float r0, float r1, float w0, float w1,
                                             float fromY, float toY, int n)
        {
            float run = (r1 - r0) / n;
            float rise = (toY - fromY) / n;
            if (Mathf.Abs(run) < 0.01f) return;
            // sloped concrete soffit under the flight (thin, not a solid wedge)
            Vector3 sa = center + ld * r0 + sd * (w0 + w1) * 0.5f;
            Vector3 sb = center + ld * r1 + sd * (w0 + w1) * 0.5f;
            sa.y = fromY - 0.24f; sb.y = toY - 0.24f;
            mat.Beam(sa, sb, w1 - w0, 0.12f, Vector3.up);
            for (int i = 0; i < n; i++)
            {
                float top = fromY + (i + 1) * rise;
                float rc = r0 + (i + 0.5f) * run;
                // open tread (no solid wedge under the flight, so headroom stays clear)
                LocalBox(mat, col, center, ld, sd, rc, (w0 + w1) * 0.5f, top - 0.08f,
                         new Vector3(w1 - w0, 0.16f, Mathf.Abs(run) * 1.04f));
                // riser at the leading edge
                float rr = r0 + (i + 1) * run;
                float hh = Mathf.Abs(rise);
                if (hh > 0.02f)
                    LocalBox(mat, col, center, ld, sd, rr - Mathf.Sign(run) * 0.03f, (w0 + w1) * 0.5f,
                             top - Mathf.Sign(rise) * hh * 0.5f,
                             new Vector3(w1 - w0, hh, 0.06f));
            }
        }

        private static void BuildStairLocal(SGeo mat, SGeo col, Vector3 center, Vector3 ld, Vector3 sd,
                                            float u0, float u1, float v0, float v1,
                                            float baseY, float rise)
        {
            float midY = baseY + rise * 0.5f;
            const int n = 10;
            float lane = (v1 - v0 - 0.3f) * 0.5f;
            float vA0 = v0 + 0.1f, vA1 = vA0 + lane;
            float vB1 = v1 - 0.1f, vB0 = vB1 - lane;
            float far = u0 + 1.25f, entry = u1 - 0.25f;

            // entry (u1, base) -> far (u0, half-landing)
            StairFlightLocal(mat, col, center, ld, sd, far, entry, vA0, vA1, midY, baseY, n);
            LocalBox(mat, col, center, ld, sd, u0 + 0.625f, (v0 + v1) * 0.5f, midY - 0.1f,
                     new Vector3(v1 - v0, 0.2f, 1.25f));
            // half-landing -> entry (u1, top)
            StairFlightLocal(mat, col, center, ld, sd, far, entry, vB0, vB1, midY, baseY + rise, n);

            // handrails along the central well
            for (int lane2 = 0; lane2 < 2; lane2++)
            {
                float v = lane2 == 0 ? vA1 : vB0;
                Vector3 a = center + ld * far + sd * v; a.y = midY + 0.95f;
                Vector3 b = center + ld * entry + sd * v; b.y = baseY + 0.95f;
                if (lane2 == 1) b.y = baseY + rise + 0.95f;
                mat.Beam(a, b, 0.06f, 0.06f, Vector3.up);
                for (int k = 0; k <= 4; k++)
                {
                    Vector3 p = Vector3.Lerp(a, b, k / 4f);
                    mat.Box(new Vector3(p.x, p.y - 0.45f, p.z), new Vector3(0.07f, 0.9f, 0.07f),
                            Quaternion.identity);
                }
            }
        }

        private static void AddPolyCeiling(SGeo ceil, Vector2[] pts, float y)
        {
            int[] tri = Triangulate(pts);
            if (tri == null) return;
            for (int k = 0; k < tri.Length; k += 3)
            {
                Vector2 a = pts[tri[k]], b = pts[tri[k + 1]], c = pts[tri[k + 2]];
                ceil.Tri(new Vector3(a.x, y, a.y), new Vector3(b.x, y, b.y),
                         new Vector3(c.x, y, c.y), Vector3.down);
            }
        }

        private static void AddRunFloor(SGeo mat, SGeo col, Vector2[] pts, float top, float cell,
            Vector3 center, Vector3 ld, Vector3 sd, float cu0, float cu1, float cv0, float cv1,
            float minX, float maxX, float minZ, float maxZ)
        {
            float h = cell * 0.5f;
            for (float z = minZ + h; z < maxZ; z += cell)
            {
                bool run = false;
                float rx0 = 0f, rx1 = 0f;
                for (float x = minX + h; x < maxX; x += cell)
                {
                    bool inside = PointInPoly(pts, x, z) &&
                        PointInPoly(pts, x - h, z - h) && PointInPoly(pts, x + h, z - h) &&
                        PointInPoly(pts, x - h, z + h) && PointInPoly(pts, x + h, z + h);
                    Vector3 d = new Vector3(x - center.x, 0f, z - center.z);
                    float u = Vector3.Dot(d, ld), v = Vector3.Dot(d, sd);
                    bool hole = u > cu0 - h && u < cu1 + h && v > cv0 - h && v < cv1 + h;
                    bool ok = inside && !hole;
                    if (ok)
                    {
                        if (!run) { run = true; rx0 = x; }
                        rx1 = x;
                    }
                    else if (run)
                    {
                        EmitRun(mat, col, rx0, rx1, z, cell, top);
                        run = false;
                    }
                }
                if (run) EmitRun(mat, col, rx0, rx1, z, cell, top);
            }
        }

        private static void EmitRun(SGeo mat, SGeo col, float x0, float x1, float z, float cell, float top)
        {
            if (x1 - x0 < 0.02f) return;
            float h = cell * 0.5f;
            Slab(mat, col, x0 - h, z - h, x1 + h, z + h, top, 0.2f);
        }

        private static void AddCoreFrame(SGeo mat, SGeo col, Vector3 center, Vector3 ld, Vector3 sd,
            float u0, float u1, float v0, float v1, float top, float b)
        {
            float uc = (u0 + u1) * 0.5f, vc = (v0 + v1) * 0.5f;
            // strips along U (thickness along V)
            LocalBox(mat, col, center, ld, sd, uc, v0 - b * 0.5f, top - 0.11f,
                     new Vector3(b, 0.22f, u1 - u0 + b * 2f));
            LocalBox(mat, col, center, ld, sd, uc, v1 + b * 0.5f, top - 0.11f,
                     new Vector3(b, 0.22f, u1 - u0 + b * 2f));
            // strips along V (thickness along U)
            LocalBox(mat, col, center, ld, sd, u0 - b * 0.5f, vc, top - 0.11f,
                     new Vector3(v1 - v0 + b * 2f, 0.22f, b));
            LocalBox(mat, col, center, ld, sd, u1 + b * 0.5f, vc, top - 0.11f,
                     new Vector3(v1 - v0 + b * 2f, 0.22f, b));
        }

        private static void AddPerimeterFloor(SGeo mat, SGeo col, float level)
        {
            for (int i = 0; i < edges.Count; i++)
            {
                var e = edges[i];
                Vector3 mid = (e.a + e.b) * 0.5f - e.n * 0.3f;
                mid.y = level - 0.1f;
                var size = new Vector3(0.6f, 0.2f, e.len);
                var rot = Quaternion.LookRotation(e.t, Vector3.up);
                mat.Box(mid, size, rot);
                col.Box(mid, size, rot);
            }
        }

        private static void AddInterior(SGeo floor, SGeo ceil, Vector2[] pts, float y0, float ceilY)
        {
            int[] tri = Triangulate(pts);
            if (tri == null) return;
            for (int k = 0; k < tri.Length; k += 3)
            {
                Vector2 a = pts[tri[k]], b = pts[tri[k + 1]], c = pts[tri[k + 2]];
                floor.Tri(new Vector3(a.x, y0 + 0.12f, a.y),
                          new Vector3(b.x, y0 + 0.12f, b.y),
                          new Vector3(c.x, y0 + 0.12f, c.y), Vector3.up);
                ceil.Tri(new Vector3(a.x, ceilY, a.y),
                         new Vector3(c.x, ceilY, c.y),
                         new Vector3(b.x, ceilY, b.y), Vector3.down);
            }
        }


        private static void AddWindow(SGeo g, Vector3 c, Vector3 t, Vector3 n, float w, float h, float y, int cell)
        {
            Vector3 a = new Vector3(c.x - t.x * w * 0.5f, y, c.z - t.z * w * 0.5f);
            Vector3 b = new Vector3(c.x + t.x * w * 0.5f, y, c.z + t.z * w * 0.5f);
            Vector3 cc = new Vector3(b.x, y + h, b.z);
            Vector3 d = new Vector3(a.x, y + h, a.z);
            int col = cell % 4, row = cell / 4;
            float u0 = col * 0.25f + 0.004f, u1 = (col + 1) * 0.25f - 0.004f;
            float v0 = row * 0.25f + 0.004f, v1 = (row + 1) * 0.25f - 0.004f;
            // texture rows are bottom-up: row 0 is the bottom atlas row
            g.QuadUv(a, b, cc, d, n,
                     new Vector2(u0, v0), new Vector2(u1, v0),
                     new Vector2(u1, v1), new Vector2(u0, v1));
        }

        private static void AddDrift(SGeo g, BEdge e, float y0)
        {
            Vector3 a = e.a, b = e.b;
            Vector3 qa = a + e.n * 1.45f, qb = b + e.n * 1.45f;
            float ya = HeightAt(qa.x, qa.z) + 0.06f, yb = HeightAt(qb.x, qb.z) + 0.06f;
            g.QuadUv(new Vector3(a.x, y0 + 0.34f, a.z), new Vector3(b.x, y0 + 0.34f, b.z),
                     new Vector3(qb.x, yb, qb.z), new Vector3(qa.x, ya, qa.z),
                     (e.n * 0.5f + Vector3.up * 0.6f).normalized,
                     new Vector2(0, 0), new Vector2(e.len * 0.2f, 0),
                     new Vector2(e.len * 0.2f, 1), new Vector2(0, 1));
        }

        private static void AddEncasement(SGeo g, Vector3 a, Vector3 b, float y0, float y1, Vector3 n)
        {
            g.Quad(new Vector3(a.x, y0, a.z) + n * 0.03f, new Vector3(b.x, y0, b.z) + n * 0.03f,
                   new Vector3(b.x, y1, b.z) + n * 0.03f, new Vector3(a.x, y1, a.z) + n * 0.03f, n);
        }

        private static void AddBalcony(MaterialBank trim, MaterialBank snow, MaterialBank accent,
                                       SGeo balCol, BEdge e, float s, float floorBase, int style, System.Random sr)
        {
            Vector3 c = e.a + e.t * s;
            Vector3 outP = c + e.n * 1.05f;
            var tg = trim.Geo(ConcreteMat, 0.2f);
            tg.Box(new Vector3(outP.x, floorBase + 0.06f, outP.z),
                   new Vector3(2.15f, 0.16f, 1.85f), Quaternion.LookRotation(e.n, Vector3.up));
            snow.Geo(SnowBankMat, 0.4f).Box(new Vector3(outP.x, floorBase + 0.17f, outP.z),
                   new Vector3(2.05f, 0.07f, 1.75f), Quaternion.LookRotation(e.n, Vector3.up));
            // solid parapet on the outer edge
            Vector3 railC = c + e.n * 1.95f;
            var railMat = (style == 1 || style == 4) ? Accent[(style + sr.Next(3)) % 5] : ConcreteMat;
            var ag = accent.Geo(railMat, 0.4f);
            ag.Box(new Vector3(railC.x, floorBase + 0.62f, railC.z),
                   new Vector3(2.15f, 0.95f, 0.10f), Quaternion.LookRotation(e.n, Vector3.up));
            snow.Geo(SnowBankMat, 0.4f).Box(new Vector3(railC.x, floorBase + 1.11f, railC.z),
                   new Vector3(2.17f, 0.06f, 0.16f), Quaternion.LookRotation(e.n, Vector3.up));
            // collision: the slab is walkable, the parapet blocks the edge
            balCol.Box(new Vector3(outP.x, floorBase + 0.06f, outP.z),
                       new Vector3(2.15f, 0.16f, 1.85f), Quaternion.LookRotation(e.n, Vector3.up));
            balCol.Box(new Vector3(railC.x, floorBase + 0.62f, railC.z),
                       new Vector3(2.15f, 0.95f, 0.10f), Quaternion.LookRotation(e.n, Vector3.up));
            // side cheeks
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                Vector3 side = e.t * (1.07f * sgn);
                tg.Box(new Vector3(c.x + e.n.x * 1.02f + side.x, floorBase + 0.62f, c.z + e.n.z * 1.02f + side.z),
                       new Vector3(0.10f, 0.95f, 1.9f), Quaternion.LookRotation(e.n, Vector3.up));
            }
        }

        private static void AddEntrance(MaterialBank trim, MaterialBank snow, MaterialBank metal,
                                        Texture tex, BEdge e, float s, float y0, float plinth, float yTop,
                                        bool inhabited, int index, int seed)
        {
            Vector3 c = e.a + e.t * s + e.n * 0.06f;
            float gy = HeightAt(c.x, c.z);
            float baseY = y0 + plinth;                            // ground-floor slab
            baseY = Mathf.Min(baseY, yTop - 3.35f);              // never let the porch reach the roof
            // never hang a canopy above the roof, and never cut a door into an uphill bank
            if (gy > baseY - 0.02f) return;
            if (baseY + 2.95f > yTop - 0.10f) return;
            float rise = Mathf.Max(0.04f, baseY - gy);
            int stepCount = Mathf.Clamp(Mathf.CeilToInt(rise / 0.17f), 1, 6);
            float stepH = rise / stepCount;
            Vector3 nUp = Vector3.up;
            // recessed dark doorway, threshold on the floor slab
            var dg = metal.Geo(WoodDarkMat, 0.2f);
            Vector3 a = new Vector3(c.x - e.t.x * 0.75f, baseY + 0.02f, c.z - e.t.z * 0.75f);
            Vector3 b = new Vector3(c.x + e.t.x * 0.75f, baseY + 0.02f, c.z + e.t.z * 0.75f);
            dg.Quad(a, b, new Vector3(b.x, baseY + 2.27f, b.z), new Vector3(a.x, baseY + 2.27f, a.z), e.n);
            // frame (x = along the wall, z = outward)
            trim.Geo(ConcreteMat, 0.2f).Box(new Vector3(c.x, baseY + 1.22f, c.z),
                new Vector3(1.95f, 2.55f, 0.14f), Quaternion.LookRotation(e.n, nUp));
            trim.Geo(ConcreteMat, 0.2f).Box(new Vector3(c.x, baseY + 1.22f, c.z),
                new Vector3(1.52f, 2.24f, 0.20f), Quaternion.LookRotation(e.n, nUp));
            dg.Quad(new Vector3(a.x, baseY + 0.04f, a.z) + e.n * 0.08f,
                    new Vector3(b.x, baseY + 0.04f, b.z) + e.n * 0.08f,
                    new Vector3(b.x, baseY + 2.20f, b.z) + e.n * 0.08f,
                    new Vector3(a.x, baseY + 2.20f, a.z) + e.n * 0.08f, e.n);
            // stoop from the snow up to the floor slab
            var sg = trim.Geo(ConcreteMat, 0.2f);
            for (int k = 0; k < stepCount; k++)
            {
                float top = gy + (k + 1) * stepH;
                Vector3 sc = c + e.n * (0.55f + k * 0.28f);
                sg.Box(new Vector3(sc.x, gy + (k + 0.5f) * stepH, sc.z),
                       new Vector3(2.3f, stepH, 1.0f - k * 0.12f), Quaternion.LookRotation(e.n, nUp));
                snow.Geo(SnowBankMat, 0.4f).Box(new Vector3(sc.x, top + 0.02f, sc.z),
                       new Vector3(2.25f, 0.04f, 0.9f - k * 0.12f), Quaternion.LookRotation(e.n, nUp));
            }
            // canopy
            Vector3 cc = c + e.n * 0.85f;
            trim.Geo(ConcreteMat, 0.2f).Box(new Vector3(cc.x, baseY + 2.75f, cc.z),
                new Vector3(2.7f, 0.16f, 1.7f), Quaternion.LookRotation(e.n, nUp));
            snow.Geo(SnowBankMat, 0.4f).Box(new Vector3(cc.x, baseY + 2.86f, cc.z),
                new Vector3(2.75f, 0.08f, 1.75f), Quaternion.LookRotation(e.n, nUp));
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                Vector3 post = c + e.t * (1.15f * sgn) + e.n * 1.55f;
                metal.Geo(MetalMat, 0.5f).Box(new Vector3(post.x, baseY + 1.35f, post.z),
                    new Vector3(0.10f, 2.7f, 0.10f), Quaternion.identity);
            }
            if (!inhabited) return;
            // warm porch lamp
            Vector3 lampP = c + e.n * 0.35f;
            trim.Geo(GlowWarmMat, 1f).Box(new Vector3(lampP.x, baseY + 2.55f, lampP.z),
                new Vector3(0.55f, 0.14f, 0.20f), Quaternion.LookRotation(e.n, nUp));
            Vector3 lightP = lampP + e.n * 0.75f;
            lightP.y = baseY + 2.3f;
            AddLampSpot(lightP, 16f);
        }

        private static void AddColliderRoof(SGeo g, Vector2[] pts, float yTop)
        {
            int[] tri = Triangulate(pts);
            if (tri == null) return;   // never fall back to the bbox — that is the invisible barrier
            for (int k = 0; k < tri.Length; k += 3)
            {
                Vector2 a = pts[tri[k]], b = pts[tri[k + 1]], c = pts[tri[k + 2]];
                g.Tri(new Vector3(a.x, yTop, a.y), new Vector3(b.x, yTop, b.y), new Vector3(c.x, yTop, c.y),
                      Vector3.up, new Vector2(a.x * 0.2f, a.y * 0.2f),
                      new Vector2(b.x * 0.2f, b.y * 0.2f), new Vector2(c.x * 0.2f, c.y * 0.2f));
            }
        }

        private static int[] Triangulate(Vector2[] p)
        {
            int n = p.Length;
            if (n < 3) return null;
            var idx = new List<int>(n);
            for (int i = 0; i < n; i++) idx.Add(i);
            // signed area; keep CCW
            float area = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = p[i], b = p[(i + 1) % n];
                area += a.x * b.y - b.x * a.y;
            }
            if (area < 0f) idx.Reverse();
            var tris = new List<int>((n - 2) * 3);
            int guard = 0;
            while (idx.Count > 3 && guard++ < n * n)
            {
                bool ear = false;
                int m = idx.Count;
                for (int i = 0; i < m; i++)
                {
                    int i0 = idx[(i - 1 + m) % m], i1 = idx[i], i2 = idx[(i + 1) % m];
                    Vector2 a = p[i0], b = p[i1], c = p[i2];
                    float cross = (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x);
                    if (cross <= 0.00001f) continue;
                    bool contains = false;
                    for (int j = 0; j < m && !contains; j++)
                    {
                        int q = idx[j];
                        if (q == i0 || q == i1 || q == i2) continue;
                        if (PointInTri(p[q], a, b, c)) contains = true;
                    }
                    if (contains) continue;
                    tris.Add(i0); tris.Add(i1); tris.Add(i2);
                    idx.RemoveAt(i);
                    ear = true;
                    break;
                }
                if (!ear) break;
            }
            if (idx.Count == 3)
            {
                tris.Add(idx[0]); tris.Add(idx[1]); tris.Add(idx[2]);
            }
            if (tris.Count != (n - 2) * 3) return null;
            return tris.ToArray();
        }

        private static bool PointInTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            bool neg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool pos = (d1 > 0) || (d2 > 0) || (d3 > 0);
            return !(neg && pos);
        }

        private static void AddFlatRoof(SGeo wall, MaterialBank snow, MaterialBank trim, MaterialBank metal,
                                        Vector2[] pts, float cx, float cz, float yTop, int kind, System.Random sr)
        {
            var sg = snow.Geo(SnowBankMat, 0.12f);
            int[] tri = Triangulate(pts);
            if (tri != null)
            {
                for (int k = 0; k < tri.Length; k += 3)
                {
                    Vector2 a = pts[tri[k]], b = pts[tri[k + 1]], c2 = pts[tri[k + 2]];
                    sg.Tri(new Vector3(a.x, yTop + 0.10f, a.y),
                           new Vector3(b.x, yTop + 0.10f, b.y),
                           new Vector3(c2.x, yTop + 0.10f, c2.y), Vector3.up,
                           new Vector2(a.x * 0.2f, a.y * 0.2f),
                           new Vector2(b.x * 0.2f, b.y * 0.2f),
                           new Vector2(c2.x * 0.2f, c2.y * 0.2f));
                }
            }
            // parapet + its snow cap
            var tg = trim.Geo(ConcreteMat, 0.2f);
            for (int i = 0; i < pts.Length; i++)
            {
                Vector2 a2 = pts[i], b2 = pts[(i + 1) % pts.Length];
                Vector3 a = new Vector3(a2.x, 0f, a2.y), b = new Vector3(b2.x, 0f, b2.y);
                Vector3 d = b - a; d.y = 0;
                float L = d.magnitude;
                if (L < 0.8f) continue;
                d /= L;
                Vector3 n = new Vector3(d.z, 0f, -d.x);
                float h = kind == 0 ? 0.72f : 0.55f;
                tg.Box(new Vector3((a.x + b.x) * 0.5f, yTop + h * 0.5f, (a.z + b.z) * 0.5f),
                       new Vector3(L, h, 0.30f), Quaternion.LookRotation(n, Vector3.up));
                sg.Box(new Vector3((a.x + b.x) * 0.5f, yTop + h + 0.04f, (a.z + b.z) * 0.5f),
                       new Vector3(L + 0.04f, 0.09f, 0.36f), Quaternion.LookRotation(n, Vector3.up));
            }
        }

        private static void AddPitchedRoof(SGeo wall, MaterialBank snow, MaterialBank trim, MaterialBank metal,
                                           Vector2[] pts, float yTop, float rise, int kind, System.Random sr)
        {
            float minX = 1e9f, maxX = -1e9f, minZ = 1e9f, maxZ = -1e9f;
            for (int i = 0; i < pts.Length; i++)
            {
                minX = Mathf.Min(minX, pts[i].x); maxX = Mathf.Max(maxX, pts[i].x);
                minZ = Mathf.Min(minZ, pts[i].y); maxZ = Mathf.Max(maxZ, pts[i].y);
            }
            float over = 0.55f;
            minX -= over; maxX += over; minZ -= over; maxZ += over;
            bool alongX = (maxX - minX) >= (maxZ - minZ);
            float ridgeY = yTop + rise;
            var sg = snow.Geo(SnowBankMat, 0.12f);

            if (alongX)
            {
                float mz = (minZ + maxZ) * 0.5f;
                wall.Quad(new Vector3(minX, yTop, minZ), new Vector3(maxX, yTop, minZ),
                          new Vector3(maxX, ridgeY, mz), new Vector3(minX, ridgeY, mz),
                          new Vector3(0f, 0.6f, -0.8f).normalized);
                wall.Quad(new Vector3(minX, ridgeY, mz), new Vector3(maxX, ridgeY, mz),
                          new Vector3(maxX, yTop, maxZ), new Vector3(minX, yTop, maxZ),
                          new Vector3(0f, 0.6f, 0.8f).normalized);
                sg.Quad(new Vector3(minX - 0.08f, yTop + 0.12f, minZ - 0.10f), new Vector3(maxX + 0.08f, yTop + 0.12f, minZ - 0.10f),
                        new Vector3(maxX + 0.08f, ridgeY + 0.14f, mz), new Vector3(minX - 0.08f, ridgeY + 0.14f, mz),
                        new Vector3(0f, 1f, -0.2f).normalized);
                sg.Quad(new Vector3(minX - 0.08f, ridgeY + 0.14f, mz), new Vector3(maxX + 0.08f, ridgeY + 0.14f, mz),
                        new Vector3(maxX + 0.08f, yTop + 0.12f, maxZ + 0.10f), new Vector3(minX - 0.08f, yTop + 0.12f, maxZ + 0.10f),
                        new Vector3(0f, 1f, 0.2f).normalized);
                // gables
                wall.Tri(new Vector3(minX, yTop, minZ), new Vector3(minX, yTop, maxZ), new Vector3(minX, ridgeY, mz), Vector3.left);
                wall.Tri(new Vector3(maxX, yTop, minZ), new Vector3(maxX, ridgeY, mz), new Vector3(maxX, yTop, maxZ), Vector3.right);
                if (kind == 2 || kind == 3) AddIcicles(snow, trim, new Vector3(minX, yTop, minZ), new Vector3(maxX, yTop, minZ), 14, sr);
            }
            else
            {
                float mx = (minX + maxX) * 0.5f;
                wall.Quad(new Vector3(minX, yTop, minZ), new Vector3(mx, ridgeY, minZ),
                          new Vector3(mx, ridgeY, maxZ), new Vector3(minX, yTop, maxZ),
                          new Vector3(-0.8f, 0.6f, 0f).normalized);
                wall.Quad(new Vector3(mx, ridgeY, minZ), new Vector3(maxX, yTop, minZ),
                          new Vector3(maxX, yTop, maxZ), new Vector3(mx, ridgeY, maxZ),
                          new Vector3(0.8f, 0.6f, 0f).normalized);
                sg.Quad(new Vector3(minX - 0.10f, yTop + 0.12f, minZ - 0.08f), new Vector3(mx, ridgeY + 0.14f, minZ - 0.08f),
                        new Vector3(mx, ridgeY + 0.14f, maxZ + 0.08f), new Vector3(minX - 0.10f, yTop + 0.12f, maxZ + 0.08f),
                        new Vector3(-0.2f, 1f, 0f).normalized);
                sg.Quad(new Vector3(mx, ridgeY + 0.14f, minZ - 0.08f), new Vector3(maxX + 0.10f, yTop + 0.12f, minZ - 0.08f),
                        new Vector3(maxX + 0.10f, yTop + 0.12f, maxZ + 0.08f), new Vector3(mx, ridgeY + 0.14f, maxZ + 0.08f),
                        new Vector3(0.2f, 1f, 0f).normalized);
                wall.Tri(new Vector3(minX, yTop, minZ), new Vector3(mx, ridgeY, minZ), new Vector3(maxX, yTop, minZ), Vector3.back);
                wall.Tri(new Vector3(maxX, yTop, maxZ), new Vector3(mx, ridgeY, maxZ), new Vector3(minX, yTop, maxZ), Vector3.forward);
                if (kind == 2 || kind == 3) AddIcicles(snow, trim, new Vector3(minX, yTop, minZ), new Vector3(minX, yTop, maxZ), 10, sr);
            }
        }

        private static void AddIcicles(MaterialBank snow, MaterialBank trim, Vector3 a, Vector3 b, int n, System.Random sr)
        {
            var ig = snow.Geo(IceMat, 1f);
            for (int i = 0; i < n; i++)
            {
                float t = (i + 0.5f) / n;
                Vector3 p = Vector3.Lerp(a, b, t);
                float len = 0.18f + (float)sr.NextDouble() * 0.55f;
                float r = 0.035f + (float)sr.NextDouble() * 0.035f;
                Vector3 tip = p + Vector3.down * len;
                Vector3 d = new Vector3(r, 0f, 0f), e = new Vector3(0f, 0f, r);
                ig.Tri(p - d, p + d, tip, Vector3.down, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
                ig.Tri(p + e, p - e, tip, Vector3.down, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            }
        }

        private static void AddChimney(MaterialBank trim, MaterialBank snow, Vector3 at, float h)
        {
            float gy = HeightAt(at.x, at.z);
            float baseY = Mathf.Min(at.y, gy + 1f);
            trim.Geo(ConcreteDarkMat, 0.2f).Box(new Vector3(at.x, baseY + h * 0.5f, at.z),
                new Vector3(0.9f, h, 0.9f), Quaternion.identity);
            snow.Geo(SnowBankMat, 0.4f).Box(new Vector3(at.x, baseY + h + 0.05f, at.z),
                new Vector3(1.02f, 0.10f, 1.02f), Quaternion.identity);
        }

        private static void AddRoofTech(MaterialBank trim, MaterialBank metal, MaterialBank snow,
                                        float cx, float cz, float yTop, float minX, float maxX, float minZ, float maxZ,
                                        System.Random sr)
        {
            float bx = Mathf.Lerp(minX, maxX, 0.68f), bz = Mathf.Lerp(minZ, maxZ, 0.62f);
            trim.Geo(ConcreteMat, 0.2f).Box(new Vector3(bx, yTop + 1.25f, bz),
                new Vector3(3.2f, 2.4f, 2.4f), Quaternion.identity);
            snow.Geo(SnowBankMat, 0.4f).Box(new Vector3(bx, yTop + 2.50f, bz),
                new Vector3(3.35f, 0.12f, 2.55f), Quaternion.identity);
            var mg = metal.Geo(MetalMat, 0.5f);
            for (int i = 0; i < 3; i++)
            {
                float ax = Mathf.Lerp(minX, maxX, 0.25f + i * 0.1f);
                float az = Mathf.Lerp(minZ, maxZ, 0.35f);
                mg.Beam(new Vector3(ax, yTop + 0.2f, az), new Vector3(ax, yTop + 2.6f + sr.Next(3) * 0.5f, az), 0.05f, 0.05f, Vector3.forward);
                mg.Beam(new Vector3(ax - 0.4f, yTop + 2.4f, az), new Vector3(ax + 0.4f, yTop + 2.4f, az), 0.035f, 0.035f, Vector3.up);
            }
        }

        private static void AddRoofVent(MaterialBank trim, MaterialBank snow, float cx, float cz, float yTop, System.Random sr)
        {
            for (int i = 0; i < 3; i++)
            {
                float x = cx + (i - 1) * 4.0f;
                trim.Geo(MetalMat, 0.5f).Box(new Vector3(x, yTop + 0.55f, cz + 1.5f),
                    new Vector3(0.9f, 1.1f, 0.9f), Quaternion.identity);
                snow.Geo(SnowBankMat, 0.4f).Box(new Vector3(x, yTop + 1.12f, cz + 1.5f),
                    new Vector3(1.0f, 0.09f, 1.0f), Quaternion.identity);
            }
        }

        private static void AddSign(MaterialBank trim, MaterialBank metal, MaterialBank snow,
                                    BEdge e, float y, string text, int seed)
        {
            float len = Mathf.Min(6.2f, Mathf.Max(2.4f, text.Length * 0.30f + 0.8f));
            Vector3 c = e.a + e.t * (e.len * 0.5f);
            Vector3 p = c + e.n * 0.12f;
            metal.Geo(SignBoardMat, 0.4f).Box(new Vector3(p.x, y, p.z),
                new Vector3(len, 0.95f, 0.12f), Quaternion.LookRotation(e.t, Vector3.up));
            // a thin backlit edge, not a glowing white slab
            trim.Geo(GlowWarmMat, 1f).Box(new Vector3(p.x, y - 0.43f, p.z) + e.n * 0.02f,
                new Vector3(len - 0.10f, 0.035f, 0.03f), Quaternion.LookRotation(e.t, Vector3.up));
            trim.Geo(GlowWarmMat, 1f).Box(new Vector3(p.x, y + 0.43f, p.z) + e.n * 0.02f,
                new Vector3(len - 0.10f, 0.035f, 0.03f), Quaternion.LookRotation(e.t, Vector3.up));
            if (SignFont == null) return;
            var go = new GameObject("sign_" + seed);
            go.transform.SetParent(Root, false);
            go.transform.localPosition = new Vector3(p.x, y, p.z) + e.n * 0.10f;
            go.transform.localRotation = Quaternion.LookRotation(-e.n, Vector3.up);
            var tm = go.AddComponent<TextMesh>();
            tm.font = SignFont;
            tm.fontSize = 64;
            tm.characterSize = 0.060f * Mathf.Clamp(6f / Mathf.Max(3f, text.Length * 0.55f), 0.7f, 1.3f);
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.text = text;
            tm.color = new Color(1f, 0.86f, 0.62f);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = SignFont.material;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
        }

        private static bool NearRoof(Vector3 p, float y)
            => Mathf.Abs(p.y - y) < 3f && p.x > 0f && p.z > 0f;

        private static bool InLandmarkClear(float x, float z)
        {
            Vector2[] c =
            {
                new Vector2(900f, 580f), new Vector2(630f, 400f), new Vector2(568f, 1286f),
                new Vector2(1180f, 1380f), new Vector2(930f, 920f), new Vector2(760f, 300f),
                new Vector2(444f, 1133f), new Vector2(1560f, 700f), new Vector2(1000f, 1440f),
                new Vector2(1180f, 1180f)
            };
            float[] r = { 64f, 75f, 55f, 55f, 40f, 65f, 45f, 50f, 35f, 35f };
            for (int i = 0; i < c.Length; i++)
                if ((x - c[i].x) * (x - c[i].x) + (z - c[i].y) * (z - c[i].y) < r[i] * r[i])
                    return true;
            return false;
        }

        private static bool NearRoadAt(float x, float z, float margin)
        {
            for (int ri = 0; ri < SulimanData.RoadCount; ri++)
            {
                int a = SulimanData.RoadOff[ri], b = SulimanData.RoadOff[ri + 1];
                for (int i = a; i + 1 < b; i++)
                {
                    float x1 = SulimanData.RoadPts[i * 2], z1 = SulimanData.RoadPts[i * 2 + 1];
                    float x2 = SulimanData.RoadPts[(i + 1) * 2], z2 = SulimanData.RoadPts[(i + 1) * 2 + 1];
                    float dx = x2 - x1, dz = z2 - z1;
                    float L2 = dx * dx + dz * dz;
                    float t = L2 < 1e-6f ? 0f : Mathf.Clamp01(((x - x1) * dx + (z - z1) * dz) / L2);
                    float px = x1 + dx * t, pz = z1 + dz * t;
                    if ((x - px) * (x - px) + (z - pz) * (z - pz) < margin * margin) return true;
                }
            }
            return false;
        }

        private static void AddYard(MaterialBank metal, MaterialBank snow,
                                    float minX, float maxX, float minZ, float maxZ, int seed)
        {
            const float pad = 3.2f;
            float x0 = minX - pad, x1 = maxX + pad, z0 = minZ - pad, z1 = maxZ + pad;
            if (x1 - x0 > 34f || z1 - z0 > 34f) return;
            var sr = new System.Random(seed ^ 0x51f1);

            // four sides; the longest one gets a gate
            var sides = new[]
            {
                new Vector4(x0, z0, x1, z0),
                new Vector4(x1, z0, x1, z1),
                new Vector4(x1, z1, x0, z1),
                new Vector4(x0, z1, x0, z0)
            };
            int gateSide = 0;
            float bestL = 0f;
            for (int i = 0; i < 4; i++)
            {
                float L = Vector2.Distance(new Vector2(sides[i].x, sides[i].y),
                                           new Vector2(sides[i].z, sides[i].w));
                if (L > bestL) { bestL = L; gateSide = i; }
            }
            for (int si = 0; si < 4; si++)
            {
                var s = sides[si];
                Vector3 a = new Vector3(s.x, 0f, s.y);
                Vector3 b = new Vector3(s.z, 0f, s.w);
                Vector3 d = b - a; d.y = 0f;
                float L = d.magnitude;
                if (L < 2f) continue;
                Vector3 mid = (a + b) * 0.5f;
                if (NearRoadAt(mid.x, mid.z, 4.5f)) continue;
                d /= L;
                Vector3 n = new Vector3(d.z, 0f, -d.x);
                int segs = Mathf.Max(1, Mathf.RoundToInt(L / 2.3f));
                float seg = L / segs;
                for (int i = 0; i < segs; i++)
                {
                    float t0 = (i + 0.5f) * seg;
                    if (si == gateSide && Mathf.Abs(t0 - L * 0.5f) < 1.5f) continue;
                    Vector3 c = a + d * t0;
                    Vector3 e0 = a + d * Mathf.Max(0f, t0 - seg * 0.5f);
                    Vector3 e1 = a + d * Mathf.Min(L, t0 + seg * 0.5f);
                    if (NearRoadAt(c.x, c.z, 1.8f) || NearRoadAt(e0.x, e0.z, 1.8f) ||
                        NearRoadAt(e1.x, e1.z, 1.8f)) continue;
                    float gy = HeightAt(c.x, c.z);
                    metal.Geo(ConcreteMat, 0.2f).Box(new Vector3(c.x, gy + 0.62f, c.z),
                        new Vector3(seg - 0.07f, 1.16f, 0.08f), Quaternion.LookRotation(n, Vector3.up));
                    snow.Geo(SnowBankMat, 0.4f).Box(new Vector3(c.x, gy + 1.23f, c.z),
                        new Vector3(seg - 0.06f, 0.05f, 0.14f), Quaternion.LookRotation(n, Vector3.up));
                    if (i == 0 || i == segs - 1)
                        metal.Geo(ConcreteMat, 0.2f).Box(new Vector3(c.x, gy + 0.7f, c.z),
                            new Vector3(0.10f, 1.4f, 0.10f), Quaternion.identity);
                }
            }
        }
    }
}
