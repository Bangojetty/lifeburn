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

    // targeting
    public GameObject targetingArrowPfb;
    public GameObject targetingArrow;
    public int currentTargetMax;
    
    // selecting
    public List<int> possibleSelectables = new();
    public List<int> selectedUids = new();
    public int currentSelectionMax;
    public ActionButtonType currentSelectionType;
    public Dictionary<DynamicReferencer, List<int>> dRefToSelectedUids = new();
    public GameObject amountSelectionPanel;

    // activated abilites
    public CardDisplay currentActivatedAbilityCdd;
    public GameObject activationVerificationPanel;

    
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
    }
    private void Start() {
        ebug = GameObject.Find("eBugger").GetComponent<Ebug>();
        InitializeStaticCardDisplays();
        InitializeGame();
        InitializePlayers();
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
        gameData.matchState = serverApi.GetMatchState(gameData.accountData, gameData.matchState.matchId);
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
                    gEventIsInProgress = false;
                    break;
                case EventType.Summon:
                    Debug.Log("Summon Event");
                    StartCoroutine(SummonEvent(gEvent));
                    break;
                case EventType.Resolve:
                    Debug.Log("Resolve Event");
                    int count = stackView.transform.childCount;
                    // destroy the last object in the stack(First in last out)
                    Destroy(currentResolvingStackObj);
                    if (gEvent.sourceCard != null) {
                        if (gEvent.isOpponent) {
                            opponent.CreateAndAddCardToZone(gEvent.sourceCard, Zone.Graveyard);
                        } else {
                            player.CreateAndAddCardToZone(gEvent.sourceCard, Zone.Graveyard);
                        }
                    }
                    if (count == 1) {
                        Debug.Log("stack is empty, disabling stack panel");
                        stackPanel.SetActive(false);
                    }
                    // finalize the event by toggling gEventsInProgress
                    gEventIsInProgress = false;
                    break;
                case EventType.LookAtDeck:
                    Debug.Log("LookAtDeck Event");
                    DisplayLookAtDeckDialogue(gEvent);
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
                    Debug.Log("Attacker is " + UidToObj[gEvent.attackerUid].GetComponent<CardDisplay>().card.name +
                              ". defender uid is " + gEvent.defenderUid + ". damage amount is " + gEvent.amount);
                    StartCoroutine(CombatEvent(gEvent));
                    
                    break;
                case EventType.Death:
                    Debug.Log("Death Event");
                    Debug.Log("Dying unit is " + UidToObj[gEvent.focusUid].GetComponent<CardDisplay>().card.name);
                    UidToObj[gEvent.focusUid].GetComponent<CardDisplay>().Kill();
                    gEventIsInProgress = false;
                    break;
                case EventType.TributeRequirement:
                    Debug.Log("Tribute Requirement Event");
                    waitingForOpponentTextObj.SetActive(false);
                    Debug.Assert(gEvent.focusCard != null, "There is no card associated with this event to pull tribute cost from");
                    currentSelectionMax = gEvent.focusCard.cost;
                    plurality = currentSelectionMax == 1 ? "" : "s";
                    selectTextObj.GetComponent<TMP_Text>().text = "Select Tribute" + plurality + ".";
                    selectTextObj.SetActive(true);
                    possibleSelectables = gEvent.focusUidList;
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
                    waitingForOpponentTextObj.SetActive(false);
                    currentTargetMax = gEvent.targetSelection.amount;
                    plurality = gEvent.targetSelection.amount == 1 ? "" : "s";
                    selectTextObj.GetComponent<TMP_Text>().text = "Select Target" + plurality + ".";
                    selectTextObj.SetActive(true);
                    possibleSelectables = gEvent.targetSelection.selectableUids;
                    currentSelectionType = ActionButtonType.Target;
                    EnableUnselectedSelectables();
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
                        UidToObj[cdd.uid].GetComponent<CardDisplay>().DisplayCardData(cdd);
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
                    Debug.Log("PayLifeCost Event");
                    // this will be updated later to show an animation
                    if (gEvent.isOpponent) {
                        opponent.LoseLife(gEvent.amount);
                    } else {
                        player.LoseLife(gEvent.amount);
                    }
                    gEventIsInProgress = false;
                    break;
                case EventType.Reveal:
                    Debug.Log("Reveal Event");
                    foreach (CardDisplay cDisplay in opponent.GetCardsInHand()) {
                        if (cDisplay.card != null) continue;
                        cDisplay.UpdateCardDisplayData(gEvent.focusCard);
                        break;
                    }
                    gEventIsInProgress = false;
                    break;
                case EventType.AttackCapable:
                    Debug.Log("AttackCapable Event");
                    Debug.Assert(gEvent.focusUidList != null, "AttackCapableUids list is null");
                    attackCapableUids = gEvent.focusUidList.ToList();
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
                    }
                    currentSelectionType = ActionButtonType.Cost;
                    break;
                case EventType.EndGame:
                    Debug.Log("EndGame Event");
                    // TODO end game animation and resolution
                    gEventIsInProgress = false;
                    break;
                case EventType.Discard:
                    Debug.Log("Discard Event");
                    StartCoroutine(DiscardEvent(gEvent));
                    break;
                case EventType.AmountSelection:
                    Debug.Log("AmountSelection Event");
                    currentSelectionMax = player.lifePoints;
                    if (gEvent.universalBool) {
                        DisplayAmountSelector(SetX);
                    } else {
                        DisplayAmountSelector(SetAmount);
                    }
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
    }

    private void EnableCostCardSelection(GameEvent gEvent) {
        waitingForOpponentTextObj.SetActive(false);
        Debug.Assert(gEvent.focusUidList != null, "selectableCosts list is null");
        currentSelectionMax = gEvent.amount;
        selectTextObj.GetComponent<TMP_Text>().text = gEvent.eventMessages.First();
        selectTextObj.SetActive(true);
        possibleSelectables = gEvent.focusUidList;
        EnableUnselectedSelectables();
    }

    private void RefreshStateDisplays() {
        player.deckAmountText.text = gameData.matchState.playerState.deckAmount.ToString();
        opponent.deckAmountText.text = gameData.matchState.opponentState.deckAmount.ToString();
        // TODO update deckObj for each player if deck is empty
    }

    private void IteratePhase() {
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
        // only auto pass if there are no possible attackers
        if (autoPass && attackCapableUids.Count == 0) {
            if(ShouldAutoPass()) {
                gEventIsInProgress = false;
                PassPrio();
                return;
            }
            passToMyMain = false;
            autoPass = false;
            PhaseButtonManager.DisableStopBorder();
        }
        // displays any playable cards/moves
        foreach (CardDisplayData card in gameData.matchState.playerState.playables) {
            GameObject cardObj = UidToObj[card.uid];
            cardObj.GetComponent<CardDisplay>().EnablePlayable();
            CardSlot cardSlot = cardObj.GetComponentInParent<CardSlot>();
            if (cardSlot != null) {
                cardSlot.ActivateTempHighlights();
            }
        }

        foreach (CardDisplayData card in gameData.matchState.playerState.activatables) {
            GameObject cardObj = UidToObj[card.uid];
            cardObj.GetComponent<CardDisplay>().EnableActivatable();
        }
        
        waitingForOpponentTextObj.SetActive(false);
        actionButtonComponent.interactable = true;
        gEventIsInProgress = false;
    }

    private bool ShouldAutoPass() {
        if (passToMyMain && gameData.accountData.id != gameData.matchState.turnPlayerId) return true;
        if (phaseToPassTo != gameData.matchState.currentPhase) return true;
        return false;
    }
    

    public void PassPrio() {
        LosePrio();
        serverApi.PassPrio(gameData.accountData, gameData.matchState.matchId);
        WaitForEvents();
    }

    private void LosePrio() {
        actionButtonComponent.interactable = false;
        actionButton.SetButtonType(ActionButtonType.Pass);
        DisableAllInteractables();
    }

    private void DisableAllInteractables() {
        foreach (GameObject cardOrPlayerObj in UidToObj.Values) {
            cardOrPlayerObj.GetComponent<DynamicReferencer>().DisableAllInteractable();
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
    
    public void CreateNewStackObj(StackDisplayData stackDisplayData, Transform parentTransform) {
        if (!stackPanel.activeSelf) {
            stackPanel.SetActive(true);
        }
        GameObject pfbBasedOnType = stackDisplayData.stackObjType == StackObjType.Spell ? spellStackDisplayPfb : abilityStackDisplayPfb;
        StackObjDisplay newStackObjDisplay = Instantiate(pfbBasedOnType, parentTransform).GetComponent<StackObjDisplay>();
        newStackObjDisplay.Initialize(stackDisplayData);
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
        Animator phaseBtnBorderAnim = phaseBtnBorderObj.GetComponent<Animator>();
        phaseBtnBorderAnim.Play(gameData.phaseToAnimDict[localPhase], -1, 0f);
        IteratePhase();
        yield return new WaitForSeconds(phaseBtnBorderAnim.GetCurrentAnimatorStateInfo(0).length);
        gEventIsInProgress = false;
    }
    
    private IEnumerator CombatEvent(GameEvent gEvent) {
        // TODO update the attack animation keyframes to match the position of the card and its target (dynamic anim)
        GameObject attackerObj = UidToObj[gEvent.attackerUid];
        // remove arrow
        Destroy(attackerToAttackArrow[attackerObj]);
        attackerToAttackArrow.Remove(attackerObj);
        // unassign AttackCapable
        attackerObj.GetComponent<DynamicReferencer>().attackCapable.isSelected = false;
        // disable highlights and interactables (CardDisplay.UpdateDamageNumber)
        attackerObj.GetComponent<DynamicReferencer>().DisableAllInteractable();
        // set attacker for use in combat animation event (
        attackerObj.GetComponent<CardDisplay>().attackTarget = UidToObj[gEvent.defenderUid];
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
        // animate it
        Animator newCardAnimator = newCardDisplay.GetComponent<Animator>();
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
                yield return new WaitUntil(() => newCardAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1
                                                 && !newCardAnimator.IsInTransition(0));
            } else {
                yield return new WaitForSeconds(0.5f);
            }
        } else {
            newCardAnimator.Play("Draw",-1, 0f);
            // wait longer if the next event is not a draw event
            if (isLastDrawEvent) {
                yield return new WaitUntil(() => newCardAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1
                                                 && !newCardAnimator.IsInTransition(0));
            } else {
                yield return new WaitForSeconds(0.5f);
            }
        }
        // finalize the event by toggling gEventsInProgress
        gEventIsInProgress = false;
    }

    private IEnumerator DiscardEvent(GameEvent gEvent) {
        CardDisplay handCardDisplay;
        Debug.Assert(gEvent.focusCard != null, "There is no focusCard for this discard event");
        if (gEvent.isOpponent) {
            if (UidToObj.ContainsKey(gEvent.focusUid)) {
                handCardDisplay = UidToObj[gEvent.focusCard.uid].GetComponent<CardDisplay>();
                handCardDisplay.RemoveFromHand();
            } else {
                foreach (CardDisplay cDisplay in opponent.handZoneObj.GetComponentsInChildren<CardDisplay>()) {
                    if (cDisplay.card != null) continue;
                    Destroy(cDisplay.gameObject);
                    break;
                }
                handCardDisplay = CreateAndInitializeNewCardDisplay(gEvent.focusCard, 
                    displayCanvas.transform).GetComponent<CardDisplay>();
            } 
        } else {
            handCardDisplay = UidToObj[gEvent.focusCard.uid].GetComponent<CardDisplay>();
            handCardDisplay.RemoveFromHand();
        }
        Animator handCardAnimator = handCardDisplay.GetComponent<Animator>();
        handCardAnimator.enabled = true;
        string animName = gEvent.isOpponent ? "DiscardOpp" : "Discard";
        handCardAnimator.Play(animName, -1, 0f);
        yield return new WaitForSeconds(handCardAnimator.GetCurrentAnimatorStateInfo(0).length);
        // finalize event 
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
        CardDisplay handCardDisplay;
        Debug.Assert(gEvent.focusStackObj != null, "there was no focusStackObj for this cast event");
        if (gEvent.isOpponent) {
            if (UidToObj.ContainsKey(gEvent.focusStackObj.cardDisplayData.uid)) {
                handCardDisplay = UidToObj[gEvent.focusStackObj.cardDisplayData.uid].GetComponent<CardDisplay>();
                handCardDisplay.RemoveFromHand();
            } else {
                foreach (CardDisplay cDisplay in opponent.handZoneObj.GetComponentsInChildren<CardDisplay>()) {
                    if (cDisplay.card != null) continue;
                    Destroy(cDisplay.gameObject);
                    break;
                }
                handCardDisplay = CreateAndInitializeNewCardDisplay(gEvent.focusStackObj.cardDisplayData, 
                    displayCanvas.transform).GetComponent<CardDisplay>();
            }
        } else {
            handCardDisplay = UidToObj[gEvent.focusStackObj.cardDisplayData.uid].GetComponent<CardDisplay>();
            handCardDisplay.RemoveFromHand();
        }
        handCardDisplay.tempStackDisplayData = gEvent.focusStackObj;
        Animator castingAnimator = handCardDisplay.GetComponent<Animator>();
        castingAnimator.enabled = true;
        string animName = gEvent.isOpponent ? "FromHandOpp" : "FromHand";
        castingAnimator.Play(animName, -1, 0f);
        yield return new WaitForSeconds(castingAnimator.GetCurrentAnimatorStateInfo(0).length);
        castingAnimator.Play("ToStack", -1, 0f);
        yield return new WaitForSeconds(castingAnimator.GetCurrentAnimatorStateInfo(0).length);
        // finalize the event by toggling gEventsInProgress
        gEventIsInProgress = false;
    }

    private IEnumerator SummonEvent(GameEvent gEvent) {
        CardDisplay newSummonCardDisplay;
        CardDisplayData focusCard = gEvent.focusCard;
        // add to map for retrieval when resetting stats
        if (gEvent.isOpponent) {
            newSummonCardDisplay = opponent.Summon(focusCard);
        } else {
            newSummonCardDisplay = player.Summon(focusCard);
            // isAttacking
            if(gEvent.universalBool) SelectAttackCapable(newSummonCardDisplay.dynamicReferencer.attackCapable, false);
        }
        yield return new WaitForSeconds((float)newSummonCardDisplay.summonVideoPlayer.clip.length);
        // finalize the event by toggling gEventsInProgress
        gEventIsInProgress = false;
    }

    private IEnumerator SendToZoneEvent(GameEvent gEvent) {
        if (gEvent.sourceZone != null && gEvent.sourceZone != Zone.Deck) {
            Debug.Assert(gEvent.focusCard != null, "there is no card for sendToZone Event");
            GameObject cardObj = UidToObj[gEvent.focusCard.uid];
            UidToObj.Remove(gEvent.focusCard.uid);
            switch (gEvent.sourceZone) {
                case Zone.Play:
                    Transform cardSlot = cardObj.transform.parent;
                    Destroy(cardSlot.parent.name == "PlayZone" ? cardSlot.gameObject : cardObj);
                    break;
                default:
                    Destroy(cardObj);
                    break;
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
            case Zone.Play:
                sendToZoneDelay = eventDelayShort;
                animationName = gEvent.isOpponent ? "ToPlayOpp" : "ToPlay";
                break;
            default:
                Debug.Log("Zone doesn't exist for SendToZone Event");
                animationName = "";
                break;
        }
        GameObject newCardObj = CreateAndInitializeNewCardDisplay(gEvent.focusCard, displayCanvas.transform, isTemp);
        Animator newCardAnimator = newCardObj.GetComponent<Animator>();
        newCardAnimator.enabled = true;
        newCardAnimator.Play(animationName, -1, 0f);
        yield return new WaitForSeconds(sendToZoneDelay);
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

    public GameObject CreateAndInitializeNewCardDisplay(CardDisplayData cdd, Transform parentTransform, bool isTemp = false) {
        GameObject newCardDisplayObj = Instantiate(cardDisplayPfb, parentTransform);
        if (cdd != null) {
            newCardDisplayObj.name = cdd.name + "(" + cdd.uid + ")";
        }
        CardDisplay newCardDisplay = newCardDisplayObj.GetComponent<CardDisplay>();
        newCardDisplay.UpdateCardDisplayData(cdd);
        if (isTemp && cdd != null) UidToObj.Remove(cdd.uid);
        return newCardDisplayObj;
    }
    
    // TODO create PlaySlot initializer to normalize code from CardDisplay.AddToPlay() and Participant.Summon()
    // public GameObject CreateAndInitializeNewPlaySlot(CardDisplayData cdd)
    
    private void AddToStack(StackDisplayData stackDisplayData) {
        CreateNewStackObj(stackDisplayData, stackView.transform);
    }

    // -------
    private void InitializeGame() {
        gameData = GameObject.Find("GameData").GetComponent<GameData>();
        gameData.accountData = GameObject.Find("AccountData").GetComponent<AccountDataGO>().accountData;
        localPhase = Phase.Draw;
    }
    
    private void InitializePlayers() {
        player.Initialize(gameData.matchState.playerState);
        opponent.Initialize(gameData.matchState.opponentState);
    }
    
    public void QuitGame() {
        Application.Quit();
    }
    
    // testing
    public void TogglePauseMenu() {
        pauseMenu.SetActive(!pauseMenu.activeSelf);
    }
    
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
        currentActivatedAbilityCdd = cardDisplay;
        activationVerificationPanel.SetActive(true);
    }

    public void CancelAbilityActivation() {
        currentActivatedAbilityCdd = null;
        activationVerificationPanel.SetActive(false);
    }

    public void AttemptToActivate() {
        LosePrio();
        serverApi.AttemptToActivate(gameData.accountData, gameData.matchState.matchId, currentActivatedAbilityCdd.card.uid);
        activationVerificationPanel.SetActive(false);
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
            DynamicReferencer dRef = UidToObj[uid].GetComponent<DynamicReferencer>();
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
            UidToObj[uid].GetComponent<CardDisplay>().EnableAttackCapable();
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
            GameObject newAttArrow = CreateAttackArrow((pair.Key, pair.Value));
            attackerToAttackArrow.Add(UidToObj[pair.Key], newAttArrow);
        }
    }

    private Vector3 GetCardTargetLocation(int uid) {
        CardDisplay cDisplay = UidToObj[uid].GetComponent<CardDisplay>();
        Participant participant = UidToObj[uid].GetComponent<Participant>();
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
        actionButtonComponent.interactable = true;
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
        DisplayAttack((attackingCardDisplay.card.uid, attackable.dynamicReferencer.uid), false);
        // normal combat attack
        var assigningUids = (attackingAttackCapable.cardDisplay.card.uid, attackable.dynamicReferencer.uid);
        if (!isSecondaryAttack) {
            serverApi.AssignAttack(gameData.accountData, gameData.matchState.matchId, assigningUids);
            actionButton.SetButtonType(ActionButtonType.Attack);
            actionButtonComponent.interactable = true;
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
        UnDisplayAttack(attackCapable.cardDisplay.card.uid);
        // deselect attackable
        attackCapable.Deselect();
    }

    private void DisplayAttack((int, int) attackUidPair, bool isOpponent) {
        GameObject attackingObj = UidToObj[attackUidPair.Item1];
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
        GameObject attackerObj = UidToObj[attackerUid];
        float localYAdjust = attackYAdjust;
        // reverse the YAdjust for opponent attacks
        if(isOpponent) localYAdjust = -localYAdjust;
        // destroy the attack arrow
        Destroy(attackerToAttackArrow[attackerObj]);
        // move the card y position back down
        RectTransform baseObjRectTransform = attackerObj.GetComponent<RectTransform>();
        Vector2 baseObjPos = baseObjRectTransform.anchoredPosition;
        baseObjRectTransform.anchoredPosition = new Vector2(baseObjPos.x, baseObjPos.y - localYAdjust);
        attackerToAttackArrow.Remove(attackerObj);
        attackUids.Remove(attackerUid);
    }

    private void ActivateTargetingArrow(Vector3 cardPosition) {
        LineRenderer taLineRenderer = targetingArrow.GetComponent<LineRenderer>();
        taLineRenderer.SetPosition(0, cardPosition);
        taLineRenderer.SetPosition(1, GetMouseWorldPositionWithZAs(0));
        targetingArrow.SetActive(true);
    }


    public void SetCurrentPassToPhase(int phaseIndex) {
        // pass to my main
        if (phaseIndex == 6) {
            passToMyMain = true;
            phaseToPassTo = Phase.Main;
        } else {
            passToMyMain = false;
            phaseToPassTo = (Phase)phaseIndex;
        }
        autoPass = true;
        if (actionButtonComponent.interactable) {
            PassPrio();
        }
        PhaseButtonManager.SetStopBorder(phaseIndex);
    }

    private List<DynamicReferencer> GetAllDynamicReferencers() {
        List<DynamicReferencer> referencers = new List<DynamicReferencer>();
        foreach (var pair in UidToObj) {
            referencers.Add(pair.Value.GetComponent<DynamicReferencer>());
        }
        return referencers;
    }
    
    public void EnableUnselectedSelectables() {
        foreach (DynamicReferencer dRef in GetAllDynamicReferencers()) {
            if (isSelected(dRef)) continue;
            if (!isSelectable(dRef)) continue;
            if (dRef.tokenDisplay == null && CardIsInHand(dRef.cardDisplay, out var cSlot)) {
                cSlot.EnableSelectable();
            } else dRef.EnableSelectable();
        }
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
        // reset possible selectables
        possibleSelectables.Clear();
        // send selected Uids based on the selection type
        switch (currentSelectionType) {
            case ActionButtonType.Cost:
                serverApi.SendCostSelection(gameData.accountData, gameData.matchState.matchId, selectedUids);
                break;
            case ActionButtonType.Target:
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
        // disable interactables
        LosePrio();
        gEventIsInProgress = false;
    }

    public void DisableUnselectedSelectables() {
        foreach (DynamicReferencer dRef in GetAllDynamicReferencers()) {
            // disable all unselected selectables
            if (isSelected(dRef)) continue;
            dRef.DisableAllInteractable();
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

    public void DisplayAmountSelector(Action<int> action) {
        amountSelectionPanel.SetActive(true);
        amountSelectionPanel.GetComponent<AmountSelector>().SetConfirmCallback(action);
    }

    private void SetX(int amount) {
        serverApi.SetX(gameData.accountData, gameData.matchState.matchId, amount);
        gEventIsInProgress = false;
    }
    
    private void SetAmount(int amount) {
        serverApi.SetAmount(gameData.accountData, gameData.matchState.matchId, amount);
        gEventIsInProgress = false;
    }

    public void DisplayCardDetails(CardDisplayData cdd) {
        detailsPanel.SetActive(true);
        detailCardDisplay.UpdateCardDisplayData(cdd);
    }

    public void DisplayCardGroup(GameObject containerObj) {
        foreach (GameObject cardObj in cardGroupView.GetChildObjects()) {
            Destroy(cardObj);
        }
        cardGroupPanel.SetActive(true);
        foreach (GameObject cardObj in containerObj.GetChildObjects()) {
            GameObject newSimpleCardObj = Instantiate(simpleCardDisplayPfb, cardGroupView.transform);
            CardDisplaySimple newSimpleCardDisplay = newSimpleCardObj.GetComponent<CardDisplaySimple>();
            newSimpleCardDisplay.UpdateCardDisplayData(cardObj.GetComponent<CardDisplay>().card);
        }
    }
}