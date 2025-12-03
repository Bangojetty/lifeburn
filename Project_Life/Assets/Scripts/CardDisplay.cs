using System.Collections;
using System.Text.RegularExpressions;
using InGame;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.Video;
using Utilities;
using Image = UnityEngine.UI.Image;

public class CardDisplay : MonoBehaviour {
    // debug
    public TMP_Text debugUidText;
    // main
    public GameData gameData;
    public GameManager gameManager;
    public CardDisplayData card;
    private CardDisplayData baseCard;
    
    // colors
    
    public GameObject cardInfo;
    public TMP_Text nameText;
    public TMP_Text cardTypeText;
    public TMP_Text descriptionText;
    public TMP_Text lifeCostText;
    public GameObject atkDef;
    public TMP_Text atkDefText;
    public GameObject keywordsObj;
    public GameObject keywordsPfb;
    public GameObject objectIcon;

    public Image artworkImg;
    public Image backgroundImg;

    public GameObject playableHighlight;
    public GameObject attackingHighlight;
    public GameObject targetableHighlight;
    public GameObject selectedHighlight;

    public bool isPlayable;
    public bool isDragDisplay;

    public GameObject castingAnimation;
    public VideoPlayer videoPlayer;
    public VideoPlayer summonVideoPlayer;
    public GameObject summonAnimation;
    
    // UI interaction
    public GameObject cardSlotPfb;
    public GameObject oppCardSlotPfb;
    public Player player;
    public Participant opponent;

    public GameObject selectableCardObj;
    public GameObject attackCapableObj;
    public GameObject attackableObj;
    public GameObject selectableTargetObj;
    public GameObject targetingLocationObj;
    public GameObject interactableObj;
    public Interactable interactable;
    
    // Dynamic Referencing
    public DynamicReferencer dynamicReferencer;

    // this was added post-break, could be jank -> you might want to consider setting this during death event if 
    // nothing else uses this bool.
    public bool ownerIsOpponent;
    
    // stack interaction
    public StackDisplayData tempStackDisplayData;
    
    // combat interaction
    public GameObject attackTarget;
    
    void Awake() {
        summonVideoPlayer.Stop();
        if (SceneManager.GetActiveScene().name == "Game Scene") {
            gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
            attackCapableObj.GetComponent<AttackCapable>().gameManager = gameManager;
            attackableObj.GetComponent<Attackable>().gameManager = gameManager;
            selectableCardObj.GetComponent<SelectableCard>().gameManager = gameManager;
            selectableTargetObj.GetComponent<SelectableTarget>().gameManager = gameManager;
            interactableObj.GetComponent<Interactable>().gameManager = gameManager;
            interactable = interactableObj.GetComponent<Interactable>();
            opponent = gameManager.opponent;
            player = gameManager.player;
        }
        gameData = GameObject.Find("GameData").GetComponent<GameData>();
    }
    private void SetBaseCard() {
        if (card == null) return;
        // if it's a token
        if (card.tokenType != null) {
            baseCard = card;
            return;
        }
        baseCard = gameManager.serverApi.GetCard(gameData.accountData, card.id);
    }

    private void ResetCard() {
        if (card.type == CardType.Summon) {
            Debug.Assert(baseCard.attack != null, "summon is missing attack");
            Debug.Assert(baseCard.defense != null, "summon is missing defense");
            card.attack = (int)baseCard.attack;
            card.defense = (int)baseCard.defense;
        }
        UpdateStats();
    }

    public void Kill() {
        dynamicReferencer.DisableAllInteractable();
        // if it's a token, just destroy it
        if (card.id < 0) {
            // remove from maps to avoid null references
            gameManager.UidToObj.Remove(card.uid);
            // OBLITERATE
            Destroy(gameObject);
        }
        GameObject playSlot = transform.parent.gameObject;
        if (ownerIsOpponent) {
            AddToGraveyardOpponent();
        } else {
            AddToGraveyard();
        }
        Destroy(playSlot);
    }

    public void UpdateCardDisplayData(CardDisplayData newCard = null) {
        if (newCard != null) {
            card = newCard;
        }
        if(card != null) AddToUidMap();
        SetBaseCard();
        DisplayCardData();
        if (card == null) return;
        DisplayDebugData();
        SetType();
    }

