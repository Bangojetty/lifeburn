using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LobbyPlayerSlot : MonoBehaviour {
    [Header("UI Elements")]
    public GameObject playerIcon;
    public TMP_Text playerNameText;
    public GameObject readyIndicator;
    public GameObject hostIndicator;
    public Button kickButton;

    private LobbyPlayerData playerData;
    private LobbyManager lobbyManager;

    public void Setup(LobbyPlayerData player, bool isMe, bool canKick, LobbyManager manager) {
        playerData = player;
        lobbyManager = manager;

        if (playerIcon != null) playerIcon.SetActive(true);

        if (playerNameText != null) {
            playerNameText.text = player.displayName;
        }

        if (readyIndicator != null) {
            readyIndicator.SetActive(player.isReady);
        }

        if (hostIndicator != null) {
            hostIndicator.SetActive(player.isHost);
        }

        // Show kick button only if viewer is host and this isn't the host
        if (kickButton != null) {
            kickButton.gameObject.SetActive(canKick && !player.isHost && !isMe);
            kickButton.onClick.RemoveAllListeners();
            kickButton.onClick.AddListener(OnKickClicked);
        }
    }

    public void SetupEmpty() {
        if (playerIcon != null) playerIcon.SetActive(false);
        if (playerNameText != null) playerNameText.text = "Waiting for player...";
        if (readyIndicator != null) readyIndicator.SetActive(false);
        if (hostIndicator != null) hostIndicator.SetActive(false);
        if (kickButton != null) kickButton.gameObject.SetActive(false);
    }

    private void OnKickClicked() {
        if (lobbyManager != null && playerData != null) {
            lobbyManager.KickPlayer(playerData.playerId);
        }
    }
}
