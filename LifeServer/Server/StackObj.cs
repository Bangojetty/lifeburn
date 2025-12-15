using System.Diagnostics;
using Server.CardProperties;

namespace Server;

public class StackObj {
    public Card sourceCard { get; set; }
    public StackObjType stackObjType;
    public Zone sourceZone { get; set; }
    public Player player { get; set; }
    public List<Effect>? effects { get; set; }
    
    // non-json
    public string? customDescription;
    private List<EffectType> effectsThatHaltEvents = new();
    
    public StackObj(Card sourceCard, StackObjType stackObjType, List<Effect> effects, Zone sourceZone, Player player, string? customDescription = null) {
        this.sourceCard = sourceCard;
        this.stackObjType = stackObjType;
        this.effects = effects;
        this.sourceZone = sourceZone;
        this.player = player;
        this.customDescription = customDescription;
        // Note: LookAtDeck halts only if it has deckDestinations (selection needed)
        // Peek (LookAtDeck without deckDestinations) doesn't halt
        effectsThatHaltEvents.Add(EffectType.Tutor);
    }

    public StackObj(Card sourceCard, StackObjType stackObjType, Zone sourceZone, Player player) {
        this.sourceCard = sourceCard;
        this.stackObjType = stackObjType;
        this.sourceZone = sourceZone;
        this.player = player;
    }

    public void ResolveStackObj(GameMatch gameMatch, int startIndex = 0) {
        Console.WriteLine($"[ResolveStackObj] Starting resolution of {sourceCard?.name} at index {startIndex}, effects.Count={effects?.Count}");
        Debug.Assert(effects != null);
        for (int i = startIndex; i < effects.Count; i++) {
            Effect currentEffect = effects[i];
            Console.WriteLine($"[ResolveStackObj] Processing effect {i}: {currentEffect.effect}, isCost={currentEffect.isCost}");
            // Set parent effect list so RepeatAllEffects knows what to repeat
            currentEffect.parentEffectList = effects;
            if (!currentEffect.ConditionsAreMet(gameMatch, player)) continue;

            // Handle isCost effects - costs that must be paid during resolution
            if (currentEffect.isCost) {
                // Check if cost can be paid
                if (!currentEffect.CanPayCost(gameMatch, player)) {
                    Console.WriteLine($"[ResolveStackObj] Cost effect {currentEffect.effect} cannot be paid - fizzling remaining effects");
                    FinalizeResolve(gameMatch);
                    return;
                }

                // Check if cost needs user selection
                if (currentEffect.NeedsCostSelection(gameMatch, player)) {
                    Console.WriteLine($"[ResolveStackObj] Cost effect {currentEffect.effect} needs user selection - halting");
                    List<int> selectableUids = currentEffect.GetCostSelectableUids(gameMatch, player);
                    gameMatch.RequestCostEffectSelection(player, currentEffect, selectableUids);
                    gameMatch.unresolvedStackObj = this;
                    gameMatch.unresolvedEffectIndex = i;  // Stay on this effect to resolve after selection
                    return;
                }
                // Cost can be auto-paid - continue to resolve normally
                Console.WriteLine($"[ResolveStackObj] Cost effect {currentEffect.effect} can be auto-paid");
            }

            if (currentEffect.optional) {
                gameMatch.HandleOptionalEffect(player, null, currentEffect);
                gameMatch.unresolvedStackObj = this;
                gameMatch.unresolvedEffectIndex = i + 1;
                return;
            }
            // Handle resolve-time target selection (e.g., Consider: select cards after drawing)
            if (currentEffect.resolveTarget && currentEffect.targetType != null && currentEffect.targetUids.Count == 0) {
                Console.WriteLine($"[ResolveStackObj] Effect {i} needs resolve-time targets, halting");
                gameMatch.RequestResolveTimeTargets(player, currentEffect);
                gameMatch.unresolvedStackObj = this;
                gameMatch.unresolvedEffectIndex = i;  // Stay on this effect to resolve after targets selected
                return;
            } else if (currentEffect.resolveTarget && currentEffect.targetType != null) {
                Console.WriteLine($"[ResolveStackObj] Effect {i} has resolveTarget but already has {currentEffect.targetUids.Count} targets");
            }
            // Handle "each player chooses" effects (e.g., Return - each player returns a summon)
            if (currentEffect.eachPlayer) {
                bool needsInput = gameMatch.HandleEachPlayerEffect(currentEffect, player);
                if (needsInput) {
                    gameMatch.unresolvedStackObj = this;
                    gameMatch.unresolvedEffectIndex = i + 1;  // Move past this effect after responses
                    return;
                }
                // If no input needed (no valid targets), effect was already resolved
                continue;
            }
            // Handle playerChoice discard (e.g., Ghastly - discard any number of shadow summons)
            if (currentEffect.effect == EffectType.Discard &&
                currentEffect.amountBasedOn == AmountBasedOn.PlayerChoice &&
                currentEffect.targetUids.Count == 0) {
                bool needsInput = gameMatch.RequestPlayerChoiceDiscard(player, currentEffect, variableAmount: true);
                if (needsInput) {
                    gameMatch.unresolvedStackObj = this;
                    gameMatch.unresolvedEffectIndex = i;  // Stay on this effect to resolve after selection
                    return;
                }
                // If no input needed (no matching cards), set amount to 0 and continue
                currentEffect.amount = 0;
            }
            // Handle fixed-amount non-random discard (e.g., Loot Ghost - discard exactly 2)
            if (currentEffect.effect == EffectType.Discard &&
                currentEffect.amountBasedOn != AmountBasedOn.PlayerChoice &&
                !currentEffect.random &&
                currentEffect.amount > 0 &&
                currentEffect.targetUids.Count == 0) {
                bool needsInput = gameMatch.RequestPlayerChoiceDiscard(player, currentEffect, variableAmount: false);
                if (needsInput) {
                    gameMatch.unresolvedStackObj = this;
                    gameMatch.unresolvedEffectIndex = i;  // Stay on this effect to resolve after selection
                    return;
                }
                // If no input needed (not enough cards), discard what's available
            }
            // Handle playerChoice CastCard from targetZones (e.g., Ghost Gathering - cast any number of ghosts from hand/graveyard)
            if (currentEffect.effect == EffectType.CastCard &&
                currentEffect.targetZones != null &&
                currentEffect.amountBasedOn == AmountBasedOn.PlayerChoice &&
                currentEffect.targetUids.Count == 0) {
                bool needsInput = gameMatch.RequestPlayerChoiceCast(player, currentEffect);
                if (needsInput) {
                    gameMatch.unresolvedStackObj = this;
                    gameMatch.unresolvedEffectIndex = i;  // Stay on this effect to resolve after selection
                    return;
                }
                // If no input needed (no matching cards), skip this effect
                continue;
            }
            currentEffect.Resolve(gameMatch, player);
            // Check if this effect needs to halt for player input
            bool shouldHalt = effectsThatHaltEvents.Contains(currentEffect.effect);
            // LookAtDeck only halts if it requires selection (has deckDestinations)
            if (currentEffect.effect == EffectType.LookAtDeck && currentEffect.deckDestinations != null) {
                shouldHalt = true;
            }
            if (shouldHalt) {
                gameMatch.unresolvedStackObj = this;
                gameMatch.unresolvedEffectIndex = i + 1;
                return;
            }
        }
        FinalizeResolve(gameMatch);
    }
    
