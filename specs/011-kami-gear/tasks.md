# Tasks: Kami Gear (spec 011)

GitHub (M4), filed 2026-10-02: epic #139. 0.A #140, 1.A #141, 1.B #83, 1.C #61, 2.A #142, 2.B #143,
2.C #144, 2.D #145, 2.E #146 (also closes #17), 3.A #147, 3.B #148, 3.C #149, 3.D #150, 3.E #151.
#85 closed as answered (Spine 4.2 already installed). Phase 0.A + Phase 2 session: `kickoff-prompt.md`
(done, closed 2026-10-05 with #17). Phase 3 session: `kickoff-prompt-phase3.md`. Phase 4 filed
2026-10-05 (M4): 4.A #152, 4.B #153, 4.C #154 (#46 cross-linked). Phase 4 session:
`kickoff-prompt-phase4.md`.

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

- [x] **3.A** **Outfits as items**: `outfitDefault`/`outfitDetective` `ResourceType`s (appended before
  `Count`), `InventoryItem` assets, added to **both** InventoryManager lists (Level 2's is a scene
  copy), granted by the starting loadouts.
- [x] **3.B** `[P]` **Equip by tapping the bag**: `InventorySlot` click/A on a gear item toggles it
  through `Player.Gear` (outfits swap, never empty). Blocked while frozen (FR-105).
- [x] **3.C** `[P]` **Effects follow what is equipped** (FR-103): `hasWaterBoots`/`hasSprintBoots`
  read from `Player.Gear`; scissors damage too. Decide Q4 first.
- [x] **3.D** **Wardrobe tab** in the Flap: one row per gear slot, gamepad-navigable (R1/L1, A, B,
  visible `m_SelectedColor`, see `controles-y-gamepad.md`). Needs a small layout from Valen.
- [ ] **3.E** `[ART-VALEN]` icons/stickers for the outfits and the Wardrobe tab (#151, not needed by
  3.A-3.D: everything ships with placeholders).

### Phase 3 as built (2026-10-05, played by Diego the same day: all good except F5, fixed by 4.A)

Diego took the three defaults asked before building (now Q9-Q11 in `spec.md`). What was built and what
changed vs. the tasks above, and why:

- **3.A** `ResourceType.outfitDefault` (14) / `outfitDetective` (15), only `Count` shifted (nothing
  serializes it). `ItemOutfitDefault`/`ItemOutfitDetective` (no sprite yet, pale tints), in
  `InventoryManager.prefab` and Level 2's scene list; `ItemTable` `outfit_*_title/_text` in es/en/pt, also
  the outfit `GearItem`s' `_displayNameKey`; `_grantedByResource` on for both outfits.
  **Granting at level start** (Q10): `Player.GrantStartingItems`, one frame after `Start`, gives the
  starting scissors and then every bag item the loadout lists (skipping what she already owns) through
  `AddResource(..., ownedAtLevelStart: true)`. That 4th `OnResourceUpdated` value makes the sticker
  (`ResourceParticleManager.StartSystem`) and the auto-equip (`Player.EquipGainedGear`) skip it. Level 2's
  starting scissors use it too, so they no longer pop a sticker at level start. The loadout never grants
  scissors itself: holding them is `hasTijera` (`_startWithTijera`); either mismatch logs a warning.
  `InventoryManager.AddItem` now warns and skips a resource missing from the scene's list instead of
  throwing (old test scenes with outdated lists would otherwise throw at start).
- **3.B** `InventorySlot.BUTTON_OnPress` keeps the showcase and then calls `Player.TryToggleGear`, only
  with the Flap open (the reward stickers are InventorySlots too). Toggleable gear slots are data:
  `GearCatalog._changeableFromBag` = Outfit, Feet (Q9). Frozen = `inDialogue` (also overlays, page turns),
  `inCutscene`, or the states that already block the attack (Casting, ReceivingReward, Dead, RidingPage).
- **3.C** `GearItem._savesFromDrowning` (on in `Gear_RainBoots`) + `PlayerGear.SavesFromDrowning`, read
  by `Player.GetWet`; `Player.hasWaterBoots` removed (and its key in `Kami.prefab`). **Added**: the river
  calls `GetWet` only on entry, so boots taken off while standing in it would still protect her until she
  stepped out; `Player.RecheckWaterAfterGearChange` re-runs `GetWet` once the Flap closes. Scissors damage
  needed nothing (Q9: the newest pair is always the one worn); `hasSprintBoots` is not gear (retired item).
- **3.D** (Q11) `WardrobeDisplay` (`Assets/Scripts/UI/`) as `_flapDisplays[4]`, `FlapManager.BTN_Wardrobe`,
  a 5th tab button (copy of the bag tab, lilac) at y = -85 below Controls, three `Row_*` (label with
  `LocalizeStringEvent` on UITexts `wardrobe_*`, `Slots` GridLayoutGroup) authored in `FlapManager.prefab`
  by YAML (219 -> 256 blocks, fileIDs unique, hierarchy/root orders checked). Item buttons are
  `InventorySlot.prefab` copies spawned at runtime per owned item; worn = scale 1.15, others 0.85.
- **Noted, not done**: `GearItem._displayNameKey` is still read by nothing (the Wardrobe shows the bag
  item's name, like the bag). Keep it for Valen's Wardrobe art or remove it: Diego's call. A `wardrobe_hat`
  UITexts key exists for the day the Hat row does.

**Diego's playtest checklist** (from the kickoff): Level 1 bag holds the default outfit from the start,
no sticker; P gives the rain boots; tapping them takes them off (default shoes and buckle back) and on;
river drowns her only with them off. Level 2 starts in the detective outfit (default look + one
missing-skin warning until #61) owning both outfits; swapping keeps boots and scissors. Wardrobe: one row
per slot, worn item bigger, R1/L1 reach it, A equips, B closes, amber selected color visible, nothing
selected after closing; check the 5th tab's position on the paper. Frozen (dialogue, cutscene, origami,
dead, riding a page): refused. Console: no new errors.

## Phase 4 — A Flap that only listens while it's open (FR-301..306; planned 2026-10-05)

Found playing Phase 3 (spec F5/F6). Not gear-specific, kept here by Q12. Everything is in
`FlapManager.cs`: one session, in order (no `[P]`). Confirm Q14 at its kickoff.

- [x] **4.A** #152 **Tabs only exist while the Flap is showing** (FR-304, F5): `SetTabsVisible` in
  `Start`, `OpenFlap` and at the end of the closing slide. Built 2026-10-05 on `011-kami-gear` with
  Phase 3 (Q13). Played by Diego with the rest of Phase 4.
- [x] **4.B** #153 **Flap states** (FR-301, FR-302, FR-306): `FlapState` replaces `_isOpen`; menu input
  (R1/L1/B) and selections by code only in `Open`; leaving `Open` clears the selection; `IsMenuOpen`
  follows Q14. Closes F6's close-slide leak.
- [x] **4.C** #154 **A closed Flap can't be clicked or navigated** (FR-303, FR-305): code-added
  `CanvasGroup`s on the menu roots (raycasts only in `Open`, `interactable` off only while `Closed`, see
  the Disabled-tint gotcha in spec "Phase D"), the safety-net deselect + warning, then the close-out
  (docs, graphify, Diego's checklist in spec "Verification").

### Phase 4 as built (2026-10-05, played by Diego the same day: everything on the checklist worked)

Diego took every default at the kickoff: Q14 (the menu owns input from the start of opening to the start
of closing), Q15 (a toggle mid-slide reverses it) and Q16 (gear taps need `IsFullyOpen`). All in
`FlapManager.cs`, plus one line each in `InventorySlot.ToggleGear` (Q16) and a comment in
`PlayerController`. No prefab or scene edits. Changes vs. the design, and why:

- **`MoveFlap(bool opening)`** instead of `MoveFlap(float targetY)`: it sets `Opening`/`Closing` itself at
  the start, and the end state comes from the same flag, so nothing compares a position to decide "open"
  (FR-301). It had no callers outside `FlapManager`.
- **The selection is cleared at the start of every slide**, not only when leaving `Open` (FR-302 asked
  for the latter, which is included). Reason: the pull tab (`Tirita fondo`, HUD strip) has Automatic
  navigation, so the click that opens the Flap selects it, and WASD/stick during the opening slide would
  navigate from it into the now-interactable menu. The `UISelector.Limpiar` that `CloseFlap` used to call
  moved into `SetState`.
- **`RefreshMenuGroup`** (new, not in the design): uGUI 1.0's `Selectable` caches whether its CanvasGroups
  allow interaction and refreshes that only when a group changes while it is active (its `OnEnable` does
  not; read in `Library/PackageCache/com.unity.ugui@1.0.0`). Without it, a display hidden while the Flap
  closed and reopened (Esc shows Settings while closed, then R1 back to the bag) would come back greyed out
  and unclickable in an open Flap. `ShowDesiredDisplay` and `BTN_Salir` flip the root's group right after
  showing it and before selecting in it.
- `BTN_Salir` selects the confirm's buttons only when `IsFullyOpen` (FR-302; with 4.C it can't be clicked
  otherwise anyway).
- Known, accepted: a reversed slide still takes the full 0.5 s even from halfway (it lerps from the
  current position over `_flapTransitionDuration`). Not touched: no one asked, and it is visual only.

## Dropped — carry gear between levels (FR-201)

Diego, 2026-10-05: no persistence between levels for now (Q5). Each level starts from its own
`GearLoadout`; revisit only together with a save system.

## Parallelism summary

Phase 1 (Valen) runs alongside Phase 2 (code). Inside Phase 2: 2.A first, then 2.B and 2.C in
parallel (different files: `PlayerView.cs` + composer vs `Player.cs` + `PlayerGear.cs`), then 2.D
(only one writer for the scenes/prefab). Phase 3: 3.B and 3.C in parallel after 3.A; 3.D after 3.B.
Phase 4: nothing parallel (all `FlapManager.cs`): 4.B, then 4.C. Max 2 code agents at a time.
