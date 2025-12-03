using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace InGame {
    public class SelectableTarget : MonoBehaviour, IPointerClickHandler {
        public GameManager gameManager;
        public DynamicReferencer dRef;

        public void OnPointerClick(PointerEventData eventData) {
            if (dRef.tokenUids.Count > 0) {
                if (gameManager.dRefToSelectedUids.ContainsKey(dRef)) {
                    DeselectMultiple();
                } else {
                    gameManager.DisplayAmountSelector(SelectMultiple);
                    return;
                }
            } else {
                // deselect
                if (gameManager.selectedUids.Contains(dRef.uid)) {
                    Deselect();
                }
                // select
                else {
                    Select();
                }
            }
            CheckMaxAmount();
        }
    
        private void Select() {
            dRef.highlightSelectable.SetActive(false);
            dRef.highlightSelected.SetActive(true);
            gameManager.selectedUids.Add(dRef.uid);
        }

        private void Deselect() {
            gameManager.selectedUids.Remove(dRef.uid);
            gameManager.EnableUnselectedSelectables();
            gameManager.actionButtonComponent.interactable = false;
            gameManager.actionButton.SetButtonType(ActionButtonType.Pass);
        }

        public void SelectMultiple(int amount) {
            dRef.highlightSelectable.SetActive(false);
            dRef.highlightSelected.SetActive(true);
            List<int> selectedUids = new();
            for (int i = 0; i < amount; i++) {
                selectedUids.Add(dRef.tokenUids[i]);
            }
            gameManager.selectedUids.AddRange(selectedUids);
            gameManager.dRefToSelectedUids.Add(dRef, selectedUids);
            CheckMaxAmount();
        }

        private void DeselectMultiple() {
            List<int> selectedUids = gameManager.dRefToSelectedUids[dRef];
            foreach (int uid in selectedUids) {
                gameManager.selectedUids.Remove(uid);
            }
            gameManager.dRefToSelectedUids.Remove(dRef);
            gameManager.EnableUnselectedSelectables();
            gameManager.actionButton.SetButtonType(ActionButtonType.Pass);
        }

        private void CheckMaxAmount() {
            // disable selecting if at max tribute (you've selected enough for the card)
            if (gameManager.selectedUids.Count < gameManager.currentSelectionMax) return;
            gameManager.DisableUnselectedSelectables();
            gameManager.actionButtonComponent.interactable = true;
            gameManager.actionButton.SetButtonType(gameManager.currentSelectionType);
        }
    }
}
