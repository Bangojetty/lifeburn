using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace InGame {
    public class Player : Participant {
        public List<CardDisplayData> hand = new();
        public List<GameObject> handSlots = new(); 
        
    }
}
