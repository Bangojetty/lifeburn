namespace Server;

public class FriendRequestData {
    public int requestId { get; set; }
    public int senderId { get; set; }
    public string senderDisplayName { get; set; }
    public string senderUsername { get; set; }
    public int receiverId { get; set; }
    public string receiverDisplayName { get; set; }
    public string receiverUsername { get; set; }
    public DateTime createdAt { get; set; }

    public FriendRequestData(int requestId = 0, int senderId = 0, string senderDisplayName = "",
                             string senderUsername = "", int receiverId = 0, string receiverDisplayName = "",
                             string receiverUsername = "", DateTime createdAt = default) {
        this.requestId = requestId;
        this.senderId = senderId;
        this.senderDisplayName = senderDisplayName;
        this.senderUsername = senderUsername;
        this.receiverId = receiverId;
        this.receiverDisplayName = receiverDisplayName;
        this.receiverUsername = receiverUsername;
        this.createdAt = createdAt;
    }
}
