using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PhaseButtonManager : MonoBehaviour {
    public GameObject stopBorderObj;
    public RectTransform stopBorderTransform;
    public Image stopBorderImage;
    public List<RectTransform> phaseButtonTransforms;
    public Sprite squareSprite;
    public Sprite wideSprite;
    public Vector2 squareSize;
    public Vector2 wideSize;
    

    public void SetStopBorder(int index) {
        if (index is 2 or 3) {
            stopBorderImage.sprite = wideSprite;
            stopBorderTransform.sizeDelta = new Vector2(wideSize.x, wideSize.y);
        } else {
            stopBorderTransform.sizeDelta = new Vector2(squareSize.x, squareSize.y);
            stopBorderImage.sprite = squareSprite;
        }
        stopBorderTransform.anchoredPosition = phaseButtonTransforms[index].anchoredPosition;
        stopBorderObj.SetActive(true);
    }

    public void DisableStopBorder() {
        stopBorderObj.SetActive(false);
    }
}
