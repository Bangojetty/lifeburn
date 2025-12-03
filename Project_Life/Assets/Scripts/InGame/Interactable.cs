using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Interactable : MonoBehaviour, IPointerClickHandler {
    public GameManager gameManager;
    public CardDisplay cardDisplay;
    public bool isActivatable;
    
    public void OnPointerClick(PointerEventData eventData) {
        if (cardDisplay.card == null) return;
        if (eventData.button == PointerEventData.InputButton.Left) {
            if (!isActivatable) return;
            gameManager.DisplayActivationVerification(cardDisplay);
        } else {
            gameManager.DisplayCardDetails(cardDisplay.card);
        }
    }
}
