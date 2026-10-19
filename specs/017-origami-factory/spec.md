# Spec 017 — Origami Factory (draft for review, 2026-10-19)

Status: **DRAFT, nothing built.** Written after a research pass; meant to be argued with, not approved as is.
Owner: Diego (design, math, pipeline), Valentino (style sign-off on every model). Author of the draft: Claude.

## 1. Intent

Add many new playable origamis (boat, crane, box, heart, flower, dragon, ...) without Valen redrawing every
fold by hand, **and without them looking like anything other than Valen's work**.

Priorities, in Diego's order:

1. **It looks like Valen's style.** Hard gate: Valen signs off each model. If not, it does not ship.
2. **Always fewer than 5 steps** per origami (we read this as a hard cap of 4; see open question 1).
3. A new model costs Diego hours, not days, and arrives **ready to drop in the game** (no hand-placing in Unity).

## 2. What exists today (read from the repo, 2026-10-19)

One playable **step** ("OSU") is:

| Piece | What it is | Where |
|---|---|---|
| Sheet | 2100x1400 RGBA PNG, 3x2 grid of **6 frames of 700x700**, transparent background, hand-painted | `Assets/2D/OrigamiProps/OrigamiSheet *.png` |
| Clip + controller | `.anim` with 6 sprite keys at 1/12 s; controller with one state whose **time is driven by the float `_OrigamiSlider`** | `OrigamiSheet_*.anim/.controller` |
| Flipbook prefab | Image + Animator using that controller | `Prefabs/OrigamiRoutes/Flipbooks/` |
| OSU prefab | `OrigamiRoute`: flipbook + `startingPoint`, green arrow (`inicioRectangle`), corridor rectangles, red arrow (`finalRectangle`) | `Prefabs/OrigamiRoutes/OSU-*.prefab` |

`OrigamiRoute.SetImagePosition` projects the pointer on the straight line start -> end and writes the 0..1 slider
(its own comment says curved routes do not work). A full origami = an `Origami` subclass holding an ordered list of
routes (`OrigamiRoute Avion` = 2 steps, Escalera = 3, Puente = 1). Today: 6 sheets, 4 OSU prefabs, 11 route prefabs.

What Valen's frames show (the "style" we must reproduce): hand-painted cardstock grain; a thin darker-gold border; the
**lifting flap is foreshortened with a little perspective** (a trapezoid, not a plain rectangle); the **reverse side is a
darker, slightly translucent tone**; a soft gradient across the flap and darker triangles at its corners; no cast shadow, no
outline. These are measurable, which is what makes a procedural pipeline plausible.

Environment: Python 3.12 + numpy + Pillow available; **no Blender installed**; GPU is an RTX 3050 laptop with 6 GB.

## 3. Research summary (what is standard, what is not)

