using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using InGame;
using InGame.CardDataEnums;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using EventType = InGame.EventType;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

public class GameManager : MonoBehaviour {
    // ebug
    public Ebug ebug;

    [Header("Testing")]
    public bool skipAnimations;

    public GameData gameData;
    public Player player;
    public Participant opponent;

    public Camera mainCam;

    public GameObject handCardDragDisplay;

    public GameObject waitingForOpponentTextObj;
    public bool isWaiting;
    public GameObject selectTextObj;
    
    // menus
    public GameObject pauseMenu;
    public List<Disabler> allDisablers = new();
    
    // priority
    public List<GameObject> activeCoroutines = new();

    // choices
    public GameObject choicesDialogue;
    public GameObject choices;
    public GameObject choicePfb;
    public TMP_Text choiceMessageText; 
    
    // ordering
    public GameObject orderingDialogue;
    public GameObject orderingView;
    public GameObject orderingContainerPfb;
    public List<OrderingContainer> finalOrderList;
    public Button orderingSubmitButton;
    
    // stack
    public GameObject stackPanel;
    public GameObject stackView;
    public GameObject stackObjContainerPfb;
    public GameObject stackObjectPfb;
    public Stack<StackDisplayData> localStack = new();
    public Button resolveButton;
    public GameObject currentResolvingStackObj;
    
    // player
    public bool cardIsGrabbed;
    
    
    public GameObject spellStackDisplayPfb;
    public GameObject abilityStackDisplayPfb;
    public GameObject cardDisplayPfb;
    public GameObject playSlotPfb;
    public GameObject tokenDisplayPfb;

    
    // phases
    public Phase phaseToPassTo;
    public bool autoPass;
    public bool passToMyMain;
    public bool autopassPausedForStack; // Pause autopass when trigger is added to stack
    public Phase localPhase;
    public PhaseButtonManager PhaseButtonManager;
    
    public GameObject mainTitle;
    public GameObject displayCanvas;
    public RectTransform displayCanvasRectTransform;

    public Button actionButtonComponent;
    public TMP_Text passBtnText;
    public GameObject phaseBtnBorderObj;
    
    public readonly ServerApi serverApi = new();

    public TMP_Text devText;
    
    // events
    private GameEvent currentGameEvent;
    public bool gEventIsInProgress;
    public bool eventAnimsAreInProgress;
    private bool pendingGameOver;  // Flag to show game over after all animations finish
    private readonly float eventDelaySuperShort = 0.05f;
    private readonly float eventDelayShort = 0.2f;
    private readonly float eventDelayLong = 1f;
    
    // card selection
    public GameObject cardSelectionDialogue;
    public GameObject cardSelectionView;
    public CardSelectionManager cardSelectionManager;
    
    // combat
    public List<int> attackCapableUids = new();
    public AttackCapable attackingAttackCapable;
    public float attackYAdjust = 50f;
    private List<DynamicReferencer> attackableDRefs = new();
    public Dictionary<int, int> attackUids = new();
    private Dictionary<GameObject, GameObject> attackerToAttackArrow = new();
    private bool isSecondaryAttack;

    // Ground Tactics: when active, player controls opponent's attack assignments
    private bool isGroundTacticsMode;
    private int groundTacticsAttackerPlayerId;

    // targeting
    public GameObject targetingArrowPfb;
    public GameObject targetingArrow;
    public int currentTargetMax;
    public int currentTargetMin;  // Minimum targets required (0 for optional/"any number")
    public bool currentTargetRequireOneFromEach;
    public List<int> currentTargetPlayerUids = new();
    public List<int> currentTargetOpponentUids = new();

    // selecting
    public List<int> possibleSelectables = new();
    public List<int> selectedUids = new();
    public int currentSelectionMax;
    public ActionButtonType currentSelectionType;
    public Dictionary<DynamicReferencer, List<int>> dRefToSelectedUids = new();
    public GameObject amountSelectionPanel;
    // tribute value tracking (for tribute multipliers)
    public Dictionary<int, int> tributeValues = new();
    public int currentTributeValue = 0;
    // variable selection (0 to max instead of exact amount)
    public bool variableSelection = false;

    // activated abilites
    public CardDisplay currentActivatedAbilityCdd;
    public int? currentActivatedTokenUid;  // For token activation
    public TokenDisplay currentActivatedTokenDisplay;
    public GameObject activationVerificationPanel;
    public TMP_Text activationVerificationText;

    // deck top casting (Sky Scryer)
    public CardDisplay currentDeckTopCastCard;

    
    public Dictionary<int, GameObject> UidToObj = new();

    public Material arrowSelectMat;
    public Material arrowAttackMat;
    
    // action button
    public ActionButton actionButton;

    // card details
    public GameObject simpleCardDisplayPfb;
    public GameObject mouseCardDisplayContainer;
    public GameObject bigCardDisplayContainer;
    public GameObject detailsPanel;
    public CardDisplaySimple detailCardDisplay;
    public CardDisplaySimple mouseCardDisplay;
    
    // card group display
    public GameObject cardGroupPanel;
    public GameObject cardGroupView;
    public TMP_Text cardGroupTitleText;
    
    // UI Panel Stack
    public List<GameObject> stackablePanels;

    // Debug deck viewer (press D to toggle)
    public GameObject debugDeckPanel;
    public TMP_Text debugDeckText;

    // Game over UI
    [Header("Game Over")]
    public GameObject gameOverPanel;
    public TMP_Text gameOverResultText;       // "VICTORY" or "DEFEAT"
    public TMP_Text gameOverSeriesScoreText;  // "1 - 0" series score
    public TMP_Text gameOverMessageText;      // "You win the series!" or "Next game..."
    public Button gameOverContinueButton;

    private void Update() {
        if (Input.GetKeyUp(KeyCode.Escape)) {
            if(gameData.panelStack.Count < 1) {
                TogglePauseMenu();
            } else {
                gameData.panelStack.Last().Disable();
            }
        }

        if (Input.GetKeyDown("9")) {
            ebug.Slog("Current UidToCardObj Dict:");
            foreach (KeyValuePair<int, GameObject> keyPair in UidToObj) {
                ebug.Slog("uid: " + keyPair.Key);
            }
        }

        // Toggle debug deck viewer with D key
        if (Input.GetKeyDown(KeyCode.D) && debugDeckPanel != null) {
            ToggleDebugDeckPanel();
        }
    }

    private void ToggleDebugDeckPanel() {
        debugDeckPanel.SetActive(!debugDeckPanel.activeSelf);
        if (debugDeckPanel.activeSelf) {
            RefreshDebugDeckDisplay();
        }
    }

    private void RefreshDebugDeckDisplay() {
        if (debugDeckText == null || gameData.matchState?.playerState?.deckContents == null) return;

        var deckContents = gameData.matchState.playerState.deckContents;
        string deckString = $"=== DECK ({deckContents.Count} cards) ===\n";
        deckString += "(Top of deck first)\n\n";

        for (int i = 0; i < deckContents.Count; i++) {
            var card = deckContents[i];
            deckString += $"{card.name}\n";
        }

        debugDeckText.text = deckString;
    }
    private void Start() {
        ebug = GameObject.Find("eBugger").GetComponent<Ebug>();
        InitializeStaticCardDisplays();
        InitializeGame();
        InitializePlayers();

        if (gameOverContinueButton != null)
            gameOverContinueButton.onClick.AddListener(OnGameOverContinueClicked);

        WireQuitButtons();

        StartCoroutine(StartGame());
    }
    
    private void InitializeStaticCardDisplays() {
        mouseCardDisplay = Instantiate(simpleCardDisplayPfb, mouseCardDisplayContainer.transform).GetComponent<CardDisplaySimple>();
        mouseCardDisplay.gameData = gameData;
        detailCardDisplay = Instantiate(simpleCardDisplayPfb, bigCardDisplayContainer.transform).GetComponent<CardDisplaySimple>();
        detailCardDisplay.gameData = gameData;
    }

    private IEnumerator StartGame() {
        // display and animate who goes first text
        TMP_Text mainTitleText = mainTitle.GetComponent<TMP_Text>();
        if (gameData.matchState.turnPlayerId == gameData.accountData.id) {
            mainTitleText.text = "You Go First";
        } else {
            mainTitleText.text = "Opponent Goes First";
        }
        Animator anim = mainTitle.GetComponent<Animator>();
        anim.Play("ShrinkCentered", -1, 0f);
        float animLength = anim.GetCurrentAnimatorStateInfo(0).length;
        yield return new WaitForSeconds(animLength);
        // wait for events
        WaitForEvents();
    }
    
    private void WaitForEvents() {
        // return if you're already pinging server
        if (activeCoroutines.Count > 0) return;
        // this is called when you pass prio
        waitingForOpponentTextObj.SetActive(true);
        GameObject repeatCheckObj = new GameObject();
        activeCoroutines.Add(repeatCheckObj);
        StartCoroutine(CheckForChangesAtInterval(1f, repeatCheckObj));
    }

    private IEnumerator CheckForChangesAtInterval(float interval, GameObject repeatCheckObj) {
        while (activeCoroutines.Contains(repeatCheckObj)) {
            // prevents matchstate changes while events being displayed
            if (!eventAnimsAreInProgress) {
                CheckForMatchStateChanges();
            }
            yield return new WaitForSeconds(interval);

        }
    }

    // The SCF (Small Checker Function) -> checks for any changes and applies them
    private void CheckForMatchStateChanges() {
        // Skip the first check if we came from lobby (events already fetched in InitializeGame)
        if (skipFirstMatchStateCheck) {
            skipFirstMatchStateCheck = false;
            Debug.Log("[Lobby] Skipping first CheckForMatchStateChanges, processing existing events");
            // Process events that were fetched in InitializeGame
            if (gameData.matchState.playerState.eventList.Count > 0) {
                InitiateEvents();
            }
            return;
        }

        gameData.matchState = serverApi.GetMatchState(gameData.accountData, gameData.matchState.matchId);
        // Phase state logging
        Debug.Log($"[PhaseState] serverPhase={gameData.matchState.currentPhase}, localPhase={localPhase}, autoPass={autoPass}, phaseToPassTo={phaseToPassTo}");

        // Update revealed top card (from TopCardRevealed passive) - only for player, not opponent
        player.UpdateRevealedTopCard(gameData.matchState.playerState.revealedTopCard);

        // Event Update
        Debug.Log("checked for matchstate changes, event count is: " + gameData.matchState.playerState.eventList.Count);
        if (gameData.matchState.playerState.eventList.Count > 0) {
            InitiateEvents();
        }
    }

    private void InitiateEvents() {
        eventAnimsAreInProgress = true;
        StartCoroutine(QueueEventAnims());
    }

