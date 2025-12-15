using InGame;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Video;

public class CardSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler,
    IPointerUpHandler, IPointerClickHandler {

    public GameManager gameManager;

    public AudioManager audioManager;
    
    public GameObject hand;
    public GameObject cardObj;
    public GameObject cardTemplate;
    public GameObject mouseCardObj;
    public float hoverHeight;
    public float hoverSpeed;
    public float dragSpeed = 5f;
    private bool isHovered;
    private bool isGrabbed;
    private float castThresholdPercent = 0.2f; // Cast when above bottom 20% of screen (top 80%)
    
    public bool isSelectable;


    public RectTransform cardRectTransform;
    public GameObject displayCanvas;
    public Camera mainCam;
    
    
    // Target pivots for hover animation

    private GameObject tempDisplayObj;
    private RectTransform tempDisplayTransform;
    private bool tempDisplayObjExists;

    private GameObject tempDragDisplay;
    private RectTransform tempDragTransform;
    public bool castVideoIsPlaying;
    public VideoPlayer castVideoPlayer;

    private Vector3 originalPosition;
    private Vector3 targetPosition;
    private Vector2 pivot;


    public void Initialize(GameObject newCardObj) {
        gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
        audioManager = GameObject.Find("AudioManager").GetComponent<AudioManager>();
        displayCanvas = GameObject.Find("CardDisplays");
        mainCam = Camera.main;
        pivot = new Vector2(0.2f, 0.5f);
        hoverSpeed = 8f;
        hoverHeight = 175f;
        cardObj = newCardObj;
        newCardObj.transform.SetParent(gameObject.transform);
        // this prevents the card from blocking the raycast for OnPointerEnter for the CardSlot.
        newCardObj.GetComponent<CardDisplay>().backgroundImg.raycastTarget = false;
        castVideoPlayer = cardObj.GetComponentInChildren<VideoPlayer>();
    }
    
    public void Update() {
        if (!isGrabbed) {
            MoveCardHovered();
        } else {
            MoveDragCard();
        }
    }

    private void MoveDragCard() {
        CardDisplay dragCardDisplay = tempDragDisplay.GetComponent<CardDisplay>();
        tempDragTransform.position = Vector3.Lerp(tempDragTransform.position, gameManager.GetMouseWorldPositionWithZAs(0), dragSpeed * Time.deltaTime);
        if (Input.mousePosition.y > Screen.height * castThresholdPercent) {
            if (castVideoIsPlaying) return;
            dragCardDisplay.playableHighlight.SetActive(false);
            dragCardDisplay.castingAnimation.SetActive(true);
            castVideoIsPlaying = true;
        } else {
            if (!castVideoPlayer) return;
            dragCardDisplay.playableHighlight.SetActive(true);
            dragCardDisplay.castingAnimation.SetActive(false);
            castVideoIsPlaying = false;
        }
    }
    
    private void MoveCardHovered() {
        if (!tempDisplayObjExists) return;
        if (isHovered) {
            tempDisplayTransform.anchoredPosition = Vector3.Lerp(tempDisplayTransform.anchoredPosition, targetPosition,
                Time.deltaTime * hoverSpeed);
        } else { 
            tempDisplayTransform.anchoredPosition = Vector3.Lerp(tempDisplayTransform.anchoredPosition, originalPosition,
                Time.deltaTime * hoverSpeed);
            // destroy temp and activate original
            if (tempDisplayTransform.anchoredPosition.y < -539) {
                Destroy(tempDisplayObj);
                tempDisplayObjExists = false;
                cardObj.SetActive(true);
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData) {
        isHovered = true;
        audioManager.PlayRandomCardClip();
        if (gameManager.cardIsGrabbed) return;
        if (!tempDisplayObjExists) {
            CreateTempHoverCard();
            cardObj.SetActive(false);
        } else {
            tempDisplayObj.transform.SetAsLastSibling();
        }
    }

    private void CreateTempHoverCard() {
        tempDisplayObj = Instantiate(cardObj, displayCanvas.transform, true);
        tempDisplayObj.transform.SetAsLastSibling();
        tempDisplayObj.GetComponent<Animator>().enabled = false;
        tempDisplayTransform = tempDisplayObj.GetComponent<RectTransform>();
        originalPosition = new Vector3(tempDisplayTransform.anchoredPosition.x, tempDisplayTransform.anchoredPosition.y,0);
        targetPosition = new Vector3(originalPosition.x, originalPosition.y + hoverHeight, 0);
        tempDisplayObjExists = true;
    }

    private void CreateTempDragCard() {
        tempDragDisplay = Instantiate(tempDisplayObj, gameManager.GetMouseWorldPositionWithZAs(0), Quaternion.identity, displayCanvas.transform);
        tempDragDisplay.GetComponent<CardDisplay>().card = cardObj.GetComponent<CardDisplay>().card;
        tempDragDisplay.GetComponent<CardDisplay>().ActivateTribeCastingVideo();
        tempDragDisplay.transform.SetAsLastSibling();
        tempDragDisplay.GetComponent<Animator>().enabled = false;
        tempDragTransform = tempDragDisplay.GetComponent<RectTransform>();
    }


    public void OnPointerExit(PointerEventData eventData) {
        isHovered = false;
        if (isGrabbed) {
            Destroy(tempDisplayObj);
            tempDisplayObjExists = false;
        }
    }
    
    public void OnPointerDown(PointerEventData eventData) {
        // Only left-click starts drag (right-click is for inspect)
        if (eventData.button != PointerEventData.InputButton.Left) return;
        // TODO !SECURITY ISSUE! verify on the server that a card is playable before attempting to cast
        if (!cardObj.GetComponent<CardDisplay>().isPlayable) return;
        CreateTempDragCard();
        gameManager.cardIsGrabbed = true;
        isGrabbed = true;
        Destroy(tempDisplayObj);
        tempDisplayObjExists = false;
    }
    public void OnPointerUp(PointerEventData eventData) {
        // Only handle left-click release
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (!cardObj.GetComponent<CardDisplay>().isPlayable) return;
        if (tempDragDisplay == null) return; // Guard against rapid clicks
        if (Input.mousePosition.y > Screen.height * castThresholdPercent) {
            gameManager.AttemptToCast(cardObj.GetComponent<CardDisplay>().card.uid);
        }
        gameManager.cardIsGrabbed = false;
        isGrabbed = false;
        tempDragDisplay.GetComponent<CardDisplay>().videoPlayer.Stop();
        Destroy(tempDragDisplay);
        if (isHovered) {
            cardObj.SetActive(true);
            CreateTempHoverCard();
            cardObj.SetActive(false);
        } else {
            cardObj.SetActive(true);
        }
    }

    public void ActivateTempHighlights() {
        if (tempDisplayObj != null) {
            tempDisplayObj.GetComponent<CardDisplay>().playableHighlight.SetActive(true);
        }
    }

    /// <summary>
    /// Cleans up any temporary display objects (hover/drag) and restores the original card.
    /// Call this before removing the card from hand to prevent orphaned UI elements.
    /// </summary>
    public void CleanupTempDisplays() {
        if (tempDragDisplay != null) {
            Destroy(tempDragDisplay);
            tempDragDisplay = null;
        }
        if (tempDisplayObj != null) {
            Destroy(tempDisplayObj);
            tempDisplayObj = null;
            tempDisplayObjExists = false;
        }
        isHovered = false;
        isGrabbed = false;
        gameManager.cardIsGrabbed = false;
        cardObj.SetActive(true);
    }


    public void EnableSelectable() {
        if (tempDisplayObj != null) {
            tempDisplayObj.GetComponent<DynamicReferencer>().EnableSelectable();
        } else {
            DynamicReferencer dRef = cardObj.GetComponent<DynamicReferencer>();
            dRef.highlightSelectable.SetActive(true);
            dRef.selectableTargetObj.SetActive(true);
        }
        isSelectable = true;
    }
    public void OnPointerClick(PointerEventData eventData) {
        // Right-click to inspect
        if (eventData.button == PointerEventData.InputButton.Right) {
            CardDisplay cardDisplay = cardObj.GetComponent<CardDisplay>();
            if (cardDisplay != null && cardDisplay.card != null) {
                gameManager.DisplayCardDetails(cardDisplay.card);
            }
            return;
        }

        // Left-click for selectable
        if (isSelectable) {
            ExecuteEvents.Execute(tempDisplayObj.GetComponent<DynamicReferencer>().selectableTargetObj,
                eventData, ExecuteEvents.pointerClickHandler);
            DynamicReferencer cardDRef = cardObj.GetComponent<DynamicReferencer>();
            cardDRef.highlightSelectable.SetActive(!cardDRef.highlightSelectable.activeSelf);
            cardDRef.highlightSelected.SetActive(!cardDRef.highlightSelected.activeSelf);
        }
    }
}
 