# Kickoff prompt — Spec 011 (Kami Gear), Phase 3: the Wardrobe

Paste everything below the line into a fresh Claude Code session at the repo root.

---

We're implementing **Phase 3 of spec 011, "Kami Gear"** in KamiPapelTijera (Unity 2021.3, URP,
spine-unity **4.2**). Goal of this session: **Kami's outfits are bag items, and the player can change
what Kami wears from the menu**: tap (mouse) or press A on a gear item to equip it, a Wardrobe tab
shows what she wears per slot, and the rain boots only protect her while she's wearing them. Phase 2
(2026-10-05, played by Diego) already composes Kami's skin from her gear at runtime. Build on it; don't
redesign it.

## Branch and state

- Work on branch **`011-kami-gear`**: Phase 0.A + Phase 2 (+ the rain boots placeholder) are committed
  and pushed there. Check `git status` is clean before starting. Never commit, push or open a PR without
  Diego's explicit permission, every time.
- GitHub (M4): epic **#139**. This session = **#147 (3.A) → #148 (3.B) + #149 (3.C) → #150 (3.D)**.
  #151 (Valen: outfit icons + Wardrobe art) runs in parallel and is NOT needed: use placeholders.
  Phase 1 art (#141 skins restructure, #83 rain boots, #61 detective outfit) isn't needed either: missing
  skins only warn, and the rain boots already have a placeholder skin.

## Read first

1. `specs/011-kami-gear/spec.md`: "Concept", "Functional requirements > Phase B" (FR-101..105), "Design",
   "Decisions" (Q3, Q4 and its 2026-10-05 update, Q8).
2. `specs/011-kami-gear/tasks.md`: Phase 3, and "Phase 2 as built" (what changed vs. the design, and why).
3. `CLAUDE.md`, `docs/claude/spine-kami.md` ("Skins: Kami Gear"), `docs/claude/nivel2-y-ui.md`
   (Inventory, Flap UI, the two InventoryManager lists), `docs/claude/controles-y-gamepad.md` (Flap
   navigation, A = Submit, selection and `m_SelectedColor` rules, focus leaks we already paid for).
4. Code: `Assets/Scripts/Gear/` (all of it), `Player.cs` (`Gear`, `OnGearChanged`, `EquipGainedGear`,
   `_startingLoadout`, `GetWet`, `_startWithTijera` and why it waits a frame), `PlayerView.cs`
   (`ApplyPendingSkin`), `Managers/LevelManager.cs` (`ResourceType`, `AddResource`, `AllItemsCheat`),
   `Inventory/InventoryManager.cs`, `Inventory/InventorySlot.cs` (`BUTTON_OnPress`),
   `Managers/ResourceParticleManager.cs` (`StartSystem`), `UI/FlapManager.cs` (`_flapDisplays`, the
   `BTN_*` methods, `CambiarTab`, `Update`), `UI/FlapDisplayButton.cs`.
5. Recipe for adding a bag item end to end: commit `3820d54d` (the café ticket): `ResourceType`, the
   `InventoryItem` asset, `Prefabs/Managers/InventoryManager.prefab`, Level 2's standalone list in
   `Level2_Newspaper.unity`, and the `ItemTable` keys (Shared Data + es/en/pt).

## Scope, in order

- **#147 (3.A)** Outfits as items: `ResourceType.outfitDefault`, `outfitDetective` **appended right before
  `Count`**. `InventoryItem` assets (no sprite yet), in **both** InventoryManager lists. `ItemTable` name
  keys in es/en/pt, also set as the outfit `GearItem`s' `_displayNameKey`. Turn `_grantedByResource` on
  for `Gear_OutfitDefault`/`Gear_OutfitDetective`. Each level's starting loadout grants what it lists:
  Level 1 owns the default outfit; Level 2 wears the detective outfit and also owns the default one.
  No reward pose, and no sticker.
- **#148 (3.B)** Tapping a gear item in the bag (mouse click or A on the selected slot) toggles it through
  `Player.Gear`: equip, or unequip if it's already on. Outfits swap and are never emptied. Blocked while
  the game is frozen for the player (FR-105). The Flap is a pause, so equipping from it is allowed.
- **#149 (3.C)** Effects follow what is *equipped* (FR-103): the rain boots save Kami from drowning only
  while worn. `Player.GetWet` reads `hasWaterBoots` today.
- **#150 (3.D)** A Wardrobe tab in the Flap: one row per gear slot (Outfit, Scissors, Feet; Hat when it
  exists) with the owned items and the equipped one highlighted. Fully gamepad-navigable. Placeholder
  layout until Valen's #151.
