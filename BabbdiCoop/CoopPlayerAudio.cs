using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;

namespace BabbdiCoop
{
    /// <summary>
    /// v0.10.29: mini-game audio isolation.
    ///
    /// The level's wind is not only the player's fall loop: `IndoorWindSound` / `OutdoorWindSound`
    /// are global 2D loops, and the held gadgets (the propeller's `Propeller2/3` clips and any
    /// other gadget wind) sit under the game camera. Every one of them is saved, muted and then
    /// **enforced every LateUpdate** while a mini-game is active, because the gadget scripts and
    /// the ambient controllers can set their own volume/mute again from Update. On exit the exact
    /// saved volumes/mute states and script enabled states are restored.
    /// </summary>
    internal static class CoopPlayerAudio
    {
        static FieldInfo fallAudioField;
        static FieldInfo fallTimeField;
        static AudioSource fallSrc;

        static readonly List<AudioSource> ambientWind = new List<AudioSource>();
        static readonly List<float> ambientWindVolume = new List<float>();
        static readonly List<bool> ambientWindMute = new List<bool>();

        static readonly List<AudioSource> heldAudio = new List<AudioSource>();
        static readonly List<float> heldVolume = new List<float>();
        static readonly List<bool> heldMute = new List<bool>();
        static readonly List<MonoBehaviour> heldScripts = new List<MonoBehaviour>();
        static readonly List<bool> heldScriptEnabled = new List<bool>();

        // v0.10.35: for the air hockey, mute every scene source except the table's click/goal sfx.
        static readonly List<AudioSource> allExcept = new List<AudioSource>();
        static readonly List<float> allExceptVolume = new List<float>();
        static readonly List<bool> allExceptMute = new List<bool>();
        static AudioSource keepSource;

        public static void MuteFallWind(FirstPersonController fpc, ManualLogSource log = null)
        {
            if (fpc == null) return;
            if (fallAudioField == null)
                fallAudioField = typeof(FirstPersonController).GetField(
                    "fallAudio", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fallTimeField == null)
                fallTimeField = typeof(FirstPersonController).GetField(
                    "fallTime", BindingFlags.Instance | BindingFlags.NonPublic);

            fallSrc = fallAudioField != null ? fallAudioField.GetValue(fpc) as AudioSource : null;
            if (fallSrc != null)
            {
                float was = fallSrc.volume;
                fallSrc.volume = 0f;
                log?.LogInfo($"[COOP] fall wind muted for the mini-game (was {was:0.00})");
            }
            if (fallTimeField != null) fallTimeField.SetValue(fpc, 0f);
        }

