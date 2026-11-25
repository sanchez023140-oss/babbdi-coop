"""Plan of the final Suliman crop: DEM hillshade, water, roads, buildings by type."""
import json, math, os
from PIL import Image, ImageDraw

DIR = r"E:\858\BabbdiWork\Project\suliman"
ART = r"E:\858\BabbdiWork\Art"
LAT0, LON0 = 62.7900, 148.1700
ML = 111132.0
MO = 111320.0 * math.cos(math.radians(LAT0))
X0, X1 = -2150.0, -150.0
Y0, Y1 = -2100.0, -300.0


def lon_of(x): return LON0 + x / MO
def lat_of(y): return LAT0 + y / ML


def tile_xy(z, lon, lat):
    n = 2 ** z
    x = (lon + 180.0) / 360.0 * n
    r = math.radians(lat)
    y = (1.0 - math.log(math.tan(r) + 1.0 / math.cos(r)) / math.pi) / 2.0 * n
    return x, y


Z, minx, miny = 14, 14933, 4492
dem = Image.open(os.path.join(DIR, "dem", "dem_merged.png")) if os.path.exists(
    os.path.join(DIR, "dem", "dem_merged.png")) else None
if dem is None:
    # merge tiles
    W = H = 768
    dem = Image.new("I", (W, H), 0)
    px = dem.load()
    for ty in range(3):
        for tx in range(3):
            im = Image.open(os.path.join(DIR, "dem", "14_%d_%d.png" % (minx + tx, miny + ty))).convert("RGB")
            r, g, b = im.split()
            rp, gp, bp = r.load(), g.load(), b.load()
            for yy in range(256):
                for xx in range(256):
                    px[tx * 256 + xx, ty * 256 + yy] = int(round(
                        rp[xx, yy] * 256 + gp[xx, yy] + bp[xx, yy] / 256.0 - 32768.0))
    dem.save(os.path.join(DIR, "dem", "dem_merged.png"))
dpx = dem.load()


def wgs_tile_px(lon, lat):
    fx, fy = tile_xy(Z, lon, lat)
    return (fx - minx) * 256.0, (fy - miny) * 256.0


def load(name):
    try:
        return json.load(open(os.path.join(DIR, name), encoding="utf-8")).get("elements", [])
    except Exception:
        return []


def xy(g):
    return ((g["lon"] - LON0) * MO, (g["lat"] - LAT0) * ML)


Wp = 1600
S = Wp / (X1 - X0)
Hp = int((Y1 - Y0) * S)
img = Image.new("RGB", (Wp, Hp), (10, 14, 20))
dr = ImageDraw.Draw(img)


def sx(x): return int((x - X0) * S)
def sy(y): return int(Hp - (y - Y0) * S)


# hillshade
for yy in range(Hp):
    my = Y0 + (Hp - yy) / S
    for xx in range(0, Wp, 2):
        mx = X0 + xx / S
        u, v = wgs_tile_px(lon_of(mx), lat_of(my))
        try:
            h = dpx[int(u), int(v)]
        except Exception:
            h = 650
        c = int(max(18, min(110, 30 + (h - 630) * 1.4)))
        dr.line([(xx, yy), (xx + 1, yy)], fill=(c // 3, c // 2 + 8, c))
    # every row is slow; skip every other row
# cheap: vertical smear to fill the skipped rows
# (draw every row is fine at 1600x1440)

bld = [e for e in load("osm_buildings.json") if e.get("geometry")]
roads = [e for e in load("osm_roads.json") if e.get("geometry")]
water = [e for e in load("osm_water.json") if e.get("geometry")]
land = [e for e in load("osm_landuse.json") if e.get("geometry")]

for e in land:
    lu = e.get("tags", {}).get("landuse", "")
    if lu in ("residential", "industrial", "commercial", "retail"):
        pts = [(sx(xy(g)[0]), sy(xy(g)[1])) for g in e["geometry"]]
        dr.polygon(pts, outline=(45, 60, 65))

for e in water:
    t = e.get("tags", {})
    pts = [(sx(xy(g)[0]), sy(xy(g)[1])) for g in e["geometry"]]
    if len(pts) < 2:
        continue
    if t.get("natural") == "water":
        if len(pts) >= 3:
            dr.polygon(pts, fill=(30, 60, 80), outline=(40, 90, 120))
    elif t.get("waterway") == "river":
        dr.line(pts, fill=(40, 100, 140), width=4)
    elif t.get("waterway") == "stream":
        dr.line(pts, fill=(40, 90, 110), width=2)

for e in roads:
    hw = e.get("tags", {}).get("highway", "")
    pts = [(sx(xy(g)[0]), sy(xy(g)[1])) for g in e["geometry"]]
    if len(pts) < 2:
        continue
    c = (190, 155, 90) if hw in ("trunk", "primary", "secondary") else (70, 82, 92)
    dr.line(pts, fill=c, width=3 if c == (190, 155, 90) else 1)

cnt = 0
for e in bld:
    pts = [(sx(xy(g)[0]), sy(xy(g)[1])) for g in e["geometry"]]
    if len(pts) < 3:
        continue
    cx = sum(p[0] for p in pts) / len(pts)
    cy = sum(p[1] for p in pts) / len(pts)
    if not (0 <= cx < Wp and 0 <= cy < Hp):
        continue
    cnt += 1
    t = e.get("tags", {})
    b = t.get("building", "yes")
    a = 0.0
    for i in range(len(pts)):
        a += pts[i][0] * pts[(i + 1) % len(pts)][1] - pts[(i + 1) % len(pts)][0] * pts[i][1]
    area = abs(a) * 0.5 / (S * S)
    L = 0
    try:
        L = int(float(t.get("building:levels", "0")))
    except Exception:
        pass
    if L == 0:
        L = 1 if area < 900 else (2 if area < 2600 else 5)
    pal = {1: (105, 95, 85), 2: (140, 120, 98), 3: (165, 145, 110),
           4: (190, 175, 140), 5: (215, 208, 190), 9: (245, 245, 245)}
    c = pal.get(L, (200, 200, 200))
    dr.polygon(pts, fill=c, outline=(25, 25, 25))

for gx in range(int(X0), int(X1) + 1, 250):
    dr.line([(sx(gx), 0), (sx(gx), Hp)], fill=(30, 40, 50))
    dr.text((sx(gx) + 2, 4), "%d" % gx, fill=(110, 130, 140))
for gy in range(int(Y0), int(Y1) + 1, 250):
    dr.line([(0, sy(gy)), (Wp, sy(gy))], fill=(30, 40, 50))
    dr.text((4, sy(gy) + 2), "%d" % gy, fill=(110, 130, 140))

dr.text((8, Hp - 18), "Suliman crop %.0fx%.0f m  %d buildings, %d road ways, %d water" %
        (X1 - X0, Y1 - Y0, cnt, len(roads), len(water)), fill=(235, 235, 235))
p = os.path.join(ART, "suliman_crop2.png")
img.save(p)
print("->", p, img.size, "buildings in crop", cnt)
