using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    // 新游戏加载的起始关卡。路线变化时在主菜单修改这里。
    [SerializeField] private string firstScenePath = SceneSave.MainStoryDirectory + "Scene0.unity";
    [SerializeField] private GameObject options;
    [SerializeField] private GameObject confirmation;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Text status;

    private GameSaveData savedGame;
    private Font menuFont;
    private bool loading;

    private void Awake()
    {
        menuFont = ApplyMenuFont(gameObject);
        continueButton.interactable = GameSaveFile.TryRead(out savedGame);
        if (!continueButton.interactable)
        {
            status.text = "暂无可继续的主线存档";
        }
    }

    private void Start()
    {
        (continueButton.interactable ? continueButton : newGameButton).Select();
    }

    private void OnDestroy()
    {
        if (menuFont != null)
        {
            Destroy(menuFont);
        }
    }

    // 单存档槽已有数据时，确认后才进入新的一局。
    public void NewGame()
    {
        if (savedGame != null)
        {
            options.SetActive(false);
            confirmation.SetActive(true);
            cancelButton.Select();
            return;
        }

        ConfirmNewGame();
    }

    public void ConfirmNewGame()
    {
        OpenScene(firstScenePath, null);
    }

    public void CancelNewGame()
    {
        confirmation.SetActive(false);
        options.SetActive(true);
        newGameButton.Select();
    }

    public void ContinueGame()
    {
        if (savedGame != null)
        {
            OpenScene(savedGame.progress.scenePath, savedGame);
        }
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OpenScene(string scenePath, GameSaveData save)
    {
        if (loading)
        {
            return;
        }

        int buildIndex = SceneUtility.GetBuildIndexByScenePath(scenePath);
        if (buildIndex < 0 || !SceneSave.IsMainStoryScene(scenePath))
        {
            CancelNewGame();
            status.text = "找不到游戏场景";
            return;
        }

        loading = true;
        SceneSave.EnterGame(buildIndex, save);
    }

    // 沿用项目的中英文字体回退，返回的字体由调用方销毁。
    internal static Font ApplyMenuFont(GameObject root)
    {
        Font font = DialogueView.FindDialogueFont();
        if (font != null)
        {
            foreach (Text label in root.GetComponentsInChildren<Text>(true))
            {
                label.font = font;
            }
        }
        return font;
    }
}
