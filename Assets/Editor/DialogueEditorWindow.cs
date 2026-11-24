using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

// Unity 编辑器窗口，用于编辑 JSON 剧情和预览当前文件里的台词。
public class DialogueEditorWindow : EditorWindow
{
    // 窗口正在编辑的剧情数据。
    private DialogueData dialogue = new DialogueData();
    // 最近导入或导出的文件路径，用于建议文件名。
    private string filePath;
    // 工具栏下方显示的操作结果或检查错误。
    private string message;
    // 左侧选中台词的数组编号，-1 表示当前没有选择。
    private int selectedIndex = -1;
    // 左侧列表的滚动位置。
    private Vector2 nodeScroll;
    // 右侧编辑面板的滚动位置。
    private Vector2 editorScroll;

    // 预览前生成的节点索引。
    private Dictionary<string, DialogueNode> previewNodes;
    // 预览中正在显示的节点，为 null 时显示编辑界面。
    private DialogueNode previewNode;
    // 各文本单位在台词中的起始位置。
    private int[] previewCharacters;
    // 预览开始打字的编辑器时间。
    private double previewStarted;
    // 当前预览台词是否已经完整显示。
    private bool previewComplete;
    // 预览是否停在选项列表。
    private bool previewChoosing;

    // 将窗口入口挂到 Unity 的 Tools 菜单。
    [MenuItem("Tools/剧情配置器")]
    public static void Open()
    {
        GetWindow<DialogueEditorWindow>("剧情配置器");
    }

    // 绘制编辑窗口。预览时展示预览面板，其余时间展示列表和编辑区。
    private void OnGUI()
    {
        DrawToolbar();
        if (!string.IsNullOrEmpty(message))
        {
            EditorGUILayout.HelpBox(message, MessageType.Info);
        }

        // 预览期间绘制预览内容，并结束本次编辑区绘制。
        if (previewNode != null)
        {
            DrawPreview();
            return;
        }

        if (dialogue.nodes == null)
        {
            dialogue.nodes = new DialogueNode[0];
        }

        // 编辑区域左侧选择台词，右侧编辑入口和当前台词。
        EditorGUILayout.BeginHorizontal();
        DrawNodeList();
        editorScroll = EditorGUILayout.BeginScrollView(editorScroll);
        dialogue.entry = DrawTarget("故事入口", dialogue.entry, "请选择入口");
        if (selectedIndex >= 0 && selectedIndex < dialogue.nodes.Length)
        {
            DrawNode(dialogue.nodes[selectedIndex]);
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndHorizontal();
    }

    // 绘制导入、导出、预览和返回编辑入口。
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("导入 JSON", EditorStyles.toolbarButton))
        {
            Import();
        }

        if (GUILayout.Button("导出 JSON", EditorStyles.toolbarButton))
        {
            Export();
        }

        if (previewNode == null)
        {
            if (GUILayout.Button("从入口预览", EditorStyles.toolbarButton))
            {
                StartPreview(dialogue.entry);
            }

            if (selectedIndex >= 0 && GUILayout.Button("从所选台词预览", EditorStyles.toolbarButton))
            {
                StartPreview(dialogue.nodes[selectedIndex].id);
            }
        }
        else if (GUILayout.Button("返回编辑", EditorStyles.toolbarButton))
        {
            previewNode = null;
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    // 列出已有台词，并处理当前选择的新增和删除。
    private void DrawNodeList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(235));
        nodeScroll = EditorGUILayout.BeginScrollView(nodeScroll);
        for (int i = 0; i < dialogue.nodes.Length; i++)
        {
            DialogueNode node = dialogue.nodes[i];
            GUIStyle style = EditorStyles.miniButton;
            if (i == selectedIndex)
            {
                style = EditorStyles.toolbarButton;
            }

            if (GUILayout.Button(NodeLabel(node), style))
            {
                selectedIndex = i;
            }
        }
        EditorGUILayout.EndScrollView();

        // 为新台词分配 ID，第一次新增时同时设为默认入口。
        if (GUILayout.Button("新增台词"))
        {
            string id = FindNewNodeId();
            DialogueNode node = new DialogueNode();
            node.id = id;
            selectedIndex = dialogue.nodes.Length;
            System.Array.Resize(ref dialogue.nodes, selectedIndex + 1);
            dialogue.nodes[selectedIndex] = node;
            if (string.IsNullOrEmpty(dialogue.entry))
            {
                dialogue.entry = id;
            }
        }

