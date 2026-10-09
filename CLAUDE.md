# Kami: Papel y Tijera

Juego de Unity (URP + Steam): personajes 2D animados con **Spine** (runtime spine-unity **4.2** vendored in `Assets/Spine`; these docs said 3.8 until 2026-10-02) sobre mundo 3D.

## LANGUAGE RULE (2026-09-09, set by Diego — overrides everything below)

**Everything you ADD to this project must be in English**: code, comments, script names,
prefabs, asset names, new docs. **Also always reply to Diego in English**, even when he writes
in Spanish.

This repo's existing code and docs are in Spanish — that is history, not a convention to
follow. Do not "match the surrounding language" for new work. Existing Spanish stays as-is
unless Diego asks for a translation.

## Dónde está todo

- `Assets/Scripts/Player/` — Kami, en MVC casero (ver abajo)
- `Assets/Scripts/PUBMechanics/` — solapas del libro pop-up (Solapa, TriggerSolapa)
- `Assets/Scripts/Cortables/` — todo lo que la tijera corta (`ICortable`) + `TijeraHitbox`
- `Assets/Scripts/Managers/` — AudioManager, LevelManager, EventManager, PageScrollerManager, PlayerPageSpawnManager, CameraManager, etc. (singletons)
- `Assets/Scripts/TriggerS/` — triggers de zona (base `TriggerScript`, con tooltip por color)
- `Assets/Scripts/Origami/` — minijuego de origami + `PedestalCanvasDisplay` (costo en pedestal)
- `Assets/Scripts/Input/` — `InputHub` (fachada única de input), `InteractionContext` (el botón B contextual), `GamepadCursor`, `InputPromptSystem`
- `Assets/Scripts/UI/` — `TooltipManager`/`PostIt` (tutorial), `FlapManager`/`CamWheelManager` (menú y selector de cámara), `InventorySlot`, `WardrobeDisplay` (the Flap's Wardrobe tab, spec 011); `LocalizedText` (embudo ÚNICO de todo el texto localizado), y sobre él `ResaltadorDeConceptos` (palabras clave con color) + `IconosDeBoton`/`AnimadorDeIconos` (íconos animados de input) — ver `specs/005-textos-resaltados-e-iconos/spec.md`
- `Assets/Scripts/Inventory/` — `InventoryManager`/`InventoryItem` (recursos recolectables)
- `Assets/Scripts/Gear/` — Kami Gear (spec 011): `GearSlot`, `GearItem` (+ the five `Gear_*.asset`), `GearLoadout` (+ `Level1_StartLoadout`/`Level2_StartLoadout`), `PlayerGear` (owned by Player), `SpineSkinComposer`; the catalog is `Assets/Resources/GearCatalog.asset` (items + owned Spine slots per gear slot + which gear slots the player changes from the bag)
- `Assets/Scripts/Level2/` — Level 2 story glue: `FindCluesTracker` (page 2 clues → cops → quest handoff), `CrimeSceneGate` (guarded police tape), `ArrestCutscene` (page 3's arrest, answers the timeline's signals), and page 4's escape: `PoliceStationPage` (owns and restarts the page sequence), `ConfiscatedGear` + `ConfiscatedGearPickup`, `AbuelaEntrance`, `EscapeWindow`, and page 5's ending: `MuseumPage` (Grace, the ticket unfold, closing the case) and `Catapult` (boarding + the launch timeline's signals); also `PageRideFollowers` (followers ride page turns) and `PageExitLock` (next-page sphere locked until the page's task is done). See `nivel2-y-ui.md`
- Level 2 Phase 6 (2026-09-29): `Level2/PaperPlaneRide` (followers ride the plane hat on page 4), `Cortables/PoliceOfficerCortable` (patrol cops can be cut: knocked out 12s), page 4/5 intro and arrival Timelines (see `cutscenes.md`)
- `Assets/Scripts/Cutscenes/` + `Assets/Timelines/` — generic Timeline cutscenes: `CutsceneDirector` (locks Kami via `LevelManager.inCutscene`, binds Cinemachine tracks in code), `CutsceneDialogueMarker` (the timeline waits for a dialogue), `HiddenRenderers`. See `cutscenes.md`
- `Assets/Prefabs/Interactables/TrashCan.prefab` — the openable trash can used everywhere in Level 2 (flap flow, 2 paper once); `TrashCan_ClueGlove` is its clue variant. Meant to be reused for sewer manholes
- `Assets/Prefabs/Level2/` — page-specific Level 2 prefabs (clue pickups, crime-scene gate, clue tracker, gift box with ribbon + Pelusa + letter pedestal, `ArrestCutscene`, page 4's `CellFence` and `ConfiscatedGearPickup`). Also `Prefabs/NPCs/PoliceOfficer.prefab`, `Prefabs/NPCs/Abuela_Follower.prefab` (Level 2 variant of the Abuela), `Prefabs/NPCs/Grace.prefab` (page 5's museum director, placeholder) and `Prefabs/Cortables/CuttableDrapes.prefab`. Page 5's catapult is built straight in the scene (`Page 5/Museum`), not a prefab
- `Assets/Scripts/Quests/` — `QuestManager`/`QuestEffector` + un ScriptableObject `QuestNN_Nombre.asset` por quest
- `Assets/Scripts/Dialogos/` — un `*DialogueTrigger.cs` por NPC + estados de NPC (`NPC_Abuela`, `NPC_Florista`, etc.)
- `Assets/Scripts/NPCs/Animation/` (spec 014) — `SpineCharacter` (any Spine NPC: idle/walk/run from its measured speed, facing, named poses, a rest pose/mood, track-1 overlays, idle break, actor id), `AnimationCue` (beats addressed by actor id, fired from a `DialogueSO` line's `animationCues` or its `cuesOnEnd`) and `SpineBoneAim` (Ariel's arm points at Kami). Level 2's Natalia, Ariel and cops (`Assets/2D/Spine/Natalia|Ariel|Policia`, skins `Policia 1-4`) run on it; how to tune: `specs/014-level2-npc-animations/spec.md` "As built"
- `Assets/Scripts/Enemies/` — Rocoso (FSM + física, ver `enemigos-e-ia.md`), `EnemySpawner` (armado, sin usar todavía)
- `Assets/Scripts/AI/` — `PatrollingAgent` (base NavMesh, agosto 2026) + `GallinaAgent` + `PoliceOfficer` (vision cone + LoS, spec 002) with `VisionConeView`; `PageNavMeshManager` swapea el NavMesh activo por página (issue #21); ver `enemigos-e-ia.md`
- `Assets/Scripts/Barquito/` — NPC bote con A* propio por nodos (no NavMesh)
- `Assets/Scripts/AttackHitBoxes/` — `IGolpeable` + hitboxes de ataque de enemigos
- `Assets/Scripts/Particles/ParticleShooter.cs` — partículas del player por índice
- `Assets/Prefabs/Kami/Kami.prefab` — el player completo
- `Assets/Prefabs/OrigamiRoutes/` — sellos, `PedestalParent.prefab` y rutas de origami
- `Assets/Prefabs/UI/PostIt.prefab` — los post-its de escena son instancias de este
- `Assets/Prefabs/Particulas/` — prefabs de partículas
- `Assets/2D/Kami Spine/Atlas 13/skeleton.json` — the ACTIVE skeleton for gameplay Kami (`Kami.prefab`, Level 1 and Level 2) since 2026-10-06: skins `FullSkins/Kami Libro 1|Diario`, `Tijeras/*`, `Zapatos/*` and an empty `default`, slots named `KamiLibro/...`; its `default` is emptied by `tools/move-default-skin-into-diario.py` (re-run after a re-export). Details in `docs/claude/spine-kami.md`. It has 19 physics constraints (hair buns, bows, cape, skirt). MainMenu's title Kami (`kami_spine_titlescreen`) is on Atlas 13 too and is dressed by `Gear/LoadoutSkin` from `Level1_StartLoadout` (Libro 1 + base shoes, no scissors; spec 012).
- `Assets/2D/Kami Spine/Atlas 12 Spine4.2/skeleton.json` — the previous skeleton (Spine 4.2.43), referenced by nothing now (the menu's dead `The Paper Model` was deleted 2026-10-08); it was `Kami.prefab`'s and both `MainMenu.unity` skeletons since 2026-10-05 (spec 011 F2/#140: they pointed at the 3.8 `Atlas 11`, which the 4.2 runtime can't load); the level scenes' own overrides to Atlas 12 are now redundant. Atlas 1-11 are old
- Escena de trabajo Nivel 1: `Nivel1_KamiPapelTijera.unity` (activa desde fines de agosto 2026 — `Nivel1_LaRural SpineTest.unity` quedó vieja/stale, no confundir; puede tener referencias rotas)
- Escena de trabajo Nivel 2: `Level2_Newspaper.unity` — ver `nivel2-y-ui.md` para estado actual (las 5 páginas armadas, pendientes de jugar/arte)
- Level 2 closing cutscene: `Level2_EndCutscene.unity` (placeholder copy of `Nivel1_EndCutscene`, reached from the page 5 catapult; `GameScene.Level2EndCutscene`)

Active work branches:
- `feature/spine-animations` — Spine character animations (Spanish code/comments)
- `feature/joystick-controls` (issue #41) — gamepad support with animated input prompts (A=jump, B=attack/interact, L1=sprint, L2=camera), target M4 (Oct 15). See `specs/004-joystick-controls/spec.md` (English).
- `feature/textos-resaltados-e-iconos` — palabras clave resaltadas con código de colores didáctico + íconos animados de input dentro del texto. Sale de `feature/joystick-controls`. Ver `specs/005-textos-resaltados-e-iconos/spec.md` (español).
- `pages-blocking` — blocking for Level 2's 5 pages (`Level2_Newspaper.unity`), in progress. Branches from `Level2_Newspaper`.
- `006-nivel2-detective-natalia` (design work started 2026-09-22) — Level 2's new mechanics: Natalia's detective story across the 5 pages (companion NPC, 3 chained quests, ambient traffic, a police vision cone, new Origami routes, a catapult level ending). Branches from `pages-blocking` (carries its in-progress blocking). See `specs/006-nivel2-detective-natalia/spec.md` and `tasks.md` for the phased breakdown meant for parallel work.
- `007-jazz-bar-minigame` (designed 2026-09-28, not started) — Level 2 sidequest 2: the page 3 bar's free-play jazz jam (every key plays a sax note), won saxophone usable from the inventory (sax mode). See `specs/007-jazz-bar-minigame/spec.md` and `tasks.md`, epic #131.

- `012-page-turn-followers` (built 2026-10-08, played once by Diego, NavMesh bake of the landing points still being fixed) - Level 2: followers ride the page turn with Kami (`PageRideFollowers`), skeleton physics ignore teleports, main-menu Kami dressed by `LoadoutSkin`. Also landed in the same session: Natalia's café fold driven by her conversation, `PageExitLock`, and the arrest escort led by the cops. `specs/012-page-turn-followers/`. Epic #156.
- `014-level2-npc-animations` (built 2026-10-09, not playtested) - Level 2: Natalia, Ariel and the cops on their Spine animations in gameplay and the three cutscenes, Natalia (and the Abuela) follow to a spot beside Kami at her pace, a second page 4 guard, page 2/3/5 cop placeholders replaced by the `CopEscort` variants. `specs/014-level2-npc-animations/`.
- `013-arrest-escort` (built 2026-10-08, not playtested, #167) - Level 2 page 3: the cops walk the NavMesh to the patrol car and march Kami in front of them, at a slower pace. `specs/013-arrest-escort/spec.md`.

Roadmap and future feature specs: `specs/` (Spec Kit) and `ROADMAP.md`.

## Herramientas de análisis del proyecto

- **graphify** (`graphify-out/`): grafo de conocimiento del código en `Assets/Scripts` —
  `graphify-out/graph.html` (interactivo), `graphify-out/GRAPH_REPORT.md` (god nodes,
  comunidades, conexiones sorprendentes). Regenerar con `/graphify Assets/Scripts` tras
  cambios grandes de arquitectura; `--update` para incremental. Run it from the repo root: the
  manifest lives in the root `graphify-out/`, and `Assets/Scripts/graphify-out/` is a mirror kept
  in sync by copying (plus the AST cache). The corpus is code-only (AST, no LLM cost), so if
  `--update` flags every file as changed (old manifest keys), a full rebuild costs the same.
- **Spec Kit** (`.specify/`): flujo spec-driven para features medianas/grandes —
  `/speckit-specify` → `/speckit-plan` → `/speckit-tasks` → `/speckit-implement`.
  Constitución del proyecto en `.specify/memory/constitution.md`. Specs existentes en
  `specs/`.

- **Community Discord** (`tools/discord-setup/`, spec 010): script that builds the "Kami Paper Scissors" server (roles, ES/EN channels, AutoMod, pinned rules), designed for an audience that includes minors. Run on 2026-10-01 against the real server: 51 steps ok, 0 failed, idempotent, with language gating (Onboarding question -> Español/English roles unlock their category) and enforced channel order. Docs: `README.md` there, moderation in `docs/discord/moderation.md`, design in `specs/010-discord-community/spec.md`. Still open: Diego's by-hand checklist, the new-member test, the legal paragraph, soft launch.

### Modus operandi: documentación viva (NO es un issue, es una regla)

Mantener el grafo, las specs y los docs al día es **parte de terminar cualquier tarea**,
no un trabajo aparte que se agenda. Al cerrar un pedazo de trabajo:

1. **Docs primero**: si el cambio toca algo descrito en `CLAUDE.md` o `docs/claude/*.md`,
   se actualiza ese archivo **en el mismo commit**. Doc desactualizada = trabajo sin terminar.
2. **Specs**: si la feature tiene spec en `specs/`, marcar ahí lo implementado y las
   decisiones que cambiaron respecto al draft (con el porqué). La spec es la memoria del
   diseño, no un documento congelado.
3. **Grafo**: tras cambios de arquitectura (clases/managers nuevos, borrados, o
   dependencias nuevas entre sistemas), regenerar con `/graphify Assets/Scripts --update`.
   No hace falta por un bugfix de una línea.
4. **Código muerto**: si algo quedó sin callers, se borra en el momento — no se deja
   "por si acaso" (ver el caso `EnemySpawner`, issue #23).

## Arquitectura del Player (MVC casero)

`Player.cs` es la fachada: stats, componentes, y `CurrentState` (única fuente de verdad).
Construye y coordina a los otros tres — ellos hablan con Player, nunca entre sí:

- **PlayerModel** — piensa (lógica de estados, física)
- **PlayerView** — reacciona: animaciones de Spine, sonidos y partículas. Escucha `OnStateChanged` y los eventos de las anims de Spine (`HandleAttack`, `HandleFootstep`)
- **PlayerController** — recibe input

## Sistema de Equipamiento (Tijeras) — Agosto 2026

**Status**: ✅ Implementado. Since 2026-10-05 what Kami *looks* like is Kami Gear (below); this
section is the gameplay half.

Kami tiene dos tipos de tijera: `TijeraEquipment.Normal` / `Mejorada` (gameplay only). Their look is
the `Gear_ScissorsNormal` / `Gear_ScissorsUpgrade1` items (Spine skins `Tijera_Normal` /
`Tijera_Upgrade_1`).

**Código**:
- `Player.cs:SetTijeraEquipment()` — lógica de daño en TijeraManager + hitbox (it no longer touches the skin)
- `Player.currentTijera` — enum que trackea equipo actual
- Al completar quest del chino, `LevelManager` llama `Player.GetTijeraMejorada()` que usa `SetTijeraEquipment()`
- `Player.LoseTijera()` (added 2026-09-22, spec 006 task 0.E) — the symmetrical counterpart to `GetTijera()`: sets `hasTijera = false`, decrements `ResourceType.tijera` by 1, and refreshes `PlayerView` the same way `GetTijera()` does. Guarded against double-calling when already unequipped. Called by `ConfiscatedGear` (Level 2 page 4, see `nivel2-y-ui.md`); never drives the `tijera` count below 0 (the P cheat equips the upgraded pair without one).
- `Player._startWithTijera` (2026-09-27): the level starts with Kami holding the normal scissors, no pickup and no reward pose. On in Level 2 (Diego: Level 2 is played with the normal scissors only, there is no upgraded pair to get). Since spec 011 Phase 3 also no sticker: granted with `ownedAtLevelStart`.

**Kami Gear, spec 011** (`specs/011-kami-gear/`; Phases 2 and 3 built and played 2026-10-05; Phase 4,
"a Flap that only listens while it's open" (#152-#154: `FlapState`, menu input/clicks only while `Open`),
built and played 2026-10-05; no gear persistence
between levels, Diego 2026-10-05): Kami's Spine skin is composed at runtime from outfit +
Scissors + Feet (+ Hat later), so parts stack and survive an outfit change. Getting an item equips it
(one `OnResourceUpdated` hook in `Player`); each level starts from a `GearLoadout`
(`Player._startingLoadout`: Level 1 default outfit and no scissors, Level 2 detective + normal scissors,
and it owns the default outfit too). The outfits are bag items. Tapping (or A on) an outfit or the rain
boots in the bag or the Flap's **Wardrobe** tab puts it on or takes it off (`Player.TryToggleGear`; the
scissors always follow the newest pair). Water protection follows what she wears (`hasWaterBoots` is
gone); scissors damage and sprint stay owned-based. Adding an item = one `GearItem` asset + one Spine
skin + its bag item, no code. Detail: "Skins: Kami Gear" in `docs/claude/spine-kami.md`.

## Muerte con causa (río, rocoso) — Agosto 2026

La causa de muerte (`DeathCause`: Generic/Drowning/Rocoso/**Caught**/**Car**) decide la anim, el texto del overlay y el respawn:

1. **Río**: `Rio.cs` espera `activationDelay` (tuneable) con el IMojable adentro — si sale antes, cancela y no pasa nada. Cumplido el delay llama `GetWet()`: para Kami eso es feedback (anim mojarse + sonido) y `Die(DeathCause.Drowning)` de una; otros IMojable siguen con damage normal. Rio NO conoce a Player, trata todo por IMojable.
2. **Rocoso**: `GetGolpeado()` → `TakeDamage(dmg, DeathCause.Rocoso)` (overload que enhebra la causa hasta `Die`).
3. **Anim**: `PlayerView.SetDeathAnimation(cause)` elige "Drowning" o "Death".
4. **Overlay**: `Player.DeathSequence` resuelve la posición de respawn (dueño de la política) y llama `ShowDefeatOverlay(cause, respawnOverride)`. `DefeatOverlay` (hereda `Overlay`) muestra la causa localizada — keys en tabla `UITexts`: `DefeatDrowning`, `DefeatRocoso`, `DefeatGeneric`, **`DefeatCaught`** (added 2026-09-22, spec 006 task 0.F, for Level 2's police-station capture — see `specs/006-nivel2-detective-natalia/`). `PlayerView.SetDeathAnimation` routes `Caught` to the generic `Death` anim (same as `Generic`/`Rocoso`) — no dedicated animation, just the new overlay text.
5. **Respawn** al cerrar con E: drowning usa `Player.drowningRespawnMode` (`LastSafePosition` = snapshot generoso con doble buffer en PlayerModel, antigüedad 1-2× `safeSnapshotInterval`; o `LevelSpawnPoint` = entrada de página). Las demás muertes siempre respawn común (`lastUsedSpawn` del `PlayerPageSpawnManager`). `Caught` (Level 2 page 4) respawns in Kami's cell because the arrest's forced page turn made the cell her `lastUsedSpawn`, and that respawn is what triggers `PoliceStationPage`'s full page restart.

**Defeat overlay setup, per level (corrected 2026-10-05: the note here had the two levels backwards)**: Level 1 gets it from `Main Canvas.prefab` -> nested `Overlays Parent.prefab`, which nests `DefeatOverlay.prefab`, removes its base `Overlay` and adds a `DefeatOverlay` whose `causeText` is the overlay's own "Moriste Text". Level 2 instances `DefeatOverlay.prefab` directly, whose root was a plain `Overlay` (no cause text, one warning per death); since 2026-10-05 that root is a `DefeatOverlay` writing into the same "Moriste Text" (#20). `DeathCause.Car` (2026-10-05) is what `TrafficCarHitbox` passes (key `DefeatCar`). Keys live in `UITexts`: `DefeatDrowning`, `DefeatRocoso`, `DefeatGeneric`, `DefeatCaught`, `DefeatCar`.

## Convenciones de código

- **New code/comments in ENGLISH** (see the language rule at the top of this file). The Spanish comments below/around are pre-2026-09-09 code and stay as they are.
- Comments explain the *why*, not the *what*
- Llaves siempre, incluso en una línea; `switch` con `break` explícito
- `[SerializeField]` privado + `[Tooltip]` para todo valor tuneable (tooltips nuevos en inglés, ver la regla de idioma arriba)
- **Los valores de diseño (colores, listas de palabras, tuning) van en un asset editable en el inspector**, no hardcodeados en el script — ver `TextHighlightSettings` como referencia del patrón: se carga solo por `Resources.Load`, no hace falta cablear nada, y tiene fallback si el asset falta
- `Debug.Log($"[NombreClase] ...")` en los puntos de decisión
- Guard clauses con `Debug.LogWarning` en referencias que pueden faltar
- **NO corregir los typos del skeleton** (`NoScissortsOverride`, `tiejraBack`): son del asset, no del código

## Detalle fino (leer según la tarea)

- Spine/skeleton/animaciones de Kami: @docs/claude/spine-kami.md
- Audio y partículas: @docs/claude/audio-y-particulas.md
- Canvas de costo de origami y tooltips/post-its: @docs/claude/origami-y-tooltips.md
- Paso de página (HojaMaster es Mecanim, no Spine), PositionMarker y el enganche de Kami al borde (RidingPage): @docs/claude/paginas-y-hoja.md
- Cutscenes con Timeline (el arresto de la página 3, cómo tunearlo): @docs/claude/cutscenes.md
- Enemigos e IA (Rocoso, PatrollingAgent/GallinaAgent, Barquito, patrón de hitboxes): @docs/claude/enemigos-e-ia.md
- Nivel 2 (estado actual), LevelManager, Inventario, Flap/CamWheel UI, Cámara: @docs/claude/nivel2-y-ui.md
- Quests y diálogos de NPCs: @docs/claude/quests-y-dialogos.md
- Controles (teclado + joystick), botón B contextual, cursor virtual del origami: @docs/claude/controles-y-gamepad.md

## Ojo al editar

- El editor de Unity suele estar ABIERTO mientras trabajamos: al crear un asset, preferir el `.meta` que Unity autogenera. Pero si Unity no tiene foco puede tardar MUCHO en generarlo: es válido crear el `.meta` a mano con un GUID random, verificado sin colisiones por grep (Unity lo adopta al refrescar).
- Cirugía YAML de prefabs: leer el archivo entero antes y copiar patrones existentes. Referencias a componentes de prefabs anidados = bloques MonoBehaviour *stripped* (patrón copiable en `OrigamiRoute 1-Easy.prefab`). El fileID que una escena usa para un target dentro de una instancia anidada se computa `(source XOR prefabInstance) & 0x7FFFFFFFFFFFFFFF`.
- `NavMeshPath` (and anything that calls into the engine) can't be built in a field initializer: Unity throws on the MonoBehaviour constructor. Create it in `Awake`.
- Line endings mixtos: algunos prefabs son CRLF y otros LF — preservar el del archivo al editar.
- **Stripped blocks must use the XOR id too** (learned 2026-09-24 building Level 2 prefabs by
  hand): inside a prefab or variant, the `stripped` block for an object of a nested instance has
  fileID `(source XOR prefabInstance) & 0x7FFFFFFFFFFFFFFF` — not a free random id. A variant's
  own root GO/Transform are addressed the same way. An object two levels deep (e.g. the fbx
  renderer inside `TrashCan.prefab` inside `TrashCan_ClueGlove`) is XORed once per level.
- **No live Editor control from here**: the Unity CLI's `unity command` needs the Pipeline
  package, which requires Unity 6 — this project is 2021.3. Prefab/scene work is YAML surgery.
- Verificación post-cirugía: contar bloques `--- !u!` antes/después + grep de unicidad de fileIDs.
- **Sí se puede compilar desde acá**: `python tools/compile-check.py [tag]` compila
  `Assembly-CSharp` con el Roslyn que trae Unity, reusando el response file real que el
  editor dejó en `Library/Bee/artifacts/` (mismos defines, mismas referencias), en ~20s y
  sin abrir Unity. Warnings de baseline conocidos: `JumpFloodOutlineRenderer` CS0162 y
  `HongueroTiburcioDialogueTrigger` CS0414. **Ojo**: verifica compilación, NO runtime — el
  feel, la física y el input real los sigue validando Diego en el editor.
- **Borrar código es el caso donde compile-check casi no sirve** (lección del 2026-09-24):
  un MonoBehaviour sin `Update()`/`Start()`/`Awake()` compila perfecto, porque Unity los
  llama por reflexión. Al eliminar un bloque, listar ANTES exactamente qué cae dentro del
  rango que se borra, y DESPUÉS re-listar los métodos que quedaron en el archivo. Un
  `Update()` borrado sin querer en `NPC.cs` dejó a Natalia sin tickear su FSM: compilaba
  limpio y no seguía más a Kami.
- `python tools/make-meta.py <ruta...>` genera los `.meta` de scripts/carpetas nuevas con
  GUID random verificado sin colisiones, sin depender de que Unity refresque.
