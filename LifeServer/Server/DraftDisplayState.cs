using System.Collections.Generic;

namespace Server;

/// <summary>
/// Client-facing draft state that hides opponent's specific picks and cards
/// </summary>
public class DraftDisplayState {
    public int draftId { get; set; }
    public string status { get; set; }  // "drafting", "deck_building", "ready"
    public int currentRound { get; set; }  // 0-7
    public int currentPick { get; set; }   // 0-14
    public int passDirection { get; set; } // 1 or -1
    public bool opponentHasSubmitted { get; set; }
    public bool iHaveSubmitted { get; set; }
    public List<int> myDraftedCards { get; set; } = new();  // Cards I've drafted so far
    public List<int> currentPackCards { get; set; } = new();  // Current pack to pick from
    public int myPackCount { get; set; }  // How many packs this player started with (for UI)
    public int opponentDraftedCount { get; set; }  // How many cards opponent has drafted
    public bool opponentDeckReady { get; set; }   // Has opponent submitted their deck?
    public int? matchId { get; set; }  // Set when match is created after deck building

    public DraftDisplayState() { }

    public DraftDisplayState(DraftState fullState, int playerId) {
        draftId = fullState.draftId;
        status = fullState.status;
        currentRound = fullState.currentRound;
        currentPick = fullState.currentPick;
        passDirection = fullState.passDirection;
        matchId = fullState.matchId;

        var myPlayer = fullState.GetPlayer(playerId);
        var opponent = fullState.GetOpponent(playerId);

        if (myPlayer != null) {
            myDraftedCards = myPlayer.draftedCardIds;
            currentPackCards = myPlayer.currentPackCards;
            iHaveSubmitted = myPlayer.hasSubmittedPick;
            myPackCount = myPlayer.packs?.Count ?? 4;
        }

        if (opponent != null) {
            opponentHasSubmitted = opponent.hasSubmittedPick;
            opponentDraftedCount = opponent.draftedCardIds.Count;
            opponentDeckReady = opponent.deckReady;
        }
    }
}
