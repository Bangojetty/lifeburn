using Server.CardProperties;

namespace Server;

public class TriggerContext {
    public Trigger trigger;
    public Player triggerController;
    public Zone? zone;
    public Zone? sourceZone;  // for EnteredZone triggers - the zone the card came from (can be modified by Ghost Deceiver)
    public Card? card;
    public List<Card>? cards;
    public Phase? phase;
    public bool isFirstDraw;  // for Draw triggers - true if this was the first draw of the turn
    public Card? tributeForCard;  // for Tribute triggers - the card being summoned that required the tribute
    public Card? targetCard;  // for AttackedSummon triggers - the summon that was attacked
    public bool wasSpellburnt;  // for Cast triggers - whether the spell was cast with spellburnt
    public int millBatchSize;  // for Mill triggers - how many cards were milled in this batch (for "mill 5 or more" triggers)

    public TriggerContext(Trigger trigger) {
        this.trigger = trigger;
    }

    public TriggerContext(Trigger trigger, Zone? zone = null, Card? card = null, List<Card>? cards = null, Phase? phase = null, Player? triggerController = null, Zone? sourceZone = null, Card? tributeForCard = null, Card? targetCard = null) {
        this.trigger = trigger;
        this.zone = zone;
        this.sourceZone = sourceZone;
        this.card = card;
        if (cards != null) this.cards = cards.ToList();
        this.phase = phase;
        if (triggerController != null) this.triggerController = triggerController;
        this.tributeForCard = tributeForCard;
        this.targetCard = targetCard;
    }

    public static TriggerContext CreatePhaseTriggerContext(Phase phase) {
        TriggerContext newTriggerContext = new TriggerContext(Trigger.Phase);
        newTriggerContext.trigger = Trigger.Phase;
        return newTriggerContext;
    }
}