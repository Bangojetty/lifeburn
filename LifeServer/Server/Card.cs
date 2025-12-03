using System.Collections;
using System.Diagnostics;
using System.Dynamic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Server.CardProperties;
namespace Server;

public class Card {
    public int id;
    public string name;
    public int cost;
    public CardType type;
    public int? attack;
    public int? defense;
    public List<Keyword>? keywords;
    public Tribe tribe;
    public Rarity rarity;
    public string? description;
    public List<Effect>? stackEffects;
    public List<TriggeredEffect>? triggeredEffects;
    public List<PassiveEffect>? passiveEffects;
    public List<ActivatedEffect>? activatedEffects;
    public List<CostModifier>? costModifiers;
    public List<AdditionalCost>? additionalCosts;
    public List<CastRestriction>? castRestrictions;

    // non-Json
    public int uid;
    public string displayDescription;
    public GameMatch? currentGameMatch;
    public Zone currentZone;
    public bool isRevealed;
    public bool hasSummoningSickness = true;
    public Player? lastControllingPlayer;
    public Player? playerHandOf;
    public int damageTaken;
    public int attackTargetUid;
    public int? x = null;
    

    public Dictionary<int, int> chosenIndices = new();

    // passives
    public List<PassiveEffect> grantedPassives = new();


    public static Card GetCard(int uid, int cardId, GameMatch? match = null) {
        string cardJson = Utils.GetCardJson(cardId);
        // set up the converter for effect subclasses
        JsonSerializerSettings settings = new();
        settings.Converters.Add(new EffectTypeConverter());
        CardDto newCardDto = JsonConvert.DeserializeObject<CardDto>(cardJson, settings);
        Card newCard = new Card(uid, newCardDto);
        if (match != null) newCard.currentGameMatch = match;
        return newCard;
    }

    public static Card GetCardSimple(int cardId) {
        Card? newCard = JsonConvert.DeserializeObject<Card>(Utils.GetCardJson(cardId));
        Debug.Assert(newCard != null, $"Card with id: {cardId} doesn't exist.");
        newCard.displayDescription = newCard.ProcessDisplayDescription();
        return newCard;
    }



    private Card(int uid, CardDto cardDto) {
        this.uid = uid;
        id = cardDto.id;
        name = cardDto.name;
        cost = cardDto.cost;
        type = cardDto.type;
        attack = cardDto.attack;
        defense = cardDto.defense;
        keywords = cardDto.keywords;
        tribe = cardDto.tribe;
        rarity = cardDto.rarity;
        description = cardDto.description;
        displayDescription = ProcessDisplayDescription();
        stackEffects = cardDto.stackEffects;
        triggeredEffects = cardDto.triggeredEffects;
        passiveEffects = cardDto.passiveEffects;
        activatedEffects = cardDto.activatedEffects;
        costModifiers = cardDto.costModifiers;
        additionalCosts = cardDto.additionalCosts;
        castRestrictions = cardDto.castRestrictions;
        AssignSourceToEffects();
    }

    private void AssignSourceToEffects() {
        HashSet<object> visited = new();
        if (stackEffects != null) {
            foreach (var effect in stackEffects)
                AssignSourceRecursive(effect, visited, this);
        }

        if (triggeredEffects != null) {
            foreach (var effect in triggeredEffects)
                AssignSourceRecursive(effect, visited, this);
        }

        if (activatedEffects != null) {
            foreach (var effect in activatedEffects)
                AssignSourceRecursive(effect, visited, this);
        }
    }

    /// <summary>
    /// Within this object, recursively look for fields named "sourceCard" and assign "this" to that field.
    /// </summary>
    /// <param name="obj">The object to examine for fields named "sourceCard".</param>
    /// <param name="visited">A hashset of objects that have been visited already (to avoid recursive loops).</param>
    /// <param name="source">The card whose reference should be set on the 'sourceCard' field, if present.</param>
    private void AssignSourceRecursive(object obj, HashSet<object> visited, Card source) {
        if (obj == null || obj is string || visited.Contains(obj)) return;
        visited.Add(obj);

        var objectType = obj.GetType();

        // Assign to any public field named "sourceCard"
        var field = objectType.GetField("sourceCard", BindingFlags.Public | BindingFlags.Instance);
        if (field != null && field.FieldType == typeof(Card))
            field.SetValue(obj, source);

        foreach (var f in objectType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
            var value = f.GetValue(obj);
            switch (value) {
                case null:
                    continue;
                case IEnumerable enumerable: {
                    foreach (var item in enumerable)
                        AssignSourceRecursive(item, visited, source);
                    break;
                }
                default: {
                    if (!(value is ValueType))
                        AssignSourceRecursive(value, visited, source);
                    break;
                }
            }
        }
    }

