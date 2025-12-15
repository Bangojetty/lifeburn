using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Server.CardProperties;

public class Effect {
    public EffectType effect;
    public List<Condition>? conditions;
    public bool mandatoryTarget;
    public bool futureProof;
    [JsonConverter(typeof(StringEnumConverter))]
    public Scope scope = Scope.All;  // Default: no self/other filter on targeting
    public int? amount;
    public int? amountToHand;
    public int? attack;
    public int? defense;
    public AmountBasedOn? attackBasedOn;
    public AmountBasedOn? defenseBasedOn;
    public bool reveal;
    public bool isOpponent;
    public bool optional;
    public bool isCost;  // If true, this effect is a cost - if it can't be paid, remaining effects fizzle
    public bool opponentsChoice;
    public string? optionMessage;
    public bool all;
    public bool allOfSameName;  // Also affects all cards with the same name as the target
    public bool eachPlayer;  // Each player makes their own selection (e.g., Return)
    public bool youChooseDamage;
    public bool canSeparate;
    public bool tokenAttacking;
    public bool sameZone;
    public int? minTargets;
    public int? maxTargets;
    public int? upTo;
    public bool attacking;
    public bool ownersControl;
    public List<PassiveEffect>? passives;
    public bool thisTurn;
    public string? duration;
    public string? owner;
    public string? revealType;
    public Keyword? keyword;
    public List<Keyword>? keywords;
    public int? keywordAmount;
    public CardType? cardType;
    public Tribe? tribe;
    public Zone? zone;
    public Zone? destination;
    public TokenType? tokenType;
    public List<Zone>? targetZones;
    public PassiveEffect? tokenPassive;
    public List<DeckDestination>? deckDestinations;
    public DeckDestinationType? deckDestination;  // simpler single destination (top/bottom)
    public TargetType? targetType;
    public string? counterType;
    public TargetBasedOn? targetBasedOn;
    public string? playerBasedOn;
    public AmountBasedOn? amountBasedOn;
    public CardType? basedOnType;
    public Tribe? basedOnTribe;
    public string? amountModifier;
    public List<Condition>? modifierConditions;
    public CostType? repeatCostType;
    public int? repeatCostAmount;
    public bool repeatSameTarget;
    public List<Restriction>? restrictions;
    public int? restrictionMax;
    public int? restrictionMin;
    public string? player;
    public List<TriggeredEffect> triggeredEffects;
    public List<Effect>? additionalEffects;
    public List<List<Effect>>? choices;
    public int? choiceIndex;
    public string? description;
    public bool resolveTarget;  // if true, target selection happens during resolution, not during cast
    public bool random;  // if true, selection is random (e.g., discard at random)
    public Passive? replacementPassive;  // for ReplacementEffect: the passive to add to the player

    // non-json
    public List<int> targetUids = new();
    public Card? sourceCard;
    public int? subjectUid;
    public Effect? rootEffect;
    public List<int>? affectedUids;
    public List<Effect>? parentEffectList;  // For RepeatAllEffects to know what to repeat

    public Effect(EffectType effect) {
        this.effect = effect;
    }

    public Effect Clone() {
        return new Effect(effect) {
            conditions = conditions?.ToList(),
            mandatoryTarget = mandatoryTarget,
            futureProof = futureProof,
            scope = scope,
            amount = amount,
            amountToHand = amountToHand,
            attack = attack,
            defense = defense,
            attackBasedOn = attackBasedOn,
            defenseBasedOn = defenseBasedOn,
            reveal = reveal,
            isOpponent = isOpponent,
            optional = optional,
            isCost = isCost,
            opponentsChoice = opponentsChoice,
            optionMessage = optionMessage,
            all = all,
            allOfSameName = allOfSameName,
            eachPlayer = eachPlayer,
            youChooseDamage = youChooseDamage,
            canSeparate = canSeparate,
            tokenAttacking = tokenAttacking,
            sameZone = sameZone,
            minTargets = minTargets,
            maxTargets = maxTargets,
            upTo = upTo,
            attacking = attacking,
            ownersControl = ownersControl,
            passives = passives?.ToList(),
            thisTurn = thisTurn,
            duration = duration,
            owner = owner,
            revealType = revealType,
            keyword = keyword,
            keywords = keywords?.ToList(),
            keywordAmount = keywordAmount,
            cardType = cardType,
            tribe = tribe,
            zone = zone,
            destination = destination,
            tokenType = tokenType,
            targetZones = targetZones?.ToList(),
            tokenPassive = tokenPassive,
            deckDestinations = deckDestinations?.ToList(),
            deckDestination = deckDestination,
            targetType = targetType,
            counterType = counterType,
            targetBasedOn = targetBasedOn,
            playerBasedOn = playerBasedOn,
            amountBasedOn = amountBasedOn,
            basedOnType = basedOnType,
            basedOnTribe = basedOnTribe,
            amountModifier = amountModifier,
            modifierConditions = modifierConditions?.ToList(),
            repeatCostType = repeatCostType,
            repeatCostAmount = repeatCostAmount,
            repeatSameTarget = repeatSameTarget,
            restrictions = restrictions?.ToList(),
            restrictionMax = restrictionMax,
            restrictionMin = restrictionMin,
            player = player,
            triggeredEffects = triggeredEffects,
            additionalEffects = additionalEffects?.Select(e => e.Clone()).ToList(),
            choices = choices,
            choiceIndex = choiceIndex,
            description = description,
            resolveTarget = resolveTarget,
            random = random,
            replacementPassive = replacementPassive,
            // Non-json fields - copy targetUids to preserve targets selected during casting
            targetUids = targetUids != null ? new List<int>(targetUids) : new List<int>(),
            sourceCard = sourceCard,
            subjectUid = subjectUid,
            rootEffect = rootEffect,
            affectedUids = affectedUids != null ? new List<int>(affectedUids) : null
        };
    }

    public static Effect CreateEffect(Effect e, Card sourceCard) {
        // Clone the effect to avoid accumulating state across multiple activations
        Effect cloned = e.Clone();
        cloned.subjectUid = sourceCard.uid;
        cloned.sourceCard = sourceCard;
        cloned.amount ??= 1;
        if (cloned.additionalEffects != null) {
            foreach (Effect addEffect in cloned.additionalEffects) {
                addEffect.rootEffect = cloned;
                addEffect.sourceCard = sourceCard;
            }
        }
        return cloned;
    }

    public bool ConditionsAreMet(GameMatch gameMatch, Player controllingPlayer) {
        Card? rootTarget = null;
        if (rootEffect != null && rootEffect.targetUids.Count > 0) {
               rootTarget = gameMatch.cardByUid[rootEffect.targetUids[0]];
        }
        return conditions == null || conditions.All(condition => condition.Verify(gameMatch, controllingPlayer, rootTarget));
    }

    /// <summary>
    /// For isCost effects: checks if the cost can be paid at all.
    /// Returns false if the cost cannot be paid (causing remaining effects to fizzle).
    /// </summary>
    public bool CanPayCost(GameMatch gameMatch, Player player) {
        if (!isCost) return true;  // Non-cost effects are always "payable"

        switch (effect) {
            case EffectType.Reveal:
                // Reveal self is always payable if sourceCard exists
                if (scope == Scope.SelfOnly) return sourceCard != null;
                return true;
            case EffectType.Sacrifice:
                if (scope == Scope.SelfOnly) return sourceCard != null && sourceCard.currentZone == Zone.Play;
                if (tokenType != null) {
                    // Need at least one matching token
                    return player.tokens.Any(t => t.tokenType == tokenType);
                }
                return true;
            default:
                return true;
        }
    }

