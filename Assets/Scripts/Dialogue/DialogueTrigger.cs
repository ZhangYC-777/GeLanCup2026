using System;
using UnityEngine;
using UnityEngine.Events;

// 场景接入点，决定什么时候请求播放，以及本次应该选哪个入口。
public class DialogueTrigger : MonoBehaviour
{
    private enum StartMode
    {
        // 由交互点、机关或关卡事件调用 Play。
        ByRequest,
        // 场景组件启动时自动尝试播放。
        OnSceneStart,
        // 当前操控角色进入触发区时自动尝试播放。
        OnAreaEnter
    }

    [Serializable]
    private class StoryRoute
    {
        // 要查询的状态名，和配置器里的「记录的状态名」一致。
        public string key;
        // 命中路径所需的状态值。
        public string value;
        // 命中该状态时要播放的节点 ID。
        public string entry;
    }

    // 场景里真正负责播放剧情的控制器。
    [SerializeField] private DialogueController controller;
    // 包含节点和台词的 JSON 文本资产。
    [SerializeField] private TextAsset dialogueFile;
    // 触发方式，决定从 Start、触发区还是外部调用进入。
    [SerializeField] private StartMode mode;
    // 没有匹配路径时使用的节点 ID，留空则使用文件的默认入口。
    [SerializeField] private string defaultEntry;
    // 按 Inspector 中的顺序逐项检查。匹配到第一条就使用该入口。
    [SerializeField] private StoryRoute[] routes = new StoryRoute[0];
    // 勾选后，此触发器在当前场景中成功启动一次便记为已用。
    [SerializeField] private bool playOnce = true;
    // 本段剧情自然结束后执行的场景事件。
    [SerializeField] private UnityEvent onFinished = new UnityEvent();

    // 记录控制器是否已经接受过本触发器的播放请求。
    private bool played;
    private bool completed;
    // 已经在当前区域处理过的角色，避免持续重播。
    private Transform lastPlayerInArea;

    internal DialogueController Controller => controller;
    internal bool HasCompleted => playOnce && completed;

    // 路径同时区分同名物体和挂在同一个物体上的多个触发器。
    internal string CheckpointKey
    {
        get
        {
            string path = ":" + Array.IndexOf(GetComponents<DialogueTrigger>(), this);
            for (Transform current = transform; current != null; current = current.parent)
            {
                path = "/" + current.GetSiblingIndex() + ":" + current.name + path;
            }
            return path;
        }
    }

    internal void RestoreCompleted()
    {
        played = completed = true;
    }

    // 开场对话在场景组件初始化完成后开始。
    private void Start()
    {
        if (mode == StartMode.OnSceneStart)
        {
            Play();
        }
    }

    // 进入区域或切换到已在区域内的角色时，尝试播放一次。
    // other 是与本区域重叠的 Collider2D。持续检测也能覆盖切换到区域内角色的情况。
    private void OnTriggerStay2D(Collider2D other)
    {
        if (mode != StartMode.OnAreaEnter || controller == null ||
            !controller.IsCurrentPlayer(other.transform) || lastPlayerInArea == other.transform)
        {
            return;
        }

        // 成功开始或已经播过一次时，登记当前角色，避免下个物理帧再请求。
        if (TryPlay() || (playOnce && played))
        {
            lastPlayerInArea = other.transform;
        }
    }

    // other 是离开区域的 Collider2D。离开时清除区域内的角色登记。
    private void OnTriggerExit2D(Collider2D other)
    {
        if (lastPlayerInArea == other.transform)
        {
            lastPlayerInArea = null;
        }
    }

    // F 交互和关卡事件都可以从 Inspector 绑定到这个方法。
    public void Play()
    {
        TryPlay();
    }

    // 检查此触发器是否可用，向控制器发起请求，成功后标记已播放。
    private bool TryPlay()
    {
        if (!isActiveAndEnabled || (playOnce && played) || controller == null)
        {
            return false;
        }

        if (!controller.TryPlay(dialogueFile, SelectEntry(), NotifyFinished))
        {
            return false;
        }

        played = true;
        return true;
    }

    // 依次检查剧情条件，未命中时使用默认入口。
    private string SelectEntry()
    {
        foreach (StoryRoute route in routes)
        {
            if (route != null && controller.HasStoryValue(route.key, route.value))
            {
                return route.entry;
            }
        }

        return defaultEntry;
    }

    // 收到控制器的完成回调后，执行 Inspector 中配置的事件。
    private void NotifyFinished()
    {
        completed = true;
        onFinished.Invoke();
    }
}
