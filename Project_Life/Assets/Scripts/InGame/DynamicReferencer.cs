using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace InGame {
    public class DynamicReferencer : MonoBehaviour {
        public GameManager gameManager;
    
        public int uid;
        public List<int> tokenUids = new();
        public TMP_Text hpText;
        public TMP_Text atkDefText;
        public GameObject highlightSelectable;
        public GameObject highlightSelected;
        public GameObject selectableTargetObj;
        public GameObject attackableObj;
    
        public AttackCapable attackCapable;
    
        public CardDisplay cardDisplay;
        public TokenDisplay tokenDisplay;

        // test
        public GameObject highlightAttacking;
        public GameObject targetLocation;

        public void EnableSelectable() {
            highlightSelectable.SetActive(true);
            selectableTargetObj.SetActive(true);
        }
    
    
        public void EnableAttackable() {
            highlightSelectable.SetActive(true);
            attackableObj.SetActive(true);
        }
    

        public void DisableAllInteractable() {
            if (cardDisplay != null) {
                cardDisplay.DisableInteractableAndHighlights();
                // if in hand
                if (cardDisplay.transform.parent.TryGetComponent(out CardSlot cardSlot)) {
                    cardSlot.isSelectable = false;
                }
            } else if (tokenDisplay != null) {
                tokenDisplay.DisableActivatable();
                highlightSelectable.SetActive(false);
                highlightSelected.SetActive(false);
                selectableTargetObj.SetActive(false);
            } else {
                highlightSelectable.SetActive(false);
                highlightSelected.SetActive(false);
                selectableTargetObj.SetActive(false);
                if(attackableObj != null) attackableObj.SetActive(false);
            }
        }
    }
}
