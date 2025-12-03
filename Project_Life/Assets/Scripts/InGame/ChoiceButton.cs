using System.Collections;
using System.Collections.Generic;
using InGame;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class ChoiceButton : MonoBehaviour {
    public Button choiceButton;
    private GameManager gameManager;
    
    private void Awake()
    {
        choiceButton.onClick.AddListener(OnClick);
        gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
    }

    private void OnClick() {
        gameManager.SendChoice(transform.GetSiblingIndex());
    }
}
