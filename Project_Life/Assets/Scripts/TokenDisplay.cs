using System.Collections;
using System.Collections.Generic;
using InGame;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TokenDisplay : MonoBehaviour, IPointerClickHandler {
    private GameData gameData;
    private GameManager gameManager;

    public Image backgroundImg;

    public TMP_Text tokenAmountText;
    public List<int> tokenUids = new();
    private int tokenAmount;


    public DynamicReferencer dynamicReferencer;
    public GameObject selectableTargetObj;
    public GameObject playableHighlight;

    public bool isActivatable;


    public void Awake() {
        gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
        gameData = GameObject.Find("GameData").GetComponent<GameData>();
        selectableTargetObj.GetComponent<SelectableTarget>().gameManager = gameManager;
    }
    public void Initialize(CardDisplayData newToken) {
        Debug.Assert(newToken.tokenType != null, "tokenType is null for card: " + newToken.name);
        backgroundImg.sprite = gameData.tokenToBack[(TokenType)newToken.tokenType];
        AddToken(newToken.uid);
    }

    private void AddToUidMap(int uid) {
        if (!gameManager.UidToObj.ContainsKey(uid)) {
            Debug.Log($"[UID] Token: Adding token uid={uid} to UidToObj");
            gameManager.UidToObj.Add(uid, gameObject);
        }
        tokenUids.Add(uid);
        dynamicReferencer.tokenUids.Add(uid);
    }

    public void AddToken(int uid) {
        AddToUidMap(uid);
        tokenAmount++;
        tokenAmountText.text = tokenAmount.ToString();
        tokenAmountText.gameObject.SetActive(true);
    }
    
    public void RemoveToken(int uid) {
        tokenAmount--;
        tokenUids.Remove(uid);
        dynamicReferencer.tokenUids.Remove(uid);
        tokenAmountText.text = tokenAmount.ToString();
        gameManager.UidToObj.Remove(uid);
    }

    public void EnableActivatable() {
        isActivatable = true;
        if (playableHighlight != null) {
            playableHighlight.SetActive(true);
        }
    }

    public void DisableActivatable() {
        isActivatable = false;
        if (playableHighlight != null) {
            playableHighlight.SetActive(false);
        }
    }

    public void OnPointerClick(PointerEventData eventData) {
        if (eventData.button == PointerEventData.InputButton.Left) {
            if (!isActivatable || tokenUids.Count == 0) return;
            // Use the first token UID for activation
            gameManager.DisplayTokenActivationVerification(this, tokenUids[0]);
        }
    }
}
