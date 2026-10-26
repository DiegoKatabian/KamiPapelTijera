# Kickoff prompt: EVA feedback batch, then specs 022 and 023

Paste everything under the line into a fresh session. **Run it on Opus** (the orchestrator; it will spawn one Opus fork and one Sonnet).

---

You are the **orchestrator** for a three-part job in KamiPapelTijera: the EVA feedback batch (spec 024), then the flying paper stickers (spec 022), then the settings menu and audio channels (spec 023). Follow `docs/claude/session-playbook.md` for how a session runs; this prompt only adds what is specific. Reply to Diego in English; everything you add to the repo is English.

## State

- Branch: `level-2-sounds` (clean at 62dd58b8 apart from three new spec folders: `specs/022-...`, `023-...`, `024-...`, uncommitted). Ask Diego once whether to cut a branch (suggest `024-eva-feedback`) before you build; don't commit, push or switch without his OK.
- GitHub: milestone **M9 - EVA feedback and polish (specs 022-024)**. Epic **#275** (spec 024, tasks #276-#295), epic **#296** (spec 022, tasks #297-#304), epic **#305** (spec 023, tasks #306-#314). Task ids in the specs (1.A, 2.D, ...) map to the issue titles.
- Specs 020 and 021 (magical girl change, magnifier and tool switching) are Diego's design work on another branch. Do not touch them. Spec 021 will add a tool selector to the HUD strip: don't make the Flap harder to extend.
- Diego's Level 2 scene test setup (`startingPage`, which page folders are on) lives in his working copy: don't "clean" it, and stage the scene without it when he asks you to commit (playbook, "Committing the scene").

## Read first

`CLAUDE.md` (all of it, the docs it points at that touch your task), then `specs/024-eva-feedback/spec.md` in full, and `gh issue view` for the tasks of the phase you are starting. Read 022 and 023 only when you reach them. Docs worth knowing for this batch: `docs/claude/audio-y-particulas.md` (AudioBank, buses), `nivel2-y-ui.md` (Flap, HUD, trash cans, CrimeSceneGate, catapult, cuttables), `origami-y-tooltips.md` (tooltips, pedestals, Origami), `paginas-y-hoja.md` (page turn), `controles-y-gamepad.md` (input gotchas, Flap states), `spine-kami.md` (the track-3 rule for `PullSolapas`).

## How to orchestrate (Diego's explicit instruction for this job)

You may run **two helpers at a time**: one **Opus fork** (`subagent_type: "fork"`, it inherits this conversation) and one **Sonnet** agent (`model: "sonnet"`). This raises the playbook's limit of one for this job only. Rules that keep it safe:

1. **File ownership is disjoint.** Before spawning, list the exact files each helper may write. A helper never touches a file another helper or you own.
2. **Only you write scenes, prefabs, timelines, and any script more than one system uses.** The Opus fork may write a NEW self-contained script (for example `CutSoundGate.cs`) or diagnose and report; it returns a diff summary and you integrate. Sonnet does mechanical work and read-only inventories.
3. Every spawn states the goal, the files, "never commit", "English", and what to report. A helper's report is a claim: check the diff before you build on it.
4. Never delegate understanding. You triangulate each task against HEAD yourself before anyone writes.

Suggested split (adjust once you have triangulated):

| Phase | You (Opus) | Opus fork | Sonnet |
|---|---|---|---|
| 1 Numbers and audio | 1.C posters/cans prefabs | 1.B `CutSoundGate` (new script, you wire the 8 callers) | 1.A bank rows, 1.D ES punctuation list (read-only first) |
| 2 UI and text | 2.B, 2.C, 2.D | 2.A and 2.E diagnosis (report, you fix) | inventory of both scenes' canvases for 2.D |
| 3 Gameplay feel | 3.B, 3.C | 3.A stone type + particle variant research | 3.D is Diego's: nothing |
| 4 Level 2 world | all scene edits | 4.C fence gap analysis, 4.G car wheel structure (report) | 4.A and 4.B renderer/sorting inventories |

## Order and cadence

