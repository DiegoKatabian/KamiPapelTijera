# Quests y diálogos de NPCs

## Arquitectura de quests (data-driven, ScriptableObject)

`Assets/Scripts/Quests/QuestSO.cs` define una quest como asset: `questName`/
`questDescription` (keys de localización), `condition` (struct con `conditionType`:
`Resource` o `Event`; si es Resource, `resourceType` + `requiredAmount`; si es Event,
qué valor de `Evento` la completa), `rewardType` (SprintBoots/WaterBoots/
TijeraMejorada/None) y `questSprite` (retrato del NPC en la UI).

**Quests existentes** (`Assets/Scripts/Quests/QuestNN_Nombre.asset`):
1. `Quest01_Abuela` — Resource (1× recurso `abuela`, vía `OnAbuelaFold`) → TijeraMejorada
2. `Quest02_Dalia` — Resource (3× flores) → TijeraMejorada
3. `Quest03_Tiburcio` — **Event** (`OnTreeCutForChickens`) → WaterBoots
4. `Quest04_Chino` — Resource (100× papel) → SprintBoots

**QuestManager** (singleton): escucha `OnResourceUpdated`/`OnAbuelaDropoff`/
`OnTreeCutForChickens` (y en general cualquier evento configurado), cachea eventos ya
sucedidos en `eventosSucedidos`, evalúa `CheckQuests()` y dispara `OnQuestCompleted`.
`AddQuest()` la llaman los triggers de diálogo al primer interact.

**QuestSlot** (UI del Flap, ver `nivel2-y-ui.md`): progreso "actual/requerido" para
quests Resource; vacío para quests Event (no hay contador que mostrar).

**QuestEffector**: activa/desactiva GameObjects al completar (`OnQuestCompleted`) o
entregar (`OnQuestDelivered`, si `activateOnDeliver=true`) una quest — spawnea ítems,
saca obstáculos, etc.

## Sistema de diálogo (herencia de triggers)

```
TriggerScript              (base: triggerBool, OnPlayerPressedE → Interact())
  └─ TriggerDialogue        (array de DialogueSO, índice currentDialogue, _burnAfterReading)
      └─ QuestDialogueTrigger  (flujo estándar de 4 diálogos para quests Resource)
          └─ HongueroTiburcioDialogueTrigger / GranjeroNorbertoDialogueTrigger / ...
```

`DialogueSO`: array de `DialogueEvent` (text + speakerName como keys de localización,
más sprite de retrato). `DialogueManager` (singleton) resuelve las keys contra las
tablas de Unity Localization (`Assets/Localization Settings/Tables/DialogueTable_es
/_en/_pt.asset` — ojo, la carpeta se llama **"Localization Settings"**, no
"Localization" a secas), setea `LevelManager.inDialogue = true`, corre `WriteText()`
línea por línea esperando E, y dispara `OnDialogueWriteText`/`OnDialogueEnd`.

**Flujo estándar de `QuestDialogueTrigger`** (quests Resource): diálogo 0 = pedido
inicial (`QuestManager.AddQuest()`), 1 = recordatorio en loop mientras no está
completa, 2 = agradecimiento + fanfarria de recompensa (saca recursos del inventario,
da la recompensa), 3 = charla post-entrega.

## Quest de Tiburcio — la más reciente, ya estabilizada

Cambió de Resource (cortar hongos) a **Event** (`OnTreeCutForChickens`) en 3 commits
seguidos (`2d8ad53` → `f8820e4` "Arreglar bugs críticos... adversarial review" →
`350f63f`). El bug real que tuvo: la primera versión no llamaba
`QuestManager.AddQuest()` en el diálogo 0, así que la quest nunca quedaba registrada
y el evento de árbol cortado no tenía qué completar. Ya arreglado, con null-check +
log de error sobre `myQuest` si no está asignado en el inspector. Cortar el árbol
también dispara `EventManager.Trigger(Evento.OnTreeCutForChickens)`, que
`GallinaAgent` escucha para cruzar de zona (ver `enemigos-e-ia.md`).

