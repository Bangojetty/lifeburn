using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour { 
    
    public List<AudioClip> buttonClips;
    public List<AudioClip> hoverClips;
    public List<AudioClip> cardClips;
    public AudioSource audioSource;
   
    public void PlayButtonSFX(int index) {
        Debug.Log("Clicked");
        audioSource.PlayOneShot(buttonClips[index]);
    }

    public void PlayHoverSFX(int hoverSfxIndex) {
        audioSource.PlayOneShot(hoverClips[hoverSfxIndex]);
    }

    public void PlayRandomCardClip() {
        audioSource.PlayOneShot(GetRandomCardClip());
    }

    private AudioClip GetRandomCardClip() {
        int index = Random.Range(0, cardClips.Count);
        return cardClips[index];
    }
}
