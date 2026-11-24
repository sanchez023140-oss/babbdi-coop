using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Text;
using Newtonsoft.Json.Linq;

namespace BabbdiCoop
{
    /// <summary>
    /// Minimal glTF-Binary (GLB) parser вЂ” geometry core. No Unity types in here so
    /// the same file is usable from a console harness. Kenney "Blocky Characters"
    /// GLBs contain POSITION / TEXCOORD_0 / UNSIGNED_SHORT indices and a PNG atlas.
    /// </summary>
    public static class GlbModelLoader
    {
        public class Primitive
        {
            public string Name;
            public Vector3[] Positions;      // world-space (skinned at bind pose) вЂ” used by harness/static path
            public Vector3[] RawPositions;   // mesh-space glTF positions вЂ” used by the skinned Unity path
            public Vector3[] Normals;        // optional authored normals (static props)
            public Vector2[] Uvs;
            public int[] Indices;
            public int MaterialIndex;
            public int SkinIndex = -1;
            public int[] BoneIndices;        // 4 per vertex, indices into Parsed.Skins[SkinIndex].Joints
            public float[] BoneWeights;      // 4 per vertex
        }

        public class NodeInfo
        {
            public string Name;
            public int Parent = -1;
            public Vector3 T = Vector3.Zero;
            public Quaternion R = Quaternion.Identity;
            public Vector3 S = Vector3.One;
            public int Mesh = -1;
            public int Skin = -1;
        }

        public class SkinInfo
        {
            public int[] Joints;
            public Matrix4x4[] InverseBind;  // row-vector convention (transposed vs glTF storage)
        }

        public class AnimChannel
        {
            public int Node;
            public string Path;              // translation | rotation | scale
            public float[] Times;
            public float[] Values;           // flat: count * comps
            public int Comps;
            public string Interpolation;     // LINEAR | STEP | CUBICSPLINE
        }

        public class AnimationInfo
        {
            public string Name;
            public float Duration;
            public List<AnimChannel> Channels = new();
        }

        public class ImageData
        {
            public string Name;
            public byte[] Png; // raw encoded PNG bytes, ready for Texture2D.LoadImage
        }

        public class Parsed
        {
            public List<Primitive> Primitives = new();
            public List<ImageData> Images = new();
            public List<(int matIdx, int texIdx, Vector4 baseColor, bool doubleSided)> Materials = new();
            public List<NodeInfo> Nodes = new();
            public List<SkinInfo> Skins = new();
            public List<AnimationInfo> Animations = new();
            public Vector3 Min, Max;
        }

