using System.Collections.Generic;
using System.Linq;
using DeckEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.VisualScripting;
using UnityEngine.Serialization;
using GameObject = UnityEngine.GameObject;

public class DeckEditManager : MonoBehaviour {

    public GameData gameData;
    private AccountDataGO accountDataGO;
    public GameObject cardPrefab;
    public GameObject deckListCardPrefab;
    public GameObject collection;
    public GameObject deckList;
    public GameObject sideBoardList;
    public GameObject sideBoardView;
    private Dictionary<int, int> cardCopies = new();
    public Dictionary<int, GameObject> collectionCardsToObj = new();

    public GameObject simpleCardDisplayPfb;
    
    // card detail display
    public DetailPanel detailPanel;
    public GameObject bigCardDisplayContainer;
    public CardDisplaySimple bigCardDisplay;
    
    // filters
    private int costMinFilter;
    private int costMaxFilter;
    private List<Tribe> tribesFilter = new();
    private List<CardType> cardTypesFilter = new();
    public string searchFilter;
    
    // !NOTICE: this list is to prevent multi-tribe searches, it may be removed later if multi-tribe cards exist
    private List<GameObject> currentlyActiveBorders = new();
    
    // functioning filter
    public GameObject functioningBorderObj;
    public GameObject mouseCardDisplayContainer;
    public CardDisplaySimple mouseCardDisplay;
    public TMP_InputField searchInputField;

    public int deckMaxSize = 40;
    public int deckSize;

    public TMP_InputField editorDeckName;
    public List<int> editorDeckList = new();
    public List<CardDisplayData> editorSideboardList = new();
    public TMP_Text deckCardCount;

    private ServerApi serverApi = new ServerApi();
    

    void Start() {
        accountDataGO = GameObject.Find("AccountData").GetComponent<AccountDataGO>();
        gameData = GameObject.Find("GameData").GetComponent<GameData>();
        if (gameData.currentDeck != null) {
            editorDeckName.text = gameData.currentDeck.deckName;
            DisplayDeckList();
        }
        InitializeStaticCardDisplays();
        DisplayCollection();
    }

    private void InitializeStaticCardDisplays() {
        mouseCardDisplay = Instantiate(simpleCardDisplayPfb, mouseCardDisplayContainer.transform).GetComponent<CardDisplaySimple>();
        mouseCardDisplay.gameData = gameData;
        bigCardDisplay = Instantiate(simpleCardDisplayPfb, bigCardDisplayContainer.transform).GetComponent<CardDisplaySimple>();
        bigCardDisplay.gameData = gameData;
    }

    private void DisplayDeckList() {
        foreach (int cardId in gameData.currentDeck.deckList) {
            editorDeckList.Add(cardId);
            CardDisplayData cdd = gameData.allCardsDict[cardId];
            DisplayCardInDeck(cdd);
            if (cardCopies.ContainsKey(cardId)) {
                cardCopies[cardId] += 1;
            } else {
                cardCopies.Add(cardId, 1);
            }
        }
    }

    private void DisplayCollection() {
        // Temporary access to all cards. The outer for loop is to put 3 copies of each card in.
        for (var cardCount = 0; cardCount < 3; cardCount++) {
            foreach (var i in gameData.allCardDisplayDatas) {
                if (collectionCardsToObj.ContainsKey(i.id)) {
                    var cardDisplay = collectionCardsToObj[i.id].GetComponent<DeckEditorCardDisplay>();
                    if (cardDisplay.copies < 3) {
                        cardDisplay.AddCopy();
                    }
                } else {
                    GameObject newCardDisplay = Instantiate(cardPrefab, new Vector3(0, 0, 0), Quaternion.identity,
                        collection.transform);
                    DeckEditorCardDisplay newDECDisplay = newCardDisplay.GetComponent<DeckEditorCardDisplay>();
                    newDECDisplay.Initialize(i);
                    collectionCardsToObj.Add(i.id, newCardDisplay);
                    newCardDisplay.name = i.name;
                    newCardDisplay.GetComponent<DeckEditorCardDisplay>().AddCopy();
                }
            }
        }

        // Final product will only show cards in each account's collection:
        /*
        foreach (var i in accountDataGO.cards) {
            if (!gameData.allCardsDict.ContainsKey(i)) {
                Debug.Log("Card with ID: " + i + " does not exist in your local game files");
                return;
            }
            cardPrefab.GetComponent<CardDisplay>().card = gameData.allCardsDict[i];
            Instantiate(cardPrefab, new Vector3(0, 0, 0), Quaternion.identity, collection.transform);
        }
        */

        // removes cards from your collection that are already in your deck.
        foreach (var cardId in editorDeckList) {
            collectionCardsToObj[cardId].GetComponent<DeckEditorCardDisplay>().RemoveCopy();
        }
    }

    private void RefreshFilters() {
        // temporarily displays all cards in game. Final release will access accountDataGO.cards
        foreach (CardDisplayData cdd in gameData.allCardDisplayDatas) {
            collectionCardsToObj[cdd.id].SetActive(CompareAgainstFilters(cdd));
        }
    }

    private bool CompareAgainstFilters(CardDisplayData cdd) {
        if (cardTypesFilter.Count > 0 && !cardTypesFilter.Contains(cdd.type)) return false;
        if (tribesFilter.Count > 0 && !tribesFilter.Contains(cdd.tribe)) return false;
        // TODO cost filters (min/max)
        if(functioningBorderObj.activeSelf && 
           !gameData.functioningIds.Contains(cdd.id)) return false;
        string tempDescription = "";
        if (cdd.description != null) {
            tempDescription = cdd.description.ToLower();
        }
        string tempName = cdd.name.ToLower();
        if(!tempName.Contains(searchFilter) && !tempDescription.Contains(searchFilter)) return false;
        return true;
    }

