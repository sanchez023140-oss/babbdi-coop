using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace BabbdiCoop
{
    // Part of the SovietDistrict builder - see SovietDistrict.cs.
    internal static partial class SovietDistrict
    {

        // ------------------------------------------------------------------ the penthouse (v0.9.18)

        /// <summary>Radius of the big clock dial вЂ” the photo's dials fill most of one facade.</summary>
        private const float DialR = 4.4f;

        // measured by `Penthouse`, read back by the probes вЂ” the tower's own numbers, not guesses
        private static float pentAw, pentAd, pentDeckY, pentApexY, pentTopY;

        /// <summary>
        /// v0.9.18 вЂ” **the clock tower on the block on the peninsula** (the outlying tower on the western
        /// wing, `Place(-40, 225, 3, 2, 1.25)`), rebuilt from the user's photo: not a one-storey attic
        /// drum but a real **four-storey** tower with huge clock dials and the atrium inside.
        ///
        /// Read off the photo, bottom to top:
        ///  1. **the base storey** вЂ” tall windows, three per face, and the entrance (2,4 x 3,2 m on `+z`);
        ///  2. **the big-window storey** вЂ” three **arched** windows per face, 2,4 x 4,2 m (see `ArchFill`);
        ///  3. **the clock storey** вЂ” **double height (10,2 m)** so a 9,2 m dial fits: the wall is solid
        ///     and the clock *is* the window, one per face, set in a bronze ring (the photo's proportions:
        ///     the dial is ~36 % of the facade and ~90 % of the storey);
        ///  4. **the top strip** вЂ” four small windows per face under the cornice;
        ///  5. **the cornice** вЂ” a 0,6 m plate overhanging the drum by 0,8 m;
        ///  6. **the steep mansard** in `M_MetalRusty00` вЂ” four planar trapezoids `1,0 x` the half-width
        ///     high, cut off at a platform `0,28` of the half-width, with **two rows of skylights** per
        ///     slope (4 + 3, the photo's roof windows);
        ///  7. **the glazed lantern** вЂ” a railing round the platform and a steel-framed glass box (3 m)
        ///     on top, the photo's little cupola.
        ///
        /// Inside (the photo's right half) the tower is a **four-storey atrium**: floor slabs as rings
        /// around a 9,2 x 9,2 m void, a 1,05 m parapet round the void on every floor, a **glass lift
        /// shaft** through the middle (dark frame + translucent panes) and a switchback stair in the east
        /// lane, so the player can walk from the door to the clock storey instead of only flying in.
        ///
        /// Materials: the building's own mesh carries concrete (part 0) and the roof metal (part 1);
        /// `det` (dark + the dial) holds the skylights, the railing and the eight clock faces, `gla`
        /// (dark + glass) the lift shaft and the lantern's panes вЂ” three objects, four materials.
        /// </summary>
        private static void Penthouse(Geo g, Bldg b, float hw, float hd, float yTop)
        {
            pentBlock = b;
            const float inX = 1.8f, inZ = 1.8f;           // the tower stands inside the roof parapet
            const float fh = 5.6f;                        // an ordinary storey (the district's tall one)
            const float ch = 10.2f;                       // the clock storey: double height
            float aw = hw - inX, ad = hd - inZ;
            float y0 = yTop + 0.26f;                      // the roof plate's top face вЂ” see Roof()
            float y1 = y0 + fh, y2 = y1 + fh, y3 = y2 + ch, yT = y3 + fh;
            pentAw = aw; pentAd = ad; pentDeckY = y0;

            var det = new Geo();                          // skylights/frames (dark) + the dials (part 1)
            var gla = new Geo();                          // frames (dark) + glass panes (part 1)

            // ---- 1. the four storeys
            DrumFloor(g, aw, ad, y0, fh, 2.0f, 3.4f, 1.2f, 3, true);     // ground: tall windows + door
            DrumFloor(g, aw, ad, y1, fh, 2.4f, 4.2f, 0.8f, 3, false);    // the big windows...
            float archR = 1.2f;                                          // ...with arched heads
            float ySpring = y1 + 0.8f + 4.2f - archR;
            foreach (float c in new[] { 0.5f, 1.5f, 2.5f })
            {
                ArchFill(g, new Vector3(0f, 0f, ad), Vector3.right, Vector3.forward, 2f * aw, 2f * aw / 3f * c, ySpring, archR);
                ArchFill(g, new Vector3(0f, 0f, -ad), Vector3.left, Vector3.back, 2f * aw, 2f * aw / 3f * c, ySpring, archR);
                ArchFill(g, new Vector3(aw, 0f, 0f), Vector3.forward, Vector3.right, 2f * ad, 2f * ad / 3f * c, ySpring, archR);
                ArchFill(g, new Vector3(-aw, 0f, 0f), Vector3.back, Vector3.left, 2f * ad, 2f * ad / 3f * c, ySpring, archR);
            }
            // the clock storey is a solid band вЂ” the dial is its window
            PlainWall(g, new Vector3(0f, 0f, ad), Vector3.right, Vector3.forward, 2f * aw, y2, y3);
            PlainWall(g, new Vector3(0f, 0f, -ad), Vector3.left, Vector3.back, 2f * aw, y2, y3);
            PlainWall(g, new Vector3(aw, 0f, 0f), Vector3.forward, Vector3.right, 2f * ad, y2, y3);
            PlainWall(g, new Vector3(-aw, 0f, 0f), Vector3.back, Vector3.left, 2f * ad, y2, y3);
            DrumFloor(g, aw, ad, y3, fh, 1.7f, 2.3f, 1.3f, 4, false);    // the top strip

            // ---- 2. the atrium: a central void through every slab, a parapet round it
            const float av = 4.6f;                        // the void's half-size
            float ai = aw - T + 0.10f, aj = ad - T + 0.10f;
            foreach (float yk in new[] { y1, y2, y3 })
            {
                g.Box(0, new Vector3(0f, yk - 0.15f, (av + aj) * 0.5f), new Vector3(2f * ai, 0.3f, aj - av), Quaternion.identity, 0.4f);
                g.Box(0, new Vector3(0f, yk - 0.15f, -(av + aj) * 0.5f), new Vector3(2f * ai, 0.3f, aj - av), Quaternion.identity, 0.4f);
                g.Box(0, new Vector3((av + ai) * 0.5f, yk - 0.15f, 0f), new Vector3(ai - av, 0.3f, 2f * av), Quaternion.identity, 0.4f);
                g.Box(0, new Vector3(-(av + ai) * 0.5f, yk - 0.15f, 0f), new Vector3(ai - av, 0.3f, 2f * av), Quaternion.identity, 0.4f);
                const float rt = 0.12f, rgt = 1.05f;
                g.Box(0, new Vector3(0f, yk + rgt * 0.5f, av + rt * 0.5f), new Vector3(2f * av, rgt, rt), Quaternion.identity, 0.5f);
                g.Box(0, new Vector3(av + rt * 0.5f, yk + rgt * 0.5f, 0f), new Vector3(rt, rgt, 2f * av), Quaternion.identity, 0.5f);
                g.Box(0, new Vector3(-(av + rt * 0.5f), yk + rgt * 0.5f, 0f), new Vector3(rt, rgt, 2f * av), Quaternion.identity, 0.5f);
                // the south side has a gap where the stair passes: two pieces, not one
                float gx0 = av - 2.15f, gx1 = av + 0.15f;
                g.Box(0, new Vector3((-av + gx0) * 0.5f, yk + rgt * 0.5f, -(av + rt * 0.5f)),
                      new Vector3(gx0 + av, rgt, rt), Quaternion.identity, 0.5f);
                g.Box(0, new Vector3((gx1 + av) * 0.5f, yk + rgt * 0.5f, -(av + rt * 0.5f)),
                      new Vector3(av - gx1, rgt, rt), Quaternion.identity, 0.5f);
            }

            // ---- 3. the glass lift shaft through the middle of the atrium
            const float sh = 1.35f;
            foreach (var s in new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(1f, 1f), new Vector2(-1f, 1f) })
                gla.Box(0, new Vector3(s.x * sh, (y0 + y3) * 0.5f, s.y * sh),
                        new Vector3(0.22f, y3 - y0, 0.22f), Quaternion.identity, 0.5f);
            foreach (float yk in new[] { y0 + 0.12f, y1, y2, y3 })
            {
                gla.Box(0, new Vector3(0f, yk, -sh), new Vector3(2f * sh + 0.22f, 0.16f, 0.16f), Quaternion.identity, 0.5f);
                gla.Box(0, new Vector3(0f, yk, sh), new Vector3(2f * sh + 0.22f, 0.16f, 0.16f), Quaternion.identity, 0.5f);
                gla.Box(0, new Vector3(-sh, yk, 0f), new Vector3(0.16f, 0.16f, 2f * sh), Quaternion.identity, 0.5f);
                gla.Box(0, new Vector3(sh, yk, 0f), new Vector3(0.16f, 0.16f, 2f * sh), Quaternion.identity, 0.5f);
            }
            float ey2 = (y0 + y3) * 0.5f, eh = y3 - y0;
            gla.Box(1, new Vector3(0f, ey2, sh), new Vector3(2f * sh, eh, 0.05f), Quaternion.identity, 0.5f);
            gla.Box(1, new Vector3(0f, ey2, -sh), new Vector3(2f * sh, eh, 0.05f), Quaternion.identity, 0.5f);
            gla.Box(1, new Vector3(sh, ey2, 0f), new Vector3(0.05f, eh, 2f * sh), Quaternion.identity, 0.5f);
            gla.Box(1, new Vector3(-sh, ey2, 0f), new Vector3(0.05f, eh, 2f * sh), Quaternion.identity, 0.5f);
            // the cage, parked on the ground floor
            det.Box(0, new Vector3(0f, y0 + 1.35f, 0f), new Vector3(2f * sh - 0.12f, 2.7f, 2f * sh - 0.12f), Quaternion.identity, 0.5f);

            // ---- 4. the stair in the atrium's east lane (walk up instead of only flying)
            for (int k = 0; k < 3; k++)
                AtticStair(g, new[] { y0, y1, y2 }[k], fh, av - 1.9f, av, -4.6f, av);

            // ---- 5. the cornice and the steep mansard
            float ex = aw + 0.8f, ez = ad + 0.8f;
            g.Box(0, new Vector3(0f, yT + 0.3f, 0f), new Vector3(2f * ex, 0.6f, 2f * ez), Quaternion.identity, 0.4f);
            float yC = yT + 0.6f;
            float rh = 1.0f * Mathf.Min(aw, ad);
            float yA = yC + rh;
            float ax = aw * 0.28f, az = ad * 0.28f;
            pentApexY = yA;
            var spots = new List<Vector2>
            {
                new Vector2(0.18f, 0.26f), new Vector2(0.40f, 0.26f), new Vector2(0.62f, 0.26f), new Vector2(0.84f, 0.26f),
                new Vector2(0.29f, 0.55f), new Vector2(0.50f, 0.55f), new Vector2(0.71f, 0.55f)
            }.ToArray();
            var winHalf = new Vector2(0.62f, 0.52f);
            Slope(g, det, new Vector3(-ex, yC, ez), new Vector3(ex, yC, ez),
                  new Vector3(ax, yA, az), new Vector3(-ax, yA, az), Vector3.forward, spots, winHalf);
            Slope(g, det, new Vector3(ex, yC, ez), new Vector3(ex, yC, -ez),
                  new Vector3(ax, yA, -az), new Vector3(ax, yA, az), Vector3.right, spots, winHalf);
            Slope(g, det, new Vector3(ex, yC, -ez), new Vector3(-ex, yC, -ez),
                  new Vector3(-ax, yA, -az), new Vector3(ax, yA, -az), Vector3.back, spots, winHalf);
            Slope(g, det, new Vector3(-ex, yC, -ez), new Vector3(-ex, yC, ez),
                  new Vector3(-ax, yA, az), new Vector3(-ax, yA, -az), Vector3.left, spots, winHalf);
            g.Quad(1, new Vector3(-ax, yA, -az), new Vector3(-ax, yA, az),
                      new Vector3(ax, yA, az), new Vector3(ax, yA, -az), Vector3.up, 0.5f);

            // ---- 6. the lantern on the platform: a railing, then a steel-framed glass box
            const float rw = 0.10f, rh2 = 1.05f;
            det.Box(0, new Vector3(0f, yA + rh2 * 0.5f, az - 0.2f), new Vector3(2f * ax, rh2, rw), Quaternion.identity, 0.5f);
            det.Box(0, new Vector3(0f, yA + rh2 * 0.5f, -az + 0.2f), new Vector3(2f * ax, rh2, rw), Quaternion.identity, 0.5f);
            det.Box(0, new Vector3(ax - 0.2f, yA + rh2 * 0.5f, 0f), new Vector3(rw, rh2, 2f * az), Quaternion.identity, 0.5f);
            det.Box(0, new Vector3(-ax + 0.2f, yA + rh2 * 0.5f, 0f), new Vector3(rw, rh2, 2f * az), Quaternion.identity, 0.5f);
            float cw = ax * 0.52f, cd = az * 0.52f, chh = 3.0f;
            foreach (var s in new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(1f, 1f), new Vector2(-1f, 1f) })
                gla.Box(0, new Vector3(s.x * cw, yA + chh * 0.5f, s.y * cd), new Vector3(0.18f, chh, 0.18f), Quaternion.identity, 0.5f);
            gla.Box(1, new Vector3(0f, yA + chh * 0.5f, cd), new Vector3(2f * cw, chh, 0.05f), Quaternion.identity, 0.5f);
            gla.Box(1, new Vector3(0f, yA + chh * 0.5f, -cd), new Vector3(2f * cw, chh, 0.05f), Quaternion.identity, 0.5f);
            gla.Box(1, new Vector3(cw, yA + chh * 0.5f, 0f), new Vector3(0.05f, chh, 2f * cd), Quaternion.identity, 0.5f);
            gla.Box(1, new Vector3(-cw, yA + chh * 0.5f, 0f), new Vector3(0.05f, chh, 2f * cd), Quaternion.identity, 0.5f);
            g.Box(1, new Vector3(0f, yA + chh + 0.12f, 0f), new Vector3(2f * cw + 0.6f, 0.24f, 2f * cd + 0.6f), Quaternion.identity, 0.5f);
            float ly = yA + chh + 0.24f;
            pentTopY = ly;
            const float lrw = 0.10f, lrh = 0.95f;
            det.Box(0, new Vector3(0f, ly + lrh * 0.5f, cd + 0.3f), new Vector3(2f * cw + 0.5f, lrh, lrw), Quaternion.identity, 0.5f);
            det.Box(0, new Vector3(0f, ly + lrh * 0.5f, -cd - 0.3f), new Vector3(2f * cw + 0.5f, lrh, lrw), Quaternion.identity, 0.5f);
            det.Box(0, new Vector3(cw + 0.3f, ly + lrh * 0.5f, 0f), new Vector3(lrw, lrh, 2f * cd + 0.6f), Quaternion.identity, 0.5f);
            det.Box(0, new Vector3(-cw - 0.3f, ly + lrh * 0.5f, 0f), new Vector3(lrw, lrh, 2f * cd + 0.6f), Quaternion.identity, 0.5f);

            // ---- 7. one great dial per face on the clock storey, in a bronze ring
            float cy = y2 + ch * 0.5f;
            Dial(det, new Vector3(0f, cy, ad + 0.04f), Vector3.right, Vector3.up, DialR);
            Dial(det, new Vector3(0f, cy, -ad - 0.04f), Vector3.left, Vector3.up, DialR);
            Dial(det, new Vector3(aw + 0.04f, cy, 0f), Vector3.back, Vector3.up, DialR);
            Dial(det, new Vector3(-aw - 0.04f, cy, 0f), Vector3.forward, Vector3.up, DialR);
            Ring(g, new Vector3(0f, cy, ad + 0.02f), Vector3.right, Vector3.up, DialR, DialR + 0.5f);
            Ring(g, new Vector3(0f, cy, -ad - 0.02f), Vector3.left, Vector3.up, DialR, DialR + 0.5f);
            Ring(g, new Vector3(aw + 0.02f, cy, 0f), Vector3.back, Vector3.up, DialR, DialR + 0.5f);
            Ring(g, new Vector3(-aw - 0.02f, cy, 0f), Vector3.forward, Vector3.up, DialR, DialR + 0.5f);

            // ---- 8. the objects of their own (the building mesh only has two materials)
            var dgo = new GameObject("penthouse_detail");
            dgo.transform.SetParent(b.go.transform, false);
            det.Apply(dgo, mDark, ClockMaterial());

            var ggo = new GameObject("penthouse_glass");
            ggo.transform.SetParent(b.go.transform, false);
            gla.Apply(ggo, mDark, GlassMaterial());

            // one warm lamp per storey in the void: the level has no sun, and an unlit atrium would be a
            // black column behind a lit facade
            foreach (float yk in new[] { y0, y1, y2, y3 })
                Lamp(ggo.transform, new Vector3(3.6f, yk + 3.4f, 3.6f), 1.1f, 16f);

            log.LogInfo($"[PENTHOUSE] clock tower on the block on the wing ({b.w:0.0}x{b.d:0.0} m, " +
                        $"{b.floors} floors, top {b.Top:0.0} m at {b.p.x:0.0},{b.p.z:0.0}): drum " +
                        $"{2f * aw:0.0}x{2f * ad:0.0} m (inset {inX:0.0} m), 4 storeys " +
                        $"{fh:0.0} + {fh:0.0} + {ch:0.0} (clock) + {fh:0.0} = {yT - y0:0.0} m, " +
                        $"atrium {2f * av:0.0}x{2f * av:0.0} m with a lift shaft and a stair, " +
                        $"cornice {2f * ex:0.0}x{2f * ez:0.0} m at y={yC:0.0}, mansard {rh:0.0} m in " +
                        $"{N(mMetal)} with 28 skylights, platform {2f * ax:0.0}x{2f * az:0.0} m at " +
                        $"y={yA:0.0}, glass lantern {2f * cw:0.0}x{2f * cd:0.0}x{chh:0.0} m, " +
                        $"4 clock dials r={DialR:0.00} m (one per face), roof deck y={y0:0.0}, " +
                        $"lantern top y={ly + lrh:0.0}");
        }

        /// <summary>
        /// v0.9.18 вЂ” **the arched head of one attic window**. `Wall` cuts rectangles only, so the opening
        /// is a rectangle up to the springing line and this fills the two spandrels above it: the visible
        /// hole becomes the rectangle plus a semicircle of radius `r`, and the arch gets a real soffit вЂ”
        /// a curved reveal across the wall's 0,94 m thickness вЂ” so it reads as an arch from inside the
        /// room as well. The reference's windows are exactly this shape.
        ///
        /// `o`/`u`/`n`/`len`/`cx` use the same convention as <see cref="Wall"/> (`o` is the wall's base
        /// midpoint, `cx` is measured from the wall's left end).
        /// </summary>
        private static void ArchFill(Geo g, Vector3 o, Vector3 u, Vector3 n, float len,
                                     float cx, float ySpring, float r, int N = 10)
        {
            o -= u * (len * 0.5f);
            Vector3 oi = o - n * T;
            System.Func<float, float> half = y =>
            {
                float dy = Mathf.Clamp(y - ySpring, 0f, r);
                return Mathf.Sqrt(Mathf.Max(0f, r * r - dy * dy));
            };
            for (int j = 0; j < N; j++)
            {
                float ya = ySpring + r * (j / (float)N), yb = ySpring + r * ((j + 1) / (float)N);
                float ha = half(ya), hb = half(yb);
                foreach (float s in new[] { -1f, 1f })         // the left and the right spandrel
                {
                    // exterior face: from the opening's outer edge (cx В± r) to the arc
                    Vector3 ea = o + u * (cx + s * r) + Vector3.up * ya;
                    Vector3 eb = o + u * (cx + s * r) + Vector3.up * yb;
                    Vector3 fa = o + u * (cx + s * ha) + Vector3.up * ya;
                    Vector3 fb = o + u * (cx + s * hb) + Vector3.up * yb;
                    g.Quad(0, ea, eb, fb, fa, n, 0.5f);
                    g.Quad(0, ea - n * T, eb - n * T, fb - n * T, fa - n * T, -n, 0.5f);
                    // the arch's soffit: the curved reveal, normal pointing into the opening
                    g.Quad(0, fa, fb, fb - n * T, fa - n * T, u * -s, 0.5f);
                }
            }
        }

        /// <summary>
        /// One roof slope: a planar trapezoid (part 1, the metal) plus three flush skylights (part 0 of
        /// the detail geo, the dark glass). `a`,`b` are the eave edge, `c`,`d` the platform edge above
        /// them, `outw` the direction the slope faces вЂ” the normal is flipped onto it, so the winding
        /// never depends on the caller's corner order.
        /// </summary>
        private static void Slope(Geo g, Geo det, Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                                  Vector3 outw, Vector2[] spots, Vector2 half)
        {
            var n = Vector3.Cross(b - a, d - a).normalized;
            if (Vector3.Dot(n, outw) < 0f) n = -n;
            g.Quad(1, a, b, c, d, n, 0.4f);
            var eu = (b - a).normalized;
            var slopeUp = ((d + c) * 0.5f - (a + b) * 0.5f).normalized;
            var up = Vector3.Cross(n, eu).normalized;           // in the slope plane, вЉҐ to the eave
            if (Vector3.Dot(up, slopeUp) < 0f) up = -up;
            // `spots` are (across the eave, up the slope) in 0..1 of the trapezoid, `half` the window's
            // half size in metres вЂ” the reference's roof has two rows of them, four over three
            foreach (var sp in spots)
            {
                float t = sp.y;
                // the trapezoid narrows as it rises: half the eave at the bottom, `ax/aw` at the top
                float eave = Mathf.Lerp(1f, 0.28f, t);
                float s = Mathf.Lerp(0.5f - eave * 0.5f, 0.5f + eave * 0.5f, sp.x);
                var p = Vector3.Lerp(Vector3.Lerp(a, b, s), Vector3.Lerp(d, c, s), t) + n * 0.06f;
                float wT = (b - a).magnitude * Mathf.Lerp(1f, 0.28f, t);
                float hw = Mathf.Min(half.x, wT * 0.5f - 0.2f);
                det.Quad(0, p - eu * hw + up * half.y, p + eu * hw + up * half.y,
                            p + eu * hw - up * half.y, p - eu * hw - up * half.y, n, 0.5f);
            }
        }

        /// <summary>One flat disc on a wall face вЂ” the clock dial (v0.9.18).</summary>
        private static void Dial(Geo g, Vector3 c, Vector3 u, Vector3 v, float r)
        {
            const int N = 48;
            var n = Vector3.Cross(u, v).normalized;
            var centre = new Vector2(0.5f, 0.5f);
            var prev = c + u * r;
            var prevUv = new Vector2(1f, 0.5f);
            for (int i = 1; i <= N; i++)
            {
                float a = i / (float)N * Mathf.PI * 2f;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                var p = c + (u * ca + v * sa) * r;
                var uv = new Vector2(0.5f + ca * 0.5f, 0.5f + sa * 0.5f);
                g.Tri(1, c, prev, p, n, centre, prevUv, uv);
                prev = p; prevUv = uv;
            }
        }

        /// <summary>A flat bronze annulus round a dial (v0.9.18) вЂ” the photo's metal bezel.</summary>
        private static void Ring(Geo g, Vector3 c, Vector3 u, Vector3 v, float r0, float r1)
        {
            const int N = 48;
            var n = Vector3.Cross(u, v).normalized;
            var po = u * r0; var pi = u * r1;
            for (int i = 1; i <= N; i++)
            {
                float a = i / (float)N * Mathf.PI * 2f;
                var d = u * Mathf.Cos(a) + v * Mathf.Sin(a);
                g.Quad(1, c + po, c + d * r0, c + d * r1, c + pi, n, 0.8f);
                po = d * r0; pi = d * r1;
            }
        }

        /// <summary>
        /// One storey of the tower (v0.9.18): the four facades off the district's own `Wall`, so the
        /// openings are real holes with reveals and the storey is enclosed on both faces. `bays` windows
        /// per face at `bay = 2В·half / bays`, `door` cuts the entrance in the middle of `+z`.
        /// </summary>
        private static void DrumFloor(Geo g, float aw, float ad, float y0, float fh,
                                      float winW, float winH, float sill, int bays, bool door)
        {
            var dZP = new List<Vector4>();
            var nil = new List<Vector4>();
            if (door) AddDoor(dZP, aw, y0, 2.6f, 3.4f);
            float bayZ = 2f * aw / bays, bayX = 2f * ad / bays;
            Wall(g, new Vector3(0f, 0f, ad), Vector3.right, Vector3.forward, 2f * aw, y0, y0 + fh, 1, fh,
                 winW, winH, sill, 0f, bayZ, dZP);
            Wall(g, new Vector3(0f, 0f, -ad), Vector3.left, Vector3.back, 2f * aw, y0, y0 + fh, 1, fh,
                 winW, winH, sill, 0f, bayZ, nil);
            Wall(g, new Vector3(aw, 0f, 0f), Vector3.forward, Vector3.right, 2f * ad, y0, y0 + fh, 1, fh,
                 winW, winH, sill, 0f, bayX, nil);
            Wall(g, new Vector3(-aw, 0f, 0f), Vector3.back, Vector3.left, 2f * ad, y0, y0 + fh, 1, fh,
                 winW, winH, sill, 0f, bayX, nil);
        }

        /// <summary>A wall with no openings at all (v0.9.18) вЂ” the clock storey's solid band.</summary>
        private static void PlainWall(Geo g, Vector3 o, Vector3 u, Vector3 n, float len, float y0, float y1)
        {
            o -= u * (len * 0.5f);
            Vector3 a = o, b = o + u * len;
            g.Quad(0, a + Vector3.up * y0, b + Vector3.up * y0, b + Vector3.up * y1, a + Vector3.up * y1, n, 0.5f);
            g.Quad(0, a - n * T + Vector3.up * y0, b - n * T + Vector3.up * y0,
                      b - n * T + Vector3.up * y1, a - n * T + Vector3.up * y1, -n, 0.5f);
        }

        /// <summary>
        /// One storey of the atrium stair (v0.9.18): two solid flights of 12 + 12 steps of `fh/24`
        /// (в‰¤ 0,25 m, so the character's 0,3 m step offset climbs it), a half landing and the exit
        /// landing that bridges the void's edge (`edgeX`) onto the gallery slab.
        /// </summary>
        private static void AtticStair(Geo g, float y, float fh, float x0, float x1, float z0, float edgeX,
                                       float zSign = 1f)
        {
            const int n = 12;
            float rise = fh / (2f * n), treadLen = 0.30f;
            float q1 = z0 + treadLen * n;               // the half-landing starts here (local run)
            float q2 = q1 + 1.5f;                       // and ends here
            float qe = q2 - treadLen * n;               // the exit landing ends here
            // v0.9.37: the flights are plates (FlightZ) and the landings closed slabs (Landing), like
            // the houses' stairs вЂ” the old box-per-step flights read as solid triangular masses.
            FlightZ(g, x0, x1, Mathf.Min(zSign * z0, zSign * q1), Mathf.Max(zSign * z0, zSign * q1),
                    y, n, rise, zSign < 0f, SLAB, StairPlate);                      // flight A, up
            Landing(g, x0, x1, Mathf.Min(zSign * q1, zSign * q2), Mathf.Max(zSign * q1, zSign * q2),
                    y + fh * 0.5f);                                                 // the half-landing
            FlightZ(g, x0, x1, Mathf.Min(zSign * qe, zSign * q2), Mathf.Max(zSign * qe, zSign * q2),
                    y + fh * 0.5f, n, rise, zSign > 0f, StairPlate, StairPlate);     // flight B, back
            // the exit landing, from the void's edge to the gallery slab
            g.Box(0, new Vector3((x0 + edgeX) * 0.5f, y + fh - StairPlate * 0.5f, zSign * (qe - 0.75f)),
                  new Vector3(edgeX - x0, StairPlate, 1.5f), Quaternion.identity, 0.4f);
        }

        // the Roman hour numerals, one string per hour
        private static readonly string[] Roman =
            { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII" };

        /// <summary>
        /// The dial (v0.9.18, rewritten for the 9,2 m clock): a 1024 px face вЂ” a bronze bezel, a
        /// patinated ring, a pale dial with 60 minute ticks, **Roman numerals drawn as strokes** and
        /// laid out radially (each numeral's foot towards the centre, like a tower clock), two ornate
        /// hands at ten past ten and a hub. Drawn in code, so the mod ships no extra file.
        /// </summary>
        private static Texture2D ClockTexture()
        {
            const int n = 1024;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true)
            {
                name = "CoopClockDial",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            var bronze = new Color(0.47f, 0.37f, 0.21f);
            var bronzeDk = new Color(0.18f, 0.14f, 0.09f);
            var patina = new Color(0.30f, 0.60f, 0.53f);          // verdigris, the reference's ring
            var pale = new Color(0.93f, 0.92f, 0.87f);            // the dial
            var ink = new Color(0.08f, 0.09f, 0.10f);

            // ---- the numerals: strokes in a box of height 1 glyph unit, `nh` dial units tall
            const float nh = 0.108f, vw = 0.62f, vgap = 0.17f;
            var segs = new List<Vector4>();
            System.Action<float, float, Vector2, Vector2, float, float, float, float> stroke =
                (midU, midV, right, outv, u0, v0, u1, v1) =>
                {
                    float sx = midU + (u0 * right.x + (v0 - 0.5f) * outv.x) * nh;
                    float sy = midV + (u0 * right.y + (v0 - 0.5f) * outv.y) * nh;
                    float ex = midU + (u1 * right.x + (v1 - 0.5f) * outv.x) * nh;
                    float ey = midV + (u1 * right.y + (v1 - 0.5f) * outv.y) * nh;
                    segs.Add(new Vector4(sx, sy, ex, ey));
                };
            for (int k = 1; k <= 12; k++)
            {
                string s = Roman[k];
                float width = 0f; int nc = 0;
                foreach (char ch in s) { width += ch == 'I' ? 0.30f : vw; nc++; }
                width += vgap * (nc - 1);
                float a = k * 30f * Mathf.Deg2Rad;
                var outv = new Vector2(Mathf.Sin(a), Mathf.Cos(a));   // radially outward
                var right = new Vector2(Mathf.Cos(a), -Mathf.Sin(a)); // clockwise along the ring
                var midU = outv.x * 0.585f; var midV = outv.y * 0.585f;
                float cur = -width * 0.5f;
                foreach (char ch in s)
                {
                    if (ch == 'I')
                        stroke(midU, midV, right, outv, cur + 0.15f, 0f, cur + 0.15f, 1f);
                    else if (ch == 'V')
                    {
                        stroke(midU, midV, right, outv, cur + 0.05f, 1f, cur + 0.31f, 0f);
                        stroke(midU, midV, right, outv, cur + 0.57f, 1f, cur + 0.31f, 0f);
                    }
                    else
                    {
                        stroke(midU, midV, right, outv, cur + 0.05f, 0f, cur + 0.57f, 1f);
                        stroke(midU, midV, right, outv, cur + 0.05f, 1f, cur + 0.57f, 0f);
                    }
                    cur += (ch == 'I' ? 0.30f : vw) + vgap;
                }
            }

            // a tapered blade from the centre, at `deg` from 12 clockwise, with a counterweight tail
            System.Func<float, float, float, float, float, float, bool> hand =
                (dx, dy, deg, len, wb, wt) =>
                {
                    float a = deg * Mathf.Deg2Rad;
                    float ux = Mathf.Sin(a), uy = Mathf.Cos(a);
                    float along = dx * ux + dy * uy, perp = -dx * uy + dy * ux;
                    if (along >= 0f && along <= len)
                        return Mathf.Abs(perp) <= Mathf.Lerp(wb, wt, along / len);
                    if (along < 0f && along > -0.11f)
                        return Mathf.Abs(perp) <= Mathf.Lerp(0.030f, 0.016f, -along / 0.11f);
                    return false;
                };

            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    Color c;
                    if (r > 0.985f) c = bronzeDk;                                   // the outer edge
                    else if (r > 0.945f) c = Color.Lerp(bronze, bronzeDk, (r - 0.945f) / 0.04f);
                    else if (r > 0.905f) c = bronze;                                // the bronze bezel
                    else if (r > 0.875f) c = Color.Lerp(patina, bronzeDk, (r - 0.875f) / 0.03f);
                    else if (r > 0.845f) c = patina;                                // the patinated ring
                    else if (r > 0.825f) c = bronzeDk;                              // the inner shadow line
                    else
                    {
                        c = pale;
                        // a soft vignette towards the rim, and a faint glass sheen from the upper left
                        c = Color.Lerp(c, new Color(0.78f, 0.77f, 0.72f), Mathf.Clamp01((r - 0.60f) / 0.22f) * 0.55f);
                        c = Color.Lerp(c, Color.white, Mathf.Clamp01(-dx * 0.45f + dy * 0.55f) * 0.10f);
                    }
                    // the minute ring: 60 ticks, every fifth one long and heavy
                    if (r > 0.695f && r < 0.815f)
                    {
                        float ang = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;            // 0 = 12, clockwise
                        int idx = Mathf.RoundToInt(ang / 6f);
                        bool hour = ((idx % 5) + 5) % 5 == 0;
                        float arc = Mathf.Abs(Mathf.DeltaAngle(ang, idx * 6f)) * Mathf.Deg2Rad * Mathf.Max(0.5f, r);
                        float tw = hour ? 0.011f : 0.0035f;
                        if (arc < tw * 0.5f && r > (hour ? 0.700f : 0.755f)) c = ink;
                    }
                    // the Roman numerals
                    if (r > 0.46f && r < 0.72f)
                        foreach (var sg in segs)
                            if (Seg(dx, dy, sg.x, sg.y, sg.z, sg.w, 0.0075f)) { c = ink; break; }
                    // ten past ten
                    if (hand(dx, dy, 305f, 0.44f, 0.026f, 0.008f)) c = ink;          // the hour hand
                    if (hand(dx, dy, 60f, 0.62f, 0.020f, 0.006f)) c = ink;           // the minute hand
                    if (r < 0.036f) c = ink;                                         // the hub
                    else if (r < 0.050f) c = Color.Lerp(ink, pale, (r - 0.036f) / 0.014f);
                    px[y * n + x] = c;
                }
            }
            t.SetPixels32(px);
            t.Apply(true, false);
            return t;
        }

        /// <summary>Is (x,y) within `w` of the segment (ax,ay)-(bx,by)? (the dial's hands)</summary>
        private static bool Seg(float x, float y, float ax, float ay, float bx, float by, float w)
        {
            float vx = bx - ax, vy = by - ay;
            float t = Mathf.Clamp01(((x - ax) * vx + (y - ay) * vy) / Mathf.Max(1e-6f, vx * vx + vy * vy));
            float px = ax + vx * t - x, py = ay + vy * t - y;
            return px * px + py * py <= w * w;
        }

        /// <summary>
        /// The dial material вЂ” the memorial's own chain (the game's emissive shader, so the face reads in
        /// the level's darkness and blooms slightly), but at a third of its boost: a clock is lit, not a
        /// lamp. Falls back to an unlit texture, then to the district's concrete with the dial as albedo.
        /// </summary>
        private static Material ClockMaterial()
        {
            if (mClock != null) return mClock;
            var tex = ClockTexture();
            foreach (var nm in new[] { "S_Emissive00", "S_EmissiveColor" })
            {
                var sh = Shader.Find(nm);
                if (sh == null || !sh.isSupported || sh.GetPropertyCount() <= 0) continue;
                var m = new Material(sh) { name = "CoopClock (" + nm + ")" };
                if (!m.HasProperty("_TextureSample0")) continue;
                m.SetTexture("_TextureSample0", tex);
                if (m.HasProperty("_EmissiveBoost")) m.SetFloat("_EmissiveBoost", 0.35f);
                if (m.HasProperty("_Color0")) m.SetColor("_Color0", Color.white);
                if (m.HasProperty("_texcoord")) m.SetVector("_texcoord", new Vector4(1f, 1f, 0f, 0f));
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
                log.LogInfo($"[PENTHOUSE] clock material: emissive {nm} tex={tex.name} ({tex.width}x{tex.height}), " +
                            $"boost=0.35, texcoord=(1.00, 1.00, 0.00, 0.00)");
                mClock = m;
                return m;
            }
            foreach (var nm in new[] { "Unlit/Texture", "Sprites/Default" })
            {
                var sh = Shader.Find(nm);
                if (sh == null || !sh.isSupported) continue;
                var m = new Material(sh) { name = "CoopClock (" + nm + ")" };
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
                log.LogInfo($"[PENTHOUSE] clock material: unlit {nm} tex={tex.name}");
                mClock = m;
                return m;
            }
            var f = new Material(mRough) { name = "CoopClock (concrete + dial)" };
            if (f.HasProperty("_BC")) f.SetTexture("_BC", tex);
            log.LogInfo("[PENTHOUSE] clock material: WARNING no emissive and no unlit shader вЂ” concrete + dial");
            mClock = f;
            return mClock;
        }

        /// <summary>
        /// The glass of the lantern and the lift shaft (v0.9.18): a translucent unlit sheet. The game
        /// ships `Sprites/Default` (it is in the build's shader list), which is alpha-blended, unlit and
        /// double-sided вЂ” exactly a pane. Falls back to the next transparent shader, then to the dark
        /// detail material (the panes then read as dark metal).
        /// </summary>
        private static Material GlassMaterial()
        {
            if (mGlass != null) return mGlass;
            foreach (var nm in new[] { "Sprites/Default", "Legacy Shaders/Particles/Alpha Blended",
                                       "Particles/Standard Unlit" })
            {
                var sh = Shader.Find(nm);
                if (sh == null || !sh.isSupported) continue;
                var m = new Material(sh) { name = "CoopGlass (" + nm + ")" };
                if (m.HasProperty("_Color")) m.SetColor("_Color", new Color(0.70f, 0.83f, 0.88f, 0.22f));
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", Texture2D.whiteTexture);
                log.LogInfo($"[PENTHOUSE] glass material: {nm} (alpha 0,22)");
                mGlass = m;
                return m;
            }
            log.LogInfo("[PENTHOUSE] glass material: WARNING no transparent shader вЂ” the panes are dark");
            mGlass = mDark;
            return mGlass;
        }

        /// <summary>v0.9.18: a warm lamp for the penthouse's room (see `Penthouse`).</summary>
        private static void Lamp(Transform parent, Vector3 local, float intensity, float range)
        {
            var go = new GameObject("penthouse_lamp");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.80f, 0.58f);
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForcePixel;
        }
    }
}
