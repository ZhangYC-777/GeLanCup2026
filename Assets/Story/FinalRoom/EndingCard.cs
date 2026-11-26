using UnityEngine;
using UnityEngine.SceneManagement;

// 结局卡上的「返回主菜单」，结局画面不写进度，直接回到主菜单。
public sealed class EndingCard : MonoBehaviour
{
    private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";

    public void ReturnToMenu()
    {
        int index = SceneUtility.GetBuildIndexByScenePath(MainMenuPath);
        if (index < 0)
        {
            Debug.LogError("找不到主菜单场景：" + MainMenuPath, this);
            return;
        }

        SceneManager.LoadScene(index);
    }
}
