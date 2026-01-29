using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Simple click handler for cards in the drafted cards panel.
/// Only handles right-click to show card details.
/// </summary>
public class DraftedCardClickHandler : MonoBehaviour, IPointerClickHandler {
    private CardDisplayData cardData;
    private DraftManager draftManager;

    public void Setup(CardDisplayData cardData, DraftManager manager) {
        this.cardData = cardData;
        this.draftManager = manager;
    }

    public void OnPointerClick(PointerEventData eventData) {
        if (eventData.button == PointerEventData.InputButton.Right) {
            draftManager?.ShowCardDetails(cardData);
        }
    }
}
