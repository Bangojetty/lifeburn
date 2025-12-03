using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ColliderButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Sprite normalSprite;
    public Sprite hoverSprite;
    public Sprite pressedSprite;

    public UnityEvent onClick;

    public Image buttonImage;

    private void Awake()
    {
        // Ensure we start with the normal sprite
        if (buttonImage && normalSprite)
        {
            buttonImage.sprite = normalSprite;
        }
    }

    private void OnMouseDown()
    {
        onClick.Invoke();
        if (buttonImage.sprite == hoverSprite) {
            buttonImage.sprite = pressedSprite;
        }
    }

    private void OnMouseUp() {
        buttonImage.sprite = normalSprite;
    }

    public void OnPointerEnter(PointerEventData eventData) {
        Debug.Log("pointer entered object: " + eventData);
        if (buttonImage && hoverSprite)
        {
            buttonImage.sprite = hoverSprite;
        }
    }

    public void OnPointerExit(PointerEventData eventData) {
        Debug.Log("pointer exited object: " + eventData);
        if (buttonImage && normalSprite)
        {
            buttonImage.sprite = normalSprite;
        }
    }
}