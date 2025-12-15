using System.Text.Json;

namespace Server;

public class OpponentState : ParticipantState {
    // Only populated for bot opponents (test matches)
    public List<CardDisplayData>? hand { get; set; }

    public OpponentState(Player player) {
        playerName = player.playerName;
        uid = player.uid;
        lifeTotal = player.lifeTotal;
        spellBurnt = player.spellBurnt;
        nextSpellFree = player.nextSpellFree;
        spellCounter = player.spellCounter;
        handAmount = player.hand.Count;
        deckAmount = player.deck?.Count ?? 0;

        // Show bot's hand in test matches
        if (player.isBot) {
            hand = player.hand.Select(c => new CardDisplayData(c)).ToList();
        }
    }
}