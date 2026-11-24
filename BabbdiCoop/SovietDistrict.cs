using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace BabbdiCoop
{
    /// <summary>
    /// A Soviet brutalist district generated at level load from the game's own materials.
    ///
    /// BABBDI's city sits in a drained concrete basin; the sea (y = 71.6) is held back by the dam
    /// that runs diagonally from about (-20, 600) to (220, 280) вЂ” that is the wall the player
    /// stands on in the screenshot, with the empty sea to its right. This builder extends the dam
    /// crest seaward: a new deck is hung off the crest, enclosed by a new dam wall that continues
    /// the old one, and the district is laid out on it. Everything is built in a local frame
    /// (x along the dam, z seaward, y up from the deck) and placed as one root object.
    ///
    /// The blocks are real shells: walls with window openings, floor slabs, a central corridor,
    /// rooms and a switchback staircase, so the player can walk into any house, up to the roof and
    /// across the covered bridges вЂ” like the interiors of the original city.
    /// </summary>
    internal static class SovietDistrict
    {
        public static bool Enabled;
        public static float Density = 1f;
        public static bool Built;
        public static Vector3 Center;

        // dam crest line measured by the surface probe (land -> sea boundary)
        private static readonly Vector2 DamA = new Vector2(-20f, 600f);
        private static readonly Vector2 DamB = new Vector2(220f, 280f);
        private const float DeckY = 85.5f;   // crest top on the probe grid
        private const float Len = 380f;      // along the dam (the plan widens the platform ~1.4x)
        private const float Wid = 300f;      // v0.9.8: seaward вЂ” +80 m, the new contour from the user's plan B
        private const float Axw = 78f;       // western wing: how far it reaches past the platform
        private const float Az0 = 155f;      // western wing: its inland edge (the wing is 78 x 145 m)
        private const float ShiftX = -15f;   // v0.9.8: the whole platform is moved 15 m along the dam, off the
                                            // round hole in the sea that the east edge used to sit on

        // block dimensions (identical for every house so that floors line up for the bridges)
        private const float T = 0.94f;        // exterior wall thickness (0.52 -> +80 %)
        private const float PT = 0.38f;       // interior partition thickness (0.21 -> +80 %)
        private const float FH = 3.2f;        // floor height
        // v0.9.11: the stalinka got storeys +75 % (5.6 m ceilings) and door openings +30 % (2.73-2.86 m
        // clear, 3.25 m at the entrances). The taller storey also changes the stair: the switchback turns
        // about z instead of x, so the flights rise in half-storeys. v0.9.16: the flights of every
        // building are 2-3 m long and the slab shaft is cut from the same plan (see PlanStair/StairsZ).
        // v0.9.12: the user asked for exactly these proportions on **every** block, so the multipliers
        // are no longer a stalinka speciality.
        private const float TallFh = 1.75f;   // ceiling height multiplier, every block
        private const float TallDoor = 1.30f; // door opening height multiplier, every block
        private const float WinClear = 0.90f; // no window closer than this to an interior partition
        private const float BASE = 0.15f;     // floor of the ground storey: on the deck, no gap under it
        private const float CORR = 2.2f;      // corridor clear width
        private const float SLAB = 0.78f;     // v0.9.43: floor plates 3x the old 0,26 m, growing **downwards**
                                              // (the user: "СѓРІРµР»РёС‡СЊ РµС‘ С‚РѕР»С‰РёРЅСѓ РІРЅРёР· РІ 3 СЂР°Р·Р°" вЂ” the plate's
                                              //  end face over the stair shaft is the one they circled)
        // the stair plates (2,5x the *old* floor slab, see v0.9.38) вЂ” deliberately independent of SLAB,
        // so thickening the floor plates does not inflate the flights/landings as well
        private const float StairPlate = 0.65f;
        private const float Grow = 1.30f;     // v0.9.3: every block is +30 % in all three dimensions
        private const int BridgeFloor = 10;   // v0.9.3: every covered bridge crosses at the 11th storey

        private static ManualLogSource log;
        private static Transform root;
        private static System.Random rng;

        private static Material mPanel, mRough, mSlabs, mLeaks, mDark, mPainted, mMossy, mMetal, mGrass, mDirt;
        private static Texture2D mGrain;   // high-passed, desaturated, lifted copy of the concrete photo
        private static Material mGlow;     // v0.9.14: the memorial's red core (unlit gradient)
        private static Material mWarmGlow; // v0.9.72: the House's warm lamp lenses / the star

        /// <summary>[District] Memorial вЂ” the red-glowing monument on the 2nd-tallest block's roof.</summary>
        public static bool MemorialOn = true;

        /// <summary>
        /// [District] MemorialGlow вЂ” brightness of the memorial's red (v0.9.15). It scales **both** the
        /// emissive boost of the core and every red point light, so one number halves the whole glow.
        /// 1 = the v0.9.14 level, 0.5 = half (the default since v0.9.15).
        /// </summary>
        public static float MemorialGlow = 0.5f;

        /// <summary>
        /// [District] Penthouse вЂ” v0.9.18: the clock tower on the roof of the outlying block on the
        /// western wing (the building on the peninsula in the user's screenshot). Off = a plain roof.
        /// </summary>
        public static bool PenthouseOn = true;

        private static Bldg pentBlock;     // the block that wears it, for the preview poses
        private static Material mClock;    // the giant clock dials (procedural texture)
        private static Material mGlass;    // the lantern's and the lift's glass panes

        private static Bldg memBlock;      // chosen before the build loop, so Roof() can skip the vent box
        private static Bldg heliBlock;     // v0.9.79: the skyscraper roof that carries the Mi-26
        // v0.9.87: the machine got too big for a roof — it stands on the dam's crest instead,
        // 50 m west of the district, nose along the dam (district-local metres).
        private static readonly Vector3 HeliSpot = new Vector3(-70f, 0.35f, -8f);

        // v0.9.98: the air-hockey table on the House of Soviets' **top floor** — the upper hall of the
        // cantilevered chevron annex, house-local y = 102.55 (above both tower top floors). District-local
        // feet/height are kept for the probes, so "it stands on the floor" is measured, not assumed.
        private static Vector3 airHockeyFeet;
        private static float airHockeyHeight;
        private static bool airHockeyPlaced;

        /// <summary>v0.10.0: the loaded table, for the two-player mini-game (AirHockeyGame).</summary>
        public static GameObject AirHockeyTableGo;
        public static GameObject AirHockeyArchGo;   // the scoreboard arch, hidden while playing
        public static Transform AirHockeyTableRoot;

        // v0.10.14: every showroom table is playable. The mini-game picks the nearest entry;
        // the ids are stable across peers because the district builds in the same order.
        public struct AirHockeyTableInfo
        {
            public GameObject Root;
            public GameObject Arch;
            public int Id;
            public string Name;
        }
        public static readonly List<AirHockeyTableInfo> AirHockeyTables =
            new List<AirHockeyTableInfo>();

        // v0.10.21: the arcade cabinets, for the "Dinozavrik" interaction (E at a cabinet)
        public struct ArcadeCabinetInfo
        {
            public GameObject Root;
            public int Id;
            public int Kind;                   // v0.10.36: 0 Dino, 1 Arkanoid, 2 Pong, 3 Snake
            public bool Duel;                  // v0.10.42: the extra opposite-wall duel cabinet
            public string PosterFile;          // v0.10.36: per-cabinet marquee texture
            public GameObject AttractScreen;   // v0.10.31: the looping gameplay screen
            public GameObject AttractBack;     // v0.10.31: the black screen backing
            public GameObject Poster;          // v0.10.33: the chosen marquee poster
            public Mesh AttractMesh;           // v0.10.33: per-cabinet desynced flipbook mesh
            public float AttractPhase;         // v0.10.33: random start offset, seconds
            public int AttractFrame;           // v0.10.33: last set cell
        }
        public static readonly List<ArcadeCabinetInfo> ArcadeCabinetList =
            new List<ArcadeCabinetInfo>();
        /// <summary>Table-local Y of the **rink face** (source y=3.0 x 0.46 x 0.80). The table's
        /// rim/bumper top is 3.1 source = 1.1408 m; the playfield is 3.7 cm lower, inside the rim.</summary>
        public const float AirHockeyRinkTop = 1.104f;
        /// <summary>Table-local Y of the rim/bumper top (source y=3.1 x 0.46 x 0.80).</summary>
        public const float AirHockeyBodyTop = 1.1408f;
        /// <summary>v0.10.12: the showroom tables get a theme-coloured raised bumper +5 cm.</summary>
        public const float AirHockeyBumperRaise = 0.05f;

        // v0.10.8/0.10.10: retextured showroom tables on the annex's two lower floors. Six are on
        // 96.15 (the floor under the playable table); four more are one floor below at 89.75, in the
        // north strip of the ground ring. Each pattern is stretched around the table's own side
        // triangles with one mirrored cycle, and the arch is painted from the pattern's dominant hue
        // (theme) plus a secondary accent for its lamp.
        private static readonly string[] AirHockeyPatternFiles =
        {
            "sloppy_01.png", "sloppy_02.png", "sloppy_03.png",
            "sloppy_04.png", "sloppy_05.png", "sloppy_06.png",
            "sloppy_07.png", "sloppy_08.png", "sloppy_09.png", "sloppy_10.png"
        };
        private static readonly Color[] AirHockeyPatternTheme =
        {
            new Color(0.99f, 0.01f, 0.81f),   // 01 · magenta
            new Color(0.99f, 0.65f, 0.11f),   // 02 · orange
            new Color(0.93f, 0.80f, 0.07f),   // 03 · acid yellow
            new Color(0.92f, 0.86f, 0.18f),   // 04 · gold
            new Color(0.08f, 0.45f, 0.70f),   // 05 · cosmic blue
            new Color(0.02f, 0.88f, 0.98f),   // 06 · cyan
            new Color(0.80f, 0.86f, 0.06f),   // 07 · muted acid yellow
            new Color(0.87f, 0.42f, 0.13f),   // 08 · muted orange (cartoon cats)
            new Color(0.11f, 0.76f, 0.36f),   // 09 · muted green
            new Color(0.82f, 0.32f, 0.75f),   // 10 · muted pink (cartoon frog)
        };
        private static readonly Color[] AirHockeyPatternAccent =
        {
            new Color(0.03f, 0.90f, 0.95f),   // 01 · cyan
            new Color(0.35f, 0.22f, 0.85f),   // 02 · purple
            new Color(0.55f, 0.95f, 0.05f),   // 03 · acid green
            new Color(0.86f, 0.09f, 0.15f),   // 04 · red
            new Color(0.10f, 0.85f, 0.90f),   // 05 · cyan
            new Color(0.94f, 0.00f, 0.95f),   // 06 · magenta
            new Color(0.83f, 0.22f, 0.62f),   // 07 · muted pink
            new Color(0.13f, 0.86f, 0.87f),   // 08 · teal
            new Color(0.55f, 0.43f, 0.24f),   // 09 · brown/gold
            new Color(0.11f, 0.93f, 0.24f),   // 10 · green
        };
        private static readonly Vector3[] AirHockeyVariantSpots =
        {
            new Vector3(-44.0f, 96.15f, 6.5f),
            new Vector3(-30.5f, 96.15f, 6.5f),
            new Vector3(-17.0f, 96.15f, 6.5f),
            new Vector3(-44.0f, 96.15f, -4.5f),
            new Vector3(-30.5f, 96.15f, -4.5f),
            new Vector3(-17.0f, 96.15f, -4.5f),
            // v0.10.10: the four on the floor below (89.75), north strip of the ground ring
            new Vector3(-44.0f, 89.75f, -11.0f),
            new Vector3(-35.0f, 89.75f, -11.0f),
            new Vector3(-26.0f, 89.75f, -11.0f),
            new Vector3(-17.0f, 89.75f, -11.0f),
        };

        private const float HeliYaw = -90f;
        private static Vector3 heliInA, heliInB;   // world-space interior cameras
        private static Vector3 heliLowA, heliLowB; // under the tail, looking up at the ramp
        private static Vector3 heliSideA, heliSideB; // low 3/4 of the open hatch
        private static Vector3 heliCabA, heliCabB;   // inside the cockpit
        private static Vector3 heliEdgeA, heliEdgeB;  // close-up of the hatch edge from inside
        private static Transform heliMesh;            // the model mesh, for preview points
        private static HeliHatch heliHatch;          // the animated cargo hatch
        private static Vector3 heliRampA, heliRampB; // camera for the ramp shots

        /// <summary>v0.9.113 — the cargo hatch: two clamshell leaves swing out first,
        /// then the ramp lowers (the real machine's sequence). H toggles.</summary>
        public class HeliHatch : MonoBehaviour
        {
            public Transform DoorL, DoorR, Ramp;
            public Vector3 AxisL = Vector3.forward, AxisR = Vector3.forward;
            public float SignL = 1f, SignR = -1f;
            public float DoorAngle = 100f;
            public float RampAngle = 19f;
            public float RampSlide = 12f;   // how far it retracts into the bay
            public float RampDrop = 3.8f;   // and settles onto the bay floor
            public float Duration = 5f;
            public bool Open = true;
            private float t = 1f;

            private void Update()
            {
                if (Input.GetKeyDown(KeyCode.H))
                {
                    Open = !Open;
                    Debug.Log("[HELI] cargo hatch " + (Open ? "opening" : "closing"));
                }
                t = Mathf.MoveTowards(t, Open ? 1f : 0f, Time.deltaTime / Mathf.Max(0.2f, Duration));
                Apply();
            }

            private void Apply()
            {
                float d = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.45f));
                float r = Mathf.Clamp01((t - 0.45f) / 0.55f);
                if (DoorL != null) DoorL.localRotation = Quaternion.AngleAxis(SignL * DoorAngle * d, AxisL);
                if (DoorR != null) DoorR.localRotation = Quaternion.AngleAxis(SignR * DoorAngle * d, AxisR);
                if (Ramp != null)
                {
                    // the state before the last turn: swing + retract into the bay
                    float down = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((r - 0.5f) / 0.5f));
                    float pull = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(r / 0.5f));
                    Ramp.localRotation = Quaternion.Euler(RampAngle * down, 0f, 0f);
                    // v0.9.125: only the horizontal pull — the ramp slides in dead level
                    Ramp.localPosition = new Vector3(0f, 0f, -RampSlide * pull);
                }
            }

            public void SetPose(float v)
            {
                t = Mathf.Clamp01(v);
                Open = t > 0.5f;
                Apply();
            }
        }
        private static Bldg reliefBlock;   // v0.9.73: the block that carries the bas-relief
        /// <summary>
        /// v0.9.10 вЂ” **the dam's own grain at the dam's own scale**. `Geo.Quad` ignores the per-call
        /// rate and maps every face at `GrainUv` repeats per metre, so the deck, the walls, the
        /// streets, the houses and the House of Soviets are one continuous surface.
        ///
        /// Measured with the `grain_dam` / `grain_ours` map shots (2.4 m straight above the two sides
        /// of the seam, 2.7 mm per pixel, both surfaces therefore on the same ruler): the dam's
        /// concrete carries 1-4 cm pits and the deck 0.7-3 cm at `GrainScale = 0.7`, i.e. the game's
        /// dam maps the texture roughly **once per 1.3-1.5 m** вЂ” which is not surprising, the crest
        /// is a blockout box. What was actually wrong in v0.9.6-v0.9.9 was not the density (0.7/m)
        /// but the map: a 32x32 high-pass **boosted 1.7x**, which turned the photo's soft stains into
        /// a coarse 2-5 cm camouflage вЂ” exactly what the user's screenshot next to the dam shows.
        /// v0.9.10 keeps the photo's pits at their own contrast and only damps the stains
        /// (`GrainCloud`), and it makes the tile exactly periodic (`Seamless`), because the photo is
        /// only *nearly* seamless and at 1.4 m tiling that drew a faint grid over the whole district.
        /// </summary>
        private const float GrainUvBase = 1f;

        /// <summary>[Debug] GrainScale вЂ” multiplier over `GrainUvBase` (live tuning, no rebuild).
        /// v0.9.11: 0.7 -> 0.35 вЂ” the user asked for grain twice as coarse, so the tile is 2.86 m
        /// instead of 1.43 m and every pit/aggregate grain doubles on screen.</summary>
        public static float GrainScale = 0.35f;

        /// <summary>[Debug] GrainCloud вЂ” how much of the photo's 20-40 %-of-tile soft stains survive.
        /// At 0 they are removed entirely (they are what tiled the district like wallpaper at 1 m),
        /// at 1 the map is the raw photo.</summary>
        public static float GrainCloud = 0.15f;

        /// <summary>[Debug] GrainGain вЂ” contrast of the surviving detail. 1.0 = exactly the photo's
        /// own pit contrast, which is what the dam shows; v0.9.6 used 1.7 (doubled the pits).</summary>
        public static float GrainGain = 1f;

        /// <summary>[Debug] GrainTone вЂ” uniform brightness lift, the only way to modulate albedo on
        /// `S_EnviroSurface00` (it has no `_Color`). The level is baked and runtime geometry only
        /// gets probe light, so the same texture renders darker here. v0.9.11: 1.2 -> 1.68 (+40 %
        /// per the user); the photo's own maximum (141/255) still lands below clipping (237).</summary>
        public static float GrainTone = 1.68f;

        /// <summary>[Debug] GrainDesat вЂ” pulls the photo's grey-green cast towards neutral.</summary>
        public static float GrainDesat = 0.8f;

        /// <summary>
        /// [Debug] PlatformGrain вЂ” **how much grainier the platform itself is than the houses**, a
        /// multiple of `GrainScale` applied to the *horizontal* faces only (deck, terraces, street
        /// kerbs, ramps, stair treads, the esplanade). v0.9.13: 4 = +400 %. **v0.9.16: 0,8** вЂ” the user
        /// asked for 0,2 of that (pits ~5x coarser, so the platform reads as big weathered slabs).
        /// Live tuning: `[District] PlatformGrain` in the cfg, no rebuild.
        /// </summary>
        public static float PlatformGrain = 2.0f;

        /// <summary>v0.9.70 вЂ” plan point 1-2: use the **main BABBDI city's own surfaces** instead of
        /// a re-baked grain. `true` = the level's raw `M_ConcreteRough00` (the material every city block
        /// and the ground use) and `M_ConcretePanels00` for the panel (khrushchevka) blocks; `false` =
        /// the old v0.9.11 derived grain with the 1.68 tone lift.</summary>
        public static bool MatchCity = true;
        /// <summary>
        /// [District] CornerApron (v0.9.20) вЂ” how far the **east terrace** (+5 m) is set back from the
        /// platform's south edge, so that its corner next to the round hole in the sea is the *base*
        /// deck and not a raised floor (the user's red outline in screenshot 1: "СЃСЂРѕРІРЅСЏС‚СЊ СЃ Р±Р°Р·РѕРІС‹Рј
        /// СѓСЂРѕРІРЅРµРј РѕР±С‰РµР№ РїР»Р°С‚С„РѕСЂРјС‹"). 0 = the old corner, flush with the crest; 12 = a 50 x 12 m apron
        /// at 0 m. Read once per build; the tier array is rebuilt from its base values every time.
        /// </summary>
        public static float CornerApron = 12f;

        /// <summary>
        /// [District] CornerPad (v0.9.44) вЂ” the 48 x 12 m plate at the south-east corner that bridges the
        /// base deck up to the crest's granite (v0.9.20-23). The user asked to remove it in v0.9.44 and
        /// then said it was the wrong object, so it is **on** again by default; `false` in the cfg turns
        /// the corner back into the plain deck (with the granite ridge's 0,3 m step).
        /// </summary>
        public static bool CornerPadOn = true;   // v0.9.47: on, with a new 6 m north ramp

        private static float GrainUv => GrainUvBase * GrainScale;

        /// <summary>Legacy alias: the deck/streets/esplanade builders still pass it, `Geo.Quad`
        /// ignores the value in favour of `GrainUv`.</summary>
        private const float DeckUv = GrainUvBase;

        private sealed class Bldg
        {
            public Vector3 p;          // local centre on the deck
            public float yaw;
            public float w, d;         // footprint
            // v0.9.13: the corridor is 2, 3 or 4 times the old 2.2 m; the extra width is taken out of
            // the block's depth (see `AddBlock`), so `d` already contains it
            public int corrMul = 1;
            public float CorrHalf => CORR * 0.5f * Mathf.Max(1, corrMul);
            public float baseY = BASE; // ground floor level
            public int floors;
            public float fh = FH;
            public float dh = 1f;      // door-height scale (stalinka: 1.3)
            public Material wall;
            public float winW = 1.45f, winH = 1.65f, sill = 0.95f, splay = 0f, bay = 3.2f;
            // v0.9.17: the House of Soviets keeps **every second** window (the user: В«СѓРґР°Р»РёРІ РєР°Р¶РґРѕРµ
            // РІС‚РѕСЂРѕРµ РѕРєРЅРѕВ»), and its 4 m openings need more of a span than the room-grid clearance
            // leaves вЂ” `winPad` is the extra slack allowed on each side of a span (0 = the old rule).
            public int winSkip = 1;
            public float winPad;
            // v0.9.52: the House of Soviets' two towers stand inside the central recess, and their
            // **inner** end walls (the ones facing the other tower) are pressed against the bay's
            // piers вЂ” 2 cm away. Every window cut there opened into a blind wall (and its reveal
            // fought with the pier's own face, which is what read as a "transparent texture"). These
            // flags build that facade solid: no windows, no entrance.
            public bool winPlusX = true, winMinusX = true;
            public bool winPlusZ = true, winMinusZ = true;
            // v0.9.59: the entrance door is independent of the windows вЂ” the House towers are entered
            // from the hall through their **inner** ends, where the honeycomb must stay solid
            public bool doorPlusX = true, doorMinusX = true;
            // v0.9.72: the House of Soviets' towers get string courses, window sills, pier ribs and a
            // cornice вЂ” the detail that makes their honeycomb read as deep-set windows, not holes
            public bool towerDetail;
            // v0.9.69: the entrance opening's size вЂ” the House of Soviets' towers get a monumental
            // 3,4 x 4,4 m portal instead of the ordinary 1,7 m street door (0 height = 2,5 * dh)
            public float entranceW = 1.7f, entranceH = 0f;
            // v0.9.60: per-floor window masks for the end walls (bit f set = windows allowed on floor f).
            // The House's towers clear bits 7-8 there, where the bridge lands.
            public int winMaskPlusX = -1, winMaskMinusX = -1;
            public int winMaskPlusZ = -1, winMaskMinusZ = -1;
            // v0.9.53: the first `winFromFloor` storeys carry **no windows** вЂ” the House of Soviets'
            // towers stand inside the hall, so their first four storeys (up to the podium's roof) look
            // into a blank wall a few metres away; above the roof they are ordinary facades again.
            public int winFromFloor;
            public bool balconies, risalit, bands, interior = true, plinth = true;
            // v0.9.14: this roof carries the memorial вЂ” no vent box on it (Roof())
            public bool memorial;
            // v0.9.73: this block carries the dark abstract bas-relief on its outward long wall
            public bool relief;
            // v0.9.18: this roof carries the penthouse (the clock tower on the wing's outlying block)
            public bool penthouse;
            // v0.9.79: this roof carries the Mi-26 вЂ” no vent box on it (Roof())
            public bool heli;
            // v0.9.19: the first three storeys are replaced by the pylons (see `Legs`)
            public bool legs;
            // v0.9.19: +1 / -1 leaves the pylons off one short face, because that face is shared with the
            // neighbour вЂ” the pair then reads as one house standing on one ring of legs
            public int legsShut;
            // v0.9.12: one balcony in building-local space, for the walkability probe
            public bool hasBalc;
            public Vector3 balcSample;
            public bool[] endPlus, endMinus;   // per floor: a bridge doorway in that end wall
            public float stairX0, stairX1, stairZ0, stairZ1;
            // v0.9.16: the plan the stair was built from (PlanStair) вЂ” the probes read it
            public float stairLand = 1.1f, stairRun = 3f;
            // v0.9.35: the open well between the two flights (see PlanStair/StairsZ)
            public float stairWell, stairFw;
            // v0.9.35: the street-level core of a block **on legs** (`PlanCore`). Its stair climbs
            // three storeys and tops out *through the block's first-floor slab*, so the hole it
            // needs вЂ” and the doorway it needs in the corridor wall вЂ” are planned once here and
            // read by both `Interior` (slab + wall) and `Legs` (the stair itself). Before this the
            // core's stair ended under a solid slab: the user's "РїСЂРѕР»РµС‚С‹ СѓРїРёСЂР°СЋС‚СЃСЏ РІ РїРµСЂРµРєСЂС‹С‚РёСЏ".
            public float coreX0, coreX1;       // the stair lane (1,7 m wide)
            public float coreZ0;               // the exit landing's north edge
            public float coreHoleZ0;           // south edge of the slab hole (headroom < 2 m below)
            public float coreHoleZ1;           // north edge, 2 cm past the landing (no coplanar faces)
            public GameObject go;
            // v0.9.11: one window centre + outward normal per facade (first floor above ground),
            // so ProbeWalkability can prove that no window looks into a partition or a stair
            public readonly List<Vector3> winPos = new List<Vector3>();
            public readonly List<Vector3> winDir = new List<Vector3>();

            public float Top => baseY + floors * fh;
            public bool Tall => fh > FH + 0.01f;
        }

        /// <summary>
        /// v0.9.11 вЂ” the interior of one block, computed **before** the facades, so the windows can be
        /// placed on the real room grid instead of on an arbitrary pitch. That pitch was the bug in
        /// the user's screenshots: a window in the middle of a cell had a cross partition, a corridor
        /// wall end or a stair flight right behind it.
        /// </summary>
        private sealed class Shell
        {
            public float hw, hd, y0, y1, fh, dh;
            public bool tall;
            public float x0, x1, zi, corr;     // interior faces / corridor half-width
            public int seg;
            public float segLen, xs;           // room cells along x, stair hall start
            public int kS;                     // stair hall cell
            // v0.9.16: **one** stair plan per block, computed in PlanStair and shared by the slab hole
            // (`Interior`) and the flights (`StairsZ`), so the two can never disagree. A floor plate
            // without a hole over a flight is exactly the user's "the stairs run into a blind ceiling",
            // and the old code cut the hole from different constants than the flights were built with.
            public float stRun;                // horizontal length of one flight вЂ” 2вЂ¦3 m (the user's spec)
            public float stLand;               // landing depth at each end (arrival / half-landing)
            public float stZLow, stZHigh;      // the flight zone in z (stZLow = the corridor side)
            public int stN;                    // risers per flight
            public float stRise, stTread;
            // v0.9.35: the two flights are separated by a real **open well** вЂ” the user's "РІРµСЂС‚РёРєР°Р»СЊРЅР°СЏ
            // С‰РµР»СЊ РјРµР¶РґСѓ РїРµСЂРёР»Р°РјРё, РїСЂРѕС…РѕРґСЏС‰Р°СЏ С‡РµСЂРµР· РІРµСЃСЊ РґРѕРј СЃРІРµСЂС…Сѓ РІРЅРёР·" (their reference photo). It
            // sits inside the slab hole, which already spans the whole stair hall, so the shaft is open
            // from the ground floor to the roof.
            public float stWell;               // clear gap between the flights
            public float stFw;                 // width of each flight

            public float StairX0 => xs;
            public float StairX1 => xs + segLen;
        }

        private sealed class BridgeSpec
        {
            public Vector3 a, b;    // corridor ends (world = root-local)
            public float y;         // floor level
            public int ia, ib, floor, fa, fb;   // v0.9.11: the storey on each side (fh can differ)
        }

        /// <summary>Footprint and storey base of one block type, already grown by <see cref="Grow"/>.</summary>
        private struct BlockStyle
        {
            public float w, d;
            public int baseFloors;
        }

        private static readonly List<Bldg> bldgs = new List<Bldg>();
        private static readonly List<BridgeSpec> bridges = new List<BridgeSpec>();
        private static int builtCount, bridgeCount, skipCount, quadCount;

        // ------------------------------------------------------------------ entry

        public static void TryBuild()
        {
            if (!Enabled || root != null) return;
            if (!WorldReady()) return;
            Built = true;
            try
            {
                Build();
            }
            catch (Exception e)
            {
                log.LogError($"[DISTRICT] build failed: {e}");
            }
        }

        /// <summary>True once the level's dam exists at the junction (so this never fires in the menu).</summary>
        private static bool WorldReady()
        {
            Vector2 dir = (DamB - DamA).normalized;
            Vector2 nrm = new Vector2(-dir.y, dir.x);
            var from = new Vector3(DamA.x + nrm.x * 1.5f, DeckY + 60f, DamA.y + nrm.y * 1.5f);
            RaycastHit h;
            if (!Physics.Raycast(from, Vector3.down, out h, 120f, ~0, QueryTriggerInteraction.Ignore)) return false;
            return Mathf.Abs(h.point.y - DeckY) < 5f;
        }

        private static void Build()
        {
            log = BepInEx.Logging.Logger.CreateLogSource("BabbdiCoop");
            rng = new System.Random(20261023);

            Vector2 dir = (DamB - DamA).normalized;                 // along the dam
            Vector2 nrm = new Vector2(-dir.y, dir.x);               // seaward
            // v0.9.8: the platform no longer sits on the round hole in the sea at its east edge вЂ” the
            // whole district is shifted 15 m along the dam (the hole stays where the level put it)
            Vector3 origin = new Vector3(DamA.x, DeckY, DamA.y) + new Vector3(dir.x, 0f, dir.y) * ShiftX;
            Quaternion rot = Quaternion.LookRotation(new Vector3(nrm.x, 0f, nrm.y), Vector3.up);

            FindMaterials();
            DumpTextures();
            ScanNpcs();         // TEMP (v0.9.21): the game's own humanoids, measured
            if (mRough == null)
            {
                log.LogError("[DISTRICT] concrete materials not found вЂ” district skipped");
                return;
            }

            var go = new GameObject("CoopSovietDistrict");
            go.transform.SetPositionAndRotation(origin, rot);
            root = go.transform;

            RaycastHit hit;
            if (Physics.Raycast(origin + Vector3.up * 120f, Vector3.down, out hit, 300f, ~0, QueryTriggerInteraction.Ignore))
            {
                log.LogInfo($"[DISTRICT] crest under the junction: y={hit.point.y:F1} ({hit.collider.name})");
                LogLook(hit);
                ScanCity();
            }

            ClearInvisibleWalls(origin, rot);
            // v0.9.20: the raised east terrace is cut back from the crest at its south-east corner, so the
            // corner next to the round hole in the sea is base deck at 0 m (the user's red outline).
            // The rest of the terrace and every other floor stay exactly as they were.
            if (CornerApron > 0.01f)
            {
                float ap = Mathf.Clamp(CornerApron, 0f, 60f);
                for (int i = 0; i < tiers.Length; i++)
                    if (tiers[i].name == "2") tiers[i].z0 = ap;
                log.LogInfo($"[DISTRICT] south-east corner: the +5 m east terrace is set back {ap:0.#} m " +
                            $"from the crest ({tiers[1].x1 - tiers[1].x0:0} x {ap:0.#} m of flat corner at the " +
                            $"round hole instead of a bare 5 m ledge), the rest of the terrace is unchanged");
            }
            Deck();
            if (CornerPadOn) CornerPad();    // v0.9.20: that corner's plate, flush with the crest (v0.9.44: off by default)
            Terraces();     // v0.9.9: the raised floors of the plan (+5 / +8 / +11 / +14 / +10 m)
            Ramps();        // the yellow bars: inclined roads between the floors
            TierStairs();   // the green bars: pedestrian stairs at the same edges
            Streets();
            Layout();
            PlanBridges();
            // v0.9.14: the memorial stands on the second-tallest block вЂ” picked before the build loop,
            // so its roof can leave the vent box out
            memBlock = null;
            if (MemorialOn && bldgs.Count > 1) memBlock = PickMemorialBlock();
            if (memBlock != null) memBlock.memorial = true;
            // v0.9.79: the Mi-26 goes on the tallest free roof wide enough to hold the 39,6 m fuselage
            heliBlock = PickHeliBlock();
            if (heliBlock != null) heliBlock.heli = true;
            foreach (var b in bldgs) BuildBuilding(b);
            if (reliefBlock != null) BuildRelief(reliefBlock);
            foreach (var s in bridges) BuildBridge(s);
            HouseOfSoviets(new Vector3(230f, PlinthH, 84f), 4f);
            Square();
            if (memBlock != null) Memorial(memBlock);
            if (heliBlock != null) Helicopter(heliBlock);

            Vector3 c = root.TransformPoint(new Vector3(Len * 0.5f, 0f, Wid * 0.5f));
            Center = c;
            log.LogInfo($"[DISTRICT] built {builtCount} buildings, {skipCount} skipped plots, {bridgeCount} bridges, " +
                        $"{quadCount} quads; platform {Len}x{Wid} m + {Axw} m western wing at y={DeckY:F1}, " +
                        $"shifted {ShiftX:F0} m along the dam, centre {c.x:F0},{c.y:F0},{c.z:F0}");
            // v0.9.13: how the 2x/3x/4x corridors came out, and the widest + deepest house as the proof
            int[] cm = new int[5];
            float deepest = 0f;
            foreach (var b in bldgs)
            {
                cm[Mathf.Clamp(b.corrMul, 1, 4)]++;
                deepest = Mathf.Max(deepest, b.d);
            }
            log.LogInfo($"[DISTRICT] corridors: 2x = {cm[2]} blocks (4,4 m), 3x = {cm[3]} (6,6 m), " +
                        $"4x = {cm[4]} (8,8 m), 1x = {cm[1]}; deepest block {deepest:0.0} m deep");
        }

        private static void FindMaterials()
        {
            mPanel = Mat("M_ConcretePanels00");
            mRough = Mat("M_ConcreteRough00");     // v0.9.4: the dam's material and the city's block material
            mSlabs = Mat("M_ConcreteSlabs01");
            mLeaks = Mat("M_ConcreteLeaks00");
            mDark = Mat("M_Black00");
            mMetal = Mat("M_MetalRusty00");
            mGrass = Mat("M_Grass00");
            mDirt = Mat("M_Dirt00");
            mPainted = Mat("M_PaintedWall00");
            mMossy = Mat("M_MossyConcrete00");
            if (mRough == null) mRough = mSlabs != null ? mSlabs : mPanel;
            if (mSlabs == null) mSlabs = mRough;
            if (mLeaks == null) mLeaks = mRough;
            if (mDark == null) mDark = mRough;
            if (mMetal == null) mMetal = mDark;
            if (mGrass == null) mGrass = mSlabs;
            if (mDirt == null) mDirt = mRough;
            if (mPainted == null) mPainted = mRough;
            if (mMossy == null) mMossy = mRough;
            // v0.9.70: plan points 1-2 вЂ” the **main BABBDI city's own surface**. The v0.9.11 derived
            // grain (tone 1.68, cloud 0.15, desat 0.8) is what made the district read smoother and
            // lighter than the city. v0.9.76: **one tone for every Stalin house** вЂ” the panel/material
            // mix (which left some blocks lighter) is gone; every block uses the same raw concrete.
            if (MatchCity)
            {
                log.LogInfo("[LOOK] district material: RAW " + N(mRough) +
                            " for every block and the ground (one tone; no re-baked grain, no panel mix)");
            }
            else
            {
                mRough = Stone() ?? mRough;
            }
            log.LogInfo($"[DISTRICT] materials: rough={N(mRough)} panel={N(mPanel)} slabs={N(mSlabs)} " +
                        $"leaks={N(mLeaks)} grass={N(mGrass)}");
        }

        private static string N(Material m) => m != null ? m.name : "none";

        // --- one-shot look diagnostics: what the existing dam and the city's blocks actually use ---

        private static string TexName(Material m)
            => m == null || m.mainTexture == null ? "-" : m.mainTexture.name;

        private static string Tiling(Material m)
            => m == null ? "-" : string.Format("tile {0:0.###},{1:0.###} off {2:0.##},{3:0.##}",
                                               m.mainTextureScale.x, m.mainTextureScale.y,
                                               m.mainTextureOffset.x, m.mainTextureOffset.y);

        private static string ColorOf(Material m)
            => m == null ? "-" : (m.HasProperty("_Color") ? m.color.ToString("F2") : "-");

        /// <summary>Texture repeats per metre of the mesh (u per metre + v per metre, averaged).</summary>
        private static float UvDensity(Mesh mesh, Vector3 size)
        {
            var uv = mesh.uv;
            if (uv == null || uv.Length == 0) return 0f;
            float u0 = float.MaxValue, u1 = float.MinValue, v0 = float.MaxValue, v1 = float.MinValue;
            for (int i = 0; i < uv.Length; i++)
            {
                if (uv[i].x < u0) u0 = uv[i].x;
                if (uv[i].x > u1) u1 = uv[i].x;
                if (uv[i].y < v0) v0 = uv[i].y;
                if (uv[i].y > v1) v1 = uv[i].y;
            }
            float w = Mathf.Max(0.5f, Mathf.Max(size.x, size.z));
            return ((u1 - u0) / w + (v1 - v0) / Mathf.Max(0.5f, size.y)) * 0.5f;
        }

        private static string MatInfo(Material m)
        {
            if (m == null) return "none";
            var sb = new System.Text.StringBuilder();
            sb.Append(m.name).Append(" shader=").Append(m.shader.name);
            sb.Append(" tex{");
            var props = m.GetTexturePropertyNames();
            for (int i = 0; i < props.Length; i++)
            {
                var t = m.GetTexture(props[i]);
                sb.Append(props[i]).Append('=').Append(t != null ? t.name : "null").Append(';');
            }
            sb.Append('}');
            for (int i = 0; i < props.Length; i++)
                if (m.HasProperty(props[i] + "_ST")) sb.Append(' ').Append(props[i]).Append("_ST=").Append(m.GetVector(props[i] + "_ST").ToString("F2"));
            if (m.HasProperty("_Color")) sb.Append(" col=").Append(m.color.ToString("F2"));
            return sb.ToString();
        }

        private static string BlockInfo(Renderer r, Material m)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(MatInfo(m));
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            var t = mpb.GetTexture("_MainTex");
            if (t != null) sb.Append(" mpb.MainTex=").Append(t.name);
            var names = new[] { "_MainTex_ST", "_UVOffset", "_UvRect", "_AtlasRect" };
            for (int i = 0; i < names.Length; i++)
                if (mpb.GetVector(names[i]) != Vector4.zero) sb.Append(' ').Append(names[i]).Append('=').Append(mpb.GetVector(names[i]).ToString("F2"));
            return sb.ToString();
        }

        /// <summary>Diagnostic for the tone gap between the district and the level's own concrete: the
        /// level is static and lightmapped (its bake carries the bounce light), while runtime geometry
        /// only gets the ambient probe + the real-time lights. Logged once per build.</summary>
        private static void LogLighting(Renderer dam)
        {
            var sh = mRough.shader;
            var sb = new System.Text.StringBuilder();
            int n = sh.GetPropertyCount();
            for (int i = 0; i < n; i++)
                sb.Append(sh.GetPropertyName(i)).Append('(').Append(sh.GetPropertyType(i)).Append(") ");
            log.LogInfo($"[LIGHT] shader {sh.name}: {sb}");
            log.LogInfo($"[LIGHT] dam lightmap={dam.lightmapIndex} uvScale={dam.lightmapScaleOffset} " +
                        $"probes={dam.lightProbeUsage} anchor={(dam.probeAnchor != null ? dam.probeAnchor.name : "none")}");
            log.LogInfo($"[LIGHT] ambient mode={RenderSettings.ambientMode} light={RenderSettings.ambientLight} " +
                        $"intensity={RenderSettings.ambientIntensity:0.00} sky={(RenderSettings.skybox != null ? RenderSettings.skybox.name : "none")} " +
                        $"fog={RenderSettings.fog} fogColor={RenderSettings.fogColor} density={RenderSettings.fogDensity:0.0000}");
            var lights = Resources.FindObjectsOfTypeAll<Light>();
            for (int i = 0; i < lights.Length; i++)
            {
                var l = lights[i];
                if (l == null || !l.gameObject.scene.IsValid() || !l.gameObject.activeInHierarchy) continue;
                log.LogInfo($"[LIGHT] {l.name}: {l.type} intensity={l.intensity:0.00} color={l.color} " +
                            $"shadows={l.shadows} cull={l.cullingMask} rot={l.transform.eulerAngles}");
            }
            log.LogInfo($"[LIGHT] light probes: {(LightmapSettings.lightProbes != null ? LightmapSettings.lightProbes.count : 0)}; " +
                        $"camera cull={(Camera.main != null ? Camera.main.cullingMask : 0)}; " +
                        $"ambientProbe up={AmbientUp()}");
        }

        private static string AmbientUp()
        {
            var sh = RenderSettings.ambientProbe;
            var dirs = new[] { Vector3.up };
            var cols = new Color[1];
            sh.Evaluate(dirs, cols);
            return $"{cols[0].linear * 255f}";
        }

        private static void LogLook(RaycastHit hit)
        {
            var r = hit.collider != null ? hit.collider.GetComponent<Renderer>() : null;
            if (r != null)
            {
                log.LogInfo($"[LOOK] dam {r.name}: {BlockInfo(r, r.sharedMaterial)}");
                LogLighting(r);
            }

            log.LogInfo($"[LOOK] panel: {MatInfo(mPanel)}");
            log.LogInfo($"[LOOK] rough: {MatInfo(mRough)}");
            log.LogInfo($"[LOOK] slabs: {MatInfo(mSlabs)}");
            log.LogInfo($"[LOOK] leaks: {MatInfo(mLeaks)}");
            log.LogInfo($"[LOOK] painted: {MatInfo(mPainted)}");
            log.LogInfo($"[LOOK] grass: {MatInfo(mGrass)}");

            // the biggest scene blocks = the city of the main level
            var list = new List<MeshRenderer>();
            var all = Resources.FindObjectsOfTypeAll<MeshRenderer>();
            for (int i = 0; i < all.Length; i++)
            {
                var mr = all[i];
                if (mr == null || !mr.gameObject.scene.IsValid()) continue;
                var mf0 = mr.GetComponent<MeshFilter>();
                if (mf0 == null || mf0.sharedMesh == null) continue;
                var s = mr.bounds.size;
                if (s.y < 6f || s.x > 200f || s.z > 200f) continue;
                list.Add(mr);
            }
            list.Sort((a, b) => (b.bounds.size.x * b.bounds.size.y * b.bounds.size.z)
                                .CompareTo(a.bounds.size.x * a.bounds.size.y * a.bounds.size.z));
            for (int i = 0; i < Mathf.Min(4, list.Count); i++)
            {
                var mr = list[i];
                log.LogInfo($"[LOOK] city {mr.name}: {BlockInfo(mr, mr.sharedMaterial)} " +
                            $"size={mr.bounds.size.x:0}x{mr.bounds.size.y:0}x{mr.bounds.size.z:0} " +
                            $"uv={UvDensity(mr.GetComponent<MeshFilter>().sharedMesh, mr.bounds.size):0.000}/m");
            }
        }

        /// <summary>v0.9.70 вЂ” what the **main BABBDI city** around the player's spawn is actually made
        /// of: the ground under it, the buildings' materials and their `_BC` textures. This is the
        /// measured target for plan points 1-2 (the district must read as the same surface).</summary>
        private static void ScanCity()
        {
            try
            {
                var fpc = FirstPersonController.instance;
                if (fpc == null) { log.LogInfo("[CITY] no FirstPersonController"); return; }
                Vector3 pp = fpc.transform.position;
                log.LogInfo($"[CITY] player spawn {pp.x:0.0},{pp.y:0.0},{pp.z:0.0}");

                // the ground under the spawn, a 8 m grid out to 16 m
                var ground = new Dictionary<string, string>();
                for (int i = -2; i <= 2; i++)
                    for (int j = -2; j <= 2; j++)
                    {
                        var p = pp + new Vector3(i * 8f, 90f, j * 8f);
                        if (!Physics.Raycast(p, Vector3.down, out var h, 260f, ~0, QueryTriggerInteraction.Ignore)) continue;
                        var r = h.collider.GetComponent<Renderer>();
                        string key = (r != null ? r.name : h.collider.name) + " | " +
                                     (r != null ? MatInfo(r.sharedMaterial) : "none");
                        if (!ground.ContainsKey(key))
                            ground[key] = $"{h.point.x:0},{h.point.y:0.0},{h.point.z:0}";
                    }
                foreach (var kv in ground) log.LogInfo($"[CITY] ground {kv.Key} at {kv.Value}");

                // the actual surface under the player's feet (the long rays above hit the overhangs)
                {
                    var c0 = fpc.GetComponent<CharacterController>();
                    Vector3 feet = c0 != null
                        ? fpc.transform.TransformPoint(c0.center) - Vector3.up * (c0.height * 0.5f)
                        : pp;
                    for (int k = 0; k < 4; k++)
                    {
                        var p = feet + new Vector3(k == 0 ? 0f : (k == 1 ? 3f : (k == 2 ? -3f : 0f)), 0.4f,
                                                   k == 3 ? 3f : 0f);
                        if (!Physics.Raycast(p, Vector3.down, out var hg, 6f, ~0, QueryTriggerInteraction.Ignore)) continue;
                        var rg = hg.collider.GetComponent<Renderer>();
                        log.LogInfo($"[CITY] under the feet {k}: y={hg.point.y:0.00} ({hg.collider.name}) " +
                                    (rg != null ? BlockInfo(rg, rg.sharedMaterial) : "no renderer"));
                        if (rg != null)
                        {
                            var mfs = rg.GetComponent<MeshFilter>();
                            var sms = rg.sharedMaterials;
                            var sbM = new System.Text.StringBuilder();
                            for (int mi = 0; mi < sms.Length; mi++)
                                sbM.Append('[').Append(mi).Append("] ")
                                   .Append(sms[mi] != null ? sms[mi].name : "none").Append(' ');
                            log.LogInfo($"[CITY] under the feet {k} materials: {sbM} " +
                                        $"subs={(mfs != null && mfs.sharedMesh != null ? mfs.sharedMesh.subMeshCount : -1)}");
                            var mpb = new MaterialPropertyBlock();
                            rg.GetPropertyBlock(mpb);
                            var mt = mpb.GetTexture("_BC") as Texture2D;
                            var mv = mpb.GetVector("_BC_ST");
                            log.LogInfo($"[CITY] under the feet {k} MPB: _BC={(mt != null ? mt.name : "none")} " +
                                        $"_BC_ST=({mv.x:0.###},{mv.y:0.###},{mv.z:0.###},{mv.w:0.###})");
                            if (mt != null)
                            {
                                string dir = System.IO.Path.Combine(
                                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
                                    "BabbdiCoop", "texdump");
                                try { System.IO.Directory.CreateDirectory(dir); } catch { }
                                SavePng(mt, System.IO.Path.Combine(dir, "city_ground_" + k + ".png"));
                            }
                        }
                    }
                }

                // the renderers around the spawn: material, `_BC`, size (buildings vs ground)
                var list = new List<MeshRenderer>();
                var all = Resources.FindObjectsOfTypeAll<MeshRenderer>();
                for (int i = 0; i < all.Length; i++)
                {
                    var mr = all[i];
                    if (mr == null || !mr.gameObject.scene.IsValid()) continue;
                    var mf = mr.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    if ((mr.bounds.center - pp).sqrMagnitude > 130f * 130f) continue;
                    list.Add(mr);
                }
                list.Sort((a, b) => (a.bounds.center - pp).sqrMagnitude
                                    .CompareTo((b.bounds.center - pp).sqrMagnitude));
                var seen = new HashSet<string>();
                int n = 0;
                for (int i = 0; i < list.Count && n < 44; i++)
                {
                    var mr = list[i];
                    var m = mr.sharedMaterial;
                    var tex = m != null ? m.GetTexture("_BC") as Texture2D : null;
                    string key = (m != null ? m.name : "none") + "|" + (tex != null ? tex.name : "none");
                    string kind = mr.bounds.size.y < 0.7f ? "flat" : (mr.bounds.size.y > 6f ? "tall" : "small");
                    if (!seen.Add(key + kind)) continue;
                    n++;
                    float d = (mr.bounds.center - pp).magnitude;
                    log.LogInfo($"[CITY] {kind} {mr.name} d={d:0} " +
                                $"size={mr.bounds.size.x:0.#}x{mr.bounds.size.y:0.#}x{mr.bounds.size.z:0.#}: {BlockInfo(mr, m)}");
                }

                // every material in the build with a `_BC`, so the palette can be matched by name
                var mats = Resources.FindObjectsOfTypeAll<Material>();
                var seenM = new HashSet<string>();
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || m.shader == null) continue;
                    var t = m.GetTexture("_BC") as Texture2D;
                    if (t == null) continue;
                    if (!seenM.Add(m.name + "|" + t.name)) continue;
                    sb.Append(m.name).Append('=').Append(t.name).Append(' ')
                      .Append(t.width).Append('x').Append(t.height).Append("; ");
                }
                log.LogInfo("[CITY] materials with _BC: " + sb);

                // the **city-wide** surface census: which material/texture the tall facades and the
                // flat ground actually use within 500 m, with the number of renderers (v0.9.70)
                var tall = new Dictionary<string, int>();
                var flat = new Dictionary<string, int>();
                for (int i = 0; i < all.Length; i++)
                {
                    var mr = all[i];
                    if (mr == null || !mr.gameObject.scene.IsValid()) continue;
                    if ((mr.bounds.center - pp).sqrMagnitude > 500f * 500f) continue;
                    var m = mr.sharedMaterial;
                    var tex = m != null ? m.GetTexture("_BC") as Texture2D : null;
                    string key = (m != null ? m.name : "none") + "|" + (tex != null ? tex.name : "none");
                    if (mr.bounds.size.y > 6f)
                        tall[key] = tall.TryGetValue(key, out var c1) ? c1 + 1 : 1;
                    else if (mr.bounds.size.y < 0.7f)
                        flat[key] = flat.TryGetValue(key, out var c2) ? c2 + 1 : 1;
                }
                var sbT = new System.Text.StringBuilder();
                foreach (var kv in tall) sbT.Append(kv.Key).Append(" x").Append(kv.Value).Append("; ");
                log.LogInfo("[CITY] tall surfaces: " + sbT);
                var sbF = new System.Text.StringBuilder();
                foreach (var kv in flat) sbF.Append(kv.Key).Append(" x").Append(kv.Value).Append("; ");
                log.LogInfo("[CITY] flat surfaces: " + sbF);
            }
            catch (Exception e) { log.LogError("[CITY] scan failed: " + e); }
        }

        // --- one-shot texture dump: every concrete texture of the game as a PNG, so the district can
        // borrow the *right* surface instead of guessing from material names ---

        private static void DumpTextures()
        {
            string dir;
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly().Location;
                dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(asm) ?? "",
                                             "BabbdiCoop", "texdump");
            }
            catch { return; }
            if (System.IO.File.Exists(System.IO.Path.Combine(dir, "done.txt"))) return;
            try { System.IO.Directory.CreateDirectory(dir); } catch { return; }

            var seen = new HashSet<Texture2D>();
            var all = Resources.FindObjectsOfTypeAll<Material>();
            for (int i = 0; i < all.Length; i++)
            {
                var m = all[i];
                if (m == null || m.shader == null) continue;
                var t = m.GetTexture("_BC") as Texture2D;
                if (t == null) continue;
                log.LogInfo($"[TEX] {m.name} ({m.shader.name}): _BC={t.name} {t.width}x{t.height} {t.format}");
                if (!seen.Add(t)) continue;
                SavePng(t, System.IO.Path.Combine(dir, Safe(t.name) + ".png"));
            }
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "done.txt"), "done"); } catch { }
        }

        private static string Safe(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
                sb.Append(char.IsLetterOrDigit(s[i]) ? s[i] : '_');
            return sb.ToString();
        }

        private static void SavePng(Texture2D t, string path)
        {
            int w = Mathf.Min(512, t.width), h = Mathf.Min(512, t.height);
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            Graphics.Blit(t, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0f, 0f, w, h), 0, 0);
            copy.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            try { System.IO.File.WriteAllBytes(path, copy.EncodeToPNG()); } catch { }
            UnityEngine.Object.Destroy(copy);
        }

        /// <summary>
        /// TEMP (v0.9.21): what do the game's **own** humanoids look like? Finds every `NPC` in the
        /// loaded scene, prints the box of each of its renderers (so the anatomy вЂ” head, torso, arms,
        /// legs вЂ” can be read off the numbers), the mesh, the material, the shader and the texture, and
        /// writes the textures to `BabbdiCoop/npcscan/*.png`. Marker: `npcscan/done.txt`.
        /// </summary>
        private static void ScanNpcs()
        {
            string dir;
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly().Location;
                dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(asm) ?? "",
                                             "BabbdiCoop", "npcscan");
            }
            catch { return; }
            if (System.IO.File.Exists(System.IO.Path.Combine(dir, "done.txt"))) return;
            try { System.IO.Directory.CreateDirectory(dir); } catch { return; }

            var npcs = Resources.FindObjectsOfTypeAll<NPC>();
            log.LogInfo($"[NPCSCAN] {npcs.Length} NPC components in the loaded scene");
            var seenTex = new HashSet<Texture2D>();
            foreach (var n in npcs)
            {
                if (n == null) continue;
                var tr = n.transform;
                var rs0 = n.GetComponentsInChildren<Renderer>(true);
                Bounds wb = default(Bounds);
                for (int i = 0; i < rs0.Length; i++)
                {
                    if (rs0[i] == null) continue;
                    if (i == 0) wb = rs0[i].bounds; else wb.Encapsulate(rs0[i].bounds);
                }
                log.LogInfo($"[NPCSCAN] '{n.name}' id={n.npcId} at ({tr.position.x:0.0},{tr.position.y:0.0},{tr.position.z:0.0}) " +
                            $"scale=({tr.lossyScale.x:0.00},{tr.lossyScale.y:0.00},{tr.lossyScale.z:0.00}) " +
                            $"height={wb.size.y:0.00} width={wb.size.x:0.00} depth={wb.size.z:0.00} " +
                            $"feet={(wb.center.y - wb.size.y * 0.5f):0.00} renderers={rs0.Length}");
                foreach (var r in rs0)
                {
                    if (r == null) continue;
                    var b = r.bounds;
                    string mesh = "-";
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                        mesh = $"mesh={mf.sharedMesh.name} v={mf.sharedMesh.vertexCount}";
                    var smr = r as SkinnedMeshRenderer;
                    if (smr != null && smr.sharedMesh != null)
                        mesh = $"skin={smr.sharedMesh.name} v={smr.sharedMesh.vertexCount} " +
                               $"bones={(smr.bones != null ? smr.bones.Length : 0)}";
                    var mat = r.sharedMaterial;
                    string tex = "-", shader = "-";
                    if (mat != null)
                    {
                        shader = mat.shader != null ? mat.shader.name : "-";
                        var t = (mat.GetTexture("_BC") ?? mat.GetTexture("_MainTex")) as Texture2D;
                        if (t != null)
                        {
                            tex = $"{t.name} {t.width}x{t.height}";
                            if (seenTex.Add(t)) SavePng(t, System.IO.Path.Combine(dir, Safe(t.name) + ".png"));
                        }
                    }
                    log.LogInfo($"[NPCSCAN]   {r.name} [{r.GetType().Name}] " +
                                $"size=({b.size.x:0.00}, {b.size.y:0.00}, {b.size.z:0.00}) " +
                                $"centre=({b.center.x:0.00}, {b.center.y:0.00}, {b.center.z:0.00}) " +
                                $"{mesh} mat={(mat != null ? mat.name : "-")} shader={shader} tex={tex}");
                }
            }
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "done.txt"), "done"); } catch { }
        }

        /// <summary>
        /// v0.9.10 вЂ” **the dam's own concrete for the whole district**. `mRough` is replaced at build
        /// time by a copy of `M_ConcreteRough00` whose `_BC` is `T_RoughConcreteMacro` itself, passed
        /// through a *faithful* grain pass (see `GrainTexture`): the pits keep the photo's own size
        /// and contrast, only the 20-40 %-of-tile soft stains are damped so the 1 m tiling of the
        /// deck does not read as wallpaper, and the whole map is lifted by `GrainTone` to sit at the
        /// dam's rendered brightness (the level is baked, runtime geometry only gets probe light).
        /// </summary>
        private static Material Stone()
        {
            var src = mRough != null ? mRough.GetTexture("_BC") as Texture2D : null;
            if (src == null) return mRough;
            // 1024 keeps the pinholes crisp (the photo is 2048); 128 is the "cloud" lattice, smoothed
            // before it is interpolated so nothing of the old 32x32 diamond grid can survive
            if (mGrain == null) mGrain = GrainTexture(src, 1024, 128, GrainGain, GrainCloud, GrainTone, GrainDesat);
            if (mGrain == null) return mRough;
            var m = new Material(mRough) { name = mRough.name + " (district)" };
            m.SetTexture("_BC", mGrain);
            log.LogInfo($"[LOOK] district material: {mRough.name} + {mGrain.width}x{mGrain.height} grain " +
                        $"from {src.name} ({src.width}x{src.height}, uv {GrainUv:0.###}/m, gain {GrainGain:0.00}, " +
                        $"cloud {GrainCloud:0.00}, tone {GrainTone:0.00}, desat {GrainDesat:0.00}); " +
                        $"platform floors x{PlatformGrain:0.##} = uv {GrainUv * PlatformGrain:0.###}/m");
            return m;
        }

        /// <summary>
        /// Faithful grain pass over the concrete photo (see `Stone()`): `out = pxв€’kВ·(blurв€’mean)` with
        /// `k = 1 в€’ cloud`. At `cloud = 0.35` the pits keep 100 % of the photo's contrast (so they are
        /// exactly the dam's pits: same size, same darkness), while the photo's broad dirty clouds are
        /// cut to a third вЂ” enough to avoid a visible repeat at the dam's 1 m tiling, not enough to
        /// flatten the surface. v0.9.6-v0.9.9 instead **boosted** the high frequencies 1.7x against a
        /// hard 32x32 box, which is what made the district look like coarse camouflage on the user's
        /// screenshot next to the dam.
        /// Runtime textures are not readable, so the photo is read back through a RenderTexture; the
        /// blur lattice is smoothed with a wrap-around 3x3 before its bilinear upsample, so no grid
        /// creases are left in the result.
        /// </summary>
        private static Texture2D GrainTexture(Texture2D src, int size, int coarse,
                                              float gain, float cloud, float tone, float desat)
        {
            var raw = Pixels(src, size);
            if (raw == null) return null;
            // v0.9.10: the photo is only *nearly* seamless (edge discontinuity 1.5x the local
            // contrast), and at one repeat per metre that drew a faint 1 m grid over the whole
            // district. Offset-and-blend makes the map exactly periodic: at the borders only the
            // half-shifted copy is used, in the middle the photo itself, and the two cross-fade.
            var hi = Seamless(raw, size);
            var lo = Downsample(hi, size, coarse);

            // smooth the cloud lattice (wrap) вЂ” a hard box leaves diagonal creases when upsampled
            var cl = new Color32[coarse * coarse];
            for (int y = 0; y < coarse; y++)
                for (int x = 0; x < coarse; x++)
                {
                    float ar = 0f, ag = 0f, ab = 0f;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            var c = lo[Wrap(y + dy, coarse) * coarse + Wrap(x + dx, coarse)];
                            ar += c.r; ag += c.g; ab += c.b;
                        }
                    cl[y * coarse + x] = new Color32((byte)(ar / 9f), (byte)(ag / 9f), (byte)(ab / 9f), 255);
                }

            // per-channel mean of the photo: keeps its neutral grey, no hue shift
            double sr = 0, sg = 0, sb = 0;
            for (int i = 0; i < hi.Length; i++) { sr += hi[i].r; sg += hi[i].g; sb += hi[i].b; }
            float mr = (float)(sr / hi.Length), mg = (float)(sg / hi.Length), mb = (float)(sb / hi.Length);

            var outp = new Color32[hi.Length];
            for (int y = 0; y < size; y++)
            {
                float fy = (y + 0.5f) * coarse / size - 0.5f;
                int y0 = Mathf.FloorToInt(fy);
                float ty = fy - y0;
                int ya = Wrap(y0, coarse), yb = Wrap(y0 + 1, coarse);
                for (int x = 0; x < size; x++)
                {
                    float fx = (x + 0.5f) * coarse / size - 0.5f;
                    int x0 = Mathf.FloorToInt(fx);
                    float tx = fx - x0;
                    int xa = Wrap(x0, coarse), xb = Wrap(x0 + 1, coarse);

                    var c00 = cl[ya * coarse + xa]; var c10 = cl[ya * coarse + xb];
                    var c01 = cl[yb * coarse + xa]; var c11 = cl[yb * coarse + xb];
                    float br = Mathf.Lerp(Mathf.Lerp(c00.r, c10.r, tx), Mathf.Lerp(c01.r, c11.r, tx), ty);
                    float bg = Mathf.Lerp(Mathf.Lerp(c00.g, c10.g, tx), Mathf.Lerp(c01.g, c11.g, tx), ty);
                    float bb = Mathf.Lerp(Mathf.Lerp(c00.b, c10.b, tx), Mathf.Lerp(c01.b, c11.b, tx), ty);

                    var c = hi[y * size + x];
                    // detail (pits, aggregate) at full contrast, clouds pulled towards the mean
                    float r = mr + (c.r - br) * gain + (br - mr) * cloud;
                    float g = mg + (c.g - bg) * gain + (bg - mg) * cloud;
                    float b = mb + (c.b - bb) * gain + (bb - mb) * cloud;

                    // colour correction: the photo's clouds are grey-green; the reference is neutral
                    // blue-grey concrete, so bleed the chroma towards the luminance
                    float l = 0.299f * r + 0.587f * g + 0.114f * b;
                    r = Mathf.Lerp(r, l, desat);
                    g = Mathf.Lerp(g, l, desat);
                    b = Mathf.Lerp(b, l, desat);

                    outp[y * size + x] = new Color32(Byte(r * tone), Byte(g * tone), Byte(b * tone), 255);
                }
            }

            var t = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = src.name + " (grain)",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 9
            };
            t.SetPixels32(outp);
            t.Apply(true);
            return t;
        }

        /// <summary>Offset-and-blend: an exactly periodic copy of the photo. At a border only the
        /// half-shifted copy is used; in the middle the photo itself; between them a sinВІ cross-fade.
        /// Both halves are sampled with wrap, so the result has no edge discontinuity at all вЂ” which
        /// is what removes the faint tiling grid at one repeat per metre.</summary>
        private static Color32[] Seamless(Color32[] src, int n)
        {
            var dst = new Color32[src.Length];
            int h = n / 2;
            for (int y = 0; y < n; y++)
            {
                float wy = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * (y + 0.5f) / n);
                int ys = Wrap(y + h, n);
                for (int x = 0; x < n; x++)
                {
                    float wx = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * (x + 0.5f) / n);
                    float w = wx * wy;
                    var a = src[y * n + x];
                    var b = src[ys * n + Wrap(x + h, n)];
                    dst[y * n + x] = new Color32(
                        (byte)(a.r * w + b.r * (1f - w) + 0.5f),
                        (byte)(a.g * w + b.g * (1f - w) + 0.5f),
                        (byte)(a.b * w + b.b * (1f - w) + 0.5f), 255);
                }
            }
            return dst;
        }

        /// <summary>Wrap-around box downsample `n -> coarse` (n must be a multiple of coarse).</summary>
        private static Color32[] Downsample(Color32[] src, int n, int coarse)
        {
            int step = Mathf.Max(1, n / coarse);
            var dst = new Color32[coarse * coarse];
            for (int y = 0; y < coarse; y++)
                for (int x = 0; x < coarse; x++)
                {
                    int sr = 0, sg = 0, sb = 0;
                    for (int dy = 0; dy < step; dy++)
                        for (int dx = 0; dx < step; dx++)
                        {
                            var c = src[(y * step + dy) * n + (x * step + dx)];
                            sr += c.r; sg += c.g; sb += c.b;
                        }
                    int k = step * step;
                    dst[y * coarse + x] = new Color32((byte)(sr / k), (byte)(sg / k), (byte)(sb / k), 255);
                }
            return dst;
        }

        private static int Wrap(int i, int n)
        {
            i %= n;
            return i < 0 ? i + n : i;
        }

        private static byte Byte(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v), 0, 255);

        /// <summary>Read a runtime (non-readable) texture back as `size x size` pixels.</summary>
        private static Color32[] Pixels(Texture2D src, int size)
        {
            RenderTexture rt = null;
            var prev = RenderTexture.active;
            try
            {
                rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                var tmp = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tmp.ReadPixels(new Rect(0f, 0f, size, size), 0, 0);
                tmp.Apply();
                var px = tmp.GetPixels32();
                UnityEngine.Object.Destroy(tmp);
                return px;
            }
            catch { return null; }
            finally
            {
                RenderTexture.active = prev;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
            }
        }

        private static Material Mat(string name)
        {
            var all = Resources.FindObjectsOfTypeAll<Material>();
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].name == name) return all[i];
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].name.StartsWith(name + " ", StringComparison.Ordinal)) return all[i];
            return null;
        }

        // ------------------------------------------------------------------ the deck and the new dam

        private static void ClearInvisibleWalls(Vector3 origin, Quaternion rot)
        {
            var b = new Bounds(origin + rot * new Vector3(Len * 0.5f, -7f, Wid * 0.5f), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var lp = new Vector3(((i & 1) == 0) ? -Axw : Len,
                                     ((i & 2) == 0) ? -16f : 2f,
                                     ((i & 4) == 0) ? 0f : Wid);
                b.Encapsulate(origin + rot * lp);
            }
            int off = 0;
            var all = Resources.FindObjectsOfTypeAll<Collider>();
            for (int i = 0; i < all.Length; i++)
            {
                var c = all[i];
                if (c == null || !c.gameObject.scene.IsValid()) continue;
                bool wall = false;
                for (var p = c.transform; p != null; p = p.parent)
                {
                    if (p.name.IndexOf("INVISIBLE", StringComparison.OrdinalIgnoreCase) >= 0) { wall = true; break; }
                }
                if (!wall) continue;
                if (!c.bounds.Intersects(b)) continue;
                c.enabled = false;
                off++;
            }
            if (off > 0)
                log.LogInfo($"[DISTRICT] opened the sea boundary: {off} invisible wall colliders inside the new platform disabled");
        }

        // ------------------------------------------------------------------ terraces, ramps, stairs (v0.9.9)

        /// <summary>
        /// One raised floor of the platform вЂ” the pink outlines of the user's terraced plan. The base
        /// deck is 0 m and each zone is lifted by its own amount: 2 в†’ +5, 3 в†’ +8, 4 в†’ +11, 5 в†’ +14,
        /// 6 в†’ +10 m. The zones tile the platform: the seaward band, the two flanks, the western wing
        /// and the plinth under the House of Soviets; everything else stays on the base deck.
        /// </summary>
        private struct Tier
        {
            public string name;
            public float x0, x1, z0, z1, h;
        }

        /// <summary>The plinth of the House of Soviets: the esplanade and the House stand on it.</summary>
        private const float PlinthH = 10f;

        private static readonly Tier[] tiers =
        {
            new Tier { name = "3", x0 = 0f,   x1 = Len, z0 = 210f, z1 = Wid, h = 8f },    // seaward band
            new Tier { name = "2", x0 = 330f, x1 = Len, z0 = 0f,   z1 = 210f, h = 5f },   // east band
            new Tier { name = "4", x0 = 0f,   x1 = 48f, z0 = 0f,   z1 = 210f, h = 11f },  // west band
            new Tier { name = "5", x0 = -Axw, x1 = 0f,  z0 = Az0,  z1 = Wid, h = 14f },   // western wing
            new Tier { name = "6", x0 = 160f, x1 = 305f, z0 = 40f, z1 = 132f, h = PlinthH }, // House plinth
        };

        /// <summary>Height of the walkable surface at a local point: 0 on the base deck.</summary>
        private static float TierAt(float x, float z)
        {
            float h = 0f;
            for (int i = 0; i < tiers.Length; i++)
            {
                var t = tiers[i];
                if (x >= t.x0 && x <= t.x1 && z >= t.z0 && z <= t.z1 && t.h > h) h = t.h;
            }
            return h;
        }

        /// <summary>A yellow bar of the plan (an inclined road) or a green one (a street stair):
        /// it runs from (x0,z0) at height y0 to (x1,z1) at height y1.</summary>
        private struct Link
        {
            public string name;
            public float x0, z0, x1, z1, w, y0, y1;
        }

        // v0.9.9 вЂ” the yellow bars: roads that climb from one floor to the next (10-18 %), wide
        // enough for the game's vehicles; each one lands exactly on the edge of the upper terrace
        private static readonly Link[] ramps =
        {
            new Link { name = "ring_west", x0 = 56f,  z0 = 140f, x1 = 56f,  z1 = 210f, w = 11f, y0 = 0f,  y1 = 8f },
            new Link { name = "ring_east", x0 = 324f, z0 = 140f, x1 = 324f, z1 = 210f, w = 11f, y0 = 0f,  y1 = 8f },
            new Link { name = "east_band", x0 = 352f, z0 = 180f, x1 = 352f, z1 = 210f, w = 9f,  y0 = 5f,  y1 = 8f },
            new Link { name = "west_band", x0 = 8f,   z0 = 235f, x1 = 8f,   z1 = 210f, w = 9f,  y0 = 8f,  y1 = 11f },
            new Link { name = "wing",      x0 = 40f,  z0 = 255f, x1 = 0f,   z1 = 255f, w = 9f,  y0 = 8f,  y1 = 14f },
            new Link { name = "plinth",    x0 = 105f, z0 = 79f,  x1 = 160f, z1 = 79f,  w = 9f,  y0 = 0f,  y1 = 10f },
        };

        // the green bars: pedestrian stairs at the same edges (rise <= 0.23 m, the character
        // controller's step offset is 0.3 m)
        private static readonly Link[] stairs =
        {
            new Link { name = "stair_z3",     x0 = 137f, z0 = 192f, x1 = 137f, z1 = 210f, w = 6f,  y0 = 0f,  y1 = 8f },
            new Link { name = "stair_plinth", x0 = 230f, z0 = 12f,  x1 = 230f, z1 = 40f,  w = 22f, y0 = 0f,  y1 = 10f },
            new Link { name = "stair_z4",     x0 = 44f,  z0 = 222f, x1 = 44f,  z1 = 210f, w = 5f,  y0 = 8f,  y1 = 11f },
            new Link { name = "stair_wing",   x0 = 12f,  z0 = 167f, x1 = 0f,   z1 = 167f, w = 5f,  y0 = 11f, y1 = 14f },
        };

        /// <summary>Is a point inside a ramp's or stair's footprint (widened by `pad`)?</summary>
        private static bool InLink(Link l, float x, float z, float pad)
        {
            float mx = 0.5f * l.w + pad;
            return x > Mathf.Min(l.x0, l.x1) - mx && x < Mathf.Max(l.x0, l.x1) + mx &&
                   z > Mathf.Min(l.z0, l.z1) - mx && z < Mathf.Max(l.z0, l.z1) + mx;
        }

        private static bool Linked(float x, float z, float pad)
        {
            for (int i = 0; i < ramps.Length; i++) if (InLink(ramps[i], x, z, pad)) return true;
            for (int i = 0; i < stairs.Length; i++) if (InLink(stairs[i], x, z, pad)) return true;
            return false;
        }

        /// <summary>A box whose local +Z runs along aв†’b; it spans [line+lift, line+lift+t] measured
        /// along the box's own up, so a ramp can be laid exactly through two given points.</summary>
        private static void Beam(Geo g, Vector3 a, Vector3 b, float w, float t, float lift, Vector3 off)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.05f) return;
            Quaternion r = Quaternion.LookRotation(d / len, Vector3.up);
            g.Box(0, (a + b) * 0.5f + off + r * Vector3.up * (lift + t * 0.5f), new Vector3(w, t, len), r, 0.4f);
        }

        /// <summary>The raised floors themselves: one concrete box per zone, plus the parapets along
        /// every edge that drops to a lower floor (the pink lines of the plan read as walls).</summary>
        private static void Terraces()
        {
            var g = new Geo { FloorMul = PlatformGrain };
            for (int i = 0; i < tiers.Length; i++)
            {
                var t = tiers[i];
                g.Box(0, new Vector3((t.x0 + t.x1) * 0.5f, (t.h - 0.5f) * 0.5f, (t.z0 + t.z1) * 0.5f),
                      new Vector3(t.x1 - t.x0, t.h + 0.5f, t.z1 - t.z0), Quaternion.identity, DeckUv);
            }
            var go = g.Emit(root, "terraces", mRough, null);
            for (int i = 0; i < tiers.Length; i++)
            {
                var t = tiers[i];
                var bc = go.AddComponent<BoxCollider>();
                bc.center = new Vector3((t.x0 + t.x1) * 0.5f, (t.h - 0.5f) * 0.5f, (t.z0 + t.z1) * 0.5f);
                bc.size = new Vector3(t.x1 - t.x0, t.h + 0.5f, t.z1 - t.z0);
                log.LogInfo($"[DISTRICT] terrace {t.name}: {t.x1 - t.x0:0}x{t.z1 - t.z0:0} m at +{t.h:0} m");
            }

            var r = new Geo();
            int rails = 0;
            for (int i = 0; i < tiers.Length; i++)
            {
                var t = tiers[i];
                rails += Rail(r, t, t.x0, t.z0, t.x1, t.z0);
                rails += Rail(r, t, t.x0, t.z1, t.x1, t.z1);
                rails += Rail(r, t, t.x0, t.z0, t.x0, t.z1);
                rails += Rail(r, t, t.x1, t.z0, t.x1, t.z1);
            }
            var rgo = r.Emit(root, "terrace_rails", mRough, null);
            AddMeshCollider(rgo, "terrace_rails");
            log.LogInfo($"[DISTRICT] terrace edges: {rails} parapet segments (open where a ramp or a stair arrives)");
        }

        /// <summary>One terrace edge: 1.05 m parapet segments wherever the other side is lower.</summary>
        private static int Rail(Geo g, Tier t, float ax, float az, float bx, float bz)
        {
            Vector3 a = new Vector3(ax, 0f, az), b = new Vector3(bx, 0f, bz);
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1.5f) return 0;
            d /= len;
            Vector3 mid = (a + b) * 0.5f;
            Vector3 outw = mid - new Vector3((t.x0 + t.x1) * 0.5f, 0f, (t.z0 + t.z1) * 0.5f);
            outw.y = 0f;
            if (outw.sqrMagnitude < 0.01f) return 0;
            outw = (outw - Vector3.Dot(outw, d) * d).normalized;

            int n = Mathf.Max(1, Mathf.CeilToInt(len / 3f));
            float step = len / n;
            int made = 0;
            for (int i = 0; i < n; i++)
            {
                Vector3 c = a + d * ((i + 0.5f) * step);
                // a higher or equally high neighbour is its own wall вЂ” no parapet on this side
                if (TierAt(c.x + outw.x * 0.9f, c.z + outw.z * 0.9f) > t.h - 0.4f) continue;
                if (Linked(c.x, c.z, 2.5f)) continue;     // the ramp / stair arrives here
                g.Box(0, c - outw * 0.2f + Vector3.up * (t.h + 0.525f),
                      new Vector3(Mathf.Abs(d.x) * step + Mathf.Abs(d.z) * 0.4f, 1.05f,
                                  Mathf.Abs(d.z) * step + Mathf.Abs(d.x) * 0.4f),
                      Quaternion.identity, 0.5f);
                made++;
            }
            return made;
        }

        /// <summary>The inclined roads: a slab through the two end points plus a parapet on each side.</summary>
        private static void Ramps()
        {
            for (int i = 0; i < ramps.Length; i++)
            {
                var l = ramps[i];
                Vector3 a = new Vector3(l.x0, l.y0, l.z0), b = new Vector3(l.x1, l.y1, l.z1);
                Vector3 d = b - a;
                float len = d.magnitude;
                if (len < 1f) continue;
                d /= len;
                Vector3 side = Vector3.Cross(Vector3.up, d).normalized;
                float o = l.w * 0.5f - 0.16f;
                var g = new Geo { FloorMul = PlatformGrain };
                Beam(g, a, b, l.w, 0.7f, -0.7f, Vector3.zero);                       // the deck
                Beam(g, a, b, 0.32f, 1.02f, 0f, side * o);                            // parapets
                Beam(g, a, b, 0.32f, 1.02f, 0f, -side * o);
                // fill under the high end, so the ramp does not read as a floating plate
                float tuck = 4f;
                Vector3 tail = a + d * Mathf.Max(0f, len - tuck);
                Beam(g, tail, b, l.w, 1.4f, -1.4f, Vector3.zero);
                var go = g.Emit(root, "ramp_" + l.name, mRough, null);
                AddMeshCollider(go, "ramp");
                log.LogInfo($"[DISTRICT] ramp {l.name}: {l.y0:0.0} -> {l.y1:0.0} m over {len:0.0} m " +
                            $"({(l.y1 - l.y0) / len * 100f:0}% slope), {l.w:0.0} m wide");
            }
        }

        /// <summary>The street stairs: solid treads from the lower floor to the edge of the upper one.</summary>
        private static void TierStairs()
        {
            for (int i = 0; i < stairs.Length; i++)
            {
                var l = stairs[i];
                Vector3 a = new Vector3(l.x0, l.y0, l.z0), b = new Vector3(l.x1, l.y1, l.z1);
                Vector3 d = b - a;
                float run = d.magnitude;
                if (run < 0.5f) continue;
                d /= run;
                float dh = l.y1 - l.y0;
                int n = Mathf.Max(3, Mathf.RoundToInt(dh / 0.22f));
                float rise = dh / n, tread = run / n;
                float bot = l.y0 - 0.5f;
                var g = new Geo { FloorMul = PlatformGrain };
                for (int s = 0; s < n; s++)
                {
                    float top = l.y0 + (s + 1) * rise;
                    Vector3 p = a + d * ((s + 0.5f) * tread);
                    g.Box(0, new Vector3(p.x, (top + bot) * 0.5f, p.z),
                          new Vector3(Mathf.Abs(d.z) * l.w + Mathf.Abs(d.x) * tread, top - bot,
                                      Mathf.Abs(d.x) * l.w + Mathf.Abs(d.z) * tread),
                          Quaternion.identity, 0.5f);
                }
                Vector3 side = Vector3.Cross(Vector3.up, d).normalized;
                float o = l.w * 0.5f - 0.15f;
                Beam(g, a, b, 0.3f, 0.95f, 0f, side * o);                             // cheek walls
                Beam(g, a, b, 0.3f, 0.95f, 0f, -side * o);
                var go = g.Emit(root, "stairs_" + l.name, mRough, null);
                AddMeshCollider(go, "stairs");
                log.LogInfo($"[DISTRICT] stairs {l.name}: {n} steps of {rise:0.00} m (tread {tread:0.00} m), " +
                            $"{l.y0:0.0} -> {l.y1:0.0} m, {l.w:0.0} m wide");
            }
        }

        /// <summary>
        /// v0.9.22 вЂ” the south-east corner, rebuilt as **one smooth height field** instead of four boxes.
        ///
        /// What the v0.9.20/21 build actually looked like was measured, not guessed (`[PROBE] corner
        /// z-profile` / `x-profile`, `CornerScan`): the level's own granite shelf sits at **+0,32 m over
        /// the whole corner** (x в‰€ 330-376, z -4 вЂ¦ 10), and our deck is at 0. The old pad was a 0,40 m
        /// plate on top of it, made of four `Beam` boxes вЂ” a flat plate, a 4 m ramp, a tongue and a 10 m
        /// bevel. Each of those boxes is a *closed* box with its own 0,9 m skirt, and they met only along
        /// their edges, so one box's top could stand up to 8 cm proud of the next one's (the flat plate's
        /// 0,40 against the ramp's 0,32 at the same z) вЂ” that is the step in the user's screenshot 1,
        /// and the seam kerb of `Deck()` (a 12 cm box) was the hard edge of screenshot 2.
        ///
        /// Now the corner is a **single triangulated surface** h(x,z) with no internal edges at all:
        ///  - the flat top is **0,345 m** вЂ” 2,5 cm over the granite, so the granite is buried and the
        ///    two never z-fight, but the concrete reads as the crest's own level (v0.9.23; it was 0,365);
        ///  - the **west** side is a 16 m smoothstep down to -0,35 m and the **south** side a 4,5 m one
        ///    (v0.9.23): both cross the deck's plane and disappear **inside** the deck slab, so the ramp's
        ///    foot has no edge вЂ” it just fades into the floor (the user's screenshot 3);
        ///  - the **north** side is a tongue that sinks to 0,30 m under the granite at z = -2,4, i.e.
        ///    the concrete comes out from **under** the crest's own stone instead of lying on top of it;
        ///  - the terrace's wall (z = CornerApron) and the sea parapet (x = Len) bury the other two
        ///    edges, so the surface has no visible boundary anywhere.
        /// The southern bevel is the v0.9.23 fix: with the west bevel only, the pad ended in a **bare
        /// 0,25-0,36 m vertical face** along z в‰€ 12,3 for x в‰€ 320-330 вЂ” taller than the controller's
        /// 0,30 m stepOffset, so it was an unclimbable plate that blocked the way north from the deck
        /// (measured by `CornerSteps`: `25,3-36,5 cm at (324,12) -> (324,14)`).
        /// The `Deck()` seam kerb is gone in this area (see `Deck`), because a 12 cm kerb is exactly the
        /// hard edge the user asked to remove.
        /// </summary>
        private static void CornerPad()
        {
            if (CornerApron <= 0.01f) return;
            float z1 = Mathf.Clamp(CornerApron, 0f, 60f);
            const float H = 0.345f;     // the corner's top where it stands on our own deck
            const float Z0 = 0.15f;     // the deck's own north edge (the platform's edge)
            const float XF = 328f;      // where the flat top ends and the west bevel begins
            const float XW = 312f;      // the bevel's tip вЂ” buried inside the deck slab
            const float HW = -0.35f;    // the bevel's tip height (the deck slab spans -0,5 вЂ¦ 0)
            const float DZS = 4.5f;     // v0.9.23: the length of the **south** bevel
            float zS = z1 + 0.3f;       // where the flat top ends (east of x = 330 the terrace's wall buries it)
            float zE = zS + DZS;        // вЂ¦ and where the south bevel is buried inside the deck slab

            Func<float, float> smooth = u => { u = Mathf.Clamp01(u); return u * u * (3f - 2f * u); };
            // v0.9.50 вЂ” **the level's granite wins.** Measured with the pad's collider off, the dam
            // crest's granite shelf (0,32 m) runs diagonally under this whole corner: at z = 6 it starts
            // at x в‰€ 336 and from there to the terrace the ground *is* the granite. The v0.9.45 tongue
            // and the v0.9.47 north ramp both left the pad **25 mm** proud of that stone вЂ” the thin plate
            // the user walks around and climbs onto in their video. Now every vertex asks the scene what
            // is under it and, over the granite shelf, the surface is clamped to **10 mm** above the
            // stone: the concrete still covers the corner (the pad's look is unchanged), but the edge
            // is a hairline instead of a ledge вЂ” nothing to climb on, nothing to see under.
            Func<float, float, float> under = (x, z) =>
            {
                Vector3 w = root.TransformPoint(new Vector3(x, 40f, z));
                if (Physics.Raycast(w, Vector3.down, out var hit, 120f, ~0, QueryTriggerInteraction.Ignore))
                {
                    float gy = hit.point.y - root.position.y;
                    if (gy > 0.25f && gy < 0.80f) return gy + 0.010f;    // the granite shelf
                }
                return H;
            };
            Func<float, float, float> height = (x, z) =>
            {
                float wx = smooth((XF - x) / (XF - XW));
                float wz = smooth((z - zS) / DZS);
                float h = HW + (H - HW) * (1f - wx) * (1f - wz);
                return Mathf.Min(h, under(x, z));
            };

            var g = new Geo { FloorMul = PlatformGrain };
            int quads = 0;

            // ---- the top surface: one continuous grid, no internal seam ----------------------------
            const float DX = 2f;
            var zRows = new List<float>();
            float dzA = (zS - Z0) / 9f;
            for (int i = 0; i <= 9; i++) zRows.Add(Z0 + i * dzA);               // the flat top
            for (int i = 1; i <= 6; i++) zRows.Add(zS + i * (DZS / 6f));        // the south bevel
            int nx = Mathf.RoundToInt((Len - XW) / DX);
            for (int ix = 0; ix < nx; ix++)
            {
                float x0 = XW + ix * DX, x1 = x0 + DX;
                for (int iz = 0; iz < zRows.Count - 1; iz++)
                {
                    float z0 = zRows[iz], zr1 = zRows[iz + 1];
                    float cx = (x0 + x1) * 0.5f, cz = (z0 + zr1) * 0.5f;
                    float gx = (height(cx + 0.5f, cz) - height(cx - 0.5f, cz));
                    float gz = (height(cx, cz + 0.5f) - height(cx, cz - 0.5f));
                    Vector3 n = new Vector3(-gx, 1f, -gz).normalized;
                    g.Quad(0,
                           new Vector3(x0, height(x0, z0), z0),
                           new Vector3(x1, height(x1, z0), z0),
                           new Vector3(x1, height(x1, zr1), zr1),
                           new Vector3(x0, height(x0, zr1), zr1), n, 0.5f);
                    quads++;
                }
            }

            // ---- the three free edges get a skirt down into the deck, so the surface reads as a
            //      solid slab from the water side and never as a paper-thin sheet -------------------
            const float bot = -0.6f;
            for (float x = XW; x < Len - 0.01f; x += DX)          // the platform's edge (north face)
            {
                float x1 = Mathf.Min(x + DX, Len);
                float t0 = height(x, Z0), t1 = height(x1, Z0);
                g.Quad(0, new Vector3(x, t0, Z0 - 0.01f), new Vector3(x1, t1, Z0 - 0.01f),
                       new Vector3(x1, bot, Z0 - 0.01f), new Vector3(x, bot, Z0 - 0.01f),
                       Vector3.back, 0.5f);
                quads++;
            }

            var go = g.Emit(root, "corner_pad", mRough, null);
            AddMeshCollider(go, "corner_pad");
            log.LogInfo($"[DISTRICT] corner pad: {Len - XW:0} x {zE:0.#} m as ONE smooth height field " +
                        $"(grid {DX:0.#} m): flat {H:0.000} m from x={XF:0} to the sea parapet and z={Z0:0.##} " +
                        $"to the terrace's wall, a 16 m bevel down to {HW:0.00} m on the west and a {DZS:0.#} m " +
                        $"bevel down to {HW:0.00} m on the south, both of which dive inside the deck slab; " +
                        $"v0.9.50: the inland tongue is gone and over the level's granite shelf the surface " +
                        $"is clamped 10 mm above the stone, so its edge is a hairline instead of a ledge " +
                        $"({quads} quads)");
        }

        private static void Deck()
        {
            // slab: the main platform hangs off the crest into the sea; a western tongue (the white
            // zone on the plan) reaches past it and carries the outlying tower
            var deck = new Geo { FloorMul = PlatformGrain };
            deck.Box(0, new Vector3(Len * 0.5f, -1.3f, Wid * 0.5f + 0.1f),
                     new Vector3(Len, 2.6f, Wid - 0.1f), Quaternion.identity, DeckUv);
            deck.Box(0, new Vector3(-Axw * 0.5f, -1.3f, (Az0 + Wid) * 0.5f + 0.05f),
                     new Vector3(Axw, 2.6f, Wid - Az0 - 0.1f), Quaternion.identity, DeckUv);
            var go = deck.Emit(root, "deck", mRough, null);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(Len * 0.5f, -1.3f, Wid * 0.5f);   // must match the mesh, or the far half has no floor
            col.size = new Vector3(Len, 2.6f, Wid);
            var col2 = go.AddComponent<BoxCollider>();
            col2.center = new Vector3(-Axw * 0.5f, -1.3f, (Az0 + Wid) * 0.5f);
            col2.size = new Vector3(Axw, 2.6f, Wid - Az0);

            // outer dam wall: the old crest continues as a 16 m wall standing in the sea
            var wall = new Geo();
            const float wallTop = 1.15f;    // parapet height above the deck
            const float wallBot = -15.0f;   // down to below the sea surface
            float wallH = wallTop - wallBot;
            float wy = wallBot + wallH * 0.5f;
            float ncx = (Len - Axw) * 0.5f;                 // centre of the full-width seaward wall
            // seaward (north) face, from the tip of the tongue to the east end
            wall.Box(0, new Vector3(ncx, wy, Wid - 2.5f),
                     new Vector3(Len + Axw, wallH, 5f), Quaternion.identity, 0.22f);
            // east end of the platform.
            // v0.9.51: at the south-east corner the parapet is **cut** вЂ” over the corner apron its
            // top is lowered to the pad's own level (0,35 m), so the +5 m terrace's south wall runs
            // its full length with nothing of the platform's foundation standing in front of it.
            // This is a real cut in the geometry (two boxes instead of one), not a patch laid over it.
            float cutZ = Mathf.Clamp(CornerApron, 0f, 60f) + 0.3f;
            const float cutTop = 0.34f;
            float cutH = cutTop - wallBot;
            float cutY = wallBot + cutH * 0.5f;
            wall.Box(0, new Vector3(Len - 2.5f, cutY, cutZ * 0.5f),
                     new Vector3(5f, cutH, cutZ), Quaternion.identity, 0.22f);
            wall.Box(0, new Vector3(Len - 2.5f, wy, (cutZ + Wid) * 0.5f),
                     new Vector3(5f, wallH, Wid - cutZ), Quaternion.identity, 0.22f);
            // west end of the tongue
            wall.Box(0, new Vector3(-Axw + 2.5f, wy, (Az0 + Wid) * 0.5f),
                     new Vector3(5f, wallH, Wid - Az0), Quaternion.identity, 0.22f);
            // the tongue's inland face
            wall.Box(0, new Vector3(-Axw * 0.5f, wy, Az0 + 2.5f),
                     new Vector3(Axw, wallH, 5f), Quaternion.identity, 0.22f);
            // west end of the main platform, from the crest to the tongue
            wall.Box(0, new Vector3(2.5f, wy, Az0 * 0.5f),
                     new Vector3(5f, wallH, Az0), Quaternion.identity, 0.22f);
            // buttresses on the seaward faces, like the old dam
            for (float x = -Axw + 10f; x < Len - 4f; x += 15f)
                wall.Box(0, new Vector3(x, wy, Wid - 0.4f), new Vector3(2.2f, wallH, 1.8f), Quaternion.identity, 0.22f);
            for (float z = Az0 + 7f; z < Wid - 4f; z += 15f)
                wall.Box(0, new Vector3(-Axw + 0.4f, wy, z), new Vector3(1.8f, wallH, 2.2f), Quaternion.identity, 0.22f);
            for (float z = 8f; z < Az0 - 4f; z += 15f)
                wall.Box(0, new Vector3(0.4f, wy, z), new Vector3(1.8f, wallH, 2.2f), Quaternion.identity, 0.22f);
            // heavy coping on top of the parapets
            wall.Box(0, new Vector3(ncx, wallTop + 0.12f, Wid - 2.5f),
                     new Vector3(Len + Axw + 0.6f, 0.24f, 5.4f), Quaternion.identity, 0.30f);
            wall.Box(0, new Vector3(Len - 2.5f, wallTop + 0.12f, (cutZ + Wid) * 0.5f),
                     new Vector3(5.4f, 0.24f, Wid - cutZ), Quaternion.identity, 0.30f);
            wall.Box(0, new Vector3(-Axw + 2.5f, wallTop + 0.12f, (Az0 + Wid) * 0.5f),
                     new Vector3(5.4f, 0.24f, Wid - Az0), Quaternion.identity, 0.30f);
            wall.Box(0, new Vector3(-Axw * 0.5f, wallTop + 0.12f, Az0 + 2.5f),
                     new Vector3(Axw + 0.6f, 0.24f, 5.4f), Quaternion.identity, 0.30f);
            wall.Box(0, new Vector3(2.5f, wallTop + 0.12f, Az0 * 0.5f),
                     new Vector3(5.4f, 0.24f, Az0), Quaternion.identity, 0.30f);
            for (int i = 0; i <= 24; i++)   // drain slots / pilaster rhythm on the inside face
            {
                float x = -Axw + 6f + i * (Len + Axw - 12f) / 24f;
                wall.Box(0, new Vector3(x, 0.55f, Wid - 5.3f), new Vector3(1.4f, 1.1f, 0.35f), Quaternion.identity, 0.5f);
            }
            go = wall.Emit(root, "dam_wall", mRough, null);
            var bc = go.AddComponent<BoxCollider>();
            bc.center = new Vector3(ncx, wy, Wid - 2.5f);
            bc.size = new Vector3(Len + Axw, wallH, 5f);
            // every other wall gets its own collider so the player cannot walk off the deck
            AddWallCollider("parapet_sea", new Vector3(ncx, 0.6f, Wid - 2.5f), new Vector3(Len + Axw, 1.2f, 5f));
            AddWallCollider("parapet_east", new Vector3(Len - 2.5f, 0.6f, (cutZ + Wid) * 0.5f), new Vector3(5f, 1.2f, Wid - cutZ));
            AddWallCollider("parapet_west_annex", new Vector3(-Axw + 2.5f, 0.6f, (Az0 + Wid) * 0.5f), new Vector3(5f, 1.2f, Wid - Az0));
            AddWallCollider("parapet_annex_inland", new Vector3(-Axw * 0.5f, 0.6f, Az0 + 2.5f), new Vector3(Axw, 1.2f, 5f));
            AddWallCollider("parapet_west_main", new Vector3(2.5f, 0.6f, Az0 * 0.5f), new Vector3(5f, 1.2f, Az0));

            // v0.9.22: the 12 cm "seam kerb" that used to run along the junction (a 1,1 x 0,12 m box at
            // z=0,55) is gone. The user's screenshot 2 was exactly that hard edge, and the corner's new
            // height field fades into the deck on its own вЂ” a kerb would only put a step back.
        }

        private static void AddWallCollider(string name, Vector3 c, Vector3 s)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var bc = go.AddComponent<BoxCollider>();
            bc.center = c;
            bc.size = s;
        }

        // ------------------------------------------------------------------ ground: streets, yards

        private static void Streets()
        {
            var g = new Geo { FloorMul = PlatformGrain };
            // the ring road, laid out around the terraces (v0.9.9): the two legs hug the +11 m west
            // band (x <= 48) and the +5 m east band (x >= 330), so the roads stay on the base deck
            // until the ramps lift them onto the +8 m seaward terrace
            const float rw = 10f;                 // road width
            const float xw = 56f, xe = 324f;      // the two legs of the ring
            const float zs = 52f, zn = Wid - 52f; // 52 (crest side) and 248 (on the seaward terrace)
            // crest-side leg: interrupted by the plinth of the House of Soviets (x 160..305)
            Road(g, xw - rw * 0.5f, 160f, zs - rw * 0.5f, zs + rw * 0.5f, 0f);
            Road(g, 305f, xe + rw * 0.5f, zs - rw * 0.5f, zs + rw * 0.5f, 0f);
            // seaward leg: on top of the +8 m terrace
            Road(g, xw - rw * 0.5f, xe + rw * 0.5f, zn - rw * 0.5f, zn + rw * 0.5f, 8f);
            // the legs: base level up to the foot of the ramp, then on the terrace
            Road(g, xw - rw * 0.5f, xw + rw * 0.5f, zs, 140f, 0f);
            Road(g, xw - rw * 0.5f, xw + rw * 0.5f, 210f, zn, 8f);
            Road(g, xe - rw * 0.5f, xe + rw * 0.5f, zs, 140f, 0f);
            Road(g, xe - rw * 0.5f, xe + rw * 0.5f, 210f, zn, 8f);
            // approach avenue from the crest to the grand stair of the House of Soviets
            Road(g, 218f, 242f, 4f, 14f, 0f);
            var sgo = g.Emit(root, "streets", mRough, null);
            AddMeshCollider(sgo, "streets");

            // v0.9.4: no grass/dirt patches on the deck any more вЂ” the platform must read as one concrete
            // field like the dam (the coloured "patches" in the plan view were these). The draws are kept
            // so the deterministic layout (seed 20261023) does not shift between builds: 44 grass plots
            // and 10 dirt plots, 5 doubles each.
            for (int i = 0; i < (44 + 10) * 5; i++) rng.NextDouble();
        }

        /// <summary>One flat road box at a given height (the streets follow the terraces now).</summary>
        private static void Road(Geo g, float x0, float x1, float z0, float z1, float y)
        {
            if (x1 - x0 < 0.5f || z1 - z0 < 0.5f) return;
            g.Box(0, new Vector3((x0 + x1) * 0.5f, y + 0.025f, (z0 + z1) * 0.5f),
                  new Vector3(x1 - x0, 0.05f, z1 - z0), Quaternion.identity, DeckUv);
        }

        private static void AddMeshCollider(GameObject go, string tag)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = false;
        }

        // ------------------------------------------------------------------ the district

        private static void Layout()
        {
            // v0.9.8: the platform is 380 x 300 m (+80 m seaward, the contour of the user's plan B) and
            // the whole district sits 15 m west of the sea hole, so the ring has room for a full band
            // more. The order of the drawing is kept: a chain of blocks along every edge, the House of
            // Soviets and its square in the open middle, rows and columns inside, and a covered bridge
            // at every gap between neighbours.

            // --- the seaward edge (the promenade side): an outer row and a staggered inner row
            RowX(26f, Len - 26f, Wid - 26f, J(3f));
            RowX(74f, 312f, Wid - 74f, J(5f));

            // --- the band behind the House of Soviets: one continuous row right across the platform
            // (v0.9.9: it stops clear of the +11 m west band, the +5 m east band and both ramp roads)
            RowX(70f, 312f, 178f, J(4f));

            // --- the flanks: blocks turned 90 deg, so their corridors run seaward. They stand on the
            // raised bands (west +11 m, east +5 m) and stop clear of the ramps onto the seaward terrace
            RowZ(46f, Wid - 96f, 30f, 90f + J(3f));
            int ne = bldgs.Count;
            RowZ(46f, 168f, 354f, 90f + J(3f));
            // v0.9.73: the **bas-relief** block вЂ” the middle one of the east flank. Its outward long
            // wall (local +z, which faces world +x after the 90В° row yaw) is built solid and carries
            // the dark abstract ornament, so the wall that faces *out of the district* is the one.
            if (bldgs.Count > ne + 1)
            {
                var rb = bldgs[ne + 1];
                rb.winPlusZ = false;
                rb.balconies = false;
                rb.bands = false;
                rb.risalit = false;
                rb.relief = true;
                reliefBlock = rb;
            }

            // --- a column inside the west flank (the drawing has a row there as well), on the base deck
            RowZ(62f, 150f, 76f, 90f + J(3f));

            // --- and one point tower in the open square west of the House (it fills the gap between
            // the inner column and the House's plinth without crowding the approaches)
            Place(122f, 118f, 6f, 2, 0.85f);

            // --- the front along the dam crest, with the approach to the square left open
            RowX(56f, 168f, 26f, J(3f));
            // v0.9.19: **one house on legs** (the user's screenshots 1-2): the row's two blocks are joined
            // into a single rectangular mass вЂ” same depth, no gap, one yaw вЂ” whose western part stays the
            // lower one (the floor counts are never touched, only the plan)
            int nl = bldgs.Count;
            RowX(246f, 328f, 26f, J(3f));
            for (int i = nl; i < bldgs.Count; i++) bldgs[i].legs = true;
            if (bldgs.Count == nl + 2)
            {
                var west = bldgs[nl];
                var east = bldgs[nl + 1];
                float deep = Mathf.Max(west.d, east.d);
                west.d = east.d = deep;
                west.yaw = east.yaw = 0f;
                west.p = new Vector3(west.p.x, west.p.y, 26f);
                east.p = new Vector3(west.p.x + (west.w + east.w) * 0.5f, west.p.y, 26f);
                west.legsShut = 1;      // the shared face needs no pylons: the two houses are one
                east.legsShut = -1;
                log.LogInfo($"[LEGS] the pair is one house in plan: {west.w:0.0} + {east.w:0.0} = " +
                            $"{west.w + east.w:0.0} x {deep:0.0} m at x {west.p.x - west.w * 0.5f:0.0}.." +
                            $"{east.p.x + east.w * 0.5f:0.0}, z 26, yaw 0 (west {west.floors} floors, " +
                            $"east {east.floors} floors вЂ” heights unchanged)");
            }

            // --- the outlying tower on the western wing (the house marked on the plan), +14 m
            // v0.9.18: **this** is the block on the peninsula вЂ” the user's screenshot 1 вЂ” so its roof
            // gets the penthouse (see `Penthouse`). The flag is set on the block that was just placed,
            // not on the last one in the list, so a rejected plot cannot wear it by mistake.
            int nb = bldgs.Count;
            Place(-40f, 225f, 3f, 2, 1.25f);
            if (PenthouseOn && bldgs.Count > nb) bldgs[nb].penthouse = true;
        }

        private static float J(float a) => ((float)rng.NextDouble() * 2f - 1f) * a;

        private static float R() => (float)rng.NextDouble();

        private static float Gap() => 4f + R() * 8f;     // 4-12 m between neighbours in a row

        /// <summary>One row along x: the blocks are laid end to end, so no plot is ever rejected.</summary>
        private static void RowX(float x0, float x1, float z, float yaw)
        {
            int n = bldgs.Count;
            float x = x0;
            while (true)
            {
                int type = Kind();
                var s = RollStyle(type, 1f);
                if (x + s.w > x1) break;
                AddBlock(new Vector3(x + s.w * 0.5f, 0f, z), yaw, type, s);
                x += s.w + Gap();
            }
            log.LogInfo($"[DISTRICT] row along x at z={z:0.0} (x {x0:0}..{x1:0}): {bldgs.Count - n} blocks");
        }

        /// <summary>One row along z; the blocks are turned 90 deg, so their long axis runs seaward.</summary>
        private static void RowZ(float z0, float z1, float x, float yaw)
        {
            int n = bldgs.Count;
            float z = z0;
            while (true)
            {
                int type = Kind();
                var s = RollStyle(type, 1f);
                if (z + s.w > z1) break;
                AddBlock(new Vector3(x, 0f, z + s.w * 0.5f), yaw, type, s);
                z += s.w + Gap();
            }
            log.LogInfo($"[DISTRICT] row along z at x={x:0.0} (z {z0:0}..{z1:0}): {bldgs.Count - n} blocks");
        }

        private static int Kind()
        {
            double r = rng.NextDouble();
            return r < 0.44 ? 0 : (r < 0.68 ? 1 : (r < 0.86 ? 2 : 3));
        }

        private static void Place(float x, float z, float yaw, int type, float size = 1f)
            => MakeBuilding(new Vector3(x, 0f, z), yaw, type, size);

        /// <summary>True if a point is on the platform (main rectangle or the western tongue).</summary>
        private static bool OnDeck(float x, float z, float m)
        {
            if (x > 3f + m && x < Len - 3f - m && z > 3f + m && z < Wid - 6f - m) return true;
            if (x > -Axw + 3f + m && x < -3f - m && z > Az0 + 3f + m && z < Wid - 3f - m) return true;
            return false;
        }

        private static void Axes(float yaw, out Vector2 ex, out Vector2 ez)
        {
            float c = Mathf.Cos(yaw * Mathf.Deg2Rad), s = Mathf.Sin(yaw * Mathf.Deg2Rad);
            ex = new Vector2(c, -s);     // local +x in XZ
            ez = new Vector2(s, c);      // local +z in XZ
        }

        private static float Radius(Vector2 u, Vector2 ex, Vector2 ez, float w, float d)
            => Mathf.Abs(Vector2.Dot(u, ex)) * w * 0.5f + Mathf.Abs(Vector2.Dot(u, ez)) * d * 0.5f;

        /// <summary>Separating-axis test of two rotated footprints (exact, unlike a bounding box).</summary>
        private static bool Overlap(Vector3 pa, float wa, float da, float ya,
                                    Vector3 pb, float wb, float db, float yb, float margin)
        {
            Vector2 exa, eza, exb, ezb;
            Axes(ya, out exa, out eza);
            Axes(yb, out exb, out ezb);
            var d = new Vector2(pb.x - pa.x, pb.z - pa.z);
            for (int k = 0; k < 4; k++)
            {
                Vector2 u = k == 0 ? exa : (k == 1 ? eza : (k == 2 ? exb : ezb));
                float r = Radius(u, exa, eza, wa, da) + Radius(u, exb, ezb, wb, db) + margin;
                if (Mathf.Abs(Vector2.Dot(d, u)) >= r) return false;
            }
            return true;
        }

        /// <summary>Footprint test: on the platform, clear of the other blocks and of the square.</summary>
        private static bool Free(Vector3 p, float w, float d, float yaw, float margin)
        {
            // all four corners must be on the deck
            Vector2 ex, ez;
            Axes(yaw, out ex, out ez);
            for (int k = 0; k < 4; k++)
            {
                float sx = (k == 0 || k == 3) ? -1f : 1f;
                float sz = (k < 2) ? -1f : 1f;
                float cx = p.x + ex.x * sx * w * 0.5f + ez.x * sz * d * 0.5f;
                float cz = p.z + ex.y * sx * w * 0.5f + ez.y * sz * d * 0.5f;
                if (!OnDeck(cx, cz, 2f)) return false;
            }
            for (int i = 0; i < bldgs.Count; i++)
            {
                var b = bldgs[i];
                if (Overlap(p, w, d, yaw, b.p, b.w, b.d, b.yaw, margin)) return false;
            }
            // the House of Soviets and its square are reserved
            float c = Mathf.Abs(Mathf.Cos(yaw * Mathf.Deg2Rad)), s = Mathf.Abs(Mathf.Sin(yaw * Mathf.Deg2Rad));
            float hx = c * w * 0.5f + s * d * 0.5f;
            float hz = s * w * 0.5f + c * d * 0.5f;
            if (p.x + hx > 165f && p.x - hx < 300f && p.z + hz > 45f && p.z - hz < 125f) return false;
            return true;
        }

        /// <summary>Rolls the footprint of one block type (already +30 % in every plane).</summary>
        private static BlockStyle RollStyle(int type, float size)
        {
            float w, d;
            int baseFloors;
            switch (type)
            {
                case 0:   // khrushchevka: panel block
                    w = 24f + R() * 7f;
                    d = 12f + R() * 3f;
                    baseFloors = 5;
                    break;
                case 1:   // stalinka: heavier walls, cornice, risalit
                    w = 26f + R() * 8f;
                    d = 14f + R() * 3f;
                    baseFloors = 7;
                    break;
                case 2:   // point tower
                    w = 16f + R() * 4f;
                    d = w;
                    baseFloors = 9 + rng.Next(0, 4);
                    break;
                default:  // slab
                    w = 31f + R() * 7f;
                    d = 13f + R() * 3f;
                    baseFloors = 9;
                    break;
            }
            return new BlockStyle { w = w * size * Grow, d = d * size * Grow, baseFloors = baseFloors };
        }

        /// <summary>
        /// v0.9.13 вЂ” how many times the 2.2 m corridor is widened on this plot: 2, 3 or 4. Arbitrary per
        /// house, but a pure function of the plot coordinates, so the seeded layout stays reproducible
        /// and `AddBlock` never draws from the RNG for it.
        /// </summary>
        private static int CorridorMul(Vector3 p)
        {
            int h = Mathf.RoundToInt(p.x * 8f) * 73856093 ^ Mathf.RoundToInt(p.z * 8f) * 19349663;
            h = (h ^ (h >> 13)) * 1274126177;
            return 2 + Mathf.Abs((h >> 9) % 3);
        }

        private static void AddBlock(Vector3 p, float yaw, int type, BlockStyle s)
        {
            // v0.9.13: a wide corridor is taken out of the block's depth, so the rooms keep their size
            // and the house itself gets deeper вЂ” the user asked for corridors 2x/3x/4x as wide
            int cm = CorridorMul(p);
            float d = s.d + (cm - 1) * CORR;
            var b = new Bldg { p = p, yaw = yaw, w = s.w, d = d, corrMul = cm };
            // v0.9.4: every block is the city's own concrete (`M_ConcreteRough00`, the dam's material);
            // v0.9.76: **one tone for all** вЂ” the panel-block mix was removed (the user: some houses
            // were lighter than the others).
            b.wall = mRough;
            switch (type)
            {
                case 0: b.balconies = true; break;
                case 1: b.risalit = true; break;
                case 2: b.bands = true; break;
            }
            // v0.9.12: the tall storey is no longer a stalinka speciality вЂ” every block in the district
            // is built with it (the user asked for the stalinka's proportions everywhere): 5.6 m
            // ceilings, +30 % door openings, and windows grown to match. No RNG is drawn here on
            // purpose, so the seeded layout stays exactly the same as in v0.9.10.
            b.fh = FH * TallFh;        // 5.6 m ceilings
            b.dh = TallDoor;           // +30 % door openings
            b.winH = 2.2f;             // taller windows for the taller storey
            b.winW = 1.55f;
            b.sill = 1.1f;
            // the whole district was raised: six storeys on top, 3-10 more per house, and +30 % height
            b.floors = Mathf.RoundToInt((s.baseFloors + 6 + rng.Next(3, 11)) * Grow);
            rng.NextDouble();   // the old facade-variety roll вЂ” kept so the seeded layout does not shift

            // v0.9.9: the block stands on the terrace that covers it вЂ” and never on two of them,
            // otherwise its floor would float above the lower half of its footprint
            float th = TierAt(p.x, p.z);
            Vector2 ex, ez;
            Axes(yaw, out ex, out ez);

            // v0.9.13: the first multiplier that still fits the plot wins; a deep house that would
            // overlap a neighbour or straddle a terrace edge falls back to a narrower corridor instead
            // of leaving a hole in the ring
            bool fits = false;
            int cmOk = 1;
            float dOk = s.d;
            for (int m = cm; m >= 1; m--)
            {
                float dd = s.d + (m - 1) * CORR;
                if (!Free(p, s.w, dd, yaw, 2.2f)) continue;
                bool straddle = false;
                for (int k = 0; k < 4 && !straddle; k++)
                {
                    float sx = (k == 0 || k == 3) ? -1f : 1f;
                    float sz = (k < 2) ? -1f : 1f;
                    float cx = p.x + ex.x * sx * s.w * 0.5f + ez.x * sz * dd * 0.5f;
                    float cz = p.z + ex.y * sx * s.w * 0.5f + ez.y * sz * dd * 0.5f;
                    if (Mathf.Abs(TierAt(cx, cz) - th) > 0.01f) straddle = true;
                }
                if (straddle) continue;
                cmOk = m;
                dOk = dd;
                fits = true;
                break;
            }
            if (!fits)
            {
                skipCount++;
                log.LogInfo($"[DISTRICT] skipped a {s.w:0.0}x{s.d:0.0} m block at {p.x:0.0},{p.z:0.0}: no fit");
                return;
            }
            b.corrMul = cmOk;
            b.d = dOk;
            b.baseY = th + BASE;
            b.plinth = th < 0.01f;    // on a terrace the mass itself hides the seam

            bldgs.Add(b);
        }

        private static void MakeBuilding(Vector3 p, float yaw, int type, float size = 1f)
            => AddBlock(p, yaw, type, RollStyle(type, size));

        // ------------------------------------------------------------------ one block: shell, floors, corridor, stairs

        /// <summary>
        /// The interior of one block, measured before anything is built: room cells along x, the
        /// corridor band, the stair hall. The facades use the same numbers to place their windows, so
        /// an opening always sits in the middle of a room and never in front of a partition or a flight.
        /// </summary>
        private static Shell ShellOf(Bldg b, float hw, float hd)
        {
            var sh = new Shell
            {
                hw = hw,
                hd = hd,
                y0 = b.baseY,
                y1 = b.Top,
                fh = b.fh,
                dh = b.dh,
                tall = b.Tall,
                x0 = -hw + T,
                x1 = hw - T,
                zi = hd - T,
                corr = b.CorrHalf      // v0.9.13: 1.1 / 2.2 / 3.3 / 4.4 m half-width (2x/3x/4x)
            };
            sh.seg = Mathf.Max(2, Mathf.RoundToInt((sh.x1 - sh.x0) / 4.6f));
            sh.segLen = (sh.x1 - sh.x0) / sh.seg;
            sh.kS = sh.seg / 2;                       // the stair hall is the middle-north room
            sh.xs = sh.x0 + sh.kS * sh.segLen;
            PlanStair(sh);
            return sh;
        }

        /// <summary>
        /// v0.9.16 вЂ” the stair of one block, planned before anything is built.
        ///
        /// The user's two reports were the same bug: the flights were laid out inside
        /// `StairsTall`/`Stairs` from their own constants (`corr + 1.5`, `zi в€’ corr в€’ 3`) while the
        /// slab hole was cut from another set, so a block with a **wide corridor and a shallow
        /// footprint** got a flight crammed into 1,16 m вЂ” or, once `run &lt; 2`, no stair at all вЂ” and a
        /// floor plate above it either way ("СѓРїРёСЂР°СЋС‚СЃСЏ РІ РіР»СѓС…РѕР№ РїРѕС‚РѕР»РѕРє"). And the flights themselves
        /// came out 1,2вЂ¦4,9 m long instead of the 2вЂ“3 m the user asked for.
        ///
        /// Now the plan is computed once and both the hole and the flights read it:
        ///
        ///     z:  [ wall ] corridor | arrival landing | в”Ђв”Ђ flight в”Ђв”Ђ | half-landing | [ wall ]
        ///                            stLand            stRun            stLand
        ///
        /// `stZHigh + stLand == zi` **exactly**, so the hole always ends on the outer wall and the
        /// half-landing always reaches it (no unrailed platform hanging in the room).
        /// </summary>
        private static void PlanStair(Shell sh)
        {
            const float runMin = 2.0f, runMax = 3.0f;   // the user's spec, every building
            const float landMin = 1.1f;                 // a landing you can actually stand on
            float depth = sh.zi - sh.corr;              // corridor wall в†’ facade, inside
            float run = Mathf.Clamp(depth - 2f * landMin, runMin, runMax);
            if (run > depth - 1.4f)                     // a hall too shallow for that: it wins
                run = Mathf.Max(1.2f, depth - 1.4f);
            float land = Mathf.Max(0.7f, (depth - run) * 0.5f);
            sh.stRun = run;
            sh.stLand = land;
            sh.stZLow = sh.corr + land;
            sh.stZHigh = sh.stZLow + run;
            // two flights share the storey, so each rises half of it; ~0.175 m risers like a real stair
            sh.stN = Mathf.Max(4, Mathf.RoundToInt(sh.fh * 0.5f / 0.175f));
            sh.stRise = sh.fh * 0.5f / sh.stN;
            sh.stTread = run / sh.stN;
            // v0.9.35: a real open well between the flights (the user's reference: a vertical slot
            // between the railings). It is part of the slab hole, so it runs the whole height of the
            // house; the flights keep their 2 m width.
            sh.stWell = Mathf.Clamp(sh.segLen * 0.15f, 0.55f, 0.75f);
            sh.stFw = (sh.segLen - sh.stWell) * 0.5f;

            // evidence вЂ” one line per block, against what v0.9.15 built there
            float oldRun = sh.zi - sh.corr - 3f;
            string old = oldRun < 2f
                ? $"v0.9.15 built NO stair here (old run {oldRun:0.00} m)"
                : $"old flight {oldRun:0.00} m";
            log.LogInfo($"[STAIR] {sh.hw * 2f:0.0}x{sh.hd * 2f:0.0} m, corridor {2f * sh.corr:0.0} m clear: " +
                        $"{old} -> flight {run:0.00} m, {sh.stN} risers of {sh.stRise:0.000} m, " +
                        $"tread {sh.stTread:0.000} m, landings {land:0.00} m, " +
                        $"well {sh.stWell:0.00} m (flights {sh.stFw:0.00} m), shaft z {sh.stZLow:0.00}..{sh.zi:0.00}");
        }

        /// <summary>
        /// v0.9.35 вЂ” the plan of the **street-level core** of a block on legs (`Legs`), computed before
        /// anything is built so the stair and the hole it needs in the block's first-floor slab cannot
        /// disagree. That is exactly the bug PlanStair fixed for the houses' own stairs, and it is the
        /// user's "РІ РЅРµРєРѕС‚РѕСЂС‹С… РґРѕРјР°С… РїСЂРѕР»РµС‚С‹ СѓРїРёСЂР°СЋС‚СЃСЏ РІ РїРѕС‚РѕР»РѕС‡РЅС‹Рµ РїРµСЂРµРєСЂС‹С‚РёСЏ": the core's three
        /// flights climbed 16,8 m and the last one ended **inside** the block's solid ground slab, so
        /// the houses on pylons could not be entered from the street at all.
        ///
        ///     z:  [ core wall ] | exit landing | в”Ђ flight B в”Ђ | half-landing | в”Ђ flight A в”Ђ | [ wall ]
        ///          4,31 .. 4,69    4,21 .. 2,71     2,71 .. -0,9      -0,9 .. 0,6 вЂ¦
        ///
        /// The stair now runs **northwards to its exit** (`AtticStair` with `zSign = -1`), so its exit
        /// landing lands under the block's stair hall вЂ” the middle-north room, whose first-flight foot
        /// is at the same x. `coreHoleZ0` is where the top flight's headroom would drop under 2 m.
        /// </summary>
        private static void PlanCore(Bldg b)
        {
            float cz = Mathf.Min(4.9f, b.d * 0.30f);
            const float cwt = 0.40f;
            b.coreX0 = -1.9f;
            b.coreX1 = -0.2f;
            b.coreZ0 = cz - cwt - PT * 0.5f - 0.10f;   // clear of the core's north wall
            b.coreHoleZ0 = b.coreZ0 - 4.5f;            // below this the flight has < 2 m of headroom
            b.coreHoleZ1 = b.coreZ0 + 0.02f;           // 2 cm past the landing: no coplanar slab/landing faces
            log.LogInfo($"[CORE] {b.w:0.0}x{b.d:0.0} m block on legs: street stairlane x {b.coreX0:0.00}" +
                        $"..{b.coreX1:0.00} m, exit landing z {b.coreZ0 - 1.5f:0.00}..{b.coreZ0:0.00} m, " +
                        $"first-floor opening z {b.coreHoleZ0:0.00}..{b.coreZ0:0.00} m (tops out in the " +
                        $"stair hall, 3 flights of {b.fh:0.0} m)");
        }

        /// <summary>Clear spans (building-local x) of a long facade: one per room cell, none over the
        /// stairwell (a window there looks straight into the flights).</summary>
        private static List<Vector2> LongSpans(Shell sh, bool north)
        {
            var l = new List<Vector2>(sh.seg);
            for (int k = 0; k < sh.seg; k++)
            {
                if (north && k == sh.kS) continue;
                float a = sh.x0 + k * sh.segLen + WinClear;
                float b = sh.x0 + (k + 1) * sh.segLen - WinClear;
                if (b - a > 0.9f) l.Add(new Vector2(a, b));
            }
            return l;
        }

        /// <summary>Clear spans (building-local z) of an end facade: the two room bands, split so a
        /// 38 m tower does not end up with two enormous single windows.</summary>
        private static List<Vector2> EndSpans(Shell sh)
        {
            var l = new List<Vector2>(4);
            float a = sh.corr + PT * 0.5f + WinClear;    // clear of the corridor walls
            float b = sh.zi - WinClear;
            if (b - a < 1.2f) return l;
            Columns(l, a, b);
            Columns(l, -b, -a);
            return l;
        }

        private static void Columns(List<Vector2> l, float a, float b)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt((b - a) / 4.2f), 1, 4);
            float step = (b - a) / n;
            for (int i = 0; i < n; i++) l.Add(new Vector2(a + i * step, a + (i + 1) * step));
        }

        /// <summary>Room-grid spans в†’ wall coordinates of one facade (see `Wall`'s `u`).</summary>
        private static List<Vector2> Spans(List<Vector2> src, float shift, bool mirror)
        {
            var l = new List<Vector2>(src.Count);
            for (int i = 0; i < src.Count; i++)
                l.Add(mirror ? new Vector2(shift - src[i].y, shift - src[i].x)
                             : new Vector2(src[i].x + shift, src[i].y + shift));
            return l;
        }

        private static void BuildBuilding(Bldg b)
        {
            var go = new GameObject("bldg");
            go.transform.SetParent(root, false);
            go.transform.localPosition = b.p;
            go.transform.localRotation = Quaternion.Euler(0f, b.yaw, 0f);
            b.go = go;

            var g = new Geo();
            float hw = b.w * 0.5f, hd = b.d * 0.5f;
            // v0.9.19: a block **on legs** вЂ” the first three storeys are replaced by pylons (the user's
            // reference photo 2), so the shell is built from the third floor up and the volume below is
            // left open. `base0`/`nf0` keep the block's own numbers for the doors, the legs and the
            // restore at the end of this method.
            float base0 = b.baseY;
            int nf0 = b.floors;
            int wantLegs = b.legs ? 3 : 0;
            float legsLift = wantLegs * b.fh;
            if (b.legs) { b.baseY += legsLift; b.floors -= wantLegs; PlanCore(b); }
            float y0 = b.baseY;
            float yTop = b.Top;
            int floors = b.floors;
            float fh = b.fh;

            // the interior is planned first: the windows are then cut on the real room grid
            var sh = ShellOf(b, hw, hd);
            var colsZP = Spans(LongSpans(sh, true), hw, false);    // +z: the stair-hall facade
            var colsZM = Spans(LongSpans(sh, false), hw, true);    // -z
            var colsXP = Spans(EndSpans(sh), hd, false);           // +x
            var colsXM = Spans(EndSpans(sh), hd, true);            // -x

            // plinth: a wider skirt that also hides the seam with the deck (never under a block on legs вЂ”
            // there the deck is left open between the pylons)
            if (!b.legs && b.plinth && y0 > 0.05f)
                g.Box(0, new Vector3(0f, y0 * 0.5f, 0f), new Vector3(b.w + 1.2f, y0, b.d + 1.2f), Quaternion.identity, 0.5f);

            // openings per facade: ground entrances on both short ends, bridge doorways above
            var doorsZP = new List<Vector4>();
            var doorsZM = new List<Vector4>();
            var doorsXP = new List<Vector4>();
            var doorsXM = new List<Vector4>();
            if (!b.legs)     // on legs the street entrance is the core's, under the block
            {
                float ew = b.entranceW;
                float eh = b.entranceH > 0.01f ? b.entranceH : 2.5f * b.dh;
                if (b.doorPlusX) AddDoor(doorsXP, b.d * 0.5f, y0, ew, eh);      // main entrance, +x
                if (b.doorMinusX) AddDoor(doorsXM, b.d * 0.5f, y0, ew, eh);     // main entrance, -x
            }
            // v0.9.12: a balcony door behind every balcony вЂ” the opening runs from the floor up to
            // 2.2 m, so the balcony is a real balcony and not a slab glued in front of a window. It sits
            // on the very column the window would use, so `Wall` skips that window (see `Overlaps`).
            if (b.balconies)
                for (int f = 1; f < floors; f++)
                    for (int c = 1; c < colsZP.Count; c += 2)
                    {
                        float bw = Mathf.Min(b.winW, colsZP[c].y - colsZP[c].x - 0.5f);
                        if (bw < 0.9f) continue;
                        AddDoor(doorsZP, (colsZP[c].x + colsZP[c].y) * 0.5f, y0 + f * fh, bw, 2.2f);
                    }
            if (b.endPlus != null)
                for (int f = wantLegs; f < nf0; f++)
                    if (b.endPlus[f]) AddDoor(doorsXP, b.d * 0.5f, base0 + f * fh, 2.2f, 2.75f);
            if (b.endMinus != null)
                for (int f = wantLegs; f < nf0; f++)
                    if (b.endMinus[f]) AddDoor(doorsXM, b.d * 0.5f, base0 + f * fh, 2.2f, 2.75f);

            Vector3? wp;
            wp = Wall(g, new Vector3(0f, 0f, hd), Vector3.right, Vector3.forward, b.w, y0, yTop, floors, fh,
                      b.winW, b.winH, b.sill, b.splay, b.bay, doorsZP, colsZP, b.winSkip, b.winPad, b.winPlusZ, b.winFromFloor, b.winMaskPlusZ);
            if (wp.HasValue) { b.winPos.Add(wp.Value); b.winDir.Add(Vector3.forward); }
            wp = Wall(g, new Vector3(0f, 0f, -hd), Vector3.left, Vector3.back, b.w, y0, yTop, floors, fh,
                      b.winW, b.winH, b.sill, b.splay, b.bay, doorsZM, colsZM, b.winSkip, b.winPad, b.winMinusZ, b.winFromFloor, b.winMaskMinusZ);
            if (wp.HasValue) { b.winPos.Add(wp.Value); b.winDir.Add(Vector3.back); }
            wp = Wall(g, new Vector3(hw, 0f, 0f), Vector3.forward, Vector3.right, b.d, y0, yTop, floors, fh,
                      b.winW, b.winH, b.sill, b.splay, b.bay, doorsXP, colsXP, b.winSkip, b.winPad, b.winPlusX, b.winFromFloor, b.winMaskPlusX);
            if (wp.HasValue) { b.winPos.Add(wp.Value); b.winDir.Add(Vector3.right); }
            wp = Wall(g, new Vector3(-hw, 0f, 0f), Vector3.back, Vector3.left, b.d, y0, yTop, floors, fh,
                      b.winW, b.winH, b.sill, b.splay, b.bay, doorsXM, colsXM, b.winSkip, b.winPad, b.winMinusX, b.winFromFloor, b.winMaskMinusX);
            if (wp.HasValue) { b.winPos.Add(wp.Value); b.winDir.Add(Vector3.left); }

            // entrance steps in front of both ends (a block on legs has no street-level facade)
            if (!b.legs)
            {
                // v0.9.69: the steps rise from the block's **own ground** (`y0 - BASE`) by one
                // threshold (BASE), not from y=0 by the whole `baseY`. Passing `y0` built the House
                // towers a 61 m stepped fin that covered their 1,7 m doorway вЂ” the user's "the
                // passage is still closed". On terraces the step now sits on the terrace, not at the
                // deck 8-14 m below.
                Vector3 sb = Vector3.up * (y0 - BASE);
                Steps(g, new Vector3(hw, 0f, 0f) + sb, Vector3.forward, Vector3.right, 2.6f, BASE, 3);
                Steps(g, new Vector3(-hw, 0f, 0f) + sb, Vector3.back, Vector3.left, 2.6f, BASE, 3);
            }

            if (b.interior) Interior(g, b, sh);

            // cornice / bands / balconies on the outside
            if (b.risalit)
            {
                g.Box(0, new Vector3(0f, yTop - 0.35f, 0f), new Vector3(b.w + 1.3f, 0.7f, b.d + 1.3f), Quaternion.identity, 0.4f);
                Pilasters(g, b, sh, hd, y0, yTop);
            }
            if (b.bands)
                for (int f = 1; f < floors; f++)
                {
                    // v0.9.36: the band is a **facade ring**, not a solid box. As a box it also capped
                    // the whole interior at every floor вЂ” the stairwell hole included вЂ” which is why
                    // the stairs of the point towers (and of the two blocks on legs, and the clock
                    // tower) ended 0,24 m under the floor: the user's "Р»РµСЃС‚РЅРёС†С‹ СѓРїРёСЂР°СЋС‚СЃСЏ РІ РїРµСЂРµРєСЂС‹С‚РёСЏ".
                    float y = y0 + f * fh;
                    float bw = b.w + 0.35f, bd = b.d + 0.35f;
                    const float bt = 0.35f;                 // band thickness, half in the wall
                    g.Box(0, new Vector3(0f, y - 0.12f, hd), new Vector3(bw, 0.24f, bt), Quaternion.identity, 0.5f);
                    g.Box(0, new Vector3(0f, y - 0.12f, -hd), new Vector3(bw, 0.24f, bt), Quaternion.identity, 0.5f);
                    g.Box(0, new Vector3(hw, y - 0.12f, 0f), new Vector3(bt, 0.24f, bd), Quaternion.identity, 0.5f);
                    g.Box(0, new Vector3(-hw, y - 0.12f, 0f), new Vector3(bt, 0.24f, bd), Quaternion.identity, 0.5f);
                }
            if (b.balconies) Balconies(g, b, hd, y0, colsZP);

            Roof(g, b, hw, hd, yTop);

            // v0.9.18: the penthouse of the block on the peninsula вЂ” the attic drum, the cornice, the
            // hipped metal roof (submesh 1) and the clock dials (its own small object)
            if (b.penthouse) Penthouse(g, b, hw, hd, yTop);

            // v0.9.19: the pylons, the transfer girder and the street-level core, then the block's own
            // numbers are put back вЂ” everything after this point (probes, bridges, the roof) sees the
            // building as it was declared
            if (b.legs)
            {
                Legs(g, b, hw, hd, base0, legsLift, nf0);
                b.baseY = base0;
                b.floors = nf0;
            }

            if (b.towerDetail) TowerDetail(g, b, sh, hw, hd, y0, yTop, floors, fh);
            g.Apply(go, b.wall, mMetal);
            AddMeshCollider(go, "bldg");
            builtCount++;
        }

        /// <summary>
        /// v0.9.19 вЂ” **the legs** (the user's reference photo 2): the first three storeys of a block are
        /// replaced by the pylons the house stands on. Read off the photo:
        ///  1. a **thick transfer girder** under the first floor, overhanging the footprint by 0,55 m on
        ///     every side вЂ” the block's own mass starts above it, not at the deck;
        ///  2. a ring of **splayed pylons**, each a tapered prism that starts narrow on the deck
        ///     (2,2 x 1,7 m) a metre inside the facade and widens to 3,2 x 2,0 m where it meets the
        ///     girder, so the legs lean out and the underside reads as a row of V-shaped haunches;
        ///  3. the **street-level core** between them: a concrete shaft with the block's own door on
        ///     `В±x` and a switchback stair up to the first floor, so the house is still entered from the
        ///     street вЂ” the reference's dark base under the block.
        /// Colliders are the block's own mesh collider: the legs are part of that mesh.
        /// </summary>
        private static void Legs(Geo g, Bldg b, float hw, float hd, float y0, float lift, int floors)
        {
            const float gh = 3.0f;                        // the transfer girder
            float yTop = y0 + lift;                       // the first floor (the shell's base)
            float yg = yTop - gh;
            g.Box(0, new Vector3(0f, yg + gh * 0.5f, 0f),
                  new Vector3(b.w + 1.7f, gh, b.d + 1.7f), Quaternion.identity, 0.4f);

            // the pylons: a perimeter ring, inset 2,2 m from the edges
            int nx = Mathf.Max(1, Mathf.RoundToInt((b.w - 4.4f) / 7f));
            int nz = Mathf.Max(1, Mathf.RoundToInt((b.d - 4.4f) / 7f));
            int made = 0;
            for (int i = 0; i <= nx; i++)
            {
                float x = -hw + 2.2f + (b.w - 4.4f) * i / nx;
                Pylon(g, new Vector2(x, hd - 2.2f), new Vector2(0f, 1f), y0, yg);
                Pylon(g, new Vector2(x, -hd + 2.2f), new Vector2(0f, -1f), y0, yg);
                made += 2;
            }
            for (int i = 1; i < nz; i++)
            {
                float z = -hd + 2.2f + (b.d - 4.4f) * i / nz;
                if (b.legsShut <= 0) { Pylon(g, new Vector2(hw - 2.2f, z), new Vector2(1f, 0f), y0, yg); made++; }
                if (b.legsShut >= 0) { Pylon(g, new Vector2(-hw + 2.2f, z), new Vector2(-1f, 0f), y0, yg); made++; }
            }

            // the street-level core
            float cx = Mathf.Min(3.4f, b.w * 0.18f), cz = Mathf.Min(4.9f, b.d * 0.30f);
            const float cwt = 0.40f;
            float xin = cx - cwt - 0.05f, zin = cz - cwt - 0.05f;
            foreach (int s in new[] { -1, 1 })
            {
                var n = new Vector3(s, 0f, 0f);
                var o = new Vector3(s * (cx - cwt), 0f, -cz);
                Piece(g, o, Vector3.forward, n, 0f, cz - 1.3f, y0, yTop);
                Piece(g, o, Vector3.forward, n, cz + 1.3f, 2f * cz, y0, yTop);
                Piece(g, o, Vector3.forward, n, cz - 1.3f, cz + 1.3f, y0 + 2.95f, yTop);
            }
            foreach (int s in new[] { -1, 1 })
            {
                var n = new Vector3(0f, 0f, s);
                var o = new Vector3(-cx, 0f, s * (cz - cwt));
                Piece(g, o, Vector3.right, n, 0f, 2f * cx, y0, yTop);
            }
            // three flights in the shaft, each 5,6 m, and the two landing slabs between them.
            // v0.9.35: the stair runs **southwards from the exit landing** (`zSign = -1`), so its top
            // flight arrives at the north end of the core вЂ” directly under the stair hall of the
            // block, whose first-floor slab now has the matching hole (`Interior`, `PlanCore`).
            for (int k = 0; k < 3; k++)
                AtticStair(g, y0 + k * b.fh, b.fh, b.coreX0, b.coreX1, -b.coreZ0, b.coreX1, -1f);
            for (int k = 1; k < 3; k++)
            {
                float yk = y0 + k * b.fh;
                Slab(g, -xin, xin, -zin, zin, yk - SLAB, yk,
                     new Vector4(b.coreX0, b.coreHoleZ0, b.coreX1, b.coreHoleZ1));
            }
            // the opening in the first floor is a real stairwell now, so it gets the same railings
            // as the houses' own wells (1,0 m, part 1 = the district's metal)
            float ry = yTop;
            float rz = b.coreZ0 - 1.5f;                    // the exit landing's south edge
            RailRun(g, new Vector3(b.coreX0 - 0.06f, ry, b.coreHoleZ0),
                       new Vector3(b.coreX0 - 0.06f, ry, rz), 0f, 1.0f, 0, 0f, false, ry);
            RailRun(g, new Vector3(b.coreX1 + 0.06f, ry, b.coreHoleZ0),
                       new Vector3(b.coreX1 + 0.06f, ry, rz), 0f, 1.0f, 0, 0f, false, ry);
            RailRun(g, new Vector3(b.coreX0 - 0.06f, ry, b.coreHoleZ0),
                       new Vector3(b.coreX1 + 0.06f, ry, b.coreHoleZ0), 0f, 1.0f, 0, 0f, false, ry);

            // the deck under the block is open to the sky (no sun in this level), so it needs its own light
            Lamp(b.go.transform, new Vector3(cx + 2.8f, y0 + 4.6f, 0f), 1.4f, 21f);
            Lamp(b.go.transform, new Vector3(-cx - 2.8f, y0 + 4.6f, 0f), 1.4f, 21f);
            Lamp(b.go.transform, new Vector3(0f, y0 + b.fh + 3.0f, 0f), 0.9f, 14f);

            log.LogInfo($"[LEGS] {b.w:0.0}x{b.d:0.0} m block at {b.p.x:0.0},{b.p.z:0.0}: {made} pylons " +
                        $"(1,8 x 1,5 m on the deck 2,1 m inside the facade -> 3,6 x 2,4 m at the girder), " +
                        $"girder {gh:0.0} m at y={yg:0.0}, core {2f * cx:0.0}x{2f * cz:0.0} m with the " +
                        $"street door and 3 flights, first floor y={yTop:0.0}, floors {floors} -> {floors - 3}");
        }

        /// <summary>One pylon: a tapered prism that splays outward as it rises (see `Legs`).</summary>
        private static void Pylon(Geo g, Vector2 at, Vector2 outw, float yb, float yt)
        {
            var along = new Vector2(-outw.y, outw.x);
            Frustum(g, yb, yt, at - outw * 2.1f, at + outw * 0.05f,
                    1.8f, 1.5f, 3.6f, 2.4f, along, outw);
        }

        /// <summary>
        /// A tapered prism (v0.9.19): the bottom rectangle (`wb` x `db`) at `yb` on `cb`, the top one
        /// (`wt` x `dt`) at `yt` on `ct`, with `along`/`across` its local axes. `Quad` flips the winding
        /// onto the normal, so the caller only has to hand in outward-facing normals.
        /// </summary>
        private static void Frustum(Geo g, float yb, float yt, Vector2 cb, Vector2 ct,
                                   float wb, float db, float wt, float dt, Vector2 along, Vector2 across)
        {
            Vector3 B(float u, float v) => new Vector3(
                cb.x + (along.x * u * wb + across.x * v * db) * 0.5f, yb,
                cb.y + (along.y * u * wb + across.y * v * db) * 0.5f);
            Vector3 T(float u, float v) => new Vector3(
                ct.x + (along.x * u * wt + across.x * v * dt) * 0.5f, yt,
                ct.y + (along.y * u * wt + across.y * v * dt) * 0.5f);
            var nA = new Vector3(across.x, 0f, across.y);
            var nC = new Vector3(along.x, 0f, along.y);
            g.Quad(0, B(-1f, 1f), B(1f, 1f), T(1f, 1f), T(-1f, 1f), nA, 0.4f);
            g.Quad(0, B(1f, -1f), B(-1f, -1f), T(-1f, -1f), T(1f, -1f), -nA, 0.4f);
            g.Quad(0, B(1f, 1f), B(1f, -1f), T(1f, -1f), T(1f, 1f), nC, 0.4f);
            g.Quad(0, B(-1f, -1f), B(-1f, 1f), T(-1f, 1f), T(-1f, -1f), -nC, 0.4f);
            g.Quad(0, B(-1f, -1f), B(1f, -1f), B(1f, 1f), B(-1f, 1f), Vector3.down, 0.4f);
        }

        private static void Interior(Geo g, Bldg b, Shell sh)
        {
            float fh = sh.fh;
            float x0 = sh.x0, x1 = sh.x1;        // interior faces of the short walls
            float zi = sh.zi;                    // interior face of the long walls
            float corr = sh.corr;
            int seg = sh.seg;
            float segLen = sh.segLen;
            int kS = sh.kS;                      // the stair hall is the middle-north room
            float xs = sh.xs;
            float land = sh.stLand;              // landing depth at both ends of the flights (PlanStair)
            // v0.9.11: the stalinka's openings are +30 % taller (its storey is 5.6 m)
            float doorH = 2.15f * sh.dh;
            float stairDoorH = 2.2f * sh.dh;
            float crossDoorH = 2.1f * sh.dh;
            b.stairX0 = xs;
            b.stairX1 = xs + segLen;
            b.stairZ0 = corr;
            b.stairZ1 = zi;
            b.stairLand = sh.stLand;
            b.stairRun = sh.stRun;
            b.stairWell = sh.stWell;
            b.stairFw = sh.stFw;

            for (int f = 0; f < b.floors; f++)
            {
                float yf = sh.y0 + f * fh;
                // floor slab with the stair shaft cut out; the arrival landing stays solid, so the
                // top of the upper flight always ends on real floor (see PlanStair, v0.9.16)
                if (f > 0)
                    Slab(g, x0, x1, -zi, zi, yf - SLAB, yf,
                         new Vector4(xs, corr + land, xs + segLen, zi));
                else if (b.legs)   // v0.9.35: the street core's stair tops out through this slab
                    Slab(g, x0, x1, -zi, zi, yf - SLAB, yf,
                         new Vector4(b.coreX0, b.coreHoleZ0, b.coreX1, b.coreHoleZ1));
                else
                    Slab(g, x0, x1, -zi, zi, yf - SLAB, yf, null);

                // corridor walls: a door into every room, a wide doorway into the stair hall
                for (int s = -1; s <= 1; s += 2)
                {
                    float z = s * corr;
                    Vector3 o = new Vector3(x0, 0f, z);
                    Vector3 u = Vector3.right;
                    Vector3 n = Vector3.Cross(Vector3.up, u);
                    for (int k = 0; k < seg; k++)
                    {
                        float xa = k * segLen;
                        if (s > 0 && k == kS)
                        {
                            // v0.9.35: a block on legs also needs a doorway here, at the west end of
                            // the stair hall, for its street core's stair вЂ” the exit landing sits
                            // under this wall, so a wall across it would seal the stair off
                            if (b.legs && f == 0)
                            {
                                float ca = b.coreX0 - x0, cb = b.coreX1 - x0;
                                Piece(g, o, u, n, xa, ca, yf, yf + fh);
                                Piece(g, o, u, n, ca, cb, yf + stairDoorH, yf + fh);
                                Piece(g, o, u, n, cb, xa + segLen - 1.85f, yf, yf + fh);
                            }
                            else
                                Piece(g, o, u, n, xa, xa + segLen - 1.85f, yf, yf + fh);
                            Piece(g, o, u, n, xa + segLen - 0.05f, xa + segLen, yf, yf + fh);
                            Piece(g, o, u, n, xa + segLen - 1.85f, xa + segLen - 0.05f, yf + stairDoorH, yf + fh);
                        }
                        else
                        {
                            float c = xa + segLen * 0.5f;
                            Piece(g, o, u, n, xa, c - 0.65f, yf, yf + fh);
                            Piece(g, o, u, n, c + 0.65f, xa + segLen, yf, yf + fh);
                            Piece(g, o, u, n, c - 0.65f, c + 0.65f, yf + doorH, yf + fh);
                        }
                    }
                }

                // cross walls: split the rooms along the row (with a door, so the rooms connect);
                // the two beside the stair hall are solid вЂ” they are its side walls.
                // v0.9.11: no partitions flush with the end walls (k = 0 and k = seg): they filled the
                // whole first and last room with a blank wall right behind the end windows
                for (int s = -1; s <= 1; s += 2)
                {
                    float za = s * corr, zb = s * zi;
                    float len = Mathf.Abs(zb - za);
                    Vector3 u = Vector3.forward;
                    Vector3 n = Vector3.Cross(Vector3.up, u);
                    for (int k = 1; k < seg; k++)
                    {
                        Vector3 o = new Vector3(x0 + k * segLen, 0f, Mathf.Min(za, zb));
                        bool solid = s > 0 && (k == kS || k == kS + 1);
                        if (solid) { Piece(g, o, u, n, 0f, len, yf, yf + fh); continue; }
                        Piece(g, o, u, n, 0f, len * 0.5f - 0.65f, yf, yf + fh);
                        Piece(g, o, u, n, len * 0.5f + 0.65f, len, yf, yf + fh);
                        Piece(g, o, u, n, len * 0.5f - 0.65f, len * 0.5f + 0.65f, yf + crossDoorH, yf + fh);
                    }
                }
            }

            // stairs: one switchback per storey, inside the stair hall (same plan as the slab hole)
            for (int f = 0; f + 1 < b.floors; f++)
                StairsZ(g, sh, xs, sh.y0 + f * fh);
        }

        private static float FH01(int x, int y, int s)
        {
            int h = x * 374761393 + y * 668265263 + s * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0x7fffffff) / 2147483647f;
        }

        /// <summary>v0.9.73 вЂ” plan points 5/8: a dark abstract bas-relief on the outward long wall of
        /// the middle east-flank block. The wall (local +z) is built solid, and the ornament is a field
        /// of raised charcoal bars: frames, a ringed medallion with a sunburst, zigzag ribbons, glyph
        /// friezes and bosses. Stage 1 = the composition; the second pass adds the labyrinth and the
        /// interlacing arcs.</summary>
        private static void BuildRelief(Bldg b)
        {
            if (b == null || b.go == null) return;
            float W = b.w - 4f;
            float yBot = b.baseY + Mathf.Max(10f, b.fh * 2f);
            float yTop = b.Top - 8f;
            float H = yTop - yBot;
            if (W < 10f || H < 30f)
            {
                log.LogInfo($"[RELIEF] skipped: field {W:0.0} x {H:0.0} m is too small on this block");
                return;
            }
            var g = new Geo();
            float z = b.d * 0.5f + 0.01f;
            float cy = (yBot + yTop) * 0.5f;
            float hw = W * 0.5f, hh = H * 0.5f;
            Vector2 V(float x, float y) => new Vector2(x, cy + y);

            // outer and inner frame
            RBar(g, z, V(-hw, -hh), V(hw, -hh), 0.70f, 0.55f);
            RBar(g, z, V(-hw, hh), V(hw, hh), 0.70f, 0.55f);
            RBar(g, z, V(-hw, -hh), V(-hw, hh), 0.70f, 0.55f);
            RBar(g, z, V(hw, -hh), V(hw, hh), 0.70f, 0.55f);
            RBar(g, z, V(-hw + 2.2f, -hh + 2.2f), V(hw - 2.2f, -hh + 2.2f), 0.30f, 0.34f);
            RBar(g, z, V(-hw + 2.2f, hh - 2.2f), V(hw - 2.2f, hh - 2.2f), 0.30f, 0.34f);
            RBar(g, z, V(-hw + 2.2f, -hh + 2.2f), V(-hw + 2.2f, hh - 2.2f), 0.30f, 0.34f);
            RBar(g, z, V(hw - 2.2f, -hh + 2.2f), V(hw - 2.2f, hh - 2.2f), 0.30f, 0.34f);

            // the medallion: a heavy ring, a 24-ray sunburst, a diamond and a central boss
            RRing(g, z, V(0f, 0f), 6.2f, 7.6f, 0.60f, 32);
            RRing(g, z, V(0f, 0f), 4.1f, 4.5f, 0.40f, 24);
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI * 2f / 24f;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                RBar(g, z, V(0f, 0f) + d * 8.2f, V(0f, 0f) + d * 11.4f, 0.42f, 0.42f);
            }
            RBar(g, z, V(-3.4f, 0f), V(0f, 3.4f), 0.55f, 0.50f);
            RBar(g, z, V(0f, 3.4f), V(3.4f, 0f), 0.55f, 0.50f);
            RBar(g, z, V(3.4f, 0f), V(0f, -3.4f), 0.55f, 0.50f);
            RBar(g, z, V(0f, -3.4f), V(-3.4f, 0f), 0.55f, 0.50f);
            RBar(g, z, V(0f, -4.7f), V(0f, 4.7f), 0.80f, 0.45f);
            RDisc(g, z, V(0f, 0f), 1.9f, 0.48f, 24);

            // the zigzag ribbons on the outside of the sunburst
            for (int sd = -1; sd <= 1; sd += 2)
            {
                float bx = sd * (hw - 3.2f);
                float y = -hh + 4f;
                bool right = true;
                while (y < hh - 4.1f)
                {
                    float y2 = Mathf.Min(y + 2.6f, hh - 4f);
                    float x1 = bx + (right ? 1.25f : -1.25f);
                    float x2 = bx + (right ? -1.25f : 1.25f);
                    RBar(g, z, V(x1, y), V(x2, y2), 0.46f, 0.34f);
                    y = y2;
                    right = !right;
                }
            }

            // glyph friezes top and bottom
            for (int row = -1; row <= 1; row += 2)
            {
                float gy = row * (hh - 4.0f);
                for (int i = 0; i < 11; i++)
                {
                    float gx = -hw + 3.6f + i * (W - 7.2f) / 10f;
                    RGlyph(g, z, V(gx, gy), 1.05f, i % 6, 0.32f);
                }
            }

            // four corner bosses and a few scattered studs
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    RDisc(g, z, V(sx * (hw - 4.4f), sy * (hh - 5.4f)), 1.15f, 0.36f, 16);
                    RDisc(g, z, V(sx * (hw - 7.2f), sy * (hh - 7.6f)), 0.52f, 0.30f, 12);
                }
            for (int i = -3; i <= 3; i++)
                RDisc(g, z, V(i * (hw / 4.2f), -hh + 1.0f), 0.28f, 0.24f, 10);

            // v2 (point 8): the abstract layers вЂ” three broken concentric rings around the medallion,
            // a second offset fan of short rays, two spirals in the tall empty thirds, a constellation
            // and a set of asymmetric shards. Deliberately not mirror-symmetric.
            for (int ring = 0; ring < 3; ring++)
            {
                float rr = 10.5f + ring * 3.0f;
                for (int a = 0; a < 3; a++)
                    RArc(g, z, V(0f, 0f), rr, a * 120f + 8f, a * 120f + 112f, 10, 0.28f, 0.30f);
            }
            for (int i = 0; i < 24; i++)
            {
                float a = (i * 15f + 7.5f) * Mathf.Deg2Rad;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                RBar(g, z, V(0f, 0f) + d * 13.0f, V(0f, 0f) + d * 14.4f, 0.18f, 0.22f);
            }
            RSpiral(g, z, V(-hw * 0.52f, hh * 0.56f), 1.1f, 5.8f, 2.2f, 44, 0.24f, 0.30f, 0f);
            RSpiral(g, z, V(hw * 0.52f, -hh * 0.56f), 1.1f, 5.8f, 2.2f, 44, 0.24f, 0.30f, Mathf.PI);
            // constellation: studs joined by thin rods (upper right, lower left)
            {
                var cd = new[] {
                    new Vector2(0.55f, 0.72f), new Vector2(0.62f, 0.65f), new Vector2(0.58f, 0.55f),
                    new Vector2(0.48f, 0.58f), new Vector2(0.45f, 0.68f), new Vector2(0.66f, 0.78f) };
                for (int i = 0; i < cd.Length; i++)
                    RDisc(g, z, V(cd[i].x * hw, cd[i].y * hh), 0.30f + (i % 3) * 0.08f, 0.26f, 10);
                for (int i = 0; i + 1 < cd.Length; i++)
                    RBar(g, z, V(cd[i].x * hw, cd[i].y * hh),
                         V(cd[i + 1].x * hw, cd[i + 1].y * hh), 0.10f, 0.18f);
                for (int i = 0; i < 5; i++)
                    RDisc(g, z, V(-(0.45f + i * 0.04f) * hw, -(0.80f - i * 0.03f) * hh),
                          0.22f + (i % 2) * 0.10f, 0.24f, 10);
                RBar(g, z, V(-0.45f * hw, -0.80f * hh), V(-0.57f * hw, -0.68f * hh), 0.08f, 0.16f);
                RBar(g, z, V(-0.57f * hw, -0.68f * hh), V(-0.50f * hw, -0.62f * hh), 0.08f, 0.16f);
            }
            // asymmetric shards, lengths and depths varied
            {
                var sh = new[] {
                    new Vector4(-0.72f, 0.34f, -0.58f, 0.42f), new Vector4(0.70f, 0.18f, 0.55f, 0.28f),
                    new Vector4(-0.66f, -0.36f, -0.48f, -0.44f), new Vector4(0.62f, -0.28f, 0.44f, -0.18f),
                    new Vector4(-0.40f, 0.86f, -0.30f, 0.78f), new Vector4(0.38f, -0.86f, 0.28f, -0.76f),
                    new Vector4(-0.28f, 0.14f, -0.18f, 0.22f), new Vector4(0.24f, -0.10f, 0.16f, -0.02f) };
                for (int i = 0; i < sh.Length; i++)
                    RBar(g, z, V(sh[i].x * hw, sh[i].y * hh), V(sh[i].z * hw, sh[i].w * hh),
                         0.24f + (i % 3) * 0.06f, 0.22f + (i % 2) * 0.10f);
            }

            var m = ReliefMaterial();
            var go = new GameObject("relief");
            go.transform.SetParent(b.go.transform, false);
            g.Apply(go, m);
            AddMeshCollider(go, "relief");
            log.LogInfo($"[RELIEF] dark ornament on the outward wall of the block at {b.p.x:0.0},{b.p.z:0.0} " +
                        $"({b.w:0.0}x{b.d:0.0} m, {b.floors} fl): field {W:0.0} x {H:0.0} m, " +
                        $"medallion r 7,6 m + 24 rays, 2 zigzag ribbons, 22 glyphs, 12 bosses; " +
                        $"{g.V.Count} verts, charcoal {N(m)}");
        }

        private static void RBar(Geo g, float z, Vector2 a, Vector2 b, float w, float d)
        {
            Vector2 mid = (a + b) * 0.5f;
            float len = (b - a).magnitude;
            if (len < 0.02f) return;
            float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            g.Box(0, new Vector3(mid.x, mid.y, z + d * 0.5f - 0.01f),
                  new Vector3(len, w, d), Quaternion.Euler(0f, 0f, ang), 0.55f);
        }

        private static void RArc(Geo g, float z, Vector2 c, float r, float a0, float a1, int seg,
                                 float w, float d)
        {
            Vector2 Prev(float t) => c + new Vector2(Mathf.Cos(t * Mathf.Deg2Rad),
                                                     Mathf.Sin(t * Mathf.Deg2Rad)) * r;
            Vector2 prev = Prev(a0);
            for (int i = 1; i <= seg; i++)
            {
                Vector2 q = Prev(Mathf.Lerp(a0, a1, i / (float)seg));
                RBar(g, z, prev, q, w, d);
                prev = q;
            }
        }

        private static void RSpiral(Geo g, float z, Vector2 c, float r0, float r1, float turns,
                                    int seg, float w, float d, float phase)
        {
            Vector2 P(float t) => c + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * Mathf.Lerp(r0, r1, t / (turns * Mathf.PI * 2f));
            Vector2 prev = P(phase);
            for (int i = 1; i <= seg; i++)
            {
                float t = phase + turns * Mathf.PI * 2f * i / seg;
                Vector2 q = P(t);
                RBar(g, z, prev, q, w, d);
                prev = q;
            }
        }

        private static void RRing(Geo g, float z, Vector2 c, float r0, float r1, float d, int seg)
        {
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                var p0 = c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r1;
                var p1 = c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r1;
                Vector2 mid = (p0 + p1) * 0.5f;
                float chord = (p1 - p0).magnitude;
                float ang = Mathf.Atan2(p1.y - p0.y, p1.x - p0.x) * Mathf.Rad2Deg;
                g.Box(0, new Vector3(mid.x, mid.y, z + d * 0.5f - 0.01f),
                      new Vector3(chord, r1 - r0, d), Quaternion.Euler(0f, 0f, ang), 0.55f);
            }
        }

        private static void RDisc(Geo g, float z, Vector2 c, float r, float d, int seg)
        {
            var uv = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                Vector3 p0 = new Vector3(c.x + Mathf.Cos(a0) * r, c.y + Mathf.Sin(a0) * r, z + d);
                Vector3 p1 = new Vector3(c.x + Mathf.Cos(a1) * r, c.y + Mathf.Sin(a1) * r, z + d);
                g.Tri(0, new Vector3(c.x, c.y, z + d), p0, p1, Vector3.forward, uv, uv, uv);
                var n = new Vector3(Mathf.Cos((a0 + a1) * 0.5f), Mathf.Sin((a0 + a1) * 0.5f), 0f);
                g.Quad(0, new Vector3(p0.x, p0.y, z), new Vector3(p1.x, p1.y, z), p1, p0, n, 0.5f);
            }
        }

        private static void RGlyph(Geo g, float z, Vector2 c, float s, int kind, float d)
        {
            switch (kind)
            {
                case 0:
                    RRing(g, z, c, 0.55f * s, 0.88f * s, d, 16);
                    break;
                case 1:
                    {
                        Vector2 a = c + new Vector2(0f, s), b = c + new Vector2(-0.9f * s, -0.8f * s),
                                e = c + new Vector2(0.9f * s, -0.8f * s);
                        RBar(g, z, a, b, 0.22f * s, d); RBar(g, z, b, e, 0.22f * s, d);
                        RBar(g, z, e, a, 0.22f * s, d);
                        break;
                    }
                case 2:
                    RBar(g, z, c + new Vector2(-s, 0f), c + new Vector2(s, 0f), 0.24f * s, d);
                    RBar(g, z, c + new Vector2(0f, -s), c + new Vector2(0f, s), 0.24f * s, d);
                    break;
                case 3:
                    RBar(g, z, c + new Vector2(-s, -s), c + new Vector2(s, -s), 0.22f * s, d);
                    RBar(g, z, c + new Vector2(s, -s), c + new Vector2(s, s), 0.22f * s, d);
                    RBar(g, z, c + new Vector2(s, s), c + new Vector2(-s, s), 0.22f * s, d);
                    RBar(g, z, c + new Vector2(-s, s), c + new Vector2(-s, -s), 0.22f * s, d);
                    break;
                case 4:
                    RBar(g, z, c + new Vector2(-s, 0.7f * s), c + new Vector2(0f, -0.7f * s), 0.24f * s, d);
                    RBar(g, z, c + new Vector2(0f, -0.7f * s), c + new Vector2(s, 0.7f * s), 0.24f * s, d);
                    break;
                default:
                    g.Box(0, new Vector3(c.x, c.y, z + d * 0.5f - 0.01f),
                          new Vector3(0.62f * s, 0.62f * s, d), Quaternion.identity, 0.5f);
                    break;
            }
        }

        private static Material mRelief;

        private static Material ReliefMaterial()
        {
            if (mRelief != null) return mRelief;
            const int S = 256;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, true)
            { name = "CoopReliefCharcoal", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float n = FH01(x, y, 17) * 0.6f + FH01(x / 8, y / 8, 23) * 0.4f;
                    float v = 0.085f + 0.050f * n;
                    float vein = Mathf.Abs(Mathf.Sin(x * 0.052f + Mathf.Sin(y * 0.031f) * 2.2f));
                    if (vein < 0.075f) v *= 0.55f;
                    if (FH01(x / 16, y / 16, 41) > 0.94f) v *= 1.55f;   // sparse pale chips
                    px[y * S + x] = new Color(v * 1.07f, v * 0.99f, v * 0.92f);
                }
            tex.SetPixels32(px);
            tex.Apply(true);
            var m = new Material(mRough) { name = "CoopRelief" };
            m.SetTexture("_BC", tex);
            m.SetTextureScale("_BC", Vector2.one);
            mRelief = m;
            log.LogInfo("[RELIEF] material: CoopRelief on " + m.shader.name + " (charcoal 256x256, mean ~0,11)");
            return m;
        }

        private static Material mBanner;

        /// <summary>v0.9.76 вЂ” a dark-red woven fabric for the House's two banners (no furniture atlas:
        /// the room furniture was removed at the user's request, the banners stay).</summary>
        private static Material BannerMaterial()
        {
            if (mBanner != null) return mBanner;
            const int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, true)
            { name = "CoopBannerRed", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float n = FH01(x, y, 31);
                    float fold = 0.88f + 0.18f * Mathf.Sin(x * 0.14f + Mathf.Sin(y * 0.03f) * 1.5f);
                    Color c = new Color(0.38f, 0.065f, 0.050f) * (0.86f + 0.22f * n) * fold;
                    px[y * S + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(true);
            var m = new Material(mRough) { name = "CoopBanner" };
            m.SetTexture("_BC", tex);
            m.SetTextureScale("_BC", Vector2.one);
            mBanner = m;
            log.LogInfo("[HOUSE] banner material: CoopBanner 128x128 (dark red woven fabric)");
            return m;
        }

        /// <summary>v0.9.72 вЂ” a flat five-pointed star, double-sided, for the mast on the core.</summary>
        private static void Star(Geo g, Vector3 c, float r, Vector3 n)
        {
            n = n.normalized;
            Vector3 ex = Mathf.Abs(n.z) > 0.5f ? Vector3.right : Vector3.forward;
            Vector3 ey = Vector3.up;
            var uv = new Vector2(0.5f, 0.30f);   // the deep-red part of the glow gradient
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 nn = n * side;
                for (int i = 0; i < 10; i++)
                {
                    float a0 = (90f + i * 36f) * Mathf.Deg2Rad;
                    float a1 = (90f + (i + 1) * 36f) * Mathf.Deg2Rad;
                    float r0 = (i % 2 == 0) ? r : r * 0.42f;
                    float r1 = (i % 2 == 0) ? r * 0.42f : r;
                    Vector3 p0 = c + (ex * Mathf.Cos(a0) + ey * Mathf.Sin(a0)) * r0;
                    Vector3 p1 = c + (ex * Mathf.Cos(a1) + ey * Mathf.Sin(a1)) * r1;
                    g.Tri(0, c, p0, p1, nn, uv, uv, uv);
                }
            }
        }

        private static void TowerDetail(Geo g, Bldg b, Shell sh, float hw, float hd,
                                        float y0, float yTop, int floors, float fh)
        {
            int f0 = Mathf.Max(1, b.winFromFloor);
            for (int f = f0; f < floors; f++)
            {
                float y = y0 + f * fh;
                // a string course at every floor line, 0,30 m proud, and a thin sill under the windows
                g.Box(0, new Vector3(0f, y - 0.11f, hd + 0.12f),
                      new Vector3(b.w, 0.22f, 0.36f), Quaternion.identity, 0.4f);
                g.Box(0, new Vector3(0f, y - 0.11f, -hd - 0.12f),
                      new Vector3(b.w, 0.22f, 0.36f), Quaternion.identity, 0.4f);
                g.Box(0, new Vector3(hw + 0.12f, y - 0.11f, 0f),
                      new Vector3(0.36f, 0.22f, b.d), Quaternion.identity, 0.4f);
                g.Box(0, new Vector3(-hw - 0.12f, y - 0.11f, 0f),
                      new Vector3(0.36f, 0.22f, b.d), Quaternion.identity, 0.4f);
                float ys = y + b.sill - 0.06f;
                g.Box(0, new Vector3(0f, ys, hd + 0.06f),
                      new Vector3(b.w, 0.12f, 0.20f), Quaternion.identity, 0.4f);
                g.Box(0, new Vector3(0f, ys, -hd - 0.06f),
                      new Vector3(b.w, 0.12f, 0.20f), Quaternion.identity, 0.4f);
            }
            // the cornice: the tower's cap, 0,4 m tall and 0,7 m proud
            g.Box(0, new Vector3(0f, yTop - 0.2f, hd + 0.2f),
                  new Vector3(b.w + 0.6f, 0.4f, 0.7f), Quaternion.identity, 0.4f);
            g.Box(0, new Vector3(0f, yTop - 0.2f, -hd - 0.2f),
                  new Vector3(b.w + 0.6f, 0.4f, 0.7f), Quaternion.identity, 0.4f);
            g.Box(0, new Vector3(hw + 0.2f, yTop - 0.2f, 0f),
                  new Vector3(0.7f, 0.4f, b.d + 0.4f), Quaternion.identity, 0.4f);
            g.Box(0, new Vector3(-hw - 0.2f, yTop - 0.2f, 0f),
                  new Vector3(0.7f, 0.4f, b.d + 0.4f), Quaternion.identity, 0.4f);

            // vertical ribs on the two long facades, on the room-cell boundaries вЂ” the deep piers
            // of the honeycomb; they start above the blank base band.
            float yr0 = y0 + f0 * fh, yr1 = yTop - 0.45f;
            if (yr1 - yr0 < 1f) return;
            for (int k = 1; k < sh.seg; k++)
            {
                float x = sh.x0 + k * sh.segLen;
                for (int s = -1; s <= 1; s += 2)
                    g.Box(0, new Vector3(x, (yr0 + yr1) * 0.5f, s * (hd + 0.10f)),
                          new Vector3(0.20f, yr1 - yr0, 0.30f), Quaternion.identity, 0.4f);
            }
        }

        /// <summary>
        /// v0.9.16 вЂ” the stair of **every** block (tall stalinkas and the House of Soviets' towers
        /// alike). Two flights run along z, side by side in x, with the landing from `PlanStair` at
        /// each end: up northwards, across the half-landing, back south to the corridor door. The
        /// flight is 2-3 m long and every number here comes from the plan the slab hole uses.
        /// </summary>
        private static void StairsZ(Geo g, Shell sh, float xs, float yf)
        {
            float fh = sh.fh;
            float fw = sh.stFw, well = sh.stWell;
            float xa0 = xs, xa1 = xs + fw;                    // the west flight
            float xb0 = xs + fw + well, xb1 = xs + sh.segLen;  // the east flight
            float rl = 1.00f;                                  // handrail height above the tread line

            FlightZ(g, xa0, xa1, sh.stZLow, sh.stZHigh, yf, sh.stN, sh.stRise, false,
                    SLAB, StairPlate);                                                    // up, northwards
            Landing(g, xs, xs + sh.segLen, sh.stZHigh, sh.zi, yf + fh * 0.5f);          // half-landing
            FlightZ(g, xb0, xb1, sh.stZLow, sh.stZHigh,                                // back, south
                    yf + fh * 0.5f, sh.stN, sh.stRise, true, StairPlate, SLAB);

            // v0.9.35: the railings of the open well вЂ” a handrail and three posts along each flight's
            // well-side edge, plus a guard across the well at the floor and at the half-landing (the
            // two places where you would otherwise walk straight into the shaft).
            Vector3 A(float x, float y, float z) => new Vector3(x, y, z);
            float half = yf + fh * 0.5f;
            RailRun(g, A(xa1 - 0.03f, yf + sh.stRise * 0.5f, sh.stZLow + 0.03f),
                       A(xa1 - 0.03f, half + sh.stRise * 0.5f, sh.stZHigh - 0.03f),
                       1f, rl, sh.stN, sh.stTread, true, yf);
            RailRun(g, A(xb0 + 0.03f, half + sh.stRise * 0.5f, sh.stZHigh - 0.03f),
                       A(xb0 + 0.03f, yf + fh + sh.stRise * 0.5f, sh.stZLow + 0.03f),
                       1f, rl, sh.stN, sh.stTread, true, half);
            RailRun(g, A(xa1, yf, sh.stZLow), A(xb0, yf, sh.stZLow), 1f, rl, 0, 0f, false, yf);
            RailRun(g, A(xa1, half, sh.stZHigh), A(xb0, half, sh.stZHigh), 1f, rl, 0, 0f, false, half);
        }

        /// <summary>
        /// v0.9.35 вЂ” one run of a thin metal railing: a handrail at `h` above the two base points,
        /// a mid rail and `posts` vertical posts. `stepped` snaps the posts onto the treads of a
        /// flight (the base points are then the first and last tread's nosing line, `tread` apart,
        /// `count` risers), so no post floats over a step. `yBase` is the floor the flight leaves.
        /// </summary>
        private static void RailRun(Geo g, Vector3 a, Vector3 b, float midFrac, float h,
                                    int count, float tread, bool stepped, float yBase)
        {
            const float t = 0.045f;
            Vector3 ha = a + Vector3.up * h, hb = b + Vector3.up * h;
            Bar(g, ha, hb, t);
            if (midFrac > 0.01f)
                Bar(g, a + Vector3.up * (h * 0.52f), b + Vector3.up * (h * 0.52f), t * 0.8f);
            int posts = count > 0 ? 3 : 2;
            for (int i = 0; i < posts; i++)
            {
                float u = i / (float)(posts - 1);
                Vector3 p = Vector3.Lerp(a, b, u);
                float top = Mathf.Lerp(ha.y, hb.y, u);
                if (stepped && count > 0)          // stand the post **on** the tread under it
                {
                    float along = (p.z - a.z) / (Mathf.Abs(b.z - a.z) > 0.01f ? b.z - a.z : 0.01f);
                    int k = Mathf.Clamp(Mathf.FloorToInt(along * count), 0, count - 1);
                    p.y = Mathf.Max(p.y, yBase + (k + 1) * (b.y - a.y) / count);
                }
                Post(g, p, Mathf.Max(0.2f, top - p.y), t);
            }
        }

        /// <summary>One metal bar between two points (a handrail or a sloped rail).</summary>
        private static void Bar(Geo g, Vector3 a, Vector3 b, float t)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.02f) return;
            g.Box(1, (a + b) * 0.5f, new Vector3(t, t, len + t),
                  Quaternion.LookRotation(d / len, Vector3.up), 0.6f);
        }

        /// <summary>One vertical post standing on `p` (its base), `h` tall.</summary>
        private static void Post(Geo g, Vector3 p, float h, float t)
            => g.Box(1, p + Vector3.up * (h * 0.5f), new Vector3(t, h, t), Quaternion.identity, 0.6f);

        /// <summary>
        /// v0.9.37 вЂ” a flight as a **concrete plate** (the user's reference photos), not the triangular
        /// prism of v0.9.36: the soffit runs from the floor at the bottom end up to the **underside of the
        /// landing** at the top, the side faces are only the thin band between that soffit and the treads,
        /// and the body is closed вЂ” so a flight reads as a slab with steps on it and you cannot see
        /// through it from below (the old side faces ran all the way down to the floor, which left the
        /// underside open: the "transparent" stairs the user photographed).
        /// `toSouth` = the flight rises towards -z (its lowest tread is at `z1`).
        /// </summary>
        private static void FlightZ(Geo g, float x0, float x1, float z0, float z1, float yBase,
                                    int count, float rise, bool toSouth,
                                    float backLow = 0f, float backHigh = 0f)
        {
            const float plate = StairPlate;                  // v0.9.38: 2,5x the floor slab = 0,65 m (the user: too thin)
            float tread = (z1 - z0) / count;
            float run = Mathf.Abs(z1 - z0);
            float zLow = toSouth ? z1 : z0, zHigh = toSouth ? z0 : z1;
            // v0.9.38: the soffit is **parallel to the flight** at a constant vertical thickness, like the
            // reference photos вЂ” the old one tapered from the floor to the landing, which read as a wedge
            float slope = (count * rise) / Mathf.Max(0.01f, run);
            float Sof(float z) => yBase + slope * Mathf.Abs(z - zLow) - plate;
            // v0.9.39: the plate's sides are built as **closed per-tread cells** (bottom = the soffit
            // strip, top = the tread, front = the riser, back = the face below the next riser, both
            // sides) instead of an open shell. Every cell is a closed solid, so no view вЂ” not even
            // through a seam вЂ” can reach the empty inside: that inside was the "РїСЂРѕР·СЂР°С‡РЅС‹Рµ С‚РѕСЂС†С‹".
            for (int i = 0; i < count; i++)
            {
                float a = toSouth ? z1 - (i + 1) * tread : z0 + i * tread;
                float top = yBase + (i + 1) * rise;
                float rz = toSouth ? a + tread : a;      // the riser on the edge walked up to
                float sa = Sof(a), sb = Sof(a + tread);
                g.Quad(0, new Vector3(x0, top, a), new Vector3(x1, top, a),
                          new Vector3(x1, top, a + tread), new Vector3(x0, top, a + tread), Vector3.up, 0.6f);
                g.Quad(0, new Vector3(x0, top - rise, rz), new Vector3(x1, top - rise, rz),
                          new Vector3(x1, top, rz), new Vector3(x0, top, rz),
                          toSouth ? Vector3.forward : Vector3.back, 0.6f);
                // the soffit strip of this cell
                g.Quad(0, new Vector3(x0, sa, a), new Vector3(x1, sa, a),
                          new Vector3(x1, sb, a + tread), new Vector3(x0, sb, a + tread), Vector3.down, 0.6f);
                // the cell's back face (the plane below the *next* riser) вЂ” closes the volume
                if (i + 1 < count)
                {
                    float rb = toSouth ? a : a + tread;
                    float yb2 = Sof(rb);
                    g.Quad(0, new Vector3(x0, yb2, rb), new Vector3(x1, yb2, rb),
                              new Vector3(x1, top, rb), new Vector3(x0, top, rb),
                              toSouth ? Vector3.back : Vector3.forward, 0.6f);
                }
                // both sides of this cell
                g.Quad(0, new Vector3(x0, sa, a), new Vector3(x0, sb, a + tread),
                          new Vector3(x0, top, a + tread), new Vector3(x0, top, a), Vector3.left, 0.6f);
                g.Quad(0, new Vector3(x1, sa, a), new Vector3(x1, sb, a + tread),
                          new Vector3(x1, top, a + tread), new Vector3(x1, top, a), Vector3.right, 0.6f);
            }
            // The two ends, closed **only where the neighbouring solid does not already close them**
            // (`backLow` / `backHigh` = that solid's thickness, 0 = nothing behind). Never coincident
            // with the floor slab's shaft rim or the landing's face вЂ” coincident faces from the same
            // concrete z-fight, which reads as a dithered "transparent" patch.
            float lowTop = yBase - backLow;
            if (lowTop > Sof(zLow) + 0.005f)
                g.Quad(0, new Vector3(x0, Sof(zLow), zLow), new Vector3(x1, Sof(zLow), zLow),
                          new Vector3(x1, lowTop, zLow), new Vector3(x0, lowTop, zLow),
                          toSouth ? Vector3.forward : Vector3.back, 0.6f);
            float highY = yBase + count * rise, highTop = highY - backHigh;
            if (highTop > Sof(zHigh) + 0.005f)
                g.Quad(0, new Vector3(x0, Sof(zHigh), zHigh), new Vector3(x1, Sof(zHigh), zHigh),
                          new Vector3(x1, highTop, zHigh), new Vector3(x0, highTop, zHigh),
                          toSouth ? Vector3.back : Vector3.forward, 0.6f);
        }

        /// <summary>
        /// v0.9.37 вЂ” a landing as a closed slab (a box): the old `Slab` drew only its top and bottom, so
        /// its exposed edge over the well was an open hole you could see into ("transparent plates").
        /// </summary>
        private static void Landing(Geo g, float x0, float x1, float z0, float z1, float yTop)
        {
            if (x1 - x0 < 0.02f || z1 - z0 < 0.02f) return;
            g.Box(0, new Vector3((x0 + x1) * 0.5f, yTop - StairPlate * 0.5f, (z0 + z1) * 0.5f),
                  new Vector3(x1 - x0, StairPlate, z1 - z0), Quaternion.identity, 0.5f);
        }

        /// <summary>
        private static void Roof(Geo g, Bldg b, float hw, float hd, float yTop)
        {
            // roof plate with a small overhang and a parapet
            g.Box(0, new Vector3(0f, yTop + 0.13f, 0f), new Vector3(b.w + 0.6f, 0.26f, b.d + 0.6f), Quaternion.identity, 0.4f);
            float y = yTop + 0.26f;
            g.Box(0, new Vector3(0f, y + 0.35f, hd - 0.15f), new Vector3(b.w, 0.7f, 0.3f), Quaternion.identity, 0.5f);
            g.Box(0, new Vector3(0f, y + 0.35f, -hd + 0.15f), new Vector3(b.w, 0.7f, 0.3f), Quaternion.identity, 0.5f);
            g.Box(0, new Vector3(hw - 0.15f, y + 0.35f, 0f), new Vector3(0.3f, 0.7f, b.d - 0.6f), Quaternion.identity, 0.5f);
            g.Box(0, new Vector3(-hw + 0.15f, y + 0.35f, 0f), new Vector3(0.3f, 0.7f, b.d - 0.6f), Quaternion.identity, 0.5f);
            if (rng.NextDouble() < 0.6 && !b.memorial && !b.penthouse && !b.heli)   // the draw is kept, so the seed layout does not shift
                g.Box(0, new Vector3(J(b.w * 0.25f), y + 1.0f, J(b.d * 0.2f)),
                      new Vector3(3.2f, 2.0f, 2.6f), Quaternion.identity, 0.5f);
        }

        // -- wall with openings -------------------------------------------------

        /// <summary>
        /// One facade: exterior and interior faces with real window openings and splayed reveals.
        /// Windows stay empty (no panes anywhere in the district). Holes are
        /// (xMin, yMin, xMax, yMax) in wall space, y measured in building space.
        ///
        /// v0.9.11: `cols` carries the **room grid** of this facade (see `Shell`/`LongSpans`/
        /// `EndSpans`): one span per room cell, one window centred in it. `null` keeps the old even
        /// pitch (`bay`), which is what the House of Soviets' podium uses. Returns the centre of the
        /// first window of the first storey above ground, in building-local space, so the physics
        /// probe can look through it.
        /// </summary>
        private static Vector3? Wall(Geo g, Vector3 o, Vector3 u, Vector3 n, float len,
                                     float y0, float y1, int floors, float fh,
                                     float winW, float winH, float sill, float splay,
                                     float bay, List<Vector4> doors, List<Vector2> cols = null,
                                     int skip = 1, float pad = 0f, bool windows = true, int fromFloor = 0,
                                     int mask = -1)
        {
            o -= u * (len * 0.5f);          // the caller passes the wall's midpoint
            var holes = new List<Vector4>(doors);
            Vector3? first = null;
            if (windows)                    // v0.9.52: a solid facade (the House towers' inner walls)
            for (int f = Mathf.Clamp(fromFloor, 0, floors); f < floors; f++)
            {
                if ((mask & (1 << Mathf.Clamp(f, 0, 30))) == 0) continue;   // v0.9.60: per-floor mask
                float fy = y0 + f * fh;
                float wa = fy + sill, wb = wa + winH;
                if (wb > y1 - 0.2f) continue;
                if (cols == null)
                {
                    int c0 = Mathf.Max(1, Mathf.RoundToInt(len / bay));
                    // v0.9.17: `skip = 2` drops every second window of the pitch (the House of
                    // Soviets' podium) and stretches the rest evenly over the same wall
                    if (skip > 1) c0 = Mathf.Max(1, (c0 + skip - 1) / skip);
                    float pitch = len / c0;
                    for (int c = 0; c < c0; c++)
                    {
                        float cx = pitch * (c + 0.5f);
                        var r = new Vector4(cx - winW * 0.5f, wa, cx + winW * 0.5f, wb);
                        if (Overlaps(r, doors)) continue;
                        holes.Add(r);
                        if (f == 1 && first == null) first = o + u * cx + Vector3.up * ((wa + wb) * 0.5f);
                    }
                    continue;
                }
                for (int c = 0; c < cols.Count; c += Mathf.Max(1, skip))
                {
                    float a = cols[c].x, b = cols[c].y;
                    float w = Mathf.Min(winW, b - a + 2f * pad - 0.5f);
                    if (w < 0.9f) continue;
                    float cx = (a + b) * 0.5f;
                    var r = new Vector4(cx - w * 0.5f, wa, cx + w * 0.5f, wb);
                    if (Overlaps(r, doors)) continue;
                    holes.Add(r);
                    if (f == 1 && first == null) first = o + u * cx + Vector3.up * ((wa + wb) * 0.5f);
                }
            }

            // v0.9.56: the wall's two faces are cut from **two** hole lists вЂ” the exterior with the
            // window's outer rect, the interior with the same rect **inset by `splay`**. That brings the
            // splayed window frames back (v0.9.55 had flattened every reveal, which made each opening
            // 0,56 m wider and left it unframed on the inside) while keeping it watertight: the reveals
            // connect the outer rect to the inset one on both faces, so the open ring through which
            // the sky used to shine is closed.
            int nDoor = doors.Count;
            var holesIn = new List<Vector4>(holes.Count);
            for (int i = 0; i < holes.Count; i++)
            {
                Vector4 r0 = holes[i];
                if (i < nDoor || splay <= 0.001f) { holesIn.Add(r0); continue; }
                var ri = new Vector4(r0.x + splay, r0.y + splay, r0.z - splay, r0.w - splay);
                if (ri.z - ri.x < 0.1f || ri.w - ri.y < 0.1f) ri = r0;
                holesIn.Add(ri);
            }

            int EmitFaces(List<Vector4> hs, Vector3 fo, Vector3 fn)
            {
                var xs = new List<float> { 0f, len };
                var ys = new List<float> { y0, y1 };
                foreach (var r in hs) { xs.Add(r.x); xs.Add(r.z); ys.Add(r.y); ys.Add(r.w); }
                xs.Sort(); ys.Sort();
                xs = Clean(xs); ys = Clean(ys);
                int cells = xs.Count - 1;
                for (int j = 0; j + 1 < ys.Count; j++)
                {
                    float ya = ys[j], yb = ys[j + 1];
                    if (yb - ya < 0.002f) continue;
                    int i = 0;
                    while (i < cells)
                    {
                        if (!CellSolid(xs[i], xs[i + 1], ya, yb, hs)) { i++; continue; }
                        int k = i;
                        while (k < cells && CellSolid(xs[k], xs[k + 1], ya, yb, hs)) k++;
                        float xa = xs[i], xb = xs[k];
                        g.Quad(0, fo + u * xa + Vector3.up * ya, fo + u * xb + Vector3.up * ya,
                                  fo + u * xb + Vector3.up * yb, fo + u * xa + Vector3.up * yb, fn, 0.25f);
                        i = k;
                    }
                }
                return cells;
            }
            int cells = EmitFaces(holes, o, n)                     // exterior face
                      + EmitFaces(holesIn, o - n * T, -n);         // interior face, one thickness behind

            // reveals: they close the wall's thickness from the outer rect to the **inner** one
            for (int hi = 0; hi < holes.Count; hi++)
            {
                Vector4 r = holes[hi], ri = holesIn[hi];
                Vector3 oi = o - n * T;
                Vector3 e1 = o + u * r.x + Vector3.up * r.y;
                Vector3 e2 = o + u * r.x + Vector3.up * r.w;
                Vector3 e3 = o + u * r.z + Vector3.up * r.w;
                Vector3 e4 = o + u * r.z + Vector3.up * r.y;
                Vector3 i1 = oi + u * ri.x + Vector3.up * ri.y;
                Vector3 i2 = oi + u * ri.x + Vector3.up * ri.w;
                Vector3 i3 = oi + u * ri.z + Vector3.up * ri.w;
                Vector3 i4 = oi + u * ri.z + Vector3.up * ri.y;
                g.Quad(0, e1, e2, i2, i1, u, 0.5f);              // left jamb
                g.Quad(0, e4, e3, i3, i4, -u, 0.5f);             // right jamb
                g.Quad(0, e2, e3, i3, i2, Vector3.down, 0.5f);   // head
                g.Quad(0, e1, e4, i4, i1, Vector3.up, 0.5f);     // sill
                // v0.9.64: the **same four faces, reversed**. A single-sided reveal disappears when you
                // look into an opening from the far side (the jamb's normal points into the opening), and
                // that is the "РїСЂРѕР·СЂР°С‡РЅР°СЏ С‚РµРєСЃС‚СѓСЂР°" at windows seen at an angle / at the corners. Two
                // coplanar faces with opposite normals cannot z-fight вЂ” culling picks one.
                g.Quad(0, e2, e1, i1, i2, -u, 0.5f);
                g.Quad(0, e3, e4, i4, i3, u, 0.5f);
                g.Quad(0, e3, e2, i2, i3, Vector3.up, 0.5f);
                g.Quad(0, e4, e1, i1, i4, Vector3.down, 0.5f);
                // v0.9.64: and the wall's **end faces**, so a wall is a closed box and its hollow corner
                // cannot be looked into from outside
                g.Quad(0, o + Vector3.up * y0, o + Vector3.up * y1,
                          o - n * T + Vector3.up * y1, o - n * T + Vector3.up * y0, -u, 0.5f);
                g.Quad(0, o + u * len + Vector3.up * y0, o + u * len + Vector3.up * y1,
                          o + u * len - n * T + Vector3.up * y1, o + u * len - n * T + Vector3.up * y0, u, 0.5f);
            }
            // no glass anywhere: every opening stays empty
            // v0.9.57: the wall is a 0,94 m thick **skin** of two faces вЂ” nothing closed its top or
            // bottom, so from above it was hollow: at the podium's roof edge (the roof ring stopped
            // 0,94 m short of the facade) you looked straight down into the cavity, i.e. through the
            // wall вЂ” the "transparent facade in the horizontal plane" in the user's screenshot. These
            // two caps make every wall watertight; under a roof plate / floor slab they are hidden
            // (they sit 5 mm inside the wall so they can never be coplanar with those slabs)
            g.Quad(0, o + Vector3.up * (y0 + 0.005f), o + u * len + Vector3.up * (y0 + 0.005f),
                      o + u * len - n * T + Vector3.up * (y0 + 0.005f), o - n * T + Vector3.up * (y0 + 0.005f),
                      Vector3.down, 0.5f);
            g.Quad(0, o + Vector3.up * (y1 - 0.005f), o + u * len + Vector3.up * (y1 - 0.005f),
                      o + u * len - n * T + Vector3.up * (y1 - 0.005f), o - n * T + Vector3.up * (y1 - 0.005f),
                      Vector3.up, 0.5f);
            quadCount += holes.Count * 4 + cells + 2;
            return first;
        }

        private static List<float> Clean(List<float> l)
        {
            var r = new List<float>(l.Count);
            for (int i = 0; i < l.Count; i++)
                if (r.Count == 0 || l[i] - r[r.Count - 1] > 0.002f) r.Add(l[i]);
            return r;
        }

        private static bool Overlaps(Vector4 a, List<Vector4> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var b = list[i];
                if (a.x < b.z - 0.01f && a.z > b.x + 0.01f && a.y < b.w - 0.01f && a.w > b.y + 0.01f) return true;
            }
            return false;
        }

        private static bool CellSolid(float x0, float x1, float y0, float y1, List<Vector4> holes)
        {
            float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f;
            for (int i = 0; i < holes.Count; i++)
            {
                var h = holes[i];
                if (cx > h.x && cx < h.z && cy > h.y && cy < h.w) return false;
            }
            return true;
        }

        private static void AddDoor(List<Vector4> list, float len, float y, float w, float h)
            => list.Add(new Vector4(len - w * 0.5f, y, len + w * 0.5f, y + h));

        /// <summary>Two faces only (no sides) вЂ” for slab pieces whose edges are closed by something else.</summary>
        private static void RectFaces(Geo g, float x0, float x1, float z0, float z1, float yb, float yt)
        {
            if (x1 - x0 < 0.002f || z1 - z0 < 0.002f) return;
            g.Quad(0, new Vector3(x0, yt, z0), new Vector3(x1, yt, z0), new Vector3(x1, yt, z1), new Vector3(x0, yt, z1), Vector3.up, 0.2f);
            g.Quad(0, new Vector3(x0, yb, z0), new Vector3(x1, yb, z0), new Vector3(x1, yb, z1), new Vector3(x0, yb, z1), Vector3.down, 0.2f);
        }

        /// <summary>Slab with an optional rectangular hole: top and bottom faces plus the hole's sides.</summary>
        private static void Slab(Geo g, float x0, float x1, float z0, float z1, float yb, float yt, Vector4? hole)
        {
            if (hole == null)
            {
                Rect2(g, x0, x1, z0, z1, yb, yt);
                return;
            }
            var h = hole.Value;
            // v0.9.63: two-face pieces here вЂ” Rect2 is a closed box now and would z-fight with the rim
            RectFaces(g, x0, h.x, z0, z1, yb, yt);
            RectFaces(g, h.z, x1, z0, z1, yb, yt);
            RectFaces(g, h.x, h.z, z0, h.y, yb, yt);
            RectFaces(g, h.x, h.z, h.w, z1, yb, yt);
            // v0.9.42: the hole's rim must face **into the hole** (that is the side you see it from вЂ”
            // standing in the stairwell). The two z-edges had their normals the other way round, so the
            // floor plate's end face over the shaft was culled: you looked straight through the slab's
            // thickness into the storey below ("РЅРµРІРёРґРёРјС‹Р№ С‚РѕСЂРµС†").
            g.Quad(0, new Vector3(h.x, yb, h.y), new Vector3(h.x, yb, h.w), new Vector3(h.x, yt, h.w), new Vector3(h.x, yt, h.y), Vector3.right, 0.5f);
            g.Quad(0, new Vector3(h.z, yb, h.y), new Vector3(h.z, yb, h.w), new Vector3(h.z, yt, h.w), new Vector3(h.z, yt, h.y), Vector3.left, 0.5f);
            g.Quad(0, new Vector3(h.x, yb, h.y), new Vector3(h.z, yb, h.y), new Vector3(h.z, yt, h.y), new Vector3(h.x, yt, h.y), Vector3.forward, 0.5f);
            g.Quad(0, new Vector3(h.x, yb, h.w), new Vector3(h.z, yb, h.w), new Vector3(h.z, yt, h.w), new Vector3(h.x, yt, h.w), Vector3.back, 0.5f);
        }

        private static void Rect2(Geo g, float x0, float x1, float z0, float z1, float yb, float yt)
        {
            if (x1 - x0 < 0.002f || z1 - z0 < 0.002f) return;
            // v0.9.63: a **closed box**, not two faces. The two-face version left every slab's side edge
            // open, so at exterior corners and along the roof edge you looked *through* the slab (the
            // user's "РїСЂРѕР·СЂР°С‡РЅС‹Рµ С‚РµРєСЃС‚СѓСЂС‹" on vertical and horizontal outside corners).
            g.Box(0, new Vector3((x0 + x1) * 0.5f, (yb + yt) * 0.5f, (z0 + z1) * 0.5f),
                  new Vector3(x1 - x0, yt - yb, z1 - z0), Quaternion.identity, 0.2f);
        }

        private static void Piece(Geo g, Vector3 o, Vector3 u, Vector3 n, float s0, float s1, float y0, float y1)
        {
            if (s1 - s0 < 0.01f || y1 - y0 < 0.01f) return;
            Vector3 a = o + u * s0 + n * (PT * 0.5f);
            Vector3 b = o + u * s1 + n * (PT * 0.5f);
            g.Quad(0, a + Vector3.up * y0, b + Vector3.up * y0, b + Vector3.up * y1, a + Vector3.up * y1, n, 0.5f);
            Vector3 c = o + u * s0 - n * (PT * 0.5f);
            Vector3 d = o + u * s1 - n * (PT * 0.5f);
            g.Quad(0, c + Vector3.up * y0, d + Vector3.up * y0, d + Vector3.up * y1, c + Vector3.up * y1, -n, 0.5f);
            // the cut faces of a doorway are visible: close both ends
            g.Quad(0, a + Vector3.up * y0, c + Vector3.up * y0, c + Vector3.up * y1, a + Vector3.up * y1, -u, 0.5f);
            g.Quad(0, b + Vector3.up * y0, d + Vector3.up * y0, d + Vector3.up * y1, b + Vector3.up * y1, u, 0.5f);
            // v0.9.40: **the head and the sill**. Without them every lintel was an open slot through the
            // wall's thickness вЂ” looking up at any doorway you saw the ceiling of the room beyond as a
            // thin dark strip, i.e. exactly the "РїСЂРѕР·СЂР°С‡РЅР°СЏ С‚РµРєСЃС‚СѓСЂР°" the user kept photographing.
            g.Quad(0, a + Vector3.up * y1, b + Vector3.up * y1, d + Vector3.up * y1, c + Vector3.up * y1,
                   Vector3.up, 0.5f);
            g.Quad(0, a + Vector3.up * y0, b + Vector3.up * y0, d + Vector3.up * y0, c + Vector3.up * y0,
                   Vector3.down, 0.5f);
        }

        /// <summary>Outdoor entrance steps.</summary>
        private static void Steps(Geo g, Vector3 o, Vector3 u, Vector3 n, float width, float rise, int count)
        {
            float h = rise / count;
            for (int i = 0; i < count; i++)
            {
                float top = h * (i + 1);
                g.Box(0, o + n * (0.45f - i * 0.32f) + Vector3.up * (top * 0.5f),
                      new Vector3(width, top, 0.34f), Quaternion.LookRotation(n, Vector3.up), 0.6f);
            }
        }

        /// <summary>
        /// Balconies on the window columns of the +z facade. v0.9.12: they form **vertical stacks** on
        /// every other room bay (`c = 1, 3, 5 вЂ¦`) and sit exactly on the window column, with a real
        /// balcony door cut in the wall behind them (see `AddDoor` in `BuildBuilding`). Before, the
        /// column was chosen by `(c + f) % 3`, i.e. a diagonal scatter, and there was no door at all вЂ”
        /// which is what the user saw as "balconies in the wrong places".
        /// </summary>
        private static void Balconies(Geo g, Bldg b, float hd, float y0, List<Vector2> cols)
        {
            const float proj = 1.15f;      // how far the slab projects
            float top = y0 + b.floors * b.fh;
            int stacks = 0, made = 0;
            for (int c = 1; c < cols.Count; c += 2)
            {
                float span = cols[c].y - cols[c].x;
                float w = Mathf.Clamp(span + 0.55f, 2.0f, 2.9f);
                // `cols` are *wall* coordinates (0 = the wall's left end), the boxes are built in
                // building-local space вЂ” v0.9.12 fixes the half-facade shift this caused
                float x = (cols[c].x + cols[c].y) * 0.5f - b.w * 0.5f;
                if (Mathf.Abs(x) + w * 0.5f > b.w * 0.5f - 0.2f) continue;   // never past the corner
                stacks++;
                for (int f = 1; f < b.floors; f++)
                {
                    float y = y0 + f * b.fh;                    // slab top flush with the floor
                    if (y + 2.4f > top) continue;
                    made++;
                    if (!b.hasBalc) { b.hasBalc = true; b.balcSample = new Vector3(x, y + 0.5f, hd + proj * 0.5f); }
                    g.Box(0, new Vector3(x, y - 0.09f, hd + proj * 0.5f),
                          new Vector3(w, 0.18f, proj), Quaternion.identity, 0.6f);
                    // parapet: front rail plus two cheeks, 1.0 m above the slab
                    g.Box(0, new Vector3(x, y + 0.5f, hd + proj - 0.06f),
                          new Vector3(w, 1.0f, 0.12f), Quaternion.identity, 0.8f);
                    g.Box(0, new Vector3(x - w * 0.5f + 0.06f, y + 0.5f, hd + proj * 0.5f),
                          new Vector3(0.12f, 1.0f, proj), Quaternion.identity, 0.8f);
                    g.Box(0, new Vector3(x + w * 0.5f - 0.06f, y + 0.5f, hd + proj * 0.5f),
                          new Vector3(0.12f, 1.0f, proj), Quaternion.identity, 0.8f);
                }
            }
            log.LogInfo($"[BALC] {b.w:0.0}x{b.d:0.0} m block at {b.p.x:0.0},{b.p.z:0.0}: " +
                        $"{stacks} stacks over {cols.Count} window bays, {made} balconies, {b.floors} floors");
        }

        /// <summary>
        /// Stalinka: pilasters on the mullions of both long facades. v0.9.11 replaced the old
        /// 1.1 m deep projecting central bay here вЂ” it stood in front of the facade's middle windows
        /// and its "own windows" were quads on submesh 1, which this mesh no longer has, so all it did
        /// was block three windows per storey with a blank box.
        /// </summary>
        private static void Pilasters(Geo g, Bldg b, Shell sh, float hd, float y0, float yTop)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                float z = s * (hd + 0.06f);
                for (int k = 1; k < sh.seg; k++)
                {
                    float x = sh.x0 + k * sh.segLen;
                    g.Box(0, new Vector3(x, (y0 + yTop) * 0.5f, z),
                          new Vector3(0.55f, yTop - y0, 0.28f), Quaternion.identity, 0.3f);
                }
            }
        }

        // ------------------------------------------------------------------ covered bridges

        private static void PlanBridges()
        {
            int max = Mathf.Clamp(Mathf.RoundToInt(40 * Density), 8, 44);
            var rot = new List<Quaternion>(bldgs.Count);
            for (int i = 0; i < bldgs.Count; i++) rot.Add(Quaternion.Euler(0f, bldgs[i].yaw, 0f));
            for (int i = 0; i < bldgs.Count; i++)
            {
                for (int j = 0; j < bldgs.Count; j++)
                {
                    if (i == j || bridgeCount >= max) continue;
                    var A = bldgs[i];
                    var B = bldgs[j];
                    if (Mathf.Abs(A.baseY - B.baseY) > 0.6f) continue;   // v0.9.9: different terraces
                    Vector3 dir = B.p - A.p;
                    dir.y = 0f;
                    float dist = dir.magnitude;
                    if (dist < 10f || dist > 70f) continue;
                    dir /= dist;
                    // the corridor end of A must point at B, and B's +x must point away from A
                    if (Vector3.Dot(rot[i] * Vector3.right, dir) < 0.6f) continue;
                    if (Vector3.Dot(rot[j] * Vector3.right, dir) < 0.6f) continue;

                    Vector3 ea = A.p + rot[i] * new Vector3(A.w * 0.5f, 0f, 0f);
                    Vector3 eb = B.p + rot[j] * new Vector3(-B.w * 0.5f, 0f, 0f);
                    float span = Vector3.Distance(ea, eb);
                    if (span < 3f || span > 34f) continue;

                    // v0.9.3: every bridge crosses at the 11th storey, so the whole ring shares one level.
                    // v0.9.11: a stalinka's storey is 5.6 m instead of 3.2 m, so the two sides of a
                    // bridge look for a pair of floors that lands on the *same* height (4 x 5.6 =
                    // 7 x 3.2 = 22.4 m); the pair closest to the old 32.15 m deck wins.
                    int fa = -1, fb = -1;
                    float y = 0f, best = float.MaxValue;
                    for (int a2 = 2; a2 < Mathf.Min(A.floors - 1, 44); a2++)
                    {
                        float target = A.baseY + a2 * A.fh;
                        int b2 = Mathf.RoundToInt((target - B.baseY) / B.fh);
                        if (b2 < 2 || b2 >= Mathf.Min(B.floors - 1, 44)) continue;
                        float err = Mathf.Abs(B.baseY + b2 * B.fh - target);
                        if (err > 0.02f) continue;
                        float score = Mathf.Abs(target - (A.baseY + BridgeFloor * FH)) + err;
                        if (score < best) { best = score; fa = a2; fb = b2; y = target; }
                    }
                    if (fa < 0) continue;

                    if (A.endPlus == null) A.endPlus = new bool[A.floors];
                    if (B.endMinus == null) B.endMinus = new bool[B.floors];
                    if (A.endPlus[fa] || B.endMinus[fb]) continue;
                    A.endPlus[fa] = true;
                    B.endMinus[fb] = true;

                    bridges.Add(new BridgeSpec { a = ea, b = eb, y = y, ia = i, ib = j, floor = fa, fa = fa, fb = fb });
                    bridgeCount++;
                }
            }
            // rebuild the two door flags we just wrote (Bldg is a class, so they are shared)
        }

        private static void BuildBridge(BridgeSpec s)
        {
            Vector3 d = s.b - s.a;
            float len = d.magnitude;
            if (len < 1f) return;
            Vector3 dir = d / len;

            var go = new GameObject("bridge");
            go.transform.SetParent(root, false);
            go.transform.localPosition = s.a;
            go.transform.localRotation = Quaternion.LookRotation(dir, Vector3.up);

            var g = new Geo();
            float zc = len * 0.5f + 0.2f;
            len += 0.4f;
            // deck, roof, solid wall on the north side, parapet on the south side
            g.Box(0, new Vector3(0f, s.y - 0.15f, zc), new Vector3(2.7f, 0.3f, len), Quaternion.identity, 0.4f);
            g.Box(0, new Vector3(0f, s.y + 2.95f, zc), new Vector3(2.95f, 0.35f, len + 0.3f), Quaternion.identity, 0.4f);
            g.Box(0, new Vector3(1.35f, s.y + 1.45f, zc), new Vector3(0.26f, 2.9f, len), Quaternion.identity, 0.5f);
            g.Box(0, new Vector3(-1.35f, s.y + 0.55f, zc), new Vector3(0.26f, 1.1f, len), Quaternion.identity, 0.5f);
            // a beam under the roof so the span reads as a covered gallery
            for (float t = 1.5f; t < len - 1f; t += 2.4f)
                g.Box(0, new Vector3(0f, s.y + 2.72f, t), new Vector3(2.7f, 0.22f, 0.22f), Quaternion.identity, 0.6f);
            g.Apply(go, mRough);
            AddMeshCollider(go, "bridge");
        }

        // ------------------------------------------------------------------ House of Soviets (Kaliningrad)

        /// <summary>
        /// v0.9.60 вЂ” the House of Soviets rebuilt **to the front reference**. The real silhouette is an
        /// "H": two separate slab-towers with a wide **open gap** between them, joined by a two-storey
        /// **bridge** at ~55 % of the height, standing on a low two-storey stylobate whose front carries
        /// the long ribbon of windows and the entrance. v0.9.59 filled that gap with a loggia volume,
        /// which is why the contour did not match at all.
        /// Scale: the real house is a 13-storey, ~72-80 m building; here it is 13 storeys of 6,4 m =
        /// 83,4 m over the plinth (the rear blocks 89,8 m, the mast 110 m) вЂ” the reference's size В±15 %,
        /// not the old 218 m skyscraper. Plan 107,3 x 49,3 m, unchanged.
        /// Walkability: the stylobate's two-storey hall with its colonnade and the towers' entrances;
        /// 13 floors and a stairwell in each tower; the bridge connects them on floors 7-8 (doorways cut
        /// in the inner end walls, windows suppressed on exactly those two floors), so you can walk
        /// across the gap. No window looks into a wall: the towers' rear faces are solid (the rear
        /// blocks stand against them) and their inner faces are solid only where the bridge lands.
        /// </summary>
        private static void HouseOfSoviets(Vector3 p, float yaw)
        {
            var house = new GameObject("house_of_soviets");
            house.transform.SetParent(root, false);
            house.transform.localPosition = p;
            house.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var rot = Quaternion.Euler(0f, yaw, 0f);

            const float K = 1.45f;
            const float pw = 74f * K, pd = 34f * K;      // 107,3 x 49,3
            const float pfh = FH * 2f;                    // 6,4 m
            const float gTop = BASE + pfh;                // 6,55 вЂ” the overhang's slab
            float ptop = BASE + 2f * pfh;                 // 12,95
            float hw = pw * 0.5f, hd = pd * 0.5f;
            float tbase = BASE + p.y;
            var tp = p; tp.y = 0f;
            // the ground floor is **recessed 4 m** and carried on round columns (the photos), so the
            // podium's upper storey overhangs it
            const float inset = 4f;
            float iw = hw - inset, id = hd - inset;

            // towers: an 8-floor lower section (6 of them above the podium) and a 6-floor upper one set
            // 3 m back at the front, with glazed bays hanging over the break
            const float tw = 30f, td = 26f;
            const float tcx = 30.5f, tcz = 2f;
            const int lfl = 8, ufl = 7;
            float lTop = BASE + lfl * pfh;                // 51,35
            float uTop = lTop + ufl * pfh;                // 89,75
            const float twu = 30f, tdu = 22f, tczu = 3f;
            float tx0 = tcx - tw * 0.5f, tx1 = tcx + tw * 0.5f;     // 15,5..45,5
            float tz0 = tcz - td * 0.5f, tz1 = tcz + td * 0.5f;     // -11..15
            float uz0 = tczu - tdu * 0.5f;                          // -8 вЂ” the upper's front
            // the gallery on the lower floors 6-7, the thin bridge on the upper floors 2-3
            float galY0 = BASE + 6f * pfh, galY1 = BASE + 8f * pfh; // 38,55..51,35
            float brY0 = lTop + 2f * pfh, brY1 = lTop + 4f * pfh;   // 64,15..76,95
            const float coreW = 12f, coreD = 16f, coreZ0 = -2f, coreTop = 84f;
            const float rtop = 95f;                       // the rear blocks

            // --- stylobate, two levels: the **recessed ground floor on round columns** (the photos) and
            //     the overhanging upper storey with the long ribbon of windows and the entrance portal
            var podGo = new GameObject("podium");
            podGo.transform.SetParent(house.transform, false);
            var g = new Geo();
            const float rw = 1.4f, rh2 = 1.6f, rbay = 2.0f;
            Wall(g, new Vector3(0f, 0f, id), Vector3.right, Vector3.forward, 2f * iw, BASE, gTop, 1, pfh,
                 rw, rh2, 3.0f, 0.12f, rbay, new List<Vector4>(), null, 1, 0f);
            Wall(g, new Vector3(0f, 0f, -id), Vector3.left, Vector3.back, 2f * iw, BASE, gTop, 1, pfh,
                 rw, rh2, 3.0f, 0.12f, rbay, new List<Vector4>
                 {
                     new Vector4(iw - 7f, BASE, iw + 7f, BASE + 6.2f)          // the main entrance
                 }, null, 1, 0f);
            Wall(g, new Vector3(iw, 0f, 0f), Vector3.forward, Vector3.right, 2f * id, BASE, gTop, 1, pfh,
                 rw, rh2, 3.0f, 0.12f, rbay, new List<Vector4>(), null, 1, 0f);
            Wall(g, new Vector3(-iw, 0f, 0f), Vector3.back, Vector3.left, 2f * id, BASE, gTop, 1, pfh,
                 rw, rh2, 3.0f, 0.12f, rbay, new List<Vector4>(), null, 1, 0f);
            // the overhang's slab ring and its **round columns** along the front edge
            Rect2(g, -hw, hw, -hd, -id, gTop - 0.4f, gTop);
            Rect2(g, -hw, hw, id, hd, gTop - 0.4f, gTop);
            Rect2(g, -hw, -iw, -id, id, gTop - 0.4f, gTop);
            Rect2(g, iw, hw, -id, id, gTop - 0.4f, gTop);
            for (int i = -3; i <= 3; i++)
            {
                if (i == 0) continue;                 // v0.9.63: no column in the **entrance axis**
                Cylinder(g, i * 10f, -hd + 1.4f, 1.6f, BASE, gTop - 0.4f, 12);
            }
            // v0.9.63: a **monumental portal** instead of that column вЂ” four times the pedestrian scale:
            // two massive jambs, a deep lintel, a tipped canopy on two heavy struts and a raised
            // threshold slab inside the recess, all raw concrete
            g.Box(0, new Vector3(-9.2f, BASE + 4.7f, -id - 1.6f), new Vector3(4.4f, 9.4f, 3.2f), Quaternion.identity, 0.4f);
            g.Box(0, new Vector3(9.2f, BASE + 4.7f, -id - 1.6f), new Vector3(4.4f, 9.4f, 3.2f), Quaternion.identity, 0.4f);
            g.Box(0, new Vector3(0f, BASE + 7.6f, -id - 1.6f), new Vector3(22.8f, 3.0f, 3.2f), Quaternion.identity, 0.4f);
            // v0.9.68: the "monumental threshold" used to be **0,7 m tall** вЂ” twice the controller's
            // stepOffset, so the entrance could not be entered at all (the user's blocked passage).
            // It is a 0,14 m sill now: still reads as a threshold, and is simply walked over.
            g.Box(0, new Vector3(0f, BASE + 0.07f, -id + 2.6f), new Vector3(18f, 0.14f, 7.2f), Quaternion.identity, 0.5f);
            g.Box(0, new Vector3(0f, gTop + 4.4f, -id - 5.4f), new Vector3(30f, 1.6f, 9.5f), Quaternion.Euler(-7f, 0f, 0f), 0.4f);
            g.Box(0, new Vector3(-13f, BASE + 5.4f, -id - 8.4f), new Vector3(1.5f, 10.8f, 1.5f), Quaternion.identity, 0.4f);
            g.Box(0, new Vector3(13f, BASE + 5.4f, -id - 8.4f), new Vector3(1.5f, 10.8f, 1.5f), Quaternion.identity, 0.4f);
            // the upper storey at the full footprint: the ribbon on the sides and rear, and the wide
            // **loggia band** across the front (the photos' deep openings over the entrance)
            Wall(g, new Vector3(0f, 0f, hd), Vector3.right, Vector3.forward, pw, gTop, ptop, 1, pfh,
                 rw, rh2, 2.4f, 0.12f, rbay, new List<Vector4>(), null, 1, 0f);
            Wall(g, new Vector3(hw, 0f, 0f), Vector3.forward, Vector3.right, pd, gTop, ptop, 1, pfh,
                 rw, rh2, 2.4f, 0.12f, rbay, new List<Vector4>(), null, 1, 0f);
            Wall(g, new Vector3(-hw, 0f, 0f), Vector3.back, Vector3.left, pd, gTop, ptop, 1, pfh,
                 rw, rh2, 2.4f, 0.12f, rbay, new List<Vector4>(), null, 1, 0f);
            Wall(g, new Vector3(0f, 0f, -hd), Vector3.left, Vector3.back, pw, gTop, ptop, 1, pfh,
                 3.2f, 2.4f, 2.2f, 0.2f, 4.4f, new List<Vector4>(), null, 1, 0f);
            // the entrance canopy, projecting from the overhang
            g.Box(0, new Vector3(0f, gTop + 1.6f, -hd - 2.2f), new Vector3(22f, 0.9f, 4.8f), Quaternion.identity, 0.5f);

            // hall floor: the ring around the two tower footprints inside the **recessed** footprint
            Rect2(g, -iw, iw, -id, tz0, BASE - SLAB, BASE);
            Rect2(g, -iw, -tx1, tz0, tz1, BASE - SLAB, BASE);
            Rect2(g, tx1, iw, tz0, tz1, BASE - SLAB, BASE);
            Rect2(g, -tx0, tx0, tz0, tz1, BASE - SLAB, BASE);
            Rect2(g, -iw, iw, tz1, id, BASE - SLAB, BASE);
            // stylobate roof: the ring around the towers at the full footprint
            float ry = ptop;
            Rect2(g, -hw, hw, -hd, tz0, ry - SLAB, ry);
            Rect2(g, -hw, -tx1, tz0, tz1, ry - SLAB, ry);
            Rect2(g, tx1, hw, tz0, tz1, ry - SLAB, ry);
            Rect2(g, -tx0, tx0, tz0, tz1, ry - SLAB, ry);
            Rect2(g, -hw, hw, tz1, hd, ry - SLAB, ry);
            // colonnade in the hall's rear strip (the only bay wide enough for it). v0.9.67: no column on
            // the hall's axis вЂ” the central passage is flanked by a **pair** instead of blocked by one
            {
                float cz = (tz1 + hd - T) * 0.5f;
                for (int i = -4; i <= 4; i++)
                {
                    if (i == 0) continue;
                    g.Box(0, new Vector3(i * 11f, (BASE + ry) * 0.5f, cz),
                          new Vector3(2.4f, ry - BASE, 2.4f), Quaternion.identity, 0.6f);
                }
                for (int sd = -1; sd <= 1; sd += 2)
                    g.Box(0, new Vector3(sd * 4.5f, (BASE + ry) * 0.5f, cz),
                          new Vector3(2.4f, ry - BASE, 2.4f), Quaternion.identity, 0.6f);
            }
            g.Apply(podGo, mRough);
            AddMeshCollider(podGo, "podium");

            // --- the two towers, each **two sections**: an 8-floor lower block (6 floors above the
            //     stylobate, its first two a blank band) and a 6-floor upper block **set 3 m back at
            //     the front**, with glazed bays hanging over the break and a cornice on the break line
            const float twW = 2.4f, twH = 2.4f;
            int nuMask = ~((1 << 0) | (3 << 2));           // upper: door on 0, the bridge on floors 2-3
            int loMask = ~(3 << 6);                        // lower: the gallery on floors 6-7
            float upperBaseY = lTop + PlinthH;             // 61,35 вЂ” the upper's ground floor
            var bandGo = new GameObject("house_bands");
            bandGo.transform.SetParent(house.transform, false);
            var bg2 = new Geo();
            for (int s = -1; s <= 1; s += 2)
            {
                // ---- lower section: 30 x 26, eight floors, a blank two-storey base band
                var endsLo = new bool[lfl];
                endsLo[6] = endsLo[7] = true;              // gallery doorways
                BuildBuilding(new Bldg
                {
                    p = tp + rot * new Vector3(s * tcx, 0f, tcz),
                    yaw = yaw,
                    baseY = tbase,
                    w = tw,
                    d = td,
                    floors = lfl,
                    fh = pfh,
                    dh = 1.3f,
                    wall = mRough,
                    winW = 2.88f,
                    winH = 2.88f,
                    sill = 1.1f,
                    splay = 0.25f,
                    bay = 3.2f,
                    winSkip = 1,
                    winPad = 0f,
                    // below the stylobate's roof the tower stands inside the hall; the two floors above
                    // that roof are the blank base band of the reference
                    winFromFloor = 4,
                    winPlusZ = false,                      // the rear blocks stand against the rear faces
                    winMaskPlusX = s < 0 ? loMask : -1,
                    winMaskMinusX = s > 0 ? loMask : -1,
                    doorPlusX = s < 0,
                    doorMinusX = s > 0,
                    entranceW = 3.4f, entranceH = 4.4f,   // v0.9.69: a real portal, not a 1,7 m slit
                    towerDetail = true,                   // v0.9.72: courses, sills, pier ribs
                    endPlus = s < 0 ? endsLo : null,
                    endMinus = s > 0 ? endsLo : null,
                    corrMul = 3,           // v0.9.63: the corridor is three times wider (the rooms pay)
                    plinth = false,
                    interior = true,
                });

                // ---- the base band, 0,7 m proud of the shaft for two storeys, and its cornice
                {
                    float by = (12.95f + 25.75f) * 0.5f;
                    bg2.Box(0, new Vector3(s * tcx, by, tz0 - 0.35f), new Vector3(tw + 1.4f, 12.8f, 0.7f), Quaternion.identity, 0.5f);
                    bg2.Box(0, new Vector3(s * tcx, by, tz1 + 0.35f), new Vector3(tw + 1.4f, 12.8f, 0.7f), Quaternion.identity, 0.5f);
                    bg2.Box(0, new Vector3(s * tcx + s * (tw * 0.5f + 0.35f), by, tcz), new Vector3(0.7f, 12.8f, td), Quaternion.identity, 0.5f);
                    bg2.Box(0, new Vector3(s * tcx - s * (tw * 0.5f + 0.35f), by, tcz), new Vector3(0.7f, 12.8f, td), Quaternion.identity, 0.5f);
                    float cy = 25.75f + 0.175f;
                    bg2.Box(0, new Vector3(s * tcx, cy, tz0 - 0.45f), new Vector3(tw + 1.8f, 0.35f, 0.9f), Quaternion.identity, 0.5f);
                    bg2.Box(0, new Vector3(s * tcx, cy, tz1 + 0.45f), new Vector3(tw + 1.8f, 0.35f, 0.9f), Quaternion.identity, 0.5f);
                    bg2.Box(0, new Vector3(s * tcx + s * (tw * 0.5f + 0.45f), cy, tcz), new Vector3(0.9f, 0.35f, td + 1.8f), Quaternion.identity, 0.5f);
                    bg2.Box(0, new Vector3(s * tcx - s * (tw * 0.5f + 0.45f), cy, tcz), new Vector3(0.9f, 0.35f, td + 1.8f), Quaternion.identity, 0.5f);
                }

                // ---- upper section: 27 x 20, six floors, set 1,5 m back in x and 6 m at the rear, on the
                //      lower block's roof terrace; its ground-floor door opens onto that terrace
                var endsUp = new bool[ufl];
                endsUp[2] = endsUp[3] = true;              // the thin bridge's doorways
                var topDoor = new bool[ufl];
                topDoor[6] = true;                         // v0.9.66: the door into the annex
                BuildBuilding(new Bldg
                {
                    p = tp + rot * new Vector3(s * tcx, 0f, tczu),
                    yaw = yaw,
                    baseY = upperBaseY,
                    w = tw,
                    d = tdu,
                    floors = ufl,
                    fh = pfh,
                    dh = 1.3f,
                    wall = mRough,
                    winW = 2.88f,
                    winH = 2.88f,
                    sill = 1.1f,
                    splay = 0.25f,
                    bay = 3.2f,
                    winSkip = 1,
                    winPad = 0f,
                    winPlusZ = false,                      // the rear blocks stand against the rear faces
                    // v0.9.63: the glazed bays hang in front of these two floors вЂ” no windows behind them
                    winMaskMinusZ = ~(3 << 0),
                    winMaskPlusX = s < 0 ? nuMask : -1,
                    winMaskMinusX = s > 0 ? nuMask : -1,
                    doorPlusX = s < 0,
                    doorMinusX = s > 0,
                    entranceW = 3.4f, entranceH = 4.4f,   // v0.9.69: the same portal upstairs
                    towerDetail = true,
                    endPlus = s < 0 ? endsUp : null,
                    endMinus = s < 0 ? topDoor : endsUp,
                    corrMul = 3,           // v0.9.63: the corridor is three times wider (the rooms pay)
                    plinth = false,
                    interior = true,
                });

                // ---- the two bays hanging over the break on the upper's front face (v0.9.66: no glass вЂ”
                //      they are plain concrete volumes now)
                for (int b = -1; b <= 1; b += 2)
                {
                    float bx = s * tcx + b * 8.5f;
                    bg2.Box(0, new Vector3(bx, lTop + 6.4f, (uz0 + tz0) * 0.5f),
                            new Vector3(6f, 12.8f, uz0 - tz0), Quaternion.identity, 0.4f);
                }
                // ---- the cornice on the break line
                {
                    float byq = lTop + 0.2f;
                    bg2.Box(0, new Vector3(s * tcx, byq, tz0 - 0.45f), new Vector3(tw + 1.8f, 0.4f, 0.9f), Quaternion.identity, 0.5f);
                    bg2.Box(0, new Vector3(s * tcx, byq, tz1 + 0.45f), new Vector3(tw + 1.8f, 0.4f, 0.9f), Quaternion.identity, 0.5f);
                    bg2.Box(0, new Vector3(s * tcx + s * (tw * 0.5f + 0.45f), byq, tcz), new Vector3(0.9f, 0.4f, td + 1.8f), Quaternion.identity, 0.5f);
                    bg2.Box(0, new Vector3(s * tcx - s * (tw * 0.5f + 0.45f), byq, tcz), new Vector3(0.9f, 0.4f, td + 1.8f), Quaternion.identity, 0.5f);
                }
            }
            bg2.Apply(bandGo, mRough, mDark);
            AddMeshCollider(bandGo, "house_bands");

            // --- the **gallery** on the lower blocks' floors 6-7 (wide, 18 m deep, its roof is the
            //     terrace the upper blocks' ground doors open onto) and the **thin bridge** on the upper
            //     blocks' floors 2-3, as in the frontal reference: one wide band low, one narrow higher
            Bridge(house, "house_gallery", -tx0, tx0, -8f, 10f, galY0, galY1);
            Bridge(house, "house_bridge", -tx0, tx0, -6f, 4f, brY0, brY1,
                   new List<Vector4> { new Vector4(16.5f, brY0, 19.5f, brY0 + 4.2f) });   // the stair arrives here

            // --- the **narrow core** between the towers, set back from the front: the stair/lift shaft
            //     of the reference. v0.9.63: it is **fused** with the two links вЂ” doorways on the gallery
            //     and bridge floors on all four walls, a floor slab per storey, and an external concrete
            //     stair on its front from the gallery's roof up to the bridge.
            var coreGo = new GameObject("house_core");
            coreGo.transform.SetParent(house.transform, false);
            var cg = new Geo();
            float cFh = (coreTop - ptop) / 11f;
            int coreMask = ~((3 << 4) | (3 << 8));        // windows off where the links pass through
            float dA0 = ptop + 4f * cFh + 0.2f, dA1 = dA0 + 5.5f;   // the gallery doorway
            float dB0 = ptop + 8f * cFh + 0.2f, dB1 = dB0 + 5.5f;   // the bridge doorway
            for (int f = 0; f < 11; f++)
                Rect2(cg, -coreW * 0.5f, coreW * 0.5f, coreZ0, coreZ0 + coreD,
                      ptop + f * cFh - SLAB, ptop + f * cFh);
            Wall(cg, new Vector3(0f, 0f, coreZ0 + coreD), Vector3.right, Vector3.forward, coreW, ptop, coreTop, 11,
                 cFh, 1.6f, 2.2f, 1.4f, 0.18f, 2.4f, new List<Vector4>
                 {
                     new Vector4(coreW * 0.5f - 2.5f, dA0, coreW * 0.5f + 2.5f, dA1),
                     new Vector4(coreW * 0.5f - 2.5f, dB0, coreW * 0.5f + 2.5f, dB1)
                 }, null, 1, 0f, true, 0, coreMask);
            Wall(cg, new Vector3(0f, 0f, coreZ0), Vector3.left, Vector3.back, coreW, ptop, coreTop, 11,
                 cFh, 1.6f, 2.2f, 1.4f, 0.18f, 2.4f, new List<Vector4>
                 {
                     new Vector4(coreW * 0.5f - 2.5f, dA0, coreW * 0.5f + 2.5f, dA1),
                     new Vector4(coreW * 0.5f - 2.5f, dB0, coreW * 0.5f + 2.5f, dB1)
                 }, null, 1, 0f, true, 0, coreMask);
            Wall(cg, new Vector3(coreW * 0.5f, 0f, coreZ0 + coreD * 0.5f), Vector3.forward, Vector3.right, coreD,
                 ptop, coreTop, 11, cFh, 1.6f, 2.2f, 1.4f, 0.18f, 2.4f, new List<Vector4>
                 {
                     new Vector4(coreD * 0.5f - 2.5f, dA0, coreD * 0.5f + 2.5f, dA1),
                     new Vector4(coreD * 0.5f - 2.5f, dB0, coreD * 0.5f + 2.5f, dB1)
                 }, null, 1, 0f, true, 0, coreMask);
            Wall(cg, new Vector3(-coreW * 0.5f, 0f, coreZ0 + coreD * 0.5f), Vector3.back, Vector3.left, coreD,
                 ptop, coreTop, 11, cFh, 1.6f, 2.2f, 1.4f, 0.18f, 2.4f, new List<Vector4>
                 {
                     new Vector4(coreD * 0.5f - 2.5f, dA0, coreD * 0.5f + 2.5f, dA1),
                     new Vector4(coreD * 0.5f - 2.5f, dB0, coreD * 0.5f + 2.5f, dB1)
                 }, null, 1, 0f, true, 0, coreMask);
            Rect2(cg, -coreW * 0.5f, coreW * 0.5f, coreZ0, coreZ0 + coreD, coreTop - 0.4f, coreTop);
            // v0.9.64: the external stair is a **three-flight switchback** вЂ” 27 steps of 0,158 m per
            // flight (the old single run was 0,30 m per step, which the controller can barely climb),
            // with landings, a sloped handrail and posts on every flight
            float fl = (brY0 - galY1) / 3f;                // 4,27 m per flight
            const float x0s = -11.6f, x1s = -3.5f;         // the run along x
            Flight(cg, new Vector3(x0s, galY1, -6.5f), Vector3.right, 27, fl, 3.0f);
            Rect2(cg, x1s, x1s + 2.2f, -8f, -2f, galY1 + fl - 0.3f, galY1 + fl);
            Flight(cg, new Vector3(x1s, galY1 + fl, -3.5f), Vector3.left, 27, fl, 3.0f);
            Rect2(cg, x0s - 2.2f, x0s, -8f, -2f, galY1 + 2f * fl - 0.3f, galY1 + 2f * fl);
            Flight(cg, new Vector3(x0s, galY1 + 2f * fl, -6.5f), Vector3.right, 27, fl, 3.0f);
            Rect2(cg, x1s, x1s + 2.2f, -8f, -5f, brY0 - 0.3f, brY0);
            {
                float runH = 27 * 0.30f;
                float diag = Mathf.Sqrt(runH * runH + fl * fl);
                for (int f = 0; f < 3; f++)
                {
                    float y0f = galY1 + f * fl;
                    bool east = (f % 2) == 0;
                    Vector3 d = east ? Vector3.right : Vector3.left;
                    var o = new Vector3(east ? x0s : x1s, y0f, east ? -8.05f : -2.05f);
                    var q = Quaternion.LookRotation((d + Vector3.up * (fl / runH)).normalized, Vector3.up);
                    cg.Box(0, o + d * (runH * 0.5f) + Vector3.up * (fl * 0.5f + 1.0f), new Vector3(0.1f, 0.1f, diag), q, 0.5f);
                    for (int k = 0; k <= 4; k++)
                        cg.Box(0, o + d * (runH * k / 4f) + Vector3.up * (fl * k / 4f + 0.5f),
                               new Vector3(0.08f, 1.0f, 0.08f), Quaternion.identity, 0.5f);
                }
            }
            cg.Apply(coreGo, mRough);
            AddMeshCollider(coreGo, "house_core");

            // --- the rear blocks: two 30 x 9,65 m slabs against the towers' backs, rising above the
            //     towers (the reference's taller grey volumes) and the antenna mast on the west one
            var rearGo = new GameObject("house_rear");
            rearGo.transform.SetParent(house.transform, false);
            var rg = new Geo();
            float rz0 = tz1, rz1 = hd;
            for (int s = -1; s <= 1; s += 2)
            {
                float x0 = s * tx0, x1 = s * tx1;
                float cx = (x0 + x1) * 0.5f;
                Wall(rg, new Vector3(cx, 0f, rz1), Vector3.right, Vector3.forward, tw, ptop, rtop, 12,
                     (rtop - ptop) / 12f, 2.0f, 2.0f, 1.4f, 0.2f, 3.0f, new List<Vector4>(), null, 1, 0f);
                Wall(rg, new Vector3(x1, 0f, (rz0 + rz1) * 0.5f), Vector3.forward, Vector3.right, rz1 - rz0,
                     ptop, rtop, 12, (rtop - ptop) / 12f, 2.0f, 2.0f, 1.4f, 0.2f, 3.0f, new List<Vector4>(), null, 1, 0f);
                Wall(rg, new Vector3(x0, 0f, (rz0 + rz1) * 0.5f), Vector3.back, Vector3.left, rz1 - rz0,
                     ptop, rtop, 12, (rtop - ptop) / 12f, 2.0f, 2.0f, 1.4f, 0.2f, 3.0f, new List<Vector4>(), null, 1, 0f);
                Wall(rg, new Vector3(cx, 0f, rz0), Vector3.left, Vector3.back, tw, ptop, rtop, 12,
                     (rtop - ptop) / 12f, 2.0f, 2.0f, 1.4f, 0.2f, 3.0f, new List<Vector4>(), null, 1, 0f, false);
                Rect2(rg, Mathf.Min(x0, x1), Mathf.Max(x0, x1), rz0, rz1, rtop - 0.5f, rtop);
            }
            // the gap must be open **through** the building (the reference shows sky/city through it),
            // so there is deliberately no central rear block between the two rear slabs
            // the antenna mast on the west rear block (the reference's)
            rg.Box(0, new Vector3(-tcx, rtop + 10f, rz1 - 4.65f), new Vector3(1.2f, 20f, 1.2f), Quaternion.identity, 0.4f);
            rg.Box(0, new Vector3(-tcx, rtop + 0.3f, rz1 - 4.65f), new Vector3(3.2f, 0.6f, 3.2f), Quaternion.identity, 0.4f);
            rg.Apply(rearGo, mRough);
            AddMeshCollider(rearGo, "house_rear");

            // --- v0.9.65: the **portholes** of reference 1 spread evenly over the plain bands, and the
            //     west tower flowing into a cantilevered **chevron annex on splayed piers** (reference 2)
            var detGo = new GameObject("house_details");
            detGo.transform.SetParent(house.transform, false);
            var dg = new Geo();
            for (int s2 = -1; s2 <= 1; s2 += 2)
            {
                for (int i = -2; i <= 2; i++)
                    Porthole(dg, new Vector3(s2 * tcx + i * 5.2f, 19.3f, -11f), Vector3.back, 1.05f);
                for (int i = -1; i <= 1; i++)
                    Porthole(dg, new Vector3(s2 * tx1, 19.3f, tcz + i * 7f),
                             s2 > 0 ? Vector3.right : Vector3.left, 1.05f);
                for (int i = -1; i <= 1; i++)
                    Porthole(dg, new Vector3(s2 * hw, 11.9f, i * 12f),
                             s2 > 0 ? Vector3.right : Vector3.left, 0.8f);
            }
            for (int i = -4; i <= 4; i++)
                Porthole(dg, new Vector3(i * 10f, 11.9f, -hd), Vector3.back, 0.8f);
            // v0.9.67: a pair of **brutalist gargoyles** on the entrance canopy's front corners
            // v0.9.69: they stand on the **upper portal canopy** вЂ” the big 30 x 9,5 m slab tipped -7В°
            // over the entrance (its top runs 11,2..12,3 m). A flat concrete pedestal is sunk into the
            // slope, so the 3,5 m statues sit level on real support instead of being cut by the slab
            for (int gs = -1; gs <= 1; gs += 2)
            {
                float gx = gs * 11.5f;
                dg.Box(0, new Vector3(gx, 11.3f, -28.3f), new Vector3(2.4f, 0.9f, 2.4f), Quaternion.identity, 0.4f);
                Gargoyle(dg, gx, 11.75f, -28.3f);
            }
            // the annex: eight **splayed piers** from the lower tower's roof up to itвЂ¦
            const float acx = -30.5f, acz = 3f;
            for (int px = -1; px <= 1; px += 2)
                for (int pz = -1; pz <= 1; pz += 2)
                    Frustum(dg, 51.6f, 89.55f,
                            new Vector2(acx + px * 17.5f, acz + pz * 13f),
                            new Vector2(acx + px * 21f, acz + pz * 16.5f),
                            2.6f, 2.2f, 3.8f, 3.2f, Vector2.right, Vector2.up);
            // вЂ¦ and its three **walkable storeys** (6,4 m headroom each, no glass) with a stair well
            float ax0 = acx - 20f, ax1 = acx + 20f, az0 = acz - 17f, az1 = acz + 17f;
            const float hx0 = -25f, hx1 = -10.5f, hz0 = 14f, hz1 = 20f;   // the stair well: the rear strip
            Rect2(dg, ax0, ax1, az0, -8f, 89.35f, 89.75f);                 // floor 0: a ring round the tower
            Rect2(dg, ax0, ax1, 14f, az1, 89.35f, 89.75f);
            Rect2(dg, ax0, -45.5f, -8f, 14f, 89.35f, 89.75f);
            Rect2(dg, -15.5f, ax1, -8f, 14f, 89.35f, 89.75f);
            for (int lvl = 0; lvl < 2; lvl++)
            {
                float y = 96.15f + lvl * 6.4f;
                Rect2(dg, ax0, hx0, az0, az1, y - 0.4f, y);                // floors 1-2 round the well
                Rect2(dg, hx1, ax1, az0, az1, y - 0.4f, y);
                Rect2(dg, hx0, hx1, az0, hz0, y - 0.4f, y);
                Rect2(dg, hx0, hx1, hz1, az1, y - 0.4f, y);
            }
            Rect2(dg, ax0, ax1, az0, az1, 108.45f, 108.95f);               // the roof plate
            // the chevron points (45В° diamonds) on every plate
            for (int lvl = 0; lvl < 4; lvl++)
            {
                float y = (lvl == 0) ? 89.75f : (lvl == 1 ? 96.15f : (lvl == 2 ? 102.55f : 108.95f));
                dg.Box(0, new Vector3(acx, y - 0.55f, az0 - 2f), new Vector3(9f, 1.1f, 9f), Quaternion.Euler(0f, 45f, 0f), 0.35f);
                dg.Box(0, new Vector3(acx, y - 0.55f, az1 + 2f), new Vector3(9f, 1.1f, 9f), Quaternion.Euler(0f, 45f, 0f), 0.35f);
                dg.Box(0, new Vector3(ax0 - 2f, y - 0.55f, acz), new Vector3(9f, 1.1f, 9f), Quaternion.Euler(0f, 45f, 0f), 0.35f);
                dg.Box(0, new Vector3(ax1 + 2f, y - 0.55f, acz), new Vector3(9f, 1.1f, 9f), Quaternion.Euler(0f, 45f, 0f), 0.35f);
            }
            // the perimeter walls: real window openings, no glazing
            Wall(dg, new Vector3(acx, 0f, az1), Vector3.right, Vector3.forward, 40f, 89.75f, 108.95f, 3, 6.4f,
                 2.4f, 2.6f, 1.8f, 0.2f, 3.6f, new List<Vector4>(), null, 1, 0f);
            Wall(dg, new Vector3(acx, 0f, az0), Vector3.left, Vector3.back, 40f, 89.75f, 108.95f, 3, 6.4f,
                 2.4f, 2.6f, 1.8f, 0.2f, 3.6f, new List<Vector4>(), null, 1, 0f);
            Wall(dg, new Vector3(ax1, 0f, acz), Vector3.forward, Vector3.right, 34f, 89.75f, 108.95f, 3, 6.4f,
                 2.4f, 2.6f, 1.8f, 0.2f, 3.6f, new List<Vector4>(), null, 1, 0f);
            Wall(dg, new Vector3(ax0, 0f, acz), Vector3.back, Vector3.left, 34f, 89.75f, 108.95f, 3, 6.4f,
                 2.4f, 2.6f, 1.8f, 0.2f, 3.6f, new List<Vector4>(), null, 1, 0f);
            // the **experimental stair**: two long straight flights of 40 steps (0,16 m each) in the rear
            // strip, alternating lanes, with landings вЂ” its own design, clear of the tower's footprint
            Flight(dg, new Vector3(hx1, 89.75f, 15.5f), Vector3.left, 40, 6.4f, 3.0f);
            Rect2(dg, -24.5f, -22.5f, 14f, 17f, 95.85f, 96.15f);
            Flight(dg, new Vector3(-24.5f, 96.15f, 18.5f), Vector3.right, 40, 6.4f, 3.0f);
            Rect2(dg, -12.5f, hx1, 17f, 20f, 102.25f, 102.55f);
            dg.Apply(detGo, mRough, mDark);
            AddMeshCollider(detGo, "house_details");

            // v0.9.98: the air-hockey table on the annex's top floor (see AirHockeyTable)
            AirHockeyTable(house.transform);
            // v0.10.8: six patterned showroom tables on the floor below
            AirHockeyVariants(house.transform);
            // v0.10.17: five AI-slop arcade cabinets along the top hall's back wall
            ArcadeCabinets(house.transform);

            // --- v0.9.72: detail pass 1 (plan points 4/6) вЂ” the stylobate's roof balustrade, the
            //     portal's open steel leaves, wall sconces, the hall's pendant lamps, two dark-red
            //     banners and a mast with a red star on the core. House-local metres.
            {
                var d2Go = new GameObject("house_detail2");
                d2Go.transform.SetParent(house.transform, false);
                var d2 = new Geo();

                // roof balustrade along the stylobate's front edge (the figures stand behind it)
                {
                    float zr = -hd + 0.32f, yb = ptop;
                    for (float x = -hw + 1.2f; x <= hw - 1.19f; x += 2.55f)
                        d2.Box(0, new Vector3(x, yb + 0.45f, zr),
                               new Vector3(0.18f, 0.90f, 0.18f), Quaternion.identity, 0.5f);
                    d2.Box(1, new Vector3(0f, yb + 0.90f, zr),
                           new Vector3(2f * hw - 1.6f, 0.12f, 0.16f), Quaternion.identity, 0.5f);
                    d2.Box(1, new Vector3(0f, yb + 0.48f, zr),
                           new Vector3(2f * hw - 1.6f, 0.08f, 0.10f), Quaternion.identity, 0.5f);
                }

                // the portal's open leaves: two 6,7 x 5,6 m steel panels, hinged at В±7 m, swung 78В° in
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    float hx = sd * 7.0f;
                    var q = Quaternion.Euler(0f, sd * 78f, 0f);
                    Vector3 H = new Vector3(hx, BASE, -id + 0.2f);
                    Vector3 LP(float lx, float ly, float lz) => H + q * new Vector3(-sd * lx, ly, lz);
                    void DB(int part, float lx, float ly, float lz, float sx, float sy, float sz)
                        => d2.Box(part, LP(lx, ly, lz), new Vector3(sx, sy, sz), q, 0.5f);
                    DB(1, 3.35f, 2.8f, 0f, 6.70f, 5.60f, 0.24f);
                    for (int i = 1; i <= 3; i++)
                        DB(0, i * 6.70f / 4f, 2.8f, 0f, 0.12f, 5.40f, 0.28f);
                    DB(0, 3.35f, 2.8f, 0f, 6.70f, 0.14f, 0.28f);
                    DB(1, 6.35f, 1.35f, 0.20f, 0.10f, 1.10f, 0.10f);
                }

                // wall sconces: a bracket on the jamb's front face and one inside the hall
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    d2.Box(1, new Vector3(sd * 9.2f, 6.55f, -id - 3.32f),
                           new Vector3(0.42f, 0.52f, 0.34f), Quaternion.identity, 0.5f);
                    d2.Box(1, new Vector3(sd * 10.0f, 6.15f, -id + 1.04f),
                           new Vector3(0.36f, 0.46f, 0.18f), Quaternion.identity, 0.5f);
                }

                // the pendant lamps' rods and shades in the central passage (house-local)
                float ceilY = ptop - 0.78f;
                for (int i = -2; i <= 2; i++)
                {
                    float lx = i * 6f, ys = 8.30f;
                    float rodY = (ceilY + ys + 0.15f) * 0.5f, rodH = ceilY - ys - 0.15f;
                    d2.Box(1, new Vector3(lx, rodY, 2f), new Vector3(0.04f, rodH, 0.04f),
                           Quaternion.identity, 0.5f);
                    d2.Box(1, new Vector3(lx, ys, 2f), new Vector3(0.90f, 0.26f, 0.90f),
                           Quaternion.identity, 0.5f);
                }

                // the core's mast
                float mastBase = coreTop, mastTop = coreTop + 22f;
                float mz = coreZ0 + coreD * 0.5f;
                d2.Box(0, new Vector3(0f, (mastBase + mastTop) * 0.5f, mz),
                       new Vector3(0.34f, mastTop - mastBase, 0.34f), Quaternion.identity, 0.5f);
                for (int i = 0; i < 6; i++)
                    d2.Box(1, new Vector3(0f, mastBase + 2.4f + i * 2.6f, mz),
                           new Vector3(2.4f - i * 0.25f, 0.10f, 0.10f), Quaternion.identity, 0.5f);

                // the banners' metal rods (the fabric itself is a separate mesh below)
                for (int sd = -1; sd <= 1; sd += 2)
                    d2.Box(1, new Vector3(sd * 15.42f, 11.55f, -4.5f),
                           new Vector3(0.12f, 0.12f, 3.4f), Quaternion.identity, 0.5f);

                // v0.9.98: a warm pendant over the air-hockey table on the annex's top floor. The annex
                // had no light of its own (the House's pendants are in the hall 90 m below), so the table
                // would be a silhouette in the probe-SH gloom. Same fitting as the hall's: a dark rod, a
                // metal shade and a warm emissive lens.
                {
                    const float ahX = -37.75f, ahZ = 3f;
                    // v0.10.0: a **flush ceiling fixture** instead of the long pendant — the top-down
                    // mini-game camera sits at house-local ~108,5 and the old shade would block the rink.
                    d2.Box(1, new Vector3(ahX, 108.86f, ahZ), new Vector3(1.10f, 0.14f, 1.10f),
                           Quaternion.identity, 0.5f);
                    Lamp(house.transform, new Vector3(ahX, 108.55f, ahZ), 2.0f, 22f);
                }

                d2.Apply(d2Go, mRough, mMetal);
                AddMeshCollider(d2Go, "house_detail2");

                // the warm lenses (sconces + pendants) and the star are emissive second meshes
                var warmGo = new GameObject("house_warm");
                warmGo.transform.SetParent(house.transform, false);
                var wg = new Geo();
                // v0.9.98: the warm lens under the annex's air-hockey fixture (v0.10.0: flush with the
                // ceiling, above the top-down camera, so it never blocks the rink)
                wg.Box(1, new Vector3(-37.75f, 108.78f, 3f), new Vector3(0.90f, 0.05f, 0.90f),
                       Quaternion.identity, 0.5f);
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    wg.Box(1, new Vector3(sd * 9.2f, 6.30f, -id - 3.58f),
                           new Vector3(0.30f, 0.34f, 0.06f), Quaternion.identity, 0.5f);
                    wg.Box(1, new Vector3(sd * 10.0f, 5.94f, -id + 1.18f),
                           new Vector3(0.24f, 0.28f, 0.05f), Quaternion.identity, 0.5f);
                }
                for (int i = -2; i <= 2; i++)
                    wg.Box(1, new Vector3(i * 6f, 8.14f, 2f), new Vector3(0.72f, 0.06f, 0.72f),
                           Quaternion.identity, 0.5f);
                wg.Apply(warmGo, mRough, WarmGlowMaterial());
                AddMeshCollider(warmGo, "house_warm");

                // the red star: two crossed flat five-point stars at the mast's top
                var starGo = new GameObject("house_star");
                starGo.transform.SetParent(house.transform, false);
                var sg = new Geo();
                Star(sg, new Vector3(0f, mastTop + 2.2f, mz), 2.1f, Vector3.forward);
                Star(sg, new Vector3(0f, mastTop + 2.2f, mz), 2.1f, Vector3.right);
                sg.Apply(starGo, GlowMaterial());
                AddMeshCollider(starGo, "house_star");

                // the warm lights themselves (point lights; the level has no sun)
                for (int sd = -1; sd <= 1; sd += 2)
                    Lamp(house.transform, new Vector3(sd * 9.2f, 6.2f, -id - 3.9f), 1.7f, 18f);
                for (int i = -2; i <= 2; i++)
                    Lamp(house.transform, new Vector3(i * 6f, 7.9f, 2f), 1.1f, 16f);

                // two dark-red banners on the towers' inner walls, clear of the portals
                var banGo = new GameObject("house_banners");
                banGo.transform.SetParent(house.transform, false);
                var bng = new Geo();
                var embGo = new GameObject("house_banners_emblem");
                embGo.transform.SetParent(house.transform, false);
                var eg = new Geo();
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    float bx = sd * 15.42f;
                    bng.Box(0, new Vector3(bx, 7.5f, -4.5f), new Vector3(0.06f, 8.0f, 3.0f),
                            Quaternion.identity, 0.4f);
                    eg.Box(0, new Vector3(bx - sd * 0.04f, 9.7f, -4.5f), new Vector3(0.05f, 1.6f, 1.6f),
                           Quaternion.identity, 0.4f);
                }
                bng.Apply(banGo, BannerMaterial());
                AddMeshCollider(banGo, "house_banners");
                eg.Apply(embGo, mPainted);
                AddMeshCollider(embGo, "house_banners_emblem");
            }

            // v0.9.59: the five figures leave this roof for another Stalin block (see FiguresOnBlock)
            FiguresOnBlock();

            log.LogInfo($"[HOUSE] v0.9.63: built to the references вЂ” stylobate {pw:0.0}x{pd:0.0} m вЂ” " +
                        $"ground floor recessed {inset:0} m on 5 round columns, overhanging upper storey with " +
                        $"the window ribbon and the loggia band (top {ptop + p.y:0.0} m); each tower is two " +
                        $"sections: lower {tw:0.0}x{td:0.0} m, {lfl:0} floors with a blank 2-storey base band " +
                        $"and a cornice -> {lTop + p.y:0.0} m, upper {tw:0.0}x{tdu:0.0} m, {ufl:0} floors set " +
                        $"3 m back at the front with **glazed bays** over the break -> {uTop + p.y:0.0} m; " +
                        $"the {2f * tx0:0.0} m gap carries the **wide gallery** (lower floors 6-7) and the " +
                        $"**thin bridge** (upper floors 2-3); narrow core {coreW:0}x{coreD:0} m to " +
                        $"{coreTop + p.y:0.0} m; rear blocks to {rtop + p.y:0.0} m + a 20 m mast; plan " +
                        $"unchanged");
            builtCount++;
        }

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

        private static Bldg PickHeliBlock()
        {
            Bldg best = null;
            foreach (var b in bldgs)
            {
                if (b.memorial || b.penthouse) continue;
                if (b.w < 42f) continue;      // the 39,6 m fuselage must fit along the long axis
                if (best == null || b.floors > best.floors ||
                    (b.floors == best.floors && b.w > best.w))
                    best = b;
            }
            if (best != null)
                log.LogInfo($"[HELI] roof chosen: {best.w:0.0}x{best.d:0.0} m, {best.floors} floors, " +
                            $"top {best.Top:0.0} m at {best.p.x:0.0},{best.p.z:0.0} вЂ” the tallest free " +
                            $"block wide enough for the 39,6 m fuselage; its vent box is skipped");
            return best;
        }

        private static void Helicopter(Bldg b)
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(typeof(SovietDistrict).Assembly.Location);
                string path = System.IO.Path.Combine(dir ?? string.Empty, "BabbdiCoop", "mi26.glb");
                if (!System.IO.File.Exists(path))
                {
                    log.LogWarning("[HELI] mi26.glb is missing вЂ” skipped (" + path + ")");
                    return;
                }
                float roofTop = b.Top + 0.26f;
                // v0.9.89: park it centred on the dam's crest — the previous fixed offset left a
                // third of the machine hanging over the water. Measure the crest's width here and
                // drop the wheels exactly onto it.
                Vector3 spot = HeliSpot;
                Vector3 w0 = root.TransformPoint(spot);
                Vector3 dzL = root.rotation * Vector3.forward;
                float lo = 0f, hi = 0f;
                for (float t = -12f; t <= 12f; t += 0.5f)
                {
                    var p = w0 + dzL * t + Vector3.up * 8f;
                    if (Physics.Raycast(p, Vector3.down, out var hit, 30f, ~0, QueryTriggerInteraction.Ignore)
                        && Mathf.Abs(hit.point.y - w0.y) < 0.9f)
                    {
                        lo = Mathf.Min(lo, t);
                        hi = Mathf.Max(hi, t);
                    }
                }
                spot.z += (lo + hi) * 0.5f;
                Vector3 w1 = root.TransformPoint(spot);
                if (Physics.Raycast(w1 + Vector3.up * 8f, Vector3.down, out var gh, 30f, ~0, QueryTriggerInteraction.Ignore))
                    spot.y += gh.point.y - w0.y + 0.02f;
                Vector3 pos = spot;
                const float HeliScale = 1.00f;   // the GLB already carries the reference Г—1.20 dimensions
                var go = GlbFigureBuilder.Load(path, pos, HeliYaw, root, Shader.Find("Standard"),
                                               log, "heli_mi26", "HELI", true, true);
                go.transform.localScale = Vector3.one * HeliScale;
                var mf = go.GetComponentInChildren<MeshFilter>();
                var mr = go.GetComponentInChildren<Renderer>();
                var mb = mf != null ? mf.sharedMesh.bounds : new Bounds();
                var wb = mr != null ? mr.bounds : mb;
                Vector3 modelSize = mb.size * HeliScale;
                // v0.9.105: the eye/target are exact model-space points, framed and checked
                // offline in render_cam.py; the mesh's own transform maps them to the world,
                // so the bay camera lands inside the bay no matter where the machine stands.
                var mft = mf.transform;
                heliMesh = mft;
                heliInA = mft.TransformPoint(new Vector3(0f, 6.2f, -14f) * 1.3f);   // inside the cargo bay
                heliInB = mft.TransformPoint(new Vector3(0f, 3.2f, 10f) * 1.3f);    // aft, down at the ramp
                heliLowA = mft.TransformPoint(new Vector3(0f, 3.2f, 30f) * 1.3f);    // on the crest behind the ramp
                heliLowB = mft.TransformPoint(new Vector3(0f, 4.2f, 12f) * 1.3f);    // up at the ramp's underside
                heliSideA = mft.TransformPoint(new Vector3(-11f, 1.3f, 25f) * 1.3f);
                heliSideB = mft.TransformPoint(new Vector3(0f, 2.3f, 12f) * 1.3f);
                heliCabA = mft.TransformPoint(new Vector3(0.6f, 9.9f, -30.5f));  // inside the cockpit
                heliCabB = mft.TransformPoint(new Vector3(0f, 9.2f, -38.0f));
                heliRampA = mft.TransformPoint(new Vector3(-17f, 4.0f, 23f) * 1.3f);
                heliRampB = mft.TransformPoint(new Vector3(0f, 4.2f, 10f));
                heliEdgeA = mft.TransformPoint(new Vector3(-1.6f, 9.6f, 11.5f));
                heliEdgeB = mft.TransformPoint(new Vector3(-4.03f, 8.4f, 14.2f));
                // v0.9.113: the cargo hatch = two clamshell leaves + the ramp, each on its
                // own hinge, played in the real order: the leaves swing out, then the ramp
                // goes down; closing runs the other way.
                try
                {
                    string hatchDir = System.IO.Path.GetDirectoryName(path) ?? "";
                    var hingePos = new Dictionary<string, Vector3>();
                    var doorAxes = new Dictionary<string, (Vector3 axis, float sign)>();
                    string hfile = System.IO.Path.Combine(hatchDir, "mi26_hinges.txt");
                    if (System.IO.File.Exists(hfile))
                        foreach (var ln in System.IO.File.ReadAllLines(hfile))
                        {
                            var q = ln.Split(' ');
                            float F(int k) => float.Parse(q[k], System.Globalization.CultureInfo.InvariantCulture);
                            if (q.Length == 4)
                                hingePos[q[0]] = new Vector3(F(1), F(2), F(3));
                            else if (q.Length == 8)
                            {
                                hingePos[q[0]] = new Vector3(F(1), F(2), F(3));
                                doorAxes[q[0]] = (new Vector3(F(4), F(5), F(6)), F(7));
                            }
                        }
                    Vector3 HP(string k, Vector3 dflt) =>
                        hingePos.TryGetValue(k, out var v) ? v : dflt;
                    Transform Hinge(string nm, Vector3 local, string file, out Transform tilt)
                    {
                        var hg = new GameObject(nm);
                        hg.transform.position = mft.TransformPoint(local);
                        // v0.9.116: the hinge frame IS the mesh frame — no yaw assumption,
                        // so a closed leaf lands exactly on the skin it was cut from
                        hg.transform.rotation = mft.rotation;
                        var tl = new GameObject("tilt");
                        tl.transform.SetParent(hg.transform, false);
                        GlbFigureBuilder.Load(System.IO.Path.Combine(hatchDir, file), Vector3.zero, 0f,
                                              tl.transform, Shader.Find("Standard"), log, nm, "HELI", true, false);
                        tilt = tl.transform;
                        return hg.transform;
                    }
                    if (System.IO.File.Exists(System.IO.Path.Combine(hatchDir, "mi26_ramp.glb")))
                    {
                        var hatch = new GameObject("mi26_hatch").AddComponent<HeliHatch>();
                        Transform tRamp, tL, tR;
                        Hinge("mi26_ramp_hinge", HP("ramp", new Vector3(0f, 3.317f, 6.643f)),
                              "mi26_ramp.glb", out tRamp);
                        Hinge("mi26_door_l_hinge", HP("door_l", new Vector3(-2.655f, 5.064f, 11.647f)),
                              "mi26_door_l.glb", out tL);
                        Hinge("mi26_door_r_hinge", HP("door_r", new Vector3(2.655f, 5.064f, 11.647f)),
                              "mi26_door_r.glb", out tR);
                        hatch.Ramp = tRamp;
                        hatch.DoorL = tL;
                        hatch.DoorR = tR;
                        if (doorAxes.TryGetValue("door_l", out var al)) { hatch.AxisL = al.axis; hatch.SignL = al.sign; }
                        if (doorAxes.TryGetValue("door_r", out var ar)) { hatch.AxisR = ar.axis; hatch.SignR = ar.sign; }
                        // v0.9.116: the leaves' trim — the red-framed window of reference 1
                        foreach (var tr in new[] { (tL, "mi26_trim_l.glb", "heli_trim_l"),
                                                   (tR, "mi26_trim_r.glb", "heli_trim_r") })
                        {
                            string tp = System.IO.Path.Combine(hatchDir, tr.Item2);
                            if (false && System.IO.File.Exists(tp))      // trim parked: it does not follow the leaf yet
                                GlbFigureBuilder.Load(tp, Vector3.zero, 0f, tr.Item1, Shader.Find("Standard"),
                                                      log, tr.Item3, "HELI", false, false);
                        }
                        hatch.SetPose(1f);     // shipped open, as the ТЗ asks
                        heliHatch = hatch;
                        log.LogInfo("[HELI] cargo hatch: two clamshell leaves (3,5 x 5,0 m each) on " +
                                    "fore-aft hinges + the 10,3 x 7,4 m ramp; H plays the sequence " +
                                    $"leaves -> ramp over {hatch.Duration:0} s (closing is reversed); " +
                                    $"hinges read from mi26_hinges.txt ({hingePos.Count} entries)");
                    }
                    else log.LogWarning("[HELI] mi26_ramp.glb is missing — the hatch stays static");
                }
                catch (Exception e)
                {
                    log.LogError("[HELI] hatch failed: " + e);
                }
                float groundWorld = root.TransformPoint(spot).y;
                log.LogInfo($"[HELI] Mi-26 on the dam's crest, 50 m west of the district (local " +
                            $"{spot.x:0.0},{spot.z:0.0}; crest width measured {hi - lo:0.0} m): " +
                            $"wheels at y={wb.min.y:0.00} m (crest {groundWorld:0.00} world), nose along the dam, " +
                            $"model {modelSize.x:0.0} x {modelSize.y:0.0} x {modelSize.z:0.0} m " +
                            $"(reference 40,0 x 32,0 x 8,15 m x1,20 = 48,0 x 38,4 x 9,77; " +
                            $"reshaped nose/tail/rotor, {HeliScale:0.00}x), " +
                            $"{mf?.sharedMesh.vertexCount ?? 0} verts, hollow hull from the user's purple outline " +
                            "(side profile exact, +40 %, +25 % width, +15 % height, real window/porthole " +
                            "openings, cabin space inside, cockpit bulkhead, open cargo ramp, staggered " +
                            "portholes so the cabin is not see-through) + " +
                            "the ORIGINAL engine deck, intakes, gearbox fairing, main rotor and landing gear " +
                            "from the source model (x/z 1,20; belly 0,95 m off the ground; 1,5 deg nose-up; " +
                            $"track {2 * 4.1f:0.0} m; main axle {17.36f:0.00} m and nose axle {6.5f:0.00} m behind the nose)");
            }
            catch (Exception e)
            {
                log.LogError("[HELI] failed: " + e);
            }
        }

        /// <summary>
        /// v0.9.61 вЂ” one of the House of Soviets' bridges: a two-storey slab spanning between the
        /// towers, with a ribbon of small windows near its top and a solid parapet below, plus a
        /// floor and a ceiling plate. Its ends are the towers' own walls (the doorways are cut
        /// there), so it is open at both ends and can be walked through.
        /// </summary>
        private static void Bridge(GameObject parent, string name,
                                   float x0, float x1, float z0, float z1, float y0, float y1,
                                   List<Vector4> frontDoors = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var g = new Geo();
            float w = x1 - x0, cx = (x0 + x1) * 0.5f, h = y1 - y0;
            Rect2(g, x0, x1, z0, z1, y0 - 0.6f, y0);
            Rect2(g, x0, x1, z0, z1, y1 - 0.6f, y1);
            Wall(g, new Vector3(cx, 0f, z0), Vector3.left, Vector3.back, w, y0, y1, 1, h,
                 1.4f, 1.6f, h - 4.2f, 0.12f, 1.8f, frontDoors ?? new List<Vector4>(), null, 1, 0f);
            Wall(g, new Vector3(cx, 0f, z1), Vector3.right, Vector3.forward, w, y0, y1, 1, h,
                 1.4f, 1.6f, h - 4.2f, 0.12f, 1.8f, new List<Vector4>(), null, 1, 0f);
            g.Apply(go, mRough);
            AddMeshCollider(go, name);
        }

        /// <summary>v0.9.62 вЂ” an n-sided concrete column (the round supports under the House's podium).</summary>
        private static void Cylinder(Geo g, float x, float z, float r, float y0, float y1, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                var p0 = new Vector3(x + Mathf.Cos(a0) * r, 0f, z + Mathf.Sin(a0) * r);
                var p1 = new Vector3(x + Mathf.Cos(a1) * r, 0f, z + Mathf.Sin(a1) * r);
                float am = (a0 + a1) * 0.5f;
                var nrm = new Vector3(Mathf.Cos(am), 0f, Mathf.Sin(am)).normalized;
                g.Quad(0, p0 + Vector3.up * y0, p1 + Vector3.up * y0,
                          p1 + Vector3.up * y1, p0 + Vector3.up * y1, nrm, 0.5f);
                g.Tri(0, new Vector3(x, y1, z),
                          new Vector3(x + Mathf.Cos(a0) * r, y1, z + Mathf.Sin(a0) * r),
                          new Vector3(x + Mathf.Cos(a1) * r, y1, z + Mathf.Sin(a1) * r),
                          Vector3.up, Vector2.zero, Vector2.zero, Vector2.zero);
                g.Tri(0, new Vector3(x, y0, z),
                          new Vector3(x + Mathf.Cos(a1) * r, y0, z + Mathf.Sin(a1) * r),
                          new Vector3(x + Mathf.Cos(a0) * r, y0, z + Mathf.Sin(a0) * r),
                          Vector3.down, Vector2.zero, Vector2.zero, Vector2.zero);
            }
        }

        /// <summary>v0.9.63 вЂ” a straight concrete flight: `n` steps of `rise`/`n` along `dir`, 0.30 m treads.</summary>
        private static void Flight(Geo g, Vector3 o, Vector3 dir, int n, float rise, float width)
        {
            dir = dir.normalized;
            float h = rise / n;
            for (int i = 0; i < n; i++)
            {
                var c = o + dir * ((i + 0.5f) * 0.30f) + Vector3.up * (h * (i + 0.5f));
                g.Box(0, c, new Vector3(width, h * (i + 1), 0.30f), Quaternion.LookRotation(dir, Vector3.up), 0.6f);
            }
        }

        /// <summary>v0.9.65 вЂ” a round **blind** porthole borrowed from reference 1: a concrete collar
        /// (protruding ring) around a recessed concrete bottom with a shallow cast boss. v0.9.69: no
        /// glass вЂ” the old black disc read as a window pane, so the niche is plain concrete now.</summary>
        private static void Porthole(Geo g, Vector3 c, Vector3 n, float r, float depth = 0.26f, int seg = 12)
        {
            n = n.normalized;
            Vector3 ex = Vector3.Cross(Vector3.up, n);
            if (ex.sqrMagnitude < 0.01f) ex = Vector3.right;
            ex.Normalize();
            Vector3 ey = Vector3.Cross(n, ex).normalized;
            Vector3 o = c + n * depth;
            Vector3 disc = c - n * 0.07f;            // the blind bottom, inside the wall
            Vector3 boss = c - n * 0.01f;            // its shallow concrete relief
            const float rim = 0.22f;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * 2f * Mathf.PI / seg, a1 = (i + 1) * 2f * Mathf.PI / seg;
                Vector3 d0 = ex * Mathf.Cos(a0) + ey * Mathf.Sin(a0);
                Vector3 d1 = ex * Mathf.Cos(a1) + ey * Mathf.Sin(a1);
                float R = r + rim;
                g.Quad(0, o + d0 * r, o + d0 * R, o + d1 * R, o + d1 * r, n, 0.6f);
                g.Quad(0, c + d0 * R, o + d0 * R, o + d1 * R, c + d1 * R, (d0 + d1).normalized, 0.6f);
                g.Quad(0, c + d0 * r, o + d0 * r, o + d1 * r, c + d1 * r, -(d0 + d1).normalized, 0.6f);
                g.Tri(0, disc + d0 * r, disc + d1 * r, disc, n, Vector2.zero, Vector2.zero, Vector2.zero);
                g.Tri(0, boss + d0 * (r * 0.45f), boss + d1 * (r * 0.45f), boss, n,
                      Vector2.zero, Vector2.zero, Vector2.zero);
                g.Quad(0, disc + d0 * (r * 0.45f), disc + d1 * (r * 0.45f),
                          boss + d1 * (r * 0.45f), boss + d0 * (r * 0.45f), (d0 + d1).normalized, 0.6f);
            }
        }

        /// <summary>v0.9.67 вЂ” a **brutalist gargoyle** for the entrance canopy's corners: a crouched,
        /// hunched beast in raw concrete вЂ” long snout, recessed black eyes and mouth, horns, angular folded
        /// wings, clawed forelimbs, a curled tail. Mirrored by `s` (the pair faces the same way).</summary>
        private static void Gargoyle(Geo g, float x, float y, float z)
        {
            g.Box(0, new Vector3(x, y + 0.3f, z), new Vector3(1.8f, 0.6f, 1.8f), Quaternion.identity, 0.5f);
            g.Box(0, new Vector3(x, y + 1.35f, z), new Vector3(1.3f, 1.5f, 1.9f), Quaternion.Euler(-8f, 0f, 0f), 0.5f);
            g.Box(0, new Vector3(x, y + 2.15f, z - 0.2f), new Vector3(1.75f, 0.7f, 1.25f), Quaternion.Euler(-12f, 0f, 0f), 0.5f);
            g.Box(0, new Vector3(x, y + 2.52f, z - 1.05f), new Vector3(0.95f, 0.85f, 1.0f), Quaternion.Euler(10f, 0f, 0f), 0.5f);
            g.Box(0, new Vector3(x, y + 2.36f, z - 1.72f), new Vector3(0.65f, 0.45f, 0.62f), Quaternion.identity, 0.5f);
            g.Box(1, new Vector3(x, y + 2.22f, z - 1.94f), new Vector3(0.55f, 0.18f, 0.22f), Quaternion.identity, 0.5f);
            g.Box(1, new Vector3(x - 0.22f, y + 2.64f, z - 1.44f), new Vector3(0.16f, 0.12f, 0.16f), Quaternion.identity, 0.5f);
            g.Box(1, new Vector3(x + 0.22f, y + 2.64f, z - 1.44f), new Vector3(0.16f, 0.12f, 0.16f), Quaternion.identity, 0.5f);
            g.Box(0, new Vector3(x - 0.38f, y + 2.98f, z - 0.72f), new Vector3(0.14f, 0.52f, 0.14f), Quaternion.Euler(18f, 0f, -22f), 0.5f);
            g.Box(0, new Vector3(x + 0.38f, y + 2.98f, z - 0.72f), new Vector3(0.14f, 0.52f, 0.14f), Quaternion.Euler(18f, 0f, 22f), 0.5f);
            g.Box(0, new Vector3(x - 0.98f, y + 2.1f, z + 0.3f), new Vector3(0.18f, 1.6f, 0.95f), Quaternion.Euler(0f, 6f, 30f), 0.5f);
            g.Box(0, new Vector3(x + 0.98f, y + 2.1f, z + 0.3f), new Vector3(0.18f, 1.6f, 0.95f), Quaternion.Euler(0f, -6f, -30f), 0.5f);
            g.Box(0, new Vector3(x - 0.58f, y + 0.95f, z - 0.9f), new Vector3(0.34f, 1.2f, 0.34f), Quaternion.Euler(12f, 0f, 0f), 0.5f);
            g.Box(0, new Vector3(x + 0.58f, y + 0.95f, z - 0.9f), new Vector3(0.34f, 1.2f, 0.34f), Quaternion.Euler(12f, 0f, 0f), 0.5f);
            for (int i = -1; i <= 1; i++)
            {
                g.Box(0, new Vector3(x - 0.58f + i * 0.13f, y + 0.3f, z - 1.15f), new Vector3(0.1f, 0.3f, 0.32f), Quaternion.Euler(42f, 0f, 0f), 0.5f);
                g.Box(0, new Vector3(x + 0.58f + i * 0.13f, y + 0.3f, z - 1.15f), new Vector3(0.1f, 0.3f, 0.32f), Quaternion.Euler(42f, 0f, 0f), 0.5f);
            }
            g.Box(0, new Vector3(x, y + 1.2f, z + 1.15f), new Vector3(0.16f, 0.16f, 1.1f), Quaternion.Euler(-20f, 0f, 0f), 0.5f);
            g.Box(0, new Vector3(x, y + 1.55f, z + 1.62f), new Vector3(0.12f, 0.5f, 0.12f), Quaternion.Euler(-35f, 0f, 0f), 0.5f);
        }

        private static void Square()
        {
            // v0.9.9: the paved esplanade is the top of the House of Soviets' +10 m plinth
            var g = new Geo { FloorMul = PlatformGrain };
            g.Box(0, new Vector3(232.5f, PlinthH + 0.06f, 85f), new Vector3(135f, 0.12f, 80f), Quaternion.identity, DeckUv);
            var go = g.Emit(root, "square", mRough, null);
            AddMeshCollider(go, "square");
        }

        // ------------------------------------------------------------------ the penthouse (v0.9.18)

        /// <summary>Radius of the big clock dial вЂ” the photo's dials fill most of one facade.</summary>
        private const float DialR = 4.4f;

        // measured by `Penthouse`, read back by the probes вЂ” the tower's own numbers, not guesses
        private static float pentAw, pentAd, pentDeckY, pentApexY, pentTopY;

        /// <summary>
        /// v0.9.18 вЂ” **the clock tower on the block on the peninsula** (the outlying tower on the western
        /// wing, `Place(-40, 225, 3, 2, 1.25)`), rebuilt from the user's photo: not a one-storey attic
        /// drum but a real **four-storey** tower with huge clock dials and the atrium inside.
        ///
        /// Read off the photo, bottom to top:
        ///  1. **the base storey** вЂ” tall windows, three per face, and the entrance (2,4 x 3,2 m on `+z`);
        ///  2. **the big-window storey** вЂ” three **arched** windows per face, 2,4 x 4,2 m (see `ArchFill`);
        ///  3. **the clock storey** вЂ” **double height (10,2 m)** so a 9,2 m dial fits: the wall is solid
        ///     and the clock *is* the window, one per face, set in a bronze ring (the photo's proportions:
        ///     the dial is ~36 % of the facade and ~90 % of the storey);
        ///  4. **the top strip** вЂ” four small windows per face under the cornice;
        ///  5. **the cornice** вЂ” a 0,6 m plate overhanging the drum by 0,8 m;
        ///  6. **the steep mansard** in `M_MetalRusty00` вЂ” four planar trapezoids `1,0 x` the half-width
        ///     high, cut off at a platform `0,28` of the half-width, with **two rows of skylights** per
        ///     slope (4 + 3, the photo's roof windows);
        ///  7. **the glazed lantern** вЂ” a railing round the platform and a steel-framed glass box (3 m)
        ///     on top, the photo's little cupola.
        ///
        /// Inside (the photo's right half) the tower is a **four-storey atrium**: floor slabs as rings
        /// around a 9,2 x 9,2 m void, a 1,05 m parapet round the void on every floor, a **glass lift
        /// shaft** through the middle (dark frame + translucent panes) and a switchback stair in the east
        /// lane, so the player can walk from the door to the clock storey instead of only flying in.
        ///
        /// Materials: the building's own mesh carries concrete (part 0) and the roof metal (part 1);
        /// `det` (dark + the dial) holds the skylights, the railing and the eight clock faces, `gla`
        /// (dark + glass) the lift shaft and the lantern's panes вЂ” three objects, four materials.
        /// </summary>
        private static void Penthouse(Geo g, Bldg b, float hw, float hd, float yTop)
        {
            pentBlock = b;
            const float inX = 1.8f, inZ = 1.8f;           // the tower stands inside the roof parapet
            const float fh = 5.6f;                        // an ordinary storey (the district's tall one)
            const float ch = 10.2f;                       // the clock storey: double height
            float aw = hw - inX, ad = hd - inZ;
            float y0 = yTop + 0.26f;                      // the roof plate's top face вЂ” see Roof()
            float y1 = y0 + fh, y2 = y1 + fh, y3 = y2 + ch, yT = y3 + fh;
            pentAw = aw; pentAd = ad; pentDeckY = y0;

            var det = new Geo();                          // skylights/frames (dark) + the dials (part 1)
            var gla = new Geo();                          // frames (dark) + glass panes (part 1)

            // ---- 1. the four storeys
            DrumFloor(g, aw, ad, y0, fh, 2.0f, 3.4f, 1.2f, 3, true);     // ground: tall windows + door
            DrumFloor(g, aw, ad, y1, fh, 2.4f, 4.2f, 0.8f, 3, false);    // the big windows...
            float archR = 1.2f;                                          // ...with arched heads
            float ySpring = y1 + 0.8f + 4.2f - archR;
            foreach (float c in new[] { 0.5f, 1.5f, 2.5f })
            {
                ArchFill(g, new Vector3(0f, 0f, ad), Vector3.right, Vector3.forward, 2f * aw, 2f * aw / 3f * c, ySpring, archR);
                ArchFill(g, new Vector3(0f, 0f, -ad), Vector3.left, Vector3.back, 2f * aw, 2f * aw / 3f * c, ySpring, archR);
                ArchFill(g, new Vector3(aw, 0f, 0f), Vector3.forward, Vector3.right, 2f * ad, 2f * ad / 3f * c, ySpring, archR);
                ArchFill(g, new Vector3(-aw, 0f, 0f), Vector3.back, Vector3.left, 2f * ad, 2f * ad / 3f * c, ySpring, archR);
            }
            // the clock storey is a solid band вЂ” the dial is its window
            PlainWall(g, new Vector3(0f, 0f, ad), Vector3.right, Vector3.forward, 2f * aw, y2, y3);
            PlainWall(g, new Vector3(0f, 0f, -ad), Vector3.left, Vector3.back, 2f * aw, y2, y3);
            PlainWall(g, new Vector3(aw, 0f, 0f), Vector3.forward, Vector3.right, 2f * ad, y2, y3);
            PlainWall(g, new Vector3(-aw, 0f, 0f), Vector3.back, Vector3.left, 2f * ad, y2, y3);
            DrumFloor(g, aw, ad, y3, fh, 1.7f, 2.3f, 1.3f, 4, false);    // the top strip

            // ---- 2. the atrium: a central void through every slab, a parapet round it
            const float av = 4.6f;                        // the void's half-size
            float ai = aw - T + 0.10f, aj = ad - T + 0.10f;
            foreach (float yk in new[] { y1, y2, y3 })
            {
                g.Box(0, new Vector3(0f, yk - 0.15f, (av + aj) * 0.5f), new Vector3(2f * ai, 0.3f, aj - av), Quaternion.identity, 0.4f);
                g.Box(0, new Vector3(0f, yk - 0.15f, -(av + aj) * 0.5f), new Vector3(2f * ai, 0.3f, aj - av), Quaternion.identity, 0.4f);
                g.Box(0, new Vector3((av + ai) * 0.5f, yk - 0.15f, 0f), new Vector3(ai - av, 0.3f, 2f * av), Quaternion.identity, 0.4f);
                g.Box(0, new Vector3(-(av + ai) * 0.5f, yk - 0.15f, 0f), new Vector3(ai - av, 0.3f, 2f * av), Quaternion.identity, 0.4f);
                const float rt = 0.12f, rgt = 1.05f;
                g.Box(0, new Vector3(0f, yk + rgt * 0.5f, av + rt * 0.5f), new Vector3(2f * av, rgt, rt), Quaternion.identity, 0.5f);
                g.Box(0, new Vector3(av + rt * 0.5f, yk + rgt * 0.5f, 0f), new Vector3(rt, rgt, 2f * av), Quaternion.identity, 0.5f);
                g.Box(0, new Vector3(-(av + rt * 0.5f), yk + rgt * 0.5f, 0f), new Vector3(rt, rgt, 2f * av), Quaternion.identity, 0.5f);
                // the south side has a gap where the stair passes: two pieces, not one
                float gx0 = av - 2.15f, gx1 = av + 0.15f;
                g.Box(0, new Vector3((-av + gx0) * 0.5f, yk + rgt * 0.5f, -(av + rt * 0.5f)),
                      new Vector3(gx0 + av, rgt, rt), Quaternion.identity, 0.5f);
                g.Box(0, new Vector3((gx1 + av) * 0.5f, yk + rgt * 0.5f, -(av + rt * 0.5f)),
                      new Vector3(av - gx1, rgt, rt), Quaternion.identity, 0.5f);
            }

            // ---- 3. the glass lift shaft through the middle of the atrium
            const float sh = 1.35f;
            foreach (var s in new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(1f, 1f), new Vector2(-1f, 1f) })
                gla.Box(0, new Vector3(s.x * sh, (y0 + y3) * 0.5f, s.y * sh),
                        new Vector3(0.22f, y3 - y0, 0.22f), Quaternion.identity, 0.5f);
            foreach (float yk in new[] { y0 + 0.12f, y1, y2, y3 })
            {
                gla.Box(0, new Vector3(0f, yk, -sh), new Vector3(2f * sh + 0.22f, 0.16f, 0.16f), Quaternion.identity, 0.5f);
                gla.Box(0, new Vector3(0f, yk, sh), new Vector3(2f * sh + 0.22f, 0.16f, 0.16f), Quaternion.identity, 0.5f);
                gla.Box(0, new Vector3(-sh, yk, 0f), new Vector3(0.16f, 0.16f, 2f * sh), Quaternion.identity, 0.5f);
                gla.Box(0, new Vector3(sh, yk, 0f), new Vector3(0.16f, 0.16f, 2f * sh), Quaternion.identity, 0.5f);
            }
            float ey2 = (y0 + y3) * 0.5f, eh = y3 - y0;
            gla.Box(1, new Vector3(0f, ey2, sh), new Vector3(2f * sh, eh, 0.05f), Quaternion.identity, 0.5f);
            gla.Box(1, new Vector3(0f, ey2, -sh), new Vector3(2f * sh, eh, 0.05f), Quaternion.identity, 0.5f);
            gla.Box(1, new Vector3(sh, ey2, 0f), new Vector3(0.05f, eh, 2f * sh), Quaternion.identity, 0.5f);
            gla.Box(1, new Vector3(-sh, ey2, 0f), new Vector3(0.05f, eh, 2f * sh), Quaternion.identity, 0.5f);
            // the cage, parked on the ground floor
            det.Box(0, new Vector3(0f, y0 + 1.35f, 0f), new Vector3(2f * sh - 0.12f, 2.7f, 2f * sh - 0.12f), Quaternion.identity, 0.5f);

            // ---- 4. the stair in the atrium's east lane (walk up instead of only flying)
            for (int k = 0; k < 3; k++)
                AtticStair(g, new[] { y0, y1, y2 }[k], fh, av - 1.9f, av, -4.6f, av);

            // ---- 5. the cornice and the steep mansard
            float ex = aw + 0.8f, ez = ad + 0.8f;
            g.Box(0, new Vector3(0f, yT + 0.3f, 0f), new Vector3(2f * ex, 0.6f, 2f * ez), Quaternion.identity, 0.4f);
            float yC = yT + 0.6f;
            float rh = 1.0f * Mathf.Min(aw, ad);
            float yA = yC + rh;
            float ax = aw * 0.28f, az = ad * 0.28f;
            pentApexY = yA;
            var spots = new List<Vector2>
            {
                new Vector2(0.18f, 0.26f), new Vector2(0.40f, 0.26f), new Vector2(0.62f, 0.26f), new Vector2(0.84f, 0.26f),
                new Vector2(0.29f, 0.55f), new Vector2(0.50f, 0.55f), new Vector2(0.71f, 0.55f)
            }.ToArray();
            var winHalf = new Vector2(0.62f, 0.52f);
            Slope(g, det, new Vector3(-ex, yC, ez), new Vector3(ex, yC, ez),
                  new Vector3(ax, yA, az), new Vector3(-ax, yA, az), Vector3.forward, spots, winHalf);
            Slope(g, det, new Vector3(ex, yC, ez), new Vector3(ex, yC, -ez),
                  new Vector3(ax, yA, -az), new Vector3(ax, yA, az), Vector3.right, spots, winHalf);
            Slope(g, det, new Vector3(ex, yC, -ez), new Vector3(-ex, yC, -ez),
                  new Vector3(-ax, yA, -az), new Vector3(ax, yA, -az), Vector3.back, spots, winHalf);
            Slope(g, det, new Vector3(-ex, yC, -ez), new Vector3(-ex, yC, ez),
                  new Vector3(-ax, yA, az), new Vector3(-ax, yA, -az), Vector3.left, spots, winHalf);
            g.Quad(1, new Vector3(-ax, yA, -az), new Vector3(-ax, yA, az),
                      new Vector3(ax, yA, az), new Vector3(ax, yA, -az), Vector3.up, 0.5f);

            // ---- 6. the lantern on the platform: a railing, then a steel-framed glass box
            const float rw = 0.10f, rh2 = 1.05f;
            det.Box(0, new Vector3(0f, yA + rh2 * 0.5f, az - 0.2f), new Vector3(2f * ax, rh2, rw), Quaternion.identity, 0.5f);
            det.Box(0, new Vector3(0f, yA + rh2 * 0.5f, -az + 0.2f), new Vector3(2f * ax, rh2, rw), Quaternion.identity, 0.5f);
            det.Box(0, new Vector3(ax - 0.2f, yA + rh2 * 0.5f, 0f), new Vector3(rw, rh2, 2f * az), Quaternion.identity, 0.5f);
            det.Box(0, new Vector3(-ax + 0.2f, yA + rh2 * 0.5f, 0f), new Vector3(rw, rh2, 2f * az), Quaternion.identity, 0.5f);
            float cw = ax * 0.52f, cd = az * 0.52f, chh = 3.0f;
            foreach (var s in new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(1f, 1f), new Vector2(-1f, 1f) })
                gla.Box(0, new Vector3(s.x * cw, yA + chh * 0.5f, s.y * cd), new Vector3(0.18f, chh, 0.18f), Quaternion.identity, 0.5f);
            gla.Box(1, new Vector3(0f, yA + chh * 0.5f, cd), new Vector3(2f * cw, chh, 0.05f), Quaternion.identity, 0.5f);
            gla.Box(1, new Vector3(0f, yA + chh * 0.5f, -cd), new Vector3(2f * cw, chh, 0.05f), Quaternion.identity, 0.5f);
            gla.Box(1, new Vector3(cw, yA + chh * 0.5f, 0f), new Vector3(0.05f, chh, 2f * cd), Quaternion.identity, 0.5f);
            gla.Box(1, new Vector3(-cw, yA + chh * 0.5f, 0f), new Vector3(0.05f, chh, 2f * cd), Quaternion.identity, 0.5f);
            g.Box(1, new Vector3(0f, yA + chh + 0.12f, 0f), new Vector3(2f * cw + 0.6f, 0.24f, 2f * cd + 0.6f), Quaternion.identity, 0.5f);
            float ly = yA + chh + 0.24f;
            pentTopY = ly;
            const float lrw = 0.10f, lrh = 0.95f;
            det.Box(0, new Vector3(0f, ly + lrh * 0.5f, cd + 0.3f), new Vector3(2f * cw + 0.5f, lrh, lrw), Quaternion.identity, 0.5f);
            det.Box(0, new Vector3(0f, ly + lrh * 0.5f, -cd - 0.3f), new Vector3(2f * cw + 0.5f, lrh, lrw), Quaternion.identity, 0.5f);
            det.Box(0, new Vector3(cw + 0.3f, ly + lrh * 0.5f, 0f), new Vector3(lrw, lrh, 2f * cd + 0.6f), Quaternion.identity, 0.5f);
            det.Box(0, new Vector3(-cw - 0.3f, ly + lrh * 0.5f, 0f), new Vector3(lrw, lrh, 2f * cd + 0.6f), Quaternion.identity, 0.5f);

            // ---- 7. one great dial per face on the clock storey, in a bronze ring
            float cy = y2 + ch * 0.5f;
            Dial(det, new Vector3(0f, cy, ad + 0.04f), Vector3.right, Vector3.up, DialR);
            Dial(det, new Vector3(0f, cy, -ad - 0.04f), Vector3.left, Vector3.up, DialR);
            Dial(det, new Vector3(aw + 0.04f, cy, 0f), Vector3.back, Vector3.up, DialR);
            Dial(det, new Vector3(-aw - 0.04f, cy, 0f), Vector3.forward, Vector3.up, DialR);
            Ring(g, new Vector3(0f, cy, ad + 0.02f), Vector3.right, Vector3.up, DialR, DialR + 0.5f);
            Ring(g, new Vector3(0f, cy, -ad - 0.02f), Vector3.left, Vector3.up, DialR, DialR + 0.5f);
            Ring(g, new Vector3(aw + 0.02f, cy, 0f), Vector3.back, Vector3.up, DialR, DialR + 0.5f);
            Ring(g, new Vector3(-aw - 0.02f, cy, 0f), Vector3.forward, Vector3.up, DialR, DialR + 0.5f);

            // ---- 8. the objects of their own (the building mesh only has two materials)
            var dgo = new GameObject("penthouse_detail");
            dgo.transform.SetParent(b.go.transform, false);
            det.Apply(dgo, mDark, ClockMaterial());

            var ggo = new GameObject("penthouse_glass");
            ggo.transform.SetParent(b.go.transform, false);
            gla.Apply(ggo, mDark, GlassMaterial());

            // one warm lamp per storey in the void: the level has no sun, and an unlit atrium would be a
            // black column behind a lit facade
            foreach (float yk in new[] { y0, y1, y2, y3 })
                Lamp(ggo.transform, new Vector3(3.6f, yk + 3.4f, 3.6f), 1.1f, 16f);

            log.LogInfo($"[PENTHOUSE] clock tower on the block on the wing ({b.w:0.0}x{b.d:0.0} m, " +
                        $"{b.floors} floors, top {b.Top:0.0} m at {b.p.x:0.0},{b.p.z:0.0}): drum " +
                        $"{2f * aw:0.0}x{2f * ad:0.0} m (inset {inX:0.0} m), 4 storeys " +
                        $"{fh:0.0} + {fh:0.0} + {ch:0.0} (clock) + {fh:0.0} = {yT - y0:0.0} m, " +
                        $"atrium {2f * av:0.0}x{2f * av:0.0} m with a lift shaft and a stair, " +
                        $"cornice {2f * ex:0.0}x{2f * ez:0.0} m at y={yC:0.0}, mansard {rh:0.0} m in " +
                        $"{N(mMetal)} with 28 skylights, platform {2f * ax:0.0}x{2f * az:0.0} m at " +
                        $"y={yA:0.0}, glass lantern {2f * cw:0.0}x{2f * cd:0.0}x{chh:0.0} m, " +
                        $"4 clock dials r={DialR:0.00} m (one per face), roof deck y={y0:0.0}, " +
                        $"lantern top y={ly + lrh:0.0}");
        }

        /// <summary>
        /// v0.9.18 вЂ” **the arched head of one attic window**. `Wall` cuts rectangles only, so the opening
        /// is a rectangle up to the springing line and this fills the two spandrels above it: the visible
        /// hole becomes the rectangle plus a semicircle of radius `r`, and the arch gets a real soffit вЂ”
        /// a curved reveal across the wall's 0,94 m thickness вЂ” so it reads as an arch from inside the
        /// room as well. The reference's windows are exactly this shape.
        ///
        /// `o`/`u`/`n`/`len`/`cx` use the same convention as <see cref="Wall"/> (`o` is the wall's base
        /// midpoint, `cx` is measured from the wall's left end).
        /// </summary>
        private static void ArchFill(Geo g, Vector3 o, Vector3 u, Vector3 n, float len,
                                     float cx, float ySpring, float r, int N = 10)
        {
            o -= u * (len * 0.5f);
            Vector3 oi = o - n * T;
            System.Func<float, float> half = y =>
            {
                float dy = Mathf.Clamp(y - ySpring, 0f, r);
                return Mathf.Sqrt(Mathf.Max(0f, r * r - dy * dy));
            };
            for (int j = 0; j < N; j++)
            {
                float ya = ySpring + r * (j / (float)N), yb = ySpring + r * ((j + 1) / (float)N);
                float ha = half(ya), hb = half(yb);
                foreach (float s in new[] { -1f, 1f })         // the left and the right spandrel
                {
                    // exterior face: from the opening's outer edge (cx В± r) to the arc
                    Vector3 ea = o + u * (cx + s * r) + Vector3.up * ya;
                    Vector3 eb = o + u * (cx + s * r) + Vector3.up * yb;
                    Vector3 fa = o + u * (cx + s * ha) + Vector3.up * ya;
                    Vector3 fb = o + u * (cx + s * hb) + Vector3.up * yb;
                    g.Quad(0, ea, eb, fb, fa, n, 0.5f);
                    g.Quad(0, ea - n * T, eb - n * T, fb - n * T, fa - n * T, -n, 0.5f);
                    // the arch's soffit: the curved reveal, normal pointing into the opening
                    g.Quad(0, fa, fb, fb - n * T, fa - n * T, u * -s, 0.5f);
                }
            }
        }

        /// <summary>
        /// One roof slope: a planar trapezoid (part 1, the metal) plus three flush skylights (part 0 of
        /// the detail geo, the dark glass). `a`,`b` are the eave edge, `c`,`d` the platform edge above
        /// them, `outw` the direction the slope faces вЂ” the normal is flipped onto it, so the winding
        /// never depends on the caller's corner order.
        /// </summary>
        private static void Slope(Geo g, Geo det, Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                                  Vector3 outw, Vector2[] spots, Vector2 half)
        {
            var n = Vector3.Cross(b - a, d - a).normalized;
            if (Vector3.Dot(n, outw) < 0f) n = -n;
            g.Quad(1, a, b, c, d, n, 0.4f);
            var eu = (b - a).normalized;
            var slopeUp = ((d + c) * 0.5f - (a + b) * 0.5f).normalized;
            var up = Vector3.Cross(n, eu).normalized;           // in the slope plane, вЉҐ to the eave
            if (Vector3.Dot(up, slopeUp) < 0f) up = -up;
            // `spots` are (across the eave, up the slope) in 0..1 of the trapezoid, `half` the window's
            // half size in metres вЂ” the reference's roof has two rows of them, four over three
            foreach (var sp in spots)
            {
                float t = sp.y;
                // the trapezoid narrows as it rises: half the eave at the bottom, `ax/aw` at the top
                float eave = Mathf.Lerp(1f, 0.28f, t);
                float s = Mathf.Lerp(0.5f - eave * 0.5f, 0.5f + eave * 0.5f, sp.x);
                var p = Vector3.Lerp(Vector3.Lerp(a, b, s), Vector3.Lerp(d, c, s), t) + n * 0.06f;
                float wT = (b - a).magnitude * Mathf.Lerp(1f, 0.28f, t);
                float hw = Mathf.Min(half.x, wT * 0.5f - 0.2f);
                det.Quad(0, p - eu * hw + up * half.y, p + eu * hw + up * half.y,
                            p + eu * hw - up * half.y, p - eu * hw - up * half.y, n, 0.5f);
            }
        }

        /// <summary>One flat disc on a wall face вЂ” the clock dial (v0.9.18).</summary>
        private static void Dial(Geo g, Vector3 c, Vector3 u, Vector3 v, float r)
        {
            const int N = 48;
            var n = Vector3.Cross(u, v).normalized;
            var centre = new Vector2(0.5f, 0.5f);
            var prev = c + u * r;
            var prevUv = new Vector2(1f, 0.5f);
            for (int i = 1; i <= N; i++)
            {
                float a = i / (float)N * Mathf.PI * 2f;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                var p = c + (u * ca + v * sa) * r;
                var uv = new Vector2(0.5f + ca * 0.5f, 0.5f + sa * 0.5f);
                g.Tri(1, c, prev, p, n, centre, prevUv, uv);
                prev = p; prevUv = uv;
            }
        }

        /// <summary>A flat bronze annulus round a dial (v0.9.18) вЂ” the photo's metal bezel.</summary>
        private static void Ring(Geo g, Vector3 c, Vector3 u, Vector3 v, float r0, float r1)
        {
            const int N = 48;
            var n = Vector3.Cross(u, v).normalized;
            var po = u * r0; var pi = u * r1;
            for (int i = 1; i <= N; i++)
            {
                float a = i / (float)N * Mathf.PI * 2f;
                var d = u * Mathf.Cos(a) + v * Mathf.Sin(a);
                g.Quad(1, c + po, c + d * r0, c + d * r1, c + pi, n, 0.8f);
                po = d * r0; pi = d * r1;
            }
        }

        /// <summary>
        /// One storey of the tower (v0.9.18): the four facades off the district's own `Wall`, so the
        /// openings are real holes with reveals and the storey is enclosed on both faces. `bays` windows
        /// per face at `bay = 2В·half / bays`, `door` cuts the entrance in the middle of `+z`.
        /// </summary>
        private static void DrumFloor(Geo g, float aw, float ad, float y0, float fh,
                                      float winW, float winH, float sill, int bays, bool door)
        {
            var dZP = new List<Vector4>();
            var nil = new List<Vector4>();
            if (door) AddDoor(dZP, aw, y0, 2.6f, 3.4f);
            float bayZ = 2f * aw / bays, bayX = 2f * ad / bays;
            Wall(g, new Vector3(0f, 0f, ad), Vector3.right, Vector3.forward, 2f * aw, y0, y0 + fh, 1, fh,
                 winW, winH, sill, 0f, bayZ, dZP);
            Wall(g, new Vector3(0f, 0f, -ad), Vector3.left, Vector3.back, 2f * aw, y0, y0 + fh, 1, fh,
                 winW, winH, sill, 0f, bayZ, nil);
            Wall(g, new Vector3(aw, 0f, 0f), Vector3.forward, Vector3.right, 2f * ad, y0, y0 + fh, 1, fh,
                 winW, winH, sill, 0f, bayX, nil);
            Wall(g, new Vector3(-aw, 0f, 0f), Vector3.back, Vector3.left, 2f * ad, y0, y0 + fh, 1, fh,
                 winW, winH, sill, 0f, bayX, nil);
        }

        /// <summary>A wall with no openings at all (v0.9.18) вЂ” the clock storey's solid band.</summary>
        private static void PlainWall(Geo g, Vector3 o, Vector3 u, Vector3 n, float len, float y0, float y1)
        {
            o -= u * (len * 0.5f);
            Vector3 a = o, b = o + u * len;
            g.Quad(0, a + Vector3.up * y0, b + Vector3.up * y0, b + Vector3.up * y1, a + Vector3.up * y1, n, 0.5f);
            g.Quad(0, a - n * T + Vector3.up * y0, b - n * T + Vector3.up * y0,
                      b - n * T + Vector3.up * y1, a - n * T + Vector3.up * y1, -n, 0.5f);
        }

        /// <summary>
        /// One storey of the atrium stair (v0.9.18): two solid flights of 12 + 12 steps of `fh/24`
        /// (в‰¤ 0,25 m, so the character's 0,3 m step offset climbs it), a half landing and the exit
        /// landing that bridges the void's edge (`edgeX`) onto the gallery slab.
        /// </summary>
        private static void AtticStair(Geo g, float y, float fh, float x0, float x1, float z0, float edgeX,
                                       float zSign = 1f)
        {
            const int n = 12;
            float rise = fh / (2f * n), treadLen = 0.30f;
            float q1 = z0 + treadLen * n;               // the half-landing starts here (local run)
            float q2 = q1 + 1.5f;                       // and ends here
            float qe = q2 - treadLen * n;               // the exit landing ends here
            // v0.9.37: the flights are plates (FlightZ) and the landings closed slabs (Landing), like
            // the houses' stairs вЂ” the old box-per-step flights read as solid triangular masses.
            FlightZ(g, x0, x1, Mathf.Min(zSign * z0, zSign * q1), Mathf.Max(zSign * z0, zSign * q1),
                    y, n, rise, zSign < 0f, SLAB, StairPlate);                      // flight A, up
            Landing(g, x0, x1, Mathf.Min(zSign * q1, zSign * q2), Mathf.Max(zSign * q1, zSign * q2),
                    y + fh * 0.5f);                                                 // the half-landing
            FlightZ(g, x0, x1, Mathf.Min(zSign * qe, zSign * q2), Mathf.Max(zSign * qe, zSign * q2),
                    y + fh * 0.5f, n, rise, zSign > 0f, StairPlate, StairPlate);     // flight B, back
            // the exit landing, from the void's edge to the gallery slab
            g.Box(0, new Vector3((x0 + edgeX) * 0.5f, y + fh - StairPlate * 0.5f, zSign * (qe - 0.75f)),
                  new Vector3(edgeX - x0, StairPlate, 1.5f), Quaternion.identity, 0.4f);
        }

        // the Roman hour numerals, one string per hour
        private static readonly string[] Roman =
            { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII" };

        /// <summary>
        /// The dial (v0.9.18, rewritten for the 9,2 m clock): a 1024 px face вЂ” a bronze bezel, a
        /// patinated ring, a pale dial with 60 minute ticks, **Roman numerals drawn as strokes** and
        /// laid out radially (each numeral's foot towards the centre, like a tower clock), two ornate
        /// hands at ten past ten and a hub. Drawn in code, so the mod ships no extra file.
        /// </summary>
        private static Texture2D ClockTexture()
        {
            const int n = 1024;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true)
            {
                name = "CoopClockDial",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            var bronze = new Color(0.47f, 0.37f, 0.21f);
            var bronzeDk = new Color(0.18f, 0.14f, 0.09f);
            var patina = new Color(0.30f, 0.60f, 0.53f);          // verdigris, the reference's ring
            var pale = new Color(0.93f, 0.92f, 0.87f);            // the dial
            var ink = new Color(0.08f, 0.09f, 0.10f);

            // ---- the numerals: strokes in a box of height 1 glyph unit, `nh` dial units tall
            const float nh = 0.108f, vw = 0.62f, vgap = 0.17f;
            var segs = new List<Vector4>();
            System.Action<float, float, Vector2, Vector2, float, float, float, float> stroke =
                (midU, midV, right, outv, u0, v0, u1, v1) =>
                {
                    float sx = midU + (u0 * right.x + (v0 - 0.5f) * outv.x) * nh;
                    float sy = midV + (u0 * right.y + (v0 - 0.5f) * outv.y) * nh;
                    float ex = midU + (u1 * right.x + (v1 - 0.5f) * outv.x) * nh;
                    float ey = midV + (u1 * right.y + (v1 - 0.5f) * outv.y) * nh;
                    segs.Add(new Vector4(sx, sy, ex, ey));
                };
            for (int k = 1; k <= 12; k++)
            {
                string s = Roman[k];
                float width = 0f; int nc = 0;
                foreach (char ch in s) { width += ch == 'I' ? 0.30f : vw; nc++; }
                width += vgap * (nc - 1);
                float a = k * 30f * Mathf.Deg2Rad;
                var outv = new Vector2(Mathf.Sin(a), Mathf.Cos(a));   // radially outward
                var right = new Vector2(Mathf.Cos(a), -Mathf.Sin(a)); // clockwise along the ring
                var midU = outv.x * 0.585f; var midV = outv.y * 0.585f;
                float cur = -width * 0.5f;
                foreach (char ch in s)
                {
                    if (ch == 'I')
                        stroke(midU, midV, right, outv, cur + 0.15f, 0f, cur + 0.15f, 1f);
                    else if (ch == 'V')
                    {
                        stroke(midU, midV, right, outv, cur + 0.05f, 1f, cur + 0.31f, 0f);
                        stroke(midU, midV, right, outv, cur + 0.57f, 1f, cur + 0.31f, 0f);
                    }
                    else
                    {
                        stroke(midU, midV, right, outv, cur + 0.05f, 0f, cur + 0.57f, 1f);
                        stroke(midU, midV, right, outv, cur + 0.05f, 1f, cur + 0.57f, 0f);
                    }
                    cur += (ch == 'I' ? 0.30f : vw) + vgap;
                }
            }

            // a tapered blade from the centre, at `deg` from 12 clockwise, with a counterweight tail
            System.Func<float, float, float, float, float, float, bool> hand =
                (dx, dy, deg, len, wb, wt) =>
                {
                    float a = deg * Mathf.Deg2Rad;
                    float ux = Mathf.Sin(a), uy = Mathf.Cos(a);
                    float along = dx * ux + dy * uy, perp = -dx * uy + dy * ux;
                    if (along >= 0f && along <= len)
                        return Mathf.Abs(perp) <= Mathf.Lerp(wb, wt, along / len);
                    if (along < 0f && along > -0.11f)
                        return Mathf.Abs(perp) <= Mathf.Lerp(0.030f, 0.016f, -along / 0.11f);
                    return false;
                };

            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    Color c;
                    if (r > 0.985f) c = bronzeDk;                                   // the outer edge
                    else if (r > 0.945f) c = Color.Lerp(bronze, bronzeDk, (r - 0.945f) / 0.04f);
                    else if (r > 0.905f) c = bronze;                                // the bronze bezel
                    else if (r > 0.875f) c = Color.Lerp(patina, bronzeDk, (r - 0.875f) / 0.03f);
                    else if (r > 0.845f) c = patina;                                // the patinated ring
                    else if (r > 0.825f) c = bronzeDk;                              // the inner shadow line
                    else
                    {
                        c = pale;
                        // a soft vignette towards the rim, and a faint glass sheen from the upper left
                        c = Color.Lerp(c, new Color(0.78f, 0.77f, 0.72f), Mathf.Clamp01((r - 0.60f) / 0.22f) * 0.55f);
                        c = Color.Lerp(c, Color.white, Mathf.Clamp01(-dx * 0.45f + dy * 0.55f) * 0.10f);
                    }
                    // the minute ring: 60 ticks, every fifth one long and heavy
                    if (r > 0.695f && r < 0.815f)
                    {
                        float ang = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;            // 0 = 12, clockwise
                        int idx = Mathf.RoundToInt(ang / 6f);
                        bool hour = ((idx % 5) + 5) % 5 == 0;
                        float arc = Mathf.Abs(Mathf.DeltaAngle(ang, idx * 6f)) * Mathf.Deg2Rad * Mathf.Max(0.5f, r);
                        float tw = hour ? 0.011f : 0.0035f;
                        if (arc < tw * 0.5f && r > (hour ? 0.700f : 0.755f)) c = ink;
                    }
                    // the Roman numerals
                    if (r > 0.46f && r < 0.72f)
                        foreach (var sg in segs)
                            if (Seg(dx, dy, sg.x, sg.y, sg.z, sg.w, 0.0075f)) { c = ink; break; }
                    // ten past ten
                    if (hand(dx, dy, 305f, 0.44f, 0.026f, 0.008f)) c = ink;          // the hour hand
                    if (hand(dx, dy, 60f, 0.62f, 0.020f, 0.006f)) c = ink;           // the minute hand
                    if (r < 0.036f) c = ink;                                         // the hub
                    else if (r < 0.050f) c = Color.Lerp(ink, pale, (r - 0.036f) / 0.014f);
                    px[y * n + x] = c;
                }
            }
            t.SetPixels32(px);
            t.Apply(true, false);
            return t;
        }

        /// <summary>Is (x,y) within `w` of the segment (ax,ay)-(bx,by)? (the dial's hands)</summary>
        private static bool Seg(float x, float y, float ax, float ay, float bx, float by, float w)
        {
            float vx = bx - ax, vy = by - ay;
            float t = Mathf.Clamp01(((x - ax) * vx + (y - ay) * vy) / Mathf.Max(1e-6f, vx * vx + vy * vy));
            float px = ax + vx * t - x, py = ay + vy * t - y;
            return px * px + py * py <= w * w;
        }

        /// <summary>
        /// The dial material вЂ” the memorial's own chain (the game's emissive shader, so the face reads in
        /// the level's darkness and blooms slightly), but at a third of its boost: a clock is lit, not a
        /// lamp. Falls back to an unlit texture, then to the district's concrete with the dial as albedo.
        /// </summary>
        private static Material ClockMaterial()
        {
            if (mClock != null) return mClock;
            var tex = ClockTexture();
            foreach (var nm in new[] { "S_Emissive00", "S_EmissiveColor" })
            {
                var sh = Shader.Find(nm);
                if (sh == null || !sh.isSupported || sh.GetPropertyCount() <= 0) continue;
                var m = new Material(sh) { name = "CoopClock (" + nm + ")" };
                if (!m.HasProperty("_TextureSample0")) continue;
                m.SetTexture("_TextureSample0", tex);
                if (m.HasProperty("_EmissiveBoost")) m.SetFloat("_EmissiveBoost", 0.35f);
                if (m.HasProperty("_Color0")) m.SetColor("_Color0", Color.white);
                if (m.HasProperty("_texcoord")) m.SetVector("_texcoord", new Vector4(1f, 1f, 0f, 0f));
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
                log.LogInfo($"[PENTHOUSE] clock material: emissive {nm} tex={tex.name} ({tex.width}x{tex.height}), " +
                            $"boost=0.35, texcoord=(1.00, 1.00, 0.00, 0.00)");
                mClock = m;
                return m;
            }
            foreach (var nm in new[] { "Unlit/Texture", "Sprites/Default" })
            {
                var sh = Shader.Find(nm);
                if (sh == null || !sh.isSupported) continue;
                var m = new Material(sh) { name = "CoopClock (" + nm + ")" };
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
                log.LogInfo($"[PENTHOUSE] clock material: unlit {nm} tex={tex.name}");
                mClock = m;
                return m;
            }
            var f = new Material(mRough) { name = "CoopClock (concrete + dial)" };
            if (f.HasProperty("_BC")) f.SetTexture("_BC", tex);
            log.LogInfo("[PENTHOUSE] clock material: WARNING no emissive and no unlit shader вЂ” concrete + dial");
            mClock = f;
            return mClock;
        }

        /// <summary>
        /// The glass of the lantern and the lift shaft (v0.9.18): a translucent unlit sheet. The game
        /// ships `Sprites/Default` (it is in the build's shader list), which is alpha-blended, unlit and
        /// double-sided вЂ” exactly a pane. Falls back to the next transparent shader, then to the dark
        /// detail material (the panes then read as dark metal).
        /// </summary>
        private static Material GlassMaterial()
        {
            if (mGlass != null) return mGlass;
            foreach (var nm in new[] { "Sprites/Default", "Legacy Shaders/Particles/Alpha Blended",
                                       "Particles/Standard Unlit" })
            {
                var sh = Shader.Find(nm);
                if (sh == null || !sh.isSupported) continue;
                var m = new Material(sh) { name = "CoopGlass (" + nm + ")" };
                if (m.HasProperty("_Color")) m.SetColor("_Color", new Color(0.70f, 0.83f, 0.88f, 0.22f));
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", Texture2D.whiteTexture);
                log.LogInfo($"[PENTHOUSE] glass material: {nm} (alpha 0,22)");
                mGlass = m;
                return m;
            }
            log.LogInfo("[PENTHOUSE] glass material: WARNING no transparent shader вЂ” the panes are dark");
            mGlass = mDark;
            return mGlass;
        }

        /// <summary>v0.9.18: a warm lamp for the penthouse's room (see `Penthouse`).</summary>
        private static void Lamp(Transform parent, Vector3 local, float intensity, float range)
        {
            var go = new GameObject("penthouse_lamp");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.80f, 0.58f);
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForcePixel;
        }

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
            log.LogInfo("[GLOW] core material: WARNING no unlit shader and no Standard вЂ” concrete + gradient");
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

        public static List<PreviewShot> PreviewPoses()
        {
            var l = new List<PreviewShot>();
            if (root == null) return l;
            Func<Vector3, Vector3, PreviewShot> P = (a, b) => new PreviewShot
            {
                Pos = root.TransformPoint(a),
                Look = root.TransformPoint(b)
            };
            // v0.9.12: same, but lit вЂ” the level has no sun and ambient 0, so a facade in shadow is
            // practically black in a still; the lighting is a debug aid, the player never sees it
            Func<Vector3, Vector3, PreviewShot> PL = (a, b) => new PreviewShot
            {
                Pos = root.TransformPoint(a),
                Look = root.TransformPoint(b),
                Lamp = true
            };
            // TEMP (v0.9.21): the game's own humanoids, up close вЂ” the anatomy reference the five figures
            // on the House of Soviets' roof were built from. World space (the NPCs live in the level, not
            // under the district root), so no TransformPoint. Three named standing NPCs, two views each;
            // SpriteRenderer / ParticleSystemRenderer are skipped (the dialogue sprite of one NPC has a
            // 19 m bounds and is what ruined the first attempt at this shot).
            {
                var npcs = Resources.FindObjectsOfTypeAll<NPC>();
                var want = new[] { "SM_Amassou00", "SM_A_Ashraf00 (1)", "SM_A_Mina00" };
                foreach (var n in npcs)
                {
                    bool hit = false;
                    foreach (var w in want) if (n.name == w) hit = true;
                    if (!hit) continue;
                    var rs0 = n.GetComponentsInChildren<Renderer>(true);
                    var b = new Bounds(n.transform.position, Vector3.zero);
                    bool first = true;
                    foreach (var r in rs0)
                    {
                        if (r is SpriteRenderer) continue;
                        var tn = r.GetType().Name;
                        if (tn.StartsWith("Particle")) continue;
                        if (r.bounds.size == Vector3.zero) continue;
                        if (first) { b = r.bounds; first = false; }
                        else b.Encapsulate(r.bounds);
                    }
                    if (first || b.size.y < 1.0f || b.size.y > 2.6f) continue;   // not a standing body
                    var c = b.center;
                    float d = Mathf.Clamp(b.size.y * 1.35f, 2.4f, 4.0f);
                    l.Add(new PreviewShot { Pos = c + new Vector3(d * 0.62f, 0.15f, -d * 0.72f), Look = c, Lamp = true });
                    l.Add(new PreviewShot { Pos = c + new Vector3(-d * 0.80f, 0.15f, -d * 0.42f), Look = c, Lamp = true });
                }
            }
            l.Add(P(new Vector3(205f, 3f, -8f), new Vector3(205f, 14f, 90f)));    // stepping off the old crest
            l.Add(P(new Vector3(110f, 2f, 40f), new Vector3(250f, 25f, 120f)));  // street level in the ring
            l.Add(P(new Vector3(190f, 130f, -260f), new Vector3(190f, 20f, 110f))); // from the city, above the dam
            l.Add(P(new Vector3(60f, 70f, 420f), new Vector3(190f, 25f, 90f)));  // from the sea
            l.Add(P(new Vector3(60f, 130f, -60f), new Vector3(200f, 10f, 110f))); // the ring from high above
            l.Add(P(new Vector3(230f, 130f, -180f), new Vector3(230f, 70f, 84f)));  // House of Soviets, the whole monument
            l.Add(PL(new Vector3(120f, 30f, -40f), new Vector3(230f, 70f, 84f)));   // House of Soviets, 3/4 from the north-west
            // v0.9.17: the hall of the House of Soviets from high up its west aisle вЂ” the 25,6 m hall with
            // the two tower masses standing **in** it (they used to hang 10 m up, hidden above the roof
            // ring; that empty cavern is exactly what the user's screenshot showed). House-local
            // (-49, 18, -22,6) в†’ (-49, 2, 20), with the House's 4В° yaw baked in.
            l.Add(PL(new Vector3(179.5f, 28f, 64.9f), new Vector3(182.5f, 12f, 107.4f)));

            // v0.9.9: the terraced platform вЂ” the wall of the +8 m terrace from the street, the ring
            // ramp, the grand stair up to the House's plinth and the wing ramp to +14 m
            l.Add(P(new Vector3(56f, 2f, 150f), new Vector3(56f, 9f, 216f)));      // up the ring ramp
            l.Add(P(new Vector3(200f, 2f, 196f), new Vector3(200f, 11f, 214f)));  // a street under the +8 m terrace wall
            l.Add(P(new Vector3(230f, 2f, 2f), new Vector3(230f, 12.5f, 62f)));    // grand stair to the plinth
            l.Add(P(new Vector3(62f, 10f, 255f), new Vector3(-12f, 17f, 255f)));   // wing ramp to +14 m
            l.Add(P(new Vector3(330f, 10f, 292f), new Vector3(60f, 11f, 292f)));   // the seaward promenade at +8 m

            // inside a block: corridor on the second floor, and the staircase
            if (bldgs.Count > 0)
            {
                var b = bldgs[0];
                var rot = Quaternion.Euler(0f, b.yaw, 0f);
                float bfh = b.fh;
                float yf = b.baseY + 2f * bfh + 1.55f;
                Vector3 c0 = b.p + rot * new Vector3(0f, yf, 0f);
                Vector3 c1 = b.p + rot * new Vector3(b.w * 0.5f - 2f, yf, 0f);
                l.Add(P(c0, c1));                                               // corridor
                float zi = b.d * 0.5f - 0.4f;
                float zm = (b.stairZ0 + b.stairZ1) * 0.5f;
                Vector3 sa = b.p + rot * new Vector3(b.stairX1 - 0.45f, b.baseY + bfh + 1.60f, b.stairZ1 - 0.55f);
                Vector3 sb = b.p + rot * new Vector3(b.stairX0 + 1.5f, b.baseY + bfh + 1.25f, zm);
                l.Add(P(sa, sb));                                               // staircase, diagonal view
                Vector3 ra = b.p + rot * new Vector3(b.stairX1 + 2.4f, b.baseY + bfh + 1.5f, -(b.CorrHalf + 0.8f));
                Vector3 rb = b.p + rot * new Vector3(b.stairX1 + 2.4f, b.baseY + bfh + 1.2f, -zi);
                l.Add(P(ra, rb));                                               // a room

                // v0.9.11: stand in the room behind each recorded window and look out вЂ” the direct
                // evidence that no partition, no corridor wall and no stair flight sits behind it
                for (int i = 0; i < b.winPos.Count && i < b.winDir.Count; i++)
                {
                    Vector3 n = rot * b.winDir[i];
                    Vector3 c = b.p + rot * b.winPos[i];
                    l.Add(P(c - n * 5.5f, c));
                }
            }

            // v0.9.11: a stalinka as well вЂ” its 5.6 m storeys, its taller doors and its stair that now
            // runs along z (and a window from outside, to check that nothing sits behind the opening)
            for (int i = 0; i < bldgs.Count; i++)
            {
                var b = bldgs[i];
                if (!b.Tall) continue;
                var rot = Quaternion.Euler(0f, b.yaw, 0f);
                float fh = b.fh;
                float y = b.baseY + fh + 1.7f;
                l.Add(P(b.p + rot * new Vector3(b.w * 0.5f - 2.5f, y, 0f),
                        b.p + rot * new Vector3(0f, y, 0f)));                     // stalinka corridor
                l.Add(P(b.p + rot * new Vector3(b.stairX1 - 2.4f, b.baseY + fh + 1.65f, b.stairZ0 + 0.7f),
                        b.p + rot * new Vector3(b.stairX0 + 1.2f, b.baseY + fh * 0.35f, b.stairZ1 - 0.5f)));  // its stair
                // v0.9.16: the user's own frame вЂ” from the half-landing looking UP the upper flight,
                // which is exactly where "the stairs run into a blind ceiling"; and from the landing at
                // the corridor doorway looking up the lower flight
                float xm = (b.stairX0 + b.stairX1) * 0.5f;
                float zLow = b.stairZ0 + b.stairLand, zHigh = zLow + b.stairRun;
                l.Add(P(b.p + rot * new Vector3(xm, b.baseY + 2f * fh + fh * 0.5f + 1.65f,
                                                b.stairZ1 - b.stairLand * 0.55f),
                        b.p + rot * new Vector3(xm, b.baseY + 3f * fh - 0.3f, zLow + b.stairRun * 0.15f)));
                l.Add(P(b.p + rot * new Vector3(xm, b.baseY + fh + 1.65f, b.stairZ0 + b.stairLand * 0.5f),
                        b.p + rot * new Vector3(xm, b.baseY + fh + fh * 0.5f, zHigh - 0.3f)));
                Vector3 face = b.p + rot * new Vector3(0f, b.baseY + fh * 1.6f, b.d * 0.5f);
                l.Add(P(face + rot * new Vector3(0f, 0f, 22f), face));            // its facade from the street
                break;
            }
            // v0.9.12: a block with balconies, from the street вЂ” the evidence that they now form
            // vertical stacks on the window columns with a door cut behind each one. The first is an
            // orthographic elevation (the exact geometry), the second a lit street view from below.
            for (int i = 0; i < bldgs.Count; i++)
            {
                var b = bldgs[i];
                if (!b.balconies) continue;
                var rot = Quaternion.Euler(0f, b.yaw, 0f);
                float hd = b.d * 0.5f;
                float cy = b.baseY + 5f * b.fh;
                // 3/4 view from above-right, orthographic: the slabs and parapets separate from the
                // wall only when their tops and edges are in frame вЂ” a straight front elevation hides
                // them completely (same material, no ambient occlusion in this level)
                l.Add(new PreviewShot
                {
                    Pos = root.TransformPoint(b.p + rot * new Vector3(24f, cy + 14f, hd + 26f)),
                    Look = root.TransformPoint(b.p + rot * new Vector3(0f, cy, hd)),
                    Ortho = 20f,
                    Lamp = true
                });
                // and the view the player actually gets from the street, under the balconies
                Vector3 face = b.p + rot * new Vector3(0f, b.baseY + 3f * b.fh, hd);
                l.Add(PL(face + rot * new Vector3(8f, -11f, 22f), face + rot * new Vector3(0f, 3f, 0f)));
                break;
            }
            if (bridges.Count > 0)
            {
                var s = bridges[0];
                Vector3 d = s.b - s.a;
                d.y = 0f;
                Vector3 dir = d.normalized;
                Vector3 mid = s.a + dir * Mathf.Max(1.2f, d.magnitude * 0.5f) + Vector3.up * (s.y + 1.5f);
                l.Add(P(s.a + dir * 1.4f + Vector3.up * (s.y + 1.5f), s.a + dir * 18f + Vector3.up * (s.y + 1.4f))); // inside a covered bridge
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                l.Add(P(s.a + side * 16f + dir * 4f + Vector3.up * (s.y + 7f),
                        s.a + dir * (d.magnitude * 0.5f) + Vector3.up * (s.y + 1.5f)));  // a bridge from outside
            }

            // the joint itself (v0.9.6): stand seaward of the seam and look back along it вЂ” the old dam
            // crest on the left, the new platform on the right. The user's screenshot 1 lives here: the
            // two must read as one surface (tone, texture, grain), which is what Stone() is for.
            l.Add(P(new Vector3(208f, 1.8f, 13f), new Vector3(196f, 0.55f, 0f)));

            // v0.9.13: the corridor at its widest (4x) and at its narrowest (2x), same camera as the
            // stalinka hall вЂ” the evidence that the 2,2 m tunnel is gone
            foreach (int want in new[] { 4, 2 })
            {
                var b = bldgs.Find(x => x.corrMul == want);
                if (b == null) continue;
                var rot = Quaternion.Euler(0f, b.yaw, 0f);
                float y = b.baseY + 2f * b.fh + 1.7f;
                l.Add(P(b.p + rot * new Vector3(b.w * 0.5f - 2.5f, y, 0f),
                        b.p + rot * new Vector3(0f, y, 0f)));                      // 4x / 2x corridor
            }

            // v0.9.14: the memorial on the second-tallest roof вЂ” head-on, the reference's own low 3/4
            // (eye level, which is where the red slits must read), an elevated 3/4, and a mid-distance
            // shot from the neighbouring rooftops
            if (memBlock != null)
            {
                var rot = Quaternion.Euler(0f, memBlock.yaw, 0f);
                float y = memBlock.Top + 0.26f;
                l.Add(P(memBlock.p + rot * new Vector3(0f, y + 2.8f, 14f),
                        memBlock.p + rot * new Vector3(0f, y + 5.2f, 0f)));         // memorial, head-on
                l.Add(P(memBlock.p + rot * new Vector3(16f, y + 1.7f, 16f),
                        memBlock.p + rot * new Vector3(0f, y + 5.4f, 0f)));         // memorial, low 3/4
                l.Add(P(memBlock.p + rot * new Vector3(12.5f, y + 9.5f, 12.5f),
                        memBlock.p + rot * new Vector3(0f, y + 4.6f, 0f)));         // memorial, high 3/4
                l.Add(P(memBlock.p + rot * new Vector3(18f, y + 2.5f, 34f),
                        memBlock.p + rot * new Vector3(0f, y + 5f, 0f)));           // from the next roof
            }

            // v0.9.17: the House of Soviets as rebuilt вЂ” (a) the aisle down the west side of its hall
            // (a tower wall on one side, the podium's windows on the other), (b) the walk up the
            // approach avenue at eye level, (c) the podium facade head-on: 4,75 x 3,00 m openings of
            // which every second one is gone.
            l.Add(PL(new Vector3(179.6f, PlinthH + 3f, 66.5f), new Vector3(182.4f, PlinthH + 4f, 105.4f)));  // hall aisle
            l.Add(PL(new Vector3(230f, 1.8f, -34f), new Vector3(230f, PlinthH + 60f, 84f)));                 // approach avenue
            l.Add(PL(new Vector3(230f, PlinthH + 13f, 6f), new Vector3(230f, PlinthH + 13f, 60f)));          // podium facade

            // v0.9.18: the clock tower on the block on the peninsula вЂ” (a) the whole tower 3/4 from the
            // sea, (b) head-on at the clock storey (the reference's own framing: one great dial per
            // face), (c) the mansard from above (the two rows of skylights and the glass lantern),
            // (d) the atrium from the ground floor, looking up the void at the lift shaft.
            if (pentBlock != null)
            {
                var rot = Quaternion.Euler(0f, pentBlock.yaw, 0f);
                float pw = pentBlock.w * 0.5f, pd = pentBlock.d * 0.5f;
                float y0 = pentDeckY;
                l.Add(P(pentBlock.p + rot * new Vector3(pw + 48f, y0 + 26f, pd + 48f),
                        pentBlock.p + rot * new Vector3(0f, y0 + 19f, 0f)));        // the whole tower, 3/4
                l.Add(P(pentBlock.p + rot * new Vector3(0f, y0 + 16.3f, pd + 40f),
                        pentBlock.p + rot * new Vector3(0f, y0 + 16.3f, 0f)));      // head-on, the clock
                l.Add(P(pentBlock.p + rot * new Vector3(pw * 1.2f, y0 + 58f, pd * 1.6f),
                        pentBlock.p + rot * new Vector3(0f, y0 + 34f, 0f)));        // the mansard from above
                l.Add(P(pentBlock.p + rot * new Vector3(-3.2f, y0 + 1.75f, -3.2f),
                        pentBlock.p + rot * new Vector3(0.6f, y0 + 19f, 1.4f)));    // the atrium, from below
            }

            // v0.9.19/20: **one** house on legs (the pair is merged into a single rectangle in plan, the
            // western part lower) вЂ” (a) the pylons from the street under the transfer girder, (b) the whole
            // mass from the crest side, the reference's own framing, (c) through the open base between the
            // legs, which must read as a colonnade of V-shaped haunches
            var legsAll = bldgs.FindAll(x => x.legs);
            if (legsAll.Count > 0)
            {
                Vector3 lo = new Vector3(float.MaxValue, 0f, float.MaxValue);
                Vector3 hi = new Vector3(float.MinValue, 0f, float.MinValue);
                float top = 0f;
                foreach (var b in legsAll)
                {
                    var rr = Quaternion.Euler(0f, b.yaw, 0f);
                    for (int c = 0; c < 4; c++)
                    {
                        Vector3 cr = b.p + rr * new Vector3(c % 2 == 0 ? -b.w * 0.5f : b.w * 0.5f, 0f,
                                                            c / 2 == 0 ? -b.d * 0.5f : b.d * 0.5f);
                        lo = Vector3.Min(lo, cr);
                        hi = Vector3.Max(hi, cr);
                    }
                    top = Mathf.Max(top, b.floors * b.fh);
                }
                var ctr = new Vector3((lo.x + hi.x) * 0.5f, 0f, (lo.z + hi.z) * 0.5f);
                float span = Mathf.Max(hi.x - lo.x, hi.z - lo.z);
                float yb = legsAll[0].baseY;
                l.Add(PL(new Vector3(ctr.x, yb + 2.5f, hi.z + 62f),
                         new Vector3(ctr.x, yb + 11f, hi.z)));             // the legs from the street
                l.Add(P(new Vector3(ctr.x, top * 0.72f, lo.z - 250f),
                        new Vector3(ctr.x, top * 0.34f, ctr.z)));          // the whole house, from the crest
                l.Add(P(new Vector3(lo.x - span * 0.55f, yb + 3.2f, hi.z + span * 0.95f),
                        new Vector3(ctr.x, yb + 9f, ctr.z)));              // the colonnade under the girder
            }
            // v0.9.20/21: the platform's south-east corner next to the round hole in the sea вЂ” the +5 m
            // east terrace used to start right at the crest there, so the corner read as a bare 5 m ledge
            // above the water; it is now set back 12 m and the corner is one skin from the crest's granite
            // to the terrace's wall. (a) standing on the deck west of it, looking along the transition at
            // eye level вЂ” the change as it is met in play, (b) walking the edge east towards the hole,
            // (c) a low 3/4 from over the water (the "floating plate" the user photographed),
            // (d) a 3/4 from above, (e) the corner in plan.
            l.Add(PL(new Vector3(316f, 1.7f, 6f), new Vector3(342f, 0.35f, 4f)));
            l.Add(P(new Vector3(300f, 1.75f, 3f), new Vector3(372f, 0.6f, 8f)));
            l.Add(PL(new Vector3(398f, 9f, -22f), new Vector3(346f, 0.3f, 5f)));
            l.Add(PL(new Vector3(366f, 24f, -26f), new Vector3(338f, 0.3f, 6f)));
            l.Add(new PreviewShot
            {
                Pos = root.TransformPoint(new Vector3(340f, 55f, 6f)),
                Look = root.TransformPoint(new Vector3(340f, 0f, 6f)),
                Ortho = 34f
            });
            // v0.9.22: two views of the *walk itself*, which is what the user's screenshots 1-2 were
            // about вЂ” (f) coming in from the city along the crest's granite, i.e. the approach where the
            // concrete must emerge from under the stone without a lip, (g) standing on the plate and
            // looking back north-west along the seam + the bevel, the user's screenshot 2's framing.
            l.Add(PL(new Vector3(348f, 1.75f, -20f), new Vector3(352f, 0.5f, 6f)));
            l.Add(PL(new Vector3(340f, 1.65f, 9f), new Vector3(318f, 0.35f, 4f)));

            // v0.9.21: the five figures on the front edge of the House of Soviets' podium roof (see
            // Figures) вЂ” (a) from the plinth below, the way a player sees them: a line against the sky,
            // (b) a 3/4 from the front-left, (c) the middle figure up close, (d) along the row.
            {
                var fh = new Vector3(230f, PlinthH, 84f);
                var fr = Quaternion.Euler(0f, 4f, 0f);
                Vector3 roof = fh + fr * new Vector3(0f, 25.75f, -22.5f);
                l.Add(PL(fh + fr * new Vector3(0f, 27f, -96f), roof + Vector3.up * 0.9f));
                l.Add(PL(fh + fr * new Vector3(0f, 34f, -104f), fh + fr * new Vector3(0f, 26.6f, -22.5f)));
                // v0.9.26: the close-ups are framed off the figures' own feet (FigFeet), 3,2 m in
                // front of the row at 1,55 m and aimed at the chest. The old fixed offsets from the
                // roof centre (5,5-7,5 m, and 2,7 m further back than the figures actually stand)
                // left a 2,1 m figure at a third of the frame and the faces unreadable.
                Vector3 fMid = FigFeet.Count == 7 ? FigFeet[2] : roof + new Vector3(0f, 0f, -2.9f);
                Vector3 fEnd = FigFeet.Count == 7 ? FigFeet[4] : roof + new Vector3(40f, 0f, -2.9f);
                // v0.9.29: the five are 2,05-2,70 m tall now, so every close-up is framed off that
                // figure's OWN height (0,72 h for the camera, 0,47 h for the chest) - a fixed 1,55 m
                // would shoot the 2,70 m officer at the waist.
                float hm = FigHi(2), he = FigHi(4);
                l.Add(PL(fMid + new Vector3(0f, 0.72f * hm, 3.2f), fMid + new Vector3(0f, 0.47f * hm, 0f)));
                l.Add(PL(fh + fr * new Vector3(44f, 27.4f, -30f), fh + fr * new Vector3(-40f, 26.6f, -22.5f)));
                l.Add(PL(fMid + new Vector3(-2.8f, 0.71f * hm, 3.3f), fMid + new Vector3(0f, 0.47f * hm, 0f)));
                // v0.9.22: the jumpsuit worker up close вЂ” the one figure in the game's own **orange**,
                // which is the palette match the user's reference stills are about
                l.Add(PL(fEnd + new Vector3(0f, 0.70f * he, 3.2f), fEnd + new Vector3(0f, 0.47f * he, 0f)));
            }
            // v0.9.79: the Mi-26 on the chosen Stalin skyscraper's roof вЂ” from the open seaward
            // side at roof height and a 3/4 from above, so the rotor disc and the tail read
            // against the sky; both cameras are clear of the neighbouring blocks.
            if (heliBlock != null)
            {
                var hr = Quaternion.Euler(0f, HeliYaw, 0f);
                Vector3 hp = HeliSpot;
                l.Add(PL(hp + hr * new Vector3(0f, 24f, 72f), hp + Vector3.up * 8.5f));
                l.Add(PL(hp + hr * new Vector3(48f, 34f, 58f), hp + Vector3.up * 7.0f));
                l.Add(PL(hp + hr * new Vector3(0f, 4.8f, 22f), hp + hr * new Vector3(0f, 5.0f, 0f)));
                l.Add(PL(hp + hr * new Vector3(22f, 4.6f, 15f), hp + hr * new Vector3(3f, 4.0f, 0f)));
            }
            // v0.9.23: the user's own framing of the plate вЂ” standing on the deck south-west of the corner
            // looking east (the +5 terrace's wall ahead-left, the crest's granite on the right, the sea
            // beyond), and the same transition walked straight up from the south (the row that used to be
            // a 0,25-0,36 m wall at z = 12,3).
            l.Add(PL(new Vector3(301f, 1.7f, 15f), new Vector3(344f, 0.5f, 3f)));
            l.Add(PL(new Vector3(324f, 1.7f, 19f), new Vector3(327f, 0.4f, 6f)));
            // v0.9.25: the other two figures up close вЂ” the scientist (white lab coat, blue shirt,
            // dark bob) and the docker (navy beanie, orange hi-vis vest). They are the first two of
            // the row (x -40 / -20), so the v0.9.22 poses above never framed them.
            {
                var fh = new Vector3(230f, PlinthH, 84f);
                var fr = Quaternion.Euler(0f, 4f, 0f);
                Vector3 roof = fh + fr * new Vector3(0f, 25.75f, -22.5f);
                Vector3 f0 = FigFeet.Count == 7 ? FigFeet[0] : roof + new Vector3(-40f, 0f, -2.9f);
                Vector3 f1 = FigFeet.Count == 7 ? FigFeet[1] : roof + new Vector3(-20f, 0f, -2.9f);
                float h0 = FigHi(0), h1 = FigHi(1);
                l.Add(PL(f0 + new Vector3(0f, 0.72f * h0, 3.2f), f0 + new Vector3(0f, 0.47f * h0, 0f)));
                l.Add(PL(f1 + new Vector3(0f, 0.70f * h1, 3.2f), f1 + new Vector3(0f, 0.47f * h1, 0f)));
            }
            // v0.9.26: the officer (figure 4, x +20) up close вЂ” the peaked cap, the greatcoat, the
            // belt and the white dress gloves. Appended last so the earlier shot numbers stay put.
            {
                var fh = new Vector3(230f, PlinthH, 84f);
                var fr = Quaternion.Euler(0f, 4f, 0f);
                Vector3 roof = fh + fr * new Vector3(0f, 25.75f, -22.5f);
                Vector3 f3 = FigFeet.Count == 7 ? FigFeet[3] : roof + new Vector3(20f, 0f, -2.9f);
                float h3 = FigHi(3);
                l.Add(PL(f3 + new Vector3(0f, 0.72f * h3, 3.2f), f3 + new Vector3(0f, 0.52f * h3, 0f)));
            }
            // v0.9.78: the two helicopter pilots at the end of the row вЂ” the flight helmet with its
            // lowered, semi-transparent visor and the chest gear are the whole point of them.
            {
                var fh = new Vector3(230f, PlinthH, 84f);
                var fr = Quaternion.Euler(0f, 4f, 0f);
                Vector3 roof = fh + fr * new Vector3(0f, 25.75f, -22.5f);
                Vector3 f5 = FigFeet.Count == 7 ? FigFeet[5] : roof + new Vector3(40f, 0f, -2.9f);
                Vector3 f6 = FigFeet.Count == 7 ? FigFeet[6] : roof + new Vector3(60f, 0f, -2.9f);
                float h5 = FigHi(5), h6 = FigHi(6);
                l.Add(PL(f5 + new Vector3(0f, 0.70f * h5, 3.0f), f5 + new Vector3(0f, 0.50f * h5, 0f)));
                l.Add(PL(f6 + new Vector3(0f, 0.70f * h6, 3.0f), f6 + new Vector3(0f, 0.50f * h6, 0f)));
                // a head close-up on each visor, from slightly above so the tint reads
                l.Add(PL(f5 + new Vector3(0.9f, 0.86f * h5, 1.5f), f5 + new Vector3(0f, 0.84f * h5, 0f)));
                l.Add(PL(f6 + new Vector3(0.9f, 0.86f * h6, 1.5f), f6 + new Vector3(0f, 0.84f * h6, 0f)));
            }
            // v0.9.59: the rebuilt House of Soviets вЂ” envelope, honeycomb towers, loggia volume, hall,
            // entrance pavilion and the rear wing.
            {
                var fh0 = new Vector3(230f, PlinthH, 84f);
                var fr0 = Quaternion.Euler(0f, 4f, 0f);
                Func<float, float, float, Vector3> HL = (x, y, z) => fh0 + fr0 * new Vector3(x, y, z);
                // (a) the whole front from the esplanade вЂ” the "H": two slabs and the open gap
                l.Add(PL(HL(0f, 22f, -150f), HL(0f, 45f, -20f)));
                // (b) a 3/4 from the front-left
                l.Add(PL(HL(-90f, 55f, -100f), HL(0f, 45f, 0f)));
                // (c) the gap and the bridge head-on from below
                l.Add(PL(HL(0f, 30f, -70f), HL(0f, 50f, -8f)));
                // (d) inside the hall, from the entrance towards the colonnade
                l.Add(PL(HL(0f, 2f, -22f), HL(0f, 8f, 14f)));
                // (e) a tower's honeycomb above the stylobate roof
                l.Add(PL(HL(-30.5f, 16f, -45f), HL(-30.5f, 20f, -11f)));
                // (f) on the stylobate roof between the towers, up at the bridge
                l.Add(PL(HL(0f, 14f, -22f), HL(0f, 30f, -8f)));
                // (g) the rear blocks and the mast from behind
                l.Add(PL(HL(0f, 30f, 80f), HL(0f, 55f, 24.65f)));
                // (h) the stylobate's entrance from the esplanade
                l.Add(PL(HL(0f, 2f, -70f), HL(0f, 7f, -24.65f)));
                // (k) v0.9.68: the hall's central passage, looking at the towers from the axis
                l.Add(PL(HL(0f, 2f, 0f), HL(15.5f, 6f, 0f)));
                // (l) under the stylobate's overhang (the arcade), towards the porch
                l.Add(PL(HL(-6f, 2f, -23.5f), HL(-6f, 5f, -17f)));
                // (i) the House in the district's skyline from the north-east
                l.Add(PL(HL(60f, 45f, -210f), HL(0f, 45f, -20f)));
                // (j) the plan from above (the two slabs, the loggia volume and the rear wing)
                l.Add(new PreviewShot
                {
                    Pos = root.TransformPoint(HL(0f, 245f, 2f)),
                    Look = root.TransformPoint(HL(0f, 0f, 2f)),
                    Ortho = 95f,
                    Lamp = true
                });
                // (m) v0.9.69: the user's own framed view of the "blocked" passage вЂ” across the hall's
                //     central strip, from the west tower's entrance towards the east tower's portal
                l.Add(PL(HL(-14f, 1.8f, 2f), HL(45f, 4f, 2f)));
                // (n) the east tower's ground-floor portal up close (it must be an open 3,4 x 4,4 m
                //     passage, not a 1,7 m slit behind a concrete fin)
                l.Add(PL(HL(9f, 1.8f, 2f), HL(16f, 3.6f, 2f)));
                // (o) the pair of gargoyles on the portal canopy, from the esplanade below
                l.Add(PL(HL(0f, 2f, -48f), HL(0f, 13.5f, -27f)));
                // (p) v0.9.98: the air-hockey table on the annex's top floor (house-local 102,55 m) вЂ”
                //     a 3/4 from the east, the table broadside from the south-west, and from outside the
                //     west facade, through one of the annex's open windows
                l.Add(PL(HL(-46f, 106.4f, -8f), HL(-37.75f, 104.4f, 3f)));
                l.Add(PL(HL(-60f, 106.6f, 3f), HL(-37.75f, 104.6f, 3f)));
            }
            // v0.9.35: the stair well and the street cores вЂ” the two things the user's screenshots were
            // about: (a) the first flight seen from the arrival landing, looking up along it (their
            // frame 1), (b) the open well from the third storey, straight down the shaft (their frame
            // 2), (c) a block on legs: the core's three flights from the deck, (d) the first-floor
            // opening of that core, (e) the same house's corridor along the new opening.
            {
                Bldg h = null;
                for (int i = 0; i < bldgs.Count; i++) if (bldgs[i].Tall && bldgs[i].bands) { h = bldgs[i]; break; }
                if (h == null) for (int i = 0; i < bldgs.Count; i++) if (bldgs[i].Tall) { h = bldgs[i]; break; }
                if (h != null)
                {
                    var rot = Quaternion.Euler(0f, h.yaw, 0f);
                    float fh = h.fh, xs = h.stairX0;
                    float zLow = h.stairZ0 + h.stairLand, zHigh = zLow + h.stairRun;
                    float xm = xs + h.stairFw * 0.5f;
                    // (a) from the stair-hall doorway, across the well at the first flight вЂ” the whole
                    // stairwell's side profile in one frame (the "are they plates?" shot)
                    l.Add(P(h.p + rot * new Vector3(h.stairX0 + (h.stairX1 - h.stairX0) * 0.85f,
                                                    h.baseY + fh + 1.65f, h.stairZ0 + 0.5f),
                            h.p + rot * new Vector3(h.stairX0 + h.stairFw * 0.5f, h.baseY + fh + 1.20f,
                                                    h.stairZ0 + h.stairLand + h.stairRun * 0.6f)));
                    // (b) the well from the top floor, straight down the shaft between the railings
                    float xc = xs + h.stairFw + h.stairWell * 0.5f;
                    l.Add(P(h.p + rot * new Vector3(xc, h.baseY + 4f * fh + 1.55f, zLow - 0.45f),
                            h.p + rot * new Vector3(xc, h.baseY + 0.6f, zLow + 2.2f)));
                    // (f) standing in the well on the ground floor, straight up the slot: the open
                    // shaft the user asked for, from the bottom of the house to the roof
                    l.Add(P(h.p + rot * new Vector3(xc, h.baseY + 1.65f, zLow + 1.2f),
                            h.p + rot * new Vector3(xc, h.baseY + 6f * fh, zLow + 1.2f)));
                }
                Bldg g = null;
                for (int i = 0; i < bldgs.Count; i++) if (bldgs[i].legs) { g = bldgs[i]; break; }
                if (g != null)
                {
                    var rot = Quaternion.Euler(0f, g.yaw, 0f);
                    float yTop = g.baseY + 3f * g.fh;
                    float xc = (g.coreX0 + g.coreX1) * 0.5f;
                    // (c) the core's own half-landing on the top storey, looking up the last flight:
                    // the frame the user photographed, where the flight used to end inside the slab
                    l.Add(P(g.p + rot * new Vector3(xc, g.baseY + 3f * g.fh - 3.0f + 1.65f, -0.6f),
                            g.p + rot * new Vector3(xc, yTop + 0.7f, 2.2f)));
                    // (d) the first-floor opening: standing in the corridor, looking down into it
                    l.Add(P(g.p + rot * new Vector3(xc + 3.4f, yTop + 1.65f, g.coreHoleZ0 + 1.2f),
                            g.p + rot * new Vector3(xc, yTop - 1.6f, g.coreHoleZ0 + 1.2f)));
                    // (e) the corridor along the opening, its railings and the stair hall behind
                    l.Add(P(g.p + rot * new Vector3(xc + 4.2f, yTop + 1.65f, g.coreHoleZ0 + 0.4f),
                            g.p + rot * new Vector3(xc - 1.2f, yTop - 0.2f, g.coreHoleZ0 + 1.6f)));
                }
                // v0.9.37: **the plate itself** вЂ” (A) standing under flight 2 and looking straight up at
                // its soffit (it must be closed: no see-through), (B) from the next storey's
                // half-landing, looking down a flight from above вЂ” the user's reference photo's angle.
                if (h != null)
                {
                    var rot = Quaternion.Euler(0f, h.yaw, 0f);
                    float zLow = h.stairZ0 + h.stairLand;
                    float zHigh = zLow + h.stairRun;
                    float fw = h.stairFw, well = h.stairWell;
                    float xm = h.stairX0 + fw * 0.5f;
                    l.Add(P(h.p + rot * new Vector3(h.stairX0 + fw + well + fw * 0.5f, h.baseY + 1.55f,
                                                    zLow + h.stairRun * 0.55f),
                            h.p + rot * new Vector3(h.stairX0 + fw + well + fw * 0.5f, h.baseY + 4.2f,
                                                    zLow + h.stairRun * 0.55f)));
                    l.Add(P(h.p + rot * new Vector3(xm, h.baseY + 1.5f * h.fh + 1.55f, zHigh + 0.6f),
                            h.p + rot * new Vector3(xm, h.baseY + 0.5f * h.fh, zLow + 0.9f)));
                }
                // v0.9.45: a top view of the south-east corner / crest seam вЂ” the spot the user circled
                // in their own aerial screenshot (a small dark rectangle on the dam's granite)
                l.Add(new PreviewShot
                {
                    Pos = root.TransformPoint(new Vector3(350f, 40f, 12f)),
                    Look = root.TransformPoint(new Vector3(350f, 0f, 11.99f)),
                    Ortho = 42f
                });
                // v0.9.50: the video's own angle вЂ” standing on the crest's granite just north of the seam,
                // looking south-west along it: the thin concrete plate that used to lie here must be gone,
                // the stone must meet the pad almost flush (10 mm) with no overhanging edge.
                l.Add(PL(new Vector3(352f, 1.35f, -3.2f), new Vector3(340f, 0.30f, 2.5f)));
                l.Add(PL(new Vector3(345f, 5.5f, -2.5f), new Vector3(340f, 0.30f, 3.5f)));
                // v0.9.51: the user's own framing вЂ” standing on the corner pad two metres off the +5 m
                // terrace's south wall and looking east along it: the wall must run straight to its end
                // and meet the plate perpendicularly, with nothing of ours sticking out past its end.
                l.Add(PL(new Vector3(334f, 1.65f, 9.4f), new Vector3(383f, 0.85f, 9.0f)));
                // v0.9.51 diagnostics: the corner from the sea and from the crest side, elevated
                l.Add(PL(new Vector3(392f, 7.0f, 34f), new Vector3(370f, 1.0f, 6f)));
                l.Add(PL(new Vector3(330f, 10.0f, -10f), new Vector3(374f, 1.0f, 9f)));
                // v0.9.42: the user's circle вЂ” the floor plate's end face at the foot of a flight, seen
                // from the arrival landing (the face that was culled/invisible)
                if (h != null)
                {
                    var rot = Quaternion.Euler(0f, h.yaw, 0f);
                    float zLow = h.stairZ0 + h.stairLand;
                    l.Add(P(h.p + rot * new Vector3(h.stairX0 + h.stairFw * 0.30f,
                                                    h.baseY + h.fh + 1.62f, zLow - 1.70f),
                            h.p + rot * new Vector3(h.stairX0 + h.stairFw * 0.95f,
                                                    h.baseY + h.fh - 0.10f, zLow + 0.30f)));
                    // and the same end face head-on: from inside the well, at the slab's mid-thickness
                    l.Add(P(h.p + rot * new Vector3(h.stairX0 + h.stairFw * 0.5f,
                                                    h.baseY + h.fh - 0.13f, zLow + 1.10f),
                            h.p + rot * new Vector3(h.stairX0 + h.stairFw * 0.5f,
                                                    h.baseY + h.fh - 0.28f, zLow - 0.80f)));
                }
            }
            // v0.9.70: reference shots of the **main BABBDI city** at the player's spawn вЂ” the exact
            // concrete, grain and ground the district has to match (plan points 1-2). World space.
            try
            {
                var fpc = FirstPersonController.instance;
                if (fpc != null)
                {
                    var cc = fpc.GetComponent<CharacterController>();
                    Vector3 feet = cc != null
                        ? fpc.transform.TransformPoint(cc.center) - Vector3.up * (cc.height * 0.5f)
                        : fpc.transform.position;
                    var cam = fpc.GetComponentInChildren<Camera>();
                    Vector3 fwd = cam != null ? cam.transform.forward : Vector3.forward;
                    fwd.y = 0f; fwd.Normalize();
                    if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                    Vector3 rgt = Vector3.Cross(Vector3.up, fwd).normalized;
                    Vector3 eye = feet + Vector3.up * 1.7f;
                    Func<Vector3, Vector3, PreviewShot> W = (a, b) => new PreviewShot { Pos = a, Look = b };
                    // the same two framings under the debug sun, so the albedo/grain can be compared
                    // with the district's own lit shots (which all use Lamp = true)
                    Func<Vector3, Vector3, PreviewShot> WL = (a, b) => new PreviewShot
                    { Pos = a, Look = b, Lamp = true };
                    l.Add(W(eye, eye + fwd * 25f));
                    l.Add(W(eye - rgt * 12f, eye - rgt * 12f + fwd * 18f - rgt * 8f));
                    l.Add(W(eye + rgt * 12f, eye + rgt * 12f + fwd * 18f + rgt * 8f));
                    l.Add(W(eye, feet + fwd * 4f));       // the ground at the spawn
                    l.Add(W(feet + Vector3.up * 45f - fwd * 60f, feet + Vector3.up * 5f + fwd * 30f));
                    l.Add(W(feet + Vector3.up * 12f + rgt * 10f - fwd * 6f, feet + Vector3.up * 4f));
                    l.Add(WL(eye, eye + fwd * 25f));
                    l.Add(WL(eye, feet + fwd * 4f));
                }
            }
            catch { }
            // v0.9.73: the bas-relief on the outward wall of the east-flank block (plan points 5/8)
            try
            {
                var rb = reliefBlock;
                if (rb != null)
                {
                    var rr = Quaternion.Euler(0f, rb.yaw, 0f);
                    float yy = rb.baseY + (rb.Top - rb.baseY) * 0.55f;
                    Vector3 wall = rb.p + rr * new Vector3(0f, yy, rb.d * 0.5f);
                    l.Add(PL(rb.p + rr * new Vector3(0f, yy, rb.d * 0.5f + 115f), wall));
                    l.Add(PL(rb.p + rr * new Vector3(0f, yy, rb.d * 0.5f + 48f), wall));
                    l.Add(PL(rb.p + rr * new Vector3(0f, rb.baseY + 16f, rb.d * 0.5f + 38f),
                             rb.p + rr * new Vector3(0f, rb.baseY + 22f, rb.d * 0.5f)));
                }
            }
            catch { }
            // v0.9.82: the Mi-26's engine deck up close вЂ” the original model's cowling, its two
            // intakes and the gearbox fairing adapted onto the new hull. Appended last so the
            // existing shot numbers stay put (the nose points along +x of the roof's own frame).
            if (heliBlock != null)
            {
                var hr = Quaternion.Euler(0f, HeliYaw, 0f);
                Vector3 hp = HeliSpot;
                l.Add(PL(hp + hr * new Vector3(31f, 9.5f, 12f), hp + hr * new Vector3(17.5f, 6.2f, 0f)));
                l.Add(PL(hp + hr * new Vector3(19.5f, 8.5f, -11.5f), hp + hr * new Vector3(17.5f, 6.2f, 0f)));
                l.Add(PL(hp + hr * new Vector3(17.5f, 24f, 8f), hp + hr * new Vector3(17.5f, 6.0f, 0f)));
                // v0.9.83: an ORTHOGRAPHIC side elevation at the reference's own scale вЂ”
                // the only honest way to compare the silhouette with the 3-view drawing
                // (a close perspective shot magnifies the near end by ~30 %).
                var op = PL(hp + hr * new Vector3(0f, 6f, 26f), hp + hr * new Vector3(0f, 6f, 0f));
                op.Ortho = 34f;
                l.Add(op);
                // v0.9.93: straight at the cargo hatch from astern (the nose points +x local)
                l.Add(PL(hp + hr * new Vector3(0f, 10f, -62f), hp + hr * new Vector3(0f, 6.5f, -12f)));
                l.Add(PL(hp + hr * new Vector3(18f, 12f, -50f), hp + hr * new Vector3(0f, 6.0f, -10f)));
                // v0.9.101: from INSIDE the cargo bay, looking aft — the proof that the lining
                // is there and the interior is not see-through
                l.Add(PL(hp + hr * new Vector3(-8f, 6.0f, 0f), hp + hr * new Vector3(-22f, 5.4f, 0f)));
                l.Add(PL(hp + hr * new Vector3(-8f, 6.0f, 0f), hp + hr * new Vector3(6f, 5.4f, 0f)));
                if (heliInA != Vector3.zero)
                {
                    l.Add(new PreviewShot { Pos = heliInA, Look = heliInB, Lamp = true, RampState = 1 });
                    l.Add(new PreviewShot { Pos = heliInB, Look = heliInA, Lamp = true });
                    // the user's angle: inside the bay, close to the closed hatch
                    l.Add(new PreviewShot { Pos = HM(new Vector3(0f, 6.1f, -2.6f)), Look = HM(new Vector3(0f, 4.2f, 13f)), Lamp = true, RampState = 1 });
                    l.Add(new PreviewShot { Pos = heliLowA, Look = heliLowB, Lamp = true, RampState = 1 });
                    l.Add(new PreviewShot { Pos = heliSideA, Look = heliSideB, Lamp = true });
                    l.Add(new PreviewShot { Pos = heliCabA, Look = heliCabB, Lamp = true });
                    l.Add(new PreviewShot { Pos = HM(new Vector3(-1.9f, 9.0f, -35.0f)), Look = HM(new Vector3(0f, 9.2f, -38.2f)), Lamp = true });
                    l.Add(new PreviewShot { Pos = HM(new Vector3(0f, 10.6f, -33.0f)), Look = HM(new Vector3(0f, 8.2f, -37.2f)), Lamp = true });
                    l.Add(new PreviewShot { Pos = HM(new Vector3(1.6f, 9.6f, -35.4f)), Look = HM(new Vector3(4.6f, 9.2f, -36.6f)), Lamp = true });
                    // v0.9.112: the same camera with the ramp raised, half way and lowered
                    l.Add(new PreviewShot { Pos = heliRampA, Look = heliRampB, Lamp = true, RampState = 1 });
                    l.Add(new PreviewShot { Pos = heliEdgeA, Look = heliEdgeB, Lamp = true, RampState = 1 });
                    l.Add(new PreviewShot { Pos = HM(new Vector3(0f, 8.5f, 10f)), Look = HM(new Vector3(0f, 8.0f, -18f)), Lamp = true, RampState = 1 });
                    l.Add(new PreviewShot { Pos = HM(new Vector3(0f, 8.5f, -6f)), Look = HM(new Vector3(-5.5f, 7.5f, -6f)), Lamp = true, RampState = 1 });
                    l.Add(new PreviewShot { Pos = HM(new Vector3(0f, 10.0f, 2f)), Look = HM(new Vector3(0f, 6.0f, 12f)), Lamp = true, RampState = 1 });
                    l.Add(new PreviewShot { Pos = heliRampA, Look = heliRampB, Lamp = true, RampState = 2 });
                    l.Add(new PreviewShot { Pos = heliRampA, Look = heliRampB, Lamp = true, RampState = 3 });
                }
            }
            // v0.10.8: the six patterned air-hockey tables on the annex floor below the playable one.
            // Appended at the end so every existing district_N shot keeps its number.
            {
                var fh2 = new Vector3(230f, PlinthH, 84f);
                var fr2 = Quaternion.Euler(0f, 4f, 0f);
                Func<float, float, float, Vector3> HL2 = (x, y, z) => fh2 + fr2 * new Vector3(x, y, z);
                // the whole showroom from the south-east corner, under the ceiling, both rows visible
                l.Add(PL(HL2(-11.4f, 101.4f, -13.6f), HL2(-30.5f, 96.9f, 2.0f)));
                // plan of the 3 x 2 grid, under the ceiling so the floor below is visible
                l.Add(new PreviewShot
                {
                    Pos = root.TransformPoint(HL2(-30.5f, 101.8f, 1f)),
                    Look = root.TransformPoint(HL2(-30.5f, 96f, 1f)),
                    Ortho = 17f,
                    Lamp = true
                });
                // variant 1 from the south-west: patterned body sides + magenta canopy
                l.Add(PL(HL2(-49.2f, 97.7f, -11.0f), HL2(-44f, 97.0f, -4.5f)));
                // variant 6 from the north-east: cyan canopy and LED ring
                l.Add(PL(HL2(-11.6f, 97.8f, 13.6f), HL2(-17f, 97.0f, 6.5f)));
                // the arch/lamp of variant 1 up close
                l.Add(PL(HL2(-44f, 99.0f, -10.0f), HL2(-44f, 98.2f, -4.5f)));
            }
            // v0.10.10: the four patterned tables one floor further down (89.75), north strip.
            // Two of them carry the neural-slop cartoon animal designs (08 cats, 10 frog).
            {
                var fh2 = new Vector3(230f, PlinthH, 84f);
                var fr2 = Quaternion.Euler(0f, 4f, 0f);
                Func<float, float, float, Vector3> HL2 = (x, y, z) => fh2 + fr2 * new Vector3(x, y, z);
                // the row from the north-west corner, under the 96.15 ceiling
                l.Add(PL(HL2(-49.0f, 95.5f, -13.2f), HL2(-30.5f, 90.6f, -10.8f)));
                // plan of the four-table row
                l.Add(new PreviewShot
                {
                    Pos = root.TransformPoint(HL2(-30.5f, 95.8f, -11f)),
                    Look = root.TransformPoint(HL2(-30.5f, 89.75f, -11f)),
                    Ortho = 18f,
                    Lamp = true
                });
                // table 08 (cartoon cats) from the north-west, offset so the arch rod does not block
                l.Add(PL(HL2(-38.5f, 91.9f, -13.2f), HL2(-35f, 90.8f, -11f)));
                // table 10 (cartoon frog) from the north-west
                l.Add(PL(HL2(-20.5f, 91.9f, -13.2f), HL2(-17f, 90.8f, -11f)));
                // low side angle on table 08: the animal/lettering skin flush with the body
                l.Add(PL(HL2(-38.0f, 90.95f, -13.3f), HL2(-35f, 90.45f, -11f)));
                // flat side elevations of the two animal tables (the arch rod is a thin vertical line)
                l.Add(new PreviewShot
                {
                    Pos = root.TransformPoint(HL2(-35f, 90.65f, -13.8f)),
                    Look = root.TransformPoint(HL2(-35f, 90.65f, -11f)),
                    Ortho = 3.2f,
                    Lamp = true
                });
                l.Add(new PreviewShot
                {
                    Pos = root.TransformPoint(HL2(-17f, 90.65f, -13.8f)),
                    Look = root.TransformPoint(HL2(-17f, 90.65f, -11f)),
                    Ortho = 3.2f,
                    Lamp = true
                });
                // v0.10.12: the raised, theme-coloured bumper on variant 1 (5 cm above the table)
                l.Add(PL(HL2(-48.5f, 97.9f, 10.8f), HL2(-44f, 97.2f, 6.5f)));
            }
            // v0.10.17: the five arcade cabinets along the top hall's back wall
            {
                var fh3 = new Vector3(230f, PlinthH, 84f);
                var fr3 = Quaternion.Euler(0f, 4f, 0f);
                Func<float, float, float, Vector3> HC =
                    (x, y, z) => fh3 + fr3 * new Vector3(x, y, z);
                l.Add(PL(HC(-39.5f, 105.90f, -4.6f), HC(-30.5f, 104.10f, -12.50f)));
                l.Add(PL(HC(-30.5f, 105.60f, -7.0f), HC(-30.5f, 104.00f, -12.50f)));
                // v0.10.36: the six new cabinets on the left half of the row
                l.Add(PL(HC(-47.5f, 105.90f, -4.2f), HC(-39.5f, 104.10f, -12.50f)));
                l.Add(PL(HC(-42.0f, 105.70f, -6.0f), HC(-39.5f, 104.00f, -12.50f)));
                // the whole eleven-cabinet row from the middle of the hall
                l.Add(PL(HC(-36.5f, 106.60f, 3.0f), HC(-36.5f, 104.10f, -12.50f)));
                // v0.10.42: the three duel cabinets at the opposite wall (front faces -z)
                l.Add(PL(HC(-36.5f, 105.90f, 14.2f), HC(-36.5f, 104.20f, 18.50f)));
                l.Add(PL(HC(-40.5f, 105.70f, 14.0f), HC(-40.5f, 104.10f, 18.50f)));
                l.Add(PL(HC(-36.5f, 105.80f, 8.0f), HC(-36.5f, 104.30f, 18.50f)));
            }
            return l;
        }

        /// <summary>
        /// v0.9.10 grain calibration (map-shot mode only): the district's surface and the dam's own
        /// concrete shot with the same ruler, so the grain can be *measured* instead of guessed.
        ///
        /// `grain_dam` / `grain_ours` look straight down from 2.4 m (60В° FOV, 1024 px) at the two
        /// sides of the seam вЂ” 2.7 mm per pixel on both, so the aggregate pits can be counted and
        /// sized in cm. `grain_seam` is the user's own framing at eye level: the joint between the
        /// old crest and the platform.
        /// </summary>
        public static List<PreviewShot> GrainShots()
        {
            var l = new List<PreviewShot>();
            if (root == null) return l;
            Func<Vector3, Vector3, string, PreviewShot> P = (a, b, n) => new PreviewShot
            {
                Pos = root.TransformPoint(a),
                Look = root.TransformPoint(b),
                Name = n
            };
            // the old dam crest is inland (local z < 0), the platform is seaward (z > 0)
            // where is what: 60 x 60 m straight down, 1024 px = 5.9 cm/px
            l.Add(new PreviewShot
            {
                Pos = root.TransformPoint(new Vector3(205f, 60f, 0f)),
                Look = root.TransformPoint(new Vector3(205f, 0f, -0.01f)),
                Name = "grain_wide",
                Ortho = 30f
            });
            l.Add(P(new Vector3(205f, 2.4f, -8f), new Vector3(205f, -1f, -8f), "grain_dam"));
            l.Add(P(new Vector3(205f, 2.4f, -12f), new Vector3(205f, -1f, -12f), "grain_dam2"));
            l.Add(P(new Vector3(205f, 2.4f, 3f), new Vector3(205f, -1f, 3f), "grain_ours"));
            l.Add(P(new Vector3(212f, 1.3f, 4f), new Vector3(194f, 0.2f, -1.5f), "grain_seam"));
            return l;
        }

        /// <summary>Plan camera: straight down, wide enough for the platform and the western tongue.</summary>
        public static void TopView(out Vector3 pos, out Quaternion rot, out float ortho)
        {
            pos = root != null ? root.TransformPoint(new Vector3(165f, 0f, 150f)) + Vector3.up * 300f
                               : new Vector3(0f, 400f, 0f);
            // aligned with the deck, so the plan reads like the reference drawing (seaward = up)
            rot = (root != null ? root.rotation : Quaternion.identity) * Quaternion.Euler(90f, 0f, 0f);
            ortho = 245f;
        }

        public static void SetShadows(bool on)
        {
            if (root == null) return;
            var mode = on ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = mode;
        }

        /// <summary>
        /// Physics probes for the one thing a still cannot show: whether the player would actually
        /// stand on these floors. Casts straight down at a few places and logs what it hit.
        /// </summary>
        /// <summary>
        /// Layout evidence (map-shot mode): the exact footprints and the bridges, so the plan can be
        /// drawn offline in the colours of the reference drawing (beige deck, green houses, red bridges,
        /// purple House) instead of guessing from a top-down render where every roof is the same grey.
        /// </summary>
        private static void DumpPlan()
        {
            try
            {
                string asm = typeof(SovietDistrict).Assembly.Location;
                string dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(asm) ?? "", "BabbdiCoop", "map");
                System.IO.Directory.CreateDirectory(dir);
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"# platform;{Len:F1};{Wid:F1};{Axw:F1};{Az0:F1};{ShiftX:F1}");
                for (int i = 0; i < tiers.Length; i++)     // v0.9.9: the terraced floors
                {
                    var t = tiers[i];
                    sb.AppendLine($"t;{t.x0:F1};{t.x1:F1};{t.z0:F1};{t.z1:F1};{t.h:F1};{t.name}");
                }
                for (int i = 0; i < ramps.Length; i++)     // the inclined roads (yellow)
                {
                    var l = ramps[i];
                    sb.AppendLine($"m;{l.x0:F1};{l.z0:F1};{l.x1:F1};{l.z1:F1};{l.w:F1};{l.y0:F1};{l.y1:F1}");
                }
                for (int i = 0; i < stairs.Length; i++)    // the street stairs (green)
                {
                    var l = stairs[i];
                    sb.AppendLine($"s;{l.x0:F1};{l.z0:F1};{l.x1:F1};{l.z1:F1};{l.w:F1};{l.y0:F1};{l.y1:F1}");
                }
                for (int i = 0; i < bldgs.Count; i++)
                {
                    var b = bldgs[i];
                    sb.AppendLine($"b;{b.p.x:F1};{b.p.z:F1};{b.w:F1};{b.d:F1};{b.yaw:F1};{b.floors}");
                }
                for (int i = 0; i < bridges.Count; i++)
                {
                    var s = bridges[i];
                    sb.AppendLine($"r;{s.a.x:F1};{s.a.z:F1};{s.b.x:F1};{s.b.z:F1};{s.y:F1}");
                }
                sb.AppendLine("p;230.0;84.0;107.0;49.0;4.0");    // House of Soviets
                sb.AppendLine($"q;232.5;85.0;135.0;80.0;{PlinthH:F1}");    // its esplanade (the +10 m plinth)
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "plan_dump.csv"), sb.ToString());
                log.LogInfo($"[DISTRICT] plan dump: {bldgs.Count} blocks, {bridges.Count} bridges, " +
                            $"{tiers.Length} terraces, {ramps.Length} ramps, {stairs.Length} stairs -> plan_dump.csv");
            }
            catch (Exception e)
            {
                log.LogWarning($"[DISTRICT] plan dump failed: {e.Message}");
            }
        }

        /// <summary>Building-local sample point above the arrival landing of the first storey.</summary>
        private static Vector3 StairLanding(Bldg b)
        {
            // v0.9.16: every stair runs along z (StairsZ), so the arrival landing is the strip next to
            // the corridor doorway and the half-landing is the one against the outer wall
            return new Vector3((b.stairX0 + b.stairX1) * 0.5f, b.baseY + b.fh + 1.2f,
                               b.stairZ0 + b.stairLand * 0.5f);
        }

        /// <summary>Building-local sample point above the half-landing.</summary>
        private static Vector3 StairMid(Bldg b)
        {
            return new Vector3((b.stairX0 + b.stairX1) * 0.5f, b.baseY + b.fh * 0.5f + 1.2f,
                               b.stairZ1 - b.stairLand * 0.5f);
        }

        /// <summary>
        /// v0.9.16 вЂ” the user's "the stairs run into a blind ceiling". A ray straight **up** from just
        /// above each flight must leave the shaft: the same hole is cut in every floor slab, so a hit
        /// roughly one storey above means a solid plate over the flights. The arrival landing and the
        /// half-landing are checked separately (`stair landing`/`half landing` drops) вЂ” those *are*
        /// meant to be solid, they are the floor you walk on.
        /// </summary>
        private static void StairProbes(Bldg b, string tag)
        {
            var rot = Quaternion.Euler(0f, b.yaw, 0f);
            float fh = b.fh;
            float w = b.stairX1 - b.stairX0;
            float zMid = b.stairZ0 + b.stairLand + b.stairRun * 0.5f;
            float y = b.baseY + fh + 0.45f;                 // in the open shaft above the 1st floor
            System.Func<Vector3, string> up = local =>
            {
                Vector3 o = root.TransformPoint(b.p + rot * local);
                if (!Physics.Raycast(o, Vector3.up, out var h, fh * 2f - 0.5f, ~0, QueryTriggerInteraction.Ignore))
                    return $"OPEN over 2 storeys ({fh * 2f - 0.5f:0.0} m checked)";
                return string.Format("{0:0.00} m above the shaft floor -> {1}", h.point.y - b.baseY - fh, h.collider.name);
            };
            log.LogInfo($"[PROBE] {tag} stair flight 1 ({b.stairRun:0.00} m long) above: " +
                        up(new Vector3(b.stairX0 + w * 0.25f, y, zMid)));
            log.LogInfo($"[PROBE] {tag} stair flight 2 above: " +
                        up(new Vector3(b.stairX0 + w * 0.75f, y, zMid)));
        }

        /// <summary>
        /// v0.9.16 вЂ” headroom over every flight of every block, measured. A vertical ray from 1,84 m
        /// above the middle tread of each flight must be clear for another 0,2 m (a 2 m person); the
        /// shaft is cut through every slab, so nothing may be hit at all. This is the numeric form of
        /// the user's "the stairs run into a blind ceiling".
        /// </summary>
        private static void Headroom()
        {
            int blocks = 0, rays = 0, blocked = 0;
            float least = 99f;
            string leastWhat = "";
            bool dumped = false;
            foreach (var b in bldgs)
            {
                if (!b.interior) continue;
                var rot = Quaternion.Euler(0f, b.yaw, 0f);
                float zLow = b.stairZ0 + b.stairLand;
                float xm1 = b.stairX0 + (b.stairX1 - b.stairX0) * 0.25f;
                float xm2 = b.stairX0 + (b.stairX1 - b.stairX0) * 0.75f;
                int worst = -1;
                float blockMin = 99f;
                string blockWhat = "";
                // v0.9.35: a block on legs has no shell below its first floor вЂ” its own stairs start
                // there, so sample from that storey (the street core is checked separately below)
                int f0 = b.legs ? 3 : 0;
                for (int f = f0; f + 1 < b.floors && f < f0 + 3; f++)
                    for (int s = 0; s < 2; s++)
                        for (int i = 1; i <= 8; i++)
                        {
                            float t = i / 8f;
                            // flight 1 rises with +z (yf .. yf+fh/2), flight 2 rises back towards -z
                            // (yf+fh/2 .. yf+fh) вЂ” v0.9.36: the old formula used flight 1's profile for
                            // both, so "flight 2" was sampled *inside* the stairs themselves
                            float y = b.baseY + f * b.fh +
                                      (s == 0 ? b.fh * 0.5f * t : b.fh * (1f - 0.5f * t)) + 1.84f;
                            Vector3 o = root.TransformPoint(b.p + rot * new Vector3(
                                s == 0 ? xm1 : xm2, y, zLow + b.stairRun * t));
                            rays++;
                            // v0.9.36: measure the **real** clearance to the first thing above, not a
                            // 0,2 m sniff вЂ” the user's "the flight butts into the slab" needs a number
                            float clear = 5.6f;
                            string what = "nothing within 5,6 m";
                            if (Physics.Raycast(o, Vector3.up, out var hh, 5.6f, ~0, QueryTriggerInteraction.Ignore))
                            {
                                clear = 1.84f + hh.distance;
                                what = hh.collider.name;
                                if (clear < 2.05f) { blocked++; if (worst < 0) worst = f; }
                            }
                            if (clear < blockMin)
                            {
                                blockMin = clear;
                                blockWhat = $"storey {f}, flight {(s == 0 ? 1 : 2)}, tread {i}/8, " +
                                            $"{what} at {clear:0.00} m above it";
                            }
                            if (clear < least) { least = clear; leastWhat = $"{b.w:0.0}x{b.d:0.0} m block at " +
                                                $"{b.p.x:0.0},{b.p.z:0.0}: {blockWhat}"; }
                        }
                blocks++;
                if (worst >= 0)
                    log.LogWarning($"[STAIR] headroom below 2 m over a flight: block {b.w:0.0}x{b.d:0.0} m " +
                                   $"at {b.p.x:0.0},{b.p.z:0.0}, first bad storey {worst}");
                if (blockMin < 4.0f)
                    log.LogWarning($"[STAIR] low ceiling over a flight: {b.w:0.0}x{b.d:0.0} m block at " +
                                   $"{b.p.x:0.0},{b.p.z:0.0}: {blockMin:0.00} m ({blockWhat})");
                if (blockMin < 1.9f && !dumped)
                {
                    dumped = true;
                    log.LogInfo($"[STAIR] geometry above flight 2 of {b.w:0.0}x{b.d:0.0} m at " +
                                $"{b.p.x:0.0},{b.p.z:0.0} (corr {b.CorrHalf:0.00}, zLow " +
                                $"{b.stairZ0 + b.stairLand:0.00}, zHigh {b.stairZ0 + b.stairLand + b.stairRun:0.00}, " +
                                $"zi {b.stairZ1:0.00}, fh {b.fh:0.0}):");
                    for (int s = 0; s < 2; s++)
                        for (int i = 1; i <= 4; i++)
                        {
                            float t = i / 4f;
                            float y = b.baseY + 2 * b.fh +
                                      (s == 0 ? b.fh * 0.5f * t : b.fh * (1f - 0.5f * t)) + 1.84f;
                            Vector3 o = root.TransformPoint(b.p + rot * new Vector3(
                                s == 0 ? xm1 : xm2, y, zLow + b.stairRun * t));
                            string r = "nothing within 5,6 m";
                            if (Physics.Raycast(o, Vector3.up, out var hh, 5.6f, ~0, QueryTriggerInteraction.Ignore))
                            {
                                Vector3 lp = Quaternion.Inverse(rot) * (root.InverseTransformPoint(hh.point) - b.p);
                                r = $"{(1.84f + hh.distance):0.00} m -> {hh.collider.name} at local " +
                                    $"({lp.x:0.00}, {lp.y:0.00}, {lp.z:0.00}), origin y {y:0.00}";
                            }
                            log.LogInfo($"[STAIR]   flight {(s == 0 ? 1 : 2)} t={t:0.00} (z {zLow + b.stairRun * t:0.00}, " +
                                        $"y {y:0.00}): {r}");
                        }
                }
                if (blocks == 1)   // v0.9.36: one block in full, so the geometry above a flight is named
                    for (int s = 0; s < 2; s++)
                        for (int i = 1; i <= 4; i++)
                        {
                            float t = i / 4f;
                            float y = b.baseY + 1 * b.fh +
                                      (s == 0 ? b.fh * 0.5f * t : b.fh * (1f - 0.5f * t)) + 1.84f;
                            Vector3 o = root.TransformPoint(b.p + rot * new Vector3(
                                s == 0 ? xm1 : xm2, y, zLow + b.stairRun * t));
                            string r = "nothing within 5,6 m";
                            if (Physics.Raycast(o, Vector3.up, out var hh, 5.6f, ~0, QueryTriggerInteraction.Ignore))
                                r = $"{(1.84f + hh.distance):0.00} m above the tread -> {hh.collider.name} " +
                                    $"at y+{hh.point.y - (b.baseY + b.fh):0.00}";
                            log.LogInfo($"[STAIR] above flight {(s == 0 ? 1 : 2)}, storey 1, t={t:0.00}: {r}");
                        }
            }
            log.LogInfo($"[STAIR] headroom: {blocks} blocks, {rays} rays, {blocked} under 2,05 m; " +
                        $"least clearance {least:0.00} m ({leastWhat})");

            // v0.9.35: the street cores of the blocks on legs. A ray up from just under the block's
            // first floor, inside the core's stair lane, must leave the shaft: before this the last
            // flight ended **inside** the solid slab (`coreHoleZ0` is where the headroom drops under
            // 2 m, so the ray there has to pass through the new hole).
            foreach (var b in bldgs)
            {
                if (!b.legs) continue;
                var rot = Quaternion.Euler(0f, b.yaw, 0f);
                float yTop = b.baseY + 3f * b.fh;
                float x = (b.coreX0 + b.coreX1) * 0.5f;
                for (int i = 0; i < 2; i++)
                {
                    float z = b.coreHoleZ0 + 0.6f + i * 1.4f;
                    Vector3 o = root.TransformPoint(b.p + rot * new Vector3(x, yTop - 0.32f, z));
                    string r;
                    if (!Physics.Raycast(o, Vector3.up, out var hit, 1.2f, ~0, QueryTriggerInteraction.Ignore))
                        r = "OPEN through the first-floor opening (1,2 m)";
                    else
                        r = string.Format("{0:0.00} m over the stair -> {1}", hit.point.y - (yTop - 0.32f),
                                          hit.collider.name);
                    log.LogInfo($"[PROBE] legs core stair under the first floor at z {z:0.0}: {r}");
                }
            }
            StairSkin();
        }

        /// <summary>
        /// v0.9.40 вЂ” the user's "РїСЂРѕР·СЂР°С‡РЅР°СЏ С‚РµРєСЃС‚СѓСЂР°" seen from below a flight. A vertical ray from just
        /// above the floor **under a flight** must hit that flight's soffit; if it passes through, the
        /// plate really has a hole, and the log says exactly where (flight, position along the run, the
        /// height the soffit should have, and what the ray hit instead).
        /// </summary>
        private static void StairSkin()
        {
            int n = 0, through = 0, wrong = 0;
            foreach (var b in bldgs)
            {
                if (!b.interior) continue;
                var rot = Quaternion.Euler(0f, b.yaw, 0f);
                float y0 = b.baseY + (b.legs ? 3f * b.fh : 0f);   // the house's own first floor
                float zLow = b.stairZ0 + b.stairLand, run = b.stairRun;
                float fw = b.stairFw, well = b.stairWell;
                float slope = b.fh * 0.5f / run;
                for (int s = 0; s < 2; s++)
                {
                    float x = s == 0 ? b.stairX0 + fw * 0.5f : b.stairX0 + fw + well + fw * 0.5f;
                    for (int i = 1; i <= 10; i++)
                    {
                        float t = i / 11f;
                        float z = zLow + run * t;
                        // flight 1 rises with +z, flight 2 comes back towards -z
                        float ySof = s == 0
                            ? slope * run * t - StairPlate
                            : b.fh * 0.5f + slope * run * (1f - t) - StairPlate;
                        if (ySof < 0.30f) continue;                 // still inside the floor slab
                        Vector3 o = root.TransformPoint(b.p + rot * new Vector3(x, y0 + 0.28f, z));
                        n++;
                        if (!Physics.Raycast(o, Vector3.up, out var h, 6.5f, ~0, QueryTriggerInteraction.Ignore))
                        {
                            through++;
                            log.LogWarning($"[SKIN] {b.w:0.0}x{b.d:0.0} at {b.p.x:0.0},{b.p.z:0.0}: flight {s + 1}, " +
                                           $"{t:0.00} of the run вЂ” **nothing** over the soffit " +
                                           $"(expected at {ySof:0.00} m): a hole in the plate");
                            continue;
                        }
                        float hy = root.InverseTransformPoint(h.point).y - y0;
                        if (hy < ySof - 0.30f || hy > ySof + 0.55f)
                        {
                            wrong++;
                            log.LogWarning($"[SKIN] {b.w:0.0}x{b.d:0.0} at {b.p.x:0.0},{b.p.z:0.0}: flight {s + 1}, " +
                                           $"{t:0.00} of the run вЂ” hit {hy:0.00} m ({h.collider.name}), " +
                                           $"the soffit is at {ySof:0.00} m");
                        }
                    }
                }
            }
            log.LogInfo($"[SKIN] stair undersides: {n} vertical rays under the flights, {through} passed through, " +
                        $"{wrong} hit the wrong surface");
            DoorHeads();
        }

        /// <summary>
        /// v0.9.41 вЂ” the user's "РїСЂРѕР·СЂР°С‡РЅР°СЏ С‚РµРєСЃС‚СѓСЂР°" over every doorway: `Piece` used to draw a wall
        /// segment as two faces plus its two jambs, with **no head and no sill**, so a lintel was an open
        /// slot through the wall's thickness and looking up at a door showed the neighbouring room's
        /// ceiling as a thin dark strip. A ray from just under a door head must hit that head immediately.
        /// </summary>
        private static void DoorHeads()
        {
            var b = bldgs.Find(x => x.interior);
            if (b == null) return;
            var rot = Quaternion.Euler(0f, b.yaw, 0f);
            float fh = b.fh, corr = b.CorrHalf, segLen = b.stairX1 - b.stairX0;
            int ok = 0, open = 0;
            for (int f = 0; f < 2 && f + 1 < b.floors; f++)
                for (int s = -1; s <= 1; s += 2)
                    foreach (float x in new[] { s > 0 ? b.stairX0 + segLen - 0.95f : b.stairX0 + segLen * 0.5f })
                    {
                        Vector3 o = root.TransformPoint(b.p + rot * new Vector3(x, b.baseY + f * fh + 2.70f,
                                                                                 s * corr));
                        if (Physics.Raycast(o, Vector3.up, out var h, 1.2f, ~0, QueryTriggerInteraction.Ignore)
                            && h.distance < 0.35f) ok++;
                        else
                        {
                            open++;
                            log.LogWarning($"[DOOR] open head over a doorway at {x:0.00},{s * corr:0.00} " +
                                           $"storey {f}: " +
                                           (h.collider != null ? $"{h.distance:0.00} m -> {h.collider.name}" : "nothing"));
                        }
                    }
            log.LogInfo($"[DOOR] door heads: {ok + open} rays just under a lintel, {ok} hit the head, {open} open");
            SeamScan();
        }

        /// <summary>
        /// v0.9.44 вЂ” "the small dark structure on the dam": list the level's **own** meshes that sit near
        /// the crest line (district-local z в€’80вЂ¦+30), so the one the user means can be named and hidden.
        /// </summary>
        private static void SeamScan()
        {
            if (root == null) return;
            int shown = 0;
            foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (r == null || r.transform == null) continue;
                if (!r.gameObject.scene.IsValid()) continue;              // scene objects only
                if (r.transform.IsChildOf(root)) continue;                // our own district
                var b = r.bounds;
                Vector3 c = root.InverseTransformPoint(b.center);
                if (c.z < -40f || c.z > 40f || Mathf.Abs(c.x) > 470f) continue;
                // only things that actually reach the crest band (local y в€’6вЂ¦+6, z в€’25вЂ¦+25)
                var lo = root.InverseTransformPoint(b.min);
                var hi = root.InverseTransformPoint(b.max);
                if (hi.y < -6f || lo.y > 6f || hi.z < -25f || lo.z > 25f) continue;
                int verts = -1;
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) verts = mf.sharedMesh.vertexCount;
                log.LogInfo($"[SEAM] {r.name} [{r.GetType().Name}]: local ({c.x:0.0}, {c.y:0.0}, {c.z:0.0}), size " +
                            $"({b.size.x:0.0}, {b.size.y:0.0}, {b.size.z:0.0}), y {lo.y:0.0}..{hi.y:0.0}, " +
                            $"z {lo.z:0.0}..{hi.z:0.0}, verts {verts}");
                if (++shown > 80) break;
            }
            log.LogInfo($"[SEAM] listed {shown} level renderers near the crest line");
            int mine = 0;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var b = r.bounds;
                Vector3 c = root.InverseTransformPoint(b.center);
                if (c.z < -10f || c.z > 30f) continue;
                var lo = root.InverseTransformPoint(b.min);
                var hi = root.InverseTransformPoint(b.max);
                if (hi.y < -3f || lo.y > 4f) continue;
                log.LogInfo($"[SEAM-OURS] {r.name}: local ({c.x:0.0}, {c.y:0.0}, {c.z:0.0}), size " +
                            $"({b.size.x:0.0}, {b.size.y:0.0}, {b.size.z:0.0}), y {lo.y:0.0}..{hi.y:0.0}, " +
                            $"z {lo.z:0.0}..{hi.z:0.0}");
                if (++mine > 40) break;
            }
            log.LogInfo($"[SEAM-OURS] listed {mine} of our meshes in the crest band");
            CornerMeshScan();
        }

        /// <summary>
        /// v0.9.51 вЂ” the user's corner shot: something of ours (or of the level) protrudes past the end
        /// of the vertical wall at the south-east corner. List every mesh вЂ” ours and the level's вЂ” whose
        /// bounds reach that corner, so the offender is named with numbers instead of guessed.
        /// </summary>
        private static void CornerMeshScan()
        {
            if (root == null) return;
            // the district is rotated, so a world AABB does NOT give the local extents: take the
            // eight corners of the world bounds, transform each, and rebuild the box in local space
            System.Func<Bounds, Vector3, Vector3> localBox = (b, unused) => Vector3.zero;
            int n = 0;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var b = r.bounds;
                Vector3 lo = new Vector3(1e4f, 1e4f, 1e4f), hi = new Vector3(-1e4f, -1e4f, -1e4f);
                for (int i = 0; i < 8; i++)
                {
                    var w = new Vector3(
                        (i & 1) == 0 ? b.min.x : b.max.x,
                        (i & 2) == 0 ? b.min.y : b.max.y,
                        (i & 4) == 0 ? b.min.z : b.max.z);
                    var p = root.InverseTransformPoint(w);
                    lo = Vector3.Min(lo, p);
                    hi = Vector3.Max(hi, p);
                }
                if (hi.x < 320f || lo.x > 392f) continue;
                if (hi.z < -10f || lo.z > 30f) continue;
                if (hi.y < -3f || lo.y > 10f) continue;
                log.LogInfo($"[CORNER] ours {r.name}: x {lo.x:0.0}..{hi.x:0.0}, y {lo.y:0.0}..{hi.y:0.0}, " +
                            $"z {lo.z:0.0}..{hi.z:0.0}");
                if (++n > 40) break;
            }
            int lv = 0;
            foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (r == null || r.transform == null) continue;
                if (!r.gameObject.scene.IsValid()) continue;
                if (r.transform.IsChildOf(root)) continue;
                var b = r.bounds;
                Vector3 lo = new Vector3(1e4f, 1e4f, 1e4f), hi = new Vector3(-1e4f, -1e4f, -1e4f);
                for (int i = 0; i < 8; i++)
                {
                    var w = new Vector3(
                        (i & 1) == 0 ? b.min.x : b.max.x,
                        (i & 2) == 0 ? b.min.y : b.max.y,
                        (i & 4) == 0 ? b.min.z : b.max.z);
                    var p = root.InverseTransformPoint(w);
                    lo = Vector3.Min(lo, p);
                    hi = Vector3.Max(hi, p);
                }
                if (hi.x < 300f || lo.x > 410f) continue;
                if (hi.z < -20f || lo.z > 45f) continue;
                if (hi.y < -20f || lo.y > 80f) continue;
                log.LogInfo($"[CORNER] level {r.name}: x {lo.x:0.0}..{hi.x:0.0}, y {lo.y:0.0}..{hi.y:0.0}, " +
                            $"z {lo.z:0.0}..{hi.z:0.0}");
                if (++lv > 40) break;
            }
            log.LogInfo($"[CORNER] listed {n} of our meshes and {lv} level renderers at the south-east corner");
        }

        public static void ProbeWalkability()        {
            if (root == null) return;
            int ok = 0, bad = 0;
            System.Func<Vector3, float, float, string> down = (local, from, len) =>
            {
                Vector3 w = root.TransformPoint(local) + Vector3.up * from;
                if (!Physics.Raycast(w, Vector3.down, out var h, len, ~0, QueryTriggerInteraction.Ignore))
                    return "MISS";
                return string.Format("{0:0.00} m ({1})", h.point.y - root.position.y, h.collider.name);
            };
            System.Action<string, string> report = (what, hit) =>
            {
                bool good = hit != "MISS";
                if (good) ok++; else bad++;
                log.LogInfo($"[PROBE] {what}: {hit}");
            };
            // v0.9.57: the same ray, upward вЂ” the House's horizontal plates are checked from both sides
            System.Func<Vector3, float, float, string> up = (local, from, len) =>
            {
                Vector3 w = root.TransformPoint(local) + Vector3.up * from;
                if (!Physics.Raycast(w, Vector3.up, out var h, len, ~0, QueryTriggerInteraction.Ignore))
                    return "MISS";
                return string.Format("{0:0.00} m ({1})", h.point.y - root.position.y, h.collider.name);
            };
            // v0.9.21: the height of the first surface under a district-local point, for the corner's
            // numeric profile (a step shows up as a jump between neighbouring samples)
            System.Func<Vector3, string> Col = local =>
            {
                if (!Physics.Raycast(root.TransformPoint(local), Vector3.down, out var h, 60f, ~0,
                                     QueryTriggerInteraction.Ignore))
                    return "MISS";
                return string.Format("{0:0.00}", h.point.y - root.position.y);
            };
            // v0.9.12: a ray in an arbitrary direction, given in the district's local space
            System.Func<Vector3, Vector3, float, string> shoot = (localFrom, localDir, len) =>
            {
                if (!Physics.Raycast(root.TransformPoint(localFrom), root.rotation * localDir,
                                     out var h, len, ~0, QueryTriggerInteraction.Ignore))
                    return "MISS";
                return string.Format("{0:0.00} m ({1})", h.distance, h.collider.name);
            };

            if (bldgs.Count > 0)
            {
                var b = bldgs[0];
                var rot = Quaternion.Euler(0f, b.yaw, 0f);
                float fh = b.fh;
                report("street floor", down(b.p + rot * new Vector3(0f, 0f, b.d * 0.5f + 6f), 2f, 6f));
                for (int f = 0; f < Mathf.Min(3, b.floors); f++)
                    report($"house floor {f}", down(b.p + rot * new Vector3(0f, b.baseY + f * fh + 1.2f, 0f), 0.5f, 3f));
                report("stair landing 1", down(b.p + rot * StairLanding(b), 0.5f, 3f));
                report("half landing", down(b.p + rot * StairMid(b), 0.5f, 3f));
                var mc = b.go != null ? b.go.GetComponentInChildren<MeshCollider>() : null;
                var bm = mc != null ? mc.sharedMesh : null;
                log.LogInfo("[PROBE] bldg mesh: " + (bm == null ? "no collider" :
                    $"{bm.vertexCount} verts, {bm.subMeshCount} subs, tris0 {bm.GetTriangles(0).Length / 3}" +
                    (bm.subMeshCount > 1 ? $", tris1 {bm.GetTriangles(1).Length / 3}" : "") + $", convex {mc.convex}"));
                WindowProbes(b, "house 0");
                StairProbes(b, "house 0");
                // v0.9.13: the corridor is really 2x/3x/4x wide вЂ” measured across the hall, not read
                // from the config. The rays run 4.6 m above the floor: below that they would pass
                // through room doors (2.15 m) or out of a window (sill 1.1 m) and never hit a wall
                float cy = b.baseY + 2f * fh + 4.6f;
                report($"corridor {b.corrMul}x (={2f * b.CorrHalf:0.0} m clear) +z",
                    shoot(b.p + rot * new Vector3(0f, cy, 0f), rot * Vector3.forward, 12f));
                report($"corridor {b.corrMul}x -z",
                    shoot(b.p + rot * new Vector3(0f, cy, 0f), rot * Vector3.back, 12f));
            }
            // v0.9.12: every block now has the same tall storey, so the second sample is simply the next
            // building вЂ” it proves the layout is not a one-off (different width, different floor count)
            if (bldgs.Count > 1)
            {
                var s = bldgs[1];
                var sr = Quaternion.Euler(0f, s.yaw, 0f);
                float sh = s.fh;
                const string tag = "house 1";
                report(tag + " corridor floor", down(s.p + sr * new Vector3(0f, s.baseY + 2f * sh + 1.2f, 0f), 0.5f, 3f));
                report(tag + " stair landing", down(s.p + sr * StairLanding(s), 0.5f, 3f));
                report(tag + " half landing", down(s.p + sr * StairMid(s), 0.5f, 3f));
                report(tag + $" corridor {s.corrMul}x (={2f * s.CorrHalf:0.0} m clear)",
                    shoot(s.p + sr * new Vector3(0f, s.baseY + 2f * sh + 4.6f, 0f), sr * Vector3.forward, 12f));
                WindowProbes(s, tag);
                StairProbes(s, tag);
            }
            // v0.9.16: the block whose stair has the least room вЂ” the case that used to have no
            // flights at all (wide corridor + shallow footprint) or a plate over them
            var tight = bldgs.Find(x => x.interior && x.stairRun <= 2.05f);
            if (tight != null)
            {
                var tr = Quaternion.Euler(0f, tight.yaw, 0f);
                var tm = (tight.stairX0 + tight.stairX1) * 0.5f;
                log.LogInfo($"[PROBE] tightest stair: flight {tight.stairRun:0.00} m, landings " +
                            $"{tight.stairLand:0.00} m, corridor {2f * tight.CorrHalf:0.0} m clear");
                report("tight stair arrival landing",
                    down(tight.p + tr * new Vector3(tm, tight.baseY + tight.fh + 1.2f, tight.stairZ0 + tight.stairLand * 0.5f), 0.5f, 3f));
                report("tight stair half-landing",
                    down(tight.p + tr * new Vector3(tm, tight.baseY + tight.fh * 0.5f + 1.2f, tight.stairZ1 - tight.stairLand * 0.5f), 0.5f, 3f));
                StairProbes(tight, "tightest stair");
            }
            // v0.9.12: a balcony must physically exist in front of the wall вЂ” shoot from 6 m out at the
            // height of the first balcony: the parapet must stop the ray at ~4.85 m (1.15 m of
            // projection), the wall alone would be 6 m. Then drop onto the slab from above it.
            var bb = bldgs.Find(x => x.hasBalc);
            if (bb != null)
            {
                var rb = Quaternion.Euler(0f, bb.yaw, 0f);
                report("balcony parapet (1,15 m out)",
                    shoot(bb.p + rb * (bb.balcSample + new Vector3(0f, 0f, 6f)), rb * Vector3.back, 9f));
                report("balcony slab",
                    down(bb.p + rb * (bb.balcSample + new Vector3(0f, -0.5f, 0f)), 2f, 4f));
                var bmc = bb.go != null ? bb.go.GetComponentInChildren<MeshCollider>() : null;
                var bmm = bmc != null ? bmc.sharedMesh : null;
                log.LogInfo("[PROBE] balcony bldg mesh: " + (bmm == null ? "none" :
                    $"{bmm.vertexCount} verts, {bmm.subMeshCount} subs, {bmm.indexFormat}"));
                // v0.9.12: a horizontal scan across the +z facade, 3 m out, at parapet height and at
                // window height вЂ” 1,9 m = a balcony rail, 3,0 m = plain wall, more = the ray went
                // through an opening. Proves where the stacks sit in x, without trusting a render.
                float hdz = bb.d * 0.5f;
                float[] scanY = { bb.baseY + 2f * bb.fh + 0.5f, bb.baseY + 2f * bb.fh + 1.6f };
                foreach (float sy in scanY)
                {
                    var sb = new System.Text.StringBuilder();
                    for (float x = -bb.w * 0.5f; x <= bb.w * 0.5f + 0.01f; x += 1f)
                    {
                        string s = shoot(bb.p + rb * new Vector3(x, sy, hdz + 3f), rb * Vector3.back, 30f);
                        int sp = s.IndexOf(' ');
                        sb.Append(s == "MISS" ? "  .  " : s.Substring(0, sp).PadLeft(5) + " ");
                    }
                    log.LogInfo($"[PROBE] facade scan y={sy:0.0} (x {-bb.w * 0.5f:0.0}..{bb.w * 0.5f:0.0}, " +
                                $"hd={hdz:0.0}): {sb}");
                }
            }
            // v0.9.19: the two blocks on legs вЂ” the deck between the pylons must be walkable, the house
            // must really start on the girder three storeys up, and the girder must overhang the facade.
            // Measured instead of trusted: a ray straight down under the block, a ray down onto the first
            // floor, a ray across the core's stair landing and a horizontal ray into the girder.
            var lg = bldgs.Find(x => x.legs);
            if (lg != null)
            {
                var lr = Quaternion.Euler(0f, lg.yaw, 0f);
                float lift = 3f * lg.fh;
                float cxl = Mathf.Min(3.4f, lg.w * 0.18f);
                Vector3 under = lg.p + lr * new Vector3(cxl + 4.6f, 0f, 0f);
                report("under a block on legs (deck)", down(under, 5f, 9f));
                report("first floor over the legs", down(under + Vector3.up * (lift + 0.6f), 0.5f, 4f));
                report("core stair landing 1",
                    down(lg.p + lr * new Vector3(1.6f, lg.fh + 1.2f, 0f), 0.5f, 4f));
                report("transfer girder",
                    shoot(lg.p + lr * new Vector3(0f, lift - 1.5f, lg.d * 0.5f + 9f), lr * Vector3.back, 14f));
                log.LogInfo($"[PROBE] legs block: {lg.w:0.0}x{lg.d:0.0} m at {lg.p.x:0.0},{lg.p.z:0.0}, " +
                            $"first floor {lg.baseY + lift:0.0} m, the deck open between the pylons");
            }
            // v0.9.21: the corner's surface as a **numeric profile** instead of a render вЂ” a ray every
            // 1,5 m from the crest across the tongue, the rise and the flat plate (z), and every 1,5 m
            // down the west bevel (x). Two neighbouring samples may differ by a couple of centimetres
            // (a 2-4 % slope), never by a step.
            {
                var sbp = new System.Text.StringBuilder();
                for (float z = -2f; z <= 14.01f; z += 1.5f)
                    sbp.Append(Col(new Vector3(340f, 8f, z))).Append(' ');
                log.LogInfo($"[PROBE] corner profile along z at x=340 (z -2..14, 1,5 m): {sbp}");
                sbp = new System.Text.StringBuilder();
                for (float x = 316f; x <= 336.01f; x += 1.5f)
                    sbp.Append(Col(new Vector3(x, 8f, 5f))).Append(' ');
                log.LogInfo($"[PROBE] corner profile along x at z=5 (x 316..336, 1,5 m): {sbp}");
            }
            // v0.9.21: the five figures on the House's roof вЂ” a ray down at the middle figure's feet
            // must land on the podium's roof ring (they stand on it, not inside it), and a ray at chest
            // height from the city side must be stopped by the figure's own body
            if (FigFeet.Count == 7)
            {
                var f2 = FigFeet[2];
                report("figure roof, at the middle figure's feet",
                       down(new Vector3(f2.x, f2.y + 0.2f, f2.z), 0.3f, 2.5f));
                var c5 = FigFeet[4] + Vector3.up * (0.63f * FigHi(4));
                report("figure 5 (chest, from the roof edge)",
                       shoot(new Vector3(c5.x, c5.y, c5.z - 6f), Vector3.forward, 9f));
            }
            if (bridges.Count > 0)
            {
                var s = bridges[0];
                Vector3 mid = (s.a + s.b) * 0.5f;
                report("bridge deck", down(mid + Vector3.up * s.y, 1.2f, 3f));
                report("street under a bridge", down(new Vector3(mid.x, 0f, mid.z), 1.0f, 3f));
            }
            report("square", down(new Vector3(232f, PlinthH, 55f), 2f, 5f));

            // v0.9.59: the **rebuilt** House of Soviets вЂ” measured, not judged from a render. Down-rays:
            // the hall floor (stylobate slab), the stylobate roof plate, each tower's roof plate and the
            // entrance pavilion; an up-ray from the hall must stop on the roof's underside; and a ray from
            // the esplanade at 60 m must be stopped by the central loggia volume.
            {
                var hp = new Vector3(230f, PlinthH, 84f);
                var hr = Quaternion.Euler(0f, 4f, 0f);
                System.Func<float, float, float, Vector3> H = (x, y, z) => hp + hr * new Vector3(x, y, z);
                report("House hall floor (expect 10,15 m)", down(H(0f, 2f, 0f), 0f, 4f));
                report("House stylobate roof (expect 22,95 m)", down(H(-50f, 15f, -20f), 0f, 4f));
                report("House lower-tower top (expect 61,4 m)", down(H(30.5f, 58f, 2f), 0f, 6f));
                report("House upper-tower roof (expect 100,0 m)", down(H(30.5f, 94f, 3f), 0f, 6f));
                report("House rear block roof (expect 105,0 m)", down(H(-30.5f, 100f, 20f), 0f, 6f));
                report("House hall ceiling (expect 22,17 m)", up(H(0f, 1.2f, 0f), 0f, 40f));
                report("House gallery floor (expect 48,55 m)", down(H(0f, 39.5f, 2f), 0f, 1f));
                report("House thin bridge floor (expect 74,15 m)", down(H(0f, 64.8f, 0f), 0f, 1f));
                report("House core top (expect 94,0 m)", down(H(0f, 90f, 6f), 0f, 6f));
                report("House entrance wall at 60 m (expect ~35 m)",
                       shoot(H(20f, 3f, -60f), hr * Vector3.forward, 60f));
                // v0.9.68: **the ground-floor passage** вЂ” a ray at knee height (0,35 m over the floor)
                // must run from the doorway all the way to the rear wall (~41 m), not stop at a 0,7 m
                // threshold. And a down-ray right behind the entrance must land on the hall floor.
                report("House ground-floor passage (0,35 m up, expect the rear wall, ~41 m)",
                       shoot(H(0f, BASE + 0.35f, -22f), hr * Vector3.forward, 60f));
                report("House hall floor behind the entrance (expect 10,15 m)",
                       down(H(0f, 2f, -17f), 0f, 4f));
                // v0.9.69: the **towers' ground-floor passage** вЂ” a ray from the hall through the
                // east tower's 3,4 m portal, its corridor and its outer portal must clear both walls
                // (~38,7 m to the hall's outer wall). The 61 m stepped fin used to stop it at ~5 m.
                report("House tower portal (hall -> tower -> side gallery)",
                       shoot(H(10f, BASE + 1.2f, 2f), hr * Vector3.right, 45f));
                report("House tower doorway floor (expect 10,15 m)",
                       down(H(16.5f, BASE + 2f, 2f), 0f, 4f));
                // every collider on the entrance axis at knee and chest height, with the distance вЂ”
                // a still is ambiguous, a distance is not
                foreach (float eye in new[] { BASE + 0.35f, BASE + 1.15f })
                {
                    var w0 = root.TransformPoint(H(0f, eye, -34f));
                    var hits = Physics.RaycastAll(w0, root.rotation * hr * Vector3.forward, 62f, ~0,
                                                  QueryTriggerInteraction.Ignore);
                    System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                    var sb = new System.Text.StringBuilder();
                    foreach (var h in hits) sb.Append($"{h.distance:0.0} ({h.collider.name}) ");
                    log.LogInfo($"[PASS] entrance axis, {eye - BASE:0.00} m over the floor: {sb}");
                }
                // and a 2 m grid over the hall: anything standing on the ground floor is named
                {
                    var sb = new System.Text.StringBuilder();
                    const float floorY = PlinthH + BASE;
                    for (float z = -20f; z <= 20.01f; z += 2f)
                        for (float x = -46f; x <= 46.01f; x += 2f)
                        {
                            var w = root.TransformPoint(H(x, 4f, z));
                            if (!Physics.Raycast(w, Vector3.down, out var hh, 8f, ~0,
                                                 QueryTriggerInteraction.Ignore)) continue;
                            float ly = root.InverseTransformPoint(hh.point).y;
                            if (ly > floorY + 0.55f)
                                sb.Append($"({x:0},{z:0}) {ly - floorY:0.00} m {hh.collider.name}; ");
                        }
                    log.LogInfo($"[HALL] things over 0,55 m on the ground floor: {sb}");
                }
            }

            // v0.9.98: the air-hockey table on the House's top floor вЂ” three measurements, no render:
            // a ray down onto the rink (0,72 m over the annex floor), a ray down beside it (the floor
            // itself, district-local 112,55 m), and a horizontal ray from 8,3 m that must be stopped by
            // the table rather than the hall's west wall.
            if (airHockeyPlaced)
            {
                var hr2 = Quaternion.Euler(0f, 4f, 0f);
                Vector3 ah = airHockeyFeet;
                report("air-hockey table top (body box, expect 1,14 m over the annex floor)",
                    down(ah + hr2 * new Vector3(0.9f, 0f, 0f), 3f, 4f));
                report("air-hockey table floor beside it (expect 112,55 m)",
                    down(ah + hr2 * new Vector3(0f, 0f, 4.2f), 1f, 3f));
                report("air-hockey table side shot from 8,3 m (expect the table at ~5,7 m)",
                    shoot(ah + hr2 * new Vector3(-8.3f, 0.8f, 0f), hr2 * Vector3.right, 14f));
            }

            // v0.9.18: the clock tower on the block on the peninsula вЂ” measured, not judged from a render.
            // A ray down inside the drum must land on the roof plate (the tower's floor); a ray shot at
            // the clock storey from outside must be stopped by the drum's solid wall (the dial is only a
            // disc 4 cm off it), and the mansard's platform must be solid from above.
            if (pentBlock != null)
            {
                var pr = Quaternion.Euler(0f, pentBlock.yaw, 0f);
                report("penthouse floor (inside the drum)",
                    down(pentBlock.p + pr * new Vector3(0f, pentDeckY, 0f), 2f, 4f));
                report("penthouse clock storey wall (+z)",
                    shoot(pentBlock.p + pr * new Vector3(3f, pentDeckY + 5.6f + 5.6f + 4.5f, pentAd + 9f),
                          pr * Vector3.back, 14f));
                report("penthouse lantern roof",
                    down(pentBlock.p + pr * new Vector3(0f, pentTopY, 0f), 4f, 12f));
                report("penthouse roof platform",
                    down(pentBlock.p + pr * new Vector3(pentAw * 0.22f, pentApexY, pentAd * 0.22f), 6f, 14f));
            }

            // v0.9.9: every floor of the terraced platform, the middle of two ramps and a stair tread
            report("terrace 3 floor (+8)", down(new Vector3(190f, 8f, 250f), 2f, 5f));
            report("terrace 2 floor (+5)", down(new Vector3(370f, 5f, 190f), 2f, 5f));
            report("terrace 4 floor (+11)", down(new Vector3(10f, 11f, 100f), 2f, 5f));
            report("terrace 5 floor (+14)", down(new Vector3(-40f, 14f, 265f), 2f, 5f));
            report("plinth of the House (+10)", down(new Vector3(200f, PlinthH, 120f), 2f, 5f));
            report("ramp ring_west (middle)", down(new Vector3(56f, 4f, 175f), 2f, 6f));
            report("ramp plinth (middle)", down(new Vector3(132f, 5f, 79f), 2f, 6f));
            report("stair to terrace 3", down(new Vector3(137f, 4f, 201f), 2f, 6f));
            report("base deck between the terraces", down(new Vector3(150f, 0f, 150f), 2f, 5f));
            // v0.9.20: the corner next to the round hole вЂ” the pad is flat and stands on the crest's own
            // level, the terrace's wall is CornerApron metres inland, and the base deck is where it was
            report("corner pad (its top, over our own deck)",
                down(new Vector3(330f, 0.4f, 6f), 2f, 5f));
            report("corner pad over the granite shelf (10 mm above the stone, was 25)",
                down(new Vector3(356f, 0.4f, 4f), 2f, 5f));
            // (the v0.9.57 House plate probes were replaced by the v0.9.59 block above вЂ” the House has
            // been rebuilt and its geometry moved)
            report("south-east corner, behind the pad (+5)", down(new Vector3(356f, 5f, 26f), 2f, 5f));
            report("east terrace still at +5 (mid band)", down(new Vector3(356f, 5f, 120f), 2f, 5f));
            report("base deck west of the ramp's foot", down(new Vector3(314f, 0f, 4f), 2f, 5f));
            // v0.9.22: the corner's *numeric height field* вЂ” the pad's own collider is switched off for
            // the scan, so the rows show the surface **under** it (the level's own granite crest and its
            // exact extent). A suffix tells who owns the hit: L = the level, d = our deck, t = terraces,
            // s = stairs, r = ramps. The design of the smooth skirt is read off these numbers.
            CornerScan();
            log.LogInfo($"[PROBE] done: {ok} ok, {bad} misses");
            Headroom();
            DumpPlan();
        }

        /// <summary>
        /// v0.9.22 вЂ” the corner as a height field, measured with the pad's collider switched off, so the
        /// rows show what is **under** it: the level's own granite crest, where it begins and ends, and
        /// the deck around it. Each value is `height` + an owner letter (`L` level, `d` deck, `t`
        /// terrace, `s` stair, `r` ramp). The skirt is designed from these rows, not from a render.
        /// </summary>
        private static void CornerScan()
        {
            if (root == null) return;
            Transform pad = root.Find("corner_pad");
            Collider pc = pad != null ? pad.GetComponent<Collider>() : null;
            bool was = pc != null && pc.enabled;
            if (pc != null) pc.enabled = false;
            try
            {
                System.Func<float, float, string> V = (x, z) =>
                {
                    Vector3 w = root.TransformPoint(new Vector3(x, 40f, z));
                    if (!Physics.Raycast(w, Vector3.down, out var h, 120f, ~0, QueryTriggerInteraction.Ignore))
                        return "MISS";
                    string n = h.collider.name;
                    char c =
                        n.StartsWith("deck") ? 'd' :
                        n.StartsWith("terrac") ? 't' :
                        n.StartsWith("stairs") ? 's' :
                        n.StartsWith("ramp") ? 'r' :
                        n.StartsWith("streets") ? 'd' : 'L';
                    return string.Format("{0:0.00}{1}", h.point.y - root.position.y, c);
                };
                foreach (float x in new[] { 318f, 322f, 326f, 330f, 334f, 342f, 350f, 360f, 370f, 378f })
                {
                    var sb = new System.Text.StringBuilder();
                    for (float z = -4f; z <= 14.01f; z += 2f)
                        sb.Append(V(x, z).PadLeft(8)).Append(' ');
                    log.LogInfo($"[PROBE] corner z-profile x={x:0} (z -4..14, 2 m): {sb}");
                }
                foreach (float z in new[] { -3f, -2f, -1f, 0f, 1f, 3f, 6f, 10f })
                {
                    var sb = new System.Text.StringBuilder();
                    for (float x = 300f; x <= 380.01f; x += 4f)
                        sb.Append(V(x, z).PadLeft(8)).Append(' ');
                    log.LogInfo($"[PROBE] corner x-profile z={z:0} (x 300..380, 4 m): {sb}");
                }
            }
            finally
            {
                if (pc != null) pc.enabled = was;
            }

            // ---- v0.9.22: the same corner with the pad **on**, sampled every 0.5 m. This is the surface
            // the player walks on, so a step is a jump between two neighbours; the biggest 0,5 m step is
            // printed with it. (A 4-7 % ramp is ~2-3,5 cm per 0,5 m, anything above that is a lip.)
            System.Func<float, float, float> Top = (x, z) =>
            {
                Vector3 w = root.TransformPoint(new Vector3(x, 40f, z));
                return Physics.Raycast(w, Vector3.down, out var h, 120f, ~0, QueryTriggerInteraction.Ignore)
                    ? h.point.y - root.position.y : float.NaN;
            };
            System.Action<string, bool, float, float, float> line = (tag, alongX, fixedC, from, to) =>
            {
                var sb = new System.Text.StringBuilder();
                float prevY = 0f, worst = 0f, at = 0f;
                int walls = 0;
                bool have = false;
                for (float t = from; t <= to + 0.001f; t += 0.5f)
                {
                    float y = alongX ? Top(t, fixedC) : Top(fixedC, t);
                    if (float.IsNaN(y)) { sb.Append("  MISS"); have = false; continue; }
                    if (have && Mathf.Abs(y - prevY) > worst)
                    {
                        if (Mathf.Abs(y - prevY) > 1f) walls++;        // a terrace wall / a parapet, not a lip
                        else { worst = Mathf.Abs(y - prevY); at = t; }
                    }
                    prevY = y; have = true;
                    sb.Append(y.ToString("0.00").PadLeft(6));
                }
                log.LogInfo($"[PROBE] corner {tag}: {sb}");
                log.LogInfo($"[PROBE] corner {tag}: biggest 0,5 m step {worst * 100f:0.0} cm at {at:0.0} m" +
                            (walls > 0 ? $" ({walls} jump(s) over 1 m: a wall, not the floor)" : ""));
            };
            line("walk z-profile x=340 (z -3..13)", false, 340f, -3f, 13f);
            line("walk x-profile z=6 (x 312..345)", true, 6f, 312f, 345f);
            // v0.9.23: the south bevel вЂ” the row that used to be a 0,25-0,36 m wall at z = 12,3
            line("south bevel z-profile x=325 (z 8..20)", false, 325f, 8f, 20f);
            line("south bevel z-profile x=316 (z 8..20)", false, 316f, 8f, 20f);
            CornerSteps();
        }

        /// <summary>
        /// v0.9.23 вЂ” the corner as a **map of the walked surface**, not a profile: every 2 m over
        /// x 296..388 / z -20..32 the first thing a ray from above hits is printed as `height|owner`
        /// (d = deck, p = corner pad, t = terrace, s = stairs, r = ramp, L = the level's own mesh).
        /// The rows are the picture the player sees: a raised plate that blocks passage shows up as its
        /// own height with a sharp change to the neighbours, and `step` lines name the two colliders.
        /// This exists because a render cannot tell 0,32 m of the level's granite from 0,365 m of our
        /// own concrete вЂ” the numbers can.
        /// </summary>
        private static void CornerSteps()
        {
            if (root == null) return;
            const float x0 = 296f, x1 = 388f, z0 = -20f, z1 = 32f, step = 2f;
            int nx = Mathf.RoundToInt((x1 - x0) / step) + 1;
            int nz = Mathf.RoundToInt((z1 - z0) / step) + 1;
            var ys = new float[nx, nz];
            var own = new char[nx, nz];
            var nom = new string[nx, nz];
            for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                {
                    Vector3 w = root.TransformPoint(new Vector3(x0 + ix * step, 40f, z0 + iz * step));
                    if (!Physics.Raycast(w, Vector3.down, out var h, 120f, ~0, QueryTriggerInteraction.Ignore))
                    { ys[ix, iz] = float.NaN; own[ix, iz] = '?'; nom[ix, iz] = "MISS"; continue; }
                    ys[ix, iz] = h.point.y - root.position.y;
                    string n = h.collider.name;
                    nom[ix, iz] = n;
                    own[ix, iz] = n.StartsWith("corner_pad") ? 'p'
                                : n.StartsWith("deck") ? 'd'
                                : n.StartsWith("terrac") ? 't'
                                : n.StartsWith("stairs") ? 's'
                                : n.StartsWith("ramp") ? 'r'
                                : n.StartsWith("streets") ? 'd' : 'L';
                }

            // ---- the picture: height in cm (2 chars) + owner, one line per z row --------------------
            for (int iz = nz - 1; iz >= 0; iz--)
            {
                var sb = new System.Text.StringBuilder();
                for (int ix = 0; ix < nx; ix++)
                {
                    if (float.IsNaN(ys[ix, iz])) { sb.Append("  .  "); continue; }
                    sb.Append((Mathf.RoundToInt(ys[ix, iz] * 100f)).ToString().PadLeft(4)).Append(own[ix, iz]).Append(' ');
                }
                log.LogInfo($"[PROBE] corner map z={z0 + iz * step,6:0} (x 296..388, 2 m): {sb}");
            }

            // ---- every real step between two neighbouring samples -----------------------------------
            int found = 0, walls = 0;
            for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                {
                    if (float.IsNaN(ys[ix, iz])) continue;
                    for (int k = 0; k < 2; k++)
                    {
                        int jx = ix + (k == 0 ? 1 : 0), jz = iz + (k == 1 ? 1 : 0);
                        if (jx >= nx || jz >= nz || float.IsNaN(ys[jx, jz])) continue;
                        float dy = ys[jx, jz] - ys[ix, iz];
                        if (Mathf.Abs(dy) < 0.20f || Mathf.Abs(dy) > 1.6f) continue;   // not a lip / a wall
                        // the report is about the **walkable corner**: the platform's own rim (z < 0,3, an
                        // 8,58 m drop into the basin), the sea parapet (x > 374) and the terrace's wall
                        // (z > 11,9) are the designed boundary, not a lip in the way
                        float sx = x0 + ix * step, sz = z0 + iz * step;
                        if (sx < 310f || sx > 374f || sz < 0.3f || sz > 11.9f) { walls++; continue; }
                        // a designed wall is not a lip either: the terrace's parapet and the sea coping
                        bool designed = nom[ix, iz].Contains("rail") || nom[jx, jz].Contains("rail")
                                     || nom[ix, iz].Contains("parapet") || nom[jx, jz].Contains("parapet");
                        if (designed) { walls++; continue; }
                        found++;
                        if (found > 60) continue;
                        log.LogInfo($"[PROBE] corner step {Mathf.Abs(dy) * 100f,5:0.0} cm at " +
                                    $"({sx:0},{sz:0}) -> ({x0 + jx * step:0},{z0 + jz * step:0}) " +
                                    $"{ys[ix, iz]:0.00}{own[ix, iz]} ({nom[ix, iz]}) / " +
                                    $"{ys[jx, jz]:0.00}{own[jx, jz]} ({nom[jx, jz]})");
                    }
                }
            log.LogInfo($"[PROBE] corner steps: {found} lip(s) of 0,20-1,60 m inside the walkable corner " +
                        $"(x 310..374, z 0,3..11,9) between 2 m samples; {walls} samples are the designed " +
                        $"boundary (the platform's rim over the drop, the terrace's rail, the sea parapet). " +
                        $"A lip over 0,30 m blocks the player (CharacterController stepOffset)");
            CornerFaces();
        }

        /// <summary>
        /// v0.9.23 вЂ” the **proof** that the corner's walking surface has no vertical face left. The
        /// 2 m `CornerSteps` grid cannot tell a 0,20 m wall from a 10 % ramp (two metres of a bevel are
        /// 0,2 m of height), so this samples the walkable part on a **0,25 m** grid вЂ” clear of the
        /// platform's edge, the terrace's wall and the sea parapet вЂ” and prints the biggest jump between
        /// two neighbours. 0,08 m over 0,25 m is a 32 % slope: anything above that is a face, not a ramp.
        /// </summary>
        private static void CornerFaces()
        {
            if (root == null) return;
            const float st = 0.25f, xa = 312f, xb = 373f, za = 0.3f, zb = 11.8f;
            int nx = Mathf.RoundToInt((xb - xa) / st) + 1, nz = Mathf.RoundToInt((zb - za) / st) + 1;
            var y = new float[nx, nz];
            for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                {
                    Vector3 w = root.TransformPoint(new Vector3(xa + ix * st, 40f, za + iz * st));
                    y[ix, iz] = Physics.Raycast(w, Vector3.down, out var h, 120f, ~0, QueryTriggerInteraction.Ignore)
                        ? h.point.y - root.position.y : float.NaN;
                }
            float worst = 0f, wx = 0f, wz = 0f;
            int faces = 0, n = 0;
            for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                {
                    if (float.IsNaN(y[ix, iz])) continue;
                    for (int k = 0; k < 2; k++)
                    {
                        int jx = ix + (k == 0 ? 1 : 0), jz = iz + (k == 1 ? 1 : 0);
                        if (jx >= nx || jz >= nz || float.IsNaN(y[jx, jz])) continue;
                        n++;
                        float dy = Mathf.Abs(y[jx, jz] - y[ix, iz]);
                        if (dy > worst) { worst = dy; wx = xa + ix * st; wz = za + iz * st; }
                        if (dy > 0.08f) faces++;
                    }
                }
            log.LogInfo($"[PROBE] corner faces: {n} rays ({nx} x {nz} at 0,25 m over x {xa:0}..{xb:0} / z {za:0.##}..{zb:0.#}), " +
                        $"biggest jump between neighbours {worst * 100f:0.0} cm at ({wx:0.0},{wz:0.0}), " +
                        $"{faces} jump(s) over 8 cm (= a 32 % slope, i.e. a vertical face) вЂ” a walkable corner has 0");
        }

        /// <summary>
        /// v0.9.11 вЂ” look inward through the recorded windows of one building and report the first
        /// thing behind the opening. A sensible window shows the room's far side (>= 3 m); a partition
        /// or a stair flight right behind it shows up as ~1 m and is the bug the user photographed.
        /// </summary>
        private static void WindowProbes(Bldg b, string tag)
        {
            var rot = Quaternion.Euler(0f, b.yaw, 0f);
            for (int i = 0; i < b.winPos.Count && i < b.winDir.Count; i++)
            {
                // world space: the district root is itself rotated, so the building yaw is not enough
                Vector3 n = root.rotation * (rot * b.winDir[i]);
                Vector3 lp = b.p + rot * b.winPos[i];
                Vector3 p = root.TransformPoint(lp) + n * 2.5f;
                string s = Physics.Raycast(p, -n, out var h, 40f, ~0, QueryTriggerInteraction.Ignore)
                    ? string.Format("first hit {0:0.00} m ({1})", h.distance - 2.5f, h.collider.name)
                    : "open all the way through";
                log.LogInfo($"[PROBE] {tag} window {i}: {s}");
            }
        }

        public struct PreviewShot
        {
            public Vector3 Pos;
            public Vector3 Look;
            public string Name;   // v0.9.10: set by GrainShots(), unused by the district poses
            public float Ortho;   // v0.9.10: > 0 = orthographic half-size (plan-style), 0 = camera FOV
            public bool Lamp;     // v0.9.12: light this shot with a temporary sun (dark facades)
            public int RampState; // v0.9.112: 1 = raised, 2 = half, 3 = lowered (0 = leave)
        }

        // ------------------------------------------------------------------ mesh helper

        /// <summary>Vertex/index accumulator with two submeshes: 0 = concrete, 1 = window glass.</summary>
        private sealed class Geo
        {
            public readonly List<Vector3> V = new List<Vector3>(4096);
            public readonly List<Vector3> N = new List<Vector3>(4096);
            public readonly List<Vector2> UV = new List<Vector2>(4096);
            public readonly List<int> T0 = new List<int>(8192);
            public readonly List<int> T1 = new List<int>(1024);

            /// <summary>v0.9.13: extra grain density for this mesh's horizontal faces (the platform's
            /// own surfaces вЂ” deck, terraces, streets, ramps, stairs, esplanade вЂ” are 4x the houses').</summary>
            public float FloorMul = 1f;

            /// <summary>Tangent frame of a face: u runs along the face, v up вЂ” horizontal faces use
            /// world X/Z instead, so every face of one orientation samples the same place.</summary>
            private static void Frame(Vector3 n, out Vector3 tx, out Vector3 ty)
            {
                if (Mathf.Abs(n.y) > 0.7f)
                {
                    tx = Vector3.right;
                    ty = Vector3.forward;
                }
                else
                {
                    tx = Vector3.Cross(Vector3.up, n).normalized;
                    ty = Vector3.Cross(n, tx).normalized;
                }
            }

            /// <summary>
            /// Quad with **world-anchored UVs** (v0.9.4-r3). The old code started every quad at (0,0)
            /// and scaled by the quad's own size, so each wall cell, window reveal and box face
            /// restarted the texture. On a facade built from 1-5 m cells that restart is a visible
            /// grid of identical patches вЂ” it read as tiling/panelling (the user's "С„РѕС‚Рѕ 1"), and it
            /// is why lowering the density only made the patches bigger ("РєР°РјСѓС„Р»СЏР¶"). Projecting the
            /// corners onto the face's tangent frame gives one continuous UV field over the whole
            /// district, so `T_RoughConcreteMacro`'s 1-5 cm pits flow across a building exactly like
            /// the game's own blocks (photo 4) and across the deck like the dam (photo 2).
            /// </summary>
            public void Quad(int part, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, float uv)
            {
                Vector3 tx, ty;
                Frame(n, out tx, out ty);
                float k = GrainUv * (Mathf.Abs(n.y) > 0.7f ? FloorMul : 1f);   // v0.9.13: the platform's floors are grainier
                Quad(part, a, b, c, d, n,
                     new Vector2(Vector3.Dot(a, tx), Vector3.Dot(a, ty)) * k,
                     new Vector2(Vector3.Dot(b, tx), Vector3.Dot(b, ty)) * k,
                     new Vector2(Vector3.Dot(c, tx), Vector3.Dot(c, ty)) * k,
                     new Vector2(Vector3.Dot(d, tx), Vector3.Dot(d, ty)) * k);
            }

            public void Quad(int part, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n,
                             Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
            {
                int i = V.Count;
                V.Add(a); V.Add(b); V.Add(c); V.Add(d);
                N.Add(n); N.Add(n); N.Add(n); N.Add(n);
                UV.Add(ua); UV.Add(ub); UV.Add(uc); UV.Add(ud);
                var t = part == 0 ? T0 : T1;
                // Unity front faces are clockwise seen from the front: the geometric normal of the
                // triangle must point along the face normal.
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), n) > 0f)
                { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
                else
                { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
            }

            /// <summary>v0.9.18: one triangle вЂ” the clock dial's fan (winding matches `Quad`).</summary>
            public void Tri(int part, Vector3 a, Vector3 b, Vector3 c, Vector3 n,
                            Vector2 ua, Vector2 ub, Vector2 uc)
            {
                int i = V.Count;
                V.Add(a); V.Add(b); V.Add(c);
                N.Add(n); N.Add(n); N.Add(n);
                UV.Add(ua); UV.Add(ub); UV.Add(uc);
                var t = part == 0 ? T0 : T1;
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), n) > 0f)
                { t.Add(i); t.Add(i + 1); t.Add(i + 2); }
                else
                { t.Add(i); t.Add(i + 2); t.Add(i + 1); }
            }

            public void Box(int part, Vector3 c, Vector3 s, Quaternion r, float uv)
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
                Quad(part, p[1], p[3], p[7], p[5], R, uv);
                Quad(part, p[0], p[4], p[6], p[2], -R, uv);
                Quad(part, p[2], p[6], p[7], p[3], U, uv);
                Quad(part, p[0], p[1], p[5], p[4], -U, uv);
                Quad(part, p[4], p[5], p[7], p[6], F, uv);
                Quad(part, p[0], p[2], p[3], p[1], -F, uv);
            }

            public GameObject Emit(Transform parent, string name, Material m0, Material m1 = null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                Apply(go, m0, m1);
                return go;
            }

            public Mesh Apply(GameObject go, Material m0, Material m1 = null)
            {
                var mesh = new Mesh { name = go.name };
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(V);
                mesh.SetNormals(N);
                mesh.SetUVs(0, UV);
                bool two = T1.Count > 0 && m1 != null;
                mesh.subMeshCount = two ? 2 : 1;
                mesh.SetTriangles(T0, 0);
                if (two) mesh.SetTriangles(T1, 1);
                mesh.RecalculateBounds();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = two ? new[] { m0, m1 } : new[] { m0 };
                return mesh;
            }
        }
    }
}