        // 有选中节点时才允许删除，并在删除后调整选中位置。
        EditorGUI.BeginDisabledGroup(selectedIndex < 0);
        if (GUILayout.Button("删除所选台词"))
        {
            List<DialogueNode> nodes = new List<DialogueNode>(dialogue.nodes);
            nodes.RemoveAt(selectedIndex);
            dialogue.nodes = nodes.ToArray();
            selectedIndex = Mathf.Min(selectedIndex, dialogue.nodes.Length - 1);
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndVertical();
    }

    // 生成一个当前文件中尚未使用的初始 ID。
    private string FindNewNodeId()
    {
        int number = dialogue.nodes.Length + 1;
        string id = "line_" + number;
        while (HasNodeId(id))
        {
            number++;
            id = "line_" + number;
        }

        return id;
    }

    // id 是正在检查的候选节点 ID。
    private bool HasNodeId(string id)
    {
        foreach (DialogueNode node in dialogue.nodes)
        {
            if (node.id == id)
            {
                return true;
            }
        }

        return false;
    }

    // node 是右侧当前编辑的台词对象。修改字段会更新窗口内的这份数据。
    private void DrawNode(DialogueNode node)
    {
        node.id = EditorGUILayout.TextField("节点 ID", node.id);
        node.speaker = EditorGUILayout.TextField("说话人", node.speaker);
        EditorGUILayout.LabelField("台词");
        node.text = EditorGUILayout.TextArea(node.text, GUILayout.MinHeight(65));

        if (node.choices == null)
        {
            node.choices = new DialogueChoice[0];
        }

        // 普通台词使用一个下一句下拉框。有选项的台词由各选项决定出口。
        if (node.choices.Length == 0)
        {
            node.next = DrawTarget("下一句", node.next, "本段结束");
        }

        if ((node.choices.Length == 0 && string.IsNullOrEmpty(node.next)) ||
            !string.IsNullOrEmpty(node.rewardItemId))
        {
            node.rewardItemId = EditorGUILayout.TextField(
                new GUIContent("获得物品 ID", "本段剧情自然结束后，将对应物品加入背包。"),
                node.rewardItemId ?? "").Trim();
        }

        // 为每个选项绘制文字、跳转目标和可选的状态写入。
        for (int i = 0; i < node.choices.Length; i++)
        {
            DialogueChoice choice = node.choices[i];
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("选项 " + (i + 1), EditorStyles.boldLabel);
            choice.text = EditorGUILayout.TextField("选项文字", choice.text);
            choice.next = DrawTarget("选中后跳到", choice.next, "本段结束");

            if (choice.set == null)
            {
                choice.set = new DialogueStateChange();
            }

            choice.set.key = EditorGUILayout.TextField("记录的状态名", choice.set.key);
            choice.set.value = EditorGUILayout.TextField("记录的状态值", choice.set.value);
            bool remove = GUILayout.Button("删除此选项");
            EditorGUILayout.EndVertical();
            if (remove)
            {
                List<DialogueChoice> remaining = new List<DialogueChoice>(node.choices);
                remaining.RemoveAt(i);
                node.choices = remaining.ToArray();
                break;
            }
        }

        // 添加分支会清空普通下一句，改由选项决定跳转。
        if (GUILayout.Button("添加选项"))
        {
            node.next = "";
            int index = node.choices.Length;
            System.Array.Resize(ref node.choices, index + 1);
            node.choices[index] = new DialogueChoice();
        }
    }

    // label 是控件标题，current 是当前节点 ID，emptyLabel 是空选择的显示文字。返回选中的 ID。
    private string DrawTarget(string label, string current, string emptyLabel)
    {
        List<string> names = new List<string> { emptyLabel };
        List<string> ids = new List<string> { "" };
        int index = 0;

        foreach (DialogueNode node in dialogue.nodes)
        {
            if (string.IsNullOrWhiteSpace(node.id))
            {
                continue;
            }

            if (node.id == current)
            {
                index = ids.Count;
            }

            ids.Add(node.id);
            names.Add(NodeLabel(node));
        }

        // 保留并标出已失效的引用，便于定位需要重选的跳转。
        if (index == 0 && !string.IsNullOrEmpty(current))
        {
            index = ids.Count;
            ids.Add(current);
            names.Add("找不到节点 " + current);
        }

        return ids[EditorGUILayout.Popup(label, index, names.ToArray())];
    }

