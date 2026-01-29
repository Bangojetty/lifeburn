using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class DraftManager : MonoBehaviour {
    [Header("References")]
    public GameData gameData;
    public AccountDataGO accountDataGO;

    [Header("Pack Selection")]
    public GameObject packSelectionView;        // Container shown before draft starts
    public Transform packContainer;             // Parent transform for instantiated packs
    public GameObject packPrefab;               // Pack prefab to instantiate
    private List<GameObject> instantiatedPacks = new();

    [Header("Pack Cards Display")]
    public GameObject packCardsView;            // Container for cards (shown after pack opened)
    public Transform packCardsContainer;
    public ScrollRect packScrollRect;

    [Header("Selected Card Preview")]
    public Transform selectedCardPreviewContainer;  // Parent transform for the preview card
    private GameObject previewCardInstance;
    private CardDisplay previewCardDisplay;

    [Header("UI Elements")]
    public TMP_Text progressText;
    public TMP_Text roundText;
    public GameObject passLeftArrow;
    public GameObject passRightArrow;
    public TMP_Text opponentStatusText;
    public Button submitPickButton;

    [Header("Drafted Cards Panel")]
    public GameObject draftedCardsButton;       // Face-down card button to open panel
    public GameObject draftedCardsPanel;        // Panel that opens with scrollview
    public Transform draftedCardsContainer;     // Container inside the scrollview
    public Button closeDraftedPanelButton;
    public Button draftedPanelBackground;       // Background blocker for click-outside-to-close

    [Header("Polling")]
    public float pollInterval = 2f;

    [Header("Card Details")]
    public GameObject cardDetailsPanel;           // Large card display panel
    public Transform cardDetailsContainer;        // Container for the large card
    public Button closeCardDetailsButton;
    public Button cardDetailsPanelBackground;     // Background blocker for click-outside-to-close
    private GameObject cardDetailsInstance;

    private ServerApi serverApi = new();
    private DraftDisplayState currentDraftState;
    private int? selectedCardId;
    private DraftCardItem selectedCardItem;       // Track the actual selected card instance
    private List<DraftCardItem> packCardItems = new();
    private List<GameObject> draftedCardObjects = new();
    private List<int> lastDisplayedPackCards = new();  // Track last displayed pack to avoid unnecessary refreshes
    private Coroutine pollingCoroutine;
    private bool isSubmitting;
    private bool packOpenedThisRound;             // Has player opened a pack for the current round?
    private int lastOpenedRound = -1;             // Track which round was last opened
    private HashSet<int> openedRounds = new();    // Track which rounds have been opened

    void Start() {
        // Find runtime objects if not assigned
        if (gameData == null) {
            var gameDataObj = GameObject.Find("GameData");
            if (gameDataObj != null) gameData = gameDataObj.GetComponent<GameData>();
        }
        if (accountDataGO == null) {
            var accountDataObj = GameObject.Find("AccountData");
            if (accountDataObj != null) accountDataGO = accountDataObj.GetComponent<AccountDataGO>();
        }

        // Setup button listeners
        if (submitPickButton != null) {
            submitPickButton.onClick.AddListener(OnSubmitPickClicked);
        }
        if (draftedCardsButton != null) {
            draftedCardsButton.GetComponent<Button>()?.onClick.AddListener(OpenDraftedCardsPanel);
        }
        if (closeDraftedPanelButton != null) {
            closeDraftedPanelButton.onClick.AddListener(CloseDraftedCardsPanel);
        }
        if (draftedPanelBackground != null) {
            draftedPanelBackground.onClick.AddListener(CloseDraftedCardsPanel);
        }
        if (closeCardDetailsButton != null) {
            closeCardDetailsButton.onClick.AddListener(CloseCardDetails);
        }
        if (cardDetailsPanelBackground != null) {
            cardDetailsPanelBackground.onClick.AddListener(CloseCardDetails);
        }

        // Hide panels initially
        if (draftedCardsPanel != null) {
            draftedCardsPanel.SetActive(false);
        }
        if (packCardsView != null) {
            packCardsView.SetActive(false);
        }
        if (cardDetailsPanel != null) {
            cardDetailsPanel.SetActive(false);
        }
        // Hide drafted cards button until first card is drafted
        if (draftedCardsButton != null) {
            draftedCardsButton.SetActive(false);
        }


        // Load initial draft state from GameData
        if (gameData?.draftState != null) {
            currentDraftState = gameData.draftState;
            UpdateUI();
        } else if (gameData?.draftId != null) {
            FetchDraftState();
        }

        // Start polling
        pollingCoroutine = StartCoroutine(PollDraftState());
    }

    void Update() {
        // Handle Escape key to close panels
        if (Input.GetKeyDown(KeyCode.Escape)) {
            // Close card details first if open
            if (cardDetailsPanel != null && cardDetailsPanel.activeSelf) {
                CloseCardDetails();
                return;
            }
            // Then close drafted cards panel if open
            if (draftedCardsPanel != null && draftedCardsPanel.activeSelf) {
                CloseDraftedCardsPanel();
                return;
            }
        }

        // DEBUG: Press F5 to skip drafting and go straight to deck building with random cards
        if (Input.GetKeyDown(KeyCode.F5)) {
            SkipDraftingDebug();
        }
    }

    private void SkipDraftingDebug() {
        if (accountDataGO?.accountData == null || gameData?.draftId == null) {
            Debug.LogWarning("[DraftManager] DEBUG: Cannot skip - missing account or draft data");
            return;
        }

        Debug.Log("[DraftManager] DEBUG: Requesting skip to deck building from server...");

        var (statusCode, draftState) = serverApi.SkipToDeckBuilding(accountDataGO.accountData, gameData.draftId.Value);

        if (statusCode == 200 && draftState != null) {
            Debug.Log($"[DraftManager] DEBUG: Server returned {draftState.myDraftedCards?.Count ?? 0} drafted cards");

            // Update game data
            gameData.draftState = draftState;
            gameData.draftedCardPool = new List<int>(draftState.myDraftedCards ?? new List<int>());
            gameData.isDraftDeckBuilding = true;

            // Stop polling
            if (pollingCoroutine != null) StopCoroutine(pollingCoroutine);

            SceneManager.LoadScene("Deck Editor");
        } else {
            Debug.LogError($"[DraftManager] DEBUG: Skip failed with status {statusCode}");
        }
    }

    void OnDestroy() {
        if (pollingCoroutine != null) StopCoroutine(pollingCoroutine);
    }

    private void FetchDraftState() {
        if (accountDataGO?.accountData == null || gameData?.draftId == null) return;

        var state = serverApi.GetDraftState(accountDataGO.accountData, gameData.draftId.Value);
        if (state != null) {
            currentDraftState = state;
            gameData.draftState = state;
            UpdateUI();
        }
    }

    private IEnumerator PollDraftState() {
        while (true) {
            yield return new WaitForSeconds(pollInterval);

            if (accountDataGO?.accountData == null || gameData?.draftId == null) continue;

            var state = serverApi.GetDraftState(accountDataGO.accountData, gameData.draftId.Value);
            if (state == null) continue;

            // Check for state changes
            bool roundChanged = currentDraftState == null || state.currentRound != currentDraftState.currentRound;
            bool pickChanged = currentDraftState == null || state.currentPick != currentDraftState.currentPick;

            currentDraftState = state;
            gameData.draftState = state;

            // Check for status change
            if (state.status == "deck_building") {
                TransitionToDeckBuilding();
                yield break;
            }

            if (state.status == "ready" && state.matchId.HasValue) {
                TransitionToGame(state.matchId.Value);
                yield break;
            }

            // If round changed, reset to pack selection for this new round
            if (roundChanged) {
                // Always reset pack opened state for new round
                packOpenedThisRound = openedRounds.Contains(state.currentRound);
                selectedCardId = null;
                selectedCardItem = null;

                // Clear pack cards from previous round
                ClearPackCards();

                // Clear instantiated packs so they get recreated for pack selection
                foreach (var pack in instantiatedPacks) {
                    if (pack != null) Destroy(pack);
                }
                instantiatedPacks.Clear();

                UpdateSelectedCardPreview();
            }

            // If pick changed (new pick available), reset selection
            if (pickChanged) {
                selectedCardId = null;
                selectedCardItem = null;
                UpdateSelectedCardPreview();
            }

            // Update UI
            UpdateUI();
        }
    }

    private void UpdateUI() {
        if (currentDraftState == null) return;

        Debug.Log($"[DraftManager] UpdateUI - Round: {currentDraftState.currentRound}, Pick: {currentDraftState.currentPick}, " +
                  $"packOpenedThisRound: {packOpenedThisRound}, iHaveSubmitted: {currentDraftState.iHaveSubmitted}, " +
                  $"currentPackCards count: {currentDraftState.currentPackCards?.Count ?? 0}");

        // Update progress text (cards drafted / 60)
        int draftedCount = currentDraftState.myDraftedCards?.Count ?? 0;
        if (progressText != null) {
            progressText.text = $"Drafted: {draftedCount}/60";
        }

        // Show drafted cards button (facedown card) once cards have been drafted
        if (draftedCardsButton != null) {
            draftedCardsButton.SetActive(draftedCount > 0);
        }

        // Update round text
        if (roundText != null) {
            roundText.text = $"Round {currentDraftState.currentRound + 1}/4";
        }

        // Update pass direction arrows
        bool passingLeft = currentDraftState.passDirection == 1;
        if (passLeftArrow != null) passLeftArrow.SetActive(passingLeft);
        if (passRightArrow != null) passRightArrow.SetActive(!passingLeft);

        // Update opponent status
        if (opponentStatusText != null) {
            if (currentDraftState.opponentHasSubmitted) {
                opponentStatusText.text = "Opponent: Submitted";
                opponentStatusText.color = Color.green;
            } else {
                opponentStatusText.text = "Opponent: Picking...";
                opponentStatusText.color = Color.yellow;
            }
        }

        // Update submit button (just toggle interactability)
        if (submitPickButton != null) {
            submitPickButton.interactable = selectedCardId.HasValue && !isSubmitting && !currentDraftState.iHaveSubmitted;
        }

        // Show pack selection when pack hasn't been opened for this round
        bool showPackSelection = !packOpenedThisRound && !currentDraftState.iHaveSubmitted;

        if (packSelectionView != null) {
            packSelectionView.SetActive(showPackSelection);
        }
        if (packCardsView != null) {
            packCardsView.SetActive(!showPackSelection && !currentDraftState.iHaveSubmitted);
        }

        // Instantiate packs if showing pack selection and none created yet
        if (showPackSelection && instantiatedPacks.Count == 0) {
            InstantiatePacks();
        }

        // Update pack cards display (only if pack has been opened this round)
        if (packOpenedThisRound) {
            UpdatePackCardsDisplay();
        }
    }

    private void InstantiatePacks() {
        if (packContainer == null || packPrefab == null || currentDraftState == null) return;

        // Clear any existing packs
        foreach (var pack in instantiatedPacks) {
            if (pack != null) Destroy(pack);
        }
        instantiatedPacks.Clear();

        // Calculate remaining packs (total packs minus opened rounds)
        int totalPacks = currentDraftState.myPackCount > 0 ? currentDraftState.myPackCount : 4;
        int remainingPacks = totalPacks - openedRounds.Count;

        // Always show at least one pack if we're in pack selection mode
        if (remainingPacks <= 0) remainingPacks = 1;

        for (int i = 0; i < remainingPacks; i++) {
            GameObject packObj = Instantiate(packPrefab, packContainer);

            // Add click listener
            var button = packObj.GetComponent<Button>();
            if (button != null) {
                button.onClick.AddListener(OnPackClicked);
            }

            instantiatedPacks.Add(packObj);
        }
    }

    private void OnPackClicked() {
        if (currentDraftState == null) return;

        Debug.Log($"[DraftManager] OnPackClicked - Round: {currentDraftState.currentRound}, packOpenedThisRound: {packOpenedThisRound}, " +
                  $"currentPackCards: {string.Join(",", currentDraftState.currentPackCards ?? new List<int>())}");

        // Opening a pack for this round
        if (!packOpenedThisRound) {
            packOpenedThisRound = true;
            openedRounds.Add(currentDraftState.currentRound);

            // Clear instantiated packs since we're transitioning to card view
            foreach (var pack in instantiatedPacks) {
                if (pack != null) Destroy(pack);
            }
            instantiatedPacks.Clear();

            UpdateUI();
        }
    }

    private void UpdatePackCardsDisplay() {
        if (packCardsContainer == null || gameData?.cardTemplatePfb == null) {
            Debug.Log("[DraftManager] UpdatePackCardsDisplay - EARLY EXIT: container or prefab null");
            return;
        }

        // Don't show cards if already submitted
        if (currentDraftState.iHaveSubmitted) {
            Debug.Log("[DraftManager] UpdatePackCardsDisplay - EARLY EXIT: already submitted");
            ClearPackCards();
            return;
        }

        var packCards = currentDraftState.currentPackCards ?? new List<int>();
        Debug.Log($"[DraftManager] UpdatePackCardsDisplay - packCards: [{string.Join(",", packCards)}], " +
                  $"lastDisplayedPackCards: [{string.Join(",", lastDisplayedPackCards)}]");

        // Check if pack has actually changed - avoid unnecessary refresh
        if (PackCardsMatch(packCards)) {
            Debug.Log("[DraftManager] UpdatePackCardsDisplay - EARLY EXIT: pack cards match (no change)");
            return;
        }

        Debug.Log($"[DraftManager] UpdatePackCardsDisplay - Creating {packCards.Count} card displays");

        // Clear existing cards
        ClearPackCards();

        // Create cards for current pack using existing card prefab
        foreach (int cardId in packCards) {
            // Instantiate the existing card prefab
            GameObject cardObj = Instantiate(gameData.cardTemplatePfb, packCardsContainer);

            // Add DraftCardItem component for draft selection behavior
            var draftItem = cardObj.AddComponent<DraftCardItem>();
            draftItem.Setup(cardId, this, gameData);
            packCardItems.Add(draftItem);
        }

        // Remember what we displayed
        lastDisplayedPackCards = new List<int>(packCards);
    }

    private bool PackCardsMatch(List<int> packCards) {
        if (packCards.Count != lastDisplayedPackCards.Count) return false;
        for (int i = 0; i < packCards.Count; i++) {
            if (packCards[i] != lastDisplayedPackCards[i]) return false;
        }
        return true;
    }

    private void ClearPackCards() {
        foreach (var item in packCardItems) {
            if (item != null) Destroy(item.gameObject);
        }
        packCardItems.Clear();
        lastDisplayedPackCards.Clear();
    }

    private void UpdateDraftedCardsPanel() {
        if (draftedCardsContainer == null || gameData?.cardTemplatePfb == null) return;

        // Clear existing
        foreach (var obj in draftedCardObjects) {
            if (obj != null) Destroy(obj);
        }
        draftedCardObjects.Clear();

        // Create cards for drafted cards
        var draftedCards = currentDraftState?.myDraftedCards ?? new List<int>();
        foreach (int cardId in draftedCards) {
            if (!gameData.allCardsDict.TryGetValue(cardId, out var cardData)) continue;

            GameObject cardObj = Instantiate(gameData.cardTemplatePfb, draftedCardsContainer);
            var cardDisplay = cardObj.GetComponent<CardDisplay>();
            if (cardDisplay != null) {
                // Explicitly set gameData reference (in case Awake didn't set it properly)
                cardDisplay.gameData = gameData;

                // Disable in-game interaction components
                if (cardDisplay.interactableObj != null) cardDisplay.interactableObj.SetActive(false);
                if (cardDisplay.selectableCardObj != null) cardDisplay.selectableCardObj.SetActive(false);
                if (cardDisplay.attackCapableObj != null) cardDisplay.attackCapableObj.SetActive(false);
                if (cardDisplay.attackableObj != null) cardDisplay.attackableObj.SetActive(false);
                if (cardDisplay.selectableTargetObj != null) cardDisplay.selectableTargetObj.SetActive(false);
                if (cardDisplay.targetingLocationObj != null) cardDisplay.targetingLocationObj.SetActive(false);

                cardDisplay.UpdateCardDisplayData(cardData, trackInUidToObj: false);

                // Add right-click handler for card details
                var clickHandler = cardObj.AddComponent<DraftedCardClickHandler>();
                clickHandler.Setup(cardData, this);
            }
            draftedCardObjects.Add(cardObj);
        }
    }

    public void SelectCard(DraftCardItem cardItem) {
        if (currentDraftState.iHaveSubmitted || cardItem == null) return;

        selectedCardId = cardItem.CardId;
        selectedCardItem = cardItem;
        UpdateSelectedCardPreview();

        // Update button state
        if (submitPickButton != null) {
            submitPickButton.interactable = true;
        }

        // Highlight only the selected card instance (not by ID)
        foreach (var item in packCardItems) {
            if (item != null) {
                item.SetSelected(item == cardItem);
            }
        }
    }

    public void ShowCardDetails(CardDisplayData cardData) {
        if (cardData == null || cardDetailsPanel == null || cardDetailsContainer == null) return;
        if (gameData?.cardTemplatePfb == null) return;

        // Always destroy and recreate to ensure fresh state
        if (cardDetailsInstance != null) {
            Destroy(cardDetailsInstance);
            cardDetailsInstance = null;
        }

        cardDetailsInstance = Instantiate(gameData.cardTemplatePfb, cardDetailsContainer);
        var cardDisplay = cardDetailsInstance.GetComponent<CardDisplay>();
        if (cardDisplay != null) {
            // Explicitly set gameData reference (in case Awake didn't set it properly)
            cardDisplay.gameData = gameData;

            // Disable in-game interaction components
            if (cardDisplay.interactableObj != null) cardDisplay.interactableObj.SetActive(false);
            if (cardDisplay.selectableCardObj != null) cardDisplay.selectableCardObj.SetActive(false);
            if (cardDisplay.attackCapableObj != null) cardDisplay.attackCapableObj.SetActive(false);
            if (cardDisplay.attackableObj != null) cardDisplay.attackableObj.SetActive(false);
            if (cardDisplay.selectableTargetObj != null) cardDisplay.selectableTargetObj.SetActive(false);
            if (cardDisplay.targetingLocationObj != null) cardDisplay.targetingLocationObj.SetActive(false);

            // Clear any existing card data first, then set new data
            cardDisplay.card = null;
            cardDisplay.UpdateCardDisplayData(cardData, trackInUidToObj: false);
        }

        cardDetailsPanel.SetActive(true);
    }

    private void CloseCardDetails() {
        if (cardDetailsPanel != null) {
            cardDetailsPanel.SetActive(false);
        }
    }

    private void UpdateSelectedCardPreview() {
        if (selectedCardPreviewContainer == null || gameData?.cardTemplatePfb == null) return;

        if (selectedCardId.HasValue && gameData.allCardsDict.TryGetValue(selectedCardId.Value, out var cardData)) {
            // Instantiate preview card if it doesn't exist yet
            if (previewCardInstance == null) {
                previewCardInstance = Instantiate(gameData.cardTemplatePfb, selectedCardPreviewContainer);
                previewCardDisplay = previewCardInstance.GetComponent<CardDisplay>();

                if (previewCardDisplay != null) {
                    // Explicitly set gameData reference (in case Awake didn't set it properly)
                    previewCardDisplay.gameData = gameData;

                    // Disable in-game interaction components
                    if (previewCardDisplay.interactableObj != null) previewCardDisplay.interactableObj.SetActive(false);
                    if (previewCardDisplay.selectableCardObj != null) previewCardDisplay.selectableCardObj.SetActive(false);
                    if (previewCardDisplay.attackCapableObj != null) previewCardDisplay.attackCapableObj.SetActive(false);
                    if (previewCardDisplay.attackableObj != null) previewCardDisplay.attackableObj.SetActive(false);
                    if (previewCardDisplay.selectableTargetObj != null) previewCardDisplay.selectableTargetObj.SetActive(false);
                    if (previewCardDisplay.targetingLocationObj != null) previewCardDisplay.targetingLocationObj.SetActive(false);
                }

                // Add right-click handler for card details
                var clickHandler = previewCardInstance.AddComponent<DraftedCardClickHandler>();
                clickHandler.Setup(cardData, this);
            }

            previewCardDisplay?.UpdateCardDisplayData(cardData, trackInUidToObj: false);

            // Update the click handler's card data reference
            var handler = previewCardInstance.GetComponent<DraftedCardClickHandler>();
            if (handler != null) handler.Setup(cardData, this);
        } else {
            // Destroy preview if no card selected
            if (previewCardInstance != null) {
                Destroy(previewCardInstance);
                previewCardInstance = null;
                previewCardDisplay = null;
            }
        }
    }

    private void OpenDraftedCardsPanel() {
        if (draftedCardsPanel != null) {
            UpdateDraftedCardsPanel();
            draftedCardsPanel.SetActive(true);
        }
    }

    private void CloseDraftedCardsPanel() {
        if (draftedCardsPanel != null) {
            draftedCardsPanel.SetActive(false);
        }
    }

    private void OnSubmitPickClicked() {
        if (!selectedCardId.HasValue || isSubmitting) return;
        if (accountDataGO?.accountData == null || gameData?.draftId == null) return;

        StartCoroutine(SubmitPick(selectedCardId.Value));
    }

    private IEnumerator SubmitPick(int cardId) {
        isSubmitting = true;
        if (submitPickButton != null) submitPickButton.interactable = false;

        var (statusCode, draftState) = serverApi.SubmitDraftPick(
            accountDataGO.accountData,
            gameData.draftId.Value,
            cardId
        );

        if (statusCode == 200 && draftState != null) {
            // Detect round change BEFORE updating state
            bool roundChanged = currentDraftState == null || draftState.currentRound != currentDraftState.currentRound;

            currentDraftState = draftState;
            gameData.draftState = draftState;
            selectedCardId = null;
            selectedCardItem = null;

            // Check for status change
            if (draftState.status == "deck_building") {
                TransitionToDeckBuilding();
                yield break;
            }

            // If round changed, reset to pack selection for the new round
            if (roundChanged) {
                packOpenedThisRound = openedRounds.Contains(draftState.currentRound);

                // Clear pack cards from previous round
                ClearPackCards();

                // Clear instantiated packs so they get recreated
                foreach (var pack in instantiatedPacks) {
                    if (pack != null) Destroy(pack);
                }
                instantiatedPacks.Clear();

                UpdateSelectedCardPreview();
            }

            UpdateUI();
        } else {
            Debug.Log($"Failed to submit pick: {statusCode}");
        }

        isSubmitting = false;
        yield return null;
    }

    private void TransitionToDeckBuilding() {
        if (pollingCoroutine != null) StopCoroutine(pollingCoroutine);

        // Store drafted cards for deck building
        if (gameData != null && currentDraftState != null) {
            gameData.draftedCardPool = new List<int>(currentDraftState.myDraftedCards ?? new List<int>());
            gameData.isDraftDeckBuilding = true;
        }

        Debug.Log("[DraftManager] Transitioning to deck building");
        SceneManager.LoadScene("Deck Editor");
    }

    private void TransitionToGame(int matchId) {
        if (pollingCoroutine != null) StopCoroutine(pollingCoroutine);

        // Store matchId for GameManager
        if (gameData != null) {
            gameData.lobbyMatchId = matchId;
        }

        Debug.Log("[DraftManager] Transitioning to game");
        SceneManager.LoadScene("Game Scene");
    }
}
