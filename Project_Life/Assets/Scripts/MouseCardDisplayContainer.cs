using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MouseCardDisplayContainer : MonoBehaviour {
    public Camera mainCam;
    public RectTransform rectTransform;
    void LateUpdate() {
        Vector3 mouseToScreenPos = GetMouseWorldPositionWithZAs(0);
        transform.position = mouseToScreenPos;
    }
    
        
    public Vector3 GetMouseWorldPositionWithZAs(float zPos) {
        Vector3 mousePos = Input.mousePosition;
        Vector3 mouseToWorldPos = mainCam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y));
        return new Vector3(mouseToWorldPos.x, mouseToWorldPos.y, zPos);
    }
}
