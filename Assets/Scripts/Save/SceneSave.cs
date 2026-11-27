using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 挂在游戏场景的存档界面上，负责收集和恢复该场景的存档数据。
[DisallowMultipleComponent, DefaultExecutionOrder(20)]
public class SceneSave : MonoBehaviour
{
    internal const string MainStoryDirectory = "Assets/Scenes/GameScene/";
    internal const string MainMenuPath = MainStoryDirectory + "Scene-1.unity";
    internal const string SecondFloorPath = MainStoryDirectory + "Scene4.unity";
    internal const string ThirdFloorPath = MainStoryDirectory + "Scene5.unity";

    [SerializeField] private SwitchPlayer switchPlayer;
    [SerializeField] private InventoryController inventory;
    [SerializeField] private PartySan partySan;
    [SerializeField] private DialogueTrigger openingDialogue;
    [SerializeField] private DialogueController dialogue;
    [SerializeField] private Text errorLabel;
    [SerializeField] private SafeRoomSequence safeRoom;
    [SerializeField] private bool checkpointOnArrival;
    // 终局等连续演出从出生点重播，保留到达场景时的背包和 SAN。
    [SerializeField] private bool keepArrivalCheckpoint;

    private static SceneProgress pendingRestore;
    private static SceneProgress latestProgress;
    private static SceneProgress latestCheckpoint;
    private static bool startedFromMenu;
    private readonly List<DialogueTrigger> sceneTriggers = new List<DialogueTrigger>();
    private bool loading;
    // 终局场景只演出不推进存档，避免「继续游戏」回到结局房。
    private bool progressBlocked;
    private Font menuFont;

    internal bool HasRestoredProgress { get; private set; }

    // 由终局演出在开场调用：本场景不再写任何进度，退出时保留上一个场景的存档。
    internal void BlockProgressWrite()
    {
        progressBlocked = true;
    }

    internal static bool IsMainStoryScene(string path)
    {
        return path != null && path != MainMenuPath && path.StartsWith(MainStoryDirectory, StringComparison.Ordinal);
    }

