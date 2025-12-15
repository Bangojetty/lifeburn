using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Server.CardProperties;

public class TriggeredEffect {
    public List<Condition>? conditions;
    public Trigger trigger;
    [JsonConverter(typeof(StringEnumConverter))]
    public Scope scope = Scope.SelfOnly;  // Default: trigger only fires for events involving this card
    public bool optional = false;
    public bool handTrigger = false;
    public string? optionMessage;
    public string? description;
    public Phase? phase;
    public string? phaseOfPlayer;  // "player" or "opponent" - whose turn the phase trigger should fire on
    public bool? isPlayerTurn;
    public Zone? zone;
    public CardType? cardType;
    public TokenType? tokenType;
    public Tribe? tribe;
    public string? player;  // "player" or "opponent" - who the trigger responds to
    public List<AdditionalCost>? additionalCosts;
    public List<Effect> effects;
    public List<Restriction>? restrictions;

    public Card sourceCard;


    public bool CostsArePayable(GameMatch gameMatch, Player player) {
        // Check old-style additionalCosts (for backwards compatibility)
        if (additionalCosts != null && !additionalCosts.All(aCost => aCost.CostIsAvailable(gameMatch, player))) {
            return false;
        }

        // Check new-style isCost effects in the effects list
        if (effects != null) {
            foreach (Effect effect in effects) {
                if (effect.isCost && !effect.CanPayCost(gameMatch, player)) {
                    return false;
                }
            }
        }

        return true;
    }
    
}