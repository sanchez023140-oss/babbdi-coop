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
    internal static partial class SovietDistrict
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
                                              // (the user: "увеличь её толщину вниз в 3 раза" вЂ” the plate's
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
        public static float GrainScale = 0.0167f;

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
        public static float PlatformGrain = 1.0f;

        /// <summary>v0.9.70 вЂ” plan point 1-2: use the **main BABBDI city's own surfaces** instead of
        /// a re-baked grain. `true` = the level's raw `M_ConcreteRough00` (the material every city block
        /// and the ground use) and `M_ConcretePanels00` for the panel (khrushchevka) blocks; `false` =
        /// the old v0.9.11 derived grain with the 1.68 tone lift.</summary>
        public static bool MatchCity = true;
        /// <summary>
        /// [District] CornerApron (v0.9.20) вЂ” how far the **east terrace** (+5 m) is set back from the
        /// platform's south edge, so that its corner next to the round hole in the sea is the *base*
        /// deck and not a raised floor (the user's red outline in screenshot 1: "сровнять с базовым
        /// уровнем общей платформы"). 0 = the old corner, flush with the crest; 12 = a 50 x 12 m apron
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
            // core's stair ended under a solid slab: the user's "пролеты упираются в перекрытия".
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
            // v0.9.35: the two flights are separated by a real **open well** вЂ” the user's "вертикальная
            // щель между перилами, проходящая через весь дом сверху вниз" (their reference photo). It
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
            log.LogInfo($"[DISTRICT] grain uv={GrainUv:0.#####}/m platform={GrainUv * PlatformGrain:0.#####}/m (scale={GrainScale:0.#####}, platform mul={PlatformGrain:0.##})");
            DumpTextures();
            ScanNpcs();         // TEMP (v0.9.21): the game's own humanoids, measured
            if (mRough == null)
            {
                log.LogError("[DISTRICT] concrete materials not found — district skipped");
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
    }
}
