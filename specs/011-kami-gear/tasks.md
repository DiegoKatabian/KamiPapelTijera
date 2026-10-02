# Tasks: Kami Gear (spec 011)

GitHub (M4), filed 2026-10-02: epic #139. 0.A #140, 1.A #141, 1.B #83, 1.C #61, 2.A #142, 2.B #143,
2.C #144, 2.D #145, 2.E #146 (also closes #17), 3.A #147, 3.B #148, 3.C #149, 3.D #150, 3.E #151.
#85 closed as answered (Spine 4.2 already installed). Phase 0.A + Phase 2 session: `kickoff-prompt.md`.

Phases in order; `[P]` = parallel-safe with its siblings. Code tasks (Phase 2) do **not** wait for
the art (Phase 1): the scissors skins already exist, and missing skins only log a warning (FR-006).
Phase 1 and Phase 2 can run at the same time.

## Phase 0 — Groundwork

- [ ] **0.A** **Point `Kami.prefab` and MainMenu's Kami at Atlas 12** (F2, Q7): swap the
  `skeletonDataAsset` GUID `d686b372...` (Atlas 11, 3.8) for `edc59de3...` (Atlas 12, 4.2) in
  `Kami.prefab` and `MainMenu.unity`. The level overrides become redundant (leave them, harmless).
  Diego looks at the MainMenu Kami in the Editor afterwards. YAML: preserve line endings.
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

- [ ] **2.A** **Contracts**: `GearSlot`, `GearItem`, `GearLoadout`, `GearCatalog` (`Assets/Scripts/Gear/`),
  with the Spine dropdowns (`[SpineSkin]`, `[SpineSlot]`, catalog implements `IHasSkeletonDataAsset`).
  `.meta` files with `tools/make-meta.py`. Compile-check.
- [ ] **2.B** `[P]` **`SpineSkinComposer`** (static, pure) + **`PlayerView`** recomposes on
  `OnGearChanged` (SetSkin + SetSlotsToSetupPose + AnimationState.Apply). Warns once per missing
  skin name.
- [ ] **2.C** `[P]` **`PlayerGear` + `Player`**: equipped state, `Equip`/`Unequip`, `OnGearChanged`,
  `_startingLoadout`, the `OnResourceUpdated` auto-equip hook. `SetTijeraEquipment` loses its two
  skin lines (gameplay half stays).
- [ ] **2.D** **Assets and wiring**: the six `GearItem` assets, `Resources/GearCatalog.asset` (owned
  Spine slots: Scissors = `tijera_back2`, `tijera_front`; Feet = the 12 shoe slots), the two
  starting loadouts, `_startingLoadout` set on Kami in both levels, Level 1's `initialSkinName`
  override removed. Skin names follow 1.A (use the current `Tijera_*` names until Valen's export lands,
  then edit two strings).
- [ ] **2.E** **Close-out**: docs (`spine-kami.md` gear section, `CLAUDE.md` equipment section),
  `/graphify Assets/Scripts --update`, spec status. **Diego plays** the checklist in `spec.md`
  "Verification".

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
