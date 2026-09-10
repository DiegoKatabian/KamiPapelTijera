# Audio System, Controls Diagram and Scene Catalog — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the string-keyed, 60-AudioSource audio manager with a pooled, data-driven audio system with a generated typed id API; replace the Controls tab text list with a device-aware diagram whose keys light up as you press them; and replace raw scene-name strings with an enum backed by one source-of-truth asset.

**Architecture:** Three independent workstreams over one shared rule — **one owner per file**. The audio refactor keeps `AudioManager.instance` and its method semantics so the 121 existing call sites keep compiling behind `[Obsolete]` shims while they migrate folder by folder; the shims are deleted last, turning any missed call site into a compile error. The controls diagram builds its hierarchy by code (the `GamepadCursor` / `IconosDeBoton` precedent in this repo) so nothing needs wiring in a prefab. The scene catalog uses an editor-only `SceneAsset` field that bakes a path string on serialize, so renaming a scene cannot break a reference.

**Tech Stack:** Unity 2021.3.12f1, URP, legacy Input Manager (`activeInputHandler: 0`), Unity Localization (`UITexts` / `DialogueTable` in es/en/pt), spine-unity 3.8. No test assembly.

**Spec:** `docs/superpowers/specs/2026-09-10-audio-controls-refactor-design.md` — read §3 (verified findings) before starting any task.

## Global Constraints

- **Everything added is in English** — code, comments, script names, asset names, new docs, log strings. Existing Spanish stays untouched unless a task says otherwise (2026-09-09 hard rule).
- **Never `git commit` without Diego's explicit permission.** Each task's "Commit" step means *stage and prepare the message*, then STOP and ask. Do not push, do not open PRs.
- **Verification is compile-only.** `python tools/compile-check.py <tag>` — always pass a unique `<tag>` so parallel agents do not collide. **There is no test assembly in this repo**, so no task in this plan writes an automated test; TDD is not available here. Runtime behaviour (feel, mix balance, input) is verified by Diego in the editor. Never report a task as working when only compilation was checked — say "compiles; runtime unverified".
- **Known baseline warnings** (not caused by this work, do not chase): `JumpFloodOutlineRenderer` CS0162, `HongueroTiburcioDialogueTrigger` CS0414, `LightCycler` CS0109.
- **Logging convention**: `Debug.Log($"[ClassName] message")`. Guard clauses use `Debug.LogWarning` when a reference that should be wired is missing — warn, never silently no-op.
- **Style**: braces always, explicit `break` in `switch`, `[SerializeField]` private + `[Tooltip]` on every tunable value, `PascalCase` types, `camelCase` fields.
- **Tunable design values live in an inspector-editable asset**, not hardcoded — follow `TextHighlightSettings` (self-loads via `Resources.Load`, works with a fallback if the asset is missing).
- **Never "fix" the Spine skeleton typos** (`NoScissortsOverride`, `tiejraBack`) if you pass near them.
- **New `.meta` files**: `python tools/make-meta.py <path...>` generates them with a collision-checked GUID. Run it for every new script and folder.
- **Unity is usually open** while this work happens. Prefer letting Unity generate `.meta` files for assets; use `make-meta.py` when it does not refresh.
- **Preserve each file's existing line endings** (some prefabs are CRLF, others LF).

## Orchestration

**One owner per file.** These files are touched by more than one workstream, so their tasks are ordered, never parallel:

| File | Owned by | Blocked until |
|---|---|---|
| `LevelManager.cs` | Task 4 (scene), then Task 20 (audio sweep) | 20 after 4 |
| `MainMenuManager.cs` | Task 4, then Task 20 | 20 after 4 |
| `OverlayManager.cs` | Task 4, then Task 16 | 16 after 4 |
| `StoryboardCutsceneManager.cs` | Task 4, then Task 20 | 20 after 4 |
| `FlapManager.cs` | Task 16 (audio sweep), then Task 12 (diagram wiring), then Task 23 (buses) | 12 after 16; 23 after 12 |
| `DialogueManager.cs` | Task 16, then Task 22 (pencil) | 22 after 16 |
| `GallinaSounds.cs` | Task 21 **only** — Task 17 must skip it | — |
| `InputHub.cs` / `InputPromptSystem.cs` | Task 1 only | — |

**Phases:**

- **Phase 0 — foundation (Tasks 1–8).** Tasks 1, 2, 5 start in parallel. Then 3 (needs 2), 4 (needs 3), 6 (needs 5), 7 (needs 6), 8 (needs 5+7).
- **Phase 1 — parallel fan-out (Tasks 9–20).** Diagram chain 9 → 10 → 12, and Task 11 independent. Audio sweep Tasks 13–20 all parallel with each other and with the diagram chain, subject to the ownership table.
- **Phase 2 — features and close-out (Tasks 21–25).** 21, 22, 23 parallel; then 24 (shim deletion, the completeness gate); then 25 (docs).

---

## File Structure

**New files:**

| Path | Responsibility |
|---|---|
| `Assets/Scripts/Input/Accion.cs` | The player-facing action enum, promoted out of `InputPromptSystem` |
| `Assets/Scripts/Managers/Scenes/GameScene.cs` | Scene identity enum |
| `Assets/Scripts/Managers/Scenes/SceneCatalog.cs` | ScriptableObject: enum → scene asset/name/path, plus lookups |
| `Assets/Editor/SceneCatalogValidator.cs` | Editor: rows complete, assets non-null, scenes enabled in Build Settings |
| `Assets/Resources/SceneCatalog.asset` | The single source of truth for scene names |
| `Assets/Scripts/Managers/Audio/AudioBus.cs` | Bus enum |
| `Assets/Scripts/Managers/Audio/SoundEntry.cs` | One sound's data (clips + playback config) |
| `Assets/Scripts/Managers/Audio/AudioBank.cs` | ScriptableObject: all `SoundEntry` rows + bus→mixer-group map + id lookup |
| `Assets/Scripts/Managers/Audio/SoundHandle.cs` | Generation-checked handle to one playing instance |
| `Assets/Scripts/Managers/Audio/AudioPool.cs` | Source pool: acquire, release, steal, per-id instance cap |
| `Assets/Scripts/Managers/Audio/SoundIdAttribute.cs` | Marker attribute for string fields that hold a bank id |
| `Assets/Editor/SoundIdDrawer.cs` | Editor: turns `[SoundId]` string fields into a bank-id dropdown |
| `Assets/Editor/AudioBankBuilder.cs` | Editor: generate/refresh the bank from `AudioManager.prefab` |
| `Assets/Editor/AudioIdGenerator.cs` | Editor: generate `AudioId.cs` from the bank |
| `Assets/Scripts/Managers/Audio/AudioId.cs` | **Generated.** One `const string` per bank entry |
| `Assets/Resources/AudioBank.asset` | The single source of truth for sound config |
| `Assets/Scripts/UI/ControlsDiagramSettings.cs` | ScriptableObject: diagram sprites + hotspot rects + colours |
| `Assets/Scripts/UI/ControlsDiagram.cs` | Builds the diagram by code, switches by device, highlights held controls |
| `Assets/Resources/ControlsDiagramSettings.asset` | Hotspot layout and placeholder-era defaults |

**Modified files:** `InputHub.cs` (held-state properties), `InputPromptSystem.cs` (enum moved out), `AudioManager.cs` (rewritten internals, same public surface), `LevelManager.cs`, `OverlayManager.cs`, `MainMenuManager.cs`, `StoryboardCutsceneManager.cs`, `ResaltadorDeConceptos.cs`, `DialogueSO.cs`, `DialogueManager.cs`, `GallinaAgent.cs`, `GallinaSounds.cs`, `FlapManager.cs`, the 45 audio call-site files, `Assets/Prefabs/UI/FlapManager.prefab`, `Assets/Prefabs/Gallina.prefab`, the three `UITexts_*.asset` tables.

---

# Phase 0 — Foundation

### Task 1: Promote `Accion` and add held-state input properties

**Files:**
- Create: `Assets/Scripts/Input/Accion.cs`
- Modify: `Assets/Scripts/Input/InputPromptSystem.cs:32-42` (delete the nested enum, keep every usage)
- Modify: `Assets/Scripts/Input/InputHub.cs` (append properties near the existing `AccionGamepadHeld`)

**Interfaces:**
- Consumes: nothing.
- Produces: `public enum Accion { Saltar, Interactuar, Atacar, Correr, Camara, Menu, Mover, CambiarTab }`; and on `InputHub`: `public static bool SaltoHeld`, `InteractHeld`, `AtaqueHeld`, `CorrerHeld`, `CamaraHeld`, `MenuHeld`, `TabSiguienteHeld`, `TabAnteriorHeld`, `public static Vector2 MoveAxisRaw`. Task 10 consumes all of these.

- [ ] **Step 1: Read the two files before touching them**

Read `Assets/Scripts/Input/InputHub.cs` in full (the `*Seguro` wrapper pattern and the axis-name constants matter) and `InputPromptSystem.cs:1-110`.

- [ ] **Step 2: Create the promoted enum**

`Assets/Scripts/Input/Accion.cs` — copy the members and their explanatory comments verbatim from `InputPromptSystem.cs:32-42` so no context is lost:

```csharp
/// <summary>
/// Player-facing actions, shared by the prompt system (which button icon a text shows) and the
/// controls diagram (which key to highlight). Promoted out of InputPromptSystem so more than one
/// system can name an action; the members and their meaning are unchanged.
/// </summary>
public enum Accion
{
    Saltar,
    Interactuar, //hablar, agarrar, pasar de pagina, avanzar el dialogo
    Atacar,      //cortar con la tijera
    Correr,
    Camara,
    Menu,
    Mover,
    CambiarTab   //L1/R1: ciclar secciones del Flap. SOLO joystick: estos ejes no tienen binding de teclado
}
```

- [ ] **Step 3: Delete the nested enum**

Remove lines 32-42 of `InputPromptSystem.cs`. Every existing `Accion.X` reference in that file resolves to the new top-level enum unchanged — do not rename any usage.

- [ ] **Step 4: Add held-state properties to `InputHub`**

Append inside the class, using the existing safe wrappers so a missing axis in `InputManager.asset` cannot throw (that is why `GetButtonSeguro` exists):

```csharp
// ---------------------------------------------------------------- held state
// The controls diagram needs "is this control held RIGHT NOW", which the rest of the game never
// needed (gameplay reads edges). Read-only and additive: no existing property changes behaviour.
// All of these go through the *Seguro wrappers, so an axis missing from InputManager.asset warns
// once and reads as "not pressed" instead of throwing every frame.

public static bool SaltoHeld => GetButtonSeguro(EJE_JUMP) || AccionGamepadHeld;
public static bool InteractHeld => GetButtonSeguro(EJE_INTERACT) || AccionGamepadHeld;
public static bool AtaqueHeld => GetButtonSeguro(EJE_FIRE1) || GetButtonSeguro(EJE_ATAQUE_GAMEPAD);
public static bool CorrerHeld => GetButtonSeguro(EJE_RUN);
public static bool CamaraHeld => GetButtonSeguro(EJE_CAMARA) || GatilloIzquierdoHeld;
public static bool MenuHeld => GetButtonSeguro(EJE_OPCIONES);
public static bool TabSiguienteHeld => GetButtonSeguro(EJE_TAB_SIGUIENTE);
public static bool TabAnteriorHeld => GetButtonSeguro(EJE_TAB_ANTERIOR);

/// <summary>Raw movement for the diagram's move hotspot. Same axes as MovimientoRaw.</summary>
public static Vector2 MoveAxisRaw => MovimientoRaw;
```

Two things to resolve while implementing, by reading the file rather than guessing:
- If `GetButtonSeguro` does not exist (only `GetButtonDownSeguro` / `GetButtonUpSeguro` do), add it following the identical try/catch + warn-once shape as its neighbours, wrapping `Input.GetButton`.
- `GatilloIzquierdoHeld`: L2 is an analog axis read with hysteresis. If only a `*Down` edge helper exists, add a held form that returns `axisValue > 0.3f` reusing the same axis constant and release threshold already in the file. Do **not** duplicate or alter the existing per-frame edge cache.

- [ ] **Step 5: Verify compilation**

Run: `python tools/compile-check.py task1`
Expected: `RESULTADO: COMPILA OK`, and only the three baseline warnings. `InputPromptSystem` must produce no new warnings — if it errors on `Accion`, the nested enum was left behind or a namespace was introduced (do not add a namespace; nothing in this project uses one).

- [ ] **Step 6: Generate the meta file**

Run: `python tools/make-meta.py Assets/Scripts/Input/Accion.cs`

- [ ] **Step 7: Stage and request permission to commit**

```bash
git add Assets/Scripts/Input/Accion.cs Assets/Scripts/Input/Accion.cs.meta Assets/Scripts/Input/InputHub.cs Assets/Scripts/Input/InputPromptSystem.cs
```

Proposed message: `refactor(input): promote Accion enum and add held-state properties`. **Ask Diego before committing.**

---

### Task 2: `GameScene` enum and `SceneCatalog` asset type

**Files:**
- Create: `Assets/Scripts/Managers/Scenes/GameScene.cs`
- Create: `Assets/Scripts/Managers/Scenes/SceneCatalog.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `enum GameScene { MainMenu, Level1, Level1EndCutscene, Level2 }`; `SceneCatalog.NameOf(GameScene) -> string`, `PathOf(GameScene) -> string`, `IsCinematic(GameScene) -> bool`, `TryResolve(string, out GameScene) -> bool`, `SceneCatalog.Instance -> SceneCatalog`, and `SceneCatalog.Entries -> IReadOnlyList<SceneCatalogEntry>` with `entry.scene`, `entry.SceneName`, `entry.ScenePath`, `entry.isCinematic`, plus editor-only `entry.HasSceneAsset`. Tasks 3 and 4 consume these.

- [ ] **Step 1: Create the enum**

```csharp
/// <summary>
/// Every scene the game can load, by identity rather than by name. The mapping from a value here
/// to an actual .unity file lives in one place only: the SceneCatalog asset in Resources.
///
/// Adding a level: add the value here, then add its row in the catalog asset and enable the scene
/// in Build Settings. The validator (Kami/Validate Scene Catalog) checks all three.
/// </summary>
public enum GameScene
{
    MainMenu,
    Level1,
    Level1EndCutscene,
    Level2
}
```

- [ ] **Step 2: Create the catalog**

The `#if UNITY_EDITOR` + `ISerializationCallbackReceiver` shape below is **verified to compile in this project** (spec §3): `Assembly-CSharp` is built with `UNITY_EDITOR` defined and referencing `UnityEditor.dll`, so the drag field exists in the editor and vanishes from player builds, leaving the baked strings.

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SceneCatalogEntry : ISerializationCallbackReceiver
{
    public GameScene scene;

#if UNITY_EDITOR
    [Tooltip("Drag the .unity file here. The name and path below are baked from it on save, so " +
             "renaming the scene cannot break the reference.")]
    [SerializeField] UnityEditor.SceneAsset sceneAsset;
#endif

    //baked from sceneAsset in the editor; these are what ship in a player build
    [SerializeField, HideInInspector] string scenePath;
    [SerializeField, HideInInspector] string sceneName;

    [Tooltip("Cutscene/storyboard scene: keyword highlighting stays off and text renders plain.")]
    public bool isCinematic;

    public string ScenePath => scenePath;
    public string SceneName => sceneName;

#if UNITY_EDITOR
    public bool HasSceneAsset => sceneAsset != null;
#endif

    public void OnBeforeSerialize()
    {
#if UNITY_EDITOR
        if (sceneAsset != null)
        {
            scenePath = UnityEditor.AssetDatabase.GetAssetPath(sceneAsset);
            sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
        }
        else
        {
            scenePath = "";
            sceneName = "";
        }
#endif
    }

    public void OnAfterDeserialize() { }
}

