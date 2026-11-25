using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.Rendering;

namespace BabbdiCoop
{
    /// <summary>
    /// v0.10.49 — "СУЛИМАН": a separate, much larger snowy town built from real Susuman
    /// (Magadan oblast) OpenStreetMap footprints and SRTM terrain, placed far away from the
    /// BABBDI level and reached through the red memorial on the district roof (press E).
    ///
    /// Everything is procedural: terrain, snow, facades, lit windows, street lamps, utility
    /// poles, frozen river, conifers and the eight brutalist landmarks from the reference set.
    /// </summary>
    internal static partial class Suliman
    {
        public static bool Enabled = true;
        /// <summary>Debug switch: keeps the night preset stored but disables fog / dark sky / night ambient.</summary>
        public static bool DebugVision = true;
        public static bool Built;

        public static Transform Root;
        public static Vector3 Origin = new Vector3(12000f, 600f, 9000f);
        public static Vector3 SpawnLocal = new Vector3(958f, 0f, 716f);
        public static float SpawnYaw = 0f;

        /// <summary>World-space anchor of the return obelisk in Suliman (set at build).</summary>
        public static Vector3 ReturnAnchor;
        /// <summary>The memorial on the district roof; its plinth is the outward portal.</summary>
        public static Transform Memorial;

        public static bool Inside;
        public static bool BuiltOnce;

        private static ManualLogSource log;
        private static System.Random rng;
        private static bool envSaved;
        private static bool oldFog;
        private static FogMode oldFogMode;
        private static Color oldFogColor;
        private static float oldFogDensity;
        private static AmbientMode oldAmbientMode;
        private static Color oldAmbient;
        private static float oldAmbientIntensity;
        private static Material oldSkybox;
        private static Light oldSun;
        private static float oldSunIntensity;
        private static Color oldSunColor;
        private static LightShadows oldSunShadows;
        private static bool oldSunEnabled;
        private static float oldShadowDistance;
        private static CameraClearFlags oldCamClear;
        private static Color oldCamBg;

        private static GameObject moonGo;
        private static Light moon;
        private static Light fillLight;
        private static GameObject weatherGo;
        private static ParticleSystem snowPs;
        private static bool heldShadowFixed;
        private static readonly List<Renderer> heldRends = new List<Renderer>();
        private static readonly List<bool> heldRecv = new List<bool>();
        private static readonly List<ShadowCastingMode> heldCast = new List<ShadowCastingMode>();
        private static readonly List<UnityEngine.Rendering.LightProbeUsage> heldProbe = new List<UnityEngine.Rendering.LightProbeUsage>();
        private static readonly List<UnityEngine.Rendering.ReflectionProbeUsage> heldRefl = new List<UnityEngine.Rendering.ReflectionProbeUsage>();
        private static readonly List<Renderer> propRends = new List<Renderer>();
        private static readonly List<Material[]> propMats = new List<Material[]>();
        private static readonly List<TriggerZone> levelZones = new List<TriggerZone>();
        private static bool levelZonesSaved;
        private static readonly List<Skybox> cameraSkyboxes = new List<Skybox>();
        private static bool cameraSkyboxesSaved;
        private static readonly List<Renderer> skyGeometry = new List<Renderer>();
        private static bool skyGeometrySaved;
        private static readonly List<Behaviour> cameraFog = new List<Behaviour>();
        private static bool cameraFogSaved;

        private static float cooldown;
        private static string toast;
        private static float toastUntil;
        private static GUIStyle promptStyle;

        private static readonly List<Light> poolLights = new List<Light>();
        private static readonly List<Vector3> poolSpots = new List<Vector3>();
        private static readonly List<float> poolRanges = new List<float>();
        private static readonly List<Color> poolColors = new List<Color>();
        private static readonly List<int> poolUsed = new List<int>();
        private static float poolNext;

        public const int PoolCount = 22;

        // material banks
        internal static Material[] Facade = new Material[14];
        internal static Material[] Accent = new Material[5];
        internal static Material SnowMat, SnowBankMat, ConcreteMat, ConcreteDarkMat, MetalMat, RustMat,
            WoodMat, WoodDarkMat, AsphaltMat, PackedMat, IceMat, RoofMat, RoofDarkMat,
            GlassDarkMat, WindowLitMat, WindowColdMat, WindowDarkMat, WindowBoardMat, WindowShopMat,
            LampHeadMat, HaloTexMat, PoolMat, FlakeMat, SmokeMat, SignBoardMat, GlowRedMat, GlowWarmMat,
            ShaftMat, PortalMat, PortalEdgeMat, StepMat;

        internal static Font SignFont;

        // ------------------------------------------------------------------ entry

