# Tasks: Kami Gear (spec 011)

GitHub (M4), filed 2026-10-02: epic #139. 0.A #140, 1.A #141, 1.B #83, 1.C #61, 2.A #142, 2.B #143,
2.C #144, 2.D #145, 2.E #146 (also closes #17), 3.A #147, 3.B #148, 3.C #149, 3.D #150, 3.E #151.
#85 closed as answered (Spine 4.2 already installed). Phase 0.A + Phase 2 session: `kickoff-prompt.md`
(done, closed 2026-10-05 with #17). Phase 3 session: `kickoff-prompt-phase3.md`.

Phases in order; `[P]` = parallel-safe with its siblings. Code tasks (Phase 2) do **not** wait for
the art (Phase 1): the scissors skins already exist, and missing skins only log a warning (FR-006).
Phase 1 and Phase 2 can run at the same time.

## Phase 0 — Groundwork

- [x] **0.A** **Point `Kami.prefab` and MainMenu's Kami at Atlas 12** (F2, Q7): swap the
  `skeletonDataAsset` GUID `d686b372...` (Atlas 11, 3.8) for `edc59de3...` (Atlas 12, 4.2) in
  `Kami.prefab` and `MainMenu.unity`. The level overrides become redundant (leave them, harmless).
  Diego looks at the MainMenu Kami in the Editor afterwards. YAML: preserve line endings.
  **Done 2026-10-05.** Diego had confirmed F2 in the Editor (MainMenu Kami not rendering). MainMenu has
  TWO skeletons on Atlas 11: the title-screen Kami and an inactive "The Paper Model"; both swapped.
  The title-screen Kami sets no skin, so it renders without scissors (it never set one before either).
- [x] **0.B** **Docs**: runtime version and active skeleton corrected in `CLAUDE.md` and
  `docs/claude/spine-kami.md`, pointing at this spec. (Done with the spec, 2026-10-02.)

## Phase 1 — Spine authoring (Valen) `[P]` with Phase 2

Follow the "Spine authoring contract" in `spec.md`. Supersedes #85; covers #83/#17; redirects #61.

- [ ] **1.A** **Restructure Kami's skins**: skin folders (`outfit/`, `scissors/`, `feet/`), rename the
  scissors skins to `scissors/normal` / `scissors/upgrade1`, and move everything an outfit or item
  changes (the 12 shoe slots, the clothes) out of `default` into `outfit/default` placeholders.
  Export, and check in game that Level 1 Kami looks exactly as before.
- [ ] **1.B** `[P]` **`feet/rain-boots`** (yellow rubber boots): the feet placeholders, `_OL_` twins
  included; leave out whatever the boots hide (the buckle).
- [ ] **1.C** `[P]` **`outfit/detective`** as a skin of the same skeleton (Q2): same placeholders as
  `outfit/default`, its own shoes, extra bones as skin bones. Relates to #59-#62.
- **1.D** (deferred, not filed) **Lightfall winged boots** replace the sprint boots (Diego,
  2026-10-02). Design later; then one `feet/` skin + one `GearItem`.

## Phase 2 — Code: getting an item replaces the part (FR-001..008)

- [x] **2.A** **Contracts**: `GearSlot`, `GearItem`, `GearLoadout`, `GearCatalog` (`Assets/Scripts/Gear/`),
  with the Spine dropdowns (`[SpineSkin]`, `[SpineSlot]`, catalog implements `IHasSkeletonDataAsset`).
  `.meta` files with `tools/make-meta.py`. Compile-check.
- [x] **2.B** `[P]` **`SpineSkinComposer`** (static, pure) + **`PlayerView`** recomposes on
  `OnGearChanged` (SetSkin + SetSlotsToSetupPose + AnimationState.Apply). Warns once per missing
  skin name.
- [x] **2.C** `[P]` **`PlayerGear` + `Player`**: equipped state, `Equip`/`Unequip`, `OnGearChanged`,
  `_startingLoadout`, the `OnResourceUpdated` auto-equip hook. `SetTijeraEquipment` loses its two
  skin lines (gameplay half stays).
- [x] **2.D** **Assets and wiring**: the six `GearItem` assets, `Resources/GearCatalog.asset` (owned
  Spine slots: Scissors = `tijera_back2`, `tijera_front`; Feet = the 12 shoe slots), the two
  starting loadouts, `_startingLoadout` set on Kami in both levels, Level 1's `initialSkinName`
  override removed. Skin names follow 1.A (use the current `Tijera_*` names until Valen's export lands,
  then edit two strings).
- [x] **2.E** **Close-out** (2026-10-05; Diego played the checklist, all as expected): docs (`spine-kami.md` gear section, `CLAUDE.md` equipment section),
  `/graphify Assets/Scripts --update`, spec status. **Diego plays** the checklist in `spec.md`
  "Verification".

### Phase 2 as built (2026-10-05, played by Diego the same day)

F3 confirmed by Diego in the Editor before the work (Level 2 Kami drew no scissors). Changes vs. the
design above, and why:

- **The skin is applied from `SkeletonAnimation.BeforeApply`, not with an extra `AnimationState.Apply`**
  (Diego's call, 2026-10-05). An extra `Apply` between two updates re-fires every Spine event of the
  frame (`animationLast` only advances in `AnimationState.Update`): a gear change landing on a footstep
  played it twice, on `HandleAttack` it re-armed the hitbox. `PlayerView` marks the skin dirty on
  `OnGearChanged` and composes right before Spine's own Apply, which gives the same no-flash guarantee.
  A change made after Spine's update shows one frame later (still no flash).
- **`GearItem` also implements `IHasSkeletonDataAsset`**, borrowing the catalog's skeleton, so its
  `[SpineSkin]` dropdown works without a skeleton reference on every item.
- **`GearItem._grantedByResource`** (bool next to `_resource`): the outfits have no `ResourceType` until
  3.A, and the enum's default 0 is `hongos`, so without it picking a mushroom would equip the outfit.
  3.A turns it on for the outfits.
- **A missing Spine skin skips the whole item, slot clearing included**: otherwise missing rain-boots art
  would clear the outfit's shoes and leave Kami barefoot once the shoes move out of `default` (1.A).
- **A missing catalog composes nothing** (Kami keeps the skin she has, `default` now that Level 1's
  `initialSkinName` override is gone), as the spec says.
- **`Player.waterBootsMaterial` removed**: unused since the old 3D model; its TODO is answered by
  `Gear_RainBoots`. Old scenes `Nivel1_LaRural`/`SampleScene` keep an orphan YAML key (harmless).
- Data assets live in `Assets/Scripts/Gear/` (like Inventory/Quests). `Level2_StartLoadout` lists
  `Gear_OutfitDefault` under "also owned" (read from 3.A on).
- **After the playtest (Diego, 2026-10-05)**: the sprint boots are retired. The P cheat no longer gives
  them and `Gear_SprintBoots` was deleted (five gear items). And a **placeholder `feet/rain-boots`** skin
  was added by hand to the Atlas 12 `skeleton.json` so the P cheat shows the Feet slot working: yellow
  silhouettes of the shoes, yellow gaiters, the buckle hidden by transparent copies (needed only while the
  shoes live in `default`). Built from atlas images already there; checked by loading the export with the
  project's spine-csharp 4.2 outside Unity. A re-export drops it: `tools/add-placeholder-rain-boots.py`
  puts it back. Details: "Placeholder `feet/rain-boots`" in `docs/claude/spine-kami.md`. Diego played it
  the same day: P gives only the rain boots and the repaint shows as expected.

## Phase 3 — The Wardrobe (FR-101..105), after Phase 2 is played

- [ ] **3.A** **Outfits as items**: `outfitDefault`/`outfitDetective` `ResourceType`s (appended before
  `Count`), `InventoryItem` assets, added to **both** InventoryManager lists (Level 2's is a scene
  copy), granted by the starting loadouts.
- [ ] **3.B** `[P]` **Equip by tapping the bag**: `InventorySlot` click/A on a gear item toggles it
  through `Player.Gear` (outfits swap, never empty). Blocked while frozen (FR-105).
- [ ] **3.C** `[P]` **Effects follow what is equipped** (FR-103): `hasWaterBoots`/`hasSprintBoots`
  read from `Player.Gear`; scissors damage too. Decide Q4 first.
- [ ] **3.D** **Wardrobe tab** in the Flap: one row per gear slot, gamepad-navigable (R1/L1, A, B,
  visible `m_SelectedColor`, see `controles-y-gamepad.md`). Needs a small layout from Valen.
- [ ] **3.E** `[ART-VALEN]` icons/stickers for the outfits and the Wardrobe tab.

## Phase 4 — Optional: carry gear between levels (FR-201, Q5: deferred, not filed)

- **4.A** A tiny persistent gear state (static or `DontDestroyOnLoad`), read by Player at start
  with the starting loadout as fallback. Only if Q5 says yes.

## Parallelism summary

Phase 1 (Valen) runs alongside Phase 2 (code). Inside Phase 2: 2.A first, then 2.B and 2.C in
parallel (different files: `PlayerView.cs` + composer vs `Player.cs` + `PlayerGear.cs`), then 2.D
(only one writer for the scenes/prefab). Phase 3: 3.B and 3.C in parallel after 3.A; 3.D after 3.B.
Max 2 code agents at a time.
