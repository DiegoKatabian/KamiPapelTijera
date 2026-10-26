# Spec 023: The settings menu (main menu + Flap) and audio channels

**Status**: Draft, 2026-10-26. Designed with Diego during the EVA feedback triage (a mockup of the four screens was
shown in chat and approved). Nothing built. Epic **#305**, tasks #306-#314 (0.A-0.B = #306-#307, 1.A-1.D = #308-#311, 2.A-2.C = #312-#314). Milestone M9. Branch: none yet.
Siblings: 022 (flying stickers), 024 (EVA feedback batch).

## Request (Diego, 2026-10-24/26)

1. On the **main menu**, Esc opens a settings menu that **fades in at the centre of the screen** (not sliding from the
   top like the Flap). Mobile will pull it from an edge like the Flap; that is for the port and is **not built now**,
   but nothing here may make it harder.
2. It holds **audio, image and language** only.
3. Audio settings expand from one volume slider to **Master + channels**. Too much clutter for one screen, so the menu is
   a **list you tap to enter a submenu**.
4. The in-level **Flap's Settings tab must follow the same structure** ("keep coherence").

## Goal (what Diego sees)

> Main menu, I press Esc (or Start on the pad): the menu fades in, centred, over the title. Three rows: Audio,
> Image, Language. Audio opens a screen with a Master slider on top and Music, Effects, Interface, Ambience below.
> Image has Brightness and Contrast. Language has Espanol, English, Portugues; tapping one applies it. Esc / B goes
> back one level, and from the list it closes. In a level, the Flap's Settings tab shows the same Audio sliders.

## Approved structure (mockup, 2026-10-26)

| Screen | Contents |
|---|---|
| Root | Audio >, Image >, Language > ; footer "Esc / B to close" |
| Audio | back arrow + breadcrumb "Settings / Audio"; **Master** (emphasised, top), divider, **Music**, **Effects**, **Interface**, **Ambience** |
| Image | Brightness, Contrast (fullscreen and resolution join them on PC later) |
| Language | Espanol (check), English, Portugues; applies on tap, no confirm button |

Names: Effects = the `SFX` bus, Interface = the `UI` bus (Diego approved the friendlier names; localise them).
Navigation: mouse and gamepad both; B goes up one level, from the root it closes; the sliders reuse the animated
"chicken" handle that already works with the stick.

## What exists today (checked, 2026-10-26)

- `AudioManager` (`Managers/Audio/`): buses `Music`, `SFX`, `UI`, `Ambience`; API `SetBusVolume(AudioBus, v)`,
  `SetGlobalVolume(v)`; mixer groups with exposed params `MusicVolume`, `SFXVolume`, `UIVolume`, `AmbienceVolume`.
  Bus volume is applied **per source in code** (the optional "Task 23" of the audio refactor would move it onto the
  mixer parameters). There is a **global volume**, which is the natural Master.
- `FlapManager` has a Settings display: brightness, contrast and volume sliders (the "gallina" handle,
  `SliderFlap.prefab`) plus the exit confirmation. Brightness/contrast are the ones Image takes over.
- `Assets/Scripts/Localization/LocaleSelector` and the main menu's language handling exist (to read before building).
- `MainMenuManager` runs the main menu; `InputHub` already exposes the `Options` axis (Esc/O, Start). What the main
  menu does today on Esc was **not** checked: read it first.
- Persistence of these settings was **not** checked (PlayerPrefs? nothing?). See open question 1.
- Rule from the project: design values (default volumes, fade time) live in an Inspector-editable asset, not in scripts.

## Design

### 1. One reusable panel stack

A `SettingsPanel` component (prefab) = a stack of screens (root, Audio, Image, Language) with `Push/Pop`, a breadcrumb
and the back behaviour. It owns no game logic; each screen is a small component that binds sliders to settings. The
**main menu** instances it as a centred overlay with a `CanvasGroup` fade (`_fadeSeconds` in an asset). The **Flap**
reuses the *screens* (not the centred frame) inside its Settings tab, so the Audio and Image screens are literally the
same prefabs in both places: coherence by construction, not by keeping two copies equal.

### 2. Audio model

- **Master** = `SetGlobalVolume`; channels = the four buses. Effective volume of a source = bus x master (verify how
  the two combine today before changing anything; they must stay multiplicative so a channel at 100% under Master 50%
  is half as loud).
- Defaults per channel in the settings asset. Music's existing `SetBGMVolumes` / `ResetBGMVolumes` (the Flap lowers music
  to 0.4x while open) must keep working on top of the user's Music setting: ducking multiplies, it does not overwrite.
- Mute (M) keeps working; its interaction with Master must be decided (open question 2).

### 3. Image and Language

Image: move the existing brightness/contrast sliders' bindings, no new behaviour. Language: reuse `LocaleSelector`;
selecting applies immediately and refreshes localised text (the Flap's tabs and the menu itself already respond to the
device/locale events: confirm).

### 4. Input

Esc / Options opens only when no other main-menu state owns it (the level select, a dialogue, the intro storyboard).
The EventSystem's Submit/Cancel already map to A/Start: B for Back needs the same arbitration care as the rest of the
joystick work (`controles-y-gamepad.md`, gotchas 1 and 6: a press that closes a menu must not leak to the world; in the
main menu there is no world, but the level select and New Game buttons are right behind it).

## Non-goals

Mobile edge-pull variant; controls rebinding (#57); graphics quality presets; resolution/fullscreen (PC later).

## Tasks (phased; `[P]` = parallel-safe)

**Phase 0 - audio model**
- 0.A Master + four channels in `AudioManager` (+ settings asset for defaults), ducking still works, mute decided.
- 0.B Persistence (per open question 1).

**Phase 1 - the panel** (0.A not needed to start the shell)
- 1.A `SettingsPanel` prefab: stack navigation, breadcrumb, centred fade, mouse + gamepad focus. `[P]` with 0.A
- 1.B Audio screen (Master + channels, chicken handles). needs 0.A, 1.A
- 1.C Image screen (brightness/contrast). `[P]` with 1.B
- 1.D Language screen. `[P]` with 1.B

**Phase 2 - wiring**
- 2.A Main menu: Esc / Start opens the panel, B / Esc goes back, nothing leaks to the buttons behind.
- 2.B Flap Settings tab uses the same screens (Audio, Image, Language); the exit confirmation stays. Both levels.
- 2.C Docs: `CLAUDE.md`, `docs/claude/audio-y-particulas.md` (buses and Master), `controles-y-gamepad.md`, this spec's "As built".

## Verification

Compile-check only from here. Playtest checklist: Esc on the main menu (fade, centre), each submenu with mouse and
pad, Master vs a channel (loudness math), music while the Flap is open, language switch live, brightness/contrast
carry into a level, settings survive a restart (if persisted), the Flap's tab in Level 1 and Level 2.

## Open questions (defaults in bold)

1. Persist settings between sessions? **Yes, PlayerPrefs, saved on change** (confirm what the game does today).
2. Does mute (M) mute Master, or only toggle a flag that restores the old Master? **A flag over Master: unmuting returns to the same level.**
3. Channel list final? **Music, Effects, Interface, Ambience** (the four buses that exist; add Dialogue/Footsteps later as a sub-screen "Audio > Mixer" if ever needed).
4. Should the root also carry "Controls" (the Flap has a Controls tab)? **No: the main-menu menu is audio, image, language only (Diego).**
