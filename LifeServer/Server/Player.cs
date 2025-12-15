namespace Server;
using Server.CardProperties;

public class Player {
    private const int StartingLife = 40;
    private const int DefaultMaxHandSize = 5;
    public string playerName { get; set; }
    public int playerId { get; set; }
    public int uid { get; set; }
    public int lifeTotal { get; set; }
    public bool spellBurnt { get; set; }
    public int spellCounter { get; set; }
    public List<Card>? deck { get; set; }
    public List<Card> hand { get; set; }
    public List<Card> playField { get; set; }
    public List<Card> graveyard { get; set; }
    public List<Card> exile { get; set; }
    public List<Token> tokens { get; set; }
    public List<Card> allCardsPlayer { get; set; }
    public List<Card> playables { get; set; }
    public List<Card> activatables { get; set; }
    public List<Card> attackCapables { get; set; }
    public List<int> attackableUids { get; set; }
    public List<TriggeredEffect> controlledTriggers { get; set; }
    public List<AdditionalCost> currentChoiceAdditionalCosts { get; set; }
    
    public List<GameEvent> eventList { get; set; }
    
    // non-json
    public Dictionary<Card, int> cardToCardStackId = new();
    public List<Card> ownedCards = new();
    public int totalSummons;
    public int totalSpells;
    public int turnSummonCount;
    public int turnSummonLimitBonus;  // extra summons allowed this turn (from Refresh, etc.)
    public int turnDrawCount;  // tracks draws this turn for "not first draw" triggers
    public int turnHerbSacrificeCount;  // tracks consecutive herb sacrifices for diminishing life gain
    public bool scorched;
    public List<TriggeredEffect> eventTriggers = new();
    public bool cantAttackThisTurn;
    public bool isBot;
    public Phase? passToPhase;
    public bool passToMyMain;  // special case: pass until it's my turn and we're on Main phase
    public List<PassiveEffect> playerPassives = new();  // passives that affect the player (not cards)
    public bool exhausted;  // prevents casting more spells this turn
    public int maxHandSize;  // maximum hand size (default 5)
    public int extraTurns;  // number of extra turns queued for this player
    public bool nextSpellFree;  // next non-summon spell costs 0 LP

    public Player(string playerName, int playerId, bool isBot = false) {
        this.isBot = isBot;
        this.playerName = playerName;
        this.playerId = playerId;
        lifeTotal = StartingLife;
        maxHandSize = DefaultMaxHandSize;
        spellBurnt = false;
        spellCounter = 0;
        totalSummons = 0;
        hand = new List<Card>();
        playField = new List<Card>();
        graveyard = new List<Card>();
        exile = new List<Card>();
        tokens = new List<Token>();
        allCardsPlayer = new List<Card>();
        playables = new List<Card>();
        activatables = new List<Card>();
        attackCapables = new List<Card>();
        attackableUids = new List<int>();
        eventList = new List<GameEvent>();
        controlledTriggers = new List<TriggeredEffect>();
    }
}