    public void DisplayCardData(CardDisplayData cdd = null) {
        if (cdd != null) card = cdd;
        if (card == null) {
            backgroundImg.sprite = gameData.faceDownTemplate;
            cardInfo.SetActive(false);
            return;
        }
        cardInfo.SetActive(true);
        nameText.text = card.name;
        // set CardTypeText depending on type
        cardTypeText.text = card.type switch {
            CardType.Summon => card.type + " - " + card.tribe,
            _ => card.type.ToString()
        };
        // set description and add additional text (added passives/actives etc.)
        // Util function sets the color of any chosen "choose" effects to cyan
        string tempDescription = Utils.GetStringWithChosenText(card.description);
        descriptionText.text = tempDescription + "\n" + Utils.colorCyan + card.additionalDescription + "</color>";
        if (card.type != CardType.Summon) {
            atkDef.SetActive(false);
        } 
        UpdateStats();
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
        artworkImg.sprite = card.id >= 0 ? gameData.allArtworks[card.id] : gameData.tokenArtById[card.id];
    }

    private void DisplayDebugData() {
        debugUidText.text = card.uid.ToString();
    }

    private void UpdateStats() {
        if (card.type == CardType.Summon) {
            atkDefText.text = GetModColoredText(card.attack, baseCard.attack) + "/" +
                              GetModColoredText(card.defense, baseCard.defense);
        }
        SetCostText();
    }

    private void SetCostText() {
        if (card.hasXCost) {
            lifeCostText.text = player.isSpellburnt ? "<color=red>X</color>" : "X";
            return;
        }
        lifeCostText.text = GetModColoredText(card.cost, baseCard.cost, true);
    }

