using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DetailPanel : MonoBehaviour {
    DeckEditManager deckEditManager;

    public TMP_Text cardCopiesAmountText;


    public void SetCardCopies(int amount) {
        cardCopiesAmountText.text = amount.ToString();
    }

    
}
