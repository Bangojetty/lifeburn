# Session Summary: Stone Toss & X-Based Additional Costs

## Cards Tested & Passed
- **Dig Up** (ID 30)
- **Stone Toss** (ID 32)
- **Master Golem** (ID 34)

---

## Key Feature: Variable X Sacrifice Selection

Stone Toss introduced a new pattern where X is determined BY the sacrifice selection, not pre-selected:

**Flow:**
1. Cast Stone Toss (fixed cost: 5 life)
2. Variable sacrifice prompt appears (select 0 to max stones)
3. Player selects stones and confirms (action button always enabled)
4. X = number of stones sacrificed
5. Life cost automatically paid (X life)
6. Target selection for damage
7. Spell resolves: deal 2X damage, create 2 stones

This differs from true X-cost spells where X is selected first via AmountSelection.

---

## Server-Side Changes

### AdditionalCost.cs
- Added `amountBasedOn` field with `[JsonConverter(typeof(StringEnumConverter))]`
- Added `GetAmount(Card? sourceCard)` method that resolves X-based amounts
- `CostIsAvailable` returns true for X-based costs (X can be 0)

### Card.cs
- **`HasXCost()`** - Only checks `costModifiers` for X (affects displayed cost)
- **`NeedsXSelection()`** - Only for true X-cost spells, not X-based additional costs
- **`GetCost()`** - Fixed to only use X as cost if `HasXCost()` is true:
  ```csharp
  int finalCost = HasXCost() && x != null ? x.Value : cost;
  ```
  This prevents cards like Stone Toss from showing X as their cost.

### GameEvent.cs
- Added `variableAmount` parameter to `CreateCostEvent`:
  ```csharp
  public static GameEvent CreateCostEvent(CostType costType, int amount,
      List<int>? selectableUids = null, List<string>? eventMessages = null,
      bool variableAmount = false)
  ```
- `universalBool` carries the `variableAmount` flag to the client

### GameMatch.cs

#### New Method: `AddVariableCostEvent`
Sends a cost event allowing selection from 0 to max:
```csharp
private void AddVariableCostEvent(Player attemptingPlayer, AdditionalCost aCost, Card sourceCard)
```

#### Updated: `CheckCardForAdditionalCosts`
Detects X-determining sacrifice costs and uses variable selection:
```csharp
if (aCost.amountBasedOn == AmountBasedOn.X && focusCard.x == null) {
    if (aCost.costType == CostType.Sacrifice) {
        AddVariableCostEvent(player, aCost, focusCard);
        // ...
    }
}
```

#### Updated: `HandleCostSelection`
For X-determining sacrifices:
1. Sets `cardBeingCast.x = selectedCards.Count`
2. Pays the sacrifice cost
3. Re-calls `CheckCardForAdditionalCosts` to process remaining costs (e.g., life cost)

#### Fixed: `Destroy` method for tokens
Changed from `GetPlayerByTurn(true)` to `controller` so `isOpponent` flag is correct:
```csharp
AddEventForBothPlayers(controller, gEvent);  // Was: GetPlayerByTurn(true)
```

---

## Client-Side Changes

### GameManager.cs
- Added `variableSelection` field to track variable selection mode
- Updated `EnableCostCardSelection`:
  - Sets `variableSelection = gEvent.universalBool`
  - Enables action button immediately for variable selection
- Added reset of `variableSelection` in cleanup code

### SelectableTarget.cs
- **`Deselect()`**:
  - Added missing `dRef.highlightSelected.SetActive(false)`
  - Only disables action button if NOT variable selection
- **`DeselectMultiple()`**: Only disables action button if NOT variable selection
- **`CheckMaxAmount()`**: For variable selection, button stays enabled and only disables further selection at max

---

## Important Integration Notes

### X-Cost vs X-Based Additional Cost
| Aspect | True X-Cost Spell | X-Based Additional Cost |
|--------|------------------|------------------------|
| Cost display | Shows "X" | Shows fixed cost (e.g., 5) |
| X selection | AmountSelection event first | Determined by sacrifice count |
| `HasXCost()` | Returns true | Returns false |
| `NeedsXSelection()` | Returns true | Returns false |
| `costModifiers` | Contains X modifier | None |
| `additionalCosts` | May or may not have | Has `amountBasedOn: "x"` |

### JSON Structure for X-Based Additional Costs
```json
{
  "additionalCosts": [
    {
      "costType": "sacrifice",
      "tokenType": "stone",
      "amountBasedOn": "x"
    },
    {
      "costType": "life",
      "amountBasedOn": "x"
    }
  ]
}
```

### Event Flow for Variable Selection
1. Server sends `EventType.Cost` with `universalBool = true` (variableAmount)
2. Client enables action button immediately
3. Player can select 0 to max cards
4. Player confirms selection
5. Server receives selection, sets `card.x`, processes remaining costs

---

## Bug Fixes

1. **Token destroy looking in wrong dictionary**: Fixed by using `controller` instead of `GetPlayerByTurn(true)` in `AddEventForBothPlayers`

2. **Stone Toss cost showing as X**: Fixed `GetCost()` to only use X as cost when `HasXCost()` is true

3. **Missing highlight toggle on deselect**: Added `dRef.highlightSelected.SetActive(false)` in `Deselect()`

---

## Files Modified
- `LifeServer/Server/Card.cs`
- `LifeServer/Server/CardProperties/AdditionalCost.cs`
- `LifeServer/Server/GameEvent.cs`
- `LifeServer/Server/GameMatch.cs`
- `Project_Life/Assets/Scripts/GameManager.cs`
- `Project_Life/Assets/Scripts/InGame/SelectableTarget.cs`