    // EVENT FUNCTION
    private IEnumerator QueueEventAnims() {
        // Log all phase-related events for debugging
        Debug.Log($"[QueueEventAnims] Received {gameData.matchState.playerState.eventList.Count} events, localPhase={localPhase}, serverPhase={gameData.matchState.currentPhase}");
        foreach (var evt in gameData.matchState.playerState.eventList) {
            if (evt.eventType == EventType.NextPhase || evt.eventType == EventType.SkipToPhase || evt.eventType == EventType.GoToPhase || evt.eventType == EventType.GainPrio) {
                Debug.Log($"  - {evt.eventType} (amount={evt.amount}, universalInt={evt.universalInt})");
            }
        }

        // set the current resolving stack object if resolve event exists
        if (gameData.matchState.playerState.eventList.Any(gEvent => gEvent.eventType == EventType.Resolve)) {
            currentResolvingStackObj = stackView.transform.GetChild(stackView.transform.childCount - 1).gameObject;
        }
        // iterate through the events and play their animations, waiting for each one to finish before the next starts
        foreach (GameEvent gEvent in gameData.matchState.playerState.eventList) {
            gEventIsInProgress = true;
            string plurality;
            switch (gEvent.eventType) {
                case EventType.Draw:
                    Debug.Log("Draw Event");
                    StartCoroutine(DrawEvent(gEvent));
                    break;
                case EventType.Mill:
                    Debug.Log("Mill Event");
                    StartCoroutine(MillEvent(gEvent));
                    break;
                case EventType.Cast:
                    Debug.Log("Cast Event");
                    StartCoroutine(CastEvent(gEvent));
                    break;
                case EventType.Trigger:
                    Debug.Log("Trigger Event");
                    AddToStack(gEvent.focusStackObj);
                    autopassPausedForStack = true; // Pause autopass when trigger is added to stack
                    gEventIsInProgress = false;
                    break;
                case EventType.Summon:
                    Debug.Log("Summon Event");
                    StartCoroutine(SummonEvent(gEvent));
                    break;
                case EventType.Resolve:
                    Debug.Log("Resolve Event");
                    int count = stackView.transform.childCount;
                    if (currentResolvingStackObj != null) {
                        // Check if resolving object is a CardDisplay (card) or StackObjDisplay (ability)
                        CardDisplay resolvingCardDisplay = currentResolvingStackObj.GetComponent<CardDisplay>();
                        StackObjDisplay resolvingStackDisplay = currentResolvingStackObj.GetComponent<StackObjDisplay>();

                        if (resolvingCardDisplay != null) {
                            // Card on stack - move to appropriate zone based on sourceCard
                            Debug.Log($"[UID] Resolve: CardDisplay uid={resolvingCardDisplay.card.uid} resolving");
                            if (gEvent.sourceCard != null) {
                                // Spell card goes to graveyard
                                if (gEvent.isOpponent) {
                                    resolvingCardDisplay.ownerIsOpponent = true;
                                    resolvingCardDisplay.AddToGraveyardOpponent();
                                } else {
                                    resolvingCardDisplay.AddToGraveyard();
                                }
                            }
                            // Note: Summon/Object cards will be handled by SummonEvent - the CardDisplay
                            // is moved from stack to play zone there, so we don't destroy here
                        } else if (resolvingStackDisplay != null) {
                            // Ability on stack - remove from UidToObj and destroy
                            if (resolvingStackDisplay.card != null) {
                                int resolveUid = resolvingStackDisplay.card.uid;
                                if (UidToObj.TryGetValue(resolveUid, out GameObject objInMap) && objInMap == currentResolvingStackObj) {
                                    Debug.Log($"[UID] Resolve: Removing StackObjDisplay uid={resolveUid} from UidToObj");
                                    UidToObj.Remove(resolveUid);
                                }
                            }
                            Destroy(currentResolvingStackObj);
                        }
                    } else {
                        Debug.LogWarning("[UID] Resolve: currentResolvingStackObj is null!");
                    }
                    if (count == 1) {
                        Debug.Log("stack is empty, disabling stack panel");
                        stackPanel.SetActive(false);
                        autopassPausedForStack = false; // Clear flag when stack is empty
                    }
                    // finalize the event by toggling gEventsInProgress
                    gEventIsInProgress = false;
                    break;
                case EventType.LookAtDeck:
                    Debug.Log("LookAtDeck Event");
                    DisplayLookAtDeckDialogue(gEvent);
                    break;
                case EventType.Peek:
                    Debug.Log("Peek Event");
                    DisplayPeekDialogue(gEvent);
                    break;
                case EventType.SendToZone:
                    Debug.Log("SendToZone Event");
                    StartCoroutine(SendToZoneEvent(gEvent));
                    break;
                case EventType.Attack:
                    Debug.Log("Attack Event");
                    if (gEvent.universalBool) {
                        DisplayAttack(((int,int))gEvent.attackUids, gEvent.isOpponent);
                    } else {
                        UnDisplayAttack(gEvent.attackUids.Value.Item1, gEvent.isOpponent);
                    }
                    // set the attack arrows to the attack color
                    Debug.Log("There are " + attackerToAttackArrow.Count + " attackers");
                    gEventIsInProgress = false;
                    break;
                case EventType.Combat:
                    Debug.Log("Combat Event");
                    if (UidToObj.TryGetValue(gEvent.attackerUid, out GameObject attackerDebugObj)) {
                        Debug.Log("Attacker is " + attackerDebugObj.GetComponent<CardDisplay>()?.card?.name +
                                  ". defender uid is " + gEvent.defenderUid + ". damage amount is " + gEvent.amount);
                    }
                    StartCoroutine(CombatEvent(gEvent));

                    break;
                case EventType.Death:
                    Debug.Log("Death Event");
                    if (UidToObj.TryGetValue(gEvent.focusUid, out GameObject dyingObj)) {
                        // Clean up attack visuals if dying unit was attacking
                        if (attackUids.ContainsKey(gEvent.focusUid)) {
                            bool isOppAttack = dyingObj.transform.IsChildOf(opponent.playZoneObj.transform);
                            UnDisplayAttack(gEvent.focusUid, isOppAttack);
                        }
                        CardDisplay dyingCard = dyingObj.GetComponent<CardDisplay>();
                        if (dyingCard != null) {
                            Debug.Log("Dying unit is " + dyingCard.card?.name);
                            dyingCard.Kill();
                        } else {
                            // Handle token death - tokens use TokenDisplay, not CardDisplay
                            TokenDisplay dyingToken = dyingObj.GetComponent<TokenDisplay>();
                            if (dyingToken != null) {
                                Debug.Log($"Dying token uid: {gEvent.focusUid}");
                                dyingToken.RemoveToken(gEvent.focusUid);
                                // If no more tokens in stack, destroy the display
                                if (dyingToken.tokenUids.Count == 0) {
                                    Destroy(dyingToken.gameObject);
                                }
                            }
                        }
                    }
                    gEventIsInProgress = false;
                    break;
                case EventType.TributeRequirement:
                    Debug.Log("Tribute Requirement Event");
                    waitingForOpponentTextObj.SetActive(false);
                    Debug.Assert(gEvent.focusCard != null, "There is no card associated with this event to pull tribute cost from");
                    currentSelectionMax = gEvent.focusCard.cost;  // This is the tribute VALUE needed
                    currentTributeValue = 0;  // Reset current tribute value
                    tributeValues = gEvent.tributeValues ?? new Dictionary<int, int>();
                    plurality = currentSelectionMax == 1 ? "" : "s";
                    selectTextObj.GetComponent<TMP_Text>().text = "Select Tribute" + plurality + ".";
                    selectTextObj.SetActive(true);
                    possibleSelectables = gEvent.focusUidList;
                    Debug.Log($"[UID] TributeRequirement: Expected selectable UIDs: [{string.Join(", ", possibleSelectables)}]");
                    Debug.Log($"[UID] TributeRequirement: UidToObj keys: [{string.Join(", ", UidToObj.Keys)}]");
                    foreach (int uid in possibleSelectables) {
                        if (!UidToObj.ContainsKey(uid)) {
                            Debug.LogWarning($"[UID] TributeRequirement: uid {uid} is expected but NOT in UidToObj!");
                        }
                    }
                    currentSelectionType = ActionButtonType.Tribute;
                    EnableUnselectedSelectables();
                    break;
                case EventType.Spellburn:
                    Debug.Log("Spellburn Event");
                    if (gEvent.isOpponent) {
                        opponent.ToggleSpellburn();
                    } else {
                        player.ToggleSpellburn();
                    }
                    gEventIsInProgress = false;
                    break;
                case EventType.GainLife:
                    Debug.Log("GainLife Event");
                    if (gEvent.isOpponent) {
                        opponent.GainLife(gEvent.amount);
                    } else {
                        player.GainLife(gEvent.amount);
                    }
                    gEventIsInProgress = false;
                    break;
                case EventType.NextPhase:
                    Debug.Log("NextPhase Event");
                    StartCoroutine(NextPhaseEvent());
                    break;
                case EventType.SkipToPhase:
                    Debug.Log($"SkipToPhase Event: {gEvent.amount} phases from {(Phase)gEvent.universalInt}");
                    StartCoroutine(SkipToPhaseEvent(gEvent.amount, (Phase)gEvent.universalInt));
                    break;
                case EventType.GoToPhase:
                    Debug.Log($"GoToPhase Event: jumping to {(Phase)gEvent.universalInt}");
                    StartCoroutine(GoToPhaseEvent((Phase)gEvent.universalInt));
                    break;
                case EventType.TriggerOrdering:
                    Debug.Log("TriggerOrdering Event");
                    waitingForOpponentTextObj.SetActive(false);
                    currentGameEvent = gEvent;
                    DisplayOrderingOptions();
                    break;
                case EventType.Choice:
                    Debug.Log("Choice Event");
                    waitingForOpponentTextObj.SetActive(false);
                    DisplayChoice(gEvent.playerChoice);
                    break;
                case EventType.TargetSelection:
                    Debug.Log("TargetSelection Event");
                    Debug.Log($"[UID] TargetSelection: Expected selectable UIDs: [{string.Join(", ", gEvent.targetSelection.selectableUids)}]");
                    Debug.Log($"[UID] TargetSelection: UidToObj keys: [{string.Join(", ", UidToObj.Keys)}]");
                    foreach (int uid in gEvent.targetSelection.selectableUids) {
                        if (!UidToObj.ContainsKey(uid)) {
                            Debug.LogWarning($"[UID] TargetSelection: uid {uid} is expected but NOT in UidToObj!");
                        }
                    }
                    waitingForOpponentTextObj.SetActive(false);
                    currentTargetMax = gEvent.targetSelection.amount;
                    currentTargetMin = gEvent.targetSelection.minAmount;
                    Debug.Log($"[TargetSelection] amount={currentTargetMax}, minAmount={currentTargetMin}");
                    currentTargetRequireOneFromEach = gEvent.targetSelection.requireOneFromEach;
                    currentTargetPlayerUids = gEvent.targetSelection.playerUids ?? new List<int>();
                    currentTargetOpponentUids = gEvent.targetSelection.opponentUids ?? new List<int>();
                    // Use custom message if provided, otherwise default to "Select Target(s)."
                    if (!string.IsNullOrEmpty(gEvent.targetSelection.message)) {
                        selectTextObj.GetComponent<TMP_Text>().text = gEvent.targetSelection.message;
                    } else {
                        plurality = gEvent.targetSelection.amount == 1 ? "" : "s";
                        selectTextObj.GetComponent<TMP_Text>().text = "Select Target" + plurality + ".";
                    }
                    selectTextObj.SetActive(true);
                    possibleSelectables = gEvent.targetSelection.selectableUids;
                    currentSelectionType = ActionButtonType.Target;
                    EnableUnselectedSelectables();
                    // If minimum targets is 0, enable submit button immediately (for "any number" selection)
                    if (currentTargetMin == 0) {
                        Debug.Log($"[TargetSelection] minAmount is 0, enabling action button immediately");
                        actionButtonComponent.interactable = true;
                        actionButton.SetButtonType(ActionButtonType.Target);
                    } else {
                        Debug.Log($"[TargetSelection] minAmount is {currentTargetMin}, not enabling action button yet");
                    }
                    break;
                case EventType.GainPrio:
                    Debug.Log("GainPrio Event");
                    if (gEvent != gameData.matchState.playerState.eventList.Last()) {
                        Debug.Log("GainPrio was not the last event in the list. It should ALWAYS be the last event");
                    }
                    GainPrio();
                    break;
                case EventType.RefreshCardDisplays:
                    Debug.Log("RefreshCardDisplays Event");
                    foreach (CardDisplayData cdd in gEvent.cards) {
                        if (UidToObj.TryGetValue(cdd.uid, out GameObject cardObj)) {
                            CardDisplay cardDisplay = cardObj.GetComponent<CardDisplay>();
                            if (cardDisplay != null) {
                                cardDisplay.DisplayCardData(cdd);
                            }
                        }
                    }
                    gEventIsInProgress = false;
                    break;
                case EventType.SetLifeTotal:
                    Debug.Log("SetLifeTotal Event");
                    if (gEvent.isOpponent) {
                        opponent.SetLifeTotal(gEvent.amount);
                    } else {
                        player.SetLifeTotal(gEvent.amount);
                    }
                    gEventIsInProgress = false;
                    break;
                case EventType.PayLifeCost or EventType.LoseLife:
                    Debug.Log("PayLifeCost/LoseLife Event");
                    // universalInt contains expected life total for verification (prevents double-damage from animation)
                    int? expectedLifeTotal = gEvent.universalInt != 0 ? gEvent.universalInt : null;
                    if (gEvent.isOpponent) {
                        opponent.LoseLife(gEvent.amount, expectedLifeTotal);
                    } else {
                        player.LoseLife(gEvent.amount, expectedLifeTotal);
                    }
                    gEventIsInProgress = false;
                    break;
                case EventType.Reveal:
                    Debug.Log("Reveal Event");
                    if (gEvent.isOpponent) {
                        // Opponent is revealing a card from their hand
                        Opponent opp = opponent as Opponent;
                        if (opp != null) {
                            // Find first unrevealed card in opponent's tracked hand
                            CardDisplay cardToReveal = opp.handCards.Find(c => c.card == null);
                            if (cardToReveal != null) {
                                cardToReveal.UpdateCardDisplayData(gEvent.focusCard);
                                // Unrotate the card now that it's revealed (was rotated 180 for face-down)
                                cardToReveal.transform.localRotation = Quaternion.identity;
                            }
                        }
                    } else {
                        // Player is revealing their own card - just show a visual indication
                        // The card is already visible to the player, so we just need to mark it as revealed
                        // Could add a reveal animation/highlight here if desired
                        Debug.Log($"Player revealed card: {gEvent.focusCard?.name}");
                    }
                    gEventIsInProgress = false;
                    break;
                case EventType.AttackCapable:
                    Debug.Log("AttackCapable Event");
                    Debug.Assert(gEvent.focusUidList != null, "AttackCapableUids list is null");
                    attackCapableUids = gEvent.focusUidList.ToList();

                    // Check for Ground Tactics forced attack mode
                    if (gEvent.universalBool) {
                        isGroundTacticsMode = true;
                        groundTacticsAttackerPlayerId = gEvent.universalInt;
                        Debug.Log($"Ground Tactics mode: controlling opponent's {attackCapableUids.Count} attackers");
                    } else {
                        isGroundTacticsMode = false;
                    }

                    ActivateAttackCapables();
                    gEventIsInProgress = false;
                    break;
                case EventType.CreateToken:
                    Debug.Log("CreateToken Event");
                    if (gEvent.isOpponent) {
                        opponent.CreateOrAddToken(gEvent.focusCard, gEvent.universalInt);
                    } else {
                        player.CreateOrAddToken(gEvent.focusCard, gEvent.universalInt);
                    }
                    gEventIsInProgress = false;
                    break;
                case EventType.RemoveToken:
                    Debug.Log("RemoveToken Event");
                    // Remove token from token zone (used when tokens are converted to summons)
                    if (gEvent.isOpponent) {
                        opponent.RemoveToken(gEvent.focusCard, gEvent.universalInt);
                    } else {
                        player.RemoveToken(gEvent.focusCard, gEvent.universalInt);
                    }
                    gEventIsInProgress = false;
                    break;
                case EventType.Destroy:
                    Debug.Log("Destroy Event");
                    switch (gEvent.focusCard.type) {
                        case CardType.Token:
                            if (gEvent.isOpponent) {
                                opponent.RemoveToken(gEvent.focusCard, gEvent.universalInt);
                            } else {
                                player.RemoveToken(gEvent.focusCard, gEvent.universalInt);
                            }
                            break;
                        case CardType.Object:
                            // TODO
                            break;
                    }
                    gEventIsInProgress = false;
                    break;
                case EventType.Cost:
                    Debug.Log("Cost Event");
                    // some costs select cards, others may do other things.
                    // the majority use "EnableCostCardSelection"
                    switch (gEvent.costType) {
                        case CostType.Sacrifice:
                            EnableCostCardSelection(gEvent);
                            break;
                        case CostType.Discard:
                            EnableCostCardSelection(gEvent);
                            break;
                        case CostType.ExileFromHand:
                            EnableCostCardSelection(gEvent);
                            break;
                        case CostType.Reveal:
                            EnableCostCardSelection(gEvent);
                            break;
                    }
                    currentSelectionType = ActionButtonType.Cost;
                    break;
                case EventType.EndGame:
                    Debug.Log("EndGame Event - will show game over after animations complete");
                    pendingGameOver = true;  // Flag to show game over after all animations finish
                    gEventIsInProgress = false;
                    break;
                case EventType.Discard:
                    Debug.Log("Discard Event");
                    StartCoroutine(DiscardEvent(gEvent));
                    break;
                case EventType.AmountSelection:
                    Debug.Log("AmountSelection Event");
                    // Use server-provided max if available, otherwise fall back to life points
                    currentSelectionMax = gEvent.amount > 0 ? gEvent.amount : player.lifePoints;
                    if (gEvent.universalBool) {
                        DisplayAmountSelector(SetX, CancelCast);
                    } else {
                        DisplayAmountSelector(SetAmount, CancelCast);
                    }
                    break;
                case EventType.ReturnToHand:
                    Debug.Log("ReturnToHand Event");
                    StartCoroutine(ReturnToHandEvent(gEvent));
                    break;
                case EventType.Counter:
                    Debug.Log("Counter Event");
                    StartCoroutine(CounterEvent(gEvent));
                    break;
                case EventType.GainControl:
                    Debug.Log("GainControl Event");
                    StartCoroutine(GainControlEvent(gEvent));
                    break;
                case EventType.RitualOfDarknessChoice:
                    Debug.Log("RitualOfDarknessChoice Event");
                    waitingForOpponentTextObj.SetActive(false);
                    currentSelectionMax = 1;
                    selectTextObj.GetComponent<TMP_Text>().text = gEvent.eventMessages?.FirstOrDefault() ?? "Put a summon into play or pass";
                    selectTextObj.SetActive(true);
                    possibleSelectables = gEvent.focusUidList ?? new List<int>();
                    currentSelectionType = ActionButtonType.RitualSummon;
                    EnableUnselectedSelectables();
                    // Enable the pass button - player can always pass
                    actionButtonComponent.interactable = true;
                    actionButton.SetButtonType(ActionButtonType.RitualPass);
                    gEventIsInProgress = false;
                    break;
                case EventType.RepeatAmountSelection:
                    Debug.Log("RepeatAmountSelection Event");
                    // amount = max repeats, universalInt = cost per repeat
                    currentSelectionMax = gEvent.amount;
                    DisplayAmountSelector(SetAmount, CancelCast);
                    break;
                default:
                    Debug.Log("EventType: " + gEvent.eventType + " not implemented.");
                    break;
            }
            yield return new WaitUntil(() => !gEventIsInProgress);
        }
        RefreshAttackArrows();
        RefreshStateDisplays();
        eventAnimsAreInProgress = false;
        Debug.Log("events anims complete");

        // Show game over panel after all animations have finished
        if (pendingGameOver) {
            Debug.Log("Showing game over screen now that animations are complete");
            pendingGameOver = false;
            HandleGameOver(null);  // We don't need the event data, we use matchState
        }
    }

