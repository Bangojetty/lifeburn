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
    public string? sourcePlayer;  // "opponent" if opponent is the actor, defaults to self
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

    // New unified targeting object for in-play targets
    public Target? target;

    // New unified selection object for selecting cards/amounts
    public Select? select;

    public bool attacking;
    public bool ownersControl;
    public List<PassiveEffect>? passives;
    public bool thisTurn;
    public bool freeCast;  // For CastCard effect - if true, cast without paying tribute/mana costs
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
    public Phase? phase;  // For GoToPhase effect OR delayed SendToZone phase
    public string? phaseOfPlayer;  // "player" or "opponent" - whose turn the delayed effect fires on
    public TokenType? tokenType;
    public TokenType? notTokenType;  // Exclude this token type (e.g., "not herb tokens")
    public bool isToken;  // Target must be a token
    public List<Zone>? targetZones;
    public PassiveEffect? tokenPassive;
    public List<DeckDestination>? deckDestinations;
    public DeckDestinationType? deckDestination;  // simpler single destination (top/bottom)
    public bool targetPlayersZone;  // If true, target a player but affect cards in their zone (from targetZones)
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
    public bool selectRepeatUpfront;  // If true, repeat count is chosen at cast time and cost paid upfront

    // non-json runtime field for upfront repeat
    [JsonIgnore]
    public int repeatCount;  // Stores the chosen repeat count when selectRepeatUpfront is true
    public List<Restriction>? restrictions;
    public int? restrictionMax;
    public int? restrictionMin;
    public string? affectedPlayer;  // "opponent" or "self", defaults to match sourcePlayer
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
        Effect cloned = new Effect(effect) {
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
            sourcePlayer = sourcePlayer,
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
            target = target,  // Reference copy is fine, Target is read-only config
            select = select,  // Reference copy is fine, Select is read-only config
            attacking = attacking,
            ownersControl = ownersControl,
            passives = passives?.ToList(),
            thisTurn = thisTurn,
            freeCast = freeCast,
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
            phase = phase,
            phaseOfPlayer = phaseOfPlayer,
            tokenType = tokenType,
            notTokenType = notTokenType,
            isToken = isToken,
            targetZones = targetZones?.ToList(),
            tokenPassive = tokenPassive,
            deckDestinations = deckDestinations?.ToList(),
            deckDestination = deckDestination,
            targetPlayersZone = targetPlayersZone,
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
            selectRepeatUpfront = selectRepeatUpfront,
            repeatCount = repeatCount,
            restrictions = restrictions?.ToList(),
            restrictionMax = restrictionMax,
            restrictionMin = restrictionMin,
            affectedPlayer = affectedPlayer,
            triggeredEffects = triggeredEffects?.Select(te => te.CloneWithTriggerCard(te.sourceCard, te.triggerCard, te.triggerController)).ToList(),
            additionalEffects = additionalEffects?.Select(e => e.Clone()).ToList(),
            choices = choices?.Select(c => c.Select(e => e.Clone()).ToList()).ToList(),  // Deep clone choices
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
        // Fix up additionalEffects to point to the cloned parent, not the original
        if (cloned.additionalEffects != null) {
            foreach (Effect addEffect in cloned.additionalEffects) {
                addEffect.rootEffect = cloned;
                addEffect.sourceCard = sourceCard;
            }
        }
        return cloned;
    }

    public static Effect CreateEffect(Effect e, Card sourceCard) {
        // Clone the effect to avoid accumulating state across multiple activations
        Effect cloned = e.Clone();
        cloned.subjectUid = sourceCard.uid;
        cloned.sourceCard = sourceCard;
        // Don't default amount to 1 for mill/draw with an amount-style select ("mill up to 3") -
        // those effects need player amount selection, which requires amount to stay null
        bool needsAmountSelection = cloned.select != null && cloned.select.zone == null && cloned.select.zones == null &&
                                    (cloned.effect == EffectType.Mill || cloned.effect == EffectType.Draw);
        if (!needsAmountSelection) {
            cloned.amount ??= 1;
        }
        if (cloned.additionalEffects != null) {
            foreach (Effect addEffect in cloned.additionalEffects) {
                addEffect.rootEffect = cloned;
                addEffect.sourceCard = sourceCard;
            }
        }
        return cloned;
    }

    #region Targeting and Selection Helpers

    /// <summary>
    /// Gets the target type from the target object.
    /// </summary>
    public TargetType? GetTargetType() {
        return target?.type;
    }

    /// <summary>
    /// Returns true if this effect requires targeting (in-play objects).
    /// </summary>
    public bool HasTargeting() {
        return target != null;
    }

    /// <summary>
    /// Returns true if this effect requires selection (cards from zone or amount choice).
    /// </summary>
    public bool HasSelection() {
        return select != null;
    }

    /// <summary>
    /// Gets the minimum number of targets (defaults to max for exact targeting).
    /// </summary>
    public int GetTargetMin() {
        return target?.GetMin() ?? GetTargetMax();
    }

    /// <summary>
    /// Gets the maximum number of targets (defaults to 1).
    /// </summary>
    public int GetTargetMax() {
        return target?.GetMax() ?? 1;
    }

    /// <summary>
    /// Gets the minimum selection count.
    /// For effects like mill, this is the minimum amount to mill.
    /// </summary>
    public int GetSelectMin() {
        if (select != null) return select.GetMin();
        return GetSelectMax();
    }

    /// <summary>
    /// Gets the maximum selection count (defaults to 1).
    /// For effects like mill, this is the maximum amount to mill.
    /// </summary>
    public int GetSelectMax() {
        return select?.GetMax() ?? 1;
    }

    /// <summary>
    /// Gets the zone to select from, considering both new (select.zone) and legacy (zone) fields.
    /// </summary>
    public Zone? GetSelectZone() {
        if (select?.zone != null) return select.zone;
        return zone;
    }

    /// <summary>
    /// Gets all zones to select from (for multi-zone selection).
    /// Falls back to single zone if zones array not specified.
    /// </summary>
    public List<Zone> GetSelectZones() {
        if (select != null) return select.GetZones();
        if (zone != null) return new List<Zone> { zone.Value };
        return new List<Zone>();
    }

    #endregion

    public bool ConditionsAreMet(GameMatch gameMatch, Player controllingPlayer, int? targetUidOverride = null) {
        Card? rootTarget = null;
        // Priority: targetUidOverride > rootEffect.targetUids > rootEffect.affectedUids
        int? targetUid = targetUidOverride;
        if (targetUid == null && rootEffect != null) {
            if (rootEffect.targetUids.Count > 0) {
                targetUid = rootEffect.targetUids[0];
            } else if (rootEffect.affectedUids != null && rootEffect.affectedUids.Count > 0) {
                targetUid = rootEffect.affectedUids[0];
            }
        }
        if (targetUid != null && gameMatch.cardByUid.TryGetValue(targetUid.Value, out Card? card)) {
            rootTarget = card;
        }
        bool result = conditions == null || conditions.All(condition => condition.Verify(gameMatch, controllingPlayer, rootTarget));
        return result;
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
                // Reveal a card from hand - need at least one matching card
                if (GetTargetType() == TargetType.CardInHand) {
                    Qualifier revealQualifier = new Qualifier(this, player);
                    return gameMatch.GetQualifiedCards(player.hand, revealQualifier).Count > 0;
                }
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
        // If targets have already been selected (e.g., from a previous selection prompt), no need for another selection
        if (targetUids.Count > 0) return false;

        switch (effect) {
            case EffectType.Reveal:
                // Reveal self is auto-pay
                if (scope == Scope.SelfOnly) return false;
                // Reveal from hand needs selection
                if (GetTargetType() == TargetType.CardInHand) return true;
                return false;
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
            case EffectType.Reveal:
                if (GetTargetType() == TargetType.CardInHand) {
                    Qualifier revealQualifier = new Qualifier(this, player);
                    foreach (Card c in gameMatch.GetQualifiedCards(player.hand, revealQualifier)) {
                        selectableUids.Add(c.uid);
                    }
                }
                break;
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
        if (!ConditionsAreMet(gameMatch, effectOwner, rootSubjectUid)) return;
        if (rootSubjectUid != null) subjectUid = (int)rootSubjectUid;
        // Determine source player (who is performing the action)
        Player resolvedSourcePlayer = sourcePlayer == "opponent" ? gameMatch.GetOpponent(effectOwner) : effectOwner;
        // Determine affected player (who is affected by the action)
        Player resolvedAffectedPlayer;
        // If we have targetUids and the target is a player, use that player
        if (targetUids.Count > 0 && gameMatch.IsPlayerUid(targetUids[0])) {
            resolvedAffectedPlayer = gameMatch.PlayerByUid(targetUids[0]);
        } else {
            // Use affectedPlayer field, defaulting to sourcePlayer if not specified
            switch (affectedPlayer) {
                case "opponent":
                    resolvedAffectedPlayer = gameMatch.GetOpponent(effectOwner);
                    break;
                case "self":
                    resolvedAffectedPlayer = effectOwner;
                    break;
                default:
                    resolvedAffectedPlayer = resolvedSourcePlayer;
                    break;
            }
        }
        // set amounts based on game state at the time of resolve
        // Skip PlayerChoice - the amount was already set during selection in HandleCostSelection
        if (amountBasedOn != null && amountBasedOn != AmountBasedOn.PlayerChoice) {
            amount = gameMatch.GetAmountBasedOn(amountBasedOn, scope, resolvedSourcePlayer, rootEffect, cardType, restrictions, sourceCard);
        }
        if (attackBasedOn != null) {
            attack = gameMatch.GetAmountBasedOn(attackBasedOn, scope, resolvedSourcePlayer, rootEffect, cardType, restrictions, sourceCard);
        }
        if (defenseBasedOn != null) {
            defense = gameMatch.GetAmountBasedOn(defenseBasedOn, scope, resolvedSourcePlayer, rootEffect, cardType, restrictions, sourceCard);
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
                    case "+2":
                        amount += 2;
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
        Qualifier eQualifier = new Qualifier(this, resolvedSourcePlayer);

        // resolve effect based on EffectType
        switch (effect) {
            case EffectType.CreateToken:
                affectedUids = CreateToken(gameMatch, resolvedAffectedPlayer);
                break;
            case EffectType.Draw:
                Draw(gameMatch, resolvedAffectedPlayer);
                break;
            case EffectType.Discard:
                affectedUids = new List<int>();
                if (all) {
                    // Discard entire hand (e.g., Hand Refresh)
                    List<Card> hand = resolvedAffectedPlayer.hand.ToList();
                    amount = hand.Count;  // Set amount for additionalEffects that use rootAmount
                    foreach (Card cardToDiscard in hand) {
                        gameMatch.Discard(resolvedAffectedPlayer, cardToDiscard, hand.Count);
                        affectedUids.Add(cardToDiscard.uid);
                    }
                } else if (targetUids.Count > 0) {
                    // Player-chosen discard (targetUids set by selection in StackObj)
                    // Skip player UIDs (e.g., Reap has opponent's player UID in targetUids[0])
                    foreach (int uid in targetUids) {
                        if (gameMatch.IsPlayerUid(uid)) continue;
                        if (gameMatch.cardByUid.TryGetValue(uid, out Card? cardToDiscard)) {
                            gameMatch.Discard(resolvedAffectedPlayer, cardToDiscard);
                            affectedUids.Add(uid);
                        }
                    }
                } else if (random) {
                    // Discard at random
                    Debug.Assert(amount != null, "Random discard effect requires an amount");
                    List<Card> hand = resolvedAffectedPlayer.hand.ToList();
                    for (int i = 0; i < amount && hand.Count > 0; i++) {
                        int randomIndex = new Random().Next(hand.Count);
                        Card cardToDiscard = hand[randomIndex];
                        gameMatch.Discard(resolvedAffectedPlayer, cardToDiscard);
                        affectedUids.Add(cardToDiscard.uid);
                        hand.RemoveAt(randomIndex);
                    }
                }
                // Note: Fixed-amount non-random discard is handled by StackObj before Resolve is called
                // If we get here with amount > 0 and no targetUids, the player had no cards to select
                break;
            case EffectType.LookAtDeck:
                LookAtDeck(gameMatch, resolvedAffectedPlayer);
                break;
            case EffectType.Tutor:
                Tutor(gameMatch, resolvedAffectedPlayer);
                break;
            case EffectType.Mill:
                Mill(gameMatch, resolvedAffectedPlayer);
                break;
            case EffectType.SendToZone:
                affectedUids = SendToZone(gameMatch, resolvedAffectedPlayer);
                break;
            case EffectType.GainLife:
                gameMatch.GainLife(resolvedAffectedPlayer, amount);
                break;
            case EffectType.GainControl:
                foreach (int uid in targetUids) {
                    Card targetCard = gameMatch.cardByUid[uid];
                    gameMatch.GainControl(resolvedAffectedPlayer, targetCard);
                }
                break;
            case EffectType.SetLifeTotal:
                gameMatch.SetLifeTotal(resolvedAffectedPlayer, amount);
                break;
            case EffectType.GrantKeyword:
                affectedUids = GrantKeyword(gameMatch, resolvedAffectedPlayer);
                break;
            case EffectType.CanAttack:
                // Allow summons to attack this turn (bypass summoning sickness)
                affectedUids = new List<int>();
                List<Card> attackTargets = new();
                if (all) {
                    // Get all summons of the specified tribe in play
                    foreach (Card c in resolvedAffectedPlayer.playField) {
                        if (tribe != null && c.tribe != tribe) continue;
                        if (c.type != CardType.Summon) continue;
                        attackTargets.Add(c);
                    }
                } else if (targetUids.Count > 0) {
                    foreach (int uid in targetUids) {
                        if (gameMatch.cardByUid.TryGetValue(uid, out Card? card)) {
                            attackTargets.Add(card);
                        }
                    }
                }
                foreach (Card target in attackTargets) {
                    target.hasSummoningSickness = false;
                    affectedUids.Add(target.uid);
                    Console.WriteLine($"[CanAttack] {target.name} can now attack this turn");
                }
                break;
            case EffectType.Reveal:
                affectedUids = new List<int>();
                if (all) {
                    foreach (Card c in resolvedAffectedPlayer.hand) {
                        gameMatch.Reveal(resolvedAffectedPlayer, c);
                        affectedUids.Add(c.uid);
                    }
                } else if (scope == Scope.SelfOnly) {
                    gameMatch.Reveal(resolvedAffectedPlayer, gameMatch.cardByUid[(int)subjectUid!]);
                } else if (targetUids.Count > 0) {
                    // Reveal specific targeted cards (e.g., from CardInHand target selection)
                    foreach (int uid in targetUids) {
                        gameMatch.Reveal(resolvedAffectedPlayer, gameMatch.cardByUid[uid]);
                        affectedUids.Add(uid);
                    }
                } else {
                    foreach (Card c in gameMatch.GetQualifiedCards(resolvedAffectedPlayer.hand, eQualifier)) {
                        gameMatch.Reveal(resolvedAffectedPlayer, c);
                        affectedUids.Add(c.uid);
                    }
                }
                break;
            case EffectType.LoseLife:
                gameMatch.LoseLife(resolvedAffectedPlayer, amount, restrictions);
                break;
            case EffectType.DealDamage:
                Debug.Assert(amount != null, "there is no amount for this deal damage effect");
                affectedUids = new List<int>();
                if (targetUids.Count > 0) {
                    foreach (int uid in targetUids) {
                        gameMatch.DealDamage(uid, (int)amount, isSpellDamage: true, restrictions: restrictions);
                        affectedUids.Add(uid);
                    }
                } else if (all) {
                    // Deal damage to all targets matching GetTargetType()/cardType
                    if (GetTargetType() == TargetType.Summon || cardType is CardType.Summon) {
                        foreach (Card c in gameMatch.GetQualifiedCards(gameMatch.GetAllSummonsInPlay(), eQualifier)) {
                            gameMatch.DealDamage(c.uid, (int)amount, isSpellDamage: true, restrictions: restrictions);
                            affectedUids.Add(c.uid);
                        }
                    } else if (GetTargetType() == TargetType.Opponent) {
                        // Deal damage to each opponent (in 1v1, just the opponent)
                        Player opponent = gameMatch.GetOpponent(effectOwner);
                        gameMatch.DealDamage(opponent.uid, (int)amount, isSpellDamage: true, restrictions: restrictions);
                        affectedUids.Add(opponent.uid);
                    }
                } else if (affectedPlayer != null) {
                    // Deal damage directly to a player (e.g., Exploder Gob)
                    gameMatch.DealDamage(resolvedAffectedPlayer.uid, (int)amount, isSpellDamage: true, restrictions: restrictions);
                    affectedUids.Add(resolvedAffectedPlayer.uid);
                } else if (targetBasedOn == TargetBasedOn.AttackedPlayer) {
                    // Deal damage to the controller of the attacked summon (from trigger context)
                    if (gameMatch.currentTriggerContext?.targetCard != null) {
                        Player attackedController = gameMatch.GetControllerOf(gameMatch.currentTriggerContext.targetCard);
                        gameMatch.DealDamage(attackedController.uid, (int)amount, isSpellDamage: true, restrictions: restrictions);
                        affectedUids.Add(attackedController.uid);
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
                } else if (all) {
                    // Destroy all summons matching the qualifier (e.g. Wrath)
                    var allSummons = gameMatch.GetAllSummonsInPlay();
                    Console.WriteLine($"  Destroy all - summons in play: {string.Join(", ", allSummons.Select(s => s.name))}");
                    foreach (Card c in gameMatch.GetQualifiedCards(allSummons, eQualifier)) {
                        Console.WriteLine($"  Destroying: {c.name} (uid={c.uid})");
                        gameMatch.Destroy(c);
                        affectedUids.Add(c.uid);
                    }
                } else {
                    Console.WriteLine("  No targetUids and not 'all' - fizzling");
                }
                break;
            case EffectType.GrantPassive:
                Debug.Assert(passives != null, "there are no passive for this GrantPassive effect");
                Console.WriteLine($"GrantPassive: sourceCard={sourceCard?.name ?? "NULL"}, scope={scope}, GetTargetType()={GetTargetType()}, targetUids.Count={targetUids.Count}, subjectUid={subjectUid}");
                affectedUids = new List<int>();
                if (targetUids.Count > 0) {
                    // Apply to specific targets
                    foreach (int uid in targetUids) {
                        GrantPassive(gameMatch.cardByUid[uid]);
                        affectedUids.Add(uid);
                    }
                } else if (subjectUid != null && gameMatch.cardByUid.ContainsKey(subjectUid.Value)) {
                    // Apply to subject (from additionalEffect with targetBasedOn: rootAffected)
                    Console.WriteLine($"  GrantPassive using subjectUid={subjectUid}");
                    GrantPassive(gameMatch.cardByUid[subjectUid.Value]);
                    affectedUids.Add(subjectUid.Value);
                } else if (GetTargetType() != null && !all) {
                    // Effect required a target but had none - fizzle (do nothing)
                    Console.WriteLine($"  GrantPassive fizzled - GetTargetType()={GetTargetType()} but no targets selected");
                } else {
                    // Apply to all qualified summons (for "all" effects or effects without GetTargetType())
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
                    gameMatch.AttemptToCast(resolvedAffectedPlayer, sourceCard, CastingStage.Initial, false, freeCast);
                } else if (targetUids.Count > 0) {
                    // Cast the pre-selected card(s) - handles both summons and spells
                    foreach (int targetUid in targetUids) {
                        if (gameMatch.cardByUid.TryGetValue(targetUid, out Card? targetCard)) {
                            if (targetCard.type == CardType.Spell) {
                                // For spells, cast them as a free cast
                                gameMatch.AttemptToCast(resolvedAffectedPlayer, targetCard, CastingStage.Initial, true);
                            } else {
                                // For summons, put directly into play
                                gameMatch.SendToZone(resolvedAffectedPlayer, Zone.Play, targetCard);
                            }
                        }
                    }
                }
                break;
            case EffectType.EventTriggers:
                Debug.Assert(sourceCard != null, "there is no sourceCard for this EventTriggers Effect");
                foreach (TriggeredEffect tEffect in triggeredEffects) {
                    tEffect.sourceCard = sourceCard;
                    resolvedAffectedPlayer.eventTriggers.Add(tEffect);
                }
                break;
            case EffectType.ReplacementEffect:
                // Add a player passive that modifies zone destinations (e.g., graveyard -> exile)
                Debug.Assert(replacementPassive != null, "there is no replacementPassive for this ReplacementEffect");
                PassiveEffect replacementEffect = new PassiveEffect((Passive)replacementPassive);
                replacementEffect.thisTurn = true;  // Replacement effects are always for this turn only
                resolvedAffectedPlayer.playerPassives.Add(replacementEffect);
                break;
            case EffectType.CantTribute:
                // Add a player passive that prevents tributing (0-cost summons still castable)
                PassiveEffect cantTributePassive = new PassiveEffect(Passive.CantTribute);
                cantTributePassive.thisTurn = thisTurn;  // Respect the thisTurn flag from JSON
                resolvedAffectedPlayer.playerPassives.Add(cantTributePassive);
                break;
            case EffectType.OnlySummonTribe:
                // Add a player passive that restricts summoning to only a specific tribe this turn
                Debug.Assert(tribe != null, "OnlySummonTribe effect requires a tribe");
                PassiveEffect onlySummonTribePassive = new PassiveEffect(Passive.OnlySummonTribe, (Tribe)tribe);
                onlySummonTribePassive.thisTurn = true;  // Always this turn only
                resolvedAffectedPlayer.playerPassives.Add(onlySummonTribePassive);
                Console.WriteLine($"[OnlySummonTribe] Player {resolvedAffectedPlayer.playerName} can only summon {tribe} this turn");
                break;
            case EffectType.GroundTactics:
                // Grant a player passive to the OPPONENT that forces all summons to attack
                // and gives the CASTER control of attack target assignments
                Player groundTacticsOpponent = gameMatch.GetOpponent(effectOwner);
                PassiveEffect groundTacticsPassive = new PassiveEffect(Passive.GroundTactics);
                groundTacticsPassive.thisTurn = true;  // Expires at end of turn
                groundTacticsPassive.attackControllerPlayerId = effectOwner.playerId;  // Caster controls assignments
                groundTacticsOpponent.playerPassives.Add(groundTacticsPassive);
                Console.WriteLine($"[GroundTactics] {groundTacticsOpponent.playerName} must attack with all summons, {effectOwner.playerName} controls assignments");
                break;
            case EffectType.TriggerSprout:
                // Manually trigger the Sprout keyword's herb creation for the source card
                // Used when Sprout is granted after the card has already entered play
                Debug.Assert(sourceCard != null, "TriggerSprout requires a sourceCard");
                int sproutAmount = sourceCard.GetSproutAmount();
                if (sproutAmount > 0) {
                    for (int i = 0; i < sproutAmount; i++) {
                        Token herb = new Token(TokenType.Herb, gameMatch);
                        herb.currentZone = Zone.Play;
                        gameMatch.CreateTokenForPlayer(resolvedAffectedPlayer, herb, false);
                    }
                    Console.WriteLine($"[TriggerSprout] Created {sproutAmount} herb(s) for {sourceCard.name}");
                }
                break;
            case EffectType.Sacrifice:
                Debug.Assert(sourceCard != null, "there is no sourceCard for this Sacrifice Effect");
                if (scope == Scope.SelfOnly) {
                    gameMatch.Destroy(sourceCard);
                } else if (GetTargetType() == TargetType.Opponent && targetBasedOn == TargetBasedOn.Weakest) {
                    // Target opponent sacrifices their weakest summon
                    // targetUids[0] contains the targeted opponent's player UID
                    Player targetedOpponent = targetUids.Count > 0
                        ? gameMatch.GetPlayerByUid(targetUids[0]) ?? gameMatch.GetOpponent(effectOwner)
                        : gameMatch.GetOpponent(effectOwner);
                    Card? weakest = gameMatch.GetWeakestSummon(targetedOpponent);
                    if (weakest != null) {
                        gameMatch.Destroy(weakest);
                    }
                } else if (GetTargetType() == TargetType.Opponent && targetBasedOn == TargetBasedOn.Strongest) {
                    // Target opponent sacrifices their strongest summon
                    Player targetedOpponent = targetUids.Count > 0
                        ? gameMatch.GetPlayerByUid(targetUids[0]) ?? gameMatch.GetOpponent(effectOwner)
                        : gameMatch.GetOpponent(effectOwner);
                    Card? strongest = gameMatch.GetStrongestSummon(targetedOpponent);
                    if (strongest != null) {
                        gameMatch.Destroy(strongest);
                    }
                } else if (targetUids.Count > 0) {
                    // Sacrifice user-selected targets
                    int sacrificedCount = 0;
                    foreach (int uid in targetUids) {
                        if (gameMatch.cardByUid.TryGetValue(uid, out Card? targetCard)) {
                            gameMatch.Destroy(targetCard);  // Destroy handles both cards and tokens
                            sacrificedCount++;
                        }
                    }
                    // Set amount for additionalEffects that use amountBasedOn: "rootAmount"
                    amount = sacrificedCount;
                } else if (tokenType != null) {
                    // Auto-sacrifice first matching token (only for single-token costs)
                    Token? tokenToSacrifice = effectOwner.tokens.FirstOrDefault(t => t.tokenType == tokenType);
                    if (tokenToSacrifice != null) {
                        gameMatch.Destroy(tokenToSacrifice);  // Destroy handles tokens
                    }
                }
                break;
            case EffectType.CantAttack:
                resolvedAffectedPlayer.cantAttackThisTurn = true;
                break;
            case EffectType.CantGainLife:
                // Add a player passive that prevents gaining life for the rest of the game
                PassiveEffect cantGainLifePassive = new PassiveEffect(Passive.CantGainLife);
                resolvedAffectedPlayer.playerPassives.Add(cantGainLifePassive);
                break;
            case EffectType.BypassHerbLifeReduction:
                // Herbs always give 2 LP this turn (bypass the diminishing returns)
                resolvedAffectedPlayer.bypassHerbLifeReduction = true;
                break;
            case EffectType.ExileAndReturn:
                // Exile a card and return it to play (flicker effect)
                affectedUids = new List<int>();
                List<Card> flickerTargets = new();

                if (targetUids.Count > 0) {
                    // Flicker targeted cards
                    foreach (int uid in targetUids) {
                        if (gameMatch.cardByUid.TryGetValue(uid, out Card? targetCard) && targetCard.currentZone == Zone.Play) {
                            flickerTargets.Add(targetCard);
                        }
                    }
                } else if (sourceCard != null && sourceCard.currentZone == Zone.Play) {
                    // Flicker self (original behavior)
                    flickerTargets.Add(sourceCard);
                }

                foreach (Card flickerTarget in flickerTargets) {
                    Player controller = gameMatch.GetControllerOf(flickerTarget);
                    // Send to exile
                    gameMatch.SendToZone(controller, Zone.Exile, flickerTarget);
                    Console.WriteLine($"[ExileAndReturn] Exiled {flickerTarget.name}");
                    affectedUids.Add(flickerTarget.uid);

                    if (duration == "drawPhase") {
                        // Schedule return for the effect owner's next draw phase
                        gameMatch.ScheduleExiledCardReturn(resolvedSourcePlayer.playerId, flickerTarget);
                        Console.WriteLine($"[ExileAndReturn] Scheduled {flickerTarget.name} to return on {resolvedSourcePlayer.playerName}'s next draw phase");
                    } else {
                        // Immediate return (flicker effect)
                        // Remove from exile list before summoning back to play
                        controller.exile.Remove(flickerTarget);
                        // Return it to play (not attacking)
                        gameMatch.Summon(flickerTarget, controller, false);
                        Console.WriteLine($"[ExileAndReturn] Returned {flickerTarget.name} to play");
                    }
                }
                break;
            case EffectType.AddCounter:
                Debug.Assert(counterType != null, "there is no counterType for this AddCounter Effect");
                int counterAmount = amount ?? 1;
                List<Card> counterTargets = new();

                if (all) {
                    // Add counters to all matching summons (both players)
                    foreach (Card c in gameMatch.playerOne.playField.Concat(gameMatch.playerTwo.playField)) {
                        if (tribe != null && c.tribe != tribe) continue;
                        if (c.type != CardType.Summon && c.type != CardType.Token) continue;
                        counterTargets.Add(c);
                    }
                    // Also check tokens (both players)
                    foreach (Token t in gameMatch.playerOne.tokens.Concat(gameMatch.playerTwo.tokens)) {
                        if (tribe != null && t.tribe != tribe) continue;
                        counterTargets.Add(t);
                    }
                } else if (scope == Scope.SelfOnly) {
                    Debug.Assert(sourceCard != null, "there is no sourceCard for this AddCounter Effect");
                    counterTargets.Add(sourceCard);
                } else if (targetUids.Count > 0) {
                    counterTargets.Add(gameMatch.cardByUid[targetUids[0]]);
                } else {
                    Debug.Assert(sourceCard != null, "there is no sourceCard for this AddCounter Effect");
                    counterTargets.Add(sourceCard);
                }

                foreach (Card counterTarget in counterTargets) {
                    if (counterType == "oneOne") {
                        counterTarget.plusOnePlusOneCounters += counterAmount;
                    } else if (counterType == "minusOneMinusOne") {
                        counterTarget.minusOneMinusOneCounters += counterAmount;
                    }
                    // Send refresh event to update the card display for both players
                    GameEvent counterRefreshEvent = GameEvent.CreateRefreshCardDisplayEvent(counterTarget);
                    effectOwner.eventList.Add(counterRefreshEvent);
                    gameMatch.GetOpponent(effectOwner).eventList.Add(counterRefreshEvent);
                    Console.WriteLine($"[AddCounter] Added {counterAmount} {counterType} counter(s) to {counterTarget.name}");
                }
                break;
            case EffectType.RemoveCounter:
                Debug.Assert(counterType != null, "there is no counterType for this RemoveCounter Effect");
                if (all) {
                    // Remove counters from all summons in play
                    List<Card> allSummons = gameMatch.playerOne.playField.Concat(gameMatch.playerTwo.playField).ToList();
                    foreach (Card summon in allSummons) {
                        bool hadCounters = false;
                        if (counterType == "haunt" && summon.hauntCounters > 0) {
                            summon.hauntCounters = 0;
                            hadCounters = true;
                        } else if (counterType == "oneOne" && summon.plusOnePlusOneCounters > 0) {
                            summon.plusOnePlusOneCounters = 0;
                            hadCounters = true;
                        } else if (counterType == "minusOneMinusOne" && summon.minusOneMinusOneCounters > 0) {
                            summon.minusOneMinusOneCounters = 0;
                            hadCounters = true;
                        }
                        if (hadCounters) {
                            GameEvent refreshEvent = GameEvent.CreateRefreshCardDisplayEvent(summon);
                            effectOwner.eventList.Add(refreshEvent);
                            gameMatch.GetOpponent(effectOwner).eventList.Add(refreshEvent);
                            Console.WriteLine($"[RemoveCounter] Removed all {counterType} counters from {summon.name}");
                        }
                    }
                } else {
                    // Remove counters from specific target
                    Card removeTarget = targetUids.Count > 0 ? gameMatch.cardByUid[targetUids[0]] : sourceCard!;
                    if (counterType == "haunt") {
                        removeTarget.hauntCounters = 0;
                    } else if (counterType == "oneOne") {
                        removeTarget.plusOnePlusOneCounters = 0;
                    } else if (counterType == "minusOneMinusOne") {
                        removeTarget.minusOneMinusOneCounters = 0;
                    }
                    GameEvent removeRefreshEvent = GameEvent.CreateRefreshCardDisplayEvent(removeTarget);
                    effectOwner.eventList.Add(removeRefreshEvent);
                    gameMatch.GetOpponent(effectOwner).eventList.Add(removeRefreshEvent);
                }
                break;
            case EffectType.ModifyType:
                // Convert tokens of specified type to summons
                Debug.Assert(tokenType != null, "there is no tokenType for this ModifyType effect");
                affectedUids = ModifyType(gameMatch, resolvedAffectedPlayer);
                break;
            case EffectType.CardRitualOfDarkness:
                // Start the Ritual of Darkness - players alternate putting summons into play
                gameMatch.StartRitualOfDarkness(resolvedAffectedPlayer);
                break;
            case EffectType.RepeatAllEffects:
                Debug.Assert(parentEffectList != null, "RepeatAllEffects has no parent effect list to repeat");
                // Determine how many times to repeat (default 1)
                int repeatCount = amount ?? 1;
                if (amountBasedOn == AmountBasedOn.X && sourceCard?.x != null) {
                    repeatCount = sourceCard.x.Value;
                }
                // Find index of this RepeatAllEffects in parent list - only repeat effects before it
                int repeatEffectIndex = parentEffectList.IndexOf(this);
                List<Effect> effectsToRepeat = repeatEffectIndex > 0
                    ? parentEffectList.Take(repeatEffectIndex).ToList()
                    : parentEffectList.Where(e => e.effect != EffectType.RepeatAllEffects).ToList();
                // Add repeat stack objects for each repetition
                for (int i = 0; i < repeatCount; i++) {
                    // Clone the effects and add them as a new trigger on the stack
                    List<Effect> clonedEffects = effectsToRepeat.Select(e => e.Clone()).ToList();
                    // Set sourceCard on cloned effects and enable resolve-time targeting
                    foreach (Effect clonedEffect in clonedEffects) {
                        clonedEffect.sourceCard = sourceCard;
                        // Clear existing targets and enable resolve-time targeting for effects that need targets
                        if (clonedEffect.HasTargeting()) {
                            clonedEffect.targetUids.Clear();
                            clonedEffect.resolveTarget = true;
                        }
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
                }
                break;
            case EffectType.Counter:
                // Counter a spell/summon on the stack - remove it and send to graveyard
                affectedUids = new List<int>();
                if (all) {
                    // Counter all spells on the stack (except this counter spell itself)
                    List<int> stackUidsToCounter = new();
                    foreach (StackObj stackObj in gameMatch.stack) {
                        if (stackObj.sourceCard?.uid == sourceCard?.uid) continue;  // Skip self
                        stackUidsToCounter.Add(stackObj.sourceCard?.uid ?? stackObj.effects.FirstOrDefault()?.subjectUid ?? 0);
                    }
                    foreach (int uid in stackUidsToCounter) {
                        if (gameMatch.CounterStackItem(uid)) {
                            affectedUids.Add(uid);
                        }
                    }
                } else {
                    Debug.Assert(targetUids.Count > 0, "Counter effect requires a target on the stack");
                    foreach (int uid in targetUids) {
                        if (gameMatch.CounterStackItem(uid)) {
                            affectedUids.Add(uid);
                        }
                    }
                }
                break;
            case EffectType.ModifyHandSize:
                Debug.Assert(amount != null, "ModifyHandSize effect requires an amount");
                resolvedAffectedPlayer.maxHandSize += amount.Value;
                break;
            case EffectType.ModifySummonLimit:
                Debug.Assert(amount != null, "ModifySummonLimit effect requires an amount");
                resolvedAffectedPlayer.turnSummonLimitBonus += amount.Value;
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
                gameMatch.ShuffleDeck(resolvedAffectedPlayer.deck);
                break;
            case EffectType.ExtraTurn:
                resolvedAffectedPlayer.extraTurns++;
                break;
            case EffectType.ModifyCost:
                // Handle "next spell free" effect
                if (targetBasedOn == TargetBasedOn.NextSpell) {
                    resolvedSourcePlayer.nextSpellFree = true;
                    // Refresh hand cards to show updated costs
                    gameMatch.RefreshCards(resolvedSourcePlayer, resolvedSourcePlayer.hand, false);
                }
                break;
            case EffectType.CopySpell:
                // Set up copying the next spell (with optional cost restriction)
                if (targetBasedOn == TargetBasedOn.NextSpell) {
                    resolvedSourcePlayer.copyNextSpell = new CopySpellInfo(sourceCard!, restrictionMax);
                    Console.WriteLine($"[CopySpell] {resolvedSourcePlayer.playerName} will copy next spell with cost <= {restrictionMax?.ToString() ?? "any"}");
                }
                break;
            case EffectType.EndTurn:
                // End the current player's turn immediately
                Console.WriteLine($"[EndTurn] Ending {resolvedSourcePlayer.playerName}'s turn");
                gameMatch.EndCurrentTurn();
                break;
            case EffectType.GoToPhase:
                // Go to a specific phase (for Rewind - opponent restarts their turn)
                Debug.Assert(phase != null, "GoToPhase effect requires a phase");
                Player phasePlayer = sourcePlayer == "opponent" ? gameMatch.GetOpponent(resolvedSourcePlayer) : resolvedSourcePlayer;
                Console.WriteLine($"[GoToPhase] {phasePlayer.playerName} goes to {phase} phase");
                gameMatch.GoToPhase(phasePlayer, (Phase)phase);
                break;
            default:
                 Console.WriteLine("Effect " + effect + " doesn't exist or is not implemented");
                 break;
        }

        // TODO check for optional effects, set their subjectUids and wait for response from player
        // resolve additional effects
        if (additionalEffects != null) {
            // Store affectedUids on this effect so additionalEffects can access via rootEffect
            this.affectedUids = affectedUids;

            foreach (Effect addEffect in additionalEffects) {
                // Propagate parentEffectList so RepeatAllEffects can access it
                addEffect.parentEffectList = parentEffectList;
                if (addEffect.targetBasedOn == TargetBasedOn.RootAffected) {
                    Debug.Assert(affectedUids != null,
                        "There is no affected entities or players for this additional effect");
                    foreach (int uid in affectedUids) {
                        addEffect.Resolve(gameMatch, resolvedAffectedPlayer, uid);
                    }
                } else if (addEffect.targetBasedOn == TargetBasedOn.RootController) {
                    // Resolve effect for the controller of the root-affected target (e.g., Glowing Spore)
                    Debug.Assert(affectedUids != null && affectedUids.Count > 0,
                        "There is no affected entity for RootController additional effect");
                    Card affectedCard = gameMatch.cardByUid[affectedUids[0]];
                    // Use lastControllingPlayer for destroyed cards, GetControllerOf for cards still in play
                    Player targetController = affectedCard.currentZone == Zone.Play
                        ? gameMatch.GetControllerOf(affectedCard)
                        : affectedCard.lastControllingPlayer ?? gameMatch.GetOwnerOf(affectedCard);
                    addEffect.Resolve(gameMatch, targetController);
                } else if (addEffect.targetBasedOn == TargetBasedOn.SourcePlayer) {
                    // Resolve effect for the caster, once per affected card (e.g., Set Straight)
                    Debug.Assert(affectedUids != null,
                        "There are no affected cards for SourcePlayer additional effect");
                    foreach (int uid in affectedUids) {
                        addEffect.Resolve(gameMatch, resolvedSourcePlayer, uid);
                    }
                } else {
                    addEffect.Resolve(gameMatch, resolvedAffectedPlayer);
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
        int kwAmount = keywordAmount ?? 1;

        // Handle NextSpellOpponent - add a player passive that grants keyword to next spell
        // Note: resolvedAffectedPlayer is already set correctly by sourcePlayer in the JSON, so don't flip again
        if (targetBasedOn == TargetBasedOn.NextSpellOpponent) {
            PassiveEffect keywordPassive = new PassiveEffect(Passive.GrantKeywordToNextSpell, (Keyword)keyword);
            affectedPlayer.playerPassives.Add(keywordPassive);
            return tempAffectedUids;
        }

        // Handle nextSummon with tribe filter - grant keyword to next matching summon
        if (targetBasedOn == TargetBasedOn.NextSummon) {
            PassiveEffect nextSummonPassive = new PassiveEffect(Passive.GrantKeywordToNextSummon, (Keyword)keyword);
            nextSummonPassive.keywordAmount = kwAmount;
            nextSummonPassive.tribe = tribe;
            nextSummonPassive.thisTurn = thisTurn;
            affectedPlayer.playerPassives.Add(nextSummonPassive);
            Console.WriteLine($"[GrantKeyword] Added GrantKeywordToNextSummon passive: keyword={keyword}, amount={kwAmount}, tribe={tribe}");
            return tempAffectedUids;
        }

        // Handle "all" - grant to all matching tokens/cards
        if (all) {
            // Get all tokens from player
            List<Card> targets = new List<Card>();

            if (isToken) {
                // Include summon-type tokens from playField
                foreach (Card c in affectedPlayer.playField) {
                    if (c is Token t) {
                        if (notTokenType != null && t.tokenType == notTokenType) continue;
                        targets.Add(c);
                    }
                }
                // Include non-summon tokens from tokens list
                foreach (Token t in affectedPlayer.tokens) {
                    if (notTokenType != null && t.tokenType == notTokenType) continue;
                    targets.Add(t);
                }
            }

            foreach (Card c in targets) {
                gameMatch.GrantKeyword(affectedPlayer, c, (Keyword)keyword, kwAmount, thisTurn);
                tempAffectedUids.Add(c.uid);
                Console.WriteLine($"[GrantKeyword] Granted {keyword} {kwAmount} to {c.name}");
            }

            // If futureProof, add player passive for future matching cards
            if (futureProof) {
                PassiveEffect futurePassive = new PassiveEffect(Passive.GrantKeywordToFutureTokens, (Keyword)keyword);
                futurePassive.keywordAmount = kwAmount;
                futurePassive.notTokenType = notTokenType;
                futurePassive.thisTurn = true;  // Expires at end of turn
                affectedPlayer.playerPassives.Add(futurePassive);
                Console.WriteLine($"[GrantKeyword] Added futureProof passive for {keyword} {kwAmount} to non-{notTokenType} tokens");
            }

            return tempAffectedUids;
        }

        // Normal case - grant to specific target(s)
        // First check targetUids (from target selection), then fall back to subjectUid
        if (targetUids.Count > 0) {
            foreach (int uid in targetUids) {
                if (gameMatch.cardByUid.TryGetValue(uid, out Card? targetCard)) {
                    gameMatch.GrantKeyword(affectedPlayer, targetCard, (Keyword)keyword, kwAmount, thisTurn);
                    tempAffectedUids.Add(uid);
                    Console.WriteLine($"[GrantKeyword] Granted {keyword} {kwAmount} to {targetCard.name} (from targetUids)");
                }
            }
        } else if (subjectUid != null) {
            gameMatch.GrantKeyword(affectedPlayer, gameMatch.cardByUid[(int)subjectUid], (Keyword)keyword, kwAmount, thisTurn);
            tempAffectedUids.Add((int)subjectUid);
        }
        return tempAffectedUids;
    }
    

    private void Mill(GameMatch gameMatch, Player affectedPlayer) {
        // Use amount if set, otherwise fallback to select.max (for cases where selection was skipped)
        int millAmount = amount ?? select?.max ?? 0;
        if (millAmount > 0) {
            gameMatch.Mill(affectedPlayer, millAmount);
        }
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
        foreach (DeckDestination dd in dDestinations) {
            // Use DeckDestination's own filters (tribe, cardType) if present, otherwise use Effect's filters
            bool hasFilter = dd.GetTribe() != null || dd.GetCardType() != null;
            Qualifier ddQualifier = hasFilter
                ? new Qualifier(dd, sourcePlayer)
                : new Qualifier(this, sourcePlayer);
            List<int> qualifiedUids = ToUids(gameMatch.GetQualifiedCards(cardsToLookAt, ddQualifier));

            // Use DeckDestination's helper methods to get selection min/max
            // These handle both new (select) and legacy (amount, amountIsPlayerChosen) formats
            int selectionMin = dd.GetSelectionMin(qualifiedUids.Count);
            int selectionMax = dd.GetSelectionMax(qualifiedUids.Count);

            // If this is a remainder destination, set min/max to 0 (auto-filled)
            if (dd.IsRemainder()) {
                selectionMin = 0;
                selectionMax = 0;
            }

            // Cap selection to available qualified cards
            // If there are fewer qualified cards than required, allow selecting all available
            if (selectionMax > qualifiedUids.Count) {
                selectionMax = qualifiedUids.Count;
            }
            if (selectionMin > qualifiedUids.Count) {
                selectionMin = qualifiedUids.Count;
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
    private List<int> CreateToken(GameMatch gameMatch, Player affectedPlayer) {
        List<int> createdUids = new();

        // Check for CreateTokenModifier passive (e.g., Tree of Abundance doubles token creation)
        int tokenAmount = amount ?? 1;
        foreach (Card c in affectedPlayer.playField) {
            if (c.passiveEffects == null) continue;
            foreach (PassiveEffect pe in c.passiveEffects) {
                if (pe.passive != Passive.CreateTokenModifier) continue;
                if (pe.amountModifier == "*2") {
                    tokenAmount *= 2;
                    Console.WriteLine($"[CreateTokenModifier] {c.name} doubled token creation to {tokenAmount}");
                }
            }
        }

        for (int i = 0; i < tokenAmount; i++) {
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
                        string tributeText = "Can only be tributed to " + tokenPassive.tribe +
                                             GetPluralityBasedOnWord(tokenPassive.tribe.ToString()) + ".";
                        // Update both description and displayDescription for client visibility
                        newToken.description = string.IsNullOrEmpty(newToken.description)
                            ? tributeText
                            : newToken.description + " " + tributeText;
                        newToken.displayDescription = string.IsNullOrEmpty(newToken.displayDescription)
                            ? tributeText
                            : newToken.displayDescription + " " + tributeText;
                        break;
                    default:
                        newToken.passiveEffects.Add(new PassiveEffect(tokenPassive.passive, tokenPassive.tribe, tokenPassive.scope));
                        break;
                }
            }

            // Add ThisTurn passive if the effect specifies thisTurn - token will be destroyed at end of turn
            if (thisTurn) {
                newToken.grantedPassives.Add(new PassiveEffect(Passive.ThisTurn));
                Console.WriteLine($"Token {tokenType} will be destroyed at end of turn (thisTurn=true)");
            }

            Console.WriteLine($"Created token from effect: {tokenType} (atk={newToken.attack}, def={newToken.defense})");
            gameMatch.CreateTokenForPlayer(affectedPlayer, newToken, attacking);
            createdUids.Add(newToken.uid);
        }
        return createdUids;
    }

    /// <summary>
    /// Converts tokens of a specified type to summons and moves them to play.
    /// Used by effects like Spreading Thornbush that animate herbs into creatures.
    /// </summary>
    private List<int> ModifyType(GameMatch gameMatch, Player affectedPlayer) {
        List<int> affectedUids = new();
        Debug.Assert(tokenType != null, "ModifyType requires a tokenType");

        // Find all tokens of the specified type
        // If rootEffect has affectedUids (from additionalEffects), only convert those specific tokens
        List<Token> tokensToConvert;
        if (rootEffect?.affectedUids != null && rootEffect.affectedUids.Count > 0) {
            tokensToConvert = affectedPlayer.tokens
                .Where(t => t.tokenType == tokenType && rootEffect.affectedUids.Contains(t.uid))
                .ToList();
        } else {
            tokensToConvert = affectedPlayer.tokens
                .Where(t => t.tokenType == tokenType)
                .ToList();
        }

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

        // Handle delayed phase effects (for cards like Endless Garden that return at a specific phase)
        if (phase != null && destination != null) {
            // Determine which player's phase this effect fires on
            int targetPlayerId = affectedPlayer.playerId;
            if (phaseOfPlayer == "opponent") {
                targetPlayerId = gameMatch.GetOpponent(affectedPlayer).playerId;
            }

            // Handle self-targeting (scope: selfOnly with subjectUid)
            if (subjectUid != null) {
                Card subjectCard = gameMatch.cardByUid[(int)subjectUid];
                gameMatch.ScheduleDelayedZoneEffect(subjectCard, (Zone)destination, (Phase)phase, targetPlayerId);
                tempAffectedUids.Add((int)subjectUid);
                return tempAffectedUids;
            }

            // Handle targeted cards
            if (targetUids.Count > 0) {
                foreach (int uid in targetUids) {
                    if (!gameMatch.cardByUid.ContainsKey(uid)) continue;
                    Card targetCard = gameMatch.cardByUid[uid];
                    gameMatch.ScheduleDelayedZoneEffect(targetCard, (Zone)destination, (Phase)phase, targetPlayerId);
                    tempAffectedUids.Add(uid);
                }
                return tempAffectedUids;
            }
        }

        // Handle targetPlayersZone - targeting a player to affect all cards in their zone
        if (targetPlayersZone && targetUids.Count > 0 && targetZones != null && targetZones.Count > 0) {
            int playerUid = targetUids[0];
            Player? targetPlayer = gameMatch.GetPlayerByUid(playerUid);
            if (targetPlayer != null) {
                Zone playerSourceZone = targetZones[0];
                List<Card> cardsInZone = playerSourceZone switch {
                    Zone.Graveyard => targetPlayer.graveyard.ToList(),
                    Zone.Hand => targetPlayer.hand.ToList(),
                    Zone.Deck => targetPlayer.deck.ToList(),
                    Zone.Play => targetPlayer.playField.ToList(),
                    _ => new List<Card>()
                };
                foreach (Card c in cardsInZone) {
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

        // Handle targeted sends (when targetUids is populated from target selection)
        if (targetUids.Count > 0) {
            // If we have deckDestinations (multiple destinations like graveyard/deck), use LookAtDeck panel
            if (deckDestinations != null && destination == Zone.Deck) {
                List<Card> cardsToSelect = targetUids
                    .Where(uid => gameMatch.cardByUid.ContainsKey(uid))
                    .Select(uid => gameMatch.cardByUid[uid])
                    .ToList();
                List<CardDisplayData> cddsToSelect = cardsToSelect.Select(card => new CardDisplayData(card)).ToList();
                List<CardSelectionData> selectionDatas = GetCardSelectionDatas(deckDestinations, cardsToSelect, gameMatch, affectedPlayer);
                gameMatch.LookAtDeck(affectedPlayer, deckDestinations, cddsToSelect, selectionDatas);
                return tempAffectedUids;
            }

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

        Zone? sourceZone = GetSelectZone();
        if (sourceZone != null) {
            switch (sourceZone) {
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
                        if (destination == Zone.Deck && deckDestination != null) {
                            DeckDestination dd = new DeckDestination((DeckDestinationType)deckDestination, false, false);
                            gameMatch.SendToZone(affectedPlayer, Zone.Deck, c, dd);
                        } else {
                            gameMatch.SendToZone(affectedPlayer, (Zone)destination, c);
                        }
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
            Card targetCard = gameMatch.cardByUid[(int)subjectUid];
            if (destination == Zone.Deck && deckDestination != null) {
                DeckDestination dd = new DeckDestination((DeckDestinationType)deckDestination, false, false);
                gameMatch.SendToZone(affectedPlayer, Zone.Deck, targetCard, dd);
            } else {
                gameMatch.SendToZone(affectedPlayer, (Zone)destination!, targetCard);
            }
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
        bool useOpponentPronoun = sourcePlayer == "opponent" && !forOpponentChoice;
        string verbAgreement = useOpponentPronoun ? "s " : " ";
        string pronoun = useOpponentPronoun ? "opponent " : "";
        string tempString = "error: no effect string";
        string target = "card";
        // set default target for effects with GetTargetType() or scope=SelfOnly
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

                    // Apply amountModifier to display amount (e.g., "/2down" for half)
                    if (amountModifier != null) {
                        switch (amountModifier) {
                            case "/2down":
                                displayTokenAmount = (int)Math.Floor(displayTokenAmount / 2f);
                                break;
                            case "+1":
                                displayTokenAmount++;
                                break;
                            case "-2":
                                displayTokenAmount -= 2;
                                if (displayTokenAmount < 0) displayTokenAmount = 0;
                                break;
                            default:
                                throw new NotImplementedException($"amountModifier {amountModifier} not implemented in CreateToken description");
                        }
                    }
                }
                plurality = displayTokenAmount == 1 ? "" : "s";
                // this looks ugly, but it does what it needs to: creates the string for the effect
                // depending on what the effect is.
                string attackingString = " ";
                string thisTurnString = "";
                if (attacking) attackingString = " attacking ";
                if (thisTurn) thisTurnString = " this turn";
                if (tokenType != TokenType.Herb && tokenType != TokenType.Stone) {
                    string keywordString = "";
                    if (keywords != null && keywords.Count > 0) {
                        keywordString = " with " + string.Join(", ", keywords.Select(k => k.ToString()));
                    }
                    tempString = "create " + displayTokenAmount + attackingString + attack + "/" + defense + " " + tokenType + " token" + plurality + keywordString + thisTurnString + ".";
                } else {
                    tempString = "create " + displayTokenAmount + " " + tokenType + " token" + plurality + ".";
                }
                break;
            case EffectType.Draw:
                plurality = amount == 1 ? "" : "s";
                if (GetTargetType() == TargetType.Opponent) {
                    tempString = "target opponent draws " + amount + " card" + plurality + ".";
                } else {
                    tempString = pronoun + "draw" + verbAgreement + amount + " card" + plurality + ".";
                }
                break;
            case EffectType.Mill:
                plurality = amount == 1 ? "" : "s";
                if (amountBasedOn != null) {
                    switch (amountBasedOn) {
                        case AmountBasedOn.UntilCardType:
                            tempString = "mill until you mill a " + cardType + ".";
                            break;
                        case AmountBasedOn.DeckSize:
                            string modifierText = amountModifier == "/2down" ? "half your deck rounded down" : "cards equal to your deck size";
                            tempString = "mill " + modifierText + ".";
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
                // Handle exile from graveyard specially
                if (destination == Zone.Exile && GetTargetType() == TargetType.CardInGraveyard) {
                    int exileAmount = amount ?? 1;
                    string plural = exileAmount == 1 ? "" : "s";
                    tempString = "exile " + exileAmount + " card" + plural + " from a graveyard.";
                    break;
                }
                string targetDesc = "";
                int targetAmount = amount ?? 1;
                if (tribe != null) {
                    targetDesc = targetAmount == 1 ? "a " + tribe.ToString()!.ToLower() : targetAmount + " " + tribe.ToString()!.ToLower() + "s";
                } else if (cardType != null) {
                    targetDesc = targetAmount == 1 ? "a " + cardType.ToString()!.ToLower() : targetAmount + " " + cardType.ToString()!.ToLower() + "s";
                } else if (GetTargetType() != null) {
                    targetDesc = "target " + GetTargetType().ToString()!.ToLower();
                } else {
                    targetDesc = targetAmount == 1 ? "a card" : targetAmount + " cards";
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
                    Zone.Exile => "exile",
                    _ => destination.ToString()!.ToLower()
                };
                // Use appropriate verb based on destination
                string verb = destination switch {
                    Zone.Play => "put",
                    Zone.Exile => "exile",
                    _ => "return"
                };
                string prep = destination == Zone.Exile ? "" : " into ";
                tempString = verb + " " + targetDesc + sourceDesc + prep + destString + ".";
                break;
            case EffectType.GainLife:
                if (sourcePlayer == "opponent") {
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
                gainControlTargetDesc += GetTargetType()?.ToString().ToLower() ?? "permanent";
                tempString = "gain control of " + gainControlTargetDesc + ".";
                break;
            case EffectType.LoseLife:
                if (sourcePlayer == "opponent") {
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
                pronoun = sourcePlayer == "opponent" ? "opponent's" : "your";
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
                } else if (GetTargetType() == TargetType.CardInHand) {
                    string tribeStr = tribe != null ? tribe.ToString()!.ToLower() + " " : "";
                    tempString = "reveal a " + tribeStr + "card from your hand";
                } else {
                    tempString = "reveal a card";
                }
                break;
            case EffectType.Tutor:
                string ending = reveal ? " and reveal it." : ".";
                string tutorDestination = destination?.ToString().ToLower() ?? "hand";
                tempString = "tutor a " + target + " to your " + tutorDestination + ending;
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
                TargetType? dmgTargetType = GetTargetType();
                if (dmgTargetType != null) {
                    if (dmgTargetType == TargetType.Any) {
                        tempString = "deal " + amount + " damage to any target";
                    } else {
                        tempString = "deal " + amount + " damage to target " + dmgTargetType;
                    }
                } else if (affectedPlayer != null) {
                    string playerTarget = affectedPlayer == "opponent" ? "each opponent" : "you";
                    tempString = "deal " + amount + " damage to " + playerTarget;
                } else if (targetBasedOn == TargetBasedOn.AttackedPlayer) {
                    tempString = "deal " + amount + " damage to that summon's controller";
                }
                break;
            case EffectType.Sacrifice:
                if (GetTargetType() == TargetType.Opponent) {
                    tempString = "choose target opponent";
                } else if (tokenType != null && select?.upToAll == true) {
                    // Sacrifice any number of tokens with additional effect
                    string tokenName = tokenType.ToString()!.ToLower();
                    tempString = "sacrifice any number of " + tokenName + "s";
                    if (additionalEffects != null && additionalEffects.Count > 0) {
                        var addEffect = additionalEffects[0];
                        if (addEffect.effect == EffectType.CreateToken) {
                            string createdToken = addEffect.tokenType?.ToString()?.ToLower() ?? "token";
                            tempString += " and create that many " + addEffect.attack + "/" + addEffect.defense + " " + createdToken + " tokens";
                        }
                    }
                } else {
                    tempString = "sacrifice " + target;
                }
                break;
            case EffectType.CantGainLife:
                tempString = "you can't gain life for the rest of the game";
                break;
            case EffectType.ExileAndReturn:
                tempString = "exile " + (sourceCard?.name ?? "this") + " and return it to play";
                break;
            case EffectType.AddCounter:
                string counterDesc = counterType == "oneOne" ? "+1/+1" : (counterType == "minusOneMinusOne" ? "-1/-1" : counterType ?? "");
                int numCounters = amount ?? 1;
                string counterPlural = numCounters == 1 ? "counter" : "counters";
                tempString = "put " + numCounters + " " + counterDesc + " " + counterPlural + " on " + target;
                break;
            case EffectType.RemoveCounter:
                string removeCounterDesc = counterType == "oneOne" ? "+1/+1" : (counterType == "minusOneMinusOne" ? "-1/-1" : counterType ?? "");
                if (all) {
                    tempString = "remove all " + removeCounterDesc + " counters in play";
                } else {
                    tempString = "remove all " + removeCounterDesc + " counters from " + target;
                }
                break;
            case EffectType.GrantKeyword:
                Debug.Assert(keyword != null, "there's no keyword for this GrantKeyword Effect");
                if (targetBasedOn == TargetBasedOn.NextSpellOpponent) {
                    tempString = "the next spell your opponent casts has " + keyword.ToString()!.ToLower() + ".";
                } else {
                    tempString = "grant " + keyword.ToString()!.ToLower() + " to " + target + ".";
                }
                break;
            case EffectType.CanAttack:
                if (all && tribe != null) {
                    tempString = $"all {tribe.ToString()!.ToLower()} may attack this turn";
                } else {
                    tempString = $"{target} may attack this turn";
                }
                break;
            case EffectType.Counter:
                // Build counter description based on GetTargetType() and restrictions
                string counterTarget = GetTargetType()?.ToString()?.ToLower() ?? "spell";
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
                string randomStr = random ? " at random" : "";
                if (GetTargetType() == TargetType.Opponent) {
                    plurality = amount == 1 ? "" : "s";
                    tempString = "target opponent discards " + amount + " card" + plurality + randomStr + ".";
                } else if (select?.upToAll == true) {
                    tempString = pronoun + "discard" + verbAgreement + " any amount" + randomStr + ".";
                } else {
                    plurality = amount == 1 ? "" : "s";
                    tempString = pronoun + "discard" + verbAgreement + amount + " card" + plurality + randomStr + ".";
                }
                break;
            case EffectType.ExtraTurn:
                tempString = pronoun + "take" + verbAgreement + " an extra turn after this one.";
                break;
            case EffectType.Destroy:
                if (GetTargetType() != null) {
                    tempString = "destroy target " + GetTargetType().ToString()!.ToLower() + ".";
                } else if (all) {
                    string destroyTarget = cardType?.ToString()?.ToLower() ?? "summon";
                    tempString = "destroy all " + destroyTarget + "s.";
                } else if (scope == Scope.SelfOnly) {
                    tempString = "destroy " + (sourceCard?.name ?? "this") + ".";
                } else {
                    tempString = "destroy target.";
                }
                break;
            case EffectType.CopySpell:
                if (targetBasedOn == TargetBasedOn.NextSpell) {
                    string costRestriction = "";
                    if (restrictions != null && restrictions.Contains(Restriction.Cost) && restrictionMax != null) {
                        costRestriction = $" with LP cost {restrictionMax} or less";
                    }
                    tempString = $"copy the next spell you cast{costRestriction}. You may choose new targets for the copy.";
                } else {
                    tempString = "copy target spell. You may choose new targets for the copy.";
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