    /// <summary>
    /// For isCost effects: checks if user selection is needed to pay the cost.
    /// Returns false if the cost can be auto-paid (e.g., reveal self, sacrifice self, only one valid target).
    /// </summary>
    public bool NeedsCostSelection(GameMatch gameMatch, Player player) {
        if (!isCost) return false;

        switch (effect) {
            case EffectType.Reveal:
                // Reveal self is auto-pay
                return scope != Scope.SelfOnly;
            case EffectType.Sacrifice:
                // Sacrifice self is auto-pay
                if (scope == Scope.SelfOnly) return false;
                if (tokenType != null) {
                    // If exactly one matching token, auto-sacrifice it
                    int matchingTokens = player.tokens.Count(t => t.tokenType == tokenType);
                    return matchingTokens > 1;  // Only need selection if more than one option
                }
                return true;  // Other sacrifice types need selection
            default:
                return false;
        }
    }

    /// <summary>
    /// For isCost effects: gets the list of selectable UIDs for paying the cost.
    /// </summary>
    public List<int> GetCostSelectableUids(GameMatch gameMatch, Player player) {
        List<int> selectableUids = new();

        switch (effect) {
            case EffectType.Sacrifice:
                if (tokenType != null) {
                    foreach (Token t in player.tokens) {
                        if (t.tokenType == tokenType) selectableUids.Add(t.uid);
                    }
                }
                break;
        }

        return selectableUids;
    }

