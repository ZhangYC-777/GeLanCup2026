using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// 剧情数据自检：JSON 结构、入口、跳转，以及主线场景是否进了构建列表。
// 菜单：Tools/剧情自检。只读检查，不改任何资源。
public static class StoryDataCheck
{
    private const string StoryRoot = "Assets/Story";

    // 与 SceneSave 中的常量保持一致。
    private static readonly string[] StoryScenes =
    {
        "Assets/Scenes/GameScene/Scene-1.unity",
        "Assets/Scenes/GameScene/Scene0.unity",
        "Assets/Scenes/GameScene/Scene1.unity",
        "Assets/Scenes/GameScene/Scene2.unity",
        "Assets/Scenes/GameScene/Scene3.unity",
        "Assets/Scenes/GameScene/Scene4.unity",
        "Assets/Scenes/GameScene/Scene5.unity"
    };

    [MenuItem("Tools/剧情自检")]
    public static void Run()
    {
        var report = new StringBuilder();
        int problems = 0;
        string[] files = Directory.GetFiles(StoryRoot, "*.json", SearchOption.AllDirectories);
        foreach (string path in files)
        {
            string assetPath = path.Replace('\\', '/');
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (asset == null)
            {
                report.AppendLine("[缺失] 不是 TextAsset：" + assetPath);
                problems++;
                continue;
            }

            DialogueData data;
            Dictionary<string, DialogueNode> nodes;
            string error;
            if (!DialogueLoader.TryLoad(asset.text, out data, out nodes, out error))
            {
                report.AppendLine("[错误] " + assetPath + "：" + error);
                problems++;
                continue;
            }

            report.AppendLine("[通过] " + assetPath + " 节点 " + nodes.Count + "，入口 " + data.entry);
            foreach (DialogueNode node in data.nodes)
            {
                if (string.IsNullOrEmpty(node.rewardItemId))
                {
                    continue;
                }

                if (AssetDatabase.LoadAssetAtPath<InventoryItem>("Assets/Inventory/" + node.rewardItemId + ".asset") == null)
                {
                    report.AppendLine("    [提示] " + node.id + " 的物品不在 Assets/Inventory：" + node.rewardItemId);
                }
            }
        }

        foreach (string scene in StoryScenes)
        {
            int index = SceneUtility.GetBuildIndexByScenePath(scene);
            if (index < 0)
            {
                report.AppendLine("[构建列表缺失] " + scene);
                problems++;
            }
            else
            {
                report.AppendLine("[场景] " + scene + " -> buildIndex " + index);
            }
        }

        report.AppendLine("---- 问题数：" + problems + "，剧情文件：" + files.Length);
        Debug.Log(report.ToString());
    }
}
