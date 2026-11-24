using UnityEngine;

// 项目中的物品定义，剧情和提交点共用同一份资产。
[CreateAssetMenu(menuName = "背包/物品", fileName = "New Item")]
public class InventoryItem : ScriptableObject
{
    // 与剧情配置器里的「获得物品 ID」一致。
    [SerializeField] private string itemId;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;
    [SerializeField, TextArea] private string description;

    public string ItemId => itemId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public Sprite Icon => icon;
    public string Description => description;
}