        public static Parsed Parse(string path, string poseAnimation = "auto")
        {
            var data = File.ReadAllBytes(path);
            if (data.Length < 20) throw new InvalidDataException("GLB too small");
            int magic = BitConverter.ToInt32(data, 0); // 'glTF'
            if (magic != 0x46546C67) throw new InvalidDataException("not a GLB");
            uint version = BitConverter.ToUInt32(data, 4);
            if (version != 2) throw new InvalidDataException($"GLB v{version} unsupported");

            var json = (string)null;
            byte[] bin = null;
            int off = 12;
            while (off + 8 <= data.Length)
            {
                uint chunkLen = BitConverter.ToUInt32(data, off);
                uint chunkType = BitConverter.ToUInt32(data, off + 4);
                var chunk = new byte[chunkLen];
                Buffer.BlockCopy(data, off + 8, chunk, 0, (int)chunkLen);
                if (chunkType == 0x4E4F534A) json = Encoding.UTF8.GetString(chunk); // 'JSON'
                else if (chunkType == 0x004E4942) bin = chunk; // 'BIN\0'
                off += 8 + (int)((chunkLen + 3u) & ~3u);
            }
            if (json == null) throw new InvalidDataException("no JSON chunk");
            var root = JObject.Parse(json);

            var result = new Parsed { Min = Vector3.One * float.MaxValue, Max = Vector3.One * float.MinValue };

            var buffers = (JArray)root["buffers"];
            var views = (JArray)root["bufferViews"];
            var accessors = (JArray)root["accessors"];
            var meshes = (JArray)root["meshes"];
            var images = (JArray)root["images"];
            var textures = (JArray)root["textures"];
            var materials = (JArray)root["materials"];

            // BIN buffer only (external uri buffers are not supported)
            JToken binBuffer = null;
            foreach (var b in buffers ?? new JArray())
                if ((string)b?["uri"] == null) { binBuffer = b; break; }
            if (binBuffer == null || bin == null) throw new InvalidDataException("no glb BIN buffer");

            // images: embedded (bufferView) or external URI (file next to the GLB)
            if (images != null)
            {
                foreach (var img in images)
                {
                    byte[] bytes = null;
                    if (img["bufferView"] != null)
                        bytes = ReadView(bin, views, (int)img["bufferView"]);
                    else if (img["uri"] != null)
                    {
                        var uri = img["uri"].ToString();
                        if (uri.StartsWith("data:"))
                        {
                            var comma = uri.IndexOf(',');
                            if (comma >= 0)
                            {
                                try { bytes = Convert.FromBase64String(uri.Substring(comma + 1)); } catch { }
                            }
                        }
                        else
                        {
                            var p = Path.Combine(Path.GetDirectoryName(path) ?? ".", uri.Replace('/', Path.DirectorySeparatorChar));
                            if (File.Exists(p)) bytes = File.ReadAllBytes(p);
                        }
                    }
                    result.Images.Add(new ImageData
                    {
                        Name = (img["name"] ?? img["uri"])?.ToString(),
                        Png = bytes
                    });
                }
            }

            // material uv symbol transforms (KHR_texture_transform): uv' = uv * scale + offset
            var matUvScale = new List<Vector2>();
            var matUvOffset = new List<Vector2>();
            if (materials != null)
            {
                foreach (var m in materials)
                {
                    var uvS = Vector2.One;
                    var uvO = Vector2.Zero;
                    var tt = m?["pbrMetallicRoughness"]?["baseColorTexture"]?["extensions"]?["KHR_texture_transform"];
                    if (tt != null)
                    {
                        if (tt["scale"] != null)
                        {
                            var s = tt["scale"].ToObject<float[]>();
                            if (s != null && s.Length >= 2) uvS = new Vector2(s[0], s[1]);
                        }
                        if (tt["offset"] != null)
                        {
                            var o = tt["offset"].ToObject<float[]>();
                            if (o != null && o.Length >= 2) uvO = new Vector2(o[0], o[1]);
                        }
                    }
                    matUvScale.Add(uvS);
                    matUvOffset.Add(uvO);
                }
            }

            // nodes / skins / animations (full data for the runtime skeletal animator)
            if (root["nodes"] is JArray nodesJson)
            {
                foreach (var n in nodesJson)
                {
                    var info = new NodeInfo { Name = n["name"]?.ToString() };
                    var t = n["translation"]?.ToObject<float[]>();
                    var r = n["rotation"]?.ToObject<float[]>();
                    var s = n["scale"]?.ToObject<float[]>();
                    if (t != null && t.Length >= 3) info.T = new Vector3(t[0], t[1], t[2]);
                    if (r != null && r.Length >= 4) info.R = new Quaternion(r[0], r[1], r[2], r[3]);
                    if (s != null && s.Length >= 3) info.S = new Vector3(s[0], s[1], s[2]);
                    if (n["mesh"] != null) info.Mesh = (int)n["mesh"];
                    if (n["skin"] != null) info.Skin = (int)n["skin"];
                    result.Nodes.Add(info);
                }
                for (int i = 0; i < nodesJson.Count; i++)
                {
                    var children = nodesJson[i]["children"] as JArray;
                    if (children == null) continue;
                    foreach (var c in children) result.Nodes[(int)c].Parent = i;
                }
            }

            if (root["skins"] is JArray skinsJson)
            {
                foreach (var s in skinsJson)
                {
                    var joints = s["joints"] as JArray;
                    result.Skins.Add(new SkinInfo
                    {
                        Joints = joints != null ? joints.Select(j => (int)j).ToArray() : Array.Empty<int>(),
                        InverseBind = s["inverseBindMatrices"] != null
                            ? ReadMat4(bin, views, accessors, (int)s["inverseBindMatrices"])
                            : Array.Empty<Matrix4x4>()
                    });
                }
            }

            if (root["animations"] is JArray animsJson)
            {
                foreach (var a in animsJson)
                {
                    var anim = new AnimationInfo { Name = a["name"]?.ToString() ?? "?" };
                    var samplers = (JArray)a["samplers"];
                    foreach (var ch in (JArray)a["channels"])
                    {
                        var target = ch?["target"];
                        if (target?["node"] == null || ch["sampler"] == null) continue;
                        var samp = samplers[(int)ch["sampler"]];
                        var times = ReadScalarFloats(bin, views, accessors, (int)samp["input"]);
                        var outAcc = accessors[(int)samp["output"]];
                        int comps = (string)outAcc["type"] == "VEC4" ? 4
                                  : (string)outAcc["type"] == "VEC3" ? 3 : 1;
                        var vals = ReadFloatsAll(bin, views, accessors, (int)samp["output"], comps);
                        anim.Channels.Add(new AnimChannel
                        {
                            Node = (int)target["node"],
                            Path = (string)target["path"],
                            Times = times,
                            Values = vals,
                            Comps = comps,
                            Interpolation = samp["interpolation"]?.ToString() ?? "LINEAR"
                        });
                        if (times.Length > 0 && times[times.Length - 1] > anim.Duration)
                            anim.Duration = times[times.Length - 1];
                    }
                    result.Animations.Add(anim);
                }
            }

            // Per-node local TRS overrides taken from the FIRST keyframe of a pose
            // animation ("Idle" by default): skinned characters look wrong in bind pose.
            var nodeOverrides = new Dictionary<int, float[][]>(); // [0]=t [1]=r [2]=s
            var anims = (JArray)root["animations"];
            if (anims != null && anims.Count > 0 && poseAnimation != "")
            {
                JToken anim = null;
                foreach (var a in anims)
                {
                    var an = (string)a?["name"];
                    if (poseAnimation == "auto" ? an == "Idle" : an == poseAnimation) { anim = a; break; }
                }
                if (anim == null) anim = anims[0];
                var sampArr = (JArray)anim["samplers"];
                foreach (var ch in (JArray)anim["channels"])
                {
                    var target = ch?["target"];
                    if (target == null) continue;
                    int ni = (int)target["node"];
                    var pathName = (string)target["path"];
                    var samp = sampArr[(int)ch["sampler"]];
                    var vals = ReadFirstKeyframe(bin, views, accessors,
                        (int)samp["output"], samp["interpolation"]?.ToString());
                    if (vals == null) continue;
                    if (!nodeOverrides.TryGetValue(ni, out var arr)) nodeOverrides[ni] = arr = new float[3][];
                    if (pathName == "translation") arr[0] = vals;
                    else if (pathName == "rotation") arr[1] = vals;
                    else if (pathName == "scale") arr[2] = vals;
                }
            }

            // traverse the scene node tree; rigid non-skinned parts only
            var nodeXforms = BuildNodeTransforms((JArray)root["nodes"], nodeOverrides);
            int sceneIdx = root["scene"] != null ? (int)root["scene"] : 0;
            var sceneNodes = (JArray)root["scenes"]?[sceneIdx]?["nodes"] ?? new JArray();

            void VisitNode(int nodeIdx)
            {
                var n = ((JArray)root["nodes"])?[nodeIdx];
                if (n == null) return;
                var meshIdx = n["mesh"] != null ? (int)n["mesh"] : -1;
                if (meshIdx >= 0 && meshes != null && meshIdx < meshes.Count)
                {
                    var xf = nodeXforms[nodeIdx];

                    // skin: joint matrices = inverseBind * jointWorld (row-vector convention)
                    List<Matrix4x4> skinMats = null;
                    if (n["skin"] != null && root["skins"] is JArray skinsArr)
                    {
                        var skin = skinsArr[(int)n["skin"]];
                        var joints = skin["joints"] as JArray;
                        if (joints != null && joints.Count > 0)
                        {
                            var ibms = skin["inverseBindMatrices"] != null
                                ? ReadMat4(bin, views, accessors, (int)skin["inverseBindMatrices"])
                                : null;
                            skinMats = new List<Matrix4x4>();
                            for (int j = 0; j < joints.Count; j++)
                            {
                                var jw = nodeXforms[(int)joints[j]];
                                var ib = ibms != null && j < ibms.Length ? ibms[j] : Matrix4x4.Identity;
                                skinMats.Add(ib * jw);
                            }
                        }
                    }

                    var mesh = meshes[meshIdx];
                    var prims = mesh["primitives"] as JArray;
                    if (prims != null)
                        foreach (var p in prims)
                        {
                            var attrs = p["attributes"];
                            var prim = new Primitive
                            {
                                Name = n["name"]?.ToString() ?? mesh["name"]?.ToString()
                            };

                            if (attrs?["POSITION"] != null)
                            {
                                var raw = ReadVec3(bin, views, accessors, (int)attrs["POSITION"]);
                                prim.RawPositions = (Vector3[])raw.Clone();
                                if (skinMats != null && attrs["JOINTS_0"] != null && attrs["WEIGHTS_0"] != null)
                                {
                                    var joints = ReadJoints(bin, views, accessors, (int)attrs["JOINTS_0"]);
                                    var weights = ReadWeights(bin, views, accessors, (int)attrs["WEIGHTS_0"]);
                                    prim.BoneIndices = joints;
                                    prim.BoneWeights = weights;
                                    prim.SkinIndex = (int)n["skin"];
                                    for (int i = 0; i < raw.Length; i++)
                                    {
                                        var p0 = raw[i];
                                        var acc4 = Vector3.Zero;
                                        for (int k = 0; k < 4; k++)
                                        {
                                            float w = weights[i * 4 + k];
                                            if (w <= 0f) continue;
                                            int j = joints[i * 4 + k];
                                            if (j < 0 || j >= skinMats.Count) continue;
                                            acc4 += w * Vector3.Transform(p0, skinMats[j]);
                                        }
                                        raw[i] = acc4;
                                    }
                                }
                                else
                                {
                                    for (int i = 0; i < raw.Length; i++)
                                        raw[i] = Vector3.Transform(raw[i], xf);
                                }
                                prim.Positions = raw;
                                foreach (var q in raw)
                                {
                                    result.Min = Vector3.Min(result.Min, q);
                                    result.Max = Vector3.Max(result.Max, q);
                                }
                            }
                            if (attrs?["NORMAL"] != null)
                                prim.Normals = ReadVec3(bin, views, accessors, (int)attrs["NORMAL"]);

                            if (attrs?["TEXCOORD_0"] != null)
                            {
                                var rawUv = ReadVec2(bin, views, accessors, (int)attrs["TEXCOORD_0"]);                                Vector2 uvS = Vector2.One, uvO = Vector2.Zero;
                                var matI = p["material"] != null ? (int)p["material"] : -1;
                                if (matI >= 0 && matI < matUvScale.Count) { uvS = matUvScale[matI]; uvO = matUvOffset[matI]; }
                                for (int i = 0; i < rawUv.Length; i++)
                                {
                                    var uv = rawUv[i] * uvS + uvO;
                                    // Kenney's atlases carry UVs outside [0,1) and their sampler is
                                    // REPEAT, so those must be wrapped. An atlas that is already
                                    // inside the unit square must NOT be touched: wrapping u = 1.0
                                    // turns it into 0.0, which samples the far edge of the atlas -
                                    // on the coop figures the seam of each trouser leg jumped from
                                    // the cloth block to the skin block and drew a pale stripe
                                    // down the front of both legs.
                                    if (uv.X < 0f || uv.X > 1f) uv.X -= MathF.Floor(uv.X);
                                    if (uv.Y < 0f || uv.Y > 1f) uv.Y -= MathF.Floor(uv.Y);
                                    rawUv[i] = uv;
                                }
                                prim.Uvs = rawUv;
                            }

                            if (p["indices"] != null)
                                prim.Indices = ReadShorts(bin, views, accessors, (int)p["indices"]);
                            if (p["material"] != null)
                                prim.MaterialIndex = (int)p["material"];

                            result.Primitives.Add(prim);
                        }
                }

                var children = n["children"] as JArray;
                if (children != null)
                    foreach (var c in children)
                        VisitNode((int)c);
            }

            foreach (var sn in sceneNodes)
                VisitNode((int)sn);

            if (materials != null)
            {
                foreach (var m in materials)
                {
                    int texIdx = -1;
                    var baseTex = m?["pbrMetallicRoughness"]?["baseColorTexture"];
                    if (baseTex?["index"] != null)
                    {
                        int t = (int)baseTex["index"];
                        if (textures != null && textures[t]?["source"] != null)
                            texIdx = (int)textures[t]["source"];
                    }
                    var bc = m?["pbrMetallicRoughness"]?["baseColorFactor"]?.ToObject<float[]>();
                    var col = bc != null && bc.Length >= 4
                        ? new Vector4(bc[0], bc[1], bc[2], bc[3])
                        : new Vector4(1, 1, 1, 1);
                    result.Materials.Add((-1, texIdx, col, m?["doubleSided"]?.ToObject<bool>() ?? false));
                }
            }

            return result;
        }

