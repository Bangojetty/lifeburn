using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using InGame;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEditor;
using UnityEngine.EventSystems;

public class MenuManager : MonoBehaviour {

    public GameObject mainMenu;
    public GameObject deckSelectEditor;
    public GameObject deckSelect;
    public GameObject loadingMatch;
    public TMP_Text matchPlayer1;
    public TMP_Text matchPlayer2;
    public Animation screenFade;
    
    public GameData gameData;
    public GameObject gameDataPfb;

    public Sprite[] backgrounds;
    public Image bgA;
    public Image bgB;
    public int bgDuration;
    public int bgFadeSpeed;

    private bool bgIsCyclable = true;

    public GameObject menuDeckDisplay;
    public GameObject createdDecks;
    public GameObject createdDecksEditor;
    public GameObject gameModeDialogue;
    public GameObject areYouSureDialogue;

    public Button editBtn;
    public Button delBtn;

    public GameObject selectedDeckObj;
    public TMP_Text displayNameText;
    public AccountDataGO accountDataGO;

    public GameObject accountScreen;

    public Button deckSelectPlayBtn;
    public GameObject matchMakingTimer;

    public List<GameObject> deckObjs;
    
    // ***Temp***
    private PlayerState _playerOneState;
    private PlayerState _playerTwoState;
    public float queueCheckInterval;
    
    // Network
    private ServerApi serverApi = new();

    void Start() {
        accountDataGO = GameObject.Find("AccountData").GetComponent<AccountDataGO>();
        gameData = GameObject.Find("GameData") == null
            ? Instantiate(gameDataPfb, new Vector3(0, 0, 0), Quaternion.identity).GetComponent<GameData>()
            : GameObject.Find("GameData").GetComponent<GameData>();
        gameData.name = "GameData";
        
        displayNameText.text = "Account: " + accountDataGO.accountData.displayName;
        accountDataGO.cards = serverApi.GetAccountCards(accountDataGO.accountData);
        DisplayAccountDecks();
        // StartCoroutine(CycleBackgrounds());
        // Debug.Log("got past the coroutine");
    }

    public void ToggleGameModeSelect() {
        gameModeDialogue.SetActive(!gameModeDialogue.activeSelf);
    }

    public void ToDeckSelect() {
        mainMenu.SetActive(false);
        deckSelect.SetActive(true);
    }

    public void ToDeckSelectEditor() {
        mainMenu.SetActive(false);
        deckSelectEditor.SetActive(true);

    }

    public void ToMainMenu() {
        mainMenu.SetActive(true);
        deckSelectEditor.SetActive(false);
        deckSelect.SetActive(false);
        gameModeDialogue.SetActive(false);
    }

    public void ToggleAccountScreen() {
        accountScreen.SetActive(!accountScreen.activeSelf);
    }

    public void EditDeck() {
        SceneManager.LoadScene("Deck Editor");
    }

    public void PlayButton() {
        deckSelectPlayBtn.interactable = false;
        ToggleMatchmaking();
    }

    public void ToggleMatchmaking() {
        if (matchMakingTimer.activeSelf) {
            matchMakingTimer.GetComponent<MatchMakingTimer>().secondsCount = 0f;
            matchMakingTimer.GetComponent<MatchMakingTimer>().minuteCount = 0;
            matchMakingTimer.SetActive(false);
            serverApi.ExitQueue(accountDataGO.accountData);
            // enable all decks for selection again
            foreach (GameObject deckObj in deckObjs) {
                deckObj.GetComponent<Selectable>().interactable = true;
            }
            return;
        }
        // entering queue, disable all decks so you can't switch decks in queue
        foreach (GameObject deckObj in deckObjs) {
            deckObj.GetComponent<Selectable>().interactable = false;
        }
        StartCoroutine(QueueRefresh());
        matchMakingTimer.SetActive(true);
    }

