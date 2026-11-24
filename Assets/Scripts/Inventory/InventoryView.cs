using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 只负责展示，格子和文字都可以直接在场景里调整。
public class InventoryView : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private InventorySlotView[] slots = new InventorySlotView[0];
    [SerializeField, Min(1)] private int columns = 4;
    [SerializeField] private Text title;
    [SerializeField] private Text itemName;
    [SerializeField] private Text description;
    [SerializeField] private Text pageLabel;
    [SerializeField] private Text controls;
    [SerializeField] private Text status;
    [SerializeField] private GameObject rewardToast;
    [SerializeField] private Image rewardIcon;
    [SerializeField] private Text rewardText;

    private Font fallbackFont;

    public int Columns => columns;
    public int SlotCount => slots.Length;

    // 初始化显示状态和中文字体。
    private void Awake()
    {
        if (title.font == null || !title.font.HasCharacter('背'))
        {
            fallbackFont = DialogueView.FindDialogueFont();
            if (fallbackFont != null)
            {
                title.font = itemName.font = description.font = pageLabel.font =
                    controls.font = status.font = rewardText.font = fallbackFont;
                foreach (InventorySlotView slot in slots)
                {
                    slot.SetFont(fallbackFont);
                }
            }
        }

        Hide();
        HideReward();
    }

    // 销毁由当前视图创建的系统字体。
    private void OnDestroy()
    {
        if (fallbackFont != null)
        {
            Destroy(fallbackFont);
        }
    }

    // 展开普通背包或提交界面。
    public void Open(string submissionPrompt)
    {
        bool isSubmitting = !string.IsNullOrEmpty(submissionPrompt);
        title.text = isSubmitting ? submissionPrompt : "背包";
        controls.text = isSubmitting ? "方向键  选择     F  提交     Esc  取消" : "方向键  选择     B / Esc  关闭";
        SetStatus("");
        panel.SetActive(true);
    }

    // 显示当前页和选中物品的详情。
    public void ShowItems(IReadOnlyList<InventoryItem> items, int selectedIndex)
    {
        int page = selectedIndex / SlotCount;
        int firstIndex = page * SlotCount;
        for (int i = 0; i < SlotCount; i++)
        {
            int index = firstIndex + i;
            slots[i].Show(index < items.Count ? items[index] : null, index == selectedIndex);
        }

        int pageCount = Mathf.Max(1, (items.Count + SlotCount - 1) / SlotCount);
        pageLabel.text = pageCount > 1 ? $"{page + 1} / {pageCount}" : "";
        InventoryItem selected = selectedIndex < items.Count ? items[selectedIndex] : null;
        itemName.text = selected != null ? selected.DisplayName : items.Count == 0 ? "背包为空" : "空格";
        description.text = selected != null ? selected.Description : "";
    }

    public void SetStatus(string message)
    {
        status.text = message;
    }

    public void Hide()
    {
        panel.SetActive(false);
    }

    // 获得物品时只显示短提示，背包保持当前状态。
    public void ShowReward(InventoryItem item)
    {
        rewardIcon.sprite = item.Icon;
        rewardIcon.enabled = item.Icon != null;
        rewardText.text = "获得物品  " + item.DisplayName;
        rewardToast.SetActive(true);
    }

    public void HideReward()
    {
        rewardToast.SetActive(false);
    }
}
