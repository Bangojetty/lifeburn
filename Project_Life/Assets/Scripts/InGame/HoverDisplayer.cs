using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class HoverDisplayer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
    private GameManager gameManager;
    public CardDisplay cardDisplay;
    

    private void Start() {
        gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
    }


    public void OnPointerEnter(PointerEventData eventData) {
        if (cardDisplay.card == null) return;
        gameManager.mouseCardDisplayContainer.SetActive(true);
        gameManager.mouseCardDisplay.UpdateCardDisplayData(cardDisplay.card);
    }

    public void OnPointerExit(PointerEventData eventData) {
        gameManager.mouseCardDisplayContainer.SetActive(false);
    }
}
