using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;

public class MenuDeckDisplay : MonoBehaviour, ISelectHandler, IDeselectHandler {
    public DeckData deckData;
    private GameData gameData;
    private MenuManager menuManager;
        
    private void Start() {
        while (gameData == null) {
            gameData = GameObject.Find("GameData").GetComponent<GameData>();
        }
        menuManager = GameObject.Find("MenuManager").GetComponent<MenuManager>();
        menuManager.deckObjs.Add(gameObject);

        // If this display is the deck that's already selected in memory (e.g. re-entering
        // the screen), re-select it so the UI reflects the actual current deck
        if (gameObject.activeInHierarchy && gameData.currentDeck != null && deckData != null &&
            deckData.id == gameData.currentDeck.id) {
            EventSystem.current.SetSelectedGameObject(gameObject);
        }
    }

    public void OnSelect(BaseEventData eventData) {
        Debug.Log("selected deck: " + eventData.selectedObject);
        menuManager.editBtn.interactable = true;
        menuManager.delBtn.interactable = true;
        menuManager.deckSelectPlayBtn.interactable = true;
        gameData.currentDeck = deckData;
        menuManager.selectedDeckObj = gameObject;
    }

    public void OnDeselect(BaseEventData eventData) {
        // TODO update the deck select UI to a more user-friendly interface (e.g. deckdisplays are buttons that open
        // TODO a separate panel with deck specific actions [delete, edit, rename, change icon, etc.])
    }
}
