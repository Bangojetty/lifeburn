using System.Diagnostics;

namespace Server; 

public class Matches {
    private Dictionary<int, GameMatch> allMatches = new();
    private Dictionary<GameMatch, MatchManager> matchDataToManager = new();
    private int matchCount = 0;

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
                return gameMatch;
            } 
        }
        Console.WriteLine("THAT'S NOT YOUR MATCH!");
        throw new InvalidDataException();
    }
    
    public void SetMatchData(GameMatch md) {
        allMatches.Add(md.matchId, md);
    }

    public void EndMatch(int id) {
        allMatches.Remove(id);
    }
}