    private void EnableCostCardSelection(GameEvent gEvent) {
        waitingForOpponentTextObj.SetActive(false);
        Debug.Assert(gEvent.focusUidList != null, "selectableCosts list is null");
        currentSelectionMax = gEvent.amount;
        variableSelection = gEvent.universalBool; // true = select 0 to max, false = select exactly amount
        selectTextObj.GetComponent<TMP_Text>().text = gEvent.eventMessages.First();
        selectTextObj.SetActive(true);
        possibleSelectables = gEvent.focusUidList;
        EnableUnselectedSelectables();
        // For variable selection, action button is enabled immediately (can confirm with 0 selections)
        if (variableSelection) {
            actionButtonComponent.interactable = true;
            actionButton.SetButtonType(ActionButtonType.Cost);
        }
    }

    private void RefreshStateDisplays() {
        player.deckAmountText.text = gameData.matchState.playerState.deckAmount.ToString();
        opponent.deckAmountText.text = gameData.matchState.opponentState.deckAmount.ToString();
        // TODO update deckObj for each player if deck is empty
    }

    private void IteratePhase() {
        // Clear attack capables when leaving Combat phase
        if (localPhase == Phase.Combat) {
            attackCapableUids.Clear();
        }
        if (localPhase == Phase.End) {
            localPhase = Phase.Draw;
            return;
        }
        localPhase += 1;
    }
    
    private void GainPrio() {
        // remove active checks for matchstate
        foreach (GameObject go in activeCoroutines) {
            Destroy(go);
        }
        activeCoroutines.Clear();

        // Reset all interactable states before enabling new ones
        DisableAllInteractables();

        // Log phase state for debugging desync
        Phase serverPhase = gameData.matchState.currentPhase;
        if (localPhase != serverPhase) {
            Debug.LogError($"[GainPrio] PHASE DESYNC! localPhase={localPhase}, serverPhase={serverPhase}");
        }

        // only auto pass if there are no possible attackers
        if (autoPass && attackCapableUids.Count == 0) {
            if(ShouldAutoPass()) {
                Debug.Log($"[GainPrio] Auto-passing: localPhase={localPhase}, phaseToPassTo={phaseToPassTo}");
                gEventIsInProgress = false;
                PassPrio();
                return;
            }
            Debug.Log($"[GainPrio] Stopping autopass: reached phaseToPassTo={phaseToPassTo}, localPhase={localPhase}");
            passToMyMain = false;
            autoPass = false;
            PhaseButtonManager.DisableStopBorder();
        }
        // displays any playable cards/moves
        foreach (CardDisplayData card in gameData.matchState.playerState.playables) {
            if (!UidToObj.TryGetValue(card.uid, out GameObject cardObj)) continue;
            CardDisplay cardDisplay = cardObj.GetComponent<CardDisplay>();
            if (cardDisplay == null) continue;
            cardDisplay.EnablePlayable();
            CardSlot cardSlot = cardObj.GetComponentInParent<CardSlot>();
            if (cardSlot != null) {
                cardSlot.ActivateTempHighlights();
            }
        }

        foreach (CardDisplayData card in gameData.matchState.playerState.activatables) {
            if (!UidToObj.ContainsKey(card.uid)) continue;
            GameObject cardObj = UidToObj[card.uid];
            // Check if it's a card or token and enable appropriately
            CardDisplay cardDisplay = cardObj.GetComponent<CardDisplay>();
            if (cardDisplay != null) {
                cardDisplay.EnableActivatable();
            } else {
                // It's a token - enable via TokenDisplay
                TokenDisplay tokenDisplay = cardObj.GetComponent<TokenDisplay>();
                if (tokenDisplay != null) {
                    tokenDisplay.EnableActivatable();
                }
            }
        }

        // Re-enable attack capables (they were disabled by DisableAllInteractables)
        ActivateAttackCapables();

        waitingForOpponentTextObj.SetActive(false);
        actionButtonComponent.interactable = true;
        gEventIsInProgress = false;
    }

    private bool ShouldAutoPass() {
        // Don't auto-pass if there's a trigger on the stack we need to respond to
        if (autopassPausedForStack) {
            Debug.Log($"[ShouldAutoPass] Returning FALSE - autopass paused for stack");
            return false;
        }
        // Don't auto-pass if a selection/targeting prompt is active
        if (IsPromptActive()) {
            Debug.Log($"[ShouldAutoPass] Returning FALSE - prompt is active");
            return false;
        }

        Debug.Log($"[ShouldAutoPass] passToMyMain={passToMyMain}, myId={gameData.accountData.id}, turnPlayerId={gameData.matchState.turnPlayerId}, currentPhase={gameData.matchState.currentPhase}, phaseToPassTo={phaseToPassTo}");

        // For passToMyMain: continue autopassing if it's not our turn yet
        if (passToMyMain && gameData.accountData.id != gameData.matchState.turnPlayerId) {
            Debug.Log($"[ShouldAutoPass] Returning TRUE - passToMyMain and not our turn");
            return true;
        }

        // For passToMyMain: stop when we reach Main on our turn
        if (passToMyMain && gameData.accountData.id == gameData.matchState.turnPlayerId && gameData.matchState.currentPhase == Phase.Main) {
            Debug.Log($"[ShouldAutoPass] Returning FALSE - passToMyMain reached target (our Main phase)");
            return false;
        }

        // Stop autopassing when we've reached OR passed the target phase
        if (gameData.matchState.currentPhase < phaseToPassTo) {
            Debug.Log($"[ShouldAutoPass] Returning TRUE - haven't reached target phase yet");
            return true;
        }

        Debug.Log($"[ShouldAutoPass] Returning FALSE - reached or passed target phase");
        return false;
    }
    

    public void PassPrio() {
        Debug.Log($"[PassPrio] localPhase={localPhase}, autoPass={autoPass}, phaseToPassTo={phaseToPassTo}, passToMyMain={passToMyMain}");
        LosePrio();
        int? passToPhaseValue = null;
        if (autoPass) {
            passToPhaseValue = passToMyMain ? 6 : (int)phaseToPassTo;
        }
        Debug.Log($"[PassPrio] Sending passToPhaseValue={passToPhaseValue}");
        serverApi.PassPrio(gameData.accountData, gameData.matchState.matchId, passToPhaseValue);
        WaitForEvents();
    }

    private void LosePrio() {
        actionButtonComponent.interactable = false;
        actionButton.SetButtonType(ActionButtonType.Pass);
        DisableAllInteractables();
    }

    private void DisableAllInteractables() {
        List<int> nullUids = new();
        foreach (var kvp in UidToObj) {
            if (kvp.Value == null) {
                Debug.LogWarning($"[UidToObj Debug] Found null/destroyed object for uid: {kvp.Key}");
                nullUids.Add(kvp.Key);
                continue;
            }
            DynamicReferencer dRef = kvp.Value.GetComponent<DynamicReferencer>();
            if (dRef != null) dRef.DisableAllInteractable();
        }
        // Clean up null entries
        foreach (int uid in nullUids) {
            UidToObj.Remove(uid);
        }
    }
    
    private void DisplayChoice(PlayerChoice pChoice) {
        ResetChoiceDialogue();
        choicesDialogue.SetActive(true);
        choiceMessageText.text = pChoice.optionMessage;
        foreach (var t in pChoice.options) {
            GameObject newChoice = Instantiate(choicePfb, choices.transform);
            newChoice.transform.GetChild(0).GetComponent<TMP_Text>().text = t;
        }
    }

    private void ResetChoiceDialogue() {
        if (choices.transform.childCount <= 1) return;
        foreach (Transform child in choices.transform) {
            Destroy(child.gameObject);
        }
    }
    

    public void SendChoice(int choiceIndex) {
        choicesDialogue.SetActive(false);
        serverApi.SendChoice(gameData.accountData, gameData.matchState.matchId, choiceIndex);
        LosePrio();
        gEventIsInProgress = false;
        WaitForEvents();
    }

    private void DisplayOrderingOptions() {
        Debug.Assert(currentGameEvent.triggerOrderingList != null, "there is no ordering list for order selection");
        // Clear any stale ordering data from previous ordering events
        finalOrderList.Clear();
        ClearOrderingPanel();
        foreach (StackDisplayData stackObj in currentGameEvent.triggerOrderingList) {
            InstantiateStackObjectForOrdering(stackObj, currentGameEvent);
        }
        orderingDialogue.SetActive(true);
    }
    
    private void InstantiateStackObjectForOrdering(StackDisplayData stackDisplayData, GameEvent gEvent) {
        Debug.Assert(gEvent.triggerOrderingList != null, "there is no ordering list for order selection");
        // create a new ordering container
        GameObject newOrderingContainerObj = Instantiate(orderingContainerPfb, orderingView.transform);
        OrderingContainer newOrderingContainer = newOrderingContainerObj.GetComponent<OrderingContainer>();
        newOrderingContainer.gameManager = this;
        newOrderingContainer.index = gEvent.triggerOrderingList.IndexOf(stackDisplayData);
        CreateNewStackObj(stackDisplayData, newOrderingContainer.transform);
    }
    
    public void CreateNewStackObj(StackDisplayData stackDisplayData, Transform parentTransform, bool trackInUidToObj = false) {
        if (!stackPanel.activeSelf) {
            stackPanel.SetActive(true);
        }
        GameObject pfbBasedOnType = stackDisplayData.stackObjType == StackObjType.Spell ? spellStackDisplayPfb : abilityStackDisplayPfb;
        GameObject newStackObjGameObject = Instantiate(pfbBasedOnType, parentTransform);
        StackObjDisplay newStackObjDisplay = newStackObjGameObject.GetComponent<StackObjDisplay>();
        newStackObjDisplay.Initialize(stackDisplayData, this);

        // Track stack objects in UidToObj for targeting (e.g., counter spells)
        Debug.Log($"[Counter Debug] CreateNewStackObj: trackInUidToObj={trackInUidToObj}, cardDisplayData null={stackDisplayData.cardDisplayData == null}");
        if (trackInUidToObj && stackDisplayData.cardDisplayData != null) {
            int uid = stackDisplayData.cardDisplayData.uid;
            Debug.Log($"[Counter Debug] Adding stack obj uid={uid} to UidToObj");
            if (!UidToObj.ContainsKey(uid)) {
                UidToObj.Add(uid, newStackObjGameObject);
                Debug.Log($"[Counter Debug] Successfully added uid={uid} to UidToObj. Keys now: [{string.Join(", ", UidToObj.Keys)}]");
            } else {
                Debug.Log($"[Counter Debug] uid={uid} already in UidToObj");
            }
        }
    }

    public void UpdateAllOrderingContainers() {
        foreach (OrderingContainer oc in finalOrderList) {
            oc.UpdateNumText();
        }
        Debug.Assert(currentGameEvent.triggerOrderingList != null, "there is no current game event trigger list" +
                                                                   " -> check triggerOrderingEvent");
        orderingSubmitButton.interactable = finalOrderList.Count == currentGameEvent.triggerOrderingList.Count;
    }

    public void SendFinalOrdering() {
        List<int> finalIntList = new();
        foreach (OrderingContainer oc in finalOrderList) {
            finalIntList.Add(oc.index);
        }
        serverApi.SendFinalOrdering(gameData.accountData, gameData.matchState.matchId, finalIntList);
        ClearOrderingPanel();
        orderingDialogue.SetActive(false);
        gEventIsInProgress = false;
    }

