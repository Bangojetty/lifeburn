using System.ComponentModel;
using System.Data.SQLite;
using System.Diagnostics;
using Server.CardProperties;

namespace Server;

public class GameMatch {
    public int matchId { get; set; }

    public Dictionary<int, Player> accountIdToPlayer { get; }
    public Player playerOne { get; }
    public Player playerTwo { get; }
    public int prioPlayerId { get; set; }
    public int turnPlayerId { get; set; }
    public Phase currentPhase;

    public int turn = 1;
    public Stack<StackObj> stack { get; set; }
    public bool allAttackersAssigned { get; set; }

    // Ground Tactics: when turn player has GroundTactics passive, this player controls their attack assignments
    public int? groundTacticsControllerId;

    public Object mdLock = new();

    public int uidCounter;
    public Dictionary<int, Card> cardByUid = new();

    // general
    private List<Card> allCardsInPlay = new();

    // prio
    private Player? currentPlayerToPassTo;

    // triggers
    public TriggerContext? currentTriggerContext;
    public List<TriggerContext> triggersToCheck = new();

    // death tracking (for CastAMold and similar effects)
    private int summonsThatDiedThisTurn = 0;

    // exiled cards awaiting return at draw phase (maps playerId -> list of exiled cards to return on that player's draw phase)
    private Dictionary<int, List<Card>> exiledCardsAwaitingReturn = new();

    // Delayed zone effects (for cards like Endless Garden that return at a specific phase)
    // Key: playerId, Value: list of (card, destination zone, phase)
    private List<(Card card, Zone destination, Phase phase, int playerId)> delayedZoneEffects = new();

    public List<Phase> phasesToPauseOn = new();
    public bool secondPass;
    private bool waitingForHandSizeDiscard;
    private bool endTurnPending; // Flag for EndTurn effect (Typhoon) - handled after stack resolves

    // Game over tracking
    public bool isGameOver { get; private set; }
    public DateTime? gameOverAt { get; private set; }  // When the game ended (for delayed cleanup)
    public int? winnerId { get; private set; }      // Player ID of the winner of this game
    public int? loserId { get; private set; }       // Player ID of the loser of this game

    // Series tracking (for best-of matches)
    public int bestOf { get; set; } = 1;            // 1, 3, or 5
    public int playerOneSeriesWins { get; set; }    // Games won by player one in series
    public int playerTwoSeriesWins { get; set; }    // Games won by player two in series
    public bool isSeriesOver { get; private set; }  // True when someone wins the series
    public int? seriesWinnerId { get; private set; } // Player ID of series winner

    // look at deck effect (temporary stored data while client chooses a card)
    public StackObj? unresolvedStackObj;
    public int unresolvedEffectIndex;

    // Effect waiting for amount selection (e.g., mill up to N)
    public Effect? effectWaitingForAmount;

    // Effect waiting for upfront repeat amount selection (e.g., Burst Lightning)
    public Effect? effectWaitingForRepeatAmount;

    public List<DeckDestination> lookedAtSelectionDestinations = new();
    public List<Card> cardsBeingLookedAt = new();

    // attacking
    public Dictionary<int, int> currentAttackUids = new();
    public int requiredAttackTargets;

    // targeting
    private List<Effect> effectsWithTargets = new();

    // each player chooses (e.g., Return - each player returns a summon)
    public Effect? eachPlayerEffect;
    public Dictionary<int, List<int>> eachPlayerSelections = new();  // playerId -> selected uids
    public HashSet<int> pendingEachPlayerResponses = new();

    // player choice discard at resolve time (for discard effects with amountBasedOn: playerChoice)
    public Effect? playerChoiceDiscardEffect;

    // cost effect selection at resolve time (for isCost effects that need user selection)
    public Effect? costEffectForSelection;

    // player choice cast at resolve time (for castCard effects with targetZones and amountBasedOn: playerChoice)
    public Effect? playerChoiceCastEffect;

    // focus cards
    private Card? cardBeingCast;
    private Card? cardWaitingForX;
    
    private ActivatedEffect? currentActivatedEffect;

    // optional triggers and effects
    private List<TriggeredEffect> optionalTriggers = new();
    private Player? optionalTriggerController;  // The player who controls the optional triggers (may differ from who chooses)
    private Effect? currentOptionalEffect;
    
    // additional costs
    private int cardAdditionalCostAmount;

    // effect choices
    private Dictionary<List<Effect>, Effect> choiceEffects = new();
    private Dictionary<List<Effect>, Effect> additionalChoiceEffects = new();
    private Card? choiceCard;
    private ActivatedEffect? choiceActivatedEffect;
    private List<int>? currentValidChoiceIndices;
    // multi-choice tracking (for "Choose Two" style effects)
    private int remainingChoices;
    private List<int> selectedChoiceIndices = new();
    private bool currentForOpponentChoice; // tracks if current choice is presented to opponent
    private bool pendingChoiceTargeting; // tracks if we're waiting for targets after a choice selection
    private Player? pendingChoicePlayer; // player who is making choices

    // Ritual of Darkness state
    private bool inRitualOfDarkness;
    private Player? ritualCurrentPlayer;  // whose turn it is to choose
    private bool ritualLastPlayerPassed;  // did the previous player pass?
    private Player? ritualCaster;  // who cast the spell (starts first)
    private Dictionary<List<Effect>, Effect>? pendingChoiceEffectDict; // choice effect dict for resuming
    private CastingStage pendingChoiceCastingStage; // casting stage to use after choices complete

    // Repeat effect state
    private bool inRepeatChoice;
    private Effect? repeatEffect;  // the effect being repeated
    private Player? repeatPlayer;  // the player who can choose to repeat
    private List<int>? repeatTargetUids;  // original target(s) of the effect

    // tributing
    private Card? cardRequiringTribute;

    // alternate costs (for card casting)
    private AlternateCost? currentAlternateCost;
    private bool usingAlternateCost;

    // alternate costs (for activated abilities)
    private bool pendingActivatedAbilityAltCostChoice;
    private AlternateCost? currentActivatedAbilityAltCost;

    // hand ability choice (for cards with activateFromHand abilities like Transparent Plant)
    private bool pendingHandAbilityChoice;
    private ActivatedEffect? currentHandAbilityEffect;

    // phase skipping
    private Phase? skipStartPhase;  // tracks where consecutive skipping started from
    public bool isAutoSkipping;    // true when we're in the middle of auto-skipping phases

    // Ghost Deceiver pre-trigger (hardcoded special case)
    private Card? ghostDeceiverPendingCard;       // the shadow summon entering graveyard
    private Player? ghostDeceiverPendingPlayer;   // the player whose graveyard it's entering
    private Zone? ghostDeceiverPendingSourceZone; // the actual source zone (before modification)
    private Player? ghostDeceiverOwner;           // the player who controls Ghost Deceiver
    private int ghostDeceiverStage;               // 0 = not active, 1 = waiting for yes/no, 2 = waiting for zone selection
    private List<Card>? ghostDeceiverRemainingDiscards; // remaining cards to discard after Ghost Deceiver resolves
    private Player? ghostDeceiverDiscardPlayer;   // player who was discarding when Ghost Deceiver triggered
    private bool ghostDeceiverWasHandSizeDiscard; // true if Ghost Deceiver interrupted hand size discard
    private int skipStartEventIndexP1;  // tracks event list index for player 1 when skip started
    private int skipStartEventIndexP2;  // tracks event list index for player 2 when skip started

    // detained cards - maps detaining card UID to list of (detained card, original owner)
    private Dictionary<int, List<(Card card, Player owner)>> detainedCards = new();

    // static data
    private readonly List<Passive> handPassiveTypes = new() {
        Passive.ModifyCost
    };

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

    public void InitializeMatch() {
        if (playerOne.deck == null) {
            Console.WriteLine("Player One is missing deck");
            return;
        }

        if (playerTwo.deck == null) {
            Console.WriteLine("Player Two is missing deck");
            return;
        }

        currentPhase = Phase.Draw;
        ShuffleDeck(playerOne.deck);
        ShuffleDeck(playerTwo.deck);
        SetOwnedCards();
        SetUids();
        SetFirstPlayer();
        DrawOpeningHands();
        SpawnBotTestSummons();
        triggersToCheck.Add(new TriggerContext(Trigger.OpeningHand));
        CheckForTriggersAndPassives(EventType.GainPrio);
    }

    private void SetOwnedCards() {
        Debug.Assert(playerOne.deck != null, "Player One has no deck");
        Debug.Assert(playerTwo.deck != null, "Player Two has no deck");
        foreach (Card c in playerOne.deck) {
            playerOne.ownedCards.Add(c);
        }

        foreach (Card c in playerTwo.deck) {
            playerTwo.ownedCards.Add(c);
        }
    }

    private void SetUids() {
        Debug.Assert(playerOne.deck != null, "playerOne deck is null");
        Debug.Assert(playerTwo.deck != null, "playerTwo deck is null");
        playerOne.uid = GetNextUid();
        playerTwo.uid = GetNextUid();
        foreach (Card c in playerOne.deck) {
            cardByUid.Add(c.uid, c);
            c.currentZone = Zone.Deck;
        }

        foreach (Card c in playerTwo.deck) {
            cardByUid.Add(c.uid, c);
            c.currentZone = Zone.Deck;
        }
    }

    private bool DetectPassive(Card card, Passive passive) {
        if (card.GetPassives().Count == 0) {
            return false;
        }
        if (card.GetPassives().All(passiveEffect => passiveEffect.passive != passive)) {
            return false;
        }
        Player controller = allCardsInPlay.Contains(card) ? GetControllerOf(card) : GetOwnerOf(card);
        foreach (var pEffect in card.GetPassives().Where(pEffect => pEffect.passive == passive)) {
            if (pEffect.conditions == null) {
                continue;
            }
            foreach (var c in pEffect.conditions) {
                bool verified = c.Verify(this, controller);
                if (!verified) {
                    return false;
                }
            }
        }

        return true;
    }

    private bool DetectKeyword(Card card, Keyword keyword) {
        // Check if card has a DisableKeyword passive (either disabling all keywords or this specific keyword)
        foreach (PassiveEffect pEffect in card.grantedPassives) {
            if (pEffect.passive == Passive.DisableKeyword) {
                // If no specific keyword is set or it matches the keyword being checked, it's disabled
                if (pEffect.keyword == null || pEffect.keyword == keyword) return false;
            }
        }
        // Check innate keywords
        if (card.keywords != null && card.keywords.Any(cardKeyword => keyword == cardKeyword)) {
            return true;
        }
        // Check granted keywords (from spells/abilities)
        foreach (PassiveEffect pEffect in card.grantedPassives) {
            if (pEffect.passive == Passive.GrantKeyword && pEffect.keyword == keyword) {
                return true;
            }
        }
        return false;
    }

    public void CheckForTriggersAndPassives(EventType eventType, Player? playerToPassTo = null) {
        // Halt all trigger processing if Ghost Deceiver is waiting for input
        if (ghostDeceiverStage > 0) return;

        foreach (TriggerContext tc in triggersToCheck) {
            Console.WriteLine($"  - TriggerContext: trigger={tc.trigger}, zone={tc.zone}, card={tc.card?.name ?? "null"}");
        }

        Player turnPlayer = GetPlayerByTurn(true);
        Player nonTurnPlayer = GetPlayerByTurn(false);
        foreach (TriggerContext tc in triggersToCheck) {
            currentTriggerContext = tc;
            CheckForTriggersPlayer(tc, turnPlayer);
            CheckForTriggersPlayer(tc, nonTurnPlayer);
        }

        foreach (TriggeredEffect te in turnPlayer.controlledTriggers) {
            Console.WriteLine($"  - TurnPlayer trigger: {te.sourceCard?.name}, trigger={te.trigger}");
        }
        foreach (TriggeredEffect te in nonTurnPlayer.controlledTriggers) {
            Console.WriteLine($"  - NonTurnPlayer trigger: {te.sourceCard?.name}, trigger={te.trigger}");
        }

        bool areTriggers = (turnPlayer.controlledTriggers.Count > 1 || nonTurnPlayer.controlledTriggers.Count > 1);
        switch (eventType) {
            case EventType.Attack:
                Debug.Assert(playerToPassTo != null, "there is no player associated with this attack");
                playerToPassTo = areTriggers ? turnPlayer : GetOpponent(playerToPassTo);
                break;
            case EventType.Cast:
                break;
            default:
                playerToPassTo = turnPlayer;
                break;
        }

        triggersToCheck.Clear();
        Debug.Assert(playerToPassTo != null,
            "Switch statement failure -> playerToPassTo must be set or passed in initially");
        HandleTriggers(turnPlayer, playerToPassTo);
    }

    /// <summary>
    /// checks for, applies, and refresh all passives in play.
    /// Only applies innate passives (passiveEffects), not granted passives (which are already applied).
    /// </summary>
    public void CheckForPassives() {
        foreach (Card c in allCardsInPlay) {
            // no innate passives
            if (c.passiveEffects == null || c.passiveEffects.Count == 0) continue;
            // apply innate passives only (not grantedPassives - those are already applied)
            foreach (PassiveEffect pEffect in c.passiveEffects) ApplyPassive(c, pEffect);
        }

        CheckForPassivesInHand(playerOne);
        CheckForPassivesInHand(playerTwo);

        CheckForPassivesInGraveyard(playerOne);
        CheckForPassivesInGraveyard(playerTwo);

        // Check for Passive.Sacrifice with conditions (e.g., Entangle: "If you control no Treefolk, sacrifice")
        CheckForConditionalSacrifice();

        // refresh all passives in all cards in non-deck zones
        RefreshPassives();
    }

    /// <summary>
    /// Checks for cards with Passive.Sacrifice that have conditions met, and sacrifices them.
    /// </summary>
    private void CheckForConditionalSacrifice() {
        List<Card> cardsToSacrifice = new();
        foreach (Card c in allCardsInPlay.ToList()) {
            if (c.passiveEffects == null) continue;
            foreach (PassiveEffect pEffect in c.passiveEffects) {
                if (pEffect.passive != Passive.Sacrifice) continue;
                if (pEffect.conditions == null) continue;
                // Check if all conditions are met (meaning sacrifice should happen)
                Player controller = GetControllerOf(c);
                if (pEffect.conditions.All(cond => cond.Verify(this, controller, null, c))) {
                    cardsToSacrifice.Add(c);
                    Console.WriteLine($"[ConditionalSacrifice] {c.name} meets sacrifice conditions");
                    break;  // Only need to check one sacrifice passive per card
                }
            }
        }
        foreach (Card c in cardsToSacrifice) {
            Kill(c);  // Sacrifice uses the same kill logic to send to graveyard
        }
    }

    private void CheckForPassivesInHand(Player player) {
        foreach (Card c in player.hand) {
            if (c.passiveEffects == null || c.passiveEffects.Count == 0) continue;
            foreach (PassiveEffect pEffect in c.passiveEffects.Where(p => handPassiveTypes.Contains(p.passive))) {
                ApplyPassive(c, pEffect, true);
            }
        }
    }

    private void CheckForPassivesInGraveyard(Player player) {
        foreach (Card c in player.graveyard) {
            if (c.passiveEffects == null || c.passiveEffects.Count == 0) continue;
            // Only apply passives that have an explicit inZone: graveyard condition (e.g., Shadow Lord)
            foreach (PassiveEffect pEffect in c.passiveEffects) {
                if (pEffect.conditions == null) continue;
                bool hasGraveyardCondition = pEffect.conditions.Any(cond =>
                    cond.condition == ConditionType.InZone && cond.zone == Zone.Graveyard);
                if (!hasGraveyardCondition) continue;
                ApplyPassive(c, pEffect, true);  // inHand=true to use GetOwnerOf instead of GetControllerOf
            }
        }
    }

    /// <summary>
    /// Applies the passive to any cards that qualify who aren't already affected.
    /// Clones the passive for each target to ensure proper tracking.
    /// </summary>
    /// <param name="sourceCard">The card that has this aura passive</param>
    /// <param name="pEffect">The passive effect to apply</param>
    /// <param name="inHand">Whether the source card is in hand</param>
    private void ApplyPassive(Card sourceCard, PassiveEffect pEffect, bool inHand = false) {
        Player playerToQualify = inHand ? GetOwnerOf(sourceCard) : GetControllerOf(sourceCard);
        Qualifier pQualifier = new Qualifier(pEffect, playerToQualify);
        // For innate passives, sourceCard isn't set on the passive, so set it on the qualifier
        if (pQualifier.sourceCard == null) pQualifier.sourceCard = sourceCard;
        // Apply to cards in play
        foreach (Card c in allCardsInPlay) {
            if (!QualifyCard(c, pQualifier)) continue;
            // Skip if this is the source card and passive is already in its passiveEffects (innate passive)
            if (c == sourceCard && sourceCard.passiveEffects != null && sourceCard.passiveEffects.Contains(pEffect)) continue;
            // Skip if already has a passive from this source with same type
            if (HasPassiveFromSource(c, sourceCard, pEffect.passive)) continue;
            // Clone and apply the passive
            ApplyClonedPassive(c, sourceCard, pEffect);
        }
        // Apply to tokens (for passives targeting tokenType like GrantActive)
        ApplyPassiveToTokens(playerToQualify, pQualifier, pEffect, sourceCard);
        // Apply to hand cards
        ApplyPassiveToHandCards(playerToQualify, pQualifier, pEffect, sourceCard);
    }

    private void ApplyPassiveToTokens(Player player, Qualifier pQualifier, PassiveEffect pEffect, Card sourceCard) {
        // Only apply if this passive could target tokens (has tokenType or tribe set)
        if (pEffect.tokenType == null && pEffect.tribe == null) return;
        foreach (Token token in player.tokens) {
            if (!QualifyCard(token, pQualifier)) continue;
            if (HasPassiveFromSource(token, sourceCard, pEffect.passive)) continue;
            ApplyClonedPassive(token, sourceCard, pEffect);
        }
    }

    private void ApplyPassiveToHandCards(Player player, Qualifier pQualifier, PassiveEffect pEffect, Card sourceCard) {
        if (!handPassiveTypes.Contains(pEffect.passive)) return;
        foreach (Card c in player.hand) {
            if (!QualifyCard(c, pQualifier)) continue;
            // Skip the source card's own innate passive - its passiveEffects entry already provides
            // it with live condition evaluation; a clone would strip the conditions and stick forever
            if (c == sourceCard && sourceCard.passiveEffects != null && sourceCard.passiveEffects.Contains(pEffect)) continue;
            if (HasPassiveFromSource(c, sourceCard, pEffect.passive)) continue;
            ApplyClonedPassive(c, sourceCard, pEffect);
        }
    }

    /// <summary>
    /// Clones a passive and applies it to the target card with proper tracking.
    /// Also handles special passives like GrantActive.
    /// </summary>
    private void ApplyClonedPassive(Card target, Card source, PassiveEffect pEffect) {
        PassiveEffect clonedPassive = pEffect.Clone();
        clonedPassive.grantedBy = source;
        clonedPassive.owner = target;
        // Clear conditions - they were verified on the source, not needed on the target
        // (e.g., Shadow Lord's "inZone: graveyard" applies to Shadow Lord, not to the Ghost receiving the buff)
        clonedPassive.conditions = null;
        target.grantedPassives.Add(clonedPassive);

        // Handle GrantActive: clone and add activated effects to the target
        if (pEffect.passive == Passive.GrantActive && pEffect.actives != null) {
            foreach (ActivatedEffect aEffect in pEffect.actives) {
                ActivatedEffect clonedActive = aEffect.Clone();
                clonedActive.sourceCard = target;  // The token will be the source when activated
                clonedActive.grantedBy = source;   // Track who granted it for cleanup
                target.grantedActivatedEffects.Add(clonedActive);
            }
        }
    }

    /// <summary>
    /// Checks if a card already has a passive of the given type from the given source.
    /// </summary>
    private bool HasPassiveFromSource(Card target, Card source, Passive passiveType) {
        return target.grantedPassives.Any(p => p.grantedBy == source && p.passive == passiveType);
    }


    /// <summary>
    /// Refreshes all affecting passives in play to reflect the current game state.
    /// </summary>
    private void RefreshPassives() {
        RefreshCardsPlayer(playerOne);
        RefreshCardsPlayer(playerTwo);
    }

    private void RefreshCardsPlayer(Player player) {
        List<CardDisplayData> cardsToRefresh = allCardsInPlay.Select(c => new CardDisplayData(c)).ToList();
        List<CardDisplayData> playerCardsToRefresh = cardsToRefresh.Concat(player.hand.Select(c => new CardDisplayData(c))).ToList();
        GameEvent gEvent = GameEvent.CreateRefreshCardDisplayEvent(null, playerCardsToRefresh);
        AddEventForPlayer(player, gEvent);
    }

    /// <summary>
    /// Removes all passives and granted activated effects from the given source card.
    /// Called when a card with auras leaves play.
    /// </summary>
    private void RemovePassivesFromSource(Card sourceCard) {
        // Remove from cards in play
        foreach (Card affectedCard in allCardsInPlay) {
            affectedCard.grantedPassives.RemoveAll(p => p.grantedBy == sourceCard);
            affectedCard.grantedActivatedEffects.RemoveAll(a => a.grantedBy == sourceCard);
        }
        // Remove from cards in hand
        foreach (Card handCard in playerOne.hand) {
            handCard.grantedPassives.RemoveAll(p => p.grantedBy == sourceCard);
        }
        foreach (Card handCard in playerTwo.hand) {
            handCard.grantedPassives.RemoveAll(p => p.grantedBy == sourceCard);
        }
        // Remove from tokens
        foreach (Token token in playerOne.tokens) {
            token.grantedPassives.RemoveAll(p => p.grantedBy == sourceCard);
            token.grantedActivatedEffects.RemoveAll(a => a.grantedBy == sourceCard);
        }
        foreach (Token token in playerTwo.tokens) {
            token.grantedPassives.RemoveAll(p => p.grantedBy == sourceCard);
            token.grantedActivatedEffects.RemoveAll(a => a.grantedBy == sourceCard);
        }
    }

    public bool QualifyCard(Card c, Qualifier q) {
        if (q.conditions != null) {
            // Use GetOwnerOf for cards not in play (graveyard, hand, etc.)
            Player cardPlayer = c.currentZone == Zone.Play ? GetControllerOf(c) : GetOwnerOf(c);
            if (q.conditions.Any(condition => !condition.Verify(this, cardPlayer, null, q.sourceCard))) {
                return false;
            }
        }
        // check if it already has the passive you are qualifying for (no need to grant or apply it if so)
        if (q.passive != null) {
            if (c.grantedPassives.Contains(q.passive)) {
                return false;
            }
        }
        // Apply scope filtering
        if (q.sourceCard != null) {
            bool isSameCard = c.Equals(q.sourceCard);
            switch (q.scope) {
                case Scope.SelfOnly:
                    if (!isSameCard) return false;
                    break;
                case Scope.OthersOnly:
                    if (isSameCard) return false;
                    break;
                case Scope.All:
                    // No filtering needed
                    break;
            }
        }
        // tribe check
        if (q.tribe != null && c.tribe != q.tribe) return false;
        // cardtype check
        // When checking for CardType.Token, also match any Token class instance (token summons)
        if (q.cardType != null) {
            if (q.cardType == CardType.Token) {
                // Match both CardType.Token AND any Token class instance
                if (c.type != CardType.Token && c is not Token) return false;
            } else {
                if (c.type != q.cardType) return false;
            }
        }
        // verify restrictions
        if (q.restrictions != null) {
            foreach (var restriction in q.restrictions) {
                if (!QualifyRestriction(c, restriction, q.sourcePlayer)) return false;
            }
        }
        // tokentype check
        if (q.tokenType != null) {
            if (c is not Token t) return false;
            if (q.tokenType != t.tokenType) return false;
        }

        // card qualifies
        return true;
    }

    /// <summary>
    /// Checks if a card COULD match the qualifier's criteria (tribe, cardType, tokenType)
    /// WITHOUT applying the scope filter. Used to determine if scope is relevant for triggers.
    /// </summary>
    private bool QualifyCardIgnoringScope(Card c, Qualifier q) {
        // tribe check
        if (q.tribe != null && c.tribe != q.tribe) {
            return false;
        }
        // cardtype check
        if (q.cardType != null && c.type != q.cardType) {
            return false;
        }
        // tokentype check
        if (q.tokenType != null) {
            if (c is not Token t) {
                return false;
            }
            if (q.tokenType != t.tokenType) {
                return false;
            }
        }
        return true;
    }

    private bool QualifyTarget(int uid, Effect effect, Player castingPlayer) {
        Debug.Assert(effect.GetTargetType() != null, "QualifyTarget called with null targetType");
        TargetType targetType = (TargetType)effect.GetTargetType()!;
        bool targetIsPlayer = playerOne.uid == uid || playerTwo.uid == uid;

        // For Counter effects, qualifying works differently - targets are on the stack
        if (effect.effect == EffectType.Counter) {
            return QualifyCounterTarget(uid, effect);
        }

        switch (targetType) {
            case TargetType.Player:
                return targetIsPlayer;
            case TargetType.Opponent:
                // Only the opponent of the casting player is valid
                return targetIsPlayer && uid == GetOpponent(castingPlayer).uid;
            case TargetType.Any:
                if (targetIsPlayer) return true;
                if (!GetAllSummonsInPlay().Contains(cardByUid[uid])) return false;
                // Check for CantBeTargeted passive
                if (cardByUid[uid].GetPassives().Any(p => p.passive == Passive.CantBeTargeted)) return false;
                return true;
            case TargetType.Token:
                if (targetIsPlayer) return false;
                // Check both tokens list AND playField for Token instances (summon-type tokens are in playField)
                List<Token> tempTokenList = playerOne.tokens.Concat(playerTwo.tokens).ToList();
                bool isInTokensList = tempTokenList.Contains(cardByUid[uid]);
                bool isTokenInPlayField = cardByUid[uid] is Token && GetAllSummonsInPlay().Contains(cardByUid[uid]);
                if (!isInTokensList && !isTokenInPlayField) return false;
                // Check for CantBeTargeted passive
                if (cardByUid[uid].GetPassives().Any(p => p.passive == Passive.CantBeTargeted)) return false;
                return true;
            case TargetType.Summon:
                if (targetIsPlayer) return false;
                if (!GetAllSummonsInPlay().Contains(cardByUid[uid])) return false;
                Card summonCard = cardByUid[uid];
                // Check for CantBeTargeted passive
                if (summonCard.GetPassives().Any(p => p.passive == Passive.CantBeTargeted)) return false;
                // Apply sourcePlayer filter - only target opponent's summons if sourcePlayer is "opponent", only your summons if "self"
                if (effect.sourcePlayer == "opponent" && GetControllerOf(summonCard) == castingPlayer) return false;
                if (effect.sourcePlayer == "self" && GetControllerOf(summonCard) != castingPlayer) return false;
                // Apply tribe filter if specified
                if (effect.tribe != null && summonCard.tribe != effect.tribe) return false;
                // Check restrictions for summon targets
                if (effect.restrictions != null) {
                    foreach (Restriction r in effect.restrictions) {
                        if (r == Restriction.KeywordsOrAbilities) {
                            bool hasKeywords = summonCard.GetKeywords()?.Count > 0;
                            bool hasAbilities = summonCard.activatedEffects?.Count > 0 || summonCard.triggeredEffects?.Count > 0;
                            if (!hasKeywords && !hasAbilities) return false;
                        }
                        if (r == Restriction.HasKeyword) {
                            bool hasKeywords = summonCard.GetKeywords()?.Count > 0;
                            if (!hasKeywords) return false;
                        }
                        if (r == Restriction.NonToken && summonCard is Token) return false;
                        if (r == Restriction.NonMerfolk && summonCard.tribe == Tribe.Merfolk) return false;
                        if (r == Restriction.NonTreefolk && summonCard.tribe == Tribe.Treefolk) return false;
                        if (r == Restriction.NonGolem && summonCard.tribe == Tribe.Golem) return false;
                        if (r == Restriction.Attacking && !currentAttackUids.ContainsKey(summonCard.uid)) return false;
                        if (r == Restriction.DefenseGreaterThanAttack) {
                            if (summonCard.defense == null || summonCard.attack == null) return false;
                            if (summonCard.defense <= summonCard.attack) return false;
                        }
                    }
                }
                return true;
            case TargetType.NonSummon:
                if (targetIsPlayer) return false;
                if (!cardByUid.ContainsKey(uid) || cardByUid[uid].type == CardType.Summon || !allCardsInPlay.Contains(cardByUid[uid])) return false;
                // Check for CantBeTargeted passive
                if (cardByUid[uid].GetPassives().Any(p => p.passive == Passive.CantBeTargeted)) return false;
                return true;
            case TargetType.Permanent:
                if (targetIsPlayer) return false;
                if (!cardByUid.ContainsKey(uid) || !allCardsInPlay.Contains(cardByUid[uid])) return false;
                // Check for CantBeTargeted passive
                if (cardByUid[uid].GetPassives().Any(p => p.passive == Passive.CantBeTargeted)) return false;
                return true;
            case TargetType.Spell:
                // Spells are on the stack, not targetable by uid in the same way
                return false;
            case TargetType.Graveyard:
                if (targetIsPlayer) return false;
                return cardByUid.ContainsKey(uid) && (playerOne.graveyard.Contains(cardByUid[uid]) || playerTwo.graveyard.Contains(cardByUid[uid]));
            case TargetType.CardInHand:
                if (targetIsPlayer) return false;
                if (!cardByUid.ContainsKey(uid)) return false;
                Card card = cardByUid[uid];
                // Must be in the casting player's hand
                if (!castingPlayer.hand.Contains(card)) return false;
                // Apply cardType filter if specified
                if (effect.cardType != null && card.type != effect.cardType) return false;
                // Apply tribe filter if specified
                if (effect.tribe != null && card.tribe != effect.tribe) return false;
                // Apply restrictions (e.g., cost restriction)
                if (effect.restrictions != null) {
                    foreach (Restriction r in effect.restrictions) {
                        if (r == Restriction.Cost) {
                            if (effect.restrictionMax != null && card.cost > effect.restrictionMax) return false;
                            if (effect.restrictionMin != null && card.cost < effect.restrictionMin) return false;
                        }
                    }
                }
                return true;
            case TargetType.OpponentHand:
                if (targetIsPlayer) return false;
                if (!cardByUid.ContainsKey(uid)) return false;
                Card oppCard = cardByUid[uid];
                // Must be in the opponent's hand
                Player opponent = GetOpponent(castingPlayer);
                if (!opponent.hand.Contains(oppCard)) return false;
                // Apply cardType filter if specified
                if (effect.cardType != null && oppCard.type != effect.cardType) return false;
                // Apply tribe filter if specified
                if (effect.tribe != null && oppCard.tribe != effect.tribe) return false;
                // Apply restrictions (e.g., nonSummon)
                if (effect.restrictions != null) {
                    foreach (Restriction r in effect.restrictions) {
                        if (r == Restriction.Summon && oppCard.type != CardType.Summon) return false;
                        if (r == Restriction.NonSummon && oppCard.type == CardType.Summon) return false;
                        if (r == Restriction.Cost) {
                            if (effect.restrictionMax != null && oppCard.cost > effect.restrictionMax) return false;
                            if (effect.restrictionMin != null && oppCard.cost < effect.restrictionMin) return false;
                        }
                    }
                }
                return true;
            case TargetType.CardInHandOrGraveyard:
                if (targetIsPlayer) return false;
                if (!cardByUid.ContainsKey(uid)) return false;
                Card handOrGraveCard = cardByUid[uid];
                // Must be in the casting player's hand OR graveyard
                bool inHand = castingPlayer.hand.Contains(handOrGraveCard);
                bool inGraveyard = castingPlayer.graveyard.Contains(handOrGraveCard);
                if (!inHand && !inGraveyard) return false;
                // Apply cardType filter if specified
                if (effect.cardType != null && handOrGraveCard.type != effect.cardType) return false;
                // Apply tribe filter if specified
                if (effect.tribe != null && handOrGraveCard.tribe != effect.tribe) return false;
                // Apply restrictions if any
                if (effect.restrictions != null) {
                    foreach (Restriction r in effect.restrictions) {
                        if (r == Restriction.Cost) {
                            if (effect.restrictionMax != null && handOrGraveCard.cost > effect.restrictionMax) return false;
                            if (effect.restrictionMin != null && handOrGraveCard.cost < effect.restrictionMin) return false;
                        }
                    }
                }
                return true;
            case TargetType.CardInGraveyard:
                if (targetIsPlayer) return false;
                if (!cardByUid.ContainsKey(uid)) return false;
                Card graveCard = cardByUid[uid];
                // Must be in the casting player's graveyard
                if (!castingPlayer.graveyard.Contains(graveCard)) return false;
                // Apply cardType filter if specified
                if (effect.cardType != null && graveCard.type != effect.cardType) return false;
                // Apply tribe filter if specified
                if (effect.tribe != null && graveCard.tribe != effect.tribe) return false;
                // Apply restrictions if any
                if (effect.restrictions != null) {
                    foreach (Restriction r in effect.restrictions) {
                        if (r == Restriction.Cost) {
                            if (effect.restrictionMax != null && graveCard.cost > effect.restrictionMax) return false;
                            if (effect.restrictionMin != null && graveCard.cost < effect.restrictionMin) return false;
                        }
                    }
                }
                return true;
            default:
                throw new Exception("TargetType not implemented/unknown (QualifyTarget)");
        }
    }

    private bool QualifyCounterTarget(int uid, Effect effect) {
        if (!cardByUid.ContainsKey(uid)) return false;
        Card targetCard = cardByUid[uid];

        // Find the stack object for this card
        StackObj? targetStackObj = null;
        foreach (StackObj stackObj in stack) {
            if (stackObj.sourceCard.uid == uid) {
                targetStackObj = stackObj;
                break;
            }
        }
        if (targetStackObj == null) return false;

        // Check targetType matches stack item type
        switch (effect.GetTargetType()) {
            case TargetType.Summon:
                if (targetCard.type != CardType.Summon) return false;
                break;
            case TargetType.Spell:
                // Spell targetType matches any spell or summon (anything on the stack)
                break;
            case TargetType.NonSummon:
                if (targetCard.type == CardType.Summon) return false;
                break;
        }

        // Check restrictions
        if (effect.restrictions != null) {
            foreach (Restriction restriction in effect.restrictions) {
                switch (restriction) {
                    case Restriction.Defense:
                        if (targetCard.defense == null) return false;
                        if (effect.restrictionMin != null && targetCard.defense < effect.restrictionMin) return false;
                        if (effect.restrictionMax != null && targetCard.defense > effect.restrictionMax) return false;
                        break;
                    case Restriction.Cost:
                        if (effect.restrictionMax != null && targetCard.cost > effect.restrictionMax) return false;
                        break;
                    case Restriction.DefenseGreaterThanAttack:
                        if (targetCard.defense == null || targetCard.attack == null) return false;
                        if (targetCard.defense <= targetCard.attack) return false;
                        break;
                }
            }
        }

        return true;
    }

    public bool QualifyRestriction(Card c, Restriction restriction, Player restrictionController) {
        switch (restriction) {
            case Restriction.YouControl:
                if (restrictionController.playField.Contains(c)) return true;
                break;
            case Restriction.CreatesStones:
                if (c.HasEffect(EffectType.CreateToken) && c.HasTokenType(TokenType.Stone)) return true;
                break;
            case Restriction.NonGolem:
                if (c.tribe != Tribe.Golem) return true;
                break;
            case Restriction.NonTreefolk:
                if (c.tribe != Tribe.Treefolk) return true;
                break;
            case Restriction.NonMerfolk:
                if (c.tribe != Tribe.Merfolk) return true;
                break;
            case Restriction.NonToken:
                if (c is not Token) return true;
                break;
            case Restriction.Cost:
                // Cost restriction is handled separately in QualifyTrigger with restrictionMax/restrictionMin
                // Just return true here to not block the trigger
                return true;
            case Restriction.DefenseGreaterThanAttack:
                // Card's defense must be greater than its attack (for Blast Open)
                if (c.defense != null && c.attack != null && c.defense > c.attack) return true;
                break;
        }

        return false;
    }

