using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class PackHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
    [Header("Rotation")]
    public Vector3 hoverRotation = new Vector3(16f, 16f, 2f);
    public float transitionSpeed = 10f;

    [Header("Audio")]
    public List<AudioClip> crinkleClips;
    public AudioSource audioSource;
    [Range(0f, 1f)] public float volume = 0.5f;

    private Vector3 targetRotation;

    void Start() {
        targetRotation = Vector3.zero;

        // Create AudioSource if not assigned
        if (audioSource == null) {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        audioSource.playOnAwake = false;
    }

    void Update() {
        transform.localRotation = Quaternion.Lerp(
            transform.localRotation,
            Quaternion.Euler(targetRotation),
            Time.deltaTime * transitionSpeed
        );
    }

    public void OnPointerEnter(PointerEventData eventData) {
        targetRotation = hoverRotation;
        PlayRandomCrinkle();
    }

    public void OnPointerExit(PointerEventData eventData) {
        targetRotation = Vector3.zero;
    }

    private void PlayRandomCrinkle() {
        if (crinkleClips == null || crinkleClips.Count == 0 || audioSource == null) return;

        AudioClip clip = crinkleClips[Random.Range(0, crinkleClips.Count)];
        audioSource.PlayOneShot(clip, volume);
    }
}
