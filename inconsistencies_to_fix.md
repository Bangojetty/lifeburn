# Card System Inconsistencies To Fix

**Created:** 2025-12-15
**Status:** In Progress
**Total Issues:** 10

---

## HIGH PRIORITY

### 1. "self: false" vs "other: true" - Dual Mechanisms for Same Purpose
**Status:** ✅ RESOLVED
**Severity:** High (architectural confusion)

Two different flags accomplish the same goal (exclude self from targeting):

**Mechanism A - PassiveEffect.self = false** (auras):
```json
// MerfolkMaster (73), LordGolem (22)
"passiveEffects": [{
  "passive": "grantKeyword",
  "tribe": "golem",
  "self": false,
  "restrictions": ["youControl"]
}]
```

**Mechanism B - Effect.other = true** (effects):
```json
// GolemBlesser (18)
"effects": [{
  "effect": "grantPassive",
  "tribe": "golem",
  "other": true,
  "restrictions": ["youControl"]
}]
```

**Problem:** These serve identical purposes but are checked in different code paths:
- `self: false` checked in `Card.GetAttack()`, `Card.GetDefense()`, `Card.GetKeywords()`
- `other: true` checked in `Qualifier` during target qualification

**Files Affected:**
- Card.cs
- Qualifier.cs
- Multiple card JSONs (LordGolem, MerfolkMaster, GolemBlesser, etc.)

**Discussion Notes:**
- Replaced `self`/`other` booleans with unified `Scope` enum: `SelfOnly`, `OthersOnly`, `All`

**Chosen Solution:**
- Created `Scope` enum and updated all C# classes and JSON card files to use it
- Default scope varies by context (PassiveEffect defaults to All, TriggeredEffect defaults to SelfOnly)

---

### 2. Zone "token" - Not a Real Zone Enum
**Status:** ✅ RESOLVED
**Severity:** High (inconsistent data model)

In `GolemBlesser.json`:
```json
"triggeredEffects": [{
  "trigger": "enteredZone",
  "zone": "token",
  ...
}]
```

The `Zone` enum has: `Deck, Hand, Play, Graveyard, Exile`

There is **no Zone.Token** - tokens are stored in `Player.tokens` separately from `Player.playField`.

**Problem:** This creates a semantic inconsistency - tokens aren't in a "zone", they're in a separate list.

**Files Affected:**
- Zone.cs
- GameMatch.cs (trigger handling)
- GolemBlesser.json (and potentially other cards)

**Discussion Notes:**
- Tokens are "in play", just stored in a different list (player.tokens vs player.playField)
- Zone.Token was unnecessary complexity

**Chosen Solution:**
- Tokens now use `Zone.Play` for their currentZone
- TriggerContext uses `Zone.Play` when tokens are created
- Cards like GolemBlesser use `"zone": "play"` with `"tokenType": "stone"` qualifier to match only stones
- Added `"scope": "all"` to GolemBlesser trigger (TriggeredEffect defaults to SelfOnly)

---

### 3. Triggered Effect Costs vs Card Costs - Split Logic
**Status:** ✅ RESOLVED
**Severity:** Medium-High (code duplication risk)

Two different code paths handle costs:

- **Card-level additionalCosts** → `CheckCardForAdditionalCosts()` (GameMatch.cs:1151)
- **TriggeredEffect additionalCosts** → `SendNextTriggerCostEvent()` (GameMatch.cs:1115)

They handle similar cost types but:
- Different payment flows
- Different "isPaid" tracking
- X-based costs only supported at card level

**Example of TriggeredEffect cost** (Ghastly 97):
```json
"triggeredEffects": [{
  "additionalCosts": [{ "costType": "reveal" }],
  "effects": [...]
}]
```

**Files Affected:**
- GameMatch.cs
- AdditionalCost.cs
- TriggeredEffect.cs