    public int GetExcessSummons(Player player, Player opponent, List<Restriction>? restrictions = null) {
        List<Card> playerSummons = player.playField.ToList();
        List<Card> opponentSummons = opponent.playField;
        if (restrictions != null) {
            foreach (Restriction r in restrictions) {
                switch (r) {
                    case Restriction.NonToken:
                        List<Card> tempSummons = playerSummons.ToList();
                        foreach (var c in tempSummons.OfType<Token>()) {
                            playerSummons.Remove(c);
                        }

                        tempSummons = opponentSummons.ToList();
                        foreach (var c in tempSummons.OfType<Token>()) {
                            opponentSummons.Remove(c);
                        }

                        break;
                }
            }
        }

        // if the opponent has less or equal amount of summons, return 0, otherwise return the difference
        return opponentSummons.Count > playerSummons.Count ? opponentSummons.Count - playerSummons.Count : 0;
    }

    public List<Card> GetAllCardsControlled(Player player) {
        List<Card> tempCardList = player.playField.ToList();
        tempCardList.AddRange(player.tokens);
        return tempCardList;
    }

    public List<Card> GetAllCardsOfTribe(Tribe tribe, Player? controllingPlayer = null) {
        List<Card> cards = new();
        // controlled by controllingPlayer
        if (controllingPlayer != null) {
            foreach (Card c in controllingPlayer.playField) {
                if (c.tribe == tribe) cards.Add(c);
            }

            return cards;
        }

        // controlled by all
        foreach (Card c in playerOne.playField) {
            if (c.tribe == tribe) cards.Add(c);
        }

        foreach (Card c in playerTwo.playField) {
            if (c.tribe == tribe) cards.Add(c);
        }

        return cards;
    }

    private void CheckForTriggersPlayer(TriggerContext tc, Player player) {
        // GetTriggers checks all zones (play, hand, graveyard, deck, exile)
        // GetTriggersInCard handles zone filtering via hasInZoneCondition and isSelfLeavingTrigger
        AddToControlledTriggers(player, GetTriggers(tc, player));
    }

    private void AddToControlledTriggers(Player player, List<TriggeredEffect> triggers) {
        foreach (TriggeredEffect tEffect in triggers) {
            // Immediate triggers resolve directly without going on the stack
            if (tEffect.immediate) {
                Console.WriteLine($"[AddToControlledTriggers] Resolving immediate trigger from {tEffect.sourceCard?.name}");
                ResolveImmediateTrigger(player, tEffect);
            } else {
                player.controlledTriggers.Add(tEffect);
            }
        }
    }

    /// <summary>
    /// Resolves an immediate trigger directly without putting it on the stack.
    /// Used for effects like delayed zone changes that shouldn't show as abilities.
    /// </summary>
    private void ResolveImmediateTrigger(Player player, TriggeredEffect tEffect) {
        if (tEffect.effects == null) return;

        foreach (Effect e in tEffect.effects) {
            // Set up the effect context
            Effect effectClone = e.Clone();
            effectClone.sourceCard = tEffect.sourceCard;

            // For self-targeting effects (like Endless Garden returning itself)
            if (tEffect.scope == Scope.SelfOnly && tEffect.triggerCard != null) {
                effectClone.subjectUid = tEffect.triggerCard.uid;
            } else if (tEffect.sourceCard != null) {
                effectClone.subjectUid = tEffect.sourceCard.uid;
            }

            // Resolve the effect
            effectClone.Resolve(this, player);
        }
    }

    private List<TriggeredEffect> GetTriggers(TriggerContext tc, Player player) {
        List<TriggeredEffect> newTEffectList = new();
        foreach (Card c in player.playField.ToList()) {
            List<TriggeredEffect> tempTriggeredEffects = GetTriggersInCard(tc, player, c);
            foreach (TriggeredEffect tEffect in tempTriggeredEffects) {
                newTEffectList.Add(tEffect);
            }
        }

        foreach (Card c in player.hand.ToList()) {
            List<TriggeredEffect> tempTriggeredEffects = GetTriggersInCard(tc, player, c);
            foreach (TriggeredEffect tEffect in tempTriggeredEffects) {
                newTEffectList.Add(tEffect);
            }
        }

        foreach (Card c in player.graveyard.ToList()) {
            List<TriggeredEffect> tempTriggeredEffects = GetTriggersInCard(tc, player, c);
            foreach (TriggeredEffect tEffect in tempTriggeredEffects) {
                newTEffectList.Add(tEffect);
            }
        }

        if (player.deck != null) {
            foreach (Card c in player.deck.ToList()) {
                List<TriggeredEffect> tempTriggeredEffects = GetTriggersInCard(tc, player, c);
                foreach (TriggeredEffect tEffect in tempTriggeredEffects) {
                    newTEffectList.Add(tEffect);
                }
            }
        }

        foreach (Card c in player.exile.ToList()) {
            List<TriggeredEffect> tempTriggeredEffects = GetTriggersInCard(tc, player, c);
            foreach (TriggeredEffect tEffect in tempTriggeredEffects) {
                newTEffectList.Add(tEffect);
            }
        }

        // Check the stack for Cast triggers (cards trigger their own "when you cast this" effects)
        if (tc.trigger == Trigger.Cast) {
            foreach (StackObj stackObj in stack) {
                if (stackObj.sourceCard != null && GetOwnerOf(stackObj.sourceCard) == player) {
                    List<TriggeredEffect> tempTriggeredEffects = GetTriggersInCard(tc, player, stackObj.sourceCard);
                    foreach (TriggeredEffect tEffect in tempTriggeredEffects) {
                        newTEffectList.Add(tEffect);
                    }
                }
            }
        }

        List<TriggeredEffect> tempEventTriggers = player.eventTriggers.ToList();
        foreach (TriggeredEffect tEffect in tempEventTriggers) {
            if (QualifyTrigger(tc, player, tEffect, tEffect.sourceCard)) {
                newTEffectList.Add(tEffect);
                player.eventTriggers.Remove(tEffect);
            }
        }

        return newTEffectList;
    }

    private bool QualifyTrigger(TriggerContext tc, Player player, TriggeredEffect tEffect, Card sourceCard) {
        if (!tEffect.CostsArePayable(this, player)) return false;
        if (!CheckTriggerConditions(player, tc.trigger, tEffect, tc.zone, sourceCard)) return false;
        if (tEffect.phase != null && currentPhase != tEffect.phase) return false;
        // phaseOfPlayer: check if the phase trigger should fire on player's turn or opponent's turn
        if (tEffect.phaseOfPlayer == "player" && player != GetPlayerByTurn(true)) return false;
        if (tEffect.phaseOfPlayer == "opponent" && player == GetPlayerByTurn(true)) return false;
        if (tEffect.isPlayerTurn == true && player != GetPlayerByTurn(true)) return false;
        if (tEffect.isPlayerTurn == false && player == GetPlayerByTurn(true)) return false;
        if (tc.trigger == Trigger.Draw) {
            if (tEffect.player == "player" && tc.triggerController != player) return false;
            if (tEffect.player == "opponent" && tc.triggerController == player) return false;
            if (tEffect.restrictions != null && tEffect.restrictions.Contains(Restriction.NotFirst)) {
                if (tc.isFirstDraw) return false;
            }
        }
        // Cast triggers: check if the casting player matches the player filter
        if (tc.trigger == Trigger.Cast) {
            Console.WriteLine($"[CastTrigger] Checking trigger on {tEffect.sourceCard?.name}, cardType filter={tEffect.cardType}, cast card={tc.card?.name} type={tc.card?.type}, cost={tc.card?.cost}");
            if (tEffect.player == "player" && tc.triggerController != player) {
                Console.WriteLine($"[CastTrigger] REJECTED: player filter mismatch");
                return false;
            }
            if (tEffect.player == "opponent" && tc.triggerController == player) {
                Console.WriteLine($"[CastTrigger] REJECTED: opponent filter mismatch");
                return false;
            }
            // Check cardType filter (spell vs summon)
            if (tEffect.cardType != null && tc.card != null && tc.card.type != tEffect.cardType) {
                Console.WriteLine($"[CastTrigger] REJECTED: cardType mismatch - trigger wants {tEffect.cardType}, card is {tc.card.type}");
                return false;
            }
            // Check cost restrictions on the cast card
            if (tEffect.restrictions != null && tEffect.restrictions.Contains(Restriction.Cost) && tc.card != null) {
                Console.WriteLine($"[CastTrigger] Checking cost restriction: card cost={tc.card.cost}, max={tEffect.restrictionMax}, min={tEffect.restrictionMin}");
                if (tEffect.restrictionMax != null && tc.card.cost > tEffect.restrictionMax) {
                    Console.WriteLine($"[CastTrigger] REJECTED: cost {tc.card.cost} > max {tEffect.restrictionMax}");
                    return false;
                }
                if (tEffect.restrictionMin != null && tc.card.cost < tEffect.restrictionMin) {
                    Console.WriteLine($"[CastTrigger] REJECTED: cost {tc.card.cost} < min {tEffect.restrictionMin}");
                    return false;
                }
            }
            Console.WriteLine($"[CastTrigger] PASSED all checks");
        }
        // Discard triggers: check if the discarding player matches the player filter
        if (tc.trigger == Trigger.Discard) {
            if (tEffect.player == "player" && tc.triggerController != player) return false;
            if (tEffect.player == "opponent" && tc.triggerController == player) return false;
        }
        // Tribute triggers: check if the card being tributed for matches the tribe requirement
        if (tc.trigger == Trigger.Tribute && tEffect.tribe != null) {
            if (tc.tributeForCard == null || tc.tributeForCard.tribe != tEffect.tribe) return false;
        }
        // Tribute triggers: check if the card being tributed has the required keyword
        if (tc.trigger == Trigger.Tribute && tEffect.keyword != null) {
            if (tc.card == null || !DetectKeyword(tc.card, tEffect.keyword.Value)) return false;
        }
        // Mill triggers with amount threshold: check if enough cards were milled
        // Triggers with amount require the batch context (millBatchSize > 0), individual card triggers won't match
        if (tc.trigger == Trigger.Mill && tEffect.amount != null) {
            if (tc.millBatchSize < tEffect.amount) return false;
        }
        return true;
    }
    
    private List<TriggeredEffect> GetTriggersInCard(TriggerContext tc, Player player, Card c) {
        List<TriggeredEffect> newTEffectList = new();
        Debug.Assert(c != null, "There is no card associated with this trigger");
        if (c.triggeredEffects == null) {
            return newTEffectList;
        }
        // Check for DisableEnterPlayEffects passive - if any card in play has it, skip all enter-play triggers
        if (tc.trigger == Trigger.EnteredZone && tc.zone == Zone.Play) {
            bool enterPlayDisabled = allCardsInPlay.Any(card =>
                card.passiveEffects?.Any(p => p.passive == Passive.DisableEnterPlayEffects) == true);
            if (enterPlayDisabled) {
                Console.WriteLine($"[GetTriggersInCard] Enter-play triggers disabled by DisableEnterPlayEffects passive");
                return newTEffectList;
            }
        }
        foreach (TriggeredEffect tEffect in c.triggeredEffects) {
            // set the source cards for all effects and sub-effects
            Qualifier tQualifier = new Qualifier(tEffect, player);
            // Set sourceCard on qualifier since tEffect.sourceCard isn't set until CloneWithTriggerCard
            if (tQualifier.sourceCard == null) tQualifier.sourceCard = c;
            if(!QualifyTrigger(tc, player, tEffect, c)) {
                continue;
            }
            // Zone check: default to play unless an InZone condition specifies otherwise
            // Exception: For LeftZone/Death/Mill/Tribute/Discard triggers on the card itself, skip zone check since
            // the card has already moved out of play/deck/hand when we check for triggers
            // Exception: OpeningHand triggers work from hand
            bool hasInZoneCondition = tEffect.conditions?.Any(cond => cond.condition == ConditionType.InZone || cond.condition == ConditionType.InZones) ?? false;
            bool isSelfLeavingTrigger = (tc.trigger == Trigger.LeftZone || tc.trigger == Trigger.Death || tc.trigger == Trigger.Mill || tc.trigger == Trigger.Tribute || tc.trigger == Trigger.Discard)
                                        && tc.card == c && tEffect.scope == Scope.SelfOnly;
            bool isOpeningHandTrigger = tEffect.trigger == Trigger.OpeningHand && c.currentZone == Zone.Hand;
            // Cast triggers with selfOnly scope fire from the stack (the card being cast has its own trigger)
            bool isSelfCastTrigger = tc.trigger == Trigger.Cast && tc.card == c && tEffect.scope == Scope.SelfOnly && c.currentZone == Zone.Stack;
            if (!hasInZoneCondition && !isSelfLeavingTrigger && !isOpeningHandTrigger && !isSelfCastTrigger && c.currentZone != Zone.Play) {
                continue;
            }
            // For Draw/Discard triggers, tc.card is just informational (the card that was drawn/discarded)
            // We don't need to qualify it - the trigger fires for any draw/discard
            if (tc.card != null && tc.trigger != Trigger.Draw && tc.trigger != Trigger.Discard) {
                // First, check if tc.card qualifies against the trigger's qualifiers
                if (!QualifyCard(tc.card, tQualifier)) {
                    continue;
                }

                // Then apply scope - but only if source card COULD match the qualifiers
                // (e.g., GolemBlesser's stone trigger: GolemBlesser can't be a stone, so scope is irrelevant)
                bool sourceCouldMatch = QualifyCardIgnoringScope(c, tQualifier);
                if (sourceCouldMatch) {
                    bool isSelf = tc.card.Equals(c);
                    switch (tEffect.scope) {
                        case Scope.SelfOnly:
                            if (!isSelf) continue;
                            break;
                        case Scope.OthersOnly:
                            if (isSelf) continue;
                            break;
                        case Scope.All:
                            // No filter - trigger fires for any qualifying card
                            break;
                    }
                }
            }

            if (tc.cards != null) {
                if (tEffect.scope == Scope.SelfOnly && !tc.cards.Contains(c)) continue;
                if (GetQualifiedCards(tc.cards, tQualifier).Count == 0) continue;
            }

            // Check conditions on the triggered effect
            if (tEffect.conditions != null) {
                bool allConditionsMet = true;
                foreach (Condition condition in tEffect.conditions) {
                    if (!condition.Verify(this, player, null, c)) {
                        allConditionsMet = false;
                        break;
                    }
                }
                if (!allConditionsMet) {
                    continue;
                }
            }

            // Add cloned tEffect to list with trigger card and controller set
            TriggeredEffect clonedTEffect = tEffect.CloneWithTriggerCard(c, tc.card, tc.triggerController);
            newTEffectList.Add(clonedTEffect);
        }

        return newTEffectList;
    }

    private bool CheckTriggerConditions(Player player, Trigger triggerType, TriggeredEffect tEffect, Zone? zone, Card sourceCard) {
        if (tEffect.trigger != triggerType) return false;
        if (zone != null && tEffect.zone != zone) return false;
        if (tEffect.conditions != null) {
            foreach (Condition condition in tEffect.conditions) {
                if (!condition.Verify(this, player, null, sourceCard)) return false;
            }
        }
        return true;
    }

    public List<Card> GetQualifiedCards(List<Card> cardsToQualify, Qualifier qualifier) {
        List<Card> qualifiedCards = new List<Card>();
        foreach (Card c in cardsToQualify) {
            if (QualifyCard(c, qualifier)) qualifiedCards.Add(c);
        }
        return qualifiedCards;
    }

    public void MakeChoice(Player player, int currentChoiceIndex) {
        // Ghost Deceiver pre-trigger handling (hardcoded)
        if (ghostDeceiverStage == 1 && player == ghostDeceiverOwner) {
            Console.WriteLine($"[GhostDeceiver] Stage 1 response: choiceIndex={currentChoiceIndex}");
            if (currentChoiceIndex == 0) {
                // Player said yes - send zone selection options
                ghostDeceiverStage = 2;
                var zoneChoices = new List<string> { "Hand", "Play", "Deck", "Exile" };
                string zoneMessage = $"Choose where {ghostDeceiverPendingCard?.name} entered the graveyard from:";
                Console.WriteLine($"[GhostDeceiver] Sending zone selection prompt: {zoneMessage}");
                GameEvent gEvent = GameEvent.CreateOptionEvent(new PlayerChoice(zoneChoices, zoneMessage));
                AddEventForPlayer(player, gEvent);
                Console.WriteLine($"[GhostDeceiver] Zone selection event added to player {player.playerName}");
            } else {
                // Player said no - complete with original source zone
                CompleteGhostDeceiverTrigger();
                // Continue with normal trigger processing
                CheckForTriggersAndPassives(EventType.GainPrio);
            }
            return;
        }
        if (ghostDeceiverStage == 2 && player == ghostDeceiverOwner) {
            // Player chose a zone
            Zone chosenZone = currentChoiceIndex switch {
                0 => Zone.Hand,
                1 => Zone.Play,
                2 => Zone.Deck,
                3 => Zone.Exile,
                _ => ghostDeceiverPendingSourceZone ?? Zone.Play
            };
            Console.WriteLine($"[GhostDeceiver] Player chose source zone: {chosenZone}");
            CompleteGhostDeceiverTrigger(chosenZone);
            // Continue with normal trigger processing
            CheckForTriggersAndPassives(EventType.GainPrio);
            return;
        }

        // alternate cost selection (for cards with alternate costs)
        if (currentAlternateCost != null && cardBeingCast != null) {
            if (currentChoiceIndex == 0) {
                // Player chose normal cost
                currentAlternateCost = null;
                if (cardBeingCast.type == CardType.Spell) {
                    // For spells, continue to target selection then cast
                    AttemptToCast(player, cardBeingCast, CastingStage.TargetSelection);
                } else {
                    // For summons, continue to tribute selection
                    AttemptToCast(player, cardBeingCast, CastingStage.TributeSelection);
                }
            } else {
                // Player chose alternate cost - request payment
                // Keep currentAlternateCost so HandleCostSelection knows the cost type
                usingAlternateCost = true;
                RequestAlternateCostPayment(player, currentAlternateCost);
            }
            return;
        }
        // hand ability choice (for cards with activateFromHand abilities like Transparent Plant)
        if (pendingHandAbilityChoice && cardBeingCast != null) {
            pendingHandAbilityChoice = false;
            if (currentChoiceIndex == 0) {
                // Player chose to cast normally - continue with normal cast flow
                currentHandAbilityEffect = null;
                AttemptToCast(player, cardBeingCast, CastingStage.AmountSelection);
            } else {
                // Player chose to use the hand ability - activate it
                Debug.Assert(currentHandAbilityEffect != null, "No hand ability effect for choice");
                ActivatedEffect aEffect = currentHandAbilityEffect;
                currentHandAbilityEffect = null;
                cardBeingCast = null;  // Clear since we're not casting
                ActivateHandAbility(player, aEffect);
            }
            return;
        }
        // alternate cost choice (for activated abilities with multiple cost options)
        if (pendingActivatedAbilityAltCostChoice) {
            pendingActivatedAbilityAltCostChoice = false;
            Debug.Assert(currentActivatedEffect != null, "No activated effect for alternate cost choice");
            Debug.Assert(currentActivatedEffect.alternateCosts != null, "No alternate costs defined");

            // Build list of payable costs (same order as presented to player)
            List<AlternateCost> payableCosts = new();
            foreach (AlternateCost altCost in currentActivatedEffect.alternateCosts) {
                if (CanPayActivatedAbilityAltCost(player, altCost)) {
                    payableCosts.Add(altCost);
                }
            }

            AlternateCost chosenCost = payableCosts[currentChoiceIndex];
            currentActivatedAbilityAltCost = chosenCost;
            RequestActivatedAbilityAltCostPayment(player, chosenCost);
            return;
        }
        // optional triggers
        if (optionalTriggers.Count > 0) {
            Debug.Assert(optionalTriggerController != null, "optionalTriggerController is null");
            TriggeredEffect currentTrigger = optionalTriggers.First();

            if (currentChoiceIndex == 1) {
                // Declined - remove trigger from controller's list
                optionalTriggerController.controlledTriggers.Remove(currentTrigger);
                optionalTriggers.Remove(currentTrigger);
            } else {
                // Accepted - trigger will fire, just remove from optional list
                optionalTriggers.Remove(currentTrigger);
            }

            Debug.Assert(currentPlayerToPassTo != null, "there is no currentPlayerToPassTo");
            if (optionalTriggers.Count == 0) {
                HandleTriggers(optionalTriggerController, currentPlayerToPassTo, TriggerStage.Choices);
            } else {
                // Send option event for the next optional trigger
                TriggeredEffect nextTrigger = optionalTriggers.First();
                Player nextDecidingPlayer = nextTrigger.opponentsChoice ? GetOpponent(optionalTriggerController) : optionalTriggerController;
                HandleOptionalEffect(nextDecidingPlayer, nextTrigger);
            }
            return;
        }
        // choice effects
        if (choiceEffects.Count > 0) {
            ApplyChosenChoice(player, currentChoiceIndex, choiceEffects, CastingStage.TargetSelection);
        }
        // choice effects in additional effects
        if (additionalChoiceEffects.Count > 0) {
            ApplyChosenChoice(player, currentChoiceIndex, additionalChoiceEffects, CastingStage.TributeSelection);
        }
        // optional effects
        if (currentOptionalEffect != null) {
            Debug.Assert(unresolvedStackObj != null, "there is no unresolved stackObj");
            if (currentChoiceIndex == 0) {
                // User accepted - mark as no longer optional and re-process through StackObj
                // This ensures resolveTarget/selection checks run before Resolve()
                currentOptionalEffect.optional = false;
                unresolvedEffectIndex--;  // Go back to this effect (was stored at i+1)
                currentOptionalEffect = null;
                unresolvedStackObj.ResumeResolve(this);
            } else {
                // User declined - continue to next effect
                currentOptionalEffect = null;
                unresolvedStackObj.ResumeResolve(this);
            }
        }
    }

    private void ApplyChosenChoice(Player player, int currentChoiceIndex,
        Dictionary<List<Effect>, Effect> choiceEffectDict, CastingStage castingStage) {
        // Map the displayed choice index to the original choice index if filtering was applied
        int originalChoiceIndex = currentChoiceIndex;
        if (currentValidChoiceIndices != null && currentChoiceIndex < currentValidChoiceIndices.Count) {
            originalChoiceIndex = currentValidChoiceIndices[currentChoiceIndex];
            currentValidChoiceIndices = null; // Clear after use
        }

        // Track this selection for multi-choice effects
        selectedChoiceIndices.Add(originalChoiceIndex);
        remainingChoices--;

        KeyValuePair<List<Effect>, Effect> pair = choiceEffectDict.First();
        Debug.Assert(pair.Value.choices != null,
            "there are no choices associated with the choice effect (MakeChoice)");

        // Check if the just-selected choice needs targeting
        List<Effect> selectedChoiceEffects = pair.Value.choices[originalChoiceIndex];
        bool needsTargeting = false;
        foreach (Effect e in selectedChoiceEffects) {
            if (e.HasTargeting() && !e.resolveTarget && !e.all && e.targetBasedOn == null) {
                List<int> possibleTargets = GetPossibleTargets(player, e);
                if (possibleTargets.Count > 0) {
                    string message = e.EffectToString(this);
                    CreateAndAddNewTargetSelectionEvent(player, possibleTargets, e.GetTargetMax(), message, e.GetTargetMin());
                    effectsWithTargets.Add(e);
                    needsTargeting = true;
                }
            }
        }

        // If targeting is needed, store state and wait for target selection
        if (needsTargeting) {
            pendingChoiceTargeting = true;
            pendingChoicePlayer = player;
            pendingChoiceEffectDict = choiceEffectDict;
            pendingChoiceCastingStage = castingStage;
            return;
        }

        // If more choices remain, prompt for next choice
        if (remainingChoices > 0) {
            HandleChoice(pair.Value.choices, player, currentForOpponentChoice);
            return;
        }

        // All choices made - continue with normal choice completion flow
        ContinueAfterAllChoicesMade(player, choiceEffectDict, castingStage);
    }

    /// <summary>
    /// Completes choice selection by inserting selected effects and continuing the cast/activate/trigger flow.
    /// Called after all choices (and their targeting) are complete.
    /// </summary>
    private void ContinueAfterAllChoicesMade(Player player, Dictionary<List<Effect>, Effect> choiceEffectDict, CastingStage castingStage) {
        KeyValuePair<List<Effect>, Effect> pair = choiceEffectDict.First();
        Debug.Assert(pair.Value.choices != null, "no choices in choiceEffectDict");

        // Insert all selected effects
        int insertIndex = pair.Key.IndexOf(pair.Value);
        pair.Key.RemoveAt(insertIndex);
        // Insert all selected choices' effects in order
        int currentInsertIndex = insertIndex;
        foreach (int selectedIndex in selectedChoiceIndices) {
            pair.Key.InsertRange(currentInsertIndex, pair.Value.choices[selectedIndex]);
            currentInsertIndex += pair.Value.choices[selectedIndex].Count;
        }

        // Store selected indices before clearing (for description highlighting)
        List<int> chosenIndicesCopy = selectedChoiceIndices.ToList();

        // Clear multi-choice tracking
        selectedChoiceIndices.Clear();

        // finish by removing it from the current choices dictionary
        choiceEffectDict.Remove(pair.Key);
        Debug.Assert(currentPlayerToPassTo != null, "there is no currentPlayerToPassTo");
        if (choiceEffectDict.Count != 0) return;
        if (choiceCard != null) {
            Debug.Assert(pair.Value.choiceIndex != null, "there is no choiceIndex for this Effect");
            // Store all selected indices for this choice group (supports "Choose Two" etc.)
            choiceCard.chosenIndices.Add((int)pair.Value.choiceIndex, chosenIndicesCopy);
            // Check for more choose effects before continuing to target selection
            // This handles cards like Lost Sanctuary with multiple choose effects
            if (CheckForChoicesCard(player, choiceCard)) return;
            AttemptToCast(player, choiceCard, castingStage);
        } else if (choiceActivatedEffect != null) {
            // Continue with activated ability after choice selection
            ActivatedEffect aEffect = choiceActivatedEffect;
            choiceActivatedEffect = null;
            AttemptToActivate(player, aEffect, ActivationStage.TargetSelection);
        } else {
            HandleTriggers(player, currentPlayerToPassTo, TriggerStage.TargetSelection);
        }
    }

    // order of operations:
    // (turnPlayer first)
    // 1. optionals
    // 2. require targets
    // 3. prompt for ordering
    // 4. add to stack
    // 5. pass prio
    private void HandleTriggers(Player player, Player playerToPassTo,
        TriggerStage stage = TriggerStage.Initial) {
        currentPlayerToPassTo = playerToPassTo;
        switch (stage) {
            case TriggerStage.Initial:
                // Filter out triggers where ALL effects have failing conditions
                player.controlledTriggers.RemoveAll(trigger => {
                    if (trigger.effects == null || trigger.effects.Count == 0) return false;
                    // If all effects have conditions and all fail, remove the trigger
                    bool allEffectsHaveConditions = trigger.effects.All(e => e.conditions != null && e.conditions.Count > 0);
                    if (!allEffectsHaveConditions) return false;
                    bool allConditionsFail = trigger.effects.All(e =>
                        e.conditions != null && !e.conditions.All(c => c.Verify(this, player)));
                    if (allConditionsFail) {
                        Console.WriteLine($"[HandleTriggers] Skipping trigger from {trigger.sourceCard?.name} - all effect conditions failed");
                    }
                    return allConditionsFail;
                });
                if (player.controlledTriggers.Count <= 0) {
                    FinishWithTriggers(player, playerToPassTo);
                    return;
                }
                if (CheckForOptionalTriggers(player)) {
                    return;
                }
                goto case TriggerStage.Choices;
            case TriggerStage.Choices:
                if (CheckForChoicesTriggers(player)) return;
                goto case TriggerStage.TargetSelection;
            case TriggerStage.TargetSelection:
                if (CheckForTargetSelectionTriggers(player)) return;
                goto case TriggerStage.Ordering;
            case TriggerStage.Ordering:
                switch (player.controlledTriggers.Count) {
                    case 0:
                        FinishWithTriggers(player, playerToPassTo);
                        return;
                    case 1:
                        TriggeredEffect tEffect = player.controlledTriggers[0];
                        player.controlledTriggers.Clear();
                        AddStackObjToStack(CreateStackObj(player, tEffect.sourceCard, tEffect));
                        FinishWithTriggers(player, playerToPassTo);
                        return;
                    case > 1:
                        CreateAndAddOrderingEvent(player);
                        return;
                }
                break;
        }
    }
    

    private bool CheckForOptionalTriggers(Player player) {
        optionalTriggerController = player;  // Track who controls these triggers

        foreach (TriggeredEffect tEffect in player.controlledTriggers) {
            if (tEffect.optional) {
                optionalTriggers.Add(tEffect);
            }
        }

        // Only send one option event at a time
        if (optionalTriggers.Count > 0) {
            TriggeredEffect firstTrigger = optionalTriggers.First();
            // Determine who makes the choice - opponent if opponentsChoice, otherwise controller
            Player decidingPlayer = firstTrigger.opponentsChoice ? GetOpponent(player) : player;

            // Bots auto-decline optional triggers they control, but accept opponent's choice triggers
            // (opponent's choice = usually bad for controller, so bot accepts)
            if (decidingPlayer.isBot) {
                if (firstTrigger.opponentsChoice) {
                    // Bot is opponent - accept (index 0) to make trigger fire
                    optionalTriggers.Remove(firstTrigger);
                    // Trigger stays in controlledTriggers to be processed
                } else {
                    // Bot is controller - decline own optional triggers
                    player.controlledTriggers.Remove(firstTrigger);
                    optionalTriggers.Remove(firstTrigger);
                }
                // Check for more optional triggers recursively
                if (optionalTriggers.Count > 0) {
                    TriggeredEffect nextTrigger = optionalTriggers.First();
                    Player nextDecidingPlayer = nextTrigger.opponentsChoice ? GetOpponent(player) : player;
                    HandleOptionalEffect(nextDecidingPlayer, nextTrigger);
                }
                return optionalTriggers.Count > 0;
            }

            HandleOptionalEffect(decidingPlayer, firstTrigger);
        }

        return optionalTriggers.Count > 0;
    }

    private bool CheckCardForAdditionalCosts(Player player, Card focusCard) {
        if(focusCard.additionalCosts == null) return false;
        foreach (AdditionalCost aCost in focusCard.additionalCosts) {
            if (aCost.isPaid) continue;

            // For X-based costs where X hasn't been set yet, the first cost determines X
            if (aCost.amountBasedOn == AmountBasedOn.X && focusCard.x == null) {
                if (aCost.costType == CostType.Sacrifice || aCost.costType == CostType.Discard) {
                    // Send variable selection cost event - player chooses how many to sacrifice/discard
                    AddVariableCostEvent(player, aCost, focusCard);
                    cardAdditionalCostAmount++;
                    return true; // Wait for selection, which will set X
                }
            }

            int resolvedAmount = aCost.GetAmount(focusCard);

            // Life costs are paid automatically, no user selection needed
            if (aCost.costType == CostType.Life) {
                PayLifeCost(player, resolvedAmount);
                aCost.isPaid = true;
                continue;
            }
            // Other costs require user selection
            AddCostEvent(player, null, aCost, focusCard);
            cardAdditionalCostAmount++;
        }
        return cardAdditionalCostAmount > 0;
    }
    private bool CheckForChoicesTriggers(Player player) {
        foreach (TriggeredEffect tEffect in player.controlledTriggers) {
            if (tEffect.effects == null) continue;
            // Clone the effects list if it's already being used (multiple copies of same summon)
            // This ensures each trigger instance has its own modifiable list
            if (choiceEffects.ContainsKey(tEffect.effects)) {
                tEffect.effects = tEffect.effects.Select(e => e.Clone()).ToList();
            }
            foreach (var e in tEffect.effects.Where(e => e.effect == EffectType.Choose)) {
                Debug.Assert(e.choices != null, "there are no choices for this choose effect");
                // If opponentsChoice is true or sourcePlayer is "opponent", the opponent makes the choice
                bool isOpponentChoice = e.opponentsChoice || e.sourcePlayer == "opponent";
                Player choosingPlayer = isOpponentChoice ? GetOpponent(player) : player;
                HandleChoice(e.choices, choosingPlayer, isOpponentChoice);
                choiceEffects.Add(tEffect.effects, e);
                choiceCard = null;
            }
        }

        return choiceEffects.Count > 0;
    }

    private bool CheckForChoicesCard(Player player, Card card) {
        if (card.stackEffects == null) return false;
        // Process only the FIRST unprocessed choose effect
        // After it's handled, ContinueAfterAllChoicesMade will call back to check for more
        foreach (Effect effect in card.stackEffects.Where(e => e.effect == EffectType.Choose)) {
            // Use player (the caster) not GetControllerOf - card may still be in hand
            if (effect.conditions != null && !effect.ConditionsAreMet(this, player)) continue;
            Debug.Assert(effect.choices != null, "there are no choices for this choose effect");
            Debug.Assert(effect.choiceIndex != null, "there is no choice index for this choose effect");

            // Determine who makes this choice
            bool isOpponentChoice = effect.opponentsChoice || effect.sourcePlayer == "opponent";
            Player choosingPlayer = isOpponentChoice ? GetOpponent(player) : player;

            // Initialize multi-choice tracking
            remainingChoices = effect.amount ?? 1;
            selectedChoiceIndices.Clear();
            HandleChoice(effect.choices, choosingPlayer, isOpponentChoice);

            // Add to dictionary and return - only process one at a time
            choiceEffects.Add(card.stackEffects, effect);
            choiceCard = card;
            return true;  // Return after first choice effect - others will be checked after this one completes
        }
        return choiceEffects.Count > 0;
    }

    private bool CheckForChoicesActivatedEffect(Player player, ActivatedEffect aEffect) {
        if (aEffect.effects == null) return false;
        foreach (Effect effect in aEffect.effects.Where(e => e.effect == EffectType.Choose)) {
            if (effect.conditions != null && !effect.ConditionsAreMet(this, player)) continue;
            Debug.Assert(effect.choices != null, "there are no choices for this choose effect");
            Debug.Assert(effect.choiceIndex != null, "there is no choice index for this choose effect");
            // Initialize multi-choice tracking
            remainingChoices = effect.amount ?? 1;
            selectedChoiceIndices.Clear();
            HandleChoice(effect.choices, player);
            choiceEffects.Add(aEffect.effects, effect);
            choiceActivatedEffect = aEffect;
            choiceCard = null;  // Make sure choiceCard is null when using activated effect
        }
        return choiceEffects.Count > 0;
    }

    public void HandleOptionalEffect(Player player, TriggeredEffect? tEffect = null, Effect? effect = null) {
        string optionMessage = "error: no option message";
        var choicesText = new List<string> {
            "yes",
            "no"
        };
        if (tEffect != null) optionMessage = tEffect.optionMessage!;
        if (effect != null) {
            currentOptionalEffect = effect;
            optionMessage = effect.optionMessage!;
        }

        GameEvent gEvent = GameEvent.CreateOptionEvent(new PlayerChoice(choicesText, optionMessage));
        AddEventForPlayer(player, gEvent);
    }