    private string ProcessDisplayDescription() {
        if (description == null) return "";
        return Regex.Replace(description, @"\{c\d+\}", "");
    }

public void Reveal() {
        isRevealed = true;
    }

    public bool HasXCost() {
        return costModifiers != null && costModifiers.Any(costMod => costMod.modifier == ModifierType.X);
    }

    public Card(int uid) {
        this.uid = uid;
    }

    
    public override string ToString() {
        string printString = $"ID: {id}, Name: {name}, Cost: {cost}, Type: {type}, ";
        if (attack != null) {
            printString += "attack: " + attack + ", ";
        }
        if(defense != null) {
            printString += "defense: " + defense + ", ";
        }

        if (keywords != null) {
            printString += "keywords: ";
            foreach(Keyword k in keywords) {
                printString += k + ", ";
            }
        }

        printString += $"Tribe: {tribe}, Rarity: {rarity}, Description: {description}, ";

        if (stackEffects != null) {
            printString += "Stack Effects: " + stackEffects.Count;
        }
        if (triggeredEffects != null) {
            printString += "Triggered Effects: " + triggeredEffects.Count;
            // foreach (TriggeredEffect t in triggeredEffects) {
            //     t.Trigger.ToString();
            // }
        }
        if (passiveEffects != null) {
            printString += "Passives Effects: " + passiveEffects.Count;
        }
        if (activatedEffects != null) {
            printString += "Activated Effects: " + activatedEffects.Count;
        }
        if (costModifiers != null) {
            printString += "Cost Modifiers: " + costModifiers.Count;
        }
        if (additionalCosts != null) {
            printString += "Additional Costs: " + additionalCosts.Count;
        }
        if (castRestrictions != null) {
            printString += "Cast Restrictions: " + castRestrictions.Count;
        }
        return printString;
        //    $"ID: {id}, Name: {name}, Cost: {cost}, Type: {type}, Attack: {attack}, Defense: {defense}, " +
        //    $"Keywords: {keywords.Count}, Tribe: {tribe}, Rarity: {rarity}, " +
        //    $"Description: {description}, " + // Full description included
        //    $"Stack Effects: {stackEffects.Count}, Triggered Effects: {triggeredEffects.Count}, " +
        //    $"Passive Effects: {passiveEffects.Count}, Activated Effects: {activatedEffects.Count}, " +
        //    $"Cost Modifiers: {costModifiers.Count}, Additional Costs: {additionalCosts.Count}, " +
        //    $"Cast Restrictions: {castRestrictions.Count}";
    }

    protected bool Equals(Card other) {
        return uid == other.uid;
    }

    public override bool Equals(object? obj) {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((Card)obj);
    }

    public override int GetHashCode() {
        return uid;
    }

    public List<PassiveEffect> GetPassives() {
        List<PassiveEffect> tempPassives = new();
        if (passiveEffects != null) tempPassives.AddRange(passiveEffects);
        tempPassives.AddRange(grantedPassives);
        return tempPassives;
    }

    /// <summary>
    /// returns all passives on the card that either don't have conditions or all conditions have been verified
    /// </summary>
    /// <returns></returns>
    private List<PassiveEffect> GetVerifiedPassives() {
        Debug.Assert(currentGameMatch != null, "no current match for checking passives -> " +
                                               "you shouldn't be checking passives if you're not in a match");
        return GetPassives().Where(p => p.conditions == null || p.conditions
                .All(c => c.Verify(currentGameMatch, currentGameMatch.GetOwnerOf(this))))
            .ToList();
    }

