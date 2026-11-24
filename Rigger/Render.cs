using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Numerics;

namespace Rigger
{
    /// <summary>
    /// Small z-buffered, textured, back-face-culled preview renderer. Used to check
    /// the source model's facing / proportions and the skinned result of the rig.
    /// </summary>
    public static class Render
    {
        public class Tri
        {
            public Vector3 A, B, C;
            public Vector2 UA, UB, UC;
            public Bitmap Tex;
        }

        public static List<Tri> FromParsed(BabbdiCoop.GlbModelLoader.Parsed parsed, bool usePositions = true)
        {
            var list = new List<Tri>();
            var texCache = new Dictionary<int, Bitmap>();
            foreach (var p in parsed.Primitives)
            {
                var pos = usePositions ? p.Positions : p.RawPositions;
                if (pos == null || p.Indices == null) continue;
                Bitmap tex = null;
                if (p.MaterialIndex >= 0 && p.MaterialIndex < parsed.Materials.Count)
                {
                    int ti = parsed.Materials[p.MaterialIndex].Item2;
                    if (ti >= 0 && ti < parsed.Images.Count && parsed.Images[ti].Png != null)
                    {
                        if (!texCache.TryGetValue(ti, out tex))
                        {
                            using var ms = new MemoryStream(parsed.Images[ti].Png);
                            tex = new Bitmap(ms);
                            texCache[ti] = tex;
                        }
                    }
                }
                for (int i = 0; i + 2 < p.Indices.Length; i += 3)
                {
                    int a = p.Indices[i], b = p.Indices[i + 1], c = p.Indices[i + 2];
                    list.Add(new Tri
                    {
                        A = pos[a], B = pos[b], C = pos[c],
                        UA = p.Uvs != null ? p.Uvs[a] : Vector2.Zero,
                        UB = p.Uvs != null ? p.Uvs[b] : Vector2.Zero,
                        UC = p.Uvs != null ? p.Uvs[c] : Vector2.Zero,
                        Tex = tex
                    });
                }
            }
            return list;
        }

        /// <summary>Renders one orthographic view; returns the file path.</summary>
        public static string View(List<Tri> tris, string outPath, Vector3 camDir, string label,
                                  int w = 380, int h = 520, bool cull = true, bool flipWinding = false,
                                  List<(Vector3 p, string text)> labels = null, bool wire = false)
        {
            if (tris.Count == 0) return null;

            // view basis: camDir points FROM the model TO the camera
            var fwd = Vector3.Normalize(camDir);
            var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, fwd));
            var up = Vector3.Cross(fwd, right);

            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            var frameSrc = (labels != null && labels.Count > 1) ? labels.Select(l => l.p).ToList()
                         : tris.SelectMany(t => new[] { t.A, t.B, t.C }).ToList();
            foreach (var p in frameSrc)
            {
                float x = Vector3.Dot(p, right), y = Vector3.Dot(p, up);
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
            }
            float spanF = Math.Max(maxX - minX, maxY - minY);
            if (labels != null && labels.Count > 1) { minX -= spanF * 0.18f; maxX += spanF * 0.18f; minY -= spanF * 0.18f; maxY += spanF * 0.18f; }
            float span = Math.Max(maxX - minX, maxY - minY);
            if (span < 1e-6f) span = 1f;
            float scale = Math.Min((w - 40f), (h - 60f)) / span;
            float cx = (minX + maxX) * 0.5f, cy = (minY + maxY) * 0.5f;

            using var bmp = new Bitmap(w, h);
            using (var g = Graphics.FromImage(bmp)) g.Clear(Color.FromArgb(52, 54, 60));
            var zbuf = new float[w * h];
            for (int i = 0; i < zbuf.Length; i++) zbuf[i] = float.MinValue;
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = Color.FromArgb(52, 54, 60);

