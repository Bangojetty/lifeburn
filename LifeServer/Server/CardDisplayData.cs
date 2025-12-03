using Server.CardProperties;

namespace Server;

public class CardDisplayData {
    public int uid { get; set; }
    public int id { get; set; }
    public string name { get; set; }
    public int cost{ get; set; }
    public CardType type { get; set; }
    public int? attack { get; set; }
    public int? defense { get; set; }
    public List<Keyword>? keywords { get; set; }
    public Tribe tribe { get; set; }
    public Rarity rarity { get; set; }
    public string description { get; set; }
    public string additionalDescription { get; set; }
    public TokenType? tokenType { get; set; }
    
    public bool hasXCost { get; set; }

    // public CardDisplayData(int uid, int id, string name, int cost, CardType type, int attack, int defense, List<Keyword?> keywords, Tribe tribe, Rarity rarity, string description) {
    //     this.uid = uid;
    //     this.id = id;
    //     this.name = name;
    //     this.cost = cost;
    //     this.type = type;
    //     this.attack = attack;
    //     this.defense = defense;
    //     this.keywords = keywords;
    //     this.tribe = tribe;
    //     this.rarity = rarity;
    //     this.description = description;
    // }

    public CardDisplayData(Card card, TokenType? tokenType = null) {
        uid = card.uid;
        id = card.id;
        name = card.name;
        cost = card.GetCost();
        type = card.type;
        if (card.attack != null) attack = card.GetAttack();
        if (card.defense != null) defense = card.GetDefense();
        // get keywords using GetKeywords (this can return a null list -> so we check for null afterward)
        List<Keyword>? tempKeywords = card.GetKeywords();
        if(tempKeywords != null) keywords = tempKeywords.ToList();
        tribe = card.tribe;
        rarity = card.rarity;
        description = card.displayDescription;
        if (card.chosenIndices.Count > 0) description = GetDescriptionAfterChoices(card);
        additionalDescription = card.GetAdditionalDescription();
        if (card is Token) {
            this.tokenType = tokenType;
        }
        hasXCost = card.HasXCost();
    }


    public string GetDescriptionAfterChoices(Card card) {
        if(card.description == null) return "";
        string tempDescription = card.description;
        foreach (KeyValuePair<int, int> pair in card.chosenIndices) {
            tempDescription = Utils.MarkChosenToken(tempDescription, pair.Key, pair.Value);
        }
        return tempDescription;
    }

    
}
