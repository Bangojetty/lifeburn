using UnityEngine;
using UnityEngine.EventSystems;

namespace InGame {
    public class Attackable : MonoBehaviour, IPointerClickHandler {
        public GameManager gameManager;
        public DynamicReferencer dynamicReferencer;
        public GameObject baseObj;
        public CardDisplay cardDisplay;
        public GameObject targetLocationObj;
        public GameObject highlight;
        
        public void OnPointerClick(PointerEventData eventData) {
            gameManager.AssignAttack(this);
        }
    }
}
    