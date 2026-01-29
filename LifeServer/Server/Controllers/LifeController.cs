using System.Data.SQLite;
using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Server.CardProperties;

namespace Server.Controllers;

[LifeExceptionFilter]
[ApiController]
[Route("[controller]")]
public class LifeController : ControllerBase {
    private static Matches matches = new();
    private static GameMatch? currentLocalMatch;
    private static AccountData? matchableAccount;
    private static Object QueueLockObj = new();
    private static Object MatchLockObj = new();
    private static Object DraftLockObj = new();
    private static Dictionary<int, DraftState> activeDrafts = new();
    private static int nextDraftId = 1;
    private const int TotalCardAmount = 281;

    // Static cleanup method for background service
    public static void CleanupInactiveMatches(int timeoutSeconds) {
        lock (MatchLockObj) {
            // First, clean up any finished matches
            var finishedMatches = matches.GetFinishedMatches();
            foreach (var matchId in finishedMatches) {
                matches.EndMatch(matchId);
                Console.WriteLine($"Cleaned up finished match {matchId}");
            }

            // Then check for disconnected players in active matches
            var inactiveMatches = matches.GetInactiveMatches(timeoutSeconds);
            foreach (var (matchId, disconnectedPlayerId) in inactiveMatches) {
                var match = matches.GetMatchData(matchId);
                if (match == null) continue;

                // Determine winner (opponent of disconnected player)
                Player disconnectedPlayer = match.accountIdToPlayer[disconnectedPlayerId];
                Player winner = match.GetOpponent(disconnectedPlayer);

                Console.WriteLine($"Player {disconnectedPlayer.playerName} disconnected from match {matchId} after {timeoutSeconds}s - {winner.playerName} wins");

                // End the game with the disconnected player losing
                match.EndGame(winner, "opponent disconnected");

                // Clean up the match
                matches.EndMatch(matchId);
            }
        }
    }

    private bool IsAuthorized(string? encodedUserPass, out AccountData accountData) {
        using SQLiteConnection conn = SqlFunctions.CreateConnection();
        bool isAuth = SqlAuthenticate(conn, encodedUserPass, out accountData);
        return isAuth;
    }


    private List<Card> GetDeckCards(int deckId, GameMatch? match = null) {
        using SQLiteConnection conn = SqlFunctions.CreateConnection();
        List<int> ids = SqlGetDeckCards(conn, deckId);
        if (match != null) {
            return ids.Select(id => Card.GetCard(match.GetNextUid(), id, match)).ToList();
        }
        int uidCounter = 0;
        return ids.Select(id => Card.GetCard(uidCounter += 1, id)).ToList();
    }

