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
    private const int TotalCardAmount = 281;

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
                currentLocalMatch = null;
                return Ok(myMatchState);
            }
            // 3rd player retries until queue is open again
            if (currentLocalMatch != null) return Ok(null);
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
            if (matchableAccount == accountData) {
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
            Console.WriteLine("event list count is: " + myMatchState.playerState.eventList.Count);
            myGameMatch.ClearEventList(myGameMatch.accountIdToPlayer[accountData.id]);
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
            } else {
                player.passToPhase = passToPhase.HasValue ? (Phase)passToPhase.Value : null;
                player.passToMyMain = false;
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
            // TODO This passes in the first ability a card has. For multiple activated abilities, we'll need
            // TODO the player to choose an index (which ability to activate) and pass that in here instead of .First()
            Card cardWithAbility = myGameMatch.cardByUid[cardUid];
            List<ActivatedEffect> allActivatedEffects = cardWithAbility.GetActivatedEffects();
            Debug.Assert(allActivatedEffects.Count > 0, "this card has no activatable abilities");
            ActivatedEffect aEffect = allActivatedEffects.First();
            myGameMatch.AttemptToActivate(myGameMatch.accountIdToPlayer[accountData.id], aEffect);
            RequestToConsole(accountData.displayName, "AttemptToActivate | eventCount: " + myGameMatch.accountIdToPlayer[accountData.id].eventList.Count);
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
            Player attackingPlayer = myGameMatch.accountIdToPlayer[accountData.id];
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

}
