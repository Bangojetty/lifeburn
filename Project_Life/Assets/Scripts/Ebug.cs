using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Ebug : MonoBehaviour {
    public GameObject ebugger;
    public TMP_Text eConsoleText;
    public GameObject eConsole;
    public TMP_Text secondaryConsoleText;
    public GameObject secondaryConsole;

    private string myLog;

    void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    void HandleLog(string logString, string stackTrace, LogType type)
    {
        myLog += logString + "\n";
        eConsoleText.text = myLog;  // Display the log on screen
    }
    private void Update() {
        if (Input.GetKeyDown("`")) {
            ebugger.SetActive(!ebugger.activeSelf);
        }
        if (Input.GetKeyDown("0")) {
            eConsole.SetActive(!eConsole.activeSelf);
            secondaryConsole.SetActive(!secondaryConsole.activeSelf);
        }
        
    }
    
    public void Log(string s) {
        eConsoleText.text += "\n" + s;
        eConsoleText.ForceMeshUpdate(true);
    }

    public void Slog(string s) {
        secondaryConsoleText.text += "\n" + s;
        secondaryConsoleText.ForceMeshUpdate(true);
    }
    
}
