using System.Collections;
using System.Collections.Generic;
using InGame;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Utilities;

public class CardDisplaySimple : MonoBehaviour, IPointerClickHandler
{
    public GameData gameData;
    public GameManager gameManager;
    public CardDisplayData card;

    // Selection
    public int uid;
    public bool isSelectable;
    public bool isSelected;
    public GameObject highlightSelectable;
    public GameObject highlightSelected;

    public GameObject cardInfo;
    public TMP_Text nameText;
    public TMP_Text cardTypeText;
    public TMP_Text descriptionText;
    public TMP_Text costText;
    public GameObject atkDef;
    public TMP_Text atkDefText;
    public GameObject keywordsObj;
    public GameObject keywordsPfb;
    public GameObject objectIcon;

    public Image artworkImg;
    public Image backgroundImg;


    void Awake() {
        gameData = GameObject.Find("GameData").GetComponent<GameData>();
        GameObject gmObj = GameObject.Find("GameManager");
        if (gmObj != null) {
            gameManager = gmObj.GetComponent<GameManager>();
        }
    }
    

    public void UpdateCardDisplayData(CardDisplayData newCard = null) {
        if (newCard != null) {
            card = newCard;
            uid = newCard.uid;
        }
        DisplayCardData();
        if (card == null) return;
        SetType();
    }

    private void DisplayCardData() {
        if (card == null) {
            backgroundImg.sprite = gameData.faceDownTemplate;
            cardInfo.SetActive(false);
            return;
        }
        Debug.Log($"CardInfo: {cardInfo}", this);
        cardInfo.SetActive(true);
        nameText.text = card.name;
        // set CardTypeText depending on type
        cardTypeText.text = card.type switch {
            CardType.Summon => card.type + " - " + card.tribe,
            _ => card.type.ToString()
        };
        string tempDescription = Utils.GetStringWithChosenText(card.description);
        // Add additional description (granted passives) in cyan
        if (!string.IsNullOrEmpty(card.additionalDescription)) {
            tempDescription += "\n" + Utils.colorCyan + card.additionalDescription + "</color>";
        }
        descriptionText.text = tempDescription;
        // set cost text (set it to X if it's an X cost card) 
        costText.text = card.hasXCost ? "X" : card.cost.ToString();
        if (card.type != CardType.Summon) {
            atkDef.SetActive(false);
        } else {
            atkDef.SetActive(true);
            atkDefText.text = (card.attack + "/" + card.defense);
        }
        // remove old keywords
        foreach (Transform child in keywordsObj.transform) {
            Destroy(child.gameObject);
        }
        // re-add current keywords
        if (card.keywords != null) {
            foreach (Keyword keyword in card.keywords) {
                GameObject newKeyword = Instantiate(keywordsPfb, keywordsObj.transform);
                newKeyword.GetComponent<Image>().sprite = gameData.keywordImgDict[keyword];
            }
        }
        // Tokens have negative IDs - use tokenArtById, regular cards use allArtworks
        if (card.id >= 0) {
            artworkImg.sprite = card.id < gameData.allArtworks.Count ? gameData.allArtworks[card.id] : null;
        } else {
            artworkImg.sprite = gameData.tokenArtById.ContainsKey(card.id) ? gameData.tokenArtById[card.id] : null;
        }
    }
    
    private void SetType() {
        // this should probably be moved to a "resetCardDisplay" function that resets all the optional stats.
        objectIcon.SetActive(false);
        switch (card.type) {
            case CardType.Spell:
                backgroundImg.sprite = gameData.spellToColor[card.tribe];
                atkDef.SetActive(false);
                break;
            case CardType.Summon:
                backgroundImg.sprite = gameData.creatureToColor[card.tribe];
                break;
            case CardType.Object:
                objectIcon.SetActive(true);
                backgroundImg.sprite = gameData.spellToColor[card.tribe];
                break;
            case CardType.Token:
                // Tokens use their tribe's color scheme
                if (gameData.creatureToColor.ContainsKey(card.tribe)) {
                    backgroundImg.sprite = gameData.creatureToColor[card.tribe];
                }
                atkDef.SetActive(false);
                break;
            default:
                Debug.Log("No CardType");
                break;
        }
    }

    public void EnableSelectable() {
        isSelectable = true;
        if (highlightSelectable != null) {
            highlightSelectable.SetActive(true);
        }
    }

    public void DisableSelectable() {
        isSelectable = false;
        isSelected = false;
        if (highlightSelectable != null) {
            highlightSelectable.SetActive(false);
        }
        if (highlightSelected != null) {
            highlightSelected.SetActive(false);
        }
    }

    public void OnPointerClick(PointerEventData eventData) {
        // Right-click should inspect, not select
        if (eventData.button == PointerEventData.InputButton.Right) {
            if (gameManager != null && card != null) {
                gameManager.DisplayCardDetails(card);
            }
            return;
        }

        if (!isSelectable || gameManager == null) return;

        if (isSelected) {
            Deselect();
        } else {
            Select();
        }
        CheckMaxAmount();
    }

    private void Select() {
        isSelected = true;
        if (highlightSelectable != null) highlightSelectable.SetActive(false);
        if (highlightSelected != null) highlightSelected.SetActive(true);
        gameManager.selectedUids.Add(uid);
    }

    private void Deselect() {
        isSelected = false;
        gameManager.selectedUids.Remove(uid);
        if (highlightSelected != null) highlightSelected.SetActive(false);
        if (highlightSelectable != null) highlightSelectable.SetActive(true);
        // Re-enable other selectables if they were disabled
        gameManager.EnableUnselectedSelectablesInCardGroup();
        if (!gameManager.variableSelection) {
            gameManager.actionButtonComponent.interactable = false;
            gameManager.actionButton.SetButtonType(ActionButtonType.Pass);
        }
    }

    private void CheckMaxAmount() {
        int selectionMax = gameManager.currentSelectionType == ActionButtonType.Target
            ? gameManager.currentTargetMax
            : gameManager.currentSelectionMax;

        if (gameManager.variableSelection) {
            if (gameManager.selectedUids.Count >= selectionMax) {
                gameManager.DisableUnselectedSelectablesInCardGroup();
            }
            return;
        }

        if (gameManager.selectedUids.Count < selectionMax) return;
        gameManager.DisableUnselectedSelectablesInCardGroup();
        gameManager.actionButtonComponent.interactable = true;
        gameManager.actionButton.SetButtonType(gameManager.currentSelectionType);
    }
}
