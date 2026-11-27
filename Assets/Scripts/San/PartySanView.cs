using UnityEngine;
using UnityEngine.UI;

// 使用地牢地砖上的符印呈现共享 SAN。
[RequireComponent(typeof(Text))]
public class PartySanView : MonoBehaviour
{
    [SerializeField] private PartySan partySan;

    private Text label;
    [SerializeField] private Text deltaLabel;
    [SerializeField] private Image bar;
    [SerializeField] private RawImage mark;
    private int targetSan;
    private int maxSan;
    private int shownSan = -1;
    private float displayedSan;
    private float deltaUntil;

    private void Awake()
    {
        label = GetComponent<Text>();
    }

    // 订阅变化通知，并立即读取一次当前值。重新打开 HUD 时也会同步。
    private void OnEnable()
    {
        if (partySan != null)
        {
            partySan.SanChanged += OnSanChanged;
            displayedSan = partySan.CurrentSan;
            targetSan = partySan.CurrentSan;
            maxSan = partySan.MaxSan;
            shownSan = -1;
            Refresh();
        }
    }

    // 关闭 HUD 时解除订阅。
    private void OnDisable()
    {
        if (partySan != null)
        {
            partySan.SanChanged -= OnSanChanged;
        }
    }

    private void OnSanChanged(int current, int maximum)
    {
        if (deltaLabel != null && Mathf.Abs(current - targetSan) >= PartySan.StatueRecovery)
        {
            int change = current - targetSan;
            deltaLabel.text = change.ToString("+0;-0");
            deltaLabel.color = change < 0 ? GameUiTheme.Danger : GameUiTheme.Vital;
            deltaUntil = Time.unscaledTime + 1.2f;
        }
        targetSan = current;
        maxSan = maximum;
        if (bar == null)
        {
            displayedSan = targetSan;
        }
        Refresh();
    }

    private void Update()
    {
        if (bar == null)
        {
            return;
        }

        displayedSan = Mathf.MoveTowards(displayedSan, targetSan, 150f * Time.unscaledDeltaTime);
        if (deltaLabel != null)
        {
            deltaLabel.enabled = Time.unscaledTime < deltaUntil;
        }
        Refresh();
    }

    private void Refresh()
    {
        int value = Mathf.RoundToInt(displayedSan);
        if (value != shownSan)
        {
            label.text = bar == null ? $"SAN  {value} / {maxSan}" : $"{value} / {maxSan}";
            shownSan = value;
        }
        if (bar != null)
        {
            float ratio = displayedSan / Mathf.Max(1, maxSan);
            bar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
            bar.color = Color.Lerp(GameUiTheme.Danger, GameUiTheme.Vital, Mathf.InverseLerp(.25f, .55f, ratio));
            if (mark != null)
            {
                mark.color = ratio < .35f ? GameUiTheme.Danger : GameUiTheme.Silver;
            }
            label.color = ratio < .35f ? GameUiTheme.Danger : GameUiTheme.Paper;
        }
    }
}
