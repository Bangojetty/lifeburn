using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Server.CardProperties;

namespace Server.Effects;

public class ModifyCostEffect : Effect {
    
    public ModifyCostEffect(EffectType effect) : base(effect) {
    }
}