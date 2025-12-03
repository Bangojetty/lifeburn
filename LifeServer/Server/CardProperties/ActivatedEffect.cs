namespace Server.CardProperties;

public class ActivatedEffect {
    public List<Condition>? conditions;
    public CostType costType;
    public CardType? cardType;
    public TokenType? tokenType;
    public Tribe? tribe;
    public string? description;
    public bool self;
    public bool oncePerTurn;
    public int amount;
    public bool playerChosenAmount;
    public List<Restriction>? restrictions;
    public List<Effect> effects;
    
    // non-json
    public Card sourceCard;



    public bool CostIsAvailable(GameMatch gameMatch, Player player) {
        int playerAmount = 0;
        Qualifier costQualifier = new Qualifier(this, player);
        switch (costType) {
            case CostType.Sacrifice:
                playerAmount += gameMatch.GetAllCardsControlled(player).Count(c => gameMatch.QualifyCard(c, costQualifier)); 
                break;
            case CostType.Discard:
                playerAmount += player.allCardsPlayer.Count(c => gameMatch.QualifyCard(c, costQualifier));
                break;
            default:
                Console.WriteLine("Unknown CostType (CostIsAvailable)");
                break;
        }
        if (playerChosenAmount && playerAmount > 0) return true;
        return playerAmount >= amount;
    }


    public void SetAmount(int newAmount) {
        amount = newAmount;
        foreach (Effect e in effects) {
            if (e.amountBasedOn is not AmountBasedOn.ActivatedCost) continue;
            e.amount = amount;
            e.amountBasedOn = null;
        }
    }
}