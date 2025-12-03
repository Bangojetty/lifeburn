using System.Collections.Generic;
using System.Diagnostics;
using InGame;
using Unity.VisualScripting;

public class MatchState {
    public int matchId { get; set; }
    public PlayerState playerState { get; set; }
    public OpponentState opponentState { get; set; }
    public int turnPlayerId { get; set; }
    public int prioPlayerId { get; set; }
    public Phase currentPhase { get; set; }
    public List<CardDisplayData> playablesInPlay { get; set; }
    public Stack<StackDisplayData> stack { get; set; }
    public bool secondPass { get; set; }
    public bool allAttackersAssigned { get; set; }
}