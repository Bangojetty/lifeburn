using System.Collections;
using System.Collections.Generic;
using InGame;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Utilities;

public class CardDisplaySimple : MonoBehaviour
{
    public GameData gameData;
    public CardDisplayData card;

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
    }
    

    public void UpdateCardDisplayData(CardDisplayData newCard = null) {
        if (newCard != null) {
            card = newCard;
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
        artworkImg.sprite = gameData.allArtworks[card.id];
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
            default: 
                Debug.Log("No CardType");
                break;
        }
    }
}
