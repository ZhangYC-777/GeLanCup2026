using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
internal class GameSaveData
{
    public const int CurrentVersion = 1;

    public int version;
    public string scenePath;
    public Vector3[] playerPositions;
    public int activePlayerIndex;
    public string[] itemIds;
    // 旧存档缺少此字段时保留关卡初始 SAN。0 点本身也是有效的存档值。
    public bool hasPartySan;
    public int currentSan;

    internal bool IsValid()
    {
        if (version != CurrentVersion || string.IsNullOrEmpty(scenePath) ||
            scenePath == SceneSave.MainMenuPath || SceneUtility.GetBuildIndexByScenePath(scenePath) < 0 ||
            playerPositions == null || activePlayerIndex < 0 || activePlayerIndex >= playerPositions.Length ||
            itemIds == null)
        {
            return false;
        }

        foreach (Vector3 position in playerPositions)
        {
            if (float.IsNaN(position.sqrMagnitude) || float.IsInfinity(position.sqrMagnitude))
            {
                return false;
            }
        }

        return true;
    }
}

// 读写单个本地 JSON 存档。先完整写入临时文件，再替换当前存档。
internal static class GameSaveFile
{
#if UNITY_EDITOR
    private static string SaveDirectory => Path.Combine(Application.dataPath, "..", "Library", "GameSaves");
#else
    private static string SaveDirectory => Application.persistentDataPath;
#endif
    private static string SavePath => Path.Combine(SaveDirectory, "save.json");

    internal static bool Exists => File.Exists(SavePath);

    internal static bool TryRead(out GameSaveData save)
    {
        save = null;
        if (!Exists)
        {
            return false;
        }

        try
        {
            GameSaveData data = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(SavePath));
            if (data != null && data.IsValid())
            {
                save = data;
                return true;
            }
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException ||
                                      error is ArgumentException)
        {
            Debug.LogWarning("读取存档失败：" + error.Message);
        }

        return false;
    }

    internal static bool TryWrite(GameSaveData save)
    {
        string path = SavePath;
        string temporaryPath = path + ".tmp";
        try
        {
            Directory.CreateDirectory(SaveDirectory);
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(save, true));
            if (File.Exists(path))
            {
                File.Replace(temporaryPath, path, null);
            }
            else
            {
                File.Move(temporaryPath, path);
            }
            return true;
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException ||
                                      error is NotSupportedException)
        {
            Debug.LogError("保存存档失败：" + error.Message);
            return false;
        }
    }
}
