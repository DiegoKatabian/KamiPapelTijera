# Spec 022: Flying paper stickers (reward feedback that travels to the HUD)

**Status**: Draft, 2026-10-26. Designed with Diego during the EVA feedback triage; decisions A-D below are his.
Nothing built. Epic **#296**, tasks #297-#304 (0.A = #297, 1.A-1.E = #298-#302, 2.A = #303, 2.B = #304). Milestone M9. Branch: none yet.
Sibling specs from the same triage: 023 (settings menu), 024 (EVA feedback batch).

## Request (Diego, 2026-10-24/26)

Players don't notice that the reward sticker "flies" to the paper counter: today it only appears and fades. When
Kami gains paper (from any source) the sticker should **travel from where the paper came from to the HUD counter**.
Chests, Dalia's gift and other big payouts must read as "a lot of paper" without spawning 25 or 50 stickers.
Reference: Kernel Hearts, where a defeated enemy drops many pink orbs that fly from the 3D world to the corner counter.

## Goal (what Diego sees)

> I cut a bush: one paper sticker pops out of it, hangs for a beat, then arcs up to the paper counter, which
> ticks +1 as it lands. I open a chest (25 paper): a small swarm of stickers bursts out, a "+25" floats above the
> chest, and the swarm streams into the counter, which counts up as each one lands. A poster or a Level 2 trash
> can gives two or four single stickers. Picking up a clue or an outfit flies its sticker to the Flap button.

## Decisions (Diego, 2026-10-26)

- **A. Many papers**: a swarm of 5-8 stickers that share the amount, plus a floating "+N" at the origin. The counter
  ticks up as each sticker lands. (His own first idea, a single bigger sticker with a number, is the fallback if the
  swarm looks noisy.)
- **B. Threshold**: up to **4** paper = one sticker per unit (bush 1, poster 2, trash can 4). 5 or more = swarm mode.
- **C. Other resources** (flowers, mushrooms, clues, hat, scissors, evidence) fly to **the Flap's open button**, not to
  a counter, because only paper has one.
- **D. Quest reward stickers** (`ResourceParticleManager.kamiRewardSticker`, the one Kami holds up) stay **untouched**.
- Posters give 2 paper each (Level 2 only) and Level 2 trash cans give 4: those two numbers are issues in spec 024.

## What exists today (checked in code, 2026-10-26)

- `Managers/ResourceParticleManager.cs` owns **one** `InventorySlot sticker`. `StartSystem` (on `OnResourceUpdated`,
  gains only) shows it: fade in over 0.5 s, stay `duration` (2 s), fade out. It never moves. A second gain during those
  2 s overwrites the first, so two quick papers look like one. The amount is not used at all.
- Its position comes from a **separate** event, `OnObjectWasCut` (param 0 = world position), raised by
  `PickupCortable`, `GrantResourcePickup`, `ConfiscatedGearPickup` and `OrigamiItemGiver` (which fakes it with Kami's
  position). It must run **before** `AddResource`: an ordering contract nothing enforces. Sources that don't raise it
  (chests? Dalia's quest reward? trash cans through `GrantResourcePickup` do) show the sticker at a stale position.
- `LevelManager.AddResource(type, valueToAdd, ownedAtLevelStart = false)` raises
  `OnResourceUpdated(type, totalNow, isAdding, ownedAtLevelStart)`. **The delta is not in the event** (only the new
  total), which is why the amount is invisible to the sticker.
- `LevelManager.IsOwnedAtLevelStart(parameter)` already filters starting-loadout grants: keep that behaviour.
- Quest rewards go through `Evento.OnQuestDelivered` -> `ShowRewardSticker` (`isShowingRewardSticker` blocks the plain
  sticker meanwhile). Out of scope (decision D).
- The HUD strip (pull tab, health, paper counter) hangs under the Flap and is faded by `FlapManager` while
  `LevelManager.inCutscene` is on (`_hudGroup`). The paper counter text is driven by `UI/TextUpdater/PapelTextUpdater`.

## Design

### 1. One request, explicit origin

A single entry point replaces "position event first, resource event after":

```
LevelManager.AddResource(type, amount, origin?)   // origin = world position; null -> Kami's chest
-> OnResourceUpdated(type, totalNow, isAdding, ownedAtLevelStart, delta, origin)   // two new trailing params
```

Existing subscribers read parameters by index and ignore extras, so appending is safe (check every `OnResourceUpdated`
subscriber still compiles: `InventoryManager`, `QuestSlot`, `QuestManager`, `FindCluesTracker`, `Player`,
`PedestalColorCheck`, `WardrobeDisplay`). Every source that already raises `OnObjectWasCut` passes its position
instead, then `OnObjectWasCut` stays only if something else listens to it (grep first; delete it if nothing does).
A source that passes nothing gets Kami's position and a log line, so a forgotten source is visible, not silent.

### 2. The flight (one `FlyingSticker` object, pooled)

Pool of about 16 lightweight stickers: an `Image` plus an optional TMP number. **Do not reuse `InventorySlot`** (it
carries hover, click, name and amount logic). Per sticker, on the overlay canvas, all in **unscaled time**:

1. **Pop**: appears at the origin's screen point (world -> screen with the live camera, as `SetStickerPosition` does
   today), scale 0 -> 1 with a small overshoot (0.15 s), a random scatter of a few pixels.
