using System;
using System.Collections;
using System.Collections.Generic;
using InGame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utilities;

public class StackObjDisplay : MonoBehaviour {
    public GameData gameData;
    
    public CardDisplayData card;
    public StackDisplayData stackDisplayData;

    public TMP_Text nameText;
    public TMP_Text cardTypeText;
    public TMP_Text descriptionText;
    public TMP_Text costText;
    public GameObject atkDef;
    public GameObject keywordsObj;
    public GameObject keywordsPfb;

    public Image artworkImg;
    public Image backgroundImg;


    public void Initialize(StackDisplayData sourceDisplayData) {
        gameData = GameObject.Find("GameData").GetComponent<GameData>();
        card = sourceDisplayData.cardDisplayData;
        stackDisplayData = sourceDisplayData;
        DisplayData();
    }

    private void DisplayData() {
        nameText.text = card.name;
        if (stackDisplayData.stackObjType == StackObjType.Spell) {
            // set CardTypeText depending on type
            cardTypeText.text = card.type switch {
                CardType.Summon => card.type + " - " + card.tribe,
                _ => card.type.ToString()
            };
            costText.text = card.cost.ToString();
            if (card.type != CardType.Summon) {
                atkDef.SetActive(false);
            } else {
                atkDef.GetComponent<TMP_Text>().text = (card.attack + "/" + card.defense);
            }
            if (card.keywords != null) {
                foreach (Transform child in keywordsObj.transform) {
                    Destroy(child.gameObject);
                }
                foreach (Keyword keyword in card.keywords) {
                    GameObject newKeyword = Instantiate(keywordsPfb, keywordsObj.transform);
                    newKeyword.GetComponent<Image>().sprite = gameData.keywordImgDict[keyword];
                }
            }
            if (card.description != null) {
                string tempDescription = Utils.GetStringWithChosenText(card.description);
                descriptionText.text = tempDescription;
            }
            SetTypeSpell();
        } else {
            descriptionText.text = String.Join(" ", stackDisplayData.effectStrings);
            SetTypeAbility();
        }
        artworkImg.sprite = gameData.allArtworks[card.id];
    }
    
    private void SetTypeSpell() {
        switch (card.type) {
            case CardType.Spell:
                backgroundImg.sprite = gameData.spellToColor[card.tribe];
                atkDef.SetActive(false);
                break;
            case CardType.Summon:
                backgroundImg.sprite = gameData.creatureToColor[card.tribe];
                break;
            case CardType.Object:
                backgroundImg.sprite = gameData.spellToColor[card.tribe];
                atkDef.SetActive(false);
                break;
        }
    }
    
    private void SetTypeAbility() {
        backgroundImg.sprite = gameData.abilityToColor[card.tribe];
        atkDef.SetActive(false);
        costText.text = "";
    }
}
