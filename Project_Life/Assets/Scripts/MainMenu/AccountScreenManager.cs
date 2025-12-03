using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class AccountScreenManager : MonoBehaviour {
    private GameData gameData;
    private TMP_Text accountName;
    // Start is called before the first frame update
    void Start() {
        gameData = GameObject.Find("GameData").GetComponent<GameData>();
        // accountName.text = gameData.
    }

}
