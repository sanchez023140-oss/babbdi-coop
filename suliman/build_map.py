"""
Build the Suliman map data for BabbdiCoop.

Source data (all open):
  * OpenStreetMap via Overpass  - building footprints, roads, rivers, landuse
  * Mapzen/SRTM terrarium tiles - 30 m terrain

Output: BabbdiCoop/SulimanData.cs + a verification plan in Art/suliman_map.png
"""
import base64
import json
import math
import os
import random
import struct

from PIL import Image, ImageDraw

DIR = r"E:\858\BabbdiWork\Project\suliman"
OUT_CS = r"E:\858\BabbdiWork\Project\BabbdiCoop\SulimanData.cs"
ART = r"E:\858\BabbdiWork\Art"

# ---- geographic frame -----------------------------------------------------
LAT0, LON0 = 62.7900, 148.1700
ML = 111132.0
MO = 111320.0 * math.cos(math.radians(LAT0))

# crop in OSM-local metres (x east, y north)
X0, X1 = -2150.0, -150.0
Y0, Y1 = -2100.0, -300.0
SCALE = 0.85                      # positions compressed 15 %, footprints keep their size
MAP_W = (X1 - X0) * SCALE         # 1700 m
MAP_D = (Y1 - Y0) * SCALE         # 1530 m
DEM_BASE = 640.0                  # world y = (h - DEM_BASE); town terrace ~ 650
NX, NZ = 192, 176                 # terrain grid
CELL_X = MAP_W / (NX - 1)
CELL_Z = MAP_D / (NZ - 1)

random.seed(20261125)


def mx(x): return (x - X0) * SCALE
def mz(y): return (y - Y0) * SCALE


def lon_of(x): return LON0 + x / MO
def lat_of(y): return LAT0 + y / ML


# ---- DEM ------------------------------------------------------------------
Z, TMINX, TMINY = 14, 14933, 4492
_dem = Image.open(os.path.join(DIR, "dem", "dem_merged.png"))
_dpx = _dem.load()


def tile_xy(z, lon, lat):
    n = 2 ** z
    x = (lon + 180.0) / 360.0 * n
    r = math.radians(lat)
    y = (1.0 - math.log(math.tan(r) + 1.0 / math.cos(r)) / math.pi) / 2.0 * n
    return x, y


def dem_at(x, y):
    """Bilinear DEM height at OSM-local metres."""
    fx, fy = tile_xy(Z, lon_of(x), lat_of(y))
    u, v = (fx - TMINX) * 256.0 - 0.5, (fy - TMINY) * 256.0 - 0.5
    x0, y0 = int(math.floor(u)), int(math.floor(v))
    tx, ty = u - x0, v - y0
    def p(xx, yy):
        xx = max(0, min(767, xx)); yy = max(0, min(767, yy))
        return _dpx[xx, yy]
    a = p(x0, y0) * (1 - tx) + p(x0 + 1, y0) * tx
    b = p(x0, y0 + 1) * (1 - tx) + p(x0 + 1, y0 + 1) * tx
    return a * (1 - ty) + b * ty


def load(name):
    try:
        return json.load(open(os.path.join(DIR, name), encoding="utf-8")).get("elements", [])
    except Exception:
        return []


def osm_xy(g):
    return ((g["lon"] - LON0) * MO, (g["lat"] - LAT0) * ML)


# ---- geometry helpers -----------------------------------------------------
def area2(pts):
    a = 0.0
    for i in range(len(pts)):
        x1, z1 = pts[i]; x2, z2 = pts[(i + 1) % len(pts)]
        a += x1 * z2 - x2 * z1
    return a * 0.5


def rdp(pts, eps):
    """Douglas-Peucker on an open polyline."""
    if len(pts) < 3:
        return pts[:]
    ax, az = pts[0]; bx, bz = pts[-1]
    dx, dz = bx - ax, bz - az
    L = math.hypot(dx, dz)
    best, bi = -1.0, 0
    for i in range(1, len(pts) - 1):
        px, pz = pts[i]
        if L < 1e-6:
            d = math.hypot(px - ax, pz - az)
        else:
            d = abs(dz * (px - ax) - dx * (pz - az)) / L
        if d > best:
            best, bi = d, i
    if best <= eps:
        return [pts[0], pts[-1]]
    left = rdp(pts[:bi + 1], eps)
    right = rdp(pts[bi:], eps)
    return left[:-1] + right


