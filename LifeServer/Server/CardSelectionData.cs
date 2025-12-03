namespace Server;

public class CardSelectionData {
    public List<int> selectableUids { get; set; }
    public string selectionMessage { get; set; }
    public int selectionMin { get; set; }
    public int selectionMax { get; set; }
    public bool selectOrder { get; set; }
    
    public CardSelectionData(List<int> selectableUids, string selectionMessage, int selectionMin, int selectionMax, bool selectOrder) {
        this.selectableUids = selectableUids.ToList();
        this.selectionMessage = selectionMessage;
        this.selectionMin = selectionMin;
        this.selectionMax = selectionMax;
        this.selectOrder = selectOrder;
    }
}