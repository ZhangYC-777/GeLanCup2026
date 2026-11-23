using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHit : MonoBehaviour
{
    [SerializeField] private SwitchPlayer switchPlayer;
    [SerializeField, Min(0f)] private float radius = 1.25f;
    [SerializeField] private GameObject[] prompts = System.Array.Empty<GameObject>();

    // 角色移动完成后，用 XY 距离判断当前角色是否在交互范围内。
    private void LateUpdate()
    {
        Transform player = switchPlayer != null ? switchPlayer.CurrentPlayer : null;
        if (player != null)
        {
            Vector2 offset = player.position - transform.position;
            if (offset.sqrMagnitude > radius * radius)
                player = null;
        }

        ShowPromptFor(player);
    }

    // 交互点失活时隐藏所有提示。
    private void OnDisable()
    {
        ShowPromptFor(null);
    }

    // 提示挂在角色根物体下，只显示指定角色自己的提示。
    private void ShowPromptFor(Transform player)
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
