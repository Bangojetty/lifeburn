# Lifeburn

Two-player collectible card game. Authoritative .NET server + Unity client, communicating over REST with client-side polling.

## Architecture

- **`LifeServer/`** — .NET 7 ASP.NET Core Web API (`LifeServer.sln`).
  - `Server/Program.cs` — entry point. Runs SQLite migrations on startup (`SqlFunctions.RunMigrations`), registers `MatchCleanupService` (disconnect detection).
  - `Server/Controllers/LifeController.cs` — the entire API surface (~29 endpoints under `/life/`): accounts, decks, collections, matchmaking queue, lobbies, friends, draft, bot matches, and every in-game action (`cast-attempt`, `pass`, `attack`, `target-select`, `cost-select`, `choose`, `set-x`, …).
  - `Server/GameMatch.cs` — the rules engine (~6,900 lines): phases, priority, the stack, triggers, combat, costs, targeting.
  - `Server/CardProperties/` — the data-driven card model (`Effect`, `TriggeredEffect`, `PassiveEffect`, `ActivatedEffect`, `Trigger`, `Condition`, `Qualifier` inputs, enums). Effect behavior lives in the big `switch` in `Effect.cs` (`Resolve()`), NOT in the per-effect stubs under `Server/Effects/` (those exist only for JSON polymorphic deserialization via `EffectTypeConverter`).
  - `Server/Data/` → **card content**: `LifeServer/Data/Cards/*.json` (281 cards, filename format `NNN_CardName.json`) plus JSON schemas (`CardSchema.json`, `ActiveSchema.json`, `AdditionalCostSchema.json`, `AlternateCostSchema.json`). Card data resolves relative to the executable (`Utils.GetDataBasePath()`), with upward directory search fallback.
  - `life.sqlite` — accounts, decks, collections, lobbies, friends. Gameplay data is NOT in the DB; it's in the card JSONs. All matches live in static in-memory state.
- **`Project_Life/`** — Unity 2022.3.62f3 client.
  - All networking through `Assets/Scripts/Network/ServerApi.cs` (UnityWebRequest + Newtonsoft). `baseAddress` toggles between `LOCAL_SERVER` (`http://localhost:5239/life/`) and `PUBLIC_SERVER` (`http://157.230.138.62:5239/life/`).
  - `Assets/Scripts/GameManager.cs` — in-match controller: polls `GET match/{id}` every 1s and animates the returned event queue (`EventType` switch).
  - `Assets/Scripts/GameData.cs` — `DontDestroyOnLoad` singleton: card DB, sprites, current deck, match state.
  - Scenes: Login Screen → Main Menu → (Deck Editor | Draft Scene | Game Scene). `Testing_Scene`/`Launcher` are not in the build.
  - Client mirrors server DTOs/enums by hand (no shared assembly) — when a server DTO changes, update the matching client class under `Assets/Scripts/` too.

## Build & run

- Server: `dotnet build "LifeServer/LifeServer.sln"`; run with `dotnet run --project LifeServer/Server` (listens on `0.0.0.0:5239` http / `7039` https per `launchSettings.json`).
- Tests: `dotnet test LifeServer/Tests` — includes strict validation of every card JSON (unknown properties fail) plus gameplay-invariant tests. The server also validates all cards at startup (`Utils.ValidateAllCards`).
- **Production server**: DigitalOcean droplet `157.230.138.62` (ssh as `dev`), systemd unit `lifeburn.service`, app at `/home/dev/lifeburn/app` (self-contained linux-x64 publish + `Data/` folder), DB at `/home/dev/lifeburn/life.sqlite` (NOT in git — `.gitignore`d). Deploy: `dotnet publish LifeServer/Server -c Release -r linux-x64 --self-contained true`, copy `LifeServer/Data` into the publish dir, tar + scp to the droplet, extract into `~/lifeburn/app`, `sudo systemctl restart lifeburn`. Client `PUBLIC_SERVER` points at `http://157.230.138.62:5239/life/`.
- Client: open `Project_Life/` in Unity 2022.3.62f3. For local testing set `ServerApi.baseAddress = LOCAL_SERVER`.
- Solo card testing: the server has a **test bot** matchmaking path (creates "Test Bot" player, id -999); used with the `/impcard` and `/cardbatch` workflows against `CARD_TEST_TRACKER.md` (repo root).