    public void Resolve(GameMatch gameMatch, Player effectOwner, int? rootSubjectUid = null) {
        if (!ConditionsAreMet(gameMatch, effectOwner)) return;
        if (rootSubjectUid != null) subjectUid = (int)rootSubjectUid;
        Player sourcePlayer = isOpponent ? gameMatch.GetOpponent(effectOwner) : effectOwner;
        // set player affected based on targeting or "player" string
        Player affectedPlayer;
        // If we have targetUids and the target is a player, use that player
        if (targetUids.Count > 0 && gameMatch.IsPlayerUid(targetUids[0])) {
            affectedPlayer = gameMatch.PlayerByUid(targetUids[0]);
        } else {
            // Fallback to "player" field for some cards (e.g. Earthquake Golem)
            switch (player) {
                case "opponent":
                    affectedPlayer = gameMatch.GetOpponent(effectOwner);
                    break;
                default:
                    affectedPlayer = sourcePlayer;
                    break;
            }
        }
        // set amounts based on game state at the time of resolve
        // Skip PlayerChoice - the amount was already set during selection in HandleCostSelection
        if (amountBasedOn != null && amountBasedOn != AmountBasedOn.PlayerChoice) {
            amount = gameMatch.GetAmountBasedOn(amountBasedOn, scope, sourcePlayer, rootEffect, cardType, restrictions, sourceCard);
        }
        if (attackBasedOn != null) {
            attack = gameMatch.GetAmountBasedOn(attackBasedOn, scope, sourcePlayer, rootEffect, cardType, restrictions, sourceCard);
        }
        if (defenseBasedOn != null) {
            defense = gameMatch.GetAmountBasedOn(defenseBasedOn, scope, sourcePlayer, rootEffect, cardType, restrictions, sourceCard);
        }

        if (amountModifier != null) {
            // modifierConditions check caster (effectOwner), not the target
            if (modifierConditions == null || modifierConditions.All(condition => condition.Verify(gameMatch, effectOwner))) {
                Debug.Assert(amount != null, "there is no amount to modify");
                switch (amountModifier) {
                    case "/2down":
                        amount = (int)Math.Floor((int)amount / 2f);
                        break;
                    case "+1":
                        amount++;
                        break;
                    case "-2":
                        amount -= 2;
                        if (amount < 0) amount = 0;  // Don't go negative
                        break;
                    case "x2":
                        amount *= 2;
                        break;
                }
            }
        }
        // set default qualifier for qualifing cards for the effect based on their properties (CardType, Tribe, etc.)
        Qualifier eQualifier = new Qualifier(this, sourcePlayer);
        
        // resolve effect based on EffectType
        switch (effect) {
            case EffectType.CreateToken:
                CreateToken(gameMatch, affectedPlayer);
                break;
            case EffectType.Draw:
                Draw(gameMatch, affectedPlayer);
                break;
            case EffectType.Discard:
                affectedUids = new List<int>();
                if (targetUids.Count > 0) {
                    // Player-chosen discard (targetUids set by selection in StackObj)
                    foreach (int uid in targetUids) {
                        if (gameMatch.cardByUid.TryGetValue(uid, out Card? cardToDiscard)) {
                            gameMatch.Discard(affectedPlayer, cardToDiscard);
                            affectedUids.Add(uid);
                        }
                    }
                } else if (random) {
                    // Discard at random
                    Debug.Assert(amount != null, "Random discard effect requires an amount");
                    List<Card> hand = affectedPlayer.hand.ToList();
                    for (int i = 0; i < amount && hand.Count > 0; i++) {
                        int randomIndex = new Random().Next(hand.Count);
                        Card cardToDiscard = hand[randomIndex];
                        gameMatch.Discard(affectedPlayer, cardToDiscard);
                        affectedUids.Add(cardToDiscard.uid);
                        hand.RemoveAt(randomIndex);
                    }
                }
                // Note: Fixed-amount non-random discard is handled by StackObj before Resolve is called
                // If we get here with amount > 0 and no targetUids, the player had no cards to select
                break;
            case EffectType.LookAtDeck:
                LookAtDeck(gameMatch, affectedPlayer);
                break;
            case EffectType.Tutor:
                Tutor(gameMatch, affectedPlayer);
                break;
            case EffectType.Mill:
                Mill(gameMatch, affectedPlayer);
                break;
            case EffectType.SendToZone:
                affectedUids = SendToZone(gameMatch, affectedPlayer);
                break;
            case EffectType.GainLife:
                gameMatch.GainLife(affectedPlayer, amount);
                break;
            case EffectType.GainControl:
                foreach (int uid in targetUids) {
                    Card targetCard = gameMatch.cardByUid[uid];
                    gameMatch.GainControl(affectedPlayer, targetCard);
                }
                break;
            case EffectType.SetLifeTotal:
                gameMatch.SetLifeTotal(affectedPlayer, amount);
                break;
            case EffectType.GrantKeyword:
                affectedUids = GrantKeyword(gameMatch, affectedPlayer);
                break;
            case EffectType.Reveal:
                affectedUids = new List<int>();
                if (all) {
                    foreach (Card c in affectedPlayer.hand) {
                        gameMatch.Reveal(affectedPlayer, c);
                        affectedUids.Add(c.uid);
                    }
                } else if (scope == Scope.SelfOnly) {
                    gameMatch.Reveal(affectedPlayer, gameMatch.cardByUid[(int)subjectUid!]);
                } else if (targetUids.Count > 0) {
                    // Reveal specific targeted cards (e.g., from CardInHand target selection)
                    foreach (int uid in targetUids) {
                        gameMatch.Reveal(affectedPlayer, gameMatch.cardByUid[uid]);
                        affectedUids.Add(uid);
                    }
                } else {
                    foreach (Card c in gameMatch.GetQualifiedCards(affectedPlayer.hand, eQualifier)) {
                        gameMatch.Reveal(affectedPlayer, c);
                        affectedUids.Add(c.uid);
                    }
                }
                break;
            case EffectType.LoseLife:
                gameMatch.LoseLife(affectedPlayer, amount);
                break;
            case EffectType.DealDamage:
                Debug.Assert(amount != null, "there is no amount for this deal damage effect");
                affectedUids = new List<int>();
                if (targetUids.Count > 0) {
                    foreach (int uid in targetUids) {
                        gameMatch.DealDamage(uid, (int)amount, isSpellDamage: true);
                        affectedUids.Add(uid);
                    }
                } else if (all) {
                    // Deal damage to all targets matching targetType/cardType
                    if (targetType == TargetType.Summon || cardType is CardType.Summon) {
                        foreach (Card c in gameMatch.GetQualifiedCards(gameMatch.GetAllSummonsInPlay(), eQualifier)) {
                            gameMatch.DealDamage(c.uid, (int)amount, isSpellDamage: true);
                            affectedUids.Add(c.uid);
                        }
                    }
                }
                break;
            case EffectType.Destroy:
                Console.WriteLine($"Destroy Effect: targetUids.Count={targetUids.Count}");
                affectedUids = new List<int>();
                if (targetUids.Count > 0) {
                    foreach (int uid in targetUids) {
                        Console.WriteLine($"  Destroying uid={uid}, exists in cardByUid={gameMatch.cardByUid.ContainsKey(uid)}");
                        if (gameMatch.cardByUid.ContainsKey(uid)) {
                            Card target = gameMatch.cardByUid[uid];
                            Console.WriteLine($"  Target card: name={target.name}, type={target.type}, zone={target.currentZone}");
                            // Fizzle if target is no longer in play (already destroyed, bounced, etc.)
                            if (target.currentZone != Zone.Play) {
                                Console.WriteLine($"  Target not in play (zone={target.currentZone}), fizzling");
                                continue;
                            }
                            gameMatch.Destroy(target);
                            affectedUids.Add(uid);
                        } else {
                            Console.WriteLine($"  ERROR: uid {uid} not found in cardByUid!");
                        }
                    }
                } else {
                    Console.WriteLine("  No targetUids - destroy all not implemented");
                    // TODO implement destroy all (e.g. Wrath) and card types
                }
                break;
            case EffectType.GrantPassive:
                Debug.Assert(passives != null, "there are no passive for this GrantPassive effect");
                Console.WriteLine($"GrantPassive: sourceCard={sourceCard?.name ?? "NULL"}, scope={scope}, targetType={targetType}, targetUids.Count={targetUids.Count}");
                affectedUids = new List<int>();
                if (targetUids.Count > 0) {
                    // Apply to specific targets
                    foreach (int uid in targetUids) {
                        GrantPassive(gameMatch.cardByUid[uid]);
                        affectedUids.Add(uid);
                    }
                } else if (targetType != null && !all) {
                    // Effect required a target but had none - fizzle (do nothing)
                    Console.WriteLine($"  GrantPassive fizzled - targetType={targetType} but no targets selected");
                } else {
                    // Apply to all qualified summons (for "all" effects or effects without targetType)
                    var allSummons = gameMatch.GetAllSummonsInPlay();
                    Console.WriteLine($"  All summons in play: {string.Join(", ", allSummons.Select(s => s.name))}");
                    foreach (Card c in gameMatch.GetQualifiedCards(allSummons, eQualifier)) {
                        Console.WriteLine($"  Granting passive to: {c.name} (uid={c.uid})");
                        GrantPassive(c);
                        affectedUids.Add(c.uid);
                    }
                }
                gameMatch.CheckForPassives();
                break;
            case EffectType.Choose:
                // This code should never run. You should always make your choices before trying to resolve the effect
                // once you've made a choice, this effect will be replaced by the chosen effect(s)
                Debug.Assert(choices != null, "Error: Choice Effect not set");
                break;
            case EffectType.CastCard:
                if (scope == Scope.SelfOnly) {
                    Debug.Assert(sourceCard != null, "there's no sourceCard for this CastCard Effect");
                    gameMatch.AttemptToCast(affectedPlayer, sourceCard, CastingStage.Initial, false);
                } else if (targetUids.Count > 0) {
                    // Put the targeted card(s) directly into play (free summon from hand)
                    foreach (int targetUid in targetUids) {
                        Card targetCard = gameMatch.cardByUid[targetUid];
                        gameMatch.SendToZone(affectedPlayer, Zone.Play, targetCard);
                    }
                }
                break;
            case EffectType.EventTriggers:
                Debug.Assert(sourceCard != null, "there is no sourceCard for this EventTriggers Effect");
                foreach (TriggeredEffect tEffect in triggeredEffects) {
                    tEffect.sourceCard = sourceCard;
                    affectedPlayer.eventTriggers.Add(tEffect);
                }
                break;
            case EffectType.ReplacementEffect:
                // Add a player passive that modifies zone destinations (e.g., graveyard -> exile)
                Debug.Assert(replacementPassive != null, "there is no replacementPassive for this ReplacementEffect");
                PassiveEffect replacementEffect = new PassiveEffect((Passive)replacementPassive);
                replacementEffect.thisTurn = true;  // Replacement effects are always for this turn only
                affectedPlayer.playerPassives.Add(replacementEffect);
                break;
            case EffectType.Sacrifice:
                Debug.Assert(sourceCard != null, "there is no sourceCard for this Sacrifice Effect");
                if (scope == Scope.SelfOnly) {
                    gameMatch.Destroy(sourceCard);
                } else if (targetUids.Count > 0) {
                    // Sacrifice user-selected targets (for isCost effects)
                    foreach (int uid in targetUids) {
                        if (gameMatch.cardByUid.TryGetValue(uid, out Card? targetCard)) {
                            gameMatch.Destroy(targetCard);  // Destroy handles both cards and tokens
                        }
                    }
                } else if (tokenType != null) {
                    // Auto-sacrifice first matching token (only for single-token costs)
                    Token? tokenToSacrifice = effectOwner.tokens.FirstOrDefault(t => t.tokenType == tokenType);
                    if (tokenToSacrifice != null) {
                        gameMatch.Destroy(tokenToSacrifice);  // Destroy handles tokens
                    }
                }
                break;
            case EffectType.CantAttack:
                affectedPlayer.cantAttackThisTurn = true;
                break;
            case EffectType.AddCounter:
                Debug.Assert(sourceCard != null, "there is no sourceCard for this AddCounter Effect");
                Debug.Assert(counterType != null, "there is no counterType for this AddCounter Effect");
                Card counterTarget = scope == Scope.SelfOnly ? sourceCard : (targetUids.Count > 0 ? gameMatch.cardByUid[targetUids[0]] : sourceCard);
                int counterAmount = amount ?? 1;
                if (counterType == "oneOne") {
                    counterTarget.plusOnePlusOneCounters += counterAmount;
                } else if (counterType == "minusOneMinusOne") {
                    counterTarget.minusOneMinusOneCounters += counterAmount;
                }
                // Send refresh event to update the card display for both players
                GameEvent counterRefreshEvent = GameEvent.CreateRefreshCardDisplayEvent(counterTarget);
                effectOwner.eventList.Add(counterRefreshEvent);
                gameMatch.GetOpponent(effectOwner).eventList.Add(counterRefreshEvent);
                break;
            case EffectType.ModifyType:
                // Convert tokens of specified type to summons
                Debug.Assert(tokenType != null, "there is no tokenType for this ModifyType effect");
                affectedUids = ModifyType(gameMatch, affectedPlayer);
                break;
            case EffectType.RepeatAllEffects:
                Debug.Assert(parentEffectList != null, "RepeatAllEffects has no parent effect list to repeat");
                // Clone the effects and add them as a new trigger on the stack
                List<Effect> clonedEffects = parentEffectList.Select(e => e.Clone()).ToList();
                // Set sourceCard on cloned effects
                foreach (Effect clonedEffect in clonedEffects) {
                    clonedEffect.sourceCard = sourceCard;
                }
                StackObj repeatStackObj = new StackObj(
                    sourceCard!,
                    StackObjType.TriggeredEffect,
                    clonedEffects,
                    sourceCard!.currentZone,
                    effectOwner,
                    "Repeat: " + sourceCard!.name
                );
                gameMatch.AddRepeatToStack(repeatStackObj);
                break;
            case EffectType.Counter:
                // Counter a spell/summon on the stack - remove it and send to graveyard
                Debug.Assert(targetUids.Count > 0, "Counter effect requires a target on the stack");
                affectedUids = new List<int>();
                foreach (int uid in targetUids) {
                    if (gameMatch.CounterStackItem(uid)) {
                        affectedUids.Add(uid);
                    }
                }
                break;
            case EffectType.ModifyHandSize:
                Debug.Assert(amount != null, "ModifyHandSize effect requires an amount");
                affectedPlayer.maxHandSize += amount.Value;
                break;
            case EffectType.ModifySummonLimit:
                Debug.Assert(amount != null, "ModifySummonLimit effect requires an amount");
                affectedPlayer.turnSummonLimitBonus += amount.Value;
                break;
            case EffectType.Detain:
                // Exile targeted card(s) from opponent's hand until source card leaves play
                Debug.Assert(sourceCard != null, "Detain effect requires a source card");
                affectedUids = new List<int>();
                foreach (int uid in targetUids) {
                    if (!gameMatch.cardByUid.ContainsKey(uid)) continue;
                    Card detainedCard = gameMatch.cardByUid[uid];
                    Player cardOwner = gameMatch.GetOwnerOf(detainedCard);
                    // Send to exile and track for return when source leaves play
                    gameMatch.DetainCard(sourceCard, detainedCard, cardOwner);
                    affectedUids.Add(uid);
                }
                break;
            case EffectType.ShuffleDeck:
                gameMatch.ShuffleDeck(affectedPlayer.deck);
                break;
            case EffectType.ExtraTurn:
                affectedPlayer.extraTurns++;
                Console.WriteLine($"[ExtraTurn] {affectedPlayer.playerName} gains an extra turn! (now has {affectedPlayer.extraTurns})");
                break;
            case EffectType.ModifyCost:
                // Handle "next spell free" effect
                if (targetBasedOn == TargetBasedOn.NextSpell) {
                    sourcePlayer.nextSpellFree = true;
                    Console.WriteLine($"[ModifyCost] {sourcePlayer.playerName}'s next spell costs 0 LP");
                    // Refresh hand cards to show updated costs
                    gameMatch.RefreshCards(sourcePlayer, sourcePlayer.hand, false);
                }
                break;
            default:
                 Console.WriteLine("Effect " + effect + " doesn't exist or is not implemented");
                 break;
        }

        // TODO check for optional effects, set their subjectUids and wait for response from player
        // resolve additional effects
        if (additionalEffects != null) {
            foreach (Effect addEffect in additionalEffects) {
                // Propagate parentEffectList so RepeatAllEffects can access it
                addEffect.parentEffectList = parentEffectList;
                if (addEffect.targetBasedOn == TargetBasedOn.RootAffected) {
                    Debug.Assert(affectedUids != null,
                        "There is no affected entities or players for this additional effect");
                    foreach (int uid in affectedUids) {
                        addEffect.Resolve(gameMatch, affectedPlayer, uid);
                    }
                } else {
                    addEffect.Resolve(gameMatch, affectedPlayer);
                }
            }
        }
    }

