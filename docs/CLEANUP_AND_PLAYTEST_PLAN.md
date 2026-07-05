# Lifeburn — Cleanup & Playtest Readiness Plan

**Created:** 2026-07-05
**Status:** In progress — Phases 1 & 2 complete (2026-07-05); Phase 3 (card re-test) underway
**Goal:** Clean up leftover AI-session markdown, eliminate the recurring bug sources, re-verify every card, and get the game to a stable, playtestable, publishable-track state.

## Context

The game reached "1.0 state" on 2026-01-29 (commit `0d98248`, message notes "bugs need fixing still" — no written list of those bugs exists, so Phase 3 re-testing is how we find them). All 281 cards passed a first testing campaign (Dec 2025 – Jan 2026), and all 10 architectural inconsistencies from `inconsistencies_to_fix.md` were resolved. The root-level `session_*.md` / `summary*.md` files were session-continuity scratch notes for earlier AI workflows; every fix they describe is committed, so git history preserves them.

Recurring bug sources identified from the session logs:

1. **Card JSON typos** (`statModfiers`, `restricion`, `optoinal`, `amountModifer`) — nothing validates card JSON against the schemas in `LifeServer/Data/`.
2. **Legacy dual code paths** kept "for backwards compatibility" after refactors:
   - Old `additionalCosts` on `TriggeredEffect` vs. new `isCost: true` effects (only 6 cards migrated — see git history of `triggered_ability_migration.md`).
   - Legacy `targetType`/`maxTargets`/`minTargets`/`upTo`/`targetZone` fields vs. new `Target`/`Select` objects (only ~17 cards migrated).
3. **No automated tests** (unit tests were removed in Dec 2025) and leftover debug logging from bug-hunting sessions.

---

## Phase 1 — Markdown cleanup & knowledge capture

- [x] **Create a project `CLAUDE.md`** at repo root containing:
  - Architecture overview (Unity client `Project_Life/`, .NET 7 server `LifeServer/`, SQLite, REST + polling, card JSON in `LifeServer/Data/Cards/`).
  - Build/run instructions (`dotnet build LifeServer/`, Unity scene flow).
  - **Card JSON conventions harvested from the session files' "Key Learnings" sections**, including at minimum:
    - `scope: "selfOnly"` on "when this enters/attacks/dies" triggers (no scope defaults to `All` on `enteredZone` = fires for every card).
    - `phaseOfPlayer: "player"` on phase triggers that should only fire on the controller's turn.
    - `zone: "x"` for non-targeting whole-zone effects; `select: { zone, tribe, cardType, max }` for zone selection; `target: { type, min, max }` for in-play targeting.
    - `targetType` enables targeting; `cardType` alone does not.
    - `sourcePlayer` = who performs the action; `affectedPlayer` = who is affected; `player` on a TriggeredEffect = trigger-condition filter.
    - Conditions belong at the triggered-effect level to prevent the trigger firing, not on the inner effect.
    - Both the outer ActivatedEffect/TriggeredEffect AND inner Effect need `targetType`/`tribe` for proper filtering.
    - When a card changes zones, remove it from the old zone list before adding to the new one (double-trigger bug class).
- [x] **Move reference docs into `docs/`:** `LIFESERVER_GAMEPLAY_MECHANICS.md`, `LIFESERVER_CLIENT_COMMUNICATION.md` (keep these — genuine architecture references).
- [x] **Keep `CARD_TEST_TRACKER.md` at repo root** (the `/cardbatch` and `/impcard` skills reference it; it becomes the Phase 3 tracker).
- [x] **Delete the 13 scratch files** (git history preserves all content):
  `session_4.md`, `session_5.md`, `session_6.md`, `session_7.md`, `session_8.md`, `session_9.md`, `session_10.md`, `session_12.md`, `summary-3.md`, `summary_11.md`, `session_summary_2025-12-20.md`, `session_summary_2025-12-22.md`, `SESSION_SUMMARY_StoneToss_XCosts.md`, `triggered_ability_migration.md`, `inconsistencies_to_fix.md`.
  (Delete only after the Key Learnings are captured in `CLAUDE.md`.)

## Phase 2 — Kill the bug factory

- [x] **Finish the `isCost` migration:** convert all remaining cards using `TriggeredEffect.additionalCosts` to `isCost: true` effects, then delete `TriggeredEffect.additionalCosts`, `SendNextTriggerCostEvent()`, and related handling in `HandleCostSelection()`.
- [x] **Finish the `Target`/`Select` migration:** convert all card JSONs still using legacy `targetType`/`maxTargets`/`minTargets`/`upTo`/`targetZone` fields, then remove the legacy fields and fallback code paths from `Effect.cs`, `Qualifier.cs`, `StackObj.cs`, `GameMatch.cs`.
- [x] **Add card JSON schema validation** — validate all 281 card files against `LifeServer/Data/CardSchema.json` (and the Active/AdditionalCost/AlternateCost schemas) at server startup or as a `dotnet test` step. Unknown/misspelled property names must fail loudly. This alone would have caught most historical card bugs.
- [x] **Reinstate a test project** in `LifeServer/Tests/` targeting the known bug classes:
  - Zone-transition bookkeeping (card never present in two zone lists at once).
  - Trigger scoping (selfOnly/othersOnly/all).
  - Cost payment / fizzle behavior for `isCost` effects.
- [x] **Strip leftover debug logging** added during Dec 2025 bug hunts (`[PassPrio]`, `[PassPrioToPlayer]`, `[FinishWithTriggers]`, `[AddStackObjToStack]`, `[ShouldAutoSkipPhases]`, `[CheckForTriggersAndPassives]`, `[AddOrderedTriggersToStack]`, QualifyCard/QualifyTrigger logs, etc.).

## Phase 3 — Full card re-test (NEW testing phase)

**Every single card gets tested again**, from scratch, after the Phase 2 migrations — the legacy-path removal touches nearly every card, so prior "Passed" statuses no longer count.

- [x] **Reset `CARD_TEST_TRACKER.md` for Round 2:** set all 281 cards back to `Untested`, keep the historical notes column (rename to "Round 1 Notes" or similar), and add a fresh notes column for this round. Update the header counts (Untested: 281 / Passed: 0 / Failed: 0).
- [ ] **Test in batches** (the `/cardbatch` and `/impcard` skills support this workflow), logging every fix in the tracker as before.
- [x] **Track engine bugs found during re-testing** in a new `docs/BUGS.md` (replaces the untracked "bugs need fixing still" from the 1.0 commit).
- [ ] Exit criteria: all 281 cards `Passed` in Round 2, `docs/BUGS.md` empty or all items resolved.

## Phase 4 — Playtesting & pre-publish flags

- [ ] Resume human playtesting (lobby → match flow from session 12 is the entry point).
- [ ] Pre-publish items (fine for playtesting, **not** fine for release):
  - Replace MD5 Basic Auth with proper password hashing + token auth.
  - Remove the hardcoded ngrok URL in `Project_Life/Assets/Scripts/Network/ServerApi.cs`; make the server address configurable.
  - Revisit 1-second polling (batching, backoff, or push-based updates).
  - Decide whether `LifeServer/life.sqlite` should stay in git (binary DB can't merge; running the server on two machines between pulls clobbers data). Consider ignoring it and adding a schema/seed script instead.

---

## Open questions

1. What were the known bugs behind the "bugs need fixing still" note on the Jan 29 commit? If remembered, add them to `docs/BUGS.md` up front; otherwise Phase 3 will resurface them.
2. `life.sqlite` in git — keep syncing via git, or ignore + seed script? (See Phase 4.)
