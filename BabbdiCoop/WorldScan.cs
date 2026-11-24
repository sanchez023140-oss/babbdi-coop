using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace BabbdiCoop
{
    /// <summary>
    /// One-shot survey of the loaded level: where the geometry is and which materials it
    /// uses. This is a planning tool, not gameplay code — the Soviet district is placed from
    /// the numbers it writes (see [Debug] WorldScan).
    /// </summary>
    internal static class WorldScan
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Run(string dir)
        {            var sb = new StringBuilder(1 << 21);
            var t0 = Time.realtimeSinceStartup;

            sb.AppendLine("# BabbdiCoop world scan");
            sb.AppendLine($"time={DateTime.Now:yyyy-MM-dd HH:mm:ss} scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            var cam = Camera.main;
            if (cam != null)
                sb.AppendLine($"camera pos={V(cam.transform.position)} fwd={V(cam.transform.forward)}");

            var byMesh = new Dictionary<string, MeshGroup>();
            var mats = new Dictionary<string, MatInfo>();
            var big = new List<Inst>();
            var all = Resources.FindObjectsOfTypeAll<Renderer>();
            int total = 0, scene = 0;

            foreach (var r in all)
            {
                total++;
                if (r == null) continue;
                var go = r.gameObject;
                if (!go.scene.IsValid()) continue;   // asset, not level geometry
                scene++;

                var b = r.bounds;
                if (b.size.x * b.size.y * b.size.z < 0.5f) continue;   // sparks, decals, props

                string meshName = "<none>";
                var mf = go.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) meshName = mf.sharedMesh.name;
                else
                {
                    var smr = go.GetComponent<SkinnedMeshRenderer>();
                    if (smr != null && smr.sharedMesh != null) meshName = smr.sharedMesh.name;
                }

                if (!byMesh.TryGetValue(meshName, out var g))
                {
                    g = new MeshGroup { Name = meshName };
                    byMesh[meshName] = g;
                }
                g.Count++;
                g.Encapsulate(b);

                var inst = new Inst
                {
                    Name = go.name,
                    Mesh = meshName,
                    C = b.center,
                    S = b.size,
                    Y = go.transform.position.y,
                    Path = ParentPath(go.transform, 3),
                    Mat = FirstMat(r)
                };
                big.Add(inst);

                var shared = r.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                {
                    var m = shared[i];
                    string key = m != null ? m.name : "<none>";
                    if (!mats.TryGetValue(key, out var mi))
                    {
                        mi = new MatInfo { Name = key };
                        if (m != null)
                        {
                            mi.Shader = m.shader != null ? m.shader.name : "?";
                            mi.Textures = TextureNames(m);
                        }
                        mats[key] = mi;
                    }
                    mi.Count++;
                    mi.Encapsulate(b);
                }
            }

            sb.AppendLine($"## renderers total={total} inScene={scene} meshGroups={byMesh.Count} materials={mats.Count}");
            sb.AppendLine();

            // ---------- groups by mesh: the vocabulary of the world ----------
            sb.AppendLine("## mesh groups (world AABB, sorted by volume)");
            sb.AppendLine("mesh\tcount\tcenter\t size \tminY\tmaxY\tvolume");
            var groups = new List<MeshGroup>(byMesh.Values);
            groups.Sort((a, b) => b.Volume.CompareTo(a.Volume));
            foreach (var g in groups)
                sb.AppendLine($"{g.Name}\t{g.Count}\t{V(g.Center)}\t{V(g.Size)}\t{F(g.MinY)}\t{F(g.MaxY)}\t{F(g.Volume)}");
            sb.AppendLine();

            // ---------- every sizeable instance ----------
            sb.AppendLine("## instances (volume >= 50 m3, sorted by volume)");
            sb.AppendLine("name\tmesh\tcenter\t size \tposY\tmat\tpath");
            big.Sort((a, b) => b.Vol.CompareTo(a.Vol));
            foreach (var i in big)
            {
                if (i.Vol < 50f) continue;
                sb.AppendLine($"{i.Name}\t{i.Mesh}\t{V(i.C)}\t{V(i.S)}\t{F(i.Y)}\t{i.Mat}\t{i.Path}");
            }
            sb.AppendLine();

            // ---------- materials ----------
            sb.AppendLine("## materials");
            sb.AppendLine("name\tshader\tcount\tcenter\t size \ttextures");
            foreach (var kv in mats)
            {
                var m = kv.Value;
                if (!m.HasBounds) continue;
                sb.AppendLine($"{kv.Key}\t{m.Shader}\t{m.Count}\t{V(m.Center)}\t{V(m.Size)}\t{m.Textures}");
            }
            sb.AppendLine();

            // ---------- a coarse terrain probe ----------
            sb.AppendLine("## top surface probe (vertical raycast, step 40 m, y=250 down; excludes the invisible ceiling)");
            sb.AppendLine("x\tz\ty\tmat\tcollider");
            for (float x = -700f; x <= 700f; x += 40f)
            {
                for (float z = -700f; z <= 900f; z += 40f)
                {
                    if (Physics.Raycast(new Vector3(x, 250f, z), Vector3.down, out var hit, 900f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        string mat = "?";
                        var rend = hit.collider.GetComponent<Renderer>();
                        if (rend != null && rend.sharedMaterial != null) mat = rend.sharedMaterial.name;
                        sb.AppendLine($"{F(x)}\t{F(z)}\t{F(hit.point.y)}\t{mat}\t{hit.collider.name}");
                    }
                }
            }

            var path = Path.Combine(dir, "worldscan.txt");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            return $"{path} ({sb.Length} bytes, {Time.realtimeSinceStartup - t0:F2}s)";
        }

        /// <summary>v0.9.77 — the level's **boundaries and sky**, measured: the skybox material and every
        /// property, the scene roots, the huge/invisible colliders (bounds, layer, name) and the union
        /// of all renderer/collider bounds. This is the data behind the map expansion (see `MapEdits`).</summary>
        public static string MapScan(string dir)
        {
            var sb = new StringBuilder(1 << 20);
            var sc = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            sb.AppendLine("# BabbdiCoop map scan");
            sb.AppendLine($"scene={sc.name} time={DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            var cam = Camera.main;
            if (cam != null)
                sb.AppendLine($"camera pos={V(cam.transform.position)} far={F(cam.farClipPlane)} " +
                              $"clear={cam.clearFlags} bg={cam.backgroundColor} cull={cam.cullingMask}");

            var sky = RenderSettings.skybox;
            sb.AppendLine($"skybox={(sky != null ? sky.name : "none")} " +
                          $"shader={(sky != null && sky.shader != null ? sky.shader.name : "?")}");
            if (sky != null && sky.shader != null)
            {
                var sh = sky.shader;
                for (int i = 0; i < sh.GetPropertyCount(); i++)
                {
                    string p = sh.GetPropertyName(i);
                    var t = sh.GetPropertyType(i);
                    string val = "?";
                    try
                    {
                        if (t == UnityEngine.Rendering.ShaderPropertyType.Texture)
                        {
                            var tx = sky.GetTexture(p);
                            val = tx != null ? $"{tx.name} {tx.width}x{tx.height}" : "null";
                        }
                        else if (t == UnityEngine.Rendering.ShaderPropertyType.Float ||
                                 t == UnityEngine.Rendering.ShaderPropertyType.Range)
                            val = F(sky.GetFloat(p));
                        else if (t == UnityEngine.Rendering.ShaderPropertyType.Color)
                            val = sky.GetColor(p).ToString();
                        else if (t == UnityEngine.Rendering.ShaderPropertyType.Vector)
                            val = sky.GetVector(p).ToString("F2");
                    }
                    catch { }
                    sb.AppendLine($"  sky.{p} ({t}) = {val}");
                }
            }
            sb.AppendLine($"fog={RenderSettings.fog} color={RenderSettings.fogColor} mode={RenderSettings.fogMode} " +
                          $"density={F(RenderSettings.fogDensity)} start={F(RenderSettings.fogStartDistance)} " +
                          $"end={F(RenderSettings.fogEndDistance)} ambient={RenderSettings.ambientLight}");

            // scene roots
            sb.AppendLine("## scene roots");
            foreach (var go in sc.GetRootGameObjects())
            {
                var rs = go.GetComponentsInChildren<Renderer>(true);
                var cs = go.GetComponentsInChildren<Collider>(true);
                Bounds? bb = null;
                foreach (var r in rs) { if (r == null) continue; if (!bb.HasValue) bb = r.bounds; else { var b2 = bb.Value; b2.Encapsulate(r.bounds); bb = b2; } }
                sb.AppendLine($"root {go.name}\tactive={go.activeInHierarchy}\trenderers={rs.Length}\tcolliders={cs.Length}\t" +
                              $"bounds={(bb.HasValue ? V(bb.Value.center) + " / " + V(bb.Value.size) : "-")}");
            }

            // sky / cloud / dome objects
            sb.AppendLine("## sky-ish renderers");
            var all = Resources.FindObjectsOfTypeAll<Renderer>();
            foreach (var r in all)
            {
                if (r == null || !r.gameObject.scene.IsValid()) continue;
                string rpath = ParentPath(r.transform, 4).ToLowerInvariant();
                if (!(rpath.Contains("sky") || rpath.Contains("cloud") || rpath.Contains("dome") ||
                      rpath.Contains("atmos") || rpath.Contains("fog") || rpath.Contains("star") ||
                      rpath.Contains("sun") || rpath.Contains("moon"))) continue;
                sb.AppendLine($"{r.name}\tmat={(r.sharedMaterial != null ? r.sharedMaterial.name : "-")}\t" +
                              $"center={V(r.bounds.center)}\tsize={V(r.bounds.size)}\tpath={ParentPath(r.transform, 4)}");
            }

            // the invisible walls tree, child by child
            var iw = GameObject.Find("---INVISIBLE WALLS");
            if (iw != null)
            {
                sb.AppendLine("## ---INVISIBLE WALLS children");
                foreach (var t in iw.GetComponentsInChildren<Transform>(true))
                {
                    if (t == iw.transform) continue;
                    var c = t.GetComponent<Collider>();
                    var r = t.GetComponent<Renderer>();
                    sb.AppendLine($"{t.name}\tpos={V(t.position)}\tactive={t.gameObject.activeSelf}\t" +
                                  $"collider={(c != null ? c.GetType().Name : "-")}\t" +
                                  $"bounds={(c != null ? V(c.bounds.center) + " / " + V(c.bounds.size) : "-")}\t" +
                                  $"render={(r != null ? r.name : "-")}\tmat={(r != null && r.sharedMaterial != null ? r.sharedMaterial.name : "-")}");
                }
            }

            // water / ground surfaces
            sb.AppendLine("## water / ground renderers");
            foreach (var r in all)
            {
                if (r == null || !r.gameObject.scene.IsValid()) continue;
                string mat = r.sharedMaterial != null ? r.sharedMaterial.name.ToLowerInvariant() : "";
                string nm = r.name.ToLowerInvariant();
                if (!(mat.Contains("water") || mat.Contains("sea") || mat.Contains("ocean") ||
                      mat.Contains("canal") || nm.Contains("water") || nm.Contains("sea") || nm.Contains("ocean")))
                    continue;
                sb.AppendLine($"{r.name}\tmat={(r.sharedMaterial != null ? r.sharedMaterial.name : "-")}\t" +
                              $"center={V(r.bounds.center)}\tsize={V(r.bounds.size)}");
            }

            // colliders: invisible and giant
            var cols = Resources.FindObjectsOfTypeAll<Collider>();            var inv = new List<Collider>();
            var giant = new List<Collider>();
            var seen = new HashSet<int>();
            int nCol = 0;
            Bounds uR = default, uC = default; bool hasR = false, hasC = false;
            foreach (var c in cols)
            {
                if (c == null) continue;
                var go = c.gameObject;
                if (!go.scene.IsValid() || !go.activeInHierarchy || !c.enabled) continue;
                if (!seen.Add(c.GetInstanceID())) continue;
                nCol++;
                var b = c.bounds;
                if (!hasC) { uC = b; hasC = true; } else uC.Encapsulate(b);
                var rr = go.GetComponent<Renderer>() ?? go.GetComponentInChildren<Renderer>();
                if (rr != null) { if (!hasR) { uR = rr.bounds; hasR = true; } else uR.Encapsulate(rr.bounds); }
                if (rr == null) inv.Add(c);
                if (b.size.x > 80f || b.size.y > 80f || b.size.z > 80f) giant.Add(c);
            }
            inv.Sort((a, b) => Vol(b.bounds.size).CompareTo(Vol(a.bounds.size)));
            giant.Sort((a, b) => Vol(b.bounds.size).CompareTo(Vol(a.bounds.size)));
            sb.AppendLine($"## invisible colliders (no renderer on self/children): {inv.Count} of {nCol}");
            for (int i = 0; i < inv.Count && i < 80; i++)
            {
                var c = inv[i]; var go = c.gameObject; var b = c.bounds;
                sb.AppendLine($"{c.name}\t{c.GetType().Name}\tcenter={V(b.center)}\tsize={V(b.size)}\t" +
                              $"layer={LayerMask.LayerToName(go.layer)}({go.layer})\ttrigger={c.isTrigger}\t" +
                              $"static={go.isStatic}\tpath={ParentPath(go.transform, 3)}");
            }
            sb.AppendLine($"## giant colliders (>80 m in a dimension): {giant.Count}");
            for (int i = 0; i < giant.Count && i < 60; i++)
            {
                var c = giant[i]; var go = c.gameObject; var b = c.bounds;
                var rr = go.GetComponent<Renderer>() ?? go.GetComponentInChildren<Renderer>();
                sb.AppendLine($"{c.name}\t{c.GetType().Name}\tcenter={V(b.center)}\tsize={V(b.size)}\t" +
                              $"layer={LayerMask.LayerToName(go.layer)}({go.layer})\ttrigger={c.isTrigger}\t" +
                              $"rend={(rr != null ? rr.name : "NO")}\tpath={ParentPath(go.transform, 3)}");
            }
            sb.AppendLine($"## union renderer bounds={(hasR ? V(uR.center) + " size " + V(uR.size) + " min " + V(uR.min) + " max " + V(uR.max) : "-")}");
            sb.AppendLine($"## union collider bounds={(hasC ? V(uC.center) + " size " + V(uC.size) + " min " + V(uC.min) + " max " + V(uC.max) : "-")}");

            var path = Path.Combine(dir, "mapscan.txt");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            return $"{path} ({sb.Length} bytes)";
        }

        private static float Vol(Vector3 s) => s.x * s.y * s.z;

        private static string FirstMat(Renderer r)
        {
            var m = r.sharedMaterial;
            return m != null ? m.name : "<none>";
        }

        private static string ParentPath(Transform t, int depth)
        {
            var parts = new List<string>();
            var cur = t;
            for (int i = 0; i < depth && cur != null; i++)
            {
                parts.Add(cur.name);
                cur = cur.parent;
            }
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

        private static string TextureNames(Material m)
        {
            var names = new List<string>();
            try
            {
                var sh = m.shader;
                if (sh == null) return "";
                int n = sh.GetPropertyCount();
                for (int i = 0; i < n; i++)
                {
                    if (sh.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Texture) continue;
                    var tx = m.GetTexture(sh.GetPropertyName(i));
                    if (tx != null) names.Add($"{sh.GetPropertyName(i)}={tx.name}");
                }
            }
            catch { }
            return string.Join(",", names.ToArray());
        }

        private static string F(float v) => v.ToString("F2", Inv);
        private static string V(Vector3 v) => $"{F(v.x)},{F(v.y)},{F(v.z)}";

        private sealed class MeshGroup
        {
            public string Name;
            public int Count;
            public Bounds B;
            public bool Has;
            public Vector3 Center, Size;

            public float Volume => Size.x * Size.y * Size.z;
            public Vector3 Min => Center - Size * 0.5f;
            public float MaxY => Center.y + Size.y * 0.5f;
            public float MinY => Center.y - Size.y * 0.5f;

            public void Encapsulate(Bounds b)
            {
                if (!Has) { B = b; Has = true; }
                else B.Encapsulate(b);
                Center = B.center; Size = B.size;
            }
        }

        private sealed class Inst
        {
            public string Name, Mesh, Mat, Path;
            public Vector3 C, S;
            public float Y;
            public float Vol => S.x * S.y * S.z;
        }

        private sealed class MatInfo
        {
            public string Name;
            public string Shader = "?";
            public string Textures = "";
            public int Count;
            public bool HasBounds;
            public Bounds B;
            public Vector3 Center, Size;

            public void Encapsulate(Bounds b)
            {
                if (!HasBounds) { B = b; HasBounds = true; }
                else B.Encapsulate(b);
                Center = B.center; Size = B.size;
            }
        }
    }
}
