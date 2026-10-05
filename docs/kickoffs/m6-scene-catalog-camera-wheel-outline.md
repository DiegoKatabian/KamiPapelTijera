We're working through **M6 — Polish, tech debt, audio and tooling** in KamiPapelTijera (Unity 2021.3, URP).
This session covers three issues, in order:
1. **#54**: close out the scene catalog. The code is done; one stale value and a playtest are left.
2. **#43**: the camera wheel and camera cycling.
3. **#36**: a **time-boxed spike**. Can Kami's Spine skeleton get the old outline that shows through walls? The approach is
   below. If the spike doesn't work, stop and report back. Don't dig deeper.

Out of scope (Diego said so): #35, #56, #32, #33, #53.

## Branch and state

- Base: `level2newspaper-pages-blocking-2` (Diego merges finished work there, never into `main`). Check `git status`
  first (it should be clean, at `7cfb96cb` or later).
- Never commit, push or open a PR without Diego's explicit permission, every time.
- GitHub: milestone M6 (number 9). Umbrella #58 covers #53, #54, #55 (closed) and #56.

## Read first

1. `docs/claude/nivel2-y-ui.md`: the "Cámara" and "CamWheelManager" sections. Also `docs/claude/cutscenes.md`, the
   gotcha about cutscene cameras not being `CameraMode`s (it cites #43).
2. `docs/claude/controles-y-gamepad.md`: "Fugas de foco" (why `FakeSelectButton` must never call `Select()`) and the
   camera bindings (middle click, L2).
3. `docs/claude/spine-kami.md`: how Kami renders (one `MeshRenderer` on "The Paper Model (SkeletonAnimation)", one
   submesh per atlas page, `Spine/Skeleton Fill`, the flip through `Skeleton.ScaleX`).
4. Issues #54, #43, #36 and the #58 umbrella, including their comments.
5. Code:
   - `Managers/CameraManager.cs`: `CameraMode`, `ToggleNextCamera`, the three `SetCamera` overloads,
     `OnCameraChange`.
   - `UI/CamWheelManager.cs` and `CamWheelButton`.
   - `Player/PlayerView.cs`, around line 227: who sets OrigamiCasting and ReceiveReward.
   - `Managers/Scenes/` (`GameScene`, `SceneCatalog`), `Managers/LevelManager.cs` (`GoToScene`, cheats F1/F2/F12).
   - `StoryboardCutsceneManager`.
   - The UnityFx outline package in `Library/PackageCache/com.unityfx.outline@0.8.5`: `OutlineRenderer.DrawRenderer`,
     `OutlineLayer`, `OutlineLayerCollection`, `OutlineRenderFlags`.
   - Its URP half in `Library/PackageCache/com.unityfx.outline.urp@0.5.0`: `OutlinePass`.

## 1. #54: scene catalog close-out (small)

- The code is complete. `SceneCatalog.asset` has all 5 rows, matching the 5 `GameScene` values, and the string
  overload is gone.
- Fix: `Assets/Scenes/Nivel1_EndCutscene.unity` line ~1054 still says `_sceneToLoadOnDialogueEnd: MainMenu`, a
  pre-enum string. Unity reads it as 0, which happens to be `GameScene.MainMenu`, so behaviour is right, but the YAML
  carries a dead string. Make it `0` (`Level2_EndCutscene.unity` already has `0`). Preserve the file's line endings.
- Also check Build Settings (`ProjectSettings/EditorBuildSettings.asset`). Is every catalog scene enabled? Which
  stale scenes are enabled and would ship in the build (`SampleScene`, `Nivel1_LaRural`,
  `Nivel1_LaRural SpineTest`)? **Report this, don't change it**: what goes in the build is Diego's call.
- Diego plays:
  - main menu → Level 1;
  - Level 1 end cutscene → main menu;
  - Level 2 catapult → `Level2_EndCutscene` → main menu;
  - cheats F1/F2/F12;
  - `Kami/Validate Scene Catalog` with no errors.
- When he confirms: close #54 and tick it in #58.

## 2. #43: camera wheel and cycling

What the code shows today (verify it):

- `CameraMode` is: CloseUp, OrigamiCasting, Normal, General, BookCenter, ReceiveReward. `_virtualCameras` is indexed
  by it. `ToggleNextCamera` (middle click / L2) cycles the **whole array**, so it lands on OrigamiCasting and
  ReceiveReward, which are game-driven (`PlayerView` sets them while casting and while receiving a reward).
- `CamWheelManager.ChangeCamera` maps cameras to wheel buttons by hand: 0→0, 2→1, 3→2, 4→3. Cases 1 and 5 are
  commented out.
- **A second bug the issue doesn't mention**: `ToggleNextCamera` raises `OnCameraChange` with the **camera** index,
  and `FakeSelectButton` treats it as a **button** index. So after cycling, the wheel highlights the wrong button
  (Normal = 2 lights button 2, which is General). Confirm it from each button's `OnClick` argument and the
  `GetComponentsInChildren` order in the scenes.
- `ToggleNextCamera` switches off `currentCamera - 1`, assuming that was the live camera. That breaks as soon as
  cycling skips entries, or after a `SetCamera` from game code. Switch off the camera that was actually live.
- `SetCamera(CameraMode)` from game code (page turns, the dam, origami, rewards) never raises `OnCameraChange`, so
  the wheel's highlight goes stale.

Shape of the fix: one source of truth for which modes the player can pick, and the button mapping derived from it,
not repeated in three places.
- Suggested approach: `CameraManager` knows which modes are internal. Cycling skips them. Each `CamWheelButton`
  declares the `CameraMode` it selects, filled in YAML from its current `OnClick` int, in every scene that has a
  wheel. The highlight then maps mode → button. A new `CameraMode` then needs its button and nothing else.
- Check which scenes have a wheel (Level 1, Level 2) and who else listens to `OnCameraChange` before changing what it
  carries.

## 3. #36: Kami's outline, a time-boxed spike

**Why it isn't "just assign the layer"** (checked 2026-10-05):

- The outline is the UnityFx.Outline URP package. `URP Asset Renderer.asset` has five `OutlineFeature`s. "OutlineFeature"
  (white, 3 px) and "OutlineFeature Black" target layers **Player (3) + Kami Outline (13)**. That worked when Kami was a 3D
  mesh. Her Spine skeleton ("The Paper Model (SkeletonAnimation)") sits on **Default (0)**, so today nothing outlines her.
- Moving the skeleton onto layer 13 would **not** work. The feature's layer-mask path draws with an `overrideMaterial`,
  which drops each renderer's own texture. Spine draws every body part as a textured quad with transparent pixels, so
  without that texture the mask is a pile of rectangles. With `_outlineMode: 0` there's no alpha test at all, and even
  with alpha testing on, that path never binds the renderer's atlas.
- **The route the spike tests** is the package's other path, the per-renderer one: `OutlineRenderer.DrawRenderer`.
  - It runs for objects added to an `OutlineLayerCollection`.
  - With `EnableAlphaTesting` (flag 4), it sets `_MainTex` from **each submesh's own material** and clips by alpha,
    so the mask is Kami's real silhouette (all attachments merged).
  - It uses the material's own `_Cutoff` when the shader has one (Spine's does).
  - Without `EnableDepthTesting` it ignores scene depth, so the outline also draws through walls, the way the 3D-era
    one did.