## Card JSON conventions (hard-won — violating these caused most historical bugs)

Scoping & triggers:
- `scope: "selfOnly"` is REQUIRED on "when this enters/attacks/dies" triggers. `enteredZone` triggers with no scope default to `Scope.All` and fire for EVERY card entering that zone. (`Scope` = `selfOnly` / `othersOnly` / `all`; `PassiveEffect` defaults to `all`, `TriggeredEffect` defaults to `selfOnly` — but be explicit.)
- Self-referential passives ("THIS card costs 0 if…", e.g. `modifyCost`) also REQUIRE `scope: "selfOnly"` — passives are applied as auras (`ApplyPassive`/`QualifyCard`), and the applied clones drop their conditions, so an unscoped conditional passive sprays a permanent effect onto every card the moment its condition is first met (the FoundryGolem 0-cost bug).
- `phaseOfPlayer: "player"` on phase triggers that should only fire on the controller's turn.
- Conditions go at the **triggered-effect level** to prevent the trigger firing at all; a condition on the inner effect still fires the trigger (and prompts) but no-ops.
- Tokens are `Zone.Play` (there is no token zone). Match stones with `"zone": "play"` + `"tokenType": "stone"`.

Targeting & selection:
- `targetType` is what enables targeting; `cardType` alone only filters, it does not create a target prompt.
- Both the outer `ActivatedEffect`/`TriggeredEffect` AND the inner `Effect` need `targetType`/`tribe` for proper filtering.
- New-style selection: `target: { type, min, max }` for targeting things in play; `select: { zone, tribe, cardType, max }` for choosing from a zone. `zone: "graveyard"` (no select/target) = non-targeting whole-zone effect. Legacy fields (`targetZone`, `maxTargets`, `minTargets`, `upTo`) still exist on ~14 unmigrated cards — do not use them in new cards.
- `Qualifier` reads both effect-level fields and `select.*` fields; keep them consistent.

Players & costs:
- `sourcePlayer` = who performs the action; `affectedPlayer` = who is affected; `player` on a `TriggeredEffect` = trigger-condition filter (whose action fires it).
- Trigger costs are effects with `isCost: true` placed in the effects array (e.g. `{ "effect": "reveal", "scope": "selfOnly", "isCost": true }`). Effects before an unpayable cost stay resolved; the cost and everything after fizzles. Auto-pay happens for self-reveal, self-sacrifice, and single-candidate token sacrifice; multiple candidates prompt selection. The legacy `TriggeredEffect.additionalCosts` array is deprecated.
- Card-level `additionalCosts` (paid before the card goes on the stack) are a separate, still-current system.

Engine invariants:
- When a card changes zones, remove it from the old zone's list BEFORE adding it to the new one — a card present in two zone lists gets double triggers.
- Use `lastControllingPlayer` as fallback controller for cards no longer in play (e.g. resolving death triggers).
- Optional effects that get accepted must re-enter `StackObj` resolution (selection checks), not call `Resolve()` directly.
- Effects that cause visual changes followed by user input must halt resolution (`ShouldHaltAfterResolve`) so the client doesn't batch the animations.
- Client UI state for repeated prompts (ordering, selection) must be explicitly cleared between prompts.

Conditions that read a card's COMPUTED stats (`oneOneInPlay`, `targetAttack`) are dangerous on a card's own passive: computing attack/defense evaluates the card's passive conditions, so a self-referential stat condition recurses. `GetAttack`/`GetDefense` now have re-entrancy guards (return base stat when re-entered), but prefer base-stat checks in new conditions.

Common card-JSON typo bugs seen historically: `statModfiers`, `restricion`, `optoinal`, `amountModifer` — nothing validates property names yet, so typos silently no-op. Check spelling against `LifeServer/Data/CardSchema.json`.

## Docs & trackers

- `docs/CLEANUP_AND_PLAYTEST_PLAN.md` — current working plan.
- `docs/LIFESERVER_GAMEPLAY_MECHANICS.md` — full rules-engine reference (turn structure, stack, combat, effect system).
- `docs/LIFESERVER_CLIENT_COMMUNICATION.md` — REST API + auth + polling reference.
- `CARD_TEST_TRACKER.md` (repo root — keep it there; `/cardbatch` and `/impcard` reference it) — per-card test status.
