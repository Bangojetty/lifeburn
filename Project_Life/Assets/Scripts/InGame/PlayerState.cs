using System.Collections.Generic;

namespace InGame {
    public class PlayerState : ParticipantState {
        // most of this data is not required for client code,
        // but I left it here for security checks down the road
        public List<CardDisplayData> playables { get; set; }
        public List<CardDisplayData> activatables { get; set; }
        public List<GameEvent> eventList { get; set; }
    }
}
