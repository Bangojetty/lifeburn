# Known Engine/Card Bugs

Tracker for engine bugs found outside of (or ahead of) the Phase 3 card re-test.
Per-card test results live in `CARD_TEST_TRACKER.md`.

| # | Found | Card / Area | Description | Status |
|---|-------|-------------|-------------|--------|
| 1 | 2026-07-05 | PlantSnap (226) | Description says "exile a card from **a** graveyard", but `TargetType.CardInGraveyard` only offers/qualifies cards in the **casting player's** graveyard (`GameMatch.GetPossibleTargets` / `QualifyTarget`). Either the card should use a target type that spans both graveyards, or the description should say "your graveyard". The dead `"player": "any"` key (removed 2026-07-05) suggests the intent was either graveyard. | Open |
| 2 | 2026-07-05 | HauntGod (134), MasterTree (229), GeistOfDroolingTears (257) | Card-level `keywordAmounts` was never read by the engine, so innate Haunt X played as Haunt 1. Fixed by wiring `keywordAmounts` through CardDto/Card into `GetHauntAmount()`. Verify haunt amounts during Phase 3 re-test. | Fixed |
| 3 | 2026-07-05 | Test-bot matches | `MatchCleanupService` disconnect detection tracked the bot (-999) like a human; bots never poll, so every bot match was ended as "opponent disconnected" after 20s (client stuck on "waiting for opponent"). Bots are no longer activity-tracked (`Matches.SetMatchData`). | Fixed |
| 4 | 2026-07-06 | FoundryGolem (26) + hand-passive engine | All cards in hand permanently dropped to 0 cost once the player hit 4 stones (e.g. after Digital Stone). Two causes: (a) FoundryGolem's `modifyCost` passive had no `scope`, defaulting to `all`, so the hand-aura pass sprayed cost-0 onto every card; (b) `ApplyPassiveToHandCards` lacked the skip-own-innate-passive guard the in-play loop has, so cards received conditions-stripped clones of their own passives that stuck forever after conditions lapsed. Fixed both + regression test. | Fixed |
| 5 | 2026-07-07 | MerfolkElite (251) + stat engine | SERVER CRASH (stack overflow, uncatchable SIGABRT, killed all matches). Its `oneOneInPlay` passive condition reads every in-play card's `GetAttack()`/`GetDefense()`, including its own - and computing its own attack re-evaluates that condition, recursing forever. Fixed with a re-entrancy guard in GetAttack/GetDefense (returns base stat when re-entered). Regression test added. | Fixed |
