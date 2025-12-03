using Microsoft.Extensions.Options;
using Server.CardProperties;

namespace Server;

public class PlayerChoice {
    public List<string> options;
    public string optionMessage;

    public PlayerChoice(List<string> options, string optionMessage) {
        this.options = options;
        this.optionMessage = optionMessage;
    }
}