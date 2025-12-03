using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Server.CardProperties;

public class Effect {
    public EffectType effect;
    public List<Condition>? conditions;
    public bool mandatoryTarget;
    public bool futureProof;
    public bool other;
    public int? amount;
    public int? amountToHand;
    public int? attack;
    public int? defense;
    public AmountBasedOn? attackBasedOn;
    public AmountBasedOn? defenseBasedOn;
    public bool reveal;
    public bool self;
    public bool isOpponent;
    public bool optional;
    public bool opponentsChoice;
    public string? optionMessage;
    public bool all;
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
    public int? keywordAmount;
    public CardType? cardType;
    public Tribe? tribe;
    public Zone? zone;
    public Zone? destination;
    public TokenType? tokenType;
    public List<Zone>? targetZones;
    public PassiveEffect? tokenPassive;
    public List<DeckDestination>? deckDestinations;
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
    public string? player;
    public List<TriggeredEffect> triggeredEffects;
    public List<Effect>? additionalEffects;
    public List<List<Effect>>? choices;
    public int? choiceIndex;
    public string? description;

    // non-json
    public List<int> targetUids = new();
    public Card? sourceCard;
    public int? subjectUid;
    public Effect? rootEffect;
    public List<int>? affectedUids;

    public Effect(EffectType effect) {
        this.effect = effect;
    }

    public static Effect CreateEffect(Effect e, Card sourceCard) {
        e.subjectUid = sourceCard.uid;
        e.amount ??= 1;
        if (e.additionalEffects != null) {
            foreach (Effect addEffect in e.additionalEffects) {
                addEffect.rootEffect = e;
            }
        }
        return e;
    }

    public bool ConditionsAreMet(GameMatch gameMatch, Player controllingPlayer) {
        Card? rootTarget = null;
        if (rootEffect != null && rootEffect.targetUids.Count > 0) {
               rootTarget = gameMatch.cardByUid[rootEffect.targetUids[0]];
        }
        return conditions == null || conditions.All(condition => condition.Verify(gameMatch, controllingPlayer, rootTarget));
    }

