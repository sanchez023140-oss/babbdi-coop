using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.Rendering;

namespace BabbdiCoop
{
    /// <summary>
    /// Unity-side loader for a **static** (unskinned) GLB — used by the five figures on the House of
    /// Soviets' roof (v0.9.24). The avatar path (`GlbAvatarBuilder`) is built for skinned models with a
    /// skeleton and clips; these figures are one rigid mesh, so they need none of that: the primitives
    /// are merged into a single `Mesh` with one submesh per material, which is one draw call per
    /// material and no per-frame cost at all.
    ///
    /// The figure GLBs are baked offline (`figs.py` in the harness) out of the game's own character
    /// meshes — `SM_A_Enki`'s body, warped and re-textured, with a different one of the game's own
    /// heads per figure — so the models already stand in metres with their feet at y = 0 and their
    /// face towards +Z (`yaw` then turns them like every other object).
    /// </summary>
    internal static class GlbFigureBuilder
    {
        /// <summary>
        /// Builds the figure as a GameObject under `parent`, with its feet on `feet` and its face
        /// turned by `yaw` (the glTF face is +Z). `shader` is the game's own surface shader (the one
        /// every NPC body uses); anything without `_BC` falls back to `Standard` + `_MainTex`.
        /// </summary>
        public static GameObject Load(string glbPath, Vector3 localFeet, float yaw, Transform parent,
                                      Shader shader, ManualLogSource log, string name)
        {
            return Load(glbPath, localFeet, yaw, parent, shader, log, name, "FIGURE", false, false);
        }