2. **Hold**: about 0.3 s so the player sees what it is.
3. **Fly**: eased (ease-in, slight arc) to the target `RectTransform`, about 0.6 s. Stagger between stickers of one
   burst about 0.05 s.
4. **Land**: counter punches in scale, tick sound (pitch rises with each landing of one burst), sticker returns to
   the pool.

All numbers live in a `RewardFlightSettings` asset loaded with `Resources.Load` (the `TextHighlightSettings`
pattern: editable in the Inspector, fallback defaults if missing).

### 3. Tiers

- `delta <= 4`: `delta` stickers, one per unit, each landing adds 1.
- `delta >= 5`: swarm of `min(8, max(5, delta / 5))` stickers sharing the amount (the last one carries the
  remainder), a "+delta" label that floats up from the origin and fades, same flight. Constant-time: a swarm
  is the same length as a single sticker plus its stagger.
- Safety cap: at most 24 stickers in flight; beyond that the extra ones skip the animation and add to the counter
  directly (log once).

### 4. The counter shows what has landed

The **real** resource count (`LevelManager`, the bag, quests, pedestals' paper check) updates **immediately**:
gameplay must never wait for an animation. Only the **displayed** HUD number (and only for paper) lags: it counts up
as stickers land, and snaps to the real value if the sticker system is interrupted (page turn, scene unload, Flap
opening mid-flight does NOT interrupt: unscaled time keeps flying). Spending paper (negative delta) updates the display
at once, as today. The Flap's Inventory tab reads the real count.

### 5. Targets

A small registry (`HudTargets`, a static or on the Flap) with two `RectTransform`s: the paper counter and the Flap's
open button. `FlapManager` registers both in `Awake`. Paper flies to the counter, every other resource to the
button. If a target is missing: warn once and fall back to a plain fade at the origin (today's behaviour), never throw.
The target's screen position is read **every frame of the flight** (the Flap can slide while a sticker is in the air).

### 6. Edge cases to handle explicitly

- HUD hidden by a cutscene (`inCutscene`): stickers still fly (the HUD group's alpha is 0, so the landing is
  invisible); the displayed counter must still end correct. Decide in the build whether to also hold the
  stickers' own alpha to the HUD group's.
- Flap open (`Time.timeScale` 0): everything runs on unscaled time.
- Level start grants (`ownedAtLevelStart`) and gear auto-equip stay silent (existing filter).
- Two cuts in the same frame: two bursts, each with its own origin, both within the cap.
- Scene unload / death mid-flight: pool cleared, display snapped to the real count.
- Level 1 and Level 2 both use it (Level 1's chest 25, Dalia 50, bushes 1; Level 2's posters, cans, typewriters).

## Non-goals

- Reworking `kamiRewardSticker` / quest reward flow (decision D).
- A counter for resources other than paper.
- New art: stickers reuse the `InventoryItem` sprites already used by the old sticker. A dedicated paper sprite is
  optional polish for Valentino.

## Tasks (phased; `[P]` = parallel-safe with its siblings)

**Phase 0 - the contract**
- 0.A `AddResource` takes an optional origin; `OnResourceUpdated` gains `delta` and `origin`; every source passes its
  position; `OnObjectWasCut` removed or reduced. Compile-check, grep all subscribers.

**Phase 1 - the flight** (needs 0.A)
- 1.A `HudTargets` registry + `FlapManager` registers the paper counter and the Flap button.
- 1.B `FlyingSticker` prefab, pool, `RewardFlightSettings` asset, the pop/hold/fly/land sequence.
- 1.C Tier logic: single stickers (<= 4), swarm + "+N" label (>= 5), cap.
- 1.D Counter display lag: `PapelTextUpdater` shows landed-so-far, snaps on interruption; tick sound and punch. `[P]` with 1.C
- 1.E Non-paper resources fly to the Flap button.

**Phase 2 - integration and close-out**
- 2.A Rewrite `ResourceParticleManager`'s plain-sticker path onto the new system; keep `kamiRewardSticker` and its flow.
- 2.B Edge cases above, docs (`CLAUDE.md`, `docs/claude/nivel2-y-ui.md`, this spec's "As built").

## Verification

`python tools/compile-check.py` is compilation only. Playtest checklist for Diego: bush (1), poster (2), trash can (4),
a Level 1 chest (swarm + "+25"), Dalia's 50, two bushes cut in one swing, a clue (flies to the Flap button), Flap
opened mid-flight, a cutscene right after a pickup, page turn mid-flight, death mid-flight.

## Open questions (defaults in bold)

1. Swarm size formula: **`min(8, max(5, delta / 5))`**. Retune in the settings asset.
2. Tick sound: reuse an existing UI sound (**`UI` bus, pitched per landing**), or a new clip from Diego later.
3. Does the "+N" label show for single stickers too? **No, only for swarms.**
