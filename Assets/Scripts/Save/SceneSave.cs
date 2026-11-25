using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 挂在游戏场景的存档界面上，负责收集和恢复该场景的存档数据。
[DisallowMultipleComponent, DefaultExecutionOrder(20)]
public class SceneSave : MonoBehaviour
{
    internal const string MainMenuPath = "Assets/Scenes/MainMenu.unity";

    [SerializeField] private SwitchPlayer switchPlayer;
    [SerializeField] private InventoryController inventory;
    [SerializeField] private PartySan partySan;
    [SerializeField] private DialogueTrigger openingDialogue;
    [SerializeField] private Text errorLabel;

    private static GameSaveData pendingRestore;
    private static bool startedFromMenu;
    private Font menuFont;

    // 每次进入播放模式时重置，包括 Unity 关闭域重载的情况。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        pendingRestore = null;
        startedFromMenu = false;
    }

    // 主菜单将待恢复的数据交给目标场景。新游戏传入 null，沿用场景初始配置。
    internal static void EnterGame(int sceneBuildIndex, GameSaveData save)
    {
        pendingRestore = save;
        startedFromMenu = true;
        SceneManager.LoadScene(sceneBuildIndex);
    }

    // 角色和背包初始化后，在开场对话的 Start 和首个物理帧之前完成恢复。
    private void Awake()
    {
        menuFont = MainMenu.ApplyMenuFont(gameObject);

        if (pendingRestore == null || pendingRestore.scenePath != gameObject.scene.path ||
            !CanCapture())
        {
            return;
        }

        GameSaveData save = pendingRestore;
        pendingRestore = null;
        inventory.RestoreItemIds(save.itemIds);
        switchPlayer.RestoreParty(save.playerPositions, save.activePlayerIndex);
        if (save.hasPartySan && partySan != null)
        {
            partySan.RestoreSan(save.currentSan);
        }

        if (openingDialogue != null)
        {
            openingDialogue.enabled = false;
        }
    }

    // 界面按钮先落盘。失败时继续留在当前场景。
    public void SaveAndReturnToMenu()
    {
        if (!SaveCurrentScene())
        {
            errorLabel.text = "保存失败，请重试";
            return;
        }

        startedFromMenu = false;
        SceneManager.LoadScene(SceneUtility.GetBuildIndexByScenePath(MainMenuPath));
    }

    // 正常关闭游戏时自动保存。直接从编辑器打开关卡调试时可用按钮显式保存。
    private void OnApplicationQuit()
    {
        if (startedFromMenu && gameObject.scene == SceneManager.GetActiveScene())
        {
            SaveCurrentScene();
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
        return switchPlayer != null && switchPlayer.CurrentPlayer != null && inventory != null;
    }

    private bool SaveCurrentScene()
    {
        if (!CanCapture())
        {
            Debug.LogError("请检查存档组件的角色切换和背包引用。", this);
            return false;
        }

        GameSaveData save = new GameSaveData
        {
            version = GameSaveData.CurrentVersion,
            scenePath = gameObject.scene.path,
            playerPositions = switchPlayer.CapturePositions(),
            activePlayerIndex = switchPlayer.CurrentPlayerIndex,
            itemIds = inventory.CaptureItemIds(),
            hasPartySan = partySan != null,
            currentSan = partySan != null ? partySan.CurrentSan : 0
        };
        return save.IsValid() && GameSaveFile.TryWrite(save);
    }
}
