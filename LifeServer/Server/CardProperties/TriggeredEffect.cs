namespace Server.CardProperties;

public class TriggeredEffect {
    public List<Condition>? conditions;
    public Trigger trigger;
    public Zone? triggerZone = Zone.Play;
    public bool self = true;
    public bool optional = false;
    public bool handTrigger = false;
    public string? optionMessage;
    public string? description;
    public Phase? phase;
    public bool? isPlayerTurn;
    public Zone? zone;
    public CardType? cardType;
    public TokenType? tokenType;
    public Tribe? tribe;
    public List<AdditionalCost>? additionalCosts;
    public List<Effect> effects;
    public List<Restriction>? restrictions;

    public Card sourceCard;


    public bool CostsArePayable(GameMatch gameMatch, Player player) {
        return additionalCosts == null || additionalCosts.All(aCost => aCost.CostIsAvailable(gameMatch, player));
    }
    
}