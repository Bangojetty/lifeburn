---
argument-hint: [card-name]
description: Implement and verify a card works correctly
---

# Card Implementation Task: $ARGUMENTS

Please read the description of the card, read its JSON data, and analyze similar cards that use similar data in their JSONs to verify that this card is implemented correctly and that it does what its description says it does (or should do). Please make sure that all messages for costs, descriptions, and choices are correctly aligned with what the card does and says in its description.

Implement the proper changes in order for the card to function fully, but do not make any large refactors to the current code without presenting your plan. Thanks.

## Steps

1. Find and read `LifeServer/Data/Cards/*_$ARGUMENTS.json`
2. Read the card's description
3. Find similar cards that use similar mechanics/JSON structure
4. Verify implementation matches description
5. Check all effect messages, cost messages, and choice prompts
6. Test the card with `dotnet test LifeServer/`

## Summary

After verification, provide:
- **What the card does** (from description)
- **What's implemented** (from JSON analysis)
- **Issues found** (if any)
- **Changes needed** (if any, get approval first)
