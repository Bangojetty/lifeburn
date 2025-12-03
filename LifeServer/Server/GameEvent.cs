using System.Diagnostics;
using System.Reflection.Metadata;
using Server.CardProperties;

namespace Server;

public class GameEvent {
    public CardDisplayData? focusCard { get; set; }
    public CardDisplayData? targetCard { get; set; }
    public CardDisplayData? sourceCard { get; set; }
    public Zone zone { get; set; }
    public Zone? sourceZone { get; set; }
    public StackDisplayData? focusStackObj { get; set; }
    public List<CardDisplayData>? cards { get; set; }
    public List<string>? eventMessages { get; set; }
    public List<CardSelectionData>? cardSelectionDatas { get; set; }
    public (int, int)? attackUids { get; set; }
    public int focusUid { get; set; }
    public List<int>? focusUidList { get; set; }
    public int attackerUid { get; set; }
    public int defenderUid { get; set; }
    public int amount { get; set; }
    public EventType? eventType { get; set; }
    public bool isOpponent { get; set; }
    public PlayerChoice? playerChoice { get; set; }
    public TargetSelection? targetSelection { get; set; }
    public List<StackDisplayData>? triggerOrderingList { get; set; }
    public CostType costType { get; set; }
    
    // universal properties
    public bool universalBool { get; set; }
    public int universalInt { get; set; }

    
    // Basic Constructor
    public GameEvent(EventType eventType, bool isOpponent = false) {
        this.eventType = eventType;
        this.isOpponent = isOpponent;
    }
    
    // StackDisplayData
    public static GameEvent CreateStackEvent(EventType eventType, StackDisplayData stackDisplayData, bool isOpponent = false) {
        GameEvent gEvent = new GameEvent(eventType, isOpponent);
        gEvent.focusStackObj = stackDisplayData;
        return gEvent;
    }
    
    // TriggerOrdering
    public static GameEvent CreateTriggerOrderingEvent(List<StackDisplayData> triggerOrderingList) {
        GameEvent gEvent = new GameEvent(EventType.TriggerOrdering);
        gEvent.triggerOrderingList = triggerOrderingList.ToList();
        return gEvent;
    }
    
    // LookAtDeck
    public static GameEvent CreateLookAtDeckEvent(List<CardSelectionData> cardSelectionDatas, List<CardDisplayData> cardsToLookAt, bool isOpponent = false) {
        GameEvent gEvent = new GameEvent(EventType.LookAtDeck, isOpponent);
        gEvent.cards = cardsToLookAt.ToList();
        gEvent.cardSelectionDatas = cardSelectionDatas.ToList();
        return gEvent;
    }
    
    // Combat
    public static GameEvent CreateCombatEvent(int attackerUid, int defenderUid, int amount, bool isOpponent = false) {
        GameEvent gEvent = new GameEvent(EventType.Combat, isOpponent);
        gEvent.attackerUid = attackerUid;
        gEvent.defenderUid = defenderUid;
        gEvent.amount = amount;
        return gEvent;
    }
    
    // Uid
    public static GameEvent CreateUidEvent(EventType eventType, int uid, bool isOpponent = false) {
        GameEvent gEvent = new GameEvent(eventType, isOpponent);
        gEvent.focusUid = uid;
        return gEvent;
    }
    
    // Multi-Uid
    public static GameEvent CreateMultiUidEvent(EventType eventType, List<int> uids, bool isOpponent = false) {
        GameEvent gEvent = new GameEvent(eventType, isOpponent);
        gEvent.focusUidList = uids.ToList();
        return gEvent;
    }
    
    // Amount
    public static GameEvent CreateGameEventWithAmount(EventType eventType, bool isOpponent, int amount) {
        GameEvent gEvent = new GameEvent(eventType, isOpponent);
        gEvent.amount = amount;
        return gEvent;
    }

    // Zone
    public static GameEvent CreateZoneGameEvent(Zone zone, CardDisplayData? focusCard = null, Zone? sourceZone = null) {
        GameEvent gEvent = new GameEvent(EventType.SendToZone);
        gEvent.focusCard = focusCard;
        gEvent.zone = zone;
        gEvent.sourceZone = sourceZone;
        return gEvent;
    }

    // Tribute
    public static GameEvent CreateTributeRequirementEvent(CardDisplayData focusCard, List<int> possibleTributeUids) {
        GameEvent gEvent = new GameEvent(EventType.TributeRequirement);
        gEvent.focusCard = focusCard;
        gEvent.focusUidList = possibleTributeUids;
        return gEvent;
    }
    
