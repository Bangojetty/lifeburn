using System.Collections.Generic;
using System.Linq;
using InGame.CardDataEnums;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace InGame {
    public abstract class Participant : MonoBehaviour {
        public int uid;
        public TMP_Text displayNameText;
        public int lifePoints;
        public TMP_Text lifePointsText;
        public TMP_Text deckAmountText;
        public GameObject attackableObj;
        public GameObject handZoneObj;
        public List<CardDisplayData> graveyard = new();
        public GameObject graveyardContents;
        public GameObject deckObj;
        public List<CardDisplayData> playField = new();
        private Dictionary<int, GameObject> stackIdToCardObj = new();
        public GameObject playZoneObj;
        public GameManager gameManager;
        
        // UI elements
        public GameObject tokenView;
        public ZoneScaler playZoneScaler;
        
        // Dynamic Referencing
        public DynamicReferencer dynamicReferencer;
        
        // tokenZone
        private List<int> uniqueTokenStackIds = new();
        
        // animations
        // TODO add a pay life animation (blood flying out of heart towards the sourceCard of the payment)

        public GameObject spellburnVFX;

        public bool isSpellburnt;
            
        public int handAmount;
        public int drawAmount;
        
        
        public void Initialize(ParticipantState participantState) {
            // set dRef values
            dynamicReferencer.uid = participantState.uid;
            displayNameText.text = participantState.playerName;
            lifePoints = participantState.lifeTotal;
            deckAmountText.text = participantState.deckAmount.ToString();
            handAmount = participantState.handAmount;
            UpdateUI();
            gameManager.UidToObj.Add(participantState.uid, gameObject);
            uid = participantState.uid;
        }
    
        public virtual CardDisplay Summon(CardDisplayData cardDisplayData) {
            GameObject newPlaySlot = Instantiate(gameManager.playSlotPfb, playZoneObj.transform);
            GameObject newCardObj = gameManager.CreateAndInitializeNewCardDisplay(cardDisplayData, newPlaySlot.transform);
            float scaleFactor = playZoneScaler.GetScaleFactor();
            newPlaySlot.GetComponent<RectTransform>().localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
            newCardObj.GetComponent<RectTransform>().localScale = new Vector3(0.7f, 0.7f, 0.7f);
            gameManager.UidToObj[cardDisplayData.uid] = newCardObj;
            CardDisplay newCardDisplay = newCardObj.GetComponent<CardDisplay>();
            newCardObj.GetComponent<Animator>().enabled = false;
            newCardDisplay.PlaySummonAnim();
            playZoneScaler.Rescale();
            return newCardDisplay;
        } 
        
        public void CreateAndAddCardToZone(CardDisplayData cdd, Zone zone) {
            GameObject newCardObj = gameManager.CreateAndInitializeNewCardDisplay(cdd, gameManager.displayCanvas.transform);
            CardDisplay newCardDisplay = newCardObj.GetComponent<CardDisplay>();
            switch (zone) {
                case Zone.Graveyard:
                    if (this is Opponent) {
                        newCardDisplay.AddToGraveyardOpponent();
                    } else {
                        newCardDisplay.AddToGraveyard();
                    }
                    break;
                default:
                    Debug.Log("Zone not implemented for CreateAndAddCardToZone");
                    break;
            }
        }

    
        public void UpdateUI() {
            lifePointsText.text = lifePoints.ToString();
        }
        
        public void GainLife(int amount) {
            lifePoints += amount;
            UpdateUI();
        }

        public void LoseLife(int amount) {
            lifePoints -= amount;
            UpdateUI();
        }
        
        public void SetLifeTotal(int gEventAmount) {
            lifePoints = gEventAmount;
            UpdateUI();
        }
        
        public void ToggleSpellburn() {
            spellburnVFX.SetActive(!spellburnVFX.activeSelf);
            isSpellburnt = spellburnVFX.activeSelf;
        }

        public List<CardDisplay> GetCardsInHand() {
            return handZoneObj.GetComponentsInChildren<CardDisplay>().ToList();
        }

        public void CreateOrAddToken(CardDisplayData tdd, int cardStackId) {
            if (stackIdToCardObj.ContainsKey(cardStackId)) {
                stackIdToCardObj[cardStackId].GetComponent<TokenDisplay>().AddToken(tdd.uid);
            } else {
                GameObject newTokenObj = Instantiate(gameManager.tokenDisplayPfb, tokenView.transform);
                TokenDisplay tokenDisplay = newTokenObj.GetComponent<TokenDisplay>();
                tokenDisplay.Initialize(tdd);
                stackIdToCardObj.Add(cardStackId, newTokenObj);
            }
        }
        
        public void RemoveToken(CardDisplayData cdd, int cardStackId) {
            TokenDisplay tempTokenDisplay = stackIdToCardObj[cardStackId].GetComponent<TokenDisplay>();
            tempTokenDisplay.RemoveToken(cdd.uid);
            if (tempTokenDisplay.tokenUids.Count == 0) {
                stackIdToCardObj.Remove(cardStackId);
                Destroy(tempTokenDisplay.gameObject);
            }
        }
    }
}