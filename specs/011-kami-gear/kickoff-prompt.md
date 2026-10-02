# Kickoff prompt — Spec 011 (Kami Gear), Phase 0.A + Phase 2

Paste everything below the line into a fresh Claude Code session at the repo root.

---

We're implementing **Phase 2 of spec 011, "Kami Gear"** in KamiPapelTijera (Unity 2021.3, URP,
spine-unity **4.2**), plus the small Phase 0 fix that goes first. Goal of this session: **getting an
item in game replaces Kami's Spine part**. Kami's skin is composed at runtime from
`default` (fallback) + outfit + scissors + feet (+ hat later), so parts stack and survive an outfit
change. The design is approved (Diego took every default, 2026-10-02). Don't redesign it; build it.

## Branch and state

- Spec, tasks and doc fixes were written on `level2newspaper-pages-blocking-2` and may still be
  uncommitted. **First thing: ask Diego which branch to work on** (my suggestion: commit the spec
  where it is, then branch `011-kami-gear` from there). Never commit, push or open a PR without his
  explicit permission, every time.
- GitHub (M4): epic **#139**. This session = **#140** (0.A), then **#142 → #143 + #144 → #145 → #146**.
  Art is in parallel and NOT needed for this session: #141 (Valen restructures the skins), #83 (rain
  boots skin), #61 (detective outfit skin). Phase 3 (#147-#151, the Wardrobe) is out of scope.

## Read first

1. `specs/011-kami-gear/spec.md`: everything, especially "How Spine mix-and-match works",
   "Concept", "Design" and "Decisions".
2. `specs/011-kami-gear/tasks.md`: Phase 0 and Phase 2.
3. `CLAUDE.md` + `docs/claude/spine-kami.md` (tracks, NoScissors overrides, the track 3 gotcha).
4. Code: `Assets/Scripts/Player/Player.cs` (`Awake` builds Model/View/Controller; `Start`;
   `SetTijeraEquipment`, `EquipTijera`, `GetTijeraMejorada`, `LoseTijera`, `_startWithTijera`),
   `PlayerView.cs`, `Managers/LevelManager.cs` (`AddResource`, `GiveWaterBoots` TODO,
   `GiveTijeraMejorada`, `AllItemsCheat`), `Level2/ConfiscatedGear.cs`, `Inventory/InventoryItem.cs`,
   `UI/TextHighlightSettings` (the `Resources.Load` + fallback pattern to copy).
5. Spine runtime, not the docs: `Assets/Spine/Runtime/spine-csharp/Skin.cs` (`AddSkin`,
   `GetAttachments`, `RemoveAttachment`), `Skeleton.cs` (`SetSkin`, `GetAttachment`), and the demo
   `Assets/Spine Examples/Scripts/Mix and Match Character Customize/MixAndMatchSkinsExample.cs`.

## Scope, in order

- **#140 (0.A)**: in `Kami.prefab` and `MainMenu.unity`, replace the `skeletonDataAsset` GUID
  `d686b3724b809514bb6fadf42b2d1142` (Atlas 11, a 3.8 export the 4.2 runtime rejects) with
  `edc59de31b75beb428810d6ee630f293` (`Atlas 12 Spine4.2`). Leave the level-scene overrides.
  Diego checks MainMenu's Kami in the Editor afterwards.
- **#142 (2.A)** contracts in `Assets/Scripts/Gear/`: `GearSlot`, `GearItem`, `GearLoadout`,
  `GearCatalog` exactly as the spec's "Design > Data" says. The catalog implements
  `IHasSkeletonDataAsset` so `[SpineSkin]`/`[SpineSlot]` show dropdowns, and holds the owned Spine slots per
  gear slot.
- **#143 (2.B)** and **#144 (2.C)**, parallel-safe (different files):
  - 2.B: static `SpineSkinComposer` + `PlayerView` recomposes on `Player.OnGearChanged`.
  - 2.C: `PlayerGear` (plain C# owned by `Player`, like `PlayerModel`), `Player._startingLoadout`,
    auto-equip from `Evento.OnResourceUpdated`, and `SetTijeraEquipment` loses its two `SetSkin` lines.
- **#145 (2.D)** assets and wiring, ONE writer: six `GearItem`s, `Resources/GearCatalog.asset`,
  `Level1_StartLoadout` / `Level2_StartLoadout`, `_startingLoadout` on Kami (prefab default = Level 1,
  scene override in `Level2_Newspaper.unity`), and remove Level 1's `initialSkinName: Tijera_Normal`
  override in `Nivel1_KamiPapelTijera.unity`.
- **#146 (2.E)** close-out (below).

## Decided rules: don't reopen these

- Composition order: outfit, then Scissors, Feet, Hat. Last added wins, so gear beats the outfit.
- A gear slot owns Spine slots (Inspector list). Equipping an item clears the outfit's entries in those
  Spine slots, then adds the item's skin. An empty gear slot shows the outfit's own version.
  Owned slots: Scissors = `tijera_back2`, `tijera_front`; Feet = the **12** shoe slots
  (`25_leg_zapato_front`, `23_leg_zapato_ebilla_front`, `26_leg_polaina_front`, `30_leg_zapato_back`,
  `28_leg_zapato_ebilla_back`, `26_leg_polaina_back`, and the `_OL_` twin of each).
- Shoes belong to the outfit; Feet can be empty. One item per gear slot; newest gained wins.
- Getting an item = ONE hook: `Player` subscribes to `Evento.OnResourceUpdated` (params: type, current
  total, `isAdding`). Gains only; removals (`LoseTijera` adds -1) are ignored. No quest, pickup or cheat
  code changes.
- "No scissors" stays animation-driven (`*NoScissors` / `NoScissortsOverride` key both scissors slots
  empty). Confiscation never touches the skin.
- Gameplay effects stay owned-based in this phase (`hasWaterBoots`, `hasSprintBoots`, scissors damage).
  Visuals only.
- A Spine skin name missing from the export warns **once** and is skipped; the rest composes. Code
  ships before the art: use the current names `Tijera_Normal` / `Tijera_Upgrade_1`; the outfit and boots
  skins don't exist yet (`outfit/default`, `outfit/detective`, `feet/rain-boots`, and a placeholder
  `Gear_SprintBoots` whose skin will never exist: the Lightfall winged boots replace it later).
