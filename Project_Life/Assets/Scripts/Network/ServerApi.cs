using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

public class ServerApi {
    // Server addresses - swap which one is used for baseAddress
    private const string LOCAL_SERVER = "http://localhost:5239/life/";
    private const string PUBLIC_SERVER = "http://157.230.138.62:5239/life/";

    private string baseAddress = PUBLIC_SERVER;


    private UnityWebRequest CreateRequest(string path, RequestType type = RequestType.GET, object data = null) {
        var request = new UnityWebRequest(path, type.ToString());

        if (data != null) {
            var bodyRaw = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data));
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        }

        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        return request;
    }

    private async Task<T> Get<T>(string service) {
        var getRequest = CreateRequest(baseAddress + service);
        getRequest.SendWebRequest();

        while (!getRequest.isDone) await Task.Delay(10);
        return JsonConvert.DeserializeObject<T>(getRequest.downloadHandler.text);
    }

    private async Task<T> Post<T>(string service, object payload) {
        UnityWebRequest postRequest = CreateRequest(baseAddress + service, RequestType.POST, payload);
        postRequest.SendWebRequest();

        while (!postRequest.isDone) await Task.Delay(10);
        return JsonConvert.DeserializeObject<T>(postRequest.downloadHandler.text);
    }

    private async Task<T> Put<T>(string service, object payload) {
        UnityWebRequest postRequest = CreateRequest(baseAddress + service, RequestType.PUT, payload);
        postRequest.SendWebRequest();

        while (!postRequest.isDone) await Task.Delay(10);
        return JsonConvert.DeserializeObject<T>(postRequest.downloadHandler.text);
    }

    public void SetMatchID(int id) {
        var md = new MatchState();
        md.matchId = id;
        Task task = Post<MatchState>("data", md);
        task.RunSynchronously();
        task.Wait();
    }

    private string PassToHash(string pass) {
        byte[] encodedPass = new UTF8Encoding().GetBytes(pass);
        byte[] hash = ((HashAlgorithm)CryptoConfig.CreateFromName("MD5")).ComputeHash(encodedPass);
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();
    }

    public AccountData GetAccountData(string name, string pass, out int httpErrorCode) {
        string hash = PassToHash(pass);
        UnityWebRequest getRequest = CreateRequest(baseAddress + "accounts");
        getRequest.SetRequestHeader("Authorization", "Basic " + Base64Encode(name + ":" + hash));
        getRequest.SendWebRequest();
        while (!getRequest.isDone) Task.Delay(10);
        if (getRequest.responseCode == 200) {
            httpErrorCode = (int)getRequest.responseCode;
            return JsonConvert.DeserializeObject<AccountData>(getRequest.downloadHandler.text);
        }
        httpErrorCode = (int)getRequest.responseCode;
        Debug.Log("GetAccountData returned: " + getRequest.responseCode);
        return null;
    }

    public List<DeckData> GetAccountDecks(AccountData accountData) {
        UnityWebRequest getRequest = CreateRequest(baseAddress + "accounts/decks");
        getRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        getRequest.SendWebRequest();
        while (!getRequest.isDone) Task.Delay(10);
        if (getRequest.responseCode == 200) {
            return JsonConvert.DeserializeObject<List<DeckData>>(getRequest.downloadHandler.text);
        }

        Debug.Log("GetAccountDecks returned: " + getRequest.responseCode);
        return null;
    }

    // temp api call for alpha testing
    public List<CardDisplayData> GetAllCards() {
        UnityWebRequest getRequest = CreateRequest(baseAddress + "cards");
        getRequest.SendWebRequest();
        while (!getRequest.isDone) Task.Delay(10);
        if (getRequest.responseCode == 200) {
            try {
                return JsonConvert.DeserializeObject<List<CardDisplayData>>(getRequest.downloadHandler.text);
            }
            catch (Exception e) {
                Debug.Log("GetAllCards Deserialize threw " + e);
            }
        }

        Debug.Log("GetAccountDecks returned: " + getRequest.responseCode);
        return null;
    }

    public MatchState PostOptionSelection(AccountData accountData, int optionIndex) {
        UnityWebRequest postRequest = CreateRequest(baseAddress + $"options/{optionIndex}", RequestType.POST);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        if (postRequest.responseCode == 200) {
            return JsonConvert.DeserializeObject<MatchState>(postRequest.downloadHandler.text);
        }

        Debug.Log("RefreshQueue returned code: " + postRequest.responseCode);
        return null;
    }

    private string Base64Encode(string s) {
        var sBytes = Encoding.UTF8.GetBytes(s);
        return Convert.ToBase64String(sBytes);
    }

    public CreateAccountResponse CreateNewAccount(string displayName, string username, string email, string password) {
        var accountData = new AccountData(0, displayName, username, email, PassToHash(password));
        UnityWebRequest postRequest = CreateRequest(baseAddress + "accounts", RequestType.POST, accountData);
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        return new CreateAccountResponse(postRequest.responseCode, postRequest.downloadHandler.text);
    }

    public List<int> GetAccountCards(AccountData accountData) {
        UnityWebRequest getRequest = CreateRequest(baseAddress + "accounts/cards");
        getRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        getRequest.SendWebRequest();
        while (!getRequest.isDone) Task.Delay(10);
        if (getRequest.responseCode == 200) {
            return JsonConvert.DeserializeObject<List<int>>(getRequest.downloadHandler.text);
        }

        Debug.Log("GetAccountCards returned: " + getRequest.responseCode);
        return null;
    }

    public long CreateOrUpdateDeck(DeckData deckData, AccountData accountData) {
        UnityWebRequest postRequest = CreateRequest(baseAddress + "accounts/decks", RequestType.POST, deckData);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        return postRequest.responseCode;
    }

    public long DeleteDeck(int deckId, AccountData accountData) {
        UnityWebRequest deleteRequest = CreateRequest(baseAddress + $"accounts/decks/{deckId}", RequestType.DELETE);
        deleteRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        deleteRequest.SendWebRequest();
        while (!deleteRequest.isDone) Task.Delay(10);
        return deleteRequest.responseCode;
    }

    public MatchState GetNewMatchData(AccountData accountData, int deckId) {
        Debug.Log("request: GetNewMatchData");
        UnityWebRequest getRequest = CreateRequest(baseAddress + "queue/" + deckId);
        getRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        getRequest.SendWebRequest();
        while (!getRequest.isDone) Task.Delay(10);
        if (getRequest.responseCode == 200) {
            return JsonConvert.DeserializeObject<MatchState>(getRequest.downloadHandler.text);
        }

        Debug.Log("RefreshQueue returned code: " + getRequest.responseCode);
        return null;
    }

    public void ExitQueue(AccountData accountData) {
        Debug.Log("request: ExitQueue");
        UnityWebRequest deleteRequest = CreateRequest(baseAddress + "queue", RequestType.DELETE);
        deleteRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        deleteRequest.SendWebRequest();
        while (!deleteRequest.isDone) Task.Delay(10);
        Debug.Log("ExitQueue returned code: " + deleteRequest.responseCode);
    }

    public MatchState CreateTestMatch(AccountData accountData, int deckId) {
        Debug.Log("request: CreateTestMatch");
        UnityWebRequest postRequest = CreateRequest(baseAddress + $"test-match/{deckId}", RequestType.POST);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        if (postRequest.responseCode == 200) {
            return JsonConvert.DeserializeObject<MatchState>(postRequest.downloadHandler.text);
        }
        Debug.Log("CreateTestMatch returned code: " + postRequest.responseCode);
        return null;
    }

    public MatchState GameReadyCheck(AccountData accountData, int matchId) {
        Debug.Log("request: GameReadyCheck");
        UnityWebRequest getRequest = CreateRequest(baseAddress + $"match/{matchId}/game-ready", RequestType.GET);
        getRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        getRequest.SendWebRequest();
        while (!getRequest.isDone) Task.Delay(10);
        if (getRequest.responseCode == 200) {
            return JsonConvert.DeserializeObject<MatchState>(getRequest.downloadHandler.text);
        }

        Debug.Log("result: " + getRequest.result);
        Debug.Log("error: " + getRequest.error);
        Debug.Log("GameReadyCheck returned code: " + getRequest.responseCode);
        return null;
    }

    #region In-Game-Requests

    public MatchState GetMatchState(AccountData accountData, int matchId) {
        Debug.Log("request: GetMatchState");
        UnityWebRequest getRequest = CreateRequest(baseAddress + $"match/{matchId}", RequestType.GET);
        getRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        getRequest.SendWebRequest();
        while (!getRequest.isDone) Task.Delay(10);
        if (getRequest.responseCode == 200) {
            return JsonConvert.DeserializeObject<MatchState>(getRequest.downloadHandler.text);
        }

        Debug.Log("GetMatchState returned code: " + getRequest.responseCode);
        return null;
    }

    public void SendChoice(AccountData accountData, int matchId, int choiceIndex) {
        Debug.Log("request: SendChoice");
        UnityWebRequest postRequest =
            CreateRequest(baseAddress + $"match/{matchId}/choose/{choiceIndex}", RequestType.POST);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        if (postRequest.responseCode == 200) {
            Debug.Log("SendChoice successful");
        }
        Debug.Log("SendChoice returned code: " + postRequest.responseCode);
    }

    public void SendFinalOrdering(AccountData accountData, int matchId, List<int> finalOrderList) {
        Debug.Log("request: SendFinalOrdering");
        UnityWebRequest postRequest =
            CreateRequest(baseAddress + $"match/{matchId}/ordering", RequestType.POST, finalOrderList);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        if (postRequest.responseCode == 200) {
            Debug.Log("SendFinalOrdering successful");
        }
        Debug.Log("SendFinalOrdering returned code: " + postRequest.responseCode);
    }

    public void SendCardSelection(AccountData accountData, int matchId, List<List<int>> cardUids) {
        Debug.Log("request: SendCardSelection");
        UnityWebRequest postRequest =
            CreateRequest(baseAddress + $"match/{matchId}/card-select", RequestType.POST, cardUids);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        if (postRequest.responseCode == 200) {
            Debug.Log("SendCardSelection successful");
        }
        Debug.Log("SendCardSelection returned code: " + postRequest.responseCode);
    }
    
    public void SendTributeSelection(AccountData accountData, int matchId, List<int> tributeUids) {
        Debug.Log("request: SendTributeSelection");
        UnityWebRequest postRequest =
            CreateRequest(baseAddress + $"match/{matchId}/tribute-select", RequestType.POST, tributeUids);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        if (postRequest.responseCode == 200) {
            Debug.Log("SendTributeSelection successful");
        }
        Debug.Log("SendTributeSelection returned code: " + postRequest.responseCode);
    }
    
    public void SendTargetSelection(AccountData accountData, int matchId, List<int> targetedUids) {
        Debug.Log("request: SendTargetSelection");
        UnityWebRequest postRequest =
            CreateRequest(baseAddress + $"match/{matchId}/target-select", RequestType.POST, targetedUids);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        if (postRequest.responseCode == 200) {
            Debug.Log("SendTargetSelection successful");
        }
        Debug.Log("SendTargetSelection returned code: " + postRequest.responseCode);
    }
    
    public void SendCostSelection(AccountData accountData, int matchId, List<int> selectedUids) {
        Debug.Log("request: SendCostSelection");
        UnityWebRequest postRequest =
            CreateRequest(baseAddress + $"match/{matchId}/cost-select", RequestType.POST, selectedUids);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        if (postRequest.responseCode == 200) {
            Debug.Log("SendCostSelection successful");
        }
        Debug.Log("SendCostSelection returned code: " + postRequest.responseCode);
    }

    public void SendRitualChoice(AccountData accountData, int matchId, int summonUid) {
        Debug.Log("request: SendRitualChoice");
        UnityWebRequest postRequest =
            CreateRequest(baseAddress + $"match/{matchId}/ritual-choice/{summonUid}", RequestType.POST);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        if (postRequest.responseCode == 200) {
            Debug.Log("SendRitualChoice successful");
        }
        Debug.Log("SendRitualChoice returned code: " + postRequest.responseCode);
    }

    public void SetX(AccountData accountData, int matchId, int amount) {
        Debug.Log("request: SetX");
        UnityWebRequest postRequest =
            CreateRequest(baseAddress + $"match/{matchId}/set-x/{amount}", RequestType.POST);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        if (postRequest.responseCode == 200) {
            Debug.Log("SetX successful");
        }
        Debug.Log("SetX returned code: " + postRequest.responseCode);
    }
    
    public void SetAmount(AccountData accountData, int matchId, int amount) {
        Debug.Log("request: SetAmount");
        UnityWebRequest postRequest =
            CreateRequest(baseAddress + $"match/{matchId}/set-amount/{amount}", RequestType.POST);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        if (postRequest.responseCode == 200) {
            Debug.Log("SetAmount successful");
        }
        Debug.Log("SetAmount returned code: " + postRequest.responseCode);
    }

    public void CancelCast(AccountData accountData, int matchId) {
        Debug.Log("request: CancelCast");
        UnityWebRequest postRequest =
            CreateRequest(baseAddress + $"match/{matchId}/cancel-cast", RequestType.POST);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        if (postRequest.responseCode == 200) {
            Debug.Log("CancelCast successful");
        }
        Debug.Log("CancelCast returned code: " + postRequest.responseCode);
    }

    public void PassPrio(AccountData accountData, int matchId, int? passToPhase = null) {
        string url = baseAddress + $"match/{matchId}/pass";
        if (passToPhase.HasValue) {
            url += $"?passToPhase={passToPhase.Value}";
        }
        UnityWebRequest getRequest = CreateRequest(url);
        getRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        getRequest.SendWebRequest();
        while (!getRequest.isDone) Task.Delay(10);
        if (getRequest.responseCode == 200) {
            Debug.Log("PassPrio successful");
        }
        Debug.Log("PassPrio returned code: " + getRequest.responseCode);
    }

    public void AttemptToCast(AccountData accountData, int matchId, int cardUid) {
        UnityWebRequest getRequest = CreateRequest(baseAddress + $"match/{matchId}/cast-attempt/{cardUid}");
        getRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        getRequest.SendWebRequest();
        while (!getRequest.isDone) Task.Delay(10);
        if (getRequest.responseCode == 200) {
            Debug.Log("AttemptToCast successful");
        }

        Debug.Log("AttemptToCast returned code: " + getRequest.responseCode);
    }
    
    public void AttemptToActivate(AccountData accountData, int matchId, int cardUid) {
        UnityWebRequest getRequest = CreateRequest(baseAddress + $"match/{matchId}/activation-attempt/{cardUid}");
        getRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        getRequest.SendWebRequest();
        while (!getRequest.isDone) Task.Delay(10);
        if (getRequest.responseCode == 200) {
            Debug.Log("AttemptToActivate successful");
        }

        Debug.Log("AttemptToActivate returned code: " + getRequest.responseCode);
    }
    
    

    public void Attack(AccountData accountData, int matchId, Dictionary<int, int> attackUids){
        UnityWebRequest postRequest = CreateRequest(baseAddress + $"match/{matchId}/attack", RequestType.POST, attackUids);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        if (postRequest.responseCode == 200) {
            Debug.Log("Attack successful");
        }
        Debug.Log("Attack returned code: " + postRequest.responseCode);
    }
    
    public List<int> GetAttackables(AccountData accountData, int matchId, int attackingUid) {
        UnityWebRequest getRequest = CreateRequest(baseAddress + $"match/{matchId}/attackables/{attackingUid}");
        getRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        getRequest.SendWebRequest();
        while (!getRequest.isDone) Task.Delay(10);
        if (getRequest.responseCode == 200) {
            Debug.Log("GetAttackables successful");
            return JsonConvert.DeserializeObject<List<int>>(getRequest.downloadHandler.text);
        }
        Debug.Log("GetAttackables returned code: " + getRequest.responseCode);
        return null;
    }
    
    public void AssignAttack(AccountData accountData, int matchId, (int, int ) attackUids){
        UnityWebRequest postRequest = 
            CreateRequest(baseAddress + $"match/{matchId}/attack/assign-attack", RequestType.POST, attackUids);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        if (postRequest.responseCode == 200) {
            Debug.Log("AssignAttack successful");
        }
        Debug.Log("AssignAttack returned code: " + postRequest.responseCode);
    }
    
    public void UnAssignAttack(AccountData accountData, int matchId, int attackerUid){
        UnityWebRequest deleteRequest = 
            CreateRequest(baseAddress + $"match/{matchId}/attack/unassign-attack/{attackerUid}", RequestType.DELETE);
        deleteRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        deleteRequest.SendWebRequest();
        while (!deleteRequest.isDone) Task.Delay(10);
        if (deleteRequest.responseCode == 200) {
            Debug.Log("UnAssignAttack successful");
        }
        Debug.Log("UnAssignAttack returned code: " + deleteRequest.responseCode);
    }
    
    public void AddSecondaryAttacker(AccountData accountData, int matchId, (int, int) attackUids) {
        UnityWebRequest postRequest = CreateRequest(baseAddress + $"match/{matchId}/add-secondary-attacker", RequestType.POST, attackUids);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        if (postRequest.responseCode == 200) {
            Debug.Log("AddToAttackers sucessful");
        }
        Debug.Log("AddToAttackers returned code: " + postRequest.responseCode);
    }

    public CardDisplayData GetCard(AccountData accountData, int id) {
        UnityWebRequest getRequest = CreateRequest(baseAddress + $"cards/{id}");
        getRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        getRequest.SendWebRequest();
        while (!getRequest.isDone) Task.Delay(10);
        if (getRequest.responseCode == 200) {
            return JsonConvert.DeserializeObject<CardDisplayData>(getRequest.downloadHandler.text);
        }
        Debug.Log("GetCard returned code: " + getRequest.responseCode);
        return null;
    }
    

    // Legacy
    public int? GetCardAtDeckPos(int deckPos, AccountData accountData) {
        UnityWebRequest getRequest = CreateRequest(baseAddress + $"card-in-deck/{deckPos}");
        getRequest.SetRequestHeader("Authorization", "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        getRequest.SendWebRequest();
        while (!getRequest.isDone) Task.Delay(10);
        if (getRequest.responseCode == 200) {
            int cardId = JsonConvert.DeserializeObject<int>(getRequest.downloadHandler.text);
            Debug.Log("got card with id: " + cardId);
            return cardId;
        }
        Debug.Log("GetCardAtDeckPos returned with code: " + getRequest.responseCode);
        return null;
    }

    public int? Draw(AccountData accountData) {
        UnityWebRequest getRequest = CreateRequest(baseAddress + "draw");
        getRequest.SetRequestHeader("Authorization", "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        getRequest.SendWebRequest();
        while (!getRequest.isDone) Task.Delay(10);
        if (getRequest.responseCode == 200) {
            int cardId = JsonConvert.DeserializeObject<int>(getRequest.downloadHandler.text);
            Debug.Log("Drew card with id: " + cardId);
            return cardId;
        }
        Debug.Log("Draw returned with code: " + getRequest.responseCode);
        return null;
    }

    public void SendAction(ActionMessage actionMessage, AccountData accountData) {
        UnityWebRequest postRequest = CreateRequest(baseAddress + "set-action", RequestType.POST, actionMessage);
        postRequest.SetRequestHeader("Authorization", "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        Debug.Log("SendAction returned code: " + postRequest.responseCode);
    }
    
    public ActionMessage GetAction(AccountData accountData) {
        UnityWebRequest getRequest = CreateRequest(baseAddress + "get-action");
        getRequest.SetRequestHeader("Authorization", "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        getRequest.SendWebRequest();
        while (!getRequest.isDone) Task.Delay(10);
        if (getRequest.responseCode == 200) {
            return JsonConvert.DeserializeObject<ActionMessage>(getRequest.downloadHandler.text);
        }
        Debug.Log("GetAction returned code: " + getRequest.responseCode);
        return null;
    }
    #endregion

    #region Friends API

    public List<FriendData> GetFriends(AccountData accountData) {
        Debug.Log("request: GetFriends");
        UnityWebRequest getRequest = CreateRequest(baseAddress + "friends");
        getRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        getRequest.SendWebRequest();
        while (!getRequest.isDone) Task.Delay(10);
        if (getRequest.responseCode == 200) {
            return JsonConvert.DeserializeObject<List<FriendData>>(getRequest.downloadHandler.text);
        }
        Debug.Log("GetFriends returned: " + getRequest.responseCode);
        return null;
    }

    public (long statusCode, string message) SendFriendRequest(AccountData accountData, string targetUsername) {
        Debug.Log("request: SendFriendRequest");
        UnityWebRequest postRequest = CreateRequest(baseAddress + $"friends/request/{targetUsername}", RequestType.POST);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        Debug.Log("SendFriendRequest returned: " + postRequest.responseCode);
        return (postRequest.responseCode, postRequest.downloadHandler?.text ?? "");
    }

    public List<FriendRequestData> GetFriendRequests(AccountData accountData) {
        Debug.Log("request: GetFriendRequests");
        UnityWebRequest getRequest = CreateRequest(baseAddress + "friends/requests");
        getRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        getRequest.SendWebRequest();
        while (!getRequest.isDone) Task.Delay(10);
        if (getRequest.responseCode == 200) {
            return JsonConvert.DeserializeObject<List<FriendRequestData>>(getRequest.downloadHandler.text);
        }
        Debug.Log("GetFriendRequests returned: " + getRequest.responseCode);
        return null;
    }

    public List<FriendRequestData> GetOutboundFriendRequests(AccountData accountData) {
        Debug.Log("request: GetOutboundFriendRequests");
        UnityWebRequest getRequest = CreateRequest(baseAddress + "friends/requests/outbound");
        getRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        getRequest.SendWebRequest();
        while (!getRequest.isDone) Task.Delay(10);
        if (getRequest.responseCode == 200) {
            return JsonConvert.DeserializeObject<List<FriendRequestData>>(getRequest.downloadHandler.text);
        }
        Debug.Log("GetOutboundFriendRequests returned: " + getRequest.responseCode);
        return null;
    }

    public long AcceptFriendRequest(AccountData accountData, int requestId) {
        Debug.Log("request: AcceptFriendRequest");
        UnityWebRequest postRequest = CreateRequest(baseAddress + $"friends/requests/{requestId}/accept", RequestType.POST);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        Debug.Log("AcceptFriendRequest returned: " + postRequest.responseCode);
        return postRequest.responseCode;
    }

    public long DeclineFriendRequest(AccountData accountData, int requestId) {
        Debug.Log("request: DeclineFriendRequest");
        UnityWebRequest deleteRequest = CreateRequest(baseAddress + $"friends/requests/{requestId}", RequestType.DELETE);
        deleteRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        deleteRequest.SendWebRequest();
        while (!deleteRequest.isDone) Task.Delay(10);
        Debug.Log("DeclineFriendRequest returned: " + deleteRequest.responseCode);
        return deleteRequest.responseCode;
    }

    public long RemoveFriend(AccountData accountData, int friendId) {
        Debug.Log("request: RemoveFriend");
        UnityWebRequest deleteRequest = CreateRequest(baseAddress + $"friends/{friendId}", RequestType.DELETE);
        deleteRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        deleteRequest.SendWebRequest();
        while (!deleteRequest.isDone) Task.Delay(10);
        Debug.Log("RemoveFriend returned: " + deleteRequest.responseCode);
        return deleteRequest.responseCode;
    }

    public long BlockPlayer(AccountData accountData, int userId) {
        Debug.Log("request: BlockPlayer");
        UnityWebRequest postRequest = CreateRequest(baseAddress + $"blocks/{userId}", RequestType.POST);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        Debug.Log("BlockPlayer returned: " + postRequest.responseCode);
        return postRequest.responseCode;
    }

    public long UnblockPlayer(AccountData accountData, int userId) {
        Debug.Log("request: UnblockPlayer");
        UnityWebRequest deleteRequest = CreateRequest(baseAddress + $"blocks/{userId}", RequestType.DELETE);
        deleteRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        deleteRequest.SendWebRequest();
        while (!deleteRequest.isDone) Task.Delay(10);
        Debug.Log("UnblockPlayer returned: " + deleteRequest.responseCode);
        return deleteRequest.responseCode;
    }

    public List<BlockedPlayerData> GetBlockedPlayers(AccountData accountData) {
        Debug.Log("request: GetBlockedPlayers");
        UnityWebRequest getRequest = CreateRequest(baseAddress + "blocks");
        getRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        getRequest.SendWebRequest();
        while (!getRequest.isDone) Task.Delay(10);
        if (getRequest.responseCode == 200) {
            return JsonConvert.DeserializeObject<List<BlockedPlayerData>>(getRequest.downloadHandler.text);
        }
        Debug.Log("GetBlockedPlayers returned: " + getRequest.responseCode);
        return null;
    }

    public List<RecentOpponentData> GetRecentOpponents(AccountData accountData) {
        Debug.Log("request: GetRecentOpponents");
        UnityWebRequest getRequest = CreateRequest(baseAddress + "opponents/recent");
        getRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        getRequest.SendWebRequest();
        while (!getRequest.isDone) Task.Delay(10);
        if (getRequest.responseCode == 200) {
            return JsonConvert.DeserializeObject<List<RecentOpponentData>>(getRequest.downloadHandler.text);
        }
        Debug.Log("GetRecentOpponents returned: " + getRequest.responseCode);
        return null;
    }

    public void SendHeartbeat(AccountData accountData) {
        // Fire and forget - don't wait for response
        UnityWebRequest postRequest = CreateRequest(baseAddress + "heartbeat", RequestType.POST);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
    }

    #endregion

    #region Lobby API

    public (long statusCode, LobbyData lobby) CreateLobby(AccountData accountData, string lobbyType, int maxPlayers = 2) {
        Debug.Log("request: CreateLobby");
        var payload = new { lobbyType, maxPlayers };
        UnityWebRequest postRequest = CreateRequest(baseAddress + "lobbies", RequestType.POST, payload);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        Debug.Log("CreateLobby returned: " + postRequest.responseCode);
        if (postRequest.responseCode == 201) {
            return (postRequest.responseCode, JsonConvert.DeserializeObject<LobbyData>(postRequest.downloadHandler.text));
        }
        return (postRequest.responseCode, null);
    }

    public LobbyData GetLobby(AccountData accountData, int lobbyId) {
        Debug.Log("request: GetLobby");
        UnityWebRequest getRequest = CreateRequest(baseAddress + $"lobbies/{lobbyId}");
        getRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        getRequest.SendWebRequest();
        while (!getRequest.isDone) Task.Delay(10);
        if (getRequest.responseCode == 200) {
            return JsonConvert.DeserializeObject<LobbyData>(getRequest.downloadHandler.text);
        }
        Debug.Log("GetLobby returned: " + getRequest.responseCode);
        return null;
    }

    public long LeaveLobby(AccountData accountData, int lobbyId) {
        Debug.Log("request: LeaveLobby");
        UnityWebRequest deleteRequest = CreateRequest(baseAddress + $"lobbies/{lobbyId}", RequestType.DELETE);
        deleteRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        deleteRequest.SendWebRequest();
        while (!deleteRequest.isDone) Task.Delay(10);
        Debug.Log("LeaveLobby returned: " + deleteRequest.responseCode);
        return deleteRequest.responseCode;
    }

    // Leave/concede the current match - the leaving player forfeits and the match ends
    public long LeaveMatch(AccountData accountData, int matchId) {
        Debug.Log("request: LeaveMatch");
        UnityWebRequest postRequest = CreateRequest(baseAddress + $"match/{matchId}/leave", RequestType.POST);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        Debug.Log("LeaveMatch returned: " + postRequest.responseCode);
        return postRequest.responseCode;
    }

    public long KickPlayer(AccountData accountData, int lobbyId, int playerId) {
        Debug.Log("request: KickPlayer");
        UnityWebRequest postRequest = CreateRequest(baseAddress + $"lobbies/{lobbyId}/kick/{playerId}", RequestType.POST);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        Debug.Log("KickPlayer returned: " + postRequest.responseCode);
        return postRequest.responseCode;
    }

    public long SendLobbyInvite(AccountData accountData, int lobbyId, int friendId) {
        Debug.Log("request: SendLobbyInvite");
        UnityWebRequest postRequest = CreateRequest(baseAddress + $"lobbies/{lobbyId}/invite/{friendId}", RequestType.POST);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        Debug.Log("SendLobbyInvite returned: " + postRequest.responseCode);
        return postRequest.responseCode;
    }

    public List<LobbyInviteData> GetLobbyInvites(AccountData accountData) {
        Debug.Log("request: GetLobbyInvites");
        UnityWebRequest getRequest = CreateRequest(baseAddress + "lobby-invites");
        getRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        getRequest.SendWebRequest();
        while (!getRequest.isDone) Task.Delay(10);
        if (getRequest.responseCode == 200) {
            return JsonConvert.DeserializeObject<List<LobbyInviteData>>(getRequest.downloadHandler.text);
        }
        Debug.Log("GetLobbyInvites returned: " + getRequest.responseCode);
        return null;
    }

    public (long statusCode, LobbyData lobby) AcceptLobbyInvite(AccountData accountData, int inviteId) {
        Debug.Log("request: AcceptLobbyInvite");
        UnityWebRequest postRequest = CreateRequest(baseAddress + $"lobby-invites/{inviteId}/accept", RequestType.POST);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        Debug.Log("AcceptLobbyInvite returned: " + postRequest.responseCode);
        if (postRequest.responseCode == 200) {
            return (postRequest.responseCode, JsonConvert.DeserializeObject<LobbyData>(postRequest.downloadHandler.text));
        }
        return (postRequest.responseCode, null);
    }

    public long DeclineLobbyInvite(AccountData accountData, int inviteId) {
        Debug.Log("request: DeclineLobbyInvite");
        UnityWebRequest deleteRequest = CreateRequest(baseAddress + $"lobby-invites/{inviteId}", RequestType.DELETE);
        deleteRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        deleteRequest.SendWebRequest();
        while (!deleteRequest.isDone) Task.Delay(10);
        Debug.Log("DeclineLobbyInvite returned: " + deleteRequest.responseCode);
        return deleteRequest.responseCode;
    }

    public long SelectLobbyDeck(AccountData accountData, int lobbyId, int deckId) {
        Debug.Log("request: SelectLobbyDeck");
        UnityWebRequest postRequest = CreateRequest(baseAddress + $"lobbies/{lobbyId}/deck/{deckId}", RequestType.POST);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        Debug.Log("SelectLobbyDeck returned: " + postRequest.responseCode);
        return postRequest.responseCode;
    }

    public (long statusCode, LobbyData lobby) ToggleLobbyReady(AccountData accountData, int lobbyId) {
        Debug.Log("request: ToggleLobbyReady");
        UnityWebRequest postRequest = CreateRequest(baseAddress + $"lobbies/{lobbyId}/ready", RequestType.POST);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        Debug.Log("ToggleLobbyReady returned: " + postRequest.responseCode);
        if (postRequest.responseCode == 200) {
            return (postRequest.responseCode, JsonConvert.DeserializeObject<LobbyData>(postRequest.downloadHandler.text));
        }
        return (postRequest.responseCode, null);
    }

    public (long statusCode, int? matchId) StartLobbyGame(AccountData accountData, int lobbyId) {
        Debug.Log("request: StartLobbyGame");
        UnityWebRequest postRequest = CreateRequest(baseAddress + $"lobbies/{lobbyId}/start", RequestType.POST);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        Debug.Log("StartLobbyGame returned: " + postRequest.responseCode);
        if (postRequest.responseCode == 200) {
            var result = JsonConvert.DeserializeAnonymousType(postRequest.downloadHandler.text, new { matchId = 0 });
            return (postRequest.responseCode, result?.matchId);
        }
        return (postRequest.responseCode, null);
    }

    public long UpdateLobbySettings(AccountData accountData, int lobbyId, string lobbyType, string lobbySubtype) {
        Debug.Log("request: UpdateLobbySettings");
        var payload = new { lobbyType, lobbySubtype };
        UnityWebRequest postRequest = CreateRequest(baseAddress + $"lobbies/{lobbyId}/settings", RequestType.POST, payload);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        Debug.Log("UpdateLobbySettings returned: " + postRequest.responseCode);
        return postRequest.responseCode;
    }

    public long UpdateLobbyBestOf(AccountData accountData, int lobbyId, int bestOf) {
        Debug.Log("request: UpdateLobbyBestOf");
        var payload = new { bestOf };
        UnityWebRequest postRequest = CreateRequest(baseAddress + $"lobbies/{lobbyId}/best-of", RequestType.POST, payload);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        Debug.Log("UpdateLobbyBestOf returned: " + postRequest.responseCode);
        return postRequest.responseCode;
    }

    #endregion

    #region Draft API

    public (long statusCode, DraftDisplayState draft) StartDraft(AccountData accountData, int lobbyId) {
        Debug.Log("request: StartDraft");
        UnityWebRequest postRequest = CreateRequest(baseAddress + $"lobbies/{lobbyId}/start-draft", RequestType.POST);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        Debug.Log("StartDraft returned: " + postRequest.responseCode);
        if (postRequest.responseCode == 201) {
            return (postRequest.responseCode, JsonConvert.DeserializeObject<DraftDisplayState>(postRequest.downloadHandler.text));
        }
        return (postRequest.responseCode, null);
    }

    public DraftDisplayState GetDraftState(AccountData accountData, int draftId) {
        Debug.Log("request: GetDraftState");
        UnityWebRequest getRequest = CreateRequest(baseAddress + $"draft/{draftId}");
        getRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        getRequest.SendWebRequest();
        while (!getRequest.isDone) Task.Delay(10);
        if (getRequest.responseCode == 200) {
            return JsonConvert.DeserializeObject<DraftDisplayState>(getRequest.downloadHandler.text);
        }
        Debug.Log("GetDraftState returned: " + getRequest.responseCode);
        return null;
    }

    public (long statusCode, DraftDisplayState draft) SubmitDraftPick(AccountData accountData, int draftId, int cardId) {
        Debug.Log("request: SubmitDraftPick");
        UnityWebRequest postRequest = CreateRequest(baseAddress + $"draft/{draftId}/pick/{cardId}", RequestType.POST);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        Debug.Log("SubmitDraftPick returned: " + postRequest.responseCode);
        if (postRequest.responseCode == 200) {
            return (postRequest.responseCode, JsonConvert.DeserializeObject<DraftDisplayState>(postRequest.downloadHandler.text));
        }
        return (postRequest.responseCode, null);
    }

    public (long statusCode, DraftDisplayState draft) SubmitDraftDeck(AccountData accountData, int draftId, List<int> deckCardIds) {
        Debug.Log($"request: SubmitDraftDeck with {deckCardIds.Count} cards");
        var requestBody = new { deck = deckCardIds };
        UnityWebRequest postRequest = CreateRequest(baseAddress + $"draft/{draftId}/submit-deck", RequestType.POST, requestBody);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        Debug.Log("SubmitDraftDeck returned: " + postRequest.responseCode);
        if (postRequest.responseCode == 200) {
            return (postRequest.responseCode, JsonConvert.DeserializeObject<DraftDisplayState>(postRequest.downloadHandler.text));
        }
        Debug.LogError($"SubmitDraftDeck error: {postRequest.downloadHandler.text}");
        return (postRequest.responseCode, null);
    }

    public (long statusCode, DraftDisplayState draft) SkipToDeckBuilding(AccountData accountData, int draftId) {
        Debug.Log("request: SkipToDeckBuilding (DEBUG)");
        UnityWebRequest postRequest = CreateRequest(baseAddress + $"draft/{draftId}/skip-to-deckbuilding", RequestType.POST);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        Debug.Log("SkipToDeckBuilding returned: " + postRequest.responseCode);
        if (postRequest.responseCode == 200) {
            return (postRequest.responseCode, JsonConvert.DeserializeObject<DraftDisplayState>(postRequest.downloadHandler.text));
        }
        return (postRequest.responseCode, null);
    }

    public (long statusCode, int? matchId) StartDraftMatch(AccountData accountData, int draftId) {
        Debug.Log("request: StartDraftMatch");
        UnityWebRequest postRequest = CreateRequest(baseAddress + $"draft/{draftId}/start-match", RequestType.POST);
        postRequest.SetRequestHeader("Authorization",
            "Basic " + Base64Encode(accountData.username + ":" + accountData.hashedPassword));
        postRequest.SendWebRequest();
        while (!postRequest.isDone) Task.Delay(10);
        Debug.Log("StartDraftMatch returned: " + postRequest.responseCode);
        if (postRequest.responseCode == 200) {
            var result = JsonConvert.DeserializeAnonymousType(postRequest.downloadHandler.text, new { matchId = 0 });
            return (postRequest.responseCode, result?.matchId);
        }
        return (postRequest.responseCode, null);
    }

    #endregion
}

public class CreateAccountResponse {
    public long StatusCode { get; }
    public string Message { get; }

    public CreateAccountResponse(long statusCode, string message) {
        StatusCode = statusCode;
        Message = message;
    }
}

public enum RequestType {
    GET,
    POST,
    PUT,
    HEAD,
    DELETE
}