    private void GrantPassive(Card c) {
        Debug.Assert(passives != null, "there are no passives to grant :(");
        Console.WriteLine($"    GrantPassive method called for: {c.name} (uid={c.uid}), passives.Count={passives.Count}, thisTurn={thisTurn}");
        foreach (PassiveEffect pEffect in passives) {
            // Clone the passive so each target has its own instance
            PassiveEffect clonedPassive = pEffect.Clone();
            clonedPassive.grantedBy = sourceCard;  // Original granter (for UI tracking)
            clonedPassive.owner = c;               // Card this passive is on (for logic)
            // Propagate thisTurn from the Effect to the passive
            if (thisTurn) clonedPassive.thisTurn = true;

            if (clonedPassive.statModifiers != null) {
                foreach (StatModifier statMod in clonedPassive.statModifiers) {
                    if (statMod.xAmount) statMod.amount = sourceCard!.x!.Value;
                }
            }
            c.grantedPassives.Add(clonedPassive);
            Console.WriteLine($"      Added passive to {c.name}, now has {c.grantedPassives.Count} grantedPassives, thisTurn={clonedPassive.thisTurn}");
        }
    }

    private List<int> GrantKeyword(GameMatch gameMatch, Player affectedPlayer) {
        Debug.Assert(keyword != null, "There is no keyword associated with this GrantKeyword Effect");
        List<int> tempAffectedUids = new List<int>();

        // Handle NextSpellOpponent - add a player passive that grants keyword to next spell
        // Note: affectedPlayer is already set correctly by isOpponent in the JSON, so don't flip again
        if (targetBasedOn == TargetBasedOn.NextSpellOpponent) {
            PassiveEffect keywordPassive = new PassiveEffect(Passive.GrantKeywordToNextSpell, (Keyword)keyword);
            affectedPlayer.playerPassives.Add(keywordPassive);
            return tempAffectedUids;
        }

        // Normal case - grant to specific target
        Debug.Assert(subjectUid != null, "There is no target for this GrantKeyword Effect");
        gameMatch.GrantKeyword(affectedPlayer, gameMatch.cardByUid[(int)subjectUid], (Keyword)keyword);
        tempAffectedUids.Add((int)subjectUid);
        return tempAffectedUids;
    }
    

    private void Mill(GameMatch gameMatch, Player affectedPlayer) {
        Debug.Assert(amount != null);
        gameMatch.Mill(affectedPlayer, (int)amount);
    }

    private void LookAtDeck(GameMatch gameMatch, Player affectedPlayer) {
        Debug.Assert(amount != null,
            "Amount is null -> check the json file for amount property under the LookAtDeck effect");
        List<Card> cardsToLookAt = GameMatch.GetTopCards(affectedPlayer, (int)amount);
        List<CardDisplayData> cddsToLookAt = cardsToLookAt.Select(card => new CardDisplayData(card)).ToList();

        // If no deckDestinations, this is a peek-only effect (just show the cards, no selection needed)
        if (deckDestinations == null) {
            gameMatch.PeekAtDeck(affectedPlayer, cddsToLookAt);
            return;
        }

        gameMatch.LookAtDeck(affectedPlayer, deckDestinations, cddsToLookAt, GetCardSelectionDatas(deckDestinations, cardsToLookAt, gameMatch, affectedPlayer));
    }


