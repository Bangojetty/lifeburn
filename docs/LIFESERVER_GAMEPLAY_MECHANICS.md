# LifeServer Gameplay Mechanics Documentation

## Overview

This document describes the server-side gameplay functionality for the Lifeburn collectible card game. It covers the core game loop, card system, effect processing, combat mechanics, and all related systems.

**Game Type:** Turn-based collectible card game (CCG)
**Players:** 2-player matches
**Win Condition:** Reduce opponent's life total to 0

---

## Table of Contents

1. [Game Initialization](#1-game-initialization)
2. [Turn Structure & Phases](#2-turn-structure--phases)
3. [Priority System](#3-priority-system)
4. [Card System](#4-card-system)
5. [Card Data Schema](#5-card-data-schema)
6. [Effect System](#6-effect-system)
7. [Triggered Abilities](#7-triggered-abilities)
8. [Activated Abilities](#8-activated-abilities)
9. [Passive Effects](#9-passive-effects)
10. [Stack System](#10-stack-system)
11. [Combat System](#11-combat-system)
12. [Cost System](#12-cost-system)
13. [Targeting System](#13-targeting-system)
14. [Token System](#14-token-system)
15. [Win/Loss Conditions](#15-winloss-conditions)
16. [Card JSON Examples](#16-card-json-examples)

---

## 1. Game Initialization

**File:** `LifeServer/Server/GameMatch.cs:81-113`

### Match Creation

When two players are matched, a `GameMatch` object is created:

```csharp
public GameMatch(int matchId, Player playerOne, Player playerTwo) {
    uidCounter = 1000;
    this.matchId = matchId;
    this.playerOne = playerOne;
    this.playerTwo = playerTwo;
    accountIdToPlayer = new Dictionary<int, Player>() {
        { playerOne.playerId, playerOne },
        { playerTwo.playerId, playerTwo }
    };
    stack = new Stack<StackObj>();
}
```

### Initialization Sequence

`InitializeMatch()` performs the following steps:

1. **Validate Decks** - Ensure both players have loaded decks
2. **Set Phase** - Initialize to `Phase.Draw`
3. **Shuffle Decks** - Fisher-Yates shuffle algorithm
4. **Set Owned Cards** - Track card ownership for each player
5. **Assign UIDs** - Give each card a unique instance ID (starting at 1000)
6. **Determine First Player** - Random selection
7. **Draw Opening Hands** - Each player draws 5 cards
8. **Check Opening Hand Triggers** - Process `Trigger.OpeningHand` effects

### Key State Variables

| Variable | Type | Purpose |
|----------|------|---------|
| `matchId` | int | Unique match identifier |
| `uidCounter` | int | Counter for unique card instance IDs |
| `cardByUid` | Dictionary<int, Card> | Map UIDs to card objects |
| `turnPlayerId` | int | Current turn player |
| `prioPlayerId` | int | Player with priority |
| `currentPhase` | Phase | Current game phase |
| `turn` | int | Turn counter (starts at 1) |
| `stack` | Stack<StackObj> | Spell/ability stack |
| `allCardsInPlay` | List<Card> | All cards on the battlefield |

---

## 2. Turn Structure & Phases

**File:** `LifeServer/Server/Phase.cs`

### Phase Enum

```csharp
public enum Phase {
    Draw,        // 0 - Draw a card
    Main,        // 1 - Play summons, spells, abilities
    Combat,      // 2 - Declare attackers
    Damage,      // 3 - Resolve combat damage
    SecondMain,  // 4 - Additional main phase
    End          // 5 - End of turn cleanup
}
```

### Phase Progression

**File:** `GameMatch.cs:1422-1442` (GoToNextPhase)

```
Turn Start
    │
    ▼
┌─────────┐
│  Draw   │ → Draw 1 card
└────┬────┘
     │
     ▼
┌─────────┐
│  Main   │ → Cast spells, summon creatures, activate abilities
└────┬────┘
     │
     ▼
┌─────────┐
│ Combat  │ → Declare attackers, assign targets
└────┬────┘
     │
     ▼
┌─────────┐
│ Damage  │ → Resolve combat damage
└────┬────┘
     │
     ▼
┌─────────────┐
│ SecondMain  │ → Additional spell/summon window
└────┬────────┘
     │
     ▼
┌─────────┐
│   End   │ → End-of-turn triggers, cleanup
└────┬────┘
     │
     ▼
  Pass Turn → Next player's Draw phase
```

### Phase Transitions

When transitioning phases:

1. Check for phase-based triggers (`Trigger.Phase`)
2. Create `NextPhase` event for both players
3. In Combat phase: calculate attack-capable creatures
4. In Draw phase: draw 1 card automatically

### End of Turn

**File:** `GameMatch.cs:1444-1477`

At end of turn:
- Remove "ThisTurn" passives from all cards
- Reset summoning sickness
- Reset damage taken on all creatures
- Destroy creatures with temporary "ThisTurn" passive
- Reset `turnSummonCount` to 0
- Remove spellburn from both players
- Switch turn and priority to opponent

---

## 3. Priority System

**File:** `GameMatch.cs:947-952, 1241-1268`

### Priority Flow

Priority determines which player can take actions:

```csharp
private void PassPrioToPlayer(Player player) {
    prioPlayerId = player.playerId;
    CalculatePossibleMoves(player);  // Determine playables/activatables
    GameEvent gEvent = new GameEvent(EventType.GainPrio);
    AddEventForPlayer(player, gEvent);
}
```

### Priority Resolution

When a player passes priority:

1. **First Pass:** Set `secondPass = true`, pass priority to opponent
2. **Second Pass (both passed):**
   - If stack has objects: Pop and resolve top object
   - If attacks pending: Resolve combat
   - Otherwise: Advance to next phase

### Playability Calculation

**File:** `Utils.cs:23-102`

Cards are playable based on:

| Card Type | Requirements |
|-----------|-------------|
| **Spell** | Life > cost, can play any time with priority |
| **Object** | Life > cost, your turn, main phase, empty stack |
| **Summon** | Your turn, main phase, empty stack, first summon (unless bypass) |
| **Token** | Never directly playable |

Additional checks:
- Target availability for targeted effects
- Additional cost availability (sacrifice, discard, etc.)

---

## 4. Card System

### Card Definition vs Card Instance

The game separates **card definitions** (static data) from **card instances** (runtime state):

| Aspect | Card Definition (CardDto) | Card Instance (Card) |
|--------|---------------------------|----------------------|
| **Storage** | JSON files | In-memory |
| **Identity** | `id` (0-280) | `uid` (unique per instance) |
| **Properties** | Base stats | Dynamic (modified by effects) |
| **Location** | `Data/Cards/` folder | `GameMatch.cardByUid` |

### Card Loading

**File:** `Utils.cs:13-21`

```csharp
public static string GetCardJson(int cardId) {
    string result = cardId.ToString("D3");  // Zero-pad to 3 digits
    string directoryPath = @"C:\...\Lifeburn\Data\Cards";
    string[] matchingFiles = Directory.GetFiles(directoryPath, result + "*");
    // Read and return JSON content
}
```

Card files are named: `001_CardName.json`, `042_AnotherCard.json`, etc.

### Card Instance Creation

**File:** `Card.cs` (GetCard method)

```csharp
public static Card GetCard(int uid, int cardId, GameMatch? match = null) {
    string cardJson = Utils.GetCardJson(cardId);
    JsonSerializerSettings settings = new();
    settings.Converters.Add(new EffectTypeConverter());  // Polymorphic effect deserialization
    CardDto newCardDto = JsonConvert.DeserializeObject<CardDto>(cardJson, settings);
    Card newCard = new Card(uid, newCardDto);
    if (match != null) newCard.currentGameMatch = match;
    return newCard;
}
```

---

## 5. Card Data Schema

### CardDto Structure

**File:** `LifeServer/Server/CardDto.cs`

```csharp
public class CardDto {
    public int id;                                    // Card definition ID (0-280)
    public string name;                              // Display name
    public int cost;                                 // Life/tribute cost
    public CardType type;                            // Summon | Spell | Object | Token
    public int? attack;                              // Attack stat (nullable for spells)
    public int? defense;                             // Defense stat (nullable for spells)
    public List<Keyword>? keywords;                  // Keyword abilities
    public Tribe tribe;                              // Creature tribe
    public Rarity rarity;                            // Common | Uncommon | Rare
    public string description;                       // Card text (with effect markers)
    public List<Effect>? stackEffects;               // Effects when cast
    public List<TriggeredEffect>? triggeredEffects;  // Triggered abilities
    public List<PassiveEffect>? passiveEffects;      // Passive bonuses
    public List<ActivatedEffect>? activatedEffects;  // Activated abilities
    public List<CostModifier>? costModifiers;        // Cost modification rules
    public List<AdditionalCost>? additionalCosts;    // Extra costs (reveal, sacrifice)
    public List<CastRestriction>? castRestrictions;  // Casting restrictions
}
```

### Card Types

**File:** `LifeServer/Server/CardProperties/CardType.cs`

```csharp
public enum CardType {
    Summon,  // Creature cards - remain on battlefield
    Spell,   // One-time effects - go to graveyard after resolution
    Object,  // Permanent effects - remain on battlefield
    Token    // Generated creatures - not in deck
}
```

### Tribes

**File:** `LifeServer/Server/CardProperties/Tribe.cs`

```csharp
public enum Tribe {
    Golem,     // Stone-based creatures
    Merfolk,   // Water creatures
    Shadow,    // Dark/ghost creatures
    Goblin,    // Goblin creatures
    Treefolk   // Plant/tree creatures
}
```

### Keywords

**File:** `LifeServer/Server/Keyword.cs`

```csharp
public enum Keyword {
    Haunt,     // Triggers on death
    Blitz,     // Can attack immediately (no summoning sickness)
    Sprout,    // Creates plant tokens
    Trample,   // Excess damage goes to player
    Taunt,     // Must be attacked first
    Dive,      // Can attack player directly (flying)
    Scorch,    // Deals damage when entering play
    Exhaust,   // Tap effect
    Spectral   // Can't be attacked
}
```

### Rarities

```csharp
public enum Rarity {
    Common,
    Uncommon,
    Rare
}
```

### Zones

**File:** `LifeServer/Server/CardProperties/Zone.cs`

```csharp
public enum Zone {
    Deck,       // In library
    Hand,       // In hand
    Play,       // On battlefield
    Graveyard,  // Discard pile
    Exile,      // Removed from game
    Token       // Token zone (special)
}
```

---

## 6. Effect System

### Effect Types

**File:** `LifeServer/Server/CardProperties/EffectType.cs`

The game supports 54 effect types:

| Category | Effect Types |
|----------|-------------|
| **Card Draw/Mill** | `Draw`, `Mill`, `LookAtDeck`, `Tutor`, `ShuffleDeck` |
| **Damage/Life** | `DealDamage`, `GainLife`, `LoseLife`, `SetLifeTotal` |
| **Destruction** | `Destroy`, `Sacrifice`, `Counter` |
| **Zone Movement** | `SendToZone`, `Discard`, `ExileAndReturn`, `Detain` |
| **Stat Modification** | `ChangeStats`, `GrantKeyword`, `GrantPassive` |
| **Tokens** | `CreateToken` |
| **Control** | `GainControl`, `ForceAttack` |
| **Cost Modification** | `ModifyCost`, `BypassHerbLifeReduction` |
| **Game Flow** | `GoToPhase`, `ExtraTurn`, `EndTurn`, `EndGame` |
| **Special** | `Choose`, `Repeat`, `RepeatAllEffects`, `CopySpell`, `CastCard` |
| **Reveal** | `Reveal` |
| **Counters** | `AddCounter`, `RemoveCounter` |
| **Restrictions** | `CantGainLife`, `CantAttackOrBlock`, `CantAttack`, `CantTribute` |

### Effect Class Structure

**File:** `LifeServer/Server/CardProperties/Effect.cs`

Key properties:

```csharp
public class Effect {
    // Core
    public EffectType effect;              // The effect type
    public List<Condition>? conditions;    // Must be met to resolve
    public int? amount;                    // Effect magnitude

    // Targeting
    public TargetType? targetType;         // Player, Summon, Any, Token
    public List<int> targetUids;           // Selected targets (runtime)
    public bool self;                      // Affects source card
    public bool isOpponent;                // Affects opponent
    public bool all;                       // Affects all qualifying cards

    // Qualifiers
    public CardType? cardType;             // Filter by card type
    public Tribe? tribe;                   // Filter by tribe
    public Zone? zone;                     // Source zone
    public Zone? destination;              // Target zone
    public TokenType? tokenType;           // Token type for CreateToken

    // Stat changes
    public int? attack;                    // Attack modifier
    public int? defense;                   // Defense modifier
    public Keyword? keyword;               // Keyword to grant
    public List<PassiveEffect>? passives;  // Passives to grant

    // Dynamic amounts
    public AmountBasedOn? amountBasedOn;   // Calculate amount from game state

    // Choices
    public List<List<Effect>>? choices;    // Modal choices
    public bool optional;                  // Can decline effect

    // Chaining
    public List<Effect>? additionalEffects; // Effects that follow
}
```

### Effect Resolution

**File:** `Effect.cs:102-278`

Resolution steps:

1. **Check Conditions** - Verify all conditions are met
2. **Determine Players** - Set source and affected players
3. **Calculate Amounts** - Resolve dynamic amounts (AmountBasedOn)
4. **Apply Modifiers** - Apply amount modifiers (+1, /2down)
5. **Create Qualifier** - Build card selection qualifier
6. **Execute Effect** - Switch on EffectType and execute
7. **Resolve Additional Effects** - Chain additional effects

### AmountBasedOn (Dynamic Amounts)

**File:** `LifeServer/Server/CardProperties/AmountBasedOn.cs`

```csharp
public enum AmountBasedOn {
    SummonsInGraveyard,      // Count summons in graveyard
    GoblinsInPlay,           // Count goblin tokens
    StonesInPlay,            // Count stone tokens
    TargetPower,             // Target's attack stat
    TargetCost,              // Target's cost
    SummonsOpponentControls, // Opponent's creature count
    SummonsThatDiedThisTurn, // Deaths this turn
    HerbsControlled,         // Herb token count
    StonesControlled,        // Stone token count
    PlantsControlled,        // Plant token count
    TreefolkControlled,      // Treefolk count
    X,                       // Card's X variable
    PlayerChoice,            // Player-selected amount
    LifeTotal,               // Player's life total
    DeckSize,                // Deck size
    Attack,                  // Source attack stat
    // ... and more
}
```

---

## 7. Triggered Abilities

### Trigger Types

**File:** `LifeServer/Server/CardProperties/Trigger.cs`

```csharp
public enum Trigger {
    Death,              // When a creature dies
    EnteredZone,        // When card enters a zone
    LeftZone,           // When card leaves a zone
    OpeningHand,        // In opening hand at game start
    Phase,              // Specific phase begins
    AttackedSummon,     // When a summon is attacked
    DeathBySpell,       // Died to spell damage
    DealDamageToPlayer, // When damage is dealt to player
    Draw,               // When a card is drawn
    Mill,               // When a card is milled
    Tribute,            // When a creature is tributed
    Attack,             // When declaring an attack
    Discard,            // When a card is discarded
    SurvivedCombat,     // When surviving combat damage
    Cast                // When a spell is cast
}
```

### TriggeredEffect Structure

**File:** `LifeServer/Server/CardProperties/TriggeredEffect.cs`

```csharp
public class TriggeredEffect {
    public Trigger trigger;                // Trigger condition
    public Zone? triggerZone = Zone.Play;  // Zone card must be in (default: Play)
    public bool self = true;               // Triggers on self events
    public bool optional = false;          // Player can skip
    public bool handTrigger = false;       // Can trigger from hand
    public Phase? phase;                   // Specific phase requirement
    public bool? isPlayerTurn;             // Only on player's turn
    public Zone? zone;                     // Zone filter
    public CardType? cardType;             // Card type filter
    public Tribe? tribe;                   // Tribe filter
    public List<Condition>? conditions;    // Additional conditions
    public List<AdditionalCost>? additionalCosts; // Costs to trigger
    public List<Effect> effects;           // Effects when triggered
    public List<Restriction>? restrictions; // Card restrictions
    public Card sourceCard;                // Source card (runtime)
}
```

### Trigger Processing

**File:** `GameMatch.cs:162-189`

```csharp
public void CheckForTriggersAndPassives(EventType eventType, Player? playerToPassTo = null) {
    Player turnPlayer = GetPlayerByTurn(true);
    Player nonTurnPlayer = GetPlayerByTurn(false);

    foreach (TriggerContext tc in triggersToCheck) {
        currentTriggerContext = tc;
        CheckForTriggersPlayer(tc, turnPlayer);
        CheckForTriggersPlayer(tc, nonTurnPlayer);
    }

    // Determine priority after triggers
    // Handle trigger ordering if multiple
    HandleTriggers(turnPlayer, playerToPassTo);
}
```

---

## 8. Activated Abilities

### ActivatedEffect Structure

**File:** `LifeServer/Server/CardProperties/ActivatedEffect.cs`

```csharp
public class ActivatedEffect {
    public CostType costType;              // Cost type (Sacrifice, Discard, etc.)
    public int amount;                     // Cost amount
    public bool playerChosenAmount;        // Player chooses cost amount
    public bool oncePerTurn;               // Limit to once per turn
    public bool self;                      // Can target self
    public CardType? cardType;             // Cost card type filter
    public TokenType? tokenType;           // Cost token type filter
    public Tribe? tribe;                   // Cost tribe filter
    public List<Condition>? conditions;    // Activation conditions
    public List<Restriction>? restrictions; // Cost restrictions
    public List<Effect> effects;           // Effects when activated
    public string? description;            // Custom description
    public Card sourceCard;                // Source card (runtime)
}
```

### Cost Availability Check

```csharp
public bool CostIsAvailable(GameMatch gameMatch, Player player) {
    int playerAmount = 0;
    Qualifier costQualifier = new Qualifier(this, player);

    switch (costType) {
        case CostType.Sacrifice:
            playerAmount += gameMatch.GetAllCardsControlled(player)
                .Count(c => gameMatch.QualifyCard(c, costQualifier));
            break;
        case CostType.Discard:
            playerAmount += player.allCardsPlayer
                .Count(c => gameMatch.QualifyCard(c, costQualifier));
            break;
    }

    if (playerChosenAmount && playerAmount > 0) return true;
    return playerAmount >= amount;
}
```

---

## 9. Passive Effects

### Passive Types

**File:** `LifeServer/Server/CardProperties/Passive.cs`

```csharp
public enum Passive {
    BypassSummonLimit,       // Ignore one-summon-per-turn rule
    ChangeStats,             // Modify attack/defense
    GrantKeyword,            // Give keyword to cards
    GrantActive,             // Give activated ability
    DisableKeyword,          // Remove keyword
    ModifyCost,              // Change casting cost
    CantTakeDamage,          // Damage immunity
    CantDealDamage,          // Cannot deal damage
    TopCardRevealed,         // Top deck card visible
    AdditionalSummonTopCard, // Can summon from top of deck
    CantBeTargeted,          // Hexproof
    ImmuneToKeyword,         // Immune to specific keyword
    DefenseUsedForAttack,    // Use defense as attack
    Sacrifice,               // Auto-sacrifice effect
    OnlyTributeToTreefolk,   // Tribute restriction
    TributeRestriction,      // Tribe-based tribute restriction
    SproutTriggersOnDeath,   // Sprout token on death
    ModifyKeywordAmount,     // Modify keyword value
    CantBeAttacked,          // Cannot be attacked
    DisableEnterPlayEffects, // Disable ETB effects
    CantSpecialSummon,       // No special summons
    TokenCanTribute,         // Tokens can tribute
    CreateTokenModifier,     // Modify token creation
    ThisTurn                 // Temporary (end of turn)
}
```

### PassiveEffect Structure

**File:** `LifeServer/Server/CardProperties/PassiveEffect.cs`

```csharp
public class PassiveEffect {
    public Passive passive;                // Passive type
    public List<Condition>? conditions;    // Conditions for active
    public int? cost;                      // Cost modifier value
    public int? amount;                    // Amount value
    public bool all;                       // Affects all qualifying
    public Keyword? keyword;               // Associated keyword
    public Zone? zone;                     // Zone applicability
    public List<StatModifier>? statModifiers; // Stat changes
    public TokenType? tokenType;           // Token filter
    public Tribe? tribe;                   // Tribe filter
    public List<Restriction>? restrictions; // Card restrictions
    public bool other;                     // Affects other cards
    public bool self = true;               // Affects self
    public bool thisTurn;                  // Temporary until end of turn
    public string? description;            // Custom description
}
```

---

## 10. Stack System

### StackObj Structure

**File:** `LifeServer/Server/StackObj.cs`

```csharp
public class StackObj {
    public Card sourceCard { get; set; }
    public StackObjType stackObjType;      // Spell, TriggeredEffect, ActivatedEffect
    public Zone sourceZone { get; set; }
    public Player player { get; set; }
    public List<Effect>? effects { get; set; }
    public string? customDescription;
}
```

### Stack Resolution

The stack uses Last-In-First-Out (LIFO) resolution:

1. When both players pass priority consecutively
2. Top object is popped from stack
3. Effects resolve in order
4. Halt for player input (targets, choices)
5. Continue with remaining effects
6. Move to graveyard (for spells)

### Stack Types

```csharp
public enum StackObjType {
    Spell,            // Cast spell
    TriggeredEffect,  // Triggered ability
    ActivatedEffect   // Activated ability
}
```

---

## 11. Combat System

### Combat Flow

```
Combat Phase Start
        │
        ▼
┌───────────────────┐
│ Calculate         │
│ Attack-Capable    │ → Cards without summoning sickness
│ Creatures         │
└────────┬──────────┘
         │
         ▼
┌───────────────────┐
│ Declare           │
│ Attackers         │ → Player selects attacking creatures
└────────┬──────────┘
         │
         ▼
┌───────────────────┐
│ Assign            │
│ Attack Targets    │ → Map attackers to defenders/player
└────────┬──────────┘
         │
         ▼
┌───────────────────┐
│ Priority Window   │ → Both players can respond
└────────┬──────────┘
         │
         ▼
┌───────────────────┐
│ Resolve Attacks   │ → Deal damage both ways
└────────┬──────────┘
         │
         ▼
┌───────────────────┐
│ Check Deaths      │ → Destroy creatures at 0 defense
└───────────────────┘
```

### Attack Assignment

**File:** `GameMatch.cs:1792-1803`

```csharp
public void AssignAttack(Player attackingPlayer, (int, int) attackUids) {
    // attackUids.Item1 = attacker UID
    // attackUids.Item2 = target UID (creature or player)
    currentAttackUids.Add(attackUids.Item1, attackUids.Item2);
    GameEvent gEvent = GameEvent.CreateAttackEvent(attackUids, true);
    AddEventForOpponent(attackingPlayer, gEvent);
}
```

### Valid Attack Targets

**File:** `GameMatch.cs:870-903`

A creature can attack:
- Enemy creatures (unless `Spectral`)
- Enemy player (if `Dive` keyword OR all enemy summons being attacked)

### Damage Resolution

**File:** `GameMatch.cs:1270-1289`

```csharp
private void ResolveAttacks() {
    foreach (var pair in currentAttackUids) {
        Card attackingCard = cardByUid[pair.Key];
        int attackValue = attackingCard.GetAttack();
        int retaliationValue = /* defender's attack or 0 for player */;

        // Deal damage both ways
        DealDamage(pair.Value, attackValue);      // Attacker -> Defender
        DealDamage(attackingCard.uid, retaliationValue); // Defender -> Attacker
    }

    // Check for SurvivedCombat triggers
    CheckForDeaths();
}
```

---

## 12. Cost System

### Cost Types

**File:** `LifeServer/Server/CardProperties/CostType.cs`

```csharp
public enum CostType {
    Stones,                    // Pay stone tokens
    Sacrifice,                 // Sacrifice creatures
    Tribute,                   // Summon cost (sacrifice creatures)
    ExileFromHand,             // Exile cards from hand
    ExileFromGraveyard,        // Exile cards from graveyard
    Discard,                   // Discard cards
    Exile,                     // Exile from play
    LoseLife,                  // Pay life
    DiscardOrSacrificeMerfolk, // Choice cost
    Reveal,                    // Reveal cards
    Life                       // Life payment
}
```

### Spell Costs

Spells cost life to cast:
- Cost is paid from player's life total
- Cannot cast if life <= cost

### Summon Costs (Tribute)

Summons require tributing creatures:
- Cost = number of creatures to sacrifice
- Must have enough creatures on battlefield
- Tributed creatures are destroyed

### Spell Burn Mechanic

**File:** `GameMatch.cs`

When a player casts multiple spells in one turn:
- Second spell costs double (original cost added again)
- `spellBurnt` flag tracks this state
- Resets at end of turn

---

## 13. Targeting System

### Target Types

```csharp
public enum TargetType {
    Player,   // Target a player
    Any,      // Target player or creature
    Token,    // Target tokens only
    Summon    // Target creatures only
}
```

### Target Selection Flow

1. **Calculate Valid Targets** - Based on TargetType and qualifiers
2. **Send Selection Event** - Client receives targetable UIDs
3. **Player Selects** - Client sends chosen UIDs
4. **Validate & Assign** - Server validates and stores targets
5. **Continue Resolution** - Effect resolves with targets

### Target Qualification

**File:** `GameMatch.cs:302-318`

```csharp
private bool QualifyTarget(int uid, TargetType targetType) {
    bool targetIsPlayer = playerOne.uid == uid || playerTwo.uid == uid;

    switch (targetType) {
        case TargetType.Player:
            return targetIsPlayer;
        case TargetType.Any:
            return targetIsPlayer || GetAllSummonsInPlay().Contains(cardByUid[uid]);
        case TargetType.Token:
            if (targetIsPlayer) return false;
            return playerOne.tokens.Concat(playerTwo.tokens).Contains(cardByUid[uid]);
        case TargetType.Summon:
            return !targetIsPlayer && GetAllSummonsInPlay().Contains(cardByUid[uid]);
    }
}
```

---

## 14. Token System

### Token Types

**File:** `LifeServer/Server/TokenType.cs`

```csharp
public enum TokenType {
    Stone,     // Resource token (Golem tribe)
    Herb,      // Resource token (Treefolk tribe)
    Golem,     // Creature token
    Goblin,    // Creature token
    Merfolk,   // Creature token
    Plant,     // Creature token
    Ghost,     // Creature token (Shadow tribe)
    Treefolk   // Creature token
}
```

### Token Categories

| Category | Types | Card Type |
|----------|-------|-----------|
| **Resource** | Stone, Herb | Token |
| **Creature** | Golem, Goblin, Merfolk, Plant, Ghost, Treefolk | Summon |

### Token-to-Tribe Mapping

**File:** `LifeServer/Server/Token.cs:10-19`

```csharp
private static Dictionary<TokenType, Tribe> tokenToTribe = new() {
    { TokenType.Ghost, Tribe.Shadow },
    { TokenType.Goblin, Tribe.Goblin },
    { TokenType.Golem, Tribe.Golem },
    { TokenType.Herb, Tribe.Treefolk },
    { TokenType.Merfolk, Tribe.Merfolk },
    { TokenType.Plant, Tribe.Treefolk },
    { TokenType.Stone, Tribe.Golem },
    { TokenType.Treefolk, Tribe.Treefolk }
};
```

### Token Creation

**File:** `Effect.cs:378-406`

```csharp
private void CreateToken(GameMatch gameMatch, Player affectedPlayer) {
    for (int i = 0; i < amount; i++) {
        Token newToken = new Token(tokenType, gameMatch);

        // Add passives if specified
        if (tokenPassive != null) {
            newToken.passiveEffects = new List<PassiveEffect>();
            // ... add passive
        }

        // Set stats for creature tokens
        if (attack != null) {
            newToken.type = CardType.Summon;
            newToken.attack = attack;
            newToken.defense = defense;
        }

        // Grant keywords
        if (keyword != null) {
            newToken.keywords = new List<Keyword> { (Keyword)keyword };
        }

        gameMatch.CreateTokenForPlayer(affectedPlayer, newToken, attacking);
    }
}
```

---

## 15. Win/Loss Conditions

### Victory Condition

**File:** `GameMatch.cs:1305-1311`

```csharp
if (playerOne.lifeTotal <= 0 || playerTwo.lifeTotal <= 0) {
    Console.WriteLine("GAME OVER");
    Player winningPlayer = playerOne.lifeTotal > playerTwo.lifeTotal
        ? playerOne : playerTwo;
    GameEvent gEvent = GameEvent.CreateEndGameEvent(winningPlayer.uid);
    AddEventForBothPlayers(winningPlayer, gEvent);
}
```

### Win Conditions

1. **Life Total** - Reduce opponent to 0 or less life
2. **Concession** - Opponent concedes

### Death Checking

**File:** `GameMatch.cs:1291-1311`

Called after:
- Combat damage resolution
- Direct damage effects
- Life loss effects

---

## 16. Card JSON Examples

Cards are stored as JSON files in: `C:\Users\bango\OneDrive\Desktop\MetaJet Games\Lifeburn\Data\Cards\`

### Example: Simple Summon Card

```json
{
  "id": 42,
  "name": "Fire Goblin",
  "cost": 1,
  "type": "Summon",
  "attack": 2,
  "defense": 1,
  "tribe": "Goblin",
  "rarity": "Common",
  "description": "A basic goblin creature."
}
```

### Example: Summon with Triggered Ability

```json
{
  "id": 55,
  "name": "Stone Sentinel",
  "cost": 2,
  "type": "Summon",
  "attack": 2,
  "defense": 3,
  "tribe": "Golem",
  "rarity": "Uncommon",
  "keywords": ["Taunt"],
  "description": "Taunt\nWhen Stone Sentinel enters play, create 1 Stone token.",
  "triggeredEffects": [
    {
      "trigger": "EnteredZone",
      "zone": "Play",
      "self": true,
      "effects": [
        {
          "effect": "CreateToken",
          "tokenType": "Stone",
          "amount": 1
        }
      ]
    }
  ]
}
```

### Example: Spell with Targeting

```json
{
  "id": 100,
  "name": "Lightning Strike",
  "cost": 2,
  "type": "Spell",
  "tribe": "Shadow",
  "rarity": "Common",
  "description": "Deal 3 damage to any target.",
  "stackEffects": [
    {
      "effect": "DealDamage",
      "amount": 3,
      "targetType": "Any"
    }
  ]
}
```

### Example: Card with Choice Effect

```json
{
  "id": 150,
  "name": "Nature's Gift",
  "cost": 1,
  "type": "Spell",
  "tribe": "Treefolk",
  "rarity": "Uncommon",
  "description": "{c0}Draw 2 cards.\n{c0}Gain 4 life.",
  "stackEffects": [
    {
      "effect": "Choose",
      "choices": [
        [{ "effect": "Draw", "amount": 2 }],
        [{ "effect": "GainLife", "amount": 4 }]
      ]
    }
  ]
}
```

### Example: Summon with Activated Ability

```json
{
  "id": 180,
  "name": "Soul Harvester",
  "cost": 3,
  "type": "Summon",
  "attack": 2,
  "defense": 2,
  "tribe": "Shadow",
  "rarity": "Rare",
  "description": "Sacrifice a creature: Draw a card.",
  "activatedEffects": [
    {
      "costType": "Sacrifice",
      "cardType": "Summon",
      "amount": 1,
      "effects": [
        {
          "effect": "Draw",
          "amount": 1
        }
      ]
    }
  ]
}
```

### Example: Card with Passive Effect

```json
{
  "id": 200,
  "name": "War Chief",
  "cost": 4,
  "type": "Summon",
  "attack": 3,
  "defense": 3,
  "tribe": "Goblin",
  "rarity": "Rare",
  "keywords": ["Blitz"],
  "description": "Blitz\nOther Goblins you control get +1/+1.",
  "passiveEffects": [
    {
      "passive": "ChangeStats",
      "tribe": "Goblin",
      "other": true,
      "self": false,
      "statModifiers": [
        { "statType": "Attack", "operatorType": "Add", "amount": 1 },
        { "statType": "Defense", "operatorType": "Add", "amount": 1 }
      ]
    }
  ]
}
```

### Example: Complex Card with Multiple Effects

```json
{
  "id": 250,
  "name": "Ancient Treefolk",
  "cost": 5,
  "type": "Summon",
  "attack": 4,
  "defense": 6,
  "tribe": "Treefolk",
  "rarity": "Rare",
  "keywords": ["Sprout"],
  "description": "Sprout 2\nWhen Ancient Treefolk enters play, create 2 Plant tokens.\nAt the beginning of your end phase, gain 1 life for each Plant you control.",
  "triggeredEffects": [
    {
      "trigger": "EnteredZone",
      "zone": "Play",
      "self": true,
      "effects": [
        {
          "effect": "CreateToken",
          "tokenType": "Plant",
          "amount": 2,
          "attack": 1,
          "defense": 1
        }
      ]
    },
    {
      "trigger": "Phase",
      "phase": "End",
      "isPlayerTurn": true,
      "effects": [
        {
          "effect": "GainLife",
          "amountBasedOn": "PlantsControlled"
        }
      ]
    }
  ]
}
```

---

## File Reference Map

| System | File Path | Key Lines |
|--------|-----------|-----------|
| **Game Match** | `Server/GameMatch.cs` | 1-2000+ |
| **Card Definition** | `Server/CardDto.cs` | 1-23 |
| **Card Instance** | `Server/Card.cs` | 1-300+ |
| **Effect Base** | `Server/CardProperties/Effect.cs` | 1-627 |
| **Effect Types** | `Server/CardProperties/EffectType.cs` | 1-57 |
| **Triggered Effects** | `Server/CardProperties/TriggeredEffect.cs` | 1-29 |
| **Activated Effects** | `Server/CardProperties/ActivatedEffect.cs` | 1-49 |
| **Passive Effects** | `Server/CardProperties/PassiveEffect.cs` | 1-60 |
| **Triggers** | `Server/CardProperties/Trigger.cs` | 1-19 |
| **Passives** | `Server/CardProperties/Passive.cs` | 1-28 |
| **Keywords** | `Server/Keyword.cs` | 1-13 |
| **Phases** | `Server/Phase.cs` | 1-9 |
| **Zones** | `Server/CardProperties/Zone.cs` | 1-10 |
| **Cost Types** | `Server/CardProperties/CostType.cs` | 1-15 |
| **Token Types** | `Server/TokenType.cs` | 1-12 |
| **Token Class** | `Server/Token.cs` | 1-54 |
| **Stack Object** | `Server/StackObj.cs` | 1-91 |
| **Playability** | `Server/Utils.cs` | 23-102 |
| **Card Loading** | `Server/Utils.cs` | 13-21 |

---

## Database Schema Reference

The SQLite database (`life.sqlite`) stores user data, not card definitions:

| Table | Purpose |
|-------|---------|
| `accounts` | User accounts (id, username, password hash, email) |
| `decks` | Deck definitions (id, deck_name) |
| `account_decks` | User-to-deck mapping |
| `deck_cards` | Cards in each deck (deck_id, card_id) |
| `account_cards` | User's card collection |

Card definitions are loaded from JSON files at runtime.