/// <summary>
/// The single source of truth for which .unity file each GameScene value points at.
///
/// Self-loading from Resources, same pattern as TextHighlightSettings: nothing has to be wired in
/// any inspector, and a missing asset degrades to loud errors rather than a NullReference.
/// </summary>
[CreateAssetMenu(fileName = "SceneCatalog", menuName = "Kami/Scene Catalog")]
public class SceneCatalog : ScriptableObject
{
    const string RESOURCE_NAME = "SceneCatalog";

    [SerializeField] List<SceneCatalogEntry> entries = new List<SceneCatalogEntry>();

    static SceneCatalog _instance;
    static bool _missingReported;

    public IReadOnlyList<SceneCatalogEntry> Entries => entries;

    public static SceneCatalog Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<SceneCatalog>(RESOURCE_NAME);
                if (_instance == null && !_missingReported)
                {
                    _missingReported = true;
                    Debug.LogError($"[SceneCatalog] no encontre Resources/{RESOURCE_NAME}.asset: " +
                                   "no puedo resolver ningun nombre de escena. Crealo con " +
                                   "Create > Kami > Scene Catalog y ponelo en Assets/Resources.");
                }
            }
            return _instance;
        }
    }

    public static string NameOf(GameScene scene)
    {
        SceneCatalogEntry entry = Find(scene);
        if (entry == null || string.IsNullOrEmpty(entry.SceneName))
        {
            Debug.LogError($"[SceneCatalog] la escena '{scene}' no tiene fila valida en el catalogo. " +
                           "Corre Kami/Validate Scene Catalog.");
            return "";
        }
        return entry.SceneName;
    }

    public static string PathOf(GameScene scene)
    {
        SceneCatalogEntry entry = Find(scene);
        if (entry == null || string.IsNullOrEmpty(entry.ScenePath))
        {
            Debug.LogError($"[SceneCatalog] la escena '{scene}' no tiene path valido en el catalogo.");
            return "";
        }
        return entry.ScenePath;
    }

    public static bool IsCinematic(GameScene scene)
    {
        SceneCatalogEntry entry = Find(scene);
        return entry != null && entry.isCinematic;
    }

    /// <summary>
    /// Reverse lookup by loaded scene name. Returns false for scenes that are not in the catalog
    /// (test scenes, Spine sample scenes) so callers can fall back instead of erroring.
    /// </summary>
    public static bool TryResolve(string sceneName, out GameScene scene)
    {
        scene = default;
        if (string.IsNullOrEmpty(sceneName) || Instance == null)
        {
            return false;
        }

        for (int i = 0; i < Instance.entries.Count; i++)
        {
            if (Instance.entries[i].SceneName == sceneName)
            {
                scene = Instance.entries[i].scene;
                return true;
            }
        }
        return false;
    }

    static SceneCatalogEntry Find(GameScene scene)
    {
        if (Instance == null)
        {
            return null;
        }

        for (int i = 0; i < Instance.entries.Count; i++)
        {
            if (Instance.entries[i].scene == scene)
            {
                return Instance.entries[i];
            }
        }
        return null;
    }
}
```

- [ ] **Step 3: Verify compilation**

Run: `python tools/compile-check.py task2`
Expected: `RESULTADO: COMPILA OK`. If `UnityEditor.SceneAsset` is reported as not found, the `#if UNITY_EDITOR` guard is wrong or a namespace was added — re-read the spec §3 note.

- [ ] **Step 4: Generate meta files**

Run: `python tools/make-meta.py Assets/Scripts/Managers/Scenes Assets/Scripts/Managers/Scenes/GameScene.cs Assets/Scripts/Managers/Scenes/SceneCatalog.cs`

- [ ] **Step 5: Stage and request permission to commit**

```bash
git add Assets/Scripts/Managers/Scenes
```

Proposed message: `feat(scenes): add GameScene enum and SceneCatalog asset type`. **Ask Diego before committing.**

---

### Task 3: Scene catalog validator, and create the asset

**Files:**
- Create: `Assets/Editor/SceneCatalogValidator.cs`
- Create: `Assets/Resources/SceneCatalog.asset` (created through the Unity editor, not hand-written YAML)

**Interfaces:**
- Consumes: `GameScene`, `SceneCatalog` (Task 2).
- Produces: menu item `Kami/Validate Scene Catalog`; a populated `Resources/SceneCatalog.asset`. Task 4 consumes the asset.

- [ ] **Step 1: Write the validator**

```csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Checks the three things that can silently break a scene load, and that nothing else checks:
/// a GameScene value with no row, a row with no scene assigned, and a scene that exists but is
/// NOT enabled in Build Settings (which fails only at build time or at runtime load).
/// </summary>
public static class SceneCatalogValidator
{
    [MenuItem("Kami/Validate Scene Catalog")]
    public static void Validate()
    {
        SceneCatalog catalog = Resources.Load<SceneCatalog>("SceneCatalog");
        if (catalog == null)
        {
            Debug.LogError("[SceneCatalogValidator] no hay Resources/SceneCatalog.asset.");
            return;
        }

        int problems = 0;
        var seen = new Dictionary<GameScene, int>();

        foreach (SceneCatalogEntry entry in catalog.Entries)
        {
            if (seen.ContainsKey(entry.scene))
            {
                Debug.LogError($"[SceneCatalogValidator] '{entry.scene}' esta duplicada en el catalogo.");
                problems++;
                continue;
            }
            seen[entry.scene] = 1;

            if (!entry.HasSceneAsset || string.IsNullOrEmpty(entry.ScenePath))
            {
                Debug.LogError($"[SceneCatalogValidator] '{entry.scene}' no tiene escena asignada.");
                problems++;
                continue;
            }

            if (!IsEnabledInBuildSettings(entry.ScenePath))
            {
                Debug.LogError($"[SceneCatalogValidator] '{entry.scene}' ({entry.ScenePath}) no esta " +
                               "habilitada en Build Settings: va a fallar al cargarla en un build.");
                problems++;
            }
        }

        foreach (GameScene scene in System.Enum.GetValues(typeof(GameScene)))
        {
            if (!seen.ContainsKey(scene))
            {
                Debug.LogError($"[SceneCatalogValidator] falta la fila de '{scene}' en el catalogo.");
                problems++;
            }
        }

        if (problems == 0)
        {
            Debug.Log($"[SceneCatalogValidator] OK: {seen.Count} escenas, todas asignadas y en Build Settings.");
        }
    }

    static bool IsEnabledInBuildSettings(string scenePath)
    {
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.path == scenePath)
            {
                return scene.enabled;
            }
        }
        return false;
    }
}
```

- [ ] **Step 2: Verify compilation**

Run: `python tools/compile-check.py task3`
Expected: `RESULTADO: COMPILA OK`. Note this script lives in `Assets/Editor`, so it compiles into `Assembly-CSharp-Editor`, which `compile-check.py` does **not** cover — a clean result here does not prove this file compiles. Say so when reporting, and ask Diego to confirm the Console is clean after Unity reimports.

- [ ] **Step 3: Generate the meta file**

Run: `python tools/make-meta.py Assets/Editor/SceneCatalogValidator.cs`

- [ ] **Step 4: Hand the asset creation to Diego**

The `.asset` must be created through Unity so the `SceneAsset` object references serialize correctly — hand-writing that YAML is exactly the kind of surgery to avoid here. Ask Diego to:

1. Create → Kami → Scene Catalog, save as `Assets/Resources/SceneCatalog.asset`.
2. Add four rows and drag each `.unity` file in:
   - `MainMenu` → `Assets/Scenes/MainMenu.unity`
   - `Level1` → `Assets/Scenes/Nivel1_KamiPapelTijera.unity`
   - `Level1EndCutscene` → `Assets/Scenes/Nivel1_EndCutscene.unity`, tick **isCinematic**
   - `Level2` → `Assets/Scenes/Level2_Newspaper.unity`
3. Run `Kami/Validate Scene Catalog` and paste the Console output back.

Expected output: `[SceneCatalogValidator] OK: 4 escenas, todas asignadas y en Build Settings.` All four are already enabled in `EditorBuildSettings.asset`, so a Build Settings error here means something changed.

- [ ] **Step 5: Stage and request permission to commit**

```bash
git add Assets/Editor/SceneCatalogValidator.cs Assets/Editor/SceneCatalogValidator.cs.meta Assets/Resources/SceneCatalog.asset Assets/Resources/SceneCatalog.asset.meta
```

Proposed message: `feat(scenes): add scene catalog validator and catalog asset`. **Ask Diego before committing.**

---

### Task 4: Migrate every scene-name string to `GameScene`

**Files:**
- Modify: `Assets/Scripts/Managers/LevelManager.cs:25-26` (delete the two public strings), `:57-75` (the two `Start()` music branches), `:94-107` (three cheat literals), `:132-135` (`GoToScene`)
- Modify: `Assets/Scripts/UI/OverlayManager.cs:158`
- Modify: `Assets/Scripts/Managers/MainMenuManager.cs:13` and `:134`
- Modify: `Assets/Scripts/Managers/StoryboardCutsceneManager.cs:13` and `:65`
- Modify: `Assets/Scripts/UI/ResaltadorDeConceptos.cs:357-407`

**Interfaces:**
- Consumes: `SceneCatalog.NameOf`, `PathOf`, `IsCinematic`, `TryResolve`, `GameScene` (Tasks 2–3).
- Produces: `LevelManager.GoToScene(GameScene)`. Tasks 16 and 20 will edit these same files afterwards for audio — **they must not start until this task is committed.**

- [ ] **Step 1: Read all five files first**

Do not pattern-match on the line numbers above; they are a map, not a contract. Read each file and locate the real usages.

- [ ] **Step 2: Replace `GoToScene` in `LevelManager`**

```csharp
    /// <summary>
    /// The only way to change scenes. Takes an identity, not a name: the name comes from the
    /// SceneCatalog asset, so a renamed scene keeps working and a missing one errors loudly
    /// instead of loading nothing.
    /// </summary>
    public void GoToScene(GameScene scene)
    {
        string sceneName = SceneCatalog.NameOf(scene);
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError($"[LevelManager] no puedo cargar '{scene}': no esta en el catalogo.");
            return;
        }

        Debug.Log($"[LevelManager] cargo la escena '{sceneName}' ({scene})");
        SceneManager.LoadScene(sceneName);
    }
```

Delete the old `GoToScene(string)` entirely. It has no callers outside this migration (verified: no UnityEvent in any prefab or scene references it), so no `[Obsolete]` shim is needed here — unlike the audio API, which does need one.

- [ ] **Step 3: Delete the two public scene-name strings and fix the music branches**

Remove `public string Level1SceneName` and `Level2SceneName` (lines 25-26). In `Start()`, replace the comparisons:

```csharp
        if (gameObject.scene.name == SceneCatalog.NameOf(GameScene.Level1))
        {
            // ... existing audio calls unchanged; Task 20 migrates them
        }

        if (gameObject.scene.name == SceneCatalog.NameOf(GameScene.Level2))
        {
            // ... existing audio calls unchanged
        }
```

Leave every `AudioManager.instance.*` line exactly as it is — Task 20 owns those.

Note for the report: deleting these fields orphans the serialized overrides in `LevelManager.prefab:47-48` and `MainMenu.unity:14129`. That is intended — the `MainMenu.unity` one is the stale `Nivel1_LaRural SpineTest` value called out in spec §3. Unity drops unknown overrides on next save; nothing needs hand-editing.

- [ ] **Step 4: Replace the cheat literals**

```csharp
            if (Input.GetKeyDown(KeyCode.F12))
            {
                GoToScene(GameScene.MainMenu);
            }

            if (Input.GetKeyDown(KeyCode.F1))
            {
                GoToScene(GameScene.Level1);
            }

            if (Input.GetKeyDown(KeyCode.F2))
            {
                GoToScene(GameScene.Level2);
            }
```

- [ ] **Step 5: Fix `OverlayManager.cs:158`**

Replace `LevelManager.Instance.GoToScene("Nivel1_EndCutscene");` with `LevelManager.Instance.GoToScene(GameScene.Level1EndCutscene);`.

- [ ] **Step 6: Convert the two serialized scene fields**

In `MainMenuManager.cs`, replace line 13 and guard the use at line 134:

```csharp
    [SerializeField, Tooltip("Scene to load when the intro dialogue ends.")]
    GameScene _sceneToLoadOnDialogueEnd = GameScene.Level1;
```

```csharp
        LevelManager.Instance.GoToScene(_sceneToLoadOnDialogueEnd);
```

Do the same in `StoryboardCutsceneManager.cs` (field at line 13, use at line 65), defaulting to `GameScene.MainMenu`.

Both fields previously held a string, and Unity cannot carry a string into an enum, so the serialized values in `MainMenu.unity` and `Nivel1_EndCutscene.unity` are lost. The defaults above are deliberately set to the values those scenes had (`Nivel1_KamiPapelTijera` → `Level1`; `MainMenu` → `MainMenu`), so behaviour is preserved even if Diego never touches the inspector. Still list both in the report as things for him to confirm visually (spec §8.2).

- [ ] **Step 7: Make `ResaltadorDeConceptos` use the catalog**

Replace the keyword sniffing at `:357-366` with a catalog lookup that keeps the keyword list as a fallback for scenes not in the catalog:

```csharp
    static bool IsCinematicScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            return false;
        }

        //the catalog is authoritative for shipped scenes
        if (SceneCatalog.TryResolve(sceneName, out GameScene scene))
        {
            return SceneCatalog.IsCinematic(scene);
        }

        //not in the catalog: test scenes and Spine sample scenes still get the old keyword guess
        Debug.Log($"[ResaltadorDeConceptos] '{sceneName}' no esta en el catalogo, uso keywords.");
        for (int i = 0; i < _cinematicKeywords.Length; i++)
        {
            if (sceneName.IndexOf(_cinematicKeywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }
        return false;
    }
```

