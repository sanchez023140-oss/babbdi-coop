"""Decode the terrarium DEM tiles, inspect the town crop, and render a plan."""
import json, math, os
from PIL import Image, ImageDraw, ImageFilter

DIR = r"E:\858\BabbdiWork\Project\suliman"
ART = r"E:\858\BabbdiWork\Art"
OUT = os.path.join(DIR, "dem")
LAT0, LON0 = 62.7900, 148.1700
ML = 111132.0
MO = 111320.0 * math.cos(math.radians(LAT0))

# crop in metres rel. to (LAT0, LON0)
X0, X1 = -2400.0, -200.0
Y0, Y1 = -2200.0, -300.0


def lon_of(x): return LON0 + x / MO
def lat_of(y): return LAT0 + y / ML


def tile_xy(z, lon, lat):
    n = 2 ** z
    x = (lon + 180.0) / 360.0 * n
    r = math.radians(lat)
    y = (1.0 - math.log(math.tan(r) + 1.0 / math.cos(r)) / math.pi) / 2.0 * n
    return x, y


Z = 14
# mosaic
minx = miny = 10 ** 9
maxx = maxy = -1
for (lon, lat) in [(lon_of(X0), lat_of(Y0)), (lon_of(X1), lat_of(Y0)),
                   (lon_of(X0), lat_of(Y1)), (lon_of(X1), lat_of(Y1))]:
    tx, ty = tile_xy(Z, lon, lat)
    minx = min(minx, int(math.floor(tx))); maxx = max(maxx, int(math.floor(tx)))
    miny = min(miny, int(math.floor(ty))); maxy = max(maxy, int(math.floor(ty)))
W = (maxx - minx + 1) * 256
H = (maxy - miny + 1) * 256
mosaic = Image.new("I", (W, H), 0)
px = mosaic.load()
for ty in range(miny, maxy + 1):
    for tx in range(minx, maxx + 1):
        p = os.path.join(OUT, "14_%d_%d.png" % (tx, ty))
        im = Image.open(p).convert("RGB")
        w, h = im.size
        r, g, b = im.split()
        rp, gp, bp = r.load(), g.load(), b.load()
        ox = (tx - minx) * 256
        oy = (ty - miny) * 256
        for yy in range(h):
            for xx in range(w):
                px[ox + xx, oy + yy] = int(round(rp[xx, yy] * 256 + gp[xx, yy] + bp[xx, yy] / 256.0 - 32768.0))

print("mosaic", W, H, "tiles", minx, maxx, miny, maxy)


def wgs_tile_px(lon, lat):
    fx, fy = tile_xy(Z, lon, lat)
    return (fx - minx) * 256.0, (fy - miny) * 256.0


# heights at crop corners / centre
for nm, (x, y) in [("SW", (X0, Y0)), ("SE", (X1, Y0)), ("NW", (X0, Y1)),
                   ("NE", (X1, Y1)), ("C", ((X0 + X1) / 2, (Y0 + Y1) / 2))]:
    u, v = wgs_tile_px(lon_of(x), lat_of(y))
    print(nm, "h=%.1f" % px[int(u), int(v)])

# height stats for the crop
hs = []
step = 8
for y in range(int(H)):
    for x in range(0, W, 1):
        pass
# sample in crop space
for yy in range(0, 256):
    for xx in range(0, 256):
        mx = X0 + (X1 - X0) * xx / 255.0
        my = Y0 + (Y1 - Y0) * yy / 255.0
        u, v = wgs_tile_px(lon_of(mx), lat_of(my))
        hs.append(px[int(u), int(v)])
