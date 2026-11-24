using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Numerics;

// Offline harness for GLB: parse with the same GlbModelLoader the plugin uses and
// render front/back so we SEE what the coop avatar becomes in-game.

class Program
{
    static void Main(string[] args)
    {
        var glbPath = args.Length > 0 && File.Exists(args[0])
            ? args[0]
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
                "downloads", "blockycharacters", "Models", "GLB format", "character-r.glb"));
        Console.WriteLine($"Parsing {Path.GetFileName(glbPath)}");

        var parsed = BabbdiCoop.GlbModelLoader.Parse(glbPath);
        foreach (var p in parsed.Primitives)
        {
            if (p.Positions == null) { Console.WriteLine($"{p.Name}: NO POSITIONS"); continue; }
            float x0 = p.Positions.Min(v => v.X), x1 = p.Positions.Max(v => v.X);
            float y0 = p.Positions.Min(v => v.Y), y1 = p.Positions.Max(v => v.Y);
            float z0 = p.Positions.Min(v => v.Z), z1 = p.Positions.Max(v => v.Z);
            Console.WriteLine($"{p.Name}: X {x0:F2}..{x1:F2}  Y {y0:F2}..{y1:F2}  Z {z0:F2}..{z1:F2}");
        }
        Console.WriteLine($"primitives: {string.Join(", ", parsed.Primitives.Select(p => $"{p.Name}:{(p.Indices?.Length ?? 0) / 3}f"))}");
        Console.WriteLine($"bounds Y {parsed.Min.Y:F3}..{parsed.Max.Y:F3} (h {parsed.Max.Y - parsed.Min.Y:F3})  X {parsed.Min.X:F3}..{parsed.Max.X:F3}  Z {parsed.Min.Z:F3}..{parsed.Max.Z:F3}");
        Console.WriteLine($"images: {parsed.Images.Count} ({string.Join(", ", parsed.Images.Select(i => $"{i.Name} {i.Png.Length}B"))})");
        Console.WriteLine($"materials: {string.Join(", ", parsed.Materials.Select(m => $"tex={m.Item2} col={m.Item3}"))}");

        texCache = new();
        foreach (var img in parsed.Images)
        {
            try
            {
                using var ms = new MemoryStream(img.Png);
                texCache[img] = new Bitmap(ms);
                Console.WriteLine($"  texture {img.Name}: {texCache[img].Width}x{texCache[img].Height}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"  texture decode failed {img.Name}: {e.Message}");
            }
        }

        // optional: --Clip:time (e.g. Walk:0.4) renders one animated frame instead
        if (args.Length > 1 && args[1].Contains(':'))
        {
            var parts = args[1].Split(':');
            float t = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
            PoseSampler.Apply(parsed, parts[0], t);
            Render(parsed, $"{parts[0].ToLowerInvariant()}_{parts[1].Replace('.', '_')}", fromPositiveZ: true);
            Render(parsed, $"{parts[0].ToLowerInvariant()}_{parts[1].Replace('.', '_')}_back", fromPositiveZ: false);
            Console.WriteLine("done");
            return;
        }

        Render(parsed, "front", fromPositiveZ: true);
        Render(parsed, "back", fromPositiveZ: false);
        Console.WriteLine("done");
    }

    static void Render(BabbdiCoop.GlbModelLoader.Parsed parsed, string tag, bool fromPositiveZ)
    {
        const int imgW = 420, imgH = 560;
        using var bmp = new Bitmap(imgW, imgH);
        using (var g = Graphics.FromImage(bmp)) g.Clear(Color.FromArgb(60, 60, 66));

        var items = new List<(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, Bitmap tex)>();
        foreach (var p in parsed.Primitives)
        {
            if (p.Positions == null) continue;
            var idx = p.Indices;
            if (idx == null || idx.Length < 3) continue;
            var tex = (p.MaterialIndex < parsed.Materials.Count)
                ? GetTex(parsed, parsed.Materials[p.MaterialIndex].Item2)
                : null;
            for (int i = 0; i + 2 < idx.Length; i += 3)
            {
                int ia = idx[i], ib = idx[i + 1], ic = idx[i + 2];
                items.Add((
                    p.Positions[ia], p.Positions[ib], p.Positions[ic],
                    p.Uvs != null ? p.Uvs[ia] : default,
                    p.Uvs != null ? p.Uvs[ib] : default,
                    p.Uvs != null ? p.Uvs[ic] : default,
                    tex));
            }
        }

        float minY = items.Min(x => Min(x.a.Y, x.b.Y, x.c.Y));
        float maxY = items.Max(x => Max(x.a.Y, x.b.Y, x.c.Y));
        float minX = items.Min(x => Min(x.a.X, x.b.X, x.c.X));
        float maxX = items.Max(x => Max(x.a.X, x.b.X, x.c.X));
        float minZ = items.Min(x => Min(x.a.Z, x.b.Z, x.c.Z));
        float maxZ = items.Max(x => Max(x.a.Z, x.b.Z, x.c.Z));
        float centerX = (minX + maxX) / 2f;
        float modelH = maxY - minY;

        float scale = Math.Min((imgH - 100f) / modelH, (imgW - 60f) / Math.Max(Math.Max(maxX - minX, maxZ - minZ), 0.5f));
        Func<double, int> mapY = y => (int)(imgH - 50 - (y - minY) * scale);
        Func<double, int> mapX = x => (int)(imgW / 2 + (x - centerX) * scale);

        using (var gg = Graphics.FromImage(bmp))
        using (var pen = new Pen(Color.FromArgb(230, 220, 90), 2))
        {
            gg.DrawLine(pen, 0, mapY(minY), imgW, mapY(minY));
        }
        using (var gg = Graphics.FromImage(bmp))
        using (var font = new Font("Consolas", 10))
            gg.DrawString($"[GLB] {tag} | height {modelH:F2}", font, Brushes.White, 4, 4);

        float camZ = fromPositiveZ ? maxZ + 10 : minZ - 10;
        items.Sort((x, y) =>
        {
            float dA = Math.Abs((x.a.Z + x.b.Z + x.c.Z) / 3f - camZ);
            float dB = Math.Abs((y.a.Z + y.b.Z + y.c.Z) / 3f - camZ);
            return dB.CompareTo(dA);
        });

        foreach (var t in items)
        {
            Func<Vector3, Point> proj = v => fromPositiveZ
                ? new Point(mapX(v.X), mapY(v.Y))
                : new Point(mapX(-v.X), mapY(v.Y));

            Rasterize(bmp,
                proj(t.a), proj(t.b), proj(t.c),
                t.ua, t.ub, t.uc, t.tex);
        }

        var outDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "harness_out"));
        Directory.CreateDirectory(outDir);
        bmp.Save(Path.Combine(outDir, $"glb_{tag}.png"), ImageFormat.Png);
        Console.WriteLine($"saved glb_{tag}.png");
    }

    // textures decode into texCache (see Main); no separate pass needed

    static Dictionary<BabbdiCoop.GlbModelLoader.ImageData, Bitmap> texCache;

    static Bitmap GetTex(BabbdiCoop.GlbModelLoader.Parsed parsed, int texIdx)
    {
        if (texIdx < 0 || texIdx >= parsed.Images.Count) return null;
        texCache.TryGetValue(parsed.Images[texIdx], out var bmp);
        return bmp;
    }

    static void Rasterize(Bitmap bmp, Point p0, Point p1, Point p2,
        Vector2 uv0, Vector2 uv1, Vector2 uv2, Bitmap texture)
    {
        int minX = Math.Max(0, Math.Min(p0.X, Math.Min(p1.X, p2.X)));
        int maxX = Math.Min(bmp.Width - 1, Math.Max(p0.X, Math.Max(p1.X, p2.X)));
        int minYp = Math.Max(0, Math.Min(p0.Y, Math.Min(p1.Y, p2.Y)));
        int maxYp = Math.Min(bmp.Height - 1, Math.Max(p0.Y, Math.Max(p1.Y, p2.Y)));
        if (maxX < minX || maxYp < minYp) return;

        float denom = (p1.Y - p2.Y) * (p0.X - p2.X) + (p2.X - p1.X) * (p0.Y - p2.Y);
        if (Math.Abs(denom) < 1e-6) return;

        for (int y = minYp; y <= maxYp; y++)
        for (int x = minX; x <= maxX; x++)
        {
            float w0 = ((p1.Y - p2.Y) * (x - p2.X) + (p2.X - p1.X) * (y - p2.Y)) / denom;
            float w1 = ((p2.Y - p0.Y) * (x - p2.X) + (p0.X - p2.X) * (y - p2.Y)) / denom;
            float w2 = 1f - w0 - w1;
            if (w0 < 0 || w1 < 0 || w2 < 0) continue;

            Color c = Color.LightGray;
            if (texture != null)
            {
                float u = w0 * uv0.X + w1 * uv1.X + w2 * uv2.X;
                float v = w0 * uv0.Y + w1 * uv1.Y + w2 * uv2.Y;
                // glTF UV origin is top-left (v grows DOWN) — no flip needed
                int tx = (int)(u * (texture.Width - 1));
                int ty = (int)(v * (texture.Height - 1));
                c = texture.GetPixel(Math.Clamp(tx, 0, texture.Width - 1), Math.Clamp(ty, 0, texture.Height - 1));
            }
            bmp.SetPixel(x, y, c);
        }
    }

    static float Min(float a, float b, float c) => Math.Min(a, Math.Min(b, c));
    static float Max(float a, float b, float c) => Math.Max(a, Math.Max(b, c));
}

static class Ext
{
    // image cache on ImageData, console only
    public static System.Drawing.Bitmap Bitmap;
}