- [ ] **Step 8: Confirm no scene-name string survives**

Run:
```bash
grep -rnE '"(MainMenu|Nivel1[A-Za-z_ ]*|Level[0-9][A-Za-z_]*)[^"]*"' Assets/Scripts --include=*.cs
```
Expected: only the `_cinematicKeywords` fallback list in `ResaltadorDeConceptos.cs`. Any other hit is a missed call site.

- [ ] **Step 9: Verify compilation**

Run: `python tools/compile-check.py task4`
Expected: `RESULTADO: COMPILA OK` with only baseline warnings.

- [ ] **Step 10: Stage and request permission to commit**

```bash
git add Assets/Scripts/Managers/LevelManager.cs Assets/Scripts/UI/OverlayManager.cs Assets/Scripts/Managers/MainMenuManager.cs Assets/Scripts/Managers/StoryboardCutsceneManager.cs Assets/Scripts/UI/ResaltadorDeConceptos.cs
```

Proposed message: `refactor(scenes): replace scene-name strings with GameScene enum`. **Ask Diego before committing.** Tell him Tasks 16 and 20 are unblocked once this lands.

---

### Task 5: Audio data types

**Files:**
- Create: `Assets/Scripts/Managers/Audio/AudioBus.cs`, `SoundEntry.cs`, `AudioBank.cs`, `SoundHandle.cs`, `SoundIdAttribute.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `enum AudioBus { Music, SFX, UI, Ambience }`; `class SoundEntry` with public fields `id`, `clips`, `volume`, `pitch`, `pitchVariation`, `loop`, `bus`, `maxSimultaneous`, `spatialBlend`, `priority`, `interruptSelf`, plus `AudioClip PickClip()`; `AudioBank.Instance`, `AudioBank.TryGet(string id, out SoundEntry)`, `AudioBank.Entries`, `AudioBank.GroupFor(AudioBus)`, `AudioBank.Ids`; `struct SoundHandle` with `IsValid`; `class SoundIdAttribute : PropertyAttribute`. Tasks 6, 7, 8 and 19 consume these.

- [ ] **Step 1: `AudioBus.cs`**

```csharp
/// <summary>
/// Mixer routing groups. Each SoundEntry declares one; AudioBank maps each to an
/// AudioMixerGroup. A bus with no group assigned yet falls back to code-side volume, so the
/// mixer can be created after all the code lands (see the design doc, section 8).
/// </summary>
public enum AudioBus
{
    Music,
    SFX,
    UI,
    Ambience
}
```

- [ ] **Step 2: `SoundEntry.cs`**

```csharp
using System;
using UnityEngine;

/// <summary>
/// Everything about one sound: which clips it can use and how it plays. This replaces a child
/// GameObject with an AudioSource in AudioManager.prefab, which is why the field defaults mirror
/// Unity's AudioSource defaults -- AudioBankBuilder copies the prefab values over them.
/// </summary>
[Serializable]
public class SoundEntry
{
    [Tooltip("Id used from code, via the generated AudioId constants. Matches the old child " +
             "GameObject name in AudioManager.prefab, which is why the migration is mechanical.")]
    public string id;

    [Tooltip("More than one clip = a random one is picked per play. This replaces PlayRandom.")]
    public AudioClip[] clips = new AudioClip[0];

    [Range(0f, 1f)] public float volume = 1f;
    [Range(-3f, 3f)] public float pitch = 1f;

    [Tooltip("Random pitch spread around pitch, applied per play. 0 = always exactly pitch.")]
    [Range(0f, 1f)] public float pitchVariation = 0f;

    public bool loop;
    public AudioBus bus = AudioBus.SFX;

    [Tooltip("Max instances of THIS id playing at once. 0 = unlimited. Keeps footsteps and " +
             "rapid-fire UI sounds from stacking into noise.")]
    public int maxSimultaneous = 4;

    [Tooltip("0 = 2D (the default for almost everything here), 1 = fully positional.")]
    [Range(0f, 1f)] public float spatialBlend = 0f;

    [Range(0, 256)] public int priority = 128;

    [Tooltip("If already at maxSimultaneous, stop the oldest instance of this id instead of " +
             "dropping the new play.")]
    public bool interruptSelf;

    /// <summary>Random clip, or null (with no log -- the caller reports) if there are none.</summary>
    public AudioClip PickClip()
    {
        if (clips == null || clips.Length == 0)
        {
            return null;
        }
        if (clips.Length == 1)
        {
            return clips[0];
        }
        return clips[UnityEngine.Random.Range(0, clips.Length)];
    }
}
```

- [ ] **Step 3: `SoundHandle.cs`**

```csharp
using UnityEngine;

/// <summary>
/// A reference to one playing instance. The generation counter is the point: pooled sources get
/// reused, so a stale handle held by some coroutine must NOT be able to stop whatever is playing
/// on that source now. AudioPool bumps the generation on every acquire.
/// </summary>
public struct SoundHandle
{
    public readonly AudioSource source;
    public readonly int generation;

    public SoundHandle(AudioSource source, int generation)
    {
        this.source = source;
        this.generation = generation;
    }

    public bool IsValid => source != null;

    public static SoundHandle None => new SoundHandle(null, 0);
}
```

- [ ] **Step 4: `SoundIdAttribute.cs`**

```csharp
using UnityEngine;

/// <summary>
/// Marks a string field as holding an AudioBank id, so the inspector shows a dropdown of real
/// ids instead of a free-text box (see SoundIdDrawer). Used by DialogueEvent.sighSound.
/// </summary>
public class SoundIdAttribute : PropertyAttribute { }
```

- [ ] **Step 5: `AudioBank.cs`**

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// The single source of truth for sound configuration. Self-loading from Resources (same pattern
/// as TextHighlightSettings) so nothing needs wiring, and generated from AudioManager.prefab by
/// AudioBankBuilder so the values match what the game shipped with.
/// </summary>
[CreateAssetMenu(fileName = "AudioBank", menuName = "Kami/Audio Bank")]
public class AudioBank : ScriptableObject
{
    const string RESOURCE_NAME = "AudioBank";

    [SerializeField] List<SoundEntry> entries = new List<SoundEntry>();

    [Header("Mixer routing (optional until the mixer exists)")]
    [SerializeField] AudioMixerGroup musicGroup;
    [SerializeField] AudioMixerGroup sfxGroup;
    [SerializeField] AudioMixerGroup uiGroup;
    [SerializeField] AudioMixerGroup ambienceGroup;

    static AudioBank _instance;
    static bool _missingReported;
    Dictionary<string, SoundEntry> _byId;

    public IReadOnlyList<SoundEntry> Entries => entries;

    public static AudioBank Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<AudioBank>(RESOURCE_NAME);
                if (_instance == null && !_missingReported)
                {
                    _missingReported = true;
                    Debug.LogError($"[AudioBank] no encontre Resources/{RESOURCE_NAME}.asset: " +
                                   "no va a sonar nada. Generalo con Kami/Audio/Rebuild Audio Bank.");
                }
            }
            return _instance;
        }
    }

    public IEnumerable<string> Ids
    {
        get
        {
            for (int i = 0; i < entries.Count; i++)
            {
                yield return entries[i].id;
            }
        }
    }

    public bool TryGet(string id, out SoundEntry entry)
    {
        if (_byId == null)
        {
            BuildIndex();
        }
        return _byId.TryGetValue(id ?? "", out entry);
    }

    public AudioMixerGroup GroupFor(AudioBus bus)
    {
        switch (bus)
        {
            case AudioBus.Music: return musicGroup;
            case AudioBus.SFX: return sfxGroup;
            case AudioBus.UI: return uiGroup;
            case AudioBus.Ambience: return ambienceGroup;
            default: return null;
        }
    }

    void BuildIndex()
    {
        _byId = new Dictionary<string, SoundEntry>(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            SoundEntry entry = entries[i];
            if (string.IsNullOrEmpty(entry.id))
            {
                Debug.LogWarning($"[AudioBank] la fila {i} no tiene id, la ignoro.");
                continue;
            }
            if (_byId.ContainsKey(entry.id))
            {
                Debug.LogWarning($"[AudioBank] id duplicado '{entry.id}', me quedo con el primero.");
                continue;
            }
            _byId[entry.id] = entry;
        }
    }

    void OnValidate()
    {
        _byId = null; //force a rebuild after an inspector edit
    }

#if UNITY_EDITOR
    /// <summary>Editor-only mutation used by AudioBankBuilder. Never call at runtime.</summary>
    public List<SoundEntry> EditorEntries => entries;
#endif
}
```

- [ ] **Step 6: Verify compilation**

Run: `python tools/compile-check.py task5`
Expected: `RESULTADO: COMPILA OK`.

- [ ] **Step 7: Generate meta files**

Run: `python tools/make-meta.py Assets/Scripts/Managers/Audio Assets/Scripts/Managers/Audio/AudioBus.cs Assets/Scripts/Managers/Audio/SoundEntry.cs Assets/Scripts/Managers/Audio/AudioBank.cs Assets/Scripts/Managers/Audio/SoundHandle.cs Assets/Scripts/Managers/Audio/SoundIdAttribute.cs`

- [ ] **Step 8: Stage and request permission to commit**

```bash
git add Assets/Scripts/Managers/Audio
```

Proposed message: `feat(audio): add AudioBank data types`. **Ask Diego before committing.**

---

### Task 6: `AudioBankBuilder` and generate the bank

**Files:**
- Create: `Assets/Editor/AudioBankBuilder.cs`
- Create: `Assets/Resources/AudioBank.asset` (generated by the tool)

**Interfaces:**
- Consumes: `AudioBank`, `SoundEntry`, `AudioBus` (Task 5).
- Produces: menu item `Kami/Audio/Rebuild Audio Bank`; a populated `Resources/AudioBank.asset`. Task 7 consumes the asset.

- [ ] **Step 1: Write the builder**

Idempotent and non-destructive is the hard requirement: it runs again after Diego hand-edits buses and clip lists, and must not wipe that work.

```csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates Resources/AudioBank.asset from the ~60 AudioSource children of
/// AudioManager.prefab, which already hold every clip, volume, pitch and loop value the game
/// shipped with. Generating rather than hand-typing is what makes the audio refactor safe: the
/// new system starts with exactly the old mix.
///
/// Idempotent: re-running only ADDS missing ids and reports orphans. Existing rows keep every
/// hand-edited field (bus, maxSimultaneous, extra clips), because reviewing 60 buses by hand is
/// work nobody wants to do twice.
/// </summary>
public static class AudioBankBuilder
{
    const string PREFAB_PATH = "Assets/Prefabs/AudioManager.prefab";
    const string BANK_PATH = "Assets/Resources/AudioBank.asset";

    [MenuItem("Kami/Audio/Rebuild Audio Bank")]
    public static void Rebuild()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
        if (prefab == null)
        {
            Debug.LogError($"[AudioBankBuilder] no encontre {PREFAB_PATH}.");
            return;
        }

        AudioBank bank = AssetDatabase.LoadAssetAtPath<AudioBank>(BANK_PATH);
        if (bank == null)
        {
            bank = ScriptableObject.CreateInstance<AudioBank>();
            AssetDatabase.CreateAsset(bank, BANK_PATH);
            Debug.Log($"[AudioBankBuilder] cree {BANK_PATH}.");
        }

        List<SoundEntry> entries = bank.EditorEntries;
        var existing = new Dictionary<string, SoundEntry>();
        for (int i = 0; i < entries.Count; i++)
        {
            if (!string.IsNullOrEmpty(entries[i].id))
            {
                existing[entries[i].id] = entries[i];
            }
        }

        AudioSource[] sources = prefab.GetComponentsInChildren<AudioSource>(true);
        int added = 0, skipped = 0;
        var seen = new HashSet<string>();

        foreach (AudioSource source in sources)
        {
            string id = source.gameObject.name;
            if (string.IsNullOrEmpty(id) || id == "AudioManager")
            {
                continue; //the root itself carries an AudioSource in some revisions
            }
            if (!seen.Add(id))
            {
                Debug.LogWarning($"[AudioBankBuilder] '{id}' aparece dos veces en el prefab.");
                continue;
            }
            if (existing.ContainsKey(id))
            {
                skipped++;
                continue; //preserve hand edits
            }

            entries.Add(new SoundEntry
            {
                id = id,
                clips = source.clip != null ? new[] { source.clip } : new AudioClip[0],
                volume = source.volume,
                pitch = source.pitch,
                pitchVariation = 0f,
                loop = source.loop,
                bus = GuessBus(id, source.loop),
                maxSimultaneous = source.loop ? 1 : 4,
                spatialBlend = source.spatialBlend,
                priority = source.priority,
                interruptSelf = false
            });
            added++;

            if (source.clip == null)
            {
                Debug.LogWarning($"[AudioBankBuilder] '{id}' no tiene clip en el prefab: " +
                                 "la fila queda sin clip.");
            }
        }

        foreach (string id in existing.Keys)
        {
            if (!seen.Contains(id))
            {
                Debug.Log($"[AudioBankBuilder] '{id}' esta en el banco pero ya no en el prefab. " +
                          "No lo borro (puede ser un sonido nuevo agregado a mano).");
            }
        }

        EditorUtility.SetDirty(bank);
        AssetDatabase.SaveAssets();
        Debug.Log($"[AudioBankBuilder] listo: {added} filas nuevas, {skipped} preservadas, " +
                  $"{entries.Count} en total. Revisa la columna 'bus' a mano una vez.");
    }

    /// <summary>
    /// First guess at routing, from the id. Deliberately a guess: Diego reviews it once
    /// (design doc section 8.3). Only affects which fader the sound sits under, never whether
    /// it plays.
    /// </summary>
    static AudioBus GuessBus(string id, bool loop)
    {
        if (id.StartsWith("MemoFlora") || id.StartsWith("Bohren") || id.StartsWith("4S_") ||
            id.StartsWith("EstampesPagodes") || id.StartsWith("GameOver") ||
            id.StartsWith("IntroStoryboard"))
        {
            return AudioBus.Music;
        }
        if (id.StartsWith("Forest"))
        {
            return AudioBus.Ambience;
        }
        if (id.StartsWith("PageTurn") || id.StartsWith("PickupSFX") ||
            id.StartsWith("PickupReversed") || id == "Action_Hover")
        {
            return AudioBus.UI;
        }
        return AudioBus.SFX;
    }
}
```

- [ ] **Step 2: Generate the meta file**