- Every `OutlineFeature` already references `Assets/URPSettings/OutlineLayerCollection.asset`. That asset has one
  empty layer, "Player" (red, 4 px, mode 0), and no script adds anything to it.

Spike steps:
1. A small component on Kami's skeleton object (`Kami.prefab`, so both levels get it). It adds that GameObject to a
   layer of the collection in `OnEnable` and removes it in `OnDisable`. The collection is a ScriptableObject asset,
   so don't leave dead references in it between plays.
   - Inspector fields: the collection, the layer index, and an on/off toggle.
   - Set the layer's mode to `EnableAlphaTesting`. The color and width are Diego's (question 3).
2. **Gotcha: all five features reference the same collection, so Kami would be outlined five times.** Keep the
   collection on the white "OutlineFeature" only, and clear `_outlineLayers` on the other four (YAML in
   `URP Asset Renderer.asset`).
   - That asset is shared by every scene. Nothing else uses the collection today (verify with grep).
   - The main menu's outline (KamiSitting, the old 3D model on layer 14) goes through the layer-mask path, so it
     must look the same. Diego checks.
3. Leave the first renderer feature, "OutlineRendererFeature" (script guid `9561aef1…`, missing from the project), alone.
   Report what it does in the console, if anything.
4. Diego looks at it:
   - does the outline hug the silhouette, not rectangles;
   - does it show through walls and page geometry;
   - does it follow the flip, the death anim and `RidingPage`;
   - does it disappear while `HiddenRenderers` hides Kami in the page 3 arrest;
   - does it look right next to her own white paper border.

