# AGENTS.md

Project: online co-op mod for **BABBDI** v1.2.3 (Unity, by manu1400). User speaks Russian — respond in Russian. Game lives at `E:\858\BABBDI_WINDOWS_1.2.3` (user-provided install; itch.io download). Everything below is verified from game files unless noted.

## Layout

- Project root `E:\858\BabbdiWork\Project` (moved off `C:` on 24.11.2026 — the old `C:\Users\LethargyLogics\Documents\Проект по умолчанию` no longer exists).
- `BabbdiCoop/` — BepInEx plugin (csproj + Plugin.cs + build.ps1). `build.ps1` compiles and copies DLL into game's `BepInEx/plugins`.
- `BabbdiCoop/SovietDistrict*.cs` — the district builder is **one partial class spread over 15 files** (v0.10.43). Read only the part you need:
  `SovietDistrict.cs` (data + Build + materials), `.World.cs` (deck/terraces/ramps/streets), `.Layout.cs` (plots/rows/free-space),
  `.Buildings.cs` (shell/interior/stairs), `.Details.cs` (walls/slabs/relief/rails/bridges), `.House.cs`, `.Arcade.cs`,
  `.Props.cs` (helicopter + helpers), `.Penthouse.cs`, `.Memorial.cs`, `.Figures.cs`, `.Preview.cs` (map-shot poses),
  `.Probes.cs` (walkability diagnostics), `.Diagnostics.cs` (city/texture/npc scans), `.Geo.cs` (mesh builder).
- `decomp/Assembly-CSharp.decompiled.cs` — ilspycmd output of game code. Regenerate: `$env:DOTNET_ROLL_FORWARD='Major'; ilspycmd <Assembly-CSharp.dll> -o decomp`. ilspycmd 8.2 needs that env var on this machine (.NET 8, not 6).
- `downloads/` — BepInEx zip etc.
- `Rigger/`, `Harness/` — offline tools (auto-rig, glTF render).

## Git

- Repository at the project root since 24.11.2026. Local identity: `user.name = LethargyLogics`, `user.email = lethargylogics@users.noreply.github.com` (change it before pushing to GitHub; `git config user.email ...`).
- Tags so far: `v0.10.43-baseline` (single 7 972-line `SovietDistrict.cs`), `v0.10.43-split` (15 partial files, verified byte-for-byte by block comparison + build + in-game district build).
- Ignored: `bin/`, `obj/`, `*.user`, `.vs/`, `harness_out/`, `*.log`. **Tracked**: `model/` (~130 MB of GLBs/PNGs) — the repo is ~250 MB.
- Rule: commit after every build that passes; tag every shipped version `vX.Y.Z`. `git diff` is now the changelog.
- Remote (when the user connects GitHub): `git remote add origin https://github.com/<user>/<repo>.git` then `git push -u origin master --tags`. GitHub Desktop works too.


## File locations (hard rule)

`C:` is almost full; **everything generated goes to `E:\858`**, never to `C:\Users\LethargyLogics\{Documents,Pictures,Desktop,Downloads}` or `%TEMP%`.

- `E:\858\BabbdiWork\Project` — sources (this folder).
- `E:\858\BabbdiWork\Art` — generated art/textures/PNG sheets.
- `E:\858\BabbdiWork\Screenshots` — game/debug screenshots (`SystemScreenshots` = the former `Pictures\Screenshots`).
- `E:\858\BabbdiWork\Temp` — scratch scripts, SD outputs, intermediate files, unpacked archives.
- `E:\858\BABBDI_WINDOWS_1.2.3` — the game install; the plugin's own `map/`, `preview/` and `*_test.png` shots stay there (that is where the plugin writes them at runtime).
- Python/SD helpers must be pointed at `E:\858\BabbdiWork\Temp\...` explicitly instead of `%TEMP%`.


## Game facts (verified)

- Mono build (`MonoBleedingEdge`), Unity **2022.1.5f1** (string in `globalgamemanagers`). BepInEx 5.4.23.2 x64 installed into game folder; doorstop `winhttp.dll` present.
- `Assembly-CSharp.dll` is small (112 KB) — all gameplay code in one cs file in `decomp/`.
- Key classes with decomp line numbers: `FirstPersonController` L1253 (asset-derived, singleton `instance`, CharacterController + child Camera, fields on L1253-1900; Awake L1943 does `GameObject.Find` of gadget transforms "Club", "Climber", "Propeller", "Blower", "Grabber", "Moto"; Update expects `GameObject.Find("Outro")` and "GameManager" unguarded → menu scene objects must exist), `GameManager` L6834 (implements ISaveable — save systems to respect later), `Interactable` L5207 base, `Door` L5156, `Lift` L5962, `NPC` L6031, `PickupItem` L3661, `LevelManager` L6514.
- Steam present: `Facepunch.Steamworks.Win64.dll` + `steam_api64.dll` in game; no Steam-host rollout assumed, coop = LiteNetLib-style host/client over LAN/Radmin/ZeroTier.
- Scene flow clue: `level0` small (main menu/intro), `level1` big (the world). Confirm actual names before patching scene load.

## Build

- .NET SDK 8.0.426 (already installed; don't reinstall). Running sessions must refresh PATH manually: `$env:Path = [Environment]::GetEnvironmentVariable('Path','Machine') + ';' + [Environment]::GetEnvironmentVariable('Path','User')`.
- csproj references game DLLs at fixed `E:\858...` path (GameDir property) — if game moves or user reinstalls, update `<GameDir>`.
- Target framework: netstandard2.1. BepInEx 5 + Harmony via direct DLL refs from the game folder, no nuget needed for plugin deps.
- Windows console prints Cyrillic as mojibake; quote Cyrillic paths. Built-in websearch/webfetch to itch.io fail (403/404) — don't retry them; derive facts from files here.

## Mod status (BabbdiCoop v0.10.43)

- Packet format (**31 B**, type=1): pos(12) + rot(16) + itemId(1, 0xFF=empty hands) + flags(1) @ ~30 Hz (v0.7.3; was 20 Hz). **Semantics since v0.5.0: pos = FEET position** of the sender, computed as `fpc.transform.TransformPoint(cc.center) - up*(cc.height/2)` (CharacterController-aware → crouch keeps avatar on ground). Rotation = camera rotation; v0.6.0 derives **speed** (position delta / packet dt, smoothed, ignores >12 m/s teleports) and **pitch** (`asin(-forward.y)`) from it. **v0.7.0 flags byte**: bit0 crouch (`fpc.isCrouching`), bit1 airborne (`!cc.isGrounded`), bit2 use (Fire1 rising edge while an item is held, latched 0.2 s). **v0.8.0**: bit3 handstand (held state, `B`), bit4 flip (latched ≈1 s on `V`, receiver starts one flip on the rising edge). Old builds are incompatible with 31 B packets.
- One-way-flow bug (client never received host packets) root-caused as unbound-UdpClient race: client `UdpClient(0)` binds at startup now; RX thread runs off Unity main thread, so no `Time.*` calls in it (Unity throws cross-thread) — Net.Clock Stopwatch instead.
- **Held items (corrected v0.5.2, verified in decomp)**: BABBDI has **no WeaponSwitching** — `FindObjectOfType<WeaponSwitching>()` always returns null (that's why items were never cloned; the dump probe never even wrote dump.txt). Real mechanism: `InteractObject` (`inHands = true`) instances are children of **Main Camera** at anchors `ObjectPos/ObjectPos2/LightPos/CompassPos/...`; picking a world item sets `GameManager.pickup/item` and **destroys the world object**. Plugin sends `ItemProperties.id` (byte) and the receiver clones any scene `ItemProperties` with the same id (Resources.FindObjectsOfTypeAll, scene objects only), strips it to visuals, parents it to the avatar's `HandAnchor` and re-centres it by renderer bounds; if no object with that id exists locally → toast + skip. v0.6.0: `HandAnchor` is driven by `HandFollow` (position = the visually-right hand bone — v0.7.1: `mixamorig:LeftHand`, see the mirror note, rotation = avatar root), so the item rides the animated hand. **v0.7.0 fit (`FitHeldItemToHand`)**: bounds are measured in the anchor's space; props longer than 0.6 m are scaled to 0.55 m (the bat is ~1.1 m in the world), elongated props have their longest local axis rotated onto the anchor's +Z (horizontal, pointing forward) and are gripped by the **thin end**, small props are just centred in the palm. **v0.7.1 grip test**: the old vertex-mass heuristic lied (handles are tessellated denser than barrels), so `HeavyEndAtHand` now builds a **cross-section radius profile** along the aligned axis and compares the **mean radius of the end 15 % bands** (flip only if the hand end is >25 % fatter) — a bat/tool has a thin handle and a fat barrel/head. `[COOP] grip: len=… near(r)=… far(r)=… bins[…] mode=… auto=… final=…` is logged; `[Debug] ItemGrip = auto|flip|keep` overrides it without a rebuild. **v0.7.2 fit**: the long axis now comes from **PCA (power iteration) over the vertices** instead of the AABB's largest component (an AABB lies for diagonals, and a wrong axis also made the 180° flip rotate about the wrong axis — the bat stayed with the fat end at the hand). Vertices are collected from `MeshFilter` **and** `SkinnedMeshRenderer` (`BakeMesh`); after setting scale+rotation the point list is transformed **mathematically** (`rot * (p * scale)`) instead of re-reading the mesh. **`HandFollow` follows the hand bone's rotation** (offset captured in `Bind()` at fit time = avatar yaw), so the prop now changes orientation on all three axes with the wrist — needed for the swing; side effect: the bat can look vertical in poses where the wrist rolls (run clip). **F8** swaps the grip end by hand for the current item id (session only) — `[COOP] manual grip flip for item id N = true|false`. **v0.7.3 `HandFollow` = arm-aim, not wrist**: following the hand BONE's rotation made the bat point straight up whenever the walk/run clip rolled the wrist (user video, 22.10.2026: bat vertical along the body) — mixamo's wrist axes are not a grip axis. Now the anchor's position = hand bone and its **+Z = `normalize(0.62·(hand−foreArm) + 0.55·avatarForward)`** (`Quaternion.LookRotation(aim, avatarUp)`), i.e. "out of the fist, ~50° forward of the arm" — the way a person carries a club. It still rotates on all three axes with the arm (swing verified in preview) but never flips vertical. `GlbAnimator.ForeArmBone` (`LeftForeArm`, mirrored rig) feeds the direction. **v0.7.4 overlay (corrected target)**: the pass now runs on the **LOCAL first-person held item**, not on the partner's prop — the user wanted the viewmodel on top, not third-person props. It walks the children of `Camera.main` (plus a fallback to the FPC camera), keeps those with `InteractObject.inHands == true`, and only if that list is non-empty does it `ClearRenderTarget(depth:true)` and `DrawRenderer` each renderer/material at `CameraEvent.AfterEverything`. The partner's items stay depth-tested. In preview mode the fit also runs on a synthetic bat (thin handle + fat barrel) so the preview screenshots show the grip, orientation and scale.
- Avatar pipeline: `GlbModelLoader.cs` (pure .NET, Newtonsoft from game Managed/) + `GlbAvatarBuilder.cs` (Unity adapter) + `GlbAnimator.cs` (runtime skeletal playback). **Primary model since v0.9.6: `plugins/BabbdiCoop/villager.glb`** — Soul Reaver `Villager Male 3`, auto-rigged onto the soldier's skeleton by `Rigger/` (see the villager section below). Fallbacks in this order: `soldier.glb` (three.js `Soldier.glb` by @vxdesign, CC-BY 4.0 — 2.1 MB, skinned, clips Idle/Walk/Run/TPose), `character-r.glb` (Kenney blocky), OBJ (`player.obj`), primitives. Normalized to **2.12 m** (`TargetHeight`), feet at local y=0, XZ centred.
- **Renderer side is now a real rig (v0.6.0)**: one GameObject per glTF node (local T/R/S from `Parsed.Nodes`), `SkinnedMeshRenderer` with `mesh.boneWeights` (JOINTS_0/WEIGHTS_0 indices are indices into `skin.joints`) and `mesh.bindposes`; vertices use `Primitive.RawPositions` (mesh space). Binding math: Unity column matrix = **transpose** of our row matrix (`ToUnity` passes rows as Unity columns); verified offline (`skinchk` temp project: skinned result matches loader world positions to 6e-7). Scale/centering/180° yaw are **wrapper transforms** (AvatarMesh → YawRoot → CenterRoot → ScaleRoot) because skinned vertices must stay in mesh space.
- **Yaw180 per model**: Soldier's raw front is glTF **-Z** (user: avatar looked backwards even with pass-through), Kenney's is +Z. `GlbAvatarBuilder.Load(glb, yaw180)`; plugin passes `true` for `soldier.glb` **and `villager.glb`** (the rigger bakes the source OBJ turned 180° into the soldier's convention, so its front is -Z as well; verified in the v0.9.6 preview — the face points at the front camera). Wrapper order matters: yaw outside centering, otherwise the XZ centering doubles.
- **Animation (v0.7.0)**: `GlbAnimator` samples the clips by time (LINEAR/STEP; CUBICSPLINE uses keyframe values, tangents ignored) and tilts the head by rotating the head bone in **world space** around `AvatarMesh.transform.right` (`pitch*0.6`, clamp ±75°). Walking/running is driven by **one shared `gaitPhase` (0..1)** advanced by `speed / lerp(1.45, 2.6)` cycles/s and sampled into both clips at `phase * duration` — blending two independently-phased gaits averaged limbs from different steps (arms clasped in front, legs sliding); with a common phase the cross-fade stays coherent. Blend weights: idle↔walk ramp 0.15–1.6 m/s, walk↔run 2.2–4.5 m/s. Rest TRS is used for nodes a clip does not animate.
- **The rig is a mirror image (v0.7.1)**: glTF positions go into Unity untouched, so the glTF skeleton's `RightHand` ends up at local **+x** while the avatar faces −Z — i.e. on its anatomical **left**. Verified in-game (`handBefore=(0.33, …)` for an avatar at x=0, yaw 180). All hand-dependent code therefore uses the **LEFT** bones: `HandBone` = `mixamorig:LeftHand`, the item swing drives `LeftArm` (+`LeftForeArm`), and the fallback anchor sits at x = **−0.3**. Using the "Right" bones made the avatar look left-handed.
- **Network smoothing (v0.7.4: jitter buffer + Catmull-Rom)** — *superseded by v0.7.8 below, kept for the history of the bug*: packets go into a `List<PoseSample>` (time, pos, unwrapped yaw, max 16); each frame the avatar renders `delay = clamp(sampleInterval*1.8, 70–260 ms)` in the past and interpolates with a **uniform Catmull-Rom spline** between the bracketing samples (`s0/s1/s2/s3`, clamped at the ends). A plain lerp reproduced the sender's frame-quantised packet pacing as a speed change at every knot — that is the "дёргает при беге" that survived v0.7.3. The yaw is **unwrapped on arrival** (`prev + DeltaAngle(prev, yaw)`) because the spline needs a continuous scalar. `sampleInterval` = EMA(0.15) of arrival intervals. The packet-delta speed estimate was **removed**: a dropped packet made the delta cover two intervals and the gait briefly doubled; `remoteSpeed` now comes only from the rendered movement (`AnimateRemote`). The old two-packet window (delay = span·1.15) starved whenever one packet arrived late → freeze + lurch. **v0.7.3 batch fix**: `PumpIncoming` drains the whole queue into `incomingBatch` and back-dates each packet (`t = now − (n−1−i)·interval`) — two packets in one `Update` used to share one `Time.time` and overwrite each other → teleport.
- **v0.7.6 (second round from the in-game footage)**: (1) **the Club stayed at FULL world size** — the log said `[COOP] item has no readable vertices — left as-is`: scene props ship with Read/Write Disabled, so `mesh.vertices` throws and every fallback path was skipped. `CollectVerts` now (a) walks renderers including inactive ones, (b) tries `Mesh.AcquireReadOnlyMeshData` for non-readable meshes *[it returns nothing in this build — kept as a cheap first attempt]*, (c) else `BoundsFromRenderers` (union of `Renderer.bounds` corners in anchor space) and fits from those 8 box corners: scale/alignment/centring work, and because the game holds props by their **pivot** (parented to the camera anchor with no offset), the grip end = the end nearest the clone origin (log `[COOP] item meshes are not readable — fitting by renderer bounds`). (2) **size**: the "normalise long props to 0.55 m" step is gone — the third-person prop is now exactly `[Debug] ItemScale` (default **0.5**) of the world size, i.e. half (`raw size 0,05x0,05x0,92 m, scale 0,50, world len 0,46 m` in the preview). (3) **item use = straight push, not a swing**: `ApplySwing` (GlbAnimator) is now a 0.75 s push — `Twist(lArm, -88°·k)`, forearm −18°·k, spine −8°·k, k = smoothstep up / hold / down — and it drives `HandFollow.Push` so the prop's aim slerps from "up out of the fist" to **horizontal forward** for the duration (preview shot t=10.0 s shows the arm and prop thrust ahead). (4) **avatar height 2.12 m** (`TargetHeight`, +14 % over 1.86; Soldier's raw bind height is 1.73 m → scale 1.226, logged). (5) **prop no longer flails while running**: `HandFollow` damps the hand — position `Lerp(current, hand, 1−e^(−dt/0.06))`, rotation `Slerp(current, target, 1−e^(−dt/0.12))` capped by `RotateTowards(..., 240°/s)`. (6) **gait slowed**: `GlbAnimator.GaitScaleWalk = 0.5`, `GaitScaleRun = 0.34` (≈3× slower run, 2× slower walk). Preview camera re-framed for the taller avatar (target `+0.98 m`, distance 4.3) and `PreviewTestItem` also fits a throwaway **non-readable** clone (`mesh.UploadMeshData(true)`) so the fallback path is exercised in every preview run.
- **v0.8.2 (tricks now turn in the sagittal plane)**: both tricks rotate about the **body's own right axis**, not the model's local Z (handstand) or the world X (flip). v0.8.1 pitched the handstand with `Euler(0,0,roll)` = about the **facing** axis, which is a cartwheel — on video the body went out "to the left" instead of over the head; the flip used world X while the avatar keeps **any** yaw, so the tumble left the sagittal plane and the body visibly changed direction past the half-turn. Now `Vector3 bodyRight = Quaternion.Inverse(parentRot) * transform.right;` (parent is yaw-only) and `qTot = AngleAxis(rollDeg, bodyRight) * baseRot`, `fq = AngleAxis(-360·curve, bodyRight) * qTot` — **+angle about right = the head goes toward `transform.forward`** (`right × up = forward`), so the handstand is `+180` (forward, the avatar ends up facing back), the backflip stays `-360`. `ApplyHandstandPose` uses the same `bodyRight` for its `qFin/qNow` measurement frames, and the local view (`ApplyTrickCamera`) now pitches **`t.right`** for the handstand instead of rolling about `t.forward`. Verified by logging `transform.forward/right/up` every 0.15 s: `rgt` stays `(-1,0,0)` for the whole move (sagittal), the handstand sweeps fwd `(0,0,-1) → (0,-1,0) → (0,0,1)`, the backflip sweeps it up and over and returns exactly to `(0,0,-1)`; `[TRICK] back to rest` = base.
- **v0.8.1 (both tricks rewritten as a timeline of the body doing the move itself)**: packet unchanged (**31 B**, same bits 3/4) → fully compatible with 0.7.7/0.7.8/0.8.0 peers.
  1. **The model stayed crooked after a trick** — the trick pose was applied on top of a root that was still rotated from the previous frame, and `basePos/baseRot` were captured inside the idle branch with leftovers. Now `ApplyTricks` **parks the root** (`localRotation = baseRot`, `localPosition = basePos`) before any bone is touched, captures the base **once** (`baseCaptured`), and in the idle branch drives the root hard back to it; `[TRICK] back to rest: localPos=(0,0,0) base=(0,0,0) rot=(0,0,0)` is logged in verbose mode. The same parking fixed a real bug: `AlignLimb` (world `Vector3.up`) ran in the *rotated* frame, so the raised arms were "aligned" downward and the handstand was performed with the arms at the hips.
  2. **The trick owns the body (`trickWeight`)**: gait, crouch, airborne, lateral strafe, item swing and the pitch-lean layers are multiplied by `leanK = 1 - trickWeight` (plus `Mathf.MoveTowards` / EMA), so they no longer stack on top of an inverted root — that overlap (spine 0.32 + 0.12 + head 0.45 while rolled) is what made the body fold in half in the second half of the flip.
  3. **Handstand = jump onto the hands** (`ApplyHandstandPose`): load (dip + knees, 0–0.10) → arms overhead (`Twist` 172° then `AlignLimb` onto `Vector3.up`, solved from the skeleton because the rig is mirrored) → the **root turns around the centre of mass** (`com - qNow*com`, `comLocalY` measured as `hipLocalY * 0.90` ≈ 1.08) with a 0.22 m hop, and the "hands on the floor" solve is applied **only at the end** (`liftEnd`, measured once at roll 180, weighted by `rise`) — v0.8.0 solved the hand height **every frame**, so the body was dragged up by the ankles as the roll passed the horizontal (the "invisible force"). Hold ≈ 1.5 s with a slow balance sway (spine ±1.8°, thighs ±2.2°, hips ±1.2 cm). Exit = the same timeline reversed (`HsExit = 0.55 s`), landing on the feet.
  4. **Straight body when inverted**: `hips.rotation` is straightened so `hips→spine` is the world vertical (a rigid "stand up straight" — the idle clip carries a soft spine and the body leaned 10–12°), then the legs are aligned along `Vector3.down` with the new directed `AlignLimb(upper, fore, end, dir, weight)` overload (the fixed 6° nudge left the legs as a "hockey stick"), then the toes are pointed 22°.
  5. **Backflip keeps its momentum** (`ApplyFlipPose` + `RotationCurve`): 1.08 s total, `FlipIn 0.16 / FlipAir 0.72 / FlipOut 0.20`; the spin is **−360° about the hips** (`com` pivot: `off += com - qTot*com`) with a 0.62 m arc and a tuck (thighs/shins/spine/arms on a `sin(π·p)` envelope). `RotationCurve` eases only the outer 6 % (`t' = clamp((t-0.06)/0.88)`, smoothstep), so the second half keeps angular speed instead of crawling; `FloorGuard` still lifts the body if any bone would sink.
  6. **Local view** (`Plugin.ApplyTrickCamera`) is driven by the same curves, in **LateUpdate**: handstand rolls the view 180° about `forward` over `1/HsEnter`, backflip −360° about `right` over `FlipDuration` — the game's own yaw/pitch is never touched.
- **v0.8.0 (real-arrival timestamps, ground bias, torso lean, handstand + backflip)**: four user tasks at once. Packet stays **31 B**; flags gain **bit3 = handstand (held state)**, **bit4 = flip (latched ≈1 s, the receiver starts one flip on the rising edge)** — 0.7.7/0.7.8 peers still connect, they just never send the trick bits.
  1. **Stutter, part 2 (real-game-only factors)**: (a) `PumpIncoming` no longer back-dates a drained batch by the average interval — `Net` stamps every packet in the **RX thread** with `Net.Clock` (`incoming` items now carry `clock`) and the plugin converts `arrival = Time.time − (ClockNow − clock)`. Back-dating compressed a batch into 2 intervals after a main-thread hitch, so the knot grid ran up to 1.5× fast for ~100 ms. (b) `arrivalJitter` = EMA(|spacing − smoothed cadence|) and `renderDelay` target = `clamp(interval·1.6 + jitter·2.0 + 0.05, 0.10, 0.32)` (was `interval·3`, blind to jitter). (c) **`groundOffset` is a slow bias now**: it used to move **5 cm per packet** toward the raycast hit (1.5 m/s of vertical motion at 30 Hz) — on rocks and stairs the ray alternates between ledge and floor and the whole model bobbed; now EMA 0.10/packet with a 1.5 cm dead zone and ±0.18 m clamp. (d) `remotePitch` is smoothed (τ 0.10 s) instead of being the raw last-packet pitch (the head — and now the torso — snapped 30×/s).
     - NetSim A/B on one build under **true ±20 ms arrival jitter** (the sim now keeps the exact `due` time, so it is stricter than the v0.7.8 reference): legacy walk `std 0.850 / maxstep 8.07 / 7 freeze`, run `std 2.064 / maxstep 18.4 / 9 freeze`; new walk `std 0.102 / maxstep 0.134 / 0 freeze`, run `std 0.362 / maxstep 0.349 / 0 freeze`. Steady-state (no ramps): walk ±1 %, run `mean 3.795 std 0.087` = **2.3 %**.
     - `[Debug] Trace = true` writes `trace_HHmmss_frame.csv` (per frame: rendered pos, target pos, speed, delay, jitter, knot count) and `trace_HHmmss_pkt.csv` (per packet: arrival, spacing, cadence, jitter, grid steps) — for the case the real LAN is still worse than the sim.
  2. **The pickaxe levitated**: the fit's fallback for Read/Write-disabled props built its box from **`Renderer.bounds` — a world-space AABB**. For a prop lying diagonally that box is a loose, wrongly-oriented cube: the long axis came out wrong, so the grip end was tens of cm away from the handle. `MeshBoxPoints` replaces it with each renderer's **mesh-local `bounds` (or `SkinnedMeshRenderer.localBounds`), 8 corners transformed by the renderer's own transform** — an oriented box, PCA gives the true long axis. Preview proof on a deliberately **rotated** non-readable clone: `axis=(0,54,−0,34,0,77)`, `world len 0,46 m` (= 0.92 × ItemScale 0.5, exact), thin end 0.9 cm vs 1.8 cm → correct end, `nearest point 4,1 cm from the fist`. Also confirmed **why `Mesh.AcquireReadOnlyMeshData` is useless here**: it does not throw, it reports `vertexCount == 0` for these meshes. The grip end is placed at +0.04 m along the anchor (the anchor sits on the wrist joint, the fist is ~4 cm further).
  3. **Torso leans with the view**: on top of the head tilt (`pitch·0.45`), `Twist(Spine2, pitch·0.32)` + `Twist(Spine1, pitch·0.12)` in world space — verified in the preview (looking down 42° bends the whole back, looking up leans it back; log/timeline drives `remotePitchTarget` at t 2.2–4.2 s).
  4. **Handstand + backflip (procedural, no new clips)**: `GlbAnimator.ApplyTricks` runs after the skeleton pose and moves the **root** (`AvatarMesh.localRotation/localPosition`, base pose captured while idle so `FeetAligner`'s shift survives).
     - Handstand: arms `Twist(±162°)` overhead + slight arch, then the body is rolled **180° about its forward axis** (`Quaternion.Euler(0,0,180·blend)` — keeps the facing, inverts up), and raised until the **lowest hand bone touches the ground plane of the avatar root** — measured from the skeleton every frame with the offset temporarily zeroed, so it self-calibrates for any height/model. A `0.28·sin(π·blend)` arc keeps the body off the floor while rolling in/out.
     - Backflip: 0.85 s, **−360° pitch about a pivot at the hips** (`off += h − q·h` holds the hip point in place, so the body spins around itself, not around its feet) + a 0.55 m jump arc + a tuck (`sin(π·p)` envelope on thighs/shins/spine/arms).
     - Local view (`Plugin.ApplyTrickCamera`, in **LateUpdate** because the game rewrites `camera.localRotation` from its own yaw/pitch in `Update`): handstand rolls the view 180° about `forward`, backflip spins it −360° about `right`. The game's controller is never touched (no physics risk) — the *sender* only sees the camera, the partner sees the animation.
     - Keys: **B = handstand toggle, V = backflip** (verified free: the game only reads A/D/W/S/E/F/C/M/R/Tab/Esc/Space/Shift/Ctrl/F12/Alpha1-3). Preview timeline extended: handstand 11.4–13.5 s, flip 13.7–14.6 s, done at 15.4 s (140 shots), studio camera re-framed for the inverted body (`+1.15 m`, distance 5.2).
- **v0.7.8 (the walk/run stutter, measured instead of guessed)**: four independent causes, all in the receive-side render path (`ProcessPacket` / `AnimateRemote`). Found by feeding a **synthetic 30 Hz stream (±20 ms jitter + a 100 ms dropout every 2 s, walk 1.4 / run 3.9 / back −1.4 m/s, 60°/s turn) through the real receive path** and logging the rendered speed per frame (see `NetSim` below):
  1. **The sample list was trimmed to 4** (`samples.Count > 4 && samples[1].Time < tr`) while the render time sat 2.2–3.5 intervals behind the newest knot → `tr` regularly fell *in front of the oldest sample*, the avatar froze on it and jumped forward when the next packet arrived. Now the trim keeps everything the spline needs (`samples.Count > 6 && samples[2].Time < tr`, hard cap 24).
  2. **Knots sat on the arrival times** and were joined by a uniform Catmull-Rom → the speed changed at every knot (±20 ms of arrival jitter on a 33 ms interval is ±60 %). Now the knots are a **uniform grid**: `steps = clamp(round((arrival − lastKnot)/interval), 1, 4)` plus a ±5 % drift correction against `arrival − lastKnot`. This also absorbs a lost packet: the next packet advances 3–4 knots at once instead of leaving a hole.
  3. **`sampleInterval` was a plain EMA** and blew up on one lost packet/burst (measured 0.0316 → 0.0436 s) → the grid stretched and the rendered speed silently dropped to 2.9 m/s. Now `TrackCadence`: EMA clipped to ±40 % of the previous value, gain 0.03 per packet.
  4. **The render delay followed that estimate directly** (`clamp(interval*1.8, …)`) → one jittery estimate moved the render time 42 ms backwards within one frame (16 cm at run speed). Now `renderDelay` is a **2 s low-pass** of `clamp(interval*3, 0.10–0.25)`.
  - **Final architecture**: `tr = Time.time − renderDelay` (a **wall clock** — it can never jump) → uniform grid + Catmull-Rom target → **critically damped follower** (`Vector3.SmoothDamp` / `Mathf.SmoothDampAngle`, smoothTime **0.15 s**) for the rendered pose. The follower eats the *remaining* 30 Hz ripple (the sender quantises its sends to its own frame boundaries, so consecutive packets carry 4/5/6 frames of movement = ±50 % speed for one frame) and turns a 100 ms dropout into a smooth dip instead of freeze + pop. Being critically damped it cannot overshoot, so a real reversal is followed without wobble. Cost: ~0.15 s extra latency on top of the ~0.1 s buffer.
  - **Dead reckoning was tried and rejected**: extrapolating the last velocity over a 150 ms hole on a 60°/s turn misses by ~18 cm and then snaps back — a worse jolt than the freeze.
  - **A/B on one build** (`[Debug] Smoothing = new|legacy`, legacy = the exact v0.7.7 code path), same stream: legacy walk `std 1.164`, max frame-to-frame speed step **11.96 m/s**, 37 frozen frames; legacy run `std 3.682`, maxstep **35.98**, 31 frozen. **new** walk `std 0.084`, maxstep **0.080**, **0 frozen**; **new** run `std 0.238`, maxstep **0.237**, **0 frozen**. Per 0.25 s window the steady state is a **2–6 % speed spread** (legacy 60–250 %); the windows right after a 100 ms dropout 15–30 % (a smooth dip). Packet format unchanged (31 B) → 0.7.7 and 0.7.8 interoperate, only the rendering differs.
  - **`[Debug] NetSim` (diagnostics, no network needed)**: `NetSim = true` feeds the plugin its own synthetic stream through `ProcessPacket` and writes `<plugin>/netsim_*.csv` — one row per frame (`t;x;z;yaw;speed;n;iv;tr;k0;k1;dtp;steps`, **decimal comma**) plus `netsim_*_pkt.csv` per packet — then flips the flag back to `false` after `NetSimSeconds`. Offline helpers live in `%TEMP%` (`simseg.ps1` = 0.25 s windows, `animsim.ps1` = std/maxstep/freeze per phase, `simdiag.ps1` = outliers).
  - **`GlbAnimator`**: walk/run clip weights are rate-limited (`MoveTowards` 3.5/s + renormalisation) — a speed spike used to snap the blend and read as a limb jolt.
- **v0.7.7 (gait direction + a jab that is a real push)**: (1) **run sped up 80 %**: `GaitScaleRun` 0.34 → **0.61**, walk stays 0.5. (2) **backing up rewinds the clip**: `remoteVelLocal` = `Quaternion.Inverse(remoteGo.rotation) * (rendered delta / dt)` (EMA 10, from the spline movement only) → `SetMotion(speed, pitch, localVel)` sets `gaitDir` (±1, smoothed) and the phase advances `dt * cadence * gaitDir`; the greedy bug was a `|gaitDir| < 0.15` clamp that pinned the sign just above zero — the phase slowed to 0.15 and **never** crossed zero, so "back" still played forward. Removed; log proof: phase `0,68 → 0,61 → 0,52 … 0,04 → 0,94` while the partner walks backwards. (3) **strafe** (`ApplyLateral`): Soldier has no side clips, so the thighs splay ±20°·|lat| about the avatar's **forward** axis in time with the gait (`step = sin(2π phase)`), the spine leans 8° into the movement and the hips sway 4.5 cm; `lateralBlend` = the normalised x of the same velocity. (4) **item use = 0.094 s jab** (`0.75f / PushSpeed`, `GlbAnimator.PushSpeed = 8`), envelope `k = sin(π p)`, no wind-up. (5) **the hand never goes up**: v0.7.6 used fixed angles, but `Twist` on the forearm *adds* to the shoulder rotation — measured `handY 1,50` with the shoulder at `1,41`, i.e. arm overhead (exactly the user's complaint). `SolveArmAngle` now **bisects** the shoulder rotation until `HandBone.y == hips.y + 0.15` (mid-body = 1.06 m for the 2.12 m avatar) and it must run **after** the torso lean + hip shift (a 22° lean alone drops the fist 19 cm → solved 1,06 but rendered 0,89). `SolveElbowSign` measures which sign of `Twist(lForeArm, 20°)` *lengthens* the arm (mirror rig) and extends 24°·k, so the reach is 0.43 m forward at the peak (`handZ −8,21` vs `−7,78` idle); torso lunges **+16°** (the v0.7.6 −8° leaned backwards — wrong sign) and the hips shift 0.14 m forward. Verified: `[PUSH] k=1,00 aim=63,3 midBodyY=1,06 handY=1,06 handZ=-8,21`, anchor `fwd=(0,0.11,-0.99)` (horizontal) at the peak. (6) `HandFollow` damping is eased during the push (pos τ 0.06→0.008, rot τ 0.12→0.01, cap 240→4000 °/s) or a 0.094 s jab disappears. (7) preview timeline is now walk → crouch → air → run fwd → **run back** → **strafe** → 5 pushes (edges every 0.2 s, 10.2–11.2 s). A thin handle at `ItemScale 0.5` (1.3 cm) makes the prop look detached in a still — the pivot is a constant `d=0,245` from the fist.
- **v0.7.5 (feet on the ground, half-size props, club points up)**: (1) **feet on the ground** — the avatar model was already fine (`FeetAligner`, a 3-frame `LateUpdate` that bakes the skinned mesh and drops the model by `min y`, logs `[GLB] feet on ground: lowest vertex was 0,000 m` → the bind-pose normalisation was exact); the real cause is that the packet "feet" are the **bottom of the sender's CharacterController capsule**, which Unity holds ~a skin-width above the surface. Receiver-side fix `GroundSnap(pos, airborne)`: `Physics.Raycast(feet+0.45·up, down, 1.1)` → put the model on `hit.point.y`, skipped while airborne (bit1), skipped for player capsules, only for corrections ≤0.32 m. Verified in the preview with a reference floor and a deliberate +12 cm spawn: `probe hit=COOP_PreviewFloor hitY=-0,300 raw y=-0,180 snapped y=-0,300`. (2) **props are half size in third person**: new config `[Debug] ItemScale` (default 0.5) multiplies the fit scale → a long prop is 0.55 m × 0.5 = **0.275 m** (`[COOP] held item fitted: ... scale 0,30, world len 0,28 m`) — *superseded in v0.7.6: no 0.55 m normalisation any more, and the fit now also covers the non-readable meshes, which is why the Club ignored this entirely*. (3) **the club points UP**: `HandFollow` aim is now `-dir*0.75 + Vector3.up*0.25 + fwd*0.75` (dir = hand − foreArm), i.e. ~70° above horizontal out of the fist, and the swing sweeps it down to horizontal-forward instead of dangling under a raised fist. The preview studio camera was widened (target `+0.85 m`, distance 3.9) so the feet/floor are in frame, and the preview floor keeps its collider because the ground snap needs it.
- **Airborne debounce (v0.7.2)**: `CharacterController.isGrounded` flickers on steps/slopes, and the jump pose popped in and out (twitching). `TrackUseEdge` (per frame) keeps `airTimer`/`airState`: airborne only after 0.12 s off the ground; `SendOwnPose` sends `airState`, not the raw flag.
- **Fire1 edges (v0.7.1)**: `Input.GetButtonDown("Fire1")` is sampled in `Update` every frame (`TrackUseEdge`, 0.25 s latch). It used to be read inside `SendOwnPose`, which returns early 2 frames out of 3 to hit 20 Hz — that dropped ~⅔ of clicks ("удар работает через раз").
- **Procedural poses (v0.7.0, replaces the missing crouch/jump/use clips)**: after the clip pose the animator applies *world-space* bone rotations (`Twist(bone, deg, axis) = AngleAxis(deg, right) * bone.rotation`), so nothing depends on mixamo's local axes. Crouch: hips −0.40 m + Spine2 +14°, thighs −60°, shins +112°, feet −50° (angles solved so the soles land under the hips). Airborne: thighs −38/−28°, shins +62/48°, feet −22/−18°, arms −32°, spine +6°. Item use: one-shot 0.60 s swing (v0.7.1: on `LeftArm`, the visually right arm — see the mirror note above; +35° windup → −105° strike → return) plus forearm/spine follow-through, triggered by the rising edge of the use flag. Blends are `MoveTowards` (`crouchBlend` 7/s, `airBlend` 6/s). Verified in-game via the preview mode (see below).
- **glTF skinning in the loader**: JOINTS_0/WEIGHTS_0 + skin.inverseBindMatrices; joint matrix = `inverseBindRow * jointWorldRow`. glTF matrices are column-major → filling a `System.Numerics.Matrix4x4` sequentially yields the transposed (=row-convention) matrix.
- **Pose from animation**: `Parse(path, poseAnimation="auto")` applies first-keyframe TRS overrides of the chosen animation (`Idle` preferred, else first; `""` = bind pose). Soldier bind pose = T-pose, Idle = standing ready pose. `Parsed.Nodes/Skins/Animations` carry the full runtime data. CesiumMan.glb downloaded too (no animations, skinned).
- Soldier facts: Character root node `R = -90° about X, S = 0.01` (cm → m); 68 nodes, 2 skins (49 + 2 joints); 52 animated bones; **no root motion** (Hips translation sways ±5–8 cm); clip durations Idle 1.97 s, Walk 1.03 s, Run 0.70 s, all LINEAR.
- Not possible with this model: **crouch / jump / item-use** clips do not exist (Soldier only ships Idle/Walk/Run/TPose), so they are procedural (see above). Crouch/jump position also syncs via the packet feet position.
- Verified Kenney Blocky GLB facts (character-r.glb, UnityGLTF export):
  - Rigid node tree: root → leg-left `[T(0.2,1.0,0), mesh0]`, leg-right `[T(-0.2,1.0,0), mesh1]`, torso `[T(0,0.7,0), mesh2]` → arm-left `[T(0.4,1.1,-0.1), mesh3]`, arm-right `[T(-0.4,1.1,-0.1), mesh4]`, head `[S(0.1) T(0,1.2,0), mesh5]`. After the order fix → world height **2.7** (legs 0..1, torso 1.0..1.9, head 1.9..2.7), X ±0.8.
  - UVs are out-of-range (u ≈ -0.53..0, v ≈ 1.52..2.0); sampler wrap = REPEAT → wrap with `uv -= floor(uv)` per component (loader does this).
  - glTF v axis is TOP-DOWN: Harness rasterizer samples row `ty = v*H` directly; **Unity converter uses vUnity = 1 - v**.
  - Texture is EXTERNAL: `"images":[{"uri":"Textures/texture-r.png"}]` next to the GLB (loader reads file / data-uri / bufferView). 1024×1024.
  - KHR_texture_transform present but identity (`texCoord:0` only); KHR_materials_unlit, baseColorFactor white.
  - Unity conversion used in GlbAvatarBuilder: **positions passed through UNTOUCHED** (no axis negation!). Interpreting RH glTF data directly in Unity LH gives the correct front-face winding by itself AND front = Unity +Z. (Earlier attempts: 180° yaw bake → model faced -Z "backward"; mirror z would need winding flip.) `vUnity = 1 - v`. Model raw height **2.7** (legs 0..1, torso 1.0..1.9, head 1.9..2.7), X ±0.8.
  - **Node-transform order trap**: .NET `Vector3.Transform` uses ROW vectors → world matrix per node must be `local * parentWorld` (NOT `parentWorld * local`). With per-node scale (head 0.1) the wrong order scaled the torso's translation into the head → head rendered inside the torso (v0.5.0 bug, fixed v0.5.1).
- Offline visual verification: `Harness/` (net8 console, System.Drawing.Common + Newtonsoft nuget; compiles `..\BabbdiCoop\GlbModelLoader.cs` via Compile-Link). `dotnet run --project Harness` renders `harness_out\glb_front.png` / `glb_back.png` (painter's algorithm, UV-sampled); `-- <clip>:<time>` (e.g. `Walk:0.4`, `Run:0.35`) renders one animated frame through `Harness/PoseSampler.cs` (same row-matrix skinning as the runtime). Kenney OBJ vt live in [-1,1] about the atlas center and the atlas PNGs have baked labels — OBJ UV path is a dead end, use GLB.
- **In-game visual check (`Debug.PreviewAvatar`)**: set `[Debug] PreviewAvatar = true` in `<game>/BepInEx/config/com.babbdi.coop.cfg`, launch the game, wait ~20 s. The plugin spawns the avatar in front of the menu camera, runs a scripted timeline (idle → walk → crouch → air → run → swing) and writes 480×480 studio renders (own `Camera` + `RenderTexture` + directional lamp, `LateUpdate`, one every 0.12 s) to `<game>/BepInEx/plugins/BabbdiCoop/preview/preview_NNN.png`. Shot index ≈ `t / 0.125`; the swing lasts 0.60 s so it shows up in ~4 frames. The mode flips the config back to `false` when done. Verbose `[ANIM]` lines go to `%LOCALAPPDATA%Low\LemaitreBros\Babbdi\Player.log`.
- **Render-side fixes (v0.7.0, targeting the "flickering stretched textures" report)**: glTF weights are **renormalised to 1** (Soldier sums were 0.98–1.0; Unity does not renormalise runtime weights, so vertices oscillated while animating); textures get a mip chain + Trilinear + aniso 2 (no shimmer at distance); avatar renderers `receiveShadows = false` (self-shadow acne on a deforming mesh); `motionVectorGenerationMode = Object` + `skinnedMotionVectors = true` (BABBDI ships `Unity.Postprocessing.Runtime.dll`, so TAA/motion blur would smear a skinned mesh left in the default `Camera` motion-vector mode); `QualitySettings.skinWeights` is forced to `FourBones` if the active quality level is lower. The preview camera has **no** Post Process Layer, so post-FX artifacts can only be judged in normal play.
- **NEVER edit .cs files with PowerShell `Set-Content -Encoding UTF8`** — it double-encodes Cyrillic (UTF-8 bytes read as ANSI). Recovery: `UTF8.GetString(bytes)` → `CP1251.GetBytes(text)` → `UTF8.GetString(...)`, then fix the leading `?`/BOM char manually (`?using` → `using`). Use the edit/write tools for sources.
- Diagnostics HUD (top-left while net active): role, sent/recv counters, age of last packet; LogOutput.log unreliable with 2 instances, trust HUD + [Net] trace lines + dump.txt.

## Flying (`леталка`, the propeller)

- **v0.9.15: the flight ceiling is patched.** `Propeller.Update` compares the player's **world y**
  against a hard-coded **120 m** three times — `player.transform.position.y < 120f` for the Space lift and
  twice for the Fire1 forward thrust (`ldc.r4 120` in IL). That ceiling was *below the district's own
  roofs* (the platform deck is at y ≈ 85,5, the second-tallest block's roof at ≈ 276 m), so the memorial
  could not be reached by flying at all. `BabbdiCoopPlugin.FlyCeilingTranspiler` (Harmony, applied in
  `Awake` after the config is bound) replaces **every** `ldc.r4 120` in that method with
  `[Game] FlyMaxHeight` (default **600 m**), and logs the count:
  `[FLY] propeller ceiling 120 m -> 600 m (3 constants replaced in Propeller.Update)`. A game update that
  changes the constant shows up as `0 replaced` — it never fails silently. `FlyMaxHeight <= 0` disables the
  patch (stock 120 m).
- **The camera's far plane follows** (`TickCameraFar`, one comparison per frame, idempotent so a scene
  change that re-creates the camera is covered): if `farClipPlane < FlyMaxHeight + 800` it is raised and
  logged once (`[FLY] camera far clip 1000 -> 1400 m`). Otherwise flying at the new ceiling would clip the
  world away.
- Only the propeller has a ceiling in the whole build (`Climber`, `Blower`, `Grabber`, `Club` have none —
  checked in the decomp), so nothing else needs patching.
- Harmony itself is already referenced by the csproj (`using HarmonyLib;` was there since v0.5.0, this is
  the first actual patch); `System.Reflection.Emit` is needed for `OpCodes`/`CodeInstruction`.

## Soviet district (v0.9.23)

Builds a new district of Soviet blocks in the sea behind the dam, once per level load. v0.9.2 rebuilt the
layout from the user's plan (platform with a western tongue, blocks in a **ring around the edge**, the House
of Soviets in the open middle). v0.9.3 keeps that plan but makes everything monumental: every block is
**+30 % in all three dimensions**, the House is **+45 %**, all wall thicknesses are **+80 %**, and **every
covered bridge crosses at the 11th storey**. v0.9.8 moved the platform off the round hole in the sea and
extended it to the new contour; **v0.9.9 makes it multi-level** — five raised floors, inclined roads and
street stairs (see the first section below). **v0.9.13 widens the corridors 2x/3x/4x** (the houses get
deeper to pay for it) and puts **4x the grain** on the platform's own floors. **v0.9.14 adds the memorial** —
the red-glowing ring of concrete piers on the roof of the second-tallest block. **v0.9.18 adds the
penthouse** — the four-storey clock tower on the outlying block on the western wing (the building on the
peninsula). **v0.9.19 puts two crest blocks on legs** — their first three storeys are replaced by splayed
pylons over an open, lamp-lit base (the user's Brutalist reference). **v0.9.20 joins that pair into one
house in plan** (65 x 28 m, one half lower) and **levels the platform's south-east corner** next to the
round hole in the sea (the +5 m east terrace is set back 12 m; see below). **v0.9.22 rebuilds that corner as
one smooth height field** (no step, no seam kerb) and puts **five BABBDI-style figures** on the House of
Soviets' roof.

### v0.9.22: the south-east corner is **one smooth surface**, and five BABBDI-style figures on the House roof

Two user requests in one round: (1) «Удали эту "ступеньку" (скрин 1) в зоне над которой ты только что
работал» + «переход у этой зоны (скрин 2) должен быть плавным (как скрин 3)» — the +5 m / corner transition
next to the round hole in the sea; (2) «проанализируй анатомию гуманоидных NPC из самой бабди и создай 5
новых персонажей на основе данных сохранив общую стилистику, габариты, цветовую палитру и пропорции тела.
Пусть они стоят на крыше Дома советов вдоль края на равном расстоянии».

1. **Why there was a step — measured, not guessed.** `CornerScan()` fires a ray every 0,5 m across the whole
   corner (`corner walk x-profile z=6`, `corner walk z-profile x=340`) and prints the height. What it found:
   the level's own **granite shelf sits at +0,32 m over the entire corner** (x ≈ 330-376, z −4 … 10), our deck
   is at 0, and the v0.9.20/21 pad was **four separate `Beam` boxes** (a 0,40 m plate, a 4 m ramp, a tongue,
   a 10 m bevel), each a *closed* box with its own 0,9 m skirt, meeting only along their edges — so one box's
   top could stand **up to 8 cm proud** of the next one's at the same z (the plate's 0,40 against the ramp's
   0,32). That was the user's screenshot 1; the 12 cm **seam kerb** of `Deck()` was the hard edge of
   screenshot 2.
2. **Now the corner is a single analytic surface** `h(x,z)` — `CornerPad()`, a triangulated height field on a
   2 m grid, **no internal edges at all** (`g.Quad` per cell, per-cell analytic normals, one `corner_pad`
   mesh collider): flat **0,365 m** from `XF = 328` to the sea parapet and `Z0 = 0,15` to the terrace's wall
   (`z = CornerApron + 0,3`), a **16 m smoothstep** down to −0,35 m on the west (`XF → XW = 312`; it crosses
   the deck at x ≈ 320 and disappears inside the slab), and a tongue from `XG = 332` to `ZN = −1,6` sinking to
   **0,30 m — 2 cm under the granite**, so the concrete *emerges* from under the stone instead of abutting it.
   Every free edge is buried (under the granite, inside the deck slab, inside the terrace box, inside the sea
   wall), so the corner has **no visible boundary** and nothing sticks out (the tongue's skirts are emitted
   only if their top would rise above 0,319 m — it never does). **The seam kerb is deleted** from `Deck()`.
3. **The evidence is numeric, not a render** (`[PROBE]`, one real game run):
   `corner walk x-profile z=6: 0,00 ×16 → 0,01 0,04 0,07 0,11 0,14 0,17 0,20 0,22 0,25 0,27 0,29 0,31 0,33 0,34
   0,35 0,36 …` — biggest **0,5 m step 3,3 cm** (a constant 6,6 % ramp, i.e. a ramp, not a step) and
   `corner walk z-profile x=340: 0,32 0,32 0,32 0,32 0,33 0,34 0,36 …` — biggest **1,9 cm**, with the two
   >1 m jumps reported separately as `a wall, not the floor`. Probes: `corner pad (its top; the granite under
   it is 0,32): 0,36 m (corner_pad)`, `base deck west of the ramp's foot: 0,00 m (deck)`,
   `south-east corner, behind the pad (+5): 5,00 m (terraces)`; `[DISTRICT] corner pad: … ONE smooth height
   field … 367 quads`.
4. **The figures are measured off the game's own people** (`[NPCSCAN]` on the loaded scene: heads 0,31-0,51 m
   on 1,79-2,23 m figures ⇒ **≈5 heads**, not the 7,5 of a realistic adult), and the reference stills
   `map/district_0` show the style: a **big pale bone-white mask** instead of a face (bald, with only small
   dark eye slits and a mouth line), a **muted photoreal palette** with **one strong orange** (the worker's
   jumpsuit), chunky boots and — the signature detail — **long thin splayed fingers**. `Figure()` now builds
   exactly that: 5 heads (chin = 1,48 k, head 0,37 k), the whole head in the `mask` colour with only the neck
   and the hands in skin, hair/hat only as a crown on top of the mask (one figure is bald like the game's own
   worker), arms 0,29 + 0,24 m with a 0,08 m palm **plus four 0,095 m fingers and a thumb** (fingertips at
   mid-thigh), and new `mask` (0,87,0,83,0,75) / `orange` (0,70,0,28,0,16) entries in `FigMaterial`.
   The five are still new characters, not clones: builder (grey jacket, flat cap), engineer (white shirt,
   rolled drawing in his hand), woman worker (headscarf, teal skirt, arm raised), official (charcoal suit, red
   tie, hands behind his back) and the broad worker in the **orange jumpsuit** — 1,70-1,90 m, 20 m apart on
   the podium roof's front edge (1,25 m inside it), facing the city. `[FIGURE] … 1098 quads in 12 colours`,
   `[PROBE] House roof figure 5 (chest, from the city side): 5,88 m (figures_orange)` and
   `[PROBE] House roof, at the middle figure's feet: 35,75 m (podium)`.
5. **Shots**: `district_52`-`district_56` the corner (eye level from the west, from the deck, 3/4 and 24 m
   above, the ortho plan), `district_57` walking in **along the crest's granite** (`PL(348,1.75,−20 →
   352,0.5,6)`), `district_58` standing on the plate looking back along the seam and the bevel, and
   `district_59`-`district_64` the figures: from the plinth below (a line against the sky), the group, the
   middle figure up close, along the row, a 3/4 close shot and **the orange worker head-on** (district_64 —
   the palette and the mask read exactly like the game's own worker).

### v0.9.23: the corner's **south** side is a ramp too — the plate that blocked the way is gone

The user's follow-up with the same spot outlined in red: «на скриншоте обвел красным по контуру ту часть,
которую я хотел чтобы ты убрал, она мешает проходу». The v0.9.22 surface was smooth **in x**, but its
**south** boundary was not: measured, not guessed.

1. **What was actually in the way (`[PROBE] corner steps`, 2 m map + neighbour list).** The pad's flat top
   ended at `z = CornerApron + 0,3 = 12,3` in a **bare vertical face**, 0,25-0,36 m tall and ~10 m long
   (x ≈ 320-330), with nothing under it — the sheet simply stopped. `CharacterController.stepOffset` is
   **0,30 m**, so walking north into the corner from the base deck ran into a wall, and from the west the
   face read as a **long narrow plate standing proud of the floor** next to the terrace's wall — exactly the
   shape the user outlined (and the underside of the sheet was visible from the south).
2. **The fix — the height is now one `lift` that vanishes on every free side.** `CornerPad()`'s
   `height(x,z) = HW + (north(z) − HW)·(1 − wx)·(1 − wz)`: `wx` is the 16 m west bevel (`XF 328 → XW 312`),
   `wz` the new **4,5 m south bevel** (`zS = 12,3 → zS + 4,5`). Both cross the deck's plane and dive
   **inside the deck slab** (`HW = −0,35 m`), so the surface has no free edge anywhere — the south face is a
   slope you walk up. The rows are no longer uniform: 9 rows over the flat top + 6 rows down the south bevel
   (0,75 m each), 571 quads, one `corner_pad` mesh collider.
3. **The top is now flush with the crest's stone**: `H` 0,365 → **0,345 m**, i.e. **2,5 cm** over the level's
   granite (0,32) instead of 4,5 — the concrete reads as the stone's own level, so nothing on the corner is
   a "plate" any more. (`HN = 0,30` under the granite at the tongue is unchanged, so the concrete still
   emerges from *under* the crest's stone.)
4. **Proof, two probes, no render needed**:
   - `[PROBE] corner steps: 0 lip(s) of 0,20-1,60 m inside the walkable corner (x 310..374, z 0,3..11,9)
     between 2 m samples; 87 samples are the designed boundary (the platform's rim over the drop, the
     terrace's rail, the sea parapet)` — was `88` with `25,3-36,5 cm at (324,12) → (324,14)` in v0.9.22.
   - `[PROBE] corner faces: 22738 rays (245 x 47 at 0,25 m over x 312..373 / z 0,3..11,8), biggest jump
     between neighbours 1,6 cm at (320,8 / 2,8), 0 jump(s) over 8 cm (= a 32 % slope, i.e. a vertical face)`
     — the whole walking surface is a continuous ramp, its steepest point 6,4 %.
   - `[PROBE] corner south bevel z-profile x=325 (z 8..20)`: `0,28 ×9 → 0,26 0,23 0,16 0,07 0,00` — the
     transition is 3 m of slope and then the deck; at x=316 the pad is entirely below the deck (`0,00` all
     the way), so the bevel has no edge to trip on either.
5. **Shots**: `district_65` is the user's own framing (standing on the deck south-west of the corner,
   looking east: the +5 terrace's wall on the left, the corner in the middle, the crest's granite and the
   city's mast on the right) and `district_66` is the same transition walked straight up from the south.
   In both the floor, the bevel and the crest's stone are one continuous surface.
6. **Nothing else changed** — the layout, the blocks, the House, the memorial, the clock tower, the terraces'
   extents and the [District] `CornerApron` knob are untouched.

### v0.9.24: the five figures are **the game's own characters**, not boxes

The user: «Не строй с нуля из примитивов, а возьми скелеты этих персонажей, слегка измени пропорции и
сгенерируй для них новые текстуры и уникальные выражения лиц. Должно получится как можно более приближенно
к оригиналу, не так угловато как оно есть сейчас. Найди их модели в файлах и используй как референсы.»

1. **The game has no character skeletons.** `[NPCSCAN]` over the loaded `level1` found every NPC to be a
   **static posed mesh + a separate head mesh** (`bones = 0`, no `SkinnedMeshRenderer` in the whole build),
   so "take their skeletons" was read as "take their models": `SM_A_Enki` (the only one standing neutrally
   with the arms down — Ashraf has them out, MacLeod one up, the rest crouch/squat) is the body for all
   five, and each figure wears **a different one of the game's own heads** — five genuinely different
   photo-scanned faces instead of five painted masks.
   `builder` → `SM_Arlend_00_Head`, `engineer` → `SM_Amir00_Head00`, `worker (woman)` →
   `SM_Cinderella_00_Head01` (headscarf), `official` → `SM_Ivano00_Head`, `worker (jumpsuit)` →
   `SM_A_Enki_Head00` (the pale bald mask of the game's own worker).
2. **Proportions are warped per figure, in metres.** `warp_apply()` remaps the body's normalised height
   (legs/torso stretch independently around a 0,545 waist) and scales the radius around four Gaussian bands
   (shoulders 0,795 / chest 0,685 / hips 0,525 / knees 0,27) plus a girth ramp and a belly bulge. Result:
   builder 1,86 m grey jacket + dark trousers, engineer 1,80 m lean white shirt, woman worker 1,70 m with
   narrow shoulders (0,905) and wide hips, official 1,88 m slim long-legged charcoal suit, broad worker
   1,92 m (+10 % shoulders, +14 % chest, +10 % girth, belly +3 cm) in the orange jumpsuit. Every head is
   scaled by `0,185·h / head_height` so all five stay **five heads tall** — the measured ratio of the
   game's own people (v0.9.22 `[NPCSCAN]`: 5,0-5,2 heads on 1,79-2,23 m bodies).
3. **New textures from the game's own atlas.** `t_palette` repaint is **luminance-based**, not procedural
   noise: each texel keeps its own photo-derived luminance (folds, seams, the fabric's grain, even the
   original photographic print) and only its **colour** is replaced, by the height band it belongs to
   (shoes / trousers / jacket). Palette per figure: jumpsuit orange, charcoal, white shirt, work-jacket
   grey, ochre + teal. The bright texels (tracksuit stripes, the gloves at the waist) keep a second
   "trim" colour **only on the front half** (`zf > 0,02`) — the atlas maps the same texels onto the back,
   so a texture-only trim painted white **bandage bands across the back** (that was the first build).
   In the end all trims are off: the game's own garment is a plain tracksuit and a uniform colour reads
   cleaner than a fake two-tone.
4. **Two traps worth remembering** (both burned a build): the body's UVs are **tiled** (u −0,66…0,89), so
   the UV rasteriser has to shift each triangle by its centroid's integer floor and write with `np.mod`
   — without that most of the body was left un-recoloured (black torso, original blue gloves); and the
   palette is in 0..1 while the texture is 0..255, so a missing `×255` renders the whole garment black.
   UV padding (12 dilation rings) removes the dark atlas-background rim at every island edge.
5. **Shipped as GLBs, loaded by a new `GlbFigureBuilder`.** `figs.py` (harness, `%TEMP%\opencode\npc`)
   writes `fig_1..5.glb` into `BabbdiCoop/model/figures/` (~0,75-1,05 MB each: body + face + hair
   primitive, 5,5-20 k tris) and `build.ps1` copies them to
   `BepInEx/plugins/BabbdiCoop/figures/`. `GlbFigureBuilder` is the **static** counterpart of the avatar
   loader: it merges the primitives into one `Mesh` with one submesh per material, `UInt32` indices,
   `RecalculateNormals`, a `BoxCollider`, and builds the materials on the game's own surface shader with
   `_BC` (so they take the probe SH exactly like the level's concrete); the **third primitive (hair) is a
   `Standard` alpha cutout** (`_ALPHATEST_ON`, cutoff 0,45, queue 2450) because those atlases are alpha
   cut-out strand cards.
6. **Placement unchanged**: `SovietDistrict.Figures()` only places them — the podium roof's front (city)
   edge, 1,25 m inside it, 20 m apart, facing the city (`yaw + 180°`, the model's face is +Z). They are
   parented to the district root in **local** coordinates, so the probes still see them.
7. **Evidence** (one map-shot run): `[FIGURE] figure_1_builder: 5764 verts, 3 materials, 1,86 m tall, feet
   at local (188,5, 35,8, 64,4)` … `figure_5_worker_(jumpsuit): 7786 verts, 3 materials, 1,92 m tall`,
   `[FIGURE] five figures … 5 of 5 loaded from …\BabbdiCoop\figures (the game's own models: SM_A_Enki's
   body warped per figure + five different heads … re-textured in a new palette, 1,70-1,92 m tall, five
   heads high)`, and the two probes: `[PROBE] House roof, at the middle figure's feet: 35,75 m (podium)` +
   `[PROBE] House roof figure 5 (chest, from the city side): 5,66 m (figure_5_worker_(jumpsuit))` — a ray
   from 6 m out at chest height is stopped by the figure's own collider, i.e. they stand **on** the roof
   and **in front of nothing**. Layout untouched: `built 36 buildings, 0 skipped plots, 22 bridges,
   71068 quads`, `[PROBE] done: 43 ok, 0 misses`.
   Shots: `district_59` (the row from the plinth below — five small figures along the edge), `district_61`
   (the woman: ochre jacket, teal trousers, headscarf, 1,70 m), `district_62` (along the row from the roof,
   the official in the dark suit nearest), `district_63` (the woman at eye level, the row receding),
   `district_64` (the orange worker head-on — the game's own jumpsuit and pale mask, the palette match).
   **v0.9.25 supersedes all five** — the same poses now frame the new characters (61 = the official,
   64 = the soldier, 67 = the scientist, 68 = the docker).
8. **Known and accepted**: the game's body is a *jacket with a hood*, so a large flat hood panel lies on the
   chest (it is in the original asset, not a texture artifact); the hands are the game's own pale gloves,
   not skin; and the reflections/stitching of the tracksuit's photo survive the repaint as soft shading.

### v0.9.25: the five figures are **new characters** built from the user's references

The user's five reference photos (a woman scientist in a white lab coat and blue shirt, a docker in a navy
beanie with an orange hi-vis vest, a bald moustached official in an olive jacket, an officer in a peaked cap
and an olive greatcoat, a bearded soldier in a winter beanie and a camo jacket) plus three hard rules:
**do not assemble them out of the game's existing NPCs** (no `SM_A_Enki` body, no borrowed heads), keep
BABBDI's proportions and palette, «слепить из низкополигональных текстур».

1. **Built from scratch offline** in `figs3.py` (harness, `%TEMP%\opencode\npc`): each figure is generated
   geometry + a painted atlas, not assembled from game assets. **Two primitives** per figure (`skin`,
   `cloth`) on one **2048x1024** atlas — left half skin (`head` 896x1024 + `nose`/`ear`/`neck`/`hand`
   128x256 at x=896), right half cloth (`torso`/`coat`/`sleeve`/`legs`/`boot`/`hat`/`hair`/`beard`/`vest`/
   `detail`/`metal`). 2.7-3.4 k triangles, 1.56-1.87 MB per GLB.
2. **Roles and heights**: scientist 2.05, docker 2.11, official 2.16, officer 2.21, soldier 2.13 m — all
   **five heads high**, the ratio measured off the game's own people in v0.9.22 (`[NPCSCAN]`).
3. **Facing**: the game's NPC *bodies* face −Z while their *heads* face +Z, so the figures are built with
   the face towards **+Z** and `SovietDistrict.Figures()` keeps `yaw + 180f`. The v0.9.24 build had all five
   standing with their backs to the city (the user's first report).
4. **+20 %** on the whole figure (body, limbs, head) over the first build — 2.05-2.21 m instead of 1.70-1.92.
5. **Eyes are narrow dark slits, not balls** (`skin_at`): `eye_ry` 0.062/0.058/0.052/0.050/0.058 →
   **0.048/0.046/0.042/0.040/0.046**, the sclera is dimmed **×0.60**, the iris fills `d < 0.64` at **×0.88**,
   the pupil `d < 0.30`, the lid shadow ×0.46, plus a thin **lash line** (`d 0.66-1.10`, upper lid) — a wide
   white sclera around a small pupil is the one thing BABBDI's masks never do. The atlas is the limit here:
   one eye is ~38x28 texels, so anything finer than this smears.
6. **One continuous walrus moustache** instead of two hard ellipses (which read as two holes punched above
   the lip): a single band `hh = mh*(1 - 0.48·ax^1.6)` with a **0.28 notch under the nose**, a centre line
   that **droops towards the corners** (`cl = myc - 0.55·hh·ax²`), a **flat plateau with a fast falloff**
   (`k = clip(1-dy²)^0.22`, blend 0.95) and its own palette key `S["moustache"]` (official 0.430/0.405/0.375,
   soldier 0.255/0.205/0.155). Widths narrowed: official `moustache_w` 0.36 → **0.27**, soldier 0.50 → **0.36**.
7. **The officer's peak no longer hangs over his brows.** The visor plate used to drop `dy -0.105`, i.e. its
   dark underside covered the *whole* forehead down to the eyebrows and read as a black slot under the cap.
   It now hangs from **inside** the band (`ny 0.400`, radius 0.96) and tips only **-0.055** over 3.5 cm of
   forward reach (`dz` 1.20), with a stronger end taper (0.30) and a **lighter underside**
   (`S["visor"]` 0.150 → 0.235): a brim, not a hole, from the front and from below.
8. **The white band at the ankles was a hole, not a texture** (the user saw it in game). Both the trouser and
   the boot are *hollow open-ended tubes*, and the trouser ended at `boot_top + 0.01·H` — 1 cm **above** the
   boot's rim — so the roof showed straight through the ankle. The trouser now ends at
   `boot_top - 0.02·H` (inside the boot, radius 0.42·hipx against the boot's 0.50·hipx) and the boot's top
   ring is **capped** (`cap_start=True`), so the rim closes the annulus.
9. **Evidence** (one real game run, `[Debug] MapShot = true`, `LoadScene = Scene_AAA`): five
   `[FIGURE] figure_N_<name>: … verts, 2 materials, H,HH m tall, feet at local (…)` lines, the summary
   `[FIGURE] five figures on the House of Soviets' roof … 5 of 5 loaded from …\BabbdiCoop\figures (new
   characters in the game's own style …)`, `[PROBE] House roof figure 5 (chest, from the city side)` stopped
   by the figure's own collider, and `[PROBE] done: 43 ok, 0 misses`. Layout untouched: `built 36 buildings,
   0 skipped plots, 22 bridges`.
10. **Shots** — the v0.9.22 poses now frame the new cast: `district_59` (the row from the plinth below),
    `district_61` (the official up close: bald, moustache, olive jacket, red tie), `district_62` (along the
    row, the officer nearest), `district_63` (the official at an angle, the row receding), `district_64` (the
    soldier head-on: beanie, beard, camo jacket, vest) and the two added in this round — `district_67` (the
    scientist: white lab coat, blue shirt at the collar, dark bob, clipboard, navy trousers) and `district_68`
    (the docker: navy beanie, stubble, olive jacket, orange hi-vis vest, navy trousers).
11. **Known and accepted**: the faces are painted at an atlas resolution where an eye is ~38x28 texels, so
    they read as *masks* (a pale face with dark slits, a moustache band, a beard border) rather than as
    portraits — which is what the game's own cast looks like from a metre away. The figures are **purely
    local** (each peer renders its own `figures\*.glb`), so they are not synced.

### v0.9.80: the pilots get **real faces** and proper **flight helmets**

The user on v0.9.78: «Очень сыро… добавь им лица так же, как для прочих NPC — с блеклыми чертами, глазами
без зрачков; головы тоже должны быть неровными. И на голове у них какие-то каски строительные, а не
нормальные лётные шлемы — сделай в несколько раз более похожими и цельными». Five helmet references (the
MSA Gallet type) came with it.

1. **The head is lumpy now.** `head_deform()` (in `pilots.py`) applies the same recipe as the five originals:
   brow ridge 0,105, eye sockets −0,105, cheekbones +0,095, jaw angle ±0,062, chin, hollow temples −8,5 %
   and a 4,2 % low-frequency `snoise` irregularity damped 85 % on the face plane. The sphere went to
   **22 × 16** and `Build.sphere()` gained the `deform` hook (it was accepted but ignored).
2. **The face is painted like the other masks**: pale `skin` with a soft `fabric` grain, **pale eyeballs
   with no pupils and no iris** (0,90/0,89/0,83) inside a dark socket + ring, a lid shadow, thick brows,
   a nose shadow, the muted lip *fold* of v0.9.31 (not lipstick) and light stubble — all **asymmetric**
   (one eye 1,4 % lower and 0,8 % further out, the mouth tilted with `0.010·nx`).
3. **The helmet was rebuilt as one piece** from bands of the shell: a rounded dome, a **raised crest ridge**
   along the top (a 2-column strip over the crown), a **thick dark rim** all round the lower edge (3 rings,
   ~8 cm), two **dark ear plates with a round boss**, one **big visor** filling the whole opening
   (`band(0.31…-0.50, ±68°)`) with a **dark frame** (a top bar and two side posts, 8 mm proud), a **chin
   strap** from the ear plates under the jaw and a **mic boom** with a tip box. The neck seal stays.
4. **The visor is genuinely transparent and lighter**: glass `baseColorFactor (0,40, 0,47, 0,54, 0,34)`, so
   the face reads through it instead of a black slot.
5. **The "transparent fingertips" were the tube caps.** A cap fan whose winding ended up culled left the
   fingertip see-through. `Build.tube()` now emits **both windings** of every cap, so a cap can never
   disappear, whichever side the model is mirrored.
5b. **Second helmet pass (the same build, thicker and more recognisable).** The shell is deeper
   (`hrh` 1,13/1,09/1,13) with a **three-rib crest** (a centre ridge and two flanking grooves, 3,0 % proud);
   the opening is edged by a **fat padded roll** built from six rings (outer 1,085 → an inner lip at 0,930
   that closes the shell edge, so the wall reads as thick and you never see into the hollow); the **ear
   cups are now massive**: a bracket plate on the shell, a 14-sided cup ~16 cm across and 6 cm deep, a metal
   hub and a black knob, plus a cable boss; the visor got a **slide block** at the top of each rail; the
   chin strap ends in a **buckle** under the jaw and the mic boom in a **foam ball**. Painted: the boots no
   longer sample the helmet rect, knee pads and a shoulder seam were added to the suit.
5c. **Third pass (suit, neck, visor).** The **visor now covers the eyes only** — a 0.30…−0.10, ±54° band with
   a top bar, a bottom bar and two side posts, so the nose, mouth and chin stay open (it read as a knight's
   bevor before). The **neck runs up into the skull** (`chin + 0,10·head_h`, radii 0,168 → 0,130·head_h) so the
   jaw grows out of it with no seam, and a soft **suit collar** cone hides where the neck leaves the shoulders.
   The **chest gear is no longer boxes**: `plate()` builds tapered, rounded plates that follow the torso's
   front surface (rig + two pouches for pilot_b, a carrier plate + a small control box for pilot_a), with the
   straps and the flag patch unchanged. The suit's painted details were turned from hard rectangles into
   soft Gaussians (knee pads, chest and thigh pockets, shoulder seam) and the atlas blur went 1,1 → 1,35 px,
   which removed the banded "armour plates" look. Face/helmet from 5 and 5b unchanged.
6. **More suit detail**: knee pads, a shoulder seam, the chest rig, the belt, the knitted cuffs and the
   boot soles all painted in the atlas (the boots were sampling the helmet's rect and came out pale).
7. **Evidence**: `BabbdiCoop v0.9.80 loaded`, `[FIGURE] figure_6_pilot_a: 2444 verts, 3 materials … 2,22 m`,
   `figure_7_pilot_b: 2564 verts, 3 materials … 2,16 m`, `7 of 7 loaded`, `[PROBE] done: 55 ok, 0 misses`.
   Shots `district_72`/`73` (both pilots beside the row) and `district_74`/`75` (helmets up close: rim,
   crest, framed semi-transparent visor, ear plate, mic boom).

### v0.9.78: two **helicopter pilots** join the roof row

The user: «сделай ещё две модели по тому же алгоритму что и для первых пяти… 2 пилота вертолёта, они могут
быть похожи, но визуально слегка отличатся (уточнение: визор шлема опущен, он полупрозрачен). Поставь обе
модели рядом с остальными, на ту же крышу.»

1. **The generator had to be rebuilt.** `figs3.py` lived in `%TEMP%\opencode\npc` and was wiped by the
   temp cleanup, so a dedicated pipeline was written for the two pilots: **`%TEMP%\opencode\npc2\pilots.py`**
   (plus `preview.py`, an offline z-buffer previewer, and `fix*.py` patch scripts). Same conventions as the
   five originals: metres, feet at y = 0, face +Z, one 1024² atlas, primitives **skin / cloth / glass**.
2. **The semi-transparent visor is a material rule, not a trick.** `GlbFigureBuilder.Load` now reads the
   GLB material's `baseColorFactor`; if **alpha < 0,99** the primitive becomes a real transparent
   Standard material (`_Mode 3`, SrcAlpha/OneMinusSrcAlpha, `_ZWrite 0`, `_ALPHABLEND_ON`, queue 3000,
   `_Color` = the GLB tint). Flipping the visor down is then just geometry + a tint of
   `(0,30, 0,36, 0,42, 0,42)`.
3. **The two pilots** (both grey flight suits, black gloves and boots, a round flight helmet with the visor
   lowered over the brow/eyes/nose, a dark neck seal, ear cups and a brim):
   - **pilot_a — 2,21 m**: dark grey helmet, **open chin** (the face reads under the visor), a **chest
     control unit** with a hose jack — the first reference;
   - **pilot_b — 2,15 m**: **white** helmet with a dark band, a **black chin mask**, and an **olive chest
     rig** with pouches and the **flag patch** on the left shoulder — references 2-3.
4. **Seven on the roof.** `FigNames`/`FigLooks` are 7 long, the row is **re-centred** (`(i - 3) * 4f`, was
   `(i - 2)`) so the five originals keep their places and the two pilots take the eastern end; every
   `FigFeet.Count == 5` guard became `== 7`, and four new close-ups were appended (full body + a head shot
   on each visor) so the earlier shot numbers stay put.
5. **Evidence**: `BabbdiCoop v0.9.78 loaded`, `[FIGURE] figure_6_pilot_a: 2161 verts, 3 materials …
   2,21 m`, `[FIGURE] figure_7_pilot_b: 2391 verts, 3 materials … 2,15 m`,
   `[FIGURE] the seven are on another Stalin roof now … 7 of 7 loaded`, `[PROBE] done: 55 ok, 0 misses`,
   `Figures: 7 GLB`. Shots: `district_72`/`73` (the two pilots beside the row), `district_74`/`75` (their
   helmets up close — the lowered visor).
6. **Known**: at this scale the visor's interior is shadowed, so it reads dark rather than showing the face
   clearly; the alpha blend is real (0,42) and the tint lightens it against the sky.

### v0.9.77: the **map itself** — 2x footprint, 2x sky height, the invisible ceiling gone, only the edge barriers kept

The user: «Измени параметры самой карты Бабди: увеличь высоту скайбокса в 2 раза и площадь карты в 2 раза
во все стороны. Также убери невидимый потолок в небе, из невидимых барьеров оставь только те что не
дают игроку выпасть за карту». Implemented in `MapEdits.cs`, applied once per level load.

1. **Measured first** (`[Debug] MapScan` → `mapscan.txt`): the level has **no `RenderSettings.skybox`**
   — its sky is the **`SM_Cloud00/01` layer** (16 renderers, centres y 1437–2131, 274/129 m thick).
   `---INVISIBLE WALLS` holds 159 children: the **tall invisible air ring** (9 walls, 296.5 m high,
   y 74–371) around the city, the **invisible ceiling `Cube (12)`** (1 m thick, 1594 x 3578 m at
   y = 367), ground-level fences, indoor/outdoor door blockers, 3 water surfaces and the **`KILLZ`**
   (6970 x 6970 m at y = −92, trigger).
2. **Map footprint x2**: the 9 map-edge ring walls are moved **twice as far from the ring's centre**
   `(−111, −205)` and their XZ sizes are doubled; their **height is doubled keeping the bottom**, so the
   ring still encloses the 600 m flight ceiling (new footprint **2005 x 6029 m**, height 645 m, vs
   ~1002 x 3014 m). The ring stays closed because positions and segment sizes scale together. The
   `KILLZ` already covers the enlarged footprint, so it was left as-is.
3. **The invisible ceiling is gone**: the 1 m horizontal plate `Cube (12)` at y = 367 is disabled
   (`SetActive(false)`). The 600 m flight ceiling is now the only vertical limit.
4. **Only the fall barriers remain**: every other invisible collider in that root is disabled —
   **66 inner barriers** (the ground fences, the `Indoor`/`Outdoor` door blockers, small cubes) plus the
   separate **`DoorInvisibleCollider`**. The **map-edge ring**, **`KILLZ`** and the **water surfaces**
   (3) are kept; 5 empty/zero-size leftovers are skipped. Triggers are untouched.
5. **Sky height x2**: the 16 cloud layers are moved **twice as high** above the city (base y = −20) and
   their Y scale is doubled — top **4347 m** (was ~2170). The camera far plane follows:
   `MapEdits.SkyFar = 5547 m` and `TickCameraFar` uses `max(flyMaxHeight + 800, SkyFar)`, so the raised
   sky is no longer clipped. (There is no skybox material to scale; the clouds *are* the sky.)
6. **Config** `[Map]`: `Expand = true`, `AreaScale = 2`, `SkyHeight = 2`, `RemoveInnerBarriers = true`.
   The log is the evidence: `[MAP] expanded: 9 map-edge walls scaled x2 around (-111,-205) — footprint
   2005x6029 m, height 645 m; invisible ceiling removed (1), inner invisible barriers removed (66),
   door blockers removed (1), water 3, KILLZ 1, empty 5; 16 cloud layers raised x2 (top 4347 m, camera
   far 5547 m)`. The district and its probes are untouched (`[PROBE] done: 55 ok, 0 misses`).

### v0.9.97: the hatch finally sits **on the sloped aft underside at the boom's root** — doors folded back inside the silhouette

After four failed cuts (each one read as "a hole in the belly") the user set the rule: «резать можно, но не
настолько же много, люк всё равно должен быть на сильно наклонной поверхности … мы не должны сильно
отклоняться от силуэта». Also: the offline preview lied about its camera axes, which is why the earlier
placements were judged blind.

1. **Tooling fixed first**: `render_cam.py` — a real camera (eye / target / up, perspective, depth-sorted
   painter). Views now cost seconds and are unambiguous, so every game run is a *verification*, not a
   guess.
2. **Aperture**: `RAMP_Z 24,8-27,0` (2,2 m long) and `RAMP_TH 239-301°` (2,4 m wide) — right at the boom's
   root, on the surface that is already sloping up, so nothing of the belly or the sides is removed.
3. **Doors = the cut skin**, two halves, fore-aft hinges; `RAMP_DEG = 155` folds them back **inside** the
   silhouette (no more wings sticking out).
4. **Rails** on the sill, projecting slightly aft — visible from below/behind.
5. Verified in game: `mi26_rear_34.png` (clean silhouette on the dam), `mi26_side.png` (the opening with
   the folded leaves and the rails under the aft boom root).
6. Next: portholes in red frames on the leaves, locks, rollers.

### v0.9.98: the air-hockey table is **on the House of Soviets' highest floor** — retextured in the game's palette

The user: «перетекстурируй … в стилистике и цветовой палитре BABBDI … помести … внутрь Дома советов на его верхний этаж».

1. **Source**: `E:\858\ASSET\psx-low-poly-air-hockey-table` — `AirhockeyTable.obj` (96 verts, 99 faces
   → 207 rendered corners after UV/normal splits, **134 triangles**) and a **128×128 PSX palette
   atlas** (exactly 32 colours). Its `.mtl` is absent; the texture is used directly.
2. **Retexture**: every one of the 128 texture's 32 exact colours is remapped to a BABBDI palette —
   **oxide/brick red** body (e.g. (122,50,37)), a **bone rink** (196,192,178) with **faded oxide-red
   face-off circles** and **steel-teal lines**, charcoal metal, near-black wheels — then a 2× nearest
   upscale to **256×256**, a ±3 % fine grain, rink scuffs and a **warm amber scoreboard display**.
   All source details (vents, wheels, rink markings, arch) survive; no new geometry.
3. **Geometry**: exported offline as `BabbdiCoop/model/airhockey.glb` — **scale 0.23**
   (2,58 × 1,82 × 1,47 m), wheels at y = 0, long axis X. The quads are **triangulated**: the first
   build wrote the raw 332 corner indices into the GLB index buffer, so the runtime mesh had no
   drawable triangles and the table was invisible (the log said 207 verts but nothing rendered). The
   GLB is **baked double-sided**: **414 verts / 268 tris** = the front copy plus a back copy with
   negated normals and reversed winding, because the built-in `Standard` shader has no `_Cull`
   property. Loaded by `GlbFigureBuilder` with `useNormals=true` and a **BoxCollider**.
4. **Placement**: the **upper hall of the cantilevered chevron annex**, house-local
   `(-37,75, 102,55, 3,00)` = district-local `(192,6, 112,6, 89,6)` = world `(158,2, 198,0, 511,7)`.
   That is the **highest interior floor of the House complex** (both tower top floors are 6,4 m lower;
   the annex roof is at 108,95 house-local). Reach it through the **west upper tower's top-floor door**
   (house-local 89,75) into annex floor 0, then the annex's experimental stair up two flights; or
   fly in through one of the annex's **2,4 × 2,6 m open windows**.
5. **Lamp**: the annex had no light of its own, so a warm pendant was added over the table (rod from
   the 108,95 ceiling, 1,10 m metal shade, warm emissive lens, a 1,6 / 18 m point light), matching the
   House's hall pendants.
6. **Evidence**: `[PROP] airhockey_table: 414 verts, 1 materials, size 2,58 x 1,82 x 1,47 m, at local
   (-37,8, 102,6, 3,0)`; `[PROP] table node: parent=house_of_soviets, world=(158.23, 198.05, 511.73),
   active=True, renderer=on`; probes `air-hockey table top: 114,37 m (airhockey_table)` (= 1,82 m over
   the 112,55 m annex floor), `floor beside it: 112,55 m (house_details)`, `side shot from 8,3 m:
   7,01 m (airhockey_table)`; `[SCAN] map shots written`.
7. **Shots**: `district_93` — the table broadside under its pendant (rink markings, brick body, arch,
   wheels on the floor); `district_94` — the same from outside, through the annex's west window (the
   lamp and the table's arch read against the dark hall). (`district_92` is the preceding House pose,
   not the table.)
8. **Not playable**: no puck/mallet physics or scoring; it is a local decorative prop, like the
   figures — each peer builds its own copy from its own GLB. The table's central scoreboard arch is
   part of the source asset and is kept.

### v0.10.43: Zmejka — the "impossible" deaths were a 180-degree step into the neck

The user: «если достаточно быстро менять направление, то можно проиграть там, где это должно быть
физически не возможно… иногда игра позволяет мне изменить направление не на 90, а на все 180».
Both versions (solo cabinet and duel) had the same input bug.

1. **Root cause.** `TryDir` compared the new key against `sdir`, but `sdir` was **overwritten by
   the previous key press** while the head only moves once per tick: right → up → left inside one
   tick validated `left` against the *queued* `up` (`left != -up`), set `sdir = left`, and the next
   step moved the head straight into `snake[1]` — a real 180, a perfectly straight snake, instant
   death. Exactly the reported case. The duel version had the same shape (`want != -selfDir`) plus
   `want` being overwritten by the last key in the frame.
2. **Fix — a turn queue.** `sdir`/`selfDir`/`oppDir` now hold **only the direction the head
   actually moved in**; a new `sdirQueued` + `sdirHasQueued` holds at most **one** turn for the next
   step, applied in `AdvanceSnake()`/`ApplyQueuedDirs()`. `TryDir`/`TrySetDir` validate against the
   executed direction, reject the reverse, and treat "same direction" as "cancel the buffered turn".
   A burst can therefore never turn twice inside one cell; a U-turn over two cells still works.
3. **Second, real unfairness (duel only).** `StepSnakes` checked the **whole** body, including the
   last segment that moves away on the same tick, so tight play killed you for nothing. New
   `HitBody(body, cell, grows)` treats the tail as free unless the snake grows into it — the solo
   version already did this, and it now also covers the grow case (the old solo loop skipped the
   tail even when the head was about to eat, letting the head and tail share a cell).
4. **Duel input.** The client now sends the **queued** direction (`selfDirHasQueued ? queued : dir`)
   and the host applies it through the same `TrySetDir`, so a client cannot sneak a 180 past an
   authoritative host either.
5. **Verification — a deterministic test, not a play-through.** `[Debug] ArcadeTest = snake` and
   `[Debug] DuelTest = snake` now call `RunSnakeTurnTest()` on the live instance; it reproduces the
   exact burst, steps the real move code and restores the state. One run:
   `[SNAKE] turn test: up buffered=True, same-tick left rejected=True, head 6,7 alive=True; later
   left accepted=True, head 5,7 alive=True` and
   `[SNAKE] duel turn test: up buffered=True, same-tick left rejected=True, head 4,7 alive=True;
   later left accepted=True, head 3,7 alive=True`. Under the old rule the same-tick `left` was
   accepted and the next step killed the snake (`head` would have been `5,8` = `snake[1]`).
   Screenshots `snake_ready/test2.png` (solo, score 0030) and `snake_duel_ready.png` (two snakes)
   confirm the games still play normally.
6. **Version `0.10.43`.** Packet format unchanged; solo and duel both fixed.
7. **Housekeeping (same version, same day).** `git init` + first commit + tags `v0.10.43-baseline` and
   `v0.10.43-split`; `SovietDistrict.cs` (7 972 lines) split into **15 partial-class files** by
   contiguous member ranges — 16/16 code blocks verified byte-for-byte against the original, build
   passes, the district still builds in game (`[DISTRICT] built 38 buildings, 0 skipped plots,
   22 bridges, 74920 quads`, 0 exceptions). Largest file is now 986 lines (`Arcade`).

### v0.10.42: two-cabinet duels for Pong, Arkanoid and Zmejka, plus three duel cabinets at the opposite wall

The user: «Реализуй дуэльные версии всех трёх игр что ты предложил, с учетом наших правок, сделав
полный цикл стилизации и поставь по одному экземпляру у противоположной стены от ряда где стоят
остальные. Убедись что они будут стоять лицевой частью от стены».

1. **Duel rules.**
   - **Pong**: shared court, one player per cabinet; each controls one paddle (`W/S`), host
     simulates the ball and both paddles, first to 7. Client input is a float paddle Y at 30 Hz;
     host state (ball, both paddles, scores) is 22 bytes at 30 Hz.
   - **Arkanoid**: **two independent fields**, one per cabinet, each with its own ball, paddle,
     3 lives and score. Every **5 destroyed bricks** sends a **junk row** (a full row of 2-hit
     bricks) to the opponent; the wall shifts down and the bottom row falls off. First to clear
     their wall wins, or the opponent loses all lives. Only scores/lives/attack counters are
     synchronised — every player simulates their own field locally, so there is no shared-ball
     unfairness.
   - **Zmejka**: one shared 24x17 grid, two snakes, each with its own food; host simulates both
     snakes on the same tick. Walls/self/other body kill; a head-on collision kills both (round
     draw). Last alive scores a round, first to 3 rounds wins. Client sends only direction
     changes; host sends the whole board at 15 Hz.
2. **Protocol.** New UDP **type 4**: `[4][game][msg][len][payload]` with
   `CabinetMsg Join=1, Start=2, State=3, Input=4, Status=5, Leave=6`. It is routed like the
   air-hockey mini stream (host broadcasts, client sends to host) and kept away from the 31-byte
   pose stream. `ArcadeGame` owns the session: the host opens a lobby at a cabinet with **F**;
   the client presses **F** at a cabinet of the same kind, sends Join, and retries every 1.2 s;
   the host answers Start and both enter their own cabinet. Either side leaving/timeout (>3 s)
   ends the duel. `E` leaves, `F` cancels a lobby.
3. **Three duel cabinets at the opposite wall.** `SovietDistrict.ArcadeCabinets` now builds
   **14** cabinets: the 11 row machines at z = -12.5 (fronts +z) plus **one duel Pong, one duel
   Arkanoid and one duel Zmejka** at house-local **z = 18.5, yaw 180** (backs against the
   opposite wall, **fronts facing the room**). They use new SD art `arcade_12…14.png`, new
   marquees `arcade_poster_12…14.png` (blue PONG / yellow ARKANOID / violet ZMEJKA), and the
   body recolour picks `#B3571A`, `#9C4031`, `#0F9018`. Map shots `district_164.png` /
   `district_165.png` show the fronts from the room side.
4. **Debug.** `[Debug] DuelTest = pong|arkanoid|snake` teleports to the matching duel cabinet,
   enters as host without a peer and writes `<game>_duel_ready/test/test2.png`. Host-only
   timeout is disabled when no network session exists. `[DUEL] … started (host)` and
   `[DUEL] N screen: 320x240 duel canvas, host` are the evidence.
5. **Notes.** The new cabinets use the same attract loop as their game kind (Pong's loop already
   reads as two-player, Arkanoid's field is exactly the duel field, Snake's loop is the solo
   snake). Cabinet art, marquee, body and the duel game itself are all styled; the attract
   screen is the existing kind loop.

### v0.10.41: dark minimalist backdrops for Arkanoid and Pong; the playable background was upside down

The user: «фоновое изображение на понге и арканоиде. Сгенерируй что-то в темных тонах, минималистичные
размытые абстрактные фигуры. А ещё если со стороны картинка выглядит нормально, но при самой игре в
них задний фон почему-то переворачивался вверх ногами».

1. **New backdrops, procedural instead of the SD concrete.** `gen_dark_shapes.py` builds a dark
   vertical gradient, adds a handful of **giant soft-edged shapes** (circles, rounded slabs, a
   diagonal streak), blurs the layer by 22-24 px, downsamples to 320x240, adds a vignette and
   ±3 grain. Arkanoid: deep-teal mass left, plum haze top-right, rust slab bottom-right, ochre
   ember and a magenta streak (mean RGB ≈ 36/33/33). Pong: amber slab left, cyan slab right,
   violet orb top-centre, rust orb below, thin ochre/teal cross-lines (mean ≈ 26/26/22). The
   Snake backdrop (`slop_snake.png`) is untouched.
2. **The upside-down playfield was a pixel-array orientation bug.** `Texture2D.GetPixels32()`
   returns pixels **bottom-up**, but `LoadSlopBackground` copied `sy = y*height/H` (the bottom row)
   into the canvas top row. The attract GIF was pre-rendered upright, so the cabinet screen looked
   correct; only the live 320x240 canvas was vertically mirrored. The source row is now
   `height - 1 - y*height/H`, which fixes the backdrop for **all three** games (Snake included).
3. **Evidence**: `map/arkanoid_test2.png` (dark teal/rust blur, bricks upright) and
   `map/pong_test2.png` (dark amber/cyan/violet blur, paddles upright); Python previews
   `slop_arkanoid_dark_preview.png`, `slop_pong_dark_preview.png`; version `0.10.41`.

### v0.10.40: W/S and the arrows are no longer inverted in Pong and Zmejka; the HUD shows the controls

The user: «У понга и змейки кнопки управление вверх и вниз перепутаны. Поменяй их местами чтобы
на W было вверх, а на S вниз».

1. **Pong**: the paddle position is a top-down canvas coordinate, so the old `W → +1` moved the
   paddle down. `UpdatePong` now does `W/↑ → dir -= 1` (up) and `S/↓ → dir += 1` (down).
2. **Zmejka**: the grid's y axis grows downward too. The input now maps `W/↑ → TryDir(0, -1)` and
   `S/↓ → TryDir(0, +1)`; left/right were already correct.
3. **The bottom HUD now names the controls**: «E — ВЫЙТИ    A/D — БИТА» for Arkanoid,
   «W/S — БИТА» for Pong, «WASD — ЗМЕЙКА» for Zmejka, plus «ПРОБЕЛ — СТАРТ» before the game
   starts. Version `0.10.40`.

### v0.10.39: Arkanoid and Pong re-textured as brutal bright slop — Snake left alone

The user: «Змейку оставь как есть, но арканоид и понг нужно перетекстурить опять. Они выглядят как
радужный ЛСД трип, а должны как брутальные нейрослопные яркие текстуры и формы с необычными
цветовыми сочетаниями и неожиданными анимациями. Это я о геймплее, конечно».

1. **No more rainbow cycling.** The two SD backgrounds were regenerated as **brutalist concrete**
   (`slop_arkanoid.png`: a concrete wall with peeling paint; `slop_pong.png`: a bunker hall with
   rusted beams). The old rainbow court/floor images are gone; Snake's background is untouched.
2. **Two limited palettes cross-fade over 8 s** — rust/bone/deep-teal/acid-lime/hot-pink and
   violet/mustard/pale-cyan/brick/bone. Individual bricks, paddles and slats take one palette
   colour plus a contrasting accent from the same set; there is no continuous hue rotation.
3. **Arkanoid's bricks are chunky industrial slats**: 2 px dark outline, light top/left bevel, dark
   bottom, two thick accent glyph bars, a chipped corner, random paint drips hanging below the
   slat and a glint. They wobble in brightness; every 4 s one whole row slides 3 px sideways
   (visual only), one slat randomly flips to the accent colour, and the whole wall's palette
   drifts. The paddle is a heavy bone/yellow slab with rolling hazard stripes, bolts and a 1 px
   rock. The ball is chrome with a single accent halo and a blocky afterimage trail.
4. **Pong's court is industrial**: black score plates with bone/acid digits, a marching two-colour
   centre line, and 16 px **hazard-striped slabs** with bolts for paddles. Every 4 s the two slabs
   swap palettes, their width pulses by 1 px, the digits glitch-double, and the whole screen gets a
   brief tint flash. The ball is chrome with one accent halo and blocky afterimages.
5. **Screen effects kept**: scanlines, 2-3 horizontal glitch bands per third of a second and white
   speckles; the attract loops were regenerated with the same renderer, so the cabinet screens
   match the playable games.
6. **Snake is deliberately unchanged** (same `slop_snake.png`, same rainbow body, same grid).
7. **Evidence**: `map/arkanoid_test2.png` (concrete wall, bone/purple/teal slats, hazard paddle),
   `map/pong_test2.png` (bunker, hazard slabs, score plates), Python previews
   `slop_arkanoid_60.png`, `slop_pong_60.png`; version `0.10.39`.

### v0.10.38: the three games' textures are now colourful neural slop (cabinets untouched)

The user: «Сами змейка, арканоид и понг выглядят слишком нормально... Пересобери все их текстуры
как цветастый нейрослоп».

1. **Three SD slop backdrops** replace the flat dark boards: `model/arcade_game/slop_arkanoid.png`
   (psychedelic tiled neon floor), `slop_pong.png` (magenta/green warped court), `slop_snake.png`
   (giant green/purple snake mural). All 320x240, SD 1.5, saturation x1.35 and brightness x0.62
   so the gameplay still reads.
2. **Every object is neon now**: Arkanoid bricks cycle through the full hue wheel per row/column
   with white speckles and bright edges; the paddle is a rainbow gradient on a dark plinth with a
   white top line (it used to disappear into the busy backdrop); the ball is a cyan glow orb with
   a rainbow trail. Pong has rainbow paddles, a glowing ball with a 9-point trail, a dark score
   strip and huge magenta/lime digits. Zmejka's body segments cycle hue along their length, the
   head has magenta/cyan eyes, the food pulses as a glowing orb; the board is darkened to 72 %
   with a faint purple 12-px grid and a magenta border so the cells remain readable.
3. **Screen effects** on both the attract loops and the live 320x240 canvas: scanlines every second
   row, 2-3 horizontal glitch slices per 1/3 second (±5 px), RGB chromatic offset, and white
   noise speckles. The HUD stays legible through coloured shadows.
4. **Attract loops regenerated** with the same renderer (`gen_games_slop.py`): `arkanoid_attract.gif`
   / `pong_attract.gif` / `snake_attract.gif` and the three 1600x2400 atlases are now slop too.
   The live `ArcadeMiniGame` loads the same `slop_*.png` backgrounds (`LoadSlopBackground`), so the
   playable screen and the idle loop belong to one visual family.
5. **The cabinets are untouched** — no changes to the bodies, side art, marquees, poster titles or
   cabinet meshes.
6. **Evidence**: `map/arkanoid_test2.png`, `pong_test2.png`, `snake_test2.png` (live screens),
   `map/district_161.png`/`162.png` (the attract loops on the row), and the Python stills
   `slop_arkanoid_60.png`, `slop_pong_60.png`, `slop_snake_120.png`. Logs:
   `Arkanoid/Pong/Zmejka screen: 320x240 pixel canvas`; the atlases still load
   `1600x2400 … 200 frames @ 10 fps`.

### v0.10.37: the six new marquee titles are melted beyond legibility, each in its own colour

The user: «надписи на маркизов на Змейках, арканоидах и понг слишком легко читаются. Они должны быть
СИЛЬНО искажены и едва напоминать буквы. Также, сделай их разных цветов, не красного».

1. **Rebuilt the title overlay generator** (`gen_titles.py` on top of the SD marquee art): each word
   is drawn glyph by glyph with random per-letter scale (0.78-1.34 x, 0.72-1.38 y), rotation
   (±16°), vertical jitter and negative tracking, then passed through a strong elastic warp
   (26-34 px sinusoidal + smooth-noise displacement, plus four local "melt drips" pulling columns
   downward), five horizontal glitch slices (6-26 px bands shifted ±34 px) and four chunk
   break-outs. After warping the word is re-fitted to 90 % x 78 % of the poster and re-centred,
   so nothing is clipped. The result is a matte, screen-printed blob that only hints at letters.
2. **Six different non-red colours** (with dark outline/shadow): Arkanoid A **acid yellow**
   `#E4BC1A`, Arkanoid B **cyan** `#26CABE`, Pong A **magenta** `#D43AC0`, Pong B **blue**
   `#4D8FE5`, Zmejka A **orange** `#E58C2C`, Zmejka B **violet** `#B47BE0`.
3. The six files are `model/arcade_game/arcade_poster_06…11.png` (1536x512), loaded per cabinet by
   `ArcadeGame.PosterMaterial`; the in-game marquees are shown in `district_161.png`/`162.png`
   (ZMEJKA violet/orange, PONG blue/magenta) and `district_163.png`. The Dino poster 4 is
   unchanged.

### v0.10.36: six new cabinets — two Arkanoid, two Pong, two Zmejka — with their own slop art, marquees and playable games

The user: «Продли ряд автоматов 6 новыми автоматами - 2 с Арканоид, 2 с понгом, 2 со змейкой.
Для всех также нейросломный дизайн, кастомная картинка на маркиз, перекрас корпуса под цвет
дизайна, гифку на заставку... короче полный цикл для этих трёх игр».

1. **The row is 11 cabinets.** `SovietDistrict.ArcadeCabinets` now builds them 2 m apart at
   house-local x `-46,5 … -26,5`, z `-12,5` (backs 1,03 m off the wall): the six new machines on
   the left (`arcade_06…11`, kinds 1/1/2/2/3/3) and the five Dinozavrik machines on the right
   (`arcade_01…05`, kind 0). `ArcadeCabinetInfo` gained `Kind` and `PosterFile`; the E prompt now
   reads «E — Арканоид / Понг / Змейка / Динозаврик».
2. **Six new AI-slop side skins and six marquees.** `model/arcade_art/arcade_06…11.png` (512x896,
   SD 1.5): neon chrome/brick Arkanoid, Soviet constructivist brick-breaker, neon Pong court,
   green phosphor terminal Pong, acid-green pixel snake, copper-circuit snake. Each cabinet's
   body is repainted from the **dominant hue of its own art** by the existing
   `RecolorCabinetBody`: `#992814`, `#A54632`, `#9D0A87`, `#2A9637`, `#95093D`, `#41926F`.
   `model/arcade_game/arcade_poster_06…11.png` are 1536x512 marquees: SD art plus a heavily
   distorted matte title (the titles were re-coloured per game and melted further in v0.10.37).
   The five Dino cabinets keep poster 4.
3. **Three playable games on one pixel canvas.** New `ArcadeMiniGames.cs` (`ArcadeMiniGame`) draws
   a 320x240 Canvas texture (Point filter) and `ArcadeGame.BuildMiniGame` maps it onto the cabinet
   screen with the same camera as Dinozavrik. Controls:
   - **Arkanoid**: A/D or arrows move the paddle, Space launches; 10x5 bricks, 3 lives, score.
   - **Pong**: W/S or arrows move the left paddle, AI on the right, first to 7, Space serves.
   - **Zmejka**: arrows/WASD turn, food grows the snake, speed ramps up, walls/self kill; Space
     starts/restarts. All games start behind a «PRESS SPACE» overlay and show «GAME OVER».
   The canvas uses a built-in 3x5 pixel font for score/status.
4. **Attract loops per game.** `gen_games.py` simulated each game for 20 s at 10 fps and wrote
   `arkanoid_attract.gif`, `pong_attract.gif`, `snake_attract.gif` (320x240) plus three
   `*_attract_atlas.png` (1600x2400, 10x20 cells of 160x120). `ArcadeGame` loads all four atlases
   (Dino + three new) and each cabinet's screen uses its kind's material; the per-cabinet random
   `AttractPhase` still gives every machine a different start point.
5. **Per-cabinet marquee posters.** `PosterMaterial(file)` loads and caches one material per
   poster PNG; the shared poster mesh gets the cabinet's own material, so all eleven marquees are
   unique (six designs, Dino's poster 4 ×5).
6. **Debug test.** `[Debug] ArcadeTest = arkanoid|pong|snake` teleports to the matching cabinet,
   enters, auto-plays and writes `<game>_attract/ready/test/test2/test3.png`; the config clears
   itself. Evidence: `[ARCADE] attract atlas Arkanoid/Pong/Zmejka: … 200 frames @ 10 fps`,
   `[ARCADE] marquee poster: arcade_poster_06.png 1536x512` … `arcade_poster_11.png`,
   `[ARCADE] attract screen 1 (Arkanoid) …`, `[ARCADE] Arkanoid screen: 320x240 pixel canvas`,
   the three test logs and shots `arkanoid_test.png` (bricks + score 0010), `pong_test2.png`
   (0:1 rally), `snake_test2.png` (score 0010, snake + food).
7. **Screens/art**: `map/district_161.png` (the six new machines along the wall),
   `district_162.png` (ZMEJKA + PONG close), `district_163.png` (the full row: Dino → ZMEJKA →
   PONG → ARKANOID). All eleven bodies are theme-coloured; the new screens play their own loops.

### v0.10.35: the GIF fits inside the monitor again; the marquee is almost monochrome; air hockey wind muted with the SFX kept

The user: «теперь гифка выезжает за края монитора слева и снизу. Также измени постер на маркизе —
цвет надписи не меняй, но всё остальное сделай почти ЧБ. Также примени фикс звука ветра и к
Аэрохоккею, только сделай так чтобы звуки биты и гола не заглушились».

1. **The screen is the recess again, not a quad.** v0.10.34 selected triangles by position and put
   a black backing + animation over the *whole* bezel rectangle, so the frame spilled over the
   left and bottom frame. The display is actually the **recessed island** in the body atlas:
   u 0,078-0,258 / v 0,588-0,718 glTF (= u 0,078-0,258 / v 0,282-0,412 after the loader's V flip),
   geometry `x -0,297..0,297, y 1,009..1,487, z 0,764..0,846`. `BuildScreenMeshes` selects that
   island by UV (u 0,05-0,30, v 0,24-0,46) plus a position filter `x > -0,32, y > 0,99,
   z 0,74-0,87, n.z > 0,2` (the thin strip on the cabinet's left side reuses the same texels), and
   builds two **conformal** meshes again — black backing +1,5 mm and animation +2,5 mm along the
   vertex normals. The old v0.10.32 UV box stopped at u = 0,22, dropping the right-hand
   `INSERT COIN`/green-bars part: that was the right gap. Log:
   `[ARCADE] screen mesh: 192 verts / 64 tris, display island x -0,30..0,30, y 1,01..1,49, whole
   picture incl. right strip (recess-conformal, no spill)`.
2. **The marquee poster is almost monochrome.** `arcade_posters/hposter_4_marquee.png` and
   `model/arcade_game/arcade_poster.png` now keep only the matte-red title; every other texel is
   converted to luminance grey (red mask `r > 55, r > g·1,22, r > b·1,12`; 129 452 red texels kept,
   656 980 greyed). The title colour is unchanged.
3. **Air hockey wind is muted like the cabinet, with the table SFX exempt.**
   `AirHockeyGame.MuteListenerForMiniGame()` saves `AudioListener.volume`, sets it to **0** and sets
   the table's own `sfx` source `ignoreListenerVolume = true`; the volume is re-zeroed every
   `LateUpdate`, so any source created later (zone wind, falling loop, gadget loops) is silent too,
   while mallet clicks and the goal banka bypass the listener volume. The held
   `Propeller`/`Blower`/`Climber`/`Grabber` scripts are disabled for the session. On exit
   `RestoreListenerAfterMiniGame()` puts the exact listener volume and the source's
   `ignoreListenerVolume` back. Logs:
   `[COOP] air-hockey audio muted (AudioListener 1,00 -> 0,00, table sfx exempt: airhockey_sfx)`
   then `[COOP] air-hockey audio restored (AudioListener 1,00)`; the MiniTest run still scores
   `[MINI] goal in the north net -> 1:0` with `arcade sounds ready: click=ok, goal=ok`.
4. **Evidence**: `district_160.png` (front) shows the game frame inside the monitor's recess, no
   spill over the left/bottom bezel and no static right column; `district_159.png` (row) the same
   from the side. The map shot log has the new screen line; the MiniTest log the audio
   mute/restore pair.

### v0.10.34: the animation covers the whole screen opening; the cabinet is fully silent

The user: «Гифка покрывает не весь экран автомата, справа большой зазор. Также ещё раз постарайся
убрать звук ветра при игре в автоматы, он очень отвлекает.»

1. **The right gap was a selection bug.** The v0.10.32/33 screen mesh was selected by its small
   display UV island (x -0,19…0,29), so the right-hand `INSERT COIN`/green-bars part of the opening
   stayed static. `BuildScreenMeshes` now selects the **whole opening by position** — centroid
   x -0,22…0,37, y 0,94…1,40, z 0,74…0,87, front normal only — giving **258 verts / 86 tris**
   (vertices span x -0,28…0,38, y 0,88…1,50). The animation is projected across the whole rectangle,
   so it fills the screen edge to edge; the static right strip is covered by the black backing and
   the animation. The game screen's 4:3 quad is unchanged.
2. **The cabinet is now completely silent.** The cabinet game has no sounds of its own, so on `E`
   the whole **`AudioListener.volume`** is saved and set to **0**, and re-zeroed every LateUpdate;
   on exit the exact previous volume is restored. This sits on top of the per-source enforcement
   (ambient indoor/outdoor wind, fall loop, held propeller/gadget loops). Logs:
   `[COOP] cabinet audio muted (AudioListener 1,00 -> 0,00)` and
   `[COOP] cabinet audio restored (AudioListener 1,00)`. No level wind can leak through, even if an
   unknown script keeps writing to its own AudioSource.
3. **Evidence**: `[ARCADE] screen mesh: 258 verts / 86 tris, whole opening x -0,28..0,38,
   y 0,88..1,50, conformal … (no floating quad, no right gap)`; `district_160.png` shows the
   gameplay frame filling the opening (no static right column), the poster 4 marquee above and
   five desynced screens; `district_159.png` the row from the side.
4. **Air hockey** keeps its click/goal SFX; the listener mute is only active while the Dino
   cabinet owns the camera and is restored on exit.

### v0.10.33: poster 4 on the marquee; every cabinet plays its own desynced gameplay loop

The user: «Четвертый постер неплох, минималистичный который. Также сделай чтобы гифки на Автоматах
были не синхронизованны - пусть для каждого автомата гифка начинает свой первый цикл из
произвольного места, а дальше идёт уже по полному кругу.»

1. **Poster 4 is installed on the marquee.** The chosen minimal-city 1536x512 poster is copied to
   `BabbdiCoop/model/arcade_game/arcade_poster.png` (`Dino assets: 36 files`) and mapped onto the
   **real top marquee panel**, again as a conformal mesh: body triangles with centroid y > 1,45 and
   z > 0,84 and a front normal (|n.z| > 0,2) give **210 verts / 70 tris**; positions are offset 2 mm
   along the normals and the UVs are `u = 1 - sx`, `v = sy` (the panel needs the same U flip as the
   screen; the first attempt was mirrored). The material is the opaque Standard+emission one with
   `_Cull 0`. It covers the `JUMP TANK` art and stays visible while playing.
2. **The title is the correct orientation.** The SD-inpainted title layer came out mirrored; the
   final poster is the clean Latin art plus that title layer **flipped back** and composited over
   it (red mask + dilated shadow), so the marquee reads D→K left-to-right, matte red, heavily
   distorted. The cabinet poster consequently shows the D on the left and the K on the right.
3. **The loops are no longer in sync.** Each cabinet gets its own **clone of the screen animation
   mesh** and a random phase: `AttractPhase = Random.value * 20 s`, and every 0,1 s
   `frame = (int)((Time.time + phase) * 10) % 200`. The first cycle starts at that random place,
   then the full 20 s loops continue normally. A per-screen `AttractMeshOwner` destroys the clone
   with the cabinet. Logged per cabinet, e.g.
   `attract screen 1: conformal, desynced start frame 127`, `… 2: 122`, `… 3: 75`, `… 4: 197`,
   `… 5: 122` — visibly different frames in one still.
4. **Evidence**: `[ARCADE] marquee poster: arcade_poster.png 1536x512 (poster 4, minimal city)`;
   `[ARCADE] marquee poster mesh: 210 verts / 70 tris on the top panel`; `[ARCADE] poster 4
   installed on cabinet N's marquee` ×5; five desynced start frames. Map shots `district_159.png`
   (row: five different gameplay frames, posters above) and `district_160.png` (front: poster 4
   with the red D…K title on the top panel, desynced screens below).

### v0.10.32: the gameplay screen is conformal to the real recessed screen mesh; horizontal matte-red posters

The user: «Гифка визит в воздухе перед экраном, нужно её поставить четко на экран(проверяй с разных
ракусов). А постеры не те, уточняю: они должны быть горизонтально ориентированы, иметь цветовую
гамму нашей игры Динозаврик и двусложную надпись сильно не читаемую, но чтобы было понятно что это
буквы. Используй самые смелые шрифты. Также саму надпись сделай матово-красной.»

1. **Why it floated.** The v0.10.31 animation was a 0,76 × 0,57 quad at z = 0,99, but the cabinet's
   actual screen is a **recessed, slanted mesh** at z ≈ 0,76-0,82, x ≈ -0,19…0,29, y ≈ 0,95…1,37.
   The quad was both larger and ~0,18 m in front of the surface, so from the row it visibly hung in
   the air.
2. **The conformal mesh.** `BuildScreenMeshes` now extracts the screen triangles from the loaded
   body submesh: the screen UV island (u **0,078-0,196**, v **0,282-0,412** after the loader's
   glTF-V flip) plus a front-position filter (y > 0,85, z > 0,70, x > -0,25) selects **144 verts /
   48 tris**. It builds two meshes at the same positions, offset 1,5 mm (black backing) and 2,5 mm
   (animation) along the vertex normals, parented with identity local transforms so the cabinet's
   1,6 scale applies exactly as it does to the body. The materials are Standard + emission with
   `_Cull 0` (the mesh's winding is not relied on).
3. **Mapping.** The animation is mapped by **planar position projection**, not by the body UVs:
   `sx=(x-minX)/(maxX-minX)`, `sy=(y-minY)/(maxY-minY)`, `u=1-sx` (the front camera's right is
   local -x), `v=sy`. The full 4:3 frame then covers the actual display rectangle, with the frame
   cell swapped into the atlas each 0,1 s. The black backing covers the old `INSERT COIN` display
   area; the surrounding painted bezel stays. Hidden on `E`, shown on exit.
4. **Evidence**: `[ARCADE] screen mesh: 144 verts / 48 tris, UV 0,078-0,196 / 0,282-0,412,
   conformal to the slanted screen (no floating quad)`; `district_159.png` (row/3-4) and
   `district_160.png` (front close-up) both show the animation lying on the screen surface, and the
   close-up crop reads the full frame (score top-left, foreman left, white asphalt below). The
   static bezel around the display is the cabinet's own painted bezel, not a floating border.
5. **Posters redone horizontally.** The four 1:3 vertical images were rejected. New **1536 × 512
   (3:1)** marquees start from SD 1.5 art graded into the game palette (grey / beige / khaki / sand /
   brick / muted green), then a **Latin transliteration** «DINO / ZAVRIK» in heavy display type is
   laid over the art and passed through **SD 1.5 img2img inpainting** with a tight title mask at
   **denoise 0,68-0,75**. The background art is untouched (only the title region is inpainted), and
   the result is a strong neural-slop distortion: warped, melted letter silhouettes that still read
   as letters, in flat matte red, with glitch cuts and a hard offset shadow. Files:
   `arcade_posters/hposter_N_marquee.png` (N = 1-4, final slop), contact sheet
   `hposters_sheet.png`; intermediates `hposter_N_latin_clean.png`, `_titlemask.png`,
   `_latin_inpaint*.png`, source art `hposter_N_art.png`, prompts in `hposter_N.json`. The marquee
   itself is still `JUMP TANK` until the user picks one; the chosen 3:1 poster will be an overlay on
   the horizontal top panel.

### v0.10.31: the idle cabinet screen plays a 20 s gameplay loop; four 1:3 marquee posters generated

The user: «Изначальная модель аркадного кабинета для Динозаврик это омаж на автомат игры Пакман.
Нам нужно заменить 2 элемента указывающие на это: прямоугольную вертикальную часть над экраном на
автомате нужно заменить на постер нашей игры(сгенерируй в старой Дифужн 4 варианта изображения с
соотношением 1:3, я выберу один из них для замены). Также на экране автомата должна в фоновом режиме
проигрыватся гифка геймплея нашей игры(сделай зацикленную гифку геймплея текущей версии геймплея
около 20 секунд и замени ей статичное изображение которое сейчас на экране аркадных кабин)».

1. **The GIF** is `BabbdiCoop/model/arcade_game/arcade_attract.gif` — **320x240, 10 fps, 200
   frames = 20 s**, 2,2 MB, looping. It was rendered offline from the real current assets and rules:
   the exact bg/ground loops, the v0.10.25 flipped foreman frames, the v0.10.30 +30 % obstacle
   geometry and weathered textures, the 0.487 s jump and the top-left chrome score. Nine obstacles
   are cleared (score 0009), the tenth barrier ends the run at 17,3 s and the
   «СТОЛКНОВЕНИЕ — ПРОБЕЛ ЗАНОВО» freeze holds for the remaining ~2,7 s; then the loop starts a
   fresh 0000 run — a natural attract-mode cut.
2. **In-game it is a flipbook, not a video codec.** The same 200 frames are packed at 160x120 into
   `arcade_attract_atlas.png` (**1600x2400, 10 columns x 20 rows**, 1,06 MB). `ArcadeGame` loads it
   once at startup (`EnsureAttractAssets`), creates a shared unit quad and an opaque Standard+
   emission material (Point filter, no mipmaps — no atlas bleeding), and `UpdateAttractScreens`
   builds one screen per cabinet (0,76 x 0,57 m, local z +0,01/s so it sits on the cabinet front,
   parented to the 1,6-scaled cabinet with a compensating local scale). Every 0,1 s the shared
   mesh's UVs move to the next 160x120 cell (`u = col/10`, `v = 1-row/20`); the U is **reversed**
   because the quad's +z face has the camera's right at local -x — without that the score/player
   appeared mirrored.
3. **The static painted screen is covered.** A **0,84 x 0,78 m opaque black backing** sits 5 mm
   in front of the body and behind the animation; the animation's 4:3 frame is centred inside it,
   so the leftover `INSERT COIN`/green-bar strip no longer reads as a static screen (the small strip
   at the very top belongs to the marquee area that the chosen poster will cover). The animation
   quad is hidden on `E` (the game's own screen takes over — the black backing stays, giving the
   game screen a bezel) and shown again on exit.
4. **Evidence**: `[ARCADE] attract atlas: arcade_attract_atlas.png 1600x2400, 10x20 cells, 200 frames
   @ 10 fps = 20 s loop`, five `[ARCADE] attract screen N: 0,76x0,57 m on the cabinet front` lines;
   `district_159.png`/`district_160.png` show the animated gameplay frame (score top-left, player
   left, pastel/weathered obstacles) on every cabinet; `dino_ready.png` shows the real game screen
   with the black bezel after E, and the DinoTest still passes.
5. **Four 1:3 posters are generated but not yet installed** (the user will choose):
   `arcade_posters/poster_1..4.png` (raw 512x1536) and the finished marquee versions
   `poster_1_marquee..poster_4_marquee.png` with a chrome «ДИНОЗАВРИК» title; prompts/seeds in
   `poster_N.json`, contact sheet `posters_sheet.png`. The vertical marquee (`JUMP TANK`) is still
   the original until the choice; the poster will be an overlay quad on that marquee, not a
   body-texture edit.

### v0.10.30: longer jump, bigger obstacles, the 10-obstacle speed ramp, score top-left, weathered retexture

The user: «Новые правки по Динозаврик: увеличь длительность прыжка от и до на 30%. Увеличь все
препятствия на 30%. Пусть после каждых 10 пройденных препятствий, скорость скорость игры
увеличивалась на 15% от изначальной скорости. Также передвинь счётчик очков с правого-верхнего, в
левый-верхний. Ещё нужен новый ретекстур препятствий, тех которые были изминены на более пастэльные
- нужно откатить их и ретестурнуть более логично, добавив бежевый, хакки, зеленй, кирпичный и
песочный там где уместно.»

1. **Jump 30 % longer, same height.** `JumpSpeed` 1,50 → **1,50 / 1,3 = 1,154 m/s** and `Gravity`
   8 → **8 / 1,3² = 4,734**; airtime `2v/g` = **0,487 s** (was 0,375 s) and the apex stays
   **0,141 m**. Logged at build: `[DINO] jump: v=1,154 m/s, g=4,734, airtime=0,487 s,
   height=0,141 m (v0.10.30: +30 % duration at the same height)`.
2. **All obstacles 30 % larger.** Spawn base is now `w = cell·0,4·1,3 = 0,079 m`,
   `h = cell·0,6·1,3 = 0,119 m`; the crate keeps its extra +30 % height and the barrier its extra
   +30 % width, so the crate is 1,69× its v0.10.23 height and the barrier 1,69× its width. The
   alpha bboxes/collision boxes scale with the quads. Logged e.g. `obstacle hitbox 'barrel':
   visible 0,031 x 0,078 m (alpha bbox of the 0,079 x 0,119 m sprite)`.
3. **Speed ramp after every 10 passed obstacles.** `speedMul = 1 + 0,15·(score/10)` multiplies the
   ground scroll, the background (still 0,6×) and the obstacle motion; at 10 → **×1,15**, 20 →
   ×1,30, 30 → ×1,45 … (15 % of the **base** speed per tier, as requested). The pass logs
   `[DINO] speed x1,15 after 10 obstacles (base 240 px/s)`; `ResetRun` puts it back to ×1.
4. **Score moved to the top-left.** `left = −ScreenW/2 + 8 px` instead of the top-right formula; the
   log now says `[DINO] score display: 4 digits at the screen's top-left …`. The four chrome cells
   and their 0000 start are unchanged.
5. **The pastel retexture is replaced with weathered materials.** The six grey/black obstacles are
   re-rendered from the original textures at load (the pastel pass is gone) with beige, khaki, moss
   green, brick and sand:
   - `rubble` — beige / sand / brick / khaki patches (concrete + broken brick),
   - `barrier` — khaki / green / sand vertical panels (the hazard stripe is preserved),
   - `cone` — sand / beige / brick patches (white band preserved),
   - `pipe` — beige / sand on top, moss green / khaki at the foot,
   - `block` — beige / sand / khaki concrete,
   - `rebar` — brick / khaki / green (rusty bars with moss).
   Grey/black detection is unchanged (s < 0,18 or v < 0,22) and coverage is still **84-99,9 %**;
   the pixel luminance, dark silhouette and every coloured detail survive. Logs:
   `[DINO] obstacle texture 'barrier': grey/black 89,0 % -> retexture 89,0 % of visible pixels
   (khaki/green/sand)`. `crate` and `barrel` (22,8 %) stay original.
6. **Evidence**: `dino_ready.png` shows **0000 top-left**, `dino_test.png` the +30 % barrel at the
   right, `dino_hit.png` the +30 % khaki/green/sand barrier at the foreman with **0001**; the logs
   include the jump, speed rule and six retexture lines.

### v0.10.29: wind enforced off for the whole game, and pastel debris

The user: «Звук ветра остался при игре. Насколько я понимаю, этот звук когда игрок находится в
воздухе - надо как-то его убрать именно в момент игры. Также нужен ретекстур препятствий - в тех где
серого или черного больше половины, то ретекстурируй их добавив от 50% пастэльных цветов.»

1. **The wind kept coming back because the mute was one-shot.** v0.10.27 zeroed the ambient loops
   and the fall loop at entry, but the gadget/ambience scripts keep writing their own volume/mute
   from Update. v0.10.29 adds `CoopPlayerAudio.Enforce()`, called from **LateUpdate** of both
   mini-games while active: it re-zeroes the fall source, both ambient loops and **every AudioSource
   under the game camera**, and re-disables the gadget scripts (`Propeller`, `Blower`, `Climber`,
   `Grabber`) so `Space`/`Fire1` cannot start the propeller lift/thrust wind. The held sources are
   saved on entry and restored on exit: `SoundSource` (Propeller2, volume 1,00 and Propeller3,
   0,43), plus `[COOP] held gadget disabled: Propeller on 'Propeller(Clone)'`. The air hockey's own
   `sfx` source is not under the camera, so clicks/goals still play (`arcade sounds ready:
   click=ok, goal=ok`).
2. **Which debris is grey/black** (measured after the v0.10.24 brightness pass, visible pixels
   only): `rubble 86,7 %`, `barrier 89,0 %`, `cone 96,8 %`, `pipe 84,2 %`, `block 99,9 %`,
   `rebar 96,5 %`; `crate 22,8 %` and `barrel 22,8 %` are coloured and stay original. The six over
   50 % are retextured.
3. **The pastel pass** (`PastelizeObstacle`) remaps every grey/black texel (saturation < 0,18 or
   value < 0,22) to one of three muted pastels per obstacle, selected by a low-frequency Perlin
   patch: **#A9CBE8 pale blue, #B5E3CC mint, #F5C7AD peach, #CFBFEB lavender, #F2E6A6 pale lemon,
   #F0BFC7 dusty rose**. The pixel's own luminance is kept (`shade 0,66 + 0,62·L` interior,
   `0,40 + 0,35·L` on the alpha edge, so the silhouette stays dark), so cracks, holes, the hazard
   stripes, the cone's white band and the rust detail survive. Coverage is **84-99,9 % pastel** —
   above the requested 50 %. Logged per texture: `grey/black 96,8 % -> pastel retexture 96,8 % of
   visible pixels (#F5C7AD / #CFBFEB / #F2E6A6)`.
   **v0.10.30 supersedes the pastels** with beige/khaki/green/brick/sand weathered materials (the
   source textures were never written to disk, so reverting is simply the new load-time pass).
4. **Evidence**: the eight `[DINO] obstacle texture …` lines (six `pastel retexture`, two `original
   kept`), the held-audio/gadget lines, and the DinoTest stills — `dino_test.png` shows the pastel
   rebar at the right, `dino_score.png` the **0001** after it passed, `dino_hit.png` the pastel
   lavender/peach cone at the foreman. No new network data; the textures are local per peer.

### v0.10.28: the Dinozavrik score counter in the top-right corner

The user: «Нужно доделать геймплей Динозаврик. Пускай на экране игры в правом-верхнем углу будет
счётчик, который будет считать сколько препятствий человечек преодолел. Для счётчика используй шрифт
"digits_v3_chrome" из "C:\Users\LethargyLogics\Documents\Проект по умолчанию\arcade_digits". Пусть
изначально счетчик будет показывать "0000"».

1. **The font is packed into one runtime atlas.** The ten 512x512 `digits_v3_chrome/*.png` have a
   black (not transparent) background; a build script cropped each digit to its bright bbox
   (threshold max RGB >= 22), scaled it to a **140 px** cap height and centred it in a **128x160**
   cell, then keyed the dark background to alpha (0 for max < 20, a 20-45 ramp to opaque brights).
   The result is `BabbdiCoop/model/arcade_game/score_digits_chrome.png` — **1280x160, 10 cells**
   (0-9), 208 KB — copied by `build.ps1` (`Dino assets: 33 files`). The atlas is loaded with
   **Trilinear + mipmaps** (not the pixel-art Point filter) so the chrome reads cleanly at 20 px.
2. **A single 4-quad mesh in the game's top-right corner.** `BuildScoreDisplay` lays four cells
   **16 x 20 screen px** (0,038 x 0,0475 m on the 0,76 x 0,57 m screen) with an **8 px** margin:
   `left = ScreenW/2 − 8/320·ScreenW − 4·cellW`, `top = ScreenH/2 − 8/240·ScreenH`. It is one mesh, one
   `Unlit/Transparent` material, queue 3100, at local z = −0,004 (in front of the player), parented
   to the game root; the atlas cell for digit `d` is `u = d/10 … (d+1)/10`.
3. **The counter is zero-padded and starts at 0000.** `score` resets to 0 in `ResetRun` (entering a
   cabinet and every Space restart) and `UpdateScoreDisplay` writes thousands/hundreds/tens/units
   into the mesh UVs. Passing is counted only when the obstacle's **visible right edge** is behind
   the foreman's **visible left edge** (`o.x + centre + halfW < playerX + centre − halfW`), so a
   jumped-over obstacle counts, while the obstacle that ends the run never does. Score caps at 9999.
4. **Evidence**: `[DINO] score atlas: score_digits_chrome.png 1280x160, 10 cells
   (digits_v3_chrome, initial 0000)`; `[DINO] score display: 4 digits at the screen's top-right
   (cells 16x20 px, margin 8 px, initial 0000)`; `[DINO] obstacle passed: obstacle_cone,
   score=0001`; then the next obstacle ends the run. The DinoTest gained a `dino_score.png` shot
   (a debug-only `testGhost` lets the first obstacle pass, then is removed so the next one hits):
   `dino_ready.png` shows **0000**, `dino_score.png` shows **0001** in the same chrome.
5. **Not changed**: the cabinet art, the wind mute, the pixel hitboxes and the screen quad.

### v0.10.27: the actual wind is the level's global loops, and pixel hitboxes

The user: «Звук ветра как при падении все еще очень есть при игре в автоматы или аэрохоккей. Также
улучши хитбоксы моделек. Они должны совпадать с их размерами по пикселям. Тоже проверь для
препятствий».

1. **The fall loop was not the wind that was heard.** A diagnostic dump of every playing `AudioSource`
   on entering a cabinet found `IndoorWindSound` at **volume 0.75** and `OutdoorWindSound` at
   **0.25**, both `loop=True`, `spatialBlend=0.00` (global 2D loops, so their world distance is
   irrelevant). The player's own `FallSound` was at 0.00 after the v0.10.24 mute. New
   `CoopPlayerAudio.MuteAmbientWind` scans all scene `AudioSource`s whose object or clip name
   contains **"wind"**, saves volume/mute, sets volume 0 and mute true; `RestoreAmbientWind` puts the
   exact values back on exit. Wired into `ArcadeGame.Enter`/`Exit` and
   `AirHockeyGame.StartGame`/`StopGame`/`ForceStop`. Logs:
   `[COOP] ambient wind muted: 'IndoorWindSound' clip='windIndoor' was volume=0,75` and the matching
   `restored` lines with 0,75/0,25 (the values vary with the zone; they are saved and restored
   exactly).
2. **Pixel hitboxes, player.** `AlphaBounds` now returns the visible alpha bbox as fractions:
   `halfW`, the bbox **centre offset** from the quad centre, `top` and the **bottom padding**.
   Every player frame (run 1/2, jump, fall, crouch, idle) is measured after the v0.10.25 mirror;
   `SetPlayerTex` switches the live box with the frame. Measured: **run halfW 0,031 m, visible
   height 0,109 m; crouch halfW 0,033 m, height 0,078 m** — the old 0,040 m half-width was ~30 %
   too wide. The collision uses the visible centre and feet (`jumpY + bottom padding`) against the
   obstacle's visible top, not the 64-px cell.
3. **Pixel hitboxes, obstacles.** The eight debris sprites keep their own alpha bboxes, now with the
   centre offset too. Logged once at load, all fractions of the 64-px cell:
   `crate 0.383/0.656`, `barrel 0.195/0.656`, `rubble 0.453/0.375`, `barrier 0.453/0.578`,
   `cone 0.211/0.625`, `pipe 0.375/0.594`, `block 0.320/0.531`, `rebar 0.336/0.563`
   (half-width / top). The crate's +30 % height and the barrier's +30 % width scale these boxes with
   the quads. A spawn logs e.g. `obstacle hitbox 'block': visible 0,039 x 0,048 m (alpha bbox of the
   0,061 x 0,091 m sprite)`.
4. **Collision evidence**: `[DINO] collision with obstacle_rubble at x=-0,20 (feet 0,000 < top
   0,034); run ended (Space restarts)` — the hit is on the rubble's visible 3,4 cm top, not its
   9,1 cm cell. The DinoTest still passes to the hit screen with the new boxes.
5. **Diagnostics**: the temporary `DumpAudio` dump (all playing sources) identified the loops and was
   removed from the entry paths; the permanent lines are the two `ambient wind muted/restored` lines
   and the hitbox table.

### v0.10.26: each cabinet's worn-yellow body is repainted in its side art's dominant colour

The user: «Корпус автоматов. Мы раскрасили боковые части, но основная передняя часть корпуса и верхняя
часть остались для каждого автомата жёлтой. Нужно чтобы жёлтый был заменен на преобладающий цвет в
фоновом рисунке для каждого автомата. Но нужно не просто обернуть в новую оболочку, а перекрасить
текущую текстуру, чтобы не потерять текстурность ржавчины и прочие эллемнты декора.»

1. **The body atlas is repainted, not tinted.** `ArcadeCabinets` already gives every cabinet its own
   copy of the embedded 1024x1024 `arcade_body` texture (each `GlbFigureBuilder.Load` decodes it
   separately), so `RecolorCabinetBody(bodyTex, theme)` edits the pixels of that copy:
   only texels with **hue 34-77 deg and saturation > 0.20** (the worn yellow paint) are touched.
   Their hue becomes the side art's dominant hue, their saturation blends 70 % towards it and their
   **own value is kept** (×0.85-1.05), so every rust speck, scratch, dirt gradient and edge darkening
   survives; orange rust (below 34 deg), the dark blue marquee panels, the green strips, the grey
   screen and all lettering are never touched.
2. **The dominant colour is computed from each side art at build time** (`ArcadeDominantColor`): a
   24-bin hue histogram over the 512x896 PNG, weighted by saturation, and the winning bin's mean RGB
   lifted to at least 0.62 value and slightly desaturated/darkened for paint. Logged per cabinet:
   `[ARCADE] cabinet 1: body yellow -> #CC1BB1 (dominant of 'arcade_01.png'), 582446 body texels
   recoloured; rust/orange and dark panels left untouched` — the five themes are
   **#CC1BB1 magenta, #BD24B1 purple-magenta, #E55717 orange, #8F3229 brick red, #C93294 pink**.
3. **The cabinet tops and fronts all change**: the same body atlas covers the front panel, the top
   trim and the control panel, so all the worn-yellow areas now read in the cabinet's own art colour.
   The side art itself is untouched, and the front screen art (`JUMP TANK` / `INSERT COIN`, mirrored)
   keeps its dark blue/green/orange pixels.
4. **Evidence**: one `MapShot` run logged all five recolour lines above plus
   `[SCAN] map shots written`; shots `district_159.png` (the row along the wall: magenta, purple,
   orange, red, pink bodies) and `district_160.png` (the middle cabinets close up: the orange body
   with rust still visible, the red dragon cabinet on the left).
5. **Not new shells**: the GLB, its UVs, colliders and side-art planes are unchanged; only the body
   texture copy is repainted.

### v0.10.25: the foreman now faces right

The user: «И ещё сделай чтобы человечек в игре смотрел вправо, а то сейчас он смотрит в лево».

1. **The generated frames themselves face left.** The PNGs' hat brim, nose, leading leg and arm all
   point to the left edge of the sprite; the screen quad maps `u=0` to the screen's left, so the game
   showed exactly that. The root's 180° turn (v0.10.21) only put the player on the left side of the
   screen — it does not mirror a texture.
2. **Every player frame is mirrored once at load**: `FlipTextureX` reverses each row of
   `player_run.png`, `player_run_2.png`, `player_jump.png`, `player_fall.png`, `player_crouch.png`
   and `player_idle.png`. The background, ground and obstacle textures are untouched; the quad mesh
   and the collision box stay as they were.
3. **Evidence**: after the flip the pale face/nose and the leading leg are on the **right** of the
   head/body in `dino_ready.png` and `dino_test.png` (the same stills that showed the left-facing
   figure before). The run loop, jump, fall and crouch frames were all flipped together, so the
   animation still alternates coherently.

### v0.10.24: no fall wind in the mini-games, sized crates/fences, brighter debris, clipped at the screen

The user: «При игре в Динозаврик или Аэрохоккей звук ветра, буд-то ты падаешь - его нужно убрать,
как когда ты стоишь на земле. Также сделай для разнообразия деревянные ящики на 30% выше, а
заборчики на 30% шире. Все препятсвия сделай немного ярче тоже - не неон, но более отличимыми от
остального в мини-игре. Ещё препятствия появляются и исчезают вне границ игрового экрана Динозаврик,
надо исправить.»

1. **The wind was the fall loop frozen while the controller is off.** `FirstPersonController` writes
   `fallAudio.volume = Lerp(0, 1, fallTime/maxFallTime)` **every Update**; both mini-games disable
   the controller (`fpc.enabled = false`), so the last wind volume stayed on over a player standing
   at the table/cabinet. New `CoopPlayerAudio.MuteFallWind(fpc, log)` (reflection on the private
   `fallAudio` and `fallTime` fields) sets the volume to 0 and clears `fallTime` on entry — from
   `ArcadeGame.Enter` and `AirHockeyGame.StartGame`. Evidence in the log:
   `[COOP] fall wind muted for the mini-game (was 0,00)` on both a cabinet and the table (the
   `was` value is 0 here because the test walks in on foot; if the player enters right after a fall
   it logs the frozen value). On exit the re-enabled controller recalculates the volume itself.
   **v0.10.27 correction:** the fall loop was only part of it — the audible wind over both
   mini-games is the level's global `IndoorWindSound` (0,75) and `OutdoorWindSound` (0,25), which
   are 2D loops; they are now muted around the games too (see the v0.10.27 section).
2. **Wooden crate +30 % taller, fence barrier +30 % wider.** In `SpawnObstacle`, `crate` gets
   `h *= 1.3f` and `barrier` gets `w *= 1.3f`; the sprite is compressed into that quad. The collision
   box follows the new quad: `AlphaBounds` now returns the alpha bbox as **fractions** of the sprite
   (`halfW`, `top`), and each obstacle stores `halfW = halfWf · w`, `top = topf · h`.
3. **All debris a little brighter, not neon.** New `BrightenObstacle` (applied to the eight
   `obstacle_*.png` at load): 22 % saturation around the pixel's luminance plus a gamma lift of
   **0.72**, no highlight clipping. Mean luminance of the sprites rises by ≈25 (crate 84 → 111,
   barrel 75 → 101, rubble 70 → 95, barrier 83 → 108, cone 26 → 46, pipe 106 → 131, block 113 → 137,
   rebar 39 → 58); the light hazard stripes and the dark outlines survive.
4. **Obstacles are clipped at the screen rectangle.** They used to be ordinary quads that spawned at
   `ScreenW/2 + w/2` and were still drawn **on the cabinet's bezel** until they crossed the edge, and
   likewise at the left. Each obstacle now owns a **dynamic 4-vertex mesh**; `UpdateClip` crops the
   quad to `[-ScreenW/2, +ScreenW/2]` (vertices and U/Vs follow the visible part) and hides the
   renderer entirely once outside. Spawning at `ScreenW/2 + w/2` now means "invisible until the right
   edge", then it slides in cropped; removal happens once its right edge is past the left border.
   `dino_spawn.png` (1,22 s into the run) shows only a thin sliver of the barrier at the screen's
   right edge — nothing on the yellow cabinet side.
5. **Evidence**: `dino_spawn.png` (first obstacle entering at the right edge, cropped),
   `dino_test.png` (wider barrier on the rink), `dino_hit.png` («СТОЛКНОВЕНИЕ» at the player), and the
   log `[DINO] collision with obstacle_barrier at x=-0,18`. The previous run with the same obstacle
   code (before the extra shot) logged `obstacle_crate` and showed the taller wooden box.
6. **Not yet**: score, death counter, sound, speed ramp.

### v0.10.23: smaller obstacles, 2x world speed, and the light asphalt ground

The user: «Пока не играбельно. Препятствия слишком большие, сделай их ниже на 40% и уже на 60%.
Увеличь базовую скорость прокрутки мира в 2 раза. Также спрайт земли сливается с текстурой фона,
сделай его сильно светлее, с текстурой асфальта».

1. **The ground was not just blending — it was hidden.** The close-up showed the background's dark
   rubble band where the asphalt should be: both `bgMat` and `groundMat` were using the
   `Sprites/Default` fallback (a **transparent** sprite shader with **ZWrite off**), and the farther
   skyline was drawn after the nearer ground, painting over it. The player/obstacles (queue 3000)
   still showed. `MakeOpaqueMat` now uses **Standard + `_EmissionMap` = the same texture** (opaque,
   depth-writing, unlit-looking because the level ambient is black); the ground finally renders in
   front of the skyline.
2. **Light asphalt ground**: a new SD 1.5 asphalt texture (fine light-grey aggregate, thin cracks,
   top-down, tileable) is lifted into the light range (`150 + 0,55 v`), quantised to light greys
   (168-238), given a dark 2 px top edge and a mirror loop (`ground_loop.png`, 640×64). It now reads
   as a **much lighter road band** against the dark skyline, with visible grain and cracks.
3. **Obstacles 60 % narrower / 40 % lower**: the square cell is now `W = cell × 0,4`,
   `H = cell × 0,6`; the sprite is compressed into it, the spawn/removal and collision bbox scale with
   it (`halfW × 0,4`, `top × 0,6`). The crate at the player's feet is now about 0,04 × 0,07 m on the
   0,76 m screen instead of 0,10 × 0,13.
4. **World speed ×2**: `ScrollPx` 120 → **240 px/s** at the 320-px internal width. Ground and
   obstacles move at 0,57 m/s, the skyline at 0,6× = 0,342 m/s. Evidence: after 1,4 s
   `ground=0,526 bg=0,158` (the old build was 0,263/0,079 at the same time).
5. **Evidence shots**: `dino_test.png` (light asphalt, small crate on the right), `dino_crouch`,
   `dino_jump`, `dino_hit.png` (small crate at the foreman + «СТОЛКНОВЕНИЕ»), log
   `[DINO] collision with obstacle_crate at x=-0,20`.
6. **Not yet**: score, death counter, sound, speed ramp.

### v0.10.22: the scrolling fix, half-speed run frames, and random obstacles

The user: «Земля и фон почему-то не едут, найди ошибку и почини. Также сделай чтобы при анимации бега
кадры сменялись в 2 раза реже чей сейчас. После выполнения, продолжи работу над игрой - нужны
препятствия с их генерацией в произвольном порядке … про смену кадров я имею ввиду именно кадры
анимации бега».

1. **Why nothing moved — the shader, not the offsets.** The logs showed `ground=0,263 bg=0,079`,
   but the skyline/ground stills were identical. `Unlit/Texture` is **not in this build's shader
   list**; `Shader.Find` fell back to `Sprites/Default`, whose sprite vertex shader does **not**
   apply `_MainTex_ST`, so `material.mainTextureScale/Offset` was silently ignored. The fix writes
   the **quad mesh's UVs** every frame instead (`SetScrollUV(bgMesh/groundMesh, offset, scale)`),
   which works with any shader and with the Repeat wrap. The new stills show a different skyline
   (offset 0,339) and the ground shifted; the offsets still log.
2. **Run frames half speed**: `RunFrameTime` 0,12 → **0,24 s** (the run only; jump/fall/crouch are
   event-driven and unchanged).
3. **Obstacles with random generation.** The eight sprites (`crate`, `barrel`, `rubble`,
   `barrier`, `cone`, `pipe`, `block`, `rebar`) are loaded as separate unlit-transparent materials.
   When the run is active, one spawns every **1,0–2,2 s** at the right edge, chosen with
   `Random.Range(0, 8)`; each moves left at the **same 1X ground speed** (120 px/s at 320 px →
   0,285 m/s on the 0,76 m screen) and is removed off-screen. The hit box is the sprite's **alpha
   bbox** (`AlphaBounds`, 64×64 → half-width/top in metres); the foreman's half-width is 0,04 m,
   and a jump above the obstacle's top clears it. A hit freezes the run and shows
   «СТОЛКНОВЕНИЕ — ПРОБЕЛ ЗАНОВО»; Space restarts (new random sequence), E leaves.
4. **Evidence**: `dino_test.png` shows one spawned obstacle; `dino_crouch`/`dino_jump` keep it in
   the frame; the auto-test then runs long enough to hit the first one:
   `[DINO] collision with obstacle_pipe at x=-0,16; run ended (Space restarts)` and
   `dino_hit.png` shows the message, the obstacle on the foreman and a second random obstacle
   behind. `dino_ready` (offset 0) vs `dino_test` (ground 0,339 / bg 0,102) proves the scroll.
5. **Not yet**: score, death counter, sound, difficulty ramp. The obstacles and the run/hit loop
   are working.

### v0.10.21: the playable "Dinozavrik" test on the cabinets

The user: «Думаю можно собрать тестовую версию игры. Сделай взаимодействие с автоматами на "E".
Человечек находится в левой части экрана лицом в право и без остановки бежит вправо (не меняя своё
положение по горизонтали относительно границ экрана), под ним двигается в противоположную сторону
поверхность земли со скоростью 1Х. Задний фон двигается в ту же сторону что и земля, но со скоростью
0.6Х. При нажатии пробела игроком, человечек прыгает. При нажатии CTRL человечек приседает. При
заходе в игровой режим Динозаврика всё стоит на месте и для начала игры нужно один раз нажать
пробел.»

1. **E at a cabinet.** `ArcadeGame` (created once by the plugin, like the air hockey) watches
   `SovietDistrict.ArcadeCabinetList` (filled by `ArcadeCabinets`). Within 2 m of a cabinet's front
   it shows «E — Динозаврик» and E detaches the camera to the cabinet's screen: FPC off, camera
   0,85 m from the screen, a 4:3 quad face **0,76 × 0,57 m** just in front of the painted screen.
   The game textures are the generated `arcade_game` PNGs (background 1280×240, ground 640×64,
   foreman frames 64×64), unlit, Point filtering.
2. **The screen is not mirrored.** The first test put the player on the right and facing left: the
   camera's right is local −x, so the root is turned **180°** and the quad mesh faces local −z. The
   foreman then stands in the **left** part of the screen, facing right; the ground and skyline read
   normally.
3. **Frozen until Space.** Entering sets `started=false`: the run frame is still, both texture
   offsets are 0, and the HUD shows «ПРОБЕЛ — СТАРТ». The first Space only starts the run; a second
   Space jumps. While running: ground offset **1X = 120 px/s** at the 320-px internal width
   (0,1875 UV/s on the 640-px ground), background **0,6X** (0,05625 UV/s on the 1280-px skyline),
   both moving left under the fixed left-hand foreman. Space = jump (1,5 m/s, gravity 8 → ≈0,14 m
   high), Ctrl = crouch (the squashed frame), E = leave. Run frames alternate every 0,12 s; in the
   air the jump/fall frames are used.
4. **Held items hidden.** The propeller/gadget viewmodels are not all `InteractObject`s, so on top
   of the old held-item loop every **Renderer under the camera** is disabled for the game view and
   restored on exit — the first version had propeller blades over the screen.
5. **Debug (`[Debug] DinoTest = true`, self-resets).** `ArcadeGame.AutoTest` teleports the player to
   cabinet 3, enters, waits, starts, and writes `dino_prompt.png` (pre-entry view),
   `dino_ready.png` (frozen, «ПРОБЕЛ — СТАРТ»), `dino_test.png` (running), `dino_crouch.png` and
   `dino_jump.png` (jump = 0,13 m in the logged frame), then leaves. Log:
   `[DINO] entered the arcade cabinet at …`, offsets `ground=0,263 bg=0,079` (ratio 0,3 in UV =
   0,6 in pixels), `[DINO] left the arcade cabinet`.
6. **Not in this test**: obstacles, score, deaths, sound. The screen, controls and parallax loop
   are working; the crates/debris and the run-to-death loop are the next round.

### v0.10.20: the foreman's run/jump/crouch frames, and 60 % bigger cabinets

The user: «Бригадир пойдет, сделай ему простенькую зацикленную анимацию бега, анимацию прыжка и
приседания. Также увеличь игровые автоматы на 60%».

1. **Foreman frames** (`model/arcade_game/player_foreman/`, all 64×64, muted palette, 1 px outline):
   - **run_1 / run_2** — the chosen foreman and an SD img2img near-stride at denoise 0,58
     ("the other leg forward, opposite arm forward"); alternate them for a **2-frame loop** at
     ~8 fps. The character's hat/coat persist; the stride swaps.
   - **jump** — the SD bent-knee pose, lifted 8 px in the cell; **fall** — the SD legs-extended
     pose, lifted 6 px. The game moves the cell up/down for the jump arc.
   - **crouch** — the run sprite squashed to **72 % height / 108 % width** (the AI crouch attempts
     either turned front-facing or stayed a wide step); it reads as the foreman ducking.
   - **idle** — the SD standing pose.
   - Top-level copies: `player_run.png`, `player_run_2.png`, `player_jump.png`,
     `player_fall.png`, `player_crouch.png`, `player_idle.png`. The old variant-1 player files are
     replaced by the foreman.
   - Previews: `player_foreman/preview_foreman.png` (6 frames at 5× with labels) and
     `player_foreman/run_loop.gif` (the 2-frame run, 8 fps, looped).
2. **Cabinets ×1,6.** `ArcadeCabinets` sets `localScale = 1.6` after loading: **1,80 m → 2,88 m**
   tall, width 0,795 → **1,27 m**, depth 0,99 → **1,58 m**. The origin is at the feet/back, so they
   grow upward and into the room; the backs stay 1,03 m off the wall and the 2 m spacing leaves
   0,73 m gaps. The window sills are covered more (accepted). Map-shot cameras re-framed for the
   taller row: `district_159` at house-local (-39,5, 105,9, -4,6) and `district_160` at
   (-30,5, 105,6, -7,0).
3. **Still not wired into the cabinet screens** — the frames are the animation set for the next
   round; the cabinets remain decor.

### v0.10.19: muted palette + four new player variants

The user corrected v0.10.18: «Общая гамма серая, но не совершенно чёрно-белая, нужные и другие цвета
в палитре, но избегай ярких оттенков. Также мне не нравится эта кривоногая модель человечка - дай
мне 4 новых варианта на выбор».

1. **Palette — 20 colours.** Ten neutral greys (`#0C0C0C … #AEAEAE`; the top step is 174, no white)
   plus ten muted colours: olive `#6A6450` / `#7A745E`, rust `#6E4E3E` / `#805E4A`, teal
   `#4E6662` / `#5A7470`, blue-grey `#56606C`, ochre `#8A7350`, brick `#744B42`, bone `#A89C88`.
   No bright shades; the sky stays a flat colourless grey.
2. **Clean pixel step.** Every asset is downscaled first, then pre-smoothed (Gaussian 0,6-0,8 +
   median 3) before the palette mapping. Without this the AI detail quantised into a salt-and-pepper
   mess; the smoothed versions have flat pixel areas and a 1 px near-black outline.
3. **Four player variants** (`model/arcade_game/player_variants/`), all in the same side-view run
   pose (the game is a runner, the old bow-legged front sprite is gone):
   - **1 STOCKY** — dark knit cap / helmet, ochre vest, blue-grey trousers, work boots;
   - **2 TALL** — rust flat cap, light shirt, grey trousers, lean figure;
   - **3 WOMAN** — headscarf, blue jacket over an ochre vest, dark trousers;
   - **4 FOREMAN** — dark hat, pale face, long charcoal coat, blue trousers.
   They are 64×64 cells with a 44 px figure, generated by SD img2img at denoise 0,68 from the
   side-run init (the 0,52 pass only recoloured the same worker; 0,68 changes the head/outfit while
   keeping the side stride). `player_run.png` currently = variant 1; jump/idle will be made for the
   chosen one.
4. **Everything re-quantised to the muted palette**: background (median 3, neutral greys), ground
   (rubble + 14 % slab texture, median 3 — muted ochre/teal hints), obstacles (muted crate brown,
   rust barrel, ochre barrier, etc.). Old `player_jump/idle.png` removed until the choice.
5. **Previews**: `player_variants/preview_variants.png` (all four at 6× with labels) and
   `preview_game.png` (320×240 upscaled 3× with variant 1, the muted crate/barrier/rubble and the
   grey skyline). `manifest.json` now carries the palette, the variant list and `current`.

### v0.10.18: the "Dinozavrik" game assets — grey, heavy, angular

The user: «Сгенерируй через старую дифюжн все составляющие для нейрослопной пиксельной игры (назовём
её "Динозаврик"). Нам нужен зацикленный медленно проплывающий задний фон, быстро проплывающая
поверхность земли, модель человечка (он у нас будет вместо динозавра), ящики (а также разного рода
строительный мусор. Они будут в роли кактусов из оригинальной игры) Ключевая стилистика игры — ретро
нейрослоп в тематике брутализма». Follow-up: «цветовая грамма серая, тяжелая. Формы угловатые. Небо
на фоне бесцветно-серое.»

1. **Palette.** 18 neutral grey levels (`#080808 … #F4F4F4`), no hue anywhere — not even a warm
   accent. Every background, ground and sprite is desaturated and quantised to that palette, then
   pixel-scaled with NEAREST. The game's internal resolution is **320 × 240** (4:3, the cabinet CRT).
2. **Background** (`model/arcade_game/bg_loop.png`, 1280 × 240): a hand-drawn angular brutalist
   skyline base (sawtooth roofs, heavy cornices, deep window grids, tower cranes, a flat colourless
   grey sky), refined with SD 1.5 img2img at denoise 0,45, then greyscaled/quantised and downscaled
   to 640 × 240. The loop is **mirrored** (640 + its mirror), so the seam is exact; it scrolls slowly.
3. **Ground** (`ground_loop.png`, 640 × 64): the angular rubble base plus 10 % of a generated concrete
   slab texture, greyscaled/quantised, with a jagged dark surface line; mirrored into a 640 px loop;
   scrolls fast. The old light-slab texture was rejected — it read as a masonry wall.
4. **Player** (`player_run.png`, `player_jump.png`, `player_idle.png`, 48 × 48): SD img2img over
   hand-drawn magenta-background side silhouettes (run with the legs apart, jump with the legs
   tucked, idle); the largest alpha component is kept (shadows/stray marks dropped), the sprite is
   greyscaled, quantised and given a 1 px near-black outline. The run sprite is the game's "dino".
5. **Obstacles** (`obstacle_*.png`, 64 × 64): eight SD-refined magenta silhouettes — wooden crate,
   rusty barrel, concrete rubble pile, striped barrier, traffic cone, concrete pipe/culvert, heavy
   concrete block, rebar bundle. These are the "cacti"; the crate is darkened to 0,78 so it does not
   read as white in the grey set.
6. **Previews**: `preview_game.png` (960 × 720, the 320 × 240 frame upscaled 3× — skyline, ground,
   worker, crate/barrier/rubble) and `preview_sprites.png` (the three player frames + eight
   obstacles). `manifest.json` lists sizes, loop mode, palette and the internal resolution.
7. **Not wired into the cabinets yet** — these are the generated components for the next round; the
   five cabinets remain decor for now. `build.ps1` copies `arcade_game/` into
   `plugins/BabbdiCoop/arcade_game` and prints `Dino assets: N files`.

### v0.10.17: five AI-slop arcade cabinets along the top hall's back wall

The user: «в "E:\858\ASSET\arcade-cabinet" лежит модель игрового автомата. Через него теперь мы
сделаем простенькую игру по типу прыгающего динозавра в гугл хром… Для этого поставь автомат в метре
от стенки задней стороной в верхней комнате с одним столом. А лучше поставь сразу 5, но для каждого
сделай уникальную раскраску через стейбл дифужн как для столов ранее (Нейрослоп, нечитаемое название
ретро игры. Нанеси изображение на боковую сторону, отзеркалив со второй стороны). Пока оставь их как
декор на растоянии двух метров друг от друга.»

1. **Asset → GLB.** `E:\858\ASSET\arcade-cabinet\source\arcadeCab.zip` is a binary FBX
   (`BabineauS_ArcadeCabinet.fbx`, 3512 verts / 1756 tris) plus a 2048² TGA base colour. It was
   read with **Assimp 6.0.6** through `pyassimp` (no Blender on the machine) and rewritten as
   `model/arcade_cabinet.glb`: 1,80 m tall, feet at y=0, **back plane z=0**, front facing **+z**,
   two primitives — the body (1432 tris) and the outer side panels (324 tris). The 1024² body
   texture is embedded; the side primitive carries a placeholder that each cabinet replaces at
   runtime.
2. **Side art.** Five 512×896 Stable Diffusion images (`model/arcade_art/arcade_01…05.png`): 80s
   arcade side-panel airbrush/neon AI slop with **unreadable pseudo-title lettering** — chrome skull,
   cyber ninja on a laser bike, pixel octopus, chrome dragon, robot samurai. Each cabinet's side
   primitive uses planar UVs over the panel's own y/z extent; the −x side mirrors U, so the second
   side is the exact mirror image. The source front/marquee keeps its worn-yellow atlas (its
   lettering is itself mirrored slop, which fits the brief).
3. **Placement.** The annex's top hall (house-local floor 102.55 m), along the **back wall**
   z = −14 (inner face −13.53): backs at z = −12.5 (**1,03 m** off the wall), centres
   x = −34.5 / −32.5 / −30.5 / −28.5 / −26.5 (**2 m** apart), fronts facing +z into the room,
   each with a BoxCollider. They stand under the window sills (1,8 m).
4. **Decor only for now** — the Chrome-dino-style game is the next round. Evidence:
   `[ARCADE] cabinet 1…5` with positions/art names, the summary `[ARCADE] 5 of 5 cabinets …`, and
   map shots `district_159` (the row along the wall) / `district_160` (the middle cabinet: side art,
   worn front, screen).

### v0.10.16: idle side mallets mark the blue and red ends

The user: «Теперь нужно показать игроку где синяя сторона, а где красная - пускай пока стол не
используется, биты будут видны стоящими на поверхности поля. Со стороны синих, она должна стоять на
поле правее ворот - синего цвета. Для красных зеркально, но красная.»

1. **Two mallets stand on the unused rink**, one per side, built with the same model as the play
   mallets (disc 0,352 m dia × 0,0495 m thick, 0,08 × 0,18 m capsule stick) and no colliders. They
   stand on the playfield 2 cm above the rink face (`AirHockeyRinkTop + 0,02`).
2. **Placement is "right of the goal" from that player's own end**: a player at +x faces −x, so
   their right is +z; blue therefore stands at **(+1,78, +0,60)** and red is the mirror image at
   **(−1,78, −0,60)** — just outside the goal mouth (posts at z ±0,42) and inside the rink
   (z ±1,28, x ±2,16).
3. **Colours**: red `#9E1F14`, blue `#1F52A8`. The blue is now shared with the mini-game's client
   mallet (`AirHockeyGame.matClient`), so the blue you see on the idle rink is exactly the blue you
   play with; the HUD already calls the side «СИНИЕ».
4. **Visibility**: `AirHockeyScoreboard.SetInUse(id, true/false)` toggles the pair — `StartGame`
   hides them (the live mallets replace them), `StopGame`/`ForceStop` brings them back. They are
   parented to the table (not the arch), so they do not depend on the lamp.
5. **Evidence**: `[SCORE] table N: idle side mallets — red at (-1,78, -0,60), blue at
   (+1,78, +0,60) on the rink, right of each goal`; MiniTest's `mini_idle.png` shows the blue marker
   standing on the near half while the table is idle. During play `mini_test.png` shows only the live
   mallets. No packet changes.

### v0.10.15: the lamp-end score boards — remembered score, acid-green digits

The user: «Мне нужно чтобы при выходе из меню мини игры стол помнил счёт на нем и выводил уго на
небольшом трапециевидном табло в сторону игроков по обе сторону, обновляясь в реальном времени. Само
табло по форме и размерам должно повторять торец лампы стола и быть градусов на 10 наклонено вниз, к
игроку. Цвет корпуса табло - цвет лампы (именно его корпуса к торцу которого оно крепится и чего
является продолжением, а не того каким светом светит лампа). Экран матово черный, а на нём цифры
кислотным маркером (только сделай их кислотно зелеными, а не жёлтыми. шрифт оставь этот). Между
цифрами должен быть небольшой дефис, того же зелёного цвета».

1. **Geometry = the lamp's end face.** The arch canopy in `airhockey_arch.glb` is a trapezoid in
   section: top z ±0,425 m, bottom z ±0,304 m, 0,148 m tall, its ends at x = ±1,656 m. Each board is
   that exact trapezoid continued 6 cm outward, so it reads as the lamp housing's own end cap. It is
   hinged at the canopy's top edge and tilted **−10°** so its face looks down at the player standing
   at that end. The two boards are `scoreboard_blue` (+x) and `scoreboard_red` (−x).
2. **Body colour = the housing, not the light.** The board's material is built from the **canopy's own
   albedo**: the flat `themeMat` colour for the ten showroom tables, or the canopy texel sampled from
   the main arch's 1024 atlas. It carries a mild self-emission (0,35 × its own colour) so the table's
   green/cyan lamp light cannot turn the body into the light's colour. Evidence:
   `[SCORE] table 10 … housing = the lamp body colour #AB439D` (sloppy_10 pink) and
   `[SCORE] table 0 … #221B12` (the main arch's dark oxide body).
3. **Screen:** an inset trapezoid (12 mm border) with an **unlit** near-black material — genuinely
   matte, no specular from the lamp. The digits are the chosen **acid-marker** glyphs (the `v2_acid`
   set), recoloured to pure acid green (R ≤ 0,36·G, B ≈ 0,06·G) and packed with alpha into
   `model/score_glyphs.png` (2860x256: eleven 260x256 cells, glyphs 200 px tall). The hyphen is a
   middle slice of the **7**'s top bar, so it carries the same marker texture. Layout is
   `red-green-blue` with a small gap, centred on the screen; the glyph meshes are rebuilt only when
   the score changes.
4. **The table remembers.** The scores live in the `AirHockeyScoreboard` component (per table id),
   not in the mini-game session: `ScoreGoal` and the client's state packet update the board at once,
   `StopGame` leaves it on, `StartGame` **continues** a running match from the board's score (a
   finished match starts a new 0-0). Evidence: after the MiniTest goal the board shows `1-0`; the
   re-entry HUD reads `КРАСНЫЕ 1 : 0 СИНИЕ`.
5. **The boards belong to the lamp.** They are parented to the arch, so the mini-game's
   `arch.SetActive(false)` hides them as well. This was measured, not assumed: with the boards
   parented to the table (always visible) the near board floated over the **centre of the rink** and
   covered the puck's lane (`mini_test.png`). Their meshes keep updating while hidden, so stepping
   back from the table always shows the current score, and a spectator at the table sees it change
   live.
6. **Evidence**: `[SCORE] digit atlas loaded: score_glyphs.png 2860x256, 11 cells (0-9 and the
   hyphen), acid green`; eleven `[SCORE] table N …` lines (both boards on every table); the goal
   `[MINI] goal in the north net -> 1:0`; `[MINI] score-board shot -> …\mini_scoreboard.png` showing
   the pink housing, black screen, green `1-0` and the marker hyphen; the re-entry HUD `1:0`.
   `build.ps1` now also prints `Score glyphs: score_glyphs.png …`.

### v0.10.14: side chosen by the goal you approach, and all 11 tables playable

The user: «Измени немного логику миниигры — нужно чтобы то за синих ты играешь или за красных
определялось стороной с которой ты подходишь для взаимодействия со столом. Также, сделай играбельными
все столы, включая кастомные, а не только верхний.»

1. **All tables are in one list**: `SovietDistrict.AirHockeyTables` holds the main table (id 0) and the
   ten showroom tables (ids 1…10), each with `Root`, `Arch`, `Id`, `Name`. The list is cleared and
   rebuilt on every level build, so ids are stable on every peer.
2. **Nearest table + approach side**: `AirHockeyGame.FindNearbyTable` checks every table's two goal
   points (`x = ±2,576`, `z = 0`) within the 1 m trigger. The nearest table is selected, and the
   **closer goal is the player's side**: local −x = red (side 0), local +x = blue (side 1).
   `AcquireTable` binds `table`, `arch`, `localTableId` and `localSide`; the E prompt works at any
   table, and the camera / FPC stance / mallet start use the chosen side.
3. **Host and client can each choose any side**: the host's mallet is on its approach side; a client
   whose approach side is the **same** as the host's is rejected (`ЭТА СТОРОНА У ХОЗЯИНА — ПОДОЙДИ
   С ДРУГОГО КРАЯ`). The first client on the opposite side owns that side (`clientSide`), and the
   state carries `hostSide`/`clientSide`. Mallet/rail colors follow the side, not the host/client
   role. The HUD labels the sides red/blue and says `ВЫ ЗА КРАСНЫХ/СИНИХ`.
4. **Protocol v0.10.14**: mallet/leave packets are 23 bytes (tableId at byte 22); the state packet is
   52 bytes (hostSide 49, clientSide 50, tableId 51). A client at a different table ignores the state
   and its mallets are ignored by the host (`ПАРТНЁР ЗА ДРУГИМ СТОЛОМ`). Both peers need v0.10.14.
5. **Any table is playable**: the custom tables' themed field, raised bumper, side skin and arch are
   all present; `BuildVisuals` attaches the mallets/puck/audio to whichever table was selected. The
   `AirHockeyTables` id is logged (`joined table 10 (side blue …)`), and MiniTest now teleports to the
   last showroom table (frog, id 10) and enters from its blue end.
6. **Evidence**: `[MINI] MiniTest: teleported to table 10 blue end (near check = True)`,
   `[MINI] host joined table 10 (side blue, …)`, then the goal/screenshot; `mini_test.png` shows the
   custom table 10 with the mini-game field, themed bumper and blue-side mallet. `[PROBE]`/map shots
   are unchanged; the main table still appears as id 0.

### v0.10.13: bumper narrowed by 3 cm, and the field's red/blue become arch/lamp colours

The user: «Новые бортики широковаты, убери выступающие края на 3 см. Также измени рисунок на поле столов
по следующему алгоритму: рисунок который мы имеем сейчас на белом поле из кругов и линий двух цветов —
красный и синий. Нужно чтобы ты для каждого кастомного стола на поле заменил красный на цвет-1 и синий
на цвет 2, где цвет-1 это цвет арки стола, а цвет-2 это цвет с которым светит лампа этого стола.»

1. **Bumper −3 cm**: the raised ring's top outer edge is now
   `ox − 0.03 / oz − 0.03` (2.546/1.913 instead of 2.576/1.943). The outer wall leans inward
   over the 5 cm height (its normal is computed from the sloped quad), so the old white rim under
   the 3 cm strip is covered — no white edge is exposed. The top ring is 3 cm narrower all round.
2. **Field recolour**: `RetextureAirHockeySides` now also pulls the horizontal rink top (y ≈ 1.104,
   normal up) into its own `airhockey_field` mesh and gives it a per-table material. The material's
   texture is a clone of the table's original atlas; `MakeAirHockeyFieldMaterial` replaces:
   - **red** pixels (r > 0.30, r > g·1.20, r > b·1.15) with **`arcColor`** — the exact arch canopy
     colour (`Color.Lerp(theme, black, 0.18)`), the same material instance used by the bumper/arch;
   - **blue/teal** pixels (b > 0.20, b > r·1.05, g > r·0.95) with **`lampColor`** — the LED/lamp
     colour `Color.Lerp(accent, white, 0.12)`.
   The bone background and anti-aliased edges are kept (the blend strength follows the pixel's
   red/blue dominance). The field mesh is exactly coplanar with the removed rink triangles, so no
   z-fighting. The main playable table's field stays original.
3. **Result**: on variant 1 the red circles/centre line are pink (arch) and the blue/teal centre
   ring/side lines are light cyan (lamp); the bumper is the same pink as the arch and is 3 cm
   narrower at its top edge. Shots `district_158` (variant 1 close-up) and `district_146` show it.
4. **Build**: v0.10.13, `Patterns: 10 PNG`, no errors.

### v0.10.12: the showroom tables get a raised, arch-coloured horizontal bumper (+5 cm)

The user: «сделай широкие горизонтальные бортики столов, которые сейчас белого цвета, тем же цветом на
кастомных столах, которого на них арка (держащая лампу над столом). Также бортик должен немного выступать,
нарости его вверх на 5 см.»

1. **The white rim is covered, not recoloured in place.** The table's wide horizontal white rim is an
   irregular ring at y = 1.1408 with a 3.7 cm step down to the rink at 1.104. A new
   `AddRaisedArcadeBumper` builds a **closed-looking raised ring** on top of it: outer edge
   x ±2.576, z ±1.943; inner edge (rink) x ±2.298, z ±1.395 (2 mm inside the old white lip, so the
   inner wall cannot z-fight); top at **y = 1.1908**, i.e. exactly **5 cm above** the old table top.
   The ring has an outer wall from 1.1408, an inner wall down to the rink 1.104, and the wide
   horizontal top face. It is one `Geo` mesh, part 0 only, parented to the table.
2. **Same material as the arch.** `AirHockeyVariants` creates one `themeMat`
   (`Color.Lerp(theme, black, 0.18)` with emission 0.12) per table and passes the **same instance** to
   both the new bumper and `PaintAirHockeyArch`'s canopy submesh. The visible ring and the arch canopy
   are therefore exactly the same colour, not two similar tints. The arch's support rods keep the
   accent and the underside keeps its deep tint.
3. **Collider**: each showroom table's `BoxCollider` height is now
   `AirHockeyBodyTop + AirHockeyBumperRaise = 1.1908 m`, with the centre at half that, so the player
   cannot walk through the raised rim. The playable top-floor table keeps its original 1.1408 collider
   and white rim; the change is only for the ten custom/showroom tables.
4. **What stays original**: the rink top (bone with the marks), the black legs, the patterned side
   skin below the bumper, and the arch. Only the wide white horizontal border is replaced.
5. **Evidence**: `district_158` is the close 3/4 of variant 1 — a **pink** raised ring, 5 cm taller
   than the old table top, matching the pink arch canopy; the pattern side continues below it and the
   legs are still black. `district_146` and the other showroom shots show the same change on all ten
   tables. Build `v0.10.12`, no exceptions; map shots written normally.

### v0.10.11: air-hockey SFX and the goal-replay puck animation (1 s delay + 1.5 s drop/bounce)

The user: «добавить игре звуковое сопровождение. В `E:\858\sound_3` лежат рабочие файлы. Используй Click
при каждом столкновении шайбы с бортиком или битой, banka когда шайба попадает в ворота. Также сделай
чтобы перед тем как после гола шайба возвращалась в игру, была задержка в 1 секунду, а сама шайба
возвращалась с анимацией — она появляется над поверхностью стола (проявляясь из состояния полной
прозрачности за пол секунды) и падает на него с высоты 10 см, дважды слегка прыгая на ребрах перед тем
как занять конечную позицию (вся анимация без учета её задержки после гола это 1,5 секунды).»

1. **Sounds**: the two requested MP3s are copied to `BabbdiCoop/sounds` by `build.ps1`
   (`Sounds: 3 MP3`): `click-close-sonorous-noisy.mp3` (0.71 s) and `avtomat--banka-padaet.mp3`
   (1.10 s). `AirHockeyGame.EnsureAudio` creates a 3D `AudioSource` at the table
   (`spatialBlend 0.65`, min 2 m, max 20 m) and `LoadArcadeSounds` decodes both with
   `UnityWebRequestMultimedia.GetAudioClip(file://…, AudioType.MPEG)`. The log is
   `[MINI] sound loaded: click-close-sonorous-noisy (0,71 s)` /
   `[MINI] sound loaded: avtomat--banka-padaet (1,10 s)` / `arcade sounds ready: click=ok, goal=ok`.
   A missing file logs a warning and the game continues silently.
2. **Click events**: every mallet hit (`HitMallet` now returns bool) and every rail / goal-post bounce
   calls `QueueClick()`; it has a 30 ms cooldown so a mallet resting on the puck cannot buzz. The host
   increments `clickSeq`; the state packet carries it, so a client plays exactly the same clicks.
3. **Goal banka**: `ScoreGoal` increments `goalSeq` and plays the banka locally; a client watches
   `goalSeq` in the state and plays its copy. Old/lost packets are harmless because the byte counters
   are compared modulo 256.
4. **Puck replay**: after a goal `StartPuckRespawn` hides the puck for **1.0 s** (`puckVisualAlpha = 0`),
   then runs the **1.5 s** animation: 0–0.5 s fade-in at **y = +0.10 m**; 0.5–0.85 s fall to the rink;
   0.85–1.15 s first edge bounce (y up to 0.035 m, tilt 14°); 1.15–1.38 s second edge bounce
   (y 0.018 m, tilt −9°); 1.38–1.5 s settle. Physics is paused for the full `1.0 + 1.5 = 2.5 s`.
   The first landing also queues a soft click.
5. **Client sync**: the state packet grew from 35 to **49 bytes** with `puckY`, `puckAlpha`, `puckTilt`,
   `clickSeq`, `goalSeq`; clients render the same animation and hear the same events. Mallet/leave
   packets stay 22 B; the pose stream is unchanged. Both peers need v0.10.11 for the new fields.
6. **Puck material**: the puck now uses a transparent Standard material (`_Mode 3`, SrcAlpha,
   `_ZWrite 0`, queue 3000) so `_Color.a` can drive the fade; at alpha 1 it looks like the old puck.
7. **Evidence (solo MiniTest)**: `[MINI] goal in the north net -> 1:0`; debug frames at t = 0.42
   (`y 0.100, a 0.85`), 0.74 (`y 0.054`), 1.08 (`y 0.024, tilt 9.8°`), 1.42 (`y 0`), then
   `respawnActive=false`; `mini_test.png` / `mini_respawn_bounce.png` in `map/`. The actual audio is played
   through the game's AudioSource; the clips decode in the game log.

### v0.10.10: four more showroom tables one floor lower — muted lettering, two cartoon-animal designs

The user: «сделай ещё 4 стола на ещё один этаж ниже. По тому же алгоритму, сгенерировав для каждого
уникальный дизайн с теми же настройками, но сделай акцент на нечитаемые надписи. И немного меньше неона.
На 2 из них добавь очень нейрослопных мультяшных животных.»

1. **Generation**: four new 768×512 SD 1.5 images with the same settings as the first six (42 steps,
   CFG 8, DPM++ 2M Karras), but the prompt asks for **dense unreadable pseudo-lettering, muted matte
   ink and less neon**. Two prompts add a **very deformed melted cartoon animal mascot**: `sloppy_08`
   is a wall of weird orange/teal cats, `sloppy_10` is a big pink/green frog among lettering.
   All four copied/upscaled to 1536×1024 as `patterns/sloppy_07…10.png` (`Patterns: 10 PNG`).
2. **Placement**: the four tables are on the annex's next floor down, house-local **y = 89.75**, in the
   north strip of the ground ring at z = −11, x = −44/−35/−26/−17 (the lower tower occupies the middle,
   so this is the free 6 m strip). The six from v0.10.8 remain at 96.15.
3. **Same skin/arch algorithm**: `RetextureAirHockeySides` applies each pattern directly to the table's
   own vertical side triangles (flush, no gap); the rink top, rim top and black legs stay original. The
   arch canopy uses the new dominant hue, the support rods its accent, and the LED strip/point light
   follow. Themes: muted acid yellow `#CCDB0F` + pink, muted orange `#DE6B21` + teal, muted green
   `#1CC25C` + brown, muted pink `#D152BF` + green.
4. **Evidence**: `[PROP] arcade variant 7/10 … 10/10` and
   `[PROP] arcade showroom: 10 of 10 patterned tables on the annex floors below the playable table —
   six at 96.15 m and four at 89.75 m; rink tops untouched, legs black`. Map shots `district_151`
   (row overview), `district_152` (plan), `district_153`/`154` (animal tables in perspective) and
   `district_156`/`157` (flat side elevations: the cats and the frog read on the side skins).
5. **Status**: still showroom pieces; only the top-floor table is wired to the mini-game. The animals
   live in the generated side pattern, not as separate 3D props.

### v0.10.9: the showroom pattern is flush with the table — no floating band, no gap

The user: «Наш кастомный бортик недостаточно плотно прилегает к столу, нужно чтобы между ним и столом не
было этой щели — вдави его внутрь без зазоров.»

1. **Why the gap was real**: the first version was a separate closed ring at the table's *bounding*
   rectangle (`z ±1,945`). The actual body is not that rectangle: at the body's lower edge
   (y = 0.405) the main side is `z ±1,336` with end caps at `z ±1,639`; at the rim (y = 1.141) the side
   is `z ±1,397` with caps at `z ±1,761` and a central tip at `z ±1,943`. The ring therefore floated
   up to ~30 cm off the long sides and only touched the central protrusion.
2. **The fix — retexture the table's own side triangles**: `RetextureAirHockeySides` reads the loaded
   `airhockey.glb` mesh, classifies every triangle (`isSide = centroid y in 0.405…1.141 and |n.y| ≤ 0.60`),
   removes the side triangles from the body and rebuilds them as a second mesh **exactly coplanar**
   with the originals. The body keeps the original material and UVs for the rink top, rim top, underside
   and black legs; the new side mesh gets the pattern material. No offset, no ring, no gap.
3. **UV wrap**: the side mesh uses a cylindrical angular wrap around the table's vertical axis, `u` 0…2
   with `TextureWrapMode.Mirror`, `v` 0 at y = 0.405 and 1 at y = 1.141. The seam is at the front-right
   corner; per-triangle angular unwrapping keeps each triangle on one side of the seam, so the mirrored
   wrap stays invisible. The pattern now follows the end caps and the flared rim instead of cutting
   through them.
4. **Unchanged**: the six pattern images/themes and the arch paint from v0.10.8; the rink top is still
   the original bone surface; the legs are still black; the six tables are still showroom pieces, not
   playable.
5. **Evidence**: `[PROP] arcade variant 1/6 … 6/6` now says `mirrored own side faces`; the follow-up
   `[PROP] arcade showroom: 6 of 6 … rink tops untouched, legs black` is unchanged. Map shots
   `district_146` (all six), `district_147` (plan), `district_148/149` (close-ups) and
   `district_150` (side/arch) show the pattern sitting directly on the table sides with no air gap.

### v0.10.8: six patterned air-hockey tables on the annex floor below the playable one

The user: «Сделай в том же здании с основным тестовым аэрохоккеем 6 уникальных его ретекстуров на этаж
ниже. На каждый сбоку растяни по одной картинке из шести выше сгенерированных на его бортики со всех
сторон отзеркалив по одной оси безшовно. Ножки оставь черными, а верхнюю часть с лампой на двух палочках
раскрась под стиль раскраски каждого конкретного стола… Горизонтальную поверхность поля пока оставь без
изменений.»

1. **Where**: the annex's floor below the playable table, house-local **y = 96.15** (the main table is at
   102.55). Six copies of the same `airhockey.glb` + `airhockey_arch.glb` in a 3 x 2 grid at
   house-local x = −44/−30.5/−17, z = 6.5/−4.5. The main table's statics (`AirHockeyTableGo`,
   `AirHockeyTableRoot`, `AirHockeyArchGo`) are untouched, so the mini-game still uses the top-floor table;
   the six below are decorative showroom variants.
2. **Pattern band**: a closed, thin ring around the body (outer +0.012 m, inner −0.012 m, y 0.30…1.10 m,
   18 cm chamfered corners). The outer skin is the pattern; the inner skin and caps are dark metal. UV
   `u` runs **0…2** once around the perimeter and the texture is `TextureWrapMode.Mirror`, so the start
   and end both sample the left edge — the wrap is invisible and the image is mirrored exactly once.
   The rink top is untouched; the legs stay black. Evidence: `5,15 x 1,14 x 3,89 m` per body.
3. **Patterns**: `BabbdiCoop/patterns/sloppy_01…06.png` (the six 1536x1024 neon AI-slop images), copied by
   `build.ps1` (`Patterns: 6 PNG`). The six themes are sampled from the dominant hue of each image:
   `#FC03CF` magenta, `#FCA61C` orange, `#EDCC12` acid yellow, `#EBDB2E` gold, `#1473B2` blue,
   `#05E0FA` cyan; each has a secondary accent (cyan, purple, green, red, cyan, magenta).
4. **Arch paint**: the arch mesh is split by geometry, not by a blind y cut: the two thin support rods
   are the only triangles under 0.15 m wide in x; they get the accent, darkened. The rest of the canopy
   gets the dominant hue, its underside a deep tint of the same hue, and a thin emissive LED strip sits
   on the canopy's front and back edge (z ±0.445, y 2.80) in the accent colour. A matching point light
   under the canopy (1.25 / 8 m) lights the table and floor.
5. **Evidence**: `[PROP] arcade variant 1/6 … arcade variant 6/6`, then
   `[PROP] arcade showroom: 6 of 6 patterned tables on the annex floor below at y=96,15 m; rink tops
   untouched, legs black`. Map shots `district_146` (all six from the south-east corner),
   `district_147` (plan of the 3 x 2 grid), `district_148` (variant 1 close-up), `district_149`
   (variant 6), `district_150` (arch edge/lamp). No exceptions in the launch log.
6. **Not playable yet**: only the top-floor table is wired to `AirHockeyGame`; the six below are
   retextured showroom pieces. Wiring them into the mini-game is a separate step.

### v0.10.7: side-based scoring, full-puck goals, the 11/100 match rule, and a substitute on the north side

The user: «Нужно чтобы стол считал очки независимо от того кто с кем играет: не относительно того какой
игрок кому сколько забил, а основывался на том в какие ворота сколько шайб залетело, чтобы третий игрок
мог подменить, к примеру, первого уже с текущим счётом (партия идёт до 11 голов. Исключение если разрыв
по очкам меньше двух, тогда партия идёт до 100). Также забить гол слишком просто — сделай чтобы гол
засчитывался только если шайба полностью ушла в ворота, а не задела их центральный триггер.»

1. **The score belongs to the goal side, not to a player.** `scoreHost` is the red/south side's
   points (goals into the north net), `scoreClient` the teal/north side's points (goals into the south
   net). The old HUD read «ВЫ n : m ПАРТНЁР» and, worse, `DrainMini` zeroed both scores whenever a
   client joined. The HUD is now `КРАСНЫЕ n : m БИРЮЗОВЫЕ` + «СЧЁТ ПО ВОРОТАМ · ДО 11», and no join
   resets it.
2. **Substitution on the north side.** The host accepts a mallet from any connected client and the
   first one owns the north side (`northPeer` = 1 or 2). Another client at the table sees
   «МЕСТО ЗАНЯТО ДРУГИМ ИГРОКОМ — ЖДЁМ». When the owner presses E (a `MiniKindLeave` with its peer) or
   its mallets time out (1.2 s), `northPeer` returns to 0 and the waiting client's next mallet takes
   over. The side score, `matchTarget` and `matchOver` are kept; only the rally is centred. The
   substitute's avatar is posed at the table too: host peer 1 → the primary avatar, peer 2 → the
   extra one; a client spectator poses the other client's extra avatar from the north mallet carried
   in the state packet.
3. **A goal is the whole puck across the line.** The old check fired when the puck touched
   `x = RinkHalfX - PuckR` with `|y| < GoalHalf + PuckR·0.5`. Now the puck is allowed into the goal
   recess and scores only at `p.x - PuckR >= RinkHalfX` (and only if its whole body fits between the
   posts, `|y| <= GoalHalf - PuckR`). A puck overlapping a post still bounces off the end rail; a
   slow puck may sit straddling the line and the defender's mallet can just reach it (max centre
   **2.2764 m**). Verified in the solo run: `[MINI] goal in the north net -> 1:0, target 11` and the
   HUD `КРАСНЫЕ 1 : 0 БИРЮЗОВЫЕ` in `map/mini_test.png`.
4. **11, or 100 when the gap is 1.** First to **11** with a lead of **2+**. If someone reaches 11
   with the gap below 2 (e.g. 11:10), the target becomes **100** and the match continues until one
   side reaches 100; reaching 12:10 does *not* end it because the target is already 100. At the end
   `matchOver` freezes the puck at the centre and the HUD shows the winner; the host leaving and
   re-entering the table starts a new match.
5. **Protocol.** The air-hockey state packet is now **35 bytes** (was 22): the original body +
   active north peer, winner, target, matchOver, recipient peer id (the host sends one copy per
   client, byte 26 tells each client its own id), and the active north mallet x/z for spectators.
   Mallet and leave packets stay 22 B; the 31-byte pose stream is untouched. Both peers need
   v0.10.7 for the new scoring/substitution; an older peer still connects but does not get the new
   side logic.
6. **Evidence.** Build 0.10.7 loads, table and arch load, `[MINI] goal in the north net -> 1:0`,
   `map/mini_test.png` shows the new side scoreboard, `MiniTest` re-entry still works. The actual
   three-machine substitution is the user's test; the host logs `[MINI] peer N took the north side;
   side score kept at a:b` and `[MINI] peer N left the north side; side score kept at a:b`, and a
   spectator client also draws the other client's active north mallet as a third teal mallet. A host
   slot whose endpoint has been silent for 3 s is evicted, so a restarted client can rejoin.

### v0.10.6: **two clients get two remote avatars** — the host no longer averages their poses into one model

The user: «Протестировал третьим игроком и открыл новый баг. Для первого игрока, второй и третий игроки
как-бы делят одну модельку, которая дергается между местами где стоит второй и третий без остановки.
Чем они стоят дальше, тем дерганья больше.»

1. **The bug was structural**: `Net` had a single `remoteEP`, a single `incoming` queue and the plugin
   had one `remoteGo`. Both clients' poses went into the same stream, so the one avatar interpolated
   between A's and B's positions — the farther apart they stood, the bigger the jump.
2. **Net v0.10.6**: the host keeps up to **two client endpoints** (`clients`, ids 1 and 2; a third
   sender is ignored). The host broadcasts its own type-1 pose to **both** clients and forwards each
   client's pose to the other as a **type-3 relay** `[type=3][source][pos][rot][item][flags]` (its own
   source id). The incoming queue entries and `MiniPacket` carry a **peer** id: host=0, first
   client=1, second=2. The host's `SendOwn` broadcasts to both; the mini-game still talks to the
   primary endpoint only.
3. **Plugin v0.10.6**: the full smoothing/animator/item pipeline stays on the **primary peer**
   (host's first client / a client's host); a second remote peer is spawned as
   `COOP_RemotePlayer2` with its own `GlbAnimator` and a simple 0.12 s follower. The host's A is the
   original avatar, B is the extra; client A sees host + B, client B sees host + A. No pose stream is
   shared any more. The extra logs `[COOP] second remote player spawned (peer 2)`; it moves with the
   packet position/yaw and drives `SetMotion/SetState` (no held-item clone and no mini-game stance on
   the extra yet).
4. **Mini-game scope**: the table activity remains **2-player** — the host (peer 0) and the first
   client (peer 1); the third player is a spectator. The AirHockeyGame filters mini packets to its
   expected peer (`1` on the host, `0` on a client), so the second client cannot corrupt the rally.
5. **Protocol**: type 1 is unchanged (31 B); the host's relay is the new 32-byte type 3. Both clients
   need v0.10.6. The host accepts two clients; a fourth peer is ignored. The local solo/net-sim paths
   use peer 0 and are unchanged.
6. **Evidence**: the source separates `primaryPeer` (host 1 / client 0) and the extra; the log line
   above. The three-machine visual check is the user's test; this build removes the shared-model
   interpolation by construction.

### v0.10.5: puck +15 %, trigger ×2, and the **remote player visibly stands at the table** (stance + arm/body follow, synced to the partner)

The user: «Это решило проблему. Теперь увеличь шайбу на 15%. Увеличь зону триггера начала игры в 2
раза. Сделай так, чтобы со стороны выглядело что моделька игрока действительно стоит за столом и
двигает биту - синхронизуй это внешне для стороннего наблюдателя. При игре, моделька должна стоят
ногами по ширине плеч на чуть согнутых коленях, слегка сутуло. При движении игрока битой, его моделька
должна двигать не только рукой, но и слегка следовать за рукой корпусом.»

1. **Puck +15 %**: the collision radius is **0,1164375 m** (was 0,10125), the visual is **0,232875 m**
   dia × 0,0543375 m thick, `PuckLift` 0,029 m; the rink/goal/mallet bounds use the new radius.
2. **Trigger ×2**: `InteractRange` is **1,0 m** from either goal point (table ends x = ±2,576, z = 0);
   the debug teleport at 0,324 m still logs `near check = True`.
3. **The local body is placed at the table**: on start the FPC is moved to the table end
   (x = ±(2,576 + 0,45), z = 0) and faces the table, so the avatar itself is standing at the goal. The
   regular pose packets are still skipped during the mini-game; instead the partner's viewer builds the
   remote stance from the **mallet packets**.
4. **Remote stance sync**: the viewer's `AirHockeyGame` writes the static
   `RemoteMiniActive/Feet/Yaw/Hand` (the opposite table end; the body shifts along the end by
   `remoteMallet.y · 0,45`, clamped ±0,85; hand = the remote mallet world). `Plugin.AnimateRemote`
   overrides `remoteGo` to that feet/yaw, calls `SetMotion(0,0,0)`, clears the crouch/air/use/trick
   flags and calls `remoteAnimator.SetMiniPose(true, hand)`. `GlbAnimator` blends a mini layer on top
   of the idle pose: hips down 0,07 m, knees −18°/+36°, feet −18°, thighs splayed ±7° outward, spine
   hunched 14°+7°, head 5°, side bend and hip yaw toward the mallet, and the visually-right arm
   (`LeftArm/LeftForeArm/LeftHand`) is pointed at the hand target with `AlignLimb`. The body therefore
   follows the mallet across the table, not just the arm. `miniBlend` ramps at 6/s and back out on exit.
5. **External look**: the partner sees the avatar standing at its table end, feet apart, knees bent,
   slightly hunched, leaning and moving the arm with the mallet; the local player's own body is set to
   the same stance (not rendered locally). The mini-game camera is top-down; the body is at the end.
6. **Evidence**: the puck is visibly larger in `map/mini_test.png` / `mini_test_reentry.png`; the
   near check is true; the stance/arm code is in `GlbAnimator.SetMiniPose/ApplyMiniPose` and
   `Plugin.AnimateRemote`. The solo auto-test cannot show the remote avatar; the two-peer visual is the
   v0.10.5 addition and both peers need it.

### v0.10.4: a synced client puck (25 ms buffer at 60 Hz), a 35 % larger puck, and mallet–mallet collision

The user: «Протестировал кооп: шайба для второго игрока двигается с большим опозданием неверно
повторяя её настоящую траекторию. Также увеличь шайбу на 35% и добавь колизию битам между друг
другом.»

1. **Client puck**: the old client ran `puck = Lerp(puck, hostTarget, 1-exp(-dt/0.04))` on every
   30 Hz state, so a fast puck lagged by the smoothing **and** the interpolation chased a moving target
   — at a bounce it cut the corner and read as a wrong trajectory. The host now broadcasts state at
   **60 Hz** (`lastStateSend >= 0.016`), and the client keeps the last 32 state samples
   `(arrivalTime, pos, vel)` and renders **25 ms in the past**: linear interpolation between the two
   bracketing samples, and velocity extrapolation (max 50 ms) if the buffer runs out. The result is a
   bounded ~25 ms + network delay with the real path, including the bounce segments, not a curved
   chase. `remotePucks` is cleared on start/leave/host timeout.
2. **Puck +35 %**: visual diameter 0,15 → **0,2025 m** (thickness 0,035 → 0,04725), collision radius
   0,075 → **0,10125 m**; it sits at `PuckLift 0,026 m`, and the rink/goal/mallet bounds use the new
   radius.
3. **Mallet–mallet collision**: each peer resolves its own mallet against the other player's after the
   mouse move and clamp: if the centres are closer than **2 × 0,176 = 0,352 m**, the local mallet is
   pushed out along the normal and re-clamped to its allowed half/20 % intrusion. Both peers run the
   same rule, so the discs stop at contact instead of passing through each other; the host's puck
   collision still uses both mallet radii.
4. **Evidence**: the host state interval is 16 ms (`SendState`); the client's 25 ms buffer; the puck
   is visibly larger in `map/mini_test.png` / `mini_test_reentry.png`. The two-peer path is a code
   change on both ends (both need v0.10.4); the solo auto-test still shows the larger puck and the
   unchanged table/camera.

### v0.10.3: the E trigger is **at the table's goal**, not around the whole body

The user: «Не могу протестировать, триггер зона для начала игры стала слишком маленькой, если она вообще
есть. Она должна быть в точке ворот стола. Кнопка должна появлятся если игрок стоит в полуметре от
ворот или ближе.»

1. The v0.10.2 check asked for ≤0,5 m from the **body's perimeter** (and ≤0,6 m above the floor), which
   was not a place a player naturally aimed for. `NearTable()` now measures the player's feet to the
   **two goal points**: table-local **x = ±2,576 m (the table ends), z = 0**, and returns true when the
   XZ distance to **either** goal is ≤ **0,5 m** and the feet are within 1,5 m of the table floor. The
   prompt and the E start use the same test.
2. The debug teleport was moved to the host goal: **0,324 m** beyond the x = −2,576 end. The log is
   `[MINI] MiniTest: teleported to the table (near check = True)`; the game then starts normally.
3. Both goals are accepted, so either player can start from the end they naturally walk up to (the
   host's mallet still belongs to the south half and the client's to the north; the camera stays behind
   the assigned end).
4. The table, camera, mallet, texture and rink physics are unchanged from v0.10.2. `map/mini_test.png`
   / `mini_test_reentry.png` are re-shot from the goal position; `district_93`/`94` remain the idle
   table with its arch.

### v0.10.2: closer camera, a thinner/smaller mallet with a stick handle, 20 % opponent-half intrusion, and a 0,5 m table interaction radius

The user: «Поставь камеру на 15% ближе к столу. Измени модель биты: её плоский диск должен быть в 2
раза тоньше, а её ручка должна быть не шариком, а короткой палочкой со сглаженым концом (она сейчас
слишком массивная). Также уменьши модель биты на 45%. Ещё сделай чтобы игрок мог водить биту не
только по своей половине поля, но и заходить на 20% поля противника. Ещё уменьши радиус
взаимодействия со Столом, чтобы играть начать можно было только стоя в полуметре непосредственного
места за столом.»

1. **Camera 15 % closer**: the whole above-end offset is multiplied by **0,85** — from 4,0 m behind
   the local end / 5,0 m above the rink to **3,4 m / 4,25 m**, still FOV 55°. The table now fills more
   of the frame; the near rim is at the bottom, the far goal at the top.
2. **Mallet**: the flat disc is **0,352 m dia × 0,0495 m thick** (the old 0,18 m plate was too thick;
   it is 2× thinner before the 45 % reduction), the sphere knob is gone. The handle is a **capsule
   stick**: 0,08 m dia × 0,18 m tall with a rounded top (the bottom end is buried in the disc). The
   whole model is **45 % smaller** than v0.10.1 (0,55×): the collision radius is **0,176 m**. It still
   re-arms on re-entry (both `mini_test` shots show it).
3. **20 % into the opponent's half**: the mallet's x clamp now allows the host to reach
   **x = +0,432 m** (20 % of the 2,16 m opponent half) and the client **x = −0,432 m**; the far ends
   still stop at ±(2,16 − 0,176) = ±1,984 m; z stays ±(1,28 − 0,176) = ±1,104 m, inside the dark
   contour.
4. **0,5 m interaction radius**: `NearTable()` no longer uses the 6,5 m centre distance. It measures
   the player's feet to the table **body rectangle** (half 2,576 × 1,943 m) and requires the distance
   to the nearest edge **≤ 0,5 m** and the feet within 0,6 m of the table floor. The E prompt and the
   start both use it, so you must stand right at the table. The debug teleport sits 0,457 m from the
   side and logs `near check = True`.
5. **Evidence**: `[MINI] MiniTest: teleported to the table (near check = True)`; `[MINI] host joined
   … camera 3,4 m behind that end / 4,25 m up, FOV 55, rink 4,32 x 2,56 m`; `map/mini_test.png` and
   `map/mini_test_reentry.png` show the closer perspective, the smaller thin-disc/stick mallet and the
   puck inside the dark frame. The table/arch loads are unchanged (`[PROP] airhockey_table: 268 verts,
   5,15 x 1,14 x 3,89 m`; `[PROP] airhockey_arch: 146 verts, 3,31 x 1,77 x 3,76 m`).

### v0.10.1: mallet re-entry, 2× mallet, a smooth low-poly table, the puck inside the dark contour, and an angled camera

The user: «При повторном заходе в мини игру, моделька биты перестаёт быть видимой. Сделай чтобы она не
пропадала, увеличь её в 2 раза. Также перетекстурируй сам стол — сейчас он низкополигональный и
пиксельный, а должен быть просто низкополигональным — откровенные квадраты пикселей нам не нужны.
Ещё шайба должна перемещаться только внутри темного контура плоскости самого стола, а то сейчас в
логике игры не читаются его толстые бортики. И измени ракурс с "сверху", на "сверху под небольшим
углом", чтобы выглядело более реалистично.»

1. **The vanishing mallet**: `StopGame` hid `malletLocalGo`, but the next `BuildVisuals` only did
   `miniRoot.SetActive(true)` and returned — the parent was visible, the mallet itself stayed
   inactive. `BuildVisuals` now re-activates the local/remote mallet and the puck, and `UpdateVisuals`
   re-arms the local mallet every frame. The `[Debug] MiniTest` now enters, leaves, **enters a second
   time** and writes `mini_test_reentry.png`; the mallet is visible in both shots.
2. **2× mallet**: the visual disc is 0.64 m dia × 0.18 m tall plus a 0.54 × 0.40 m dome (was 0.32 /
   0.09 / 0.27 / 0.20); the collision radius is **0.32 m**. Its clamp keeps the whole 0.64 m disc
   inside the player's half and inside the dark contour (host x −1.84…−0.33, client +0.33…+1.84,
   z ±0.96).
3. **Smooth low-poly table**: the 128² palette atlas is upscaled to **1024² with Lanczos** (no blocky
   pixels) and the rink is redrawn at 4× as anti-aliased markings — a dark charcoal frame, four red
   face-off rings, the teal centre ring/bands and the red centre line, teal goal arcs, a bone field
   with a soft gradient and a few scuffs. The mesh stays low-poly; only the texture is smooth. The
   1024 atlas is embedded in **two** GLBs: `airhockey.glb` (body/legs/rink, 268 verts, 5,15 × 1,14 ×
   3,89 m) and `airhockey_arch.glb` (scoreboard arch, 146 verts, 3,31 × 1,77 × 3,76 m). The mini-game
   simply `SetActive(false)`s the arch — the old vertex-moving fold stretched the arch faces and
   produced a fan of shards in the angled view.
4. **The puck inside the dark contour**: the source rink face is **x ±5.0, z ±2.3**, scaled to **±2,30
   × ±1,3966 m**. The old physics (±2,36 × ±1,70) was **larger than the rink face**, so the puck and
   mallets could ride onto the thick rim. The physics is now **±2,16 × ±1,28 m** — the inner edge of
   the dark frame drawn in the new texture — with goals 0,84 m wide at x = ±2,16. The rink surface is
   **1,104 m** above the table feet (source y=3,0), not the rim top 1,1408; the mallets/puck sit on
   the real playfield.
5. **The camera** is no longer an orthographic plan: a **perspective** camera at 4,0 m behind the
   local player's end, **5,0 m above the rink, FOV 55°**, looking at the centre. The table recedes
   towards the far goal; the local half is at the bottom of the player's screen (the client camera is
   mirrored). The held item (the propeller is the usual one up here) is a child of the main camera;
   it appeared as a fan of huge blades in the top-down view, so `HideHeldItems` disables the in-hand
   props for the duration and restores them on exit.
6. **Evidence**: `[PROP] airhockey_table: 268 verts, 1 materials, size 5,15 x 1,14 x 3,89 m`;
   `[PROP] airhockey_arch: 146 verts, 1 materials, size 3,31 x 1,77 x 3,76 m`;
   `[PROBE] air-hockey table top: 113,69 m` (= 1,14 over the 112,55 m floor), `side shot ... 5,72 m`;
   `[MINI] host joined ... camera behind that end 5,0 m up, FOV 55, rink 4,32 x 2,56 m`;
   `[MINI] hid 1 held item(s)`. `map/mini_test.png` and `map/mini_test_reentry.png` (5120×1440) show
   the angled smooth table, the 2× red mallet on the rink and the puck inside the dark frame;
   `district_93`/`94` show the idle body + arch.

### v0.10.0: the table becomes a **two-player air-hockey activity** — E, top-down camera, mouse mallet, host-authoritative puck

The user: «Высоковат, сделай ниже на 20% и шире на 10%. Нужно сделай его не только декором — нужно
чтобы это была активность для двух игроков. Чтобы игрок управлял мышкой битой которую можно двигать
по столу. При начале миниигры, на букву "E" камера должна открепляться от модельки и переходить в
режим вида стола сверху».

1. **Size**: scale Y ×0,80 and Z ×1,10 on top of v0.9.99 → **5,15 × 2,91 × 3,89 m** (was 5,15 ×
   3,63 × 3,53); the rink surface is **1,1408 m** over the floor. The GLB is still 414 verts / 268
   tris, one material, double-sided. The **BoxCollider is the body only** (5,15 × 1,14 × 3,89) — the
   mesh bounds still include the arch, so the collider is set explicitly after the load. The flush
   ceiling light at 108,86 replaced the long pendant (its shade would have sat between the top-down
   camera and the rink).
2. **E / camera**: near the table (6,5 m XZ, &lt; 4 m vertically) press **E**. The new
   `AirHockeyGame.cs` disables `FirstPersonController`, detaches `fpc.playerCamera`, saves/restores
   its parent/local TRS + ortho/FOV/near/far, switches to an **orthographic top-down** camera at the
   rink + 4,9 m (size 3,15), unlocks/shows the cursor and disables the camera's `PostProcessLayer`
   (bloom/auto-exposure turned the pale rink into one white sheet). E again exits and restores
   everything. The camera's screen-up is the table's **+x** for the host and **−x** for the client,
   so each player's own half is at the bottom of their own screen.
3. **The game**: a table-local 2D rink **4,72 × 3,40 m**, goals 0,84 m wide at x = ±2,36; mallet
   radius 0,16 m, puck radius 0,075 m. The mouse ray is intersected with the rink plane and clamped
   to the player's half; the mallet follows at 40 m/s. The host simulates the puck (drag, wall
   bounces, mallet impulses, goal detection), resets the rally after a goal and is authoritative for
   the score. The client sends only its mallet and renders the host's puck/scores. The **central
   scoreboard arch of the source table is folded 8 m below the floor while playing** (the mesh
   vertices above the rink are translated; the body/rim/wheels stay) and restored on exit; the HUD
   shows the score instead. Mallets are a red disc+dome (host) and a teal one (client), matching the
   user's 96 mm / 60 mm reference scaled to the 5 m table; the puck is a dark 0,15 m disc.
4. **Network**: `Net` gains a separate **type 2** packet (22 bytes) with `kind` (1 = mallet,
   2 = host state, 3 = leave), player, x/z (and vx/vz for state), scores and flags; it is queued
   separately from the 31-byte pose packets, so older peers still connect and simply see no
   mini-game. The host sends state at 30 Hz; each peer sends its mallet at 30 Hz. The host resets
   scores/rally when the client's first mallet arrives and times the client out after 1,2 s; the
   client times the host out after 1,5 s. The idle peer drains the queue so it does not grow when the
   host practises alone.
5. **HUD/controls**: top-right score `ВЫ n : m ПАРТНЁР` (scaled with `Screen.height`, because the
   game runs at 5120×1440 here), a waiting line, status messages («ПАРТНЁР ЗА СТОЛОМ», «ГОЛ!»,
   «ПАРТНЁР ВЫШЕЛ»), bottom center «E — выйти из-за стола»; near the idle table «E — аэрохоккей».
   Solo (`NetRole.None`) works as host practice; the client waits for the host.
6. **Evidence**: `[PROP] airhockey_table: 414 verts, 1 materials, size 5,15 x 2,91 x 3,89 m`;
   probes `air-hockey table top: 113,69 m (airhockey_table)` (= 1,14 over the 112,55 floor), `side
   shot ... 5,72 m`; `[MINI] host joined ... camera top-down 3,2 m, rink 4,72 x 3,40 m`; the debug
   auto-test (`[Debug] MiniTest`) teleported, entered, captured `map/mini_test.png` (5120×1440, the
   top-down rink with the mallet/puck/HUD) and left. The idle table shots `district_93`/`94` still
   show the prop.
7. **Not** a physics-heavy table: no Unity colliders on the mallets/puck (custom 2D), no puck
   prediction on the client, and the physical scoreboard arch is hidden only while playing. The host
   is authoritative; both peers need v0.10.0 for the mini-game.

### v0.9.99: the air-hockey table is **2× bigger and 20 % wider**

The user: «Сделай его в 2 раза больше и на 20% шире».

1. **GLB**: scale 0.23 → **0.46**, and the source's width axis (Z) multiplied by **1.20** — **5,15 ×
   3,63 × 3,53 m** (was 2,58 × 1,82 × 1,47). The width is baked into the vertices, and the normals
   go through the inverse-transpose (`nz / 1,20`, renormalised), so the lighting is unchanged. Still
   **414 verts / 268 tris**, one material, one BoxCollider, wheels at y = 0.
2. **The room fits**: the annex hall is 40 × 34 m with a 6,4 m ceiling; the scoreboard top is 3,63 m
   over the floor (house-local 106,18). The pendant was raised to 107,65 (rod 1,1 m, 1,5 m shade,
   1,2 m lens, 2,0 / 22 m light), so it hangs 1,47 m above the arch instead of through it.
3. **Probes**: `[PROP] airhockey_table: 414 verts, 1 materials, size 5,15 x 3,63 x 3,53 m`;
   `[PROBE] air-hockey table top: 116,18 m` (= 3,63 over the 112,55 m annex floor), `floor beside
   it: 112,55 m`, `side shot from 8,3 m: 5,72 m` (the face is now 2,576 m from the centre). The map
   cameras were raised: `district_93` broadside from house-local (-46, 106,4, -8) looking at
   (-37,75, 104,4, 3); `district_94` the west-window view from (-60, 106,6, 3).
4. **Same look**, twice as monumental: the bone rink, oxide-red body, steel-teal lines, amber
   scoreboard, double-sided geometry and the decorative (non-playable) nature are unchanged.

### v0.9.95: cargo hatch **open** — the frame follows the hull's contour, the leaves are the cut skin

The user: «Погоди, а если подогнать раму под контур корпуса?» and then «доделывай — мне нужен вид с уже
открытыми створками».

1. **The frame follows the contour**: a 0.16 m lip runs all round the aperture (front and rear rim bands
   along the section, plus the two side rims), so there is no square frame on a round hull.
2. **The leaves are the cut-out skin**: the aperture (z 21,0-24,6, θ 198-342°) is real, and the two
   leaves are generated from the same surface grid (offset 12 mm) — closed they are flush.
3. **Hinges are vertical at the aft corners** (`rp()` rotates about the line x = hull edge, z = z1), and
   `RAMP_DEG = 120` swings them out and aft like the МЧС photo. The leaves are double-sided.
4. Rails on the floor as before.
5. **Evidence (checked before shipping):** `mi26_rear_34.png` — the machine on the dam with the leaf
   standing out behind the fuselage; `mi26_side.png` — the opened bay with the leaf swung aft; both show
   the cargo bay's interior through the opening.
6. Still to do: windows in red frames on the leaves, locks, rollers on the rails.

### v0.9.92: the cargo hatch re-hung on an **aft frame at z = 21** — the leaves are the aft skin

Approved plan («Давай попробуем по твоему. Только не делай вертолет ощутимо меньше»): the cargo bay ends
at **z = 21,0** with a flat frame, the tail cone continues above it, and the **two clamshell leaves are
the aft skin itself** — so closed they are flush with the hull (no hole anywhere else), open they swing
outward about their fore-aft hinge lines, like the МЧС photo.

1. Aperture `RAMP_Z 21,0-23,8`, `RAMP_TH 200-340°`: the aft/lower skin of the cabin's end. `in_hole()` cuts
   it, and the leaf patches are generated from the *same* surface grid, offset 15 mm outward, so the pair
   closes the aperture exactly.
2. Each leaf is hinged along its outboard edge (the θ = 200° / 340° line, running fore-aft) and rotated by
   `RAMP_DEG` (62° in this build) about that line; hinge fairing boxes run along both edges.
3. Two sliding rails (0,30 x 0,12 x 5,0 m, cross-ties) on the floor, projecting aft of the sill.
4. **Nothing got smaller:** the model is still **53,52 x 16,00 x 63,16 m** — the tail cone keeps its length;
   the cabin simply ends in a frame now instead of a long taper.
5. Still to do (next pass): the frame's rounded-rectangle lip, portholes in the leaves, locks and rollers.

### v0.9.89: the cargo aperture moved to the **aft end** (where the reference shows it), the belly closed, and the machine **centred on the crest**

The user: «Я же специально тебе референсы дал люка, зачем ты сделал это окошко нелепое снизу? … он на
треть в воздухе над водой висит … обвел красным примерное место люка, а своё отверстие заделай обратно».

1. **The belly slot is gone.** The aperture is now the **whole aft end of the cargo cabin** under the
   boom's root — `RAMP_Z 21,6-24,6`, `RAMP_TH 205-335°` (belly and both sides up to just above the
   waterline), i.e. exactly the place the user outlined on the side view. Nothing is cut in the belly
   forward of it; the skin there is continuous again.
2. **Two clamshell leaves** hinged on the aperture's outboard vertical edges (`RAMP_DEG`, 0 = closed,
   78 = open; the test build ships open) and **two sliding rails** (0,30 x 0,12 x 5,2 m with cross-ties)
   along the floor, projecting ~2 m aft.
3. **Placement is measured, not guessed.** `Helicopter()` now raycasts across the crest at the spot,
   finds its width (**12,0 m** on the dam), **centres the local z on it** and drops the wheels onto the
   hit point: log `crest width measured 12,0 m … wheels at y=85,51 m (crest 85,51 world)`, local
   `-70,0,-14,0`. Previously the fixed offset (-8) left the machine hanging over the water.
4. Still open items: the leaves are flat plates (no curvature, no portholes of the reference), and the
   rails have no rollers.

### v0.9.88: the Mi-26 moves **onto the dam**, gets **+25 % width / +15 % height**, a cockpit bulkhead and a two-leaf cargo hatch

The user: «сделай ми-26 на 25% шире и на 15% выше. Раздели переборкой с дверным проемом кабину от
грузового отсека. Убедись что вертолет изнутри не прозрачный. Сделай чтобы грузовой люк открывался и
закрывался свободно (для теста оставь его в открытом положении) — вертолёту становится тесно на этой
крыше, давай переместим его на саму дамбу, метрах в 50 от района Сталин».

1. **+25 % width, +15 % height** (`HW` x1,25 and `H = 9,77 x 1,15` in `build_heli_v7b.py`): the body is
   6,0 m wide at build scale (5,0 m life) and 5,30 m tall over the belly; the engine deck and the rotor
   were raised by the roof's delta (`BY 3,95 -> 4,64`) so they still sit on the hull, the tail rotor
   went up with the fin (x1,15). Model **53,52 x 16,00 x 63,16 m**, 110 565 verts / 57 275 tris.
2. **Cockpit separated from the cargo bay** by a real bulkhead at z = 5,0 with a **1,04 x 1,90 m
   doorway** (jambs + head), built as horizontal strips that follow the hull's section
   (`bulkhead()` in `hull_shell.py`). A second bulkhead at z = 21,8 (doorway 1,9 x 1,95) separates the
   cabin from the ramp bay.
3. **Not see-through from inside**: the portholes are now **staggered** — six on starboard at z 10,6 …
   21,6 and five on port at 11,7 … 20,5 (`PORTH_Z_PORT`) — so no line of sight passes straight through
   the cabin, and the two bulkheads close the fore-aft axis. The hull is still one double-sided shell,
   so its inside is solid from every angle.
4. **Cargo hatch (per the references): two clamshell leaves + two sliding rails.** The rear skin is cut
   (z 22,0-25,0, 44° of the belly) and two leaves are hinged on the outboard edges; at `RAMP_DEG = 78`
   they swing aft along the fuselage exactly like the photos, at 0 they close the aperture. Two rails
   (0,30 x 0,12 x 5,2 m with cross-ties) run along the floor and project ~2 m aft of the sill — the
   loading tracks of the references. `RAMP_DEG` is a one-line knob in `hull_shell.py`; the shipped build
   has the hatch **open**.
5. **Moved to the dam**: `HeliSpot = (-70, 0,35, -8)` in the district's local frame — on the crest,
   **50 m west** of the district, nose along the dam (`HeliYaw = -90`). The log confirms the stance:
   `[HELI] Mi-26 on the dam's crest, 50 m west of the district (local -70,0,-8,0): wheels at y=85,85 m
   (crest 85,85 world)`. All preview cameras moved with it.
6. **Files:** `BabbdiCoop/model/mi26.glb` (5,54 MB) -> plugin. Shots: `mi26_on_dam.png`,
   `mi26_side.png`, `mi26_front.png`, `mi26_ortho_side.png`. Known: the clamshell leaves are a first
   pass (flat plates) — they read as open doors but not yet as the curved shells of the reference.

### v0.9.86: measured against the **real Mi-26** (two videos): the body was 44 % too wide and the cabin 18 % too short — corrected, cabin fitted out

The user supplied two videos (`E:\858\ASSET\mi26`): a walk-through of the cargo bay through the port side
door, and a news report of a Mi-26 landing in a snowy village, and asked how much the model must grow,
and for the cargo bay's fit-out at human scale.

1. **What the videos measure** (a person 1,75 m; the bay is 3,1 m high, 3,25 m wide in the real machine):
   the walk-through shows the ceiling at ~1,8 person-heights, the floor 3,2 m wide, the portholes
   Ø 0,42-0,45 m, the frames every 0,5-0,6 m, the floor 1,2 m above the ground, the port door
   1,5 x 0,9 m with a five-step folding ladder.
2. **What the model was:** 5,6 x 3,06 m at build scale = **4,67 m wide against a 2,55 m cabin height**
   (life size) — a low, wide box, which is exactly the "Mi-8" read: the Mi-8's cabin is 2,3 x 1,8.
   The reference fuselage is 4,0 m wide and the cabin nearly square (3,25 x 3,1).
3. **So the enlargement is 0 %:** the model is already **1,58x life size** (63,2 m against 40,0 m), and
   by the video's human scale its cabin was already 1,35x. The fix was the *proportions*, not the size:
   - **width 5,6 -> 4,8 m** at build scale (`HW` x0,857 in `build_heli_v7b.py`), i.e. 4,0 m life size,
     the same as the source model's fuselage and the drawing's front view;
   - **cabin floor 1,55 -> 0,70 m** above the belly, so the cabin is **3,91 x 3,86 m at build scale =
     3,26 x 3,22 m life size** — the video's nearly square hall instead of a shallow one;
   - **portholes Ø 1,18 -> 0,70 m** (0,42 x 1,68), six per side on a 2,2 m pitch; the cockpit glazing
     went from three 2,3 m windows to **two 1,4 m ones** per side.
4. **Cargo bay fit-out** (`hull_shell.py`, group `cabin_fit_out`): **26 barrel-vault frames** (0,07 x
   0,055 m ribs every 0,62 m), **12 folded side seats**, two longitudinal floor tie-down rails plus 10
   transverse ties, the ceiling spine with two pipes and six lamps, handrails at the waist, a **front
   bulkhead with a doorway** and a rear bulkhead, and the **port side door with a five-step ladder**
   (the video's entry) — cut through the shell as a real opening with a 0,14 m reveal.
5. **Model:** still **53,52 x 13,97 x 63,17 m** (4,80 x 13,97: the width is the rotor disc), 108 677
   verts, 18 materials, 54 885 tris, 5,45 MB.
6. **Shots:** `district_120` (the glazing and portholes up close — the cabin's frames read through
   them), `district_67` (cabin side), `district_123` (orthographic side), `district_65` (the machine on
   the roof). Still the simple 5-blade tail rotor; the ramp is not cut yet.

### v0.9.85: the Mi-26 is **40 % bigger**, **hollow** (real window and porthole openings) and its main axle is 2 m aft

The user: «Передвинь основную ось на 2 метра назад, увеличь вертолет на 40% и сделай ему пространство
внутри с отверстиями на месте окон и элюминаторов (а то что сейчас в роли стекла удали)».

1. **Main axle 15,36 -> 17,36 m** behind the nose, i.e. 37,1 % -> **41,9 %** of the 41,4 m hull (in the
   final, +40 % machine that is 24,3 m; the 2 m are in the pre-enlargement units). The gear itself is the
   same original assembly, still standing on the roof.
2. **+40 % on the whole machine** (`SCALE = 1.4` in `build_heli_v8.py`, applied to every group after the
   pitch): **53,52 x 13,97 x 63,17 m**, rotor disc 53,5 m, track 8,2 -> 11,5 m, wheels 1,34 -> 1,87 m,
   belly clearance 0,95 -> 1,33 m. 79 422 verts / 17 materials / 43 935 tris (4,38 MB GLB).
3. **The hull is a hollow shell now** (`hull_shell.py`): the same purple-outline loft, closed at the nose
   and the tail, with **real openings** cut on a 170 x 52 (z, θ) grid —
   - **three cockpit windows per side** (`z 2,2-3,9 / 4,1-5,8 / 6,0-7,7`, ring θ 15-42°), each with a
     0,14 m reveal;
   - **five round portholes per side** (`z 11,2 / 13,6 / 16,0 / 18,4 / 20,8`, r 0,42, centre 8° above
     the waterline) — the rectangular grid cell block around each is covered by a **circular annulus**
     (smooth, analytic normals, so the patch is invisible) and a 0,14 m cylindrical reveal;
   - the fake glass quads of v0.9.81-84 are **gone**.
   The inside is drawn because the GLB materials are `doubleSided` and `GlbFigureBuilder` now sets
   `_Cull 0` for them (`GlbModelLoader` parses `doubleSided` into its material tuple). Inside there is a
   **cabin floor** at 1,55 m and two bulkheads (z 3,6 and 23,6), so the openings read as a real
   compartment: through a window you see the interior — and, straight across, the opposite opening.
4. **Evidence:** `[HELI] heli_mi26: 79422 verts, 17 materials, size 53,52 x 13,97 x 63,17 m` and the log
   tail `…hollow hull from the user's purple outline (side profile exact, +40 %, real window/porthole
   openings, cabin space inside) + the ORIGINAL engine deck … main axle 17,36 m and nose axle 6,50 m`.
   Shots: `district_67` (the portholes up close — round holes with the dark cabin behind them),
   `district_120` (front 3/4: the openings, the gear on the roof), `district_123` (orthographic side),
   `district_65` (the whole machine over the district), `mi26_window_frame.png` (wireframe proof that
   each porthole is a full circle in a rectangular patch).
5. Still the simple 5-blade tail rotor; the hull's nose height and fin outline are still the hand
   contour's.

### v0.9.84: the Mi-26 **stands on the ORIGINAL landing gear**, belly 0,95 m off the roof, 1,5° nose-up

The user: «Вертолет почти лежит на земле — отрегулируй высоту (+вертолет должен быть под небольшим углом,
слегка смотреть вверх), сделай шасси и крепления к ним в тех же пропорционально местах как на чертеже
референсе и используй колесики из оригинальной модели, а не те что ты сделал с нуля».

1. **The gear is the source model's own** (`MI26_unz.wrl`): the two main units — a **two-wheel bogie on
   each side** with the original trailing-arm frame (`Cylinder31…52`, 6 800 tris) — and the **twin nose
   wheel** (`Torus02/09`, `Cylinder53/54/55`, `Box02`), tyres/hubs on the source's own `SHINa` atlas.
   The homemade cylinders/boxes of v0.9.81 are gone. 26 421 tris / 17 materials / 67 165 verts.
2. **Stations from the reference drawing** (nose -> fin trailing edge 41,4 m): the **nose axle 6,50 m**
   behind the nose (15,8 % of the length) and the **main axle 15,36 m** (37,1 %), read off the 3-view;
   the source's own gear is at 14,5 % / 43 %, so it is *placed* by its axle, not scaled. The **track is
   8,2 m** (the user's earlier choice — the reference's 8 150 x 1,20 = 9,78 was called «ужасно широко»),
   the source's own 6,84 m widened per side without stretching the wheels.
3. **Height**: the hull is lifted **0,95 m** so the belly clears the roof (the source's own belly
   clearance 0,89 x 1,20 = 1,07 was tried first and left the nose strut 4 cm short of the belly — the
   running gear is exactly as the original built it, so the hull came down to it instead). The wheels
   touch: `wheels at y=261,94 m (roof plate 261,91)`, i.e. 3 cm.
4. **Attitude**: the whole hull, engine, rotor and tail are **pitched 1,5° nose-up about the main axle**
   (`pitch()` in `build_heli_v8.py`) — the nose rises 0,40 m, the tail sinks 0,78 m; the gear itself stays
   vertical and on the ground, exactly like an aircraft resting on its main wheels.
5. **Model:** **38,23 x 9,91 x 45,12 m** (was 38,23 x 9,64 x 45,10). Log: `[HELI] heli_mi26: 67165 verts,
   17 materials, size 38,23 x 9,91 x 45,12 m` and the tail of the `[HELI]` line now names the gear and
   the stance (`belly 0,95 m off the roof; 1,5 deg nose-up; track 8,2 m; main axle 15,36 m and nose axle
   6,50 m behind the nose`).
6. **Shots:** `district_120` (front 3/4: both gear units standing on the roof), `district_123` (the
   orthographic side elevation — the whole stance; the roof's parapet hides the tyre bottoms from
   outside), `district_65` (whole machine), `district_67` (engine side). Still the simple 5-blade tail
   rotor — the original's tail rotor is the next piece.

### v0.9.83: the Mi-26's **tail was 7 m too long** — the boom is squeezed onto the real stations, plus an **orthographic** side shot

The user pasted the in-game model over the dimensioned 3-view: «расхождения огромны. Почему это вышло?»
Two separate causes, measured:

1. **The overlay itself was invalid.** The game screenshot is a *perspective* shot taken a few metres
   from the nose (the machine fills the frame), so the near end is magnified by roughly **1,5x** and the
   tail shrinks; pasting that on an orthographic drawing produces a big apparent error on its own. The
   only fair comparison is an orthographic elevation — `district_123` is now exactly that
   (`Ortho = 24`, from the side, 26 m out), and it is the shot to judge the profile by.
2. **There was a real error, and it was mine.** The hull's length was set to **48,03 m** — the
   *overall* reference length 40,025 x 1,20 — but 40,025 m is the length **with the rotors turning**
   (blade tip to tail-rotor tip), while the purple contour is only the **fuselage + fin**. The reference's
   nose -> fin trailing edge is **34,5 m = 41,4 m** after x1,20, so the hull was **7 m (17 %) too long**
   and the whole tail (boom, fin, tail rotor) was stretched with it. The front half was fine: the deck's
   front sits at **11,06 m** against the reference's 8950 x 1,20 = **10,74 m**, its rear at 23,66 vs 23,4.
3. **The fix is a piecewise z-remap of the contour** (`build_heli_v7b.py`, `remap()`): the front half
   (0-23,66 m) is untouched, the boom keeps its taper compressed onto 23,66-37,2 m, and the fin's chord
   is squeezed from 8,4 to **4,2 m**, ending exactly at **41,4 m**. The tail-rotor axis moved from 45,40
   to **40,47 m** (33727 x 1,20). The rotors/engine/gear are placed in nose-origin metres, so they did
   not need re-tuning; `Z0` in `build_heli_v8.py` is now derived from the hull's own nose, so the model
   stays centred on the roof.
4. **Result:** model **38,23 x 9,64 x 45,10 m** (hull 41,4 m; the reference's nose->tail-rotor tip is
   45,03 m). The engine deck, its seam on the roof and the whole turbine assembly are unchanged —
   verified in `district_67`/`120`/`122`.
5. **Evidence shots:** `district_123` (orthographic side elevation — the profile compared honestly),
   `district_65` (whole machine on the roof), `district_67` (engine side), `district_120`/`122` (deck),
   and `mi26_vs_reference.png`, which draws the reference's stations (red) over the model's silhouette
   (blue): nose 0 / deck 10,74 / deck rear 23,4 / tail rotor 40,47 / fin TE 41,4.
6. **What is still the hand contour's, not the drawing's:** the nose tip sits low (the reference's nose
   is at mid-height), the cabin roof's front slope and the fin's swept outline. Say the word and those
   three curves are redrawn from the 3-view too.

### v0.9.82: the Mi-26's **engine deck, intakes and main rotor are the ORIGINAL model's**, adapted onto the new hull

The user on v0.9.81: «Фюзеляж в целом норм, но работы ещё много. Давай по кусочкам — моторный отсек и
турбины не верны. Не придумывай своё, а адаптируй готовое — перенеси из оригинальной модели мотор и
турбины, лишь растянув и подправив их для бесшовности под пропорции и габариты фюзеляжа».

1. **The parts are the source model's own geometry**, extracted from `MI26_unz.wrl` with their
   textures/UVs and **flat-shaded** (one normal per triangle, like the original low-poly look):
   `DV02` (the engine cowling with its side grilles, `DV_LEV.jpg`), `Sphere01/02` + `Object01/02`
   (the two round intake drums and their lips), `Tube01/02` (the exhausts), `Object05` (the oil-cooler
   intake) and `Object06` (the gearbox fairing, `RED_GL_VINT.jpg`) — 6 649 triangles, plus the
   **whole 8-blade main rotor** (`Cylinder57…64` + the hub `Cylinder14/15/17/19/21/23/25/27/29/30`,
   `Box01`, `Line01`, `GL_VINT.jpg`, 9 000+ triangles). The temporary deck boxes, intake cylinders and
   the simple 8-blade rotor of v0.9.81 are gone. Model total **15 345 tris / 11 materials**.
2. **The adaptation is a transform, not a redesign:** **x/z × 1,20** (the reference rule), **y × 1,00**
   (the source model is 13,0 m tall against the reference 8,15 m, so a ×1,20 y would put the deck's
   top at 8,3 m and the rotor above the fin), **z + 21,0 m** behind the nose (the cowling's front lands
   on the drawing's intake station, its tail on the drawing's fairing end) and the **base sunk 0,20 m
   into the roof** so the cowling's lower edge is buried along its whole length — no gap anywhere.
   Deck top **7,20 m**, rotor plane **7,85 m** (the drawing: 7,3 / 8,25), rotor disc **38,23 m**
   (reference 38,40).
3. **The hull's cross-section had to flatten for the seam.** The v0.9.81 superellipse (p = 0,72) left
   the roof 0,5-0,7 m below the cowling's side edges, so the deck floated on a shadow. The section now
   uses a **height-dependent exponent** — boxy at the top (p 0,45: a flat roof), round at the belly and
   the boom (p 0,95) — so the roof is flat where the deck sits and the boom stays a round tube. The
   **side profile is untouched** (it still comes from the purple contour pixel by pixel); only the
   cross-section changed.
4. **The cockpit glazing was rebuilt** as a grid of small surface patches on that section (three
   windows per side at nose-origin z 2,2-3,9 / 4,1-5,8 / 6,0-7,7, offset 3 cm along the surface
   normal, 0,10 so the 20-segment facets cannot poke through them) — the old flat quads stuck out of
   the roof at the front.
5. **Evidence:** `[HELI] heli_mi26: 33937 verts, 11 materials, size 38,23 x 9,73 x 50,03 m` and the
   new log tail `…hull built from the user's purple outline (exact side profile) + the ORIGINAL engine
   deck, its two intakes, the gearbox fairing and the 8-blade main rotor from the source model, adapted
   onto it (x/z 1,20, y 1,00, base sunk 0,2 m into the roof)`. Shots: **`district_120`** (front 3/4 of
   the deck: cowling, both intake drums, gearbox fairing), **`district_122`** (top: the deck's plan on
   the flat roof), `district_67` (side: the grilles and the fairing), plus the whole machine
   `district_65`/`66`/`68`. `mi26_engine_overlay.png` puts the transformed deck (blue) on the user's
   own drawing: its front, top and tail land on the drawn engine.
6. **Files:** `BabbdiCoop/model/mi26.glb` (heli_v8.glb, 2,11 MB) → plugin; pipeline
   `build_heli_v8.py` (hull + glazing + source parts + gear) over `eng_extract.py` (WRL parts,
   transform) and `glbwrite.py` (multi-material GLB), `build_heli_v7b.py` (silhouette hull). Still
   placeholders: the **main/front gear** (simple cylinders, 8,2 m track) and the **tail rotor**
   (simple 5-blade) — the next pieces.

### v0.9.81: the Mi-26 hull is built **from the user's purple outline** (side profile exact)

The user rejected v0.9.80 («вытянул ещё, налепил конус, шасси ужасно широко») and supplied a clean
Mi-26 line drawing with a **closed purple outline around the fuselage + tail boom + fin only**
(the engine deck/turbines, landing gear and rotors were explicitly outside it).

1. **The outline is the model.** The purple loop is extracted by colour (`R,B > 90, G < 125`),
   closed morphologically and filled; for every pixel column its **top and bottom** are read, so
   the new hull's side silhouette is the purple outline by construction. The overlay
   (`v7_full_overlay.png`, red hull over the purple line) matches it along the nose, cabin,
   boom, fin and belly — no hand-tuned top/bottom curves any more.
2. **Hull:** a superellipse loft over 240 stations (~4 840 tris, smooth normals), width taper
   0,2 → 2,8 → 0,2 m (fin is a thin plate merged into the boom). The surface uses the **gray
   FUZEL atlas** (the panel lines, doors and hatches of the original model) mapped along
   z/y; the cockpit windows are dark quads, not red squares.
3. **Separate parts** (outside the outline, as the user said): the engine deck/pylon boxes,
   two round intakes, the 8-blade main rotor (38,4 m disc) at z≈17,8 m, the 5-blade tail rotor
   (9,12 m) at the fin, and the main/front gear with an **8,2 m track** — narrower than the old
   9,78 m, because the user called the old track «ужасно широко». Total **5 472 tris / 3
   materials** (panel, dark, glass).
4. **Dimensions:** model **35,7 × 9,79 × 50,0 m** (width is the rotor planform; hull length
   48,43 m; disc 38,4 m). Placement unchanged: 47,2 × 21,9 m block, 30 floors, wheels at
   **261,94 m** vs roof **261,91 m**.
5. **Evidence:** `[HELI] heli_mi26: 13626 verts, 3 materials, size 35,69 x 9,79 x 50,00 m` and
   the overlay above. Shots `district_65` (whole machine), `district_66` (top 3/4), `district_67`
   (side close-up: the FUZEL panel lines/doors) and `district_68` (front 3/4: rounded nose,
   cockpit glazing, intakes, gear).
6. **Files:** `BabbdiCoop/model/mi26.glb` (heli_v7.glb, 0,53 MB) → plugin; pipeline
   `build_heli_v7b.py` (silhouette hull) + `build_heli_v7_full.py` (parts/GLB). The v0.9.80
   profile-warped model and its 1,3×/cone/wide-gear look are gone.

### v0.9.80: the Mi-26 **reshaped to the reference drawing** and to reference ×1,20 dimensions

The user's third round: the original free model's proportions were wrong («менее остроносая
передняя часть и прочие пропорции не совсем верны»), with a side/front/top drawing
(40 025 length, 33 727 nose→tail rotor, 8 145/8 050 height, 32 000 rotor, 7 600 tail rotor,
8 150 track, 8 950 nose section) and the rule **every reference dimension ×1,20**. The mesh is
still the original detailed one (31 220 tris, authored normals/UVs, grey 512 px textures,
livery removed); only its **shape** is warped to the drawing.

1. **The reference targets after +20 %:** length **48,03 m**, rotor **38,40 m**, height
   **9,774 m**, wheel track **9,78 m**, tail rotor **9,12 m**, nose section **10,74 m**. The
   placed GLB measures **38,42 × 9,77 × 48,12 m** (width = rotor; length includes the forward
   blade tips), i.e. the reference dimensions with the +20 % pass baked in. `HeliScale` in
   `Helicopter()` is **1,00** — no extra game-side scale.
2. **What was wrong in the source:** height **13,04 m** (the fin/tail rotor was ~4,9 m too tall),
   wheel track **5,74 m**, the nose a long needle, the tail boom too short and the main rotor
   too far aft. The fix is a set of explicit transforms in `build_heli_v3.py`:
   - global **(X 1,206, Y 0,750, Z 1,186)** — X/Z hit rotor/length, Y removes the excessive
     height without squashing the cabin (the fuselage ends ~5,5 m tall);
   - **main rotor** moved **2,50 m forward** to the reference station and its mast stretched
     vertically (×1,68 about the fairing), so the hub reaches the 9,77 m height instead of the
     tail being the highest point;
   - **tail rotor** rebuilt as a rigid group at the new lower centre, **9,12 m** diameter
     (the source's 7,5 m disc, +20 %);
   - **landing gear/wheels** widened by **×1,414** to the 9,78 m track; tyres stay circular;
   - the **needle nose is cut off** at the radome station and replaced by a **rounded
     half-ellipsoid radome** (18 segments, same FUZEL texture) — no spike, no seam;
   - **tail boom** lengthened relative to the cabin by the same Z map.
3. **Detail/text pass is unchanged** from v0.9.79: original geometry, authored per-vertex
   normals, **31 220 tris / 21 298 verts / 16 materials / 9 embedded textures**, grey palette,
   512 px softened textures, the two livery titles and `RA-06078` inpainted, faint abstract
   glyphs in their place. No readable lettering.
4. **Placement unchanged:** the 47,2 × 21,9 m, 30-floor block at 169,5,226,0 (top 176,15 m,
   world roof 261,91 m), vent box skipped. Wheels at **261,94 m**; the 49,7 m model now
   overhangs the roof ends by ~1,25 m each and the 38,4 m rotor by ~8,25 m each side.
5. **Evidence:** `[HELI] heli_mi26: 21298 verts, 16 materials, size 38,42 x 9,77 x 48,12 m`
   and `[HELI] Mi-26 on the Stalin block … model 38,4 x 9,8 x 48,1 m (reference 40,0 x 32,0 x
   8,15 m ×1,20 = 48,0 x 38,4 x 9,77; reshaped nose/tail/rotor, 1,00x) …`. Shots `district_65`
   (whole machine, seaward side), `district_66` (3/4 from above) and `district_68` (front 3/4:
   the rounded radome, cockpit glazing, intakes, wide gear).
6. **Files:** `BabbdiCoop/model/mi26.glb` (2,28 MB) → `BepInEx/plugins/BabbdiCoop/mi26.glb`;
   pipeline `E:\858\work\mi26\build_heli_v3.py` (reference warp) over `wrl.py`,
   `prep_textures.py`, `build_heli_v2.py`. `heli_v3.glb` is the shipped file; the old
   v0.9.77 origami pass and the 1,30× game-side scale are gone. Purely local, not synced.

### v0.9.76: **furniture, atmosphere and the civic centre removed**, and every Stalin house is **one tone**

The user: «Убери всё что сделал выше касательно "7. Атмосфера района" и "3. Мебель в случайных
комнатах", вырежи также "белый общественный центр над водой, в стороне от района Сталин" полностью.
Также сейчас несколько домов Сталин светлее остальных, они должны быть одно тона.»

1. **Furniture is gone.** `Furnish()`/`FurnishRoom()` and the whole room-set system (bed, wardrobe,
   sofa, TV, kitchen, shelf, rugs, books, crates, the 384x192 palette atlas, `[FURN]` logs, the
   `furnCams/furnLooks` room shots and the `[District] Furniture` config) were removed. The rooms are
   empty again. The House's two dark-red banners were the only thing still using that atlas: they now
   have their own **`CoopBanner`** 128x128 dark-red woven fabric (with a `[HOUSE] banner material`
   line) and a separate **white plaster emblem** mesh, so no furniture code remains.
2. **The district atmosphere is gone.** `Atmosphere()` and every prop it built — 32 street lamps, 9
   benches, 9 bins, 8 bollards, 10 planters/trees, 9 cars, 8 laundry lines, 10 antennas, the `[ATM]`
   line and the `StreetLamp/Bench/Bin/Bollard/Tree/Car/Laundry/Antenna` helpers — were removed. The
   streets are plain again.
3. **The white civic centre over the sea is gone entirely.** `CivicCenter()`, its 64x78 pier, white
   slab, stepped glass pyramid, A-frame, wing with the three rounded balconies, glazed entrance, lawn,
   cars/taxi, trees, lamps, `Beam/BigTree/CivicGlassMaterial`, the `CoopCivicGlass` material and the
   `[CIVIC]` lines and shots were cut. The sea east of the district is empty again.
4. **One tone for every Stalin house.** The lighter blocks were the khrushchevkas wearing the
   `M_ConcretePanels00` clone (`CoopPanelFacade`, v0.9.70). That mix is removed: `mPanelFacade` and the
   `type == 0` branch are gone, **every block uses the same raw `M_ConcreteRough00`**
   (`b.wall = mRough`). The log now reads
   `[LOOK] district material: RAW M_ConcreteRough00 for every block and the ground (one tone; no
   re-baked grain, no panel mix)`. The bas-relief block, the House, the bridges and the ground all share
   that one material/tone.
5. **Kept**: Plan-1 points 1-2 (the raw main-city surface + 0,7/m ground), 4/6 (House detail: balustrade,
   portal leaves, sconces, pendant lamps, tower courses/ribs, star, banners) and 5/8 (the dark bas-relief,
   14 404 verts). Evidence: `BabbdiCoop v0.9.76 loaded`, `[RELIEF] … 14404 verts`,
   `[PROBE] done: 55 ok, 0 misses`; the map shots now end at `district_111` (the relief moved to
   `district_109`-`district_111` because the room shots were removed); `district_71` shows the uniform
   grey district (no light panel block), `district_82` the surviving hall banner and pendants.

### v0.9.75: the **white civic centre over the sea** east of the district (the user's reference)

The user's reference photo: a white modernist civic building — a tall white slab on the left, a **stepped
glass pyramid** in a big concrete **A-frame** in the middle, a long low wing on the right with **rounded
balconies**, a glazed entrance, a lawn, black cars and a yellow taxi. Requested: «в стороне от Сталин
над водой в трёх метрах».

1. **Placement**: the complex stands **east of the platform's east edge**, at district-local
   `(438, -10,9, 170)` — on a 64 × 78 m white pier **3 m above the water** (deck top world 74,6 m; sea
   71,6 m), with four concrete caissons going down into the sea. The east terrace edge is 58 m away, so
   it reads as an island civic centre in the water; its 24 m slab rises ~13 m above the district deck.
2. **The composition** (all house-local metres, facing the district, front = −x):
   - **white slab** 20 × 18 × 24 m, six storeys of ribbon glazing with white piers, a roof parapet;
   - **stepped glass pyramid** 18 → 14,5 → 11 → 7,5 m wide over four tiers to 16,5 m, with white bands
     at every tier top, corner posts and face mullions;
   - the **concrete A-frame** — two 0,95 m legs from (±12 m, 0) to the apex at (−11, 20), a 0,55 m
     horizontal tie at 9,5 m and an apex block; the pyramid sits inside it;
   - **long wing** 14 × 34 × 12 m with a 3 m ribbon of glass and white mullions, and **three rounded
     balconies** on its far end (16-sided white slabs + curved parapet rails);
   - **glazed entrance** 26 m wide below the pyramid, seven white columns, a 4,6 m deep canopy, a wide
     glass door; low hall walls behind;
   - **plaza**: a 20 × 14 m lawn, a white drive, **three black cars + a yellow taxi with a roof sign**,
     four street lamps (two with real warm lights), three big trees and a monolith sign; the deck's
     underside and the caissons are concrete.
3. **Materials**: the building is the level's own **`M_PaintedWall00`** (the off-white bunker paint,
   the only true white plaster in the build) and a generated **`CoopCivicGlass`** 256² —
   **opaque** dark blue-grey glass with a pale reflection band (no transparent shader, so no
   see-through artefacts), smoothness 0,62. The cars/trees/lamps reuse the furniture atlas + the warm
   glow material.
4. **Evidence**: `[CIVIC] white civic centre east of the district, 3 m above the water (deck local y
   -10,9): 64x78 m pier, 20x18x24 m white slab, stepped glass pyramid 18 m wide to 16,5 m in a 20 m
   concrete A-frame, 14x34x12 m wing with three rounded balconies, glazed 26 m entrance, lawn, 3 black
   cars + a taxi, 8760 verts`; `[CIVIC] glass material: CoopCivicGlass 256x256 …`; `[PROBE] done:
   55 ok, 0 misses` (nothing else moved). Shots: `district_119` (from the east terrace — the complex
   over the parapet), `district_120` (front: pyramid, A-frame, glass entrance, taxi and black cars),
   `district_121` (3/4 from the front-left: slab, pyramid, wing, lawn), `district_123` (from the
   south-east: the whole island pier over the water, the three rounded balconies), `district_122`
   (wide from the district edge).
5. **Purely local**, like the district — each peer builds it, nothing is networked. The sea has no
   collider under it; the pier's deck and every volume have MeshColliders, so the player can fly down
   and walk the plaza. `[District] Enabled = false` still removes the whole district (and this).

### Plan-1 (v0.9.70–0.9.74): the eight modernisation points, done in order

The user's `E:\858\Plan-1.txt` (8 points). All eight are in, each measured with the map shots/logs below.

1. **The Stalin district wears the main BABBDI city's own building surfaces.** `[CITY]` census of the
   level's renderers within 500 m of the player's spawn: **33 tall facades on `M_ConcreteRough00` +
   `T_RoughConcreteMacro 1`**, 13 on `M_ConcretePanels00` + `TexturesCom_ConcreteNew0032_...`, 1
   board-formed; the ground under the player's feet is **`M_ConcreteRough00` raw**. So `[District]
   MatchCity = true` (default) now uses the level's raw `M_ConcreteRough00` **without the v0.9.11
   derived grain** (which had tone 1.68, cloud 0.15, desat 0.8 and was what made the district look
   smoother/lighter than the city), and the khrushchevka (panel) blocks use a clone of the city's own
   `M_ConcretePanels00` with `_BC_ST 0.22` so the panel seams read at their real ~2-3 m size. Log:
   `[LOOK] district material: RAW M_ConcreteRough00 + CoopPanelFacade … no re-baked grain, no tone lift`.
   `MatchCity=false` restores the old derived grain.
2. **The ground is the main city's ground.** It is the same raw `M_ConcreteRough00`; the platform's
   horizontal faces now map it at **0,7 repeats/m** (`GrainScale 0.35 × PlatformGrain 2,0`), the scale
   measured in v0.9.10 against the dam/city paving, instead of the old 0,28/m. The fine paver/patch
   pattern in `district_82` / `district_96` now matches the city ground in the reference shots
   `district_101`-`district_108` (shot at the player's spawn, unlit and under the same debug sun).
3. **Furniture in random, plausible rooms.** `Furnish()` gives every block (and each House tower)
   **3-4 random rooms** (a seeded, plot-hash RNG, no draw from the layout's RNG) with a coherent set:
   bedroom (bed, mattress/blanket/pillow, wardrobe with doors + handle, nightstand, chair, rug, picture),
   living room (sofa, TV on a stand with antennas, coffee table with books, shelf with 50 books, rug,
   picture), kitchen (counter with sink + tap, stove with four burners, fridge, table, two chairs,
   bottles, shelf) or study/storage (shelf + books, desk, chair, two crates, rug, picture). All from
   **one 384x192 palette atlas** (32 cells: plaster, woods, enamel, metal, five fabric colours, two
   carpets with a diamond field, books, bottles, a picture) on `S_EnviroSurface00`, one mesh + one
   MeshCollider per building. Log: `[FURN] 43,8x24,1 m block …: 4 rooms furnished, 3624 verts`,
   `[FURN] furniture material: CoopFurniture … atlas 384x192`. Shots `district_109` (kitchen),
   `district_110` (bedroom), `district_111` (living room).
4. **House of Soviets detail, pass 1.** Monumental **roof balustrade** on the stylobate's front edge;
   two **6,7 x 5,6 m open steel door leaves** in the portal (hinged at ±7 m, swung 78° inward, with
   ribs + handles); **wall sconces** with warm emissive lenses outside and inside the portal; and the
   **five warm pendant lamps** down the hall's central passage (each with a real point light). Shots
   `district_77` (portal + doors + balustrade), `district_73`/`district_82` (the lit hall).
5. **The dark abstract bas-relief.** The middle block of the **east flank** (37,2 x 23,6 m, 25 floors,
   at 354, 98.5) has its **outward long wall** (local +z, which faces world +x after the row's 90° yaw)
   built **solid** (`winPlusZ=false`, no balconies/bands/risalit) and carries a **33,2 x 120,8 m relief**:
   double frame, a ringed medallion (r 7,6 m) with a 24-ray sunburst, a central diamond and boss, two
   full-height zigzag ribbons, two glyph friezes (11 abstract symbols each), 12 corner bosses and a
   row of studs. A dark charcoal material (`CoopRelief`, 256x256, mean ~0,11) on the same
   `S_EnviroSurface00`. `[RELIEF] dark ornament on the outward wall of the block at 354,0,98,5 …`.
   Shots `district_114` (whole field), `district_115`/`district_116` (medallion/glyphs).
6. **House of Soviets detail, pass 2.** The towers got **string courses at every floor line**
   (0,30 m proud), **window-sill ledges**, a **top cornice** and **vertical pier ribs** on both long
   facades (on the room-cell boundaries, starting above the blank base band) — `TowerDetail()`; the
   stylobate roof got the balustrade (point 4), the hall the five pendants, the core a **22 m mast with
   a red five-point star** (two crossed flat stars, `S_Emissive00` red gradient), and the two tower
   inner walls two **dark-red banners with a pale emblem**. Shots `district_71` (the ribbed towers),
   `district_80` (the star in the skyline), `district_82` (banner in the passage).
7. **District atmosphere.** `Atmosphere()` adds **32 street lamps** (warm emissive lenses, 5 with real
   point lights) along the esplanade, the crest street, the seaward promenade and the flank legs;
   **9 benches, 9 bins, 8 bollards, 10 concrete planters with stylized trees, 9 parked cars**
   (muted red/teal/ochre/blue/grey bodies, black wheels, pale/red lamps), **8 laundry lines** on the
   khrushchevkas' balconies and **10 rooftop antennas**. One mesh with the furniture atlas + the warm
   glow material, 14 808 verts. Log `[ATM] street life: …`; shots `district_117` (approach avenue,
   lamps, trees, bench, open doors) and `district_118` (seaward promenade, a blue car, receding lamps).
8. **The bas-relief, stage 2 (more detail, more abstract).** On top of point 5: **three broken
   concentric rings** (three 104° arcs each at r 10,5 / 13,5 / 16,5), a **second offset 24-ray fan**,
   **two Archimedean spirals** in the tall upper-left/lower-right thirds, an asymmetric **constellation**
   (6 studs joined by thin rods upper-right, 5 studs + 2 rods lower-left) and **eight asymmetric
   shards**. The relief now has 14 404 verts; shots `district_114`-`district_116`.

**Config**: `[District] MatchCity` (true), `[District] Furniture` (true), `PlatformGrain` default now
**2,0**; the existing `GrainScale`/`GrainCloud`/`GrainGain`/`GrainTone`/`GrainDesat` remain for
`MatchCity=false`. **Diagnostics**: `[CITY]` one-shot census + the player-spawn reference shots
(`district_101`-`district_108`), `[FURN]`, `[RELIEF]`, `[ATM]`. **Verification**: build 0 errors;
`[PROBE] done: 55 ok, 0 misses`; the ground-floor passage is still 41,71 m to the rear wall and the
tower portal 34,56 m; the five figures still load; the whole district is one raw concrete + panel mix
with the city's own grain.

### v0.9.69: the "blocked passage" was a **61 m porch-step fin** in front of the tower door; the portholes lose their last "glass"

The user's screenshots: (1) a blank wall with only a narrow slit — the passage still closed; (2) the gargoyles half-buried in a slab; (3) «why do the portholes still have glass?».

1. **The passage blocker was `Steps(..., y0, 3)` in `BuildBuilding`.** The "entrance steps" were built
   from `y = 0` up to the block's whole `baseY`. For the House's **upper towers** (`baseY = 61,35`) that
   made a 2,6 m wide **stepped fin 61 m tall** at each end, passing through the lower tower and the hall.
   On the lower tower's inner face it stood right across the **1,7 m entrance**, leaving only a
   0,28-0,62 m gap at its side — the narrow bright slit in the user's screenshot. (The same bug made
   8-14 m fins on every terrace block, sitting uselessly at the deck below their entrances.)
   **Fixed**: the steps start at the block's own ground (`y0 - BASE`) and rise one `BASE` threshold, so
   on a terrace they sit on the terrace, on the deck on the deck, and the House towers get nothing above
   their floor. The portal is clear.
2. **A real portal instead of a 1,7 m slit.** `Bldg` gained `entranceW/entranceH`; the House's towers now
   cut **3,4 x 4,4 m** openings in both end walls (the ordinary blocks keep 1,7 m).
3. **Evidence** — measured, not judged from a still:
   - `[PROBE] House tower portal (hall -> tower -> side gallery): 34,56 m (bldg)` — a ray from the hall
     passes the 3,4 m portal, runs the 30 m ground-floor corridor and stops on the **outer wall's inner
     face** (before the fix the fin stopped it at ~5 m);
   - `[PROBE] House tower doorway floor (expect 10,15 m): 10,15 m (bldg)`;
   - the ground-floor passage along the entrance axis is still `41,71 m` to the rear wall;
   - `[PROBE] done: 55 ok, 0 misses`.
   Shots: `district_82` (the user's own framing — across the hall's central strip at the east tower:
   one wide open portal, no wall/fin), `district_83` (the portal close up: a 3,4 x 4,4 m opening into
   the corridor), `district_84` (the gargoyle pair from below).
4. **The gargoyles stand on the upper portal canopy.** The big 30 x 9,5 m canopy tipped -7° has its top
   at 11,2-12,3 m; each statue now stands on a **flat 2,4 x 0,9 x 2,4 m concrete pedestal** sunk into the
   slope (base 11,75 m), so it is level and fully supported — not cut by the slab (the v0.9.68 placement
   at 8,6 m put them through this canopy).
5. **No glass at all in the House.** The portholes' black disc was still reading as a pane. It is gone:
   the niche now ends in a **plain concrete bottom 7 cm inside the wall** with a shallow cast boss
   (45 % radius) — all in the district's own rough concrete (`part 0`), no `mDark`, no transparency.
   The only dark material left in the House is the gargoyles' **recessed eyes and mouth** (not glass).

### v0.9.68: the ground-floor passage was **barred by a 0,7 m threshold slab** — now a 0,14 m sill

The user's two screenshots of the still-blocked ground-floor passage. v0.9.67 removed the column on the
axis (that was real, but not the last obstacle); the **entrance itself** was unenterable. Measured, then
fixed:

1. **The culprit.** `HouseOfSoviets` had a "monumental threshold slab" from v0.9.63:
   `18 × 0,7 × 7,2 m` at `z = -id + 2,6` — **0,70 m tall**, i.e. **2,3× the CharacterController's 0,30 m
   `stepOffset`**. It straddled the front wall and ran 4,7 m into the hall, so walking in from the porch
   meant a 0,70 m step up onto it and another 0,70 m down on the far side. That is the "проход
   заблокирован": you could not get past the doorway at all.
2. **The fix** — the same footprint is now a **0,14 m sill** (`18 × 0,14 × 7,2 m`): the threshold still
   reads as a sill in the portal, and the controller walks over it without noticing. Hall floor behind the
   entrance: **10,29 m** (0,14 above the floor) instead of the old 0,85.
3. **The passage is now measured end to end, not judged from a still**:
   - `[PROBE] House ground-floor passage (0,35 m up, expect the rear wall, ~41 m): 41,71 m (podium)` — a
     knee-height ray from just inside the doorway reaches the **rear wall** 41,7 m away;
   - `[PASS] entrance axis, 0,35 m over the floor: 53,7 (podium)` and the same at 1,15 m — from the porch
     through the whole hall, **one single hit**, the rear wall. Nothing stands between the entrance and
     the back of the hall.
   - `[HALL] things over 0,55 m on the ground floor: …` — a 2 m down-ray grid over the whole hall now
     lists **only surfaces inside the two towers** (x ±16…34: their own stairs/fittings), none in the
     passage or the hall.
4. **The gargoyles were floating 3,5 m above the canopy.** They had been placed at `y = 12,1` while the
   entrance canopy's top is `8,6` (the box `22 × 0,9 × 4,8` with the dark soffit). They now stand **on**
   it, plinth fully supported, at **(±9,6, 8,6, −27,6)** — still flanking the portal, now visibly resting
   on its canopy instead of hanging in air.
5. **Evidence**: `[PROBE] done: 53 ok, 0 misses`; shots `district_73` (the hall from the entrance: open
   floor all the way to the far colonnade), `district_79` (the porch towards the entrance — the raised
   white band of the old threshold is gone) and `district_77` (the pair on the canopy).

### v0.9.67: the hall's central passage is flanked by a **pair** of columns, and two **brutalist gargoyles** guard the entrance canopy

1. **No column in the passage.** The hall's colonnade had one column on the building's axis, standing in
   the central passage. The centre column is gone: the row is now ±11/±22/±33/±44 m and the passage itself
   is **flanked by a pair at ±4,5 m** — two columns at the sides, none in the way.
2. **Two gargoyles** (`Gargoyle()`), one on each front corner of the entrance canopy at (±12,2, 12,1,
   −29,2), facing outward: a crouched, hunched beast in raw concrete — 1,8 m plinth, crouched body, hunched
   shoulders, a head thrust forward with a **long snout**, **recessed black eyes and mouth** (mDark, not
   glass), two swept horns, angular folded wings, clawed forelimbs and a curled tail. They are deliberately
   oversized (≈3,5 m) against the portal.
3. Evidence: `[PROBE] done: 51 ok, 0 misses`; shot `district_77` — the pair on the canopy, the portholes and
   the portal.

### v0.9.66: the west annex is **walkable** (normal storeys, no glass), with its own experimental stair

1. **The annex was a stack of solid plates** — its bands left no headroom and its "glazing" volumes filled the
   space, so you could not get in (the user's screenshot). It is now a real building: **three storeys of
   6,4 m** (floors 89,75 / 96,15 / 102,55, roof 108,95) inside the 40 × 34 m chevron outline, built from
   `Rect2` plates with a **stair well** in the rear strip and `Wall` perimeters with **open window holes**
   (2,4 × 2,6 m on a 3,6 m pitch) — no glass anywhere.
2. **Access from the main stair**: the west tower's upper section got a **7th floor**, so the game's own
   stair now ends exactly on the annex's floor 0 (89,75 m); a 2,2 × 2,75 m **doorway in its outer end wall**
   at that floor (`endMinus = topDoor`, `Bldg.endMinus`) leads onto the annex's floor-0 ring, which runs
   all the way round the tower. The main stair goes no further.
3. **The experimental stair** (`Flight`): two long **straight flights of 40 steps** (0,16 m each, 12 m run)
   in the rear well, alternating lanes with landings — its own design, away from the tower's stair and clear
   of its footprint.
4. **All glass removed from the House**: the bays' black panels, the annex's dark glazing volumes and the
   vertical slot strips are gone. Only the portholes keep their opaque black bottoms (they are the closed
   bottom of the niche, not glazing) — everything else is concrete with open holes.
5. Evidence: `[PROBE] House … upper tower roof 99,75`, `rear block roof 107,95`, `gallery floor 48,79`,
   `thin bridge floor 74,62`, `core top 94,00`; `[PROBE] done: 51 ok, 0 misses`; shot `district_71` — the
   annex now reads as a glazed-free concrete volume with real windows.

### v0.9.65: reference-1 **portholes** on the facades, and the west tower flows into a **cantilevered chevron annex**

1. **Portholes** (`Porthole()`): a concrete collar (12-sided ring, 0,26 m proud) around an **opaque black**
   glass disc 2 cm off the wall — no transparency, no cut hole. Spread evenly: five on each tower's front
   base band (y 19,3, 5,2 m pitch), three on each tower's outer band, three on the stylobate's flanks and
   **nine along the stylobate's front** above the ribbon. Vertical slot windows (0,5 × 4,4 m, black) between
   them on the tower bands.
2. **The west tower's annex** (reference 2, the Geisel Library): eight **splayed piers** (`Frustum`, from
   2,6 × 2,2 m at the lower tower's roof 51,6 m to 3,8 × 3,2 m at 83,8 m, leaning outward from ±17,5/±13 to
   ±21/±16,5) carry a **cantilevered stack of three chevron bands** — 40 × 34 m plates with 45° diamond
   points on every side (the zig-zag plan) at 85/90/95 m, opaque dark glazing volumes between them, a dark
   attic band and a roof plate at 100 m. The tower's own roof disappears inside the annex, so the mass
   **flows** into it.
3. Evidence: `[PROBE] done: 51 ok, 0 misses`; shot `district_71` — the splayed legs, the chevron stack and
   the portholes along the stylobate.

### v0.9.64: reveals and wall ends are **closed from both sides**, and the core stair is a walkable switchback

1. **The remaining see-through faces were the single-sided reveals.** Each window's four reveal quads had
   their normal pointing into the opening, so from the far side (looking into a window at an angle, or along
   a facade at the corners) they were backface-culled and you looked *through* the wall thickness — the
   user's screenshots 1-2. `Wall` now emits **both sides of each reveal** (coplanar, opposite normals; the
   culling picks one) and also the **wall's two end faces**, so a wall is a closed box whose hollow corner
   cannot be looked into from outside.
2. **The core stair is a three-flight switchback**: 27 steps of **0,158 m** per flight (the old single run
   was 0,298 m per step — barely climbable), landings at both ends, **a sloped handrail and posts** on every
   flight. It rises from the gallery's roof (51,35 m) to a landing at the thin bridge (64,15 m), whose front
   wall now has the matching doorway at x −4…−1.
3. Evidence: `[PROBE] done: 51 ok, 0 misses`; shots `district_71` (the switchback on the core,
   `district_74` (honeycomb close-up — the reveals are lit from every angle, no sliver).

### v0.9.63: the six fixes — closed slabs, a monumental portal, a fused core with stairs, ×3 corridors, bigger lower windows, no blocked windows

The user's six-point list with screenshots.

1. **No more see-through slab edges.** `Rect2` (the House's floor/roof/landing rings) emitted only a top
   and a bottom face, so every slab's side edge was open — that was the thin bright slot along wall/ceiling
   junctions and on the building's outer corners («прозрачные текстуры… на многих углах снаружи»). `Rect2`
   is now a **closed `Box`**. `Slab` keeps two-face pieces (`RectFaces`) so its hole rims cannot z-fight
   with the new boxes.
2. **The middle column is gone; the entrance is monumental.** The five round columns became six, placed at
   ±10/±20/±30 m, none on the axis. The 14 m opening now has a **brutalist portal**: two 4,4 × 9,4 m jambs,
   a 22,8 × 3,0 m lintel, a raised 18 × 7,2 m threshold slab in the recess, a 30 × 9,5 m canopy tipped
   −7° on two 1,5 × 10,8 m struts — deliberately oversized.
3. **The core is fused with the links.** Doorways are cut in all four of its walls on the gallery and
   bridge floors, it got a floor slab per storey (11), and an **external concrete stair** (43 steps along
   x, 12,8 m rise) runs from the gallery's roof up to a landing at the bridge, whose front wall has a
   matching doorway.
4. **Corridors ×3**: both tower sections are built with `corrMul = 3` — the hall between the rooms is
   6,6 m clear instead of 2,2, the rooms pay for it.
5. **Windows 0,5 m lower and 20 % bigger**: tower cells 2,4 → **2,88 m**, sill 1,6 → **1,1 m**.
6. **No window into a wall**: the upper sections' front wall carries **no windows on floors 0-1**
   (`Bldg.winMaskMinusZ`), where the four glazed bays hang in front of it; the core's windows are masked on
   the floors the gallery and the bridge pass through (`coreMask`).
7. Evidence: `[HOUSE] v0.9.63 …`; `[PROBE] House stylobate roof 22,95`, `upper tower roof 100,01`, `rear
   block roof 105,60`, `gallery floor 48,79 (house_core)`, `thin bridge floor 74,62 (house_core)`, `core top
   94,00 (house_core)`; `[PROBE] done: 51 ok, 0 misses`.
8. Shots: `district_71` (the external stair on the core, the bays, the gallery/bridge), `73` (the hall —
   the wall/ceiling joint is solid now), `77` (the new portal), `70`/`72` (front/gap).

### v0.9.62: the House of Soviets rebuilt to the **architectural references** (podium on columns, sectioned towers, glazed bays, core, two bridges)

The user's third round with two photo references and «перенести на модель КАЖДЫЙ существенный выступ…
трижды перепроверь… убедись что в нем не будет прозрачных текстур». The photos show what v0.9.61 missed:
a **two-level podium**: a recessed ground floor on **round columns**, an overhanging upper storey with
the ribbon and the wide **loggia band**; towers whose **lower section projects forward** with **glazed
bays** hanging under the break; a **narrow core** between the towers; and **two** links — a **wide gallery**
at the break and a **thin bridge** high above it.

1. **Podium** (`podium` mesh): the ground floor is recessed **4 m** and built on **five 12-sided round
   columns** (r 1,6 m) under the overhang; the overhang's slab ring closes the soffit; the upper storey sits
   at the full 107,3 × 49,3 m with the 1,4 × 1,6 m ribbon (2,0 m pitch) and, across the front, a **loggia
   band** of 3,2 × 2,4 m openings (4,4 m pitch); a 22 x 4,8 m entrance canopy projects over the porch.
2. **Towers**: **lower** 30 × 26 m, 8 floors (6 above the stylobate), a **blank two-storey band** with its
   cornice, top 61,4 m; **upper** 30 × 22 m, 6 floors, **set 3 m back at the front**, top 100,0 m, with a
   cornice on the break and **four glazed bays** (6 × 3 m, two storeys) hanging over the break on its front
   face — their glazing is **opaque black** (`M_Black00`), never a transparent material.
3. **The gap** (31 m) now carries **two** links, as in the frontal reference: the **wide gallery** on the
   lower floors 6-7 (31 × 18 m, 38,55-51,35 m; its roof is the terrace the upper blocks' ground doors open
   onto) and the **thin bridge** on the upper floors 2-3 (31 × 10 m, 64,15-76,95 m). Both walkable
   (`endPlus/endMinus` doorways + the per-floor window masks).
4. **The narrow core** 12 × 16 m between the towers, set back 6 m from the front, from the stylobate's roof
   to 84 m (94 m district), with vertical strips of small windows — the stair/lift shaft of the photos.
5. **Rear blocks** to 95 m (105,6 m district) with the 20 m antenna mast.
6. **No transparent textures anywhere**: every surface is a `Wall` (two faces + caps + watertight reveals)
   or a `Box`; the bays' glazing is a solid black box; no `mGlass`/Sprites material is used in the House.
7. **Evidence** (one run): `[HOUSE] v0.9.62 architectural rebuild: stylobate 107,3x49,3 m — ground floor
   recessed 4 m on 5 round columns, overhanging upper storey with the window ribbon and the loggia band
   (top 23,0 m); each tower is two sections: lower 30,0x26,0 m, 8 floors with a blank 2-storey base band
   and a cornice -> 61,4 m, upper 30,0x22,0 m, 6 floors set 3 m back at the front with **glazed bays** over
   the break -> 99,8 m; the 31,0 m gap carries the **wide gallery** (lower floors 6-7) and the **thin
   bridge** (upper floors 2-3); narrow core 12x16 m to 94,0 m; rear blocks to 105,0 m + a 20 m mast`;
   `[PROBE] House … stylobate roof 22,95`, `upper tower roof 100,01`, `rear block roof 105,60`, `gallery
   floor 48,55 (house_gallery)`, `thin bridge floor 74,15 (house_bridge)`, `core top 94,00 (house_core)`;
   `[PROBE] done: 51 ok, 0 misses`.
8. **Shots**: `district_71` (the readable 3/4: columns under the overhang, ribbon, base bands, glazed bays,
   gallery + bridge, stepped upper sections, rear blocks), `70` (front), `72` (the gap/bridges), `73` (hall),
   `76` (rear), `77` (entrance).

### v0.9.61: the House of Soviets — **sectioned towers** and **three stacked bridges**

The user: «Крайне грубо… мостики более сложные, разных размеров и один над другим… Сами башни тоже разбиты
на секции (как по высоте, так и по глубине)» — so v0.9.60's plain boxes were replaced by the reference's
stepped composition.

1. **Each tower is now two stacked sections** (two `Bldg`s per tower, four in all):
   - **lower**: 30 x 26 m, **9 floors** of 6,4 m, windows only from floor 4 — the first two storeys above
     the stylobate's roof are a **blank base band**, exactly the dark band under the white blocks in the
     photo. The band is 0,7 m proud of the shaft and ends in its own **cornice** (`house_bands` mesh).
   - **upper**: 27 x 20 m, **6 floors**, set **1,5 m back in x and 6 m at the rear**, standing on the lower
     block's **roof terrace** (its ground-floor door opens onto it) — the depth break the user asked for.
     Top: 106,67 m district.
2. **Three bridges of different sizes, one above another** (`Bridge()` helper, each its own mesh/collider):
   **A** 31 x 20 m on the lower blocks' floors 5-6 (32,15-44,95 m), **B** 34 x 14 m on the upper blocks'
   floors 1-2 (64,41-77,21) and **C** 34 x 12 m on floors 4-5 (83,61-96,41). Each has a floor and a ceiling
   plate, a ribbon of small windows near its top and a solid parapet below. All three are **walkable**:
   `Bldg.endPlus/endMinus` cuts doorways in the towers' inner end walls on exactly those floors, and
   `Bldg.winMaskPlusX/winMaskMinusX` (v0.9.60) suppresses the honeycomb there only.
3. **Rear blocks** raised to **103 m** (they stay above the towers, as in the reference) with the 20 m mast.
4. **Evidence**: `[HOUSE] v0.9.61 … each tower is two stacked sections — lower 30,0x26,0 m, 9 floors with a
   blank 2-storey base band and a cornice, upper 27,0x20,0 m, 6 floors set 1,5 m back in x and 6 m at the
   rear on the lower block's roof terrace -> 106,7 m; 31,0 m of open sky between the lower blocks; **three
   bridges** A/B/C (31 x 20 m on the lower floors 5-6, 34 x 14 m on the upper floors 1-2, 34 x 12 m on
   floors 4-5, all walkable); rear blocks to 113,0 m + a 20 m mast`; probes `lower-tower roof terrace: 68,01`,
   `upper-tower roof east/west: 106,67`, `rear block roof: 113,60`, `bridge A/B/C floors: 42,15 / 74,41 /
   93,61` (each with its own collider name); `[PROBE] done: 52 ok, 0 misses`.
5. The first build of this round placed the upper sections with `p.y` **and** `baseY` both set — the old
   double-plinth bug — and they floated 58 m too high (the two roof probes MISSed). Fixed: `p.y = 0`, the
   height lives in `baseY` only.
6. **Shots**: `district_71` is the readable 3/4 (stylobate + ribbon, base bands + cornices, three bridges,
   the stepped upper sections, the rear blocks); `72` the bridges/gap head-on; `70` the front; `73` the
   hall; `74` the honeycomb; `76` the rear blocks and the mast.

### v0.9.60: the House of Soviets rebuilt as the real **"H"** — two slabs, an open gap, a bridge

The user's front reference photo: the real building's contour is an **H** — two separate slab-towers with
a wide **open gap** between them, a two-storey **bridge** at ~55 % height, and a low two-storey podium
under everything. v0.9.59 filled that gap with a loggia volume and stretched the house to 208 m, so
«контуры вообще не совпадают». This round rebuilds the massing **and** restores the real 13-storey scale.

1. **The massing** (house-local): **stylobate** 107,3 x 49,3 m, two storeys of 6,4 m (roof 12,95) with the
   reference's single **ribbon of windows** (1,4 x 1,6 m on a 2,0 m pitch, sill 5,6) and the 10 m porch;
   **two slab-towers** 30 x 26 m at ±30,5 m, **13 storeys** of 6,4 m -> 83,35 m, honeycomb 2,4 x 2,4 m on
   a 3,2 m pitch; **31 m of open sky** between them; a **bridge** on floors 7-8 (44,95-57,75 m) with one
   ribbon of small windows and a solid parapet; **two rear blocks** 30 x 9,65 m against the towers' backs,
   rising to 89,75 m (the reference's taller grey volumes) with a **20 m antenna mast** on the west one.
   There is deliberately **no central rear block** — the gap is open *through* the building.
2. **The scale.** The real house is a 13-storey ~72-80 m building; this one is **83,4 m** (towers) /
   **89,8 m** (rear blocks) / 110 m with the mast — inside the user's ±30 % of the reference, plan
   unchanged. v0.9.59's 208 m was ~2,7x the real building, which is exactly why the silhouette could not
   read. (If the old 200 m skyline is wanted back, it is the storey count — one constant.)
3. **Walkability kept and extended**: the two-storey hall with its colonnade (7 columns) and the towers'
   entrances on their inner ends; 13 floors and a stairwell in each tower; and the **bridge is walkable** —
   its floor/ceiling line up with the towers' floors 7/9, and the towers' inner walls get doorways on those
   two floors (`Bldg.endPlus/endMinus`) while their windows are suppressed exactly there by a new
   **per-floor mask** (`Bldg.winMaskPlusX/winMaskMinusX` + `Wall`'s `mask` argument). So you can cross the
   gap between the towers.
4. **No window into a wall** (the v0.9.55-58 lesson): the towers' rear faces are solid (the rear blocks
   touch them), their inner faces are solid only where the bridge lands, and the bridge's ends are the
   towers' own walls.
5. **Evidence** (one map-shot run): `[HOUSE] v0.9.60 rebuild to the FRONT reference: stylobate 107,3x49,3
   m, 2 storeys of 6,4 m (top 23,0 m), two slab towers 30,0x26,0 m at ±30,5 m, 13 storeys -> 93,4 m,
   31,0 m of open sky between them, the bridge on floors 7-8 (55,0-67,8 m), rear blocks to 99,8 m + a
   20 m mast; plan 107,3x49,3 m (unchanged)`; `[PROBE] House hall floor: 10,15 (podium)`, `stylobate roof:
   22,95`, `stylobate wall top: 22,95`, `tower roof east/west: 93,61 (bldg)`, `rear block roof: 100,35
   (house_rear)`, `hall ceiling: 22,17`, `bridge floor: 54,95 (house_bridge)`; `[PROBE] done: 50 ok,
   0 misses`.
6. **Shots**: `district_70` (the front from the esplanade — the H), `71` (3/4), `72` (the gap and the
   bridge head-on from below), `73` (the hall), `74` (the honeycomb), `75` (the stylobate roof up at the
   bridge), `76` (the rear blocks and the mast), `77` (the entrance), `78` (the skyline 3/4) and `79` (the
   plan). The figure close-ups `61/63/64/67/68/69` are unchanged (they are on another block's roof).

### v0.9.59: the House of Soviets **rebuilt from the user's model and films** *(massing superseded by v0.9.60)*

The user: «наши 5 NPC временно убери с Дома советов на другую крышу Сталин. А сам Дом советов нужно
СИЛЬНО пересобрать, чтобы максимально приблизить к оригиналу» — with `E:\858\house-of-soviets.zip` and two
films as references, keeping the envelope within ±30 % in area and height.

1. **What the references actually are.** The zip holds a **photogrammetry OBJ** (100 433 verts / 199 999
   tris, 6 texture atlases, no normals, a tower crane and the ground baked in). It was **measured, not
   loaded**: at 200 k tris and ~18 MB of JPEG it is heavier than the whole district, has no interior and
   no axis alignment. The films (an aerial pass and a street maquette) give the massing: two **white
   slab-towers** covered in a honeycomb of deep square cells, a **recessed central volume** between them
   with a vertical stack of huge **loggia** openings, lower blue-grey **rear wings**, a low **entrance
   pavilion** with a long ribbon of windows, and the whole on the plinth with the grand stair.
2. **The rebuilt House** (all in `HouseOfSoviets`, house-local metres):
   - **stylobate** 107,3 x 49,3 x 25,6 m, honeycomb: 3,0 x 3,0 m cells on a 3,6 m pitch, splay 0,22 (the
     v0.9.57 watertight reveal), 4 storeys of 6,4 m, an open hall inside with a colonnade of 9 columns;
   - **two slab towers** 40 x 26 m at ±33,35 m, 31 storeys of 6,4 m -> 198,55 m over the plinth, the same
     honeycomb (3,4 x 3,4 m cells) up the front and the outer end, the inner end and the rear **solid**
     (they abut the central volume and face the wing's slot), the entrance cut in the **inner** end (into
     the hall) — v0.9.55/58's lesson: no window may look into a wall;
   - **central volume** 26,7 m wide, five **17,4 x 16 m loggias** with 6 m spandrels and 4,65 m piers, a
     solid back 12 m behind, an upper shaft with 7 rows of cells and the **crown of 11 fins** (190-198 m);
   - **rear wing** 107,3 x 16,7 m up to 70,55 m with a cell grid on its rear and sides, solid front (it
     faces the towers across a 3 m slot);
   - **entrance pavilion** 44 x 10 x 12 m in front of the stylobate, ribbon windows 1,5 x 1,8 m on a
     2,2 m pitch and an 8 m entrance that lines up with the stylobate's porch.
3. **Envelope, checked against the user's box**: plan 107,3 x 59,3 m = **+21 % area**, height **208,6 m**
   against the old 218,0 = **−4 %** — both inside ±30 %.
4. **Previous fixes kept**: the `Wall` top/bottom caps (no hollow wall tops), the splayed reveal cut from
   the **inner** face as well (v0.9.57), roof and floor rings reaching the facade plane, no coincident
   faces — and one new one: the towers sit **0,3 m inside** the stylobate's faces, because flush faces
   would z-fight along the lower 25 m (and their doors, which used to be on that flush outer side, moved
   to the **inner** ends into the hall: `Bldg.doorPlusX/doorMinusX`, independent of the window flags).
5. **The figures are on another Stalin roof** (`FiguresOnBlock`): the widest ordinary block
   (`43,8 x 24,1 m at 47,9,274,0`, roof **148,4 m**), 2 m inside its `+z` edge, 4 m apart, facing
   outward; the five close-up poses were flipped to `+z` so they still shoot the faces, not the backs.
   Log: `[FIGURE] the five are on another Stalin roof now: block 43,8x24,1 m at 47,9,274,0, roof 148,4 m,
   2 m inside the +z edge, 4 m apart, facing outward; 5 of 5 loaded`.
6. **Evidence** (one map-shot run): `[HOUSE] v0.9.59 rebuild (model + films): stylobate 107,3x49,3 m …,
   slab towers 40,0x26,0 m at ±33,4 m, 31 storeys -> 208,6 m, central loggia volume 26,7 m wide to 200,0 m
   (five 17,4 x 16 m openings), rear wing 107,3x16,7 m to 80,6 m, entry pavilion 44,0x10,0x12,0 m;
   envelope 107,3x59,3 m, height 208,6 m (old 218,0)`; `[PROBE] House hall floor: 10,15`, `stylobate roof:
   35,75 (podium)`, `stylobate wall top: 35,75`, `tower roof east/west: 208,8 (bldg)`, `hall ceiling:
   34,97`, `central loggia wall at 60 m: ~44-56`, `entrance pavilion roof: 22,0`; `[PROBE] done: 49 ok,
   0 misses`; `built 36 buildings, 0 skipped plots, 22 bridges`.
7. **Shots**: `district_70` (the front from the esplanade — the two honeycomb slabs and the loggia stack),
   `71` (3/4), `72` (the loggias head-on), `73` (the hall: the colonnade and light through the cells),
   `74` (a honeycomb close-up: deep square cells with splayed reveals), `75` (the stylobate roof up at the
   central volume), `76` (the rear wing's cell grid), `77` (the entrance pavilion and the grand stair),
   `78` (the front 3/4 from 210 m) and `79` (the plan, lit). The figure close-ups `61`, `63`, `64`, `67`,
   `68`, `69` now show them on the new roof, faces to the camera.

### v0.9.58: the towers' **inner** end walls are the ones that had to be solid (v0.9.55 had them swapped)

The user's screenshot: a tower wall whose two openings are 0,94 m deep niches closed by a flat concrete
face — «Окна выходящие в стены все замуруй тоже в Доме советов (они смотрят внутрь с обеих сторон дома)».

1. **v0.9.55's flags were inverted.** `winPlusX = s < 0, winMinusX = s > 0` bricked the towers' **outer**
   end walls and left the **inner** ones open — the exact opposite of the intent: the inner faces (x =
   ±7,25 m) are the ones pressed against the central bay's piers (x = ±7,23 m), so every window there was
   a 0,94 m deep niche whose back was the pier's face (the user's screenshot), and the entrance door was
   on the blocked side too. Now `winPlusX = s > 0, winMinusX = s < 0`: for the east tower (`s = +1`) its
   `+x` (outer) wall keeps the door and the windows, its `-x` (inner) wall is solid; the west tower is the
   mirror image.
2. **Unchanged**: `winFromFloor = 4` — no windows on the first four storeys on any facade (there the
   towers stand inside the hall); straight-free splayed frames (v0.9.57); wall top/bottom caps and the
   podium's roof/floor reaching the facade plane (v0.9.57).
3. **Evidence**: `[PROBE] done: 52 ok, 0 misses`; shots `district_79` and `district_80` — the central
   strip at hall level, looking at each tower's inner end wall: one blank concrete face from the floor to
   the storey above, no openings at all. `district_72` still shows the splayed frames on the outer
   windows above the roof.

### v0.9.57: the window **frames are back**, and the facade's hollow top is closed

Two corrections after the user's reply to v0.9.55: «ты просто вырезал все оконные рамы (скрин 1) и теперь
окна слишком большие. И прозрачные текстуры самого фасада в горизонтальной плоскости никуда не делись».

1. **v0.9.55's straight reveals were wrong** — the splay *was* the window's frame. Flattening it made every
   opening 0,56 m wider with no frame. It is back, but **watertight this time**: `Wall` now cuts the
   exterior face from the **outer** rect and the interior face from the rect **inset by `splay`**
   (`holesIn`), and the reveals connect the two. Before v0.9.55 both faces used the outer rect while the
   reveal ended on the inset one — that open 0,28 m ring was real, but it was not what the user was
   pointing at; the framed look is what matters. Doors are never inset.
2. **The real "transparent facade in the horizontal plane" — the wall is a hollow skin.** `Wall` emits two
   faces 0,94 m (`T`) apart and **nothing closes the wall at its top or bottom**. The podium's roof ring
   started at `−hw + T`, i.e. 0,94 m *short of the facade*, and its floor ring the same — so a 0,94 m open
   channel ran around the whole roof edge straight into the wall's cavity, whose inner faces are
   back-facing from inside the cavity and therefore culled: you looked **through the wall** — from the
   podium roof into the hall and out through the hall's windows to the city (the user's screenshot 2:
   a "transparent plane" with the colonnade and the city behind it).
   - `Wall` now emits a **top and a bottom cap** (5 mm inside so they can never be coplanar with a slab);
   - the podium's **roof ring and hall floor ring reach the facade plane** (`±hw`, `±hd` instead of
     `±(hw−T)`), so the plates cover the wall's thickness.
3. **Both towers still have solid inner end walls and no windows on their first four storeys** (v0.9.55,
   unchanged) — those were the "окна в глухую стену".
4. **Evidence** (one map-shot run): a new set of `[PROBE] House …` rays measures the horizontal plates from
   both sides — `podium roof plate, from above: 35,75 m (bldg)`, `hall ceiling, central/front/west:
   34,97 m (podium)` (three strips), `east/west tower roof plate: 202,41 m (bldg)`, `hall floor: 10,15 m`,
   and the regression test **`podium wall top, from above: 35,75 m (podium)`** — before the fix that ray
   fell 25 m into the hall. `[PROBE] done: 52 ok, 0 misses`.
5. **Shots**: `district_72` (a tower window above the roof: the splayed frame is back, the opening is no
   longer 0,56 m too wide), `district_78` (on the podium roof at its west edge, along the roof/facade
   joint: no channel), `district_70`/`71` (the hall: blank tower walls at hall level, the podium's own
   windows still open onto the esplanade).

### v0.9.55: the House of Soviets — **watertight window reveals** and no windows into blind walls

The user: «Нужно избавится от прозрачных текстур (скрины 1 и 2) и убрать окна с той стороны где они выходят
в глухую стену (скрин 3)» — screenshots taken inside the House's hall and at a tower's facade.

1. **Why the walls were transparent around splayed windows** (`[Wall]`, the reveals). Both faces of a wall
   are cut with the **outer** window rect, but a splayed reveal connected that rect to a **smaller inner**
   one (`ix0 = r.x + splay …`). So on every splayed opening — and only the House's towers use `splay =
   0,28` — there was an **open ring 0,28 m wide** between the inner face's hole edge and the slanted jamb:
   from inside you looked straight through it to the sky, a bright sliver above and beside each window.
   That is the "прозрачная текстура" in the user's frames 1-2. The reveal is now a **straight rectangular
   tube** (`ix0 = r.x, ix1 = r.z, iy0 = r.y, iy1 = r.w`), exactly matching the hole in both faces, so an
   opening is watertight.
2. **The towers' inner end walls are solid** (`Bldg.winPlusX / winMinusX`). The two towers stand in the
   central recess and their inner faces (x = ±7,25 m) are pressed against the bay's piers (x = ±7,23 m) for
   the piers' whole 127,6 m — every window cut there opened into a blank wall 2 cm away (user's frame 3).
   Both the windows and the useless inner entrance door are gone; the outer ±x entrance from the hall's
   side strip stays.
3. **The towers' first four storeys carry no windows at all** (`Bldg.winFromFloor = 4`). Down there the
   towers are *inside* the hall: their ±z and outer ±x windows opened across the 4,85-7,7 m aisle onto the
   podium's own wall — the other half of "окна в глухую стену". Windows now start at floor 4, i.e. at the
   podium's roof (25,75 m), where the towers become ordinary facades.
4. **Evidence**: `[HOUSE] … v0.9.55: the towers' inner end walls are solid and their first 4 storeys carry
   no windows …, and every reveal is straight (a splayed one left a 0,28 m open ring you could see the sky
   through)`; `[PROBE] done: 44 ok, 0 misses`; `built 36 buildings, 0 skipped plots, 22 bridges`.
5. **Shots** (four new House poses, inserted after the figures — every later shot moved by four):
   `district_70` (the hall's west aisle from the floor: the tower's hall-level wall is blank, the podium's
   wall left, the olive roof above), `district_71` (the central strip: the towers' front walls at hall
   level are blank, while the podium's own windows still show the esplanade), `district_72` (a tower window
   **above the roof**, straight on — a clean rectangular reveal, no sky sliver) and `district_73` (the
   towers' inner facades from the slot above the bay: solid). Previously: `district_5`/`36` etc. are
   unchanged; the corner/pad shots are now `district_82`-`84`, the parapet cut `85`-`87`, the stair
   end-faces `88`/`89`.

### v0.9.51: the platform's parapet is **cut** at the corner — the wall now runs to its end

The user's screenshot (standing on the corner pad, looking east along the +5 m terrace's south wall):
«Нужно чтобы она шла ровно до конца, перпендикулярно горизонтальной плите… сейчас из неё в конце
выступает часть фундамента платформы Сталин, её нужно убрать (без закладок, а вырезать).»

1. **Named it with numbers, not by eye.** A new `[CORNER]` scan (`CornerMeshScan`, map-shot only) lists
   every mesh of ours and of the level at the corner with its **local** bounds — the district is rotated,
   so a world AABB is transformed corner by corner back into the local frame first. It showed nothing else
   of ours in that strip, and the `corner z-profile x=378` probe named the offender exactly: at
   x ≈ 375-380, z 0-10 the ground was **1,15 m (the platform's east parapet)** while the pad around it is
   0,345 — a 0,8 m box with a coping standing in front of the terrace's south wall at the platform's edge.
2. **Cut, not patched.** The east wall is now built as **two boxes**: over the corner apron
   (`CornerApron + 0,3` = 12,3 m from the crest) its top is at **0,34 m**, i.e. flush with the pad, and it
   keeps its full 1,15 m height only from z = 12,3 to the seaward end. The **coping** is cut the same way
   (it starts at 12,3 m too) and the `parapet_east` collider was shortened to match, so there is nothing
   left standing in front of the wall — no cover box, no filler.
3. **Result**: the terrace's south wall runs its whole length and ends at the platform's edge, the pad's
   surface runs flat to that edge — one perpendicular joint, nothing protruding. The 5 m strip is now open
   to the sea (the drop is deliberate: the user asked for the piece to be removed).
4. **Evidence**: `[PROBE] corner z-profile x=378 (z -4..14, 2 m)` used to read
   `0,32 0,32 **1,20 1,20 1,20 1,20 1,20 1,20** 6,05 5,00` and now reads
   `0,32 0,32 0,32 0,32 0,32 0,32 0,32 **0,00** 6,05 5,00` — the 1,15 m parapet is gone from the corner and
   only the pad and the terrace's rail (6,05 m) remain; `[PROBE] done: 44 ok, 0 misses`;
   `[CORNER] listed 8 of our meshes and 15 level renderers at the south-east corner` (ours: deck, dam_wall,
   corner_pad, terraces, terrace_rails, streets, bldg, square — the level adds its blockout cubes and the
   water planes, i.e. nothing of theirs protrudes either).
5. **Shots**: `district_81` is the user's own framing (the wall runs to its end, the pad runs flat to the
   platform's edge), `district_82` the corner from the crest side, elevated, `district_83` from over the
   sea (the parapet now starts beyond the corner). The stair end-face shots moved to `84`/`85`.

### v0.9.50: the corner pad is **flush with the crest's granite** — the thin plate on the stone is gone

The user's video (20 s, recorded on v0.9.47) shows them walking **around** a thin concrete slab lying on
the dam's granite and **climbing onto it**, with the terrace's wall behind: «проблема была не в ровной
платформе… на нём я хожу вокруг той части которую нужно убрать и залезаю на неё». Reverting v0.9.46/47
alone was not enough — the measured cause was the pad's own surface.

1. **Measured first, with the pad's collider switched off** (`[PROBE] corner map` / `corner z-profile`):
   the level's granite shelf (0,32 m, `SM_Scene_A_Blockout00.011`) does not stop at z = 0 — it runs
   diagonally **under the whole corner** (at z = 6 it starts at x ≈ 336 and reaches the terrace). The pad's
   top was 0,345 m, i.e. **25 mm proud of that stone over its entire area**, so every edge of it — the
   v0.9.45 tongue, the v0.9.47 north ramp, and the flat top itself — read as a thin concrete plate with a
   visible 2,5 cm face and a shadow gap underneath.
2. **The pad now asks the scene what is under every vertex.** `CornerPad()` raycasts down at each grid
   vertex (plus the four normal samples per quad) and, where the hit is the granite shelf
   (`0,25 < y < 0,80`), clamps the surface to **stone + 10 mm**. Everywhere it stands on our own deck the
   full 0,345 m top and both bevels are unchanged, so the corner still reads as one smooth concrete
   surface — the platform the user asked to keep.
3. **The tongue is gone for good** (no `ZN`/`HN`, no second region, the platform-edge skirt runs the whole
   width): nothing of the pad extends north of `z = Z0 = 0,15`. Combined with the clamp, the seam is a
   **hairline** — you can walk from the crest's granite onto the platform without a step, and there is
   nothing to climb onto.
4. Evidence, one map-shot run: `[DISTRICT] corner pad: … v0.9.50: the inland tongue is gone and over the
   level's granite shelf the surface is clamped 10 mm above the stone, so its edge is a hairline instead of
   a ledge (544 quads)`; `[PROBE] corner pad (its top, over our own deck): 0,35 m (corner_pad)`;
   `[PROBE] corner pad over the granite shelf (10 mm above the stone, was 25): 0,33 m (corner_pad)`;
   `[PROBE] corner steps: 0 lip(s) of 0,20-1,60 m …`; `[PROBE] done: 44 ok, 0 misses`. The `corner map`
   rows that used to read `32L` (the stone on top) across x 338-368 at z 2-10 now read `33p` — our concrete,
   a centimetre above the stone.
5. **Shots**: `district_79` (the video's own framing — standing on the granite a metre south of the seam,
   the concrete and the stone meet with no lip and no gap) and `district_80` (5,5 m above the joint: the
   pad's surface, the hairline boundary and the terrace's wall); `district_78` (aerial) and `district_57`
   (walking in along the crest) are unchanged in look.
6. `[District] CornerPad = true` stays the shipped look (`false` still gives the bare deck).

### v0.9.47: the corner is reached by a **6 m ramp**, not a 0,345 m step (the user's before/after)

The user sent two stills: «как должно быть» (the corner runs **flat** from the deck to the sea) and «как это
выглядит сейчас» (a raised plate with a **step up**). The step was my own v0.9.46: with the inland tongue
gone, the pad ended in a bare 0,345 m vertical face at z = 0,15 — above the controller's 0,30 m stepOffset.

1. **A north ramp** `DN = 6 m` was added to the pad's height field: `height = HW + (north − HW)·(1−wx)·(1−wz_south)·(1−wz_north)`,
   so the surface rises out of the deck over six metres and meets the crest's granite at 0,345 — you **walk**
   onto the corner instead of stepping up. The z-grid is refined to 0,67 m rows so the ramp is smooth
   (850 quads).
2. **Nothing of the pad lies on the granite** (v0.9.46): the tongue stays removed, so the stone is untouched
   and the only thing at the seam is the pad's 2,5 cm lip at 0,345 against the granite's 0,32.
3. `CornerPad` is **true** again (both the default and the cfg): the pad with the ramp is the shipped look;
   `false` still gives the bare deck.
4. Log: `[DISTRICT] corner pad: … a 6 m **ramp to the north** (v0.9.47: you walk onto the pad from the deck
   instead of stepping up 0,345 m), and the inland tongue is gone so nothing of it lies on the crest's
   granite (850 quads)`; `[PROBE] … corner pad (its top): 0,35 m (corner_pad)`, `[PROBE] done: 43 ok,
   0 misses`.
5. Shots: `district_65` (the corner from the deck — one continuous surface up to the sea parapet, the
   user's «как должно быть» framing) and `district_78` (aerial: the granite/deck boundary).

### v0.9.46: only the part of the corner pad that lay **on the crest's granite** is gone

The user: «я УВЕРЕН что это часть платформы… изначально её не было. Всю её убирать не нужно, только
выделенную часть» — correct: it is the `corner_pad`'s **inland tongue**.

1. **What the tongue was**: to hide the 0,32 m step between the deck (0 m) and the crest's granite, the pad
   used to extend **1,6 m inland** (`ZN = −1,6`) east of `XG = 332`, sloping from the flat top 0,345 down to
   0,30 — so it lay *on the granite* and its western end face (a 0,94 m skirt at `x = 332`) stuck up beside
   it. That is the ledge in the user's photos.
2. **Now the pad ends at the platform's own edge** (`z = Z0 = 0,15`): the tongue surface, its west end face
   and its north face are not emitted, and the platform-edge skirt (down to −0,6) now runs along the whole
   width instead. The granite is the surface you walk in on, untouched, and the pad keeps its flat top
   (0,345), its 16 m west bevel and its 4,5 m south bevel — so the corner is still one smooth surface on the
   deck side (`Log`: `v0.9.46: the inland tongue is gone, so the pad ends at the platform's own edge z=0,15
   and nothing of it lies on the crest's granite (544 quads)`).
3. `[PROBE] south-east corner pad (its top): 0,35 m (corner_pad)`, `[PROBE] done: 43 ok, 0 misses`.
4. Shots: `district_78` (top view of the corner: granite / concrete boundary now a clean line, nothing of
   ours on the stone) and `district_65` (the corner from the deck: no concrete ledge over the granite).

### v0.9.45: the corner plate is **back** (v0.9.44 removed the wrong object)

The user: «Откати изменения за последний промпт, ты удалил не то» — the low plate at the south-east corner
was **not** what they meant.

1. `[District] CornerPad` is **true** again (and the value in the shipped cfg was re-written): the 48 x 12 m
   plate bridges the deck up to the crest's granite as before v0.9.44; `[PROBE] … corner pad (its top):
   0,35 m (corner_pad)` is back. `false` still turns it off if ever needed.
2. `[District] CornerPad` (v0.9.44) and the 0,78 m floor plates (v0.9.43) are otherwise unchanged.
3. **The object the user circled in their aerial screenshot is *not ours*.** Two scene scans (map-shot only)
   prove it: `[SEAM]` lists every **level** renderer in the crest band — only the game's two giant blockout
   `Cube`s (`Cube (9)`, `Cube (10)`, 24 verts each, bounds hundreds of metres) are there — and
   `[SEAM-OURS]` lists ours (`corner_pad`, `stairs_stair_plinth`, the four crest-row blocks). A small dark
   rectangle lying on the dam's granite at the corner is therefore part of the **level's own crest mesh**
   (the dam's drain opening / blockout edge), not a separate object: it cannot be deleted without deleting
   the dam itself.
4. If it must disappear visually, the only options are (a) cover it with a small concrete patch at crest
   level (one extra box, needs its position) or (b) hide the whole crest mesh (not acceptable — that is the
   surface you walk in on). Waiting for a close-up screenshot to pick.
5. A new map shot frames that corner from 40 m up (`district_78`, ortho 42) to compare with the user's
   aerial photo.

### v0.9.44: the low plate at the south-east corner is **removed** *(reverted in v0.9.45)*

The user: «убери эту... тёмную постройку, на краю района Сталин» — after a scene scan it is the **48 x 12 m
plate (`corner_pad`)** at the south-east corner: the only low structure of ours in the crest band
(`[SEAM-OURS] corner_pad: local (346.0, −0.1, 7.6), y −0.6..0.3, z −34..49`), the one v0.9.20-23 built to
bridge the deck up to the crest's granite.

1. `CornerPad()` is now gated by `[District] CornerPad` (**default `false`**) — the corner is the plain
   base deck again, and the granite crest's own 0,32 m ridge is what you step off when you walk in. Setting
   the knob to `true` brings the plate back unchanged.
2. `[PROBE] south-east corner pad (its top; the granite under it is 0,32)` now answers
   **0,32 m (SM_Scene_A_Blockout00.011)** — i.e. you stand on the crest's own stone, our plate is gone;
   `[PROBE] done: 43 ok, 0 misses`.
3. Diagnostics added while locating it (map-shot only): `[SEAM]` lists the level's renderers near the crest
   line (only the game's two giant blockout `Cube`s are there) and `[SEAM-OURS]` lists **our** meshes in the
   crest band (`corner_pad`, `stairs_stair_plinth`, the four crest-row blocks) — that is how the plate was
   named without guessing.
4. Unchanged: the floor plates at **0,78 m** (v0.9.43), the well/railings, `[STAIR] … least clearance
   4,95 m`, `[SKIN] 0 passed through`, `[DOOR] 0 open`, `built 36 buildings, 0 skipped plots, 22 bridges`.
   Shots `district_65`/`district_66` show the corner without the plate.

### v0.9.43: the floor plates are **0,78 m** (3x the old 0,26), growing downwards

The user: «ты верно определил проблемную плиту — теперь увеличь её толщину вниз в 3 раза».

1. `SLAB` 0,26 → **0,78 m** — the floor plate keeps its **top** face (the walking level) and grows *downwards*,
   so every storey's clear height shrinks by 0,52 m (4,82 m under a 5,6 m storey — the stair flights
   themselves are unaffected: their headroom is set by the flight above, `[STAIR] … least clearance 4,95 m`).
2. The plate's **end face over the stair shaft** — the one the user circled — is now 0,78 m tall and is the
   face that closes the foot of a flight (the flight plates are 0,65 m and now end *inside* the floor
   plate, so their own end faces are no longer drawn at all: nothing can coincide or gap there).
3. `StairPlate` is now an **independent** constant (0,65 m) instead of `SLAB × 2,5`, so thickening the floor
   plates does not inflate the flights/landings as well.
4. The street cores' intermediate landing slabs (`Legs`) use `SLAB` too, so the pylon houses stay flush.
5. **Verified**: `[SKIN] stair undersides: 561 vertical rays under the flights, 0 passed through, 0 hit the
   wrong surface`, `[DOOR] door heads: 4 hit the head, 0 open`, `[STAIR] headroom … 0 under 2,05 m; least
   clearance 4,95 m`, `[PROBE] done: 43 ok, 0 misses`, `built 36 buildings, 0 skipped plots, 22 bridges`.
   Shots: `district_78` (foot of a flight from the landing — the 0,78 m plate edge now stands under the
   floor) and `district_79` (that end face head-on from inside the well).

### v0.9.42: the floor plate's **end face over the shaft** was drawn inside-out (the "невидимый торец")

The user's fifth screenshot circled the edge of the **floor plate** at the foot of a flight: the plate's
end face over the stair shaft was invisible, so you looked straight through its 0,26 m thickness into the
storey below.

1. **Root cause — two of the hole rim's four quads had their normal the wrong way round.** `Slab` cuts the
   shaft out of a floor plate and closes the hole's rim with four quads: the two **x-edges** faced into the
   hole (`right` / `left`, correct) but the two **z-edges** were given `back` / `forward`, i.e. they faced
   *into the slab* instead of *into the hole*. `Geo.Quad` flips the winding to match the normal it is given,
   so those two faces were rendered back-to-front and culled from the only side you can see them from — the
   stairwell. Every floor plate in the district therefore had a see-through end at the shaft (and so did
   the pylon houses' core slabs, which use the same `Slab`).
2. **Fixed**: the z-edges now face into the hole (`forward` at `z = h.y`, `back` at `z = h.w`), matching
   the x-edges. The 0,26 m concrete end face is drawn where the flight meets the floor, which is exactly
   the spot in the user's circle.
3. **Evidence**: `district_79` is a head-on view of that end face from inside the well — the 0,26 m band is
   now a drawn, lit vertical face (before this it was a hole and the frame showed the storey below);
   `district_78` is the user's own framing (the foot of a flight from the arrival landing).
4. Also confirmed in this round (unchanged): `[SKIN] stair undersides: 561 vertical rays under the
   flights, 0 passed through, 0 hit the wrong surface`, `[DOOR] door heads: 4 rays just under a lintel, 4
   hit the head, 0 open`, `[STAIR] headroom … 0 under 2,05 m; least clearance 4,95 m`,
   `[PROBE] done: 43 ok, 0 misses`.
5. **Known cosmetic**: the plate undersides are lit only by the level's probe SH (no sun, ambient 0), so
   they read dark olive, and at grazing angles the 2,9 m grain tile shows a fine moiré — `district_79`
   shows both. That is shading, not a hole.

### v0.9.41: the see-through was in the **walls** — every doorway's lintel was an open slot

The user's fourth screenshot («прозрачная текстура всё там же») finally pinned it: the thin dark strip is
visible **over a doorway** in the background, and the same strip appeared in the video in front of a lit
doorway.

1. **The bug: `Piece` had no head and no sill.** Interior walls (corridor walls, cross partitions, the
   stair halls' walls, the core shafts) are built from `Piece` segments: two faces at ±0.19 m plus the two
   **jambs**. The top and bottom of a segment were never drawn, so **every lintel was an open slot through
   the whole wall thickness**: looking up at any doorway you saw straight through the wall into the room
   beyond (its ceiling appearing as a thin dark strip). That is the "прозрачная текстура", and it is why
   three rounds of stair geometry never helped — the stairs were fine all along.
2. **Fixed**: `Piece` now closes the segment's **head and sill** as well (`v0.9.41`), so a lintel is a
   solid concrete band. Cost: two extra quads per wall segment (~70 k across the district).
3. **The stairs were measured and are watertight.** New `[SKIN]` probe: a vertical ray from 28 cm above
   the floor under **both flights of every house** (10 stations each) must hit that flight's soffit —
   `[SKIN] stair undersides: 561 vertical rays under the flights, 0 passed through, 0 hit the wrong
   surface`. (The first version of this probe reported "holes" — it used flight 1's slope for flight 2
   and probed the pylon houses below their first floor; both fixed. Lesson: compute each flight's own
   profile.)
4. **New `[DOOR]` probe**: a ray from just under a lintel must hit the head —
   `[DOOR] door heads: 4 rays just under a lintel, 4 hit the head, 0 open`.
5. **Unchanged**: `StairPlate = 0,65 m`, the well + railings, `[STAIR] headroom … 0 under 2,05 m; least
   clearance 4,95 m`, `[PROBE] done: 43 ok, 0 misses`, `built 36 buildings, 0 skipped plots, 22 bridges`.
6. **Shots**: `district_26` (a corridor — the door heads are solid, the dark wedges at the top of a
   doorway are the *undersides* of the lintels, not holes), `district_70`/`71`/`76`/`77` as before and
   `district_78` = the user's own framing from the half-landing.

### v0.9.39: every flight is a chain of **closed cells** — no hollow shell, no coincident faces

The user's video (25 s inside a house stairwell) showed two artefacts: plate ends that read as razor-thin
"transparent" sheets, and dithered/checkered patches on the flat surfaces.
1. **The transparent ends were the hollow shell.** A flight was a shell (treads, risers, two sides, one
   soffit quad, two end faces, each 1 cm *recessed* to dodge z-fighting). From outside it looked solid, but
   the recess left a 1 cm ring slot at both ends and **nothing inside** the shell: any view through a slot
   (or round an end) reached the empty interior — the "прозрачные торцы".
2. **Now each tread is its own closed solid cell** (bottom = that tread's soffit strip, top = the tread,
   front = the riser, back = the plane under the next riser, both sides). The cells tile the plate exactly,
   so the union is watertight in every direction — there is no interior to see into, and the soffit is
   still the same smooth plane (its strips are coplanar and share edges, which cannot z-fight).
3. **No coincident same-normal faces** (the dithered patches): the two end faces are now drawn **only
   where the neighbouring solid does not already close the plate** (`backLow` / `backHigh` = that solid's
   thickness): the nose at the floor shows its 0,39 m below the floor slab's shaft rim, the top of flight B
   shows its 0,39 m below the floor slab, and where a flight meets a 0,65 m landing **no face at all** is
   drawn (the landing's own face closes it, and back-to-back faces with opposite normals cannot fight).
   The 1 cm recesses are gone.
4. **The core stairs and the clock tower** (`AtticStair`) got the same treatment: its two flights pass the
   backing thicknesses of their own floor/landings, so the pylon houses are watertight too.
5. **Unchanged**: `StairPlate = 0,65 m` (§v0.9.38), the well and its railings, the plan (16 risers of
   0,175 m, 3 m runs), `[STAIR] headroom: 33 blocks, 1584 rays, 0 under 2,05 m; least clearance 4,95 m`,
   `[PROBE] done: 43 ok, 0 misses`.
6. **Shots**: `district_70` (the stairwell from the doorway — the landing above now shows a 0,65 m edge),
   `district_76` (straight up at a flight's underside: a closed soffit with its thick edge in frame),
   `district_71` (down the well), `district_77` (a flight from the storey above).

### v0.9.38: the stair plates are 2,5x thicker, parallel soffit, closed ends

The user: «Плиты лестниц слишком тонкие (скрин 1) — сделай их в 2,5 раз толще. Также прозрачные текстуры
на горизонтальных плитах (скрин 2) остались.»

1. **Thickness**: `StairPlate = SLAB × 2,5` = **0,65 m** (the flight's vertical plate and its landings'
   slabs alike, so a flight and the landing it meets stay flush). Was 0,26 m.
2. **Parallel soffit**: the underside now runs **parallel to the flight** at that constant thickness (the
   v0.9.37 one tapered from the floor to the landing, which read as a wedge). At the bottom it therefore
   starts 0,65 m below the floor — the classic concrete stair "nose" hanging into the well, exactly as in
   the user's reference photos.
3. **Closed ends** (the remaining “transparent” edges): the plate now has **vertical end faces** at both
   ends — the nose under the first step and the cut at the top. Both sit 1 cm *inside* the flight so they
   can never z-fight with the landing or the floor slab; the top one matters because flight B's 0,65 m
   plate is thicker than the 0,26 m floor slab it lands under (that 0,39 m slot used to be open — you
   could see into the stairwell through the flight's end).
4. **Everything in the stairwell is a closed volume now**: flight = soffit + treads + risers + thin side
   bands + two end faces; landings = boxes; the floor slabs' shaft rim is closed by `Slab`. The dark
   olive surfaces the user photographed are the **undersides** of those plates (this level has no sun and
   ambient 0 — every ceiling faces down into shadow), not holes: with the 0,65 m edges they now read as
   thick concrete.
5. **Measured**: `[STAIR] headroom: 33 blocks, 1584 rays, 0 under 2,05 m; least clearance 4,95 m` — the
   soffit above a flight is now 0,65 m below its treads, hence 5,6 − 0,65 = 4,95. `[PROBE] done: 43 ok,
   0 misses`.
6. **Shots**: `district_70` = the whole stairwell from the doorway (both flights, the well, the landings —
   the plates read as 0,65 m concrete), `district_71` down the well from the top floor, `district_76` =
   straight up at a flight's underside (closed, with its thick edge), `district_77` = a flight from the
   storey above.

### v0.9.37: the flights are **plates** now (closed, straight soffit) — no more triangular prisms or see-through stairs

The user: «сами лестницы слишком громоздские треугольники, а должны быть плитами. Также, сами плиты и
лестницы имеют прозрачные текстуры, это нужно убрать» (with two stills from our build and two reference
photos of real concrete stairs).

1. **Why they read as triangles and looked transparent.** `FlightZ` drew the *side* faces of a flight as
   quads from `yBase` (the storey floor) all the way up to each tread — so the silhouette under the steps
   was one big filled triangle — while the *soffit* was a separate quad cutting diagonally *through* that
   solid. There was therefore **no bottom face along the floor line**, so from below the whole flight was
   open: you looked straight through it into the stairwell (the “transparent textures”). The half-landing
   was a `Slab`, i.e. its top and bottom only, so its exposed edge over the well was open too.
2. **Now every flight is a plate** (`FlightZ`): the soffit runs **from the floor at the low end to the
   underside of the landing** at the high end (a 0.26 m vertical plate, `SLAB`), the treads and risers sit
   on top, and the sides are only the thin band between the soffit and the treads — so the profile is the
   reference's thin slab with a straight under-line, and the body is watertight (no see-through from any
   angle).
3. **Landings are closed boxes** (`Landing`, 0.26 m thick) instead of open `Slab`s, so their edge over the
   well is a real concrete edge.
4. **The core stairs and the clock tower use the same code**: `AtticStair` was rebuilt from its per-step
   boxes (a solid triangular mass from the storey floor — exactly the “bulky” look) to two `FlightZ`
   plates + a `Landing`, keeping its plan, its 12+12 steps and its exit landing.
5. **Measured**: `[STAIR] headroom: 33 blocks, 1584 rays, 0 under 2,05 m; least clearance 5,34 m` (the
   plate's 0.26 m thickness is what changed the 5.40 to 5.34). `[PROBE] done: 43 ok, 0 misses`.
6. **Shots**: `district_71` (from the top floor down the well: the flights now read as thin plates with
   their own soffit), `district_74`/`district_75` (a flight from the landing), `district_76` (straight up
   at a flight's underside from the floor — a closed dark soffit, nothing behind it) and `district_77`
   (looking down a flight from the storey above, the reference photos' angle).

### v0.9.36: the point towers' stairs were capped by the facade **bands** — the real «лестницы упираются в перекрытия»

The user's screenshot (a flight that ends flush in a slab, with the v0.9.35 well railings visible) plus
«задание не выполнено» meant v0.9.35 had fixed the *wrong* stair. The measurement then named the culprit:

1. **`Headroom` now measures the real clearance**, not a 0,2 m sniff: a 5,6 m ray from 1,84 m above every
   eighth tread of both flights of the first three storeys, logging the collider and the distance. On
   v0.9.35 it reported `30 rays under 2,05 m` and `least clearance 1,86 m` — and **every** one of those
   rays was in a block with `bands = true`.
2. **The cause: the facade "band" was a solid box over the whole footprint.** `BuildBuilding` drew the
   point towers' floor bands as `g.Box(0, (0, y−0,12, 0), (b.w+0,35, 0,24, b.d+0,35))` — a 0,24 m plate
   covering the **entire plan at every storey**, so it also capped the stairwell hole (and every room). A
   flight tops out exactly at the floor level, so its last step met that plate and the player could not
   step off the stairs; 1,86 m was the measured gap at tread 6/8 of flight 2. That is why it hit **only
   some** houses: the point towers (`Kind()` case 2 — which also covers the two blocks on legs, the
   memorial tower and the clock tower). The slab / khrushchovka / stalinka blocks were always fine.
3. **Fixed as a ring**: four 0,35 m wide boxes at ±`hw` / ±`hd`, 0,24 m tall, centred on the wall plane.
   The facade still gets its band (0,175 m proud on each side, the corners overlap), the interior is no
   longer touched.
4. **Measured after the fix**: `[STAIR] headroom: 33 blocks, 1584 rays, 0 under 2,05 m; least clearance
   5,40 m` — the least clearance anywhere in the district is now 5,40 m (the soffit of the flight
   above), i.e. every flight has a full storey of nothing above it.
5. Shots (the first `bands` block, a point tower), `district_70` the foot of a flight — the dark plate
   that used to cap the view is gone, the flight, the well and the handrail are open to the storey
   above; `district_71` down the well from the top floor; `district_72` straight up the slot from the
   ground floor; `district_73` down the well from the third-floor landing. The legs-core shots
   `district_74`–`district_76` are the v0.9.35 ones.

### v0.9.35: a real stair **well** through the whole house, and the street core that reaches the first floor

Two user reports on the district's staircases: «в некоторых домах пролеты упираются в потолочные
перекрытия, ими невозможно пользоватся» (screenshot 1) and «между лесенками должны быть проемы
(вертикальная щель между пирилами, проходящая через весь дом сверху вниз)» (screenshot 2).

1. **A second, independent blockage was the street cores of the two blocks on legs** (`Legs`,
   v0.9.19/20) — found while chasing the reported one. Each core
   has three `AtticStair` flights of 5,6 m that climb to the block's first floor — but the block's **ground
   floor slab is solid** (`Interior` cuts the stair shaft for `f > 0` only, which is correct for the houses:
   their `f = 0` *is* the street floor). The last flight therefore ended 0,26 m **inside** the slab: the two
   houses on pylons could be looked at from below but not entered. The new probe reports it directly —
   `[PROBE] legs core stair under the first floor at z 0,3 / 1,7: OPEN through the first-floor opening
   (1,2 m)` for **both** blocks (before the fix the same ray hit the slab 0,06 m up).
2. **One plan for the stair and its hole** (`PlanCore` — the same discipline `PlanStair` brought in
   v0.9.16): lane x −1,90..−0,20 m, exit landing z 2,71..4,21 m, slab opening z −0,29..4,21 m, logged once
   per block as `[CORE] 41,4x28,0 m block on legs: street stairlane x -1,90..-0,20 m, exit landing z
   2,71..4,21 m, first-floor opening z -0,29..4,21 m (tops out in the stair hall, 3 flights of 5,6 m)`.
   `Interior` cuts that opening in the `f = 0` slab and, because the exit landing runs **under the corridor
   wall**, also cuts a 1,7 m doorway in that wall at the west end of the stair hall. `Legs` builds the stair
   **mirrored** (`AtticStair` gained `zSign = −1`), so it now climbs **northwards to its exit** and tops out
   inside the house's stair hall, exactly at the foot of the house's own first flight: street → core →
   house staircase is one continuous climb. The opening gets the same metal railings as the wells.
3. **The well** (the user's second screenshot): the two flights used to be glued together with a 0,1 m seam.
   They are now separated by a real open well, `well = clamp(segLen · 0,15, 0,55…0,75)` → **0,66-0,73 m**
   here (flights 1,87-2,06 m), logged as `[STAIR] … well 0,70 m (flights 1,98 m), shaft z 5,70..11,10`. The
   well lies inside the slab hole, which already spans the whole stair hall, so the slot is **open from the
   ground floor to the roof** with nothing filling it at any storey; both ends are guarded at every level
   (a rail across the well at the arrival floor and one at the half-landing), so nobody can walk into the
   shaft.
4. **The railings** are the district's own rusty metal (`M_MetalRusty00`, submesh 1 — the material the
   penthouse's mansard already used, so no third material was needed): a 1,0 m handrail + mid rail + three
   posts per flight, the posts snapped **onto the treads** (`RailRun`), plus the two well guards. Every
   house and both House-of-Soviets towers get them (they share `StairsZ`), and so do the legs cores. The
   layout is untouched: `built 36 buildings, 0 skipped plots, 22 bridges`.
5. **Headroom re-measured**: `[STAIR] headroom: 33 blocks, 1320 rays 1,84 m above the treads, 0 blocked`.
   1320 rays instead of 1056 because the probe now also samples the **top** of each flight (the tread that
   meets the next floor) and, for a block on legs, starts at the block's own first floor instead of probing
   the empty air under the house (that used to report the transfer girder as a "blocked stair" — a probe
   artifact, now gone).
6. **Shots**: `district_70` = the user's own frame 1 (the foot of a flight: the treads, the rusty handrail,
   the soffit and the stepped side of the flight above — nothing blocks it), `district_71` = the well from
   the top floor, straight down the shaft, `district_72` = straight **up** the slot from inside it on the
   ground floor (several storeys of flights and railings, open to the roof), `district_73` = the legs core's
   top half-landing looking up its last flight and **through the new opening** in the first-floor slab
   (the frame that used to end in concrete), `district_74`/`district_75` = the core's first-floor opening
   from the corridor and along it, with its railings.
7. **Known, not touched**: the penthouse atrium stair (v0.9.18) still stops one storey short — its three
   flights reach `y0 + 16,8 m` while the clock storey's floor is at `y0 + 21,4 m`, and the atrium void above
   it is open (so you arrive on a landing in mid-air 4,6 m under the next gallery). The user's report was
   about the ordinary houses' stairwells; a fix needs a fourth flight (the clock storey is 10,2 m tall) and
   is left for a round of its own.

### v0.9.34: solid collars, a greatcoat collar, pointed shoes and real trainers

The user: «Увеличь воротник офицеру в 2,5 раз в высоту и в 1,5 в толщину. Также сделай все воротники не
прозрачными с внутренней стороны. Ещё сделай Прапору остроносые туфли, а Докеру спортивные кроссовки (не
прямоугольные)».

1. **Every collar is a closed volume now.** They were a single `B.grid` surface — a sheet of rings — so
   from inside/behind you looked straight through them. Each collar is built as **outer wall → top rim →
   inner wall → bottom rim → back to the start**, i.e. a solid band with a real wall thickness
   (`th = 0,006·H` ≈ 1,6 cm, clamped so the inner wall can never punch through the neck).
2. **The officer's great-coat collar**: 2,5× taller — the rows now run from the yoke (0,692·H) up to
   **0,792·H**, i.e. ~18,5 cm of upstanding collar that wraps the jaw (the old one was 7 cm and stopped
   at `chin − 0,022·H`) — and 1,5× thicker (`th = 0,009·H`, 2,4 cm). The top ring stays outside the head
   (radius `nr·1,85` against the skull's `0,053·H` at that height), so it wraps the jaw instead of
   intersecting it.
3. **The officer wears pointed shoes** (`shoe="pointy"`, the tall `boot_h = 0.36` is gone — he keeps his
   dark trousers): a long, low shoe whose tube tapers from `0,27·hipx` at the ball to **0,04·hipx** at the
   tip (a real point), on a **thin dark sole** and a small heel.
4. **The docker's trainers are trainers, not boxes.** The shoe was a `B.box` — a brick. `sneaker` (and
   `pointy`) are now built as a **tube along the foot's own axis** (heel → arch → ball → toe, an elliptical
   section per ring, capped at both ends) with a matching **white midsole tube** underneath, so the trainer
   has a rounded toe and a proper side profile.
5. **Evidence, one `MapShot = true` run**: `BabbdiCoop v0.9.34 loaded`, `[FIGURE] figure_4_officer … 2,74 m`,
   `[PROBE] done: 43 ok, 0 misses`, `Figures: 5 GLB`. Shots: `district_69` (the tall collar up to the jaw,
   the pointed shoes), `district_68` (the rounded trainers), `61`/`64`/`67` (the other collars, now solid).
   Offline: `zz_neck.png` (four necks/collars), `zz_shoes_officer.png`, `zz_shoes_docker_official.png`.

### v0.9.33: the neck joins the head, the collars join the clothes, a real peaked cap

The user: «у них всех что-то с шеей. Она не соединяется нормально с головой и идет не под тем углом.
Также на шее воротники не присоединены с одеждой, исправь. И увеличь козырек Офицеру раза в 4».

1. **Why the neck looked broken.** In v0.9.30 its top ring was placed `0,22·hrz` (4,4 cm) **behind** the
   head's centre to keep it inside the skull — so in profile the neck ran up *behind* the head and in front
   it ended in a hard horizontal cut under the jaw. It now ends at `chin + 0,11·head_h` (11 % of the head's
   height above the chin, i.e. well inside the skull) and is centred at `head_z·0,85`, so it **follows the
   head's own forward lean**, and the jaw grows out of it. Radii `0,158 → 0,140 → 0,120 · head_h · neck_w`.
2. **Why the collars floated.** They were rings of radius `head_h·0,135` hung at `chin − 0,03·H`: that is
   **smaller than the neck they were supposed to wrap** (0,150·head_h) — they sank into it — and they sat
   3,7 cm **above** the shoulder line, so there was a bare strip of trapezius between the collar and the
   jacket. Every collar is now a **cone that starts at the shoulder yoke with the jacket's own radius**
   (0,694·H, +3 % so it cannot z-fight), tapers over the shoulders and rises to just under the jaw
   (`great` flares to ×1,72 and folds back, `stand` to ×1,52, `flat` lies at ×1,55).
3. **The vest ends at the collarbone** (0,712·H) instead of climbing the neck as a closed cone — that cone
   read as a turtleneck and swallowed the collar's base.
4. **The officer's peak is real now**: 6 cm of forward reach → **15 cm** (2,5× linear, ≈6× the area), the arc
   is 12° wider and it tips further down. Two traps fixed on the way: the plate was a **zero-thickness
   sheet** (the underside was the same faces as the top, so the bigger peak became a black wedge across the
   forehead — it now has a real 6 mm underside), and the underside colour went 0,235 → 0,330 so it reads as
   cloth, not a hole.
5. **Evidence, one `MapShot = true` run**: `BabbdiCoop v0.9.33 loaded`, `[PROBE] done: 43 ok, 0 misses`,
   `Figures: 5 GLB`. Shots: `district_61` (the official — the neck runs into the jaw, the stand collar grows
   out of the jacket), `district_64` (the soldier), `district_68` (the docker — the vest stops at the
   collarbone, the collar above it), `district_69` (the officer). Offline `zz_neck.png` renders the four
   necks at two angles, `officer_side.png` shows the new peak in profile.

### v0.9.32: a torso with a chest, a waist and a gut — and two mouth widths

The user: «Докеру и Прапору нужно переделать губы: сделай их больше, но сильно более блеклыми (чтобы у
одного из них был рот шире другого). А ещё поправь анатомию тела: после увеличения модели туловища,
Докер и Солдат будто одеты в бочку. Сделай разделение на грудь, живот, плечи и т.д.»

1. **Why they were barrels.** The old `torso_prof` ran `rx 0,94 → 1,04 → 0,32`: a ±5 % variation over the
   whole body, i.e. a tube. Once the figures grew 20 % that read as a barrel with arms glued on. There is
   now a `DEFAULT_TORSO` profile with real anatomy — hips 0,95, **waist 0,92 (narrowest)**, ribcage 1,005,
   **chest 1,045**, **shoulder yoke 1,075** — plus two per-figure overrides:
   - **docker**: a gut. The belly is the widest ring (`mx 1,155`, `mz 1,19`) at **51 % of the height**,
     pushed 2,4 cm forward (`zo 0,024`), with a **tucked waist above it** (`mx 0,94` at 62 %) and a narrower
     chest — a pear, not a cylinder;
   - **soldier**: a V. `mx 1,07` at the chest and **1,11** at the yoke against **0,90** at the waist.
2. **The vest follows the body** (it used to be its own straight tube, `hipx*(1,07-0,03t)`): seven rings
   sampled from `prof_at()`, +7,5 % in x and +5,5 % in z, plus a **hem lip turned inwards** so the open
   tube cannot show its dark inside along the bottom edge.
3. **The arms hang from the shoulder's edge.** `shx` was a fixed `0,094·H`, which sat 2-3 cm *inside* a
   chest that is 0,105·H wide, so the sleeves sprouted from the ribs; it is now derived from the profile's
   yoke (`prof_at(0.700)·0,96`) and `shoulders` is a fine trim on top of that. The joint stays at `0,88·shx`
   (half-buried deltoid), the elbow/wrist at `1,02/0,98` — a relaxed A-pose instead of a flare to the elbow.
4. **Two mouths for the docker and the officer**: bigger but much paler — `lip_h` **1,25** (docker) and
   **1,05** (officer), both at `lip_amt 0,16` (the four men were at 0,26). And the widths differ on
   purpose: the docker's `mouth_w` **0,34** (a wide mouth on a big face), the officer's **0,21** — 1,6×
   narrower, so the two read as different people at a glance.
5. **`render3.py` fixed**: the body view names were ignored (`render(...)` was called without a yaw), so
   the "front / 3-4 / back / side" columns of every sheet were four identical shots. They now use
   `VIEWS[v]["yaw"]`.
6. **Evidence, one `MapShot = true` run**: `BabbdiCoop v0.9.32 loaded`, `[PROBE] done: 43 ok, 0 misses`,
   `Figures: 5 GLB`, heights unchanged (2,04 / 2,23 / 2,50 / 2,72 / 2,35 m). Shots: `district_68` (the
   docker — a chest, a gut and a tucked waist under the vest), `district_64` (the soldier — the V), `61`
   and `69` (the two mouths: wide on the docker, narrow on the officer, both faded).

### v0.9.31: the men's mouths are folds, not lipstick

The user on v0.9.30's lips: «С губами вообще не то, они теперь все выглядят как накрашенные… (Учёной идёт,
остальным нет). Сделай чтобы было менее мультяшно, как на скрине (всем кроме Учёной)» — the reference is
the pale old man whose mouth is just a slightly darker fold of the same skin, a soft seam and a shadow.

1. **No painted lip colour for the four men.** `lips` is now read only if a figure defines it (the
   scientist — she keeps `lips 0,58/0,33/0,32` at `lip_amt 0,64`). Everybody else gets a **fold of the
   same skin**: `lipc = skin × (0,96, 0,86, 0,85)` at **26 %** blend, so the mouth is a touch darker and
   redder than the cheek, never a lipstick shape.
2. **Thinner lips**: `lip_h` 1,05 → **0,70** (docker), 0,92 → **0,60** (official), 0,86 → **0,56** (officer),
   1,02 → **0,80** (soldier). The **lit band** on the lower lip now only runs where `lip_amt ≥ 0,5`, i.e.
   the woman's.
3. **The seam is soft**: a 0,7 % band blended 45 % towards the dark mouth colour (it used to be a solid
   `mouth` block). A **shadow under the lower lip** (`×0,90`, 0,024 thick) is what actually reads at ten
   metres, exactly as on the reference.
4. **The official lost his teeth.** `teeth=True` drew a hard-edged rectangle of pale teeth in the atlas —
   the most cartoonish thing on the roof. His mouth is now closed under the moustache (his reference photo
   has a closed mouth), and the default teeth colour went 0,545 → 0,480 in case another figure ever uses it.
5. **Evidence**: `BabbdiCoop v0.9.31 loaded`, `[PROBE] done: 43 ok, 0 misses`, `Figures: 5 GLB`. Shots:
   `district_61` (the official — a closed, muted mouth under the moustache), `district_69` (the officer — a
   bare thin line), `district_64` (the soldier), `district_67` (the scientist keeps her rose mouth).

### v0.9.30: a real **neck**, real **lips**, and five skin tones

The user: «Добавь им шеи (в Бабди у НПС есть шеи, прикрепил пруф). А ещё губы персонажей должны быть не
линией, а более реалистичными, цвета губ (тоже см референсы). А ещё нужно чтобы у них был разный цвет кожи
(преимущественно бледные оттенки).»

1. **The neck.** The old build deliberately put the head straight on the collar («there is no neck to speak
   of»), which is wrong — the game's own people clearly have one. `landmarks()` is restructured: the
   **shoulder line stays at `0,722·H`** (so the torso, the arms and every landmark keep their place), a
   **`neck_len = 0,045·H`** (≈ 9,5 cm) neck is added above it, and the chin is derived from the neck's top
   (`chin = neck_top + 0,004·H`). The head is then **8 % smaller** (`0,229·H` ≈ 4,4 heads, was 0,252·H) to
   pay for the neck, so **the five keep their heights** — 2,04 / 2,23 / 2,50 / 2,72 / 2,35 m.
   The neck itself is a three-ring `tube` (`head_h·0,150 → 0,138 → 0,128` radii, so ~14 cm at the base),
   it **leans back as it rises** (the head is carried forward) and its top ring ends **inside the skull
   behind the chin** — a neck that stopped at the chin would flare out around the narrow end of the jaw.
   `neck_w` per figure: **0,88** (the scientist — a slim neck), 1,00 (official), 0,94 (officer),
   1,02 (soldier), **1,06** (docker — a thick one).
2. **Real lips, not a line.** `skin_at` now paints a **cupid's-bow upper lip** (thin, two soft peaks with a
   dip in the middle), a **fuller lower lip** with a **lit band** on it, and a **dark seam** between them;
   the old `mline` (a 1,3 % band of `mouth`) is gone. Each figure carries `lips` (a muted colour) and
   `lip_h` (thickness): scientist 0,58/0,33/0,32 @ 1,00, docker 0,52/0,33/0,30 @ 1,05, official
   0,50/0,27/0,25 @ 0,92, officer 0,47/0,29/0,28 @ 0,86, soldier 0,55/0,33/0,30 @ 1,02. The
   open-mouth/teeth block of the official draws **on top** of the lips, so his grimace is framed by them.
3. **Five different skin tones**, all pale and all distinct (they used to be five shades of the same warm
   beige): **scientist** pale pink-cool `0,790/0,706/0,668`, **docker** sallow tan `0,700/0,598/0,480`,
   **official** ruddy pale `0,800/0,668/0,598`, **officer** cold grey `0,726/0,706/0,672` (the white mask
   of the reference), **soldier** olive-yellow pale `0,722/0,652/0,512`. `glove` follows the skin on the
   four bare-handed figures; the officer keeps his white dress gloves.
4. **Evidence, one `MapShot = true` run**: `BabbdiCoop v0.9.30 loaded`, `[FIGURE] figure_N_<name>` lines
   with **2,04 / 2,23 / 2,50 / 2,72 / 2,35 m**, `[PROBE] done: 43 ok, 0 misses`, `Figures: 5 GLB`.
5. **Shots**: `district_61` (the official — a pale ruddy face, a neck above the dark collar, a red mouth
   under the moustache), `district_67` (the scientist — a slim neck, rose lips), `district_68` (the docker —
   a tan face and a thick neck), `district_69` (the officer — the cold grey-white mask and a red mouth),
   `district_59` (the row: five necks, five skin tones).

### v0.9.29: five different **sizes**, trainers and dress shoes

The user: «Добавь ещё больше разнообразия: сделай 1 больше на 20%, 1 выше на 15%, 1 выше на 10%, 1 выше
на 5% и толстым. Также сделай Докеру кроссовки, а Чиновнику туфли.»

1. **Five heights instead of five clones.** The figures now span **2,05 / 2,23 / 2,50 / 2,70 / 2,35 m**
   (was 2,05-2,25, a 10 % spread; now a **32 %** spread), applied as `h` in `FIGURES` — every landmark is
   a fraction of `h`, so the scale is uniform:
   - **officer 2,20 → 2,64** (+20 %, the tall one — built 2,70 m with the cap),
   - **official 2,16 → 2,484** (+15 %, built 2,50),
   - **soldier 2,12 → 2,332** (+10 %, built 2,35),
   - **docker 2,10 → 2,205** (+5 %) **and fat**: `hips 1,00 → 1,20`, `depth 1,02 → 1,24`,
     `shoulders 1,02 → 1,24` (the last one because at `hips 1,20` the sleeve's root fell inside the torso
     and the arm emerged from the belly at hip height),
   - **scientist** unchanged (2,05) — the small one.
2. **Per-figure framing in C#.** `SovietDistrict.FigHeight` records each figure's own height, read back
   from the loaded mesh (`MeshFilter.sharedMesh.bounds.size.y`), and every close-up camera/target is now
   `0,72 h` / `0,47 h` above the feet instead of a fixed 1,55 / 1,02 m — a fixed camera would shoot the
   2,70 m officer at the waist. The chest probe uses `0,63 h` for the same reason. The log line now says
   `2,05-2,70 m tall`.
3. **Trainers (docker)** — a new `shoe="sneaker"` branch in `build_body`: a low cuff (top `ankle+0,045·H`),
   a slightly wider foot box, a **thick pale midsole** (`sole` 0,011·H, the palette's `sole` is white
   0,830/0,820/0,795), a **white toe cap** and a **lace panel** in the same colour; the upper is pale canvas
   (0,645/0,630/0,595 instead of the old dark leather 0,185/0,154/0,119).
4. **Dress shoes (official)** — `shoe="dress"`: a low sleek shoe (top `ankle+0,012·H`), a **longer, narrower**
   foot (0,275 × 0,62 of the foot length against the boot's 0,305 × 0,56), a **thin sole** (0,006·H) and a
   **heel** block at the back; the palette is black leather (0,085/0,080/0,076) with a near-black sole.
   Boots stay the default (`shoe` absent) for the other three, so the officer keeps his tall boots.
5. **Evidence, one `MapShot = true` run**: `BabbdiCoop v0.9.29 loaded`, five
   `[FIGURE] figure_N_<name>: … 2,05 / 2,23 / 2,50 / 2,70 / 2,35 m tall` lines,
   `[PROBE] House roof figure 5 (chest, from the city side): 5,65 m (figure_5_soldier)`,
   `[PROBE] done: 43 ok, 0 misses`, `Figures: 5 GLB`.
6. **Shots**: `district_59` (the row from the plinth — the height spread is the point: the small woman on
   the left, the stocky docker, then the tall official and the towering officer), `district_68` (**white
   trainers** on the docker), `district_61` (**black dress shoes**, the official at 2,50 m), `district_69`
   (the 2,70 m officer, framed off his own height).

### v0.9.28: five different faces, soft skin, and the bob's artefacts

The user's fifth round: «У Терапевта какие-то артефакты в волосах. В целом, текстуры головы всем им нужно
смазать, как это уже у прочих персонажей Бабди и добавить ассиметрию легкую. Также, сейчас это выглядит
как один манекен в 5 разных нарядах: сделай каждого из них с разными чертами лица (рельеф головы). Как
референс, используй картинки выше которыми вдохновлены образы.»

1. **The bob's two artefacts** (the therapist's close-up): (a) a **pale rectangle** at the side of the head
   was the **ear**, which stuck out 2 cm (`0.995·hrx + 0.105·hrx`) through a bob whose surface was only
   7,5 % above the skull — a bob covers the ears, so `build_head` now **skips the ear entirely** when
   `hair == "bob"`; (b) a **pale notch at the crown** was the lumpy skull poking through the hair: the
   dome's crown clearance was 4,8 % (`sc1 = 1.048`) while the noise is ±4,5 %, and the pole vertex and the
   skull's top vertex get *different* noise values. The bob's scale is now `sc0 1.095 / sc1 1.075`, the
   short hair's `1.055 / 1.075`, the beanie's `sc·1.055 / sc·1.040`, and the skull's lumpiness is **damped
   by 80 % above `ny 0.45`** (`crown` in `deform`) so nothing can cross a hat or a hairline.
2. **The head textures are soft now**, like the game's own photo-painted faces: `soften_skin()` halves the
   **left (skin) half of the atlas** (x < 1024, i.e. head/nose/ear/neck/hand), scales it back up bicubically
   and applies a **1,15 px Gaussian**; the cloth half is untouched so stripes and seams stay crisp. The
   skin's own noise went from `amp 0.028 / freq 80` to **0.010 / 60**. Proof that the texture really is
   softer: the GLBs shrank from **1,64-1,95 MB to 0,87-1,14 MB** (PNG of a smooth image compresses far
   better), with the same 2 611-3 076 verts.
3. **Five different skulls**, from the reference portraits — `head_deform_factory` now reads a `face` dict
   per figure: `brow` (brow-ridge depth), `sock` (socket depth), `cheek` / `cheekw` (how far the cheekbones
   stand out / how far they widen the face), `jaww` (how much the jaw narrows), `chin`, `jowl` (the pouch
   beside the jaw), `fore` (a sloping forehead), `nose_len` / `nose_w` / `nose_x`. `head_hm` scales the
   whole head (a shorter, rounder head for the woman, a long one for the officer). The five:
   - **scientist** — round, wide, soft (brow 0,082, socket 0,092, cheek 0,092, **cheekw 0,098**, jaww 0,082,
     chin 0,052, `head_hm 0,97`, `head_w 1,02`), the **smallest nose** (0,84 × 0,92), thin arched brows
     (w 0,15, h 0,021, slope −0,14), the largest palest eyes (rx 0,145, ry 0,050);
   - **docker** — a long weathered face (**head_hm 1,02**), heavy grey brows (w 0,215, h 0,037), the
     widest jaw (jaww 0,072 with a narrow cheekw 0,068), a big nose (1,16 × 1,10), a drooping eye line
     (`eye_tilt 0,09`), heavier stubble (0,40), the darkest tan;
   - **official** — the heaviest head: brow **0,150**, sockets 0,135, cheeks 0,126, **jowls 0,055**, almost
     no jaw taper (jaww 0,052), brow w 0,245 / h 0,043, a big moustache (w 0,31, h 0,064, colour darkened
     to 0,33/0,30/0,27), narrow deep-set eyes (rx 0,124, tilt −0,09);
   - **officer** — the longest face (**head_hm 1,04**) with the deepest sockets (**0,148**), brow sloping
     **+0,24** into a permanent frown, a long thin nose (1,22 × 0,96), small eyes (rx 0,120);
   - **soldier** — the softest skull (brow 0,118, socket 0,106) with a wide young jaw (cheekw 0,082), dark
     thick brows (w 0,245, h 0,042, colour 0,10/0,075/0,055), a straight big nose (1,02 × 1,06).
4. **Every face is slightly asymmetric** — `asym` (0,10-0,17) makes one brow ridge 45 % heavier, one eye
   socket 55 % deeper, the opposite cheekbone 45 % flatter and one jaw 30 % wider; the painted features
   carry their own offsets (`fasym`: `eye_dx`, `eye_dy`, `eye_sz`, `brow_dy`, `mouth_dy`, `mouth_tilt`), and
   `nose_x` bends **both the nose ridge in the deform and the nose geometry** sideways. No two figures carry
   the same set.
5. **Evidence, one `MapShot = true` run**: `BabbdiCoop v0.9.28 loaded`, five `[FIGURE] figure_N_<name>`
   lines (2 611-3 076 verts), `[FIGURE] five figures … 5 of 5 loaded`, `[PROBE] done: 43 ok, 0 misses`,
   `Figures: 5 GLB`. GLBs 0,87-1,14 MB each.
6. **Shots**: `district_61` (the official — bald, heavy brow, white eyes, a big moustache, jowls),
   `district_64` (the soldier — dark thick brows, a beard), `district_67` (the scientist — no hair artefacts,
   a round soft face), `district_68` (the docker — grey brows, a long weathered face), `district_69` (the
   officer — the long deep-set face under the cap). Offline `zz_bob_back.png` renders the back of the bob
   from four angles: solid, no pale patch, no ear.
7. Defaults are unchanged for a figure without a `face` dict (`brow 0,125`, `sock 0,125`, `cheek 0,115`,
   `cheekw 0,080`, `jaww 0,105`, `chin 0,062/0,085`), so the v0.9.27 look is the fallback, not a special
   case.

### v0.9.27: hands that are hands, **no pupils**, a lumpy skull, boots that fit

The user's fourth round on the same five figures, with a screenshot of a hand seen from above: «Кисти рук
сделаны плохо. У них должны быть анатомически верные кисти с длинными тонкими пальцами, как у остальных
NPC Бабди. Также лица: У NPC в Бабди нет зрачков, вместо них белые (иногда желтоватые) белки. А сами лица
у них всех очень не ровные, с выпирающими частями (скулы, глазные впадины, уши и прочие неровности
черепа). И ботинки вышли нелепо большими». Cast, costumes and placement are unchanged.

1. **The hand was a garden rake.** `add_hand` built the palm as a **flat box** `(0.044, 0.060, 0.020)·u`
   (12 × 16 × 5,4 cm) and then started the four fingers at `wrist + d·0.064u` — i.e. **in the middle of the
   palm's bottom face**, not on the knuckle line, so from above it read as a paddle with a rake hanging off
   it (the user's screenshot). Now:
   - the palm is a **rounded, flattened wedge** — `B.tube` of three rings, 8,1 cm wide × 9,0 cm long ×
     2,5 cm thick, `(0.0242 → 0.0296 → 0.0286)·u` across and `(0.0084 → 0.0098 → 0.0090)·u` thick, so it
     tapers from the wrist to the knuckles and has no box corners; a **thenar ball** (2,4 × 3,5 × 1,9 cm)
     sits on the palm's edge so the hand is not a flat oval seen from the side;
   - four **three-segment fingers** start **on the knuckle line** (`wrist + d·0.062u`), spread across the
     palm's width at `±3,4 cm`, fan only **±10°** (`(k−1.5)·0.70·splay`) and **curl 0,10 + 0,30 towards the
     palm** at the middle joint and the tip — a hand at rest, not a rake. Lengths per phalanx
     **3,1 / 2,6 / 2,0 cm** (the middle finger the longest, the little finger 2,6 / 2,2 / 1,7), radii
     **0,78 → 0,46 cm**;
   - the **thumb** is two phalanges out of the palm's edge near the wrist, angled **down and forward**
     (`d·0.62 + side·0.70 + fwd·0.20`); pointing it straight out to the side read as a horn.
2. **No pupils, no iris, no lash line** — in the game's own people the eye is a **pale ball in a dark
   socket**. The painted iris (`d < 0.74`) and pupil (`d < 0.36`) are gone; the sclera is
   `eye_white` **0,905-0,945** with a warm yellow cast on the docker and the soldier, a **soft shadow under
   the upper lid (×0,70 at the top)**, a grey outer corner, a **dark rim** around the opening (×0,55) and a
   **lit lower lid** (×1,12). `FACE_EXTRA` lost its per-figure `lash`/`iris` keys.
3. **The face is bone, not a balloon.** `head_deform_factory`'s amplitudes are roughly doubled and the
   sphere went **26×18 → 32×22**: brow ridge **0,125** depth (0,100 for the woman), eye sockets **−0,125**,
   cheekbones **+0,115**, a hollow at the eye's outer corner **−0,055**, a **nose bridge** ridge **+0,050**,
   chin **0,085**, a **jaw angle** where the jaw meets the ear (**±0,070** in x), hollowed **temples**
   (−9,5 %), an occipital bump. The cheekbones also **widen the face** (+8 % in x at `ny −0,13`) and the
   **jaw narrows** under them (−10,5 % at `ny −0,62`), so the front silhouette is a **diamond, not an egg** —
   a depth bump alone cannot change a silhouette.
4. **The skull is lumpy**, like the game's photo-scanned heads: `k = 1 + 0.045·snoise(P, 14, 5)` multiplies
   all three radii, damped by **85 % on the face plane** (`front = clip(nz,0,1)`) so the painted eyes and
   mouth stay in their sockets.
5. **The nose is long and heavy** — tip at `+0.070·hrz` instead of `+0.030`, from `ny +0.09` down to
   `−0,150` (length 0,24·hry), radii up ~15 %. Under the level's flat probe light the old stub was
   invisible and the whole face read as a smooth egg.
6. **Ears stick out** (~2 cm): radius `(0.105, 0.225, 0.145)·hr` at `0.995·hrx`, `nu=10, nv=8`. They also
   have **their own paint branch** (`part == "ear"`): before this they fell inside the sideburn mask
   (`|nx| > 0.88`) and the bald official wore two **dark grey slabs** where his ears are. A darker **concha**
   (a Gaussian in y/z, ×0,66) keeps a pale oval from reading as a sticker.
7. **The sideburns are soft**: a Gaussian patch in front of the ear blended at 62 %. The old hard mask
   painted a **black bar across the whole side of the skull** (visible in the first v0.9.27 render).
8. **Cheekbone/temple paint accents** so the geometry reads under flat light: a highlight on the
   cheekbone (+11 %), a shadow in the hollow under it (−11 %), a shadow in the temple (−10 %).
9. **The boots were ski boots**: 41 cm tall (top at `0.192·H`) and **17 cm across**. Now the default
   `boot_h` is **0,075** (top at `0,137·H` ≈ 28 cm), `boot_r` **0,335·hipx** (13,7 cm), the foot box
   `0.305 / 0.021 / 0.56` (was `0.40 / 0.028 / 0.50`), the sole `0.325 / 0.009 / 0.60` (was
   `0.43 / 0.012 / 0.54`) and its bottom sits exactly on `y = 0` (the model must not float). The trouser
   still ends 2 cm **inside** the boot and the boot's top ring is capped, so the v0.9.25 ankle hole cannot
   come back. The officer keeps his tall boots (`boot_h = 0.36`, hidden under the greatcoat).
10. **Evidence, one `MapShot = true` run**: `BabbdiCoop v0.9.27 loaded`, five
    `[FIGURE] figure_N_<name>: … 2 materials, 2,05-2,25 m tall` lines, `[FIGURE] five figures … 5 of 5
    loaded`, `[PROBE] done: 43 ok, 0 misses`, `built 36 buildings, 0 skipped plots, 22 bridges`. GLBs
    1,64-1,95 MB, 2 611-3 076 verts, 3 652-4 332 tris each. `build.ps1` prints **`Figures: 5 GLB`** — the
    stray `fig_5_diag.glb` was deleted from `model\figures` (it made the count 6).
11. **Shots**: `district_61` (the official: white eyes in dark sockets, brow, big nose, cheekbones, a
    narrower jaw, small boots, thin fingers beside the hips), `district_64` (the soldier), `district_67`
    (the scientist — the clipboard now sits **in** the hand), `district_68` (the docker), `district_69`
    (the officer, white gloves), `district_59`/`district_62` (the row from the plinth and along the roof).
12. **Known and accepted**: the eyes are **bright** — they read almost as if lit, which is exactly what the
    game's own masks look like; the faces still read as **masks** at an atlas where an eye is ~38×28
    texels. The figures are purely local (each peer renders its own `figures\*.glb`).

### v0.9.26: the figures' **anatomy** is measured off the game's own people — four heads, huge hands

The user re-sent the in-game reference stills with «Стилизация далека до нормального уровня»: the v0.9.25
figures were still **slim mannequins** (five heads, thin neck, small hands, a straight neckless posture).
This round changes nothing about the cast, the costumes or the placement — only the body underneath, and
every number below was measured on the reference images, not guessed.

1. **The head is 25,2 % of the height (≈4 heads, not 5)** and **egg-shaped**: `head_h = 0.252·H`,
   `hrx = 0.5·0.755·head_h` (head **width** 0,755 of its height), `hrz = 0.5·0.84·head_h` (depth). The old
   0,68/0,86 made a narrow, very deep head that read as a mask on a pole. `landmarks()` (figs3.py ~L518).
2. **The head is carried in front of the shoulders and nodded forward**: `head_z = 0.012·H` and
   `head_tilt = 6°` (`build_head` rotates the finished head geometry about `neck_base`; `skin_at` and
   `make_shade` undo exactly that rotation — `htilt`/`ct`/`st` — or the eyes and the mouth slide up the
   skull). **There is no neck**: `neck_base = chin`, the head sits straight on the collar, the neck tube is
   0,132·head_h wide.
3. **Narrow sloping shoulders, thin arms and legs, huge hands.** Shoulder half-width `shx = 0.094·H`
   (narrower than the head's 0,10·H). Sleeve radii `hipx·0.265 → 0.176` (was 0.300 → 0.200); trouser radii
   `hipx·0.52 / 0.44 / 0.38 / 0.33` (was 0.60 / 0.50 / 0.43 / 0.37) — a thigh is now 12 cm across instead
   of 25. The hand is the signature: palm `(0,044, 0,060, 0,020)·u`, palm centre `wrist + 0,040·u` into it,
   then **four fingers 0,040·u + 0,034·u long** (radii 0,0092 / 0,0082 / 0,0058) splayed by `1,05·splay`
   and a thumb 0,034 + 0,064·u, so the fingertips reach mid-thigh as thin spikes.
4. **The eyes are the other half of the style**: the opening is `1.38` of the old scale, `eye_x 0.335`
   (closer set), inside a **dark socket** (×0,70 at the centre) ringed by a dark hollow (×0,50), with a
   dimmed sclera (×0,72), an iris that fills `d < 0.74` at ×1,55, a pupil `d < 0.36` and a **lash line**
   along the upper lid. A wide white sclera around a small pupil is the one thing BABBDI's masks never do.
5. **Face details**: the nose is slimmer (radii `hr[0]·(0,072 / 0,108 / 0,128 / 0,112)`, nostril shadow
   0,42 → 0,30), the moustache is one continuous band with a notch under the nose, and the official's
   `teeth=True` mouth got dimmer (0,545/0,500/0,430 instead of 0,66/0,62/0,54) — at 0,66 the row of teeth
   read as a slip of paper under the moustache.
6. **Hands are bare skin on four of the five** (`glove` = the figure's own skin for the scientist, docker,
   official and soldier); only the **officer wears white dress gloves** (0,790/0,775/0,735). Dark work
   gloves turned the fingers — the whole point of the new hands — into a dark blob.
7. **The officer's peaked cap is a real cap now**: a snug band (`y0 = 0.330 + 0.05·cf`), a crown that
   flares to **1,075–1,080** of the skull radius and rises to `0.780 + 0.400·sin θ` (a **dome**, not the old
   0,105-rise flat plate that read as a pot lid), and a **visor** hanging from `ny 0.445` (inside the band,
   above the brows), `±56°`, reaching `dz 1,22` with a `−0,105` droop — a brim, not a black slot. The first
   attempt at 1,36/`−0,155` put the dark underside straight over the eyes from a camera at chest height.
8. **The scientist's clipboard reads as a clipboard**: a 8,8 × 12,8 cm dark board, a **pale paper sheet**
   (7,6 × 11,2 cm, new `part == "paper"` in `shade`) 9 mm in front of it and a metal clip on top. The old
   10 × 14,6 cm dark slab at chest height read as a satchel.
9. **Every close-up is framed off the figures' own feet** (`FigFeet`, filled by `Figures()` before the map
   shots run): the camera **3,2 m in front of the row at 1,55 m**, aimed at the chest. The old fixed offsets
   from the roof centre (5,5–7,5 m, and 2,7 m further back than the figures actually stand) left a 2,1 m
   figure at a third of the frame and the faces — the whole point — unreadable. A fifth pose was appended
   for the officer (`district_69`) so the earlier shot numbers stay put.
10. **Evidence** (one `MapShot = true` run): five `[FIGURE] figure_N_<name>: … verts, 2 materials, H,HH m
    tall, feet at local (…)` lines — 2,05 / 2,12 / 2,15 / **2,25** / 2,14 m (the officer is the tallest
    because his cap is 9 cm of crown above the skull) — the summary line `… 2,05-2,25 m tall, four heads
    high`, and `[PROBE] done: 43 ok, 0 misses`. GLBs: 1 805 292 / 1 906 592 / 1 606 900 / 1 863 716 /
    1 919 300 B, 2 684–3 364 triangles each, two primitives (`skin` + `cloth`) on one 2048x1024 atlas.
11. **Shots**: `district_59` (the row from the plinth — five silhouettes, the orange docker the only strong
    colour), `district_60` (3/4), `district_61` (the official: bald egg head, big dark eyes, moustache,
    olive jacket, red tie, pale splayed hands), `district_62` (along the row), `district_63` (the official at
    an angle), `district_64` (the soldier: beanie, beard, camo, vest, belt), `district_65`/`district_66` (the
    south-east corner, unchanged), `district_67` (the scientist: white coat, blue shirt, bob, clipboard with
    paper), `district_68` (the docker: navy beanie, stubble, orange hi-vis vest with reflective strips) and
    `district_69` (**the officer**: banded peaked cap with a visor, olive greatcoat, belt, white gloves).
12. **Known and accepted**: at an atlas where an eye is ~38x28 texels the faces still read as **masks** — a
    pale face, dark hollows, a moustache band — which is what the game's own cast looks like from a metre
    away. The figures are purely local (each peer renders its own `figures\*.glb`), so they are not synced.

### v0.9.18: the penthouse — a four-storey clock tower on the block on the peninsula

The user's request with a reference photo: «Построй на крыше здания на полуострове пентхаус как на фото,
сохранив минималистичный стиль игры» — the lone tower standing in the sea on the western wing gets a
**four-storey** attic drum (two storeys of rooms, a **double-height clock storey** and a top storey), one
4,4 m patinated dial per face, a heavy cornice, a truncated mansard in rusty metal with skylights and a
glass lantern. The dial drawing was rejected once («совсем не то что нужно») and is now a **1024 x 1024
`CoopClockDial`** with roman-numeral strokes, a verstigris ring and 60 minute ticks.

1. **Which block**: `Place(-40f, 225f, 3f, 2, 1.25f)` in `Layout()` — the outlying point tower on the
   western wing (78 x 145 m terrace at +14 m, the «полуостров»). The flag is set on the block that was
   **just added** (`int nb = bldgs.Count; Place(...); if (PenthouseOn && bldgs.Count > nb)
   bldgs[nb].penthouse = true;`), so a rejected plot can never wear it by mistake. `[District] Penthouse
   = true` builds it, `false` leaves a plain roof.
2. **The drum** is the district's own `Wall`/`PlainWall` at `fh = 5,6 m`, inset **1,8 m** from the roof
   plate: **5,6 + 5,6 + 10,2 (clock, double height) + 5,6 = 27,0 m**, `25,8 x 28,0 m` at this block's
   29,4 x 31,6 m roof. The floors are real: the ground floor is one open hall entered through the
   1,9 x 2,9 m door on `+z`, and the storeys above carry a **9,2 x 9,2 m atrium** cut through every slab
   (`DrumFloor`) with 1,05 m parapets, a **glass lift shaft** down the middle and a march stair
   (`AtticStair`) in the east band — so the tower can be climbed on foot.
3. **The clock storey** is double height so a **r=4,40 m dial fits one face** (`Dial`, a 48-segment disc
   fan with radial UVs and `Geo.Tri`), centred in the storey: 4 dials, one per facade, drawn in code on a
   **1024 x 1024 `CoopClockDial`** texture (bronze ring, verdigris, 60 minute ticks, roman numerals as
   strokes, hands at ten past ten). Material chain (logged): the game's **emissive** `S_Emissive00` at
   `_EmissiveBoost 0,35` → `Unlit/Texture` → concrete + dial as `_BC` —
   `[PENTHOUSE] clock material: emissive S_Emissive00 tex=CoopClockDial (1024x1024), boost=0.35` is the proof.
4. **The cornice** (`27,4 x 29,6 m` at `y=187,6`) is the ceiling of the top storey and the mansard's eave.
5. **The mansard** is a truncated pyramid in **`M_MetalRusty00`** (the game's own rusty metal — the photo's
   standing-seam roof), **12,9 m** high, the building mesh's **submesh 1** (`g.Apply(go, b.wall, mMetal)`),
   with **28 flush skylights** (4 on the long slopes, 3 on the short ones, 0,05 m off the plane so they can
   never z-fight), a `7,2 x 7,9 m` platform at `y=200,5` and a **3,8 x 4,1 x 3,0 m glass lantern** on it.
6. **Three meshes**: the drum + mansard (`part 0` concrete / `part 1` metal), `penthouse_detail` (`mDark` +
   `mClock`) and `penthouse_glass` (`mDark` + `GlassMaterial()` = `Sprites/Default`, alpha 0,22 — logged as
   `[PENTHOUSE] glass material: Sprites/Default (alpha 0,22)`).
7. **Evidence** (log + shots): `[PENTHOUSE] clock tower on the block on the wing (29,4x31,6 m, 26 floors,
   top 159,8 m at -40,0,225,0): drum 25,8x28,0 m (inset 1,8 m), 4 storeys 5,6 + 5,6 + 10,2 (clock) + 5,6 =
   27,0 m, atrium 9,2x9,2 m with a lift shaft and a stair, cornice 27,4x29,6 m at y=187,6, mansard 12,9 m in
   M_MetalRusty00 with 28 skylights, platform 7,2x7,9 m at y=200,5, glass lantern 3,8x4,1x3,0 m, 4 clock
   dials r=4,40 m (one per face), roof deck y=160,0, lantern top y=204,7`, and four probes that measure it
   instead of trusting a render: `penthouse floor (inside the drum): 160,01 m (bldg)` (the room stands on the
   roof plate), `penthouse clock storey wall (+z): 9,00 m (bldg)` (a ray from 9 m out at clock height is
   stopped exactly on the wall — not through a window), `penthouse lantern roof: 203,77 m (bldg)` and
   `penthouse roof platform: 200,53 m (bldg)`.
8. **Nothing else moved**: the vent box is skipped on this roof (`Roof()` keeps its `rng.NextDouble()`
   draw), so the seeded layout is untouched — `built 36 buildings, 0 skipped plots, 23 bridges, 71056 quads`.
9. **How to look at it**: fly up with the propeller (`[Game] FlyMaxHeight = 600` since v0.9.15) — the roof
   plate is at **160 m** — and land on it; the door is on the `+z` side (towards the district). Map-shot
   poses `district_39` (the whole tower 3/4 from the sea), `district_40` (head-on at the clock storey),
   `district_41` (the mansard from above: skylights, lantern) and `district_42` (the atrium from below).
10. **Known and accepted**: the drum's windows read pale from outside — it is one bright room behind them
    (the level's probe SH), unlike a house, which has corridors and rooms behind its openings.

### v0.9.19: the legs — two blocks stand on splayed pylons

The user's request with two screenshots: photo 1 marks **two** blocks at the dam crest, photo 2 is the
Brutalist reference (a slab hung on a ring of huge concrete legs, an open dark base under it, a mass of
block above a girder): «Замени первые 3 этажа этих двух домов из Сталин на "ножки", на которых стоят сами
дома».

1. **Which two**: the crest row `RowX(246f, 328f, 26f, J(3f))` — it produces **exactly two** blocks (a wide
   slab and a narrow one, as in the screenshot): `41,4 x 22,7 m`, 23 floors, at `266,7,26,0` and
   `23,6 x 28,0 m`, 31 floors, at `310,2,26,0`. The flag is set on the blocks that row **just added**
   (`int nl = bldgs.Count; RowX(...); for (i = nl; i < bldgs.Count; i++) bldgs[i].legs = true;`), so no
   other plot can inherit it. Nothing else in the layout moved (`built 36 buildings, 0 skipped plots,
   23 bridges`).
2. **How the three storeys are removed without disturbing anything**: `BuildBuilding` shifts the block's
   own numbers for the duration of the shell build and puts them back afterwards —
   `b.baseY += 3*fh; b.floors -= 3;` before `ShellOf`, restored just before `g.Apply`. `b.Top`, the bridge
   heights, the probes and the plan dump therefore keep seeing the declared building (its roof is still at
   `base + 23*5,6`), while the walls, the interior, the stairs and the balconies start **17,0 m above the
   deck**. `b.legs` also switches off the plinth, the two street entrances and the entrance steps — they
   would hang in mid-air, and the plinth would fill the space the legs are for.
3. **The transfer girder** (`Legs`): a **3,0 m** band under the first floor overhanging the footprint by
   **0,85 m** on every side (`b.w + 1,7`), so the block visibly hangs on it.
4. **The pylons** (`Pylon` → `Frustum`, new): a perimeter ring inset 2,2 m from the edges, **1,8 x 1,5 m on
   the deck 2,1 m inside the facade → 3,6 x 2,4 m at the girder** — each one splays outward as it rises, so
   the underside reads as the reference's row of V-shaped haunches. 16 pylons on the wide block, 12 on the
   narrow one. `Frustum` builds the tapered prism with 4 side quads + a bottom cap; `Geo.Quad` flips the
   winding onto the supplied normal, so the caller only hands in outward normals.
5. **The street-level core**: 6,8 x 9,8 m concrete shaft between the legs with the block's own **2,6 m door
   on `±x`** (the line of the entrances the legs removed), walls 0,40 m, **three `AtticStair` flights**
   (5,6 m each) and two landing slabs with a hole over the lane — so the house is still entered from the
   street and climbed to its first floor, exactly the reference's dark base. Two warm lamps (1,4 / range
   21) light the open deck under the girder and one (0,9 / 14) the shaft.
6. **Colliders**: no new ones — the legs and the core are part of the block's own mesh, so the existing
   `MeshCollider` covers them (the open deck between the legs is walkable).
7. **Measured, not trusted** — four new probes in `ProbeWalkability`: `[PROBE] under a block on legs
   (deck): 0,00 m (deck)` (a ray down between the pylons lands on the deck), `first floor over the legs:
   16,95 m (bldg)` (the house really starts three storeys up), `core stair landing 1: 5,75 m (bldg)` (the
   shaft's first landing is there) and `transfer girder: 8,15 m (bldg)` (a ray from 9 m out at girder
   height is stopped 0,85 m in front of the facade — the overhang). Full run `[PROBE] done: 37 ok,
   0 misses`.
8. **Log**: `[LEGS] 41,4x22,7 m block at 266,7,26,0: 16 pylons (1,8 x 1,5 m on the deck 2,1 m inside the
   facade -> 3,6 x 2,4 m at the girder), girder 3,0 m at y=14,0, core 6,8x9,8 m with the street door and 3
   flights, first floor y=17,0, floors 23 -> 20` (and the same for the 23,6 x 28,0 m block, 12 pylons,
   31 -> 28 floors). `built 36 buildings, 0 skipped plots, 23 bridges, 70752 quads`.
9. **Shots** (`district_43`-`district_48`, three per block): the pylons from the street, the whole house
   on its legs from the crest side (the reference's own framing) and the colonnade of V-legs seen from
   under the girder at deck level. Both bases read as an open, lamp-lit space you can walk into.
10. **Knob**: nothing to configure — the flag lives on the two blocks; moving the treatment to another
    pair is one `bldgs[i].legs = true;` line in `Layout()`.

### v0.9.20: the pair on legs is **one house in plan**, and the south-east corner is levelled

Two user requests in one round: (1) «соедини два дома на ножках в один прямоугольный в плане дом, одна
часть выше другой»; (2) «я обвел по контуру ту часть возвышения которую нужно сровнять с базовым
уровнем общей платформы Сталин» — a selected rectangle at the platform's south-east corner, next to the
round hole in the sea.

1. **One house, two heights.** After the crest row (`int nl = bldgs.Count; RowX(246f, 328f, 26f, J(3f));`
   which produced the pair: `41,4 x 22,7 m`, 23 floors at `266,7,26,0` and `23,6 x 28,0 m`, 31 floors
   at `310,2,26,0`) `Layout()` **mutates those two `Bldg`s into one footprint**: the same depth
   `d = 28,0` for both (the wider of the two), `yaw = 0` for both, `z = 26` for both and
   `east.p.x = west.p.x + (west.w + east.w) * 0.5f`, so the east block's west face lies exactly on the
   west block's east face. `west.w` stays 41,4 and `east.w` 23,6, so the plan is one rectangle
   **65,0 x 28,0 m at x 246,0..311,0** with no gap and no overlap, and the floor counts (23 and 31) are
   **untouched** — the west half is 128,8 m tall, the east half 173,6 m, i.e. "one part higher than the
   other". Log: `[LEGS] the pair is one house in plan: 41,4 + 23,6 = 65,0 x 28,0 m at x 246,0..311,0,
   z 26, yaw 0 (west 23 floors, east 31 floors - heights unchanged)`, then the two usual `[LEGS]` lines
   (14 pylons, first floor y=17,0, 23 -> 20 floors; 10 pylons, 31 -> 28).
   `Bldg.legsShut` (**+1** on the west block, **-1** on the east one) makes `Pylon()` skip the ring side
   that is shared, so the two open bases join into **one colonnade** instead of two rows of legs facing
   each other through a wall — the girder stays continuous over both halves. Nothing else moved:
   `built 36 buildings, 0 skipped plots, 22 bridges, 71068 quads` (bridges 23 -> 22: the gap the pair
   used to have is gone).

2. **What the outlined raised thing was** — settled by a temporary coloured diagnostic render (deck
   green, terraces red, rails blue, seam yellow, dam wall magenta, plus a 4 m height grid that printed
   every collider's name) and then removed: the strip was the **+5 m east terrace** (`tiers` "2",
   x 330..380, z 0..210), which ran right up to the crest, so the corner next to the hole was a bare
   5 m ledge with a 1,05 m parapet on top. It was not the level's granite and not the `seam` kerb.

3. **The fix — the terrace is set back.** `tiers[1].z0 = CornerApron` (default **12 m**) and the corner
   becomes one flat plate: new `CornerPad()` builds a **48 x 12 m** slab (`x 332..380`, `z 0..12`) whose
   top is **0,36 m** — the crest's own level — with a **2 m chamfer** down to the base deck on its west
   so it is simply walked onto. Why 0,36 and not 0: the level's own granite ridge
   (`SM_Scene_A_Blockout00.011`) stands **0,32 m** above the platform deck exactly there, so a
   deck-level plate would leave a granite tongue poking out of the middle of the corner — the pad
   buries it and meets the crest **flush**. The terrace's wall and parapet now stand 12 m inland, its
   north edge is hidden against that wall, and the rest of the terrace is unchanged.
   `[DISTRICT] south-east corner: the +5 m east terrace is set back 12 m from the crest (50 x 12 m of
   flat corner at the round hole instead of a bare 5 m ledge), the rest of the terrace is unchanged`;
   `[DISTRICT] corner pad: 48 x 12 m at the south-east corner, top 0,36 m …, chamfered to the base deck
   on the west, 12 quads`.

4. **Measured, not judged from a render** — four probes in `ProbeWalkability`:
   `south-east corner pad (crest level): 0,36 m (corner_pad)`,
   `south-east corner, behind the pad (+5): 5,00 m (terraces)`,
   `east terrace still at +5 (mid band): 5,00 m (terraces)` and
   `base deck west of the pad: 0,00 m (deck)`; full run `[PROBE] done: 41 ok, 0 misses`.

5. **Knob**: `[District] CornerApron` (12; `0` = the old 5 m ledge at the crest, live, no rebuild —
   the tier array is rebuilt from its declarations every build, so the value is absolute).
   Shots: `district_46` walking the edge east towards the hole at eye level (the change in play),
   `district_47` from over the water, `district_48` the crest junction with the round hole in the same
   frame, `district_49` the corner in plan (the pad reads as one strip between the terrace and the
   granite).

### v0.9.14: the memorial — a red-glowing ring of concrete piers on the 2nd-tallest roof

The user's request with a reference photo: «на крыше второго по высоте здания построй подобную скульптуру
(сохрани красное свечение)» — a cluster of dark faceted monoliths with bright red light in the vertical
slits, on a stepped plinth, with small dark blocks scattered around.

1. **Where**: `PickMemorialBlock()` sorts `bldgs` by `b.Top` and takes **index 1** (the user's "#2"; `#1` is
   never touched), skipping a roof narrower than the plinth. The choice is logged as evidence:
   `[MEMORIAL] tallest blocks: #1 22,6x29,2 34fl 198,6m, #2 18,9x25,5 34fl 190,6m, #3 …`. The monument
   stands on the **roof plate's top face, `b.Top + 0.26`** (`Roof()` puts a 0.26 m plate centred at
   `yTop + 0.13`), in the block's own yaw. `Bldg.memorial` is set **before** the build loop so `Roof()`
   skips that block's vent box — the `rng.NextDouble()` draw is kept, so the seeded layout does not shift
   (still `built 36 buildings, 0 skipped plots, 23 bridges, 74992 quads`).
2. **The monument** (all local to its own `Geo`): a two-riser plinth (13,4 × 9,4 × 0,32 + 12 × 8,2 × 0,30,
   top 0,62), then **10 piers** — 3 slabs on each long side (2,2 m wide, 1,0 m gaps) and 2 on each short
   side (0,7 × 2,0 m, 1,4 m gap), heights **9,4-11,0 m**, each with a stepped crown (0,72 footprint, 0,38
   tall) so the silhouette is cut, not a row of plain boxes — and a ring of 6 small dark blocks lying on the
   roof around the plinth (the cubes in the reference's snow). 980 verts, one `MeshCollider`.
3. **The red core**: a **box inside the ring** (`GlowBox`, part 1 = the glow submaterial) — 8,3 × 5,6 × 9,0 m,
   i.e. **0,15 m behind the piers' inner faces and below the shortest pier**, so no red plate is ever seen
   above the ring (in the first build the 10,4 m core poked out and read as a dark disc from above).
   Its four vertical faces carry a **vertical gradient through explicit UVs** (`Geo.Quad(part, a,b,c,d, n,
   ua,ub,uc,ud)`, v = 0 at the plinth, 1 at the top), so the falloff does not depend on any shader trick:
   hot orange at the base → saturated red → deep maroon → nearly black at 9 m.
4. **The glow is the game's own emissive shader**: `S_Emissive00` (`_TextureSample0`, `_EmissiveBoost`
   1,8, `_texcoord` (1,1,0,0)) over a generated **4 × 128 `CoopRedGradient`** texture. That is what the
   level's signs and monitors use, it is HDR, and BABBDI ships `Hidden/PostProcessing/Bloom`, so it blooms
   in game. Fallback chain (each logged): `S_EmissiveColor` → `Unlit/Texture` → `Sprites/Default` →
   `Particles/Standard Unlit` → `Standard` + `_EMISSION` → the district's concrete with the gradient as
   `_BC`. The shader list of the build is dumped once (`[GLOW] 65 shaders in the build: …`) — that is how
   the ASE property names were found instead of guessed (`_BC`/`_MainTex` do not exist on these).
5. **Six red point lights sit *in the channels*, never inside the core** (1,8-2,4 intensity, 6-7 m range,
   no shadows): 2 low + 2 high in the front gaps, 1 in the rear gap, 1 on the west flank. A light inside the
   ring would light the piers' **outer** faces too (no shadow occlusion) and wash the whole monument pink;
   a light in a channel only reaches the inner faces, because the outer faces' normals point away from it.
   That is what keeps the slits glowing **at an angle**, where the core itself is hidden.
6. **Pier depth is the whole trick** (measured, not guessed): the visible width of the red at an angle is
   `gap − depth·tan θ`. With the first 1,0 m deep piers the core vanished completely from 60° (1,0 − 1,73 < 0
   — preview 31 was a solid grey mass with one red slit). The piers are now **0,55 m deep**, which keeps
   0,45 m of red at 45° and 0,2 m at 60°; and every wide pier is additionally **split by a 0,24 m glowing
   slit** (`Pier` → two `Monolith`s), so a long face reads as 5 red lines (0,24 / 1,0 / 0,24 / 1,0 / 0,24)
   from any angle instead of 2.
7. **Knob**: `[District] Memorial = true` (`SovietDistrict.MemorialOn`) builds or skips it; the monument
   does not consume RNG, so switching it off changes nothing else.
8. **Evidence shots** (`MapShot = true`, poses 30-33 after the corridor poses): `district_30` head-on (broad
   red bands with hot orange bases), `district_31` the reference's own **low 3/4** at eye level,
   `district_32` high 3/4 (the five red lines on the front face and the flank), `district_33` from the
   neighbouring roof 34 m away — the red reads across the district. Log: `[MEMORIAL] on the 2nd-tallest
   block (18,9x25,5 m, 34 floors, top 190,6 m at 122,0,118,0): 10 piers (wide ones split by a 0,24 m slit),
   red core 8,3x5,6x9,0 m, 6 red lights, 980 verts, roof y=190,8`.

### v0.9.17: the towers of the House of Soviets stand **in** its hall, every storey 6,4 m, monumental windows

The user's three reports, all about the House of Soviets only: «Пропал весь первый этаж Дома советов (скрин
приложил), верни наполнение» → «увеличь высоту всех этажей в нем в 2 раза» → «увеличь его окна в 2 раза по
высоте и в 2,5 раз по ширине, удалив каждое второе окно». The blocks were not touched.

1. **Why the ground floor "disappeared" — the plinth height was counted twice.** The towers were placed as
   `Bldg.p = p + rot*(±tx,0,0)` with `p.y = PlinthH (=10)` **and** `baseY = BASE + p.y` (= 10,15), so their
   floor 0 sat at `10 + 10,15 = 20,15` m in district space instead of 10,15 — 10 m above the hall floor and
   just behind the podium's roof ring (`ry = 12,95` local → 22,95). From outside the House looked correct (the
   towers seemed to start at the podium roof), but inside the hall there was **nothing**: a 25 m long empty
   cavern with only the colonnade — exactly the user's screenshot. Now `var tp = p; tp.y = 0f;` is the towers'
   object position and `tbase = BASE + p.y` (10,15) their floor; the `[HOUSE]` line states it.
2. **Every storey of the House is 6,4 m** — `const float pfh = FH * 2f` feeds the podium (`pfloors = 4`,
   `ptop = 25,75`, twice the old 12,95) **and** the towers (`fh = pfh`, 30 floors → **192 m** above the hall
   floor, top ≈ 202 m above the deck, i.e. the tallest thing in the district; the memorial still sits on the
   2nd-tallest *block* — the towers are not in `bldgs`). The towers also take `dh = 1.3f`, so their doors are
   the same 2,8 m as everywhere else. Their stair follows the storey automatically (`PlanStair`):
   `[STAIR] 37,7x37,7 m, corridor 2,2 m clear: … flight 3,00 m, 18 risers of 0,178 m, tread 0,167 m,
   landings 6,91 m`.
3. **Windows: 2,5 × wider, 2 × taller, every second one gone.**
   - Podium: **4,75 × 3,00 m** (was 1,9 × 1,5), sill 1,15 m, 4 rows; `Wall(..., skip: 2)` halves the pitch, so
     the 15 openings of the old 5·K bay become **8** spread evenly over the 107 m facade (7 on the sides).
   - Towers: **4,00 × 4,10 m** (was 1,60 × 2,05), sill **1,60 m** (×2 as well, so the opening is centred in
     the 6,4 m storey), and `winSkip = 2` drops every second room cell on **all four** facades (8→4 openings
     per long facade, 2 per band per end wall).
   - `Wall` gained two optional parameters, `int skip = 1, float pad = 0f`: `skip` strides the window loops
     (both the `bay` and the `cols` branch) and `pad` widens the width clamp (`b − a + 2·pad − 0,5`). Without
     `pad` the room-grid spans (cell 4,6 m minus `WinClear` 0,9 on each side) capped the towers' openings at
     2,3 m; `winPad = 0,85` lets the 4,0 m window fill its cell. New `Bldg` fields `winSkip` / `winPad` default
     to 1 / 0, so every other building is untouched (`built 36 buildings, 0 skipped plots, 23 bridges,
     70980 quads` — the drop from 74992 is exactly the removed openings).
4. **The hall is not a bare cavern even without the towers**: the floor is now a **ring** around the two tower
   footprints (`Rect2` ×5 at `BASE − SLAB … BASE`) instead of one quad, because the towers bring their own
   floor slab at exactly `BASE` (an overlapping quad would z-fight); the colonnade was thickened to
   `2,0·K` and moved to the middle of its strip (`cz = (tz + hd − T)/2` ≈ 21,3 m) so a column neither cuts
   into the towers nor sits on the hall's outer wall, and the recessed central bay doubled its vertical
   rhythm with the storey (`ph = 88·K` = 127,6 m, 11 slots at 7,2·K, link slab at 60·K).
5. **Measured, not guessed** — three new probes in `ProbeWalkability`, the first two are the proof of the fix:
   `[PROBE] House hall tower front (+z): 2,92 m (bldg)` and `(-z): 3,29 m (bldg)` — a ray from the front
   gallery at 6 m above the hall floor stops on a tower face ~3 m in (the podium's own wall is 25 m away,
   so `MISS` or 20+ m would mean the towers are gone again; at 3 m above the floor the ray went *through* a
   window and returned 19,6 m, which is why the probe sits at wall height) — and `[PROBE] House hall floor:
   10,15 m (podium)`. Full run: `[PROBE] done: 29 ok, 0 misses`.
6. **Evidence shots** (the House's cameras were re-framed for the doubled height, and the House's 4° yaw is
   baked into the coordinates — a camera "by eye" lands in a column or inside a wall, which is what happened
   twice): `district_5` the whole district from the city (the two 192 m towers are the skyline), `district_6`
   3/4 from the north-west (lit), `district_7` the hall from high up its west aisle (25,6 m tall, tower wall of
   rooms on one side), `district_36` the same aisle at eye level (tower windows on one side, the podium's on
   the other), `district_37` the walk up the approach avenue at ground level — podium, both towers and the
   central strip, `district_38` the podium facade head-on (8 openings, every second one gone).

### v0.9.16: the stairs — one plan per block, flights 2-3 m, a coarser platform

The user's three reports: «Лестницы между этажами в некоторых домах не работают: они упираются в глухой
потолок… Лестничные пролеты сами по себе слишком короткие, они должны быть от 2 до 3 метров во всех зданиях
Сталин (включая Дом советов). Также, измени зернистость платформы… с 1 на 0,2 от текущего состояния».

1. **Root cause of both stair complaints — two sets of numbers.** The flights were laid out inside
   `StairsTall`/`Stairs` from their own constants (`corr + 1.5`, `zi − corr − 3`, a fixed 1.5 m landing;
   the House's x-running variant used `land 1.9 / mid 0.9`) while the slab hole was cut in `Interior` from
   another set (`corr + 1.5`, `segLen − land`). Since v0.9.13 the corridor is 2.2/3.3/4.4 m half-width
   (`corrMul`), so the same room layout produced a flight anywhere from 1.16 to 9.68 m — and once
   `run < 2` the builder returned **without building a stair at all**, leaving the doorway and the open
   shaft in place.
2. **Now there is exactly one stair plan per block.** `PlanStair(sh)` runs inside `ShellOf` and stores
   `stRun / stLand / stZLow / stZHigh / stN / stRise / stTread` on the `Shell`; **both** the slab hole
   (`Interior`) and the flights (`StairsZ`) read those numbers, so they cannot disagree:
   `run = clamp(depth − 2·1,1, 2, 3)` (a shallow hall shortens the flight, never the other way round),
   `land = (depth − run)/2 ≥ 0,7`, and `stZHigh + stLand == zi` **exactly**, so the half-landing always
   reaches the outer wall instead of hanging in the room.
3. **One stair shape for every building** — the tall blocks and the House of Soviets' 3.2 m towers alike:
   two flights along z, side by side in x, up northwards, across the half-landing, back south to the
   corridor door. `n = round(fh/2 / 0,175)` risers per flight → 16 × 0,175 m (tall) and 9 × 0,178 m
   (House). The old x-running switchback (`Stairs`/`Flight`/`Step`) is deleted (−103 lines).
4. **Measured, one line per block** (`[STAIR]`, 35 lines: 33 blocks + 2 House towers): flight **3,00 m**
   everywhere the hall is deep enough and 2,0-2,9 m in the wide-corridor/shallow blocks, tread 0,188 m
   (tall) / 0,333 m (House), landings 1,78-4,84 m. The House's towers went **1,68 → 3,00 m**
   (`[STAIR] 37,7x37,7 m, corridor 2,2 m clear: … -> flight 3,00 m, 9 risers of 0,178 m, tread 0,333 m`),
   which is what the user asked for by name.
5. **The "blind ceiling" is now measured, not argued.** `Headroom()` fires 1056 vertical rays from 1,84 m
   above the treads of both flights, on the first four storeys of every block — the slab hole is cut
   through every floor, so any hit is a ceiling in the player's face. Result:
   `[STAIR] headroom: 33 blocks, 1056 rays 1,84 m above the treads, 0 blocked`. `StairProbes` (in
   `ProbeWalkability`) complements it: a ray up out of the shaft now runs **through all floors to the
   roof** (`[PROBE] house 0 stair flight 1 (3,00 m long) above: 86,90 m … -> bldg`, flight 2 89,70 m),
   while the two landings stay solid where they must be (`stair landing 1: 13,75`, `half landing: 10,95`).
6. **`[District] PlatformGrain` 4 → 0,8** (0,2 of the v0.9.13 value — the user's number): the platform's
   horizontal faces drop from uv 1,4/m to **0,28/m** (`[LOOK] … platform floors x0,8 = uv 0,28/m`), the
   walls and houses keep 0,35/m. Up close the deck still reads as fine pitted concrete (`grain_ours`, 2,4 m
   above it), at 60 m it reads as a smooth slab (`grain_wide`, same ruler as the dam crest). Live knob, no
   rebuild; the value is written into the game's cfg.
7. **New evidence poses** (right after `district_21`): from the **half-landing looking up the upper
   flight** — the user's own frame — and from the corridor landing looking up the lower flight.

### v0.9.13: corridors 2x/3x/4x wide, a grainier platform

The user's two requests after v0.9.12: «Коридоры теперь высокие, но слишком узкие (фото) — сделай их шире
в 2, 3 и 4 раза для разных домов произвольно (делая сами дома шире при этом). Также, увеличь зернистость
самой платформы на 400 %».

1. **The corridor is no longer one width.** `Bldg.corrMul` (2, 3 or 4) is chosen per plot by
   `CorridorMul(p)` — a **pure hash of the plot's local coordinates** (`x·8`, `z·8` mixed with the usual
   73856093/19349663 primes and folded once), so it is arbitrary from house to house, reproducible on
   every launch, and **draws no RNG** — the seeded layout is untouched. `Bldg.CorrHalf => CORR*0.5f*mul`
   feeds `ShellOf`, and every corridor-dependent number follows from it: `Interior`'s corridor walls,
   cross-wall doorways, the stair hall's `stairZ0`, the slab cut, the end-facade window bands
   (`EndSpans` keeps `corr + PT/2 + WinClear` clear) and the stair flights (`StairsTall` starts at
   `corr`). Result in the log: `[DISTRICT] corridors: 2x = 13 blocks (4,4 m), 3x = 8 (6,6 m),
   4x = 12 (8,8 m), 1x = 0; deepest block 31,6 m deep`.
2. **The house grows instead of the rooms shrinking.** `AddBlock` takes the extra width out of the
   block's *depth*: `d = s.d + (mul-1)·CORR` (2,2 m per step, i.e. +1,1 m of room band on each side, so
   the room cells keep their 4,6-4,9 m and the corridor is the only thing that grows). Footprints went
   from 16-17 m to **19-31,6 m**; rows at z 274/226/178, the flank columns x 30/354 and the wing tower
   all still fit — `built 36 buildings, 0 skipped plots, 23 bridges, 74992 quads` is unchanged (quads
   only count wall cells, which the depth does not change).
3. **A deep house falls back instead of leaving a hole.** `AddBlock` now tries the hash's multiplier
   first, then 3 and 2, and only gives up if none fits `Free()` **and** the four-footprint-corner
   terrace test (`TierAt` at the grown depth). So the ring never loses a plot to a wide corridor.
4. **Measured, not assumed** — the corridor probes shoot across the hall from its axis **4,6 m above the
   floor** (lower rays escaped through the 2,15 m room doors or out of a window whose sill is 1,1 m and
   reported `MISS`): `[PROBE] corridor 3x (=6,6 m clear) +z: 3,11 m (bldg)` and `-z: 3,11 m (bldg)`,
   `[PROBE] house 1 corridor 4x (=8,8 m clear): 4,21 m (bldg)` — the clear half-width is
   `corr − PT/2`, so the real hall is **4,02 / 6,22 / 8,42 m** clear (the old one was **1,82 m** clear).
   `[PROBE] done: 26 ok, 0 misses`.
5. **`[District] PlatformGrain = 4`** — the **+400 % grain is on the platform's own horizontal
   surfaces**, not on the houses: `Geo.FloorMul` multiplies the UV density of every face with
   `|n.y| > 0,7`, and it is set only on the platform meshes — `terraces`, `deck`, `seam`, the ramp
   beams, the stair treads, `streets` and the House's `square`. Walls, houses, bridges and the House's
   towers keep the v0.9.11 grain, so the platform reads as the older, rougher slab. The `[LOOK]` line
   reports it: `… uv 0,35/m … platform floors x4 = uv 1,4/m` (a 0,71 m tile, ~0,5-1,5 cm pits — finer
   and denser, which is what the reference photo's floor looks like; the knob is a live cfg value, set
   0,25 instead of 4 if what was meant was *coarser* pits rather than denser grain).
6. **Not changed on purpose**: the covered bridges stay 2,44 m wide (a bridge door in the middle of the
   end wall, a wide hall behind it), and the **House of Soviets keeps its 3,2 m storeys and its 2,2 m
   corridor** — it is a monument, built directly rather than through `AddBlock`. Balconies, windows,
   storey heights and the seeded layout are all untouched.
7. **New evidence shots**: `district_28` = a **4x** corridor (8,8 m, a hall), `district_29` = a **2x**
   corridor, `district_20` now shows the 3x stalinka hall; the room-at-the-window pose starts at
   `-(CorrHalf + 0,8)` so it is still inside a room; `grain_ours` / `district_1` show the new platform
   grain.


### v0.9.12: the tall storey on **every** block, balconies that really sit on their windows

The user's follow-up to v0.9.11: «Примени изменение размеров домов и ретекстур из предыдущего промпта ко
ВСЕМ домам… также, поправь балконы, они не на своих местах» (screenshot: slabs scattered diagonally along a
facade, some of them between two windows).

1. **The tall storey is no longer a stalinka speciality.** `StalFh`/`StalDoor` are renamed `TallFh`/`TallDoor`
   and applied **unconditionally** in `AddBlock`: every block now has **5.6 m** floor-to-floor (3.2 × 1.75),
   **+30 %** door openings (interior 2.795 m + 0.065 rounded trim ≈ 2.86, entrances 3.25), windows
   **1.55 × 2.2** with a **1.1 m** sill. No RNG is drawn there, so the seeded layout is unchanged
   (`built 36 buildings, 0 skipped plots, 23 bridges, 74992 quads`). Consequences: `Bldg.Tall` is true
   everywhere, so **every** house uses `StairsTall` (16 risers of ≈0.175 m, flights turned 90°, 1.5 m
   landings) and every bridge joins two equal storeys (`[PROBE] bridge deck: 41,75 m` = 8.15 + 6 × 5.6).
   Heights grew 1.75×: **18-36 floors × 5.6 = 101-202 m** above the deck (v0.9.11: 58-115 m).
2. **The texture needed no change** — since v0.9.4 every builder (deck, streets, terraces, every block, the
   House, the bridges) uses the same `M_ConcreteRough00` + `Stone()` grain map, so the v0.9.11 grain
   (`uv 0,35/m, tone 1,68`) already covered all houses; the screenshot that prompted this was taken on the
   older tone. Re-verified in the log: `[LOOK] district material: M_ConcreteRough00 + 1024x1024 grain …
   uv 0,35/m, gain 1,00, cloud 0,15, tone 1,68, desat 0,80`.
3. **Balcony root cause, part 1 — a half-facade shift.** `Balconies` took the window span's **wall**
   coordinate (`Spans()` returns 0..w, measured from the wall's left end) and used it directly as the
   **building-local** x of the slab. Every balcony was therefore displaced by `+b.w/2`, and the corner guard
   then dropped everything whose shifted x fell outside the footprint — the "balconies in the wrong places"
   in the user's screenshot. Fixed with `x = (cols[c].x + cols[c].y) * 0.5f - b.w * 0.5f`.
4. **Part 2 — a diagonal scatter instead of columns.** The column was chosen by `(c + f) % 3 == 1`, i.e. a
   different bay on every floor (an ascending diagonal), and there was **no balcony door at all** — a slab
   glued in front of a 1.1 m-sill window. Now: **vertical stacks** on every second bay (`c = 1, 3, 5 …`) for
   **every** floor from the 1st, and `BuildBuilding` cuts a real **balcony door** behind each stack
   (`AddDoor(doorsZP, bay centre, y0 + f·fh, winW, 2.2)`), so `Wall` skips the window there and the opening
   runs from the floor up 2.2 m — you can step out onto the slab, which is **flush with the floor**.
   Geometry: slab 0.18 m thick, **2.9 m** wide (clamped 2.0-2.9), projecting **1.15 m**, with a 1.0 m
   parapet (front rail + two cheeks).
   Evidence, `[BALC] 33,7x17,2 m block at 288,5,274,0: 3 stacks over 6 window bays, 75 balconies, 26 floors`
   (7 of the 36 blocks are khrushchevkas, the only type with balconies — unchanged).
5. **Two new probes make the balconies measurable without a render** (`ProbeWalkability`), because a
   straight front elevation cannot show them at all: the level has no sun and ambient 0, the parapet is the
   same material as the wall and stands parallel to it, so in an orthographic face-on still it is
   indistinguishable from the wall. `[PROBE] balcony parapet (1,15 m out): 5,42 m (bldg)` shoots at the wall
   from 6 m and is stopped **0,58 m in front of it**; `[PROBE] balcony slab: 13,75 m (bldg)` drops onto the
   slab from above and lands exactly on the floor level. A horizontal **facade scan** (rays 3 m out, every
   1 m in x, at parapet height and at window height) prints the stacks' positions and the openings:
   `… 3,00 3,00 1,85 1,85 1,85 3,00 …` at y=19.9 (1.85 = the rail 1.15 m out, 3.00 = plain wall) and
   `… 3,00 . 10,32 …` at y=21.0 (`.` = the ray left through an opening). The block's mesh is
   `66624 verts, 1 subs, UInt32`, so nothing is clipped by a 16-bit index buffer.
6. **Debug stills**: `PreviewShot` gained `Lamp` and the map-shot loop now passes `s.Ortho` **and** `s.Lamp`
   (it used to hard-code `0f`), plus `[SCAN] district_N: pos … look … ortho …` for every camera. Poses 23
   (3/4 orthographic view from above-right, lit) and 24 (the player's own street view from below, lit) are
   the balcony evidence — in 23 the stacks read as white ledges in perfect vertical columns in front of
   their windows.
7. **Probes**: the second sample block is now simply `bldgs[1]` (`house 1`) — with every block tall there is
   no "other storey layout" to search for. Full run: `23 ok, 0 misses`, house floors 8,15 / 13,75 / 19,35,
   stair landing 13,75, half landing 10,95, windows `open all the way through` (0/1) and `19,37 / 14,14 /
   18,61 m` for 2/3.
8. **Not changed**: the **House of Soviets** keeps its own 3.2 m storeys, window sizes and proportions (it
   is a monument, not a block); say the word and it gets the same treatment.

### v0.9.10: the district wears the dam's own concrete, at the dam's own grain

The user compared the seam (screenshot from the joint: the platform on one side, the old dam crest on the
other) and asked for the platform and the blocks on it to read as **the same surface as the dam**, with the
texture and the grain picked as precisely as possible. The material was already `M_ConcreteRough00` +
`T_RoughConcreteMacro`; what made the district look like coarse camouflage was the **map**, not the material.

1. **Diagnosis**: the v0.9.6 grain pass was a high-pass against a hard 32x32 box with **gain 1.7** — it
   removed the photo's broad stains but *doubled* the mid-frequency noise, so every 2-5 cm detail was
   amplified into a blocky pattern; the 32x32 lattice (1/32 of the 1.4 m tile) showed up as square blobs.
   Measured (see 3) the density was never the problem: 0.7 repeats/m gives 0.7-3 cm pits, the dam shows
   1-4 cm.
2. **The map now (`Stone()` / `GrainTexture()`)**: `T_RoughConcreteMacro` read back at its own 1024²,
   `out = mean + (px − blur)·gain + (blur − mean)·cloud` with **gain 1.0** (the pits keep exactly the
   photo's/ dam's own contrast) and **cloud 0.15** (the 20-40 %-of-tile stains are damped, not removed —
   at 1.4 m tiling they would repeat like wallpaper), 128² cloud lattice smoothed with a wrap 3x3 so no
   grid creases survive, per-channel mean (no hue shift), then `GrainDesat 0.8` and a uniform `GrainTone 1.2`
   (the only way to lift albedo — `S_EnviroSurface00` has **no `_Color`**; the level is baked, runtime
   geometry only gets probe light).
3. **The tile is now exactly periodic (`Seamless()`)**: `T_RoughConcreteMacro` is only *nearly* seamless
   (mean |Δ| across the wrap 13.1 vs 8.6 between neighbouring columns), and at 1.4 m tiling that drew a
   faint **1 m grid over the whole district** (visible in the first v0.9.10 shots). `Seamless` = offset-and-
   blend with a sin² window (edge = the half-shifted copy only, middle = the photo, cross-faded between), so
   the map has no edge discontinuity at all. With `GrainCloud 0.35` the blend's ghost still showed as a
   diagonal lattice on sloped floors (the `district_10` shot); at 0.15 it is invisible — the ghost of pure
   grain is just grain.
4. **Measurement, not guesswork — `GrainShots()` (map-shot mode only)** writes `grain_wide` (60 x 60 m
   ortho straight down at the seam, 5.9 cm/px), `grain_dam` / `grain_dam2` (2.4 m above the crest, straight
   down) and `grain_ours` (the same over the deck) — both sides of the seam on the **same ruler**
   (2.7 mm/px). Result: the deck's pits 0.7-3 cm, the dam's 1-4 cm → the game's crest maps the texture
   about **once per 1.3-1.5 m**, so `GrainScale 0.7` (0.7 repeats/m = a 1.43 m tile) is the match.
5. **Live knobs, no rebuild** — `[District] GrainScale` (0.7), `GrainCloud` (0.15), `GrainGain` (1.0),
   `GrainTone` (1.2), `GrainDesat` (0.8); the actual values are logged once per build as
   `[LOOK] district material: M_ConcreteRough00 + 1024x1024 grain from T_RoughConcreteMacro 1 (1024x1024,
   uv 0,7/m, gain 1,00, cloud 0,15, tone 1,20, desat 0,80)`.
6. **What the shots show now**: the deck, the streets, the terraces, the stairs, every house, the House of
   Soviets and the bridges are one continuous fine-pored concrete with no tile grid, no camo and no visible
   repeat; `district_1` (street), `district_10` (grand stair, previously the worst lattice), `district_16`
   (inside a bridge) and `grain_ours` (2.4 m above the deck) all read as the dam's own surface.
   Note the tone caveat: the dam is lightmapped and its crest reaches ~180 grey while probe-lit runtime
   geometry sits at ~60-110 depending on the spot, so a brightness step at the joint depends on *where* you
   stand; `GrainTone` is the only lever.


### v0.9.11: lighter and coarser concrete, a stalinka with real storeys, windows cut on the room grid

Three user reports: the concrete is too dark and too fine against the dam (photo comparison), the stalinka
floors are cramped (ceilings + 75 %, door openings + 30 %), and **windows look into a wall / a stair / a
blank slab** (three screenshots of green wall and stair soffit seen through window openings).

1. **Texture** — the v0.9.10 grain pipeline is untouched, only its two knobs moved:
   `GrainScale` 0.7 → **0.35** repeats/m (a 2.86 m tile instead of 1.43 m, i.e. **grain twice as coarse**)
   and `GrainTone` 1.2 → **1.68** (**+40 %** albedo). 1.68 is the ceiling of the usable range: the photo's
   brightest pixel is 141/255, so 141 · 1.68 = 237 leaves margin, 1.75 would clip the pinholes to white.
   Both are live `[District]` cfg knobs (`GrainScale`, `GrainTone`); the log line reports the values in use
   (`[LOOK] district material: … uv 0,35/m, gain 1,00, cloud 0,15, tone 1,68, desat 0,80`).
2. **Stalinka storeys** (`StalFh = 1.75`, `StalDoor = 1.30`, `[FLOOR]` type `1` only): `b.fh = FH * 1.75` =
   **5.6 m** ceiling-to-ceiling (was 3.2), door openings **×1.3** — interior doors 2.795 m, plus the +0.065
   rounded trimmed heights (2.86 / 2.73), entrances **3.25 m**. Windows had to grow with the storey or a
   5.6 m floor would read as a blank band: `winH 2.2` (was 1.65), `sill 1.1` (was 0.95), `winW 1.55`.
   Bridge portals stay 2.75 × 2.2 (the bridge shell is built from the same constants on both sides).
3. **The tall storey needs its own stair** — the old switchback (flights along x, 8 + 8 risers of 0.2 m =
   3.2 m) cannot climb 5.6 m in one storey at a walkable tread, and simply stretching it would have left
   ~4 m of blank wall in the stair hall (one of the user's screenshots: a stair soffit seen through a
   window). `StairsTall`/`FlightZ` turn the switchback **90°**: two flights **along z**, side by side in x,
   a 1.5 m landing at each end, **16 risers of ≈0.175 m** — treads 0.25-0.38 m, `n = round(fh / 0.175)`.
   The slab hole is `(xs, corr + 1.5, xs + segLen, zi)` so the arrival landing (the corridor-side strip)
   stays solid and the flights + half-landing get a full storey of headroom.
4. **Windows are cut from the room grid, not from a bay pitch** (root cause of the blocked windows): the
   facade used an even `bay` (3.2 m) independent of the interior, so a window could land over a partition;
   and the end facades got windows over the first/last room, where the cross partition at `k = 0` is flush
   with the end wall and fills the room with a blank face 1 m behind the opening. Now one `Shell` per
   building (`ShellOf`, `LongSpans`, `EndSpans`, `Columns`, `Spans`) describes the same room grid the
   interior uses, and:
   - long facades: **one window per room cell, centred in the cell** (`x0 + (k + 0.5) · segLen`), so the
     mullion always falls on a partition and the pane always faces a room;
   - the stair cell gets **no window on the +z facade** (that is where the flights rise along z) — the
     ordinary stair hall is on the north side, so the window would have looked straight at the stair;
   - end facades: windows only **inside the room band**, i.e. clear of `corr ± PT/2` and `zi` by `WinClear`
     (0.90 m), never straddling the corridor wall;
   - `Interior()` no longer emits the `k = 0` / `k = seg` cross partitions (they duplicated the end wall
     and blocked the end windows);
   - the stalinka's **solid projecting risalit box is gone** (it covered three windows per floor); the
     facade rhythm is now carried by `Pilasters` on the cell mullions, and `Balconies` sit on the same
     window columns.
5. **Bridges pair storeys of different heights**: with 3.2 m and 5.6 m storeys in one district a fixed
   `BridgeFloor` no longer lines up, so `PlanBridges` searches the **common height** per pair
   (`score` against `baseY + BridgeFloor · FH`, storey indices kept as `fa`/`fb` in `BridgeSpec`) and skips
   a pair whose terraces differ by more than 0.02 m.
6. **Verification — `WindowProbes(b, tag)`** fires a ray from 2.5 m outside **each recorded floor-1 window**
   straight in and logs the first hit. The first run reported a uniform `0,97 m (bldg)` for every window,
   which looked like the bug the user reported — it was a **probe bug**: the ray normal was built in
   root-local space while the district root itself is rotated, so the ray started 2.5 m in the *wrong*
   direction and clipped a nearby wall. With `Vector3 n = root.rotation * (rot * b.winDir[i])` the real
   answer is: `house 0 window 0/1` = **open all the way through** (the window, the corridor doorway and the
   opposite facade window all sit on the same cell centre), `window 2/3` = first hit **19,37 m** (a cross
   partition across the middle of the building); second building 0/1 open, 2/3 = **14,37 m**. Nothing sits
   behind an opening any more, and the interior shots (`district_16`-`district_19`, one per facade, camera
   in the room looking at the window) show sky, sea and the neighbour across the street.
7. Probes are now storey-agnostic: `StairLanding(b)` / `StairMid(b)` return the sample point above the
   arrival landing and above the half-landing for **both** stair layouts (tall flights turn about z), and
   the second probe block picks a building of the **other** layout than `bldgs[0]`. Result:
   `[PROBE] done: 21 ok, 0 misses` — house floors 8,15 / 13,75 / 19,35 (fh 5.6), stair landing 13,75,
   half landing 10,95; the 3.2 m house 11,35 / 9,95.
8. Preview poses: 16-19 are the four room-at-the-window shots, 20 the stalinka corridor (2.8 m doors under
   5.6 m ceilings), 21 its stair, 22 its facade, 23/24 a bridge, 25 the dam joint. `[SCAN] shot <name>` is
   logged before every render, so a slow map-shot run is diagnosable from the log.


### v0.9.9: the platform is terraced — five raised floors, inclined roads, street stairs

The user's third plan (legend: **pink = the borders of the platform's floors, yellow = roads under an
angle, green = stairs, purple = the House of Soviets, black = the hole in the sea, grey = the dam**) with
the levels written on it: «Все этажи платформы нужно поднять относительно базового уровня на Х метров.
Для 2 х=5, для 3 х=8, для 4 х=11, для 5 х=14, для 6 ч=10».

1. **`Tier` / `tiers[]` / `TierAt(x, z)`** — the platform is no longer one flat deck. Five zones, in
   local coordinates, each lifted by its own height and built as one concrete box from -0.5 m to its top
   (one `BoxCollider` each, so the walkable surface is exact):
   - `3` seaward band, x 0..380, z 210..300, **+8 m** (the promenade over the sea);
   - `2` east band, x 330..380, z 0..210, **+5 m**;
   - `4` west band along the crest, x 0..48, z 0..210, **+11 m**;
   - `5` the western wing, x -78..0, z 155..300, **+14 m**;
   - `6` the plinth of the House of Soviets, x 160..305, z 40..132, **+10 m** (`const PlinthH`).

   `TierAt` returns the highest zone covering a point (0 on the base deck) and is the single source of
   truth for the ground everywhere: `AddBlock`, the streets, the bridges, the probes and the plan dump.
2. **Every block stands on a terrace** (`b.baseY = TierAt(p.x, p.z) + BASE`), and a block whose four
   footprint corners do not all land on the same tier is **rejected** (logged as "would straddle a
   terrace edge") — otherwise its floor would float over the lower half of its footprint. On a terrace
   `b.plinth` is switched off (the terrace mass is the plinth). The House of Soviets moves up as a whole:
   `HouseOfSoviets(new Vector3(230, PlinthH, 84), 4)` with the towers' `baseY = BASE + p.y`, and the
   esplanade slab sits at `PlinthH + 0.06` (probe 10,12 m).
3. **Bridges only between blocks on the same floor** (`|A.baseY - B.baseY| > 0.6` is skipped), so a
   covered bridge never spans two terraces.
4. **`ramps[]` (yellow)** — six inclined roads, built by `Beam()` (a box whose local +Z runs along the
   line, top face exactly through both end points, 0.7 m thick, a 1.02 m parapet on each side and a
   thicker tail under the upper end): `ring_west` x 56, z 140..210, 0 -> 8 m (11 %), `ring_east` x 324,
   same, `east_band` x 352, z 180..210, 5 -> 8 m, `west_band` x 8, z 235..210, 8 -> 11 m, `wing` z 255,
   x 40..0, 8 -> 14 m, `plinth` z 79, x 105..160, 0 -> 10 m. Every ramp **ends exactly on the edge of the
   upper tier** (a metre of overshoot would leave a 0.5 m lip the CharacterController cannot climb —
   `stepOffset` is 0.3 m).
5. **`stairs[]` (green)** — four street stairs of solid treads (`TierStairs`), rise <= 0.23 m:
   `stair_z3` (x 137, z 192..210, 0 -> 8 m: 36 steps of 0.22 m, tread 0.55), `stair_plinth` (x 230,
   z 12..40, 22 m wide, 0 -> 10 m — the grand stair up to the House's plinth at the end of the approach
   avenue), `stair_z4` (x 44, z 222..210, 8 -> 11 m), `stair_wing` (z 167, x 12..0, 11 -> 14 m). Two
   cheek walls (sloped beams) close the sides, so a stair never reads as floating slabs.
6. **Parapets (`Rail`)**: for every tier edge, 3 m segments of a 1.05 x 0.4 m wall are emitted **only
   where the other side is lower** (a higher neighbour is its own wall; the platform rim counts as a drop,
   so the sea edge is fenced too). A segment is left out wherever a ramp or a stair arrives
   (`Linked(pad 2.5)` over the two `Link` lists), so the openings line up with the roads automatically.
   812 segments in the current layout.
7. **Streets follow the floors** (`Road(g, x0, x1, z0, z1, y)` — segments with an explicit height): the
   crest-side ring at z 52 (base, split at the House's plinth x 160..305), the seaward ring at z 248
   (+8 m), the two legs x 56 / x 324 (base up to the ramp foot, then +8 m) and the approach avenue
   x 218..242 from the crest to the grand stair.
8. **Layout re-fitted to the zones** (blocks are 24..47 m wide, so the rows were re-cut around the
   terraces and the ramp corridors): rows at z 274 (7 blocks), z 226 (5, +8 m), z 178 (6, base); the west
   flank column x 30 (4, +11 m), the east flank column x 354 (3, +5 m), the inner column x 76 (2); the
   crest front x 56..168 (2) and x 246..328 (2); one point tower in the open square west of the House
   (122, 118) and one on the wing (-40, 225, +14 m). Build log: `built 36 buildings, 0 skipped plots,
   23 bridges, 135226 quads`, `[PROBE] done: 19 ok, 0 misses` (terrace floors 8,05 / 5,00 / 11,00 /
   14,00, plinth 10,12, ramp middles 4,00 and 4,91, a stair tread 4,22, a bridge deck 32,15).
9. **`DumpPlan` / `drawplan.ps1`** now also write and draw the terraces (`t;x0;x1;z0;z1;h;name`), the
   ramps (`m;...`) and the stairs (`s;...`), so the plan render uses the user's legend: **pink floor
   borders labelled "floor 3: +8 m"**, **yellow bands for the inclined roads**, **green bands for the
   stairs**, purple House, grey blocks, black hole, grey dam. `plan_v099.png` in the project root is the
   result (the labels are drawn last, so the houses no longer cover them).
10. **Preview poses** 8..12 shoot the new floors: up the ring ramp, a street under the +8 m wall, the
    grand stair to the plinth, the wing ramp to +14 m and the seaward promenade. (The level has no sun
    and ambient is 0, so these renders are as dark as the v0.9.4 ones — the plan is the layout evidence.)

### v0.9.8: moved off the sea hole, extended to the new contour, layout re-planned

The user sent two hand-drawn plans of the district (A = what stands now, B = what it should be): a legend
(grey = dam, black = the round hole in the water, beige = platform, green = houses, purple = House of
Soviets, red = bridges) and a new outline that reaches further seaward.

1. **The platform is off the hole.** The round hole in the sea (centre `local (395, 64.5)`, dark disc
   r 10.5 m, bright rim r 15.5 m) sat with its rim exactly on the east edge of the deck (x = 380) —
   that is the "partially blocked" hole the user pointed at. New `const float ShiftX = -15f` is applied
   once, in `Build()`, as `origin += dir * ShiftX` (`dir` = along the dam), so **the whole district —
   deck, walls, streets, every house, the bridges, the colliders — moves 15 m generally west**. The east
   edge is now at local x = 365 and the hole's rim at 379.5 → **14.5 m of open water between them**
   (measured on the new top render: platform edge px 961, rim px 988 at 2.089 px/m). The crest junction is
   unaffected (the join runs along the dam, and the deck still starts at z = 0); `ClearInvisibleWalls`
   gets the shifted origin, so the disabled boundary colliders follow.
2. **The platform is 380 x 300 m** (`Wid` 220 → 300, +80 m seaward): the plan `B` outline gives 84 px of
   new deck over the seaward edge and 151 px on the wing, and the two drawings share their scale (the
   House of Soviets is 111x50 px in A and 113x48 px in B, the platform fill is 528 px wide in both).
   **The western wing grows with it**: `Az0` 150 → 155, so it is **78 x 145 m** (`z 155..300`) instead of
   78x70 — the plan draws the wing from the new seaward edge down to z ~155.
3. **Layout re-planned for the bigger area** (the drawing's *order*, evenly spread): a chain of blocks
   along the seaward edge (7) and a staggered inner row (6), **one continuous band behind the House (7)**,
   the two **flank columns** turned 90° (4 + 4) and an inner column on x = 92 (2), the **front row along
   the dam crest** (3 + 2, with the approach to the square left open) and the outlying tower on the wing.
   That is **34 houses + the House's two towers = 36 blocks**, 19-36 storeys each, `0 skipped plots`.
4. **27 covered bridges** (was 14), all still at the 11th storey (`BridgeFloor = 10` → deck 32.15 m):
   every gap in every row/column is bridged gable to gable, so the ring is a chain of houses and the
   flanks are vertical chains. The drawing also shows a few end-to-wall links; the builder only does
   corridor-end to corridor-end (a bridge door in a side wall would mean cutting the interior shell), so
   those are represented by the perpendicular columns instead of diagonal links.
5. **`DumpPlan()` (map-shot only)** writes `map/plan_dump.csv` next to the PNGs: `# platform;Len;Wid;Axw;
   Az0;ShiftX`, then `b;x;z;w;d;yaw;floors` per block, `r;ax;az;bx;bz;y` per bridge, `p;…` the House and
   `q;…` its esplanade (**decimal commas**, `;` separated). `%TEMP%\opencode\drawplan.ps1` renders it as
   `plans\plan_new.png` in the legend's colours (beige deck, green houses, red bridges, purple House,
   black hole) — **this is the layout evidence now**: the in-game plan render cannot show it, the level
   has no sun (`ambientLight = 0`, baked lightmaps, only probe SH at runtime), so every roof is the same
   flat grey and the western roofs are invisible in `district_top_ns.png` while the eastern ones show only
   as faint patches. The top view is still useful for the **outline + the hole**, not for the blocks.
6. Numbers after the rebuild: `built 39 buildings, 0 skipped plots, 27 bridges, 148098 quads; platform
   380x300 m + 78 m western wing at y=85,5, shifted -15 m along the dam, centre 205,86,550`,
   `[PROBE] … 10 ok, 0 misses`, `[PROBE] bridge deck: 32,15 m`. Rows: 7 / 6 / 7 / 4 / 4 / 2 / 3 / 2.
   The top view is centred on local `(165, 0, 150)` (ortho 245 → 0.4785 m/px); its `PreviewPoses` first
   shot moved to x = 205 (the open approach) because a front-row house stood in the old camera.

- **Where**: BABBDI's city sits in a drained concrete basin; the sea (`y=71.6`) is held back by the dam that runs from about `(-20, 600)` to `(220, 280)` in world XZ — that is the crest the player stands on, with the empty sea behind it. The surface probe (land → water boundary) gave that line; the crest top is `y=85.5` (`SM_Scene_A_Blockout00.010`).
- **Look (v0.9.4, the user's "retexture" request)**: one material — **`M_ConcreteRough00`** (`T_RoughConcreteMacro`) — for the deck, the extended dam wall, the seam, the streets, the esplanade, every house, the House of Soviets and the bridges. That is what the real dam and most main-level boxes use (`[LOOK] dam … M_ConcreteRough00`, `[LOOK] city … M_ConcreteRough00` / `M_ConcretePanels00`). The panel/painted/stained variety (`mPanel`/`mPainted`/`mLeaks`) is gone from the geometry; those lookups only remain for the log/fallbacks.
  - **Why the district looked "tiled" (root cause, v0.9.4-r4)**: `Geo.Quad` started **every** quad's UV at (0,0) with a span of `size × rate`. A facade is built from 1–5 m cells (walls, reveals, sills, jambs, boxes), so the texture **restarted in every cell** — on a grid of cells that restart reads exactly like tile/panel cladding, and shrinking the density did not remove it, it only made the patches bigger (0.02 = "camouflage stone", 0.002 = one texel, i.e. one flat tone — the user's "фото 1/3"). Now `Geo.Quad(part,a,b,c,d,n,rate)` projects each corner onto the face's **tangent frame** (`Geo.Frame`: `u` along the face, `v` up; horizontal faces use world X/Z) and passes absolute UVs to the corner overload — one continuous UV field over the whole district, so a wall and the deck it stands on sample the same surface.
  - **`UvK = 1`** — the builders' per-metre rates are used as designed (walls/stairs/reveals 0.25–0.5 → one repeat per **2–4 m**; that is what makes `T_RoughConcreteMacro`'s 1–3 cm aggregate pits crisp, like the game's blocks and the user's photo 4). **`DeckUv = 0.12`** (one repeat per ~8 m) for the deck, the streets and the esplanade, and **`DeckMat()`** — the same `M_ConcreteRough00` with a **128×128 smoothed copy of `_BC`** (halving `Graphics.Blit`s, mip-mapped, trilinear, aniso 9; log: `[LOOK] deck material: … 128x128 smoothed copy …`). The raw photo's pits are nearly black and at any usable density they read as potholes (0.12 → 8–15 cm black dots) or as large blots plus a repeating chequer of the texture's diagonal streaks (0.05 / 0.2); the **dam crest the user compares against has no UV variation at all** (blockout box), so it is smooth concrete detailed only by the baked lightmap — the smoothed copy reproduces that (soft ~1 m clouds, no pits, no tiling). Walls stay on the sharp material.
  - **`mesh.uv` is *not* evidence**: `[LOOK] city … uv=0,000/m` came from `Mesh.uv` being **empty for non-readable meshes**, not from degenerate UVs. The `[TEX]` dump (`DumpTextures()`, one shot, marker `texdump/done.txt`, 512 px PNGs via `Graphics.Blit` + `ReadPixels` — `EncodeToPNG` needs a readable texture) is the reliable evidence: `T_RoughConcreteMacro` is light grey concrete with pinholes and no joints (= photo 4), `TexturesCom_Concrete_Bunker6…` is board-formed with plank lines, `T_ConcreteTiles02` is a 4×4 slab grid, and `T_ConcreteStained00_BC` / `TexturesCom_ConcreteDirty0246…` carry a **Textures.com watermark** in the shipped asset (another reason to stay on the rough concrete).
  - **Lighting, measured (`[LIGHT]`, v0.9.4-r4)**: the level is **baked** (`dam lightmapIndex=0`), ambient is **black** (`ambientMode=Flat`, `ambientLight=(0,0,0)`), there is **no directional light** in the level — only point/spot lamps — and 1198 light probes. Runtime geometry therefore gets probe SH only, so it reads a little darker and flatter than the baked dam; measured in `district_2.png`, our towers 58–68 vs the dam 72–81, our deck 71 — within ~15 %, i.e. exposure/fog, not a wrong albedo, so no brightening hack was added.
  - `[LOOK]` / `[LIGHT]` lines (once per build) record the dam's material/texture, the city's largest renderers, the shader properties (`S_EnviroSurface00`: `_BC`, `_Metallic`, `_Smoothness`, `_texcoord` — **no `_Color`**, so a material tint is not available) and the lighting setup: they are the *evidence* for the choice, not leftover debug. The streets are the same concrete as the deck now, so they only read as a 5 cm kerb; from above the roofs equal the deck, so `district_top_ns.png` is one field with faint roof outlines (a debug view, not gameplay).
  - **No "patches" any more**: `Streets()` no longer emits the 44 `M_Grass00` + 10 `M_Dirt00` boxes (the coloured rectangles on the old plan render). The RNG draws are **burned** (270 × `rng.NextDouble()`) and so is the old facade-variety roll (1 per placed block), so the seeded layout stays bit-identical — `built 25 buildings, 0 skipped plots, 14 bridges, 97710 quads`.
- **Idea** (v0.9.2, from the attached plan): the dam crest is extended seaward into a **380×220 m platform** with its top flush at `y=85.5`, plus a **78 m western tongue** (`x −78…0`, `z 150…220`, the white zone on the plan) that carries one outlying tower. The outline continues the dam as a 16 m wall standing in the sea (buttresses every 15 m, coping, parapet-height top, full-width coping on the seaward face); side walls + parapet colliders close the platform. The player walks off the crest onto it (a 0.12 m kerb marks the joint). The plan was scaled with the House of Soviets as the reference: the House is 74 m wide on the drawing and the platform is ~5× that.
- **Local frame**: origin `DamA`, `+X` along the dam `u=(0.6,0,−0.8)`, `+Z` seaward `n=(0.8,0,0.6)`, `y` up from the deck. Everything is built in local coordinates under one root named `CoopSovietDistrict`. On the plan render, seaward is up and `+x` is right.
- **Gating**: `SovietDistrict.TryBuild()` is called every frame from `TickWorldScan`; it does nothing until `WorldReady()` (a ray down at the junction hits `y≈85.5`), so it never fires in the menu, and `root != null` means "already built in this scene".
- **Invisible walls**: level colliders whose bounds intersect the platform volume (including the tongue) are disabled (`[DISTRICT] opened the sea boundary: 4 invisible wall colliders …`). The district's own parapets keep the player on the platform.
- **Layout** (v0.9.3, ring per the plan): `Layout()` fills six rows (+ two single plots), one yaw per row so bridges can run gable to gable. Because the blocks are 1.3× longer, a fixed pitch no longer fits, so each row is laid **end to end**: `RowX`/`RowZ` roll a `BlockStyle` (`RollStyle`), place the block at the cursor and advance by `w + Gap()` (`Gap()` = 4…12 m random), stopping when the next block would leave the row's range. **22 houses + the House of Soviets (2 of its towers are ordinary `Bldg`s) = 25 buildings, 0 skipped plots**: seaward edge 6 (z 194, x 50…330) + 4 staggered inner (z 148, x 98…302); flanks 4 (x 26, z 28…176) + 3 (x 354, z 26…174), turned 90°; the crest front 2 (x 70…172) + 1 (x 288…336) with a 116 m gap in front of the House; one block inside the west flank (74, 92); one point tower on the tongue (−40, 185). House of Soviets at **(230, 84)** — 60 % along the platform, 38 % seaward, as on the plan — on a 135×80 paved esplanade. Types (base w×d before `Grow`): khrushchevka 5 (24–31 × 12–15 m), stalinka 7 (26–34 × 14–17, cornice + risalit), point tower 9–12 (16–20 m square, floor bands), slab 9 (31–38 × 13–16). **`Grow = 1.30`** multiplies w and d of every block (and the tongue tower's `size 1.25` on top of that); yaw jitter ±3…5°; `BASE 0.15` so the ground floor sits on the deck (no gap under the houses); balconies, roof parapet + vent box. `Free()` is an exact **separating-axis test** (`Overlap`/`Axes`/`Radius`) against the other rotated footprints, plus the 4 deck corners against the platform outline (main rect ∪ tongue) — an AABB rejected the diagonal pairs the plan needs. Each row logs its count (`[DISTRICT] row along x at z=194,0 (x 50..330): 6 blocks`).
- **Every house is hollow and walkable**: a shell of exterior walls with real window openings + splayed reveals, and inside a **corridor 2.2 m wide running the full length** (so an entrance door in either gable end opens into it), a row of rooms north and south (`seg ≈ 4.6 m` each, cross walls with 1.3 m doors so the rooms connect), a floor slab per storey (`SLAB 0.26`), and **one stair hall per house** — the middle-north room, open to the corridor through a 1.8 m doorway, with a switchback stair. Common grid: `BASE 0.15`, `FH 3.2`, **v0.9.3 walls `T 0.94` / partitions `PT 0.38` (+80 % over v0.9.2's 0.52/0.21)** — the `Wall` builder offsets the interior face and the reveal depth by `T`, so the openings simply get deep reveals and the rooms/corridor keep their clear sizes (`x1 = hw − T`, corridor 2.2, `segLen = (x1−x0)/seg` ≈ 4.7–4.9 → the flights still get 1.8–1.9 m and 0.23 m treads). Floor count = **(type base + 6 + rng(3…10)) × `Grow`** → **18…36 floors (58–115 m)**; every floor of every house is at the same height, so bridge doors line up.

- **Stairs** (`Stairs` + `Flight`): per storey, up westward along the north half (`Flight(..., toEast:false)`), a 0.9 m half-landing at +1.6 m at the west end, then back eastward along the south half. 8 risers × 0.2 per flight, tread `(segLen − land − mid) / 8` (0.25–0.35 m), `land 1.9` = the arrival landing next to the doorway. Each flight is **solid**: treads, risers, stepped side cheeks from the flight base and a sloped soffit, so it never reads as floating slabs. The level-(f) slab is cut out over `[stairX0, stairX1−land] × [corr, zi]`, so the headroom over the flights and the half-landing is a full storey.
- **Columns/limits**: the game's character is a `CharacterController` with `height 2` and the prefab's `stepOffset 0.3` (never set in code) — 0.2 risers climb fine; door openings are 2.1–2.5 m so the 2 m capsule passes.
- **House of Soviets** (rebuilt in v0.9.1 from `Temp\opencode\sovetov\ref1.jpg`, the real Kaliningrad building — the GMod map the user linked, `gm_dom_sovetov`, was removed from the Workshop and a Source BSP cannot be imported anyway; positioned on the plan in v0.9.2 at `(230, 84)` yaw 4°; **v0.9.3: every dimension × `K = 1.45`**): a **107×49 m podium of 4 storeys** with a colonnade along the front and a walkable hall (entrances from the esplanade, 8.4 × 6.1 m main porch), two **38×38 m towers of 30 floors** (96 m above the podium; `splay 0.28`, `bay 2.9`, windows 1.6 × 2.05, sill 0.8 — the window *size* is deliberately not scaled: `Wall` drops a top-floor window whose `wb` passes `y1 − 0.2`, and scaled windows left the 30th storey blank), built by the same `BuildBuilding` and passing through the podium, a **recessed central bay** (14.5 × 34.8 m, 1.45 m behind the towers) carrying a vertical strip of dark window bands (11 spandrels at 5.22 m, piers 63.8 m tall), and a **horizontal link slab** across the 14.5 m gap 43.5 m up (it only shows in the gap). Towers' entrances open from the podium hall. The esplanade stays 135×80 (the House's 107 m width leaves 14 m of plaza on each side) and `Free()`'s reservation (x 165…300, z 45…125) still matches it. **v0.9.17: every storey is 6,4 m, the windows are 4,75 × 3,00 m (podium) and 4,00 × 4,10 m (towers) with **every second opening removed**, and the towers finally stand **in** the hall — they were placed with the +10 m plinth counted twice and hung above the roof ring, which is why the ground floor was an empty cavern (see the v0.9.17 section).**
- **Bridges** (v0.9.3, **14 of them — all at the 11th storey** (`BridgeFloor = 10` → deck `y = baseY + 10·FH = 32.15 m`), one per gap in every row so each row of the ring is a chain of houses joined at the gable ends): planned by `PlanBridges` between two blocks that are 10–70 m apart and whose `+x` axes both point the same way (the corridor ends face each other); the floor is now fixed (`const int f = BridgeFloor`, the old random `2…top−1` is gone, so a house with ≤ 11 storeys is skipped — with 18…36 floors that never happens). Axis = the straight line between the two corridor ends. **Covered**: 2.2 m deck, **solid wall 2.9 m on one side**, **1.1 m parapet on the other**, 0.3 m roof slab at +2.95 with cross beams every 2.4 m — exactly the reference screenshot (dark covered passage, plate above, wall one side, low parapet the other). Doors 2.2 × 2.75 in the end walls, so a bridge runs passage → passage in the other house. `span = 3…34 m` (real gaps are 4…12 m); corners of the ring stay open (a straight bridge cannot turn).
- **Collisions** (the "textures without collision" complaint): every emitted mesh gets a `MeshCollider` (`convex = false`) — each house, each bridge, the podium, the streets, the esplanade; the platform and the dam wall are `BoxCollider`s (two boxes: main platform + tongue). `ProbeWalkability()` (run from the map-shot path) raycasts and logs `[PROBE]`: street floor 0,00 m (deck), house floors 0,15 / 3,35 / 6,55 (bldg), stair landing 1 3,35, half landing 1,95, **bridge deck 32,15 m (bridge)**, street under a bridge 0,00, esplanade 0,12 — **10 ok, 0 misses**.
- **Windows are open holes** (since v0.9.1, v0.9.2 confirms it visually): there is **no glass mesh at all** — submesh 1, the `CoopWindowGlass` material and the old glass panes were removed. Since v0.9.35 submesh 1 exists again but carries only the **stair railings** in the game's rusty metal (`M_MetalRusty00`, the penthouse's mansard material), so a house mesh is `2 subs` — concrete + railings (`[PROBE] bldg mesh: … 2 subs`). From inside a room the openings show the neighbouring block and the sky; interiors are lit by ambient only (no lamps).
- **Materials** (all from the scene, found by name): `M_ConcretePanels00` (panels), `M_ConcreteRough00` (streets, stalinkas), `M_ConcreteSlabs01` (platform tiles, roofs, esplanade), `M_ConcreteLeaks00` (slabs), `M_PaintedWall00` (painted facades), `M_Grass00`/`M_Dirt00` (yards).
- **Winding**: `Geo.Quad()` orients every quad so `cross(b-a, c-a)` points **along** the face normal. Getting this backwards renders the district inside-out — roofs and windows vanish (that was the v0.9.0 bug).
- **`Geo`**: one mesh — since v0.9.35 up to **two** submeshes (concrete + the stair railings' metal); UVs are world-scaled per face (`uvPerMeter`), so the game's seamless concrete tiles properly. `indexFormat = UInt32`.
- **Log**: `[DISTRICT] materials: …`, `crest under the junction: y=85,5 (…)`, `opened the sea boundary: 4 …`, then one `row along x/z …` line per row, one `skipped a WxD m block at x,z yaw …` line per rejected plot, `built 39 buildings, 0 skipped plots, 27 bridges, 148098 quads; platform 380x300 m + 78 m western wing at y=85,5, shifted -15 m along the dam, centre 205,86,550`, `plan dump: 36 blocks, 27 bridges -> plan_dump.csv`, then the `[PROBE]` lines.
- **Config**: `[District] Enabled = true`, `[District] Density = 1.0` (it only scales the bridge cap, `clamp(round(40·Density), 8…44)` — the block layout is fixed by the plan; the 27 real gaps are below the cap either way).
- **Preview**: `[Debug] LoadScene = Scene_AAA` + `[Debug] MapShot = true` (both self-reset) → `BepInEx/plugins/BabbdiCoop/map/district_0..12.png` (0 crest, 1 street in the ring, 2 the whole front from the city, 3 from the sea, 4 the ring from high above, 5 House from the approach, 6 House tower from street level inside the district, 7 House hall, 8 corridor, 9 staircase, 10 room, 11 inside a bridge at the 11th storey, 12 a bridge from outside), plus `district_top.png` (orthographic plan, aligned with the deck: the dam at the bottom, `+x` right, 245 m half-size) and `district_top_ns.png` (the same with shadows off — the readable plan) and `district_noshadow.png`. Poses live in `SovietDistrict.PreviewPoses()`/`TopView()`; shots use `Camera.main.cullingMask` so they show what the player sees. Indoor poses use the per-building `stairX0/X1/Z0/Z1` recorded by `Interior`.
- **Black patches on the sea** in front of the new dam are not the district's shadows (they survive `SetShadows(false)`) and not new geometry; they belong to the original level's water (unexplained, harmless).


## Villager model (v0.9.6)

The player avatar is the Soul Reaver **`Villager Male 3`** mesh instead of the soldier. The source
(`E:\858\legacy-of-kain-soul-reaver-villager-male-3.zip` → `Villager Male 3.obj`) is a **static mesh:
852 verts, 284 tris, 147 welded positions, 1 material + a 256×256 PNG, no skeleton at all**, so it is
auto-rigged offline onto the soldier's skeleton and shipped as `BabbdiCoop/model/villager.glb`
(2 211 560 B) → copied to `plugins/BabbdiCoop/villager.glb` by `build.ps1`.

- **`Rigger/`** (net8 console, `dotnet run --project Rigger`) is the whole pipeline: it loads the
  soldier GLB as the donor (69 nodes, 2 skins, 4 clips Idle/Walk/Run/TPose), transfers weights to the
  villager mesh, bakes the mesh from its I-pose into the soldier's T-pose and writes a GLB with **the
  donor's nodes, skeletons and animation tracks untouched, only the mesh/weights replaced** — which is
  why the plugin (`GlbAvatarBuilder` / `GlbAnimator`) needed no animation changes at all. Only
  `Plugin.BuildAvatar` changed: it prefers `villager.glb`, and `yaw180 = true` for it.
- Commands: `rig <donor.glb> <src.obj> <out.glb> [dry] [arm=] [fwd=] [pow=] [margin=] [inf=]
  [smooth=] [alpha=] [drop=] [armcut=] [cloth=] [geod=] [nox] [strict]`, `views <glb> <clip:t>
  <tag>` (offline 3-view PNG in `harness_out/`), `wcol <glb> <tag> <clip:t>` (dominant-bone colour
  map), `bones`, `faces <obj> <y0> <y1>`, `vlabels <obj> <tag> [y0] [y1]`, `geodesy`, `stretch`,
  `check`. `Render.cs` draws with a painter's algorithm and labelling; `Villager Male 3.obj` lives in
  `%TEMP%\opencode\kain\src\`.
- **Weight transfer**: for every villager vertex take the nearest donor bones by distance to the bone
  *segments* (`arm=`/`fwd=` offsets, `pow=` falloff, `margin=`), blend the 4 strongest, then Laplacian
  smoothing (`smooth=` iterations, `alpha=`), with a hard `Ok()` gate re-checked after smoothing.
- **Arm classification is geodesic, not by |x| (v0.9.6 fix)**: `|x|` cannot separate the arm from the
  tunic — the tunic hem (±0.051) and the fingers (±0.070) sit at the same height 2 cm apart. So
  `Rigging.Geodesic()` runs a **Dijkstra over the welded mesh edges** from the hand tips (seeds
  `yf < 0.45 && |x| > 0.11·h`) and normalises the distance by height. The measured bands: hand
  0.00–0.10, forearm/elbow 0.22–0.32, cuff/deltoid 0.36–0.38, tunic/back 0.40–0.51 → **`ArmCut =
  0.385`**. With the old 0.21 only the hand counted (82/852 verts) and the sleeve did not follow the
  bone, sticking out like shoulder blades. `ArmGateAt` (the per-height gate table) survives only as a
  fallback for verts with no geodesic data (`geod=0` / `nox`).
- **Skirt rule**: `rigidToPelvis = !inArm && yf > ClothAbove (0.38)` — above the knee no leg bone may
  win, otherwise the centre of the hem gets 50/50 from both thighs and tears apart between the legs
  when running (`skirt band locked to the pelvis 115` verts). The same gate is applied inside `Ok()`
  after smoothing, and arm bones are forbidden to non-arm verts (except `Shoulder`), so the tunic
  cannot be dragged away by the hands.
- **The bake uses the RAW weights**, not the smoothed ones: smoothing bled shoulder weight into the
  hand, so the arms hung ~6° low in Idle (`baked hand centre 0.8°` after the fix). Smoothing still
  feeds the runtime weights.
- **Dropping stretched triangles (`drop=`) is rejected** — it punches holes in the trousers and the
  tunic. Best build metrics: `bake stretch median 1.00x, >1.5x 9, >2x 0, worst 1.9x`, `arm region 198
  of 852`.
- **All-white UVs** (soles, 31 verts) are remapped to the UV of the nearest painted vertex.
- The rigger turns the mesh **180° about Y** into the soldier's convention (front = glTF -Z), so the
  plugin passes `yaw180 = true`; it also re-normalises the height (`[GLB] raw height 1,72 m → avatar
  2,12 m (scale 1,235)`).
- **In-game preview (v0.9.6)** — `[Debug] PreviewAvatar = true`, 134 shots, all stages verified: idle
  (arms hang free of the tunic, feet on the floor), walk, crouch, airborne, run, run back, strafe,
  push (arm forward, prop horizontal), handstand (inverted, on the hands), flip and the clean return
  to rest (`[GLB] feet on ground: lowest vertex was -0,001 m`, `[PREVIEW] hand pos=(-0.40, 0.94,
  -7.80)`, `item bottom=0,973 hand=0,937`). The prop still rides `HandAnchor` by the fist; the
  existing "club points up out of the fist" aim (v0.7.5) is unchanged and was not re-tuned for the
  villager's more vertical Idle arm — it reads as a baton held upright beside the sleeve.
- Offline references kept in `harness_out/`: `v3_Idle_0_0_*`, `v3_Walk_0_4_*`, `v3_Run_0_35_*`
  (three views each), `w3_idle_*` (dominant-bone colour map: green = spine/pelvis, orange = arms,
  blue = legs), `zz_geo2.txt` / `zz_faces.txt` / `zz_verts.txt` (the geodesic/topology dump behind the
  weights).

### Item grip: held in the fist (v0.9.7)

The partner's prop was anchored to the **wrist joint** (`mixamorig:LeftHand`'s origin) and aimed by
`-armDir*0.75 + up*0.25 + fwd*0.75`. Two bugs made it read as floating next to the hand (user video
of the bat hovering beside a straight arm):

1. **The aim degenerated.** With the arm extended forward `-armDir` points *backwards* and cancelled
   the explicit `forward` term, so the aim collapsed to straight up — a vertical bat beside a
   horizontal arm. `HandFollow` now strips the horizontal component out of `-armDir` **before** mixing
   it in, so a hanging arm stands the prop up and an extended arm carries it forward. The carry is
   also flatter (`carryUp*0.40 + up*0.12 + fwd*0.95` ≈ **30° above horizontal**): the old ~53° sent the
   shaft back across the forearm, i.e. speared through the wrist.
2. **The anchor was not in the hand.** `GlbAvatarBuilder.ComputePalmOffset` now measures the palm:
   (a) from the skeleton — mixamo hangs the five finger roots off the hand bone, so half the mean
   child offset is the middle of the palm (the villager: 0.07 m); (b) from the mesh — centroid of the
   vertices the hand bone dominates (weight ≥ 0.5), converted with `bindposes[hand]` (bone space, **not**
   a world round trip: this rig carries a 0.01 root scale and `TransformPoint`/`InverseTransformPoint`
   explode) → **0.21 m** from the wrist, glove 0.24 m across. (b) is used when it finds ≥ 8 verts.
   Both figures are logged: `[GLB] palm centre (hand bone local)=… , skeleton palm=… , mesh palm=…`.
3. `FitHeldItemToHand` places the **butt of the handle 2 cm behind the palm** (`-b1.min.z - 0.02`) so
   the shaft passes through the glove instead of balancing on it (it used to be +4 cm *in front of*
   the wrist bone). Preview log: `closest item point 0,0 cm from the palm`.
4. `HandFollow` drives the anchor from `Hand.TransformPoint(Palm)`, and the arm direction for the aim
   is measured from the elbow to the **palm**, not to the wrist.

Verified from the preview screenshots (`zz_final_*.png` crops, marker test): in idle, walk and push the
handle disappears inside the mitten and the shaft leaves it forward-up — no gap, no crossing of the
forearm. A temporary magenta sphere at the anchor was used to locate the palm against the visible glove
and then removed.


## Smoke-test checklist

1. `powershell -ExecutionPolicy Bypass -File build.ps1` from `BabbdiCoop/`
2. Launch `Babbdi.exe`, look for `BabbdiCoop v0.10.43 loaded` in `<game>\BepInEx\LogOutput.log`.
3. HUD (top-left) shows the avatar source; `модель: GLB villager.glb (yaw180)` means the villager rig loaded (`[COOP] avatar loaded from GLB: …villager.glb yaw180=True` then `[GLB] raw height 1,72 m → avatar 2,12 m`).
4. Both players should run **v0.9.20** (the district is host-side geometry, so a client without it still joins); the 31-byte packet is unchanged, so a 0.7.7/0.7.8/0.8.0 peer still connects — they just do not send/receive the trick bits (bits 3/4). The district is identical for both only if both load the same level scene; it is rebuilt per level load, not synced. **The avatar model is purely local** — each peer renders whatever `model\*.glb` it has, so a mixed 0.9.5/0.9.6 pair sees a soldier on one screen and the villager on the other.
5. In game: partner should see crouch (flag bit0), jump tuck (bit1), a fast 0.094 s item **jab** on Fire1 (bit2) — the fist stays at mid-body height (~1.06 m), the prop swings horizontal forward — the held prop carried up out of the fist (~70° above horizontal, half world size), the boots touching the floor, a faster run cycle, the legs **stepping backwards** when the partner backs up and a **side-step** when they strafe. **v0.8.0**: the partner's **torso must lean with their view** (not just the head), a held **pickaxe must sit in the fist by the handle** (log: `nearest point … cm from the fist` ≈ 4 cm), and pressing **B** (handstand toggle) / **V** (backflip) must show the avatar standing on its hands / doing a full backflip while your own view rolls 180° / spins 360°.
6. **v0.8.1/0.8.2 tricks**: **B** must read as the avatar *jumping onto its hands* **forward, over the head** (not sideways like a cartwheel) — crouch, push, the body turns in the air around its own centre of mass, the hands come down last, then a still handstand (straight arms under the shoulders, body vertical, legs straight up, slow sway) and the same move reversed back onto the feet; **V** must keep its spin speed through the second half (no slowing/vs. twisting) and land upright. After any trick — including one interrupted mid-air, and including a landing from height — the model must stand **exactly** as before (verbose log: `[TRICK] back to rest: localPos=(0,0,0) base=(0,0,0) rot=(0,0,0)`).
7. **Walk/run must be smooth — no freeze-then-jump, no vertical bobbing, no leg cadence pumping.** If it still stutters on the real network: set `[Debug] Trace = true` and play ~20 s with the partner, then send `BepInEx/plugins/BabbdiCoop/trace_*_frame.csv` + `trace_*_pkt.csv` (that is the receive path with real arrival times, delay and jitter). `[Debug] Smoothing = legacy` gives the old path on the same build for an A/B, `[Debug] NetSim = true` runs the synthetic stream offline.
8. Locally, your own first-person item is drawn on top of everything (viewmodel overlay); the partner's item is depth-tested normally.
9. **v0.9.8 district (look + layout; counts changed in v0.9.9 — see item 11; storey heights changed in v0.9.12 — see item 14, floors are now 5,6 m and the bridge deck 41,75 m)**: the platform, the streets, every house, the House of Soviets and the bridges must be **one grey rough concrete, the dam's own** (`M_ConcreteRough00`) — the same surface as the crest you walk off: **fine 1–3 cm aggregate pinholes on the walls (photo 4)**, soft ~1 m stains on the deck, **no tile/panel grid anywhere** (that grid was the per-quad UV restart, not the texture) and **not a single green/pink patch on the platform**; windows read only as deep dark recesses. From the city, walk up onto the dam crest and follow it to the new platform; the skyline of the district must be visible above the crest. The platform is **380×300 m** with a **78×145 m western wing** (top-left on the plan render) and is **moved 15 m along the dam**, so the round hole in the sea is **clear of the east edge** (~15 m of open water, no rim under the deck). On it: **36 blocks** — 7 + 6 on the seaward side, one continuous band of 7 behind the House, 4 + 4 in the flank columns plus 2 in an inner column, 3 + 2 along the crest with the approach to the square left open, and one point tower on the wing — **all 18–36 storeys (58–115 m), every block +30 % in all three dimensions**, walls **0.94 m** / partitions **0.38 m** (+80 %: the window reveals are deep, the rooms, corridor and stairs are unchanged). **27 covered bridges — every one at the 11th storey** (`[PROBE] bridge deck: 32,15 m`), so each row is a chain of houses joined gable to gable at the same height. The **House of Soviets stands in the open middle** at 60 % of the width and 38 % seaward, on a 135×80 m esplanade: **+45 %** — a 107×49 m 4-storey podium, two 38×38 m towers of 30 floors, the recessed dark central strip and the link slab. `[PROBE] … 10 ok, 0 misses`: street 0,00 m, house floors 0,15 / 3,35 / 6,55, stair landing 1 3,35, half landing 1,95, bridge deck 32,15, esplanade 0,12. **Walk in through a gable-end door into the corridor** (2.2 m wide, rooms both sides, **no glass at all — the windows are open holes into the room**), up the stair hall (the middle-north room) to floor 11, and **across a covered bridge into the corridor of the next house** (solid wall one side, low parapet the other, roof plate above). No texture should let you fall through. Set `[District] Enabled = false` to switch it off.
10. **v0.9.8 layout check (no guessing from renders)**: `[Debug] MapShot = true` now also writes `map/plan_dump.csv` (`.cs: SovietDistrict.DumpPlan()`); run `%TEMP%\opencode\drawplan.ps1` to get `plans\plan_new.png` — the actual footprints drawn in the plan's own colours (beige deck, green houses with their yaw, red bridges, purple House, black sea hole). Compare it against the drawing: the ring must be **closed all the way round** (no empty edge), the House standing free in the middle on its esplanade, the hole clearly outside the east edge, and a red bridge in **every** gap of every row. The in-game `district_top_ns.png` only proves the **outline and the hole** — its roofs are flat grey (no sun in the level), so blocks in the western half do not read there at all.
11. **v0.9.9 terraces (the floors, the inclined roads, the stairs)**: the platform must no longer read as one flat
    field. From the crest the seaward band is a **+8 m** plateau, the east band **+5 m**, the west band along the
    crest **+11 m**, the wing **+14 m**, the House's plinth **+10 m** — each edge a concrete wall with a 1.05 m
    parapet on top, and the parapet **open exactly where a road or a stair arrives**. Walk every connection: the
    two ring ramps at x 56 / x 324 (11 %, parapets on both sides), the wing ramp at z 255 (8 → 14 m), the plinth
    ramp at z 79 (0 → 10 m), the 22 m wide **grand stair** at x 230 at the end of the approach avenue (0 → 10 m),
    the stair at x 137 (0 → 8 m), the one at x 44 (8 → 11 m) and the one at z 167 onto the wing (11 → 14 m). No
    lip may stop you at a ramp's head (`stepOffset` 0.3 m, so a ramp must land **on** the edge); no house may
    stand half on one floor (the log says `0 skipped plots` and never `would straddle a terrace edge`); covered
    bridges only join houses on the **same** floor; the House of Soviets stands on its plinth and the esplanade
    is 10,12 m above the deck. Expected log: `terraces 3/2/4/5/6: 380x90 +8 / 50x210 +5 / 48x210 +11 / 78x145
    +14 / 145x92 +10`, `terrace edges: 812 parapet segments`, six `ramp …` and four `stairs …` lines, `built 36
    buildings, 0 skipped plots, 23 bridges`, `[PROBE] done: 19 ok, 0 misses`. Set `[District] Enabled = false`
    to switch the district off.
12. **v0.9.10 the surface itself** (this is the user's "ретекстурируй" pass): walk to the joint of the old dam
    crest and the platform and look at both floors at once. They must read as **one concrete**: dense 1-3 cm
    aggregate pinholes, no square blobs, no 1-1.4 m grid, no diagonal lattice, no wallpaper repeat. The values
    in the log are the evidence: `[LOOK] district material: … uv 0,7/m, gain 1,00, cloud 0,15, tone 1,20`.
    The same must hold on a house facade up close, on the terrace walls, on the grand stair (the slope used to
    show the worst lattice) and inside a covered bridge. `[Debug] MapShot = true` re-shoots the comparison:
    `map/grain_wide` (60 x 60 m at the seam), `grain_dam` / `grain_dam2` (2.4 m above the dam crest) and
    `grain_ours` (2.4 m above the deck) — the two latter on the same ruler (2.7 mm/px). If it still reads
    wrong, tune `[District] GrainScale` / `GrainCloud` / `GrainTone` in the cfg and relaunch — no rebuild.
13. **v0.9.11 — texture, stalinka storeys, windows**:
    - **Concrete is lighter and coarser** than in v0.9.10: the log must say `uv 0,35/m, … tone 1,68`, the
      pinholes read at **~2-6 cm** instead of 1-3 cm and there is still no tile grid at the seam.
    - **Stalinka** (the type-1 blocks; since v0.9.12 **every** block has these storeys — see item 14): ceiling-to-ceiling **5.6 m** (probe: house floors 8,15 / 13,75 /
      19,35), interior doors **2.8 m**, entrance portals **3.25 m**, windows 1.55 x 2.2. Walk its corridor —
      a doorway must be about half the storey high and the corridor must not read as a tunnel of tight
      floors. Its stair (`district_21`) has **16 risers of ≈0.175 m** with a 1.5 m landing at each end,
      flights running **along the corridor's length** (rotated 90° from the ordinary switchback) — climb it
      in game, the capsule must pass under every landing.
    - **Windows**: from the street every opening must show a lit hole with a deep (0.94 m) reveal, never a
      green wall face, a stair soffit or a blank slab right behind it; from inside every room the window
      must open onto that room's own cell. The probe evidence is `[PROBE] house 0 window 0/1: open all the
      way through`, `window 2/3: first hit 19,37 m` (and `14,37 m` for the 3.2 m house) plus
      `[PROBE] done: 21 ok, 0 misses`. If a probe ever reports a hit **< 1.5 m**, a window really is walled
      in — check `ShellOf` cell centres against the partitions, not the probe (`root.rotation` is already
      factored in).
    - **Bridges** now join storeys of different heights (e.g. 5.6 m floor 4 ↔ 3.2 m floor 7 = 22,4 m), so a
      covered bridge must still meet a door in the other house at the same deck height (`[PROBE] bridge
      deck` = one value). Maps worth re-shooting: `district_1` (street), `district_16`-`district_19` (room
      at the window, one per facade), `district_20` (stalinka corridor), `district_21` (its stair),
      `district_22` (its facade), `district_23/24` (balconies since v0.9.12), `district_25/26` (bridge),
      `district_27` (dam joint).
14. **v0.9.12 — the tall storey everywhere, balconies on their windows**:
    - Every block now has the **5,6 m** storey, **2,8 m** door openings, entrances **3,25 m**, windows
      **1,55 x 2,2** (sill 1,1) — the log must show `built 36 buildings, 0 skipped plots, 23 bridges,
      74992 quads`, `house floor 0/1/2: 8,15 / 13,75 / 19,35`, stair landing 13,75, half landing 10,95 and
      `bridge deck: 41,75 m`. Blocks are **101-202 m** tall now (18-36 floors), so the skyline is 1,75x
      what it was; if that is too much, the floor counts in `RollStyle` are the knob (ask first).
    - **Balconies** (7 khrushchevka blocks): vertical stacks, **on every second window bay, on every floor**,
      each one **exactly centred on its own window column**, slab **flush with the floor**, **1,15 m**
      projection, 1,0 m parapet, and a real **balcony door** behind it (the wall opening runs from the floor
      to 2,2 m — you can walk out and stand on the slab). The `[BALC]` line is the evidence of the count:
      `[BALC] 33,7x17,2 m block at 288,5,274,0: 3 stacks over 6 window bays, 75 balconies, 26 floors`.
      Two probes prove the geometry: `[PROBE] balcony parapet (1,15 m out): 5,42 m (bldg)` (a ray from 6 m
      is stopped 0,58 m in front of the wall) and `[PROBE] balcony slab: 13,75 m (bldg)`; the
      `[PROBE] facade scan` lines print the stacks' x positions at parapet height (`1,85`) and the openings
      at window height. **Do not judge them from a face-on render** — same material, no sun, no AO: use
      `district_23` (3/4 ortho, lit, stacks read as white ledges in vertical columns) or walk the street
      and look up (`district_24`).
    - **Texture** is unchanged and identical on every house (`[LOOK] district material: M_ConcreteRough00
      … uv 0,35/m, gain 1,00, cloud 0,15, tone 1,68, desat 0,80`) — the deck, streets, terraces, bridges,
      every block and the House of Soviets share one material, so nothing can look different by type.
    - **The House of Soviets** keeps its own 3,2 m storeys and proportions (a monument, not a block).
15. **v0.9.13 — wide corridors, grainier platform**:
    - Walk into any house: the corridor must **not** be the old 1,82 m tunnel any more. The log states the
      spread: `[DISTRICT] corridors: 2x = 13 blocks (4,4 m), 3x = 8 (6,6 m), 4x = 12 (8,8 m), 1x = 0;
      deepest block 31,6 m deep`, and the probes confirm it across the hall on floor 2:
      `[PROBE] corridor 3x (=6,6 m clear) +z: 3,11 m` / `-z: 3,11 m` and `[PROBE] house 1 corridor
      4x (=8,8 m clear): 4,21 m`, with `[PROBE] done: 26 ok, 0 misses`. Worth walking: a 4x hall
      (`district_28`), a 2x one (`district_29`), the 3x stalinka (`district_20`).
    - **The rooms must not have shrunk and nothing may be missing**: `built 36 buildings, 0 skipped
      plots, 23 bridges` — the deeper footprints fit and the ring is closed. The room cells keep their
      4,6-4,9 m, the stair hall and its flights still start at the corridor edge, and the end-facade
      windows still sit clear of the corridor band (the window probes stay `open all the way through` /
      `19,37 m`, never < 1,5 m).
    - **The platform's own floor must be visibly grainier** than the house walls while the walls stay as
      in v0.9.11: `[LOOK] district material: … platform floors x4 = uv 1,4/m` next to `uv 0,35/m` for
      the walls. Look at the joint of the crest and the platform and at `grain_ours` / `district_1`.
      Knob: `[District] PlatformGrain` (4 = +400 %, `1` = the old uniform grain, `0,25` = pits 4x
      *coarser* instead of denser) — live, no rebuild.
16. **v0.9.14 — the memorial on the 2nd-tallest roof**:
    - Somewhere in the district there is a **glowing red monument** standing on top of a skyscraper. The log
      says exactly where and what: `[MEMORIAL] tallest blocks: #1 … 198,6m, #2 … 190,6m, #3 …` then
      `[MEMORIAL] on the 2nd-tallest block (18,9x25,5 m, 34 floors, top 190,6 m at 122,0,118,0): 10 piers
      (wide ones split by a 0,24 m slit), red core 8,3x5,6x9,0 m, 6 red lights at 0,50x (1,20 / 0,65 /
      0,90), 980 verts, roof y=190,8`. `#1` (198,6 m) must stay bare — the user asked for the second one.
    - **The red must be bright and visible from any direction**: head-on it is two broad bands plus three
      thin slits (0,24 / 1,0 / 0,24 / 1,0 / 0,24); from 45° and from above every one of them is still a
      bright red line, never a solid grey mass. The base of the slits is hot orange-white, the tops fade to
      near black, and the plinth around the piers catches a soft red pool (`district_30`-`district_33` are
      the same four views).
    - **The red core must never poke above the ring** — the crown of the monument is concrete, not a red
      plate (that was the first build's bug).
    - Material: `[GLOW] core material: emissive S_Emissive00 tex=CoopRedGradient, boost=0,90,
      texcoord=(1.00, 1.00, 0.00, 0.00)` (**v0.9.15: half of the v0.9.14 `1,80`** — the user asked for
      −50 %). If a future build logs `core material: unlit …` or
      `Standard + emission map` instead, the game's emissive shader is gone — the red will still read, it
      just will not bloom. The full shader list is dumped once per build (`[GLOW] 65 shaders in the build:
      …`) for exactly this case.
    - **Walk onto the roof** (climb the stair hall to the top floor of the block at 122/118) — the plinth is
      walkable, the piers have colliders, and the roof around the monument is solid. `[District] Memorial =
      false` removes it without touching the layout (`built 36 buildings, 0 skipped plots, 23 bridges` is
      the same either way).
    - **`[District] MemorialGlow` (v0.9.15, default 0,5)** scales the whole glow with one number: the
      emissive `_EmissiveBoost` (1,8 × value) **and** every red point light (2,4 / 1,3 / 1,8 × value). 1 =
      the v0.9.14 look, 0,5 = half, 0 = the red core goes black (the piers stay). Live, no rebuild.
17. **v0.9.15 — the flight ceiling and the dimmer glow**:
    - **Fly up past 120 m.** Take the propeller ("леталка") in hand, hold **Space**: you must keep climbing
      straight through the old 120 m ceiling and be able to reach the memorial roof (**≈276 m world**:
      platform 85,5 + block 190,6 + 0,26). The log says `[FLY] propeller ceiling 120 m -> 600 m (3 constants
      replaced in Propeller.Update)` — **3** is the number of `ldc.r4 120` in `Propeller.Update`; `0` means
      the game's IL changed and the old ceiling is still in force. `[FLY] camera far clip 1000 -> 1400 m`
      must also be there (the stock far plane was **1000**, so without it the world would clip at ~1 km).
      While at 600 m the ground must still be drawn (fog hides the far edge, no hard cut). Set
      `[Game] FlyMaxHeight = 0` in the cfg to get the stock 120 m back — no rebuild.
    - **The glow is half.** `district_30` head-on and `district_31` (the low 3/4 of the reference) must still
      read as bright red slits with a hot orange base, just visibly dimmer than the v0.9.14 shots —
      `[GLOW] … boost=0,90` and `[MEMORIAL] … 6 red lights at 0,50x (1,20 / 0,65 / 0,90)` are the evidence.
      `[District] MemorialGlow = 1` restores the old level, `0,25` goes to a quarter.
18. **v0.9.16 — the stairs reach the next floor, flights 2-3 m, coarser platform**:
    - **Climb a stair in any house, and one in a House of Soviets tower.** No stair may end under a solid
      plate: the shaft is cut through **every** floor slab from the same plan the flights are built from
      (`PlanStair` → `Interior` hole + `StairsZ`). Evidence in the log, no walking needed:
      `[STAIR] headroom: 33 blocks, 1056 rays 1,84 m above the treads, 0 blocked` (a hit would mean a
      ceiling under 2 m in the player's face), `[PROBE] house 0 stair flight 1 (3,00 m long) above: 86,90 m
      -> bldg` (the ray leaves the shaft and runs to the roof), `stair landing 1: 13,75` and `half landing:
      10,95` (both landings still solid where you walk).
    - **Flights are 2-3 m in every building**, the House of Soviets included: `[STAIR]` prints one line per
      block — `flight 3,00 m, 16 risers of 0,175 m, tread 0,188 m, landings 2,40 m, shaft z 5,70..11,10` —
      and the towers went **1,68 → 3,00 m**, `9 risers of 0,178 m, tread 0,333 m`. The old
      `old flight X,XX m` half of the line is what v0.9.15 built there (3,04-9,68 m in the blocks), so the
      line proves the change per building. A shallow hall with a wide corridor is clamped to a 2,0-2,9 m
      flight with 0,7 m landings instead of being skipped.
    - **Platform grain is 0,2 of v0.9.13**: `[LOOK] district material: … uv 0,35/m …; platform floors x0,8 =
      uv 0,28/m` (was `x4 = uv 1,4/m`). Stand at the dam joint: the deck now reads as a smooth slab with
      soft broad mottling at 50 m, and still as fine pitted concrete from 2-3 m up (`map/grain_ours` vs
      `map/grain_wide`, the latter on the dam's own ruler). Knob `[District] PlatformGrain` (1 = the plain
      house grain, 4 = the old +400 %) — live, no rebuild.
    - Debug poses for the stair are right after `district_21`: from the half-landing looking up the upper
      flight (the user's screenshot) and from the corridor landing looking up the lower one.
19. **v0.9.17 — the House of Soviets: the hall is filled, the storeys are 6,4 m, the windows are monumental**:
    - **The ground floor must not be an empty cavern any more.** Walk in through the 8,4 × 6,1 m porch onto
      the hall floor: two **37,7 × 37,7 m** tower masses stand right there, ~3 m behind the front gallery, and
      can be entered and climbed from the hall. The log is the proof, no walking needed:
      `[HOUSE] podium 107,3x49,3 m, 4 storeys of 6,4 m (top 35,8 m), towers 37,7x37,7 m at ±26,1 m with 30
      storeys of 6,4 m on the hall floor (10,2 m), windows 4,75x3,00 / 4,00x4,10 m, every second one, central
      strip 127,6 m` plus `[PROBE] House hall tower front (+z): 2,92 m (bldg)`, `(-z): 3,29 m (bldg)` and
      `[PROBE] House hall floor: 10,15 m (podium)` — a `MISS` or a 20+ m answer means the towers are floating
      again (that was the v0.9.16 bug: the +10 m plinth counted twice). Full run `[PROBE] done: 29 ok,
      0 misses`.
    - **Storeys are twice the old height everywhere in the House**: the podium is 4 × 6,4 m = 25,6 m tall (top
      35,8 m in district space) and the towers are 30 × 6,4 m = **192 m** above the hall floor, i.e. the
      tallest thing in the district — the skyline shot `district_5` must show the two towers above every
      block. The House's own stair follows: `[STAIR] 37,7x37,7 m, corridor 2,2 m clear: … flight 3,00 m,
      18 risers of 0,178 m, tread 0,167 m, landings 6,91 m` (the blocks stay 16 risers of 0,175 m).
    - **Windows**: the podium's openings are **4,75 × 3,00 m** (2,5 × wider and 2 × taller than 1,9 × 1,5) and
      there are **8** of them per long facade instead of 15; the towers' are **4,00 × 4,10 m** with a 1,6 m
      sill, and **every second one is removed** on all four facades (`winSkip = 2`), i.e. 4 per long facade
      and 2 per band per end wall. Everything else — every block — is untouched (`built 36 buildings,
      0 skipped plots, 23 bridges, 70980 quads`).
    - **Evidence shots**: `district_5` (the district from the city — the two towers dominate), `district_6`
      (3/4 lit), `district_7` (the hall from high up its west aisle), `district_36` (the same aisle at eye
      level: a tower wall of rooms on one side, the podium's windows on the other), `district_37` (the walk up
      the approach avenue — podium, both towers, central strip), `district_38` (the podium facade head-on).
      Do not judge the window rhythm from a distance-only still: a 4 m opening at 150 m is a few pixels.
20. **v0.9.18 — the penthouse (four-storey clock tower) on the block on the peninsula**:
    - **Fly to the roof of the lone tower on the western wing** (the propeller, `[Game] FlyMaxHeight = 600`;
      the roof plate is at **160 m** world) and land on it: there is now a **four-storey attic drum**
      (5,6 + 5,6 + a **double-height clock storey** 10,2 + 5,6 = 27,0 m), **one patinated clock dial per
      face (r = 4,40 m)**, a heavy concrete cornice, a **truncated mansard in the game's own rusty metal**
      with 28 flush skylights, and a **glass lantern** on its flat top. It is the reference photo, built
      out of the district's own concrete, so it reads as part of the game.
    - **The log is the evidence**: `[PENTHOUSE] clock tower on the block on the wing (29,4x31,6 m, 26 floors,
      top 159,8 m at -40,0,225,0): drum 25,8x28,0 m (inset 1,8 m), 4 storeys 5,6 + 5,6 + 10,2 (clock) + 5,6
      = 27,0 m, atrium 9,2x9,2 m …, cornice 27,4x29,6 m at y=187,6, mansard 12,9 m …, platform 7,2x7,9 m at
      y=200,5, glass lantern 3,8x4,1x3,0 m, 4 clock dials r=4,40 m` and
      `[PENTHOUSE] clock material: emissive S_Emissive00 tex=CoopClockDial (1024x1024), boost=0.35`. A future
      build logging `clock material: unlit …` still works, it just will not bloom.
    - **Walk in and up**: the door is the **1,9 × 2,9 m** opening in the middle of the `+z` face (the side
      facing the district). The ground floor is one open hall, the storeys above have a **9,2 × 9,2 m
      atrium** cut through every slab with 1,05 m parapets, a glass lift shaft in the middle and a stair in
      the east band — the tower can be climbed on foot to the clock storey.
    - **Probes, not renders**: `penthouse floor (inside the drum): 160,01 m (bldg)`,
      `penthouse clock storey wall (+z): 9,00 m (bldg)` (a ray from 9 m out at clock height stops exactly on
      the wall, i.e. it did **not** slip through a window), `penthouse lantern roof: 203,77 m (bldg)` and
      `penthouse roof platform: 200,53 m (bldg)`. Full run `[PROBE] done: 33 ok, 0 misses`. The layout is
      untouched: `built 36 buildings, 0 skipped plots, 23 bridges, 71056 quads`.
    - **Shots**: `district_39` (the whole tower 3/4 from the sea), `district_40` (head-on at the clock
      storey — the reference's own framing), `district_41` (the mansard from above: four slopes, the
      skylights, the lantern), `district_42` (the atrium from the ground floor). **Known and accepted**: from
      outside the windows read **pale** rather than dark — the drum is one bright room, so an opening shows
      its far wall (the level's probe SH; the lamps changed nothing, measured at 1,5 and 0,8).
    - `[District] Penthouse = false` (live, no rebuild) leaves the roof plain; the RNG is not touched, so
      the rest of the district is identical.
21. **v0.9.19 — the legs (two blocks on splayed pylons)**:
    - **Walk to the crest and look at the two blocks on the seaward side of the dam joint, right of the
      approach avenue** (the pair the user marked in screenshot 1): their **first three storeys are gone** —
      the house now hangs on a **3,0 m transfer girder** over a ring of **V-shaped concrete pylons**
      (1,8 × 1,5 m on the deck → 3,6 × 2,4 m at the girder), and the deck between them is an **open,
      lamp-lit colonnade you can walk through**. It is the reference photo 2 in the game's own concrete.
    - **Walk in**: stand under the block, find the **6,8 × 9,8 m core** in the middle — a 2,6 m door on
      `±x`, then **three flights** up the shaft to the first floor (the house's own corridor, rooms and
      stair start there). The base is the only entrance; the old street doors and steps are gone with the
      three storeys.
    - **The log is the evidence**: two `[LEGS]` lines — `41,4x22,7 m block at 266,7,26,0: 16 pylons … girder
      3,0 m at y=14,0, core 6,8x9,8 m …, first floor y=17,0, floors 23 -> 20` and `23,6x28,0 m block at
      310,2,26,0: 12 pylons … floors 31 -> 28` — plus `built 36 buildings, 0 skipped plots, 23 bridges,
      70752 quads` (the old 71056 minus the three removed storeys' walls, plus the legs).
    - **Measured, not trusted**: `[PROBE] under a block on legs (deck): 0,00 m (deck)`,
      `first floor over the legs: 16,95 m (bldg)`, `core stair landing 1: 5,75 m (bldg)` and
      `transfer girder: 8,15 m (bldg)` (a horizontal ray from 9 m out is stopped 0,85 m in front of the
      facade — the girder overhangs). Full run `[PROBE] done: 37 ok, 0 misses`.
    - **Shots**: `district_43`-`district_45` — the pylons from the street, the whole house on its legs from
      the crest side (the reference's framing) and the colonnade of V-legs from deck level under the
      girder (v0.9.20 re-framed them now that the pair is one house).
    - **Nothing else changed**: only these **two** blocks are affected — the other 31 blocks, the House of
      Soviets, the memorial, the clock tower, every bridge and the layout are identical; moving the
      treatment to another pair is one `bldgs[i].legs = true;` line in `Layout()`.
22. **v0.9.20 — one house on legs, the south-east corner levelled**:
    - **The pair on legs is one building in plan now**: `65,0 x 28,0 m` at `x 246..311, z 26`, yaw 0, no gap
      between the halves — the west half stays **23 floors**, the east half **31**, i.e. the same house
      with one part taller (the heights were not touched). The open base under it is **one** colonnade:
      the ring of pylons is not emitted on the shared face. Log: `[LEGS] the pair is one house in plan:
      41,4 + 23,6 = 65,0 x 28,0 m at x 246,0..311,0, z 26, yaw 0 (west 23 floors, east 31 floors -
      heights unchanged)`, then 14 and 10 pylons, and `built 36 buildings, 0 skipped plots, 22 bridges,
      71068 quads` (was 23 bridges).
    - **Walk the platform's south-east corner next to the round hole in the sea**: the **5 m ledge with
      its parapet is gone** — the +5 m east terrace is set **12 m** back from the crest and the corner is
      one flat plate, **flush with the granite crest you walk in on** (top 0,36 m, i.e. the crest's own
      level), with a 2 m chamfer down to the base deck on its west. You should be able to walk from the
      crest across the corner and up to the terrace's wall without any ledge, step or railing on the sea
      side; the corner next to the hole is walkable and the round hole is in open water, clear of the
      platform.
    - **The log is the evidence**: `[DISTRICT] south-east corner: the +5 m east terrace is set back 12 m
      from the crest (50 x 12 m of flat corner at the round hole instead of a bare 5 m ledge), the rest of
      the terrace is unchanged` and `[DISTRICT] corner pad: 48 x 12 m at the south-east corner, top
      0,36 m …, chamfered to the base deck on the west, 12 quads`; probes
      `south-east corner pad (crest level): 0,36 m (corner_pad)`, `south-east corner, behind the pad
      (+5): 5,00 m (terraces)`, `east terrace still at +5 (mid band): 5,00 m (terraces)`,
      `base deck west of the pad: 0,00 m (deck)`, full run `[PROBE] done: 41 ok, 0 misses`.
    - **Nothing else changed**: the other blocks, the House of Soviets, the memorial, the clock tower, the
      terraces' extents elsewhere, the ramps and the stairs are untouched. Knob
      `[District] CornerApron` (12; `0` restores the old 5 m ledge, live, no rebuild).
      Shots `district_46` (the edge at eye level walking east to the hole), `district_47` (from over the
      water — the corner plate meets the crest flush), `district_48` (the crest junction with the hole in
      the frame), `district_49` (the corner in plan).
23. **v0.9.22 — the corner is smooth and the roof has five people on it**:
    - **Walk the platform's south-east corner from the crest** (in from the city along the granite, then
      across the corner to the terrace's wall at +5): the floor must be **one continuous surface** — the
      granite, then the concrete emerging from under it, then a constant ~6,6 % ramp down to the deck. No
      ledge, no lip, no seam kerb, and nothing that reads as two plates at different heights. The log is the
      proof, no walking needed: `[PROBE] corner walk x-profile z=6 … biggest 0,5 m step 3,3 cm at 321,0 m`
      and `[PROBE] corner walk z-profile x=340 … biggest 0,5 m step 1,9 cm at -0,5 m (2 jump(s) over 1 m: a
      wall, not the floor)` — anything over ~5 cm inside one 0,5 m step is a step again. Probes:
      `south-east corner pad (its top; the granite under it is 0,32): 0,36 m (corner_pad)`,
      `base deck west of the ramp's foot: 0,00 m (deck)`, `south-east corner, behind the pad (+5): 5,00 m
      (terraces)`, and `[DISTRICT] corner pad: … ONE smooth height field …` (`367 quads`).
    - **Fly up to the House of Soviets' podium roof** (the propeller; the roof plate is at **35,75 m**, i.e.
      y ≈ 121 in the world) and walk its **front (city) edge**: there are **five people** standing 1,25 m
      inside it, **20 m apart**, facing the city — they must read as the game's own NPCs: a big **pale bone
      mask** head with small dark eyes and a mouth, a muted palette with **one orange jumpsuit**, chunky
      boots and **long thin splayed fingers**. `[FIGURE] five figures on the House of Soviets' roof … 1098
      quads in 12 colours` is the log line; `[PROBE] House roof, at the middle figure's feet: 35,75 m
      (podium)` and `[PROBE] House roof figure 5 (chest, from the city side): 5,88 m (figures_orange)` prove
      they stand on the roof and not inside it.
    - **Shots**: `district_57` (the crest approach — the flattest, most `lip`-revealing angle),
      `district_58` (on the plate, looking back along the seam), `district_59` (the row from the plinth
      below), `district_60` (the group), `district_61`/`district_63` (one figure close up: mask, fingers),
      `district_64` (the orange worker head-on — the palette match).
24. **v0.9.23 — the corner's south side is walkable (the plate the user outlined is gone)**:
    - **Walk into the corner from the base deck, from the south, next to the +5 terrace's wall** — the way
      the user's red outline points. The pad used to end there in a bare **0,25-0,36 m vertical face** at
      `z = 12,3` (taller than the controller's 0,30 m `stepOffset`, so it was a wall), with its underside
      visible. Now the surface slopes down and dives into the deck slab, so you can walk up from any side.
    - **The numbers are the proof**: `[PROBE] corner steps: 0 lip(s) of 0,20-1,60 m inside the walkable
      corner (x 310..374, z 0,3..11,9)` (v0.9.22 printed `25,3-36,5 cm at (324,12) → (324,14)`) and
      `[PROBE] corner faces: 22738 rays … at 0,25 m … biggest jump between neighbours 1,6 cm …, 0 jump(s)
      over 8 cm` — a continuous surface, its steepest point 6,4 %. `[PROBE] corner south bevel z-profile
      x=325 (z 8..20)`: `0,28 … 0,26 0,23 0,16 0,07 0,00`; at x=316 it is `0,00` the whole way (below the
      deck), so there is no edge left to trip on.
    - **The top no longer reads as a plate**: `[DISTRICT] corner pad: … flat 0,345 m …` — **2,5 cm** over the
      crest's granite instead of 4,5. Shots: `district_65` (the user's own framing: deck south-west of the
      corner, the terrace's wall left, the crest's granite and the city's mast right) and `district_66`
      (the same transition walked straight up from the south). Floor, bevel and stone must be one surface.
25. **v0.9.24 — the five figures are the game's own characters** *(superseded by 26: the figures are new
    characters now, and the shot numbers below frame them)*:
    - **Fly to the House of Soviets' podium roof** (35,75 m) and walk the front (city) edge: the five are no
      longer box people. Each one is the game's own body (`SM_A_Enki`, warped per figure) with **a different
      one of the game's own heads** — the pale bald mask of the game's worker on the broad man in the
      orange jumpsuit, the woman's headscarf, the official's bald head, the builder's curly hair, the
      engineer's grin. Close up there is a real photo-scanned face on each, so **no two expressions** are
      alike; from the plinth below they are five people along the edge, five heads tall (1,70-1,92 m).
    - **The log is the evidence**: five `[FIGURE] figure_N_<name>: … verts, N materials, H,HH m tall, feet at
      local (x, y, z)` lines (3 materials = body + face + hair, except the official whose head has no
      separate hair mesh, 2), then `[FIGURE] five figures on the House of Soviets' roof … 5 of 5 loaded from
      …\BabbdiCoop\figures (the game's own models: SM_A_Enki's body warped per figure + five different heads
      … re-textured in a new palette, 1,70-1,92 m tall, five heads high)`. A `[FIGURE] figure N … failed: …`
      or `is missing — skipped` line means a GLB did not ship — `build.ps1` must print
      `Figures: 5 GLB in …\BepInEx\plugins\BabbdiCoop\figures`.
    - **Probes**: `[PROBE] House roof, at the middle figure's feet: 35,75 m (podium)` (they stand on the roof
      plate, not in it) and `[PROBE] House roof figure 5 (chest, from the city side): 5,66 m
      (figure_5_worker_(jumpsuit))` — the collider name in the answer is the figure itself, i.e. a ray from
      the city at chest height is stopped by the body. Full run `[PROBE] done: 43 ok, 0 misses`, and the
      layout is untouched (`built 36 buildings, 0 skipped plots, 22 bridges, 71068 quads`).
    - **Shots**: `district_59` (the row from the plinth: five figures along the edge against the sky),
      `district_61` (the woman at 1,70 m: ochre jacket, teal trousers, headscarf), `district_62` (along the
      row from the roof — the official in the dark suit nearest), `district_63` (the woman at eye level, the
      row receding towards the worker), `district_64` (the orange worker head-on: the game's own jumpsuit and
      pale mask — the palette match to the game's cast).
    - **What is inherent in the game's asset, not a bug**: a large flat hood panel lies on the chest (the body
      is a hooded jacket), the hands are the game's pale gloves rather than skin, and the tracksuit's
      photographic print survives the repaint as soft shading on the thigh.
26. **v0.9.25 — the five figures are new characters (supersedes 25)**:
    - **Fly to the House of Soviets' podium roof** (35,75 m) and walk the front (city) edge. The five must
      read as **new people in the game's own style**, not as re-skinned copies of the game's cast: a woman
      scientist in a **white lab coat** with a blue shirt at the collar, a dark bob and a clipboard; a docker
      in a **navy beanie** with stubble and an **orange hi-vis vest** over an olive jacket; a **bald official
      with a walrus moustache**, an olive jacket and a red tie; an **officer in a peaked cap** and an olive
      greatcoat with a belt; a **bearded soldier in a winter beanie**, a camo jacket and a load-bearing vest.
      They face the city (the bodies used to be turned 180°), are **2,05-2,21 m** tall (1,70-1,92 before, the
      user asked for +20 %) and are **five heads high** like the game's own people.
    - **Styling that must be visible up close**: the eyes are **narrow dark slits** with a dim sclera and a
      lash line, never round white balls; the official's and the soldier's moustaches are **one continuous
      band** with a notch under the nose and drooping ends, not two dark ellipses; the officer's peak sits
      **under the cap's band and above the brows** (the forehead is visible, no black slot); and there is
      **no white band at the ankles** — that was a see-through gap between two hollow tubes (the roof showing
      through), not a texture, and the trouser now ends 2 cm inside a capped boot.
    - **The log is the evidence**: five `[FIGURE] figure_N_<name>: … verts, 2 materials, H,HH m tall, feet at
      local (x, y, z)` lines (always **2** materials now: one `skin` + one `cloth` primitive on a single
      2048x1024 atlas), then `[FIGURE] five figures on the House of Soviets' roof … 5 of 5 loaded from
      …\BabbdiCoop\figures (new characters in the game's own style … 2,05-2,21 m tall, five heads high)`.
      A `[FIGURE] … failed:` / `is missing — skipped` line means a GLB did not ship — `build.ps1` must print
      `Figures: 5 GLB in …\BepInEx\plugins\BabbdiCoop\figures`.
    - **Probes**: `[PROBE] House roof, at the middle figure's feet: 35,75 m (podium)` (they stand on the roof
      plate) and `[PROBE] House roof figure 5 (chest, from the city side)` stopped by the figure's own
      collider. Full run `[PROBE] done: 43 ok, 0 misses`; the layout is untouched (`built 36 buildings,
      0 skipped plots, 22 bridges`).
    - **Shots**: `district_59` (the row from the plinth — five figures along the edge against the sky),
      `district_61` (the official: bald, moustache, olive jacket, red tie), `district_62` (along the row, the
      officer nearest), `district_63` (the official at an angle, the row receding), `district_64` (the soldier
      head-on), `district_67` (the scientist: white coat, blue shirt, bob, clipboard, navy trousers) and
      `district_68` (the docker: navy beanie, stubble, orange vest, navy trousers).
    - **Known and accepted**: the faces are painted at an atlas resolution where an eye is ~38x28 texels, so
      they read as **masks** (a pale face with dark slits, a moustache band, a beard border) rather than as
      portraits — which is how the game's own cast reads from a metre away. The figures are **purely local**:
      each peer renders its own `figures\*.glb`, nothing about them is networked.
27. **v0.9.26 — the figures' anatomy: four heads, huge hands, a real peaked cap**:
    - **Fly to the House of Soviets' podium roof** (35,75 m) and walk the front (city) edge. The five must
      read as the game's own people at a glance, not as slim mannequins: a **head 25 % of the height** (≈4
      heads, not 5), **egg-shaped** (0,755 as wide as it is tall), **carried in front of the shoulders and
      nodded 6° forward**, **no neck** — the head sits straight on the collar.
    - **The limbs are sticks and the hands are the signature**: shoulders narrower than the head, sleeve and
      trouser radii cut by 12–15 % (a thigh is 12 cm across, not 25), and **huge pale hands with four long
      thin splayed fingers plus a thumb**, the fingertips reaching mid-thigh. Only the **officer** wears
      **white dress gloves**; the other four have bare skin hands. If a figure's hands read as a dark blob,
      its `glove` colour is not the skin colour.
    - **The faces**: big **dark hollow eyes** in a dark socket with a lash line (never a wide white sclera
      around a small pupil), a slim nose, one continuous moustache band with a notch under the nose (the
      official) and a dim row of teeth (the official only).
    - **The officer's cap** must read as a **peaked cap**, not a pot lid or a beret: a snug dark band, a
      crown that flares over it and rises in a dome, and a **visor** under the band, above the brows — the
      forehead stays visible from a camera at chest height (the first attempt dropped the dark underside
      straight over the eyes).
    - **The scientist's clipboard** is a dark board with a **pale paper sheet** and a metal clip, 9 × 13 cm —
      a bare dark slab at chest height reads as a satchel.
    - **The log is the evidence**: five `[FIGURE] figure_N_<name>: … verts, 2 materials, H,HH m tall, feet at
      local (x, y, z)` lines — **2,05 / 2,12 / 2,15 / 2,25 / 2,14 m** (the officer is tallest because of his
      cap) — then `[FIGURE] five figures on the House of Soviets' roof … 2,05-2,25 m tall, four heads high`.
      Full run `[PROBE] done: 43 ok, 0 misses`; the layout is untouched (`built 36 buildings, 0 skipped
      plots, 22 bridges`).
    - **Shots**: `district_59` (the row from the plinth), `district_61` (the official up close — bald egg
      head, big dark eyes, moustache, olive jacket, red tie, pale splayed hands), `district_62` (along the
      row), `district_63` (the official at an angle), `district_64` (the soldier: beanie, beard, camo, vest,
      belt), `district_65`/`district_66` (the south-east corner, unchanged), `district_67` (the scientist:
      white coat, blue shirt, bob, clipboard with paper), `district_68` (the docker: navy beanie, stubble,
      orange hi-vis vest with reflective strips) and `district_69` (**the officer**: banded peaked cap with a
      visor, olive greatcoat, belt, white gloves). Every close-up is framed off `FigFeet` — **3,2 m** in
      front of the row at 1,55 m, aimed at the chest; the old fixed offsets left a 2,1 m figure at a third
      of the frame.
    - **Known and accepted**: at an atlas where an eye is ~38x28 texels the faces still read as **masks** —
      which is what the game's own cast looks like from a metre away. The figures are purely local (each
      peer renders its own `figures\*.glb`), so they are not synced.
28. **v0.9.27 — real hands, white eyes, a lumpy skull, boots that fit**:
    - **Look at the hands first** (fly to the roof, stand 2 m from any figure, `district_61`/`district_67`):
      the palm must be a **rounded wedge** with four **long thin fingers growing from the knuckle line**, a
      slight fan and a curl at the tips, and a **thumb** off the palm's edge pointing down-forward. If it
      still looks like a paddle with a rake under it, `add_hand` regressed — the palm is `B.tube` of three
      rings and `knuck = wrist + d·0.062u`.
    - **The eyes are white, with no pupil and no iris** (`district_61`/`district_68`): a pale ball in a dark
      socket, slightly **yellow** on the docker and the soldier. If a pupil shows, the `iris`/`pupil` block
      came back into `skin_at`. They read very bright — that is the game's own look.
    - **The faces are uneven**: a brow ridge, deep sockets, **proud cheekbones that widen the face** and a
      jaw that narrows under them (the front silhouette is a diamond, not an egg), a big nose, a low
      frequency lumpiness over the skull and **ears that stick out ~2 cm** with a darker concha. The bald
      official must have **skin-coloured ears** (a `part == "ear"` branch in `shade`), never the old dark
      slabs, and only a **soft** shadow at the temples, never a black bar.
    - **The boots are ordinary work boots**: ~28 cm tall, 13,7 cm across, with the sole flush on the floor
      (the figure must not float). 41 cm / 17 cm was the v0.9.26 ski boot.
    - The log is the evidence: `BabbdiCoop v0.9.27 loaded`, `[FIGURE] five figures … 5 of 5 loaded`,
      `[PROBE] done: 43 ok, 0 misses`, and `build.ps1` printing **`Figures: 5 GLB`**.
    - **Known and accepted**: the faces still read as **masks** at this atlas resolution, and the eyes are
      bright enough to look lit — both match the game's own cast. The figures are purely local.
29. **v0.9.28 — five different faces, soft skin, the bob fixed**:
    - **The therapist's hair must be solid** (walk round her): no pale rectangle at the side (an ear used to
      poke through the bob — a bob has no ear now) and no pale notch at the crown (the bob's clearance is
      7,5 % and the skull's lumpiness is damped above `ny 0,45`). Offline `zz_bob_back.png` shows the back
      of the bob from four angles.
    - **The head textures must look soft**, like the rest of BABBDI: the skin half of the atlas is halved,
      upscaled and blurred (`soften_skin`), so there is no procedural grain on a face. The GLBs are
      **0,87-1,14 MB** — if they grow back to ~1,9 MB the softening pass stopped running.
    - **The five must be five different people**, not one head in five hats: read the faces side by side at
      `district_61`/`64`/`67`/`68`/`69`. The woman's head is short, wide and soft with a small nose; the
      official's is heavy-browed with jowls and a big moustache; the officer's is long, deep-set and
      frowning; the docker's is long and weathered with grey brows; the soldier's is broad and young with
      dark thick brows. Each also carries a **slight asymmetry** (one brow heavier, one socket deeper, the
      nose bent a millimetre, the mouth tilted) — `face`, `asym`, `fasym` in `FIGURES`.
    - The log is the evidence: `BabbdiCoop v0.9.28 loaded`, `[FIGURE] five figures … 5 of 5 loaded`,
      `[PROBE] done: 43 ok, 0 misses`, `Figures: 5 GLB`.
    - **Known and accepted**: at two metres they still read as painted masks rather than portraits, which is
      what the game's own cast looks like.
30. **v0.9.29 — five sizes, trainers and dress shoes**:
    - **The five are visibly different heights**: 2,05 / 2,23 / 2,50 / 2,70 / 2,35 m — the small woman, the
      **stocky** (short and broad) docker, then the tall official and the towering officer. The log names
      them: five `[FIGURE] figure_N_<name>: … X,XX m tall` lines. Every close-up is framed off the figure's
      **own** height (`FigHeight` → 0,72 h camera / 0,47 h target), so nobody is shot at the waist; the
      chest probe uses 0,63 h.
    - **The docker wears white trainers** (`district_68`): a pale upper, a **thick white midsole**, a white
      toe cap. **The official wears black dress shoes** (`district_61`): low, sleek, a thin sole and a heel.
      The other three keep boots (the officer his tall ones).
    - **The fat docker** must look stocky, not inflated: hips/depth 1,20/1,24 and shoulders 1,24 so the
      arms still hang *outside* the belly — at 1,20 hips with the old 1,02 shoulders the sleeve's root sat
      inside the torso.
    - The log is the evidence: `BabbdiCoop v0.9.29 loaded`, `[PROBE] done: 43 ok, 0 misses`,
      `[FIGURE] five figures … 2,05-2,70 m tall`, `Figures: 5 GLB`.
31. **v0.9.30 — necks, lips and five skin tones**:
    - **Every figure has a visible neck** (`district_61`/`67`/`68`/`69`): ~9,5 cm of skin between the jaw
      and the collar. The head is 8 % smaller than in v0.9.29 to pay for it, and the shoulder line did not
      move — if a figure suddenly looks like a head on a pole, `landmarks()` regressed to
      `chin = H - head_h`.
    - **The mouths are lips**, not a line: a cupid's-bow upper lip, a fuller lower lip with a lit band and
      a dark seam between them, in a muted per-figure colour (`lips`, `lip_h`). *v0.9.31 supersedes the
      colours: only the scientist keeps a rosy mouth, the men's lips are a fold of the same skin.*
    - **Five distinct pale skin tones**: pink-cool scientist, sallow docker, ruddy official, **cold
      grey-white officer**, olive-yellow soldier. If two of them read as the same beige, `S[...]["skin"]`
      was reverted.
    - The log is the evidence: `BabbdiCoop v0.9.30 loaded`, `[FIGURE] five figures … 5 of 5 loaded`,
      `[PROBE] done: 43 ok, 0 misses`, `[FIGURE] … 2,04 / 2,23 / 2,50 / 2,72 / 2,35 m tall`,
      `Figures: 5 GLB`.
32. **v0.9.31 — the men's mouths are folds, not lipstick**:
    - **Only the scientist has a rosy mouth.** The four men's lips must read as a slightly darker fold of
      the *same skin* (26 % blend of `skin × 0,96/0,86/0,85`), thin, with a soft seam and a shadow under
      the lower lip — that is what the reference photo shows. If a man's mouth is a red shape again,
      `lip_amt` or a stray `lips` key in `FIGURES` is to blame.
    - **The official's mouth is closed** — no teeth (`teeth=False`): the tooth row was a hard-edged
      rectangle of pale texels and the most cartoonish thing on the roof.
    - **Shots**: `district_61` (the official), `district_69` (the officer — a bare thin line), `district_64`
      (the soldier), `district_67` (the scientist keeps her rose mouth).
33. **v0.9.32 — a real torso, and two different mouths**:
    - **The docker must look fat, not like a barrel**: a chest under the vest, the **gut as the widest
      point** at half his height, a **waist tucked in above it** and a narrower jacket hem
      (`district_68`). **The soldier must be a V**: broad chest and shoulder yoke, narrow waist, belt
      (`district_64`). If either comes back as a straight tube, `torso_prof` (the docker's is his own
      11-ring list) or `DEFAULT_TORSO` was lost.
    - **The arms hang from the shoulder's edge**, not out of the ribs — `shx` is derived from the yoke
      (`prof_at(0.700)·0,96`). A sleeve whose root sits inside the chest is the old fixed `0,094·H`.
    - **Two mouths**: the docker's is **wide** (`mouth_w 0,34`), the officer's **narrow** (0,21); both have
      big but **very faded** lips (`lip_h 1,25 / 1,05`, `lip_amt 0,16`) — no lipstick.
    - The log is the evidence: `BabbdiCoop v0.9.32 loaded`, `[PROBE] done: 43 ok, 0 misses`,
      `Figures: 5 GLB`.
    - Note for the offline previews: `render3.py` now actually renders front / 3-4 / back / side (the view
      name used to be ignored, so every sheet showed four identical columns).
34. **v0.9.33 — necks, collars and the officer's peak**:
    - **The neck must grow out of the jaw**, not end behind the skull: its top ring is at
      `chin + 0,11·head_h` on the head's own forward axis (`head_z·0,85`). A neck that shows a horizontal
      cut under the jaw, or a column behind the head in profile, is the old `head_z − 0,22·hrz` placement.
    - **Every collar is a cone from the shoulder yoke** (base at 0,694·H with the jacket's radius), so it
      continues the jacket instead of floating above it or sinking into the neck. A dark band hanging in
      mid-air on the neck means the old `head_h·0,135` rings are back.
    - **The vest stops at the collarbone** (0,712·H) — it must not climb the neck like a turtleneck.
    - **The officer's peak** is now ~15 cm of forward reach (was 6 cm) with a real 6 mm underside; from a
      low camera it reads as a proper military peak, not a black wedge across the forehead.
    - The log is the evidence: `BabbdiCoop v0.9.33 loaded`, `[PROBE] done: 43 ok, 0 misses`,
      `Figures: 5 GLB`.
35. **v0.9.34 — solid collars, a greatcoat collar, pointed shoes, real trainers**:
    - **No collar may be see-through from inside**: each one is a closed band (outer wall → top rim →
      inner wall → bottom rim). A collar you can look through is the old single-surface `B.grid`.
    - **The officer's collar** is 2,5× taller (up to 0,792·H, wrapping the jaw) and 1,5× thicker
      (`th = 0,009·H`); it must clear the skull (radius `nr·1,85` against the head's `0,053·H`), not
      intersect it.
    - **The officer wears pointed shoes** (`shoe="pointy"`, no more tall boots) and **the docker rounded
      trainers** — both are tubes along the foot's axis with a matching sole tube. A rectangular shoe is
      the old `B.box` and must not come back.
    - Shots: `district_69` (the tall collar, the pointed toes), `district_68` (the trainers),
      `61`/`64`/`67` (solid collars).
36. **v0.9.35 — the stair well and the street core**:
    - **Every house's stairwell now has an open well between the flights** (the user's screenshot 2): walk into
      a house, up to the stair hall, and look at the two flights — they are separated by a **0,66-0,73 m slot**
      that runs from the ground floor to the roof, with a **1,0 m rusty-metal handrail** and posts on each
      flight's well side and a guard across the slot at every floor and half-landing. The log states it:
      `[STAIR] … well 0,70 m (flights 1,98 m), shaft z 5,70..11,10`. Shots `district_70` (the foot of a flight,
      the user's own frame), `district_71` (down the well from the top floor), `district_72` (straight up the
      slot from the ground floor).
    - **The two houses on pylons can be entered now.** Take the core's door on the deck, climb the three
      flights: the top flight used to end **inside the first-floor slab**; it now tops out through a 1,7 x
      4,5 m opening into the house's **stair hall**, at the foot of the house's own first flight (and a doorway
      was cut in the corridor wall for it). Evidence: `[CORE] … block on legs: street stairlane x -1,90..-0,20 m,
      exit landing z 2,71..4,21 m, first-floor opening z -0,29..4,21 m …` and, per block,
      `[PROBE] legs core stair under the first floor at z 0,3` / `z 1,7`: **OPEN through the first-floor
      opening (1,2 m)**. Shots `district_73` (the core's top half-landing, looking up through the new
      opening), `district_74`/`district_75` (the opening and its railings from the corridor).
    - **No stair anywhere may hit a ceiling**: `[STAIR] headroom: 33 blocks, 1584 rays, 0 under 2,05 m;
      least clearance 5,40 m` — the probe measures the **real** distance to the first thing above every
      eighth tread of both flights (v0.9.36), so 5,40 m means a full storey of nothing above any flight.
37. **v0.9.36 — the point towers' stairs (the reported case)**:
    - **Walk into any point tower** (the square towers between the slab blocks — they have a horizontal
      band on the facade at every floor) **and climb its stairs**: the flight must now reach the floor
      and you must be able to step off. Before, the facade band was drawn as a solid 0,24 m plate over
      the whole plan at every storey, so it capped the stairwell hole: the flight ended 0,24 m under the
      floor and the last step was blocked (`least clearance 1,86 m` at tread 6/8 measured).
    - The log is the evidence: `[STAIR] headroom: 33 blocks, 1584 rays, 0 under 2,05 m; least clearance
      5,40 m`, and a block whose clearance is under 1,9 m dumps its whole geometry (the
      `[STAIR] geometry above flight 2 of …` lines) — there must be **none** now.
    - The band must still be visible from the street as a protruding ring at every floor (it is now four
      0,35 m boxes, 0,175 m proud of each facade), and no room may have a doubled floor.
    - Shots: `district_70` (a point tower's flight from the landing — open, no plate above), `district_71`
      (down the well), `district_72` (up the slot), `district_73` (down the well from the 3rd floor).
38. **v0.9.37 — the flights are plates, and nothing is see-through**:
    - **Look at a flight from the landing (or from any storey above), at the side**: it must read as a
      **thin concrete slab with steps on top**, its underside a single straight line from the floor at the
      bottom to the landing above — not a triangular mass filling the space under the steps.
    - **Look straight up at a flight from the floor below** (`district_76`): the underside must be a closed
      dark soffit; if you can see the railing/room through the flight, the old open wedge is back.
    - **The half-landing must have real concrete edges** over the well (a closed box, 0,26 m), not a paper
      edge you can see into.
    - Same for the street cores and the clock tower (`AtticStair` now = two `FlightZ` plates + a `Landing`).
    - Log: `BabbdiCoop v0.9.37 loaded`, `[STAIR] headroom: 33 blocks, 1584 rays, 0 under 2,05 m; least
      clearance 5,34 m`, `[PROBE] done: 43 ok, 0 misses`.
    - Shots: `district_71` (plates seen down the well), `district_74`/`75` (a flight from the landing),
      `district_76` (its closed underside), `district_77` (down a flight from the storey above).
39. **v0.9.38 — 2,5x thicker plates, closed ends**:
    - **The flight's plate must be 0,65 m** (2,5 × the 0,26 m floor slab) and its **underside parallel to
      the flight** all the way down, ending in a vertical "nose" face 0,65 m under the floor at the first
      step; the landing it meets is the same 0,65 m slab, so the two are flush.
    - **No open end anywhere**: the top of a flight (where it lands under the thinner 0,26 m floor slab)
      must show a closed vertical face, not a slot. Look up at a flight from the floor (`district_76`):
      a closed dark soffit with a thick visible edge, never a bright sliver behind it.
    - The half-landings and the cores' flights/landings use the same `StairPlate`, so the pylon houses and
      the clock tower match.
    - Log: `BabbdiCoop v0.9.38 loaded`, `[STAIR] headroom: 33 blocks, 1584 rays, 0 under 2,05 m; least
      clearance 4,95 m`, `[PROBE] done: 43 ok, 0 misses`.
    - Shots: `district_70` (the stairwell from the doorway — chunky plates), `71` (down the well), `76`
      (a closed underside), `77` (a flight from above).
40. **v0.9.39 — no transparent flight ends, no dithered patches**:
    - **Walk round a flight and look at it from every angle, especially from the landing below and from
      under the flight**: no bright sliver may appear *behind* an edge (that was a see-through) and no
      patch may shimmer/dither (that was two coincident faces). In `district_76` the underside is a closed
      dark soffit with its 0,65 m edge in frame.
    - The soffit must still be one smooth plane (the flight is built from closed per-tread cells whose
      bottoms are coplanar strips), and the plate stays 0,65 m thick.
    - Log: `BabbdiCoop v0.9.39 loaded`, `[STAIR] headroom: 33 blocks, 1584 rays, 0 under 2,05 m; least
      clearance 4,95 m`, `[PROBE] done: 43 ok, 0 misses`.
41. **v0.9.41 — the "transparent" strips were open wall segments (door lintels)**:
    - **Look up at any doorway from the corridor** (`district_26`): above the opening there must be a solid
      concrete head. Before this the wall segment above a door was two flat faces with no top/bottom, so
      the lintel was a slot through the wall and you saw the neighbouring room's dark ceiling as a thin
      strip — the artefact the user photographed four times.
    - **Measured**: `[DOOR] door heads: 4 rays just under a lintel, 4 hit the head, 0 open` and
      `[SKIN] stair undersides: 561 vertical rays under the flights, 0 passed through, 0 hit the wrong
      surface` (the flights themselves were never the problem).
    - Log: `BabbdiCoop v0.9.41 loaded`, `[PROBE] done: 43 ok, 0 misses`, `built 36 buildings, 0 skipped
      plots, 22 bridges`.
42. **v0.9.51 — the platform's parapet is cut at the corner**:
    - **Stand on the corner pad and look east along the +5 m terrace's south wall** (`district_81`, the
      user's own framing): the wall must run its whole length and end at the platform's edge, the pad's
      surface must run flat to that same edge, and **nothing may stand in front of the wall** — the 1,15 m
      east parapet that used to cross that strip is gone (cut out of the wall/coping mesh and the collider,
      not covered).
    - Measured: `[PROBE] corner z-profile x=378` must read ~0,3 for z 0-8 (the pad), **not 1,2**; only the
      terrace's rail (6,05 m) may show at z = 12.
    - **The 5 m strip is now open to the sea** — that drop is deliberate (the user asked for the piece to be
      removed). If a guard is wanted back, it has to be a *new* low kerb, not the old parapet.
    - Diagnostics kept: `[CORNER]` lists every mesh of ours and of the level at the corner with local
      bounds (the district is rotated, so world AABBs are transformed corner by corner first).
    - Log: `BabbdiCoop v0.9.51 loaded`, `[CORNER] listed 8 of our meshes and 15 level renderers …`,
      `[PROBE] done: 44 ok, 0 misses`. Shots: `district_81` (the user's view), `82` (from the crest,
      elevated), `83` (from over the sea) — the stair end-face shots moved to `84`/`85`.
43. **v0.9.98 — the air-hockey table on the House of Soviets' top floor**:
    - **Fly to the House's highest interior level** (world y ≈ 198 m; the cantilevered chevron annex
      above the west tower) or climb the west tower to its top floor and take the annex stair. The
      table must stand under a **warm pendant** in the middle of the hall, its **bone rink** with
      oxide-red face-off circles and steel-teal lines visible, the **brick-red body** with vents, the
      dark scoreboard arch and the black castors on the floor. `district_93` is the broadside,
      `district_94` the view from outside through a west window. The table is **not** a playable
      mini-game (no puck/mallet physics) — it is a local decorative prop.
    - **Measured, not assumed**: `[PROP] airhockey_table: 414 verts, 1 materials, size 2,58 x 1,82 x
      1,47 m`, `[PROP] table node: … world=(158.23, 198.05, 511.73), active=True, renderer=on`;
      `[PROBE] air-hockey table top: 114,37 m (airhockey_table)` (= 1,82 m over the 112,55 m annex
      floor), `floor beside it: 112,55 m (house_details)`, `side shot from 8,3 m: 7,01 m
      (airhockey_table)`.
    - **Double-sided**: the GLB is **414 verts / 268 tris** — a front copy plus a back copy with
      negated normals and reversed winding — because the built-in `Standard` shader has no `_Cull`
      property. Look at the table from below/behind: the underside, the inner faces of the arch and
      the scoreboard's back must all be drawn (no see-through). If the table is invisible, the GLB's
      index buffer is not a triangle list: the quads must be triangulated, not written as raw OBJ
      face corners.
44. **v0.9.99 — the table is 2× bigger and 20 % wider**:
    - **The same spot, a monumental table**: `[PROP] airhockey_table: 414 verts, 1 materials, size
      5,15 x 3,63 x 3,53 m` (was 2,58 x 1,82 x 1,47). The width is the source's Z axis ×1,20, baked
      into the GLB; the normals use the inverse-transpose, so the shading is unchanged. The pendant
      was raised to house-local 107,65 so it hangs above the 3,63 m scoreboard (shade bottom 107,50,
      arch top 106,18), and the map cameras were raised to frame the whole piece:
      `district_93` broadside (house-local -46, 106,4, -8 → -37,75, 104,4, 3) and `district_94` the
      west-window view.
    - **Measured**: `[PROBE] air-hockey table top: 116,18 m (airhockey_table)` (= 3,63 m over the
      112,55 m annex floor), `floor beside it: 112,55 m (house_details)`, `side shot from 8,3 m:
      5,72 m (airhockey_table)` (the face is 2,576 m from the centre). The table still has no puck or
      scoring physics — decorative, with a BoxCollider.
45. **v0.10.0 — the air-hockey table is a playable two-player activity**:
    - **Fly to the House's top floor** (annex hall, world y ≈ 198). The table is **5,15 × 2,91 ×
      3,89 m**; the source's scoreboard arch is visible while idle, but press **E** near the table and
      the camera detaches into an **orthographic top-down** view: the arch folds below the floor, the
      mouse moves the local mallet (red for the host, teal for the client) on the rink, and the dark
      puck is simulated by the host. Press **E** again to restore the camera. The score is top-right;
      «E — выйти из-за стола» is bottom-center; near the idle table the prompt is «E — аэрохоккей».
    - **Two players**: both peers need v0.10.0. Each flies to the table and presses E; the host's HUD
      says «партнёр не подключился» until the client's first mallet packet, then resets the rally.
      The host's puck/score are authoritative; the client sends only its mallet. Older peers still
      connect normally (31-byte pose packets untouched) but see no mini-game.
    - **Measured**: `[PROP] airhockey_table: 414 verts, 1 materials, size 5,15 x 2,91 x 3,89 m`;
      `[PROBE] air-hockey table top: 113,69 m` (= 1,14 over the 112,55 floor), `side shot ... 5,72 m`;
      `[MINI] ... camera top-down 3,2 m, rink 4,72 x 3,40 m`. The auto-test `[Debug] MiniTest = true`
      writes `map/mini_test.png` (5120×1440): the top-down rink with the mallet, puck and HUD;
      `district_93`/`94` are the idle table.
46. **v0.10.1 — table fixes**:
    - **The mallet survives re-entry**: enter, press E to leave, press E again — the 2× red/teal
      disc+dome must be back on the rink (the old code hid the mallet in `StopGame` and only
      re-activated the parent). `mini_test_reentry.png` is the regression shot.
    - **The table is smooth low-poly**: the rink is a 1024² anti-aliased redraw (dark charcoal frame,
      red face-off rings, teal centre/bands, teal goal arcs) on the low-poly mesh; no square pixels.
      The scoreboard arch is a **separate** GLB (`airhockey_arch.glb`) hidden during play, not a
      vertex-folded mesh — the old fold made a fan of shards in the angled camera. The held propeller
      is hidden too (it was the other fan).
    - **The puck stays inside the dark contour**: physics is **±2,16 × ±1,28 m**, goals 0,84 m at
      x = ±2,16; the old ±2,36 × ±1,70 was larger than the source's rink face and let the puck ride
      the thick rim. The mallet is 2× (0.64 m disc, collision radius 0.32) and clamped inside its
      half.
    - **Camera**: perspective, 4,0 m behind the local end, 5,0 m above the rink, FOV 55° (not an
      orthographic top-down plan); the table recedes and the rails read. The client camera is mirrored.
    - **Measured**: `[PROP] airhockey_table: 268 verts, 1 materials, size 5,15 x 1,14 x 3,89 m`;
      `[PROP] airhockey_arch: 146 verts, 1 materials, size 3,31 x 1,77 x 3,76 m`; `[MINI] ... camera
      behind that end 5,0 m up, FOV 55, rink 4,32 x 2,56 m`; `[MINI] hid 1 held item(s)`;
      `map/mini_test.png` and `map/mini_test_reentry.png` (5120×1440).
47. **v0.10.2 — closer camera, smaller stick-handled mallet, 20 % opponent reach, 0,5 m interaction**:
    - **Camera** is 15 % closer: 3,4 m behind the local end, 4,25 m above the rink (was 4,0 / 5,0),
      FOV 55. The table fills more of the screen and the near rim sits at the bottom.
    - **Mallet** is 45 % smaller: a **0,352 m × 0,0495 m** flat disc (2× thinner before the 45 %
      reduction) and a **0,08 m × 0,18 m** capsule stick with a rounded top instead of the massive
      sphere knob. Collision radius 0,176 m; it still re-appears on the second E (both screenshots).
    - **20 % into the opponent's half**: the clamp lets the host's mallet reach x = +0,432 m and the
      client's x = −0,432 m (20 % of the 2,16 m half); z stays ±1,104 m. The far ends remain
      ±1,984 m.
    - **Interaction**: the E prompt/start requires standing within **0,5 m of the table body edge**
      (half 2,576 × 1,943 m) and within 0,6 m of its floor — not within 6,5 m of the centre. The
      auto-test stands 0,457 m from the side and logs `near check = True`.
    - **Evidence**: `[MINI] MiniTest: teleported to the table (near check = True)`; `[MINI] host
      joined … camera 3,4 m behind that end / 4,25 m up`; `map/mini_test.png` /
      `mini_test_reentry.png`; `district_93`/`94` still the idle body + arch.
48. **v0.10.3 — E only at the table's goals**:
    - Walk to **either end of the table (the goal)**. The prompt «E — аэрохоккей» and the start work
      when the feet are within **0,5 m of the goal centre** (table-local x = ±2,576, z = 0) and
      within 1,5 m of the table floor. The old whole-body-edge test is gone. Both goals are accepted;
      the host/client mallet halves and camera ends are unchanged.
    - Evidence: the debug teleport stands **0,324 m** beyond the host goal and logs `[MINI] MiniTest:
      teleported to the table (near check = True)`; `map/mini_test.png` and `mini_test_reentry.png`
      are re-shot from there. The camera, mallet, texture and rink physics are the v0.10.2 ones.
49. **v0.10.4 — client puck sync, +35 % puck, mallet–mallet collision**:
    - **Client puck** no longer uses the lagging per-frame Lerp. The host sends state at **60 Hz**;
      the client buffers `(arrival, pos, vel)` and renders **25 ms behind** with linear interpolation
      between states, velocity extrapolation (≤50 ms) if the buffer runs dry. Fast shots and bounces
      now follow the host's real segments; clear on start/leave/timeout.
    - **Puck** is **0,2025 m** dia (was 0,15) with collision radius **0,10125 m**; the visual and the
      rink/goal/mallet bounds use the new radius.
    - **Mallet collision**: each peer pushes its own mallet out when closer than **0,352 m** (2 ×
      0,176) to the other player's, then re-clamps to its half/20 % intrusion. Both peers use the same
      rule, so the discs stop at contact; the host's puck still collides with both.
    - Both peers need **v0.10.4**. The solo auto-test verifies the larger puck and the unchanged table;
      the two-peer path is the 60 Hz state buffer plus the local collision rule.
50. **v0.10.5 — +15 % puck, ×2 trigger, remote player visibly at the table**:
    - **Puck** is **0,232875 m** dia (collision radius **0,1164375 m**) and sits at 0,029 m; the visual
      and the rink/goal/mallet bounds use it. **Trigger** is **1,0 m** from either goal point (table
      ends x = ±2,576, z = 0).
    - **Local body**: on start the FPC is placed at its table end (x = ±3,026, z = 0), facing the
      table; the player avatar is actually at the goal.
    - **Remote look**: the viewer's `AirHockeyGame` publishes `RemoteMiniActive/Feet/Yaw/Hand` from
      the partner's mallet. `Plugin.AnimateRemote` places `remoteGo` at that stance, zeroes the gait
      and calls `GlbAnimator.SetMiniPose(true, hand)`: hips down, knees bent, feet shoulder-width and
      splayed, spine hunched and side-bent toward the mallet, hips yawed, and the arm pointed at the
      mallet with `AlignLimb`. The partner therefore sees the avatar standing and moving its arm/body
      with the mallet; the body also shifts along the table end.
    - **Both peers need v0.10.5** for the remote stance. The solo `MiniTest` verifies the larger puck
      and trigger (near check true) but cannot show the remote avatar; the visual check needs the
      coop pair.
51. **v0.10.6 — a third player gets a third model**:
    - **Never share a remote model again**: the host accepts **two clients** (peer ids 1 and 2) and
      broadcasts its own pose to both. Each client's pose is forwarded to the other client as a new
      32-byte **type-3 relay** with its source id. The primary peer uses the full smoothing/animator
      pipeline; the secondary peer gets `COOP_RemotePlayer2` (own animator, simple 0.12 s follower).
      On the host, client 1 is the original avatar and client 2 the extra; client 1 sees host + client
      2, client 2 sees host + client 1. `[COOP] second remote player spawned (peer 2)` is the log.
    - **The mini-game is still 2-player** (host + first client); the third client is a spectator. The
      mini packets are filtered by peer (`1` on the host, `0` on a client), so a second client cannot
      corrupt the rally. The extra avatar currently has no held-item clone and no table stance.
      *(v0.10.7 item 52 supersedes the mini-game part: a client can now substitute on the north side
      and the extra avatar gets the table stance.)*
    - **Protocol**: type 1 remains 31 B; the relay is type 3 (32 B). Both clients need **v0.10.6**;
      a fourth peer is ignored. The old 2-player path (host + one client) still uses peer 1 and is
      unchanged. The three-machine visual check is the user's test.
52. **v0.10.7 — side score, full-puck goals, 11/100, substitution**:
    - **The scoreboard must be side-based**: top-right reads `КРАСНЫЕ n : m БИРЮЗОВЫЕ` (red = south,
      teal = north), never «ВЫ : ПАРТНЁР». A client joining or leaving must **not** reset it; the log
      on a takeover is `[MINI] peer N took the north side; side score kept at a:b`.
    - **A goal is the whole puck across the line**: drive a puck into the mouth slowly — touching the
      line must not score; it counts only once its trailing edge is past `RinkHalfX` (whole body
      inside the posts, `|y| <= GoalHalf - PuckR`). The solo `MiniTest` proves the path:
      `[MINI] goal in the north net -> 1:0, target 11`; `map/mini_test.png` shows the new HUD.
    - **Match rule**: first to 11 with a 2+ lead. At 11:10 the target becomes **100** and the HUD says
      «РАЗРЫВ МЕНЬШЕ 2 — МАТЧ ИДЁТ ДО 100»; 12:10 does not end it, 100 wins. At the end the puck
      freezes at the centre and the HUD names the winner.
    - **Substitution**: with three players, have the first client press E at the table, then the
      second client walk up and press E. The second client must inherit the north score, see the
      table and move the teal mallet; the host should pose the **substitute's** avatar at the table
      (`COOP_RemotePlayer2` for peer 2). A client already at the table while another owns the north
      side reads «МЕСТО ЗАНЯТО ДРУГИМ ИГРОКОМ — ЖДЁМ».
    - **Protocol**: the mini state packet is **35 B** (was 22); mallet/leave stay 22 B and the pose
      stream is 31 B. All three machines need **v0.10.7** for the new side logic.
53. **v0.10.8 — six patterned showroom tables one floor below the playable table**:
    - **Where**: in the House of Soviets' chevron annex, the playable table is on house-local
      **102.55**; the six retextures are on the floor below at **96.15**, a 3 x 2 grid at
      x −44/−30.5/−17, z 6.5/−4.5. The top-floor table remains the only mini-game table; these six are
      decorative.
    - **Sides**: each body has a closed perimeter band from **0.30 to 1.10 m**, one generated image
      stretched around it and mirrored exactly once (`TextureWrapMode.Mirror`, u 0…2). The wrap seam is
      invisible; the rink top and the black legs are untouched. The six patterns are
      `patterns/sloppy_01…06.png`, copied by `build.ps1` (`Patterns: 6 PNG`).
    - **Arch**: the canopy is painted in the image's dominant hue, the two support rods in its accent,
      and a thin LED strip on the canopy's front/back edge glows in the accent; a point light under the
      canopy lights the table. Themes: magenta, orange, acid yellow, gold, blue, cyan.
    - **Evidence**: `[PROP] arcade variant 1/6 … 6/6`, then `[PROP] arcade showroom: 6 of 6 patterned
      tables on the annex floor below at y=96,15 m; rink tops untouched, legs black`; map shots
      `district_146` (all six), `district_147` (plan), `district_148`/`149` (close-ups),
      `district_150` (arch/lamp). No exceptions.
    - **Not playable**: only the top-floor table is wired to `AirHockeyGame`; the six below are a
      design showroom. Wiring any of them into the mini-game is a separate step.
54. **v0.10.9 — the custom sides are flush with the table (no gap)**:
    - **Look at the table edges from a low angle and from above**: the neon pattern must sit directly
      on the table's own side faces, with **no air slit** between the pattern and the body, no floating
      ring, and no gap at the flared rim or the end caps. The rink top, the rim top and the black legs
      are still the original table.
    - **The pattern should follow the real shape** (the lower body is narrower in z than the top rim),
      so the image bends around the end caps instead of cutting straight through the air.
    - **Evidence**: `[PROP] arcade variant 1/6 …` ends with `mirrored own side faces`; map shots
      `district_146`–`150` show the flush skin. If a gap is visible again, the side-triangle
      classification or the angular unwrap regressed, not the image.
55. **v0.10.10 — four more showroom tables on the next floor down (89.75)**:
    - **Where**: the north strip of the annex's ground ring, house-local z = −11, x = −44/−35/−26/−17,
      one floor below the six tables at 96.15. The lower tower fills the middle, so walk around it.
    - **Designs**: four new muted-ink patterns (`sloppy_07…10`). They emphasise **unreadable
      pseudo-lettering** and use **less neon** than the first six. `sloppy_08` is a wall of deformed
      orange/teal **cartoon cats**; `sloppy_10` is a big pink/green **cartoon frog**. The animals are in
      the generated side skin (not separate 3D figures).
    - **Same skin**: the pattern is applied to the table's own side triangles, flush with the body; rink
      top, rim top and black legs untouched. The arch canopy/rods/LED follow each pattern's dominant
      hue/accent.
    - **Evidence**: `[PROP] arcade variant 7/10 … 10/10`, `[PROP] arcade showroom: 10 of 10 … six at
      96.15 m and four at 89.75 m`; map shots `district_151` (row), `district_152` (plan),
      `district_153`/`154` (perspective) and `district_156`/`157` (flat sides: cats and frog).
    - **Still showroom**: only the top-floor table is playable.
56. **v0.10.11 — table SFX + the goal-replay puck**:
    - **Sounds**: from `E:\858\sound_3`, `click-close-sonorous-noisy.mp3` must play on every rail or
      mallet hit and `avtomat--banka-padaet.mp3` on a goal. The log must show
      `[MINI] sound loaded: click-close-sonorous-noisy (0,71 s)`, the banka line and
      `arcade sounds ready: click=ok, goal=ok`. If it says `missing`, `build.ps1` did not copy
      `sounds/` or the MP3 failed to decode.
    - **Goal replay**: after the goal the puck disappears for **1 s**, then fades in at **+10 cm**
      over **0.5 s**, falls, does **two small edge bounces** (with a slight tilt) and settles — the
      animation itself is **1.5 s**. Physics resumes only after 2.5 s. The host sends y/alpha/tilt in
      the 49-byte state, so a client sees the same motion.
    - **Evidence**: MiniTest log frames at t = 0.42 / 0.74 / 1.08 / 1.42, then `respawnActive=false`;
      `map/mini_test.png` and `map/mini_respawn_bounce.png` (the tilted bounce). Both peers need
      **v0.10.11**; older peers ignore the extra state bytes but have no sound/replay sync.
57. **v0.10.12 — the wide white rim is now a raised theme-coloured bumper**:
    - **Look at any of the ten showroom tables**: the wide horizontal border that used to be white
      must be the **same colour as that table's arch canopy**, and it must sit **5 cm higher** than
      the old table top (a visible raised lip, not a flush shelf). The rink top, the black legs and
      the patterned side skin below the bumper stay original.
    - **The player must not walk through it**: the showroom table collider height is 1.1908 m
      (1.1408 + 0.05). The playable top-floor table is deliberately unchanged.
    - **Evidence**: `district_158` (variant 1 close-up: pink rim = pink arch), `district_146` and the
      other showroom shots. If the rim is white or flush, `AddRaisedArcadeBumper` or the shared
      `themeMat` regressed.
58. **v0.10.13 — bumper −3 cm, field red/blue → arch/lamp**:
    - **The raised bumper's top outer edge is 3 cm narrower** than v0.10.12; the outer wall now leans
      inward over its 5 cm height and covers the old white edge. No white strip may show.
    - **On every showroom table's field**, red circles/centre line must become the **arch canopy
      colour**, and blue/teal lines/centre ring the **lamp colour**. The bone background, the
      anti-aliased edges and the rink size stay the same. The main playable table's field is
      deliberately unchanged.
    - **Evidence**: `district_158` (variant 1: pink field circles, cyan field lines, pink bumper) and
      `district_146`. If the field is still red/blue, `MakeAirHockeyFieldMaterial`'s masks or the
      `airhockey_field` split regressed; if the rim is still wide, the `tp` outer ring lost its 3 cm
      inset.
59. **v0.10.14 — side from approach, every table playable**:
    - **Any of the 11 tables**: walk to a table's goal and press E; the game must start at **that**
      table, with the camera behind the end you approached and the FPC standing there. The log says
      `joined table N (side red|blue …)`. The top table is id 0; the ten showroom tables are 1…10.
    - **Side = closer goal**: approach the red end (local −x) → you are red; the blue end (local +x)
      → you are blue. The HUD says `ВЫ ЗА КРАСНЫХ/СИНИХ`, your mallet is red/blue accordingly, and
      the score is still side-based.
    - **Host/client**: the host takes its approach side; a client on the **same** side is rejected
      with `ЭТА СТОРОНА У ХОЗЯИНА — ПОДОЙДИ С ДРУГОГО КРАЯ`. The client must go to the other end. The
      first client on the free side inherits that side; both may be red/blue independently of who
      hosts.
    - **Cross-table**: a client at a different table must not see the host's puck/score; it shows
      `ПАРТНЁР ЗА ДРУГИМ СТОЛОМ` and the host ignores its mallets (table id in every mini packet).
    - **Evidence**: MiniTest now starts at the last showroom table (frog, id 10) from its blue end:
      `MiniTest: teleported to table 10 blue end (near check = True)`,
      `host joined table 10 (side blue …)`, and `mini_test.png` shows the custom table with the
      blue-side mallet. Both peers need **v0.10.14**; mallet/leave are 23 B, state is 52 B.
60. **v0.10.15 — the lamp-end score boards**:
    - **Look at any table's lamp**: both ends carry a small **trapezoid board** that repeats the
      canopy's end section (0,85 m top / 0,608 m bottom, 0,148 m tall), tilted **10° down** towards
      the player. Its border is the **lamp body colour** (pink `#AB439D` on the sloppy_10 table,
      dark oxide `#221B12` on the main one), not the green/cyan light colour; the screen is matte
      black, the score is in the **acid-green marker digits** with a small green hyphen.
    - **The score survives leaving the mini-game**: score a goal, press E; the board still shows the
      score, and pressing E again continues the running match (a finished match starts 0-0).
      MiniTest proves it: after `goal in the north net -> 1:0`, `mini_scoreboard.png` shows `1-0`
      and the re-entry HUD reads `КРАСНЫЕ 1 : 0 СИНИЕ`.
    - **The boards hide with the lamp during play** (they are parented to the arch): a board left
      floating over the table covers the centre of the rink, which was verified in `mini_test.png`.
      They update while hidden, so the table always shows the current score when the lamp is back.
    - **Assets/logs**: `build.ps1` must print `Score glyphs: score_glyphs.png`; the log must contain
      `[SCORE] digit atlas loaded: score_glyphs.png 2860x256, 11 cells …` and one
      `[SCORE] table N …` line per table with the housing hex colour. A missing atlas logs
      `score_glyphs.png is missing — the boards will be blank` (the boards still exist, the screens
      stay black).
61. **v0.10.16 — idle side mallets**:
    - **Walk up to any unused table**: a **blue** mallet stands on the rink to the **right of the
      blue goal** from the blue player's view, and a **red** one is the mirror image at the red end.
      The blue is the same blue the client plays with (`#1F52A8`), the red `#9E1F14`.
    - **Press E**: both markers disappear and the live mallets take over; press E again to leave —
      they are back on the rink. Log: `[SCORE] table N: idle side mallets — red at (-1,78, -0,60),
      blue at (+1,78, +0,60) …`; MiniTest writes `mini_idle.png` with the blue marker on the idle
      table and `mini_test.png` without it during play.
62. **v0.10.17 — five arcade cabinets on the top hall's back wall** *(decor for now)*:
    - **Fly/climb to the annex's top floor** (the room with the single air-hockey table) and walk to
      the wall opposite the stair (z = −14): **five arcade cabinets** stand with their backs **1 m
      off the wall**, fronts into the room, **2 m apart**, 1,80 m tall, under the window sills.
    - **Each cabinet has a different AI-slop side skin** (chrome skull, cyber ninja, pixel octopus,
      chrome dragon, robot samurai); the image is on the side panel and **mirrored on the other
      side**. The front keeps the source asset's worn yellow body/screen (its own lettering is
      mirrored slop). They are solid (BoxCollider) but **not playable yet**.
    - **Evidence**: `[ARCADE] cabinet 1…5` + `[ARCADE] 5 of 5 cabinets …`, map shots `district_159`
      (the row along the wall) and `district_160` (the middle cabinet's side art + front). Build
      prints `Arcade art: 5 PNG`.
63. **v0.10.18 — "Dinozavrik" assets (generated, not wired yet)**:
    - **Style check**: every asset is **neutral grey** (18 fixed grey levels), heavy and angular;
      the background sky is flat, colourless grey. No blue glass, no yellow/rust accents.
    - **`model/arcade_game/` + `plugins/BabbdiCoop/arcade_game/`**: `bg_loop.png` (1280×240, mirror
      loop, slow), `ground_loop.png` (640×64, mirror loop, fast), `player_run/jump/idle.png`
      (48×48), eight `obstacle_*.png` (64×64: crate, barrel, rubble, barrier, cone, pipe, block,
      rebar), `manifest.json`, and the previews `preview_game.png` / `preview_sprites.png`.
    - **Preview**: `preview_game.png` must show the 320×240 frame upscaled 3× — giant grey brutalist
      blocks with sawtooth roofs and cranes, a fast rubble ground band, the small worker standing on
      the surface, and the crate/barrier/rubble obstacles. The player and obstacles stand **on** the
      ground line (feet at the top of the band, not sunk into it).
    - **Build**: `build.ps1` prints `Dino assets: N files`; the cabinets still show their static
      screens — the game loop is the next round.
64. **v0.10.19 — muted palette + four player variants**:
    - **Style**: the game is **grey-dominant, not black-and-white**. The palette is 20 colours —
      ten neutral greys up to 174 (no white) plus muted olive/rust/teal/blue-grey/ochre/brick/bone.
      No bright shades; the sky stays colourless grey. The background, ground and obstacles are all
      re-quantised to this palette and pre-smoothed (Gaussian + median 3) so they read as flat pixel
      art, not salt-and-pepper.
    - **Four player variants to choose from** in `model/arcade_game/player_variants/`:
      `variant_1_stocky`, `variant_2_tall`, `variant_3_woman`, `variant_4_foreman` (64×64 cells,
      44 px side-view run, straight legs, 1 px outline). `preview_variants.png` shows all four at 6×
      with labels. The old bow-legged front sprite is gone; `player_run.png` currently = variant 1.
    - **Pick one**; the jump/idle frames and the actual animated player will be made for the chosen
      variant. `preview_game.png` shows the muted look with variant 1.
    - **Build**: `build.ps1` copies `player_variants/` with the rest; `manifest.json` lists the
      palette and the four variants.
65. **v0.10.20 — foreman animation frames + 60 % bigger cabinets**:
    - **Foreman**: `arcade_game/player_foreman/` has `run_1`, `run_2` (2-frame loop, ~8 fps),
      `jump` (lifted 8 px), `fall` (lifted 6 px), `crouch` (72 % squash) and `idle`, all 64×64 in
      the muted palette. Top-level `player_run.png`/`player_run_2.png`/`player_jump.png`/
      `player_fall.png`/`player_crouch.png`/`player_idle.png` are the foreman now.
      `preview_foreman.png` is the labelled 6-frame sheet; `run_loop.gif` is the looped run.
    - **Cabinets ×1,6**: each cabinet is **2,88 m** tall (was 1,80), 1,27 m wide, 1,58 m deep; the
      backs stay 1,03 m off the wall, 2 m apart (0,73 m gaps). Log:
      `1,80 m x1,6 = 2,88 m tall` and `… 1,6x size (2,88 m tall)`.
    - **Map shots**: `district_159` (the row) / `district_160` (middle cabinet) re-framed for the
      taller cabinets: cameras at house-local (-39,5, 105,9, -4,6) and (-30,5, 105,6, -7,0).
    - **Not wired yet**: the frames are ready for the runner; the cabinet screens are still static.
66. **v0.10.21 — playable "Dinozavrik" test**:
    - **Walk up to any of the five cabinets** (the 2,88 m ones on the annex's top floor): «E —
      Динозаврик» appears; E moves the camera to the cabinet screen. The game is **frozen** with
      «ПРОБЕЛ — СТАРТ»; the first Space only starts it. Then the foreman runs in place on the
      **left**, facing right, the ground scrolls left at **1X**, the skyline at **0.6X**.
      Space jumps, Ctrl crouches, E leaves. No obstacles/score yet.
    - **Orientation**: the player must be on the **left** and face **right** (the first test had
      him on the right/facing left — the root is turned 180° and the quads face local −z now).
      The game screen is a 4:3 0,76 × 0,57 m quad on the cabinet face.
    - **Debug**: `[Debug] DinoTest = true` (self-resets) teleports to cabinet 3 and writes
      `dino_prompt.png`, `dino_ready.png` (start prompt), `dino_test.png` (running),
      `dino_crouch.png`, `dino_jump.png`. Log: `[DINO] entered the arcade cabinet …`,
      `ground=0,263 bg=0,079` (0,6X in pixels), `[DINO] left …`.
    - **Known test artifacts**: the pre-entry `dino_prompt.png` may catch the camera pitch looking
      up (the auto-test does not set the FPC pitch) and the propeller before it is hidden; the
      actual game view (`dino_ready`/`dino_test`) has no held item and the prompt/start text.
67. **v0.10.22 — scrolling fixed, run frames 2x slower, random obstacles**:
    - **The ground and skyline must move.** Enter, Space: the ground slides left at 1X, the
      skyline at 0.6X. The stills `dino_ready` (offset 0) and `dino_test` (ground 0,339 / bg
      0,102) show a different skyline/ground. Root cause fixed: the material's `_MainTex_ST` was
      ignored by the `Sprites/Default` fallback (Unlit/Texture is stripped); the quads' UVs are
      now written every frame.
    - **Run frames** change every **0,24 s** (was 0,12) — the run only; jump/fall/crouch are
      unchanged.
    - **Obstacles**: every 1,0–2,2 s a random one of the eight debris sprites appears at the right
      and slides left at ground speed. A jump above its bbox clears it; touching it freezes the
      run with «СТОЛКНОВЕНИЕ — ПРОБЕЛ ЗАНОВО»; Space starts a new random run.
    - **Evidence**: `dino_test.png` (one obstacle), `dino_crouch.png`/`dino_jump.png` (same
      obstacle), `dino_hit.png` (the pipe on the foreman + the message + a second obstacle);
      log `[DINO] collision with obstacle_pipe at x=-0,16; run ended (Space restarts)`.
    - **Not yet**: score, death counter, sound, speed ramp.
68. **v0.10.23 — light asphalt ground, smaller obstacles, 2x speed**:
    - **The ground must be a clearly separate, much lighter asphalt band** under the dark skyline:
      fine light-grey aggregate, thin cracks, dark 2 px top edge. The old dark rubble band was the
      background showing through — the background/ground materials used the **transparent**
      `Sprites/Default` fallback (ZWrite off) and the farther skyline painted over the nearer
      ground. They are now opaque Standard + emission (depth-writing).
    - **Obstacles** are **60 % narrower and 40 % lower** than the old squares; the collision bbox
      scales with them. They still spawn in random order every 1,0–2,2 s and move at ground speed.
    - **World speed ×2**: ground/obstacles 240 px/s (0,57 m/s on the 0,76 m screen), skyline 0,6×.
      After 1,4 s the log should read `ground=0,526 bg=0,158` (was 0,263/0,079); the run frames
      remain 0,24 s.
    - **Evidence**: `dino_test.png` (light asphalt, a small crate at the right), `dino_hit.png`
      (small crate at the foreman + «СТОЛКНОВЕНИЕ»); `[DINO] collision with obstacle_crate at
      x=-0,20`.
    - **Not yet**: score, death counter, sound, speed ramp.
69. **v0.10.24 — no fall wind, sized crate/fence, brighter debris, screen clipping**:
    - **Enter a cabinet or the table right after a fall**: the "wind, as if falling" must stop with
      the mini-game camera. Both entries log `[COOP] fall wind muted for the mini-game (was …)`; the
      controller restores its own volume on exit. If wind is still heard, the log will say
      `could not find FirstPersonController.fallAudio` instead (field renamed by a game update).
    - **The wooden crate is 30 % taller** and the **striped fence barrier 30 % wider** than the other
      debris; their collision boxes scale with the stretched sprites.
    - **All eight obstacles are lifted** (gamma 0.72 + 22 % saturation): visible mean luminance
      +≈25 per sprite, no clipping of the light stripes, no neon.
    - **Obstacles never appear outside the 4:3 screen any more.** At spawn the first one is
      invisible, then enters cropped by the right border (`dino_spawn.png` shows the sliver at the
      edge only, not on the cabinet); at the left it disappears exactly at the border.
    - **Evidence**: log `[DINO] collision with obstacle_barrier at x=-0,18`; shots `dino_spawn.png`,
      `dino_test.png`, `dino_hit.png` in `BepInEx/plugins/BabbdiCoop/map/`.
70. **v0.10.25 — the foreman faces right**:
    - On the cabinet screen the runner must face **right**: hat brim, nose, leading leg and arm all to
      the right. The frame PNGs were drawn facing left; `FlipTextureX` mirrors all six player frames
      once at load (run 1/2, jump, fall, crouch, idle). The background/ground/obstacles are not
      flipped.
    - **Evidence**: `dino_ready.png` (idle) and `dino_test.png` (run) — the pale face and the leading
      foot are on the right. Log `BabbdiCoop v0.10.25 loaded`.
71. **v0.10.26 — each cabinet body is repainted in its art's dominant colour**:
    - Walk to the five cabinets: no cabinet may have a worn-yellow front/top any more. Each body is
      the dominant colour of its own side art — **magenta, purple-magenta, orange, brick red,
      pink** — while the yellow paint's **rust, scratches and worn edges are still visible** (the
      texels keep their luminance; only the hue/saturation of yellow paint is changed).
    - **Untouched**: the side art panels, the dark blue `JUMP TANK` marquee, the green strips, the
      `INSERT COIN` screen, the grey lower panel, the buttons.
    - **Evidence**: five `[ARCADE] cabinet N: body yellow -> #RRGGBB (dominant of 'arcade_0N.png'),
      … body texels recoloured` lines; map shots `district_159.png` (the row) and
      `district_160.png` (close-ups, the orange body's rust texture reads).
72. **v0.10.27 — real wind mute + pixel hitboxes**:
    - **Enter a cabinet or the air-hockey table**: the indoor/outdoor wind must stop. The log says
      `[COOP] ambient wind muted: 'IndoorWindSound' clip='windIndoor' was volume=0,75` and the same
      for `OutdoorWindSound`; on exit both `restored` lines show the original volumes/0,25.
      The player's `FallSound` is also 0 (`[COOP] fall wind muted`).
    - **Hitboxes match the visible pixels.** The player's box follows the current frame's alpha
      bbox (run halfW **0,031 m**, height **0,109 m**; crouch **0,033 / 0,078 m**) and its centre
      offset; the obstacles use their own alpha bboxes (`crate 0.383/0.656`, `rubble 0.453/0.375`,
      …), with the crate/barrier size multipliers applied. The collision log shows the visible
      values: `feet 0,000 < top 0,034` for the rubble (a 3,4 cm visible top, not the 9,1 cm cell).
    - **Evidence**: the eight `[DINO] obstacle bbox …` lines, `[DINO] player pixel hitbox …`,
      `[DINO] obstacle hitbox '…': visible … m`, and the collision line; DinoTest's screenshots
      `dino_spawn/test/crouch/jump/hit.png` still pass.
73. **v0.10.28 — the Dinozavrik score counter**:
    - **On entering / at the frozen start**: the game screen's **top-right** shows four chrome
      digits **0000** in the `digits_v3_chrome` font (16 x 20 px cells, 8 px margin). No plate —
      only the keyed glyphs.
    - **Pass an obstacle** (jump over it or just survive it): the counter increments — `0001`,
      `0002`, … The log says `[DINO] obstacle passed: obstacle_cone, score=0001`. Colliding with an
      obstacle freezes the run and that obstacle is **not** counted; `Space` starts a new run at
      **0000**.
    - **Assets/logs**: `Dino assets: 33 files` (the new `score_digits_chrome.png` 1280x160);
      `[DINO] score atlas … 10 cells`, `[DINO] score display: 4 digits at the screen's top-right …`.
    - **Evidence shots**: `map/dino_ready.png` (0000), `map/dino_score.png` (0001 after the cone),
      `map/dino_hit.png` (collision, counter frozen).
74. **v0.10.29 — wind silenced for the whole game + pastel debris**:
    - **No wind at any point in a cabinet or at the air-hockey table**, including while jumping
      (`Space`) or holding the propeller: the ambient `IndoorWindSound`/`OutdoorWindSound`, the
      fall loop and the camera's held `Propeller2/3` sources are re-muted every LateUpdate, and
      the Propeller/Blower/Climber/Grabber scripts are disabled for the duration. Logs:
      `[COOP] ambient wind muted …`, `[COOP] held audio muted …`, `[COOP] held gadget disabled:
      Propeller …`. On exit the saved volumes (0,75/0,25, 1,00/0,43) are restored, and the air
      hockey's own click/goal `sfx` keeps working.
    - **Pastel debris**: the six obstacles whose grey/black share is over half (rubble 86,7 %,
      barrier 89,0 %, cone 96,8 %, pipe 84,2 %, block 99,9 %, rebar 96,5 %) now carry **84-99,9 %
      pastel** texels in three muted colours each, with the original luminance, outline, cracks,
      hazard stripes and white bands intact. `crate` and `barrel` (22,8 %) stay original.
    - **Evidence**: the eight `[DINO] obstacle texture …` lines; `map/dino_test.png` (pastel rebar),
      `dino_score.png` (0001), `dino_hit.png` (pastel cone).
75. **v0.10.30 — jump +30 %, obstacles +30 %, speed ramp, score top-left, weathered debris**:
    - **Jump**: visibly longer (airtime **0,487 s**, was 0,375 s) at the same **0,141 m** height;
      log `airtime=0,487 s (v0.10.30: +30 % duration ...)`.
    - **Obstacles**: all **30 % larger** (barrel sprite 0,079 x 0,119 m; barrier 0,103 x 0,119 m),
      with the crate/barrier extra multipliers still applied. They are noticeably chunkier and
      harder to clear, but the jump height still clears the crate (0,101 m top < 0,141 m apex).
    - **Speed**: after **10** passed obstacles the world runs at **×1,15** of base, 20 → ×1,30,
      30 → ×1,45 … (base 240 px/s; ground, background 0,6× and obstacles all scale). Log
      `[DINO] speed x1,15 after 10 obstacles`; a new run resets to ×1.
    - **Score** is now in the **top-left** (8 px margin), still four chrome cells, 0000 at the start.
    - **Debris**: the pastel look is gone; the six grey/black obstacles now use beige / khaki /
      moss green / brick / sand: rubble with brick chips, barrier khaki-green-sand panels, cone
      sand/beige/brick, pipe with moss at the foot, block beige/sand, rebar brick/khaki/green. 84-
      99,9 % coverage, original luminance/outline/details preserved; `crate`/`barrel` stay original.
    - **Evidence**: `map/dino_ready.png` (0000 top-left), `dino_test.png` (larger barrel),
      `dino_hit.png` (larger retextured barrier); logs `[DINO] jump: …`, `[DINO] speed: …`,
      six `obstacle texture … -> retexture …` lines.
76. **v0.10.31 — looping gameplay screen + four 1:3 posters**:
    - **Walk up to any cabinet**: instead of the static `INSERT COIN` screen it plays a **20 s
      looping gameplay GIF** — running, jumping over the +30 % obstacles, chrome score 0000 →
      0009, then the collision freeze and loop. It is a shared flipbook: 200 frames at 10 fps,
      160x120 cells in `arcade_attract_atlas.png` (1600x2400, 10x20), plus the source
      `arcade_attract.gif` (320x240, 2,2 MB). Logs `[ARCADE] attract atlas … 200 frames @ 10 fps =
      20 s loop` and five `[ARCADE] attract screen N …` lines.
    - **Enter with E**: the animation hides, the real game screen shows (black bezel stays); on
      exit the animation returns. The screen is upright (score top-left, foreman left) — the U is
      reversed for the quad's +z face.
    - **Posters**: four 1:3 (512x1536) SD 1.5 variants are ready in `arcade_posters/` as
      `poster_N_marquee.png` (chrome «ДИНОЗАВРИК» title) with a `posters_sheet.png` contact sheet;
      the raw AI files are `poster_N.png`. The marquee is still `JUMP TANK` until the user picks one;
      the chosen poster becomes an overlay on the vertical marquee, not a body-texture edit.
    - **Evidence**: `map/district_159.png` / `district_160.png` show the gameplay frames on all five
      cabinets; `map/dino_ready.png` shows the real game screen after E.
77. **v0.10.32 — conformal screen + horizontal matte-red posters**:
    - **No floating screen.** From the front and from the row/3-4, the animation must lie on the
      cabinet's recessed/slanted display, not hover in front of it. Log
      `[ARCADE] screen mesh: 144 verts / 48 tris … conformal to the slanted screen`; the frame reads
      upright and complete (score top-left, foreman left, ground below). The painted bezel around
      the display is the cabinet's own art.
    - **Posters**: four **horizontal 1536 × 512 (3:1)** marquees in the game palette, with a
      Latin **DINO / ZAVRIK** title passed through **SD img2img inpainting** (denoise 0,68-0,75)
      into heavy matte-red neural slop — warped letter silhouettes, deliberately hard to read.
      Files `arcade_posters/hposter_1_marquee.png` … `_4_`, contact sheet `hposters_sheet.png`;
      pick one and it will replace `JUMP TANK` on the marquee.
    - **Evidence**: `district_159.png` / `district_160.png` (two angles); the screen log; the poster
      sheet.
78. **v0.10.33 — poster 4 installed; desynced cabinet loops**:
    - **The marquee** above every screen is the chosen minimal-city poster 4, conformal to the real
      top panel (210 verts / 70 tris), its matte-red slop title reading D→K left-to-right. Logs
      `marquee poster: arcade_poster.png`, `marquee poster mesh: 210 verts / 70 tris`, `poster 4
      installed on cabinet N's marquee`.
    - **The five screens no longer play in sync**: each has a random start phase
      (`desynced start frame` e.g. 127 / 122 / 75 / 197 / 122), runs its first cycle from that
      random point, then full 20 s loops. One still (`district_159.png`) must show different game
      moments on the five cabinets.
    - **Evidence**: `map/district_159.png` and `district_160.png`; the five start-frame log lines;
      `Dino assets: 36 files` (the new `arcade_poster.png`).
79. **v0.10.34 — screen edge-to-edge; cabinet fully silent**:
    - **The animation must fill the whole screen opening**, including the right-hand
      `INSERT COIN`/green-bars strip: no static column, no gap. Log `screen mesh: 258 verts /
      86 tris, whole opening x -0,28..0,38, y 0,88..1,50 … (no right gap)`.
    - **No wind/audio at all in a cabinet**: on E the log says `cabinet audio muted (AudioListener
      1,00 -> 0,00)`; on exit `cabinet audio restored`. The Dino game has no own sounds; the air
      hockey's SFX is unaffected because the listener is restored outside the cabinet.
    - **Evidence**: `map/district_160.png` (full-width gameplay, poster 4 above, desynced screens),
      `district_159.png` (row); the screen/audio logs.
80. **v0.10.35 — the screen fits the bezel, mono marquee, air-hockey wind muted (SFX kept)**:
    - **The gameplay frame must sit inside the monitor, not on its frame**: walk up to a cabinet
      and look from the front and from the side. No part of the GIF may cover the white surround,
      the control panel or the cabinet's left post. Log: `screen mesh: 192 verts / 64 tris,
      display island x -0,30..0,30, y 1,01..1,49, whole picture incl. right strip
      (recess-conformal, no spill)`. The right `INSERT COIN`/green-bars strip is covered by the
      animation — there is no static column.
    - **The marquee poster is near-monochrome**: only the matte-red `DINO / ZAVRIK` title keeps
      its colour; the city, sky and ground are grey. `district_159.png`/`district_160.png` show it.
    - **Air hockey**: on E the log says `air-hockey audio muted (AudioListener 1,00 -> 0,00,
      table sfx exempt: airhockey_sfx)` and holds the listener at 0 the whole match — no wind,
      no fall loop, no gadget loops. Mallet clicks and the goal banka must still be audible (the
      table's `sfx` bypasses the listener volume); on exit
      `air-hockey audio restored (AudioListener 1,00)`. `MiniTest` still logs
      `goal in the north net -> 1:0` and `arcade sounds ready: click=ok, goal=ok`.
81. **v0.10.36 — six new cabinets, three new games**:
    - **Walk the annex's top hall**: the row now has **eleven** machines, 2 m apart. From the far
      left: two **Arkanoid**, two **Pong**, two **Zmejka**, then the five **Dinozavrik**. Each new
      one has its own SD slop side art, its own 3:1 marquee poster (`ARKANOID` / `PONG` /
      `ZMEYKA`), and a body repainted in that art's dominant colour (`#992814`, `#A54632`,
      `#9D0A87`, `#2A9637`, `#95093D`, `#41926F`). Logs: `cabinet N: body yellow -> #…`,
      `marquee 'arcade_poster_NN.png' installed on cabinet N`.
    - **The screens play the right attract loop**: Dino cabinets the grey city, Arkanoid the
      brick field, Pong the two paddles, Zmejka the snake — all desynced per cabinet. Logs:
      `attract atlas Arkanoid: arkanoid_attract_atlas.png 1600x2400 …`,
      `attract screen N (Arkanoid|Pong|Zmejka): conformal, desynced start frame …`.
    - **Play each**: E near a cabinet. Arkanoid — A/D or arrows, Space to launch, 3 lives.
      Pong — W/S or arrows, first to 7. Zmejka — arrows/WASD, eat food, walls kill. Space starts
      and restarts after `GAME OVER`; E leaves. The HUD prompt names the game.
    - **Evidence**: `[ARCADE] Arkanoid/Pong/Zmejka screen: 320x240 pixel canvas`; the debug
      `[Debug] ArcadeTest = arkanoid|pong|snake` writes `arkanoid_test.png` (bricks + 0010),
      `pong_test2.png` (0:1), `snake_test2.png` (0010). Map shots `district_161.png` (new row),
      `district_162.png` (close-up), `district_163.png` (all eleven).
82. **v0.10.38 — the three games are colourful neural slop**:
    - **Enter Arkanoid, Pong or Zmejka**: the screen must no longer read as clean pixel art. The
      background is a psychedelic SD slop image (`slop_arkanoid/pong/snake.png`), the bricks cycle
      the whole hue wheel with white speckles, the paddle is a rainbow bar on a dark plinth, the
      ball is a cyan glow orb with a rainbow trail, Pong's paddles/ball glow with a dark score
      strip, and Zmejka's body is a hue-cycling neon snake with a pulsing orb for food over a
      darkened, faint-grid board. Scanlines, glitch slices, RGB offset and white speckles run over
      everything.
    - **The idle attract loops are the same art**: `arkanoid/pong/snake_attract.gif` and their
      atlases were regenerated with that renderer, so a cabinet's screen and the playable game
      share one look. The cabinets, side art and marquees are unchanged.
    - **Evidence**: `map/arkanoid_test2.png`, `pong_test2.png`, `snake_test2.png`,
      `district_161/162.png` (attract loops); logs unchanged except the version.
83. **v0.10.39 — Arkanoid/Pong are brutal, not rainbow; Snake unchanged**:
    - **Arkanoid**: concrete wall, not a neon LSD floor. Bricks are chunky industrial slats in a
      limited rust/bone/teal/acid/pink (or violet/mustard/cyan/brick) palette with black outlines,
      thick accent bars, chipped corners and paint drips; the paddle is a hazard-striped slab
      with bolts; the ball is chrome with one accent halo and blocky afterimages. Every 4 s a row
      slides 3 px; slats flicker in brightness; the palette drifts over 8 s; the screen flashes
      briefly every 7 s.
    - **Pong**: bunker hall, black score plates, bone/acid digits, marching two-colour centre line,
      hazard-striped slab paddles with bolts. The paddles swap palettes every 4 s and pulse 1 px,
      the digits glitch-double, the screen flashes every 6 s.
    - **Snake**: exactly the previous look (`slop_snake.png`, rainbow body, grid) — do not change.
    - **Evidence**: `map/arkanoid_test2.png`, `map/pong_test2.png`; Python previews
      `slop_arkanoid_60.png`, `slop_pong_60.png`; version `0.10.39`.
84. **v0.10.40 — W/S are correct in Pong and Zmejka**:
    - **Pong**: W or ↑ moves the left paddle **up**, S or ↓ moves it **down**.
    - **Zmejka**: W or ↑ steers **up**, S or ↓ steers **down** (A/D and ←/→ unchanged).
    - The bottom HUD shows `A/D — БИТА` (Arkanoid), `W/S — БИТА` (Pong),
      `WASD — ЗМЕЙКА` (Zmejka).
    - Version `0.10.40`, flags reset.
85. **v0.10.41 — dark minimal backdrops; playfield backdrop upright**:
    - **Arkanoid/Pong**: the background must be dark charcoal with a few large, heavily blurred
      abstract shapes — teal/plum/rust/ochre for Arkanoid, amber/cyan/violet/rust for Pong — not
      the old concrete photos. Snake's backdrop is unchanged.
    - **The live playfield background must be upright** (same orientation as the attract screen).
      The old `LoadSlopBackground` copied Unity's bottom-up pixel rows into the top-down canvas;
      it now inverts the source row (`height-1-y*height/H`). Applies to all three games.
    - **Evidence**: `map/arkanoid_test2.png`, `map/pong_test2.png`; when entering the cabinet the
      shapes at the top of the Python preview must also be at the top in game. Version `0.10.41`.
86. **v0.10.42 — duels for Pong/Arkanoid/Zmejka; three duel cabinets at the opposite wall**:
    - **The row**: 11 machines at z=-12.5 facing +z; **three extra duel cabinets** at z=18.5,
      yaw 180 (fronts facing the room) — Pong, Arkanoid, Zmejka, each with its own art, marquee
      and body colour. Map shots `district_164/165`.
    - **Lobby**: at a cabinet press **F**. Host opens the lobby; the client presses F at any
      cabinet of the same kind and joins (retries every 1.2 s). Host sends Start. `E` leaves,
      `F` cancels; a 3 s silence ends the duel.
    - **Pong duel**: shared court, `W/S`, first to 7; host authoritative.
    - **Arkanoid duel**: two independent fields, own lives/score; every 5 destroyed bricks sends
      a junk row to the opponent; first to clear or the opponent out of lives wins.
    - **Zmejka duel**: shared grid, two snakes with their own food, last alive wins a round,
      first to 3.
    - **Debug**: `[Debug] DuelTest = pong|arkanoid|snake` writes `<game>_duel_*.png`; logs
      `[DUEL] N screen: 320x240 duel canvas, host` and `[DUEL] ... started (host)`.
    - Version `0.10.42`.
87. **v0.10.43 — Zmejka's "impossible" death is fixed (solo and duel)**:
    - **Reproduce the old bug**: in Zmejka, hold right and tap **up then left as fast as you can**
      (both within one move tick, ~0,1 s). Before this build the snake reversed into its own neck
      and you lost with a perfectly straight body; now the second press is rejected and the snake
      simply turns up.
    - **Turns are now one per step**: the game keeps at most **one buffered turn**, validated
      against the direction the head **actually moved in**. `right → up → left` in one tick drops
      the left; `right → up` then `left` on the next step still works (a normal two-cell U-turn).
      Pressing the current direction cancels a buffered turn.
    - **Also fixed (duel)**: the opponent/own **tail cell** is free on a normal step (it moves
      away); it only kills when the snake grows into it. Tight play no longer dies for nothing.
    - **Machine proof**: `[Debug] ArcadeTest = snake` → `[SNAKE] turn test: up buffered=True,
      same-tick left rejected=True, head 6,7 alive=True; later left accepted=True, head 5,7
      alive=True`; `[Debug] DuelTest = snake` → the same line with `head 4,7` / `3,7`. A `False`
      in `rejected` or `alive` means the guard regressed.
    - Version `0.10.43`; packet format unchanged.


