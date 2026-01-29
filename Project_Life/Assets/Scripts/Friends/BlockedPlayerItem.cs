using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BlockedPlayerItem : MonoBehaviour {
    public TMP_Text displayNameText;
    public Button unblockButton;

    private BlockedPlayerData playerData;
    private FriendsManager manager;

    public void Setup(BlockedPlayerData data, FriendsManager friendsManager) {
        playerData = data;
        manager = friendsManager;

        if (displayNameText != null) displayNameText.text = data.displayName;

        if (unblockButton != null) unblockButton.onClick.AddListener(() => manager.UnblockPlayer(playerData.id));
    }
}
