using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// 资源体检：找出体积最大的贴图，并可按需压缩超过阈值的插图。
// 菜单：Tools/资源体检、Tools/压缩大贴图。
public static class AssetBudgetReport
{
    private const string AssetsRoot = "Assets";
    private const long WarnBytes = 1024L * 1024L;

    [MenuItem("Tools/资源体检")]
    public static void Report()
    {
        var entries = new List<FileInfo>();
        foreach (string path in Directory.GetFiles(AssetsRoot, "*.png", SearchOption.AllDirectories))
        {
            entries.Add(new FileInfo(path));
        }

        entries.Sort((a, b) => b.Length.CompareTo(a.Length));
        var report = new StringBuilder();
        long total = 0;
        foreach (FileInfo file in entries)
        {
            total += file.Length;
        }

        report.AppendLine("贴图 " + entries.Count + " 张，合计 " + (total / 1024 / 1024) + " MB");
        int shown = 0;
        foreach (FileInfo file in entries)
        {
            if (shown >= 10)
            {
                break;
            }

            report.AppendLine("  " + (file.Length / 1024) + " KB  " + file.FullName.Replace('\\', '/').Replace(AssetsRoot + "/", string.Empty));
            shown++;
        }

        foreach (FileInfo file in entries)
        {
            if (file.Length >= WarnBytes)
            {
                report.AppendLine("  [超 1MB] " + file.Name + "  -> 建议压缩到 2048 并启用压缩");
            }
        }

        Debug.Log(report.ToString());
    }

    [MenuItem("Tools/压缩大贴图")]
    public static void CompressLargeTextures()
    {
        var targets = new List<string>();
        foreach (string path in Directory.GetFiles(AssetsRoot, "*.png", SearchOption.AllDirectories))
        {
            if (new FileInfo(path).Length >= WarnBytes)
            {
                targets.Add(path.Replace('\\', '/'));
            }
        }

        if (targets.Count == 0)
        {
            Debug.Log("没有超过 1MB 的贴图。");
            return;
        }

        if (!EditorUtility.DisplayDialog("压缩贴图", "将压缩 " + targets.Count + " 张大贴图（最大 2048，压缩质量 High）。导入设置改动后可通过版本库回退。", "继续", "取消"))
        {
            return;
        }

        foreach (string path in targets)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                continue;
            }

            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            Debug.Log("已压缩：" + path);
        }
    }
}
