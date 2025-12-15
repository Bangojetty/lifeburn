using System.Collections.Generic;
using InGame;
using UnityEngine;

public class Opponent : Participant {
    // Track opponent's hand cards in order (revealed cards have card data, unrevealed have card == null)
    public List<CardDisplay> handCards = new();

    public override CardDisplay Summon(CardDisplayData cardDisplayData) {
        CardDisplay baseCardDisplay = base.Summon(cardDisplayData);
        baseCardDisplay.ownerIsOpponent = true;
        return baseCardDisplay;
    }

    /// <summary>
    /// Removes a card from opponent's hand tracking.
    /// If uid is provided and matches a revealed card, removes that specific card.
    /// Otherwise removes the first unrevealed card.
    /// Returns the removed CardDisplay, or null if not found.
    /// </summary>
    public CardDisplay RemoveCardFromHand(int? uid = null) {
        CardDisplay cardToRemove = null;

        // If UID provided, try to find revealed card with that UID
        if (uid.HasValue) {
            cardToRemove = handCards.Find(c => c.card != null && c.card.uid == uid.Value);
        }

        // If not found by UID, find first unrevealed card
        if (cardToRemove == null) {
            cardToRemove = handCards.Find(c => c.card == null);
        }

        // If still not found, just take the last card (fallback)
        if (cardToRemove == null && handCards.Count > 0) {
            cardToRemove = handCards[handCards.Count - 1];
        }

        if (cardToRemove != null) {
            handCards.Remove(cardToRemove);
            // Also remove from UidToObj if it was tracked there
            if (cardToRemove.card != null) {
                gameManager.UidToObj.Remove(cardToRemove.card.uid);
            }
        }

        return cardToRemove;
    }
}