    public void ResumeResolve(GameMatch gameMatch) {
        gameMatch.unresolvedStackObj = null;
        Debug.Assert(effects != null);
        if (effects.Count <= gameMatch.unresolvedEffectIndex) {
            FinalizeResolve(gameMatch);
            return;
        }
        ResolveStackObj(gameMatch, gameMatch.unresolvedEffectIndex);
    }

    private void FinalizeResolve(GameMatch gameMatch) {
        // set the spell card for adding to graveyard on resolve
        Card? spellCard = null;
        if (stackObjType == StackObjType.Spell && sourceCard.type == CardType.Spell) spellCard = sourceCard;
        gameMatch.CreateAndAddResolveEvent(player, spellCard);

        // Check for exhaust keyword on spells - prevents casting more spells this turn
        if (stackObjType == StackObjType.Spell && sourceCard.type == CardType.Spell && sourceCard.HasKeyword(Keyword.Exhaust)) {
            player.exhausted = true;
        }

        // summon the spell if it's a summon spell
        if (stackObjType == StackObjType.Spell) {
            switch (sourceCard.type) {
                case CardType.Summon:
                    gameMatch.Summon(sourceCard, player, false);
                    break;
                case CardType.Object:
                    gameMatch.SummonNonSummon(sourceCard, player);
                    break;
            }
        }
        // if any attack targets are required (summons that enter attacking), bail out and check for triggers after
        // client response
        if (gameMatch.requiredAttackTargets > 0) return;
        gameMatch.CheckForTriggersAndPassives(EventType.Resolve);
    }
}