#nullable enable
using System.Collections.Generic;
using InGame.CardDataEnums;
using JetBrains.Annotations;
using Unity.VisualScripting;

namespace InGame {
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
        public EventType eventType { get; set; }
        public bool isOpponent { get; set; }
        public PlayerChoice? playerChoice { get; set; }
        public TargetSelection targetSelection { get; set; }
        public List<StackDisplayData>? triggerOrderingList { get; set; }
        public CostType costType { get; set; }
        public bool universalBool { get; set; }
        public int universalInt { get; set; }
        
        public GameEvent(EventType eventType,  bool isOpponent, TargetSelection targetSelection, CardDisplayData? focusCard = null) {
            this.eventType = eventType;
            this.focusCard = focusCard;
            this.isOpponent = isOpponent;
            this.targetSelection = targetSelection;
        }
    }
}