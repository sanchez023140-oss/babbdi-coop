using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.Rendering;

namespace BabbdiCoop
{
    /// <summary>
    /// v0.10.15 — the physical score display on the air-hockey table. Each table gets **two**
    /// trapezoid boards, one on each end of the lamp housing (the arch canopy), facing the player
    /// standing at that end and tilted 10° down towards them. The board repeats the lamp end's
    /// cross-section (0,85 m top / 0,608 m bottom, 0,148 m tall), continues its material (the
    /// canopy's own colour — not the emitted light's accent), and shows the side score on a matte
    /// black screen in the acid-green marker digits the user picked.
    ///
    /// The boards live under the arch, so they hide with it during the mini-game (the canopy would
    /// cover the rink from the raised camera), but the meshes keep updating: the moment the arch is
    /// shown again the board carries the current score. Scores are stored per table id in this
    /// component, so leaving the mini-game keeps them on the table, and re-entering continues them
    /// unless the previous match was already over.
    /// </summary>
    internal class AirHockeyScoreboard : MonoBehaviour
    {
        // ---- lamp end geometry (metres, table-local; matches airhockey_arch.glb) -------------
        public const float EndX = 1.656f;      // canopy end plane, +x and -x
        public const float TopY = 2.834f;      // canopy top edge
        public const float TopHalf = 0.425f;   // top edge half-width in z
        public const float BotHalf = 0.304f;   // bottom edge half-width in z
        public const float LampH = 0.148f;     // canopy thickness
        const float PlateT = 0.06f;            // board thickness, outward along +x
        const float Inset = 0.012f;            // screen border
        const float TiltDeg = -10f;            // face tips down towards the player
        const float GlyphH = 0.108f;           // digit cap height on the screen
        const float GlyphGap = 0.011f;

        // ---- idle mallets (v0.10.16): the side markers on the unused rink ---------------------
        // Blue stands to the **right of the blue goal** from the blue player's view; red is the
        // mirror image. Right of a player facing -x is +z, so blue = (+x, +z), red = (-x, -z).
        const float IdleMalletX = 1.78f;
        const float IdleMalletZ = 0.60f;
        public static readonly Color RedMalletColor = new Color(0.62f, 0.12f, 0.08f);
        public static readonly Color BlueMalletColor = new Color(0.12f, 0.32f, 0.62f);
        static Material idleRedMat, idleBlueMat;

        // ---- digit atlas: model/score_glyphs.png (11 cells of 260 x 256, glyphs 200 px tall) --
        const int CellW = 260;
        const int CellH = 256;
        const int GlyphPx = 200;
        const int AtlasW = CellW * 11;
        const float V0 = 0.109375f;            // tight v range of the 200 px glyphs
        const float V1 = 0.890625f;
        // glyph widths at 200 px height: 0..9 then the hyphen
        static readonly float[] GlyphRatio =
        {
            0.975f, 1.215f, 0.965f, 0.720f, 0.895f, 0.935f,
            0.750f, 0.870f, 0.785f, 0.805f, 0.380f
        };

        static readonly Dictionary<int, AirHockeyScoreboard> Registry =
            new Dictionary<int, AirHockeyScoreboard>();
        static Texture2D glyphTexture;
        static Material screenMaterial;
        static Material digitMaterial;

        int tableId;
        int red, blue;
        bool matchOver;
        int target = 11;
        bool built;

        Mesh digitsPlus;
        Mesh digitsMinus;
        GameObject idleMallets;

        // ------------------------------------------------------------------ public API
        public static void ClearAll()
        {
            Registry.Clear();
        }

