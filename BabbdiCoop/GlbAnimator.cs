using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BabbdiCoop
{
    /// <summary>
    /// Runtime skeletal animation for the GLB avatar: samples the clips baked into
    /// the file (Idle/Walk/Run), blends them by movement speed and tilts the head
    /// with the remote player's camera pitch.
    ///
    /// Sampling supports LINEAR / STEP / CUBICSPLINE (cubic uses keyframe values
    /// only, tangents are ignored — visually fine at 30 fps clips).
    /// </summary>
    public class GlbAnimator : MonoBehaviour
    {
        private class ClipState
        {
            public GlbModelLoader.AnimationInfo Info;
            public Dictionary<int, GlbModelLoader.AnimChannel> Transl = new Dictionary<int, GlbModelLoader.AnimChannel>();
            public Dictionary<int, GlbModelLoader.AnimChannel> Rot = new Dictionary<int, GlbModelLoader.AnimChannel>();
            public Dictionary<int, GlbModelLoader.AnimChannel> Scale = new Dictionary<int, GlbModelLoader.AnimChannel>();
            public float Time;
        }

        private GlbModelLoader.NodeInfo[] restNodes;
        private Transform[] boneTransforms;
        private readonly List<ClipState> clips = new List<ClipState>();
        private ClipState idle, walk, run;

        private int headIndex = -1;
        private float speed;      // m/s, fed by the plugin from packet position deltas
        private float pitchDeg;   // remote camera pitch, + = looking down

        // v0.7.0 remote state (packet flags)
        private bool crouching, airborne;
        private float crouchBlend, airBlend;   // smoothed 0..1
        private float swingStart = -1f;        // Time.time when the current item push began
        private bool lastUseFlag;

        // procedural pose bones
        private Transform hips, spine, spine1, lUpLeg, lLeg, lFoot, rUpLeg, rLeg, rFoot, rArm, rForeArm, lArm, lForeArm;

        // v0.10.5: the remote player at the air-hockey table. The viewer feeds the partner's
        // stance/hand target every frame from AirHockeyGame; the pose is applied after the normal
        // gait layers (legs apart, knees bent, hunched, arm/body following the mallet).
        private bool miniActive;
        private float miniBlend;
        private Vector3 miniHand;
        private Transform rHand;

        // v0.8.1 tricks (packet flag bits 3 and 4) — explicit timelines, not a blend of the
        // raw flag. The handstand has to be ENTERED (load → push → hands take the weight →
        // hold → push off) and the backflip is a load → air → landing arc, otherwise the
        // body is simply rotated by an invisible hand.
        private bool handstandFlag, lastFlipFlag;
        private bool hsActive;                 // entry / hold / exit in progress
        private float hsT;                     // seconds into the entry
        private float hsOutT;                  // seconds into the exit (after the flag dropped)
        private float hsProgress;              // 0 = feet down … 1 = inverted, on the hands
        private float flipT0 = -1f;            // Time.time the current backflip started (-1 = idle)
        private float trickWeight;             // 0..1 how much the trick owns the pose
        private float hipLocalY = 1.05f;       // hips height in avatar space
        private float comLocalY = 0.95f;       // centre of mass (flip pivot), measured upright
        private bool pivotMeasured;
        private Vector3 basePos;               // rest local transform of the root, captured once
        private Quaternion baseRot = Quaternion.identity;
        private bool baseCaptured;
        private bool tricksWereActive;
        public const float HsEnter = 0.95f;    // crouch → push → settle onto the hands
        public const float HsExit = 0.55f;     // push off, legs come back down
        public const float FlipIn = 0.16f;     // load
        public const float FlipAir = 0.72f;    // rotation + jump arc
        public const float FlipOut = 0.20f;    // landing absorb
        public const float FlipDuration = FlipIn + FlipAir + FlipOut;

        private readonly float[] tmp = new float[4];

        private struct Blend
        {
            public ClipState Clip;
            public float Weight;
            public float Time;
        }

        private readonly List<Blend> active = new List<Blend>(3);
        private float gaitPhase; // 0..1 shared by Walk and Run so they cross-fade in step
        private float gaitDir = 1f;      // +1 walking forward, -1 backing up (clip rewinds)
        private float lateralBlend;      // -1 strafing to the avatar's left, +1 to its right
        // v0.7.8: the walk/run weights are rate-limited. They used to jump straight to
        // the value implied by the current speed, so a speed estimate that wobbled on
        // the 2.2 m/s threshold flickered the whole pose between two clips.
        private float curWalk, curRun;

        /// <summary>Debug (preview mode): log the blend state every few frames.</summary>
        public static bool Verbose;
        private int dbgFrame;

        /// <summary>The prop's anchor, so the push pose can aim the item forward.</summary>
        public HandFollow HandFollowAnchor;

        /// <summary>
        /// Gait playback rate. 1 = the clip advances at the speed the movement implies.
        /// v0.7.7: run sped up 80 % over the v0.7.6 value (0.34 → 0.61), walk stays 0.5.
        /// </summary>
        public float GaitScaleWalk = 0.5f;
        public float GaitScaleRun = 0.61f;

        /// <summary>How much faster than the 0.75 s v0.7.6 push the jab plays (8 = 0.094 s).</summary>
        public static float PushSpeed = 8f;

        public Transform HandBone { get; private set; }

        /// <summary>Lower-arm bone feeding the hand (grip direction for held props).</summary>
        public Transform ForeArmBone { get; private set; }

        public void Init(GlbModelLoader.Parsed parsed, Transform[] nodeTransforms)
        {
            restNodes = parsed.Nodes.ToArray();
            boneTransforms = nodeTransforms;
            foreach (var anim in parsed.Animations)
            {
                var st = new ClipState { Info = anim };
                foreach (var ch in anim.Channels)
                {
                    if (ch.Path == "translation") st.Transl[ch.Node] = ch;
                    else if (ch.Path == "rotation") st.Rot[ch.Node] = ch;
                    else if (ch.Path == "scale") st.Scale[ch.Node] = ch;
                }
                clips.Add(st);

                if (anim.Name == "Idle") idle = st;
                else if (anim.Name == "Walk") walk = st;
                else if (anim.Name == "Run") run = st;
            }

            // fallback assignments if names differ
            if (idle == null && clips.Count > 0) idle = clips[0];
            if (walk == null) walk = idle;
            if (run == null) run = walk;

            // Bones of interest: the hand that carries items and the head (camera pitch).
            // NOTE: glTF is right-handed and we push its positions straight into Unity's
            // left-handed space, so the rendered model is a MIRROR image: the skeleton's
            // "LeftHand"/"LeftArm" sit on the side a viewer reads as the RIGHT hand
            // (verified in-game: RightHand bone at local x = +0.33 while facing -Z).
            // Everything hand-dependent therefore uses the LEFT bones.
            for (int i = 0; i < restNodes.Length; i++)
            {
                var n = restNodes[i] != null ? restNodes[i].Name : null;
                if (string.IsNullOrEmpty(n)) continue;
                if (HandBone == null && n.EndsWith("LeftHand")) HandBone = nodeTransforms[i];
                if (rHand == null && n.EndsWith("RightHand")) rHand = nodeTransforms[i];
                if (headIndex < 0 && (n.EndsWith(":Head") || n == "Head")) headIndex = i;

                // procedural pose bones (mixamo naming from Soldier.glb)
                if (n.EndsWith("Hips")) hips = nodeTransforms[i];
                else if (n.EndsWith("Spine2")) spine = nodeTransforms[i];
                else if (n.EndsWith("Spine1")) spine1 = nodeTransforms[i];
                else if (n.EndsWith("LeftUpLeg")) lUpLeg = nodeTransforms[i];
                else if (n.EndsWith("LeftLeg")) lLeg = nodeTransforms[i];
                else if (n.EndsWith("LeftFoot")) lFoot = nodeTransforms[i];
                else if (n.EndsWith("RightUpLeg")) rUpLeg = nodeTransforms[i];
                else if (n.EndsWith("RightLeg")) rLeg = nodeTransforms[i];
                else if (n.EndsWith("RightFoot")) rFoot = nodeTransforms[i];
                else if (n.EndsWith("RightArm")) rArm = nodeTransforms[i];
                else if (n.EndsWith("RightForeArm")) rForeArm = nodeTransforms[i];
                else if (n.EndsWith("LeftArm")) lArm = nodeTransforms[i];
                else if (n.EndsWith("LeftForeArm")) lForeArm = nodeTransforms[i];
            }
            if (spine == null) spine = rArm; // degrade gracefully on other rigs
            ForeArmBone = lForeArm != null ? lForeArm : rForeArm;
        }

        /// <summary>
        /// Remote player state from the network flags. "use" is level-triggered:
        /// a rising edge starts one procedural swing of the right arm.
        /// </summary>
        public void SetState(bool isCrouching, bool isAirborne, bool useHeldItem)
        {
            SetState(isCrouching, isAirborne, useHeldItem, false, false);
        }

        /// <summary>
        /// Remote player state from the network flags. "use" is level-triggered: a rising
        /// edge starts one procedural swing. v0.8.0 adds the tricks: handstand is a held
        /// state, backflip is level-triggered for ~1 s on the sender and starts once here.
        /// </summary>
        public void SetState(bool isCrouching, bool isAirborne, bool useHeldItem,
                             bool isHandstand, bool isFlip)
        {
            crouching = isCrouching;
            airborne = isAirborne;
            if (useHeldItem && !lastUseFlag) swingStart = Time.time;
            lastUseFlag = useHeldItem;

            handstandFlag = isHandstand;
            if (isFlip && !lastFlipFlag) flipT0 = Time.time;   // rising edge starts one flip
            lastFlipFlag = isFlip;
        }

        /// <summary>Called by the plugin every frame with the remote player's motion.</summary>
        /// <summary>v0.10.5 — the partner is standing at the air-hockey table; the hand target is
        /// the mini-game mallet in world space. `active=false` blends the stance back out.</summary>
        public void SetMiniPose(bool active, Vector3 handTarget)
        {
            miniActive = active;
            miniHand = handTarget;
        }

        public void SetMotion(float speedMps, float pitchDegrees)
        {
            SetMotion(speedMps, pitchDegrees, Vector3.zero);
        }

        /// <summary>
        /// Same, plus the velocity in avatar space (x = right, z = forward, m/s). Needed to
        /// play the gait BACKWARDS when the partner backs up and to add a side-step when
        /// they strafe — playing the forward walk in both cases is the "лунная походка".
        /// </summary>
        public void SetMotion(float speedMps, float pitchDegrees, Vector3 localVelocity)
        {
            speed = Mathf.Clamp(speedMps, 0f, 8f);
            pitchDeg = Mathf.Clamp(pitchDegrees, -75f, 75f);

            float mag = new Vector2(localVelocity.x, localVelocity.z).magnitude;
            float targetLat = (mag > 0.2f && speed > 0.15f) ? Mathf.Clamp(localVelocity.x / mag, -1f, 1f) : 0f;
            float targetDir = (mag > 0.2f && speed > 0.15f && localVelocity.z < 0f) ? -1f : 1f;
            float k = 1f - Mathf.Exp(-Mathf.Max(0.0001f, Time.deltaTime) * 8f);
            lateralBlend = Mathf.Lerp(lateralBlend, targetLat, k);
            // NOTE: no lower clamp on |gaitDir| — pinning it above zero made the sign flip
            // impossible (the gait slowed down to 0.15 and never reversed, so backing up
            // still played a forward cycle). It may pass through 0 for a moment: the legs
            // stop and then step the other way, which is what a person does.
            gaitDir = Mathf.Lerp(gaitDir, targetDir, k);
        }

        private void Update()
        {
            if (boneTransforms == null || boneTransforms.Length == 0) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // The rest pose of the root is captured ONCE, on the very first frame (the
            // avatar is built at local 0 / identity). v0.8.0 re-captured it every idle
            // frame, so any residue of a trick that was interrupted (e.g. the partner
            // landing while the trick played) became the new "rest" — that is why the
            // model ended up standing crooked after a fall.
            if (!baseCaptured)
            {
                basePos = transform.localPosition;
                baseRot = transform.localRotation;
                baseCaptured = true;
            }
            UpdateTrickTimers(dt);

            // While a trick runs it OWNS the body: the gait, the crouch/air layers and the
            // camera-driven lean are faded out. Stacked on a rotated root they folded the
            // model in half mid-flip ("выворачивается"), and a crouch shoved the hips 40 cm
            // in world space while the body was inverted.
            float leanK = 1f - trickWeight;
            float speedEff = speed * leanK;

            // walk 0.15..1.6 m/s, run 2.2..4.5 m/s (BABBDI run speed is around 4)
            float walkT = Mathf.Clamp01((speedEff - 0.15f) / 1.45f);
            float runT = Mathf.Clamp01((speedEff - 2.2f) / 2.3f);

            float wIdle, wWalk, wRun;
            if (runT > 0f) { wRun = runT; wWalk = 1f - runT; wIdle = 0f; }
            else { wWalk = walkT; wIdle = 1f - walkT; wRun = 0f; }

            // v0.7.8: rate-limit the cross-fade (≈0.3 s to swing 0→1) so the pose follows
            // the average speed instead of every wobble of the estimate.
            float step = dt * 3.5f;
            curWalk = Mathf.MoveTowards(curWalk, wWalk, step);
            curRun = Mathf.MoveTowards(curRun, wRun, step);
            if (curWalk + curRun > 1f)
            {
                float sum = curWalk + curRun;
                curWalk /= sum;
                curRun /= sum;
            }
            wWalk = curWalk;
            wRun = curRun;
            wIdle = 1f - curWalk - curRun;
            runT = curRun;   // cadence/stride follow the same smoothed weight

            // ONE shared gait phase drives both Walk and Run. Blending two gaits that
            // are out of phase used to average limbs from different steps (arms in the
            // chest, legs sliding); with a common phase the cross-fade stays coherent.
            float strideLen = Mathf.Lerp(1.45f, 2.6f, runT);            // meters per cycle
            float cadence = speedEff > 0.05f ? speedEff / strideLen : 0f; // cycles per second
            cadence *= Mathf.Lerp(GaitScaleWalk, GaitScaleRun, runT);     // v0.7.6: gait slowed
            // backing up → the same clip rewinds, so the legs step backwards
            gaitPhase = Mathf.Repeat(gaitPhase + dt * cadence * gaitDir, 1f);

            Advance(idle, dt, true);

            active.Clear();
            if (idle != null && wIdle > 0.001f)
                active.Add(new Blend { Clip = idle, Weight = wIdle, Time = idle.Time });
            if (walk != null && wWalk > 0.001f)
                active.Add(new Blend { Clip = walk, Weight = wWalk, Time = gaitPhase * walk.Info.Duration });
            if (run != null && wRun > 0.001f)
                active.Add(new Blend { Clip = run, Weight = wRun, Time = gaitPhase * run.Info.Duration });

            Vector3 headRight = transform.right;
            headRight.y = 0f;
            if (headRight.sqrMagnitude < 0.0001f) headRight = Vector3.right;
            headRight.Normalize();

            for (int i = 0; i < boneTransforms.Length; i++)
            {
                var tr = boneTransforms[i];
                if (tr == null || restNodes[i] == null) continue;

                var rest = restNodes[i];
                Vector3 p = Vector3.zero, s = Vector3.zero;
                Quaternion q = Quaternion.identity;
                float total = 0f;

                foreach (var blend in active)
                {
                    float w = blend.Weight;
                    SamplePose(blend.Clip, blend.Time, i, out var cp, out var cq, out var cs);
                    p += cp * w;
                    s += cs * w;
                    if (total <= 0f) q = cq;
                    else q = Quaternion.Slerp(q, cq, w / (total + w));
                    total += w;
                }

                if (total <= 0f)
                {
                    p = ToUnity(rest.T); q = ToUnity(rest.R); s = ToUnity(rest.S);
                }
                else
                {
                    p /= total;
                    s /= total;
                }

                tr.localPosition = p;
                tr.localRotation = q;
                tr.localScale = s;
            }

            // head + torso pitch: rotate in WORLD space (after the skeleton update), so it
            // does not depend on the bone's unknown local axis orientation.
            // v0.8.0: the torso follows the camera too — the head alone read as "the body is
            // a statue and the head is on a stick". The lean is split over the spine so the
            // bend looks like a spine and not like a hinge at the neck (≈0.32 + 0.12 of the
            // pitch, head keeps 0.45; the total roughly matches the camera angle).
            if (Mathf.Abs(pitchDeg) > 1f && leanK > 0.01f)
            {
                float lean = pitchDeg * leanK;
                if (headIndex >= 0 && headIndex < boneTransforms.Length && boneTransforms[headIndex] != null)
                {
                    var head = boneTransforms[headIndex];
                    head.rotation = Quaternion.AngleAxis(lean * 0.45f, headRight) * head.rotation;
                }
                Twist(spine, lean * 0.32f, headRight);
                Twist(spine1, lean * 0.12f, headRight);
            }

            // ---- procedural layers: crouch / airborne / item swing (v0.7.0) ----
            // All of them rotate bones in WORLD space after the clip pose is applied,
            // so they never depend on the unknown local axes of the mixamo rig.
            Vector3 right = transform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
            right.Normalize();

            crouchBlend = Mathf.MoveTowards(crouchBlend, crouching ? 1f : 0f, dt * 7f);
            airBlend = Mathf.MoveTowards(airBlend, airborne ? 1f : 0f, dt * 6f);

            if (crouchBlend > 0.002f && leanK > 0.01f) ApplyCrouch(crouchBlend * leanK, right);
            if (airBlend > 0.002f && leanK > 0.01f) ApplyAir(airBlend * leanK, right);
            ApplyLateral(right, leanK);
            ApplySwing(right, leanK);
            ApplyTricks(right);

            // v0.10.5: the air-hockey stance/arm on top of the normal pose. `leanK` is one only when
            // no trick is running; the mini pose owns the body while the partner is at the table.
            miniBlend = Mathf.MoveTowards(miniBlend, miniActive ? 1f : 0f, dt * 6f);
            if (miniBlend > 0.002f) ApplyMiniPose(right, miniBlend);

            if (Verbose && (++dbgFrame % 15) == 0)
            {
                string act = "";
                foreach (var b in active) act += $"{b.Clip.Info.Name}:{b.Weight:F2}@{b.Time:F2}s ";
                UnityEngine.Debug.Log($"[ANIM] speed={speed:F2} phase={gaitPhase:F2} active=[{act}] " +
                                      $"use={lastUseFlag} swingAt={swingStart:F2} now={Time.time:F2} rArm={(rArm != null)} " +
                                      $"hipsLocal={hips?.localPosition} lThigh={lUpLeg?.localRotation.eulerAngles}");
            }
        }

        /// <summary>
        /// Advances the trick clocks. Kept apart from ApplyTricks because the other pose
        /// layers (gait, crouch/air, camera lean) need to know how much the trick owns the
        /// body BEFORE the skeleton is posed.
        /// </summary>
        private void UpdateTrickTimers(float dt)
        {
            if (handstandFlag)
            {
                hsActive = true;
                hsOutT = 0f;
                hsT = Mathf.Min(hsT + dt, HsEnter);
                hsProgress = Mathf.Clamp01(hsT / HsEnter);
            }
            else if (hsActive)
            {
                // released: push off and come back down on the feet
                hsT = 0f;
                hsOutT += dt;
                if (hsOutT >= HsExit)
                {
                    hsActive = false;
                    hsOutT = 0f;
                    hsProgress = 0f;
                }
                else hsProgress = Mathf.Clamp01(1f - hsOutT / HsExit);
            }

            if (flipT0 >= 0f && Time.time - flipT0 > FlipDuration) flipT0 = -1f;

            float target = (hsActive || flipT0 >= 0f) ? 1f : 0f;
            trickWeight = Mathf.MoveTowards(trickWeight, target, dt * 9f);
            if (target > 0f) tricksWereActive = true;
        }

        /// <summary>
        /// Handstand and backflip (v0.8.1). Both are a rigid ROOT rotation stacked on a
        /// body-space pose, which is why they run after the skeleton is posed.
        ///
        ///   handstand: load → push → the hands take the weight → hold → push off. The body
        ///              rolls 180° about its forward axis and the root is raised every frame
        ///              until the lowest hand touches the floor, so the arms carry it.
        ///   backflip:  load → jump + 360° about the CENTRE OF MASS (the body turns around
        ///              itself instead of around its feet) → land and absorb. The pose is a
        ///              tuck, so nothing sticks out of the rotation circle.
        /// </summary>
        private void ApplyTricks(Vector3 right)
        {
            // ---- 0. idle: park the root EXACTLY on its rest transform. v0.8.0 only
            // captured the rest pose while a trick happened to be idle, so a trick that was
            // interrupted (e.g. the partner landed from a height mid-animation) left its
            // rotation and height as the new "rest" — the model stood crooked afterwards.
            if (!hsActive && flipT0 < 0f && trickWeight <= 0.0001f)
            {
                transform.localRotation = baseRot;
                transform.localPosition = basePos;
                if (tricksWereActive)
                {
                    tricksWereActive = false;
                    if (Verbose)
                        UnityEngine.Debug.Log($"[TRICK] back to rest: localPos={transform.localPosition} base={basePos} " +
                                              $"rot={transform.localRotation.eulerAngles} baseRot={baseRot.eulerAngles}");
                }
                // the pivots are only measurable with the body upright
                if (!pivotMeasured && hips != null)
                {
                    hipLocalY = Mathf.Clamp(hips.position.y - transform.position.y, 0.6f, 1.5f);
                    comLocalY = hipLocalY * 0.90f;
                    pivotMeasured = true;
                }
                return;
            }

            // The trick poses are written in the UPRIGHT rest frame (arms up = +Y), and the
            // previous frame left the root rotated. Park it first, otherwise every world-space
            // helper below (AlignLimb → Vector3.up) works in a rotated frame and undoes the
            // pose — that is why the arms hung down in the first v0.8.1 handstand.
            transform.localRotation = baseRot;
            transform.localPosition = basePos;
            right = transform.right;

            float rollDeg = 0f;
            Vector3 off = Vector3.zero;
            if (hsActive) ApplyHandstandPose(right, out rollDeg, out off);

            // ---- backflip (one shot, started on the flag's rising edge)
            if (flipT0 >= 0f) ApplyFlipPose(right);

            // ---- root: one composition of the handstand roll and the flip spin.
            // Both turn about the BODY's own right axis. v0.8.1 spun the handstand about the
            // local Z (= the facing axis → a cartwheel to the side, the user's "выходит
            // через влево") and the flip about WORLD X — but the avatar keeps any yaw, so
            // that tumble left the sagittal plane and the body visibly changed direction at
            // the half-turn. +angle about `right` = the head goes toward transform.forward.
            Quaternion parentRot = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
            Vector3 bodyRight = Quaternion.Inverse(parentRot) * right;
            Vector3 com = Vector3.up * comLocalY;
            Quaternion qTot = Quaternion.AngleAxis(rollDeg, bodyRight) * baseRot;

            if (flipT0 >= 0f)
            {
                float ft = Time.time - flipT0;
                float q = Mathf.Clamp01((ft - FlipIn) / FlipAir);
                // negative = backwards; RotationCurve keeps the speed up through the second half
                var fq = Quaternion.AngleAxis(-360f * RotationCurve(q), bodyRight);
                qTot = fq * qTot;
                // hold the centre of mass in place, so the body turns around itself…
                off += com - qTot * com;
                // …and a jump arc is what carries it up and back down
                off += Vector3.up * (0.62f * Mathf.Sin(Mathf.PI * q));
            }

            transform.localRotation = qTot;
            transform.localPosition = basePos + off;

            // safety net: no part of the body may sink through the floor mid-flip
            if (flipT0 >= 0f) FloorGuard();
        }

        /// <summary>
        /// Handstand, as a timeline of the body getting there by itself: load, jump, the body
        /// turns around its centre of mass, the hands take the weight at the end, hold with a
        /// slow balance sway, then (on release) push off and come back down on the feet.
        /// Sets <paramref name="rollDeg"/> (0 = feet down, 180 = inverted) and
        /// <paramref name="off"/> (root offset: COM-relative rotation + the hop).
        /// </summary>
        private void ApplyHandstandPose(Vector3 right, out float rollDeg, out Vector3 off)
        {
            float hs = Smooth01(hsProgress);

            // ---- load: dip, knees bend, weight goes over the hands (and the same crouch
            // shows up again at the end of the exit, i.e. the landing)
            float load = Mathf.Clamp01(hsProgress / 0.10f) * Mathf.Clamp01((0.30f - hsProgress) / 0.20f);
            if (load > 0.002f)
            {
                if (hips != null) hips.position += Vector3.down * (0.22f * load);
                Twist(lUpLeg, -48f * load, right);
                Twist(rUpLeg, -48f * load, right);
                Twist(lLeg, 88f * load, right);
                Twist(rLeg, 88f * load, right);
                Twist(lFoot, -40f * load, right);
                Twist(rFoot, -40f * load, right);
                Twist(spine, 20f * load, right);
            }

            // ---- body axis first: the hips are the root of the skeleton, so straightening
            // hips→chest onto the world vertical is a rigid "stand up straight" for the whole
            // body. The idle clip carries a soft spine, and inverted that read as a body
            // leaning 10-12° off the vertical (preview 11.10.2026). Every limb below is
            // aligned after this, so nothing is dragged out of place by the correction.
            float arch = Smooth01(hs);
            if (arch > 0.05f && hips != null && spine != null)
            {
                Vector3 torso = spine.position - hips.position;
                if (torso.sqrMagnitude > 0.001f)
                {
                    Quaternion q = Quaternion.FromToRotation(torso.normalized, Vector3.up);
                    hips.rotation = Quaternion.Slerp(Quaternion.identity, q, arch) * hips.rotation;
                }
            }
            Twist(spine, -6f * arch, right);
            Twist(spine1, -3f * arch, right);

            // ---- arms overhead early on purpose: the hands are what the body pivots around
            float pose = Smooth01(Mathf.Clamp01(hsProgress * 1.7f));
            Twist(lArm, 172f * pose, right);
            Twist(rArm, 172f * pose, right);
            if (pose > 0.05f)
            {
                // a fixed angle cannot stand the arms up: the clip leaves them out, and the
                // rig is a mirror image — solve it from the skeleton instead
                AlignLimb(lArm, lForeArm, HandBone, pose);
                AlignLimb(rArm, rForeArm, rHand, pose);
            }

            // ---- inverted body: slight arch, toes pointed. The legs are ALIGNED to the torso
            // axis instead of nudged by a fixed angle: the clip leaves them slightly bent, and
            // inverted that reads as a broken "hockey stick" (verified in the preview).
            if (arch > 0.05f)
            {
                AlignLimb(lUpLeg, lLeg, lFoot, Vector3.down, arch);
                AlignLimb(rUpLeg, rLeg, rFoot, Vector3.down, arch);
            }
            Twist(lFoot, 22f * arch, right);
            Twist(rFoot, -22f * arch, right);

            // ---- balance: a handstand is never perfectly still
            if (handstandFlag && hsT >= HsEnter * 0.7f)
            {
                float w = Mathf.Clamp01((hsT - HsEnter * 0.7f) / 0.5f);
                float t = Time.time;
                Twist(spine, 1.8f * w * Mathf.Sin(t * 1.55f), right);
                Twist(lUpLeg, 2.2f * w * Mathf.Sin(t * 1.05f + 1.2f), right);
                Twist(rUpLeg, -2.2f * w * Mathf.Sin(t * 1.05f + 1.2f), right);
                Vector3 fwd = Vector3.Cross(right, Vector3.up);
                if (hips != null && fwd.sqrMagnitude > 0.0001f)
                    hips.position += fwd.normalized * (0.012f * w * Mathf.Sin(t * 1.15f + 0.7f));
            }

            // ---- root roll with a touch of overshoot: momentum, not a lever
            rollDeg = 180f * hs + 6f * Mathf.Sin(Mathf.PI * hs);

            // ---- the JUMP. The body turns around its centre of mass (same as the flip) and
            // only comes down onto the hands at the end of the move. v0.8.0 solved for "the
            // lowest hand is on the floor" on every frame, so the body was dragged up by the
            // ankles as the roll passed the horizontal — the invisible force. With the COM
            // path the height is smooth and the move reads as a real jump onto the hands.
            var com = Vector3.up * comLocalY;
            Quaternion pRot = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
            Vector3 bRight = Quaternion.Inverse(pRot) * right;
            var qFin = Quaternion.AngleAxis(180f, bRight) * baseRot;
            transform.localRotation = qFin;
            transform.localPosition = basePos + (com - qFin * com);
            float groundY = transform.parent != null ? transform.parent.position.y : transform.position.y;
            float liftEnd = Mathf.Clamp(groundY - LowestHandY() + 0.02f, 0f, 4f);

            var qNow = Quaternion.AngleAxis(rollDeg, bRight) * baseRot;
            float rise = Smooth01(Mathf.Clamp01((hsProgress - 0.10f) / 0.62f));
            float bump = 0.22f * Mathf.Sin(Mathf.PI * Mathf.Clamp01((hsProgress - 0.06f) / 0.78f));
            off = (com - qNow * com) + Vector3.up * (liftEnd * rise + bump);
        }

        /// <summary>
        /// Backflip: load → jump + rotate → land and absorb, with a tuck that keeps every
        /// limb inside the rotation circle (v0.8.0 rotated a stretched body, so the head
        /// dove through the floor in the second half).
        /// </summary>
        private void ApplyFlipPose(Vector3 right)
        {
            float ft = Time.time - flipT0;
            float q = Mathf.Clamp01((ft - FlipIn) / FlipAir);

            // ---- load before the jump, and the same deep crouch again on landing
            float loadIn = Mathf.Clamp01(ft / FlipIn);
            float loadOut = Mathf.Clamp01((ft - FlipIn - FlipAir) / FlipOut);
            float load = Mathf.Max(
                Mathf.Sin(Mathf.PI * 0.5f * loadIn) * Mathf.Clamp01((FlipIn + 0.08f - ft) / 0.12f),
                Mathf.Sin(Mathf.PI * 0.5f * loadOut) * (1f - loadOut * 0.5f));
            if (load > 0.002f)
            {
                if (hips != null) hips.position += Vector3.down * (0.18f * load);
                Twist(lUpLeg, -50f * load, right);
                Twist(rUpLeg, -50f * load, right);
                Twist(lLeg, 92f * load, right);
                Twist(rLeg, 92f * load, right);
                Twist(lFoot, -42f * load, right);
                Twist(rFoot, -42f * load, right);
                Twist(spine, 16f * load, right);
                Twist(lArm, 34f * load, right);   // arms swing back before the jump
                Twist(rArm, 34f * load, right);
            }

            // ---- tuck in the air
            float tuck = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * q)), 0.7f);
            if (tuck > 0.002f)
            {
                Twist(lUpLeg, -98f * tuck, right);
                Twist(rUpLeg, -94f * tuck, right);
                Twist(lLeg, 122f * tuck, right);
                Twist(rLeg, 120f * tuck, right);
                Twist(lFoot, -28f * tuck, right);
                Twist(rFoot, -28f * tuck, right);
                Twist(spine, 30f * tuck, right);
                Twist(spine1, 12f * tuck, right);
                Twist(lArm, 55f * tuck, right);   // arms hug the knees
                Twist(rArm, 55f * tuck, right);
                if (headIndex >= 0 && headIndex < boneTransforms.Length && boneTransforms[headIndex] != null)
                    Twist(boneTransforms[headIndex], -22f * tuck, right);   // chin to the chest
            }
        }

        /// <summary>Rotation progress of the flip: short ease at both ends, near constant
        /// in the middle — the second half keeps its momentum instead of hanging.</summary>
        public static float RotationCurve(float x)
        {
            float t = Mathf.Clamp01((x - 0.06f) / 0.88f);
            return t * t * (3f - 2f * t);
        }

        private static float Smooth01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        private float LowestHandY()
        {
            float y = float.MaxValue;
            if (HandBone != null) y = HandBone.position.y;
            if (rHand != null) y = Mathf.Min(y, rHand.position.y);
            if (y == float.MaxValue) y = transform.position.y;
            return y;
        }

        /// <summary>
        /// Last-resort safety net: no part of the avatar may sink through the floor while
        /// the flip rotates it (v0.8.0 let the head dive into the ground).
        /// </summary>
        private void FloorGuard()
        {
            float groundY = transform.parent != null ? transform.parent.position.y : transform.position.y;
            float low = float.MaxValue;
            low = MinY(low, lFoot);
            low = MinY(low, rFoot);
            low = MinY(low, HandBone);
            low = MinY(low, rHand);
            if (headIndex >= 0 && headIndex < boneTransforms.Length) low = MinY(low, boneTransforms[headIndex]);
            if (low == float.MaxValue) return;
            float need = (groundY + 0.03f) - low;
            if (need > 0.002f)
            {
                transform.position += Vector3.up * need;
                if (Verbose) UnityEngine.Debug.Log($"[TRICK] floor guard raised the body {need:F2} m");
            }
        }

        private static float MinY(float current, Transform t)
        {
            if (t == null) return current;
            return Mathf.Min(current, t.position.y);
        }

        /// <summary>
        /// Points a two-bone limb straight along <paramref name="dir"/> in world space. The
        /// trick poses are written while the body is still upright, and neither a fixed angle
        /// nor the clip pose can do this on a mirrored rig (the arms ended up splayed).
        /// </summary>
        private static void AlignLimb(Transform upper, Transform fore, Transform hand, float weight)
        {
            AlignLimb(upper, fore, hand, Vector3.up, weight);
        }

        private static void AlignLimb(Transform upper, Transform fore, Transform end, Vector3 dir, float weight)
        {
            if (upper == null || end == null) return;
            Vector3 v = end.position - upper.position;
            if (v.sqrMagnitude > 0.0004f)
            {
                Quaternion q = Quaternion.FromToRotation(v.normalized, dir);
                if (weight < 0.999f) q = Quaternion.Slerp(Quaternion.identity, q, weight);
                upper.rotation = q * upper.rotation;
            }
            if (fore == null) return;
            Vector3 v2 = end.position - fore.position;
            if (v2.sqrMagnitude > 0.0004f)
            {
                Quaternion q2 = Quaternion.FromToRotation(v2.normalized, dir);
                if (weight < 0.999f) q2 = Quaternion.Slerp(Quaternion.identity, q2, weight);
                fore.rotation = q2 * fore.rotation;
            }
        }

        /// <summary>World-space pitch of a bone around the avatar's right axis.</summary>
        private static void Twist(Transform b, float degrees, Vector3 axis)
        {
            if (b == null || Mathf.Abs(degrees) < 0.01f) return;
            b.rotation = Quaternion.AngleAxis(degrees, axis) * b.rotation;
        }

        /// <summary>
        /// v0.10.5 — the partner at the air-hockey table: feet shoulder-width, knees bent, a slight
        /// hunch, and the visually-right arm/body following the mini-game mallet. The hand target is
        /// world-space; the arm is pointed at it and the spine side-bends toward it.
        /// </summary>
        private void ApplyMiniPose(Vector3 right, float b)
        {
            if (b <= 0.002f) return;

            // knees / hunch
            if (hips != null) hips.position += Vector3.down * (0.07f * b);
            Twist(spine, 14f * b, right);
            Twist(spine1, 7f * b, right);
            if (headIndex >= 0 && headIndex < boneTransforms.Length && boneTransforms[headIndex] != null)
                Twist(boneTransforms[headIndex], 5f * b, right);

            Twist(lUpLeg, -18f * b, right);
            Twist(rUpLeg, -18f * b, right);
            Twist(lLeg, 36f * b, right);
            Twist(rLeg, 36f * b, right);
            Twist(lFoot, -18f * b, right);
            Twist(rFoot, -18f * b, right);

            // feet shoulder-width: splay the thighs outward about the body's forward axis
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            fwd.Normalize();
            float sideL = 1f;
            if (hips != null && lUpLeg != null)
            {
                float sx = Vector3.Dot(lUpLeg.position - hips.position, right);
                if (Mathf.Abs(sx) > 0.01f) sideL = Mathf.Sign(sx);
            }
            Twist(lUpLeg, 7f * b * sideL, fwd);
            Twist(rUpLeg, -7f * b * sideL, fwd);

            // the body follows the mallet: side bend toward it and a little extra forward lean
            Vector3 local = transform.InverseTransformPoint(miniHand);
            float side = Mathf.Clamp(local.x * 20f, -12f, 12f);
            float fwdLean = Mathf.Clamp(local.z * 5f, -4f, 9f);
            Twist(spine, -side * b, fwd);
            Twist(spine, fwdLean * b, right);
            Twist(hips, side * 0.25f * b, Vector3.up);

            // visually-right arm (Left* bones are on the viewer's right on this mirrored rig)
            if (lArm != null && HandBone != null)
            {
                Vector3 dir = miniHand - lArm.position;
                if (dir.sqrMagnitude > 0.0004f)
                    AlignLimb(lArm, lForeArm, HandBone, dir.normalized, b);
            }
        }

        /// <summary>
        /// Squat: hips drop, thighs swing forward, knees fold back under the body,
        /// feet stay flat. Angles were solved so the feet land under the hips.
        /// </summary>
        private void ApplyCrouch(float k, Vector3 right)
        {
            if (hips != null) hips.position += Vector3.down * (0.40f * k);
            Twist(spine, 14f * k, right);      // lean forward
            Twist(lUpLeg, -60f * k, right);
            Twist(rUpLeg, -60f * k, right);
            Twist(lLeg, 112f * k, right);
            Twist(rLeg, 112f * k, right);
            Twist(lFoot, -50f * k, right);     // keep the soles flat
            Twist(rFoot, -50f * k, right);
        }

        /// <summary>Airborne: legs tucked, arms slightly forward.</summary>
        private void ApplyAir(float a, Vector3 right)
        {
            Twist(lUpLeg, -38f * a, right);
            Twist(rUpLeg, -28f * a, right);
            Twist(lLeg, 62f * a, right);
            Twist(rLeg, 48f * a, right);
            Twist(lFoot, -22f * a, right);
            Twist(rFoot, -18f * a, right);
            Twist(rArm, -32f * a, right);
            Twist(lArm, -32f * a, right);
            Twist(spine, 6f * a, right);
        }

        /// <summary>
        /// One-shot item use. It is a straight PUSH away from the chest, not a swing:
        /// the arm comes up to horizontal, extends and goes back, and the prop's anchor
        /// is aimed forward at the same time (HandFollow.Push).
        /// </summary>
        private void ApplySwing(Vector3 right, float scale)
        {
            if (swingStart < 0f || scale < 0.01f)
            {
                if (HandFollowAnchor != null && HandFollowAnchor.Push != 0f) HandFollowAnchor.Push = 0f;
                return;
            }

            // v0.7.7: 8× faster than the 0.75 s push of v0.7.6 → a quick jab
            float dur = 0.75f / Mathf.Max(0.05f, PushSpeed);
            float t = Time.time - swingStart;
            if (t >= dur)
            {
                swingStart = -1f;
                if (HandFollowAnchor != null) HandFollowAnchor.Push = 0f;
                return;
            }
            float p = t / dur;
            float k = Mathf.Sin(p * Mathf.PI) * scale;     // straight out and back, no wind-up

            Vector3 fwd = Vector3.Cross(right, Vector3.up);
            if (fwd.sqrMagnitude < 0.0001f) fwd = transform.forward;
            fwd.Normalize();

            // v0.7.6 rotated the arm by a fixed angle, and because the forearm twist adds
            // to the shoulder rotation the fist ended up ABOVE the shoulder (measured
            // handY 1.50 with the shoulder at 1.41 — arm overhead, exactly what the user
            // complained about). Now the shoulder angle is solved from the skeleton so the
            // fist lands at mid-body height, where it pushes straight ahead.
            // Straighten the elbow first — the clip carries it bent, which shortens the
            // reach by ~10 cm. Which sign extends it is unknown on a mirrored rig, so it
            // is measured once (SolveElbowSign).
            if (lForeArm != null && HandBone != null && lArm != null)
            {
                if (!elbowSigned) { elbowSign = SolveElbowSign(right); elbowSigned = true; }
                Twist(lForeArm, elbowSign * 24f * k, right);
            }

            // Mid-body height = the model's middle (2.12 m → 1.06 m), expressed off the pelvis
            // so it follows a crouch. The fist pushes forward AT that line and never above it.
            // ORDER MATTERS: the torso lean and the forward hip shift are applied FIRST —
            // a 22° lean alone drops the fist ~19 cm, so solving before it put the arm back
            // down (measured 1.06 in the solver vs 0.89 in the rendered pose).
            Twist(spine, 16f * k, right);                           // torso lunges forward
            Twist(rArm, 15f * k, right);                            // free arm counter-swing
            if (hips != null) hips.position += fwd * (0.14f * k);    // the whole body reaches in

            float midBodyY = (hips != null ? hips.position.y : 0.91f) + 0.15f;
            float shoulderAngle = Mathf.Clamp(SolveArmAngle(midBodyY, right), 0f, 80f);
            Twist(lArm, -shoulderAngle * k, right);

            if (HandFollowAnchor != null) HandFollowAnchor.Push = k;

            if (Verbose)
                UnityEngine.Debug.Log($"[PUSH] p={p:F2} k={k:F2} aim={shoulderAngle:F1} elbow={elbowSign} " +
                                      $"midBodyY={midBodyY:F2} shoulderY={(lArm != null ? lArm.position.y : -1f):F2} " +
                                      $"handY={(HandBone != null ? HandBone.position.y : -1f):F2} " +
                                      $"handZ={(HandBone != null ? HandBone.position.z : -1f):F2}");
        }

        private float elbowSign;
        private bool elbowSigned;

        /// <summary>
        /// The elbow is bent by the clip; to reach further the forearm has to rotate the way
        /// that MAKES THE ARM LONGER. Measured at runtime (the rig is a mirror image, so the
        /// correct sign cannot be assumed).
        /// </summary>
        private float SolveElbowSign(Vector3 right)
        {
            float len0 = (HandBone.position - lArm.position).magnitude;
            Quaternion baseRot = lForeArm.rotation;

            lForeArm.rotation = Quaternion.AngleAxis(20f, right) * baseRot;
            float lenPlus = (HandBone.position - lArm.position).magnitude;

            lForeArm.rotation = Quaternion.AngleAxis(-20f, right) * baseRot;
            float lenMinus = (HandBone.position - lArm.position).magnitude;

            lForeArm.rotation = baseRot;

            float best = Mathf.Max(lenPlus, lenMinus);
            if (best <= len0 + 0.002f) return 0f;                 // already straight
            return lenPlus >= lenMinus ? 1f : -1f;
        }

        /// <summary>
        /// Bisects how far to rotate the upper arm about the avatar's right axis so the FIST
        /// ends up at <paramref name="targetY"/>. A fixed angle cannot do this: the fist also
        /// rides the clip pose and the elbow bend, so the same 46° put it at chest height in
        /// one clip and above the head in another.
        /// </summary>
        private float SolveArmAngle(float targetY, Vector3 right)
        {
            if (lArm == null || HandBone == null) return 0f;
            Quaternion baseRot = lArm.rotation;
            float lo = 0f, hi = 125f;
            for (int i = 0; i < 14; i++)
            {
                float mid = 0.5f * (lo + hi);
                lArm.rotation = Quaternion.AngleAxis(-mid, right) * baseRot;
                if (HandBone.position.y < targetY) lo = mid;
                else hi = mid;
            }
            lArm.rotation = baseRot;
            return 0.5f * (lo + hi);
        }

        /// <summary>
        /// Side-step for strafing: Soldier has no side clips, so the thighs splay about the
        /// avatar's forward axis in time with the gait and the body leans into the movement
        /// — otherwise the forward walk sliding sideways is the "лунная походка".
        /// </summary>
        private void ApplyLateral(Vector3 right, float scale)
        {
            float lat = Mathf.Abs(lateralBlend) * scale;
            if (lat < 0.12f) return;

            Vector3 fwd = Vector3.Cross(right, Vector3.up);
            if (fwd.sqrMagnitude < 0.0001f) fwd = transform.forward;
            fwd.Normalize();

            float phase = gaitPhase * Mathf.PI * 2f;
            float sign = lateralBlend < 0f ? -1f : 1f;            float step = Mathf.Sin(phase) * 20f * lat;

            // leading leg abducts while the other adducts → alternating side steps
            Twist(lUpLeg, step * sign, fwd);
            Twist(rUpLeg, -step * sign, fwd);
            Twist(spine, -8f * lat * sign, fwd);                 // lean into the strafe
            if (hips != null)
                hips.position += right * (0.045f * Mathf.Sin(phase) * sign * lat);
        }

        private static Vector3 ToUnity(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);
        private static Quaternion ToUnity(System.Numerics.Quaternion q) => new Quaternion(q.X, q.Y, q.Z, q.W);

        private static void Advance(ClipState clip, float dt, bool loop)
        {
            if (clip == null) return;
            float dur = clip.Info.Duration;
            if (dur <= 0.0001f) return;
            clip.Time += dt;
            if (loop) clip.Time %= dur;
            else if (clip.Time > dur) clip.Time = 0f;
        }

        private void SamplePose(ClipState clip, float time, int node, out Vector3 pos, out Quaternion rot, out Vector3 scl)
        {
            var rest = restNodes[node];
            pos = ToUnity(rest.T);
            rot = ToUnity(rest.R);
            scl = ToUnity(rest.S);

            if (clip.Transl.TryGetValue(node, out var ct))
            {
                ReadValue(ct, time, tmp);
                pos = new Vector3(tmp[0], tmp[1], tmp[2]);
            }
            if (clip.Rot.TryGetValue(node, out var cr))
            {
                ReadValue(cr, time, tmp);
                rot = new Quaternion(tmp[0], tmp[1], tmp[2], tmp[3]).normalized;
            }
            if (clip.Scale.TryGetValue(node, out var cs))
            {
                ReadValue(cs, time, tmp);
                scl = new Vector3(tmp[0], tmp[1], tmp[2]);
            }
        }

        /// <summary>Read an animation sampler value at time t into outV (2..4 comps).</summary>
        private static void ReadValue(GlbModelLoader.AnimChannel ch, float t, float[] outV)
        {
            int comps = ch.Comps;
            int count = ch.Times.Length;
            if (count == 0) return;

            bool cubic = ch.Interpolation == "CUBICSPLINE";
            int stride = cubic ? 3 : 1;
            int valueIdx = cubic ? 1 : 0;

            int lo, hi;
            if (count == 1 || t <= ch.Times[0]) { lo = hi = 0; }
            else if (t >= ch.Times[count - 1]) { lo = hi = count - 1; }
            else
            {
                lo = 0; hi = count - 1;
                while (hi - lo > 1)
                {
                    int mid = (lo + hi) >> 1;
                    if (ch.Times[mid] <= t) lo = mid; else hi = mid;
                }
            }

            int baseLo = (lo * stride + valueIdx) * comps;
            if (lo == hi || ch.Interpolation == "STEP")
            {
                for (int k = 0; k < comps; k++) outV[k] = ch.Values[baseLo + k];
                return;
            }

            float t0 = ch.Times[lo], t1 = ch.Times[hi];
            float a = t1 > t0 ? (t - t0) / (t1 - t0) : 0f;
            int baseHi = (hi * stride + valueIdx) * comps;
            for (int k = 0; k < comps; k++)
            {
                float v0 = ch.Values[baseLo + k];
                float v1 = ch.Values[baseHi + k];
                outV[k] = v0 + (v1 - v0) * a;
            }
        }
    }
}
