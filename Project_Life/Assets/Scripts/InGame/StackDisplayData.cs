using System.Collections;
using System.Collections.Generic;
using InGame;
using Newtonsoft.Json;
using UnityEngine;

public class StackDisplayData {
    [JsonProperty]
    private int stackDisplayId { get; set; }
    public CardDisplayData cardDisplayData;
    public StackObjType stackObjType;
    public List<string> effectStrings;

    protected bool Equals(StackDisplayData other) {
        return stackDisplayId == other.stackDisplayId;
    }

    public override bool Equals(object obj) {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((StackDisplayData)obj);
    }

    public override int GetHashCode() {
        return stackDisplayId;
    }
}
