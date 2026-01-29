using System.Text.Json;

namespace Server;

public class OpponentState : ParticipantState {
    // Only populated for bot opponents (test matches)
    public List<CardDisplayData>? hand { get; set; }

    // Revealed top card (from TopCardRevealed passive)
    public CardDisplayData? revealedTopCard { get; set; }

    public OpponentState(Player player, Card? topCardRevealed = null) {
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

        // Set revealed top card if applicable (from TopCardRevealed passive)
        revealedTopCard = topCardRevealed != null ? new CardDisplayData(topCardRevealed) : null;
    }
}