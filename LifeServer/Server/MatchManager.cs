namespace Server; 

public class MatchManager {
    public GameMatch? matchData { get; set; }


    public MatchManager(GameMatch? matchData = null) {
        this.matchData = matchData;
    }
    
    

}