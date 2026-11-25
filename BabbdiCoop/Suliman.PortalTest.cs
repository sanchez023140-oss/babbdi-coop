using System;
using System.IO;
using UnityEngine;

namespace BabbdiCoop
{
    // v0.10.49: debug checks for the memorial portal (Debug.PortalTest = true).
    internal static partial class Suliman
    {
        public static bool PortalTestRequested;
        private static bool portalTesting;
        private static int portalStep;
        private static float portalAt;

        private static void TickPortalTest()
        {
            if (PortalTestRequested && !portalTesting)
            {
                PortalTestRequested = false;
                portalTesting = true;
                portalStep = 0;
                portalAt = Time.time + 1.0f;
                if (Memorial == null)
                {
                    Warn("[SULIMAN] portal test: no memorial (district disabled?)");
                    portalTesting = false;
                    return;
                }
                Vector3 p = Memorial.TransformPoint(new Vector3(0f, 0.42f, 6.9f));
                Teleport(p, 180f);
                Inside = false;
                Log($"[SULIMAN] portal test: teleported to the roof {p}; memorial at {Memorial.position}");
            }
            if (!portalTesting) return;
            if (Time.time < portalAt) return;

            try
            {
            if (portalStep == 0)
            {
                bool near; MemorialEdge(out near);
                var fpc = FirstPersonController.instance;
                Log($"[SULIMAN] portal test: player {fpc?.transform.position} nearMemorial={near}");
                var cam = Camera.main ?? (fpc != null ? fpc.playerCamera : null);
                if (cam != null) Shot("portal_roof_before", cam.transform.position, cam.transform.position + cam.transform.forward * 10f);
                else Warn("[SULIMAN] portal test: no camera at the roof");
                Enter();
                portalStep = 1;
                portalAt = Time.time + 3.0f;
            }
            else if (portalStep == 1)
            {
                Log($"[SULIMAN] portal test: after Enter inside={Inside} envSaved={envSaved} " +
                    $"player={FirstPersonController.instance?.transform.position} returnAnchor={ReturnAnchor}");
                var fpc = FirstPersonController.instance;
                var cam = Camera.main ?? (fpc != null ? fpc.playerCamera : null);
                if (cam != null) Shot("portal_suliman", cam.transform.position, cam.transform.position + cam.transform.forward * 20f);
                else Warn("[SULIMAN] portal test: no camera in Suliman");
                Leave();
                portalStep = 2;
                portalAt = Time.time + 3.0f;
            }
            else if (portalStep == 2)
            {
                Log($"[SULIMAN] portal test: after Leave inside={Inside} envSaved={envSaved} " +
                    $"player={FirstPersonController.instance?.transform.position}");
                var fpc = FirstPersonController.instance;
                var cam = Camera.main ?? (fpc != null ? fpc.playerCamera : null);
                if (cam != null) Shot("portal_roof_after", cam.transform.position, cam.transform.position + cam.transform.forward * 10f);
                portalTesting = false;
                Log("[SULIMAN] portal test: done");
            }
            }
            catch (Exception e)
            {
                Warn("[SULIMAN] portal test step " + portalStep + " failed: " + e);
                portalStep = 3;
                portalTesting = false;
            }
        }
    }
}
