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
    GrantKeywordToNextSummon,  // Player passive: grants keyword to next summon cast (with optional tribe filter)
    GrantKeywordToFutureTokens,  // Player passive: grants keyword to tokens created this turn
    SummonsToGraveyardExileInstead,  // Player passive: summons go to exile instead of graveyard this turn
    CantTribute,  // Player passive: prevents tributing summons (0-cost summons still castable)
    CantGainLife,  // Player passive: prevents gaining life for the rest of the game
    OnlySummonTribe,  // Player passive: can only summon cards of a specific tribe this turn
    CantAttack,  // Card passive: this card cannot attack
    GroundTactics,  // Player passive: forces all summons to attack, caster controls attack assignments
    ExileInsteadOfGraveyardOnTribute  // Card passive: when tributed, exile instead of going to graveyard (replacement effect)
}