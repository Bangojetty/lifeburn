using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class ZoneScaler : MonoBehaviour {
    public RectTransform rectTransform;
    public GridLayoutGroup layoutGroup;
    public float widthThreshold;
    public float childThreshhold;
    public float hSpacingRatio;
    public float vSpacingRatio;
    public float cardMaxWidth;
    public float cardMaxHeight;

    private float currentChildScale = 1f;

    private void Update() {
        Rescale();
    }
    public void Rescale() {
        // verify rescale requirements
        if (rectTransform.rect.width < widthThreshold && transform.childCount <= childThreshhold) {
            ResetScaling();
        }
        while (rectTransform.rect.width > widthThreshold) {
            // add a row
            if (transform.childCount > 8 && layoutGroup.constraintCount < 2) {
                layoutGroup.constraintCount = 2;
                break;
            }
            // reduce the scale factor
            currentChildScale -= 0.1f;
            // set the scale of each card
            foreach (Transform child in gameObject.transform) {
                RectTransform childRTransform = child.GetComponent<RectTransform>();
                childRTransform.localScale = new Vector3(currentChildScale, currentChildScale, currentChildScale);
            }
            // spacing for the whole play zone
            layoutGroup.spacing = new Vector2(hSpacingRatio * currentChildScale, vSpacingRatio * currentChildScale);
            // cellsize for the whole play zone
            layoutGroup.cellSize = new Vector2(cardMaxWidth * currentChildScale, cardMaxHeight * currentChildScale);
            Debug.Log("current child scale is: " + currentChildScale);
            break;
        }
    }

    public float GetScaleFactor() {
        return currentChildScale;
    }
    private void ResetScaling() {
        currentChildScale = 1f;
        // reset scale of each card
        foreach (Transform child in gameObject.transform) {
            RectTransform childRTransform = child.GetComponent<RectTransform>();
            childRTransform.localScale = new Vector3(currentChildScale, currentChildScale, currentChildScale);
        }
        // reset spacing and cell size
        layoutGroup.spacing = new Vector2(hSpacingRatio, vSpacingRatio);
        layoutGroup.cellSize = new Vector2(cardMaxWidth, cardMaxHeight);
        layoutGroup.constraintCount = 1;
    }
}
