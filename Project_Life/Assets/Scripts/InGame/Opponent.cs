using InGame;
using UnityEngine;

// !!
// !! THIS IS DISABLED UNTIL A USE CASE PRESENTS ITSELF
// !!
public class Opponent : Participant {
    public override CardDisplay Summon(CardDisplayData cardDisplayData) {
        CardDisplay baseCardDisplay = base.Summon(cardDisplayData);
        baseCardDisplay.ownerIsOpponent = true;
        return baseCardDisplay;
    }
}
