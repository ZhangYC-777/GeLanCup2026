using System;
using System.Collections.Generic;
using UnityEngine;

// JSON 文件的根对象，对应默认入口和台词列表。
[Serializable]
public class DialogueData
{
    // 配置器从入口预览时的首句 ID，也是触发器未指定入口时的默认值。
    public string entry;
    // 文件里的所有台词节点，按各自的 ID 查找。
    public DialogueNode[] nodes;
}

// 一条台词和它的出口。
[Serializable]
public class DialogueNode
{
    // 节点的唯一名称，用于引用和跳转。
    public string id;
    // 对话面板显示的说话人。
    public string speaker;
    // 台词正文。
    public string text;
    // 本句使用说话人的惊恐立绘，未填写时使用普通立绘。
    public bool distressedPortrait;
    // 普通台词的下一个节点 ID，留空时结束本段。
    public string next;
    // 玩家可以选择的回应。有选项时由选项各自的 next 决定后续。
    public DialogueChoice[] choices;
    // 播完本段最后一句时发放的物品 ID，留空表示没有奖励。
    public string rewardItemId;
}

// 玩家可选的一个回应。
[Serializable]
public class DialogueChoice
{
    // 选项列表里显示的文字。
    public string text;
    // 选择此项后跳转到的节点 ID，留空时结束本段。
    public string next;
    // 选中后可写入一项剧情状态，按需填写。
    public DialogueStateChange set;
}

// 一条剧情状态的写入指令。
[Serializable]
public class DialogueStateChange
{
    // 剧情状态名称，同一组选择可以共用这个名称。
    public string key;
    // 保存的值，后续触发器会用这个值匹配路径。
    public string value;
}

// 运行时和配置器共用同一套格式检查。
public static class DialogueLoader
{
    // text 是未经解析的 JSON。
    // 成功时 out dialogue 返回剧情数据，out nodes 返回节点字典；失败时 out error 说明原因。
    public static bool TryLoad(string text, out DialogueData dialogue,
        out Dictionary<string, DialogueNode> nodes, out string error)
    {
        dialogue = null;
        nodes = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "剧情文件为空。";
            return false;
        }

        // 交给 Unity 反序列化，将格式异常转换成清楚的错误提示。
        try
        {
            dialogue = JsonUtility.FromJson<DialogueData>(text);
        }
        catch (ArgumentException exception)
        {
            error = "JSON 格式错误。 " + exception.Message;
            return false;
        }

        return TryIndex(dialogue, out nodes, out error);
    }

    // 先登记全部节点，再检查后续节点和选项能否跳转。
    // dialogue 是待检查的剧情。out nodes 返回节点字典，out error 返回检查失败的原因。
    public static bool TryIndex(DialogueData dialogue, out Dictionary<string, DialogueNode> nodes, out string error)
    {
        // 用区分大小写的节点 ID 建立索引。
        nodes = new Dictionary<string, DialogueNode>(StringComparer.Ordinal);
        if (dialogue == null || dialogue.nodes == null || dialogue.nodes.Length == 0)
        {
            error = "剧情至少需要一句台词。";
            return false;
        }

        // 检查每句必填内容和重复 ID，随后登记节点。
        foreach (DialogueNode node in dialogue.nodes)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.id) || string.IsNullOrWhiteSpace(node.text))
            {
                error = "每句台词都需要填写节点 ID 和正文。";
                return false;
            }

            if (nodes.ContainsKey(node.id))
            {
                error = "节点 ID 重复：" + node.id;
                return false;
            }

            nodes.Add(node.id, node);
        }

        // 确认默认入口已经登记。
        if (string.IsNullOrEmpty(dialogue.entry) || !nodes.ContainsKey(dialogue.entry))
        {
            error = "找不到入口节点：" + dialogue.entry;
            return false;
        }

        // 节点全部登记后，再逐个检查跳转和选项。
        foreach (DialogueNode node in dialogue.nodes)
        {
            if (!string.IsNullOrWhiteSpace(node.rewardItemId) &&
                (!string.IsNullOrEmpty(node.next) || (node.choices != null && node.choices.Length > 0)))
            {
                error = node.id + " 有后续台词或选项，请把物品奖励配置到本段最后一句。";
                return false;
            }

            if (!string.IsNullOrEmpty(node.next) && !nodes.ContainsKey(node.next))
            {
                error = node.id + " 跳转到了不存在的节点：" + node.next;
                return false;
            }

            if (node.choices == null || node.choices.Length == 0)
            {
                continue;
            }

            // 有选项时使用每个选项的跳转目标，避免同时配普通跳转。
            if (!string.IsNullOrEmpty(node.next))
            {
                error = node.id + " 已有选项，请清空普通的下一句。";
                return false;
            }

            // 逐项确认文字、跳转以及状态写入格式。
            foreach (DialogueChoice choice in node.choices)
            {
                if (choice == null || string.IsNullOrWhiteSpace(choice.text))
                {
                    error = node.id + " 有一个选项未填写文本。";
                    return false;
                }

                if (!string.IsNullOrEmpty(choice.next) && !nodes.ContainsKey(choice.next))
                {
                    error = node.id + " 的选项跳转到了不存在的节点：" + choice.next;
                    return false;
                }

                if (choice.set != null && string.IsNullOrWhiteSpace(choice.set.key) &&
                    !string.IsNullOrWhiteSpace(choice.set.value))
                {
                    error = node.id + " 的选项填写了状态值，请补上状态名。";
                    return false;
                }
            }
        }

        error = null;
        return true;
    }
}
