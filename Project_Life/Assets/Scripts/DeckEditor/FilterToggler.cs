using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class FilterToggler : MonoBehaviour {
    public DeckEditManager deckEditManager;
    public GameObject toggleBorder;
    public Tribe tribe;
    public CardType cardType;
    
    public void ToggleFilter() {
        deckEditManager.ToggleTribeFilter(this);
    }
}
