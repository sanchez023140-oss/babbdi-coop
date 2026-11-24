using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BabbdiCoop
{
    /// <summary>v0.9.77 — the user's map edits, applied once per level load:
    ///  * the **sky/cloud layer** is raised and thickened x2 (<see cref="SkyScale"/>), and the camera's
    ///    far plane follows so the sky is no longer clipped;
    ///  * the **invisible ceiling** in the sky (`Cube (12)`, a 1 m plate at y=367) is removed;
    ///  * the **map footprint is doubled in every direction**: the tall invisible air ring
    ///    (`---INVISIBLE WALLS`, height 296 m) is moved twice as far from its centre and its segments
    ///    and height are doubled, so it still encloses the flyable volume at the 600 m flight ceiling;
    ///  * every **other invisible barrier** in that root (the `Outdoor`/`Indoor` door blockers, the
    ///    ground fences, the small cubes) and `DoorInvisibleCollider` are disabled — only the map-edge
    ///    ring and `KILLZ` remain.
    /// The `KILLZ` and `WaterSurface` colliders are left alone; the KILLZ still covers the enlarged
    /// footprint (it is 6970 m across).</summary>
    internal static class MapEdits
    {
        public static bool Enabled = true;
        public static float AreaScale = 2f;
        public static float SkyScale = 2f;
        public static bool RemoveInner = true;

        /// <summary>Camera far plane needed for the raised cloud layer; read by `TickCameraFar`.</summary>
        public static float SkyFar;

        private static int appliedId;
        private static string appliedScene;

        public static void TryApply(ManualLogSource log)
        {
            if (!Enabled) return;
            var scene = SceneManager.GetActiveScene();
            var root = GameObject.Find("---INVISIBLE WALLS");
            if (root == null) return;
            if (appliedId == root.GetInstanceID() && appliedScene == scene.name) return;
            appliedId = root.GetInstanceID();
            appliedScene = scene.name;
            try { Apply(root, log); }
            catch (System.Exception e) { log.LogError("[MAP] edit failed: " + e); }
        }

        private static void Apply(GameObject root, ManualLogSource log)
        {
            var walls = new List<Transform>();
            int ceiling = 0, inner = 0, water = 0, kill = 0, zero = 0;

            foreach (var c in root.GetComponentsInChildren<Collider>(true))
            {
                if (c == null) continue;
                var t = c.transform;
                var b = c.bounds;
                string n = t.name;

                if (n == "KILLZ") { kill++; continue; }
                if (n == "WaterSurface") { water++; continue; }

                // the invisible ceiling: a huge, thin horizontal plate high above the city
                if (b.size.y <= 2.5f && b.center.y > 300f && b.size.x > 200f && b.size.z > 200f)
                {
                    t.gameObject.SetActive(false);
                    ceiling++;
                    continue;
                }

                // an empty/zero-size collider — the level's leftover markers
                if (b.size.sqrMagnitude < 0.001f) { zero++; continue; }

                // the map edge: the tall air ring
                if (b.size.y >= 50f) { walls.Add(t); continue; }

                // everything else in this root is an inner invisible barrier
                if (RemoveInner && !c.isTrigger)
                {
                    c.enabled = false;
                    inner++;
                }
            }

            // the ring's centre and old footprint
            bool has = false;
            Bounds u = default;
            foreach (var t in walls)
            {
                var b = t.GetComponent<Collider>().bounds;
                if (!has) { u = b; has = true; }
                else u.Encapsulate(b);
            }
            Vector3 cc = u.center;

            // move the ring out and scale it; the bottom of each wall stays put, its height doubles
            foreach (var t in walls)
            {
                var b = t.GetComponent<Collider>().bounds;
                Vector3 p = t.position;
                p.x = cc.x + (p.x - cc.x) * AreaScale;
                p.z = cc.z + (p.z - cc.z) * AreaScale;
                p.y = b.min.y + b.size.y * 0.5f * AreaScale;
                var s = t.localScale;
                s.x *= AreaScale;
                s.y *= AreaScale;
                s.z *= AreaScale;
                t.localScale = s;
                t.position = p;
            }

            // the sky/cloud layer: twice as high above the city and twice as thick
            int clouds = 0;
            float cloudTop = 0f;
            const float baseY = -20f;
            var renderers = Resources.FindObjectsOfTypeAll<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || !r.gameObject.scene.IsValid()) continue;
                if (!r.name.StartsWith("SM_Cloud")) continue;
                var t = r.transform;
                Vector3 p = t.position;
                p.y = baseY + (p.y - baseY) * SkyScale;
                var s = t.localScale;
                s.y *= SkyScale;
                t.localScale = s;
                t.position = p;
                cloudTop = Mathf.Max(cloudTop, p.y + r.bounds.size.y * 0.5f);
                clouds++;
            }
            SkyFar = Mathf.Max(2500f, cloudTop + 1200f);
            var cam = Camera.main;
            if (cam != null && cam.farClipPlane < SkyFar) cam.farClipPlane = SkyFar;

            // a door's own invisible collider is not a map boundary either
            int door = 0;
            var doorGo = GameObject.Find("DoorInvisibleCollider");
            if (doorGo != null)
                foreach (var c in doorGo.GetComponentsInChildren<Collider>(true))
                    if (c != null && c.enabled) { c.enabled = false; door++; }

            log.LogInfo($"[MAP] expanded: {walls.Count} map-edge walls scaled x{AreaScale:0.##} around " +
                        $"({cc.x:0},{cc.z:0}) — footprint {u.size.x * AreaScale:0}x{u.size.z * AreaScale:0} m, " +
                        $"height {u.size.y * AreaScale:0} m; invisible ceiling removed ({ceiling}), " +
                        $"inner invisible barriers removed ({inner}), door blockers removed ({door}), " +
                        $"water {water}, KILLZ {kill}, empty {zero}; " +
                        $"{clouds} cloud layers raised x{SkyScale:0.##} (top {cloudTop:0} m, camera far {SkyFar:0} m)");
        }
    }
}
