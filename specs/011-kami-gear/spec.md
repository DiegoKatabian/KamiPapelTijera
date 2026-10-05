# Spec 011: Kami Gear — mix-and-match Spine parts, outfits and the Wardrobe

**Status**: Approved, 2026-10-02. Diego accepted every default in "Decisions" (Q1-Q8) the same day.
**Phase 0.A + Phase 2 built 2026-10-05 and played by Diego the same day** (everything on the checklist
worked): see "Phase 2 as built" in `tasks.md` for what changed vs. this design (the skin is applied from
`BeforeApply`, not with an extra `AnimationState.Apply`; sprint boots retired; a placeholder rain boots
skin). F2 and F3 were confirmed in the Editor by Diego before the fix. Phase 3 session:
`kickoff-prompt-phase3.md`.
**Phase 3 (the Wardrobe, FR-101..105) built 2026-10-05 and played by Diego the same day**: see "Phase 3
as built" in `tasks.md`, and Q9-Q11 below. His one finding (the Wardrobe tab showed during gameplay) led
to **Phase 4, a Flap that only listens while it's open** (FR-301..306, F5/F6, Q12-Q14), planned the same
day; its first task (4.A) was built right away. Phase C (carrying gear between levels) is dropped.
**Tasks**: `tasks.md`. **Art side**: the "Spine authoring contract" section is the brief for Valen.

## Request (Diego, 2026-10-02)

1. Valen delivers Kami's parts separately (scissors, upgraded scissors, yellow rubber boots, ...).
   In game Kami keeps her base look and **only the needed parts are replaced**. No combinatorial
   skins ("normal", "upgraded-scissors", "upgraded-scissors-with-yellow-boots", ...).
2. In Level 2 Kami wears a whole different outfit (detective). Boots and scissors she has
   equipped **must survive** the outfit change.
3. Later: items can be equipped/unequipped by tapping them in the bag (morral). At first only for
   the rubber boots and the two outfits ("default Kami outfit", "detective Kami outfit"). Kami starts
   with the default outfit in her bag and gets the detective one when Level 2 starts, so in Level 2 she
   can switch. A dedicated menu panel shows Kami's slots: **one outfit, one pair of scissors, one pair of
   boots at a time** (maybe hats later, also one at a time). The system needs a name and a solid design.
4. Now: understand how Spine's swappable-part skins work, explain it, and plan "getting an item in
   game replaces the part in Spine".

## Name

**Gear** for the system (code: `GearSlot`, `GearItem`, `GearLoadout`, `PlayerGear`), and
**Wardrobe** for the player-facing menu tab. "Gear" covers both clothes and tools (scissors are not
clothes); "Wardrobe" reads naturally to a kid. Change either if you prefer (question Q1).

## How Spine mix-and-match works (verified against our runtime, 2026-10-02)

Read from `Assets/Spine/Runtime/spine-csharp/Skin.cs` and `Skeleton.cs`, the two demo scenes
`Assets/Spine Examples/Other Examples/Mix and Match Skins.unity` (script `MixAndMatchSkinsExample.cs`)
and `Mix and Match Equip.unity` (`EquipSystemExample.cs`), the demo skeleton
`mix-and-match-pro.json`, and Esoteric's docs (`spine-skins`, `spine-runtime-skins`).

1. **A slot shows one attachment at a time.** Animations never name images; they key a slot to a
   *placeholder name* (e.g. `tijera_front`, `8_head_eyes_closed`).
2. **A skin is a lookup table**: `(slot, placeholder name) -> attachment` (image/mesh + its offset,
   rotation, size, weights). Swapping skins changes which image answers each name; the animations
   do not change. A skin may also own **skin bones and constraints** (only active while the skin is
   on the skeleton: a coat tail, a hat feather with physics, ...).
3. **Lookup order** (`Skeleton.GetAttachment`): the skeleton's current skin first, then the
   **`default` skin** as the fallback. Consequence: whatever lives in `default` can be *replaced*
   by a skin that uses the same key, but **can never be removed**. A part that some item must hide
   must not live in `default`.
