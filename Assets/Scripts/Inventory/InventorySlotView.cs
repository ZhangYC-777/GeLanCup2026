using UnityEngine;
using UnityEngine.UI;

// 显示一个物品格，空格仍可高亮。
public class InventorySlotView : MonoBehaviour
{
    [SerializeField] private GameObject selection;
    [SerializeField] private Image icon;
    [SerializeField] private Text label;

    // 测试场景和背包详情共用中文字体。
    public void SetFont(Font font)
    {
        label.font = font;
    }

    // 根据物品和选中状态刷新格子内容。
    public void Show(InventoryItem item, bool selected)
    {
        selection.SetActive(selected);
        icon.sprite = item != null ? item.Icon : null;
        icon.enabled = icon.sprite != null;
        label.text = item != null ? item.DisplayName : "";
    }
}
