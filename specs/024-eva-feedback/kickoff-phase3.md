# Kickoff prompt: spec 024 Phase 3 (gameplay feel)

Paste everything under the line into a fresh session. **Run it on Opus** (the orchestrator; it may spawn one Opus fork and one Sonnet).

---

You are the **orchestrator** for spec 024 Phase 3 in KamiPapelTijera, and after it Phase 4, then specs 022 and 023. The general
rules, the helper split, the decisions already made, the gotchas and the per-phase playtest checklists are in
`specs/024-eva-feedback/kickoff.md`: read it and follow it. This prompt only adds what changed since. Reply to Diego in English;
everything you add to the repo is English.

## State

- Branch `after-eva-feedback`. HEAD is the commit "Spec 024 Phases 1-2" (2026-10-27): Phases 1 and 2 are built, played by Diego
  and closed (#276-#284, ticked in epic #275). Nothing of Phase 3 is started.
- Left unstaged on purpose, not yours: `Assets/Scenes/Nivel1_KamiPapelTijera.unity` (Diego's working changes) and
  `specs/020-kami-magical-girl-change/spec.md` (another session's status note). Diego's Level 2 test setup
  (`startingPage` 4, page 4's folder on) is in his working copy: never commit it (playbook, "Committing the scene").
- Graphify was not refreshed after Phase 1's new `CutSoundGate` class; do it at the end of the job
  (`/graphify Assets/Scripts --update`, from the repo root).
- Filed for later, **not part of Phase 3**: Natalia and the Abuela jump out of page 4's window instead of reappearing below it
  (#328, milestone M4). Don't build it unless Diego asks.

## Phase 3 tasks (spec 024 "Phase 3", issues #285-#288)

Triangulate each against HEAD before planning. Starting points, not conclusions:

- **3.A #285, a stone that can't be cut yet.** Find the stone first: it is a cuttable gated on the upgraded scissors
  (`TijeraEquipment.Mejorada`, `Player.currentTijera`). `RocosoCortable` has `_isImmuneToTijeraMejorada`, which is the opposite
  gate and the Rocoso enemy, so it is probably not it; grep the Level 1 scene and `Assets/Prefabs/Cortables/` for the stone
  prefabs and read how the cut is refused. Sparks = a prefab **variant** of an existing particle prefab (never edit
  `Brillitos Mágicos.prefab` itself, other scenes share it). Reaction: try Kami's existing `Hit` animation (track 5,
  `PlayerView`); a missing `cut-pushback` means an `[ART-VALEN]` issue with `dependency-arte`.
- **3.B #286, plane pedestal locked while the hat's jump is ready.** The spec has the verified facts (`isPaperPlaneHat`,
  `augmentedJumpsLeft`, `Evento.OnPaperPlaneHatLost` from `Player.cs` ~line 1032). The gate belongs in `TriggerOrigami`
  (`CanAutoPrompt`, the step-on path, the cost canvas) behind a serialized flag on the two plane pedestals only (Level 1's Avion
  route pedestal, Level 2 page 4's `OrigamiRoute_PaperPlane` pedestal). Default from the spec: no tooltip, just disabled.
- **3.C #287, `PullSolapas` before a page turn.** `PageScrollerManager.TriggerPageScroll` (the E at an edge sphere) starts
  `CerrarPaginaCoroutine(delayTime)`; `TurnToNextPage(ridePage: false)` (the arrest) must skip the pull. Call
  `Player.CancelAttack()` first (track 3 rule, `spine-kami.md`); `Player.PlayPullSolapa` already does that and locks movement
  through `IsPullingSolapa`. Lengthen `delayTime` in both scenes only as much as the animation needs (measure the clip in the
  skeleton json, don't guess).
- **3.D #288, the dam's hitbox** is Diego's manual fix: just ask him whether it's done; close it when he says so.

## Notes from Phases 1-2 that matter here

- The Flap now has its own canvas (order 5) and the origami takes no input while it is open (`MultipleRectCheck.PausedByFlap`).
  A new UI element that must show above the Flap needs a canvas order above 5.
- `TriggerSolapa._tooltipOnlyUntilOpened` is the pattern used for "a tooltip that stops meaning anything": reuse it rather than
  `_maxTooltipShows` if 3.B ends up wanting a tooltip.
- Phase 3's playtest checklist is in `kickoff.md` ("Playtest checklists").

Start now: read, triangulate Phase 3, and send Diego the plan with numbered questions (each with a default) only where something
is genuinely open.
