using System;
using UnityEngine;
using UnityEngine.UI;

// 接收播放器给出的说话人、文字和选项，更新场景中的对话界面。
public class DialogueView : MonoBehaviour
{
    [Serializable]
    private class SpeakerPortrait
    {
        // 与剧情 JSON 的 speaker 一致。
        public string speaker;
        // 立绘使用对应 Image 的 RectTransform 布局。
        public Image image;
        // 可选的惊恐立绘。普通立绘仍由 Image 的 Sprite 提供。
        public Sprite distressedSprite;
    }

    // 可以整体开关的对话面板物体。
    [SerializeField] private GameObject panel;
    // 显示说话人的 Text 组件。
    [SerializeField] private Text speaker;
    // 显示打字机正文的 Text 组件。
    [SerializeField] private Text body;
    // 显示继续、选择和确认操作提示的 Text 组件。
    [SerializeField] private Text hint;
    // 场景可以按说话人名称绑定立绘，也可以留空。
    [SerializeField] private SpeakerPortrait[] portraits = Array.Empty<SpeakerPortrait>();

    private DialogueChoicesView choiceList;
    private Font fallbackFont;
    [SerializeField] private RectTransform nameplate;

    // 初始化对话字体和面板可见状态。
    private void Awake()
    {
        // 测试界面使用系统字体显示中文。也可以在 Inspector 指定项目自己的字体。
        if (body.font == null || !body.font.HasCharacter('剧'))
        {
            fallbackFont = FindDialogueFont();
            if (fallbackFont != null)
            {
                speaker.font = body.font = hint.font = fallbackFont;
            }
        }

        Hide();
    }

    private void OnDestroy()
    {
        if (fallbackFont != null)
        {
            Destroy(fallbackFont);
        }
    }

    // name 是新一句的说话人。清空上一句文字和选项后显示面板。
    public void ShowLine(string name, bool distressedPortrait = false)
    {
        speaker.text = name;
        nameplate.gameObject.SetActive(!string.IsNullOrWhiteSpace(name));
        body.text = "";
        choiceList?.Hide();
        hint.enabled = true;
        hint.text = "F  继续";
        ShowPortrait(name, distressedPortrait);
        panel.SetActive(true);
    }

    // text 是目前可见的正文，可能是打字中的部分内容。
    public void SetBody(string text)
    {
        body.text = text;
    }

    internal void ShowChoices(DialogueChoice[] options, Action<int> select)
    {
        if (choiceList == null)
        {
            choiceList = new DialogueChoicesView(GetComponentInParent<Canvas>(), body.font);
        }

        hint.enabled = false;
        choiceList.Show(options, select);
    }

    internal void HighlightChoice(int index)
    {
        choiceList.Highlight(index);
    }

    // 隐藏对话面板和立绘。
    public void Hide()
    {
        choiceList?.Hide();
        panel.SetActive(false);
        ShowPortrait(null, false);
    }

    // 每句只显示当前说话人的立绘，旁白和未配置的说话人清空立绘。
    private void ShowPortrait(string name, bool distressedPortrait)
    {
        string speakerName = name?.Trim();
        foreach (SpeakerPortrait portrait in portraits)
        {
            if (portrait?.image == null)
            {
                continue;
            }

            bool visible = !string.IsNullOrEmpty(speakerName) && portrait.speaker == speakerName;
            portrait.image.overrideSprite = visible && distressedPortrait ? portrait.distressedSprite : null;
            portrait.image.enabled = visible;
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