hs.sort()
print("crop height: min %.0f q10 %.0f med %.0f q90 %.0f max %.0f" %
      (hs[0], hs[int(.1 * len(hs))], hs[len(hs) // 2], hs[int(.9 * len(hs))], hs[-1]))

# ---- plan ---------------------------------------------------------------
def load(name):
    p = os.path.join(DIR, name)
    try:
        return json.load(open(p, encoding="utf-8")).get("elements", [])
    except Exception:
        return []


def xy(g):
    return ((g["lon"] - LON0) * MO, (g["lat"] - LAT0) * ML)


bld = [e for e in load("osm_buildings.json") if e.get("geometry")]
roads = [e for e in load("osm_roads.json") if e.get("geometry")]
land = [e for e in load("osm_landuse.json") if e.get("geometry")]

Wp, Hp = 1500, int(1500 * (Y1 - Y0) / (X1 - X0))
S = Wp / (X1 - X0)
img = Image.new("RGB", (Wp, Hp), (14, 18, 26))
dr = ImageDraw.Draw(img)


def sx(x): return int((x - X0) * S)
def sy(y): return int(Hp - (y - Y0) * S)


# background: elevation hillshade
bg = Image.new("RGB", (Wp, Hp), (14, 18, 26))
bgpx = bg.load()
for yy in range(0, Hp, 2):
    for xx in range(0, Wp, 2):
        mx = X0 + xx / S
        my = Y0 + (Hp - yy) / S
        u, v = wgs_tile_px(lon_of(mx), lat_of(my))
        h = px[int(u), int(v)]
        c = int(max(20, min(90, (h - 580) * 0.55)))
        for dy in (0, 1):
            for dx in (0, 1):
                if xx + dx < Wp and yy + dy < Hp:
                    bgpx[xx + dx, yy + dy] = (c // 2, c // 2 + 2, c)
img = bg
dr = ImageDraw.Draw(img)

for e in land:
    lu = e.get("tags", {}).get("landuse", "")
    if lu in ("residential", "industrial", "commercial", "retail"):
        pts = [(sx(xy(g)[0]), sy(xy(g)[1])) for g in e["geometry"]]
        dr.polygon(pts, outline=(45, 55, 60))

for e in roads:
    hw = e.get("tags", {}).get("highway", "")
    pts = [(sx(xy(g)[0]), sy(xy(g)[1])) for g in e["geometry"]]
    if len(pts) < 2:
        continue
    c = (180, 150, 90) if hw in ("trunk", "primary", "secondary") else (70, 80, 90)
    dr.line(pts, fill=c, width=2 if c == (180, 150, 90) else 1)

lv = {}
for e in bld:
    pts = [(sx(xy(g)[0]), sy(xy(g)[1])) for g in e["geometry"]]
    if len(pts) < 3:
        continue
    t = e.get("tags", {})
    try:
        L = int(float(t.get("building:levels", "0")))
    except Exception:
        L = 0
    a = 0.0
    for i in range(len(pts)):
        a += pts[i][0] * pts[(i + 1) % len(pts)][1] - pts[(i + 1) % len(pts)][0] * pts[i][1]
    area = abs(a) * 0.5
    if L == 0:
        L = 1 if area < 800 else (2 if area < 2500 else 5)
    lv[L] = lv.get(L, 0) + 1
    pal = {1: (120, 100, 80), 2: (150, 120, 90), 3: (170, 150, 110),
           4: (190, 170, 130), 5: (210, 200, 180), 9: (240, 240, 240)}
    c = pal.get(L, (200, 200, 200))
    dr.polygon(pts, fill=c, outline=(30, 30, 30))

# grid every 250 m
for gx in range(int(X0), int(X1) + 1, 250):
    dr.line([(sx(gx), 0), (sx(gx), Hp)], fill=(35, 45, 55))
    dr.text((sx(gx) + 2, 4), "%d" % gx, fill=(100, 120, 130))
for gy in range(int(Y0), int(Y1) + 1, 250):
    dr.line([(0, sy(gy)), (Wp, sy(gy))], fill=(35, 45, 55))
    dr.text((4, sy(gy) + 2), "%d" % gy, fill=(100, 120, 130))

dr.text((8, Hp - 20), "Susuman crop %.0f..%.0f x %.0f..%.0f, %d bld, %d roads, levels %s" %
        (X0, X1, Y0, Y1, len(bld), len(roads), lv), fill=(230, 230, 230))
p = os.path.join(ART, "suliman_crop.png")
img.save(p)
print("->", p, img.size)