        /// <summary>Builds the two boards for one table. `texDir` is the plugin's BabbdiCoop
        /// folder (score_glyphs.png lives next to airhockey.glb). Call after the arch has been
        /// themed/split so its canopy material is final.</summary>
        public static void Attach(int id, Transform tableRoot, GameObject arch, string texDir,
                                  ManualLogSource log)
        {
            if (tableRoot == null || arch == null || Registry.ContainsKey(id)) return;
            var mf = arch.GetComponentInChildren<MeshFilter>();
            var mr = arch.GetComponentInChildren<MeshRenderer>();
            if (mf == null || mr == null || mf.sharedMesh == null) return;
            if (mr.sharedMaterials == null || mr.sharedMaterials.Length == 0) return;

            var mesh = mf.sharedMesh;
            int canopySub = mr.sharedMaterials.Length >= 3 ? 1 : 0;
            int matIdx = Mathf.Clamp(canopySub, 0, mr.sharedMaterials.Length - 1);
            Material housing = mr.sharedMaterials[matIdx];
            if (housing == null) return;

            Vector2 canopyUv = CanopyUv(mesh, canopySub);
            // v0.10.15: the board's body must read as the lamp's **painted housing**, not as the
            // colour the lamp emits. A fresh material of the canopy's own albedo (the texel for
            // the main table's textured arch, the flat colour for the variants) with a mild
            // self-emission keeps that colour under the table's coloured lamp light.
            Color housingColor = CanopyColor(housing, canopyUv);
            Material boardHousing = MakeHousingMaterial(housingColor);

            var comp = tableRoot.GetComponent<AirHockeyScoreboard>();
            if (comp == null) comp = tableRoot.gameObject.AddComponent<AirHockeyScoreboard>();
            comp.tableId = id;
            // The board belongs to the lamp, so it hides with it during the mini-game: from the
            // raised camera a board left floating at the lamp end covers the centre of the rink
            // (verified in mini_test.png). Its meshes keep updating while hidden, so stepping back
            // from the table always shows the current score.
            comp.Init(arch.transform, boardHousing, canopyUv, texDir, log);
            comp.BuildIdleMallets(tableRoot);
            Registry[id] = comp;
            log?.LogInfo($"[SCORE] table {id}: idle side mallets — red at " +
                         $"(-{IdleMalletX:0.00}, -{IdleMalletZ:0.00}), blue at " +
                         $"(+{IdleMalletX:0.00}, +{IdleMalletZ:0.00}) on the rink, right of each goal");
        }

        public static void SetScore(int id, int red, int blue, bool over, int target)
        {
            if (!Registry.TryGetValue(id, out var sb) || sb == null) return;
            if (sb.red == red && sb.blue == blue && sb.matchOver == over && sb.target == target)
                return;
            sb.red = Mathf.Max(0, red);
            sb.blue = Mathf.Max(0, blue);
            sb.matchOver = over;
            sb.target = target >= 100 ? 100 : 11;
            sb.WriteDigits(sb.digitsPlus);
            sb.WriteDigits(sb.digitsMinus);
        }

        public static void SetScore(int id, int red, int blue, bool over)
        {
            SetScore(id, red, blue, over, 11);
        }

        /// <summary>v0.10.16: hide the two idle side-markers while this peer is playing at the
        /// table (the mini-game's own mallets are shown instead).</summary>
        public static void SetInUse(int id, bool inUse)
        {
            if (!Registry.TryGetValue(id, out var sb) || sb == null || sb.idleMallets == null) return;
            sb.idleMallets.SetActive(!inUse);
        }

        public static bool GetScore(int id, out int red, out int blue, out bool over, out int target)
        {
            red = 0; blue = 0; over = false; target = 11;
            if (!Registry.TryGetValue(id, out var sb) || sb == null) return false;
            red = sb.red; blue = sb.blue; over = sb.matchOver; target = sb.target;
            return true;
        }

        // ------------------------------------------------------------------ build
        void Init(Transform arch, Material housing, Vector2 canopyUv, string texDir,
                  ManualLogSource log)
        {
            if (built) return;
            built = true;
            var tex = LoadGlyphs(texDir, log);
            EnsureMaterials(tex);
            BuildBoard(arch, true, housing, canopyUv, out digitsPlus);
            BuildBoard(arch, false, housing, canopyUv, out digitsMinus);
            WriteDigits(digitsPlus);
            WriteDigits(digitsMinus);
            log?.LogInfo($"[SCORE] table {tableId}: two trapezoid boards on the lamp ends " +
                         $"(+/-{EndX:0.000} m), {TopHalf * 2f:0.00} x {LampH:0.000} m, tilted " +
                         $"{TiltDeg:0.0} deg down; housing = the lamp body colour " +
                         $"#{ColorUtility.ToHtmlStringRGB(housing != null ? housing.color : Color.gray)} " +
                         $"(material '{(housing != null ? housing.name : "?")}'), screen matte black, " +
                         $"digits acid green from the acid-marker set; score {red}-{blue}");
        }

