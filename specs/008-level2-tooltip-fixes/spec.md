# Spec 008: Level 2 tooltip fixes (Natalia language, trash can position)

Status: diagnosed and applied 2026-09-30 (Natalia key; Azul anchored bottom-centre at y=-260, i.e. BELOW the player on purpose (Diego tuned it from my +170 guess); size left at default 611x187). Verified in Play by Diego. Reported by Diego while playtesting Level 2.

## Reported symptoms

1. Natalia's "talk" tooltip shows in English while the rest of the game is in Spanish.
2. The trash can tooltip sits in the middle of the screen, covering Kami and the can. It should
   sit lower, like the other central tooltips.

## Diagnosis

### Bug 1: English tooltip is a raw string, not a localization key

- `Assets/Prefabs/NPCs/Natalia.prefab:230` has `tooltipTextToShow: Press E to talk`.
- `TooltipManager.ShowTooltip` -> `LocalizedText.Escribir(..., "TooltipTable")` treats that field
  as a **table key**. "Press E to talk" is not a key, so `LocalizedText.cs:104-115` misses and
  falls back to writing the raw string. So it is English in every language.
- The correct key already exists and is translated: `dialogue_tooltip`
  (`TooltipTable_es`: "Tocá E para hablar", `_en`: "Press E to talk", `_pt`: "Pressione E para
  conversar"). `GiftBox.prefab` and `Grace.prefab` already use it.
- Root cause: Natalia was authored with a literal English string instead of the key (she was built
  after the 2026-09-09 "English everywhere" rule, which applies to code/assets, not to player-facing
  text that goes through localization). Older NPCs (Abuela, Norberto, Florista) use literal Spanish
  the same way, which happens to look right only in Spanish.
- Not a locale problem: the trash can (`trashcan_tooltip`) and `dialogue_tooltip` resolve to Spanish
  fine, which rules out the locale setting and the table loading.

### Bug 2: the Azul post-it in Level 2 has no position override

- `TrashCan.prefab` uses `postItColor: 0` (Azul), `_maxTooltipShows: 1`.
- In `Level2_Newspaper.unity` the `PostItAzul` instance only overrides pivot/anchors (all 0.5);
  there is **no `m_AnchoredPosition` and no size override** (around line 140344). So it keeps the
  `PostIt.prefab` defaults: anchor centre, position (0,0), size 611x187 = the exact middle of the
  screen, on top of Kami.
- Level 1 has no such problem: `Main Canvas.prefab` overrides its blue-slot post-it
  (`PostItBlanco (Ex-Azul)`) to anchor bottom-centre (0.5, 0), size 800x170. Level 2's canvas was
  built separately and never got this.
- The other Level 2 colours were already moved to the screen edges (Naranja x -1050, Rosa/Amarillo/
  Verde x +1050), so Azul is the only central slot and the only one left unplaced.
- Side effect to be aware of: Azul is shared by every Azul trigger in Level 2 (page-turn spheres
  `esferaPrev`/`esferaNext`, the trash cans, etc.), so the fix also moves those. That is desired:
  they are all the "central" tooltips.

## Plan

### Fix 1 (data only, 1 line)

In `Natalia.prefab`, change `tooltipTextToShow: Press E to talk` to `tooltipTextToShow: dialogue_tooltip`.
No code change, no new table entry.

### Fix 2 (scene override)

In `Level2_Newspaper.unity`, on the `PostItAzul` instance, add overrides matching Level 1's
central slot: anchor min/max (0.5, 0), size about 800x170, anchored position lifted to a height that
clears Kami and the can (start around y = +170 and tune visually in Play).

Caveat: Level 1's prefab override reads `m_AnchoredPosition.y = -260` with a bottom anchor, which
would be off-screen on its face. I did not resolve how that resolves in the Level 1 canvas (extra
parent scaling or a different target in the same override list). **Do not copy -260**; set the value
by eye in the Editor on the Level 2 canvas, or confirm Level 1's real on-screen result first.

Alternative considered: hand the trash can a different colour. Rejected: the other free colours sit
at the screen edges, which reads as "side note", not as a prompt at the object.

### Optional follow-up (not part of this fix)

Sweep every `tooltipTextToShow` that is a literal instead of a key (Abuela, Norberto, Florista,
TriggerSolapa, the five `SelloOrigami*` ones, etc.). They look right in Spanish only; EN/PT players
see Spanish. Needs new table keys in three languages, so it is a separate task. Related debt is
already noted for `origami_guide` in `docs/claude/controles-y-gamepad.md` (#41.5).
A cheap guard would be to set `avisarSinClave = true` for TooltipManager so a missing key logs a
warning instead of silently showing the raw string.

## Verification

Compile-check is irrelevant (data-only). Verify in the Editor, Level 2:

1. Play with the Spanish locale: walk into Natalia's trigger, the post-it reads "Tocá E para hablar".
   Switch to EN and PT: text follows.
2. Walk to any trash can: the tooltip appears low on screen, does not cover Kami or the can.
3. Walk to a page edge sphere (also Azul): same low position, still readable.
4. Check the Naranja/Rosa/Amarillo/Verde post-its did not move.
5. Confirm at 16:9 and one other aspect ratio (anchors are bottom-centre, so it should hold).

## Files touched

- `Assets/Prefabs/NPCs/Natalia.prefab` (one field)
- `Assets/Scenes/Level2_Newspaper.unity` (PostItAzul RectTransform overrides)
- `docs/claude/origami-y-tooltips.md`: add a line that `tooltipTextToShow` is a TooltipTable key,
  and that Level 2's Azul slot is positioned by a scene override.
