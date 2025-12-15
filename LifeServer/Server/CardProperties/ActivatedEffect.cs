using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Server.CardProperties;

public class ActivatedEffect {
    public List<Condition>? conditions;
    public CostType costType;
    public CardType? cardType;
    public TokenType? tokenType;
    public Tribe? tribe;
    public string? description;
    [JsonConverter(typeof(StringEnumConverter))]
    public Scope scope = Scope.All;
    public bool oncePerTurn;
    public int amount;
    public bool playerChosenAmount;
    public List<Restriction>? restrictions;
    public List<Effect> effects;
    
    // non-json
    public Card sourceCard;
    public Card grantedBy;  // The card that granted this activated effect (for cleanup)



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
            case CostType.DiscardOrSacrificeMerfolk:
                // Check if player has merfolk in hand (for discard) OR in play (for sacrifice)
                // Note: Can sacrifice the source card itself (e.g., Eadro can sacrifice itself)
                int merfolkInHand = player.hand.Count(c => c.tribe == Tribe.Merfolk);
                int merfolkInPlay = gameMatch.GetAllCardsControlled(player).Count(c => c.tribe == Tribe.Merfolk);
                playerAmount = merfolkInHand + merfolkInPlay;
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

    public ActivatedEffect Clone() {
        return new ActivatedEffect {
            conditions = conditions?.ToList(),
            costType = costType,
            cardType = cardType,
            tokenType = tokenType,
            tribe = tribe,
            description = description,
            scope = scope,
            oncePerTurn = oncePerTurn,
            amount = amount,
            playerChosenAmount = playerChosenAmount,
            restrictions = restrictions?.ToList(),
            effects = effects.Select(e => e.Clone()).ToList(),  // Deep copy effects
            sourceCard = sourceCard,
            grantedBy = grantedBy
        };
    }
}