**Discussion Notes:**
- Card-level costs are paid BEFORE putting on stack; trigger costs are paid DURING resolution
- Trigger "costs" aren't really costs - they're effects that happen as part of resolution
- New approach: Use `isCost: true` flag on effects positioned in the effects array

**Chosen Solution:**
- Added `isCost` property to Effect class
- Costs become effects with `isCost: true` positioned where they should execute in the effects array
- ResolveStackObj checks `isCost` effects: if can't be paid → fizzle remaining effects
- Auto-pay logic for reveal/sacrifice with `scope: selfOnly` or single-token sacrifice
- User selection prompted when multiple valid targets (e.g., multiple stones)
- Updated `CostsArePayable` in TriggeredEffect to scan both old-style `additionalCosts` and new-style `isCost` effects
- Migrated 6 cards to new format: Ghastly (97), LootGhost (99), ReconfigureGolem (13), GhastlyTutor (107), GoblinPortal (162), TransparentPlant (206)
- Old `additionalCosts` system kept for backwards compatibility; will be removed after testing

---

## MEDIUM PRIORITY

### 4. CostType.DiscardOrSacrificeMerfolk - Hardcoded Composite Cost
**Status:** RESOLVED
**Severity:** Medium (not extensible)

Special cost type exists only for Eadro (card 66):
```csharp
case CostType.DiscardOrSacrificeMerfolk:
    int merfolkInHand = player.hand.Count(c => c.tribe == Tribe.Merfolk);
    int merfolkInPlay = gameMatch.GetAllCardsControlled(player)...
```

**Problem:** If you wanted another card with "discard or sacrifice X", you'd need to add another enum value and hardcode it.

**Files Affected:**
- GameMatch.cs (AddCostEvent method)
- ActivatedEffect.cs (CostIsAvailable method)
- CostType enum

**Discussion Notes:**
- Reused the existing AlternateCost system from card casting for activated abilities
- Added `Discard` to AltCostType enum
- Added `alternateCosts` property to ActivatedEffect class

**Chosen Solution:**
- Extended the AlternateCost system to work with activated abilities
- ActivatedEffect now supports `alternateCosts: [{ altCostType, tribe, amount }, ...]`
- When multiple cost options are available, player is presented with a choice
- Removed hardcoded `DiscardOrSacrificeMerfolk` enum and all related logic
- Eadro's JSON updated to: `"alternateCosts": [{ "altCostType": "discard", "tribe": "merfolk", "amount": 1 }, { "altCostType": "sacrifice", "tribe": "merfolk", "amount": 1 }]`

---

### 5. amount vs maxTargets vs upTo - Ambiguous Selection Counts
**Status:** ✅ RESOLVED
**Severity:** Medium (confusing)

Different fields control selection counts:

| Field | Used For | Example |
|-------|----------|---------|
| `amount` | Effect quantity AND sometimes selection count | `"effect": "draw", "amount": 2` |
| `maxTargets` | Target selection limit | `"maxTargets": 2` (Brainstorm) |
| `minTargets` | Minimum required selections | `"minTargets": 2` (Brainstorm) |
| `upTo` | Optional maximum | Not commonly used |
| `deckDestinations[].amount` | Cards to select for destination | Opt |

**Problem:** `amount` is overloaded - sometimes it's effect magnitude, sometimes selection count.

**Files Affected:**
- Effect.cs
- Multiple card JSONs

**Discussion Notes:**
- Separated concepts: "targeting" (in-play objects subject to game mechanics like hexproof) vs "selecting" (cards from zones)
- `amount` should only be effect magnitude (damage, cards drawn, tokens created)
- Created unified objects for selection/targeting