    private void Tutor(GameMatch gameMatch, Player affectedPlayer) {
        Console.WriteLine($"[TUTOR DEBUG] Tutor called for {sourceCard?.name ?? "null"}, stack trace:");
        Console.WriteLine(Environment.StackTrace);
        if (amount == 0) amount = 1;
        Debug.Assert(affectedPlayer.deck != null, affectedPlayer.playerName + " has no deck");
        Zone tempZone = destination ?? Zone.Hand;
        List<Card> cardsToLookAt = affectedPlayer.deck.ToList();
        List<DeckDestination> tempDestinations = new List<DeckDestination> {
            new(Utils.ZoneToDestinationType(tempZone), false, reveal, amount),
            new(DeckDestinationType.Bottom, false, false, null, null, null, Ordering.Random)
        };
        List<CardDisplayData> cddsToLookAt = cardsToLookAt.Select(card => new CardDisplayData(card)).ToList();
        List<CardSelectionData> selectionDatas = GetCardSelectionDatas(tempDestinations, cardsToLookAt, gameMatch, affectedPlayer);
        gameMatch.LookAtDeck(affectedPlayer, tempDestinations, cddsToLookAt, selectionDatas);
    }
    private List<CardSelectionData> GetCardSelectionDatas(List<DeckDestination> dDestinations, List<Card> cardsToLookAt, GameMatch gameMatch, Player sourcePlayer) {
        List<CardSelectionData> csDatas = new List<CardSelectionData>();
        Qualifier eQualifier = new Qualifier(this, sourcePlayer);
        foreach (DeckDestination dd in dDestinations) {
            List<int> qualifiedUids = ToUids(gameMatch.GetQualifiedCards(cardsToLookAt, eQualifier));
            int selectionMin;
            int selectionMax;
            if (dd.amountIsPlayerChosen) {
                // Player can choose any amount from 0 to all available cards
                selectionMin = 0;
                selectionMax = qualifiedUids.Count;
            } else if (dd.amount != null) {
                // Fixed amount required
                selectionMin = (int)dd.amount;
                selectionMax = (int)dd.amount;
            } else {
                // "The rest" - will be auto-filled with remaining cards
                selectionMin = 0;
                selectionMax = 0;
            }
            bool isSelectOrder = dd.ordering == Ordering.Any;
            csDatas.Add(new CardSelectionData(qualifiedUids, GetDDMessage(dd, qualifiedUids.Count), selectionMin, selectionMax, isSelectOrder));
        }
        return csDatas;
    }

    private List<int> ToUids(List<Card> cards) {
        return cards.Select(c => c.uid).ToList();
    }

private string GetDDMessage(DeckDestination dd, int totalCards) {
        string message;
        if (dd.amountIsPlayerChosen) {
            message = "Select any cards to ";
        } else if (dd.amount == null) {
            // "The rest" destination - remaining cards go here
            message = "Remaining cards will ";
        } else {
            message = dd.amount switch {
                1 => "Pick a card to ",
                _ => $"Pick {dd.amount} cards to "
            };
        }
        string destinationMessage = dd.deckDestination switch {
            DeckDestinationType.Bottom => "put on the bottom of your deck",
            DeckDestinationType.Graveyard => "send to your graveyard",
            DeckDestinationType.Hand => "put into your hand",
            DeckDestinationType.Top => "put on top of your deck",
            DeckDestinationType.Play => "put into play",
            _ => "unknown deck destination"
        };
        return message + destinationMessage;
    }
    private void Draw(GameMatch gameMatch, Player affectedPlayer) {
        Debug.Assert(amount != null, "Amount is null for this effect -> check the json file for amount property under the draw effect");
        gameMatch.Draw(affectedPlayer, (int)amount);
    }
    
    /// <summary>
    /// Handles CreateToken Effect logic using the Effect's properties.
    /// Token base stats/abilities come from JSON, effect can override/add to them.
    /// </summary>
    /// <param name="gameMatch"></param>
    /// <param name="affectedPlayer"></param>
    private void CreateToken(GameMatch gameMatch, Player affectedPlayer) {
        for (int i = 0; i < amount; i++) {
            // Token loads base definition from JSON
            Token newToken = new Token(tokenType, gameMatch);

            // If attackBasedOn/defenseBasedOn are set, create a passive with dynamic stat modifiers
            if (attackBasedOn != null || defenseBasedOn != null) {
                newToken.passiveEffects ??= new List<PassiveEffect>();
                PassiveEffect statsPassive = new PassiveEffect {
                    passive = Passive.ChangeStats,
                    scope = Scope.SelfOnly,
                    statModifiers = new List<StatModifier>()
                };

                // Set base stats to 0 and add modifiers for dynamic calculation
                if (attackBasedOn != null) {
                    newToken.attack = 0;
                    statsPassive.statModifiers.Add(new StatModifier {
                        statType = StatType.Attack,
                        operatorType = OperatorType.Add,
                        amountBasedOn = attackBasedOn
                    });
                }
                if (defenseBasedOn != null) {
                    newToken.defense = 0;
                    statsPassive.statModifiers.Add(new StatModifier {
                        statType = StatType.Defense,
                        operatorType = OperatorType.Add,
                        amountBasedOn = defenseBasedOn
                    });
                }

                // Set description for the passive (use token's name, not the effect's description)
                statsPassive.description = GetTokenStatDescription(newToken.name, attackBasedOn ?? defenseBasedOn);

                newToken.grantedPassives.Add(statsPassive);

                // Ensure it's a summon if we're giving it stats
                if (newToken.type == CardType.Token) {
                    newToken.type = CardType.Summon;
                }
            }
            // Override attack/defense if specified directly (not via BasedOn)
            else if (attack != null) {
                newToken.attack = attack;
                newToken.defense = defense;
                // Ensure it's a summon if we're giving it stats
                if (newToken.type == CardType.Token) {
                    newToken.type = CardType.Summon;
                }
            }

            // Add keyword if specified (adds to existing keywords from JSON)
            if (keyword != null) {
                newToken.keywords ??= new List<Keyword>();
                if (!newToken.keywords.Contains((Keyword)keyword)) {
                    newToken.keywords.Add((Keyword)keyword);
                }
            }

            // Add keywords list if specified
            if (keywords != null) {
                newToken.keywords ??= new List<Keyword>();
                foreach (Keyword kw in keywords) {
                    if (!newToken.keywords.Contains(kw)) {
                        newToken.keywords.Add(kw);
                    }
                }
            }

            // Add passive modifier if specified (adds to existing passives from JSON)
            if (tokenPassive != null) {
                newToken.passiveEffects ??= new List<PassiveEffect>();
                switch (tokenPassive.passive) {
                    case Passive.TributeRestriction:
                        newToken.passiveEffects.Add(new PassiveEffect(Passive.TributeRestriction, tokenPassive.tribe));
                        newToken.description += " Can only be tributed to " + tokenPassive.tribe +
                                                GetPluralityBasedOnWord(tokenPassive.tribe.ToString()) + ".";
                        break;
                    default:
                        newToken.passiveEffects.Add(new PassiveEffect(tokenPassive.passive, tokenPassive.tribe, tokenPassive.scope));
                        break;
                }
            }

            Console.WriteLine($"Created token from effect: {tokenType} (atk={newToken.attack}, def={newToken.defense})");
            gameMatch.CreateTokenForPlayer(affectedPlayer, newToken, attacking);
        }
    }

