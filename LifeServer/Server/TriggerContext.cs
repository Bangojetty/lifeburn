using Server.CardProperties;

namespace Server;

public class TriggerContext {
    public Trigger trigger;
    public Player triggerController;
    public Zone? zone;
    public Card? card;
    public List<Card>? cards;
    public Phase? phase;
    public bool isFirstDraw;  // for Draw triggers - true if this was the first draw of the turn

    public TriggerContext(Trigger trigger) {
        this.trigger = trigger;
    }

    public TriggerContext(Trigger trigger, Zone? zone = null, Card? card = null, List<Card>? cards = null, Phase? phase = null, Player? triggerController = null) {
        this.trigger = trigger;
        this.zone = zone;
        this.card = card;
        if (cards != null) this.cards = cards.ToList();
        this.phase = phase;
        if (triggerController != null) this.triggerController = triggerController;
    }

    public static TriggerContext CreatePhaseTriggerContext(Phase phase) {
        TriggerContext newTriggerContext = new TriggerContext(Trigger.Phase);
        newTriggerContext.trigger = Trigger.Phase;
        return newTriggerContext;
    }
}