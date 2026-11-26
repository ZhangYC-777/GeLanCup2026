using UnityEngine;

// 把手电锥形光限制在首面墙前，和迷宫移动共用同一份格子数据。
internal sealed class BreadTrialLight : MonoBehaviour
{
    private const int RayCount = 96;
    private const float RayRange = 13f;
    private const float RayStep = .12f;

    private static readonly int View = Shader.PropertyToID("_View");
    private static readonly int Runner = Shader.PropertyToID("_Runner");
    private static readonly int Facing = Shader.PropertyToID("_Facing");
    private static readonly int Danger = Shader.PropertyToID("_Danger");
    private static readonly int RayLimits = Shader.PropertyToID("_RayLimits");

    private readonly float[] limits = new float[RayCount];
    private readonly Vector2[] directions = new Vector2[RayCount];
    private Material material;
    private Camera mazeCamera;
    private Vector2 worldOffset;
    private BreadMazeLayout lastMaze;
    private Vector2 lastPosition;

    internal void Configure(Shader shader, Camera source, Vector2 offset)
    {
        if (shader.isSupported)
            material = new Material(shader);
        mazeCamera = source;
        worldOffset = offset;
        for (int i = 0; i < directions.Length; i++)
        {
            float angle = i * Mathf.PI * 2f / directions.Length;
            directions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
    }

    internal void SetState(BreadMazeLayout maze, Vector2 position, Vector2 facing, float danger)
    {
        if (material == null) return;

        if (lastMaze != maze || (lastPosition - position).sqrMagnitude > .01f)
        {
            lastMaze = maze;
            lastPosition = position;
            for (int i = 0; i < limits.Length; i++)
            {
                Vector2 direction = directions[i];
                float distance = RayStep;
                while (distance < RayRange && maze.IsOpen(BreadMazeLayout.CellAt(position + direction * distance)))
                    distance += RayStep;
                limits[i] = distance;
            }
            material.SetFloatArray(RayLimits, limits);
        }
        material.SetVector(Runner, position);
        material.SetVector(Facing, facing);
        material.SetFloat(Danger, danger);
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (material == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        Vector2 center = (Vector2)mazeCamera.transform.position - worldOffset;
        float height = mazeCamera.orthographicSize * 2f;
        material.SetVector(View, new Vector4(center.x, center.y, height * mazeCamera.aspect, height));
        Graphics.Blit(source, destination, material);
    }

    private void OnDestroy()
    {
        if (material != null)
            Destroy(material);
    }
}