    // node 是要生成列表标签的台词，标签包括 ID 和一小段正文。
    private static string NodeLabel(DialogueNode node)
    {
        string summary = node.text;
        if (string.IsNullOrWhiteSpace(summary))
        {
            summary = "空台词";
        }

        summary = summary.Replace('\r', ' ').Replace('\n', ' ');
        if (summary.Length > 16)
        {
            summary = summary.Substring(0, 16) + "…";
        }

        return node.id + " · " + summary;
    }

    // 选取外部 JSON，检查成功后载入编辑区。
    private void Import()
    {
        string path = EditorUtility.OpenFilePanel("导入剧情", Application.dataPath, "json");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        DialogueData imported;
        Dictionary<string, DialogueNode> nodes;
        string error;
        try
        {
            // 读取文件，使用与运行时相同的格式检查。
            if (!DialogueLoader.TryLoad(File.ReadAllText(path), out imported, out nodes, out error))
            {
                message = error;
                return;
            }
        }
        catch (IOException exception)
        {
            message = exception.Message;
            return;
        }

        dialogue = imported;
        filePath = path;
        message = "已导入 " + Path.GetFileName(path);
        selectedIndex = 0;
        previewNode = null;
    }

    // 校验全部节点后，将窗口内的剧情保存成 JSON。
    private void Export()
    {
        Dictionary<string, DialogueNode> nodes;
        // 存在无效引用或缺失字段时，在窗口显示错误并停在编辑状态。
        if (!DialogueLoader.TryIndex(dialogue, out nodes, out message))
        {
            return;
        }

        string suggestedName = "Dialogue";
        if (!string.IsNullOrEmpty(filePath))
        {
            suggestedName = Path.GetFileNameWithoutExtension(filePath);
        }

        string path = EditorUtility.SaveFilePanel("导出剧情", Application.dataPath, suggestedName, "json");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            // 写出易于阅读的缩进 JSON，随后刷新 Unity 资产列表。
            File.WriteAllText(path, JsonUtility.ToJson(dialogue, true));
        }
        catch (IOException exception)
        {
            message = exception.Message;
            return;
        }

        filePath = path;
        message = "已导出 " + Path.GetFileName(path);
        AssetDatabase.Refresh();
    }

    // entry 是这次预览的开头，可以选默认入口或选中的台词。
    private void StartPreview(string entry)
    {
        if (DialogueLoader.TryIndex(dialogue, out previewNodes, out message))
        {
            EnterPreviewNode(entry);
        }
    }

    // id 是要试播的节点 ID，空 ID 表示结束此次预览。
    private void EnterPreviewNode(string id)
    {
        previewChoosing = false;
        if (string.IsNullOrEmpty(id))
        {
            previewNode = null;
            message = "这一段剧情已经播完。";
            return;
        }

        // 一旦进入新台词，重新计算字符切分和打字时间。
        previewNode = previewNodes[id];
        previewCharacters = StringInfo.ParseCombiningCharacters(previewNode.text);
        previewStarted = EditorApplication.timeSinceStartup;
        previewComplete = false;
    }

    // 同时绘制打字进度和可点选的分支，方便核对编排。
    private void DrawPreview()
    {
        EditorGUILayout.LabelField(previewNode.speaker, EditorStyles.boldLabel);
        int count = (int)((EditorApplication.timeSinceStartup - previewStarted) * 25.0);
        if (count >= previewCharacters.Length)
        {
            previewComplete = true;
        }

        // 随编辑器时间增加显示正文，并持续请求绘制。
        string visible = previewNode.text;
        if (!previewComplete)
        {
            visible = previewNode.text.Substring(0, previewCharacters[count]);
            Repaint();
        }
        EditorGUILayout.LabelField(visible, EditorStyles.wordWrappedLabel, GUILayout.MinHeight(70));

        // 展示可点击的选项，点击后试走该选项指向的节点。
        if (previewChoosing)
        {
            foreach (DialogueChoice choice in previewNode.choices)
            {
                if (GUILayout.Button(choice.text))
                {
                    EnterPreviewNode(choice.next);
                    break;
                }
            }
        }
        // 继续按钮依进度补全文字、打开选项或切到下一句。
        else if (GUILayout.Button("继续"))
        {
            if (!previewComplete)
            {
                previewComplete = true;
            }
            else if (previewNode.choices != null && previewNode.choices.Length > 0)
            {
                previewChoosing = true;
            }
            else
            {
                EnterPreviewNode(previewNode.next);
            }
        }
    }
}