        /// <summary>World-space matrix per node (parent chain applied), optional pose overrides.</summary>
        static List<Matrix4x4> BuildNodeTransforms(JArray nodes, Dictionary<int, float[][]> overrides = null)
        {
            var xforms = new List<Matrix4x4>();
            if (nodes == null) return xforms;
            for (int i = 0; i < nodes.Count; i++)
                xforms.Add(Matrix4x4.Identity); // filled below

            Matrix4x4 Compose(int idx)
            {
                var n = nodes[idx];
                float[] t = n?["translation"]?.ToObject<float[]>();
                float[] r = n?["rotation"]?.ToObject<float[]>(); // XYZW order in glTF
                float[] s = n?["scale"]?.ToObject<float[]>();
                if (overrides != null && overrides.TryGetValue(idx, out var ov))
                {
                    if (ov[0] != null) t = ov[0];
                    if (ov[1] != null) r = ov[1];
                    if (ov[2] != null) s = ov[2];
                }
                var m = Matrix4x4.Identity;
                if (s != null && s.Length >= 3) m *= Matrix4x4.CreateScale(s[0], s[1], s[2]);
                if (r != null && r.Length >= 4)
                    m *= Matrix4x4.CreateFromQuaternion(new Quaternion(r[0], r[1], r[2], r[3]));
                if (t != null && t.Length >= 3) m *= Matrix4x4.CreateTranslation(t[0], t[1], t[2]);
                return m;
            }

            // parent-child mapping
            var parentOf = new Dictionary<int, int>();
            for (int i = 0; i < nodes.Count; i++)
            {
                var children = nodes[i]?["children"] as JArray;
                if (children == null) continue;
                foreach (var c in children)
                    parentOf[(int)c] = i;
            }

            Matrix4x4 World(int idx)
            {
                var local = Compose(idx);
                if (!parentOf.TryGetValue(idx, out var p)) return local;
                // System.Numerics uses ROW vectors: v' = v * M.
                // Local must be applied FIRST, then the accumulated parent:
                // M_child = local * M_parent  (this is why the head's scale 0.1
                // must not swallow the torso's translation).
                return local * World(p);
            }

            for (int i = 0; i < nodes.Count; i++)
                xforms[i] = World(i);
            return xforms;
        }

