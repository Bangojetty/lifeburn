using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TargetingArrow : MonoBehaviour {
    public GameManager gameManager;
    public LineRenderer lineRenderer;
    public Camera mainCam;
    
    void Update() {
        Vector3 mousePos = Input.mousePosition;
        Vector3 mouseToWorldPos = mainCam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y));
        Vector3 finalPos = new Vector3(mouseToWorldPos.x, mouseToWorldPos.y, 0);
        lineRenderer.SetPosition(1, finalPos);
    }
}