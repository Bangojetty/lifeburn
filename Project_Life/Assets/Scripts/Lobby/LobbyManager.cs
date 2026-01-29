using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class LobbyManager : MonoBehaviour {
    [Header("References")]
    public MenuManager menuManager;

    // Found at runtime
    private AccountDataGO accountDataGO;
    private GameData gameData;

    [Header("Lobby Screen")]
    public GameObject lobbyScreen;
    public TMP_Text lobbyTitleText;
    public Transform playerSlotsContainer;
    public GameObject playerSlotPfb;

    [Header("Invited Players")]
    public Transform invitedPlayersContainer;
    public GameObject invitedPlayerPfb;

    [Header("Deck Selection")]
    public TMP_Dropdown deckDropdown;

    [Header("Ready/Start")]
    public Button readyStartButton;
    public TMP_Text readyStartButtonText;
    public Button leaveButton;
    public Button backButton;

    [Header("Lobby Settings Modal")]
    public Button openSettingsButton;
    public GameObject settingsModal;
    public Button settingsCloseButton;
    public TMP_Dropdown gameModeDropdown;      // Constructed / Draft
    public TMP_Dropdown gameStyleDropdown;     // Normal / Tournament

    [Header("Best Of Buttons")]
    public Button bestOf1Button;
    public Button bestOf3Button;
    public Button bestOf5Button;
    public GameObject bestOf1Selected;         // Visual indicator for selected state
    public GameObject bestOf3Selected;
    public GameObject bestOf5Selected;

    [Header("Lobby Indicator Bar")]
    public GameObject lobbyIndicatorBar;
    public TMP_Text lobbyIndicatorText;
    public Button returnToLobbyButton;


    [Header("Polling Settings")]
    public float lobbyPollInterval = 2f;

    private ServerApi serverApi = new();
    private LobbyData currentLobby;
    private int? currentLobbyId;
    private bool isInLobby;
    private List<DeckData> playerDecks = new();
    private Coroutine lobbyPollingCoroutine;

    void Start() {
        // Find runtime objects
        var accountDataObj = GameObject.Find("AccountData");
        if (accountDataObj != null) {
            accountDataGO = accountDataObj.GetComponent<AccountDataGO>();
        }

        var gameDataObj = GameObject.Find("GameData");
        if (gameDataObj != null) {
            gameData = gameDataObj.GetComponent<GameData>();
        }

        // Button listeners
        if (readyStartButton != null) readyStartButton.onClick.AddListener(OnReadyStartClicked);
        if (leaveButton != null) leaveButton.onClick.AddListener(OnLeaveClicked);
        if (backButton != null) backButton.onClick.AddListener(OnBackClicked);
        if (returnToLobbyButton != null) returnToLobbyButton.onClick.AddListener(ShowLobbyScreen);
        if (deckDropdown != null) deckDropdown.onValueChanged.AddListener(OnDeckSelected);

        // Settings modal listeners
        if (openSettingsButton != null) openSettingsButton.onClick.AddListener(OpenSettingsModal);
        if (settingsCloseButton != null) settingsCloseButton.onClick.AddListener(CloseSettingsModal);
        if (gameModeDropdown != null) {
            gameModeDropdown.ClearOptions();
            gameModeDropdown.AddOptions(new List<string> { "Constructed", "Draft" });
            gameModeDropdown.onValueChanged.AddListener(OnGameModeChanged);
        }
        if (gameStyleDropdown != null) {
            gameStyleDropdown.ClearOptions();
            gameStyleDropdown.AddOptions(new List<string> { "Normal", "Tournament" });
            gameStyleDropdown.onValueChanged.AddListener(OnGameStyleChanged);
        }

        // Best of buttons
        if (bestOf1Button != null) bestOf1Button.onClick.AddListener(() => OnBestOfClicked(1));
        if (bestOf3Button != null) bestOf3Button.onClick.AddListener(() => OnBestOfClicked(3));
        if (bestOf5Button != null) bestOf5Button.onClick.AddListener(() => OnBestOfClicked(5));

        // Hide lobby UI initially
        if (lobbyScreen != null) lobbyScreen.SetActive(false);
        if (lobbyIndicatorBar != null) lobbyIndicatorBar.SetActive(false);
    }

    void OnDestroy() {
        if (lobbyPollingCoroutine != null) StopCoroutine(lobbyPollingCoroutine);
    }

    #region Lobby Creation & Joining

    public void CreateLobby(string lobbyType = "normal", int maxPlayers = 2) {
        if (accountDataGO == null || accountDataGO.accountData == null) return;
        if (isInLobby) {
            Debug.Log("Already in a lobby");
            return;
        }

        var (statusCode, lobby) = serverApi.CreateLobby(accountDataGO.accountData, lobbyType, maxPlayers);
        if (statusCode == 201 && lobby != null) {
            EnterLobby(lobby);
        } else {
            Debug.Log($"Failed to create lobby: {statusCode}");
        }
    }

    public void CreateAndInviteFriend(int friendId, string lobbyType = "normal") {
        if (isInLobby && currentLobbyId.HasValue) {
            // Already in lobby, just send invite
            InviteFriendToLobby(friendId);
        } else {
            // Create new lobby then invite
            var (statusCode, lobby) = serverApi.CreateLobby(accountDataGO.accountData, lobbyType, 2);
            if (statusCode == 201 && lobby != null) {
                EnterLobby(lobby);
                InviteFriendToLobby(friendId);
            }
        }
    }

    public void InviteFriendToLobby(int friendId) {
        if (!currentLobbyId.HasValue) return;

        long result = serverApi.SendLobbyInvite(accountDataGO.accountData, currentLobbyId.Value, friendId);
        if (result == 201) {
            Debug.Log("Invite sent successfully");
        } else {
            Debug.Log($"Failed to send invite: {result}");
        }
    }

    private void EnterLobby(LobbyData lobby) {
        currentLobby = lobby;
        currentLobbyId = lobby.id;
        isInLobby = true;

        // Load player decks for dropdown
        LoadPlayerDecks();

        // Start polling
        if (lobbyPollingCoroutine != null) StopCoroutine(lobbyPollingCoroutine);
        lobbyPollingCoroutine = StartCoroutine(PollLobbyState());

        // Show lobby UI
        UpdateLobbyUI();
        ShowLobbyScreen();
        // Indicator bar will show when user navigates away from lobby screen
    }

    #endregion

    #region Lobby UI

    public void ShowLobbyScreen() {
        if (lobbyScreen != null) lobbyScreen.SetActive(true);
        if (lobbyIndicatorBar != null) lobbyIndicatorBar.SetActive(false);
        if (menuManager != null) {
            menuManager.mainMenu.SetActive(false);
            menuManager.deckSelect.SetActive(false);
            menuManager.deckSelectEditor.SetActive(false);
        }
    }

    public void HideLobbyScreen() {
        if (lobbyScreen != null) lobbyScreen.SetActive(false);
        if (isInLobby && lobbyIndicatorBar != null) lobbyIndicatorBar.SetActive(true);
    }

    private void OnBackClicked() {
        HideLobbyScreen();
        if (menuManager != null) {
            menuManager.ToMainMenu();
        }
    }

    private void UpdateLobbyUI() {
        if (currentLobby == null) return;

        // Update title
        if (lobbyTitleText != null) {
            string gameStyle = currentLobby.lobbyType == "normal" ? "Normal" : "Tournament";
            string gameMode = currentLobby.lobbySubtype == "constructed" ? "Constructed" : "Draft";
            lobbyTitleText.text = $"{currentLobby.hostDisplayName}'s {gameStyle} {gameMode}";
        }

        // Update player slots
        UpdatePlayerSlots();

        // Update invited players
        UpdateInvitedPlayers();

        // Update deck dropdown state
        UpdateDeckDropdownState();

        // Update buttons based on state
        UpdateButtonStates();
    }

    private Dictionary<int, LobbyPlayerSlot> playerSlots = new Dictionary<int, LobbyPlayerSlot>();

    private void UpdatePlayerSlots() {
        if (playerSlotsContainer == null || playerSlotPfb == null) return;

        // Track which players are still in lobby
        HashSet<int> currentPlayerIds = new HashSet<int>();
        foreach (var player in currentLobby.players) {
            currentPlayerIds.Add(player.playerId);
        }

        // Remove slots for players who left
        List<int> toRemove = new List<int>();
        foreach (var kvp in playerSlots) {
            if (!currentPlayerIds.Contains(kvp.Key)) {
                Destroy(kvp.Value.gameObject);
                toRemove.Add(kvp.Key);
            }
        }
        foreach (int id in toRemove) {
            playerSlots.Remove(id);
        }

        // Update existing or create new slots
        foreach (var player in currentLobby.players) {
            bool isMe = player.playerId == accountDataGO.accountData.id;
            bool isHost = currentLobby.hostId == accountDataGO.accountData.id;

            if (playerSlots.TryGetValue(player.playerId, out var existingSlot)) {
                // Update existing slot
                existingSlot.Setup(player, isMe, isHost, this);
            } else {
                // Create new slot
                GameObject slot = Instantiate(playerSlotPfb, playerSlotsContainer);
                var slotUI = slot.GetComponent<LobbyPlayerSlot>();
                if (slotUI != null) {
                    slotUI.Setup(player, isMe, isHost, this);
                    playerSlots[player.playerId] = slotUI;
                }
            }
        }
    }

    private Dictionary<int, InvitedPlayerItem> invitedPlayerItems = new Dictionary<int, InvitedPlayerItem>();

    private void UpdateInvitedPlayers() {
        if (invitedPlayersContainer == null || invitedPlayerPfb == null) return;
        if (currentLobby.pendingInvites == null) return;

        // Track which invites are still pending
        HashSet<int> currentInviteIds = new HashSet<int>();
        foreach (var invite in currentLobby.pendingInvites) {
            currentInviteIds.Add(invite.inviteeId);
        }

        // Remove items for invites that are no longer pending
        List<int> toRemove = new List<int>();
        foreach (var kvp in invitedPlayerItems) {
            if (!currentInviteIds.Contains(kvp.Key)) {
                Destroy(kvp.Value.gameObject);
                toRemove.Add(kvp.Key);
            }
        }
        foreach (int id in toRemove) {
            invitedPlayerItems.Remove(id);
        }

        // Update existing or create new items
        foreach (var invite in currentLobby.pendingInvites) {
            if (invitedPlayerItems.TryGetValue(invite.inviteeId, out var existingItem)) {
                // Update existing item
                existingItem.Setup(invite.inviteeDisplayName);
            } else {
                // Create new item
                GameObject item = Instantiate(invitedPlayerPfb, invitedPlayersContainer);
                var itemUI = item.GetComponent<InvitedPlayerItem>();
                if (itemUI != null) {
                    itemUI.Setup(invite.inviteeDisplayName);
                    invitedPlayerItems[invite.inviteeId] = itemUI;
                }
            }
        }
    }

    private void UpdateButtonStates() {
        if (currentLobby == null) return;

        bool isHost = currentLobby.hostId == accountDataGO.accountData.id;
        var myPlayer = currentLobby.players.Find(p => p.playerId == accountDataGO.accountData.id);

        if (readyStartButton != null && readyStartButtonText != null) {
            if (isHost) {
                // Host sees "Start" button
                readyStartButtonText.text = "Start";
                readyStartButton.interactable = CanStartGame();
            } else {
                // Non-host sees "Ready/Unready" button, always interactable
                readyStartButtonText.text = myPlayer?.isReady == true ? "Unready" : "Ready";
                readyStartButton.interactable = true;
            }
        }
    }

    private bool CanStartGame() {
        if (currentLobby == null) return false;

        int playerCount = currentLobby.players.Count;
        bool isNormal = currentLobby.lobbyType == "normal";
        bool isTournament = currentLobby.lobbyType == "tournament";
        bool isConstructed = currentLobby.lobbySubtype == "constructed";
        bool isDraft = currentLobby.lobbySubtype == "draft";

        // Check all non-host players are ready
        bool allOthersReady = currentLobby.players.Where(p => !p.isHost).All(p => p.isReady);
        if (!allOthersReady) return false;

        // Check player count requirements
        if (isNormal) {
            // Normal: exactly 2 players
            if (playerCount != 2) return false;
        } else if (isTournament) {
            // Tournament: even number, 2-16 players
            if (playerCount < 2 || playerCount > 16 || playerCount % 2 != 0) return false;
        }

        // Check deck requirements for constructed mode
        if (isConstructed) {
            // All players must have a deck selected
            bool allHaveDecks = currentLobby.players.All(p => p.deckId != null && p.deckId > 0);
            if (!allHaveDecks) return false;
        }

        return true;
    }

    private void UpdateIndicatorBar() {
        if (lobbyIndicatorBar == null) return;

        // Only show indicator bar if in lobby AND not on the lobby screen
        bool shouldShow = isInLobby && (lobbyScreen == null || !lobbyScreen.activeSelf);
        lobbyIndicatorBar.SetActive(shouldShow);

        if (lobbyIndicatorText != null && currentLobby != null) {
            string gameStyle = currentLobby.lobbyType == "normal" ? "Normal" : "Tournament";
            string gameMode = currentLobby.lobbySubtype == "constructed" ? "Constructed" : "Draft";
            lobbyIndicatorText.text = $"{currentLobby.hostDisplayName}'s {gameStyle} {gameMode} ({currentLobby.players.Count}/{currentLobby.maxPlayers})";
        }
    }

    #endregion

    #region Deck Selection

    private void LoadPlayerDecks() {
        if (accountDataGO == null) return;

        playerDecks = serverApi.GetAccountDecks(accountDataGO.accountData) ?? new List<DeckData>();

        if (deckDropdown != null) {
            deckDropdown.ClearOptions();
            var options = new List<TMP_Dropdown.OptionData>();
            options.Add(new TMP_Dropdown.OptionData("Select a deck..."));
            foreach (var deck in playerDecks) {
                options.Add(new TMP_Dropdown.OptionData(deck.deckName));
            }
            deckDropdown.AddOptions(options);
        }
    }

    private void OnDeckSelected(int selectedIndex) {
        if (!currentLobbyId.HasValue) return;

        int deckIndex = selectedIndex - 1; // -1 because first option is placeholder
        if (deckIndex < 0 || deckIndex >= playerDecks.Count) return;

        int deckId = playerDecks[deckIndex].id;
        long result = serverApi.SelectLobbyDeck(accountDataGO.accountData, currentLobbyId.Value, deckId);
        if (result == 200) {
            Debug.Log("Deck selected successfully");
        }
    }

    #endregion

    #region Ready/Start

    private void OnReadyStartClicked() {
        if (!currentLobbyId.HasValue || currentLobby == null) return;

        bool isHost = currentLobby.hostId == accountDataGO.accountData.id;

        if (isHost) {
            bool isDraft = currentLobby.lobbySubtype == "draft";

            if (isDraft) {
                // Host starts a draft
                var (statusCode, draftState) = serverApi.StartDraft(accountDataGO.accountData, currentLobbyId.Value);
                if (statusCode == 201 && draftState != null) {
                    StartDraftTransition(draftState);
                } else {
                    Debug.Log($"Failed to start draft: {statusCode}");
                }
            } else {
                // Host starts constructed game
                var (statusCode, matchId) = serverApi.StartLobbyGame(accountDataGO.accountData, currentLobbyId.Value);
                if (statusCode == 200 && matchId.HasValue) {
                    StartGameTransition(matchId.Value);
                } else {
                    Debug.Log($"Failed to start game: {statusCode}");
                }
            }
        } else {
            // Non-host clicks to toggle ready
            var (statusCode, lobby) = serverApi.ToggleLobbyReady(accountDataGO.accountData, currentLobbyId.Value);
            if (statusCode == 200 && lobby != null) {
                currentLobby = lobby;
                UpdateLobbyUI();
            }
        }
    }

    private void StartGameTransition(int matchId) {
        // Stop polling
        if (lobbyPollingCoroutine != null) StopCoroutine(lobbyPollingCoroutine);

        // Store player names from lobby before clearing (for loading screen)
        // Don't call GetMatchState here - it clears events on the server!
        // GameManager will fetch the first MatchState and get the events.
        string player1Name = "";
        string player2Name = "";
        if (currentLobby != null && currentLobby.players.Count >= 2) {
            player1Name = currentLobby.players[0].displayName;
            player2Name = currentLobby.players[1].displayName;
        }

        // Store matchId for GameManager to use
        if (gameData != null) {
            gameData.lobbyMatchId = matchId;
        }

        // Clean up lobby state
        currentLobby = null;
        currentLobbyId = null;
        isInLobby = false;

        if (lobbyIndicatorBar != null) lobbyIndicatorBar.SetActive(false);
        if (lobbyScreen != null) lobbyScreen.SetActive(false);

        // Start the loading screen transition (like matchmaking does)
        StartCoroutine(LoadingScreenTransition(player1Name, player2Name));
    }

    private IEnumerator LoadingScreenTransition(string player1Name, string player2Name) {
        Debug.Log($"[LobbyManager] LoadingScreenTransition started. menuManager={menuManager != null}");

        // Disable friends sidebar
        if (menuManager != null) {
            if (menuManager.friendsSidebar != null) {
                Debug.Log("[LobbyManager] Disabling friendsSidebar");
                menuManager.friendsSidebar.SetActive(false);
            } else {
                Debug.Log("[LobbyManager] friendsSidebar is null!");
            }

            // Also hide main menu elements
            if (menuManager.mainMenu != null) menuManager.mainMenu.SetActive(false);
            if (menuManager.deckSelect != null) menuManager.deckSelect.SetActive(false);

            Debug.Log($"[LobbyManager] loadingMatch={menuManager.loadingMatch != null}, screenFade={menuManager.screenFade != null}");
            Debug.Log($"[LobbyManager] player1Name={player1Name}, player2Name={player2Name}");

            // Set player names on loading screen
            if (menuManager.matchPlayer1 != null) menuManager.matchPlayer1.text = player1Name;
            if (menuManager.matchPlayer2 != null) menuManager.matchPlayer2.text = player2Name;

            // Show loading screen and play animation
            if (menuManager.loadingMatch != null) {
                Debug.Log("[LobbyManager] Activating loadingMatch");
                menuManager.loadingMatch.SetActive(true);

                if (menuManager.screenFade != null) {
                    menuManager.screenFade.Play();
                    yield return new WaitForSeconds(menuManager.screenFade.GetClip("ScreenFade").length);
                }
                yield return new WaitForSeconds(4);
            } else {
                Debug.Log("[LobbyManager] loadingMatch is null, waiting 2 seconds before loading scene");
                yield return new WaitForSeconds(2);
            }
        } else {
            Debug.Log("[LobbyManager] menuManager is null!");
            yield return new WaitForSeconds(2);
        }

        // Load game scene
        Debug.Log("[LobbyManager] Loading Game Scene");
        SceneManager.LoadScene("Game Scene");
    }

    private void StartDraftTransition(DraftDisplayState draftState) {
        // Stop polling
        if (lobbyPollingCoroutine != null) StopCoroutine(lobbyPollingCoroutine);

        // Store draft state in GameData
        if (gameData != null) {
            gameData.draftId = draftState.draftId;
            gameData.draftState = draftState;
        }

        // Clean up lobby state
        currentLobby = null;
        currentLobbyId = null;
        isInLobby = false;

        if (lobbyIndicatorBar != null) lobbyIndicatorBar.SetActive(false);
        if (lobbyScreen != null) lobbyScreen.SetActive(false);

        // Disable friends sidebar
        if (menuManager?.friendsSidebar != null) {
            menuManager.friendsSidebar.SetActive(false);
        }

        // Load Draft Scene
        Debug.Log("[LobbyManager] Loading Draft Scene");
        SceneManager.LoadScene("Draft Scene");
    }

    #endregion

    #region Leave/Kick

    private void OnLeaveClicked() {
        if (!currentLobbyId.HasValue) return;

        long result = serverApi.LeaveLobby(accountDataGO.accountData, currentLobbyId.Value);
        ExitLobby();
    }

    public void KickPlayer(int playerId) {
        if (!currentLobbyId.HasValue) return;

        long result = serverApi.KickPlayer(accountDataGO.accountData, currentLobbyId.Value, playerId);
        if (result == 200) {
            Debug.Log("Player kicked");
        }
    }

    private void ExitLobby() {
        if (lobbyPollingCoroutine != null) StopCoroutine(lobbyPollingCoroutine);

        currentLobby = null;
        currentLobbyId = null;
        isInLobby = false;

        // Clear player slots
        foreach (var slot in playerSlots.Values) {
            if (slot != null) Destroy(slot.gameObject);
        }
        playerSlots.Clear();

        // Clear invited player items
        foreach (var item in invitedPlayerItems.Values) {
            if (item != null) Destroy(item.gameObject);
        }
        invitedPlayerItems.Clear();

        HideLobbyScreen();
        if (lobbyIndicatorBar != null) lobbyIndicatorBar.SetActive(false);

        // Return to main menu
        if (menuManager != null) {
            menuManager.ToMainMenu();
        }
    }

    #endregion

    #region Settings Modal

    private void OpenSettingsModal() {
        if (settingsModal != null) {
            settingsModal.SetActive(true);
            UpdateSettingsModalUI();
        }
    }

    private void CloseSettingsModal() {
        if (settingsModal != null) settingsModal.SetActive(false);
    }

    private void UpdateSettingsModalUI() {
        if (currentLobby == null) return;

        bool isHost = currentLobby.hostId == accountDataGO.accountData.id;
        Debug.Log($"UpdateSettingsModalUI: hostId={currentLobby.hostId}, myId={accountDataGO.accountData.id}, isHost={isHost}");

        // Only host can change settings
        if (gameModeDropdown != null) gameModeDropdown.interactable = isHost;
        if (gameStyleDropdown != null) gameStyleDropdown.interactable = isHost;

        // Best of buttons - only host can click
        if (bestOf1Button != null) bestOf1Button.interactable = isHost;
        if (bestOf3Button != null) bestOf3Button.interactable = isHost;
        if (bestOf5Button != null) bestOf5Button.interactable = isHost;

        // Set game mode dropdown (0 = Constructed, 1 = Draft)
        if (gameModeDropdown != null) {
            gameModeDropdown.SetValueWithoutNotify(currentLobby.lobbySubtype == "constructed" ? 0 : 1);
        }

        // Set game style dropdown (0 = Normal, 1 = Tournament)
        if (gameStyleDropdown != null) {
            gameStyleDropdown.SetValueWithoutNotify(currentLobby.lobbyType == "normal" ? 0 : 1);
        }

        // Update best of button visual states
        UpdateBestOfButtonStates();
    }

    private void UpdateBestOfButtonStates() {
        int bestOf = currentLobby?.bestOf ?? 1;

        // Show/hide selected indicators
        if (bestOf1Selected != null) bestOf1Selected.SetActive(bestOf == 1);
        if (bestOf3Selected != null) bestOf3Selected.SetActive(bestOf == 3);
        if (bestOf5Selected != null) bestOf5Selected.SetActive(bestOf == 5);
    }

    private void OnGameModeChanged(int index) {
        if (!currentLobbyId.HasValue || currentLobby == null) return;
        if (currentLobby.hostId != accountDataGO.accountData.id) return; // Only host

        string gameMode = index == 0 ? "constructed" : "draft";
        long result = serverApi.UpdateLobbySettings(accountDataGO.accountData, currentLobbyId.Value, currentLobby.lobbyType, gameMode);
        if (result == 200) {
            currentLobby.lobbySubtype = gameMode;
            UpdateDeckDropdownState();
            UpdateLobbyUI();
            UpdateIndicatorBar();
        }
    }

    private void OnGameStyleChanged(int index) {
        if (!currentLobbyId.HasValue || currentLobby == null) return;
        if (currentLobby.hostId != accountDataGO.accountData.id) return; // Only host

        string gameStyle = index == 0 ? "normal" : "tournament";
        long result = serverApi.UpdateLobbySettings(accountDataGO.accountData, currentLobbyId.Value, gameStyle, currentLobby.lobbySubtype);
        if (result == 200) {
            currentLobby.lobbyType = gameStyle;
            UpdateLobbyUI();
            UpdateIndicatorBar();
        }
    }

    private void OnBestOfClicked(int bestOf) {
        if (!currentLobbyId.HasValue || currentLobby == null) return;
        if (currentLobby.hostId != accountDataGO.accountData.id) return; // Only host

        // Don't do anything if already selected
        if (currentLobby.bestOf == bestOf) return;

        long result = serverApi.UpdateLobbyBestOf(accountDataGO.accountData, currentLobbyId.Value, bestOf);
        if (result == 200) {
            currentLobby.bestOf = bestOf;
            UpdateBestOfButtonStates();
            UpdateLobbyUI();
        }
    }

    private void UpdateDeckDropdownState() {
        if (deckDropdown == null || currentLobby == null) return;
        // Disable deck dropdown if draft mode
        deckDropdown.interactable = currentLobby.lobbySubtype == "constructed";
    }

    #endregion

    #region Polling

    private IEnumerator PollLobbyState() {
        while (isInLobby && currentLobbyId.HasValue) {
            yield return new WaitForSeconds(lobbyPollInterval);

            var lobby = serverApi.GetLobby(accountDataGO.accountData, currentLobbyId.Value);
            if (lobby == null) {
                // Lobby no longer exists
                ExitLobby();
                yield break;
            }

            // Check if draft started
            if (lobby.draftId.HasValue) {
                // Fetch draft state and transition to Draft Scene
                var draftState = serverApi.GetDraftState(accountDataGO.accountData, lobby.draftId.Value);
                if (draftState != null) {
                    StartDraftTransition(draftState);
                    yield break;
                }
            }

            // Check if game started (constructed)
            if (lobby.status == "in_progress" && lobby.matchId.HasValue) {
                StartGameTransition(lobby.matchId.Value);
                yield break;
            }

            // Check if we were kicked
            var myPlayer = lobby.players.Find(p => p.playerId == accountDataGO.accountData.id);
            if (myPlayer == null || myPlayer.status == "kicked") {
                ExitLobby();
                yield break;
            }

            currentLobby = lobby;
            UpdateLobbyUI();
            UpdateIndicatorBar();
        }
    }

    #endregion

    #region Invite Handling

    public void AcceptInvite(int inviteId) {
        if (isInLobby) {
            Debug.Log("Already in a lobby - leave first");
            return;
        }

        var (statusCode, lobby) = serverApi.AcceptLobbyInvite(accountDataGO.accountData, inviteId);
        if (statusCode == 200 && lobby != null) {
            EnterLobby(lobby);
        } else {
            Debug.Log($"Failed to accept invite: {statusCode}");
        }
    }

    public void DeclineInvite(int inviteId) {
        serverApi.DeclineLobbyInvite(accountDataGO.accountData, inviteId);
    }

    #endregion

    #region Public Accessors

    public bool IsInLobby => isInLobby;
    public int? CurrentLobbyId => currentLobbyId;

    #endregion
}
