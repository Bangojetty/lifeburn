using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MouseCardDisplayContainer : MonoBehaviour {
    public Camera mainCam;
    public RectTransform rectTransform;
    void LateUpdate() {
        Vector3 mouseToScreenPos = GetMouseWorldPositionWithZAs(0);
        transform.position = ClampToScreen(mouseToScreenPos);
    }

    // Keep the hover card fully on screen - without this, hovering cards near the screen
    // edge pushes the preview off-screen where it can't be read
    private Vector3 ClampToScreen(Vector3 pos) {
        if (mainCam == null) return pos;
        if (rectTransform == null && transform.childCount > 0) {
            rectTransform = transform.GetChild(0) as RectTransform;
        }
        if (rectTransform == null) return pos;

        Rect r = rectTransform.rect;
        Vector3 scale = rectTransform.lossyScale;
        float halfW = r.width * Mathf.Abs(scale.x) / 2f;
        float halfH = r.height * Mathf.Abs(scale.y) / 2f;
        // account for the card being offset from the container position (pivot/anchoring)
        Vector3 centerOffset = rectTransform.TransformPoint(r.center) - transform.position;

        Vector3 min = mainCam.ScreenToWorldPoint(new Vector3(0f, 0f));
        Vector3 max = mainCam.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height));
        pos.x = Mathf.Clamp(pos.x, min.x + halfW - centerOffset.x, max.x - halfW - centerOffset.x);
        pos.y = Mathf.Clamp(pos.y, min.y + halfH - centerOffset.y, max.y - halfH - centerOffset.y);
        return pos;
    }
    
        
    public Vector3 GetMouseWorldPositionWithZAs(float zPos) {
        Vector3 mousePos = Input.mousePosition;
        Vector3 mouseToWorldPos = mainCam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y));
        return new Vector3(mouseToWorldPos.x, mouseToWorldPos.y, zPos);
    }
}
