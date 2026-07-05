using System.Diagnostics;

namespace Server;

public class Matches {
    private Dictionary<int, GameMatch> allMatches = new();
    private Dictionary<GameMatch, MatchManager> matchDataToManager = new();
    private int matchCount = 0;

    // Track last activity time for each player in each match (matchId -> playerId -> lastActivity)
    private Dictionary<int, Dictionary<int, DateTime>> matchPlayerActivity = new();

    // Track which players are currently in a match (playerId -> matchId)
    private Dictionary<int, int> playerToMatch = new();

    public GameMatch? GetMatchData(int matchId) {
        return allMatches.ContainsKey(matchId) ? allMatches[matchId] : null;
    }

    public int NextMatchId() {
        return ++matchCount;
    }

    public GameMatch ValidatePlayerMatch(int playerId, int matchId) {
        if (allMatches.ContainsKey(matchId)) {
            GameMatch gameMatch = allMatches[matchId];
            if (gameMatch.playerOne.playerId == playerId || gameMatch.playerTwo.playerId == playerId) {
                // Automatically track activity when a valid request is made
                UpdatePlayerActivity(matchId, playerId);
                return gameMatch;
            }
        }
        Console.WriteLine($"THAT'S NOT YOUR MATCH! Player {playerId} tried to access match {matchId}");
        Console.WriteLine($"Active matches: [{string.Join(", ", allMatches.Keys)}]");
        throw new InvalidDataException();
    }

    public void SetMatchData(GameMatch md) {
        allMatches.Add(md.matchId, md);
        // Initialize activity tracking for human players only - bots never poll,
        // so tracking them gets the match killed as "disconnected" after the timeout
        matchPlayerActivity[md.matchId] = new Dictionary<int, DateTime>();
        if (!md.playerOne.isBot) matchPlayerActivity[md.matchId][md.playerOne.playerId] = DateTime.UtcNow;
        if (!md.playerTwo.isBot) matchPlayerActivity[md.matchId][md.playerTwo.playerId] = DateTime.UtcNow;
        // Track players in this match
        playerToMatch[md.playerOne.playerId] = md.matchId;
        playerToMatch[md.playerTwo.playerId] = md.matchId;
        Console.WriteLine($"Match {md.matchId} created - tracking players {md.playerOne.playerId} and {md.playerTwo.playerId}");
    }

    public void EndMatch(int id) {
        if (allMatches.TryGetValue(id, out var match)) {
            // Remove player tracking
            playerToMatch.Remove(match.playerOne.playerId);
            playerToMatch.Remove(match.playerTwo.playerId);
            Console.WriteLine($"Match {id} ended - removed player tracking for {match.playerOne.playerId} and {match.playerTwo.playerId}");
        }
        allMatches.Remove(id);
        matchPlayerActivity.Remove(id);
    }

    // Update last activity timestamp for a player
    public void UpdatePlayerActivity(int matchId, int playerId) {
        if (matchPlayerActivity.TryGetValue(matchId, out var players)) {
            if (players.ContainsKey(playerId)) {
                players[playerId] = DateTime.UtcNow;
            }
        }
    }

    // Check if a player is in an active match
    public int? GetPlayerMatchId(int playerId) {
        return playerToMatch.TryGetValue(playerId, out var matchId) ? matchId : null;
    }

    // Check if a player is currently in any match
    public bool IsPlayerInMatch(int playerId) {
        return playerToMatch.ContainsKey(playerId);
    }

    // Get inactive matches (where a non-priority player hasn't made a request in timeoutSeconds)
    public List<(int matchId, int disconnectedPlayerId)> GetInactiveMatches(int timeoutSeconds) {
        var result = new List<(int matchId, int disconnectedPlayerId)>();
        var cutoffTime = DateTime.UtcNow.AddSeconds(-timeoutSeconds);

        foreach (var kvp in matchPlayerActivity) {
            int matchId = kvp.Key;
            if (!allMatches.TryGetValue(matchId, out var match)) continue;

            // Skip if game is already over
            if (match.isGameOver) continue;

            // Skip if match hasn't been initialized yet (prioPlayerId is 0 or invalid)
            // This happens between match creation and when both players have loaded
            bool prioIsValid = match.prioPlayerId == match.playerOne.playerId ||
                               match.prioPlayerId == match.playerTwo.playerId;
            if (!prioIsValid) {
                continue;
            }

            foreach (var playerActivity in kvp.Value) {
                int playerId = playerActivity.Key;
                DateTime lastActivity = playerActivity.Value;

                // Only check players who don't currently have priority
                // They should be polling for updates, so inactivity means disconnect
                if (match.prioPlayerId != playerId && lastActivity < cutoffTime) {
                    result.Add((matchId, playerId));
                }
            }
        }

        return result;
    }

    // Get all match IDs (for iteration)
    public List<int> GetAllMatchIds() {
        return allMatches.Keys.ToList();
    }

    // Get finished matches that should be cleaned up
    public List<int> GetFinishedMatches() {
        var result = new List<int>();
        foreach (var kvp in allMatches) {
            if (kvp.Value.isGameOver) {
                result.Add(kvp.Key);
            }
        }
        return result;
    }

    // Get match count for debugging
    public int MatchCount => allMatches.Count;
}