        /// <summary>
        /// v0.10.16: while nobody is at the table, one mallet of each side stands on the rink as a
        /// side marker — blue to the right of the blue goal, red mirrored to the right of the red
        /// goal (right from the point of view of the player standing at that end).
        /// </summary>
        void BuildIdleMallets(Transform tableRoot)
        {
            if (idleMallets != null) return;
            idleMallets = new GameObject("idle_mallets");
            idleMallets.transform.SetParent(tableRoot, false);
            idleMallets.transform.localPosition = Vector3.zero;
            idleMallets.transform.localRotation = Quaternion.identity;
            if (idleRedMat == null) idleRedMat = MakeMat(RedMalletColor, 0.35f);
            if (idleBlueMat == null) idleBlueMat = MakeMat(BlueMalletColor, 0.35f);
            float y = SovietDistrict.AirHockeyRinkTop + 0.02f;
            MakeMallet("idle_mallet_red", idleRedMat,
                       new Vector3(-IdleMalletX, y, -IdleMalletZ));
            MakeMallet("idle_mallet_blue", idleBlueMat,
                       new Vector3(IdleMalletX, y, IdleMalletZ));
        }

        void MakeMallet(string name, Material mat, Vector3 pos)
        {
            var root = new GameObject(name);
            root.transform.SetParent(idleMallets.transform, false);
            root.transform.localPosition = pos;
            root.transform.localRotation = Quaternion.identity;

            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            UnityEngine.Object.Destroy(disc.GetComponent<Collider>());
            disc.name = name + "_disc";
            disc.transform.SetParent(root.transform, false);
            disc.transform.localScale = new Vector3(0.352f, 0.02475f, 0.352f);
            disc.transform.localPosition = new Vector3(0f, 0.02475f, 0f);
            disc.GetComponent<Renderer>().sharedMaterial = mat;

            var stick = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            UnityEngine.Object.Destroy(stick.GetComponent<Collider>());
            stick.name = name + "_stick";
            stick.transform.SetParent(root.transform, false);
            stick.transform.localScale = new Vector3(0.08f, 0.09f, 0.08f);
            stick.transform.localPosition = new Vector3(0f, 0.09f, 0f);
            stick.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static Material MakeMat(Color c, float gloss)
        {
            var sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Legacy Shaders/Diffuse");
            var m = new Material(sh) { name = "CoopIdleMallet" };
            m.color = c;
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);
            return m;
        }

