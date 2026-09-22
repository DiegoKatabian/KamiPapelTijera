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
| Abuela | `AbuelaDialogueTrigger.cs` + `NPCs/NPC_Abuela.cs` | Completo — el más complejo, coordina boss fight + origami fold/unfold |
| Dalia | (vía `QuestDialogueTrigger`, quest de flores) | Completo |
| Tiburcio/Honguero | `HongueroTiburcioDialogueTrigger.cs` | Completo, recién estabilizado |
| Granjero Norberto | `GranjeroNorbertoDialogueTrigger.cs` | Completo — además spawnea el pickup de tijera en el primer diálogo |
| **Chino** | `ChinoDialogueTrigger.cs` | **STUB VACÍO** — la clase existe pero no implementa nada, pese a que `Quest04_Chino.asset` ya está configurada (100× papel → SprintBoots). Quest sin NPC funcional. |
| **Florista** | `NPCs/NPC_Florista.cs` | **STUB VACÍO** — clase sin implementación, sin diálogo conectado. |

`NPC.cs` es la base común de estado (junto con `NPC_FollowPlayerState`/
`NPC_IdleState`, reusados por Abuela y pensados para reusarse por más NPCs).

## Natalia (Level 2 NPC) — 5-page detective story, see spec 006

Exhaustive grep over scripts, quest/dialogue assets, `.unity` scenes, and
localization tables: **zero results** (just a "Natalia's building" GameObject in the
scene, no component). Natalia is NOT a simple quest NPC like the 4 above — she's a
companion who travels with Kami across Level 2's 5 pages in a detective arc (a
painting theft, Ariel's trap, an arrest, a police-station escape with the
Grandmother, resolution at the museum). Implemented on top of the generic
`NPC`/`NPC_IdleState`/`NPC_FollowPlayerState` FSM (NOT the duplicated `NPC_Abuela`
mold), with 3 chained `QuestSO` instead of one. Full detail, phased breakdown, and
what current code already covers: `specs/006-nivel2-detective-natalia/spec.md` and
`tasks.md` in that same folder. The old spec (`specs/003-natalia-npc-nivel2/`) is now
superseded — it didn't reflect this scope.

## Eventos de diálogo/quest relevantes (subconjunto de `Evento`)

`OnPlayerPressedE`, `OnDialogueStart`, `OnDialogueEnd`, `OnDialogueWriteText`,
`OnQuestCompleted`, `OnQuestDelivered`, `OnQuestRewardedStart`, `OnQuestRewardedEnd`,
`OnAbuelaDropoff`, `OnAbuelaFold`, `OnAbuelaUnfold`, `OnEncounterEnd`,
`OnResourceUpdated`, `OnTreeCutForChickens`.
