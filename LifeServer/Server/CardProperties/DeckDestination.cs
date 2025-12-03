namespace Server.CardProperties;

public class DeckDestination {
    public DeckDestinationType deckDestination;
    public bool amountIsPlayerChosen;
    public int? amount;
    public CardType? cardType;
    public Tribe? tribe;
    public Ordering? ordering;
    public bool reveal;


    public DeckDestination(DeckDestinationType type, bool amountIsPlayerChosen, bool reveal, int? amount = null,
        CardType? cardType = null, Tribe? tribe = null, Ordering? ordering = null) {
        deckDestination = type;
        this.amountIsPlayerChosen = amountIsPlayerChosen;
        this.reveal = reveal;
        this.amount = amount;
        this.cardType = cardType;
        this.tribe = tribe;
        this.ordering = ordering;
    }

}