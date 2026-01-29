using System.Collections.Generic;

public class DraftDisplayState {
    public int draftId { get; set; }
    public string status { get; set; }  // "drafting", "deck_building", "ready"
    public int currentRound { get; set; }  // 0-7
    public int currentPick { get; set; }   // 0-14
    public int passDirection { get; set; } // 1 or -1
    public bool opponentHasSubmitted { get; set; }
    public bool iHaveSubmitted { get; set; }
    public List<int> myDraftedCards { get; set; } = new();
    public List<int> currentPackCards { get; set; } = new();
    public int myPackCount { get; set; }
    public int opponentDraftedCount { get; set; }
    public bool opponentDeckReady { get; set; }
    public int? matchId { get; set; }
}
