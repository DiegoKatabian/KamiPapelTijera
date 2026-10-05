We're implementing **spec 007, Phases 0 and 1** in KamiPapelTijera (Unity 2021.3, URP): Level 2's
sidequest 2, the **jazz bar jam session** in the bar under Natalia's house (page 3). For N seconds every
key/button plays a random saxophone note from the song's scale; 8+ presses in 10 s wins a saxophone.
Goal of this session: **the jam is playable end to end on page 3** (invitation, countdown, notes, counter,
win/fail, the sax in the inventory, the side quest). Sax mode (Phase 2) is the next session.

## Branch and state

- `011-kami-gear` is clean and pushed (spec 011 Phases 2-4 done, played by Diego 2026-10-05). It is the
  newest Level 2 branch and carries spec 006, the audio refactor and Spine 4.2. **Create
  `007-jazz-bar-minigame` from `011-kami-gear`'s HEAD.** The spec header still says "branches from
  `pages-blocking`": that's outdated, so fix it. Check `git status` before starting.
- Never commit, push or open a PR without Diego's explicit permission, every time.
- GitHub (M7, its own milestone since 2026-10-05: optional, after the public build): epic **#131**. This session = Phase 0 **#122 (0.A), #123 (0.B),
  #124 (0.C)**, then Phase 1 **#125 (1.A), #126 (1.B)**, then the close-out. Art #129 and audio #130 run
  outside code: use placeholders.

## Read first

1. `specs/007-jazz-bar-minigame/spec.md` (stories, FR-001..010) and `tasks.md` (Phases 0 and 1).
2. `docs/claude/controles-y-gamepad.md`: "El botón A es contextual", "Cutscenes: inCutscene", "Pausa del
   Flap" (spec 011 Phase 4 changed it: `FlapState`, `IsMenuOpen` vs `IsFullyOpen`), and the A-button
   gotchas 5 and 6 (same-frame input arbitration).
3. `docs/claude/audio-y-particulas.md` (AudioBank/AudioPool, `maxSimultaneous`, adding a sound = a bank
   row + `Kami/Audio/Regenerate AudioId`, the particle glossary: `moveWithTransform: 0` = World).
4. `docs/claude/quests-y-dialogos.md` (dialogue triggers, the Event-quest gotcha, quests closed from code)
   and `docs/claude/nivel2-y-ui.md` (the two InventoryManager lists, page 3, `PageAmbience`).
5. Code: `Input/InputHub.cs` (the `JoystickButton0..19` sweep, `GetButtonDownSeguro`),
   `Player/PlayerController.CheckControls` (every gate, and which reads happen BEFORE them: sprint and
   Mute are at the top), `Managers/LevelManager.cs` (`ResourceType`, `inCutscene`, `inDialogue`),
   `Managers/AudioManager.cs` (`Play(id, pitch)`), `Managers/Audio/SoundEntry.cs`, `UI/DialogueManager.cs`
   (`CanShowDialogueNow`), `TriggerS/TriggerDialogue.cs`, `Dialogos/GiftDialogueTrigger.cs` (a page 3
   one-off trigger, the closest model), `Quests/QuestManager.cs`, `Level2/FindCluesTracker.cs` (an NPC-less
   quest closed from code), `Managers/PageAmbience.cs`, `Particles/ParticleShooter.cs`.
6. Scene: `Level2_Newspaper.unity`, `Page 3/Natalia's building/Bar` and `Page 3/Musicos` (blocking only,
   no scripts). Page 3's music is silence (`PageMusicManager`); its ambience is `BarAmbience` (`PageAmbience`).

## Scope, in order

