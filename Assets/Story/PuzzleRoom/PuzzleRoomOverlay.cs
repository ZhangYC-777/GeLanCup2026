using UnityEngine;
using UnityEngine.UI;

// 黑场摸索和输入石门密码时使用原对话字体，演出结束后收起提示。
[DisallowMultipleComponent]
public sealed class PuzzleRoomOverlay : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Text heading;
    [SerializeField] private Text instructions;
    [SerializeField] private Text feedback;
    [SerializeField] private Text[] wallWriting;

    private Font font;

    private void Awake()
    {
        font = DialogueView.FindDialogueFont();
        if (font != null)
        {
            heading.font = instructions.font = feedback.font = font;
            foreach (Text writing in wallWriting)
            {
                writing.font = font;
            }
        }

        Hide();
    }

    private void OnDestroy()
    {
        if (font != null)
        {
            Destroy(font);
        }
    }

    public void Show(string title, string hint, string status)
    {
        heading.text = title;
        instructions.text = hint;
        feedback.text = status;
        panel.SetActive(true);
    }

    public void Hide()
    {
        panel.SetActive(false);
    }
}
