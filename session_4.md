# Session 4 Summary - 2025-12-25

## Cards Tested and Fixed

### Shadow Lord (127)
- **Issue**: Graveyard aura wasn't applying stat buffs to shadow summons
- **Fixes**:
  - Fixed typo in JSON: `statModfiers` → `statModifiers`
  - Fixed cloned passive conditions being re-verified on target instead of source
  - Added `RemovePassivesFromSource()` and `CheckForPassives()` when cards leave graveyard
  - Added `CheckForPassives()` call when cards enter graveyard

### Fisher (125)
- **Issue**: Couldn't target opponent for sacrifice effect
- **Fixes**:
  - Changed `sourcePlayer: "opponent"` to `targetType: "opponent"` in JSON
  - Added `GetWeakestSummon()` method for targeting weakest summon

### Reaper (131)
- **Issue**: Couldn't select opponent's summons to discard
- **Fixes**:
  - Rewrote JSON to use `targetType: "opponentHand"`, `restrictions: ["summon"]`, `resolveTarget: true`
  - Added `Summon` to Restriction enum with corresponding check in `QualifyTarget`
  - Fixed castability: `resolveTarget: true` effects skip target validation at cast time
  - Fixed reveal to only show filtered cards matching restrictions
  - Fixed halting with no valid targets (return `bool` from `RequestResolveTimeTargets`)

### Duress (133)
- **Issue**: Same issues as Reaper for non-summon targeting
- **Fixes**: Benefited from all Reaper fixes (shared code paths)

### Edict (128)
- **Issue**: Didn't prompt both players to sacrifice a summon
- **Fixes**:
  - Added Sacrifice handling to `HandleEachPlayerEffect()` and `HandleEachPlayerSelection()`
  - Updated JSON to use `eachPlayer: true`

### Strongfall (132)
- **Issue**: Couldn't target opponent for sacrifice effect
- **Fixes**:
  - Changed `sourcePlayer: "opponent"` to `targetType: "opponent"` in JSON
  - Added `GetStrongestSummon()` method for targeting strongest summon
  - Added handling for `targetBasedOn == TargetBasedOn.Strongest` in Sacrifice effect

### Dark Shade (124)
- **Issue 1**: Triggered ability didn't proc on opponent discard
- **Fixes**:
  - Changed `sourcePlayer: "opponent"` to `player: "opponent"` in triggeredEffects JSON
  - Added Discard to exception list for card qualification (like Draw triggers)

- **Issue 2**: Activated ability didn't resolve when opponent is bot
- **Fixes**:
  - Added bot auto-discard handling in `RequestPlayerChoiceDiscard()` - bots select highest index cards
  - Added `affectedPlayer: "opponent"` to effect JSON so Effect.Resolve knows whose hand is affected

- **Issue 3**: `oncePerTurn` not limiting activated ability
- **Fixes**:
  - Added `usedThisTurn` property to `ActivatedEffect`
  - Check `oncePerTurn && usedThisTurn` in `CostIsAvailable()`
  - Set `usedThisTurn = true` when ability activates in `ActivateAbility()`
  - Added `ResetOncePerTurnAbilities()` method called at turn end in `PassTurn()`

### Vanquish (126), Fable (129), ItsAlive (130)
- Tested and working correctly (no fixes needed)

## Code Changes Summary

### New Methods Added
- `GameMatch.GetWeakestSummon(Player)` - Returns weakest summon by attack, then defense
- `GameMatch.GetStrongestSummon(Player)` - Returns strongest summon by attack, then defense
- `GameMatch.ResetOncePerTurnAbilities()` - Resets usedThisTurn for all activated effects

### New Properties Added
- `ActivatedEffect.usedThisTurn` - Tracks if ability was used this turn

### Key Pattern Fixes
- `targetType: "opponent"` is the correct field for opponent targeting (not `sourcePlayer`)
- `player: "opponent"` is correct for triggered effects filtering by who caused the trigger
- `affectedPlayer: "opponent"` tells Effect.Resolve who is affected by the effect
- `resolveTarget: true` effects skip target validation at cast time and select at resolution
- Discard triggers (like Draw) should skip card qualification - they fire on any discard

## Files Modified
- `LifeServer/Server/GameMatch.cs`
- `LifeServer/Server/Utils.cs`
- `LifeServer/Server/StackObj.cs`
- `LifeServer/Server/CardProperties/Effect.cs`
- `LifeServer/Server/CardProperties/ActivatedEffect.cs`
- `LifeServer/Server/CardProperties/Restriction.cs`
- `Data/Cards/124_DarkShade.json`
- `Data/Cards/125_Fisher.json`
- `Data/Cards/127_ShadowLord.json`
- `Data/Cards/128_Edict.json`
- `Data/Cards/131_Reaper.json`
- `Data/Cards/132_Strongfall.json`

## Test Stats
- **Cards Tested**: 10
- **Cards Passed**: 10
- **Total Passed**: 136/281
