using System;

[Serializable]
public class RecentOpponentData {
    public int id;
    public string displayName;
    public string username;
    public DateTime matchDate;
    public bool isFriend;
    public bool isBlocked;
}
