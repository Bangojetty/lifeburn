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
        effectsThatHaltEvents.Add(EffectType.LookAtDeck);
        effectsThatHaltEvents.Add(EffectType.Tutor);
    }

    public StackObj(Card sourceCard, StackObjType stackObjType, Zone sourceZone, Player player) {
        this.sourceCard = sourceCard;
        this.stackObjType = stackObjType;
        this.sourceZone = sourceZone;
        this.player = player;
    }

    public void ResolveStackObj(GameMatch gameMatch, List<Effect>? stackEffects = null) {
        Debug.Assert(effects != null);
        stackEffects ??= effects;
        for (int i = 0; i < stackEffects.Count; i++) {
            if (!stackEffects[i].ConditionsAreMet(gameMatch, player)) continue;
            if (stackEffects[i].optional) {
                gameMatch.HandleOptionalEffect(player, null, stackEffects[i]);
                gameMatch.unresolvedStackObj = this;
                gameMatch.unresolvedEffectIndex = i + 1;
                return;
            }
            stackEffects[i].Resolve(gameMatch, player);
            if (effectsThatHaltEvents.Contains(stackEffects[i].effect)) {
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
        List<Effect> unresolvedEffects = new(); 
        if (effects.Count <= gameMatch.unresolvedEffectIndex) {
            FinalizeResolve(gameMatch);
            return;
        }
        for (int i = gameMatch.unresolvedEffectIndex; i < effects.Count; i++) {
            unresolvedEffects.Add(effects[i]);
        }
        ResolveStackObj(gameMatch, unresolvedEffects);
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
}