using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace BabbdiCoop
{
    /// <summary>
    /// Runtime OBJ loader for the coop avatar (Kenney "Blocky Characters" shape):
    /// v / vt / f faces in triangles & quads with negative indices, groups split by
    /// usemtl, per-material diffuse from MTL (Kd color + optional map_Kd PNG texture).
    /// </summary>
    public static class ObjModelLoader
    {
        public static GameObject Load(string objPath)
        {
            var objText = File.ReadAllText(objPath);
            var dir = Path.GetDirectoryName(objPath);

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();

            // material -> triangle list of (outVertIndex) triples; out vertices are
            // duplicated per corner so each material group has its own flat buffers
            var groupOrder = new List<string>();
            var groupFaces = new Dictionary<string, List<int[]>>();
            var groupVerts = new Dictionary<string, List<Vector3>>();
            var groupUvs = new Dictionary<string, List<Vector2>>();

            void StartGroup(string name)
            {
                if (groupFaces.ContainsKey(name)) return;
                groupOrder.Add(name);
                groupFaces[name] = new List<int[]>();
                groupVerts[name] = new List<Vector3>();
                groupUvs[name] = new List<Vector2>();
            }

            var currentMat = "default";
            StartGroup(currentMat);

            foreach (var rawLine in objText.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                var sp = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (sp.Length == 0) continue;

                switch (sp[0])
                {
                    case "v":
                        if (sp.Length >= 4)
                        {
                            // ignore optional trailing vertex-color triple
                            verts.Add(new Vector3(F(sp, 1), F(sp, 2), F(sp, 3)));
                        }
                        break;

                    case "vt":
                        if (sp.Length >= 3)
                            uvs.Add(new Vector2(F(sp, 1), F(sp, 2)));
                        break;

                    case "usemtl":
                        currentMat = sp.Length > 1 ? sp[1] : "default";
                        StartGroup(currentMat);
                        break;

                    case "f":
                        {
                            if (sp.Length < 4) break;
                            var faces = groupFaces[currentMat];
                            var gv = groupVerts[currentMat];
                            var gu = groupUvs[currentMat];

                            var corners = new int[sp.Length - 1]; // out vertex indices
                            for (int i = 1; i < sp.Length; i++)
                            {
                                var parts = sp[i].Split('/');
                                int vi = int.Parse(parts[0], CultureInfo.InvariantCulture);
                                if (vi < 0) vi += verts.Count + 1;

                                int ti = (uvs.Count > 0 && parts.Length > 1 && parts[1].Length > 0)
                                    ? int.Parse(parts[1], CultureInfo.InvariantCulture)
                                    : 0;
                                if (ti < 0) ti += uvs.Count + 1;

                                var outIdx = gv.Count;
                                gv.Add(verts[vi - 1]);
                                gu.Add(uvs.Count > 0 && ti > 0 ? uvs[ti - 1] : Vector2.zero);
                                corners[i - 1] = outIdx;
                            }

                            for (int i = 2; i < corners.Length; i++)
                                faces.Add(new[] { corners[0], corners[i - 1], corners[i] });
                        }
                        break;
                }
            }

            if (verts.Count == 0) return null;

            var mtl = ParseMtl(TryReadMtl(objText, dir), dir);

            var root = new GameObject("RemoteAvatarMesh");
            Shader standard = Shader.Find("Standard");
            if (standard == null) standard = Shader.Find("Sprites/Default");

            foreach (var name in groupOrder)
            {
                var gv = groupVerts[name];
                var gu = groupUvs[name];
                var faces = groupFaces[name];
                if (faces.Count == 0) continue;

                var v = new Vector3[faces.Count * 3];
                var u = new Vector2[faces.Count * 3];
                var idx = new int[faces.Count * 3];
                for (int i = 0; i < faces.Count; i++)
                {
                    var f = faces[i];
                    v[i * 3 + 0] = gv[f[0]];
                    v[i * 3 + 1] = gv[f[1]];
                    v[i * 3 + 2] = gv[f[2]];
                    u[i * 3 + 0] = gu[f[0]];
                    u[i * 3 + 1] = gu[f[1]];
                    u[i * 3 + 2] = gu[f[2]];
                    idx[i * 3 + 0] = i * 3 + 0;
                    idx[i * 3 + 1] = i * 3 + 1;
                    idx[i * 3 + 2] = i * 3 + 2;
                }

                var go = new GameObject(name);
                go.transform.SetParent(root.transform, false);
                var meshFilter = go.AddComponent<MeshFilter>();
                var meshRenderer = go.AddComponent<MeshRenderer>();

                var mesh = new Mesh();
                mesh.SetVertices(new List<Vector3>(v));
                if (gu.AnyNonZero())
                    mesh.SetUVs(0, new List<Vector2>(u));
                mesh.SetTriangles(idx, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                meshFilter.sharedMesh = mesh;

                var color = Color.white;
                Texture2D tex = null;
                if (mtl.TryGetValue(name, out var info))
                {
                    color = info.color;
                    tex = info.texture;
                }

                var material = new Material(standard) { color = color };
                if (tex != null) material.mainTexture = tex;
                meshRenderer.material = material;
            }

            Normalize(root);
            return root;
        }

        private class MtlInfo { public Color color = Color.white; public Texture2D texture; }

        private static Dictionary<string, MtlInfo> ParseMtl(string mtlText, string dir)
        {
            var result = new Dictionary<string, MtlInfo>();
            if (mtlText == null) return result;

            MtlInfo current = null;
            foreach (var rawLine in mtlText.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.StartsWith("newmtl"))
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var name = parts.Length > 1 ? parts[1] : null;
                    if (name != null)
                    {
                        current = new MtlInfo();
                        result[name] = current;
                    }
                }
                else if (current != null && line.StartsWith("Kd"))
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4)
                        current.color = new Color(F(parts, 1), F(parts, 2), F(parts, 3));
                }
                else if (current != null && line.StartsWith("map_Kd"))
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        var texPath = Path.Combine(dir ?? string.Empty, parts[1].Replace('/', Path.DirectorySeparatorChar));
                        if (File.Exists(texPath))
                        {
                            try
                            {
                                var tex = new Texture2D(2, 2);
                                if (tex.LoadImage(File.ReadAllBytes(texPath)))
                                {
                                    tex.wrapMode = TextureWrapMode.Clamp;
                                    current.texture = tex;
                                }
                            }
                            catch (System.Exception swallowed) { CoopDiag.Swallowed(swallowed); }
                        }
                    }
                }
            }
            return result;
        }

        /// Scale to ~1.55 m tall, feet on transform origin. OBJ is Y-up as exported.
        private static void Normalize(GameObject root)
        {
            root.transform.localScale = Vector3.one;
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;

            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }
            float height = bounds.size.y;
            if (height < 0.001f) return;
            float scale = 1.55f / height;
            root.transform.localScale = Vector3.one * scale;
            float lowest = bounds.min.y * scale;
            var shift = Vector3.up * -lowest;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
                r.transform.position += shift;
        }

        private static string TryReadMtl(string objText, string dir)
        {
            foreach (var line in objText.Split('\n'))
            {
                var t = line.Trim();
                if (t.StartsWith("mtllib") && t.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length > 1)
                {
                    var mtlFile = t.Substring(6).Trim();
                    var path = Path.Combine(dir ?? string.Empty, mtlFile);
                    if (File.Exists(path)) return File.ReadAllText(path);
                }
            }
            return null;
        }

        private static float F(string[] parts, int i)
        {
            if (i >= parts.Length) return 0f;
            float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v);
            return v;
        }

        private static bool AnyNonZero(this List<Vector2> list)
        {
            foreach (var v in list) if (v != Vector2.zero) return true;
            return false;
        }
    }
}
