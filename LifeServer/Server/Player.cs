namespace Server;
using Server.CardProperties;

public class Player {
    private const int StartingLife = 40;
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
    public bool scorched;
    public List<TriggeredEffect> eventTriggers = new();
    


    public Player(string playerName, int playerId) {
        this.playerName = playerName;
        this.playerId = playerId;
        lifeTotal = StartingLife;
        spellBurnt = false;
        spellCounter = 0;
        totalSummons = 0;
        hand = new List<Card>();
        playField = new List<Card>();
        graveyard = new List<Card>();
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