        /// <summary>
        /// Same loader with a caller-supplied log tag and collider choice. A helicopter is a
        /// static prop whose bounding box is mostly empty air between the rotor blades, so it
        /// gets a MeshCollider (the district's own convention) instead of a BoxCollider.
        /// `useNormals` keeps authored per-vertex normals (smooth fuselage + hard panel edges)
        /// instead of RecalculateNormals, which would round every hard edge.
        /// </summary>
        public static GameObject Load(string glbPath, Vector3 localFeet, float yaw, Transform parent,
                                      Shader shader, ManualLogSource log, string name, string tag,
                                      bool meshCollider, bool useNormals = false)
        {
            var parsed = GlbModelLoader.Parse(glbPath);
            if (parsed.Primitives.Count == 0) throw new InvalidDataException("figure GLB has no primitives");

            // ---- textures (decoded once per image index) ----------------------------
            var texByIdx = new Dictionary<int, Texture2D>();
            Texture2D Tex(int idx)
            {
                if (idx < 0 || idx >= parsed.Images.Count) return null;
                if (texByIdx.TryGetValue(idx, out var t)) return t;
                var png = parsed.Images[idx].Png;
                if (png == null) return null;
                var nt = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                nt.name = "glb_tex_" + idx;
                if (!nt.LoadImage(png))
                {
                    log?.LogWarning($"[{tag}] texture {idx} failed to decode in {name}");
                    return null;
                }
                // the figure is a single atlas, not a tiling material: with Repeat a UV that
                // lands exactly on u = 1.0 (the legs rect ends at the atlas' right edge)
                // samples the left edge instead - the skin block - and a pale stripe runs
                // down the seam of each trouser leg
                nt.wrapMode = TextureWrapMode.Clamp;
                nt.filterMode = FilterMode.Trilinear;
                nt.anisoLevel = 2;
                nt.Apply(true, false);
                texByIdx[idx] = nt;
                return nt;
            }

            // ---- one vertex buffer, one submesh per primitive ----------------------
            var verts = new List<Vector3>(8192);
            var uvs = new List<Vector2>(8192);
            var nors = new List<Vector3>(8192);
            var subs = new List<int[]>(4);
            var mats = new List<Material>(4);
            bool normalsComplete = useNormals;
            for (int i = 0; i < parsed.Primitives.Count; i++)
            {
                var prim = parsed.Primitives[i];
                if (prim.Positions == null || prim.Uvs == null || prim.Indices == null) continue;
                if (prim.Uvs.Length != prim.Positions.Length) continue;
                bool hasNormals = prim.Normals != null && prim.Normals.Length == prim.Positions.Length;
                if (!hasNormals) normalsComplete = false;
                int baseIdx = verts.Count;
                for (int v = 0; v < prim.Positions.Length; v++)
                {
                    var p = prim.Positions[v];
                    verts.Add(new Vector3(p.X, p.Y, p.Z));
                    // glTF v is top-down, the game's textures are stored Unity-style
                    uvs.Add(new Vector2(prim.Uvs[v].X, 1f - prim.Uvs[v].Y));
                    if (hasNormals)
                    {
                        var nrm = prim.Normals[v];
                        nors.Add(new Vector3(nrm.X, nrm.Y, nrm.Z));
                    }
                    else nors.Add(Vector3.zero);
                }
                var tri = new int[prim.Indices.Length];
                for (int t = 0; t < tri.Length; t++) tri[t] = prim.Indices[t] + baseIdx;
                subs.Add(tri);

                // the offline builder writes [skin, cloth] (v0.9.25). The old three-primitive
                // figures put an alpha-cutout hair card third, so keep that rule for i >= 2.
                // v0.9.78: a primitive whose GLB material carries alpha < 1 is GLASS (the pilots'
                // lowered helmet visor) and becomes a real transparent material, not a cutout.
                int matIdx = prim.MaterialIndex;
                float alpha = 1f;
                bool twoSided = false;
                Color tint = Color.white;
                if (matIdx >= 0 && matIdx < parsed.Materials.Count)
                {
                    var bc4 = parsed.Materials[matIdx].baseColor;
                    alpha = bc4.W;
                    twoSided = parsed.Materials[matIdx].doubleSided;
                    tint = new Color(bc4.X, bc4.Y, bc4.Z, bc4.W);
                }
                if (alpha < 0.99f)
                    mats.Add(MakeGlass(name + "_" + i, Tex(MaterialTexture(parsed, prim)), tint, log));
                else
                {
                    var mm = MakeMaterial(name + "_" + i, shader, Tex(MaterialTexture(parsed, prim)),
                                          tag == "FIGURE" && i >= 2, tint, log);
                    // v0.9.85: the Mi-26's hull is a hollow shell — its inside must be drawn,
                    // otherwise the window and porthole openings show the sky through the model
                    if (twoSided && mm.HasProperty("_Cull")) mm.SetInt("_Cull", 0);
                    mats.Add(mm);
                }
            }
            if (verts.Count == 0) throw new InvalidDataException("figure GLB has no geometry");

            var mesh = new Mesh { name = name + "_mesh" };
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = subs.Count;
            for (int i = 0; i < subs.Count; i++) mesh.SetTriangles(subs[i], i, true);
            if (normalsComplete) mesh.SetNormals(nors);
            else mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            // `localFeet` is in the parent's (district) space, like every other builder here
            go.transform.localPosition = localFeet;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats.ToArray();
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = false;
            // static geometry: no motion vectors needed, but declaring it saves a warning under TAA
            mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

            // a simple collider, so the player cannot walk through the prop
            if (meshCollider)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
            }
            else
            {
                var col = go.AddComponent<BoxCollider>();
                var b = mesh.bounds;
                col.center = b.center;
                col.size = new Vector3(Mathf.Max(b.size.x, 0.45f), b.size.y, Mathf.Max(b.size.z, 0.35f));
            }
            log?.LogInfo($"[{tag}] {name}: {verts.Count} verts, {subs.Count} materials, " +
                         $"size {mesh.bounds.size.x:0.00} x {mesh.bounds.size.y:0.00} x {mesh.bounds.size.z:0.00} m, " +
                         $"at local ({localFeet.x:0.0}, {localFeet.y:0.0}, {localFeet.z:0.0})");
            return go;
        }

        private static int MaterialTexture(GlbModelLoader.Parsed parsed, GlbModelLoader.Primitive prim)
        {
            if (prim.MaterialIndex < 0 || prim.MaterialIndex >= parsed.Materials.Count) return -1;
            return parsed.Materials[prim.MaterialIndex].Item2;
        }

