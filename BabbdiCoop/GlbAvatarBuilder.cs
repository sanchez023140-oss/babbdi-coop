using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace BabbdiCoop
{
    /// <summary>
    /// Unity-side adapter for glTF/GLB avatars.
    ///
    /// Coordinate handling (verified in the offline raster harness):
    ///   * vertex positions are passed through UNTOUCHED (RH glTF data read directly
    ///     in Unity's LH space gives the correct winding),
    ///   * uv: v' = 1 - v (glTF v is top-down; loader already wraps into [0,1)),
    ///   * scale / centering / optional 180° yaw are applied with wrapper transforms
    ///     (required for skinned meshes: vertices must stay in mesh space).
    ///
    /// Skinned models get real bones + SkinnedMeshRenderer + GlbAnimator, so the
    /// soldier plays Idle/Walk/Run and turns his head with the camera pitch.
    /// </summary>
    public static class GlbAvatarBuilder
    {
        public const float TargetHeight = 2.12f; // meters (the game's capsule is 2.0 m tall; +14 % from 1.86)

        public static GameObject Load(string glbPath, bool yaw180 = false, LogStub log = null)
        {
            var parsed = GlbModelLoader.Parse(glbPath);
            if (parsed.Primitives.Count == 0)
                throw new InvalidDataException("GLB has no primitives");

            // Unity quality levels can drop to 1-2 bone skinning; our rigs carry 4
            // weights per vertex and dropping influences makes limbs stretch while
            // animating, so make sure the session runs with FourBones.
            if (QualitySettings.skinWeights != SkinWeights.FourBones)
            {
                if (log?.LogWarning != null)
                    log.LogWarning($"[GLB] QualitySettings.skinWeights was {QualitySettings.skinWeights} → forcing FourBones");
                QualitySettings.skinWeights = SkinWeights.FourBones;
            }

            float h = parsed.Max.Y - parsed.Min.Y;
            if (h < 0.001f) throw new InvalidDataException("GLB degenerate height");
            float scale = TargetHeight / h;
            log?.LogInfo?.Invoke($"[GLB] raw height {h:F2} m → avatar {TargetHeight:F2} m (scale {scale:F3})");
            float midX = (parsed.Min.X + parsed.Max.X) * 0.5f;
            float midZ = (parsed.Min.Z + parsed.Max.Z) * 0.5f;

            var root = new GameObject("AvatarMesh");

            // wrapper chain: yaw (outermost, so centering stays correct) → centering → scale
            var yawRoot = new GameObject("YawRoot").transform;
            yawRoot.SetParent(root.transform, false);
            yawRoot.localRotation = yaw180 ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;

            var centerRoot = new GameObject("CenterRoot").transform;
            centerRoot.SetParent(yawRoot, false);
            centerRoot.localPosition = new Vector3(-midX * scale, -parsed.Min.Y * scale, -midZ * scale);

            var scaleRoot = new GameObject("ScaleRoot").transform;
            scaleRoot.SetParent(centerRoot, false);
            scaleRoot.localScale = Vector3.one * scale;

            // decode texture(s) once
            var texByIdx = new Dictionary<int, Texture2D>();
            foreach (var mat in parsed.Materials)
            {
                if (texByIdx.ContainsKey(mat.Item2)) continue;
                if (mat.Item2 < 0 || mat.Item2 >= parsed.Images.Count || parsed.Images[mat.Item2].Png == null) continue;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true); // mip chain: no shimmer at distance
                tex.name = "avatar_tex_" + mat.Item2;
                if (tex.LoadImage(parsed.Images[mat.Item2].Png))
                {
                    tex.wrapMode = TextureWrapMode.Repeat;
                    tex.filterMode = FilterMode.Trilinear;
                    tex.anisoLevel = 2;
                    tex.Apply(true, false);
                    texByIdx[mat.Item2] = tex;
                }
                else if (log != null) log.LogWarning($"[GLB] texture {mat.Item2} LoadImage failed");
            }

            var shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            // ---- skeleton: one GameObject per glTF node ----------------------------
            var nodeTr = new Transform[parsed.Nodes.Count];
            for (int i = 0; i < parsed.Nodes.Count; i++)
            {
                var n = parsed.Nodes[i];
                var go = new GameObject(string.IsNullOrEmpty(n.Name) ? $"node{i}" : n.Name);
                nodeTr[i] = go.transform;
                nodeTr[i].localPosition = new Vector3(n.T.X, n.T.Y, n.T.Z);
                nodeTr[i].localRotation = new Quaternion(n.R.X, n.R.Y, n.R.Z, n.R.W);
                nodeTr[i].localScale = new Vector3(n.S.X, n.S.Y, n.S.Z);
            }
            for (int i = 0; i < parsed.Nodes.Count; i++)
            {
                int p = parsed.Nodes[i].Parent;
                nodeTr[i].SetParent(p >= 0 ? nodeTr[p] : scaleRoot, false);
            }

            // ---- meshes ------------------------------------------------------------
            foreach (var prim in parsed.Primitives)
            {
                if (prim.Indices == null || prim.Indices.Length < 3) continue;

                bool skinned = prim.SkinIndex >= 0 && prim.SkinIndex < parsed.Skins.Count
                    && prim.RawPositions != null && prim.BoneIndices != null && prim.BoneWeights != null;
                if (skinned)
                {
                    var skinCheck = parsed.Skins[prim.SkinIndex];
                    if (skinCheck.Joints == null || skinCheck.Joints.Length == 0
                        || skinCheck.Joints.Any(j => j < 0 || j >= nodeTr.Length)
                        || prim.BoneIndices.Any(b => b < 0 || b >= skinCheck.Joints.Length))
                        skinned = false;
                }

                var srcPos = skinned ? prim.RawPositions : prim.Positions;
                if (srcPos == null || prim.Uvs == null || prim.Uvs.Length != srcPos.Length) continue;

                var verts = new Vector3[srcPos.Length];
                var uvs = new Vector2[prim.Uvs.Length];
                for (int i = 0; i < srcPos.Length; i++)
                {
                    verts[i] = new Vector3(srcPos[i].X, srcPos[i].Y, srcPos[i].Z); // untouched, glTF mesh/world space
                    uvs[i] = new Vector2(prim.Uvs[i].X, 1f - prim.Uvs[i].Y);
                }

                var mesh = new Mesh { name = prim.Name ?? "part" };
                mesh.vertices = verts;
                mesh.uv = uvs;
                mesh.triangles = prim.Indices;

                var go = new GameObject(prim.Name ?? "part");
                go.transform.SetParent(scaleRoot, false);

                var mat = new Material(shader);
                if (prim.MaterialIndex >= 0 && prim.MaterialIndex < parsed.Materials.Count)
                {
                    int texIdx = parsed.Materials[prim.MaterialIndex].Item2;
                    if (texIdx >= 0 && texByIdx.TryGetValue(texIdx, out var texFound))
                        mat.mainTexture = texFound;
                }

                if (skinned)
                {
                    var skin = parsed.Skins[prim.SkinIndex];
                    var bw = new BoneWeight[srcPos.Length];
                    for (int i = 0; i < bw.Length; i++)
                    {
                        // glTF weights can sum to 0.98 → Unity does NOT renormalise
                        // runtime bone weights, which makes the mesh "breathe" while
                        // animating. Normalise to exactly 1.
                        float w0 = prim.BoneWeights[i * 4 + 0];
                        float w1 = prim.BoneWeights[i * 4 + 1];
                        float w2 = prim.BoneWeights[i * 4 + 2];
                        float w3 = prim.BoneWeights[i * 4 + 3];
                        float sum = w0 + w1 + w2 + w3;
                        if (sum > 1e-5f)
                        {
                            float inv = 1f / sum;
                            w0 *= inv; w1 *= inv; w2 *= inv; w3 *= inv;
                        }
                        else
                        {
                            w0 = 1f; w1 = w2 = w3 = 0f;
                        }

                        bw[i].boneIndex0 = prim.BoneIndices[i * 4 + 0];
                        bw[i].weight0 = w0;
                        bw[i].boneIndex1 = prim.BoneIndices[i * 4 + 1];
                        bw[i].weight1 = w1;
                        bw[i].boneIndex2 = prim.BoneIndices[i * 4 + 2];
                        bw[i].weight2 = w2;
                        bw[i].boneIndex3 = prim.BoneIndices[i * 4 + 3];
                        bw[i].weight3 = w3;
                    }

                    var bindposes = new Matrix4x4[skin.Joints.Length];
                    var bones = new Transform[skin.Joints.Length];
                    for (int j = 0; j < skin.Joints.Length; j++)
                    {
                        var m = j < skin.InverseBind.Length ? skin.InverseBind[j] : System.Numerics.Matrix4x4.Identity;
                        bindposes[j] = ToUnity(m);
                        bones[j] = nodeTr[skin.Joints[j]];
                    }

                    mesh.boneWeights = bw;
                    mesh.bindposes = bindposes;
                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();

                    var smr = go.AddComponent<SkinnedMeshRenderer>();
                    smr.sharedMesh = mesh;
                    smr.bones = bones;
                    smr.rootBone = bones.Length > 0 ? bones[0] : null;
                    smr.sharedMaterial = mat;
                    smr.updateWhenOffscreen = true;
                    smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    // a deforming mesh shadowing itself gives acne that flickers while
                    // animating; the avatar still casts a shadow onto the world
                    smr.receiveShadows = false;
                    // BABBDI ships the Post Processing Stack: with the default "Camera"
                    // motion-vector mode a moving avatar leaves smeared trails, so give
                    // the TAA real motion vectors (object + per-bone if supported)
                    smr.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
                    smr.skinnedMotionVectors = true;
                }
                else
                {
                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();

                    var mf = go.AddComponent<MeshFilter>();
                    mf.sharedMesh = mesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = mat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    mr.receiveShadows = false;
                    mr.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
                }
            }

            // ---- animation ---------------------------------------------------------
            GlbAnimator animator = null;
            if (parsed.Animations.Count > 0 && nodeTr.Length > 0)
            {
                animator = root.AddComponent<GlbAnimator>();
                animator.Init(parsed, nodeTr);
            }

            // ---- hand anchor (item holder) -----------------------------------------
            Transform handBone = animator != null ? animator.HandBone : null;
            var anchor = new GameObject("HandAnchor");
            anchor.transform.SetParent(root.transform, false);
            var follow = anchor.AddComponent<HandFollow>();
            follow.Hand = handBone;
            follow.Elbow = animator != null ? animator.ForeArmBone : null;
            follow.AvatarRoot = root.transform; // item points along the avatar's forward
            if (animator != null) animator.HandFollowAnchor = follow; // lets the push pose aim the prop
            if (handBone != null)
            {
                // A prop has to be gripped by the FIST, not by the wrist joint. The anchor used
                // to sit exactly on mixamorig:LeftHand — the wrist of the SOLDIER's skeleton —
                // so the handle started at the cuff and the prop read as levitating next to the
                // hand (the villager's mitten is offset from that joint). The palm centre is
                // derived from the rig's own finger bones: half way from the wrist to the
                // knuckle line, expressed in the hand bone's frame so it is pose-independent.
                follow.Palm = ComputePalmOffset(handBone, root, out string palmReport, out int palmVerts);
                log?.LogInfo($"[GLB] palm centre (hand bone local)=({follow.Palm.x:F3},{follow.Palm.y:F3},{follow.Palm.z:F3}) " +
                             $"[{palmVerts}], {palmReport}");
            }
            if (handBone == null)
            {
                // static models have no bones — park a fixed anchor at hip height
                // (mirrored rig: the visually right hand is at -X)
                anchor.transform.localPosition = new Vector3(-0.3f, 1.05f, 0.35f);
            }

            // the pose we start in is the Idle clip's first frame, not the bind pose:
            // drop the model so the boots actually touch the ground (runs 3 frames later,
            // when the animator has posed the rig)
            root.AddComponent<FeetAligner>();
            FeetAligner.Log = log?.LogInfo;

            return root;
        }

        /// <summary>
        /// Centre of the fist in the hand bone's local space.
        /// Two independent estimates are logged: (a) half way from the wrist to the knuckle
        /// line (the rig's own finger bones), and (b) the centroid of the mesh vertices the
        /// hand bone dominates — the actual glove geometry, which for a re-rigged mesh need
        /// not agree with the skeleton. (b) is used when it looks like a hand.
        /// </summary>
        public static Vector3 ComputePalmOffset(Transform handBone, GameObject root,
                                               out string report, out int verts)
        {
            report = "no data";
            verts = 0;
            if (handBone == null) return Vector3.zero;

            // (a) skeleton: the five finger roots hang off the hand bone
            Vector3 bonePalm = Vector3.zero;
            int fingers = 0;
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < handBone.childCount; i++)
            {
                var c = handBone.GetChild(i);
                if (c == null) continue;
                sum += c.localPosition;
                fingers++;
            }
            float knuckle = 0f;
            if (fingers > 0)
            {
                Vector3 knuckles = sum / fingers;
                knuckle = knuckles.magnitude;
                bonePalm = knuckles * 0.5f;
            }
            float boneScale = Mathf.Abs(handBone.lossyScale.x);

            // (b) mesh: centroid of the hand-dominated vertices in bone space — the middle of
            // the glove the prop has to pass through (the bounding box lies for the mittens:
            // a few verts near the fingertips drag its centre to the very end of the hand).
            Vector3 meshPalm = Vector3.zero;
            int meshVerts = 0;
            float meshRadius = 0f;
            Vector3 lo = Vector3.positiveInfinity, hi = Vector3.negativeInfinity;
            if (root != null)
            {
                Vector3 acc = Vector3.zero;
                foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var mesh = smr.sharedMesh;
                    if (mesh == null) continue;
                    int bi = System.Array.IndexOf(smr.bones, handBone);
                    if (bi < 0) continue;
                    var v = mesh.vertices;
                    var bw = mesh.boneWeights;
                    var bp = mesh.bindposes;
                    if (v == null || bw == null || bp == null ||
                        bw.Length != v.Length || bp.Length <= bi) continue;
                    var bind = bp[bi];
                    for (int i = 0; i < v.Length; i++)
                    {
                        var w = bw[i];
                        float hw = 0f;
                        if (w.boneIndex0 == bi) hw += w.weight0;
                        if (w.boneIndex1 == bi) hw += w.weight1;
                        if (w.boneIndex2 == bi) hw += w.weight2;
                        if (w.boneIndex3 == bi) hw += w.weight3;
                        if (hw < 0.5f) continue;
                        var p = bind.MultiplyPoint3x4(v[i]);
                        acc += p;
                        lo = Vector3.Min(lo, p);
                        hi = Vector3.Max(hi, p);
                        meshVerts++;
                    }
                }
                if (meshVerts > 0)
                {
                    meshPalm = acc / meshVerts;
                    meshRadius = (hi - lo).magnitude;
                }
            }

            report = $"skeleton palm=({bonePalm.x:F2},{bonePalm.y:F2},{bonePalm.z:F2})/{fingers} fingers, " +
                     $"knuckle {knuckle * boneScale:F3} m; " +
                     $"mesh palm=({meshPalm.x:F2},{meshPalm.y:F2},{meshPalm.z:F2}) from {meshVerts} verts, " +
                     $"glove {meshRadius * boneScale:F3} m across, {(meshPalm - bonePalm).magnitude * boneScale:F3} m from the skeleton palm";

            // The mesh estimate is the one that puts the prop inside the visible glove; only
            // trust it inside a plausible hand-sized blob (< 0.25 m across in bone units).
            if (meshVerts >= 8)
            {
                verts = meshVerts;
                return meshPalm;
            }
            verts = -fingers;
            return bonePalm;
        }

        /// <summary>System.Numerics row-vector matrix → Unity column-vector matrix.</summary>
        private static Matrix4x4 ToUnity(System.Numerics.Matrix4x4 m)
        {
            // Unity's Matrix4x4(Vector4 col0, col1, col2, col3); the transpose of the
            // row-vector matrix means Unity column c = row c of m.
            return new Matrix4x4(
                new Vector4(m.M11, m.M12, m.M13, m.M14),
                new Vector4(m.M21, m.M22, m.M23, m.M24),
                new Vector4(m.M31, m.M32, m.M33, m.M34),
                new Vector4(m.M41, m.M42, m.M43, m.M44));
        }

        /// <summary>
        /// Puts the feet exactly on the avatar root's y=0 once the animator has posed the
        /// rig. The model is normalised using the BIND pose but spawned already posed on
        /// the Idle clip's first frame (knees bent), so the boots hovered a few cm above
        /// the floor ("модель левитирует"). Runs a few frames after spawn, measures the
        /// lowest skinned vertex in the real pose and shifts the whole model down; then
        /// removes itself.
        /// </summary>
        public class FeetAligner : MonoBehaviour
        {
            /// <summary>Set by Load(): routes the measurement into the BepInEx log.</summary>
            public static Action<string> Log;

            private int frames;

            private void LateUpdate()
            {
                frames++;
                if (transform.parent == null || frames < 3) return;

                float minY = float.MaxValue;
                foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (smr == null || smr.sharedMesh == null) continue;
                    var baked = new Mesh();
                    try
                    {
                        smr.BakeMesh(baked);
                        var v = baked.vertices;
                        var m = smr.transform.localToWorldMatrix;
                        int step = Mathf.Max(1, v.Length / 4000);
                        for (int i = 0; i < v.Length; i += step)
                        {
                            float y = transform.parent.InverseTransformPoint(m.MultiplyPoint3x4(v[i])).y;
                            if (y < minY) minY = y;
                        }
                    }
                    catch { }
                    finally { UnityEngine.Object.Destroy(baked); }
                }

                if (minY != float.MaxValue)
                {
                    var lp = transform.localPosition;
                    transform.localPosition = new Vector3(lp.x, lp.y - minY, lp.z);
                    string msg = $"[GLB] feet on ground: lowest vertex was {minY:F3} m above the feet point → model shifted down";
                    if (Log != null) Log(msg); else Debug.Log(msg);
                }
                UnityEngine.Object.Destroy(this);
            }
        }

        public class LogStub
        {
            public Action<string> LogWarning;
            public Action<string> LogInfo;
        }
    }

    /// <summary>
    /// Glues the item anchor to a hand bone: position = the hand, orientation = "out of
    /// the fist, angled forward-down" (the forearm direction blended with the avatar's
    /// forward). This is how a person carries a club — and because the forearm drives
    /// it, a swing rotates the prop on ALL axes with the arm instead of leaving it
    /// level. Following the wrist bone instead made the bat point straight up whenever
    /// the walk/run clip rolled the wrist (unrealistic), hence the arm-based aim.
    /// </summary>
    public class HandFollow : MonoBehaviour
    {
        public Transform Hand;
        public Transform Elbow;      // lower-arm bone: gives the arm direction
        public Transform AvatarRoot;

        /// <summary>
        /// Centre of the fist in the hand bone's local space (measured from the skinned mesh
        /// at load). The anchor sits there, not on the wrist joint, so the prop's handle
        /// actually passes through the closed hand.
        /// </summary>
        public Vector3 Palm;

        /// <summary>
        /// 0 = carried up out of the fist, 1 = thrust straight forward (item use).
        /// Driven by GlbAnimator while the avatar pushes the prop away from the chest.
        /// </summary>
        public float Push;

        private bool initialized;

        private void LateUpdate()
        {
            if (Hand == null) return;
            float dt = Mathf.Max(0f, Time.deltaTime);

            // During the item push the prop has to follow a ~0.1 s jab, so the damping is
            // switched off for its duration; otherwise it keeps the slow carry smoothing.
            float push = Mathf.Clamp01(Push);

            // The fist bobs a lot while running; following it 1:1 made the prop flail.
            // A short position lag plus a rotation speed cap keeps it readable.
            Vector3 palm = Hand.TransformPoint(Palm);
            if (!initialized)
            {
                transform.position = palm;
                initialized = true;
            }
            else
            {
                float kp = 1f - Mathf.Exp(-dt / Mathf.Lerp(0.06f, 0.008f, push));
                transform.position = Vector3.Lerp(transform.position, palm, kp);
            }

            Vector3 fwd = AvatarRoot != null ? AvatarRoot.forward : transform.forward;
            Vector3 flatFwd = new Vector3(fwd.x, 0f, fwd.z);
            if (flatFwd.sqrMagnitude < 1e-4f) flatFwd = fwd;
            flatFwd.Normalize();

            Vector3 dir = Elbow != null ? palm - Elbow.position : fwd;
            if (dir.sqrMagnitude < 1e-6f) dir = fwd;
            dir.Normalize();

            // A club is carried pointing UP out of the fist: with the arm hanging down
            // (-dir = up) the prop stands up and tilts a little forward. The arm still
            // pulls on it, so the swing sweeps the prop from vertical to horizontal
            // instead of leaving it stiff, but the explicit `up` term keeps it from
            // dangling below a raised fist.
            //
            // v0.9.7: the horizontal component is removed from -dir BEFORE it is mixed in.
            // With the arm extended forward -dir points backwards and cancelled the explicit
            // `forward` term, so the aim collapsed to straight up — the bat stood vertically
            // beside a horizontal arm (a "floating" look on the user's screenshot). Now an
            // extended arm carries the prop forward, a hanging arm still stands it up.
            Vector3 carryUp = -dir;
            carryUp -= flatFwd * Vector3.Dot(carryUp, flatFwd);
            // ...and the carry is mostly FORWARD now (v0.9.7): with the fist at the hip a
            // steep up-aim sent the shaft back across the forearm, so the prop looked
            // speared through the wrist instead of held. ~30° above horizontal out of the
            // fist reads as a club carried in the hand and still sweeps to horizontal on use.
            Vector3 carry = carryUp * 0.40f + Vector3.up * 0.12f + flatFwd * 0.95f;

            // pushing the item: the prop turns from "up out of the fist" to straight ahead
            Vector3 aim = Vector3.Slerp(carry.sqrMagnitude > 1e-6f ? carry.normalized : fwd,
                                        flatFwd, Mathf.Clamp01(Push));
            if (aim.sqrMagnitude < 1e-6f) aim = fwd;
            aim.Normalize();

            Vector3 up = AvatarRoot != null ? AvatarRoot.up : Vector3.up;
            // the prop is round, so the roll around its own axis does not matter
            Quaternion target = Quaternion.LookRotation(aim, up);
            float k = 1f - Mathf.Exp(-dt / Mathf.Lerp(0.12f, 0.01f, push));
            Quaternion smoothed = Quaternion.Slerp(transform.rotation, target, k);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, smoothed,
                                                          Mathf.Lerp(240f, 4000f, push) * dt);
        }
    }
}
