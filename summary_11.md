# Summary of Ground Tactics Implementation Changes

## Server-Side Files Modified:

### 1. CastRestriction.cs
- Added `OpponentsTurnBeforeCombat` to enum

### 2. Passive.cs
- Added `GroundTactics` to enum

### 3. EffectType.cs
- Added `GroundTactics` to enum

### 4. PassiveEffect.cs
- Added `public int? attackControllerPlayerId;` field
- Updated `Clone()` method to copy this field

### 5. Utils.cs (lines 221-225)
- Added cast restriction validation for `OpponentsTurnBeforeCombat`:
```csharp
case CastRestriction.OpponentsTurnBeforeCombat:
    // Must be opponent's turn AND before combat phase (Draw or Main)
    if (gameMatch.turnPlayerId == player.playerId) return false;
    if (gameMatch.currentPhase != Phase.Draw && gameMatch.currentPhase != Phase.Main) return false;
    break;
```

### 6. EffectTypeConverter.cs
- Added case `"groundTactics"` → `GroundTacticsEffect`

### 7. Effects/GroundTacticsEffect.cs - NEW FILE
```csharp
using Server.CardProperties;
namespace Server.Effects;

public class GroundTacticsEffect : Effect {
    public GroundTacticsEffect(EffectType effect) : base(effect) {
    }
}
```

### 8. Effect.cs (lines 733-742)
- Added `GroundTactics` resolution logic:
```csharp
case EffectType.GroundTactics:
    Player groundTacticsOpponent = gameMatch.GetOpponent(effectOwner);
    PassiveEffect groundTacticsPassive = new PassiveEffect(Passive.GroundTactics);
    groundTacticsPassive.thisTurn = true;
    groundTacticsPassive.attackControllerPlayerId = effectOwner.playerId;
    groundTacticsOpponent.playerPassives.Add(groundTacticsPassive);
    break;
```

### 9. GameMatch.cs
- Added `groundTacticsControllerId` field (line ~22)
- Added `GetPlayerById()` helper method
- Modified `GoToNextPhase()` for combat phase entry with Ground Tactics check
- Modified `PassPrio()` to block pass until all attackers assigned
- Modified `SubmitAttack()` to use turn player as actual attacker
- Added reset `groundTacticsControllerId = null` in `PassTurn()`

### 10. LifeController.cs
- Updated GetAttackables endpoint to allow Ground Tactics controller to request attackables for turn player's cards

---

## Client-Side (GameManager.cs):

### Added state variables (lines 117-119):
```csharp
private bool isGroundTacticsMode;
private int groundTacticsAttackerPlayerId;
```

### Modified AttackCapable event handler (lines 560-576):
- Check `gEvent.universalBool` flag to detect Ground Tactics mode
- Store state variables appropriately

### Modified DeselectAttackCapable() and AssignAttack():
- Button interactability logic for Ground Tactics mode (can't pass until all attackers assigned)

### Modified ResetAttackReferences():
- Reset `isGroundTacticsMode = false`

---

## Card JSON:

### 239_GroundTactics.json
```json
{
  "id": 239,
  "name": "Ground Tactics",
  "cost": 5,
  "type": "spell",
  "tribe": "golem",
  "rarity": "common",
  "description": "Cast only on opponent's turn before combat. All opponent's summons must attack this turn. You choose their attack targets.",
  "castRestrictions": ["opponentsTurnBeforeCombat"],
  "stackEffects": [
    {
      "effect": "groundTactics"
    }
  ]
}
```

---

## Pending Fix (NOT YET APPLIED):

In **GameManager.cs**, need to change two lines to fix the Y-position direction when controlling opponent's attackers:

### Line 1829 - In AssignAttack():
**Current:**
```csharp
DisplayAttack((attackingCardDisplay.card.uid, attackable.dynamicReferencer.uid), false);
```
**Change to:**
```csharp
DisplayAttack((attackingCardDisplay.card.uid, attackable.dynamicReferencer.uid), isGroundTacticsMode);
```

### Line 1858 - In UnAssignAttack():
**Current:**
```csharp
UnDisplayAttack(attackCapable.cardDisplay.card.uid);
```
**Change to:**
```csharp
UnDisplayAttack(attackCapable.cardDisplay.card.uid, isGroundTacticsMode);
```

This makes the attacker card Y-position move **down** (like opponent attacks) instead of **up** when in Ground Tactics mode, since you're controlling the opponent's attackers which are on the opposite side of the field.
