using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

public class Disabler : MonoBehaviour, IPointerClickHandler {
    public DisablerType disablerType;
    public Disabler targetDisabler;
    public List<KeyCode> keyCodes;
    public List<int> mouseButtons;
    public GameData gameData;
    

    private void OnEnable() {
        // set references that are null
        if(gameData == null) gameData = GameObject.Find("GameData").GetComponent<GameData>();
        if (targetDisabler == null) targetDisabler = this;
        // add it to the panel stack if gameData is set and disabler is default
        if (gameData != null && disablerType == DisablerType.PanelDefault) {
            gameData.panelStack.Add(this);
        }
    }

    void Update() {
        HandleInput();
    }

    private void HandleInput() {
        if (disablerType != DisablerType.PanelDefault) return;
        foreach (int buttonIndex in mouseButtons) {
            if (Input.GetMouseButtonUp(buttonIndex)) {
                Disable();
            }  
        }
        foreach (KeyCode keyCode in keyCodes) {
            if (Input.GetKeyUp(keyCode)) {
                Disable();
            }
        }
    }

    public void Disable() {
        gameData.panelStack.Remove(targetDisabler);
        targetDisabler.gameObject.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData) {
        if (disablerType != DisablerType.SelfClick) return;
        Disable();
    }
}

public enum DisablerType {
    PanelDefault,
    SelfClick
}
