using UnityEngine;
using UnityEngine.UI;

// 将共享 SAN 显示在一个普通的 Unity UI Text 上。
[RequireComponent(typeof(Text))]
public class PartySanView : MonoBehaviour
{
    [SerializeField] private PartySan partySan;

    private Text label;

    private void Awake()
    {
        label = GetComponent<Text>();
    }

    // 订阅变化通知，并立即读取一次当前值。重新打开 HUD 时也会同步。
    private void OnEnable()
    {
        if (partySan != null)
        {
            partySan.SanChanged += ShowSan;
            ShowSan(partySan.CurrentSan, partySan.MaxSan);
        }
    }

    // 关闭 HUD 时解除订阅。
    private void OnDisable()
    {
        if (partySan != null)
        {
            partySan.SanChanged -= ShowSan;
        }
    }

    private void ShowSan(int current, int maximum)
    {
        label.text = "SAN  " + current + " / " + maximum;
    }
}
