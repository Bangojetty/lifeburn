using System;
using System.Collections.Generic;

[Serializable]
public class LobbyData {
    public int id;
    public int hostId;
    public string hostDisplayName;
    public string lobbyType;      // "normal" or "tournament"
    public string lobbySubtype;   // "constructed" or "draft"
    public string status;
    public int maxPlayers;
    public int bestOf;            // 1, 3, or 5 (best-of series setting)
    public int? matchId;
    public int? draftId;
    public DateTime createdAt;
    public List<LobbyPlayerData> players;
    public List<PendingInviteData> pendingInvites;
}

[Serializable]
public class PendingInviteData {
    public int inviteeId;
    public string inviteeDisplayName;
}
