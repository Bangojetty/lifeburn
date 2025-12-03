using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeckData {
    public int id { get; set; }
    public string deckName { get; set; }
    public List<int> deckList { get; set; }

    public DeckData(int id, string deckName, List<int> deckList) {
        this.id = id;
        this.deckName = deckName;
        this.deckList = deckList;
    }
}
