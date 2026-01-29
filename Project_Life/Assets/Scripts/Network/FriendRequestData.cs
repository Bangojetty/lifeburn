using System;

[Serializable]
public class FriendRequestData {
    public int requestId;
    public int senderId;
    public string senderDisplayName;
    public string senderUsername;
    public int receiverId;
    public string receiverDisplayName;
    public string receiverUsername;
    public DateTime createdAt;
}
