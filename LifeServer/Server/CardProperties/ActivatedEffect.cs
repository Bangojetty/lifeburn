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
    public List<AlternateCost>? alternateCosts;
    public bool summonTiming;  // If true, can only activate at times you could cast a summon (your turn, main phase, empty stack)
    public bool activateFromHand;  // If true, can be activated from hand as alternative to casting (e.g., Transparent Plant discard ability)
    public TargetType? targetType;  // Target type for the ability (e.g., summon)
    public bool immediate;  // If true, effect resolves immediately without going on the stack

    // non-json
    public Card sourceCard;
    public Card grantedBy;  // The card that granted this activated effect (for cleanup)
    public bool usedThisTurn;  // Tracks if this ability has been used this turn (for oncePerTurn)



    public bool CostIsAvailable(GameMatch gameMatch, Player player) {
        // Check once per turn restriction
        if (oncePerTurn && usedThisTurn) return false;

        // If alternateCosts is defined, check if ANY of them can be paid
        if (alternateCosts != null && alternateCosts.Count > 0) {
            foreach (AlternateCost altCost in alternateCosts) {
                if (CanPayAlternateCost(gameMatch, player, altCost)) {
                    return true;
                }
            }
            return false;
        }

        // Legacy costType handling
        int playerAmount = 0;
        Qualifier costQualifier = new Qualifier(this, player);
        switch (costType) {
            case CostType.Sacrifice:
                playerAmount += gameMatch.GetAllCardsControlled(player).Count(c => gameMatch.QualifyCard(c, costQualifier));
                break;
            case CostType.Discard:
                playerAmount += player.allCardsPlayer.Count(c => gameMatch.QualifyCard(c, costQualifier));
                break;
            case CostType.LoseLife:
            case CostType.Life:
                // Player must have more life than the cost (can't pay if it would kill you)
                return player.lifeTotal > amount;
        }
        if (playerChosenAmount && playerAmount > 0) return true;
        return playerAmount >= amount;
    }

    private bool CanPayAlternateCost(GameMatch gameMatch, Player player, AlternateCost altCost) {
        int matchingCount = 0;
        switch (altCost.altCostType) {
            case AltCostType.Sacrifice:
            case AltCostType.Tribute:
                if (altCost.tokenType != null) {
                    matchingCount = player.tokens.Count(t => t.tokenType == altCost.tokenType);
                } else if (altCost.tribe != null) {
                    matchingCount = player.tokens.Count(t => t.tribe == altCost.tribe);
                    matchingCount += gameMatch.GetAllCardsControlled(player).Count(c => c.tribe == altCost.tribe);
                } else if (altCost.cardType != null) {
                    matchingCount = gameMatch.GetAllCardsControlled(player).Count(c => c.type == altCost.cardType);
                }
                break;
            case AltCostType.Discard:
                foreach (Card c in player.hand) {
                    bool matches = true;
                    if (altCost.tribe != null && c.tribe != altCost.tribe) matches = false;
                    if (altCost.cardType != null && c.type != altCost.cardType) matches = false;
                    if (matches) matchingCount++;
                }
                break;
            case AltCostType.ExileFromHand:
                foreach (Card c in player.hand) {
                    bool matches = true;
                    if (altCost.tribe != null && c.tribe != altCost.tribe) matches = false;
                    if (altCost.cardType != null && c.type != altCost.cardType) matches = false;
                    if (matches) matchingCount++;
                }
                break;
        }
        return matchingCount >= altCost.amount;
    }


    public void SetAmount(int newAmount) {
        amount = newAmount;
        foreach (Effect e in effects) {
            if (e.amountBasedOn is not AmountBasedOn.ActivatedCost) continue;
            e.amount = amount;
            e.amountBasedOn = null;
        }
    }

    /// <summary>
    /// Checks if this activated effect has valid targets available.
    /// Used for activateFromHand abilities to determine if the discard option should be shown.
    /// </summary>
    public bool HasValidTargets(GameMatch gameMatch, Player player) {
        if (targetType == null) return true;  // No targeting required

        Qualifier qualifier = new Qualifier(this, player);

        switch (targetType) {
            case TargetType.Summon:
                // Check all summons in play
                List<Card> allSummons = gameMatch.GetAllSummonsInPlay();
                return allSummons.Any(c => gameMatch.QualifyCard(c, qualifier));
            case TargetType.Token:
                // Check all tokens
                List<Token> allTokens = new List<Token>();
                allTokens.AddRange(player.tokens);
                allTokens.AddRange(gameMatch.GetOpponent(player).tokens);
                return allTokens.Any(t => gameMatch.QualifyCard(t, qualifier));
            default:
                return true;  // Other target types - assume valid
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
            effects = effects.Select(e => e.Clone()).ToList(),
            alternateCosts = alternateCosts?.ToList(),
            summonTiming = summonTiming,
            activateFromHand = activateFromHand,
            targetType = targetType,
            sourceCard = sourceCard,
            grantedBy = grantedBy
        };
    }
}