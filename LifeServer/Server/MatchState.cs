using Server.CardProperties;

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

    // Game over tracking
    public bool isGameOver { get; set; }
    public int? winnerId { get; set; }
    public int? loserId { get; set; }

    // Series tracking
    public int bestOf { get; set; }
    public int playerSeriesWins { get; set; }
    public int opponentSeriesWins { get; set; }
    public bool isSeriesOver { get; set; }
    public int? seriesWinnerId { get; set; }

    public MatchState(GameMatch gameMatch, bool isPlayerOne) {
        matchId = gameMatch.matchId;

        Player player = isPlayerOne ? gameMatch.playerOne : gameMatch.playerTwo;
        Player opponent = isPlayerOne ? gameMatch.playerTwo : gameMatch.playerOne;

        // Check for TopCardRevealed passive for each player
        Card? playerRevealedCard = GetRevealedTopCard(player);
        Card? opponentRevealedCard = GetRevealedTopCard(opponent);

        playerState = new PlayerState(player, playerRevealedCard);
        opponentState = new OpponentState(opponent, opponentRevealedCard);
        turnPlayerId = gameMatch.turnPlayerId;
        prioPlayerId = gameMatch.prioPlayerId;
        currentPhase = gameMatch.currentPhase;
        stack = new Stack<StackDisplayData>();
        allAttackersAssigned = gameMatch.allAttackersAssigned;
        foreach (StackObj stackObj in gameMatch.stack) {
            stack.Push(new StackDisplayData(stackObj, gameMatch));
        }

        // Game over tracking
        isGameOver = gameMatch.isGameOver;
        winnerId = gameMatch.winnerId;
        loserId = gameMatch.loserId;

        // Series tracking
        bestOf = gameMatch.bestOf;
        playerSeriesWins = isPlayerOne ? gameMatch.playerOneSeriesWins : gameMatch.playerTwoSeriesWins;
        opponentSeriesWins = isPlayerOne ? gameMatch.playerTwoSeriesWins : gameMatch.playerOneSeriesWins;
        isSeriesOver = gameMatch.isSeriesOver;
        seriesWinnerId = gameMatch.seriesWinnerId;
    }

    // Check if player has TopCardRevealed passive active and return the top card if so
    private static Card? GetRevealedTopCard(Player player) {
        if (player.deck == null || player.deck.Count == 0) return null;

        foreach (Card cardInPlay in player.playField) {
            foreach (PassiveEffect pEffect in cardInPlay.GetPassives()) {
                if (pEffect.passive == Passive.TopCardRevealed) {
                    return player.deck.First();
                }
            }
        }
        return null;
    }
}