Run: `python tools/make-meta.py Assets/Editor/AudioBankBuilder.cs`

- [ ] **Step 3: Ask Diego to run the tool**

This needs the editor (it reads a prefab through `AssetDatabase`). Ask him to run `Kami/Audio/Rebuild Audio Bank` and paste the Console output.

Expected: roughly `60 filas nuevas, 0 preservadas, ~60 en total`. Report the exact count and every `no tiene clip` warning — those are pre-existing prefab gaps, not new breakage.

- [ ] **Step 4: Add the three placeholder entries by hand**

Ask Diego to add these rows in the bank inspector (they have no counterpart in the prefab because they are new sounds). Placeholder clips reuse existing files, and each row is named for its **final** purpose so swapping in real recordings later is just dragging files:

| id | clips | volume | pitch | pitchVariation | bus | maxSimultaneous |
|---|---|---|---|---|---|---|
| `Dialogue_PencilWrite` | `PaperCut01`, `PaperCut02`, `PaperFold01`, `PaperFold02` | 0.35 | 1.6 | 0.15 | UI | 3 |
| `Dialogue_Sigh` | *(leave empty for now)* | 0.8 | 1.0 | 0.05 | SFX | 1 |
| `Gallina_Cacareo` | `Gallina_Evade` | 0.9 | 1.0 | 0.2 | SFX | 6 |

Clips live in `Assets/Sounds/`. `Dialogue_Sigh` stays clip-less on purpose: it is per-line and optional, and a missing clip must be a silent no-op with one warning (Task 8 handles that), not an error.

- [ ] **Step 5: Stage and request permission to commit**

```bash
git add Assets/Editor/AudioBankBuilder.cs Assets/Editor/AudioBankBuilder.cs.meta Assets/Resources/AudioBank.asset Assets/Resources/AudioBank.asset.meta
```

Proposed message: `feat(audio): add AudioBankBuilder and generate the bank from the prefab`. **Ask Diego before committing.**

---

### Task 7: `AudioId` code generation

**Files:**
- Create: `Assets/Editor/AudioIdGenerator.cs`
- Create: `Assets/Scripts/Managers/Audio/AudioId.cs` (generated)

**Interfaces:**
- Consumes: `AudioBank.Instance`, `AudioBank.Ids` (Tasks 5–6).
- Produces: menu item `Kami/Audio/Regenerate AudioId`; `public static class AudioId` with one `const string` per bank entry. Tasks 8 and 13–23 consume the constants.

- [ ] **Step 1: Write the generator**

```csharp
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates AudioId.cs from the bank, so call sites say AudioId.QuestCompleted02 instead of a
/// magic string. The payoff is compile-time safety: delete a sound from the bank and every call
/// site stops compiling, instead of throwing at runtime the way the old string dictionary did
/// (TriggerSound.cs used to ask for "4S_MarimbaLoop", which exists nowhere -- see the design doc).
///
/// Re-run after adding or renaming anything in the bank.
/// </summary>
public static class AudioIdGenerator
{
    const string OUTPUT_PATH = "Assets/Scripts/Managers/Audio/AudioId.cs";

    [MenuItem("Kami/Audio/Regenerate AudioId")]
    public static void Generate()
    {
        AudioBank bank = AssetDatabase.LoadAssetAtPath<AudioBank>("Assets/Resources/AudioBank.asset");
        if (bank == null)
        {
            Debug.LogError("[AudioIdGenerator] no hay Resources/AudioBank.asset. " +
                           "Corre Kami/Audio/Rebuild Audio Bank primero.");
            return;
        }

        var used = new Dictionary<string, string>(); //identifier -> original id
        var rows = new List<KeyValuePair<string, string>>();

        foreach (string id in bank.Ids)
        {
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            string identifier = Sanitize(id);
            if (used.TryGetValue(identifier, out string clash))
            {
                //fail loudly instead of writing a file that will not compile
                Debug.LogError($"[AudioIdGenerator] colision: '{id}' y '{clash}' dan el mismo " +
                               $"identificador '{identifier}'. Renombra uno en el banco y volve a correr.");
                return;
            }
            used[identifier] = id;
            rows.Add(new KeyValuePair<string, string>(identifier, id));
        }

        var sb = new StringBuilder();
        sb.AppendLine("// GENERATED FILE -- do not edit by hand.");
        sb.AppendLine("// Regenerate with Kami/Audio/Regenerate AudioId after changing AudioBank.");
        sb.AppendLine();
        sb.AppendLine("/// <summary>Every sound id in the AudioBank, as compile-time constants.</summary>");
        sb.AppendLine("public static class AudioId");
        sb.AppendLine("{");
        foreach (KeyValuePair<string, string> row in rows)
        {
            sb.AppendLine($"    public const string {row.Key} = \"{row.Value}\";");
        }
        sb.AppendLine("}");

        System.IO.File.WriteAllText(OUTPUT_PATH, sb.ToString());
        AssetDatabase.ImportAsset(OUTPUT_PATH);
        Debug.Log($"[AudioIdGenerator] escribi {rows.Count} constantes en {OUTPUT_PATH}.");
    }

    /// <summary>
    /// Bank ids come from GameObject names, which can contain spaces and other characters that
    /// are illegal in a C# identifier. The const VALUE always keeps the original id, so the
    /// lookup still matches the bank.
    /// </summary>
    static string Sanitize(string id)
    {
        string cleaned = Regex.Replace(id, @"[^A-Za-z0-9_]", "_");
        if (cleaned.Length > 0 && char.IsDigit(cleaned[0]))
        {
            cleaned = "_" + cleaned;
        }
        return cleaned;
    }
}
```

Note: ids beginning with a digit (`4S_IntroBigChords`) become `_4S_IntroBigChords`. That is intentional and the const value is unchanged.

- [ ] **Step 2: Generate the meta file**

Run: `python tools/make-meta.py Assets/Editor/AudioIdGenerator.cs`

- [ ] **Step 3: Ask Diego to run it**

`Kami/Audio/Regenerate AudioId`. Expected: `escribi ~60 constantes`. If it reports a collision, stop and report which ids clash — do not "fix" it by editing the generated file.

- [ ] **Step 4: Verify compilation**

Run: `python tools/compile-check.py task7`
Expected: `RESULTADO: COMPILA OK`. The generated file must compile before anything depends on it.

- [ ] **Step 5: Stage and request permission to commit**

```bash
git add Assets/Editor/AudioIdGenerator.cs Assets/Editor/AudioIdGenerator.cs.meta Assets/Scripts/Managers/Audio/AudioId.cs Assets/Scripts/Managers/Audio/AudioId.cs.meta
```

Proposed message: `feat(audio): generate typed AudioId constants from the bank`. **Ask Diego before committing.**

---

### Task 8: Pooled `AudioManager` with compatibility shims

**Files:**
- Create: `Assets/Scripts/Managers/Audio/AudioPool.cs`
- Modify: `Assets/Scripts/Managers/AudioManager.cs` (full internal rewrite, public surface preserved)

**Interfaces:**
- Consumes: `AudioBank`, `SoundEntry`, `SoundHandle`, `AudioBus` (Task 5), `AudioId` (Task 7).
- Produces, on `AudioManager.instance`: `SoundHandle Play(string id)`, `Play(string id, float pitch)`, `Play(string id, float centralPitch, float pitchVariation)`, `SoundHandle PlayAt(string id, Vector3 position)`, `void Stop(SoundHandle)`, `void StopById(string id)`, `void StopAll()`, `void MuteAll()`, `void UnmuteAll()`, `void SetGlobalVolume(float)`, `void SetBusVolume(AudioBus, float)`, `void SetBGMVolumes(float)`, `void ResetBGMVolumes()`, `void PlayOnEnd(string idToEnd, string idToPlay)`, `void PlayGallinaSound()`, `bool SoundOn { get; set; }`, `bool IsPlaying(string id)`. Every task from 13 on consumes these.
- Also produces, deleted in Task 24: `[Obsolete] PlayByName(...)` (3 overloads), `[Obsolete] PlayRandom(params string[])`, `[Obsolete] StopByName(string)`, `[Obsolete] StopByName(params string[])`.

- [ ] **Step 1: Read the current `AudioManager.cs` in full**

All 121 call sites depend on its exact semantics. Note especially: `SoundOn` mutes/unmutes everything; `PlayByName(name, centralPitch, variation)` randomises pitch then restores it when the clip ends; `PlayOnEnd` clears `loop`, waits for the clip, then restores `loop` and plays the next; `SetBGMVolumes` scales only the three hardcoded BGMs.

- [ ] **Step 2: Write `AudioPool.cs`**

```csharp
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fixed pool of AudioSources. Replaces the ~60 always-alive AudioSource children of
/// AudioManager.prefab: sources are generic here, and a SoundEntry configures one at play time.
///
/// Generation counters make stale SoundHandles safe (see SoundHandle): a handle can only ever
/// stop the exact instance it was issued for.
/// </summary>
public class AudioPool
{
    class PooledSource
    {
        public AudioSource source;
        public int generation;
        public string id;
        public bool inUse;
        public float baseVolume;   //entry volume, before the global multiplier
        public double releaseTime; //AudioSettings.dspTime when a one-shot is expected to end
        public bool isLoop;
    }

    readonly List<PooledSource> _sources = new List<PooledSource>();
    readonly Transform _parent;
    bool _warnedExhausted;

    public AudioPool(Transform parent, int size)
    {
        _parent = parent;
        for (int i = 0; i < size; i++)
        {
            _sources.Add(CreateSource(i));
        }
    }

    PooledSource CreateSource(int index)
    {
        var go = new GameObject($"PooledAudioSource_{index}");
        go.transform.SetParent(_parent, false);
        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        return new PooledSource { source = source, generation = 0, inUse = false };
    }

    public int CountPlaying(string id)
    {
        int count = 0;
        for (int i = 0; i < _sources.Count; i++)
        {
            if (_sources[i].inUse && _sources[i].id == id)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>Oldest live instance of an id, or null. Used by interruptSelf.</summary>
    public AudioSource OldestOf(string id)
    {
        PooledSource oldest = null;
        for (int i = 0; i < _sources.Count; i++)
        {
            PooledSource candidate = _sources[i];
            if (!candidate.inUse || candidate.id != id)
            {
                continue;
            }
            if (oldest == null || candidate.releaseTime < oldest.releaseTime)
            {
                oldest = candidate;
            }
        }
        return oldest?.source;
    }

    public bool TryAcquire(string id, bool isLoop, float baseVolume, double expectedEnd,
                           out AudioSource source, out int generation)
    {
        for (int i = 0; i < _sources.Count; i++)
        {
            if (!_sources[i].inUse)
            {
                Occupy(_sources[i], id, isLoop, baseVolume, expectedEnd);
                source = _sources[i].source;
                generation = _sources[i].generation;
                return true;
            }
        }

        //everything busy: steal the oldest non-looping source. Stealing a loop would silence
        //music or ambience, which is far more noticeable than dropping one sound effect.
        PooledSource victim = null;
        for (int i = 0; i < _sources.Count; i++)
        {
            if (_sources[i].isLoop)
            {
                continue;
            }
            if (victim == null || _sources[i].releaseTime < victim.releaseTime)
            {
                victim = _sources[i];
            }
        }

        if (victim == null)
        {
            source = null;
            generation = 0;
            return false;
        }

        if (!_warnedExhausted)
        {
            _warnedExhausted = true;
            Debug.LogWarning($"[AudioPool] pool agotado ({_sources.Count} sources): le robo el " +
                             $"source a '{victim.id}' para tocar '{id}'. Si pasa seguido, subile " +
                             "poolSize al AudioManager o bajale maxSimultaneous a los sonidos " +
                             "que se apilan. (Aviso una sola vez por sesion.)");
        }

        victim.source.Stop();
        Occupy(victim, id, isLoop, baseVolume, expectedEnd);
        source = victim.source;
        generation = victim.generation;
        return true;
    }

    void Occupy(PooledSource pooled, string id, bool isLoop, float baseVolume, double expectedEnd)
    {
        pooled.inUse = true;
        pooled.id = id;
        pooled.isLoop = isLoop;
        pooled.baseVolume = baseVolume;
        pooled.releaseTime = expectedEnd;
        pooled.generation++;
    }

    /// <summary>
    /// Called every frame by AudioManager: returns finished one-shots to the pool. A poll rather
    /// than a coroutine per sound, on purpose -- EncounterManager calls
    /// AudioManager.instance.StopAllCoroutines() from outside, which would otherwise strand
    /// every source it had already handed out.
    /// </summary>
    public void ReclaimFinished()
    {
        for (int i = 0; i < _sources.Count; i++)
        {
            PooledSource pooled = _sources[i];
            if (pooled.inUse && !pooled.isLoop && !pooled.source.isPlaying)
            {
                Release(pooled);
            }
        }
    }

    void Release(PooledSource pooled)
    {
        pooled.inUse = false;
        pooled.id = null;
        pooled.isLoop = false;
        pooled.source.clip = null;
        pooled.source.loop = false;
        pooled.source.pitch = 1f;
        pooled.source.volume = 1f;
        pooled.source.spatialBlend = 0f;
        pooled.source.outputAudioMixerGroup = null;
        pooled.source.transform.localPosition = Vector3.zero;
    }

    public bool IsCurrent(AudioSource source, int generation)
    {
        for (int i = 0; i < _sources.Count; i++)
        {
            if (_sources[i].source == source)
            {
                return _sources[i].inUse && _sources[i].generation == generation;
            }
        }
        return false;
    }

    public void StopId(string id)
    {
        for (int i = 0; i < _sources.Count; i++)
        {
            if (_sources[i].inUse && _sources[i].id == id)
            {
                _sources[i].source.Stop();
                Release(_sources[i]);
            }
        }
    }

    public void StopSource(AudioSource source)
    {
        for (int i = 0; i < _sources.Count; i++)
        {
            if (_sources[i].source == source)
            {
                _sources[i].source.Stop();
                Release(_sources[i]);
                return;
            }
        }
    }

    public void StopEverything()
    {
        for (int i = 0; i < _sources.Count; i++)
        {
            if (_sources[i].inUse)
            {
                _sources[i].source.Stop();
                Release(_sources[i]);
            }
        }
    }

    public bool IsPlayingId(string id) => CountPlaying(id) > 0;

    public void SetMuted(bool muted)
    {
        for (int i = 0; i < _sources.Count; i++)
        {
            _sources[i].source.mute = muted;
        }
    }

    /// <summary>Re-applies a global multiplier over each live source's own entry volume.</summary>
    public void ApplyGlobalVolume(float globalVolume)
    {
        for (int i = 0; i < _sources.Count; i++)
        {
            if (_sources[i].inUse)
            {
                _sources[i].source.volume = _sources[i].baseVolume * globalVolume;
            }
        }
    }

    public void ApplyVolumeToId(string id, float multiplier)
    {
        for (int i = 0; i < _sources.Count; i++)
        {
            if (_sources[i].inUse && _sources[i].id == id)
            {
                _sources[i].source.volume = _sources[i].baseVolume * multiplier;
            }
        }
    }
}
```

