using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MatchMakingTimer : MonoBehaviour {
    public TMP_Text timerText;
    public float secondsCount;
    public int minuteCount;

    private void Update() {
        if (!gameObject.activeSelf) return;
        secondsCount += Time.deltaTime;
        timerText.text = (secondsCount < 10) ? minuteCount + ":0" + (int)secondsCount : minuteCount + ":" + (int)secondsCount;
        if (!(secondsCount >= 60)) return;
        minuteCount++;
        secondsCount = 0;
    }
}