- Close-out (below).

## Ask Diego before building (each has a recommended default)

1. **Which items can be toggled from the bag?** His original request (spec "Request" 3) says "at first
   only the rubber boots and the two outfits". FR-102 says any gear item. *Default: outfits + Feet only;
   the scissors stay auto-equipped (newest gained).* With that default, 3.C has nothing to do for the
   scissors (the upgraded damage already follows the newest pair).
2. **How are a level's owned items granted at start without side effects?** (See the gotcha below.)
   *Default: `PlayerGear` grants them one frame after `Start` (like `_startWithTijera`) with auto-equip
   suppressed, and the reward sticker skips them.*
3. **Wardrobe layout**: *Default: rows built from the existing `InventorySlot` prefab inside a new
   `FlapDisplay`, appended as the LAST display, so the hardcoded `BTN_*` indexes 0-3 keep working.*

## Decided rules: don't reopen these

- Composition and owned slots are Phase 2's (outfit, then Scissors, Feet, Hat; the last one added wins).
  Equipping goes through `PlayerGear.Equip/Unequip`, never through `SetSkin`.
- Outfits can't be unequipped, only swapped. An empty Feet slot shows the outfit's own shoes.
- **Sprint boots are retired** (Diego, 2026-10-05): nothing gives them and `Gear_SprintBoots` is gone.
  Kami's sprint is `hasSprintBoots`, on in `Kami.prefab`: leave it alone (it's not gear).
- Gear state doesn't carry across levels (Q5: no). Each level starts from its loadout.
- Getting an item still auto-equips it (newest wins in its slot), whichever way it arrives.
- Everything a designer touches is an Inspector-editable asset.

## Gotchas already paid for (or found by reading the code)

- **`AddResource` has side effects for every subscriber of `OnResourceUpdated`**:
  - `Player.EquipGainedGear` auto-equips. Granting Level 2's also-owned default outfit through it would
    put her in the default outfit over the detective one.
  - `ResourceParticleManager.StartSystem` shows a reward sticker + glitter for every gain.
  - `InventoryManager.AddItem` and the sticker index `itemsByResourceType[rt]`, which throws for an item
    missing from that scene's list. Level 2's list is a standalone scene copy.
  - `InventoryManager` builds its slots in its own `Start`. That's why `_startWithTijera` waits a frame.
- `OnQuestDelivered` for a quest with no reward shows a mushroom sticker (`rewardRt` defaults to 0).
  Don't route anything through it.
- **Skin changes are applied from `SkeletonAnimation.BeforeApply`** (`PlayerView.ApplyPendingSkin`). Never
  add an extra `AnimationState.Apply`: it re-fires the frame's Spine events (`HandleAttack`,
  `HandleFootstep`). From reading the code: with the Flap open (`Time.timeScale` 0), `SkeletonAnimation`
  still updates every frame with delta 0, so an equip from the menu should show behind it right away.
  Confirm in play.