            foreach (var t in tris)
            {
                var n = Vector3.Cross(t.B - t.A, t.C - t.A);
                if (flipWinding) n = -n;
                var centroid = (t.A + t.B + t.C) / 3f;
                // camera sits at +inf along `fwd` (orthographic): visible when n·fwd > 0
                if (cull && Vector3.Dot(n, fwd) <= 0f) continue;

                var pa = Project(t.A); var pb = Project(t.B); var pc = Project(t.C);
                float denom = (pb.Y - pc.Y) * (pa.X - pc.X) + (pc.X - pb.X) * (pa.Y - pc.Y);
                if (Math.Abs(denom) < 1e-6f) continue;

                int x0 = Math.Max(0, (int)Math.Floor(Math.Min(pa.X, Math.Min(pb.X, pc.X))));
                int x1 = Math.Min(w - 1, (int)Math.Ceiling(Math.Max(pa.X, Math.Max(pb.X, pc.X))));
                int y0 = Math.Max(0, (int)Math.Floor(Math.Min(pa.Y, Math.Min(pb.Y, pc.Y))));
                int y1 = Math.Min(h - 1, (int)Math.Ceiling(Math.Max(pa.Y, Math.Max(pb.Y, pc.Y))));

                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float w0 = ((pb.Y - pc.Y) * (x - pc.X) + (pc.X - pb.X) * (y - pc.Y)) / denom;
                    float w1 = ((pc.Y - pa.Y) * (x - pc.X) + (pa.X - pc.X) * (y - pc.Y)) / denom;
                    float w2 = 1f - w0 - w1;
                    if (w0 < -1e-4f || w1 < -1e-4f || w2 < -1e-4f) continue;
                    float z = w0 * Vector3.Dot(t.A, fwd) + w1 * Vector3.Dot(t.B, fwd) + w2 * Vector3.Dot(t.C, fwd);
                    int o = y * w + x;
                    if (z <= zbuf[o]) continue;
                    zbuf[o] = z;

                    Color c = Color.FromArgb(150, 150, 155);
                    if (t.Tex != null)
                    {
                        float u = w0 * t.UA.X + w1 * t.UB.X + w2 * t.UC.X;
                        float v = w0 * t.UA.Y + w1 * t.UB.Y + w2 * t.UC.Y;
                        int tx = (int)(u * (t.Tex.Width - 1));
                        int ty = (int)(v * (t.Tex.Height - 1));
                        c = t.Tex.GetPixel(Math.Clamp(tx, 0, t.Tex.Width - 1), Math.Clamp(ty, 0, t.Tex.Height - 1));
                    }
                    // cheap directional shade so limbs read in a still
                    float shade = 0.55f + 0.45f * Math.Abs(Vector3.Dot(Vector3.Normalize(n), Vector3.Normalize(fwd + up * 0.4f + right * 0.4f)));
                    px[o] = Color.FromArgb(c.R == 0 && c.A == 0 ? 90 : (int)(c.R * shade),
                                           (int)(c.G * shade), (int)(c.B * shade));
                }
            }

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                bmp.SetPixel(x, y, px[y * w + x]);

            using (var g = Graphics.FromImage(bmp))
            using (var font = new Font("Consolas", 9))
            {
                if (wire)
                {
                    using var pen = new Pen(Color.FromArgb(150, 235, 235, 255), 1f);
                    foreach (var t in tris)
                    {
                        if (cull)
                        {
                            var n = Vector3.Cross(t.B - t.A, t.C - t.A);
                            if (flipWinding) n = -n;
                            if (Vector3.Dot(n, fwd) <= 0f) continue;
                        }
                        var pa = Project(t.A); var pb = Project(t.B); var pc = Project(t.C);
                        g.DrawLine(pen, pa, pb); g.DrawLine(pen, pb, pc); g.DrawLine(pen, pc, pa);
                    }
                }
                if (labels != null)
                {
                    using var dot = new SolidBrush(Color.FromArgb(255, 255, 90, 90));
                    using var lab = new Font("Consolas", labels.Count < 60 ? 9 : 7, FontStyle.Bold);
                    foreach (var (p, text) in labels)
                    {
                        var q = Project(p);
                        g.FillEllipse(dot, q.X - 1.5f, q.Y - 1.5f, 3f, 3f);
                        g.DrawString(text, lab, Brushes.Yellow, q.X + 2f, q.Y - 7f);
                    }
                }
                g.DrawString(label, font, Brushes.White, 4, 4);
                // ground line
                float gy = (float)(h * 0.5 + (0 - cy) * scale);
                g.DrawLine(new Pen(Color.FromArgb(220, 200, 80)), 0, gy, w, gy);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            bmp.Save(outPath, ImageFormat.Png);
            return outPath;

            PointF Project(Vector3 p) => new PointF(
                (float)(w * 0.5 + (Vector3.Dot(p, right) - cx) * scale),
                (float)(h * 0.5 - (Vector3.Dot(p, up) - cy) * scale));
        }
    }
}