def simplify_ring(pts, eps):
    if len(pts) <= 4:
        return pts[:]
    p = pts[:] + [pts[0]]
    s = rdp(p, eps)
    if s[0] == s[-1]:
        s = s[:-1]
    return s if len(s) >= 3 else pts[:]


def clip_seg(a, b, xmin, xmax, zmin, zmax):
    """Liang-Barsky, returns (t0,t1) or None."""
    ax, az = a; bx, bz = b
    dx, dz = bx - ax, bz - az
    t0, t1 = 0.0, 1.0
    for p, q in ((-dx, ax - xmin), (dx, xmax - ax), (-dz, az - zmin), (dz, zmax - az)):
        if abs(p) < 1e-12:
            if q < 0:
                return None
            continue
        r = q / p
        if p < 0:
            if r > t1: return None
            if r > t0: t0 = r
        else:
            if r < t0: return None
            if r < t1: t1 = r
    return (t0, t1)


def clip_polyline(pts):
    """Clip a polyline to the crop; returns a list of polylines."""
    out, cur = [], []
    for i in range(len(pts) - 1):
        r = clip_seg(pts[i], pts[i + 1], 0.0, MAP_W, 0.0, MAP_D)
        if r is None:
            if len(cur) > 1: out.append(cur)
            cur = []
            continue
        t0, t1 = r
        a = (pts[i][0] + (pts[i + 1][0] - pts[i][0]) * t0,
             pts[i][1] + (pts[i + 1][1] - pts[i][1]) * t0)
        b = (pts[i][0] + (pts[i + 1][0] - pts[i][0]) * t1,
             pts[i][1] + (pts[i + 1][1] - pts[i][1]) * t1)
        if not cur or math.hypot(cur[-1][0] - a[0], cur[-1][1] - a[1]) > 0.2:
            if len(cur) > 1: out.append(cur)
            cur = [a]
        cur.append(b)
        if t1 < 1.0:
            out.append(cur); cur = []
    if len(cur) > 1: out.append(cur)
    # drop pieces that only graze the border
    return [p for p in out if len(p) >= 2 and seg_len(p) > 8.0]


def seg_len(p):
    return sum(math.hypot(p[i + 1][0] - p[i][0], p[i + 1][1] - p[i][1]) for i in range(len(p) - 1))


def dist_point_seg(px, pz, ax, az, bx, bz):
    dx, dz = bx - ax, bz - az
    L2 = dx * dx + dz * dz
    if L2 < 1e-9:
        return math.hypot(px - ax, pz - az)
    t = max(0.0, min(1.0, ((px - ax) * dx + (pz - az) * dz) / L2))
    return math.hypot(px - (ax + dx * t), pz - (az + dz * t))


# ---- buildings ------------------------------------------------------------
SIGNS = [
    "", "СРЕДНЯЯ ШКОЛА №1", "ДЕТСКАЯ МУЗЫКАЛЬНАЯ ШКОЛА", "АДМИНИСТРАЦИЯ",
    "ОМВД", "АВТОВОКЗАЛ", "ДОМ ДЕТСКОГО ТВОРЧЕСТВА", "ПОЧТА", "МУЗЕЙ",
    "РАЙОННЫЙ СУД", "ПРОФЕССИОНАЛЬНЫЙ ЛИЦЕЙ №2", "СУСУМАНЗОЛОТО",
    "МАГАЗИН", "КОТЕЛЬНАЯ", "БАНЯ", "ДОМ КУЛЬТУРЫ", "ЦЕРКОВЬ",
]
SIGN_KEYS = {
    "Средняя Школа №1": 1,
    "Начальная общеобразовательная школа г. Сусумана": 1,
    "Детская музыкальная школа": 2,
    "Администрация Сусуманского р-на": 3,
    "ОМВД по Сусуманскому району": 4,
    "Автовокзал": 5,
    "Дом детского творчества": 6,
    "Почта России": 7,
    "Народный музей": 8,
    "Сусумансакий районный суд": 9,
    "Профессиональный лицей № 2": 10,
    'ОАО "СусуманЗолото"': 11,
    "Луч": 12,
    "Морджот": 12,
    "Церковь Спиридона Тримифунтского": 16,
    "Учкомбинат": 10,
}

