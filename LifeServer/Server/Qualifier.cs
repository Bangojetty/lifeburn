using System.Security.AccessControl;
using Server.CardProperties;

namespace Server;

public class Qualifier {
    public Tribe? tribe;
    public CardType? cardType;
    public TokenType? tokenType;
    public TargetType? targetType;
    public List<Restriction>? restrictions;
    public List<Condition>? conditions;
    public PassiveEffect? passive;
    public Card? sourceCard;
    public Player sourcePlayer;
    public Scope scope = Scope.All;



    public Qualifier(Effect e, Player sourcePlayer) {
        tribe = e.tribe;
        cardType = e.cardType;
        tokenType = e.tokenType;
        targetType = e.targetType;
        if (e.restrictions != null) restrictions = e.restrictions.ToList();
        if (e.conditions != null) conditions = e.conditions.ToList();
        sourceCard = e.sourceCard;
        scope = e.scope;
        this.sourcePlayer = sourcePlayer;
    }

    public Qualifier(TriggeredEffect t, Player sourcePlayer) {
        tribe = t.tribe;
        cardType = t.cardType;
        tokenType = t.tokenType;
        if (t.restrictions != null) restrictions = t.restrictions.ToList();
        if (t.conditions != null) conditions = t.conditions.ToList();
        sourceCard = t.sourceCard;
        scope = t.scope;
        this.sourcePlayer = sourcePlayer;
    }

    public Qualifier(ActivatedEffect a, Player sourcePlayer) {
        tribe = a.tribe;
        cardType = a.cardType;
        tokenType = a.tokenType;
        if(a.restrictions != null) restrictions = a.restrictions.ToList();
        if (a.conditions != null) conditions = a.conditions.ToList();
        sourceCard = a.sourceCard;
        // ActivatedEffect doesn't have scope yet - default to All
        scope = Scope.All;
        this.sourcePlayer = sourcePlayer;
    }

    public Qualifier(PassiveEffect p, Player sourcePlayer) {
        tribe = p.tribe;
        scope = p.scope;
        passive = p;
        tokenType = p.tokenType;  // For passives targeting tokens (e.g., GrantActive)
        // Use owner for logic (granted passives), fallback to grantedBy for innate passives
        sourceCard = p.owner ?? p.grantedBy;
        if(p.restrictions != null) restrictions = p.restrictions.ToList();
        this.sourcePlayer = sourcePlayer;
    }
    
    public Qualifier(AdditionalCost cost, Player sourcePlayer) {
        tokenType = cost.tokenType;
        this.sourcePlayer = sourcePlayer;
    }
    
    
}