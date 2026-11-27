using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 新游戏开始时，在黑屏上逐字播放引言，再进入漂流瓶序章。
[DisallowMultipleComponent]
public class OpeningQuote : MonoBehaviour
{
    internal const string ScenePath = SceneSave.MainStoryDirectory + "scene-0.5.unity";
    private const string ProloguePath = SceneSave.MainStoryDirectory + "Scene0.unity";
    private static readonly string[] Lines =
    {
        "人心的贪欲是看不见的黑洞，永远填不满。",
        "得到一点，就想要更多",
        "拥有了又害怕失去，于是不断伸手去掠夺不属于自己的东西。",
        "贪心会蒙住双眼，让你看不清底线，分不清善恶。",
        "为了追逐虚妄的欲望，丢掉良心，情义与本心",
        "所有靠贪婪换来的一切都是短暂的泡沫，来的快，消散也快。",
        "越贪婪，越容易被欲望裹挟，在无休止的索取里越陷越深。",
        "最后耗尽所有，落得一场空……"
    };

    [SerializeField] private Text caption;
    [SerializeField] private Text continueHint;
    [SerializeField, Min(1f)] private float charactersPerSecond = 25f;
    private Font quoteFont;
    private int lineIndex;
    private int openedFrame;
    private int[] characterStarts;
    private int visibleCharacters;
    private float typingStarted;
    private bool loading;

    private void Start()
    {
        quoteFont = DialogueView.FindDialogueFont();
        if (quoteFont != null)
        {
            caption.font = continueHint.font = quoteFont;
        }
        caption.color = GameUiTheme.Paper;
        caption.supportRichText = true;
        continueHint.color = GameUiTheme.Muted;
        StartLine();
    }

    private void Update()
    {
        // 进入场景的第一帧不消费主菜单的确认输入。
        if (loading || Time.frameCount == openedFrame)
        {
            return;
        }

        bool confirmPressed = MenuInput.WasConfirmPressed();
        if (visibleCharacters < characterStarts.Length)
        {
            // 打字期间按 F 只补全本句。本句播完后才接受下一次推进。
            int count = confirmPressed ? characterStarts.Length :
                Mathf.Min(characterStarts.Length, Mathf.FloorToInt((Time.unscaledTime - typingStarted) * charactersPerSecond));
            if (count > visibleCharacters)
            {
                Reveal(count);
            }
            return;
        }

        if (!confirmPressed)
        {
            return;
        }

        if (lineIndex + 1 < Lines.Length)
        {
            lineIndex++;
            StartLine();
            return;
        }

        loading = true;
        OpenPrologue();
    }

    private void StartLine()
    {
        characterStarts = StringInfo.ParseCombiningCharacters(Lines[lineIndex]);
        typingStarted = Time.unscaledTime;
        openedFrame = Time.frameCount;
        continueHint.text = "F  补全";
        Reveal(0);
    }

    private void Reveal(int count)
    {
        visibleCharacters = count;
        string line = Lines[lineIndex];
        if (count == characterStarts.Length)
        {
            caption.text = line;
            continueHint.text = "F  继续";
            return;
        }

        // 透明的文字仍然参与布局，使已显示的文字在居中排版时保持原位。
        int split = characterStarts[count];
        caption.text = line.Substring(0, split) + "<color=#FFFFFF00>" + line.Substring(split) + "</color>";
    }

    private void OpenPrologue()
    {
        int prologueIndex = SceneUtility.GetBuildIndexByScenePath(ProloguePath);
        if (prologueIndex < 0)
        {
            Debug.LogError("找不到序章场景，请检查构建列表。", this);
            SceneManager.LoadScene(SceneUtility.GetBuildIndexByScenePath(SceneSave.MainMenuPath));
            return;
        }

        SceneManager.LoadScene(prologueIndex);
    }

    private void OnDestroy()
    {
        if (quoteFont != null)
        {
            Destroy(quoteFont);
        }
    }
}
