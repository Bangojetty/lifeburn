using System.Collections;
using System.Collections.Generic;
using TestingScene;
using UnityEngine;

public class MouseFollowScript : MonoBehaviour {
    public TestingManager testingManager;
    void Update() {
        transform.position = testingManager.mousePosInWorld;
    }
}
