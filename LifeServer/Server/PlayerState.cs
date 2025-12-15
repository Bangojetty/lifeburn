using Microsoft.AspNetCore.Mvc.TagHelpers;
using Server.CardProperties;

namespace Server;

public class PlayerState : ParticipantState {
    // most of this data is not required for client code,
    // but I left it here for security checks down the road
    public List<CardDisplayData> playables { get; set; }
    public List<CardDisplayData> activatables { get; set; }
    public List<GameEvent> eventList { get; set; }

    // Debug: current deck contents (top of deck = first in list)
    public List<CardDisplayData> deckContents { get; set; }

    public PlayerState(Player player) {
        playerName = player.playerName;
        uid = player.uid;
        lifeTotal = player.lifeTotal;
        spellBurnt = player.spellBurnt;
        nextSpellFree = player.nextSpellFree;
        spellCounter = player.spellCounter;
        deckAmount = player.deck?.Count ?? 0;
        eventList = new List<GameEvent>();
        eventList.AddRange(player.eventList);
        playables = player.playables.Select(card => new CardDisplayData(card)).ToList();
        activatables = player.activatables.Select(card => new CardDisplayData(card)).ToList();
        // Debug: include full deck contents
        deckContents = player.deck?.Select(card => new CardDisplayData(card)).ToList() ?? new List<CardDisplayData>();
    }
}