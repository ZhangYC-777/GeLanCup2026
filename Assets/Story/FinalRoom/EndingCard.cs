using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 结局卡上的「返回主菜单」，结局画面不写进度，直接回到主菜单。
public sealed class EndingCard : MonoBehaviour
{
    [SerializeField] private Button returnButton;

    private void Awake()
    {
        // 兼容尚未保存按钮引用的终局场景。
        if (returnButton == null)
        {
            returnButton = GetComponentInChildren<Button>(true);
        }

        if (returnButton == null)
        {
            Debug.LogError("结局卡缺少返回主菜单按钮。", this);
            return;
        }

        returnButton.onClick.AddListener(ReturnToMenu);
    }

    public void ReturnToMenu()
    {
        int index = SceneUtility.GetBuildIndexByScenePath(SceneSave.MainMenuPath);
        if (index < 0)
        {
            Debug.LogError("找不到主菜单场景：" + SceneSave.MainMenuPath, this);
            return;
        }

        SceneManager.LoadScene(index);
    }
}
