namespace Server;

public class TargetSelection {
    public List<int> selectableUids { get; set; }
    public int amount { get; set; }


    public TargetSelection(List<int> selectableUids, int amount) {
        this.selectableUids = selectableUids;
        this.amount = amount;
    }
}