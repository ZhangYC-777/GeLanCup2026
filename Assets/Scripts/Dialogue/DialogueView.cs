using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

// 接收播放器给出的说话人、文字和选项，更新场景中的对话界面。
public class DialogueView : MonoBehaviour
{
    private const float ChoiceWidth = 680f;
    private const float ChoicePadding = 48f;
    private const float ChoiceFooterHeight = 68f;

    [Serializable]
    private class SpeakerPortrait
    {
        // 与剧情 JSON 的 speaker 一致。
        public string speaker;
        // 立绘使用对应 Image 的 RectTransform 布局。
        public Image image;
    }

    // 可以整体开关的对话面板物体。
    [SerializeField] private GameObject panel;
    // 显示说话人的 Text 组件。
    [SerializeField] private Text speaker;
    // 显示打字机正文的 Text 组件。
    [SerializeField] private Text body;
    // 显示选项列表与高亮前缀的 Text 组件。
    [SerializeField] private Text choices;
    // 显示继续、选择和确认操作提示的 Text 组件。
    [SerializeField] private Text hint;
    // 场景可以按说话人名称绑定立绘，也可以留空。
    [SerializeField] private SpeakerPortrait[] portraits = Array.Empty<SpeakerPortrait>();

    private RectTransform choicePanel;

    // 初始化对话字体和面板可见状态。
    private void Awake()
    {
        // 测试界面使用系统字体显示中文。也可以在 Inspector 指定项目自己的字体。
        if (body.font == null || !body.font.HasCharacter('剧'))
        {
            Font font = FindDialogueFont();
            if (font != null)
            {
                speaker.font = font;
                body.font = font;
                choices.font = font;
                hint.font = font;
            }
        }

        Hide();
    }

    // name 是新一句的说话人。清空上一句文字和选项后显示面板。
    public void ShowLine(string name)
    {
        speaker.text = name;
        body.text = "";
        HideChoices();
        hint.enabled = true;
        hint.text = "F  继续";
        ShowPortrait(name);
        panel.SetActive(true);
    }

    // text 是目前可见的正文，可能是打字中的部分内容。
    public void SetBody(string text)
    {
        body.text = text;
    }

    // options 是本句选项，selected 是要显示高亮的选项编号。
    public void ShowChoices(DialogueChoice[] options, int selected)
    {
        if (choicePanel == null)
        {
            CreateChoicePanel();
        }

        StringBuilder content = new StringBuilder();
        for (int i = 0; i < options.Length; i++)
        {
            if (i > 0)
            {
                content.AppendLine();
            }

            content.Append(i == selected ? ">  " : "    ");
            content.Append(options[i].text);
        }

        choices.text = content.ToString();
        choicePanel.sizeDelta = new Vector2(ChoiceWidth, Mathf.Clamp(choices.preferredHeight + ChoiceFooterHeight + 24f, 160f, 440f));
        hint.enabled = false;
        choicePanel.gameObject.SetActive(true);
    }

    // 把现有选项文字移到全屏 Canvas 下，兼容场景中独立配置的对话界面。
    private void CreateChoicePanel()
    {
        var card = new GameObject("Dialogue Choice Panel", typeof(RectTransform), typeof(Image), typeof(Outline));
        choicePanel = (RectTransform)card.transform;
        choicePanel.SetParent(GetComponentInParent<Canvas>().transform, false);
        choicePanel.anchorMin = choicePanel.anchorMax = new Vector2(.5f, .5f);
        choicePanel.anchoredPosition = Vector2.zero;
        choicePanel.sizeDelta = new Vector2(ChoiceWidth, 160f);

        Image background = card.GetComponent<Image>();
        background.color = new Color32(22, 19, 22, 250);
        background.raycastTarget = false;
        Outline border = card.GetComponent<Outline>();
        border.effectColor = new Color32(155, 121, 81, 255);
        border.effectDistance = new Vector2(2f, 2f);
        border.useGraphicAlpha = false;

        RectTransform options = choices.rectTransform;
        options.SetParent(choicePanel, false);
        options.anchorMin = Vector2.zero;
        options.anchorMax = Vector2.one;
        options.offsetMin = new Vector2(ChoicePadding, ChoiceFooterHeight);
        options.offsetMax = new Vector2(-ChoicePadding, -24f);
        choices.alignment = TextAnchor.MiddleLeft;
        choices.lineSpacing = 1.35f;
        choices.resizeTextForBestFit = true;
        choices.resizeTextMinSize = 18;
        choices.resizeTextMaxSize = choices.fontSize;
        choices.color = new Color32(248, 229, 200, 255);

        Text controls = Instantiate(hint, choicePanel, false);
        controls.name = "Choice Controls";
        controls.enabled = true;
        controls.alignment = TextAnchor.MiddleCenter;
        controls.color = new Color32(216, 177, 126, 255);
        controls.text = "↑ / ↓  选择      Enter  确认";
        RectTransform footer = controls.rectTransform;
        footer.anchorMin = Vector2.zero;
        footer.anchorMax = Vector2.right;
        footer.offsetMin = new Vector2(ChoicePadding, 14f);
        footer.offsetMax = new Vector2(-ChoicePadding, 54f);
    }

    private void HideChoices()
    {
        if (choicePanel != null)
        {
            choicePanel.gameObject.SetActive(false);
        }
    }

    // 隐藏对话面板和立绘。
    public void Hide()
    {
        HideChoices();
        panel.SetActive(false);
        ShowPortrait(null);
    }

    // 每句只显示当前说话人的立绘，旁白和未配置的说话人清空立绘。
    private void ShowPortrait(string name)
    {
        string speakerName = name?.Trim();
        foreach (SpeakerPortrait portrait in portraits)
        {
            if (portrait?.image != null)
            {
                portrait.image.enabled = !string.IsNullOrEmpty(speakerName) && portrait.speaker == speakerName;
            }
        }
    }

    // 依次试用常见中文字体，Windows 和 macOS 可以各用自己的系统字体。
    internal static Font FindDialogueFont()
    {
        // 按照候选顺序选择能够显示中文的系统字体。
        string[] names = { "PingFang SC", "Hiragino Sans GB", "Microsoft YaHei", "SimHei", "Noto Sans CJK SC" };
        foreach (string name in names)
        {
            Font font = Font.CreateDynamicFontFromOSFont(name, 28);
            if (font != null && font.HasCharacter('剧'))
            {
                return font;
            }

            // 当前字体用不了就释放，再尝试下一个。
            if (font != null)
            {
                Destroy(font);
            }
        }

        return null;
    }
}
