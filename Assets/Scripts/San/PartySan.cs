using System;
using UnityEngine;
using UnityEngine.Events;

// 全队共用一份 SAN。三个角色和场景交互点引用同一个组件。
[DisallowMultipleComponent]
public class PartySan : MonoBehaviour, IHealth
{
    [SerializeField, Min(1)] private int maxSan = 100;
    [SerializeField, Min(0)] private int startingSan = 100;

    // 从正数降到 0 时触发，关卡可以在 Inspector 绑定失败流程。
    [SerializeField] private UnityEvent onDepleted = new UnityEvent();

    // 第一次修改前直接读取配置的初始值。
    private int? currentSan;

    // SAN 实际变化后通知界面，参数依次是当前值和上限。
    public event Action<int, int> SanChanged;
    internal event Action Depleted;

    public int MaxSan
    {
        get { return Mathf.Max(1, maxSan); }
    }

    public int CurrentSan
    {
        get { return currentSan ?? Mathf.Clamp(startingSan, 0, MaxSan); }
    }

    // 扣除正数点数，扣完最低为 0。
    public void TakeDamage(int amount)
    {
        if (amount > 0)
        {
            SetSan(Mathf.Max(0, CurrentSan - amount));
        }
    }

    // 恢复正数点数，最多恢复至上限。此方法可直接绑定到 PlayerHit 的 On Interact。
    public void Heal(int amount)
    {
        if (amount > 0)
        {
            int current = CurrentSan;
            SetSan(current + Mathf.Min(amount, MaxSan - current));
        }
    }

    // 读档同步 SAN 和界面，耗尽事件只用于游玩中扣至 0 的那一刻。
    internal void RestoreSan(int savedSan)
    {
        SetSan(Mathf.Clamp(savedSan, 0, MaxSan), notifyDepleted: false);
    }

    private void SetSan(int nextSan, bool notifyDepleted = true)
    {
        if (nextSan == CurrentSan)
        {
            return;
        }

        currentSan = nextSan;
        SanChanged?.Invoke(nextSan, MaxSan);

        if (notifyDepleted && nextSan == 0)
        {
            onDepleted.Invoke();
            Depleted?.Invoke();
        }
    }
}