bld_raw = [e for e in load("osm_buildings.json") if e.get("geometry")]
buildings = []
for w in bld_raw:
    pts = [osm_xy(g) for g in w["geometry"]]
    if len(pts) < 3:
        continue
    cx = sum(p[0] for p in pts) / len(pts)
    cy = sum(p[1] for p in pts) / len(pts)
    if not (X0 < cx < X1 and Y0 < cy < Y1):
        continue
    t = w.get("tags", {})
    a = abs(area2(pts))
    name = t.get("name", "")
    sign = SIGN_KEYS.get(name, 0)
    kind_t = t.get("building", "yes")
    amen = t.get("amenity", "")

    # kind: 0 panel, 1 stucco, 2 wood, 3 private, 4 garage, 5 industrial,
    #       6 shop, 7 school/admin, 8 church, 9 ruin
    if sign in (16,):
        kind = 8
    elif amen in ("school",) or kind_t == "school" or sign in (1, 2, 6, 10):
        kind = 7
    elif amen in ("townhall", "police", "courthouse", "bus_station"):
        kind = 7
    elif sign in (3, 4, 5, 9, 11):
        kind = 7
    elif kind_t in ("garage", "garages", "carport") or a < 70:
        kind = 4
    elif kind_t in ("industrial", "warehouse", "factory", "hangar") or a > 2600:
        kind = 5
    elif kind_t in ("commercial", "retail") or sign == 12:
        kind = 6
    elif kind_t == "house" or a < 150:
        kind = 3
    elif a < 620:
        bw = max(p[0] for p in pts) - min(p[0] for p in pts)
        bd = max(p[1] for p in pts) - min(p[1] for p in pts)
        elong = max(bw, bd) / max(1.0, min(bw, bd))
        kind = 2 if (a < 340 or elong > 3.4) else 1
    elif a < 950:
        kind = 1
    else:
        kind = 0

    # floors
    try:
        lv = int(float(t.get("building:levels", "0")))
    except Exception:
        lv = 0
    if lv <= 0:
        if kind == 0: lv = 5
        elif kind == 1: lv = 4
        elif kind == 2: lv = 2
        elif kind == 3: lv = 1
        elif kind == 4: lv = 1
        elif kind == 5: lv = 2
        elif kind == 6: lv = 1
        elif kind == 7: lv = 4
        elif kind == 8: lv = 1
        else: lv = 3
    if kind == 0 and a > 2200 and random.random() < 0.35:
        lv = 9
    if kind == 5 and a > 3000:
        lv = max(2, min(4, lv))
    if kind == 7:
        lv = max(2, min(4, lv))

    # style palette index (resolved in C#)
    style = random.randrange(0, 6)
    if kind in (2, 3):
        style = random.randrange(6, 10)
    elif kind == 5:
        style = random.randrange(10, 13)
    elif kind == 7:
        style = random.choice([0, 0, 1, 2, 3, 4, 5])
    elif kind == 8:
        style = 13
    elif kind == 4:
        style = random.randrange(10, 13)
    elif kind in (0, 1):
        style = random.choice([0, 0, 0, 1, 1, 2, 3, 4, 5])

    # inhabited vs abandoned
    d2town = math.hypot(cx - (-1050), cy - (-1200))
    abandoned = (kind == 5 and random.random() < 0.35) or \
                (d2town > 480 and random.random() < 0.30) or \
                (random.random() < 0.06)
    if kind == 6 or kind == 7 or sign:
        abandoned = random.random() < 0.05
    lit = 0 if abandoned else (1 if random.random() < 0.82 else 2)

    pts = simplify_ring(pts, 0.35)
    if area2(pts) < 0:
        pts.reverse()
    if len(pts) > 32:
        pts = pts[:32]
    buildings.append(dict(pts=pts, a=a, kind=kind, lv=lv, style=style, lit=lit,
                          sign=sign, name=name, cx=cx, cy=cy,
                          seed=random.randrange(1, 1 << 30)))

print("buildings:", len(buildings))

# ---- infill: small winter houses/garages in the empty plots of the town ----
# OSM in Susuman is sparse inside the blocks; these keep the streets from reading as a field.
def to_osm_map(mx_, mz_):
    return (X0 + mx_ / SCALE, Y0 + mz_ / SCALE)


def near_road(x, z, pad):
    for r in roads:
        for i in range(len(r["pts"]) - 1):
            if dist_point_seg(x, z, r["pts"][i][0], r["pts"][i][1],
                              r["pts"][i + 1][0], r["pts"][i + 1][1]) < r["w"] * 0.5 + pad:
                return True
    return False


def near_building(x, z, pad):
    for b in buildings:
        bx, bz = mx(b["cx"]), mz(b["cy"])
        rr = math.sqrt(b["a"]) * 0.62
        if math.hypot(bx - x, bz - z) < rr + pad:
            return True
    return False


