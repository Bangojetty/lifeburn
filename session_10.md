# Session 10 Summary - 2025-12-29

## Overview
This session focused on fixing autopass behavior when items go on the stack, and resolving a bug where Tree God's enter-play trigger was firing twice.

## Issues Fixed

### 1. Autopass Not Pausing When Stack Items Added
**Problem:** When a trigger or spell went on the stack, autopass should pause for both players to let them respond, but it wasn't pausing.

**Root Cause:** The `autopassPausedForStack` flag was being cleared in `PassPrio()` whenever a player passed, but it should only be cleared when:
- Player clicks a passToPhase button (to resume autopass)
- The stack empties

**Fix:**
- `LifeController.cs`: Only clear `autopassPausedForStack` when `passToPhase.HasValue` is true (player clicked a phase button, not manual pass)
- `GameMatch.cs`: Removed the clearing logic from `PassPrio()` for manual passes

### 2. Tree God Triggering Twice When Entering Play
**Problem:** Tree God's "when this enters play, choose one" trigger was firing twice - once for Tree God entering and once for any other card entering play.

**Root Cause:** Tree God's first triggered effect had no `scope` specified, defaulting to `Scope.All`, which meant it fired for ANY card entering play.

**Fix:**
- `Data/Cards/223_TreeGod.json`: Added `"scope": "selfOnly"` to the first triggered effect

### 3. Tree God Triggering Twice When Returning from Exile
**Problem:** After ExileAndReturn resolved, Tree God's enter-play trigger fired twice.

**Root Cause:** In the `ExileAndReturn` effect:
1. `SendToZone(exile)` added Tree God to `player.exile` list
2. `Summon()` -> `AddToPlay()` added Tree God to `player.playField` list but didn't remove from exile
3. `GetTriggers()` iterated through BOTH lists, finding Tree God twice

**Fix:**
- `Effect.cs`: Added `controller.exile.Remove(sourceCard)` before calling `Summon()` in ExileAndReturn

### 4. Trigger Ordering UI Using Stale Indices
**Problem:** When multiple trigger ordering prompts occurred in the same game, the second prompt showed indices like 3,4 instead of 0,1 because the ordering list wasn't reset.

**Root Cause:** `DisplayOrderingOptions()` didn't clear `finalOrderList` before displaying new ordering options.

**Fixes:**
- `GameManager.cs` (client): Added `finalOrderList.Clear()` and `ClearOrderingPanel()` at the start of `DisplayOrderingOptions()`
- `GameMatch.cs` (server): Added validation in `AddOrderedTriggersToStack()` to detect invalid indices and fallback to default order

## Files Modified

### Server (LifeServer)
- `Server/Controllers/LifeController.cs` - Autopass pause logic for passToPhase buttons
- `Server/GameMatch.cs` - Removed manual pass clearing, added debug logging, added ordering validation
- `Server/CardProperties/Effect.cs` - Fixed ExileAndReturn to remove from exile before summoning

### Client (Project_Life)
- `Assets/Scripts/GameManager.cs` - Clear ordering state before new ordering event

### Data
- `Data/Cards/223_TreeGod.json` - Added `scope: "selfOnly"` to first trigger

## Debug Logging Added
Added extensive logging to trace autopass and trigger detection:
- `[AddStackObjToStack]` - Logs when items are added to stack and autopass pause state
- `[ShouldAutoSkipPhases]` - Logs autopass decision making
- `[PassPrioToPlayer]` - Logs priority passing with autopass state
- `[CheckForTriggersAndPassives]` - Logs trigger contexts and detected triggers
- `[AddOrderedTriggersToStack]` - Logs received ordering indices

## Key Learnings
1. When a card moves between zones, it must be removed from the old zone's list before being added to the new zone's list, otherwise trigger detection will find it twice.
2. EnteredZone triggers with no scope default to `Scope.All`, meaning they fire for ANY card entering that zone. Use `scope: "selfOnly"` for "when this enters" triggers.
3. Client-side UI state must be explicitly cleared between repeated prompts of the same type.
