namespace Server;

public class LobbyInviteData {
    public int inviteId { get; set; }
    public int lobbyId { get; set; }
    public string lobbyType { get; set; }
    public int inviterId { get; set; }
    public string inviterDisplayName { get; set; }
    public int maxPlayers { get; set; }
    public int currentPlayerCount { get; set; }
    public DateTime createdAt { get; set; }

    public LobbyInviteData(int inviteId = 0, int lobbyId = 0, string lobbyType = "1v1",
                           int inviterId = 0, string inviterDisplayName = "",
                           int maxPlayers = 2, int currentPlayerCount = 0,
                           DateTime createdAt = default) {
        this.inviteId = inviteId;
        this.lobbyId = lobbyId;
        this.lobbyType = lobbyType;
        this.inviterId = inviterId;
        this.inviterDisplayName = inviterDisplayName;
        this.maxPlayers = maxPlayers;
        this.currentPlayerCount = currentPlayerCount;
        this.createdAt = createdAt;
    }
}