- [ ] **Step 3: Rewrite `AudioManager.cs`**

Keep the class name, the `instance` field, `DontDestroyOnLoad`, and the `SoundOn` property exactly as they are — 121 call sites and the settings UI depend on them. Replace the internals:

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Pooled, data-driven audio. Configuration lives in Resources/AudioBank.asset (one row per
/// sound); playback goes through a fixed pool of AudioSources instead of ~60 always-alive ones.
///
/// The public surface is deliberately the same shape as the old string-keyed manager so the
/// migration could happen one folder at a time. The PlayByName / PlayRandom / StopByName shims
/// at the bottom are marked Obsolete and get deleted once every call site is migrated -- at
/// which point a missed call site fails to compile.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;

    [SerializeField, Tooltip("How many AudioSources the pool creates. Raise it if the pool " +
                             "warns about stealing sources.")]
    int _poolSize = 24;

    [SerializeField, Tooltip("Music bus level used when a menu ducks the music.")]
    float _duckedMusicVolume = 0.4f;

    AudioPool _pool;
    float _globalVolume = 1f;
    bool _soundOn = true;
    readonly Dictionary<AudioBus, float> _busVolumes = new Dictionary<AudioBus, float>();
    readonly HashSet<string> _unknownIdsReported = new HashSet<string>();

    public bool SoundOn
    {
        get { return _soundOn; }
        set
        {
            _soundOn = value;
            if (_soundOn)
            {
                UnmuteAll();
            }
            else
            {
                MuteAll();
            }
        }
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        _pool = new AudioPool(transform, _poolSize);

        foreach (AudioBus bus in System.Enum.GetValues(typeof(AudioBus)))
        {
            _busVolumes[bus] = 1f;
        }

        if (AudioBank.Instance == null)
        {
            Debug.LogError("[AudioManager] arranque sin AudioBank: no va a sonar nada.");
        }
    }

    void Update()
    {
        //returns finished one-shots to the pool. See AudioPool.ReclaimFinished for why this is a
        //poll and not a coroutine per sound.
        _pool.ReclaimFinished();
    }

    // ------------------------------------------------------------------ play

    public SoundHandle Play(string id)
    {
        return PlayInternal(id, null, null, false, Vector3.zero);
    }

    public SoundHandle Play(string id, float pitch)
    {
        return PlayInternal(id, pitch, null, false, Vector3.zero);
    }

    public SoundHandle Play(string id, float centralPitch, float pitchVariation)
    {
        return PlayInternal(id, centralPitch, pitchVariation, false, Vector3.zero);
    }

    /// <summary>Positional one-shot. Used by the chickens; the rest of the game is 2D.</summary>
    public SoundHandle PlayAt(string id, Vector3 position)
    {
        return PlayInternal(id, null, null, true, position);
    }

    SoundHandle PlayInternal(string id, float? pitchOverride, float? variationOverride,
                             bool positional, Vector3 position)
    {
        if (!TryGetEntry(id, out SoundEntry entry))
        {
            return SoundHandle.None;
        }

        AudioClip clip = entry.PickClip();
        if (clip == null)
        {
            //a bank row with no clip is a legitimate "not recorded yet" state (Dialogue_Sigh
            //ships that way), so warn once per id and stay silent instead of erroring
            if (_unknownIdsReported.Add($"noclip:{id}"))
            {
                Debug.LogWarning($"[AudioManager] '{id}' no tiene clips en el banco: no suena.");
            }
            return SoundHandle.None;
        }

        if (entry.maxSimultaneous > 0 && _pool.CountPlaying(id) >= entry.maxSimultaneous)
        {
            if (!entry.interruptSelf)
            {
                return SoundHandle.None; //at the cap: drop this play, by design
            }
            AudioSource oldest = _pool.OldestOf(id);
            if (oldest != null)
            {
                _pool.StopSource(oldest);
            }
        }

        float basePitch = pitchOverride ?? entry.pitch;
        float variation = variationOverride ?? entry.pitchVariation;
        float finalPitch = variation > 0f
            ? Random.Range(basePitch - variation, basePitch + variation)
            : basePitch;

        double expectedEnd = AudioSettings.dspTime +
                             (clip.length / Mathf.Max(0.01f, Mathf.Abs(finalPitch)));

        if (!_pool.TryAcquire(id, entry.loop, entry.volume, expectedEnd,
                              out AudioSource source, out int generation))
        {
            return SoundHandle.None;
        }

        source.clip = clip;
        source.loop = entry.loop;
        source.pitch = finalPitch;
        source.priority = entry.priority;
        source.spatialBlend = positional ? Mathf.Max(entry.spatialBlend, 1f) : entry.spatialBlend;
        source.volume = entry.volume * _globalVolume * BusVolume(entry.bus);
        source.mute = !_soundOn;
        source.transform.position = positional ? position : Vector3.zero;

        AudioMixerGroup group = AudioBank.Instance != null ? AudioBank.Instance.GroupFor(entry.bus) : null;
        source.outputAudioMixerGroup = group; //null until the mixer exists: volume stays code-side

        source.Play();
        return new SoundHandle(source, generation);
    }

    bool TryGetEntry(string id, out SoundEntry entry)
    {
        entry = null;
        if (AudioBank.Instance == null)
        {
            return false;
        }
        if (!AudioBank.Instance.TryGet(id, out entry))
        {
            //the old manager threw KeyNotFoundException here, which is how TriggerSound's call for
            //the nonexistent "4S_MarimbaLoop" could take down a whole coroutine. Warn once instead.
            if (_unknownIdsReported.Add(id))
            {
                Debug.LogWarning($"[AudioManager] no existe el sonido '{id}' en el banco. " +
                                 "Reviso Resources/AudioBank.asset. (Aviso una sola vez por id.)");
            }
            return false;
        }
        return true;
    }

    // ------------------------------------------------------------------ stop

    public void Stop(SoundHandle handle)
    {
        if (!handle.IsValid || !_pool.IsCurrent(handle.source, handle.generation))
        {
            return; //stale handle: the source now plays something else. Never stop it.
        }
        _pool.StopSource(handle.source);
    }

    public void StopById(string id)
    {
        _pool.StopId(id);
    }

    public void StopAll()
    {
        _pool.StopEverything();
    }

    public bool IsPlaying(string id)
    {
        return _pool.IsPlayingId(id);
    }

    // ------------------------------------------------------------------ volume

    public void MuteAll()
    {
        _pool.SetMuted(true);
    }

    public void UnmuteAll()
    {
        _pool.SetMuted(false);
    }

    public void SetGlobalVolume(float volume)
    {
        _globalVolume = volume;
        _pool.ApplyGlobalVolume(_globalVolume);
        OnGlobalVolumeChanged();
    }

    public void SetBusVolume(AudioBus bus, float volume)
    {
        _busVolumes[bus] = volume;
        //no mixer yet: re-apply to whatever of this bus is currently playing
        foreach (SoundEntry entry in AudioBank.Instance.Entries)
        {
            if (entry.bus == bus)
            {
                _pool.ApplyVolumeToId(entry.id, _globalVolume * volume);
            }
        }
    }

    float BusVolume(AudioBus bus)
    {
        return _busVolumes.TryGetValue(bus, out float volume) ? volume : 1f;
    }

    /// <summary>Menu ducking. Same signature as before; now one bus instead of a hardcoded list.</summary>
    public void SetBGMVolumes(float volume)
    {
        SetBusVolume(AudioBus.Music, volume);
    }

    public void ResetBGMVolumes()
    {
        SetBusVolume(AudioBus.Music, 1f);
    }

    void OnGlobalVolumeChanged()
    {
        if (!IsPlaying(AudioId.Gallina_Evade_VolumeTest))
        {
            PlayGallinaSound();
        }
    }

    /// <summary>Audition sound when the volume slider moves. Behaviour preserved from before.</summary>
    public void PlayGallinaSound()
    {
        Play(AudioId.Gallina_Evade_VolumeTest);
    }

    // ------------------------------------------------------------------ sequencing

    public void PlayOnEnd(string idToEnd, string idToPlay)
    {
        StartCoroutine(PlayOnOtherSoundEnd(idToEnd, idToPlay));
    }

    public IEnumerator PlayOnOtherSoundEnd(string idToEnd, string idToPlay)
    {
        while (IsPlaying(idToEnd))
        {
            yield return null;
        }
        StopById(idToEnd);
        Play(idToPlay);
    }
```

Then the shims, at the bottom of the class, each one line of forwarding. **Task 24 deletes this whole block**:

```csharp
    // ---------------------------------------------------- obsolete compatibility shims
    // Kept ONLY so the project compiles while the 121 call sites migrate folder by folder.
    // Task 24 deletes this region; anything still calling these then fails to compile, which is
    // exactly the completeness check we want.

    [System.Obsolete("Use Play(AudioId.X)")]
    public void PlayByName(string clipName) => Play(clipName);

    [System.Obsolete("Use Play(AudioId.X, pitch)")]
    public void PlayByName(string clipName, float pitch) => Play(clipName, pitch);

    [System.Obsolete("Use Play(AudioId.X, pitch, variation)")]
    public void PlayByName(string clipName, float centralPitch, float pitchVariation)
        => Play(clipName, centralPitch, pitchVariation);

    [System.Obsolete("Put the clips in one bank entry and use Play(AudioId.X)")]
    public void PlayRandom(params string[] clipNames)
    {
        if (clipNames == null || clipNames.Length == 0)
        {
            return;
        }
        Play(clipNames[Random.Range(0, clipNames.Length)]);
    }

    [System.Obsolete("Use StopById(AudioId.X)")]
    public void StopByName(string clipName) => StopById(clipName);

    [System.Obsolete("Use StopById(AudioId.X)")]
    public void StopByName(params string[] clipNames)
    {
        for (int i = 0; i < clipNames.Length; i++)
        {
            StopById(clipNames[i]);
        }
    }
}
```

Deleted deliberately, with their reasons — record these in the report: `soundDict` and `_allSounds` (the pool replaces them), `originalVolumes` (per-entry volume lives in the bank), the `bgms` list (the Music bus), `SetPitchToOriginal` / `SetVolumeToOriginal` (a released source is reset, so there is nothing to restore).

- [ ] **Step 4: Suppress the obsolete warnings during the sweep**

The 121 not-yet-migrated call sites will each emit CS0618, drowning the real output. Add `#pragma warning disable 618` at the top of `AudioManager.cs` **only if** the warnings appear outside that file; they should not, since the attribute warns at the *call* site. If `compile-check` floods with CS0618, report the count and proceed — the count going to zero is the Task 24 signal. Do **not** blanket-disable 618 project-wide.

- [ ] **Step 5: Verify compilation**

Run: `python tools/compile-check.py task8`
Expected: `RESULTADO: COMPILA OK`. Baseline warnings plus up to 121 CS0618 obsolete warnings. Any *error* means a semantic drift from the old public surface — re-read Step 1.

- [ ] **Step 6: Generate the meta file**

Run: `python tools/make-meta.py Assets/Scripts/Managers/Audio/AudioPool.cs`

- [ ] **Step 7: Stage and request permission to commit**

```bash
git add Assets/Scripts/Managers/Audio/AudioPool.cs Assets/Scripts/Managers/Audio/AudioPool.cs.meta Assets/Scripts/Managers/AudioManager.cs
```

Proposed message: `refactor(audio): pooled bank-driven AudioManager behind compatibility shims`. **Ask Diego before committing.** Tell him Phase 1 is unblocked, and that the game should sound identical at this point — a good moment for a quick playtest.

---

# Phase 1 — Controls diagram and the call-site sweep

### Task 9: `ControlsDiagramSettings` and its asset

**Files:**
- Create: `Assets/Scripts/UI/ControlsDiagramSettings.cs`
- Create: `Assets/Resources/ControlsDiagramSettings.asset`

**Interfaces:**
- Consumes: `Accion` (Task 1).
- Produces: `ControlsDiagramSettings.Instance`, fields `keyboardDiagram`, `gamepadDiagram`, `keyboardHotspots`, `gamepadHotspots`, `idleTint`, `mappedTint`, `pressedTint`, `pressedPulseSpeed`; `struct ControlHotspot { Accion action; Rect normalizedRect; string labelKey; Color color; }`. Task 10 consumes all of it.

- [ ] **Step 1: Write the settings type**

```csharp
using System;
using UnityEngine;

/// <summary>
/// One hotspot over the diagram: which action it represents, where it sits on the sprite, and
/// what to call it.
/// </summary>
[Serializable]
public struct ControlHotspot
{
    public Accion action;

    [Tooltip("Position over the diagram sprite in 0..1 coordinates, origin bottom-left. " +
             "Normalized on purpose: swapping in art at a different resolution keeps these valid; " +
             "only a different LAYOUT needs re-tuning.")]
    public Rect normalizedRect;

    [Tooltip("UITexts key for this action's label, e.g. Ctrl_Saltar.")]
    public string labelKey;

    [Tooltip("Highlight colour. Match the keyword palette so the colour code reads the same as " +
             "in the rest of the game's text.")]
    public Color color;
}

/// <summary>
/// Art and layout for the controls diagram. Self-loading from Resources with a code fallback,
/// same pattern as TextHighlightSettings: nothing to wire, and a missing asset degrades to a
/// warning instead of an empty tab.
///
/// Valentino only ever needs the two sprite fields. Leave one empty and ControlsDiagram draws a
/// procedural placeholder instead.
/// </summary>
[CreateAssetMenu(fileName = "ControlsDiagramSettings", menuName = "Kami/Controls Diagram Settings")]
public class ControlsDiagramSettings : ScriptableObject
{
    const string RESOURCE_NAME = "ControlsDiagramSettings";

    [Header("Art (leave empty to draw a placeholder)")]
    public Sprite keyboardDiagram;
    public Sprite gamepadDiagram;

    [Header("Hotspots")]
    public ControlHotspot[] keyboardHotspots = new ControlHotspot[0];
    public ControlHotspot[] gamepadHotspots = new ControlHotspot[0];

    [Header("Tints")]
    [Tooltip("Controls with no mapping on this device.")]
    public Color idleTint = new Color(1f, 1f, 1f, 0.15f);

    [Tooltip("Mapped, at rest. This is the permanent highlight.")]
    public Color mappedTint = new Color(1f, 0.85f, 0.3f, 0.55f);

    [Tooltip("While the control is actually held.")]
    public Color pressedTint = new Color(1f, 0.95f, 0.6f, 0.95f);

    [Tooltip("Pulses per second while held. Uses unscaled time (the Flap pauses the game).")]
    public float pressedPulseSpeed = 3f;

    static ControlsDiagramSettings _instance;
    static bool _missingReported;

    public static ControlsDiagramSettings Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<ControlsDiagramSettings>(RESOURCE_NAME);
                if (_instance == null)
                {
                    if (!_missingReported)
                    {
                        _missingReported = true;
                        Debug.LogWarning($"[ControlsDiagramSettings] no encontre " +
                                         $"Resources/{RESOURCE_NAME}.asset, uso valores por defecto.");
                    }
                    _instance = CreateInstance<ControlsDiagramSettings>();
                }
            }
            return _instance;
        }
    }
}
```