- **Phase 0 (all `[P]`, no scene work)**:
  - **0.A #122** `InputHub.NoteKeysPressedThisFrame` (count of fresh presses): keyboard A-Z, 0-9, Space;
    gamepad every `JoystickButton0..19` except Start (7). No sticks, no D-pad, no mouse, and holding does
    nothing. One list in `InputHub`. Plus the **music-mode gate** in `PlayerController` (question 2).
  - **0.B #123** `JazzScale` ScriptableObject (root + semitone offsets + sax clip, optional per-note clips)
    + `SaxNotePlayer` (random note of the scale, pitched by `2^(semitones/12)` through `AudioManager`, capped
    via the entry's `maxSimultaneous`). AudioBank rows + `AudioId`s for the sax note and the jazz loop,
    reusing existing clips as placeholders (question 4).
  - **0.C #124** `ResourceType.saxophone`, appended before `Count` (after `outfitDetective`), +
    `ItemSaxophone.asset` + ItemTable keys es/en/pt, in BOTH InventoryManager lists (the prefab and Level
    2's standalone scene copy).
- **Phase 1 (one scene writer)**:
  - **1.A #125** `JazzJamSession` (`Assets/Scripts/Level2/`) + `JazzBandDialogueTrigger` on a placeholder
    sax player under `Page 3/Musicos` (a tinted Natalia sprite, the Grace/cop recipe). Flow: invitation
    dialogue -> countdown -> jam (backing loop, counter, a note particle burst on Kami per press) -> win
    (sax granted once, cheer line) or fail ("¡Casi!", talk again to retry). Duration (10 s) and minimum (8)
    in the Inspector. A small UI panel under Level 2's `Canvas` (countdown + counter, `LocalizedText`).
  - **1.B #126** `Quest09_JamSession` (Event): a new `Evento.OnJamSessionWon` appended at the END of
    `Evento`, plus its `QuestManager` subscribe, handler and unsubscribe. Added on the first invitation;
    removed a frame after it completes. No `OnQuestDelivered`. Dialogue lines es/en/pt.
- **Close-out** (below).

## Ask Diego before building (each has a recommended default)

1. **Base branch**: `007-jazz-bar-minigame` from `011-kami-gear` HEAD. Default: yes.
2. **Music-mode gate**: a dedicated `LevelManager.inMusicMode`, or reuse `inCutscene`? `inCutscene`
   blocks movement, jump and attack, but lets Interact through while a dialogue shows and doesn't block
   I/U/O/M or Mute. During the jam, E, Space, I, U, O and M are notes. Default: a dedicated flag that
   `CheckControls` checks first, blocking everything gameplay plus I/U/O/M/Mute/sprint.
3. **Esc/Start during the jam**: Default: they cancel the jam quietly (no line, no fail); talking again
   restarts it. They never open the Flap on that press (same-frame arbitration, #41.2/#41.14 pattern).
4. **Placeholder audio**: Default: the sax note reuses an existing clip with a clear pitch (name it in the
   bank row), and the backing loop reuses an existing loop, playing on the Music bus only during the jam.
   `BarAmbience` keeps playing. Diego swaps in the real clips (#130) without code changes.
5. **Scale**: Default: a blues scale (0, 3, 5, 6, 7, 10) on the placeholder clip's own pitch, editable on
   the `JazzScale` asset.

## Decided rules: don't reopen these

- Free play: every press is a point, with no timing and no wrong notes (FR-001). 10 s / 8 presses by default.
- The sax is granted with the normal reward sticker (NOT `ownedAtLevelStart`). It isn't gear:
  `GearCatalog.ForResource` returns null for it, so tapping it in the bag only showcases it (until Phase 2).
- The quest never blocks the story. The bar is only reachable on page 3 before the letter is read.
- One writer of `Level2_Newspaper.unity` at a time. Agents only if Diego asks (Phase 0's three tasks are
  parallel-safe if he does).
- Spec 011's Flap rules stand: menu input, selection and item taps only while `FlapManager.IsFullyOpen`.

## Gotchas already paid for

- **Same-frame input**: the E/A that closes the invitation dialogue must not count as the first note. The
  countdown helps, but start counting only after it ends, and ignore presses during it.
- `DialogueManager.ShowDialogue` silently drops a request while another dialogue is open: wait on
  `CanShowDialogueNow` for the win/fail lines (`GiftDialogueTrigger` does it).
- An Event quest needs its own `QuestManager` handler, or it never completes and nothing says so. Don't
  add/remove quests inside an `OnQuestCompleted` handler (wait a frame). Don't fire `OnQuestDelivered`
  for a quest with no reward (you'd get a mushroom sticker).
- `InventoryManager.Start` builds a dictionary with `.Add()`: a duplicated item throws. A missing item in
  one of the two lists warns and is skipped.
- `ResourceType` ints are stored in assets: append only, never reorder.
- Particles: one-shot prefabs need `looping: 0`, `playOnAwake: 0`, `moveWithTransform: 0` (World),
  or the notes ride along with Kami.
- A new trigger that answers the action button must override `EsInteractuable => true`, or A jumps.
- `PlayerController` reads sprint (L1) and Mute (M) BEFORE its gates: the music gate has to sit above them.
- `FlapManager.ToggleFlap` reverses a slide mid-way since spec 011 Phase 4. Esc is read in
  `CheckControls` (`OpcionesDown` -> `OnPlayerPressedEsc` -> `OpenSettings`): gate it there, not in the Flap.
- YAML surgery: read the whole file, copy existing patterns, stripped blocks use the XOR fileID, preserve
  line endings, count `--- !u!` blocks before/after, check fileID uniqueness. New `.meta` files:
  `python tools/make-meta.py`.
- Deleting code: list what's inside the range before cutting (a deleted `Update()` compiles clean).

## Working rules

- English for everything new and in replies to Diego. Leave existing Spanish alone, except a comment that
  becomes false.
- Comments explain the non-obvious why. Braces always. `[SerializeField] private` + `[Tooltip]` for every
  tunable. Guard clauses that warn. `Debug.Log($"[ClassName] ...")` at decision points.
- Do the mechanical work yourself (YAML, assets, localization rows). Ask Diego about decisions, not labour.
- Triangulate the code first. If the spec doesn't match the code, stop and ask with a recommended default.

## Verification

- `python tools/compile-check.py` after each task. Baseline warnings: `JumpFloodOutlineRenderer` CS0162,
  `HongueroTiburcioDialogueTrigger` CS0414. Compilation only: say clearly what wasn't run.
- Diego's playtest (start the scene with `PageScrollerManager.startingPage = 3`): the spec's Story 1
  acceptance 1-4, plus: holding a key counts once; WASD/Space/E/I/U/O/M during the jam only play notes
  (no movement, jump, interact, Flap); Esc/Start follow question 3; the press that closes the invitation
  isn't a note; win twice gives one sax; fail -> talk again -> retry; the quest appears on the first
  invitation and completes on the win; mashing never clips the audio; the arrest still works afterwards.

## Close-out

- Docs: `docs/claude/nivel2-y-ui.md` (page 3 bar), `quests-y-dialogos.md` (Quest09, the band trigger),
  `audio-y-particulas.md` (the new bank rows as placeholders), `controles-y-gamepad.md` (the music-mode
  gate and the note-key list), `CLAUDE.md` (the 007 branch line and `Assets/Scripts/Level2/`).
- Spec and tasks: spec status, Diego's answers as decisions, "Phase 0/1 as built" in `tasks.md` with any
  changes vs. the design and why.
- `/graphify Assets/Scripts --update` from the repo root, then copy `graph.json`, `GRAPH_REPORT.md`,
  `graph.html` and `manifest.json` into `Assets/Scripts/graphify-out/` (both gitignored).
- Leave everything uncommitted. When Diego has played it and asks: commit and push, comment on and close
  #122-#126, tick them in epic #131, and comment on #131 with what Phase 2 (#127 usable items, #128 sax
  mode) inherits. Note for Phase 2: `InventorySlot.BUTTON_OnPress` already showcases + toggles gear (spec
  011), so the "use" action must coexist with that path and also require `IsFullyOpen`.
