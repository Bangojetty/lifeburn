# Triggered Ability Migration

**Date:** 2025-12-15
**Status:** In Progress (6 cards migrated, old system retained for backwards compatibility)

---

## Overview

This document describes the migration from the old `additionalCosts` system to the new `isCost` effect system for triggered abilities.

### The Problem

The old system had two separate code paths for handling costs:
- **Card-level costs** - Paid BEFORE putting something on the stack
- **Triggered effect costs** - Paid via a separate `additionalCosts` array on `TriggeredEffect`

This caused confusion because triggered effect "costs" aren't really costs in the traditional sense - they're actions that happen DURING resolution, not before.

### The Solution

Costs for triggered effects are now regular effects with an `isCost: true` flag, positioned in the effects array where they should execute during resolution.

---

## Migration Format

### Before (Old Format)
```json
"triggeredEffects": [{
  "trigger": "openingHand",
  "optional": true,
  "additionalCosts": [
    { "costType": "reveal" }
  ],
  "effects": [
    { "effect": "draw", "amount": 1 }
  ]
}]
```

### After (New Format)
```json
"triggeredEffects": [{
  "trigger": "openingHand",
  "optional": true,
  "effects": [
    { "effect": "reveal", "scope": "selfOnly", "isCost": true },
    { "effect": "draw", "amount": 1 }
  ]
}]
```

---

## Cost Types and Their Effect Equivalents

| Old additionalCost | New isCost Effect |
|-------------------|-------------------|
| `{ "costType": "reveal" }` | `{ "effect": "reveal", "scope": "selfOnly", "isCost": true }` |
| `{ "costType": "sacrifice", "scope": "selfOnly" }` | `{ "effect": "sacrifice", "scope": "selfOnly", "isCost": true }` |
| `{ "costType": "sacrifice", "tokenType": "stone", "amount": 1 }` | `{ "effect": "sacrifice", "tokenType": "stone", "isCost": true }` |

---

## Behavior

### Auto-Pay Logic
The following costs are automatically paid without user input:
- **Reveal with `scope: selfOnly`** - Reveals the source card
- **Sacrifice with `scope: selfOnly`** - Sacrifices the source card
- **Sacrifice with `tokenType`** when only ONE matching token exists

### User Selection Required
- **Sacrifice with `tokenType`** when MULTIPLE matching tokens exist - User must choose which to sacrifice

### Fizzle Behavior
If a cost cannot be paid during resolution:
- Effects BEFORE the cost have already resolved and remain in effect
- The cost effect and all REMAINING effects fizzle (do not execute)
- Resolution completes normally

---

## Migrated Cards

| Card ID | Card Name | Cost Type | Status |
|---------|-----------|-----------|--------|
| 97 | Ghastly | Reveal self | ✅ Migrated |
| 99 | Loot Ghost | Reveal self | ✅ Migrated |
| 13 | Reconfigure Golem | Sacrifice stone | ✅ Migrated |
| 107 | Ghastly Tutor | Sacrifice self | ✅ Migrated |
| 162 | Goblin Portal | Reveal self | ✅ Migrated |
| 206 | Transparent Plant | Reveal self | ✅ Migrated |

---

## Files Modified

### C# Files
- `Effect.cs` - Added `isCost` property and helper methods (`CanPayCost`, `NeedsCostSelection`, `GetCostSelectableUids`)
- `StackObj.cs` - Updated `ResolveStackObj` to handle `isCost` effects
- `GameMatch.cs` - Added `costEffectForSelection` field, `RequestCostEffectSelection` method, and handler in `HandleCostSelection`
- `TriggeredEffect.cs` - Updated `CostsArePayable` to check both old and new formats

### JSON Card Files
- `097_Ghastly.json`
- `099_LootGhost.json`
- `013_ReconfigureGolem.json`
- `107_GhastlyTutor.json`
- `162_GoblinPortal.json`
- `206_TransparentPlant.json`

---

## Backwards Compatibility

The old `additionalCosts` system is still functional and checked by `CostsArePayable()`. This allows:
1. Gradual migration of cards
2. Testing new format before removing old code
3. Rollback if issues are discovered

Once all cards are migrated and tested, the following can be removed:
- `TriggeredEffect.additionalCosts` field
- `SendNextTriggerCostEvent()` method in GameMatch.cs
- Related cost handling code in `HandleCostSelection()`

---

## Testing Checklist

- [ ] Ghastly (97) - Reveal + variable discard
- [ ] Loot Ghost (99) - Reveal + draw + discard
- [ ] Reconfigure Golem (13) - Sacrifice stone from graveyard
- [ ] Ghastly Tutor (107) - Sacrifice self on attack
- [ ] Goblin Portal (162) - Reveal + create token
- [ ] Transparent Plant (206) - Reveal + create tokens
- [ ] Test with 0 stones (ReconfigureGolem should not be able to activate)
- [ ] Test with 1 stone (ReconfigureGolem should auto-sacrifice)
- [ ] Test with 2+ stones (ReconfigureGolem should prompt for selection)