    /// <summary>
    /// Converts tokens of a specified type to summons and moves them to play.
    /// Used by effects like Spreading Thornbush that animate herbs into creatures.
    /// </summary>
    private List<int> ModifyType(GameMatch gameMatch, Player affectedPlayer) {
        List<int> affectedUids = new();
        Debug.Assert(tokenType != null, "ModifyType requires a tokenType");

        // Find all tokens of the specified type
        List<Token> tokensToConvert = affectedPlayer.tokens
            .Where(t => t.tokenType == tokenType)
            .ToList();

        Console.WriteLine($"ModifyType: Converting {tokensToConvert.Count} {tokenType} tokens to summons");

        foreach (Token token in tokensToConvert) {
            Console.WriteLine($"  Token BEFORE conversion: name={token.name}, type={token.type}, tribe={token.tribe}, atk={token.attack}, def={token.defense}");

            // Remove from token zone
            gameMatch.RemoveFromTokenZone(affectedPlayer, token);

            // Change type to Summon
            token.type = CardType.Summon;

            // Set attack/defense if specified
            if (attack != null) token.attack = attack;
            if (defense != null) token.defense = defense;

            // Add tribe if specified (treefolk in addition to their types)
            if (tribe != null) {
                token.tribe = (Tribe)tribe;
            }

            // Move to play - Summon handles the event creation
            token.currentZone = Zone.Play;
            gameMatch.Summon(token, affectedPlayer, false);

            affectedUids.Add(token.uid);
            Console.WriteLine($"  Converted {token.name} (uid={token.uid}) to {token.attack}/{token.defense} {token.tribe} summon");
        }

        return affectedUids;
    }

    private string GetTokenStatDescription(string tokenName, AmountBasedOn? amountBasedOn) {
        string basedOnText = amountBasedOn switch {
            AmountBasedOn.StonesControlled => "stones you control",
            AmountBasedOn.HerbsControlled => "herbs you control",
            AmountBasedOn.GoblinsControlled => "goblins you control",
            AmountBasedOn.TreefolkControlled => "treefolk you control",
            AmountBasedOn.PlantsControlled => "plants you control",
            _ => "a dynamic value"
        };
        return $"{tokenName}'s attack and defense are equal to the number of {basedOnText}.";
    }

    /// <summary>
    ///  Handles SendToZone Effect logic using the Effect's properties
    /// </summary>
    /// <param name="gameMatch"> current game match </param>
    /// <param name="affectedPlayer"> affected player </param>
    /// <returns> list of affected uids </returns>
    
    private List<int> SendToZone(GameMatch gameMatch, Player affectedPlayer) {
        Qualifier eQualifier = new Qualifier(this, affectedPlayer);
        // all cards from zone that meet the qualifications
        Debug.Assert(destination != null, "there is no zone associated with this sendToZone effect");
        List<int> tempAffectedUids = new();

        // Handle targeted sends (when targetUids is populated from target selection)
        if (targetUids.Count > 0) {
            HashSet<string> targetNames = new(); // Track names for allOfSameName
            foreach (int uid in targetUids) {
                if (!gameMatch.cardByUid.ContainsKey(uid)) continue;
                Card targetCard = gameMatch.cardByUid[uid];
                if (allOfSameName) targetNames.Add(targetCard.name);
                Player targetPlayer = ownersControl ? gameMatch.GetOwnerOf(targetCard) : affectedPlayer;
                if (destination == Zone.Deck && deckDestination != null) {
                    DeckDestination dd = new DeckDestination((DeckDestinationType)deckDestination, false, false);
                    gameMatch.SendToZone(targetPlayer, Zone.Deck, targetCard, dd);
                } else {
                    gameMatch.SendToZone(targetPlayer, (Zone)destination!, targetCard);
                }
                tempAffectedUids.Add(uid);
            }
            // If allOfSameName, also send all other cards with the same name(s)
            if (allOfSameName && targetNames.Count > 0) {
                // Get all summons and summon-type tokens in play
                List<Card> allInPlay = gameMatch.GetAllSummonsInPlay().ToList();
                allInPlay.AddRange(gameMatch.playerOne.tokens.Where(t => t.type == CardType.Summon));
                allInPlay.AddRange(gameMatch.playerTwo.tokens.Where(t => t.type == CardType.Summon));
                foreach (Card c in allInPlay) {
                    if (tempAffectedUids.Contains(c.uid)) continue; // Already processed
                    if (!targetNames.Contains(c.name)) continue; // Different name
                    Player targetPlayer = ownersControl ? gameMatch.GetOwnerOf(c) : affectedPlayer;
                    if (destination == Zone.Deck && deckDestination != null) {
                        DeckDestination dd = new DeckDestination((DeckDestinationType)deckDestination, false, false);
                        gameMatch.SendToZone(targetPlayer, Zone.Deck, c, dd);
                    } else {
                        gameMatch.SendToZone(targetPlayer, (Zone)destination!, c);
                    }
                    tempAffectedUids.Add(c.uid);
                }
            }
            return tempAffectedUids;
        }

        // Handle selection-based sends using LookAtDeck panel (complex deckDestinations)
        if (deckDestinations != null && destination == Zone.Deck) {
            // Get cards from the target zone (hand by default for this type of effect)
            List<Card> cardsToSelect = affectedPlayer.hand.ToList();
            List<CardDisplayData> cddsToSelect = cardsToSelect.Select(card => new CardDisplayData(card)).ToList();
            List<CardSelectionData> selectionDatas = GetCardSelectionDatas(deckDestinations, cardsToSelect, gameMatch, affectedPlayer);
            gameMatch.LookAtDeck(affectedPlayer, deckDestinations, cddsToSelect, selectionDatas);
            return tempAffectedUids;
        }

        if (zone != null) {
            switch (zone) {
                // TODO TargetTypes
                case Zone.Hand:
                    List<Card> handCards = affectedPlayer.hand.ToList();
                    foreach (Card c in gameMatch.GetQualifiedCards(handCards, eQualifier)) {
                        if (destination == Zone.Deck && deckDestination != null) {
                            DeckDestination dd = new DeckDestination((DeckDestinationType)deckDestination, false, false);
                            gameMatch.SendToZone(affectedPlayer, Zone.Deck, c, dd);
                        } else {
                            gameMatch.SendToZone(affectedPlayer, (Zone)destination, c);
                        }
                        tempAffectedUids.Add(c.uid);
                    }
                    break;
                case Zone.Play:
                    // Get all cards in play that match the qualifier
                    List<Card> allPlayCards = gameMatch.GetAllSummonsInPlay().ToList();
                    foreach (Card c in gameMatch.GetQualifiedCards(allPlayCards, eQualifier)) {
                        // Send to owner's hand (ownersControl) or caster's control
                        Player targetPlayer = ownersControl ? gameMatch.GetOwnerOf(c) : affectedPlayer;
                        gameMatch.SendToZone(targetPlayer, (Zone)destination, c);
                        tempAffectedUids.Add(c.uid);
                    }
                    break;
                case Zone.Graveyard:
                    List<Card> tempCardList = affectedPlayer.graveyard.ToList();
                    foreach (Card c in gameMatch.GetQualifiedCards(tempCardList, eQualifier)) {
                        gameMatch.SendToZone(affectedPlayer, (Zone)destination, c);
                        // added for additional effects
                        tempAffectedUids.Add(c.uid);
                    }
                    if (all) {
                        tempCardList = gameMatch.GetOpponent(affectedPlayer).graveyard.ToList();
                        // determine who's zone to send it to. Owner will NEVER have non-owned cards in their graveyard
                        Player controllingPlayer = ownersControl ? gameMatch.GetOpponent(affectedPlayer) : affectedPlayer;
                        // send the opponents graveyard to the determined player's zone
                        foreach (Card c in tempCardList) {
                            if (gameMatch.QualifyCard(c, eQualifier)) {
                                gameMatch.SendToZone(controllingPlayer, (Zone)destination, c);
                                // added for additional effects after
                                tempAffectedUids.Add(c.uid);
                            }
                        }
                    }
                    break;
                default:
                    Console.WriteLine("Source zone not implemented for SendToZone Effect");
                    break;
            }
        } else {
            Debug.Assert(subjectUid != null, "There is no target for this SendToZone Effect");
            gameMatch.SendToZone(affectedPlayer, (Zone)destination!, gameMatch.cardByUid[(int)subjectUid]);
            tempAffectedUids.Add((int)subjectUid);
        }
        return tempAffectedUids;
    }