avoid = [(958.0, 716.0, 52.0), (900.0, 580.0, 32.0), (930.0, 920.0, 24.0),
         (1180.0, 1180.0, 28.0), (455.0, 1240.0, 22.0), (1000.0, 1440.0, 26.0),
         (630.0, 400.0, 40.0), (760.0, 300.0, 34.0), (520.0, 1100.0, 22.0),
         (1560.0, 700.0, 26.0)]


# ---- roads ----------------------------------------------------------------
ROAD_W = {"trunk": 15.0, "primary": 12.0, "secondary": 10.0, "tertiary": 9.0,
          "residential": 8.0, "living_street": 7.0, "unclassified": 7.0,
          "service": 4.5, "track": 3.5, "footway": 2.6, "path": 2.2,
          "pedestrian": 5.0, "cycleway": 3.0}
ROAD_CLS = {"trunk": 0, "primary": 0, "secondary": 1, "tertiary": 1,
            "residential": 2, "living_street": 2, "unclassified": 2,
            "service": 3, "track": 4, "footway": 5, "path": 5,
            "pedestrian": 5, "cycleway": 5}

roads = []
for e in load("osm_roads.json"):
    if not e.get("geometry"):
        continue
    hw = e.get("tags", {}).get("highway", "")
    if hw not in ROAD_W:
        continue
    pts = [osm_xy(g) for g in e["geometry"]]
    pieces = clip_polyline([(mx(x), mz(y)) for x, y in pts])
    for p in pieces:
        p = rdp(p, 0.5)
        if len(p) < 2:
            continue
        if seg_len(p) < 12:
            continue
        roads.append(dict(pts=p, w=ROAD_W[hw], cls=ROAD_CLS[hw], hw=hw,
                          name=e.get("tags", {}).get("name", "")))
print("roads:", len(roads), "pieces")

def add_infill():
    infill = 0
    for gz in range(480, 1160, 38):
        for gx in range(520, 1400, 38):
            if random.random() > 0.62:
                continue
            x = gx + random.uniform(-14, 14)
            z = gz + random.uniform(-14, 14)
            if any((x - ax) ** 2 + (z - az) ** 2 < rr * rr for ax, az, rr in avoid):
                continue
            if near_road(x, z, 7.0) or near_building(x, z, 6.0):
                continue
            kind = random.choice([3, 3, 3, 4, 2])
            if kind == 4:
                w, d = random.uniform(4.5, 6.5), random.uniform(4.0, 6.0)
                lv = 1
            elif kind == 2:
                w, d = random.uniform(9.0, 14.0), random.uniform(6.0, 8.5)
                lv = 2
            else:
                w, d = random.uniform(7.0, 10.0), random.uniform(6.5, 9.0)
                lv = 1
            ang = random.uniform(0, math.pi / 2)
            ca, sa = math.cos(ang), math.sin(ang)
            corners = []
            for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                px = sx * w * 0.5 * ca - sz * d * 0.5 * sa
                pz = sx * w * 0.5 * sa + sz * d * 0.5 * ca
                corners.append(to_osm_map(x + px, z + pz))
            if area2(corners) < 0:
                corners.reverse()
            buildings.append(dict(pts=corners, a=w * d, kind=kind, lv=lv,
                                  style=random.randrange(6, 10) if kind != 4 else random.randrange(10, 13),
                                  lit=1 if random.random() < 0.78 else 0, sign=0, name="",
                                  cx=to_osm_map(x, z)[0], cy=to_osm_map(x, z)[1],
                                  seed=random.randrange(1, 1 << 30)))
            infill += 1
    print("infill:", infill, "-> total", len(buildings))

add_infill()


# ---- water ----------------------------------------------------------------
rivers, ponds, streams = [], [], []
for e in load("osm_water.json"):
    if not e.get("geometry"):
        continue
    t = e.get("tags", {})
    pts = [(mx(x), mz(y)) for x, y in (osm_xy(g) for g in e["geometry"])]
    if t.get("natural") == "water":
        if len(pts) >= 3 and area2(pts) != 0:
            c = (sum(p[0] for p in pts) / len(pts), sum(p[1] for p in pts) / len(pts))
            if 0 <= c[0] <= MAP_W and 0 <= c[1] <= MAP_D and 30 < abs(area2(pts)) < 60000:
                ponds.append(dict(pts=pts))
    elif t.get("waterway") == "river":
        for p in clip_polyline(pts):
            p = rdp(p, 0.8)
            if len(p) >= 2 and seg_len(p) > 60:
                rivers.append(p)
    elif t.get("waterway") == "stream":
        for p in clip_polyline(pts):
            p = rdp(p, 0.8)
            if len(p) >= 2 and seg_len(p) > 25:
                streams.append(p)
