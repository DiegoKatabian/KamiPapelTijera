# Spec 016 — Demo playtest fixes (2026-10-14)

Part of the Level 1 Steam demo (spec 015, epic [#207](https://github.com/DiegoKatabian/KamiPapelTijera/issues/207), milestone M8).
Source: Diego's playtest of the first local build (`tools/steam/run-local.bat`). Everything below is **built and compile-checked, uncommitted,
not run in the Editor or a build**. Issues #211-#216.

| # | Finding | Root cause | Change | Check |
|---|---|---|---|---|
| [#211](https://github.com/DiegoKatabian/KamiPapelTijera/issues/211) | Abuela half sunk and "CANNOT MOVE" on the unfold | `PageNavMeshManager` indexes pages from 0; `NavMesh Pag 1 Kami` was entered as index 1 (page 2), so page 1 had no NavMesh and her agent never snapped to the ground | Scene `PageNavMeshManager` entry 0: `pageIndex` 1 -> 0 (`Pag 5` is index 4 and `Pag 6` index 5, those were right) | Log shows `NavMesh activado para pagina 0`; she stands, walks to the drop-off; no `CANNOT MOVE` |
| [#212](https://github.com/DiegoKatabian/KamiPapelTijera/issues/212) | Music a touch low; Tiburcio's quest sound played twice | Sound design call, same `QuestCompleted02` on tree fall and on delivery | Music x1.122 (+1 dB) on the 4 Estampes entries; new `QuestCompleted02Pitched` (-3 dB = x0.708) played at pitch 2 by the tree and the dam; delivery unchanged | Listen: tree + dam pitched and quieter, delivery normal |
| [#213](https://github.com/DiegoKatabian/KamiPapelTijera/issues/213) | Wanted R1 run, R2 camera | Feature | `Run` also on button 5; new axis `CameraTriggerRight` (R2, 10th axis) = **next** camera, L2 = **previous**; `InputHub.CambiarCamaraAtrasDown`, `CameraManager.CycleCamera(+-1)`; prompts show `L2 / R2`; `InputHub` logs the pad names | Xbox-layout pad: R1/L1 run, R2 forward, L2 back |
| [#214](https://github.com/DiegoKatabian/KamiPapelTijera/issues/214) | Flap tab hint is loose text | Text sat on a bare GameObject | `FlapTabHint`: `Dialogov3` panel (sliced, multiplier 4) + child `Text`; table entry `< {INPUT:cambiarseccion} >` in es/en/pt | Check the `<` renders, panel proportions, text colour |
| [#215](https://github.com/DiegoKatabian/KamiPapelTijera/issues/215) | Missing-script components | Leftovers with no `.meta` in git | 12 dead components removed (Level 1: 1, Level 2: 11); full scan of scenes and prefabs found no others (Spine Examples hits are false positives) | Level 1/2 load without the warning |
| [#216](https://github.com/DiegoKatabian/KamiPapelTijera/issues/216) | GameCube adapter: UI stuck on the intro dialogue | Legacy Input Manager maps by button number; a third-party adapter numbers its buttons differently | Only logging added: `[InputHub] joysticks reported by the system: ...` | See the plan below |

## Decisions (Diego)

- Quest sound: **tree and dam pitched up at -3 dB, delivery unchanged.**
- Camera: **R2 = next, L2 = previous** (L2 used to be "next").
- Music +1 dB: **only the Estampes game music**, not the menu or storyboard loops.
- GameCube pad: a **third-party adapter** (Mayflash-style).

## The GameCube pad: what we know and the plan

The prompt switched to `(A)`, so the game saw a joystick; pressing A did nothing, so the adapter's A is not "joystick button 0".
We cannot map what we cannot see. Plan, in order:

1. **Launch the build through Steam** (beta branch install), with Steam Input on for the app: Steam Input presents a generic or
   Nintendo pad to the game as an Xbox controller, which is the layout the game already supports. This needs, on the Steamworks app,
   a Steam Input configuration (the Gamepad template) and controller support set to Full. Outside the project: Diego.
2. If it still fails, read the new `[InputHub] joysticks reported ...` line in `Player.log` plus which button numbers the adapter sends,
   and add a per-device mapping (or tell players to map the pad in Steam's controller settings).
3. Quick check meanwhile: does **keyboard** E/Enter still advance the dialogue with the adapter plugged in? If it does not, the
   cause is not button numbering (an axis stuck at -1 on the adapter would be the next suspect).

## Not done / worth knowing

- The `<` and `>` around the icons: TextMeshPro should print a lone `<` as text, but nobody has seen it yet.
- R2 is the 10th axis on Windows XInput; other drivers (and Steam Input's virtual pad) may number triggers differently.
- ScriptableObject `.asset` files were not scanned for missing scripts.
- `Abuela` fix assumes `NavMesh Pag 1 Kami` was baked over the unfold point. If she is still off the mesh with the log line present, rebake it.