- Flap and gamepad: A is Submit. Select only once the menu has finished opening, and clear the selection
  on close. **Every Selectable has its own `m_Colors.m_SelectedColor`**: check each new one, the base gray
  is invisible. `Selectable.Select()` gives focus, it doesn't just highlight. R1/L1 cycle `_flapDisplays`
  by index, and `FlapDisplay.number` is the index. A display with nothing navigable must fall back to its
  tab button. B closes the Flap.
- `InventorySlot.BUTTON_OnPress` showcases the item today. Keep that, and add the toggle for gear items.
- Adding a `ResourceType` before `Count` shifts only `Count`. Nothing serializes `Count`, but grep for it.
- The rain boots skin is a **placeholder** in the Atlas 12 export (`tools/add-placeholder-rain-boots.py`).
  A re-export of Kami drops it; re-run the script if #83 isn't in the new export.
- YAML surgery (prefabs/scenes): read the target block whole, preserve line endings, count `--- !u!`
  blocks before and after, grep new fileIDs for uniqueness. Stripped blocks of nested prefab objects use
  `(source XOR prefabInstance) & 0x7FFFFFFFFFFFFFFF`. New ScriptableObject assets: copy an existing
  asset's YAML. Script/folder `.meta`: `python tools/make-meta.py`. `.asset` metas need a
  `NativeFormatImporter` meta (`mainObjectFileID: 11400000`), not make-meta's MonoImporter one.
- `EventManager` swallows handler exceptions into a log: log decisions (`Debug.Log($"[Class] ...")`).
- Deleting code: list exactly what's in the range before cutting, and re-list the remaining methods after.

## Working rules

- English for everything new (code, comments, asset names, docs) and in replies to Diego. Leave existing
  Spanish alone.
- Comments explain the non-obvious *why*. Braces always, guard clauses that warn,
  `[SerializeField] private` + `[Tooltip]`.
- Do the mechanical work yourself (YAML, assets, `.meta`, localization tables). Ask Diego about
  decisions, not labour.
- Agents: none unless Diego asks. **Only one writer for scenes and prefabs.**
- Triangulate the code first. If the spec doesn't match the code, stop and ask with a recommended
  default before building.

## Verification

- `python tools/compile-check.py` after each step. Known baseline warnings: `JumpFloodOutlineRenderer`
  CS0162, `HongueroTiburcioDialogueTrigger` CS0414. **Compilation only**: say clearly what wasn't run.
- Diego's playtest checklist:
  - **Level 1**: the bag holds the default outfit from the start, with no sticker. P gives the rain boots
    (yellow placeholder). Tapping them in the bag takes them off (default shoes, buckle back) and puts
    them on again. With the boots off, the river drowns her; with them on, it doesn't.
  - **Level 2**: Kami starts in the detective outfit (still the default look until #61's art, with one
    warning) and owns both outfits. Swapping outfits keeps the boots and the scissors on.
  - **Wardrobe tab**: one row per slot, the equipped item highlighted, R1/L1 reach it, A equips, B closes
    the Flap, the selected color is visible, nothing stays selected after closing.
  - **Frozen** (dialogue, cutscene, origami, dead, riding a page): equipping is refused.
  - **Console**: no new errors. Only the known missing-skin warnings.

## Close-out

- Docs: `spine-kami.md` ("Skins: Kami Gear": the Wardrobe, equip from the bag, effects follow what's
  equipped), `nivel2-y-ui.md` (Flap tabs, inventory items), `controles-y-gamepad.md` if navigation
  changed, `CLAUDE.md` (equipment section + "where everything is").
- `/graphify Assets/Scripts --update` from the repo root, then copy `graph.json`, `GRAPH_REPORT.md`,
  `graph.html` and `manifest.json` into the `Assets/Scripts/graphify-out/` mirror. If every file shows
  as changed, a full rebuild costs the same (AST only).
- Mark Phase 3 in `tasks.md`, with any design changes vs. the spec and why.
- Leave everything uncommitted for Diego's review. When he has played it and asks: post a status comment
  on #147-#150, close them, and comment on epic #139.
