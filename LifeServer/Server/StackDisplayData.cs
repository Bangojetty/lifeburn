using System.Diagnostics;
using Newtonsoft.Json;
using Server.CardProperties;

namespace Server;

public class StackDisplayData {
    private static int idCounter = 0;
    
    [JsonProperty]
    private int stackDisplayId { get; }
    public CardDisplayData cardDisplayData { get; set; }
    public StackObjType stackObjType;
    public List<string> effectStrings { get; set; }

    public StackDisplayData(StackObj stackObj, GameMatch gameMatch) {
        cardDisplayData = new CardDisplayData(stackObj.sourceCard);
        stackObjType = stackObj.stackObjType;
        effectStrings = new List<string>();
        // if it's a trigger and has a custom description (cards with triggers don't have this,
        // only StackObjs that ARE triggers -> see Match.CreateStackObj
        if (stackObj.customDescription != null) {
            effectStrings.Add(stackObj.customDescription);
        } else {
            // otherwise, get it's list of effect strings
            if (stackObj.effects != null) {
                foreach (Effect effect in stackObj.effects) {
                    effectStrings.Add(effect.EffectToString(gameMatch));
                }
            }
        }
        stackDisplayId = ++idCounter;
    }
    
    protected bool Equals(StackDisplayData other) {
        return stackDisplayId == other.stackDisplayId;
    }

    public override bool Equals(object? obj) {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((StackDisplayData)obj);
    }

    public override int GetHashCode() {
        return stackDisplayId;
    }
}