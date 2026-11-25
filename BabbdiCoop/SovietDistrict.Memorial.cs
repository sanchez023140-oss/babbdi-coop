using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace BabbdiCoop
{
    // Part of the SovietDistrict builder - see SovietDistrict.cs.
    internal static partial class SovietDistrict
    {

        // ------------------------------------------------------------------ the memorial (v0.9.14)

        /// <summary>
        /// Picks the block the memorial goes on: the **second-tallest** of the district (the user's
        /// reference), with the roofs logged so the choice can be checked without a render.
        /// </summary>
        private static Bldg PickMemorialBlock()
        {
            var l = new List<Bldg>(bldgs);
            l.Sort((x, y) => y.Top.CompareTo(x.Top));
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < l.Count && i < 4; i++)
                sb.Append($"#{i + 1} {l[i].w:0.0}x{l[i].d:0.0} {l[i].floors}fl {l[i].Top:0.0}m").Append(i < 3 ? ", " : "");
            log.LogInfo($"[MEMORIAL] tallest blocks: {sb}");
            // #1 is the one it must NOT be on; a roof too narrow for the plinth (15 x 10 m) is skipped
            for (int i = 1; i < l.Count; i++)
                if (l[i].w >= 17f && l[i].d >= 15f) return l[i];
            return l.Count > 1 ? l[1] : null;
        }

        /// <summary>
        /// v0.9.14 вЂ” the memorial the user asked for: on the roof of the **second-tallest block**, a
        /// cluster of dark concrete piers whose vertical gaps glow red from the inside, on a stepped
        /// plinth, with fallen blocks scattered around (their reference photo).
        ///
        /// The red is a **box inside the ring of piers** вЂ” only its outer faces are visible through
        /// the gaps, and it carries a vertical gradient (white-hot at the base в†’ deep red в†’ nearly
        /// black at the top) through explicit UVs, so it does not depend on any shader's emission.
        /// Four red **point lights sit in the gaps**, never inside the core: a light inside the ring
        /// would light the piers' *outer* faces too (no shadows here) and wash the whole monument red,
        /// while a light inside a 0.7 m slot only reaches the inner faces, because the outer faces'
        /// normals point away from it. That is what puts the red gradient on the concrete.
        /// </summary>
        private static void Memorial(Bldg b)
        {
            var rot = Quaternion.Euler(0f, b.yaw, 0f);
            float y0 = b.Top + 0.26f;                     // the roof plate's top face вЂ” see Roof()
            var go = new GameObject("memorial");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(b.p.x, y0, b.p.z);
            go.transform.localRotation = rot;
            Suliman.SetMemorial(go.transform);   // v0.10.49: the red memorial is the portal to Suliman

            var g = new Geo();
            const float f = 0.62f;                        // the plinth's top
            // the reference stands on a two-riser platform
            g.Box(0, new Vector3(0f, 0.16f, 0f), new Vector3(13.4f, 0.32f, 9.4f), Quaternion.identity, 0.4f);
            g.Box(0, new Vector3(0f, 0.47f, 0f), new Vector3(12f, 0.30f, 8.2f), Quaternion.identity, 0.4f);

            // the glowing core: 0.15 m behind the piers' inner faces, **below the shortest pier** so no
            // red plate is ever seen above the ring (v0.9.14 fix вЂ” it poked out in the first build)
            var core = new Vector3(8.3f, 9f, 5.55f);
            GlowBox(g, new Vector3(0f, f, 0f), core);

            // piers вЂ” three slabs on each long side (1.0 m gaps), two on each short side. They are only
            // **0.55 m deep**: a 1.0 m deep channel hides the core completely from 60В° (the visible width
            // is `gap в€’ depthВ·tan(60В°)` = 1,0 в€’ 1,73 < 0, which is exactly what the first preview showed
            // вЂ” a solid grey mass with one red slit). At 0.55 m the same sum keeps 0.45 m of red at 45В°
            // and 0.2 m at 60В°, i.e. a bright vertical line, which is what the reference reads as.
            float[] cxf = { -3.2f, 0f, 3.2f };
            float[] hf = { 10.4f, 9.4f, 11f };
            float[] hb = { 10f, 10.8f, 9.6f };
            for (int i = 0; i < 3; i++) Pier(g, cxf[i], -3.2f, 2.2f, 0.55f, hf[i], f);
            for (int i = 0; i < 3; i++) Pier(g, cxf[i], 3.2f, 2.2f, 0.55f, hb[i], f);
            float[] czs = { -1.7f, 1.7f };
            float[] hl = { 9.6f, 10.2f };
            float[] hr = { 10f, 9.4f };
            for (int i = 0; i < 2; i++) Pier(g, -4.65f, czs[i], 0.7f, 2f, hl[i], f);
            for (int i = 0; i < 2; i++) Pier(g, 4.65f, czs[i], 0.7f, 2f, hr[i], f);

            // fallen blocks scattered on the roof around the plinth (the cubes in the reference's snow)
            float[] dx = { -8.4f, -7.4f, 7.6f, 9.0f, -1.4f, 5.2f };
            float[] dz = { -1.2f, 4.6f, -3.4f, 2.4f, 6.8f, 7.1f };
            float[] dw = { 1.6f, 1.0f, 2.1f, 1.2f, 1.8f, 1.0f };
            float[] dh = { 0.9f, 0.6f, 1.1f, 0.5f, 0.8f, 0.55f };
            for (int i = 0; i < dx.Length; i++)
                g.Box(0, new Vector3(dx[i], dh[i] * 0.5f, dz[i]),
                      new Vector3(dw[i], dh[i], dw[i] * 0.8f), Quaternion.Euler(0f, i * 37f, 0f), 0.4f);

            var mesh = g.Apply(go, mRough, GlowMaterial());
            AddMeshCollider(go, "memorial");

            // the lights sit **in the channels**, just in front of the core: they light the piers' inner
            // faces вЂ” which is what keeps the slits glowing at an angle where the core itself is hidden.
            // v0.9.15: every intensity is scaled by [District] MemorialGlow (0.5 = half of v0.9.14).
            float gl = Mathf.Max(0f, MemorialGlow);
            float gy = f + 2.4f;
            GlowLight(go.transform, new Vector3(-1.6f, gy, -3.05f), 2.4f * gl, 7f);      // front gaps, low
            GlowLight(go.transform, new Vector3(1.6f, gy, -3.05f), 2.4f * gl, 7f);
            GlowLight(go.transform, new Vector3(-1.6f, f + 6.6f, -3.05f), 1.3f * gl, 6f); // front gaps, high
            GlowLight(go.transform, new Vector3(1.6f, f + 6.6f, -3.05f), 1.3f * gl, 6f);
            GlowLight(go.transform, new Vector3(-1.6f, gy, 3.05f), 1.8f * gl, 6f);       // rear gap
            GlowLight(go.transform, new Vector3(-4.65f, gy, 0f), 1.8f * gl, 6f);         // west flank

            log.LogInfo($"[MEMORIAL] on the 2nd-tallest block ({b.w:0.0}x{b.d:0.0} m, {b.floors} floors, " +
                        $"top {b.Top:0.0} m at {b.p.x:0.0},{b.p.z:0.0}): 10 piers (wide ones split by a " +
                        $"{Slit:0.00} m slit), red core {core.x:0.0}x{core.z:0.0}x{core.y:0.0} m, " +
                        $"6 red lights at {gl:0.00}x ({2.4f * gl:0.00} / {1.3f * gl:0.00} / {1.8f * gl:0.00}), " +
                        $"{mesh.vertexCount} verts, roof y={y0:0.0}");
        }

        /// <summary>Width of the thin glowing slit that splits every wide pier (v0.9.14).</summary>
        private const float Slit = 0.24f;

        /// <summary>
        /// One pier. The wide ones (2,2 m) are **split down the middle by a 0,24 m slit**: a 1 m gap
        /// between 1 m deep monoliths foreshortens to nothing at any angle but head-on (measured in the
        /// first preview), while a thin slit backed by the core is still a bright red line from 45В° and
        /// from above вЂ” which is what the reference reads as. The two halves stay one monolith by their
        /// stepped crown height.
        /// </summary>
        private static void Pier(Geo g, float x, float z, float w, float d, float h, float f)
        {
            if (w < 1.8f) { Monolith(g, x, z, w, d, h, f); return; }
            float half = (w - Slit) * 0.5f, off = (Slit + half) * 0.5f;
            Monolith(g, x - off, z, half, d, h, f);
            Monolith(g, x + off, z, half, d, h, f);
        }

        /// <summary>A slab plus its stepped crown (the reference's monoliths are cut, not plain boxes).</summary>
        private static void Monolith(Geo g, float x, float z, float w, float d, float h, float f)
        {
            g.Box(0, new Vector3(x, f + h * 0.5f, z), new Vector3(w, h, d), Quaternion.identity, 0.4f);
            g.Box(0, new Vector3(x, f + h + 0.19f, z), new Vector3(w * 0.72f, 0.38f, d * 0.72f), Quaternion.identity, 0.4f);
        }

        /// <summary>
        /// A box whose **vertical faces** sample the red gradient by world height (v = 0 at the bottom,
        /// 1 at the top), so the glow fades exactly like the reference. Submesh 1 = the glow material.
        /// </summary>
        private static void GlowBox(Geo g, Vector3 c, Vector3 s)
        {
            float x0 = c.x - s.x * 0.5f, x1 = c.x + s.x * 0.5f;
            float z0 = c.z - s.z * 0.5f, z1 = c.z + s.z * 0.5f;
            float y0 = c.y, y1 = c.y + s.y;
            var v0 = new Vector2(0f, 0f); var v1 = new Vector2(1f, 0f);
            var v2 = new Vector2(1f, 1f); var v3 = new Vector2(0f, 1f);
            // +x, -x, +z, -z (the top face is a dark cap, all four corners at v = 1)
            g.Quad(1, new Vector3(x1, y0, z1), new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1),
                   Vector3.right, v0, v1, v2, v3);
            g.Quad(1, new Vector3(x0, y0, z0), new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x0, y1, z0),
                   Vector3.left, v0, v1, v2, v3);
            g.Quad(1, new Vector3(x1, y0, z1), new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1),
                   Vector3.forward, v0, v1, v2, v3);
            g.Quad(1, new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x0, y1, z0),
                   Vector3.back, v0, v1, v2, v3);
            var t = new Vector2(0.5f, 0.72f);   // the top cap: dim red, never a black hole
            g.Quad(1, new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0),
                   Vector3.up, t, t, t, t);
        }

        /// <summary>
        /// The red gradient itself: a 4 x 128 strip, white-hot just above the plinth, deep red at a
        /// third, nearly black at the top (the reference's slots are brightest at the floor).
        /// </summary>
        private static Texture2D GlowTexture()
        {
            const int h = 128;
            var t = new Texture2D(4, h, TextureFormat.RGBA32, false)
            {
                name = "CoopRedGradient",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[4 * h];
            for (int y = 0; y < h; y++)
            {
                float u = y / (h - 1f);   // 0 = the bottom of the slot
                Color c = u < 0.10f
                    ? Color.Lerp(new Color(1f, 0.58f, 0.38f), new Color(1f, 0.10f, 0.05f), u / 0.10f)
                    : u < 0.62f
                        ? Color.Lerp(new Color(1f, 0.10f, 0.05f), new Color(0.72f, 0.030f, 0.020f), (u - 0.10f) / 0.52f)
                        : Color.Lerp(new Color(0.72f, 0.030f, 0.020f), new Color(0.07f, 0.006f, 0.005f), (u - 0.62f) / 0.38f);
                var c32 = (Color32)c;
                for (int x = 0; x < 4; x++) px[y * 4 + x] = c32;
            }
            t.SetPixels32(px);
            t.Apply(true, false);
            return t;
        }

        /// <summary>
        /// The glow material вЂ” **unlit if the build has an unlit texture shader**, so the slots are
        /// bright red in the level's darkness without depending on emission support. The chain is
        /// logged with the shaders that actually exist in this build, so a missing one is visible.
        /// </summary>
        private static Material GlowMaterial()
        {
            if (mGlow != null) return mGlow;
            var tex = GlowTexture();
            // what the build actually carries (once, for the log)
            var sb = new System.Text.StringBuilder();
            var all = Resources.FindObjectsOfTypeAll<Shader>();
            for (int i = 0; i < all.Length && i < 60; i++) sb.Append(all[i].name).Append(", ");
            log.LogInfo($"[GLOW] {all.Length} shaders in the build: {sb}");

            // the game's own **emissive** shaders first: the level's signs and monitors use them and they
            // are HDR (`_EmissiveBoost`), so BABBDI's bloom (Hidden/PostProcessing/Bloom is in the build)
            // makes them actually glow. Their texture slot is called `_TextureSample0` (ASE export) and
            // `_texcoord` is the UV transform вЂ” both set explicitly, the values are logged as evidence.
            foreach (var n in new[] { "S_Emissive00", "S_EmissiveColor" })
            {
                var sh = Shader.Find(n);
                if (sh == null || !sh.isSupported) continue;
                if (sh.GetPropertyCount() <= 0) continue;
                var m = new Material(sh) { name = "CoopGlowRed (" + n + ")" };
                if (!m.HasProperty("_TextureSample0")) continue;
                m.SetTexture("_TextureSample0", tex);
                // v0.9.15: the boost is 1,8 at [District] MemorialGlow = 1 (the v0.9.14 look)
                if (m.HasProperty("_EmissiveBoost")) m.SetFloat("_EmissiveBoost", 1.8f * Mathf.Max(0f, MemorialGlow));
                if (m.HasProperty("_Color0")) m.SetColor("_Color0", Color.white);
                if (m.HasProperty("_texcoord")) m.SetVector("_texcoord", new Vector4(1f, 1f, 0f, 0f));
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
                if (m.HasProperty("_Float0")) m.SetFloat("_Float0", 1f);
                log.LogInfo($"[GLOW] core material: emissive {n} tex={tex.name}, " +
                            $"boost={(m.HasProperty("_EmissiveBoost") ? m.GetFloat("_EmissiveBoost") : -1f):0.00}, " +
                            $"texcoord={(m.HasProperty("_texcoord") ? m.GetVector("_texcoord").ToString("F2") : "-")}");
                mGlow = m;
                return m;
            }

            // unlit fallbacks (guaranteed bright, no emission needed)
            foreach (var n in new[] { "Unlit/Texture", "Sprites/Default", "Particles/Standard Unlit", "Unlit/Color" })
            {
                var sh = Shader.Find(n);
                if (sh == null) continue;
                var m = new Material(sh) { name = "CoopGlowRed (" + n + ")" };
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
                if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                log.LogInfo($"[GLOW] core material: unlit {n}");
                mGlow = m;
                return m;
            }
            // no unlit shader either: Standard with an emission map, the gradient as albedo too
            var std = Shader.Find("Standard");
            if (std != null)
            {
                var m = new Material(std) { name = "CoopGlowRed (Standard+emission)" };
                m.SetTexture("_MainTex", tex);
                m.SetColor("_Color", Color.white * 0.35f);
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", tex);
                m.SetColor("_EmissionColor", new Color(1f, 0.13f, 0.07f) * 2.5f);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                log.LogInfo("[GLOW] core material: Standard + emission map");
                mGlow = m;
                return m;
            }
            // last resort: the district's concrete with the gradient as albedo + the red lights
            var f = new Material(mRough) { name = "CoopGlowRed (concrete)" };
            f.SetTexture("_BC", tex);
            log.LogInfo("[GLOW] core material: WARNING no unlit shader and no Standard — concrete + gradient");
            mGlow = f;
            return f;
        }

        /// <summary>v0.9.72 вЂ” the House's warm lamps and the red star: a 4 x 32 strip from pale yellow to
        /// deep amber, on the same emissive/unlit chain as the memorial's red gradient.</summary>
        private static Texture2D WarmGlowTexture()
        {
            const int h = 32;
            var t = new Texture2D(4, h, TextureFormat.RGBA32, false)
            { name = "CoopWarmGradient", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[4 * h];
            for (int y = 0; y < h; y++)
            {
                float u = y / (h - 1f);
                Color c = u < 0.55f
                    ? Color.Lerp(new Color(1f, 0.97f, 0.82f), new Color(1f, 0.72f, 0.34f), u / 0.55f)
                    : Color.Lerp(new Color(1f, 0.72f, 0.34f), new Color(0.95f, 0.38f, 0.10f), (u - 0.55f) / 0.45f);
                var c32 = (Color32)c;
                for (int x = 0; x < 4; x++) px[y * 4 + x] = c32;
            }
            t.SetPixels32(px);
            t.Apply(true, false);
            return t;
        }

        private static Material WarmGlowMaterial()
        {
            if (mWarmGlow != null) return mWarmGlow;
            var tex = WarmGlowTexture();
            foreach (var n in new[] { "S_Emissive00", "S_EmissiveColor" })
            {
                var sh = Shader.Find(n);
                if (sh == null || !sh.isSupported || sh.GetPropertyCount() <= 0) continue;
                var m = new Material(sh) { name = "CoopWarmLamp (" + n + ")" };
                if (!m.HasProperty("_TextureSample0")) continue;
                m.SetTexture("_TextureSample0", tex);
                if (m.HasProperty("_EmissiveBoost")) m.SetFloat("_EmissiveBoost", 1.35f);
                if (m.HasProperty("_Color0")) m.SetColor("_Color0", Color.white);
                if (m.HasProperty("_texcoord")) m.SetVector("_texcoord", new Vector4(1f, 1f, 0f, 0f));
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
                if (m.HasProperty("_Float0")) m.SetFloat("_Float0", 1f);
                log.LogInfo($"[GLOW] House warm lamps: emissive {n}, tex=CoopWarmGradient, boost=1,35");
                mWarmGlow = m;
                return m;
            }
            foreach (var n in new[] { "Unlit/Texture", "Sprites/Default", "Particles/Standard Unlit" })
            {
                var sh = Shader.Find(n);
                if (sh == null) continue;
                var m = new Material(sh) { name = "CoopWarmLamp (" + n + ")" };
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
                if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                log.LogInfo($"[GLOW] House warm lamps: unlit {n}");
                mWarmGlow = m;
                return m;
            }
            mWarmGlow = GlowMaterial();
            return mWarmGlow;
        }

        private static void GlowLight(Transform parent, Vector3 local, float intensity, float range)
        {
            var go = new GameObject("memorial_glow");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.095f, 0.055f);
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForcePixel;
        }
    }
}
