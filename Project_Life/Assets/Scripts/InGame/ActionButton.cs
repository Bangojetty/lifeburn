using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ActionButton : MonoBehaviour {
    public GameManager gameManager;
    public ActionButtonType currentButtonType = ActionButtonType.Pass;
    public TMP_Text btnText;
    public Image btnImg;
    public string green;
    public string orange;
    public string blue;

    public void Activate() {
        // always reset attack references when activation action button (you never need them after passing prio)
        gameManager.ResetAttackReferences();
        switch (currentButtonType) {
            case ActionButtonType.Pass:
                gameManager.PassPrio();
                break;
            case ActionButtonType.Attack:
                gameManager.SendAttackToServer();
                break;
            case ActionButtonType.Tribute or ActionButtonType.Cost or ActionButtonType.Target:
                gameManager.DisablesSelectables();
                gameManager.SendSelection();
                break;
            default:
                Debug.Log("ActionButtonType not implemented");
                break;
        }
    }

    public void SetButtonType(ActionButtonType type) {
        switch (type) {
            case ActionButtonType.Attack: 
                SetColor(orange);
                btnText.text = "Attack";
                btnText.fontSize = 44;
                break;
            case ActionButtonType.Pass:
                SetColor(green);
                btnText.text = "Pass";
                btnText.fontSize = 47;
                break;
            case ActionButtonType.Tribute:
                SetColor(blue);
                btnText.text = "Tribute";
                btnText.fontSize = 42;
                break;
            case ActionButtonType.Submit:
                SetColor(green);
                btnText.text = "Submit";
                btnText.fontSize = 44;
                break;
            case ActionButtonType.Target:
                SetColor(blue);
                btnText.text = "Submit";
                btnText.fontSize = 44;
                break;
            case ActionButtonType.Cost:
                SetColor(blue);
                btnText.text = "Submit";
                btnText.fontSize = 44;
                break;
            default:
                Debug.Log("ActionButtonType not implemented");
                break;
        }
        currentButtonType = type;
    }

    private void SetColor(string colorHex) {
        if (!ColorUtility.TryParseHtmlString(colorHex, out var attackColor)) {
            Debug.Log("Error with color hex code for attackBtn color");
        }
        btnImg.color = attackColor;
    }
}


public enum ActionButtonType {
    Pass,
    Attack,
    Tribute,
    Submit,
    Target,
    Cost
}
