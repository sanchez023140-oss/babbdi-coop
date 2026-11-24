using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace BabbdiCoop
{
    // Part of the SovietDistrict builder - see SovietDistrict.cs.
    internal static partial class SovietDistrict
    {

        // ---------------------------------------- the five figures on the House's roof (v0.9.21)

        /// <summary>Where the five figures stand (district coordinates of their feet) вЂ” probed by
        /// `ProbeWalkability`, so "they stand on the roof" is measured, not assumed.</summary>
        public static readonly List<Vector3> FigFeet = new List<Vector3>();
        /// <summary>v0.9.29 вЂ” each figure's own height in metres (2,05 m вЂ¦ 2,70 m), read back from the
        /// loaded mesh. The close-up cameras and the chest probe scale with it, otherwise the tall ones
        /// would be shot at the waist.</summary>
        public static readonly List<float> FigHeight = new List<float>();

        /// <summary>Height of figure `i` (fallback only while the GLBs have not been loaded).</summary>
        private static float FigHi(int i, float fallback = 2.10f)
        {
            return (i >= 0 && i < FigHeight.Count && FigHeight[i] > 0.5f) ? FigHeight[i] : fallback;
        }
        /// <summary>
        /// v0.9.25 вЂ” the five figures on the House of Soviets' roof: **new characters**, built
        /// procedurally in the game's own style, not assembled out of other NPCs' parts.
        ///
        /// The user: В«РќРµ СЃС‚СЂРѕР№ СЃ РЅСѓР»СЏ РёР· РїСЂРёРјРёС‚РёРІРѕРІ, Р° РІРѕР·СЊРјРё СЃРєРµР»РµС‚С‹ СЌС‚РёС… РїРµСЂСЃРѕРЅР°Р¶РµР№вЂ¦В» в†’ v0.9.24 used the
        /// game's own body and heads, but a warped photo-scanned body still read as "mixed assets", and the
        /// follow-up (В«РїРѕСЂР°Р±РѕС‚Р°Р№ РЅР°Рґ СЃС‚РёР»РёР·Р°С†РёРµР№ NPCВ») settled it. The five are now built from scratch by
        /// `figs3.py` in the harness with the proportions, palette and face language **measured off the
        /// game's own people** (`[NPCSCAN]`: 1,79-2,23 m tall, **five heads** high; `map/district_0`: a pale
        /// bone-mask face with small dark eyes and a thin mouth line, chunky boots, long splayed fingers, a
        /// muted photoreal palette carrying **one strong orange**):
        ///
        ///   fig_1  scientist  2,05 m  white lab coat, blue shirt, dark bob, clipboard
        ///   fig_2  docker     2,12 m  navy beanie, orange hi-vis vest over an olive jacket, stubble
        ///   fig_3  official   2,16 m  bald, moustache, olive jacket, white shirt + dark red tie
        ///   fig_4  officer    2,21 m  peaked cap, olive greatcoat, belt, tall boots
        ///   fig_5  soldier    2,14 m  winter beanie, full beard, camo jacket, load-bearing vest
        ///
        /// Every figure is one mesh with **two primitives** (skin + cloth) on a single 2048x1024 atlas; the
        /// skin half gives the face a whole 896x1024 block, which is what makes eyes, brows, the mouth,
        /// stubble and the beard border readable at this poly count (2,6-3,4 k triangles per figure).
        ///
        /// The models are pre-baked GLBs (metres, feet at y = 0, face +Z) shipped next to `villager.glb`
        /// in `BabbdiCoop/figures/`; this only places them. They stand on the **front edge of the
        /// podium roof** (the city side), 1,25 m inside the edge, **20 m apart** across the middle 80 m
        /// of the 107,3 m facade, facing the city.
        /// </summary>
        /// <summary>
        /// v0.9.59 вЂ” the five figures leave the House of Soviets' roof (the House is rebuilt) and stand
        /// on a Stalin block's roof instead: the widest ordinary block, 2 m inside its +z edge, 4 m
        /// apart, facing outward.
        /// </summary>
        private static void FiguresOnBlock()
        {
            FigFeet.Clear();
            FigHeight.Clear();
            Bldg pick = null;
            float best = 0f;
            for (int i = 0; i < bldgs.Count; i++)
            {
                var b = bldgs[i];
                if (b.memorial || b.penthouse || b.legs) continue;
                float area = b.w * b.d;
                if (area > best) { best = area; pick = b; }
            }
            if (pick == null) { log.LogWarning("[FIGURE] no block roof for the seven вЂ” skipped"); return; }
            var rot = Quaternion.Euler(0f, pick.yaw, 0f);
            float roofY = pick.Top + 0.26f;          // the roof plate's top face (see `Roof`)
            float zn = pick.d * 0.5f - 2f;           // 2 m inside the +z edge
            var shader = mRough != null ? mRough.shader : Shader.Find("Standard");
            int loaded = 0;
            for (int i = 0; i < FigNames.Length; i++)
            {
                // v0.9.78: seven now (two helicopter pilots joined the row), so the row is
                // centred on the middle one and the two extra slots sit at its ends
                Vector3 feet = new Vector3(pick.p.x, 0f, pick.p.z) + rot * new Vector3((i - 3) * 4f, 0f, zn)
                               + Vector3.up * roofY;
                FigFeet.Add(feet);
                FigHeight.Add(2.10f);
                string file = System.IO.Path.Combine(GlbFigureBuilder.Dir, $"fig_{i + 1}.glb");
                if (!System.IO.File.Exists(file))
                {
                    log.LogWarning($"[FIGURE] figure {i + 1} ({FigNames[i]}): {file} is missing вЂ” skipped");
                    continue;
                }
                try
                {
                    var go = GlbFigureBuilder.Load(file, feet, pick.yaw, root, shader, log,
                                                   $"figure_{i + 1}_{FigNames[i].Replace(" ", "_")}");
                    var mf = go != null ? go.GetComponentInChildren<MeshFilter>() : null;
                    if (mf != null && mf.sharedMesh != null)
                        FigHeight[i] = mf.sharedMesh.bounds.size.y;   // v0.9.29: per-figure framing
                    loaded++;
                }
                catch (Exception e)
                {
                    log.LogWarning($"[FIGURE] figure {i + 1} ({FigNames[i]}) failed: {e.Message}");
                }
            }
            log.LogInfo($"[FIGURE] the seven are on another Stalin roof now: block {pick.w:0.0}x{pick.d:0.0} m " +
                        $"at {pick.p.x:0.0},{pick.p.z:0.0}, roof {roofY:0.0} m, 2 m inside the " +
                        $"+z edge, 4 m apart, facing outward; {loaded} of {FigNames.Length} loaded from " +
                        $"{GlbFigureBuilder.Dir} (new characters in the game's own style вЂ” " +
                        $"{string.Join(", ", FigLooks)} вЂ” 2,04-2,72 m tall, four heads high)");
        }

        /// <summary>The seven, in the order of `fig_1..7.glb` on the roof (west to east).</summary>
        private static readonly string[] FigNames =
        {
            "scientist", "docker", "official", "officer", "soldier", "pilot_a", "pilot_b"
        };

        /// <summary>What each one wears вЂ” logged once so the build says what is on the roof.</summary>
        private static readonly string[] FigLooks =
        {
            "scientist: white lab coat, blue shirt, dark bob, clipboard",
            "docker: navy beanie, orange hi-vis vest, stubble, white trainers, heavy build",
            "official: bald, moustache, olive jacket, red tie, black dress shoes",
            "officer: peaked cap, olive greatcoat, belt",
            "soldier: winter beanie, full beard, camo jacket, vest",
            "pilot_a: grey flight suit, dark flight helmet with a lowered smoked visor, chest control unit, black gloves and boots",
            "pilot_b: light grey flight suit, white helmet with a lowered visor, olive chest rig with pouches and a flag patch"
        };

        // ------------------------------------------------------------------ preview

        private static Vector3 HM(Vector3 v) => heliMesh != null ? heliMesh.TransformPoint(v) : v;

        public static void SetRampState(int st)
        {
            if (heliHatch != null) heliHatch.SetPose(st == 1 ? 0f : st == 2 ? 0.45f : 1f);
        }
    }
}
