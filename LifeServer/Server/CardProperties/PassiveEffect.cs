using System.Text.Json.Serialization;

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
    public bool other;
    public bool self = true;
    public bool thisTurn;


    // non-json
    public Card sourceCard;
    public int tempAttackMod;
    public int tempDefenseMod;
    public int costModifier;
    public int x;

    [JsonConstructor]
    public PassiveEffect() {}

    // Generic 
    public PassiveEffect(Passive passive, Tribe? tribe = null, bool self = true) {
        this.passive = passive;
        this.tribe = tribe;
        this.self = self;
    }
    
    // GrantKeyword
    public PassiveEffect(Passive passive, Keyword keyword, bool self = true) {
        this.passive = passive;
        this.keyword = keyword;
        this.self = self;
    }
    

    public string GetDescription() {
        if(description != null) return description;
        string tempDesc = "";
        switch (passive) {
            case Passive.CantTakeDamage:
                tempDesc += "can't take damage";
                break;
        }
        if (thisTurn) tempDesc += " this turn";
        return tempDesc;
    }
}