If the outline traces rectangles, or the package fights Spine in a way that isn't a setting, **stop**. Write down what
was seen and the next option (a custom pass, or Spine's `SkeletonRenderTexture`) in #36, and leave it for later.
Don't build a custom renderer feature this session.

## Ask Diego before building (each has a recommended default)

1. **Branch**: a new branch `m6-camera-wheel-scene-catalog` from `level2newspaper-pages-blocking-2` HEAD, merged
   back there when he says. Default: yes.
2. **#43, cameras the player can't pick**: OrigamiCasting and ReceiveReward are internal. They have no wheel button,
   cycling skips them, and a cycle press while one is live is ignored (so L2 can't break the origami view).
   BookCenter stays pickable (it has a wheel button). Default: yes.
3. **#36, look**:
   - When it shows: always, which is what the 3D era did. "Only while hidden behind something" isn't something this
     package can do; it would need a custom pass, so it's a later job.
   - Color and width: white, 3 px, the 3D-era white feature. Tunable in the collection asset.
   - Default: always visible, white, 3 px, on in both levels, with the toggle on the component.
4. **#36, if the spike works**: ship it on in both levels now, or leave it built but off until art has seen it?
   Default: on.

## Decided rules: don't reopen these

- #35, #56, #32, #33 and #53 are out of scope.
- No Build Settings changes without Diego.
- No `Select()` to highlight anything (focus leak, see "Fugas de foco").
- Cutscene cameras never become `CameraMode`s.
- One writer of each scene at a time.

## Gotchas already paid for

- YAML surgery:
  - Read the whole file first and copy existing patterns.
  - Preserve line endings (some files are CRLF, some LF).
  - Count `--- !u!` blocks before and after, and check fileIDs are unique.
  - New `.meta` files: `python tools/make-meta.py`.
- `GameScene` and `CameraMode` values are serialized as ints: append only, never reorder.
- Deleting code: list what's inside the range before cutting (a deleted `Update()` compiles clean).
- Unity calls `OnEnable`/`OnDisable` by reflection, so a typo compiles clean and never runs. Check the method names.

## Working rules

- English for everything new and in replies to Diego. Leave existing Spanish alone, except a comment that becomes
  false.
- Comments explain the non-obvious why.
- Braces always.
- `[SerializeField] private` + `[Tooltip]` for every tunable.
- Guard clauses that warn.
- `Debug.Log($"[ClassName] ...")` at decision points.
- Do the mechanical work yourself (YAML, assets). Ask Diego about decisions, not labour.
- Triangulate the code first. If an issue doesn't match the code, stop and ask with a recommended default.

## Verification

- `python tools/compile-check.py` after each part. Baseline warnings: `JumpFloodOutlineRenderer` CS0162 and
  `HongueroTiburcioDialogueTrigger` CS0414. That is compilation only. Rendering, cameras and scene loads are Diego's
  playtest: say clearly what wasn't run.
- #43 playtest, in Level 1 and Level 2:
  - cycle with middle click and with L2: it never lands on origami or reward, and the wheel highlights the right
    button every time;
  - pick each button on the wheel;
  - turn a page (CloseUp → BookCenter → Normal): the wheel follows;
  - press L2 while folding an origami and while receiving a reward: nothing happens.

## Close-out

- Docs:
  - `docs/claude/nivel2-y-ui.md`: the Cámara and CamWheelManager sections.
  - `docs/claude/cutscenes.md`: its #43 note.
  - `docs/claude/spine-kami.md`: the outline, if it ships.
  - `CLAUDE.md`, only if something in it becomes false.
- Run `/graphify Assets/Scripts --update` from the repo root if a component was added. Then copy `graph.json`,
  `GRAPH_REPORT.md`, `graph.html` and `manifest.json` into `Assets/Scripts/graphify-out/`.
- Leave everything uncommitted. When Diego has played it and asks:
  - commit, push, and merge into `level2newspaper-pages-blocking-2`;
  - close #54 and #43 with a comment each;
  - comment on #36 with the spike's result, and close it if it shipped;
  - tick them in #58, and state #53's real status there: its 2026-09-11 comment says Tasks 9-10 landed in `72ab35e`,
    but no `ControlsDiagram` code exists in any branch, so only the `Accion` enum shipped.
