namespace Server.CardProperties;

public class AdditionalCost {
    public CostType costType;
    public TokenType? tokenType;
    public int amount;
    public bool playerChosenAmount;
    
    // non-json
    public bool isPaid;
    
    
    public bool CostIsAvailable(GameMatch gameMatch, Player player) {
        int playerAmount = 0;
        switch (costType) {
            case CostType.Sacrifice:
                if (tokenType != null) {
                    foreach (Token t in player.tokens) {
                        if (t.tokenType == tokenType) playerAmount++;
                    }
                }
                break;
        }
        if (playerChosenAmount && playerAmount > 0) return true;
        return playerAmount >= amount;
    }
}