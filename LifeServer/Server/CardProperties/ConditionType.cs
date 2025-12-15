namespace Server.CardProperties;

public enum ConditionType {
    Control,
    TopCardIsMerfolk,
    FirstSpell,
    InZone,
    RootTargetTribe,
    RootTargetNotSummon,
    Spellburnt,
    IsTribe,
    IsNotTribe,
    OneOneInPlay,
    DidntAttack,
    HandSize,
    TargetAttack,
    TriggerWasSpellburnt,
    TriggerNotSpellburnt,
    YouHaveMoreLife,
    FirstSummon,
    OpponentHasMore,
    TargetIs,
    EnteredZoneThisTurn,
    Attacked,
    SummonsInPlay,      // Any summons exist in play (either player)
    NoSummonsInPlay     // No summons exist in play
}