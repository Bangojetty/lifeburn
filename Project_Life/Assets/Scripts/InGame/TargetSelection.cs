using System.Collections.Generic;

namespace InGame {
    public class TargetSelection {
        public List<int> selectableUids { get; set; }
        public int amount { get; set; }  // Maximum amount to select
        public int minAmount { get; set; }  // Minimum amount to select (0 for optional)
        public string message { get; set; }
        public bool requireOneFromEach { get; set; }
        public List<int> playerUids { get; set; }
        public List<int> opponentUids { get; set; }
    }
}