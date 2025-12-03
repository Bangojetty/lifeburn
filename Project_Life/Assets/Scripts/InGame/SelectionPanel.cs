using TMPro;
using UnityEngine;

public class SelectionPanel : MonoBehaviour {
    public TMP_Text hideBtnText;
    public GameObject hideableSelectionDialogue;
    
    public void ToggleVisible() {
        hideBtnText.text = hideableSelectionDialogue.activeSelf ? "Show" : "Hide";
        hideableSelectionDialogue.SetActive(!hideableSelectionDialogue.activeSelf);
    }
}
