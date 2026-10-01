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

**Phase 2 (page 2, "Find clues") built 2026-09-24 — compiles, not yet played.** Three
clues (glove in a trash can, hat on the manhole, watch in the museum yard), two cops that
drive off after the second clue so the police tape can be cut, and the quest handoff to
`Quest06_GoBackToNataliasHouse` (closed by the page 3 arrest since Phase 3). What exists and where:

- **Every trash can in Level 2 is now `Assets/Prefabs/Interactables/TrashCan.prefab`**
  (15 of them, pages 1-5; the scene instances used to be the bare `Kami_TrashCan.fbx`).
  Opening one runs the stock `Solapa`/`TriggerSolapa` flap flow (Kami's `PullSolapas`
  gesture) and gives 2 paper once. `TrashCan_ClueGlove.prefab` is a variant that gives the
  glove (page 2's `TrashCan_ClueGlove`). The contents are a `GrantResourcePickup`
  (`Assets/Scripts/TriggerS/Pickups/`) in Revealed mode: opening = collecting, and it
  destroys itself, so later opens find the can empty. The per-can "Newspaper 1" material
  override the old instances had was carried over. The model sits under a `Wobble` pivot at
  scale 0.012876 so the placeholder open/close squash (`Assets/Animations/TrashCan/`) has
  something scale-1 to animate; Valentino's lid animation replaces those two clips.
- `Assets/Prefabs/Level2/`: `CrimeSceneGate` (tape + blocker + cops + car,
  `Assets/Scripts/Level2/CrimeSceneGate.cs`), `CluePickup_Hat`/`CluePickup_Watch` (walk-into
  pickups), `FindCluesTracker` (`Assets/Scripts/Level2/FindCluesTracker.cs`: counts clues,
  queues Natalia's comments, calls off the cops, fires `OnAllCluesFound` once, hands off the
  quest) and `GiftBox` (`GiftDialogueTrigger`, extended in Phase 3 — see above).
- **Known gap: the museum fence has no colliders.** `KamiMuseo.fbx` imports with
  `addColliders: 0` and the scene adds none, so the gate's blocker only blocks the gap itself
  — Kami can walk around it. Also positions of the gate, the gift and the hat are
  placeholders (`_PLACEHOLDER_POSITION` in the names), to be dragged into place in the Editor.
- Page 5 is page 2 after the clues (Diego): none of the page 2 gameplay objects live there.

**Phase 3 (page 3, "The trap") built 2026-09-25 — compiles, not yet played.** Chain: talk to
the gift ("what's this? let's cut it open!") → cut its ribbon (any time, talked to or not) →
the box opens, the Pelusa pops into the inventory, Natalia reacts → the letter origami opens by
itself → reading it and closing the text starts the arrest cutscene → the page turns to page 4
with Kami and Natalia in separate cells → Quest06 closes and `Quest07_EscapeAndReturnThePainting`
starts. Where things are:

- `GiftBox.prefab` now holds everything: the nested `CuttableRibbon`, closed/open placeholder
  cubes, `PelusaPickup` (a `GrantResourcePickup` in Revealed mode, `pelusaPainting`) and a
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

**Phase 4 (page 4, "Escape from the police station") built 2026-09-27 — compiles, not yet played.**
`PoliceStationPage` (`Assets/Scripts/Level2/`, on `Page 4/PoliceStation`) owns the whole page
sequence and is the only thing that restarts it:

1. The arrest's `WrapUp` calls `PoliceStationPage.BeginFromArrest()`. Kami (west cell room, whose
   east wall is now a `CellFence`) and Natalia (a new fenced cell in the front-left room, AROUND the
   paper-plane pedestal) are locked up, and `ConfiscatedGear` takes Kami's scissors, ALL her paper
   and the Pelusa.
2. The narrator's line (`Narrator_JailIntro`, `_introLine`, first time only) plays half a second
   after the girls land — the dialogue itself keeps Kami still. `_abuelaDelaySeconds` (3) after it
   closes (Kami can walk around her closed cell meanwhile) `AbuelaEntrance`
   drops the Abuela (`Abuela_Follower` prefab variant) next to the fence: Kami is locked only for the
   fall and her two lines (`Abuela_JailLanding`), the fence disappears (reveal, not destruction), she
   follows Kami.
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
5. **Getting caught restarts the whole page** (Diego): on the respawn after a `DeathCause.Caught`,
   everything above goes back to step 1, the Abuela falls again after only
   `_abuelaDelayAfterCaptureSeconds` (3), the cops return to their posts. The stash only grows:
   paper Kami gathered before being caught again is returned at the pickup too; the plane hat is lost.

