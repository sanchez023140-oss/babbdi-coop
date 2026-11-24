using System;
using System.Collections.Generic;
using System.Numerics;

// Diagnostic helper for the harness: skins the mesh at an arbitrary animation time
// and writes the result into Primitive.Positions, so the rasterizer can render it.
// Uses exactly the same row-matrix math as GlbModelLoader / GlbAnimator.

static class PoseSampler
{
    public static void Apply(BabbdiCoop.GlbModelLoader.Parsed parsed, string clipName, float time)
    {
        var anim = parsed.Animations.Find(a => string.Equals(a.Name, clipName, StringComparison.OrdinalIgnoreCase));
        if (anim == null)
        {
            Console.WriteLine($"PoseSampler: clip '{clipName}' not found");
            return;
        }

        var chanByNode = new Dictionary<int, Dictionary<string, BabbdiCoop.GlbModelLoader.AnimChannel>>();
        foreach (var ch in anim.Channels)
        {
            if (!chanByNode.TryGetValue(ch.Node, out var d)) chanByNode[ch.Node] = d = new();
            d[ch.Path] = ch;
        }

        // local transforms: sampled where the clip animates the node, rest otherwise
        var local = new Matrix4x4[parsed.Nodes.Count];
        for (int i = 0; i < parsed.Nodes.Count; i++)
        {
            var n = parsed.Nodes[i];
            var t = n.T; var r = n.R; var s = n.S;
            if (chanByNode.TryGetValue(i, out var chans))
            {
                if (chans.TryGetValue("translation", out var ct)) { var v = Sample(ct, time); t = new Vector3(v[0], v[1], v[2]); }
                if (chans.TryGetValue("rotation", out var cr)) { var v = Sample(cr, time); r = new Quaternion(v[0], v[1], v[2], v[3]); }
                if (chans.TryGetValue("scale", out var cs)) { var v = Sample(cs, time); s = new Vector3(v[0], v[1], v[2]); }
            }
            local[i] = Matrix4x4.CreateScale(s) *
                       Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(r)) *
                       Matrix4x4.CreateTranslation(t);
        }

        var world = new Matrix4x4[parsed.Nodes.Count];
        var state = new int[parsed.Nodes.Count];
        for (int i = 0; i < parsed.Nodes.Count; i++) Resolve(i);

        void Resolve(int i)
        {
            if (state[i] != 0) return;
            state[i] = 1;
            int p = parsed.Nodes[i].Parent;
            if (p >= 0) Resolve(p);
            world[i] = p >= 0 ? local[i] * world[p] : local[i];
            state[i] = 2;
        }

        foreach (var prim in parsed.Primitives)
        {
            if (prim.SkinIndex < 0 || prim.RawPositions == null || prim.BoneIndices == null || prim.BoneWeights == null)
                continue;
            var skin = parsed.Skins[prim.SkinIndex];
            var mats = new Matrix4x4[skin.Joints.Length];
            for (int j = 0; j < skin.Joints.Length; j++)
            {
                var ib = j < skin.InverseBind.Length ? skin.InverseBind[j] : Matrix4x4.Identity;
                mats[j] = ib * world[skin.Joints[j]];
            }

            var outPos = new Vector3[prim.RawPositions.Length];
            for (int i = 0; i < outPos.Length; i++)
            {
                var p0 = prim.RawPositions[i];
                Vector3 acc = Vector3.Zero;
                float wsum = 0f;
                for (int k = 0; k < 4; k++)
                {
                    float w = prim.BoneWeights[i * 4 + k];
                    if (w <= 0f) continue;
                    int j = prim.BoneIndices[i * 4 + k];
                    if (j < 0 || j >= mats.Length) continue;
                    acc += w * Vector3.Transform(p0, mats[j]);
                    wsum += w;
                }
                outPos[i] = wsum > 1e-6f ? acc / wsum : p0;
            }
            prim.Positions = outPos;
        }

        Console.WriteLine($"PoseSampler: clip '{clipName}' at t={time:F2}s applied");
    }

    static float[] Sample(BabbdiCoop.GlbModelLoader.AnimChannel ch, float t)
    {
        int comps = ch.Comps, count = ch.Times.Length;
        bool cubic = ch.Interpolation == "CUBICSPLINE";
        int stride = cubic ? 3 : 1;
        int valueIdx = cubic ? 1 : 0;
        int lo, hi;
        if (count == 1 || t <= ch.Times[0]) { lo = hi = 0; }
        else if (t >= ch.Times[count - 1]) { lo = hi = count - 1; }
        else
        {
            lo = 0; hi = count - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (ch.Times[mid] <= t) lo = mid; else hi = mid;
            }
        }
        var res = new float[comps];
        int b0 = (lo * stride + valueIdx) * comps;
        if (lo == hi || ch.Interpolation == "STEP")
        {
            for (int k = 0; k < comps; k++) res[k] = ch.Values[b0 + k];
            return res;
        }
        float t0 = ch.Times[lo], t1 = ch.Times[hi];
        float a = t1 > t0 ? (t - t0) / (t1 - t0) : 0f;
        int b1 = (hi * stride + valueIdx) * comps;
        for (int k = 0; k < comps; k++)
            res[k] = ch.Values[b0 + k] + (ch.Values[b1 + k] - ch.Values[b0 + k]) * a;
        return res;
    }
}
