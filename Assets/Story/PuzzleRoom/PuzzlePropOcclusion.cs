using UnityEngine;
using UnityEngine.Rendering;

// 当前角色被装饰物遮挡时，降低整件装饰物的不透明度。
[DefaultExecutionOrder(30)]
[RequireComponent(typeof(SortingGroup))]
[DisallowMultipleComponent]
public sealed class PuzzlePropOcclusion : MonoBehaviour
{
    [SerializeField] private SwitchPlayer switchPlayer;
    [SerializeField, Range(0f, 1f)] private float obscuringOpacity = 0.22f;

    private SpriteRenderer[] propSprites;
    private float[] normalOpacities;
    private SortingGroup propOrder;
    private Transform currentPlayer;
    private SortingGroup playerOrder;
    private SpriteRenderer[] playerSprites = System.Array.Empty<SpriteRenderer>();
    private float opacity = 1f;

    private void Awake()
    {
        propOrder = GetComponent<SortingGroup>();
        propSprites = GetComponentsInChildren<SpriteRenderer>(true);
        normalOpacities = new float[propSprites.Length];
        for (int i = 0; i < propSprites.Length; i++)
        {
            normalOpacities[i] = propSprites[i].color.a;
        }
    }

    private void OnDisable()
    {
        if (propSprites != null && opacity < 1f)
        {
            SetOpacity(1f);
        }
    }

    private void LateUpdate()
    {
        Transform player = switchPlayer != null ? switchPlayer.CurrentPlayer : null;
        if (currentPlayer != player)
        {
            currentPlayer = player;
            playerOrder = player != null ? player.GetComponent<SortingGroup>() : null;
            playerSprites = player != null ? player.GetComponentsInChildren<SpriteRenderer>(true) : System.Array.Empty<SpriteRenderer>();
        }

        bool overlap = playerOrder != null && propOrder.sortingOrder >= playerOrder.sortingOrder && OverlapsPlayer();
        float nextOpacity = overlap ? obscuringOpacity : Mathf.MoveTowards(opacity, 1f, 6f * Time.unscaledDeltaTime);
        if (nextOpacity != opacity)
        {
            SetOpacity(nextOpacity);
        }
    }

    private void SetOpacity(float value)
    {
        opacity = value;
        for (int i = 0; i < propSprites.Length; i++)
        {
            SpriteRenderer sprite = propSprites[i];
            if (sprite != null)
            {
                Color color = sprite.color;
                color.a = normalOpacities[i] * opacity;
                sprite.color = color;
            }
        }
    }

    private bool OverlapsPlayer()
    {
        foreach (SpriteRenderer prop in propSprites)
        {
            if (prop == null || !prop.enabled || !prop.gameObject.activeInHierarchy || prop.sprite == null)
            {
                continue;
            }

            Bounds bounds = prop.bounds;
            foreach (SpriteRenderer sprite in playerSprites)
            {
                if (sprite == null || !sprite.enabled || !sprite.gameObject.activeInHierarchy || sprite.sprite == null)
                {
                    continue;
                }

                Bounds body = sprite.bounds;
                if (body.min.x < bounds.max.x && body.max.x > bounds.min.x && body.min.y < bounds.max.y && body.max.y > bounds.min.y)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
