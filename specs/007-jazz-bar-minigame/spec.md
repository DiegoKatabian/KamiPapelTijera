# Feature Specification: Jazz bar jam session + the saxophone

**Feature Branch**: `007-jazz-bar-minigame` (branches from `pages-blocking`, like spec 006)
**Created**: 2026-09-28
**Status**: Designed, not started
**Input**: Diego, 2026-09-28: "mini juego de la música en el bar abajo de la casa de Natalia.
Durante n segundos, tocá todo el jazz que puedas. El input es completamente libre, todas las teclas
hacen jazz (una nota de saxofón al azar dentro de la escala de la canción que suena de fondo). La
intención es que el jugador pueda darle rienda suelta a tocar todos los botones, y cuanto más,
mejor. Si supera el puntaje mínimo (8 teclas cualquiera), gana. El premio es un saxofón, que Kami
puede usar desde el inventario para entrar a modo saxo y tocar jazz cuando quiera."

This is the GDD's "sidequest 2" (`docs/GDD.md` §5.2, page 3: *"un minijuego de ritmo en el bar...
al tempo de la música"*). Diego's version drops the rhythm/timing idea: it is **free play**, no
timing judgement, more presses = better.

## Context (triangulated 2026-09-28)

- **The bar exists in the blocking**: `Page 3/Natalia's building/Bar` (bar counter, stools, tables)
  and `Page 3/Musicos` in `Level2_Newspaper.unity`. No script, NPC or trigger yet.
- **Page 3 music is silence** today (`PageMusicManager`, Level 2's per-page music).
- **No jazz loop and no saxophone sample exist** in `AudioBank` (Diego is the audio lead).
- **Inventory items cannot be "used"**: `InventorySlot` only shows sprite/name/count; there is no
  click/submit action on a slot. Using the saxophone from the inventory is new, generic work.
- **Input**: everything reads through `InputHub` (the old Input Manager). Any-key detection already
  exists for device switching (`KeyCode.JoystickButton0..19` sweep). The menu hotkeys that are
  letters (I inventory, U quests, O options, M mute) are read in `PlayerController.CheckControls`.
- **Timing constraint**: page 3 ends with the arrest (reading the gift's letter starts it), so the
  bar is only reachable on page 3 **before** the letter is read.
- The inventory does not carry between levels (per-scene `LevelManager`), so the sax exists only in
  Level 2 after winning it.

## User Scenarios & Testing

### Story 1 — The jam session (Priority: P1)

Kami walks into the bar under Natalia's house and talks to the band. The sax player asks if she
wants to jam. The backing track starts, a countdown shows, and for N seconds every key/button she
presses plays a random saxophone note from the song's scale, with floating notes around her and a
counter on screen. With at least the minimum presses she wins: the band cheers and she gets the
saxophone. Otherwise the band says "¡Casi!" and she can try again whenever she wants.

**Acceptance**:
1. **Given** Kami is in the bar on page 3 before the letter is read, **When** she talks to the
   band, **Then** the jam invitation plays and the jam starts when it closes.
2. **Given** the jam is running, **When** the player presses any letter, digit or Space (keyboard)
   or any gamepad button except Start, **Then** a note plays and the counter goes up; holding a key
   does nothing more; Kami does not move, jump, attack, and no menu opens.
3. **Given** the time runs out with the counter at or above the minimum, **Then** the win line
   plays and the saxophone enters the inventory (once).
4. **Given** it runs out below the minimum, **Then** the "¡Casi!" line plays and talking to the
   band again restarts the jam.

### Story 2 — Sax mode (Priority: P2)

Once she has the saxophone, the player selects it in the Flap inventory: the menu closes and Kami
enters sax mode, standing still and playing a note on every key, until the player presses Esc
(keyboard) or Start (gamepad), which leaves sax mode instead of opening the menu.

**Acceptance**:
1. **Given** the sax is in the inventory, **When** the player activates its slot, **Then** the
   Flap closes and sax mode starts.
2. **Given** sax mode, **When** any note key is pressed, **Then** a note plays; Kami stays still.
3. **Given** sax mode, **When** Esc/Start is pressed, **Then** sax mode ends and the menu does NOT
   open on that same press.
4. Sax mode can't start during a dialogue, cutscene, origami, overlay or page turn.

## Requirements

- **FR-001** Free play: every note key press is one point; no timing, no wrong notes.
- **FR-002** Duration (default **10 s**) and minimum score (default **8**) are Inspector values.
- **FR-003** Notes: a `JazzScale` asset (ScriptableObject) holds the song's root and scale
  (semitone offsets) and the sax clip; each press plays the clip pitched by `2^(semitones/12)` to a
  random note of the scale. One clip is enough; a list of real per-note clips can replace the
  pitching later without code changes (the asset decides which it uses).
- **FR-004** Note keys: keyboard A-Z, 0-9 and Space; gamepad every button except Start
  (`JoystickButton7`); sticks and D-pad don't count. One place in `InputHub` owns this list.
- **FR-005** While the jam or sax mode runs, all gameplay input and the letter menu hotkeys
  (I/U/O/M) are ignored; Kami is frozen (`LevelManager.inCutscene`, or a dedicated flag if that one's
  dialogue exception gets in the way).
- **FR-006** Feedback: countdown + counter UI, floating note particles around Kami, win/fail band
  lines (localized es/en/pt, like every Level 2 line).
- **FR-007** Optional side quest (`Quest09_JamSession`, Event quest with its own `QuestManager`
  handler) added when the band first invites Kami, completed on the win. Never blocks the story.
- **FR-008** The saxophone is a new `ResourceType` (appended before `Count`) + `InventoryItem`,
  registered in BOTH `InventoryManager` lists (the prefab and Level 2's standalone scene copy).
- **FR-009** Using items from the inventory is generic: an item can declare a "use" action; the
  sax is the first one. Items without one behave exactly as today.
- **FR-010** Sax mode uses the bar song's scale over whatever music is playing.

## Dependencies (not code)

- **Audio (Diego)**: a jazz backing loop for the bar (also page 3's bar ambience) and a saxophone
  sample (one note is enough; per-note samples optional).
- **Art (Valentino)**: Kami playing the sax (Spine animation, used in the jam and sax mode), the
  saxophone inventory icon, the band musicians. Placeholders until then: Kami's idle, a tinted
  sprite for the band, an empty icon.

## Out of scope

- Rhythm/timing judgement (the GDD's original idea): explicitly replaced by free play.
- Carrying the sax to other levels (no cross-level inventory exists).
- Sax mode while walking (WASD are notes).
