namespace Server;

public class TargetSelection {
    public List<int> selectableUids { get; set; }
    public int amount { get; set; }  // Maximum amount to select
    public int minAmount { get; set; }  // Minimum amount to select (0 for optional)
    public string? message { get; set; }
    public bool requireOneFromEach { get; set; }  // Require at least 1 from each player
    public List<int>? playerUids { get; set; }  // UIDs controlled by casting player (for requireOneFromEach validation)
    public List<int>? opponentUids { get; set; }  // UIDs controlled by opponent (for requireOneFromEach validation)


    public TargetSelection(List<int> selectableUids, int amount, string? message = null, int minAmount = -1) {
        this.selectableUids = selectableUids;
        this.amount = amount;
        // Default minAmount to amount (exact selection) if not specified
        this.minAmount = minAmount >= 0 ? minAmount : amount;
        this.message = message;
    }
}