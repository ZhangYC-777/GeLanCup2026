using System;
using UnityEngine;
using UnityEngine.UI;

// 显示解谜失败界面和密码门的三枚转盘。
[DisallowMultipleComponent]
public sealed class PuzzleRoomOverlay : MonoBehaviour
{
    [Serializable]
    private sealed class Dial
    {
        public Text digit;
        public GameObject selection;
        public RectTransform wheel;
    }

    [SerializeField] private GameObject passwordPanel;
    [SerializeField] private Text passwordFeedback;
    [SerializeField] private Dial[] dials;
    [SerializeField] private GameObject failurePanel;
    [SerializeField] private Text[] wallWriting;

    private Font font;

    private void Awake()
    {
        font = DialogueView.FindDialogueFont();
        if (font != null)
        {
            ApplyFont(passwordPanel);
            ApplyFont(failurePanel);
            foreach (Text writing in wallWriting)
            {
                writing.font = font;
            }
        }

        HidePassword();
        failurePanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (font != null)
        {
            Destroy(font);
        }
    }

    private void ApplyFont(GameObject panel)
    {
        foreach (Text text in panel.GetComponentsInChildren<Text>(true))
        {
            text.font = font;
        }
    }

    public void ShowPassword(char[] digits, int selected, string feedback, bool rejected)
    {
        for (int i = 0; i < dials.Length; i++)
        {
            Dial dial = dials[i];
            dial.digit.text = digits[i].ToString();
            dial.digit.color = i == selected ? GameUiTheme.Silver : GameUiTheme.Muted;
            dial.selection.SetActive(i == selected);
            dial.wheel.localRotation = Quaternion.Euler(0f, 0f, (digits[i] - '0') * -36f);
        }

        passwordFeedback.text = feedback;
        passwordFeedback.color = rejected ? GameUiTheme.Danger : GameUiTheme.Muted;
        passwordPanel.SetActive(true);
    }

    public void HidePassword()
    {
        passwordPanel.SetActive(false);
    }

    public void ShowFailure()
    {
        failurePanel.SetActive(true);
    }
}
