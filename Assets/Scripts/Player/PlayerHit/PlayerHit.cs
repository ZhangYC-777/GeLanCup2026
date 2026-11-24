using UnityEngine;
using UnityEngine.Events;

public class PlayerHit : MonoBehaviour
{
    // 提供当前可操控角色。
    [SerializeField] private SwitchPlayer switchPlayer;
    // 选填的交互范围，配置后按此碰撞体检测。
    [SerializeField] private Collider2D interactionArea;
    // 未配置交互范围时使用的圆形检测半径。
    [SerializeField, Min(0f)] private float radius = 1.25f;
    // 由此交互组件单独管理提示时，拖入各角色头顶的 F 图标。
    [SerializeField] private GameObject[] prompts = System.Array.Empty<GameObject>();

    // 按 F 命中该交互点以后执行的 Inspector 事件。
    [SerializeField] private UnityEvent onInteract = new UnityEvent();

    // 角色移动完成后，更新当前角色的头顶提示。
    private void LateUpdate()
    {
        Transform player = null;
        if (switchPlayer != null && !switchPlayer.IsGameplayLocked)
        {
            player = switchPlayer.CurrentPlayer;
        }

        if (!IsInRange(player))
        {
            player = null;
        }

        ShowPromptFor(prompts, player);
    }

    // 配了 Collider2D 就使用它的范围，其余场景沿用已有的半径判断。
    // player 是当前角色的 Transform，用它的位置判断交互范围。
    public bool IsInRange(Transform player)
    {
        if (!isActiveAndEnabled || player == null)
        {
            return false;
        }

        if (interactionArea != null)
        {
            return interactionArea.isActiveAndEnabled && interactionArea.OverlapPoint(player.position);
        }

        Vector2 offset = player.position - transform.position;
        return offset.sqrMagnitude <= radius * radius;
    }

    // 通知 Inspector 里绑定的听众，例如 DialogueTrigger.Play。
    public void Interact()
    {
        onInteract.Invoke();
    }

    // 一次性交互结束后，可通过 UnityEvent 关闭范围和提示。
    public void DisableInteraction()
    {
        enabled = false;
    }

    // 交互点失活时隐藏所有提示。
    private void OnDisable()
    {
        ShowPromptFor(prompts, null);
    }

    // 提示挂在角色根物体下，只显示指定角色自己的提示。
    // prompts 是候选提示；player 是该显示提示的角色，传 null 会全部隐藏。
    public static void ShowPromptFor(GameObject[] prompts, Transform player)
    {
        foreach (GameObject prompt in prompts)
        {
            if (prompt == null)
                continue;

            bool show = player != null && prompt.transform.parent == player;
            if (prompt.activeSelf != show)
                prompt.SetActive(show);
        }
    }
}
