namespace Server;

public abstract class ParticipantState {
    // most of this data is not required for client code,
    // but I left it here for security checks down the road
    public string playerName { get; set; }
    public int uid { get; set; }
    public int lifeTotal { get; set; }
    public bool spellBurnt { get; set; }
    public bool nextSpellFree { get; set; }
    public int spellCounter { get; set; }
    public int deckAmount { get; set; }
    public int handAmount { get; set; }
}