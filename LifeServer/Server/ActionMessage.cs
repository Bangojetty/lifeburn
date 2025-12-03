namespace Server; 

public class ActionMessage
{ 
    public ActionType actionType { get; set; }
    public int playerId { get; set; }
    public int cardId { get; set; }
    public int targetIndex { get; set; }
    public int xValue { get; set; }
    
    // Implement: "Game Targets" so that cards that ask for targets apply proper triggers and effects.


    public ActionMessage(ActionType actionType, int playerId, int cardId = default, int targetIndex = default, int xValue = default) {
        this.actionType = actionType;
        this.playerId = playerId;
        this.cardId = cardId;
        this.targetIndex = targetIndex;
        this.xValue = xValue;
    }
}

public enum ActionType {
    Cast,
    Attack,
    Ability,
    Pass,
}