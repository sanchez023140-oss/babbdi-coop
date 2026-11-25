using System;
using System.IO;
using UnityEngine;

namespace BabbdiCoop
{
    // Debug stills of the winter town (Debug.SulimanShot = true).
    internal static partial class Suliman
    {
        public static bool ShotRequested;
        private static bool shotsRunning;
        private static int shotStep;
        private static float shotNext;
        private static Vector3 shotSavedPos;
        private static Quaternion shotSavedRot;
        private static bool shotSaved;

        private struct ShotPose
        {
            public float x, z, up, lx, lz, lup;
            public string name;
            public ShotPose(float x, float z, float up, float lx, float lz, float lup, string name)
            { this.x = x; this.z = z; this.up = up; this.lx = lx; this.lz = lz; this.lup = lup; this.name = name; }
        }

        private static readonly ShotPose[] poses =
        {
            new ShotPose(958f, 716f, 2.2f, 930f, 900f, 7f, "street_spawn"),
            new ShotPose(850f, 730f, 90f, 900f, 850f, 0f, "town_air"),
            new ShotPose(900f, 580f, 55f, 900f, 592f, 0f, "stadium"),
            new ShotPose(1135f, 849f, 2.5f, 980f, 820f, 5f, "street_warm"),
            new ShotPose(438f, 1216f, 5f, 455f, 1242f, 3f, "river_pump"),
            new ShotPose(1512f, 748f, 18f, 1560f, 700f, 8f, "cliff_house"),
            new ShotPose(930f, 842f, 30f, 930f, 925f, 0f, "underpass"),
            new ShotPose(960f, 548f, 36f, 980f, 900f, 0f, "town_wide"),
            new ShotPose(958f, 716f, 2.2f, 958f, 650f, 2.5f, "spawn_south"),
            new ShotPose(958f, 716f, 2.2f, 1060f, 716f, 2.5f, "spawn_east"),
            new ShotPose(958f, 716f, 8f, 975f, 728f, 1f, "plaza_lamp"),
            new ShotPose(958f, 716f, 28f, 958f, 720f, 0f, "plaza_top"),
            new ShotPose(1019f, 780f, 2.5f, 1019f, 816f, 5f, "wood_house"),
            new ShotPose(1180f, 1335f, 2.5f, 1180f, 1378f, 4f, "portholes"),
            new ShotPose(1108f, 872f, 2.0f, 1135f, 849f, 1.5f, "entrance"),
            new ShotPose(958f, 714f, 2.0f, 962f, 722f, 0.8f, "propeller"),
        };

        private static void TickShots()
        {
            if (ShotRequested && !shotsRunning)
            {
                ShotRequested = false;
                shotsRunning = true;
                shotStep = 0;
                shotNext = Time.time + 1.3f;
                var fpc = FirstPersonController.instance;
                if (fpc != null)
                {
                    shotSavedPos = fpc.transform.position;
                    shotSavedRot = fpc.transform.rotation;
                    shotSaved = true;
                }
                Vector3 p = Origin + SpawnLocal;
                p.y = Origin.y + HeightAt(SpawnLocal.x, SpawnLocal.z) + 0.2f;
                Teleport(p, SpawnYaw);
                Inside = true;
                ApplyNight();
                Log("[SULIMAN] shot session: entered the town");
            }
            if (!shotsRunning) return;
            if (Time.time < shotNext) return;
            shotNext = Time.time + 0.55f;

            if (shotStep >= poses.Length)
            {
                RefShots();
                ShotMain("suliman_gameview");
                shotsRunning = false;
                Inside = false;
                RestoreEnv();
                if (shotSaved && FirstPersonController.instance != null)
                {
                    Teleport(shotSavedPos, shotSavedRot.eulerAngles.y);
                }
                shotSaved = false;
                Log("[SULIMAN] shot session done");
                return;
            }

            var s = poses[shotStep];
            Vector3 pos = Origin + new Vector3(s.x, HeightAt(s.x, s.z) + s.up, s.z);
            Vector3 look = Origin + new Vector3(s.lx, HeightAt(s.lx, s.lz) + s.lup, s.lz);
            Shot($"suliman_{shotStep:00}_{s.name}", pos, look);
            Log($"[SULIMAN] shot {shotStep} {s.name} pos {pos} look {look}");
            shotStep++;
        }

        private static void RefShots()
        {
            var refs = new[]
            {
                new ShotPose(900f, 450f, 38f, 900f, 580f, 4f, "ref_1_stadium"),
                new ShotPose(630f, 250f, 38f, 630f, 400f, 12f, "ref_2_ruined_towers"),
                new ShotPose(615f, 1335f, 26f, 568f, 1286f, 4f, "ref_3_pump_station"),
                new ShotPose(1180f, 1290f, 22f, 1180f, 1378f, 4f, "ref_4_porthole_gallery"),
                new ShotPose(930f, 995f, 18f, 930f, 918f, 3f, "ref_5_underpass"),
                new ShotPose(760f, 380f, 30f, 760f, 300f, 1f, "ref_6_collapsed_blocks"),
                new ShotPose(410f, 1152f, 16f, 440f, 1134f, 3f, "ref_7_river_stairs"),
                new ShotPose(1490f, 770f, 26f, 1560f, 700f, 6f, "ref_8_cliff_house"),
            };
            bool oldFog = RenderSettings.fog;
            Color oldAmb = RenderSettings.ambientLight;
            float oldInt = RenderSettings.ambientIntensity;
            var sunGo = new GameObject("SulimanRefSun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.95f, 0.96f, 1f);
            sun.intensity = 0.95f;
            sun.shadows = LightShadows.None;
            sunGo.transform.rotation = Quaternion.Euler(38f, 35f, 0f);
            // second, softer light from the opposite side so no façade is left in the dark
            var fillGo = new GameObject("SulimanRefFill");
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.74f, 0.82f, 1f);
            fill.intensity = 0.55f;
            fill.shadows = LightShadows.None;
            fillGo.transform.rotation = Quaternion.Euler(24f, 215f, 0f);
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.62f);
            RenderSettings.ambientIntensity = 1f;
            try
            {
                foreach (var s in refs)
                {
                    Vector3 pos = Origin + new Vector3(s.x, HeightAt(s.x, s.z) + s.up, s.z);
                    Vector3 look = Origin + new Vector3(s.lx, HeightAt(s.lx, s.lz) + s.lup, s.lz);
                    Shot(s.name, pos, look);
                    Log($"[SULIMAN] ref shot {s.name}");
                }
            }
            finally
            {
                RenderSettings.fog = oldFog;
                RenderSettings.ambientLight = oldAmb;
                RenderSettings.ambientIntensity = oldInt;
                UnityEngine.Object.Destroy(sunGo);
                UnityEngine.Object.Destroy(fillGo);
            }
        }

