# Session 9 Summary

## Cards Tested and Fixed

### Treefolk Batch (10 cards marked as Passed)
- PlantGrower
- Sproutlings
- PlantSprouter
- PlatePlant
- CliffsideSprout
- GlowingSpore
- DeadTree
- GiftOfNature
- TransparentPlant
- PerfectBog

---

## Bugs Fixed

### 1. Plant Sprouter - Wrong Trigger Type
**Issue:** Card was using `enteredZone` trigger instead of `cast` trigger, and was including opponent's summons.
**Fix:** Changed trigger from `enteredZone` with `zone: "play"` to `trigger: "cast"` with `scope: "selfOnly"`.
**File:** `Data/Cards/200_PlantSprouter.json`

### 2. Cliffside Sprout - Triggering on Both Players' Draw Step
**Issue:** The draw phase trigger was firing for both players instead of just the controller.
**Fix:** Added `"phaseOfPlayer": "player"` to the phase trigger.
**File:** `Data/Cards/202_CliffsideSprout.json`

### 3. Transparent Plant - Discard Ability Not Targeting
**Issue:** The discard ability didn't let the player choose a target before going on the stack, and it was applying to all summons instead of just treefolk.
**Fix:** Added `"targetType": "summon"` and `"tribe": "treefolk"` to the inner `grantPassive` effect (not just the ActivatedEffect).
**File:** `Data/Cards/206_TransparentPlant.json`

### 4. Dead Tree - Exiling Itself Instead of Graveyards
**Issue:** Card was exiling itself instead of all cards in all graveyards.
**Root Cause:** Used `targetZones: ["graveyard"]` but `GetSelectZone()` only checks `select.zone` or `zone`, not `targetZones`.
**Fix:** Changed `"targetZones": ["graveyard"]` to `"zone": "graveyard"` for non-targeting effects.
**File:** `Data/Cards/204_DeadTree.json`

### 5. Glowing Spore - Crash on Resolution
**Issue:** Assertion error "Neither player controls that card" when trying to give life to destroyed permanent's controller.
**Root Cause:** `GetControllerOf()` was called on a card that was already destroyed and removed from play.
**Fix:** Modified RootController handling to use `lastControllingPlayer` for cards no longer in play:
```csharp
Player targetController = affectedCard.currentZone == Zone.Play
    ? gameMatch.GetControllerOf(affectedCard)
    : affectedCard.lastControllingPlayer ?? gameMatch.GetOwnerOf(affectedCard);
```
**File:** `LifeServer/Server/CardProperties/Effect.cs` (line ~844)

### 6. Plant Grower - Sacrifice Not Working After Combat
**Issues:**
1. Missing `phaseOfPlayer: "player"` on the phase trigger
2. Missing `scope: "selfOnly"` on the sacrifice effect
3. Condition was on the effect instead of the triggered effect (trigger was firing even when player hadn't attacked)

**Fix:** Moved conditions to the triggered effect level and added proper scope:
```json
{
  "trigger": "phase",
  "phase": "secondMain",
  "phaseOfPlayer": "player",
  "conditions": [{ "condition": "attacked" }],
  "effects": [{ "effect": "sacrifice", "scope": "selfOnly" }]
}
```
**File:** `Data/Cards/198_PlantGrower.json`

### 7. Plant Sprouter - Creating Too Many Tokens
**Issue:** Creating 2 tokens when only 1 treefolk summon was in play.
**Root Cause:** `TreefolkControlled` was counting both treefolk cards AND treefolk tokens (Plant tokens have `tribe: "treefolk"`).
**Fix:** Removed the line that counts treefolk tokens since the description says "treefolk summon" not "treefolk":
```csharp
// Removed: tempAmount += player.tokens.Count(t => t.tribe == Tribe.Treefolk);
```
**File:** `LifeServer/Server/GameMatch.cs` (line ~1983)

### 8. Gift of Nature - No Target Selection Before Stack
**Issue:** Card didn't let player choose a target before going on the stack, and played exile animation on itself.
**Root Cause:** Effect had `targetZone: "graveyard"` but no `targetType`, so `HasTargeting()` returned false.
**Fix:** Added `targetType: "graveyard"` to the effect and added `TargetType.Graveyard` handling in `GetPossibleTargets()`.
**Files:**
- `Data/Cards/205_GiftOfNature.json`
- `LifeServer/Server/GameMatch.cs`

---

## Other Changes

### Fertilize Description Update
Changed description from "Tokens you control have Sprout 1" to "Non-herb tokens you control have sprout 1 this turn."
**File:** `Data/Cards/217_Fertilize.json`

---

## Key Learnings

1. **`zone` vs `targetZones`:** For non-targeting effects that affect all cards in a zone, use `zone: "graveyard"`. Use `targetZones` only for targeting effects that need to select specific cards.

2. **Trigger conditions:** Place conditions on the triggered effect level (not the inner effect) if you want to prevent the trigger from firing at all when conditions aren't met.

3. **Phase triggers:** Always add `phaseOfPlayer: "player"` for phase triggers that should only fire on the controller's turn.

4. **Effect targeting:** Both the outer structure (ActivatedEffect/TriggeredEffect) AND the inner Effect need `targetType`/`tribe` for proper target selection and filtering.

5. **RootController for destroyed cards:** Use `lastControllingPlayer` as a fallback when the card is no longer in play.

---

## Test Tracker Update
- **Untested:** 80 → 70
- **Passed:** 201 → 211

## Next Cards to Test
1. TreeGiant
2. SpiritTree
3. WarbriarStomper
4. VerdictCommand
5. Entangle
6. Harvest
7. LostSanctuary
8. PlanterBox
9. GrowTall
10. Fertilize
