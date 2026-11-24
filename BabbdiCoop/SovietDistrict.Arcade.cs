using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace BabbdiCoop
{
    // Part of the SovietDistrict builder - see SovietDistrict.cs.
    internal static partial class SovietDistrict
    {

        /// <summary>
        /// v0.9.98 вЂ” the air-hockey table on the House of Soviets' **top floor**: the upper hall of the
        /// cantilevered chevron annex (house-local y = 102.55, i.e. 6.4 m above the towers' top floors
        /// and 6.4 m under the annex roof). The model is the user's PSX asset
        /// (`AirhockeyTable.obj`), retextured offline in the game's palette вЂ” a bone rink with
        /// oxide-red face-off circles and steel-teal lines, an oxide-red body, charcoal metal and
        /// near-black wheels вЂ” and shipped as `airhockey.glb` with one **double-sided** material.
        ///
        /// The GLB is authored in metres (v0.10.0: scale 0.46, height x0.80 and width x1.32 baked in:
        /// **5.15 x 2.91 x 3.89 m**), wheels at y = 0. The annex's top floor was chosen because it is
        /// the highest **interior** floor of the House complex (both tower top floors are 6.4 m lower);
        /// it is reached from the west tower's top-floor door and the annex's own stair, or by flying
        /// in through one of its 2.4 x 2.6 m window openings.
        /// </summary>
        private static void AirHockeyTable(Transform house)
        {
            airHockeyPlaced = false;                       // a missing GLB must not leave a stale probe target
            AirHockeyTables.Clear();
            AirHockeyScoreboard.ClearAll();                // v0.10.15: fresh boards on a level build
            const float floorY = 102.55f;                 // house-local: the annex's top floor
            var feet = new Vector3(-37.75f, floorY, 3f);   // the west half of the 40 x 34 m hall
            string dir = System.IO.Path.GetDirectoryName(GlbFigureBuilder.Dir);
            string path = System.IO.Path.Combine(dir ?? string.Empty, "airhockey.glb");
            if (!System.IO.File.Exists(path))
            {
                log.LogWarning("[PROP] airhockey.glb is missing вЂ” skipped (" + path + ")");
                return;
            }
            try
            {
                // useNormals = true: the source's authored PSX normals are kept (recalculating would
                // round the hard panel edges). The GLB is **baked double-sided** вЂ” a front copy plus
                // a back copy with reversed winding and negated normals вЂ” because the built-in
                // Standard shader has no _Cull property; the material is also flagged doubleSided.
                // The underside, the arch's inner faces and the scoreboard's back are therefore
                // drawn from every side, per the project's surface rule.
                var go = GlbFigureBuilder.Load(path, feet, 0f, house, Shader.Find("Standard"), log,
                                               "airhockey_table", "PROP", false, true);
                var mf = go.GetComponentInChildren<MeshFilter>();
                airHockeyHeight = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.size.y : 0f;
                airHockeyFeet = house.localPosition + house.localRotation * feet;
                airHockeyPlaced = true;
                AirHockeyTableGo = go;
                AirHockeyTableRoot = go.transform;
                // v0.10.0: the collider is the **body** only (rink top 1,14 m), not the mesh bounds,
                // which still include the tall central scoreboard arch. The mini-game folds that arch
                // below the floor while playing, so a body-height box is the correct solid.
                {
                    var bc = go.GetComponent<BoxCollider>();
                    if (bc != null && mf != null && mf.sharedMesh != null)
                    {
                        float bodyH = AirHockeyBodyTop;
                        bc.center = new Vector3(0f, bodyH * 0.5f, 0f);
                        bc.size = new Vector3(mf.sharedMesh.bounds.size.x, bodyH, mf.sharedMesh.bounds.size.z);
                    }
                }
                // v0.10.1: the scoreboard arch is a separate GLB — the mini-game hides it instead of
                // stretching its vertices below the floor (that produced a fan of shards in the
                // angled camera). The body/legs/rink are `airhockey.glb`, the arch is `airhockey_arch.glb`.
                string archPath = System.IO.Path.Combine(dir ?? string.Empty, "airhockey_arch.glb");
                if (System.IO.File.Exists(archPath))
                {
                    try
                    {
                        var arch = GlbFigureBuilder.Load(archPath, Vector3.zero, 0f, go.transform,
                                                         Shader.Find("Standard"), log, "airhockey_arch",
                                                         "PROP", false, true);
                        var ac = arch.GetComponent<Collider>();
                        if (ac != null) UnityEngine.Object.Destroy(ac);
                        AirHockeyArchGo = arch;
                        // v0.10.15: the two lamp-end score boards (the main arch keeps its own
                        // material; the boards sample its canopy texel and continue its colour).
                        AirHockeyScoreboard.Attach(0, go.transform, arch, dir, log);
                    }
                    catch (Exception e)
                    {
                        log.LogWarning("[PROP] air-hockey arch failed: " + e.Message);
                    }
                }
                else log.LogWarning("[PROP] airhockey_arch.glb is missing — the idle table will have no scoreboard arch");
                AirHockeyTables.Add(new AirHockeyTableInfo
                {
                    Root = go,
                    Arch = AirHockeyArchGo,
                    Id = 0,
                    Name = "main"
                });

                var mr = go.GetComponentInChildren<MeshRenderer>();
                log.LogInfo($"[PROP] table node: parent={go.transform.parent?.name}, " +
                            $"local={go.transform.localPosition}, world={go.transform.position}, " +
                            $"active={go.activeInHierarchy}, renderer={(mr == null ? "none" : (mr.enabled ? "on" : "off"))}");
                log.LogInfo($"[PROP] air-hockey table on the House of Soviets' top floor: retextured in " +
                            $"the game's palette (oxide-red body, bone rink, steel-teal lines, charcoal " +
                            $"metal), {mf?.sharedMesh?.vertexCount ?? 0} verts, 1 double-sided material, " +
                            $"{mf?.sharedMesh?.bounds.size.x ?? 0f:0.00} x {airHockeyHeight:0.00} x " +
                            $"{mf?.sharedMesh?.bounds.size.z ?? 0f:0.00} m, feet at house-local " +
                            $"({feet.x:0.0}, {feet.y:0.0}, {feet.z:0.0}) = district-local " +
                            $"({airHockeyFeet.x:0.0}, {airHockeyFeet.y:0.0}, {airHockeyFeet.z:0.0}); " +
                            $"reach it through the west tower's top-floor door and the annex stair, or " +
                            $"fly in through the annex windows");
            }
            catch (Exception e)
            {
                log.LogWarning("[PROP] air-hockey table failed: " + e.Message);
            }
        }

        /// <summary>
        /// v0.9.79 вЂ” the Mi-26 on a Stalin skyscraper's roof. The model is the **original**
        /// MI26.WRL geometry (31 408 tris, authored normals, original UVs) with only a mild
        /// detail pass: every texture is grey-scaled, downsampled to 512 px and softened a
        /// touch, and the two livery titles + the tail registration are inpainted and replaced
        /// by barely visible abstract marks. It is parked along the roof's long axis, wheels
        /// on the plate, the 32 m rotor overhanging the parapet.
        /// </summary>
        /// <summary>
        /// v0.10.8 — six patterned showroom tables on the annex's **lower floor** (house-local 96.15,
        /// directly under the playable table at 102.55). Each one is the same `airhockey.glb` with the
        /// six sloppy neon patterns applied to its **own vertical side faces**; the rink top, the rim
        /// top and the black legs keep the original material. v0.10.9 replaced the first floating band
        /// with a direct retexture of the table's side triangles, so there is no gap at all. The arch's
        /// canopy and its two supports are painted in the pattern's dominant hue, with a secondary
        /// accent used for the glowing lamp strip on the canopy edge and its point light.
        /// </summary>
        private static void AirHockeyVariants(Transform house)
        {
            string dir = System.IO.Path.GetDirectoryName(GlbFigureBuilder.Dir);
            string tablePath = System.IO.Path.Combine(dir ?? string.Empty, "airhockey.glb");
            string archPath = System.IO.Path.Combine(dir ?? string.Empty, "airhockey_arch.glb");
            string patternDir = System.IO.Path.Combine(dir ?? string.Empty, "patterns");
            if (!System.IO.File.Exists(tablePath))
            {
                log.LogWarning("[PROP] arcade variants: airhockey.glb is missing — skipped");
                return;
            }
            int made = 0;
            for (int i = 0; i < AirHockeyPatternFiles.Length; i++)
            {
                var spot = AirHockeyVariantSpots[i];
                try
                {
                    var go = GlbFigureBuilder.Load(tablePath, spot, 0f, house, Shader.Find("Standard"), log,
                                                   $"airhockey_variant_{i + 1}", "PROP", false, true);
                    var mf = go.GetComponentInChildren<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        var bc = go.GetComponent<BoxCollider>();
                        if (bc != null)
                        {
                            float bodyH = AirHockeyBodyTop + AirHockeyBumperRaise;
                            bc.center = new Vector3(0f, bodyH * 0.5f, 0f);
                            bc.size = new Vector3(mf.sharedMesh.bounds.size.x, bodyH,
                                                  mf.sharedMesh.bounds.size.z);
                        }
                    }

                    // Pattern band around the body: one image, mirrored once around the loop.
                    string patternPath = System.IO.Path.Combine(patternDir, AirHockeyPatternFiles[i]);
                    Material patternMat;
                    if (System.IO.File.Exists(patternPath))
                    {
                        var tex = LoadAirHockeyPattern(patternPath);
                        patternMat = MakeAirHockeyPatternMaterial(tex, AirHockeyPatternTheme[i]);
                    }
                    else
                    {
                        log.LogWarning($"[PROP] arcade variant {i + 1}: pattern " +
                                       $"{AirHockeyPatternFiles[i]} is missing — using its theme colour");
                        patternMat = MakeArcadeMetal(AirHockeyPatternTheme[i] * 0.85f, 0.08f);
                    }
                    Color arcColor = Color.Lerp(AirHockeyPatternTheme[i], Color.black, 0.18f);
                    Color lampColor = Color.Lerp(AirHockeyPatternAccent[i], Color.white, 0.12f);
                    var fieldMat = MakeAirHockeyFieldMaterial(go, arcColor, lampColor, i + 1);
                    RetextureAirHockeySides(go, patternMat, fieldMat);
                    // v0.10.12: the wide white rim becomes a raised bumper in the arch's exact
                    // theme colour, 5 cm taller than the original table top.
                    var themeMat = MakeArcadeMetal(arcColor, 0.12f);
                    AddRaisedArcadeBumper(go, themeMat);
                    // The arch (scoreboard canopy on two supports) gets the table's palette.
                    GameObject archGo = null;
                    if (System.IO.File.Exists(archPath))
                    {
                        try
                        {
                            archGo = GlbFigureBuilder.Load(archPath, Vector3.zero, 0f, go.transform,
                                                           Shader.Find("Standard"), log,
                                                           $"airhockey_variant_{i + 1}_arch",
                                                           "PROP", false, true);
                            var ac = archGo.GetComponent<Collider>();
                            if (ac != null) UnityEngine.Object.Destroy(ac);
                            PaintAirHockeyArch(archGo, AirHockeyPatternTheme[i],
                                               AirHockeyPatternAccent[i], i + 1, themeMat);
                            // v0.10.15: both lamp ends get the physical score board, its housing
                            // sharing the canopy's material (the lamp body colour, not the light).
                            AirHockeyScoreboard.Attach(i + 1, go.transform, archGo, dir, log);
                        }
                        catch (Exception e)
                        {
                            log.LogWarning($"[PROP] arcade variant {i + 1}: arch failed: {e.Message}");
                        }
                    }
                    AirHockeyTables.Add(new AirHockeyTableInfo
                    {
                        Root = go,
                        Arch = archGo,
                        Id = i + 1,
                        Name = AirHockeyPatternFiles[i]
                    });

                    made++;
                    var c = AirHockeyPatternTheme[i];
                    log.LogInfo($"[PROP] arcade variant {i + 1}/{AirHockeyPatternFiles.Length}: " +
                                $"{AirHockeyPatternFiles[i]} on the annex showroom floor " +
                                $"(house-local {spot.x:0.0}, {spot.y:0.0}, {spot.z:0.0}); " +
                                $"body 5,15 x 1,14 x 3,89 m, mirrored own side faces, arch theme " +
                                $"#{ColorUtility.ToHtmlStringRGB(c)} + accent " +
                                $"#{ColorUtility.ToHtmlStringRGB(AirHockeyPatternAccent[i])}");
                }
                catch (Exception e)
                {
                    log.LogWarning($"[PROP] arcade variant {i + 1} failed: {e.Message}");
                }
            }
            log.LogInfo($"[PROP] arcade showroom: {made} of {AirHockeyPatternFiles.Length} patterned tables " +
                        $"on the annex floors below the playable table — six at 96.15 m and four at " +
                        $"89.75 m; rink tops untouched, legs black");
        }

        /// <summary>
        /// v0.10.17: five AI-slop arcade cabinets along the **back wall** of the annex's top hall
        /// (the room with the single air-hockey table). Each one repeats the same cabinet mesh but
        /// carries its own Stable-Diffusion side art, mirrored on the other side; the body keeps the
        /// source asset's worn-yellow texture. They are decor for now — the playable dino-run game
        /// comes in a later round. House-local metres: the back wall is z = -14 (inner face -13.53),
        /// the row stands with its backs 1 m off it and the fronts facing +z into the room.
        /// </summary>
        private static void ArcadeCabinets(Transform house)
        {
            ArcadeCabinetList.Clear();
            string dir = System.IO.Path.GetDirectoryName(GlbFigureBuilder.Dir);
            string path = System.IO.Path.Combine(dir ?? string.Empty, "arcade_cabinet.glb");
            if (!System.IO.File.Exists(path))
            {
                log.LogWarning("[ARCADE] arcade_cabinet.glb is missing — skipped (" + path + ")");
                return;
            }
            string artDir = System.IO.Path.Combine(dir ?? string.Empty, "arcade_art");
            // v0.10.36: eleven cabinets — the five Dinozavrik machines on the right of the
            // row, plus two Arkanoid, two Pong and two Snake cabinets extended to the left.
            string[] artFiles =
            {
                "arcade_06.png", "arcade_07.png",   // Arkanoid A/B
                "arcade_08.png", "arcade_09.png",   // Pong A/B
                "arcade_10.png", "arcade_11.png",   // Snake A/B
                "arcade_01.png", "arcade_02.png", "arcade_03.png", "arcade_04.png", "arcade_05.png",
                "arcade_12.png", "arcade_13.png", "arcade_14.png"   // duel Pong / Arkanoid / Snake
            };
            int[] kinds = { 1, 1, 2, 2, 3, 3, 0, 0, 0, 0, 0, 2, 1, 3 };
            bool[] duels = { false, false, false, false, false, false, false, false, false, false, false,
                             true, true, true };
            string[] posters =
            {
                "arcade_poster_06.png", "arcade_poster_07.png",
                "arcade_poster_08.png", "arcade_poster_09.png",
                "arcade_poster_10.png", "arcade_poster_11.png",
                "arcade_poster.png", "arcade_poster.png", "arcade_poster.png",
                "arcade_poster.png", "arcade_poster.png",
                "arcade_poster_12.png", "arcade_poster_13.png", "arcade_poster_14.png"
            };
            string[] kindNames = { "Dinozavrik", "Arkanoid", "Pong", "Zmejka" };
            float[] xs =
            {
                -46.5f, -44.5f, -42.5f, -40.5f, -38.5f, -36.5f,
                -34.5f, -32.5f, -30.5f, -28.5f, -26.5f,
                -40.5f, -36.5f, -32.5f
            };
            float[] zs =
            {
                -12.5f, -12.5f, -12.5f, -12.5f, -12.5f, -12.5f,
                -12.5f, -12.5f, -12.5f, -12.5f, -12.5f,
                18.5f, 18.5f, 18.5f
            };
            float[] yaws =
            {
                0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f,
                180f, 180f, 180f
            };
            const float floorY = 102.55f;      // the annex's top hall
            int made = 0;
            for (int i = 0; i < artFiles.Length; i++)
            {
                try
                {
                    var go = GlbFigureBuilder.Load(path, new Vector3(xs[i], floorY, zs[i]), yaws[i],
                                                   house, Shader.Find("Standard"), log,
                                                   $"arcade_cabinet_{i + 1}", "PROP", false, true);
                    // v0.10.20: the cabinets are 60 % larger (1,80 m -> 2,88 m); the origin is
                    // at the feet/back, so they grow upwards and into the room, not into the wall.
                    go.transform.localScale = Vector3.one * 1.6f;
                    ArcadeCabinetList.Add(new ArcadeCabinetInfo
                    {
                        Root = go,
                        Id = i + 1,
                        Kind = kinds[i],
                        Duel = duels[i],
                        PosterFile = posters[i]
                    });
                    var mr = go.GetComponentInChildren<MeshRenderer>();
                    var tex = LoadArcadeArt(System.IO.Path.Combine(artDir, artFiles[i]));
                    string applied = "placeholder";
                    if (mr != null && tex != null && mr.sharedMaterials != null &&
                        mr.sharedMaterials.Length >= 2 && mr.sharedMaterials[1] != null)
                    {
                        mr.sharedMaterials[1].mainTexture = tex;
                        applied = tex.name;
                    }
                    else if (tex == null)
                        log.LogWarning($"[ARCADE] cabinet {i + 1}: side art " +
                                       $"{artFiles[i]} is missing — placeholder stays");
                    // v0.10.26: the body/front/top no longer stay worn yellow. The yellow paint
                    // texels of this cabinet's own body atlas are hue-shifted to the dominant
                    // colour of its side art, preserving every texel's luminance — rust, scratches
                    // and the dark panels/lettering are untouched.
                    Color bodyTheme = new Color(0.78f, 0.68f, 0.12f);
                    if (tex != null) bodyTheme = ArcadeDominantColor(tex);
                    int changed = 0;
                    long recolorMs = 0;
                    if (mr != null && mr.sharedMaterials != null && mr.sharedMaterials.Length >= 1 &&
                        mr.sharedMaterials[0] != null)
                    {
                        var bodyTex = mr.sharedMaterials[0].mainTexture as Texture2D;
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        changed = RecolorCabinetBody(bodyTex, bodyTheme);
                        sw.Stop();
                        recolorMs = sw.ElapsedMilliseconds;
                    }
                    log.LogInfo($"[ARCADE] cabinet {i + 1}: body yellow -> " +
                                $"#{ColorUtility.ToHtmlStringRGB(bodyTheme)} (dominant of " +
                                $"'{artFiles[i]}'), {changed} body texels recoloured in " +
                                $"{recolorMs} ms; rust/orange and dark panels left untouched");
                    made++;
                    log.LogInfo($"[ARCADE] cabinet {i + 1}: at house-local " +
                                $"({xs[i]:0.0}, {floorY:0.0}, {zs[i]:0.0}) yaw {yaws[i]:0}, " +
                                $"back 1,03 m off the wall, 1,80 m x1,6 = 2,88 m tall, " +
                                $"{kindNames[kinds[i]]}{(duels[i] ? " (duel)" : "")}, " +
                                $"side art '{applied}' mirrored on the other side");
                }
                catch (Exception e)
                {
                    log.LogWarning($"[ARCADE] cabinet {i + 1} failed: {e.Message}");
                }
            }
            log.LogInfo($"[ARCADE] {made} of {artFiles.Length} cabinets: eleven along the top hall's " +
                        $"back wall (2 m apart, fronts facing the room) plus three duel cabinets at " +
                        $"the opposite wall (z 18,5, yaw 180, fronts facing the room), 1,6x size " +
                        $"(2,88 m tall); five Dinozavrik + two each of Arkanoid/Pong/Zmejka, plus one " +
                        $"duel cabinet per game, each with its own Stable-Diffusion side skin, " +
                        $"marquee poster and body colour");
        }

        private static Texture2D LoadArcadeArt(string path)
        {
            if (!System.IO.File.Exists(path)) return null;
            try
            {
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (!t.LoadImage(System.IO.File.ReadAllBytes(path))) return null;
                t.name = System.IO.Path.GetFileNameWithoutExtension(path);
                t.wrapMode = TextureWrapMode.Clamp;
                t.filterMode = FilterMode.Trilinear;
                t.anisoLevel = 2;
                return t;
            }
            catch (Exception e)
            {
                log.LogWarning("[ARCADE] side art failed: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// v0.10.26: the dominant hue of a side-art image. Pixels are weighted by saturation
        /// (dark space and neutral highlights drop out) and the winning 15-degree hue bin's mean
        /// RGB is returned, lifted so a dark bin is still usable as paint.
        /// </summary>
        private static Color ArcadeDominantColor(Texture2D art)
        {
            if (art == null) return new Color(0.78f, 0.68f, 0.12f);
            Color32[] px = art.GetPixels32();
            int w = art.width, h = art.height;
            const int bins = 24;
            var weight = new float[bins];
            var sumR = new float[bins];
            var sumG = new float[bins];
            var sumB = new float[bins];
            int step = Mathf.Max(1, w / 160);
            for (int y = 0; y < h; y += step)
            {
                int row = y * w;
                for (int x = 0; x < w; x += step)
                {
                    Color32 p = px[row + x];
                    float r = p.r / 255f, g = p.g / 255f, b = p.b / 255f;
                    float mx = Mathf.Max(r, Mathf.Max(g, b));
                    float mn = Mathf.Min(r, Mathf.Min(g, b));
                    float delta = mx - mn;
                    float v = mx;
                    float s = mx > 0f ? delta / mx : 0f;
                    if (s < 0.25f || v < 0.15f) continue;
                    float hue;
                    if (delta <= 0.00001f) hue = 0f;
                    else if (mx == r) hue = ((g - b) / delta) % 6f;
                    else if (mx == g) hue = 2f + (b - r) / delta;
                    else hue = 4f + (r - g) / delta;
                    hue /= 6f;
                    if (hue < 0f) hue += 1f;
                    int bin = Mathf.Clamp((int)(hue * bins), 0, bins - 1);
                    weight[bin] += s;
                    sumR[bin] += r * s;
                    sumG[bin] += g * s;
                    sumB[bin] += b * s;
                }
            }
            int best = 0;
            for (int i = 1; i < bins; i++)
                if (weight[i] > weight[best]) best = i;
            if (weight[best] <= 0.0001f) return new Color(0.78f, 0.68f, 0.12f);
            var c = new Color(sumR[best] / weight[best], sumG[best] / weight[best],
                              sumB[best] / weight[best], 1f);
            // A dark mean (the dragon's deep red) must still read as paint on a cabinet.
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (max < 0.62f && max > 0.0001f) c *= 0.62f / max;
            // Keep a little of the cabinet's weathered look: a touch darker and less acid.
            float lum = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
            c = Color.Lerp(c, new Color(lum, lum, lum, 1f), 0.08f) * 0.94f;
            c.a = 1f;
            return c;
        }

        /// <summary>
        /// v0.10.26: recolour only the worn-yellow paint of a cabinet body atlas. Every such texel
        /// keeps its own value/saturation variation (rust specks, scratches, dirt) while its hue
        /// becomes the side art's dominant hue; orange rust (hue below ~34 deg) and every dark,
        /// green or neutral panel are skipped. Returns the number of changed texels.
        /// </summary>
        private static int RecolorCabinetBody(Texture2D body, Color theme)
        {
            if (body == null) return 0;
            Color.RGBToHSV(theme, out float th, out float ts, out float tv);
            Color32[] px = body.GetPixels32();
            int changed = 0;
            for (int i = 0; i < px.Length; i++)
            {
                Color32 p = px[i];
                if (p.a < 16) continue;
                float r = p.r / 255f, g = p.g / 255f, b = p.b / 255f;
                float mx = Mathf.Max(r, Mathf.Max(g, b));
                float mn = Mathf.Min(r, Mathf.Min(g, b));
                float delta = mx - mn;
                float v = mx;
                float s = mx > 0f ? delta / mx : 0f;
                if (s < 0.20f || v < 0.10f) continue;
                float hue;
                if (delta <= 0.00001f) continue;
                else if (mx == r) hue = ((g - b) / delta) % 6f;
                else if (mx == g) hue = 2f + (b - r) / delta;
                else hue = 4f + (r - g) / delta;
                hue /= 6f;
                if (hue < 0f) hue += 1f;
                // the worn yellow paint; orange rust (h < 0.095) stays rust
                if (hue < 0.095f || hue > 0.215f) continue;
                float ns = Mathf.Lerp(s, ts, 0.70f);
                float nv = Mathf.Clamp01(v * (0.85f + 0.20f * tv));
                Color nc = Color.HSVToRGB(th, ns, nv);
                px[i] = new Color32((byte)(nc.r * 255f + 0.5f), (byte)(nc.g * 255f + 0.5f),
                                    (byte)(nc.b * 255f + 0.5f), p.a);
                changed++;
            }
            if (changed == 0) return 0;
            body.SetPixels32(px);
            body.Apply(true, false);
            return changed;
        }

        private static Texture2D LoadAirHockeyPattern(string path)
        {
            var bytes = System.IO.File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes))
                throw new InvalidOperationException("could not decode " + path);
            tex.name = "CoopArcadePattern_" + System.IO.Path.GetFileNameWithoutExtension(path);
            // One mirrored cycle around the perimeter: u 0..2, Mirror at the wrap, so the
            // start and end of the band both sample the left edge — no seam.
            tex.wrapMode = TextureWrapMode.Mirror;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 4;
            tex.Apply(true, false);
            return tex;
        }

        private static Material MakeAirHockeyPatternMaterial(Texture2D tex, Color theme)
        {
            var sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Unlit/Texture");
            var m = new Material(sh) { name = "CoopArcadePattern_" + tex.name };
            m.mainTexture = tex;
            m.color = Color.white;
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.06f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.38f);
            if (m.HasProperty("_EmissionMap") && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", tex);
                m.SetColor("_EmissionColor", Color.Lerp(theme, Color.white, 0.30f) * 0.22f);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return m;
        }

        private static Material MakeArcadeMetal(Color c, float emission)
        {
            var sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            var m = new Material(sh) { name = "CoopArcadeMetal" };
            m.color = c;
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.38f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.58f);
            if (emission > 0.001f && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return m;
        }

        private static Material MakeArcadeLamp(Color accent)
        {
            var sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            var c = Color.Lerp(accent, Color.white, 0.12f);
            var m = new Material(sh) { name = "CoopArcadeLamp", color = c };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.70f);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * 1.8f);   // HDR, so the game's bloom picks it up
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return m;
        }

        /// <summary>
        /// v0.10.13 — per-table field texture. The original table atlas is cloned and its red
        /// markings are replaced by the arch canopy colour; its blue/teal markings by the lamp
        /// colour. The bone/white background, shading and anti-aliasing are kept; the blend strength
        /// follows the original pixel's red/blue dominance, so the edge pixels fade naturally.
        /// </summary>
        private static Material MakeAirHockeyFieldMaterial(GameObject table, Color arcColor,
                                                           Color lampColor, int index)
        {
            var mr = table.GetComponentInChildren<MeshRenderer>();
            var baseMat = mr != null ? mr.sharedMaterial : null;
            var srcTex = baseMat != null ? baseMat.mainTexture as Texture2D : null;
            if (srcTex == null)
            {
                log.LogWarning($"[PROP] arcade variant {index}: field texture is missing — " +
                               "keeping the original red/blue field");
                return null;
            }

            Color32[] src;
            try { src = srcTex.GetPixels32(); }
            catch (Exception e)
            {
                log.LogWarning($"[PROP] arcade variant {index}: field texture is not readable " +
                               $"({e.Message}) — keeping the original field");
                return null;
            }

            var dst = new Color32[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                Color c = src[i];
                float r = c.r, g = c.g, b = c.b;
                bool red = r > 0.30f && r > g * 1.20f && r > b * 1.15f;
                bool blue = b > 0.20f && b > r * 1.05f && g > r * 0.95f;
                if (!red && !blue)
                {
                    dst[i] = src[i];
                    continue;
                }

                Color target;
                float strength;
                if (red)
                {
                    target = arcColor;
                    strength = Mathf.Clamp01((r - Mathf.Max(g, b)) * 4f);
                }
                else
                {
                    target = lampColor;
                    // The original lines are teal, so use the max of blue/green over red;
                    // the old blue-only difference could be negative for a green-ish line.
                    strength = Mathf.Clamp01((Mathf.Max(b, g) - r) * 3f);
                }
                Color outc = Color.Lerp(c, target, strength);
                outc.a = c.a;
                dst[i] = outc;
            }

            var tex = new Texture2D(srcTex.width, srcTex.height, TextureFormat.RGBA32, true)
            {
                name = $"CoopArcadeField_{index}",
                wrapMode = srcTex.wrapMode,
                filterMode = srcTex.filterMode,
                anisoLevel = srcTex.anisoLevel
            };
            tex.SetPixels32(dst);
            tex.Apply(true, false);

            var sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Unlit/Texture");
            var m = new Material(sh) { name = $"CoopArcadeFieldMat_{index}", color = Color.white };
            m.mainTexture = tex;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.22f);
            return m;
        }

        /// <summary>
        /// v0.10.12 — replaces the wide white horizontal rim on a showroom table with a raised
        /// bumper: a closed-looking ring from the rink surface up to 5 cm above the old table top.
        /// The whole visible ring (top, outer wall, inner wall) uses the exact same material as the
        /// arch canopy, so the "white" border becomes the table's theme colour.
        /// </summary>
        private static void AddRaisedArcadeBumper(GameObject table, Material theme)
        {
            const float ox = 2.576f, oz = 1.943f;      // old rim outer edge
            const float ix = 2.298f, iz = 1.395f;      // rink edge, 2 mm inside the old white lip
            const float oc = 0.18f, ic = 0.12f;        // corner chamfers
            float yOuter = AirHockeyBodyTop;            // 1.1408, old table top
            float yInner = AirHockeyRinkTop;            // 1.104, playfield
            float yTop = AirHockeyBodyTop + AirHockeyBumperRaise;   // 1.1908

            Func<float, float, float, List<Vector2>> ring = (x, z, c) =>
            {
                return new List<Vector2>(8)
                {
                    new Vector2(-x + c, -z),
                    new Vector2( x - c, -z),
                    new Vector2( x, -z + c),
                    new Vector2( x,  z - c),
                    new Vector2( x - c,  z),
                    new Vector2(-x + c,  z),
                    new Vector2(-x,  z - c),
                    new Vector2(-x, -z + c),
                };
            };
            var op = ring(ox, oz, oc);
            var ip = ring(ix, iz, ic);
            // v0.10.13: the raised top is 3 cm narrower than the old table edge (the user asked
            // to remove the protruding edges by 3 cm); the outer wall leans in to meet it.
            var tp = ring(ox - 0.03f, oz - 0.03f, oc);
            var g = new Geo();

            for (int i = 0; i < 8; i++)
            {
                int j = (i + 1) % 8;
                Vector2 d = op[j] - op[i];
                if (d.sqrMagnitude < 0.000001f) continue;
                Vector3 outward = Vector3.Cross(Vector3.up,
                                                new Vector3(d.x, 0f, d.y)).normalized;

                Vector3 ob0 = new Vector3(op[i].x, yOuter, op[i].y);
                Vector3 ob1 = new Vector3(op[j].x, yOuter, op[j].y);
                Vector3 ot0 = new Vector3(tp[i].x, yTop, tp[i].y);
                Vector3 ot1 = new Vector3(tp[j].x, yTop, tp[j].y);
                Vector3 ib0 = new Vector3(ip[i].x, yInner, ip[i].y);
                Vector3 ib1 = new Vector3(ip[j].x, yInner, ip[j].y);
                Vector3 it0 = new Vector3(ip[i].x, yTop, ip[i].y);
                Vector3 it1 = new Vector3(ip[j].x, yTop, ip[j].y);

                Vector2 u0 = Vector2.zero, u1 = Vector2.right, u2 = Vector2.up, u3 = Vector2.one;

                // outer wall, leaning 3 cm inward over its 5 cm height
                Vector3 wallN = Vector3.Cross(ot0 - ob0, ob1 - ob0);
                if (wallN.sqrMagnitude < 0.000001f) wallN = outward;
                wallN.Normalize();
                if (Vector3.Dot(wallN, outward) < 0f) wallN = -wallN;
                g.Quad(0, ob0, ob1, ot1, ot0, wallN, u0, u1, u2, u3);
                // inner wall down to the rink, covering the old white lip
                g.Quad(0, ib1, ib0, it0, it1, -outward, u1, u0, u3, u2);
                // the wide horizontal top of the bumper, now 3 cm narrower
                g.Quad(0, ot0, ot1, it1, it0, Vector3.up, u0, u1, u2, u3);
            }

            var go = new GameObject("airhockey_raised_bumper");
            go.transform.SetParent(table.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            g.Apply(go, theme);
        }

        /// <summary>
        /// v0.10.9 — the pattern is applied to the table's **own vertical side triangles**, not a
        /// separate ring. The side triangles are removed from the body mesh (the rink top, the rim
        /// top and the black legs keep the original material/UVs) and rebuilt as a second mesh
        /// exactly coplanar with them. The UV is a cylindrical wrap around the table (u 0..2,
        /// mirrored texture), v from the body's lower edge 0.405 m to the rim 1.141 m. There is no
        /// offset, so there is no gap between the pattern and the table.
        /// </summary>
        private static void RetextureAirHockeySides(GameObject table, Material pattern, Material field)
        {
            var mf = table.GetComponentInChildren<MeshFilter>();
            var mr = table.GetComponentInChildren<MeshRenderer>();
            if (mf == null || mr == null || mf.sharedMesh == null) return;

            var src = mf.sharedMesh;
            var verts = src.vertices;
            var norms = src.normals;
            if (norms == null || norms.Length != verts.Length) norms = new Vector3[verts.Length];
            var tris = src.triangles;
            var body = new List<int>(tris.Length);
            var fieldTris = new List<int>(256);
            var sideV = new List<Vector3>(tris.Length);
            var sideN = new List<Vector3>(tris.Length);
            var sideUv = new List<Vector2>(tris.Length);
            var sideT = new List<int>(tris.Length);
            const float y0 = 0.405f;
            const float y1 = 1.141f;
            const float TAU = Mathf.PI * 2f;
            float seam = Mathf.Atan2(-1.336f, 2.576f);   // the front-right corner

            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                Vector3 va = verts[a], vb = verts[b], vc = verts[c];
                Vector3 n = Vector3.Cross(vb - va, vc - va);
                if (n.sqrMagnitude < 1e-10f)
                {
                    body.Add(a); body.Add(b); body.Add(c);
                    continue;
                }
                n.Normalize();
                float cy = (va.y + vb.y + vc.y) / 3f;
                // v0.10.13: the bone rink top is a horizontal surface at y ≈ 1.104; it goes
                // into its own mesh so the per-table red/blue recolour can be applied.
                bool isField = field != null && cy > 1.100f && cy < 1.120f && n.y > 0.60f;
                if (isField)
                {
                    fieldTris.Add(a); fieldTris.Add(b); fieldTris.Add(c);
                    continue;
                }
                bool isSide = cy >= y0 && cy <= y1 + 0.002f && Mathf.Abs(n.y) <= 0.60f;
                if (!isSide)
                {
                    body.Add(a); body.Add(b); body.Add(c);
                    continue;
                }

                // Per-triangle angular unwrap around the seam corner; u 0..2 with the texture
                // mirrored once, so the wrap is invisible.
                float da = Mathf.Repeat(Mathf.Atan2(va.z, va.x) - seam, TAU);
                float db = Mathf.Repeat(Mathf.Atan2(vb.z, vb.x) - seam, TAU);
                float dc = Mathf.Repeat(Mathf.Atan2(vc.z, vc.x) - seam, TAU);
                float mn = Mathf.Min(da, Mathf.Min(db, dc));
                float mx = Mathf.Max(da, Mathf.Max(db, dc));
                if (mx - mn > Mathf.PI)
                {
                    if (da < Mathf.PI) da += TAU;
                    if (db < Mathf.PI) db += TAU;
                    if (dc < Mathf.PI) dc += TAU;
                }

                int baseIdx = sideV.Count;
                AddAirHockeySideVertex(sideV, sideN, sideUv, va, norms[a], da, y0, y1, TAU);
                AddAirHockeySideVertex(sideV, sideN, sideUv, vb, norms[b], db, y0, y1, TAU);
                AddAirHockeySideVertex(sideV, sideN, sideUv, vc, norms[c], dc, y0, y1, TAU);
                sideT.Add(baseIdx); sideT.Add(baseIdx + 1); sideT.Add(baseIdx + 2);
            }

            // The body keeps its original material (rink top, rim top, black legs, underside).
            var bodyMesh = UnityEngine.Object.Instantiate(src);
            bodyMesh.name = src.name + "_body";
            bodyMesh.SetTriangles(body, 0);
            bodyMesh.RecalculateBounds();
            mf.sharedMesh = bodyMesh;

            // The patterned skin is exactly where the removed triangles were: zero gap.
            var sideMesh = new Mesh { name = src.name + "_pattern_sides" };
            sideMesh.SetVertices(sideV);
            sideMesh.SetNormals(sideN);
            sideMesh.SetUVs(0, sideUv);
            sideMesh.SetTriangles(sideT, 0);
            sideMesh.RecalculateBounds();
            var sideGo = new GameObject("airhockey_pattern_sides");
            sideGo.transform.SetParent(table.transform, false);
            sideGo.transform.localPosition = Vector3.zero;
            sideGo.transform.localRotation = Quaternion.identity;
            sideGo.transform.localScale = Vector3.one;
            sideGo.AddComponent<MeshFilter>().sharedMesh = sideMesh;
            var sideMr = sideGo.AddComponent<MeshRenderer>();
            sideMr.sharedMaterial = pattern;

            // v0.10.13: the rink top is exactly where the removed field triangles were; its
            // material is the per-table recoloured clone of the source atlas.
            if (field != null && fieldTris.Count > 0)
            {
                var fieldMesh = UnityEngine.Object.Instantiate(src);
                fieldMesh.name = src.name + "_field";
                fieldMesh.SetTriangles(fieldTris, 0);
                fieldMesh.RecalculateBounds();
                var fieldGo = new GameObject("airhockey_field");
                fieldGo.transform.SetParent(table.transform, false);
                fieldGo.transform.localPosition = Vector3.zero;
                fieldGo.transform.localRotation = Quaternion.identity;
                fieldGo.transform.localScale = Vector3.one;
                fieldGo.AddComponent<MeshFilter>().sharedMesh = fieldMesh;
                var fieldMr = fieldGo.AddComponent<MeshRenderer>();
                fieldMr.sharedMaterial = field;
            }
        }

        private static void AddAirHockeySideVertex(List<Vector3> v, List<Vector3> n, List<Vector2> uv,
                                                   Vector3 p, Vector3 normal, float d,
                                                   float y0, float y1, float tau)
        {
            v.Add(p);
            n.Add(normal);
            uv.Add(new Vector2(Mathf.Clamp(d / tau * 2f, 0f, 2f),
                               Mathf.Clamp01((p.y - y0) / (y1 - y0))));
        }

        /// <summary>
        /// v0.10.8 legacy: the first version used a separate closed ring around the body. It could
        /// leave a gap because the table's real sides are not a plain 5,15 x 3,89 rectangle (the
        /// lower body is z ±1,336, the rim flares to ±1,943). v0.10.9 does not call this; the six
        /// showroom tables use RetextureAirHockeySides instead.
        /// </summary>
        private static void AddAirHockeyPatternBand(GameObject table, Material pattern, Material edge)
        {
            const float hx = 5.15f * 0.5f;
            const float hz = 3.89f * 0.5f;
            const float chamfer = 0.18f;
            const float y0 = 0.30f;
            const float y1 = 1.10f;
            const float outset = 0.012f; // outside the body: no z-fighting
            const float ins = 0.012f;    // inside the body: the band is a closed ring
            float ox = hx + outset, oz = hz + outset;
            float ix = hx - ins, iz = hz - ins;

            Func<float, float, List<Vector2>> ring = (x, z) =>
            {
                var l = new List<Vector2>(8)
                {
                    new Vector2(-x + chamfer, -z),
                    new Vector2( x - chamfer, -z),
                    new Vector2( x, -z + chamfer),
                    new Vector2( x,  z - chamfer),
                    new Vector2( x - chamfer,  z),
                    new Vector2(-x + chamfer,  z),
                    new Vector2(-x,  z - chamfer),
                    new Vector2(-x, -z + chamfer),
                };
                return l;
            };
            var op = ring(ox, oz);
            var ip = ring(ix, iz);
            float total = 0f;
            for (int i = 0; i < 8; i++)
            {
                int j = (i + 1) % 8;
                total += (op[j] - op[i]).magnitude;
            }

            var g = new Geo();
            float acc = 0f;
            for (int i = 0; i < 8; i++)
            {
                int j = (i + 1) % 8;
                Vector2 d = op[j] - op[i];
                float len = d.magnitude;
                if (len < 0.0001f) continue;
                Vector3 outward = Vector3.Cross(Vector3.up,
                                                new Vector3(d.x, 0f, d.y)).normalized;
                float u0 = acc / total * 2f;
                float u1 = (acc + len) / total * 2f;
                acc += len;

                Vector3 ob0 = new Vector3(op[i].x, y0, op[i].y);
                Vector3 ob1 = new Vector3(op[j].x, y0, op[j].y);
                Vector3 ot0 = new Vector3(op[i].x, y1, op[i].y);
                Vector3 ot1 = new Vector3(op[j].x, y1, op[j].y);
                Vector3 ib0 = new Vector3(ip[i].x, y0, ip[i].y);
                Vector3 ib1 = new Vector3(ip[j].x, y0, ip[j].y);
                Vector3 it0 = new Vector3(ip[i].x, y1, ip[i].y);
                Vector3 it1 = new Vector3(ip[j].x, y1, ip[j].y);

                // outer skin: the pattern
                g.Quad(0, ob0, ob1, ot1, ot0, outward,
                       new Vector2(u0, 0f), new Vector2(u1, 0f),
                       new Vector2(u1, 1f), new Vector2(u0, 1f));
                // inner skin and the thin top/bottom caps: dark metal
                g.Quad(1, ib1, ib0, it0, it1, -outward,
                       new Vector2(u1, 0f), new Vector2(u0, 0f),
                       new Vector2(u0, 1f), new Vector2(u1, 1f));
                g.Quad(1, ot0, ot1, it1, it0, Vector3.up,
                       new Vector2(0f, 0f), new Vector2(1f, 0f),
                       new Vector2(1f, 1f), new Vector2(0f, 1f));
                g.Quad(1, ob1, ob0, ib0, ib1, Vector3.down,
                       new Vector2(0f, 0f), new Vector2(1f, 0f),
                       new Vector2(1f, 1f), new Vector2(0f, 1f));
            }

            var band = new GameObject("airhockey_pattern_band");
            band.transform.SetParent(table.transform, false);
            band.transform.localPosition = Vector3.zero;
            band.transform.localRotation = Quaternion.identity;
            band.transform.localScale = Vector3.one;
            g.Apply(band, pattern, edge);
        }

        /// <summary>
        /// The arch is one mesh. Split it at y = 2.40 m: below are the two supports (dark tinted
        /// metal), above is the canopy (the table's dominant hue with a gentle emission). A bright
        /// accent LED ring under the canopy and a matching point light turn the original yellow
        /// lamp strip into the table's own colour.
        /// </summary>
        private static void PaintAirHockeyArch(GameObject arch, Color theme, Color accent, int index,
                                               Material canopyMat)
        {
            var mf = arch.GetComponentInChildren<MeshFilter>();
            var mr = arch.GetComponentInChildren<MeshRenderer>();
            if (mf == null || mr == null || mf.sharedMesh == null) return;

            var src = mf.sharedMesh;
            var mesh = UnityEngine.Object.Instantiate(src);
            mesh.name = src.name + "_themed_" + index;
            var verts = mesh.vertices;
            var srcTris = src.triangles;
            var supports = new List<int>(srcTris.Length);
            var canopy = new List<int>(srcTris.Length);
            var underside = new List<int>(srcTris.Length);
            for (int i = 0; i + 2 < srcTris.Length; i += 3)
            {
                int a = srcTris[i], b = srcTris[i + 1], c = srcTris[i + 2];
                float cy = (verts[a].y + verts[b].y + verts[c].y) / 3f;
                float minX = Mathf.Min(verts[a].x, Mathf.Min(verts[b].x, verts[c].x));
                float maxX = Mathf.Max(verts[a].x, Mathf.Max(verts[b].x, verts[c].x));
                // The two support rods are the only extremely thin strips in x (the arch is
                // 3,31 m wide); they run from the table up to the canopy at z ±1,88.
                bool rod = (maxX - minX) < 0.15f && Mathf.Abs((minX + maxX) * 0.5f) < 0.15f;
                if (rod)
                {
                    supports.Add(a); supports.Add(b); supports.Add(c);
                }
                else
                {
                    Vector3 n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]).normalized;
                    if (cy > 2.40f && n.y < -0.25f)
                    {
                        underside.Add(a); underside.Add(b); underside.Add(c);
                    }
                    else
                    {
                        canopy.Add(a); canopy.Add(b); canopy.Add(c);
                    }
                }
            }
            mesh.subMeshCount = 3;
            mesh.SetTriangles(supports, 0);
            mesh.SetTriangles(canopy, 1);
            mesh.SetTriangles(underside, 2);
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
            // The two rods take the pattern's accent, the canopy its dominant hue, and the
            // underside a deep tint of the same hue so the arch reads as painted from below too.
            mr.sharedMaterials = new[]
            {
                MakeArcadeMetal(Color.Lerp(accent, Color.black, 0.52f), 0.05f),
                canopyMat,
                MakeArcadeMetal(Color.Lerp(theme, Color.black, 0.68f), 0.04f)
            };

            // A thin LED strip on the canopy's front and back edge — the original yellow lamp
            // line, now in the table's accent colour. The canopy board is only 0,86 m deep
            // (z ±0,43), so the strip belongs on that edge, not out at the support rods.
            var lamp = MakeArcadeLamp(accent);
            AddArcadeLampBox(arch.transform, new Vector3(0f, 2.80f, -0.445f),
                             new Vector3(3.10f, 0.05f, 0.05f), lamp);
            AddArcadeLampBox(arch.transform, new Vector3(0f, 2.80f, 0.445f),
                             new Vector3(3.10f, 0.05f, 0.05f), lamp);

            var lightGo = new GameObject("airhockey_arch_light");
            lightGo.transform.SetParent(arch.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 2.25f, 0f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = Color.Lerp(accent, Color.white, 0.25f);
            l.intensity = 1.25f;
            l.range = 8f;
            l.shadows = LightShadows.None;
        }

        private static void AddArcadeLampBox(Transform parent, Vector3 localPos, Vector3 size, Material mat)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "airhockey_arch_led";
            var col = box.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPos;
            box.transform.localRotation = Quaternion.identity;
            box.transform.localScale = size;
            var r = box.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
        }
    }
}
