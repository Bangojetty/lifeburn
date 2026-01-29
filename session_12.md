# Session 12: Lobby-to-Match Transition

## Summary
Completed the implementation for transitioning players from a lobby to an active game match.

## Changes Made

### 1. Database Schema Update
- Added `match_id` column to `lobbies` table to track the associated game match

### 2. Server Changes (LifeController.cs)
- Updated `SqlGetLobbyBasic` to return matchId (now returns 7-tuple)
- Added `SqlUpdateLobbyStatusAndMatch` helper method
- Modified `StartLobbyGame` endpoint to:
  - Create a new match using existing match creation logic
  - Store the matchId in the lobby record
  - Update lobby status to "in_progress"
  - Return `Ok(new { matchId = newMatch.matchId })` to the host

### 3. Client DTO Updates
- Added `int? matchId` field to `LobbyData.cs` (both client and server versions)

### 4. LobbyManager.cs Updates
- **Host flow**: `OnReadyStartClicked()` calls `StartLobbyGame`, receives matchId, calls `StartGameTransition(matchId)`
- **Non-host flow**: `PollLobbyState()` coroutine detects when `lobby.status == "in_progress" && lobby.matchId.HasValue`, then calls `StartGameTransition(lobby.matchId.Value)`

### 5. StartGameTransition Method
```csharp
private void StartGameTransition(int matchId) {
    // Stop polling
    if (lobbyPollingCoroutine != null) StopCoroutine(lobbyPollingCoroutine);

    // Set up game data
    if (gameData != null) {
        gameData.matchState = serverApi.GetMatchState(accountDataGO.accountData, matchId);
    }

    // Clean up lobby state
    currentLobby = null;
    currentLobbyId = null;
    isInLobby = false;

    if (lobbyIndicatorBar != null) lobbyIndicatorBar.SetActive(false);

    // Load game scene
    SceneManager.LoadScene("Game Scene");
}
```

## Flow Summary

### Host Starts Game:
1. Host clicks Start button
2. Client calls `POST /life/lobbies/{id}/start`
3. Server creates match, updates lobby with matchId and status="in_progress"
4. Server returns `{ matchId: X }`
5. Host client receives matchId, transitions to Game Scene

### Non-Host Joins Game:
1. Non-host is polling lobby state every 2 seconds
2. Poll detects `status == "in_progress"` and `matchId` is set
3. Non-host client transitions to Game Scene using the matchId

## Start Button Validation (CanStartGame)
- **Normal mode**: Exactly 2 players required
- **Tournament mode**: Even number, 2-16 players
- **Constructed mode**: All players must have decks selected
- **All modes**: All non-host players must be ready
