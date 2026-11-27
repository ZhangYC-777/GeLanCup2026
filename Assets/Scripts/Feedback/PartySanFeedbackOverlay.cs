using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 队伍状态的屏幕反馈：受伤时压暗红雾、回复时暖光一闪、低 SAN 持续心跳式暗角。
// 运行时自建画布并跨场景常驻，不需要在每个剧情场景里摆 UI。
public sealed class PartySanFeedbackOverlay : MonoBehaviour
{
    private const int SortOrder = 900;
    private const float EdgeThickness = 110f;
    private const float LowSanThreshold = .35f;

    private PartySan san;
    private CanvasGroup group;
    private Image[] edges;
    private float flash;
    private float flashDecay = 2.2f;
    private Color flashColor = Color.red;
    private float lastRatio = 1f;

    private static PartySanFeedbackOverlay instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null)
        {
            return;
        }

        var host = new GameObject("Party SAN Feedback");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<PartySanFeedbackOverlay>();
    }

    private void Awake()
    {
        BuildCanvas();
        SceneManager.sceneLoaded += OnSceneLoaded;
        Rebind();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Unbind();
        if (instance == this)
        {
            instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Rebind();
    }

    private void Rebind()
    {
        Unbind();
        san = FindObjectOfType<PartySan>();
        if (san == null)
        {
            return;
        }

        san.SanChanged += OnSanChanged;
        lastRatio = san.MaxSan > 0 ? san.CurrentSan / (float)san.MaxSan : 1f;
    }

    private void Unbind()
    {
        if (san != null)
        {
            san.SanChanged -= OnSanChanged;
            san = null;
        }
    }

    private void BuildCanvas()
    {
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortOrder;
        group = canvasGo.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        edges = new[]
        {
            CreateEdge(canvasGo.transform, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -EdgeThickness), Vector2.zero),
            CreateEdge(canvasGo.transform, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, EdgeThickness)),
            CreateEdge(canvasGo.transform, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(EdgeThickness, 0f)),
            CreateEdge(canvasGo.transform, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-EdgeThickness, 0f), Vector2.zero)
        };
    }

    private static Image CreateEdge(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        var image = go.GetComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
    }

    private void OnSanChanged(int current, int max)
    {
        float ratio = max > 0 ? current / (float)max : 0f;
        if (ratio < lastRatio)
        {
            flashColor = new Color(.62f, .05f, .06f, 1f);
            flash = Mathf.Clamp01(.35f + (lastRatio - ratio) * 4f);
            flashDecay = 1.9f;
        }
        else if (ratio > lastRatio)
        {
            flashColor = new Color(.85f, .78f, .55f, 1f);
            flash = Mathf.Clamp01(.2f + (ratio - lastRatio) * 3f);
            flashDecay = 2.8f;
        }

        lastRatio = ratio;
    }

    private void Update()
    {
        if (group == null)
        {
            return;
        }

        bool low = san != null && san.MaxSan > 0 && san.CurrentSan / (float)san.MaxSan < LowSanThreshold;
        float ambient = low ? .16f + Mathf.Sin(Time.unscaledTime * 3.4f) * .05f : 0f;

        flash = Mathf.Max(0f, flash - Time.unscaledDeltaTime * flashDecay);
        group.alpha = Mathf.Max(ambient, flash);

        Color tint = ambient > flash ? new Color(.5f, .02f, .03f, 1f) : flashColor;
        foreach (Image edge in edges)
        {
            if (edge != null)
            {
                edge.color = tint;
            }
        }
    }
}
