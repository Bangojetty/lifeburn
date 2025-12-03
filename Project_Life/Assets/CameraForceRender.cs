using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraForceRender : MonoBehaviour
{
    public Camera renderCamera;
    void Update() {
        renderCamera.Render();
    }
}
