using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

namespace BabbdiCoop
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class BabbdiCoopPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.babbdi.coop";
        public const string PluginName = "BabbdiCoop";
        public const string PluginVersion = "0.10.43";

        private static ManualLogSource Log;

        private Net net = new Net();
        private AirHockeyGame miniGame;   // v0.10.0: the two-player air-hockey activity

        private ConfigEntry<string> joinIp;
        private ConfigEntry<int> port;
        private ConfigEntry<bool> previewCfg;   // debug: spawn the avatar locally + screenshots
        private ConfigEntry<string> gripCfg;    // debug: auto | flip | keep вЂ” which end the item is held by
        private ConfigEntry<float> itemScaleCfg; // debug: third-person item size multiplier
        private ConfigEntry<string> smoothingCfg; // debug: new | legacy (v0.7.7 behaviour)
        private ConfigEntry<bool> netSimCfg;     // debug: synthetic packet stream self-test
        private ConfigEntry<float> netSimSecondsCfg;
        private ConfigEntry<bool> traceCfg;     // debug: per-frame CSV of the REAL receive path
        private ConfigEntry<bool> worldScanCfg; // debug: one-shot survey of the loaded level
        private bool worldScanDone;
        private ConfigEntry<bool> mapScanCfg;   // v0.9.77: one-shot scan of the level's sky and boundaries
        private bool mapScanDone;
        private ConfigEntry<bool> mapShotCfg;   // debug: stills of the level (top-down + dam)
        private bool mapShotDone;
        private ConfigEntry<bool> miniTestCfg;  // v0.10.0 debug: teleport to the table, enter, screenshot
        private ConfigEntry<bool> dinoTestCfg;  // v0.10.21 debug: teleport to a cabinet, enter, screenshot
        private ConfigEntry<string> arcadeTestCfg; // v0.10.36 debug: arkanoid|pong|snake test
        private ConfigEntry<string> duelTestCfg;   // v0.10.42 debug: duel cabinet host test
        private ArcadeGame arcadeGame;
        private ConfigEntry<string> loadSceneCfg; // debug: jump straight into a scene at start
        private bool loadSceneDone;

        // remote avatar state
        private GameObject remoteGo;
        private Vector3 remoteTargetPos;
        private float remoteTargetYaw;
        private float lastSend;
        private int remoteItemId = -1;        // last received ItemProperties.id (-1 = empty hands)
        private int remoteManualFlipFor = -1; // F8 override: item id whose grip end was swapped by hand
        private bool remoteManualFlip;        // the chosen orientation for that id
        private GameObject remoteHeldItem;     // cloned item attached to avatar
        private CommandBuffer itemOverlayCb;   // AfterEverything pass that draws the viewmodel on top
        private Camera itemOverlayCam;
        private readonly List<Renderer> overlayItems = new List<Renderer>(8);
        private GlbAnimator remoteAnimator;    // skeletal animation of the avatar

        // v0.10.6: the second remote peer (host's second client / the other client). A simple
        // smoothed follower; the full pipeline stays on the primary peer.
        private GameObject extraGo;
        private GlbAnimator extraAnimator;
        private Vector3 extraTargetPos;
        private float extraTargetYaw;
        private Vector3 extraSmoothPos;
        private float extraSmoothYaw;
        private float extraSpeed, extraPitchTarget, extraPitch;
        private Vector3 extraVelLocal;
        private int extraFlags;
        private float extraLastPacketTime;
        private int remoteFlags;               // crouch / airborne / use bits from the last packet
        private float usePulseUntil;           // local Fire1 held-item "use" edge, sent for a few packets
        private float airTimer;                // debounce for CharacterController.isGrounded
        private bool airState;
        private float remoteSpeed;             // m/s estimated from packet positions
        private Vector3 remoteVelLocal;        // rendered velocity in the avatar's frame (x=right, z=forward)
        private float remotePitch;             // remote camera pitch, smoothed (+ = looking down)
        private float remotePitchTarget;       // raw pitch of the last packet
        private Vector3 remoteLastPacketPos;
        private float remoteLastPacketTime = -1f;

        // v0.8.0 tricks. Local player state is sent in the packet flags; the camera flip is
        // applied in LateUpdate so the game's own mouse look runs first.
        // v0.8.1: both tricks are written as a timeline of the body doing the move itself вЂ”
        // the handstand jumps onto the hands (COM path, hands land last), the flip keeps its
        // angular speed through the second half, and no layer (gait/crouch/air/lean) fights
        // the trick pose. The packet format did not change.
        private bool handstandOn;
        private float handstandCamBlend;
        private float flipStart = -1f;
        private const float FlipCamDuration = GlbAnimator.FlipDuration;

        // jitter buffer: packets arrive at ~20 Hz over UDP (variable intervals), so the
        // avatar renders slightly in the past, interpolating between the two samples
        // that bracket that render time
        private struct PoseSample
        {
            public float Time;
            public Vector3 Pos;
            public float Yaw;
        }
        private readonly List<PoseSample> samples = new List<PoseSample>(16);
        private float sampleInterval = 1f / 30f;

        // v0.7.8 smoothing state.
        // The knots of the spline are put on a synthetic uniform grid (sampleInterval
        // apart) instead of on the real arrival times: a Catmull-Rom is parameterised
        // uniformly, so unevenly spaced knots make the speed change at every knot вЂ”
        // that was the stutter that survived the earlier buffer work.
        private float lastArrivalTime = -1f;
        // rendered pose: critically damped follower of the interpolated target (see AnimateRemote)
        private Vector3 smoothPos, posVel;
        private float smoothYaw, yawVel;
        private bool smoothInit;
        private float renderDelay = 0.12f;  // smoothed render delay (never follows a hiccup)
        // v0.8.0: measured arrival jitter (|spacing в€’ smoothed cadence|). It is what the
        // buffer has to cover, so it вЂ” not the interval вЂ” sizes the render delay.
        private float arrivalJitter;
        // smoothed floor correction (a stair used to pop the whole model in one packet)
        private float groundOffset;
        private bool groundOffsetInit;
        private readonly List<(Vector3 pos, Quaternion rot, int weaponIdx, int flags, float clock, int peer)> incomingBatch
            = new List<(Vector3, Quaternion, int, int, float, int)>(8);

        private void Awake()
        {
            Log = Logger;
            joinIp = Config.Bind("Net", "JoinIP", "127.0.0.1", "IP Р°РґСЂРµСЃ С…РѕСЃС‚Р° РґР»СЏ РїРѕРґРєР»СЋС‡РµРЅРёСЏ (F6)");
            port = Config.Bind("Net", "Port", 7777, "UDP РїРѕСЂС‚");
            previewCfg = Config.Bind("Debug", "PreviewAvatar", false,
                "РћС„С„Р»Р°Р№РЅ-РїСЂРѕРІРµСЂРєР°: СЃРїР°РІРЅРёС‚ Р°РІР°С‚Р°СЂ РІ РјРµРЅСЋ, РїСЂРѕРёРіСЂС‹РІР°РµС‚ РїРѕР·С‹ Рё РїРёС€РµС‚ СЃРєСЂРёРЅС€РѕС‚С‹ РІ BepInEx/plugins/BabbdiCoop/preview");
            gripCfg = Config.Bind("Debug", "ItemGrip", "auto",
                "РљР°РєРёРј РєРѕРЅС†РѕРј Р°РІР°С‚Р°СЂ РґРµСЂР¶РёС‚ РґР»РёРЅРЅС‹Р№ РїСЂРµРґРјРµС‚: auto = С‚РѕРЅРєРёР№ РєРѕРЅРµС† (СЂСѓРєРѕСЏС‚РєР°) РІ Р»Р°РґРѕРЅРё, flip = РїСЂРёРЅСѓРґРёС‚РµР»СЊРЅРѕ СЂР°Р·РІРµСЂРЅСѓС‚СЊ, keep = РЅРµ СЂР°Р·РІРѕСЂР°С‡РёРІР°С‚СЊ");
            itemScaleCfg = Config.Bind("Debug", "ItemScale", 0.5f,
                "РњР°СЃС€С‚Р°Р± РїСЂРµРґРјРµС‚РѕРІ РІ СЂСѓРєР°С… Р°РІР°С‚Р°СЂР° (С‚СЂРµС‚СЊРµ Р»РёС†Рѕ): 1 = РєР°Рє РІ РјРёСЂРµ, 0.5 = РІРґРІРѕРµ РјРµРЅСЊС€Рµ");
            smoothingCfg = Config.Bind("Debug", "Smoothing", "new",
                "РЎРіР»Р°Р¶РёРІР°РЅРёРµ РґРІРёР¶РµРЅРёСЏ Р°РІР°С‚Р°СЂР°: new = РІС‹СЂРѕРІРЅРµРЅРЅР°СЏ СЃРµС‚РєР° РѕС‚СЃС‡С‘С‚РѕРІ + РїР»Р°РІСѓС‡РёР№ РєР»РѕРє (v0.7.8), legacy = РєР°Рє РІ v0.7.7 (РґР»СЏ A/B)");
            netSimCfg = Config.Bind("Debug", "NetSim", false,
                "РўРѕР»СЊРєРѕ РґР»СЏ РѕС‚Р»Р°РґРєРё: РіРѕРЅСЏРµС‚ СЃРёРЅС‚РµС‚РёС‡РµСЃРєРёР№ РїРѕС‚РѕРє РїР°РєРµС‚РѕРІ (30 Р“С† + РґР¶РёС‚С‚РµСЂ В±20 РјСЃ + РїР°СѓР·С‹ 100 РјСЃ) С‡РµСЂРµР· РЅР°СЃС‚РѕСЏС‰РёР№ РїСЂРёС‘Рј Рё РїРёС€РµС‚ CSV РІ BepInEx/plugins/BabbdiCoop");
            netSimSecondsCfg = Config.Bind("Debug", "NetSimSeconds", 12f,
                "РЎРєРѕР»СЊРєРѕ СЃРµРєСѓРЅРґ РґР»РёС‚СЃСЏ NetSim-РїСЂРѕРіРѕРЅ");
            traceCfg = Config.Bind("Debug", "Trace", false,
                "РўРѕР»СЊРєРѕ РґР»СЏ РѕС‚Р»Р°РґРєРё: РїРёС€РµС‚ РїРѕРєР°РґСЂРѕРІС‹Р№ CSV РЅР°СЃС‚РѕСЏС‰РµРіРѕ РїСЂРёС‘РјР° (trace_HHmmss.csv) вЂ” РїРѕР·РёС†РёСЏ РґРѕ Рё РїРѕСЃР»Рµ СЃРіР»Р°Р¶РёРІР°РЅРёСЏ, СЃРєРѕСЂРѕСЃС‚СЊ, Р·Р°РґРµСЂР¶РєР°, РґР¶РёС‚С‚РµСЂ РїСЂРёС…РѕРґР°");
            worldScanCfg = Config.Bind("Debug", "WorldScan", false,
                "РўРѕР»СЊРєРѕ РґР»СЏ РѕС‚Р»Р°РґРєРё: РѕРґРёРЅ СЂР°Р· СЃРєР°РЅРёСЂСѓРµС‚ Р·Р°РіСЂСѓР¶РµРЅРЅС‹Р№ СѓСЂРѕРІРµРЅСЊ Рё РїРёС€РµС‚ worldscan.txt (РіРґРµ РіРѕСЂРѕРґ, РіРґРµ СЃС‚РµРЅР°-РґР°РјР±Р°, РєР°РєРёРµ РјР°С‚РµСЂРёР°Р»С‹). РЎР°РјРѕ СЃР±СЂР°СЃС‹РІР°РµС‚СЃСЏ РІ false");
            mapScanCfg = Config.Bind("Debug", "MapScan", false,
                "РўРѕР»СЊРєРѕ РґР»СЏ РѕС‚Р»Р°РґРєРё: РѕРґРёРЅ СЂР°Р· СЃРєР°РЅРёСЂСѓРµС‚ РЅРµР±Рѕ Рё РЅРµРІРёРґРёРјС‹Рµ РіСЂР°РЅРёС†С‹ РєР°СЂС‚С‹, РїРёС€РµС‚ mapscan.txt. РЎР°РјРѕ СЃР±СЂР°СЃС‹РІР°РµС‚СЃСЏ РІ false");
            mapShotCfg = Config.Bind("Debug", "MapShot", false,
                "РўРѕР»СЊРєРѕ РґР»СЏ РѕС‚Р»Р°РґРєРё: РґРµР»Р°РµС‚ РЅРµСЃРєРѕР»СЊРєРѕ СЃРЅРёРјРєРѕРІ СѓСЂРѕРІРЅСЏ СЃРІРµСЂС…Сѓ Рё СЃРѕ СЃС‚РѕСЂРѕРЅС‹ РґР°РјР±С‹ РІ BepInEx/plugins/BabbdiCoop/map. РЎР°РјРѕ СЃР±СЂР°СЃС‹РІР°РµС‚СЃСЏ РІ false");
            miniTestCfg = Config.Bind("Debug", "MiniTest", false,
                "РўРѕР»СЊРєРѕ РґР»СЏ РѕС‚Р»Р°РґРєРё: С‚РµР»РµРїРѕСЂС‚РёСЂСѓРµС‚ РёРіСЂРѕРєР° Рє Р°СЌСЂРѕС…РѕРєРєРµСЋ, РІС…РѕРґРёС‚ РІ СЂРµР¶РёРј СЃС‚РѕР»Р° Рё РїРёС€РµС‚ mini_test.png РІ РїР°РїРєСѓ РёРіСЂС‹. РЎР°РјРѕ СЃР±СЂР°СЃС‹РІР°РµС‚СЃСЏ РІ false");
            dinoTestCfg = Config.Bind("Debug", "DinoTest", false,
                "РўРѕР»СЊРєРѕ РґР»СЏ РѕС‚Р»Р°РґРєРё: С‚РµР»РµРїРѕСЂС‚РёСЂСѓРµС‚ РёРіСЂРѕРєР° Рє Р°РІС‚РѕРјР°С‚Сѓ, РІС…РѕРґРёС‚ РІ РёРіСЂСѓ В«Р”РёРЅРѕР·Р°РІСЂРёРєВ» Рё РїРёС€РµС‚ dino_test.png РІ РїР°РїРєСѓ РёРіСЂС‹. РЎР°РјРѕ СЃР±СЂР°СЃС‹РІР°РµС‚СЃСЏ РІ false");
            duelTestCfg = Config.Bind("Debug", "DuelTest", "",
                "debug: pong/arkanoid/snake - enter the duel cabinet as host and write screenshots");
            arcadeTestCfg = Config.Bind("Debug", "ArcadeTest", "",
                "РўРѕР»СЊРєРѕ РґР»СЏ РѕС‚Р»Р°РґРєРё: arkanoid, pong РёР»Рё snake вЂ” С‚РµР»РµРїРѕСЂС‚РёСЂСѓРµС‚ Рє Р°РІС‚РѕРјР°С‚Сѓ, РІС…РѕРґРёС‚ Рё РїРёС€РµС‚ СЃРЅРёРјРєРё. РЎР°РјРѕ СЃР±СЂР°СЃС‹РІР°РµС‚СЃСЏ РІ false");
            // v0.9.10: live tuning of the district's concrete (no rebuild) вЂ” see SovietDistrict.Stone()
            SovietDistrict.GrainScale = Config.Bind("District", "GrainScale", 0.35f,
                "РџР»РѕС‚РЅРѕСЃС‚СЊ С‚РµРєСЃС‚СѓСЂС‹ Р±РµС‚РѕРЅР° СЂР°Р№РѕРЅР° РІ РїРѕРІС‚РѕСЂР°С… РЅР° РјРµС‚СЂ (0.35 = Р·РµСЂРЅРѕ РІРґРІРѕРµ РєСЂСѓРїРЅРµРµ, С‡РµРј 0.7). 1 = РѕРґРЅРѕ Р·РµСЂРЅРѕ РЅР° 1 Рј").Value;
            SovietDistrict.GrainCloud = Config.Bind("District", "GrainCloud", 0.15f,
                "РЎРєРѕР»СЊРєРѕ РѕСЃС‚Р°РІРёС‚СЊ РѕС‚ РєСЂСѓРїРЅС‹С… РїСЏС‚РµРЅ С‚РµРєСЃС‚СѓСЂС‹ (0 = С‚РѕР»СЊРєРѕ Р·РµСЂРЅРѕ/РїРѕСЂС‹, 1 = СЃС‹СЂРѕРµ С„РѕС‚Рѕ)").Value;
            SovietDistrict.GrainGain = Config.Bind("District", "GrainGain", 1f,
                "РљРѕРЅС‚СЂР°СЃС‚ Р·РµСЂРЅР°: 1 = РєР°Рє РЅР° С„РѕС‚Рѕ Рё РЅР° РґР°РјР±Рµ, Р±РѕР»СЊС€Рµ = РіСЂСѓР±РµРµ").Value;
            SovietDistrict.GrainTone = Config.Bind("District", "GrainTone", 1.68f,
                "РЇСЂРєРѕСЃС‚СЊ Р±РµС‚РѕРЅР° СЂР°Р№РѕРЅР° (1.68 = +40 % Рє РїСЂРµР¶РЅРёРј 1.2)").Value;
            SovietDistrict.GrainDesat = Config.Bind("District", "GrainDesat", 0.8f,
                "РќР°СЃРєРѕР»СЊРєРѕ СѓР±СЂР°С‚СЊ Р·РµР»РµРЅРѕРІР°С‚С‹Р№ РѕС‚С‚РµРЅРѕРє С„РѕС‚Рѕ (1 = С‡РёСЃС‚С‹Р№ СЃРµСЂС‹Р№)").Value;
            // v0.9.13: the platform's own floors are grainier than the houses' walls (the user: +400 %)
            SovietDistrict.PlatformGrain = Config.Bind("District", "PlatformGrain", 2f,
                "Р’Рѕ СЃРєРѕР»СЊРєРѕ СЂР°Р· РїР»РѕС‚РЅРµРµ Р·РµСЂРЅРѕ РЅР° РіРѕСЂРёР·РѕРЅС‚Р°Р»СЊРЅС‹С… РїРѕРІРµСЂС…РЅРѕСЃС‚СЏС… РїР»Р°С‚С„РѕСЂРјС‹ (РїР»РёС‚Р°, С‚РµСЂСЂР°СЃС‹, СѓР»РёС†С‹, РїР°РЅРґСѓСЃС‹, Р»РµСЃС‚РЅРёС†С‹, СЌСЃРїР»Р°РЅР°РґР°) РїРѕ СЃСЂР°РІРЅРµРЅРёСЋ СЃРѕ СЃС‚РµРЅР°РјРё РґРѕРјРѕРІ. 2 = РєР°Рє РјРѕСЃС‚РѕРІР°СЏ РѕСЃРЅРѕРІРЅРѕРіРѕ Р‘Р°Р±РґРё (0,7 РїРѕРІС‚РѕСЂР°/Рј)").Value;
            // v0.9.70: plan points 1-2 вЂ” the district wears the *main city's* own surface
            SovietDistrict.MatchCity = Config.Bind("District", "MatchCity", true,
                "Р‘СЂР°С‚СЊ С‚РµРєСЃС‚СѓСЂСѓ РѕСЃРЅРѕРІРЅРѕРіРѕ Р‘Р°Р±РґРё РєР°Рє РµСЃС‚СЊ: СЃС‹СЂРѕР№ M_ConcreteRough00 РґР»СЏ РІСЃРµС… РґРѕРјРѕРІ Рё Р·РµРјР»Рё, РѕРґРёРЅ С‚РѕРЅ; false = РїСЂРµР¶РЅРµРµ РѕСЃРІРµС‚Р»С‘РЅРЅРѕРµ Р·РµСЂРЅРѕ").Value;
            // v0.9.20: the south-east corner of the +5 m east terrace is cut back to the base deck
            SovietDistrict.CornerApron = Config.Bind("District", "CornerApron", 12f,                "РќР° СЃРєРѕР»СЊРєРѕ РјРµС‚СЂРѕРІ РІРѕСЃС‚РѕС‡РЅР°СЏ С‚РµСЂСЂР°СЃР° (+5 Рј) РѕС‚РѕРґРІРёРЅСѓС‚Р° РѕС‚ РіСЂРµР±РЅСЏ РЅР° СЋРіРѕ-РІРѕСЃС‚РѕС‡РЅРѕРј СѓРіР»Сѓ: С‚Р°Рј, Сѓ РєСЂСѓРіР»РѕР№ РґС‹СЂС‹ РІ РјРѕСЂРµ, С‚РµРїРµСЂСЊ СЂРѕРІРЅС‹Р№ РЅР°СЃС‚РёР» РЅР° СѓСЂРѕРІРЅРµ РіСЂРµР±РЅСЏ, Р° РЅРµ РіРѕР»Р°СЏ 5-РјРµС‚СЂРѕРІР°СЏ РїР»РёС‚Р° (0 = РєР°Рє СЂР°РЅСЊС€Рµ)").Value;
            // v0.9.44: the user asked to remove the low plate that used to bridge the corner up to the
            // granite crest вЂ” the corner is the plain deck again
            SovietDistrict.CornerPadOn = Config.Bind("District", "CornerPad", true,
                "РџР»РёС‚Р° 48 x 12 Рј РЅР° СЋРіРѕ-РІРѕСЃС‚РѕС‡РЅРѕРј СѓРіР»Сѓ: Рє РіСЂР°РЅРёС‚Сѓ РіСЂРµР±РЅСЏ РІРµРґС‘С‚ РїРѕР»РѕРіРёР№ РІСЉРµР·Рґ 6 Рј (Р±РµР· СЃС‚СѓРїРµРЅСЊРєРё). false = СѓРіРѕР» РІСЂРѕРІРµРЅСЊ СЃ РЅР°СЃС‚РёР»РѕРј").Value;
            // v0.9.14: the red-glowing memorial on the roof of the second-tallest block
            SovietDistrict.MemorialOn = Config.Bind("District", "Memorial", true,
                "РњРµРјРѕСЂРёР°Р» СЃ РєСЂР°СЃРЅС‹Рј СЃРІРµС‡РµРЅРёРµРј РЅР° РєСЂС‹С€Рµ РІС‚РѕСЂРѕРіРѕ РїРѕ РІС‹СЃРѕС‚Рµ Р·РґР°РЅРёСЏ СЂР°Р№РѕРЅР°").Value;
            // v0.9.15: brightness of that glow вЂ” 1 = the v0.9.14 level, 0.5 = half (the user's request)
            SovietDistrict.MemorialGlow = Config.Bind("District", "MemorialGlow", 0.5f,
                "РЇСЂРєРѕСЃС‚СЊ СЃРІРµС‡РµРЅРёСЏ РјРµРјРѕСЂРёР°Р»Р°: 1 = РєР°Рє РІ v0.9.14, 0.5 = РІРґРІРѕРµ СЃР»Р°Р±РµРµ (Рё СЌРјРёСЃСЃРёСЏ, Рё РєСЂР°СЃРЅС‹Рµ РёСЃС‚РѕС‡РЅРёРєРё)").Value;
            SovietDistrict.Enabled = Config.Bind("District", "Enabled", true,
                "РЎС‚СЂРѕРёС‚СЊ СЃРѕРІРµС‚СЃРєРёР№ СЂР°Р№РѕРЅ Р·Р° РґР°РјР±РѕР№ РїСЂРё Р·Р°РіСЂСѓР·РєРµ СѓСЂРѕРІРЅСЏ").Value;
            // v0.9.18: the penthouse (clock tower) on the roof of the block on the peninsula
            SovietDistrict.PenthouseOn = Config.Bind("District", "Penthouse", true,
                "РџРµРЅС‚С…Р°СѓСЃ СЃ С‡Р°СЃР°РјРё РЅР° РєСЂС‹С€Рµ РѕС‚РґРµР»СЊРЅРѕ СЃС‚РѕСЏС‰РµР№ Р±Р°С€РЅРё РЅР° Р·Р°РїР°РґРЅРѕРј РєСЂС‹Р»Рµ (РїРѕР»СѓРѕСЃС‚СЂРѕРІРµ)").Value;
            SovietDistrict.Density = Config.Bind("District", "Density", 1f,
                "РџР»РѕС‚РЅРѕСЃС‚СЊ Р·Р°СЃС‚СЂРѕР№РєРё: 1 = РєР°Рє Р·Р°РґСѓРјР°РЅРѕ, 0.5 = РІРґРІРѕРµ СЂРµР¶Рµ, 1.5 = РїР»РѕС‚РЅРµРµ").Value;
            // v0.9.77: the map itself вЂ” bigger footprint, higher sky, no invisible ceiling
            MapEdits.Enabled = Config.Bind("Map", "Expand", true,
                "РџСЂР°РІРєР° РєР°СЂС‚С‹ Р‘Р°Р±РґРё: РїР»РѕС‰Р°РґСЊ РІ 2 СЂР°Р·Р° РІРѕ РІСЃРµ СЃС‚РѕСЂРѕРЅС‹, РЅРµРІРёРґРёРјС‹Р№ РїРѕС‚РѕР»РѕРє СѓР±СЂР°РЅ, РЅРµР±Рѕ РїРѕРґРЅСЏС‚Рѕ РІ 2 СЂР°Р·Р°, РёР· РЅРµРІРёРґРёРјС‹С… Р±Р°СЂСЊРµСЂРѕРІ РѕСЃС‚Р°СЋС‚СЃСЏ С‚РѕР»СЊРєРѕ РіСЂР°РЅРёС†С‹ РєР°СЂС‚С‹").Value;
            MapEdits.AreaScale = Config.Bind("Map", "AreaScale", 2f,
                "Р’Рѕ СЃРєРѕР»СЊРєРѕ СЂР°Р· СЂР°Р·РґРІРёРЅСѓС‚СЊ РіСЂР°РЅРёС†С‹ РєР°СЂС‚С‹ РѕС‚ РµС‘ С†РµРЅС‚СЂР° (2 = РїР»РѕС‰Р°РґСЊ РІ 2 СЂР°Р·Р° РІ РєР°Р¶РґСѓСЋ СЃС‚РѕСЂРѕРЅСѓ)").Value;
            MapEdits.SkyScale = Config.Bind("Map", "SkyHeight", 2f,
                "Р’Рѕ СЃРєРѕР»СЊРєРѕ СЂР°Р· РїРѕРґРЅСЏС‚СЊ СЃР»РѕР№ РѕР±Р»Р°РєРѕРІ/РЅРµР±Р° РЅР°Рґ РіРѕСЂРѕРґРѕРј (2 = РІС‹СЃРѕС‚Р° СЃРєР°Р№Р±РѕРєСЃР° РІ 2 СЂР°Р·Р°)").Value;
            MapEdits.RemoveInner = Config.Bind("Map", "RemoveInnerBarriers", true,
                "РЈР±СЂР°С‚СЊ РІСЃРµ РЅРµРІРёРґРёРјС‹Рµ Р±Р°СЂСЊРµСЂС‹ РІРЅСѓС‚СЂРё РєР°СЂС‚С‹ (РґРІРµСЂРё, Р·Р°Р±РѕСЂС‹, РјРµР»РєРёРµ РєСѓР±С‹), РѕСЃС‚Р°РІРёРІ С‚РѕР»СЊРєРѕ РіСЂР°РЅРёС†С‹ РєР°СЂС‚С‹ Рё KILLZ").Value;
            loadSceneCfg = Config.Bind("Debug", "LoadScene", "",
                "РўРѕР»СЊРєРѕ РґР»СЏ РѕС‚Р»Р°РґРєРё: СЃСЂР°Р·Сѓ Р·Р°РіСЂСѓР·РёС‚СЊ СЌС‚Сѓ СЃС†РµРЅСѓ РїСЂРё СЃС‚Р°СЂС‚Рµ (РЅР°РїСЂРёРјРµСЂ Scene_A), С‡С‚РѕР±С‹ РЅРµ РїСЂРѕС…РѕРґРёС‚СЊ РјРµРЅСЋ");
            // v0.9.15: the propeller ("Р»РµС‚Р°Р»РєР°") lifts only to a hard-coded 120 m, which is *below* the
            // district's own roofs (deck 85,5 m, the tallest roof в‰€276 m) вЂ” see PropellerFlyLimitPatch
            flyMaxHeight = Config.Bind("Game", "FlyMaxHeight", 600f,
                "РџРѕС‚РѕР»РѕРє РїРѕР»С‘С‚Р° РЅР° РїСЂРѕРїРµР»Р»РµСЂРµ РІ РјРµС‚СЂР°С… (РІ РёРіСЂРµ Р±С‹Р»Рѕ Р¶С‘СЃС‚РєРѕ 120 вЂ” СЌС‚Рѕ РЅРёР¶Рµ РєСЂС‹С€ СЂР°Р№РѕРЅР°). 0 РёР»Рё РјРµРЅСЊС€Рµ = РЅРµ РјРµРЅСЏС‚СЊ").Value;
            Net.Trace = msg => Log.LogInfo($"[Net] {msg}");
            // v0.9.15: game patches (Harmony) вЂ” applied once, after the config is bound
            try
            {
                if (flyMaxHeight > 0f)
                {
                    harmony = new Harmony("com.babbdi.coop");
                    var m = AccessTools.Method(typeof(Propeller), "Update");
                    if (m == null)
                        Log.LogError("[FLY] Propeller.Update not found вЂ” the flight ceiling stays at 120 m");
                    else
                        harmony.Patch(m, transpiler: new HarmonyMethod(
                            AccessTools.Method(typeof(BabbdiCoopPlugin), nameof(FlyCeilingTranspiler))));
                }
            }
            catch (Exception e)
            {
                Log.LogError($"[FLY] patch failed: {e.Message} вЂ” the flight ceiling stays at 120 m");
            }
            Log.LogInfo($"{PluginName} v{PluginVersion} loaded. F5=С…РѕСЃС‚ F6=РєР»РёРµРЅС‚(ip:{joinIp.Value}) F7=СЃС‚РѕРї РїРѕСЂС‚={port.Value}");
        }

        private static Harmony harmony;
        private static float flyMaxHeight;

        /// <summary>
        /// v0.9.15 вЂ” the whole flight ceiling. `Propeller.Update` compares the player's world y against a
        /// hard-coded **120 m** three times: once for the Space lift, twice for the Fire1 forward thrust.
        /// The count is logged, so a game update that changes the constant shows up as `0 replaced`
        /// instead of silently leaving the old ceiling.
        /// </summary>
        private static IEnumerable<CodeInstruction> FlyCeilingTranspiler(IEnumerable<CodeInstruction> code)
        {
            int n = 0;
            var list = new List<CodeInstruction>();
            foreach (var ci in code)
            {
                if (ci.opcode == OpCodes.Ldc_R4 && ci.operand is float f && Mathf.Abs(f - 120f) < 0.01f)
                {
                    n++;
                    list.Add(new CodeInstruction(OpCodes.Ldc_R4, flyMaxHeight));
                }
                else list.Add(ci);
            }
            Log.LogInfo($"[FLY] propeller ceiling 120 m -> {flyMaxHeight:0} m ({n} constants replaced in Propeller.Update)");
            return list;
        }

        /// <summary>
        /// The camera's far plane has to reach further than the new ceiling, or flying at 600 m would clip
        /// the world away. Idempotent and cheap (one comparison per frame), so it also survives a scene
        /// change re-creating the camera.
        /// </summary>
        private static void TickCameraFar()
        {
            var cam = Camera.main;
            if (cam == null || flyMaxHeight <= 0f) return;
            float want = Mathf.Max(flyMaxHeight + 800f, MapEdits.SkyFar);
            if (cam.farClipPlane >= want) return;
            Log.LogInfo($"[FLY] camera far clip {cam.farClipPlane:0} -> {want:0} m (fog still hides the far edge)");
            cam.farClipPlane = want;
        }

        private void Update()
        {
            try
            {
                TickPreview();
                TickWorldScan();
                TickMapShot();
                TickCameraFar();
                EnsureMiniGame();
                SimTick();
                HandleHotkeys();
                if (miniGame == null || !miniGame.Active)
                {
                    TrackUseEdge();
                    TrackTricks();
                }
                SendOwnPose();
                PumpIncoming();
                AnimateRemote();
                AnimateExtra();
                SimLog();
                FlushTrace();
                UpdateItemOverlay();
            }
            catch (Exception e)
            {
                Log.LogError($"Update error: {e}");
            }
        }

        /// <summary>
        /// Draws the LOCAL player's first-person held item on top of everything, the way
        /// games render a viewmodel: the prop hangs right in front of the camera and used
        /// to poke through walls. A camera command buffer at AfterEverything wipes depth
        /// and re-draws the item's renderers with the game's own materials, so lighting
        /// and textures are unchanged. (The partner's avatar is NOT affected вЂ” third-person
        /// props stay depth-tested.)
        /// </summary>
        private void UpdateItemOverlay()
        {
            // v0.10.0: no first-person viewmodel on the detached top-down camera
            if (miniGame != null && miniGame.Active)
            {
                if (itemOverlayCam != null && itemOverlayCb != null)
                {
                    try { itemOverlayCam.RemoveCommandBuffer(CameraEvent.AfterEverything, itemOverlayCb); }
                    catch { }
                    itemOverlayCb = null;
                    itemOverlayCam = null;
                }
                return;
            }

            var cam = Camera.main;
            if (cam == null)
            {
                var fpc = LocalPlayer();
                if (fpc != null) cam = fpc.GetComponentInChildren<Camera>();
            }

            if (cam != itemOverlayCam)
            {
                if (itemOverlayCam != null && itemOverlayCb != null)
                    itemOverlayCam.RemoveCommandBuffer(CameraEvent.AfterEverything, itemOverlayCb);
                itemOverlayCb = null;
                itemOverlayCam = cam;
                if (cam != null)
                {
                    itemOverlayCb = new CommandBuffer { name = "BabbdiCoop.ItemOverlay" };
                    cam.AddCommandBuffer(CameraEvent.AfterEverything, itemOverlayCb);
                }
            }
            if (itemOverlayCb == null) return;

            itemOverlayCb.Clear();
            if (cam == null) return;

            // held items are direct children of the player camera + InteractObject.inHands
            overlayItems.Clear();
            foreach (Transform child in cam.transform)
            {
                if (child == null) continue;
                var io = child.GetComponent<InteractObject>();
                if (io == null || !io.inHands) continue;
                foreach (var r in child.GetComponentsInChildren<Renderer>())
                    if (r != null && r.enabled) overlayItems.Add(r);
            }
            if (overlayItems.Count == 0) return;

            // depth wipe: walls/props can no longer cut into the viewmodel
            itemOverlayCb.ClearRenderTarget(true, false, Color.black);

            foreach (var r in overlayItems)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null) itemOverlayCb.DrawRenderer(r, mats[i], i);
            }
        }

        /// <summary>
        /// Fire1 must be sampled EVERY frame: GetButtonDown is true for exactly one
        /// frame, and SendOwnPose runs only at ~30 Hz, so checking it there dropped
        /// about two out of three presses ("СѓРґР°СЂ СЂР°Р±РѕС‚Р°РµС‚ С‡РµСЂРµР· СЂР°Р·").
        /// </summary>
        private void TrackUseEdge()
        {
            if (net.role == NetRole.None) return;
            if (Input.GetButtonDown("Fire1")) usePulseUntil = Time.time + 0.25f;

            // debounce "airborne": CharacterController.isGrounded flickers on steps and
            // slopes, which made the jump pose pop in and out (visible twitching)
            var fpc = LocalPlayer();
            var cc = fpc != null ? fpc.GetComponent<CharacterController>() : null;
            if (cc == null) { airTimer = 0f; airState = false; return; }
            if (!cc.isGrounded)
            {
                airTimer += Time.deltaTime;
                if (airTimer > 0.12f) airState = true;
            }
            else
            {
                airTimer = 0f;
                airState = false;
            }
        }

        /// <summary>
        /// v0.8.0 tricks. B = toggle handstand, V = backflip. Sampled every frame like
        /// Fire1 (SendOwnPose runs at ~30 Hz and would drop presses), then latched for a
        /// while so at least one packet carries the bit even if the click lands between
        /// sends. Both are pure visuals on the sender's side (no CharacterController
        /// changes) вЂ” the game's own movement stays untouched.
        /// </summary>
        private void TrackTricks()
        {
            if (Input.GetKeyDown(KeyCode.B))
            {
                handstandOn = !handstandOn;
                Toast(handstandOn ? "РЎС‚РѕР№РєР° РЅР° СЂСѓРєР°С…" : "РЎС‚РѕР№РєР° РЅР° СЂСѓРєР°С…: РІС‹РєР»", 1.5f);
                Log.LogInfo($"[COOP] handstand = {handstandOn}");
            }
            if (Input.GetKeyDown(KeyCode.V) && flipStart < 0f)
            {
                flipStart = Time.time;
                Toast("РЎР°Р»СЊС‚Рѕ РЅР°Р·Р°Рґ", 1.2f);
                Log.LogInfo("[COOP] backflip");
            }
            if (flipStart >= 0f && Time.time - flipStart > FlipCamDuration) flipStart = -1f;
        }

        /// <summary>
        /// Local first-person view of the tricks, applied in LateUpdate (after the game's
        /// own mouse look wrote the camera rotation this frame, so our rotation stacks on
        /// top instead of being overwritten):
        ///   handstand = 180В° pitch forward about the view right axis (the body pitches over
        ///   its head, the world turns upside down);
        ///   backflip  = -360В° pitch about the view right axis в†’ the world flips backwards.
        /// The game keeps its own yaw/pitch state untouched, so releasing the key returns
        /// to a normal view instantly.
        /// </summary>
        private void ApplyTrickCamera()
        {
            // v0.8.1: the local view follows the same timeline as the partner's body вЂ”
            // the roll starts after the load and takes HsEnter, and the flip spins with
            // the same curve as the avatar, so both players experience one motion.
            float hsTarget = handstandOn ? 1f : 0f;
            float hsSpeed = handstandOn ? 1f / GlbAnimator.HsEnter : 1f / GlbAnimator.HsExit;
            handstandCamBlend = Mathf.MoveTowards(handstandCamBlend, hsTarget, Time.deltaTime * hsSpeed);
            if (handstandCamBlend < 0.002f && flipStart < 0f) return;

            var fpc = LocalPlayer();
            if (fpc == null) return;
            var cam = fpc.GetComponentInChildren<Camera>();
            if (cam == null) return;
            var t = cam.transform;

            if (handstandCamBlend > 0.002f)
            {
                // the pitch begins once the load is over, exactly like the body: the partner
                // tips forward over the head, so the local view pitches forward about the
                // player's own right axis (a roll about forward was the cartwheel look)
                float e = handstandCamBlend * handstandCamBlend * (3f - 2f * handstandCamBlend);
                float roll = Mathf.Clamp01((e - 0.12f) / 0.80f);
                if (roll > 0f) t.rotation = Quaternion.AngleAxis(180f * roll, t.right) * t.rotation;
            }
            if (flipStart >= 0f)
            {
                float p = Mathf.Clamp01((Time.time - flipStart - GlbAnimator.FlipIn) / GlbAnimator.FlipAir);
                // re-read the axes: the handstand roll above already changed them
                t.rotation = Quaternion.AngleAxis(-360f * GlbAnimator.RotationCurve(p), t.right) * t.rotation;
            }
        }

        // ---------- debug: offline visual check of the avatar ----------
        // Enable Debug.PreviewAvatar in BepInEx/config/com.babbdi.coop.cfg, launch
        // the game, and look at BepInEx/plugins/BabbdiCoop/preview/*.png. No network
        // is needed: the avatar is spawned in front of the current camera and a short
        // scripted timeline (idle в†’ walk в†’ crouch в†’ air в†’ run в†’ swing) is played.

        private bool previewStarted;
        private float previewT0, previewNextShot;
        private int previewShotIdx;
        private Camera studioCam;
        private RenderTexture studioRT;
        private GameObject previewFloor;
        private bool previewItemLogged;

        // ---------- debug: one-shot survey of the level (planning aid for the district) ----------
        private void TickWorldScan()
        {
            // the Soviet district appears once the world is up: it probes the dam crest under the
            // junction, so it builds in the level and stays out of the menu
            if (SovietDistrict.Enabled) SovietDistrict.TryBuild();
            MapEdits.TryApply(Log);   // v0.9.77: bigger map, higher sky, no invisible ceiling

            // debug: skip the menu so the scan/preview can run headless
            if (!loadSceneDone && loadSceneCfg != null && !string.IsNullOrEmpty(loadSceneCfg.Value) && Time.time > 2f)
            {
                loadSceneDone = true;
                try
                {
                    Log.LogInfo($"[SCAN] loading scene '{loadSceneCfg.Value}'");
                    UnityEngine.SceneManagement.SceneManager.LoadScene(loadSceneCfg.Value);
                }
                catch (Exception e) { Log.LogError($"[SCAN] LoadScene failed: {e}"); }
                return;
            }

            // v0.9.77: one-shot scan of the skybox / invisible boundaries (the map-expansion data)
            if (!mapScanDone && mapScanCfg != null && mapScanCfg.Value &&
                Time.time > 4f && LocalPlayer() != null)
            {
                mapScanDone = true;
                mapScanCfg.Value = false;
                try { Log.LogInfo($"[MAPSCAN] {WorldScan.MapScan(PluginDir)}"); }
                catch (Exception e) { Log.LogError($"[MAPSCAN] failed: {e}"); }
            }

            bool forced = Input.GetKeyDown(KeyCode.F9);
            if (!forced)
            {
                if (worldScanDone || worldScanCfg == null || !worldScanCfg.Value) return;
                if (Time.time < 4f || LocalPlayer() == null) return; // still in the menu / loading
            }
            worldScanDone = true;
            if (worldScanCfg != null) worldScanCfg.Value = false;
            try
            {
                Log.LogInfo($"[SCAN] {WorldScan.Run(PluginDir)}");
            }
            catch (Exception e)
            {
                Log.LogError($"[SCAN] failed: {e}");
            }
        }
        // ---------- debug: a few stills of the level (planning aid for the district) ----------
        private void TickMapShot()
        {
            if (mapShotDone || mapShotCfg == null || !mapShotCfg.Value) return;
            if (Time.time < 8f) return;
            mapShotDone = true;
            mapShotCfg.Value = false;
            try
            {
                var dir = System.IO.Path.Combine(PluginDir, "map");
                System.IO.Directory.CreateDirectory(dir);
                Log.LogInfo("[SCAN] map shots: start");
                Shot(dir, "top", new Vector3(0f, 500f, 150f), Quaternion.Euler(90f, 0f, 0f), 480f, 1024);
                Shot(dir, "dam_east", new Vector3(-160f, 10f, 120f), LookAt(new Vector3(-160f, 10f, 120f), new Vector3(300f, -40f, 250f)), 0f, 1024);
                Shot(dir, "dam_wide", new Vector3(-300f, 60f, -60f), LookAt(new Vector3(-300f, 60f, -60f), new Vector3(200f, -30f, 300f)), 0f, 1024);
                if (SovietDistrict.Built)
                {
                    // v0.9.10: the concrete of the district vs the dam's own concrete, same ruler
                    foreach (var s in SovietDistrict.GrainShots())
                        Shot(dir, s.Name, s.Pos, LookAt(s.Pos, s.Look), s.Ortho, 1024);

                    int n = 0;
                    foreach (var s in SovietDistrict.PreviewPoses())
                    {
                        if (s.RampState > 0) SovietDistrict.SetRampState(s.RampState);
                        Log.LogInfo($"[SCAN] district_{n}: pos {s.Pos} look {s.Look} ortho {s.Ortho}");
                        Shot(dir, $"district_{n++}", s.Pos, LookAt(s.Pos, s.Look), s.Ortho, 1024, s.Lamp);
                    }
                    Vector3 tp; Quaternion tr; float tortho;
                    SovietDistrict.TopView(out tp, out tr, out tortho);
                    Shot(dir, "district_top", tp, tr, tortho, 1024);
                    SovietDistrict.SetShadows(false);
                    Shot(dir, "district_top_ns", tp, tr, tortho, 1024);
                    SovietDistrict.SetShadows(true);
                    SovietDistrict.ProbeWalkability();
                    var poses = SovietDistrict.PreviewPoses();
                    if (poses.Count > 3)
                    {
                        SovietDistrict.SetShadows(false);
                        Shot(dir, "district_noshadow", poses[3].Pos, LookAt(poses[3].Pos, poses[3].Look), 0f, 1024);
                        SovietDistrict.SetShadows(true);
                    }
                }
                Log.LogInfo($"[SCAN] map shots written to {dir}");
            }
            catch (Exception e)
            {
                Log.LogError($"[SCAN] map shots failed: {e}");
            }
        }

        private static Quaternion LookAt(Vector3 from, Vector3 to)
            => Quaternion.LookRotation((to - from).normalized, Vector3.up);

        private static void Shot(string dir, string name, Vector3 pos, Quaternion rot, float ortho, int size,
                                 bool lamp = false)
        {
            Log.LogInfo("[SCAN] shot " + name);
            var go = new GameObject("CoopMapCam");
            var cam = go.AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(pos, rot);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.07f, 0.10f, 1f);
            cam.orthographic = ortho > 0f;
            cam.orthographicSize = ortho;
            cam.farClipPlane = 5000f;
            cam.nearClipPlane = 0.1f;
            cam.enabled = false;
            var main = Camera.main;
            if (main != null) cam.cullingMask = main.cullingMask;   // show exactly what the player sees
            // v0.9.12: debug-only sun for the stills that would otherwise be black вЂ” the level has no sun
            // and ambient is 0, so a facade in shadow has no readable evidence at all
            GameObject lampGo = null;
            if (lamp)
            {
                lampGo = new GameObject("CoopMapLamp");
                var li = lampGo.AddComponent<Light>();
                li.type = LightType.Directional;
                li.color = new Color(1f, 0.98f, 0.94f);
                li.intensity = 1.15f;
                li.shadows = LightShadows.None;
                // light coming over the camera's shoulder, 35 deg above the view direction
                lampGo.transform.rotation = Quaternion.Euler(35f, rot.eulerAngles.y + 25f, 0f);
            }
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                tex.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            }
            finally
            {
                RenderSettings.fog = fog;
                RenderTexture.active = null;
                cam.targetTexture = null;
                UnityEngine.Object.Destroy(rt);
                UnityEngine.Object.Destroy(tex);
                UnityEngine.Object.Destroy(go);
                if (lampGo != null) UnityEngine.Object.Destroy(lampGo);
            }
        }

        private void TickPreview()
        {
            if (previewCfg == null || !previewCfg.Value) return;

            if (!previewStarted)
            {
                if (Time.frameCount < 90) return; // let the scene finish loading
                var cam = Camera.main;
                if (cam == null)
                {
                    var cams = Camera.allCameras;
                    if (cams.Length > 0) cam = cams[0];
                }
                if (cam == null) return;

                var pos = cam.transform.position + cam.transform.forward * 2.4f;
                pos.y = cam.transform.position.y - 1.3f;
                previewStarted = true;
                previewT0 = Time.time;
                previewNextShot = 0f;

                // Reference floor: its top face is exactly at the "feet" point of the pose.
                // The collider must stay: the ground snap uses it.
                previewFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                previewFloor.name = "COOP_PreviewFloor";
                previewFloor.transform.position = new Vector3(pos.x, pos.y - 0.05f, pos.z);
                previewFloor.transform.localScale = new Vector3(6f, 0.1f, 6f);
                var floorRend = previewFloor.GetComponent<Renderer>();
                if (floorRend != null && floorRend.material != null)
                    floorRend.material.color = new Color(0.42f, 0.44f, 0.48f);

                // deliberately spawn 12 cm too high: GroundSnap has to pull the avatar down
                // onto the floor, which is exactly what the in-game float looked like
                var rawPos = pos + Vector3.up * 0.12f;
                Physics.SyncTransforms();   // the just-created cube is not in the physics world yet
                var spawn = GroundSnap(rawPos, false);
                bool probe = Physics.Raycast(rawPos + Vector3.up * 0.45f, Vector3.down, out var probeHit, 1.1f, ~0,
                    QueryTriggerInteraction.Ignore);
                Log.LogInfo($"[PREVIEW] probe hit={probe} col={(probe && probeHit.collider != null ? probeHit.collider.name : "-")} " +
                            $"hitY={(probe ? probeHit.point.y : 0f):F3} | spawn raw y={rawPos.y:F3} snapped y={spawn.y:F3} (floor top {pos.y:F3})");

                SpawnRemote(spawn);
                remoteTargetPos = spawn;
                remoteTargetYaw = cam.transform.eulerAngles.y + 180f;
                remoteGo.transform.position = spawn;
                remoteGo.transform.rotation = Quaternion.Euler(0f, remoteTargetYaw, 0f);
                PreviewTestItem();

                // studio camera + lamp: renders the avatar into a RenderTexture so the
                // screenshots are close-ups, independent of the menu lighting
                studioRT = new RenderTexture(480, 480, 24);
                var camGo = new GameObject("COOP_StudioCam");
                studioCam = camGo.AddComponent<Camera>();
                studioCam.targetTexture = studioRT;
                studioCam.clearFlags = CameraClearFlags.SolidColor;
                studioCam.backgroundColor = new Color(0.16f, 0.17f, 0.20f);
                studioCam.fieldOfView = 32f;
                studioCam.nearClipPlane = 0.05f;
                studioCam.farClipPlane = 40f;
                studioCam.enabled = false;
                GlbAnimator.Verbose = true;

                var lampGo = new GameObject("COOP_StudioLight");
                var lamp = lampGo.AddComponent<Light>();
                lamp.type = LightType.Directional;
                lamp.intensity = 1.5f;
                lamp.color = Color.white;
                lampGo.transform.rotation = Quaternion.Euler(35f, cam.transform.eulerAngles.y + 160f, 0f);

                Log.LogInfo($"[PREVIEW] avatar at {pos}, camera {cam.transform.position} yaw {remoteTargetYaw:F0}");
                return;
            }

            float t = Time.time - previewT0;

            bool crouch = t > 4.2f && t < 6.2f;
            bool air = t > 6.2f && t < 7.0f;

            // walk fwd в†’ crouch в†’ air в†’ run fwd в†’ run BACK в†’ strafe в†’ repeated item push
            float speed = 0f;
            Vector3 vel = Vector3.zero;
            if (t > 1.0f && t < 4.2f) { speed = 1.2f; vel = new Vector3(0f, 0f, 1.2f); }
            else if (t > 7.0f && t < 8.4f) { speed = 3.6f; vel = new Vector3(0f, 0f, 3.6f); }
            else if (t > 8.4f && t < 9.4f) { speed = 3.6f; vel = new Vector3(0f, 0f, -3.6f); }
            else if (t > 9.4f && t < 10.2f) { speed = 1.4f; vel = new Vector3(1.4f, 0f, 0f); }

            // the push is ~0.1 s now and shots are 0.12 s apart, so fire it repeatedly
            bool use = t > 10.2f && t < 11.2f && Mathf.Repeat(t, 0.2f) < 0.1f;

            // v0.8.1 tricks: the handstand is a held state with a ~1 s entry (load в†’ push)
            // and a ~0.55 s exit, the flip is a 1.08 s load в†’ air в†’ landing
            bool handstand = t > 11.2f && t < 14.0f;
            bool flip = t > 14.6f && t < 15.9f;

            // feed the normal pipeline (AnimateRemote applies these to the animator)
            remoteSpeed = speed;
            remoteVelLocal = vel;
            remoteFlags = (crouch ? Net.FlagCrouch : 0) | (air ? Net.FlagAir : 0) | (use ? Net.FlagUse : 0)
                        | (handstand ? Net.FlagHandstand : 0) | (flip ? Net.FlagFlip : 0);

            // pitch test (v0.8.0, task 3): the whole torso must lean with the camera view,
            // otherwise the head is the only thing that moves on a "statue"
            remotePitchTarget = (t > 2.2f && t < 3.4f) ? -38f
                              : (t > 3.4f && t < 4.2f) ? 42f
                              : ((t > 7.4f && t < 8.4f) ? 30f : 0f);

            if (!previewItemLogged && t > 0.8f)
            {
                previewItemLogged = true;
                var hand = FindDeep(remoteGo.transform, "HandAnchor");
                var itemT = FindDeep(remoteGo.transform, "COOP_TestBat");
                var item = itemT != null ? itemT.gameObject : remoteHeldItem;
                if (hand != null && item != null)
                {
                    var rends = item.GetComponentsInChildren<Renderer>();
                    var b = new Bounds(item.transform.position, Vector3.zero);
                    if (rends.Length > 0)
                    {
                        b = rends[0].bounds;
                        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                    }
                    Vector3 closest = b.ClosestPoint(hand.position);
                    Log.LogInfo($"[PREVIEW] palm pos={hand.position} fwd={hand.forward} | item local={item.transform.localPosition} scale={item.transform.localScale.x:F3} worldCenter={b.center} size={b.size}");
                    Log.LogInfo($"[PREVIEW] item bottom={b.min.y:F3} hand={hand.position.y:F3} avatar={remoteGo.transform.position.y:F3}, " +
                                $"closest item point {Vector3.Distance(closest, hand.position) * 100f:F1} cm from the palm");
                }
            }

            if (t > 16.8f)
            {
                Log.LogInfo($"[PREVIEW] done, {previewShotIdx} screenshots in {System.IO.Path.Combine(PluginDir, "preview")}");
                if (previewFloor != null) Destroy(previewFloor);
                previewCfg.Value = false;
            }
        }

        /// <summary>Studio capture runs in LateUpdate so the skeleton pose is final.</summary>
        private void LateUpdate()
        {
            // v0.8.0: the local view of the tricks has to be applied here вЂ” LateUpdate runs
            // after the game's own mouse look, and the game rewrites the camera rotation
            // every frame from its internal yaw/pitch state, so anything set earlier is lost.
            try { ApplyTrickCamera(); }
            catch (Exception e) { Log.LogError($"trick camera error: {e}"); }

            if (!previewStarted || previewCfg == null || !previewCfg.Value) return;
            if (studioCam == null || remoteGo == null) return;

            float t = Time.time - previewT0;
            if (t < previewNextShot || previewShotIdx >= 152) return;
            previewNextShot = t + 0.12f;

            try
            {
                var target = remoteGo.transform.position + Vector3.up * 1.15f;
                // a 3/4 front view: avatar forward is +Z of remoteGo (yaw only)
                var dir = Quaternion.Euler(0f, -25f, 0f) * remoteGo.transform.forward;
                // wide enough that an inverted handstand (feet ~2.4 m up) still fits
                studioCam.transform.position = target + dir * 5.2f;
                studioCam.transform.LookAt(target);
                studioCam.Render();
                studioCam.Render(); // 2nd pass: the 1st one only refreshes the skinning

                var prev = RenderTexture.active;
                RenderTexture.active = studioRT;
                var tex = new Texture2D(studioRT.width, studioRT.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0f, 0f, studioRT.width, studioRT.height), 0, 0);
                tex.Apply();
                var dirOut = System.IO.Path.Combine(PluginDir, "preview");
                System.IO.Directory.CreateDirectory(dirOut);
                System.IO.File.WriteAllBytes(
                    System.IO.Path.Combine(dirOut, $"preview_{previewShotIdx++:000}.png"),
                    tex.EncodeToPNG());
                if (GlbAnimator.Verbose && previewShotIdx % 10 == 0)
                    Log.LogInfo($"[PREVIEW] shot {previewShotIdx - 1} at t={t:F2}");
                RenderTexture.active = prev;
                Destroy(tex);
            }
            catch (Exception e)
            {
                Log.LogWarning($"[PREVIEW] studio capture failed: {e.Message}");
            }
        }

        /// <summary>
        /// Debug only: a synthetic bat (thin handle + fat barrel) fitted exactly like a
        /// real picked-up prop, so the preview shows the grip end, orientation and size.
        /// </summary>
        private void PreviewTestItem()
        {
            var hand = FindDeep(remoteGo.transform, "HandAnchor");
            if (hand == null) return;

            var bat = new GameObject("COOP_TestBat");
            AddPart(bat.transform, new Vector3(0.05f, 0.05f, 0.50f), new Vector3(0f, 0f, 0.22f));  // barrel (thick)
            AddPart(bat.transform, new Vector3(0.026f, 0.026f, 0.42f), new Vector3(0f, 0f, -0.24f)); // handle (thin)
            bat.transform.SetParent(hand, false);
            FitHeldItemToHand(bat, hand);

            // Real scene props ship with Read/Write Disabled (mesh.vertices throws), which
            // is what kept the Club at full size. Exercise that path on a throwaway copy so
            // the log tells us which fallback is taken.
            var ro = new GameObject("COOP_TestBatReadOnly");
            AddPart(ro.transform, new Vector3(0.05f, 0.05f, 0.50f), new Vector3(0f, 0f, 0.22f));
            AddPart(ro.transform, new Vector3(0.026f, 0.026f, 0.42f), new Vector3(0f, 0f, -0.24f));
            ro.transform.SetParent(hand, false);
            // v0.8.0: a real world prop is NOT axis-aligned in the hand вЂ” it lies in the level
            // at some yaw/roll. Rotating the test clone is what exposes the old world-AABB
            // fallback (wrong long axis в†’ the pickaxe levitating beside the hand).
            ro.transform.localRotation = Quaternion.Euler(20f, 35f, 15f);
            foreach (var mf in ro.GetComponentsInChildren<MeshFilter>())
            {
                var copy = Instantiate(mf.sharedMesh);
                copy.UploadMeshData(true);            // в†’ isReadable = false
                mf.sharedMesh = copy;
            }
            // both fit calls would name-collide in the logs, so make it explicit
            Log.LogInfo("[PREVIEW] read-only clone: isReadable=" +
                        ro.GetComponentInChildren<MeshFilter>().sharedMesh.isReadable);
            FitHeldItemToHand(ro, hand);
            Destroy(ro);
        }

        private static void AddPart(Transform parent, Vector3 size, Vector3 localPos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
        }

        private static string PluginDir
        {
            get
            {
                var d = System.IO.Path.GetDirectoryName(typeof(BabbdiCoopPlugin).Assembly.Location);
                return System.IO.Path.Combine(d ?? string.Empty, "BabbdiCoop");
            }
        }

        // ---------- synthetic network self-test (Config: [Debug] NetSim) ----------
        // Feeds a fake 30 Hz stream through the REAL receive path
        // (net.incoming в†’ PumpIncoming в†’ ProcessPacket в†’ AnimateRemote) and dumps what is
        // actually rendered, so the smoothing is measured instead of eyeballed.

        private bool simActive;
        private float simClock;
        private float simNextSend;
        private float simYaw;
        private Vector3 simPos;
        private readonly System.Random simRng = new System.Random(1234);
        private readonly List<(float due, (Vector3 pos, Quaternion rot, int weaponIdx, int flags, float clock, int peer) pkt)>
            simQueue = new List<(float, (Vector3, Quaternion, int, int, float, int))>(64);
        private readonly List<string> simRows = new List<string>(4096);
        private readonly List<string> simPktRows = new List<string>(1024);
        private float dbgTr, dbgFirstKnot, dbgLastKnot, dbgInterval; // NetSim diagnostics
        private float dbgDtp;
        private int dbgSteps;

        // v0.8.0 real-network trace ([Debug] Trace): the synthetic NetSim cannot reproduce
        // the partner's frame pacing, the LAN jitter or the terrain raycasts, so if the real
        // game still stutters the next step is data from the real game.
        private readonly List<string> traceRows = new List<string>(4096);
        private readonly List<string> tracePktRows = new List<string>(4096);
        private string tracePath;
        private bool traceFrameHeader;
        private Vector3 traceTarget;

        private void SimTick()
        {
            if (!simActive)
            {
                if (netSimCfg == null || !netSimCfg.Value || Time.frameCount < 90) return;
                simActive = true;
                simClock = 0f;
                simNextSend = 0f;
                var cam = Camera.main;
                var origin = cam != null
                    ? cam.transform.position + cam.transform.forward * 3f
                    : new Vector3(0f, 1.3f, 0f);
                simPos = new Vector3(origin.x, origin.y - 1.3f, origin.z);
                Log.LogInfo($"[NETSIM] start at {simPos}, {netSimSecondsCfg.Value}s, smoothing={smoothingCfg.Value}");
            }

            float now = Time.time;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            simClock += dt;

            // sender script: walk 1.4 в†’ run 3.9 в†’ walk backwards 1.4 в†’ stand, turning 60В°/s
            float speed;
            if (simClock < 3f) speed = 1.4f;
            else if (simClock < 6f) speed = 3.9f;
            else if (simClock < 8f) speed = -1.4f;
            else speed = 0f;
            if (simClock > 3f) simYaw = Mathf.Repeat(simYaw + 60f * dt, 360f);
            simPos += Quaternion.Euler(0f, simYaw, 0f) * new Vector3(0f, 0f, speed * dt);

            // 30 Hz sender gate, but the network does not deliver in step: В±20 ms per
            // packet plus a 100 ms hole every 2 s вЂ” the kind of jitter two machines on a
            // LAN actually produce.
            bool hole = Mathf.Repeat(simClock, 2f) < 0.1f;
            Vector3 dir = Quaternion.Euler(0f, simYaw, 0f) * Vector3.forward;
            while (simClock >= simNextSend)
            {
                float gate = simNextSend;
                simNextSend += 1f / 30f;
                if (hole) continue;                    // sender stalled в†’ packet arrives late
                // the packet carries the position AT ITS SEND TIME, not at the end of this
                // frame вЂ” otherwise the sim itself would inject a frame's worth of noise
                Vector3 emitPos = simPos - dir * (speed * (simClock - gate));
                float emitYaw = Mathf.Repeat(simYaw - 60f * (simClock - gate), 360f);
                float jitter = (float)((simRng.NextDouble() * 2.0 - 1.0) * 0.020);
                simQueue.Add((now + 0.02f + jitter,
                              (emitPos, Quaternion.Euler(0f, emitYaw, 0f), -1, 0, 0f, 0)));
            }

            for (int i = simQueue.Count - 1; i >= 0; i--)
            {
                if (simQueue[i].due > now) continue;
                var pk = simQueue[i].pkt;
                // keep the simulated arrival time exact (including the В±20 ms jitter) even
                // though the frame boundary is later than `due`
                pk.clock = net.ClockNow - (now - simQueue[i].due);
                net.incoming.Enqueue(pk);
                simQueue.RemoveAt(i);
            }
        }

        private void SimLog()
        {
            if (!simActive || remoteGo == null) return;
            var p = remoteGo.transform.position;
            simRows.Add($"{simClock:F4};{p.x:F4};{p.z:F4};{remoteGo.transform.eulerAngles.y:F2};" +
                        $"{remoteSpeed:F3};{samples.Count};{dbgInterval:F5};{dbgTr:F5};" +
                        $"{dbgFirstKnot:F5};{dbgLastKnot:F5};{dbgDtp:F5};{dbgSteps}");
            if (simClock >= netSimSecondsCfg.Value)
            {
                simActive = false;
                string path = System.IO.Path.Combine(PluginDir, $"netsim_{DateTime.Now:HHmmss}.csv");
                try { System.IO.File.WriteAllLines(path, simRows); }
                catch (Exception e) { Log.LogWarning($"[NETSIM] write failed: {e.Message}"); }
                try { System.IO.File.WriteAllLines(path.Replace(".csv", "_pkt.csv"), simPktRows); } catch { }
                Log.LogInfo($"[NETSIM] done, {simRows.Count} rows в†’ {path}");
                netSimCfg.Value = false;
            }
        }

        private void FlushTrace()
        {
            if (traceCfg == null || !traceCfg.Value)
            {
                traceRows.Clear();
                tracePktRows.Clear();
                tracePath = null;
                traceFrameHeader = false;
                return;
            }
            if (traceRows.Count < 240 && tracePktRows.Count < 240) return;
            if (tracePath == null)
                tracePath = System.IO.Path.Combine(PluginDir, $"trace_{DateTime.Now:HHmmss}");
            try
            {
                if (traceRows.Count >= 240)
                {
                    string p = tracePath + "_frame.csv";
                    if (!traceFrameHeader)
                    {
                        System.IO.File.AppendAllText(p, "t;px;py;pz;tx;ty;tz;speed;delay;jitter;n\n");
                        traceFrameHeader = true;
                    }
                    System.IO.File.AppendAllLines(p, traceRows);
                    traceRows.Clear();
                }
                if (tracePktRows.Count >= 240)
                {
                    string p = tracePath + "_pkt.csv";
                    if (!System.IO.File.Exists(p))
                        System.IO.File.AppendAllText(p, "arrival;dtp;interval;jitter;steps\n");
                    System.IO.File.AppendAllLines(p, tracePktRows);
                    tracePktRows.Clear();
                }
            }
            catch (Exception e)
            {
                Log.LogWarning($"[TRACE] write failed: {e.Message}");
                traceRows.Clear();
                tracePktRows.Clear();
            }
        }

        // ---------- hotkeys ----------

        private void HandleHotkeys()
        {
            if (Input.GetKeyDown(KeyCode.F5)) StartHost();
            if (Input.GetKeyDown(KeyCode.F6)) StartClient();
            if (Input.GetKeyDown(KeyCode.F7)) StopNet();

            // F8: the auto grip test guessed wrong в†’ swap the end for this item id
            if (Input.GetKeyDown(KeyCode.F8) && remoteItemId >= 0 && remoteHeldItem != null)
            {
                remoteManualFlipFor = remoteItemId;
                remoteManualFlip = !remoteManualFlip;
                RebuildRemoteHeldItem();
                Toast($"РџСЂРµРґРјРµС‚ СЂР°Р·РІС‘СЂРЅСѓС‚: {(!remoteManualFlip ? "РєР°Рє Р°РІС‚Рѕ" : "РІСЂСѓС‡РЅСѓСЋ")}", 2f);
                Log.LogInfo($"[COOP] manual grip flip for item id {remoteItemId} = {remoteManualFlip}");
            }
        }

        private void StartHost()
        {
            try
            {
                net.StartHost(port.Value);
                var ips = string.Join(", ", Dns.GetHostAddresses(Dns.GetHostName())
                    .Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
                    .Select(a => a.ToString()));
                Log.LogInfo($"[COOP] HOST port {port.Value}, IPs: {ips}");
                Toast($"РҐРћРЎРў. Р”Р°Р№ РґСЂСѓРіСѓ IP: {ips}", 6f);
            }
            catch (Exception e)
            {
                Log.LogError($"[COOP] host failed: {e}");
                Toast("РҐРћРЎРў: РѕС€РёР±РєР° РІ Р»РѕРі", 4f);
            }
        }

        private void StartClient()
        {
            try
            {
                net.StartClient(joinIp.Value, port.Value);
                Toast($"РРґСѓ Рє {joinIp.Value}:{port.Value}", 5f);
            }
            catch (Exception e)
            {
                Log.LogError($"[COOP] join failed: {e}");
                Toast("РљР›РР•РќРў: РѕС€РёР±РєР° РІ Р»РѕРі", 4f);
            }
        }

        private void StopNet()
        {
            net.Stop();
            DestroyHeldItem();
            if (remoteGo != null) Destroy(remoteGo);
            remoteGo = null;
            remoteItemId = -1;
            remoteAnimator = null;
            remoteSpeed = 0f;
            remoteLastPacketTime = -1f;
            if (extraGo != null) Destroy(extraGo);
            extraGo = null;
            extraAnimator = null;
            extraSpeed = 0f;
            extraLastPacketTime = -1f;
            samples.Clear();
            Toast("РљРѕРѕРї РІС‹СЂСѓР±Р»РµРЅ", 2f);
        }

        // ---------- network pump ----------

        private void SendOwnPose()
        {
            if (net.role == NetRole.None) return;
            // v0.10.0: the air-hockey camera is detached and top-down; sending its rotation would
            // tilt the remote avatar's head/torso at the table. The last standing pose stays.
            if (miniGame != null && miniGame.Active) return;
            if (Time.time - lastSend < 0.034f) return; // ~30 Hz

            var fpc = LocalPlayer();
            if (fpc == null) return;

            var cam = fpc.GetComponentInChildren<Camera>();
            if (cam == null) return;

            // held item = InteractObject.inHands object parented under Main Camera
            int itemId = FindHeldItemId(cam.transform);

            // state flags (v0.7.0): crouch / airborne / using the held item
            byte flags = 0;
            var cc = fpc.GetComponent<CharacterController>();
            if (fpc.isCrouching) flags |= Net.FlagCrouch;
            if (airState) flags |= Net.FlagAir;
            if (itemId >= 0 && Time.time < usePulseUntil) flags |= Net.FlagUse;
            if (handstandOn) flags |= Net.FlagHandstand;
            if (flipStart >= 0f) flags |= Net.FlagFlip;

            // packet carries FEET position: derived from the local CharacterController so
            // crouching keeps the remote avatar on the ground automatically
            Vector3 feet;
            if (cc != null)
            {
                var centerWorld = fpc.transform.TransformPoint(cc.center);
                feet = centerWorld - Vector3.up * (cc.height * 0.5f);
            }
            else
            {
                feet = fpc.transform.position;
            }

            net.SendOwn(feet, cam.transform.rotation, itemId, flags);
            lastSend = Time.time;
        }

        /// <summary>
        /// The game instantiates held-item prefabs as children of "Main Camera"
        /// (anchors ObjectPos / LightPos / CompassPos вЂ¦) and flags them
        /// InteractObject.inHands = true. WeaponSwitching does not exist in BABBDI.
        /// </summary>
        private int FindHeldItemId(Transform camera)
        {
            foreach (var io in Resources.FindObjectsOfTypeAll<InteractObject>())
            {
                if (io == null || !io.inHands) continue;
                if (!io.gameObject.scene.IsValid()) continue; // skip assets
                if (io.transform.parent != camera) continue;  // held items are direct camera children
                var ip = io.GetComponent<ItemProperties>();
                if (ip != null) return ip.id;
            }
            return -1;
        }

        private void PumpIncoming()
        {
            // Drain everything that arrived since the last frame. If the local frame rate
            // dips, two packets land inside one Update; giving them the same Time.time
            // collapsed them into a single sample and the avatar teleported.
            //
            // v0.8.0: the arrival time of each packet is taken from the RECEIVE THREAD
            // (Net.Clock, a Stopwatch), not back-dated by the average interval. Back-dating
            // assumed the batch had arrived evenly spaced; in practice a main-thread hitch
            // queues 3-4 packets, and squeezing them into 2 intervals made the knot grid run
            // 1.5x too fast for a moment вЂ” the avatar accelerated for ~100 ms, which is
            // exactly the "РґС‘СЂРіР°РµС‚ РїСЂРё Р±РµРіРµ" that survived the v0.7.8 smoothing.
            incomingBatch.Clear();
            while (net.incoming.TryDequeue(out var packet)) incomingBatch.Add(packet);
            if (incomingBatch.Count == 0) return;

            float now = Time.time;
            float clockNow = net.ClockNow;
            // v0.10.6: the primary peer feeds the full smoothing/animator pipeline; a second
            // remote peer feeds the extra avatar. This stops two clients sharing one model.
            int primaryPeer = net.role == NetRole.Host ? 1 : 0;
            for (int i = 0; i < incomingBatch.Count; i++)
            {
                // Clock в†’ Unity time. Both advance at real-time rate; the offset is taken
                // from this frame, so any drift cancels out.
                float age = Mathf.Clamp(clockNow - incomingBatch[i].clock, 0f, 1f);
                var p = incomingBatch[i];
                if (p.peer == primaryPeer)
                    ProcessPacket((p.pos, p.rot, p.weaponIdx, p.flags, p.clock), now - age);
                else
                    ProcessExtraPacket(p, now - age);
            }
            remoteLastPacketTime = now;
        }

        /// <summary>
        /// Smoothed arrival cadence. Plain median dies on packet bursts (after a hiccup the
        /// queued packets arrive a few ms apart and the median collapses to ~7 ms, which
        /// squeezes the knot grid and makes the avatar sprint); a plain EMA dies on a single
        /// lost packet. Clipping each sample into В±40 % of the estimate rejects both, and a
        /// 3 % gain still follows a real rate change within ~1 s.
        /// </summary>
        private float TrackCadence(float dtp)
        {
            float lo = sampleInterval * 0.7f, hi = sampleInterval * 1.4f;
            return sampleInterval + (Mathf.Clamp(dtp, lo, hi) - sampleInterval) * 0.03f;
        }

        private void ProcessPacket((Vector3 pos, Quaternion rot, int weaponIdx, int flags, float clock) p, float arrival)
        {
            bool legacy = smoothingCfg != null && smoothingCfg.Value == "legacy";

            // Cadence from the REAL arrival spacing (never from the synthetic grid below).
            if (lastArrivalTime > 0f)
            {
                float dtp = arrival - lastArrivalTime;
                dbgDtp = dtp;
                if (dtp > 0.002f && dtp < 0.5f)
                {
                    sampleInterval = TrackCadence(dtp);
                    // v0.8.0: jitter = how far this spacing strays from the smoothed cadence.
                    // A loss burst (100 ms hole) is the interesting case: it lifts the average
                    // a bit, and the render delay grows with it, so the next hole is covered
                    // instead of leaving the buffer empty.
                    float j = Mathf.Min(Mathf.Abs(dtp - sampleInterval), 0.15f);
                    arrivalJitter += (j - arrivalJitter) * 0.05f;
                }
            }
            lastArrivalTime = arrival;

            // v0.7.8: knots go on a UNIFORM grid, not on the arrival times. The spline in
            // AnimateRemote is a uniform Catmull-Rom, so its speed between two knots is
            // (distance / knot spacing): with jittery arrival times the spacing jumps
            // between 8 and 90 ms and the avatar visibly accelerates at every knot вЂ” that
            // was "РґС‘СЂРіР°РµС‚ РїСЂРё Р±РµРіРµ", no matter how smooth the curve itself was. Arrival
            // jitter is absorbed by the grid instead of being baked into the curve.
            float interval = Mathf.Clamp(sampleInterval, 1f / 90f, 0.12f);
            float t;
            int stepsUsed = 0;
            if (legacy || samples.Count == 0)
            {
                t = arrival;
            }
            else
            {
                float lastKnot = samples[samples.Count - 1].Time;
                // How many sample intervals have passed since the last knot? A LOST packet
                // leaves a 2Г— gap between positions; if the grid advanced by only one step,
                // that missing sample gets squeezed into one interval and the avatar sprints
                // for 33 ms (measured: 16 m/s spikes after every 100 ms hiccup). Stepping by
                // the real number of intervals keeps the speed honest; the sub-interval part
                // is the jitter and is limited to В±5 % of a step, so the grid can still track
                // a small rate error without injecting network jitter into the curve.
                int steps = Mathf.Clamp(Mathf.RoundToInt((arrival - lastKnot) / interval), 1, 4);
                dbgSteps = steps;
                stepsUsed = steps;
                float predicted = lastKnot + steps * interval;
                float drift = Mathf.Clamp(arrival - predicted, -0.05f * interval, 0.05f * interval);
                t = predicted + drift;
            }

            if (simActive)
                simPktRows.Add($"{Time.time:F4};{dbgDtp:F5};{stepsUsed};{sampleInterval:F5};{t:F5}");
            if (traceCfg != null && traceCfg.Value)
                tracePktRows.Add($"{arrival:F4};{dbgDtp:F4};{sampleInterval:F4};{arrivalJitter:F4};{stepsUsed}");

            // NOTE: no speed estimate from packet deltas any more вЂ” a dropped packet made
            // the delta cover two intervals and the gait doubled for a moment. Speed now
            // comes from the RENDERED movement in AnimateRemote (AnimateRemote/SetMotion).

            // unwrap the yaw so the samples form a continuous curve for the cubic spline
            float yaw = p.rot.eulerAngles.y;
            if (samples.Count > 0)
                yaw = samples[samples.Count - 1].Yaw + Mathf.DeltaAngle(samples[samples.Count - 1].Yaw, yaw);

            // The "feet" in the packet are the bottom of the sender's CharacterController
            // capsule, and Unity holds that shape a skin-width above the surface вЂ” so the
            // avatar visibly hovered ("РјРѕРґРµР»СЊ Р»РµРІРёС‚РёСЂСѓРµС‚"). Probe the real floor under the
            // feet on our side and place the model on it. v0.7.8: the correction itself is
            // smoothed вЂ” a stair/ledge used to move it by up to 32 cm in ONE packet, which
            // read as the whole model hopping.
            bool airborne = (p.flags & Net.FlagAir) != 0;
            Vector3 feet = p.pos;
            if (airborne)
            {
                groundOffset = 0f;            // in the air the packet position is the truth
                groundOffsetInit = false;
            }
            else if (!legacy)
            {
                float dy = FloorDelta(p.pos);
                // v0.8.0: this used to move 5 cm PER PACKET toward the raycast hit, i.e. up to
                // 1.5 m/s of vertical motion at 30 Hz. On rocks and stairs the ray alternates
                // between a ledge and the floor and the whole model bobbed вЂ” vertical
                // bobbing is the most visible part of "РґС‘СЂРіР°РµС‚СЃСЏ РїСЂРё С…РѕРґСЊР±Рµ". The correction
                // is a slow bias now (П„ в‰€ 0.4 s), ignores anything under 1.5 cm and is
                // clamped, so it cannot turn into motion of its own.
                if (Mathf.Abs(dy) < 0.015f) dy = groundOffset;
                if (!groundOffsetInit) { groundOffset = dy; groundOffsetInit = true; }
                groundOffset += (dy - groundOffset) * 0.10f;
                groundOffset = Mathf.Clamp(groundOffset, -0.18f, 0.18f);
                feet.y += groundOffset;
            }
            else
            {
                feet = GroundSnap(p.pos, false);
            }

            samples.Add(new PoseSample { Time = t, Pos = feet, Yaw = yaw });            while (samples.Count > 24) samples.RemoveAt(0); // 0.8 s at 30 Hz

            remoteFlags = p.flags;
            remoteLastPacketPos = feet;
            remoteTargetPos = feet;
            remoteTargetYaw = p.rot.eulerAngles.y;

            var fwd = p.rot * Vector3.forward;
            remotePitchTarget = Mathf.Asin(Mathf.Clamp(-fwd.y, -1f, 1f)) * Mathf.Rad2Deg;

            if (remoteGo == null) SpawnRemote(feet);
            if (p.weaponIdx != remoteItemId)
            {
                remoteItemId = p.weaponIdx;
                RebuildRemoteHeldItem();
            }
        }

        // ---------- avatar ----------

        /// <summary>
        /// Puts the remote feet on the surface that is actually under them. The sender's
        /// capsule bottom floats ~skinWidth above the floor, which read as the model
        /// levitating. Only while grounded (a jump must not be pulled back down) and only
        /// for small corrections; anything else keeps the packet position.
        /// </summary>
        private Vector3 GroundSnap(Vector3 feet, bool airborne)
        {
            if (airborne) return feet;
            float dy = FloorDelta(feet);
            return new Vector3(feet.x, feet.y + dy, feet.z);
        }

        /// <summary>
        /// Height correction toward the real floor under the given feet (0 = no floor /
        /// player capsule / nonsense step). Split out in v0.7.8 so the caller can smooth
        /// the correction over several packets instead of applying it in one step.
        /// </summary>
        private float FloorDelta(Vector3 feet)
        {
            if (!Physics.Raycast(feet + Vector3.up * 0.45f, Vector3.down, out var hit, 1.1f, ~0,
                    QueryTriggerInteraction.Ignore))
                return 0f;
            if (hit.collider != null && hit.collider.GetComponentInParent<CharacterController>() != null)
                return 0f;                      // don't stand on a player capsule
            float dy = hit.point.y - feet.y;
            if (dy < -0.32f || dy > 0.32f) return 0f; // step/ledge noise
            return dy;
        }

        private void SpawnRemote(Vector3 pos)
        {
            remoteGo = new GameObject("COOP_RemotePlayer");
            remoteGo.transform.position = pos; // packet pos = feet of the remote player
            smoothInit = false;                // restart the render follower at the spawn pose
            BuildAvatar(remoteGo.transform);
            remoteAnimator = remoteGo.GetComponentInChildren<GlbAnimator>();
            Log.LogInfo("[COOP] remote player spawned");
            Toast("Р”СЂСѓРі РІ РёРіСЂРµ!", 3f);
        }

        // v0.10.6: the second remote peer. Simple follower (not the full 30 Hz spline stack);
        // it only needs to be its own model, not the same one as the primary partner.
        private void SpawnExtra(Vector3 pos)
        {
            extraGo = new GameObject("COOP_RemotePlayer2");
            extraGo.transform.position = pos;
            extraSmoothPos = pos;
            extraSmoothYaw = extraTargetYaw;
            BuildAvatar(extraGo.transform);
            extraAnimator = extraGo.GetComponentInChildren<GlbAnimator>();
            Log.LogInfo("[COOP] second remote player spawned (peer 2)");
        }

        private void ProcessExtraPacket((Vector3 pos, Quaternion rot, int weaponIdx, int flags, float clock, int peer) p,
                                        float arrival)
        {
            if (extraGo == null) SpawnExtra(p.pos);
            extraTargetPos = p.pos;
            extraTargetYaw = p.rot.eulerAngles.y;
            Vector3 fwd = p.rot * Vector3.forward;
            extraPitchTarget = Mathf.Asin(Mathf.Clamp(-fwd.y, -1f, 1f)) * Mathf.Rad2Deg;
            extraFlags = p.flags;
            extraLastPacketTime = Time.time;
        }

        private void AnimateExtra()
        {
            if (extraGo == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0.0001f) return;

            float k = 1f - Mathf.Exp(-dt / 0.12f);
            extraSmoothPos = Vector3.Lerp(extraSmoothPos, extraTargetPos, k);
            extraSmoothYaw = Mathf.LerpAngle(extraSmoothYaw, extraTargetYaw, k);
            Vector3 before = extraGo.transform.position;
            extraGo.transform.position = extraSmoothPos;
            extraGo.transform.rotation = Quaternion.Euler(0f, extraSmoothYaw, 0f);

            Vector3 d = extraSmoothPos - before;
            d.y = 0f;
            float v = d.magnitude / dt;
            if (v > 12f) v = 0f;                    // spawn teleports
            float ks = 1f - Mathf.Exp(-dt / 0.15f);
            extraSpeed = Mathf.Lerp(extraSpeed, v, ks);
            extraPitch = Mathf.Lerp(extraPitch, extraPitchTarget, 1f - Mathf.Exp(-dt / 0.10f));
            Vector3 local = Quaternion.Inverse(extraGo.transform.rotation) * (d / dt);
            extraVelLocal = Vector3.Lerp(extraVelLocal, local, ks);

            if (extraAnimator != null)
            {
                extraAnimator.SetMotion(extraSpeed, extraPitch, extraVelLocal);
                extraAnimator.SetState((extraFlags & Net.FlagCrouch) != 0,
                                       (extraFlags & Net.FlagAir) != 0,
                                       (extraFlags & Net.FlagUse) != 0,
                                       (extraFlags & Net.FlagHandstand) != 0,
                                       (extraFlags & Net.FlagFlip) != 0);

                // v0.10.7: the second client can be the one holding the north mallet (or
                // the only one watching a substitute play). Pose it at the table too.
                if (AirHockeyGame.ExtraMiniActive)
                {
                    extraGo.transform.position = AirHockeyGame.ExtraMiniFeet;
                    extraGo.transform.rotation = Quaternion.Euler(0f, AirHockeyGame.ExtraMiniYaw, 0f);
                    extraAnimator.SetMotion(0f, 0f, Vector3.zero);
                    extraAnimator.SetState(false, false, false, false, false);
                    extraAnimator.SetMiniPose(true, AirHockeyGame.ExtraMiniHand);
                }
                else
                {
                    extraAnimator.SetMiniPose(false, Vector3.zero);
                }
            }
        }

        private string avatarSource = "?";

        /// GLB (soldier) if present, else Kenney GLB, else OBJ, else primitives.
        private void BuildAvatar(Transform root)
        {
            GameObject model = null;
            try
            {
                var pluginsDir = System.IO.Path.GetDirectoryName(typeof(BabbdiCoopPlugin).Assembly.Location);
                var dir = System.IO.Path.Combine(pluginsDir ?? string.Empty, "BabbdiCoop");
                // Villager (Soul Reaver male 3) is auto-rigged onto the soldier's skeleton
                // by the Rigger tool: same nodes, skins and clips, only the mesh differs.
                var glbPath = System.IO.Path.Combine(dir, "villager.glb");
                if (!System.IO.File.Exists(glbPath))
                    glbPath = System.IO.Path.Combine(dir, "soldier.glb");
                if (!System.IO.File.Exists(glbPath))
                    glbPath = System.IO.Path.Combine(dir, "character-r.glb");
                if (System.IO.File.Exists(glbPath))
                {
                    // Rigger output is authored in the soldier's own convention (front = glTF -Z)
                    // and the soldier needs the flip; Kenney's is +Z
                    var glbName = System.IO.Path.GetFileName(glbPath);
                    bool yaw180 = glbName == "soldier.glb" || glbName == "villager.glb";
                    model = GlbAvatarBuilder.Load(glbPath, yaw180, new GlbAvatarBuilder.LogStub
                    {
                        LogWarning = m => Log.LogWarning(m),
                        LogInfo = m => Log.LogInfo(m)
                    });
                    avatarSource = "GLB " + System.IO.Path.GetFileName(glbPath) + (yaw180 ? " (yaw180)" : "");
                    Log.LogInfo($"[COOP] avatar loaded from GLB: {glbPath} yaw180={yaw180}");
                }
            }
            catch (Exception e)
            {
                Log.LogWarning($"[COOP] GLB avatar failed ({e.Message}), trying OBJ");
                model = null;
            }

            if (model == null)
            {
                try
                {
                    var pluginsDir = System.IO.Path.GetDirectoryName(typeof(BabbdiCoopPlugin).Assembly.Location);
                    var objPath = System.IO.Path.Combine(pluginsDir ?? string.Empty, "BabbdiCoop", "player.obj");
                    if (System.IO.File.Exists(objPath))
                    {
                        model = ObjModelLoader.Load(objPath);
                        avatarSource = "OBJ (fallback!)";
                    }
                }
                catch (Exception e)
                {
                    Log.LogWarning($"[COOP] OBJ avatar failed, fallback to primitives: {e.Message}");
                }
            }

            if (model != null)
            {
                model.transform.SetParent(root, false);
                model.transform.localPosition = Vector3.zero;
                model.name = "AvatarMesh";
            }
            else
            {
                BuildHumanoid(root);
                avatarSource = "РїСЂРёРјРёС‚РёРІС‹ (fallback!)";
            }
        }

        /// Humanoid-ish figure from primitives, ~1.7m tall, feet at transform origin.
        private void BuildHumanoid(Transform root)
        {
            // torso: capsule 0.45..1.35
            var torso = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            torso.name = "Torso";
            torso.transform.SetParent(root, false);
            torso.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            torso.transform.localScale = new Vector3(0.42f, 0.45f, 0.24f);
            Paint(torso, new Color(0.55f, 0.75f, 0.95f));

            // head
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root, false);
            head.transform.localPosition = new Vector3(0f, 1.48f, 0f);
            head.transform.localScale = new Vector3(0.19f, 0.21f, 0.19f);
            Paint(head, new Color(0.95f, 0.85f, 0.7f));

            // "nose" so you can see where he looks
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Nose";
            nose.transform.SetParent(head.transform, false);
            nose.transform.localPosition = new Vector3(0f, 0f, 0.12f);
            nose.transform.localScale = new Vector3(0.12f, 0.08f, 0.12f);
            Paint(nose, new Color(1f, 0.4f, 0.3f));

            // arms: slightly angled cylinders
            foreach (var side in new[] { -1f, 1f })
            {
                var arm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                arm.name = side < 0 ? "ArmL" : "ArmR";
                arm.transform.SetParent(root, false);
                arm.transform.localPosition = new Vector3(side * 0.27f, 1.05f, 0f);
                arm.transform.rotation = Quaternion.Euler(0f, 0f, side * 20f);
                arm.transform.localScale = new Vector3(0.07f, 0.28f, 0.07f);
                Paint(arm, new Color(0.55f, 0.75f, 0.95f));
            }

            // legs
            foreach (var side in new[] { -1f, 1f })
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                leg.name = side < 0 ? "LegL" : "LegR";
                leg.transform.SetParent(root, false);
                leg.transform.localPosition = new Vector3(side * 0.11f, 0.35f, 0f);
                leg.transform.localScale = new Vector3(0.09f, 0.35f, 0.09f);
                Paint(leg, new Color(0.25f, 0.28f, 0.35f));
            }

            foreach (var rend in root.GetComponentsInChildren<Renderer>())
            {
                Destroy(rend.gameObject.GetComponent<Collider>());
            }
        }

        private void Paint(GameObject go, Color color)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            r.material.color = color;
        }

        // ---------- held item mirror ----------

        /// <summary>
        /// Replicates the partner's held item by ItemProperties.id: the partner's
        /// held item is an InteractObject instance parented to their Main Camera, so
        /// we find any scene object with the same id (incl. our own held copy),
        /// clone its visuals and park it at the avatar's hand.
        /// </summary>
        private void RebuildRemoteHeldItem()
        {
            DestroyHeldItem();
            if (remoteGo == null || remoteItemId < 0) return;

            GameObject source = null;
            foreach (var ip in Resources.FindObjectsOfTypeAll<ItemProperties>())
            {
                if (ip == null || ip.id != remoteItemId) continue;
                if (!ip.gameObject.scene.IsValid()) continue; // skip assets
                source = ip.gameObject;
                break;
            }

            if (source == null)
            {
                Toast($"РџСЂРµРґРјРµС‚ #{remoteItemId} Сѓ РґСЂСѓРіР° РЅРµ РЅР°Р№РґРµРЅ РІ СЌС‚РѕРј РјРёСЂРµ", 2f);
                Log.LogWarning($"[COOP] no scene object with item id {remoteItemId}");
                return;
            }

            var clone = Instantiate(source);
            clone.name = "COOP_HeldItem";
            clone.SetActive(true);
            StripNonVisual(clone.transform);

            var hand = FindDeep(remoteGo.transform, "HandAnchor");
            if (hand == null)
            {
                hand = new GameObject("HandAnchor").transform;
                hand.SetParent(remoteGo.transform, false);
                // mirrored model: the visually right hand sits at -X of the avatar root
                hand.localPosition = new Vector3(-0.3f, 1.05f, 0.35f);
            }
            clone.transform.SetParent(hand, false);
            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;
            clone.transform.localScale = Vector3.one;

            FitHeldItemToHand(clone, hand);
            remoteHeldItem = clone;

            Toast($"Р”СЂСѓРі РґРµСЂР¶РёС‚: {source.name}", 2f);
            Log.LogInfo($"[COOP] partner holds item id {remoteItemId} ('{source.name}') в†’ cloned");
        }

        private void StripNonVisual(Transform t)
        {
            foreach (var comp in t.GetComponents<Component>())
            {
                // keep Unity visuals + transform only kill behaviors/colliders/lights/audio
                if (comp is Transform
                    || comp is MeshFilter
                    || comp is MeshRenderer
                    || comp is SkinnedMeshRenderer)
                    continue;
                UnityEngine.Object.Destroy(comp);
            }
            foreach (Transform child in t)
            {
                StripNonVisual(child);
            }
        }

        // ---------- held item fit (v0.7.0, reworked v0.7.2) ----------

        /// <summary>
        /// Puts a cloned world prop into the hand sensibly: long props (bats, sticks,
        /// tools) are shrunk to hand-held size, aligned by their REAL long axis (PCA of
        /// the vertices вЂ” an AABB lies for diagonal meshes) and laid forward from the
        /// grip so the procedural swing reads correctly. Compact props are centred.
        /// </summary>
        private void FitHeldItemToHand(GameObject clone, Transform hand)
        {
            var pts = CollectVerts(clone, hand);
            bool vertsOk = pts.Count >= 4;
            if (!vertsOk)
            {
                // Scene props often ship with Read/Write Disabled, so mesh.vertices throws
                // and we cannot sample the shape (that is why the Club kept its full world
                // size). v0.8.0: the fallback used Renderer.bounds, which is an axis-aligned
                // box in WORLD space вЂ” for a prop lying diagonally (or rotated relative to
                // the hand) that box is a loose, wrongly-oriented cube: the long axis came out
                // wrong, the grip end landed tens of centimetres off the handle and the item
                // visibly levitated next to the fist (the pickaxe). The mesh's OWN bounds are
                // available even without Read/Write and are aligned with the model's axes, so
                // every renderer contributes its own oriented box.
                pts.Clear();
                MeshBoxPoints(clone, hand, pts);
                if (pts.Count < 8)
                {
                    Log.LogWarning("[COOP] item has no usable renderers вЂ” left as-is");
                    return;
                }
                Log.LogInfo($"[COOP] item meshes are not readable вЂ” fitting by mesh-local bounds ({pts.Count} box corners)");
            }

            var b0 = BoundsOf(pts);
            Vector3 size = b0.size;
            float maxExtent = Mathf.Max(size.x, Mathf.Max(size.y, size.z));

            // Third-person props are shown at ItemScale of their world size (default 0.5,
            // i.e. exactly half): the partner's club looked comically huge next to the
            // avatar at 1:1.
            float itemScale = itemScaleCfg != null ? Mathf.Clamp(itemScaleCfg.Value, 0.05f, 1f) : 0.5f;
            float scale = itemScale;
            clone.transform.localScale = Vector3.one * scale;

            bool elongated = maxExtent > 0.35f;
            if (!elongated)
            {
                if (TryBoundsIn(hand, clone.transform, out var b2))
                    clone.transform.localPosition = -b2.center;
                Log.LogInfo($"[COOP] held item fitted: compact {size.x:F2}x{size.y:F2}x{size.z:F2} m, scale {scale:F2}");
                return;
            }

            var axis = DominantAxis(pts);
            var rot = Quaternion.FromToRotation(axis, Vector3.forward);
            clone.transform.localRotation = rot;

            // where the vertices sit after the uniform scale + alignment
            var aligned = new List<Vector3>(pts.Count);
            foreach (var p in pts) aligned.Add(rot * (p * scale));

            string profile = ProfileAlongZ(aligned, out float nearMean, out float farMean, out float len);

            string mode = (gripCfg != null && gripCfg.Value != null) ? gripCfg.Value.Trim().ToLowerInvariant() : "auto";
            bool flipAuto = nearMean > farMean * 1.25f;   // thick end at the hand в†’ swap
            if (!vertsOk)
            {
                // No radius profile available. Scene props are held by their PIVOT (the game
                // parents them to the camera anchor with no offset), so the pivot end is the
                // grip end: put the end nearest to the clone origin in the hand.
                float minP = float.MaxValue, maxP = float.MinValue;
                foreach (var p in pts)
                {
                    float d = Vector3.Dot(p, axis);
                    if (d < minP) minP = d;
                    if (d > maxP) maxP = d;
                }
                flipAuto = Mathf.Abs(minP) > Mathf.Abs(maxP); // pivot at the far end в†’ turn it round
            }
            bool flip = mode == "flip" || (mode != "keep" && flipAuto);
            if (remoteManualFlipFor == remoteItemId) flip = remoteManualFlip;

            if (flip)
            {
                clone.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * rot;
                for (int i = 0; i < aligned.Count; i++)
                {
                    var p = aligned[i];
                    aligned[i] = new Vector3(-p.x, p.y, -p.z);   // 180В° about Y
                }
                float t = nearMean; nearMean = farMean; farMean = t;
            }

            var b1 = BoundsOf(aligned);
            // The grip end goes INTO the palm and the palm itself is the anchor (measured from
            // the rig's finger bones), so the handle has to start BEHIND it: with the butt at
            // the palm the prop looked balanced on the hand. ~2 cm back, inside the glove,
            // makes the shaft pass through the closed fist вЂ” that is what reads as "held".
            clone.transform.localPosition = new Vector3(-b1.center.x, -b1.center.y, -b1.min.z - 0.02f);

            float fistGap = float.MaxValue;
            var lp = clone.transform.localPosition;
            foreach (var p in aligned)
                fistGap = Mathf.Min(fistGap, (p + lp).magnitude);

            Log.LogInfo($"[COOP] grip: {profile} near(r)={nearMean * 100f:F1}cm far(r)={farMean * 100f:F1}cm " +
                        $"axis=({axis.x:F2},{axis.y:F2},{axis.z:F2}) mode={mode} " +
                        $"auto={(flipAuto ? "flip" : "keep")} final={(flip ? "flip" : "keep")}");
            Log.LogInfo($"[COOP] held item fitted: raw size {size.x:F2}x{size.y:F2}x{size.z:F2} m, " +
                        $"scale {scale:F2}, world len {len:F2} m, nearest point {fistGap * 100f:F1} cm from the fist");
        }

        /// <summary>All mesh vertices of the clone, expressed in <paramref name="space"/>.</summary>
        private static List<Vector3> CollectVerts(GameObject root, Transform space)
        {
            var pts = new List<Vector3>(2048);
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    Vector3[] v = null;
                    try { v = mf.sharedMesh.vertices; } catch { }
                    if (v != null)
                    {
                        int step = Mathf.Max(1, v.Length / 1200);
                        for (int i = 0; i < v.Length; i += step)
                            pts.Add(space.InverseTransformPoint(mf.transform.TransformPoint(v[i])));
                    }
                    else
                    {
                        // Read/Write Disabled: mesh.vertices throws, but the read-only
                        // MeshDataArray view still gives us the positions
                        ReadOnlyVerts(mf.sharedMesh, mf.transform, space, pts);
                    }
                    continue;
                }

                var smr = r as SkinnedMeshRenderer;
                if (smr != null && smr.sharedMesh != null)
                {
                    var baked = new Mesh();
                    try
                    {
                        smr.BakeMesh(baked);
                        var v = baked.vertices;
                        int step = Mathf.Max(1, v.Length / 1200);
                        for (int i = 0; i < v.Length; i += step)
                            pts.Add(space.InverseTransformPoint(smr.transform.TransformPoint(v[i])));
                    }
                    catch { }
                    finally { Destroy(baked); }
                }
            }
            return pts;
        }

        /// <summary>
        /// Positions of a mesh whose Read/Write flag is Disabled вЂ” mesh.vertices throws for
        /// those, but the read-only MeshDataArray view is allowed.
        /// </summary>
        private static void ReadOnlyVerts(Mesh mesh, Transform tr, Transform space, List<Vector3> pts)
        {
            if (mesh == null) return;
            try
            {
                using (var data = Mesh.AcquireReadOnlyMeshData(mesh))
                {
                    var md = data[0];
                    int n = md.vertexCount;
                    if (n <= 0) return;
                    var arr = new NativeArray<Vector3>(n, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                    try
                    {
                        md.GetVertices(arr);
                        int step = Mathf.Max(1, n / 1200);
                        for (int i = 0; i < n; i += step)
                            pts.Add(space.InverseTransformPoint(tr.TransformPoint(arr[i])));
                        if (Log != null)
                            Log.LogInfo($"[COOP] non-readable mesh '{mesh.name}': read {n} vertices via MeshDataArray");
                    }
                    finally { arr.Dispose(); }
                }
            }
            catch (Exception e)
            {
                // one warning is enough: the fallback (mesh-local bounds) then takes over
                if (!readOnlyVertsWarned && Log != null)
                {
                    readOnlyVertsWarned = true;
                    Log.LogWarning($"[COOP] MeshDataArray failed on '{mesh?.name}': {e.GetType().Name}: {e.Message}");
                }
            }
        }

        private static bool readOnlyVertsWarned;

        private static Bounds BoundsOf(List<Vector3> pts)
        {
            var b = new Bounds(pts[0], Vector3.zero);
            for (int i = 1; i < pts.Count; i++) b.Encapsulate(pts[i]);
            return b;
        }

        /// <summary>
        /// Corners of each renderer's mesh bounds in ITS OWN axes, transformed into
        /// <paramref name="space"/>. Unlike Renderer.bounds (a world-space AABB) this box
        /// follows the model's orientation, so the long axis and both ends of a diagonal
        /// prop come out right вЂ” that is what made the held pickaxe float beside the hand.
        /// Mesh.bounds is serialized data, available even with Read/Write Disabled.
        /// </summary>
        private static void MeshBoxPoints(GameObject root, Transform space, List<Vector3> pts)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                Bounds lb;
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    lb = mf.sharedMesh.bounds;
                }
                else if (r is SkinnedMeshRenderer smr)
                {
                    lb = smr.localBounds;
                }
                else
                {
                    // unsupported renderer: fall back to its world AABB, still better than nothing
                    var wb = r.bounds;
                    if (wb.size.sqrMagnitude < 1e-8f) continue;
                    for (int i = 0; i < 8; i++)
                    {
                        var p = new Vector3(wb.center.x + ((i & 1) == 0 ? -wb.extents.x : wb.extents.x),
                                            wb.center.y + ((i & 2) == 0 ? -wb.extents.y : wb.extents.y),
                                            wb.center.z + ((i & 4) == 0 ? -wb.extents.z : wb.extents.z));
                        pts.Add(space.InverseTransformPoint(p));
                    }
                    continue;
                }

                if (lb.size.sqrMagnitude < 1e-10f) continue;
                for (int i = 0; i < 8; i++)
                {
                    var c = new Vector3((i & 1) == 0 ? lb.min.x : lb.max.x,
                                        (i & 2) == 0 ? lb.min.y : lb.max.y,
                                        (i & 4) == 0 ? lb.min.z : lb.max.z);
                    pts.Add(space.InverseTransformPoint(r.transform.TransformPoint(c)));
                }
            }
        }

        /// <summary>Principal (longest) direction of the point cloud вЂ” power iteration.</summary>
        private static Vector3 DominantAxis(List<Vector3> pts)
        {
            var mean = Vector3.zero;
            foreach (var p in pts) mean += p;
            mean /= pts.Count;

            float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
            foreach (var p in pts)
            {
                var d = p - mean;
                xx += d.x * d.x; xy += d.x * d.y; xz += d.x * d.z;
                yy += d.y * d.y; yz += d.y * d.z; zz += d.z * d.z;
            }

            var v = new Vector3(1f, 0.05f, 0.05f);
            for (int i = 0; i < 32; i++)
            {
                var nv = new Vector3(
                    xx * v.x + xy * v.y + xz * v.z,
                    xy * v.x + yy * v.y + yz * v.z,
                    xz * v.x + yz * v.y + zz * v.z);
                if (nv.sqrMagnitude < 1e-12f) break;
                v = nv.normalized;
            }
            return v;
        }

        /// <summary>Radius profile along +Z: mean cross-section radius of the two end bands.</summary>
        private static string ProfileAlongZ(List<Vector3> pts, out float nearMean, out float farMean, out float len)
        {
            var b = BoundsOf(pts);
            len = b.size.z;
            nearMean = farMean = 0f;
            if (len < 1e-4f) return "len=0";

            const int Bins = 10;
            var rad = new float[Bins];
            double nSum = 0, fSum = 0;
            int nCnt = 0, fCnt = 0;

            foreach (var p in pts)
            {
                float t = (p.z - b.min.z) / len;                       // 0 at the hand end
                float r = new Vector2(p.x - b.center.x, p.y - b.center.y).magnitude;
                int bin = Mathf.Clamp((int)(t * Bins), 0, Bins - 1);
                if (r > rad[bin]) rad[bin] = r;

                if (t < 0.15f) { nSum += r; nCnt++; }
                else if (t > 0.85f) { fSum += r; fCnt++; }
            }

            nearMean = nCnt > 0 ? (float)(nSum / nCnt) : 0f;
            farMean = fCnt > 0 ? (float)(fSum / fCnt) : 0f;

            var sb = new System.Text.StringBuilder();
            sb.Append($"len={len:F2}m bins[");
            for (int i = 0; i < Bins; i++) sb.Append($"{rad[i] * 100f:F1} ");
            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>Renderer bounds expressed in <paramref name="space"/>'s local coordinates.</summary>
        private static bool TryBoundsIn(Transform space, Transform root, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                var b = r.bounds;
                var e = b.extents;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + new Vector3(
                        (i & 1) == 0 ? -e.x : e.x,
                        (i & 2) == 0 ? -e.y : e.y,
                        (i & 4) == 0 ? -e.z : e.z);
                    var lp = space.InverseTransformPoint(corner);
                    if (!any) { bounds = new Bounds(lp, Vector3.zero); any = true; }
                    else bounds.Encapsulate(lp);
                }
            }
            return any;
        }

        private void DestroyHeldItem()
        {
            if (remoteHeldItem != null)
            {
                Destroy(remoteHeldItem);
                remoteHeldItem = null;
            }
        }

        private void AnimateRemote()
        {
            if (remoteGo == null) return;

            // no packets в†’ partner stopped: decay the estimated speed so he idles
            bool stale = remoteLastPacketTime > 0f && Time.time - remoteLastPacketTime > 0.5f;
            if (stale)
            {
                remoteSpeed = Mathf.MoveTowards(remoteSpeed, 0f, Time.deltaTime * 5f);
                remoteVelLocal = Vector3.MoveTowards(remoteVelLocal, Vector3.zero, Time.deltaTime * 6f);
                remoteFlags = 0;
            }

            var before = remoteGo.transform.position;

            if (remoteLastPacketTime > 0f && samples.Count >= 2)
            {
                bool legacy = smoothingCfg != null && smoothingCfg.Value == "legacy";
                // Render this far behind the wall clock. The wall clock is perfectly smooth,
                // so the render TIME can never jump; everything the network does to the data
                // (jitter, quantisation, losses) is absorbed by the damped follower at the end.
                // The delay itself is smoothed with a 2 s time constant: it used to follow
                // `sampleInterval` directly, so a hiccup that moved the estimate shifted the
                // render time by up to 150 ms in one frame вЂ” a 60 cm jump at run speed.
                float delayTarget = Mathf.Clamp(sampleInterval * 1.6f + arrivalJitter * 2.0f + 0.05f, 0.10f, 0.32f);
                if (!legacy)
                    renderDelay = Mathf.Lerp(renderDelay, delayTarget, 1f - Mathf.Exp(-Time.deltaTime / 2f));
                float delay = legacy ? Mathf.Clamp(sampleInterval * 1.8f, 0.07f, 0.26f) : renderDelay;
                float tr = Time.time - delay;

                // Keep the knots the spline actually needs: the two older than the render
                // time (for its tangents) and everything newer. The old guard kept only 4
                // samples while the render time sat 2.2вЂ“3.5 intervals behind the newest
                // knot вЂ” so it regularly fell IN FRONT of the oldest sample, the avatar
                // froze on it and then jumped forward when the next packet arrived. That
                // was the visible stutter during walk and run, not the curve itself.
                while (samples.Count > 6 && samples[2].Time < tr) samples.RemoveAt(0);

                dbgTr = tr;
                dbgFirstKnot = samples[0].Time;
                dbgLastKnot = samples[samples.Count - 1].Time;
                dbgInterval = sampleInterval;

                int n = samples.Count;
                var a0 = samples[0];
                var a1 = samples[n - 1];
                Vector3 pos;
                float yaw;
                if (tr <= a0.Time)
                {
                    pos = a0.Pos;
                    yaw = a0.Yaw;
                }
                else if (tr >= a1.Time)
                {
                    // No data newer than the render time (a loss longer than the buffer).
                    // Hold the last pose: extrapolating here was tried and it overshoots on
                    // curves (a 60В°/s turn with 150 ms of dead reckoning misses by ~18 cm and
                    // then snaps back, which reads as a worse jolt than the freeze).
                    pos = a1.Pos;
                    yaw = a1.Yaw;
                }
                else
                {
                    int i = 0;
                    while (i < n - 2 && samples[i + 1].Time < tr) i++;
                    var s1 = samples[i];
                    var s2 = samples[i + 1];
                    float span = s2.Time - s1.Time;
                    float f = span > 1e-4f ? Mathf.Clamp01((tr - s1.Time) / span) : 1f;

                    // Catmull-Rom instead of a plain lerp: packets are not evenly spaced
                    // (the sender's gate quantises to frame boundaries), so straight-line
                    // segments changed the avatar's speed at every knot вЂ” that is what
                    // still read as "РґС‘СЂРіР°РµС‚" at running speed. A C1-continuous spline
                    // keeps the velocity smooth across knots.
                    var s0 = samples[Mathf.Max(0, i - 1)];
                    var s3 = samples[Mathf.Min(n - 1, i + 2)];
                    pos = CatmullRom(s0.Pos, s1.Pos, s2.Pos, s3.Pos, f);
                    yaw = CatmullRom(s0.Yaw, s1.Yaw, s2.Yaw, s3.Yaw, f);
                }
                if (legacy)
                {
                    remoteGo.transform.position = pos;
                    remoteGo.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                    traceTarget = pos;
                }
                else
                {
                    // v0.7.8: the rendered pose is a critically damped follower of the
                    // interpolated target. This is what actually removes the stutter:
                    //  - the sender quantises its sends to its own frame boundaries, so
                    //    consecutive packets carry 4 / 5 / 6 frames of movement (~2Г— speed for
                    //    one frame) вЂ” the follower attenuates that 30 Hz ripple by ~25Г—;
                    //  - a lost packet leaves the target standing still for 100 ms and then
                    //    jumping вЂ” the follower glides through it instead of freezing and popping;
                    //  - and it cannot ring: a critically damped spring has no overshoot, so a
                    //    real direction change is followed without wobble.
                    // A fixed render delay (above) plus this lag is the only cost.
                    if (!smoothInit)
                    {
                        smoothPos = pos; smoothYaw = yaw;
                        posVel = Vector3.zero; yawVel = 0f;
                        smoothInit = true;
                    }
                    smoothPos = Vector3.SmoothDamp(smoothPos, pos, ref posVel, 0.15f);
                    smoothYaw = Mathf.SmoothDampAngle(smoothYaw, yaw, ref yawVel, 0.15f);
                    remoteGo.transform.position = smoothPos;
                    remoteGo.transform.rotation = Quaternion.Euler(0f, smoothYaw, 0f);
                    traceTarget = pos;
                }
            }
            else
            {
                // no packets yet (debug preview) в†’ simple settle toward the target
                var target = remoteTargetPos; // packet pos = feet; no extra offset
                remoteGo.transform.position = Vector3.Lerp(remoteGo.transform.position, target, Time.deltaTime * 15f);
                remoteGo.transform.rotation = Quaternion.Slerp(remoteGo.transform.rotation, Quaternion.Euler(0f, remoteTargetYaw, 0f), Time.deltaTime * 15f);
            }

            if (remoteLastPacketTime > 0f)
            {
                // Drive the gait from what is actually rendered, not from packet arrival
                // intervals (network jitter there made the legs speed up and slow down).
                // Distance travelled в†’ phase, so the feet stay locked to the ground.
                float dt = Time.deltaTime;
                if (dt > 0.0001f)
                {
                    var d = remoteGo.transform.position - before;
                    d.y = 0f;
                    float v = d.magnitude / dt;              // m/s actually moved this frame
                    if (v < 12f)                              // ignore spawn teleports
                    {
                        // v0.7.8: П„ = 0.15 s instead of 0.10 s. The per-frame estimate is
                        // quantised by the frame time (two packets: 3.3 m/s one frame,
                        // 3.0 the next), and the gait reads the result directly вЂ” a
                        // slightly slower filter keeps the legs from twitching.
                        float ks = 1f - Mathf.Exp(-dt / 0.15f);
                        remoteSpeed = Mathf.Lerp(remoteSpeed, v, ks);

                        // velocity in the avatar's own frame: z>0 walking forward, z<0
                        // backing up, x = strafe. The animator needs it to rewind the
                        // gait clip (backwards) and to add a side-step.
                        Vector3 local = Quaternion.Inverse(remoteGo.transform.rotation) * (d / dt);
                        remoteVelLocal = Vector3.Lerp(remoteVelLocal, local, ks);
                    }
                }
            }

            if (remoteAnimator != null)
            {
                // v0.8.0: the pitch is smoothed like the position. It used to be the raw
                // value of the last packet, so the head (and now the torso) snapped to a new
                // angle 30 times a second while the partner looked around.
                float kp = 1f - Mathf.Exp(-Time.deltaTime / 0.10f);
                remotePitch = Mathf.Lerp(remotePitch, remotePitchTarget, kp);

                remoteAnimator.SetMotion(remoteSpeed, remotePitch, remoteVelLocal);
                remoteAnimator.SetState((remoteFlags & Net.FlagCrouch) != 0,
                                        (remoteFlags & Net.FlagAir) != 0,
                                        (remoteFlags & Net.FlagUse) != 0,
                                        (remoteFlags & Net.FlagHandstand) != 0,
                                        (remoteFlags & Net.FlagFlip) != 0);

                // v0.10.5: the partner is at the air-hockey table вЂ” override the pose position
                // and hand the mini-game mallet target to the animator (stance + arm/body follow).
                if (AirHockeyGame.RemoteMiniActive)
                {
                    remoteGo.transform.position = AirHockeyGame.RemoteMiniFeet;
                    remoteGo.transform.rotation = Quaternion.Euler(0f, AirHockeyGame.RemoteMiniYaw, 0f);
                    remoteAnimator.SetMotion(0f, 0f, Vector3.zero);
                    remoteAnimator.SetState(false, false, false, false, false);
                    remoteAnimator.SetMiniPose(true, AirHockeyGame.RemoteMiniHand);
                }
                else
                {
                    remoteAnimator.SetMiniPose(false, Vector3.zero);
                }
            }

            if (traceCfg != null && traceCfg.Value)
            {
                var rp = remoteGo.transform.position;
                traceRows.Add($"{Time.time:F4};{rp.x:F4};{rp.y:F4};{rp.z:F4};" +
                              $"{traceTarget.x:F4};{traceTarget.y:F4};{traceTarget.z:F4};" +
                              $"{remoteSpeed:F3};{renderDelay:F4};{arrivalJitter:F4};{samples.Count}");
            }
        }

        /// <summary>Uniform Catmull-Rom spline (C1): the curve passes through bв†’c.</summary>
        private static float CatmullRom(float a, float b, float c, float d, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * b + (c - a) * t
                           + (2f * a - 5f * b + 4f * c - d) * t2
                           + (-a + 3f * b - 3f * c + d) * t3);
        }

        private static Vector3 CatmullRom(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * b + (c - a) * t
                           + (2f * a - 5f * b + 4f * c - d) * t2
                           + (-a + 3f * b - 3f * c + d) * t3);
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var found = FindDeep(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private FirstPersonController LocalPlayer()
        {
            try { return FirstPersonController.instance; }
            catch { return null; }
        }

        /// <summary>
        /// v0.10.0 вЂ” one persistent component runs the air-hockey table activity. It finds the table
        /// whenever the district has been (re)built, so a level reload needs no re-wiring.
        /// </summary>
        private void EnsureMiniGame()
        {
            if (arcadeGame == null)
            {
                var ago = new GameObject("BabbdiCoopArcadeGame");
                UnityEngine.Object.DontDestroyOnLoad(ago);
                arcadeGame = ago.AddComponent<ArcadeGame>();
                arcadeGame.Init(Log, net);
                arcadeGame.autoTestKind = 0;
                string ap = arcadeTestCfg != null ? arcadeTestCfg.Value : "";
                if (ap == "arkanoid") arcadeGame.autoTestKind = 1;
                else if (ap == "pong") arcadeGame.autoTestKind = 2;
                else if (ap == "snake") arcadeGame.autoTestKind = 3;
                arcadeGame.autoTest = (dinoTestCfg != null && dinoTestCfg.Value) ||
                                      arcadeGame.autoTestKind > 0;
                arcadeGame.duelTestKind = 0;
                string dt = duelTestCfg != null ? duelTestCfg.Value : "";
                if (dt == "arkanoid") arcadeGame.duelTestKind = 1;
                else if (dt == "pong") arcadeGame.duelTestKind = 2;
                else if (dt == "snake") arcadeGame.duelTestKind = 3;
                if (arcadeGame.duelTestKind > 0 && duelTestCfg != null) duelTestCfg.Value = "";
                if (arcadeGame.duelTestKind > 0)
                    Log.LogInfo("[DUEL] test kind=" + arcadeGame.duelTestKind);
                if (arcadeGame.autoTest)
                {
                    if (dinoTestCfg != null) dinoTestCfg.Value = false;
                    if (arcadeTestCfg != null) arcadeTestCfg.Value = "";
                }
                Log.LogInfo("[ARCADE] arcade controller created; E at a cabinet enters its game" +
                            (arcadeGame.autoTest ? " (cabinet test armed)" : ""));
            }
            if (miniGame != null) return;
            var go = new GameObject("BabbdiCoopAirHockey");
            UnityEngine.Object.DontDestroyOnLoad(go);
            miniGame = go.AddComponent<AirHockeyGame>();
            miniGame.Init(net, Log);
            miniGame.autoTest = miniTestCfg != null && miniTestCfg.Value;
            Log.LogInfo("[MINI] air-hockey controller created; E at the table enters the top-down game" +
                        (miniGame.autoTest ? " (MiniTest armed)" : ""));
        }

        // ---------- HUD ----------

        private string toastText;
        private float toastUntil;

        private void Toast(string text, float seconds)
        {
            toastText = text;
            toastUntil = Time.realtimeSinceStartup + seconds;
        }

        private void OnGUI()
        {
            if (net.role != NetRole.None)
            {
                float age = net.lastRecvClock < 0f ? -1f : net.ClockNow - net.lastRecvClock;
                string stale = net.role == NetRole.None ? ""
                    : (net.remoteEndpointKnown
                        ? (age < 0 ? "Р¶РґСѓ РїР°РєРµС‚С‹вЂ¦" : $"РїР°РєРµС‚ {age:0.0}s РЅР°Р·Р°Рґ")
                        : (net.role == NetRole.Client ? "РЅРµ СЃСЂР°Р±РѕС‚Р°Р»Рѕ РїРѕРєР° вЂ” Р¶РґСѓ С…РѕС‚СЊ С‡РµРіРѕ-С‚Рѕ РѕС‚ С…РѕСЃС‚Р°вЂ¦" : "Р¶РґСѓ РєР»РёРµРЅС‚Р°вЂ¦"));

                GUI.Label(new Rect(8, 8, 520, 64),
                    $"<b>[COOP v{PluginVersion}]</b> {net.role} | РѕС‚РґР°РЅРѕ: {net.sentPackets} | РїСЂРёРЅСЏС‚Рѕ: {net.recvPackets} | {stale} | РјРѕРґРµР»СЊ: {avatarSource}",
                    new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true });
            }

            if (!string.IsNullOrEmpty(toastText) && Time.realtimeSinceStartup < toastUntil)
            {
                GUI.Label(new Rect(12, Screen.height - 64, Screen.width - 24, 48),
                    $"<b>[COOP]</b> {toastText}", new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 18,
                        richText = true,
                        normal = { textColor = Color.white }
                    });
            }
        }

        private void OnDestroy()
        {
            try
            {
                if (itemOverlayCam != null && itemOverlayCb != null)
                    itemOverlayCam.RemoveCommandBuffer(CameraEvent.AfterEverything, itemOverlayCb);
            }
            catch { }
            net?.Dispose();
        }
    }
}

