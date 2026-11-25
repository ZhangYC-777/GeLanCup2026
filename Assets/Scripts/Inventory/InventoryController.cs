using System;
using System.Collections.Generic;
using UnityEngine;

// 当前场景的角色共用一个背包。负责物品记录、键盘操作和提交。
[DefaultExecutionOrder(-10)]
public class InventoryController : MonoBehaviour
{
    [SerializeField] private SwitchPlayer switchPlayer;
    [SerializeField] private InventoryView view;
    // 剧情和读档共用的物品目录。ID 应唯一，发布后应保持稳定。
    [SerializeField] private InventoryItem[] itemCatalog = new InventoryItem[0];
    [SerializeField, Min(0.1f)] private float rewardSeconds = 2.5f;

    private readonly List<InventoryItem> items = new List<InventoryItem>();
    private InventorySubmission submission;
    private int selectedIndex;
    private int openedFrame;
    private int closedFrame = -1;
    private bool isOpen;
    private float rewardUntil;

    // 打开背包时接管 F，关闭当帧也不让同一次 F 传给对话。
    public bool BlocksDialogueInput => isOpen || Time.frameCount == closedFrame;

    // 按背包格子的顺序记录物品 ID。
    internal string[] CaptureItemIds()
    {
        string[] ids = new string[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            ids[i] = items[i].ItemId;
        }
        return ids;
    }

    // 读档时直接还原背包内容，保持原来的顺序和唯一物品规则。
    internal void RestoreItemIds(string[] ids)
    {
        items.Clear();
        foreach (string id in ids)
        {
            InventoryItem item = FindItemDefinition(id);
            if (item == null)
            {
                Debug.LogWarning("物品目录中找不到存档物品：" + id, this);
                continue;
            }

            if (!items.Contains(item))
            {
                items.Add(item);
            }
        }
    }

    // 剧情自然结束时调用。同一物品在背包里只占一个格子。
    public bool TryGrant(string itemId)
    {
        if (!isActiveAndEnabled || view == null || string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        InventoryItem item = FindItemDefinition(itemId);
        if (item == null)
        {
            return false;
        }

        if (items.Contains(item))
        {
            return true;
        }

        items.Add(item);
        if (isOpen)
        {
            Refresh();
        }

        view.ShowReward(item);
        rewardUntil = Time.unscaledTime + rewardSeconds;
        return true;
    }

    // 由场景交互点请求打开提交界面。
    public bool TryOpenSubmission(InventorySubmission target)
    {
        return target != null && TryOpen(target);
    }

    // 背包打开后，方向键只移动高亮，F 只用于确认提交。
    private void Update()
    {
        if (rewardUntil > 0f && Time.unscaledTime >= rewardUntil)
        {
            view.HideReward();
            rewardUntil = 0f;
        }

        if (!isOpen)
        {
            if (switchPlayer != null && switchPlayer.WasInventoryPressed())
            {
                TryOpen(null);
            }
            return;
        }

        if (Time.frameCount == openedFrame)
        {
            return;
        }

        if (switchPlayer.WasInventoryCancelPressed() || switchPlayer.WasInventoryPressed())
        {
            Close();
            return;
        }

        MoveSelection(switchPlayer.ReadInventoryNavigation());
        if (submission != null && switchPlayer.WasInteractPressed())
        {
            SubmitSelection();
        }
    }

    // 停用背包时归还自己持有的角色控制权。
    private void OnDisable()
    {
        if (isOpen)
        {
            Close();
        }

        if (rewardUntil > 0f && view != null)
        {
            view.HideReward();
            rewardUntil = 0f;
        }
    }

    // 对话或其他面板已锁定角色时，等待对方归还控制权。
    private bool TryOpen(InventorySubmission target)
    {
        if (!isActiveAndEnabled || isOpen || switchPlayer == null || switchPlayer.CurrentPlayer == null ||
            switchPlayer.IsGameplayLocked || view == null || view.SlotCount == 0)
        {
            return false;
        }

        submission = target;
        selectedIndex = 0;
        openedFrame = Time.frameCount;
        isOpen = true;
        switchPlayer.SetGameplayLocked(true);
        view.Open(target != null ? target.Prompt : null);
        Refresh();
        return true;
    }

    // 水平移动限制在当前行，向下可移动到下一页。
    private void MoveSelection(Vector2Int direction)
    {
        int columns = view.Columns;
        int column = selectedIndex % columns;
        int nextColumn = Mathf.Clamp(column + direction.x, 0, columns - 1);
        int pageCount = Mathf.Max(1, (items.Count + view.SlotCount - 1) / view.SlotCount);
        int next = selectedIndex + nextColumn - column + direction.y * columns;
        if (next < 0 || next >= pageCount * view.SlotCount || next == selectedIndex)
        {
            return;
        }

        selectedIndex = next;
        view.SetStatus("");
        Refresh();
    }

    // 提交错误时停在背包。正确时按配置扣除物品，再触发交互。
    private void SubmitSelection()
    {
        if (selectedIndex >= items.Count)
        {
            view.SetStatus(items.Count == 0 ? "背包里还没有可提交的物品。" : "请先选中一个物品。");
            return;
        }

        if (items[selectedIndex] != submission.RequiredItem)
        {
            view.SetStatus("这件物品无法用于这里。");
            return;
        }

        InventorySubmission accepted = submission;
        if (accepted.ConsumeItem)
        {
            items.RemoveAt(selectedIndex);
        }

        Close();
        accepted.NotifyAccepted();
    }

    private void Refresh()
    {
        view.ShowItems(items, selectedIndex);
    }

    // 清除提交上下文，下一次按 B 恢复普通浏览模式。
    private void Close()
    {
        isOpen = false;
        submission = null;
        closedFrame = Time.frameCount;
        view.Hide();
        if (switchPlayer != null)
        {
            switchPlayer.SetGameplayLocked(false);
        }
    }

    // 与剧情节点 ID 一样，物品 ID 区分大小写。
    private InventoryItem FindItemDefinition(string itemId)
    {
        InventoryItem found = null;
        foreach (InventoryItem item in itemCatalog)
        {
            if (item == null || !string.Equals(item.ItemId, itemId, StringComparison.Ordinal))
            {
                continue;
            }

            if (found != null && found != item)
            {
                return null;
            }

            found = item;
        }

        return found;
    }
}