        static Vector3[] ReadVec3(byte[] bin, JArray views, JArray accessors, int accIdx)
        {
            var acc = accessors[accIdx];
            int count = (int)acc["count"];
            int comp = ComponentSize((int)acc["componentType"]);
            int vi = (int)acc["bufferView"];
            int ao = acc["byteOffset"] != null ? (int)acc["byteOffset"] : 0;
            var v = views[vi];
            int bo = (int)(v["byteOffset"] ?? 0) + ao;
            int stride = v["byteStride"] != null ? (int)v["byteStride"] : comp * 3;

            var res = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                int at = bo + i * stride;
                res[i] = new Vector3(
                    comp == 4 ? BitConverter.ToSingle(bin, at) : BitConverter.ToUInt16(bin, at),
                    comp == 4 ? BitConverter.ToSingle(bin, at + comp) : BitConverter.ToUInt16(bin, at + comp),
                    comp == 4 ? BitConverter.ToSingle(bin, at + 2 * comp) : BitConverter.ToUInt16(bin, at + 2 * comp));
            }
            return res;
        }

        static Vector2[] ReadVec2(byte[] bin, JArray views, JArray accessors, int accIdx)
        {
            var acc = accessors[accIdx];
            int count = (int)acc["count"];
            int comp = ComponentSize((int)acc["componentType"]);
            int vi = (int)acc["bufferView"];
            int ao = acc["byteOffset"] != null ? (int)acc["byteOffset"] : 0;
            var v = views[vi];
            int bo = (int)(v["byteOffset"] ?? 0) + ao;
            int stride = v["byteStride"] != null ? (int)v["byteStride"] : comp * 2;

            var uvAccType = (string)acc["type"];
            var res = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                int at = bo + i * stride;
                float x, y;
                if (comp == 4)
                {
                    x = BitConverter.ToSingle(bin, at);
                    y = BitConverter.ToSingle(bin, at + 4);
                }
                else if (comp == 2)
                {
                    x = BitConverter.ToUInt16(bin, at) / 65535f;
                    y = BitConverter.ToUInt16(bin, at + 2) / 65535f;
                }
                else
                {
                    x = bin[at] / 255f;
                    y = bin[at + 1] / 255f;
                }
                res[i] = new Vector2(x, y);
            }
            return res;
        }

        static Matrix4x4[] ReadMat4(byte[] bin, JArray views, JArray accessors, int accIdx)
        {
            var acc = accessors[accIdx];
            int count = (int)acc["count"];
            var res = new Matrix4x4[count];
            for (int i = 0; i < count; i++)
            {
                var v = new float[16];
                var vi = (int)acc["bufferView"];
                int ao = acc["byteOffset"] != null ? (int)acc["byteOffset"] : 0;
                var view = views[vi];
                int bo = (int)(view["byteOffset"] ?? 0) + ao + i * 64;
                for (int k = 0; k < 16; k++) v[k] = BitConverter.ToSingle(bin, bo + k * 4);
                // glTF stores column-major; this sequential fill gives the transposed
                // matrix, which is exactly what the row-vector convention needs.
                res[i] = new Matrix4x4(
                    v[0], v[1], v[2], v[3],
                    v[4], v[5], v[6], v[7],
                    v[8], v[9], v[10], v[11],
                    v[12], v[13], v[14], v[15]);
            }
            return res;
        }

        static int[] ReadJoints(byte[] bin, JArray views, JArray accessors, int accIdx)
        {
            var acc = accessors[accIdx];
            int count = (int)acc["count"];
            int comp = ComponentSize((int)acc["componentType"]);
            var vi = (int)acc["bufferView"];
            int ao = acc["byteOffset"] != null ? (int)acc["byteOffset"] : 0;
            var view = views[vi];
            int bo = (int)(view["byteOffset"] ?? 0) + ao;
            int stride = view["byteStride"] != null ? (int)view["byteStride"] : comp * 4;
            var res = new int[count * 4];
            for (int i = 0; i < count; i++)
                for (int k = 0; k < 4; k++)
                {
                    int at = bo + i * stride + k * comp;
                    res[i * 4 + k] = comp == 2 ? BitConverter.ToUInt16(bin, at) : bin[at];
                }
            return res;
        }

        static float[] ReadWeights(byte[] bin, JArray views, JArray accessors, int accIdx)
        {
            var acc = accessors[accIdx];
            int count = (int)acc["count"];
            int compType = (int)acc["componentType"];
            int comp = ComponentSize(compType);
            var vi = (int)acc["bufferView"];
            int ao = acc["byteOffset"] != null ? (int)acc["byteOffset"] : 0;
            var view = views[vi];
            int bo = (int)(view["byteOffset"] ?? 0) + ao;
            int stride = view["byteStride"] != null ? (int)view["byteStride"] : comp * 4;
            var res = new float[count * 4];
            for (int i = 0; i < count; i++)
                for (int k = 0; k < 4; k++)
                {
                    int at = bo + i * stride + k * comp;
                    res[i * 4 + k] = compType == 5126 ? BitConverter.ToSingle(bin, at)
                        : compType == 5123 ? BitConverter.ToUInt16(bin, at) / 65535f
                        : bin[at] / 255f;
                }
            return res;
        }

        /// <summary>First keyframe value of an animation sampler output (VEC3 or VEC4).</summary>
        static float[] ReadFirstKeyframe(byte[] bin, JArray views, JArray accessors, int accIdx, string interpolation)
        {
            var acc = accessors[accIdx];
            var type = (string)acc["type"];
            int comps = type == "VEC4" ? 4 : type == "VEC3" ? 3 : type == "VEC2" ? 2 : 0;
            if (comps == 0) return null;
            int comp = ComponentSize((int)acc["componentType"]);
            if (comp != 4) return null; // only float samplers expected here
            int vi = (int)acc["bufferView"];
            int ao = acc["byteOffset"] != null ? (int)acc["byteOffset"] : 0;
            var view = views[vi];
            int stride = view["byteStride"] != null ? (int)view["byteStride"] : comp * comps;
            // CUBICSPLINE stores (inTangent, value, outTangent) per keyframe
            int valueIdx = interpolation == "CUBICSPLINE" ? 1 : 0;
            int bo = (int)(view["byteOffset"] ?? 0) + ao + valueIdx * stride;
            var res = new float[comps];
            for (int k = 0; k < comps; k++) res[k] = BitConverter.ToSingle(bin, bo + k * 4);
            return res;
        }

        static float[] ReadScalarFloats(byte[] bin, JArray views, JArray accessors, int accIdx)
        {
            var acc = accessors[accIdx];
            int count = (int)acc["count"];
            int comp = ComponentSize((int)acc["componentType"]);
            int vi = (int)acc["bufferView"];
            int ao = acc["byteOffset"] != null ? (int)acc["byteOffset"] : 0;
            var view = views[vi];
            int bo = (int)(view["byteOffset"] ?? 0) + ao;
            int stride = view["byteStride"] != null ? (int)view["byteStride"] : comp;
            var res = new float[count];
            for (int i = 0; i < count; i++)
                res[i] = comp == 4 ? BitConverter.ToSingle(bin, bo + i * stride)
                                   : BitConverter.ToUInt16(bin, bo + i * stride) / 65535f;
            return res;
        }

        static float[] ReadFloatsAll(byte[] bin, JArray views, JArray accessors, int accIdx, int comps)
        {
            var acc = accessors[accIdx];
            int count = (int)acc["count"]; // may include CUBICSPLINE tangents (3Г—)
            int comp = ComponentSize((int)acc["componentType"]);
            int vi = (int)acc["bufferView"];
            int ao = acc["byteOffset"] != null ? (int)acc["byteOffset"] : 0;
            var view = views[vi];
            int bo = (int)(view["byteOffset"] ?? 0) + ao;
            int stride = view["byteStride"] != null ? (int)view["byteStride"] : comp * comps;
            var res = new float[count * comps];
            for (int i = 0; i < count; i++)
                for (int k = 0; k < comps; k++)
                {
                    int at = bo + i * stride + k * comp;
                    res[i * comps + k] = comp == 4 ? BitConverter.ToSingle(bin, at)
                        : (int)acc["componentType"] == 5123 ? BitConverter.ToUInt16(bin, at) / 65535f
                        : bin[at] / 255f;
                }
            return res;
        }

        static byte[] ReadView(byte[] bin, JArray views, int viewIdx)
        {
            var v = views[viewIdx];
            int bo = v["byteOffset"] != null ? (int)v["byteOffset"] : 0;
            int len = (int)v["byteLength"];
            var outp = new byte[len];
            Buffer.BlockCopy(bin, bo, outp, 0, len);
            return outp;
        }

        static byte[] ReadViewSlice(byte[] bin, JArray views, int viewIdx, int byteOffset, int elementCount, int componentSize, int components)
        {
            var v = views[viewIdx];
            int bo = (v["byteOffset"] != null ? (int)v["byteOffset"] : 0);
            int stride = v["byteStride"] != null ? (int)v["byteStride"] : componentSize * components;
            int total = elementCount * stride;

            var flat = new float[elementCount * components];
            // handle the accessors that pack tightly (stride == comp size * comps)
            for (int i = 0; i < elementCount; i++)
            {
                int start = bo + byteOffset + i * stride;
                for (int c = 0; c < components; c++)
                {
                    int at = start + c * componentSize;
                    float f = componentSize switch
                    {
                        4 => BitConverter.ToSingle(bin, at),
                        2 => BitConverter.ToUInt16(bin, at), // unsigned short normalized
                        1 => bin[at] / 255f,
                        _ => 0f,
                    };
                    flat[i * components + c] = f;
                }
            }
            return flat.Select(f => BitConverter.GetBytes(f))
                       .SelectMany(b => b).ToArray();
        }

        static Vector3[] ReadFloats(byte[] bin, JArray views, JArray accessors, int accIdx, int comps)
        {
            var acc = accessors[accIdx];
            if ((string)acc["type"] == "VEC3" && comps == 3)
            {
                int vi = (int)acc["bufferView"];
                int ao = acc["byteOffset"] != null ? (int)acc["byteOffset"] : 0;
                int count = (int)acc["count"];
                var v = views[vi];
                int bo = (int)(v["byteOffset"] ?? 0) + ao;
                int compSize = ComponentSize((int)acc["componentType"]);
                int stride = v["byteStride"] != null ? (int)v["byteStride"] : compSize * comps;

                var res = new Vector3[count];
                for (int i = 0; i < count; i++)
                {
                    int at = bo + i * stride;
                    float x = compSize == 4 ? BitConverter.ToSingle(bin, at) : BitConverter.ToUInt16(bin, at);
                    float y = compSize == 4 ? BitConverter.ToSingle(bin, at + compSize) : BitConverter.ToUInt16(bin, at + compSize);
                    float z = compSize == 4 ? BitConverter.ToSingle(bin, at + compSize * 2) : BitConverter.ToUInt16(bin, at + compSize * 2);
                    res[i] = new Vector3(x, y, z);
                }
                return res;
            }
            if ((string)acc["type"] == "VEC2" && comps == 2)
            {
                int vi = (int)acc["bufferView"];
                int ao = acc["byteOffset"] != null ? (int)acc["byteOffset"] : 0;
                int count = (int)acc["count"];
                var v = views[vi];
                int bo = (int)(v["byteOffset"] ?? 0) + ao;
                int compSize = ComponentSize((int)acc["componentType"]);
                int stride = v["byteStride"] != null ? (int)v["byteStride"] : compSize * comps;

                var res = new Vector3[count];
                for (int i = 0; i < count; i++)
                {
                    int at = bo + i * stride;
                    float x = compSize == 4 ? BitConverter.ToSingle(bin, at) : BitConverter.ToUInt16(bin, at) / 65535f;
                    float y = compSize == 4 ? BitConverter.ToSingle(bin, at + compSize) : BitConverter.ToUInt16(bin, at + compSize) / 65535f;
                    res[i] = new Vector3(x, y, 0f);
                }
                return res;
            }
            throw new InvalidDataException($"unsupported accessor type {acc["type"]}");
        }

        static int[] ReadShorts(byte[] bin, JArray views, JArray accessors, int accIdx)
        {
            var acc = accessors[accIdx];
            int vi = (int)acc["bufferView"];
            int ao = acc["byteOffset"] != null ? (int)acc["byteOffset"] : 0;
            int count = (int)acc["count"];
            int comp = ComponentSize((int)acc["componentType"]);

            var v = views[vi];
            int bo = (int)(v["byteOffset"] ?? 0) + ao;
            int stride = v["byteStride"] != null ? (int)v["byteStride"] : comp;

            var res = new int[count];
            for (int i = 0; i < count; i++)
            {
                int at = bo + i * stride;
                res[i] = comp == 2 ? BitConverter.ToUInt16(bin, at)
                       : comp == 4 ? (int)BitConverter.ToUInt32(bin, at)
                       : bin[at];
            }
            return res;
        }

        static int ComponentSize(int componentType) => componentType switch
        {
            5120 => 1, 5121 => 1,
            5122 => 2, 5123 => 2,
            5125 => 4, 5126 => 4,
            _ => 4,
        };
    }
}