        public static void TryBuild()
        {
            if (BuiltOnce && Root == null)
            {
                // the level was reloaded: the whole town went with the old scene
                if (envSaved) RestoreEnv();
                Inside = false;
                Built = false;
                BuiltOnce = false;
                Memorial = null;
                heights = null;
                nightSky = null;
                nightSkyMat = null;
                treeMat = null;
                treeSnowMat = null;
                snowPs = null;
                weatherGo = null;
                moon = null;
                fillLight = null;
                RestoreHeldShadowFix();
                RestorePropellerMaterials();
                RestoreLevelZones();
                RestoreCameraSkyboxes();
                RestoreSkyGeometry();
                RestoreCameraFog();
                poolSpots.Clear();
                poolRanges.Clear();
                poolColors.Clear();
                poolLights.Clear();
                poolUsed.Clear();
                Log("[SULIMAN] scene changed: town statics reset");
            }
            if (!Enabled || BuiltOnce) return;
            if (Time.time < 6f) return;
            if (FirstPersonController.instance == null) return;
            BuiltOnce = true;
            try { Build(); }
            catch (Exception e)
            {
                log = log ?? BepInEx.Logging.Logger.CreateLogSource("BabbdiCoop");
                log.LogError("[SULIMAN] build failed: " + e);
            }
        }

        private static void Build()
        {
            log = BepInEx.Logging.Logger.CreateLogSource("BabbdiCoop");
            rng = new System.Random(20261125);
            float t0 = Time.realtimeSinceStartup;
            poolSpots.Clear();
            poolRanges.Clear();
            poolColors.Clear();
            poolLights.Clear();
            poolUsed.Clear();
            Inside = false;

            MakeMaterials();

            var go = new GameObject("Suliman");
            go.transform.SetPositionAndRotation(Origin, Quaternion.identity);
            Root = go.transform;

            Vector3 spawn = Origin + SpawnLocal;
            spawn.y = Origin.y + HeightAt(SpawnLocal.x, SpawnLocal.z) + 0.18f;
            SpawnLocal = new Vector3(SpawnLocal.x, spawn.y - Origin.y, SpawnLocal.z);
            Vector3 ret = spawn + new Vector3(0f, 0f, -5.8f);
            ret.y = Origin.y + HeightAt(ret.x - Origin.x, ret.z - Origin.z) + 0.05f;
            ReturnAnchor = ret;

            BuildTerrain();
            BuildRoads();
            BuildBuildings();
            BuildProps();
            BuildLandmarks();
            BuildWeather();
            BuildPortal();

            Built = true;
            log.LogInfo($"[SULIMAN] built in {Time.realtimeSinceStartup - t0:0.00}s: " +
                        $"{SulimanData.BldCount} buildings, {SulimanData.RoadCount} road pieces, " +
                        $"{SulimanData.LampCount} lamps, {SulimanData.TreeCount} trees, " +
                        $"map {SulimanData.W:F0}x{SulimanData.D:F0} m " +
                        $"({SulimanData.W * SulimanData.D / 1e6f:0.00} km2), origin {Origin}, " +
                        $"spawn {spawn}");
            log.LogInfo("[SULIMAN] winter night: snow, lit windows, 187 lamps, frozen river, 8 landmarks; " +
                        "portal = red memorial on the district roof (E within 3 m)");
        }

        // ------------------------------------------------------------------ data

        private static float[] heights;

        internal static float HeightAt(float x, float z)
        {
            if (heights == null)
            {
                byte[] raw = Convert.FromBase64String(SulimanData.HeightB64);
                heights = new float[SulimanData.NX * SulimanData.NZ];
                for (int i = 0; i < heights.Length; i++)
                    heights[i] = BitConverter.ToUInt16(raw, i * 2) * 0.1f + SulimanData.BaseY;
                CarveRiverValley(heights);
            }
            float fx = Mathf.Clamp(x / SulimanData.CellX, 0f, SulimanData.NX - 1.001f);
            float fz = Mathf.Clamp(z / SulimanData.CellZ, 0f, SulimanData.NZ - 1.001f);
            int ix = (int)fx, iz = (int)fz;
            float tx = fx - ix, tz = fz - iz;
            int i0 = iz * SulimanData.NX + ix;
            int i1 = i0 + SulimanData.NX;
            float a = Mathf.Lerp(heights[i0], heights[i0 + 1], tx);
            float b = Mathf.Lerp(heights[i1], heights[i1 + 1], tx);
            return Mathf.Lerp(a, b, tz);
        }

        internal static bool InMap(float x, float z)
            => x > 2f && z > 2f && x < SulimanData.W - 2f && z < SulimanData.D - 2f;

        // ------------------------------------------------------------------ geometry

        /// <summary>Vertex/index accumulator with world-anchored UVs.</summary>
        internal sealed class SGeo
        {
            public readonly List<Vector3> V = new List<Vector3>(1024);
            public readonly List<Vector3> N = new List<Vector3>(1024);
            public readonly List<Vector2> UV = new List<Vector2>(1024);
            public readonly List<int> T = new List<int>(2048);
            public float UvScale = 0.25f;

