using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace BabbdiCoop
{
    // Part of the SovietDistrict builder - see SovietDistrict.cs.
    internal static partial class SovietDistrict
    {

        // ------------------------------------------------------------------ House of Soviets (Kaliningrad)

        /// <summary>
        /// v0.9.60 вЂ” the House of Soviets rebuilt **to the front reference**. The real silhouette is an
        /// "H": two separate slab-towers with a wide **open gap** between them, joined by a two-storey
        /// **bridge** at ~55 % of the height, standing on a low two-storey stylobate whose front carries
        /// the long ribbon of windows and the entrance. v0.9.59 filled that gap with a loggia volume,
        /// which is why the contour did not match at all.
        /// Scale: the real house is a 13-storey, ~72-80 m building; here it is 13 storeys of 6,4 m =
        /// 83,4 m over the plinth (the rear blocks 89,8 m, the mast 110 m) вЂ” the reference's size В±15 %,
        /// not the old 218 m skyscraper. Plan 107,3 x 49,3 m, unchanged.
        /// Walkability: the stylobate's two-storey hall with its colonnade and the towers' entrances;
        /// 13 floors and a stairwell in each tower; the bridge connects them on floors 7-8 (doorways cut
        /// in the inner end walls, windows suppressed on exactly those two floors), so you can walk
        /// across the gap. No window looks into a wall: the towers' rear faces are solid (the rear
        /// blocks stand against them) and their inner faces are solid only where the bridge lands.
        /// </summary>
        private static void HouseOfSoviets(Vector3 p, float yaw)
        {
            var house = new GameObject("house_of_soviets");
            house.transform.SetParent(root, false);
            house.transform.localPosition = p;
            house.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var rot = Quaternion.Euler(0f, yaw, 0f);

            const float K = 1.45f;
            const float pw = 74f * K, pd = 34f * K;      // 107,3 x 49,3
            const float pfh = FH * 2f;                    // 6,4 m
            const float gTop = BASE + pfh;                // 6,55 вЂ” the overhang's slab
            float ptop = BASE + 2f * pfh;                 // 12,95
            float hw = pw * 0.5f, hd = pd * 0.5f;
            float tbase = BASE + p.y;
            var tp = p; tp.y = 0f;
            // the ground floor is **recessed 4 m** and carried on round columns (the photos), so the
            // podium's upper storey overhangs it
            const float inset = 4f;
            float iw = hw - inset, id = hd - inset;

            // towers: an 8-floor lower section (6 of them above the podium) and a 6-floor upper one set
            // 3 m back at the front, with glazed bays hanging over the break
            const float tw = 30f, td = 26f;
            const float tcx = 30.5f, tcz = 2f;
            const int lfl = 8, ufl = 7;
            float lTop = BASE + lfl * pfh;                // 51,35
            float uTop = lTop + ufl * pfh;                // 89,75
            const float twu = 30f, tdu = 22f, tczu = 3f;
            float tx0 = tcx - tw * 0.5f, tx1 = tcx + tw * 0.5f;     // 15,5..45,5
            float tz0 = tcz - td * 0.5f, tz1 = tcz + td * 0.5f;     // -11..15
            float uz0 = tczu - tdu * 0.5f;                          // -8 вЂ” the upper's front
            // the gallery on the lower floors 6-7, the thin bridge on the upper floors 2-3
            float galY0 = BASE + 6f * pfh, galY1 = BASE + 8f * pfh; // 38,55..51,35
            float brY0 = lTop + 2f * pfh, brY1 = lTop + 4f * pfh;   // 64,15..76,95
            const float coreW = 12f, coreD = 16f, coreZ0 = -2f, coreTop = 84f;
            const float rtop = 95f;                       // the rear blocks

            // --- stylobate, two levels: the **recessed ground floor on round columns** (the photos) and
            //     the overhanging upper storey with the long ribbon of windows and the entrance portal
            var podGo = new GameObject("podium");
            podGo.transform.SetParent(house.transform, false);
            var g = new Geo();
            const float rw = 1.4f, rh2 = 1.6f, rbay = 2.0f;
            Wall(g, new Vector3(0f, 0f, id), Vector3.right, Vector3.forward, 2f * iw, BASE, gTop, 1, pfh,
                 rw, rh2, 3.0f, 0.12f, rbay, new List<Vector4>(), null, 1, 0f);
            Wall(g, new Vector3(0f, 0f, -id), Vector3.left, Vector3.back, 2f * iw, BASE, gTop, 1, pfh,
                 rw, rh2, 3.0f, 0.12f, rbay, new List<Vector4>
                 {
                     new Vector4(iw - 7f, BASE, iw + 7f, BASE + 6.2f)          // the main entrance
                 }, null, 1, 0f);
            Wall(g, new Vector3(iw, 0f, 0f), Vector3.forward, Vector3.right, 2f * id, BASE, gTop, 1, pfh,
                 rw, rh2, 3.0f, 0.12f, rbay, new List<Vector4>(), null, 1, 0f);
            Wall(g, new Vector3(-iw, 0f, 0f), Vector3.back, Vector3.left, 2f * id, BASE, gTop, 1, pfh,
                 rw, rh2, 3.0f, 0.12f, rbay, new List<Vector4>(), null, 1, 0f);
            // the overhang's slab ring and its **round columns** along the front edge
            Rect2(g, -hw, hw, -hd, -id, gTop - 0.4f, gTop);
            Rect2(g, -hw, hw, id, hd, gTop - 0.4f, gTop);
            Rect2(g, -hw, -iw, -id, id, gTop - 0.4f, gTop);
            Rect2(g, iw, hw, -id, id, gTop - 0.4f, gTop);
            for (int i = -3; i <= 3; i++)
            {
                if (i == 0) continue;                 // v0.9.63: no column in the **entrance axis**
                Cylinder(g, i * 10f, -hd + 1.4f, 1.6f, BASE, gTop - 0.4f, 12);
            }
            // v0.9.63: a **monumental portal** instead of that column вЂ” four times the pedestrian scale:
            // two massive jambs, a deep lintel, a tipped canopy on two heavy struts and a raised
            // threshold slab inside the recess, all raw concrete
            g.Box(0, new Vector3(-9.2f, BASE + 4.7f, -id - 1.6f), new Vector3(4.4f, 9.4f, 3.2f), Quaternion.identity, 0.4f);
            g.Box(0, new Vector3(9.2f, BASE + 4.7f, -id - 1.6f), new Vector3(4.4f, 9.4f, 3.2f), Quaternion.identity, 0.4f);
            g.Box(0, new Vector3(0f, BASE + 7.6f, -id - 1.6f), new Vector3(22.8f, 3.0f, 3.2f), Quaternion.identity, 0.4f);
            // v0.9.68: the "monumental threshold" used to be **0,7 m tall** вЂ” twice the controller's
            // stepOffset, so the entrance could not be entered at all (the user's blocked passage).
            // It is a 0,14 m sill now: still reads as a threshold, and is simply walked over.
            g.Box(0, new Vector3(0f, BASE + 0.07f, -id + 2.6f), new Vector3(18f, 0.14f, 7.2f), Quaternion.identity, 0.5f);
            g.Box(0, new Vector3(0f, gTop + 4.4f, -id - 5.4f), new Vector3(30f, 1.6f, 9.5f), Quaternion.Euler(-7f, 0f, 0f), 0.4f);
            g.Box(0, new Vector3(-13f, BASE + 5.4f, -id - 8.4f), new Vector3(1.5f, 10.8f, 1.5f), Quaternion.identity, 0.4f);
            g.Box(0, new Vector3(13f, BASE + 5.4f, -id - 8.4f), new Vector3(1.5f, 10.8f, 1.5f), Quaternion.identity, 0.4f);
            // the upper storey at the full footprint: the ribbon on the sides and rear, and the wide
            // **loggia band** across the front (the photos' deep openings over the entrance)
            Wall(g, new Vector3(0f, 0f, hd), Vector3.right, Vector3.forward, pw, gTop, ptop, 1, pfh,
                 rw, rh2, 2.4f, 0.12f, rbay, new List<Vector4>(), null, 1, 0f);
            Wall(g, new Vector3(hw, 0f, 0f), Vector3.forward, Vector3.right, pd, gTop, ptop, 1, pfh,
                 rw, rh2, 2.4f, 0.12f, rbay, new List<Vector4>(), null, 1, 0f);
            Wall(g, new Vector3(-hw, 0f, 0f), Vector3.back, Vector3.left, pd, gTop, ptop, 1, pfh,
                 rw, rh2, 2.4f, 0.12f, rbay, new List<Vector4>(), null, 1, 0f);
            Wall(g, new Vector3(0f, 0f, -hd), Vector3.left, Vector3.back, pw, gTop, ptop, 1, pfh,
                 3.2f, 2.4f, 2.2f, 0.2f, 4.4f, new List<Vector4>(), null, 1, 0f);
            // the entrance canopy, projecting from the overhang
            g.Box(0, new Vector3(0f, gTop + 1.6f, -hd - 2.2f), new Vector3(22f, 0.9f, 4.8f), Quaternion.identity, 0.5f);

            // hall floor: the ring around the two tower footprints inside the **recessed** footprint
            Rect2(g, -iw, iw, -id, tz0, BASE - SLAB, BASE);
            Rect2(g, -iw, -tx1, tz0, tz1, BASE - SLAB, BASE);
            Rect2(g, tx1, iw, tz0, tz1, BASE - SLAB, BASE);
            Rect2(g, -tx0, tx0, tz0, tz1, BASE - SLAB, BASE);
            Rect2(g, -iw, iw, tz1, id, BASE - SLAB, BASE);
            // stylobate roof: the ring around the towers at the full footprint
            float ry = ptop;
            Rect2(g, -hw, hw, -hd, tz0, ry - SLAB, ry);
            Rect2(g, -hw, -tx1, tz0, tz1, ry - SLAB, ry);
            Rect2(g, tx1, hw, tz0, tz1, ry - SLAB, ry);
            Rect2(g, -tx0, tx0, tz0, tz1, ry - SLAB, ry);
            Rect2(g, -hw, hw, tz1, hd, ry - SLAB, ry);
            // colonnade in the hall's rear strip (the only bay wide enough for it). v0.9.67: no column on
            // the hall's axis вЂ” the central passage is flanked by a **pair** instead of blocked by one
            {
                float cz = (tz1 + hd - T) * 0.5f;
                for (int i = -4; i <= 4; i++)
                {
                    if (i == 0) continue;
                    g.Box(0, new Vector3(i * 11f, (BASE + ry) * 0.5f, cz),
                          new Vector3(2.4f, ry - BASE, 2.4f), Quaternion.identity, 0.6f);
                }
                for (int sd = -1; sd <= 1; sd += 2)
                    g.Box(0, new Vector3(sd * 4.5f, (BASE + ry) * 0.5f, cz),
                          new Vector3(2.4f, ry - BASE, 2.4f), Quaternion.identity, 0.6f);
            }
            g.Apply(podGo, mRough);
            AddMeshCollider(podGo, "podium");

            // --- the two towers, each **two sections**: an 8-floor lower block (6 floors above the
            //     stylobate, its first two a blank band) and a 6-floor upper block **set 3 m back at
            //     the front**, with glazed bays hanging over the break and a cornice on the break line
            const float twW = 2.4f, twH = 2.4f;
            int nuMask = ~((1 << 0) | (3 << 2));           // upper: door on 0, the bridge on floors 2-3
            int loMask = ~(3 << 6);                        // lower: the gallery on floors 6-7
            float upperBaseY = lTop + PlinthH;             // 61,35 вЂ” the upper's ground floor
            var bandGo = new GameObject("house_bands");
            bandGo.transform.SetParent(house.transform, false);
            var bg2 = new Geo();
            for (int s = -1; s <= 1; s += 2)
            {
                // ---- lower section: 30 x 26, eight floors, a blank two-storey base band
                var endsLo = new bool[lfl];
                endsLo[6] = endsLo[7] = true;              // gallery doorways
                BuildBuilding(new Bldg
                {
                    p = tp + rot * new Vector3(s * tcx, 0f, tcz),
                    yaw = yaw,
                    baseY = tbase,
                    w = tw,
                    d = td,
                    floors = lfl,
                    fh = pfh,
                    dh = 1.3f,
                    wall = mRough,
                    winW = 2.88f,
                    winH = 2.88f,
                    sill = 1.1f,
                    splay = 0.25f,
                    bay = 3.2f,
                    winSkip = 1,
                    winPad = 0f,
                    // below the stylobate's roof the tower stands inside the hall; the two floors above
                    // that roof are the blank base band of the reference
                    winFromFloor = 4,
                    winPlusZ = false,                      // the rear blocks stand against the rear faces
                    winMaskPlusX = s < 0 ? loMask : -1,
                    winMaskMinusX = s > 0 ? loMask : -1,
                    doorPlusX = s < 0,
                    doorMinusX = s > 0,
                    entranceW = 3.4f, entranceH = 4.4f,   // v0.9.69: a real portal, not a 1,7 m slit
                    towerDetail = true,                   // v0.9.72: courses, sills, pier ribs
                    endPlus = s < 0 ? endsLo : null,
                    endMinus = s > 0 ? endsLo : null,
                    corrMul = 3,           // v0.9.63: the corridor is three times wider (the rooms pay)
                    plinth = false,
                    interior = true,
                });

                // ---- the base band, 0,7 m proud of the shaft for two storeys, and its cornice
                {
                    float by = (12.95f + 25.75f) * 0.5f;
                    bg2.Box(0, new Vector3(s * tcx, by, tz0 - 0.35f), new Vector3(tw + 1.4f, 12.8f, 0.7f), Quaternion.identity, 0.5f);
                    bg2.Box(0, new Vector3(s * tcx, by, tz1 + 0.35f), new Vector3(tw + 1.4f, 12.8f, 0.7f), Quaternion.identity, 0.5f);
                    bg2.Box(0, new Vector3(s * tcx + s * (tw * 0.5f + 0.35f), by, tcz), new Vector3(0.7f, 12.8f, td), Quaternion.identity, 0.5f);
                    bg2.Box(0, new Vector3(s * tcx - s * (tw * 0.5f + 0.35f), by, tcz), new Vector3(0.7f, 12.8f, td), Quaternion.identity, 0.5f);
                    float cy = 25.75f + 0.175f;
                    bg2.Box(0, new Vector3(s * tcx, cy, tz0 - 0.45f), new Vector3(tw + 1.8f, 0.35f, 0.9f), Quaternion.identity, 0.5f);
                    bg2.Box(0, new Vector3(s * tcx, cy, tz1 + 0.45f), new Vector3(tw + 1.8f, 0.35f, 0.9f), Quaternion.identity, 0.5f);
                    bg2.Box(0, new Vector3(s * tcx + s * (tw * 0.5f + 0.45f), cy, tcz), new Vector3(0.9f, 0.35f, td + 1.8f), Quaternion.identity, 0.5f);
                    bg2.Box(0, new Vector3(s * tcx - s * (tw * 0.5f + 0.45f), cy, tcz), new Vector3(0.9f, 0.35f, td + 1.8f), Quaternion.identity, 0.5f);
                }

                // ---- upper section: 27 x 20, six floors, set 1,5 m back in x and 6 m at the rear, on the
                //      lower block's roof terrace; its ground-floor door opens onto that terrace
                var endsUp = new bool[ufl];
                endsUp[2] = endsUp[3] = true;              // the thin bridge's doorways
                var topDoor = new bool[ufl];
                topDoor[6] = true;                         // v0.9.66: the door into the annex
                BuildBuilding(new Bldg
                {
                    p = tp + rot * new Vector3(s * tcx, 0f, tczu),
                    yaw = yaw,
                    baseY = upperBaseY,
                    w = tw,
                    d = tdu,
                    floors = ufl,
                    fh = pfh,
                    dh = 1.3f,
                    wall = mRough,
                    winW = 2.88f,
                    winH = 2.88f,
                    sill = 1.1f,
                    splay = 0.25f,
                    bay = 3.2f,
                    winSkip = 1,
                    winPad = 0f,
                    winPlusZ = false,                      // the rear blocks stand against the rear faces
                    // v0.9.63: the glazed bays hang in front of these two floors вЂ” no windows behind them
                    winMaskMinusZ = ~(3 << 0),
                    winMaskPlusX = s < 0 ? nuMask : -1,
                    winMaskMinusX = s > 0 ? nuMask : -1,
                    doorPlusX = s < 0,
                    doorMinusX = s > 0,
                    entranceW = 3.4f, entranceH = 4.4f,   // v0.9.69: the same portal upstairs
                    towerDetail = true,
                    endPlus = s < 0 ? endsUp : null,
                    endMinus = s < 0 ? topDoor : endsUp,
                    corrMul = 3,           // v0.9.63: the corridor is three times wider (the rooms pay)
                    plinth = false,
                    interior = true,
                });

                // ---- the two bays hanging over the break on the upper's front face (v0.9.66: no glass вЂ”
                //      they are plain concrete volumes now)
                for (int b = -1; b <= 1; b += 2)
                {
                    float bx = s * tcx + b * 8.5f;
                    bg2.Box(0, new Vector3(bx, lTop + 6.4f, (uz0 + tz0) * 0.5f),
                            new Vector3(6f, 12.8f, uz0 - tz0), Quaternion.identity, 0.4f);
                }
                // ---- the cornice on the break line
                {
                    float byq = lTop + 0.2f;
                    bg2.Box(0, new Vector3(s * tcx, byq, tz0 - 0.45f), new Vector3(tw + 1.8f, 0.4f, 0.9f), Quaternion.identity, 0.5f);
                    bg2.Box(0, new Vector3(s * tcx, byq, tz1 + 0.45f), new Vector3(tw + 1.8f, 0.4f, 0.9f), Quaternion.identity, 0.5f);
                    bg2.Box(0, new Vector3(s * tcx + s * (tw * 0.5f + 0.45f), byq, tcz), new Vector3(0.9f, 0.4f, td + 1.8f), Quaternion.identity, 0.5f);
                    bg2.Box(0, new Vector3(s * tcx - s * (tw * 0.5f + 0.45f), byq, tcz), new Vector3(0.9f, 0.4f, td + 1.8f), Quaternion.identity, 0.5f);
                }
            }
            bg2.Apply(bandGo, mRough, mDark);
            AddMeshCollider(bandGo, "house_bands");

            // --- the **gallery** on the lower blocks' floors 6-7 (wide, 18 m deep, its roof is the
            //     terrace the upper blocks' ground doors open onto) and the **thin bridge** on the upper
            //     blocks' floors 2-3, as in the frontal reference: one wide band low, one narrow higher
            Bridge(house, "house_gallery", -tx0, tx0, -8f, 10f, galY0, galY1);
            Bridge(house, "house_bridge", -tx0, tx0, -6f, 4f, brY0, brY1,
                   new List<Vector4> { new Vector4(16.5f, brY0, 19.5f, brY0 + 4.2f) });   // the stair arrives here

            // --- the **narrow core** between the towers, set back from the front: the stair/lift shaft
            //     of the reference. v0.9.63: it is **fused** with the two links вЂ” doorways on the gallery
            //     and bridge floors on all four walls, a floor slab per storey, and an external concrete
            //     stair on its front from the gallery's roof up to the bridge.
            var coreGo = new GameObject("house_core");
            coreGo.transform.SetParent(house.transform, false);
            var cg = new Geo();
            float cFh = (coreTop - ptop) / 11f;
            int coreMask = ~((3 << 4) | (3 << 8));        // windows off where the links pass through
            float dA0 = ptop + 4f * cFh + 0.2f, dA1 = dA0 + 5.5f;   // the gallery doorway
            float dB0 = ptop + 8f * cFh + 0.2f, dB1 = dB0 + 5.5f;   // the bridge doorway
            for (int f = 0; f < 11; f++)
                Rect2(cg, -coreW * 0.5f, coreW * 0.5f, coreZ0, coreZ0 + coreD,
                      ptop + f * cFh - SLAB, ptop + f * cFh);
            Wall(cg, new Vector3(0f, 0f, coreZ0 + coreD), Vector3.right, Vector3.forward, coreW, ptop, coreTop, 11,
                 cFh, 1.6f, 2.2f, 1.4f, 0.18f, 2.4f, new List<Vector4>
                 {
                     new Vector4(coreW * 0.5f - 2.5f, dA0, coreW * 0.5f + 2.5f, dA1),
                     new Vector4(coreW * 0.5f - 2.5f, dB0, coreW * 0.5f + 2.5f, dB1)
                 }, null, 1, 0f, true, 0, coreMask);
            Wall(cg, new Vector3(0f, 0f, coreZ0), Vector3.left, Vector3.back, coreW, ptop, coreTop, 11,
                 cFh, 1.6f, 2.2f, 1.4f, 0.18f, 2.4f, new List<Vector4>
                 {
                     new Vector4(coreW * 0.5f - 2.5f, dA0, coreW * 0.5f + 2.5f, dA1),
                     new Vector4(coreW * 0.5f - 2.5f, dB0, coreW * 0.5f + 2.5f, dB1)
                 }, null, 1, 0f, true, 0, coreMask);
            Wall(cg, new Vector3(coreW * 0.5f, 0f, coreZ0 + coreD * 0.5f), Vector3.forward, Vector3.right, coreD,
                 ptop, coreTop, 11, cFh, 1.6f, 2.2f, 1.4f, 0.18f, 2.4f, new List<Vector4>
                 {
                     new Vector4(coreD * 0.5f - 2.5f, dA0, coreD * 0.5f + 2.5f, dA1),
                     new Vector4(coreD * 0.5f - 2.5f, dB0, coreD * 0.5f + 2.5f, dB1)
                 }, null, 1, 0f, true, 0, coreMask);
            Wall(cg, new Vector3(-coreW * 0.5f, 0f, coreZ0 + coreD * 0.5f), Vector3.back, Vector3.left, coreD,
                 ptop, coreTop, 11, cFh, 1.6f, 2.2f, 1.4f, 0.18f, 2.4f, new List<Vector4>
                 {
                     new Vector4(coreD * 0.5f - 2.5f, dA0, coreD * 0.5f + 2.5f, dA1),
                     new Vector4(coreD * 0.5f - 2.5f, dB0, coreD * 0.5f + 2.5f, dB1)
                 }, null, 1, 0f, true, 0, coreMask);
            Rect2(cg, -coreW * 0.5f, coreW * 0.5f, coreZ0, coreZ0 + coreD, coreTop - 0.4f, coreTop);
            // v0.9.64: the external stair is a **three-flight switchback** вЂ” 27 steps of 0,158 m per
            // flight (the old single run was 0,30 m per step, which the controller can barely climb),
            // with landings, a sloped handrail and posts on every flight
            float fl = (brY0 - galY1) / 3f;                // 4,27 m per flight
            const float x0s = -11.6f, x1s = -3.5f;         // the run along x
            Flight(cg, new Vector3(x0s, galY1, -6.5f), Vector3.right, 27, fl, 3.0f);
            Rect2(cg, x1s, x1s + 2.2f, -8f, -2f, galY1 + fl - 0.3f, galY1 + fl);
            Flight(cg, new Vector3(x1s, galY1 + fl, -3.5f), Vector3.left, 27, fl, 3.0f);
            Rect2(cg, x0s - 2.2f, x0s, -8f, -2f, galY1 + 2f * fl - 0.3f, galY1 + 2f * fl);
            Flight(cg, new Vector3(x0s, galY1 + 2f * fl, -6.5f), Vector3.right, 27, fl, 3.0f);
            Rect2(cg, x1s, x1s + 2.2f, -8f, -5f, brY0 - 0.3f, brY0);
            {
                float runH = 27 * 0.30f;
                float diag = Mathf.Sqrt(runH * runH + fl * fl);
                for (int f = 0; f < 3; f++)
                {
                    float y0f = galY1 + f * fl;
                    bool east = (f % 2) == 0;
                    Vector3 d = east ? Vector3.right : Vector3.left;
                    var o = new Vector3(east ? x0s : x1s, y0f, east ? -8.05f : -2.05f);
                    var q = Quaternion.LookRotation((d + Vector3.up * (fl / runH)).normalized, Vector3.up);
                    cg.Box(0, o + d * (runH * 0.5f) + Vector3.up * (fl * 0.5f + 1.0f), new Vector3(0.1f, 0.1f, diag), q, 0.5f);
                    for (int k = 0; k <= 4; k++)
                        cg.Box(0, o + d * (runH * k / 4f) + Vector3.up * (fl * k / 4f + 0.5f),
                               new Vector3(0.08f, 1.0f, 0.08f), Quaternion.identity, 0.5f);
                }
            }
            cg.Apply(coreGo, mRough);
            AddMeshCollider(coreGo, "house_core");

            // --- the rear blocks: two 30 x 9,65 m slabs against the towers' backs, rising above the
            //     towers (the reference's taller grey volumes) and the antenna mast on the west one
            var rearGo = new GameObject("house_rear");
            rearGo.transform.SetParent(house.transform, false);
            var rg = new Geo();
            float rz0 = tz1, rz1 = hd;
            for (int s = -1; s <= 1; s += 2)
            {
                float x0 = s * tx0, x1 = s * tx1;
                float cx = (x0 + x1) * 0.5f;
                Wall(rg, new Vector3(cx, 0f, rz1), Vector3.right, Vector3.forward, tw, ptop, rtop, 12,
                     (rtop - ptop) / 12f, 2.0f, 2.0f, 1.4f, 0.2f, 3.0f, new List<Vector4>(), null, 1, 0f);
                Wall(rg, new Vector3(x1, 0f, (rz0 + rz1) * 0.5f), Vector3.forward, Vector3.right, rz1 - rz0,
                     ptop, rtop, 12, (rtop - ptop) / 12f, 2.0f, 2.0f, 1.4f, 0.2f, 3.0f, new List<Vector4>(), null, 1, 0f);
                Wall(rg, new Vector3(x0, 0f, (rz0 + rz1) * 0.5f), Vector3.back, Vector3.left, rz1 - rz0,
                     ptop, rtop, 12, (rtop - ptop) / 12f, 2.0f, 2.0f, 1.4f, 0.2f, 3.0f, new List<Vector4>(), null, 1, 0f);
                Wall(rg, new Vector3(cx, 0f, rz0), Vector3.left, Vector3.back, tw, ptop, rtop, 12,
                     (rtop - ptop) / 12f, 2.0f, 2.0f, 1.4f, 0.2f, 3.0f, new List<Vector4>(), null, 1, 0f, false);
                Rect2(rg, Mathf.Min(x0, x1), Mathf.Max(x0, x1), rz0, rz1, rtop - 0.5f, rtop);
            }
            // the gap must be open **through** the building (the reference shows sky/city through it),
            // so there is deliberately no central rear block between the two rear slabs
            // the antenna mast on the west rear block (the reference's)
            rg.Box(0, new Vector3(-tcx, rtop + 10f, rz1 - 4.65f), new Vector3(1.2f, 20f, 1.2f), Quaternion.identity, 0.4f);
            rg.Box(0, new Vector3(-tcx, rtop + 0.3f, rz1 - 4.65f), new Vector3(3.2f, 0.6f, 3.2f), Quaternion.identity, 0.4f);
            rg.Apply(rearGo, mRough);
            AddMeshCollider(rearGo, "house_rear");

            // --- v0.9.65: the **portholes** of reference 1 spread evenly over the plain bands, and the
            //     west tower flowing into a cantilevered **chevron annex on splayed piers** (reference 2)
            var detGo = new GameObject("house_details");
            detGo.transform.SetParent(house.transform, false);
            var dg = new Geo();
            for (int s2 = -1; s2 <= 1; s2 += 2)
            {
                for (int i = -2; i <= 2; i++)
                    Porthole(dg, new Vector3(s2 * tcx + i * 5.2f, 19.3f, -11f), Vector3.back, 1.05f);
                for (int i = -1; i <= 1; i++)
                    Porthole(dg, new Vector3(s2 * tx1, 19.3f, tcz + i * 7f),
                             s2 > 0 ? Vector3.right : Vector3.left, 1.05f);
                for (int i = -1; i <= 1; i++)
                    Porthole(dg, new Vector3(s2 * hw, 11.9f, i * 12f),
                             s2 > 0 ? Vector3.right : Vector3.left, 0.8f);
            }
            for (int i = -4; i <= 4; i++)
                Porthole(dg, new Vector3(i * 10f, 11.9f, -hd), Vector3.back, 0.8f);
            // v0.9.67: a pair of **brutalist gargoyles** on the entrance canopy's front corners
            // v0.9.69: they stand on the **upper portal canopy** вЂ” the big 30 x 9,5 m slab tipped -7В°
            // over the entrance (its top runs 11,2..12,3 m). A flat concrete pedestal is sunk into the
            // slope, so the 3,5 m statues sit level on real support instead of being cut by the slab
            for (int gs = -1; gs <= 1; gs += 2)
            {
                float gx = gs * 11.5f;
                dg.Box(0, new Vector3(gx, 11.3f, -28.3f), new Vector3(2.4f, 0.9f, 2.4f), Quaternion.identity, 0.4f);
                Gargoyle(dg, gx, 11.75f, -28.3f);
            }
            // the annex: eight **splayed piers** from the lower tower's roof up to itвЂ¦
            const float acx = -30.5f, acz = 3f;
            for (int px = -1; px <= 1; px += 2)
                for (int pz = -1; pz <= 1; pz += 2)
                    Frustum(dg, 51.6f, 89.55f,
                            new Vector2(acx + px * 17.5f, acz + pz * 13f),
                            new Vector2(acx + px * 21f, acz + pz * 16.5f),
                            2.6f, 2.2f, 3.8f, 3.2f, Vector2.right, Vector2.up);
            // вЂ¦ and its three **walkable storeys** (6,4 m headroom each, no glass) with a stair well
            float ax0 = acx - 20f, ax1 = acx + 20f, az0 = acz - 17f, az1 = acz + 17f;
            const float hx0 = -25f, hx1 = -10.5f, hz0 = 14f, hz1 = 20f;   // the stair well: the rear strip
            Rect2(dg, ax0, ax1, az0, -8f, 89.35f, 89.75f);                 // floor 0: a ring round the tower
            Rect2(dg, ax0, ax1, 14f, az1, 89.35f, 89.75f);
            Rect2(dg, ax0, -45.5f, -8f, 14f, 89.35f, 89.75f);
            Rect2(dg, -15.5f, ax1, -8f, 14f, 89.35f, 89.75f);
            for (int lvl = 0; lvl < 2; lvl++)
            {
                float y = 96.15f + lvl * 6.4f;
                Rect2(dg, ax0, hx0, az0, az1, y - 0.4f, y);                // floors 1-2 round the well
                Rect2(dg, hx1, ax1, az0, az1, y - 0.4f, y);
                Rect2(dg, hx0, hx1, az0, hz0, y - 0.4f, y);
                Rect2(dg, hx0, hx1, hz1, az1, y - 0.4f, y);
            }
            Rect2(dg, ax0, ax1, az0, az1, 108.45f, 108.95f);               // the roof plate
            // the chevron points (45В° diamonds) on every plate
            for (int lvl = 0; lvl < 4; lvl++)
            {
                float y = (lvl == 0) ? 89.75f : (lvl == 1 ? 96.15f : (lvl == 2 ? 102.55f : 108.95f));
                dg.Box(0, new Vector3(acx, y - 0.55f, az0 - 2f), new Vector3(9f, 1.1f, 9f), Quaternion.Euler(0f, 45f, 0f), 0.35f);
                dg.Box(0, new Vector3(acx, y - 0.55f, az1 + 2f), new Vector3(9f, 1.1f, 9f), Quaternion.Euler(0f, 45f, 0f), 0.35f);
                dg.Box(0, new Vector3(ax0 - 2f, y - 0.55f, acz), new Vector3(9f, 1.1f, 9f), Quaternion.Euler(0f, 45f, 0f), 0.35f);
                dg.Box(0, new Vector3(ax1 + 2f, y - 0.55f, acz), new Vector3(9f, 1.1f, 9f), Quaternion.Euler(0f, 45f, 0f), 0.35f);
            }
            // the perimeter walls: real window openings, no glazing
            Wall(dg, new Vector3(acx, 0f, az1), Vector3.right, Vector3.forward, 40f, 89.75f, 108.95f, 3, 6.4f,
                 2.4f, 2.6f, 1.8f, 0.2f, 3.6f, new List<Vector4>(), null, 1, 0f);
            Wall(dg, new Vector3(acx, 0f, az0), Vector3.left, Vector3.back, 40f, 89.75f, 108.95f, 3, 6.4f,
                 2.4f, 2.6f, 1.8f, 0.2f, 3.6f, new List<Vector4>(), null, 1, 0f);
            Wall(dg, new Vector3(ax1, 0f, acz), Vector3.forward, Vector3.right, 34f, 89.75f, 108.95f, 3, 6.4f,
                 2.4f, 2.6f, 1.8f, 0.2f, 3.6f, new List<Vector4>(), null, 1, 0f);
            Wall(dg, new Vector3(ax0, 0f, acz), Vector3.back, Vector3.left, 34f, 89.75f, 108.95f, 3, 6.4f,
                 2.4f, 2.6f, 1.8f, 0.2f, 3.6f, new List<Vector4>(), null, 1, 0f);
            // the **experimental stair**: two long straight flights of 40 steps (0,16 m each) in the rear
            // strip, alternating lanes, with landings вЂ” its own design, clear of the tower's footprint
            Flight(dg, new Vector3(hx1, 89.75f, 15.5f), Vector3.left, 40, 6.4f, 3.0f);
            Rect2(dg, -24.5f, -22.5f, 14f, 17f, 95.85f, 96.15f);
            Flight(dg, new Vector3(-24.5f, 96.15f, 18.5f), Vector3.right, 40, 6.4f, 3.0f);
            Rect2(dg, -12.5f, hx1, 17f, 20f, 102.25f, 102.55f);
            dg.Apply(detGo, mRough, mDark);
            AddMeshCollider(detGo, "house_details");

            // v0.9.98: the air-hockey table on the annex's top floor (see AirHockeyTable)
            AirHockeyTable(house.transform);
            // v0.10.8: six patterned showroom tables on the floor below
            AirHockeyVariants(house.transform);
            // v0.10.17: five AI-slop arcade cabinets along the top hall's back wall
            ArcadeCabinets(house.transform);

            // --- v0.9.72: detail pass 1 (plan points 4/6) вЂ” the stylobate's roof balustrade, the
            //     portal's open steel leaves, wall sconces, the hall's pendant lamps, two dark-red
            //     banners and a mast with a red star on the core. House-local metres.
            {
                var d2Go = new GameObject("house_detail2");
                d2Go.transform.SetParent(house.transform, false);
                var d2 = new Geo();

                // roof balustrade along the stylobate's front edge (the figures stand behind it)
                {
                    float zr = -hd + 0.32f, yb = ptop;
                    for (float x = -hw + 1.2f; x <= hw - 1.19f; x += 2.55f)
                        d2.Box(0, new Vector3(x, yb + 0.45f, zr),
                               new Vector3(0.18f, 0.90f, 0.18f), Quaternion.identity, 0.5f);
                    d2.Box(1, new Vector3(0f, yb + 0.90f, zr),
                           new Vector3(2f * hw - 1.6f, 0.12f, 0.16f), Quaternion.identity, 0.5f);
                    d2.Box(1, new Vector3(0f, yb + 0.48f, zr),
                           new Vector3(2f * hw - 1.6f, 0.08f, 0.10f), Quaternion.identity, 0.5f);
                }

                // the portal's open leaves: two 6,7 x 5,6 m steel panels, hinged at В±7 m, swung 78В° in
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    float hx = sd * 7.0f;
                    var q = Quaternion.Euler(0f, sd * 78f, 0f);
                    Vector3 H = new Vector3(hx, BASE, -id + 0.2f);
                    Vector3 LP(float lx, float ly, float lz) => H + q * new Vector3(-sd * lx, ly, lz);
                    void DB(int part, float lx, float ly, float lz, float sx, float sy, float sz)
                        => d2.Box(part, LP(lx, ly, lz), new Vector3(sx, sy, sz), q, 0.5f);
                    DB(1, 3.35f, 2.8f, 0f, 6.70f, 5.60f, 0.24f);
                    for (int i = 1; i <= 3; i++)
                        DB(0, i * 6.70f / 4f, 2.8f, 0f, 0.12f, 5.40f, 0.28f);
                    DB(0, 3.35f, 2.8f, 0f, 6.70f, 0.14f, 0.28f);
                    DB(1, 6.35f, 1.35f, 0.20f, 0.10f, 1.10f, 0.10f);
                }

                // wall sconces: a bracket on the jamb's front face and one inside the hall
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    d2.Box(1, new Vector3(sd * 9.2f, 6.55f, -id - 3.32f),
                           new Vector3(0.42f, 0.52f, 0.34f), Quaternion.identity, 0.5f);
                    d2.Box(1, new Vector3(sd * 10.0f, 6.15f, -id + 1.04f),
                           new Vector3(0.36f, 0.46f, 0.18f), Quaternion.identity, 0.5f);
                }

                // the pendant lamps' rods and shades in the central passage (house-local)
                float ceilY = ptop - 0.78f;
                for (int i = -2; i <= 2; i++)
                {
                    float lx = i * 6f, ys = 8.30f;
                    float rodY = (ceilY + ys + 0.15f) * 0.5f, rodH = ceilY - ys - 0.15f;
                    d2.Box(1, new Vector3(lx, rodY, 2f), new Vector3(0.04f, rodH, 0.04f),
                           Quaternion.identity, 0.5f);
                    d2.Box(1, new Vector3(lx, ys, 2f), new Vector3(0.90f, 0.26f, 0.90f),
                           Quaternion.identity, 0.5f);
                }

                // the core's mast
                float mastBase = coreTop, mastTop = coreTop + 22f;
                float mz = coreZ0 + coreD * 0.5f;
                d2.Box(0, new Vector3(0f, (mastBase + mastTop) * 0.5f, mz),
                       new Vector3(0.34f, mastTop - mastBase, 0.34f), Quaternion.identity, 0.5f);
                for (int i = 0; i < 6; i++)
                    d2.Box(1, new Vector3(0f, mastBase + 2.4f + i * 2.6f, mz),
                           new Vector3(2.4f - i * 0.25f, 0.10f, 0.10f), Quaternion.identity, 0.5f);

                // the banners' metal rods (the fabric itself is a separate mesh below)
                for (int sd = -1; sd <= 1; sd += 2)
                    d2.Box(1, new Vector3(sd * 15.42f, 11.55f, -4.5f),
                           new Vector3(0.12f, 0.12f, 3.4f), Quaternion.identity, 0.5f);

                // v0.9.98: a warm pendant over the air-hockey table on the annex's top floor. The annex
                // had no light of its own (the House's pendants are in the hall 90 m below), so the table
                // would be a silhouette in the probe-SH gloom. Same fitting as the hall's: a dark rod, a
                // metal shade and a warm emissive lens.
                {
                    const float ahX = -37.75f, ahZ = 3f;
                    // v0.10.0: a **flush ceiling fixture** instead of the long pendant — the top-down
                    // mini-game camera sits at house-local ~108,5 and the old shade would block the rink.
                    d2.Box(1, new Vector3(ahX, 108.86f, ahZ), new Vector3(1.10f, 0.14f, 1.10f),
                           Quaternion.identity, 0.5f);
                    Lamp(house.transform, new Vector3(ahX, 108.55f, ahZ), 2.0f, 22f);
                }

                d2.Apply(d2Go, mRough, mMetal);
                AddMeshCollider(d2Go, "house_detail2");

                // the warm lenses (sconces + pendants) and the star are emissive second meshes
                var warmGo = new GameObject("house_warm");
                warmGo.transform.SetParent(house.transform, false);
                var wg = new Geo();
                // v0.9.98: the warm lens under the annex's air-hockey fixture (v0.10.0: flush with the
                // ceiling, above the top-down camera, so it never blocks the rink)
                wg.Box(1, new Vector3(-37.75f, 108.78f, 3f), new Vector3(0.90f, 0.05f, 0.90f),
                       Quaternion.identity, 0.5f);
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    wg.Box(1, new Vector3(sd * 9.2f, 6.30f, -id - 3.58f),
                           new Vector3(0.30f, 0.34f, 0.06f), Quaternion.identity, 0.5f);
                    wg.Box(1, new Vector3(sd * 10.0f, 5.94f, -id + 1.18f),
                           new Vector3(0.24f, 0.28f, 0.05f), Quaternion.identity, 0.5f);
                }
                for (int i = -2; i <= 2; i++)
                    wg.Box(1, new Vector3(i * 6f, 8.14f, 2f), new Vector3(0.72f, 0.06f, 0.72f),
                           Quaternion.identity, 0.5f);
                wg.Apply(warmGo, mRough, WarmGlowMaterial());
                AddMeshCollider(warmGo, "house_warm");

                // the red star: two crossed flat five-point stars at the mast's top
                var starGo = new GameObject("house_star");
                starGo.transform.SetParent(house.transform, false);
                var sg = new Geo();
                Star(sg, new Vector3(0f, mastTop + 2.2f, mz), 2.1f, Vector3.forward);
                Star(sg, new Vector3(0f, mastTop + 2.2f, mz), 2.1f, Vector3.right);
                sg.Apply(starGo, GlowMaterial());
                AddMeshCollider(starGo, "house_star");

                // the warm lights themselves (point lights; the level has no sun)
                for (int sd = -1; sd <= 1; sd += 2)
                    Lamp(house.transform, new Vector3(sd * 9.2f, 6.2f, -id - 3.9f), 1.7f, 18f);
                for (int i = -2; i <= 2; i++)
                    Lamp(house.transform, new Vector3(i * 6f, 7.9f, 2f), 1.1f, 16f);

                // two dark-red banners on the towers' inner walls, clear of the portals
                var banGo = new GameObject("house_banners");
                banGo.transform.SetParent(house.transform, false);
                var bng = new Geo();
                var embGo = new GameObject("house_banners_emblem");
                embGo.transform.SetParent(house.transform, false);
                var eg = new Geo();
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    float bx = sd * 15.42f;
                    bng.Box(0, new Vector3(bx, 7.5f, -4.5f), new Vector3(0.06f, 8.0f, 3.0f),
                            Quaternion.identity, 0.4f);
                    eg.Box(0, new Vector3(bx - sd * 0.04f, 9.7f, -4.5f), new Vector3(0.05f, 1.6f, 1.6f),
                           Quaternion.identity, 0.4f);
                }
                bng.Apply(banGo, BannerMaterial());
                AddMeshCollider(banGo, "house_banners");
                eg.Apply(embGo, mPainted);
                AddMeshCollider(embGo, "house_banners_emblem");
            }

            // v0.9.59: the five figures leave this roof for another Stalin block (see FiguresOnBlock)
            FiguresOnBlock();

            log.LogInfo($"[HOUSE] v0.9.63: built to the references — stylobate {pw:0.0}x{pd:0.0} m — " +
                        $"ground floor recessed {inset:0} m on 5 round columns, overhanging upper storey with " +
                        $"the window ribbon and the loggia band (top {ptop + p.y:0.0} m); each tower is two " +
                        $"sections: lower {tw:0.0}x{td:0.0} m, {lfl:0} floors with a blank 2-storey base band " +
                        $"and a cornice -> {lTop + p.y:0.0} m, upper {tw:0.0}x{tdu:0.0} m, {ufl:0} floors set " +
                        $"3 m back at the front with **glazed bays** over the break -> {uTop + p.y:0.0} m; " +
                        $"the {2f * tx0:0.0} m gap carries the **wide gallery** (lower floors 6-7) and the " +
                        $"**thin bridge** (upper floors 2-3); narrow core {coreW:0}x{coreD:0} m to " +
                        $"{coreTop + p.y:0.0} m; rear blocks to {rtop + p.y:0.0} m + a 20 m mast; plan " +
                        $"unchanged");
            builtCount++;
        }
    }
}
