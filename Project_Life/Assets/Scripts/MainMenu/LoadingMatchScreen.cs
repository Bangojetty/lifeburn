using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

public class LoadingMatchScreen : MonoBehaviour {
    public GameObject username1;
    public GameObject username2;
    public GameObject background;
    public GameObject title;
    void Awake() {
        background.GetComponent<Animation>().Play();
        title.GetComponent<Animation>().Play();
        username1.GetComponent<Animation>().Play();
        username2.GetComponent<Animation>().Play();
    }
}
