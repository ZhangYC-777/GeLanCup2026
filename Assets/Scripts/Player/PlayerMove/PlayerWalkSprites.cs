using UnityEngine;

// 保存一名角色的三组朝向。每组首帧为待机，其余帧循环播放。
[CreateAssetMenu(menuName = "GeLanCup/Player Walk Sprites")]
public sealed class PlayerWalkSprites : ScriptableObject
{
    internal enum Facing { Left, Right, Up }

    [SerializeField] private Sprite[] left;
    [SerializeField] private Sprite[] right;
    [SerializeField] private Sprite[] up;
    [SerializeField, Min(0.01f)] private float frameSeconds = 0.12f;

    // 按朝向与经过的时间选帧。向下时复用朝左的原像素稿。
    internal Sprite GetFrame(Facing facing, bool walking, float elapsed)
    {
        Sprite[] frames = facing == Facing.Up ? up : facing == Facing.Right ? right : left;
        if (frames == null || frames.Length == 0)
        {
            return null;
        }

        int index = walking && frames.Length > 1
            ? 1 + Mathf.FloorToInt(elapsed / Mathf.Max(0.01f, frameSeconds)) % (frames.Length - 1)
            : 0;
        return frames[index];
    }
}