    private void ClearOrderingPanel() {
        foreach (Transform child in orderingView.transform) {
            Destroy(child.gameObject);
        }
    }

    private IEnumerator NextPhaseEvent() {
        Phase beforePhase = localPhase;
        Animator phaseBtnBorderAnim = phaseBtnBorderObj.GetComponent<Animator>();
        phaseBtnBorderAnim.Play(gameData.phaseToAnimDict[localPhase], -1, 0f);
        IteratePhase();
        Debug.Log($"[NextPhaseEvent] {beforePhase} -> {localPhase}, serverPhase={gameData.matchState.currentPhase}");
        yield return new WaitForSeconds(phaseBtnBorderAnim.GetCurrentAnimatorStateInfo(0).length);
        gEventIsInProgress = false;
    }

    private IEnumerator SkipToPhaseEvent(int phasesToSkip, Phase startPhase) {
        Debug.Log($"SkipToPhaseEvent: phasesToSkip={phasesToSkip}, startPhase={startPhase}, localPhase={localPhase}");

        // Sync localPhase to startPhase in case they're out of sync
        if (localPhase != startPhase) {
            Debug.LogWarning($"SkipToPhaseEvent: localPhase ({localPhase}) != startPhase ({startPhase}), syncing to startPhase");
            localPhase = startPhase;
        }

        Animator phaseBtnBorderAnim = phaseBtnBorderObj.GetComponent<Animator>();
        const float speedMultiplier = 4f;

        // Ensure animator speed starts at normal (in case previous skip was interrupted)
        phaseBtnBorderAnim.speed = speedMultiplier;

        // Play each phase transition animation in sequence
        for (int i = 0; i < phasesToSkip; i++) {
            string animName = gameData.phaseToAnimDict[localPhase];
            Debug.Log($"  Playing animation {i + 1}/{phasesToSkip}: {animName} (localPhase={localPhase})");

            // Play the transition animation for current phase at 4x speed
            phaseBtnBorderAnim.Play(animName, -1, 0f);
            IteratePhase();

            // Wait until the animation is complete (normalizedTime >= 1)
            yield return null; // Wait one frame for Play to take effect
            while (phaseBtnBorderAnim.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f) {
                yield return null;
            }
        }

        // Reset animator speed back to normal
        phaseBtnBorderAnim.speed = 1f;
        Debug.Log($"SkipToPhaseEvent complete, localPhase now={localPhase}");
        gEventIsInProgress = false;
    }

    private IEnumerator GoToPhaseEvent(Phase targetPhase) {
        Debug.Log($"GoToPhaseEvent: jumping from {localPhase} to {targetPhase}");

        // Set localPhase directly to target
        localPhase = targetPhase;

        // Get the animation that ENDS at the target phase
        // e.g., for Draw phase, we want EndToDraw to be at 100%
        Phase previousPhase = targetPhase == Phase.Draw ? Phase.End : targetPhase - 1;
        string animName = gameData.phaseToAnimDict[previousPhase];

        Animator phaseBtnBorderAnim = phaseBtnBorderObj.GetComponent<Animator>();

        // Play the animation and immediately skip to the end (normalizedTime = 1)
        phaseBtnBorderAnim.Play(animName, -1, 1f);

        yield return null; // Wait one frame to apply
        gEventIsInProgress = false;
    }

    private IEnumerator CombatEvent(GameEvent gEvent) {
        // TODO update the attack animation keyframes to match the position of the card and its target (dynamic anim)
        if (!UidToObj.TryGetValue(gEvent.attackerUid, out GameObject attackerObj)) {
            Debug.LogWarning($"CombatEvent: Attacker uid {gEvent.attackerUid} not found");
            gEventIsInProgress = false;
            yield break;
        }
        // remove arrow
        if (attackerToAttackArrow.TryGetValue(attackerObj, out GameObject arrow)) {
            Destroy(arrow);
            attackerToAttackArrow.Remove(attackerObj);
        }
        // unassign AttackCapable
        DynamicReferencer attackerDRef = attackerObj.GetComponent<DynamicReferencer>();
        if (attackerDRef != null) {
            if (attackerDRef.attackCapable != null) attackerDRef.attackCapable.isSelected = false;
            attackerDRef.DisableAllInteractable();
        }
        // set attacker for use in combat animation event
        CardDisplay attackerCardDisplay = attackerObj.GetComponent<CardDisplay>();
        if (attackerCardDisplay != null && UidToObj.TryGetValue(gEvent.defenderUid, out GameObject defenderObj)) {
            attackerCardDisplay.attackTarget = defenderObj;
        }
        // play animation and get current parent
        Animator attackerAnimator = attackerObj.GetComponent<Animator>();
        Transform parentTransform = attackerObj.transform.parent;
        attackerObj.transform.SetParent(displayCanvas.transform);
        attackerAnimator.enabled = true;
        if (gEvent.isOpponent) {
            attackerAnimator.Play("CombatDamageOpp", -1, 0f);
            yield return new WaitForSeconds(attackerAnimator.GetCurrentAnimatorStateInfo(0).length);
        } else {
            attackerAnimator.Play("CombatDamage", -1, 0f);
            yield return new WaitForSeconds(attackerAnimator.GetCurrentAnimatorStateInfo(0).length);
        }
        // reassign parent (probably always PlayZoneObj) and set size values (could methodize)
        attackerObj.transform.SetParent(parentTransform);
        attackerAnimator.enabled = false;
        RectTransform attackerRectTransform = attackerObj.GetComponent<RectTransform>();
        attackerRectTransform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
        attackerRectTransform.anchoredPosition = new Vector3(0, 0, 0);
        // might need to find a better location for this
        attackUids.Clear();
        ResetAttackReferences();
        // remove the highlights
        attackerObj.GetComponent<DynamicReferencer>().DisableAllInteractable();
        gEventIsInProgress = false;
    }
    
    // events + animations
    private IEnumerator DrawEvent(GameEvent gEvent) {
        // set the dealy 
        bool isLastDrawEvent = false;
        // create a new card to draw using the focusCard of the gEvent
        GameObject newCardObj = CreateAndInitializeNewCardDisplay(gEvent.focusCard, displayCanvas.transform);
        CardDisplay newCardDisplay = newCardObj.GetComponent<CardDisplay>();
        if (gEvent.isOpponent) {
            RectTransform newCardRectTransform = newCardDisplay.GetComponent<RectTransform>();
            newCardRectTransform.localEulerAngles = new Vector3(newCardRectTransform.localEulerAngles.x,
                newCardRectTransform.localEulerAngles.y, 180);
        } else {
            newCardDisplay.card = gEvent.focusCard;
        }
        // animate it (skip if testing)
        Animator newCardAnimator = newCardDisplay.GetComponent<Animator>();
        if (skipAnimations) {
            newCardAnimator.enabled = false;
            if (gEvent.isOpponent) {
                newCardDisplay.AddToHandOpponent();
            } else {
                newCardDisplay.AddToHand();
            }
        } else {
            newCardAnimator.enabled = true;
            // set the animDelay to exceed the animation duration if next event is gainprio
            List<GameEvent> eventList = gameData.matchState.playerState.eventList;
            if (eventList.Last() != gEvent) {
                int index = eventList.IndexOf(gEvent);
                if (eventList[index + 1].eventType != EventType.Draw) {
                    isLastDrawEvent = true;
                }
            }
            if (gEvent.isOpponent) {
                newCardAnimator.Play("DrawOpp",-1, 0f);
                // wait longer if the next event is not a draw event
                if (isLastDrawEvent) {
                    yield return WaitForAnimationOrTimeout(newCardAnimator);
                } else {
                    yield return new WaitForSeconds(0.5f);
                }
            } else {
                newCardAnimator.Play("Draw",-1, 0f);
                // wait longer if the next event is not a draw event
                if (isLastDrawEvent) {
                    yield return WaitForAnimationOrTimeout(newCardAnimator);
                } else {
                    yield return new WaitForSeconds(0.5f);
                }
            }
        }
        // finalize the event by toggling gEventsInProgress
        gEventIsInProgress = false;
    }

    /// <summary>
    /// Waits for an animator to finish its current state, but never forever - if the
    /// animation doesn't complete (object inactive, state missing, destroyed mid-wait),
    /// give up after a few seconds so the event queue can't get permanently stuck.
    /// </summary>
    private IEnumerator WaitForAnimationOrTimeout(Animator animator, float timeoutSeconds = 3f) {
        float deadline = Time.time + timeoutSeconds;
        yield return new WaitUntil(() =>
            Time.time >= deadline ||
            animator == null ||
            (animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1 && !animator.IsInTransition(0)));
        if (Time.time >= deadline) {
            Debug.LogWarning("[AnimWait] Animation wait timed out - continuing so the event queue doesn't stall");
        }
    }

    private IEnumerator DiscardEvent(GameEvent gEvent) {
        CardDisplay handCardDisplay;
        Debug.Assert(gEvent.focusCard != null, "There is no focusCard for this discard event");
        if (gEvent.isOpponent) {
            int cardUid = gEvent.focusCard.uid;
            Opponent opp = opponent as Opponent;
            if (UidToObj.ContainsKey(cardUid)) {
                // Revealed card - found in UidToObj
                handCardDisplay = UidToObj[cardUid].GetComponent<CardDisplay>();
                handCardDisplay.RemoveFromHand();
                // Also remove from opponent's hand tracking
                opp?.handCards.Remove(handCardDisplay);
            } else if (opp != null) {
                // Unrevealed card - use tracking to remove
                CardDisplay removedCard = opp.RemoveCardFromHand(cardUid);
                if (removedCard != null) {
                    GameObject cardSlot = removedCard.transform.parent.gameObject;
                    Destroy(cardSlot);
                }
                // Create new display for animation
                handCardDisplay = CreateAndInitializeNewCardDisplay(gEvent.focusCard,
                    displayCanvas.transform).GetComponent<CardDisplay>();
            } else {
                // Fallback - shouldn't happen
                handCardDisplay = CreateAndInitializeNewCardDisplay(gEvent.focusCard,
                    displayCanvas.transform).GetComponent<CardDisplay>();
            }
        } else {
            handCardDisplay = UidToObj[gEvent.focusCard.uid].GetComponent<CardDisplay>();
            handCardDisplay.RemoveFromHand();
        }
        Animator handCardAnimator = handCardDisplay.GetComponent<Animator>();
        handCardAnimator.enabled = true;
        // Speed up animation when discarding multiple cards (e.g., Hand Refresh)
        float animSpeed = gEvent.amount > 1 ? 2f : 1f;
        handCardAnimator.speed = animSpeed;
        string animName = gEvent.isOpponent ? "DiscardOpp" : "Discard";
        handCardAnimator.Play(animName, -1, 0f);
        // Wait a frame for animator to update, then get the actual clip length
        yield return null;
        yield return new WaitForSeconds(handCardAnimator.GetCurrentAnimatorStateInfo(0).length / animSpeed);
        // finalize event
        gEventIsInProgress = false;
    }

    private IEnumerator ReturnToHandEvent(GameEvent gEvent) {
        Debug.Assert(gEvent.focusCard != null, "There is no focusCard for this ReturnToHand event");

        // Remove card from play zone UI if it exists
        if (UidToObj.ContainsKey(gEvent.focusCard.uid)) {
            GameObject cardInPlay = UidToObj[gEvent.focusCard.uid];
            // Clean up attack visuals if this unit was attacking
            if (attackUids.ContainsKey(gEvent.focusCard.uid)) {
                bool isOppAttack = cardInPlay.transform.IsChildOf(opponent.playZoneObj.transform);
                UnDisplayAttack(gEvent.focusCard.uid, isOppAttack);
            }
            UidToObj.Remove(gEvent.focusCard.uid);
            // Destroy the card slot (which contains the card)
            Transform cardSlot = cardInPlay.transform.parent;
            if (cardSlot != null && cardSlot.parent != null && cardSlot.parent.name == "PlayZone") {
                Destroy(cardSlot.gameObject);
            } else {
                Destroy(cardInPlay);
            }
        }

        // Create new card display for hand (similar to DrawEvent)
        GameObject newCardObj = CreateAndInitializeNewCardDisplay(gEvent.focusCard, displayCanvas.transform);
        CardDisplay newCardDisplay = newCardObj.GetComponent<CardDisplay>();
        if (gEvent.isOpponent) {
            RectTransform newCardRectTransform = newCardDisplay.GetComponent<RectTransform>();
            newCardRectTransform.localEulerAngles = new Vector3(newCardRectTransform.localEulerAngles.x,
                newCardRectTransform.localEulerAngles.y, 180);
        } else {
            newCardDisplay.card = gEvent.focusCard;
        }

        // Animate card going to hand
        Animator newCardAnimator = newCardDisplay.GetComponent<Animator>();
        if (skipAnimations) {
            newCardAnimator.enabled = false;
            if (gEvent.isOpponent) {
                newCardDisplay.AddToHandOpponent();
            } else {
                newCardDisplay.AddToHand();
            }
        } else {
            string animName = gEvent.isOpponent ? "DrawOpp" : "Draw";
            newCardAnimator.Play(animName, -1, 0f);
            yield return WaitForAnimationOrTimeout(newCardAnimator);
        }

        gEventIsInProgress = false;
    }

