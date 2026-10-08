# Triage 2026-10-07: followers on page turns, Kami's Spine physics shake, main-menu Kami

Source: Diego's two OBS clips of 2026-10-07 (`17-38-10` Level 2 idle, `17-39-43` main menu) plus his notes.
Status (2026-10-08): **issue 2 is RESOLVED** (see "Resolution" in section 2). Issues 1 and 3 are specced in
`specs/012-page-turn-followers/`. The only repo change from this triage is `m_IsKinematic: 1` in `Kami.prefab`.
Method: systematic-debugging. Evidence first, no fix proposed for a cause that isn't proven.

| # | Issue | Cause known? | Size |
|---|---|---|---|
| 1 | Natalia stays behind on the old page while Kami rides the page edge | **Yes**, three stacked causes, all in code | Medium (new component, reuses the `PaperPlaneRide` technique) |
| 2 | **RESOLVED 2026-10-08**: Kami's Spine physics parts shake (always, every skin and animation, only on `Player`-driven Kamis, since the Atlas 13 import) | **Yes, confirmed**: a dynamic Rigidbody fighting the `CharacterController` wobbled the root; spine-unity fed the Y wobble 1:1 into the physics | Done: `m_IsKinematic: 1` on `Kami.prefab` |
| 3 | Main-menu Kami must be Libro 1 outfit + original gear, no scissors | **Yes**, plain config/composition gap | Small |

---

## 1. Followers don't travel with Kami across a page turn

### Symptom
Kami hangs from the page edge (`RidingPage`) and flies to the start of the next page. Natalia stays frozen at the exit end of the old page's coordinates. Expected: Natalia holds on to Kami, flies with her, and they start the new page together.

### Root cause (traced in code, not yet reproduced with logs)
Three things stack, and each one alone would be enough to leave her behind:

1. **Nothing moves a follower on a normal page turn.** `PageScrollerManager` -> `PlayerPageSpawnManager` only place *Kami* (`FinalizePlacement` -> `PositionPlayerAtPoint(targetPos)`). A grep for `OnNewPageOpen` / `OnPageFinishTurning` / `OnPlayerPlaced` shows every NPC `WarpTo` call is page-specific (`ArrestCutscene`, `PoliceStationPage`, `MuseumPage`, `AbuelaEntrance`). No generic "followers come along".
2. **The NavMesh is swapped under her.** `PageNavMeshManager.OnNewPageOpen` removes the old page's `NavMeshData` and adds the new page's. Natalia's agent is now `isOnNavMesh == false`, so `NPC.IsAgentUsable()` returns false and `MoveTowards` no-ops with a throttled "CANNOT MOVE" warning.
3. **Even on a mesh she would be far away.** Kami lands at `pageEntryX`/`pageExitX` (`GetProjectedPositionInNewPage`); Natalia is still at the old X, i.e. the far edge. That matches "se queda quieta allá, en el final de la página".

Expected log when reproducing (turn a page with Natalia following): `[NPC] Natalia CANNOT MOVE: ... isOnNavMesh == false` right after `[PageNavMeshManager] NavMesh activado para pagina N`. **Not yet confirmed in a play session, so confirm it before building.**

### Proposed solution (to become the spec)
Reuse the `Rider` technique that `Level2/PaperPlaneRide.cs` already proves out (stop following, `agent.updatePosition = false`, a `LateUpdate` holds the NPC at a seat offset around Kami), driven by the page turn instead of the hat:

- **Board**: when Kami starts riding the page edge (`Player.StartRidingPage`), every `NPC` with `isFollowing` boards: `StopFollowingPlayer()`, `updatePosition = false`, short ease to the seat (0.2-0.3 s so there is no snap), then held at `kami.position + seat` (X mirrored with Kami's facing, which `ForceFacing` already sets per turn direction).
- **Land**: when the turn finishes (`OnPageFinishTurning`, i.e. after `PlayerPageSpawnManager.FinishRide` has placed Kami), `agent.updatePosition = true`, `agent.Warp(NavMesh.SamplePosition(...))` next to Kami on the **new** page's mesh (it is active by then: `OnNewPageOpen` fires seconds earlier), then `StartFollowingPlayer()`. This is `PaperPlaneRide.TryLand`; extract or share it rather than copying.
- Multiple followers (Natalia + Abuela): one seat and one landing slot each, same as `_nataliaSeat` / `_abuelaSeat` / `landingSlot` there.

### Traps already visible (put these in the spec)
- **Execution order.** `Player.LateUpdate` sets Kami's position from the edge bone; the riders must run *after* it or they lag one frame (visible as a wobble, since the edge moves fast). The project has no `ScriptExecutionOrder` asset. Use `[DefaultExecutionOrder(...)]` on the new component, or compute the seat from the bone directly. `PaperPlaneRide` has the same latent issue (harmless there because Kami moves via `CharacterController` in `Update`).
- **Forced turns.** `TurnToNextPage(ridePage: false)` (the arrest) must NOT board followers: `ArrestCutscene` / `PoliceStationPage` warp Natalia and the Abuela themselves. Boarding must key off `Player.IsRidingPage`, not off every turn.
- **Turning back** (`HojaMaster_Rev`, `pageExitX`): seat X mirrors, nothing else changes. Test both directions.
- **Conflicts with `PaperPlaneRide`**: riders of the plane hat during a turn. `PaperPlaneRide.OnDisable` already force-dismounts when its page is switched off; make sure the two never own the same NPC.
- **Death / respawn mid-turn** and **Flap open during the turn**: followers must not be stranded in "boarded" state (landing must also run if the turn is interrupted).
- **Level 1**: the same component would also carry the Abuela if she is following during a turn. Probably desirable, but confirm it doesn't alter the boss-fight flow.
- **Art**: placeholder sprite + bob for now; a real "holding on" pose is a Valentino item (the same way 6.F treats the plane seats).

### Decisions (Diego, 2026-10-08)
- **Every follower boards**, even one far from Kami at the start of the turn: ease to the seat so they always arrive together.
- **Level 2 only.** The Abuela rides in Level 2 (she is a follower on page 4+). Level 1's Abuela flow is untouched: the component must not run in Level 1.

---

## 2. Kami's Spine physics shaking while she stands still

### Resolution (2026-10-08, confirmed by Diego in Level 2)
The prime suspect below was right. Ticking `Is Kinematic` on `Kami`'s Rigidbody stopped the shaking. Diego's
own bisect of the inheritance factors agrees: X inheritance did nothing, **Y inheritance to 0 stopped the
jiggling (and all physics with it)**, rotation did nothing. So the Y wobble of the root, produced by the dynamic
Rigidbody fighting the `CharacterController`, was being fed 1:1 into the physics. The fix is the prefab flag
(applied to `Kami.prefab` so Level 1 gets it too; Diego had only made it a Level 2 scene override).
Lessons, now in `docs/claude/spine-kami.md` ("Physics constraints"): the feed is 1:1 (my first reading, 100x
weak, was wrong); don't mix a dynamic Rigidbody with a `CharacterController`; a video can't clear a
sub-pixel root wobble; ask Diego what he already bisected before handing him experiments.

### What the evidence says
Measured on the two clips (60 fps recordings, frames decoded with ffmpeg, analysis with numpy; scripts were scratch only):

| Fact | Evidence | Consequence |
|---|---|---|
| **Kami's root transform looks steady to the eye.** | Template-tracking the shoes across the whole Level 2 clip: they move at most 1 px (half-res, about 0.02 world units) in x and y. Kami's world rotation is identity in the scene, so the rotation feed is constant. | **This does NOT clear the position feed** (corrected after Diego's answers, see the next row): a sub-pixel oscillation is invisible in the video but can still be large for the physics. |
| **The shaking is in the physics-driven parts.** | 6 consecutive frames at 60 fps: the cape, pleated skirt and hair buns change shape from one frame to the next while feet and torso stay put. | It really is the physics constraints, not the body pose. |
| **The transform feed is 1:1, not negligible.** (An earlier version of this doc said it was ~100x too weak. **That was wrong**: spine-unity applies `SkeletonDataAsset.scale` (0.01) when it loads the JSON (`SkeletonDataAsset.cs`: `Scale = scale`; `referenceScale` is scaled too), so skeleton units ARE local Unity units.) | `SkeletonRenderer.ApplyTransformMovementToPhysics` feeds each frame's world-position delta, divided by the skeleton's 0.35 scale, straight into every constraint. The physics bones are only ~1-3 units long (`15_moño_front` 1.07, `21_pollera` 2.95, ...). A 0.02 u wobble of the root is therefore ~0.06 local units, **about 2-6% of a bone length every frame**, and with `inertia` up to 0.5 it becomes a rotation kick that alternates sign frame to frame. | A root that wobbles by less than a pixel in the video can fully explain the shaking. Diego's original instinct ("something in Update micro-positions her") is back on the table. |
| **Diego's own bisect (2026-10-08)** | Shakes in **every skin, every animation, every `Player`-driven Kami**, **always** (standing, walking, running). The pure-animated menu Kami never shakes. Started **right after the Atlas 13 import**. | The rig (skin, animation) is not the discriminator. What differs is what surrounds the skeleton: the moving transform and what moves it. The Atlas 13 import only matters because it is the first rig that HAS physics: Atlas 12 had none, so a jittering root was always there and always invisible. |
| **Same data, same component settings.** | `Kami.prefab` and the menu's title Kami reference the same `skeleton_SkeletonData.asset` (Atlas 13) and all `SkeletonAnimation` fields are identical (only `zSpacing` and the initial skin differ). | The shake cannot come from the SkeletonAnimation configuration. |
| **A non-kinematic Rigidbody with gravity sits on the same object as the `CharacterController`.** | `Kami.prefab`: `Rigidbody` mass 1, **`useGravity 1`, `isKinematic 0`**, interpolate off, rotation frozen; `CharacterController` height 4.45 / radius 1 / skinWidth 0.08. Nothing in `Assets/Scripts/Player` touches the Rigidbody. Fixed timestep is Unity's default 0.02 s (50 Hz) while the Player moves in `Update`. | Unity says not to mix a dynamic Rigidbody with a CharacterController: gravity integrates at 50 Hz and writes the transform while `cc.Move` writes it at frame rate, so the root can oscillate by a few hundredths of a unit even while she "stands still". It is exactly what the menu Kami lacks. The Rigidbody is presumably there only so trigger callbacks fire (a kinematic one keeps those). **Prime suspect.** |
| **The main menu is NOT a different rig.** The active title-screen Kami (`kami_spine_titlescreen`) is on **Atlas 13**, the same export and the same 19 physics constraints as gameplay. Only the other main-menu skeleton (`The Paper Model`, inactive) is Atlas 12, which has no physics at all. | The comparison "the main menu doesn't shake" is a valid control for *export vs runtime*, so the export's parameters alone are not the whole story. |
| **...but the control is not clean.** | Menu: `TitleScreen` animation (sitting), skin `Libro 1`, no `Player`, static. Gameplay: `Idle`, skin `Diario` (activates 2 skin-scoped transform constraints, `Bun-Detective`, `DetectiveBow`), `Player` + `CharacterController` + scale 0.35. | Four things differ at once. Each needs isolating. |
| **Frame-time irregularity does not separate the two.** | Both clips hold ~45-48 effective fps in the Editor (19-25% duplicate frames). The menu clip is calm under the same conditions. | Variable `dt` vs Spine's fixed 1/60 physics step is not, by itself, the cause. |
| **Skin recomposition is not per-frame.** | `Editor.log` has 6 `[PlayerView] gear skin recomposed` lines, one per play start. | The `SetSkin` + `SetSlotsToSetupPose` path is cleared. |
| **Nothing in `Assets/Scripts` calls `UpdateWorldTransform`/`Update(0)`/`ApplyAnimation`,** and animations are set only on state changes (`OnStateChanged`), not per frame. | Grep. | An extra per-frame skeleton update from our code is not the cause. |

### Suspects (after Diego's bisect, most to least likely)
1. **The Player root's micro-motion feeding the physics**, with the dynamic Rigidbody + `CharacterController` pairing as the likely source (also: `cc.Move` with a tiny downward velocity every frame against a 0.08 skin width; `PlayerPageSpawnManager`/ride teleports are one-offs and not this).
2. **`Player`-side writes to the skeleton**: `PlayerView` sets `Skeleton.ScaleX` (a flip is a world-space jump for the physics) and runs `SetSkin`/`SetSlotsToSetupPose` on gear changes. Neither runs per frame while idle (6 recomposes in the whole log), so low.
3. **Spine runtime/version**: 4.2.43 export on the vendored 4.2 runtime. `PhysicsConstraint.Update` read: stable for these parameters under a fixed step, and the menu Kami proves the rig itself is calm. Low.

Eliminated by Diego's bisect: skin (Diario / Libro 1), animation, level, and the `SkeletonAnimation` configuration (identical). Eliminated by measurement: skin recomposition per frame, extra `UpdateWorldTransform` calls from our scripts, Editor frame-time hitches as the sole cause (the menu clip runs at the same ~45-48 fps and is calm).

### Experiments (Diego, Level 2, Play mode; each ~30 seconds, no code)
Select `Kami` (root) and `Kami > The Paper Model (SkeletonAnimation)`:

- **E1 (decisive): Physics Inheritance to 0** on The Paper Model: `Physics Position Inheritance Factor` = (0,0), `Rotation Inheritance Factor` = 0. **Calm -> the root's movement is what shakes it.** Still shakes -> the root is exonerated, go to E4.
- **E2: Kinematic Rigidbody.** Tick `Is Kinematic` on `Kami`'s Rigidbody. Calm -> the dynamic Rigidbody is the source of the wobble (fix = make it kinematic in the prefab; check that every trigger still fires). Still shakes with E1 calm -> the `CharacterController` itself is the source (E3).
- **E3: No Rigidbody and no gravity on the CC.** Optional; only if E1 is calm and E2 isn't. Untick `Use Gravity` on the Rigidbody, then in a scratch scene drive the CC with zero downward velocity. Tells us if `cc.Move`'s per-frame downward push is the oscillation.
- **E4: Menu Kami moved by hand.** In `MainMenu`, wiggle the title Kami's `Transform.position` by ~0.02 in play mode (type values in the Inspector) while she plays. **If she starts shaking, the physics inherits movement and the diagnosis is closed**; if not, the cause is elsewhere and I'll need a probe.
- **(Not a Diego task) probe, only if E1-E4 don't settle it**: a temporary component logging the per-frame world delta fed to the physics and the count of `UpdateWorldTransform(Physics.Update)` calls per frame, then deleted.

### Candidate fixes, by layer (do NOT apply before an experiment points at one)
- **If E1 is calm and E2 is calm (Rigidbody)**: make the Rigidbody kinematic in `Kami.prefab` (and the same on any prefab with the same pairing, e.g. NPCs/enemies if they shake too). Zero code. Verify triggers/hitboxes still report.
- **If E1 is calm but E2 is not**: don't feed the physics the raw root position. Options in order of preference: set `Physics Position Inheritance Factor` to (1, 0) or lower so vertical wobble is ignored (loses some trailing when jumping); smooth the feed (a small custom `SkeletonAnimation` subclass or a dead zone below ~0.05 u per frame); fix the `CharacterController` micro-motion at the source.
- **If it's the root and she should still trail when running**: keep horizontal inheritance on and tune it; check the feel while running because the feed is 1:1 now.
- **Unity side, regardless**: call `SkeletonAnimation.ResetLastPositionAndRotation()` (public) after every teleport (`PositionPlayerAtPoint`, `StartRidingPage`/`StopRidingPage`, `WarpTo`) so the page-turn ride's multi-unit jump is not injected as a giant translation, and `skeleton.UpdateWorldTransform(Skeleton.Physics.Reset)` once on gear change if the skin swap leaves constraints stale.
- **Spine export side (Valentino)**: reduce `inertia` (several are at the 0.5 ceiling), raise damping on `hips*`/`tapado*`, drop strengths that need a >60 Hz look; make sure the skin-scoped constraints don't fight the physics order; delete the `Test/Cape Test` animation from the shipped export.
- **Re-measure after each single change** (same two metrics: shoes stay put, cape/skirt frame-to-frame energy goes down).

### Related finding (not the cause, but it will bite at page turns)
`Player.LateUpdate` teleports Kami along the page edge, and `PositionPlayerAtPoint` teleports her to the new page. The Spine runtime reads that as a huge translation and kicks every constraint (hair, cape, skirt) at the end of each page turn. `ResetLastPositionAndRotation()` after those teleports (see above) is cheap and safe, and belongs in the same spec.

---

## 3. Main-menu Kami: Libro 1 outfit, original gear, no scissors

### State today
- The visible title Kami is `kami_spine_titlescreen` (root of `MainMenu.unity`, active, **Atlas 13**), `initialSkinName` = `FullSkins/Kami Diario` **in the saved scene** (the clip shows `Libro 1`: unsaved edit).
- `Level1_StartLoadout.asset` = `Gear_OutfitDefault` (= `FullSkins/Kami Libro 1`, no shoes) + `Gear_ShoesBase`, no scissors. That is "Libro 1 with her original gear, no scissors".
- The second skeleton, `The Paper Model (SkeletonAnimation)` (inactive), is still on **Atlas 12** with `Tijera_Normal` as its skin. If anything ever enables it, it shows scissors and has no physics.
- Atlas 13's Libro 1 skin has **no shoes** (they live in `Zapatos/*`), so `initialSkinName = Libro 1` alone leaves her barefoot. The correct look needs the composed skin, not a single named skin.

### Proposed solution
A tiny component on `kami_spine_titlescreen` (e.g. `LoadoutSkin`) that, in `Start`, composes the skin from a `GearLoadout` asset with the existing `SpineSkinComposer` + `GearCatalog` (the same code Level 1 uses) and defaults to `Level1_StartLoadout`. Reusing the loadout means the menu can never drift from Level 1's look. No code in `Player`, no new asset.
Plus: decide what to do with the inactive Atlas 12 skeleton (point it at Atlas 13 + the same component, or delete it if it's dead; check its `Loading Animation` references first).

### Notes
- Both menu clips will inherit the physics experiments' result: if E3 says the rig is fine, the menu Kami now has live physics, so the title animation should be re-checked once.
- Part of the existing note in `CLAUDE.md` ("MainMenu's two static skeletons are still on Atlas 12") is out of date: the active one is on Atlas 13. Fix the doc as part of the spec.

---

## Unrelated, spotted in `Editor.log` while checking
`PedestalColorCheck.SetPedestalColor` throws a `NullReferenceException` (lines 27 and 39) on every `OnResourceUpdated` during Level 2's start-of-level grants and in its own `Start`; `EventManager` swallows it, but every startup logs 6+ of them. Worth its own small issue.