The cop is `PoliceOfficer.prefab` walking the four corners of Diego's "Poli Path" rectangle
(`PoliceStation/PatrolRoute`); his blocking placeholder "Poli" (with the reference cone) is inactive.
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

**Phase 5 (page 5, "Clearing their name") built 2026-09-28 — compiles, not yet played.**
`MuseumPage` (`Assets/Scripts/Level2/`, on `Page 5/Museum`) owns the page:

1. Talking to **Grace** (the museum director, `Prefabs/NPCs/Grace.prefab`: a tinted Natalia sprite +
   `GraceDialogueTrigger`, whose lines `MuseumPage` picks) needs the Pelusa, the café ticket and the
   three clues. Her meeting dialogue ends -> the ticket pedestal (`SelloOrigami CafeTicket`, a
   `SelloOrigami Cafe` instance pointed at `OrigamiRoute_Cafe_Unfold`, 0 paper, auto-prompt) appears.
   **Ariel and a cop are there** (tinted placeholders, `Ariel_PLACEHOLDER_POSITION` /
   `CopWitness_PLACEHOLDER_POSITION`): they chased the escapees from page 4. The meeting is where
   Grace introduces herself, and Natalia introduces herself and Kami.
2. The unfold shows the ticket (the girls' receipt from page 1: 2 hot chocolates, paid at 21:47) on
   the letter's `OrigamiTextRevealPanel`; closing it plays `Museum_Evidence`: both girls were at the
   café at 21:47 and heard of the robbery on its radio, Ariel's hat + glove, the watch stopped at
   21:47, Grace pointing out Ariel's access through his father, the police apology and arrest, Ariel's apology to Natalia, Grace's thanks, and **Grace's catapult offer in
   the same dialogue** (Diego, 2026-09-28: the player talks to Grace once).
3. When that ends: the five items leave the inventory (handed over), `OnPaintingReturned` completes
   Quest08 (removed a frame later), Natalia and the Abuela stop following where they stand,
   Natalia becomes talkable with her farewell (`NataliaDialogueTrigger.StayBehind`), and the
   **catapult** (`Catapult`, same object) unlocks.
4. Interact on the catapult = confirm -> short fade (`Canvas/ScreenFade`, `ScreenFader`), Kami lands in the
   bucket (walled in by `BucketWalls`, she can still attack), the Abuela next to her, her line, a
   "cut the rope" post-it. Only now is the rope's trigger on. Cutting it plays `Timeline_Catapult`
   (see `cutscenes.md`): launch, hold on the empty catapult, fade, `Level2_EndCutscene`.

The rope is the old loose root `CuttableRope` instance, moved under the catapult. Its 21-unit trigger
sits 3.5 to Kami's right at seat height: her scissor hitbox (3.16 +- 4.27 ahead of her body center,
1.15 +- 2.32 above it, +-1.74 deep) reaches it from anywhere in the 5x5 bucket, facing right.
Everything is placeholder cubes/sprites and `_PLACEHOLDER_POSITION` objects (Grace, Ariel, the cop
witness, the pedestal, the catapult). Starting the scene with `startingPage = 5` gives Kami the five items,
Quest08 and both followers.

`Level2_EndCutscene.unity` (`GameScene.Level2EndCutscene`, catalog + Build Settings) is a copy of
`Nivel1_EndCutscene` running `Narrator_Level2Ending` (the flight, the Narrator's blue lamp, a third
book) with empty portraits, then MainMenu: Valentino swaps the frames in the DialogueSO's sprites.

