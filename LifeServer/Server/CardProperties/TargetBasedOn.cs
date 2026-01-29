namespace Server.CardProperties;

public enum TargetBasedOn {
    AttachedTo,
    RootAffected,
    NextSpellOpponent,
    NextSpell,
    Weakest,
    Strongest,
    AttackedPlayer,
    RootController,
    NextSummon,
    TriggerCard,  // The card that caused the trigger to fire (e.g., the dying card for death triggers)
    TriggerController,  // The player who caused the trigger to fire (e.g., the player who cast the spell)
    SourcePlayer  // The player who cast the spell (caster) - runs once per affected card
}