**Chosen Solution:**
- Created `Target` class for in-play targeting: `target: { type: "summon", min: 2, max: 2 }`
- Created `Select` class for zone selection: `select: { zone: "hand", max: 2 }` or `select: { zones: ["hand", "graveyard"] }`
- Updated `DeckDestination` with `destination` (replaces `deckDestination`), `select` (replaces `amount`), and `remainder: true`
- Added helper methods to Effect.cs: `GetTargetType()`, `HasTargeting()`, `GetTargetMin()`, `GetTargetMax()`, etc.
- Updated GameMatch/StackObj/Utils/Qualifier to use new helper methods
- System is fully backwards compatible - old fields (`targetType`, `maxTargets`, `minTargets`, `upTo`) still work
- Migrated 17+ cards to new format, tests pass
- Remaining cards can be migrated incrementally

---

### 6. effectsThatHaltEvents List Incomplete
**Status:** ✅ RESOLVED
**Severity:** Medium (maintenance issue)

In `StackObj.cs`:
```csharp
private List<EffectType> effectsThatHaltEvents = new() {
    EffectType.Tutor
};
// Later, inline check:
if (currentEffect.effect == EffectType.LookAtDeck && currentEffect.deckDestinations != null) {
    shouldHalt = true;
}
```

**Problem:** LookAtDeck conditional halting is inline. Other halt conditions (resolveTarget, eachPlayer, playerChoice) are also checked inline rather than centralized.

**Files Affected:**
- StackObj.cs

**Discussion Notes:**
- Identified two categories of halts: pre-resolve (before effect executes) and post-resolve (after effect executes)
- Pre-resolve halts must stay inline because they each call different methods, halt at different indices (i vs i+1), and have unique conditions
- Post-resolve halts are simpler - just a boolean check after the effect runs

**Chosen Solution:**
- Created `ShouldHaltAfterResolve(Effect effect)` helper method for post-resolve halts
- Uses switch expression for clean, centralized logic
- Removed the `effectsThatHaltEvents` list entirely
- Added XML doc comment explaining why pre-resolve halts remain inline

---

## LOW PRIORITY

### 7. AlternateCost Enum Typo
**Status:** ✅ RESOLVED
**Severity:** Low (compatibility cruft)

In `AlternateCost.cs`:
```csharp
public enum AltCostType {
    TributeMultiplier,
    Sacrifice,
    ExileFromHand,
    Tribute,
    TribueMultiplier  // Typo, kept for compatibility
}
```

**Files Affected:**
- AlternateCost.cs
- Any JSON files using the typo

**Discussion Notes:**
- Searched codebase - no JSON files or code actually used the typo

**Chosen Solution:**
- Removed `TribueMultiplier` from enum (no usages found)

---

### 8. Player vs isOpponent for Effect Direction
**Status:** ✅ RESOLVED
**Severity:** Low (two ways to do same thing)

Some effects use `"isOpponent": true`:
```json
// BackSnap (77)
"effect": "sendToZone",
"isOpponent": true
```

Others use `"player": "opponent"`:
```json
"effect": "mill",
"player": "opponent"
```

Both accomplish similar goals but with different resolution paths.

**Files Affected:**
- Effect.cs
- Multiple card JSONs

**Discussion Notes:**
- Clarified the distinction between the two concepts:
  - `isOpponent` = who performs the action (the "source player" or "actor")
  - `player` = who is affected by the action (the "affected player" or "target")