**Fragilidad remanente (no bloqueante, anotar como tech debt)**: si por lo que sea
`OnQuestCompleted` no llega, el flag `treeWasCut` de `HongueroTiburcioDialogueTrigger`
se queda en `false` para siempre sin fallback — no hay forma de recuperarse sin
reiniciar. Bajo riesgo (el evento es confiable) pero vale un check si vuelve a
reportarse un bug ahí.

## Roster de NPCs — estado real (relevado por lectura de código, no por nombre de archivo)

| NPC | Script | Estado |
|---|---|---|
| Abuela | `AbuelaDialogueTrigger.cs` + `NPCs/NPC_Abuela.cs` | Completo — el más complejo, coordina boss fight + origami fold/unfold. Desde 2026-09-22 usa el follow genérico compartido con Natalia (ver abajo); solo conserva el estado Dropoff propio. |
| Dalia | (vía `QuestDialogueTrigger`, quest de flores) | Completo |
| Tiburcio/Honguero | `HongueroTiburcioDialogueTrigger.cs` | Completo, recién estabilizado |
| Granjero Norberto | `GranjeroNorbertoDialogueTrigger.cs` | Completo — además spawnea el pickup de tijera en el primer diálogo |
| **Chino** | `ChinoDialogueTrigger.cs` | **STUB VACÍO** — la clase existe pero no implementa nada, pese a que `Quest04_Chino.asset` ya está configurada (100× papel → SprintBoots). Quest sin NPC funcional. |
| **Florista** | `NPCs/NPC_Florista.cs` | **STUB VACÍO** — clase sin implementación, sin diálogo conectado. |
| Natalia (Nivel 2) | `NataliaDialogueTrigger.cs` + `NPCs/NPC_Natalia.cs` | Page 1 (opening dialogue + follow + quest start) implementado 2026-09-22; page 2 clue comments come from `FindCluesTracker` (2026-09-24); páginas 3-5 pendientes. Ver la sección de abajo. |

`NPC.cs` es la base común de estado (junto con `NPC_FollowPlayerState`/
`NPC_IdleState`, reusados por Abuela y pensados para reusarse por más NPCs).

## Natalia (Level 2 NPC) — page 1 implemented, pages 2-5 pending

Natalia is NOT a simple quest NPC like the 4 above — she's a companion who travels with
Kami across Level 2's 5 pages in a detective arc (a painting theft, Ariel's trap, an
arrest, a police-station escape with the Grandmother, resolution at the museum). Full
design: `specs/006-nivel2-detective-natalia/`. The old spec
(`specs/003-natalia-npc-nivel2/`) is superseded — it didn't reflect this scope.

**Shipped 2026-09-22 (page 1 only)**:
- `Assets/Scripts/NPCs/NPC_Natalia.cs` — uses the generic `NPC`/`NPC_IdleState`/
  `NPC_FollowPlayerState` FSM as-is (NOT the duplicated `NPC_Abuela` mold). Resolves
  its `Player` from `LevelManager.Instance.player` at `Start` when the Inspector field
  is empty, since a prefab can't hold a scene reference.
- `Assets/Scripts/Dialogos/NataliaDialogueTrigger.cs` — inherits `TriggerDialogue`, one
  level BELOW `QuestDialogueTrigger`: that subclass hardcodes the 4-dialogue
  request→reminder→thanks→chat mold with a resource subtraction on dialogue 2, which an
  Event quest with no item handover can't use.
- `Quest05_FindClues.asset` — Event-based, completed by `Evento.OnAllCluesFound`.
- `Assets/Prefabs/NPCs/Natalia.prefab`, placed in Page 1. Visual is a placeholder
  `SpriteRenderer` child (`NataliaVisual_PLACEHOLDER`) until Valentino's Spine art;
  swapping it touches nothing else.

