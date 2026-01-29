using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LobbyInviteItem : MonoBehaviour {
    [Header("UI Elements")]
    public TMP_Text inviteText;
    public Button acceptButton;
    public Button declineButton;

    private LobbyInviteData inviteData;
    private FriendsManager friendsManager;

    public void Setup(LobbyInviteData invite, FriendsManager manager) {
        inviteData = invite;
        friendsManager = manager;

        if (inviteText != null) {
            string typeLabel = invite.lobbyType == "normal" ? "Normal" : "Tournament";
            inviteText.text = $"{invite.inviterDisplayName} ({typeLabel})";
        }

        if (acceptButton != null) {
            acceptButton.onClick.RemoveAllListeners();
            acceptButton.onClick.AddListener(OnAcceptClicked);
        }

        if (declineButton != null) {
            declineButton.onClick.RemoveAllListeners();
            declineButton.onClick.AddListener(OnDeclineClicked);
        }
    }

    private void OnAcceptClicked() {
        if (friendsManager != null && inviteData != null) {
            friendsManager.AcceptLobbyInvite(inviteData.inviteId);
        }
    }

    private void OnDeclineClicked() {
        if (friendsManager != null && inviteData != null) {
            friendsManager.DeclineLobbyInvite(inviteData.inviteId);
        }
    }
}
