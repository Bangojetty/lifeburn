# Session Summary - December 22, 2025

## Overview
Continued from previous session. Fixed multiple issues with GhostReceiver card (109) and the Target/Select system implemented in Issue #5.

## Issues Fixed

### 1. Optional Effect Bypassing Selection (GameMatch.cs)
**Problem:** When an optional effect was accepted, `HandleChoice` called `Resolve()` directly, bypassing the `resolveTarget && HasSelection()` checks in StackObj. This meant selection prompts never appeared.

**Fix (lines 1014-1026):** When user accepts optional effect:
- Mark effect as no longer optional (`optional = false`)
- Decrement `unresolvedEffectIndex` to go back to this effect
- Resume StackObj resolution, which now properly triggers selection checks

### 2. Mill Effect Not Supporting `upTo` (Multiple files)
**Problem:** Mill effects with `"upTo": 3` didn't prompt for amount selection - they either crashed or used wrong values.

**Fixes:**
- **GameMatch.cs:** Added `effectWaitingForAmount` field and `RequestEffectAmount()`/`SetEffectAmount()` methods
- **GameMatch.cs:** Modified `SetAmount()` to check for `effectWaitingForAmount` first
- **StackObj.cs (lines 140-152):** Added check for mill/draw with `upTo` that requests amount selection before resolving
- **Effect.cs (Mill method):** Added fallback `amount ?? upTo ?? 0` and debug logging

### 3. Select Object Filters Not Applied (Qualifier.cs)
**Problem:** The `Qualifier` constructor read `tribe` and `cardType` from the effect directly, but the new Select system stores them in `effect.select.tribe` and `effect.select.cardType`. This caused selection to allow non-qualifying cards.

**Fix (lines 21-23):**
```csharp
tribe = e.tribe ?? e.select?.tribe;
cardType = e.cardType ?? e.select?.cardType;
```

### 4. Animation Ordering Issue (StackObj.cs)
**Problem:** SendToZone and mill amount selection events were batched together, causing animations to overlap instead of playing sequentially.

**Fix (lines 218-219):** Added SendToZone to `ShouldHaltAfterResolve()`:
```csharp
EffectType.SendToZone => effect.resolveTarget && effect.targetUids.Count > 0,
```
This halts after SendToZone resolves when it used resolve-time selection, allowing the client to process the animation before the next prompt appears.

## Files Modified
- `LifeServer/Server/GameMatch.cs` - Optional effect flow, amount selection methods
- `LifeServer/Server/StackObj.cs` - Mill upTo check, SendToZone halt after resolve
- `LifeServer/Server/Qualifier.cs` - Read tribe/cardType from select object
- `LifeServer/Server/CardProperties/Effect.cs` - Mill method fallback and debug logging
- `Data/Cards/109_GhostReceiver.json` - Updated to use proper `select` object format

## GhostReceiver Final JSON
```json
{
  "id": 109,
  "name": "Ghost Receiver",
  "description": "When Ghost Receiver attacks, you may put a shadow summon from your graveyard on top of your deck. Mill up to 3.",
  "triggeredEffects": [
    {
      "trigger": "attack",
      "scope": "selfOnly",
      "effects": [
        {
          "effect": "sendToZone",
          "optional": true,
          "optionMessage": "Put a shadow summon from your graveyard on top of your deck?",
          "resolveTarget": true,
          "select": {
            "zone": "graveyard",
            "tribe": "shadow",
            "cardType": "summon",
            "max": 1
          },
          "destination": "deck",
          "deckDestination": "top"
        },
        {
          "effect": "mill",
          "upTo": 3
        }
      ]
    }
  ]
}
```

## Expected Flow After Fixes
1. Ghost Receiver attacks → trigger fires
2. Optional prompt: "Put a shadow summon from your graveyard on top of your deck?"
3. If accepted → Selection prompt showing only shadow summons in graveyard
4. User selects one → Card moves to top of deck
5. **Server halts, sends events to client**
6. Client processes animation, acknowledges
7. Mill amount selection prompt (0-3) appears
8. User selects amount → Cards are milled

## Where We Left Off
- All fixes implemented and code compiles
- Server needs restart to pick up changes
- **Testing GhostReceiver** - Need to verify:
  1. Only shadow summons appear in selection
  2. Animation plays before mill prompt appears
  3. Mill amount selection works correctly
  4. Correct number of cards are milled

## Next Steps
- Complete GhostReceiver testing
- Continue testing remaining shadow cards from CARD_TEST_TRACKER.md:
  - GraveDigger (98)
  - SelflessShadow (105)
  - ShadowOfTheGrave (106)
  - GhastlyTutor (107)
  - ShadeCrawler (108)
  - ShadeRunner (110)
  - GhostDeceiver (111)
  - DarkBlessing (112)
  - ShadeOfReturn (113)

## Key Learnings
- The Target/Select system (Issue #5) requires `select` object for zone selection, not legacy `targetZone` field
- `Qualifier` must be updated to read from both effect-level fields AND select object fields
- Optional effects that are accepted need to re-run through StackObj checks, not call Resolve() directly
- Effects that produce visual changes and are followed by user-input effects should halt to prevent event batching
