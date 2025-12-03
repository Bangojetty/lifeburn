using System.Collections.Generic;

namespace InGame {
    public class PlayerChoice {
        public List<string> options;
        public string optionMessage;
        
        public PlayerChoice(string choiceType, List<string> options, string optionMessage) {
            this.options = options;
            this.optionMessage = optionMessage;
        }
    }
}