- [ ] **Step 2: Generate the meta file**

Run: `python tools/make-meta.py Assets/Scripts/UI/ControlsDiagramSettings.cs`

- [ ] **Step 3: Verify compilation**

Run: `python tools/compile-check.py task9`
Expected: `RESULTADO: COMPILA OK`.

- [ ] **Step 4: Ask Diego to create the asset with these defaults**

Create → Kami → Controls Diagram Settings, saved as `Assets/Resources/ControlsDiagramSettings.asset`, with both sprites left **empty** (placeholder mode) and these hotspot rows. The rects match the procedural placeholder layout Task 10 draws, so it looks right immediately:

**Keyboard** (7 rows) — `Mover` (0.06, 0.30, 0.26, 0.34) `Ctrl_Mover`; `Saltar` (0.36, 0.10, 0.30, 0.14) `Ctrl_Saltar`; `Interactuar` (0.36, 0.46, 0.10, 0.16) `Ctrl_Interactuar`; `Atacar` (0.06, 0.10, 0.14, 0.14) `Ctrl_Atacar`; `Correr` (0.06, 0.66, 0.20, 0.14) `Ctrl_Correr`; `Camara` (0.70, 0.10, 0.14, 0.14) `Ctrl_Camara`; `Menu` (0.06, 0.82, 0.12, 0.14) `Ctrl_Menu`.

Two actions get **no keyboard row**, both deliberately: `CambiarTab` has no keyboard binding at all (its axes are joystick-only — see the comment on the `Accion` member), and **mute (M) has no `Accion` member**, so it cannot be a hotspot without extending the enum. Leave mute off the diagram and note it in the report as the one player-facing key the diagram does not show; extending `Accion` for it is a decision for Diego, not for this task.

**Gamepad** (8 rows) — `Mover` (0.10, 0.44, 0.16, 0.20) `Ctrl_Mover`; `Saltar` (0.74, 0.34, 0.09, 0.11) `Ctrl_Saltar`; `Interactuar` (0.74, 0.34, 0.09, 0.11) `Ctrl_Interactuar` (**same rect as Saltar on purpose** — A is the contextual button, see `controles-y-gamepad.md`); `Atacar` (0.84, 0.44, 0.09, 0.11) `Ctrl_Atacar`; `Correr` (0.20, 0.78, 0.12, 0.10) `Ctrl_Correr`; `Camara` (0.20, 0.90, 0.12, 0.08) `Ctrl_Camara`; `Menu` (0.56, 0.60, 0.08, 0.08) `Ctrl_Menu`; `CambiarTab` (0.68, 0.78, 0.12, 0.10) `Ctrl_CambiarTab`.

Colours: pick four distinct hues from the `TextHighlightSettings` palette so the code matches the rest of the game's text — read that asset and reuse its actual values rather than inventing colours.

- [ ] **Step 5: Stage and request permission to commit**

```bash
git add Assets/Scripts/UI/ControlsDiagramSettings.cs Assets/Scripts/UI/ControlsDiagramSettings.cs.meta Assets/Resources/ControlsDiagramSettings.asset Assets/Resources/ControlsDiagramSettings.asset.meta
```

Proposed message: `feat(ui): add controls diagram settings asset`. **Ask Diego before committing.**

---

### Task 10: `ControlsDiagram`

**Files:**
- Create: `Assets/Scripts/UI/ControlsDiagram.cs`

**Interfaces:**
- Consumes: `ControlsDiagramSettings.Instance`, `ControlHotspot` (Task 9); `Accion` and the `InputHub.*Held` / `MoveAxisRaw` properties (Task 1); `InputHub.OnDeviceCambio`, `InputHub.UltimoDeviceFueJoystick`; `LocalizedText.Escribir`.
- Produces: `MonoBehaviour ControlsDiagram` with `public void Refresh()`. Task 12 attaches it and may call `Refresh()`.

- [ ] **Step 1: Read the precedents before writing anything**

Read `Assets/Scripts/Input/GamepadCursor.cs` (how a UI hierarchy gets built from scratch in code, and how a procedural texture is generated) and `Assets/Scripts/UI/SoloConJoystick.cs` (the `OnDeviceCambio` subscribe/unsubscribe shape). Follow both rather than inventing a third style. Also read `LocalizedText.Escribir`'s signature — Task 11 uses the `UITexts` table and this class must call it the same way the rest of the UI does.

- [ ] **Step 2: Write the component**

Structure to build in `Awake`, all by code, no prefab:

```
ControlsDisplay (this GameObject, already exists in FlapManager.prefab)
└── ControlsDiagramRoot (RectTransform, stretched to parent)
    ├── DiagramImage (Image: keyboard or gamepad sprite)
    └── Hotspots
        ├── Hotspot_Mover   (Image tint + child TMP label)
        ├── Hotspot_Saltar
        └── ... one per row of the active device's hotspot list
```

Requirements the implementation must satisfy, each for a stated reason:

1. **Both hotspot sets are built once** in `Awake` and toggled with `SetActive`, not rebuilt per device change — a device change can happen mid-frame and rebuilding would churn UI allocations.
2. **`Update()` only runs while visible.** Gate on `OnEnable` / `OnDisable`; `ControlsDisplay` is shown and hidden by `FlapManager`, so a keyboard-only session in gameplay costs nothing.
3. **Pulse uses `Time.unscaledDeltaTime`.** `FlapManager` sets `Time.timeScale = 0` when the menu finishes opening, so scaled time is frozen exactly when this UI is on screen.
4. **Never read `Input` directly** — go through `InputHub`. That is the project's rule and the reason `InputHub` exists.
5. **The diagram never consumes input.** No `GetButtonDown` handling, no `EventSystem` selection, no flags others read. It observes only, so it cannot create an arbitration bug of the #41.2 / #41.14 family.
6. **A hotspot whose action has no binding on the current device gets `idleTint`** and no label.

The held-state lookup:

```csharp
    static bool IsHeld(Accion action)
    {
        switch (action)
        {
            case Accion.Saltar: return InputHub.SaltoHeld;
            case Accion.Interactuar: return InputHub.InteractHeld;
            case Accion.Atacar: return InputHub.AtaqueHeld;
            case Accion.Correr: return InputHub.CorrerHeld;
            case Accion.Camara: return InputHub.CamaraHeld;
            case Accion.Menu: return InputHub.MenuHeld;
            case Accion.CambiarTab: return InputHub.TabSiguienteHeld || InputHub.TabAnteriorHeld;
            case Accion.Mover: return InputHub.MoveAxisRaw.sqrMagnitude > 0.01f;
            default: return false;
        }
    }
```

The per-frame tint:

```csharp
    void Update()
    {
        ControlsDiagramSettings settings = ControlsDiagramSettings.Instance;
        //unscaled: the Flap freezes Time.timeScale while this tab is on screen
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * settings.pressedPulseSpeed * Mathf.PI * 2f);

        for (int i = 0; i < _activeHotspots.Count; i++)
        {
            HotspotVisual visual = _activeHotspots[i];
            if (IsHeld(visual.action))
            {
                visual.image.color = Color.Lerp(visual.baseColor, settings.pressedTint, pulse);
            }
            else
            {
                visual.image.color = visual.baseColor;
            }
        }
    }
```

- [ ] **Step 3: Implement the procedural placeholder**

When the device's sprite field is null, generate a `Texture2D` and log once:

```csharp
        Debug.Log("[ControlsDiagram] no hay sprite de diagrama para este device, dibujo un " +
                  "placeholder. Asignalo en Resources/ControlsDiagramSettings.asset cuando " +
                  "llegue el arte.");
```

Draw a dark rounded panel plus one outlined rectangle per hotspot rect (so the placeholder *is* the layout documentation), sized ~512x256 for the keyboard and ~512x384 for the gamepad. Keep it simple: filled rects and 1px borders, `FilterMode.Bilinear`, `Apply()` once. Do not attempt real key legends — the TMP labels already name each action, and the point is a readable stand-in, not art.

- [ ] **Step 4: Implement device switching**

```csharp
    void OnEnable()
    {
        InputHub.OnDeviceCambio += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        InputHub.OnDeviceCambio -= Refresh;
    }

    /// <summary>Shows the diagram for the device in use and re-localizes the labels.</summary>
    public void Refresh()
    {
        bool joystick = InputHub.UltimoDeviceFueJoystick;
        _keyboardRoot.SetActive(!joystick);
        _gamepadRoot.SetActive(joystick);
        _activeHotspots = joystick ? _gamepadVisuals : _keyboardVisuals;
    }
```

Labels go through `LocalizedText` so they follow both the language and any device-dependent prompt rewriting, exactly like every other string in this project.

- [ ] **Step 5: Verify compilation**

Run: `python tools/compile-check.py task10`
Expected: `RESULTADO: COMPILA OK`. If `Accion` or an `InputHub.*Held` member is missing, Task 1 is not merged — stop, do not add the property here.

- [ ] **Step 6: Generate the meta file**

Run: `python tools/make-meta.py Assets/Scripts/UI/ControlsDiagram.cs`

- [ ] **Step 7: Stage and request permission to commit**

```bash
git add Assets/Scripts/UI/ControlsDiagram.cs Assets/Scripts/UI/ControlsDiagram.cs.meta
```

Proposed message: `feat(ui): add code-built controls diagram with live input highlight`. **Ask Diego before committing.**

---

### Task 11: Localization keys for the diagram labels

**Files:**
- Modify: `Assets/Localization Settings/Tables/UITexts_es.asset`, `UITexts_en.asset`, `UITexts_pt.asset`, and `UITexts Shared Data.asset`

**Interfaces:**
- Consumes: nothing (`labelKey` strings agreed in Task 9).
- Produces: keys `Ctrl_Mover`, `Ctrl_Saltar`, `Ctrl_Interactuar`, `Ctrl_Atacar`, `Ctrl_Correr`, `Ctrl_Camara`, `Ctrl_Menu`, `Ctrl_CambiarTab` in all three locales. Task 10's labels resolve against them.

- [ ] **Step 1: Read how an existing key is stored**

Open `UITexts Shared Data.asset` and find `controlsText` — note its numeric key id and how the shared-data entry pairs with the per-locale table rows. Then find the same id in `UITexts_es.asset`. Every new key needs a shared-data entry **and** one row per locale, with matching ids.

- [ ] **Step 2: Add the eight keys**

Because these tables are asset YAML with generated key ids, adding entries by hand risks id collisions. **Prefer asking Diego to add them through the Localization Tables window** (Window → Asset Management → Localization Tables), which allocates ids correctly. Provide him this exact copy:

| Key | es | en | pt |
|---|---|---|---|
| `Ctrl_Mover` | Mover | Move | Mover |
| `Ctrl_Saltar` | Saltar | Jump | Saltar |
| `Ctrl_Interactuar` | Interactuar | Interact | Interagir |
| `Ctrl_Atacar` | Cortar | Cut | Cortar |
| `Ctrl_Correr` | Correr | Run | Correr |
| `Ctrl_Camara` | Cámara | Camera | Câmera |
| `Ctrl_Menu` | Menú | Menu | Menu |
| `Ctrl_CambiarTab` | Cambiar sección | Change section | Mudar seção |

If Diego prefers it done by hand instead, mirror the `controlsText` structure exactly, allocate ids above the current maximum in the shared data, and verify with a grep that each new id appears exactly once per file.

- [ ] **Step 3: Verify the keys resolve**

Ask Diego to open the Flap's Controls tab in each language after Task 12 lands. A key that shows as its own name (`Ctrl_Saltar` rendered literally) means the shared-data entry and the locale row have mismatched ids.

- [ ] **Step 4: Stage and request permission to commit**

```bash
git add "Assets/Localization Settings/Tables"
```

Proposed message: `feat(i18n): add controls diagram action labels in es/en/pt`. **Ask Diego before committing.**

---

### Task 12: Wire the diagram into the Flap's Controls tab

**Blocked until Task 16 is committed** (`FlapManager.cs` is owned by the audio sweep first — see the ownership table).

**Files:**
- Modify: `Assets/Prefabs/UI/FlapManager.prefab` (the `ControlsDisplay` GameObject, around line 3108)
- Modify: `Assets/Scripts/UI/FlapManager.cs` (only if a `Refresh()` call is needed when the tab is shown)

**Interfaces:**
- Consumes: `ControlsDiagram` (Task 10).
- Produces: a working Controls tab. Nothing consumes this.

- [ ] **Step 1: Read the whole prefab region first**

Read `FlapManager.prefab` around `ControlsDisplay` (line ~3108) and map its children: find the TMP text object that currently renders `controlsText` and its `LocalizeStringEvent`. Follow the repo's prefab-surgery rules — read the file before editing, copy existing block patterns, count `--- !u!` blocks before and after, and preserve the file's line endings.

- [ ] **Step 2: Attach the component**

Add a `MonoBehaviour` block for `ControlsDiagram` on the `ControlsDisplay` GameObject, using the script GUID from `ControlsDiagram.cs.meta`. Copy the shape of an existing `MonoBehaviour` block in the same file rather than writing one from memory.

- [ ] **Step 3: Hide the old text list**

Set `m_IsActive: 0` on the GameObject holding the `controlsText` TMP. Deactivate rather than delete: it is the fallback if the diagram needs to be backed out, and the localization entry stays valid.

- [ ] **Step 4: Verify the prefab is still structurally sound**

```bash
grep -c "^--- !u!" Assets/Prefabs/UI/FlapManager.prefab
```
Compare against the count before the edit: it must be exactly one higher (the new `MonoBehaviour`). Also confirm the new block's `fileID` appears exactly once in the file.

- [ ] **Step 5: Verify compilation**

Run: `python tools/compile-check.py task12`
Expected: `RESULTADO: COMPILA OK`.

- [ ] **Step 6: Hand the runtime check to Diego**

