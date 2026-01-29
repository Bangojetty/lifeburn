using System;
using System.Collections.Generic;

namespace Server;

public class DraftState {
    public int draftId { get; set; }
    public int lobbyId { get; set; }
    public string status { get; set; } = "drafting";  // "drafting", "deck_building", "ready"
    public int currentRound { get; set; } = 0;  // 0-3 (which pack round we're on)
    public int currentPick { get; set; } = 0;   // 0-14 (which pick within the current round)
    public int passDirection { get; set; } = 1; // 1 = left, -1 = right (alternates each round)
    public List<DraftPlayerState> players { get; set; } = new();
    public DateTime createdAt { get; set; } = DateTime.UtcNow;
    public int? matchId { get; set; }  // Set when match is created after deck building

    public DraftPlayerState GetPlayer(int playerId) {
        return players.Find(p => p.playerId == playerId);
    }

    public DraftPlayerState GetOpponent(int playerId) {
        return players.Find(p => p.playerId != playerId);
    }

    public bool BothPlayersSubmitted() {
        return players.Count == 2 && players[0].hasSubmittedPick && players[1].hasSubmittedPick;
    }

    public bool BothDecksReady() {
        return players.Count == 2 && players[0].deckReady && players[1].deckReady;
    }
}

public class DraftPlayerState {
    public int playerId { get; set; }
    public string displayName { get; set; }
    public List<DraftPack> packs { get; set; } = new();  // 4 packs initially assigned to this player
    public List<int> draftedCardIds { get; set; } = new();  // All cards picked so far
    public List<int> currentPackCards { get; set; } = new();  // Cards in current pack to pick from
    public int? submittedPick { get; set; }  // Card ID they picked (null until submitted)
    public bool hasSubmittedPick { get; set; } = false;
    public List<int> finalDeck { get; set; } = new();  // 40-card deck after building
    public bool deckReady { get; set; } = false;  // True when deck building complete
}

public class DraftPack {
    public int packIndex { get; set; }  // 0-3 (which of original 4 packs for this player)
    public List<int> cardIds { get; set; } = new();  // Cards remaining in this pack
    public bool isOpened { get; set; } = false;
}
