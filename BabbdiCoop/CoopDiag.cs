using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace BabbdiCoop
{
    /// <summary>v0.10.46: the replacement for 28 empty `catch { }` blocks.
    ///
    /// An empty catch hid every failure in the mod: a texture that did not load, a mesh that was
    /// not readable, a packet that could not be parsed — all silent. Now each site reports itself
    /// once (file:line + the exception), and then stays quiet, so a per-frame path cannot flood
    /// the log. `[CallerFilePath]`/`[CallerLineNumber]` mean no call site needs a hand-written
    /// label: the compiler fills in the exact location.
    /// </summary>
    internal static class CoopDiag
    {
        static readonly HashSet<string> seen = new HashSet<string>();

        public static void Swallowed(Exception e,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            string where = Path.GetFileName(file) + ":" + line;
            lock (seen)
            {
                if (seen.Contains(where)) return;
                seen.Add(where);
            }
            string msg = e == null ? "unknown" : e.GetType().Name + ": " + e.Message;
            try
            {
                UnityEngine.Debug.LogWarning("[COOP] swallowed exception at " + where + " — " + msg);
            }
            catch (Exception)
            {
                // logging must never throw
            }
        }

        /// <summary>Lets a test or a scene reload report the same site again.</summary>
        public static void Reset()
        {
            lock (seen) seen.Clear();
        }
    }
}