    public void LoadMainMenu() {
        SceneManager.LoadScene("Main Menu");
    }

    public void AddCard(CardDisplayData card, DeckEditorCardDisplay cardDisplay) {
        // return if deck is max size
        if (deckCardCount.text is "40/40" or "9/9") {
            Debug.Log("Max cards in deck");
            return;
        }

        // check if the card is in the deck already
        if (cardCopies.ContainsKey(card.id)) {
            // return if you already have max copies
            if (cardCopies[card.id] >= 3) {
                Debug.Log("Max copies of " + card.name);
                return;
            }

            // if there are copies, but not max
            cardDisplay.copies--;
            cardCopies[card.id] += 1;
        } else {
            // if there are no copies in the deck yet
            cardDisplay.copies--;
            cardCopies.Add(card.id, 1);
        }

        // add it to the main deck or sideboard, depending on which is active
        Debug.Log("adding card to deck: " + card.name);
        if (sideBoardView.activeSelf) {
            editorSideboardList.Add(card);
        } else {
            editorDeckList.Add(card.id);
        }

        DisplayCardInDeck(card);
    }

    private void DisplayCardInDeck(CardDisplayData c) {
        GameObject deckListTemp = deckList;
        if (sideBoardView.activeSelf) {
            deckListTemp = sideBoardList;
        }

        var newDeckCard =
            Instantiate(deckListCardPrefab, new Vector3(0, 0, 0), Quaternion.identity, deckListTemp.transform);
        var newDCardDisplay = newDeckCard.GetComponent<DeckListCardDisplay>();
        newDCardDisplay.name.text = c.name;
        newDCardDisplay.cost.text = c.cost.ToString();
        newDCardDisplay.backgroundType.sprite = gameData.decklistToColor[c.tribe];
        newDCardDisplay.card = c;
        UpdateCardCount();
    }

    public void RemoveCard(CardDisplayData c) {
        Debug.Log("removing " + c.name + " from deck list");
        if (sideBoardView.activeSelf) {
            editorSideboardList.Remove(c);
        } else {
            editorDeckList.Remove(c.id);
        }

        UpdateCardCount();
        cardCopies[c.id] -= 1;
    }

    public void SaveDeck() {
        Debug.Log("Deck list is saved with card ids: ");
        foreach (int cardId in editorDeckList) {
            Debug.Log(cardId);
        }

        DeckData deckData = new DeckData(gameData.currentDeck?.id ?? 0, editorDeckName.text, editorDeckList);
        var response = serverApi.CreateOrUpdateDeck(deckData, accountDataGO.accountData);
        Debug.Log(response);
    }

    public void UpdateSearchFilter() {
        if (searchInputField.text.Length is 1 or 2) return; 
        searchFilter = searchInputField.text.ToLower();
        RefreshFilters();
    }

    public void ClearFilters() {
        searchFilter = "";
        tribesFilter.Clear();
        cardTypesFilter.Clear();
        costMaxFilter = 100;
        costMinFilter = 0;
        RefreshFilters();
    }
    public void ToggleFunctioningCards() {
        functioningBorderObj.SetActive(!functioningBorderObj.activeSelf);
        RefreshFilters();
    }

    public void ToggleTribeFilter(FilterToggler filterToggler) {
        Tribe tribe = filterToggler.tribe;
        if (tribesFilter.Contains(tribe)) {
            filterToggler.toggleBorder.SetActive(false);
            tribesFilter.Remove(tribe);
            currentlyActiveBorders.Remove(filterToggler.toggleBorder);
        } else {
            ClearTribeFilters();
            filterToggler.toggleBorder.SetActive(true);
            tribesFilter.Add(tribe);
            currentlyActiveBorders.Add(filterToggler.toggleBorder);
        }
        RefreshFilters();
    }

    private void ClearTribeFilters() {
        tribesFilter.Clear();
        foreach (GameObject borderObj in currentlyActiveBorders) {
            borderObj.SetActive(false);
        }
    }
    
    public void DisplayMainDeck() {
        sideBoardList.SetActive(false);
        sideBoardView.SetActive(false);
        deckList.SetActive(true);
        UpdateCardCount();
    }

    public void DisplaySideBoard() {
        sideBoardList.SetActive(true);
        sideBoardView.SetActive(true);
        deckList.SetActive(false);
        UpdateCardCount();
    }
    
    private void UpdateCardCount() {
        if (sideBoardView.activeSelf) {
            deckCardCount.text = editorSideboardList.Count + "/9";
        }
        else {
            deckCardCount.text = editorDeckList.Count + "/40";
        }
    }

    public void DisplayDetailPanel(CardDisplayData cdd, int copiesAmount) {
        SetBigCard(cdd);
        detailPanel.cardCopiesAmountText.text = copiesAmount.ToString();
        detailPanel.gameObject.SetActive(true);
    }
    
    public void SetBigCard(CardDisplayData cdd) {
        bigCardDisplay.UpdateCardDisplayData(cdd);
    }

    public int GetTotalCopies(DeckEditorCardDisplay editorCDD = null) {
        if (editorCDD == null) return 0;
        int finalAmount = editorDeckList.Count(deckCard => editorCDD.card.id == deckCard);
        return finalAmount += editorCDD.copies;
    }
    
}
