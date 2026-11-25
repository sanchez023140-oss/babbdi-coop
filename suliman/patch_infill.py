import re

p = r"E:\858\BabbdiWork\Project\suliman\build_map.py"
s = open(p, encoding="utf-8").read()
start = s.index("infill = 0")
end_marker = 'print("infill:", infill, "-> total", len(buildings))'
end = s.index(end_marker) + len(end_marker)
block = s[start:end]
s = s[:start] + s[end:]

indented = "\n".join(("    " + line if line.strip() else line) for line in block.splitlines())
func = "def add_infill():\n" + indented + "\n"
anchor = 'print("roads:", len(roads), "pieces")'
pos = s.index(anchor) + len(anchor)
s = s[:pos] + "\n\n" + func + "\nadd_infill()\n" + s[pos:]
open(p, "w", encoding="utf-8").write(s)
print("patched")
