using System;

[Serializable]
public class LobbyPlayerData {
    public int playerId;
    public string displayName;
    public int? deckId;
    public string deckName;
    public bool isReady;
    public bool isHost;
    public string status;
}
