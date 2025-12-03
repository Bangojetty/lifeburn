using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class VersionChecker : MonoBehaviour {
    public string versionNum;
    // Start is called before the first frame update
    void Start() {
        SetVersionNumber();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        SetVersionNumber();
    }

    void SetVersionNumber() {
        GameObject versionObj = GameObject.Find("Version");
        if(versionObj != null) versionObj.GetComponent<TMP_Text>().text = "Ver " + versionNum;
    }
}