    private IEnumerator CounterEvent(GameEvent gEvent) {
        Debug.Log($"CounterEvent: Countering card with uid {gEvent.focusUid}");

        // Find the stack object with this uid - could be CardDisplay (card) or StackObjDisplay (ability)
        GameObject stackObjToHandle = null;
        bool isCardDisplay = false;

        foreach (Transform child in stackView.transform) {
            // Check for CardDisplay first (cards on stack)
            CardDisplay cardDisplay = child.GetComponent<CardDisplay>();
            if (cardDisplay != null && cardDisplay.card != null && cardDisplay.card.uid == gEvent.focusUid) {
                stackObjToHandle = child.gameObject;
                isCardDisplay = true;
                break;
            }

            // Check for StackObjDisplay (abilities on stack)
            StackObjDisplay stackDisplay = child.GetComponent<StackObjDisplay>();
            if (stackDisplay != null && stackDisplay.card != null && stackDisplay.card.uid == gEvent.focusUid) {
                stackObjToHandle = child.gameObject;
                isCardDisplay = false;
                break;
            }
        }

        if (stackObjToHandle != null) {
            if (isCardDisplay) {
                // CardDisplay - don't destroy, just move to displayCanvas for SendToZone animation
                // The card will be sent to graveyard by a separate SendToZone event
                Debug.Log($"[UID] CounterEvent: Moving CardDisplay uid={gEvent.focusUid} off stack for graveyard");
                stackObjToHandle.transform.SetParent(displayCanvas.transform);
            } else {
                // StackObjDisplay (ability) - remove from UidToObj and destroy
                if (UidToObj.TryGetValue(gEvent.focusUid, out GameObject objInMap) && objInMap == stackObjToHandle) {
                    UidToObj.Remove(gEvent.focusUid);
                }
                Destroy(stackObjToHandle);
            }

            // Check if stack is now empty
            yield return null;
            if (stackView.transform.childCount == 0) {
                stackPanel.SetActive(false);
            }
        } else {
            Debug.LogWarning($"CounterEvent: Could not find stack object with uid {gEvent.focusUid}");
        }

        // Note: The countered card will be sent to graveyard by a separate SendToZone event
        gEventIsInProgress = false;
    }

    private IEnumerator GainControlEvent(GameEvent gEvent) {
        Debug.Assert(gEvent.focusCard != null, "There is no focusCard for this GainControl event");
        int cardUid = gEvent.focusCard.uid;

        if (!UidToObj.TryGetValue(cardUid, out GameObject cardObj)) {
            Debug.LogWarning($"GainControlEvent: Could not find card with uid {cardUid}");
            gEventIsInProgress = false;
            yield break;
        }

        CardDisplay cardDisplay = cardObj.GetComponent<CardDisplay>();
        if (cardDisplay == null) {
            Debug.LogWarning($"GainControlEvent: Object with uid {cardUid} is not a CardDisplay");
            gEventIsInProgress = false;
            yield break;
        }

        // Determine the new controller - isOpponent means the opponent gained control
        Participant newController = gEvent.isOpponent ? opponent : player;
        Participant oldController = gEvent.isOpponent ? player : opponent;

        // Get the card's slot (parent) and destroy it from old play zone
        Transform oldSlot = cardObj.transform.parent;
        cardObj.transform.SetParent(displayCanvas.transform);
        if (oldSlot != null) {
            Destroy(oldSlot.gameObject);
        }
        oldController.playZoneScaler.Rescale();

        // Create new slot in new controller's play zone
        GameObject newPlaySlot = Instantiate(playSlotPfb, newController.playZoneObj.transform);
        cardObj.transform.SetParent(newPlaySlot.transform);
        cardObj.transform.localPosition = Vector3.zero;

        // Scale appropriately
        float scaleFactor = newController.playZoneScaler.GetScaleFactor();
        newPlaySlot.GetComponent<RectTransform>().localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
        cardObj.GetComponent<RectTransform>().localScale = new Vector3(0.7f, 0.7f, 0.7f);

        // Update ownership
        cardDisplay.ownerIsOpponent = gEvent.isOpponent;

        // Play summon animation
        cardDisplay.PlaySummonAnim();
        newController.playZoneScaler.Rescale();

        yield return new WaitForSeconds((float)cardDisplay.summonVideoPlayer.clip.length);
        gEventIsInProgress = false;
    }

    private IEnumerator MillEvent(GameEvent gEvent) {
        GameObject newCardObj = CreateAndInitializeNewCardDisplay(gEvent.focusCard, displayCanvas.transform);
        CardDisplay newCardDisplay = newCardObj.GetComponent<CardDisplay>();      
        Animator newCardAnimator = newCardDisplay.GetComponent<Animator>();
        newCardAnimator.enabled = true;
        if (gEvent.isOpponent) {
            newCardAnimator.Play("MillOpp",-1, 0f);
            yield return new WaitForSeconds(0.2f);
        } else {
            newCardAnimator.Play("Mill",-1, 0f);
            yield return new WaitForSeconds(0.2f);
        }
        gEventIsInProgress = false;
    }

    private IEnumerator CastEvent(GameEvent gEvent) {
        CardDisplay castingCardDisplay;
        Debug.Assert(gEvent.focusStackObj != null, "there was no focusStackObj for this cast event");
        int cardUid = gEvent.focusStackObj.cardDisplayData.uid;
        Zone sourceZone = gEvent.sourceZone ?? Zone.Hand;

        // Handle casting from graveyard (or other non-hand zones)
        if (sourceZone == Zone.Graveyard) {
            // Remove and destroy the card from graveyard, create fresh for animation
            if (UidToObj.TryGetValue(cardUid, out GameObject existingObj)) {
                UidToObj.Remove(cardUid);
                Destroy(existingObj);
            }
            // Create new display for animation
            castingCardDisplay = CreateAndInitializeNewCardDisplay(gEvent.focusStackObj.cardDisplayData,
                displayCanvas.transform).GetComponent<CardDisplay>();
            castingCardDisplay.tempStackDisplayData = gEvent.focusStackObj;

            // For graveyard casts, just play ToStack directly
            Animator castingAnimator = castingCardDisplay.GetComponent<Animator>();
            castingAnimator.enabled = true;
            castingAnimator.Play("ToStack", -1, 0f);
            yield return new WaitForSeconds(castingAnimator.GetCurrentAnimatorStateInfo(0).length);
            gEventIsInProgress = false;
            yield break;
        }

        // Handle casting from hand (original logic)
        if (gEvent.isOpponent) {
            Opponent opp = opponent as Opponent;
            if (UidToObj.ContainsKey(cardUid)) {
                // Revealed card - found in UidToObj
                castingCardDisplay = UidToObj[cardUid].GetComponent<CardDisplay>();
                castingCardDisplay.RemoveFromHand();
                // Also remove from opponent's hand tracking
                opp?.handCards.Remove(castingCardDisplay);
            } else if (opp != null) {
                // Unrevealed card - use tracking to remove
                CardDisplay removedCard = opp.RemoveCardFromHand(cardUid);
                if (removedCard != null) {
                    GameObject cardSlot = removedCard.transform.parent.gameObject;
                    Destroy(cardSlot);
                }
                // Create new display for animation
                castingCardDisplay = CreateAndInitializeNewCardDisplay(gEvent.focusStackObj.cardDisplayData,
                    displayCanvas.transform).GetComponent<CardDisplay>();
            } else {
                // Fallback - shouldn't happen
                castingCardDisplay = CreateAndInitializeNewCardDisplay(gEvent.focusStackObj.cardDisplayData,
                    displayCanvas.transform).GetComponent<CardDisplay>();
            }
        } else {
            // Check if card exists in UidToObj (might not if cast from deck top)
            if (UidToObj.TryGetValue(gEvent.focusStackObj.cardDisplayData.uid, out GameObject cardObj)) {
                castingCardDisplay = cardObj.GetComponent<CardDisplay>();
                // Update with fresh server data (includes chosenIndices for highlighting choices)
                castingCardDisplay.UpdateCardDisplayData(gEvent.focusStackObj.cardDisplayData);
                castingCardDisplay.RemoveFromHand();
            } else {
                // Card not in UidToObj (e.g., cast from deck top) - create new display
                castingCardDisplay = CreateAndInitializeNewCardDisplay(gEvent.focusStackObj.cardDisplayData,
                    displayCanvas.transform).GetComponent<CardDisplay>();
            }
        }
        castingCardDisplay.tempStackDisplayData = gEvent.focusStackObj;
        Animator handCastAnimator = castingCardDisplay.GetComponent<Animator>();
        handCastAnimator.enabled = true;
        string animName = gEvent.isOpponent ? "FromHandOpp" : "FromHand";
        handCastAnimator.Play(animName, -1, 0f);
        yield return new WaitForSeconds(handCastAnimator.GetCurrentAnimatorStateInfo(0).length);
        handCastAnimator.Play("ToStack", -1, 0f);
        yield return new WaitForSeconds(handCastAnimator.GetCurrentAnimatorStateInfo(0).length);
        // The ToStack animation event calls CardDisplay.AddToStack() which reparents the card
        // to the stack (keeping UidToObj tracking intact for counter spell targeting)
        gEventIsInProgress = false;
    }

    private IEnumerator SummonEvent(GameEvent gEvent) {
        CardDisplay summonCardDisplay;
        CardDisplayData focusCard = gEvent.focusCard;

        // Check if this card is already on the stack (cast from hand)
        if (UidToObj.TryGetValue(focusCard.uid, out GameObject existingObj)) {
            CardDisplay existingCard = existingObj.GetComponent<CardDisplay>();
            if (existingCard != null && existingCard.transform.parent == stackView.transform) {
                // Card exists on stack - move it to play zone
                Debug.Log($"[UID] SummonEvent: Moving existing CardDisplay uid={focusCard.uid} from stack to play");
                summonCardDisplay = SummonFromStack(existingCard, gEvent.isOpponent);
            } else {
                // Card exists but not on stack - create new (fallback)
                summonCardDisplay = gEvent.isOpponent ? opponent.Summon(focusCard) : player.Summon(focusCard);
            }
        } else {
            // Card doesn't exist in UidToObj - create new
            summonCardDisplay = gEvent.isOpponent ? opponent.Summon(focusCard) : player.Summon(focusCard);
        }

        // Handle attacking summons
        if (!gEvent.isOpponent && gEvent.universalBool) {
            SelectAttackCapable(summonCardDisplay.dynamicReferencer.attackCapable, false);
        }

        float clipLength = summonCardDisplay.summonVideoPlayer.clip != null
            ? (float)summonCardDisplay.summonVideoPlayer.clip.length
            : 0f;
        yield return new WaitForSeconds(clipLength);
        gEventIsInProgress = false;
    }

    /// <summary>
    /// Moves an existing CardDisplay from the stack to the play zone.
    /// Used when summoning cards that were cast from hand (already have a CardDisplay on stack).
    /// </summary>
    private CardDisplay SummonFromStack(CardDisplay cardDisplay, bool isOpponent) {
        Participant targetParticipant = isOpponent ? opponent : player;
        RectTransform rt = cardDisplay.GetComponent<RectTransform>();

        // Create play slot and move the card there
        GameObject newPlaySlot = Instantiate(playSlotPfb, targetParticipant.playZoneObj.transform);
        cardDisplay.transform.SetParent(newPlaySlot.transform);

        // Reset anchors to center - VerticalLayoutGroup on stack controls anchors while cards are there
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);

        // Position and scale
        float scaleFactor = targetParticipant.playZoneScaler.GetScaleFactor();
        newPlaySlot.GetComponent<RectTransform>().localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
        rt.localScale = new Vector3(0.7f, 0.7f, 0.7f);
        cardDisplay.transform.localPosition = Vector3.zero;

        // Update UidToObj (already there, but ensure reference is correct)
        UidToObj[cardDisplay.card.uid] = cardDisplay.gameObject;

        // Set owner flag for opponent cards
        cardDisplay.ownerIsOpponent = isOpponent;

        // Disable selectableCardObj raycast - only used for card selection dialogues, not in-play cards
        if (cardDisplay.selectableCardObj != null) {
            cardDisplay.selectableCardObj.GetComponent<SelectableCard>()?.Deactivate();
        }

        // Play summon animation
        cardDisplay.PlaySummonAnim();

        targetParticipant.playZoneScaler.Rescale();