    // 每次进入播放模式时重置，包括 Unity 关闭域重载的情况。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        pendingRestore = null;
        latestProgress = null;
        latestCheckpoint = null;
        startedFromMenu = false;
    }

    // 主菜单将待恢复的数据交给目标场景。新游戏传入 null，沿用场景初始配置。
    internal static void EnterGame(int sceneBuildIndex, GameSaveData save)
    {
        pendingRestore = save?.progress;
        latestProgress = pendingRestore;
        latestCheckpoint = save?.checkpoint;
        startedFromMenu = true;
        SceneManager.LoadScene(sceneBuildIndex);
    }

    // 角色和背包初始化后，在开场对话的 Start 和首个物理帧之前完成恢复。
    private void Awake()
    {
        menuFont = MainMenu.ApplyMenuFont(gameObject);
        if (dialogue == null && openingDialogue != null)
        {
            dialogue = openingDialogue.Controller;
        }

        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            sceneTriggers.AddRange(root.GetComponentsInChildren<DialogueTrigger>(true));
        }

        SceneProgress restore = pendingRestore?.scenePath == gameObject.scene.path ? pendingRestore : null;
        // 新场景沿用已完成的分支，读档则以该份进度为准。
        SceneProgress storyProgress = restore ?? latestProgress;
        if (dialogue != null && storyProgress != null)
        {
            dialogue.RestoreStoryValues(storyProgress.storyValues);
        }

        if (restore == null || !CanCapture())
        {
            return;
        }

        pendingRestore = null;
        HasRestoredProgress = true;
        if (!keepArrivalCheckpoint && switchPlayer != null && restore.playerPositions.Length > 0)
        {
            safeRoom?.RestoreRoutePositions(restore);
            switchPlayer.RestoreParty(restore.playerPositions, restore.activePlayerIndex);
        }
        inventory?.RestoreItemIds(restore.itemIds);
        if (restore.hasPartySan && partySan != null)
        {
            partySan.RestoreSan(restore.currentSan);
        }

        var completed = new HashSet<string>(restore.completedDialogueTriggers ?? Array.Empty<string>());
        foreach (DialogueTrigger trigger in sceneTriggers)
        {
            if (completed.Contains(trigger.CheckpointKey))
            {
                trigger.RestoreCompleted();
            }
        }

        if (restore.skipOpeningDialogue && openingDialogue != null)
        {
            openingDialogue.RestoreCompleted();
        }

        // 跨场景进度没有位置数据，由本场景的出生站位建立检查点。
        if (checkpointOnArrival && (keepArrivalCheckpoint || restore.playerPositions.Length == 0))
        {
            CaptureCheckpoint();
        }
    }

    private void OnEnable()
    {
        if (dialogue != null && switchPlayer != null && !keepArrivalCheckpoint)
        {
            dialogue.PlaybackCompleted += CaptureCheckpoint;
        }
        if (partySan != null)
        {
            partySan.Depleted += RetryCheckpoint;
        }
    }

    private void Start()
    {
        // 第一段对话完成前，也有一个安全的关卡初始位置可重试。
        if (switchPlayer != null && (latestCheckpoint == null || keepArrivalCheckpoint && !HasRestoredProgress))
        {
            CaptureCheckpoint();
        }
    }

    private void OnDisable()
    {
        if (dialogue != null && switchPlayer != null && !keepArrivalCheckpoint)
        {
            dialogue.PlaybackCompleted -= CaptureCheckpoint;
        }
        if (partySan != null)
        {
            partySan.Depleted -= RetryCheckpoint;
        }
    }

    // 退出时保存可继续的当前位置，并保留独立的失败检查点。
    public void SaveAndReturnToMenu()
    {
        if (loading)
        {
            return;
        }

        if (!SaveProgress())
        {
            if (errorLabel != null)
            {
                errorLabel.text = "保存失败，请重试";
            }
            return;
        }

        loading = true;
        ResetSession();
        SceneManager.LoadScene(SceneUtility.GetBuildIndexByScenePath(MainMenuPath));
    }

    // 从主菜单进入后，正常关闭游戏也走相同的保存流程。
    private void OnApplicationQuit()
    {
        if (startedFromMenu && !loading && gameObject.scene == SceneManager.GetActiveScene())
        {
            SaveProgress();
        }
    }

    private void OnDestroy()
    {
        if (menuFont != null)
        {
            Destroy(menuFont);
        }
    }

    private bool CanCapture()
    {
        return switchPlayer == null ? dialogue != null : switchPlayer.CurrentPlayer != null && inventory != null;
    }

    private SceneProgress CaptureProgress()
    {
        if (progressBlocked || loading || !CanCapture() || (partySan != null && partySan.CurrentSan == 0))
        {
            return null;
        }

        var completed = new List<string>();
        foreach (DialogueTrigger trigger in sceneTriggers)
        {
            if (trigger != null && trigger.HasCompleted)
            {
                completed.Add(trigger.CheckpointKey);
            }
        }

        SceneProgress progress = new SceneProgress
        {
            scenePath = gameObject.scene.path,
            playerPositions = switchPlayer != null ? switchPlayer.CapturePositions() : Array.Empty<Vector3>(),
            activePlayerIndex = switchPlayer != null ? switchPlayer.CurrentPlayerIndex : -1,
            safeRoomRouteLayoutVersion = safeRoom != null ? SafeRoomSequence.RouteLayoutVersion : 0,
            itemIds = inventory != null ? inventory.CaptureItemIds() : Array.Empty<string>(),
            hasPartySan = partySan != null,
            currentSan = partySan != null ? partySan.CurrentSan : 0,
            storyValues = dialogue != null ? dialogue.CaptureStoryValues() : latestProgress?.storyValues,
            completedDialogueTriggers = completed.ToArray()
        };
        return progress.IsValid() ? progress : null;
    }

    internal void CaptureCheckpoint()
    {
        SceneProgress progress = CaptureProgress();
        if (progress == null)
        {
            return;
        }

        latestProgress = latestCheckpoint = progress;
        if (startedFromMenu && IsMainStoryScene(progress.scenePath))
        {
            WriteProgress();
        }
    }

    // 保留队伍状态，位置和本场景对话触发器由下一场景重新初始化。
    internal void LoadNextPartyScene(string path)
    {
        if (loading)
        {
            return;
        }

        int sceneIndex = SceneUtility.GetBuildIndexByScenePath(path);
        SceneProgress progress = CaptureProgress();
        if (!IsMainStoryScene(path) || sceneIndex < 0 || progress == null)
        {
            Debug.LogError("无法进入主线场景：" + path, this);
            return;
        }

        progress.scenePath = path;
        progress.playerPositions = Array.Empty<Vector3>();
        progress.activePlayerIndex = -1;
        progress.safeRoomRouteLayoutVersion = 0;
        progress.completedDialogueTriggers = Array.Empty<string>();
        pendingRestore = latestProgress = progress;
        loading = true;
        SceneManager.LoadScene(sceneIndex);
    }

    private bool SaveProgress()
    {
        // 终局只演出不写档：直接当作保存成功，退出时沿用上一个场景的进度。
        if (progressBlocked || !IsMainStoryScene(gameObject.scene.path))
        {
            return true;
        }

        // 对话播到一半时仍保存最近完整的进度，避免记录只执行了一半的分支。
        if (!keepArrivalCheckpoint && switchPlayer != null && (dialogue == null || !dialogue.IsPlaying))
        {
            latestProgress = CaptureProgress() ?? latestProgress;
        }

        return latestProgress == null || WriteProgress();
    }

    private static bool WriteProgress()
    {
        return GameSaveFile.TryWrite(new GameSaveData
        {
            version = GameSaveData.CurrentVersion,
            progress = latestProgress,
            checkpoint = latestCheckpoint
        });
    }

    // 重新加载场景以清掉检查点之后发生的临时变化。
    internal void RetryCheckpoint()
    {
        if (loading)
        {
            return;
        }

        string path = latestCheckpoint?.scenePath ?? gameObject.scene.path;
        int sceneIndex = SceneUtility.GetBuildIndexByScenePath(path);
        if (sceneIndex < 0)
        {
            Debug.LogError("找不到失败检查点场景：" + path, this);
            return;
        }

        loading = true;
        pendingRestore = latestProgress = latestCheckpoint;
        if (startedFromMenu && latestProgress != null && IsMainStoryScene(path))
        {
            WriteProgress();
        }

        SceneManager.LoadScene(sceneIndex);
    }
}
