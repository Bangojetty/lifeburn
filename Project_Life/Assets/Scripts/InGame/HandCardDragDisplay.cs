using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Vector3 = System.Numerics.Vector3;

public class HandCardDragDisplay : MonoBehaviour {
    public Canvas canvas;
    public float smoothSpeed = 10f;
    public Vector2 targetPosition;

    private void OnEnable() {
        GetComponent<RectTransform>().anchoredPosition = targetPosition;
    }

    private void Update() {
        targetPosition = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
        transform.position = Vector2.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);
    }

    public void SetPositionOffset() {
        // position offset stuff 
    }
    
    

}
