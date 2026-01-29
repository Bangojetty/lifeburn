namespace Server;

public class RecentOpponentData {
    public int id { get; set; }
    public string displayName { get; set; }
    public string username { get; set; }
    public DateTime matchDate { get; set; }
    public bool isFriend { get; set; }
    public bool isBlocked { get; set; }

    public RecentOpponentData(int id = 0, string displayName = "", string username = "",
                              DateTime matchDate = default, bool isFriend = false, bool isBlocked = false) {
        this.id = id;
        this.displayName = displayName;
        this.username = username;
        this.matchDate = matchDate;
        this.isFriend = isFriend;
        this.isBlocked = isBlocked;
    }
}
