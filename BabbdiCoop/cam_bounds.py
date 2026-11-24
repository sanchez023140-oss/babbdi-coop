p = 'SovietDistrict.cs'
s = open(p, encoding='utf-8').read()

# static fields for the interior cameras (world space, derived from the model's own bounds)
s = s.replace("        private const float HeliYaw = -90f;",
              "        private const float HeliYaw = -90f;\n"
              "        private static Vector3 heliInA, heliInB;   // world-space interior cameras")

anchor = "                Vector3 modelSize = mb.size * HeliScale;"
cam = """                Vector3 modelSize = mb.size * HeliScale;
                // v0.9.102: interior cameras taken from the mesh's own bounds, not guessed
                Vector3 bc = mb.center, bs = mb.size;
                heliInA = go.transform.TransformPoint(bc + new Vector3(0f, bs.y * 0.02f, -bs.z * 0.04f));
                heliInB = go.transform.TransformPoint(bc + new Vector3(0f, -bs.y * 0.02f, bs.z * 0.30f));"""
assert anchor in s
s = s.replace(anchor, cam, 1)

anchor2 = "                l.Add(PL(hp + hr * new Vector3(-8f, 6.0f, 0f), hp + hr * new Vector3(6f, 5.4f, 0f)));"
add = anchor2 + """
                if (heliInA != Vector3.zero)
                {
                    l.Add(new PreviewShot { Pos = heliInA, Look = heliInB, Lamp = true });
                    l.Add(new PreviewShot { Pos = heliInB, Look = heliInA, Lamp = true });
                }"""
assert anchor2 in s
s = s.replace(anchor2, add, 1)
open(p, 'w', encoding='utf-8').write(s)
print('interior cameras from bounds')