1. **Phase 1, then 2, 3, 4** of spec 024 (cheap and independent first, scene work last). One phase at a time.
2. For each phase: triangulate every task against HEAD, then send Diego a **short plan per task with numbered questions only where something is genuinely open, each with a recommended default**. Build after his OK. Where he already decided, don't re-ask (the spec records his decisions).
3. When Diego has played a phase and says it works, close the tasks per the playbook ("The Usual": evidence comment on each issue, close, tick the epic). Not before.
4. After spec 024's epic is done, start **spec 022**, then **spec 023**, each with its own triangulation + plan + OK. Their open questions are listed at the bottom of each spec with defaults.

## Design decisions already made (don't reopen)

- 1.B: stagger 15-40 ms, +2/+4 semitones, gain 1/sqrt(N), cap 3, both `TijeraHit` and `PaperCut` through one gate. 1.A: `CarHornIdling` -6 dB, `QuestCompleted02` -3 dB (not the Pitched row), `RopeCreak` volume +0.1.
- 1.C: posters 2 paper, Level 2 trash cans 4, no other rebalancing. 1.D: show the list before writing; Spanish only.
- 2.B lock = logo + 1 s. 2.D: Flap above everything and the origami pauses. 3.B: lock while `isPaperPlaneHat && augmentedJumpsLeft > 0`, both levels. 3.C: `PullSolapas` placeholder, longer delay, player-initiated turns only.
- 4.D: keep both police tapes, neither cuttable until the cops leave. 4.E: bigger trigger and move away from `esferaNext`.
- 022: swarm + "+N" from 5 papers, single stickers up to 4, non-paper fly to the Flap button, `kamiRewardSticker` untouched. 023: structure and names in the spec's table (approved mockup).
- Nothing gets committed without Diego's explicit OK, every time.

## Gotchas already paid for

- A deleted `Update`/`Awake` compiles clean and breaks silently: list a range before deleting, re-list the file's methods after.
- Anything that calls into the engine can't run in a field initializer (`NavMeshPath` lesson); create in `Awake`.
- `TooltipManager.HideTooltip()` without a colour hides all five post-its; a tooltip that "opens and closes" is usually something calling it.
- `PullSolapas` and `Attack` share track 3: `Player.CancelAttack()` first.
- Kami teleports go through `PlayerPageSpawnManager.PositionPlayerAtPoint` (calls `TriggerScript.ExitAll()`); a trigger that gets its object disabled sends no exit.
- A page switched on by a page turn's coroutine runs one Update before `Start`: park animated objects in `Awake` (#220).
- `OnResourceUpdated` subscribers read parameters by index: append, never reorder (spec 022 0.A).
- New data-like values (volumes, timings, thresholds) go in an Inspector-editable asset or a serialized field, never hard-coded; add `[Tooltip]`s in English.
- Two Flap-related specs are in flight (022 HUD targets, 023 Settings tab) plus 021's selector: keep `FlapManager` changes minimal and localized.
- Headless Unity is blocked by licensing; verification is `python tools/compile-check.py` (compilation only) plus Diego playing. Say what you did NOT verify, always.

## Playtest checklists (give Diego the one for the phase you finish)

- **Phase 1**: cut two bushes in one swing (no doubled thump), cut one (unchanged); a poster gives 2, a Level 2 trash can 4; the list of ES strings before edits.
- **Phase 2**: a 3-line dialogue stays still while typing (both levels); main menu logo then New Game (and pad); level select title click/select; Esc in the middle of a fold in both levels (Flap in front, fold frozen); page 4 jail cans' tooltip, several approaches and after a capture.
- **Phase 3**: hit a locked stone (sparks + reaction), an unlocked one (as before); plane pedestal with the hat ready vs spent, both levels; press E at a page edge (pull, then turn), a forced turn (no pull).
- **Phase 4**: "start on page N" for each: page 1 buildings and newsstand never over Kami; lupas vs fences; walk out of the museum yard everywhere; both tapes before and after the cops leave; hat pickup without E, no accidental page turn; page 5 rope entrance and its creak; cars' wheels while moving only.

## Close-out per phase

Report per issue: what changed, files, what you did not verify. Run `python tools/compile-check.py` before reporting. Update the docs a change touches in the same pass (`CLAUDE.md` "documentación viva") and the spec's "As built". At the end of the whole job, write the next kickoff (phase-specific content only) and recommend a session model.

Start now: read, triangulate Phase 1, spawn your helpers per the table, and send Diego the Phase 1 plan with questions and defaults.
