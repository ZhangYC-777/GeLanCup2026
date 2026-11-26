using Cinemachine;
using UnityEngine;

// 解谜路线留一小圈近处的光。房间和拐角的可见范围由 Scene2 配置。
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public sealed class PuzzleRoomVisibility : MonoBehaviour
{
    [System.Serializable]
    private sealed class View
    {
        public string name;
        public Rect playerArea;
        public Rect secondPlayerArea;
        public Rect room;
        public Rect doorway;
        [Range(0f, 1f)] public float ambientBrightness = 1f;
        [Range(0f, 1f)] public float nearbyBrightness = 1f;
        [Min(0f)] public float lightRadius;
    }

    [SerializeField] private CinemachineVirtualCamera virtualCamera;
    [SerializeField] private Shader visibilityShader;
    [SerializeField] private View safeRoom = new View();
    [SerializeField] private View[] views = System.Array.Empty<View>();

    private static readonly int ViewOrigin = Shader.PropertyToID("_ViewOrigin");
    private static readonly int ViewRight = Shader.PropertyToID("_ViewRight");
    private static readonly int ViewUp = Shader.PropertyToID("_ViewUp");
    private static readonly int RoomBounds = Shader.PropertyToID("_RoomBounds");
    private static readonly int DoorwayBounds = Shader.PropertyToID("_DoorwayBounds");
    private static readonly int RoomLight = Shader.PropertyToID("_RoomLight");
    private static readonly int FocusPoint = Shader.PropertyToID("_FocusPoint");
    private static readonly int Blackout = Shader.PropertyToID("_Blackout");
    private static readonly int BeamOrigin = Shader.PropertyToID("_BeamOrigin");
    private static readonly int BeamDirection = Shader.PropertyToID("_BeamDirection");

    private Camera sceneCamera;
    private Material material;
    private bool blackout;
    private Vector4 beamOrigin;
    private Vector4 beamDirection;

    public void SetBlackout(bool active)
    {
        blackout = active;
    }

    public void SetFlashlight(bool active, Vector2 origin, Vector2 direction)
    {
        beamOrigin = new Vector4(origin.x, origin.y, active ? 1f : 0f, 0f);
        beamDirection = new Vector4(direction.x, direction.y, 0f, 0f);
    }

    private void OnEnable()
    {
        sceneCamera = GetComponent<Camera>();
        if (visibilityShader != null && visibilityShader.isSupported)
        {
            material = new Material(visibilityShader) { hideFlags = HideFlags.HideAndDontSave };
        }
    }

    private void OnDisable()
    {
        if (material != null)
        {
            Destroy(material);
            material = null;
        }
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Transform focus = virtualCamera != null ? virtualCamera.Follow : null;
        if (material == null || focus == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        Vector2 position = focus.position;
        View view = GetView(position);
        float depth = -sceneCamera.transform.position.z;
        Vector3 origin = sceneCamera.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
        Vector3 right = sceneCamera.ViewportToWorldPoint(new Vector3(1f, 0f, depth));
        Vector3 up = sceneCamera.ViewportToWorldPoint(new Vector3(0f, 1f, depth));

        // 用世界坐标切出实际房间。镜头宽高比变化时，墙后仍会被遮住。
        material.SetVector(ViewOrigin, origin);
        material.SetVector(ViewRight, right - origin);
        material.SetVector(ViewUp, up - origin);
        material.SetVector(RoomBounds, GetBounds(view.room));
        material.SetVector(DoorwayBounds, GetBounds(view.doorway));
        material.SetVector(FocusPoint, new Vector4(position.x, position.y, 0f, 0f));
        material.SetVector(RoomLight, new Vector4(view.ambientBrightness, view.nearbyBrightness, view.lightRadius, 0f));
        material.SetFloat(Blackout, blackout ? 1f : 0f);
        material.SetVector(BeamOrigin, beamOrigin);
        material.SetVector(BeamDirection, beamDirection);
        Graphics.Blit(source, destination, material);
    }

    private View GetView(Vector2 position)
    {
        foreach (View view in views)
        {
            if (view.playerArea.Contains(position) || view.secondPlayerArea.Contains(position))
            {
                return view;
            }
        }

        return safeRoom;
    }

    private static Vector4 GetBounds(Rect rect)
    {
        return new Vector4(rect.xMin, rect.yMin, rect.xMax, rect.yMax);
    }
}
