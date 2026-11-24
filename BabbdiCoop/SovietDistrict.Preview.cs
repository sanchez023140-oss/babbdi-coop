using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace BabbdiCoop
{
    // Part of the SovietDistrict builder - see SovietDistrict.cs.
    internal static partial class SovietDistrict
    {

        public static List<PreviewShot> PreviewPoses()
        {
            var l = new List<PreviewShot>();
            if (root == null) return l;
            Func<Vector3, Vector3, PreviewShot> P = (a, b) => new PreviewShot
            {
                Pos = root.TransformPoint(a),
                Look = root.TransformPoint(b)
            };
            // v0.9.12: same, but lit вЂ” the level has no sun and ambient 0, so a facade in shadow is
            // practically black in a still; the lighting is a debug aid, the player never sees it
            Func<Vector3, Vector3, PreviewShot> PL = (a, b) => new PreviewShot
            {
                Pos = root.TransformPoint(a),
                Look = root.TransformPoint(b),
                Lamp = true
            };
            // TEMP (v0.9.21): the game's own humanoids, up close вЂ” the anatomy reference the five figures
            // on the House of Soviets' roof were built from. World space (the NPCs live in the level, not
            // under the district root), so no TransformPoint. Three named standing NPCs, two views each;
            // SpriteRenderer / ParticleSystemRenderer are skipped (the dialogue sprite of one NPC has a
            // 19 m bounds and is what ruined the first attempt at this shot).
            {
                var npcs = Resources.FindObjectsOfTypeAll<NPC>();
                var want = new[] { "SM_Amassou00", "SM_A_Ashraf00 (1)", "SM_A_Mina00" };
                foreach (var n in npcs)
                {
                    bool hit = false;
                    foreach (var w in want) if (n.name == w) hit = true;
                    if (!hit) continue;
                    var rs0 = n.GetComponentsInChildren<Renderer>(true);
                    var b = new Bounds(n.transform.position, Vector3.zero);
                    bool first = true;
                    foreach (var r in rs0)
                    {
                        if (r is SpriteRenderer) continue;
                        var tn = r.GetType().Name;
                        if (tn.StartsWith("Particle")) continue;
                        if (r.bounds.size == Vector3.zero) continue;
                        if (first) { b = r.bounds; first = false; }
                        else b.Encapsulate(r.bounds);
                    }
                    if (first || b.size.y < 1.0f || b.size.y > 2.6f) continue;   // not a standing body
                    var c = b.center;
                    float d = Mathf.Clamp(b.size.y * 1.35f, 2.4f, 4.0f);
                    l.Add(new PreviewShot { Pos = c + new Vector3(d * 0.62f, 0.15f, -d * 0.72f), Look = c, Lamp = true });
                    l.Add(new PreviewShot { Pos = c + new Vector3(-d * 0.80f, 0.15f, -d * 0.42f), Look = c, Lamp = true });
                }
            }
            l.Add(P(new Vector3(205f, 3f, -8f), new Vector3(205f, 14f, 90f)));    // stepping off the old crest
            l.Add(P(new Vector3(110f, 2f, 40f), new Vector3(250f, 25f, 120f)));  // street level in the ring
            l.Add(P(new Vector3(190f, 130f, -260f), new Vector3(190f, 20f, 110f))); // from the city, above the dam
            l.Add(P(new Vector3(60f, 70f, 420f), new Vector3(190f, 25f, 90f)));  // from the sea
            l.Add(P(new Vector3(60f, 130f, -60f), new Vector3(200f, 10f, 110f))); // the ring from high above
            l.Add(P(new Vector3(230f, 130f, -180f), new Vector3(230f, 70f, 84f)));  // House of Soviets, the whole monument
            l.Add(PL(new Vector3(120f, 30f, -40f), new Vector3(230f, 70f, 84f)));   // House of Soviets, 3/4 from the north-west
            // v0.9.17: the hall of the House of Soviets from high up its west aisle вЂ” the 25,6 m hall with
            // the two tower masses standing **in** it (they used to hang 10 m up, hidden above the roof
            // ring; that empty cavern is exactly what the user's screenshot showed). House-local
            // (-49, 18, -22,6) в†’ (-49, 2, 20), with the House's 4В° yaw baked in.
            l.Add(PL(new Vector3(179.5f, 28f, 64.9f), new Vector3(182.5f, 12f, 107.4f)));

            // v0.9.9: the terraced platform вЂ” the wall of the +8 m terrace from the street, the ring
            // ramp, the grand stair up to the House's plinth and the wing ramp to +14 m
            l.Add(P(new Vector3(56f, 2f, 150f), new Vector3(56f, 9f, 216f)));      // up the ring ramp
            l.Add(P(new Vector3(200f, 2f, 196f), new Vector3(200f, 11f, 214f)));  // a street under the +8 m terrace wall
            l.Add(P(new Vector3(230f, 2f, 2f), new Vector3(230f, 12.5f, 62f)));    // grand stair to the plinth
            l.Add(P(new Vector3(62f, 10f, 255f), new Vector3(-12f, 17f, 255f)));   // wing ramp to +14 m
            l.Add(P(new Vector3(330f, 10f, 292f), new Vector3(60f, 11f, 292f)));   // the seaward promenade at +8 m

            // inside a block: corridor on the second floor, and the staircase
            if (bldgs.Count > 0)
            {
                var b = bldgs[0];
                var rot = Quaternion.Euler(0f, b.yaw, 0f);
                float bfh = b.fh;
                float yf = b.baseY + 2f * bfh + 1.55f;
                Vector3 c0 = b.p + rot * new Vector3(0f, yf, 0f);
                Vector3 c1 = b.p + rot * new Vector3(b.w * 0.5f - 2f, yf, 0f);
                l.Add(P(c0, c1));                                               // corridor
                float zi = b.d * 0.5f - 0.4f;
                float zm = (b.stairZ0 + b.stairZ1) * 0.5f;
                Vector3 sa = b.p + rot * new Vector3(b.stairX1 - 0.45f, b.baseY + bfh + 1.60f, b.stairZ1 - 0.55f);
                Vector3 sb = b.p + rot * new Vector3(b.stairX0 + 1.5f, b.baseY + bfh + 1.25f, zm);
                l.Add(P(sa, sb));                                               // staircase, diagonal view
                Vector3 ra = b.p + rot * new Vector3(b.stairX1 + 2.4f, b.baseY + bfh + 1.5f, -(b.CorrHalf + 0.8f));
                Vector3 rb = b.p + rot * new Vector3(b.stairX1 + 2.4f, b.baseY + bfh + 1.2f, -zi);
                l.Add(P(ra, rb));                                               // a room

                // v0.9.11: stand in the room behind each recorded window and look out вЂ” the direct
                // evidence that no partition, no corridor wall and no stair flight sits behind it
                for (int i = 0; i < b.winPos.Count && i < b.winDir.Count; i++)
                {
                    Vector3 n = rot * b.winDir[i];
                    Vector3 c = b.p + rot * b.winPos[i];
                    l.Add(P(c - n * 5.5f, c));
                }
            }

            // v0.9.11: a stalinka as well вЂ” its 5.6 m storeys, its taller doors and its stair that now
            // runs along z (and a window from outside, to check that nothing sits behind the opening)
            for (int i = 0; i < bldgs.Count; i++)
            {
                var b = bldgs[i];
                if (!b.Tall) continue;
                var rot = Quaternion.Euler(0f, b.yaw, 0f);
                float fh = b.fh;
                float y = b.baseY + fh + 1.7f;
                l.Add(P(b.p + rot * new Vector3(b.w * 0.5f - 2.5f, y, 0f),
                        b.p + rot * new Vector3(0f, y, 0f)));                     // stalinka corridor
                l.Add(P(b.p + rot * new Vector3(b.stairX1 - 2.4f, b.baseY + fh + 1.65f, b.stairZ0 + 0.7f),
                        b.p + rot * new Vector3(b.stairX0 + 1.2f, b.baseY + fh * 0.35f, b.stairZ1 - 0.5f)));  // its stair
                // v0.9.16: the user's own frame вЂ” from the half-landing looking UP the upper flight,
                // which is exactly where "the stairs run into a blind ceiling"; and from the landing at
                // the corridor doorway looking up the lower flight
                float xm = (b.stairX0 + b.stairX1) * 0.5f;
                float zLow = b.stairZ0 + b.stairLand, zHigh = zLow + b.stairRun;
                l.Add(P(b.p + rot * new Vector3(xm, b.baseY + 2f * fh + fh * 0.5f + 1.65f,
                                                b.stairZ1 - b.stairLand * 0.55f),
                        b.p + rot * new Vector3(xm, b.baseY + 3f * fh - 0.3f, zLow + b.stairRun * 0.15f)));
                l.Add(P(b.p + rot * new Vector3(xm, b.baseY + fh + 1.65f, b.stairZ0 + b.stairLand * 0.5f),
                        b.p + rot * new Vector3(xm, b.baseY + fh + fh * 0.5f, zHigh - 0.3f)));
                Vector3 face = b.p + rot * new Vector3(0f, b.baseY + fh * 1.6f, b.d * 0.5f);
                l.Add(P(face + rot * new Vector3(0f, 0f, 22f), face));            // its facade from the street
                break;
            }
            // v0.9.12: a block with balconies, from the street вЂ” the evidence that they now form
            // vertical stacks on the window columns with a door cut behind each one. The first is an
            // orthographic elevation (the exact geometry), the second a lit street view from below.
            for (int i = 0; i < bldgs.Count; i++)
            {
                var b = bldgs[i];
                if (!b.balconies) continue;
                var rot = Quaternion.Euler(0f, b.yaw, 0f);
                float hd = b.d * 0.5f;
                float cy = b.baseY + 5f * b.fh;
                // 3/4 view from above-right, orthographic: the slabs and parapets separate from the
                // wall only when their tops and edges are in frame вЂ” a straight front elevation hides
                // them completely (same material, no ambient occlusion in this level)
                l.Add(new PreviewShot
                {
                    Pos = root.TransformPoint(b.p + rot * new Vector3(24f, cy + 14f, hd + 26f)),
                    Look = root.TransformPoint(b.p + rot * new Vector3(0f, cy, hd)),
                    Ortho = 20f,
                    Lamp = true
                });
                // and the view the player actually gets from the street, under the balconies
                Vector3 face = b.p + rot * new Vector3(0f, b.baseY + 3f * b.fh, hd);
                l.Add(PL(face + rot * new Vector3(8f, -11f, 22f), face + rot * new Vector3(0f, 3f, 0f)));
                break;
            }
            if (bridges.Count > 0)
            {
                var s = bridges[0];
                Vector3 d = s.b - s.a;
                d.y = 0f;
                Vector3 dir = d.normalized;
                Vector3 mid = s.a + dir * Mathf.Max(1.2f, d.magnitude * 0.5f) + Vector3.up * (s.y + 1.5f);
                l.Add(P(s.a + dir * 1.4f + Vector3.up * (s.y + 1.5f), s.a + dir * 18f + Vector3.up * (s.y + 1.4f))); // inside a covered bridge
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                l.Add(P(s.a + side * 16f + dir * 4f + Vector3.up * (s.y + 7f),
                        s.a + dir * (d.magnitude * 0.5f) + Vector3.up * (s.y + 1.5f)));  // a bridge from outside
            }

            // the joint itself (v0.9.6): stand seaward of the seam and look back along it вЂ” the old dam
            // crest on the left, the new platform on the right. The user's screenshot 1 lives here: the
            // two must read as one surface (tone, texture, grain), which is what Stone() is for.
            l.Add(P(new Vector3(208f, 1.8f, 13f), new Vector3(196f, 0.55f, 0f)));

            // v0.9.13: the corridor at its widest (4x) and at its narrowest (2x), same camera as the
            // stalinka hall вЂ” the evidence that the 2,2 m tunnel is gone
            foreach (int want in new[] { 4, 2 })
            {
                var b = bldgs.Find(x => x.corrMul == want);
                if (b == null) continue;
                var rot = Quaternion.Euler(0f, b.yaw, 0f);
                float y = b.baseY + 2f * b.fh + 1.7f;
                l.Add(P(b.p + rot * new Vector3(b.w * 0.5f - 2.5f, y, 0f),
                        b.p + rot * new Vector3(0f, y, 0f)));                      // 4x / 2x corridor
            }

            // v0.9.14: the memorial on the second-tallest roof вЂ” head-on, the reference's own low 3/4
            // (eye level, which is where the red slits must read), an elevated 3/4, and a mid-distance
            // shot from the neighbouring rooftops
            if (memBlock != null)
            {
                var rot = Quaternion.Euler(0f, memBlock.yaw, 0f);
                float y = memBlock.Top + 0.26f;
                l.Add(P(memBlock.p + rot * new Vector3(0f, y + 2.8f, 14f),
                        memBlock.p + rot * new Vector3(0f, y + 5.2f, 0f)));         // memorial, head-on
                l.Add(P(memBlock.p + rot * new Vector3(16f, y + 1.7f, 16f),
                        memBlock.p + rot * new Vector3(0f, y + 5.4f, 0f)));         // memorial, low 3/4
                l.Add(P(memBlock.p + rot * new Vector3(12.5f, y + 9.5f, 12.5f),
                        memBlock.p + rot * new Vector3(0f, y + 4.6f, 0f)));         // memorial, high 3/4
                l.Add(P(memBlock.p + rot * new Vector3(18f, y + 2.5f, 34f),
                        memBlock.p + rot * new Vector3(0f, y + 5f, 0f)));           // from the next roof
            }

            // v0.9.17: the House of Soviets as rebuilt вЂ” (a) the aisle down the west side of its hall
            // (a tower wall on one side, the podium's windows on the other), (b) the walk up the
            // approach avenue at eye level, (c) the podium facade head-on: 4,75 x 3,00 m openings of
            // which every second one is gone.
            l.Add(PL(new Vector3(179.6f, PlinthH + 3f, 66.5f), new Vector3(182.4f, PlinthH + 4f, 105.4f)));  // hall aisle
            l.Add(PL(new Vector3(230f, 1.8f, -34f), new Vector3(230f, PlinthH + 60f, 84f)));                 // approach avenue
            l.Add(PL(new Vector3(230f, PlinthH + 13f, 6f), new Vector3(230f, PlinthH + 13f, 60f)));          // podium facade

            // v0.9.18: the clock tower on the block on the peninsula вЂ” (a) the whole tower 3/4 from the
            // sea, (b) head-on at the clock storey (the reference's own framing: one great dial per
            // face), (c) the mansard from above (the two rows of skylights and the glass lantern),
            // (d) the atrium from the ground floor, looking up the void at the lift shaft.
            if (pentBlock != null)
            {
                var rot = Quaternion.Euler(0f, pentBlock.yaw, 0f);
                float pw = pentBlock.w * 0.5f, pd = pentBlock.d * 0.5f;
                float y0 = pentDeckY;
                l.Add(P(pentBlock.p + rot * new Vector3(pw + 48f, y0 + 26f, pd + 48f),
                        pentBlock.p + rot * new Vector3(0f, y0 + 19f, 0f)));        // the whole tower, 3/4
                l.Add(P(pentBlock.p + rot * new Vector3(0f, y0 + 16.3f, pd + 40f),
                        pentBlock.p + rot * new Vector3(0f, y0 + 16.3f, 0f)));      // head-on, the clock
                l.Add(P(pentBlock.p + rot * new Vector3(pw * 1.2f, y0 + 58f, pd * 1.6f),
                        pentBlock.p + rot * new Vector3(0f, y0 + 34f, 0f)));        // the mansard from above
                l.Add(P(pentBlock.p + rot * new Vector3(-3.2f, y0 + 1.75f, -3.2f),
                        pentBlock.p + rot * new Vector3(0.6f, y0 + 19f, 1.4f)));    // the atrium, from below
            }

            // v0.9.19/20: **one** house on legs (the pair is merged into a single rectangle in plan, the
            // western part lower) вЂ” (a) the pylons from the street under the transfer girder, (b) the whole
            // mass from the crest side, the reference's own framing, (c) through the open base between the
            // legs, which must read as a colonnade of V-shaped haunches
            var legsAll = bldgs.FindAll(x => x.legs);
            if (legsAll.Count > 0)
            {
                Vector3 lo = new Vector3(float.MaxValue, 0f, float.MaxValue);
                Vector3 hi = new Vector3(float.MinValue, 0f, float.MinValue);
                float top = 0f;
                foreach (var b in legsAll)
                {
                    var rr = Quaternion.Euler(0f, b.yaw, 0f);
                    for (int c = 0; c < 4; c++)
                    {
                        Vector3 cr = b.p + rr * new Vector3(c % 2 == 0 ? -b.w * 0.5f : b.w * 0.5f, 0f,
                                                            c / 2 == 0 ? -b.d * 0.5f : b.d * 0.5f);
                        lo = Vector3.Min(lo, cr);
                        hi = Vector3.Max(hi, cr);
                    }
                    top = Mathf.Max(top, b.floors * b.fh);
                }
                var ctr = new Vector3((lo.x + hi.x) * 0.5f, 0f, (lo.z + hi.z) * 0.5f);
                float span = Mathf.Max(hi.x - lo.x, hi.z - lo.z);
                float yb = legsAll[0].baseY;
                l.Add(PL(new Vector3(ctr.x, yb + 2.5f, hi.z + 62f),
                         new Vector3(ctr.x, yb + 11f, hi.z)));             // the legs from the street
                l.Add(P(new Vector3(ctr.x, top * 0.72f, lo.z - 250f),
                        new Vector3(ctr.x, top * 0.34f, ctr.z)));          // the whole house, from the crest
                l.Add(P(new Vector3(lo.x - span * 0.55f, yb + 3.2f, hi.z + span * 0.95f),
                        new Vector3(ctr.x, yb + 9f, ctr.z)));              // the colonnade under the girder
            }
            // v0.9.20/21: the platform's south-east corner next to the round hole in the sea вЂ” the +5 m
            // east terrace used to start right at the crest there, so the corner read as a bare 5 m ledge
            // above the water; it is now set back 12 m and the corner is one skin from the crest's granite
            // to the terrace's wall. (a) standing on the deck west of it, looking along the transition at
            // eye level вЂ” the change as it is met in play, (b) walking the edge east towards the hole,
            // (c) a low 3/4 from over the water (the "floating plate" the user photographed),
            // (d) a 3/4 from above, (e) the corner in plan.
            l.Add(PL(new Vector3(316f, 1.7f, 6f), new Vector3(342f, 0.35f, 4f)));
            l.Add(P(new Vector3(300f, 1.75f, 3f), new Vector3(372f, 0.6f, 8f)));
            l.Add(PL(new Vector3(398f, 9f, -22f), new Vector3(346f, 0.3f, 5f)));
            l.Add(PL(new Vector3(366f, 24f, -26f), new Vector3(338f, 0.3f, 6f)));
            l.Add(new PreviewShot
            {
                Pos = root.TransformPoint(new Vector3(340f, 55f, 6f)),
                Look = root.TransformPoint(new Vector3(340f, 0f, 6f)),
                Ortho = 34f
            });
            // v0.9.22: two views of the *walk itself*, which is what the user's screenshots 1-2 were
            // about вЂ” (f) coming in from the city along the crest's granite, i.e. the approach where the
            // concrete must emerge from under the stone without a lip, (g) standing on the plate and
            // looking back north-west along the seam + the bevel, the user's screenshot 2's framing.
            l.Add(PL(new Vector3(348f, 1.75f, -20f), new Vector3(352f, 0.5f, 6f)));
            l.Add(PL(new Vector3(340f, 1.65f, 9f), new Vector3(318f, 0.35f, 4f)));

            // v0.9.21: the five figures on the front edge of the House of Soviets' podium roof (see
            // Figures) вЂ” (a) from the plinth below, the way a player sees them: a line against the sky,
            // (b) a 3/4 from the front-left, (c) the middle figure up close, (d) along the row.
            {
                var fh = new Vector3(230f, PlinthH, 84f);
                var fr = Quaternion.Euler(0f, 4f, 0f);
                Vector3 roof = fh + fr * new Vector3(0f, 25.75f, -22.5f);
                l.Add(PL(fh + fr * new Vector3(0f, 27f, -96f), roof + Vector3.up * 0.9f));
                l.Add(PL(fh + fr * new Vector3(0f, 34f, -104f), fh + fr * new Vector3(0f, 26.6f, -22.5f)));
                // v0.9.26: the close-ups are framed off the figures' own feet (FigFeet), 3,2 m in
                // front of the row at 1,55 m and aimed at the chest. The old fixed offsets from the
                // roof centre (5,5-7,5 m, and 2,7 m further back than the figures actually stand)
                // left a 2,1 m figure at a third of the frame and the faces unreadable.
                Vector3 fMid = FigFeet.Count == 7 ? FigFeet[2] : roof + new Vector3(0f, 0f, -2.9f);
                Vector3 fEnd = FigFeet.Count == 7 ? FigFeet[4] : roof + new Vector3(40f, 0f, -2.9f);
                // v0.9.29: the five are 2,05-2,70 m tall now, so every close-up is framed off that
                // figure's OWN height (0,72 h for the camera, 0,47 h for the chest) - a fixed 1,55 m
                // would shoot the 2,70 m officer at the waist.
                float hm = FigHi(2), he = FigHi(4);
                l.Add(PL(fMid + new Vector3(0f, 0.72f * hm, 3.2f), fMid + new Vector3(0f, 0.47f * hm, 0f)));
                l.Add(PL(fh + fr * new Vector3(44f, 27.4f, -30f), fh + fr * new Vector3(-40f, 26.6f, -22.5f)));
                l.Add(PL(fMid + new Vector3(-2.8f, 0.71f * hm, 3.3f), fMid + new Vector3(0f, 0.47f * hm, 0f)));
                // v0.9.22: the jumpsuit worker up close вЂ” the one figure in the game's own **orange**,
                // which is the palette match the user's reference stills are about
                l.Add(PL(fEnd + new Vector3(0f, 0.70f * he, 3.2f), fEnd + new Vector3(0f, 0.47f * he, 0f)));
            }
            // v0.9.79: the Mi-26 on the chosen Stalin skyscraper's roof вЂ” from the open seaward
            // side at roof height and a 3/4 from above, so the rotor disc and the tail read
            // against the sky; both cameras are clear of the neighbouring blocks.
            if (heliBlock != null)
            {
                var hr = Quaternion.Euler(0f, HeliYaw, 0f);
                Vector3 hp = HeliSpot;
                l.Add(PL(hp + hr * new Vector3(0f, 24f, 72f), hp + Vector3.up * 8.5f));
                l.Add(PL(hp + hr * new Vector3(48f, 34f, 58f), hp + Vector3.up * 7.0f));
                l.Add(PL(hp + hr * new Vector3(0f, 4.8f, 22f), hp + hr * new Vector3(0f, 5.0f, 0f)));
                l.Add(PL(hp + hr * new Vector3(22f, 4.6f, 15f), hp + hr * new Vector3(3f, 4.0f, 0f)));
            }
            // v0.9.23: the user's own framing of the plate вЂ” standing on the deck south-west of the corner
            // looking east (the +5 terrace's wall ahead-left, the crest's granite on the right, the sea
            // beyond), and the same transition walked straight up from the south (the row that used to be
            // a 0,25-0,36 m wall at z = 12,3).
            l.Add(PL(new Vector3(301f, 1.7f, 15f), new Vector3(344f, 0.5f, 3f)));
            l.Add(PL(new Vector3(324f, 1.7f, 19f), new Vector3(327f, 0.4f, 6f)));
            // v0.9.25: the other two figures up close вЂ” the scientist (white lab coat, blue shirt,
            // dark bob) and the docker (navy beanie, orange hi-vis vest). They are the first two of
            // the row (x -40 / -20), so the v0.9.22 poses above never framed them.
            {
                var fh = new Vector3(230f, PlinthH, 84f);
                var fr = Quaternion.Euler(0f, 4f, 0f);
                Vector3 roof = fh + fr * new Vector3(0f, 25.75f, -22.5f);
                Vector3 f0 = FigFeet.Count == 7 ? FigFeet[0] : roof + new Vector3(-40f, 0f, -2.9f);
                Vector3 f1 = FigFeet.Count == 7 ? FigFeet[1] : roof + new Vector3(-20f, 0f, -2.9f);
                float h0 = FigHi(0), h1 = FigHi(1);
                l.Add(PL(f0 + new Vector3(0f, 0.72f * h0, 3.2f), f0 + new Vector3(0f, 0.47f * h0, 0f)));
                l.Add(PL(f1 + new Vector3(0f, 0.70f * h1, 3.2f), f1 + new Vector3(0f, 0.47f * h1, 0f)));
            }
            // v0.9.26: the officer (figure 4, x +20) up close вЂ” the peaked cap, the greatcoat, the
            // belt and the white dress gloves. Appended last so the earlier shot numbers stay put.
            {
                var fh = new Vector3(230f, PlinthH, 84f);
                var fr = Quaternion.Euler(0f, 4f, 0f);
                Vector3 roof = fh + fr * new Vector3(0f, 25.75f, -22.5f);
                Vector3 f3 = FigFeet.Count == 7 ? FigFeet[3] : roof + new Vector3(20f, 0f, -2.9f);
                float h3 = FigHi(3);
                l.Add(PL(f3 + new Vector3(0f, 0.72f * h3, 3.2f), f3 + new Vector3(0f, 0.52f * h3, 0f)));
            }
            // v0.9.78: the two helicopter pilots at the end of the row вЂ” the flight helmet with its
            // lowered, semi-transparent visor and the chest gear are the whole point of them.
            {
                var fh = new Vector3(230f, PlinthH, 84f);
                var fr = Quaternion.Euler(0f, 4f, 0f);
                Vector3 roof = fh + fr * new Vector3(0f, 25.75f, -22.5f);
                Vector3 f5 = FigFeet.Count == 7 ? FigFeet[5] : roof + new Vector3(40f, 0f, -2.9f);
                Vector3 f6 = FigFeet.Count == 7 ? FigFeet[6] : roof + new Vector3(60f, 0f, -2.9f);
                float h5 = FigHi(5), h6 = FigHi(6);
                l.Add(PL(f5 + new Vector3(0f, 0.70f * h5, 3.0f), f5 + new Vector3(0f, 0.50f * h5, 0f)));
                l.Add(PL(f6 + new Vector3(0f, 0.70f * h6, 3.0f), f6 + new Vector3(0f, 0.50f * h6, 0f)));
                // a head close-up on each visor, from slightly above so the tint reads
                l.Add(PL(f5 + new Vector3(0.9f, 0.86f * h5, 1.5f), f5 + new Vector3(0f, 0.84f * h5, 0f)));
                l.Add(PL(f6 + new Vector3(0.9f, 0.86f * h6, 1.5f), f6 + new Vector3(0f, 0.84f * h6, 0f)));
            }
            // v0.9.59: the rebuilt House of Soviets вЂ” envelope, honeycomb towers, loggia volume, hall,
            // entrance pavilion and the rear wing.
            {
                var fh0 = new Vector3(230f, PlinthH, 84f);
                var fr0 = Quaternion.Euler(0f, 4f, 0f);
                Func<float, float, float, Vector3> HL = (x, y, z) => fh0 + fr0 * new Vector3(x, y, z);
                // (a) the whole front from the esplanade вЂ” the "H": two slabs and the open gap
                l.Add(PL(HL(0f, 22f, -150f), HL(0f, 45f, -20f)));
                // (b) a 3/4 from the front-left
                l.Add(PL(HL(-90f, 55f, -100f), HL(0f, 45f, 0f)));
                // (c) the gap and the bridge head-on from below
                l.Add(PL(HL(0f, 30f, -70f), HL(0f, 50f, -8f)));
                // (d) inside the hall, from the entrance towards the colonnade
                l.Add(PL(HL(0f, 2f, -22f), HL(0f, 8f, 14f)));
                // (e) a tower's honeycomb above the stylobate roof
                l.Add(PL(HL(-30.5f, 16f, -45f), HL(-30.5f, 20f, -11f)));
                // (f) on the stylobate roof between the towers, up at the bridge
                l.Add(PL(HL(0f, 14f, -22f), HL(0f, 30f, -8f)));
                // (g) the rear blocks and the mast from behind
                l.Add(PL(HL(0f, 30f, 80f), HL(0f, 55f, 24.65f)));
                // (h) the stylobate's entrance from the esplanade
                l.Add(PL(HL(0f, 2f, -70f), HL(0f, 7f, -24.65f)));
                // (k) v0.9.68: the hall's central passage, looking at the towers from the axis
                l.Add(PL(HL(0f, 2f, 0f), HL(15.5f, 6f, 0f)));
                // (l) under the stylobate's overhang (the arcade), towards the porch
                l.Add(PL(HL(-6f, 2f, -23.5f), HL(-6f, 5f, -17f)));
                // (i) the House in the district's skyline from the north-east
                l.Add(PL(HL(60f, 45f, -210f), HL(0f, 45f, -20f)));
                // (j) the plan from above (the two slabs, the loggia volume and the rear wing)
                l.Add(new PreviewShot
                {
                    Pos = root.TransformPoint(HL(0f, 245f, 2f)),
                    Look = root.TransformPoint(HL(0f, 0f, 2f)),
                    Ortho = 95f,
                    Lamp = true
                });
                // (m) v0.9.69: the user's own framed view of the "blocked" passage вЂ” across the hall's
                //     central strip, from the west tower's entrance towards the east tower's portal
                l.Add(PL(HL(-14f, 1.8f, 2f), HL(45f, 4f, 2f)));
                // (n) the east tower's ground-floor portal up close (it must be an open 3,4 x 4,4 m
                //     passage, not a 1,7 m slit behind a concrete fin)
                l.Add(PL(HL(9f, 1.8f, 2f), HL(16f, 3.6f, 2f)));
                // (o) the pair of gargoyles on the portal canopy, from the esplanade below
                l.Add(PL(HL(0f, 2f, -48f), HL(0f, 13.5f, -27f)));
                // (p) v0.9.98: the air-hockey table on the annex's top floor (house-local 102,55 m) вЂ”
                //     a 3/4 from the east, the table broadside from the south-west, and from outside the
                //     west facade, through one of the annex's open windows
                l.Add(PL(HL(-46f, 106.4f, -8f), HL(-37.75f, 104.4f, 3f)));
                l.Add(PL(HL(-60f, 106.6f, 3f), HL(-37.75f, 104.6f, 3f)));
            }
            // v0.9.35: the stair well and the street cores вЂ” the two things the user's screenshots were
            // about: (a) the first flight seen from the arrival landing, looking up along it (their
            // frame 1), (b) the open well from the third storey, straight down the shaft (their frame
            // 2), (c) a block on legs: the core's three flights from the deck, (d) the first-floor
            // opening of that core, (e) the same house's corridor along the new opening.
            {
                Bldg h = null;
                for (int i = 0; i < bldgs.Count; i++) if (bldgs[i].Tall && bldgs[i].bands) { h = bldgs[i]; break; }
                if (h == null) for (int i = 0; i < bldgs.Count; i++) if (bldgs[i].Tall) { h = bldgs[i]; break; }
                if (h != null)
                {
                    var rot = Quaternion.Euler(0f, h.yaw, 0f);
                    float fh = h.fh, xs = h.stairX0;
                    float zLow = h.stairZ0 + h.stairLand, zHigh = zLow + h.stairRun;
                    float xm = xs + h.stairFw * 0.5f;
                    // (a) from the stair-hall doorway, across the well at the first flight вЂ” the whole
                    // stairwell's side profile in one frame (the "are they plates?" shot)
                    l.Add(P(h.p + rot * new Vector3(h.stairX0 + (h.stairX1 - h.stairX0) * 0.85f,
                                                    h.baseY + fh + 1.65f, h.stairZ0 + 0.5f),
                            h.p + rot * new Vector3(h.stairX0 + h.stairFw * 0.5f, h.baseY + fh + 1.20f,
                                                    h.stairZ0 + h.stairLand + h.stairRun * 0.6f)));
                    // (b) the well from the top floor, straight down the shaft between the railings
                    float xc = xs + h.stairFw + h.stairWell * 0.5f;
                    l.Add(P(h.p + rot * new Vector3(xc, h.baseY + 4f * fh + 1.55f, zLow - 0.45f),
                            h.p + rot * new Vector3(xc, h.baseY + 0.6f, zLow + 2.2f)));
                    // (f) standing in the well on the ground floor, straight up the slot: the open
                    // shaft the user asked for, from the bottom of the house to the roof
                    l.Add(P(h.p + rot * new Vector3(xc, h.baseY + 1.65f, zLow + 1.2f),
                            h.p + rot * new Vector3(xc, h.baseY + 6f * fh, zLow + 1.2f)));
                }
                Bldg g = null;
                for (int i = 0; i < bldgs.Count; i++) if (bldgs[i].legs) { g = bldgs[i]; break; }
                if (g != null)
                {
                    var rot = Quaternion.Euler(0f, g.yaw, 0f);
                    float yTop = g.baseY + 3f * g.fh;
                    float xc = (g.coreX0 + g.coreX1) * 0.5f;
                    // (c) the core's own half-landing on the top storey, looking up the last flight:
                    // the frame the user photographed, where the flight used to end inside the slab
                    l.Add(P(g.p + rot * new Vector3(xc, g.baseY + 3f * g.fh - 3.0f + 1.65f, -0.6f),
                            g.p + rot * new Vector3(xc, yTop + 0.7f, 2.2f)));
                    // (d) the first-floor opening: standing in the corridor, looking down into it
                    l.Add(P(g.p + rot * new Vector3(xc + 3.4f, yTop + 1.65f, g.coreHoleZ0 + 1.2f),
                            g.p + rot * new Vector3(xc, yTop - 1.6f, g.coreHoleZ0 + 1.2f)));
                    // (e) the corridor along the opening, its railings and the stair hall behind
                    l.Add(P(g.p + rot * new Vector3(xc + 4.2f, yTop + 1.65f, g.coreHoleZ0 + 0.4f),
                            g.p + rot * new Vector3(xc - 1.2f, yTop - 0.2f, g.coreHoleZ0 + 1.6f)));
                }
                // v0.9.37: **the plate itself** вЂ” (A) standing under flight 2 and looking straight up at
                // its soffit (it must be closed: no see-through), (B) from the next storey's
                // half-landing, looking down a flight from above вЂ” the user's reference photo's angle.
                if (h != null)
                {
                    var rot = Quaternion.Euler(0f, h.yaw, 0f);
                    float zLow = h.stairZ0 + h.stairLand;
                    float zHigh = zLow + h.stairRun;
                    float fw = h.stairFw, well = h.stairWell;
                    float xm = h.stairX0 + fw * 0.5f;
                    l.Add(P(h.p + rot * new Vector3(h.stairX0 + fw + well + fw * 0.5f, h.baseY + 1.55f,
                                                    zLow + h.stairRun * 0.55f),
                            h.p + rot * new Vector3(h.stairX0 + fw + well + fw * 0.5f, h.baseY + 4.2f,
                                                    zLow + h.stairRun * 0.55f)));
                    l.Add(P(h.p + rot * new Vector3(xm, h.baseY + 1.5f * h.fh + 1.55f, zHigh + 0.6f),
                            h.p + rot * new Vector3(xm, h.baseY + 0.5f * h.fh, zLow + 0.9f)));
                }
                // v0.9.45: a top view of the south-east corner / crest seam вЂ” the spot the user circled
                // in their own aerial screenshot (a small dark rectangle on the dam's granite)
                l.Add(new PreviewShot
                {
                    Pos = root.TransformPoint(new Vector3(350f, 40f, 12f)),
                    Look = root.TransformPoint(new Vector3(350f, 0f, 11.99f)),
                    Ortho = 42f
                });
                // v0.9.50: the video's own angle вЂ” standing on the crest's granite just north of the seam,
                // looking south-west along it: the thin concrete plate that used to lie here must be gone,
                // the stone must meet the pad almost flush (10 mm) with no overhanging edge.
                l.Add(PL(new Vector3(352f, 1.35f, -3.2f), new Vector3(340f, 0.30f, 2.5f)));
                l.Add(PL(new Vector3(345f, 5.5f, -2.5f), new Vector3(340f, 0.30f, 3.5f)));
                // v0.9.51: the user's own framing вЂ” standing on the corner pad two metres off the +5 m
                // terrace's south wall and looking east along it: the wall must run straight to its end
                // and meet the plate perpendicularly, with nothing of ours sticking out past its end.
                l.Add(PL(new Vector3(334f, 1.65f, 9.4f), new Vector3(383f, 0.85f, 9.0f)));
                // v0.9.51 diagnostics: the corner from the sea and from the crest side, elevated
                l.Add(PL(new Vector3(392f, 7.0f, 34f), new Vector3(370f, 1.0f, 6f)));
                l.Add(PL(new Vector3(330f, 10.0f, -10f), new Vector3(374f, 1.0f, 9f)));
                // v0.9.42: the user's circle вЂ” the floor plate's end face at the foot of a flight, seen
                // from the arrival landing (the face that was culled/invisible)
                if (h != null)
                {
                    var rot = Quaternion.Euler(0f, h.yaw, 0f);
                    float zLow = h.stairZ0 + h.stairLand;
                    l.Add(P(h.p + rot * new Vector3(h.stairX0 + h.stairFw * 0.30f,
                                                    h.baseY + h.fh + 1.62f, zLow - 1.70f),
                            h.p + rot * new Vector3(h.stairX0 + h.stairFw * 0.95f,
                                                    h.baseY + h.fh - 0.10f, zLow + 0.30f)));
                    // and the same end face head-on: from inside the well, at the slab's mid-thickness
                    l.Add(P(h.p + rot * new Vector3(h.stairX0 + h.stairFw * 0.5f,
                                                    h.baseY + h.fh - 0.13f, zLow + 1.10f),
                            h.p + rot * new Vector3(h.stairX0 + h.stairFw * 0.5f,
                                                    h.baseY + h.fh - 0.28f, zLow - 0.80f)));
                }
            }
            // v0.9.70: reference shots of the **main BABBDI city** at the player's spawn вЂ” the exact
            // concrete, grain and ground the district has to match (plan points 1-2). World space.
            try
            {
                var fpc = FirstPersonController.instance;
                if (fpc != null)
                {
                    var cc = fpc.GetComponent<CharacterController>();
                    Vector3 feet = cc != null
                        ? fpc.transform.TransformPoint(cc.center) - Vector3.up * (cc.height * 0.5f)
                        : fpc.transform.position;
                    var cam = fpc.GetComponentInChildren<Camera>();
                    Vector3 fwd = cam != null ? cam.transform.forward : Vector3.forward;
                    fwd.y = 0f; fwd.Normalize();
                    if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                    Vector3 rgt = Vector3.Cross(Vector3.up, fwd).normalized;
                    Vector3 eye = feet + Vector3.up * 1.7f;
                    Func<Vector3, Vector3, PreviewShot> W = (a, b) => new PreviewShot { Pos = a, Look = b };
                    // the same two framings under the debug sun, so the albedo/grain can be compared
                    // with the district's own lit shots (which all use Lamp = true)
                    Func<Vector3, Vector3, PreviewShot> WL = (a, b) => new PreviewShot
                    { Pos = a, Look = b, Lamp = true };
                    l.Add(W(eye, eye + fwd * 25f));
                    l.Add(W(eye - rgt * 12f, eye - rgt * 12f + fwd * 18f - rgt * 8f));
                    l.Add(W(eye + rgt * 12f, eye + rgt * 12f + fwd * 18f + rgt * 8f));
                    l.Add(W(eye, feet + fwd * 4f));       // the ground at the spawn
                    l.Add(W(feet + Vector3.up * 45f - fwd * 60f, feet + Vector3.up * 5f + fwd * 30f));
                    l.Add(W(feet + Vector3.up * 12f + rgt * 10f - fwd * 6f, feet + Vector3.up * 4f));
                    l.Add(WL(eye, eye + fwd * 25f));
                    l.Add(WL(eye, feet + fwd * 4f));
                }
            }
            catch (System.Exception swallowed) { CoopDiag.Swallowed(swallowed); }
            // v0.9.73: the bas-relief on the outward wall of the east-flank block (plan points 5/8)
            try
            {
                var rb = reliefBlock;
                if (rb != null)
                {
                    var rr = Quaternion.Euler(0f, rb.yaw, 0f);
                    float yy = rb.baseY + (rb.Top - rb.baseY) * 0.55f;
                    Vector3 wall = rb.p + rr * new Vector3(0f, yy, rb.d * 0.5f);
                    l.Add(PL(rb.p + rr * new Vector3(0f, yy, rb.d * 0.5f + 115f), wall));
                    l.Add(PL(rb.p + rr * new Vector3(0f, yy, rb.d * 0.5f + 48f), wall));
                    l.Add(PL(rb.p + rr * new Vector3(0f, rb.baseY + 16f, rb.d * 0.5f + 38f),
                             rb.p + rr * new Vector3(0f, rb.baseY + 22f, rb.d * 0.5f)));
                }
            }
            catch (System.Exception swallowed) { CoopDiag.Swallowed(swallowed); }
            // v0.9.82: the Mi-26's engine deck up close вЂ” the original model's cowling, its two
            // intakes and the gearbox fairing adapted onto the new hull. Appended last so the
            // existing shot numbers stay put (the nose points along +x of the roof's own frame).
            if (heliBlock != null)
            {
                var hr = Quaternion.Euler(0f, HeliYaw, 0f);
                Vector3 hp = HeliSpot;
                l.Add(PL(hp + hr * new Vector3(31f, 9.5f, 12f), hp + hr * new Vector3(17.5f, 6.2f, 0f)));
                l.Add(PL(hp + hr * new Vector3(19.5f, 8.5f, -11.5f), hp + hr * new Vector3(17.5f, 6.2f, 0f)));
                l.Add(PL(hp + hr * new Vector3(17.5f, 24f, 8f), hp + hr * new Vector3(17.5f, 6.0f, 0f)));
                // v0.9.83: an ORTHOGRAPHIC side elevation at the reference's own scale вЂ”
                // the only honest way to compare the silhouette with the 3-view drawing
                // (a close perspective shot magnifies the near end by ~30 %).
                var op = PL(hp + hr * new Vector3(0f, 6f, 26f), hp + hr * new Vector3(0f, 6f, 0f));
                op.Ortho = 34f;
                l.Add(op);
                // v0.9.93: straight at the cargo hatch from astern (the nose points +x local)
                l.Add(PL(hp + hr * new Vector3(0f, 10f, -62f), hp + hr * new Vector3(0f, 6.5f, -12f)));
                l.Add(PL(hp + hr * new Vector3(18f, 12f, -50f), hp + hr * new Vector3(0f, 6.0f, -10f)));
                // v0.9.101: from INSIDE the cargo bay, looking aft — the proof that the lining
                // is there and the interior is not see-through
                l.Add(PL(hp + hr * new Vector3(-8f, 6.0f, 0f), hp + hr * new Vector3(-22f, 5.4f, 0f)));
                l.Add(PL(hp + hr * new Vector3(-8f, 6.0f, 0f), hp + hr * new Vector3(6f, 5.4f, 0f)));
                if (heliInA != Vector3.zero)
                {
                    l.Add(new PreviewShot { Pos = heliInA, Look = heliInB, Lamp = true, RampState = 1 });
                    l.Add(new PreviewShot { Pos = heliInB, Look = heliInA, Lamp = true });
                    // the user's angle: inside the bay, close to the closed hatch
                    l.Add(new PreviewShot { Pos = HM(new Vector3(0f, 6.1f, -2.6f)), Look = HM(new Vector3(0f, 4.2f, 13f)), Lamp = true, RampState = 1 });
                    l.Add(new PreviewShot { Pos = heliLowA, Look = heliLowB, Lamp = true, RampState = 1 });
                    l.Add(new PreviewShot { Pos = heliSideA, Look = heliSideB, Lamp = true });
                    l.Add(new PreviewShot { Pos = heliCabA, Look = heliCabB, Lamp = true });
                    l.Add(new PreviewShot { Pos = HM(new Vector3(-1.9f, 9.0f, -35.0f)), Look = HM(new Vector3(0f, 9.2f, -38.2f)), Lamp = true });
                    l.Add(new PreviewShot { Pos = HM(new Vector3(0f, 10.6f, -33.0f)), Look = HM(new Vector3(0f, 8.2f, -37.2f)), Lamp = true });
                    l.Add(new PreviewShot { Pos = HM(new Vector3(1.6f, 9.6f, -35.4f)), Look = HM(new Vector3(4.6f, 9.2f, -36.6f)), Lamp = true });
                    // v0.9.112: the same camera with the ramp raised, half way and lowered
                    l.Add(new PreviewShot { Pos = heliRampA, Look = heliRampB, Lamp = true, RampState = 1 });
                    l.Add(new PreviewShot { Pos = heliEdgeA, Look = heliEdgeB, Lamp = true, RampState = 1 });
                    l.Add(new PreviewShot { Pos = HM(new Vector3(0f, 8.5f, 10f)), Look = HM(new Vector3(0f, 8.0f, -18f)), Lamp = true, RampState = 1 });
                    l.Add(new PreviewShot { Pos = HM(new Vector3(0f, 8.5f, -6f)), Look = HM(new Vector3(-5.5f, 7.5f, -6f)), Lamp = true, RampState = 1 });
                    l.Add(new PreviewShot { Pos = HM(new Vector3(0f, 10.0f, 2f)), Look = HM(new Vector3(0f, 6.0f, 12f)), Lamp = true, RampState = 1 });
                    l.Add(new PreviewShot { Pos = heliRampA, Look = heliRampB, Lamp = true, RampState = 2 });
                    l.Add(new PreviewShot { Pos = heliRampA, Look = heliRampB, Lamp = true, RampState = 3 });
                }
            }
            // v0.10.8: the six patterned air-hockey tables on the annex floor below the playable one.
            // Appended at the end so every existing district_N shot keeps its number.
            {
                var fh2 = new Vector3(230f, PlinthH, 84f);
                var fr2 = Quaternion.Euler(0f, 4f, 0f);
                Func<float, float, float, Vector3> HL2 = (x, y, z) => fh2 + fr2 * new Vector3(x, y, z);
                // the whole showroom from the south-east corner, under the ceiling, both rows visible
                l.Add(PL(HL2(-11.4f, 101.4f, -13.6f), HL2(-30.5f, 96.9f, 2.0f)));
                // plan of the 3 x 2 grid, under the ceiling so the floor below is visible
                l.Add(new PreviewShot
                {
                    Pos = root.TransformPoint(HL2(-30.5f, 101.8f, 1f)),
                    Look = root.TransformPoint(HL2(-30.5f, 96f, 1f)),
                    Ortho = 17f,
                    Lamp = true
                });
                // variant 1 from the south-west: patterned body sides + magenta canopy
                l.Add(PL(HL2(-49.2f, 97.7f, -11.0f), HL2(-44f, 97.0f, -4.5f)));
                // variant 6 from the north-east: cyan canopy and LED ring
                l.Add(PL(HL2(-11.6f, 97.8f, 13.6f), HL2(-17f, 97.0f, 6.5f)));
                // the arch/lamp of variant 1 up close
                l.Add(PL(HL2(-44f, 99.0f, -10.0f), HL2(-44f, 98.2f, -4.5f)));
            }
            // v0.10.10: the four patterned tables one floor further down (89.75), north strip.
            // Two of them carry the neural-slop cartoon animal designs (08 cats, 10 frog).
            {
                var fh2 = new Vector3(230f, PlinthH, 84f);
                var fr2 = Quaternion.Euler(0f, 4f, 0f);
                Func<float, float, float, Vector3> HL2 = (x, y, z) => fh2 + fr2 * new Vector3(x, y, z);
                // the row from the north-west corner, under the 96.15 ceiling
                l.Add(PL(HL2(-49.0f, 95.5f, -13.2f), HL2(-30.5f, 90.6f, -10.8f)));
                // plan of the four-table row
                l.Add(new PreviewShot
                {
                    Pos = root.TransformPoint(HL2(-30.5f, 95.8f, -11f)),
                    Look = root.TransformPoint(HL2(-30.5f, 89.75f, -11f)),
                    Ortho = 18f,
                    Lamp = true
                });
                // table 08 (cartoon cats) from the north-west, offset so the arch rod does not block
                l.Add(PL(HL2(-38.5f, 91.9f, -13.2f), HL2(-35f, 90.8f, -11f)));
                // table 10 (cartoon frog) from the north-west
                l.Add(PL(HL2(-20.5f, 91.9f, -13.2f), HL2(-17f, 90.8f, -11f)));
                // low side angle on table 08: the animal/lettering skin flush with the body
                l.Add(PL(HL2(-38.0f, 90.95f, -13.3f), HL2(-35f, 90.45f, -11f)));
                // flat side elevations of the two animal tables (the arch rod is a thin vertical line)
                l.Add(new PreviewShot
                {
                    Pos = root.TransformPoint(HL2(-35f, 90.65f, -13.8f)),
                    Look = root.TransformPoint(HL2(-35f, 90.65f, -11f)),
                    Ortho = 3.2f,
                    Lamp = true
                });
                l.Add(new PreviewShot
                {
                    Pos = root.TransformPoint(HL2(-17f, 90.65f, -13.8f)),
                    Look = root.TransformPoint(HL2(-17f, 90.65f, -11f)),
                    Ortho = 3.2f,
                    Lamp = true
                });
                // v0.10.12: the raised, theme-coloured bumper on variant 1 (5 cm above the table)
                l.Add(PL(HL2(-48.5f, 97.9f, 10.8f), HL2(-44f, 97.2f, 6.5f)));
            }
            // v0.10.17: the five arcade cabinets along the top hall's back wall
            {
                var fh3 = new Vector3(230f, PlinthH, 84f);
                var fr3 = Quaternion.Euler(0f, 4f, 0f);
                Func<float, float, float, Vector3> HC =
                    (x, y, z) => fh3 + fr3 * new Vector3(x, y, z);
                l.Add(PL(HC(-39.5f, 105.90f, -4.6f), HC(-30.5f, 104.10f, -12.50f)));
                l.Add(PL(HC(-30.5f, 105.60f, -7.0f), HC(-30.5f, 104.00f, -12.50f)));
                // v0.10.36: the six new cabinets on the left half of the row
                l.Add(PL(HC(-47.5f, 105.90f, -4.2f), HC(-39.5f, 104.10f, -12.50f)));
                l.Add(PL(HC(-42.0f, 105.70f, -6.0f), HC(-39.5f, 104.00f, -12.50f)));
                // the whole eleven-cabinet row from the middle of the hall
                l.Add(PL(HC(-36.5f, 106.60f, 3.0f), HC(-36.5f, 104.10f, -12.50f)));
                // v0.10.42: the three duel cabinets at the opposite wall (front faces -z)
                l.Add(PL(HC(-36.5f, 105.90f, 14.2f), HC(-36.5f, 104.20f, 18.50f)));
                l.Add(PL(HC(-40.5f, 105.70f, 14.0f), HC(-40.5f, 104.10f, 18.50f)));
                l.Add(PL(HC(-36.5f, 105.80f, 8.0f), HC(-36.5f, 104.30f, 18.50f)));
            }
            return l;
        }

        /// <summary>
        /// v0.9.10 grain calibration (map-shot mode only): the district's surface and the dam's own
        /// concrete shot with the same ruler, so the grain can be *measured* instead of guessed.
        ///
        /// `grain_dam` / `grain_ours` look straight down from 2.4 m (60В° FOV, 1024 px) at the two
        /// sides of the seam вЂ” 2.7 mm per pixel on both, so the aggregate pits can be counted and
        /// sized in cm. `grain_seam` is the user's own framing at eye level: the joint between the
        /// old crest and the platform.
        /// </summary>
        public static List<PreviewShot> GrainShots()
        {
            var l = new List<PreviewShot>();
            if (root == null) return l;
            Func<Vector3, Vector3, string, PreviewShot> P = (a, b, n) => new PreviewShot
            {
                Pos = root.TransformPoint(a),
                Look = root.TransformPoint(b),
                Name = n
            };
            // the old dam crest is inland (local z < 0), the platform is seaward (z > 0)
            // where is what: 60 x 60 m straight down, 1024 px = 5.9 cm/px
            l.Add(new PreviewShot
            {
                Pos = root.TransformPoint(new Vector3(205f, 60f, 0f)),
                Look = root.TransformPoint(new Vector3(205f, 0f, -0.01f)),
                Name = "grain_wide",
                Ortho = 30f
            });
            l.Add(P(new Vector3(205f, 2.4f, -8f), new Vector3(205f, -1f, -8f), "grain_dam"));
            l.Add(P(new Vector3(205f, 2.4f, -12f), new Vector3(205f, -1f, -12f), "grain_dam2"));
            l.Add(P(new Vector3(205f, 2.4f, 3f), new Vector3(205f, -1f, 3f), "grain_ours"));
            l.Add(P(new Vector3(212f, 1.3f, 4f), new Vector3(194f, 0.2f, -1.5f), "grain_seam"));
            return l;
        }

        /// <summary>Plan camera: straight down, wide enough for the platform and the western tongue.</summary>
        public static void TopView(out Vector3 pos, out Quaternion rot, out float ortho)
        {
            pos = root != null ? root.TransformPoint(new Vector3(165f, 0f, 150f)) + Vector3.up * 300f
                               : new Vector3(0f, 400f, 0f);
            // aligned with the deck, so the plan reads like the reference drawing (seaward = up)
            rot = (root != null ? root.rotation : Quaternion.identity) * Quaternion.Euler(90f, 0f, 0f);
            ortho = 245f;
        }

        public static void SetShadows(bool on)
        {
            if (root == null) return;
            var mode = on ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = mode;
        }

        /// <summary>
        /// Physics probes for the one thing a still cannot show: whether the player would actually
        /// stand on these floors. Casts straight down at a few places and logs what it hit.
        /// </summary>
        /// <summary>
        /// Layout evidence (map-shot mode): the exact footprints and the bridges, so the plan can be
        /// drawn offline in the colours of the reference drawing (beige deck, green houses, red bridges,
        /// purple House) instead of guessing from a top-down render where every roof is the same grey.
        /// </summary>
        private static void DumpPlan()
        {
            try
            {
                string asm = typeof(SovietDistrict).Assembly.Location;
                string dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(asm) ?? "", "BabbdiCoop", "map");
                System.IO.Directory.CreateDirectory(dir);
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"# platform;{Len:F1};{Wid:F1};{Axw:F1};{Az0:F1};{ShiftX:F1}");
                for (int i = 0; i < tiers.Length; i++)     // v0.9.9: the terraced floors
                {
                    var t = tiers[i];
                    sb.AppendLine($"t;{t.x0:F1};{t.x1:F1};{t.z0:F1};{t.z1:F1};{t.h:F1};{t.name}");
                }
                for (int i = 0; i < ramps.Length; i++)     // the inclined roads (yellow)
                {
                    var l = ramps[i];
                    sb.AppendLine($"m;{l.x0:F1};{l.z0:F1};{l.x1:F1};{l.z1:F1};{l.w:F1};{l.y0:F1};{l.y1:F1}");
                }
                for (int i = 0; i < stairs.Length; i++)    // the street stairs (green)
                {
                    var l = stairs[i];
                    sb.AppendLine($"s;{l.x0:F1};{l.z0:F1};{l.x1:F1};{l.z1:F1};{l.w:F1};{l.y0:F1};{l.y1:F1}");
                }
                for (int i = 0; i < bldgs.Count; i++)
                {
                    var b = bldgs[i];
                    sb.AppendLine($"b;{b.p.x:F1};{b.p.z:F1};{b.w:F1};{b.d:F1};{b.yaw:F1};{b.floors}");
                }
                for (int i = 0; i < bridges.Count; i++)
                {
                    var s = bridges[i];
                    sb.AppendLine($"r;{s.a.x:F1};{s.a.z:F1};{s.b.x:F1};{s.b.z:F1};{s.y:F1}");
                }
                sb.AppendLine("p;230.0;84.0;107.0;49.0;4.0");    // House of Soviets
                sb.AppendLine($"q;232.5;85.0;135.0;80.0;{PlinthH:F1}");    // its esplanade (the +10 m plinth)
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "plan_dump.csv"), sb.ToString());
                log.LogInfo($"[DISTRICT] plan dump: {bldgs.Count} blocks, {bridges.Count} bridges, " +
                            $"{tiers.Length} terraces, {ramps.Length} ramps, {stairs.Length} stairs -> plan_dump.csv");
            }
            catch (Exception e)
            {
                log.LogWarning($"[DISTRICT] plan dump failed: {e.Message}");
            }
        }
    }
}
