namespace Server;

public class MatchState {
    public int matchId { get; set; }
    public PlayerState playerState { get; set; }
    public OpponentState opponentState { get; set; }
    public int turnPlayerId { get; set; }
    public int prioPlayerId { get; set; }
    public Phase currentPhase { get; set; }
    public List<CardDisplayData> playablesInPlay { get; set; }
    public Stack<StackDisplayData> stack { get; set; }
    public bool allAttackersAssigned { get; set; }

    public MatchState(GameMatch gameMatch, bool isPlayerOne) {
        matchId = gameMatch.matchId;
        playerState = new PlayerState(isPlayerOne ? gameMatch.playerOne : gameMatch.playerTwo);
        opponentState = new OpponentState(isPlayerOne ? gameMatch.playerTwo : gameMatch.playerOne);
        turnPlayerId = gameMatch.turnPlayerId;
        prioPlayerId = gameMatch.prioPlayerId;
        currentPhase = gameMatch.currentPhase;
        stack = new Stack<StackDisplayData>();
        allAttackersAssigned = gameMatch.allAttackersAssigned;
        foreach (StackObj stackObj in gameMatch.stack) {
            stack.Push(new StackDisplayData(stackObj, gameMatch));
        }
    }
}