- Example: "Target opponent draws a card" → opponent is sourcePlayer (they draw)
- Example: "Opponent loses 2 life" → opponent is affectedPlayer (they're affected)

**Chosen Solution:**
- Renamed `Effect.isOpponent` (bool) → `Effect.sourcePlayer` (string: "opponent" | null)
- Renamed `Effect.player` → `Effect.affectedPlayer`
- Updated Resolve method with clearer variable names: `resolvedSourcePlayer`, `resolvedAffectedPlayer`
- Updated Clone method and EffectToString for new field names
- Updated GameMatch references
- Migrated 26 JSON files from `isOpponent: true` to `sourcePlayer: "opponent"`
- Migrated 2 JSON files from `player: "opponent"` to `affectedPlayer: "opponent"`
- Removed `isOpponent: false` entries (defaults to null/self)
- Note: TriggeredEffect.player is a separate field (trigger condition filter) and was not renamed

---

### 9. Spell vs Summon Alternate Cost Stages
**Status:** ✅ RESOLVED
**Severity:** Low (documented behavior)

Alternate costs are handled at different stages:
- **Spells**: `CastingStage.SpellAlternateCost` (before target selection)
- **Summons**: `CastingStage.AlternateCostSelection` (after target selection)

This is intentional (BackSnap fix) but could be confusing.

**Files Affected:**
- GameMatch.cs (AttemptToCast method)
- CastingStage.cs

**Discussion Notes:**
- The separate stages existed because spells need alternate cost choice before target selection (for BackSnap)
- Summons don't have target selection during casting anyway, so the order doesn't matter for them
- Having two separate stages for the same concept was unnecessary code duplication

**Chosen Solution:**
- Consolidated both stages into a single `CastingStage.AlternateCost` stage
- Removed `SpellAlternateCost` and `AlternateCostSelection` from enum
- Single stage now handles both spells (ExileFromHand) and summons (ExileFromHand, Sacrifice)
- All alternate cost logic now runs before TargetSelection
- Flow: Choices → AlternateCost → TargetSelection → AdditionalChoices → TributeSelection

---

### 10. self Field on TriggeredEffect vs Effect
**Status:** ✅ RESOLVED (as part of Issue #1)
**Severity:** Low (works correctly but confusing)

Both `TriggeredEffect` and `Effect` have a `self` field with different meanings:
- `TriggeredEffect.self` = "trigger only when THIS card does the thing"
- `Effect.self` = "affect only THIS card"

Example from GolemBlinker (1):
```json
"triggeredEffects": [{
  "trigger": "death",
  "self": false,  // Trigger when ANY golem dies
  "effects": [{
    "effect": "castCard",
    "self": true   // Cast THIS card
  }]
}]
```

**Files Affected:**
- TriggeredEffect.cs
- Effect.cs
- All card JSON files

**Discussion Notes:**
- This was already fixed when Issue #1 introduced the Scope enum

**Chosen Solution:**
- Both classes now use unified `Scope scope` field with enum values: `SelfOnly`, `OthersOnly`, `All`
- TriggeredEffect defaults to `Scope.SelfOnly` (only fires for events involving this card)
- Effect defaults to `Scope.All` (no self/other filter on targeting)
- All JSON files migrated from `"self": true/false` to `"scope": "selfOnly"/"othersOnly"/"all"`
- Example GolemBlinker now uses: `"scope": "othersOnly"` on trigger, `"scope": "selfOnly"` on effect

---

## Change Log

| Date | Issue # | Action |
|------|---------|--------|
| 2025-12-15 | - | Document created |
| 2025-12-15 | #1 | Resolved - Replaced self/other with Scope enum |
| 2025-12-15 | #2 | Resolved - Removed Zone.Token, tokens use Zone.Play |
| 2025-12-15 | #3 | Resolved - Added isCost effect flag, migrated 6 cards |
| 2025-12-18 | #4 | Resolved - Extended AlternateCost system to activated abilities, removed hardcoded merfolk logic |
| 2025-12-20 | #5 | Resolved - Created Target/Select classes for unified targeting/selection |
| 2025-12-21 | #6 | Resolved - Created ShouldHaltAfterResolve helper method, removed effectsThatHaltEvents list |
| 2025-12-21 | #7 | Resolved - Removed TribueMultiplier typo from enum (no usages found) |
| 2025-12-21 | #8 | Resolved - Renamed isOpponent→sourcePlayer, player→affectedPlayer, updated 28 JSON files |
| 2025-12-21 | #9 | Resolved - Consolidated SpellAlternateCost and AlternateCostSelection into single AlternateCost stage |
| 2025-12-21 | #10 | Resolved - Already fixed as part of Issue #1 (Scope enum replaced self field) |

