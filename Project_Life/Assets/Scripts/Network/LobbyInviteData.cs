using System;

[Serializable]
public class LobbyInviteData {
    public int inviteId;
    public int lobbyId;
    public string lobbyType;
    public int inviterId;
    public string inviterDisplayName;
    public int maxPlayers;
    public int currentPlayerCount;
    public DateTime createdAt;
}