# keep the 3 longest river pieces
rivers.sort(key=seg_len, reverse=True)
rivers = rivers[:3]
print("rivers:", len(rivers), [round(seg_len(p)) for p in rivers], "ponds:", len(ponds))

# ---- terrain --------------------------------------------------------------
river_pts = [p for poly in rivers for p in poly]


def nearest_river(x, z):
    best = 1e9
    for poly in rivers:
        for i in range(len(poly) - 1):
            d = dist_point_seg(x, z, poly[i][0], poly[i][1], poly[i + 1][0], poly[i + 1][1])
            if d < best:
                best = d
    return best


# height map in OSM-local metres, then compressed to map
H = [[0.0] * NX for _ in range(NZ)]
for j in range(NZ):
    z = j * CELL_Z
    oy = Y0 + z / SCALE
    for i in range(NX):
        x = i * CELL_X
        ox = X0 + x / SCALE
        h = dem_at(ox, oy) - DEM_BASE

        # river channel: the ICE surface sits ~2.6 m below the bank
        d = nearest_river(x, z) if rivers else 1e9
        if d < 60.0:
            target = -3.2 + 0.004 * d * d
            if h > target:
                w = 1.0 if d < 22 else max(0.0, 1.0 - (d - 22) / 38.0)
                h = h * (1.0 - w) + target * w

        # procedural ridges at the map border (Susuman is in a valley)
        edge = min(x, z, MAP_W - x, MAP_D - z)
        if edge < 300.0:
            k = (300.0 - edge) / 300.0
            ridge = (math.sin(x * 0.0121 + 1.3) * math.cos(z * 0.0157 - 0.4) * 0.5 + 0.5)
            h += k * k * (18.0 + 38.0 * ridge)

        # soft drifts / hummocks everywhere (1-3 m)
        h += (math.sin(x * 0.031 + z * 0.017) * 0.5 +
              math.sin(x * 0.013 - z * 0.027 + 2.1) * 0.5) * 1.1
        H[j][i] = h

hmin = min(min(r) for r in H)
hmax = max(max(r) for r in H)
print("terrain: %.1f .. %.1f m" % (hmin, hmax))


def height_at(x, z):
    fx = max(0.0, min(NX - 1.001, x / CELL_X))
    fz = max(0.0, min(NZ - 1.001, z / CELL_Z))
    i, j = int(fx), int(fz)
    tx, tz = fx - i, fz - j
    a = H[j][i] * (1 - tx) + H[j][i + 1] * tx
    b = H[j + 1][i] * (1 - tx) + H[j + 1][i + 1] * tx
    return a * (1 - tz) + b * tz


# ---- lamps ----------------------------------------------------------------
# In town, along streets; every ~44 m, alternating sides. Light arm points to the road.
lamps = []
town = (-1050.0, -1200.0)
for r in roads:
    if r["cls"] > 2 or r["w"] < 5:
        continue
    pts = r["pts"]
    acc = 0.0
    next_d = 18.0
    side = 1
    for i in range(len(pts) - 1):
        ax, az = pts[i]; bx, bz = pts[i + 1]
        L = math.hypot(bx - ax, bz - az)
        while next_d < acc + L:
            t = (next_d - acc) / max(1e-6, L)
            x = ax + (bx - ax) * t
            z = az + (bz - az) * t
            # offset to one side
            ux, uz = (bx - ax) / max(1e-6, L), (bz - az) / max(1e-6, L)
            off = r["w"] * 0.5 + 1.8
            lx, lz = x - uz * off * side, z + ux * off * side
            ox, oz = X0 + lx / SCALE, Y0 + lz / SCALE
            if math.hypot(ox - town[0], oz - town[1]) < 620 and 6 < lx < MAP_W - 6 and 6 < lz < MAP_D - 6:
                lamps.append((lx, lz, math.degrees(math.atan2(ux * side, uz * side))))
            side = -side
            next_d += 44.0
        acc += L
print("lamps:", len(lamps))

# ---- trees ----------------------------------------------------------------
tree_mask = []
for b in buildings:
    for p in b["pts"]:
        tree_mask.append((mx(p[0]), mz(p[1]), 16.0))