Ask him to: open the Flap → Controls with the keyboard (keyboard placeholder shows, labels in the current language), hold W/Space/E/Shift (those hotspots pulse), move the stick (diagram swaps to the gamepad placeholder), hold A and B (those pulse), press Esc/Start to close, then walk around and confirm nothing highlights during gameplay and no `[ControlsDiagram]` spam appears in the Console.

- [ ] **Step 7: Stage and request permission to commit**

```bash
git add Assets/Prefabs/UI/FlapManager.prefab Assets/Scripts/UI/FlapManager.cs
```

Proposed message: `feat(ui): show the controls diagram in the Flap instead of the text list`. **Ask Diego before committing.**

---

### Tasks 13–20: Audio call-site sweep

**These eight tasks are identical in shape.** Each takes one folder, migrates its `AudioManager.instance` calls to the typed API, and verifies compilation. They can all run in parallel with each other, subject to the ownership table.

**The transformation rules** (the same for every task):

| Before | After |
|---|---|
| `PlayByName("X")` | `Play(AudioId.X)` |
| `PlayByName("X", 1.5f)` | `Play(AudioId.X, 1.5f)` |
| `PlayByName("X", 0.5f, 0.01f)` | `Play(AudioId.X, 0.5f, 0.01f)` |
| `StopByName("X")` | `StopById(AudioId.X)` |
| `StopByName("X", "Y")` | `StopById(AudioId.X); StopById(AudioId.Y);` |
| `PlayRandom("X", "Y")` | `Play(AudioId.X_or_Y_group)` — see the rule below |
| `PlayOnEnd("X", "Y")` | `PlayOnEnd(AudioId.X, AudioId.Y)` |
| `StopAll()`, `MuteAll()`, `SetGlobalVolume(v)`, `SetBGMVolumes(v)`, `ResetBGMVolumes()` | unchanged |

**`PlayRandom` rule.** Do **not** collapse groups yourself in these tasks — a bank entry with several clips is a bank change, and two agents editing the bank asset in parallel would conflict. Instead migrate each `PlayRandom("X","Y")` to `Play(AudioId.X)` **only if** the group already exists as a single multi-clip bank entry; otherwise leave the call as `PlayRandom(AudioId.X, AudioId.Y)` (the shim accepts it) and **list it in your report**. Task 24 collapses the reported groups in one place. Known groups: `TijeraHit01/02` (6 sites), `PaperCut01/02` (2), `PaperFold01/02` (2), `MagicChannelingLoop01/02` (1), `Pasos_Kami_01..04` (3), `Pasos_KamiMojados_01..04` (2).

**Rules for every sweep task:**

1. **Read each file before editing it.** Some calls sit inside coroutines or `switch` arms where a mechanical replace would break indentation or a `break`.
2. **Change nothing but the audio call.** No renames, no reordering, no "while I'm here" fixes. If you spot a real bug, report it; do not fix it.
3. **If an id has no `AudioId` constant**, the sound does not exist in the bank. Do **not** invent a constant and do **not** delete the call. Leave the line as-is and report it. (`TriggerSound.cs:19` is a known case: it asks for `"4S_MarimbaLoop"` / `"4S_MarimbaLoopConPiano"`, which exist neither in the prefab nor in `Assets/Sounds` — that line throws `KeyNotFoundException` today. Task 24 resolves it with Diego.)
4. **Verify** with `python tools/compile-check.py <task-tag>` and confirm the CS0618 count **dropped by the number of calls you migrated**. That number is the proof the task is complete.
5. **Report**: files touched, calls migrated, CS0618 before/after, plus any `PlayRandom` groups or unknown ids you left behind.

| Task | Folder | Files (calls) | Notes |
|---|---|---|---|
| **13** | `Cortables` | `Arbol2DCortable`(3), `Arbol3DCortable`(2), `CofreCortable`(1), `EntityCortable`(1), `FlorCortable`(1), `HongoCortable`(3), `ObjetoCortable`(2), `PiedraCortable`(1), `PuertaCortable`(2), `RepresaManager`(2), `RocosoCortable`(1), `TijeraHitbox`(1), `VidaCortable`(1) = 21 | Most `PlayRandom` sites live here. `PuertaCortable:11` has a commented-out call — leave the comment alone. |
| **14** | `Player` | `Player`(2), `PlayerView`(14) = 16 | `PlayerView` has the footstep `switch` with 5 `PlayRandom` arms (lines ~471-483). Read the whole method; do not restructure it. |
| **15** | `Origami` | `MultipleRectCheck`(9), `OrigamiObjectSpawner`(1), `OrigamiShip`(1) = 11 | `MultipleRectCheck:332` is the only multi-arg `StopByName`. |
| **16** | `UI` | `CamWheelManager`(2), `DialogueManager`(1), `FlapManager`(13), `OverlayManager`(1) = 17 | **Blocked until Task 4.** Unblocks Tasks 12 and 22 when done. `FlapManager` volume/BGM calls keep their signatures — do not reroute to buses here, that is Task 23. |
| **17** | `Enemies` | `Enemy`(1), `Rocoso`(4), `RocosoDeathState`(1), `RocosoStartState`(1) = 7 | **Skip `GallinaSounds.cs` entirely** — Task 21 owns it. |
| **18** | `TriggerS` | `TriggerAbuelaDropoff`(1), `TriggerSound`(2), `TriggerTijeraPickup`(1), `TriggerViento`(2) = 6 | `TriggerSound:19` is the known bad-id case in rule 3. |
| **19** | `Dialogos` | `AbuelaDialogueTrigger`(3), `GranjeroNorbertoDialogueTrigger`(1), `HongueroTiburcioDialogueTrigger`(1), `QuestDialogueTrigger`(1) = 6 | `HongueroTiburcio` carries a baseline CS0414 — ignore it. |
| **20** | `Managers` + tail | `CameraManager`(1), `EncounterManager`(9), `LevelManager`(9), `MainMenuManager`(7), `PageScrollerManager`(3), `StoryboardCutsceneManager`(2), `InventorySlot`(2), `BarquitoMovingState`(2), `Solapa`(1), `RocosoAplastadoBehaviour`(1) = 37 | **Blocked until Task 4.** Largest task; split into two commits (Managers, then tail) if it helps review. `EncounterManager:77` calls `AudioManager.instance.StopAllCoroutines()` from outside — **replace it with `AudioManager.instance.StopById(...)` for the specific music ids that method is trying to silence**, because the pool's own coroutines must not be killed by an outside caller. Read the method and report what you chose. `EncounterManager:71` has a commented-out `PlayOnEnd` — leave it. |

Each of these tasks ends with the same two steps:

- [ ] **Verify**: `python tools/compile-check.py task<N>` → `RESULTADO: COMPILA OK`, CS0618 count down by the expected number.
- [ ] **Stage and request permission to commit** with message `refactor(audio): migrate <folder> to typed AudioId API`. **Ask Diego before committing.**

---

# Phase 2 — Features and close-out

### Task 21: Chickens cackle, and `GallinaSounds` on the pool

**Files:**
- Modify: `Assets/Scripts/Enemies/GallinaSounds.cs`
- Modify: `Assets/Scripts/AI/GallinaAgent.cs`
- Modify: `Assets/Prefabs/Gallina.prefab`

**Interfaces:**
- Consumes: `AudioManager.PlayAt`, `AudioId.Gallina_Cacareo`, `AudioId.Gallina_Evade`, `AudioId.Gallina_Cortada`, `AudioId.Pasos_Gallina_01/02` (Tasks 7–8).
- Produces: `GallinaSounds.PlayCacareoSound()`, plus the existing `PlayEvadeSound()`, `PlayCortadaSound()`, `PlayPasosSound()` with their names unchanged.

- [ ] **Step 1: Check whether the gallina clips are in the bank**

```bash
grep -nE "Gallina|Pasos_Gallina" Assets/Scripts/Managers/Audio/AudioId.cs
```
The chicken clips (`Gallina_Evade.mp3`, `Gallina_Cortada.mp3`, `Pasos_Gallina_01/02.mp3`) live in `Assets/Sounds/` but were wired directly on the prefab, so they were probably never children of `AudioManager.prefab` and so are **not** in the generated bank. If the constants are missing, ask Diego to add these rows (`Gallina_Evade`, `Gallina_Cortada`, `Pasos_Gallina` with both step clips) with `bus = SFX`, `spatialBlend = 1`, then re-run `Kami/Audio/Regenerate AudioId`. `Gallina_Cacareo` was already added in Task 6.

- [ ] **Step 2: Rewrite `GallinaSounds`**

Method names are load-bearing — animation events and other callers reference them by name — so keep all three and add the fourth:

```csharp
using UnityEngine;

/// <summary>
/// Chicken sounds, played positionally through the pooled AudioManager. The AudioSource fields
/// this used to carry on the prefab are gone: configuration lives in the AudioBank now, and the
/// pool supplies the source.
///
/// The method names are unchanged on purpose -- animation events and other callers reference
/// them by name.
/// </summary>
public class GallinaSounds : MonoBehaviour
{
    public void PlayEvadeSound()
    {
        AudioManager.instance.PlayAt(AudioId.Gallina_Evade, transform.position);
    }

    public void PlayCortadaSound()
    {
        AudioManager.instance.PlayAt(AudioId.Gallina_Cortada, transform.position);
    }

    public void PlayPasosSound()
    {
        AudioManager.instance.PlayAt(AudioId.Pasos_Gallina, transform.position);
    }

    /// <summary>
    /// Cackling when the tree quest completes and the chickens are freed. Called by GallinaAgent
    /// with a small per-chicken delay so a flock overlaps like a real coop instead of firing as
    /// one flat unison hit.
    /// </summary>
    public void PlayCacareoSound()
    {
        AudioManager.instance.PlayAt(AudioId.Gallina_Cacareo, transform.position);
    }
}
```

- [ ] **Step 3: Trigger the cackle from `GallinaAgent`**

Read `GallinaAgent.cs` first: it already subscribes to `EventManager`'s `OnTreeCutForChickens` to move between patrol zones. Extend that existing handler — do not add a second subscription:

```csharp
    [SerializeField, Tooltip("Max random delay before this chicken cackles when the tree is cut, " +
                             "so a flock overlaps instead of firing in unison.")]
    float _maxCacareoDelay = 0.6f;

    //inside the existing OnTreeCutForChickens handler:
    StartCoroutine(CacarearConDelay());
```

```csharp
    IEnumerator CacarearConDelay()
    {
        yield return new WaitForSeconds(Random.Range(0f, _maxCacareoDelay));

        if (_sounds == null)
        {
            Debug.LogWarning($"[GallinaAgent] {name} no tiene GallinaSounds, no cacarea.");
            yield break;
        }
        _sounds.PlayCacareoSound();
    }
```

Resolve `_sounds` the way the class already resolves its own components (a `[SerializeField]` with a `GetComponent` fallback in `Awake` plus a warning, matching the guard-clause convention). Read the file and match it.

- [ ] **Step 4: Remove the AudioSource components from `Gallina.prefab`**

Read the whole prefab first. Remove the `AudioSource` blocks the old `GallinaSounds` referenced, and the now-dangling `_gallinaEvade` / `_gallinaCortada` / `_gallinaPasosArray` serialized fields on its `MonoBehaviour` block. Verify with a before/after `grep -c "^--- !u!"` and confirm no surviving `fileID` points at a removed block:

```bash
grep -c "AudioSource" Assets/Prefabs/Gallina.prefab
```

If the removal looks at all risky, **leave the components in place** (an unused `AudioSource` is harmless), report that, and let Diego delete them in the editor. A working prefab matters more than a tidy one.

- [ ] **Step 5: Verify compilation**

Run: `python tools/compile-check.py task21`
Expected: `RESULTADO: COMPILA OK`.

- [ ] **Step 6: Hand the runtime check to Diego**

Cut the tree in Level 1 page 5: the chickens should cackle from their own positions, slightly staggered, and then walk across as before. Chicken footsteps and evade sounds must still work, and cutting a chicken must still sound.

- [ ] **Step 7: Stage and request permission to commit**

```bash
git add Assets/Scripts/Enemies/GallinaSounds.cs Assets/Scripts/AI/GallinaAgent.cs Assets/Prefabs/Gallina.prefab
```

Proposed message: `feat(audio): chickens cackle on quest complete, positionally through the pool`. **Ask Diego before committing.**

---

### Task 22: Dialogue pencil SFX and per-line sigh

**Blocked until Task 16 is committed** (`DialogueManager.cs` ownership).

**Files:**
- Modify: `Assets/Scripts/Dialogos/DialogueSO.cs` (add one field to `DialogueEvent`)
- Modify: `Assets/Scripts/UI/DialogueManager.cs` (`WriteText`, `EjecutarTypewriter`)
- Create: `Assets/Editor/SoundIdDrawer.cs`

**Interfaces:**
- Consumes: `AudioManager.Play`, `AudioId.Dialogue_PencilWrite`, `AudioBank.Ids`, `SoundIdAttribute` (Tasks 5–8).
- Produces: `DialogueEvent.sighSound`; a bank-id dropdown in the inspector for any `[SoundId]` string field.

- [ ] **Step 1: Add the sigh field**

```csharp
[System.Serializable]
public struct DialogueEvent
{
    [TextAreaAttribute] public string text;
    public Sprite sprite;
    public string speakerName;

    [SoundId]
    [Tooltip("Optional: sound this NPC makes when the line starts (a sigh, a grunt). " +
             "Leave empty for no sound.")]
    public string sighSound;
}
```

Additive on a struct, so every existing `DialogueSO` keeps working with it empty. Do not reorder the existing fields — that would scramble serialized data in every dialogue asset.

- [ ] **Step 2: Write the dropdown drawer**

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Draws a [SoundId] string field as a dropdown of real AudioBank ids, so a sound gets picked
/// rather than typed. "(none)" writes an empty string, which every caller treats as silence.
/// </summary>
[CustomPropertyDrawer(typeof(SoundIdAttribute))]
public class SoundIdDrawer : PropertyDrawer
{
    const string NONE = "(none)";

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.LabelField(position, label.text, "[SoundId] solo va en strings");
            return;
        }

        AudioBank bank = AssetDatabase.LoadAssetAtPath<AudioBank>("Assets/Resources/AudioBank.asset");
        if (bank == null)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        List<string> options = new List<string> { NONE };
        options.AddRange(bank.Ids.Where(id => !string.IsNullOrEmpty(id)).OrderBy(id => id));

        string current = string.IsNullOrEmpty(property.stringValue) ? NONE : property.stringValue;
        int index = options.IndexOf(current);
        if (index < 0)
        {
            //an id that is no longer in the bank: keep it visible so it is obvious it is stale
            options.Add(current + "  (no esta en el banco)");
            index = options.Count - 1;
        }

        int picked = EditorGUI.Popup(position, label.text, index, options.ToArray());
        if (picked != index)
        {
            property.stringValue = picked == 0 ? "" : options[picked];
        }
    }
}
```

- [ ] **Step 3: Play the sigh at line start**

In `WriteText`, right after the speaker and portrait are set for line `i` and before the typewriter runs:

```csharp
            if (!string.IsNullOrEmpty(dialogue.events[i].sighSound))
            {
                AudioManager.instance.Play(dialogue.events[i].sighSound);
            }