    private void HandleChoice(List<List<Effect>> choices, Player player, bool forOpponentChoice = false) {
        currentForOpponentChoice = forOpponentChoice; // Store for multi-choice continuations
        List<string> choicesText = new();
        List<int> validChoiceIndices = new();

        for (int i = 0; i < choices.Count; i++) {
            // Skip choices that were already selected (for multi-choice effects)
            if (selectedChoiceIndices.Contains(i)) continue;

            List<Effect> effectList = choices[i];
            // Check if this choice has valid targets (if it requires any)
            // Skip resolveTarget effects - they select targets at resolution, not cast time
            // Skip targetBasedOn effects - they have auto-determined targets
            bool choiceHasValidTargets = true;
            foreach (Effect e in effectList) {
                if (e.HasTargeting() && !e.resolveTarget && e.targetBasedOn == null && GetPossibleTargets(player, e).Count == 0) {
                    choiceHasValidTargets = false;
                    break;
                }
            }

            if (choiceHasValidTargets) {
                List<string> effectStrings = new();
                foreach (Effect e in effectList) {
                    effectStrings.Add(e.EffectToString(this, forOpponentChoice));
                }
                choicesText.Add(String.Join(" ", effectStrings));
                validChoiceIndices.Add(i);
            }
        }

        // Build message based on remaining choices
        string chooseMessage = remainingChoices > 1
            ? $"Choose ({remainingChoices} remaining):"
            : "Choose:";
        GameEvent gEvent = GameEvent.CreateOptionEvent(new PlayerChoice(choicesText, chooseMessage));
        gEvent.validChoiceIndices = validChoiceIndices;
        currentValidChoiceIndices = validChoiceIndices;
        AddEventForPlayer(player, gEvent);
    }

    /// <summary>
    /// Checks if the card has an activateFromHand ability with valid targets.
    /// If so, presents a choice to the player: Cast normally or use the hand ability.
    /// </summary>
    private bool CheckForHandAbility(Player player, Card card) {
        if (card.activatedEffects == null) return false;

        // Find a hand ability with valid targets
        foreach (ActivatedEffect aEffect in card.activatedEffects) {
            if (!aEffect.activateFromHand) continue;
            if (!aEffect.HasValidTargets(this, player)) continue;

            // Found a valid hand ability - present choice to player
            pendingHandAbilityChoice = true;
            currentHandAbilityEffect = aEffect;

            // Build choice text
            string abilityDescription = aEffect.description ?? "Use discard ability";
            var choicesText = new List<string> {
                "Cast " + card.name,
                abilityDescription
            };
            GameEvent gEvent = GameEvent.CreateOptionEvent(new PlayerChoice(choicesText, "Choose an action"));
            AddEventForPlayer(player, gEvent);
            return true;
        }

        return false;
    }

    private bool CheckForXCost(Player player, Card card) {
        if (!card.NeedsXSelection()) return false;
        cardWaitingForX = card;
        // Calculate max X based on additional costs that use X
        int? maxX = null;
        if (card.additionalCosts != null) {
            foreach (AdditionalCost aCost in card.additionalCosts) {
                if (aCost.amountBasedOn != AmountBasedOn.X) continue;
                int available = 0;
                switch (aCost.costType) {
                    case CostType.Sacrifice:
                        if (aCost.tokenType != null) {
                            foreach (Token t in player.tokens) {
                                if (t.tokenType == aCost.tokenType) available++;
                            }
                        }
                        break;
                    case CostType.Life:
                        available = player.lifeTotal - 1; // Can't go to 0
                        break;
                }
                // Take the minimum of all X-based cost limits
                maxX = maxX == null ? available : Math.Min(maxX.Value, available);
            }
        }
        var gEvent = GameEvent.CreateAmountSelectionEvent(true, maxX);
        AddEventForPlayer(player, gEvent);
        return true;
    }

    public void SetX(Player player, int xAmount) {
        Debug.Assert(cardWaitingForX != null, "there's no card waiting for an x amount (SetX)");
        cardWaitingForX.x = player.spellBurnt ? xAmount * 2 : xAmount;
        AttemptToCast(player, cardWaitingForX, CastingStage.AdditionalCosts);
        cardWaitingForX = null;
    }

    public void CancelCast(Player player) {
        // Reset any pending cast state
        cardWaitingForX = null;
        cardBeingCast = null;
        effectsWithTargets.Clear();
        additionalChoiceEffects.Clear();
        // Return priority to the player
        PassPrioToPlayer(player);
    }

    public void SetAmount(Player player, int amount) {
        // Check if an effect is waiting for upfront repeat amount selection
        if (effectWaitingForRepeatAmount != null) {
            SetRepeatAmount(player, amount);
            return;
        }
        // Check if an effect is waiting for amount (e.g., mill up to N)
        if (effectWaitingForAmount != null) {
            SetEffectAmount(player, amount);
            return;
        }
        // Otherwise, this is for an activated ability
        Debug.Assert(currentActivatedEffect != null, "there's no activated effect waiting for an amount (SetAmount)");
        currentActivatedEffect.SetAmount(amount);
        AttemptToActivate(player, currentActivatedEffect, ActivationStage.CostPayment);
    }

    /// <summary>
    /// Sets the upfront repeat count for an effect and pays the cost.
    /// </summary>
    public void SetRepeatAmount(Player player, int repeatCount) {
        Debug.Assert(effectWaitingForRepeatAmount != null, "there's no effect waiting for repeat amount (SetRepeatAmount)");
        Debug.Assert(cardBeingCast != null, "there's no card being cast (SetRepeatAmount)");

        // Store the repeat count on the effect
        effectWaitingForRepeatAmount.repeatCount = repeatCount;
        Console.WriteLine($"[Repeat Upfront] {player.playerName} chose to repeat {repeatCount} time(s)");

        // Pay the repeat cost upfront
        if (repeatCount > 0 && effectWaitingForRepeatAmount.repeatCostType == CostType.LoseLife) {
            int totalCost = repeatCount * effectWaitingForRepeatAmount.repeatCostAmount!.Value;
            Console.WriteLine($"[Repeat Upfront] {player.playerName} pays {totalCost} LP for repeats");
            LoseLife(player, totalCost);
        }

        effectWaitingForRepeatAmount = null;

        // Continue with the casting flow
        AttemptToCast(player, cardBeingCast, CastingStage.TributeSelection);
    }

    /// <summary>
    /// Requests player to select an amount for an effect (e.g., mill up to N).
    /// </summary>
    public void RequestEffectAmount(Player player, Effect effect, int maxAmount) {
        effectWaitingForAmount = effect;
        var gEvent = GameEvent.CreateAmountSelectionEvent(false, maxAmount);
        AddEventForPlayer(player, gEvent);
    }

    /// <summary>
    /// Sets the amount for an effect waiting for amount selection and resumes resolution.
    /// </summary>
    public void SetEffectAmount(Player player, int amount) {
        Debug.Assert(effectWaitingForAmount != null, "there's no effect waiting for an amount (SetEffectAmount)");
        Debug.Assert(unresolvedStackObj != null, "there's no unresolved stack object (SetEffectAmount)");
        effectWaitingForAmount.amount = amount;
        effectWaitingForAmount = null;
        unresolvedStackObj.ResumeResolve(this);
    }

    private bool CheckForCardTargetSelection(Player player, Card card) {
        if (card.stackEffects != null) {
            foreach (Effect effect in card.stackEffects) {
                HandleEffectTargetSelection(player, effect);
            }
        }
        return effectsWithTargets.Count > 0;
    }

    private bool CheckForTargetSelectionTriggers(Player player) {
        foreach (TriggeredEffect tEffect in player.controlledTriggers) {
            if (tEffect.effects == null) continue;
            foreach (Effect effect in tEffect.effects) {
                HandleEffectTargetSelection(player, effect);
            }
        }

        return effectsWithTargets.Count > 0;
    }

    private void HandleEffectTargetSelection(Player player, Effect effect) {
        // Use new helper methods that support both new target object and legacy fields
        int targetAmount = effect.GetTargetMax();
        int minTargetAmount = effect.GetTargetMin();
        // Skip if no targeting or if effect targets all (no individual selection needed)
        if (!effect.HasTargeting()) {
            return;
        }
        if (effect.all) {
            return;
        }
        // Skip resolve-time selections - these are handled during stack resolution, not before casting
        if (effect.resolveTarget) {
            return;
        }
        // Skip if target will be auto-determined (e.g., TriggerCard, TriggerController)
        if (effect.targetBasedOn != null) {
            return;
        }
        // Skip if this effect already has targets assigned (from post-choice targeting)
        if (effect.targetUids.Count > 0) {
            return;
        }
        List<int> possibleTargets = GetPossibleTargets(player, effect);
        // Skip target selection if there are no valid targets (ability fizzles - resolves with no effect)
        if (possibleTargets.Count == 0) {
            return;
        }
        string message = effect.EffectToString(this);

        // Check if this effect requires one target from each player
        bool requireOneFromEach = effect.target?.requireOneFromEach ?? false;
        List<int>? playerUids = null;
        List<int>? opponentUids = null;

        if (requireOneFromEach) {
            Player opponent = GetOpponent(player);
            playerUids = possibleTargets.Where(uid => {
                if (cardByUid.TryGetValue(uid, out Card? c)) {
                    return GetControllerOf(c) == player;
                }
                return false;
            }).ToList();
            opponentUids = possibleTargets.Where(uid => {
                if (cardByUid.TryGetValue(uid, out Card? c)) {
                    return GetControllerOf(c) == opponent;
                }
                return false;
            }).ToList();
        }

        CreateAndAddNewTargetSelectionEvent(player, possibleTargets, targetAmount, message, minTargetAmount, requireOneFromEach, playerUids, opponentUids);
        effectsWithTargets.Add(effect);
    }

    private void CreateAndAddNewTargetSelectionEvent(Player player, List<int> targetableUids, int amount, string? message = null, int minAmount = -1, bool requireOneFromEach = false, List<int>? playerUids = null, List<int>? opponentUids = null) {
        TargetSelection newTargetSelection = new TargetSelection(targetableUids, amount, message, minAmount);
        newTargetSelection.requireOneFromEach = requireOneFromEach;
        newTargetSelection.playerUids = playerUids;
        newTargetSelection.opponentUids = opponentUids;
        GameEvent gEvent = GameEvent.CreateTargetSelectionEvent(newTargetSelection);
        AddEventForPlayer(player, gEvent);
    }

    // For resolve-time target selection (e.g., Consider: select cards after drawing)
    public Effect? resolveTimeTargetEffect;

    /// <summary>
    /// Requests resolve-time target selection. Returns true if waiting for input, false if no valid targets.
    /// </summary>
    public bool RequestResolveTimeTargets(Player player, Effect effect) {
        List<int> possibleTargets = GetPossibleTargets(player, effect);

        // For OpponentHand targets, reveal only valid targets (filtered by restrictions)
        if (effect.GetTargetType() == TargetType.OpponentHand) {
            Player opponent = GetOpponent(player);
            foreach (int uid in possibleTargets) {
                if (cardByUid.TryGetValue(uid, out Card? c)) {
                    Reveal(opponent, c);
                }
            }
        }

        // If no valid targets, skip selection (effect fizzles this part)
        if (possibleTargets.Count == 0) {
            return false;
        }

        resolveTimeTargetEffect = effect;
        int targetAmount = effect.GetTargetMax();
        int minTargetAmount = effect.GetTargetMin();

        // Generate message based on effect type
        string message = GenerateResolveTimeSelectionMessage(effect, targetAmount, minTargetAmount, possibleTargets.Count);

        // If opponentsChoice, the opponent selects the target instead of the caster
        Player selectingPlayer = effect.opponentsChoice ? GetOpponent(player) : player;
        CreateAndAddNewTargetSelectionEvent(selectingPlayer, possibleTargets, targetAmount, message, minTargetAmount);
        return true;
    }

    private string GenerateResolveTimeSelectionMessage(Effect effect, int maxAmount, int minAmount, int availableCount) {
        // Handle "any number" case - when min is 0 and max is >= available cards
        if (minAmount == 0 && maxAmount >= availableCount) {
            return "Select any number of cards.";
        }

        // Handle "up to X" case - when min is 0 but max is less than available
        if (minAmount == 0 && maxAmount < availableCount) {
            return $"Select up to {maxAmount} card{(maxAmount == 1 ? "" : "s")}.";
        }

        string plural = maxAmount == 1 ? "" : "s";

        if (effect.effect == EffectType.SendToZone && effect.destination == Zone.Deck) {
            // Check if there's a shuffleDeck effect after this one
            bool willShuffle = false;
            if (effect.parentEffectList != null) {
                int myIndex = effect.parentEffectList.IndexOf(effect);
                for (int i = myIndex + 1; i < effect.parentEffectList.Count; i++) {
                    if (effect.parentEffectList[i].effect == EffectType.ShuffleDeck) {
                        willShuffle = true;
                        break;
                    }
                }
            }

            if (willShuffle) {
                return $"Shuffle {maxAmount} card{plural} into your deck.";
            }

            string position = effect.deckDestination switch {
                DeckDestinationType.Bottom => "bottom",
                DeckDestinationType.Top => "top",
                _ => ""
            };
            return $"Put {maxAmount} card{plural} on the {position} of your library.";
        }

        // Default message
        return $"Select {maxAmount} card{plural}.";
    }

    /// <summary>
    /// Requests user selection from a zone during stack resolution.
    /// Used for effects with resolveTarget and select (e.g., Consider: select cards from hand).
    /// </summary>
    public void RequestResolveTimeSelection(Player player, Effect effect) {
        resolveTimeTargetEffect = effect;  // Reuse same field as targeting

        // Get cards from the zone(s) specified in select
        List<Zone> zones = effect.GetSelectZones();
        List<int> selectableUids = new();
        Qualifier qualifier = new Qualifier(effect, player);

        // For sacrifice effects with tokenType, select from tokens in play
        if (effect.effect == EffectType.Sacrifice && effect.tokenType != null) {
            foreach (Token t in player.tokens) {
                if (t.tokenType == effect.tokenType) {
                    selectableUids.Add(t.uid);
                }
            }
        } else {
            foreach (Zone zone in zones) {
                List<Card> cardsInZone = zone switch {
                    Zone.Hand => player.hand.ToList(),
                    Zone.Graveyard => player.graveyard.ToList(),
                    Zone.Deck => player.deck.ToList(),
                    _ => new List<Card>()
                };

                foreach (Card c in cardsInZone) {
                    if (QualifyCard(c, qualifier)) {
                        selectableUids.Add(c.uid);
                    }
                }
            }
        }

        int selectionMin = effect.GetSelectMin();
        int selectionMax = effect.select?.upToAll == true ? selectableUids.Count : effect.GetSelectMax();

        // Generate message based on effect and destination
        string message = GenerateZoneSelectionMessage(effect, selectionMin, selectionMax);

        Console.WriteLine($"[RequestResolveTimeSelection] zones={string.Join(",", zones)}, selectableCount={selectableUids.Count}, min={selectionMin}, max={selectionMax}");

        // Use the same event type as targeting, client handles it the same way
        CreateAndAddNewTargetSelectionEvent(player, selectableUids, selectionMax, message, selectionMin);
    }

    private string GenerateZoneSelectionMessage(Effect effect, int min, int max) {
        // Handle sacrifice effects
        if (effect.effect == EffectType.Sacrifice) {
            string tokenName = effect.tokenType?.ToString()?.ToLower() ?? "token";
            if (min == 0) {
                return $"Choose any number of {tokenName}s to sacrifice.";
            } else if (min == max) {
                string plural = max == 1 ? "" : "s";
                return $"Choose {max} {tokenName}{plural} to sacrifice.";
            } else {
                return $"Choose {min} to {max} {tokenName}s to sacrifice.";
            }
        }

        string destDesc = effect.destination switch {
            Zone.Deck when effect.deckDestination == DeckDestinationType.Bottom => "put on the bottom of your library",
            Zone.Deck when effect.deckDestination == DeckDestinationType.Top => "put on top of your library",
            Zone.Hand => "return to your hand",
            Zone.Graveyard => "discard",
            Zone.Exile => "exile",
            _ => "select"
        };

        if (min == 0) {
            return $"Choose up to {max} cards to {destDesc}.";
        } else if (min == max) {
            string plural = max == 1 ? "" : "s";
            return $"Choose {max} card{plural} to {destDesc}.";
        } else {
            return $"Choose {min} to {max} cards to {destDesc}.";
        }
    }

    /// <summary>
    /// Requests user selection for a cost effect (isCost: true) during stack resolution.
    /// </summary>
    public void RequestCostEffectSelection(Player player, Effect effect, List<int> selectableUids) {
        costEffectForSelection = effect;

        // Generate message based on effect type
        string message = effect.effect switch {
            EffectType.Sacrifice => $"Sacrifice a {effect.tokenType?.ToString()?.ToLower() ?? "token"}.",
            EffectType.Reveal => $"Reveal a {effect.tribe?.ToString()?.ToLower() ?? effect.cardType?.ToString()?.ToLower() ?? "card"} from your hand.",
            _ => "Select a target for the cost."
        };

        CostType costType = effect.effect switch {
            EffectType.Sacrifice => CostType.Sacrifice,
            EffectType.Reveal => CostType.Reveal,
            _ => CostType.Discard
        };

        // Create a cost event (uses same client-side handling as other costs)
        GameEvent gEvent = GameEvent.CreateCostEvent(
            costType,
            1,  // amount
            selectableUids,
            new List<string> { message }
        );
        AddEventForPlayer(player, gEvent);
    }

    public List<int> GetPossibleTargets(Player player, Effect effect) {
        TargetType? targetType = effect.GetTargetType();
        Debug.Assert(targetType != null, "There is no effect TargetType (GetPossibleTargets)");

        // For Counter effects, targets are on the stack, not in play
        if (effect.effect == EffectType.Counter) {
            List<int> stackUids = new List<int>();
            foreach (StackObj stackObj in stack) {
                // Skip the counter spell itself (it's on top of the stack)
                if (stackObj.sourceCard.uid == effect.sourceCard?.uid) continue;
                stackUids.Add(stackObj.sourceCard.uid);
            }
            return stackUids.Where(uid => QualifyTarget(uid, effect, player)).ToList();
        }

        List<int> allUids = allCardsInPlay.Select(c => c.uid).ToList();
        allUids.Add(playerOne.uid);
        allUids.Add(playerTwo.uid);
        // Add token UIDs for effects that can target tokens
        allUids.AddRange(playerOne.tokens.Select(t => t.uid));
        allUids.AddRange(playerTwo.tokens.Select(t => t.uid));
        // Add hand card UIDs for CardInHand target type
        if (targetType == TargetType.CardInHand) {
            allUids.AddRange(player.hand.Select(c => c.uid));
        }
        // Add opponent's hand card UIDs for OpponentHand target type
        if (targetType == TargetType.OpponentHand) {
            allUids.AddRange(GetOpponent(player).hand.Select(c => c.uid));
        }
        // Add hand and graveyard card UIDs for CardInHandOrGraveyard target type
        if (targetType == TargetType.CardInHandOrGraveyard) {
            allUids.AddRange(player.hand.Select(c => c.uid));
            allUids.AddRange(player.graveyard.Select(c => c.uid));
        }
        // Add graveyard card UIDs for CardInGraveyard target type
        if (targetType == TargetType.CardInGraveyard) {
            allUids.AddRange(player.graveyard.Select(c => c.uid));
        }
        // Add both players' graveyard card UIDs for Graveyard target type (any graveyard)
        if (targetType == TargetType.Graveyard) {
            allUids.AddRange(playerOne.graveyard.Select(c => c.uid));
            allUids.AddRange(playerTwo.graveyard.Select(c => c.uid));
        }
        return allUids.Where(uid => QualifyTarget(uid, effect, player)).ToList();
    }

    public int GetAmountBasedOn(AmountBasedOn? amountBasedOn, Scope scope = Scope.All, Player? player = null, Effect? rootEffect = null, CardType? cardType = null,
        List<Restriction>? restrictions = null, Card? sourceCard = null) {
        int modAmount = 0;
        if (scope == Scope.OthersOnly) modAmount = -1;  // Exclude self from count
        int tempAmount = 0;
        switch (amountBasedOn) {
            case AmountBasedOn.GoblinsInPlay:
                tempAmount = GetAllCardsOfTribe(Tribe.Goblin).Count + modAmount;
                break;
            case AmountBasedOn.GoblinsControlled:
                tempAmount = GetAllCardsOfTribe(Tribe.Goblin, player).Count + modAmount;
                break;
            case AmountBasedOn.OpponentExcessSummons:
                Debug.Assert(player != null, "there is no player to check for excess summons");
                tempAmount = GetExcessSummons(player, GetOpponent(player), restrictions);
                break;
            case AmountBasedOn.StonesControlled:
                Debug.Assert(player != null, "there is no player to check for controlled summons");
                foreach (Token t in player.tokens) {
                    if (t.tokenType is TokenType.Stone) tempAmount++;
                }
                break;
            case AmountBasedOn.StonesInPlay:
                // Count stones from all players
                foreach (Token t in playerOne.tokens) {
                    if (t.tokenType is TokenType.Stone) tempAmount++;
                }
                foreach (Token t in playerTwo.tokens) {
                    if (t.tokenType is TokenType.Stone) tempAmount++;
                }
                break;
            case AmountBasedOn.RootAmount:
                Debug.Assert(rootEffect != null, "there is no rootEffect to obtain an amount from");
                Debug.Assert(rootEffect.amount != null, "there is no amount in the root effect");
                tempAmount = rootEffect.amount.Value;
                break;
            case AmountBasedOn.UntilCardType:
                Debug.Assert(cardType != null, "there is no card type for this GetAmountBasedOn");
                Debug.Assert(player != null, "there is no player for this GetAmountBasedOn");
                tempAmount = GetAmountUntilCardType(cardType, player);
                break;
            case AmountBasedOn.LifeTotal:
                Debug.Assert(player != null, "there is no player for this GetAmountBasedOn");
                tempAmount = player.lifeTotal;
                break;
            case AmountBasedOn.SubtractLife:
                Debug.Assert(player != null, "there is no player for SubtractLife");
                tempAmount = -player.lifeTotal;
                break;
            case AmountBasedOn.RootAffected:
                Debug.Assert(rootEffect != null, "there is no rootEffect to obtain an amount from");
                Debug.Assert(rootEffect.affectedUids != null, "there are no affected uids from rootEffect");
                tempAmount = rootEffect.affectedUids.Count;
                break;
            case AmountBasedOn.X:
                Debug.Assert(sourceCard != null, "there is no source card to pull X from");
                Debug.Assert(sourceCard.x != null, "x is not set for the source card");
                tempAmount = (int)sourceCard.x;
                break;
            case AmountBasedOn.HerbSacrificeLifeGain:
                // First herb gives 2 life, subsequent herbs give 1 life each
                // Unless bypassHerbLifeReduction is active (from Herblore), then always 2
                Debug.Assert(player != null, "there is no player for HerbSacrificeLifeGain");
                if (player.bypassHerbLifeReduction) {
                    tempAmount = 2;  // Always 2 when bypass is active
                } else {
                    tempAmount = player.turnHerbSacrificeCount == 0 ? 2 : 1;
                }
                // Increment the counter after calculating (so this herb counts for subsequent ones)
                player.turnHerbSacrificeCount++;
                break;
            case AmountBasedOn.TargetCost:
                Debug.Assert(rootEffect != null, "there is no rootEffect for TargetCost");
                Debug.Assert(rootEffect.targetUids.Count > 0, "there are no targetUids for TargetCost");
                Debug.Assert(cardByUid.ContainsKey(rootEffect.targetUids[0]), "could not find target card for TargetCost");
                tempAmount = cardByUid[rootEffect.targetUids[0]].GetCost();
                break;
            case AmountBasedOn.TargetPower:
                Debug.Assert(rootEffect != null, "there is no rootEffect for TargetPower");
                Debug.Assert(rootEffect.targetUids.Count > 0, "there are no targetUids for TargetPower");
                Debug.Assert(cardByUid.ContainsKey(rootEffect.targetUids[0]), "could not find target card for TargetPower");
                Card powerTargetCard = cardByUid[rootEffect.targetUids[0]];
                Debug.Assert(powerTargetCard.attack != null, "target card has no attack for TargetPower");
                tempAmount = powerTargetCard.attack.Value;
                break;
            case AmountBasedOn.MerfolkInGraveyard:
                Debug.Assert(player != null, "there is no player for MerfolkInGraveyard");
                tempAmount = player.graveyard.Count(c => c.tribe == Tribe.Merfolk && c.type == CardType.Summon);
                break;
            case AmountBasedOn.SummonsInGraveyard:
                Debug.Assert(player != null, "there is no player for SummonsInGraveyard");
                tempAmount = player.graveyard.Count(c => c.type == CardType.Summon);
                break;
            case AmountBasedOn.DeckSize:
                Debug.Assert(player != null, "there is no player for DeckSize");
                tempAmount = player.deck?.Count ?? 0;
                break;
            case AmountBasedOn.SummonsOpponentControls:
                Debug.Assert(player != null, "there is no player for SummonsOpponentControls");
                Player opponent = GetOpponent(player);
                tempAmount = opponent.playField.Count;
                break;
            case AmountBasedOn.TreefolkControlled:
                Debug.Assert(player != null, "there is no player for TreefolkControlled");
                tempAmount = player.playField.Count(c => c.tribe == Tribe.Treefolk);
                break;
            case AmountBasedOn.HerbsControlled:
                Debug.Assert(player != null, "there is no player for HerbsControlled");
                tempAmount = player.tokens.Count(t => t.tokenType == TokenType.Herb);
                break;
            case AmountBasedOn.RedCardsDiscardedForCost:
                Debug.Assert(sourceCard != null, "there is no source card for RedCardsDiscardedForCost");
                tempAmount = sourceCard.redCardsDiscardedForCost;
                break;
            case AmountBasedOn.SummonsThatDiedThisTurn:
                tempAmount = summonsThatDiedThisTurn;
                break;
            case AmountBasedOn.HalfLife:
                Debug.Assert(player != null, "there is no player for HalfLife");
                tempAmount = player.lifeTotal / 2;  // Integer division rounds down
                break;
            case AmountBasedOn.Attack:
                Debug.Assert(sourceCard != null, "there is no source card for Attack amountBasedOn");
                Debug.Assert(sourceCard.attack != null, "source card has no attack value");
                tempAmount = sourceCard.attack.Value;
                break;
            case AmountBasedOn.FinalAttack:
                Debug.Assert(sourceCard != null, "there is no source card for FinalAttack amountBasedOn");
                Debug.Assert(sourceCard.attack != null, "source card has no attack value");
                tempAmount = sourceCard.GetAttack();
                break;
            default:
                Console.WriteLine("Unknown AmountBasedOn value: " + amountBasedOn);
                return -69;
        }

        return tempAmount; 
    }

    private List<int> GetAttackCapableUids(Player player) {
        if (player.cantAttackThisTurn) return new List<int>();

        List<int> tempList = new();
        foreach (Card c in player.playField) {
            // Object type cards cannot attack
            if (c.type == CardType.Object) continue;
            if (c.HasSummoningSickness()) continue;
            // Check for CantAttack passive on the card
            if (c.HasPassive(Passive.CantAttack)) continue;
            tempList.Add(c.uid);
        }

        return tempList;
    }

    public List<int> GetAttackables(Player attackingPlayer, Card attackingCard) {
        return GetAttackableUids(attackingPlayer, attackingCard);
    }

    private List<int> GetAttackableUids(Player player, Card attackingCard) {
        List<int> attackableUids = new();
        Player opponent = GetOpponent(player);
        bool attackerHasSpectral = DetectKeyword(attackingCard, Keyword.Spectral);

        // Check for Taunt summons first - if any exist, ONLY they can be attacked
        List<int> tauntUids = new();
        foreach (Card c in opponent.playField) {
            if (DetectKeyword(c, Keyword.Taunt)) {
                // Spectral summons can only be attacked by Spectral attackers
                if (DetectKeyword(c, Keyword.Spectral) && !attackerHasSpectral) continue;
                tauntUids.Add(c.uid);
            }
        }

        // If there are Taunt summons, only they can be attacked (no other summons, no player)
        if (tauntUids.Count > 0) {
            return tauntUids;
        }

        // No Taunt summons - normal attack logic
        foreach (Card c in opponent.playField) {
            // Spectral summons can only be attacked by Spectral attackers
            if (DetectKeyword(c, Keyword.Spectral) && !attackerHasSpectral) continue;

            attackableUids.Add(c.uid);
        }

        // if it has dive, add opponent if all dive-immune summons are being attacked
        // dive bypasses normal summons but not summons immune to dive (e.g., Undead Goblin)
        if (DetectKeyword(attackingCard, Keyword.Dive)) {
            if (AllDiveImmuneSummonsAreBeingAttacked(player)) {
                attackableUids.Add(opponent.uid);
            }
        } else {
            if (AllOpponentSummonsAreBeingAttacked(player)) attackableUids.Add(opponent.uid);
        }

        return attackableUids;
    }

    private void FinishWithTriggers(Player player, Player playerToPassTo) {
        if (player == GetPlayerByTurn(true)) {
            HandleTriggers(GetPlayerByTurn(false), playerToPassTo);
        } else {
            player.controlledTriggers.Clear();
            GetOpponent(player).controlledTriggers.Clear();
            PassPrioToPlayer(playerToPassTo);
        }
    }

    private void CreateAndAddOrderingEvent(Player player) {
        List<StackDisplayData> tempOrderingList = new();
        foreach (TriggeredEffect tEffect in player.controlledTriggers) {
            tempOrderingList.Add(new StackDisplayData(CreateStackObj(player, tEffect.sourceCard, tEffect), this));
        }

        GameEvent gEvent = GameEvent.CreateTriggerOrderingEvent(tempOrderingList);
        AddEventForPlayer(player, gEvent);
    }

    public void AddOrderedTriggersToStack(int accountId, List<int> finalOrderList) {
        Player player = accountIdToPlayer[accountId];

        // Validate indices - if they're out of range, the client sent stale ordering data
        if (finalOrderList.Any(i => i < 0 || i >= player.controlledTriggers.Count)) {
            // Use default order (0, 1, 2, ...)
            finalOrderList = Enumerable.Range(0, player.controlledTriggers.Count).ToList();
        }

        var tempList = new List<TriggeredEffect>(player.controlledTriggers);
        player.controlledTriggers.Clear();
        foreach (int i in finalOrderList) {
            player.controlledTriggers.Add(tempList[i]);
        }

        foreach (TriggeredEffect tEffect in player.controlledTriggers) {
            AddStackObjToStack(CreateStackObj(player, tEffect.sourceCard, tEffect));
        }

        player.controlledTriggers.Clear();
        Debug.Assert(currentPlayerToPassTo != null, "there is no current player to pass to");
        FinishWithTriggers(player, currentPlayerToPassTo);
    }

    private void PassPrioToPlayer(Player player) {
        // Clear the other player's playables/activatables when priority changes
        Player opponent = GetOpponent(player);
        opponent.playables.Clear();
        opponent.activatables.Clear();

        prioPlayerId = player.playerId;
        CalculatePossibleMoves(player);

        // Auto-pass for bot players
        if (player.isBot) {
            PassPrio();
            return;
        }

        // Check if we should auto-skip phases
        if (ShouldAutoSkipPhases(player)) {
            // Track the start of skipping if not already tracking
            if (!isAutoSkipping) {
                skipStartPhase = currentPhase;
                isAutoSkipping = true;
                // Remember where we are in each player's event list so we only remove NextPhase events added during skip
                // We start from the CURRENT index - the first NextPhase of THIS skip will be added by the next GoToNextPhase call
                // Don't search backwards - that incorrectly includes NextPhase events from previous contexts (like turn transitions)
                skipStartEventIndexP1 = playerOne.eventList.Count;
                skipStartEventIndexP2 = playerTwo.eventList.Count;
            }
            PassPrio();
            return;
        }

        // We're stopping - convert consecutive NextPhase events to SkipToPhase if applicable
        FinalizePhaseSkip();

        // Clear passToPhase for any player who has reached or passed their target
        // This prevents the client from continuing to autopass after the target is reached
        if (HasPlayerReachedTarget(playerOne)) {
            playerOne.passToPhase = null;
            playerOne.passToMyMain = false;
        }
        if (HasPlayerReachedTarget(playerTwo)) {
            playerTwo.passToPhase = null;
            playerTwo.passToMyMain = false;
        }

        GameEvent gEvent = new GameEvent(EventType.GainPrio);
        AddEventForPlayer(player, gEvent);
    }