        return cardDisplay;
    }

    private IEnumerator SendToZoneEvent(GameEvent gEvent) {
        GameObject existingCardObj = null;
        if (gEvent.sourceZone != null && gEvent.sourceZone != Zone.Deck) {
            Debug.Assert(gEvent.focusCard != null, "there is no card for sendToZone Event");

            // For Stack source zone, card may be a CardDisplay (moved to displayCanvas by CounterEvent)
            // or already destroyed (old StackObjDisplay behavior)
            if (gEvent.sourceZone == Zone.Stack) {
                // Try to find existing CardDisplay (cards on stack now stay as CardDisplay)
                if (UidToObj.TryGetValue(gEvent.focusCard.uid, out existingCardObj)) {
                    CardDisplay cardDisplay = existingCardObj.GetComponent<CardDisplay>();
                    if (cardDisplay != null) {
                        Debug.Log($"[SendToZone Debug] Found existing CardDisplay uid={gEvent.focusCard.uid} from stack");
                        // Card is already in displayCanvas from CounterEvent, ready for animation
                    } else {
                        // Not a CardDisplay, must be old StackObjDisplay - shouldn't happen anymore
                        existingCardObj = null;
                    }
                } else {
                    Debug.Log($"[SendToZone Debug] Card uid={gEvent.focusCard.uid} not in UidToObj for Stack source");
                    existingCardObj = null;
                }
            } else if (gEvent.sourceZone == Zone.Exile || gEvent.sourceZone == Zone.Graveyard) {
                // Find and destroy the card in exile/graveyard, create fresh for animation
                if (UidToObj.TryGetValue(gEvent.focusCard.uid, out existingCardObj)) {
                    UidToObj.Remove(gEvent.focusCard.uid);
                    Destroy(existingCardObj);
                } else {
                    Debug.LogWarning($"[SendToZone Debug] Card uid {gEvent.focusCard.uid} ({gEvent.focusCard.name}) not found in UidToObj for sourceZone {gEvent.sourceZone}");
                }
                existingCardObj = null;
            } else {
                if (!UidToObj.TryGetValue(gEvent.focusCard.uid, out existingCardObj)) {
                    Debug.LogWarning($"[SendToZone Debug] Card uid {gEvent.focusCard.uid} ({gEvent.focusCard.name}) not found in UidToObj for sourceZone {gEvent.sourceZone}");
                    existingCardObj = null;
                } else {
                    UidToObj.Remove(gEvent.focusCard.uid);
                }
                // Only process source zone cleanup if we found the card object
                if (existingCardObj != null) {
                    switch (gEvent.sourceZone) {
                        case Zone.Play:
                            // Clean up attack visuals if this unit was attacking
                            if (attackUids.ContainsKey(gEvent.focusCard.uid)) {
                                bool isOppAttack = existingCardObj.transform.IsChildOf(opponent.playZoneObj.transform);
                                UnDisplayAttack(gEvent.focusCard.uid, isOppAttack);
                            }
                            Transform cardSlot = existingCardObj.transform.parent;
                            Destroy(cardSlot.parent.name == "PlayZone" ? cardSlot.gameObject : existingCardObj);
                            existingCardObj = null; // Card was destroyed, don't reuse
                            break;
                        case Zone.Hand:
                            CardDisplay handCard = existingCardObj.GetComponent<CardDisplay>();
                            // If this is an opponent's revealed card, also remove from tracking
                            if (gEvent.isOpponent) {
                                Opponent oppTracking = opponent as Opponent;
                                oppTracking?.handCards.Remove(handCard);
                            }
                            // For Hand->Deck, destroy and create fresh to ensure correct anchoring for animation
                            if (gEvent.zone == Zone.Deck) {
                                handCard.RemoveFromHand();
                                Destroy(existingCardObj);
                                existingCardObj = null;
                            } else {
                                handCard.RemoveFromHand();
                                // Card still exists, will be reused for animation
                            }
                            break;
                        default:
                            Destroy(existingCardObj);
                            existingCardObj = null;
                            break;
                    }
                } else if (gEvent.sourceZone == Zone.Hand && gEvent.isOpponent) {
                    // Card not found in UidToObj - it's from opponent's hand
                    // Use proper tracking to remove the card
                    Opponent opp = opponent as Opponent;
                    if (opp != null) {
                        int? cardUid = gEvent.focusCard?.uid;
                        CardDisplay removedCard = opp.RemoveCardFromHand(cardUid);
                        if (removedCard != null) {
                            // Destroy the card slot (parent) and the card
                            GameObject cardSlot = removedCard.transform.parent.gameObject;
                            Destroy(cardSlot);
                        }
                    }
                }
                // Animation will create a temporary card object below
            }
        }

        string animationName;
        float sendToZoneDelay = eventDelayLong;
        bool isTemp = false;
        switch (gEvent.zone) {
            case Zone.Hand:
                animationName = gEvent.isOpponent ? "ToHandOpp" : "ToHand";
                break;
            case Zone.Deck:
                sendToZoneDelay = eventDelaySuperShort;
                animationName = gEvent.isOpponent ? "ToDeckOpp" : "ToDeck";
                isTemp = true;
                break;
            case Zone.Graveyard:
                sendToZoneDelay = eventDelayShort;
                animationName = gEvent.isOpponent ? "ToGraveOpp" : "ToGrave";
                break;
            case Zone.Exile:
                sendToZoneDelay = eventDelayShort;
                animationName = gEvent.isOpponent ? "ToExileOpp" : "ToExile";
                break;
            case Zone.Play:
                sendToZoneDelay = eventDelayShort;
                animationName = gEvent.isOpponent ? "ToPlayOpp" : "ToPlay";
                break;
            default:
                Debug.Log("Zone doesn't exist for SendToZone Event");
                animationName = "";
                break;
        }

        // Reuse existing card from hand if available, otherwise create new display
        GameObject cardObjForAnimation;
        if (existingCardObj != null) {
            cardObjForAnimation = existingCardObj;
        } else {
            cardObjForAnimation = CreateAndInitializeNewCardDisplay(gEvent.focusCard, displayCanvas.transform, isTemp);
        }

        Animator cardAnimator = cardObjForAnimation.GetComponent<Animator>();
        cardAnimator.enabled = true;
        cardAnimator.Play(animationName, -1, 0f);
        yield return new WaitForSeconds(sendToZoneDelay);

        // Re-add to UidToObj if card went to play (so RefreshCardDisplays can find it)
        if (gEvent.zone == Zone.Play && gEvent.focusCard != null) {
            UidToObj[gEvent.focusCard.uid] = cardObjForAnimation;
        }

        gEventIsInProgress = false;
    }

    private void DisplayLookAtDeckDialogue(GameEvent gEvent) {
        cardSelectionDialogue.SetActive(true);
        Debug.Assert(gEvent.cards != null,
            "there are no cards to look at for this look at deck event");
        foreach (CardDisplayData cdd in gEvent.cards) {
            GameObject newCardDisplayObj = CreateAndInitializeNewCardDisplay(cdd, cardSelectionView.transform, true);
            cardSelectionManager.cDisplaysToSelectFrom.Add(newCardDisplayObj.GetComponent<CardDisplay>());
            newCardDisplayObj.GetComponent<CardDisplay>().selectableCardObj.SetActive(true);
        }
        cardSelectionManager.InitializeFirstDestination(gEvent);
    }

    private void DisplayPeekDialogue(GameEvent gEvent) {
        cardSelectionDialogue.SetActive(true);
        Debug.Assert(gEvent.cards != null,
            "there are no cards to peek at for this peek event");
        foreach (CardDisplayData cdd in gEvent.cards) {
            GameObject newCardDisplayObj = CreateAndInitializeNewCardDisplay(cdd, cardSelectionView.transform, true);
            cardSelectionManager.cDisplaysToSelectFrom.Add(newCardDisplayObj.GetComponent<CardDisplay>());
        }
        // For peek, just show cards and use OK button to continue (no selection needed)
        cardSelectionManager.InitializePeek();
    }

    public GameObject CreateAndInitializeNewCardDisplay(CardDisplayData cdd, Transform parentTransform, bool isTemp = false) {
        GameObject newCardDisplayObj = Instantiate(cardDisplayPfb, parentTransform);
        if (cdd != null) {
            newCardDisplayObj.name = cdd.name + "(" + cdd.uid + ")";
        }
        CardDisplay newCardDisplay = newCardDisplayObj.GetComponent<CardDisplay>();
        // Temp cards don't track in UidToObj - they're just for display, the real card stays tracked
        newCardDisplay.UpdateCardDisplayData(cdd, trackInUidToObj: !isTemp);
        return newCardDisplayObj;
    }
    
    // TODO create PlaySlot initializer to normalize code from CardDisplay.AddToPlay() and Participant.Summon()
    // public GameObject CreateAndInitializeNewPlaySlot(CardDisplayData cdd)
    
    /// <summary>
    /// Adds an ability/trigger to the stack using StackObjDisplay.
    /// Note: Cards use CardDisplay.AddToStack() which reparents the original card to the stack.
    /// Abilities use this method because they don't have a card identity to preserve.
    /// </summary>
    private void AddToStack(StackDisplayData stackDisplayData) {
        // Track stack objects for counter spell targeting
        CreateNewStackObj(stackDisplayData, stackView.transform, trackInUidToObj: true);
    }

    // -------
    // Flag to skip the first CheckForMatchStateChanges when coming from lobby
    // (because we already fetched the MatchState with events in InitializeGame)
    private bool skipFirstMatchStateCheck = false;

    private void InitializeGame() {
        gameData = GameObject.Find("GameData").GetComponent<GameData>();
        gameData.accountData = GameObject.Find("AccountData").GetComponent<AccountDataGO>().accountData;

        // If coming from lobby, matchState is null but lobbyMatchId is set
        // Fetch the initial matchState here (this is the first fetch, so events will be included)
        if (gameData.matchState == null && gameData.lobbyMatchId.HasValue) {
            gameData.matchState = serverApi.GetMatchState(gameData.accountData, gameData.lobbyMatchId.Value);
            gameData.lobbyMatchId = null;  // Clear after use
            // Skip the first CheckForMatchStateChanges since we just fetched and it would overwrite our events
            skipFirstMatchStateCheck = true;
        }

        localPhase = Phase.Draw;
    }
    
    private void InitializePlayers() {
        player.Initialize(gameData.matchState.playerState);
        opponent.Initialize(gameData.matchState.opponentState);

        // Update revealed top card (from TopCardRevealed passive) - only for player, not opponent
        player.UpdateRevealedTopCard(gameData.matchState.playerState.revealedTopCard);
    }
    
    public void QuitGame() {
        LeaveCurrentMatch();
        Application.Quit();
    }

    // testing
    public void TogglePauseMenu() {
        pauseMenu.SetActive(!pauseMenu.activeSelf);
    }

    #region Quit to Menu

    private GameObject quitConfirmPanel;

    private Transform PausePanel => pauseMenu != null ? pauseMenu.transform.Find("Panel") : null;

    // Wired at runtime so the scene doesn't need re-editing
    private void WireQuitButtons() {
        Transform mainMenuBtn = PausePanel?.Find("MainMenuBtn");
        if (mainMenuBtn == null) {
            Debug.LogWarning("[QuitToMenu] MainMenuBtn not found under pause menu");
            return;
        }
        Button btn = mainMenuBtn.GetComponent<Button>();
        btn.interactable = true;  // disabled in the scene from before the feature existed
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(ShowQuitConfirmation);
    }

    public void ShowQuitConfirmation() {
        if (quitConfirmPanel == null) BuildQuitConfirmation();
        quitConfirmPanel.SetActive(true);
    }

    private void BuildQuitConfirmation() {
        // Full-screen dim overlay, parented to the pause menu so closing the menu hides it too
        quitConfirmPanel = new GameObject("QuitConfirmPanel", typeof(RectTransform), typeof(Image));
        RectTransform rt = (RectTransform)quitConfirmPanel.transform;
        rt.SetParent(pauseMenu.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        quitConfirmPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

        GameObject box = new GameObject("Box", typeof(RectTransform), typeof(Image));
        RectTransform boxRt = (RectTransform)box.transform;
        boxRt.SetParent(rt, false);
        boxRt.sizeDelta = new Vector2(560, 240);
        box.GetComponent<Image>().color = new Color(0.12f, 0.13f, 0.16f, 0.98f);

        GameObject msg = new GameObject("Message", typeof(RectTransform));
        RectTransform msgRt = (RectTransform)msg.transform;
        msgRt.SetParent(boxRt, false);
        msgRt.anchorMin = new Vector2(0f, 0.45f);
        msgRt.anchorMax = new Vector2(1f, 1f);
        msgRt.offsetMin = new Vector2(20f, 0f);
        msgRt.offsetMax = new Vector2(-20f, -15f);
        TextMeshProUGUI tmp = msg.AddComponent<TextMeshProUGUI>();
        tmp.text = "Are you sure you want to quit?\nThis will concede the match.";
        tmp.fontSize = 30;
        tmp.alignment = TextAlignmentOptions.Center;

        Transform template = PausePanel?.Find("MainMenuBtn");
        CreateConfirmButton(template, boxRt, "Yes, quit", new Vector2(0.27f, 0f), ConfirmQuitToMenu);
        CreateConfirmButton(template, boxRt, "Cancel", new Vector2(0.73f, 0f), () => quitConfirmPanel.SetActive(false));
    }

    private void CreateConfirmButton(Transform template, RectTransform parent, string label,
                                     Vector2 anchor, UnityEngine.Events.UnityAction onClick) {
        GameObject go;
        if (template != null) {
            go = Instantiate(template.gameObject, parent);
        } else {
            go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.GetComponent<Image>().color = new Color(0.25f, 0.3f, 0.4f);
            GameObject txt = new GameObject("Text (TMP)", typeof(RectTransform));
            txt.transform.SetParent(go.transform, false);
            TextMeshProUGUI t = txt.AddComponent<TextMeshProUGUI>();
            t.alignment = TextAlignmentOptions.Center;
            t.fontSize = 26;
        }
        go.name = label;
        RectTransform brt = (RectTransform)go.transform;
        brt.SetParent(parent, false);
        brt.anchorMin = brt.anchorMax = anchor;
        brt.pivot = new Vector2(0.5f, 0f);
        brt.anchoredPosition = new Vector2(0f, 25f);
        brt.sizeDelta = new Vector2(210f, 60f);
        TextMeshProUGUI labelTmp = go.GetComponentInChildren<TextMeshProUGUI>();
        if (labelTmp != null) labelTmp.text = label;
        Button b = go.GetComponent<Button>();
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(onClick);
    }

    public void ConfirmQuitToMenu() {
        StopAllCoroutines();  // stop match-state polling and any running animations
        LeaveCurrentMatch();
        UnityEngine.SceneManagement.SceneManager.LoadScene("Main Menu");
    }

    // Tell the server we're conceding; safe to call even if the match already ended
    private void LeaveCurrentMatch() {
        try {
            AccountData acct = gameData.accountData;
            if (acct == null) acct = GameObject.Find("AccountData").GetComponent<AccountDataGO>().accountData;
            if (gameData.matchState != null) {
                serverApi.LeaveMatch(acct, gameData.matchState.matchId);
            }
        } catch (Exception e) {
            Debug.LogWarning("[QuitToMenu] LeaveMatch failed: " + e.Message);
        }
    }

    #endregion
    
    public static bool DictionariesEqual<TKey, TValue>(
        Dictionary<TKey, List<TValue>> dict1, 
        Dictionary<TKey, List<TValue>> dict2)
    {
        if (dict1.Count != dict2.Count)
            return false;

        foreach (var kvp in dict1)
        {
            if (!dict2.TryGetValue(kvp.Key, out var list2))
                return false;

            var list1 = kvp.Value;
            if (!list1.SequenceEqual(list2))
                return false;
        }
        return true;
    }

    public void AttemptToCast(int cardUid) {
        LosePrio();
        serverApi.AttemptToCast(gameData.accountData, gameData.matchState.matchId, cardUid);
        WaitForEvents();
    }
    
    public void DisplayActivationVerification(CardDisplay cardDisplay) {
        // Close card group panel if open (e.g., activating from graveyard inspection)
        CloseCardGroupPanel();
        currentActivatedAbilityCdd = cardDisplay;
        currentActivatedTokenUid = null;
        currentActivatedTokenDisplay = null;
        if (activationVerificationText != null) {
            activationVerificationText.text = "Activate this card's ability?";
        }
        activationVerificationPanel.SetActive(true);
    }

    public void DisplayTokenActivationVerification(TokenDisplay tokenDisplay, int tokenUid) {
        currentActivatedAbilityCdd = null;
        currentActivatedTokenUid = tokenUid;
        currentActivatedTokenDisplay = tokenDisplay;
        if (activationVerificationText != null) {
            activationVerificationText.text = "Activate this card's ability?";
        }
        activationVerificationPanel.SetActive(true);
    }

    public void CancelAbilityActivation() {
        currentActivatedAbilityCdd = null;
        currentActivatedTokenUid = null;
        currentActivatedTokenDisplay = null;
        currentDeckTopCastCard = null;
        activationVerificationPanel.SetActive(false);
    }

    public void AttemptToActivate() {
        // Handle deck top casting (Sky Scryer)
        if (currentDeckTopCastCard != null) {
            AttemptToCastDeckTop();
            return;
        }

        LosePrio();
        // Determine which UID to use - card or token
        int uidToActivate = currentActivatedAbilityCdd != null
            ? currentActivatedAbilityCdd.card.uid
            : currentActivatedTokenUid.Value;
        serverApi.AttemptToActivate(gameData.accountData, gameData.matchState.matchId, uidToActivate);
        activationVerificationPanel.SetActive(false);
        currentActivatedAbilityCdd = null;
        currentActivatedTokenUid = null;
        currentActivatedTokenDisplay = null;
        WaitForEvents();
    }

    public void DisplayDeckTopCastVerification(CardDisplay cardDisplay) {
        currentDeckTopCastCard = cardDisplay;
        currentActivatedAbilityCdd = null;
        currentActivatedTokenUid = null;
        currentActivatedTokenDisplay = null;
        if (activationVerificationText != null) {
            activationVerificationText.text = $"Cast {cardDisplay.card.name}?";
        }
        activationVerificationPanel.SetActive(true);
    }

    private void AttemptToCastDeckTop() {
        LosePrio();
        int cardUid = currentDeckTopCastCard.card.uid;
        serverApi.AttemptToCast(gameData.accountData, gameData.matchState.matchId, cardUid);
        activationVerificationPanel.SetActive(false);
        currentDeckTopCastCard = null;
        WaitForEvents();
    }

    public void SendCardSelection(List<List<int>> destinationCardUids) {
        cardSelectionDialogue.SetActive(false);
        serverApi.SendCardSelection(gameData.accountData, gameData.matchState.matchId, destinationCardUids);
        gEventIsInProgress = false;
    }
    
    public Vector3 GetMouseWorldPositionWithZAs(float zPos) {
        Vector3 mousePos = Input.mousePosition;
        Vector3 mouseToWorldPos = mainCam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y));
        return new Vector3(mouseToWorldPos.x, mouseToWorldPos.y, zPos);
    }

    private void ActivateAttackables(List<int> uids) {
        foreach (int uid in uids) {
            if (!UidToObj.TryGetValue(uid, out GameObject obj)) continue;
            DynamicReferencer dRef = obj.GetComponent<DynamicReferencer>();
            if (dRef == null) continue;
            dRef.EnableAttackable();
            attackableDRefs.Add(dRef);
        }
    }

    private void DeactivateAttackables() {
        // attackable reference is required to cover both cards and players (which is why we don't use CardDisplay.DisableAttackable here)
        foreach (DynamicReferencer dRef in attackableDRefs) {
            dRef.DisableAllInteractable();
        }
        attackableDRefs.Clear();
    }

    private void ActivateAttackCapables() {
        foreach (int uid in attackCapableUids) {
            if (UidToObj.TryGetValue(uid, out GameObject cardObj)) {
                CardDisplay cardDisplay = cardObj.GetComponent<CardDisplay>();
                if (cardDisplay != null) {
                    cardDisplay.EnableAttackCapable();
                }
            }
        }
    }

    public CardDisplay CardDisplayFromUid(int uid) {
        if(!UidToObj[uid].TryGetComponent(out CardDisplay cDisplay))
            throw new Exception($"No CardDisplay associated with uid {uid}");
        return cDisplay;
    }

    private GameObject CreateAttackArrow((int, int) attackUidPair) {
        Vector3 attackArrowStartPosition = GetCardTargetLocation(attackUidPair.Item1);
        Vector3 attackArrowEndPosition = GetCardTargetLocation(attackUidPair.Item2);
        // set Z pos to be visible (necessary for line renderer[arrow])
        Vector3 startPos = new Vector3(attackArrowStartPosition.x, attackArrowStartPosition.y, -1);
        Vector3 endPos = new Vector3(attackArrowEndPosition.x, attackArrowEndPosition.y, -1);
        // create arrow
        GameObject attackArrow = Instantiate(targetingArrowPfb, displayCanvas.transform);
        LineRenderer arrowLineRenderer = attackArrow.GetComponent<LineRenderer>();
        arrowLineRenderer.material = arrowAttackMat;
        // set arrow position
        arrowLineRenderer.SetPosition(0, startPos);
        arrowLineRenderer.SetPosition(1, endPos);
        return attackArrow;
    }

    private void RefreshAttackArrows() {
        // possible reference issues
        foreach (var attackerToArrow in attackerToAttackArrow) {
            Destroy(attackerToArrow.Value);
        }
        attackerToAttackArrow.Clear();
        foreach (var pair in attackUids) {
            if (!UidToObj.TryGetValue(pair.Key, out GameObject attackerObj)) continue;
            if (!UidToObj.ContainsKey(pair.Value)) continue; // defender must exist too
            GameObject newAttArrow = CreateAttackArrow((pair.Key, pair.Value));
            attackerToAttackArrow.Add(attackerObj, newAttArrow);
        }
    }

    private Vector3 GetCardTargetLocation(int uid) {
        if (!UidToObj.TryGetValue(uid, out GameObject obj)) return Vector3.zero;
        CardDisplay cDisplay = obj.GetComponent<CardDisplay>();
        Participant participant = obj.GetComponent<Participant>();
        if (cDisplay != null) {
            return cDisplay.targetingLocationObj.transform.position;
        }
        if (participant != null) {
            return participant.attackableObj.transform.position;
        }
        Debug.Log("There was not a valid attack target for the attack arrow location.");
        return Vector3.zero;
    }
    

    public void SendAttackToServer() {
        serverApi.Attack(gameData.accountData, gameData.matchState.matchId, attackUids);
        LosePrio();
        WaitForEvents();
    }

    public void ResetAttackReferences() {
        attackingAttackCapable = null;
        attackCapableUids.Clear();
        attackableDRefs.Clear();
        isGroundTacticsMode = false;
    }

    public void SelectAttackCapable(AttackCapable attackCapable, bool selectedByPlayer = true) {
        // set references for attackables when selected
        attackingAttackCapable = attackCapable;
        // activate targeting arrow
        Transform acTransform = attackCapable.GetComponent<Transform>();
        Vector3 cardPosition = new Vector3(acTransform.position.x, acTransform.position.y, -1);
        ActivateTargetingArrow(cardPosition);
        actionButtonComponent.interactable = false;
        DisableAllInteractables();
        // activate opponent attackables
        ActivateAttackables(serverApi.GetAttackables(gameData.accountData, gameData.matchState.matchId, attackCapable.cardDisplay.card.uid));
        if (selectedByPlayer) {
            attackCapable.gameObject.SetActive(true);
        } else {
            isSecondaryAttack = true;
        }
    }

    public void DeselectAttackCapable() {
        attackingAttackCapable = null;
        targetingArrow.SetActive(false);
        if(attackUids.Count < 1) actionButton.SetButtonType(ActionButtonType.Pass);

        // In Ground Tactics mode, disable pass until all attackers are assigned
        if (isGroundTacticsMode) {
            int unassignedCount = attackCapableUids.Count(uid => !attackUids.ContainsKey(uid));
            actionButtonComponent.interactable = unassignedCount == 0;
        } else {
            actionButtonComponent.interactable = true;
        }

        DeactivateAttackables();
        // activate attackCapables
        ActivateAttackCapables();
    }
    
    public void AssignAttack(Attackable attackable) {
        CardDisplay attackingCardDisplay = attackingAttackCapable.cardDisplay;
        // toggle highlights
        attackingCardDisplay.selectedHighlight.SetActive(false);
        attackingCardDisplay.attackingHighlight.SetActive(true);
        // disable mouse targeting arrow
        targetingArrow.SetActive(false);
        // display the attack and add attack references
        DisplayAttack((attackingCardDisplay.card.uid, attackable.dynamicReferencer.uid), isGroundTacticsMode);
        // normal combat attack
        var assigningUids = (attackingAttackCapable.cardDisplay.card.uid, attackable.dynamicReferencer.uid);
        if (!isSecondaryAttack) {
            serverApi.AssignAttack(gameData.accountData, gameData.matchState.matchId, assigningUids);
            actionButton.SetButtonType(ActionButtonType.Attack);

            // In Ground Tactics mode, only enable button when all attackers are assigned
            if (isGroundTacticsMode) {
                // After this assignment, check if all attackers are now assigned
                // Note: attackUids is updated by DisplayAttack before this
                int unassignedCount = attackCapableUids.Count(uid => !attackUids.ContainsKey(uid));
                actionButtonComponent.interactable = unassignedCount == 0;
            } else {
                actionButtonComponent.interactable = true;
            }
        } else {
            // secondary combat (attacking tokens summoned)
            serverApi.AddSecondaryAttacker(gameData.accountData, gameData.matchState.matchId, 
                assigningUids);
            isSecondaryAttack = false;
        }
        // finish by deactivating attackables
        DeactivateAttackables();
        ActivateAttackCapables();
    }

    public void UnAssignAttack(AttackCapable attackCapable) {
        serverApi.UnAssignAttack(gameData.accountData, gameData.matchState.matchId, attackCapable.cardDisplay.card.uid);
        UnDisplayAttack(attackCapable.cardDisplay.card.uid, isGroundTacticsMode);
        // deselect attackable
        attackCapable.Deselect();
    }

    private void DisplayAttack((int, int) attackUidPair, bool isOpponent) {
        if (!UidToObj.TryGetValue(attackUidPair.Item1, out GameObject attackingObj)) return;
        if (!UidToObj.ContainsKey(attackUidPair.Item2)) return; // defender must exist
        float localYAdjust = attackYAdjust;
        // reverse the YAdjust for opponent attacks
        if(isOpponent) localYAdjust = -localYAdjust;
        // adjust Y pos
        RectTransform attackingObjRectTransform = attackingObj.GetComponent<RectTransform>();
        Vector2 attackingPos = attackingObjRectTransform.anchoredPosition;
        attackingObjRectTransform.anchoredPosition = new Vector2(attackingPos.x, attackingPos.y + localYAdjust);
        // create attack arrow
        // TODO reorder attackers so that they are separate from non-attacking summons and update their attack arrows
        GameObject attackArrow = CreateAttackArrow(attackUidPair);
        // add to references
        attackerToAttackArrow.Add(attackingObj, attackArrow);
        attackUids.Add(attackUidPair.Item1, attackUidPair.Item2);
    }

    private void UnDisplayAttack(int attackerUid, bool isOpponent = false) {
        if (!UidToObj.TryGetValue(attackerUid, out GameObject attackerObj)) {
            attackUids.Remove(attackerUid);
            return;
        }
        float localYAdjust = attackYAdjust;
        // reverse the YAdjust for opponent attacks
        if(isOpponent) localYAdjust = -localYAdjust;
        // destroy the attack arrow
        if (attackerToAttackArrow.TryGetValue(attackerObj, out GameObject arrow)) {
            Destroy(arrow);
            attackerToAttackArrow.Remove(attackerObj);
        }
        // move the card y position back down
        RectTransform baseObjRectTransform = attackerObj.GetComponent<RectTransform>();
        Vector2 baseObjPos = baseObjRectTransform.anchoredPosition;
        baseObjRectTransform.anchoredPosition = new Vector2(baseObjPos.x, baseObjPos.y - localYAdjust);
        attackUids.Remove(attackerUid);
    }

    private void ActivateTargetingArrow(Vector3 cardPosition) {
        LineRenderer taLineRenderer = targetingArrow.GetComponent<LineRenderer>();
        taLineRenderer.SetPosition(0, cardPosition);
        taLineRenderer.SetPosition(1, GetMouseWorldPositionWithZAs(0));
        targetingArrow.SetActive(true);
    }


    public void SetCurrentPassToPhase(int phaseIndex) {
        // Block if a selection/targeting prompt is active
        if (IsPromptActive()) {
            Debug.Log("[SetCurrentPassToPhase] Blocked - a selection/targeting prompt is active");
            return;
        }

        // pass to my main
        if (phaseIndex == 6) {
            passToMyMain = true;
            phaseToPassTo = Phase.Main;
        } else {
            passToMyMain = false;
            phaseToPassTo = (Phase)phaseIndex;
        }
        autoPass = true;
        // User explicitly clicked a phase button - clear the pause flag to resume autopass
        autopassPausedForStack = false;
        Debug.Log($"[SetCurrentPassToPhase] phaseIndex={phaseIndex}, phaseToPassTo={phaseToPassTo}, passToMyMain={passToMyMain}, autoPass={autoPass}");
        if (actionButtonComponent.interactable) {
            PassPrio();
        }
        PhaseButtonManager.SetStopBorder(phaseIndex);
    }

    private bool IsPromptActive() {
        // Check if any selection/targeting prompt is active
        if (currentSelectionType != ActionButtonType.Pass) return true;
        if (possibleSelectables.Count > 0) return true;
        if (cardSelectionDialogue.activeSelf) return true;
        if (amountSelectionPanel.activeSelf) return true;
        if (activationVerificationPanel.activeSelf) return true;
        return false;
    }

    private List<DynamicReferencer> GetAllDynamicReferencers() {
        List<DynamicReferencer> referencers = new List<DynamicReferencer>();
        foreach (var pair in UidToObj) {
            referencers.Add(pair.Value.GetComponent<DynamicReferencer>());
        }
        return referencers;
    }
    
    public void EnableUnselectedSelectables() {
        Debug.Log($"[Counter Debug] EnableUnselectedSelectables called. possibleSelectables: [{string.Join(", ", possibleSelectables)}]");
        foreach (DynamicReferencer dRef in GetAllDynamicReferencers()) {
            Debug.Log($"[Counter Debug] Checking dRef uid={dRef.uid}, isSelected={isSelected(dRef)}, isSelectable={isSelectable(dRef)}");
            if (dRef == null) {
                Debug.Log($"[Counter Debug] dRef is null, skipping");
                continue;
            }
            if (isSelected(dRef)) continue;
            if (!isSelectable(dRef)) continue;
            Debug.Log($"[Counter Debug] Enabling selectable for uid={dRef.uid}");
            // Check if this is a card in hand (not a player or token)
            if (dRef.cardDisplay != null && dRef.tokenDisplay == null && CardIsInHand(dRef.cardDisplay, out var cSlot)) {
                cSlot.EnableSelectable();
            } else {
                dRef.EnableSelectable();
            }
        }
        // Also enable selectables in card group view (graveyard/exile inspection)
        EnableUnselectedSelectablesInCardGroup();
    }

    private bool CardIsInHand(CardDisplay cDisplay, out CardSlot cSlot) {
        if (cDisplay.transform.parent.TryGetComponent(out CardSlot cardSlot)) {
            cSlot = cardSlot;
            return true;
        }
        cSlot = null;
        return false;
    }


    public void DisablesSelectables() {
        selectTextObj.SetActive(false);
        foreach (DynamicReferencer dRef in GetAllDynamicReferencers()) {
            dRef.DisableAllInteractable();
        }
    }

    public void SendSelection() {
        Debug.Log($"[Selection Debug] SendSelection called. Type: {currentSelectionType}, SelectedUids: [{string.Join(", ", selectedUids)}]");
        // reset possible selectables
        possibleSelectables.Clear();
        // send selected Uids based on the selection type
        switch (currentSelectionType) {
            case ActionButtonType.Cost:
                serverApi.SendCostSelection(gameData.accountData, gameData.matchState.matchId, selectedUids);
                break;
            case ActionButtonType.Target:
                Debug.Log($"[Selection Debug] Sending target selection with UIDs: [{string.Join(", ", selectedUids)}]");
                serverApi.SendTargetSelection(gameData.accountData, gameData.matchState.matchId, selectedUids);
                break;
            case ActionButtonType.Tribute:
                serverApi.SendTributeSelection(gameData.accountData, gameData.matchState.matchId, selectedUids);
                break;
            default:
                Debug.Log("unknown selection type");
                return;
        }
        // reset selected selectables
        selectedUids.Clear();
        currentSelectionType = 0;
        currentSelectionMax = 0;
        variableSelection = false;
        // reset tribute values
        tributeValues.Clear();
        currentTributeValue = 0;
        // disable interactables
        LosePrio();
        gEventIsInProgress = false;
    }

    public void SendRitualChoice(int summonUid) {
        Debug.Log($"[RitualOfDarkness] SendRitualChoice called with uid: {summonUid}");
        // Reset selection state
        possibleSelectables.Clear();
        selectedUids.Clear();
        currentSelectionType = 0;
        currentSelectionMax = 0;
        selectTextObj.SetActive(false);
        // Send to server
        serverApi.SendRitualChoice(gameData.accountData, gameData.matchState.matchId, summonUid);
        // Disable interactables and wait
        LosePrio();
        gEventIsInProgress = false;
    }

    public int GetSelectedRitualSummonUid() {
        // Return the first selected UID (there should only be one for ritual selection)
        return selectedUids.Count > 0 ? selectedUids[0] : -1;
    }

    public void DisableUnselectedSelectables() {
        foreach (DynamicReferencer dRef in GetAllDynamicReferencers()) {
            // disable all unselected selectables
            if (isSelected(dRef)) continue;
            dRef.DisableAllInteractable();
        }
        // Also disable selectables in card group view (graveyard/exile inspection)
        DisableUnselectedSelectablesInCardGroup();
    }

    /// <summary>
    /// Disable selectables from a specific list of UIDs (used for requireOneFromEach targeting)
    /// </summary>
    public void DisableSelectablesFromList(List<int> uidsToDisable) {
        foreach (DynamicReferencer dRef in GetAllDynamicReferencers()) {
            if (isSelected(dRef)) continue;
            // Check if this dRef's uid is in the list to disable
            if (dRef.tokenDisplay != null) {
                if (dRef.tokenDisplay.tokenUids.Any(uid => uidsToDisable.Contains(uid))) {
                    dRef.DisableAllInteractable();
                }
            } else if (uidsToDisable.Contains(dRef.uid)) {
                dRef.DisableAllInteractable();
            }
        }
    }

    /// <summary>
    /// Enable selectables from a specific list of UIDs (used for requireOneFromEach targeting)
    /// </summary>
    public void EnableSelectablesFromList(List<int> uidsToEnable) {
        foreach (DynamicReferencer dRef in GetAllDynamicReferencers()) {
            if (isSelected(dRef)) continue;
            // Check if this dRef's uid is in the list to enable
            if (dRef.tokenDisplay != null) {
                if (dRef.tokenDisplay.tokenUids.Any(uid => uidsToEnable.Contains(uid))) {
                    dRef.EnableSelectable();
                }
            } else if (uidsToEnable.Contains(dRef.uid)) {
                dRef.EnableSelectable();
            }
        }
    }

    private bool isSelected(DynamicReferencer dRef) {
        if (dRef.tokenDisplay != null) {
            if (dRef.tokenDisplay.tokenUids.Any(uid => selectedUids.Contains(uid))) return true;
        } else if (selectedUids.Contains(dRef.uid)) return true;
        return false;
    }

    private bool isSelectable(DynamicReferencer dRef) {
        if (dRef.tokenDisplay != null) {
            if (dRef.tokenDisplay.tokenUids.Any(uid => possibleSelectables.Contains(uid))) return true;
        } else if (possibleSelectables.Contains(dRef.uid)) return true;
        return false;
    }

    /// <summary>
    /// Get the tribute value for a specific UID. Returns 1 if not in tributeValues dict.
    /// </summary>
    public int GetTributeValue(int uid) {
        return tributeValues.TryGetValue(uid, out int value) ? value : 1;
    }

    /// <summary>
    /// Get remaining tribute value needed to reach currentSelectionMax.
    /// </summary>
    public int GetRemainingTributeNeeded() {
        return currentSelectionMax - currentTributeValue;
    }

    public void DisplayAmountSelector(Action<int> action, Action cancelAction = null, int? maxOverride = null) {
        amountSelectionPanel.SetActive(true);
        var selector = amountSelectionPanel.GetComponent<AmountSelector>();
        selector.SetConfirmCallback(action);
        selector.SetCancelCallback(cancelAction);
        selector.SetMaxOverride(maxOverride);
    }

    private void SetX(int amount) {
        serverApi.SetX(gameData.accountData, gameData.matchState.matchId, amount);
        gEventIsInProgress = false;
    }

    private void SetAmount(int amount) {
        serverApi.SetAmount(gameData.accountData, gameData.matchState.matchId, amount);
        gEventIsInProgress = false;
    }

    private void CancelCast() {
        serverApi.CancelCast(gameData.accountData, gameData.matchState.matchId);
        gEventIsInProgress = false;
    }

    public void DisplayCardDetails(CardDisplayData cdd) {
        detailsPanel.SetActive(true);
        detailsPanel.transform.SetAsLastSibling(); // Ensure it renders on top of other panels
        detailCardDisplay.UpdateCardDisplayData(cdd);
    }

    public void DisplayCardGroup(GameObject containerObj) {
        foreach (GameObject cardObj in cardGroupView.GetChildObjects()) {
            Destroy(cardObj);
        }
        cardGroupPanel.SetActive(true);

        // Get list of activatable UIDs for checking
        HashSet<int> activatableUids = new HashSet<int>();
        foreach (CardDisplayData card in gameData.matchState.playerState.activatables) {
            activatableUids.Add(card.uid);
        }
        Debug.Log($"[DisplayCardGroup] activatableUids count: {activatableUids.Count}, prioPlayerId: {gameData.matchState.prioPlayerId}, myId: {gameData.accountData.id}");

        foreach (GameObject cardObj in containerObj.GetChildObjects()) {
            CardDisplayData cardData = cardObj.GetComponent<CardDisplay>().card;
            GameObject newCardObj = CreateAndInitializeNewCardDisplay(cardData, cardGroupView.transform, true);
            newCardObj.GetComponent<Animator>().enabled = false;

            if (cardData == null) continue;

            DynamicReferencer dRef = newCardObj.GetComponent<DynamicReferencer>();
            if (dRef == null) continue;

            // Check if this card was already selected (from a previous view)
            if (selectedUids.Contains(cardData.uid)) {
                dRef.highlightSelected.SetActive(true);
                dRef.highlightSelectable.SetActive(false);
                dRef.selectableTargetObj.SetActive(true);
            }
            // Otherwise, enable as selectable if it's a valid target
            else if (possibleSelectables.Contains(cardData.uid)) {
                // Only enable if we haven't reached max selection yet
                if (!IsSelectionComplete()) {
                    dRef.EnableSelectable();
                }
            }

            // Check if this card is activatable (e.g., Ghostly Looter in graveyard)
            // Only enable if we have priority and are not auto-passing
            bool hasPriority = gameData.matchState.prioPlayerId == gameData.accountData.id;
            if (hasPriority && !autoPass && activatableUids.Contains(cardData.uid)) {
                CardDisplay cardDisplay = newCardObj.GetComponent<CardDisplay>();
                if (cardDisplay != null) {
                    cardDisplay.EnableActivatable();
                    Debug.Log($"[DisplayCardGroup] Enabled activatable for {cardData.name} (uid={cardData.uid})");
                }
            }
        }
    }

    private bool IsSelectionComplete() {
        if (possibleSelectables.Count == 0) return false;

        // For targets, check against minimum required (enables "any number" selection when min is 0)
        if (currentSelectionType == ActionButtonType.Target) {
            bool complete = selectedUids.Count >= currentTargetMin;
            Debug.Log($"[IsSelectionComplete] Target: selectedUids.Count={selectedUids.Count}, currentTargetMin={currentTargetMin}, complete={complete}");
            return complete;
        }

        if (currentSelectionType == ActionButtonType.Tribute) {
            return currentTributeValue >= currentSelectionMax;
        }

        return selectedUids.Count >= currentSelectionMax;
    }

    public void CloseCardGroupPanel() {
        if (cardGroupPanel.activeSelf) {
            cardGroupPanel.SetActive(false);
        }
    }

    public void ConfirmCardGroupSelection() {
        CloseCardGroupPanel();
        SendSelection();
    }

    public void EnableUnselectedSelectablesInCardGroup() {
        foreach (Transform child in cardGroupView.transform) {
            DynamicReferencer dRef = child.GetComponent<DynamicReferencer>();
            if (dRef == null) continue;
            if (selectedUids.Contains(dRef.uid)) continue;
            if (!possibleSelectables.Contains(dRef.uid)) continue;
            dRef.EnableSelectable();
        }
    }

    public void DisableUnselectedSelectablesInCardGroup() {
        foreach (Transform child in cardGroupView.transform) {
            DynamicReferencer dRef = child.GetComponent<DynamicReferencer>();
            if (dRef == null) continue;
            if (selectedUids.Contains(dRef.uid)) continue;
            dRef.DisableAllInteractable();
        }
    }

    // Game Over handling
    private void HandleGameOver(GameEvent gEvent) {
        if (gameOverPanel == null) {
            Debug.LogWarning("GameOver panel not assigned!");
            return;
        }

        var matchState = gameData.matchState;
        int myPlayerId = player.uid;
        bool iWon = matchState.winnerId == myPlayerId;

        // Set result text
        if (gameOverResultText != null) {
            gameOverResultText.text = iWon ? "VICTORY" : "DEFEAT";
            gameOverResultText.color = iWon ? Color.green : Color.red;
        }

        // Set series score
        if (gameOverSeriesScoreText != null) {
            gameOverSeriesScoreText.text = $"{matchState.playerSeriesWins} - {matchState.opponentSeriesWins}";
        }

        // Determine message and button text based on series state
        bool seriesOver = matchState.isSeriesOver;
        bool iWonSeries = matchState.seriesWinnerId == myPlayerId;
        int bestOf = matchState.bestOf;

        if (gameOverMessageText != null) {
            if (bestOf == 1) {
                // Single game, no series
                gameOverMessageText.text = iWon ? "You win!" : "You lose!";
            } else if (seriesOver) {
                // Series is over
                gameOverMessageText.text = iWonSeries ? "You win the series!" : "You lose the series.";
            } else {
                // Series continues
                gameOverMessageText.text = $"Best of {bestOf}";
            }
        }

        // Show the panel
        gameOverPanel.SetActive(true);

        Debug.Log($"Game Over - Winner: {matchState.winnerId}, I Won: {iWon}, Series: {matchState.playerSeriesWins}-{matchState.opponentSeriesWins}, Series Over: {seriesOver}");
    }

    public void OnGameOverContinueClicked() {
        var matchState = gameData.matchState;
        bool seriesOver = matchState.isSeriesOver;
        int bestOf = matchState.bestOf;

        if (bestOf == 1 || seriesOver) {
            // Return to main menu
            UnityEngine.SceneManagement.SceneManager.LoadScene("Main Menu");
        } else {
            // Go to deck editor for next game in series
            // Store the current deck for editing
            gameData.isDraftDeckBuilding = false;  // Not draft mode, just between-game editing
            gameData.isSeriesDeckEditing = true;   // Flag for series deck editing
            gameData.seriesMatchId = matchState.matchId;  // Keep track of which match series
            gameData.seriesPlayerWins = matchState.playerSeriesWins;
            gameData.seriesOpponentWins = matchState.opponentSeriesWins;
            gameData.seriesBestOf = matchState.bestOf;
            UnityEngine.SceneManagement.SceneManager.LoadScene("Deck Editor");
        }
    }
}