    private void RequestToConsole(string accountName, string requestName) {
        Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " Request: " + requestName + " | User: " + accountName);
    }

    [HttpGet("accounts")]
    [Produces("application/json")]
    public IActionResult Login() {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "Login");
        return Ok(accountData);
    }

    private string[]? NameAndHashFromAuthHeader(string? authHeaderValue) {
        if (!authHeaderValue.Contains("Basic ")) return null;
        string encodedUsernamePassword = authHeaderValue.Substring("Basic ".Length).Trim();
        string userHash = Base64Decode(encodedUsernamePassword);
        if (userHash.Contains(":")) {
            return userHash.Split(":");
        }
        return null;
    }

    private string Base64Decode(string s) {
        return Encoding.UTF8.GetString(Convert.FromBase64String(s));
    }

    [HttpPost("accounts")]
    public IActionResult CreateAccount(AccountData accountData) {
        RequestToConsole(accountData.displayName, "CreateAccount");
        using SQLiteConnection conn = SqlFunctions.CreateConnection();
        int id = SqlCreateAccount(conn, accountData, out var conflictField);
        if (id > 0) {
            // if the account is successfully created return 201 Created with the account id as the string response:
            Console.WriteLine("Created account with username: " + accountData.username);
            return Created(new Uri(Request.GetEncodedUrl() + "/" + id), null);
        }
        Console.WriteLine("failed because of: " + conflictField);
        // otherwise return 409 Conflict with the conflicting field as a string response:
        return Conflict(conflictField);
    }
    
    
    // I forgot what this is used for :(
    [HttpGet("data/{id:int}")]
    public IActionResult GetData([FromRoute] int id) {
        var md = matches.GetMatchData(id);
        if (md == null) {
            return NotFound();
        }
        return Ok(md);
    }
    
    
    [HttpGet("accounts/decks")]
    [Produces("application/json")]
    public IActionResult GetDecks() { 
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "GetDecks");
        using SQLiteConnection conn = SqlFunctions.CreateConnection();
        List<DeckData>? deckDataList = SqlGetAccountDecks(conn, accountData.username);
        return Ok(deckDataList);
    }

    [HttpGet("accounts/cards")]
    [Produces("application/json")]
    public IActionResult GetCards() { 
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "GetCards");
        using SQLiteConnection conn = SqlFunctions.CreateConnection();
        List<int>? accountCards = SqlGetAccountCards(conn, accountData.username);
        return Ok(accountCards);
    }
    
    [HttpGet("cards/{id:int}")]
    [Produces("application/json")]
    public IActionResult GetCard([FromRoute] int id) {
        Card? singleCard = Card.GetCard(0, id);
        CardDisplayData newCardDisplayData = new CardDisplayData(singleCard);
        return Ok(newCardDisplayData);
    } 

    [HttpGet("cards")]
    [Produces("application/json")]  
    public IActionResult GetAllCards() { 
        // if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        List<CardDisplayData> cardDataList = new();
        for (int i = 0; i < TotalCardAmount; i++) {
            Card newCard = Card.GetCardSimple(i);
            CardDisplayData? newCardDisplayData = new CardDisplayData(newCard);
            Debug.Assert(newCardDisplayData != null);
            cardDataList.Add(newCardDisplayData);
        }
        return Ok(cardDataList);
    }

    [HttpPost("accounts/decks")]
    public IActionResult CreateOrUpdateDeck(DeckData deckData) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "CreateOrUpdateDeck");
        using SQLiteConnection conn = SqlFunctions.CreateConnection();
        int deckId = SqlCreateOrUpdateDeck(conn, deckData, accountData.id);
        if (deckData.id <= 0) {
            Console.WriteLine("Deck created with Id: " + deckId);
            return Created(new Uri(Request.GetEncodedUrl() + "/" + deckId), null);
        }
        //existing deck was updated
        Console.WriteLine("Deck updated with Id: " + deckId);
        return Ok();
    }

    [HttpDelete("accounts/decks/{deckId:int}")]
    public IActionResult DeleteDeck([FromRoute] int deckId) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "DeleteDeck");
        using SQLiteConnection conn = SqlFunctions.CreateConnection();
        SqlDeleteDeck(conn, deckId);
        Console.WriteLine("Deck with id " + deckId + " has been deleted");
        return Ok();
    }

    // ===== FRIENDS SYSTEM ENDPOINTS =====

    [HttpGet("friends")]
    [Produces("application/json")]
    public IActionResult GetFriends() {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "GetFriends");
        using SQLiteConnection conn = SqlFunctions.CreateConnection();
        List<FriendData> friends = SqlGetFriends(conn, accountData.id);
        return Ok(friends);
    }

    [HttpPost("friends/request/{username}")]
    public IActionResult SendFriendRequest([FromRoute] string username) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "SendFriendRequest");
        using SQLiteConnection conn = SqlFunctions.CreateConnection();

        // Check target user exists
        var targetAccount = SqlGetAccount(conn, username);
        if (targetAccount == null) return NotFound("user_not_found");

        // Check not self
        if (targetAccount.id == accountData.id) return BadRequest("cannot_request_self");

        // Check not already friends
        if (SqlAreFriends(conn, accountData.id, targetAccount.id))
            return Conflict("already_friends");

        // Check not blocked by target
        if (SqlIsBlocked(conn, targetAccount.id, accountData.id))
            return Conflict("blocked");

        // Check no pending request from us exists
        if (SqlFriendRequestExists(conn, accountData.id, targetAccount.id))
            return Conflict("request_exists");

        // Check if target already sent us a request (auto-accept)
        if (SqlFriendRequestExists(conn, targetAccount.id, accountData.id)) {
            SqlDeleteFriendRequestsBetween(conn, accountData.id, targetAccount.id);
            SqlCreateFriendship(conn, accountData.id, targetAccount.id);
            return Ok("accepted");
        }

        SqlCreateFriendRequest(conn, accountData.id, targetAccount.id);
        return Created(new Uri(Request.GetEncodedUrl()), null);
    }

    [HttpGet("friends/requests")]
    [Produces("application/json")]
    public IActionResult GetFriendRequests() {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "GetFriendRequests");
        using SQLiteConnection conn = SqlFunctions.CreateConnection();
        List<FriendRequestData> requests = SqlGetFriendRequests(conn, accountData.id);
        return Ok(requests);
    }

    [HttpGet("friends/requests/outbound")]
    [Produces("application/json")]
    public IActionResult GetOutboundFriendRequests() {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "GetOutboundFriendRequests");
        using SQLiteConnection conn = SqlFunctions.CreateConnection();
        List<FriendRequestData> requests = SqlGetOutboundFriendRequests(conn, accountData.id);
        return Ok(requests);
    }

    [HttpPost("friends/requests/{requestId}/accept")]
    public IActionResult AcceptFriendRequest([FromRoute] int requestId) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "AcceptFriendRequest");
        using SQLiteConnection conn = SqlFunctions.CreateConnection();

        var request = SqlGetFriendRequest(conn, requestId);
        if (request == null || request.Value.receiverId != accountData.id)
            return NotFound();

        SqlAcceptFriendRequestById(conn, requestId);
        return Ok();
    }

    [HttpDelete("friends/requests/{requestId}")]
    public IActionResult DeclineFriendRequest([FromRoute] int requestId) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "DeclineFriendRequest");
        using SQLiteConnection conn = SqlFunctions.CreateConnection();

        var request = SqlGetFriendRequest(conn, requestId);
        // Allow sender or receiver to delete
        if (request == null ||
            (request.Value.receiverId != accountData.id && request.Value.senderId != accountData.id))
            return NotFound();

        SqlDeleteFriendRequest(conn, requestId);
        return Ok();
    }

    [HttpDelete("friends/{friendId}")]
    public IActionResult RemoveFriend([FromRoute] int friendId) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "RemoveFriend");
        using SQLiteConnection conn = SqlFunctions.CreateConnection();

        if (!SqlAreFriends(conn, accountData.id, friendId))
            return NotFound();

        SqlRemoveFriend(conn, accountData.id, friendId);
        return Ok();
    }

    [HttpPost("blocks/{userId}")]
    public IActionResult BlockPlayer([FromRoute] int userId) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "BlockPlayer");
        using SQLiteConnection conn = SqlFunctions.CreateConnection();

        if (userId == accountData.id) return BadRequest("cannot_block_self");

        // Remove any existing friendship
        SqlRemoveFriend(conn, accountData.id, userId);

        // Remove any pending requests in either direction
        SqlDeleteFriendRequestsBetween(conn, accountData.id, userId);

        SqlBlockPlayer(conn, accountData.id, userId);
        return Ok();
    }

    [HttpDelete("blocks/{userId}")]
    public IActionResult UnblockPlayer([FromRoute] int userId) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "UnblockPlayer");
        using SQLiteConnection conn = SqlFunctions.CreateConnection();
        SqlUnblockPlayer(conn, accountData.id, userId);
        return Ok();
    }

    [HttpGet("blocks")]
    [Produces("application/json")]
    public IActionResult GetBlockedPlayers() {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "GetBlockedPlayers");
        using SQLiteConnection conn = SqlFunctions.CreateConnection();
        List<BlockedPlayerData> blocked = SqlGetBlockedPlayers(conn, accountData.id);
        return Ok(blocked);
    }

    [HttpGet("opponents/recent")]
    [Produces("application/json")]
    public IActionResult GetRecentOpponents() {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "GetRecentOpponents");
        using SQLiteConnection conn = SqlFunctions.CreateConnection();
        List<RecentOpponentData> opponents = SqlGetRecentOpponents(conn, accountData.id, 15);
        return Ok(opponents);
    }

    [HttpPost("heartbeat")]
    public IActionResult Heartbeat() {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        // Don't log heartbeats to reduce console noise
        using SQLiteConnection conn = SqlFunctions.CreateConnection();
        SqlUpdateLastActive(conn, accountData.id);
        return Ok();
    }

    [HttpGet("queue/{deckId}")]
    [Produces("application/json")]
    public IActionResult CreateNewMatch(int deckId) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "Queue/CreateNewMatch");

        // lock to prevent multiple players from matching with the same player
        lock (QueueLockObj) {
            // if no one has claimed queue spot 1 put yourself there
            if (matchableAccount == null) {
                matchableAccount = accountData;
                Console.WriteLine($"Player {accountData.displayName} entered queue");
                return Ok(null);
            }
            // if you're already in queue spot 1
            if (matchableAccount.id == accountData.id) {
                // check to see if someone has matched with you yet
                if (currentLocalMatch == null) return Ok(null);
                matchableAccount = null;
                currentLocalMatch.playerOne.deck = GetDeckCards(deckId, currentLocalMatch);
                currentLocalMatch.InitializeMatch();
                MatchState myMatchState = new MatchState(currentLocalMatch, true);
                Console.WriteLine($"Player {accountData.displayName} got match from queue, clearing currentLocalMatch");
                currentLocalMatch = null;
                return Ok(myMatchState);
            }
            // 3rd player retries until queue is open again
            if (currentLocalMatch != null) {
                Console.WriteLine($"Player {accountData.displayName} waiting - queue busy with pending match");
                return Ok(null);
            }
            // someone else is in spot 1, create a new match
            currentLocalMatch = new GameMatch(matches.NextMatchId(),
                new Player(matchableAccount.displayName, matchableAccount.id),
                new Player(accountData.displayName, accountData.id));
            matches.SetMatchData(currentLocalMatch);
            currentLocalMatch.playerTwo.deck = GetDeckCards(deckId, currentLocalMatch);
            Console.WriteLine("New match with ID " + (currentLocalMatch.matchId + " has been created with users " +
                                                      matchableAccount.displayName + " and " +
                                                      accountData.displayName));
            MatchState newMatchState = new MatchState(currentLocalMatch, false);
            return Ok(newMatchState);
        }
    }

    [HttpGet("match/{matchId}/game-ready")]
    public IActionResult GameReadyCheck(int matchId) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "GameReadyCheck");
        GameMatch myGameMatch = matches.ValidatePlayerMatch(accountData.id, matchId);
        // TODO: check for rogue player request
        Player player = myGameMatch.accountIdToPlayer[accountData.id];
        if (myGameMatch.GetOpponent(player).deck == null) {
            return Ok(null);
        }
        MatchState myMatchState = new MatchState(myGameMatch, myGameMatch.IsPlayerOne(accountData.id));
        return Ok(myMatchState);
    }
    
    [HttpDelete("queue")]
    public IActionResult ExitQueue() {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "ExitQueue");
        lock (QueueLockObj) {
            // Use ID comparison instead of reference equality
            if (matchableAccount != null && matchableAccount.id == accountData.id) {
                Console.WriteLine($"Player {accountData.displayName} left queue");
                matchableAccount = null;
            }
        }
        return Ok();
    }

    [HttpPost("test-match/{deckId}")]
    public IActionResult CreateTestMatch(int deckId) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "CreateTestMatch");
        lock (MatchLockObj) {
            // Create bot player with ID -999 (avoiding -1 which means "no priority" in GameMatch)
            Player botPlayer = new Player("Test Bot", -999, isBot: true);

            // Create match with real player and bot
            GameMatch testMatch = new GameMatch(matches.NextMatchId(),
                new Player(accountData.displayName, accountData.id),
                botPlayer);

            matches.SetMatchData(testMatch);

            // Load decks - player gets their deck, bot gets all Gamble cards (ID 175)
            testMatch.playerOne.deck = GetDeckCards(deckId, testMatch);
            testMatch.playerTwo.deck = Enumerable.Range(0, 40)
                .Select(_ => Card.GetCard(testMatch.GetNextUid(), 175, testMatch)).ToList();

            // Initialize the match
            testMatch.InitializeMatch();

            Console.WriteLine($"Test match {testMatch.matchId} created for {accountData.displayName} vs Bot");

            MatchState matchState = new MatchState(testMatch, true);
            return Ok(matchState);
        }
    }

    // In-game requests

    [HttpGet("match/{matchId}")]
    public IActionResult GetMatchState(int matchId) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "GetMatchState");
        lock (MatchLockObj) {
            GameMatch myGameMatch = matches.ValidatePlayerMatch(accountData.id, matchId);
            MatchState myMatchState = new MatchState(myGameMatch, myGameMatch.IsPlayerOne(accountData.id));

            // Detailed phase debugging
            var player = myGameMatch.accountIdToPlayer[accountData.id];
            Console.WriteLine($"[GetMatchState] Player={accountData.displayName}, serverPhase={myGameMatch.currentPhase}, isAutoSkipping={myGameMatch.isAutoSkipping}");
            Console.WriteLine($"[GetMatchState] Events being sent ({myMatchState.playerState.eventList.Count}):");
            foreach (var evt in myMatchState.playerState.eventList) {
                if (evt.eventType == EventType.NextPhase || evt.eventType == EventType.SkipToPhase || evt.eventType == EventType.GoToPhase) {
                    Console.WriteLine($"  - {evt.eventType} (amount={evt.amount}, universalInt={evt.universalInt})");
                }
            }

            myGameMatch.ClearEventList(player);
            return Ok(myMatchState);
        }
    }

    [HttpPost("match/{matchId}/choose/{choiceIndex}")]
    public IActionResult SendChoice(int matchId, int choiceIndex) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "SendChoice");
        lock (MatchLockObj) {
            GameMatch myGameMatch = matches.ValidatePlayerMatch(accountData.id, matchId);
            myGameMatch.MakeChoice(myGameMatch.accountIdToPlayer[accountData.id], choiceIndex);
            Console.WriteLine("event list count is: " + myGameMatch.accountIdToPlayer[accountData.id].eventList.Count);
            return Ok();
        }
    }
    
    [HttpPost("match/{matchId}/ordering")]
    public IActionResult SendFinalOrdering(int matchId, List<int> finalOrderList) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "SendFinalOrdering");
        lock (MatchLockObj) {
            GameMatch myGameMatch = matches.ValidatePlayerMatch(accountData.id, matchId);
            myGameMatch.AddOrderedTriggersToStack(accountData.id, finalOrderList);
            Console.WriteLine("event list count is: " + myGameMatch.accountIdToPlayer[accountData.id].eventList.Count);
            return Ok();
        }
    }

    [HttpPost("match/{matchId}/card-select")]
    public IActionResult SendCardSelection(int matchId, List<List<int>> cardUids) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "SendCardSelection");
        lock (MatchLockObj) {
            GameMatch myGameMatch = matches.ValidatePlayerMatch(accountData.id, matchId);
            Player player = myGameMatch.accountIdToPlayer[accountData.id];
            myGameMatch.SendCardsToDestinations(cardUids, player);
            Console.WriteLine("event list count is: " + player.eventList.Count);
            return Ok();
        }
    }
    
    [HttpGet("match/{matchId}/pass")]
    public IActionResult PassPrio(int matchId, [FromQuery] int? passToPhase = null) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        lock (MatchLockObj) {
            GameMatch myGameMatch = matches.ValidatePlayerMatch(accountData.id, matchId);
            Player player = myGameMatch.accountIdToPlayer[accountData.id];
            // passToPhase == 6 is special case for "pass to my main"
            if (passToPhase == 6) {
                player.passToPhase = Phase.Main;
                player.passToMyMain = true;
                // Only clear autopass pause if stack is empty - if stack has items, keep paused
                if (myGameMatch.stack.Count == 0) {
                    player.autopassPausedForStack = false;
                }
            } else {
                player.passToPhase = passToPhase.HasValue ? (Phase)passToPhase.Value : null;
                player.passToMyMain = false;
                // Only clear autopass pause if player clicked a passToPhase button AND stack is empty
                if (passToPhase.HasValue && myGameMatch.stack.Count == 0) {
                    player.autopassPausedForStack = false;
                }
            }
            myGameMatch.PassPrio();
            RequestToConsole(accountData.displayName, "PassPrio" + (passToPhase.HasValue ? (passToPhase == 6 ? " (passTo: MyMain)" : $" (passTo: {(Phase)passToPhase.Value})") : ""));
            return Ok();
        }
    }

    [HttpGet("match/{matchId}/cast-attempt/{cardUid}")]
    public IActionResult AttemptToCast(int matchId, int cardUid) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        lock (MatchLockObj) {
            GameMatch myGameMatch = matches.ValidatePlayerMatch(accountData.id, matchId);
            myGameMatch.AttemptToCast(myGameMatch.accountIdToPlayer[accountData.id], myGameMatch.cardByUid[cardUid]);
            RequestToConsole(accountData.displayName, "AttemptToCast | eventCount: " + myGameMatch.accountIdToPlayer[accountData.id].eventList.Count);
            return Ok();
        }
    }
    
    [HttpGet("match/{matchId}/activation-attempt/{cardUid}")]
    public IActionResult AttemptToActivate(int matchId, int cardUid) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        lock (MatchLockObj) {
            GameMatch myGameMatch = matches.ValidatePlayerMatch(accountData.id, matchId);
            Player player = myGameMatch.accountIdToPlayer[accountData.id];
            Card cardWithAbility = myGameMatch.cardByUid[cardUid];
            List<ActivatedEffect> allActivatedEffects = cardWithAbility.GetActivatedEffects();
            Debug.Assert(allActivatedEffects.Count > 0, "this card has no activatable abilities");
            // Filter to abilities whose conditions are met AND cost is payable from current zone
            ActivatedEffect? aEffect = allActivatedEffects.FirstOrDefault(ae => {
                // Check conditions are met
                bool conditionsMet = ae.conditions == null ||
                    ae.conditions.All(c => c.Verify(myGameMatch, player, null, cardWithAbility));
                if (!conditionsMet) return false;
                // For self-sacrifice, card must be in play
                if (ae.scope == Scope.SelfOnly && ae.costType == CostType.Sacrifice) {
                    return cardWithAbility.currentZone == Zone.Play;
                }
                // For self-exile, card must be in the zone specified by conditions (or in play/graveyard)
                if (ae.scope == Scope.SelfOnly && ae.costType == CostType.Exile) {
                    return cardWithAbility.currentZone == Zone.Graveyard || cardWithAbility.currentZone == Zone.Play;
                }
                return true;
            });
            Debug.Assert(aEffect != null, "no activatable abilities have their conditions met");
            myGameMatch.AttemptToActivate(player, aEffect);
            RequestToConsole(accountData.displayName, "AttemptToActivate | eventCount: " + player.eventList.Count);
            return Ok();
        }
    }
    
    [HttpPost("match/{matchId}/tribute-select")]
    public IActionResult SendTributeSelection(int matchId, List<int> tributeUids) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        lock (MatchLockObj) {
            GameMatch myGameMatch = matches.ValidatePlayerMatch(accountData.id, matchId);
            myGameMatch.Tribute(accountData.id, tributeUids);
            RequestToConsole(accountData.displayName, "SendTributeSelection | eventCount: " + myGameMatch.accountIdToPlayer[accountData.id].eventList.Count);
            return Ok();
        }
    }

    [HttpPost("match/{matchId}/target-select")]
    public IActionResult SendTargetSelection(int matchId, List<int> targetedUids) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        lock (MatchLockObj) {
            GameMatch myGameMatch = matches.ValidatePlayerMatch(accountData.id, matchId);
            Player player = myGameMatch.accountIdToPlayer[accountData.id];
            myGameMatch.AssignTargets(player, targetedUids);
            RequestToConsole(accountData.displayName, "SendTargetSelection | eventCount: " + myGameMatch.accountIdToPlayer[accountData.id].eventList.Count);
            return Ok();
        }
    }
    
    [HttpPost("match/{matchId}/cost-select")]
    public IActionResult SendCostSelection(int matchId, List<int> targetedUids) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        lock (MatchLockObj) {
            GameMatch myGameMatch = matches.ValidatePlayerMatch(accountData.id, matchId);
            Player player = myGameMatch.accountIdToPlayer[accountData.id];
            List<Card> selectedCards = targetedUids.Select(uid => myGameMatch.cardByUid[uid]).ToList();
            myGameMatch.HandleCostSelection(player, selectedCards);
            RequestToConsole(accountData.displayName, "SendCostSelection | eventCount: " + myGameMatch.accountIdToPlayer[accountData.id].eventList.Count);
            return Ok();
        }
    }
    
    [HttpPost("match/{matchId}/set-x/{amount}")]
    public IActionResult SetX(int matchId, int amount) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        lock (MatchLockObj) {
            GameMatch myGameMatch = matches.ValidatePlayerMatch(accountData.id, matchId);
            Player player = myGameMatch.accountIdToPlayer[accountData.id];
            myGameMatch.SetX(player, amount);
            RequestToConsole(accountData.displayName, "SetX | eventCount: " + myGameMatch.accountIdToPlayer[accountData.id].eventList.Count);
            return Ok();
        }
    }

    [HttpPost("match/{matchId}/cancel-cast")]
    public IActionResult CancelCast(int matchId) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        lock (MatchLockObj) {
            GameMatch myGameMatch = matches.ValidatePlayerMatch(accountData.id, matchId);
            Player player = myGameMatch.accountIdToPlayer[accountData.id];
            myGameMatch.CancelCast(player);
            RequestToConsole(accountData.displayName, "CancelCast | eventCount: " + myGameMatch.accountIdToPlayer[accountData.id].eventList.Count);
            return Ok();
        }
    }

    [HttpPost("match/{matchId}/set-amount/{amount}")]
    public IActionResult SetAmount(int matchId, int amount) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        lock (MatchLockObj) {
            GameMatch myGameMatch = matches.ValidatePlayerMatch(accountData.id, matchId);
            Player player = myGameMatch.accountIdToPlayer[accountData.id];
            myGameMatch.SetAmount(player, amount);
            RequestToConsole(accountData.displayName, "SetAmount | eventCount: " + myGameMatch.accountIdToPlayer[accountData.id].eventList.Count);
            return Ok();
        }
    }

    [HttpPost("match/{matchId}/attack")]
    public IActionResult Attack(int matchId) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        lock (MatchLockObj) {
            GameMatch myGameMatch = matches.ValidatePlayerMatch(accountData.id, matchId);
            Player attackingPlayer = myGameMatch.accountIdToPlayer[accountData.id];
            myGameMatch.SubmitAttack(attackingPlayer);
            RequestToConsole(accountData.displayName, "Attack | eventCount: " + myGameMatch.accountIdToPlayer[accountData.id].eventList.Count);
            return Ok();
        }
    }

    [HttpGet("match/{matchId}/attackables/{attackingUid}")]
    public IActionResult GetAttackables(int matchId, int attackingUid) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        lock (MatchLockObj) {
            GameMatch myGameMatch = matches.ValidatePlayerMatch(accountData.id, matchId);
            Player requestingPlayer = myGameMatch.accountIdToPlayer[accountData.id];

            // For Ground Tactics: the controller requests attackables for the turn player's cards
            Player attackingPlayer;
            if (myGameMatch.groundTacticsControllerId == requestingPlayer.playerId) {
                attackingPlayer = myGameMatch.GetPlayerById(myGameMatch.turnPlayerId);
            } else {
                attackingPlayer = requestingPlayer;
            }

            List<int> attackables = myGameMatch.GetAttackables(attackingPlayer, myGameMatch.cardByUid[attackingUid]);
            RequestToConsole(accountData.displayName, "SelectAttacker | eventCount: " + myGameMatch.accountIdToPlayer[accountData.id].eventList.Count);
            return Ok(attackables);
        }
    }
    
    [HttpPost("match/{matchId}/attack/assign-attack")]
    public IActionResult AssignAttack(int matchId, (int, int) attackUids) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        lock (MatchLockObj) {
            GameMatch myGameMatch = matches.ValidatePlayerMatch(accountData.id, matchId);
            Player player = myGameMatch.accountIdToPlayer[accountData.id];
            myGameMatch.AssignAttack(player, attackUids);
            RequestToConsole(accountData.displayName, "AssignAttack | eventCount: " + myGameMatch.accountIdToPlayer[accountData.id].eventList.Count);
            return Ok();
        }
    }

    [HttpDelete("match/{matchId}/attack/unassign-attack/{attackerUid}")]
    public IActionResult UnAssignAttack(int matchId, int attackerUid) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        lock (MatchLockObj) {
            GameMatch myGameMatch = matches.ValidatePlayerMatch(accountData.id, matchId);
            Player player = myGameMatch.accountIdToPlayer[accountData.id];
            myGameMatch.UnAssignAttack(player, attackerUid);
            RequestToConsole(accountData.displayName, "UnAssignAttack | eventCount: " + myGameMatch.accountIdToPlayer[accountData.id].eventList.Count);
            return Ok();
        }
    }

    [HttpPost("match/{matchId}/add-secondary-attacker")]
    public IActionResult AddSecondaryAttack(int matchId, (int, int) attackUids) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        lock (MatchLockObj) {
            GameMatch myGameMatch = matches.ValidatePlayerMatch(accountData.id, matchId);
            Player attackingPlayer = myGameMatch.accountIdToPlayer[accountData.id];
            myGameMatch.AddSecondaryAttacker(attackingPlayer, attackUids);
            RequestToConsole(accountData.displayName, "AddSecondaryAttack | eventCount: " + myGameMatch.accountIdToPlayer[accountData.id].eventList.Count);
            return Ok();
        }
    }

    // Leave/concede a match - the leaving player loses
    [HttpPost("match/{matchId}/leave")]
    public IActionResult LeaveMatch(int matchId) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "LeaveMatch");
        lock (MatchLockObj) {
            var match = matches.GetMatchData(matchId);
            if (match == null) {
                // Match already ended
                return Ok(new { message = "Match already ended" });
            }

            // Check if player is in this match
            if (match.playerOne.playerId != accountData.id && match.playerTwo.playerId != accountData.id) {
                return BadRequest("Not your match");
            }

            // If game not already over, the leaving player loses
            if (!match.isGameOver) {
                Player leavingPlayer = match.accountIdToPlayer[accountData.id];
                Player winner = match.GetOpponent(leavingPlayer);
                Console.WriteLine($"Player {leavingPlayer.playerName} left match {matchId} - {winner.playerName} wins by forfeit");
                match.EndGame(winner, "opponent left");
            }

            // Clean up the match
            matches.EndMatch(matchId);
            Console.WriteLine($"Match {matchId} cleaned up after player left");

            return Ok(new { message = "Left match successfully" });
        }
    }

    // Endpoint to check if player is currently in a match (for reconnection)
    [HttpGet("match/current")]
    public IActionResult GetCurrentMatch() {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "GetCurrentMatch");
        lock (MatchLockObj) {
            int? matchId = matches.GetPlayerMatchId(accountData.id);
            if (matchId == null) {
                return Ok(new { inMatch = false });
            }

            var match = matches.GetMatchData(matchId.Value);
            if (match == null) {
                return Ok(new { inMatch = false });
            }

            return Ok(new {
                inMatch = true,
                matchId = matchId.Value,
                isGameOver = match.isGameOver
            });
        }
    }

    //SQL TEST
    private bool SqlAuthenticate(SQLiteConnection conn, string? encodedUserPass, out AccountData accountData) {
        accountData = new AccountData();
        if (encodedUserPass == null) {
            return false;
        }
        var userAndHash = NameAndHashFromAuthHeader(encodedUserPass);
        if (userAndHash == null) {
            return false;
        }
        accountData = SqlGetAccount(conn, userAndHash[0]);
        return accountData != null && userAndHash[1].Equals(accountData.hashedPassword);
    }

    private int SqlCreateAccount(SQLiteConnection conn, AccountData accountData, out string conflictField) {
        var sqLiteCommand = conn.CreateCommand();
        conflictField = "";

        // first check for duplicate username
        sqLiteCommand.CommandText = $"SELECT id FROM accounts WHERE username = '{accountData.username}';";
        var sqLiteDataReader = sqLiteCommand.ExecuteReader();
        var hasRows = sqLiteDataReader.HasRows;
        if (hasRows) {
            conflictField = "username";
            return -1;
        }
        sqLiteDataReader.Close();
        
        // then check for duplicate email
        sqLiteCommand.CommandText = $"SELECT id FROM accounts WHERE email_address = '{accountData.email}';";
        sqLiteDataReader = sqLiteCommand.ExecuteReader();
        hasRows = sqLiteDataReader.HasRows;
        if (hasRows) {
            conflictField = "email";
            return -1;
        }
        sqLiteDataReader.Close();

        // all good - create account
        sqLiteCommand.CommandText =
            "INSERT INTO accounts (display_name, username, email_address, pwdhash) " +
            $"VALUES('{accountData.displayName}', '{accountData.username}', '{accountData.email}', '{accountData.hashedPassword}');";
        sqLiteCommand.ExecuteNonQuery();
        sqLiteDataReader.Close();

        // read account we just created to obtain id:
        sqLiteCommand.CommandText = $"SELECT id FROM accounts WHERE username = '{accountData.username}';";
        sqLiteDataReader = sqLiteCommand.ExecuteReader();
        if (!sqLiteDataReader.Read()) {
            conflictField = "database error";
            return -1;
        }

        int id = sqLiteDataReader.GetInt32(0);
        sqLiteDataReader.Close();
        return id;
    }


    private int SqlCreateOrUpdateDeck(SQLiteConnection conn, DeckData deckData, int accountId) {
        bool hasRows;
        using (var cmd = conn.CreateCommand()) {
            cmd.CommandText = $"SELECT * FROM decks WHERE id = {deckData.id}";
            using (var sqLiteDataReader = cmd.ExecuteReader()) {
                hasRows = sqLiteDataReader.HasRows;
            }
        }
        if (hasRows) {
            using var sqLiteTransaction1 = conn.BeginTransaction();
            // Update the name
            using (var cmd = conn.CreateCommand()) {
                cmd.CommandText = $"UPDATE decks SET deck_name = '{deckData.deckName}' WHERE id = {deckData.id}";
                cmd.ExecuteNonQuery();
            }

            // Delete existing deck list
            using (var cmd = conn.CreateCommand()) {
                cmd.CommandText = $"DELETE FROM deck_cards WHERE deck_id = {deckData.id}";
                cmd.ExecuteNonQuery();
            }

            // Insert updated deck list, one at a time
            using (var cmd = conn.CreateCommand()) {
                cmd.CommandText = "INSERT INTO deck_cards (deck_id, card_id) VALUES ";
                for (int i = 0; i < deckData.deckList.Count; i++) {
                    cmd.CommandText += $"({deckData.id}, {deckData.deckList[i]})";
                    cmd.CommandText += i < deckData.deckList.Count - 1 ? "," : ";";
                }
                cmd.ExecuteNonQuery();
            }
            sqLiteTransaction1.Commit();
            return deckData.id;
        }
        
        using var sqLiteTransaction2 = conn.BeginTransaction();
        
        //Add deck to decks table(for name querying)
        using (var cmd = conn.CreateCommand()) {
            cmd.CommandText = "INSERT INTO decks (deck_name)" +
                              $"VALUES('{deckData.deckName}')";
            cmd.ExecuteNonQuery();
        }

        var deckId = conn.LastInsertRowId;

        //Add deck to account
        using (var cmd = conn.CreateCommand()) {
            cmd.CommandText = "INSERT INTO account_decks (account_id, deck_id)" +
                             $"VALUES({accountId}, {deckId})";
            cmd.ExecuteNonQuery();
        }
        
        //Add cards to deck
        using (var cmd = conn.CreateCommand()) {
            cmd.CommandText = "INSERT INTO deck_cards (deck_id, card_id) VALUES ";
            for (int i = 0; i < deckData.deckList.Count; i++) {
                cmd.CommandText += $"({deckId}, {deckData.deckList[i]})";
                cmd.CommandText += i < deckData.deckList.Count - 1 ? "," : ";";
            }
            cmd.ExecuteNonQuery();
        }
        sqLiteTransaction2.Commit();
        
        return (int)deckId;
    }

    private List<string> ReadUsernames(SQLiteConnection conn) {
        List<string> nameList = new List<string>();
        var sqLiteCommand = conn.CreateCommand();
        sqLiteCommand.CommandText = "SELECT * FROM accounts";

        var sqLiteDataReader = sqLiteCommand.ExecuteReader();
        while (sqLiteDataReader.Read()) {
            string name = sqLiteDataReader.GetString(2);
            nameList.Add(name);
        }
        sqLiteDataReader.Close();
        return nameList;
    }
    
    
    private AccountData? SqlGetAccount(SQLiteConnection conn, string username) {
        AccountData? data = null;
        using SQLiteCommand sqlite_cmd = conn.CreateCommand();
        sqlite_cmd.CommandText = $"SELECT * FROM accounts WHERE username ='{username}';";

        using SQLiteDataReader sqlite_datareader = sqlite_cmd.ExecuteReader();
        var hasRows = sqlite_datareader.HasRows;
        if (hasRows) {
            sqlite_datareader.Read();
            data = new AccountData {
                id = sqlite_datareader.GetInt32(0),
                displayName = sqlite_datareader.GetString(1),
                username = sqlite_datareader.GetString(2),
                hashedPassword = sqlite_datareader.GetString(3),
                email = sqlite_datareader.GetString(4), 
            };
        }
        return data;
    }

    private List<int> SqlGetDeckCards(SQLiteConnection conn, int deckId) {
        using var sqLiteCommand = conn.CreateCommand();
        sqLiteCommand.CommandText = $"SELECT card_id FROM deck_cards WHERE deck_id = '{deckId}'";
        using var sqLiteDataReader = sqLiteCommand.ExecuteReader();
        List<int> cards = new List<int>();
        if (sqLiteDataReader.HasRows) {
            while (sqLiteDataReader.Read()) {
                cards.Add(sqLiteDataReader.GetInt32(0));
            }
        }
        return cards;
    }

    private List<int> SqlGetAccountCards(SQLiteConnection conn, string username) {
        using var sqLiteCommand = conn.CreateCommand();
        sqLiteCommand.CommandText = "SELECT account_cards.card_id FROM accounts, account_cards " +
                                   $"WHERE username = '{username}' AND account_cards.account_id = accounts.id";
        using var sqLiteDataReader = sqLiteCommand.ExecuteReader();
        List<int> cards = new List<int>();
        if (sqLiteDataReader.HasRows) {
            while (sqLiteDataReader.Read()) {
                cards.Add(sqLiteDataReader.GetInt32(0));
            }
        }
        return cards;
    }

    private List<DeckData>? SqlGetAccountDecks(SQLiteConnection conn, string username) {
        using var sqLiteCommand = conn.CreateCommand();
        sqLiteCommand.CommandText = "SELECT account_decks.deck_id, decks.deck_name, deck_cards.card_id " +
                                    "FROM accounts, decks, account_decks, deck_cards " +
                                    $"WHERE accounts.username = '{username}'" +
                                    "AND account_decks.deck_id = decks.id " +
                                    "AND deck_cards.deck_id = decks.id " +
                                    "AND accounts.id = account_decks.account_id;";
        using var sqLiteDataReader = sqLiteCommand.ExecuteReader();
        Dictionary<int, DeckData> deckInfo = new Dictionary<int, DeckData>();
        if (sqLiteDataReader.HasRows) {
            while (sqLiteDataReader.Read()) {
                int deckId = sqLiteDataReader.GetInt32(0);
                if (!deckInfo.ContainsKey(deckId)) {
                    deckInfo[deckId] = new DeckData(deckId, sqLiteDataReader.GetString(1));
                }

                deckInfo[deckId].AddCard(sqLiteDataReader.GetInt32(2));
            }
        }

        return deckInfo.Values.ToList();
    }

    private void SqlDeleteDeck(SQLiteConnection conn, int deckId) {
        using var sqLiteTransaction = conn.BeginTransaction();
        using (var cmd = conn.CreateCommand()) {
            cmd.CommandText = $"DELETE FROM account_decks WHERE deck_id = {deckId}";
            cmd.ExecuteNonQuery();
        }

        using (var cmd = conn.CreateCommand()) {
            cmd.CommandText = $"DELETE FROM decks WHERE id = {deckId}";
            cmd.ExecuteNonQuery();
        }

        using (var cmd = conn.CreateCommand()) {
            cmd.CommandText = $"DELETE FROM deck_cards WHERE deck_id = {deckId}";
            cmd.ExecuteNonQuery();
        }
        sqLiteTransaction.Commit();
    }

    private void SaveDraftDeckToCollection(int accountId, List<int> deckCards) {
        using SQLiteConnection conn = SqlFunctions.CreateConnection();
        using var transaction = conn.BeginTransaction();

        string deckName = "Draft Deck";
        using (var cmd = conn.CreateCommand()) {
            cmd.CommandText = $"INSERT INTO decks (deck_name) VALUES ('{deckName}')";
            cmd.ExecuteNonQuery();
        }

        long deckId = conn.LastInsertRowId;

        // Link deck to account
        using (var cmd = conn.CreateCommand()) {
            cmd.CommandText = $"INSERT INTO account_decks (account_id, deck_id) VALUES ({accountId}, {deckId})";
            cmd.ExecuteNonQuery();
        }

        // Add cards to deck
        if (deckCards.Count > 0) {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO deck_cards (deck_id, card_id) VALUES ";
            for (int i = 0; i < deckCards.Count; i++) {
                cmd.CommandText += $"({deckId}, {deckCards[i]})";
                cmd.CommandText += i < deckCards.Count - 1 ? "," : ";";
            }
            cmd.ExecuteNonQuery();
        }

        transaction.Commit();
        Console.WriteLine($"Draft deck saved to collection: '{deckName}' (id={deckId}, {deckCards.Count} cards) for account {accountId}");
    }

    // ===== FRIENDS SYSTEM SQL HELPERS =====
    private const int OnlineThresholdMinutes = 5;
    private const int LobbyOfflineThresholdSeconds = 30;

    private List<FriendData> SqlGetFriends(SQLiteConnection conn, int accountId) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT a.id, a.display_name, a.username, a.last_active
            FROM accounts a
            INNER JOIN friendships f ON
                (f.account_id_1 = {accountId} AND f.account_id_2 = a.id) OR
                (f.account_id_2 = {accountId} AND f.account_id_1 = a.id)
            ORDER BY a.display_name";

        List<FriendData> friends = new();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) {
            DateTime? lastActive = reader.IsDBNull(3) ? null : DateTime.Parse(reader.GetString(3));
            bool isOnline = lastActive.HasValue &&
                (DateTime.UtcNow - lastActive.Value).TotalMinutes < OnlineThresholdMinutes;
            friends.Add(new FriendData(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                isOnline,
                lastActive
            ));
        }
        return friends;
    }

    private bool SqlAreFriends(SQLiteConnection conn, int id1, int id2) {
        int lower = Math.Min(id1, id2);
        int higher = Math.Max(id1, id2);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT 1 FROM friendships WHERE account_id_1 = {lower} AND account_id_2 = {higher}";
        using var reader = cmd.ExecuteReader();
        return reader.HasRows;
    }

    private void SqlCreateFriendship(SQLiteConnection conn, int id1, int id2) {
        int lower = Math.Min(id1, id2);
        int higher = Math.Max(id1, id2);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"INSERT OR IGNORE INTO friendships (account_id_1, account_id_2) VALUES ({lower}, {higher})";
        cmd.ExecuteNonQuery();
    }

    private void SqlRemoveFriend(SQLiteConnection conn, int id1, int id2) {
        int lower = Math.Min(id1, id2);
        int higher = Math.Max(id1, id2);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM friendships WHERE account_id_1 = {lower} AND account_id_2 = {higher}";
        cmd.ExecuteNonQuery();
    }

    private void SqlCreateFriendRequest(SQLiteConnection conn, int senderId, int receiverId) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"INSERT INTO friend_requests (sender_id, receiver_id) VALUES ({senderId}, {receiverId})";
        cmd.ExecuteNonQuery();
    }

    private bool SqlFriendRequestExists(SQLiteConnection conn, int senderId, int receiverId) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT 1 FROM friend_requests WHERE sender_id = {senderId} AND receiver_id = {receiverId}";
        using var reader = cmd.ExecuteReader();
        return reader.HasRows;
    }

    private List<FriendRequestData> SqlGetFriendRequests(SQLiteConnection conn, int receiverId) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT fr.id, fr.sender_id, sender.display_name, sender.username,
                   fr.receiver_id, receiver.display_name, receiver.username, fr.created_at
            FROM friend_requests fr
            INNER JOIN accounts sender ON fr.sender_id = sender.id
            INNER JOIN accounts receiver ON fr.receiver_id = receiver.id
            WHERE fr.receiver_id = {receiverId}
            ORDER BY fr.created_at DESC";

        List<FriendRequestData> requests = new();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) {
            requests.Add(new FriendRequestData(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4),
                reader.GetString(5),
                reader.GetString(6),
                DateTime.Parse(reader.GetString(7))
            ));
        }
        return requests;
    }

    private List<FriendRequestData> SqlGetOutboundFriendRequests(SQLiteConnection conn, int senderId) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT fr.id, fr.sender_id, sender.display_name, sender.username,
                   fr.receiver_id, receiver.display_name, receiver.username, fr.created_at
            FROM friend_requests fr
            INNER JOIN accounts sender ON fr.sender_id = sender.id
            INNER JOIN accounts receiver ON fr.receiver_id = receiver.id
            WHERE fr.sender_id = {senderId}
            ORDER BY fr.created_at DESC";

        List<FriendRequestData> requests = new();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) {
            requests.Add(new FriendRequestData(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4),
                reader.GetString(5),
                reader.GetString(6),
                DateTime.Parse(reader.GetString(7))
            ));
        }
        return requests;
    }

    private (int senderId, int receiverId)? SqlGetFriendRequest(SQLiteConnection conn, int requestId) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT sender_id, receiver_id FROM friend_requests WHERE id = {requestId}";
        using var reader = cmd.ExecuteReader();
        if (reader.Read()) {
            return (reader.GetInt32(0), reader.GetInt32(1));
        }
        return null;
    }

    private void SqlAcceptFriendRequestById(SQLiteConnection conn, int requestId) {
        using var transaction = conn.BeginTransaction();

        // Get the request details
        int senderId, receiverId;
        using (var cmd = conn.CreateCommand()) {
            cmd.CommandText = $"SELECT sender_id, receiver_id FROM friend_requests WHERE id = {requestId}";
            using var reader = cmd.ExecuteReader();
            reader.Read();
            senderId = reader.GetInt32(0);
            receiverId = reader.GetInt32(1);
        }

        // Create friendship (canonical ordering)
        int lower = Math.Min(senderId, receiverId);
        int higher = Math.Max(senderId, receiverId);
        using (var cmd = conn.CreateCommand()) {
            cmd.CommandText = $"INSERT OR IGNORE INTO friendships (account_id_1, account_id_2) VALUES ({lower}, {higher})";
            cmd.ExecuteNonQuery();
        }

        // Delete the request
        using (var cmd = conn.CreateCommand()) {
            cmd.CommandText = $"DELETE FROM friend_requests WHERE id = {requestId}";
            cmd.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private void SqlDeleteFriendRequest(SQLiteConnection conn, int requestId) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM friend_requests WHERE id = {requestId}";
        cmd.ExecuteNonQuery();
    }

    private void SqlDeleteFriendRequestsBetween(SQLiteConnection conn, int id1, int id2) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"DELETE FROM friend_requests
            WHERE (sender_id = {id1} AND receiver_id = {id2})
               OR (sender_id = {id2} AND receiver_id = {id1})";
        cmd.ExecuteNonQuery();
    }

    private bool SqlIsBlocked(SQLiteConnection conn, int blockerId, int blockedId) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT 1 FROM blocks WHERE blocker_id = {blockerId} AND blocked_id = {blockedId}";
        using var reader = cmd.ExecuteReader();
        return reader.HasRows;
    }

    private void SqlBlockPlayer(SQLiteConnection conn, int blockerId, int blockedId) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"INSERT OR IGNORE INTO blocks (blocker_id, blocked_id) VALUES ({blockerId}, {blockedId})";
        cmd.ExecuteNonQuery();
    }

    private void SqlUnblockPlayer(SQLiteConnection conn, int blockerId, int blockedId) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM blocks WHERE blocker_id = {blockerId} AND blocked_id = {blockedId}";
        cmd.ExecuteNonQuery();
    }

    private List<BlockedPlayerData> SqlGetBlockedPlayers(SQLiteConnection conn, int blockerId) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT a.id, a.display_name, a.username
            FROM accounts a
            INNER JOIN blocks b ON b.blocked_id = a.id
            WHERE b.blocker_id = {blockerId}
            ORDER BY a.display_name";

        List<BlockedPlayerData> blocked = new();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) {
            blocked.Add(new BlockedPlayerData(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2)
            ));
        }
        return blocked;
    }

    private void SqlUpdateLastActive(SQLiteConnection conn, int accountId) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE accounts SET last_active = datetime('now') WHERE id = {accountId}";
        cmd.ExecuteNonQuery();
    }

    private List<RecentOpponentData> SqlGetRecentOpponents(SQLiteConnection conn, int accountId, int limit) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT DISTINCT
                a.id, a.display_name, a.username, mh.completed_at,
                EXISTS(SELECT 1 FROM friendships f
                    WHERE (f.account_id_1 = {accountId} AND f.account_id_2 = a.id)
                       OR (f.account_id_2 = {accountId} AND f.account_id_1 = a.id)) as is_friend,
                EXISTS(SELECT 1 FROM blocks b
                    WHERE b.blocker_id = {accountId} AND b.blocked_id = a.id) as is_blocked
            FROM accounts a
            INNER JOIN match_history mh ON
                (mh.player_one_id = {accountId} AND mh.player_two_id = a.id) OR
                (mh.player_two_id = {accountId} AND mh.player_one_id = a.id)
            WHERE a.id != {accountId}
            ORDER BY mh.completed_at DESC
            LIMIT {limit}";

        List<RecentOpponentData> opponents = new();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) {
            opponents.Add(new RecentOpponentData(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                DateTime.Parse(reader.GetString(3)),
                reader.GetInt32(4) == 1,
                reader.GetInt32(5) == 1
            ));
        }
        return opponents;
    }

    private void SqlRecordMatch(SQLiteConnection conn, int player1Id, int player2Id, int? winnerId) {
        using var cmd = conn.CreateCommand();
        string winnerValue = winnerId.HasValue ? winnerId.Value.ToString() : "NULL";
        cmd.CommandText = $@"INSERT INTO match_history (player_one_id, player_two_id, winner_id)
                             VALUES ({player1Id}, {player2Id}, {winnerValue})";
        cmd.ExecuteNonQuery();
    }

    private AccountData? SqlGetAccountById(SQLiteConnection conn, int accountId) {
        AccountData? data = null;
        using SQLiteCommand cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT * FROM accounts WHERE id = {accountId};";

        using SQLiteDataReader reader = cmd.ExecuteReader();
        if (reader.HasRows) {
            reader.Read();
            data = new AccountData {
                id = reader.GetInt32(0),
                displayName = reader.GetString(1),
                username = reader.GetString(2),
                hashedPassword = reader.GetString(3),
                email = reader.GetString(4),
            };
        }
        return data;
    }

    // Ritual of Darkness choice (select summon uid or -1 to pass)
    [HttpPost("match/{matchId}/ritual-choice/{summonUid}")]
    public IActionResult SendRitualChoice(int matchId, int summonUid) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        lock (MatchLockObj) {
            GameMatch myGameMatch = matches.ValidatePlayerMatch(accountData.id, matchId);
            Player player = myGameMatch.accountIdToPlayer[accountData.id];
            myGameMatch.HandleRitualOfDarknessChoice(player, summonUid);
            RequestToConsole(accountData.displayName, "SendRitualChoice | eventCount: " + myGameMatch.accountIdToPlayer[accountData.id].eventList.Count);
            return Ok();
        }
    }

    // Repeat choice (accept or decline to pay cost and repeat effect)
    [HttpPost("match/{matchId}/repeat-choice/{accepted}")]
    public IActionResult SendRepeatChoice(int matchId, bool accepted) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        lock (MatchLockObj) {
            GameMatch myGameMatch = matches.ValidatePlayerMatch(accountData.id, matchId);
            Player player = myGameMatch.accountIdToPlayer[accountData.id];
            myGameMatch.HandleRepeatChoice(player, accepted);
            RequestToConsole(accountData.displayName, "SendRepeatChoice | eventCount: " + myGameMatch.accountIdToPlayer[accountData.id].eventList.Count);
            return Ok();
        }
    }

    // ==================== LOBBY ENDPOINTS ====================

    [HttpPost("lobbies")]
    [Produces("application/json")]
    public IActionResult CreateLobby([FromBody] CreateLobbyRequest request) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "CreateLobby");
        using SQLiteConnection conn = SqlFunctions.CreateConnection();

        int lobbyId = SqlCreateLobby(conn, accountData.id, request.lobbyType, request.maxPlayers);
        SqlAddPlayerToLobby(conn, lobbyId, accountData.id);

        LobbyData lobby = SqlGetLobby(conn, lobbyId, accountData.id);
        return Created($"/life/lobbies/{lobbyId}", lobby);
    }

    [HttpGet("lobbies/{lobbyId}")]
    [Produces("application/json")]
    public IActionResult GetLobby(int lobbyId) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "GetLobby");
        using SQLiteConnection conn = SqlFunctions.CreateConnection();

        // Cleanup offline players and their invites
        SqlCleanupOfflineLobbyPlayers(conn, lobbyId);
        SqlCleanupOfflineHostLobbies(conn);

        // Check if lobby was cancelled (host went offline)
        var basic = SqlGetLobbyBasic(conn, lobbyId);
        if (basic == null || basic.Value.status == "cancelled")
            return NotFound("Lobby no longer exists");

        if (!SqlIsPlayerInLobby(conn, lobbyId, accountData.id))
            return NotFound("Not in this lobby");

        LobbyData lobby = SqlGetLobby(conn, lobbyId, accountData.id);
        return Ok(lobby);
    }

    [HttpDelete("lobbies/{lobbyId}")]
    public IActionResult LeaveLobby(int lobbyId) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "LeaveLobby");
        using SQLiteConnection conn = SqlFunctions.CreateConnection();

        var lobby = SqlGetLobbyBasic(conn, lobbyId);
        if (lobby == null) return NotFound();

        if (lobby.Value.hostId == accountData.id) {
            // Host leaving cancels the lobby
            SqlCancelLobby(conn, lobbyId);
        } else {
            SqlRemovePlayerFromLobby(conn, lobbyId, accountData.id);
        }
        return Ok();
    }

    [HttpPost("lobbies/{lobbyId}/kick/{playerId}")]
    public IActionResult KickPlayer(int lobbyId, int playerId) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "KickPlayer");
        using SQLiteConnection conn = SqlFunctions.CreateConnection();

        var lobby = SqlGetLobbyBasic(conn, lobbyId);
        if (lobby == null) return NotFound();
        if (lobby.Value.hostId != accountData.id) return Forbid();

        SqlKickPlayerFromLobby(conn, lobbyId, playerId);
        return Ok();
    }

    [HttpPost("lobbies/{lobbyId}/invite/{friendId}")]
    public IActionResult SendLobbyInvite(int lobbyId, int friendId) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "SendLobbyInvite");
        using SQLiteConnection conn = SqlFunctions.CreateConnection();

        if (!SqlIsPlayerInLobby(conn, lobbyId, accountData.id))
            return NotFound("Not in this lobby");

        if (SqlIsBlocked(conn, accountData.id, friendId) || SqlIsBlocked(conn, friendId, accountData.id))
            return Conflict("blocked");

        if (SqlHasPendingInvite(conn, lobbyId, friendId))
            return Conflict("invite_exists");

        SqlCreateLobbyInvite(conn, lobbyId, accountData.id, friendId);
        return Created("", null);
    }

    [HttpGet("lobby-invites")]
    [Produces("application/json")]
    public IActionResult GetLobbyInvites() {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "GetLobbyInvites");
        using SQLiteConnection conn = SqlFunctions.CreateConnection();

        // Cleanup invites from offline hosts and cancelled lobbies
        SqlCleanupOfflineHostLobbies(conn);
        SqlCleanupAllOfflineInvites(conn);

        List<LobbyInviteData> invites = SqlGetLobbyInvites(conn, accountData.id);
        return Ok(invites);
    }

    [HttpPost("lobby-invites/{inviteId}/accept")]
    [Produces("application/json")]
    public IActionResult AcceptLobbyInvite(int inviteId) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "AcceptLobbyInvite");
        using SQLiteConnection conn = SqlFunctions.CreateConnection();

        var invite = SqlGetLobbyInvite(conn, inviteId);
        if (invite == null || invite.Value.inviteeId != accountData.id)
            return NotFound();

        var lobby = SqlGetLobbyBasic(conn, invite.Value.lobbyId);
        if (lobby == null || lobby.Value.status != "waiting")
            return Conflict("lobby_not_available");

        int playerCount = SqlGetLobbyPlayerCount(conn, invite.Value.lobbyId);
        if (playerCount >= lobby.Value.maxPlayers)
            return Conflict("lobby_full");

        SqlUpdateInviteStatus(conn, inviteId, "accepted");
        SqlAddPlayerToLobby(conn, invite.Value.lobbyId, accountData.id);

        LobbyData lobbyData = SqlGetLobby(conn, invite.Value.lobbyId, accountData.id);
        return Ok(lobbyData);
    }

    [HttpDelete("lobby-invites/{inviteId}")]
    public IActionResult DeclineLobbyInvite(int inviteId) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "DeclineLobbyInvite");
        using SQLiteConnection conn = SqlFunctions.CreateConnection();

        var invite = SqlGetLobbyInvite(conn, inviteId);
        if (invite == null || invite.Value.inviteeId != accountData.id)
            return NotFound();

        SqlUpdateInviteStatus(conn, inviteId, "declined");
        return Ok();
    }

    [HttpPost("lobbies/{lobbyId}/deck/{deckId}")]
    public IActionResult SelectLobbyDeck(int lobbyId, int deckId) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "SelectLobbyDeck");
        using SQLiteConnection conn = SqlFunctions.CreateConnection();

        if (!SqlIsPlayerInLobby(conn, lobbyId, accountData.id))
            return NotFound("Not in this lobby");

        SqlSetPlayerDeck(conn, lobbyId, accountData.id, deckId);
        return Ok();
    }

    [HttpPost("lobbies/{lobbyId}/ready")]
    public IActionResult ToggleLobbyReady(int lobbyId) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "ToggleLobbyReady");
        using SQLiteConnection conn = SqlFunctions.CreateConnection();

        if (!SqlIsPlayerInLobby(conn, lobbyId, accountData.id))
            return NotFound("Not in this lobby");

        SqlTogglePlayerReady(conn, lobbyId, accountData.id);
        LobbyData lobby = SqlGetLobby(conn, lobbyId, accountData.id);
        return Ok(lobby);
    }

    public class LobbySettingsRequest {
        public string lobbyType { get; set; }
        public string lobbySubtype { get; set; }
    }

    public class LobbyBestOfRequest {
        public int bestOf { get; set; }
    }

    [HttpPost("lobbies/{lobbyId}/settings")]
    public IActionResult UpdateLobbySettings(int lobbyId, [FromBody] LobbySettingsRequest request) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "UpdateLobbySettings");
        using SQLiteConnection conn = SqlFunctions.CreateConnection();

        var lobby = SqlGetLobbyBasic(conn, lobbyId);
        if (lobby == null) return NotFound();
        if (lobby.Value.hostId != accountData.id) return Forbid();

        // Validate values
        if (request.lobbyType != "normal" && request.lobbyType != "tournament")
            return BadRequest("Invalid lobby type");
        if (request.lobbySubtype != "constructed" && request.lobbySubtype != "draft")
            return BadRequest("Invalid lobby subtype");

        SqlUpdateLobbySettings(conn, lobbyId, request.lobbyType, request.lobbySubtype);
        return Ok();
    }

    [HttpPost("lobbies/{lobbyId}/best-of")]
    public IActionResult UpdateLobbyBestOf(int lobbyId, [FromBody] LobbyBestOfRequest request) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "UpdateLobbyBestOf");
        using SQLiteConnection conn = SqlFunctions.CreateConnection();

        var lobby = SqlGetLobbyBasic(conn, lobbyId);
        if (lobby == null) return NotFound();
        if (lobby.Value.hostId != accountData.id) return Forbid();

        // Validate bestOf (only 1, 3, or 5 allowed)
        if (request.bestOf != 1 && request.bestOf != 3 && request.bestOf != 5)
            return BadRequest("Best of must be 1, 3, or 5");

        SqlUpdateLobbyBestOf(conn, lobbyId, request.bestOf);
        return Ok();
    }

    [HttpPost("lobbies/{lobbyId}/start")]
    [Produces("application/json")]
    public IActionResult StartLobbyGame(int lobbyId) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "StartLobbyGame");
        using SQLiteConnection conn = SqlFunctions.CreateConnection();

        var lobby = SqlGetLobbyBasic(conn, lobbyId);
        if (lobby == null) return NotFound();
        if (lobby.Value.hostId != accountData.id) return Forbid();
        if (lobby.Value.lobbyType != "normal") return BadRequest("Use tournament start for tournaments");

        var players = SqlGetLobbyPlayers(conn, lobbyId);
        if (players.Count != 2) return BadRequest("Need exactly 2 players");

        // Host doesn't need to be ready, only other players
        var nonHostPlayers = players.Where(p => !p.isHost).ToList();
        if (!nonHostPlayers.All(p => p.isReady)) return BadRequest("All players must be ready");
        if (!players.All(p => p.deckId != null)) return BadRequest("All players must have decks selected");

        // Create the match
        var p1 = players[0];
        var p2 = players[1];

        lock (MatchLockObj) {
            GameMatch newMatch = new GameMatch(
                matches.NextMatchId(),
                new Player(p1.displayName, p1.playerId),
                new Player(p2.displayName, p2.playerId)
            );
            newMatch.bestOf = lobby.Value.bestOf;  // Set best-of from lobby
            matches.SetMatchData(newMatch);
            newMatch.playerOne.deck = GetDeckCards(p1.deckId!.Value, newMatch);
            newMatch.playerTwo.deck = GetDeckCards(p2.deckId!.Value, newMatch);
            newMatch.InitializeMatch();

            SqlUpdateLobbyStatusAndMatch(conn, lobbyId, "in_progress", newMatch.matchId);

            return Ok(new { matchId = newMatch.matchId });
        }
    }

    // ==================== DRAFT ENDPOINTS ====================

    [HttpPost("lobbies/{lobbyId}/start-draft")]
    [Produces("application/json")]
    public IActionResult StartDraft(int lobbyId) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "StartDraft");
        using SQLiteConnection conn = SqlFunctions.CreateConnection();

        var lobby = SqlGetLobbyBasic(conn, lobbyId);
        if (lobby == null) return NotFound();
        if (lobby.Value.hostId != accountData.id) return Forbid();
        if (lobby.Value.lobbySubtype != "draft") return BadRequest("Lobby is not in draft mode");

        var players = SqlGetLobbyPlayers(conn, lobbyId);
        if (players.Count != 2) return BadRequest("Need exactly 2 players");

        var nonHostPlayers = players.Where(p => !p.isHost).ToList();
        if (!nonHostPlayers.All(p => p.isReady)) return BadRequest("All players must be ready");

        lock (DraftLockObj) {
            // Generate packs
            var packs = PackGenerator.GeneratePacks(8);

            // Create draft state
            var draftState = new DraftState {
                draftId = nextDraftId++,
                lobbyId = lobbyId,
                status = "drafting",
                currentRound = 0,
                currentPick = 0,
                passDirection = 1
            };

            // Create player states
            var p1State = new DraftPlayerState {
                playerId = players[0].playerId,
                displayName = players[0].displayName
            };
            var p2State = new DraftPlayerState {
                playerId = players[1].playerId,
                displayName = players[1].displayName
            };

            // Assign packs to players
            PackGenerator.AssignPacksToPlayers(packs, p1State, p2State);

            // Open first pack for each player (set currentPackCards)
            if (p1State.packs.Count > 0) {
                p1State.packs[0].isOpened = true;
                p1State.currentPackCards = new List<int>(p1State.packs[0].cardIds);
            }
            if (p2State.packs.Count > 0) {
                p2State.packs[0].isOpened = true;
                p2State.currentPackCards = new List<int>(p2State.packs[0].cardIds);
            }

            draftState.players.Add(p1State);
            draftState.players.Add(p2State);

            activeDrafts[draftState.draftId] = draftState;

            // Update lobby status
            SqlUpdateLobbyDraft(conn, lobbyId, "drafting", draftState.draftId);

            // Return full display state for the requesting player
            var displayState = new DraftDisplayState(draftState, accountData.id);
            return Created($"/draft/{draftState.draftId}", displayState);
        }
    }

    [HttpGet("draft/{draftId}")]
    [Produces("application/json")]
    public IActionResult GetDraftState(int draftId) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "GetDraftState");

        lock (DraftLockObj) {
            if (!activeDrafts.TryGetValue(draftId, out var draftState)) {
                return NotFound("Draft not found");
            }

            var displayState = new DraftDisplayState(draftState, accountData.id);
            return Ok(displayState);
        }
    }

    [HttpPost("draft/{draftId}/pick/{cardId}")]
    [Produces("application/json")]
    public IActionResult SubmitPick(int draftId, int cardId) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "SubmitPick");

        lock (DraftLockObj) {
            if (!activeDrafts.TryGetValue(draftId, out var draftState)) {
                return NotFound("Draft not found");
            }

            if (draftState.status != "drafting") {
                return BadRequest("Draft is not in drafting phase");
            }

            var player = draftState.GetPlayer(accountData.id);
            if (player == null) {
                return BadRequest("Player not in this draft");
            }

            if (player.hasSubmittedPick) {
                return BadRequest("Already submitted a pick this round");
            }

            if (!player.currentPackCards.Contains(cardId)) {
                return BadRequest("Card not in current pack");
            }

            // Record the pick
            player.submittedPick = cardId;
            player.hasSubmittedPick = true;
            player.draftedCardIds.Add(cardId);
            player.currentPackCards.Remove(cardId);

            // Check if both players have submitted
            if (draftState.BothPlayersSubmitted()) {
                ProcessPackPassing(draftState);
            }

            var displayState = new DraftDisplayState(draftState, accountData.id);
            return Ok(displayState);
        }
    }

    private void ProcessPackPassing(DraftState draftState) {
        var player1 = draftState.players[0];
        var player2 = draftState.players[1];

        // Check if current packs are empty (end of round)
        if (player1.currentPackCards.Count == 0 && player2.currentPackCards.Count == 0) {
            draftState.currentRound++;
            draftState.currentPick = 0;
            draftState.passDirection *= -1;  // Alternate direction

            // Check if all rounds complete (4 rounds, one per pack pair)
            if (draftState.currentRound >= 4) {
                draftState.status = "deck_building";
                Console.WriteLine($"Draft {draftState.draftId} moving to deck building phase");
            } else {
                // Open next pack for BOTH players (each round uses one pack from each player)
                int packIndex = draftState.currentRound;  // 0-3 maps directly to pack index

                if (packIndex < player1.packs.Count) {
                    player1.packs[packIndex].isOpened = true;
                    player1.currentPackCards = new List<int>(player1.packs[packIndex].cardIds);
                }
                if (packIndex < player2.packs.Count) {
                    player2.packs[packIndex].isOpened = true;
                    player2.currentPackCards = new List<int>(player2.packs[packIndex].cardIds);
                }
                Console.WriteLine($"Draft {draftState.draftId} starting round {draftState.currentRound}, pack index {packIndex}");
            }
        } else {
            // Swap packs between players (pass in the current direction)
            var temp = player1.currentPackCards;
            player1.currentPackCards = player2.currentPackCards;
            player2.currentPackCards = temp;
            draftState.currentPick++;
        }

        // Reset pick states for next pick
        player1.hasSubmittedPick = false;
        player1.submittedPick = null;
        player2.hasSubmittedPick = false;
        player2.submittedPick = null;
    }

    public class SubmitDraftDeckRequest {
        public List<int> deck { get; set; } = new();
    }

    [HttpPost("draft/{draftId}/submit-deck")]
    [Produces("application/json")]
    public IActionResult SubmitDraftDeck(int draftId, [FromBody] SubmitDraftDeckRequest request) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "SubmitDraftDeck");

        lock (DraftLockObj) {
            if (!activeDrafts.TryGetValue(draftId, out var draftState)) {
                return NotFound("Draft not found");
            }

            if (draftState.status != "deck_building") {
                return BadRequest("Draft is not in deck building phase");
            }

            var player = draftState.GetPlayer(accountData.id);
            if (player == null) {
                return BadRequest("Player not in this draft");
            }

            if (request.deck.Count != 40) {
                return BadRequest("Deck must contain exactly 40 cards");
            }

            // Validate all cards are from drafted pool
            foreach (int cardId in request.deck) {
                if (!player.draftedCardIds.Contains(cardId)) {
                    return BadRequest($"Card {cardId} was not drafted");
                }
            }

            player.finalDeck = request.deck;
            player.deckReady = true;

            // Save the draft deck to the player's collection
            SaveDraftDeckToCollection(accountData.id, request.deck);

            if (draftState.BothDecksReady()) {
                draftState.status = "ready";
                Console.WriteLine($"Draft {draftState.draftId} - both decks ready, creating match...");

                // Get lobby bestOf setting
                using SQLiteConnection conn = SqlFunctions.CreateConnection();
                var lobby = SqlGetLobbyBasic(conn, draftState.lobbyId);
                int bestOf = lobby?.bestOf ?? 1;

                // Auto-create the match
                var player1 = draftState.players[0];
                var player2 = draftState.players[1];

                lock (MatchLockObj) {
                    GameMatch newMatch = new GameMatch(
                        matches.NextMatchId(),
                        new Player(player1.displayName, player1.playerId),
                        new Player(player2.displayName, player2.playerId)
                    );
                    newMatch.bestOf = bestOf;  // Set best-of from lobby
                    matches.SetMatchData(newMatch);

                    // Convert card IDs to Card objects for the deck
                    newMatch.playerOne.deck = player1.finalDeck
                        .Select(cardId => Card.GetCard(newMatch.GetNextUid(), cardId, newMatch))
                        .ToList();
                    newMatch.playerTwo.deck = player2.finalDeck
                        .Select(cardId => Card.GetCard(newMatch.GetNextUid(), cardId, newMatch))
                        .ToList();

                    newMatch.InitializeMatch();

                    // Store matchId in draft state so clients can get it
                    draftState.matchId = newMatch.matchId;

                    Console.WriteLine($"Draft match {newMatch.matchId} created (Best of {bestOf})");
                }
            }

            var displayState = new DraftDisplayState(draftState, accountData.id);
            return Ok(displayState);
        }
    }

    [HttpPost("draft/{draftId}/skip-to-deckbuilding")]
    [Produces("application/json")]
    public IActionResult SkipToDeckBuilding(int draftId) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "SkipToDeckBuilding (DEBUG)");

        lock (DraftLockObj) {
            if (!activeDrafts.TryGetValue(draftId, out var draftState)) {
                return NotFound("Draft not found");
            }

            if (draftState.status == "deck_building") {
                // Already in deck building, just return current state
                var displayState = new DraftDisplayState(draftState, accountData.id);
                return Ok(displayState);
            }

            // Get all available card IDs
            var allCardIds = new List<int>();
            for (int i = 1; i <= 280; i++) {
                try {
                    var json = Utils.GetCardJson(i);
                    if (!string.IsNullOrEmpty(json)) allCardIds.Add(i);
                } catch { }
            }

            var random = new Random();

            // Generate 60 random cards for each player
            foreach (var player in draftState.players) {
                player.draftedCardIds.Clear();
                for (int i = 0; i < 60; i++) {
                    int randomIndex = random.Next(allCardIds.Count);
                    player.draftedCardIds.Add(allCardIds[randomIndex]);
                }
                player.currentPackCards.Clear();
                player.hasSubmittedPick = false;
            }

            draftState.status = "deck_building";
            Console.WriteLine($"Draft {draftState.draftId} DEBUG: Skipped to deck building phase");

            var result = new DraftDisplayState(draftState, accountData.id);
            return Ok(result);
        }
    }

    [HttpPost("draft/{draftId}/start-match")]
    [Produces("application/json")]
    public IActionResult StartDraftMatch(int draftId) {
        if (!IsAuthorized(Request.Headers["Authorization"], out var accountData)) return Unauthorized();
        RequestToConsole(accountData.displayName, "StartDraftMatch");

        lock (DraftLockObj) {
            if (!activeDrafts.TryGetValue(draftId, out var draftState)) {
                return NotFound("Draft not found");
            }

            if (draftState.status != "ready") {
                return BadRequest("Draft is not ready to start match");
            }

            var player1 = draftState.players[0];
            var player2 = draftState.players[1];

            lock (MatchLockObj) {
                GameMatch newMatch = new GameMatch(
                    matches.NextMatchId(),
                    new Player(player1.displayName, player1.playerId),
                    new Player(player2.displayName, player2.playerId)
                );
                matches.SetMatchData(newMatch);

                // Convert card IDs to Card objects for the deck
                newMatch.playerOne.deck = player1.finalDeck
                    .Select(cardId => Card.GetCard(newMatch.GetNextUid(), cardId, newMatch))
                    .ToList();
                newMatch.playerTwo.deck = player2.finalDeck
                    .Select(cardId => Card.GetCard(newMatch.GetNextUid(), cardId, newMatch))
                    .ToList();

                newMatch.InitializeMatch();

                // Clean up draft
                activeDrafts.Remove(draftId);

                Console.WriteLine($"Draft match {newMatch.matchId} created");
                return Ok(new { matchId = newMatch.matchId });
            }
        }
    }

    private void SqlUpdateLobbyDraft(SQLiteConnection conn, int lobbyId, string status, int draftId) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE lobbies SET status = '{status}', draft_id = {draftId} WHERE id = {lobbyId}";
        cmd.ExecuteNonQuery();
    }

    // ==================== LOBBY SQL HELPERS ====================

    private int SqlCreateLobby(SQLiteConnection conn, int hostId, string lobbyType, int maxPlayers, int bestOf = 1) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            INSERT INTO lobbies (host_id, lobby_type, max_players, best_of)
            VALUES ({hostId}, '{lobbyType}', {maxPlayers}, {bestOf});
            SELECT last_insert_rowid();";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private (int id, int hostId, string lobbyType, string lobbySubtype, string status, int maxPlayers, int bestOf, int? matchId, int? draftId)? SqlGetLobbyBasic(SQLiteConnection conn, int lobbyId) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT id, host_id, lobby_type, lobby_subtype, status, max_players, best_of, match_id, draft_id FROM lobbies WHERE id = {lobbyId}";
        using var reader = cmd.ExecuteReader();
        if (reader.Read()) {
            int bestOf = reader.IsDBNull(6) ? 1 : reader.GetInt32(6);
            int? matchId = reader.IsDBNull(7) ? null : reader.GetInt32(7);
            int? draftId = reader.IsDBNull(8) ? null : reader.GetInt32(8);
            return (reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetInt32(5), bestOf, matchId, draftId);
        }
        return null;
    }

    private LobbyData SqlGetLobby(SQLiteConnection conn, int lobbyId, int requestingPlayerId) {
        var basic = SqlGetLobbyBasic(conn, lobbyId);
        if (basic == null) return null!;

        var hostAccount = SqlGetAccountById(conn, basic.Value.hostId);
        var players = SqlGetLobbyPlayers(conn, lobbyId);
        var pendingInvites = SqlGetPendingInvitesForLobby(conn, lobbyId);

        return new LobbyData(
            id: basic.Value.id,
            hostId: basic.Value.hostId,
            hostDisplayName: hostAccount?.displayName ?? "Unknown",
            lobbyType: basic.Value.lobbyType,
            lobbySubtype: basic.Value.lobbySubtype,
            status: basic.Value.status,
            maxPlayers: basic.Value.maxPlayers,
            bestOf: basic.Value.bestOf,
            matchId: basic.Value.matchId,
            draftId: basic.Value.draftId,
            players: players,
            pendingInvites: pendingInvites
        );
    }

    private List<PendingInviteData> SqlGetPendingInvitesForLobby(SQLiteConnection conn, int lobbyId) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT li.invitee_id, a.display_name
            FROM lobby_invites li
            INNER JOIN accounts a ON li.invitee_id = a.id
            WHERE li.lobby_id = {lobbyId} AND li.status = 'pending'";

        List<PendingInviteData> invites = new();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) {
            invites.Add(new PendingInviteData(
                inviteeId: reader.GetInt32(0),
                inviteeDisplayName: reader.GetString(1)
            ));
        }
        return invites;
    }

    private void SqlCleanupOfflineLobbyPlayers(SQLiteConnection conn, int lobbyId) {
        // Remove players who have been offline for more than LobbyOfflineThresholdSeconds
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            UPDATE lobby_players
            SET status = 'left'
            WHERE lobby_id = {lobbyId}
            AND status = 'active'
            AND player_id IN (
                SELECT id FROM accounts
                WHERE last_active IS NULL
                OR datetime(last_active) < datetime('now', '-{LobbyOfflineThresholdSeconds} seconds')
            )
            AND player_id != (SELECT host_id FROM lobbies WHERE id = {lobbyId})";
        cmd.ExecuteNonQuery();

        // Also cleanup invites from/to offline players
        using var cmd2 = conn.CreateCommand();
        cmd2.CommandText = $@"
            UPDATE lobby_invites
            SET status = 'expired'
            WHERE lobby_id = {lobbyId}
            AND status = 'pending'
            AND (
                inviter_id IN (
                    SELECT id FROM accounts
                    WHERE last_active IS NULL
                    OR datetime(last_active) < datetime('now', '-{LobbyOfflineThresholdSeconds} seconds')
                )
                OR invitee_id IN (
                    SELECT id FROM accounts
                    WHERE last_active IS NULL
                    OR datetime(last_active) < datetime('now', '-{LobbyOfflineThresholdSeconds} seconds')
                )
            )";
        cmd2.ExecuteNonQuery();
    }

    private void SqlCleanupOfflineHostLobbies(SQLiteConnection conn) {
        // Cancel lobbies where the host is offline
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            UPDATE lobbies
            SET status = 'cancelled'
            WHERE status = 'waiting'
            AND host_id IN (
                SELECT id FROM accounts
                WHERE last_active IS NULL
                OR datetime(last_active) < datetime('now', '-{LobbyOfflineThresholdSeconds} seconds')
            )";
        cmd.ExecuteNonQuery();
    }

    private void SqlCleanupAllOfflineInvites(SQLiteConnection conn) {
        // Expire invites where the inviter is offline
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            UPDATE lobby_invites
            SET status = 'expired'
            WHERE status = 'pending'
            AND inviter_id IN (
                SELECT id FROM accounts
                WHERE last_active IS NULL
                OR datetime(last_active) < datetime('now', '-{LobbyOfflineThresholdSeconds} seconds')
            )";
        cmd.ExecuteNonQuery();

        // Also expire invites for cancelled lobbies
        using var cmd2 = conn.CreateCommand();
        cmd2.CommandText = @"
            UPDATE lobby_invites
            SET status = 'expired'
            WHERE status = 'pending'
            AND lobby_id IN (SELECT id FROM lobbies WHERE status = 'cancelled')";
        cmd2.ExecuteNonQuery();
    }

    private List<LobbyPlayerData> SqlGetLobbyPlayers(SQLiteConnection conn, int lobbyId) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT lp.player_id, a.display_name, lp.deck_id, d.deck_name, lp.is_ready, lp.status,
                   (SELECT host_id FROM lobbies WHERE id = {lobbyId}) as host_id
            FROM lobby_players lp
            INNER JOIN accounts a ON lp.player_id = a.id
            LEFT JOIN decks d ON lp.deck_id = d.id
            WHERE lp.lobby_id = {lobbyId} AND lp.status = 'active'";

        List<LobbyPlayerData> players = new();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) {
            int hostId = reader.GetInt32(6);
            players.Add(new LobbyPlayerData(
                playerId: reader.GetInt32(0),
                displayName: reader.GetString(1),
                deckId: reader.IsDBNull(2) ? null : reader.GetInt32(2),
                deckName: reader.IsDBNull(3) ? null : reader.GetString(3),
                isReady: reader.GetInt32(4) == 1,
                isHost: reader.GetInt32(0) == hostId,
                status: reader.GetString(5)
            ));
        }
        return players;
    }

    private bool SqlIsPlayerInLobby(SQLiteConnection conn, int lobbyId, int playerId) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT 1 FROM lobby_players WHERE lobby_id = {lobbyId} AND player_id = {playerId} AND status = 'active'";
        using var reader = cmd.ExecuteReader();
        return reader.HasRows;
    }

    private int SqlGetLobbyPlayerCount(SQLiteConnection conn, int lobbyId) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM lobby_players WHERE lobby_id = {lobbyId} AND status = 'active'";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private void SqlAddPlayerToLobby(SQLiteConnection conn, int lobbyId, int playerId) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"INSERT OR IGNORE INTO lobby_players (lobby_id, player_id) VALUES ({lobbyId}, {playerId})";
        cmd.ExecuteNonQuery();
    }

    private void SqlRemovePlayerFromLobby(SQLiteConnection conn, int lobbyId, int playerId) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE lobby_players SET status = 'left' WHERE lobby_id = {lobbyId} AND player_id = {playerId}";
        cmd.ExecuteNonQuery();
    }

    private void SqlKickPlayerFromLobby(SQLiteConnection conn, int lobbyId, int playerId) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE lobby_players SET status = 'kicked' WHERE lobby_id = {lobbyId} AND player_id = {playerId}";
        cmd.ExecuteNonQuery();
    }

    private void SqlCancelLobby(SQLiteConnection conn, int lobbyId) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE lobbies SET status = 'cancelled' WHERE id = {lobbyId}";
        cmd.ExecuteNonQuery();
    }

    private void SqlUpdateLobbyStatus(SQLiteConnection conn, int lobbyId, string status) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE lobbies SET status = '{status}' WHERE id = {lobbyId}";
        cmd.ExecuteNonQuery();
    }

    private void SqlUpdateLobbyStatusAndMatch(SQLiteConnection conn, int lobbyId, string status, int matchId) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE lobbies SET status = '{status}', match_id = {matchId} WHERE id = {lobbyId}";
        cmd.ExecuteNonQuery();
    }

    private void SqlSetPlayerDeck(SQLiteConnection conn, int lobbyId, int playerId, int deckId) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE lobby_players SET deck_id = {deckId} WHERE lobby_id = {lobbyId} AND player_id = {playerId}";
        cmd.ExecuteNonQuery();
    }

    private void SqlTogglePlayerReady(SQLiteConnection conn, int lobbyId, int playerId) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE lobby_players SET is_ready = 1 - is_ready WHERE lobby_id = {lobbyId} AND player_id = {playerId}";
        cmd.ExecuteNonQuery();
    }

    private void SqlUpdateLobbySettings(SQLiteConnection conn, int lobbyId, string lobbyType, string lobbySubtype) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE lobbies SET lobby_type = '{lobbyType}', lobby_subtype = '{lobbySubtype}' WHERE id = {lobbyId}";
        cmd.ExecuteNonQuery();
    }

    private void SqlUpdateLobbyBestOf(SQLiteConnection conn, int lobbyId, int bestOf) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE lobbies SET best_of = {bestOf} WHERE id = {lobbyId}";
        cmd.ExecuteNonQuery();
    }

    private void SqlCreateLobbyInvite(SQLiteConnection conn, int lobbyId, int inviterId, int inviteeId) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"INSERT INTO lobby_invites (lobby_id, inviter_id, invitee_id) VALUES ({lobbyId}, {inviterId}, {inviteeId})";
        cmd.ExecuteNonQuery();
    }

    private bool SqlHasPendingInvite(SQLiteConnection conn, int lobbyId, int inviteeId) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT 1 FROM lobby_invites WHERE lobby_id = {lobbyId} AND invitee_id = {inviteeId} AND status = 'pending'";
        using var reader = cmd.ExecuteReader();
        return reader.HasRows;
    }

    private (int id, int lobbyId, int inviterId, int inviteeId)? SqlGetLobbyInvite(SQLiteConnection conn, int inviteId) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT id, lobby_id, inviter_id, invitee_id FROM lobby_invites WHERE id = {inviteId}";
        using var reader = cmd.ExecuteReader();
        if (reader.Read()) {
            return (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3));
        }
        return null;
    }

    private List<LobbyInviteData> SqlGetLobbyInvites(SQLiteConnection conn, int inviteeId) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT li.id, li.lobby_id, l.lobby_type, li.inviter_id, a.display_name, l.max_players, li.created_at,
                   (SELECT COUNT(*) FROM lobby_players WHERE lobby_id = li.lobby_id AND status = 'active') as player_count
            FROM lobby_invites li
            INNER JOIN lobbies l ON li.lobby_id = l.id
            INNER JOIN accounts a ON li.inviter_id = a.id
            WHERE li.invitee_id = {inviteeId} AND li.status = 'pending' AND l.status = 'waiting'
            ORDER BY li.created_at DESC";

        List<LobbyInviteData> invites = new();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) {
            invites.Add(new LobbyInviteData(
                inviteId: reader.GetInt32(0),
                lobbyId: reader.GetInt32(1),
                lobbyType: reader.GetString(2),
                inviterId: reader.GetInt32(3),
                inviterDisplayName: reader.GetString(4),
                maxPlayers: reader.GetInt32(5),
                createdAt: DateTime.Parse(reader.GetString(6)),
                currentPlayerCount: reader.GetInt32(7)
            ));
        }
        return invites;
    }

    private void SqlUpdateInviteStatus(SQLiteConnection conn, int inviteId, string status) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE lobby_invites SET status = '{status}' WHERE id = {inviteId}";
        cmd.ExecuteNonQuery();
    }

}

public class CreateLobbyRequest {
    public string lobbyType { get; set; } = "normal";
    public int maxPlayers { get; set; } = 2;
}
