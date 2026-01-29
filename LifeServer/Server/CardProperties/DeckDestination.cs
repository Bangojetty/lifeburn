using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Server.CardProperties;

public class DeckDestination {
    // New field - preferred
    [JsonConverter(typeof(StringEnumConverter))]
    public DeckDestinationType? destination;

    // Legacy field - kept for backwards compatibility
    [JsonConverter(typeof(StringEnumConverter))]
    public DeckDestinationType? deckDestination;

    // New unified selection field
    public Select? select;

    // New field - indicates this destination gets remaining cards
    public bool remainder;

    // Legacy field - kept for backwards compatibility
    public bool amountIsPlayerChosen;

    // Legacy field - kept for backwards compatibility (use select instead)
    public int? amount;

    // Filters (can also be in select, but kept here for backwards compatibility)
    [JsonConverter(typeof(StringEnumConverter))]
    public CardType? cardType;

    [JsonConverter(typeof(StringEnumConverter))]
    public Tribe? tribe;

    [JsonConverter(typeof(StringEnumConverter))]
    public Ordering? ordering;

    public bool reveal;

    // Default constructor for JSON deserialization
    public DeckDestination() { }

    // Constructor for programmatic creation
    public DeckDestination(DeckDestinationType type, bool amountIsPlayerChosen, bool reveal, int? amount = null,
        CardType? cardType = null, Tribe? tribe = null, Ordering? ordering = null) {
        destination = type;
        deckDestination = type; // Set both for compatibility
        this.amountIsPlayerChosen = amountIsPlayerChosen;
        this.reveal = reveal;
        this.amount = amount;
        this.cardType = cardType;
        this.tribe = tribe;
        this.ordering = ordering;
    }

    /// <summary>
    /// Gets the destination type, preferring new field over legacy.
    /// </summary>
    public DeckDestinationType GetDestination() {
        return destination ?? deckDestination ?? DeckDestinationType.Bottom;
    }

    /// <summary>
    /// Gets the selection min, considering both new and legacy fields.
    /// </summary>
    public int GetSelectionMin(int availableCards) {
        // New format takes precedence
        if (select != null) {
            if (select.upToAll) return 0;
            return select.GetMin();
        }

        // Legacy format
        if (remainder) return 0;
        if (amountIsPlayerChosen) return 0;
        if (amount != null) return (int)amount;

        return 0; // Default: remainder destination
    }

    /// <summary>
    /// Gets the selection max, considering both new and legacy fields.
    /// </summary>
    public int GetSelectionMax(int availableCards) {
        // New format takes precedence
        if (select != null) {
            if (select.upToAll) return availableCards;
            return select.GetMax();
        }

        // Legacy format
        if (remainder) return availableCards;
        if (amountIsPlayerChosen) return availableCards;
        if (amount != null) return (int)amount;

        return availableCards; // Default: remainder destination
    }

    /// <summary>
    /// Returns true if this is a remainder destination (gets leftover cards).
    /// </summary>
    public bool IsRemainder() {
        if (remainder) return true;

        // Legacy: no amount and not player chosen = remainder
        if (select == null && amount == null && !amountIsPlayerChosen) return true;

        return false;
    }

    /// <summary>
    /// Gets the card type filter, preferring select over top-level.
    /// </summary>
    public CardType? GetCardType() {
        return select?.cardType ?? cardType;
    }

    /// <summary>
    /// Gets the tribe filter, preferring select over top-level.
    /// </summary>
    public Tribe? GetTribe() {
        return select?.tribe ?? tribe;
    }
}