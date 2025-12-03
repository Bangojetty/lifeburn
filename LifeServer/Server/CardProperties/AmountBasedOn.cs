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
    RootAffected
}