# Nivel 2 (Newspaper) y sistemas de UI

## Estado de Nivel 2 (relevado agosto/septiembre 2026)

`Assets/Scenes/Level2_Newspaper.unity`. Base geometry, decoration, the left building
(with its own animator, `d5a027d`), and several newspaper pages are integrated and
stable. Blocking for all 5 pages is in progress on the `pages-blocking` branch (pages
3 and 4 in progress as of 2026-09-22).

**Phase 0 of the implementation (spec 006) landed 2026-09-22** — see
`specs/006-nivel2-detective-natalia/tasks.md` for the full phase breakdown. What exists
now, ready for pages 1-5 to wire up:

- **Ambient traffic** (`Assets/Scripts/Traffic/`: `TrafficObstacle`, `TrafficObstacleSet`
  ScriptableObject, `TrafficSpawner`; prefabs in `Assets/Prefabs/Traffic/`) — a spawner
  moves instances in a straight line between two points, then despawns them. Wraps the
  existing `Paper Car Prefab aniamted.prefab` art without modifying it. Only a car
  variant exists today — a pedestrian variant is just a new prefab wrapping different art
  plus the same `TrafficObstacle` component, no new code. To use: drag
  `TrafficSpawner.prefab` into a scene, reposition its `SpawnPoint`/`DespawnPoint`
  children, assign a `TrafficObstacleSet` asset.

  **Car and poster variants (2026-10-15)**: Valentino's `Assets/3D/Cars/PaperCar variacion 1-3.fbx` and `PaperCar Policia.fbx` are
  wired as prefab variants in `Prefabs/Traffic/CarVariants/`: `Paper Car Stationed Variacion 1-3` (of `Paper Car Stationed Variant`),
  `Paper Car Idling Variacion 1-3`, `TrafficObstacle_Car Variacion 1-3` (also in `TrafficObstacleSet_StreetCars`, so moving traffic
  mixes six cars) and `TrafficObstacle_PoliceCar` (the three patrol cars: `PoliceCar_Parked`, `CrimeSceneGate`, `ArrestCutscene`).
  The new meshes are in metres (the old mesh asset is in cm), so each variant sets the `Mesh` child's scale to 1.5 (was 0.015),
  moves it so the body is centred like the old one (their pivot sits ~0.3 m above the body's centre) and repositions the windows.
  Scene cars were re-pointed with `tools/replace-prefab-instance.py` (variant ids = base id XOR the variant's PrefabInstance id).
  The 8 `CuttablePoster` instances use `CuttablePoster 2-5` in rotation. Page 4's `Paper Car Stationed (9)` stays the original car.
  **Tuning is travel duration (seconds to cross), never speed** — that's the fix for the
  first playtest bug (2026-09-22): a default of 2-4 units/second meant cars needed 20-40s
  to cross an 80-unit street while spawning every 5-10s, so they piled up at the spawn
  point and looked like they never despawned. Units/second is meaningless without knowing
  the segment length; duration reads the same on any street, and the spawner derives the
  per-instance speed from the real distance. Two safety nets back it up: `maxAlive` (a
  spawn is skipped while at the cap, so obstacles physically cannot stack) and
  `maxLifetime` (a hard despawn that fires even if arrival somehow never happens).

  **Cars hurt and carry Kami (spec 009, 2026-09-30, played and confirmed by Diego).** A front hit costs
  `damage` (25 of her 120 HP) and shoves her sideways out of the lane; standing on a car's roof
  carries her along. Sides and tail keep the old solid shove, no damage. Pieces:
  - `TrafficCarHitbox` (`Traffic/`): trigger box + kinematic Rigidbody on a `CarHitbox` child of the
    car's SOLID collider object (`Mesh`, under the animated art), in both car prefabs (the Dark/Gray
    variants inherit it). The frontal test runs at runtime from `TrafficObstacle.TravelDirection`, so
    one prefab works driven either way. All tuning is on the component (damage, `frontFraction`,
    `pushDistance`/`pushDuration`, `immunitySeconds`, `hitSound` = `CarHorn`). By default it fits
    itself to the mesh bounds at spawn (`fitToSolidCollider`; `heightFraction` keeps it below the
    roof, `padding` makes it fire before the shove); turn that off to author the box with the gizmo.
  - `IImpactReceiver`/`ImpactInfo` (`Player/`) and `IMovingGround` (`Traffic/`): the seams. The car
    knows nothing about Kami and Kami nothing about cars (`Player` implements the first,
    `TrafficObstacle` the second).
  - **The car art has its own looping Animator** ("Andando"): it slides the `Mesh` collider along local
    Z (about 17 u/s) on top of the root's own motion, so the real velocity is NOT
    `TrafficObstacle`'s speed. `TrafficObstacle` therefore MEASURES the velocity of its solid collider
    in `LateUpdate` (after the Animator) and caches it; `GroundVelocity`/`TravelDirection` read only
    that cache (safe after Destroy). It stops carrying `releaseSecondsBeforeDespawn` (0.75 s) before
    the root's despawn point.
  - `Player`: `ReceiveImpact` (ignored when dead, in a cutscene, riding a page or immune),
    `KnockbackVelocity` (linear decay, area = `pushDistance`), `OnControllerColliderHit` ground
    tracking (`groundNormalMinY` 0.6, `movingGroundLatchSeconds` 0.1) -> `GroundVelocity` /
    `IsOnMovingGround`. Both velocities are summed into the SINGLE `cc.Move` of
    `PlayerModel.ApplyPhysics`, next to the wind (a second horizontal move brings back issue #30).
    No last-safe-position snapshot while on moving ground; `lastDirection` stays input-only so the
    paper plane glide does not inherit a car's speed.
  - Knockback side at the car's axis: she is pushed against her own sideways `CharacterController`
    velocity (the hitbox cannot read `Player.lastDirection` through the interface).
  - Followers (Natalia, Abuela) are not carried and take no damage. The page 2 crime-scene car and
    the page 3 patrol car are instances of the same prefab, so they carry the hitbox too (the arrest
    car is covered by the cutscene filter, the page 2 car can hurt if Kami stands in its way).
- **New cuttables** (`Assets/Prefabs/Cortables/`): `CuttablePoster.prefab`,
  `CuttablePoliceTape.prefab`, `CuttableRibbon.prefab`, `CuttableRope.prefab` — all built
  on the same bush pattern as `Arbusto 1.prefab` (`ObjetoCortable`: whole sprite off →
  base + top on → top thrown in an arc and shrunk). The poster uses **stock
  `ObjetoCortable`** with no new script. The other three use `CuttableTwoPieces`
  (`Assets/Scripts/Cortables/`), a subclass for strips that split sideways into two halves
  that BOTH fly out, reusing the inherited slots as whole/left/right and adding a
  `UnityEvent onCut` for the ribbon (gift box) and rope (catapult). Each prefab root has a
  trigger `BoxCollider` **plus a kinematic `Rigidbody`** — two triggers only report a hit
  if one side carries a Rigidbody, so without it the scissors would silently never cut
  them. Sprites are bush stand-ins until Valentino draws the real art.
- **Evidence items**: 4 new `ResourceType` values (`caughtBelonging` — the hat,
  `brokenWatch`, `arielScarfCap` (**retired 2026-09-28**: Ariel's evidence is the hat + glove; the
  enum slot stays as `unusedArielScarfCap` so the later ints keep their meaning, its item asset and
  ItemTable keys are gone), `pelusaPainting`; `cafeTicket` (2026-09-27, given by the page 1
  café fold through `OrigamiItemGiver`, see `origami-y-tooltips.md`) was appended after `lostGlove`;
  `lostGlove` was appended as a 5th in
  Phase 2, in both InventoryManager lists) + matching `InventoryItem` assets in
  `Assets/Scripts/Inventory/` (sprites left empty, no art yet), registered in
  `InventoryManager._allItems`.

  **Careful — the two levels wire InventoryManager differently.**
  `Nivel1_KamiPapelTijera.unity` instantiates `Assets/Prefabs/Managers/InventoryManager.prefab`,
  but `Level2_Newspaper.unity` has a **standalone, non-prefab** `InventoryManager` component
  with its own hardcoded copy of the item list. So editing the prefab alone never reaches
  Level 2 — both had to be updated. Worth collapsing onto the prefab eventually (its
  `_inventorySlotsParent`/`_showcaseSlot` point at scene objects, so that swap needs the
  Editor, not YAML). Note `InventoryManager.Start()` builds a `ResourceType`-keyed dictionary
  with `.Add()`, so a duplicated item in that list throws at startup.

**Phase 2 (page 2, "Find clues") built 2026-09-24, played in Diego's full playthrough (2026-09-28).** Three
clues (glove in a trash can, hat on the manhole, watch in the museum yard), two cops that
drive off after the second clue so the police tape can be cut, and the quest handoff to
`Quest06_GoBackToNataliasHouse` (closed by the page 3 arrest since Phase 3). What exists and where:

- **Every trash can in Level 2 is now `Assets/Prefabs/Interactables/TrashCan.prefab`**
  (15 of them, pages 1-5; the scene instances used to be the bare `Kami_TrashCan.fbx`).
  Opening one runs the stock `Solapa`/`TriggerSolapa` flap flow (Kami's `PullSolapas`
  gesture) and gives 2 paper once. `TrashCan_ClueGlove.prefab` is a variant that gives the
  glove (page 2's `TrashCan_ClueGlove`). The contents are a `GrantResourcePickup`
  (`Assets/Scripts/TriggerS/Pickups/`) in Revealed mode: opening = collecting, and it
  switches itself off (shrunk to nothing; it used to destroy itself), so later opens find the can
  empty. `TriggerSolapa.ResetToFresh()` closes a can silently and refills it (`Solapa.ResetClosed`,
  `GrantResourcePickup.ResetPickup`); only page 4's restart calls it (#219). The per-can "Newspaper 1" material
  override the old instances had was carried over. The model sits under a `Wobble` pivot at
  scale 0.012876 so the placeholder open/close squash (`Assets/Animations/TrashCan/`) has
  something scale-1 to animate; Valentino's lid animation replaces those two clips (done 2026-10-15: `Trashcan.fbx` with a separate lid, clips `TrashCan_Opening`/`Closing` swing `Trashcan_Tapa` on a back hinge, position + rotation, and `TrashCan_Open` holds the opened pose because an empty Animator state would snap the lid back shut; transitions are instant).
- `Assets/Prefabs/Level2/`: `CrimeSceneGate` (tape + blocker + cops + car,
  `Assets/Scripts/Level2/CrimeSceneGate.cs`), `CluePickup_Hat`/`CluePickup_Watch` (walk-into
  pickups), `FindCluesTracker` (`Assets/Scripts/Level2/FindCluesTracker.cs`: counts clues,
  queues Natalia's comments, calls off the cops, fires `OnAllCluesFound` once, hands off the
  quest) and `GiftBox` (`GiftDialogueTrigger`, extended in Phase 3 — see above).
- **The museum's main gate is broken (#180, 2026-10-13)**: the robber got in and out through it. The fbx's two
  `RejaCentral Puerta` doors are switched off in `Museum Page Parent.prefab` (shared by pages 2 and 5) and replaced
  by `Prefabs/Level2/Reja Central Rota.prefab` (Diego posed the doors fallen and bent, convex MeshColliders), plus
  extra fence pieces with MeshColliders. `CrimeSceneGate` (page 2 only) moved to that gate, its blocker resized to the
  gap; on page 5 the gap is open. The yard slabs `Cube (11)` / `Cube (16)` use `Materials/Libro/Pasto.mat`. Page 5's
  NavMesh must be rebaked through the gate (Ariel's arrest walks it).
- **The museum fence is bars (#155, 2026-10-13)**: every fence panel (`RejasYColumnas/Reja1-8` in
  `Museum Page Parent.prefab`, and both fallen doors in `Reja Central Rota.prefab`) is an instance of
  `Prefabs/Level2/Reja/`: one base `Reja.prefab` (root + `Panel` with the BoxCollider, Navigation Static) and one
  variant per fbx mesh, named after it: `Reja_3/4/5/6/8/9`, `Reja_PuertaDer/Izq` (scene Reja1 and Reja8 use
  `Reja_8`, Reja2 and Reja6 use `Reja_6`, Reja7 uses `Reja_9`). Each variant's `Panel` sits on its old mesh's plane,
  and its cubes (bars every ~1 unit, a bottom rail, a rail at the old rectangle's top, arch segments following the
  old top edge) use the cell fences' `Newspaper 4 Dark`, the same recipe as `CellFence`. The bars are visual only:
  the Panel's box is the collision. Built by a script from the fbx meshes' outlines; the columns (`Columna*`) still
  have their fbx MeshColliders.
- **The catapult is one prefab for pages 2 and 5 (2026-10-15)**: `Prefabs/Level2/Catapulta.prefab` = Pochi's
  `Catapulta.fbx` (posed by Diego in `Museum Page Parent`) + an Animator on the root (`Animations/Catapulta/`: states
  Disarmed (default, arm up) -> `Arm` trigger -> Arming -> Armed -> `Shoot` trigger -> Shooting -> Disarmed; clips animate
  `Palo_Catapulta`'s z rotation, so the pose is edited in the Animation window) + a solid `BaseCollider` + a `Gameplay`
  child holding everything the placeholder had (`Catapult`, trigger, `CutsceneDirector`, `PlayableDirector`,
  `SignalReceiver`, rope, seats, walls, floor, flying girls, flight target, `CM Catapult Hold`). Page 2's museum instance
  overrides `Gameplay` inactive (#181's scenery: stays disarmed); page 5's overrides `_fader` with the scene's
  `Canvas/ScreenFade`. `MuseumPage._catapult` points at the nested component through a stripped block.
  `Catapult.Unlock` plays the arming animation and the catapult can be boarded once it reaches `Armed`; `CUE_Launch`
  fires `Shoot`. `BucketFrame` and `BaseCollider` are yawed 157.4 deg to match the model's override in the museum
  prefab: if the model is rotated again, rotate those two with it. The old `Catapult_PLACEHOLDER_POSITION` stays in the
  scene switched off (safe to delete).
- Page 5 is page 2 after the clues (Diego): none of the page 2 gameplay objects live there.

**Phase 3 (page 3, "The trap") built 2026-09-25, played in Diego's full playthrough (2026-09-28).** Chain: talk to
the gift ("what's this? let's cut it open!") → cut its ribbon (any time, talked to or not) →
the box opens, the Pelusa pops into the inventory, Natalia reacts → the letter origami opens by
itself → reading it and closing the text starts the arrest cutscene → the page turns to page 4
with Kami and Natalia in separate cells → Quest06 closes and `Quest07_EscapeAndReturnThePainting`
starts. Where things are:

- `GiftBox.prefab` now holds everything: the nested `CuttableRibbon`, the real model (`Caja de Regalo.prefab`,
  2026-10-15: cutting the ribbon fires the `Open` trigger of the Animator on the GiftBox root, whose clip
  `Animations/GiftBox/GiftBox_Open` swings `Caja_Regalo_Tapa` up on a back hinge, position + rotation, tunable in the
  Animation window; the old closed/open cubes and `PelusaVisual_PLACEHOLDER` are left inactive and unused),
  `PelusaPickup` (a `GrantResourcePickup` in Revealed mode, `pelusaPainting`, `_collectDelay` 3s so the Pelusa is on
  show before it flies to the inventory; `GiftDialogueTrigger._revealSeconds` 5s holds Natalia's reaction and the letter
  pedestal back until then) and a
  nested `SelloOrigami Letter` pedestal with `_promptAutomatically` on. The scene wires that
  pedestal's `origami` to `Canvas/Origamis/OrigamiRoute_Letter`, which shows its text on the new
  `Canvas/OrigamiTextRevealPanel` (`Assets/Prefabs/UI/`). The loose Phase 0 ribbon and a leftover
  `NPC_Florista` were removed from page 3.
- `ArrestCutscene_PLACEHOLDER_POSITION` (instance of `Prefabs/Level2/ArrestCutscene.prefab`, at
  the **scene root** on purpose) + `Assets/Timelines/Level2/Timeline_Arrest.playable`. Ariel and
  the cops are tinted Natalia sprites (`CopEscort.prefab` = an `NPC` that can follow Kami). How to
  retime it: `docs/claude/cutscenes.md`.
- Page 4: `KamiCell_PLACEHOLDER_POSITION` and `NataliaCell_PLACEHOLDER_POSITION` (moved into the
  real cells in Phase 4, see below). The forced page turn drops Kami on hers, which also makes it
  her respawn point.
- **Phase 1's café pedestal is wired too** (task 1.D): it was already in the scene at the root,
  inactive and pointed at the café fold route. It now lives under Page 1 (same world position),
  auto-prompts, and appears when Natalia's opening dialogue ends
  (`NataliaDialogueTrigger._activateAfterOpening`).
- All positions are placeholders: the gift box, the cutscene (street group, car, car door mark,
  drive-off target).

**Phase 4 (page 4, "Escape from the police station") built 2026-09-27, played in Diego's full playthrough (2026-09-28).**
`PoliceStationPage` (`Assets/Scripts/Level2/`, on `Page 4/PoliceStation`) owns the whole page
sequence and is the only thing that restarts it:

1. The arrest's `WrapUp` calls `PoliceStationPage.BeginFromArrest()`. Kami (west cell room, whose
   east wall is now a `CellFence`) and Natalia (a new fenced cell in the front-left room, AROUND the
   paper-plane pedestal) are locked up, and `ConfiscatedGear` takes Kami's scissors, ALL her paper
   and the Pelusa.
2. The narrator's line (`Narrator_JailIntro`, `_introLine`, first time only) plays half a second
   after the girls land — the dialogue itself keeps Kami still. `_abuelaDelaySeconds` (3) after it
   closes `AbuelaEntrance`
   drops the Abuela (`Abuela_Follower` prefab variant) next to the fence: Kami is locked (`inCutscene`)
   from the start of that countdown until her two lines (`Abuela_JailLanding`) end (#172, 2026-10-13: it
   used to be only the fall and the lines, and the free wait read as "go explore"); the fence disappears
   (reveal, not destruction), she follows Kami. After a capture the same lock covers the shorter countdown.
3. `ConfiscatedGearPickup` (where Diego had placed the TijeraPickup, just east of the cell) gives
   everything back. Kami cuts the `CandadoCortable` on Natalia's door (moved there, 1 paper as
   before) -> the door opens and Natalia follows. Freeing her also opens the pedestal:
   `OrigamiRoute_PaperPlane` (the Level 1 Avion route: 2 folds, 2 paper, reusable) under
   `Canvas/Origamis`, classic step-on + Interact.
4. The hat's one augmented jump (22 units) reaches the mezzanine (~18 above the floor). The window is
   `Door (8)` (upper front-right; its collider is off), covered by `CuttableDrapes`. Past them,
   `EscapeWindow` completes `Quest07_EscapeThePoliceStation`, starts `Quest08_ReturnThePelusa`, warps
   both followers to `OutsideLanding` below the window, makes that Kami's respawn point and blinds
   the cops. The page is then left the normal way (edge of the book), no forced turn.
   `OnPoliceStationEscaped` (what `PageExitLock` opens page 4's next sphere on) fires on every crossing,
   with or without Quest07 held (#169, 2026-10-13): a lost Quest07 used to keep the sphere locked for good.
5. **Getting caught restarts the whole page** (Diego): on the respawn after a `DeathCause.Caught`,
   everything above goes back to step 1, the Abuela falls again after only
   `_abuelaDelayAfterCaptureSeconds` (3), the cops return to their posts. Her crash shakes the camera
   (`AbuelaEntrance._shake`, a `CinemachineImpulseSource` with the package's `6D Shake` signal; #227, 2026-10-16, not played):
   Amplitude Gain and Impulse Duration on that component are the knobs, and the four pickable vcams in
   `Camara Main and Set.prefab` carry a `CinemachineImpulseListener`. The page 4 intro vcams do not: the crash happens after the
   intro timeline. The stash only grows:
   paper Kami gathered before being caught again is returned at the pickup too; the plane hat is lost.
   The page's three trash cans (`PoliceStationPage._trashCans`) close and fill up again (#219,
   2026-10-15): the paper spent on the fold and the padlock is gone and both must be redone, so without
   it a capture could leave Kami waiting on the typewriters' 30 s respawn.

The cops are `PoliceOfficer.prefab` (male) and `PoliceOfficer Female Variant` (since spec 014, 2026-10-09) walking
the four corners of Diego's "Poli Path" rectangle (`PoliceStation/PatrolRoute`), half a lap apart (the female starts
at corner B, `_startWaypointIndex` 2); the blocking placeholder "Poli" (with the reference cone) is inactive.
See `enemigos-e-ia.md`. The paper sources are the two desk typewriters (2 paper, respawn 30s); the
stray Phase 0 typewriter at the page origin was deleted. **No NavMesh rebake was needed**: page
NavMeshes are the classic bake of Navigation-Static objects, and every fence is non-static with a
carving `NavMeshObstacle`, so opening one opens the path at runtime.

Also removed in Phase 4 (Diego's call): the inactive Level 1 leftover `Objetos_Pagina_6 Abuela`
under `Nivel (Mesa y Libro)` — it held a whole copy of Level 1's page 6 (Rocoso encounter, dam,
river, props), all inactive. Recoverable from git (commit before Phase 4) if ever needed.

**Level 2 starts with the normal scissors** (Diego): `Player._startWithTijera` is on for Level 2's
Kami, equips them a frame after start without the reward pose. There is no upgraded pair in Level 2;
the P cheat still gives one. Confiscating it works (`LoseTijera` no longer drives the normal-scissors
count below 0), but after getting it back the upgraded pair shows the normal trail — cheat-only.

**Phase 1 (page 1)** — `Assets/Prefabs/NPCs/Natalia.prefab` exists and is
drag-and-drop (placed in Page 1); see `docs/claude/quests-y-dialogos.md` for her dialogue/
quest wiring and the Event-quest gotcha that comes with it. **Since 2026-09-28 the café fold is
mandatory**: Natalia only joins Kami (Quest05 + follow) once the café ticket is folded, so Kami
always reaches page 5 with it.

**Phase 5 (page 5, "Clearing their name") built 2026-09-28, played in Diego's full playthrough (2026-09-28).**
`MuseumPage` (`Assets/Scripts/Level2/`, on `Page 5/Museum Sequence`) owns the page:

1. Walking up to **Grace** (`_approachRadius`, 12) or talking to her (the museum director, `Prefabs/NPCs/Grace.prefab`:
   a tinted Natalia sprite + `GraceDialogueTrigger`, whose lines `MuseumPage` picks) with the Pelusa, the café ticket
   and the three clues starts the meeting (#182: no E press needed; without the evidence the approach does nothing and
   E gets `Grace_NoProof`). Her meeting dialogue ends -> the ticket pedestal (`SelloOrigami CafeTicket`, a
   `SelloOrigami Cafe` instance pointed at `OrigamiRoute_Cafe_Unfold`, 0 paper, auto-prompt) appears.
   **Ariel and two cops are there** (Spine since spec 014: `Ariel.prefab` + the Old Male / Femme `CopEscort`
   variants, still named `Ariel_PLACEHOLDER_POSITION` / `CopWitness1|2_PLACEHOLDER_POSITION`) from the start, taking
   Grace's statement about the robbery, their car (`PoliceCar_Parked`) parked in the street. The meeting is where
   Grace introduces herself, and Natalia introduces herself and Kami.
2. The unfold shows the ticket (the girls' receipt from page 1: 2 hot chocolates, paid at 21:47) on
   the letter's `OrigamiTextRevealPanel`; closing it plays `Museum_Evidence`: both girls were at the
   café at 21:47 and heard of the robbery on its radio, Ariel's hat + glove, the watch stopped at
   21:47, Grace pointing out Ariel's access through his father, the police apology and arrest, Ariel's apology to Natalia and Grace's thanks.
3. When that ends the cops march Ariel to their car and drive off (`MuseumArrest`, `Timeline_MuseumArrest`, #183,
   see `cutscenes.md`). Once the car has left and Kami is free, **Grace offers the catapult** (`Grace_CatapultOffer`,
   `MuseumPage._catapultOffer`, phase `Offer`; #223, 2026-10-16: it used to be the end of `Museum_Evidence`, so she
   helped while Ariel was still free; keys `grace_catapult_offer_01-04` in `DialogueTable`, formerly
   `museum_evidence_11-14`). When it ends the case closes: the five items leave the inventory (handed over), `OnPaintingReturned` completes
   Quest08 (removed a frame later) and ends the story music, Natalia walks to `Marks/NataliaStayMark` (a few units
   from Grace, #184) and only then becomes talkable with her farewell (`NataliaDialogueTrigger.StayBehind`), the
   Abuela walks to `Marks/AbuelaStayMark` (Diego's) and waits there, and the **catapult** (`Catapult`) unlocks.
4. Interact on the catapult = confirm -> short fade (`Canvas/ScreenFade`, `ScreenFader`), Kami lands in the
   bucket (walled in by `BucketWalls`, she can still attack), the Abuela next to her, her line, a
   "cut the rope" post-it. Only now is the rope's trigger on. Cutting it plays `Timeline_Catapult`
   (see `cutscenes.md`): launch, hold on the empty catapult, fade, `Level2_EndCutscene`.

The rope is a `CuttableRope` instance inside the catapult prefab (`Gameplay`), switched off until the catapult is armed (`Catapult.ArmRoutine` turns it on; its trigger stays off until Kami boards). Its 21-unit trigger
sits 3.5 to Kami's right and 5 above her seat (the same offset the placeholder had): her scissor hitbox (3.16 +- 4.27 ahead of her body center,
1.15 +- 2.32 above it, +-1.74 deep) reaches it from anywhere in the 5x5 bucket, facing right.
Everything is placeholder cubes/sprites and `_PLACEHOLDER_POSITION` objects (Grace, Ariel, the cop
witness, the pedestal, the catapult). Starting the scene with `startingPage = 5` gives Kami the five items,
Quest08 and both followers.

**Test starts on pages 2-4 bring Natalia along (#170, 2026-10-13).** Diego tests a page by switching on only that
page's folder, so Natalia (under `Page 1 Objects`) is off and none of her scripts run; before this fix she only woke
up on page 4 when the padlock made her follow, still on her page 1 opening and talkable. Each page owner now calls
`NPC_Natalia.SetUpForTestStart` on its own test start: page 2 `FindCluesTracker` (her and Quest05, following beside
Kami), page 3 `ArrestCutscene` (following, no Quest05), page 4 `PoliceStationPage` (out of page 1's folder, then into
her cell as before). It marks her page 1 conversation done (`NataliaDialogueTrigger.SkipToJoined`) and makes her not
talkable, like a real join. Page 5 keeps `MuseumPage`'s own setup.

**...and the items the earlier pages give (#187, 2026-10-13).** The same test starts call
`Level2/Level2TestStartItems.GrantItemsBefore(page)`: one table in code (page 1 the café ticket, page 2 the three clues,
page 3 the Pelusa), granted quietly (`ownedAtLevelStart`, no sticker), skipping what Kami holds. Page 4 grants them
before `StartSequence`, so the confiscation takes the Pelusa and the pickup gives it back like in the real flow. Page 4
and 5 test starts also raise `OnArrestStarted`, so the story music plays as it would after the arrest.

**A sun per page (#179, 2026-10-13).** `LightCycler` on `Nivel (Mesa y Libro)/Luces/EL SOL` is on in Level 2 with
5 states (index 0 = page 1), all at the light's authored rotation and position: page 1 is the authored warm 0.3, then
cooler and dimmer (0.255 / 0.195 / night blue 0.135), page 5 = page 2. It eases position, rotation (slerp), colour and
intensity over `_transitionDuration` on `OnPageTurnStart`, and applies the starting page instantly. Its old code
snapped to the target and wrote the rotation into the position; Level 1's sun now eases its rotation too.

**Page 3's traffic jam (#177).** Page 3's four stopped cars are `Prefabs/Traffic/Paper Car Idling.prefab`, a variant
of `Paper Car Stationed Variant` (left untouched: pages 1 and 4 use it too) that switches the car's own exhaust
(`HumoChimenea`) back on, in world space with a steady rate, and adds `Traffic/IdlingCar`: a positional engine loop
(`CarEngineIdleLoop`) and a honk every `_hornMinSeconds`-`_hornMaxSeconds` (`CarHornIdling`). How far away they are
heard is on the two AudioBank rows (see `audio-y-particulas.md`).

`Level2_EndCutscene.unity` (`GameScene.Level2EndCutscene`, catalog + Build Settings) is a copy of
`Nivel1_EndCutscene` running `Narrator_Level2Ending` (the flight, the Narrator's blue lamp, a third
book) with empty portraits, then MainMenu: Valentino swaps the frames in the DialogueSO's sprites.

Also removed in Phase 5 (Diego's call): the loose root `CuttablePoliceTape` and `CuttablePoster`
(Phase 0 leftovers; page 2's tape lives in `CrimeSceneGate`), the unreferenced Level 1
`ParticulasSplash`, the inactive `Plane-VertexPaintTest` and page 5's inactive `NPC_Florista`.

**Phase 6 (presentation polish, 2026-09-29) built, played by Diego 2026-10-05 (all good).** 6.A: Level 2's
`OverlayManager.mainQuestTriggeringDialogue` now points at `Natalia_AfterTicket` and the overlay text
uses the new UITexts key `overlay_mainquest_level2` (scene override on the nested LocalizeStringEvent).
6.B: `Timeline_PoliceIntro` (camera tour, cop taunt, Natalia reply, narrator) played by
`PoliceStationPage` first time only via `_introCutscene`; cameras are static vcams under
`PoliceStation/IntroCameras` (drag them). 6.C: `Natalia_Freed` on the padlock cut (first time only) and
`PaperPlaneRide` (followers board on the hat fold, leave on the new `Evento.OnPaperPlaneHatLost`; parked
if Kami lands off-NavMesh). 6.D: `Timeline_MuseumArrival` (its car arrival was removed by #182: everyone is already
there). Patrolling cops are cuttable
(`PoliceOfficerCortable`): knocked out 12s, then resume. `PageAmbience` is placed in the scene.

**Page exits are earned (2026-10-08).** `Level2/PageExitLock` (scene root) keeps a page's next-page sphere off until its
task is done, latched: page 1 `OnCafeWrapperFolded`, page 2 `OnAllCluesFound`, page 3 `OnArrestSequenceEnded`, page 4
`OnPoliceStationEscaped` (an Inspector table of page + event). It calls `PageScrollerManager.SetNextLocked`; the previous
sphere is never locked and the arrest's forced turn ignores spheres.

**Followers ride page turns (spec 012).** `Level2/PageRideFollowers`, see `paginas-y-hoja.md`. `PaperPlaneRide` is
untouched: its riders are not `isFollowing`, so the two never own the same NPC.

**The full 5-page narrative is now defined** (Diego, 2026-09-22): Kami meets Natalia,
they investigate a stolen painting (the Pelusa), get framed by Ariel, end up
arrested, escape with the Grandmother's help, and clear their name at the museum
(`KamiMuseo.fbx`, page 5) before catapulting back to their book. Full spec + phased
breakdown (what current code already covers, what's 100% new) in
`specs/006-nivel2-detective-natalia/spec.md` and `tasks.md`. This replaces the old
"museum page just started" description — the real scope is much larger than that one
page (ambient traffic, a police vision cone, new Origami routes, the Natalia NPC,
etc., all 100% new). Cross-cutting systems (page-turn, inventory, quests, camera,
Flap UI) are production-ready and get reused as the base.

**Water boots visual (2026-10-05)**: getting `botasAgua` equips `Gear_RainBoots` through Kami Gear
(spec 011, see `spine-kami.md`); a yellow placeholder skin shows until Valen exports `feet/rain-boots`
(#83). Since Phase 3 they only keep Kami from drowning while she wears them: tapped off in the bag or
the Wardrobe, the river drowns her.

**Outfits are bag items (spec 011 Phase 3, 2026-10-05)**: `outfitDefault`/`outfitDetective`
(`ItemOutfitDefault`/`ItemOutfitDetective`, in BOTH InventoryManager lists). Level 2 starts wearing the
detective outfit and owning both, so tapping one in the bag or the Wardrobe swaps them; the boots and the
scissors stay on. Granted quietly at level start (no sticker), see "Owned from the start" in
`spine-kami.md`.

## LevelManager

Singleton dueño de los recursos del nivel: `Dictionary<ResourceType, int>` con 8 tipos
(hongos, flores, papel, botasAgua, botasRapidas, tijera, tijeraMejorada, abuela).
Guarda la referencia a `Player`, y los flags de control `agency` / `inDialogue` que
gatean input y diálogo (ver `paginas-y-hoja.md` para el uso de `inDialogue` durante el
page-turn). `AddResource()` dispara `Evento.OnResourceUpdated`, consumido por
`InventoryManager` y `QuestSlot`. Los métodos `GiveSprintBoots()`/`GiveWaterBoots()`/
`GiveTijeraMejorada()` son las recompensas de quest; what each one looks like on Kami is gear (see
"Skins: Kami Gear" in `spine-kami.md`). También decide qué música de nivel usar
(MemoFloraMainLoop en Nivel 1, BohrenDestroyingAngels en Nivel 2). **Level 2 music is per
page since 2026-09-24**: `PageMusicManager` (`Assets/Scripts/Managers/`, instance of
`Prefabs/Level2/PageMusicManager.prefab` at the scene root) holds one AudioId per page in the
Inspector — Bohren on pages 1-2, silence on 3, `DeadEndAngels` on 4, Bohren again on 5. It re-applies
on `OnNewPageOpen` and one frame after start, so it wins over `LevelManager.Start` on a test started from
page 3. **Story music (#176, 2026-10-13)**: `_storyMusic` (`DeadEndAngels` in the prefab) overrides every page
from `_storyMusicStartsOn` (`OnArrestStarted`, raised by the arrest) until `_storyMusicEndsOn`
(`OnPaintingReturned`, page 5's evidence), latched: the tension track runs through the escape without
restarting, pages 1-3 included, and page 5 goes back to Bohren when the case closes. LevelManager también expone
un sistema de cheats solo-editor gateado por flags.

**Verificado**: el flujo de page-turn documentado en `paginas-y-hoja.md` sigue
coincidiendo con el código actual (PageScrollerManager → PlayerPageSpawnManager →
Hoja/EdgeBone → RidingPage) — no se encontraron discrepancias.

## Inventario

- `InventoryItem` (ScriptableObject): nombre, sprite, color de "sticker", `ResourceType`.
- `InventoryManager` (singleton): diccionarios `ResourceType → InventoryItem` y
  `InventoryItem → int` (cantidad); reacciona a `Evento.OnResourceUpdated`.
- `InventorySlot`: sprite + color + nombre localizado (tabla `ItemTable`, con fallback
  al nombre del asset si la carga async todavía no resolvió) + cantidad si es &gt;1;
  sonidos de hover/click; fade in/out para el "sticker" de recompensa nueva. Los slots
  se reacomodan sin huecos cuando se quita un ítem.
- **Worn badge (2026-10-14)**: `InventorySlot._wornBadge` (`WornBadge` child of `InventorySlot.prefab`, a small `Dialogov3` square top-right) shows on gear items only: orange-yellow while Kami wears the item, pale white when she owns it but doesn't. It refreshes on `SetItem`/`ClearSlot` and `Player.OnGearChanged`; colours are fields on the slot. The Level 1 demo has no Wardrobe tab, so this is the bag's only equipped signal.
- **A tap on a gear item also wears it** (spec 011, 2026-10-05): with the Flap fully open (`IsFullyOpen`, Q16), `BUTTON_OnPress`
  showcases the item as always and then calls `Player.TryToggleGear` (outfits and boots; see
  `spine-kami.md`). The showcase slot's own click is emptied in the prefab, so it never toggles.
- `InventoryManager.AddItem` warns and skips a resource missing from that scene's `_allItems` list
  (it used to throw). Every Kami gets her outfit at level start now, so the old test scenes with their
  own outdated lists (`Nivel1_LaRural`, `SampleScene`, `Level1_Kami (Test Use Only)`) log one warning
  instead of an exception.

## Flap UI (menú deslizable) y CamWheel (selector de cámara)

**FlapManager**: panel que entra deslizando (tecla ESC/I/U) con 5 tabs — Quests
(`QuestSlot[]`), Inventario (`InventorySlot[]`), Settings (sliders de
brillo/contraste/volumen + confirmación de salida), Controles y **Wardrobe** (spec 011, 2026-10-05,
see below). Al abrir del todo
pausa el juego (`Time.timeScale = 0`) y baja la música a 0.4x; sonido de "vuelta de
página" al abrir/cerrar. Con joystick: R1/L1 ciclan de tab y B cierra (ver
`controles-y-gamepad.md`); el botón de la "tirita" (imagen `_tiritaPull`/`_tiritaPush`
que sobresale del costado) tiene `FlapManager.BTN_ToggleFlap()` cableado a su `OnClick` y
también es navegable/apretable con A. Los 3 sliders de Settings usan un handle animado
("gallina", `Chicken Handle.controller`) que camina tanto al arrastrar con mouse como al
enfocar con joystick (mismo clip en los estados Pressed/Selected del Animator). Arriba de
la columna de íconos de sección hay un cartel (`FlapTabHint`, componente
`SoloConJoystick`) que dice "L1 / R1 para cambiar de sección", visible solo con joystick.

**States (spec 011 Phase 4, 2026-10-05)**: the Flap is always `Closed`, `Opening`, `Open` or `Closing`
(`FlapState`). Kami's input belongs to the menu from the moment it starts opening until it starts closing
(`IsMenuOpen`), a toggle mid-slide reverses it, and the menu itself (every display, tab button and the exit
confirm) takes clicks, R1/L1/B and code selections only while `Open` (`IsFullyOpen`), through `CanvasGroup`s
`FlapManager` adds in `Awake` (no prefab edit). The HUD strip is never blocked. Detail and the uGUI
stale-CanvasGroup gotcha: "Pausa del Flap" and "Fugas de foco" in `controles-y-gamepad.md`.

**Wardrobe tab** (spec 011 FR-104, 2026-10-05, placeholder layout until Valen's #151): `_flapDisplays[4]`
(appended last, so the hardcoded `BTN_*` indexes 0-3 still hold), `FlapManager.BTN_Wardrobe`, and a 5th
tab button (`FlapDisplayButtonWardrobe`, a copy of the bag tab tinted lilac) below Controls at y = -85.
**The Flap prefab has two bands**: the menu paper (local y above about -35) sits off screen while closed,
and the HUD strip hanging under it (pull tab, health, paper ammo) is always on screen. y -85 falls in the
HUD strip, so since 2026-10-05 (spec 011 task 4.A) **every tab button is inactive while the Flap is closed**
(`FlapManager.SetTabsVisible`: on in `OpenFlap`, off when the closing slide ends). Anything new that must
not show or be clicked during gameplay has to sit above the paper's bottom edge or be hidden the same way.
**The HUD leaves during cutscenes (#188, 2026-10-13)**: the prefab root has a `CanvasGroup` (`_hudGroup`, authored
in the prefab) that `FlapManager.UpdateHudVisibility` fades to 0 over `_hudFadeSeconds` (0.25, unscaled) and stops
raycasts while `LevelManager.inCutscene` is on and the Flap is `Closed`, so it follows every lock: Timeline
cutscenes, the Abuela's landing, the catapult boarding and the origami text panel. Opening the Flap shows it at
once. Alpha and raycasts only: never `SetActive`, never `interactable` (the Selectable cache, "Fugas de foco").
**The camera selector follows the same rule (#226, 2026-10-16, not played)**: `CamWheelManager.prefab`'s root has its own
`CanvasGroup` (`_hudGroup`, `_hudFadeSeconds`), faded by `CamWheelManager.Update` on `inCutscene` alone (it has no
closed/open exception: an open wheel is closed with `CloseFlap()` the moment a cutscene starts).
`WardrobeDisplay` (`Assets/Scripts/UI/`) holds one row per gear slot (Outfit, Scissors, Feet), authored in
`FlapManager.prefab` as `Row_*` = a `Label` (TMP + `LocalizeStringEvent`, UITexts `wardrobe_*`) and a
`Slots` container (GridLayoutGroup, one row). The buttons are `InventorySlot.prefab` copies spawned at
runtime, one per gear item Kami owns (the bag's own count, so confiscated scissors leave both), in
`GearCatalog` order, reused rather than destroyed. The worn item is drawn at 1.15x, the others at 0.85x
(Inspector); the selected one gets InventorySlot's amber. A tap goes through the bag's own path
(`BUTTON_OnPress`). Rebuilt on `OnEnable` (FlapManager selects the first button right after showing the
tab) and a frame after any `OnResourceUpdated` (InventoryManager may update the amounts after it).

**CamWheelManager** (implementa `IFlap`, mismo patrón de apertura/cierre que Flap):
menú radial para elegir `CameraMode` a mano. **Since #43 (2026-10-05) each `CamWheelButton`
declares its `CameraMode` (`_mode`, set per instance in `CamWheelManager.prefab`: Close Up 0, Normal
2, General 3, Book 4)** and its own `OnClick` calls `CamWheelButton.BTN_SelectCamera` ->
`CameraManager.SelectCamera(_mode)`. That field is the only link between a button and a camera: the
highlight (`Evento.OnCameraChange` -> `FakeSelectButton()`, scale only, never `Select()`) finds the
button by mode, not by its position in the wheel. A new pickable camera = a new `CameraMode` + a
button with that mode, no code. `ChangeCamera(int)` and its hand-written camera->button switch are gone
(the stale test scenes `Level1_Kami (Test Use Only)`, `Nivel1_LaRural` and `SampleScene` still
override their buttons to call it, so their wheels no longer click; they are out of the build).

**HoverDetector**: dispara `Evento.OnMouseEnterFlap`/`OnMouseExitFlap`, usado para
suprimir tooltips mientras el mouse está sobre UI.

## Cámara

`CameraManager`: array de Cinemachine VirtualCameras, una por `CameraMode`
(`CloseUp`, `OrigamiCasting`, `Normal`, `General`, `BookCenter`, `ReceiveReward`).
`SetCamera(modo)` apaga todas y prende la target; `ToggleNextCamera()` (click medio)
cicla. Durante un page-turn la secuencia es CloseUp → BookCenter (con delay) → Normal.

**Player-pickable vs game-driven (#43, 2026-10-05, played by Diego in both levels).** `CameraManager.IsPlayerSelectable` is the one
source of truth: `OrigamiCasting` and `ReceiveReward` are driven by `PlayerView` (casting / receiving
a reward) and are never the player's. Cycling (middle click / L2) goes to the next pickable mode in enum
order and skips them; a cycle press or a wheel pick (`SelectCamera`) while one of them is live is
ignored, so L2 can't pull the camera off a fold or a reward. `BookCenter` stays pickable. Every
`SetCamera` (game code included: page turns, the dam, origami, rewards) now raises
`Evento.OnCameraChange` with the live `CameraMode` (it used to carry an int, only from cycling, and the
wheel read it as a button index: L2 onto Normal lit General). Its only listener is `CamWheelManager`.
The click sound stays on player picks only.
`SplashCamaraController` es un sistema aparte y más simple (dos cámaras que alternan
solas cada 5s) solo para la pantalla de splash/intro.
