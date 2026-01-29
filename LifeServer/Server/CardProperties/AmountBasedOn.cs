namespace Server.CardProperties;

public enum AmountBasedOn {
    // consider making this an object so you can normalize some of these values (e.g. tribe controlled/inplay)
    SummonsInGraveyard,
    GoblinsInPlay,
    StonesInPlay,
    TargetPower,
    TargetCost,
    SummonsOpponentControls,
    SummonsThatDiedThisTurn,
    SubtractLife,
    HerbsControlled,
    StonesControlled,
    PlantsControlled,
    GoblinsControlled,
    TreefolkControlled,
    UntilCardType,
    X,
    OpponentExcessSummons,
    MerfolkInGraveyard,
    ActivatedCost,
    Zero,
    RootAmount,
    PlayerChoice,
    LifeTotal,
    DeckSize,
    Attack,
    FinalAttack,  // card's attack after all modifiers (counters, buffs, etc.)
    RootAffected,
    HerbSacrificeLifeGain,  // 2 for first herb, 1 for each subsequent herb sacrificed this turn
    RedCardsDiscardedForCost,  // number of goblin (red) cards discarded as additional cost
    HalfLife  // half of player's current life, rounded down
}