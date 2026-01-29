namespace Server;

public class LobbyPlayerData {
    public int playerId { get; set; }
    public string displayName { get; set; }
    public int? deckId { get; set; }
    public string? deckName { get; set; }
    public bool isReady { get; set; }
    public bool isHost { get; set; }
    public string status { get; set; }

    public LobbyPlayerData(int playerId = 0, string displayName = "", int? deckId = null,
                           string? deckName = null, bool isReady = false, bool isHost = false,
                           string status = "active") {
        this.playerId = playerId;
        this.displayName = displayName;
        this.deckId = deckId;
        this.deckName = deckName;
        this.isReady = isReady;
        this.isHost = isHost;
        this.status = status;
    }
}