- Everything a designer touches is an Inspector-editable asset (`Resources.Load` + warning fallback,
  like `TextHighlightSettings`); a missing catalog must not break the game (Kami falls back to `default`).
- Level 2 starting loadout = detective outfit + normal scissors; Level 1 = default outfit, no
  scissors until the pickup. No reward pose for the outfit.

## Gotchas already paid for (Spine 4.2 source + this repo)

- `Skeleton.SetSkin(skin)` **returns immediately if it's the same object**: build a `new Skin` every
  compose (cheap, only on gear change).
- After `SetSkin`: `SetSlotsToSetupPose()` and then `AnimationState.Apply(skeleton)` **in the same
  frame**. Otherwise the scissors flash on for one frame while a NoScissors animation is playing.
- Whatever is in the `default` skin can be replaced but never removed. The owned-slot clearing only
  affects entries in the composed skin.
- Removing entries: `skin.GetAttachments(slotIndex, list)` copies into your list, then
  `RemoveAttachment(slotIndex, entry.Name)`. Never remove while iterating `skin.Attachments`.
- `SetTijeraEquipment` currently early-returns when the value is unchanged, and `currentTijera` defaults
  to `Normal`. That's why **Level 2 Kami has no skin today** (finding F3, static read). The composed skin
  at start fixes it. Make `Equip` idempotent (equipping the same item raises no event): Level 2's
  `_startWithTijera` and the loadout both equip the normal scissors.
- `Player.Awake` builds `PlayerView`, and `SkeletonAnimation` initializes in its own `Awake`. Compose the
  first skin in `Player.Start` or later, never in `Awake`.
- `EventManager` swallows handler exceptions into a log: a broken handler fails silently. Log your
  decisions (`Debug.Log($"[PlayerGear] ...")`) so Diego can follow it in the console.
- Kami is a prefab instance in both levels, each with a `skeletonDataAsset` override. Edit only what you
  need.
- New ScriptableObject assets by hand: copy the YAML shape of `Assets/Scripts/Inventory/ItemTijera.asset`
  (its `m_Script` points at the script's GUID). `.meta` files for new scripts and folders:
  `python tools/make-meta.py <paths>`.
- YAML surgery (prefab/scene): read the whole file first, preserve its line endings (CRLF or LF, it varies
  by file), count `--- !u!` blocks before and after, grep new fileIDs for uniqueness.
- Deleting code: list exactly what falls in the range before cutting and re-list the remaining methods
  after. A deleted Unity lifecycle method compiles clean and breaks silently.

## Working rules

- English for everything new (code, comments, asset names, docs) and in replies to Diego. Leave existing
  Spanish alone.
- Comments explain the non-obvious *why*. Braces always, guard clauses that warn,
  `[SerializeField] private` + `[Tooltip]`.
- Do the mechanical work yourself (YAML, assets, `.meta`): don't hand Diego Editor busywork. Ask him
  about decisions, not labour.
- Agents: at most 2 code agents (2.B and 2.C in parallel after 2.A). **Only one agent writes scenes and
  prefabs** (#140 and #145).
- Triangulate the code first. If something in the spec doesn't match the code, stop and ask with a
  recommended default, before building.

## Verification

- `python tools/compile-check.py` after each step. Known baseline warnings: `JumpFloodOutlineRenderer`
  CS0162, `HongueroTiburcioDialogueTrigger` CS0414. **Compilation only**: say clearly what wasn't run.
- Diego's playtest checklist (from the spec):
  - **Level 1**: no scissors before the pickup, normal after it, upgraded after the P cheat.
    NoScissors animations still hide them, with no flash on a skin change.
  - **Console**: one warning per missing gear skin, nothing else.
  - **Level 2**: Kami has scissors from the first frame, and confiscation hides and returns them.
  - **MainMenu**: Kami renders (after #140).

## Close-out (#146)

- Update `docs/claude/spine-kami.md` (Skins section: how gear composes, the owned-slots table, how to add
  an item = one `GearItem` asset + one Spine skin) and `CLAUDE.md` (equipment section + "where
  everything is": `Assets/Scripts/Gear/`).
- Run `/graphify Assets/Scripts --update`, mark Phase 2 done in `tasks.md` with any design changes vs.
  the spec and why.
- Leave everything uncommitted for Diego's review. When he has played it and asks, post a status comment
  on #140 and #142-#146, close them, and comment on epic #139 with what Phase 3 inherits.
