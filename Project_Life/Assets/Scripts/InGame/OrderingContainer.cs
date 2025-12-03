using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OrderingContainer : MonoBehaviour {
    public Image selectButtonImg;
    public Sprite selectedSprite;
    public Sprite blankSprite;
    public GameObject orderNumBox;
    public TMP_Text orderNumText;
    public bool isSet;
    public GameManager gameManager;
    public int index;
    
    public void SetOrder() {
        if (!isSet) {
            selectButtonImg.sprite = selectedSprite;
            orderNumBox.SetActive(true);
            gameManager.finalOrderList.Add(this);
            isSet = true;
            gameManager.UpdateAllOrderingContainers();
        } else {
            selectButtonImg.sprite = blankSprite;
            orderNumBox.SetActive(false);
            gameManager.finalOrderList.Remove(this);
            isSet = false;
            gameManager.UpdateAllOrderingContainers();
        }
    }
    

    public void UpdateNumText() {
        if (!gameManager.finalOrderList.Contains(this)) return;
        orderNumText.text = (gameManager.finalOrderList.IndexOf(this) + 1).ToString();
    }
}