4. **Skins combine at runtime.** `new Skin("x")` then `AddSkin(a)`, `AddSkin(b)`, ...: every entry is
   copied, and an entry with the same key **overwrites** the previous one. So the add order is the
   priority: last added wins. Combining is cheap (a dictionary copy of a few dozen entries), done
   only when the gear changes.
5. **Applying**: `skeleton.SetSkin(combined)`, then `skeleton.SetSlotsToSetupPose()`, then
   `animationState.Apply(skeleton)` in the same frame, so attachment keys of the running animations
   (our `*NoScissors` animations hide both scissors slots) apply before the next render. `SetSkin`
   ignores the call if it receives the same `Skin` object, so a new object is built each time.
6. **The demo's structure is the one to copy**: `default` almost empty, the body in `skin-base`, and
   one Spine skin **folder per category** (`hair/*`, `eyes/*`, `clothes/*`, `legs/*`,
   `accessories/*`). Folder names become part of the skin name at runtime (`legs/boots-red`). The
   demo builds `base + nose + eyes + hair`, then adds clothes, pants, bag and hat on top.
7. Separate sprites can also be "remapped" onto a template attachment at runtime
   (`GetRemappedClone`, the Equip demo). **Not needed here**: our parts are drawn and placed by Valen
   in Spine, so they ship as skins inside Kami's export, and no runtime repacking is needed (one
   character; Kami already renders from two atlas pages).

In the Spine editor Valen can pin several skins in the Skins view to preview a combination
(outfit + boots + scissors) while animating.

## Current state (read from the repo, 2026-10-02)

- **The runtime is spine-unity 4.2** (`Assets/Spine/version.txt`: 4.2, 2026-05-29), installed in
  commit `7f9706bd` (Valen, 2026-09-14). The docs still say 3.8; fixed in the same pass as this spec.
- **In game Kami uses `Atlas 12 Spine4.2`** (`skeleton.json`, Spine 4.2.43) through a scene override
  of `skeletonDataAsset` in both `Nivel1_KamiPapelTijera.unity` and `Level2_Newspaper.unity`.
  `Kami.prefab` itself and `MainMenu.unity` still point at `Atlas 11` (a 3.8.99 export), which the 4.2
  runtime refuses to load (`SkeletonDataCompatibility` accepts 4.2 only). See F2.
- Atlas 12 skins: `default` (67 slots: the whole body), `Tijera_Normal` and `Tijera_Upgrade_1` (2
  slots each: `tijera_back2`, `tijera_front`). The scissors are **already authored the right way**:
  they exist only in skins, never in `default`. Nothing else is a skin yet.
- The "no scissors" look is done by **animations**, not skins: `NoScissortsOverride` and every
  `*NoScissors` animation key both scissors slots to empty. That stays as is.
- Shoes are 12 slots in `default` (6 parts + their `_OL_` outline twins): `25_leg_zapato_front`, `23_leg_zapato_ebilla_front`,
  `26_leg_polaina_front`, `30_leg_zapato_back`, `28_leg_zapato_ebilla_back`, `26_leg_polaina_back`,
  each with an `_OL_` twin (outline layer). The skirt (`21_pollera`, `21_OL_pollera`) has a
  `_side` variant keyed by `TitleScreen`.
