namespace Server;

public class TargetSelection {
    public List<int> selectableUids { get; set; }
    public int amount { get; set; }
    public string? message { get; set; }


    public TargetSelection(List<int> selectableUids, int amount, string? message = null) {
        this.selectableUids = selectableUids;
        this.amount = amount;
        this.message = message;
    }
}