    private string GetModColoredText(int? modValue, int? baseValue, bool isReverse = false) {
        string preColor = "";
        string postColor = "";
        if (modValue < baseValue) { 
            preColor = isReverse ? "<color=green>" : "<color=red>";
            postColor = "</color>";
        }
        if (modValue > baseValue) {
            preColor = isReverse ? "<color=red>" : "<color=green>";
            postColor = "</color>";
        } 
        if (modValue == baseValue) {
            preColor = "<color=white>";
            postColor = "</color>";
        }
        return preColor + modValue + postColor;
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
                break;
            default: 
                Debug.Log("No CardType");
                break;
        }
    }

    public void ActivateTribeCastingVideo() {
        videoPlayer.Stop();
        videoPlayer.clip = gameData.tribeToCastVideo[card.tribe];
        videoPlayer.Play();
    }

    public void AddToHand() {
        gameManager.player.hand.Add(card);
        GameObject newCardSlot = Instantiate(cardSlotPfb, player.handZoneObj.transform);
        player.handSlots.Add(newCardSlot);
        transform.SetParent(newCardSlot.transform);
        newCardSlot.GetComponent<CardSlot>().Initialize(gameObject);
        GetComponent<Animator>().enabled = false;
        transform.localPosition = new Vector3(0, 0, 0);
        RectTransform cardRectTransform = gameObject.GetComponent<RectTransform>();
        cardRectTransform.anchorMin = new Vector2(0, 0.5f);
        cardRectTransform.anchorMax = new Vector2(0, 0.5f);
        cardRectTransform.pivot = new Vector2(0, 0.5f);
    }

    public void RemoveFromHand() {
        DisableInteractableAndHighlights();
        GameObject oldCardSlot = transform.parent.gameObject;
        transform.SetParent(gameManager.displayCanvas.transform);
        backgroundImg.raycastTarget = true;
        RectTransform cardRectTransform = gameObject.GetComponent<RectTransform>();
        transform.localPosition = new Vector3(0, 0, 0);
        cardRectTransform.pivot = new Vector2(0.5f, 0);
        cardRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        cardRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        Destroy(oldCardSlot);
    }

    public void AddToStack() {
        gameManager.CreateNewStackObj(tempStackDisplayData, gameManager.stackView.transform);
        gameManager.UidToObj.Remove(card.uid);
        Destroy(gameObject);
    }

    public void AddToHandOpponent() {
        // creates a oppCardSlot object in the handzone and sets it as the parent for this card.
        GameObject newOppCardSlot = Instantiate(oppCardSlotPfb, opponent.handZoneObj.transform);
        transform.SetParent(newOppCardSlot.transform);
        newOppCardSlot.GetComponent<HoverDisplayer>().cardDisplay = this;
        GetComponent<Animator>().enabled = false;
        transform.localPosition = new Vector3(0, 0, 0);
        gameObject.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
    }
    public void AddToGraveyard() {
        if (card.id >= 0) {
            ResetCard();
        }
        GetComponent<Animator>().enabled = false;
        transform.SetParent(player.graveyardContents.transform);
        transform.localPosition = new Vector3(0, 0, 0);
        gameObject.GetComponent<RectTransform>().localScale = new Vector3(0.6f, 0.6f, 0.6f);
        gameObject.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
    }

    public void AddToGraveyardOpponent() {
        if (card.id >= 0) {
            ResetCard();
        }
        GetComponent<Animator>().enabled = false;
        transform.SetParent(opponent.graveyardContents.transform);
        transform.localPosition = new Vector3(0, 0, 0);
        gameObject.GetComponent<RectTransform>().localScale = new Vector3(0.6f, 0.6f, 0.6f);
        gameObject.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
    }
    
    public void AddToDeck() {
        transform.SetParent(player.deckObj.transform);
        GetComponent<Animator>().enabled = false;
        transform.localPosition = new Vector3(0, 0, 0);
        gameObject.GetComponent<RectTransform>().localScale = new Vector3(0.5f, 0.5f, 0.5f);
        Destroy(gameObject);
    }

    public void AddToDeckOpponent() {
        transform.SetParent(opponent.deckObj.transform);
        GetComponent<Animator>().enabled = false;
        transform.localPosition = new Vector3(0, 0, 0);
        gameObject.GetComponent<RectTransform>().localScale = new Vector3(0.5f, 0.5f, 0.5f);
        Destroy(gameObject);
    }

    public void AddToPlay() {
        GameObject newPlaySlot = Instantiate(gameManager.playSlotPfb, player.playZoneObj.transform);
        transform.SetParent(newPlaySlot.transform);
        GetComponent<Animator>().enabled = false;
        transform.localPosition = new Vector3(0, 0, 0);
        float scaleFactor = player.playZoneScaler.GetScaleFactor();
        newPlaySlot.GetComponent<RectTransform>().localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
        gameObject.GetComponent<RectTransform>().localScale = new Vector3(0.7f, 0.7f, 0.7f);
        AddToUidMap();
        player.playZoneScaler.Rescale();

    }

    public void AddToPlayOpp() {
        GameObject newPlaySlot = Instantiate(gameManager.playSlotPfb, opponent.playZoneObj.transform);
        transform.SetParent(newPlaySlot.transform);
        GetComponent<Animator>().enabled = false;
        transform.localPosition = new Vector3(0, 0, 0);
        float scaleFactor = opponent.playZoneScaler.GetScaleFactor();
        newPlaySlot.GetComponent<RectTransform>().localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
        gameObject.GetComponent<RectTransform>().localScale = new Vector3(0.7f, 0.7f, 0.7f);
        AddToUidMap();
        opponent.playZoneScaler.Rescale();
    }

    public void DisableInteractableAndHighlights() {
        // highlights
        if(!gameManager.attackUids.ContainsKey(card.uid)) attackingHighlight.SetActive(false);
        playableHighlight.SetActive(false);
        selectedHighlight.SetActive(false);
        targetableHighlight.SetActive(false);
        // gameobjects
        attackCapableObj.SetActive(false);
        attackableObj.SetActive(false);
        selectableTargetObj.SetActive(false);
        interactable.isActivatable = false;
        // booleans
        isPlayable = false;
    }

    public void EnablePlayable() {
        playableHighlight.SetActive(true);
        isPlayable = true;
    }

    public void EnableActivatable() {
        interactable.isActivatable = true;
        playableHighlight.SetActive(true);
    }
    public void EnableAttackCapable() {
        attackCapableObj.SetActive(true);
        if (gameManager.attackUids.ContainsKey(card.uid)) return;
        playableHighlight.SetActive(true);
    }

    public void PlaySummonAnim() {
        summonAnimation.SetActive(true);
        summonVideoPlayer.Play();
        StartCoroutine(DisableSummonEffectWhenDone());
    }

    private void AddToUidMap() {
        if (!gameManager.UidToObj.ContainsKey(card.uid)) {
            gameManager.UidToObj[card.uid] = gameObject;
        }
        // also sets the reference in the DRef
        dynamicReferencer.uid = card.uid;
    }

    private IEnumerator DisableSummonEffectWhenDone() {
        yield return new WaitForSeconds((float)summonVideoPlayer.clip.length);
        summonAnimation.SetActive(false);
    }

    // might be a cleaner way to do this -> create an attack reference and call a GameManager function passing in this 
    // card display as a parameter (e.g. DisplayAttackResolve).
    public void UpdateDamageNumber() {
        DynamicReferencer targetDr = attackTarget.GetComponent<DynamicReferencer>();
        if (targetDr.atkDefText != null) {
            CardDisplay targetCd = attackTarget.GetComponent<CardDisplay>(); 
            targetCd.card.defense -= card.attack;
            targetCd.UpdateStats();
        } else {
            Participant targetOpponent = attackTarget.GetComponent<Participant>();
            Player targetPlayer = attackTarget.GetComponent<Player>();
            Debug.Assert(card.attack != null, "attacking card has no attack value");
            if (targetOpponent != null) {
                targetOpponent.lifePoints -= card.attack.Value;
                targetOpponent.UpdateUI(); 
            } else {
                targetPlayer.lifePoints -= card.attack.Value;
                targetPlayer.UpdateUI();
            } 
        }
    }
}