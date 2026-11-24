using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Rigger
{
    /// <summary>
    /// Minimal glTF 2.0 binary writer: appends float/ushort/ubyte accessors into one
    /// BIN buffer. Every accessor gets its own tightly packed bufferView, which keeps
    /// the code trivial and the files small enough for a 284-triangle avatar.
    /// </summary>
    public class GlbOut
    {
        public JObject Root = new JObject();
        public JArray Views = new JArray();
        public JArray Accessors = new JArray();
        public JArray Meshes = new JArray();
        public JArray Nodes = new JArray();
        public JArray Materials = new JArray();
        public JArray Textures = new JArray();
        public JArray Images = new JArray();
        public JArray Samplers = new JArray();
        public JArray Scenes = new JArray();
        public JArray Skins = new JArray();
        public JArray Animations = new JArray();

        private readonly MemoryStream bin = new MemoryStream();

        /// <summary>Seeds a writer with an existing glTF (all accessors/views + its BIN)
        /// so new geometry can be appended without re-packing animation data.</summary>
        public static GlbOut FromExisting(JObject root, byte[] binData)
        {
            var g = new GlbOut();
            void Copy(string name, JArray target)
            {
                if (root[name] is JArray arr)
                    foreach (var t in arr) target.Add(t.DeepClone());
            }
            Copy("bufferViews", g.Views);
            Copy("accessors", g.Accessors);
            Copy("nodes", g.Nodes);
            Copy("skins", g.Skins);
            Copy("animations", g.Animations);
            Copy("materials", g.Materials);
            Copy("textures", g.Textures);
            Copy("images", g.Images);
            Copy("samplers", g.Samplers);
            g.bin.Write(binData, 0, binData.Length);
            return g;
        }

        public int AddView(byte[] data, int target = 0)
        {
            Pad4();
            int offset = (int)bin.Length;
            bin.Write(data, 0, data.Length);
            var v = new JObject
            {
                ["buffer"] = 0,
                ["byteOffset"] = offset,
                ["byteLength"] = data.Length
            };
            if (target != 0) v["target"] = target;
            Views.Add(v);
            return Views.Count - 1;
        }

        public int AddAccessor(int view, string type, int componentType, int count,
                               Vector3? min = null, Vector3? maxv = null, bool normalized = false)
        {
            var a = new JObject
            {
                ["bufferView"] = view,
                ["componentType"] = componentType,
                ["count"] = count,
                ["type"] = type
            };
            if (normalized) a["normalized"] = true;
            if (min.HasValue) a["min"] = new JArray(min.Value.X, min.Value.Y, min.Value.Z);
            if (maxv.HasValue) a["max"] = new JArray(maxv.Value.X, maxv.Value.Y, maxv.Value.Z);
            Accessors.Add(a);
            return Accessors.Count - 1;
        }

        public int AddVec3(Vector3[] data, bool positions = false)
        {
            var bytes = new byte[data.Length * 12];
            Vector3 mn = new Vector3(float.MaxValue), mx = new Vector3(float.MinValue);
            for (int i = 0; i < data.Length; i++)
            {
                BitConverter.TryWriteBytes(new Span<byte>(bytes, i * 12, 4), data[i].X);
                BitConverter.TryWriteBytes(new Span<byte>(bytes, i * 12 + 4, 4), data[i].Y);
                BitConverter.TryWriteBytes(new Span<byte>(bytes, i * 12 + 8, 4), data[i].Z);
                mn = Vector3.Min(mn, data[i]);
                mx = Vector3.Max(mx, data[i]);
            }
            int v = AddView(bytes, 34962);
            return AddAccessor(v, "VEC3", 5126, data.Length,
                               positions ? mn : null, positions ? mx : null);
        }

        public int AddVec2(Vector2[] data)
        {
            var bytes = new byte[data.Length * 8];
            for (int i = 0; i < data.Length; i++)
            {
                BitConverter.TryWriteBytes(new Span<byte>(bytes, i * 8, 4), data[i].X);
                BitConverter.TryWriteBytes(new Span<byte>(bytes, i * 8 + 4, 4), data[i].Y);
            }
            int v = AddView(bytes, 34962);
            return AddAccessor(v, "VEC2", 5126, data.Length);
        }

        public int AddVec4(float[] data)
        {
            var bytes = new byte[data.Length * 4];
            for (int i = 0; i < data.Length; i++)
                BitConverter.TryWriteBytes(new Span<byte>(bytes, i * 4, 4), data[i]);
            int v = AddView(bytes, 34962);
            return AddAccessor(v, "VEC4", 5126, data.Length / 4);
        }

        public int AddUshort4(int[] data)
        {
            var bytes = new byte[data.Length * 2];
            for (int i = 0; i < data.Length; i++)
                BitConverter.TryWriteBytes(new Span<byte>(bytes, i * 2, 2), (ushort)data[i]);
            int v = AddView(bytes, 34962);
            return AddAccessor(v, "VEC4", 5123, data.Length / 4);
        }

        public int AddIndices(int[] data)
        {
            var bytes = new byte[data.Length * 2];
            for (int i = 0; i < data.Length; i++)
                BitConverter.TryWriteBytes(new Span<byte>(bytes, i * 2, 2), (ushort)data[i]);
            int v = AddView(bytes, 34963);
            return AddAccessor(v, "SCALAR", 5123, data.Length);
        }

        /// <summary>Raw PNG/JPG bytes → image + sampler + texture, returns the texture index.</summary>
        public int AddTexture(byte[] png, string name, string mimeType = "image/png")
        {
            int view = AddView(png);
            Images.Add(new JObject { ["name"] = name, ["bufferView"] = view, ["mimeType"] = mimeType });
            Samplers.Add(new JObject { ["magFilter"] = 9729, ["minFilter"] = 9987, ["wrapS"] = 10497, ["wrapT"] = 10497 });
            Textures.Add(new JObject { ["sampler"] = Samplers.Count - 1, ["source"] = Images.Count - 1 });
            return Textures.Count - 1;
        }

        public int AddMaterial(int texture, string name, float[] baseColorFactor = null)
        {
            var m = new JObject
            {
                ["name"] = name,
                ["pbrMetallicRoughness"] = new JObject
                {
                    ["baseColorFactor"] = new JArray(baseColorFactor ?? new[] { 1f, 1f, 1f, 1f }),
                    ["metallicFactor"] = 0.0,
                    ["roughnessFactor"] = 0.85
                },
                ["doubleSided"] = false
            };
            if (texture >= 0)
                m["pbrMetallicRoughness"]["baseColorTexture"] = new JObject { ["index"] = texture };
            Materials.Add(m);
            return Materials.Count - 1;
        }

        public int AddNode(JObject node)
        {
            Nodes.Add(node);
            return Nodes.Count - 1;
        }

        public void Finalize(string path, int[] sceneRoots, string generator = "BabbdiCoop Rigger")
        {
            Pad4();
            var binBytes = bin.ToArray();

            var json = new JObject
            {
                ["asset"] = new JObject { ["version"] = "2.0", ["generator"] = generator },
                ["scene"] = 0
            };
            Scenes.Add(new JObject { ["name"] = "Scene", ["nodes"] = new JArray(sceneRoots) });
            json["scenes"] = Scenes;
            if (Nodes.Count > 0) json["nodes"] = Nodes;
            if (Meshes.Count > 0) json["meshes"] = Meshes;
            if (Accessors.Count > 0) json["accessors"] = Accessors;
            if (Views.Count > 0) json["bufferViews"] = Views;
            if (Materials.Count > 0) json["materials"] = Materials;
            if (Textures.Count > 0) json["textures"] = Textures;
            if (Images.Count > 0) json["images"] = Images;
            if (Samplers.Count > 0) json["samplers"] = Samplers;
            if (Skins.Count > 0) json["skins"] = Skins;
            if (Animations.Count > 0) json["animations"] = Animations;
            json["buffers"] = new JArray(new JObject { ["byteLength"] = binBytes.Length });

            var jsonBytes = Encoding.UTF8.GetBytes(json.ToString(Formatting.None));
            int jsonPad = (4 - jsonBytes.Length % 4) % 4;
            var jsonPadded = new byte[jsonBytes.Length + jsonPad];
            Array.Copy(jsonBytes, jsonPadded, jsonBytes.Length);
            for (int i = jsonBytes.Length; i < jsonPadded.Length; i++) jsonPadded[i] = 0x20; // spaces

            using var fs = File.Create(path);
            using var w = new BinaryWriter(fs);
            int total = 12 + 8 + jsonPadded.Length + 8 + binBytes.Length;
            w.Write(0x46546C67);          // 'glTF'
            w.Write(2u);                  // version
            w.Write((uint)total);
            w.Write((uint)jsonPadded.Length);
            w.Write(0x4E4F534A);          // 'JSON'
            w.Write(jsonPadded);
            w.Write((uint)binBytes.Length);
            w.Write(0x004E4942);          // 'BIN\0'
            w.Write(binBytes);
        }

        private void Pad4()
        {
            while (bin.Length % 4 != 0) bin.WriteByte(0);
        }
    }
}
