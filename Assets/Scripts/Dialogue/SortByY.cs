using UnityEngine;
using UnityEngine.Rendering;

// 按脚底的 Y 坐标排序。画面下方的物体会遮挡画面上方的物体。
[RequireComponent(typeof(SortingGroup))]
public class SortByY : MonoBehaviour
{
    // 相对于物体原点的脚底 Y 偏移，填写世界坐标单位。
    [SerializeField] private float footOffsetY;
    // 把一个角色或道具的多个 Sprite 当作一组排序。
    private SortingGroup sortingGroup;

    // 缓存同一物体上的 SortingGroup。
    private void Awake()
    {
        sortingGroup = GetComponent<SortingGroup>();
    }

    // 角色本帧移动后再更新前后遮挡。负号使较低的脚底位置得到更高的排序值。
    private void LateUpdate()
    {
        float footY = transform.position.y + footOffsetY;
        sortingGroup.sortingOrder = Mathf.RoundToInt(-footY * 100f);
    }
}
