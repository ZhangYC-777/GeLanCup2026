using UnityEngine;

// 迷宫入口的北门，玩家走到门前后进入一层安全房。
[DisallowMultipleComponent]
public sealed class SafeRoomEntrance : MonoBehaviour
{
    [SerializeField] private SwitchPlayer switchPlayer;
    [SerializeField] private SceneSave sceneSave;

    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.transform == switchPlayer.CurrentPlayer && !switchPlayer.IsGameplayLocked)
        {
            sceneSave.LoadNextPartyScene(SceneSave.MainStoryDirectory + "Scene2.unity");
        }
    }
}
