using UnityEngine;
using UnityEngine.EventSystems;

namespace InGame {
    public class AttackCapable : MonoBehaviour, IPointerClickHandler {
        public GameManager gameManager;
        public GameObject baseObj;
        public CardDisplay cardDisplay;
        public bool isSelected;
        public void OnPointerClick(PointerEventData eventData) {
            if (!isSelected) {
                Select();
            } else {
                // This checks to see if it's already set with an attacked object and an attack arrow (not the cursor arrow)
                if (gameManager.attackUids.ContainsKey(cardDisplay.card.uid)) {
                    UnAssignAttackable();
                } else {
                    // otherwise simply deselect it (UnassignAttackable does this in gameManager.UnAssignAttack)
                    Deselect();
                }
            }
        }

        private void Select() {
            gameManager.SelectAttackCapable(this);
            // toggle highlights
            cardDisplay.playableHighlight.SetActive(false);
            cardDisplay.selectedHighlight.SetActive(true);
            // assign
            isSelected = true;
        }

        public void Deselect() {
            gameManager.DeselectAttackCapable();
            // toggle highlights
            cardDisplay.selectedHighlight.SetActive(false);
            cardDisplay.attackingHighlight.SetActive(false);
            cardDisplay.playableHighlight.SetActive(true);
            // unassign
            isSelected = false;
        }

        private void UnAssignAttackable() {
            gameManager.UnAssignAttack(this);
        }
    }
}
