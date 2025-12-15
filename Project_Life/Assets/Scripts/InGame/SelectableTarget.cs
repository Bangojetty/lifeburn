using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace InGame {
    public class SelectableTarget : MonoBehaviour, IPointerClickHandler {
        public GameManager gameManager;
        public DynamicReferencer dRef;

        public void OnPointerClick(PointerEventData eventData) {
            // Right-click should inspect, not select
            if (eventData.button == PointerEventData.InputButton.Right) return;

            if (dRef.tokenUids.Count > 0) {
                if (gameManager.dRefToSelectedUids.ContainsKey(dRef)) {
                    DeselectMultiple();
                } else {
                    // Calculate max tokens to select from this stack
                    int? maxOverride = null;
                    // Use currentTargetMax for target selection, currentSelectionMax for other types
                    int selectionMax = gameManager.currentSelectionType == ActionButtonType.Target
                        ? gameManager.currentTargetMax
                        : gameManager.currentSelectionMax;

                    if (gameManager.currentSelectionType == ActionButtonType.Tribute && dRef.tokenUids.Count > 0) {
                        // For tribute: calculate based on tribute values
                        int tributeValuePerToken = gameManager.GetTributeValue(dRef.tokenUids[0]);
                        int remainingTributeNeeded = gameManager.GetRemainingTributeNeeded();
                        // Max tokens = remaining tribute needed / tribute value per token
                        int maxByTribute = remainingTributeNeeded / tributeValuePerToken;
                        // Also limit by available tokens
                        maxOverride = Mathf.Min(maxByTribute, dRef.tokenUids.Count);
                    } else {
                        // For other selection types (Sacrifice, Target, etc.): calculate based on count
                        int remainingNeeded = selectionMax - gameManager.selectedUids.Count;
                        // Limit by both remaining needed and available tokens in this stack
                        maxOverride = Mathf.Min(remainingNeeded, dRef.tokenUids.Count);
                    }

                    // If selection max is 1 or only 1 token can be selected, auto-select without amount selector
                    int effectiveMax = maxOverride ?? dRef.tokenUids.Count;
                    if (selectionMax == 1 || effectiveMax == 1) {
                        SelectMultiple(1);
                        return;
                    }
                    gameManager.DisplayAmountSelector(SelectMultiple, null, maxOverride);
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
            // Track tribute value if in tribute selection mode
            if (gameManager.currentSelectionType == ActionButtonType.Tribute) {
                gameManager.currentTributeValue += gameManager.GetTributeValue(dRef.uid);
            }
        }

        private void Deselect() {
            gameManager.selectedUids.Remove(dRef.uid);
            dRef.highlightSelected.SetActive(false);
            // Track tribute value if in tribute selection mode
            if (gameManager.currentSelectionType == ActionButtonType.Tribute) {
                gameManager.currentTributeValue -= gameManager.GetTributeValue(dRef.uid);
            }
            gameManager.EnableUnselectedSelectables();
            // For variable selection, button stays enabled (can confirm with any amount)
            if (!gameManager.variableSelection) {
                gameManager.actionButtonComponent.interactable = false;
                gameManager.actionButton.SetButtonType(ActionButtonType.Pass);
            }
        }

        public void SelectMultiple(int amount) {
            dRef.highlightSelectable.SetActive(false);
            dRef.highlightSelected.SetActive(true);
            List<int> selectedUids = new();
            for (int i = 0; i < amount; i++) {
                selectedUids.Add(dRef.tokenUids[i]);
                // Track tribute value if in tribute selection mode
                if (gameManager.currentSelectionType == ActionButtonType.Tribute) {
                    gameManager.currentTributeValue += gameManager.GetTributeValue(dRef.tokenUids[i]);
                }
            }
            gameManager.selectedUids.AddRange(selectedUids);
            gameManager.dRefToSelectedUids.Add(dRef, selectedUids);
            CheckMaxAmount();
        }

        private void DeselectMultiple() {
            List<int> selectedUids = gameManager.dRefToSelectedUids[dRef];
            foreach (int uid in selectedUids) {
                gameManager.selectedUids.Remove(uid);
                // Track tribute value if in tribute selection mode
                if (gameManager.currentSelectionType == ActionButtonType.Tribute) {
                    gameManager.currentTributeValue -= gameManager.GetTributeValue(uid);
                }
            }
            gameManager.dRefToSelectedUids.Remove(dRef);
            dRef.highlightSelected.SetActive(false);
            gameManager.EnableUnselectedSelectables();
            // For variable selection, button stays enabled (can confirm with any amount)
            if (!gameManager.variableSelection) {
                gameManager.actionButtonComponent.interactable = false;
                gameManager.actionButton.SetButtonType(ActionButtonType.Pass);
            }
        }

        private void CheckMaxAmount() {
            // Use currentTargetMax for target selection, currentSelectionMax for other types
            int selectionMax = gameManager.currentSelectionType == ActionButtonType.Target
                ? gameManager.currentTargetMax
                : gameManager.currentSelectionMax;

            // For variable selection, button is always enabled, just disable unselected at max
            if (gameManager.variableSelection) {
                if (gameManager.selectedUids.Count >= selectionMax) {
                    gameManager.DisableUnselectedSelectables();
                }
                // Button already enabled for variable selection
                return;
            }
            // For tribute selection, check tribute VALUE, not count
            if (gameManager.currentSelectionType == ActionButtonType.Tribute) {
                if (gameManager.currentTributeValue < selectionMax) return;
            } else {
                // For other selection types, check count
                if (gameManager.selectedUids.Count < selectionMax) return;
            }
            gameManager.DisableUnselectedSelectables();
            gameManager.actionButtonComponent.interactable = true;
            gameManager.actionButton.SetButtonType(gameManager.currentSelectionType);
        }
    }
}