Also removed in Phase 5 (Diego's call): the loose root `CuttablePoliceTape` and `CuttablePoster`
(Phase 0 leftovers; page 2's tape lives in `CrimeSceneGate`), the unreferenced Level 1
`ParticulasSplash`, the inactive `Plane-VertexPaintTest` and page 5's inactive `NPC_Florista`.

**Phase 6 (presentation polish, 2026-09-29) built - compiles, NOT played.** 6.A: Level 2's
`OverlayManager.mainQuestTriggeringDialogue` now points at `Natalia_AfterTicket` and the overlay text
uses the new UITexts key `overlay_mainquest_level2` (scene override on the nested LocalizeStringEvent).
6.B: `Timeline_PoliceIntro` (camera tour, cop taunt, Natalia reply, narrator) played by
`PoliceStationPage` first time only via `_introCutscene`; cameras are static vcams under
`PoliceStation/IntroCameras` (drag them). 6.C: `Natalia_Freed` on the padlock cut (first time only) and
`PaperPlaneRide` (followers board on the hat fold, leave on the new `Evento.OnPaperPlaneHatLost`; parked
if Kami lands off-NavMesh). 6.D: `Timeline_MuseumArrival` + `MuseumPage.CUE_Sirens/CarArrives/WalkUp`
(car enters from the left of the page along the street lane). Patrolling cops are cuttable
(`PoliceOfficerCortable`): knocked out 12s, then resume. `PageAmbience` is placed in the scene.

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

**Pendiente conocido**: feedback visual de las botas de agua (TODO en
`LevelManager.cs`, línea ~121) — falta el skin de Spine correspondiente.

## LevelManager

Singleton dueño de los recursos del nivel: `Dictionary<ResourceType, int>` con 8 tipos
(hongos, flores, papel, botasAgua, botasRapidas, tijera, tijeraMejorada, abuela).
Guarda la referencia a `Player`, y los flags de control `agency` / `inDialogue` que
gatean input y diálogo (ver `paginas-y-hoja.md` para el uso de `inDialogue` durante el
page-turn). `AddResource()` dispara `Evento.OnResourceUpdated`, consumido por
`InventoryManager` y `QuestSlot`. Los métodos `GiveSprintBoots()`/`GiveWaterBoots()`/
`GiveTijeraMejorada()` son las recompensas de quest — ver `SetTijeraEquipment()` en
`spine-kami.md` para el caso tijera. También decide qué música de nivel usar
(MemoFloraMainLoop en Nivel 1, BohrenDestroyingAngels en Nivel 2). **Level 2 music is per
page since 2026-09-24**: `PageMusicManager` (`Assets/Scripts/Managers/`, instance of
`Prefabs/Level2/PageMusicManager.prefab` at the scene root) holds one AudioId per page in the
Inspector — Bohren on pages 1-2, silence on 3, **page 4 empty until its own track exists**
(just type the id there), Bohren again on 5. It re-applies on `OnNewPageOpen` and one frame
after start, so it wins over `LevelManager.Start` on a test started from page 3 y expone un sistema
de cheats solo-editor gateado por flags.

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

## Flap UI (menú deslizable) y CamWheel (selector de cámara)

**FlapManager**: panel que entra deslizando (tecla ESC/I/U) con 4 tabs — Quests
(`QuestSlot[]`), Inventario (`InventorySlot[]`), Settings (sliders de
brillo/contraste/volumen + confirmación de salida) y Controles. Al abrir del todo
pausa el juego (`Time.timeScale = 0`) y baja la música a 0.4x; sonido de "vuelta de
página" al abrir/cerrar. Con joystick: R1/L1 ciclan de tab y B cierra (ver
`controles-y-gamepad.md`); el botón de la "tirita" (imagen `_tiritaPull`/`_tiritaPush`
que sobresale del costado) tiene `FlapManager.BTN_ToggleFlap()` cableado a su `OnClick` y
también es navegable/apretable con A. Los 3 sliders de Settings usan un handle animado
("gallina", `Chicken Handle.controller`) que camina tanto al arrastrar con mouse como al
enfocar con joystick (mismo clip en los estados Pressed/Selected del Animator). Arriba de
la columna de íconos de sección hay un cartel (`FlapTabHint`, componente
`SoloConJoystick`) que dice "L1 / R1 para cambiar de sección", visible solo con joystick.

**CamWheelManager** (implementa `IFlap`, mismo patrón de apertura/cierre que Flap):
menú radial para elegir `CameraMode` a mano. Botones indexados por el enum de cámara;
se resincroniza solo al cambiar de cámara por otro medio (`Evento.OnCameraChange` →
`FakeSelectButton()` resalta el botón activo).

**HoverDetector**: dispara `Evento.OnMouseEnterFlap`/`OnMouseExitFlap`, usado para
suprimir tooltips mientras el mouse está sobre UI.

## Cámara

`CameraManager`: array de Cinemachine VirtualCameras, una por `CameraMode`
(`CloseUp`, `OrigamiCasting`, `Normal`, `General`, `BookCenter`, `ReceiveReward`).
`SetCamera(modo)` apaga todas y prende la target; `ToggleNextCamera()` (click medio)
cicla. Durante un page-turn la secuencia es CloseUp → BookCenter (con delay) → Normal.
`SplashCamaraController` es un sistema aparte y más simple (dos cámaras que alternan
solas cada 5s) solo para la pantalla de splash/intro.
