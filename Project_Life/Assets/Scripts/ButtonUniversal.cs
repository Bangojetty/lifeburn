using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ButtonUniversal : MonoBehaviour, IPointerEnterHandler
{
    public AudioManager audioManager;
    public int hoverSfxIndex;
    public int sfxIndex;
    private Button button;
    void Start()
    {
        audioManager = GameObject.Find("AudioManager").GetComponent<AudioManager>();
        button = GetComponent<Button>();
        button.onClick.AddListener(() => audioManager.PlayButtonSFX(sfxIndex));
        
    }

    public void OnPointerEnter(PointerEventData eventData) {
        if(!button.interactable) return;
        audioManager.PlayHoverSFX(hoverSfxIndex);
    }
}
