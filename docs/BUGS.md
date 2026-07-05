# Known Engine/Card Bugs

Tracker for engine bugs found outside of (or ahead of) the Phase 3 card re-test.
Per-card test results live in `CARD_TEST_TRACKER.md`.

| # | Found | Card / Area | Description | Status |
|---|-------|-------------|-------------|--------|
| 1 | 2026-07-05 | PlantSnap (226) | Description says "exile a card from **a** graveyard", but `TargetType.CardInGraveyard` only offers/qualifies cards in the **casting player's** graveyard (`GameMatch.GetPossibleTargets` / `QualifyTarget`). Either the card should use a target type that spans both graveyards, or the description should say "your graveyard". The dead `"player": "any"` key (removed 2026-07-05) suggests the intent was either graveyard. | Open |
| 2 | 2026-07-05 | HauntGod (134), MasterTree (229), GeistOfDroolingTears (257) | Card-level `keywordAmounts` was never read by the engine, so innate Haunt X played as Haunt 1. Fixed by wiring `keywordAmounts` through CardDto/Card into `GetHauntAmount()`. Verify haunt amounts during Phase 3 re-test. | Fixed |
