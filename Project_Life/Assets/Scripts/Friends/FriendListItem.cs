using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class FriendListItem : MonoBehaviour, IPointerClickHandler {
    public TMP_Text displayNameText;
    public Image onlineIndicator;
    public Color onlineColor = Color.green;
    public Color offlineColor = Color.gray;
    public Button joinButton;  // Disabled for now, will be used later

    private FriendData friendData;
    private FriendsManager manager;

    public void Setup(FriendData data, FriendsManager friendsManager) {
        friendData = data;
        manager = friendsManager;

        if (displayNameText != null) displayNameText.text = data.displayName;
        if (onlineIndicator != null) onlineIndicator.color = data.isOnline ? onlineColor : offlineColor;
        if (joinButton != null) joinButton.gameObject.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData) {
        if (eventData.button == PointerEventData.InputButton.Right) {
            // Right click - show context menu
            manager.ShowContextMenu(friendData.id, Input.mousePosition);
        }
    }
}
