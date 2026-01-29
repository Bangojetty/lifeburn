namespace Server;

public class BlockedPlayerData {
    public int id { get; set; }
    public string displayName { get; set; }
    public string username { get; set; }

    public BlockedPlayerData(int id = 0, string displayName = "", string username = "") {
        this.id = id;
        this.displayName = displayName;
        this.username = username;
    }
}