        private static void ShotMain(string name)
        {
            var cam = PlayerCam();
            if (cam == null) { Warn("[SULIMAN] main camera missing for gameview"); return; }
            const int W = 1280, H = 720;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            var oldTarget = cam.targetTexture;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0f, 0f, W, H), 0, 0);
                tex.Apply();
                var dir = Path.Combine(BepInEx.Paths.PluginPath, "BabbdiCoop", "map");
                Directory.CreateDirectory(dir);
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
                Log($"[SULIMAN] gameview shot {name}: clear={cam.clearFlags} bg={cam.backgroundColor} " +
                    $"sky={(RenderSettings.skybox != null ? RenderSettings.skybox.name : "none")} " +
                    $"fog={RenderSettings.fogColor} density={RenderSettings.fogDensity}");
                RaycastHit upHit;
                if (Physics.Raycast(cam.transform.position, Vector3.up, out upHit, 600f, ~0, QueryTriggerInteraction.Ignore))
                    Log($"[SULIMAN] overhead: {upHit.collider.name} at {upHit.point} d={upHit.distance:0.0}");
                else
                    Log("[SULIMAN] overhead: nothing (clear sky / no geometry)");
            }
            catch (Exception e) { Warn("[SULIMAN] gameview shot failed: " + e.Message); }
            finally
            {
                RenderTexture.active = null;
                cam.targetTexture = oldTarget;
                UnityEngine.Object.Destroy(rt);
                UnityEngine.Object.Destroy(tex);
            }
        }

        private static void Shot(string name, Vector3 pos, Vector3 look)
        {
            var go = new GameObject("SulimanShotCam");
            var cam = go.AddComponent<Camera>();
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation((look - pos).normalized, Vector3.up);
            cam.clearFlags = Inside ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
            cam.backgroundColor = Color.black;
            cam.fieldOfView = 62f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1800f;
            cam.enabled = false;
            var main = PlayerCam();
            if (main != null) cam.cullingMask = main.cullingMask;
            var fpc = FirstPersonController.instance;
            var held = new System.Collections.Generic.List<GameObject>();
            if (fpc != null)
            {
                if (fpc.currentObject != null) held.Add(fpc.currentObject);
                if (fpc.propeller != null) held.Add(fpc.propeller);
                if (fpc.club != null) held.Add(fpc.club);
                if (fpc.grabber != null) held.Add(fpc.grabber);
                if (fpc.climber != null) held.Add(fpc.climber);
                if (fpc.blower != null) held.Add(fpc.blower);
            }
            var wasOn = new System.Collections.Generic.List<bool>();
            for (int i = 0; i < held.Count; i++) { wasOn.Add(held[i].activeSelf); held[i].SetActive(false); }
            var camRends = new System.Collections.Generic.List<Renderer>();
            var camRendWas = new System.Collections.Generic.List<bool>();
            var shotProps = new System.Collections.Generic.List<GameObject>();
            var shotPropsWas = new System.Collections.Generic.List<bool>();
            foreach (var p in Resources.FindObjectsOfTypeAll<Propeller>())
            {
                if (p == null || !p.gameObject.scene.IsValid()) continue;
                shotProps.Add(p.gameObject);
                shotPropsWas.Add(p.gameObject.activeSelf);
                p.gameObject.SetActive(false);
            }
            if (fpc != null && fpc.playerCamera != null)
            {
                foreach (var r in fpc.playerCamera.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null) continue;
                    camRends.Add(r); camRendWas.Add(r.enabled); r.enabled = false;
                }
            }

            const int W = 1280, H = 720;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0f, 0f, W, H), 0, 0);
                tex.Apply();
                var dir = Path.Combine(BepInEx.Paths.PluginPath, "BabbdiCoop", "map");
                Directory.CreateDirectory(dir);
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            }
            catch (Exception e) { Warn("[SULIMAN] shot failed: " + e.Message); }
            finally
            {
                for (int i = 0; i < held.Count; i++) held[i].SetActive(wasOn[i]);
                for (int i = 0; i < camRends.Count; i++) if (camRends[i] != null) camRends[i].enabled = camRendWas[i];
                for (int i = 0; i < shotProps.Count; i++) if (shotProps[i] != null) shotProps[i].SetActive(shotPropsWas[i]);
                RenderTexture.active = null;
                cam.targetTexture = null;
                UnityEngine.Object.Destroy(rt);
                UnityEngine.Object.Destroy(tex);
                UnityEngine.Object.Destroy(go);
            }
        }
    }
}