```

An id missing from the bank warns once and stays silent (Task 8), so a stale value cannot break a dialogue.

- [ ] **Step 4: Add the pencil sound to the typewriter**

Add the tunable, then hook the reveal loop in `EjecutarTypewriter`:

```csharp
    [SerializeField, Tooltip("Play one pencil-writing sound every N revealed characters. " +
                             "Higher = sparser. 0 disables the pencil sound.")]
    int _charsPerPencilSound = 2;
```

Inside the inner `while` that increments `visibles`, after `visibles++`:

```csharp
                //one sound every N characters, skipping whitespace and punctuation so it reads
                //as writing rather than a metronome. The bank entry caps maxSimultaneous, so this
                //cannot machine-gun no matter how fast _charsPerSecond is.
                if (_charsPerPencilSound > 0 && visibles % _charsPerPencilSound == 0 &&
                    EsCaracterQueSuena(textElement, visibles - 1))
                {
                    AudioManager.instance.Play(AudioId.Dialogue_PencilWrite);
                }
```

```csharp
    /// <summary>
    /// Whether the character just revealed should make a pencil sound. Spaces and punctuation
    /// do not: the gaps are what makes it sound like handwriting.
    /// </summary>
    static bool EsCaracterQueSuena(TMPro.TextMeshProUGUI textElement, int index)
    {
        if (index < 0 || index >= textElement.textInfo.characterCount)
        {
            return false;
        }

        char c = textElement.textInfo.characterInfo[index].character;
        return !char.IsWhiteSpace(c) && !char.IsPunctuation(c);
    }
```

Nothing extra is needed for the skip path: `CompletarTypewriter` sets `_isTyping = false`, which exits the loop, so no further pencil sounds fire.

- [ ] **Step 5: Verify compilation**

Run: `python tools/compile-check.py task22`
Expected: `RESULTADO: COMPILA OK`.

- [ ] **Step 6: Generate the meta file**

Run: `python tools/make-meta.py Assets/Editor/SoundIdDrawer.cs`

- [ ] **Step 7: Hand the runtime check to Diego**

Talk to any NPC: the pencil should tick along with the letters and stop when the line finishes. Press E mid-line: the text completes and the pencil stops immediately. Then set a `sighSound` on one line of one `DialogueSO` (the dropdown should list real ids) and confirm it plays once at that line and never on the others. `_charsPerPencilSound` and the bank entry's volume/pitch are the two dials if the rhythm feels wrong.

- [ ] **Step 8: Stage and request permission to commit**

```bash
git add Assets/Scripts/Dialogos/DialogueSO.cs Assets/Scripts/UI/DialogueManager.cs Assets/Editor/SoundIdDrawer.cs Assets/Editor/SoundIdDrawer.cs.meta
```

Proposed message: `feat(audio): pencil writing sound in the typewriter and optional per-line NPC sigh`. **Ask Diego before committing.**

---

### Task 23: Route the settings sliders through buses

**Blocked until Task 12 is committed** (`FlapManager.cs` ownership).

**Files:**
- Modify: `Assets/Scripts/UI/FlapManager.cs:261-265` (`SLIDER_Volumen`) and `:54-66` (open/close ducking)
- Modify: `Assets/Scripts/Managers/AudioManager.cs` (only if a mixer-parameter path is needed)

**Interfaces:**
- Consumes: `AudioManager.SetBusVolume`, `SetGlobalVolume`, `SetBGMVolumes`, `ResetBGMVolumes` (Task 8); the `AudioMixerGroup` references Diego assigns (spec §8.1).
- Produces: nothing.

- [ ] **Step 1: Confirm the mixer exists**

```bash
find Assets -name "*.mixer"
```
If empty, Diego has not done spec §8.1 yet. **Stop and ask him**, because there is nothing meaningful to route to. Everything already works via the code-side fallback, so this task simply waits — it blocks nothing.

- [ ] **Step 2: Keep the existing call shapes**

`SLIDER_Volumen` keeps calling `SetGlobalVolume(_sliderVolumen.value)` and the ducking keeps calling `SetBGMVolumes(0.4f)` / `ResetBGMVolumes()`. Those already mean "one Music bus level" after Task 8, so **the likely correct diff here is zero lines in `FlapManager`**. Verify that by reading it; if it is zero, say so plainly rather than inventing a change.

- [ ] **Step 3: Move volume onto the mixer inside `AudioManager`**

Where a bus has a mixer group assigned, prefer setting the group's exposed volume parameter over per-source volume — that is the whole point of having buses, and it also means sounds that start later inherit the right level. Requires Diego to expose the four volume parameters on the mixer (name them `MusicVolume`, `SFXVolume`, `UIVolume`, `AmbienceVolume`) and to say he has done it. Keep the per-source fallback for the no-mixer case; do not delete it.

Use `AudioMixer.SetFloat(param, Mathf.Log10(Mathf.Max(0.0001f, linear)) * 20f)` — mixer volumes are decibels, and a linear slider value assigned directly sounds wrong (barely audible until the very top of the slider).

- [ ] **Step 4: Verify compilation**

Run: `python tools/compile-check.py task23`
Expected: `RESULTADO: COMPILA OK`.

- [ ] **Step 5: Hand the runtime check to Diego**

Move the volume slider across its whole range and confirm it feels even rather than jumping at the end (that is the dB conversion doing its job); open and close the Flap and confirm the music ducks and comes back; mute with M and confirm everything goes quiet.

- [ ] **Step 6: Stage and request permission to commit**

```bash
git add Assets/Scripts/UI/FlapManager.cs Assets/Scripts/Managers/AudioManager.cs
```

Proposed message: `feat(audio): route volume settings through mixer buses`. **Ask Diego before committing.**

---

### Task 24: Delete the shims — the completeness gate

**Blocked until Tasks 13–20 are all committed.**

**Files:**
- Modify: `Assets/Scripts/Managers/AudioManager.cs` (delete the obsolete region)
- Modify: whichever files the resulting compile errors point at
- Modify: `Assets/Resources/AudioBank.asset` (collapse the reported `PlayRandom` groups)

**Interfaces:**
- Consumes: the sweep reports from Tasks 13–20.
- Produces: an `AudioManager` with no string-keyed legacy API.

- [ ] **Step 1: Collapse the `PlayRandom` groups in the bank**

Using the groups reported by Tasks 13–20, ask Diego to add one multi-clip entry per group and re-run `Kami/Audio/Regenerate AudioId`:

| New entry | Clips | Replaces |
|---|---|---|
| `TijeraHit` | `TijeraHit01`, `TijeraHit02` | 6 call sites |
| `PaperCut` | `PaperCut01`, `PaperCut02` | 2 |
| `PaperFold` | `PaperFold01`, `PaperFold02` | 2 |
| `Pasos_Kami` | `Pasos_Kami_01..04` | 3 |
| `Pasos_KamiMojados` | `Pasos_KamiMojados_01..04` | 2 |

Keep the individual entries too — `MagicChannelingLoop01/02` are looping sounds started and stopped by id in `MultipleRectCheck`, so they must **not** be collapsed.

- [ ] **Step 2: Migrate the remaining `PlayRandom` call sites**

Replace each reported `PlayRandom(AudioId.X, AudioId.Y)` with `Play(AudioId.<Group>)`. `PlayerView`'s footstep `switch` (~lines 471-483) collapses to two calls — one for wet, one for dry — but **read the method first**: the arms alternate deliberately to avoid repeating the same clip twice in a row, and a multi-clip bank entry already randomises. Preserve any per-arm pitch.

`MultipleRectCheck:311`'s `PlayRandom("MagicChannelingLoop01", "MagicChannelingLoop02")` picks one of two loops and `:332` stops both. Keep both ids and migrate to `Play(AudioId.MagicChannelingLoop01)` or `02` chosen with `Random.Range` at the call site — a comment must say why this one is not a bank group (the caller needs to stop a specific loop by id).

- [ ] **Step 3: Delete the obsolete region**

Remove the whole `// ---- obsolete compatibility shims` block from `AudioManager.cs`.

- [ ] **Step 4: Run the gate**

Run: `python tools/compile-check.py task24`

Every error is a call site the sweep missed. Fix each with the Task 13–20 transformation rules, then re-run. **Expected end state: `RESULTADO: COMPILA OK` with zero CS0618 and only the three baseline warnings.** That zero is the proof no sound was silently dropped.

- [ ] **Step 5: Resolve the two known bad ids with Diego**

`TriggerSound.cs:19` plays `"4S_MarimbaLoop"` and `"4S_MarimbaLoopConPiano"`, which exist neither in the prefab nor in `Assets/Sounds/` — that line throws today. Ask Diego which he wants: delete the call, or add the clips. **Do not choose for him**, and do not leave a call that cannot compile.

- [ ] **Step 6: Confirm the old API is gone**

```bash
grep -rn "PlayByName\|StopByName\|PlayRandom\|soundDict" Assets/Scripts --include=*.cs
```
Expected: no hits.

- [ ] **Step 7: Stage and request permission to commit**

```bash
git add Assets/Scripts/Managers/AudioManager.cs Assets/Resources/AudioBank.asset Assets/Scripts
```

Proposed message: `refactor(audio): delete legacy string audio API`. **Ask Diego before committing.** Remind him of spec §8.4: the `AudioSource` children of `AudioManager.prefab` can now be retired, but only after he has played through and is happy.

---

### Task 25: Documentation

**Blocked until Tasks 1–24 are committed.**

**Files:**
- Modify: `CLAUDE.md`, `docs/claude/audio-y-particulas.md`, `docs/claude/controles-y-gamepad.md`, `docs/claude/nivel2-y-ui.md`, `docs/claude/quests-y-dialogos.md`
- Modify: `docs/superpowers/specs/2026-09-10-audio-controls-refactor-design.md` (record what changed from the design)

**Interfaces:** none.

Keeping docs current is part of finishing the work in this project, not a separate chore.

- [ ] **Step 1: `docs/claude/audio-y-particulas.md`**

Rewrite the `AudioManager` section. The current text says a new sound means adding a child GameObject with an `AudioSource` to the prefab and that the `PlayByName` string must match that name — **both are now wrong**. Replace with: a new sound means a row in `Resources/AudioBank.asset` plus re-running `Kami/Audio/Regenerate AudioId`; ids are typed constants; the pool has a size and per-entry `maxSimultaneous`; buses route to `KamiMixer`. Keep the `TijeraHitbox` / `TijeraMiss` and `ParticleShooter` sections untouched — nothing in this work changed them.

- [ ] **Step 2: `docs/claude/controles-y-gamepad.md`**

Update the "El tab Controles del Flap YA se traduce solo" section: the tab is now a diagram, not a text list, and `controlsText` is deactivated but still in the tables. Add a short "Controls diagram" section covering the settings asset, the placeholder behaviour, the device switch, the live highlight, and the note that B/L1/R1/Start highlights only blink because those buttons also drive Flap navigation. Add the new `InputHub` held-state properties to the architecture section.

- [ ] **Step 3: `CLAUDE.md`**

Add `Assets/Scripts/Managers/Audio/` and `Assets/Scripts/Managers/Scenes/` to the "Dónde está todo" list. Add a line to "Ojo al editar" about `AudioId.cs` being generated (never hand-edited) and scenes being referenced through `GameScene` / the catalog rather than by name.

- [ ] **Step 4: `nivel2-y-ui.md` and `quests-y-dialogos.md`**

In `nivel2-y-ui.md`, note that the Flap's volume slider moves a mixer bus. In `quests-y-dialogos.md`, note the optional `DialogueEvent.sighSound` and the typewriter pencil sound.

- [ ] **Step 5: Record design deltas in the spec**

Append a short "What changed during implementation" section to the design doc, with the reasons. Candidates seen while planning: the two known bad audio ids, whatever was decided about the `Gallina.prefab` `AudioSource` removal, and any hotspot rects re-tuned during Diego's playtest. The spec is the memory of the design, not a frozen document.

- [ ] **Step 6: Regenerate the knowledge graph**

Run: `/graphify Assets/Scripts --update`

Justified here: new managers (`AudioPool`, `AudioBank`, `SceneCatalog`, `ControlsDiagram`) and new dependencies between systems — exactly the "architecture change" trigger in the modus operandi.

- [ ] **Step 7: Stage and request permission to commit**

```bash
git add CLAUDE.md docs
```

Proposed message: `docs: update audio, controls and scene docs for the refactor`. **Ask Diego before committing.**

---

## Self-Review

**Spec coverage:** §4.1 → Tasks 10, 12. §4.2 → Task 9 (and the `Accion` promotion in Task 1). §4.3 → Task 10 Step 3. §4.4 → Task 10 Step 4. §4.5 → Task 1 Step 4 + Task 10 Step 2. §4.6 → Task 11. §5.1 → Task 2. §5.2 → Task 4. §5.3 → Task 3. §6.1 → Task 5. §6.2 → Task 6. §6.3 → Task 8. §6.4 → Task 7. §6.5 → Tasks 13–20, gated by 24. §6.6 → Task 23. §7.1 → Task 22. §7.2 → Task 22 (+ the `[SoundId]` type in Task 5). §7.3 → Task 21. §8 → Diego steps inside Tasks 3, 6, 9, 11, 23, 24. §9 → the verify step of every task. §10 risks → the pool steal warning (Task 8), the CS0618 gate (Task 24), the codegen collision guard (Task 7), the catalog validator (Task 3). §11 deferred → recorded in `pending-issues.md` as #41.15, not in this plan.

**Gap found and closed:** the spec's §6.3 `IsPlaying` was needed by `OnGlobalVolumeChanged` but was not in the original interface list — it is now declared in Task 8's Produces block.

**Known soft spots**, flagged rather than papered over:
- Task 9's hotspot rects are first-pass numbers matched to a procedural placeholder. They will need re-tuning against real art; the spec already accepts this.
- Task 11 prefers Diego adding localization keys via Unity's Localization window over hand-editing table YAML, because key ids are generated and a hand-allocated collision is hard to spot.
- Task 23 may legitimately be a zero-line diff in `FlapManager`; the task says to report that rather than invent work.
- `Accion.Mover`'s "held" state is a stick/WASD magnitude check, not a button — deliberate, and the reason `MoveAxisRaw` exists rather than a `MoverHeld` bool.
