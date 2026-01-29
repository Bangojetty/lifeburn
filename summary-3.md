# Session Summary - December 24, 2024

## Overview
This session focused on fixing several card mechanics and updating card descriptions for consistency.

## Bug Fixes

### 1. Fable - Remainder Cards Not Going to Graveyard
**Problem:** When selecting cards for deck top in Fable's sendToZone effect, the remaining cards weren't being sent to graveyard.

**Root Cause:** In `SendCardsToDestinations`, the switch statement used `.deckDestination` (legacy field) directly instead of `.GetDestination()`. When JSON uses the new `"destination"` field, the legacy field is null.

**Fix:** Changed `switch (lookedAtSelectionDestinations[i].deckDestination)` to `switch (currentDestination.GetDestination())` in GameMatch.cs:3475.

---

### 2. Vanquish - No Target Selection
**Problem:** Vanquish didn't prompt for target selection before going on the stack.

**Root Cause:** Missing `targetType` on the destroy effect. Only had `cardType: "summon"` which doesn't enable targeting.

**Fix:** Changed `cardType: "summon"` to `targetType: "summon"` in 126_Vanquish.json.

---

### 3. It's Alive - Multiple Issues
**Problems:**
- Life loss was setting life to 0 instead of losing half
- "If it's a ghost" condition wasn't working
- Couldn't target cards in graveyard

**Root Causes:**
- Typo: `amountModifer` instead of `amountModifier`
- `isTribe` condition wasn't implemented in Condition.cs
- No `CardInGraveyard` TargetType existed

**Fixes:**
- Fixed typo: `amountModifer` → `amountModifier`
- Changed condition from `isTribe` to `rootTargetTribe` (which is implemented)
- Added new `CardInGraveyard` TargetType:
  - Added to TargetType.cs enum
  - Added handling in `GetPossibleTargets()` to include graveyard cards
  - Added handling in `QualifyTarget()` to validate graveyard targets

---

### 4. Dark Shade - Ability Activatable from Graveyard
**Problem:** Dark Shade's activated ability could be activated from the graveyard when it shouldn't be.

**Root Cause:** In Utils.CheckPlayability, if an activated effect had no conditions, it would still be activatable from graveyard.

**Fix:** Added check in Utils.cs: if card is in graveyard and activated effect has no conditions, skip it. Cards in graveyard now require explicit `inZone: graveyard` condition (like Ghostly Looter has).

---

### 5. Shadow Lord - Graveyard Passive Not Working
**Problem:** Shadow Lord's passive effects (giving shadow summons +1/+0 and Haunt while in graveyard) weren't being applied.

**Root Cause:** `CheckForPassives()` only iterated over `allCardsInPlay` and hand cards, never graveyard cards.

**Fix:** Added `CheckForPassivesInGraveyard()` method in GameMatch.cs that:
- Iterates over graveyard cards
- Finds passives with explicit `inZone: graveyard` conditions
- Applies those passives to qualifying targets

---

## Card Description Updates

Updated 12 shadow cards to use "shadow summon" instead of "ghost" for consistency with how the mechanics actually work (targeting by tribe, not token type):

| Card | Change |
|------|--------|
| Shadow Lord | "ghosts get" → "shadow summons get" |
| Ghastly | "discard any number ghosts" → "discard any number shadow summons" |
| Ghost Gathering | "summon any amount of ghosts" → "summon any amount of shadow summons" |
| Selfless Shadow | "tributed to a ghost" → "tributed to a shadow summon" |
| Ghastly Tutor | "tutor a ghost" → "tutor a shadow summon" |
| Shade Crawler | "Whenever a ghost dies" → "Whenever a shadow summon dies" |
| Ghost Deceiver | "Whenever a ghost enters" → "Whenever a shadow summon enters" |
| Dark Blessing | "Tutor a ghost" → "Tutor a shadow summon" |
| It's Alive | "If it's a ghost" → "If it's a shadow summon" |
| Haunt God | "tribute 3 ghosts" → "tribute 3 shadow summons" (kept "ghost token" for ETB) |
| Crawl Back | "target ghost with power 3" → "target shadow summon with cost 3" |
| Spectralize | "If it's a ghost" → "If it's a shadow summon" |

---

## Files Modified

### Server Code
- `LifeServer/Server/GameMatch.cs` - Added graveyard passive checking, fixed deckDestination switch
- `LifeServer/Server/Utils.cs` - Added graveyard activation restriction
- `LifeServer/Server/CardProperties/TargetType.cs` - Added CardInGraveyard enum value

### Card Data
- `Data/Cards/097_Ghastly.json`
- `Data/Cards/102_GhostGathering.json`
- `Data/Cards/105_SelflessShadow.json`
- `Data/Cards/107_GhastlyTutor.json`
- `Data/Cards/108_ShadeCrawler.json`
- `Data/Cards/111_GhostDeceiver.json`
- `Data/Cards/112_DarkBlessing.json`
- `Data/Cards/126_Vanquish.json`
- `Data/Cards/127_ShadowLord.json`
- `Data/Cards/130_ItsAlive.json`
- `Data/Cards/134_HauntGod.json`
- `Data/Cards/137_CrawlBack.json`
- `Data/Cards/235_Spectralize.json`

---

## Testing Status
- Fable: Fixed
- Vanquish: Fixed
- It's Alive: Fixed
- Dark Shade: Fixed (no longer activatable from graveyard)
- Shadow Lord: Awaiting user test confirmation
