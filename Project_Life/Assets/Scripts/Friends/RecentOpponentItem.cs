using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RecentOpponentItem : MonoBehaviour {
    public TMP_Text displayNameText;
    public TMP_Text usernameText;
    public TMP_Text matchDateText;
    public Button addFriendButton;
    public Button blockButton;
    public GameObject alreadyFriendIndicator;
    public GameObject blockedIndicator;

    private RecentOpponentData opponentData;
    private FriendsManager manager;

    public void Setup(RecentOpponentData data, FriendsManager friendsManager) {
        opponentData = data;
        manager = friendsManager;

        if (displayNameText != null) displayNameText.text = data.displayName;
        if (usernameText != null) usernameText.text = "@" + data.username;
        if (matchDateText != null) matchDateText.text = data.matchDate.ToString("MMM dd");

        // Show appropriate state
        if (addFriendButton != null) addFriendButton.gameObject.SetActive(!data.isFriend && !data.isBlocked);
        if (alreadyFriendIndicator != null) alreadyFriendIndicator.SetActive(data.isFriend);
        if (blockedIndicator != null) blockedIndicator.SetActive(data.isBlocked);
        if (blockButton != null) blockButton.gameObject.SetActive(!data.isBlocked);

        if (addFriendButton != null) {
            addFriendButton.onClick.AddListener(() =>
                manager.SendFriendRequestById(opponentData.id, opponentData.username));
        }
        if (blockButton != null) {
            blockButton.onClick.AddListener(() => manager.BlockPlayer(opponentData.id));
        }
    }
}
