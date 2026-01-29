namespace Server;

public enum StackObjType {
    Spell,
    SpellCopy,  // Copy of a spell (e.g., from Merfolk Mage)
    ActivatedEffect,
    TriggeredEffect
}