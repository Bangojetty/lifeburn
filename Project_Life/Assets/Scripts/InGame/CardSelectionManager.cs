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

    // Ordering mode state
    public bool isOrderingMode { get; private set; }

    // Top/Bottom labels for ordering mode (assign in inspector)
    public GameObject topLabel;
    public GameObject bottomLabel;

    // Track cards with enabled arrow buttons for cleanup
    private List<CardDisplay> cardsWithArrowsEnabled = new();

    // build to send to server
    private List<List<int>> selectedUidLists = new();
    
    public void InitializeFirstDestination(GameEvent gEvent) {
        Debug.Assert(gEvent.cardSelectionDatas != null, "there are no CardSelectionDatas for this LookAtDeck effect");
        currentSelectionData = gEvent.cardSelectionDatas[0];
        selectionMessageText.text = currentSelectionData.selectionMessage;
        currentEvent = gEvent;
        isPeekMode = false;

        // If selectionMax < 1 (all cards go to this destination) and ordering is required,
        // skip selection and go straight to ordering mode
        if (currentSelectionData.selectionMax < 1 && currentSelectionData.selectOrder && cDisplaysToSelectFrom.Count > 1) {
            EnterOrderingModeForRemaining();
            return;
        }

        SetAllUnselectable();
        SetSelectables();
    }

    private bool isPeekMode;

    public void InitializePeek() {
        isPeekMode = true;
        selectionMessageText.text = "Top of deck:";
        confirmButton.interactable = true;
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
        // Set initial confirm button state based on selection requirements
        confirmButton.interactable = selectedSelectables.Count >= currentSelectionData.selectionMin;
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
        // Peek mode - just close the dialogue without sending selection data
        if (isPeekMode) {
            ResetManager();
            gameManager.gEventIsInProgress = false;
            return;
        }

        Debug.Assert(currentEvent.cardSelectionDatas != null, "there is no list of selectionDatas to select from");

        // If we're in ordering mode, submit the ordered cards
        if (isOrderingMode) {
            SubmitOrderedSelection();
            return;
        }

        // Check if we need to enter ordering mode for this selection
        if (currentSelectionData.selectOrder && selectedSelectables.Count > 1) {
            EnterOrderingMode();
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
            // Check if we need ordering for "the rest"
            if (currentSelectionData.selectOrder && cDisplaysToSelectFrom.Count > 1) {
                EnterOrderingModeForRemaining();
                return;
            }
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

    private void EnterOrderingMode() {
        isOrderingMode = true;
        selectionMessageText.text = "Use arrows to reorder";
        confirmButton.interactable = true;  // Can confirm at any time since order is visual

        // Show Top/Bottom labels
        if (topLabel != null) topLabel.SetActive(true);
        if (bottomLabel != null) bottomLabel.SetActive(true);

        // Remove non-selected cards and enable arrow buttons on selected ones
        List<CardDisplay> toRemove = new();
        foreach (CardDisplay cDisplay in cDisplaysToSelectFrom) {
            SelectableCard card = cDisplay.selectableCardObj.GetComponent<SelectableCard>();
            if (!selectedSelectables.Contains(card)) {
                toRemove.Add(cDisplay);
            } else {
                // Deselect visually and enable arrow buttons
                card.selectedImgObj.SetActive(false);
                EnableArrowButtons(cDisplay);
            }
        }
        foreach (CardDisplay cDisplay in toRemove) {
            cDisplaysToSelectFrom.Remove(cDisplay);
            Destroy(cDisplay.gameObject);
        }
    }

    private void EnterOrderingModeForRemaining() {
        isOrderingMode = true;
        selectionMessageText.text = "Use arrows to reorder";
        confirmButton.interactable = true;  // Can confirm at any time since order is visual

        // Show Top/Bottom labels
        if (topLabel != null) topLabel.SetActive(true);
        if (bottomLabel != null) bottomLabel.SetActive(true);

        // Enable arrow buttons on all remaining cards
        foreach (CardDisplay cDisplay in cDisplaysToSelectFrom) {
            EnableArrowButtons(cDisplay);
        }
    }

    private void EnableArrowButtons(CardDisplay cardDisplay) {
        // Enable the container
        if (cardDisplay.orderingArrowsContainer != null) {
            cardDisplay.orderingArrowsContainer.SetActive(true);
        }

        // Add listeners to buttons
        if (cardDisplay.leftArrowButton != null) {
            cardDisplay.leftArrowButton.onClick.RemoveAllListeners();
            cardDisplay.leftArrowButton.onClick.AddListener(() => MoveCardLeft(cardDisplay));
        }

        if (cardDisplay.rightArrowButton != null) {
            cardDisplay.rightArrowButton.onClick.RemoveAllListeners();
            cardDisplay.rightArrowButton.onClick.AddListener(() => MoveCardRight(cardDisplay));
        }

        cardsWithArrowsEnabled.Add(cardDisplay);
    }

    private void DisableArrowButtons(CardDisplay cardDisplay) {
        // Remove listeners
        if (cardDisplay.leftArrowButton != null) {
            cardDisplay.leftArrowButton.onClick.RemoveAllListeners();
        }

        if (cardDisplay.rightArrowButton != null) {
            cardDisplay.rightArrowButton.onClick.RemoveAllListeners();
        }

        // Disable the container
        if (cardDisplay.orderingArrowsContainer != null) {
            cardDisplay.orderingArrowsContainer.SetActive(false);
        }
    }

    public void MoveCardLeft(CardDisplay cardDisplay) {
        int currentIndex = cardDisplay.transform.GetSiblingIndex();
        if (currentIndex > 0) {
            cardDisplay.transform.SetSiblingIndex(currentIndex - 1);
        }
    }

    public void MoveCardRight(CardDisplay cardDisplay) {
        int currentIndex = cardDisplay.transform.GetSiblingIndex();
        int maxIndex = cardDisplay.transform.parent.childCount - 1;
        if (currentIndex < maxIndex) {
            cardDisplay.transform.SetSiblingIndex(currentIndex + 1);
        }
    }

    private void DisableAllArrowButtons() {
        foreach (CardDisplay cardDisplay in cardsWithArrowsEnabled) {
            if (cardDisplay != null) {
                DisableArrowButtons(cardDisplay);
            }
        }
        cardsWithArrowsEnabled.Clear();
    }

    private void SubmitOrderedSelection() {
        // Disable arrow buttons first
        DisableAllArrowButtons();

        // Get the order from the visual positions of the cards (left to right = top to bottom of deck)
        List<CardDisplay> orderedDisplays = cDisplaysToSelectFrom
            .OrderBy(cd => cd.transform.GetSiblingIndex())
            .ToList();

        List<int> orderedUids = new();
        foreach (CardDisplay cDisplay in orderedDisplays) {
            orderedUids.Add(cDisplay.card.uid);
            Destroy(cDisplay.gameObject);
        }
        selectedUidLists.Add(orderedUids);
        cDisplaysToSelectFrom.Clear();

        // Hide Top/Bottom labels and exit ordering mode
        if (topLabel != null) topLabel.SetActive(false);
        if (bottomLabel != null) bottomLabel.SetActive(false);
        isOrderingMode = false;
        selectedSelectables.Clear();

        // Move to next destination or finish
        currentIndex++;
        if (currentIndex >= currentEvent.cardSelectionDatas.Count) {
            FinalSubmit();
            return;
        }

        currentSelectionData = currentEvent.cardSelectionDatas[currentIndex];

        // Handle "the rest" destination
        if (currentSelectionData.selectionMax < 1) {
            if (currentSelectionData.selectOrder && cDisplaysToSelectFrom.Count > 1) {
                EnterOrderingModeForRemaining();
                return;
            }
            List<int> tempUidList = new();
            foreach (CardDisplay cDisplay in cDisplaysToSelectFrom) {
                tempUidList.Add(cDisplay.card.uid);
            }
            selectedUidLists.Add(tempUidList);
            FinalSubmit();
            return;
        }

        selectionMessageText.text = currentSelectionData.selectionMessage;
        SetSelectables();
    }
    
    

    private void ResetManager() {
        currentIndex = 0;
        cDisplaysToSelectFrom.Clear();
        selectedSelectables.Clear();
        currentSelectionData = null;
        selectedUidLists.Clear();
        isPeekMode = false;
        isOrderingMode = false;
        DisableAllArrowButtons();
        if (topLabel != null) topLabel.SetActive(false);
        if (bottomLabel != null) bottomLabel.SetActive(false);
        foreach (Transform child in gameManager.cardSelectionView.transform) {
            Destroy(child.gameObject);
        }
        gameObject.SetActive(false);
    }
}
