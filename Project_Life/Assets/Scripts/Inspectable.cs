using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Inspectable : MonoBehaviour, IPointerClickHandler {
    public InspectableType type;
    public GameObject contentsContainer;
    public GameManager gameManager;

    public void Start() {
        gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
    }
    
    // private void Update() {
    //     Debug.Log("id: " + contentsContainer.GetInstanceID());
    // }
    
    public void OnPointerClick(PointerEventData eventData) {
        if (eventData.button == PointerEventData.InputButton.Left) {
            switch (type) {
                case InspectableType.Graveyard:
                    gameManager.cardGroupTitleText.text = "Graveyard";
                    gameManager.DisplayCardGroup(contentsContainer);
                    break;
            }
        }
    }
}

public enum InspectableType {
    Graveyard
}
