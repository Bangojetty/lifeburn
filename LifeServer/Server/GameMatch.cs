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

    // tributing
    private Card? cardRequiringTribute;
    
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
        if (card.GetPassives().Count == 0) return false;
        if (card.GetPassives().All(passiveEffect => passiveEffect.passive != passive)) return false;
        Player controller = allCardsInPlay.Contains(card) ? GetControllerOf(card) : GetOwnerOf(card);
        foreach (var pEffect in card.GetPassives().Where(pEffect => pEffect.passive == passive)) {
            if (pEffect.conditions == null) continue;
            if (pEffect.conditions.Any(c => !c.Verify(this, controller))) {
                return false;
            }
        }

        return true;
    }

    private bool DetectKeyword(Card card, Keyword keyword) {
        if (card.keywords == null) return false;
        return card.keywords.Any(cardKeyword => keyword == cardKeyword);
    }

    public void CheckForTriggersAndPassives(EventType eventType, Player? playerToPassTo = null) {
        Player turnPlayer = GetPlayerByTurn(true);
        Player nonTurnPlayer = GetPlayerByTurn(false);
        foreach (TriggerContext tc in triggersToCheck) {
            currentTriggerContext = tc;
            // first check for the players whose turn it is
            CheckForTriggersPlayer(tc, turnPlayer);
            CheckForTriggersPlayer(tc, nonTurnPlayer);
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
    /// </summary>
    public void CheckForPassives() {
        foreach (Card c in allCardsInPlay) {
            // no passives
            if (c.GetPassives().Count == 0) continue;
            // apply passives
            foreach (PassiveEffect pEffect in c.GetPassives()) ApplyPassive(c, pEffect);
        }

        CheckForPassivesInHand(playerOne);
        CheckForPassivesInHand(playerTwo);

        // refresh all passives in all cards in non-deck zones
        RefreshPassives();
    }

    private void CheckForPassivesInHand(Player player) {
        foreach (Card c in player.hand) {
            if (c.GetPassives().Count == 0) continue;
            foreach (PassiveEffect pEffect in c.GetPassives().Where(p => handPassiveTypes.Contains(p.passive))) { 
                ApplyPassive(c, pEffect, true);
            }
        }
    }

    /// <summary>
    /// Applies the passive to any cards that qualify who aren't already affected.
    /// </summary>
    /// <param name="sourceCard"></param>
    /// <param name="pEffect"></param>
    private void ApplyPassive(Card sourceCard, PassiveEffect pEffect, bool inHand = false) {
        Player playerToQualify = inHand ? GetOwnerOf(sourceCard) : GetControllerOf(sourceCard);
        Qualifier pQualifier = new Qualifier(pEffect, playerToQualify);
        foreach (Card c in allCardsInPlay) {
            if (!QualifyCard(c, pQualifier)) continue;
            // add passive to affecting passives for this card
            c.grantedPassives.Add(pEffect);
        }
        ApplyPassiveToHandCards(playerToQualify, pQualifier, pEffect); 
    }

    private void ApplyPassiveToHandCards(Player player, Qualifier pQualifier, PassiveEffect pEffect) {
        if (!handPassiveTypes.Contains(pEffect.passive)) return;
        foreach (Card c in player.hand) {
            if(!QualifyCard(c, pQualifier)) continue;
            c.grantedPassives.Add(pEffect);
        }
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

    private void RemovePassives(Card c) {
        if (c.passiveEffects == null) return;
        foreach (PassiveEffect pEffect in c.passiveEffects) {
            foreach (Card affectedCard in allCardsInPlay) {
                if (affectedCard.grantedPassives.Contains(pEffect)) {
                    affectedCard.grantedPassives.Remove(pEffect);
                }
            }
        }
    }

    public bool QualifyCard(Card c, Qualifier q) {
        if (q.conditions != null) {
            if (q.conditions.Any(condition => !condition.Verify(this, GetControllerOf(c)))) {
                return false;
            }
        }
        // check if it already has the passive you are qualifying for (no need to grant or apply it if so)
        if (q.passive != null) {
            if (c.grantedPassives.Contains(q.passive)) return false;
        }
        // self effecting only
        if (q.self && !c.Equals(q.sourceCard)) return false;
        // doesn't effect self (other)
        if (q.other && c.Equals(q.sourceCard)) return false;
        // tribe check
        if (q.tribe != null && c.tribe != q.tribe) return false;
        // cardtype check
        if (q.cardType != null && c.type != q.cardType) return false;
        // verify restrictions
        if (q.restrictions != null) {
            if (q.restrictions.Any(restriction => !QualifyRestriction(c, restriction, q.sourcePlayer))) return false;
        }

        // tokentype check
        if (q.tokenType != null) {
            if (c is not Token t) return false;
            if (q.tokenType != t.tokenType) return false;
        }

        // card qualifies
        return true;
    }

    private bool QualifyTarget(int uid, TargetType targetType) {
        bool targetIsPlayer = playerOne.uid == uid || playerTwo.uid == uid;
        switch (targetType) {
            case TargetType.Player:
                return targetIsPlayer;
            case TargetType.Any:
                return targetIsPlayer || GetAllSummonsInPlay().Contains(cardByUid[uid]);
            case TargetType.Token:
                if (targetIsPlayer) return false;
                List<Token> tempTokenList = playerOne.tokens.Concat(playerTwo.tokens).ToList();
                return tempTokenList.Contains(cardByUid[uid]);
            case TargetType.Summon:
                return !targetIsPlayer && GetAllSummonsInPlay().Contains(cardByUid[uid]);
            default:
                throw new Exception("TargetType not implemented/unknown (QualifyTarget)");
        }
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
        // check for triggers for the player in question (second param)
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
                if (player == GetOwnerOf(tc.card))
                    AddToControlledTriggers(player, GetTriggersInCard(tc, player, tc.card));
                break;
            case Trigger.EnteredZone:
                Debug.Assert(tc.card != null, "there is no card associated with this EnteredZone trigger");
                AddToControlledTriggers(player, GetTriggers(tc, player));
                if(tc.zone != Zone.Play && player == GetOwnerOf(tc.card))
                    AddToControlledTriggers(player, GetTriggersInCard(tc, player, tc.card));
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
        // triggers in play
        foreach (Card c in player.playField) {
            List<TriggeredEffect> tempTriggeredEffects = GetTriggersInCard(tc, player, c);
            foreach (TriggeredEffect tEffect in tempTriggeredEffects) {
                newTEffectList.Add(tEffect);
            }
        }

        // triggers in hand
        foreach (Card c in player.hand) {
            List<TriggeredEffect> tempTriggeredEffects = GetTriggersInCard(tc, player, c);
            foreach (TriggeredEffect tEffect in tempTriggeredEffects) {
                newTEffectList.Add(tEffect);
            }
        }
        
        // event triggers
        List<TriggeredEffect> tempEventTriggers = player.eventTriggers.ToList();
        foreach (TriggeredEffect tEffect in tempEventTriggers) {
            if (QualifyTrigger(tc, player, tEffect)) {
                newTEffectList.Add(tEffect);
                player.eventTriggers.Remove(tEffect);
            }
        }

        return newTEffectList;
    }

    private bool QualifyTrigger(TriggerContext tc, Player player, TriggeredEffect tEffect) {
        // additional costs payable check
        if (!tEffect.CostsArePayable(this, player)) return false;
        if (!CheckTriggerConditions(player, tc.trigger, tEffect, tc.zone)) return false;
        // Phase check
        if (tEffect.phase != null && currentPhase != tEffect.phase) return false;
        // Player turn check
        if (tEffect.isPlayerTurn == true && player != GetPlayerByTurn(true)) return false;
        // Opponent turn check
        if (tEffect.isPlayerTurn == false && player == GetPlayerByTurn(true)) return false;
        return true;
    }
    
    private List<TriggeredEffect> GetTriggersInCard(TriggerContext tc, Player player, Card c) {
        List<TriggeredEffect> newTEffectList = new();
        Debug.Assert(c != null, "There is no card associated with this trigger");
        if (c.triggeredEffects == null) return newTEffectList;
        foreach (TriggeredEffect tEffect in c.triggeredEffects) {
            // set the source cards for all effects and sub-effects 
            Qualifier tQualifier = new Qualifier(tEffect, player);
            if(!QualifyTrigger(tc, player, tEffect)) continue;
            // handTrigger check
            if (c.currentZone != tEffect.triggerZone) continue;
            if (tc.card != null) {
                if (tc.card.Equals(c)) {
                    if (!tEffect.self) continue;
                    if (!QualifyCard(c, tQualifier)) continue;
                } else {
                    if (tEffect.self) continue;
                    if (!QualifyCard(tc.card, tQualifier)) continue;
                }
            }

            if (tc.cards != null) {
                if (tEffect.self && !tc.cards.Contains(c)) continue;
                if (GetQualifiedCards(tc.cards, tQualifier).Count == 0) continue;
            }

            // Add tEffect to list.
            newTEffectList.Add(tEffect);
        }


        return newTEffectList;
    }

    private bool CheckTriggerConditions(Player player, Trigger triggerType, TriggeredEffect tEffect, Zone? zone) {
        if (tEffect.trigger != triggerType) return false;
        if (zone != null && tEffect.zone != zone) return false;
        if (tEffect.conditions != null) {
            foreach (Condition condition in tEffect.conditions) {
                if (!condition.Verify(this, player)) return false;
            }

            return true;
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
        // optional triggers
        if (optionalTriggers.Count > 0) {
            if (currentChoiceIndex == 1) {
                player.controlledTriggers.Remove(optionalTriggers.First());
                optionalTriggers.Remove(optionalTriggers.First());
            } else {
                optionalTriggers.Remove(optionalTriggers.First());
            }

            Debug.Assert(currentPlayerToPassTo != null, "there is no currentPlayerToPassTo");
            if (optionalTriggers.Count == 0)
                HandleTriggers(player, currentPlayerToPassTo, TriggerStage.AdditionalCosts);
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
        // set the target choice
        KeyValuePair<List<Effect>, Effect> pair = choiceEffectDict.First();
        Debug.Assert(pair.Value.choices != null,
            "there are no choices associated with the choice effect (MakeChoice)");
        // get the index of the choice effect associated with it's corresponding triggered effect
        int insertIndex = pair.Key.IndexOf(pair.Value);
        // replace the choice effect with the effects with that choice  
        pair.Key.RemoveAt(insertIndex);
        pair.Key.InsertRange(insertIndex, pair.Value.choices[currentChoiceIndex]);
        // finish by removing it from the current choices dictionary
        choiceEffectDict.Remove(pair.Key);
        Debug.Assert(currentPlayerToPassTo != null, "there is no currentPlayerToPassTo");
        if (choiceEffectDict.Count != 0) return;
        if (choiceCard != null) {
            Debug.Assert(pair.Value.choiceIndex != null, "there is no choiceIndex for this Effect");
            choiceCard.chosenIndices.Add((int)pair.Value.choiceIndex, currentChoiceIndex);
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
        currentPlayerToPassTo = playerToPassTo;
        switch (stage) {
            case TriggerStage.Initial:
                // Trigger Count: 0
                if (player.controlledTriggers.Count <= 0) {
                    // checks trigger for next player or passes priority
                    FinishWithTriggers(player, playerToPassTo);
                    return;
                }
                // wait for response if optionals exist
                if (CheckForOptionalTriggers(player)) return;
                goto case TriggerStage.AdditionalCosts;
            case TriggerStage.AdditionalCosts:
                if (CheckForAdditionalCosts(player)) return;
                goto case TriggerStage.Choices;
            case TriggerStage.Choices:
                if (CheckForChoicesTriggers(player)) return;
                goto case TriggerStage.TargetSelection;
            case TriggerStage.TargetSelection:
                // Wait for target selection if needed
                if (CheckForTargetSelectionTriggers(player)) return;
                goto case TriggerStage.Ordering;
            case TriggerStage.Ordering:
                switch (player.controlledTriggers.Count) {
                    case 0:
                        FinishWithTriggers(player, playerToPassTo);
                        return;
                    case 1:
                        // adds the single triggered effect to the stack
                        TriggeredEffect tEffect = player.controlledTriggers[0];
                        player.controlledTriggers.Clear();
                        AddStackObjToStack(CreateStackObj(player, tEffect.sourceCard, tEffect));
                        FinishWithTriggers(player, playerToPassTo);
                        return;
                    case > 1:
                        // waits for player to select the order
                        CreateAndAddOrderingEvent(player);
                        return;
                }

                Console.WriteLine("Error in handling triggers: there are an impossible number of triggers");
                break;
        }
    }
    

    private bool CheckForOptionalTriggers(Player player) {
        foreach (TriggeredEffect tEffect in player.controlledTriggers) {
            if (tEffect.optional) {
                HandleOptionalEffect(player, tEffect);
                optionalTriggers.Add(tEffect);
            }
        }

        return optionalTriggers.Count > 0;
    }

    private bool CheckForAdditionalCosts(Player player) {
        foreach (TriggeredEffect tEffect in player.controlledTriggers) {
            if(tEffect.additionalCosts == null) continue;
            foreach (AdditionalCost aCost in tEffect.additionalCosts) {
                AddCostEvent(player, null, aCost);
                if(!triggersWithCosts.Contains(tEffect)) triggersWithCosts.Add(tEffect);
            }
        }
        return triggersWithCosts.Count > 0;
    }

    private bool CheckCardForAdditionalCosts(Player player, Card focusCard) {
        if(focusCard.additionalCosts == null) return false;
        foreach (AdditionalCost aCost in focusCard.additionalCosts) {
            AddCostEvent(player, null, aCost);
            cardAdditionalCostAmount++;
        }
        return cardAdditionalCostAmount > 0;
    }
    private bool CheckForChoicesTriggers(Player player) {
        foreach (TriggeredEffect tEffect in player.controlledTriggers) {
            foreach (var e in tEffect.effects.Where(e => e.effect == EffectType.Choose)) {
                Debug.Assert(e.choices != null, "there are no choices for this choose effect");
                HandleChoice(e.choices, player);
                choiceEffects.Add(tEffect.effects, e);
                choiceCard = null;
            }
        }

        return choiceEffects.Count > 0;
    }

    private bool CheckForChoicesCard(Player player, Card card) {
        if (card.stackEffects == null) return false;
        foreach (Effect effect in card.stackEffects.Where(e => e.effect == EffectType.Choose)) {
            if (effect.conditions != null && !effect.ConditionsAreMet(this, GetControllerOf(card))) continue;
            Debug.Assert(effect.choices != null, "there are no choices for this choose effect");
            Debug.Assert(effect.choiceIndex != null, "there is no choice index for this choose effect");
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


    private void HandleChoice(List<List<Effect>> choices, Player player) {
        List<string> choicesText = new();
        foreach (List<Effect> effectList in choices) {
            List<string> effectStrings = new();
            foreach (Effect e in effectList) {
                effectStrings.Add(e.EffectToString(this));
            }

            choicesText.Add(String.Join(" ", effectStrings));
        }

        GameEvent gEvent = GameEvent.CreateOptionEvent(new PlayerChoice(choicesText, "Choose:"));
        AddEventForPlayer(player, gEvent);
    }

    private bool CheckForXCost(Player player, Card card) {
        if (!card.HasXCost()) return false;
        cardWaitingForX = card;
        var gEvent = GameEvent.CreateAmountSelectionEvent(true);
        AddEventForPlayer(player, gEvent);
        return true;
    }
    
    public void SetX(Player player, int xAmount) {
        Debug.Assert(cardWaitingForX != null, "there's no card waiting for an x amount (SetX)");
        cardWaitingForX.x = player.spellBurnt ? xAmount * 2 : xAmount;
        AttemptToCast(player, cardWaitingForX, CastingStage.TargetSelection);
        cardWaitingForX = null;
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
            foreach (Effect effect in tEffect.effects) {
                HandleEffectTargetSelection(player, effect);
            }
        }

        return effectsWithTargets.Count > 0;
    }

    private void HandleEffectTargetSelection(Player player, Effect effect) {
        int targetAmount = effect.amount ?? 1;
        // invert if to add more logic
        if (effect.targetType == null) return;
        CreateAndAddNewTargetSelectionEvent(player, GetPossibleTargets(player, effect), targetAmount);
        effectsWithTargets.Add(effect);
    }

    private void CreateAndAddNewTargetSelectionEvent(Player player, List<int> targetableUids, int amount) {
        TargetSelection newTargetSelection = new TargetSelection(targetableUids, amount);
        GameEvent gEvent = GameEvent.CreateTargetSelectionEvent(newTargetSelection);
        AddEventForPlayer(player, gEvent);
    }

    public List<int> GetPossibleTargets(Player player, Effect effect) {
        Debug.Assert(effect.targetType != null, "There is no effect TargetType (GetPossibleTargets)");
        List<int> allUids = allCardsInPlay.Select(c => c.uid).ToList();
        allUids.Add(playerOne.uid);
        allUids.Add(playerTwo.uid);
        return allUids.Where(uid => QualifyTarget(uid, (TargetType)effect.targetType)).ToList();
    }

    public int GetAmountBasedOn(AmountBasedOn? amountBasedOn, bool other = false, Player? player = null, Effect? rootEffect = null, CardType? cardType = null,
        List<Restriction>? restrictions = null, Card? sourceCard = null) {
        int modAmount = 0;
        if (other) modAmount = -1;
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
            default:
                Console.WriteLine("Unknown AmountBasedOn value: " + amountBasedOn);
                return -69;
        }

        return tempAmount; 
    }

    private List<int> GetAttackCapableUids(Player player) {
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
        // if you only checked one player
        if (player == GetPlayerByTurn(true)) {
            HandleTriggers(GetPlayerByTurn(false), playerToPassTo);
        } else {
            // this runs once you've checked both players for triggers
            // reset trigger lists when done with triggers. player to pass to is determined in CheckForTriggersAndPassives
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
        prioPlayerId = player.playerId;
        CalculatePossibleMoves(player);
        GameEvent gEvent = new GameEvent(EventType.GainPrio);
        AddEventForPlayer(player, gEvent);
    }

    private StackObj CreateStackObj(Player player, Card stackObjCard, TriggeredEffect? triggeredEffect = null,
        ActivatedEffect? aEffect = null) {
        List<Effect> effectsList = new();
        if (triggeredEffect != null) {
            foreach (Effect e in triggeredEffect.effects) {
                effectsList.Add(Effect.CreateEffect(e, stackObjCard));
            }

            return new StackObj(stackObjCard, StackObjType.TriggeredEffect, effectsList, stackObjCard.currentZone,
                player, triggeredEffect.description);
        }

        if (aEffect != null) {
            foreach (Effect e in aEffect.effects) {
                effectsList.Add(Effect.CreateEffect(e, stackObjCard));
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
        stack.Push(stackObj);
        GameEvent gEvent = GameEvent.CreateStackEvent(EventType.Trigger, new StackDisplayData(stackObj, this));
        AddEventForBothPlayers(stackObj.player, gEvent);
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
                goto case CastingStage.AdditionalCosts;
            case CastingStage.AdditionalCosts:
                if (CheckCardForAdditionalCosts(attemptingPlayer, card)) return;
                goto case CastingStage.AmountSelection;
            case CastingStage.AmountSelection:
                if (CheckForXCost(attemptingPlayer, card)) return;
                goto case CastingStage.Choices; 
            case CastingStage.Choices:
                if (CheckForChoicesCard(attemptingPlayer, card)) return;
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
                goto case CastingStage.TributeSelection;
            case CastingStage.TributeSelection:
                Debug.Assert(cardBeingCast != null, "there is no card being cast for AttemptToCast()");
                // activate tribute requirements for summons
                if (cardBeingCast.type == CardType.Summon && cardBeingCast.GetCost() > 0) {
                    cardRequiringTribute = cardBeingCast;
                    List<int> tributeableUids = new();
                    // check for tribute restrictions
                    foreach (Card c in attemptingPlayer.playField) {
                        if (CardCanTributeTo(c, cardBeingCast)) tributeableUids.Add(c.uid);
                    }

                    GameEvent gEvent =
                        GameEvent.CreateTributeRequirementEvent(new CardDisplayData(cardBeingCast), tributeableUids);
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
                AddCostEvent(attemptingPlayer, aEffect);
                return;
            case ActivationStage.TargetSelection:
                // if there are any effects requiring targets, handle target selection for all effects
                if (aEffect.effects.Any(effect => effect.targetType != null)) {
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
        PassPrioToPlayer(GetPlayerByTurn(true));
    }

    private void AddCostEvent(Player attemptingPlayer, ActivatedEffect? aEffect = null, AdditionalCost? aCost = null) {
        Qualifier effectQualifier;
        CostContext cc;
        if (aEffect != null) {
            cc = new CostContext(aEffect);
            effectQualifier = new Qualifier(aEffect, attemptingPlayer);
        } else {
            cc = new CostContext(aCost!);
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
        foreach (int uid in tributeUids) {
            Kill(cardByUid[uid]);
        }

        Debug.Assert(cardRequiringTribute != null, "there is no card requiring tribute");
        CastCard(tributingPlayer, cardRequiringTribute);
    }


    private void CastCard(Player player, Card card, bool isAction = true) {
        cardBeingCast = null;
        switch (card.type) {
            // increment total spells
            case CardType.Spell:
                player.totalSpells++;
                break;
            // increment turnSummonCount
            case CardType.Summon when
                !DetectPassive(card, Passive.BypassSummonLimit):
                player.turnSummonCount++;
                break;
        }

        player.playables.Remove(card);
        player.allCardsPlayer.Remove(card);
        RemoveFromHand(player, card);
        StackObj newStackObj = CreateStackObj(player, card);
        stack.Push(newStackObj);
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
            // resolve stack object if both players pass
            if (stack.Count > 0) {
                StackObj tempStackObj = stack.Peek();
                stack.Pop();
                // this ensures no one gains priority while we wait for a user input for the stackObj
                prioPlayerId = -1;
                // ResolveStackObj handles passing priority ONLY AFTER ALL USER INPUT IS FINISHED for that stackObj
                tempStackObj.ResolveStackObj(this);
                secondPass = false;
                return;
            }

            // resolve attacks if both players pass
            if (currentAttackUids.Count > 0) {
                ResolveAttacks();
                // might need to check for triggers and return if an attack is not on combat phase 
                // we don't want pass the phase if an attack is from a trigger
            }

            secondPass = false;
            GoToNextPhase();
        } else {
            secondPass = true;
            PassPrioToPlayer(GetPlayerByPrio(false));
        }
    }

    private void ResolveAttacks() {
        List<Card> cardsThatSurvivedCombat = new();
        foreach (var pair in currentAttackUids) {
            Card attackingCard = cardByUid[pair.Key];
            // set combat damage values
            int attackValue = attackingCard.GetAttack();
            int retaliationValue = cardByUid.TryGetValue(pair.Value, out var value) ? value.GetAttack() : 0;
            // create a combat event
            GameEvent gEvent = GameEvent.CreateCombatEvent(pair.Key, pair.Value, attackValue);
            AddEventForBothPlayers(GetPlayerByTurn(true), gEvent);
            // deal the damage
            DealDamage(pair.Value, attackValue);
            DealDamage(attackingCard.uid, retaliationValue);
            // check for SurvivedCombat triggers
            if (attackingCard.currentZone == Zone.Play && attackingCard.GetDefense() > 0) cardsThatSurvivedCombat.Add(attackingCard);
            // TODO check for damage triggers before continuing to the next combat event
        }
        triggersToCheck.Add(new TriggerContext(Trigger.SurvivedCombat, null, null, cardsThatSurvivedCombat));
        currentAttackUids.Clear();
    }

    private void CheckForDeaths() {
        // this also checks for player deaths (see below). This might need to be moved to a separate function
        foreach (var c in playerOne.playField) {
            if (c.defense == null) continue;
            if (c.GetDefense() <= 0) {
                Kill(c);
            }
        }
        foreach (var c in playerTwo.playField) {
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
        RemoveFromPlay(GetControllerOf(c), c);
        // add to owner's graveyard
        if (c is Token) {
            RemoveFromAllCardsPlayer(GetOwnerOf(c), c);
        } else {
            AddToGraveyard(GetOwnerOf(c), c);
            c.grantedPassives.Clear();
        }

        // remove from current attack if necessary
        if (c.type == CardType.Summon) {
            foreach (var pair in currentAttackUids.ToList()) {
                if (pair.Key == c.id || pair.Value == c.id) {
                    currentAttackUids.Remove(pair.Key);
                }
            }
        }
        

        GameEvent gEvent = GameEvent.CreateUidEvent(EventType.Death, c.uid);
        AddEventForBothPlayers(GetPlayerByTurn(true), gEvent);
        RemovePassives(c);
        triggersToCheck.Add(new TriggerContext(Trigger.Death, null, c));
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
                AddEventForBothPlayers(GetPlayerByTurn(true), gEvent);
                break;
            default:
                Console.WriteLine("you can't destroy that type of card -> match.Destroy()");
                break;
        }

    }

    public void Discard(Player player, Card c) {
        RemoveFromHand(player, c);
        AddToGraveyard(player, c);
        GameEvent gEvent = GameEvent.CreateCardEvent(EventType.Discard, new CardDisplayData(c));
        AddEventForBothPlayers(player, gEvent);
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
        if (currentPhase == Phase.Draw) Draw(GetPlayerByTurn(true), 1);
        CheckForTriggersAndPassives(EventType.NextPhase);
    }

    private void PassTurn() {
        HandleEndOfTurnPassives();
        // reset summoner counter
        GetPlayerByTurn(true).turnSummonCount = 0;
        // remove spellburn if not scorched
        RemoveSpellburn(GetPlayerByTurn(true));
        RemoveSpellburn(GetPlayerByTurn(false));
        // update phase
        currentPhase = Phase.Draw;
        // switch prio and turn ids
        turnPlayerId = GetPlayerByTurn(false).playerId;
        prioPlayerId = GetPlayerByTurn(true).playerId;
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

    private void RefreshCards(Player player, List<Card> cards, bool bothPlayers = true) {
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
        }
    }
    
    public void Mill(Player player, int amount) {
        for (int i = 0; i < amount; i++) {
            Debug.Assert(player.deck != null, "player.deck != null");
            Card topCard = player.deck[0];
            AddToGraveyard(player, topCard);
            AddCardToAllCardsPlayer(player, topCard);
            // add the drawing of this card to the event list after uid and other values are set
            GameEvent gEvent = GameEvent.CreateCardEvent(EventType.Mill, new CardDisplayData(topCard));
            AddEventForBothPlayers(player, gEvent);
            player.deck.RemoveAt(0);
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
                case DeckDestinationType.Top or DeckDestinationType.Bottom:
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

    private void RemoveFromHand(Player player, Card c) {
        player.hand.Remove(c);
        c.playerHandOf = null;
    }

    private void RemoveFromPlay(Player player, Card c) {
        player.playField.Remove(c);
        allCardsInPlay.Remove(c);
        triggersToCheck.Add(new TriggerContext(Trigger.LeftZone, Zone.Play, c));
    }

    private void AddToGraveyard(Player player, Card c) {
        player.graveyard.Add(c);
        AddCardToAllCardsPlayer(player, c);
        c.currentZone = Zone.Graveyard;
        triggersToCheck.Add(new TriggerContext(Trigger.EnteredZone, Zone.Graveyard, c));
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
        token.currentZone = Zone.Token;
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
        triggersToCheck.Add(new TriggerContext(Trigger.EnteredZone, Zone.Token, token));
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
    private void ShuffleDeck(List<Card> cardList) {
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
        Random rng = new Random();
        int randNum = rng.Next(1, 3);
        // this temporarily sets BangoJetty to go first always
        // turnPlayerId = 1;
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
        RemoveFromCurrentZone(targetCard);
        // add to the new zone
        switch (destination) {
            case Zone.Hand:
                AddToHand(targetPlayer, targetCard);
                break;
            case Zone.Play:
                AddToPlay(targetPlayer, targetCard);
                break;
            case Zone.Graveyard:
                AddToGraveyard(targetPlayer, targetCard);
                break;
            case Zone.Deck:
                Debug.Assert(deckDestination != null, "There is no deck destination for this SendToZone Event");
                targetPlayer.allCardsPlayer.Remove(targetCard);
                targetCard.currentZone = Zone.Deck;
                switch (deckDestination.deckDestination) {
                    case DeckDestinationType.Bottom:
                        targetPlayer.deck!.Insert(0, targetCard);
                        break;
                    case DeckDestinationType.Top:
                        targetPlayer.deck!.Add(targetCard);
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
        GameEvent gEvent = GameEvent.CreateZoneGameEvent(destination, null, sourceZone);
        // this was sent using a deck destination effect
        if (deckDestination != null) {
            switch (deckDestination.deckDestination) {
                // if it's going to the deck, send it without a CardDisplay (face-down) for both players
                case DeckDestinationType.Bottom or DeckDestinationType.Top:
                    AddEventForBothPlayers(targetPlayer, gEvent);
                    return;
                case DeckDestinationType.Hand when !deckDestination.reveal:
                    // use an event without the selected card for opponent (it wasn't revealed)
                    GameEvent playerEvent = new GameEvent(gEvent) { focusCard = new CardDisplayData(targetCard) };
                    gEvent.isOpponent = true;
                    AddEventForBothPlayers(targetPlayer, playerEvent, gEvent);
                    return;
            }

            // all other cases should reveal the card for both players
        }
        gEvent.focusCard = new CardDisplayData(targetCard);
        AddEventForBothPlayers(targetPlayer, gEvent);
    }

    private void RemoveFromCurrentZone(Card card) {
        switch (card.currentZone) {
            case Zone.Play:
                RemoveFromPlay(GetControllerOf(card), card);
                break;
            case Zone.Graveyard:
                GetOwnerOf(card).graveyard.Remove(card);
                break;
            case Zone.Deck:
                GetOwnerOf(card).deck!.Remove(card);
                break;
            default:
                Console.WriteLine("Unknown zone for RemoveFromCurrentZone: " + card.currentZone);
                break;
        }
    }

    public void DealDamage(int targetUid, int amount) {
        if (cardByUid.TryGetValue(targetUid, out var card)) {
            if (card.GetPassives().Any(p => p.passive == Passive.CantTakeDamage)) return;
            card.damageTaken += amount;
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
        Effect focusEffect = effectsWithTargets.Last();
        // assign targets for effects
        foreach (int uid in targetedUids) {
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

    public void HandleCostSelection(Player player, List<Card> selectedCards) {
        // activated effect
        if (currentActivatedEffect != null) {
            PayCost(player, currentActivatedEffect.costType, selectedCards);
            AttemptToActivate(GetControllerOf(currentActivatedEffect.sourceCard), currentActivatedEffect,
                ActivationStage.TargetSelection);
            return;
        }

        // triggers
        if (triggersWithCosts.Count > 0) {
            TriggeredEffect focusTrigger = triggersWithCosts.First();
            foreach (AdditionalCost aCost in focusTrigger.additionalCosts!) {
                if (aCost.isPaid) continue;
                PayCost(player, aCost.costType, selectedCards);
                aCost.isPaid = true;
                if (focusTrigger.additionalCosts.Last() == aCost) triggersWithCosts.Remove(focusTrigger);
                break;
            }

            if (triggersWithCosts.Count == 0) {
                Debug.Assert(currentPlayerToPassTo != null, "there is no current player to pass to");
                HandleTriggers(player, currentPlayerToPassTo, TriggerStage.Choices);
            }
        }

        // card casts
        if (cardAdditionalCostAmount > 0) {
            Debug.Assert(cardBeingCast != null, "there is no card being cast");
            Debug.Assert(cardBeingCast.additionalCosts != null, "card being cast has no additional costs");
            foreach (AdditionalCost aCost in cardBeingCast.additionalCosts) {
                if(aCost.isPaid) continue;
                PayCost(player, aCost.costType, selectedCards);
                aCost.isPaid = true;
                if(cardBeingCast.additionalCosts.Last() == aCost) AttemptToCast(player, cardBeingCast, CastingStage.AmountSelection);
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
        }
    }
    
    

    public void Reveal(Player affectedPlayer, Card subjectCard) {
        GameEvent gEvent = GameEvent.CreateCardEvent(EventType.Reveal, new CardDisplayData(subjectCard));
        AddEventForPlayer(GetOpponent(affectedPlayer), gEvent);
    }

    public Player PlayerByUid(int uid) {
        return playerOne.uid == uid ? playerOne : playerTwo;
    }

    public List<Card> GetAllSummonsInPlay() {
        List<Card> cards = playerOne.playField.ToList();
        cards.AddRange(playerTwo.playField);
        return cards;
    }
}
