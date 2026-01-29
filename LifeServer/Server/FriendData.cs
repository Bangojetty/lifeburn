namespace Server;

public class FriendData {
    public int id { get; set; }
    public string displayName { get; set; }
    public string username { get; set; }
    public bool isOnline { get; set; }
    public DateTime? lastActive { get; set; }

    public FriendData(int id = 0, string displayName = "", string username = "",
                      bool isOnline = false, DateTime? lastActive = null) {
        this.id = id;
        this.displayName = displayName;
        this.username = username;
        this.isOnline = isOnline;
        this.lastActive = lastActive;
    }
}
