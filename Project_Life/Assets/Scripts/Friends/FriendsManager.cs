using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FriendsManager : MonoBehaviour {
    [Header("References")]
    public AccountDataGO accountDataGO;

    [Header("Friends List (Always Visible)")]
    public Transform friendsListContent;
    public GameObject friendListItemPfb;
    public GameObject noFriendsText;
    public TMP_Text usernameText;

    [Header("Modal Buttons")]
    public Button addRequestsButton;
    public Button settingsButton;
    public GameObject requestsBadge;
    public TMP_Text requestsBadgeCount;

    [Header("Add/Requests Modal")]
    public GameObject addRequestsModal;
    public Button addRequestsCloseButton;
    public TMP_InputField addFriendInput;
    public Button addFriendButton;
    public TMP_Text addFriendStatusText;
    public Transform inboundRequestsContent;
    public Transform outboundRequestsContent;
    public GameObject friendRequestItemPfb;
    public Button blockedButton; // Opens blocked submodal

    [Header("Blocked Submodal")]
    public GameObject blockedModal;
    public Button blockedCloseButton;
    public Transform blockedListContent;
    public GameObject blockedPlayerItemPfb;

    [Header("Settings Modal")]
    public GameObject settingsModal;
    public Button settingsCloseButton;

    [Header("Context Menu")]
    public GameObject contextMenu;
    public Button contextInviteButton;
    public Button contextRemoveButton;
    public Button contextBlockButton;
    private int contextTargetId;

    [Header("Lobby Integration")]
    public LobbyManager lobbyManager;
    public GameObject lobbyInviteItemPfb;

    [Header("Polling Settings")]
    public float refreshInterval = 5f;
    public float heartbeatInterval = 5f;
    public float invitePollInterval = 5f;

    private ServerApi serverApi = new();
    private List<FriendData> currentFriends = new();
    private List<FriendRequestData> inboundRequests = new();
    private List<FriendRequestData> outboundRequests = new();
    private List<LobbyInviteData> lobbyInvites = new();

    private Coroutine refreshCoroutine;
    private Coroutine heartbeatCoroutine;
    private Coroutine invitePollingCoroutine;

    void Start() {
        if (accountDataGO == null) {
            var accountDataObj = GameObject.Find("AccountData");
            if (accountDataObj != null) {
                accountDataGO = accountDataObj.GetComponent<AccountDataGO>();
            }
        }

        // Update username in sidebar header
        if (usernameText != null && accountDataGO != null && accountDataGO.accountData != null) {
            usernameText.text = accountDataGO.accountData.displayName;
        }

        // Setup button listeners
        if (addRequestsButton != null) addRequestsButton.onClick.AddListener(ToggleAddRequestsModal);
        if (settingsButton != null) settingsButton.onClick.AddListener(ToggleSettingsModal);
        if (addFriendButton != null) addFriendButton.onClick.AddListener(OnAddFriendClicked);
        if (blockedButton != null) blockedButton.onClick.AddListener(ToggleBlockedModal);

        // Close button listeners
        if (addRequestsCloseButton != null) addRequestsCloseButton.onClick.AddListener(CloseAddRequestsModal);
        if (blockedCloseButton != null) blockedCloseButton.onClick.AddListener(CloseBlockedModal);
        if (settingsCloseButton != null) settingsCloseButton.onClick.AddListener(CloseSettingsModal);

        // Context menu buttons
        if (contextInviteButton != null) contextInviteButton.onClick.AddListener(OnContextInvite);
        if (contextRemoveButton != null) contextRemoveButton.onClick.AddListener(OnContextRemove);
        if (contextBlockButton != null) contextBlockButton.onClick.AddListener(OnContextBlock);

        // Initial load
        RefreshFriendsList();
        RefreshRequestsBadge();

        // Start periodic refresh, heartbeat, and invite polling
        refreshCoroutine = StartCoroutine(PeriodicRefresh());
        heartbeatCoroutine = StartCoroutine(PeriodicHeartbeat());
        invitePollingCoroutine = StartCoroutine(PollForLobbyInvites());
    }

    void Update() {
        // Close context menu when clicking outside of it
        if (contextMenu != null && contextMenu.activeSelf) {
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) {
                RectTransform menuRect = contextMenu.GetComponent<RectTransform>();
                Canvas canvas = contextMenu.GetComponentInParent<Canvas>();
                Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                if (!RectTransformUtility.RectangleContainsScreenPoint(menuRect, Input.mousePosition, cam)) {
                    contextMenu.SetActive(false);
                }
            }
        }

        // Escape key closes modals (blocked submodal first, then requests modal)
        if (Input.GetKeyDown(KeyCode.Escape)) {
            if (blockedModal != null && blockedModal.activeSelf) {
                CloseBlockedModal();
            } else if (addRequestsModal != null && addRequestsModal.activeSelf) {
                CloseAddRequestsModal();
            } else if (settingsModal != null && settingsModal.activeSelf) {
                CloseSettingsModal();
            }
        }
    }

    private IEnumerator CloseContextMenuDelayed() {
        yield return new WaitForEndOfFrame();
        if (contextMenu != null) {
            contextMenu.SetActive(false);
        }
    }

    void OnDestroy() {
        if (refreshCoroutine != null) StopCoroutine(refreshCoroutine);
        if (heartbeatCoroutine != null) StopCoroutine(heartbeatCoroutine);
        if (invitePollingCoroutine != null) StopCoroutine(invitePollingCoroutine);
    }

    #region Modals

    public void ToggleAddRequestsModal() {
        bool opening = addRequestsModal != null && !addRequestsModal.activeSelf;
        CloseAllModals();
        if (addRequestsModal != null) {
            addRequestsModal.SetActive(opening);
            if (opening) {
                RefreshRequestsList();
            }
        }
    }

    public void ToggleBlockedModal() {
        bool opening = blockedModal != null && !blockedModal.activeSelf;
        if (blockedModal != null) {
            blockedModal.SetActive(opening);
            if (opening) {
                RefreshBlockedList();
            }
        }
    }

    public void CloseBlockedModal() {
        if (blockedModal != null) blockedModal.SetActive(false);
    }

    public void CloseAddRequestsModal() {
        if (blockedModal != null) blockedModal.SetActive(false);
        if (addRequestsModal != null) addRequestsModal.SetActive(false);
    }

    public void CloseSettingsModal() {
        if (settingsModal != null) settingsModal.SetActive(false);
    }

    public void ToggleSettingsModal() {
        bool opening = settingsModal != null && !settingsModal.activeSelf;
        CloseAllModals();
        if (settingsModal != null) {
            settingsModal.SetActive(opening);
        }
    }

    public void CloseAllModals() {
        if (addRequestsModal != null) addRequestsModal.SetActive(false);
        if (blockedModal != null) blockedModal.SetActive(false);
        if (settingsModal != null) settingsModal.SetActive(false);
        if (contextMenu != null) contextMenu.SetActive(false);
    }

    #endregion

    #region Context Menu

    public void ShowContextMenu(int friendId, Vector3 screenPosition) {
        if (contextMenu == null) return;

        contextTargetId = friendId;

        // Only show invite button if we're in a lobby
        if (contextInviteButton != null) {
            contextInviteButton.gameObject.SetActive(lobbyManager != null && lobbyManager.IsInLobby);
        }

        // Convert screen position to local position in parent
        RectTransform parentRect = contextMenu.transform.parent.GetComponent<RectTransform>();
        RectTransform menuRect = contextMenu.GetComponent<RectTransform>();
        Canvas canvas = contextMenu.GetComponentInParent<Canvas>();
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPosition, cam, out localPoint);
        menuRect.pivot = new Vector2(0, 1); // Top-left corner at mouse position
        menuRect.anchoredPosition = localPoint;

        contextMenu.SetActive(true);
    }

    private void OnContextInvite() {
        if (lobbyManager != null && lobbyManager.IsInLobby) {
            lobbyManager.InviteFriendToLobby(contextTargetId);
        }
        if (contextMenu != null) contextMenu.SetActive(false);
    }

    private void OnContextRemove() {
        RemoveFriend(contextTargetId);
        if (contextMenu != null) contextMenu.SetActive(false);
    }

    private void OnContextBlock() {
        BlockPlayer(contextTargetId);
        if (contextMenu != null) contextMenu.SetActive(false);
    }

    #endregion

    #region Periodic Updates

    private IEnumerator PeriodicRefresh() {
        while (true) {
            yield return new WaitForSeconds(refreshInterval);
            RefreshFriendsList();
            RefreshRequestsBadge();
        }
    }

    private IEnumerator PeriodicHeartbeat() {
        while (true) {
            if (accountDataGO != null && accountDataGO.accountData != null) {
                serverApi.SendHeartbeat(accountDataGO.accountData);
            }
            yield return new WaitForSeconds(heartbeatInterval);
        }
    }

    private IEnumerator PollForLobbyInvites() {
        while (true) {
            yield return new WaitForSeconds(invitePollInterval);

            if (accountDataGO == null || accountDataGO.accountData == null) continue;

            var newInvites = serverApi.GetLobbyInvites(accountDataGO.accountData) ?? new List<LobbyInviteData>();

            // Only refresh if invites changed
            if (newInvites.Count != lobbyInvites.Count) {
                lobbyInvites = newInvites;
                RefreshFriendsList();
            }
        }
    }

    #endregion

    #region Friends List

    public void RefreshFriendsList() {
        if (accountDataGO == null || accountDataGO.accountData == null) return;

        ClearContainer(friendsListContent);
        currentFriends = serverApi.GetFriends(accountDataGO.accountData) ?? new List<FriendData>();

        // Sort: online first, then alphabetically
        currentFriends.Sort((a, b) => {
            if (a.isOnline != b.isOnline) return b.isOnline.CompareTo(a.isOnline);
            return a.displayName.CompareTo(b.displayName);
        });

        // Show/hide "no friends" text (also hide if there are invites)
        if (noFriendsText != null) {
            noFriendsText.SetActive(currentFriends.Count == 0 && lobbyInvites.Count == 0);
        }

        // Display lobby invites first
        foreach (var invite in lobbyInvites) {
            if (lobbyInviteItemPfb == null || friendsListContent == null) continue;
            GameObject item = Instantiate(lobbyInviteItemPfb, friendsListContent);
            var inviteItem = item.GetComponent<LobbyInviteItem>();
            if (inviteItem != null) {
                inviteItem.Setup(invite, this);
            }
        }

        // Then display friends
        foreach (var friend in currentFriends) {
            if (friendListItemPfb == null || friendsListContent == null) continue;
            GameObject item = Instantiate(friendListItemPfb, friendsListContent);
            var listItem = item.GetComponent<FriendListItem>();
            if (listItem != null) {
                listItem.Setup(friend, this);
            }
        }
    }

    #endregion

    #region Requests Badge

    private void RefreshRequestsBadge() {
        if (accountDataGO == null || accountDataGO.accountData == null) return;

        inboundRequests = serverApi.GetFriendRequests(accountDataGO.accountData) ?? new List<FriendRequestData>();

        // Badge only shows inbound request count
        if (requestsBadge != null) {
            requestsBadge.SetActive(inboundRequests.Count > 0);
        }
        if (requestsBadgeCount != null) {
            requestsBadgeCount.text = inboundRequests.Count.ToString();
        }
    }

    #endregion

    #region Add/Requests Modal Content

    public void RefreshRequestsList() {
        if (accountDataGO == null || accountDataGO.accountData == null) return;

        ClearContainer(inboundRequestsContent);
        ClearContainer(outboundRequestsContent);

        inboundRequests = serverApi.GetFriendRequests(accountDataGO.accountData) ?? new List<FriendRequestData>();
        outboundRequests = serverApi.GetOutboundFriendRequests(accountDataGO.accountData) ?? new List<FriendRequestData>();

        // Update badge (inbound only)
        if (requestsBadge != null) requestsBadge.SetActive(inboundRequests.Count > 0);
        if (requestsBadgeCount != null) requestsBadgeCount.text = inboundRequests.Count.ToString();

        // Populate inbound requests
        foreach (var request in inboundRequests) {
            if (friendRequestItemPfb == null || inboundRequestsContent == null) continue;
            GameObject item = Instantiate(friendRequestItemPfb, inboundRequestsContent);
            var listItem = item.GetComponent<FriendRequestItem>();
            if (listItem != null) {
                listItem.Setup(request, this, isOutbound: false);
            }
        }

        // Populate outbound requests
        foreach (var request in outboundRequests) {
            if (friendRequestItemPfb == null || outboundRequestsContent == null) continue;
            GameObject item = Instantiate(friendRequestItemPfb, outboundRequestsContent);
            var listItem = item.GetComponent<FriendRequestItem>();
            if (listItem != null) {
                listItem.Setup(request, this, isOutbound: true);
            }
        }
    }

    public void RefreshBlockedList() {
        if (accountDataGO == null || accountDataGO.accountData == null) return;

        ClearContainer(blockedListContent);
        var blocked = serverApi.GetBlockedPlayers(accountDataGO.accountData) ?? new List<BlockedPlayerData>();

        foreach (var player in blocked) {
            if (blockedPlayerItemPfb == null || blockedListContent == null) continue;
            GameObject item = Instantiate(blockedPlayerItemPfb, blockedListContent);
            var listItem = item.GetComponent<BlockedPlayerItem>();
            if (listItem != null) {
                listItem.Setup(player, this);
            }
        }
    }

    public void OnAddFriendClicked() {
        if (addFriendInput == null) return;

        string username = addFriendInput.text.Trim();
        if (string.IsNullOrEmpty(username)) {
            ShowStatus("Enter a username", Color.red);
            return;
        }

        var (statusCode, message) = serverApi.SendFriendRequest(accountDataGO.accountData, username);

        switch (statusCode) {
            case 201:
                ShowStatus("Request sent!", Color.green);
                addFriendInput.text = "";
                RefreshRequestsList();
                break;
            case 200:
                ShowStatus("You are now friends!", Color.green);
                addFriendInput.text = "";
                RefreshFriendsList();
                break;
            case 404:
                ShowStatus("User not found", Color.red);
                break;
            case 409:
                if (message.Contains("already_friends"))
                    ShowStatus("Already friends", Color.yellow);
                else if (message.Contains("blocked"))
                    ShowStatus("Cannot send request", Color.red);
                else if (message.Contains("request_exists"))
                    ShowStatus("Request already sent", Color.yellow);
                else
                    ShowStatus("Request failed", Color.red);
                break;
            default:
                ShowStatus("Error occurred", Color.red);
                break;
        }
    }

    private void ShowStatus(string text, Color color) {
        if (addFriendStatusText == null) return;
        addFriendStatusText.text = text;
        addFriendStatusText.color = color;
        StartCoroutine(ClearStatusAfterDelay(3f));
    }

    private IEnumerator ClearStatusAfterDelay(float delay) {
        yield return new WaitForSeconds(delay);
        if (addFriendStatusText != null) {
            addFriendStatusText.text = "";
        }
    }

    #endregion

    #region Actions (Called by List Items)

    public void AcceptRequest(int requestId) {
        if (accountDataGO == null || accountDataGO.accountData == null) return;
        serverApi.AcceptFriendRequest(accountDataGO.accountData, requestId);
        RefreshRequestsList();
        RefreshFriendsList();
    }

    public void DeclineRequest(int requestId) {
        if (accountDataGO == null || accountDataGO.accountData == null) return;
        serverApi.DeclineFriendRequest(accountDataGO.accountData, requestId);
        RefreshRequestsList();
    }

    public void RemoveFriend(int friendId) {
        if (accountDataGO == null || accountDataGO.accountData == null) return;
        serverApi.RemoveFriend(accountDataGO.accountData, friendId);
        RefreshFriendsList();
    }

    public void BlockPlayer(int userId) {
        if (accountDataGO == null || accountDataGO.accountData == null) return;
        serverApi.BlockPlayer(accountDataGO.accountData, userId);
        RefreshFriendsList();
        if (blockedModal != null && blockedModal.activeSelf) {
            RefreshBlockedList();
        }
    }

    public void UnblockPlayer(int userId) {
        if (accountDataGO == null || accountDataGO.accountData == null) return;
        serverApi.UnblockPlayer(accountDataGO.accountData, userId);
        RefreshBlockedList();
    }

    // Used by RecentOpponentItem to send friend request by user ID
    public void SendFriendRequestById(int userId, string username) {
        if (accountDataGO == null || accountDataGO.accountData == null) return;
        serverApi.SendFriendRequest(accountDataGO.accountData, username);
        // Could refresh UI here if needed
    }

    public void AcceptLobbyInvite(int inviteId) {
        if (lobbyManager != null) {
            lobbyManager.AcceptInvite(inviteId);
        }
        lobbyInvites.RemoveAll(i => i.inviteId == inviteId);
        RefreshFriendsList();
    }

    public void DeclineLobbyInvite(int inviteId) {
        if (lobbyManager != null) {
            lobbyManager.DeclineInvite(inviteId);
        }
        lobbyInvites.RemoveAll(i => i.inviteId == inviteId);
        RefreshFriendsList();
    }

    #endregion

    #region Utility

    private void ClearContainer(Transform container) {
        if (container == null) return;
        foreach (Transform child in container) {
            // Skip noFriendsText so it doesn't get destroyed
            if (noFriendsText != null && child.gameObject == noFriendsText) continue;
            Destroy(child.gameObject);
        }
    }

    #endregion
}
