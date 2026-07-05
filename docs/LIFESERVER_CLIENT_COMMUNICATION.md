# LifeServer & Project_Life Client Communication Documentation

## Overview

This document describes the communication architecture between the **LifeServer** (.NET 7.0 ASP.NET Core HTTP API) and **Project_Life** (Unity client) for the Lifeburn collectible card game.

**Communication Protocol:** RESTful HTTP with JSON payloads
**Authentication:** HTTP Basic Authentication (Base64 encoded username:MD5_password)
**Real-time Updates:** Client-side polling (1-second intervals)

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Authentication](#2-authentication)
3. [API Endpoints Reference](#3-api-endpoints-reference)
4. [Data Models](#4-data-models)
5. [Communication Flows](#5-communication-flows)
6. [Game State Management](#6-game-state-management)
7. [Error Handling](#7-error-handling)
8. [File Reference Map](#8-file-reference-map)

---

## 1. Architecture Overview

### Server Stack
- **Framework:** .NET 7.0 ASP.NET Core Web API
- **Database:** SQLite (`life.sqlite`)
- **Serialization:** Newtonsoft.Json
- **Entry Point:** `LifeServer/Server/Program.cs`
- **Controller:** `LifeServer/Server/Controllers/LifeController.cs` (29 endpoints)

### Client Stack
- **Engine:** Unity
- **Networking:** UnityWebRequest
- **Serialization:** Newtonsoft.Json (Json.NET)
- **API Layer:** `Project_Life/Assets/Scripts/Network/ServerApi.cs`

### Base URL Configuration

**Server Route:** `/life/` (defined by `[Route("[controller]")]` on LifeController)

**Client Configuration:** `ServerApi.cs:11`
```csharp
baseAddress = "https://f0f8217697ce.ngrok-free.app/life/"
```

---

## 2. Authentication

### Authentication Method

HTTP Basic Authentication with MD5 password hashing.

### Client-Side (ServerApi.cs:59-63)
```csharp
private static string GetHash(string inputString) {
    var hashData = MD5.HashData(Encoding.ASCII.GetBytes(inputString));
    return BitConverter.ToString(hashData).Replace("-", "").ToLower();
}
```

### Header Format
```
Authorization: Basic base64(username:md5_hashed_password)
```

### Server-Side Validation (LifeController.cs:22-26, 51-63)
```csharp
private bool IsAuthorized(out int accountId) {
    accountId = SqlAuthenticate();
    return accountId >= 0;
}

private (string, string) NameAndHashFromAuthHeader() {
    var authHeader = Request.Headers.Authorization;
    var headerChunks = authHeader.ToString().Split(' ');
    var decodedCredentials = Encoding.UTF8.GetString(Convert.FromBase64String(headerChunks[1]));
    var credentials = decodedCredentials.Split(':');
    return (credentials[0], credentials[1]);
}
```

### Protected vs Public Endpoints

| Endpoint Type | Authentication Required | Example |
|---------------|------------------------|---------|
| Account Management | Yes | `GET /life/accounts` |
| User Decks | Yes | `GET /life/accounts/decks` |
| Match Operations | Yes | All `/life/match/*` endpoints |
| Card Catalog | No | `GET /life/cards` |

---

## 3. API Endpoints Reference

### 3.1 Account Endpoints

| Endpoint | Method | Description | Request | Response |
|----------|--------|-------------|---------|----------|
| `/accounts` | GET | Login/authenticate | Auth header | `AccountData` |
| `/accounts` | POST | Create account | `AccountData` body | 201 + ID or 409 |
| `/accounts/decks` | GET | Get user's decks | Auth header | `List<DeckData>` |
| `/accounts/cards` | GET | Get card collection | Auth header | `List<int>` |
| `/accounts/decks` | POST | Create/update deck | `DeckData` body | 201 or 200 |
| `/accounts/decks/{deckId}` | DELETE | Delete deck | Auth header | Status code |

### 3.2 Card Catalog Endpoints

| Endpoint | Method | Description | Response |
|----------|--------|-------------|----------|
| `/cards` | GET | Get all cards (281 total) | `List<CardDisplayData>` |
| `/cards/{id}` | GET | Get single card | `CardDisplayData` |

### 3.3 Matchmaking Endpoints

| Endpoint | Method | Description | Response |
|----------|--------|-------------|----------|
| `/queue/{deckId}` | GET | Enter queue with deck | `MatchState` or null |
| `/queue` | DELETE | Exit queue | Status code |
| `/match/{matchId}/game-ready` | GET | Check if both players ready | `MatchState` |

### 3.4 Game State Endpoints

| Endpoint | Method | Description | Response |
|----------|--------|-------------|----------|
| `/match/{matchId}` | GET | Get current state + events | `MatchState` |
| `/match/{matchId}/pass` | GET | Pass priority | Status code |

### 3.5 Card Action Endpoints

| Endpoint | Method | Description | Parameters |
|----------|--------|-------------|------------|
| `/match/{matchId}/cast-attempt/{cardUid}` | GET | Attempt to cast | cardUid in route |
| `/match/{matchId}/activation-attempt/{cardUid}` | GET | Activate ability | cardUid in route |
| `/match/{matchId}/choose/{choiceIndex}` | POST | Make choice | choiceIndex in route |

### 3.6 Selection Endpoints

| Endpoint | Method | Description | Body |
|----------|--------|-------------|------|
| `/match/{matchId}/card-select` | POST | Card selection | `List<List<int>>` UIDs |
| `/match/{matchId}/ordering` | POST | Trigger ordering | `List<int>` indices |
| `/match/{matchId}/tribute-select` | POST | Tribute cards | `List<int>` UIDs |
| `/match/{matchId}/target-select` | POST | Select targets | `List<int>` UIDs |
| `/match/{matchId}/cost-select` | POST | Cost payment | `List<int>` UIDs |
| `/match/{matchId}/set-x/{amount}` | POST | Set X value | amount in route |
| `/match/{matchId}/set-amount/{amount}` | POST | Set amount | amount in route |

### 3.7 Combat Endpoints

| Endpoint | Method | Description | Body |
|----------|--------|-------------|------|
| `/match/{matchId}/attack` | POST | Submit attacks | - |
| `/match/{matchId}/attackables/{attackingUid}` | GET | Get valid targets | - |
| `/match/{matchId}/attack/assign-attack` | POST | Assign attack | `(int, int)` tuple |
| `/match/{matchId}/attack/unassign-attack/{attackerUid}` | DELETE | Remove assignment | - |
| `/match/{matchId}/add-secondary-attacker` | POST | Add secondary | `(int, int)` tuple |

---

## 4. Data Models

### 4.1 AccountData

**Server:** `LifeServer/Server/AccountData.cs`
**Client:** `Project_Life/Assets/Scripts/Network/AccountData.cs`

```csharp
public class AccountData {
    public int id { get; set; }
    public string displayName { get; set; }
    public string username { get; set; }
    public string email { get; set; }
    public string hashedPassword { get; set; }
}
```

### 4.2 DeckData

**Server:** `LifeServer/Server/DeckData.cs`
**Client:** `Project_Life/Assets/Scripts/DeckData.cs`

```csharp
public class DeckData {
    public int id { get; set; }
    public string deckName { get; set; }
    public List<int> deckList { get; set; }  // Card IDs
}
```

### 4.3 CardDisplayData

**Server:** `LifeServer/Server/CardDisplayData.cs`
**Client:** `Project_Life/Assets/Scripts/CardDisplayData.cs`

```csharp
public class CardDisplayData {
    public int uid { get; set; }              // Unique instance ID (in-game)
    public int id { get; set; }               // Card definition ID
    public string name { get; set; }
    public int cost { get; set; }
    public CardType type { get; set; }
    public int? attack { get; set; }
    public int? defense { get; set; }
    public List<Keyword> keywords { get; set; }
    public Tribe tribe { get; set; }
    public Rarity rarity { get; set; }
    public string description { get; set; }
    public string additionalDescription { get; set; }
    public TokenType? tokenType { get; set; }
    public bool hasXCost { get; set; }
}
```

### 4.4 MatchState

**Server:** `LifeServer/Server/MatchState.cs`
**Client:** `Project_Life/Assets/Scripts/InGame/MatchState.cs`

```csharp
public class MatchState {
    public int matchId { get; set; }
    public PlayerState playerState { get; set; }
    public OpponentState opponentState { get; set; }
    public int turnPlayerId { get; set; }
    public int prioPlayerId { get; set; }
    public Phase currentPhase { get; set; }
    public List<CardDisplayData> playablesInPlay { get; set; }
    public Stack<StackDisplayData> stack { get; set; }
    public bool secondPass { get; set; }
    public bool allAttackersAssigned { get; set; }
}
```

### 4.5 PlayerState

**Server:** `LifeServer/Server/PlayerState.cs`
**Client:** `Project_Life/Assets/Scripts/InGame/PlayerState.cs`

```csharp
public class PlayerState : ParticipantState {
    public List<CardDisplayData> playables { get; set; }
    public List<CardDisplayData> activatables { get; set; }
    public List<GameEvent> eventList { get; set; }
}
```

### 4.6 OpponentState

**Server:** `LifeServer/Server/OpponentState.cs`
**Client:** `Project_Life/Assets/Scripts/InGame/OpponentState.cs`

```csharp
public class OpponentState {
    public string playerName { get; set; }
    public int uid { get; set; }
    public int lifeTotal { get; set; }
    public bool spellBurnt { get; set; }
    public int spellCounter { get; set; }
    public int handAmount { get; set; }
    public int deckAmount { get; set; }
}
```

### 4.7 GameEvent

**Server:** `LifeServer/Server/GameEvent.cs`
**Client:** `Project_Life/Assets/Scripts/InGame/GameEvent.cs`

```csharp
public class GameEvent {
    public CardDisplayData? focusCard { get; set; }
    public CardDisplayData? targetCard { get; set; }
    public CardDisplayData? sourceCard { get; set; }
    public Zone zone { get; set; }
    public Zone? sourceZone { get; set; }
    public StackDisplayData? focusStackObj { get; set; }
    public List<CardDisplayData>? cards { get; set; }
    public List<string>? eventMessages { get; set; }
    public List<CardSelectionData>? cardSelectionDatas { get; set; }
    public (int, int)? attackUids { get; set; }
    public int focusUid { get; set; }
    public List<int>? focusUidList { get; set; }
    public int attackerUid { get; set; }
    public int defenderUid { get; set; }
    public int amount { get; set; }
    public EventType eventType { get; set; }
    public bool isOpponent { get; set; }
    public PlayerChoice? playerChoice { get; set; }
    public TargetSelection targetSelection { get; set; }
    public List<StackDisplayData>? triggerOrderingList { get; set; }
    public CostType costType { get; set; }
    public bool universalBool { get; set; }
    public int universalInt { get; set; }
}
```

### 4.8 Event Types (EventType Enum)

```csharp
public enum EventType {
    Draw,
    Cast,
    Trigger,
    Resolve,
    Summon,
    LookAtDeck,
    SendToZone,
    Attack,
    Combat,
    Death,
    Choice,
    LifeTotalChange,
    CardSelection,
    TriggerOrdering,
    SpellBurn,
    TargetSelect,
    CostSelect,
    SetX,
    SetAmount,
    WaitForOpponent,
    AttackEvent,
    DealDamage,
    RevealFromHand,
    RevealFromDeck,
    CreateToken,
    ClearStack,
    ModifierUpdate,
    ReturnToHand,
    CounterSpell,
    SpellBurntOff,
    MatchEnd,
    Message,
    UntapAll,
    Concede
}
```

---

## 5. Communication Flows

### 5.1 Login Flow

```
┌──────────────┐                    ┌──────────────┐
│    Client    │                    │    Server    │
└──────┬───────┘                    └──────┬───────┘
       │                                   │
       │  POST /life/accounts              │
       │  Body: AccountData                │
       │──────────────────────────────────>│
       │                                   │
       │  201 Created + account ID         │
       │  (or 409 Conflict)                │
       │<──────────────────────────────────│
       │                                   │
       │  GET /life/accounts               │
       │  Auth: Basic base64(user:hash)    │
       │──────────────────────────────────>│
       │                                   │
       │  200 OK + AccountData             │
       │  (or 401 Unauthorized)            │
       │<──────────────────────────────────│
       │                                   │
```

**Client Implementation:** `LoginManager.cs:98`
```csharp
var accountData = serverApi.GetAccountData(loginUsername.text, loginPassword.text, out var httpErrorCode);
```

### 5.2 Matchmaking Flow

```
┌──────────────┐                    ┌──────────────┐
│   Player 1   │                    │    Server    │
└──────┬───────┘                    └──────┬───────┘
       │                                   │
       │  GET /life/queue/{deckId}         │
       │──────────────────────────────────>│
       │                                   │
       │  null (waiting for opponent)      │
       │<──────────────────────────────────│
       │                                   │

       ... Player 2 joins queue ...

       │  GET /life/queue/{deckId}         │
       │──────────────────────────────────>│
       │                                   │
       │  MatchState (match created)       │
       │<──────────────────────────────────│
       │                                   │
       │  GET /life/match/{id}/game-ready  │
       │──────────────────────────────────>│
       │                                   │ (polls every 1s)
       │  MatchState (when both ready)     │
       │<──────────────────────────────────│
       │                                   │
```

**Client Implementation:** `MenuManager.cs:131-159`
```csharp
IEnumerator QueueRefresh() {
    matchState = serverApi.GetNewMatchData(accountData, selectedDeck.id);
    while (matchState == null) {
        yield return new WaitForSeconds(1f);
        matchState = serverApi.GetNewMatchData(accountData, selectedDeck.id);
    }
    // Match found, check game ready
}
```

### 5.3 Game Loop Flow

```
┌──────────────┐                    ┌──────────────┐
│    Client    │                    │    Server    │
└──────┬───────┘                    └──────┬───────┘
       │                                   │
       │  GET /life/match/{matchId}        │
       │──────────────────────────────────>│ (polls every 1s)
       │                                   │
       │  MatchState + eventList           │
       │<──────────────────────────────────│
       │                                   │
       │  [Process events, render UI]      │
       │                                   │
       │  POST /life/match/{id}/choose/0   │
       │──────────────────────────────────>│ (user action)
       │                                   │
       │  200 OK                           │
       │<──────────────────────────────────│
       │                                   │
       │  GET /life/match/{matchId}        │
       │──────────────────────────────────>│ (resume polling)
       │                                   │
```

**Client Polling Implementation:** `GameManager.cs:202-232`
```csharp
private IEnumerator CheckForChangesAtInterval(float interval, GameObject repeatCheckObj) {
    while (activeCoroutines.Contains(repeatCheckObj)) {
        if (!eventAnimsAreInProgress) {
            CheckForMatchStateChanges();
        }
        yield return new WaitForSeconds(interval);
    }
}

private void CheckForMatchStateChanges() {
    gameData.matchState = serverApi.GetMatchState(gameData.accountData, gameData.matchState.matchId);
    if (gameData.matchState.playerState.eventList.Count > 0) {
        InitiateEvents();
    }
}
```

### 5.4 Combat Flow

```
┌──────────────┐                    ┌──────────────┐
│    Client    │                    │    Server    │
└──────┬───────┘                    └──────┬───────┘
       │                                   │
       │  GET /attackables/{attackerUid}   │
       │──────────────────────────────────>│
       │                                   │
       │  List<int> validTargetUids        │
       │<──────────────────────────────────│
       │                                   │
       │  POST /attack/assign-attack       │
       │  Body: (attackerUid, defenderUid) │
       │──────────────────────────────────>│
       │                                   │
       │  200 OK                           │
       │<──────────────────────────────────│
       │                                   │
       │  POST /attack                     │
       │──────────────────────────────────>│ (submit all attacks)
       │                                   │
       │  200 OK                           │
       │<──────────────────────────────────│
       │                                   │
```

---

## 6. Game State Management

### Server-Side State Storage

**Location:** `LifeController.cs:15-19`

```csharp
private static readonly Matches matches = new();
private static GameMatch? currentLocalMatch;
private static AccountDeckDTO? matchableAccount;
private static readonly object QueueLockObj = new();
private static readonly object MatchLockObj = new();
```

**Key Points:**
- Matches stored in-memory (not persisted to database)
- Thread synchronization via lock objects
- Lost on server restart

### Match Registry (Matches.cs)

```csharp
public class Matches {
    private Dictionary<int, GameMatch> _dict = new();

    public GameMatch? Get(int matchId) { ... }
    public void Add(int matchId, GameMatch match) { ... }

    public void ValidatePlayerMatch(int accountId, int matchId) {
        if (!Get(matchId).PlayerIds.Contains(accountId))
            throw new InvalidDataException("THAT'S NOT YOUR MATCH!");
    }
}
```

### Event Queue System

**Server generates events:** `GameEvent` objects added to `Player.eventList`

**Client receives events:** On `GET /match/{id}`, events are returned and cleared

**Event Processing Example:**
```csharp
// Server side (LifeController.cs:226-237)
public MatchState GetMatchState([FromRoute] int matchId) {
    if (!IsAuthorized(out var accountId)) return null;
    matches.ValidatePlayerMatch(accountId, matchId);
    var match = matches.Get(matchId)!;
    var state = match.GetMatchState(accountId);  // Clears events
    return state;
}
```

---

## 7. Error Handling

### Server-Side Exception Filter

**Location:** `LifeServer/Server/Controllers/LifeExceptionFilter.cs`

```csharp
public class LifeExceptionFilter : ActionFilterAttribute, IExceptionFilter {
    public void OnException(ExceptionContext context) {
        if (context.Exception is InvalidDataException) {
            context.Result = new BadRequestObjectResult(context.Exception.Message);
        }
        else if (context.Exception is UnauthorizedAccessException) {
            context.Result = new UnauthorizedObjectResult(context.Exception.Message);
        }
    }
}
```

### Client-Side Error Mapping

**Location:** `LoginManager.cs:112-134`

```csharp
errorMessage = errorCode switch {
    0   => "Server is offline",
    200 => "",  // Success
    400 => "Could not connect. Try again shortly.",
    401 => "Invalid username or password.",
    403 => "Access DENIED!",
    404 => "Can't connect to server",
    408 => "Can't connect to server...",
    429 => "WOAH... Slow down there.",
    500 => "Somebody messed up the server code...",
    502 => "Check your internet connection (Bad Gateway)",
    503 => "Server is currently under maintenance...",
    504 => "Check your internet connection (Gateway Timeout)",
    _   => "unknown error"
};
```

### Client Request Pattern

**Location:** `ServerApi.cs` (all request methods)

```csharp
// All methods follow this pattern:
var request = CreateRequest(baseAddress + "endpoint", RequestType.GET);
AttachAuthHeader(request, accountData);
request.SendWebRequest();
while (!request.isDone) Task.Delay(10);

if (request.responseCode == 200) {
    return JsonConvert.DeserializeObject<T>(request.downloadHandler.text);
}
return null;  // Error case returns null
```

---

## 8. File Reference Map

### Server Files

| Purpose | File Path | Key Lines |
|---------|-----------|-----------|
| **HTTP Endpoints** | `LifeServer/Server/Controllers/LifeController.cs` | 1-665 |
| **Exception Filter** | `LifeServer/Server/Controllers/LifeExceptionFilter.cs` | 1-20 |
| **Startup Config** | `LifeServer/Server/Program.cs` | 1-23 |
| **Account Model** | `LifeServer/Server/AccountData.cs` | 1-23 |
| **Deck Model** | `LifeServer/Server/DeckData.cs` | 1-17 |
| **Card Display** | `LifeServer/Server/CardDisplayData.cs` | 1-69 |
| **Match State** | `LifeServer/Server/MatchState.cs` | 1-26 |
| **Player State** | `LifeServer/Server/PlayerState.cs` | 1-25 |
| **Opponent State** | `LifeServer/Server/OpponentState.cs` | 1-14 |
| **Game Event** | `LifeServer/Server/GameEvent.cs` | 1-216 |
| **Match Registry** | `LifeServer/Server/Matches.cs` | 1-36 |
| **Game Match** | `LifeServer/Server/GameMatch.cs` | 1-100+ |
| **Player** | `LifeServer/Server/Player.cs` | 1-56 |
| **Card** | `LifeServer/Server/Card.cs` | 1-150+ |
| **SQL Functions** | `LifeServer/Server/SqlFunctions.cs` | 1-38 |

### Client Files

| Purpose | File Path | Key Lines |
|---------|-----------|-----------|
| **API Layer** | `Project_Life/Assets/Scripts/Network/ServerApi.cs` | 1-529 |
| **Account Model** | `Project_Life/Assets/Scripts/Network/AccountData.cs` | 1-24 |
| **Deck Model** | `Project_Life/Assets/Scripts/DeckData.cs` | 1-15 |
| **Card Display** | `Project_Life/Assets/Scripts/CardDisplayData.cs` | 1-35 |
| **Match State** | `Project_Life/Assets/Scripts/InGame/MatchState.cs` | 1-16 |
| **Player State** | `Project_Life/Assets/Scripts/InGame/PlayerState.cs` | 1-11 |
| **Opponent State** | `Project_Life/Assets/Scripts/InGame/OpponentState.cs` | 1-14 |
| **Game Event** | `Project_Life/Assets/Scripts/InGame/GameEvent.cs` | 1-39 |
| **Login Manager** | `Project_Life/Assets/Scripts/LoginScreen/LoginManager.cs` | 17, 75, 98 |
| **Menu Manager** | `Project_Life/Assets/Scripts/MainMenu/MenuManager.cs` | 60, 70, 134 |
| **Game Manager** | `Project_Life/Assets/Scripts/GameManager.cs` | 88, 226, 566 |
| **Deck Editor** | `Project_Life/Assets/Scripts/DeckEditor/DeckEditManager.cs` | 55, 224 |
| **Game Data** | `Project_Life/Assets/Scripts/GameData.cs` | 86, 106 |

### Database Schema

**File:** `LifeServer/life.sqlite`

| Table | Columns | Purpose |
|-------|---------|---------|
| `accounts` | id, display_name, username, email_address, pwdhash | User accounts |
| `decks` | id, deck_name | Deck definitions |
| `account_decks` | account_id, deck_id | User-deck relationships |
| `deck_cards` | deck_id, card_id | Cards in decks |
| `account_cards` | account_id, card_id | User card collection |

---

## Appendix A: Complete Endpoint List

| # | Endpoint | Method | Auth | Purpose |
|---|----------|--------|------|---------|
| 1 | `/accounts` | GET | Yes | Login |
| 2 | `/accounts` | POST | No | Create account |
| 3 | `/accounts/decks` | GET | Yes | Get user decks |
| 4 | `/accounts/decks` | POST | Yes | Create/update deck |
| 5 | `/accounts/decks/{id}` | DELETE | Yes | Delete deck |
| 6 | `/accounts/cards` | GET | Yes | Get card collection |
| 7 | `/cards` | GET | No | Get all cards |
| 8 | `/cards/{id}` | GET | No | Get single card |
| 9 | `/queue/{deckId}` | GET | Yes | Enter queue |
| 10 | `/queue` | DELETE | Yes | Exit queue |
| 11 | `/match/{id}/game-ready` | GET | Yes | Check ready status |
| 12 | `/match/{id}` | GET | Yes | Get match state |
| 13 | `/match/{id}/pass` | GET | Yes | Pass priority |
| 14 | `/match/{id}/cast-attempt/{uid}` | GET | Yes | Cast spell/summon |
| 15 | `/match/{id}/activation-attempt/{uid}` | GET | Yes | Activate ability |
| 16 | `/match/{id}/choose/{index}` | POST | Yes | Make choice |
| 17 | `/match/{id}/card-select` | POST | Yes | Card selection |
| 18 | `/match/{id}/ordering` | POST | Yes | Trigger ordering |
| 19 | `/match/{id}/tribute-select` | POST | Yes | Select tributes |
| 20 | `/match/{id}/target-select` | POST | Yes | Select targets |
| 21 | `/match/{id}/cost-select` | POST | Yes | Pay costs |
| 22 | `/match/{id}/set-x/{amount}` | POST | Yes | Set X value |
| 23 | `/match/{id}/set-amount/{amount}` | POST | Yes | Set amount |
| 24 | `/match/{id}/attack` | POST | Yes | Submit attacks |
| 25 | `/match/{id}/attackables/{uid}` | GET | Yes | Get attack targets |
| 26 | `/match/{id}/attack/assign-attack` | POST | Yes | Assign attack |
| 27 | `/match/{id}/attack/unassign-attack/{uid}` | DELETE | Yes | Remove assignment |
| 28 | `/match/{id}/add-secondary-attacker` | POST | Yes | Add secondary |
| 29 | `/data/{id}` | GET | No | Get match data |

---

## Appendix B: Enums Reference

### CardType
```csharp
Summon, Spell, Object, Token
```

### Phase
```csharp
Draw, Main, Combat, Damage, SecondMain, End
```

### Zone
```csharp
Deck, Hand, Play, Graveyard, Exile, Token
```

### CostType
```csharp
Stones, Sacrifice, Tribute, ExileFromHand, ExileFromGraveyard,
Discard, Exile, LoseLife, DiscardOrSacrificeMerfolk, Reveal, Life
```

### Tribe
```csharp
Golem, Merfolk, Shadow, Goblin, Treefolk
```

### Rarity
```csharp
Common, Uncommon, Rare
```
