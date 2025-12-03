using System.Text.Json;

namespace Server;

public class OpponentState : ParticipantState {
    public OpponentState(Player player) {
        playerName = player.playerName;
        uid = player.uid;
        lifeTotal = player.lifeTotal;
        spellBurnt = player.spellBurnt;
        spellCounter = player.spellCounter;
        handAmount = player.hand.Count;
        deckAmount = player.deck?.Count ?? 0;
    }
}