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

    public List<Phase> phasesToPauseOn = new();
    public bool secondPass;
    private bool waitingForHandSizeDiscard;

    // look at deck effect (temporary stored data while client chooses a card)
    public StackObj? unresolvedStackObj;
    public int unresolvedEffectIndex;

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
    private Effect? currentOptionalEffect;
    
    // additional costs
    private List<TriggeredEffect> triggersWithCosts = new();
    private int cardAdditionalCostAmount;

    // effect choices
    private Dictionary<List<Effect>, Effect> choiceEffects = new();
    private Dictionary<List<Effect>, Effect> additionalChoiceEffects = new();
    private Card? choiceCard;
    private List<int>? currentValidChoiceIndices;
    // multi-choice tracking (for "Choose Two" style effects)
    private int remainingChoices;
    private List<int> selectedChoiceIndices = new();
    private bool currentForOpponentChoice; // tracks if current choice is presented to opponent

    // tributing
    private Card? cardRequiringTribute;

    // alternate costs
    private AlternateCost? currentAlternateCost;
    private bool usingAlternateCost;

    // discard or sacrifice choice (for Eadro-style costs)
    private bool pendingDiscardOrSacrificeChoice;
    private CostType? resolvedChoiceCostType;  // tracks the actual cost type after player chooses

    // phase skipping
    private Phase? skipStartPhase;  // tracks where consecutive skipping started from
    private bool isAutoSkipping;    // true when we're in the middle of auto-skipping phases
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
        if (card.keywords == null) return false;
        return card.keywords.Any(cardKeyword => keyword == cardKeyword);
    }

    public void CheckForTriggersAndPassives(EventType eventType, Player? playerToPassTo = null) {
        Console.WriteLine($"[CheckForTriggersAndPassives] eventType={eventType}, triggersToCheck.Count={triggersToCheck.Count}");
        Player turnPlayer = GetPlayerByTurn(true);
        Player nonTurnPlayer = GetPlayerByTurn(false);
        foreach (TriggerContext tc in triggersToCheck) {
            Console.WriteLine($"[CheckForTriggersAndPassives] Checking trigger: {tc.trigger}, card={tc.card?.name ?? "null"}");
            currentTriggerContext = tc;
            // first check for the players whose turn it is
            CheckForTriggersPlayer(tc, turnPlayer);
            CheckForTriggersPlayer(tc, nonTurnPlayer);
        }

        Console.WriteLine($"[CheckForTriggersAndPassives] After checking: turnPlayer.controlledTriggers={turnPlayer.controlledTriggers.Count}, nonTurnPlayer.controlledTriggers={nonTurnPlayer.controlledTriggers.Count}");
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
        Console.WriteLine($"[CheckForTriggersAndPassives] Calling HandleTriggers for {turnPlayer.playerName}, passing to {playerToPassTo.playerName}");
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

        // refresh all passives in all cards in non-deck zones
        RefreshPassives();
    }

    private void CheckForPassivesInHand(Player player) {
        foreach (Card c in player.hand) {
            if (c.passiveEffects == null || c.passiveEffects.Count == 0) continue;
            foreach (PassiveEffect pEffect in c.passiveEffects.Where(p => handPassiveTypes.Contains(p.passive))) {
                ApplyPassive(c, pEffect, true);
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
        Console.WriteLine($"[DEBUG] ApplyPassiveToTokens: source={sourceCard.name}, passive={pEffect.passive}, tokenType={pEffect.tokenType}, tribe={pEffect.tribe}");
        Console.WriteLine($"[DEBUG] Player {player.playerName} has {player.tokens.Count} tokens");
        foreach (Token token in player.tokens) {
            Console.WriteLine($"[DEBUG]   Checking token uid={token.uid}, name={token.name}, tribe={token.tribe}");
            if (!QualifyCard(token, pQualifier)) {
                Console.WriteLine($"[DEBUG]   Token uid={token.uid} did NOT qualify");
                continue;
            }
            if (HasPassiveFromSource(token, sourceCard, pEffect.passive)) {
                Console.WriteLine($"[DEBUG]   Token uid={token.uid} already has passive from source");
                continue;
            }
            Console.WriteLine($"[DEBUG]   Applying passive to token uid={token.uid}");
            ApplyClonedPassive(token, sourceCard, pEffect);
        }
    }

    private void ApplyPassiveToHandCards(Player player, Qualifier pQualifier, PassiveEffect pEffect, Card sourceCard) {
        if (!handPassiveTypes.Contains(pEffect.passive)) return;
        foreach (Card c in player.hand) {
            if (!QualifyCard(c, pQualifier)) continue;
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
                Console.WriteLine($"[QualifyCard] FAILED conditions check for {c.name}");
                return false;
            }
        }
        // check if it already has the passive you are qualifying for (no need to grant or apply it if so)
        if (q.passive != null) {
            if (c.grantedPassives.Contains(q.passive)) {
                Console.WriteLine($"[QualifyCard] FAILED passive already exists for {c.name}");
                return false;
            }
        }
        // Apply scope filtering
        if (q.sourceCard != null) {
            bool isSameCard = c.Equals(q.sourceCard);
            switch (q.scope) {
                case Scope.SelfOnly:
                    if (!isSameCard) {
                        Console.WriteLine($"[QualifyCard] FAILED scope SelfOnly for {c.name}");
                        return false;
                    }
                    break;
                case Scope.OthersOnly:
                    if (isSameCard) {
                        Console.WriteLine($"[QualifyCard] FAILED scope OthersOnly for {c.name}");
                        return false;
                    }
                    break;
                case Scope.All:
                    // No filtering needed
                    break;
            }
        }
        // tribe check
        if (q.tribe != null && c.tribe != q.tribe) {
            Console.WriteLine($"[QualifyCard] FAILED tribe check for {c.name}: expected {q.tribe}, got {c.tribe}");
            return false;
        }
        // cardtype check
        if (q.cardType != null && c.type != q.cardType) {
            Console.WriteLine($"[QualifyCard] FAILED cardType check for {c.name}: expected {q.cardType}, got {c.type}");
            return false;
        }
        // verify restrictions
        if (q.restrictions != null) {
            foreach (var restriction in q.restrictions) {
                if (!QualifyRestriction(c, restriction, q.sourcePlayer)) {
                    Console.WriteLine($"[QualifyCard] FAILED restriction {restriction} for {c.name}");
                    return false;
                }
            }
        }

        // tokentype check
        if (q.tokenType != null) {
            if (c is not Token t) {
                Console.WriteLine($"[QualifyCard] FAILED tokenType check for {c.name}: card is not a Token");
                return false;
            }
            if (q.tokenType != t.tokenType) {
                Console.WriteLine($"[QualifyCard] FAILED tokenType check for {c.name}: expected {q.tokenType}, got {t.tokenType}");
                return false;
            }
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
        Debug.Assert(effect.targetType != null, "QualifyTarget called with null targetType");
        TargetType targetType = (TargetType)effect.targetType;
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
                List<Token> tempTokenList = playerOne.tokens.Concat(playerTwo.tokens).ToList();
                if (!tempTokenList.Contains(cardByUid[uid])) return false;
                // Check for CantBeTargeted passive
                if (cardByUid[uid].GetPassives().Any(p => p.passive == Passive.CantBeTargeted)) return false;
                return true;
            case TargetType.Summon:
                if (targetIsPlayer) return false;
                if (!GetAllSummonsInPlay().Contains(cardByUid[uid])) return false;
                Card summonCard = cardByUid[uid];
                // Check for CantBeTargeted passive
                if (summonCard.GetPassives().Any(p => p.passive == Passive.CantBeTargeted)) return false;
                // Apply isOpponent filter - only target opponent's summons
                if (effect.isOpponent && GetControllerOf(summonCard) == castingPlayer) return false;
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
                        if (r == Restriction.NonToken && summonCard is Token) return false;
                        if (r == Restriction.NonMerfolk && summonCard.tribe == Tribe.Merfolk) return false;
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
        switch (effect.targetType) {
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
        switch (tc.trigger) {
            case Trigger.Death:
                Debug.Assert(tc.card != null, "there is no dead card associated with this Death trigger");
                AddToControlledTriggers(player, GetTriggers(tc, player));
                if (player == tc.card.lastControllingPlayer)
                    AddToControlledTriggers(player, GetTriggersInCard(tc, player, tc.card));
                break;
            case Trigger.LeftZone:
                Debug.Assert(tc.card != null, "there is no card associated with this LeftZone trigger");
                AddToControlledTriggers(player, GetTriggers(tc, player));
                if (player == tc.card.lastControllingPlayer)
                    AddToControlledTriggers(player, GetTriggersInCard(tc, player, tc.card));
                break;
            case Trigger.Mill:
                Debug.Assert(tc.card != null, "there is no card associated with this Mill trigger");
                AddToControlledTriggers(player, GetTriggers(tc, player));
                // Only check the milled card directly if it's still in graveyard
                // (it might have been brought back to play by another effect like Ghost Gathering)
                if (player == GetOwnerOf(tc.card) && tc.card.currentZone != Zone.Play)
                    AddToControlledTriggers(player, GetTriggersInCard(tc, player, tc.card));
                break;
            case Trigger.EnteredZone:
                Debug.Assert(tc.card != null, "there is no card associated with this EnteredZone trigger");
                AddToControlledTriggers(player, GetTriggers(tc, player));
                if(tc.zone != Zone.Play && player == GetOwnerOf(tc.card)) {
                    AddToControlledTriggers(player, GetTriggersInCard(tc, player, tc.card));
                }
                break;
            default:
                AddToControlledTriggers(player, GetTriggers(tc, player));
                break;
        }
    }

    private void AddToControlledTriggers(Player player, List<TriggeredEffect> triggers) {
        foreach (TriggeredEffect tEffect in triggers) {
            player.controlledTriggers.Add(tEffect);
        }
    }

    private List<TriggeredEffect> GetTriggers(TriggerContext tc, Player player) {
        List<TriggeredEffect> newTEffectList = new();
        foreach (Card c in player.playField) {
            List<TriggeredEffect> tempTriggeredEffects = GetTriggersInCard(tc, player, c);
            foreach (TriggeredEffect tEffect in tempTriggeredEffects) {
                newTEffectList.Add(tEffect);
            }
        }

        foreach (Card c in player.hand) {
            List<TriggeredEffect> tempTriggeredEffects = GetTriggersInCard(tc, player, c);
            foreach (TriggeredEffect tEffect in tempTriggeredEffects) {
                newTEffectList.Add(tEffect);
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
        return true;
    }
    
    private List<TriggeredEffect> GetTriggersInCard(TriggerContext tc, Player player, Card c) {
        List<TriggeredEffect> newTEffectList = new();
        Debug.Assert(c != null, "There is no card associated with this trigger");
        if (c.triggeredEffects == null) {
            return newTEffectList;
        }
        foreach (TriggeredEffect tEffect in c.triggeredEffects) {
            // set the source cards for all effects and sub-effects
            Qualifier tQualifier = new Qualifier(tEffect, player);
            if(!QualifyTrigger(tc, player, tEffect, c)) continue;
            // Zone check: default to play unless an InZone condition specifies otherwise
            // Exception: For LeftZone/Death/Mill triggers on the card itself, skip zone check since
            // the card has already moved out of play/deck when we check for triggers
            // Exception: OpeningHand triggers work from hand
            bool hasInZoneCondition = tEffect.conditions?.Any(cond => cond.condition == ConditionType.InZone) ?? false;
            bool isSelfLeavingTrigger = (tc.trigger == Trigger.LeftZone || tc.trigger == Trigger.Death || tc.trigger == Trigger.Mill)
                                        && tc.card == c && tEffect.scope == Scope.SelfOnly;
            bool isOpeningHandTrigger = tEffect.trigger == Trigger.OpeningHand && c.currentZone == Zone.Hand;
            if (!hasInZoneCondition && !isSelfLeavingTrigger && !isOpeningHandTrigger && c.currentZone != Zone.Play) {
                continue;
            }
            // For Draw triggers, tc.card is just informational (the card that was drawn)
            // We don't need to qualify it - the trigger fires for any draw
            if (tc.card != null && tc.trigger != Trigger.Draw) {
                // First, check if tc.card qualifies against the trigger's qualifiers
                if (!QualifyCard(tc.card, tQualifier)) {
                    Console.WriteLine($"[GetTriggersInCard] QualifyCard failed: tc.card={tc.card.name}, sourceCard={c.name}, tokenType qualifier={tQualifier.tokenType}");
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

            // Add tEffect to list.
            Console.WriteLine($"[GetTriggersInCard] Trigger MATCHED: sourceCard={c.name}, trigger={tEffect.trigger}, zone={tEffect.zone}");
            newTEffectList.Add(tEffect);
        }

        return newTEffectList;
    }

    private bool CheckTriggerConditions(Player player, Trigger triggerType, TriggeredEffect tEffect, Zone? zone, Card sourceCard) {
        if (tEffect.trigger != triggerType) {
            return false;
        }
        if (zone != null && tEffect.zone != zone) {
            Console.WriteLine($"[CheckTriggerConditions] Zone mismatch: tc.zone={zone}, tEffect.zone={tEffect.zone} for {sourceCard.name}");
            return false;
        }
        if (tEffect.conditions != null) {
            foreach (Condition condition in tEffect.conditions) {
                if (!condition.Verify(this, player, null, sourceCard)) return false;
            }
        }
        return true;
    }

    public List<Card> GetQualifiedCards(List<Card> cardsToQualify, Qualifier qualifier) {
        List<Card> tempCards = cardsToQualify.ToList();
        foreach (Card c in tempCards) {
            if (!QualifyCard(c, qualifier)) cardsToQualify.Remove(c);
        }

        return cardsToQualify;
    }

    public void MakeChoice(Player player, int currentChoiceIndex) {
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
        // discard or sacrifice choice (for Eadro-style costs)
        if (pendingDiscardOrSacrificeChoice) {
            pendingDiscardOrSacrificeChoice = false;
            Debug.Assert(currentActivatedEffect != null, "No activated effect for discard/sacrifice choice");
            List<int> selectableUids = new();
            List<string> messageList;
            CostType costType;

            if (currentChoiceIndex == 0) {
                // Player chose to discard
                costType = CostType.Discard;
                messageList = new List<string> { "discard a merfolk" };
                foreach (Card c in player.hand.Where(c => c.tribe == Tribe.Merfolk)) {
                    selectableUids.Add(c.uid);
                }
            } else {
                // Player chose to sacrifice (can include Eadro itself)
                costType = CostType.Sacrifice;
                messageList = new List<string> { "sacrifice a merfolk" };
                foreach (Card c in GetAllCardsControlled(player).Where(c => c.tribe == Tribe.Merfolk)) {
                    selectableUids.Add(c.uid);
                }
            }

            resolvedChoiceCostType = costType;  // Track the actual cost type for HandleCostSelection
            GameEvent costEvent = GameEvent.CreateCostEvent(costType, 1, selectableUids, messageList);
            AddEventForPlayer(player, costEvent);
            return;
        }
        // optional triggers
        if (optionalTriggers.Count > 0) {
            if (currentChoiceIndex == 1) {
                player.controlledTriggers.Remove(optionalTriggers.First());
                optionalTriggers.Remove(optionalTriggers.First());
            } else {
                optionalTriggers.Remove(optionalTriggers.First());
            }

            Debug.Assert(currentPlayerToPassTo != null, "there is no currentPlayerToPassTo");
            if (optionalTriggers.Count == 0) {
                HandleTriggers(player, currentPlayerToPassTo, TriggerStage.AdditionalCosts);
            } else {
                // Send option event for the next optional trigger
                HandleOptionalEffect(player, optionalTriggers.First());
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
                currentOptionalEffect.Resolve(this, player);
                unresolvedStackObj.ResumeResolve(this);
                currentOptionalEffect = null;
            } else {
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

        // If more choices remain, prompt for next choice
        if (remainingChoices > 0) {
            HandleChoice(pair.Value.choices, player, currentForOpponentChoice);
            return;
        }

        // All choices made - now insert all selected effects
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
            AttemptToCast(player, choiceCard, castingStage);
        } else {
            HandleTriggers(player, currentPlayerToPassTo, TriggerStage.TargetSelection);
        }
    }

    private void ApplyAdditionalCosts(TriggeredEffect tEffect) {
        Debug.Assert(tEffect.additionalCosts != null);
        foreach (AdditionalCost aCost in tEffect.additionalCosts) {
            switch (aCost.costType) {
                case CostType.Reveal:
                    if (aCost.amount == null && aCost.tokenType == null) {
                        tEffect.sourceCard.Reveal();
                    }
                    break;
                default:
                    Console.WriteLine("Cost type of additional cost of type: " + aCost.costType + " for card: " +
                                      tEffect.sourceCard.name + " is not implemented");
                    break;
            }
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
        Console.WriteLine($"[HandleTriggers] player={player.playerName}, stage={stage}, controlledTriggers.Count={player.controlledTriggers.Count}");
        currentPlayerToPassTo = playerToPassTo;
        switch (stage) {
            case TriggerStage.Initial:
                if (player.controlledTriggers.Count <= 0) {
                    Console.WriteLine($"[HandleTriggers] No triggers, calling FinishWithTriggers");
                    FinishWithTriggers(player, playerToPassTo);
                    return;
                }
                if (CheckForOptionalTriggers(player)) {
                    Console.WriteLine($"[HandleTriggers] Waiting for optional trigger response");
                    return;
                }
                goto case TriggerStage.AdditionalCosts;
            case TriggerStage.AdditionalCosts:
                if (CheckForAdditionalCosts(player)) return;
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
        // Bots auto-decline all optional triggers
        if (player.isBot) {
            player.controlledTriggers.RemoveAll(t => t.optional);
            return false;
        }

        foreach (TriggeredEffect tEffect in player.controlledTriggers) {
            if (tEffect.optional) {
                optionalTriggers.Add(tEffect);
            }
        }

        // Only send one option event at a time
        if (optionalTriggers.Count > 0) {
            HandleOptionalEffect(player, optionalTriggers.First());
        }

        return optionalTriggers.Count > 0;
    }

    private bool CheckForAdditionalCosts(Player player) {
        // Collect all triggers with costs, but only send cost event for the first one
        foreach (TriggeredEffect tEffect in player.controlledTriggers) {
            if(tEffect.additionalCosts == null) continue;
            if(!triggersWithCosts.Contains(tEffect)) triggersWithCosts.Add(tEffect);
        }

        // Only send cost event for the first trigger's first unpaid cost
        if (triggersWithCosts.Count > 0) {
            SendNextTriggerCostEvent(player);
        }
        return triggersWithCosts.Count > 0;
    }

    private void SendNextTriggerCostEvent(Player player) {
        if (triggersWithCosts.Count == 0) return;
        TriggeredEffect focusTrigger = triggersWithCosts.First();
        foreach (AdditionalCost aCost in focusTrigger.additionalCosts!) {
            if (aCost.isPaid) continue;

            // Reveal costs are auto-paid (no player selection needed)
            if (aCost.costType == CostType.Reveal) {
                if (aCost.amount == null && aCost.tokenType == null) {
                    // Just reveal the source card
                    focusTrigger.sourceCard.Reveal();
                    GameEvent revealEvent = new GameEvent(EventType.Reveal);
                    revealEvent.focusCard = new CardDisplayData(focusTrigger.sourceCard);
                    AddEventForPlayer(player, revealEvent);
                    AddEventForPlayer(GetOpponent(player), revealEvent);
                }
                aCost.isPaid = true;
                continue; // Check for next cost
            }

            // Sacrifice self costs are auto-paid (sacrifice the source card)
            if (aCost.costType == CostType.Sacrifice && aCost.scope == Scope.SelfOnly) {
                Destroy(focusTrigger.sourceCard);
                aCost.isPaid = true;
                continue; // Check for next cost
            }

            AddCostEvent(player, null, aCost);
            return; // Only send one cost event at a time
        }

        // All costs paid, remove from triggersWithCosts and continue
        triggersWithCosts.Remove(focusTrigger);
        HandleTriggers(player, currentPlayerToPassTo!, TriggerStage.Choices);
    }

    private bool CheckCardForAdditionalCosts(Player player, Card focusCard) {
        if(focusCard.additionalCosts == null) return false;
        foreach (AdditionalCost aCost in focusCard.additionalCosts) {
            if (aCost.isPaid) continue;

            // For X-based costs where X hasn't been set yet, the first sacrifice cost determines X
            if (aCost.amountBasedOn == AmountBasedOn.X && focusCard.x == null) {
                if (aCost.costType == CostType.Sacrifice) {
                    // Send variable selection cost event - player chooses how many to sacrifice
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
                // If isOpponent is true, the opponent makes the choice
                Player choosingPlayer = e.isOpponent ? GetOpponent(player) : player;
                HandleChoice(e.choices, choosingPlayer, e.isOpponent);
                choiceEffects.Add(tEffect.effects, e);
                choiceCard = null;
            }
        }

        return choiceEffects.Count > 0;
    }

    private bool CheckForChoicesCard(Player player, Card card) {
        if (card.stackEffects == null) return false;
        foreach (Effect effect in card.stackEffects.Where(e => e.effect == EffectType.Choose)) {
            // Use player (the caster) not GetControllerOf - card may still be in hand
            if (effect.conditions != null && !effect.ConditionsAreMet(this, player)) continue;
            Debug.Assert(effect.choices != null, "there are no choices for this choose effect");
            Debug.Assert(effect.choiceIndex != null, "there is no choice index for this choose effect");
            // Initialize multi-choice tracking
            remainingChoices = effect.amount ?? 1;
            selectedChoiceIndices.Clear();
            HandleChoice(effect.choices, player);
            choiceEffects.Add(card.stackEffects, effect);
            choiceCard = card;
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
            bool choiceHasValidTargets = true;
            foreach (Effect e in effectList) {
                if (e.targetType != null && GetPossibleTargets(player, e).Count == 0) {
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
        Debug.Assert(currentActivatedEffect != null, "there's no activated effect waiting for an amount (SetAmount)");
        currentActivatedEffect.SetAmount(amount);
        AttemptToActivate(player, currentActivatedEffect, ActivationStage.CostPayment);
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
        Console.WriteLine($"[HandleEffectTargetSelection] effect={effect.effect}, targetType={effect.targetType}, sourceCard={effect.sourceCard?.name}");
        // Use maxTargets for target count, defaulting to 1 (not amount, which is for damage/life/etc.)
        int targetAmount = effect.maxTargets ?? 1;
        // Skip if no targetType or if effect targets all (no individual selection needed)
        if (effect.targetType == null) {
            Console.WriteLine($"[HandleEffectTargetSelection] Skipping - no targetType");
            return;
        }
        if (effect.all) {
            Console.WriteLine($"[HandleEffectTargetSelection] Skipping - targets all");
            return;
        }
        // Skip resolve-time selections - these are handled during stack resolution, not before casting
        if (effect.resolveTarget) {
            Console.WriteLine($"[HandleEffectTargetSelection] Skipping - resolveTarget");
            return;
        }
        List<int> possibleTargets = GetPossibleTargets(player, effect);
        Console.WriteLine($"[HandleEffectTargetSelection] possibleTargets.Count={possibleTargets.Count}");
        // Skip target selection if there are no valid targets (ability fizzles - resolves with no effect)
        if (possibleTargets.Count == 0) {
            Console.WriteLine($"  No valid targets for effect {effect.effect} - ability will fizzle");
            return;
        }
        string message = effect.EffectToString(this);
        CreateAndAddNewTargetSelectionEvent(player, possibleTargets, targetAmount, message);
        effectsWithTargets.Add(effect);
    }

    private void CreateAndAddNewTargetSelectionEvent(Player player, List<int> targetableUids, int amount, string? message = null) {
        TargetSelection newTargetSelection = new TargetSelection(targetableUids, amount, message);
        GameEvent gEvent = GameEvent.CreateTargetSelectionEvent(newTargetSelection);
        AddEventForPlayer(player, gEvent);
    }

    // For resolve-time target selection (e.g., Consider: select cards after drawing)
    public Effect? resolveTimeTargetEffect;

    public void RequestResolveTimeTargets(Player player, Effect effect) {
        resolveTimeTargetEffect = effect;
        int targetAmount = effect.maxTargets ?? 1;

        // For OpponentHand targets, reveal opponent's hand first so client can show/select them
        if (effect.targetType == TargetType.OpponentHand) {
            Player opponent = GetOpponent(player);
            foreach (Card c in opponent.hand) {
                Reveal(opponent, c);
            }
        }

        List<int> possibleTargets = GetPossibleTargets(player, effect);

        // Generate message based on effect type
        string message = GenerateResolveTimeSelectionMessage(effect, targetAmount);
        CreateAndAddNewTargetSelectionEvent(player, possibleTargets, targetAmount, message);
    }

    private string GenerateResolveTimeSelectionMessage(Effect effect, int amount) {
        string plural = amount == 1 ? "" : "s";

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
                return $"Shuffle {amount} card{plural} into your deck.";
            }

            string position = effect.deckDestination switch {
                DeckDestinationType.Bottom => "bottom",
                DeckDestinationType.Top => "top",
                _ => ""
            };
            return $"Put {amount} card{plural} on the {position} of your library.";
        }

        // Default message
        return $"Select {amount} card{plural}.";
    }

    /// <summary>
    /// Requests user selection for a cost effect (isCost: true) during stack resolution.
    /// </summary>
    public void RequestCostEffectSelection(Player player, Effect effect, List<int> selectableUids) {
        costEffectForSelection = effect;

        // Generate message based on effect type
        string message = effect.effect switch {
            EffectType.Sacrifice => $"Sacrifice a {effect.tokenType?.ToString()?.ToLower() ?? "token"}.",
            _ => "Select a target for the cost."
        };

        // Create a cost event (uses same client-side handling as other costs)
        GameEvent gEvent = GameEvent.CreateCostEvent(
            effect.effect == EffectType.Sacrifice ? CostType.Sacrifice : CostType.Discard,
            1,  // amount
            selectableUids,
            new List<string> { message }
        );
        AddEventForPlayer(player, gEvent);
    }

    public List<int> GetPossibleTargets(Player player, Effect effect) {
        Debug.Assert(effect.targetType != null, "There is no effect TargetType (GetPossibleTargets)");

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
        if (effect.targetType == TargetType.CardInHand) {
            allUids.AddRange(player.hand.Select(c => c.uid));
        }
        // Add opponent's hand card UIDs for OpponentHand target type
        if (effect.targetType == TargetType.OpponentHand) {
            allUids.AddRange(GetOpponent(player).hand.Select(c => c.uid));
        }
        // Add hand and graveyard card UIDs for CardInHandOrGraveyard target type
        if (effect.targetType == TargetType.CardInHandOrGraveyard) {
            allUids.AddRange(player.hand.Select(c => c.uid));
            allUids.AddRange(player.graveyard.Select(c => c.uid));
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
                Debug.Assert(player != null, "there is no player for HerbSacrificeLifeGain");
                tempAmount = player.turnHerbSacrificeCount == 0 ? 2 : 1;
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
            if (!c.HasSummoningSickness()) tempList.Add(c.uid);
        }

        return tempList;
    }

    public List<int> GetAttackables(Player attackingPlayer, Card attackingCard) {
        return GetAttackableUids(attackingPlayer, attackingCard);
    }

    private List<int> GetAttackableUids(Player player, Card attackingCard) {
        List<int> attackableUids = new();
        Player opponent = GetOpponent(player);
        foreach (Card c in opponent.playField) {
            // spectral keyword check
            if (c.keywords != null) {
                if (c.keywords.Contains(Keyword.Spectral)) continue;
            }

            attackableUids.Add(c.uid);
        }

        // if it has dive, add opponent, verify all defending player's summons have been attacked before 
        if (DetectKeyword(attackingCard, Keyword.Dive)) {
            attackableUids.Add(GetOpponent(player).uid);
        } else {
            if (AllOpponentSummonsAreBeingAttacked(player)) attackableUids.Add(opponent.uid);
        }

        return attackableUids;
    }



    private void FinishWithTriggers(Player player, Player playerToPassTo) {
        Console.WriteLine($"[FinishWithTriggers] player={player.playerName}, isTurnPlayer={player == GetPlayerByTurn(true)}");
        if (player == GetPlayerByTurn(true)) {
            Console.WriteLine($"[FinishWithTriggers] Checking non-turn player triggers");
            HandleTriggers(GetPlayerByTurn(false), playerToPassTo);
        } else {
            Console.WriteLine($"[FinishWithTriggers] Both players done, passing prio to {playerToPassTo.playerName}");
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
        Console.WriteLine($"[PassPrioToPlayer] Passing to {player.playerName}, phase={currentPhase}, secondPass={secondPass}");
        prioPlayerId = player.playerId;
        CalculatePossibleMoves(player);

        // Auto-pass for bot players
        if (player.isBot) {
            Console.WriteLine($"[PassPrioToPlayer] Bot auto-passing");
            PassPrio();
            return;
        }

        // Check if we should auto-skip phases
        if (ShouldAutoSkipPhases()) {
            // Track the start of skipping if not already tracking
            if (!isAutoSkipping) {
                skipStartPhase = GetPreviousPhase(currentPhase);
                isAutoSkipping = true;
                // Remember where we are in each player's event list so we only remove NextPhase events added during skip
                skipStartEventIndexP1 = playerOne.eventList.Count;
                skipStartEventIndexP2 = playerTwo.eventList.Count;
            }
            PassPrio();
            return;
        }

        // We're stopping - convert consecutive NextPhase events to SkipToPhase if applicable
        FinalizePhaseSkip();

        GameEvent gEvent = new GameEvent(EventType.GainPrio);
        AddEventForPlayer(player, gEvent);
    }

    /// <summary>
    /// Determines if we should auto-skip phases.
    /// For bot games: only the human player needs passToPhase set.
    /// For PvP: both players need passToPhase set.
    /// Also requires stack to be empty.
    /// Stops at Combat if turn player has attack-capable creatures.
    /// </summary>
    private bool ShouldAutoSkipPhases() {
        // Stack must be empty
        if (stack.Count > 0) {
            return false;
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

        // Check if any player has reached their target
        if (HasPlayerReachedTarget(playerOne) || HasPlayerReachedTarget(playerTwo)) {
            return false;
        }

        // Stop at Combat phase if turn player has creatures that can attack
        if (currentPhase == Phase.Combat) {
            Player turnPlayer = GetPlayerByTurn(true);
            if (GetAttackCapableUids(turnPlayer).Count > 0) {
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
        if (!isAutoSkipping || !skipStartPhase.HasValue) {
            return;
        }

        Phase startPhase = skipStartPhase.Value;

        // Count actual NextPhase events added during the skip (more accurate than phase calculation)
        int phasesSkipped = CountNextPhaseEventsSinceSkipStart(playerOne);

        Console.WriteLine($"FinalizePhaseSkip: from {startPhase}, skipped {phasesSkipped} phases (skipStartEventIndexP1={skipStartEventIndexP1}, P2={skipStartEventIndexP2})");

        // Only create SkipToPhase if we skipped 2+ phases
        if (phasesSkipped >= 2) {
            // Remove the individual NextPhase events and replace with SkipToPhase
            ReplaceNextPhaseEventsWithSkipToPhase(startPhase, phasesSkipped);
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
        Console.WriteLine($"ReplaceNextPhaseEventsWithSkipToPhase: startPhase={startPhase}, phasesSkipped={phasesSkipped}");

        // Remove NextPhase events that were added during the skip (after skipStartEventIndex)
        int p1Removed = RemoveNextPhaseEventsFromSkip(playerOne);
        int p2Removed = RemoveNextPhaseEventsFromSkip(playerTwo);
        Console.WriteLine($"  Removed {p1Removed} NextPhase events from {playerOne.playerName}, {p2Removed} from {playerTwo.playerName}");

        // Add SkipToPhase event for both players
        GameEvent skipEvent = new GameEvent(EventType.SkipToPhase);
        skipEvent.amount = phasesSkipped;  // number of phases to animate through
        skipEvent.universalInt = (int)startPhase;  // starting phase
        Console.WriteLine($"  Adding SkipToPhase event: amount={phasesSkipped}, universalInt={startPhase}");
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
                    effectsList.Add(Effect.CreateEffect(e, stackObjCard));
                }
            }

            return new StackObj(stackObjCard, StackObjType.TriggeredEffect, effectsList, stackObjCard.currentZone,
                player, triggeredEffect.description);
        }

        if (aEffect != null) {
            if (aEffect.effects != null) {
                foreach (Effect e in aEffect.effects) {
                    effectsList.Add(Effect.CreateEffect(e, stackObjCard));
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
        GameEvent gEvent = GameEvent.CreateStackEvent(EventType.Trigger, new StackDisplayData(stackObj, this));
        AddEventForBothPlayers(stackObj.player, gEvent);
    }

    public void AddRepeatToStack(StackObj stackObj) {
        // If something goes on the stack during auto-skipping, finalize the skip first
        FinalizePhaseSkip();

        stack.Push(stackObj);
        // Reset secondPass so the stack doesn't auto-resolve when priority is passed
        secondPass = false;
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
        // Check tokens for activatable abilities (e.g., granted by GrantActive passive)
        foreach (Token token in player.tokens) {
            if (Utils.CheckPlayability(token, this, player)) {
                if (!player.activatables.Contains(token)) {
                    player.activatables.Add(token);
                }
            }
        }
    }

    private bool AllOpponentSummonsAreBeingAttacked(Player player) {
        Player opponent = GetOpponent(player);
        // has no summons or they all have been assigned attackers
        return opponent.playField.Count == 0 || opponent.playField.All(c => currentAttackUids.ContainsValue(c.uid));
    }

    public void AttemptToCast(Player attemptingPlayer, Card card, CastingStage stage = CastingStage.Initial, bool isAction = true) {
        switch (stage) {
            case CastingStage.Initial:
                cardBeingCast = card;
                goto case CastingStage.AmountSelection;
            case CastingStage.AmountSelection:
                // X must be set before additional costs (for X-based sacrifice/life costs)
                if (CheckForXCost(attemptingPlayer, card)) return;
                goto case CastingStage.AdditionalCosts;
            case CastingStage.AdditionalCosts:
                if (CheckCardForAdditionalCosts(attemptingPlayer, card)) return;
                goto case CastingStage.Choices; 
            case CastingStage.Choices:
                if (CheckForChoicesCard(attemptingPlayer, card)) return;
                goto case CastingStage.SpellAlternateCost;
            case CastingStage.SpellAlternateCost:
                Debug.Assert(cardBeingCast != null, "there is no card being cast for AttemptToCast()");
                // Check for ExileFromHand alternate costs on SPELLS BEFORE target selection
                if (cardBeingCast.type == CardType.Spell && cardBeingCast.GetCost() > 0) {
                    AlternateCost? spellExileAltCost = GetPayableExileFromHandAlternateCost(attemptingPlayer, cardBeingCast);
                    if (spellExileAltCost != null) {
                        bool canPayLife = attemptingPlayer.lifeTotal > cardBeingCast.GetCost();
                        if (!canPayLife) {
                            // Only ExileFromHand available - use it automatically
                            usingAlternateCost = true;
                            currentAlternateCost = spellExileAltCost;
                            RequestAlternateCostPayment(attemptingPlayer, spellExileAltCost);
                            return;
                        }
                        // Both options available - ask player to choose
                        currentAlternateCost = spellExileAltCost;
                        string altCostDescription = GetAlternateCostDescription(spellExileAltCost);
                        var choicesText = new List<string> {
                            $"Pay life ({cardBeingCast.GetCost()} LP)",
                            altCostDescription
                        };
                        GameEvent gEvent = GameEvent.CreateOptionEvent(new PlayerChoice(choicesText, "Choose how to cast " + cardBeingCast.name));
                        AddEventForPlayer(attemptingPlayer, gEvent);
                        return;
                    }
                }
                goto case CastingStage.TargetSelection;
            case CastingStage.TargetSelection:
                Debug.Assert(cardBeingCast != null, "there is no card being cast for AttemptToCast()");
                // check for targets for card being cast -> wait for player response by returning
                if (CheckForCardTargetSelection(attemptingPlayer, cardBeingCast)) return;
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
                goto case CastingStage.AlternateCostSelection;
            case CastingStage.AlternateCostSelection:
                Debug.Assert(cardBeingCast != null, "there is no card being cast for AttemptToCast()");
                // Check for ExileFromHand alternate costs on SUMMONS (can skip tribute entirely)
                AlternateCost? exileFromHandAltCost = GetPayableExileFromHandAlternateCost(attemptingPlayer, cardBeingCast);
                if (exileFromHandAltCost != null && cardBeingCast.type == CardType.Summon && cardBeingCast.GetCost() > 0) {
                    bool canPayTribute = cardBeingCast.GetCost() <= Utils.GetTributeValue(attemptingPlayer, cardBeingCast);
                    if (!canPayTribute) {
                        // Only ExileFromHand available - use it automatically
                        usingAlternateCost = true;
                        currentAlternateCost = exileFromHandAltCost;
                        RequestAlternateCostPayment(attemptingPlayer, exileFromHandAltCost);
                        return;
                    }
                    // Both options available - ask player to choose
                    currentAlternateCost = exileFromHandAltCost;
                    string altCostDescription = GetAlternateCostDescription(exileFromHandAltCost);
                    var choicesText = new List<string> {
                        "Pay tribute (normal cost)",
                        altCostDescription
                    };
                    GameEvent gEvent = GameEvent.CreateOptionEvent(new PlayerChoice(choicesText, "Choose how to summon " + cardBeingCast.name));
                    AddEventForPlayer(attemptingPlayer, gEvent);
                    return;
                }
                // Check for sacrifice alternate costs on summons
                if (cardBeingCast.type == CardType.Summon && cardBeingCast.GetCost() > 0) {
                    bool canPayTribute = cardBeingCast.GetCost() <= Utils.GetTributeValue(attemptingPlayer, cardBeingCast);
                    AlternateCost? sacrificeAltCost = GetPayableSacrificeAlternateCost(attemptingPlayer, cardBeingCast);

                    if (sacrificeAltCost != null && !canPayTribute) {
                        // Only alternate cost is available - use it automatically
                        usingAlternateCost = true;
                        currentAlternateCost = sacrificeAltCost;
                        RequestAlternateCostPayment(attemptingPlayer, sacrificeAltCost);
                        return;
                    }
                    if (sacrificeAltCost != null && canPayTribute) {
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
                    // Only tribute available - continue to tribute selection
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
                    Console.WriteLine($"[DEBUG] TributeSelection: player has {attemptingPlayer.tokens.Count} tokens");
                    foreach (Token t in attemptingPlayer.tokens) {
                        Console.WriteLine($"[DEBUG]   Token in list: uid={t.uid}, name={t.name}, tribe={t.tribe}");
                    }
                    if (cardBeingCast.alternateCosts != null) {
                        foreach (AlternateCost altCost in cardBeingCast.alternateCosts) {
                            Console.WriteLine($"[DEBUG]   Checking altCost: type={altCost.altCostType}, tokenType={altCost.tokenType}, tribe={altCost.tribe}");
                            if (altCost.altCostType != AltCostType.TributeMultiplier) continue;
                            foreach (Token token in attemptingPlayer.tokens) {
                                Console.WriteLine($"[DEBUG]     Checking token uid={token.uid} against altCost");
                                if (altCost.tokenType != null && token.tokenType == altCost.tokenType) {
                                    Console.WriteLine($"[DEBUG]     Token uid={token.uid} matched by tokenType");
                                    tributeableUids.Add(token.uid);
                                    tributeValues[token.uid] = altCost.amount;
                                } else if (altCost.tribe != null && token.tribe == altCost.tribe) {
                                    Console.WriteLine($"[DEBUG]     Token uid={token.uid} matched by tribe");
                                    tributeableUids.Add(token.uid);
                                    tributeValues[token.uid] = altCost.amount;
                                } else {
                                    Console.WriteLine($"[DEBUG]     Token uid={token.uid} did NOT match");
                                }
                            }
                        }
                    }
                    Console.WriteLine($"[DEBUG] Final tributeableUids: [{string.Join(", ", tributeableUids)}]");

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
        // pay the life
        if (cardBeingCast.type != CardType.Summon && cardBeingCast.GetCost() > 0)
            PayLifeCost(attemptingPlayer, cardBeingCast.GetCost());
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
                    goto case ActivationStage.TargetSelection;
                }
                AddCostEvent(attemptingPlayer, aEffect);
                return;
            case ActivationStage.TargetSelection:
                // if there are any effects requiring targets, handle target selection for all effects
                if (aEffect.effects != null && aEffect.effects.Any(effect => effect.targetType != null)) {
                    foreach (Effect e in aEffect.effects) {
                        HandleEffectTargetSelection(attemptingPlayer, e);
                    }
                }
                break;
        }

        Debug.Assert(currentActivatedEffect != null, "there is no current activated effect");
        ActivateAbility(attemptingPlayer, currentActivatedEffect);
    }

    private void ActivateAbility(Player attemptingPlayer, ActivatedEffect aEffect) {
        currentActivatedEffect = null;
        AddStackObjToStack(CreateStackObj(attemptingPlayer, aEffect.sourceCard, null, aEffect));
        // Check for triggers from cost payment (e.g., discard triggers) before passing priority
        CheckForTriggersAndPassives(EventType.Cast, attemptingPlayer);
    }

    private void AddVariableCostEvent(Player attemptingPlayer, AdditionalCost aCost, Card sourceCard) {
        Qualifier effectQualifier = new Qualifier(aCost, attemptingPlayer);
        List<int> selectableUidList = new();

        // Get all matching cards/tokens
        foreach (Card c in attemptingPlayer.allCardsPlayer) {
            if (QualifyCard(c, effectQualifier)) selectableUidList.Add(c.uid);
        }

        int maxAmount = selectableUidList.Count;
        string targetName = aCost.tokenType?.ToString() ?? "card";
        string message = $"sacrifice any number of {targetName}s (0 to {maxAmount})";

        // Create cost event with variableAmount=true, amount=max
        GameEvent gEvent = GameEvent.CreateCostEvent(aCost.costType, maxAmount, selectableUidList,
            new List<string> { message }, variableAmount: true);
        AddEventForPlayer(attemptingPlayer, gEvent);
    }

    private void AddCostEvent(Player attemptingPlayer, ActivatedEffect? aEffect = null, AdditionalCost? aCost = null, Card? sourceCard = null) {
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
        List<string> eventMessageList = new() { GetCostMessage(cc) };
        switch (cc.costType) {
            case CostType.Sacrifice:
                // get the list of possible selections
                foreach (Card c in attemptingPlayer.allCardsPlayer) {
                    if (QualifyCard(c, effectQualifier)) selectableUidList.Add(c.uid);
                }
                break;
            case CostType.Discard:
                foreach (Card c in attemptingPlayer.hand) {
                    if(QualifyCard(c, effectQualifier)) selectableUidList.Add(c.uid);
                }
                break;
            case CostType.DiscardOrSacrificeMerfolk:
                // Check available options for this choice cost
                // Note: Eadro CAN sacrifice itself - if no valid targets remain, ability fizzles
                List<Card> merfolkInHand = attemptingPlayer.hand.Where(c => c.tribe == Tribe.Merfolk).ToList();
                List<Card> merfolkInPlay = GetAllCardsControlled(attemptingPlayer)
                    .Where(c => c.tribe == Tribe.Merfolk).ToList();

                bool canDiscard = merfolkInHand.Count > 0;
                bool canSacrifice = merfolkInPlay.Count > 0;

                if (canDiscard && canSacrifice) {
                    // Both options available - present choice to player
                    pendingDiscardOrSacrificeChoice = true;
                    var choicesText = new List<string> {
                        "Discard a merfolk",
                        "Sacrifice a merfolk"
                    };
                    GameEvent choiceEvent = GameEvent.CreateOptionEvent(new PlayerChoice(choicesText, "Choose how to pay the cost:"));
                    AddEventForPlayer(attemptingPlayer, choiceEvent);
                    return; // Wait for choice response
                } else if (canDiscard) {
                    // Only discard available - send discard cost event
                    resolvedChoiceCostType = CostType.Discard;
                    foreach (Card c in merfolkInHand) selectableUidList.Add(c.uid);
                    eventMessageList = new List<string> { "discard a merfolk" };
                    GameEvent discardEvent = GameEvent.CreateCostEvent(CostType.Discard, 1, selectableUidList, eventMessageList);
                    AddEventForPlayer(attemptingPlayer, discardEvent);
                    return;
                } else if (canSacrifice) {
                    // Only sacrifice available - send sacrifice cost event
                    resolvedChoiceCostType = CostType.Sacrifice;
                    foreach (Card c in merfolkInPlay) selectableUidList.Add(c.uid);
                    eventMessageList = new List<string> { "sacrifice a merfolk" };
                    GameEvent sacrificeEvent = GameEvent.CreateCostEvent(CostType.Sacrifice, 1, selectableUidList, eventMessageList);
                    AddEventForPlayer(attemptingPlayer, sacrificeEvent);
                    return;
                }
                return; // No valid options (shouldn't happen if CostIsAvailable checked first)
        }
        // create and add the event
        GameEvent gEvent = GameEvent.CreateCostEvent(cc.costType, cc.amount,
            selectableUidList, eventMessageList);
        AddEventForPlayer(attemptingPlayer, gEvent);
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
    }

    private bool CardCanTributeTo(Card c, Card requiringCard) {
        // no passive
        if (c.passiveEffects == null) return true;
        // check each passive
        foreach (PassiveEffect pEffect in c.passiveEffects) {
            // no tribute restriction
            if (pEffect.passive != Passive.TributeRestriction) return true;
            // tribute restriction matches requirement
            if (pEffect.tribe != null && pEffect.tribe == requiringCard.tribe) return true;
        }

        // tribute restriction does not match requirement
        return false;
    }


    public void Tribute(int playerId, List<int> tributeUids) {
        Player tributingPlayer = accountIdToPlayer[playerId];
        Console.WriteLine($"[DEBUG] Tribute called with uids: [{string.Join(", ", tributeUids)}]");
        foreach (int uid in tributeUids) {
            Console.WriteLine($"[DEBUG]   Destroying uid={uid}");
            if (!cardByUid.ContainsKey(uid)) {
                Console.WriteLine($"[DEBUG]   ERROR: uid={uid} not found in cardByUid!");
                continue;
            }
            Card c = cardByUid[uid];
            Console.WriteLine($"[DEBUG]   Card: name={c.name}, type={c.type}");
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
                Console.WriteLine($"  turnSummonCount after={player.turnSummonCount}");
                break;
        }

        player.playables.Remove(card);
        player.allCardsPlayer.Remove(card);
        RemoveFromHand(player, card);

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
        GameEvent gEvent = GameEvent.CreateStackEvent(EventType.Cast, new StackDisplayData(newStackObj, this));
        AddEventForBothPlayers(player, gEvent);
        // reset second pass whenever something is added to the stack
        secondPass = false;
        // pay cost
        // scorch check
        if (card.keywords != null && card.keywords.Contains(Keyword.Scorch)) {
            ApplySpellburn(player, true);
        } else if (card.type == CardType.Spell) {
            if (card.GetCost() > 0) player.scorched = false;
            ApplySpellburn(player, false);
        }

        if (isAction) {
            CheckForTriggersAndPassives(EventType.Cast, player);
        }
    }

    public void PassPrio() {
                if (secondPass) {
            if (stack.Count > 0) {
                StackObj tempStackObj = stack.Peek();
                                stack.Pop();
                prioPlayerId = -1;
                tempStackObj.ResolveStackObj(this);
                secondPass = false;
                return;
            }

            if (currentAttackUids.Count > 0) {
                ResolveAttacks();
            }

            secondPass = false;
            GoToNextPhase();
        } else {
            secondPass = true;
            PassPrioToPlayer(GetPlayerByPrio(false));
        }
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
            int attackValue = attackingCard.GetAttack();
            int retaliationValue = cardByUid.TryGetValue(pair.Value, out var value) ? value.GetAttack() : 0;
            Console.WriteLine($"[ResolveAttacks] {attackingCard.name} (uid={pair.Key}, atk={attackValue}) -> target uid={pair.Value}");
            // create a combat event
            GameEvent gEvent = GameEvent.CreateCombatEvent(pair.Key, pair.Value, attackValue);
            AddEventForBothPlayers(GetPlayerByTurn(true), gEvent);
            // deal the damage
            DealDamage(pair.Value, attackValue);
            DealDamage(attackingCard.uid, retaliationValue);
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
            if (c.GetDefense() <= 0) {
                Kill(c);
            }
        }
        foreach (var c in playerTwo.playField.ToList()) {
            if (c.defense == null) continue;
            if (c.GetDefense() <= 0) {
                Kill(c);
            }
        }
        if (playerOne.lifeTotal <= 0 || playerTwo.lifeTotal <= 0) {
            Console.WriteLine("GAME OVER");
            Player winningPlayer = playerOne.lifeTotal > playerTwo.lifeTotal ? playerOne : playerTwo;
            GameEvent gEvent = GameEvent.CreateEndGameEvent(winningPlayer.uid);
            AddEventForBothPlayers(winningPlayer, gEvent);
        }
    }

    private void Kill(Card c) {
        // reset damage taken
        c.damageTaken = 0;

        // Check for DeathBySpell replacement effect
        bool returnToHand = false;
        if (c.tookSpellDamage && c.triggeredEffects != null) {
            foreach (TriggeredEffect tEffect in c.triggeredEffects) {
                if (tEffect.trigger == Trigger.DeathBySpell && tEffect.scope == Scope.SelfOnly) {
                    returnToHand = true;
                    break;
                }
            }
        }
        c.tookSpellDamage = false;  // reset flag

        // Store controller/owner before removing from play
        Player controller = GetControllerOf(c);
        Player owner = GetOwnerOf(c);

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
                AddToGraveyard(cardOwner, c);
                // Send Death event (card went to graveyard)
                GameEvent gEvent = GameEvent.CreateUidEvent(EventType.Death, c.uid);
                AddEventForBothPlayers(GetPlayerByTurn(true), gEvent);
            }
            c.grantedPassives.Clear();
            // Add death trigger regardless of destination (card still "died")
            triggersToCheck.Add(new TriggerContext(Trigger.Death, null, c));
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

    public void Discard(Player player, Card c) {
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
            AddToGraveyard(player, c);
            // Send normal Discard event (animates to graveyard)
            GameEvent gEvent = GameEvent.CreateCardEvent(EventType.Discard, new CardDisplayData(c));
            AddEventForBothPlayers(player, gEvent);
        }
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

    private void GoToNextPhase() {
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
        } else {
            currentPhase++;
        }

        triggersToCheck.Add(TriggerContext.CreatePhaseTriggerContext(currentPhase));
        GameEvent gEvent = new GameEvent(EventType.NextPhase);
        AddEventForBothPlayers(GetPlayerByTurn(true), gEvent);
        // activate attackCapables for combat phase
        if (currentPhase == Phase.Combat) {
            List<int> attackCapableUids = GetAttackCapableUids(GetPlayerByTurn(true));
            GameEvent acEvent = GameEvent.CreateMultiUidEvent(EventType.AttackCapables, attackCapableUids);
            AddEventForPlayer(GetPlayerByTurn(true), acEvent);
        }

        // draw for beginning of turn
        if (currentPhase == Phase.Draw) {
            Console.WriteLine($"[GoToNextPhase] Drawing for turn player on Draw phase");
            Draw(GetPlayerByTurn(true), 1);
        }
        Console.WriteLine($"[GoToNextPhase] About to CheckForTriggersAndPassives, triggersToCheck.Count={triggersToCheck.Count}");
        CheckForTriggersAndPassives(EventType.NextPhase);
    }

    private void PassTurn() {
        HandleEndOfTurnPassives();
        Player currentTurnPlayer = GetPlayerByTurn(true);
        // reset turn counters
        currentTurnPlayer.turnSummonCount = 0;
        currentTurnPlayer.turnSummonLimitBonus = 0;
        currentTurnPlayer.turnDrawCount = 0;
        // reset herb sacrifice counters for both players
        GetPlayerByTurn(true).turnHerbSacrificeCount = 0;
        GetPlayerByTurn(false).turnHerbSacrificeCount = 0;
        // remove spellburn if not scorched
        RemoveSpellburn(GetPlayerByTurn(true));
        RemoveSpellburn(GetPlayerByTurn(false));
        // reset cantAttack flags
        GetPlayerByTurn(true).cantAttackThisTurn = false;
        GetPlayerByTurn(false).cantAttackThisTurn = false;
        // reset exhausted flags
        GetPlayerByTurn(true).exhausted = false;
        GetPlayerByTurn(false).exhausted = false;
        // clear player passives that expire at end of turn
        GetPlayerByTurn(true).playerPassives.RemoveAll(p => p.thisTurn);
        GetPlayerByTurn(false).playerPassives.RemoveAll(p => p.thisTurn);
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

    public void CreateTokenForPlayer(Player player, Token token, bool isAttacking) {
        cardByUid.Add(token.uid, token);
        if (token.type == CardType.Token) {
            AddToTokenZone(player, token);
            CheckForPassives();
        } else {
            token.currentZone = Zone.Play;
            Summon(token, player, isAttacking);
        }
    }

    public bool IsPlayerOne(int accountId) {
        return accountId == playerOne.playerId;
    }

    public Player GetOpponent(Player player) {
        return player == playerOne ? playerTwo : playerOne;
    }

    private void DrawOpeningHands() {
        Draw(playerOne, 5);
        Draw(playerTwo, 5);
    }
                
    public void Draw(Player player, int amount) {
        for (int i = 0; i < amount; i++) {
            Debug.Assert(player.deck != null, "player.deck != null");
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
        for (int i = 0; i < amount; i++) {
            Debug.Assert(player.deck != null, "player.deck != null");
            // If deck is empty, stop milling (no death from mill in this game)
            if (player.deck.Count == 0) return;
            Card topCard = player.deck[0];
            player.deck.RemoveAt(0);
            AddCardToAllCardsPlayer(player, topCard);

            // Check for replacement effect: summons go to exile instead of graveyard
            if (topCard.type == CardType.Summon &&
                player.playerPassives.Any(p => p.passive == Passive.SummonsToGraveyardExileInstead)) {
                Console.WriteLine($"[Mill] Replacement effect: {topCard.name} exiled instead of going to graveyard");
                AddToExile(player, topCard);
                // Send SendToZone event to Exile with source = Deck
                GameEvent exileEvent = GameEvent.CreateZoneGameEvent(Zone.Exile, new CardDisplayData(topCard), Zone.Deck);
                AddEventForBothPlayers(player, exileEvent);
            } else {
                AddToGraveyard(player, topCard);
                // Send normal Mill event (animates to graveyard)
                GameEvent gEvent = GameEvent.CreateCardEvent(EventType.Mill, new CardDisplayData(topCard));
                AddEventForBothPlayers(player, gEvent);
            }
            triggersToCheck.Add(new TriggerContext(Trigger.Mill, null, topCard));
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
        for(int i = 0; i < lookedAtSelectionDestinations.Count; i++) {
            DeckDestination currentDestination = lookedAtSelectionDestinations[i];
            List<int> uidList = destinationUidLists[i];
            if (currentDestination.ordering == Ordering.Random) {
                uidList = GetShuffled(uidList);
            }
            switch (lookedAtSelectionDestinations[i].deckDestination) {
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
            AddToGraveyard(player, sourceCard);
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

    private void AddToGraveyard(Player player, Card c) {
        player.graveyard.Add(c);
        AddCardToAllCardsPlayer(player, c);
        c.currentZone = Zone.Graveyard;
        triggersToCheck.Add(new TriggerContext(Trigger.EnteredZone, Zone.Graveyard, c));
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
    

    public void SubmitAttack(Player attackingPlayer) {
        List<Card> currentAttackingCards = currentAttackUids.Select(pair => cardByUid[pair.Key]).ToList();
        triggersToCheck.Add(new TriggerContext(Trigger.Attack, null, null, currentAttackingCards));
        CheckForTriggersAndPassives(EventType.Attack, attackingPlayer);
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

    public void SendToZone(Player targetPlayer, Zone destination, Card targetCard, DeckDestination? deckDestination = null) {
        // cards leaving play always go to their owner's zones (unless stated otherwise);
        Zone sourceZone = targetCard.currentZone;

        // Tokens can't go to hand, deck, or exile - destroy them instead
        if (targetCard is Token && destination != Zone.Play && destination != Zone.Graveyard) {
            Console.WriteLine($"[SendToZone] Token {targetCard.name} can't go to {destination}, destroying instead");
            Destroy(targetCard);
            return;
        }

        // Replacement effect: summons go to exile instead of graveyard if player has the passive
        if (destination == Zone.Graveyard && targetCard.type == CardType.Summon) {
            Player cardOwner = GetOwnerOf(targetCard);
            if (cardOwner.playerPassives.Any(p => p.passive == Passive.SummonsToGraveyardExileInstead)) {
                Console.WriteLine($"[SendToZone] Replacement effect: {targetCard.name} exiled instead of going to graveyard");
                destination = Zone.Exile;
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
                AddToGraveyard(targetPlayer, targetCard);
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

    public void DealDamage(int targetUid, int amount, bool isSpellDamage = false) {
        if (cardByUid.TryGetValue(targetUid, out var card)) {
            if (card.GetPassives().Any(p => p.passive == Passive.CantTakeDamage)) return;
            card.damageTaken += amount;
            if (isSpellDamage) card.tookSpellDamage = true;
            GameEvent gEvent = GameEvent.CreateRefreshCardDisplayEvent(card);
            AddEventForBothPlayers(GetControllerOf(card), gEvent);
        } else {
            // TODO you might want to consider an independent take damage function instead of LoseLife
            // TODO they might eventually be separate triggers just like MTG
            LoseLife(PlayerByUid(targetUid), amount);
        }
        CheckForDeaths();
    }

    public void GainLife(Player affectedPlayer, int? amount) {
        Debug.Assert(amount != null, "there is no amount associated with this gainLife Effect");
        affectedPlayer.lifeTotal += amount.Value;
        // TODO check for life gain triggers
        GameEvent gEvent = GameEvent.CreateGameEventWithAmount(EventType.GainLife, false, amount.Value);
        AddEventForBothPlayers(affectedPlayer, gEvent);
    }
    
    public void LoseLife(Player affectedPlayer, int? amount) {
        Debug.Assert(amount != null, "there is no amount associated with this loseLife Effect");
        affectedPlayer.lifeTotal -= amount.Value;
        // TODO check for lose life triggers
        GameEvent gEvent = GameEvent.CreateGameEventWithAmount(EventType.LoseLife, false, amount.Value);
        gEvent.universalInt = affectedPlayer.lifeTotal;  // Include expected life total for client verification
        AddEventForBothPlayers(affectedPlayer, gEvent);
    }

    public void SetLifeTotal(Player affectedPlayerId, int? amount) {
        Debug.Assert(amount != null, "there is no amount associated with this SetLifeTotal Effect");
        affectedPlayerId.lifeTotal = amount.Value;
        // TODO check for life change triggers (life gain and loss)
        GameEvent gEvent = GameEvent.CreateGameEventWithAmount(EventType.SetLifeTotal, false, amount.Value);
        AddEventForBothPlayers(affectedPlayerId, gEvent);
    }
    
    public void GrantKeyword(Player player, Card targetCard, Keyword keyword) {
        targetCard.grantedPassives.Add(new PassiveEffect(Passive.GrantKeyword, keyword));
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
        Console.WriteLine($"  Focus effect type: {focusEffect.effect}");
        // assign targets for effects
        foreach (int uid in targetedUids) {
            Console.WriteLine($"  Adding uid {uid} to targetUids");
            focusEffect.targetUids.Add(uid);
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
        // if it's a card your selecting targets for
        if (cardBeingCast != null) {
            AttemptToCast(player, cardBeingCast, CastingStage.AdditionalChoices);
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
        Console.WriteLine($"[PlayerChoiceDiscard] Requesting selection for {effect.sourceCard?.name}, variableAmount={variableAmount}");
        Console.WriteLine($"[PlayerChoiceDiscard] Effect tribe={effect.tribe}, cardType={effect.cardType}");
        Console.WriteLine($"[PlayerChoiceDiscard] Player hand count: {player.hand.Count}");

        // Find matching cards in hand based on cardType and tribe (if specified)
        List<int> selectableUids = new();
        Qualifier qualifier = new Qualifier(effect, player);

        foreach (Card c in player.hand) {
            bool qualifies = QualifyCard(c, qualifier);
            Console.WriteLine($"[PlayerChoiceDiscard] Card {c.name} (uid={c.uid}): tribe={c.tribe}, type={c.type}, qualifies={qualifies}");
            if (qualifies) {
                selectableUids.Add(c.uid);
            }
        }

        if (selectableUids.Count == 0) {
            Console.WriteLine("[PlayerChoiceDiscard] No matching cards in hand");
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

        Console.WriteLine($"[PlayerChoiceDiscard] Selectable UIDs: [{string.Join(", ", selectableUids)}]");
        Console.WriteLine($"[PlayerChoiceDiscard] Message: {message}, selectionAmount={selectionAmount}");

        GameEvent gEvent = GameEvent.CreateCostEvent(CostType.Discard, selectionAmount, selectableUids,
            new List<string> { message }, variableAmount: variableAmount);
        AddEventForPlayer(player, gEvent);

        Console.WriteLine($"[PlayerChoiceDiscard] Sent selection event with {selectableUids.Count} options");
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
            waitingForHandSizeDiscard = false;
            // Now continue with passing the turn
            PassTurn();
            // Send the NextPhase event that was skipped
            triggersToCheck.Add(TriggerContext.CreatePhaseTriggerContext(currentPhase));
            GameEvent gEvent = new GameEvent(EventType.NextPhase);
            AddEventForBothPlayers(GetPlayerByTurn(true), gEvent);
            if (currentPhase == Phase.Draw) Draw(GetPlayerByTurn(true), 1);
            CheckForTriggersAndPassives(EventType.NextPhase);
            return;
        }

        // cost effect selection at resolve time (for isCost effects that need user selection)
        if (costEffectForSelection != null) {
            Console.WriteLine($"[CostEffect] Processing selection, {selectedCards?.Count ?? 0} cards selected");
            Effect effect = costEffectForSelection;
            costEffectForSelection = null;

            // Set the targetUids on the effect so it knows what to sacrifice
            effect.targetUids = selectedCards?.Select(c => c.uid).ToList() ?? new List<int>();

            Console.WriteLine($"[CostEffect] Set targetUids=[{string.Join(", ", effect.targetUids)}]");

            // Resume stack object resolution - the effect will now be resolved with the selected targets
            Debug.Assert(unresolvedStackObj != null, "No unresolved stack object for cost effect selection");
            unresolvedStackObj.ResumeResolve(this);
            return;
        }

        // playerChoice discard at resolve time (for discard effects with amountBasedOn: playerChoice)
        if (playerChoiceDiscardEffect != null) {
            Console.WriteLine($"[PlayerChoiceDiscard] Processing selection, {selectedCards?.Count ?? 0} cards selected");
            Effect effect = playerChoiceDiscardEffect;
            playerChoiceDiscardEffect = null;

            // Set the targetUids and amount on the effect
            effect.targetUids = selectedCards?.Select(c => c.uid).ToList() ?? new List<int>();
            effect.amount = selectedCards?.Count ?? 0;

            Console.WriteLine($"[PlayerChoiceDiscard] Set amount={effect.amount}, targetUids=[{string.Join(", ", effect.targetUids)}]");

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
            // Use resolvedChoiceCostType for choice-based costs (like DiscardOrSacrificeMerfolk)
            CostType costToUse = resolvedChoiceCostType ?? currentActivatedEffect.costType;
            resolvedChoiceCostType = null;  // Clear after use
            // Save the controller before paying cost (card might be sacrificed as part of the cost)
            Player controller = player;
            PayCost(player, costToUse, selectedCards);
            AttemptToActivate(controller, currentActivatedEffect, ActivationStage.TargetSelection);
            return;
        }

        // triggers
        if (triggersWithCosts.Count > 0) {
            TriggeredEffect focusTrigger = triggersWithCosts.First();
            foreach (AdditionalCost aCost in focusTrigger.additionalCosts!) {
                if (aCost.isPaid) continue;
                // Validate that enough cards were selected for the cost
                int requiredAmount = aCost.amount;
                if (selectedCards == null || selectedCards.Count < requiredAmount) {
                    Console.WriteLine($"Cost not met: required {requiredAmount}, got {selectedCards?.Count ?? 0}");
                    return;
                }
                PayCost(player, aCost.costType, selectedCards);
                aCost.isPaid = true;
                if (focusTrigger.additionalCosts.Last() == aCost) triggersWithCosts.Remove(focusTrigger);
                break;
            }

            if (triggersWithCosts.Count == 0) {
                Debug.Assert(currentPlayerToPassTo != null, "there is no current player to pass to");
                HandleTriggers(player, currentPlayerToPassTo, TriggerStage.Choices);
            } else {
                // Send the next cost event with fresh selectable cards
                SendNextTriggerCostEvent(player);
            }
        }

        // card casts
        if (cardAdditionalCostAmount > 0) {
            Debug.Assert(cardBeingCast != null, "there is no card being cast");
            Debug.Assert(cardBeingCast.additionalCosts != null, "card being cast has no additional costs");
            foreach (AdditionalCost aCost in cardBeingCast.additionalCosts) {
                if(aCost.isPaid) continue;

                // Check if this is an X-determining sacrifice (variable selection that sets X)
                bool isXDeterminingSacrifice = aCost.amountBasedOn == AmountBasedOn.X &&
                                                cardBeingCast.x == null &&
                                                aCost.costType == CostType.Sacrifice;

                if (isXDeterminingSacrifice) {
                    // Set X based on how many cards were selected (can be 0)
                    int selectedCount = selectedCards?.Count ?? 0;
                    cardBeingCast.x = selectedCount;

                    // Pay the cost (sacrifice the selected cards)
                    if (selectedCards != null && selectedCards.Count > 0) {
                        PayCost(player, aCost.costType, selectedCards);
                    }
                    aCost.isPaid = true;
                    cardAdditionalCostAmount--;

                    // Now that X is set, re-check for remaining additional costs
                    if (CheckCardForAdditionalCosts(player, cardBeingCast)) {
                        return; // More costs to pay
                    }
                    // All costs paid, continue to choices
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
                    Discard(player, c);
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
        }
    }
    
    

    public void Reveal(Player affectedPlayer, Card subjectCard) {
        GameEvent gEvent = GameEvent.CreateCardEvent(EventType.Reveal, new CardDisplayData(subjectCard));
        AddEventForPlayer(GetOpponent(affectedPlayer), gEvent);
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
}
