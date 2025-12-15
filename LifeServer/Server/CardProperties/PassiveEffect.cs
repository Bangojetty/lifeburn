using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Server.CardProperties;

public class PassiveEffect {
    public Passive passive;
    public List<Condition>? conditions;
    public int? cost;
    public int? amount;
    public bool all;
    public string? description;
    public Keyword? keyword;
    public TargetBasedOn? targetBasedOn;
    public Zone? zone;
    public List<StatModifier>? statModifiers;
    public TokenType? tokenType;
    public Tribe? tribe;
    public List<Restriction>? restrictions;
    [Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
    public Scope scope = Scope.All;  // Default: passive affects all qualifying cards (including self)
    public bool thisTurn;
    public List<ActivatedEffect>? actives;  // For GrantActive passive


    // non-json
    public Card grantedBy;   // The card that originally granted this passive (for UI tracking)
    public Card owner;       // The card this passive is currently on (for logic)
    public int tempAttackMod;
    public int tempDefenseMod;
    public int costModifier;
    public int x;

    [Newtonsoft.Json.JsonConstructor]
    public PassiveEffect() {}

    // Generic
    public PassiveEffect(Passive passive, Tribe? tribe = null, Scope scope = Scope.All) {
        this.passive = passive;
        this.tribe = tribe;
        this.scope = scope;
    }

    // GrantKeyword
    public PassiveEffect(Passive passive, Keyword keyword, Scope scope = Scope.All) {
        this.passive = passive;
        this.keyword = keyword;
        this.scope = scope;
    }
    

    public string GetDescription() {
        if(description != null) return description;
        string tempDesc = "";
        switch (passive) {
            case Passive.CantTakeDamage:
                tempDesc += "can't take damage";
                break;
            case Passive.ChangeStats:
                if (statModifiers != null) {
                    int attackMod = 0;
                    int defenseMod = 0;
                    OperatorType? opType = null;
                    foreach (StatModifier statMod in statModifiers) {
                        int value = statMod.xAmount && grantedBy?.x != null ? grantedBy.x.Value : statMod.amount;
                        if (statMod.statType == StatType.Attack) attackMod = value;
                        if (statMod.statType == StatType.Defense) defenseMod = value;
                        opType ??= statMod.operatorType;
                    }
                    if (opType == OperatorType.Multiply) {
                        // For multiply, show "x2/x2" format or "doubles" for x2
                        if (attackMod == 2 && defenseMod == 2) {
                            tempDesc += "doubles its attack and defense";
                        } else {
                            tempDesc += $"gets x{attackMod}/x{defenseMod}";
                        }
                    } else {
                        // For add (default), show "+X/+Y" format
                        string sign = attackMod >= 0 ? "+" : "";
                        string defSign = defenseMod >= 0 ? "+" : "";
                        tempDesc += $"gets {sign}{attackMod}/{defSign}{defenseMod}";
                    }
                }
                break;
            case Passive.GrantKeyword:
                if (keyword != null) {
                    tempDesc += $"gains {keyword}";
                }
                break;
            case Passive.CantBeTargeted:
                tempDesc += "can't be targeted by spells or abilities";
                break;
        }
        if (thisTurn) tempDesc += " this turn";
        return tempDesc;
    }

    /// <summary>
    /// Creates a deep clone of this PassiveEffect for granting to a target card.
    /// </summary>
    public PassiveEffect Clone() {
        PassiveEffect clone = new PassiveEffect {
            passive = passive,
            conditions = conditions?.ToList(),
            cost = cost,
            amount = amount,
            all = all,
            description = description,
            keyword = keyword,
            targetBasedOn = targetBasedOn,
            zone = zone,
            statModifiers = statModifiers?.Select(sm => sm.Clone()).ToList(),
            tokenType = tokenType,
            tribe = tribe,
            restrictions = restrictions?.ToList(),
            scope = scope,
            thisTurn = thisTurn,
            actives = actives?.ToList(),  // Shallow copy - ActivatedEffects are reused
            // non-json fields set by caller
            grantedBy = grantedBy,
            owner = null,
            tempAttackMod = tempAttackMod,
            tempDefenseMod = tempDefenseMod,
            costModifier = costModifier,
            x = x
        };
        return clone;
    }
}