### Gotcha: an Event-condition quest needs a handler in `QuestManager`, not just an enum value

`QuestManager` only ever completes an Event quest if `eventosSucedidos` says that event
happened — and that dictionary is written **exclusively** by per-event handler methods
registered in `QuestManager.Start` (`SetAbuelaDropoff`, `SetTreeCutForChickens`,
`SetAllCluesFound`...). Adding a value to `Evento` and pointing a `QuestSO` at it is NOT
enough: without its own subscribe + handler + `Unsubscribe`, the quest can never
complete, silently. Same family of bug as Tiburcio's missing `AddQuest()` above.

**Phase 2 fires `Evento.OnAllCluesFound` once ALL THREE clues (`lostGlove`,
`caughtBelonging`, `brokenWatch`) are collected** — not once per clue. That's
`FindCluesTracker` (`Assets/Scripts/Level2/`), latched. Built 2026-09-24.

### Natalia is not talkable while she follows (2026-09-24, Diego playtest)

Her `TriggerDialogue` child travels with her, so while following Kami was ALWAYS inside it:
every interact press opened her chatter ("let's look for clues") and beat whatever Kami was
aiming at — the gift box on page 3 could not be talked to. `NataliaDialogueTrigger` now calls
`SetTalkable(false)` right after she starts following (manual `OnExitBehaviour()` + collider
off; disabling a collider sends no `OnTriggerExit`). Whatever makes her stop following later
(page 5 staying behind) must call `SetTalkable(true)`.

Related: `TriggerSolapa.Interact` now ignores presses while `LevelManager.inDialogue` — the
press that advanced her glove comment was also slamming the trash can shut.

**There is exactly ONE Natalia in Level 2** (Diego, 2026-09-24): the page 1 instance. Kami
picks her up on page 1 and she follows (reparenting to each page) until she is dropped off on
page 5. The extra full instances that sat on pages 2 and 5 were deleted — each one would have
re-run her opening dialogue, added Quest05 a second time and started a second follower. Don't
place Natalia on other pages to "stage" her; page 5's drop-off (task 5.C) acts on the same one.

### Quests without a delivering NPC: Quest05 → Quest06 (2026-09-24)

`Quest05_FindClues` has no one to hand it to, and `Quest06_GoBackToNataliasHouse`
(Event, `OnGiftAtNataliasDoorReached`, handler `SetGiftAtNataliasDoorReached`) is delivered
to an object: `GiftDialogueTrigger` on the gift at Natalia's door. Two traps found building
them, both relevant to any future quest that is closed from code:

