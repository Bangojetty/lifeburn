using UnityEngine;
using TMPro;

public class InvitedPlayerItem : MonoBehaviour {
    public TMP_Text playerNameText;

    public void Setup(string displayName) {
        if (playerNameText != null) {
            playerNameText.text = displayName;
        }
    }
}
