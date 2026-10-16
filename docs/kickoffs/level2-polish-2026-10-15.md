We're working through **epic #218 — Level 2 polish after the 2026-10-15 art integration** in KamiPapelTijera
(Unity 2021.3, URP, milestone **M4**, public build due **2026-10-21**). 11 open sub-issues in 3 phases (#221, the Abuela sinking into the floor, was already solved and is closed), in the order below.
Follow `docs/claude/session-playbook.md` for how a phase session runs; this prompt only adds what is specific here.

Out of scope: #121 (real SFX clips), #151 / #83 and the other `[ART-VALEN]` / `[POCHI]` issues, M5-M8.

## Branch and state

- Base: `level2newspaper-pages-blocking-2` (Diego merges finished work there, never into `main`). Last clean commit:
  `3157a1dc` ("page 4 fixes"); the working tree was clean when this was written. Check `git status` and
  `git diff 3157a1dc HEAD -- Assets/Scenes/Level2_Newspaper.unity` first: Diego's test setup (`startingPage`, only one
  page's folder on) shows up in the scene.
- **Never commit, push or open a PR without Diego's explicit OK, every time.** Close issues only after he confirms
  in-game. Tick the epic's checklist (#218) as each one closes.
- Almost every issue touches `Level2_Newspaper.unity` or a prefab it nests: **only one agent writes the scene at a time**.
  Code-only work (2.D, 3.C's script) may run in parallel with scene work.
- Recommended model: Sonnet for phases 2 and 3; Opus for phase 1's diagnosis (1.B).

## Read first

1. `CLAUDE.md`, `docs/claude/session-playbook.md`, then per issue the doc it names. Always: `docs/claude/nivel2-y-ui.md`
   (pages 3-5, the catapult/gift box/trash can/car-variant notes added 2026-10-15) and `docs/claude/cutscenes.md`.
2. `gh issue view` for #218 and each issue in scope, with comments.
3. Tools that already exist (use them, don't redo them by hand):
   - `python tools/compile-check.py` compiles `Assembly-CSharp` in ~20 s. It does NOT prove runtime.
   - `python tools/replace-prefab-instance.py` swaps scene instances to a prefab variant and keeps overrides.
   - `python tools/make-meta.py <paths>` writes `.meta` files for new scripts/folders.
   - Hand-written Animator clips + controllers: copy `Assets/Animations/Catapulta`, `GiftBox`, `TrashCan` (generators
     lived in a scratchpad; the formats are in those folders). **An Animator state with no motion snaps animated
     properties back to their default pose**: give "held" states a hold clip.
4. YAML rules that bit before: ids seen through a variant / nested prefab are `base XOR PrefabInstance id` (per level);
   preserve each file's line endings; **never open a real file with `open(p, 'w')` to test it** (it truncates it; write
   to a temp file and `os.replace`).

## Phase 1: bugs and softlocks (do first)

1. **#220 1.B patrol car vanishes on page 5.** Start by checking whether it happened before the 2026-10-15
   `TrafficObstacle_PoliceCar` swap. First suspect: the car art's looping "Andando" Animator (its scale curve ends at 0)
   still running until `MuseumArrest.Start` switches it off. Report the cause to Diego before fixing.
2. **#219 1.A caught on page 4 refills the trash cans.** Ask the two open questions (which cans; reopen closed) with
   the recommended defaults. `GrantResourcePickup` destroys itself today: change it to deactivate + reset.
3. **#222 1.D missing paper sticker from trash cans.** Investigate first; it may already work. Compare with a bush.

## Phase 2: story order, sound and cameras

5. **#223 2.A Grace's offer after Ariel's arrest.** Split `Museum_Evidence`: new dialogue + es/en table keys + portrait
   (#185 pattern) + `MuseumPage` phase order. Update `nivel2-y-ui.md` and `cutscenes.md`.
6. **#225 2.C witness cops without the lantern** (page 5 `SpineCharacter` idle overlay).
7. **#224 2.B replace the quest-complete sound at page 4's intro.** Ask the question (drop it vs a new sound).
8. **#226 2.D camera selector HUD hides during cutscenes.** Extends #188; same rules (alpha + raycasts, never `SetActive`).
9. **#227 2.E camera shake at the Abuela's crash.** Cinemachine impulse; works on all four pickable cameras.

## Phase 3: visual polish

10. **#228 3.A sparkles on cuttables** (reuse `Brillitos Mágicos`; ask which cuttables).
11. **#229 3.B the Pelusa floats and sparkles** (Animator on the child, never the pickup root).
12. **#230 3.C `PoliceLights`: Animator spin + a ~20-line script, NO `Update`.** Diego already approved this design
    ("Option A") in the issue. Ask only the red-vs-blue intensity question and whether `SetSiren` hooks go into
    `ArrestCutscene` / `MuseumArrest`.

## How to work each issue

- Triangulate the diagnosis against HEAD, then send Diego a short plan per issue with numbered questions only where
  something is genuinely open, each with a recommended default. Build after his OK.
- Verify what can be verified from here (compile-check, YAML parse, id checks, Editor.log after he focuses Unity) and
  say plainly what was NOT run. Diego tests in the Editor.
- Docs in the same pass: the `docs/claude/*.md` each change touches; run `/graphify Assets/Scripts --update` only if a
  new class or a new dependency between systems appeared (3.C's `PoliceLights` counts).
- ENGLISH for everything new (code, comments, assets, docs), and reply in English.
