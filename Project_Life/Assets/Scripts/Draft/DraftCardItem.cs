using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Add-on component for CardDisplay that enables draft selection behavior.
/// Attach this to the same GameObject as CardDisplay when using in draft mode.
/// </summary>
public class DraftCardItem : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler {
    private CardDisplay cardDisplay;
    private DraftManager draftManager;
    private int cardId;
    private bool isSelected;
    private bool isHovering;

    public int CardId => cardId;
    public bool IsSelected => isSelected;

    public void Setup(int cardId, DraftManager manager, GameData gameData) {
        this.cardId = cardId;
        this.draftManager = manager;

        // Get or find CardDisplay on same object
        cardDisplay = GetComponent<CardDisplay>();
        if (cardDisplay == null) {
            cardDisplay = GetComponentInChildren<CardDisplay>();
        }

        // Disable in-game interaction components that would interfere with draft selection
        if (cardDisplay != null) {
            // Explicitly set gameData reference (in case Awake didn't set it properly)
            cardDisplay.gameData = gameData;
            DisableInGameComponents();
        }

        // Setup card display (don't track in UidToObj since this is draft, not in-game)
        if (cardDisplay != null && gameData.allCardsDict.TryGetValue(cardId, out var cardData)) {
            cardDisplay.UpdateCardDisplayData(cardData, trackInUidToObj: false);
        } else {
            Debug.LogWarning($"[DraftCardItem] Could not find card data for cardId: {cardId}");
        }

        // Ensure the background image can receive raycasts
        if (cardDisplay != null && cardDisplay.backgroundImg != null) {
            cardDisplay.backgroundImg.raycastTarget = true;
        }
    }

    private void DisableInGameComponents() {
        // Disable all in-game interaction objects that might intercept clicks
        if (cardDisplay.interactableObj != null) cardDisplay.interactableObj.SetActive(false);
        if (cardDisplay.selectableCardObj != null) cardDisplay.selectableCardObj.SetActive(false);
        if (cardDisplay.attackCapableObj != null) cardDisplay.attackCapableObj.SetActive(false);
        if (cardDisplay.attackableObj != null) cardDisplay.attackableObj.SetActive(false);
        if (cardDisplay.selectableTargetObj != null) cardDisplay.selectableTargetObj.SetActive(false);
        if (cardDisplay.targetingLocationObj != null) cardDisplay.targetingLocationObj.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData) {
        isHovering = true;
        UpdateHighlight();
    }

    public void OnPointerExit(PointerEventData eventData) {
        isHovering = false;
        UpdateHighlight();
    }

    public void OnPointerClick(PointerEventData eventData) {
        if (eventData.button == PointerEventData.InputButton.Left) {
            // Left click selects the card
            draftManager?.SelectCard(this);
        } else if (eventData.button == PointerEventData.InputButton.Right) {
            // Right click shows card details
            draftManager?.ShowCardDetails(cardDisplay?.card);
        }
    }

    public void SetSelected(bool selected) {
        isSelected = selected;
        UpdateHighlight();
    }

    private void UpdateHighlight() {
        if (cardDisplay == null) return;

        // Selected state takes priority - use selectedHighlight (stays on)
        if (isSelected) {
            if (cardDisplay.selectedHighlight != null) {
                cardDisplay.selectedHighlight.SetActive(true);
            }
            // Hide hover highlight when selected
            if (cardDisplay.targetableHighlight != null) {
                cardDisplay.targetableHighlight.SetActive(false);
            }
        }
        // Hover state - use targetableHighlight (temporary)
        else if (isHovering) {
            if (cardDisplay.targetableHighlight != null) {
                cardDisplay.targetableHighlight.SetActive(true);
            }
            if (cardDisplay.selectedHighlight != null) {
                cardDisplay.selectedHighlight.SetActive(false);
            }
        }
        // Neither selected nor hovering - hide both
        else {
            if (cardDisplay.selectedHighlight != null) {
                cardDisplay.selectedHighlight.SetActive(false);
            }
            if (cardDisplay.targetableHighlight != null) {
                cardDisplay.targetableHighlight.SetActive(false);
            }
        }
    }
}
