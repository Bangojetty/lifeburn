using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Server.CardProperties;

public class AdditionalCost {
    [JsonConverter(typeof(StringEnumConverter))]
    public CostType costType;
    [JsonConverter(typeof(StringEnumConverter))]
    public TokenType? tokenType;
    public int amount;
    public bool playerChosenAmount;
    [JsonConverter(typeof(StringEnumConverter))]
    public AmountBasedOn? amountBasedOn;
    [JsonConverter(typeof(StringEnumConverter))]
    public Scope scope = Scope.All;  // If selfOnly, sacrifice/discard the source card itself

    // non-json
    public bool isPaid;

    /// <summary>
    /// Gets the resolved amount for this cost, using amountBasedOn if set.
    /// </summary>
    public int GetAmount(Card? sourceCard) {
        if (amountBasedOn == AmountBasedOn.X && sourceCard?.x != null) {
            return sourceCard.x.Value;
        }
        return amount;
    }

    public bool CostIsAvailable(GameMatch gameMatch, Player player, Card? sourceCard = null) {
        // X-based costs are always available (X can be 0)
        if (amountBasedOn == AmountBasedOn.X) return true;

        int playerAmount = 0;
        switch (costType) {
            case CostType.Sacrifice:
                if (tokenType != null) {
                    foreach (Token t in player.tokens) {
                        if (t.tokenType == tokenType) playerAmount++;
                    }
                }
                break;
            case CostType.Life:
                return player.lifeTotal > amount;
        }
        if (playerChosenAmount && playerAmount > 0) return true;
        return playerAmount >= amount;
    }
}