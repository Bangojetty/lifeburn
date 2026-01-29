namespace Server;

// Tracks info about a spell copy effect (e.g., Merfolk Mage)
public class CopySpellInfo {
    public int? maxCost;  // Maximum cost of spell that can be copied (null = no restriction)
    public Card sourceCard;  // The card that created this effect

    public CopySpellInfo(Card sourceCard, int? maxCost = null) {
        this.sourceCard = sourceCard;
        this.maxCost = maxCost;
    }

    // Check if a spell can be copied based on restrictions
    public bool CanCopy(Card spell) {
        if (maxCost != null && spell.cost > maxCost) return false;
        return true;
    }
}
