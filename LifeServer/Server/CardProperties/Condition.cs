using System.Diagnostics;
using Microsoft.AspNetCore.Components.Rendering;

namespace Server.CardProperties;

public class Condition {
    public ConditionType condition;
    public bool self;
    public int minAmount;
    public int? maxAmount;
    public bool isOpponent;
    public Zone? zone;
    public List<int>? amounts;
    public Tribe? tribe;
    public TokenType? tokenType;

    public bool Verify(GameMatch gameMatch, Player player, Card? rootTarget = null) {
        switch (condition) {
            case ConditionType.FirstSummon:
                return player.totalSummons <= 1;
            case ConditionType.YouHaveMoreLife:
                return player.lifeTotal > gameMatch.GetOpponent(player).lifeTotal;
            case ConditionType.FirstSpell:
                return player.totalSpells <= 1;
            case ConditionType.Control:
                if (tokenType != null) {
                    int amountControlled = 0;
                    switch (tokenType) {
                        case TokenType.Stone or TokenType.Herb:
                            foreach (Token t in player.tokens) {
                                if (t.tokenType == tokenType) amountControlled++;
                            }
                            break;
                    }
                    if (amountControlled > maxAmount) return false;
                    return amountControlled >= minAmount;
                }
                return false;
            case ConditionType.RootTargetTribe:
                Debug.Assert(rootTarget != null, "there is no root target for this RootTargetType Condition");
                return rootTarget.tribe == tribe;
            default:
                Console.WriteLine("Condition not implemented: " + condition);
                return false;
        }
    }
}