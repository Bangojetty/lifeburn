using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InGame {
    public class SelectableCard : MonoBehaviour, IPointerClickHandler {
        public GameObject selectedImgObj;
        public Image selectableImg;
        public GameManager gameManager;
        public CardDisplay cardDisplay;

        public void OnPointerClick(PointerEventData eventData) {
            // Don't handle clicks in ordering mode (arrow buttons handle reordering)
            if (gameManager.cardSelectionManager.isOrderingMode) {
                return;
            }

            ToggleSelected();
            gameManager.cardSelectionManager.CheckSelectionLimit();
        }

        private void ToggleSelected() {
            if (!selectedImgObj.activeSelf) {
                selectedImgObj.SetActive(true);
                gameManager.cardSelectionManager.selectedSelectables.Add(this);
            } else {
                selectedImgObj.SetActive(false);
                gameManager.cardSelectionManager.selectedSelectables.Remove(this);
            }
        }

        public void Deactivate() {
            selectableImg.raycastTarget = false;
            Color color = selectableImg.color;
            color.a = 0.8f;
            selectableImg.color = color;
        }

        public void Activate() {
            selectableImg.raycastTarget = true;
            Color color = selectableImg.color;
            color.a = 0;
            selectableImg.color = color;
        }
    }
}
