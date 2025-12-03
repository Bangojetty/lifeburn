using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeckEditor  {
    public class DeckEditorCardDisplay : MonoBehaviour, IPointerClickHandler {
        public GameData gameData;
        public DeckEditManager deckEditManager;

        public CardDisplayData card;
        public int copies;

        public TMP_Text nameText;
        public TMP_Text cardTypeText;
        public TMP_Text descriptionText;
        public TMP_Text lifeCostText;
        public GameObject atkDef;
        public GameObject keywordsObj;
        public GameObject keywordsPfb;
        public GameObject objectIcon;

        public TMP_Text collectionCopiesText;
        public GameObject greyFilter;

        public Image artworkImg;
        public Image backgroundImg;

        void Awake() {
            deckEditManager = GameObject.Find("DeckEditManager").GetComponent<DeckEditManager>();
            gameData = GameObject.Find("GameData").GetComponent<GameData>();
        }

        public void Initialize(CardDisplayData c) {
            card = c;
            DisplayCardData();
            SetType();
        }
        private void DisplayCardData() {
            nameText.text = card.name;
            // set CardTypeText depending on type
            cardTypeText.text = card.type switch {
                CardType.Summon => card.type + " - " + card.tribe,
                _ => card.type.ToString()
            };
            descriptionText.text = card.description;
            // set cost text (set it to X if it's an X cost card) 
            lifeCostText.text = card.hasXCost ? "X" : card.cost.ToString();
            atkDef.GetComponent<TMP_Text>().text = (card.attack + "/" + card.defense);
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
                    atkDef.SetActive(false);
                    break;
            }
        }

        private void DisplayCardBack() {
            nameText.text = null;
            descriptionText.text = null;
            lifeCostText.text = null;
            atkDef.GetComponent<TMP_Text>().text = null;
            artworkImg.sprite = null;
        }

        public void OnPointerClick(PointerEventData eventData) {
            // left click
            if (eventData.button == PointerEventData.InputButton.Left) {
                if (greyFilter.activeSelf) return;
                if (copies < 1) {
                    greyFilter.SetActive(true);
                    return;
                }
                deckEditManager.AddCard(card, this);
                collectionCopiesText.text = copies.ToString();
                if (copies < 1) {
                    greyFilter.SetActive(true);
                }
            } 
            // right click
            else {
                deckEditManager.DisplayDetailPanel(card, deckEditManager.GetTotalCopies(this));
            }
        }
        public void AddCopy() {
            copies++;
            collectionCopiesText.text = copies.ToString();
            greyFilter.SetActive(false);
        }

        public void RemoveCopy() {
            copies--;
            collectionCopiesText.text = copies.ToString();
            if (copies < 1) {
                greyFilter.SetActive(true);
            }
        }
    }
}