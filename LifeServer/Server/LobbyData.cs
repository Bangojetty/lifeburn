namespace Server;

public class LobbyData {
    public int id { get; set; }
    public int hostId { get; set; }
    public string hostDisplayName { get; set; }
    public string lobbyType { get; set; }  // "normal" or "tournament"
    public string lobbySubtype { get; set; }  // "constructed" or "draft"
    public string status { get; set; }
    public int maxPlayers { get; set; }
    public int bestOf { get; set; }  // 1, 3, or 5 (best-of series setting)
    public int? matchId { get; set; }
    public int? draftId { get; set; }
    public DateTime createdAt { get; set; }
    public List<LobbyPlayerData> players { get; set; }
    public List<PendingInviteData> pendingInvites { get; set; }

    public LobbyData(int id = 0, int hostId = 0, string hostDisplayName = "",
                     string lobbyType = "normal", string lobbySubtype = "constructed",
                     string status = "waiting", int maxPlayers = 2, int bestOf = 1,
                     int? matchId = null, int? draftId = null, DateTime createdAt = default,
                     List<LobbyPlayerData>? players = null,
                     List<PendingInviteData>? pendingInvites = null) {
        this.id = id;
        this.hostId = hostId;
        this.hostDisplayName = hostDisplayName;
        this.lobbyType = lobbyType;
        this.lobbySubtype = lobbySubtype;
        this.status = status;
        this.maxPlayers = maxPlayers;
        this.bestOf = bestOf;
        this.matchId = matchId;
        this.draftId = draftId;
        this.createdAt = createdAt;
        this.players = players ?? new List<LobbyPlayerData>();
        this.pendingInvites = pendingInvites ?? new List<PendingInviteData>();
    }
}

public class PendingInviteData {
    public int inviteeId { get; set; }
    public string inviteeDisplayName { get; set; }

    public PendingInviteData(int inviteeId = 0, string inviteeDisplayName = "") {
        this.inviteeId = inviteeId;
        this.inviteeDisplayName = inviteeDisplayName;
    }
}
