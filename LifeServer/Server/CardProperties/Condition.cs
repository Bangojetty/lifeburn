using System.Diagnostics;
using Microsoft.AspNetCore.Components.Rendering;

namespace Server.CardProperties;

public class Condition {
    public ConditionType condition;
    public Scope scope = Scope.All;
    public int minAmount;
    public int amountMin { set => minAmount = value; }  // JSON alias for minAmount
    public int? maxAmount;
    public int? conditionMax { set => maxAmount = value; }  // JSON alias for maxAmount
    public int? amount;  // single amount for exact match conditions
    public bool isOpponent;
    public Zone? zone;
    public List<Zone>? zones;  // For InZones condition - list of valid zones
    public List<int>? amounts;
    public Tribe? tribe;
    public TokenType? tokenType;
    public CardType? cardType;  // Filter by card type (e.g., summon, spell, object)

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
                    // Count cards on the field with the specified tribe
                    foreach (Card c in player.playField) {
                        if (c.tribe != tribe) continue;
                        // Filter by cardType if specified (e.g., only count summons, not objects)
                        if (cardType != null && c.type != cardType) continue;
                        amountControlled++;
                    }
                    // Also count tokens with the specified tribe
                    foreach (Token t in player.tokens) {
                        if (t.tribe != tribe) continue;
                        if (cardType != null && t.type != cardType) continue;
                        amountControlled++;
                    }
                }
                if (maxAmount != null && amountControlled > maxAmount) return false;
                // If maxAmount is specified and minAmount is not, allow 0 (e.g., "control 0 goblins")
                // Otherwise, default to requiring at least 1 for Control conditions
                int requiredAmount;
                if (maxAmount != null && minAmount == 0) {
                    requiredAmount = 0;  // Allow 0 when maxAmount is set and minAmount is not
                } else {
                    requiredAmount = minAmount > 0 ? minAmount : 1;
                }
                return amountControlled >= requiredAmount;
            case ConditionType.RootTargetTribe:
                Debug.Assert(rootTarget != null, "there is no root target for this RootTargetType Condition");
                return rootTarget.tribe == tribe;
            case ConditionType.IsTribe:
                // Check if the target card is of the specified tribe
                if (rootTarget == null) return false;
                return rootTarget.tribe == tribe;
            case ConditionType.IsNotTribe:
                // Check if the target card is NOT of the specified tribe
                if (rootTarget == null) return true;  // No target means condition is met
                return rootTarget.tribe != tribe;
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
            case ConditionType.InZones:
                Debug.Assert(zones != null && zones.Count > 0, "InZones condition requires zones list to be specified");
                // If no sourceCard provided, condition can't be verified - return false
                if (sourceCard == null) return false;
                return zones.Contains(sourceCard.currentZone);
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
            case ConditionType.TargetAttack:
                // Check if target's attack is >= minAmount (e.g., GobLaunch: if target has 4+ attack)
                if (rootTarget == null) return false;
                int targetAttack = rootTarget.GetAttack();
                if (maxAmount != null && targetAttack > maxAmount) return false;
                return targetAttack >= minAmount;
            case ConditionType.TriggerWasSpellburnt:
                // Check if the triggering spell was cast with spellburnt
                return gameMatch.currentTriggerContext?.wasSpellburnt ?? false;
            case ConditionType.TriggerNotSpellburnt:
                // Check if the triggering spell was NOT cast with spellburnt
                return !(gameMatch.currentTriggerContext?.wasSpellburnt ?? false);
            case ConditionType.HasInZone:
                // Check if player has cards matching criteria in specified zone
                Debug.Assert(zone != null, "HasInZone condition requires a zone to be specified");
                List<Card> cardsInZone = zone switch {
                    Zone.Graveyard => player.graveyard,
                    Zone.Hand => player.hand,
                    Zone.Deck => player.deck ?? new List<Card>(),
                    _ => new List<Card>()
                };
                int matchingCount = 0;
                foreach (Card c in cardsInZone) {
                    if (tribe != null && c.tribe != tribe) continue;
                    matchingCount++;
                }
                int required = minAmount > 0 ? minAmount : 1;
                return matchingCount >= required;
            case ConditionType.Attacked:
                // Check if player attacked this turn (for Plant Grower post-combat sacrifice)
                return player.attackedThisTurn;
            case ConditionType.DidntAttack:
                // Check if player did NOT attack this turn (for Blooming Marsh herb creation)
                return !player.attackedThisTurn;
            case ConditionType.OpponentHasMore:
                // Check if opponent has more of a specified type (e.g., summons)
                Player opponent = gameMatch.GetOpponent(player);
                int playerCount = 0;
                int opponentCount = 0;
                if (cardType == CardType.Summon) {
                    // Count summons + tokens for each player
                    playerCount = player.playField.Count + player.tokens.Count;
                    opponentCount = opponent.playField.Count + opponent.tokens.Count;
                }
                Console.WriteLine($"[OpponentHasMore] Player has {playerCount} summons, opponent has {opponentCount}");
                return opponentCount > playerCount;
            case ConditionType.EnteredZoneThisTurn:
                // Check if a token of specified type entered play this turn
                if (zone == Zone.Play && tokenType != null) {
                    bool hasToken = player.tokensCreatedThisTurn.Any(t => t.tokenType == tokenType);
                    Console.WriteLine($"[EnteredZoneThisTurn] Checking for {tokenType} tokens created this turn: {hasToken}");
                    return hasToken;
                }
                return false;
            case ConditionType.OneOneInPlay:
                // Check if any player controls a 1/1 summon (for Merfolk Elite's cantBeAttacked)
                foreach (Card c in gameMatch.playerOne.playField) {
                    if (c.GetAttack() == 1 && c.GetDefense() == 1) return true;
                }
                foreach (Card c in gameMatch.playerTwo.playField) {
                    if (c.GetAttack() == 1 && c.GetDefense() == 1) return true;
                }
                // Also check tokens
                foreach (Token t in gameMatch.playerOne.tokens) {
                    if (t.GetAttack() == 1 && t.GetDefense() == 1) return true;
                }
                foreach (Token t in gameMatch.playerTwo.tokens) {
                    if (t.GetAttack() == 1 && t.GetDefense() == 1) return true;
                }
                return false;
            default:
                Console.WriteLine("Condition not implemented: " + condition);
                return false;
        }
    }
}