            private static void Frame(Vector3 n, out Vector3 tx, out Vector3 ty)
            {
                if (Mathf.Abs(n.y) > 0.7f) { tx = Vector3.right; ty = Vector3.forward; }
                else { tx = Vector3.Cross(Vector3.up, n).normalized; ty = Vector3.up; }
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
            {
                Vector3 tx, ty; Frame(n, out tx, out ty);
                QuadUv(a, b, c, d, n,
                    new Vector2(Vector3.Dot(a, tx), Vector3.Dot(a, ty)) * UvScale,
                    new Vector2(Vector3.Dot(b, tx), Vector3.Dot(b, ty)) * UvScale,
                    new Vector2(Vector3.Dot(c, tx), Vector3.Dot(c, ty)) * UvScale,
                    new Vector2(Vector3.Dot(d, tx), Vector3.Dot(d, ty)) * UvScale);
            }

            public void QuadUv(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n,
                               Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
            {
                int i = V.Count;
                V.Add(a); V.Add(b); V.Add(c); V.Add(d);
                N.Add(n); N.Add(n); N.Add(n); N.Add(n);
                UV.Add(ua); UV.Add(ub); UV.Add(uc); UV.Add(ud);
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), n) > 0f)
                { T.Add(i); T.Add(i + 1); T.Add(i + 2); T.Add(i); T.Add(i + 2); T.Add(i + 3); }
                else
                { T.Add(i); T.Add(i + 2); T.Add(i + 1); T.Add(i); T.Add(i + 3); T.Add(i + 2); }
            }

            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 n)
            {
                Vector3 tx, ty; Frame(n, out tx, out ty);
                Tri(a, b, c, n,
                    new Vector2(Vector3.Dot(a, tx), Vector3.Dot(a, ty)) * UvScale,
                    new Vector2(Vector3.Dot(b, tx), Vector3.Dot(b, ty)) * UvScale,
                    new Vector2(Vector3.Dot(c, tx), Vector3.Dot(c, ty)) * UvScale);
            }

            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 n,
                            Vector2 ua, Vector2 ub, Vector2 uc)
            {
                int i = V.Count;
                V.Add(a); V.Add(b); V.Add(c);
                N.Add(n); N.Add(n); N.Add(n);
                UV.Add(ua); UV.Add(ub); UV.Add(uc);
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), n) > 0f)
                { T.Add(i); T.Add(i + 1); T.Add(i + 2); }
                else { T.Add(i); T.Add(i + 2); T.Add(i + 1); }
            }

            public void Box(Vector3 c, Vector3 s, Quaternion r)
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
                Quad(p[1], p[3], p[7], p[5], R);
                Quad(p[0], p[4], p[6], p[2], -R);
                Quad(p[2], p[6], p[7], p[3], U);
                Quad(p[0], p[1], p[5], p[4], -U);
                Quad(p[4], p[5], p[7], p[6], F);
                Quad(p[0], p[2], p[3], p[1], -F);
            }

            /// <summary>Axis-aligned-ish box between two points (for thin beams, rails, icicles).</summary>
            public void Beam(Vector3 a, Vector3 b, float w, float h, Vector3 up)
            {
                Vector3 d = b - a;
                float L = d.magnitude;
                if (L < 0.001f) return;
                Vector3 f = d / L;
                Vector3 r = Vector3.Cross(up, f);
                if (r.sqrMagnitude < 1e-4f) r = Vector3.Cross(Vector3.right, f);
                r.Normalize();
                Vector3 u = Vector3.Cross(f, r).normalized;
                Box((a + b) * 0.5f, new Vector3(w, h, L), Quaternion.LookRotation(f, u));
            }

            public void Cylinder(Vector3 baseP, float radius, float height, int sides,
                                 MaterialBank bank, Vector3? tilt = null)
            {
                Vector3 top = baseP + (tilt ?? Vector3.up) * height;
                for (int i = 0; i < sides; i++)
                {
                    float a0 = Mathf.PI * 2f * i / sides;
                    float a1 = Mathf.PI * 2f * (i + 1) / sides;
                    Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                    Vector3 d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                    Vector3 p0 = baseP + d0 * radius, p1 = baseP + d1 * radius;
                    Vector3 q0 = top + d0 * radius, q1 = top + d1 * radius;
                    Vector3 n = (d0 + d1).normalized;
                    Quad(p0, p1, q1, q0, n);
                }
            }

            // Horizontal circular cap. Cylinder() only builds the side wall, so open
            // silos and tanks used to show straight through their tops.
            public void Disc(Vector3 c, float radius, int sides)
            {
                for (int i = 0; i < sides; i++)
                {
                    float a0 = Mathf.PI * 2f * i / sides;
                    float a1 = Mathf.PI * 2f * (i + 1) / sides;
                    Vector3 p0 = c + new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                    Vector3 p1 = c + new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
                    Tri(c, p0, p1, Vector3.up);
                }
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                m.indexFormat = IndexFormat.UInt32;
                m.SetVertices(V);
                m.SetNormals(N);
                m.SetUVs(0, UV);
                m.SetTriangles(T, 0);
                m.RecalculateBounds();
                return m;
            }

            public GameObject Emit(Transform parent, string name, Material mat, bool collider = false,
                                   ShadowCastingMode shadows = ShadowCastingMode.On)
            {
                if (T.Count == 0) return null;
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                var mesh = ToMesh(name);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = shadows;
                mr.receiveShadows = true;
                if (collider)
                {
                    var mc = go.AddComponent<MeshCollider>();
                    mc.sharedMesh = mesh;
                }
                return go;
            }

            /// <summary>Collision-only copy of this mesh (no renderer).</summary>
            public void EmitCollider(Transform parent, string name)
            {
                if (T.Count == 0) return;
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                var mesh = ToMesh(name);
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
        }

        /// <summary>Material -> one merged mesh. Keeps draw calls in the tens instead of thousands.</summary>
        internal sealed class MaterialBank
        {
            private readonly Dictionary<Material, SGeo> map = new Dictionary<Material, SGeo>();
            private readonly Transform parent;
            private readonly string prefix;

            public MaterialBank(Transform parent, string prefix)
            {
                this.parent = parent;
                this.prefix = prefix;
            }

            public SGeo Geo(Material m, float uv = 0.25f)
            {
                SGeo g;
                if (!map.TryGetValue(m, out g))
                {
                    g = new SGeo { UvScale = uv };
                    map[m] = g;
                }
                return g;
            }

            public void Emit(bool colliders = false, ShadowCastingMode shadows = ShadowCastingMode.On)
            {
                int i = 0;
                foreach (var kv in map)
                {
                    var go = kv.Value.Emit(parent, prefix + "_" + (kv.Key != null ? kv.Key.name : "null") + "_" + i,
                                           kv.Key, colliders, shadows);
                    if (go != null) i++;
                }
            }
        }

        // ------------------------------------------------------------------ primitives

        internal static void AddSnowCap(SGeo g, Vector3 c, Vector3 size, Quaternion rot)
        {
            g.Box(c, size, rot);
        }

        internal static void LightPool(Vector3 worldLocal, float radius, float intensity)
        {
            Vector3 p = worldLocal;
            float y = HeightAt(p.x, p.z) + 0.04f;
            var g = new SGeo { UvScale = 1f };
            float r = radius;
            g.QuadUv(new Vector3(p.x - r, y, p.z - r), new Vector3(p.x - r, y, p.z + r),
                     new Vector3(p.x + r, y, p.z + r), new Vector3(p.x + r, y, p.z - r),
                     Vector3.up, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
            var go = new GameObject("pool");
            go.transform.SetParent(Root, false);
            var mesh = g.ToMesh("pool");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var mat = PoolMat;
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        // ------------------------------------------------------------------ environment

        public static void SetMemorial(Transform tr)
        {
            if (log == null) log = BepInEx.Logging.Logger.CreateLogSource("BabbdiCoop");
            Memorial = tr;
            Log("[SULIMAN] portal anchor: memorial on the district roof at " +
                (tr != null ? tr.position.ToString() : "null"));
        }

        internal static Camera PlayerCam()
        {
            var fpc = FirstPersonController.instance;
            if (fpc != null && fpc.playerCamera != null) return fpc.playerCamera;
            return Camera.main;
        }

        private static void RememberEnv()
        {
            if (envSaved) return;
            envSaved = true;
            oldFog = RenderSettings.fog;
            oldFogMode = RenderSettings.fogMode;
            oldFogColor = RenderSettings.fogColor;
            oldFogDensity = RenderSettings.fogDensity;
            oldAmbientMode = RenderSettings.ambientMode;
            oldAmbient = RenderSettings.ambientLight;
            oldAmbientIntensity = RenderSettings.ambientIntensity;
            oldSkybox = RenderSettings.skybox;
            var oldCam = PlayerCam();
            if (oldCam != null)
            {
                oldCamClear = oldCam.clearFlags;
                oldCamBg = oldCam.backgroundColor;
            }
            oldSun = null;
            foreach (var l in UnityEngine.Object.FindObjectsOfType<Light>())
            {
                if (l != null && l.type == LightType.Directional && l.enabled && l.intensity > 0.01f)
                { oldSun = l; break; }
            }
            if (oldSun != null)
            {
                oldSunIntensity = oldSun.intensity;
                oldSunColor = oldSun.color;
                oldSunShadows = oldSun.shadows;
                oldSunEnabled = oldSun.enabled;
            }
            oldShadowDistance = QualitySettings.shadowDistance;
        }

        private static void ApplyNight()
        {
            RememberEnv();
            if (DebugVision)
            {
                // Debug view: the night preset above is kept in the fields below, only
                // its rendering (fog, black sky, near-zero ambient) is switched off.
                RenderSettings.fog = false;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.42f, 0.45f, 0.52f);
                RenderSettings.ambientIntensity = 1f;
                RenderSettings.skybox = null;
                QualitySettings.shadowDistance = 320f;
            }
            else
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = new Color(0.002f, 0.003f, 0.005f);
                RenderSettings.fogDensity = 0.012f;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.004f, 0.005f, 0.009f);
                RenderSettings.ambientIntensity = 0.75f;
                RenderSettings.skybox = null;
                QualitySettings.shadowDistance = 220f;
            }

            if (oldSun != null)
            {
                oldSun.enabled = false;
                oldSun.intensity = 0f;
                oldSun.shadows = LightShadows.None;
            }

            if (moon == null)
            {
                var go = new GameObject("SulimanMoon");
                go.transform.SetParent(Root, false);
                moon = go.AddComponent<Light>();
                moon.type = LightType.Directional;
                moon.color = new Color(0.62f, 0.74f, 1f);
                moon.intensity = 0f;
                moon.shadows = LightShadows.None;
                go.transform.rotation = Quaternion.Euler(48f, -38f, 0f);
            }
            moon.enabled = false;

            var cam = PlayerCam();
            if (cam != null)
            {
                cam.farClipPlane = Mathf.Max(cam.farClipPlane, 1500f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = DebugVision ? new Color(0.075f, 0.090f, 0.130f) : Color.black;
                DisableCameraSkybox(cam);
                DisableCameraFog(cam);
                HideLevelSkyGeometry();
                if (!camComponentsLogged)
                {
                    camComponentsLogged = true;
                    foreach (var c in cam.GetComponents<Component>())
                        Log($"[SULIMAN] camera component: {(c != null ? c.GetType().FullName : "null")} ({c?.name})");
                    if (cam.transform.parent != null)
                        foreach (var c in cam.transform.parent.GetComponents<Component>())
                            Log($"[SULIMAN] camera parent component: {(c != null ? c.GetType().FullName : "null")} ({c?.name})");
                }
                EnsureFillLight(cam.transform);
            EnsureNightGuard(cam);
            }
            if (weatherGo != null) weatherGo.SetActive(true);
            if (snowPs != null && !snowPs.isPlaying) snowPs.Play();
            EnsureHeldShadowFix();
            DisableLevelZones();
            if (!envNightLogged)
            {
                envNightLogged = true;
                string fc = RenderSettings.fogColor.ToString();
                Log($"[SULIMAN] night environment: fog={RenderSettings.fog} color={fc} dens={RenderSettings.fogDensity:0.0000} " +
                    $"ambient={RenderSettings.ambientLight} mode={RenderSettings.ambientMode} " +
                    $"sky={(RenderSettings.skybox != null ? RenderSettings.skybox.name : "none")} " +
                    $"moon={(moon != null ? moon.intensity : 0f)} debugVision={DebugVision}");
            }
        }

        private static bool envNightLogged;
        private static bool camComponentsLogged;

        private static void DisableCameraSkybox(Camera cam)
        {
            if (!cameraSkyboxesSaved)
            {
                cameraSkyboxesSaved = true;
                cameraSkyboxes.AddRange(cam.GetComponentsInChildren<Skybox>(true));
                if (cameraSkyboxes.Count > 0)
                    Log($"[SULIMAN] disabled {cameraSkyboxes.Count} camera Skybox component(s)");
            }
            for (int i = 0; i < cameraSkyboxes.Count; i++)
                if (cameraSkyboxes[i] != null) cameraSkyboxes[i].enabled = false;
        }

        private static void RestoreCameraSkyboxes()
        {
            for (int i = 0; i < cameraSkyboxes.Count; i++)
                if (cameraSkyboxes[i] != null) cameraSkyboxes[i].enabled = true;
            cameraSkyboxes.Clear();
            cameraSkyboxesSaved = false;
        }

        private static void DisableCameraFog(Camera cam)
        {
            if (!cameraFogSaved)
            {
                cameraFogSaved = true;
                foreach (var b in cam.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (b == null) continue;
                    string n = b.GetType().Name;
                    if (n.IndexOf("Fog", StringComparison.OrdinalIgnoreCase) >= 0)
                        cameraFog.Add(b);
                }
                if (cameraFog.Count > 0)
                    Log($"[SULIMAN] disabled {cameraFog.Count} camera fog component(s)");
            }
            for (int i = 0; i < cameraFog.Count; i++)
                if (cameraFog[i] != null) cameraFog[i].enabled = false;
        }

        private static void RestoreCameraFog()
        {
            for (int i = 0; i < cameraFog.Count; i++)
                if (cameraFog[i] != null) cameraFog[i].enabled = true;
            cameraFog.Clear();
            cameraFogSaved = false;
        }

        private static void HideLevelSkyGeometry()
        {
            if (!skyGeometrySaved)
            {
                skyGeometrySaved = true;
                foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
                {
                    if (r == null || !r.gameObject.scene.IsValid()) continue;
                    string n = r.gameObject.name;
                    if (n.IndexOf("Cloud", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("FogCard", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("Sky", StringComparison.OrdinalIgnoreCase) >= 0)
                        skyGeometry.Add(r);
                }
                if (skyGeometry.Count > 0)
                    Log($"[SULIMAN] hidden {skyGeometry.Count} level sky/cloud renderer(s)");
            }
            for (int i = 0; i < skyGeometry.Count; i++)
                if (skyGeometry[i] != null) skyGeometry[i].enabled = false;
        }

        private static void RestoreSkyGeometry()
        {
            for (int i = 0; i < skyGeometry.Count; i++)
                if (skyGeometry[i] != null) skyGeometry[i].enabled = true;
            skyGeometry.Clear();
            skyGeometrySaved = false;
        }

        private static void EnsureNightGuard(Camera cam)
        {
            if (cam == null) return;
            if (cam.GetComponent<SulimanNightGuard>() == null)
                cam.gameObject.AddComponent<SulimanNightGuard>();
        }

        private static void DisableLevelZones()
        {
            if (!levelZonesSaved)
            {
                levelZonesSaved = true;
                foreach (var z in Resources.FindObjectsOfTypeAll<TriggerZone>())
                    if (z != null && z.gameObject.scene.IsValid()) levelZones.Add(z);
                if (levelZones.Count > 0)
                    Log($"[SULIMAN] disabled {levelZones.Count} level TriggerZone(s) while inside");
            }
            for (int i = 0; i < levelZones.Count; i++)
                if (levelZones[i] != null) levelZones[i].enabled = false;
        }

        private static void RestoreLevelZones()
        {
            for (int i = 0; i < levelZones.Count; i++)
                if (levelZones[i] != null) levelZones[i].enabled = true;
            levelZones.Clear();
            levelZonesSaved = false;
        }

        public static void EnforceNight()
        {
            if (Built && Inside) ApplyNight();
        }

        private static void EnsureFillLight(Transform cam)
        {
            if (fillLight == null)
            {
                var go = new GameObject("SulimanFillLight");
                fillLight = go.AddComponent<Light>();
                fillLight.type = LightType.Point;
                fillLight.color = new Color(0.78f, 0.84f, 1f);
                fillLight.intensity = 0.55f;
                fillLight.range = 3.6f;
                fillLight.shadows = LightShadows.None;
            }
            if (fillLight.transform.parent != cam)
                fillLight.transform.SetParent(cam, false);
            fillLight.transform.localPosition = new Vector3(0f, 0.08f, 0.55f);
            fillLight.enabled = true;
        }

        /// <summary>
        /// The handheld propeller is lit by the level's light probes/baked light in the city; with
        /// Suliman's flat night ambient its blades read as black shreds. Shadow casting on a
        /// viewmodel also produces stripe acne at this angle. Both are fixed for the held object.
        /// </summary>
        private static void EnsureHeldShadowFix()
        {
            if (heldShadowFixed) return;
            var fpc = FirstPersonController.instance;
            if (fpc == null) return;
            heldShadowFixed = true;
            var seen = new HashSet<int>();
            var roots = new List<GameObject>
            {
                fpc.currentObject, fpc.club, fpc.grabber, fpc.climber, fpc.blower
            };
            if (fpc.playerCamera != null) roots.Add(fpc.playerCamera.gameObject);
            foreach (var root in roots)
            {
                if (root == null) continue;
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null || !seen.Add(r.GetInstanceID())) continue;
                    heldRends.Add(r);
                    heldRecv.Add(r.receiveShadows);
                    heldCast.Add(r.shadowCastingMode);
                    heldProbe.Add(r.lightProbeUsage);
                    heldRefl.Add(r.reflectionProbeUsage);
                    r.receiveShadows = false;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    // no probes in Suliman: a probe-blended viewmodel otherwise reads as black shreds
                    r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                    r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                }
            }
            if (heldRends.Count > 0)
                Log($"[SULIMAN] held-item lighting: {heldRends.Count} renderers, shadows off");
            ConvertHeldPropeller();
        }

        private static void RestoreHeldShadowFix()
        {
            for (int i = 0; i < heldRends.Count; i++)
            {
                if (heldRends[i] == null) continue;
                heldRends[i].receiveShadows = heldRecv[i];
                heldRends[i].shadowCastingMode = heldCast[i];
                heldRends[i].lightProbeUsage = heldProbe[i];
                heldRends[i].reflectionProbeUsage = heldRefl[i];
            }
            heldRends.Clear();
            heldRecv.Clear();
            heldCast.Clear();
            heldProbe.Clear();
            heldRefl.Clear();
            heldShadowFixed = false;
        }

        private static void ConvertHeldPropeller()
        {
            var fpc = FirstPersonController.instance;
            if (fpc == null) return;
            var candidates = new List<GameObject>
            {
                fpc.propeller, fpc.currentObject, fpc.club, fpc.grabber, fpc.climber, fpc.blower
            };
            foreach (var c in candidates)
            {
                if (c == null) continue;
                var prop = c.GetComponentInChildren<Propeller>(true);
                if (prop == null) continue;
                ConvertPropellerObject(prop.gameObject, true);
                return;
            }
        }

        private static void ConvertPropellerObject(GameObject root, bool save)
        {
            int n = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var old = r.sharedMaterials;
                if (old == null || old.Length == 0) continue;
                if (save)
                {
                    propRends.Add(r);
                    propMats.Add(old);
                }
                var conv = new Material[old.Length];
                for (int i = 0; i < old.Length; i++)
                    conv[i] = ToStandardMaterial(old[i], root.name);
                r.sharedMaterials = conv;
                n++;
            }
            if (n > 0)
                Log($"[SULIMAN] propeller material converted: {n} renderers ({(save ? "held" : "spare")})");
        }

        private static Material ToStandardMaterial(Material src, string owner)
        {
            if (src == null) return null;
            if (src.renderQueue >= 3000) return src;   // keep the game's transparent rotor material
            var sh = Shader.Find("Standard");
            var m = new Material(sh) { name = src.name + " (Suliman)" };
            Texture tex = src.HasProperty("_BC") ? src.GetTexture("_BC") : src.mainTexture;
            if (tex != null) m.SetTexture("_MainTex", tex);
            m.SetColor("_Color", new Color(0.85f, 0.85f, 0.85f));
            m.SetFloat("_Metallic", 0.15f);
            m.SetFloat("_Glossiness", 0.30f);
            return m;
        }

        private static void RestorePropellerMaterials()
        {
            for (int i = 0; i < propRends.Count; i++)
                if (propRends[i] != null) propRends[i].sharedMaterials = propMats[i];
            propRends.Clear();
            propMats.Clear();
        }

        private static void RestoreEnv()
        {
            if (!envSaved) return;
            if (fillLight != null) fillLight.enabled = false;
            RestoreHeldShadowFix();
            RestorePropellerMaterials();
            RestoreLevelZones();
            RestoreCameraSkyboxes();
            RestoreSkyGeometry();
            RestoreCameraFog();
            RenderSettings.fog = oldFog;
            RenderSettings.fogMode = oldFogMode;
            RenderSettings.fogColor = oldFogColor;
            RenderSettings.fogDensity = oldFogDensity;
            RenderSettings.ambientMode = oldAmbientMode;
            RenderSettings.ambientLight = oldAmbient;
            RenderSettings.ambientIntensity = oldAmbientIntensity;
            RenderSettings.skybox = oldSkybox;
            QualitySettings.shadowDistance = oldShadowDistance;
            if (oldSun != null)
            {
                oldSun.intensity = oldSunIntensity;
                oldSun.color = oldSunColor;
                oldSun.shadows = oldSunShadows;
                oldSun.enabled = oldSunEnabled;
            }
            if (moon != null) moon.enabled = false;
            if (weatherGo != null) weatherGo.SetActive(false);
            if (snowPs != null) snowPs.Stop();
            envNightLogged = false;
            var cam = PlayerCam();
            if (cam != null && cam.farClipPlane > 4000f) cam.farClipPlane = 2500f;
            if (cam != null)
            {
                cam.clearFlags = oldCamClear;
                cam.backgroundColor = oldCamBg;
            }
            envSaved = false;
        }

        // ------------------------------------------------------------------ portal

        private static Vector3 MemorialEdge(out bool near)
        {
            near = false;
            if (Memorial == null) return Vector3.zero;
            var fpc = FirstPersonController.instance;
            if (fpc == null) return Vector3.zero;
            Vector3 p = fpc.transform.position;
            Vector3 lp = Memorial.InverseTransformPoint(p);
            // plinth is 13.4 x 9.4 m; allow 3 m around it on the roof slab
            float dx = Mathf.Max(0f, Mathf.Abs(lp.x) - 6.7f);
            float dz = Mathf.Max(0f, Mathf.Abs(lp.z) - 4.7f);
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            near = d < 3.0f && lp.y > -1.5f && lp.y < 4.0f;
            return Memorial.TransformPoint(new Vector3(0f, 0.36f, 6.9f));
        }

        public static void Enter()
        {
            if (!Built || cooldown > Time.time) return;
            Vector3 p = Origin + SpawnLocal;
            p.y = Origin.y + HeightAt(SpawnLocal.x, SpawnLocal.z) + 0.2f;
            Teleport(p, SpawnYaw);
            Inside = true;
            ApplyNight();
            Toast("СУЛИМАН · ночь, −38 °C");
            cooldown = Time.time + 2.2f;
            log.LogInfo($"[SULIMAN] entered at {p}; fog/shadow/sky switched, snowfall on");
        }

        public static void Leave()
        {
            if (cooldown > Time.time) return;
            if (Memorial == null) return;
            Vector3 p = Memorial.TransformPoint(new Vector3(0f, 0.42f, 6.9f));
            Teleport(p, 180f);
            Inside = false;
            RestoreEnv();
            Toast("крыша района · красный обелиск");
            cooldown = Time.time + 2.2f;
            log.LogInfo("[SULIMAN] returned to the district roof; original fog/sky/lights restored");
        }

        private static void Teleport(Vector3 p, float yaw)
        {
            var fpc = FirstPersonController.instance;
            if (fpc == null) return;
            var cc = fpc.characterController;
            if (cc != null) cc.enabled = false;
            fpc.transform.position = p;
            fpc.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (cc != null) cc.enabled = true;
            fpc.moveDirection = Vector3.zero;
            fpc.force = Vector3.zero;
            fpc.addingForce = false;
            fpc.rotationX = 0f;
        }

        // ------------------------------------------------------------------ tick

        public static void Tick()
        {            if (!Built) return;
            TickShots();
            TickPortalTest();
            float dt = Time.deltaTime;
            if (cooldown > Time.time) { }
            var fpc = FirstPersonController.instance;
            if (fpc == null) return;
            Vector3 p = fpc.transform.position;

            bool nearMem = false;
            if (!Inside) MemorialEdge(out nearMem);
            bool nearReturn = Inside && Vector3.Distance(p, ReturnAnchor) < 3.2f;

            if (Input.GetKeyDown(KeyCode.E) && cooldown <= Time.time)
            {
                if (!Inside && nearMem) Enter();
                else if (Inside && nearReturn) Leave();
            }

            // leaving the far region by other means (respawn, fall) must restore the level look
            if (Inside && Vector3.Distance(p, Origin) > 3200f && Time.time > cooldown)
            {
                Inside = false;
                RestoreEnv();
            }
            if (Inside)
            {
                // keep re-asserting the winter night: the level's TriggerZone keeps lerping
                // fog/sky/sun toward the city's daytime every frame, even from afar
                ApplyNight();
            }

            if (Inside && snowPs != null)
            {
                var cam = PlayerCam();
                Vector3 at = cam != null ? cam.transform.position : p;
                snowPs.transform.position = at + new Vector3(0f, 8f, 0f) + Vector3.forward * 8f;
                if (Time.time > snowCheckNext)
                {
                    snowCheckNext = Time.time + 0.4f;
                    RaycastHit roofHit;
                    bool covered = Physics.Raycast(at + Vector3.up * 0.2f, Vector3.up, out roofHit, 40f,
                                                   ~0, QueryTriggerInteraction.Ignore);
                    var em = snowPs.emission;
                    em.enabled = !covered;
                }
            }

            if (Inside) TickLightPool(p);
            if (Inside && Time.time > fpsNext)
            {
                fpsNext = Time.time + 6f;
                Log($"[SULIMAN] fps ~ {(1f / Mathf.Max(0.0001f, Time.smoothDeltaTime)):0} " +
                    $"({poolSpots.Count} light spots, {SulimanData.TreeCount} trees)");
            }
        }

        private static float fpsNext;
        private static float snowCheckNext;

        /// <summary>Re-applies the night after every other LateUpdate, so a level zone cannot win.</summary>
        public static void LateTick()
        {
            if (Built && Inside) ApplyNight();
        }

        private static void Toast(string s)
        {
            toast = s;
            toastUntil = Time.time + 4.5f;
        }

        public static void DrawPrompt()
        {
            if (!Built) return;
            string text = null;
            var fpc = FirstPersonController.instance;
            if (fpc != null)
            {
                if (!Inside)
                {
                    bool near; MemorialEdge(out near);
                    if (near) text = "<b>[E]</b>  спуститься в Сулиман";
                }
                else if (Vector3.Distance(fpc.transform.position, ReturnAnchor) < 3.2f)
                {
                    text = "<b>[E]</b>  вернуться на крышу";
                }
            }
            if (promptStyle == null)
            {
                promptStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 19,
                    richText = true,
                    alignment = TextAnchor.MiddleCenter
                };
                promptStyle.normal.textColor = new Color(1f, 0.96f, 0.88f);
            }
            if (text != null)
            {
                var r = new Rect(Screen.width * 0.5f - 230f, Screen.height - 118f, 460f, 34f);
                GUI.color = new Color(0f, 0f, 0f, 0.62f);
                GUI.Box(r, "");
                GUI.color = Color.white;
                GUI.Label(r, text, promptStyle);
            }
            if (!string.IsNullOrEmpty(toast) && Time.time < toastUntil)
            {
                float a = Mathf.Clamp01((toastUntil - Time.time) / 1.2f);
                var st = new GUIStyle(promptStyle) { fontSize = 30, fontStyle = FontStyle.Bold };
                st.normal.textColor = new Color(0.96f, 0.97f, 1f, a);
                GUI.Label(new Rect(0, Screen.height * 0.24f, Screen.width, 48f), toast, st);
            }
        }

        // ------------------------------------------------------------------ light pool

        internal static void AddLampSpot(Vector3 local, float range)
        {
            AddLampSpot(local, range, new Color(1f, 0.94f, 0.86f));
        }

        internal static void AddLampSpot(Vector3 local, float range, Color color)
        {
            poolSpots.Add(local);
            poolRanges.Add(range);
            poolColors.Add(color);
        }

        private static void EnsurePoolLights()
        {
            if (poolLights.Count > 0) return;
            for (int i = 0; i < PoolCount; i++)
            {
                var go = new GameObject("SulimanLampLight");
                go.transform.SetParent(Root, false);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.94f, 0.86f);
                l.intensity = 0f;
                l.range = 22f;
                l.shadows = LightShadows.None;
                l.renderMode = LightRenderMode.ForcePixel;
                l.enabled = false;
                poolLights.Add(l);
                poolUsed.Add(-1);
            }
        }

        private static void TickLightPool(Vector3 world)
        {
            if (poolSpots.Count == 0) return;
            EnsurePoolLights();
            if (Time.time < poolNext) return;
            poolNext = Time.time + 0.35f;
            Vector3 p = world - Origin;
            for (int i = 0; i < poolUsed.Count; i++) poolUsed[i] = -1;

            for (int li = 0; li < poolLights.Count; li++)
            {
                int best = -1;
                float bestD = float.MaxValue;
                for (int si = 0; si < poolSpots.Count; si++)
                {
                    if (poolUsed.Contains(si)) continue;
                    float d = (poolSpots[si] - p).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = si; }
                }
                if (best < 0) break;
                poolUsed[li] = best;
                float dist = Mathf.Sqrt(bestD);
                var l = poolLights[li];
                l.transform.localPosition = poolSpots[best];
                l.range = poolRanges[best];
                l.color = poolColors[best];
                l.intensity = Mathf.Lerp(0.85f, 0.22f, Mathf.Clamp01(dist / 90f));
                l.enabled = dist < 110f;
            }
        }

        // ------------------------------------------------------------------ logging / helpers

        internal static void Log(string s) { if (log != null) log.LogInfo(s); }
        internal static void Warn(string s) { if (log != null) log.LogWarning(s); }

        internal static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        internal static int RI(int a, int b) => rng.Next(a, b + 1);
    }

    /// <summary>Re-applies the Suliman night immediately before the camera renders.</summary>
    internal sealed class SulimanNightGuard : MonoBehaviour
    {
        private void OnPreRender()
        {
            Suliman.EnforceNight();
        }
    }
}