        static Vector2 CanopyUv(Mesh mesh, int sub)
        {
            var verts = mesh.vertices;
            var uvs = mesh.uv;
            if (uvs == null || uvs.Length != verts.Length) return new Vector2(0.5f, 0.5f);
            var tris = mesh.GetTriangles(Mathf.Clamp(sub, 0, mesh.subMeshCount - 1));
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                if (sub == 0)
                {
                    // the main table's arch is one mesh: skip rods and the underside
                    float minX = Mathf.Min(verts[a].x, Mathf.Min(verts[b].x, verts[c].x));
                    float maxX = Mathf.Max(verts[a].x, Mathf.Max(verts[b].x, verts[c].x));
                    bool rod = (maxX - minX) < 0.15f && Mathf.Abs((minX + maxX) * 0.5f) < 0.15f;
                    if (rod) continue;
                    Vector3 n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]).normalized;
                    float cy = (verts[a].y + verts[b].y + verts[c].y) / 3f;
                    if (cy > 2.40f && n.y < -0.25f) continue;   // the lamp's underside
                }
                return (uvs[a] + uvs[b] + uvs[c]) / 3f;
            }
            return new Vector2(0.5f, 0.5f);
        }

        static Color CanopyColor(Material m, Vector2 uv)
        {
            if (m == null) return new Color(0.34f, 0.32f, 0.29f);
            Color c = m.HasProperty("_Color") ? m.color : Color.white;
            var tex = m.mainTexture as Texture2D;
            if (tex != null && tex.isReadable)
            {
                var t = tex.GetPixelBilinear(Mathf.Repeat(uv.x, 1f), Mathf.Repeat(uv.y, 1f));
                if (t.a > 0.01f) c = new Color(t.r, t.g, t.b);
            }
            return c;
        }

        static Material MakeHousingMaterial(Color c)
        {
            var sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            var m = new Material(sh) { name = "CoopScoreHousing" };
            m.color = c;
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.12f);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * 0.35f);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return m;
        }

        void BuildBoard(Transform arch, bool plus, Material housing, Vector2 canopyUv,
                        out Mesh digitMesh)
        {
            var root = new GameObject(plus ? "scoreboard_blue" : "scoreboard_red");
            root.transform.SetParent(arch, false);
            root.transform.localPosition = new Vector3(plus ? EndX : -EndX, TopY, 0f);
            // tilt in the board's own frame, then turn the -x board round to face its player
            root.transform.localRotation =
                Quaternion.Euler(0f, plus ? 0f : 180f, 0f) * Quaternion.Euler(0f, 0f, TiltDeg);

            // housing: the lamp end's trapezoid continued 6 cm outwards
            var hgo = new GameObject("housing");
            hgo.transform.SetParent(root.transform, false);
            var hf = hgo.AddComponent<MeshFilter>();
            hf.sharedMesh = BuildHousingMesh(canopyUv);
            var hr = hgo.AddComponent<MeshRenderer>();
            hr.sharedMaterial = housing;
            hr.shadowCastingMode = ShadowCastingMode.On;

            // the matte black face
            var sgo = new GameObject("screen");
            sgo.transform.SetParent(root.transform, false);
            var sf = sgo.AddComponent<MeshFilter>();
            sf.sharedMesh = BuildScreenMesh();
            var sr = sgo.AddComponent<MeshRenderer>();
            sr.sharedMaterial = screenMaterial;
            sr.shadowCastingMode = ShadowCastingMode.Off;
            sr.receiveShadows = false;

            // the acid-green score
            var ggo = new GameObject("digits");
            ggo.transform.SetParent(root.transform, false);
            var gf = ggo.AddComponent<MeshFilter>();
            digitMesh = new Mesh { name = plus ? "score_digits_blue" : "score_digits_red" };
            gf.sharedMesh = digitMesh;
            var gr = ggo.AddComponent<MeshRenderer>();
            gr.sharedMaterial = digitMaterial;
            gr.shadowCastingMode = ShadowCastingMode.Off;
            gr.receiveShadows = false;
        }

        Mesh BuildHousingMesh(Vector2 uv)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var t = new List<Vector2>();
            var tri = new List<int>();

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 want)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), want) < 0f)
                {
                    var tmp = b; b = d; d = tmp;
                }
                int i0 = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                for (int k = 0; k < 4; k++) { n.Add(want); t.Add(uv); }
                tri.Add(i0); tri.Add(i0 + 1); tri.Add(i0 + 2);
                tri.Add(i0); tri.Add(i0 + 2); tri.Add(i0 + 3);
            }

            const float T = PlateT, H = LampH, A = TopHalf, B = BotHalf;
            // outer / inner faces
            Quad(new Vector3(T, 0f, -A), new Vector3(T, 0f, A),
                 new Vector3(T, -H, B), new Vector3(T, -H, -B), Vector3.right);
            Quad(new Vector3(0f, 0f, -A), new Vector3(0f, 0f, A),
                 new Vector3(0f, -H, B), new Vector3(0f, -H, -B), Vector3.left);
            // top / bottom
            Quad(new Vector3(0f, 0f, -A), new Vector3(T, 0f, -A),
                 new Vector3(T, 0f, A), new Vector3(0f, 0f, A), Vector3.up);
            Quad(new Vector3(0f, -H, -B), new Vector3(T, -H, -B),
                 new Vector3(T, -H, B), new Vector3(0f, -H, B), Vector3.down);
            // the two slanted sides
            Quad(new Vector3(0f, 0f, -A), new Vector3(T, 0f, -A),
                 new Vector3(T, -H, -B), new Vector3(0f, -H, -B), Vector3.back);
            Quad(new Vector3(0f, 0f, A), new Vector3(T, 0f, A),
                 new Vector3(T, -H, B), new Vector3(0f, -H, B), Vector3.forward);

            var m = new Mesh { name = "scoreboard_housing" };
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, t);
            m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            return m;
        }

        Mesh BuildScreenMesh()
        {
            const float T = PlateT, H = LampH;
            float yt = -Inset, yb = -H + Inset;
            float zt = TopHalf - Inset, zb = BotHalf - Inset;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var tri = new List<int>();

            void Quad(float x, Vector3 normal)
            {
                int i0 = v.Count;
                v.Add(new Vector3(x, yt, -zt));
                v.Add(new Vector3(x, yt, zt));
                v.Add(new Vector3(x, yb, zb));
                v.Add(new Vector3(x, yb, -zb));
                // the face winding: a horizontal top edge, then the narrower bottom edge
                Vector3 cr = Vector3.Cross(v[i0 + 1] - v[i0], v[i0 + 2] - v[i0]);
                if (Vector3.Dot(cr, normal) < 0f)
                {
                    var tmp = v[i0 + 1]; v[i0 + 1] = v[i0 + 3]; v[i0 + 3] = tmp;
                    tmp = v[i0]; v[i0] = v[i0 + 2]; v[i0 + 2] = tmp;
                }
                for (int k = 0; k < 4; k++) n.Add(normal);
                tri.Add(i0); tri.Add(i0 + 1); tri.Add(i0 + 2);
                tri.Add(i0); tri.Add(i0 + 2); tri.Add(i0 + 3);
            }

            Quad(T + 0.0006f, Vector3.right);
            Quad(T + 0.0003f, Vector3.left);

            var m = new Mesh { name = "scoreboard_screen" };
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            return m;
        }

        void WriteDigits(Mesh mesh)
        {
            if (mesh == null) return;
            mesh.Clear();
            if (glyphTexture == null) return;

            string s = $"{red}-{blue}";
            float total = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                total += GlyphRatio[CharIndex(s[i])] * GlyphH;
                if (i < s.Length - 1) total += GlyphGap;
            }

            float yc = -LampH * 0.5f;
            float y0 = yc - GlyphH * 0.5f;
            float y1 = yc + GlyphH * 0.5f;
            float z = -total * 0.5f;
            const float X = PlateT + 0.0012f;

            var v = new List<Vector3>(s.Length * 4);
            var uv = new List<Vector2>(s.Length * 4);
            var tri = new List<int>(s.Length * 6);

            for (int i = 0; i < s.Length; i++)
            {
                int idx = CharIndex(s[i]);
                float w = GlyphRatio[idx] * GlyphH;
                float z0 = z, z1 = z + w;

                float wpx = GlyphRatio[idx] * GlyphPx;
                float px0 = idx * CellW + (CellW - wpx) * 0.5f;
                float u0 = px0 / AtlasW;
                float u1 = (px0 + wpx) / AtlasW;

                int i0 = v.Count;
                v.Add(new Vector3(X, y0, z0));
                v.Add(new Vector3(X, y1, z0));
                v.Add(new Vector3(X, y1, z1));
                v.Add(new Vector3(X, y0, z1));
                uv.Add(new Vector2(u0, V0));
                uv.Add(new Vector2(u0, V1));
                uv.Add(new Vector2(u1, V1));
                uv.Add(new Vector2(u1, V0));
                tri.Add(i0); tri.Add(i0 + 1); tri.Add(i0 + 2);
                tri.Add(i0); tri.Add(i0 + 2); tri.Add(i0 + 3);

                z += w + GlyphGap;
            }

            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
        }

        static int CharIndex(char c)
        {
            if (c == '-') return 10;
            int d = c - '0';
            return d < 0 || d > 9 ? 10 : d;
        }

        // ------------------------------------------------------------------ assets
        void EnsureMaterials(Texture2D tex)
        {
            if (screenMaterial == null)
            {
                // genuinely matte: unlit, so the lamp's green light cannot reflect off the face
                var sh = Shader.Find("Unlit/Color");
                bool unlit = sh != null;
                if (sh == null) sh = Shader.Find("Standard");
                screenMaterial = new Material(sh) { name = "CoopScoreScreen" };
                screenMaterial.color = new Color(0.008f, 0.009f, 0.008f, 1f);
                if (!unlit)
                {
                    if (screenMaterial.HasProperty("_Metallic")) screenMaterial.SetFloat("_Metallic", 0f);
                    if (screenMaterial.HasProperty("_Glossiness")) screenMaterial.SetFloat("_Glossiness", 0f);
                }
            }
            if (digitMaterial == null && tex != null)
            {
                var sh = Shader.Find("Unlit/Transparent");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                if (sh == null) sh = Shader.Find("Unlit/Texture");
                digitMaterial = new Material(sh) { name = "CoopScoreDigits", mainTexture = tex };
                digitMaterial.renderQueue = 3000;
            }
        }

        static Texture2D LoadGlyphs(string dir, ManualLogSource log)
        {
            if (glyphTexture != null) return glyphTexture;
            string path = Path.Combine(dir ?? string.Empty, "score_glyphs.png");
            if (!File.Exists(path))
            {
                log?.LogWarning("[SCORE] score_glyphs.png is missing — the boards will be blank (" +
                                path + ")");
                return null;
            }
            try
            {
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (!t.LoadImage(File.ReadAllBytes(path)))
                {
                    log?.LogWarning("[SCORE] score_glyphs.png could not be decoded");
                    return null;
                }
                t.name = "CoopScoreGlyphs";
                t.wrapMode = TextureWrapMode.Clamp;
                t.filterMode = FilterMode.Trilinear;
                t.anisoLevel = 2;
                glyphTexture = t;
                log?.LogInfo($"[SCORE] digit atlas loaded: score_glyphs.png {t.width}x{t.height}, " +
                             $"11 cells (0-9 and the hyphen), acid green");
                return t;
            }
            catch (Exception e)
            {
                log?.LogWarning("[SCORE] score_glyphs.png failed: " + e.Message);
                return null;
            }
        }
    }
}
