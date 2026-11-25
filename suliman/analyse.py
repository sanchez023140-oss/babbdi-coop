"""Analyse the Overpass extracts for Susuman and draw a plan."""
import json, math, os
from PIL import Image, ImageDraw

DIR = r"E:\858\BabbdiWork\Project\suliman"
OUT = r"E:\858\BabbdiWork\Art"
os.makedirs(OUT, exist_ok=True)

LAT0, LON0 = 62.7900, 148.1700
M_LAT = 111132.0
M_LON = 111320.0 * math.cos(math.radians(LAT0))


def load(name):
    p = os.path.join(DIR, name)
    if not os.path.exists(p):
        return []
    try:
        return json.load(open(p, encoding="utf-8")).get("elements", [])
    except Exception as e:
        print("failed", name, e)
        return []


def xy(pt):
    return ((pt["lon"] - LON0) * M_LON, (pt["lat"] - LAT0) * M_LAT)


bld = [e for e in load("osm_buildings.json") if e.get("type") == "way" and e.get("geometry")]
roads = [e for e in load("osm_roads.json") if e.get("type") == "way" and e.get("geometry")]
land = [e for e in load("osm_landuse.json") if e.get("type") == "way" and e.get("geometry")]

# ---- extents -------------------------------------------------------------
xs, ys = [], []
for e in bld:
    for g in e["geometry"]:
        x, y = xy(g)
        xs.append(x); ys.append(y)
print("buildings:", len(bld))
print("extent x %.0f..%.0f (%.0f m), y %.0f..%.0f (%.0f m)" %
      (min(xs), max(xs), max(xs) - min(xs), min(ys), max(ys), max(ys) - min(ys)))

lv = {}
typ = {}
for e in bld:
    t = e.get("tags", {})
    lv[t.get("building:levels", "?")] = lv.get(t.get("building:levels", "?"), 0) + 1
    b = t.get("building", "yes")
    typ[b] = typ.get(b, 0) + 1
print("levels:", dict(sorted(lv.items(), key=lambda kv: (kv[0] == "?", kv[0]))))
print("types:", dict(sorted(typ.items(), key=lambda kv: -kv[1])))

# footprint area
tot = 0.0
for e in bld:
    pts = [xy(g) for g in e["geometry"]]
    a = 0.0
    for i in range(len(pts)):
        x1, y1 = pts[i]; x2, y2 = pts[(i + 1) % len(pts)]
        a += x1 * y2 - x2 * y1
    tot += abs(a) * 0.5
print("total footprint area: %.0f m2 (%.2f ha)" % (tot, tot / 10000.0))

print("roads:", len(roads), " landuse:", len(land))

# ---- plan render ---------------------------------------------------------
x0, x1 = min(xs) - 60, max(xs) + 60
y0, y1 = min(ys) - 60, max(ys) + 60
W = 1600
S = W / max(x1 - x0, y1 - y0)
H = int((y1 - y0) * S)
img = Image.new("RGB", (W, H), (18, 20, 24))
dr = ImageDraw.Draw(img)


def sx(x): return int((x - x0) * S)
def sy(y): return int(H - (y - y0) * S)


cols = {
    "residential": (120, 120, 128), "apartments": (150, 150, 150), "house": (110, 105, 96),
    "school": (150, 130, 90), "kindergarten": (150, 140, 100), "industrial": (90, 80, 70),
    "garage": (80, 78, 74), "garages": (80, 78, 74), "warehouse": (95, 88, 78),
    "yes": (110, 110, 110),
}
for e in land:
    lu = e.get("tags", {}).get("landuse", "")
    if lu in ("residential", "industrial", "commercial", "retail", "forest", "meadow", "grass"):
        pts = [g for g in e["geometry"]]
        dr.polygon([(sx(xy(g)[0]), sy(xy(g)[1])) for g in pts], outline=(50, 60, 50))

for e in roads:
    hw = e.get("tags", {}).get("highway", "")
    pts = [(sx(xy(g)[0]), sy(xy(g)[1])) for g in e["geometry"]]
    if len(pts) < 2:
        continue
    c = (200, 160, 90) if hw in ("primary", "secondary", "trunk") else (90, 95, 105)
    dr.line(pts, fill=c, width=3 if hw in ("primary", "secondary", "trunk") else 1)

for e in bld:
    t = e.get("tags", {})
    pts = [(sx(xy(g)[0]), sy(xy(g)[1])) for g in e["geometry"]]
    if len(pts) < 3:
        continue
    b = t.get("building", "yes")
    c = cols.get(b, (100, 100, 104))
    try:
        L = int(float(t.get("building:levels", "1")))
    except Exception:
        L = 1
    c = tuple(min(255, int(v * (0.75 + 0.06 * min(L, 9)))) for v in c)
    dr.polygon(pts, fill=c, outline=(40, 40, 44))

dr.text((8, 6), "Susuman OSM: %d buildings, %d roads, %.0fx%.0f m" %
        (len(bld), len(roads), max(xs) - min(xs), max(ys) - min(ys)), fill=(220, 220, 220))
p = os.path.join(OUT, "suliman_plan.png")
img.save(p)
print("plan ->", p, img.size)
