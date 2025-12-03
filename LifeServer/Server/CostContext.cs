using Server.CardProperties;

namespace Server;

public class CostContext {
    public CostType costType;
    public CardType? cardType;
    public TokenType? tokenType;
    public Tribe? tribe;
    public string? description;
    public int amount;
    public bool playerChosenAmount;

    public CostContext(ActivatedEffect aEffect) {
        costType = aEffect.costType;
        cardType = aEffect.cardType;
        tokenType = aEffect.tokenType;
        tribe = aEffect.tribe;
        description = aEffect.description;
        amount = aEffect.amount;
        playerChosenAmount = aEffect.playerChosenAmount;
    }

    public CostContext(AdditionalCost aCost) {
        costType = aCost.costType;
        tokenType = aCost.tokenType;
        amount = aCost.amount;
    } 
}