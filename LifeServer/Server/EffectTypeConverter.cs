using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Server.CardProperties;
using Server.Effects;

namespace Server;

public class EffectTypeConverter : JsonConverter {
    
    public override bool CanConvert(Type objectType) {
        return typeof(Effect) == objectType;
    }
    
    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer) {
        JObject jo = JObject.Load(reader);
        string effect = jo["effect"].Value<string>();

        switch (effect) {
            case "addCounter":
                return jo.ToObject<AddCounterEffect>(serializer);
            case "applySpellburn":
                return jo.ToObject<ApplySpellburnEffect>(serializer);
            case "attach":
                return jo.ToObject<AttachEffect>(serializer);
            case "bypassHerbLifeReduction":
                return jo.ToObject<BypassHerbLifeReductionEffect>(serializer);
            case "bypassSummonLimit":
                return jo.ToObject<BypassSummonLimitEffect>(serializer);
            case "canAttack":
                return jo.ToObject<CanAttackEffect>(serializer);
            case "cantAttack":
                return jo.ToObject<CantAttackEffect>(serializer);
            case "cantAttackOrBlock":
                return jo.ToObject<CantAttackOrBlockEffect>(serializer);
            case "cantGainLife":
                return jo.ToObject<CantGainLifeEffect>(serializer);
            case "cantTribute":
                return jo.ToObject<CantTributeEffect>(serializer);
            case "cardRitualOfDarkness":
                return jo.ToObject<CardRitualOfDarknessEffect>(serializer);
            case "cardSetStraight":
                return jo.ToObject<CardSetStraightEffect>(serializer);
            case "castCard":
                return jo.ToObject<CastCardEffect>(serializer);
            case "changeSource":
                return jo.ToObject<ChangeSourceEffect>(serializer);
            case "discardOpponent":
                return jo.ToObject<DiscardOpponentEffect>(serializer);
            case "changeStats":
                return jo.ToObject<ChangeStatsEffect>(serializer);
            case "choose":
                return jo.ToObject<ChooseEffect>(serializer);
            case "copySpell":
                return jo.ToObject<CopySpellEffect>(serializer);
            case "counter":
                return jo.ToObject<CounterEffect>(serializer);
            case "createToken":
                return jo.ToObject<CreateTokenEffect>(serializer);
            case "dealDamage":
                return jo.ToObject<DealDamageEffect>(serializer);
            case "destroy":
                return jo.ToObject<DestroyEffect>(serializer);
            case "detain":
                return jo.ToObject<DetainEffect>(serializer);
            case "discard":
                return jo.ToObject<DiscardEffect>(serializer);
            case "draw":
                return jo.ToObject<DrawEffect>(serializer);
            case "endGame":
                return jo.ToObject<EndGameEffect>(serializer);
            case "endTurn":
                return jo.ToObject<EndTurnEffect>(serializer);
            case "exileAndReturn":
                return jo.ToObject<ExileAndReturnEffect>(serializer);
            case "extraTurn":
                return jo.ToObject<ExtraTurnEffect>(serializer);
            case "forceAttack":
                return jo.ToObject<ForceAttackEffect>(serializer);
            case "gainControl":
                return jo.ToObject<GainControlEffect>(serializer);
            case "gainLife":
                return jo.ToObject<GainLifeEffect>(serializer);
            case "goToPhase":
                return jo.ToObject<GoToPhaseEffect>(serializer);
            case "grantKeyword":
                return jo.ToObject<GrantKeywordEffect>(serializer);
            case "grantPassive":
                return jo.ToObject<GrantPassiveEffect>(serializer);
            case "lookAtDeck":
                return jo.ToObject<LookAtDeckEffect>(serializer);
            case "loseLife":
                return jo.ToObject<LoseLifeEffect>(serializer);
            case "mill":
                return jo.ToObject<MillEffect>(serializer);
            case "modifyCost":
                return jo.ToObject<ModifyCostEffect>(serializer);
            case "modifyDamage":
                return jo.ToObject<ModifyDamageEffect>(serializer);
            case "modifyHandSize":
                return jo.ToObject<ModifyHandSizeEffect>(serializer);
            case "modifySummonLimit":
                return jo.ToObject<ModifySummonLimitEffect>(serializer);
            case "modifyType":
                return jo.ToObject<ModifyTypeEffect>(serializer);
            case "playerModifier":
                return jo.ToObject<PlayerModifierEffect>(serializer);
            case "removeCounter":
                return jo.ToObject<RemoveCounterEffect>(serializer);
            case "repeatAllEffects":
                return jo.ToObject<RepeatAllEffectsEffect>(serializer);
            case "repeat":
                return jo.ToObject<RepeatEffect>(serializer);
            case "reveal":
                return jo.ToObject<RevealEffect>(serializer);
            case "sacrifice":
                return jo.ToObject<SacrificeEffect>(serializer);
            case "sendToZone":
                return jo.ToObject<SendToZoneEffect>(serializer);
            case "setLifeTotal":
                return jo.ToObject<SetLifeTotalEffect>(serializer);
            case "shuffleDeck":
                return jo.ToObject<ShuffleDeckEffect>(serializer);
            case "tutor":
                return jo.ToObject<TutorEffect>(serializer);
            case "eventTriggers":
                return jo.ToObject<EventTriggersEffect>(serializer);
            case "replacementEffect":
                return jo.ToObject<ReplacementEffectEffect>(serializer);
            default:
                throw new Exception("Unknown Effect: " + effect);
        }
    }
    
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) {
        throw new NotImplementedException();
    }
 
}