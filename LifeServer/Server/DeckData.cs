namespace Server;

public class DeckData {
    public int id { get; set; }
    public string deckName { get; set; }
    public readonly List<int> deckList = new();

    public DeckData(int id, string deckName) {
        this.id = id;
        this.deckName = deckName;
    }

    public void AddCard(int cardId) {
        deckList.Add(cardId);
    }
    
}