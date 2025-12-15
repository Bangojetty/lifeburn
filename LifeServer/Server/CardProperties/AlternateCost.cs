using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Server.CardProperties;

public class AlternateCost {
    [JsonConverter(typeof(StringEnumConverter))]
    public AltCostType altCostType;
    public TokenType? tokenType;
    public Tribe? tribe;
    public CardType? cardType;
    public int amount;
}

public enum AltCostType {
    TributeMultiplier,
    Sacrifice,
    ExileFromHand,
    Tribute,
    TribueMultiplier  // Typo in JSON, keep for compatibility
}
