using System;
using System.Collections.Generic;
using System.Linq;
using InGame;
using InGame.Other_Enums;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.Android;
using UnityEngine.Experimental.Audio;
using UnityEngine.Serialization;
using UnityEngine.UIElements;
using Button = UnityEngine.UI.Button;
using Random = UnityEngine.Random;

public class CardSelectionManager : MonoBehaviour {
    public GameManager gameManager;
    public List<CardDisplay> cDisplaysToSelectFrom = new();
    public List<SelectableCard> selectedSelectables = new();
    public TMP_Text selectionMessageText;
    public Button confirmButton;
    private CardSelectionData currentSelectionData;
    private int currentIndex;
    private GameEvent currentEvent;

    
    // build to send to server
    private List<List<int>> selectedUidLists = new();
    
    public void InitializeFirstDestination(GameEvent gEvent) {
        Debug.Assert(gEvent.cardSelectionDatas != null, "there are no CardSelectionDatas for this LookAtDeck effect");
        currentSelectionData = gEvent.cardSelectionDatas[0];
        selectionMessageText.text = currentSelectionData.selectionMessage;
        currentEvent = gEvent;
        SetAllUnselectable();
        SetSelectables();
    }
    
    private void SetAllUnselectable() {
        foreach (CardDisplay cDisplay in cDisplaysToSelectFrom) {
            cDisplay.selectableCardObj.GetComponent<SelectableCard>().Deactivate();
        }
    }

    private void SetSelectables() {
        // there are no cards to select, allow the player to confirm without selecting a card
        if (currentSelectionData.selectableUids.Count == 0) {
            confirmButton.interactable = true;
            return;
        }
        // activate the possible selections
        var cardsToActivate = cDisplaysToSelectFrom.Where(cDisplay => 
            currentSelectionData.selectableUids.Contains(cDisplay.card.uid)).ToList();
        foreach (CardDisplay cDisplay in cardsToActivate) {
            cDisplay.selectableCardObj.GetComponent<SelectableCard>().Activate();
        }
    }

    public void CheckSelectionLimit() {
        // if you haven't selected enough, disable the confirm button
        confirmButton.interactable = selectedSelectables.Count >= currentSelectionData.selectionMin;
        // if you've selected the max amount, disable all selectables
        if (selectedSelectables.Count == currentSelectionData.selectionMax) {
            foreach (CardDisplay cDisplay in cDisplaysToSelectFrom) {
                SelectableCard selectableCard = cDisplay.selectableCardObj.GetComponent<SelectableCard>();
                // if you've already selected it, continue
                if (selectedSelectables.Contains(selectableCard)) continue;
                // otherwise, deactivate
                selectableCard.Deactivate();
            }
        } else {
            // if you haven't selected the max, activate the possible selectables
            SetSelectables();
        }
    }

    public void SubmitSelection() {
        Debug.Assert(currentEvent.cardSelectionDatas != null, "there is no list of selectionDatas to select from");
        // wait for order selection from player
        if (currentSelectionData.selectOrder) {
            // TODO: select order of selected cards (this might require a unique ui selection system to display 
            // TODO positional data in a stack of cards (like the deck or graveyard)
            return;
        }
        // adds the selected uids to a list, destroys the selected GameObjects, and adds the created list to the final
        // list of lists (this is sent to the server on the final iteration)
        ApplySelection();
        
        // iterate and deal with final iteration logic
        currentIndex++;
        if (currentIndex >= currentEvent.cardSelectionDatas.Count) {
            FinalSubmit();
            return;
        }
        // iterate to the next SelectionData
        currentSelectionData = currentEvent.cardSelectionDatas[currentIndex];
        // clear the selected cards list
        selectedSelectables.Clear();
        // if there is no max amount, it means this destination is automatic -> add the rest and send it
        if (currentSelectionData.selectionMax < 1) {
            List<int> tempUidList = new();
            foreach (CardDisplay cDisplay in cDisplaysToSelectFrom) {
                tempUidList.Add(cDisplay.card.uid);
            }
            selectedUidLists.Add(tempUidList);    
            FinalSubmit();
            return;
        }
        // set up and activate the next CardSelectionData
        selectionMessageText.text = currentSelectionData.selectionMessage;
        SetSelectables();
    }

    private void FinalSubmit() {
        gameManager.SendCardSelection(selectedUidLists);
        ResetManager();
    }

    private void ApplySelection() {
        // create uid list from selection
        List<int> selectedUids = new();
        foreach (SelectableCard selectedCard in selectedSelectables) {
            selectedUids.Add(selectedCard.cardDisplay.card.uid);
            cDisplaysToSelectFrom.Remove(selectedCard.cardDisplay);
            Destroy(selectedCard.cardDisplay.gameObject);
        }
        // add to final list of selection lists
        selectedUidLists.Add(selectedUids);
    }

    public void DisplaySelectionOrderingPanel() {
        // TODO create panel and code logic
    }
    
    

    private void ResetManager() {
        currentIndex = 0;
        cDisplaysToSelectFrom.Clear();
        selectedSelectables.Clear();
        currentSelectionData = null;
        selectedUidLists.Clear();
        foreach (Transform child in gameManager.cardSelectionView.transform) {
            Destroy(child.gameObject);
        }
        gameObject.SetActive(false);
    }
}