        /// <summary>Mute every looping wind source in the scene (the level's 2D indoor/outdoor
        /// loops), saving volume and mute.</summary>
        public static void MuteAmbientWind(ManualLogSource log = null)
        {
            RestoreAmbientWind();
            foreach (var a in Resources.FindObjectsOfTypeAll<AudioSource>())
            {
                if (a == null || !a.gameObject.scene.IsValid()) continue;
                string obj = a.gameObject.name;
                string clip = a.clip != null ? a.clip.name : string.Empty;
                if (obj.IndexOf("wind", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                    clip.IndexOf("wind", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                ambientWind.Add(a);
                ambientWindVolume.Add(a.volume);
                ambientWindMute.Add(a.mute);
                a.volume = 0f;
                a.mute = true;
                log?.LogInfo($"[COOP] ambient wind muted: '{obj}' clip='{clip}' " +
                             $"was volume={ambientWindVolume[ambientWindVolume.Count - 1]:0.00}");
            }
            if (ambientWind.Count == 0)
                log?.LogWarning("[COOP] ambient wind: no wind source found in the scene");
        }

        /// <summary>v0.10.35: air-hockey audio isolation — save and mute every scene AudioSource
        /// except the table's own `sfx` (click/goal), so no wind/ambience can leak through while
        /// the mallet and goal sounds stay audible. Enforced every LateUpdate; restored on exit.</summary>
        public static void MuteAllExcept(AudioSource keep, ManualLogSource log = null)
        {
            RestoreAllExcept(log);
            keepSource = keep;
            int n = 0;
            foreach (var a in Resources.FindObjectsOfTypeAll<AudioSource>())
            {
                if (a == null || !a.gameObject.scene.IsValid() || a == keep) continue;
                allExcept.Add(a);
                allExceptVolume.Add(a.volume);
                allExceptMute.Add(a.mute);
                a.volume = 0f;
                a.mute = true;
                n++;
            }
            log?.LogInfo($"[COOP] air-hockey audio isolation: {n} sources muted, " +
                         $"table sfx kept ({(keep != null ? keep.gameObject.name : "none")})");
        }

        public static void RestoreAllExcept(ManualLogSource log = null)
        {
            for (int i = 0; i < allExcept.Count; i++)
            {
                var a = allExcept[i];
                if (a == null) continue;
                a.volume = allExceptVolume[i];
                a.mute = allExceptMute[i];
            }
            allExcept.Clear();
            allExceptVolume.Clear();
            allExceptMute.Clear();
            keepSource = null;
        }

        /// <summary>v0.10.35: disable the held gadget scripts without touching their audio list;
        /// used together with `MuteAllExcept`.</summary>
        public static void MuteHeldScripts(Camera cam, ManualLogSource log = null)
        {
            RestoreHeldScripts(log);
            if (cam == null) return;
            int n = 0;
            foreach (var mb in cam.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                string t = mb.GetType().Name;
                if (t != "Propeller" && t != "Blower" && t != "Climber" && t != "Grabber")
                    continue;
                heldScripts.Add(mb);
                heldScriptEnabled.Add(mb.enabled);
                mb.enabled = false;
                n++;
            }
            log?.LogInfo($"[COOP] held gadget scripts disabled for the mini-game: {n}");
        }

        public static void RestoreHeldScripts(ManualLogSource log = null)
        {
            for (int i = 0; i < heldScripts.Count; i++)
                if (heldScripts[i] != null) heldScripts[i].enabled = heldScriptEnabled[i];
            heldScripts.Clear();
            heldScriptEnabled.Clear();
        }

        /// <summary>v0.10.29: save and silence every AudioSource under the game camera (held
        /// gadgets: propeller lift/thrust, blower, etc.) and disable their gadget scripts so their
        /// Update cannot unmute a source while the mini-game owns Space/Fire1.</summary>
        public static void MuteHeldAudio(Camera cam, ManualLogSource log = null)
        {
            RestoreHeldAudio();
            if (cam == null) return;
            foreach (var a in cam.GetComponentsInChildren<AudioSource>(true))
            {
                if (a == null) continue;
                heldAudio.Add(a);
                heldVolume.Add(a.volume);
                heldMute.Add(a.mute);
                a.volume = 0f;
                a.mute = true;
                log?.LogInfo($"[COOP] held audio muted: '{a.gameObject.name}' " +
                             $"clip='{(a.clip != null ? a.clip.name : "null")}' " +
                             $"was volume={heldVolume[heldVolume.Count - 1]:0.00}");
            }
            foreach (var mb in cam.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                string t = mb.GetType().Name;
                if (t != "Propeller" && t != "Blower" && t != "Climber" && t != "Grabber")
                    continue;
                heldScripts.Add(mb);
                heldScriptEnabled.Add(mb.enabled);
                mb.enabled = false;
                log?.LogInfo($"[COOP] held gadget disabled: {t} on '{mb.gameObject.name}'");
            }
        }

        /// <summary>v0.10.29: called every LateUpdate while a mini-game is active. Enforces the
        /// mute after all gadget/ambience Updates have run, so no script can bring the wind back.</summary>
        public static void Enforce()
        {
            if (fallSrc != null) fallSrc.volume = 0f;
            for (int i = 0; i < ambientWind.Count; i++)
            {
                var a = ambientWind[i];
                if (a == null) continue;
                a.volume = 0f;
                a.mute = true;
            }
            for (int i = 0; i < heldAudio.Count; i++)
            {
                var a = heldAudio[i];
                if (a == null) continue;
                a.volume = 0f;
                a.mute = true;
            }
            for (int i = 0; i < allExcept.Count; i++)
            {
                var a = allExcept[i];
                if (a == null) continue;
                a.volume = 0f;
                a.mute = true;
            }
            for (int i = 0; i < heldScripts.Count; i++)
                if (heldScripts[i] != null) heldScripts[i].enabled = false;
        }

        public static void RestoreAmbientWind(ManualLogSource log = null)
        {
            for (int i = 0; i < ambientWind.Count; i++)
            {
                var a = ambientWind[i];
                if (a == null) continue;
                a.volume = ambientWindVolume[i];
                a.mute = ambientWindMute[i];
                log?.LogInfo($"[COOP] ambient wind restored: '{a.gameObject.name}' " +
                             $"volume={a.volume:0.00}");
            }
            ambientWind.Clear();
            ambientWindVolume.Clear();
            ambientWindMute.Clear();
        }

        public static void RestoreHeldAudio(ManualLogSource log = null)
        {
            for (int i = 0; i < heldAudio.Count; i++)
            {
                var a = heldAudio[i];
                if (a == null) continue;
                a.volume = heldVolume[i];
                a.mute = heldMute[i];
            }
            heldAudio.Clear();
            heldVolume.Clear();
            heldMute.Clear();
            for (int i = 0; i < heldScripts.Count; i++)
                if (heldScripts[i] != null) heldScripts[i].enabled = heldScriptEnabled[i];
            heldScripts.Clear();
            heldScriptEnabled.Clear();
            fallSrc = null;
        }
    }
}
