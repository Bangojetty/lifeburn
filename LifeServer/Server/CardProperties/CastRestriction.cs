namespace Server.CardProperties;

public enum CastRestriction {
    OnOpponentAttack,
    InResponse,
    OpponentsTurn,
    ControlGoblin,
    OpponentsTurnBeforeCombat,  // Can only cast on opponent's turn, before their combat phase
    DuringCombat,  // Can only cast during combat when there are attackers assigned
    BothPlayersHaveSummons  // Both players must have at least one summon in play
}