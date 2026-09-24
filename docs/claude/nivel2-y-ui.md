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
- **Evidence items**: 4 new `ResourceType` values (`caughtBelonging`, `brokenWatch`,
  `arielScarfCap`, `pelusaPainting`) + matching `InventoryItem` assets in
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

**Phase 1 (page 1) in progress** — `Assets/Prefabs/NPCs/Natalia.prefab` exists and is
drag-and-drop (placed in Page 1); see `docs/claude/quests-y-dialogos.md` for her dialogue/
quest wiring and the Event-quest gotcha that comes with it.

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
(MemoFloraMainLoop en Nivel 1, BohrenDestroyingAngels en Nivel 2) y expone un sistema
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
