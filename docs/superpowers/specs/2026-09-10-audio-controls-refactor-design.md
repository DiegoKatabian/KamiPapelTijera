# Audio system, controls diagram and scene catalog — design

- **Date**: 2026-09-10
- **Branch**: `feature/audio-controls-refactor` (off `Level2_Newspaper`, base commit `93639df`)
- **Status**: approved design, not yet implemented
- **Language**: English (per the 2026-09-09 language rule — everything added to this repo is English)

## 1. Goals

Three independent changes, requested together because they can be built in parallel:

1. **Controls menu** — replace the plain "KEY - Action" text list in the Flap's Controls tab
   with a diagram of the keyboard / gamepad, with the mapped keys highlighted. The diagram art
   must be a sprite that Valentino can swap without touching code. Placeholder art until then.
2. **Scene catalog** — stop referring to scenes by raw string. One enum for scene identity, one
   asset that is the single source of truth for which `.unity` file each enum value points at.
3. **Audio** — replace the ~60 always-instantiated `AudioSource` children with a real pooled
   audio system driven by a data asset, then use it for two new sound features: dialogue SFX
   (pencil writing synced to the typewriter, optional per-line NPC sigh) and chickens cackling
   when the tree quest completes.

## 2. Non-goals

- **Rebinding / editing controls is explicitly OUT of this batch** (decided 2026-09-10). The
  Controls tab only *displays* the defaults. Rationale and the design constraints found while
  scoping it are recorded as a future issue in
  `specs/004-joystick-controls/pending-issues.md` (issue #41.15) so the analysis is not lost.
- No migration to the new Input System package.
- No re-authoring of existing localized copy; existing Spanish content stays as-is.
- No changes to Barquito pathfinding, quest logic, or Spine animation wiring.

## 3. Findings that shape the design (verified, not assumed)

| Finding | Evidence | Consequence |
|---|---|---|
| Scene names are referenced from code only — no scene name is wired into a UnityEvent argument in any prefab or scene | `grep "m_MethodName: GoToScene"` and `m_StringArgument` across `Assets/**` → zero hits outside Spine examples | The scene refactor is a code-only change, ~6 call sites. Much cheaper than it looks. |
| Three *serialized* scene strings exist | `LevelManager.prefab:47-48`, `MainMenu.unity:5419`, `Nivel1_EndCutscene.unity:1054` | Changing field types loses these values, so Diego re-picks 2 dropdowns (§8). |
| One of them is already stale | `MainMenu.unity:14129` overrides `Level1SceneName` to `Nivel1_LaRural SpineTest`, the dead scene | Concrete justification for the refactor. Dies with the migration. |
| `UnityEditor.SceneAsset` can be dragged into a runtime ScriptableObject | **Spike compiled OK**: `#if UNITY_EDITOR [SerializeField] UnityEditor.SceneAsset` plus `ISerializationCallbackReceiver` baking the path, compiled via `tools/compile-check.py`. `Assembly-CSharp.rsp` defines `UNITY_EDITOR` and references `UnityEditor.dll` | The catalog is rename-proof. Spike deleted after verification. |
| The `AudioManager` prefab already holds every clip, volume, pitch and loop value across ~60 `AudioSource` children | `grep -c AudioSource Assets/Prefabs/AudioManager.prefab` → 61 | The bank asset can be **generated** from the prefab. No hand-typing, and parity with today's mix is mechanical rather than a judgement call. |
| 121 `AudioManager.instance.*` call sites across 45 files, distributed by folder | `Managers` 31, `Cortables` 21, `UI` 17, `Player` 16, `Origami` 11, `Enemies` 7, `TriggerS` 6, `Dialogos` 6, tail 6 | The call-site sweep partitions cleanly by folder: one agent per folder, no two agents in one file. |
| There is no `AudioMixer` asset anywhere in the project | `find Assets -name "*.mixer"` → empty | Buses are new. `AudioMixerController` is internal, so the asset cannot be reliably created from script → Diego creates it (§8), and code treats a null group as "no mixer yet". |
| The dialogue typewriter already exists and reveals via `maxVisibleCharacters` | `DialogueManager.EjecutarTypewriter` (issue #41.9) | Pencil SFX hooks into one existing loop; no text or localization changes. |
| `DialogueEvent` is a 3-field struct (`text`, `sprite`, `speakerName`) | `Assets/Scripts/Dialogos/DialogueSO.cs` | Adding an optional sigh field is additive; existing dialogue assets keep working with it empty. |
| `InputPromptSystem` already has an `Accion` enum covering exactly the player-facing actions | `InputPromptSystem.cs:32-42` | Reuse it as the diagram's action identity. No new taxonomy. |
| `InputHub` exposes `*Down` / `*Up` edges but almost no *held* state | `grep "public static"` on `InputHub.cs` — only `AccionGamepadHeld` exists | Live highlight needs new held-state properties (§4.5). |
| `GallinaSounds` uses hand-wired per-prefab `AudioSource` fields | `Assets/Scripts/Enemies/GallinaSounds.cs` | Migrating it to the pool removes that wiring. Diego approved editing `Gallina.prefab`. |

## 4. Workstream A — Controls diagram

### 4.1 Structure

`ControlsDiagram.cs` (`Assets/Scripts/UI/`) sits on the existing `ControlsDisplay` GameObject in
`Assets/Prefabs/UI/FlapManager.prefab` and **builds its hierarchy by code** at runtime: one
`Image` for the device diagram, plus one hotspot overlay per mapped control.

Building by code (rather than authoring ~18 GameObjects) is the established precedent in this
project — `GamepadCursor` and `IconosDeBoton` both do it, for the stated reason that everything
must work on Play without wiring prefabs. It also avoids blind YAML surgery on `FlapManager.prefab`.

The old localized text block stays in the tables (`controlsText` in `UITexts` is not deleted, just
no longer displayed) so nothing that might reference it breaks, and the list remains available if
the diagram ever needs a fallback.

### 4.2 Data: `ControlsDiagramSettings`

A `ScriptableObject` in `Resources/`, self-loading with a fallback if the asset is missing — the
exact pattern `TextHighlightSettings` established, so nothing needs to be wired:

```csharp
[CreateAssetMenu(menuName = "Kami/Controls Diagram Settings")]
public class ControlsDiagramSettings : ScriptableObject
{
    public Sprite keyboardDiagram;   // Valentino drops final art here
    public Sprite gamepadDiagram;
    public ControlHotspot[] keyboardHotspots;
    public ControlHotspot[] gamepadHotspots;
    public Color idleTint, mappedTint, pressedTint;
    public float pressedPulseSpeed;
}

[Serializable]
public struct ControlHotspot
{
    public Accion action;         // reuses InputPromptSystem.Accion
    public Rect normalizedRect;   // 0..1 over the diagram sprite
    public string labelKey;       // UITexts key for the action label
    public int paletteColorIndex; // reuses the ResaltadorDeConceptos palette
}
```

**Decision (rects in the asset, not draggable GameObjects):** hotspot positions are numbers in the
asset. The alternative — one GameObject per hotspot, positioned visually — is friendlier for
Valentino but needs ~18 GameObjects created by an editor tool and re-tuned anyway once real art
lands. Start with numbers; add visual authoring later only if it proves annoying. Because the rects
are *normalized*, replacing the sprite with art of a different resolution does not invalidate them
— only a different *layout* does.

`Accion` must become accessible to `ControlsDiagram`: it is currently a private nested enum in
`InputPromptSystem`. Promote it to a top-level `public enum Accion` in the `Input` folder and
update `InputPromptSystem`'s references. Rename only, no behaviour change.

### 4.3 Placeholder art

If a sprite field is empty, `ControlsDiagram` draws a procedural placeholder texture (labelled key
rectangles for the keyboard, a gamepad silhouette with lettered buttons) and logs
`[ControlsDiagram]` once. Consequences: the tab is readable and self-documenting today, the
placeholder disappears the moment art is assigned, and there is no binary placeholder asset to keep
in sync.

### 4.4 Device switching

Subscribe to `InputHub.OnDeviceCambio`; show the gamepad diagram when
`InputHub.UltimoDeviceFueJoystick`, the keyboard one otherwise. Same listener pattern as
`SoloConJoystick`. Both hotspot sets are built once and toggled, not rebuilt per switch.

### 4.5 Live input-reactive highlight

Each hotspot has three visual states: **idle** (no mapping), **mapped** (permanent colour plus
label — this is the "resaltadito"), and **pressed** (brighter, pulsing) while the real control is
held.

This needs held-state on `InputHub`, which mostly does not exist yet. Add read-only properties
built on the existing `*Seguro` wrappers, so a missing axis in `InputManager.asset` still cannot
throw: `SaltoHeld`, `InteractHeld`, `AtaqueHeld`, `CorrerHeld`, `CamaraHeld`, `MenuHeld`,
`TabSiguienteHeld`, `TabAnteriorHeld`, plus `MoveAxisRaw` for the move hotspot. Purely additive —
no existing property changes.

Three things the implementation must respect:

- **`Time.timeScale` is 0 while the Flap is open** (`FlapManager.MoveFlap`). Any pulse animation
  uses `Time.unscaledDeltaTime`. `Input.GetKey` itself is unaffected by timeScale.
- **B / L1 / R1 / Start / Esc / O are also Flap navigation.** Holding them to see the highlight
  will also close the Flap or change tab, so their pressed state is a blink. Accepted, not a bug:
  the diagram only *observes* input and never consumes it, so it adds no arbitration problem of
  the #41.2 / #41.14 family.
- `Update()` runs only while the Controls tab is visible (gated in `OnEnable`/`OnDisable`, driven
  by the display being shown), so a keyboard-only session pays nothing.

### 4.6 Localization

New `UITexts` keys for the action labels, in **es / en / pt**: `Ctrl_Mover`, `Ctrl_Saltar`,
`Ctrl_Interactuar`, `Ctrl_Atacar`, `Ctrl_Correr`, `Ctrl_Camara`, `Ctrl_Menu`, `Ctrl_CambiarTab`,
`Ctrl_Mutear`. Labels go through `LocalizedText` like every other string in the project, so
`InputPromptSystem` processing and device-change rewriting come for free.

## 5. Workstream B — Scene catalog

### 5.1 Enum and asset

```csharp
public enum GameScene { MainMenu, Level1, Level1EndCutscene, Level2 }
```

`SceneCatalog` (ScriptableObject in `Resources/`) holds one row per enum value:

```csharp
[Serializable]
public class SceneCatalogEntry : ISerializationCallbackReceiver
{
    public GameScene scene;
#if UNITY_EDITOR
    [SerializeField] UnityEditor.SceneAsset sceneAsset;  // drag the .unity here
#endif
    [SerializeField, HideInInspector] string scenePath;  // baked on serialize
    [SerializeField, HideInInspector] string sceneName;
    public bool isCinematic;                             // replaces keyword sniffing
}
```

`OnBeforeSerialize` bakes `AssetDatabase.GetAssetPath(sceneAsset)` into `scenePath` / `sceneName`.
In a player build the editor-only field is gone and the baked strings are what ship. **Verified to
compile** — see §3.

Public API: `SceneCatalog.NameOf(GameScene)`, `PathOf(GameScene)`, `IsCinematic(GameScene)`,
`TryResolve(string sceneName, out GameScene)`. Every accessor logs an error and returns a safe
value if a row is missing, rather than throwing mid-scene-load.

### 5.2 Migration

- `LevelManager.GoToScene(GameScene)` becomes the real entry point. The `string` overload is kept
  `[Obsolete]` only while migrating and **deleted in the closing task** (dead code goes immediately,
  per this project's modus operandi).
- `LevelManager.Level1SceneName` / `Level2SceneName` public strings are deleted; the two `Start()`
  music branches compare against `SceneCatalog.NameOf(GameScene.Level1 / Level2)`.
- The three editor cheat literals (`"MainMenu"`, `"Nivel1_KamiPapelTijera"`, `"Level2_Newspaper"`)
  become enum values.
- `OverlayManager.cs:158` `"Nivel1_EndCutscene"` becomes `GameScene.Level1EndCutscene`.
- `MainMenuManager.sceneToLoadOnDialogueEnd` and
  `StoryboardCutsceneManager._sceneToLoadOnDialogueEnd` become `GameScene` fields. Unity cannot
  carry a string into an enum, so both need re-picking in the inspector (§8); both log a warning if
  left at the default rather than silently loading nothing.
- `ResaltadorDeConceptos.IsCinematicScene` keyword matching is replaced by
  `SceneCatalog.IsCinematic`, resolved through `TryResolve` on the active scene name. The keyword
  list stays as a fallback for scenes not in the catalog (test scenes), with a debug log.

### 5.3 Validator

An editor menu item (`Kami/Validate Scene Catalog`) plus `OnValidate` on the asset checks: every
enum value has exactly one row, every row has a non-null scene asset, and every scene is present
**and enabled** in `EditorBuildSettings`. Without this, a scene missing from Build Settings fails
only at build time or at runtime load.

## 6. Workstream C — Audio core

### 6.1 `AudioBank`

One `SoundEntry` per sound, keyed by the **same id as today's GameObject name**, so the call-site
migration is a mechanical string→const swap:

```csharp
[Serializable]
public class SoundEntry
{
    public string id;                 // e.g. "QuestCompleted02"
    public AudioClip[] clips;         // more than one = pick at random (retires PlayRandom)
    public float volume = 1f;
    public float pitch = 1f;
    public float pitchVariation = 0f;
    public bool loop;
    public AudioBus bus;              // Music / SFX / UI / Ambience
    public int maxSimultaneous = 4;   // 0 = unlimited
    public float spatialBlend;        // 0 = 2D (default), 1 = full 3D
    public int priority = 128;
    public bool interruptSelf;
}

public enum AudioBus { Music, SFX, UI, Ambience }
```

The bank also holds one `AudioMixerGroup` reference per bus. A null group means "no mixer yet" and
the pool falls back to applying volume in code — so the mixer can land after everything else
without blocking any task.

### 6.2 `AudioBankBuilder` (Editor)

A menu item that reads `Assets/Prefabs/AudioManager.prefab` via
`AssetDatabase.LoadAssetAtPath<GameObject>` plus `GetComponentsInChildren<AudioSource>(true)` and
writes one entry per source, carrying over clip, volume, pitch, loop and spatial blend. It must be
**idempotent and non-destructive**: re-running preserves hand-edited fields (bus, maxSimultaneous,
extra clips) for ids that already exist, only adds missing ones, and reports ids that exist in the
bank but no longer in the prefab. Bus is guessed from the id (`MemoFlora*` / `Bohren*` / `4S_*` /
`*Loop*` → Music, `Forest*` → Ambience, `PageTurn*` / `PickupSFX*` / `Action_Hover` → UI, rest →
SFX) and is expected to be reviewed by hand once.

### 6.3 Pooled `AudioManager`

Keeps the `AudioManager.instance` singleton and its `DontDestroyOnLoad` behaviour. Internals:

- A pool of N (default 24, inspector-tunable) `AudioSource` components created at runtime under the
  manager. Loops get a source held for their lifetime; one-shots return theirs on completion.
- `SoundHandle Play(string id)` — `SoundHandle` is a struct of `{ AudioSource, int generation }` so
  `Stop(handle)` can only ever stop the instance it was issued for, never a recycled source that
  now plays something else.
- `Play(id, pitch)`, `Play(id, centralPitch, variation)` — same semantics as today's overloads.
- `PlayAt(id, Vector3 position)` — spatial one-shot. New capability; used by the chickens.
- `StopById(id)` — stops every live instance of that id (matches today's `StopByName`).
- `StopAll()`, `MuteAll()`, `UnmuteAll()`, `SetGlobalVolume(float)`, `SetBusVolume(bus, float)`.
- `PlayOnEnd(idToEnd, idToPlay)` — preserved, reimplemented over handles.

Behaviour that disappears as a deliberate consequence: the `SetPitchToOriginal` /
`SetVolumeToOriginal` coroutines (a pooled source is reset on release, so nothing needs restoring),
the hardcoded `bgms` list (the Music bus replaces it), and the `originalVolumes` dictionary keyed by
`KeyValuePair<string, AudioSource>`.

`SetBGMVolumes` / `ResetBGMVolumes` keep their signatures (13 `FlapManager` call sites rely on them
for menu ducking) but become one Music-bus volume set.

The `PlayGallinaSound` / `Gallina_Evade_VolumeTest` slider-audition hack keeps its behaviour,
reimplemented over the new API.

### 6.4 `AudioId` codegen

An editor menu item generates `Assets/Scripts/Managers/AudioId.cs`:

```csharp
public static class AudioId
{
    public const string QuestCompleted02 = "QuestCompleted02";
    // ... one per bank entry
}
```

Call sites become `AudioManager.instance.Play(AudioId.QuestCompleted02)`. Deleting a sound from the
bank removes its const, so every call site fails to **compile** instead of failing silently at
runtime — which is the entire point of choosing this API shape. Ids that are not valid C#
identifiers are sanitised, with the original string preserved as the const's value.

### 6.5 Call-site migration

`PlayByName` / `StopByName` / `PlayRandom` are kept as `[Obsolete]` shims **for the duration of the
sweep only**, so the project compiles at every intermediate step. That is what makes running one
agent per folder safe. The closing task deletes the shims; any surviving caller then fails to
compile, which is the completeness check.

`PlayRandom("Pasos_Kami_01", ..., "_04")` collapses into a single bank entry with 4 clips, so those
call sites become one `Play(AudioId.Pasos_Kami)`. The individual ids stay in the bank until the
sweep proves nothing else references them.

### 6.6 Mixer

Four groups: Music, SFX, UI, Ambience. Created by hand (§8) because `AudioMixerController` is
internal and hand-writing the asset YAML is risky surgery for no benefit. Once the groups are
assigned in the bank, `FlapManager.SLIDER_Volumen` moves a bus instead of looping 60 volumes, and
menu ducking becomes a Music-bus volume change (or a snapshot) rather than the current manual list
walk.

## 7. Workstream D — Dialogue SFX and chickens

### 7.1 Pencil writing

Inside `DialogueManager.EjecutarTypewriter`, every `_charsPerPencilSound` revealed characters
(inspector-tunable, default 2) play the pencil entry — a bank entry holding several short clips with
pitch variation and `maxSimultaneous` 2–3, so it can never machine-gun regardless of typing speed.
Skip whitespace and punctuation so the rhythm reads as writing rather than a metronome. Nothing
plays when the line is skipped via `CompletarTypewriter`.

Placeholder clips: pitched `PaperCut01/02` plus `PaperFold01/02`. The bank entry is named for its
final purpose (`Dialogue_PencilWrite`), so swapping in real recordings is dragging files into that
entry.

### 7.2 Per-line NPC sigh

`DialogueEvent` gains one optional field:

```csharp
[SoundId] public string sighSound;   // empty = no sigh
```

Played once as the line starts revealing. `SoundIdAttribute` plus a `PropertyDrawer` turn any such
string field into a **dropdown of bank ids**, so it is pickable in the inspector rather than typed.
That drawer is reusable anywhere a sound gets configured in an asset, which is why it earns its one
small editor script.

Because the field is additive on a struct, every existing `DialogueSO` keeps working with it empty.

### 7.3 Chickens cackling

`OnTreeCutForChickens` already reaches every `GallinaAgent`, so each chicken cackles **from its own
position** via `PlayAt`, each with a small random delay (0–0.6s) so they overlap like a real coop
instead of one flat 2D jingle. Placeholder clip: `Gallina_Evade` with pitch variation, in a bank
entry named `Gallina_Cacareo`.

Diego approved editing `Gallina.prefab`, so `GallinaSounds` also migrates to the pool: its three
hand-wired `AudioSource` fields (`_gallinaEvade`, `_gallinaCortada`, `_gallinaPasosArray`) are
replaced by `PlayAt` calls and the `AudioSource` components come off the prefab. The class keeps its
public method names (`PlayEvadeSound`, `PlayCortadaSound`, `PlayPasosSound`) because animation
events and other callers reference them by name.

## 8. Manual steps for Diego (nothing else blocks on these)

1. **Create the mixer**: Create → Audio Mixer named `KamiMixer` in `Assets/Audio/`, add groups
   `Music`, `SFX`, `UI`, `Ambience`. Then drag the four groups into the `AudioBank` asset. Until
   this happens the game sounds exactly as before (code-side volume fallback).
2. **Re-pick two scene dropdowns** (string→enum cannot migrate): `MainMenuManager` in
   `MainMenu.unity` → `Level1`; `StoryboardCutsceneManager` in `Nivel1_EndCutscene.unity` →
   `MainMenu`. Both log a warning if left unset. This is also what kills the stale
   `Nivel1_LaRural SpineTest` override.
3. **Review the bank's bus column** once after generation — the builder guesses from the id.
4. **Retire the `AudioSource` children** of `AudioManager.prefab` once the sweep is verified in the
   editor (they are kept until then as the rollback path).

## 9. Verification

- **Per task**: `python tools/compile-check.py <tag>` — the tag makes parallel runs safe. Known
  baseline warnings: `JumpFloodOutlineRenderer` CS0162, `HongueroTiburcioDialogueTrigger` CS0414,
  and `LightCycler` CS0109 (present on this branch before any of this work; recorded here because
  the existing docs list only the first two).
- **Completeness check for the audio sweep**: deleting the `[Obsolete]` shims must leave the project
  compiling. Any remaining caller surfaces as a compile error.
- **Runtime is Diego's**, in the editor. There is no test assembly in this repo (unlike hexwalls),
  so nothing here claims to be verified beyond compilation. Playtest checklist:
  - Controls tab: keyboard diagram with keyboard, gamepad diagram after touching the stick, keys
    light up while held, no stray highlight during gameplay.
  - Every sound still plays where it used to: footsteps (dry and wet), scissors hit and miss, page
    turn, jump and land, origami success and fail, dialogue advance, menu open and close, quest
    complete, death, level music on entering Level 1 and Level 2, and menu ducking on Flap open.
  - Dialogue: pencil rhythm feels like writing, skipping a line kills it cleanly, a sigh plays only
    on lines that have one.
  - Chickens cackle from their own positions when the tree is cut.
  - Scenes: main menu → Level 1, end cutscene → main menu, cheats F1 / F2 / F12.

## 10. Risks

| Risk | Mitigation |
|---|---|
| Audio parity regression across 121 call sites | The bank is *generated* from the prefab, so levels and pitches are carried over, not retyped. The prefab stays until Diego signs off, as the rollback path. |
| A sound is silently dropped during the sweep | Deleting the `[Obsolete]` shims turns any missed call site into a compile error. |
| Pool exhaustion under load (many footsteps plus combat) | `maxSimultaneous` per entry plus `priority`; the pool logs once when it has to steal a source, naming the id that lost. |
| `AudioId` const name collisions after sanitising | The generator fails loudly on a collision instead of emitting a broken file. |
| Hotspot rects drift from the real art | Rects are normalized, so only a layout change invalidates them; the placeholder documents the expected layout. |
| Scene enum re-serialization loses a value silently | Both affected scripts warn on the default value; the validator catches missing rows and Build Settings gaps. |

## 11. Deferred

- **#41.15 — editing / rebinding controls** (moved out 2026-09-10). The blocking constraint found
  while scoping it: the legacy Input Manager (`activeInputHandler: 0`, no `com.unity.inputsystem`)
  cannot be rebound at runtime, so rebinding requires `InputHub` to stop reading named axes and read
  a runtime bindings table instead. Notes worth keeping: `KeyCode` covers keyboard, mouse
  (`Mouse0/1/2`) and gamepad (`JoystickButton0..19`) in one binding type, so only the sticks and the
  L2 axis need the axis form; the `Accion` enum is already the right action set; UI Submit/Cancel are
  driven by `StandaloneInputModule` reading `InputManager.asset` directly, so they would not follow a
  rebind without a custom `BaseInput`; the defaults deliberately share buttons (A = jump + interact,
  L1 = run + tab-change) so any "one button, one action" rule would make the shipped defaults
  illegal; and hold-to-rebind collides with Flap navigation (holding B closes the menu), needing an
  explicit edit-mode gate of the #41.2 / #41.14 family. Full write-up in
  `specs/004-joystick-controls/pending-issues.md`.
- Visual (drag-a-GameObject) authoring for diagram hotspots, if numbers prove annoying.
- Migrating the origami minigame's direct `Input.GetMouseButton` reads behind `InputHub`.
