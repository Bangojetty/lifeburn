namespace Server.CardProperties;

public enum Passive {
    BypassSummonLimit,
    ChangeStats,
    GrantKeyword,
    GrantActive,
    DisableKeyword,
    ModifyCost,
    CantTakeDamage,
    CantDealDamage,
    TopCardRevealed,
    AdditionalSummonTopCard,
    CantBeTargeted,
    ImmuneToKeyword,
    DefenseUsedForAttack,
    Sacrifice,
    OnlyTributeToTreefolk,
    TributeRestriction,
    SproutTriggersOnDeath,
    ModifyKeywordAmount,
    CantBeAttacked,
    DisableEnterPlayEffects,
    CantSpecialSummon,
    TokenCanTribute,
    CreateTokenModifier,
    ThisTurn,
    GrantKeywordToNextSpell,  // Player passive: grants keyword to next spell cast
    SummonsToGraveyardExileInstead  // Player passive: summons go to exile instead of graveyard this turn
}