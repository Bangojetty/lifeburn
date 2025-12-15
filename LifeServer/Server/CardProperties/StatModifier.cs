using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Server.CardProperties;

public class StatModifier {
    public StatType statType;
    public OperatorType operatorType;
    public int amount;
    public AmountBasedOn? amountBasedOn;
    public int? amountMulitplier;
    public bool xAmount;
    [JsonConverter(typeof(StringEnumConverter))]
    public Scope scope = Scope.All;  // For counting: All=include self, OthersOnly=exclude self

    public int Apply(int statAmount) {
        return operatorType switch {
            OperatorType.Add => statAmount + amount,
            OperatorType.Multiply => statAmount * amount,
            OperatorType.Divide => statAmount / amount,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public StatModifier Clone() {
        return new StatModifier {
            statType = statType,
            operatorType = operatorType,
            amount = amount,
            amountBasedOn = amountBasedOn,
            amountMulitplier = amountMulitplier,
            xAmount = xAmount,
            scope = scope
        };
    }
}


public enum StatType {
    Cost,
    Attack,
    Defense
}

public enum OperatorType {
    Add,
    Multiply,
    Divide,
}