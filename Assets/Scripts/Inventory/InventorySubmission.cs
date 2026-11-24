using UnityEngine;
using UnityEngine.Events;

// 挂在需要交物品的交互点，由 PlayerHit 的 onInteract 调用。
public class InventorySubmission : MonoBehaviour
{
    [SerializeField] private InventoryController inventory;
    [SerializeField] private InventoryItem requiredItem;
    [SerializeField] private string prompt = "选择要提交的物品";
    [SerializeField] private bool consumeItem = true;
    [SerializeField] private UnityEvent onAccepted = new UnityEvent();

    public InventoryItem RequiredItem => requiredItem;
    public string Prompt => string.IsNullOrWhiteSpace(prompt) ? "选择要提交的物品" : prompt;
    public bool ConsumeItem => consumeItem;

    // 进入提交模式，成功前允许重新选择或取消。
    public void Open()
    {
        if (isActiveAndEnabled && inventory != null && requiredItem != null)
        {
            inventory.TryOpenSubmission(this);
        }
    }

    // 背包关闭并交还控制权后，再执行机关或剧情事件。
    public void NotifyAccepted()
    {
        onAccepted.Invoke();
    }
}
