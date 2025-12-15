using System.Diagnostics;
using Microsoft.AspNetCore.Components.Rendering;

namespace Server.CardProperties;

public class Condition {
    public ConditionType condition;
    public Scope scope = Scope.All;
    public int minAmount;
    public int? maxAmount;
    public int? amount;  // single amount for exact match conditions
    public bool isOpponent;
    public Zone? zone;
    public List<int>? amounts;
    public Tribe? tribe;
    public TokenType? tokenType;

    public bool Verify(GameMatch gameMatch, Player player, Card? rootTarget = null, Card? sourceCard = null) {
        switch (condition) {
            case ConditionType.FirstSummon:
                return player.totalSummons <= 1;
            case ConditionType.YouHaveMoreLife:
                return player.lifeTotal > gameMatch.GetOpponent(player).lifeTotal;
            case ConditionType.FirstSpell:
                return player.totalSpells <= 1;
            case ConditionType.Control:
                int amountControlled = 0;
                if (tokenType != null) {
                    foreach (Token t in player.tokens) {
                        if (t.tokenType == tokenType) amountControlled++;
                    }
                } else if (tribe != null) {
                    // Count summons on the field with the specified tribe
                    foreach (Card c in player.playField) {
                        if (c.tribe == tribe) amountControlled++;
                    }
                    // Also count tokens with the specified tribe
                    foreach (Token t in player.tokens) {
                        if (t.tribe == tribe) amountControlled++;
                    }
                }
                if (maxAmount != null && amountControlled > maxAmount) return false;
                return amountControlled >= minAmount;
            case ConditionType.RootTargetTribe:
                Debug.Assert(rootTarget != null, "there is no root target for this RootTargetType Condition");
                return rootTarget.tribe == tribe;
            case ConditionType.RootTargetNotSummon:
                Debug.Assert(rootTarget != null, "there is no root target for this RootTargetNotSummon Condition");
                return rootTarget.type != CardType.Summon;
            case ConditionType.TopCardIsMerfolk:
                if (player.deck == null || player.deck.Count == 0) return false;
                Card topCard = player.deck.First();
                return topCard.tribe == Tribe.Merfolk && topCard.type == CardType.Summon;
            case ConditionType.InZone:
                Debug.Assert(zone != null, "InZone condition requires a zone to be specified");
                // If no sourceCard provided, condition can't be verified - return false
                if (sourceCard == null) return false;
                return sourceCard.currentZone == zone;
            case ConditionType.HandSize:
                int handSize = player.hand.Count;
                // Support both single amount and amounts list
                if (amount != null) {
                    return handSize == amount.Value;
                }
                Debug.Assert(amounts != null, "HandSize condition requires amount or amounts to be specified");
                return amounts.Contains(handSize);
            case ConditionType.Spellburnt:
                return player.spellBurnt;
            case ConditionType.SummonsInPlay:
                // Check if any summons exist in play (either player)
                return gameMatch.playerOne.playField.Count > 0 ||
                       gameMatch.playerTwo.playField.Count > 0;
            case ConditionType.NoSummonsInPlay:
                // Check if no summons exist in play
                return gameMatch.playerOne.playField.Count == 0 &&
                       gameMatch.playerTwo.playField.Count == 0;
            default:
                Console.WriteLine("Condition not implemented: " + condition);
                return false;
        }
    }
}