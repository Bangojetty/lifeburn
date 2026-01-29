# Session 5 Summary

## Cards Tested & Marked Passed
- GoblinDuelist (147)
- BlitzGoblin (144)
- GobRunner (149)
- TransparentGoblin (146)
- GobRocket (145)
- Maglubiyet'sBlessing (148)
- GoblinCrew (151)
- FuryGoblin (150)
- UndeadGoblin (153)
- LooterGob (152)

**Test Tracker Updated:** 156 passed, 125 untested

---

## Bug Fixes

### Looter Gob Reveal Selection Infinite Loop
- **Issue:** After selecting a card to reveal, the selection prompt appeared again infinitely
- **Cause:** `NeedsCostSelection()` didn't check if targets were already selected
- **Fix:** Added `if (targetUids.Count > 0) return false;` at start of `NeedsCostSelection()` in Effect.cs

### Dive Immunity Implementation
- **Added:** `IsImmuneToKeyword(Keyword)` method to Card.cs
- **Added:** `AllDiveImmuneSummonsAreBeingAttacked()` helper in GameMatch.cs
- **Modified:** `GetAttackableUids()` - Dive summons must now attack dive-immune summons before going direct
- **Result:** Undead Gob properly blocks Dive attackers

### Trample Keyword Implementation
- **Added:** Trample damage calculation in `ResolveAttacks()` (GameMatch.cs:3006-3027)
- **Logic:** Calculates excess damage before dealing combat damage, deals excess to defender's controller
- **Immunity:** Respects `ImmuneToKeyword: trample` passive (Undead Gob blocks trample carryover)

### Goblin Portal Tribute Trigger Not Firing
- **Issue:** Tribute trigger with `keyword: "blitz"` filter wasn't working
- **Cause:** TriggeredEffect didn't have a `keyword` property
- **Fix:**
  - Added `keyword` property to TriggeredEffect.cs
  - Added keyword check in `QualifyTrigger()` for tribute triggers

### Fire Master Gob Issues
1. **Trigger not firing on spell cast**
   - Added `scope: "othersOnly"` to trigger on other goblin spells

2. **Spellburnt condition not working**
   - Added `wasSpellburnt` field to TriggerContext
   - Captured spellburnt state before it gets reset in CastCard
   - Implemented `TriggerWasSpellburnt` and `TriggerNotSpellburnt` conditions in Condition.cs

3. **Damage not dealing to opponent**
   - Added handling for `targetType: opponent` with `all: true` in DealDamage effect

---

## Card JSON Fixes

### FiringGoblin (157)
- Fixed `optoinal` typo → `optional`
- Moved optional to triggeredEffect level
- Added `scope: "selfOnly"`, `optionMessage`
- Added `targetType: "cardInHand"`, `isCost: true` to reveal
- Changed dealDamage to sibling effect with `resolveTarget: true`

### Shot (155)
- Changed from two separate effects (causing double target selection) to single effect
- Uses `amountModifier: "+1"` with `modifierConditions` for goblin control bonus

### GoblinRitualist (154)
- Added `optional: true`, `optionMessage`, `scope: "selfOnly"`
- Added `cardType: "spell"` filter
- Added `select: { "amount": 1 }` for selection

### BabyGobs (159) & GoblinSquadron (158)
- Added `description: "Can only be tributed to Goblins."` to tokenPassive

### FireMasterGob (156)
- Added `scope: "othersOnly"` to cast trigger

---

## Code Cleanup
- Removed QualifyCard, QualifyTrigger, and GetTriggersInCard debug logs from GameMatch.cs

---

## Files Modified
- LifeServer/Server/Card.cs - Added `IsImmuneToKeyword()` method
- LifeServer/Server/CardProperties/Condition.cs - Added TriggerWasSpellburnt/TriggerNotSpellburnt
- LifeServer/Server/CardProperties/Effect.cs - Fixed NeedsCostSelection, DealDamage opponent handling
- LifeServer/Server/CardProperties/TriggeredEffect.cs - Added `keyword` property
- LifeServer/Server/GameMatch.cs - Dive immunity, Trample, spellburnt tracking, debug cleanup
- LifeServer/Server/TriggerContext.cs - Added `wasSpellburnt` field
- Data/Cards/153_UndeadGoblin.json
- Data/Cards/154_GoblinRitualist.json
- Data/Cards/155_Shot.json
- Data/Cards/156_FireMasterGob.json
- Data/Cards/157_FiringGoblin.json
- Data/Cards/158_GoblinSquadron.json
- Data/Cards/159_BabyGobs.json
- Data/Cards/162_GoblinPortal.json
- CARD_TEST_TRACKER.md