trees = []
# grid with jitter, denser near the borders / away from town
step = 21.0
for gz in range(10, int(MAP_D) - 10, int(step)):
    for gx in range(10, int(MAP_W) - 10, int(step)):
        x = gx + random.uniform(-9, 9)
        z = gz + random.uniform(-9, 9)
        d_town = math.hypot(x - 935.0, z - 765.0)
        p = 0.10 + 0.55 * max(0.0, (d_town - 520.0) / 500.0)
        edge = min(x, z, MAP_W - x, MAP_D - z)
        if edge < 260:
            p += 0.35
        if random.random() > p:
            continue
        ok = True
        for b in buildings:
            bx, bz = mx(b["cx"]), mz(b["cy"])
            if abs(bx - x) < 30 and abs(bz - z) < 30:
                # exact-enough check against the footprint bounding circle
                rr = max(8.0, math.sqrt(b["a"]) * 0.62 + 6.0)
                if math.hypot(bx - x, bz - z) < rr:
                    ok = False
                    break
        if not ok:
            continue
        for r in roads:
            for i in range(len(r["pts"]) - 1):
                if dist_point_seg(x, z, r["pts"][i][0], r["pts"][i][1],
                                  r["pts"][i + 1][0], r["pts"][i + 1][1]) < r["w"] * 0.5 + 2.2:
                    ok = False
                    break
            if not ok:
                break
        if not ok:
            continue
        if nearest_river(x, z) < 22:
            continue
        # hillside only? allow some in town parks
        trees.append((x, z, random.uniform(0.75, 1.55), random.randrange(0, 3)))
print("trees:", len(trees))

# ---- utility poles --------------------------------------------------------
poles, pole_links = [], []
for ri, r in enumerate(roads):
    if r["cls"] not in (0, 1, 2):
        continue
    pts = r["pts"]
    acc, next_d = 0.0, 22.0
    chain = []
    side = 1 if ri % 2 == 0 else -1
    for i in range(len(pts) - 1):
        ax, az = pts[i]; bx, bz = pts[i + 1]
        L = math.hypot(bx - ax, bz - az)
        while next_d < acc + L:
            t = (next_d - acc) / max(1e-6, L)
            x, z = ax + (bx - ax) * t, az + (bz - az) * t
            ux, uz = (bx - ax) / L, (bz - az) / L
            off = r["w"] * 0.5 + 2.6
            px, pz = x - uz * off * side, z + ux * off * side
            # keep poles off the buildings
            bad = False
            for b in buildings:
                if math.hypot(mx(b["cx"]) - px, mz(b["cy"]) - pz) < math.sqrt(b["a"]) * 0.55 + 4:
                    bad = True; break
            if not bad and 4 < px < MAP_W - 4 and 4 < pz < MAP_D - 4:
                chain.append((px, pz))
            next_d += 42.0
        acc += L
    if len(chain) >= 2:
        base = len(poles)
        poles.extend(chain)
        pole_links.append((base, len(chain)))
print("poles:", len(poles), "chains:", len(pole_links))

# ---- bridges (road segments crossing a river) -----------------------------
bridges = []
for r in roads:
    if r["cls"] > 2:
        continue
    pts = r["pts"]
    for i in range(len(pts) - 1):
        mxp = (pts[i][0] + pts[i + 1][0]) * 0.5
        mzp = (pts[i][1] + pts[i + 1][1]) * 0.5
        if rivers and nearest_river(mxp, mzp) < 12.0:
            ax, az = pts[i]; bx, bz = pts[i + 1]
            L = math.hypot(bx - ax, bz - az)
            if L < 1e-3:
                continue
            ux, uz = (bx - ax) / L, (bz - az) / L
            bridges.append((ax - ux * 10, az - uz * 10, bx + ux * 10, bz + uz * 10,
                            max(6.0, r["w"] + 2.0)))
# deduplicate
uniq = []
for b in bridges:
    if not any(math.hypot(b[0] - u[0], b[1] - u[1]) < 20 for u in uniq):
        uniq.append(b)
bridges = uniq
print("bridges:", len(bridges))

# ---- emit C# --------------------------------------------------------------
def farr(vals, per_line=14, fmt="%.3ff"):
    out = []
    for i in range(0, len(vals), per_line):
        out.append(", ".join(fmt % v for v in vals[i:i + per_line]) + ",")
    return "\n        ".join(out)


def barr(vals, per_line=32):
    out = []
    for i in range(0, len(vals), per_line):
        out.append(", ".join(str(v) for v in vals[i:i + per_line]) + ",")
    return "\n        ".join(out)