    public int GetAttack() {
        Debug.Assert(attack != null, "card does not have attack"); 
        if (currentGameMatch == null) return attack.Value;
        int tempAttack = (int)attack;
        foreach (PassiveEffect pEffect in GetVerifiedPassives()) {
            // this prevents checking for passives if the card is not in play (combat stats can't be altered outside of play)
            if (currentZone != Zone.Play) continue;
            if(pEffect.statModifiers == null) continue;
            foreach (StatModifier statMod in pEffect.statModifiers) {
                if (statMod.statType != StatType.Attack) continue;
                if (statMod.amountBasedOn != null) {
                    // you must set the stat modifiers amount before applying it if it has an amountBasedOn
                    statMod.amount = ResolveAmount(statMod.amountBasedOn.Value, statMod.other) * (statMod.amountMulitplier ?? 1);
                }

                tempAttack = statMod.Apply(tempAttack);
            }
        }
        return tempAttack;
    }

    public int GetDefense() {
        Debug.Assert(defense != null, "card does not have defense"); 
        if (currentGameMatch == null) return defense.Value;
        int tempDefense = (int)defense;
        foreach (PassiveEffect pEffect in GetVerifiedPassives()) {
            // this prevents checking for passives if the card is not in play (combat stats can't be altered outside of play)
            if (currentZone != Zone.Play) continue;
            if(pEffect.statModifiers == null) continue;
            foreach (StatModifier statMod in pEffect.statModifiers) {
                if (statMod.statType != StatType.Defense) continue;
                if (statMod.amountBasedOn != null) {
                    // you must set the stat modifiers amount before applying it if it has an amountBasedOn
                    statMod.amount = ResolveAmount(statMod.amountBasedOn.Value, statMod.other) * (statMod.amountMulitplier ?? 1);
                }

                tempDefense = statMod.Apply(tempDefense);
            }
        }
        tempDefense -= damageTaken;
        return tempDefense;
    }

    public int GetCost() {
        int finalCost = x ?? cost;
        if (currentGameMatch == null) return finalCost;
        foreach (PassiveEffect pEffect in GetVerifiedPassives()) {
            if(pEffect.passive != Passive.ModifyCost) continue;
            Debug.Assert(pEffect.cost != null, "Passive Effect has no cost value to modify to");
            finalCost = pEffect.cost.Value;
        }
        if (type == CardType.Spell && playerHandOf != null && playerHandOf.spellBurnt) finalCost += cost;
        finalCost += grantedPassives.Sum(pEffect => pEffect.costModifier);
        return finalCost;
    }
    
    public int ResolveAmount(AmountBasedOn basedOn, bool other = false, string? amountModifier = null, CardType? cardType = null, List<Restriction>? restrictions = null) {
        return currentGameMatch.GetAmountBasedOn(
            basedOn,
            other,
            currentGameMatch.GetControllerOf(this),
            null,
            cardType,
            restrictions,
            this
        );
    }

    public List<Keyword>? GetKeywords() {
        List<Keyword> tempKeywords = new();
        if (keywords != null) {
            tempKeywords.AddRange(keywords);
        } 
        foreach (PassiveEffect pEffect in GetPassives()) {
            if (pEffect.passive != Passive.GrantKeyword) continue;
            Debug.Assert(pEffect.keyword != null, "Passive Effect has no keywords to grant");
            if (!tempKeywords.Contains((Keyword)pEffect.keyword)) tempKeywords.Add((Keyword)pEffect.keyword);
        }
        return tempKeywords;
    }

    public string GetAdditionalDescription() {
        string addDesc = "";
        foreach (PassiveEffect pEffect in grantedPassives) {
            if (pEffect.statModifiers != null) continue;
            if (pEffect.passive == Passive.GrantKeyword) continue;
            if(addDesc.Length > 0) addDesc += "\n";
            addDesc += Utils.CapitalizeFirstLetter(pEffect.GetDescription());
        }
        return addDesc;
    }

    public bool HasSummoningSickness() {
        return hasSummoningSickness && !HasKeyword(Keyword.Blitz);
    }

    private bool HasKeyword(Keyword keyword) {
        return GetKeywords() != null &&  GetKeywords()!.Contains(keyword);
    }

    public bool HasEffect(EffectType effectType) {
        if (stackEffects != null) {
            foreach (Effect stackEffect in stackEffects) {
                if (stackEffect.effect == effectType) return true;
            }
        }
        // TODO add all possible EffectType locations (triggers, additional effects, etc.)
        return false;
    }

    public bool HasTokenType(TokenType tokenType) {
        if (stackEffects != null) {
            foreach (Effect stackEffect in stackEffects) {
                if (stackEffect.tokenType == tokenType) return true;
            }
        }
        // TODO add all possible TokenType locations (triggers, additional effect, conditions, etc)
        return false;
    }
}

