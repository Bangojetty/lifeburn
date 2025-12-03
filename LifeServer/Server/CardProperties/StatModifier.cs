namespace Server.CardProperties;

public class StatModifier {
    public StatType statType;
    public OperatorType operatorType;
    public int amount;
    public AmountBasedOn? amountBasedOn;
    public int? amountMulitplier;
    public bool xAmount;
    public bool other = false;

    public int Apply(int statAmount) {
        return operatorType switch {
            OperatorType.Add => statAmount + amount,
            OperatorType.Multiply => statAmount * amount,
            OperatorType.Divide => statAmount / amount,
            _ => throw new ArgumentOutOfRangeException()
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