        private static Material MakeMaterial(string name, Shader shader, Texture2D tex, bool cutout,
                                             Color tint, ManualLogSource log)
        {
            if (cutout)
            {
                var std = Shader.Find("Standard");
                if (std == null) std = Shader.Find("Unlit/Transparent Cutout");
                if (std == null) return MakeOpaque(name, shader, tex);   // never throw over a hair card
                var cm = new Material(std) { name = name };
                if (tex != null) cm.mainTexture = tex;
                cm.color = tint;
                // standard cutout recipe (the hair atlases are alpha cut-outs: without the alpha
                // test every strand card renders as a solid quad)
                cm.SetFloat("_Mode", 1f);
                cm.SetInt("_SrcBlend", (int)BlendMode.One);
                cm.SetInt("_DstBlend", (int)BlendMode.Zero);
                cm.SetInt("_ZWrite", 1);
                cm.EnableKeyword("_ALPHATEST_ON");
                cm.SetFloat("_Cutoff", 0.45f);
                cm.renderQueue = 2450;
                TrySet(cm, "_Metallic", 0f);
                TrySet(cm, "_Glossiness", 0.10f);
                return cm;
            }
            // v0.9.25: the level's surface shader puts a specular glint on the flat-shaded
            // facets of a 12-sided trouser tube - a pale band ran down the front of each leg
            // and a bright ring sat on every boot rim (measured: (183,156,125) against the
            // trousers' (67,68,55), i.e. one facet lit 2.7x). Standard, matte and with
            // specular highlights off, is what the avatar already uses and it still takes
            // the probe SH exactly like the concrete does.
            var stdShader = Shader.Find("Standard");
            var m = new Material(stdShader != null ? stdShader : (shader ?? Shader.Find("Standard")))
            { name = name };
            m.color = tint;
            if (tex != null)
            {
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
                else if (m.HasProperty("_BC")) m.SetTexture("_BC", tex);
                else m.mainTexture = tex;
            }
            TrySet(m, "_Metallic", 0f);
            TrySet(m, "_Glossiness", 0f);
            TrySet(m, "_Smoothness", 0f);
            try
            {
                m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                m.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
            }
            catch (System.Exception swallowed) { CoopDiag.Swallowed(swallowed); }
            if (m.HasProperty("_texcoord")) m.SetVector("_texcoord", new Vector4(1f, 1f, 0f, 0f));
            return m;
        }

        /// <summary>
        /// v0.9.78 — a semi-transparent material for the pilots' lowered helmet visor: the GLB's
        /// material carries alpha &lt; 1, and the alpha-blended Standard mode is what makes the face
        /// read through the tinted glass. ZWrite is off so it never hides what is behind it.
        /// </summary>
        private static Material MakeGlass(string name, Texture2D tex, Color tint, ManualLogSource log)
        {
            var std = Shader.Find("Standard");
            if (std == null)
            {
                log?.LogWarning($"[FIGURE] {name}: no Standard shader, the visor will be opaque");
                return MakeOpaque(name, null, tex);
            }
            var m = new Material(std) { name = name };
            if (tex != null) m.mainTexture = tex;
            m.SetFloat("_Mode", 3f);
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
            m.color = tint;
            TrySet(m, "_Metallic", 0f);
            TrySet(m, "_Glossiness", 0.65f);
            try
            {
                m.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
            }
            catch (System.Exception swallowed) { CoopDiag.Swallowed(swallowed); }
            return m;
        }

        /// <summary>Opaque fallback for a hair card when no alpha-cutout shader is available.</summary>
        private static Material MakeOpaque(string name, Shader shader, Texture2D tex)
        {
            var m = new Material(shader ?? Shader.Find("Standard")) { name = name };
            if (tex != null)
            {
                if (m.HasProperty("_BC")) m.SetTexture("_BC", tex);
                else if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            }
            TrySet(m, "_Metallic", 0f);
            TrySet(m, "_Smoothness", 0.12f);
            return m;
        }

        private static void TrySet(Material m, string prop, float v)
        {
            try { if (m.HasProperty(prop)) m.SetFloat(prop, v); } catch (System.Exception swallowed) { CoopDiag.Swallowed(swallowed); }
        }

        /// <summary>The GLBs live next to `villager.glb`, in `BepInEx/plugins/BabbdiCoop/figures/`.</summary>
        public static string Dir
        {
            get
            {
                var d = System.IO.Path.GetDirectoryName(typeof(GlbFigureBuilder).Assembly.Location);
                return System.IO.Path.Combine(d ?? string.Empty, "BabbdiCoop", "figures");
            }
        }
    }
}
