using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace BabbdiCoop
{
    // The eight brutalist reference pieces, re-used as town structures, plus the boiler house.
    internal static partial class Suliman
    {
        private static void BuildLandmarks()
        {
            StadiumDetail();
            RuinedTowersDetail();
            PumpStationDetail();
            Viewpoint();
            UnderpassDetail();
            CollapsedBlocksDetail();
            RiverStairsDetail();
            CliffHouseDetail();
            BoilerHouse();
            PortholeGalleryDetail();
            Log("[SULIMAN] 8 detailed reference landmarks + boiler house placed");
        }

        private static void Lab(SGeo g, MaterialBank bank, Material mat, Vector3 c, Vector3 s, Quaternion r)
            => bank.Geo(mat, 0.4f).Box(c, s, r);

        private static void Label(Vector3 local, float yaw, string text, float size)
        {
            // v0.10.50: no signage/lettering anywhere on the map
        }

        private static void SignBoard(SGeo metal, SGeo glow, Vector3 c, float yaw, float w, float h)
        {
            // v0.10.50: the boards went with the lettering
        }

        // ------------------------------------------------------------------ 1 stadium

        private static void Stadium()
        {
            Vector3 c0 = OnGround(900f, 580f);
            float gy = c0.y;
            var conc = new SGeo { UvScale = 0.3f };
            var snow = new SGeo { UvScale = 0.25f };
            var ice = new SGeo { UvScale = 0.1f };
            var metal = new SGeo { UvScale = 0.5f };
            var glow = new SGeo { UvScale = 1f };
            var col = new SGeo { UvScale = 1f };

            // ice arena, cleared of snow
            const float r0 = 12.5f;
            Vector3 iceC = new Vector3(c0.x, gy + 0.10f, c0.z);
            for (int i = 0; i < 36; i++)
            {
                float a0 = Mathf.PI * 2f * i / 36f, a1 = Mathf.PI * 2f * (i + 1) / 36f;
                Vector3 p0 = iceC + new Vector3(Mathf.Cos(a0) * r0, 0f, Mathf.Sin(a0) * r0);
                Vector3 p1 = iceC + new Vector3(Mathf.Cos(a1) * r0, 0f, Mathf.Sin(a1) * r0);
                ice.Tri(iceC, p0, p1, Vector3.up,
                    new Vector2(0.5f, 0.5f), new Vector2(Mathf.Cos(a0) * 0.5f + 0.5f, Mathf.Sin(a0) * 0.5f + 0.5f),
                    new Vector2(Mathf.Cos(a1) * 0.5f + 0.5f, Mathf.Sin(a1) * 0.5f + 0.5f));
            }

            int tiers = 6;
            for (int t = 0; t < tiers; t++)
            {
                float rad = r0 + 1.6f + t * 1.55f;
                float yy = gy + 0.35f + t * 0.55f;
                int seg = 30;
                for (int i = 0; i < seg; i++)
                {
                    float a0 = Mathf.PI * 2f * i / seg;
                    // leave two entrances (north and south)
                    if (Mathf.Abs(Mathf.Sin(a0)) < 0.16f) continue;
                    float am = a0 + Mathf.PI / seg;
                    Vector3 p = c0 + new Vector3(Mathf.Cos(am) * (rad + 0.75f), 0f, Mathf.Sin(am) * (rad + 0.75f));
                    var r = Quaternion.Euler(0f, -am * Mathf.Rad2Deg + 90f, 0f);
                    conc.Box(new Vector3(p.x, yy, p.z), new Vector3(3.7f, 0.55f, 2.05f), r);
                    snow.Box(new Vector3(p.x, yy + 0.30f, p.z), new Vector3(3.66f, 0.07f, 2.0f), r);
                    col.Box(new Vector3(p.x, yy - 2f, p.z), new Vector3(3.7f, 4f, 2.05f), r);
                }
            }
            // low outer wall
            int os = 30;
            for (int i = 0; i < os; i++)
            {
                float a0 = Mathf.PI * 2f * i / os;
                if (Mathf.Abs(Mathf.Sin(a0)) < 0.14f) continue;
                float am = a0 + Mathf.PI / os;
                float rad = r0 + 1.6f + tiers * 1.55f + 0.4f;
                Vector3 p = c0 + new Vector3(Mathf.Cos(am) * rad, 0f, Mathf.Sin(am) * rad);
                var r = Quaternion.Euler(0f, -am * Mathf.Rad2Deg + 90f, 0f);
                conc.Box(new Vector3(p.x, gy + 3.85f, p.z), new Vector3(3.3f, 0.85f, 0.42f), r);
                snow.Box(new Vector3(p.x, gy + 4.30f, p.z), new Vector3(3.32f, 0.08f, 0.47f), r);
                col.Box(new Vector3(p.x, gy + 1.0f, p.z), new Vector3(3.3f, 5f, 0.42f), r);
            }
            // four floodlight masts
            for (int i = 0; i < 4; i++)
            {
                float a = Mathf.PI * 0.25f + i * Mathf.PI * 0.5f;
                Vector3 p = c0 + new Vector3(Mathf.Cos(a) * 27f, 0f, Mathf.Sin(a) * 27f);
                p.y = HeightAt(p.x, p.z);
                metal.Cylinder(p, 0.22f, 13f, 6, null);
                Vector3 head = p + Vector3.up * 13.2f;
                metal.Box(head + new Vector3(Mathf.Cos(a) * -0.7f, 0f, Mathf.Sin(a) * -0.7f),
                          new Vector3(2.2f, 0.25f, 0.6f), Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f));
                glow.Box(head + new Vector3(Mathf.Cos(a) * -0.9f, -0.15f, Mathf.Sin(a) * -0.9f),
                         new Vector3(1.9f, 0.10f, 0.42f), Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f));
                AddLampSpot(head + new Vector3(Mathf.Cos(a) * -1.4f, -0.3f, Mathf.Sin(a) * -1.4f), 30f);
                var lg = new GameObject("stadium_light");
                lg.transform.SetParent(Root, false);
                lg.transform.localPosition = head;
                var li = lg.AddComponent<Light>();
                li.type = LightType.Point; li.color = new Color(1f, 0.83f, 0.62f);
                li.intensity = 3.5f; li.range = 34f; li.shadows = LightShadows.None;
            }
            // scoreboard / stage
            Vector3 st = c0 + new Vector3(0f, 0f, -r0 - 0.6f);
            conc.Box(new Vector3(st.x, gy + 1.2f, st.z), new Vector3(9f, 2.4f, 1.2f), Quaternion.identity);
            snow.Box(new Vector3(st.x, gy + 2.45f, st.z), new Vector3(9.1f, 0.10f, 1.3f), Quaternion.identity);
            SignBoard(metal, glow, new Vector3(st.x, gy + 3.6f, st.z), 180f, 6.4f, 1.2f);
            Label(new Vector3(st.x, gy + 3.6f, st.z + 0.12f), 0f, "СТАДИОН", 0.05f);

            conc.Emit(Root, "stadium_concrete", StepMat);
            snow.Emit(Root, "stadium_snow", SnowBankMat);
            ice.Emit(Root, "stadium_ice", IceMat, true, ShadowCastingMode.Off);
            metal.Emit(Root, "stadium_metal", MetalMat);
            glow.Emit(Root, "stadium_glow", GlowWarmMat, false, ShadowCastingMode.Off);
            col.EmitCollider(Root, "stadium_col");
            Log("[SULIMAN] landmark: stadium amphitheatre at 900,580 (ice rink, 6 tiers)");
        }

        // ------------------------------------------------------------------ 2 ruined towers

        private static void RuinedTowers()
        {
            Vector3 a = OnGround(630f, 400f);
            var conc = new SGeo { UvScale = 0.25f };
            var snow = new SGeo { UvScale = 0.2f };
            var rust = new SGeo { UvScale = 0.3f };
            var col = new SGeo { UvScale = 1f };
            RuinTower(conc, snow, rust, col, a + new Vector3(-9f, 0f, 0f), 15f, 15f, 30f, 9);
            RuinTower(conc, snow, rust, col, a + new Vector3(9f, 0f, 4f), 12f, 13f, 22f, 6);
            conc.Emit(Root, "ruin_concrete", ConcreteDarkMat);
            snow.Emit(Root, "ruin_snow", SnowBankMat);
            rust.Emit(Root, "ruin_rust", RustMat);
            col.EmitCollider(Root, "ruin_col");
            Log("[SULIMAN] landmark: two ruined towers at 630,400");
        }

        private static void RuinTower(SGeo conc, SGeo snow, SGeo rust, SGeo col,
                                      Vector3 c, float w, float d, float h, int seed)
        {
            var sr = new System.Random(seed);
            float gy = c.y;
            int floors = Mathf.FloorToInt(h / 4.2f);
            // four walls as broken panels
            for (int side = 0; side < 4; side++)
            {
                bool xSide = side < 2;
                float len = xSide ? d : w;
                Vector3 n = xSide ? new Vector3(side == 0 ? -1f : 1f, 0f, 0f)
                                  : new Vector3(0f, 0f, side == 2 ? -1f : 1f);
                Vector3 mid = c + n * (xSide ? w * 0.5f : d * 0.5f);
                int panels = Mathf.Max(2, Mathf.FloorToInt(len / 2.5f));
                for (int f = 0; f < floors; f++)
                {
                    for (int p = 0; p < panels; p++)
                    {
                        if (sr.NextDouble() < 0.13) continue;      // missing panel = broken hole
                        if (f == 0 && p == panels / 2) continue;   // entrance
                        float along = -len * 0.5f + (p + 0.5f) * (len / panels);
                        Vector3 pos = mid + (xSide ? new Vector3(0f, 0f, along) : new Vector3(along, 0f, 0f));
                        pos.y = gy + (f + 0.5f) * 4.2f;
                        var rot = xSide ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
                        Vector3 size = new Vector3(len / panels - 0.05f, 4.2f, 0.55f);
                        conc.Box(pos, size, rot);
                        col.Box(pos, size, rot);
                    }
                }
            }
            // floors with big holes
            for (int f = 1; f < floors; f++)
            {
                float y = gy + f * 4.2f;
                if (f % 2 == 0) continue;
                conc.Box(new Vector3(c.x, y, c.z), new Vector3(w, 0.45f, d), Quaternion.identity);
                col.Box(new Vector3(c.x, y, c.z), new Vector3(w, 0.45f, d), Quaternion.identity);
                snow.Box(new Vector3(c.x, y + 0.26f, c.z), new Vector3(w - 0.8f, 0.06f, d - 0.8f), Quaternion.identity);
            }
            // broken crown + rebar
            for (int i = 0; i < 10; i++)
            {
                float x = c.x + (float)sr.NextDouble() * w - w * 0.5f;
                float z = c.z + (float)sr.NextDouble() * d - d * 0.5f;
                float top = gy + h + (float)sr.NextDouble() * 2.5f;
                conc.Box(new Vector3(x, top, z), new Vector3(1.2f, 2.5f, 1.0f),
                         Quaternion.Euler(sr.Next(-8, 8), sr.Next(0, 90), sr.Next(-8, 8)));
                rust.Beam(new Vector3(x, top + 1f, z), new Vector3(x + 0.4f, top + 3.4f, z + 0.2f),
                          0.045f, 0.045f, Vector3.forward);
            }
            // rubble at the foot
            for (int i = 0; i < 26; i++)
            {
                float x = c.x + ((float)sr.NextDouble() - 0.5f) * (w + 10f);
                float z = c.z + ((float)sr.NextDouble() - 0.5f) * (d + 10f);
                float s = 0.5f + (float)sr.NextDouble() * 1.8f;
                float y = HeightAt(x, z) + s * 0.25f;
                conc.Box(new Vector3(x, y, z), new Vector3(s, s * 0.6f, s * 0.8f),
                         Quaternion.Euler(sr.Next(-20, 20), sr.Next(0, 90), sr.Next(-20, 20)));
            }
        }

        // ------------------------------------------------------------------ 3 pump station

        private static void PumpStation()
        {
            Vector3 c = OnGround(535f, 1322f);
            float gy = c.y;
            var conc = new SGeo { UvScale = 0.3f };
            var snow = new SGeo { UvScale = 0.25f };
            var rust = new SGeo { UvScale = 0.35f };
            var lit = new SGeo { UvScale = 1f };
            var col = new SGeo { UvScale = 1f };

            // bunker body
            conc.Box(new Vector3(c.x, gy + 3.5f, c.z), new Vector3(18f, 7f, 12f), Quaternion.identity);
            col.Box(new Vector3(c.x, gy + 3.5f, c.z), new Vector3(18f, 7f, 12f), Quaternion.identity);
            snow.Box(new Vector3(c.x, gy + 7.08f, c.z), new Vector3(18.2f, 0.12f, 12.2f), Quaternion.identity);
            // banded plinth
            rust.Box(new Vector3(c.x, gy + 0.35f, c.z), new Vector3(18.3f, 0.7f, 12.3f), Quaternion.identity);
            // control-room window (warm)
            lit.Box(new Vector3(c.x - 4.5f, gy + 4.3f, c.z - 6.05f), new Vector3(4.0f, 1.6f, 0.05f), Quaternion.identity);
            AddLampSpot(new Vector3(c.x - 4.5f, gy + 3.8f, c.z - 7.4f), 12f);
            // door
            rust.Box(new Vector3(c.x + 2.0f, gy + 1.45f, c.z - 6.05f), new Vector3(1.8f, 2.9f, 0.10f), Quaternion.identity);
            // pipes down to the river
            Vector3 iceP = OnGround(475f, 1234f);
            for (int i = 0; i < 3; i++)
            {
                Vector3 a = new Vector3(c.x + 4.0f + i * 0.8f, gy + 1.0f, c.z - 6.1f);
                Vector3 b = new Vector3(a.x + 5f, HeightAt(a.x + 5f, a.z - 1f) + 0.6f, a.z - 1f);
                rust.Beam(a, b, 0.38f, 0.38f, Vector3.up);
                rust.Cylinder(b, 0.38f, 3.2f, 8, null);
            }
            // stair down the bank
            Vector3 st = new Vector3(c.x - 9.2f, gy, c.z - 6f);
            for (int i = 0; i < 14; i++)
            {
                float x = st.x - i * 0.65f;
                float z = st.z - i * 0.9f;
                float y = HeightAt(x, z) + 0.12f;
                conc.Box(new Vector3(x, y, z), new Vector3(1.8f, 0.20f, 1.1f), Quaternion.Euler(0f, 32f, 0f));
                col.Box(new Vector3(x, y, z), new Vector3(1.8f, 0.20f, 1.1f), Quaternion.Euler(0f, 32f, 0f));
                snow.Box(new Vector3(x, y + 0.12f, z), new Vector3(1.7f, 0.05f, 1.0f), Quaternion.Euler(0f, 32f, 0f));
            }
            // upper control cabin
            conc.Box(new Vector3(c.x - 4f, gy + 8.5f, c.z), new Vector3(6f, 3f, 6f), Quaternion.identity);
            snow.Box(new Vector3(c.x - 4f, gy + 10.05f, c.z), new Vector3(6.2f, 0.10f, 6.2f), Quaternion.identity);
            lit.Box(new Vector3(c.x - 4f, gy + 8.7f, c.z - 3.05f), new Vector3(3.6f, 1.3f, 0.05f), Quaternion.identity);
            AddLampSpot(new Vector3(c.x - 4f, gy + 8.2f, c.z - 4.2f), 13f);
            // tall vent stack with the red beacon
            rust.Cylinder(new Vector3(c.x + 7f, gy + 7f, c.z + 4f), 0.55f, 13f, 8, null);
            var beacon = new GameObject("pump_beacon");
            beacon.transform.SetParent(Root, false);
            beacon.transform.localPosition = new Vector3(c.x + 8f, gy + 20.5f, c.z + 5f);
            var bl = beacon.AddComponent<Light>();
            bl.type = LightType.Point; bl.color = new Color(1f, 0.1f, 0.05f);
            bl.intensity = 2.4f; bl.range = 12f; bl.shadows = LightShadows.None;
            SignBoard(rust, lit, new Vector3(c.x - 2f, gy + 5.6f, c.z - 6.12f), 180f, 3.4f, 0.8f);
            Label(new Vector3(c.x - 2f, gy + 5.6f, c.z - 6.2f), 0f, "НАСОСНАЯ", 0.032f);

            conc.Emit(Root, "pump_concrete", ConcreteMat);
            snow.Emit(Root, "pump_snow", SnowBankMat);
            rust.Emit(Root, "pump_rust", RustMat);
            lit.Emit(Root, "pump_light", GlowWarmMat, false, ShadowCastingMode.Off);
            col.EmitCollider(Root, "pump_col");
            Log("[SULIMAN] landmark: river pump station at 535,1322");
        }

        // ------------------------------------------------------------------ 4 viewpoint

        private static void Viewpoint()
        {
            Vector3 c = OnGround(1000f, 1440f);
            float deckY = Mathf.Max(HeightAt(1000f, 1400f) + 1.2f, c.y + 0.8f);
            var conc = new SGeo { UvScale = 0.3f };
            var snow = new SGeo { UvScale = 0.25f };
            var metal = new SGeo { UvScale = 0.5f };
            var glow = new SGeo { UvScale = 1f };
            var col = new SGeo { UvScale = 1f };

            // cantilevered deck hanging south over the slope
            conc.Box(new Vector3(c.x, deckY - 0.35f, c.z - 5f), new Vector3(15f, 0.7f, 16f), Quaternion.identity);
            col.Box(new Vector3(c.x, deckY - 0.35f, c.z - 5f), new Vector3(15f, 0.7f, 16f), Quaternion.identity);
            snow.Box(new Vector3(c.x, deckY + 0.02f, c.z - 5f), new Vector3(14.8f, 0.09f, 15.8f), Quaternion.identity);
            // two inclined supports
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 top = new Vector3(c.x + s * 5.5f, deckY - 0.6f, c.z - 11f);
                Vector3 bot = new Vector3(c.x + s * 7.5f, HeightAt(c.x + s * 7.5f, c.z + 1f) - 1.5f, c.z + 1f);
                conc.Beam(bot, top, 1.0f, 1.0f, Vector3.forward);
                col.Beam(bot, top, 1.0f, 1.0f, Vector3.forward);
            }
            // railings on three sides
            for (int side = 0; side < 3; side++)
            {
                Vector3 a, b;
                if (side == 0) { a = new Vector3(c.x - 7.4f, deckY, c.z - 12.8f); b = new Vector3(c.x + 7.4f, deckY, c.z - 12.8f); }
                else if (side == 1) { a = new Vector3(c.x - 7.4f, deckY, c.z - 12.8f); b = new Vector3(c.x - 7.4f, deckY, c.z + 2.6f); }
                else { a = new Vector3(c.x + 7.4f, deckY, c.z - 12.8f); b = new Vector3(c.x + 7.4f, deckY, c.z + 2.6f); }
                Vector3 d = b - a;
                metal.Box((a + b) * 0.5f + Vector3.up * 1.05f, new Vector3(0.09f, 0.08f, d.magnitude), Quaternion.LookRotation(d, Vector3.up));
                int posts = Mathf.Max(2, Mathf.FloorToInt(d.magnitude / 1.7f));
                for (int i = 0; i <= posts; i++)
                {
                    Vector3 p = Vector3.Lerp(a, b, i / (float)posts);
                    metal.Box(p + Vector3.up * 0.55f, new Vector3(0.09f, 1.1f, 0.09f), Quaternion.identity);
                }
            }
            // stair up from the town side
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = new Vector3(c.x - 8.4f, 0f, c.z + 2.6f + i * 0.65f);
                float y = Mathf.Lerp(HeightAt(p.x, p.z) + 0.1f, deckY, i / 5f);
                conc.Box(new Vector3(p.x, y - 0.15f, p.z), new Vector3(2.2f, 0.32f, 0.75f), Quaternion.identity);
                col.Box(new Vector3(p.x, y - 0.15f, p.z), new Vector3(2.2f, 0.32f, 0.75f), Quaternion.identity);
            }
            // kiosk with a lit window
            conc.Box(new Vector3(c.x + 4.8f, deckY + 1.6f, c.z + 1.4f), new Vector3(5f, 3.2f, 4f), Quaternion.identity);
            col.Box(new Vector3(c.x + 4.8f, deckY + 1.6f, c.z + 1.4f), new Vector3(5f, 3.2f, 4f), Quaternion.identity);
            snow.Box(new Vector3(c.x + 4.8f, deckY + 3.28f, c.z + 1.4f), new Vector3(5.2f, 0.10f, 4.2f), Quaternion.identity);
            glow.Box(new Vector3(c.x + 2.28f, deckY + 1.9f, c.z + 0.7f), new Vector3(0.06f, 1.1f, 1.8f), Quaternion.identity);
            AddLampSpot(new Vector3(c.x + 1.2f, deckY + 1.8f, c.z + 0.5f), 12f);
            // telescope
            metal.Cylinder(new Vector3(c.x, deckY + 0.7f, c.z - 12f), 0.09f, 1.1f, 6, null);
            metal.Beam(new Vector3(c.x, deckY + 1.7f, c.z - 12f), new Vector3(c.x, deckY + 2.0f, c.z - 13.6f), 0.16f, 0.16f, Vector3.up);
            SignBoard(metal, glow, new Vector3(c.x - 2.5f, deckY + 2.2f, c.z + 2.62f), 0f, 3.6f, 0.7f);
            Label(new Vector3(c.x - 2.5f, deckY + 2.2f, c.z + 2.72f), 180f, "СМОТРОВАЯ", 0.032f);

            conc.Emit(Root, "view_concrete", ConcreteMat, true);
            snow.Emit(Root, "view_snow", SnowBankMat);
            metal.Emit(Root, "view_metal", MetalMat, true);
            glow.Emit(Root, "view_glow", GlowWarmMat, false, ShadowCastingMode.Off);
            col.EmitCollider(Root, "view_col");
            Log($"[SULIMAN] landmark: cantilever viewpoint at 1000,1440 (deck y={deckY:0.0})");
        }

        // ------------------------------------------------------------------ 5 underpass

        private static void Underpass()
        {
            Vector3 c = OnGround(930f, 920f);
            float gy = c.y;
            var conc = new SGeo { UvScale = 0.3f };
            var snow = new SGeo { UvScale = 0.25f };
            var metal = new SGeo { UvScale = 0.5f };
            var glow = new SGeo { UvScale = 1f };
            var col = new SGeo { UvScale = 1f };

            float W = 11f, L = 24f, H = 4.6f;
            // two side walls and a roof; open ends make a real tunnel
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 p = new Vector3(c.x + s * W * 0.5f, gy + H * 0.5f, c.z);
                conc.Box(p, new Vector3(1.2f, H, L), Quaternion.identity);
                col.Box(p, new Vector3(1.2f, H, L), Quaternion.identity);
            }
            conc.Box(new Vector3(c.x, gy + H, c.z), new Vector3(W + 2.4f, 0.8f, L), Quaternion.identity);
            col.Box(new Vector3(c.x, gy + H, c.z), new Vector3(W + 2.4f, 0.8f, L), Quaternion.identity);
            snow.Box(new Vector3(c.x, gy + H + 0.45f, c.z), new Vector3(W + 2.6f, 0.10f, L + 0.2f), Quaternion.identity);
            // portal frames
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 z = c + new Vector3(0f, 0f, s * L * 0.5f);
                conc.Box(new Vector3(z.x, gy + H + 0.2f, z.z), new Vector3(W + 4f, 1.0f, 1.0f), Quaternion.identity);
                for (int k = -1; k <= 1; k += 2)
                    conc.Box(new Vector3(z.x + k * (W * 0.5f + 1.3f), gy + H * 0.5f, z.z),
                             new Vector3(1.2f, H, 1.2f), Quaternion.identity);
            }
            // warm ceiling lights and wall band
            for (int i = -1; i <= 1; i++)
            {
                glow.Box(new Vector3(c.x, gy + H - 0.25f, c.z + i * 7f),
                         new Vector3(2.0f, 0.10f, 0.35f), Quaternion.identity);
                AddLampSpot(new Vector3(c.x, gy + H - 0.6f, c.z + i * 7f), 14f);
            }
            for (int s = -1; s <= 1; s += 2)
                metal.Box(new Vector3(c.x + s * (W * 0.5f - 0.55f), gy + 2.1f, c.z),
                          new Vector3(0.05f, 0.5f, L - 1f), Quaternion.identity);
            // stairs to the roof on the east side
            for (int i = 0; i < 7; i++)
            {
                Vector3 p = new Vector3(c.x + W * 0.5f + 2.2f, 0f, c.z + L * 0.5f - 1f - i * 0.8f);
                p.y = Mathf.Lerp(HeightAt(p.x, p.z) + 0.1f, gy + H + 0.5f, i / 6f);
                conc.Box(new Vector3(p.x, p.y - 0.16f, p.z), new Vector3(2.0f, 0.32f, 0.85f), Quaternion.identity);
                col.Box(new Vector3(p.x, p.y - 0.16f, p.z), new Vector3(2.0f, 0.32f, 0.85f), Quaternion.identity);
                snow.Box(new Vector3(p.x, p.y + 0.02f, p.z), new Vector3(1.95f, 0.05f, 0.80f), Quaternion.identity);
            }
            SignBoard(metal, glow, new Vector3(c.x - 3f, gy + 5.4f, c.z - L * 0.5f - 0.6f), 0f, 3.8f, 0.8f);
            Label(new Vector3(c.x - 3f, gy + 5.4f, c.z - L * 0.5f - 0.70f), 180f, "ПЕРЕХОД", 0.035f);

            conc.Emit(Root, "pass_concrete", ConcreteMat);
            snow.Emit(Root, "pass_snow", SnowBankMat);
            metal.Emit(Root, "pass_metal", MetalMat);
            glow.Emit(Root, "pass_glow", GlowWarmMat, false, ShadowCastingMode.Off);
            col.EmitCollider(Root, "pass_col");
            Log("[SULIMAN] landmark: concrete underpass at 930,920");
        }

        // ------------------------------------------------------------------ 6 collapsed blocks

        private static void CollapsedBlocks()
        {
            Vector3 c = OnGround(760f, 300f);
            var conc = new SGeo { UvScale = 0.3f };
            var snow = new SGeo { UvScale = 0.25f };
            var rust = new SGeo { UvScale = 0.4f };
            var col = new SGeo { UvScale = 1f };
            var sr = new System.Random(991);
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = c + new Vector3(-9f + i * 6f, 0f, -3f + (i % 2) * 7f);
                p.y = HeightAt(p.x, p.z) + 0.6f;
                var r = Quaternion.Euler(sr.Next(-24, 24), sr.Next(0, 180), sr.Next(-24, 24));
                Vector3 size = new Vector3(14f, 0.8f, 9f);
                conc.Box(p, size, r);
                snow.Box(p + Vector3.up * 0.45f, new Vector3(13.5f, 0.10f, 8.6f), r);
                col.Box(p, size, r);
                for (int k = 0; k < 6; k++)
                {
                    Vector3 q = p + new Vector3((float)sr.NextDouble() * 18f - 9f,
                                                (float)sr.NextDouble() * 1.4f,
                                                (float)sr.NextDouble() * 12f - 6f);
                    float s = 0.5f + (float)sr.NextDouble() * 1.7f;
                    conc.Box(q, new Vector3(s, s * 0.7f, s), Quaternion.Euler(sr.Next(0, 90), sr.Next(0, 90), 0f));
                    col.Box(q, new Vector3(s, s * 0.7f, s), Quaternion.Euler(sr.Next(0, 90), sr.Next(0, 90), 0f));
                }
                for (int k = 0; k < 4; k++)
                    rust.Beam(p + new Vector3(0f, 0.4f, 0f), p + new Vector3((float)sr.NextDouble() * 8f - 4f, 2f + (float)sr.NextDouble() * 2f, (float)sr.NextDouble() * 6f - 3f),
                              0.05f, 0.05f, Vector3.forward);
            }
            conc.Emit(Root, "collapse_concrete", ConcreteDarkMat);
            snow.Emit(Root, "collapse_snow", SnowBankMat);
            rust.Emit(Root, "collapse_rust", RustMat);
            col.EmitCollider(Root, "collapse_col");
            Log("[SULIMAN] landmark: collapsed block field at 760,300");
        }

        // ------------------------------------------------------------------ 7 river stairs

        private static void RiverStairs()
        {
            Vector3 c = OnGround(420f, 1150f);
            var conc = new SGeo { UvScale = 0.3f };
            var snow = new SGeo { UvScale = 0.3f };
            var metal = new SGeo { UvScale = 0.5f };
            var col = new SGeo { UvScale = 1f };
            Vector3 dir = new Vector3(-1f, 0f, -0.25f).normalized;
            Vector3 side = new Vector3(-dir.z, 0f, dir.x);
            float topY = HeightAt(c.x, c.z) + 1.2f;
            // a monumental staircase descending west toward the frozen river
            for (int i = 0; i < 15; i++)
            {
                Vector3 p = c + dir * (i * 0.9f);
                float y = topY - i * 0.22f;
                conc.Box(new Vector3(p.x, y - 0.12f, p.z), new Vector3(6f, 0.24f, 1.0f),
                         Quaternion.LookRotation(dir, Vector3.up));
                snow.Box(new Vector3(p.x, y + 0.02f, p.z), new Vector3(5.9f, 0.05f, 0.95f),
                         Quaternion.LookRotation(dir, Vector3.up));
                col.Box(new Vector3(p.x, y - 0.35f, p.z), new Vector3(6f, 0.7f, 1.0f),
                        Quaternion.LookRotation(dir, Vector3.up));
            }
            // solid side walls and handrails along the full flight
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 a0 = c + side * (3.15f * s) + Vector3.up * (topY + 0.55f);
                Vector3 a1 = c + dir * 13.0f + side * (3.15f * s) + Vector3.up * (topY - 2.35f);
                conc.Beam(a0, a1, 0.5f, 1.0f, Vector3.forward);
                col.Beam(a0, a1, 0.5f, 1.0f, Vector3.forward);
                metal.Beam(a0 + Vector3.up * 1.0f, a1 + Vector3.up * 1.0f, 0.07f, 0.07f, Vector3.up);
                for (int i = 0; i < 5; i++)
                {
                    Vector3 p = Vector3.Lerp(a0, a1, i / 4f);
                    metal.Box(new Vector3(p.x, p.y + 0.5f, p.z), new Vector3(0.08f, 1.0f, 0.08f), Quaternion.identity);
                }
            }
            // two lamps at the top
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 p = c + side * (3.9f * s);
                p.y = HeightAt(p.x, p.z);
                metal.Cylinder(p, 0.10f, 4.2f, 6, null);
                Vector3 head = p + Vector3.up * 4.3f;
                metal.Box(head, new Vector3(0.5f, 0.15f, 0.3f), Quaternion.identity);
                AddLampSpot(head, 15f);
            }
            conc.Emit(Root, "stairs_concrete", ConcreteMat);
            snow.Emit(Root, "stairs_snow", SnowBankMat);
            metal.Emit(Root, "stairs_metal", MetalMat);
            col.EmitCollider(Root, "stairs_col");
            Log("[SULIMAN] landmark: river stairs at 420,1150");
        }

        // ------------------------------------------------------------------ 8 cliff house

        private static void CliffHouse()
        {
            Vector3 c = OnGround(1560f, 700f);
            float top = Mathf.Max(HeightAt(1560f, 700f), HeightAt(1530f, 700f)) + 1.0f;
            var conc = new SGeo { UvScale = 0.3f };
            var snow = new SGeo { UvScale = 0.25f };
            var metal = new SGeo { UvScale = 0.5f };
            var glow = new SGeo { UvScale = 1f };
            var col = new SGeo { UvScale = 1f };

            // support columns and the cantilevered house
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 p = new Vector3(c.x + s * 6.0f, 0f, c.z - 4f);
                float gy = HeightAt(p.x, p.z);
                conc.Box(new Vector3(p.x, (gy + top) * 0.5f, p.z), new Vector3(1.6f, top - gy, 1.6f), Quaternion.identity);
                col.Box(new Vector3(p.x, (gy + top) * 0.5f, p.z), new Vector3(1.6f, top - gy, 1.6f), Quaternion.identity);
            }
            conc.Box(new Vector3(c.x, top + 2.6f, c.z), new Vector3(20f, 5.2f, 12f), Quaternion.identity);
            col.Box(new Vector3(c.x, top + 2.6f, c.z), new Vector3(20f, 5.2f, 12f), Quaternion.identity);
            snow.Box(new Vector3(c.x, top + 5.30f, c.z), new Vector3(20.2f, 0.12f, 12.2f), Quaternion.identity);
            // bright warm windows (one of the few inhabited ruins high above the town)
            for (int i = -2; i <= 2; i++)
            {
                glow.Box(new Vector3(c.x + i * 3.6f, top + 3.1f, c.z - 6.05f),
                         new Vector3(2.4f, 1.6f, 0.06f), Quaternion.identity);
                AddLampSpot(new Vector3(c.x + i * 3.6f, top + 2.6f, c.z - 7.3f), 14f);
                glow.Box(new Vector3(c.x + i * 3.6f, top + 3.1f, c.z + 6.05f),
                         new Vector3(2.4f, 1.6f, 0.06f), Quaternion.identity);
                AddLampSpot(new Vector3(c.x + i * 3.6f, top + 2.6f, c.z + 7.3f), 14f);
            }
            // balcony (south) and a matching north terrace
            conc.Box(new Vector3(c.x, top + 0.65f, c.z - 8.2f), new Vector3(17f, 0.35f, 5f), Quaternion.identity);
            snow.Box(new Vector3(c.x, top + 0.87f, c.z - 8.2f), new Vector3(16.9f, 0.08f, 4.9f), Quaternion.identity);
            metal.Box(new Vector3(c.x, top + 1.75f, c.z - 10.6f), new Vector3(17f, 0.08f, 0.08f), Quaternion.identity);
            for (int i = -8; i <= 8; i++)
                metal.Box(new Vector3(c.x + i, top + 1.30f, c.z - 10.6f), new Vector3(0.07f, 1.0f, 0.07f), Quaternion.identity);
            conc.Box(new Vector3(c.x, top + 0.65f, c.z + 8.2f), new Vector3(17f, 0.35f, 5f), Quaternion.identity);
            snow.Box(new Vector3(c.x, top + 0.87f, c.z + 8.2f), new Vector3(16.9f, 0.08f, 4.9f), Quaternion.identity);
            metal.Box(new Vector3(c.x, top + 1.75f, c.z + 10.6f), new Vector3(17f, 0.08f, 0.08f), Quaternion.identity);
            for (int i = -8; i <= 8; i++)
                metal.Box(new Vector3(c.x + i, top + 1.30f, c.z + 10.6f), new Vector3(0.07f, 1.0f, 0.07f), Quaternion.identity);
            // stairs down the hill
            for (int i = 0; i < 18; i++)
            {
                float x = c.x - 11.5f - i * 0.55f;
                float z = c.z + 1f + i * 0.8f;
                float y = HeightAt(x, z) + 0.15f;
                conc.Box(new Vector3(x, y, z), new Vector3(2.8f, 0.28f, 1.0f), Quaternion.Euler(0f, 22f, 0f));
                col.Box(new Vector3(x, y, z), new Vector3(2.8f, 0.28f, 1.0f), Quaternion.Euler(0f, 22f, 0f));
            }
            // red aviation beacon on the roof
            metal.Cylinder(new Vector3(c.x, top + 5.3f, c.z), 0.08f, 4.2f, 5, null);
            var bg = new GameObject("cliff_beacon");
            bg.transform.SetParent(Root, false);
            bg.transform.localPosition = new Vector3(c.x, top + 9.6f, c.z);
            var bl = bg.AddComponent<Light>();
            bl.type = LightType.Point; bl.color = new Color(1f, 0.12f, 0.06f);
            bl.intensity = 2.0f; bl.range = 12f; bl.shadows = LightShadows.None;

            conc.Emit(Root, "cliff_concrete", ConcreteMat);
            snow.Emit(Root, "cliff_snow", SnowBankMat);
            metal.Emit(Root, "cliff_metal", MetalMat);
            glow.Emit(Root, "cliff_glow", GlowWarmMat, false, ShadowCastingMode.Off);
            col.EmitCollider(Root, "cliff_col");
            Log($"[SULIMAN] landmark: cliff house at 1560,700 (deck y={top:0.0})");
        }

        // ------------------------------------------------------------------ porthole gallery (ref 4)

        private static void PortholeGallery()
        {
            Vector3 c = OnGround(1180f, 1380f);
            float gy = c.y;
            var conc = new SGeo { UvScale = 0.3f };
            var snow = new SGeo { UvScale = 0.25f };
            var dark = new SGeo { UvScale = 1f };
            var metal = new SGeo { UvScale = 0.5f };
            var col = new SGeo { UvScale = 1f };

            // a long concrete gallery with seven round openings, like the hillside bunker reference
            conc.Box(new Vector3(c.x, gy + 2.8f, c.z), new Vector3(36f, 5.6f, 9f), Quaternion.identity);
            col.Box(new Vector3(c.x, gy + 2.8f, c.z), new Vector3(36f, 5.6f, 9f), Quaternion.identity);
            snow.Box(new Vector3(c.x, gy + 5.66f, c.z), new Vector3(36.2f, 0.12f, 9.2f), Quaternion.identity);
            // front face: dark discs + a concrete rim
            float frontZ = c.z - 4.51f;
            for (int k = -3; k <= 3; k++)
            {
                float px = c.x + k * 4.6f;
                float py = gy + 3.35f;
                const float r = 1.15f;
                Vector3 centre = new Vector3(px, py, frontZ);
                for (int i = 0; i < 16; i++)
                {
                    float a0 = Mathf.PI * 2f * i / 16f, a1 = Mathf.PI * 2f * (i + 1) / 16f;
                    Vector3 p0 = centre + new Vector3(Mathf.Cos(a0) * r, Mathf.Sin(a0) * r, 0f);
                    Vector3 p1 = centre + new Vector3(Mathf.Cos(a1) * r, Mathf.Sin(a1) * r, 0f);
                    dark.Tri(centre, p0, p1, Vector3.back,
                             new Vector2(0.5f, 0.5f),
                             new Vector2(Mathf.Cos(a0) * 0.5f + 0.5f, Mathf.Sin(a0) * 0.5f + 0.5f),
                             new Vector2(Mathf.Cos(a1) * 0.5f + 0.5f, Mathf.Sin(a1) * 0.5f + 0.5f));
                    // rim
                    Vector3 q0 = centre + new Vector3(Mathf.Cos(a0) * (r + 0.34f), Mathf.Sin(a0) * (r + 0.34f), 0.02f);
                    Vector3 q1 = centre + new Vector3(Mathf.Cos(a1) * (r + 0.34f), Mathf.Sin(a1) * (r + 0.34f), 0.02f);
                    conc.QuadUv(p0 + new Vector3(0f, 0f, 0.03f), p1 + new Vector3(0f, 0f, 0.03f), q1, q0,
                                Vector3.back, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
                }
                // a snow sill under the porthole
                snow.Box(new Vector3(px, py - r - 0.10f, frontZ - 0.18f), new Vector3(3.1f, 0.08f, 0.5f), Quaternion.identity);
            }
            // exterior stair to the roof
            for (int i = 0; i < 9; i++)
            {
                Vector3 p = new Vector3(c.x - 20.5f, 0f, c.z - 5.2f + i * 0.8f);
                float y = Mathf.Lerp(HeightAt(p.x, p.z) + 0.1f, gy + 5.6f, i / 8f);
                conc.Box(new Vector3(p.x, y - 0.14f, p.z), new Vector3(3.2f, 0.28f, 0.95f), Quaternion.identity);
                col.Box(new Vector3(p.x, y - 0.14f, p.z), new Vector3(3.2f, 0.28f, 0.95f), Quaternion.identity);
                snow.Box(new Vector3(p.x, y + 0.02f, p.z), new Vector3(3.15f, 0.05f, 0.9f), Quaternion.identity);
            }
            // rail along the stair
            for (int i = 0; i < 5; i++)
            {
                Vector3 p = new Vector3(c.x - 22.0f, 0f, c.z - 5.2f + i * 1.7f);
                float y = Mathf.Lerp(HeightAt(p.x, p.z) + 0.1f, gy + 5.6f, i / 4f);
                metal.Box(new Vector3(p.x, y + 0.75f, p.z), new Vector3(0.07f, 1.5f, 0.07f), Quaternion.identity);
            }

            conc.Emit(Root, "porthole_concrete", ConcreteMat);
            snow.Emit(Root, "porthole_snow", SnowBankMat);
            dark.Emit(Root, "porthole_dark", GlassDarkMat, false, ShadowCastingMode.Off);
            metal.Emit(Root, "porthole_metal", MetalMat);
            col.EmitCollider(Root, "porthole_col");
            Log("[SULIMAN] landmark: porthole gallery at 1180,1380 (ref 4)");
        }

        // ------------------------------------------------------------------ boiler house

        private static void BoilerHouse()
        {
            Vector3 c = OnGround(1180f, 1180f);
            float gy = c.y;
            var conc = new SGeo { UvScale = 0.3f };
            var snow = new SGeo { UvScale = 0.25f };
            var brick = new SGeo { UvScale = 0.3f };
            var metal = new SGeo { UvScale = 0.5f };
            var glow = new SGeo { UvScale = 1f };
            var col = new SGeo { UvScale = 1f };

            conc.Box(new Vector3(c.x, gy + 4.2f, c.z), new Vector3(20f, 8.4f, 12f), Quaternion.identity);
            col.Box(new Vector3(c.x, gy + 4.2f, c.z), new Vector3(20f, 8.4f, 12f), Quaternion.identity);
            snow.Box(new Vector3(c.x, gy + 8.46f, c.z), new Vector3(20.2f, 0.12f, 12.2f), Quaternion.identity);
            // sawtooth roof hint
            for (int i = -1; i <= 1; i++)
            {
                conc.Box(new Vector3(c.x + i * 6f, gy + 9.2f, c.z), new Vector3(4.5f, 1.4f, 12f),
                         Quaternion.Euler(0f, 0f, 18f));
                snow.Box(new Vector3(c.x + i * 6f, gy + 9.95f, c.z), new Vector3(4.6f, 0.10f, 12.1f),
                         Quaternion.Euler(0f, 0f, 18f));
            }
            // tall brick chimney
            Vector3 ch = new Vector3(c.x + 8f, gy, c.z + 4.5f);
            brick.Cylinder(ch, 1.1f, 32f, 10, null);
            metal.Cylinder(ch + Vector3.up * 32f, 1.25f, 1.0f, 10, null);
            // lit windows and door
            for (int i = -1; i <= 1; i++)
            {
                glow.Box(new Vector3(c.x + i * 5f, gy + 2.4f, c.z - 6.06f), new Vector3(2.6f, 1.4f, 0.06f), Quaternion.identity);
                AddLampSpot(new Vector3(c.x + i * 5f, gy + 2.1f, c.z - 7.2f), 12f);
            }
            SignBoard(metal, glow, new Vector3(c.x, gy + 7.2f, c.z - 6.08f), 180f, 5.4f, 1.1f);
            Label(new Vector3(c.x, gy + 7.2f, c.z - 6.2f), 0f, "КОТЕЛЬНАЯ", 0.04f);

            conc.Emit(Root, "boiler_concrete", ConcreteDarkMat, true);
            snow.Emit(Root, "boiler_snow", SnowBankMat);
            brick.Emit(Root, "boiler_brick", RustMat, true);
            metal.Emit(Root, "boiler_metal", MetalMat, true);
            glow.Emit(Root, "boiler_glow", GlowWarmMat, false, ShadowCastingMode.Off);
            col.EmitCollider(Root, "boiler_col");

            // smoke plume
            var go = new GameObject("boiler_smoke");
            go.transform.SetParent(Root, false);
            go.transform.localPosition = new Vector3(c.x + 8f, gy + 33.5f, c.z + 4.5f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 9f;
            main.startSpeed = 2.2f;
            main.startSize = new ParticleSystem.MinMaxCurve(3f, 7f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.57f, 0.60f, 0.30f),
                                                               new Color(0.75f, 0.77f, 0.80f, 0.16f));
            main.maxParticles = 120;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.02f;
            var em = ps.emission; em.rateOverTime = 7f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 10f; shape.radius = 0.6f;
            var vel = ps.velocityOverLifetime; vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(1.1f, 2.2f);
            vel.y = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            vel.z = new ParticleSystem.MinMaxCurve(0.2f, 0.9f);
            var col2 = ps.colorOverLifetime; col2.enabled = true;
            var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                       new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.15f),
                               new GradientAlphaKey(0f, 1f) });
            col2.color = new ParticleSystem.MinMaxGradient(gr);
            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.material = SmokeMat;
            rend.shadowCastingMode = ShadowCastingMode.Off;

            Log("[SULIMAN] boiler house at 1180,1180 with a 32 m chimney and smoke");
        }
    }
}
