using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Server.CardProperties;

public class TriggeredEffect {
    public List<Condition>? conditions;
    [JsonConverter(typeof(StringEnumConverter))]
    public Trigger trigger;
    [JsonConverter(typeof(StringEnumConverter))]
    public Scope scope = Scope.SelfOnly;  // Default: trigger only fires for events involving this card
    public bool optional = false;
    public bool opponentsChoice = false;  // If true, opponent decides whether optional trigger fires
    public bool handTrigger = false;
    public bool immediate = false;  // If true, trigger resolves immediately without going on the stack
    public string? optionMessage;
    public string? description;
    [JsonConverter(typeof(StringEnumConverter))]
    public Phase? phase;
    public string? phaseOfPlayer;  // "player" or "opponent" - whose turn the phase trigger should fire on
    public bool? isPlayerTurn;
    [JsonConverter(typeof(StringEnumConverter))]
    public Zone? zone;
    [JsonConverter(typeof(StringEnumConverter))]
    public CardType? cardType;
    [JsonConverter(typeof(StringEnumConverter))]
    public TokenType? tokenType;
    [JsonConverter(typeof(StringEnumConverter))]
    public Tribe? tribe;
    [JsonConverter(typeof(StringEnumConverter))]
    public Keyword? keyword;  // For tribute triggers - the keyword the tributed card must have
    public string? player;  // "player" or "opponent" - who the trigger responds to
    public List<AdditionalCost>? additionalCosts;
    public List<Effect> effects;
    [JsonProperty(ItemConverterType = typeof(StringEnumConverter))]
    public List<Restriction>? restrictions;
    public int? restrictionMax;
    public int? restrictionMin;
    public int? amount;  // For mill triggers - minimum cards milled to trigger (e.g., "when you mill 5 or more")

    // non-json
    public Card sourceCard;
    public Card? triggerCard;  // The card that caused this trigger to fire (e.g., the dying card for death triggers)
    public Player? triggerController;  // The player who caused the trigger to fire (e.g., the player who cast the spell)

    public TriggeredEffect CloneWithTriggerCard(Card source, Card? triggerCardToSet, Player? triggerControllerToSet = null) {
        return new TriggeredEffect {
            conditions = conditions,
            trigger = this.trigger,
            scope = scope,
            optional = optional,
            opponentsChoice = opponentsChoice,
            handTrigger = handTrigger,
            immediate = immediate,
            optionMessage = optionMessage,
            description = description,
            phase = phase,
            phaseOfPlayer = phaseOfPlayer,
            isPlayerTurn = isPlayerTurn,
            zone = zone,
            cardType = cardType,
            tokenType = tokenType,
            tribe = tribe,
            keyword = keyword,
            player = player,
            additionalCosts = additionalCosts,
            effects = effects?.Select(e => e.Clone()).ToList(),  // Deep clone effects list
            restrictions = restrictions,
            restrictionMax = restrictionMax,
            restrictionMin = restrictionMin,
            amount = amount,
            sourceCard = source,
            triggerCard = triggerCardToSet,
            triggerController = triggerControllerToSet
        };
    }

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