- Code: `Player.SetTijeraEquipment()` calls `Skeleton.SetSkin("Tijera_...")`, which **replaces the
  whole skin**: it can never stack a second part. `GiveWaterBoots()` has a TODO for the boots visual
  (issue #17). `hasWaterBoots`/`hasSprintBoots`/`hasTijera` are set when the item is obtained and
  are what gameplay reads.
- `LevelManager.AddResource` fires `Evento.OnResourceUpdated(type, total, isAdding)` for every item
  gain or loss: quests, pickups, confiscation, cheats. One event covers every acquisition path.
- No state survives a scene change (no save system, no `DontDestroyOnLoad` for game state).
- Related open issues: #85 `[POCHI] Spine Multi-slot Setup` ("resolve the Spine version for part
  skins", blocker for #83), #83 `[ART-VALEN] Zapatos Ule Sprite Swap`, #17 (water boots visual),
  #59-#62 (Kami detective art, rig, animations).

### Findings (pre-existing problems this work touches)

- **F1** Docs say runtime 3.8 and "multi-slot only with Spine 4.x". Both wrong: 4.2 is installed, and
  skin combining (`Skin.AddSkin`) existed in 3.8 too. #85 is answered: nothing to upgrade.
- **F2** `Kami.prefab` and `MainMenu.unity` reference the 3.8 export the runtime can't read. Levels
  work only because their scene overrides point at Atlas 12. Any new scene that drops `Kami.prefab`
  in gets a broken Kami, and the MainMenu Kami is most likely not rendering. Not yet confirmed in
  the Editor.
- **F3** Level 2 sets no skin on Kami: no `initialSkinName` override there, and `SetTijeraEquipment`
  returns early because `currentTijera` already defaults to `Normal`. By static read, **Level 2 Kami
  draws no scissors**. Not yet confirmed in the Editor. Gear fixes it by construction (the skin is
  always composed at start).
- **F4** Effects are tied to *owning* an item, so with "one pair of boots at a time" the water and
  sprint boots would both be active while only one is visible (Q4).
- **F5** (2026-10-05, Diego playing Phase 3) **The Wardrobe tab showed during gameplay.** `FlapManager.prefab`
  has two bands: the menu paper (local y above about -35) sits off screen at the top while the Flap is
  closed, and the HUD strip hanging under it (pull tab y -23, health -130, paper ammo -105) is always on
  screen. The 5th tab was placed at y -85, inside the HUD strip. Fixed by task 4.A (FR-304).
- **F6** (2026-10-05, from reading the code, not reproduced) **The Flap can be driven while playing**
  (Diego remembers changing settings "on any tab" during gameplay). The EventSystem navigates with the
  `Horizontal`/`Vertical` axes (arrows, WASD and the stick) and submits with `Interact` (E/Enter/A), and
  the Flap's active display stays live while closed. So a Selectable left selected after closing is
  driven by walking: left/right moves a selected slider (brightness, contrast, volume), E/A presses a
  selected button (Exit included). Two ways a selection survives closing:
  1. **The close slide.** `_isOpen` turns false only when the 0.5 s slide ends, and `FlapManager.Update`
     keeps reading R1/L1/B until then. L1 is also sprint: closing and starting to run fires
     `CambiarTab` -> `ShowDesiredDisplay`, which selects inside the display AFTER `CloseFlap` cleared the
     selection. Joystick only (code selects only for joystick players).
  2. **A click on a Flap Selectable shown while closed.** A click selects it. Before Phase 3 that was
     only the pull tab, whose click opens the Flap anyway; F5's tab was a second one (closed by 4.A).
  Side effect of the same `_isOpen` timing: gameplay input is free during the opening slide and blocked
  for the whole closing slide, the opposite of what feels right (FR-306).

## Concept: three layers, composed in a fixed order

```
default skin (fallback, never changes: face, head, arms, everything no item touches)
  + outfit           (exactly one: outfit/default, outfit/detective)
  + gear slots, in order: Scissors, Feet, (Hat later)   (zero or one item each)
  = Kami's skin
```

- Later layers win, so **gear always beats the outfit**: changing outfit never removes the boots or
  the scissors (request 2).
- **A gear slot owns a set of Spine slots.** When an item is equipped in a gear slot, everything the
  outfit put in those Spine slots is cleared first, then the item is added. This lets an item hide
  part of the outfit (rubber boots have no buckle, so the outfit's `ebilla` must disappear) without
  Valen drawing transparent placeholders. An empty gear slot shows the outfit's own version (the
  detective's shoes, the default shoes).
- Owned Spine slots per gear slot are data (an Inspector list with Spine's slot dropdown), not code.

## Spine authoring contract (for Valen)

1. **One skeleton.** The detective is an outfit *skin inside Kami's skeleton*, not a separate
   skeleton/rig. Same bones, same animations, so the gear attaches to the same bones and every
   existing animation works in both outfits. Outfit meshes can carry their own weights; extra bones
   (coat tails, hat) are added as **skin bones** of `outfit/detective`. (#61 "Kami Detective Rig"
   must be done this way. Q2.)
2. **`default` keeps only what no outfit or item ever changes.** Anything an outfit or item replaces
   or hides moves out of `default` into skin placeholders: the shoes (12 slots), and whatever the
   detective outfit changes (dress, overall, skirt, maybe hair if there's a hat).
3. **Skin folders and names**:
   - `outfit/default`, `outfit/detective`
   - `scissors/normal`, `scissors/upgrade1` (today `Tijera_Normal` / `Tijera_Upgrade_1`: renaming is a
     data edit, see task 2.D)
   - `feet/rain-boots` (the yellow rubber boots); later `feet/lightfall-winged-boots` (replaces the
     sprint boots, see Q4)
   - later `hat/<name>`
4. **Placeholder names are shared across a category.** Every outfit fills the same placeholders
   (`21_pollera`, `21_pollera_side`, ...), every feet item the same feet placeholders. An animation
   keys the placeholder, so it works with any item.
5. **Every outfit fills every placeholder the animations key** (e.g. `21_pollera_side` used by
   `TitleScreen`), or that animation shows a gap in that outfit.
6. **Include the `_OL_` twins** of every part you replace.
7. Preview combinations by pinning skins in the Skins view before exporting.

## Functional requirements

### Phase A: getting an item replaces the part (the current task)

- **FR-001** Kami's skin is always composed from: outfit + equipped Scissors + Feet (+ Hat), in that
  order, over the `default` fallback. Composed at start and on every gear change, never by
  `SetSkin(string)`.
- **FR-002** Each gear slot owns a list of Spine slots; equipping an item clears the outfit's entries
  in those slots before adding the item.
- **FR-003** Gaining a gear item equips it automatically (newest wins in its slot). Hook: one
  subscription to `Evento.OnResourceUpdated` with `isAdding == true`. No quest, pickup or cheat code
  changes.
- **FR-004** Each level defines a **starting loadout** (Inspector asset): Level 1 = default outfit, no
  scissors equipped yet (the pickup gives them); Level 2 = detective outfit + normal scissors. This
  replaces Level 1's `initialSkinName` override and fixes F3.
- **FR-005** Changing the outfit keeps all equipped gear (FR-001's order guarantees it).
- **FR-006** A Spine skin name that doesn't exist in the export logs one warning and is skipped; the
  rest still composes. So code can ship before the art (boots and outfits just stay invisible).
- **FR-007** The "no scissors" look stays animation-driven. Confiscation (`LoseTijera`) does not touch
  the skin; the scissors return with the same skin.
- **FR-008** Gameplay effects are unchanged in Phase A (`hasWaterBoots`, `hasSprintBoots`, scissors
  damage keep their current owned-based logic). Only the visuals are new.

### Phase B: the Wardrobe (later)

- **FR-101** Outfits become items: `ResourceType.outfitDefault`, `outfitDetective` (appended before
  `Count`, in **both** InventoryManager lists). Kami starts every level owning `outfitDefault`; Level 2
  also grants `outfitDetective` (and equips it).
- **FR-102** Tapping (mouse) or pressing A on a gear item in the bag toggles it: equip, or unequip if
  it's already on. Outfits can't be unequipped, only swapped (there's always exactly one).
- **FR-103** Effects follow what is *equipped*: water boots save from drowning only while worn, the
  upgraded scissors' damage only while equipped. (The sprint boots clause is gone with the item, Q4:
  Kami's sprint is `hasSprintBoots`, on in `Kami.prefab`, and stays as it is.)
- **FR-104** A Wardrobe tab in the Flap menu shows one row per gear slot (Outfit, Scissors, Feet, Hat
  when it exists) with the owned items, the equipped one highlighted. Fully navigable with gamepad
  (same rules as the other tabs: R1/L1, A, B, visible selected color).
- **FR-105** Equipping is blocked while the game is frozen for the player (dialogue, cutscene,
  casting, dead, riding a page). The Flap is a pause, so equipping from it is allowed.

### Phase C: carry gear between levels — DROPPED (Q5: Diego, 2026-10-05, no persistence for now)

- ~~**FR-201** What Kami owns and wears at the end of a level is the start of the next, with the level's
  starting loadout as the fallback (playing a level directly from the Editor).~~ Each level starts from
  its own loadout. Revisit only with a save system.

### Phase D: a Flap that only listens while it's open (planned 2026-10-05, F5/F6)

Not gear-specific: the Wardrobe exposed it (Q12 keeps it in this spec, in M4).

- **FR-301** The Flap is always in exactly one state: `Closed`, `Opening`, `Open`, `Closing`. Every Flap
  rule reads that state. Nothing infers "open" from a position or from the end of a slide.
- **FR-302** Menu input (R1/L1 tab cycling, B to close or to answer the exit confirm) and every selection
  made by code happen only in `Open`. Leaving `Open` clears the selection.
- **FR-303** The menu part (every display, the exit confirm, the tab buttons) can't be clicked, selected
  or navigated unless the Flap is `Open`. The HUD strip (pull tab, health, paper, page) is not touched,
  and the pull tab stays clickable in every state (it is what opens the Flap).
- **FR-304** The tab buttons exist only while the Flap isn't `Closed`: shown when it starts opening,
  hidden when it finishes closing, so no tab can show or be clicked during gameplay. **Built 2026-10-05
  (4.A).**
- **FR-305** Safety net: a Selectable inside the menu part found selected while the Flap isn't `Open` is
  deselected, with one warning naming it, so any leak path we haven't found shows up in the console.
- **FR-306** Gameplay input during the slides follows Q14: the menu owns input from the moment it starts
  opening until the moment it starts closing (today it's the reverse).

## Design

### Data (ScriptableObjects, auto-loaded like `TextHighlightSettings`)

- `enum GearSlot { Outfit, Scissors, Feet, Hat }`. The order is the composition order.
- `GearItem` (SO): `slot`, `spineSkin` (`[SpineSkin]` dropdown), `resource` (`ResourceType`, links it
  to the bag item), display name key. One asset per item: `Gear_OutfitDefault`, `Gear_OutfitDetective`,
  `Gear_ScissorsNormal`, `Gear_ScissorsUpgrade1`, `Gear_RainBoots`. (`Gear_SprintBoots` was built as a
  placeholder and deleted on 2026-10-05: Diego retired the sprint boots, see Q4.)
- `GearCatalog` (SO, `Resources/GearCatalog.asset`): the skeleton data (for the Spine dropdowns), all
  `GearItem`s, and per gear slot the **owned Spine slots** (`[SpineSlot]` list). Lookup
  `ForResource(ResourceType)`. Fallback with a warning if the asset is missing: nothing composes,
  the game still runs with the `default` skin.
- `GearLoadout` (SO): one equipped item per slot (Outfit required) plus extra owned items.
  `Level1_StartLoadout`, `Level2_StartLoadout`.

### Runtime (fits Kami's homemade MVC: the parts talk to `Player`, never to each other)

- **`PlayerGear`** (plain C# owned by `Player`, like `PlayerModel`): equipped item per slot,
  `Equip(GearItem)`, `Unequip(GearSlot)`, raises `Player.OnGearChanged`. Built from the scene's
  starting loadout (`Player._startingLoadout`, Inspector).
- **`Player`** subscribes to `OnResourceUpdated`: `isAdding` and the catalog knows that resource ->
  `Gear.Equip(item)` (FR-003).
- **`PlayerView`** listens to `OnGearChanged` and recomposes, through a small static
  **`SpineSkinComposer.Compose(skeletonData, outfit, items, ownedSlots)`** returning the new `Skin`, then
  `SetSkin` + `SetSlotsToSetupPose` + `AnimationState.Apply`. Sketch:

  ```csharp
  var skin = new Skin("kami-gear");
  AddIfFound(skin, data, outfit.spineSkin);
  foreach (GearItem item in equippedInSlotOrder)        // Scissors, Feet, Hat
  {
      foreach (int slotIndex in ownedSlots[item.slot])  // FR-002
      {
          entries.Clear();
          skin.GetAttachments(slotIndex, entries);
          foreach (var e in entries) { skin.RemoveAttachment(slotIndex, e.Name); }
      }
      AddIfFound(skin, data, item.spineSkin);           // FR-006: warn once if missing
  }
  skeleton.SetSkin(skin);
  skeleton.SetSlotsToSetupPose();
  skeletonAnimation.AnimationState.Apply(skeleton);
  ```

- **`SetTijeraEquipment`** keeps the gameplay half (TijeraManager, hitbox) and loses its two skin lines:
  the upgraded pair reaches the skin through FR-003 when `tijeraMejorada` is added.
  `TijeraEquipment` stays as is for now (Phase B can fold it into `GearItem`).

### Phase D: the Flap's states (all in `FlapManager.cs`, no prefab edits)

- `enum FlapState { Closed, Opening, Open, Closing }` replaces `_isOpen`. `MoveFlap` sets `Opening`/`Closing`
  when a slide starts and `Open`/`Closed` when it ends; a reversed slide (open pressed mid-close) just
  starts the other one. `IsMenuOpen`, the pause gate that `PlayerController`, `TriggerOrigami`,
  `InventorySlot` and `Player` read, follows Q14. A second property (`IsFullyOpen`, `State == Open`) gates
  menu input and selection: `Update`, `CambiarTab`, `ShowDesiredDisplay`, `SeleccionarDisplayVisible`.
- Menu part = each `_flapDisplays[i].display`, its `flapButton` and `_seguroOverlay`. Each gets a
  `CanvasGroup`, added by code in `Awake` when missing (code-only wiring, like `UISelector`), driven by
  the state: `blocksRaycasts` only in `Open`; `interactable` false only while `Closed`.
  **Gotcha**: `interactable = false` switches every Selectable under it to its Disabled tint (0.78 gray,
  half alpha). While `Closed` the paper is off screen so it's never seen; during the slides it would
  flash, so the slides rely on "no raycasts + no selection" instead.
- Safety net (FR-305): `Update` already runs every frame; while not `Open` it checks whether
  `EventSystem.current.currentSelectedGameObject` sits under a menu root, and if so calls
  `UISelector.Limpiar()` and warns once per object.
- Tabs (FR-304, built): `SetTabsVisible` activates every `flapButton` in `OpenFlap` and deactivates them
  when the closing slide ends, plus once in `Start`.

### What does not change

Tracks and mix times, the NoScissors overrides, bone followers (`tijera_front3`, `9_HEAD`, `Smoke`;
bones aren't skin-dependent), the hit flash (a MaterialPropertyBlock on the renderer, not slot
colors; nothing in game code tints slots, so `SetSlotsToSetupPose` can't wipe anything).

## Decisions (were open questions; Diego took every default, 2026-10-02)

- **Q1 Names.** "Gear" (system) + "Wardrobe" (menu tab). *Default: yes.*
- **Q2 Detective = a skin of Kami's skeleton, not a separate rig.** Needed for the gear to carry
  over and the animations to be shared; #61-#62 have to be redirected that way. *Default: yes; I
  comment it on #61 when filing issues.*
- **Q3 Shoes belong to the outfit**, and the Feet slot is empty-able: rubber boots off = the outfit's
  own shoes (default or detective). The alternative is a "shoes" item that's always in the Feet slot.
  *Default: shoes belong to the outfit.*
- **Q4 One pair of boots at a time makes water and sprint boots mutually exclusive** in Phase B
  (FR-103): picking rubber boots means no sprint. Today only the water boots are actually obtainable
  (Chino's quest is a stub). *Default: yes, exclusive. That's the interesting choice, and it costs
  nothing now.* In Phase A both effects stay owned-based and the newest boots are the visible ones.
  **Note (Diego, 2026-10-02): the sprint boots will be replaced by the "Lightfall winged boots"** (a
  Feet item; design not started). Left for later: no art or code task is filed for the sprint boots
  or the winged boots. When it happens it's one `GearItem` asset + one `feet/` skin, nothing else.
  **Update (Diego, 2026-10-05): the sprint boots are retired.** Nothing gives them (the P cheat no longer
  does; Chino's quest still names them as its reward, but Chino is a stub) and `Gear_SprintBoots` was
  deleted. Q4 is moot until the winged boots exist: today the rain boots are the only Feet item.
- **Q5 Carry gear across levels** (Phase C)? Level 2 already resets to normal scissors by design.
  *Default: no carry-over for now; per-level starting loadouts. Revisit with a save system.*
  **Confirmed (Diego, 2026-10-05): no persistence between levels for now.** Phase C is dropped.
- **Q6 "Botas de ule" = the water boots** (`botasAgua`, Tiburcio's reward, issue #83's "zapatos ule").
  *Default: yes, the same item: `feet/rain-boots`, yellow.*
- **Q7 F2 fix**: point `Kami.prefab` (and MainMenu's Kami) at Atlas 12 so the scene overrides become
  redundant. Small YAML edit, but MainMenu's Kami then needs a look in the Editor. *Default: do it as
  task 0.A.*
- **Q8 Level 2's detective outfit arrives with no reward pose** (she simply is in it when the level
  starts; the bag shows both outfits). *Default: no pose.*

Asked before building Phase 3 (Diego took every default, 2026-10-05):

- **Q9 What the player can change from the bag/Wardrobe**: outfits and Feet only, as in request 3.
  The scissors stay auto-equipped (newest pair), so FR-102's "any gear item" is narrowed and FR-103's
  scissors clause holds by construction. Data, not code: `GearCatalog._changeableFromBag`.
- **Q10 Granting what a level starts with**: one frame after `Start`, quietly (`ownedAtLevelStart`: no
  sticker, no auto-equip), and Level 2's starting scissors through the same path (they used to pop a
  sticker at level start).
- **Q11 Wardrobe layout**: a 5th Flap tab appended last, rows authored in the prefab, item buttons spawned
  at runtime from `InventorySlot.prefab` (placeholder until Valen's #151).

After playing Phase 3 (Diego, 2026-10-05):

- **Q12 Where the Flap fix lives**: Phase 4 of this spec (replacing the dropped Phase C), sub-issues of
  epic #139 in M4, so it ships in the public build with the Wardrobe that exposed it. *Chosen.*
- **Q13 The visible Wardrobe tab**: fixed right away on the Phase 3 branch by hiding every tab while the
  Flap is closed (4.A), rather than re-spacing the tabs. The rest of Phase 4 gets its own session. *Chosen.*
- **Q14 Gameplay input during the slides** (FR-306), to confirm at the Phase 4 kickoff. *Default: the menu
  owns input from the moment it starts opening until the moment it starts closing*, so pressing Esc
  freezes Kami at once and closing gives her back at once. Today it's the reverse: she can still move
  during the 0.5 s opening slide, and can't move during the 0.5 s closing one.

## Verification

- `python tools/compile-check.py` (compilation only, not runtime).
- Diego in the Editor, Level 1: no scissors before the pickup, normal ones after it, upgraded after
  the P cheat; rubber boots appear when Tiburcio's quest pays (once Valen's skin exists; before
  that, one warning and nothing else). Every NoScissors animation still hides the scissors; no frame
  shows them during a skin change.
- Level 2: detective outfit + normal scissors from the first frame (F3 gone); confiscation hides and
  returns the scissors; switching outfit keeps boots and scissors (Phase B).
- Spine side: the warning list on start names every gear skin missing from the export.
- Phase 4 (Flap): no tab ever shows during gameplay. With a joystick, close the Flap and immediately hold
  L1 (sprint), then walk and press A for a while: no slider moves (brightness, contrast, volume stay put)
  and nothing in the menu gets pressed. Same with the mouse: open with the pull tab, click a slider, close
  with Esc, walk. Opening and closing fast, the exit confirm with B, and every tab (Wardrobe included)
  keep working. Console: no FR-305 warning in normal play (one would mean a leak path still exists).

## Out of scope

Runtime sprite remapping/repacking, Spine 4.x multi-character skin sharing, hats (only the slot is
reserved), NPC skins.
