using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace InGame {
    public class AmountSelector : MonoBehaviour {
        public GameManager gameManager;
        public TMP_InputField amountField;
        public Action<int> onConfirm;
        
        private int amount;

        public void SetConfirmCallback(Action<int> callback) {
            onConfirm = callback;
        }

        public void SetAmount() {
            amount = int.Parse(amountField.text);
        }
        
        public void IncrementAmount() {
            amount++;
            CheckSelectionMax();
            amountField.text = amount.ToString();
        }

        private void CheckSelectionMax() {
            if(amount > gameManager.currentSelectionMax) amount = gameManager.currentSelectionMax;
        }
        
        public void DecrementAmount() {
            amount--;
            if(amount < 0) amount = 0;
            amountField.text = amount.ToString();
        }

        public void SubmitAmount() { 
            onConfirm.Invoke(amount);
            ResetAndClose();
        }

        private void ResetAndClose() {
            onConfirm = null;
            amount = 0;
            amountField.text = amount.ToString();
            gameManager.currentSelectionMax = 0;
            gameObject.SetActive(false);
        }
    }
}