We're on **Phase 2 of epic #218 — Level 2 polish after the 2026-10-15 art integration** in KamiPapelTijera
(Unity 2021.3, URP, milestone **M4**, public build due **2026-10-21**). Phase 1 is done and played (#219, #220 fixed,
#221/#222 needed nothing); the epic's last comment says what it left behind.
Follow `docs/claude/session-playbook.md` for how a phase session runs; this prompt only adds what is specific here.
Recommended model: Sonnet.

Out of scope: Phase 3 (#228-#230), #121 (real SFX clips), the `[ART-VALEN]` / `[POCHI]` issues, M5-M8.

## Branch and state

- Base: `level2newspaper-pages-blocking-2` (Diego merges there, never into `main`). Last clean commit: the Phase 1
  commit right after `26c46813`. It restored the scene's clean test values (`startingPage: 1`, Page 1 Objects and
  Page 5 on, Pages 2-4 off); `3157a1dc` had shipped Diego's page 4 setup. Check `git status` and the scene diff first.
- **Never commit, push or open a PR without Diego's explicit OK, every time.** Close issues only after he confirms
  in-game, and tick the epic's checklist as each one closes.
- Only one agent writes `Level2_Newspaper.unity` (or a prefab it nests) at a time. 2.D is the only code-only issue.

## Read first

`CLAUDE.md`, the playbook, `docs/claude/nivel2-y-ui.md` (page 4 and page 5 sections, "The HUD leaves during
cutscenes"), `docs/claude/cutscenes.md` ("Tuning the page 4 intro", "Tuning Ariel's arrest"), and `gh issue view` for
#218 (last comment) and each issue below. Phase 1 lesson: anything that must be in place before a page's Animators run
goes in `Awake`, not `Start` (a page switched on by a page turn gets one animation pass first).

## Phase 2: story order, sound and cameras (in this order)

1. **#223 2.A Grace's catapult offer after Ariel's arrest.** Split the offer out of `Museum_Evidence` into its own
   `DialogueSO` (new es/en `DialogueTable` keys, portraits like #185) and play it from `MuseumPage` after
   `MuseumArrest` finishes, before the items are handed over and the catapult unlocks. Check the speaker portraits and
   that no line is shown twice. Docs: `nivel2-y-ui.md` page 5 steps 2-3, `cutscenes.md`.
2. **#225 2.C witness cops without the lantern.** Page 5's two `CopEscort` witnesses get an empty `SpineCharacter`
   idle overlay from the start (scene overrides, page 4's guards keep theirs); remove the now-pointless "lose the
   lantern" cue in `Museum_Evidence` if it only did that, and check the `MuseumArrest` escort doesn't put it back.
3. **#224 2.B page 4 intro fanfare.** `ArrestCutscene`'s wrap-up plays `QuestCompleted02` as Quest06 is removed.
   Ask: drop it (default; `CellBarsSlam` already marks the moment) or a new sound. Only this one; the other NPC-less
   quests keep theirs.
4. **#226 2.D camera selector HUD hides in cutscenes.** Extend #188's rule to `CamWheelManager`: fade with a
   `CanvasGroup` (alpha + `blocksRaycasts`, never `SetActive`, never `interactable`), close an open wheel when a
   cutscene starts, back exactly when Kami gets control back. Compare with `FlapManager.UpdateHudVisibility`.
5. **#227 2.E camera shake at the Abuela's crash.** `CinemachineImpulseSource` fired from `AbuelaEntrance.Impact()`
   plus a `CinemachineImpulseListener` on each pickable vcam (`CameraManager`) and the page 4 intro vcams; amplitude
   and duration in the Inspector. Check it also fires when she falls again after a capture.

## How to work each issue

- Triangulate against HEAD, send Diego a short plan per issue (numbered questions only where genuinely open, each
  with a recommended default), build after his OK.
- Verify what can be verified from here (compile-check, YAML checks, Editor.log after he focuses Unity) and say
  plainly what was not run. Diego tests in the Editor.
- Docs in the same pass. No new class is expected in this phase, so no `/graphify` unless one appears.
- ENGLISH for everything new, and reply in English.