def b64_u16(vals):
    raw = bytearray()
    for v in vals:
        raw += struct.pack("<H", max(0, min(65535, int(round(v)))))
    return base64.b64encode(bytes(raw)).decode("ascii")


# heights in decimetres above DEM_BASE (0 .. 6553 m), row-major z then x
heights = []
for j in range(NZ):
    for i in range(NX):
        heights.append((H[j][i] - hmin) * 10.0)
BASE = hmin
print("height base %.2f m, max %.2f m" % (BASE, hmax - hmin))

bld_off = [0]
bld_pts, bld_h, bld_kind, bld_style, bld_floors, bld_lit, bld_sign, bld_seed = ([] for _ in range(8))
for b in buildings:
    for (x, y) in b["pts"]:
        bld_pts += [mx(x), mz(y)]
    bld_off.append(len(bld_pts) // 2)
    bld_h.append(b["a"])
    bld_kind.append(b["kind"])
    bld_style.append(b["style"])
    bld_floors.append(b["lv"])
    bld_lit.append(b["lit"])
    bld_sign.append(b["sign"])
    bld_seed.append(b["seed"])

road_off = [0]
road_pts, road_w, road_cls = [], [], []
for r in roads:
    for (x, z) in r["pts"]:
        road_pts += [x, z]
    road_off.append(len(road_pts) // 2)
    road_w.append(r["w"])
    road_cls.append(r["cls"])

water_polys = []
for p in rivers:
    water_polys.append((0, 102.0, p))
for p in streams:
    water_polys.append((1, 7.0, p))
water_off = [0]
water_pts, water_w, water_kind = [], [], []
for kind, w, p in water_polys:
    for (x, z) in p:
        water_pts += [x, z]
    water_off.append(len(water_pts) // 2)
    water_w.append(w)
    water_kind.append(kind)

pond_off = [0]
pond_pts = []
for p in ponds:
    for (x, z) in p["pts"]:
        pond_pts += [x, z]
    pond_off.append(len(pond_pts) // 2)

lamp_flat = []
for (x, z, yaw) in lamps:
    lamp_flat += [x, z, yaw]

tree_flat = []
for (x, z, s, k) in trees:
    tree_flat += [x, z, s, float(k)]

pole_links_flat = []
for (b, n) in pole_links:
    pole_links_flat += [b, n]

bridge_flat = []
for b in bridges:
    bridge_flat += list(b)

cs = f'''// <auto-generated> SulimanData.cs -- generated by suliman/build_map.py.
// Sources: OpenStreetMap (ODbL) and SRTM/Mapzen terrain tiles. Do not edit by hand.
using System;

namespace BabbdiCoop
{{
    internal static class SulimanData
    {{
        public const float W = {MAP_W:.1f}f;
        public const float D = {MAP_D:.1f}f;
        public const float Scale = {SCALE}f;
        public const float BaseY = {BASE:.2f}f;
        public const int NX = {NX};
        public const int NZ = {NZ};
        public const float CellX = {CELL_X:.4f}f;
        public const float CellZ = {CELL_Z:.4f}f;

        // ushort decimetres above BaseY, row-major (z * NX + x)
        public const string HeightB64 =
            "{b64_u16(heights)}";

        // ---- buildings ----
        public const int BldCount = {len(buildings)};
        public static readonly int[] BldOff = {{
        {barr(bld_off)}
        }};
        public static readonly float[] BldPts = {{
        {farr(bld_pts)}
        }};
        public static readonly float[] BldArea = {{
        {farr(bld_h, 12, "%.1ff")}
        }};
        public static readonly byte[] BldKind = {{
        {barr(bld_kind)}
        }};
        public static readonly byte[] BldStyle = {{
        {barr(bld_style)}
        }};
        public static readonly byte[] BldFloors = {{
        {barr(bld_floors)}
        }};
        public static readonly byte[] BldLit = {{
        {barr(bld_lit)}
        }};
        public static readonly byte[] BldSign = {{
        {barr(bld_sign)}
        }};
        public static readonly int[] BldSeed = {{
        {barr(bld_seed, 12)}
        }};

        // ---- roads ----
        public const int RoadCount = {len(roads)};
        public static readonly int[] RoadOff = {{
        {barr(road_off)}
        }};
        public static readonly float[] RoadPts = {{
        {farr(road_pts)}
        }};
        public static readonly float[] RoadW = {{
        {farr(road_w, 16, "%.1ff")}
        }};
        public static readonly byte[] RoadCls = {{
        {barr(road_cls)}
        }};

        // ---- water ----
        public const int WaterCount = {len(water_polys)};
        public static readonly int[] WaterOff = {{
        {barr(water_off)}
        }};
        public static readonly float[] WaterPts = {{
        {farr(water_pts)}
        }};
        public static readonly float[] WaterW = {{
        {farr(water_w, 16, "%.1ff")}
        }};
        public static readonly byte[] WaterKind = {{
        {barr(water_kind)}
        }};

        public const int PondCount = {len(ponds)};
        public static readonly int[] PondOff = {{
        {barr(pond_off)}
        }};
        public static readonly float[] PondPts = {{
        {farr(pond_pts)}
        }};

        // ---- street furniture ----
        public const int LampCount = {len(lamps)};
        public static readonly float[] Lamps = {{   // x, z, yaw
        {farr(lamp_flat, 9)}
        }};

        public const int TreeCount = {len(trees)};
        public static readonly float[] Trees = {{   // x, z, scale, kind
        {farr(tree_flat, 8)}
        }};

        public const int PoleCount = {len(poles)};
        public static readonly float[] Poles = {{   // x, z
        {farr([v for p in poles for v in p], 10)}
        }};
        public static readonly int[] PoleChains = {{
        {barr(pole_links_flat, 16)}
        }};

        public const int BridgeCount = {len(bridges)};
        public static readonly float[] Bridges = {{   // x1, z1, x2, z2, width
        {farr(bridge_flat, 10)}
        }};

        public static readonly string[] SignText = {{
            {", ".join('"%s"' % s if s else '""' for s in SIGNS)}
        }};
    }}
}}
'''
open(OUT_CS, "w", encoding="utf-8-sig").write(cs)
print("->", OUT_CS, len(cs), "bytes")

# ---- verification plan ----------------------------------------------------
Wp = 1500
S = Wp / MAP_W
Hp = int(MAP_D * S)
img = Image.new("RGB", (Wp, Hp), (12, 17, 24))
dr = ImageDraw.Draw(img)


def sx(x): return int(x * S)
def sy(z): return int(Hp - z * S)


for j in range(NZ - 1, 0, -2):
    for i in range(0, NX, 2):
        h = H[j][i]
        c = int(max(22, min(150, 40 + (h + 4) * 2.2)))
        dr.rectangle([sx(i * CELL_X), sy((j + 1) * CELL_Z),
                      sx((i + 2) * CELL_X), sy((j - 1) * CELL_Z)], fill=(c // 3, c // 2 + 6, c))

for poly in rivers:
    dr.line([(sx(x), sy(z)) for x, z in poly], fill=(45, 110, 160), width=13)
for poly in streams:
    dr.line([(sx(x), sy(z)) for x, z in poly], fill=(40, 90, 120), width=3)
for p in ponds:
    dr.polygon([(sx(x), sy(z)) for x, z in p["pts"]], fill=(35, 80, 110))

for r in roads:
    c = (190, 155, 90) if r["cls"] == 0 else ((120, 120, 120) if r["cls"] <= 2 else (70, 75, 85))
    dr.line([(sx(x), sy(z)) for x, z in r["pts"]], fill=c, width=max(1, int(r["w"] * S)))

pal = {0: (225, 210, 175), 1: (200, 180, 150), 2: (150, 115, 80), 3: (130, 105, 80),
       4: (110, 110, 110), 5: (95, 100, 105), 6: (210, 170, 130), 7: (235, 230, 220),
       8: (180, 150, 120), 9: (120, 110, 100)}
for b in buildings:
    pts = [(sx(mx(x)), sy(mz(y))) for x, y in b["pts"]]
    dr.polygon(pts, fill=pal[b["kind"]], outline=(20, 20, 20))

for (x, z, yaw) in lamps:
    dr.ellipse([sx(x) - 1, sy(z) - 1, sx(x) + 1, sy(z) + 1], fill=(255, 200, 100))
for (x, z, s, k) in trees:
    dr.point((sx(x), sy(z)), fill=(50, 90, 60))

dr.text((8, 6), "Suliman: %.0fx%.0f m (%.2f km2)  bld %d  roads %d  lamps %d  trees %d" %
        (MAP_W, MAP_D, MAP_W * MAP_D / 1e6, len(buildings), len(roads), len(lamps), len(trees)),
        fill=(235, 235, 235))
p = os.path.join(ART, "suliman_map.png")
img.save(p)
print("->", p, img.size)
