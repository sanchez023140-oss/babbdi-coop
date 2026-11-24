p = 'SovietDistrict.cs'
s = open(p, encoding='utf-8').read()

old = """                heliInA = go.transform.TransformPoint(bc + new Vector3(0f, bs.y * 0.02f, -bs.z * 0.04f));
                heliInB = go.transform.TransformPoint(bc + new Vector3(0f, -bs.y * 0.02f, bs.z * 0.30f));"""
new = """                // v0.9.104: locate the bay by raycast instead of guessing offsets.
                Vector3 centreW = go.transform.TransformPoint(bc);
                Vector3 axis = go.transform.TransformDirection(Vector3.forward);   // model +Z
                Vector3 side = go.transform.TransformDirection(Vector3.up);
                Vector3 start = centreW + axis * (bs.z * 0.42f) + Vector3.up * (bs.y * 0.02f);
                if (Physics.Raycast(start, -axis, out var hitIn, bs.z * 0.9f, ~0, QueryTriggerInteraction.Ignore))
                {
                    heliInA = hitIn.point - axis * 1.6f;
                    heliInB = hitIn.point + axis * 8f;
                }
                else
                {
                    heliInA = centreW;
                    heliInB = centreW + axis * 5f;
                }"""
assert old in s
s = s.replace(old, new)

s = s.replace("l.Add(PL(hp + hr * new Vector3(12f, 5.6f, 0f), hp + hr * new Vector3(26f, 4.6f, 0f)));",
              "l.Add(new PreviewShot { Pos = heliInA, Look = heliInB, Lamp = true });")
s = s.replace("l.Add(PL(hp + hr * new Vector3(21f, 4.2f, 0f), hp + hr * new Vector3(4f, 5.6f, 0f)));",
              "l.Add(new PreviewShot { Pos = heliInB, Look = heliInA, Lamp = true });")
open(p, 'w', encoding='utf-8').write(s)
print('bay camera by raycast')