    // Refresh Card Display
    public static GameEvent CreateRefreshCardDisplayEvent(Card? card = null, List<CardDisplayData>? cardsToRefresh = null) {
        GameEvent gEvent = new GameEvent(EventType.RefreshCardDisplays);
        // create a new list if there's only one card, otherwise just use the list passed in
        gEvent.cards = card != null ? new List<CardDisplayData> { new(card) } : cardsToRefresh;
        return gEvent;
    }

    // Target Selection
    public static GameEvent CreateTargetSelectionEvent(TargetSelection targetSelection) {
        GameEvent gEvent = new GameEvent(EventType.TargetSelection);
        gEvent.targetSelection = targetSelection;
        return gEvent;
    }
    
    // Attack
    public static GameEvent CreateAttackEvent((int, int) attackUids, bool isAssign) {
        GameEvent gEvent = new GameEvent(EventType.Attack);
        gEvent.attackUids = attackUids;
        gEvent.universalBool = isAssign;
        return gEvent;
    }
    
    // CardDisplayData
    public static GameEvent CreateCardEvent(EventType eventType, CardDisplayData cardDisplayData, bool isOpponent = false, bool universalBool = false) {
        GameEvent gEvent = new GameEvent(eventType, isOpponent);
        gEvent.focusCard = cardDisplayData;
        gEvent.universalBool = universalBool;
        return gEvent;
    }
    
    // Toggleable
    public static GameEvent CreateToggleableEvent(EventType eventType, bool universalBool, bool isOpponent = false) {
        GameEvent gEvent = new GameEvent(eventType, isOpponent);
        gEvent.universalBool = universalBool;
        return gEvent;
    }
    
    // Option
    public static GameEvent CreateOptionEvent(PlayerChoice playerChoice) {
        GameEvent gEvent = new GameEvent(EventType.Choice);
        gEvent.isOpponent = false;
        gEvent.playerChoice = playerChoice;
        return gEvent;
    }
    
    // Cost
    public static GameEvent CreateCostEvent(CostType costType, int amount, List<int>? selectableUids = null, List<string>? eventMessages = null) {
        GameEvent gEvent = new GameEvent(EventType.Cost);
        gEvent.isOpponent = false;
        gEvent.costType = costType;
        gEvent.amount = amount;
        if (selectableUids != null) {
            gEvent.focusUidList = selectableUids.ToList();
        }
        if (eventMessages != null) {
            gEvent.eventMessages = eventMessages.ToList();
        }
        return gEvent;
    }
    
    // Amount Selection
    public static GameEvent CreateAmountSelectionEvent(bool isX) {
        GameEvent gEvent = new GameEvent(EventType.AmountSelection);
        gEvent.universalBool = isX;
        return gEvent;
    }
    
    
    // endgame
    public static GameEvent CreateEndGameEvent(int winningPlayerUid) {
        GameEvent gEvent = new GameEvent(EventType.EndGame);
        gEvent.isOpponent = false;
        gEvent.focusUid = winningPlayerUid;
        return gEvent;
    }
    
    // Copy Constructor
    public GameEvent(GameEvent playerEvent) {
        focusCard = playerEvent.focusCard;
        targetCard = playerEvent.targetCard;
        sourceCard = playerEvent.sourceCard;
        zone = playerEvent.zone;
        sourceZone = playerEvent.sourceZone;
        focusStackObj = playerEvent.focusStackObj;
        if(playerEvent.cards != null) cards = playerEvent.cards.ToList();
        if(playerEvent.eventMessages != null) eventMessages = playerEvent.eventMessages.ToList();
        if(playerEvent.focusUidList != null) focusUidList = playerEvent.focusUidList.ToList();
        if(playerEvent.cardSelectionDatas != null) cardSelectionDatas = playerEvent.cardSelectionDatas.ToList();
        if (playerEvent.attackUids != null) {
            attackUids = playerEvent.attackUids;
        }
        focusUid = playerEvent.focusUid;
        attackerUid = playerEvent.attackerUid;
        defenderUid = playerEvent.defenderUid;
        amount = playerEvent.amount;
        eventType = playerEvent.eventType;
        isOpponent = playerEvent.isOpponent;
        if (playerChoice != null) playerChoice = playerEvent.playerChoice;
        if (targetSelection != null) targetSelection = playerEvent.targetSelection;
        if(playerEvent.triggerOrderingList != null) triggerOrderingList = playerEvent.triggerOrderingList.ToList();
        costType = playerEvent.costType;
        universalBool = playerEvent.universalBool;
        universalInt = playerEvent.universalInt;

    }

}