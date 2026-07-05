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

    public StackObj(Card sourceCard, StackObjType stackObjType, List<Effect> effects, Zone sourceZone, Player player, string? customDescription = null) {
        this.sourceCard = sourceCard;
        this.stackObjType = stackObjType;
        this.effects = effects;
        this.sourceZone = sourceZone;
        this.player = player;
        this.customDescription = customDescription;
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
            // Handle resolve-time target selection (e.g., ForkBolt: select targets after cast)
            if (currentEffect.resolveTarget && currentEffect.HasTargeting() && currentEffect.targetUids.Count == 0) {
                Console.WriteLine($"[ResolveStackObj] Effect {i} needs resolve-time targets");
                bool needsInput = gameMatch.RequestResolveTimeTargets(player, currentEffect);
                if (needsInput) {
                    Console.WriteLine($"[ResolveStackObj] Halting for resolve-time target selection");
                    gameMatch.unresolvedStackObj = this;
                    gameMatch.unresolvedEffectIndex = i;  // Stay on this effect to resolve after targets selected
                    return;
                }
                // No valid targets - effect fizzles this part, continue to next effect
                Console.WriteLine($"[ResolveStackObj] No valid targets, skipping effect");
                continue;
            } else if (currentEffect.resolveTarget && currentEffect.HasTargeting()) {
                Console.WriteLine($"[ResolveStackObj] Effect {i} has resolveTarget but already has {currentEffect.targetUids.Count} targets");
            }
            // Handle resolve-time selection from zone (e.g., Consider: select cards from hand after drawing)
            if (currentEffect.resolveTarget && currentEffect.HasSelection() && currentEffect.targetUids.Count == 0) {
                Console.WriteLine($"[ResolveStackObj] Effect {i} needs resolve-time zone selection, halting");
                gameMatch.RequestResolveTimeSelection(player, currentEffect);
                gameMatch.unresolvedStackObj = this;
                gameMatch.unresolvedEffectIndex = i;  // Stay on this effect to resolve after selection
                return;
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
                !currentEffect.all &&
                currentEffect.amountBasedOn == AmountBasedOn.PlayerChoice &&
                currentEffect.targetUids.Count == 0 &&
                currentEffect.ConditionsAreMet(gameMatch, player)) {
                bool needsInput = gameMatch.RequestPlayerChoiceDiscard(player, currentEffect, variableAmount: true);
                if (needsInput) {
                    gameMatch.unresolvedStackObj = this;
                    gameMatch.unresolvedEffectIndex = i;  // Stay on this effect to resolve after selection
                    return;
                }
                // If no input needed (no matching cards), set amount to 0 and continue
                currentEffect.amount = 0;
            }
            // Handle select.upToAll discard (e.g., Shade Runner - discard any amount)
            if (currentEffect.effect == EffectType.Discard &&
                !currentEffect.all &&
                currentEffect.select?.upToAll == true &&
                currentEffect.targetUids.Count == 0 &&
                currentEffect.ConditionsAreMet(gameMatch, player)) {
                bool needsInput = gameMatch.RequestPlayerChoiceDiscard(player, currentEffect, variableAmount: true);
                if (needsInput) {
                    gameMatch.unresolvedStackObj = this;
                    gameMatch.unresolvedEffectIndex = i;  // Stay on this effect to resolve after selection
                    return;
                }
                // If no input needed (no matching cards), set amount to 0 and continue
                currentEffect.amount = 0;
            }
            // Handle fixed-amount non-random discard (e.g., Loot Ghost - discard exactly 2, Reap - opponent discards 2)
            // Check if targetUids contains ONLY a player UID (opponent targeting) vs card UIDs (already selected cards)
            // If count > 1 and first is player UID, we already have card selections after the player UID
            bool targetUidsContainsOnlyPlayerUid = currentEffect.targetUids.Count == 1 && gameMatch.IsPlayerUid(currentEffect.targetUids[0]);
            bool needsDiscardSelection = currentEffect.targetUids.Count == 0 || targetUidsContainsOnlyPlayerUid;
            if (currentEffect.effect == EffectType.Discard &&
                !currentEffect.all &&
                currentEffect.amountBasedOn != AmountBasedOn.PlayerChoice &&
                !currentEffect.random &&
                currentEffect.amount > 0 &&
                needsDiscardSelection &&
                currentEffect.ConditionsAreMet(gameMatch, player)) {
                // Determine who is discarding - check targetType or if a player UID was targeted
                Player discardingPlayer = player;
                if (targetUidsContainsOnlyPlayerUid) {
                    // Player was targeted directly (e.g., Reap targeting opponent)
                    // Keep the player UID in targetUids[0] so Effect.Resolve can determine resolvedAffectedPlayer
                    // The discard loop in Effect.Resolve will skip player UIDs
                    discardingPlayer = gameMatch.PlayerByUid(currentEffect.targetUids[0]);
                } else if (currentEffect.GetTargetType() == TargetType.Opponent) {
                    discardingPlayer = gameMatch.GetOpponent(player);
                } else if (currentEffect.affectedPlayer == "opponent") {
                    discardingPlayer = gameMatch.GetOpponent(player);
                }
                bool needsInput = gameMatch.RequestPlayerChoiceDiscard(discardingPlayer, currentEffect, variableAmount: false);
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
            // Handle fixed-amount CastCard from targetZones (e.g., Goblin Ritualist - cast 1 spell from graveyard)
            if (currentEffect.effect == EffectType.CastCard &&
                currentEffect.targetZones != null &&
                currentEffect.select != null &&
                currentEffect.targetUids.Count == 0) {
                bool needsInput = gameMatch.RequestFixedCastFromZone(player, currentEffect);
                if (needsInput) {
                    gameMatch.unresolvedStackObj = this;
                    gameMatch.unresolvedEffectIndex = i;  // Stay on this effect to resolve after selection
                    return;
                }
                // If no input needed (no matching cards), skip this effect
                continue;
            }
            // Handle mill/draw with an amount-style select ("mill up to 3") - need player to select amount
            if ((currentEffect.effect == EffectType.Mill || currentEffect.effect == EffectType.Draw) &&
                currentEffect.select != null && currentEffect.select.zone == null && currentEffect.select.zones == null &&
                currentEffect.amount == null) {
                Console.WriteLine($"[ResolveStackObj] Effect {i} ({currentEffect.effect}) needs amount selection (max={currentEffect.GetSelectMax()}), deck.Count={player.deck.Count}");
                int maxAmount = currentEffect.GetSelectMax();
                // For mill, cap at deck size
                if (currentEffect.effect == EffectType.Mill) {
                    maxAmount = Math.Min(maxAmount, player.deck.Count);
                }
                Console.WriteLine($"[ResolveStackObj] Requesting amount selection, maxAmount={maxAmount}");
                gameMatch.RequestEffectAmount(player, currentEffect, maxAmount);
                gameMatch.unresolvedStackObj = this;
                gameMatch.unresolvedEffectIndex = i;  // Stay on this effect to resolve after amount selected
                return;
            }
            currentEffect.Resolve(gameMatch, player);
            // Check if this effect has upfront repeat count - execute additional times
            if (currentEffect.selectRepeatUpfront && currentEffect.repeatCount > 0) {
                Console.WriteLine($"[StackObj] Executing {currentEffect.repeatCount} upfront repeats");
                for (int r = 0; r < currentEffect.repeatCount; r++) {
                    // Check if targets are still valid (in play) before repeating
                    bool hasValidTarget = false;
                    foreach (int targetUid in currentEffect.targetUids) {
                        if (gameMatch.cardByUid.TryGetValue(targetUid, out Card? targetCard)) {
                            if (targetCard.currentZone == Zone.Play) {
                                hasValidTarget = true;
                                break;
                            }
                        }
                    }
                    if (!hasValidTarget) {
                        Console.WriteLine($"[StackObj] Skipping repeat {r + 1} - no valid targets remaining");
                        continue;
                    }
                    // Clone the effect and execute it again with same targets
                    Effect repeatEffect = currentEffect.Clone();
                    repeatEffect.targetUids = currentEffect.targetUids.ToList();
                    repeatEffect.Resolve(gameMatch, player);
                }
            }
            // Check if this effect has a repeat cost (old-style) - start the repeat choice flow
            else if (currentEffect.repeatCostType != null && currentEffect.repeatCostAmount != null && !currentEffect.selectRepeatUpfront) {
                gameMatch.unresolvedStackObj = this;
                gameMatch.unresolvedEffectIndex = i + 1;
                gameMatch.StartRepeatChoice(currentEffect, player, currentEffect.targetUids);
                return;
            }
            // Check if this effect needs to halt for player input (post-resolve halts)
            if (ShouldHaltAfterResolve(currentEffect)) {
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

    /// <summary>
    /// Determines if an effect should halt resolution after executing to wait for player input.
    /// These are effects that run but need player interaction to complete (e.g., selecting cards from deck).
    /// Also halts after effects that used resolve-time selection to let the client process animations
    /// before the next effect's prompts appear.
    /// Note: Pre-resolve halts (resolveTarget, eachPlayer, playerChoice, etc.) are handled inline
    /// because they each require different setup logic and halt at different effect indices.
    /// </summary>
    private bool ShouldHaltAfterResolve(Effect effect) {
        return effect.effect switch {
            // Tutor always halts - player must select a card from deck
            EffectType.Tutor => true,
            // LookAtDeck only halts if it has deckDestinations (player must assign cards)
            // Peek (LookAtDeck without deckDestinations) doesn't halt
            EffectType.LookAtDeck => effect.deckDestinations != null,
            // SendToZone halts if it has deckDestinations (player must assign cards to destinations)
            EffectType.SendToZone => effect.deckDestinations != null,
            // CardRitualOfDarkness halts - players alternate putting summons until both pass
            EffectType.CardRitualOfDarkness => true,
            _ => false
        };
    }
}