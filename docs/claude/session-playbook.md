# Session playbook (how a phase session runs)

Every phase kickoff prompt says "follow `docs/claude/session-playbook.md`" and then only lists what is
specific to that phase. This file holds what every session repeats. `CLAUDE.md` still wins on project
facts; this is the working protocol around them.

## Start

1. Read `CLAUDE.md`, the docs the kickoff names, and `gh issue view` for every issue in scope (plus the
   epic's last comment when there is one).
2. Check the scene state: `git diff <last clean commit> HEAD -- Assets/Scenes/Level2_Newspaper.unity`
   and `git diff` on the working copy. Diego's test setup (`startingPage` N, only that page's folder on)
   lives in his working copy and often in his own commits too. The kickoff names the last clean commit.
3. Triangulate: confirm each issue's diagnosis against HEAD before planning.
4. Send Diego a short plan per issue, with numbered questions only where something is genuinely open,
   each with a recommended default. Build after his OK.

## Rules

- ENGLISH for everything new (code, comments, assets, docs). Reply in English.
- NEVER commit or push without Diego's explicit OK for that commit. Close issues only after he confirms.
- Do the mechanical work yourself (scene/prefab/timeline YAML). Hand Diego Editor work only when it
  can't be done from here (a binary fbx's insides, a NavMesh bake, a look he has to judge).
- Surgical changes; comments explain the WHY; `Debug.Log("[ClassName] ...")` at decision points;
  guard clauses that warn when a reference that should be wired is missing.
- Deleting code: list exactly what is in the range first, re-list the file's methods after. A deleted
  `Update`/`Start`/`Awake` compiles clean and breaks silently.
- Docs in the same pass (`CLAUDE.md` "documentación viva"): the `docs/claude/*.md` a change touches,
  and the spec if the feature has one.

## Runtime evidence (we can't run Unity from here)

- Headless Unity (batch mode) is BLOCKED by licensing ("Access token is unavailable"). Don't retry it,
  never type Diego's credentials.
- Diego's Editor log: `$LOCALAPPDATA/Unity/Editor/Editor.log`. Read it after he focuses Unity on your
  changes, before reporting. `EventManager` catches each subscriber's exception and carries on: grep
  `[EventManager] el suscriptor`. A warning's text can hide from a narrow grep: search the class tag.
- `Library/Artifacts` holds each import's result: `grep -rl "<asset name>" Library/Artifacts` + its
  printable strings proves a hand-made asset imported with the right components.
- A prefab created on disk can show "Missing Prefab" until the scene is reopened. Say so up front.
- `python tools/compile-check.py [tag]` = compilation only. Baseline warnings: `JumpFloodOutlineRenderer`
  CS0162, `HongueroTiburcioDialogueTrigger` CS0414. It re-globs sources from disk, so new files count.
- Known noise: `PedestalColorCheck.SetPedestalColor` throws a harmless NRE on every inventory change.

## YAML surgery

- Keep a pristine copy in the scratchpad before editing. Count `--- !u!` blocks before/after, keep new
  fileIDs unique, and check every local `{fileID: N}` (no guid) still resolves to a block.
- Stripped ids: inside a PREFAB they must be `(source XOR prefabInstance) & 0x7FFFFFFFFFFFFFFF`; in the
  SCENE they are free local ids. Retargeting an instance only changes `m_CorrespondingSourceObject` and
  the PI's `target`/`m_SourcePrefab`.
- World positions: walk the `m_Father` chain (prefab-instance roots take their TRS from modifications).
- Binary fbx (`KamiMuseo.fbx`, `Comisaria.fbx`): child names, transforms and mesh ids are hashed inside
  it. Look for existing overrides first; otherwise ask Diego for the Editor-side part.
- Line endings: check with Python (`data.count(b'\r\n')`) and preserve them.
- Scripts that contain escapes: write them with the Write tool and run them, never heredocs (Git Bash
  eats backslashes in them). Issue bodies and comments: write a file, post with `--body-file`.

## Committing the scene

Write a copy of the working scene with `startingPage` and the five page folders' `m_IsActive`
(PageScrollerManager `pagesToToggle`) set to the last clean commit's values, confirm it differs from his
working scene in nothing else, then
`git hash-object -w --no-filters <copy>` + `git update-index --cacheinfo 100644,<hash>,<path>`.
His working copy keeps his test setup. Last clean values (e9326df5): `startingPage: 1`, Page 1 on,
Pages 2-4 off, Page 5 on.

## Level 2 runtime facts

- Diego tests a page by switching on ONLY that page's folder. Test-start setup belongs to the started
  page's owner (`FindCluesTracker`, `ArrestCutscene`, `PoliceStationPage`, `MuseumPage.BeginForTest`);
  `Level2TestStartItems.GrantItemsBefore(page)` gives earlier pages' items.
- `LevelManager.inCutscene` is the shared "Kami frozen" lock (CutsceneDirector, and by hand in a few
  places): release it only if you took it. Kami teleports go through
  `PlayerPageSpawnManager.PositionPlayerAtPoint` (it calls `TriggerScript.ExitAll()`).
- `DialogueManager.ShowDialogue` drops a request while another dialogue is open: wait on
  `CanShowDialogueNow`. `CutsceneDirector.HoldTimeline` must not overlap a dialogue marker's hold.
- NPCs placed in a page wake up before `PageNavMeshManager` adds the page's NavMesh: their agents stay
  detached until warped. Warp before walking them.
- Story music: `DeadEndAngels` from the arrest until `Evento.OnPaintingReturned` (`MuseumPage.CloseTheCase`).
- Positional sounds need distances on their AudioBank row; the listener is on the camera, 13-45 away.
- A cutscene whose wrap-up outlives its page sits at the scene root (page 3's arrest).

## Agents and models

- At most 1 parallel agent besides you, and only you write scenes, prefabs, timelines and shared scripts.
- Set `model` on every spawn: `haiku` for read-only searches and inventories, `sonnet` for mechanical
  edits that follow a known pattern, Opus only for subtle diagnosis or shared-code changes.

## Close-out ("The Usual", once Diego has played it and asks)

- Report per issue what changed, the playtest checklist ("start on page N"), and what you did NOT verify.
- `/graphify Assets/Scripts --update` if classes were added or removed (gitignored, local; copy the
  outputs between root `graphify-out/` and `Assets/Scripts/graphify-out/`).
- One descriptive commit (scene staged without his test setup), push when he says so.
- A status comment on each issue (what was built, changes vs. the issue and why), close it; tick the
  epic's checklist, comment what the next phase inherits, close the epic when all its children are done.
- Consolidate memory, then write the next kickoff: phase-specific content only, pointing here.
