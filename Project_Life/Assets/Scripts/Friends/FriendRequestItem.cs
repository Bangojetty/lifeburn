using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FriendRequestItem : MonoBehaviour {
    public TMP_Text displayNameText;
    public GameObject buttonsObject;  // Accept/Decline buttons container
    public GameObject pendingObject;  // "Pending" indicator
    public Button acceptButton;
    public Button declineButton;

    private FriendRequestData requestData;
    private FriendsManager manager;

    public void Setup(FriendRequestData data, FriendsManager friendsManager, bool isOutbound) {
        requestData = data;
        manager = friendsManager;

        // Show sender name for inbound, receiver name for outbound
        if (displayNameText != null) {
            displayNameText.text = isOutbound ? data.receiverDisplayName : data.senderDisplayName;
        }

        // Show buttons for inbound, pending indicator for outbound
        if (buttonsObject != null) buttonsObject.SetActive(!isOutbound);
        if (pendingObject != null) pendingObject.SetActive(isOutbound);

        if (!isOutbound) {
            if (acceptButton != null) acceptButton.onClick.AddListener(() => manager.AcceptRequest(requestData.requestId));
            if (declineButton != null) declineButton.onClick.AddListener(() => manager.DeclineRequest(requestData.requestId));
        }
    }
}