**Data.** FOLD (`.fold`, JSON; edemaine/fold) is the shared interchange format: vertices/edges/faces, `edges_assignment`
(M/V/B/F/...), `edges_foldAngle` (-180..180), `faceOrders`, and multi-frame files with `frame_classes` "diagrams" /
"animation". Origami Simulator, ORIPA and Oriedita all read/write it.
**Diagram language.** Yoshizawa-Randlett (valley dashed, mountain dash-dot, arrows) is the universal notation for step
diagrams. Valen's sheets are not diagrams, they are *footage of paper*; we borrow the logic of the steps, not the symbols.
**Simulation.** Origami Simulator (MIT, WebGL/three.js) folds *all* creases of a pattern at once with a Fold Percent
slider and exports OBJ/STL/FOLD and GIF/WebM; Oriedita/ORIPA compute flat-folded forms from crease patterns.
**Sequences.** Generating a clean step sequence from a crease pattern is research-grade (graph rewriting, Akitaya et al.
2013; particle swarm, Bui et al.; a 2026 learned model, Learn2Fold) and works only on restricted classes. **Conclusion: the
choice of key states must stay human (Diego's math); machines do the in-betweening and the painting.**
**Generative art.** The usual recipe is style LoRA + ControlNet (canny/depth/pose) + IP-Adapter, and for in-betweening a
first/last-frame video model (Wan FLF2V, Apache 2.0). Weak points here: frame-to-frame consistency, and a style dataset of
only ~42 frames (sources suggest 30-120 images for a style LoRA, with overfitting risk below that).
**Law (not legal advice).** Origami **diagrams are copyrighted even when free to download**; whether a fold sequence itself
is protected is unsettled; **traditional models are free to use**, modern designs need the designer's permission for
commercial use; some "traditional" labels hide a known author. Crease-pattern collections often carry NC licences (Mitani's
site is CC BY-NC vs CC BY, conflicting; Vigo's is CC BY-NC-SA). Steam also asks for disclosure of AI-generated content
(from memory of the policy; verify before shipping).

## 4. Proposed approach: deterministic geometry + Valen's own materials

Generative AI is the wrong *primary* tool: a child must recognise each fold, geometry has to be exact, and 6 frames x 4 steps
must agree with each other. So:

```
 blueprint (Diego's key states)  ->  fold engine  ->  style kit render  ->  sheet pack  ->  Unity import
   <=4 macro-steps, JSON/FOLD       frames per step    Valen's textures     2100x1400 +      OSU prefab,
                                    (geometry only)    + shading ramps      route geometry   Origami asset
```

**Three engine options, in recommended order:**

- **A. 2.5D polygon-layer engine (own code, Python).** The model is a stack of polygons; a fold splits a polygon on a line and
  rotates one part by theta (0..180) with foreshortening and mild perspective, which is exactly what Valen paints for single
  folds. Deterministic, headless, tiny, and it can *also* emit the drag route (grab point -> landing point). Weak at 3D
  inflation (box, boat, tulip bulb) and compound moves (petal, sink).
- **B. 3D bar-and-hinge simulation -> render.** Origami Simulator (MIT, fork it) or a Blender rig renders 3D states, then a
  flat Valen-material pass. Handles box/boat/flower. Costs: Blender is not installed, per-step crease grouping needs either a
  fork (to verify: its page documents Fold Percent, not per-crease target angles) or a different solver.
- **C. Generative stylisation (optional, off by default).** A low-denoise img2img/ControlNet pass over A/B renders, or FLF2V
  in-betweens between keyframes. Only if A/B output fails Valen's bar, only on a model Valen approves, and with the Steam
  disclosure and studio-values question settled first (open question 3). The local GPU (6 GB) rules out the big video models.

**The Valen Style Kit** (the heart of priority 1): measured from the existing sheets and stored as data + textures: paper
grain texture, face/back/border colours and border width, reverse-side translucency, flap gradient ramps, corner-triangle
shading as a function of fold angle, frame layout and margins. Valen can adjust the kit; every model inherits the change.

## 5. The "fewer than 5 steps" abstraction

A game step = **one drag = one continuous transformation between two key states**, not one physical fold. Reduce a traditional
sequence (crane is ~7 diagram steps even in a *simplified* version) with four rules, all in Diego's hands:

1. **Pre-creases vanish.** Fold-and-unfold prep is invisible; only the base state is a key state.
2. **Symmetric moves merge.** Left and right folds are one step; the slider drives both.
3. **Compound moves are one step.** Squash, petal, reverse fold, sink: the engine interpolates the transition.
4. **Finishing details ride along.** Head, wing tips, beak: folded into the last step's final frames or dropped. The silhouette,
   not the procedure, must stay recognisable.

Tooling needed: a timeline view of the full "honest" sequence where Diego marks the key states to keep (<=4 transitions) and
sees each result as a silhouette strip. Auto-selection is a later nice-to-have (silhouette-complexity heuristics), not v1.

## 6. Blueprint sourcing

"Acquire blueprints from the internet" is fine as **research input**, not as source material to redraw. Recommendation:

- Ship only **traditional models** (crane, masu box, boat, tulip) and **Diego's own designs**. The dragon and probably the
  heart must be *original simplified designs* made by Diego (a valid creative move: it is his abstraction); a real modern
  dragon (e.g. any named designer's) needs permission.
- Record provenance per model in its blueprint file (`source`, `license`, `traditional: true/false`).
- Never scrape or redistribute other people's diagrams or crease patterns; check licences before even using a collection as
  reference, since several are non-commercial and the game is a commercial Steam release.

## 7. Output contract (what the factory hands the game)

Per origami an **Origami Pack**: `manifest.json` (model, steps, provenance, paper cost) + per step a sheet PNG (same
2100x1400 / 6 x 700 layout, so existing Flipbook assumptions hold) + route geometry (start, end, corridor in normalized
coordinates) + a finished-figure icon. A Unity **Editor importer** turns a pack into: sprite slicing, `.anim`/`.controller`,
Flipbook prefab, OSU prefab and an `Origami` prefab (reusing `OrigamiRoute`/`MultipleRectCheck` untouched). Recommendation:
C# Editor importer using `PrefabUtility` (avoids hand-computed nested-prefab ids); YAML scripts are the fallback.

## 8. Phases (each ends with something Diego can look at)

0. **Style Kit + baseline test.** Measure the 6 existing sheets, regenerate `OrigamiSheet Puente` procedurally, and A/B it
   against Valen's (blind). Gate: Valen cannot tell, or tells us exactly what to tune. *This decides whether the whole project
   is viable.*
1. **Engine A spike.** Reproduce Avion A+B (2 steps) and Escalera (3 steps) with route geometry; compare to hand-made ones.
2. **Abstraction tool + 3 models.** Timeline/key-state marker; crane, masu box, boat; decide whether engine B is needed (box and
   boat probably yes).
3. **Unity importer + one playable origami** end to end, in a scene, played by Diego.
4. **Rest of the roster** (heart, flower, original dragon). Optional C evaluated only here, if at all.

Done when: Valen has approved every shipped model, each has <=4 steps, a new model is added from a blueprint file with no
Unity hand-work, and rebuilding from the blueprint reproduces the same assets.

## 9. Goals, non-goals, anti-goals

- **Goals:** Valen-identical look; <=4 steps; reproducible from a source file; Diego authors the math, machines paint.
- **Non-goals:** Auto-solving fold sequences; general origami simulator; replacing Valen's existing six sheets; new gameplay
  for the drag mechanic (curved routes, multi-touch).
- **Anti-goals (would make it a failure even if it "works"):** a model that Valen would redraw; shipped art with unclear
  copyright; AI frames in the game that the team did not decide to ship; a pipeline only Claude can run.

## 10. Skills and repos we might need

Nothing off the shelf turns origami into game sprites, so the useful skills are **project-local** (to write with
`skill-creator` once Phase 1 works): `origami-factory` (runbook) and `valen-style-kit`. Relevant outside pieces, none installed:

| Need | Candidate | Notes |
|---|---|---|
| Fold data | [FOLD](https://github.com/edemaine/fold) | JSON, spec still evolving |
| Fold simulation | [Origami Simulator](https://github.com/amandaghassaei/OrigamiSimulator) (MIT) | all-creases-at-once; fork if per-step angles needed |
| Pattern editing / flat-fold check | [Oriedita](https://github.com/oriedita/oriedita), [ORIPA](https://github.com/oripa/oripa) | Java 17+, `.fold` export; Diego's authoring tool |
| Blender automation (engine B) | blender-mcp servers (e.g. sandraschi headless, cwahlfeldt script runner), [blender-origami-fold](https://github.com/Olliebrown/blender-origami-fold) | maintenance not verified; Blender must be installed |
| Generative (option C) | ComfyUI + community MCP servers, Wan FLF2V | optional; 6 GB GPU limits models |
| Packing/render (engine A) | Python + numpy + Pillow | already on this machine |

## 11. Open questions for Diego (a default for each)

1. **Hard cap:** "fewer than 5" = max 4 steps, or is 5 allowed for the crane? *Default: max 4.*
2. **Roster legality:** ship traditional models plus original Diego designs (dragon, heart). *Default: yes; no copied modern designs.*
3. **AI in the pipeline:** does the studio (and Valen) want any generative step at all, given Steam's disclosure? *Default: none in v1, C only as a Phase 4 experiment.*
4. **What does each origami do in the game?** (boat to ride, crane to glide, dragon to ...) The factory makes them foldable;
   each still needs an `Origami` subclass and a purpose. *Default: out of scope here, tracked separately.*
5. **Blender:** OK to install it (engine B), or stay Python-only and accept flatter 3D models? *Default: install for the Phase 2 spike.*
6. **Frame count:** keep 6 per step, or allow more for 3D moves? *Default: 6, as today.*

## 12. Not verified

Origami Simulator's per-crease angle support; maintenance and security of the Blender/ComfyUI MCP servers; Steam's current AI
disclosure wording; whether the small `Avion.png`/`Escalera.png`/`Puente.png` icons are the finished figures; every step count
quoted from beginner sites (rough, not authoritative). Nothing here was run.

## Sources

[FOLD spec](https://github.com/edemaine/fold/blob/main/doc/spec.md) ·
[Origami Simulator](https://apps.amandaghassaei.com/OrigamiSimulator/) ·
[Oriedita](https://github.com/oriedita/oriedita) ·
[Yoshizawa-Randlett system](https://en.wikipedia.org/wiki/Yoshizawa%E2%80%93Randlett_system) ·
[Lang: diagramming conventions](https://www.langorigami.com/article/origami-diagramming-conventions) ·
[Akitaya et al., folding sequences from crease patterns](https://cgg.cs.tsukuba.ac.jp/projects/2013/generation_diagrams/index.html) ·
[Learn2Fold](https://arxiv.org/pdf/2603.29585) ·
[Origami USA copyright FAQ](https://origamiusa.org/node/14840) ·
[Origami copyright guide](https://origamiexpressions.com/origami-copyright-how-to-play-nice) ·
[Mitani CP downloads](https://www.mitani.cs.tsukuba.ac.jp/en/cp_download.html) ·
[Sprite Sheet Diffusion](https://arxiv.org/html/2412.03685v2) ·
[Runware: game assets with ControlNet canny](https://runware.ai/docs/guides/game-assets-canny) ·
[Wan FLF2V](https://comfyui-wiki.com/en/models/wan/flf2v) ·
[blender-mcp (sandraschi)](https://github.com/sandraschi/blender-mcp) ·
[blender-origami-fold](https://github.com/Olliebrown/blender-origami-fold)
