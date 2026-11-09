# 025 - progress log (build session, 2026-11-07)

Branch `025-paper-book`, nothing committed. Diego asked to run every phase to the end without waiting; Unity is closed,
so nothing has been imported or played yet. Checks run from here are listed per item.

## Part A: paper page turns - built

- 0.E `tools/compile-check.py` compiles game + Editor code. `tools/run-paper-tests.py` compiles the core + tests with
  Unity's bundled csc and runs them on its bundled Mono: 7/7 pass.
- 0.B/0.C `tools/measure-paper-book.py` -> `tools/data/paper-book-level1|2.json` (spine, page z, surface profiles with
  the gutter dip, uv maps of each half, max fit error 0.005). `tools/fit-paper-curl.py` -> `tools/data/paper-curl.json`
  (lead 0.25, ease 0, shape 1.8, rms 14.5 deg vs the old clip, which floated above the book and drooped below
  horizontal) + `previews/curl-fit.png`.
- 0.D audit: done at runtime instead (PaperTurn logs every turn's pieces by class; `_logPieces` lists each one). The
  static audit's facts are in spec.md "Review findings".
- Core (`Assets/Scripts/Book/Paper/Core/`, no UnityEngine): PaperMath, PaperProfile, PaperCurl, PaperFold, PaperTurnModel.
  Python twin `tools/paper_core.py`, goldens `tools/make-paper-goldens.py`, tests `tools/paper-tests/`.
- Runtime: PaperBookData (assets `Assets/Data/PaperBook/PaperBook_Level1|2.asset`, by `tools/make-paper-book-assets.py`),
  PagePiece, PaperPieces (grouping, classification, Graphics.DrawMesh proxies), PaperSheet (+ prefab
  `Assets/Prefabs/Book/PaperSheet.prefab`), PaperTurn, shader `Kami/Paper Overlay` + `Assets/Materials/Paper/PaperOverlay.mat`.
  PageScrollerManager `_paperPages` / `_paperTurn`; BookPageSpread.DressPaperSheet.
- Design changes found while building (spec updated in close-out):
  - proxies are drawn with Graphics.DrawMesh, no GameObjects (a copied transform can't hold the source's skew);
  - Begin runs at the sheet's start (0.5 s), not at the press: nothing to draw before the sheet exists;
  - flat pieces: four captures (each page over each half), two ride the sheet, two lie still on the halves, so a flat
    piece across the spine is split by the two rectangles;
  - facing pages count each other's pieces (`pageThicknessShare`, 0.5): see previews/paper-turn-level2.png;
  - page lights are left alone (they follow their folder as today); particles, trails and lines are hidden.
- Scenes: `tools/wire-paper-turn.py` added a root `PaperTurn` object to both level scenes and switched `_paperPages` on.
- 4.D `tools/unbatch-page-props.py --apply`: Batching Static removed from 29 flags in page prefabs and 10 in Level 1's
  scene; Level 2's remaining flags are managers/walls/table.
- 4.C Editor: `Kami > Pages > Tag Page Pieces - Report / Apply`. EditMode tests: `Assets/Editor/Tests/PaperBookTests.cs`.

## Part B: the hand and the intro - built

Importer, hand, intro books, covers, clips (intro + Level 3), BookIntro, carry, skip, sounds, MagicalGirlChange
hand-off, the Build menu and EditMode tests. Details, design changes and Diego's next steps: spec.md "As built".

## Checks at the end of the session

- `python tools/compile-check.py`: game + Editor compile, baseline warnings only.
- `python tools/run-paper-tests.py`: 7/7.
- `python tools/verify-gen.py`: 6 files, 0 failures.
- Not run: anything inside Unity (closed all session).