1. **Don't add or remove quests inside an `OnQuestCompleted` handler.** `QuestManager`
   raises it from INSIDE `CheckQuests`' `foreach` over its quest list, so
   `RemoveQuest`/`AddQuest` there throws "collection was modified" (and `EventManager`
   swallows it into a log, so it just silently doesn't happen). `FindCluesTracker` waits one
   frame. Related: a completed quest is re-announced on EVERY later `CheckQuests` until it is
   removed, so handlers need a latch.
2. **Don't fire `OnQuestDelivered` for a quest with no reward.** `ResourceParticleManager`
   answers it by showing the quest's `rewardRt` sticker, and `rewardRt` defaults to 0 =
   `hongos`: you'd get a mushroom. The NPC-less quests just `RemoveQuest` + play
   `QuestCompleted02`.

`DialogueManager.ShowDialogue` silently drops a request while another dialogue is open, so
anything that wants a line to be heard for sure (the clue comments, "the cops left", "let's
head home") queues it — see `FindCluesTracker.Update`. `GiftDialogueTrigger` counts the talk
from `OnDialogueWriteText` of ITS dialogue, not from `Interact`, because the same press can
open Natalia's chatter instead.

## Los NPC se mueven por NavMesh (2026-09-24)

Diego's call: Natalia, Abuela and the future cops all move on the NavMesh, like the chickens.
`NPC` used to integrate a hand-rolled `velocity` into `transform.position` (`AddForce` +
`Arrive` steering), which walked through walls and ignored the level's navigation entirely.
That whole steering path is **deleted** — `velocity`, `maxForce`, `AddForce()`, `Arrive()`,
`FollowPlayer()`, `playerOffsetDistance` and `arriveRadius` are gone.

`NPC` now owns a `NavMeshAgent` (`[RequireComponent]`, configured in `Awake`:
`updateRotation = false` because facing is `flipX`, `speed` from `_maxSpeed`,
`stoppingDistance` from the new `followStoppingDistance`). The API the states use:
- `MoveTowards(destination, force = false)` — re-paths on `repathInterval` (default 0.2s)
  instead of every frame, and validates the destination with `NavMesh.SamplePosition` first,
  the same way `PatrollingAgent` does (an off-mesh destination otherwise fails silently).
- `StopAgent()` / `HasArrived()` / `UpdateSpriteFlip()` (flip now reads `navAgent.velocity`,
  with a deadzone so an almost-parked agent doesn't flicker).

**This follows the project's existing split** (see `enemigos-e-ia.md`): waypoint patrollers
inherit `PatrollingAgent`; things that chase a *moving* target drive their own `NavMeshAgent`
(Rocoso's precedent, which deliberately does not inherit `PatrollingAgent`). An NPC following
Kami is the second kind. The **cops of page 4 are the first kind** — `PoliceOfficer :
PatrollingAgent` was already the plan in spec 006 FR-007, so they get NavMesh for free.

### The trap that cost a playtest: a centred sprite pivot puts the agent in mid-air

Natalia entered her follow state and then just stood there, with a completely clean console
(2026-09-24). The cause was geometry, not logic:

- Her sprite is 2643x6788 px at 100 PPU and its pivot is **Center**, with the visual child at
  localPosition 0 — so her transform origin, which is where the `NavMeshAgent` lives, sits at her
  **waist**, about 4 units above her feet.
- She was scaled to 0.12 in the scene, which shrank the agent's own cylinder to 0.06 units of
  radius and 0.12 units of height, with `m_BaseOffset: 0`.

A cylinder that small floating 4 units over the baked surface never attaches:
`navAgent.isOnNavMesh` stays `false` and the agent refuses **every** move order, in total silence.
`Gallina.prefab` already showed the intended compensation (`m_BaseOffset: 3.95` on a 0.371-scaled
root); nobody had done it for the NPCs.

**How Diego actually fixed it**: scaled her instance back to 1 and gave the Humanoid agent
real numbers — `m_Height: 7`, `m_BaseOffset: 4` (exactly the pivot-to-feet gap) — plus a rebake of
the pages whose NavMesh had been baked wrong. An `_autoFitToNavMesh` option that tried to measure
the gap and set `baseOffset` from code was written and **removed**: it did not work, and the
honest fix is authoring the agent correctly in the prefab. If an NPC ever stands still again,
check `m_BaseOffset` against the sprite's pivot **first**.

**What stayed from that debugging session**: `IsAgentUsable()` used to return `false` silently —
the doc claimed it degraded into "a motionless NPC plus a warning", but there was no warning
anywhere, which is precisely why this was invisible. It now says which condition failed (no agent
/ disabled / `isOnNavMesh == false`), throttled to once per `_debugInterval` because `MoveTowards`
asks every frame, and `SetDestination`'s return value is checked instead of discarded. The
`_debugMovement` flag (off by default) prints a full snapshot — position, lossyScale, effective
agent radius/height in world units, `isOnNavMesh`, path status, `remainingDistance`,
`desiredVelocity`, distance to Kami, distance to the nearest NavMesh point — on follow-start and
once a second while following. Turn it on for the next NPC that refuses to walk.

**Still worth knowing**: Level 2 was baked with `agentRadius 0.5 / agentHeight 2` while Natalia is
about 8 world units tall, so the mesh hugs walls more tightly than a character her size. Raising
an agent's radius above the bake radius is the classic way to get a stuck agent — if hers needs to
grow, the pages have to be rebaked with a matching radius first.

### `_invertFlip`: not every character is drawn facing the same way

`UpdateSpriteFlip()` flips the sprite toward `navAgent.velocity.x`, but which `flipX` value means
"walking right" is a fact about the **art**, not about the movement code — Natalia's placeholder
is drawn facing the opposite way to the rest, so she moonwalked. `_invertFlip` (on `NPC`, off by
default, **on** in `Natalia.prefab`) mirrors which direction counts as unflipped. It only swaps
the two states; it does not change *when* the flip happens, so it stays a pure art-side knob.

**Hard requirement: every page they walk on needs a baked NavMesh.** `PageNavMeshManager`
swaps the active NavMesh per page (issue #21). If a page has none — which is the case for
Level 2's pages until someone bakes them — the agent reports `isOnNavMesh == false` and the
NPC simply will not move. `IsAgentUsable()` guards every agent call so this degrades into a
motionless NPC plus a warning rather than a spam of engine errors, but the fix is baking, in
the Editor. `NavMeshAgent` was added to `Natalia.prefab` and `Abuela.prefab`; all Abuela
instances across every scene are prefab instances, so they inherit it. `m_BaseOffset` is 0 on
both and may need tuning so the sprite sits correctly on the mesh.

## Un solo comportamiento de follow para todos los NPC (2026-09-22)

Diego's call: scrap Abuela's own follow behaviour and use Natalia's for both. `NPC_Abuela`
no longer owns duplicated idle/follow states — `Abuela_IdleState.cs` and
`Abuela_FollowPlayerState.cs` were **deleted**, and their two `State` enum entries with them.
Every NPC now runs the shared `NPC_IdleState`/`NPC_FollowPlayerState`.

What moved onto the `NPC` base so nothing was lost in the merge:
- `StartFollowingPlayer()`/`StopFollowingPlayer()` (one implementation, `params object[]` so
  they can be wired to an `EventManager` event or a UnityEvent).
- `anim` + `SetWalkAnimation()` — the walk/idle animator bool, now optional and null-safe
  (parameter name is a serialized field, default `"IsWalking"`). Abuela's Animator reference
  survives the move untouched: same field name on the same component, so the prefab still binds.
- `UpdateSpriteFlip()` — the sprite flip toward travel direction Abuela's state used to do.
- `_reparentToPlayerPageOnFollow` — reparent under the player's page when following.

`Abuela_DropoffState` stays (it is genuinely hers) and is reached through a new
`NPC.TryExtraTransitions()` hook that the shared states consult first. Note the priority is
now **dropoff outranks follow**, and entering dropoff explicitly stops the follow so finishing
it returns her to a real idle instead of bouncing straight back into following.

**Two real bugs this fixed**, both latent because nothing currently activates Abuela's follow:
1. **A crash.** The old `Abuela_FollowPlayerState` changed to `State.NPC_Idle`, which
   `NPC_Abuela` never registered — and `FiniteStateMachine.ChangeState` does `allStates[state]`,
   which throws `KeyNotFoundException` on a missing key. Stopping her follow would have thrown.
2. **Endless drift.** `NPC.Update()` integrates `velocity` every frame regardless of state and
   only the follow state writes it, so an NPC told to stop kept gliding. The shared
   `StopFollowingPlayer()` now zeroes it for everyone.

## Eventos de diálogo/quest relevantes (subconjunto de `Evento`)

`OnPlayerPressedE`, `OnDialogueStart`, `OnDialogueEnd`, `OnDialogueWriteText`,
`OnQuestCompleted`, `OnQuestDelivered`, `OnQuestRewardedStart`, `OnQuestRewardedEnd`,
`OnAbuelaDropoff`, `OnAbuelaFold`, `OnAbuelaUnfold`, `OnEncounterEnd`,
`OnResourceUpdated`, `OnTreeCutForChickens`.
