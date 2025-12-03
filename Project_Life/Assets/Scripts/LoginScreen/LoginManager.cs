using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoginManager : MonoBehaviour {
    public GameObject loginPanel;
    public GameObject newAccPanel;
    public GameObject currentPanel;
    public List<TMP_InputField> newAccFields;
    public List<TMP_InputField> loginFields;
    public List<TMP_InputField> currentFields;
    private int fieldIndex = 1;
    private bool loggingIn = false;
        
    private ServerApi serverApi = new();

    public TMP_InputField newEmail;
    public TMP_InputField newUsername;
    public TMP_InputField newPass;
    public TMP_InputField verNewPass;

    public TMP_InputField loginUsername;
    public TMP_InputField loginPassword;
    public TMP_Text loginError;

    public GameObject accountDataObj;
    public GameObject versionDataObj;

    public GameObject eBugger;
    public Ebug eBug;

    public GameObject audioManagerObj;
    private void Start() {
        DontDestroyOnLoadObjects();
        currentFields = loginFields;
    }
    private void Update() {
        if (Input.GetKeyDown(KeyCode.Return)) {
            Login();
        }
        IncrementTextField();
    }

    private void DontDestroyOnLoadObjects() {
        DontDestroyOnLoad(eBugger);
        DontDestroyOnLoad(accountDataObj);
        DontDestroyOnLoad(versionDataObj);
        DontDestroyOnLoad(audioManagerObj);
    }

    private void IncrementTextField() {
        if (!Input.GetKeyDown(KeyCode.Tab)) return;
        if (currentFields.Count <= fieldIndex) {
            fieldIndex = 0;
        }
        currentFields[fieldIndex].Select();
        fieldIndex++;
    }

    public void CreateNewAcc() {
        // verify all fields are entered and correct
        if (FieldIsEmpty(newEmail.text, "Please enter a valid email address")) return;
        if (FieldIsEmpty(newUsername.text, "Please enter a valid username")) return;
        if (FieldIsEmpty(newPass.text, "Please enter a valid password")) return;
        if (verNewPass.text == null || verNewPass.text != newPass.text) {
            loginError.text = "Passwords do no match";
            return;
        }
        if (newUsername.text.Length < 3) {
            loginError.text = "Username must be at least 3 characters.";
            return;
        }
        var rsp = serverApi.CreateNewAccount(newUsername.text, newUsername.text, newEmail.text, newPass.text);
        eBug.Log("server returned: " + rsp.StatusCode + " with message: " + rsp.Message);
        OpenLoginPanel();
    }

    public bool FieldIsEmpty(string input, string errorMessage) {
        if (string.IsNullOrEmpty(input)) {
            loginError.text = errorMessage;
            return true;
        }
        return false;
    }

    public void Login() {
        // already loggin in. this prevents spamming the button causing multiple login attempts
        if (loggingIn) return;
        loggingIn = true;
        // username too short
        if (loginUsername.text.Length < 3) {
            loginError.text = "Username must be at least 3 characters";
            loggingIn = false;
            return;
        }
        var accountData = serverApi.GetAccountData(loginUsername.text, loginPassword.text, out var httpErrorCode);
        // if there's an error with login (http request)
        if (DisplayErrorMessage(httpErrorCode, out var errorMessage)) {
            loginError.text = errorMessage;
            loggingIn = false;
            return;
        }
        // log in!
        accountDataObj.GetComponent<AccountDataGO>().accountData = accountData;
        Debug.Log("Account Data for Login: " + accountData);
        loggingIn = false;
        SceneManager.LoadScene("Main Menu");
    }

    private bool DisplayErrorMessage(int errorCode, out string errorMessage) {
        if (errorCode == 200) {
            errorMessage = "";
            return false;
        } 
        errorMessage = errorCode switch {
            0   => "Server is offline",
            400 => "Could not connect. Try again shortly.",
            401 => "Invalid username or password.",
            403 => "Access DENIED! Are you trying to hack the mainframe?!!?",
            404 => "Can't connect to server (check your connection or talk to @bangoJetty on Discord).",
            405 => "Silly rabbit! hacking's for nerds!",
            408 => "Can't connect to server (check your connection or talk to @bangoJetty on Discord).",
            429 => "WOAH... Slow down there. You only gotta click the button once lol.",
            500 => "Somebody messed up the server code. Contact support: @bangoJetty on Discord",
            501 => "Idk what you did but we don't have a response for that.",
            502 => "Check your internet connection (Bad Gateway)",
            503 => "Server is currently under maintenance, Thank you for your patience <3.",
            504 => "Check your internet connection (Gateway Timeout)",
            _   => "unknown error"
        };
        return true;
    }
    
    public void OpenNewAccPanel() {
        loginPanel.SetActive(false);
        newAccPanel.SetActive(true);
        currentFields = newAccFields;
        currentPanel = newAccPanel;
    }

    public void OpenLoginPanel() {
        newAccPanel.SetActive(false);
        loginPanel.SetActive(true);
        foreach(var field in loginFields) {
            field.text = "";
        }
        currentPanel = loginPanel;
        currentFields = loginFields;
    }

    public void QuitGame() {
        Application.Quit();
    }
}
