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
        public Action onCancel;

        private int amount;
        private int? maxOverride;

        public void SetConfirmCallback(Action<int> callback) {
            onConfirm = callback;
        }

        public void SetCancelCallback(Action callback) {
            onCancel = callback;
        }

        public void SetMaxOverride(int? max) {
            maxOverride = max;
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
            int max = maxOverride ?? gameManager.currentSelectionMax;
            if(amount > max) amount = max;
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

        public void CancelSelection() {
            onCancel?.Invoke();
            ResetAndClose();
        }

        private void ResetAndClose() {
            onConfirm = null;
            onCancel = null;
            amount = 0;
            maxOverride = null;
            amountField.text = amount.ToString();
            gameObject.SetActive(false);
        }
    }
}