# Spec 015 — Steam demo: Level 1 only

Epic: [#207](https://github.com/DiegoKatabian/KamiPapelTijera/issues/207), milestone **M8 — Steam demo (Level 1)**.
Status: **Phase A built 2026-10-13 (uncommitted, compile-checked only, nothing run in the Editor or a player build).
Phases B and C are Diego's.** Written from a read-only audit, then implemented after Diego's answers.

## 1. Goal

A Windows build with **only Level 1** (MainMenu -> Level 1 -> Level 1 end cutscene -> MainMenu), uploaded through
SteamPipe to the **Steam demo app** (a separate app from the full game), with Steam actually initialised, nothing
from Level 2 reachable, and nothing visibly unfinished in Level 1.

| Finish line | What it needs |
|---|---|
| **Upload** to a private beta branch and install it from the Steam client | Phase A, B.1-B.2, the first build. No store page needed: art and details can be improved after the upload. |
| **Release the demo** | Demo store page + build reviewed and approved (page review 3-5 business days); the full game's page must be at least "Coming soon" if the demo goes first; then re-publish the full game's page so the Download Demo button appears (Valve's demo docs, 2026-10-13). |

## 2. Decisions (Diego, 2026-10-13)

- **D1** It is a **demo, Level 1 only**, on its **own Steam demo app**. **The demo's App ID is 3284350** (Diego, 2026-10-14; also `steam_appid.txt`). The full game's own app does not exist yet.
- **D2** The full game's store page is submitted and working.
- **D3** No Rocoso cutscenes (#35) in the demo.
- **D4** No Wardrobe tab (and no blank default-outfit bag item) in this build.
- **D5** Company name `kimmiarts`; version bumped to **0.2.0**, shown as small text bottom-left of the main menu.
- Existing GitHub issues are not part of this work (Diego: ignore them). No save system needed.
- Resolved 2026-10-14: `SteamAppIds.Demo = 3284350`. `FullGame = 0` (no such app yet): any non-demo build falls back to the demo id.
- Depot ID **3284353** ("Kami Paper Scissors - November Demo Depot", Diego 2026-10-14), set in `tools/steam/`.

### How the two Steam apps relate (so the code is right)

A Steam demo has its own App ID, its own depots and its own builds, associated with the full game on the partner
site ("Demos are a separate App ID that is associated with your full game's App ID"). The demo build is uploaded to
the **demo's** App ID only; nothing in Valve's docs asks the game to know both. The game passes **its own** app's id to
`SteamAPI.RestartAppIfNecessary`; Valve's page does not say which id a demo passes, so the demo id is the working
assumption, to be confirmed by the install test (B.4): if the exe loops relaunching through Steam, revisit it.
`SteamAppIds` (`Assets/Scripts/Steamworks.NET/`) holds both ids; the demo build defines `KAMI_STEAM_DEMO` and gets the demo id.

## 3. Audit findings (state before the work)

Verified by reading the repo; `python tools/compile-check.py` compiled clean. **Not verified**: a player build, a Level 1
playthrough on the current skeleton, anything on the Steamworks partner site.

- **Steam was wired but off.** Steamworks.NET 2024.8.0 (SDK 1.60, DLLs in `Assets/com.rlabrecque.steamworks.net/Plugins`),
  `STEAMWORKS_NET` define set, `SteamManager.cs` present, `steam_appid.txt` at the repo root (outside `Assets/`, so it never
  ships). But `SteamManager` was in **no scene** and nothing used it: Steam never initialised.
- **Build Settings** had both Level 2 scenes ticked. `Nivel1_EndCutscene` returns to MainMenu. Cheats are off in every scene.
- **Localization is on Addressables.** The game code never uses Addressables, but Unity Localization 1.3.2 depends on
  Addressables 1.19.19 and stores its tables in the `Localization-*` groups. `m_BuildAddressablesWithPlayerBuild` was 0
  ("follow the per-machine Preferences value"), so a build might ship without any text.
- **Rain boots:** `Gear_RainBoots` -> `feet/rain-boots`, a skin that does not exist in Atlas 13 (skipped with one warning;
  the water protection still worked because it follows the equipped item). `Zapatos/Zapatos_Upgrade_1` is the dark base
  shoes with a yellow multiply tint: not boots.
- **Wardrobe/outfit:** Level 1's bag got an "Outfit (default)" item with no icon, and the Flap showed the placeholder Wardrobe tab.
- **Locales:** only en and es exist. Portuguese tables and groups are dead weight (#204).
- No save system (and none needed), no achievements (none needed).
- Level 2's `CoffeeChair`/`Kami_Bench` missing-script warnings are in `Level2_Newspaper` only: not in the demo (#203).
- No Steamworks SDK / SteamCMD on this PC. Headless Unity is blocked by licensing: builds are made from the Editor menu.

## 4. What was built (Phase A, 2026-10-13)

All in the working tree, **uncommitted**, on branch `level2-polish` next to Diego's own uncommitted Level 2 work. The demo is
a scene list plus a define, not a branch, so Build Settings and Level 2 development are untouched.

| Issue | Change |
|---|---|
| #191 A.1 | `SteamManager` GameObject in `MainMenu.unity` (appended; scene 294 -> 300 blocks); `SteamAppIds.cs` (`Demo = 3284350`, `FullGame = 0` = not created, `Current` by `KAMI_STEAM_DEMO`); `SteamManager.cs` passes `SteamAppIds.Current` |
| #192 A.2 | `Assets/Editor/SteamDemoBuild.cs`, menu **Kami > Build > Steam Demo (Level 1)** -> `Builds/SteamDemo/` |
| #193 A.3 | `m_BuildAddressablesWithPlayerBuild: 1` |
| #206 A.4 | `tools/add-placeholder-rain-boots.py` rewritten for Atlas 13; skin `Zapatos/Zapatos_RainBoots_Placeholder` added to `Atlas 13/skeleton.json`; `Gear_RainBoots._spineSkin` repointed |
| #194 A.5 | `GearLoadout._hasWardrobe` (Level 1 = off); `FlapManager` hides tab index 4 and R1/L1 skip it; `Player.GrantStartingLoadout` skips the worn outfit; `Player.StartingLoadout` property |
| #195 A.6 | `companyName: kimmiarts`, `bundleVersion: 0.2.0`, `VersionLabel` (+ GameObject in `MainMenu.unity`) |
| #197 B.2 | `tools/steam/`: `app_build_demo.vdf`, `app_build_demo_preview.vdf` (Preview 1), `depot_build_demo.vdf` (depot id **3284353**), `upload-demo.bat [preview]`; the SDK is at `..\steamworks sdk` (outside the repo). No `SetLive`: pick the branch in Steamworks after the upload. |

Verification so far: `tools/compile-check.py` clean (the two baseline warnings); the Editor assembly (`SteamDemoBuild.cs`)
compiled by hand against Unity's references (one CS0162 warning that disappears once `Demo` is non-zero); skeleton JSON still
parses. **Not run:** anything in the Editor or a build. First things to look at: the boots' look (A.4), the Flap's four tabs (A.5),
the version label (A.6), Steam initialising (A.1), text in the build (A.3).

Note for later: `FlapManager.cs`, `Player.cs`, `Level2_Newspaper.unity` etc. also carry Diego's own uncommitted changes.
When committing, stage the Steam demo files separately (the list is in the table above plus `ProjectSettings`,
`AddressableAssetSettings.asset`, `Level1_StartLoadout.asset`, `Gear_RainBoots.asset`, `skeleton.json`, `tools/`).

### Added 2026-10-14

- **A.7 (#208) Licensed music out.** MemoFlora -> Estampes Pagodes in the AudioBank (loudness-matched volumes), boss theme plays intro then loop, `Assets/Sounds/MemoFlora/` deleted. Details in the issue.
- **Licensed clips never ship in the demo.** The AudioBank is in Resources, so everything it references ships. The build menu item empties the Level 2 music entries (`BohrenDestroyingAngels`, `DeadEndAngels`) and the Sims-named `QuestCompleted`/`NewsJingle` clip for the length of the build, then restores them. The provenance of the Level 1 clips (menu music, ambience, UI piano, Cathedral) is Diego's to confirm (#209).
- **Testing the build locally.** Double-clicking the exe from `Builds/SteamDemo` hands the launch to Steam (`RestartAppIfNecessary`, no `steam_appid.txt` next to the exe), which starts the version installed in the library: the old build. Use `tools/steam/run-local.bat` (copies `steam_appid.txt` in and starts the exe); `upload-demo.bat` removes it again before uploading.

## 5. What is left

### Phase B — outside the project (Diego)

- **B.1 (#196) Demo app.** On the partner site create/confirm the demo app (type **Demo**, base game = the full game's app, when it exists), one Windows
  depot, launch option `KamiPapelTijera.exe` (Windows 64-bit), the app in a package, a private beta branch. **Done: demo App ID 3284350, depot 3284353.**
- **B.2 (#197) SDK and scripts.** Download the Steamworks SDK (partner site, login) to a short path, use `tools\ContentBuilder`:
  `app_build_<demoAppId>.vdf` (ContentRoot = `Builds/SteamDemo`, SetLive = the beta branch) and `depot_build_<depotId>.vdf`
  (exclude `*.pdb`, `*_BurstDebugInformation*`, `steam_appid.txt`); a `Preview 1` run first, then
  `steamcmd +login <account> +run_app_build ..\scripts\app_build_<id>.vdf +quit` (Steam Guard code). The `default` branch cannot be set live by
  script, hence the beta branch.
- **B.3 (#198) Store assets and page.** Not needed to upload. Needed to release the demo; sizes are in the issue.
- **B.4 (#199) Install test** from the beta branch on another PC.

### Phase C — gates

- **C.1 (#200)** first player build (Diego runs the menu item) and a smoke test outside the Editor.
- **C.2 (#205)** Level 1 playthrough in the build (the scene was last changed 2026-10-07, Kami moved to Atlas 13 on 2026-10-06).
- **C.3 (#201)** submit and release the demo, then re-publish the full game's page.

### The night's sequence (if B.1 is done)

1. Diego: B.1 (App ID to me) and B.2 (SDK download).
2. I put the id in `SteamAppIds`; Diego opens Unity (reload MainMenu if it was open), checks A.4/A.5/A.6 by eye, runs the menu item.
3. Diego: smoke-test the folder, `Preview 1`, then the real upload; B.4.

## 6. Out of scope

Level 2 and its content, achievements/stats/cloud saves, a save system, rebinding (#57), Linux/macOS builds, Steam Deck
verification, Portuguese, Valentino's real rain boots (#202), Rocoso cutscenes (#35).
