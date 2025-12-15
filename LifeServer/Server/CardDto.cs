using Server.CardProperties;

namespace Server;

public class CardDto {
    public int id;
    public string name;
    public int cost;
    public CardType type;
    public int? attack;
    public int? defense;
    public List<Keyword>? keywords;
    public Tribe tribe;
    public Rarity rarity;
    public string description;
    public List<Effect>? stackEffects;
    public List<TriggeredEffect>? triggeredEffects;
    public List<PassiveEffect>? passiveEffects;
    public List<ActivatedEffect>? activatedEffects;
    public List<CostModifier>? costModifiers;
    public List<AdditionalCost>? additionalCosts;
    public List<CastRestriction>? castRestrictions;
    public List<AlternateCost>? alternateCosts;
}