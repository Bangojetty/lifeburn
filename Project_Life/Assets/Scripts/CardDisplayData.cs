using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardDisplayData {
    public int uid { get; set; }
    public int id { get; set; }
    public string name { get; set; }
    public int cost{ get; set; }
    public CardType type { get; set; }
    public int? attack { get; set; }
    public int? defense { get; set; }
    public int? baseAttack { get; set; }
    public int? baseDefense { get; set; }
    public List<Keyword> keywords { get; set; }
    public Tribe tribe { get; set; }
    public Rarity rarity { get; set; }
    public string description { get; set; }
    public string additionalDescription { get; set; }
    public TokenType? tokenType { get; set; }
    public bool hasXCost { get; set; }

    protected bool Equals(CardDisplayData other) {
        return uid == other.uid;
    }

    public override bool Equals(object obj) {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((CardDisplayData)obj);
    }

    public override int GetHashCode() {
        return uid;
    }
}
