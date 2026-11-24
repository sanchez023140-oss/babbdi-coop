using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace BabbdiCoop
{
    // Part of the SovietDistrict builder - see SovietDistrict.cs.
    internal static partial class SovietDistrict
    {

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
                                try { System.IO.Directory.CreateDirectory(dir); } catch (System.Exception swallowed) { CoopDiag.Swallowed(swallowed); }
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
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "done.txt"), "done"); } catch (System.Exception swallowed) { CoopDiag.Swallowed(swallowed); }
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
            try { System.IO.File.WriteAllBytes(path, copy.EncodeToPNG()); } catch (System.Exception swallowed) { CoopDiag.Swallowed(swallowed); }
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
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "done.txt"), "done"); } catch (System.Exception swallowed) { CoopDiag.Swallowed(swallowed); }
        }
    }
}
