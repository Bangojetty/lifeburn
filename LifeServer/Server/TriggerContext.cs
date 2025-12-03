using Server.CardProperties;

namespace Server;

public class TriggerContext {
    public Trigger trigger;
    public Player triggerController;
    public Zone? zone;
    public Card? card;
    public List<Card>? cards;
    public Phase? phase;

    public TriggerContext(Trigger trigger) {
        this.trigger = trigger;
    }

    public TriggerContext(Trigger trigger, Zone? zone = null, Card? card = null, List<Card>? cards = null, Phase? phase = null) {
        this.trigger = trigger;
        this.zone = zone;
        this.card = card;
        if (cards != null) this.cards = cards.ToList();
        this.phase = phase;
    }

    public static TriggerContext CreatePhaseTriggerContext(Phase phase) {
        TriggerContext newTriggerContext = new TriggerContext(Trigger.Phase);
        newTriggerContext.trigger = Trigger.Phase;
        return newTriggerContext;
    }
}