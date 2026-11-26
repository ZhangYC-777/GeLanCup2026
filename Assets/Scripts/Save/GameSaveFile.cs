using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
internal class GameSaveData
{
    public const int CurrentVersion = 4;

    public int version;
    public SceneProgress progress;
    public SceneProgress checkpoint;

    internal bool IsValid()
    {
        return version == CurrentVersion && IsMainStoryProgress(progress) && IsMainStoryProgress(checkpoint);
    }

    private static bool IsMainStoryProgress(SceneProgress progress)
    {
        return progress != null && SceneSave.IsMainStoryScene(progress.scenePath) && progress.IsValid();
    }
}

[Serializable]
internal class SceneProgress
{
    public string scenePath;
    public Vector3[] playerPositions;
    public int activePlayerIndex;
    public int safeRoomRouteLayoutVersion;
    public string[] itemIds;
    public bool hasPartySan;
    public int currentSan;
    public DialogueStateChange[] storyValues;
    public string[] completedDialogueTriggers;
    public bool skipOpeningDialogue;

    internal bool IsValid()
    {
        if (string.IsNullOrEmpty(scenePath) || scenePath == SceneSave.MainMenuPath ||
            SceneUtility.GetBuildIndexByScenePath(scenePath) < 0 || playerPositions == null ||
            itemIds == null || (hasPartySan && currentSan <= 0))
        {
            return false;
        }

        // 序章这样的纯剧情场景没有队伍位置。
        if (playerPositions.Length == 0)
        {
            return activePlayerIndex == -1;
        }

        if (activePlayerIndex < 0 || activePlayerIndex >= playerPositions.Length)
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
            string json = File.ReadAllText(SavePath);
            GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);
            UpgradeSave(data, json);

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

    private static void UpgradeSave(GameSaveData data, string json)
    {
        if (data == null || data.version < 1 || data.version >= GameSaveData.CurrentVersion)
        {
            return;
        }

        if (data.version <= 2)
        {
            // 旧主线存档只有一份进度，用它作为首次读档的失败检查点。
            SceneProgress oldProgress = JsonUtility.FromJson<SceneProgress>(json);
            oldProgress.hasPartySan = oldProgress.hasPartySan && oldProgress.currentSan > 0;
            oldProgress.skipOpeningDialogue = data.version == 1;
            data.progress = oldProgress;
            data.checkpoint = oldProgress;
        }

        // 旧 Scene2 和新 Scene2 对应不同关卡，只对改名前写入的存档换路径。
        MigrateScenePath(data.progress);
        if (data.checkpoint != data.progress)
        {
            MigrateScenePath(data.checkpoint);
        }

        data.version = GameSaveData.CurrentVersion;
    }

    private static void MigrateScenePath(SceneProgress progress)
    {
        switch (progress?.scenePath)
        {
            case SceneSave.MainStoryDirectory + "Prologue.unity":
                progress.scenePath = SceneSave.MainStoryDirectory + "Scene0.unity";
                break;
            case SceneSave.MainStoryDirectory + "GameScene0.unity":
                progress.scenePath = SceneSave.MainStoryDirectory + "Scene1.unity";
                break;
            case SceneSave.MainStoryDirectory + "GameScene1.unity":
                progress.scenePath = SceneSave.MainStoryDirectory + "Scene2.unity";
                break;
            case SceneSave.MainStoryDirectory + "Scene2.unity":
                progress.scenePath = SceneSave.MainStoryDirectory + "Scene3.unity";
                break;
            case SceneSave.MainStoryDirectory + "Scene3.unity":
                progress.scenePath = SceneSave.MainStoryDirectory + "Scene4.unity";
                break;
        }
    }

    internal static bool TryWrite(GameSaveData save)
    {
        if (!save.IsValid())
        {
            return false;
        }

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
