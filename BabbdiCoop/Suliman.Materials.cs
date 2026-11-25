using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace BabbdiCoop
{
    // Procedural look of Suliman: snow, concrete, wood, asphalt, windows, lamps, night sky.
    internal static partial class Suliman
    {
        private static Cubemap nightSky;
        private static Texture2D panelTex, panelNrm, stuccoTex, stuccoNrm, woodTex, woodNrm,
            concreteTex, concreteNrm, snowTex, snowNrm, asphaltTex, asphaltNrm, iceTex, iceNrm,
            windowLit, windowCold, windowDark, windowBoard, windowShop, halo, pool, flake, smoke,
            lampTex, signTex, shaftTex;

        private static void MakeMaterials()
        {
            Shader std = Shader.Find("Standard");
            if (std == null) std = Shader.Find("Legacy Shaders/Diffuse");
            if (std == null) throw new Exception("no usable shader");
            Log($"[SULIMAN] shaders: standard={Shader.Find("Standard") != null} " +
                $"cube={(Shader.Find("Skybox/Cubemap") != null)} proc={(Shader.Find("Skybox/Procedural") != null)} " +
                $"add={(Shader.Find("Legacy Shaders/Particles/Additive") != null)} " +
                $"alpha={(Shader.Find("Legacy Shaders/Particles/Alpha Blended") != null)} " +
                $"unlit={(Shader.Find("Unlit/Texture") != null)}");

            BuildTextures();

            Func<string, Texture2D, Texture2D, Color, float, float, Material> M =
                (name, tex, nrm, tint, smooth, metal) =>
                {
                    var m = new Material(std) { name = name };
                    if (tex != null) m.SetTexture("_MainTex", tex);
                    m.SetColor("_Color", tint);
                    m.SetFloat("_Glossiness", smooth);
                    m.SetFloat("_Metallic", metal);
                    if (nrm != null)
                    {
                        m.SetTexture("_BumpMap", nrm);
                        m.EnableKeyword("_NORMALMAP");
                        m.SetFloat("_BumpScale", 0.85f);
                    }
                    return m;
                };

            Color[] panelTint =
            {
                new Color(0.70f, 0.69f, 0.64f),   // cream
                new Color(0.76f, 0.66f, 0.44f),   // pale yellow
                new Color(0.62f, 0.68f, 0.62f),   // white-green
                new Color(0.55f, 0.63f, 0.72f),   // pale blue
                new Color(0.69f, 0.58f, 0.57f),   // pale rose
                new Color(0.58f, 0.57f, 0.55f),   // grey
            };
            for (int i = 0; i < 6; i++)
                Facade[i] = M("SulimanPanel" + i, panelTex, panelNrm, panelTint[i], 0.10f, 0.02f);

            Color[] woodTint =
            {
                new Color(0.62f, 0.47f, 0.31f),
                new Color(0.52f, 0.49f, 0.44f),
                new Color(0.43f, 0.31f, 0.22f),
                new Color(0.59f, 0.35f, 0.25f),
            };
            for (int i = 0; i < 4; i++)
                Facade[6 + i] = M("SulimanWood" + i, woodTex, woodNrm, woodTint[i], 0.12f, 0.0f);

            Facade[10] = M("SulimanIndConcrete", concreteTex, concreteNrm, new Color(0.66f, 0.66f, 0.63f), 0.10f, 0.04f);
            Facade[11] = M("SulimanIndMetal", panelTex, panelNrm, new Color(0.47f, 0.50f, 0.52f), 0.32f, 0.35f);
            Facade[12] = M("SulimanIndRust", concreteTex, concreteNrm, new Color(0.57f, 0.39f, 0.28f), 0.18f, 0.25f);
            Facade[13] = M("SulimanChurch", stuccoTex, stuccoNrm, new Color(0.90f, 0.89f, 0.84f), 0.08f, 0.0f);

            Accent[0] = M("SulimanAccentBlue", panelTex, panelNrm, new Color(0.22f, 0.36f, 0.58f), 0.20f, 0.05f);
            Accent[1] = M("SulimanAccentRed", panelTex, panelNrm, new Color(0.56f, 0.20f, 0.17f), 0.20f, 0.05f);
            Accent[2] = M("SulimanAccentYellow", panelTex, panelNrm, new Color(0.72f, 0.58f, 0.20f), 0.20f, 0.05f);
            Accent[3] = M("SulimanAccentGreen", panelTex, panelNrm, new Color(0.22f, 0.38f, 0.26f), 0.20f, 0.05f);
            Accent[4] = M("SulimanAccentWhite", panelTex, panelNrm, new Color(0.88f, 0.88f, 0.85f), 0.20f, 0.05f);

            SnowMat = M("SulimanSnow", snowTex, snowNrm, new Color(0.72f, 0.74f, 0.78f), 0.18f, 0f);
            SnowMat.SetFloat("_BumpScale", 0.55f);
            SnowBankMat = M("SulimanSnowBank", snowTex, snowNrm, new Color(0.62f, 0.64f, 0.70f), 0.20f, 0f);
            ConcreteMat = M("SulimanConcrete", concreteTex, concreteNrm, new Color(0.48f, 0.48f, 0.46f), 0.10f, 0.02f);
            ConcreteDarkMat = M("SulimanConcreteDark", concreteTex, concreteNrm, new Color(0.25f, 0.26f, 0.26f), 0.12f, 0.02f);
            MetalMat = M("SulimanMetal", panelTex, panelNrm, new Color(0.32f, 0.35f, 0.37f), 0.38f, 0.55f);
            RustMat = M("SulimanRust", concreteTex, concreteNrm, new Color(0.46f, 0.27f, 0.17f), 0.20f, 0.30f);
            WoodMat = M("SulimanWoodDetail", woodTex, woodNrm, new Color(0.42f, 0.32f, 0.22f), 0.10f, 0f);
            WoodDarkMat = M("SulimanWoodDark", woodTex, woodNrm, new Color(0.24f, 0.19f, 0.15f), 0.10f, 0f);
            AsphaltMat = M("SulimanAsphalt", asphaltTex, asphaltNrm, Color.white, 0.12f, 0.02f);
            PackedMat = M("SulimanPackedSnow", snowTex, snowNrm, new Color(0.55f, 0.57f, 0.62f), 0.22f, 0f);
            IceMat = M("SulimanIce", iceTex, iceNrm, new Color(0.22f, 0.28f, 0.36f), 0.55f, 0.05f);
            RoofMat = M("SulimanRoof", asphaltTex, asphaltNrm, new Color(0.44f, 0.43f, 0.41f), 0.22f, 0.05f);
            RoofDarkMat = M("SulimanRoofDark", asphaltTex, asphaltNrm, new Color(0.25f, 0.25f, 0.26f), 0.25f, 0.10f);

            GlassDarkMat = M("SulimanGlassDark", null, null, new Color(0.03f, 0.05f, 0.08f), 0.7f, 0.25f);
            WindowLitMat = Emissive("SulimanWindowLit", windowLit, new Color(1f, 0.72f, 0.42f), 1.45f);
            WindowColdMat = Emissive("SulimanWindowCold", windowCold, new Color(0.70f, 0.86f, 1f), 1.15f);
            WindowDarkMat = M("SulimanWindowDark", windowDark, null, new Color(0.30f, 0.36f, 0.46f), 0.65f, 0.15f);
            WindowBoardMat = M("SulimanWindowBoard", windowBoard, null, new Color(0.48f, 0.45f, 0.40f), 0.10f, 0f);
            WindowShopMat = Emissive("SulimanWindowShop", windowShop, new Color(1f, 0.93f, 0.76f), 1.75f);

            LampHeadMat = Emissive("SulimanLampHead", lampTex, new Color(1f, 0.95f, 0.88f), 1.5f);
            SignBoardMat = M("SulimanSignBoard", panelTex, panelNrm, new Color(0.16f, 0.18f, 0.20f), 0.35f, 0.30f);

            var add = Shader.Find("Sprites/Default");
            if (add == null) add = Shader.Find("Legacy Shaders/Particles/Additive");
            HaloTexMat = new Material(add) { name = "SulimanHalo" };
            HaloTexMat.SetTexture("_MainTex", halo);
            if (HaloTexMat.HasProperty("_Color")) HaloTexMat.SetColor("_Color", new Color(1f, 0.94f, 0.86f, 0.55f));
            PoolMat = new Material(add) { name = "SulimanLightPool" };
            PoolMat.SetTexture("_MainTex", pool);
            if (PoolMat.HasProperty("_Color")) PoolMat.SetColor("_Color", new Color(1f, 0.66f, 0.34f, 0.45f));

            var alpha = Shader.Find("Sprites/Default");
            if (alpha == null) alpha = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            FlakeMat = new Material(alpha) { name = "SulimanFlake" };
            FlakeMat.SetTexture("_MainTex", flake);
            if (FlakeMat.HasProperty("_Color")) FlakeMat.SetColor("_Color", new Color(1f, 1f, 1f, 0.90f));
            SmokeMat = new Material(alpha) { name = "SulimanSmoke" };
            SmokeMat.SetTexture("_MainTex", smoke);
            if (SmokeMat.HasProperty("_Color")) SmokeMat.SetColor("_Color", new Color(0.55f, 0.58f, 0.62f, 0.40f));
            ShaftMat = new Material(alpha) { name = "SulimanShaft" };
            ShaftMat.SetTexture("_MainTex", shaftTex);
            if (ShaftMat.HasProperty("_Color")) ShaftMat.SetColor("_Color", new Color(1f, 0.72f, 0.40f, 0.45f));

            GlowRedMat = Lit("SulimanGlowRed", new Color(1f, 0.16f, 0.10f), 5.0f);
            GlowWarmMat = Lit("SulimanGlowWarm", new Color(1f, 0.70f, 0.34f), 1.6f);
            PortalMat = Lit("SulimanPortal", new Color(0.62f, 0.08f, 0.05f), 1.25f);
            PortalEdgeMat = Lit("SulimanPortalEdge", new Color(1f, 0.10f, 0.05f), 1.8f);
            StepMat = M("SulimanStadiumSteps", concreteTex, concreteNrm, new Color(0.72f, 0.72f, 0.68f), 0.12f, 0.02f);

            try
            {
                SignFont = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Segoe UI", "Tahoma" }, 64);
            }
            catch { SignFont = null; }
            ApplyCityMaterials();
        }

        // v0.10.80: the original BABBDI material set (S_EnviroSurface00) instead of the
        // procedural "cardboard" colours. The base materials are shared with the level, so
        // the town keeps the real concrete/wood/rust grain the user asked for.
        private static void ApplyCityMaterials()
        {
            var rough = CityMat("M_ConcreteRough00");
            var panels = CityMat("M_ConcretePanels00");
            var leaks = CityMat("M_ConcreteLeaks00");
            var slabs = CityMat("M_ConcreteSlabs01");
            var planks = CityMat("M_ConcretePlanks00");
            var painted = CityMat("M_PaintedWall00");
            var mossy = CityMat("M_MossyConcrete00");
            var wood = CityMat("M_Wood00");
            var metal = CityMat("M_MetalRusty00");
            var rust = CityMat("M_Rust00");
            var rustPanels = CityMat("M_RustPanels00");
            if (rough == null && panels == null && wood == null)
            {
                Log("[SULIMAN] original city materials not found; keeping procedural look");
                return;
            }
            Material F(Material a, Material b) => a != null ? a : (b != null ? b : rough);

            Facade[0] = F(panels, rough);
            Facade[1] = F(painted, rough);
            Facade[2] = F(wood, rough);
            Facade[3] = F(wood, rough);
            Facade[4] = F(planks, rough);
            Facade[5] = F(leaks, rough);
            Facade[6] = F(wood, rough);
            Facade[7] = F(wood, rough);
            Facade[8] = F(wood, rough);
            Facade[9] = F(wood, rough);
            Facade[10] = F(planks, rough);
            Facade[11] = F(metal, rough);
            Facade[12] = F(rust, rough);
            Facade[13] = F(mossy, rough);

            if (rough != null) ConcreteMat = rough;
            ConcreteDarkMat = leaks != null ? leaks : rough;
            if (metal != null) MetalMat = metal;
            if (rust != null) RustMat = rust;
            else if (rustPanels != null) RustMat = rustPanels;
            if (wood != null) { WoodMat = wood; WoodDarkMat = wood; }
            if (slabs != null) { RoofMat = slabs; RoofDarkMat = slabs; StepMat = slabs; }
            else if (planks != null) { RoofMat = planks; RoofDarkMat = planks; StepMat = planks; }

            Log($"[SULIMAN] city materials: rough={N2(rough)} panels={N2(panels)} leaks={N2(leaks)} " +
                $"slabs={N2(slabs)} wood={N2(wood)} metal={N2(metal)} rust={N2(rust)} " +
                $"painted={N2(painted)}");
        }

        private static string N2(Material m) => m != null ? m.name : "none";

        private static Material CityMat(string name)
        {
            var all = Resources.FindObjectsOfTypeAll<Material>();
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].name == name) return all[i];
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].name.StartsWith(name + " ", StringComparison.Ordinal)) return all[i];
            return null;
        }

        private static Material Unlit(string name, Texture tex, Color tint)
        {
            return Emissive(name, tex, tint, 1.1f);
        }

        private static Material Emissive(string name, Texture tex, Color tint, float power)
        {
            var sh = Shader.Find("Standard");
            var m = new Material(sh) { name = name };
            if (tex != null)
            {
                m.SetTexture("_MainTex", tex);
                m.SetTexture("_EmissionMap", tex);
            }
            m.SetColor("_Color", Color.white);
            m.SetColor("_EmissionColor", tint * power);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetFloat("_Glossiness", 0.05f);
            m.SetFloat("_Metallic", 0f);
            return m;
        }

        private static Material Lit(string name, Color emission, float power)
        {
            var sh = Shader.Find("Standard");
            var m = new Material(sh) { name = name };
            m.SetColor("_Color", new Color(0.08f, 0.08f, 0.08f));
            m.SetColor("_EmissionColor", emission * power);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return m;
        }

        // ------------------------------------------------------------------ textures

        private static Texture2D Tex(string name, int w, int h, Func<int, int, Color> f, bool normal = false)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8
            };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = f(x, y);
            t.SetPixels(px);
            t.Apply(true);
            if (normal)
            {
                t.wrapMode = TextureWrapMode.Repeat;
            }
            return t;
        }

        private static float Fbm(float x, float y, int oct = 4)
        {
            float s = 0f, a = 0.5f, f = 1f;
            for (int i = 0; i < oct; i++)
            {
                s += Mathf.PerlinNoise(x * f, y * f) * a;
                f *= 2f; a *= 0.5f;
            }
            return s;
        }

        private static float Hash(int x, int y, int s = 0)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + s * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0x7fffffff) / (float)0x7fffffff;
            }
        }

        private static Texture2D NormalMap(string name, int n, Func<int, int, float> height, float strength)
        {
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float hl = height((x - 1 + n) % n, y), hr = height((x + 1) % n, y);
                    float hd = height(x, (y - 1 + n) % n), hu = height(x, (y + 1) % n);
                    Vector3 v = new Vector3((hl - hr) * strength, (hd - hu) * strength, 1f).normalized;
                    px[y * n + x] = new Color(v.x * 0.5f + 0.5f, v.y * 0.5f + 0.5f, v.z * 0.5f + 0.5f, 1f);
                }
            }
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true)
            { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        private static void BuildTextures()
        {
            const int N = 512;
            Func<int, int, float> ph = (x, y) =>
                Fbm(x / (float)N * 7f, y / (float)N * 7f, 5) * 0.7f +
                Fbm(x / (float)N * 31f, y / (float)N * 31f, 3) * 0.3f;
            panelNrm = NormalMap("SulimanPanelNrm", N, ph, 2.2f);
            stuccoNrm = NormalMap("SulimanStuccoNrm", N, ph, 1.3f);

            panelTex = Tex("SulimanPanel", N, N, (x, y) =>
            {
                float h = ph(x, y);
                float v = 0.70f + h * 0.20f;
                // 4x4 panel seams
                int sx = x % 128, sy = y % 128;
                if (sx < 2 || sy < 2) v *= 0.72f;
                if (sx < 5 && sx >= 2) v *= 0.93f;
                // small chips
                if (Hash(x / 3, y / 3, 7) > 0.985f) v *= 0.72f;
                return new Color(v, v * 0.995f, v * 0.97f);
            });

            stuccoTex = Tex("SulimanStucco", N, N, (x, y) =>
            {
                float h = Fbm(x / (float)N * 9f, y / (float)N * 9f, 5);
                float v = 0.76f + h * 0.16f;
                // hairline cracks
                float c = Mathf.Abs(Mathf.Sin(x * 0.021f + Fbm(x * 0.01f, y * 0.01f) * 5f) * 22f - (y % 256));
                if (c < 0.8f && Hash(x, y, 3) > 0.5f) v *= 0.63f;
                return new Color(v, v * 0.99f, v * 0.95f);
            });

            int wN = 256;
            Func<int, int, float> wh = (x, y) => Fbm(x / 16f, y / 16f, 4);
            woodNrm = NormalMap("SulimanWoodNrm", wN, wh, 1.8f);
            woodTex = Tex("SulimanWood", wN, wN, (x, y) =>
            {
                float grain = Mathf.PerlinNoise(x * 0.09f, y * 0.9f);
                float v = 0.50f + grain * 0.34f;
                if (x % 18 == 0) v *= 0.55f;          // plank gap
                if (Hash(x / 2, y / 2, 5) > 0.982f) v *= 0.7f;
                return new Color(v, v * 0.82f, v * 0.64f);
            });

            concreteNrm = NormalMap("SulimanConcreteNrm", wN, (x, y) => Fbm(x / 9f, y / 9f, 5), 1.4f);
            concreteTex = Tex("SulimanConcrete", wN, wN, (x, y) =>
            {
                float h = Fbm(x / 13f, y / 13f, 5);
                float v = 0.58f + h * 0.22f;
                if (Hash(x, y, 11) > 0.997f) v *= 0.55f;
                return new Color(v, v * 0.99f, v * 0.96f);
            });

            Func<int, int, float> sh = (x, y) =>
                Fbm(x / 20f, y / 20f, 5) * 0.75f +
                Mathf.Sin(x * 0.11f + Mathf.PerlinNoise(x * 0.01f, y * 0.01f) * 8f) * 0.04f;
            snowNrm = NormalMap("SulimanSnowNrm", N, sh, 1.6f);
            snowTex = Tex("SulimanSnow", N, N, (x, y) =>
            {
                float h = Fbm(x / 24f, y / 24f, 5);
                float v = 0.84f + h * 0.14f;
                // wind ripples
                v += Mathf.Sin(x * 0.028f + y * 0.011f + Fbm(x / 40f, y / 40f, 3) * 12f) * 0.018f;
                // sparkles
                if (Hash(x, y, 17) > 0.9955f) v = 1f;
                return new Color(v * 0.965f, v * 0.985f, v);
            });

            asphaltNrm = NormalMap("SulimanAsphaltNrm", wN, (x, y) => Fbm(x / 5f, y / 5f, 4), 1.1f);
            asphaltTex = Tex("SulimanAsphalt", 256, 512, (x, y) =>
            {
                float u = x / 255f;
                float n = Fbm(x / 7f, y / 7f, 4);
                float v = 0.12f + n * 0.07f;
                // snow at the shoulders, ragged
                float edge = 0.11f + 0.05f * Mathf.PerlinNoise(y * 0.02f, 3.1f);
                if (u < edge || u > 1f - edge) v = 0.20f + n * 0.06f;
                // packed snow between the wheel tracks
                if (u > 0.43f && u < 0.57f) v = Mathf.Lerp(v, 0.24f + n * 0.06f, 0.55f);
                // cleared wheel tracks
                if ((u > 0.23f && u < 0.35f) || (u > 0.65f && u < 0.77f)) v = 0.10f + n * 0.05f;
                return new Color(v * 0.96f, v * 0.98f, v * 1.03f);
            });

            iceTex = Tex("SulimanIce", 512, 512, (x, y) =>
            {
                float n = Fbm(x / 18f, y / 18f, 5);
                float v = 0.78f + n * 0.16f;
                // cracks
                float k = Mathf.Abs(Mathf.Sin(x * 0.034f + Fbm(x / 60f, y / 60f) * 9f) * 10f - (y % 128));
                if (k < 0.7f) v = 0.55f;
                if (k < 0.3f) v = 0.38f;
                return new Color(v * 0.91f, v * 0.96f, v);
            });
            iceNrm = NormalMap("SulimanIceNrm", 256, (x, y) => Fbm(x / 11f, y / 11f, 4), 1.2f);

            // ---- window atlas: 4 x 4 cells, 512 x 512 ----
            const int cell = 128;
            Func<int, int, Color>[] cells = new Func<int, int, Color>[16];
            cells[0] = Warm(0.93f, 0.55f, 0.25f, 0.55f);
            cells[1] = Warm(1f, 0.72f, 0.38f, 0.30f);
            cells[2] = Warm(1f, 0.62f, 0.30f, 0.85f);
            cells[3] = Cold(0.72f, 0.88f, 1f);
            cells[4] = Dark();
            cells[5] = Boarded();
            cells[6] = Bright();
            cells[7] = Basement();
            windowLit = WindowAtlas("SulimanWindowsLit", cells);
            windowCold = WindowAtlas("SulimanWindowsCold", cells);
            windowDark = windowLit;
            windowBoard = windowLit;
            windowShop = windowLit;

            halo = RadialTex("SulimanHalo", 128, 0.10f, 1.8f, new Color(1f, 0.93f, 0.85f));
            pool = RadialTex("SulimanPool", 128, 0.18f, 1.5f, new Color(1f, 0.66f, 0.32f));
            flake = RadialTex("SulimanFlake", 64, 0.32f, 0.85f, Color.white);
            smoke = SmokeTex("SulimanSmoke", 128);
            lampTex = Tex("SulimanLampHead", 64, 64, (x, y) =>
            {
                float u = x / 63f, v = y / 63f;
                float d = Mathf.Max(Mathf.Abs(u - 0.5f), Mathf.Abs(v - 0.5f));
                float k = d < 0.48f ? 1f : 0f;
                return new Color(1f * k, 0.94f * k, 0.86f * k);
            });
            shaftTex = Tex("SulimanShaft", 16, 128, (x, y) =>
            {
                float v = 1f - y / 127f;                // 1 at the lamp, 0 at the ground
                float a = Mathf.Pow(v, 1.6f) * 0.62f;
                float side = 0.65f + 0.35f * Mathf.Sin(x / 15f * Mathf.PI);
                return new Color(1f, 0.93f, 0.85f, a * side);
            });
            signTex = Tex("SulimanSign", 64, 64, (x, y) => new Color(0.10f, 0.12f, 0.14f));
        }

        private static Func<int, int, Color> Warm(float r, float g, float b, float curtains)
        {
            return (x, y) =>
            {
                float u = x / 127f, v = y / 127f;
                // frame
                if (u < 0.075f || u > 0.925f || v < 0.06f || v > 0.94f)
                    return new Color(0.66f, 0.63f, 0.57f);
                if (u < 0.11f || u > 0.89f || v < 0.095f || v > 0.905f)
                    return new Color(0.20f, 0.19f, 0.18f);
                float k = 0.55f + 0.45f * v;
                var c = new Color(r * k, g * k, b * k);
                // curtain folds at both sides
                float side = Mathf.Min(u - 0.11f, 0.89f - u) / 0.78f;
                if (side < curtains * 0.42f && Hash(x / 4, y / 5, 3) > 0.25f)
                {
                    float f = 0.62f + 0.25f * Mathf.Abs(Mathf.Sin((u * 60f) + v * 4f));
                    c *= new Color(f * 0.95f, f * 0.80f, f * 0.66f);
                }
                // mullion
                if (Mathf.Abs(u - 0.5f) < 0.012f) c *= 0.55f;
                return c;
            };
        }

        private static Func<int, int, Color> Cold(float r, float g, float b)
        {
            return (x, y) =>
            {
                float u = x / 127f, v = y / 127f;
                if (u < 0.075f || u > 0.925f || v < 0.06f || v > 0.94f)
                    return new Color(0.60f, 0.62f, 0.63f);
                if (u < 0.11f || u > 0.89f || v < 0.095f || v > 0.905f)
                    return new Color(0.12f, 0.14f, 0.16f);
                float k = 0.6f + 0.4f * v;
                var c = new Color(r * k, g * k, b * k);
                if (x % 24 < 2) c *= 0.82f;  // blinds
                return c;
            };
        }

        private static Func<int, int, Color> Dark()
        {
            return (x, y) =>
            {
                float u = x / 127f, v = y / 127f;
                if (u < 0.075f || u > 0.925f || v < 0.06f || v > 0.94f)
                    return new Color(0.30f, 0.32f, 0.34f);
                if (u < 0.11f || u > 0.89f || v < 0.095f || v > 0.905f)
                    return new Color(0.07f, 0.08f, 0.10f);
                float refl = Mathf.Clamp01(0.55f - Mathf.Abs((u - v) * 1.4f)) * 0.12f;
                return new Color(0.03f + refl * 0.6f, 0.05f + refl * 0.7f, 0.075f + refl);
            };
        }

        private static Func<int, int, Color> Boarded()
        {
            var dark = Dark();
            return (x, y) =>
            {
                var c = dark(x, y);
                int band = (y + x / 3) % 46;
                if (band < 9 && x > 12 && x < 116 && y > 10 && y < 118)
                    return new Color(0.38f, 0.31f, 0.23f) * (0.8f + 0.2f * Mathf.PerlinNoise(x * 0.2f, y * 0.4f));
                return c;
            };
        }

        private static Func<int, int, Color> Bright()
        {
            return (x, y) =>
            {
                float u = x / 127f, v = y / 127f;
                if (u < 0.06f || u > 0.94f || v < 0.05f || v > 0.95f)
                    return new Color(0.55f, 0.54f, 0.50f);
                return new Color(1f, 0.96f, 0.86f) * (0.85f + 0.15f * v);
            };
        }

        private static Func<int, int, Color> Basement()
        {
            return (x, y) =>
            {
                float u = x / 127f, v = y / 127f;
                if (u < 0.09f || u > 0.91f || v < 0.10f || v > 0.90f)
                    return new Color(0.22f, 0.22f, 0.22f);
                return new Color(0.50f, 0.56f, 0.52f) * (0.7f + 0.3f * v);
            };
        }

        private static Texture2D WindowAtlas(string name, Func<int, int, Color>[] cells)
        {
            const int cell = 128;
            var t = new Texture2D(512, 512, TextureFormat.RGBA32, true)
            { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            var px = new Color[512 * 512];
            for (int cy = 0; cy < 4; cy++)
                for (int cx = 0; cx < 4; cx++)
                {
                    int ci = cx + cy * 4;
                    var f = cells[ci];
                    if (f == null) f = cells[4];
                    for (int y = 0; y < cell; y++)
                        for (int x = 0; x < cell; x++)
                            px[(cy * cell + y) * 512 + (cx * cell + x)] = f(x, y);
                }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        private static Texture2D RadialTex(string name, int n, float core, float fall, Color tint)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false)
            { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f;
                    float v = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Pow(Mathf.Clamp01((1f - d) / Mathf.Max(0.01f, 1f - core)), fall);
                    px[y * n + x] = new Color(tint.r, tint.g, tint.b, a);
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        private static Texture2D SmokeTex(string name, int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false)
            { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f;
                    float v = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(u * u + v * v);
                    float f = Fbm(x / 22f, y / 22f, 4);
                    float a = Mathf.Clamp01(1f - d) * Mathf.Clamp01(0.35f + f);
                    px[y * n + x] = new Color(1f, 1f, 1f, a * 0.8f);
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        // ------------------------------------------------------------------ sky

        internal static Material NightSky()
        {
            if (nightSkyMat != null) return nightSkyMat;
            if (nightSky == null) nightSky = BuildSky(256);
            var sh = Shader.Find("Skybox/Cubemap");
            if (sh != null && nightSky != null)
            {
                nightSkyMat = new Material(sh) { name = "SulimanNightSky" };
                nightSkyMat.SetTexture("_Tex", nightSky);
                return nightSkyMat;
            }
            // this build has no Skybox/Cubemap shader: a dark procedural sky still reads as night
            var p = Shader.Find("Skybox/Procedural");
            if (p != null)
            {
                nightSkyMat = new Material(p) { name = "SulimanNightProcedural" };
                nightSkyMat.SetColor("_SkyTint", new Color(0.032f, 0.052f, 0.100f));
                nightSkyMat.SetColor("_GroundColor", new Color(0.009f, 0.013f, 0.018f));
                nightSkyMat.SetFloat("_AtmosphereThickness", 0.35f);
                nightSkyMat.SetFloat("_Exposure", 0.11f);
                nightSkyMat.SetFloat("_SunSize", 0.02f);
                return nightSkyMat;
            }
            return oldSkybox;
        }

        private static Material nightSkyMat;

        private static Cubemap BuildSky(int n)
        {
            var cube = new Cubemap(n, TextureFormat.RGBA32, false)
            { name = "SulimanSky", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };

            // stars: direction list, splatted into the faces
            var rnd = new System.Random(7717);
            int stars = 1600;
            var dirs = new Vector3[stars];
            for (int i = 0; i < stars; i++)
            {
                float z = (float)rnd.NextDouble() * 2f - 1f;
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
                var d = new Vector3(r * Mathf.Cos(a), z, r * Mathf.Sin(a));
                if (d.y < 0.02f) d.y = -d.y + 0.02f;
                dirs[i] = d.normalized;
            }

            var moonDir = new Vector3(-0.38f, 0.42f, 0.82f).normalized;
            for (int face = 0; face < 6; face++)
            {
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                {
                    float vv = (y + 0.5f) / n * 2f - 1f;
                    for (int x = 0; x < n; x++)
                    {
                        float uu = (x + 0.5f) / n * 2f - 1f;
                        Vector3 d;
                        switch (face)
                        {
                            case 0: d = new Vector3(1f, -vv, -uu); break;
                            case 1: d = new Vector3(-1f, -vv, uu); break;
                            case 2: d = new Vector3(uu, 1f, vv); break;
                            case 3: d = new Vector3(uu, -1f, -vv); break;
                            case 4: d = new Vector3(uu, -vv, 1f); break;
                            default: d = new Vector3(-uu, -vv, -1f); break;
                        }
                        d.Normalize();
                        float h = Mathf.Clamp01(d.y);
                        Color top = new Color(0.006f, 0.009f, 0.017f);
                        Color hor = new Color(0.028f, 0.038f, 0.055f);
                        Color c = Color.Lerp(hor, top, Mathf.Pow(h, 0.55f));
                        if (d.y < 0f) c = Color.Lerp(hor, new Color(0.03f, 0.04f, 0.055f), Mathf.Clamp01(-d.y * 3f));

                        // faint aurora above the northern horizon
                        if (d.z > 0.15f && d.y > 0f && d.y < 0.55f)
                        {
                            float band = Mathf.Sin(d.x * 9f + d.y * 24f + Mathf.PerlinNoise(d.x * 3f, d.y * 5f) * 6f);
                            float a = Mathf.Clamp01(0.55f - d.y) * Mathf.Clamp01((d.z - 0.15f) * 2.5f);
                            c += new Color(0.03f, 0.10f, 0.07f) * Mathf.Clamp01(band * 0.5f + 0.5f) * a;
                        }

                        // moon disc + halo
                        float md = Vector3.Dot(d, moonDir);
                        if (md > 0.985f) c = Color.Lerp(new Color(0.98f, 0.97f, 0.92f), c, Mathf.Clamp01((0.999f - md) * 60f));
                        else if (md > 0.90f)
                        {
                            float g = Mathf.Pow((md - 0.90f) / 0.10f, 2.2f);
                            c += new Color(0.22f, 0.26f, 0.34f) * g;
                        }
                        px[y * n + x] = c;
                    }
                }
                // splat stars
                for (int i = 0; i < stars; i++)
                {
                    Vector3 d = dirs[i];
                    int f; float su, sv;
                    ProjectFace(d, out f, out su, out sv);
                    if (f != face) continue;
                    int sx = Mathf.Clamp((int)((d.x * 0f) + (su + 1f) * 0.5f * n), 0, n - 1);
                    int sy = Mathf.Clamp((int)((sv + 1f) * 0.5f * n), 0, n - 1);
                    float br = 0.45f + 0.55f * (float)rnd.NextDouble();
                    var sc = new Color(0.85f * br, 0.90f * br, 1f * br);
                    px[sy * n + sx] = sc;
                    if (br > 0.8f && sx + 1 < n) px[sy * n + sx + 1] = sc * 0.55f;
                }
                cube.SetPixels(px, (CubemapFace)face);
            }
            cube.Apply(false, false);
            return cube;
        }

        private static void ProjectFace(Vector3 d, out int face, out float u, out float v)
        {
            float ax = Mathf.Abs(d.x), ay = Mathf.Abs(d.y), az = Mathf.Abs(d.z);
            if (ax >= ay && ax >= az)
            {
                face = d.x > 0 ? 0 : 1;
                u = d.x > 0 ? -d.z / ax : d.z / ax;
                v = -d.y / ax;
            }
            else if (ay >= az)
            {
                face = d.y > 0 ? 2 : 3;
                u = d.x / ay;
                v = d.y > 0 ? d.z / ay : -d.z / ay;
            }
            else
            {
                face = d.z > 0 ? 4 : 5;
                u = d.z > 0 ? d.x / az : -d.x / az;
                v = -d.y / az;
            }
        }

        // ------------------------------------------------------------------ weather

        private static void BuildWeather()
        {
            weatherGo = new GameObject("SulimanSnow");
            weatherGo.transform.SetParent(Root, false);
            weatherGo.transform.position = Origin + new Vector3(SulimanData.W * 0.5f, 40f, SulimanData.D * 0.5f);

            snowPs = weatherGo.AddComponent<ParticleSystem>();
            var main = snowPs.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = 12f;
            main.startSpeed = 1.8f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.20f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.82f, 0.88f, 1f, 0.95f),
                                                               new Color(1f, 1f, 1f, 1f));
            main.maxParticles = 9000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.012f;

            var em = snowPs.emission;
            em.enabled = true;
            em.rateOverTime = 520f;

            var shape = snowPs.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(70f, 34f, 70f);

            var vel = snowPs.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.55f, 1.1f);
            vel.y = new ParticleSystem.MinMaxCurve(-1.2f, -0.6f);
            vel.z = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);

            var noise = snowPs.noise;
            noise.enabled = true;
            noise.strength = 0.65f;
            noise.frequency = 0.23f;
            noise.scrollSpeed = 0.12f;
            noise.damping = true;
            noise.octaveCount = 2;

            var col = snowPs.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.12f),
                        new GradientAlphaKey(0.9f, 0.85f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var rend = weatherGo.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.material = FlakeMat;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;

            weatherGo.SetActive(false);
        }
    }
}
