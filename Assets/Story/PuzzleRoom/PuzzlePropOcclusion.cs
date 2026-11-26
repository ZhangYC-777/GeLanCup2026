using UnityEngine;
using UnityEngine.Rendering;

// 角色在家具后面且与其画面重叠时，降低家具不透明度。
[DefaultExecutionOrder(30)]
[RequireComponent(typeof(SortingGroup), typeof(SpriteRenderer))]
[DisallowMultipleComponent]
public sealed class PuzzlePropOcclusion : MonoBehaviour
{
    [SerializeField] private SwitchPlayer switchPlayer;
    [SerializeField, Range(0f, 1f)] private float obscuringOpacity = 0.22f;

    private SpriteRenderer furniture;
    private SortingGroup furnitureOrder;
    private Transform currentPlayer;
    private SortingGroup playerOrder;
    private SpriteRenderer[] playerSprites = System.Array.Empty<SpriteRenderer>();
    private Color normalColor;

    private void Awake()
    {
        furniture = GetComponent<SpriteRenderer>();
        furnitureOrder = GetComponent<SortingGroup>();
        normalColor = furniture.color;
    }

    private void OnDisable()
    {
        furniture.color = normalColor;
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

        bool overlap = playerOrder != null && furnitureOrder.sortingOrder >= playerOrder.sortingOrder && OverlapsPlayer();
        Color color = normalColor;
        color.a = overlap ? normalColor.a * obscuringOpacity : Mathf.MoveTowards(furniture.color.a, normalColor.a, 6f * Time.unscaledDeltaTime);
        furniture.color = color;
    }

    private bool OverlapsPlayer()
    {
        Bounds bounds = furniture.bounds;
        foreach (SpriteRenderer sprite in playerSprites)
        {
            if (!sprite.enabled || !sprite.gameObject.activeInHierarchy || sprite.sprite == null)
            {
                continue;
            }

            Bounds body = sprite.bounds;
            if (body.min.x < bounds.max.x && body.max.x > bounds.min.x && body.min.y < bounds.max.y && body.max.y > bounds.min.y)
            {
                return true;
            }
        }

        return false;
    }
}
