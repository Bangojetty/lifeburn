# Session 7 Summary - 2025-12-27

## Cards Tested
Marked 10 cards as tested in CARD_TEST_TRACKER.md:
- Greed
- ForkBolt
- ChieftanGob
- HeatRay
- Smite
- Gamble
- GoblinTown
- HeatWave
- RunAmok
- ExplosiveVegetation

**Stats Update:** 111 → 101 untested, 170 → 180 passed

## Major Fixes

### 1. GoblinTown Token Description Fix
**Issue:** Token description showed full goblin count instead of halved amount
**Fix:** `Effect.cs:1307-1323` - Added amountModifier application to CreateToken display amount
- Now applies modifiers like "/2down" to the displayed token count

### 2. RunAmok CantTribute Implementation
**Issue:** CantTribute effect wasn't implemented
**Files Changed:**
- `Passive.cs:30` - Added `CantTribute` to Passive enum
- `Effect.cs:679-684` - Implemented CantTribute effect to add player passive
- `Utils.cs:231-234` - Modified GetTributeValue to return 0 when CantTribute is active
**Result:** Players can't tribute after RunAmok, but 0-cost summons still work

### 3. Greed Discard Prompt Fixes
**Issue:** Greed wasn't prompting for discard at all
**Root Cause:** Control condition had default minAmount=1 even with maxAmount:0
**Fixes:**
- `Condition.cs:43-52` - Fixed Control condition to allow requiredAmount=0 when maxAmount is set
- `StackObj.cs:110,125,154` - Added ConditionsAreMet checks to discard selection requests
**Result:** Greed now properly prompts to discard 2 cards (no goblins) or 1 card (with goblins)

### 4. Explosive Vegetation Tribute Multiplier
**Issue:** Tribute multipliers didn't work for summons on field (only tokens)
**Fixes:**
- `ExplosiveVegetation.json:19` - Fixed JSON typo: "costType" → "altCostType"
- `Utils.cs:241-276` - Updated GetTributeValue to check both tokens and summons
- `GameMatch.cs:2528-2564` - Updated tribute cost event to include multiplier values for summons
**Result:** Treefolk summons count as 4 tribute, herb summons count as 2

### 5. Herb Token Activation Fix
**Issue:** Herb sacrifice ability prompted for target selection
**Fix:** `Herb.json:9` - Changed "self": true → "scope": "selfOnly"
**Result:** Activating herb ability now auto-sacrifices without prompt

### 6. Explosive Vegetation Token Conversion
**Issue:** Converting all herbs including existing ones, not just newly created
**Fixes:**
- `Effect.cs:820` - Store affectedUids before running additionalEffects
- `Effect.cs:997-1094` - Modified CreateToken to return created token UIDs
- `Effect.cs:456` - Capture CreateToken return value in affectedUids
- `Effect.cs:1095-1133` - ModifyType now filters by rootEffect.affectedUids
- `ExplosiveVegetation.json:30-42` - Removed attack/defense from CreateToken, added to ModifyType
**Result:** Only the 4 newly created herbs get converted to 1/1 treefolk summons, existing herbs unaffected

## Technical Improvements

### Token Type System
- Clarified that tokens can have `type: Summon` and still retain `tokenType` field
- This enables dual-type mechanics (e.g., treefolk summons that are also herbs)

### Condition System Enhancement
- Fixed Control condition to properly handle "control 0" scenarios
- Previously always required at least 1, breaking negative conditions

### Effect Propagation
- Implemented affectedUids propagation from parent effects to additionalEffects
- Enables targeted filtering in sequential effect chains

## Files Modified
- `Data/Cards/174_ExplosiveVegetation.json`
- `Data/Tokens/Herb.json`
- `LifeServer/Server/CardProperties/Condition.cs`
- `LifeServer/Server/CardProperties/Effect.cs`
- `LifeServer/Server/CardProperties/Passive.cs`
- `LifeServer/Server/GameMatch.cs`
- `LifeServer/Server/StackObj.cs`
- `LifeServer/Server/Utils.cs`
- `CARD_TEST_TRACKER.md`

## Next Session
Next 10 cards to test:
- Fireblast
- Obliterate
- Wildfire
- Channel
- PlantOfSolitude
- EternalTreefolk
- JeelaiPlant
- CipplingVines
- PlantOfHerbs
- NaturesBlessing
