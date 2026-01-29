namespace Server.CardProperties;

public enum ModifierType {
    X,
    SummonsDiedThisTurn,
    FirstSpell,
    DynamicAdd  // Adds calculated amount to base cost (based on xBasedOn), shows actual cost in hand
}