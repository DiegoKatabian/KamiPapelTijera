# Tasks: Jazz bar jam session + the saxophone (spec 007)

Phases run in order; `[P]` tasks inside a phase are parallel-safe with their siblings. **Only one
agent may write `Level2_Newspaper.unity` at a time** (same rule as spec 006).

## Phase 0 — Foundations (no scene work, all `[P]`)

- **0.A** `[P]` **Note input**: `InputHub.NoteKeysPressedThisFrame` (count of fresh presses this
  frame) over keyboard A-Z / 0-9 / Space and `JoystickButton0..19` except Start (7). One place owns
  the list (FR-004). Plus the gate: a flag (`LevelManager.inMusicMode` or reuse `inCutscene`, decide
  by reading `PlayerController.CheckControls`) that makes `PlayerController` ignore movement, jump,
  attack, interact AND the letter menu hotkeys (I/U/O/M) while music mode is on (FR-005).
- **0.B** `[P]` **Notes**: `JazzScale` ScriptableObject (root + scale offsets + sax clip, optional
  per-note clips) and `SaxNotePlayer` (plays a random note of a scale through `AudioManager`/the
  pool, with a small cap on simultaneous notes so mashing doesn't clip). FR-003. `AudioBank` row +
  `AudioId` for the sax sample and the jazz loop (placeholder clips until Diego's arrive).
- **0.C** `[P]` **The saxophone item**: `ResourceType.saxophone` (appended before `Count`) +
  `ItemSaxophone.asset` + ItemTable keys (es/en/pt), in BOTH `InventoryManager` lists (FR-008).

## Phase 1 — The jam session (page 3; depends on 0.A, 0.B, 0.C)

- **1.A** **Jam minigame** (`JazzJamSession`, `Assets/Scripts/Level2/`): band trigger (a
  `TriggerDialogue`-based `JazzBandDialogueTrigger` on a placeholder sax player in
  `Page 3/Musicos`) -> invitation dialogue -> jam: backing loop, countdown, counter, floating notes
  (a particle burst per note on Kami) -> win (sax granted once, cheer line) / fail ("¡Casi!",
  retry by talking again). Duration and minimum in the Inspector (FR-002). UI: a small panel under
  the Canvas (countdown + counter), `LocalizedText` for its labels.
- **1.B** `[P]` **Side quest**: `Quest09_JamSession` (Event), new `Evento.OnJamSessionWon`
  appended at the end of `Evento` + its `QuestManager` handler (the Event-quest gotcha in
  `docs/claude/quests-y-dialogos.md`); added on the first invitation, removed a frame after it
  completes (no `OnQuestDelivered`: no reward sticker from the quest itself, the sax is given by
  1.A). Dialogue lines es/en/pt.

## Phase 2 — Sax mode (depends on Phase 1 for the item; 2.A can start after 0.C)

- **2.A** `[P]` **Usable inventory items** (generic, FR-009): an optional "use" action on
  `InventoryItem` (e.g. an `Evento` or a `UsableItem` asset) + `InventorySlot` becomes
  activatable (click / Submit with gamepad) only when its item has one. Activating closes the Flap
  and raises the action. Items without a use action are untouched.
- **2.B** **Sax mode** (`SaxMode`): answers the sax's use action; enters music mode (0.A gate),
  plays notes with `SaxNotePlayer` on the bar scale (FR-010), exits on Esc/Start without letting
  that press open the Flap on the same frame (the #41.2/#41.14 same-frame arbitration pattern). Can't
  start during dialogue/cutscene/origami/overlay/page turn.

## Art and audio dependencies (outside code, `[P]` with everything)

- **[ART-VALEN]** Kami playing the saxophone (Spine animation; the jam and sax mode use it, idle
  until then), the saxophone inventory icon, the jazz band musicians.
- **Audio (Diego)**: the bar's jazz backing loop and the saxophone sample(s); swap them into the
  `AudioBank` rows from 0.B.

## Parallelism summary

Phase 0 is three independent agents (0.A input, 0.B notes, 0.C item), none touching the scene.
Phase 1 has one scene task (1.A) plus the quest data (1.B) alongside. Phase 2: 2.A can start as
soon as the item exists; 2.B closes it. Art and audio run in parallel from day one.

## Verification

`python tools/compile-check.py [tag]` after every code task (baseline warnings:
`JumpFloodOutlineRenderer` CS0162, `HongueroTiburcioDialogueTrigger` CS0414). It proves compilation
only: the feel of mashing, the gate on the menu hotkeys and the gamepad button list are Diego's to
play-test (start the scene with `PageScrollerManager.startingPage = 3`).
