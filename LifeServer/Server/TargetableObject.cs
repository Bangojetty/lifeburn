using Server.CardProperties;

namespace Server;

public class TargetableObject {
    public TargetType targetType;

    public Player? player;
    public Card? card;

    public TargetableObject(TargetType targetType, Player? player = null, Card? card = null) {
        this.targetType = targetType;
        this.player = player;
        this.card = card;

    } 
}