    public void Resolve(GameMatch gameMatch, Player effectOwner, int? rootSubjectUid = null) {
        if (!ConditionsAreMet(gameMatch, effectOwner)) return;
        if (rootSubjectUid != null) subjectUid = (int)rootSubjectUid;
        Player sourcePlayer = isOpponent ? gameMatch.GetOpponent(effectOwner) : effectOwner;
        // set player affected based on "player" string for some cards (e.g. Earthquake Golem)
        Player affectedPlayer;
        switch (player) {
            case "opponent":
                affectedPlayer = gameMatch.GetOpponent(effectOwner);
                break;
            default:
                affectedPlayer = sourcePlayer;
                break;
        }
        // set amounts based on game state at the time of resolve
        if (amountBasedOn != null) {
            amount = gameMatch.GetAmountBasedOn(amountBasedOn, other, sourcePlayer, rootEffect, cardType, restrictions, sourceCard);
        }
        if (attackBasedOn != null) {
            attack = gameMatch.GetAmountBasedOn(attackBasedOn, other, sourcePlayer, rootEffect, cardType, restrictions, sourceCard);
        }
        if (defenseBasedOn != null) {
            defense = gameMatch.GetAmountBasedOn(defenseBasedOn, other, sourcePlayer, rootEffect, cardType, restrictions, sourceCard);
        }

        if (amountModifier != null) {
            if (modifierConditions == null || modifierConditions.All(condition => condition.Verify(gameMatch, affectedPlayer))) {
                Debug.Assert(amount != null, "there is no amount to modify");
                switch (amountModifier) {
                    case "/2down":
                        amount = (int)Math.Floor((int)amount / 2f);
                        break;
                    case "+1":
                        amount++;
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
                } else if (self) {
                    gameMatch.Reveal(affectedPlayer, gameMatch.cardByUid[(int)subjectUid!]);
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
                        gameMatch.DealDamage(uid, (int)amount);
                        affectedUids.Add(uid);
                    }
                } else {
                    if (cardType is CardType.Summon) {
                        foreach(Card c in gameMatch.GetQualifiedCards(gameMatch.GetAllSummonsInPlay(), eQualifier)) {
                            gameMatch.DealDamage(c.uid, (int)amount);
                            affectedUids.Add(c.uid);
                        }
                    }
                }
                break;
            case EffectType.Destroy:
                affectedUids = new List<int>();
                if (targetUids.Count > 0) {
                    foreach (int uid in targetUids) {
                        gameMatch.Destroy(gameMatch.cardByUid[uid]);
                        affectedUids.Add(uid);
                    }
                } else {
                    // TODO implement destroy all (e.g. Wrath) and card types
                }
                break;
            case EffectType.GrantPassive: 
                Debug.Assert(passives != null, "there are no passive for this GrantPassive effect"); 
                affectedUids = new List<int>();
                if (targetUids.Count > 0) {
                    foreach (int uid in targetUids) {
                        GrantPassive(gameMatch.cardByUid[uid]);
                        affectedUids.Add(uid);
                    } 
                } else {
                   foreach (Card c in gameMatch.GetQualifiedCards(gameMatch.GetAllSummonsInPlay(), eQualifier)) {
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
                if (self) {
                    Debug.Assert(sourceCard != null, "there's no sourceCard for this CastCard Effect");
                    gameMatch.AttemptToCast(affectedPlayer, sourceCard, CastingStage.Initial, false);
                }
                break;
            case EffectType.EventTriggers: 
                Debug.Assert(sourceCard != null, "there is no sourceCard for this EventTriggers Effect");
                foreach (TriggeredEffect tEffect in triggeredEffects) {
                    affectedPlayer.eventTriggers.Add(tEffect); 
                }
                break;
            case EffectType.Sacrifice:
                Debug.Assert(sourceCard != null, "there is no sourceCard for this Sacrifice Effect"); 
                if (self) {
                    gameMatch.Destroy(sourceCard);
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
        foreach (PassiveEffect pEffect in passives) {
            if (pEffect.statModifiers != null) {
                foreach (StatModifier statMod in pEffect.statModifiers) {
                    if (statMod.xAmount) statMod.amount = sourceCard!.x!.Value; 
                }
            }
            c.grantedPassives.Add(pEffect);
        }
    }

    private List<int> GrantKeyword(GameMatch gameMatch, Player affectedPlayer) {
        Debug.Assert(keyword != null, "There is no keyword associated with this GrantKeyword Effect");
        Debug.Assert(subjectUid != null, "There is no target for this GrantKeyword Effect");
        List<int> tempAffectedUids = new List<int>();
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
        Debug.Assert(deckDestinations != null,
            "there are no deck destinations for this look at deck effect (there should be at least one)");
        List<Card> cardsToLookAt = GameMatch.GetTopCards(affectedPlayer, (int)amount);
        List<CardDisplayData> cddsToLookAt = cardsToLookAt.Select(card => new CardDisplayData(card)).ToList();
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
        Qualifier eQualifier = new Qualifier(this, sourcePlayer);
        foreach (DeckDestination dd in dDestinations) {
            List<int> qualifiedUids = ToUids(gameMatch.GetQualifiedCards(cardsToLookAt, eQualifier));
            // TODO: deal with max selection amount (upTo property)
            int selectionMin = dd.amount ?? 0;
            bool isSelectOrder = dd.ordering == Ordering.Any;
            csDatas.Add(new CardSelectionData(qualifiedUids, GetDDMessage(dd), selectionMin, selectionMin, isSelectOrder));
        }
        return csDatas;
    }

    private List<int> ToUids(List<Card> cards) {
        return cards.Select(c => c.uid).ToList();
    }

private string GetDDMessage(DeckDestination dd) {
        string message = "";
        switch (dd.amount) {
            case 1:
                message = "Pick a card to ";
                break;
            case > 1:
                message = ("Pick " + dd.amount + " cards to ");
                break;
        }
        string destinationMessage = dd.deckDestination switch {
            DeckDestinationType.Bottom => "put at the bottom of your deck",
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
    /// Handles CreateToken Effect logic using the Effect's properties
    /// </summary>
    /// <param name="gameMatch"></param>
    /// <param name="affectedPlayer"></param>
    private void CreateToken(GameMatch gameMatch, Player affectedPlayer) {
        for (int i = 0; i < amount; i++) {
            Token newToken = new Token(tokenType, gameMatch);
            if (tokenPassive != null) {
                // this adds any passives to the token (because tokens don't be default have any extra fields)
                newToken.passiveEffects = new List<PassiveEffect>();
                switch (tokenPassive.passive) {
                    case Passive.TributeRestriction:
                        newToken.passiveEffects.Add(new PassiveEffect(Passive.TributeRestriction, tokenPassive.tribe));
                        newToken.description += "Can only be tributed to " + tokenPassive.tribe + 
                                                GetPluralityBasedOnWord(tokenPassive.tribe.ToString()) + ".";
                        break;
                    default:
                        newToken.passiveEffects.Add(new PassiveEffect(tokenPassive.passive, tokenPassive.tribe, tokenPassive.self));
                        break;
                }
            }
            if (attack != null) {
                newToken.type = CardType.Summon;
                newToken.attack = attack;
                newToken.defense = defense;
            }
            if (keyword != null) {
                newToken.keywords = new List<Keyword> { (Keyword)keyword };
            }
            Console.WriteLine("created token from effect: " + effect);
            gameMatch.CreateTokenForPlayer(affectedPlayer, newToken, attacking);
        }
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
        if (zone != null) {
            switch (zone) {
                // TODO TargetTypes
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

    public string EffectToString(GameMatch gameMatch) {
        if (description != null) {
            return description;
        }
        // set defaults for sentence structure
        string plurality;
        string verbAgreement = isOpponent ? "s " : " ";
        string pronoun = isOpponent ? "opponent " : "";
        string tempString = "error: no effect string";
        string target = "card";
        // set default target for effects with targetType or (bool)self
        if (cardType != null) target = cardType.ToString()!;
        if (tribe != null) target = tribe.ToString()!;
        Debug.Assert(sourceCard != null, "no sourceCard for this effect");
        if (self) target = sourceCard.name;
        // set final string based on EffectType
        switch (effect) { 
            case EffectType.CreateToken:
                plurality = amount == 1 ? "" : "s";
                // this looks ugly, but it does what it needs to: creates the string for the effect 
                // depending on what the effect is.
                string attackingString = " ";
                string thisTurnString = "";
                if (attacking) attackingString = " attacking ";
                if (thisTurn) thisTurnString = " this turn";
                if (tokenType != TokenType.Herb && tokenType != TokenType.Stone) {
                    tempString = "create " + amount + attackingString + attack + "/" + defense + " " + tokenType + " token" + plurality + thisTurnString + ".";
                } else {
                    tempString = "create " + amount + " " + tokenType + " token" + plurality + ".";
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
                tempString = "send to " + destination; 
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
            case EffectType.LoseLife:
                if (isOpponent) {
                    plurality = "s";
                    pronoun = "opponent";
                } else {
                    plurality = "";
                }
                tempString = pronoun + " lose" + plurality + " " + amount + " life.";
                break;
            case EffectType.SetLifeTotal:
                pronoun = isOpponent ? "opponent's" : "your";
                tempString = "set " + pronoun + " lifetotal to " + amount + ".";
                break;
            case EffectType.Reveal:
                // TODO might need to change this to handle multiple types of reveal effects (self, target cards, etc.)
                if (all) {
                    tempString = "reveal your hand";
                } else if(subjectUid != null) {
                    tempString = "reveal " + gameMatch.cardByUid[(int)subjectUid].name;
                } else {
                    tempString = "Not implemented (Reveal Effect)";
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
                tempString = "cast " + target;
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