    private IEnumerator QueueRefresh() {
        while (gameData.matchState == null) {
            yield return new WaitForSeconds(1); 
            gameData.matchState = serverApi.GetNewMatchData(accountDataGO.accountData, gameData.currentDeck.id);
        }
        serverApi.ExitQueue(accountDataGO.accountData);
        if (gameData.matchState.turnPlayerId == accountDataGO.accountData.id) {
            matchPlayer1.text = gameData.matchState.playerState.playerName;
            matchPlayer2.text = gameData.matchState.opponentState.playerName;
        } else {
            matchPlayer1.text = gameData.matchState.opponentState.playerName;
            matchPlayer2.text = gameData.matchState.playerState.playerName;
        }
        loadingMatch.SetActive(true);
        screenFade.Play();
        StartCoroutine(CheckForGameReady());
    }

    private IEnumerator CheckForGameReady() {
        MatchState tempMatchState = null;
        while (tempMatchState == null) {
            yield return new WaitForSeconds(1);
            tempMatchState = serverApi.GameReadyCheck(accountDataGO.accountData, gameData.matchState.matchId);
        }
        gameData.matchState = tempMatchState;
        yield return new WaitForSeconds(screenFade.GetClip("ScreenFade").length);
        yield return new WaitForSeconds(4);
        SceneManager.LoadScene("Game Scene");
    }

    public void NewDeck() {
        gameData.currentDeck = null;
        SceneManager.LoadScene("Deck Editor");
    }

    private IEnumerator CycleBackgrounds() {
        var alpha = bgB.color.a;
        while (bgIsCyclable) {
            foreach (var b in backgrounds) {
                yield return new WaitForSeconds(bgDuration);
                if (alpha > 0) {
                    bgA.sprite = b;
                    while (alpha > 0) {
                        alpha -= bgFadeSpeed * Time.deltaTime;
                    }
                }
                else {
                    bgB.sprite = b;
                    while (alpha < 1) {
                        alpha -= bgFadeSpeed * Time.deltaTime;
                    }
                }
            }
        }

    }

    private void DisplayAccountDecks() {
        accountDataGO.decks = serverApi.GetAccountDecks(accountDataGO.accountData);
        foreach (DeckData deckData in accountDataGO.decks) {
            GameObject newDeckDisplay = Instantiate(menuDeckDisplay, new Vector3(0, 0, 0), Quaternion.identity,
                createdDecks.transform);
            GameObject newEditorDeckDisplay = Instantiate(menuDeckDisplay, new Vector3(0, 0, 0), Quaternion.identity,
                createdDecksEditor.transform);
            newDeckDisplay.GetComponent<MenuDeckDisplay>().deckData = deckData;
            newDeckDisplay.GetComponentInChildren<TMP_Text>().text = deckData.deckName;
            newEditorDeckDisplay.GetComponent<MenuDeckDisplay>().deckData = deckData;
            newEditorDeckDisplay.GetComponentInChildren<TMP_Text>().text = deckData.deckName;
            Debug.Log("Deck updated: " + deckData.deckName);
        }

        gameData.accDataUpdated = true;
    }

    public void ToggleAreYouSure() {
        areYouSureDialogue.SetActive(!areYouSureDialogue.activeSelf);
    }

    public void DeleteDeck() {
        var rsp = serverApi.DeleteDeck(gameData.currentDeck.id, accountDataGO.accountData);
        Debug.Log("Delete request response code: " + rsp);
        Destroy(selectedDeckObj);
        gameData.currentDeck = null;
    }

    public void StartTestGame() {
        if (gameData.currentDeck == null || gameData.currentDeck.id == 0) {
            Debug.Log("Please select a deck first before starting a test game");
            return;
        }

        Debug.Log("Starting test game with deck: " + gameData.currentDeck.deckName);
        MatchState matchState = serverApi.CreateTestMatch(accountDataGO.accountData, gameData.currentDeck.id);

        if (matchState != null) {
            gameData.matchState = matchState;
            SceneManager.LoadScene("Game Scene");
        } else {
            Debug.Log("Failed to create test match");
        }
    }
}
    