    public string EffectToString(GameMatch gameMatch, bool forOpponentChoice = false) {
        if (description != null) {
            return description;
        }
        // set defaults for sentence structure
        string plurality;
        // When generating text for opponent's choice, don't use "opponent" prefix since they're choosing for themselves
        bool useOpponentPronoun = isOpponent && !forOpponentChoice;
        string verbAgreement = useOpponentPronoun ? "s " : " ";
        string pronoun = useOpponentPronoun ? "opponent " : "";
        string tempString = "error: no effect string";
        string target = "card";
        // set default target for effects with targetType or scope=SelfOnly
        if (cardType != null) target = cardType.ToString()!;
        if (tribe != null) target = tribe.ToString()!;
        Debug.Assert(sourceCard != null, "no sourceCard for this effect");
        if (scope == Scope.SelfOnly) target = sourceCard.name;
        // set final string based on EffectType
        switch (effect) { 
            case EffectType.CreateToken:
                // Calculate display amount - use amountBasedOn if set, otherwise use amount
                int displayTokenAmount = amount ?? 1;
                if (amountBasedOn != null) {
                    // Use GetOwnerOf for cards not in play (e.g., death triggers where source is in graveyard)
                    Player? controllingPlayer = sourceCard.currentZone == Zone.Play
                        ? gameMatch.GetControllerOf(sourceCard)
                        : gameMatch.GetOwnerOf(sourceCard);
                    displayTokenAmount = gameMatch.GetAmountBasedOn(amountBasedOn, scope, controllingPlayer, rootEffect, cardType, restrictions, sourceCard);
                }
                plurality = displayTokenAmount == 1 ? "" : "s";
                // this looks ugly, but it does what it needs to: creates the string for the effect
                // depending on what the effect is.
                string attackingString = " ";
                string thisTurnString = "";
                if (attacking) attackingString = " attacking ";
                if (thisTurn) thisTurnString = " this turn";
                if (tokenType != TokenType.Herb && tokenType != TokenType.Stone) {
                    tempString = "create " + displayTokenAmount + attackingString + attack + "/" + defense + " " + tokenType + " token" + plurality + thisTurnString + ".";
                } else {
                    tempString = "create " + displayTokenAmount + " " + tokenType + " token" + plurality + ".";
                }
                break;
            case EffectType.Draw:
                plurality = amount == 1 ? "" : "s";
                tempString = pronoun + "draw" + verbAgreement + amount + " card" + plurality + ".";
                break;
            case EffectType.Mill:
                plurality = amount == 1 ? "" : "s";
                if (amountBasedOn != null) {
                    switch (amountBasedOn) {
                        case AmountBasedOn.UntilCardType:
                            tempString = "mill until you mill a " + cardType + ".";
                            break;
                    }
                } else {
                    tempString = "mill " + amount + " card" + plurality + ".";
                }
                break;
            case EffectType.LookAtDeck:
                StringBuilder sb = new("look at the top " + amount + " cards of your deck");
                if (deckDestinations != null) {
                    foreach (DeckDestination dd in deckDestinations) {
                        sb.Append(ReferenceEquals(dd, deckDestinations.Last()) ? ", and" : ",");
                        sb.Append(" put ");
                        sb.Append(dd.amountIsPlayerChosen ? "any amount" : dd.amount == null ? "the rest" : dd.amount);
                        switch (dd.deckDestination) {
                            case DeckDestinationType.Bottom:
                                sb.Append(" on the bottom of your deck");
                                break;
                            case DeckDestinationType.Top:
                                sb.Append(" on top of your deck");
                                break;
                            case DeckDestinationType.Graveyard:
                                sb.Append(" into your graveyard");
                                break;
                            case DeckDestinationType.Hand:
                                sb.Append(" into your hand");
                                break;
                            default:
                                Console.WriteLine("destination for card selection not implemented");
                                break;
                        }

                        switch (dd.ordering) {
                            case null:
                                break;
                            case Ordering.Random:
                                sb.Append(" in a random order");
                                break;
                            case Ordering.Same:
                                sb.Append(" in the same order");
                                break;
                            case Ordering.Any:
                                sb.Append(" in any order");
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                }
                sb.Append('.');
                tempString = sb.ToString();
                break;
            case EffectType.SendToZone:
                string targetDesc = "";
                if (tribe != null) {
                    targetDesc = "a " + tribe.ToString()!.ToLower();
                } else if (cardType != null) {
                    targetDesc = "a " + cardType.ToString()!.ToLower();
                } else if (targetType != null) {
                    targetDesc = "target " + targetType.ToString()!.ToLower();
                } else {
                    targetDesc = "a card";
                }
                string sourceDesc = "";
                if (targetZones != null && targetZones.Count > 0) {
                    var zoneNames = targetZones.Select(z => z.ToString().ToLower());
                    sourceDesc = " from your " + string.Join(" or ", zoneNames);
                }
                string destString = destination switch {
                    Zone.Hand => "hand",
                    Zone.Graveyard => "graveyard",
                    Zone.Deck => "deck",
                    Zone.Play => "play",
                    _ => destination.ToString()!.ToLower()
                };
                // Use "put" when moving to play, "return" otherwise
                string verb = destination == Zone.Play ? "put" : "return";
                tempString = verb + " " + targetDesc + sourceDesc + " into " + destString + ".";
                break;
            case EffectType.GainLife:
                if (isOpponent) {
                    plurality = "s";
                    pronoun = "opponent";
                } else {
                    plurality = "";
                }
                tempString = pronoun + " gain" + plurality + " " + amount + " life.";
                break;
            case EffectType.GainControl:
                string gainControlTargetDesc = "target ";
                if (restrictions != null && restrictions.Count > 0) {
                    gainControlTargetDesc += string.Join(" ", restrictions.Select(r => r.ToString().ToLower().Replace("non", "non-"))) + " ";
                }
                gainControlTargetDesc += targetType?.ToString().ToLower() ?? "permanent";
                tempString = "gain control of " + gainControlTargetDesc + ".";
                break;
            case EffectType.LoseLife:
                if (isOpponent) {
                    plurality = "s";
                    pronoun = "opponent";
                } else {
                    plurality = "";
                }
                // Calculate amount from amountBasedOn if set
                int? displayAmount = amount;
                if (amountBasedOn != null) {
                    // Use GetOwnerOf for cards not in play (e.g., spells being cast from hand)
                    Player? controllingPlayer = sourceCard.currentZone == Zone.Play
                        ? gameMatch.GetControllerOf(sourceCard)
                        : gameMatch.GetOwnerOf(sourceCard);
                    displayAmount = gameMatch.GetAmountBasedOn(amountBasedOn, scope, controllingPlayer, rootEffect, cardType, restrictions, sourceCard);
                }
                tempString = pronoun + " lose" + plurality + " " + displayAmount + " life.";
                break;
            case EffectType.SetLifeTotal:
                pronoun = isOpponent ? "opponent's" : "your";
                tempString = "set " + pronoun + " lifetotal to " + amount + ".";
                break;
            case EffectType.Reveal:
                if (all) {
                    tempString = "reveal your hand";
                } else if (targetUids.Count > 0) {
                    // Show the name of the selected card(s)
                    List<string> cardNames = targetUids.Select(uid => gameMatch.cardByUid[uid].name).ToList();
                    tempString = "reveal " + string.Join(", ", cardNames);
                } else if (subjectUid != null) {
                    tempString = "reveal " + gameMatch.cardByUid[(int)subjectUid].name;
                } else if (targetType == TargetType.CardInHand) {
                    string tribeStr = tribe != null ? tribe.ToString()!.ToLower() + " " : "";
                    tempString = "reveal a " + tribeStr + "card from your hand";
                } else {
                    tempString = "reveal a card";
                }
                break;
            case EffectType.Tutor:
                string ending = reveal ? " and reveal it." : "."; 
                tempString = "tutor a " + target + " to your hand" + ending;
                break;
            case EffectType.GrantPassive:
                Debug.Assert(sourceCard != null, "there's no sourceCard for this GrantPassive Effect");
                Debug.Assert(passives != null, "there are no passives for this GrantPassive Effect");
                tempString = target;
                foreach (PassiveEffect pEffect in passives) {
                    tempString += " " + pEffect.GetDescription() + ".";
                }
                break;
            case EffectType.CastCard:
                Debug.Assert(sourceCard != null, "there's no sourceCard for this CastCard Effect");
                if (targetUids.Count > 0) {
                    List<string> cardNames = targetUids.Select(uid => gameMatch.cardByUid[uid].name).ToList();
                    tempString = "cast " + string.Join(", ", cardNames);
                } else {
                    tempString = "cast " + target;
                }
                break;
            case EffectType.DealDamage:
                if (targetType != null) {
                    if (targetType == TargetType.Any) {
                        tempString = "deal " + amount + " damage to any target";
                    } else {
                        tempString = "deal " + amount + " damage to target " + targetType;
                    }
                }
                break;
            case EffectType.Sacrifice:
                tempString = "sacrifice " + target;
                break;
            case EffectType.AddCounter:
                string counterDesc = counterType == "oneOne" ? "+1/+1" : (counterType == "minusOneMinusOne" ? "-1/-1" : counterType ?? "");
                int numCounters = amount ?? 1;
                string counterPlural = numCounters == 1 ? "counter" : "counters";
                tempString = "put " + numCounters + " " + counterDesc + " " + counterPlural + " on " + target;
                break;
            case EffectType.GrantKeyword:
                Debug.Assert(keyword != null, "there's no keyword for this GrantKeyword Effect");
                if (targetBasedOn == TargetBasedOn.NextSpellOpponent) {
                    tempString = "the next spell your opponent casts has " + keyword.ToString()!.ToLower() + ".";
                } else {
                    tempString = "grant " + keyword.ToString()!.ToLower() + " to " + target + ".";
                }
                break;
            case EffectType.Counter:
                // Build counter description based on targetType and restrictions
                string counterTarget = targetType?.ToString()?.ToLower() ?? "spell";
                string restrictionDesc = "";
                if (restrictions != null && restrictions.Count > 0) {
                    foreach (Restriction r in restrictions) {
                        switch (r) {
                            case Restriction.Defense:
                                if (restrictionMin != null && restrictionMax != null && restrictionMin == restrictionMax) {
                                    restrictionDesc = " with " + restrictionMax + " defense";
                                } else if (restrictionMax != null) {
                                    restrictionDesc = " with " + restrictionMax + " or less defense";
                                }
                                break;
                            case Restriction.Cost:
                                if (restrictionMax != null) {
                                    restrictionDesc = " with " + restrictionMax + " or less LP cost";
                                }
                                break;
                        }
                    }
                }
                tempString = "counter target " + counterTarget + restrictionDesc + ".";
                break;
            case EffectType.ModifyHandSize:
                if (amount != null && amount >= 20) {
                    tempString = "you have no maximum hand size.";
                } else {
                    string sign = amount > 0 ? "+" : "";
                    tempString = "your maximum hand size is " + sign + amount + ".";
                }
                break;
            case EffectType.ModifySummonLimit:
                if (amount == 1) {
                    tempString = pronoun + "may play an additional summon this turn.";
                } else {
                    tempString = pronoun + "may play " + amount + " additional summons this turn.";
                }
                break;
            case EffectType.ModifyType:
                string modifyTokenName = tokenType?.ToString()?.ToLower() ?? "token";
                string modifyStatsStr = "";
                if (attack != null && defense != null) {
                    modifyStatsStr = $" {attack}/{defense}";
                }
                string modifyTribeStr = tribe != null ? " " + tribe.ToString()!.ToLower() : "";
                tempString = $"all {modifyTokenName} tokens become{modifyStatsStr}{modifyTribeStr} summons.";
                break;
            case EffectType.Detain:
                string detainRestriction = "";
                if (restrictions != null && restrictions.Contains(Restriction.NonSummon)) {
                    detainRestriction = "non-summon ";
                }
                tempString = $"target opponent reveals their hand. You choose a {detainRestriction}card from their hand to exile until {sourceCard?.name ?? "this"} leaves play.";
                break;
            case EffectType.Discard:
                plurality = amount == 1 ? "" : "s";
                string randomStr = random ? " at random" : "";
                tempString = pronoun + "discard" + verbAgreement + amount + " card" + plurality + randomStr + ".";
                break;
            case EffectType.ExtraTurn:
                tempString = pronoun + "take" + verbAgreement + " an extra turn after this one.";
                break;
            case EffectType.Destroy:
                if (targetType != null) {
                    tempString = "destroy target " + targetType.ToString()!.ToLower() + ".";
                } else if (all) {
                    string destroyTarget = cardType?.ToString()?.ToLower() ?? "summon";
                    tempString = "destroy all " + destroyTarget + "s.";
                } else if (scope == Scope.SelfOnly) {
                    tempString = "destroy " + (sourceCard?.name ?? "this") + ".";
                } else {
                    tempString = "destroy target.";
                }
                break;
            default:
                tempString = "error: unknown effect";
                break;
        }
        string finalString = char.ToUpper(tempString[0]) + tempString.Substring(1);
        return finalString;
    }
    
    // could move this to util if needed
    private string GetPluralityBasedOnWord(string? word) {
        return word switch {
            "Treefolk" => "",
            "Merfolk" => "",
            _ => "s"
        };
    }
}