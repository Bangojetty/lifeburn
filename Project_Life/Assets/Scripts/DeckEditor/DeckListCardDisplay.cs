using System;
using System.Collections;
using System.Collections.Generic;
using DeckEditor;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class DeckListCardDisplay : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler {
    public CardDisplayData card;
    public Image backgroundType;
    public new TMP_Text name;
    public TMP_Text cost;
    public DeckEditManager deckEditManager;

    private void Start() {
        deckEditManager = GameObject.Find("DeckEditManager").GetComponent<DeckEditManager>();
    }

    public void OnPointerClick(PointerEventData eventData) {
        if (SceneManager.GetActiveScene().name != "Deck Editor") return;
        deckEditManager.RemoveCard(card);
        deckEditManager.collectionCardsToObj[card.id].GetComponent<DeckEditorCardDisplay>().AddCopy();
        deckEditManager.mouseCardDisplayContainer.SetActive(false);
        Destroy(gameObject);
    }

    public void OnPointerEnter(PointerEventData eventData) {
        deckEditManager.mouseCardDisplay.gameData = deckEditManager.gameData;
        deckEditManager.mouseCardDisplay.UpdateCardDisplayData(card);
        deckEditManager.mouseCardDisplayContainer.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData) {
        deckEditManager.mouseCardDisplayContainer.SetActive(false);
    }
}
