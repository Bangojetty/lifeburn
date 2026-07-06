using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Interactable : MonoBehaviour, IPointerClickHandler {
    public GameManager gameManager;
    public CardDisplay cardDisplay;
    public bool isActivatable;

    public void OnPointerClick(PointerEventData eventData) {
        // Null safety for non-game scenes (draft, deck editor, etc.)
        if (gameManager == null || cardDisplay == null || cardDisplay.card == null) return;

        if (eventData.button == PointerEventData.InputButton.Left) {
            // While choosing attackers, attacking takes precedence over activated abilities -
            // forward the click to the attack-selection handler instead of opening the ability
            if (gameManager.attackCapableUids.Contains(cardDisplay.card.uid)) {
                InGame.AttackCapable attackCapable = cardDisplay.dynamicReferencer != null
                    ? cardDisplay.dynamicReferencer.attackCapable : null;
                if (attackCapable != null) {
                    attackCapable.OnPointerClick(eventData);
                    return;
                }
            }
            // Handle deck top card casting (Sky Scryer)
            if (cardDisplay.isDeckTopCard && cardDisplay.isPlayable) {
                gameManager.DisplayDeckTopCastVerification(cardDisplay);
                return;
            }
            if (!isActivatable) return;
            gameManager.DisplayActivationVerification(cardDisplay);
        } else {
            gameManager.DisplayCardDetails(cardDisplay.card);
        }
    }
}