    /// <summary>
    /// Determines if we should auto-skip phases.
    /// For bot games: only the human player needs passToPhase set.
    /// For PvP: both players need passToPhase set.
    /// When stack has items: only autopass if player's autopassPausedForStack is false.
    /// Stops at Combat if turn player has attack-capable creatures.
    /// </summary>
    private bool ShouldAutoSkipPhases(Player player) {
        // If stack has items, check if this player's autopass is paused
        if (stack.Count > 0) {
            if (player.autopassPausedForStack) {
                return false;
            }
            // Even if this player resumed autopass, we still need to stop to let them respond
            // Only continue auto-skip if BOTH players have resumed (clicked autopass button)
            Player opponent = GetOpponent(player);
            if (opponent.autopassPausedForStack) {
                return false;
            }
        }

        // Stop if turn player needs to discard to hand size at end of turn
        if (currentPhase == Phase.End) {
            Player turnPlayer = GetPlayerByTurn(true);
            if (turnPlayer.hand.Count > turnPlayer.maxHandSize) {
                return false;
            }
        }

        // Check if we have the required passToPhase settings
        bool p1HasPass = playerOne.passToPhase.HasValue || playerOne.isBot;
        bool p2HasPass = playerTwo.passToPhase.HasValue || playerTwo.isBot;

        if (!p1HasPass || !p2HasPass) {
            return false;
        }

        // Check if the player RECEIVING priority has reached their target
        // Only check this player, not both - if the other player already passed at their target, we can continue
        // BUT: If there are items on the stack, don't stop for target phases - let the stack fully resolve first
        if (stack.Count == 0 && HasPlayerReachedTarget(player)) {
            return false;
        }

        // Stop at Combat phase if turn player has creatures that can attack
        if (currentPhase == Phase.Combat) {
            Player turnPlayer = GetPlayerByTurn(true);
            var attackCapable = GetAttackCapableUids(turnPlayer);
            if (attackCapable.Count > 0) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks if a player has reached their passToPhase target.
    /// For "passToMyMain", the player must be the turn player AND on Main phase.
    /// </summary>
    private bool HasPlayerReachedTarget(Player player) {
        // Bots never have a target to reach
        if (player.isBot) {
            return false;
        }

        // No passToPhase set means no target
        if (!player.passToPhase.HasValue) {
            return false;
        }

        Phase target = player.passToPhase.Value;

        // Special case: passToMyMain means we need to be on Main AND it's this player's turn
        if (player.passToMyMain) {
            return currentPhase == Phase.Main && turnPlayerId == player.playerId;
        }

        // Normal case: just check if we've reached the target phase
        return currentPhase >= target;
    }

    /// <summary>
    /// Gets the earliest phase that either player has set as their passToPhase target.
    /// Bots are treated as having passToPhase = End (they never want to stop early).
    /// For passToMyMain players who aren't the turn player, treat as End (skip past their target).
    /// </summary>
    private Phase GetSoonestPassToPhase() {
        Phase p1Target = GetEffectiveTargetPhase(playerOne);
        Phase p2Target = GetEffectiveTargetPhase(playerTwo);
        return p1Target <= p2Target ? p1Target : p2Target;
    }

    /// <summary>
    /// Gets the effective target phase for a player, accounting for passToMyMain.
    /// </summary>
    private Phase GetEffectiveTargetPhase(Player player) {
        if (player.isBot) {
            return Phase.End;
        }

        if (!player.passToPhase.HasValue) {
            return Phase.End;
        }

        // If passToMyMain and it's not their turn, they want to skip to End (and beyond to their turn)
        if (player.passToMyMain && turnPlayerId != player.playerId) {
            return Phase.End;
        }

        return player.passToPhase.Value;
    }

    /// <summary>
    /// Gets the phase before the given phase (wraps from Draw to End).
    /// </summary>
    private Phase GetPreviousPhase(Phase phase) {
        if (phase == Phase.Draw) return Phase.End;
        return phase - 1;
    }

    /// <summary>
    /// If we were auto-skipping and skipped multiple phases, replace the NextPhase events
    /// with a single SkipToPhase event.
    /// </summary>
    private void FinalizePhaseSkip() {
        Console.WriteLine($"[FinalizePhaseSkip] isAutoSkipping={isAutoSkipping}, skipStartPhase={skipStartPhase}");
        if (!isAutoSkipping || !skipStartPhase.HasValue) {
            Console.WriteLine($"[FinalizePhaseSkip] Early return - not auto-skipping");
            return;
        }

        Phase startPhase = skipStartPhase.Value;

        // Count actual NextPhase events added during the skip (more accurate than phase calculation)
        int phasesSkipped = CountNextPhaseEventsSinceSkipStart(playerOne);
        Console.WriteLine($"[FinalizePhaseSkip] startPhase={startPhase}, phasesSkipped={phasesSkipped}, skipStartEventIndexP1={skipStartEventIndexP1}, eventListCount={playerOne.eventList.Count}");

        // Only create SkipToPhase if we skipped 2+ phases
        if (phasesSkipped >= 2) {
            Console.WriteLine($"[FinalizePhaseSkip] Creating SkipToPhase event");
            // Remove the individual NextPhase events and replace with SkipToPhase
            ReplaceNextPhaseEventsWithSkipToPhase(startPhase, phasesSkipped);
        } else {
            Console.WriteLine($"[FinalizePhaseSkip] Not enough phases skipped ({phasesSkipped} < 2), keeping NextPhase events");
        }

        // Reset tracking
        isAutoSkipping = false;
        skipStartPhase = null;
    }

    /// <summary>
    /// Counts NextPhase events in the player's event list since skip started.
    /// </summary>
    private int CountNextPhaseEventsSinceSkipStart(Player player) {
        int startIndex = player == playerOne ? skipStartEventIndexP1 : skipStartEventIndexP2;
        int count = 0;
        for (int i = startIndex; i < player.eventList.Count; i++) {
            if (player.eventList[i].eventType == EventType.NextPhase) {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Removes NextPhase events (added during skip) from both players' event lists and adds a SkipToPhase event.
    /// </summary>
    private void ReplaceNextPhaseEventsWithSkipToPhase(Phase startPhase, int phasesSkipped) {
        Console.WriteLine($"[ReplaceNextPhase] startPhase={startPhase}, phasesSkipped={phasesSkipped}");
        Console.WriteLine($"[ReplaceNextPhase] P1 eventList BEFORE removal (count={playerOne.eventList.Count}, skipStartIdx={skipStartEventIndexP1}):");
        for (int i = 0; i < playerOne.eventList.Count; i++) {
            var e = playerOne.eventList[i];
            Console.WriteLine($"  [{i}] {e.eventType}");
        }
        Console.WriteLine($"[ReplaceNextPhase] P2 eventList BEFORE removal (count={playerTwo.eventList.Count}, skipStartIdx={skipStartEventIndexP2}):");
        for (int i = 0; i < playerTwo.eventList.Count; i++) {
            var e = playerTwo.eventList[i];
            Console.WriteLine($"  [{i}] {e.eventType}");
        }

        // Remove NextPhase events that were added during the skip (after skipStartEventIndex)
        int p1Removed = RemoveNextPhaseEventsFromSkip(playerOne);
        int p2Removed = RemoveNextPhaseEventsFromSkip(playerTwo);
        Console.WriteLine($"[ReplaceNextPhase] Removed {p1Removed} from P1, {p2Removed} from P2");
        Console.WriteLine($"[ReplaceNextPhase] P1 eventList AFTER removal: count={playerOne.eventList.Count}");
        Console.WriteLine($"[ReplaceNextPhase] P2 eventList AFTER removal: count={playerTwo.eventList.Count}");

        // Add SkipToPhase event for both players
        GameEvent skipEvent = new GameEvent(EventType.SkipToPhase);
        skipEvent.amount = phasesSkipped;  // number of phases to animate through
        skipEvent.universalInt = (int)startPhase;  // starting phase
        Console.WriteLine($"[ReplaceNextPhase] Adding SkipToPhase: amount={phasesSkipped}, startPhase={startPhase}");
        AddEventForBothPlayers(GetPlayerByTurn(true), skipEvent);
    }

    /// <summary>
    /// Removes NextPhase events from a player's event list that were added during the skip.
    /// Only removes events at or after that player's skip start index.
    /// Returns the number actually removed.
    /// </summary>
    private int RemoveNextPhaseEventsFromSkip(Player player) {
        int startIndex = player == playerOne ? skipStartEventIndexP1 : skipStartEventIndexP2;
        int removed = 0;
        // Start from the end and only go back to this player's skip start index
        for (int i = player.eventList.Count - 1; i >= startIndex; i--) {
            if (player.eventList[i].eventType == EventType.NextPhase) {
                player.eventList.RemoveAt(i);
                removed++;
            }
        }
        return removed;
    }

    private StackObj CreateStackObj(Player player, Card stackObjCard, TriggeredEffect? triggeredEffect = null,
        ActivatedEffect? aEffect = null) {
        List<Effect> effectsList = new();
        if (triggeredEffect != null) {
            if (triggeredEffect.effects != null) {
                foreach (Effect e in triggeredEffect.effects) {
                    Effect createdEffect = Effect.CreateEffect(e, stackObjCard);
                    // If effect targets the trigger card, set its targetUids
                    if (createdEffect.targetBasedOn == TargetBasedOn.TriggerCard && triggeredEffect.triggerCard != null) {
                        createdEffect.targetUids.Add(triggeredEffect.triggerCard.uid);
                    }
                    // If effect targets the trigger controller, set its targetUids to the player's UID
                    if (createdEffect.targetBasedOn == TargetBasedOn.TriggerController && triggeredEffect.triggerController != null) {
                        createdEffect.targetUids.Add(triggeredEffect.triggerController.uid);
                    }
                    effectsList.Add(createdEffect);
                }
            }

            return new StackObj(stackObjCard, StackObjType.TriggeredEffect, effectsList, stackObjCard.currentZone,
                player, triggeredEffect.description);
        }

        if (aEffect != null) {
            if (aEffect.effects != null) {
                foreach (Effect e in aEffect.effects) {
                    Effect cloned = Effect.CreateEffect(e, stackObjCard);
                    effectsList.Add(cloned);
                }
            }

            return new StackObj(stackObjCard, StackObjType.ActivatedEffect, effectsList, stackObjCard.currentZone,
                player, aEffect.description);
        }

        if (stackObjCard.stackEffects != null) {
            foreach (Effect e in stackObjCard.stackEffects) {
                effectsList.Add(Effect.CreateEffect(e, stackObjCard));
            }
        }

        return new StackObj(stackObjCard, StackObjType.Spell, effectsList, stackObjCard.currentZone, player);
    }

    private void AddStackObjToStack(StackObj stackObj) {
        FinalizePhaseSkip();
        stack.Push(stackObj);
        // Reset secondPass so the stack doesn't auto-resolve when priority is passed
        secondPass = false;
        // Pause autopass for both players - they must manually pass before autopassing resumes for this stack
        // (passToPhase is NOT cleared - just paused until player manually passes)
        playerOne.autopassPausedForStack = true;
        playerTwo.autopassPausedForStack = true;
        GameEvent gEvent = GameEvent.CreateStackEvent(EventType.Trigger, new StackDisplayData(stackObj, this));
        AddEventForBothPlayers(stackObj.player, gEvent);
    }

    public void AddRepeatToStack(StackObj stackObj) {
        // If something goes on the stack during auto-skipping, finalize the skip first
        FinalizePhaseSkip();

        stack.Push(stackObj);
        // Reset secondPass so the stack doesn't auto-resolve when priority is passed
        secondPass = false;
        // Pause autopass for both players - they must manually pass before autopassing resumes for this stack
        playerOne.autopassPausedForStack = true;
        playerTwo.autopassPausedForStack = true;
        Console.WriteLine($"[AddRepeatToStack] Paused autopass for both players - new stack item requires response");
        GameEvent gEvent = GameEvent.CreateStackEvent(EventType.Trigger, new StackDisplayData(stackObj, this));
        AddEventForBothPlayers(stackObj.player, gEvent);
    }

    /// <summary>
    /// Counters a spell/summon on the stack by removing it and sending the source card to graveyard
    /// </summary>
    /// <param name="uid">The uid of the card to counter</param>
    /// <returns>True if the card was successfully countered</returns>
    public bool CounterStackItem(int uid) {
        // Find the stack object with this card
        StackObj? targetStackObj = null;
        Stack<StackObj> tempStack = new Stack<StackObj>();

        // Pop items off the stack until we find the target
        while (stack.Count > 0) {
            StackObj current = stack.Pop();
            if (current.sourceCard.uid == uid) {
                targetStackObj = current;
                break;
            }
            tempStack.Push(current);
        }

        // Restore the stack (without the countered item)
        while (tempStack.Count > 0) {
            stack.Push(tempStack.Pop());
        }

        if (targetStackObj == null) {
            Console.WriteLine($"CounterStackItem: Could not find stack item with uid {uid}");
            return false;
        }

        Card counteredCard = targetStackObj.sourceCard;
        Player owner = targetStackObj.player;

        Console.WriteLine($"CounterStackItem: Countering {counteredCard.name} (uid={uid})");

        // Create a counter event for the client to remove the stack object and display message
        // Use focusUid to identify which stack object to remove
        GameEvent gEvent = GameEvent.CreateUidEvent(EventType.Counter, uid);
        gEvent.focusCard = new CardDisplayData(counteredCard);
        AddEventForBothPlayers(owner, gEvent);

        // Send the countered card to graveyard (after the counter event)
        SendToZone(owner, Zone.Graveyard, counteredCard);

        return true;
    }

    // not sure if this was required for anything

    // private void AddPotentialTriggersToStack(Player player) {
    //     foreach (PotentialTriggeredEffect ptEffect in player.potentialTriggeredEffects) {
    //         StackObj newStackObj = CreateStackObj(player, ptEffect.sourceCard, ptEffect.triggeredEffect);
    //         AddTriggerToStack(newStackObj);
    //     }
    // }

    private void CalculatePossibleMoves(Player player) {
        // TODO change playables to use uids instead of the carddisplaydata -> use events instead of playerstate
        player.playables.Clear();
        player.activatables.Clear();
        foreach (Card c in player.allCardsPlayer) {
            if (Utils.CheckPlayability(c, this, player)) {
                if (!player.playables.Contains(c)) {
                    switch (c.currentZone) {
                        case Zone.Hand:
                            player.playables.Add(c);
                            break;
                        case Zone.Play:
                            player.activatables.Add(c);
                            break;
                    }
                }
            }
        }
        // Check graveyard cards for activatable abilities (e.g., Ghostly Looter)
        foreach (Card c in player.graveyard) {
            if (Utils.CheckPlayability(c, this, player)) {
                if (!player.activatables.Contains(c)) {
                    player.activatables.Add(c);
                }
            }
        }
        // Check tokens for activatable abilities (e.g., granted by GrantActive passive)
        foreach (Token token in player.tokens) {
            if (Utils.CheckPlayability(token, this, player)) {
                if (!player.activatables.Contains(token)) {
                    player.activatables.Add(token);
                }
            }
        }

        // Check for TopCardRevealed and AdditionalSummonTopCard passives (Sky Scryer Merfolk)
        if (player.deck != null && player.deck.Count > 0) {
            bool hasTopCardRevealed = false;
            bool hasAdditionalSummonTopCard = false;
            foreach (Card cardInPlay in player.playField) {
                foreach (PassiveEffect pEffect in cardInPlay.GetPassives()) {
                    if (pEffect.passive == Passive.TopCardRevealed) hasTopCardRevealed = true;
                    if (pEffect.passive == Passive.AdditionalSummonTopCard) hasAdditionalSummonTopCard = true;
                }
            }

            // If has both passives, allow summoning the top card
            if (hasTopCardRevealed && hasAdditionalSummonTopCard) {
                Card topCard = player.deck.First();
                if (topCard.type == CardType.Summon && Utils.CheckPlayability(topCard, this, player, fromTopOfDeck: true)) {
                    if (!player.playables.Contains(topCard)) {
                        player.playables.Add(topCard);
                        Console.WriteLine($"[CalculatePossibleMoves] Added top deck card {topCard.name} to playables (AdditionalSummonTopCard)");
                    }
                }
            }
        }

        // Log results
        Console.WriteLine($"[CalculatePossibleMoves] {player.playerName}: playables={player.playables.Count}, activatables={player.activatables.Count}");
        foreach (Card c in player.activatables) {
            Console.WriteLine($"  - Activatable: {c.name} (uid={c.uid}, zone={c.currentZone})");
        }
    }

    private bool AllOpponentSummonsAreBeingAttacked(Player player) {
        Player opponent = GetOpponent(player);
        // has no summons or they all have been assigned attackers
        return opponent.playField.Count == 0 || opponent.playField.All(c => currentAttackUids.ContainsValue(c.uid));
    }

    /// <summary>
    /// Checks if all opponent summons that are immune to Dive are being attacked.
    /// Dive summons can bypass normal summons but must still attack dive-immune summons.
    /// </summary>
    private bool AllDiveImmuneSummonsAreBeingAttacked(Player player) {
        Player opponent = GetOpponent(player);
        var diveImmuneSummons = opponent.playField.Where(c => c.IsImmuneToKeyword(Keyword.Dive)).ToList();
        // if no dive-immune summons, can attack directly
        if (diveImmuneSummons.Count == 0) return true;
        // all dive-immune summons must be assigned attackers
        return diveImmuneSummons.All(c => currentAttackUids.ContainsValue(c.uid));
    }

    // Track if the current cast is free (no cost required) - used by effects that cast from graveyard
    private bool currentCastIsFree = false;

    public void AttemptToCast(Player attemptingPlayer, Card card, CastingStage stage = CastingStage.Initial, bool isAction = true, bool freeCast = false) {
        if (stage == CastingStage.Initial && freeCast) {
            currentCastIsFree = true;
        }
        switch (stage) {
            case CastingStage.Initial:
                cardBeingCast = card;
                // Check OnlySummonTribe player passive - can only summon specific tribe this turn
                if (card.type == CardType.Summon) {
                    PassiveEffect? tribeRestriction = attemptingPlayer.playerPassives.FirstOrDefault(p => p.passive == Passive.OnlySummonTribe);
                    if (tribeRestriction != null && card.tribe != tribeRestriction.tribe) {
                        Console.WriteLine($"[AttemptToCast] Blocked - player can only summon {tribeRestriction.tribe} this turn, but {card.name} is {card.tribe}");
                        cardBeingCast = null;
                        return;
                    }
                }
                // Check for hand abilities (activateFromHand) before proceeding with normal cast
                if (CheckForHandAbility(attemptingPlayer, card)) return;
                goto case CastingStage.AmountSelection;
            case CastingStage.AmountSelection:
                // X must be set before additional costs (for X-based sacrifice/life costs)
                if (CheckForXCost(attemptingPlayer, card)) return;
                goto case CastingStage.AdditionalCosts;
            case CastingStage.AdditionalCosts:
                if (CheckCardForAdditionalCosts(attemptingPlayer, card)) return;
                goto case CastingStage.Choices; 
            case CastingStage.Choices:
                Console.WriteLine($"[AttemptToCast] CastingStage.Choices for {card.name}");
                if (CheckForChoicesCard(attemptingPlayer, card)) return;
                goto case CastingStage.AlternateCost;
            case CastingStage.AlternateCost:
                Debug.Assert(cardBeingCast != null, "there is no card being cast for AttemptToCast()");
                // Skip cost handling for free casts (e.g., Goblin Ritualist casting from graveyard)
                if (currentCastIsFree) {
                    Console.WriteLine($"[AttemptToCast] Free cast - skipping cost for {cardBeingCast.name}");
                    break;  // Skip to end where CastCard is called
                }
                // Check for alternate costs on both spells and summons
                if (cardBeingCast.GetCost() > 0) {
                    AlternateCost? exileAltCost = GetPayableExileFromHandAlternateCost(attemptingPlayer, cardBeingCast);

                    // Handle SPELLS - alternate to paying life
                    if (cardBeingCast.type == CardType.Spell && exileAltCost != null) {
                        bool canPayLife = attemptingPlayer.lifeTotal > cardBeingCast.GetCost();
                        if (!canPayLife) {
                            // Only ExileFromHand available - use it automatically
                            usingAlternateCost = true;
                            currentAlternateCost = exileAltCost;
                            RequestAlternateCostPayment(attemptingPlayer, exileAltCost);
                            return;
                        }
                        // Both options available - ask player to choose
                        currentAlternateCost = exileAltCost;
                        string altCostDescription = GetAlternateCostDescription(exileAltCost);
                        var choicesText = new List<string> {
                            $"Pay life ({cardBeingCast.GetCost()} LP)",
                            altCostDescription
                        };
                        GameEvent gEvent = GameEvent.CreateOptionEvent(new PlayerChoice(choicesText, "Choose how to cast " + cardBeingCast.name));
                        AddEventForPlayer(attemptingPlayer, gEvent);
                        return;
                    }

                    // Handle SUMMONS - alternate to paying tribute
                    if (cardBeingCast.type == CardType.Summon) {
                        bool canPayTribute = cardBeingCast.GetCost() <= Utils.GetTributeValue(attemptingPlayer, cardBeingCast);

                        // Check ExileFromHand alternate cost
                        if (exileAltCost != null) {
                            if (!canPayTribute) {
                                // Only ExileFromHand available - use it automatically
                                usingAlternateCost = true;
                                currentAlternateCost = exileAltCost;
                                RequestAlternateCostPayment(attemptingPlayer, exileAltCost);
                                return;
                            }
                            // Both options available - ask player to choose
                            currentAlternateCost = exileAltCost;
                            string altCostDescription = GetAlternateCostDescription(exileAltCost);
                            var choicesText = new List<string> {
                                "Pay tribute (normal cost)",
                                altCostDescription
                            };
                            GameEvent gEvent = GameEvent.CreateOptionEvent(new PlayerChoice(choicesText, "Choose how to summon " + cardBeingCast.name));
                            AddEventForPlayer(attemptingPlayer, gEvent);
                            return;
                        }

                        // Check Sacrifice alternate cost
                        AlternateCost? sacrificeAltCost = GetPayableSacrificeAlternateCost(attemptingPlayer, cardBeingCast);
                        if (sacrificeAltCost != null) {
                            if (!canPayTribute) {
                                // Only alternate cost is available - use it automatically
                                usingAlternateCost = true;
                                currentAlternateCost = sacrificeAltCost;
                                RequestAlternateCostPayment(attemptingPlayer, sacrificeAltCost);
                                return;
                            }
                            // Both options available - ask player to choose
                            currentAlternateCost = sacrificeAltCost;
                            string altCostDescription = GetAlternateCostDescription(sacrificeAltCost);
                            var choicesText = new List<string> {
                                "Pay tribute (normal cost)",
                                altCostDescription
                            };
                            GameEvent gEvent = GameEvent.CreateOptionEvent(new PlayerChoice(choicesText, "Choose how to summon " + cardBeingCast.name));
                            AddEventForPlayer(attemptingPlayer, gEvent);
                            return;
                        }
                    }
                }
                goto case CastingStage.TargetSelection;
            case CastingStage.TargetSelection:
                Console.WriteLine($"[AttemptToCast] CastingStage.TargetSelection for {cardBeingCast?.name}");
                Debug.Assert(cardBeingCast != null, "there is no card being cast for AttemptToCast()");
                // check for targets for card being cast -> wait for player response by returning
                if (CheckForCardTargetSelection(attemptingPlayer, cardBeingCast)) {
                    Console.WriteLine($"[AttemptToCast] Target selection needed, returning");
                    return;
                }
                Console.WriteLine($"[AttemptToCast] No target selection needed, continuing to AdditionalChoices");
                goto case CastingStage.AdditionalChoices;
            case CastingStage.AdditionalChoices:
                if (additionalChoiceEffects.Count > 0) {
                    // create choice events for all additional choice effects
                    foreach (KeyValuePair<List<Effect>, Effect> pair in additionalChoiceEffects) {
                        Debug.Assert(pair.Value.choices != null, "there are no choices for this choice effect");
                        HandleChoice(pair.Value.choices, attemptingPlayer);
                    }
                    return;
                }
                goto case CastingStage.RepeatSelection;
            case CastingStage.RepeatSelection:
                Debug.Assert(cardBeingCast != null, "there is no card being cast for AttemptToCast()");
                // Check for effects with selectRepeatUpfront
                if (cardBeingCast.stackEffects != null) {
                    foreach (Effect effect in cardBeingCast.stackEffects) {
                        if (effect.selectRepeatUpfront && effect.repeatCostType != null && effect.repeatCostAmount != null) {
                            // Calculate max repeats based on player's remaining life after base cost
                            int baseCost = cardBeingCast.GetCost();
                            int lifeAfterCast = attemptingPlayer.lifeTotal - baseCost;
                            int maxRepeats = lifeAfterCast / effect.repeatCostAmount.Value;
                            if (maxRepeats > 0) {
                                // Store the effect and request repeat amount selection
                                effectWaitingForRepeatAmount = effect;
                                GameEvent gEvent = GameEvent.CreateRepeatAmountSelectionEvent(maxRepeats, effect.repeatCostAmount.Value);
                                AddEventForPlayer(attemptingPlayer, gEvent);
                                return;
                            }
                        }
                    }
                }
                goto case CastingStage.TributeSelection;
            case CastingStage.TributeSelection:
                Debug.Assert(cardBeingCast != null, "there is no card being cast for AttemptToCast()");
                // Skip tribute if using alternate cost (already paid)
                if (usingAlternateCost) {
                    usingAlternateCost = false;
                    currentAlternateCost = null;
                    break;
                }
                // activate tribute requirements for summons
                if (cardBeingCast.type == CardType.Summon && cardBeingCast.GetCost() > 0) {
                    cardRequiringTribute = cardBeingCast;
                    List<int> tributeableUids = new();
                    Dictionary<int, int> tributeValues = new();
                    // check for tribute restrictions on playField summons (each summon = 1 tribute)
                    foreach (Card c in attemptingPlayer.playField) {
                        if (!CardCanTributeTo(c, cardBeingCast)) continue;
                        tributeableUids.Add(c.uid);
                        tributeValues[c.uid] = 1;
                    }
                    // include tokens that can tribute (from alternateCosts tributeMultiplier)
                    if (cardBeingCast.alternateCosts != null) {
                        foreach (AlternateCost altCost in cardBeingCast.alternateCosts) {
                            if (altCost.altCostType != AltCostType.TributeMultiplier) continue;
                            // Check tokens (not yet summons)
                            foreach (Token token in attemptingPlayer.tokens) {
                                if (altCost.tokenType != null && token.tokenType == altCost.tokenType) {
                                    tributeableUids.Add(token.uid);
                                    tributeValues[token.uid] = altCost.amount;
                                } else if (altCost.tribe != null && token.tribe == altCost.tribe) {
                                    tributeableUids.Add(token.uid);
                                    tributeValues[token.uid] = altCost.amount;
                                }
                            }
                            // Check summons on field (including converted tokens)
                            foreach (Card summon in attemptingPlayer.playField) {
                                bool matches = false;
                                if (altCost.tokenType != null && summon is Token t && t.tokenType == altCost.tokenType) {
                                    matches = true;
                                } else if (altCost.tribe != null && summon.tribe == altCost.tribe) {
                                    matches = true;
                                } else if (altCost.cardType != null && summon.type == altCost.cardType) {
                                    matches = true;
                                }
                                if (matches && tributeableUids.Contains(summon.uid)) {
                                    // Update existing value (already counted as 1, now set to full multiplier)
                                    tributeValues[summon.uid] = altCost.amount;
                                }
                            }
                        }
                    }

                    // Check for TokenCanTribute passive (e.g., Tree of Abundance allows herbs as tributes)
                    foreach (Card c in attemptingPlayer.playField) {
                        if (c.passiveEffects == null) continue;
                        foreach (PassiveEffect pe in c.passiveEffects) {
                            if (pe.passive != Passive.TokenCanTribute) continue;
                            if (pe.tokenType == null) continue;
                            // Add matching tokens as tributeable
                            foreach (Token token in attemptingPlayer.tokens) {
                                if (token.tokenType == pe.tokenType && !tributeableUids.Contains(token.uid)) {
                                    tributeableUids.Add(token.uid);
                                    tributeValues[token.uid] = 1;  // Each token counts as 1 tribute
                                    Console.WriteLine($"[TokenCanTribute] {c.name} allows {token.tokenType} to tribute");
                                }
                            }
                        }
                    }

                    GameEvent gEvent =
                        GameEvent.CreateTributeRequirementEvent(new CardDisplayData(cardBeingCast), tributeableUids, tributeValues);
                    AddEventForPlayer(attemptingPlayer, gEvent);
                    return;
                }
                break;
            default:
                Console.WriteLine("There is no CastingStage for this AttemptToCast call");
                break;
        }

        Debug.Assert(cardBeingCast != null, "there is no card being cast for AttemptToCast()");
        // pay the life (skip for free casts and alternate costs)
        if (!currentCastIsFree && !usingAlternateCost && cardBeingCast.type != CardType.Summon && cardBeingCast.GetCost() > 0) {
            int costToPay = cardBeingCast.GetCost();
            // Finale spells can't reduce life below 1
            if (cardBeingCast.HasKeyword(Keyword.Finale)) {
                costToPay = Math.Min(costToPay, attemptingPlayer.lifeTotal - 1);
            }
            PayLifeCost(attemptingPlayer, costToPay);
        }
        // Reset free cast flag and alternate cost flag
        currentCastIsFree = false;
        usingAlternateCost = false;
        // cast the card
        CastCard(attemptingPlayer, cardBeingCast, isAction);
    }

    public void AttemptToActivate(Player attemptingPlayer, ActivatedEffect aEffect,
        ActivationStage stage = ActivationStage.Initial) {
        Debug.Assert(aEffect != null, "Activated Effect is null (AttemptToActivate)");
        // the Initial stage is not currently necessary.
        // it allows for scalability in the event that user input is required before cost payment.
        switch (stage) {
            case ActivationStage.Initial:
                currentActivatedEffect = aEffect;
                goto case ActivationStage.AmountSelection;
            case ActivationStage.AmountSelection:
                // For playerChosenAmount with sacrifice/discard, skip to CostPayment
                // which will create a variable cost event for direct card selection
                if (aEffect.playerChosenAmount &&
                    (aEffect.costType == CostType.Sacrifice || aEffect.costType == CostType.Discard)) {
                    goto case ActivationStage.CostPayment;
                }
                if (aEffect.playerChosenAmount) {
                    var gEvent = GameEvent.CreateAmountSelectionEvent(false);
                    AddEventForPlayer(attemptingPlayer, gEvent);
                    return;
                }
                goto case ActivationStage.CostPayment;
            case ActivationStage.CostPayment:
                // Handle self-sacrifice automatically
                if (aEffect.scope == Scope.SelfOnly && aEffect.costType == CostType.Sacrifice) {
                    PayCost(attemptingPlayer, CostType.Sacrifice, new List<Card> { aEffect.sourceCard });
                    goto case ActivationStage.Choices;
                }
                // Handle self-exile automatically
                if (aEffect.scope == Scope.SelfOnly && aEffect.costType == CostType.Exile) {
                    PayCost(attemptingPlayer, CostType.Exile, new List<Card> { aEffect.sourceCard });
                    goto case ActivationStage.Choices;
                }
                // Handle life costs automatically (no selection needed)
                if (aEffect.costType == CostType.LoseLife || aEffect.costType == CostType.Life) {
                    LoseLife(attemptingPlayer, aEffect.amount);
                    goto case ActivationStage.Choices;
                }
                AddCostEvent(attemptingPlayer, aEffect);
                return;
            case ActivationStage.Choices:
                if (CheckForChoicesActivatedEffect(attemptingPlayer, aEffect)) return;
                goto case ActivationStage.TargetSelection;
            case ActivationStage.TargetSelection:
                // if there are any effects requiring targets, handle target selection for all effects
                if (aEffect.effects != null && aEffect.effects.Any(effect => effect.HasTargeting())) {
                    foreach (Effect e in aEffect.effects) {
                        HandleEffectTargetSelection(attemptingPlayer, e);
                    }
                }
                // If there are effects waiting for targets, wait for player selection
                if (effectsWithTargets.Count > 0) return;
                break;
        }

        Debug.Assert(currentActivatedEffect != null, "there is no current activated effect");
        ActivateAbility(attemptingPlayer, currentActivatedEffect);
    }

    private void ActivateAbility(Player attemptingPlayer, ActivatedEffect aEffect) {
        currentActivatedEffect = null;
        // Mark as used for oncePerTurn tracking
        if (aEffect.oncePerTurn) {
            aEffect.usedThisTurn = true;
        }

        // Immediate abilities resolve directly without going on the stack
        if (aEffect.immediate) {
            Console.WriteLine($"[ActivateAbility] Resolving immediate ability from {aEffect.sourceCard?.name}");
            if (aEffect.effects != null) {
                foreach (Effect e in aEffect.effects) {
                    Effect effectClone = Effect.CreateEffect(e, aEffect.sourceCard);
                    effectClone.Resolve(this, attemptingPlayer);
                }
            }
            // Check for triggers from cost payment before passing priority
            CheckForTriggersAndPassives(EventType.Cast, attemptingPlayer);
            return;
        }

        AddStackObjToStack(CreateStackObj(attemptingPlayer, aEffect.sourceCard, null, aEffect));
        // Check for triggers from cost payment (e.g., discard triggers) before passing priority
        CheckForTriggersAndPassives(EventType.Cast, attemptingPlayer);
    }

    /// <summary>
    /// Activates a hand ability (activateFromHand). Discards the card as cost, then proceeds
    /// to target selection before adding to stack.
    /// </summary>
    private void ActivateHandAbility(Player player, ActivatedEffect aEffect) {
        Card sourceCard = aEffect.sourceCard;

        // Pay the cost: discard self from hand
        Debug.Assert(sourceCard.currentZone == Zone.Hand, "Hand ability source card must be in hand");
        Discard(player, sourceCard);

        // Proceed with target selection using normal AttemptToActivate flow
        currentActivatedEffect = aEffect;
        AttemptToActivate(player, aEffect, ActivationStage.TargetSelection);
    }

    private void AddVariableCostEvent(Player attemptingPlayer, AdditionalCost aCost, Card sourceCard) {
        Qualifier effectQualifier = new Qualifier(aCost, attemptingPlayer);
        List<int> selectableUidList = new();

        // Get matching cards based on cost type
        if (aCost.costType == CostType.Discard) {
            // For discard, iterate over hand and exclude the card being cast
            foreach (Card c in attemptingPlayer.hand) {
                if (c.uid == sourceCard.uid) continue;  // Exclude the card being cast
                if (QualifyCard(c, effectQualifier)) selectableUidList.Add(c.uid);
            }
        } else {
            // For sacrifice, get all matching cards/tokens in play
            foreach (Card c in attemptingPlayer.allCardsPlayer) {
                if (QualifyCard(c, effectQualifier)) selectableUidList.Add(c.uid);
            }
        }

        int maxAmount = selectableUidList.Count;
        string targetName = aCost.tokenType?.ToString() ?? "card";
        string costVerb = aCost.costType == CostType.Discard ? "discard" : "sacrifice";
        string message = $"{costVerb} any number of {targetName}s (0 to {maxAmount})";

        // Create cost event with variableAmount=true, amount=max
        GameEvent gEvent = GameEvent.CreateCostEvent(aCost.costType, maxAmount, selectableUidList,
            new List<string> { message }, variableAmount: true);
        AddEventForPlayer(attemptingPlayer, gEvent);
    }

    private void AddCostEvent(Player attemptingPlayer, ActivatedEffect? aEffect = null, AdditionalCost? aCost = null, Card? sourceCard = null) {
        // Handle alternate costs for activated abilities
        if (aEffect?.alternateCosts != null && aEffect.alternateCosts.Count > 0) {
            HandleActivatedAbilityAlternateCosts(attemptingPlayer, aEffect);
            return;
        }

        Qualifier effectQualifier;
        CostContext cc;
        if (aEffect != null) {
            cc = new CostContext(aEffect);
            effectQualifier = new Qualifier(aEffect, attemptingPlayer);
        } else {
            cc = new CostContext(aCost!, sourceCard);
            effectQualifier = new Qualifier(aCost!, attemptingPlayer);
        }
        List<int> selectableUidList = new();
        switch (cc.costType) {
            case CostType.Sacrifice:
                // get the list of possible selections (only cards in play - playField + tokens)
                foreach (Card c in attemptingPlayer.playField) {
                    if (QualifyCard(c, effectQualifier)) selectableUidList.Add(c.uid);
                }
                foreach (Token t in attemptingPlayer.tokens) {
                    if (QualifyCard(t, effectQualifier)) selectableUidList.Add(t.uid);
                }
                break;
            case CostType.Discard:
                foreach (Card c in attemptingPlayer.hand) {
                    if(QualifyCard(c, effectQualifier)) selectableUidList.Add(c.uid);
                }
                break;
        }

        // Handle variable amount costs for activated effects (playerChosenAmount)
        if (aEffect != null && aEffect.playerChosenAmount) {
            int maxAmount = selectableUidList.Count;
            string targetName = aEffect.tokenType?.ToString()?.ToLower() ?? "card";
            string costVerb = aEffect.costType == CostType.Discard ? "discard" : "sacrifice";
            string message = $"{costVerb} any number of {targetName}s (1 to {maxAmount})";

            GameEvent gEvent = GameEvent.CreateCostEvent(cc.costType, maxAmount, selectableUidList,
                new List<string> { message }, variableAmount: true, minAmount: 1);
            AddEventForPlayer(attemptingPlayer, gEvent);
            return;
        }

        // create and add the event for fixed amount costs
        List<string> eventMessageList = new() { GetCostMessage(cc) };
        GameEvent fixedCostEvent = GameEvent.CreateCostEvent(cc.costType, cc.amount,
            selectableUidList, eventMessageList);
        AddEventForPlayer(attemptingPlayer, fixedCostEvent);
    }

    private void HandleActivatedAbilityAlternateCosts(Player player, ActivatedEffect aEffect) {
        List<AlternateCost> payableCosts = new();
        foreach (AlternateCost altCost in aEffect.alternateCosts!) {
            if (CanPayActivatedAbilityAltCost(player, altCost)) {
                payableCosts.Add(altCost);
            }
        }

        if (payableCosts.Count == 0) {
            // No valid options (shouldn't happen if CostIsAvailable checked first)
            return;
        }

        if (payableCosts.Count == 1) {
            // Only one option available - use it directly
            currentActivatedAbilityAltCost = payableCosts[0];
            RequestActivatedAbilityAltCostPayment(player, payableCosts[0]);
            return;
        }

        // Multiple options available - present choice to player
        pendingActivatedAbilityAltCostChoice = true;
        var choicesText = payableCosts.Select(GetAlternateCostDescription).ToList();
        GameEvent choiceEvent = GameEvent.CreateOptionEvent(new PlayerChoice(choicesText, "Choose how to pay the cost:"));
        AddEventForPlayer(player, choiceEvent);
    }

    private bool CanPayActivatedAbilityAltCost(Player player, AlternateCost altCost) {
        int matchingCount = 0;
        switch (altCost.altCostType) {
            case AltCostType.Sacrifice:
            case AltCostType.Tribute:
                if (altCost.tokenType != null) {
                    matchingCount = player.tokens.Count(t => t.tokenType == altCost.tokenType);
                } else if (altCost.tribe != null) {
                    matchingCount = player.tokens.Count(t => t.tribe == altCost.tribe);
                    matchingCount += GetAllCardsControlled(player).Count(c => c.tribe == altCost.tribe);
                } else if (altCost.cardType != null) {
                    matchingCount = GetAllCardsControlled(player).Count(c => c.type == altCost.cardType);
                }
                break;
            case AltCostType.Discard:
            case AltCostType.ExileFromHand:
                foreach (Card c in player.hand) {
                    bool matches = true;
                    if (altCost.tribe != null && c.tribe != altCost.tribe) matches = false;
                    if (altCost.cardType != null && c.type != altCost.cardType) matches = false;
                    if (matches) matchingCount++;
                }
                break;
        }
        return matchingCount >= altCost.amount;
    }

    private void RequestActivatedAbilityAltCostPayment(Player player, AlternateCost altCost) {
        string targetName = altCost.tokenType?.ToString() ?? altCost.tribe?.ToString() ?? altCost.cardType?.ToString() ?? "card";
        string plural = altCost.amount > 1 ? "s" : "";
        List<int> selectableUids;
        string message;
        CostType costType;

        switch (altCost.altCostType) {
            case AltCostType.Discard:
                selectableUids = Utils.GetDiscardAlternateCostTargets(player, altCost);
                message = $"Select {altCost.amount} {targetName}{plural} to discard";
                costType = CostType.Discard;
                break;
            case AltCostType.Sacrifice:
                selectableUids = Utils.GetSacrificeAlternateCostTargets(player, altCost);
                message = $"Select {altCost.amount} {targetName}{plural} to sacrifice";
                costType = CostType.Sacrifice;
                break;
            case AltCostType.Tribute:
                selectableUids = Utils.GetSacrificeAlternateCostTargets(player, altCost);
                message = $"Select {altCost.amount} {targetName}{plural} to tribute";
                costType = CostType.Sacrifice;  // Tribute uses same cost handling as sacrifice
                break;
            case AltCostType.ExileFromHand:
                // For activated abilities, we don't have a "card being cast" to exclude
                selectableUids = Utils.GetDiscardAlternateCostTargets(player, altCost);
                message = $"Select {altCost.amount} {targetName}{plural} to exile from hand";
                costType = CostType.ExileFromHand;
                break;
            default:
                return;
        }

        GameEvent gEvent = GameEvent.CreateCostEvent(costType, altCost.amount, selectableUids, new List<string> { message });
        AddEventForPlayer(player, gEvent);
    }
    
    
    

    private static string GetCostMessage(CostContext cc) {
        string targetName = "[error: no TargetType name]";
        string plurality = cc.amount > 1 ? "s" : "";
        if (cc.cardType != null) targetName = cc.cardType.ToString()!;
        if (cc.tribe != null) targetName = cc.tribe.ToString()!;
        if (cc.tokenType != null) targetName = cc.tokenType.ToString()!;
        string amountString = cc.amount == 1 ? "a" : cc.amount.ToString();
        return cc.costType switch {
            CostType.Sacrifice => "sacrifice " + amountString + " " + targetName + plurality,
            CostType.Discard => "discard " + amountString + " " + targetName + plurality,
            _ => "error: CostType Message not implemented (GetCostMessage)"
        };
    }
    
    private void PayLifeCost(Player player, int cost) {
        player.lifeTotal -= cost;
        GameEvent gEvent = GameEvent.CreateGameEventWithAmount(EventType.PayLifeCost, false, cost);
        AddEventForBothPlayers(player, gEvent);
        RefreshLifeDependentCards(player);
    }

    /// <summary>
    /// Refreshes all cards with passives that depend on life total (e.g., Blunt Ambusher).
    /// </summary>
    private void RefreshLifeDependentCards(Player player) {
        List<Card> cardsToRefresh = new();
        foreach (Card c in player.playField) {
            if (HasLifeDependentPassive(c)) cardsToRefresh.Add(c);
        }
        foreach (Token t in player.tokens) {
            if (HasLifeDependentPassive(t)) cardsToRefresh.Add(t);
        }
        if (cardsToRefresh.Count > 0) {
            RefreshCards(player, cardsToRefresh);
        }
    }

    private bool HasLifeDependentPassive(Card card) {
        if (card.passiveEffects == null) return false;
        foreach (PassiveEffect pEffect in card.passiveEffects) {
            if (pEffect.statModifiers == null) continue;
            foreach (StatModifier statMod in pEffect.statModifiers) {
                if (statMod.amountBasedOn == AmountBasedOn.SubtractLife) return true;
            }
        }
        return false;
    }

    private bool CardCanTributeTo(Card c, Card requiringCard) {
        // Object type cards cannot be tributed
        if (c.type == CardType.Object) return false;
        // no passive
        if (c.passiveEffects == null) return true;
        // check each passive
        foreach (PassiveEffect pEffect in c.passiveEffects) {
            // CantTribute passive on the card itself means it can never be tributed
            if (pEffect.passive == Passive.CantTribute && pEffect.scope == Scope.SelfOnly) {
                return false;
            }
            // TributeRestriction - can only tribute for specific tribes
            if (pEffect.passive == Passive.TributeRestriction) {
                // tribute restriction matches requirement
                if (pEffect.tribe != null && pEffect.tribe == requiringCard.tribe) return true;
                // tribute restriction does not match requirement
                return false;
            }
        }

        return true;
    }


    public void Tribute(int playerId, List<int> tributeUids) {
        Player tributingPlayer = accountIdToPlayer[playerId];
        foreach (int uid in tributeUids) {
            if (!cardByUid.ContainsKey(uid)) continue;
            Card c = cardByUid[uid];
            // Mark card as being tributed (for Tribute triggers)
            c.isBeingTributed = true;
            // Use Destroy to handle both summons and tokens
            Destroy(c);
        }

        Debug.Assert(cardRequiringTribute != null, "there is no card requiring tribute");
        CastCard(tributingPlayer, cardRequiringTribute);
    }

    /// <summary>
    /// Gets the first payable Sacrifice-type alternate cost for the card, or null if none available.
    /// </summary>
    private AlternateCost? GetPayableSacrificeAlternateCost(Player player, Card card) {
        if (card.alternateCosts == null) return null;

        foreach (AlternateCost altCost in card.alternateCosts) {
            if (altCost.altCostType != AltCostType.Sacrifice) continue;

            int matchingCount = 0;
            if (altCost.tokenType != null) {
                matchingCount = player.tokens.Count(t => t.tokenType == altCost.tokenType);
            } else if (altCost.tribe != null) {
                matchingCount = player.tokens.Count(t => t.tribe == altCost.tribe);
                matchingCount += player.playField.Count(c => c.tribe == altCost.tribe);
            } else if (altCost.cardType != null) {
                matchingCount = player.playField.Count(c => c.type == altCost.cardType);
            }

            if (matchingCount >= altCost.amount) return altCost;
        }

        return null;
    }

    /// <summary>
    /// Gets the first payable ExileFromHand-type alternate cost for the card, or null if none available.
    /// </summary>
    private AlternateCost? GetPayableExileFromHandAlternateCost(Player player, Card card) {
        Console.WriteLine($"[GetPayableExileFromHandAlternateCost] card={card.name}, alternateCosts={(card.alternateCosts != null ? card.alternateCosts.Count.ToString() : "null")}");
        if (card.alternateCosts == null) return null;

        foreach (AlternateCost altCost in card.alternateCosts) {
            Console.WriteLine($"[GetPayableExileFromHandAlternateCost] altCost.altCostType={altCost.altCostType}");
            if (altCost.altCostType != AltCostType.ExileFromHand) continue;

            // Count matching cards in hand (excluding the card being cast)
            int matchingCount = 0;
            foreach (Card c in player.hand) {
                if (c.uid == card.uid) continue; // Don't count the card being cast
                bool matches = true;
                if (altCost.tribe != null && c.tribe != altCost.tribe) matches = false;
                if (altCost.cardType != null && c.type != altCost.cardType) matches = false;
                if (matches) matchingCount++;
            }
            Console.WriteLine($"[GetPayableExileFromHandAlternateCost] matchingCount={matchingCount}, required={altCost.amount}");

            if (matchingCount >= altCost.amount) return altCost;
        }

        return null;
    }

    /// <summary>
    /// Gets a human-readable description of an alternate cost.
    /// </summary>
    private string GetAlternateCostDescription(AlternateCost altCost) {
        string targetName = altCost.tokenType?.ToString() ?? altCost.tribe?.ToString() ?? altCost.cardType?.ToString() ?? "card";
        string plural = altCost.amount > 1 ? "s" : "";
        return altCost.altCostType switch {
            AltCostType.ExileFromHand => $"Exile {altCost.amount} {targetName}{plural} from hand",
            _ => $"Sacrifice {altCost.amount} {targetName}{plural}"
        };
    }

    /// <summary>
    /// Sends a Cost event to the player to select targets for the alternate cost.
    /// </summary>
    private void RequestAlternateCostPayment(Player player, AlternateCost altCost) {
        string targetName = altCost.tokenType?.ToString() ?? altCost.tribe?.ToString() ?? altCost.cardType?.ToString() ?? "card";
        string plural = altCost.amount > 1 ? "s" : "";
        List<int> selectableUids;
        string message;
        CostType costType;

        if (altCost.altCostType == AltCostType.ExileFromHand) {
            Debug.Assert(cardBeingCast != null, "No card being cast for ExileFromHand alternate cost");
            selectableUids = Utils.GetExileFromHandAlternateCostTargets(player, altCost, cardBeingCast);
            message = $"Select {altCost.amount} {targetName}{plural} to exile from hand";
            costType = CostType.ExileFromHand;
        } else {
            selectableUids = Utils.GetSacrificeAlternateCostTargets(player, altCost);
            message = $"Select {altCost.amount} {targetName}{plural} to sacrifice";
            costType = CostType.Sacrifice;
        }

        GameEvent gEvent = GameEvent.CreateCostEvent(costType, altCost.amount, selectableUids, new List<string> { message });
        AddEventForPlayer(player, gEvent);
    }

    private void CastCard(Player player, Card card, bool isAction = true) {
        Console.WriteLine($"[CastCard] Card being cast: {card.name}");
        cardBeingCast = null;
        switch (card.type) {
            // increment total spells
            case CardType.Spell:
                player.totalSpells++;
                break;
            // increment turnSummonCount
            case CardType.Summon:
                bool hasBypass = DetectPassive(card, Passive.BypassSummonLimit);
                Console.WriteLine($"CastCard: {card.name}, hasBypassSummonLimit={hasBypass}, turnSummonCount before={player.turnSummonCount}");
                if (!hasBypass) {
                    player.turnSummonCount++;
                }
                // Check for AdditionalSummonTopCard - if summoning from deck, get bonus summon
                if (card.currentZone == Zone.Deck) {
                    foreach (Card cardInPlay in player.playField) {
                        if (cardInPlay.GetPassives().Any(p => p.passive == Passive.AdditionalSummonTopCard)) {
                            player.turnSummonLimitBonus++;
                            Console.WriteLine($"[CastCard] {player.playerName} gets bonus summon from AdditionalSummonTopCard");
                            break;
                        }
                    }
                }
                Console.WriteLine($"  turnSummonCount after={player.turnSummonCount}");
                break;
        }

        player.playables.Remove(card);
        player.allCardsPlayer.Remove(card);

        // Save source zone before removing the card (for client animation)
        Zone cardSourceZone = card.currentZone;

        // Remove card from its current zone (supports casting from hand, graveyard, etc.)
        switch (card.currentZone) {
            case Zone.Hand:
                RemoveFromHand(player, card);
                break;
            case Zone.Graveyard:
                player.graveyard.Remove(card);
                Console.WriteLine($"[CastCard] Removed {card.name} from graveyard for casting");
                break;
            case Zone.Deck:
                player.deck?.Remove(card);
                break;
            default:
                // For other zones, just try to remove from hand (legacy behavior)
                RemoveFromHand(player, card);
                break;
        }

        // Apply player passives that grant keywords to next spell
        if (card.type == CardType.Spell) {
            List<PassiveEffect> passivesToRemove = new();
            foreach (PassiveEffect passive in player.playerPassives) {
                if (passive.passive == Passive.GrantKeywordToNextSpell && passive.keyword != null) {
                    card.grantedPassives.Add(new PassiveEffect(Passive.GrantKeyword, (Keyword)passive.keyword));
                    passivesToRemove.Add(passive);
                }
            }
            foreach (PassiveEffect passive in passivesToRemove) {
                player.playerPassives.Remove(passive);
            }
        }

        // Clear "next spell free" flag when casting a non-summon spell
        if (card.type != CardType.Summon && player.nextSpellFree) {
            player.nextSpellFree = false;
            Console.WriteLine($"[CastCard] {player.playerName}'s next spell free effect consumed");
            // Refresh hand cards to show normal costs again
            RefreshCards(player, player.hand, false);
        }

        StackObj newStackObj = CreateStackObj(player, card);
        stack.Push(newStackObj);
        card.currentZone = Zone.Stack;
        GameEvent gEvent = GameEvent.CreateStackEvent(EventType.Cast, new StackDisplayData(newStackObj, this), false, cardSourceZone);
        AddEventForBothPlayers(player, gEvent);

        // Check for copy spell effect (e.g., Merfolk Mage)
        if (card.type == CardType.Spell && player.copyNextSpell != null) {
            if (player.copyNextSpell.CanCopy(card)) {
                Console.WriteLine($"[CastCard] Copying spell {card.name} due to {player.copyNextSpell.sourceCard.name}");
                // Create a copy of the spell and add it to the stack
                CopySpellToStack(player, card, newStackObj);
            }
            // Clear the copy effect (one-time use)
            player.copyNextSpell = null;
        }

        // reset second pass whenever something is added to the stack
        secondPass = false;
        // Capture spellburnt state before it gets modified
        bool wasSpellburnt = player.spellBurnt;
        // pay cost
        // scorch check
        if (card.keywords != null && card.keywords.Contains(Keyword.Scorch)) {
            ApplySpellburn(player, true);
        } else if (card.type == CardType.Spell) {
            if (card.GetCost() > 0) player.scorched = false;
            ApplySpellburn(player, false);
        }

        if (isAction) {
            // Add Cast trigger for the card being cast (for "when you cast this" triggers)
            var castTrigger = new TriggerContext(Trigger.Cast, card: card, triggerController: player);
            castTrigger.wasSpellburnt = wasSpellburnt;
            triggersToCheck.Add(castTrigger);
            CheckForTriggersAndPassives(EventType.Cast, player);
        }
    }

    public void PassPrio() {
        Player p1 = playerOne;
        Player p2 = playerTwo;
        // Halt priority passing if Ghost Deceiver is waiting for input
        if (ghostDeceiverStage > 0) {
            return;
        }

        // Halt priority passing if Ground Tactics is active and not all attackers are assigned
        if (groundTacticsControllerId != null && currentPhase == Phase.Combat) {
            Player turnPlayer = GetPlayerByTurn(true);
            List<int> attackCapableUids = GetAttackCapableUids(turnPlayer);
            int unassignedCount = attackCapableUids.Count(uid => !currentAttackUids.ContainsKey(uid));
            if (unassignedCount > 0) {
                return;
            }
        }

        // NOTE: autopassPausedForStack is NOT cleared here for manual passes.
        // It is only cleared in LifeController when player clicks a passToPhase button,
        // or when the stack empties (see below).

        if (secondPass) {
            if (stack.Count > 0) {
                StackObj tempStackObj = stack.Peek();
                stack.Pop();
                prioPlayerId = -1;
                tempStackObj.ResolveStackObj(this);
                secondPass = false;

                // Check if EndTurn effect was triggered during resolution - handle immediately
                if (endTurnPending) {
                    HandleEndTurnPending();
                }
                return;
            }

            // Stack is empty - clear autopassPausedForStack for both players
            playerOne.autopassPausedForStack = false;
            playerTwo.autopassPausedForStack = false;

            if (currentAttackUids.Count > 0) {
                ResolveAttacks();
            }

            secondPass = false;

            // Check if EndTurn effect (Typhoon) was triggered - skip directly to end of turn
            if (endTurnPending) {
                HandleEndTurnPending();
                return;
            }

            GoToNextPhase();
        } else {
            secondPass = true;
            PassPrioToPlayer(GetPlayerByPrio(false));
        }
    }

    /// <summary>
    /// Handles the endTurnPending flag set by EndTurn effect (Typhoon).
    /// Skips directly to the opponent's Draw phase without giving priority.
    /// </summary>
    private void HandleEndTurnPending() {
        endTurnPending = false;
        Phase startPhase = currentPhase;
        Player startTurnPlayer = GetPlayerByTurn(true);

        // Calculate phases to skip: from current phase through End, then to opponent's Draw
        int phasesToEnd = (int)Phase.End - (int)currentPhase;
        int totalPhasesSkipped = phasesToEnd + 1;  // +1 for End->Draw transition
        Console.WriteLine($"[HandleEndTurnPending] phase={currentPhase}, phasesToSkip={totalPhasesSkipped}, turn={startTurnPlayer.playerName}");

        // Set phase to End and handle hand size discard / turn pass
        currentPhase = Phase.End;

        // Check for hand size discard
        Player activePlayer = GetPlayerByTurn(true);
        int cardsToDiscard = activePlayer.hand.Count - activePlayer.maxHandSize;
        if (cardsToDiscard > 0) {
            if (activePlayer.isBot) {
                List<Card> cardsToDiscardList = activePlayer.hand.Take(cardsToDiscard).ToList();
                foreach (Card c in cardsToDiscardList) {
                    Discard(activePlayer, c);
                }
            } else {
                // Player must select cards to discard - send the skip event first, then wait for discard
                GameEvent skipEvent = new GameEvent(EventType.SkipToPhase);
                skipEvent.amount = phasesToEnd;  // Just to End for now
                skipEvent.universalInt = (int)startPhase;
                AddEventForBothPlayers(startTurnPlayer, skipEvent);

                waitingForHandSizeDiscard = true;
                List<int> selectableUids = activePlayer.hand.Select(c => c.uid).ToList();
                string message = $"Discard {cardsToDiscard} card{(cardsToDiscard > 1 ? "s" : "")} (max hand size: {activePlayer.maxHandSize})";
                GameEvent discardEvent = GameEvent.CreateCostEvent(CostType.Discard, cardsToDiscard, selectableUids, new List<string> { message });
                AddEventForPlayer(activePlayer, discardEvent);
                return;
            }
        }

        // Pass the turn
        PassTurn();
        Console.WriteLine($"[HandleEndTurnPending] After PassTurn: phase={currentPhase}, turn={GetPlayerByTurn(true).playerName}");

        // Send SkipToPhase event (covers the full skip from original phase to new Draw)
        GameEvent skipPhaseEvent = new GameEvent(EventType.SkipToPhase);
        skipPhaseEvent.amount = totalPhasesSkipped;
        skipPhaseEvent.universalInt = (int)startPhase;
        Console.WriteLine($"[HandleEndTurnPending] Sending SkipToPhase: amount={totalPhasesSkipped}, startPhase={startPhase}");
        AddEventForBothPlayers(startTurnPlayer, skipPhaseEvent);

        // Handle Draw phase: return exiled cards and draw
        Player newTurnPlayer = GetPlayerByTurn(true);
        ReturnExiledCardsForPlayer(newTurnPlayer);
        Draw(newTurnPlayer, 1);

        // Add triggers for Draw phase but don't give priority - auto-pass to Main phase
        triggersToCheck.Add(TriggerContext.CreatePhaseTriggerContext(currentPhase));
        // Don't call CheckForTriggersAndPassives here - go directly to Main phase after triggers are processed
        if (triggersToCheck.Count > 0) {
            CheckForTriggersPlayer(triggersToCheck[0], playerOne);
            CheckForTriggersPlayer(triggersToCheck[0], playerTwo);
            triggersToCheck.Clear();
        }
        // Process any triggers that were collected (ordering, etc) then auto-pass to Main
        ProcessCollectedTriggersAndAutoPassToMain();
    }

    /// <summary>
    /// Processes collected triggers and then auto-passes to Main phase.
    /// Used after EndTurn effect to avoid giving priority during Draw phase.
    /// </summary>
    private void ProcessCollectedTriggersAndAutoPassToMain() {
        // If there are triggers, put them on the stack
        if (playerOne.controlledTriggers.Count > 0 || playerTwo.controlledTriggers.Count > 0) {
            // For simplicity, put all triggers on stack in order (player first)
            foreach (var tEffect in playerOne.controlledTriggers) {
                AddStackObjToStack(CreateStackObj(playerOne, tEffect.sourceCard, tEffect));
            }
            foreach (var tEffect in playerTwo.controlledTriggers) {
                AddStackObjToStack(CreateStackObj(playerTwo, tEffect.sourceCard, tEffect));
            }
            playerOne.controlledTriggers.Clear();
            playerTwo.controlledTriggers.Clear();
        }

        // If stack has items, resolve them without giving priority
        while (stack.Count > 0) {
            StackObj tempStackObj = stack.Pop();
            prioPlayerId = -1;
            tempStackObj.ResolveStackObj(this);
        }

        // Now go to Main phase
        GoToNextPhase();
    }

    private void ResolveAttacks() {
        Console.WriteLine($"[ResolveAttacks] Processing {currentAttackUids.Count} attacks");
        List<Card> cardsThatSurvivedCombat = new();
        foreach (var pair in currentAttackUids) {
            Card attackingCard = cardByUid[pair.Key];
            // Skip if attacker is no longer in play (e.g., sacrificed during attack trigger)
            if (attackingCard.currentZone != Zone.Play) {
                Console.WriteLine($"[ResolveAttacks] Skipping attack - {attackingCard.name} is no longer in play (zone={attackingCard.currentZone})");
                continue;
            }
            // set combat damage values
            // Check for DefenseUsedForAttack passive (e.g., Tree Giant makes treefolk deal damage equal to defense)
            bool attackerUsesDefense = attackingCard.GetPassives().Any(p => p.passive == Passive.DefenseUsedForAttack);
            int attackValue = attackerUsesDefense ? attackingCard.GetDefense() : attackingCard.GetAttack();
            // Check retaliation damage (defender may also have DefenseUsedForAttack)
            int retaliationValue = 0;
            if (cardByUid.TryGetValue(pair.Value, out var defender)) {
                bool defenderUsesDefense = defender.GetPassives().Any(p => p.passive == Passive.DefenseUsedForAttack);
                retaliationValue = defenderUsesDefense ? defender.GetDefense() : defender.GetAttack();
            }
            Console.WriteLine($"[ResolveAttacks] {attackingCard.name} (uid={pair.Key}, atk={attackValue}) -> target uid={pair.Value}");
            // create a combat event
            GameEvent gEvent = GameEvent.CreateCombatEvent(pair.Key, pair.Value, attackValue);
            AddEventForBothPlayers(GetPlayerByTurn(true), gEvent);
            // check for Trample - calculate excess damage before dealing damage
            int trampleDamage = 0;
            Player? trampleTarget = null;
            if (DetectKeyword(attackingCard, Keyword.Trample) && !IsPlayerUid(pair.Value)) {
                Card defendingCard = cardByUid[pair.Value];
                // only trample if defender is not immune to trample
                if (!defendingCard.IsImmuneToKeyword(Keyword.Trample)) {
                    int defenderDefense = defendingCard.GetDefense();
                    trampleDamage = attackValue - defenderDefense;
                    if (trampleDamage > 0) {
                        trampleTarget = GetControllerOf(defendingCard);
                    }
                }
            }
            // deal the damage
            DealDamage(pair.Value, attackValue);
            DealDamage(attackingCard.uid, retaliationValue);
            // Apply Haunt counters (if attacker has Haunt and target is a summon)
            int attackerHauntAmount = attackingCard.GetHauntAmount();
            if (attackerHauntAmount > 0 && !IsPlayerUid(pair.Value)) {
                Card defendingCard = cardByUid[pair.Value];
                if (!defendingCard.IsImmuneToKeyword(Keyword.Haunt)) {
                    defendingCard.hauntCounters += attackerHauntAmount;
                    Console.WriteLine($"[Haunt] {attackingCard.name} applies {attackerHauntAmount} haunt counter(s) to {defendingCard.name} (total: {defendingCard.hauntCounters})");
                    GameEvent hauntEvent = GameEvent.CreateRefreshCardDisplayEvent(defendingCard);
                    AddEventForBothPlayers(GetControllerOf(defendingCard), hauntEvent);
                }
            }
            // Apply Haunt counters from retaliation (if defender has Haunt)
            if (cardByUid.TryGetValue(pair.Value, out var defenderForHaunt) && defenderForHaunt.type == CardType.Summon) {
                int defenderHauntAmount = defenderForHaunt.GetHauntAmount();
                if (defenderHauntAmount > 0 && !attackingCard.IsImmuneToKeyword(Keyword.Haunt)) {
                    attackingCard.hauntCounters += defenderHauntAmount;
                    Console.WriteLine($"[Haunt] {defenderForHaunt.name} applies {defenderHauntAmount} haunt counter(s) to {attackingCard.name} (total: {attackingCard.hauntCounters})");
                    GameEvent hauntEvent = GameEvent.CreateRefreshCardDisplayEvent(attackingCard);
                    AddEventForBothPlayers(GetControllerOf(attackingCard), hauntEvent);
                }
            }
            // Check for haunt deaths (after haunt counters are applied)
            CheckForDeaths();
            // deal trample damage to defender's controller
            if (trampleDamage > 0 && trampleTarget != null) {
                DealDamage(trampleTarget.uid, trampleDamage);
                Console.WriteLine($"[Trample] {attackingCard.name} deals {trampleDamage} excess damage to {trampleTarget.playerName}");
            }
            // check for DealDamageToPlayer trigger (if target was a player)
            if (IsPlayerUid(pair.Value)) {
                triggersToCheck.Add(new TriggerContext(Trigger.DealDamageToPlayer, null, attackingCard));
            }
            // check for SurvivedCombat triggers
            if (attackingCard.currentZone == Zone.Play && attackingCard.GetDefense() > 0) cardsThatSurvivedCombat.Add(attackingCard);
        }
        triggersToCheck.Add(new TriggerContext(Trigger.SurvivedCombat, null, null, cardsThatSurvivedCombat));
        currentAttackUids.Clear();
    }

    private void CheckForDeaths() {
        // this also checks for player deaths (see below). This might need to be moved to a separate function
        // Use ToList() to avoid collection modification during iteration
        foreach (var c in playerOne.playField.ToList()) {
            if (c.defense == null) continue;
            // Kill if defense <= 0 OR has 2+ haunt counters
            if (c.GetDefense() <= 0 || c.hauntCounters >= 2) {
                if (c.hauntCounters >= 2) Console.WriteLine($"[Haunt Death] {c.name} dies from haunt counters ({c.hauntCounters})");
                Kill(c);
            }
        }
        foreach (var c in playerTwo.playField.ToList()) {
            if (c.defense == null) continue;
            // Kill if defense <= 0 OR has 2+ haunt counters
            if (c.GetDefense() <= 0 || c.hauntCounters >= 2) {
                if (c.hauntCounters >= 2) Console.WriteLine($"[Haunt Death] {c.name} dies from haunt counters ({c.hauntCounters})");
                Kill(c);
            }
        }
    }

    private void Kill(Card c) {
        // reset damage taken
        c.damageTaken = 0;

        // Check for DeathBySpell replacement effect
        bool returnToHand = false;
        bool exileOnTribute = false;
        if (c.tookSpellDamage && c.triggeredEffects != null) {
            foreach (TriggeredEffect tEffect in c.triggeredEffects) {
                if (tEffect.trigger == Trigger.DeathBySpell && tEffect.scope == Scope.SelfOnly) {
                    returnToHand = true;
                    break;
                }
            }
        }
        c.tookSpellDamage = false;  // reset flag

        // Check for Tribute replacement effect (e.g., Shade of Return)
        if (c.isBeingTributed && c.triggeredEffects != null) {
            foreach (TriggeredEffect tEffect in c.triggeredEffects) {
                if (tEffect.trigger == Trigger.Tribute) {
                    // Check if this tribute trigger has a sendToZone effect to hand
                    if (tEffect.effects != null) {
                        foreach (Effect e in tEffect.effects) {
                            if (e.effect == EffectType.SendToZone && e.destination == Zone.Hand) {
                                returnToHand = true;
                                break;
                            }
                        }
                    }
                    if (returnToHand) break;
                }
            }
        }

        // Check for ExileInsteadOfGraveyardOnTribute passive (replacement effect)
        if (c.isBeingTributed && c.passiveEffects != null) {
            if (c.passiveEffects.Any(p => p.passive == Passive.ExileInsteadOfGraveyardOnTribute)) {
                exileOnTribute = true;
            }
        }

        // Add Tribute trigger context so cards in play can respond to tributes (e.g., Goblin Portal)
        if (c.isBeingTributed) {
            Player tributeController = GetControllerOf(c);
            Console.WriteLine($"[Kill] Adding Tribute trigger for {c.name}, tributeForCard={cardRequiringTribute?.name ?? "NULL"}, tribe={cardRequiringTribute?.tribe}");
            triggersToCheck.Add(new TriggerContext(Trigger.Tribute, card: c, triggerController: tributeController, tributeForCard: cardRequiringTribute));
        }
        c.isBeingTributed = false;  // reset flag

        // Store controller/owner before removing from play
        Player controller = GetControllerOf(c);
        Player owner = GetOwnerOf(c);

        // Store sprout amount before removing from play (for SproutTriggersOnDeath)
        int sproutAmount = c.GetSproutAmount();

        RemoveFromPlay(controller, c);

        // add to owner's hand (if DeathBySpell) or graveyard
        if (c is Token token) {
            // Summon-type tokens (like Goblin tokens) need a Death event for the client
            RemoveFromAllCardsPlayer(owner, c);
            // Update zone so QualifyCard doesn't try to call GetControllerOf on a dead token
            c.currentZone = Zone.Graveyard;
            GameEvent gEvent = GameEvent.CreateUidEvent(EventType.Death, c.uid);
            AddEventForBothPlayers(owner, gEvent);
            // Add death trigger for summon-type tokens (with stored controller)
            if (token.type == CardType.Summon) {
                triggersToCheck.Add(new TriggerContext(Trigger.Death, null, c, triggerController: controller));
            }
        } else if (returnToHand) {
            // DeathBySpell replacement effect - return to hand instead of graveyard
            AddToHand(GetOwnerOf(c), c);
            c.grantedPassives.Clear();
            GameEvent handEvent = GameEvent.CreateCardEvent(EventType.ReturnToHand, new CardDisplayData(c));
            AddEventForBothPlayers(GetOwnerOf(c), handEvent);
        } else if (exileOnTribute) {
            // ExileInsteadOfGraveyardOnTribute replacement effect - exile instead of graveyard when tributed
            Player cardOwner = GetOwnerOf(c);
            Console.WriteLine($"[Kill] Tribute replacement effect: {c.name} exiled instead of going to graveyard");
            AddToExile(cardOwner, c);
            c.grantedPassives.Clear();
            // Send SendToZone event to Exile (plays the exile animation)
            GameEvent exileEvent = GameEvent.CreateZoneGameEvent(Zone.Exile, new CardDisplayData(c), Zone.Play);
            AddEventForBothPlayers(cardOwner, exileEvent);
            // Still trigger Death since the card was tributed (died)
            triggersToCheck.Add(new TriggerContext(Trigger.Death, null, c));
        } else {
            // Check for replacement effect: summons go to exile instead of graveyard
            Player cardOwner = GetOwnerOf(c);
            if (c.type == CardType.Summon &&
                cardOwner.playerPassives.Any(p => p.passive == Passive.SummonsToGraveyardExileInstead)) {
                Console.WriteLine($"[Kill] Replacement effect: {c.name} exiled instead of going to graveyard");
                AddToExile(cardOwner, c);
                // Send SendToZone event to Exile instead of Death event
                GameEvent exileEvent = GameEvent.CreateZoneGameEvent(Zone.Exile, new CardDisplayData(c), Zone.Play);
                AddEventForBothPlayers(cardOwner, exileEvent);
            } else {
                AddToGraveyard(cardOwner, c, Zone.Play);
                // Send Death event (card went to graveyard)
                GameEvent gEvent = GameEvent.CreateUidEvent(EventType.Death, c.uid);
                AddEventForBothPlayers(GetPlayerByTurn(true), gEvent);
            }
            c.grantedPassives.Clear();
            // Add death trigger regardless of destination (card still "died")
            triggersToCheck.Add(new TriggerContext(Trigger.Death, null, c));
        }

        // Track summons that died this turn (for CastAMold and similar effects)
        if (c.type == CardType.Summon) {
            summonsThatDiedThisTurn++;
        }

        // SproutTriggersOnDeath: If dying card had Sprout and controller has the passive, create herbs
        if (sproutAmount > 0 && controller.playField.Any(card =>
            card.passiveEffects?.Any(p => p.passive == Passive.SproutTriggersOnDeath) == true)) {
            for (int i = 0; i < sproutAmount; i++) {
                Token herb = new Token(TokenType.Herb, this);
                herb.currentZone = Zone.Play;
                CreateTokenForPlayer(controller, herb, false);
            }
        }

        // remove from current attack if necessary
        if (c.type == CardType.Summon) {
            foreach (var pair in currentAttackUids.ToList()) {
                if (pair.Key == c.id || pair.Value == c.id) {
                    currentAttackUids.Remove(pair.Key);
                }
            }
        }

        RemovePassivesFromSource(c);
        CheckForPassives();
    }

    public void Destroy(Card c) {
        switch (c.type) {
            case CardType.Summon or CardType.Object:
                Kill(c);
                break;
            case CardType.Token:
                // get controller and remove it from tokens
                Player controller = GetControllerOf(c);
                controller.tokens.Remove((Token)c);
                // create event
                GameEvent gEvent = GameEvent.CreateCardEvent(EventType.Destroy, new CardDisplayData(c));
                // set cardStackId for event and remove cardStackId entry
                gEvent.universalInt = controller.cardToCardStackId[c];
                controller.cardToCardStackId.Remove(c);
                // Use controller (not turn player) so isOpponent flag is correct for the stack ID
                AddEventForBothPlayers(controller, gEvent);
                break;
            default:
                Console.WriteLine("you can't destroy that type of card -> match.Destroy()");
                break;
        }

    }

    public void Discard(Player player, Card c, int batchSize = 1) {
        RemoveFromHand(player, c);
        // Check for replacement effect: summons go to exile instead of graveyard
        if (c.type == CardType.Summon &&
            player.playerPassives.Any(p => p.passive == Passive.SummonsToGraveyardExileInstead)) {
            Console.WriteLine($"[Discard] Replacement effect: {c.name} exiled instead of going to graveyard");
            AddToExile(player, c);
            // Send SendToZone event to Exile with source = Hand
            GameEvent exileEvent = GameEvent.CreateZoneGameEvent(Zone.Exile, new CardDisplayData(c), Zone.Hand);
            AddEventForBothPlayers(player, exileEvent);
        } else {
            AddToGraveyard(player, c, Zone.Hand);
            // Send normal Discard event (animates to graveyard)
            // batchSize > 1 tells client to speed up animation
            GameEvent gEvent = GameEvent.CreateCardEvent(EventType.Discard, new CardDisplayData(c));
            gEvent.amount = batchSize;
            AddEventForBothPlayers(player, gEvent);
        }
        // Add discard trigger context
        TriggerContext discardContext = new TriggerContext(Trigger.Discard, null, c);
        discardContext.triggerController = player;
        triggersToCheck.Add(discardContext);
        Console.WriteLine($"[Discard] Added trigger context: card={c.name}, triggerController={player.playerName}");
    }

    public Player GetOwnerOf(Card c) {
        return playerOne.ownedCards.Contains(c) ? playerOne : playerTwo;
    }

    public Player GetControllerOf(Card c) {
        if (c.type == CardType.Token) {
            Debug.Assert(playerOne.tokens.Contains(c) || playerTwo.tokens.Contains(c),
                "Neither player controls that token");
            return playerOne.tokens.Contains(c) ? playerOne : playerTwo;
        }

        Debug.Assert(playerOne.playField.Contains(c) || playerTwo.playField.Contains(c),
            "Neither player controls that card");
        return playerOne.playField.Contains(c) ? playerOne : playerTwo;
    }

    /// <summary>
    /// Transfers control of a card from its current controller to the new controller.
    /// The card moves from one player's playField to the other's.
    /// </summary>
    public void GainControl(Player newController, Card card) {
        Player currentController = GetControllerOf(card);
        if (currentController == newController) return; // Already controls it

        // Remove from current controller's play field
        if (card is Token token) {
            currentController.tokens.Remove(token);
            newController.tokens.Add(token);
        } else {
            currentController.playField.Remove(card);
            newController.playField.Add(card);
        }

        // Update the card's controlling player
        card.lastControllingPlayer = newController;

        // Create event for client to update display
        GameEvent gEvent = new GameEvent(EventType.GainControl);
        gEvent.focusCard = new CardDisplayData(card);
        AddEventForBothPlayers(newController, gEvent);

        // Re-check passives since controller changed (auras may need to update)
        CheckForPassives();
    }

    public void Summon(Card c, Player player, bool isAttacking) {
        // Check for player passives that grant keywords to summons BEFORE they enter play
        // This is critical for sprout triggers - the keyword must be present when enter-play triggers are checked
        // Only applies to non-token summons (tokens are handled in ApplyPlayerPassivesToToken)
        if (c is not Token) {
            ApplyPlayerPassivesToSummon(player, c);
        }

        AddToPlay(player, c);
        Debug.Assert(c.lastControllingPlayer != null, "Card has no controller");
        player.totalSummons++;
        TokenType? tokenType = null;
        if (c is Token token) {
            tokenType = token.tokenType;
        }

        GameEvent gEvent =
            GameEvent.CreateCardEvent(EventType.Summon, new CardDisplayData(c, tokenType), false, isAttacking);
        if (isAttacking) {
            requiredAttackTargets++;
        }

        AddEventForBothPlayers(c.lastControllingPlayer, gEvent);
        CheckForPassives();
        CheckForDeaths();
    }

    public void SummonNonSummon(Card c, Player player) {
        AddToPlay(player, c);
        Debug.Assert(c.lastControllingPlayer != null, "Card has no controller");
        TokenType? tokenType = null;
        if (c is Token token) {
            tokenType = token.tokenType;
        }
        GameEvent gEvent =
            GameEvent.CreateCardEvent(EventType.Summon, new CardDisplayData(c, tokenType));
        AddEventForBothPlayers(c.lastControllingPlayer, gEvent);
        CheckForPassives();
        CheckForDeaths();
    }

    // Copy a spell to the stack (for Merfolk Mage effect)
    private void CopySpellToStack(Player player, Card originalCard, StackObj originalStackObj) {
        // Create a triggered ability that copies the spell's effects
        // Uses the source card (e.g., Merfolk Mage) as the stack object source, not the original spell
        List<Effect>? originalEffects = originalStackObj.effects;
        if (originalEffects == null || player.copyNextSpell == null) return;

        List<Effect> copiedEffects = originalEffects.Select(e => e.Clone()).ToList();

        // Use the card that created the copy effect (e.g., Merfolk Mage) as the source
        Card sourceCard = player.copyNextSpell.sourceCard;

        // Create a stack object as a triggered effect (not a spell copy)
        // This prevents the client from trying to manipulate the original spell card
        StackObj copyStackObj = new StackObj(
            sourceCard,  // Use Merfolk Mage (or whatever created the copy effect) as source
            StackObjType.TriggeredEffect,  // Treat as triggered ability
            copiedEffects,
            sourceCard.currentZone,  // Use source card's zone
            player,
            "Copy of " + originalCard.name  // Description shows what's being copied
        );

        // Copy the targets from the original (player can't choose new targets in this implementation)
        for (int i = 0; i < copiedEffects.Count && i < originalEffects.Count; i++) {
            copiedEffects[i].targetUids = new List<int>(originalEffects[i].targetUids);
        }

        stack.Push(copyStackObj);

        // Send as Trigger event, not Cast event - this is a triggered ability, not a spell being cast
        GameEvent copyEvent = GameEvent.CreateStackEvent(EventType.Trigger, new StackDisplayData(copyStackObj, this), false, sourceCard.currentZone);
        AddEventForBothPlayers(player, copyEvent);

        Console.WriteLine($"[CopySpellToStack] Added triggered copy of {originalCard.name} (source: {sourceCard.name}) to stack");
    }

    // End the current player's turn immediately (for Typhoon)
    // Sets a flag - actual turn ending happens after stack finishes resolving
    public void EndCurrentTurn() {
        Console.WriteLine($"[EndCurrentTurn] Setting endTurnPending flag for {GetPlayerByTurn(true).playerName}");
        endTurnPending = true;
    }

    // Go to a specific phase (for Rewind - restarts opponent's turn)
    public void GoToPhase(Player targetPlayer, Phase targetPhase) {
        // If going to Draw phase, this is essentially restarting the turn
        if (targetPhase == Phase.Draw) {
            // Switch turn to target player if needed
            if (GetPlayerByTurn(true) != targetPlayer) {
                turnPlayerId = targetPlayer.playerId;
            }
            currentPhase = Phase.Draw;

            // Send GoToPhase event so client can reset phase border directly
            GameEvent phaseEvent = GameEvent.CreateGoToPhaseEvent(targetPhase);
            AddEventForBothPlayers(targetPlayer, phaseEvent);

            // Draw a card for the new turn
            Draw(targetPlayer, 1);

            triggersToCheck.Add(TriggerContext.CreatePhaseTriggerContext(currentPhase));
            CheckForTriggersAndPassives(EventType.NextPhase);
        } else {
            // Just go to the target phase
            currentPhase = targetPhase;
            GameEvent phaseEvent = GameEvent.CreateGoToPhaseEvent(targetPhase);
            AddEventForBothPlayers(GetPlayerByTurn(true), phaseEvent);
            triggersToCheck.Add(TriggerContext.CreatePhaseTriggerContext(currentPhase));
            CheckForTriggersAndPassives(EventType.GoToPhase);
        }
    }

    private void GoToNextPhase() {
        Console.WriteLine($"[GoToNextPhase] phase={currentPhase}, turn={GetPlayerByTurn(true).playerName}");
        // Halt phase progression if Ghost Deceiver is waiting for input
        if (ghostDeceiverStage > 0) return;
        if (currentPhase == Phase.End) {
            // Check if player needs to discard to hand size before passing turn
            Player activePlayer = GetPlayerByTurn(true);
            int cardsToDiscard = activePlayer.hand.Count - activePlayer.maxHandSize;
            if (cardsToDiscard > 0) {
                // Bot auto-discards the first cards in hand
                if (activePlayer.isBot) {
                    List<Card> cardsToDiscardList = activePlayer.hand.Take(cardsToDiscard).ToList();
                    foreach (Card c in cardsToDiscardList) {
                        Discard(activePlayer, c);
                    }
                } else {
                    // Player must select cards to discard down to hand size
                    waitingForHandSizeDiscard = true;
                    List<int> selectableUids = activePlayer.hand.Select(c => c.uid).ToList();
                    string message = $"Discard {cardsToDiscard} card{(cardsToDiscard > 1 ? "s" : "")} (max hand size: {activePlayer.maxHandSize})";
                    GameEvent discardEvent = GameEvent.CreateCostEvent(CostType.Discard, cardsToDiscard, selectableUids, new List<string> { message });
                    AddEventForPlayer(activePlayer, discardEvent);
                    return;
                }
            }
            PassTurn();
            Console.WriteLine($"[GoToNextPhase] After PassTurn: phase={currentPhase}, turn={GetPlayerByTurn(true).playerName}");
        } else {
            currentPhase++;
            Console.WriteLine($"[GoToNextPhase] Incremented: phase={currentPhase}");
        }

        triggersToCheck.Add(TriggerContext.CreatePhaseTriggerContext(currentPhase));
        GameEvent gEvent = new GameEvent(EventType.NextPhase);
        Console.WriteLine($"[GoToNextPhase] Adding NextPhase event: phase={currentPhase}, isAutoSkipping={isAutoSkipping}");
        Console.WriteLine($"[GoToNextPhase] P1 eventList count BEFORE add: {playerOne.eventList.Count}");
        Console.WriteLine($"[GoToNextPhase] P2 eventList count BEFORE add: {playerTwo.eventList.Count}");
        AddEventForBothPlayers(GetPlayerByTurn(true), gEvent);
        Console.WriteLine($"[GoToNextPhase] P1 eventList count AFTER add: {playerOne.eventList.Count}");
        Console.WriteLine($"[GoToNextPhase] P2 eventList count AFTER add: {playerTwo.eventList.Count}");
        // activate attackCapables for combat phase
        if (currentPhase == Phase.Combat) {
            Player turnPlayer = GetPlayerByTurn(true);
            List<int> attackCapableUids = GetAttackCapableUids(turnPlayer);

            // Check for GroundTactics passive on turn player
            PassiveEffect? groundTacticsPassive = turnPlayer.playerPassives
                .FirstOrDefault(p => p.passive == Passive.GroundTactics);

            if (groundTacticsPassive != null && groundTacticsPassive.attackControllerPlayerId != null) {
                // Ground Tactics is active - opponent controls attack assignments
                groundTacticsControllerId = groundTacticsPassive.attackControllerPlayerId;
                Player controller = GetPlayerById(groundTacticsControllerId.Value);

                // ALL attack-capable summons MUST attack (can't skip any)
                // Send AttackCapables event to the controller (not the turn player)
                GameEvent acEvent = GameEvent.CreateMultiUidEvent(EventType.AttackCapables, attackCapableUids);
                acEvent.universalBool = true;  // Flag: forced attack mode (can't pass until all assigned)
                acEvent.universalInt = turnPlayer.playerId;  // Store whose summons are attacking
                AddEventForPlayer(controller, acEvent);

                Console.WriteLine($"[GroundTactics] {controller.playerName} controls attack assignment for {turnPlayer.playerName}'s {attackCapableUids.Count} attackers");
            } else {
                // Normal combat - turn player assigns their own attacks
                groundTacticsControllerId = null;
                GameEvent acEvent = GameEvent.CreateMultiUidEvent(EventType.AttackCapables, attackCapableUids);
                AddEventForPlayer(turnPlayer, acEvent);
            }
        }

        // Process delayed zone effects at the beginning of specific phases
        ProcessDelayedZoneEffects(GetPlayerByTurn(true), currentPhase);

        // draw for beginning of turn
        if (currentPhase == Phase.Draw) {
            // Return any exiled cards that were scheduled to return on this player's draw phase
            ReturnExiledCardsForPlayer(GetPlayerByTurn(true));
            Draw(GetPlayerByTurn(true), 1);
        }
        CheckForTriggersAndPassives(EventType.NextPhase);
    }

    private void PassTurn() {
        HandleEndOfTurnPassives();
        Player currentTurnPlayer = GetPlayerByTurn(true);
        // reset turn counters
        currentTurnPlayer.turnSummonCount = 0;
        currentTurnPlayer.turnSummonLimitBonus = 0;
        currentTurnPlayer.turnDrawCount = 0;
        // reset herb sacrifice counters and bypass flag for both players
        GetPlayerByTurn(true).turnHerbSacrificeCount = 0;
        GetPlayerByTurn(false).turnHerbSacrificeCount = 0;
        GetPlayerByTurn(true).bypassHerbLifeReduction = false;
        GetPlayerByTurn(false).bypassHerbLifeReduction = false;
        // remove spellburn if not scorched
        RemoveSpellburn(GetPlayerByTurn(true));
        RemoveSpellburn(GetPlayerByTurn(false));
        // reset cantAttack and attackedThisTurn flags
        GetPlayerByTurn(true).cantAttackThisTurn = false;
        GetPlayerByTurn(false).cantAttackThisTurn = false;
        GetPlayerByTurn(true).attackedThisTurn = false;
        // reset tokensCreatedThisTurn for both players
        GetPlayerByTurn(true).tokensCreatedThisTurn.Clear();
        GetPlayerByTurn(false).tokensCreatedThisTurn.Clear();
        // reset summons that died this turn counter
        summonsThatDiedThisTurn = 0;
        // reset ground tactics controller
        groundTacticsControllerId = null;
        GetPlayerByTurn(false).attackedThisTurn = false;
        // reset exhausted flags
        GetPlayerByTurn(true).exhausted = false;
        GetPlayerByTurn(false).exhausted = false;
        // clear player passives that expire at end of turn
        GetPlayerByTurn(true).playerPassives.RemoveAll(p => p.thisTurn);
        GetPlayerByTurn(false).playerPassives.RemoveAll(p => p.thisTurn);
        // reset oncePerTurn activated abilities for all cards
        ResetOncePerTurnAbilities();
        // reset next spell free flags (expires at end of turn)
        if (GetPlayerByTurn(true).nextSpellFree) {
            GetPlayerByTurn(true).nextSpellFree = false;
            RefreshCards(GetPlayerByTurn(true), GetPlayerByTurn(true).hand, false);
        }
        if (GetPlayerByTurn(false).nextSpellFree) {
            GetPlayerByTurn(false).nextSpellFree = false;
            RefreshCards(GetPlayerByTurn(false), GetPlayerByTurn(false).hand, false);
        }
        // update phase
        currentPhase = Phase.Draw;

        // Check for extra turns - if current player has extra turns, they keep the turn
        if (currentTurnPlayer.extraTurns > 0) {
            currentTurnPlayer.extraTurns--;
            Console.WriteLine($"[PassTurn] {currentTurnPlayer.playerName} takes an extra turn! ({currentTurnPlayer.extraTurns} remaining)");
            // Keep turnPlayerId the same, just reset priority
            prioPlayerId = currentTurnPlayer.playerId;
        } else {
            // Normal turn pass - switch prio and turn ids
            turnPlayerId = GetPlayerByTurn(false).playerId;
            prioPlayerId = GetPlayerByTurn(true).playerId;
        }
        // draw cards for turn
    }

    private void ResetOncePerTurnAbilities() {
        // Reset for all cards in play
        foreach (Card c in allCardsInPlay) {
            if (c.activatedEffects != null) {
                foreach (ActivatedEffect aEffect in c.activatedEffects) {
                    aEffect.usedThisTurn = false;
                }
            }
            foreach (ActivatedEffect aEffect in c.grantedActivatedEffects) {
                aEffect.usedThisTurn = false;
            }
        }
        // Reset for all tokens
        foreach (Token t in playerOne.tokens.Concat(playerTwo.tokens)) {
            if (t.activatedEffects != null) {
                foreach (ActivatedEffect aEffect in t.activatedEffects) {
                    aEffect.usedThisTurn = false;
                }
            }
            foreach (ActivatedEffect aEffect in t.grantedActivatedEffects) {
                aEffect.usedThisTurn = false;
            }
        }
    }

    private void HandleEndOfTurnPassives() {
        List<Card> tempCardsInPlay = allCardsInPlay.ToList();
        foreach (Card c in tempCardsInPlay) {
            c.hasSummoningSickness = false;
            c.damageTaken = 0;
            foreach (PassiveEffect pEffect in c.GetPassives()) {
                if (pEffect.passive == Passive.ThisTurn) Kill(c);
                if (pEffect.thisTurn) c.grantedPassives.Remove(pEffect);
            }
        }
        List<Token> tempTokensList = playerOne.tokens.Concat(playerTwo.tokens).ToList();
        foreach (var t in tempTokensList) {
            foreach (PassiveEffect pEffect in t.GetPassives()) {
                if (pEffect.passive == Passive.ThisTurn) Destroy(t);
                if (pEffect.thisTurn) t.grantedPassives.Remove(pEffect);
            }
        }
        CheckForPassives();
    }
    

private void ApplySpellburn(Player player, bool isScorch) {
        if (isScorch) player.scorched = true;
        if (player.spellBurnt) return;
        player.spellBurnt = true;
        GameEvent gEvent = new GameEvent(EventType.Spellburn);
        AddEventForBothPlayers(player, gEvent);
        RefreshCards(player, player.hand, false);
    }

    private void RemoveSpellburn(Player player) {
        if (player.scorched) return;
        if (!player.spellBurnt) return;
        player.spellBurnt = false;
        GameEvent gEvent = new GameEvent(EventType.Spellburn);
        AddEventForBothPlayers(player, gEvent);
        RefreshCards(player, player.hand, false);
    }

    public void RefreshCards(Player player, List<Card> cards, bool bothPlayers = true) {
        List<CardDisplayData> cardDisplays = cards.Select(c => new CardDisplayData(c)).ToList();
        GameEvent refreshEvent = GameEvent.CreateRefreshCardDisplayEvent(null, cardDisplays);
        if (bothPlayers) {
            AddEventForBothPlayers(player, refreshEvent);
        } else {
            AddEventForPlayer(player, refreshEvent);
        }
    }

    private Player GetPlayerByPrio(bool prio) {
        if (prio) {
            return prioPlayerId == playerOne.playerId ? playerOne : playerTwo;
        }

        return prioPlayerId == playerTwo.playerId ? playerOne : playerTwo;
    }

    private Player GetPlayerByTurn(bool playerTurn) {
        if (playerTurn) {
            return turnPlayerId == playerOne.playerId ? playerOne : playerTwo;
        }

        return turnPlayerId == playerTwo.playerId ? playerOne : playerTwo;
    }

    public Player GetPlayerById(int playerId) {
        if (playerOne.playerId == playerId) return playerOne;
        if (playerTwo.playerId == playerId) return playerTwo;
        throw new InvalidOperationException($"No player found with ID {playerId}");
    }

    public void CreateTokenForPlayer(Player player, Token token, bool isAttacking) {
        cardByUid.Add(token.uid, token);

        // Track tokens created this turn (for EnteredZoneThisTurn condition)
        player.tokensCreatedThisTurn.Add(token);

        // Check for player passives that grant keywords to tokens BEFORE they enter play
        // This is critical for sprout triggers - the keyword must be present when enter-play triggers are checked
        ApplyPlayerPassivesToToken(player, token);

        if (token.type == CardType.Token) {
            // Non-summon tokens (herbs, stones) go to tokens list and appear stacked
            AddToTokenZone(player, token);
            CheckForPassives();
        } else {
            // Summon-type tokens (goblin, ghost, golem) go to playField and appear as regular summons
            token.currentZone = Zone.Play;
            Summon(token, player, isAttacking);
        }
    }

    /// <summary>
    /// Applies player passives that grant keywords to tokens (e.g., Fertilize's "non-herb tokens have sprout 1")
    /// Called BEFORE the token enters play so keywords are present when enter-play triggers fire.
    /// </summary>
    private void ApplyPlayerPassivesToToken(Player player, Token token) {
        foreach (PassiveEffect pEffect in player.playerPassives.ToList()) {
            if (pEffect.passive == Passive.GrantKeywordToFutureTokens) {
                // Check if this token matches the filter (e.g., not an herb)
                if (pEffect.notTokenType != null && token.tokenType == pEffect.notTokenType) {
                    Console.WriteLine($"[ApplyPlayerPassivesToToken] Skipping {token.name} - it's a {token.tokenType} (excluded)");
                    continue;
                }
                // Grant the keyword
                if (pEffect.keyword != null) {
                    int kwAmount = pEffect.keywordAmount ?? 1;
                    PassiveEffect kwPassive = new PassiveEffect(Passive.GrantKeyword, (Keyword)pEffect.keyword);
                    kwPassive.keywordAmount = kwAmount;
                    kwPassive.thisTurn = pEffect.thisTurn;
                    token.grantedPassives.Add(kwPassive);
                    Console.WriteLine($"[ApplyPlayerPassivesToToken] Granted {pEffect.keyword} {kwAmount} to {token.name} (from player passive)");
                }
            }
        }
    }

    /// <summary>
    /// Applies player passives that grant keywords to the next summon (e.g., Fertilize's "next Treefolk has sprout 2")
    /// Called BEFORE the summon enters play so keywords are present when enter-play triggers fire.
    /// This passive is consumed after use (one-shot).
    /// </summary>
    private void ApplyPlayerPassivesToSummon(Player player, Card summon) {
        List<PassiveEffect> passivesToRemove = new();
        foreach (PassiveEffect pEffect in player.playerPassives.ToList()) {
            if (pEffect.passive == Passive.GrantKeywordToNextSummon) {
                // Check if this summon matches the tribe filter
                if (pEffect.tribe != null && summon.tribe != pEffect.tribe) {
                    Console.WriteLine($"[ApplyPlayerPassivesToSummon] Skipping {summon.name} - tribe {summon.tribe} doesn't match {pEffect.tribe}");
                    continue;
                }
                // Grant the keyword
                if (pEffect.keyword != null) {
                    int kwAmount = pEffect.keywordAmount ?? 1;
                    PassiveEffect kwPassive = new PassiveEffect(Passive.GrantKeyword, (Keyword)pEffect.keyword);
                    kwPassive.keywordAmount = kwAmount;
                    kwPassive.thisTurn = pEffect.thisTurn;
                    summon.grantedPassives.Add(kwPassive);
                    Console.WriteLine($"[ApplyPlayerPassivesToSummon] Granted {pEffect.keyword} {kwAmount} to {summon.name} (from player passive)");
                    // This is a one-shot passive, remove it after granting
                    passivesToRemove.Add(pEffect);
                }
            }
        }
        // Remove consumed passives
        foreach (PassiveEffect p in passivesToRemove) {
            player.playerPassives.Remove(p);
        }
    }

    public bool IsPlayerOne(int accountId) {
        return accountId == playerOne.playerId;
    }

    public Player GetOpponent(Player player) {
        return player == playerOne ? playerTwo : playerOne;
    }

    public Player? GetPlayerByUid(int uid) {
        if (playerOne.uid == uid) return playerOne;
        if (playerTwo.uid == uid) return playerTwo;
        return null;
    }

    /// <summary>
    /// Gets the weakest summon a player controls using MTG rules:
    /// 1. Lowest attack first
    /// 2. If tied, lowest defense
    /// 3. If still tied, returns null to indicate controller must choose
    /// </summary>
    public Card? GetWeakestSummon(Player player) {
        List<Card> summons = player.playField.Where(c => c.type == CardType.Summon).ToList();
        if (summons.Count == 0) return null;

        // Find lowest attack
        int lowestAttack = summons.Min(c => c.GetAttack());
        List<Card> lowestAttackSummons = summons.Where(c => c.GetAttack() == lowestAttack).ToList();

        if (lowestAttackSummons.Count == 1) return lowestAttackSummons[0];

        // Tie on attack - find lowest defense among those
        int lowestDefense = lowestAttackSummons.Min(c => c.GetDefense());
        List<Card> tiedSummons = lowestAttackSummons.Where(c => c.GetDefense() == lowestDefense).ToList();

        if (tiedSummons.Count == 1) return tiedSummons[0];

        // Still tied - controller must choose
        // For now, return the first one (TODO: prompt controller to choose)
        // In a real implementation, we'd set up a selection event for the controller
        return tiedSummons[0];
    }

    /// <summary>
    /// Gets the strongest summon a player controls using MTG rules:
    /// 1. Highest attack first
    /// 2. If tied, highest defense
    /// 3. If still tied, returns first one (TODO: controller chooses)
    /// </summary>
    public Card? GetStrongestSummon(Player player) {
        List<Card> summons = player.playField.Where(c => c.type == CardType.Summon).ToList();
        if (summons.Count == 0) return null;

        // Find highest attack
        int highestAttack = summons.Max(c => c.GetAttack());
        List<Card> highestAttackSummons = summons.Where(c => c.GetAttack() == highestAttack).ToList();

        if (highestAttackSummons.Count == 1) return highestAttackSummons[0];

        // Tie on attack - find highest defense among those
        int highestDefense = highestAttackSummons.Max(c => c.GetDefense());
        List<Card> tiedSummons = highestAttackSummons.Where(c => c.GetDefense() == highestDefense).ToList();

        if (tiedSummons.Count == 1) return tiedSummons[0];

        // Still tied - controller must choose
        // For now, return the first one (TODO: prompt controller to choose)
        return tiedSummons[0];
    }

    private void DrawOpeningHands() {
        Draw(playerOne, 5);
        Draw(playerTwo, 5);
    }

    /// <summary>
    /// Spawns test summons for bot player to test against.
    /// Creates a 1/1 Ghost, a 2/2 Goblin with Blitz, and a 5/5 Golem.
    /// </summary>
    private void SpawnBotTestSummons() {
        Player? botPlayer = playerOne.isBot ? playerOne : (playerTwo.isBot ? playerTwo : null);
        if (botPlayer == null) return;

        // Create 1/1 Ghost
        Token ghost = new Token(TokenType.Ghost, this);
        ghost.currentZone = Zone.Play;
        CreateTokenForPlayer(botPlayer, ghost, false);

        // Create 2/2 Goblin with Blitz
        Token goblin = new Token(TokenType.Goblin, this);
        goblin.attack = 2;
        goblin.defense = 2;
        goblin.keywords ??= new List<Keyword>();
        goblin.keywords.Add(Keyword.Blitz);
        goblin.currentZone = Zone.Play;
        CreateTokenForPlayer(botPlayer, goblin, false);

        // Create 5/5 Golem
        Token golem = new Token(TokenType.Golem, this);
        golem.attack = 5;
        golem.defense = 5;
        golem.currentZone = Zone.Play;
        CreateTokenForPlayer(botPlayer, golem, false);

        // Create 0/1 Plant
        Token plant = new Token(TokenType.Plant, this);
        plant.attack = 0;
        plant.defense = 1;
        plant.currentZone = Zone.Play;
        CreateTokenForPlayer(botPlayer, plant, false);
    }

    public void Draw(Player player, int amount) {
        for (int i = 0; i < amount; i++) {
            Debug.Assert(player.deck != null, "player.deck != null");
            if (player.deck.Count == 0) return;  // Can't draw from empty deck
            Card topCard = player.deck[0];
            AddToHand(player, topCard);
            // add the drawing of this card to the event list after uid and other values are set
            GameEvent playerEvent = GameEvent.CreateCardEvent(EventType.Draw, new CardDisplayData(topCard));
            GameEvent opponentEvent = new GameEvent(EventType.Draw, true);
            AddEventForBothPlayers(player, playerEvent, opponentEvent);
            player.deck.RemoveAt(0);
            // Track draw count and add trigger context
            player.turnDrawCount++;
            TriggerContext drawContext = new TriggerContext(Trigger.Draw, null, topCard);
            drawContext.triggerController = player;
            drawContext.isFirstDraw = player.turnDrawCount == 1;
            triggersToCheck.Add(drawContext);
        }
    }
    
    public void Mill(Player player, int amount) {
        int actuallyMilled = 0;
        List<Card> milledCards = new();
        for (int i = 0; i < amount; i++) {
            Debug.Assert(player.deck != null, "player.deck != null");
            // If deck is empty, stop milling (no death from mill in this game)
            if (player.deck.Count == 0) break;
            Card topCard = player.deck[0];
            player.deck.RemoveAt(0);
            AddCardToAllCardsPlayer(player, topCard);
            actuallyMilled++;
            milledCards.Add(topCard);

            // Check for replacement effect: summons go to exile instead of graveyard
            if (topCard.type == CardType.Summon &&
                player.playerPassives.Any(p => p.passive == Passive.SummonsToGraveyardExileInstead)) {
                Console.WriteLine($"[Mill] Replacement effect: {topCard.name} exiled instead of going to graveyard");
                AddToExile(player, topCard);
                // Send SendToZone event to Exile with source = Deck
                GameEvent exileEvent = GameEvent.CreateZoneGameEvent(Zone.Exile, new CardDisplayData(topCard), Zone.Deck);
                AddEventForBothPlayers(player, exileEvent);
            } else {
                AddToGraveyard(player, topCard, Zone.Deck);
                // Send normal Mill event (animates to graveyard)
                GameEvent gEvent = GameEvent.CreateCardEvent(EventType.Mill, new CardDisplayData(topCard));
                AddEventForBothPlayers(player, gEvent);
            }
            // Individual card mill trigger (for "when this is milled" effects)
            triggersToCheck.Add(new TriggerContext(Trigger.Mill, null, topCard));
        }
        // Batch mill trigger with count (for "when you mill X or more" effects like Undying Deathwood)
        if (actuallyMilled > 0) {
            TriggerContext batchContext = new TriggerContext(Trigger.Mill, null, null, milledCards);
            batchContext.triggerController = player;
            batchContext.millBatchSize = actuallyMilled;
            triggersToCheck.Add(batchContext);
        }
    }

    public static List<Card> GetTopCards(Player player, int amount) {
        Debug.Assert(player.deck != null, "no deck for player: " + player.playerName);
        return player.deck.Take(amount).ToList();
    }
    public void LookAtDeck(Player player, List<DeckDestination> deckDestinations, List<CardDisplayData> cardsToLookAt, List<CardSelectionData> cardSelectionDatas) {
        foreach(DeckDestination dd in deckDestinations) {
            lookedAtSelectionDestinations.Add(dd);
        }
        // Store the actual cards being looked at for remainder calculation
        cardsBeingLookedAt.Clear();
        foreach (CardDisplayData cdd in cardsToLookAt) {
            if (cardByUid.TryGetValue(cdd.uid, out Card? card)) {
                cardsBeingLookedAt.Add(card);
            }
        }
        GameEvent gEvent = GameEvent.CreateLookAtDeckEvent(cardSelectionDatas, cardsToLookAt);
        AddEventForPlayer(player, gEvent);
    }

    /// <summary>
    /// Simple peek at deck - shows cards to player without requiring selection or movement.
    /// Cards remain on top of deck in their current order.
    /// </summary>
    public void PeekAtDeck(Player player, List<CardDisplayData> cardsToLookAt) {
        GameEvent gEvent = GameEvent.CreatePeekEvent(cardsToLookAt);
        AddEventForPlayer(player, gEvent);
    }

    public void SendCardsToDestinations(List<List<int>> destinationUidLists, Player player) {
        // Collect all UIDs that were explicitly assigned to non-remainder destinations
        HashSet<int> assignedUids = new();
        for (int i = 0; i < lookedAtSelectionDestinations.Count; i++) {
            if (!lookedAtSelectionDestinations[i].IsRemainder()) {
                foreach (int uid in destinationUidLists[i]) {
                    assignedUids.Add(uid);
                }
            }
        }

        for(int i = 0; i < lookedAtSelectionDestinations.Count; i++) {
            DeckDestination currentDestination = lookedAtSelectionDestinations[i];
            List<int> uidList = destinationUidLists[i];

            // For remainder destinations, calculate the unassigned cards
            if (currentDestination.IsRemainder()) {
                uidList = cardsBeingLookedAt
                    .Where(c => !assignedUids.Contains(c.uid))
                    .Select(c => c.uid)
                    .ToList();
                Console.WriteLine($"[SendCardsToDestinations] Remainder destination: {currentDestination.GetDestination()}, uids=[{string.Join(", ", uidList)}]");
            }

            if (currentDestination.ordering == Ordering.Random) {
                uidList = GetShuffled(uidList);
            }
            switch (currentDestination.GetDestination()) {
                case DeckDestinationType.Hand:
                    foreach (Card card in uidList.Select(cardUid => cardByUid[cardUid])) {
                        SendToZone(player, Zone.Hand, card, currentDestination);
                    }
                    break;
                case DeckDestinationType.Top:
                    // Iterate in reverse so first card in list ends up on top
                    for (int j = uidList.Count - 1; j >= 0; j--) {
                        SendToZone(player, Zone.Deck, cardByUid[uidList[j]], currentDestination);
                    }
                    break;
                case DeckDestinationType.Bottom:
                    foreach (Card card in uidList.Select(cardUid => cardByUid[cardUid])) {
                        SendToZone(player, Zone.Deck, card, currentDestination);
                    }
                    break;
                case DeckDestinationType.Graveyard:
                    foreach (Card card in uidList.Select(cardUid => cardByUid[cardUid])) {
                        SendToZone(player, Zone.Graveyard, card);
                    }
                    break;
                case DeckDestinationType.Play:
                    foreach (Card card in uidList.Select(cardUid => cardByUid[cardUid])) {
                        SendToZone(player, Zone.Play, card);
                    }
                    break;
                default:
                    Console.WriteLine("DeckDestination does not exist");
                    break;
            }
        }
        lookedAtSelectionDestinations.Clear();
        cardsBeingLookedAt.Clear();
        // resolve unresolve stack obj if this halted resolve sequence
        unresolvedStackObj?.ResumeResolve(this);
    }
    

    private void AddCardToAllCardsPlayer(Player player, Card card) {
        if (player.allCardsPlayer.Contains(card)) return;
        player.allCardsPlayer.Add(card);
    }

    private void RemoveFromAllCardsPlayer(Player player, Card card) {
        if (!player.allCardsPlayer.Contains(card)) return;
        player.allCardsPlayer.Remove(card);
    }

    public void CreateAndAddResolveEvent(Player player, Card? sourceCard) {
        GameEvent gEvent = new GameEvent(EventType.Resolve);
        if (sourceCard != null) {
            sourceCard.chosenIndices.Clear();
            AddToGraveyard(player, sourceCard, Zone.Stack);
            gEvent.sourceCard = new CardDisplayData(sourceCard);
        }
        AddEventForBothPlayers(player, gEvent);
    }

    private void AddEventForBothPlayers(Player player, GameEvent playerEvent, GameEvent? opponentEvent = null) {
        player.eventList.Add(playerEvent);
        if (opponentEvent != null) {
            GetOpponent(player).eventList.Add(opponentEvent);
        } else {
            GameEvent gEvent = new GameEvent(playerEvent);
            gEvent.isOpponent = true;
            GetOpponent(player).eventList.Add(gEvent);
        }
    }
    
    private void AddEventForPlayer(Player player, GameEvent gEvent) {
        gEvent.isOpponent = false;
        player.eventList.Add(gEvent);
    }

    private void AddEventForOpponent(Player player, GameEvent gEvent) {
        gEvent.isOpponent = true;
        GetOpponent(player).eventList.Add(gEvent);
    }

    public void ClearEventList(Player player) {
        Console.WriteLine($"[ClearEventList] Clearing {player.eventList.Count} events for {player.playerName}, isAutoSkipping={isAutoSkipping}");
        player.eventList.Clear();
    }
    private void AddToHand(Player player, Card c) {
        player.hand.Add(c);
        AddCardToAllCardsPlayer(player, c);
        c.currentZone = Zone.Hand;
        c.playerHandOf = player;
    }

    public void RemoveFromHand(Player player, Card c) {
        player.hand.Remove(c);
        c.playerHandOf = null;
    }

    private void RemoveFromPlay(Player player, Card c) {
        player.playField.Remove(c);
        allCardsInPlay.Remove(c);
        triggersToCheck.Add(new TriggerContext(Trigger.LeftZone, Zone.Play, c));
        // Release any cards this card was detaining
        ReleaseDetainedCards(c);
    }

    private void AddToGraveyard(Player player, Card c, Zone? sourceZone = null) {
        player.graveyard.Add(c);
        AddCardToAllCardsPlayer(player, c);
        c.currentZone = Zone.Graveyard;

        // Ghost Deceiver pre-trigger check (hardcoded)
        // Check if Ghost Deceiver is in play and the card is a shadow summon
        if (c.type == CardType.Summon && c.tribe == Tribe.Shadow) {
            Card? ghostDeceiver = FindGhostDeceiverInPlay();
            if (ghostDeceiver != null) {
                Player gdOwner = GetControllerOf(ghostDeceiver);
                // Only prompt if the Ghost Deceiver controller is not a bot
                if (!gdOwner.isBot) {
                    ghostDeceiverPendingCard = c;
                    ghostDeceiverPendingPlayer = player;
                    ghostDeceiverPendingSourceZone = sourceZone;
                    ghostDeceiverOwner = gdOwner;
                    ghostDeceiverStage = 1;

                    // Send optional trigger prompt
                    var choicesText = new List<string> { "yes", "no" };
                    string optionMessage = $"Change where {c.name} entered the graveyard from?";
                    GameEvent gEvent = GameEvent.CreateOptionEvent(new PlayerChoice(choicesText, optionMessage));
                    AddEventForPlayer(gdOwner, gEvent);
                    // Check for graveyard passives before returning
                    CheckForPassives();
                    return; // Don't add the trigger yet - wait for response
                }
            }
        }

        triggersToCheck.Add(new TriggerContext(Trigger.EnteredZone, Zone.Graveyard, c, sourceZone: sourceZone));

        // Check for graveyard passives (e.g., Shadow Lord's aura)
        CheckForPassives();
    }

    /// <summary>
    /// Finds Ghost Deceiver (card ID 111) in play for either player.
    /// </summary>
    private Card? FindGhostDeceiverInPlay() {
        foreach (Card c in playerOne.playField) {
            if (c.id == 111) return c;
        }
        foreach (Card c in playerTwo.playField) {
            if (c.id == 111) return c;
        }
        return null;
    }

    /// <summary>
    /// Completes the Ghost Deceiver pre-trigger after zone selection.
    /// </summary>
    private void CompleteGhostDeceiverTrigger(Zone? chosenSourceZone = null) {
        if (ghostDeceiverPendingCard == null) return;

        Zone? finalSourceZone = chosenSourceZone ?? ghostDeceiverPendingSourceZone;
        triggersToCheck.Add(new TriggerContext(Trigger.EnteredZone, Zone.Graveyard, ghostDeceiverPendingCard, sourceZone: finalSourceZone));

        // Store state we need before clearing
        List<Card>? remainingDiscards = ghostDeceiverRemainingDiscards;
        Player? discardPlayer = ghostDeceiverDiscardPlayer;
        bool wasHandSizeDiscard = ghostDeceiverWasHandSizeDiscard;

        // Clear Ghost Deceiver state
        ghostDeceiverPendingCard = null;
        ghostDeceiverPendingPlayer = null;
        ghostDeceiverPendingSourceZone = null;
        ghostDeceiverOwner = null;
        ghostDeceiverStage = 0;
        ghostDeceiverRemainingDiscards = null;
        ghostDeceiverDiscardPlayer = null;
        ghostDeceiverWasHandSizeDiscard = false;

        // Continue with remaining discards if any
        if (remainingDiscards != null && remainingDiscards.Count > 0 && discardPlayer != null) {
            Console.WriteLine($"[CompleteGhostDeceiverTrigger] Continuing with {remainingDiscards.Count} remaining discards");
            foreach (Card c in remainingDiscards) {
                Discard(discardPlayer, c);
                // If Ghost Deceiver triggers again, halt and wait
                if (ghostDeceiverStage > 0) {
                    Console.WriteLine($"[CompleteGhostDeceiverTrigger] Ghost Deceiver triggered again, halting");
                    // Store remaining cards (excluding current one which just triggered)
                    int currentIndex = remainingDiscards.IndexOf(c);
                    ghostDeceiverRemainingDiscards = remainingDiscards.Skip(currentIndex + 1).ToList();
                    ghostDeceiverDiscardPlayer = discardPlayer;
                    ghostDeceiverWasHandSizeDiscard = wasHandSizeDiscard;
                    return; // Don't continue - wait for next Ghost Deceiver resolution
                }
            }
        }

        // If this was a hand size discard, continue with PassTurn flow
        if (wasHandSizeDiscard) {
            Console.WriteLine($"[CompleteGhostDeceiverTrigger] Continuing hand size discard flow");
            PassTurn();
            triggersToCheck.Add(TriggerContext.CreatePhaseTriggerContext(currentPhase));
            GameEvent gEvent = new GameEvent(EventType.NextPhase);
            AddEventForBothPlayers(GetPlayerByTurn(true), gEvent);
            if (currentPhase == Phase.Draw) {
                ReturnExiledCardsForPlayer(GetPlayerByTurn(true));
                Draw(GetPlayerByTurn(true), 1);
            }
        }
    }

    private void AddToExile(Player player, Card c) {
        player.exile.Add(c);
        AddCardToAllCardsPlayer(player, c);
        c.currentZone = Zone.Exile;
        triggersToCheck.Add(new TriggerContext(Trigger.EnteredZone, Zone.Exile, c));
    }

    /// <summary>
    /// Detains a card - removes it from opponent's hand and sends to exile,
    /// tracking it for return when the detaining card leaves play.
    /// </summary>
    public void DetainCard(Card detainer, Card detained, Player cardOwner) {
        // Remove from hand
        RemoveFromHand(cardOwner, detained);
        // Add to exile
        AddToExile(cardOwner, detained);
        // Track for return
        if (!detainedCards.ContainsKey(detainer.uid)) {
            detainedCards[detainer.uid] = new List<(Card, Player)>();
        }
        detainedCards[detainer.uid].Add((detained, cardOwner));
        // Create event for client
        GameEvent gEvent = GameEvent.CreateZoneGameEvent(Zone.Exile, new CardDisplayData(detained), Zone.Hand);
        gEvent.focusCard = new CardDisplayData(detained);
        AddEventForBothPlayers(cardOwner, gEvent);
    }

    /// <summary>
    /// Releases all cards detained by the specified card, returning them to their owners' hands.
    /// </summary>
    public void ReleaseDetainedCards(Card detainer) {
        if (!detainedCards.ContainsKey(detainer.uid)) return;
        foreach ((Card detained, Player owner) in detainedCards[detainer.uid]) {
            // Remove from exile
            owner.exile.Remove(detained);
            detained.currentZone = Zone.Hand;
            // Add back to owner's hand
            AddToHand(owner, detained);
            // Create event for client
            GameEvent gEvent = GameEvent.CreateZoneGameEvent(Zone.Hand, new CardDisplayData(detained), Zone.Exile);
            gEvent.focusCard = new CardDisplayData(detained);
            AddEventForBothPlayers(owner, gEvent);
        }
        detainedCards.Remove(detainer.uid);
    }

    private void AddToPlay(Player player, Card c) {
        triggersToCheck.Add(new TriggerContext(Trigger.EnteredZone, Zone.Play, c));
        player.playField.Add(c);
        c.currentZone = Zone.Play;
        c.lastControllingPlayer = player;
        AddCardToAllCardsPlayer(player, c);
        allCardsInPlay.Add(c);

        // Handle Sprout keyword - create Herb tokens when entering play
        int sproutAmount = c.GetSproutAmount();
        if (sproutAmount > 0) {
            for (int i = 0; i < sproutAmount; i++) {
                Token herb = new Token(TokenType.Herb, this);
                herb.currentZone = Zone.Play;
                CreateTokenForPlayer(player, herb, false);
            }
        }
    }
    
    private void AddToTokenZone(Player player, Token token) {
        int cardStackId = 0;
        token.currentZone = Zone.Play;  // Tokens are "in play", just in a different list
        // check for matching tokens to stack with
        foreach (Token t in player.tokens) {
            if (isStackableWith(token, t)) {
                cardStackId = player.cardToCardStackId[t];
                break;
            }
        }
        player.tokens.Add(token);
        // if no matching tokens, create a new card stack with a new id
        if (cardStackId == 0) {
            cardStackId = player.cardToCardStackId.Count + 1;
        }
        player.cardToCardStackId.Add(token, cardStackId);
        AddCardToAllCardsPlayer(player, token);
        // creat token event for both players
        GameEvent gEvent = GameEvent.CreateCardEvent(EventType.CreateToken, new CardDisplayData(token, token.tokenType));
        gEvent.universalInt = cardStackId;
        AddEventForBothPlayers(player, gEvent);
        triggersToCheck.Add(new TriggerContext(Trigger.EnteredZone, Zone.Play, token));
    }

    /// <summary>
    /// Removes a token from the token zone (used when converting tokens to summons)
    /// </summary>
    public void RemoveFromTokenZone(Player player, Token token) {
        int cardStackId = player.cardToCardStackId[token];
        player.tokens.Remove(token);
        player.cardToCardStackId.Remove(token);
        GameEvent gEvent = GameEvent.CreateCardEvent(EventType.RemoveToken, new CardDisplayData(token, token.tokenType));
        gEvent.universalInt = cardStackId;
        AddEventForBothPlayers(player, gEvent);
    }

    private bool isStackableWith(Card card1, Card card2) {
        // For non-summon tokens (herbs, stones), just compare token types - they should always stack
        if (card1 is Token t1 && card2 is Token t2) {
            if (t1.type == CardType.Token && t2.type == CardType.Token) {
                // Non-summon tokens stack by tokenType only
                return t1.tokenType == t2.tokenType;
            }
        }

        // For summon-type tokens and regular cards, use the original logic
        if (card1.id != card2.id) return false;
        // if they both have no passives
        if (card1.GetPassives().Count == 0 && card2.GetPassives().Count == 0) return true;
        // if only one has passives
        if (card1.GetPassives().Count == 0 || card2.GetPassives().Count == 0) return false;
        // if either has more than 1 passive
        if(card1.GetPassives().Count > 1 || card2.GetPassives().Count > 1) return false;
        // if both have the same passive
        return card1.GetPassives()[0].passive == card2.GetPassives()[0].passive;
    }

    /// <summary>
    ///  Fisher-Yates shuffle algorithm implementation. Swap each card in the deck with another from a
    ///  random position.
    /// </summary>
    /// <param name="cardList">The list of card ids to be shuffled.</param>
    public void ShuffleDeck(List<Card> cardList) {
        Random rng = new Random();
        int n = cardList.Count;
        while (n-- > 1) {
            int k = rng.Next(n + 1);
            (cardList[k], cardList[n]) = (cardList[n], cardList[k]);
        }
    }

    private List<int> GetShuffled(List<int> uidList) {
        List<int> tempList = uidList.ToList();
        Random rng = new Random();
        int n = tempList.Count;
        while (n-- > 1) {
            int k = rng.Next(n + 1);
            (tempList[k], tempList[n]) = (tempList[n], tempList[k]);
        }
        return tempList;
    }
    
    // Should add a null check for the deck so the client can't change decks mid-match.
    public void SetPlayerDeck(int playerId, int deckId) {
        using SQLiteConnection conn = SqlFunctions.CreateConnection();
        List<int>? deckCardIds = SqlFunctions.SqlGetDeckCards(conn, deckId);
        Debug.Assert(deckCardIds != null, "deckCardIds cannot be null");
        List<Card> deckCards = deckCardIds.Select(id => Card.GetCard(GetNextUid(), id)).ToList();
        accountIdToPlayer[playerId].deck = deckCards;
        Console.WriteLine("Deck added to game for user: " + accountIdToPlayer[playerId].playerName
                + ". Deck card list is: " + deckCards);
    }

    private void SetFirstPlayer() {
        // In test matches, human player always goes first
        if (playerOne.isBot) {
            turnPlayerId = playerTwo.playerId;
            return;
        }
        if (playerTwo.isBot) {
            turnPlayerId = playerOne.playerId;
            return;
        }

        // Normal matches: random first player
        Random rng = new Random();
        int randNum = rng.Next(1, 3);
        turnPlayerId = randNum == 1 ? playerOne.playerId : playerTwo.playerId;
    }
    

    public void SubmitAttack(Player submittingPlayer) {
        // For Ground Tactics: the turn player is the "attacker" even though someone else controlled assignments
        Player actualAttackingPlayer;
        if (groundTacticsControllerId != null) {
            actualAttackingPlayer = GetPlayerByTurn(true);
            Console.WriteLine($"[SubmitAttack] Ground Tactics: {submittingPlayer.playerName} submitted, but {actualAttackingPlayer.playerName} is the actual attacker");
        } else {
            actualAttackingPlayer = submittingPlayer;
        }

        actualAttackingPlayer.attackedThisTurn = true;  // Track that this player attacked this turn
        List<Card> currentAttackingCards = currentAttackUids.Select(pair => cardByUid[pair.Key]).ToList();
        triggersToCheck.Add(new TriggerContext(Trigger.Attack, null, null, currentAttackingCards));

        // Add AttackedSummon triggers for each attacker targeting a summon (not a player)
        foreach (var pair in currentAttackUids) {
            int targetUid = pair.Value;
            if (!IsPlayerUid(targetUid) && cardByUid.TryGetValue(targetUid, out Card? targetCard)) {
                Card attacker = cardByUid[pair.Key];
                // AttackedSummon trigger fires for the attacker, with the target summon as context
                triggersToCheck.Add(new TriggerContext(Trigger.AttackedSummon, card: attacker, triggerController: actualAttackingPlayer, targetCard: targetCard));
            }
        }

        // Clear Ground Tactics state after attack submission
        groundTacticsControllerId = null;

        CheckForTriggersAndPassives(EventType.Attack, actualAttackingPlayer);
    }

    public void AssignAttack(Player attackingPlayer, (int, int) attackUids) {
        currentAttackUids.Add(attackUids.Item1, attackUids.Item2);
        GameEvent gEvent = GameEvent.CreateAttackEvent(attackUids, true);
        AddEventForOpponent(attackingPlayer, gEvent);
    }

    public void UnAssignAttack(Player attackingPlayer, int attackerUid) {
        (int, int) attackUids = (attackerUid, currentAttackUids[attackerUid]);
        currentAttackUids.Remove(attackerUid);
        GameEvent gEvent = GameEvent.CreateAttackEvent(attackUids, false);
        AddEventForOpponent(attackingPlayer, gEvent);
    }
    
    
    public void AddSecondaryAttacker(Player attackingPlayer, (int, int) attackUids) {
        currentAttackUids.Add(attackUids.Item1, attackUids.Item2);
        GameEvent gEvent = GameEvent.CreateAttackEvent(attackUids, true);
        AddEventForOpponent(attackingPlayer, gEvent);
        requiredAttackTargets--;
        if (requiredAttackTargets == 0) {
            CheckForTriggersAndPassives(EventType.Resolve);
        }
    }

    public int GetNextUid() {
        return uidCounter += 1;
    }

    public void ScheduleExiledCardReturn(int playerId, Card card) {
        if (!exiledCardsAwaitingReturn.ContainsKey(playerId)) {
            exiledCardsAwaitingReturn[playerId] = new List<Card>();
        }
        exiledCardsAwaitingReturn[playerId].Add(card);
    }

    private void ReturnExiledCardsForPlayer(Player player) {
        if (!exiledCardsAwaitingReturn.TryGetValue(player.playerId, out List<Card>? cards) || cards.Count == 0) {
            return;
        }

        Console.WriteLine($"[ReturnExiledCards] Returning {cards.Count} exiled cards for {player.playerName}'s draw phase");
        foreach (Card card in cards.ToList()) {
            Player owner = GetOwnerOf(card);
            // Return to play using SendToZone (handles the event properly)
            SendToZone(owner, Zone.Play, card);
            Console.WriteLine($"[ReturnExiledCards] Returned {card.name} to play under {owner.playerName}'s control");
        }
        cards.Clear();
    }

    /// <summary>
    /// Schedules a card to move to a zone at a specific phase (for Endless Garden, Unending Sundew, etc.)
    /// </summary>
    public void ScheduleDelayedZoneEffect(Card card, Zone destination, Phase phase, int playerId) {
        delayedZoneEffects.Add((card, destination, phase, playerId));
        Console.WriteLine($"[ScheduleDelayedZoneEffect] Scheduled {card.name} to go to {destination} at {phase} phase for player {playerId}");
    }

    /// <summary>
    /// Processes delayed zone effects for the given player and phase
    /// </summary>
    public void ProcessDelayedZoneEffects(Player player, Phase phase) {
        var effectsToProcess = delayedZoneEffects
            .Where(e => e.playerId == player.playerId && e.phase == phase)
            .ToList();

        if (effectsToProcess.Count == 0) return;

        Console.WriteLine($"[ProcessDelayedZoneEffects] Processing {effectsToProcess.Count} delayed effects for {player.playerName}'s {phase} phase");
        foreach (var (card, destination, _, _) in effectsToProcess) {
            // Return card to its owner (for ownersControl behavior)
            Player owner = GetOwnerOf(card);
            SendToZone(owner, destination, card);
            Console.WriteLine($"[ProcessDelayedZoneEffects] Moved {card.name} to {destination} under {owner.playerName}'s control");
            delayedZoneEffects.Remove((card, destination, phase, player.playerId));
        }
    }

    public void SendToZone(Player targetPlayer, Zone destination, Card targetCard, DeckDestination? deckDestination = null) {
        // cards leaving play always go to their owner's zones (unless stated otherwise);
        Zone sourceZone = targetCard.currentZone;

        // Tokens can't go to hand, deck, or exile - destroy them instead
        if (targetCard is Token && destination != Zone.Play && destination != Zone.Graveyard) {
            Destroy(targetCard);
            return;
        }

        // Replacement effect: summons go to exile instead of graveyard if player has the passive
        if (destination == Zone.Graveyard && targetCard.type == CardType.Summon) {
            Player cardOwner = GetOwnerOf(targetCard);
            if (cardOwner.playerPassives.Any(p => p.passive == Passive.SummonsToGraveyardExileInstead)) {
                destination = Zone.Exile;
            }
        }

        // CantSpecialSummon: block cards from entering play from zones other than hand/stack
        // Special summons are cards that enter play from graveyard, exile, deck, etc.
        if (destination == Zone.Play && sourceZone != Zone.Hand && sourceZone != Zone.Stack) {
            // Check if the targetCard itself has CantSpecialSummon with SelfOnly scope
            bool targetCantBeSpecialSummoned = targetCard.passiveEffects?.Any(p =>
                p.passive == Passive.CantSpecialSummon && p.scope == Scope.SelfOnly) == true;
            if (targetCantBeSpecialSummoned) {
                Console.WriteLine($"[SendToZone] Blocked special summon of {targetCard.name} - card has CantSpecialSummon (selfOnly)");
                return;
            }

            // Check if any card in play has CantSpecialSummon with All scope (global block)
            bool globalCantSpecialSummon = targetPlayer.playField.Any(card =>
                card.passiveEffects?.Any(p => p.passive == Passive.CantSpecialSummon && p.scope == Scope.All) == true);
            if (globalCantSpecialSummon) {
                Console.WriteLine($"[SendToZone] Blocked special summon of {targetCard.name} due to global CantSpecialSummon passive");
                return;
            }
        }

        RemoveFromCurrentZone(targetCard);
        bool needsPassiveCheck = false;
        // add to the new zone
        switch (destination) {
            case Zone.Hand:
                AddToHand(targetPlayer, targetCard);
                break;
            case Zone.Play:
                AddToPlay(targetPlayer, targetCard);
                needsPassiveCheck = true; // Delay CheckForPassives until after SendToZone event is queued
                break;
            case Zone.Graveyard:
                AddToGraveyard(targetPlayer, targetCard, sourceZone);
                break;
            case Zone.Exile:
                AddToExile(targetPlayer, targetCard);
                break;
            case Zone.Deck:
                Debug.Assert(deckDestination != null, "There is no deck destination for this SendToZone Event");
                targetPlayer.allCardsPlayer.Remove(targetCard);
                targetCard.currentZone = Zone.Deck;
                switch (deckDestination.deckDestination) {
                    case DeckDestinationType.Bottom:
                        targetPlayer.deck!.Add(targetCard);  // Append to end = bottom
                        break;
                    case DeckDestinationType.Top:
                        targetPlayer.deck!.Insert(0, targetCard);  // Insert at index 0 = top
                        break;
                    default:
                        Console.WriteLine("There is no DeckDestination type for this SendToZone (deck) Event");
                        break;
                }
                break;
            default:
                Console.WriteLine("destination for match.SendToZone not implemented.");
                break;
        }
        CheckForDeaths();
        // create client event
        GameEvent gEvent = GameEvent.CreateZoneGameEvent(destination, new CardDisplayData(targetCard), sourceZone);
        // this was sent using a deck destination effect
        if (deckDestination != null) {
            switch (deckDestination.deckDestination) {
                // Card goes to deck - client needs the card info to animate it leaving hand/play
                case DeckDestinationType.Bottom or DeckDestinationType.Top:
                    AddEventForBothPlayers(targetPlayer, gEvent);
                    if (needsPassiveCheck) CheckForPassives();
                    return;
                case DeckDestinationType.Hand when !deckDestination.reveal:
                    // use an event without the selected card for opponent (it wasn't revealed)
                    GameEvent playerEvent = new GameEvent(gEvent) { focusCard = new CardDisplayData(targetCard) };
                    gEvent.isOpponent = true;
                    AddEventForBothPlayers(targetPlayer, playerEvent, gEvent);
                    if (needsPassiveCheck) CheckForPassives();
                    return;
            }

            // all other cases should reveal the card for both players
        }
        gEvent.focusCard = new CardDisplayData(targetCard);
        AddEventForBothPlayers(targetPlayer, gEvent);
        // Apply existing auras to card entering play - must be after SendToZone event is queued
        // so client creates the CardDisplay before RefreshCardDisplays tries to update it
        if (needsPassiveCheck) CheckForPassives();
    }

    private void RemoveFromCurrentZone(Card card) {
        switch (card.currentZone) {
            case Zone.Play:
                RemoveFromPlay(GetControllerOf(card), card);
                // Remove passives this card granted to other cards (aura cleanup)
                RemovePassivesFromSource(card);
                // Re-check passives in case other auras need to be reapplied
                CheckForPassives();
                break;
            case Zone.Hand:
                RemoveFromHand(card.playerHandOf!, card);
                break;
            case Zone.Graveyard:
                GetOwnerOf(card).graveyard.Remove(card);
                // Remove passives this card granted from graveyard (e.g., Shadow Lord's aura)
                RemovePassivesFromSource(card);
                CheckForPassives();
                break;
            case Zone.Exile:
                GetOwnerOf(card).exile.Remove(card);
                break;
            case Zone.Deck:
                GetOwnerOf(card).deck!.Remove(card);
                break;
            case Zone.Stack:
                // Card was already removed from the stack (e.g., by CounterStackItem)
                break;
            default:
                Console.WriteLine("Unknown zone for RemoveFromCurrentZone: " + card.currentZone);
                break;
        }
    }

    public void DealDamage(int targetUid, int amount, bool isSpellDamage = false, List<Restriction>? restrictions = null) {
        if (cardByUid.TryGetValue(targetUid, out var card)) {
            if (card.GetPassives().Any(p => p.passive == Passive.CantTakeDamage)) return;
            card.damageTaken += amount;
            if (isSpellDamage) card.tookSpellDamage = true;
            GameEvent gEvent = GameEvent.CreateRefreshCardDisplayEvent(card);
            AddEventForBothPlayers(GetControllerOf(card), gEvent);
        } else {
            // TODO you might want to consider an independent take damage function instead of LoseLife
            // TODO they might eventually be separate triggers just like MTG
            LoseLife(PlayerByUid(targetUid), amount, restrictions);
        }
        CheckForDeaths();
    }

    public void GainLife(Player affectedPlayer, int? amount) {
        Debug.Assert(amount != null, "there is no amount associated with this gainLife Effect");

        // Check if player has CantGainLife passive
        if (affectedPlayer.playerPassives.Any(p => p.passive == Passive.CantGainLife)) {
            // Player can't gain life - do nothing
            return;
        }

        affectedPlayer.lifeTotal += amount.Value;
        // TODO check for life gain triggers
        GameEvent gEvent = GameEvent.CreateGameEventWithAmount(EventType.GainLife, false, amount.Value);
        AddEventForBothPlayers(affectedPlayer, gEvent);
        RefreshLifeDependentCards(affectedPlayer);
    }

    public void LoseLife(Player affectedPlayer, int? amount, List<Restriction>? restrictions = null) {
        Debug.Assert(amount != null, "there is no amount associated with this loseLife Effect");

        int actualAmount = amount.Value;

        // Check CantReduceBelowOne restriction
        if (restrictions != null && restrictions.Contains(Restriction.CantReduceBelowOne)) {
            int newLifeTotal = affectedPlayer.lifeTotal - actualAmount;
            if (newLifeTotal < 1) {
                actualAmount = affectedPlayer.lifeTotal - 1; // Only reduce to 1 LP
                if (actualAmount < 0) actualAmount = 0; // Don't gain life if already at or below 1
            }
        }

        affectedPlayer.lifeTotal -= actualAmount;
        // TODO check for lose life triggers
        GameEvent gEvent = GameEvent.CreateGameEventWithAmount(EventType.LoseLife, false, actualAmount);
        gEvent.universalInt = affectedPlayer.lifeTotal;  // Include expected life total for client verification
        AddEventForBothPlayers(affectedPlayer, gEvent);
        RefreshLifeDependentCards(affectedPlayer);
        CheckWinCondition();
    }

    public void SetLifeTotal(Player affectedPlayerId, int? amount) {
        Debug.Assert(amount != null, "there is no amount associated with this SetLifeTotal Effect");
        affectedPlayerId.lifeTotal = amount.Value;
        // TODO check for life change triggers (life gain and loss)
        GameEvent gEvent = GameEvent.CreateGameEventWithAmount(EventType.SetLifeTotal, false, amount.Value);
        AddEventForBothPlayers(affectedPlayerId, gEvent);
        RefreshLifeDependentCards(affectedPlayerId);
        CheckWinCondition();
    }

    /// <summary>
    /// Checks if any player has reached 0 life and handles game/series end
    /// </summary>
    public void CheckWinCondition() {
        if (isGameOver) return; // Already determined

        Player? loser = null;
        Player? winner = null;

        if (playerOne.lifeTotal <= 0) {
            loser = playerOne;
            winner = playerTwo;
        } else if (playerTwo.lifeTotal <= 0) {
            loser = playerTwo;
            winner = playerOne;
        }

        if (loser != null && winner != null) {
            EndGame(winner, loser);
        }
    }

    /// <summary>
    /// Public method to end game due to forfeit or disconnect
    /// </summary>
    public void EndGame(Player winner, string reason) {
        Player loser = GetOpponent(winner);
        Console.WriteLine($"Game {matchId} ended due to: {reason}");
        EndGame(winner, loser);
    }

    /// <summary>
    /// Ends the current game with the specified winner and loser
    /// </summary>
    private void EndGame(Player winner, Player loser) {
        isGameOver = true;
        gameOverAt = DateTime.UtcNow;
        winnerId = winner.uid;
        loserId = loser.uid;

        // Update series wins
        if (winner == playerOne) {
            playerOneSeriesWins++;
        } else {
            playerTwoSeriesWins++;
        }

        // Check if series is over (first to majority wins)
        int winsNeeded = (bestOf / 2) + 1; // Bo1=1, Bo3=2, Bo5=3
        if (playerOneSeriesWins >= winsNeeded) {
            isSeriesOver = true;
            seriesWinnerId = playerOne.uid;
        } else if (playerTwoSeriesWins >= winsNeeded) {
            isSeriesOver = true;
            seriesWinnerId = playerTwo.uid;
        }

        // Send game over event to both players
        GameEvent gameOverEvent = GameEvent.CreateEndGameEvent(winnerId.Value);
        AddEventForBothPlayers(winner, gameOverEvent);

        Console.WriteLine($"Game {matchId} ended - Winner: {winner.playerName}, Loser: {loser.playerName}");
        Console.WriteLine($"Series score: {playerOne.playerName} {playerOneSeriesWins} - {playerTwoSeriesWins} {playerTwo.playerName}");
        if (isSeriesOver) {
            Console.WriteLine($"Series over! Winner: {winner.playerName}");
        }
    }

    public void GrantKeyword(Player player, Card targetCard, Keyword keyword, int amount = 1, bool thisTurn = false) {
        PassiveEffect kwPassive = new PassiveEffect(Passive.GrantKeyword, keyword);
        kwPassive.keywordAmount = amount;
        kwPassive.thisTurn = thisTurn;
        targetCard.grantedPassives.Add(kwPassive);
        Console.WriteLine($"[GrantKeyword] Granted {keyword} {amount} to {targetCard.name} (thisTurn={thisTurn})");
        GameEvent gEvent = GameEvent.CreateRefreshCardDisplayEvent(targetCard);
        AddEventForBothPlayers(player, gEvent);
    }
    
    private int GetAmountUntilCardType(CardType? cardType, Player player) {
        Debug.Assert(player.deck != null, "no deck for " + player.playerName);
        int amount = 0;
        foreach (Card c in player.deck) {
            amount++;
            if (c.type == cardType) {
                return amount;
            }
        }
        // deck is empty
        return amount;
        
    }
    
    public void AssignTargets(Player player, List<int> targetedUids) {
        Console.WriteLine($"AssignTargets: received {targetedUids.Count} uids: [{string.Join(", ", targetedUids)}]");

        // Handle "each player chooses" selections (e.g., Return)
        if (eachPlayerEffect != null) {
            HandleEachPlayerSelection(player, targetedUids);
            return;
        }

        // Handle resolve-time target selection (e.g., Consider)
        if (resolveTimeTargetEffect != null) {
            Console.WriteLine($"  Resolve-time target assignment for effect: {resolveTimeTargetEffect.effect}");
            foreach (int uid in targetedUids) {
                Console.WriteLine($"  Adding uid {uid} to resolveTimeTargetEffect.targetUids");
                resolveTimeTargetEffect.targetUids.Add(uid);
            }
            resolveTimeTargetEffect = null;
            // Resume stack resolution
            Debug.Assert(unresolvedStackObj != null, "No unresolved stack object for resolve-time targets");
            unresolvedStackObj.ResumeResolve(this);
            return;
        }

        Effect focusEffect = effectsWithTargets.Last();
        Console.WriteLine($"  Focus effect type: {focusEffect.effect} (hash={focusEffect.GetHashCode()})");
        Console.WriteLine($"  focusEffect.targetUids BEFORE = [{string.Join(", ", focusEffect.targetUids)}]");
        // assign targets for effects
        foreach (int uid in targetedUids) {
            Console.WriteLine($"  Adding uid {uid} to targetUids");
            focusEffect.targetUids.Add(uid);
        }
        Console.WriteLine($"  focusEffect.targetUids AFTER = [{string.Join(", ", focusEffect.targetUids)}]");

        // Validate sameZone constraint - all targets must be in the same graveyard
        if (focusEffect.sameZone && focusEffect.targetUids.Count > 1) {
            bool allInPlayerOneGraveyard = focusEffect.targetUids.All(uid =>
                cardByUid.ContainsKey(uid) && playerOne.graveyard.Contains(cardByUid[uid]));
            bool allInPlayerTwoGraveyard = focusEffect.targetUids.All(uid =>
                cardByUid.ContainsKey(uid) && playerTwo.graveyard.Contains(cardByUid[uid]));
            if (!allInPlayerOneGraveyard && !allInPlayerTwoGraveyard) {
                Console.WriteLine($"  ERROR: sameZone constraint violated - targets are from different graveyards");
                // Clear targets and re-request selection
                focusEffect.targetUids.Clear();
                List<int> possibleTargets = GetPossibleTargets(player, focusEffect);
                string message = focusEffect.EffectToString(this) + " (must select from same graveyard)";
                CreateAndAddNewTargetSelectionEvent(player, possibleTargets, focusEffect.GetTargetMax(), message);
                effectsWithTargets.Add(focusEffect);
                return;
            }
        }

        if (focusEffect.additionalEffects != null) {
            foreach (Effect e in focusEffect.additionalEffects) {
                // must be a choose effect
                if (e.effect != EffectType.Choose) continue;
                // if it has conditions
                if (e.conditions != null) {
                    // all conditions must verify using first target -> TODO eventually you'll have to iterate the
                    // TODO targets for multi-target effects.
                    if (!e.conditions.All(c => c.Verify(this, player, cardByUid[targetedUids[0]]))) continue;
                }
                // if it doesn't have conditions or they are all verified
                Debug.Assert(e.choices != null, "this choice effect doesn't have any choices");
                additionalChoiceEffects.Add(focusEffect.additionalEffects, e);
                choiceCard = focusEffect.sourceCard;
            }
        }
        effectsWithTargets.Remove(focusEffect);
        if (effectsWithTargets.Count != 0) return;

        // if we're doing post-choice targeting, continue with choice flow
        if (pendingChoiceTargeting) {
            pendingChoiceTargeting = false;
            Debug.Assert(pendingChoicePlayer != null, "pendingChoicePlayer is null");
            Debug.Assert(pendingChoiceEffectDict != null, "pendingChoiceEffectDict is null");

            KeyValuePair<List<Effect>, Effect> pair = pendingChoiceEffectDict.First();
            Debug.Assert(pair.Value.choices != null, "no choices in pendingChoiceEffectDict");

            // If more choices remain, prompt for next choice
            if (remainingChoices > 0) {
                HandleChoice(pair.Value.choices, pendingChoicePlayer, currentForOpponentChoice);
                return;
            }

            // All choices made - continue with normal choice completion flow
            ContinueAfterAllChoicesMade(pendingChoicePlayer, pendingChoiceEffectDict, pendingChoiceCastingStage);
            return;
        }

        // if it's a card your selecting targets for
        if (cardBeingCast != null) {
            AttemptToCast(player, cardBeingCast, CastingStage.AdditionalChoices);
            return;
        }
        // if it's an activated effect
        if (currentActivatedEffect != null) {
            ActivateAbility(player, currentActivatedEffect);
            return;
        }
        // if it's a triggered effect
        Debug.Assert(currentPlayerToPassTo != null, "there is no current player to pass to");
        HandleTriggers(player, currentPlayerToPassTo, TriggerStage.Ordering);
    }

    /// <summary>
    /// Handles effects where each player makes their own selection (e.g., Return).
    /// Returns true if waiting for player input, false if no input needed.
    /// </summary>
    public bool HandleEachPlayerEffect(Effect effect, Player effectOwner) {
        Console.WriteLine($"[EachPlayer] HandleEachPlayerEffect called, effect: {effect.effect}");
        eachPlayerEffect = effect;
        eachPlayerSelections.Clear();
        pendingEachPlayerResponses.Clear();

        // For SendToZone effects with zone set and all=true, apply to both players without selection
        if (effect.effect == EffectType.SendToZone && effect.zone != null && effect.all) {
            Console.WriteLine($"[EachPlayer] SendToZone with zone={effect.zone}, all=true - applying to both players");
            // Apply to both players - no selection needed
            effect.Resolve(this, playerOne);
            effect.Resolve(this, playerTwo);
            eachPlayerEffect = null;
            return false; // No input needed
        }

        // For SendToZone effects that need target selection
        if (effect.effect == EffectType.SendToZone) {
            Qualifier eQualifier = new Qualifier(effect, playerOne);

            // Check player one's play field
            List<Card> p1Targets = GetQualifiedCards(playerOne.playField.ToList(), eQualifier);
            if (p1Targets.Count > 0) {
                pendingEachPlayerResponses.Add(playerOne.playerId);
                List<int> targetUids = p1Targets.Select(c => c.uid).ToList();
                int targetAmount = effect.amount ?? 1;
                string message = $"Choose {targetAmount} summon{(targetAmount > 1 ? "s" : "")} to return to hand";
                CreateAndAddNewTargetSelectionEvent(playerOne, targetUids, targetAmount, message);
                Console.WriteLine($"[EachPlayer] Player {playerOne.playerName} has {p1Targets.Count} valid targets");
            }

            // Check player two's play field
            List<Card> p2Targets = GetQualifiedCards(playerTwo.playField.ToList(), eQualifier);
            if (p2Targets.Count > 0) {
                pendingEachPlayerResponses.Add(playerTwo.playerId);
                List<int> targetUids = p2Targets.Select(c => c.uid).ToList();
                int targetAmount = effect.amount ?? 1;
                string message = $"Choose {targetAmount} summon{(targetAmount > 1 ? "s" : "")} to return to hand";
                CreateAndAddNewTargetSelectionEvent(playerTwo, targetUids, targetAmount, message);
                Console.WriteLine($"[EachPlayer] Player {playerTwo.playerName} has {p2Targets.Count} valid targets");
            }

            // If no one has targets, resolve immediately with no effect
            if (pendingEachPlayerResponses.Count == 0) {
                Console.WriteLine("[EachPlayer] No players have valid targets, skipping effect");
                eachPlayerEffect = null;
                return false;
            }

            Console.WriteLine($"[EachPlayer] Waiting for {pendingEachPlayerResponses.Count} player(s)");
            return true;
        }

        // For ShuffleDeck with eachPlayer - apply to both players
        if (effect.effect == EffectType.ShuffleDeck) {
            Console.WriteLine($"[EachPlayer] ShuffleDeck - applying to both players");
            ShuffleDeck(playerOne.deck);
            ShuffleDeck(playerTwo.deck);
            eachPlayerEffect = null;
            return false; // No input needed
        }

        // For Draw with eachPlayer - apply to both players
        if (effect.effect == EffectType.Draw) {
            Console.WriteLine($"[EachPlayer] Draw {effect.amount} - applying to both players");
            int amount = effect.amount ?? 1;
            Draw(playerOne, amount);
            Draw(playerTwo, amount);
            eachPlayerEffect = null;
            return false; // No input needed
        }

        // For Sacrifice with eachPlayer - each player chooses a summon to sacrifice
        if (effect.effect == EffectType.Sacrifice) {
            Console.WriteLine($"[EachPlayer] Sacrifice - checking both players");
            Qualifier eQualifier = new Qualifier(effect, playerOne);

            // Check player one's summons
            List<Card> p1Summons = playerOne.playField.Where(c => c.type == CardType.Summon).ToList();
            if (effect.cardType != null) {
                p1Summons = p1Summons.Where(c => c.type == effect.cardType).ToList();
            }
            if (p1Summons.Count == 1) {
                // Auto-sacrifice the only summon
                Console.WriteLine($"[EachPlayer] Player {playerOne.playerName} has 1 summon, auto-sacrificing");
                Destroy(p1Summons[0]);
            } else if (p1Summons.Count > 1) {
                pendingEachPlayerResponses.Add(playerOne.playerId);
                List<int> targetUids = p1Summons.Select(c => c.uid).ToList();
                string message = "Choose a summon to sacrifice";
                CreateAndAddNewTargetSelectionEvent(playerOne, targetUids, 1, message);
                Console.WriteLine($"[EachPlayer] Player {playerOne.playerName} has {p1Summons.Count} summons, prompting choice");
            }

            // Check player two's summons
            List<Card> p2Summons = playerTwo.playField.Where(c => c.type == CardType.Summon).ToList();
            if (effect.cardType != null) {
                p2Summons = p2Summons.Where(c => c.type == effect.cardType).ToList();
            }
            if (p2Summons.Count == 1) {
                // Auto-sacrifice the only summon
                Console.WriteLine($"[EachPlayer] Player {playerTwo.playerName} has 1 summon, auto-sacrificing");
                Destroy(p2Summons[0]);
            } else if (p2Summons.Count > 1) {
                pendingEachPlayerResponses.Add(playerTwo.playerId);
                List<int> targetUids = p2Summons.Select(c => c.uid).ToList();
                string message = "Choose a summon to sacrifice";
                CreateAndAddNewTargetSelectionEvent(playerTwo, targetUids, 1, message);
                Console.WriteLine($"[EachPlayer] Player {playerTwo.playerName} has {p2Summons.Count} summons, prompting choice");
            }

            // If no one needs to choose, we're done
            if (pendingEachPlayerResponses.Count == 0) {
                Console.WriteLine("[EachPlayer] No players need to choose, sacrifice complete");
                eachPlayerEffect = null;
                return false;
            }

            Console.WriteLine($"[EachPlayer] Waiting for {pendingEachPlayerResponses.Count} player(s) to choose");
            return true;
        }

        // Unsupported effect type for eachPlayer
        Console.WriteLine($"[EachPlayer] Effect type {effect.effect} not supported for eachPlayer");
        eachPlayerEffect = null;
        return false;
    }

    /// <summary>
    /// Requests player to select cards to discard.
    /// variableAmount=true: select 0 to N cards (e.g., Ghastly - discard any number)
    /// variableAmount=false: select exactly N cards (e.g., Loot Ghost - discard 2)
    /// Returns true if waiting for player input, false if no cards to select.
    /// </summary>
    public bool RequestPlayerChoiceDiscard(Player player, Effect effect, bool variableAmount) {

        // Find matching cards in hand based on cardType and tribe (if specified)
        List<int> selectableUids = new();
        Qualifier qualifier = new Qualifier(effect, player);

        foreach (Card c in player.hand) {
            bool qualifies = QualifyCard(c, qualifier);
            if (qualifies) {
                selectableUids.Add(c.uid);
            }
        }

        if (selectableUids.Count == 0) {
            return false;
        }

        // Bot auto-discards highest index cards (last in hand)
        if (player.isBot) {
            int discardCount = variableAmount ? 0 : Math.Min(effect.amount ?? 1, selectableUids.Count);
            // Select from the end of the list (highest index cards)
            List<int> botSelectedUids = selectableUids.TakeLast(discardCount).ToList();
            foreach (int uid in botSelectedUids) {
                effect.targetUids.Add(uid);
            }
            return false;
        }

        // Store the effect for later processing
        playerChoiceDiscardEffect = effect;

        // Build message and determine selection parameters
        string message;
        int selectionAmount;

        if (variableAmount) {
            // Variable selection (0 to all matching cards)
            string tribeName = effect.tribe?.ToString().ToLower() ?? "";
            string cardTypeName = effect.cardType?.ToString().ToLower() ?? "card";
            string descriptor = string.IsNullOrEmpty(tribeName) ? cardTypeName : $"{tribeName} {cardTypeName}";
            message = $"Choose {descriptor}s to discard (0 to {selectableUids.Count})";
            selectionAmount = selectableUids.Count;
        } else {
            // Fixed amount selection (exactly N cards, or all if fewer available)
            int requiredAmount = effect.amount ?? 1;
            int actualAmount = Math.Min(requiredAmount, selectableUids.Count);
            string plural = actualAmount == 1 ? "" : "s";
            message = $"Choose {actualAmount} card{plural} to discard";
            selectionAmount = actualAmount;
        }


        GameEvent gEvent = GameEvent.CreateCostEvent(CostType.Discard, selectionAmount, selectableUids,
            new List<string> { message }, variableAmount: variableAmount);
        AddEventForPlayer(player, gEvent);

        return true;
    }

    public bool RequestPlayerChoiceCast(Player player, Effect effect) {
        Console.WriteLine($"[PlayerChoiceCast] Requesting selection for {effect.sourceCard?.name}");
        Debug.Assert(effect.targetZones != null, "RequestPlayerChoiceCast requires targetZones");

        // Find matching cards in specified zones based on cardType and tribe (if specified)
        List<int> selectableUids = new();
        Qualifier qualifier = new Qualifier(effect, player);

        foreach (Zone zone in effect.targetZones) {
            List<Card> cardsInZone = zone switch {
                Zone.Hand => player.hand,
                Zone.Graveyard => player.graveyard,
                Zone.Deck => player.deck ?? new List<Card>(),
                _ => new List<Card>()
            };

            foreach (Card c in cardsInZone) {
                // Only allow summons to be cast
                if (c.type != CardType.Summon) continue;
                if (QualifyCard(c, qualifier)) {
                    selectableUids.Add(c.uid);
                    Console.WriteLine($"[PlayerChoiceCast] Card {c.name} (uid={c.uid}) from {zone} qualifies");
                }
            }
        }

        if (selectableUids.Count == 0) {
            Console.WriteLine("[PlayerChoiceCast] No matching cards in target zones");
            return false;
        }

        // Store the effect for later processing
        playerChoiceCastEffect = effect;

        // Build message
        string tribeName = effect.tribe?.ToString().ToLower() ?? "";
        string descriptor = string.IsNullOrEmpty(tribeName) ? "summons" : $"{tribeName} summons";
        string zoneNames = string.Join(" or ", effect.targetZones.Select(z => z.ToString().ToLower()));
        string message = $"Choose {descriptor} to cast from your {zoneNames} (0 to {selectableUids.Count})";

        Console.WriteLine($"[PlayerChoiceCast] Selectable UIDs: [{string.Join(", ", selectableUids)}]");

        // Use a generic selection event - CostType doesn't really matter here since we're just selecting
        GameEvent gEvent = GameEvent.CreateCostEvent(CostType.Sacrifice, selectableUids.Count, selectableUids,
            new List<string> { message }, variableAmount: true);
        AddEventForPlayer(player, gEvent);

        Console.WriteLine($"[PlayerChoiceCast] Sent selection event with {selectableUids.Count} options");
        return true;
    }

    // Stored effect for fixed cast from zone selection (e.g., Goblin Ritualist)
    private Effect? fixedCastFromZoneEffect;

    /// <summary>
    /// Requests player to select a fixed number of cards to cast from specified zones.
    /// Unlike RequestPlayerChoiceCast, this handles spells and uses a fixed amount from select.
    /// </summary>
    public bool RequestFixedCastFromZone(Player player, Effect effect) {
        Console.WriteLine($"[FixedCastFromZone] Requesting selection for {effect.sourceCard?.name}");
        Debug.Assert(effect.targetZones != null, "RequestFixedCastFromZone requires targetZones");
        Debug.Assert(effect.select != null, "RequestFixedCastFromZone requires select object");

        // Find matching cards in specified zones
        List<int> selectableUids = new();
        Qualifier qualifier = new Qualifier(effect, player);

        foreach (Zone zone in effect.targetZones) {
            List<Card> cardsInZone = zone switch {
                Zone.Hand => player.hand,
                Zone.Graveyard => player.graveyard,
                Zone.Deck => player.deck ?? new List<Card>(),
                _ => new List<Card>()
            };

            foreach (Card c in cardsInZone) {
                // Apply cardType filter (spell, summon, etc.)
                if (effect.cardType != null && c.type != effect.cardType) continue;
                // Apply tribe filter
                if (effect.tribe != null && c.tribe != effect.tribe) continue;
                // Apply cost restrictions
                if (effect.restrictions != null && effect.restrictions.Contains(Restriction.Cost)) {
                    if (effect.restrictionMax != null && c.cost > effect.restrictionMax) continue;
                    if (effect.restrictionMin != null && c.cost < effect.restrictionMin) continue;
                }
                selectableUids.Add(c.uid);
                Console.WriteLine($"[FixedCastFromZone] Card {c.name} (uid={c.uid}, cost={c.cost}) from {zone} qualifies");
            }
        }

        if (selectableUids.Count == 0) {
            Console.WriteLine("[FixedCastFromZone] No matching cards in target zones");
            return false;
        }

        // Store the effect for later processing
        fixedCastFromZoneEffect = effect;

        // Get selection amount from select object
        int selectAmount = effect.select.GetMin();  // Min == Max for fixed selection
        string typeName = effect.cardType?.ToString().ToLower() ?? "card";
        string zoneNames = string.Join(" or ", effect.targetZones.Select(z => z.ToString().ToLower()));

        // Build restriction description
        string restrictionDesc = "";
        if (effect.restrictions != null && effect.restrictions.Contains(Restriction.Cost)) {
            if (effect.restrictionMin != null && effect.restrictionMax != null) {
                restrictionDesc = $" with LP cost {effect.restrictionMin} to {effect.restrictionMax}";
            } else if (effect.restrictionMax != null) {
                restrictionDesc = $" with LP cost {effect.restrictionMax} or less";
            }
        }

        string message = $"Choose a {typeName}{restrictionDesc} from your {zoneNames} to cast";

        Console.WriteLine($"[FixedCastFromZone] Selectable UIDs: [{string.Join(", ", selectableUids)}]");

        // Request selection (selectAmount, not variable)
        GameEvent gEvent = GameEvent.CreateCostEvent(CostType.Sacrifice, selectAmount, selectableUids,
            new List<string> { message }, variableAmount: false);
        AddEventForPlayer(player, gEvent);

        Console.WriteLine($"[FixedCastFromZone] Sent selection event for {selectAmount} card(s)");
        return true;
    }

    /// <summary>
    /// Handles player's selection for fixed cast from zone (e.g., Goblin Ritualist casting spell from graveyard).
    /// </summary>
    public void HandleFixedCastFromZoneSelection(Player player, List<int> selectedUids) {
        Console.WriteLine($"[FixedCastFromZone] Player selected: [{string.Join(", ", selectedUids)}]");

        if (fixedCastFromZoneEffect == null) {
            Console.WriteLine("[FixedCastFromZone] ERROR: No fixedCastFromZoneEffect active");
            return;
        }

        // Cast each selected card
        foreach (int uid in selectedUids) {
            if (!cardByUid.TryGetValue(uid, out Card? card)) continue;
            Console.WriteLine($"[FixedCastFromZone] Casting {card.name} (type={card.type})");

            // Actually cast the card (put on stack, resolve)
            AttemptToCast(player, card, CastingStage.Initial, freeCast: true);
        }

        // Clear stored effect
        fixedCastFromZoneEffect = null;

        // Resume the stack resolution
        if (unresolvedStackObj != null) {
            unresolvedEffectIndex++;  // Move past the CastCard effect
            unresolvedStackObj.ResumeResolve(this);
        }
    }

    /// <summary>
    /// Handles a player's response to an "each player chooses" effect.
    /// </summary>
    public void HandleEachPlayerSelection(Player player, List<int> selectedUids) {
        Console.WriteLine($"[EachPlayer] Player {player.playerName} selected: [{string.Join(", ", selectedUids)}]");

        if (eachPlayerEffect == null) {
            Console.WriteLine("[EachPlayer] ERROR: No eachPlayerEffect active");
            return;
        }

        // Store this player's selection
        eachPlayerSelections[player.playerId] = selectedUids;
        pendingEachPlayerResponses.Remove(player.playerId);

        Console.WriteLine($"[EachPlayer] Remaining responses needed: {pendingEachPlayerResponses.Count}");

        // If still waiting for more responses, don't continue
        if (pendingEachPlayerResponses.Count > 0) {
            return;
        }

        // All responses received - apply the effect
        Console.WriteLine("[EachPlayer] All responses received, applying effect");

        if (eachPlayerEffect.effect == EffectType.SendToZone) {
            Debug.Assert(eachPlayerEffect.destination != null, "No destination for SendToZone");
            Zone destination = (Zone)eachPlayerEffect.destination;

            foreach (var kvp in eachPlayerSelections) {
                Player affectedPlayer = accountIdToPlayer[kvp.Key];
                foreach (int uid in kvp.Value) {
                    Card card = cardByUid[uid];
                    Console.WriteLine($"[EachPlayer] Sending {card.name} to {destination}");
                    SendToZone(affectedPlayer, destination, card);
                }
            }
        } else if (eachPlayerEffect.effect == EffectType.Sacrifice) {
            foreach (var kvp in eachPlayerSelections) {
                foreach (int uid in kvp.Value) {
                    if (cardByUid.TryGetValue(uid, out Card? card)) {
                        Console.WriteLine($"[EachPlayer] Sacrificing {card.name}");
                        Destroy(card);
                    }
                }
            }
        }

        // Clean up and resume
        eachPlayerEffect = null;
        eachPlayerSelections.Clear();

        // Resume stack resolution
        Debug.Assert(unresolvedStackObj != null, "No unresolved stack object after eachPlayer effect");
        unresolvedStackObj.ResumeResolve(this);
    }

    public void HandleCostSelection(Player player, List<Card> selectedCards) {
        // hand size discard at end of turn
        if (waitingForHandSizeDiscard) {
            PayCost(player, CostType.Discard, selectedCards);
            // Check if Ghost Deceiver triggered during discards - halt if so
            if (ghostDeceiverStage > 0) {
                Console.WriteLine($"[HandleCostSelection] Ghost Deceiver triggered during hand size discard, halting");
                ghostDeceiverWasHandSizeDiscard = true;
                waitingForHandSizeDiscard = false;
                return;
            }
            waitingForHandSizeDiscard = false;
            // Now continue with passing the turn
            PassTurn();
            // Send the NextPhase event that was skipped
            triggersToCheck.Add(TriggerContext.CreatePhaseTriggerContext(currentPhase));
            GameEvent gEvent = new GameEvent(EventType.NextPhase);
            AddEventForBothPlayers(GetPlayerByTurn(true), gEvent);
            if (currentPhase == Phase.Draw) {
                ReturnExiledCardsForPlayer(GetPlayerByTurn(true));
                Draw(GetPlayerByTurn(true), 1);
            }
            CheckForTriggersAndPassives(EventType.NextPhase);
            return;
        }

        // cost effect selection at resolve time (for isCost effects that need user selection)
        if (costEffectForSelection != null) {
            Effect effect = costEffectForSelection;
            costEffectForSelection = null;

            // Set the targetUids on the effect so it knows what to sacrifice
            effect.targetUids = selectedCards?.Select(c => c.uid).ToList() ?? new List<int>();


            // Resume stack object resolution - the effect will now be resolved with the selected targets
            Debug.Assert(unresolvedStackObj != null, "No unresolved stack object for cost effect selection");
            unresolvedStackObj.ResumeResolve(this);
            return;
        }

        // playerChoice discard at resolve time (for discard effects with amountBasedOn: playerChoice)
        if (playerChoiceDiscardEffect != null) {
            Effect effect = playerChoiceDiscardEffect;
            playerChoiceDiscardEffect = null;

            // Preserve player UID if present (for "target opponent discards" effects like Reap)
            int? preservedPlayerUid = null;
            if (effect.targetUids.Count > 0 && IsPlayerUid(effect.targetUids[0])) {
                preservedPlayerUid = effect.targetUids[0];
            }

            // Set the targetUids and amount on the effect
            List<int> newTargetUids = new List<int>();
            if (preservedPlayerUid != null) {
                newTargetUids.Add(preservedPlayerUid.Value);
            }
            if (selectedCards != null) {
                newTargetUids.AddRange(selectedCards.Select(c => c.uid));
            }
            effect.targetUids = newTargetUids;
            effect.amount = selectedCards?.Count ?? 0;


            // Resume stack object resolution - the effect will now be resolved with the selected cards
            Debug.Assert(unresolvedStackObj != null, "No unresolved stack object for playerChoice discard");
            unresolvedStackObj.ResumeResolve(this);
            return;
        }

        // playerChoice cast at resolve time (for castCard effects with targetZones)
        if (playerChoiceCastEffect != null) {
            Console.WriteLine($"[PlayerChoiceCast] Processing selection, {selectedCards?.Count ?? 0} cards selected");
            Effect effect = playerChoiceCastEffect;
            playerChoiceCastEffect = null;

            // Set the targetUids on the effect
            effect.targetUids = selectedCards?.Select(c => c.uid).ToList() ?? new List<int>();

            Console.WriteLine($"[PlayerChoiceCast] Set targetUids=[{string.Join(", ", effect.targetUids)}]");

            // Resume stack object resolution - the effect will now be resolved with the selected cards
            Debug.Assert(unresolvedStackObj != null, "No unresolved stack object for playerChoice cast");
            unresolvedStackObj.ResumeResolve(this);
            return;
        }

        // fixed-amount cast from zone at resolve time (e.g., Goblin Ritualist casting spell from graveyard)
        if (fixedCastFromZoneEffect != null) {
            Console.WriteLine($"[FixedCastFromZone] Processing selection, {selectedCards?.Count ?? 0} cards selected");
            List<int> selectedUids = selectedCards?.Select(c => c.uid).ToList() ?? new List<int>();
            HandleFixedCastFromZoneSelection(player, selectedUids);
            return;
        }

        // alternate cost (sacrifice or exile from hand instead of tribute/life)
        if (usingAlternateCost && cardBeingCast != null) {
            // Determine cost type from the alternate cost
            CostType costType = currentAlternateCost?.altCostType == AltCostType.ExileFromHand
                ? CostType.ExileFromHand
                : CostType.Sacrifice;
            PayCost(player, costType, selectedCards);
            currentAlternateCost = null;

            // For spells, continue to target selection (alternate cost was paid before targets)
            if (cardBeingCast.type == CardType.Spell) {
                AttemptToCast(player, cardBeingCast, CastingStage.TargetSelection);
                return;
            }

            // For summons, continue to cast the card (skipping tribute)
            CastCard(player, cardBeingCast);
            usingAlternateCost = false;
            return;
        }

        // activated effect
        if (currentActivatedEffect != null) {
            CostType costToUse;
            if (currentActivatedAbilityAltCost != null) {
                // Use alternate cost type
                costToUse = currentActivatedAbilityAltCost.altCostType switch {
                    AltCostType.Discard => CostType.Discard,
                    AltCostType.Sacrifice => CostType.Sacrifice,
                    AltCostType.Tribute => CostType.Sacrifice,
                    AltCostType.ExileFromHand => CostType.ExileFromHand,
                    _ => currentActivatedEffect.costType
                };
                currentActivatedAbilityAltCost = null;
            } else {
                costToUse = currentActivatedEffect.costType;
            }
            // For variable-amount costs, set the amount based on how many cards were selected
            if (currentActivatedEffect.playerChosenAmount) {
                int selectedCount = selectedCards?.Count ?? 0;
                currentActivatedEffect.SetAmount(selectedCount);
            }
            // Save the controller before paying cost (card might be sacrificed as part of the cost)
            Player controller = player;
            PayCost(player, costToUse, selectedCards);
            AttemptToActivate(controller, currentActivatedEffect, ActivationStage.Choices);
            return;
        }

        // card casts
        if (cardAdditionalCostAmount > 0) {
            Debug.Assert(cardBeingCast != null, "there is no card being cast");
            Debug.Assert(cardBeingCast.additionalCosts != null, "card being cast has no additional costs");
            foreach (AdditionalCost aCost in cardBeingCast.additionalCosts) {
                if(aCost.isPaid) continue;

                // Check if this is an X-determining cost (variable selection that sets X)
                bool isXDeterminingCost = aCost.amountBasedOn == AmountBasedOn.X &&
                                          cardBeingCast.x == null &&
                                          (aCost.costType == CostType.Sacrifice || aCost.costType == CostType.Discard);

                if (isXDeterminingCost) {
                    // Set X based on how many cards were selected (can be 0)
                    int selectedCount = selectedCards?.Count ?? 0;
                    Console.WriteLine($"[HandleCostSelection] X-determining cost: setting X to {selectedCount}");
                    cardBeingCast.x = selectedCount;

                    // Pay the cost (sacrifice the selected cards)
                    if (selectedCards != null && selectedCards.Count > 0) {
                        PayCost(player, aCost.costType, selectedCards);
                    }
                    aCost.isPaid = true;
                    cardAdditionalCostAmount--;

                    // Now that X is set, re-check for remaining additional costs
                    Console.WriteLine($"[HandleCostSelection] Checking for remaining additional costs...");
                    if (CheckCardForAdditionalCosts(player, cardBeingCast)) {
                        Console.WriteLine($"[HandleCostSelection] More costs to pay, returning");
                        return; // More costs to pay
                    }
                    // All costs paid, continue to choices
                    Console.WriteLine($"[HandleCostSelection] All costs paid, calling AttemptToCast with Choices stage");
                    AttemptToCast(player, cardBeingCast, CastingStage.Choices);
                    return;
                }

                // Get the resolved amount (handles X-based costs)
                int requiredAmount = aCost.GetAmount(cardBeingCast);
                // Handle different cost types
                switch (aCost.costType) {
                    case CostType.Life:
                        // Life costs don't require card selection
                        PayLifeCost(player, requiredAmount);
                        break;
                    case CostType.Sacrifice:
                    case CostType.Discard:
                        // Validate that enough cards were selected for the cost
                        if (selectedCards == null || selectedCards.Count < requiredAmount) {
                            Console.WriteLine($"Cost not met: required {requiredAmount}, got {selectedCards?.Count ?? 0}");
                            return;
                        }
                        PayCost(player, aCost.costType, selectedCards);
                        break;
                    default:
                        Console.WriteLine($"Unknown cost type: {aCost.costType}");
                        break;
                }
                aCost.isPaid = true;
                if(cardBeingCast.additionalCosts.Last() == aCost) AttemptToCast(player, cardBeingCast, CastingStage.Choices);
            }
        }

}

    public void PayCost(Player player, CostType costType, List<Card>? selectedCards = null) {
        switch (costType) {
            case CostType.Sacrifice:
                Debug.Assert(selectedCards != null, "there are no cards to sacrifice for this cost (PayCost)");
                foreach (Card c in selectedCards) {
                    Destroy(c);
                }
                break;
            case CostType.Discard:
                Debug.Assert(selectedCards != null, "there are no cards to discard for this cost (PayCost)");
                foreach (Card c in selectedCards) {
                    // Track goblin (red) cards discarded for cost on the card being cast
                    if (cardBeingCast != null && c.tribe == Tribe.Goblin) {
                        cardBeingCast.redCardsDiscardedForCost++;
                    }
                    Discard(player, c);
                    // If Ghost Deceiver triggered, halt the loop - remaining discards will be handled after resolution
                    if (ghostDeceiverStage > 0) {
                        Console.WriteLine($"[PayCost] Ghost Deceiver triggered, halting discard loop");
                        // Store remaining cards to discard after Ghost Deceiver resolves
                        int currentIndex = selectedCards.IndexOf(c);
                        ghostDeceiverRemainingDiscards = selectedCards.Skip(currentIndex + 1).ToList();
                        ghostDeceiverDiscardPlayer = player;
                        break;
                    }
                }
                break;
            case CostType.ExileFromHand:
                Debug.Assert(selectedCards != null, "there are no cards to exile for this cost (PayCost)");
                foreach (Card c in selectedCards) {
                    RemoveFromHand(player, c);
                    AddToExile(player, c);
                    GameEvent gEvent = GameEvent.CreateZoneGameEvent(Zone.Exile, new CardDisplayData(c), Zone.Hand);
                    gEvent.focusCard = new CardDisplayData(c);
                    AddEventForBothPlayers(player, gEvent);
                }
                break;
            case CostType.Exile:
                // Self-exile from current zone (e.g., graveyard for activated abilities)
                Debug.Assert(selectedCards != null, "there are no cards to exile for this cost (PayCost)");
                foreach (Card c in selectedCards) {
                    Zone fromZone = c.currentZone;
                    if (fromZone == Zone.Graveyard) {
                        player.graveyard.Remove(c);
                    } else if (fromZone == Zone.Play) {
                        RemoveFromPlay(player, c);
                    }
                    AddToExile(player, c);
                    GameEvent gEvent = GameEvent.CreateZoneGameEvent(Zone.Exile, new CardDisplayData(c), fromZone);
                    gEvent.focusCard = new CardDisplayData(c);
                    AddEventForBothPlayers(player, gEvent);
                }
                break;
        }
    }
    
    

    public void Reveal(Player affectedPlayer, Card subjectCard) {
        GameEvent gEvent = GameEvent.CreateCardEvent(EventType.Reveal, new CardDisplayData(subjectCard));
        AddEventForBothPlayers(affectedPlayer, gEvent);
    }

    public Player PlayerByUid(int uid) {
        return playerOne.uid == uid ? playerOne : playerTwo;
    }

    public bool IsPlayerUid(int uid) {
        return playerOne.uid == uid || playerTwo.uid == uid;
    }

    public List<Card> GetAllSummonsInPlay() {
        List<Card> cards = playerOne.playField.Where(c => c.type == CardType.Summon).ToList();
        cards.AddRange(playerTwo.playField.Where(c => c.type == CardType.Summon));
        return cards;
    }

    // ==================== Ritual of Darkness ====================

    /// <summary>
    /// Starts the Ritual of Darkness effect. Caster gets first opportunity to put a summon into play.
    /// </summary>
    public void StartRitualOfDarkness(Player caster) {
        Console.WriteLine($"[RitualOfDarkness] Starting ritual, caster: {caster.playerName}");
        inRitualOfDarkness = true;
        ritualCaster = caster;
        ritualLastPlayerPassed = false;
        PromptRitualChoice(caster);
    }

    /// <summary>
    /// Prompts a player to choose a summon from hand or pass.
    /// </summary>
    private void PromptRitualChoice(Player player) {
        ritualCurrentPlayer = player;

        // Get summons in hand
        List<int> selectableSummonUids = player.hand
            .Where(c => c.type == CardType.Summon)
            .Select(c => c.uid)
            .ToList();

        Console.WriteLine($"[RitualOfDarkness] Prompting {player.playerName}, has {selectableSummonUids.Count} summons in hand");

        // If no summons in hand, auto-pass
        if (selectableSummonUids.Count == 0) {
            Console.WriteLine($"[RitualOfDarkness] {player.playerName} has no summons, auto-passing");
            HandleRitualOfDarknessChoice(player, -1);  // -1 indicates pass
            return;
        }

        // Send event to player to choose
        GameEvent gEvent = GameEvent.CreateRitualOfDarknessChoiceEvent(selectableSummonUids);
        AddEventForPlayer(player, gEvent);
    }

    /// <summary>
    /// Handles a player's choice during Ritual of Darkness.
    /// summonUid = -1 means the player passed.
    /// </summary>
    public void HandleRitualOfDarknessChoice(Player player, int summonUid) {
        Debug.Assert(inRitualOfDarkness, "HandleRitualOfDarknessChoice called but not in ritual state");
        Debug.Assert(ritualCurrentPlayer == player, "HandleRitualOfDarknessChoice called by wrong player");

        if (summonUid == -1) {
            // Player passed
            Console.WriteLine($"[RitualOfDarkness] {player.playerName} passed");

            if (ritualLastPlayerPassed) {
                // Both players passed consecutively - end the ritual
                Console.WriteLine($"[RitualOfDarkness] Both players passed, ending ritual");
                EndRitualOfDarkness();
            } else {
                // First pass - mark it and switch to other player
                ritualLastPlayerPassed = true;
                Player nextPlayer = GetOpponent(player);
                PromptRitualChoice(nextPlayer);
            }
        } else {
            // Player chose a summon
            Console.WriteLine($"[RitualOfDarkness] {player.playerName} chose summon uid {summonUid}");

            // Get the card and put it into play
            Card summon = cardByUid[summonUid];
            Debug.Assert(summon.currentZone == Zone.Hand, "Selected card is not in hand");
            Debug.Assert(summon.type == CardType.Summon, "Selected card is not a summon");

            // Remove from hand and put into play (with summoning sickness, bypasses tribute/cost)
            // Use SendToZone pattern instead of Summon to avoid triggering abilities immediately
            // Triggers are deferred until EndRitualOfDarkness -> ResumeResolve -> FinalizeResolve
            RemoveFromHand(player, summon);

            // Apply player passives before entering play (same as Summon does)
            ApplyPlayerPassivesToSummon(player, summon);

            // AddToPlay adds EnteredZone trigger to triggersToCheck (deferred until ritual ends)
            AddToPlay(player, summon);
            player.totalSummons++;

            // Send SendToZone event (Hand -> Play) instead of Summon event
            GameEvent gEvent = GameEvent.CreateZoneGameEvent(Zone.Play, new CardDisplayData(summon), Zone.Hand);
            AddEventForBothPlayers(player, gEvent);

            // Check passives and deaths (these don't process triggers)
            CheckForPassives();
            CheckForDeaths();

            Console.WriteLine($"[RitualOfDarkness] {summon.name} put into play for {player.playerName}");

            // Reset pass tracker and switch to other player
            ritualLastPlayerPassed = false;
            Player nextPlayer = GetOpponent(player);
            PromptRitualChoice(nextPlayer);
        }
    }

    /// <summary>
    /// Ends the Ritual of Darkness and resumes normal game flow.
    /// </summary>
    private void EndRitualOfDarkness() {
        Console.WriteLine($"[RitualOfDarkness] Ritual ended");
        inRitualOfDarkness = false;
        ritualCurrentPlayer = null;
        ritualCaster = null;
        ritualLastPlayerPassed = false;

        // Resume stack resolution - this will continue to next effect or FinalizeResolve
        // which will call CheckForTriggersAndPassives for all the enter-play triggers
        if (unresolvedStackObj != null) {
            unresolvedStackObj.ResumeResolve(this);
        }
    }

    // ==================== Repeat Effect ====================

    /// <summary>
    /// Starts the repeat choice flow after an effect with repeatCostType resolves.
    /// </summary>
    public void StartRepeatChoice(Effect effect, Player player, List<int> targetUids) {
        Debug.Assert(effect.repeatCostType != null, "StartRepeatChoice called on effect without repeatCostType");
        Debug.Assert(effect.repeatCostAmount != null, "StartRepeatChoice called on effect without repeatCostAmount");

        int costAmount = effect.repeatCostAmount.Value;

        // Check if player can afford the repeat cost
        if (effect.repeatCostType == CostType.LoseLife && player.lifeTotal <= costAmount) {
            Console.WriteLine($"[Repeat] {player.playerName} cannot afford repeat cost ({costAmount} LP)");
            EndRepeatChoice(false);
            return;
        }

        // Check if target still exists (for repeatSameTarget)
        if (effect.repeatSameTarget && targetUids.Count > 0) {
            int targetUid = targetUids[0];
            if (!cardByUid.ContainsKey(targetUid) || cardByUid[targetUid].currentZone != Zone.Play) {
                Console.WriteLine($"[Repeat] Target no longer valid, cannot repeat");
                EndRepeatChoice(false);
                return;
            }
        }

        Console.WriteLine($"[Repeat] Prompting {player.playerName} to repeat for {costAmount} LP");
        inRepeatChoice = true;
        repeatEffect = effect;
        repeatPlayer = player;
        repeatTargetUids = targetUids.ToList();

        // Send event to player asking if they want to repeat
        int targetUidForEvent = targetUids.Count > 0 ? targetUids[0] : -1;
        GameEvent gEvent = GameEvent.CreateRepeatChoiceEvent(costAmount, effect.repeatCostType.ToString()!, targetUidForEvent);
        AddEventForPlayer(player, gEvent);
    }

    /// <summary>
    /// Handles player's choice to repeat or decline.
    /// </summary>
    public void HandleRepeatChoice(Player player, bool accepted) {
        Debug.Assert(inRepeatChoice, "HandleRepeatChoice called but not in repeat choice state");
        Debug.Assert(repeatPlayer == player, "HandleRepeatChoice called by wrong player");

        if (!accepted) {
            Console.WriteLine($"[Repeat] {player.playerName} declined to repeat");
            EndRepeatChoice(false);
            return;
        }

        Console.WriteLine($"[Repeat] {player.playerName} chose to repeat");

        // Pay the cost
        int costAmount = repeatEffect!.repeatCostAmount!.Value;
        if (repeatEffect.repeatCostType == CostType.LoseLife) {
            Console.WriteLine($"[Repeat] {player.playerName} pays {costAmount} LP");
            LoseLife(player, costAmount);
        }

        // Clone the effect and execute it again with same targets
        Effect clonedEffect = repeatEffect.Clone();
        if (repeatEffect.repeatSameTarget && repeatTargetUids != null && repeatTargetUids.Count > 0) {
            clonedEffect.targetUids = repeatTargetUids.ToList();
        }

        // Execute the effect
        Console.WriteLine($"[Repeat] Executing repeated effect");
        clonedEffect.Resolve(this, player, null);

        // Check if we can repeat again
        StartRepeatChoice(repeatEffect, player, repeatTargetUids ?? new List<int>());
    }

    /// <summary>
    /// Ends the repeat choice flow.
    /// </summary>
    private void EndRepeatChoice(bool wasRepeated) {
        Console.WriteLine($"[Repeat] Repeat choice ended");
        inRepeatChoice = false;
        repeatEffect = null;
        repeatPlayer = null;
        repeatTargetUids = null;

        // Resume stack resolution
        if (unresolvedStackObj != null) {
            unresolvedStackObj.ResumeResolve(this);
        }
    }
}
