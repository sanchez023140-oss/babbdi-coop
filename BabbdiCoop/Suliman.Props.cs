using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BabbdiCoop
{
    /// <summary>Billboard quad for lamp halos (one component per lamp, cheap).</summary>
    internal sealed class SulimanBillboard : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            transform.rotation = cam.transform.rotation;
        }
    }

    // Street lamps, utility poles and wires, conifers, benches and the return obelisk.
    internal static partial class Suliman
    {
        private static Material treeMat, treeSnowMat;

        private static void BuildProps()
        {
            BuildTrees();
            BuildLamps();
            BuildPoles();
            BuildPlazaProps();
            SpawnPropeller();
            Log($"[SULIMAN] props: {SulimanData.TreeCount} conifers, {SulimanData.LampCount} lamps, " +
                $"{SulimanData.PoleCount} poles");
        }

        // ------------------------------------------------------------------ trees

        private static void BuildTrees()
        {
            if (treeMat == null)
            {
                var sh = Shader.Find("Standard");
                treeMat = new Material(sh) { name = "SulimanPine" };
                treeMat.SetColor("_Color", new Color(0.10f, 0.16f, 0.12f));
                treeMat.SetFloat("_Glossiness", 0.08f);
                treeMat.SetFloat("_Metallic", 0f);
                treeSnowMat = new Material(SnowBankMat) { name = "SulimanPineSnow" };
            }
            var trunks = new SGeo { UvScale = 0.5f };
            var needles = new SGeo { UvScale = 0.5f };
            var caps = new SGeo { UvScale = 0.5f };

            for (int i = 0; i < SulimanData.TreeCount; i++)
            {
                float x = SulimanData.Trees[i * 4], z = SulimanData.Trees[i * 4 + 1];
                float s = SulimanData.Trees[i * 4 + 2];
                int kind = (int)SulimanData.Trees[i * 4 + 3];
                if (OnWater(x, z)) continue;
                if (InLandmarkClear(x, z)) continue;
                // keep the spawn square and the landmark clearings clear
                if (Mathf.Abs(x - SpawnLocal.x) < 80f && Mathf.Abs(z - SpawnLocal.z) < 80f) continue;
                if (Mathf.Abs(x - 900f) < 30f && Mathf.Abs(z - 580f) < 30f) continue;
                if (Mathf.Abs(x - 930f) < 20f && Mathf.Abs(z - 920f) < 25f) continue;
                if (Mathf.Abs(x - 1180f) < 26f && Mathf.Abs(z - 1180f) < 22f) continue;
                if (Mathf.Abs(x - 455f) < 26f && Mathf.Abs(z - 1240f) < 26f) continue;
                if (Mathf.Abs(x - 1560f) < 34f && Mathf.Abs(z - 700f) < 34f) continue;
                if (Mathf.Abs(x - 1000f) < 30f && Mathf.Abs(z - 1440f) < 30f) continue;
                if (Mathf.Abs(x - 1180f) < 28f && Mathf.Abs(z - 1380f) < 28f) continue;
                float y = HeightAt(x, z);
                AddTree(trunks, needles, caps, new Vector3(x, y, z), s, kind, i);
            }
            trunks.Emit(Root, "tree_trunks", WoodDarkMat);
            needles.Emit(Root, "tree_needles", treeMat);
            caps.Emit(Root, "tree_snow", treeSnowMat);
        }

        private static void AddTree(SGeo trunks, SGeo needles, SGeo caps, Vector3 p, float s, int kind, int seed)
        {
            float h = (kind == 0 ? 1f : kind == 1 ? 1.25f : 0.82f) * s;
            float trunkH = 2.0f * s;
            trunks.Cylinder(p, 0.13f * s, trunkH, 5, null);
            // tiers of needles
            int tiers = kind == 1 ? 4 : 3;
            for (int t = 0; t < tiers; t++)
            {
                float f = (t + 1f) / tiers;
                float cy = p.y + h * (1.4f + t * 1.55f) * s;
                float rad = (2.25f - t * 0.55f) * s;
                float th = (3.1f - t * 0.35f) * s;
                int seg = 8;
                for (int i = 0; i < seg; i++)
                {
                    float a0 = Mathf.PI * 2f * i / seg + seed;
                    float a1 = Mathf.PI * 2f * (i + 1) / seg + seed;
                    Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                    Vector3 d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                    Vector3 q0 = new Vector3(p.x, cy, p.z) + d0 * rad;
                    Vector3 q1 = new Vector3(p.x, cy, p.z) + d1 * rad;
                    Vector3 apex = new Vector3(p.x, cy + th, p.z);
                    needles.Tri(q0, q1, apex, (d0 + d1).normalized,
                        new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f));
                    // snow layer: only the top tier carries a white cap
                    if (t == tiers - 1 || kind == 2)
                    {
                        Vector3 s0 = new Vector3(p.x, cy + th * 0.32f, p.z) + d0 * rad * 0.72f;
                        Vector3 s1 = new Vector3(p.x, cy + th * 0.32f, p.z) + d1 * rad * 0.72f;
                        Vector3 sa = new Vector3(p.x, cy + th * 1.03f, p.z);
                        caps.Tri(s0, s1, sa, (d0 + d1).normalized,
                            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 1));
                    }
                }
            }
            if (kind == 0)
            {
                // a short bare top
                trunks.Cylinder(new Vector3(p.x, p.y + h * 5.2f * s, p.z), 0.05f * s, 0.6f * s, 4, null);
            }
        }

        // ------------------------------------------------------------------ lamps

        private static void BuildLamps()
        {
            var poles = new SGeo { UvScale = 0.5f };
            var arms = new SGeo { UvScale = 0.5f };
            var heads = new SGeo { UvScale = 1f };
            var shafts = new SGeo { UvScale = 1f };
            int idx = 0;
            for (int i = 0; i < SulimanData.LampCount; i++)
            {
                float x = SulimanData.Lamps[i * 3], z = SulimanData.Lamps[i * 3 + 1];
                float yaw = SulimanData.Lamps[i * 3 + 2];
                float y = HeightAt(x, z);
                Vector3 dir = new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad));
                Vector3 baseP = new Vector3(x, y, z);
                Vector3 top = baseP + Vector3.up * 5.1f;
                poles.Cylinder(baseP, 0.115f, 5.1f, 6, null);
                Vector3 armEnd = top + dir * 1.45f + Vector3.up * 0.15f;
                arms.Beam(top, armEnd, 0.10f, 0.10f, Vector3.up);
                Vector3 headP = armEnd + dir * 0.16f - Vector3.up * 0.10f;
                heads.Box(headP, new Vector3(0.42f, 0.13f, 0.24f),
                          Quaternion.LookRotation(dir, Vector3.up));
                // billboard halo
                AddHalo(headP + Vector3.down * 0.02f, 3.6f);
                AddLampSpot(headP + Vector3.down * 0.2f, 38f);
                // shafts removed: the cone base drew a hard circle on the snow
                idx++;
            }
            poles.Emit(Root, "lamp_poles", MetalMat);
            arms.Emit(Root, "lamp_arms", MetalMat);
            heads.Emit(Root, "lamp_heads", LampHeadMat, false, ShadowCastingMode.Off);
            // shafts.Emit(Root, "lamp_shafts", ShaftMat, false, ShadowCastingMode.Off);
        }

        private static void AddShaft(SGeo g, Vector3 top, float ground, float radius)
        {
            const int seg = 8;
            float topR = 0.24f;
            float y0 = ground + 0.05f;
            if (top.y - y0 < 0.5f) return;
            for (int i = 0; i < seg; i++)
            {
                float a0 = Mathf.PI * 2f * i / seg;
                float a1 = Mathf.PI * 2f * (i + 1) / seg;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                Vector3 d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                Vector3 p0 = new Vector3(top.x, top.y - 0.12f, top.z) + d0 * topR;
                Vector3 p1 = new Vector3(top.x, top.y - 0.12f, top.z) + d1 * topR;
                Vector3 q0 = new Vector3(top.x, y0, top.z) + d0 * radius;
                Vector3 q1 = new Vector3(top.x, y0, top.z) + d1 * radius;
                g.QuadUv(p0, p1, q1, q0, (d0 + d1).normalized,
                         new Vector2(0f, 0f), new Vector2(1f, 0f),
                         new Vector2(1f, 1f), new Vector2(0f, 1f));
            }
        }

        private static void AddPoolQuad(SGeo g, Vector3 c, float r)
        {
            g.QuadUv(new Vector3(c.x - r, c.y, c.z - r), new Vector3(c.x - r, c.y, c.z + r),
                     new Vector3(c.x + r, c.y, c.z + r), new Vector3(c.x + r, c.y, c.z - r),
                     Vector3.up, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
        }

        private static void AddHalo(Vector3 p, float size)
        {
            var go = new GameObject("lamp_halo");
            go.transform.SetParent(Root, false);
            go.transform.localPosition = p;
            var m = new Mesh { name = "halo" };
            float r = size * 0.5f;
            m.vertices = new[]
            {
                new Vector3(-r, -r, 0f), new Vector3(-r, r, 0f),
                new Vector3(r, r, 0f), new Vector3(r, -r, 0f)
            };
            m.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = HaloTexMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.AddComponent<SulimanBillboard>();
        }

        // ------------------------------------------------------------------ utility poles

        private static void BuildPoles()
        {
            var poles = new SGeo { UvScale = 0.6f };
            var arms = new SGeo { UvScale = 0.6f };
            var wires = new SGeo { UvScale = 1f };
            for (int i = 0; i < SulimanData.PoleCount; i++)
            {
                float x = SulimanData.Poles[i * 2], z = SulimanData.Poles[i * 2 + 1];
                float y = HeightAt(x, z);
                Vector3 p = new Vector3(x, y, z);
                poles.Cylinder(p, 0.14f, 8.2f, 6, null);
                arms.Box(new Vector3(x, y + 7.7f, z), new Vector3(1.9f, 0.11f, 0.12f),
                         Quaternion.identity);
                arms.Box(new Vector3(x, y + 6.9f, z), new Vector3(1.4f, 0.09f, 0.10f),
                         Quaternion.identity);
            }
            for (int c = 0; c < SulimanData.PoleChains.Length / 2; c++)
            {
                int start = SulimanData.PoleChains[c * 2], count = SulimanData.PoleChains[c * 2 + 1];
                for (int i = 0; i + 1 < count; i++)
                {
                    int a = start + i, b = start + i + 1;
                    Vector3 pa = new Vector3(SulimanData.Poles[a * 2], HeightAt(SulimanData.Poles[a * 2], SulimanData.Poles[a * 2 + 1]) + 7.7f, SulimanData.Poles[a * 2 + 1]);
                    Vector3 pb = new Vector3(SulimanData.Poles[b * 2], HeightAt(SulimanData.Poles[b * 2], SulimanData.Poles[b * 2 + 1]) + 7.7f, SulimanData.Poles[b * 2 + 1]);
                    if ((pa - pb).sqrMagnitude > 70f * 70f) continue;
                    for (int off = -1; off <= 1; off += 2)
                    {
                        Vector3 oa = pa + new Vector3(off * 0.7f, 0f, 0f);
                        Vector3 ob = pb + new Vector3(off * 0.7f, 0f, 0f);
                        Vector3 mid = (oa + ob) * 0.5f - Vector3.up * 0.55f;
                        wires.Beam(oa, mid, 0.028f, 0.028f, Vector3.up);
                        wires.Beam(mid, ob, 0.028f, 0.028f, Vector3.up);
                    }
                }
            }
            poles.Emit(Root, "util_poles", ConcreteMat);
            arms.Emit(Root, "util_arms", WoodDarkMat);
            wires.Emit(Root, "util_wires", MetalMat, false, ShadowCastingMode.Off);
        }

        // ------------------------------------------------------------------ plaza / small props

        private static void BuildPlazaProps()
        {
            var wood = new SGeo { UvScale = 0.5f };
            var metal = new SGeo { UvScale = 0.5f };
            var snow = new SGeo { UvScale = 0.4f };
            var conc = new SGeo { UvScale = 0.3f };
            var heads = new SGeo { UvScale = 1f };
            var glow = new SGeo { UvScale = 1f };
            var ground = new SGeo { UvScale = 0.10f };

            // a cleared apron under the spawn: packed snow with a few concrete slabs
            Vector3 sp = new Vector3(SpawnLocal.x, HeightAt(SpawnLocal.x, SpawnLocal.z) + 0.07f, SpawnLocal.z);
            for (int i = -1; i <= 1; i++)
                for (int j = -1; j <= 1; j++)
                {
                    Vector3 c = sp + new Vector3(i * 9f, 0f, j * 9f);
                    ground.QuadUv(new Vector3(c.x - 4.5f, c.y, c.z - 4.5f), new Vector3(c.x - 4.5f, c.y, c.z + 4.5f),
                                  new Vector3(c.x + 4.5f, c.y, c.z + 4.5f), new Vector3(c.x + 4.5f, c.y, c.z - 4.5f),
                                  Vector3.up, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
                }
            // four plaza lamps
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = sp + new Vector3((i < 2 ? -1f : 1f) * 12f, 0f, (i % 2 == 0 ? -1f : 1f) * 12f);
                p.y = HeightAt(p.x, p.z);
                metal.Cylinder(p, 0.09f, 4.5f, 6, null);
                Vector3 hp = p + Vector3.up * 4.62f;
                heads.Box(hp, new Vector3(0.40f, 0.14f, 0.40f), Quaternion.identity);
                AddHalo(hp + Vector3.down * 0.10f, 3.0f);
                AddLampSpot(hp + Vector3.down * 0.3f, 24f);
            }
            // benches
            for (int i = 0; i < 5; i++)
            {
                float a = 1.0f + i * Mathf.PI * 2f / 5f;
                Vector3 c = sp + new Vector3(Mathf.Cos(a) * 14f, 0f, Mathf.Sin(a) * 14f);
                AddBench(wood, metal, snow, c, -a * Mathf.Rad2Deg);
            }
            // a few snow-covered concrete blocks
            for (int i = 0; i < 8; i++)
            {
                float a = i * 2.4f;
                Vector3 c = sp + new Vector3(Mathf.Cos(a) * (6f + i), 0f, Mathf.Sin(a) * (6f + i * 0.7f));
                c.y = HeightAt(c.x, c.z);
                conc.Box(new Vector3(c.x, c.y + 0.28f, c.z), new Vector3(1.2f, 0.55f, 0.7f),
                         Quaternion.Euler(0f, i * 37f, 0f));
                snow.Box(new Vector3(c.x, c.y + 0.58f, c.z), new Vector3(1.25f, 0.08f, 0.75f),
                         Quaternion.Euler(0f, i * 37f, 0f));
            }

            // v0.10.50: the welcome stele and every other sign/lettering were removed

            wood.Emit(Root, "plaza_wood", WoodMat);
            metal.Emit(Root, "plaza_metal", MetalMat);
            snow.Emit(Root, "plaza_snow", SnowBankMat);
            conc.Emit(Root, "plaza_concrete", ConcreteMat);
            heads.Emit(Root, "plaza_lamp_heads", LampHeadMat, false, ShadowCastingMode.Off);
            glow.Emit(Root, "plaza_glow", GlowWarmMat, false, ShadowCastingMode.Off);
            ground.Emit(Root, "plaza_ground", PackedMat);
        }

        private static void SpawnPropeller()
        {
            Propeller src = null;
            foreach (var p in Resources.FindObjectsOfTypeAll<Propeller>())
            {
                if (p == null || !p.gameObject.scene.IsValid()) continue;
                src = p;
                if (p.gameObject.activeInHierarchy) break;
            }
            if (src == null)
            {
                Warn("[SULIMAN] no Propeller found in the level; spare flyer not placed");
                return;
            }
            Vector3 at = new Vector3(SpawnLocal.x + 10f, 0f, SpawnLocal.z + 2f);
            at.y = HeightAt(at.x, at.z) + 0.46f;
            var clone = UnityEngine.Object.Instantiate(src.gameObject);
            clone.name = "SulimanPropeller";
            clone.transform.SetParent(Root, false);
            clone.transform.position = Origin + at;
            clone.transform.rotation = Quaternion.Euler(90f, 25f, 0f);
            clone.SetActive(true);
            var cloneProp = clone.GetComponentInChildren<Propeller>(true);
            if (cloneProp != null) ConvertPropellerObject(cloneProp.gameObject, false);
            var rends = clone.GetComponentsInChildren<Renderer>(true);
            Log($"[SULIMAN] spare propeller placed at {at} (source {src.name}, {rends.Length} renderers)");
            // small concrete stand so the pickup is visible above the snow
            var conc = new SGeo { UvScale = 0.4f };
            var snow = new SGeo { UvScale = 0.4f };
            conc.Box(new Vector3(at.x, at.y - 0.28f, at.z), new Vector3(1.5f, 0.32f, 1.5f), Quaternion.identity);
            snow.Box(new Vector3(at.x, at.y - 0.10f, at.z), new Vector3(1.55f, 0.05f, 1.55f), Quaternion.identity);
            conc.Emit(Root, "propeller_stand", ConcreteMat);
            snow.Emit(Root, "propeller_stand_snow", SnowBankMat);
        }

        private static void AddBench(SGeo wood, SGeo metal, SGeo snow, Vector3 c, float yaw)
        {
            var r = Quaternion.Euler(0f, yaw, 0f);
            Vector3 y = Vector3.up;
            wood.Box(c + y * 0.52f, new Vector3(2.1f, 0.10f, 0.55f), r);
            wood.Box(c + y * 0.86f + r * new Vector3(0f, 0f, 0.25f), new Vector3(2.1f, 0.45f, 0.08f), r);
            snow.Box(c + y * 0.58f, new Vector3(2.15f, 0.05f, 0.60f), r);
            for (int s = -1; s <= 1; s += 2)
                metal.Box(c + y * 0.28f + r * new Vector3(0.8f * s, 0f, 0f), new Vector3(0.08f, 0.55f, 0.5f), r);
        }

        // ------------------------------------------------------------------ return obelisk

        private static void BuildPortal()
        {
            Vector3 local = ReturnAnchor - Origin;
            float y = HeightAt(local.x, local.z);
            Vector3 c = new Vector3(local.x, y, local.z);
            var conc = new SGeo { UvScale = 0.4f };
            var snow = new SGeo { UvScale = 0.4f };
            var red = new SGeo { UvScale = 1f };
            var rust = new SGeo { UvScale = 0.4f };

            conc.Box(c + Vector3.up * 0.22f, new Vector3(3.4f, 0.44f, 3.4f), Quaternion.identity);
            rust.Box(c + Vector3.up * 2.4f, new Vector3(1.35f, 4.4f, 0.95f), Quaternion.identity);
            snow.Box(c + Vector3.up * 4.68f, new Vector3(1.48f, 0.12f, 1.08f), Quaternion.identity);
            // four glowing seams + a red cap
            red.Box(c + new Vector3(0f, 2.4f, -0.50f), new Vector3(0.12f, 3.7f, 0.04f), Quaternion.identity);
            red.Box(c + new Vector3(0.70f, 2.4f, 0f), new Vector3(0.04f, 3.7f, 0.12f), Quaternion.identity);
            red.Box(c + new Vector3(0f, 2.4f, 0.50f), new Vector3(0.12f, 3.7f, 0.04f), Quaternion.identity);
            red.Box(c + new Vector3(-0.70f, 2.4f, 0f), new Vector3(0.04f, 3.7f, 0.12f), Quaternion.identity);
            red.Box(c + new Vector3(0f, 4.55f, 0f), new Vector3(0.95f, 0.22f, 0.95f), Quaternion.identity);
            // snow cap on the plinth
            snow.Box(c + Vector3.up * 0.46f, new Vector3(3.5f, 0.07f, 3.5f), Quaternion.identity);

            conc.Emit(Root, "obelisk_concrete", ConcreteDarkMat);
            rust.Emit(Root, "obelisk_rust", PortalMat);
            snow.Emit(Root, "obelisk_snow", SnowBankMat);
            red.Emit(Root, "obelisk_red", PortalEdgeMat, false, ShadowCastingMode.Off);

            // red light and a warm porch lamp make the stele findable in the snowfall
            var lg = new GameObject("obelisk_light");
            lg.transform.SetParent(Root, false);
            lg.transform.localPosition = c + Vector3.up * 2.4f;
            var light = lg.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.16f, 0.10f);
            light.intensity = 2.2f;
            light.range = 13f;
            light.shadows = LightShadows.None;

            // v0.10.50: no lettering on the obelisk either
            ReturnAnchor = Origin + c;
            Log($"[SULIMAN] return obelisk at {ReturnAnchor}");
        }
    }
}
