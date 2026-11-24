using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// 运行时对话播放器，负责输入、打字机、跳转、选项和收尾。
public class DialogueController : MonoBehaviour
{
    // 同一个 F 键会根据当前播放状态执行不同操作。
    private enum PlaybackState
    {
        // 没有在播放剧情，此时 F 可以交互。
        Idle,
        // 正文逐字出现，此时 F 可以立即补全当前句。
        Typing,
        // 当前句已完整显示，等待 F 继续。
        WaitingForAdvance,
        // 正在显示选项，此时上下键切换选项，F 确认。
        Choosing
    }

    // 场景中的角色切换组件，提供当前角色、输入和控制权锁定。
    [SerializeField] private SwitchPlayer switchPlayer;
    // 负责显示当前对话的 UI 组件。
    [SerializeField] private DialogueView view;
    // 剧情奖励和提交界面共用的场景背包。
    [SerializeField] private InventoryController inventory;
    // 每秒显示的文本单位数，Inspector 最小值为 1。
    [SerializeField, Min(1f)] private float charactersPerSecond = 25f;
    // 可以用 F 交互的场景对象，范围重叠时取最近的一个。
    [SerializeField] private PlayerHit[] interactionPoints = new PlayerHit[0];

    // 各角色根物体下的 F 键提示，由控制器统一显示和隐藏。
    [SerializeField] private GameObject[] prompts = new GameObject[0];

    // 本次场景游玩中已经写入的剧情选择。
    private readonly StoryState story = new StoryState();
    // 记录当前对话处于空闲、打字、等待推进或选择。
    private PlaybackState state;
    // 当前剧情按节点 ID 建立的索引。
    private Dictionary<string, DialogueNode> nodes;
    // 此时正在播放的台词。
    private DialogueNode currentNode;
    // 自然播完后调用的回调。
    private Action onFinished;
    // 启动对话时的帧号，用来挡住同一帧的推进输入。
    private int openedFrame;
    // 当前高亮选项在数组中的编号，从 0 开始。
    private int selectedChoice;
    // 每个文本单位在原始字符串中的起始位置。
    private int[] characterStarts;
    // 本句开始打字的时间，以不受 Time Scale 影响的时间记录。
    private float typingStarted;
    // 已经显示的文本单位数，用来避免重复刷新 UI。
    private int visibleCharacters;

    // 处于任意对话状态时返回 true，供触发器和提示判断。
    public bool IsPlaying
    {
        get { return state != PlaybackState.Idle; }
    }

    // F 在场景和对话中共用。一次按键只处理当前所在的状态。
    private void Update()
    {
        // 有可操控角色时才处理这一帧的交互输入。
        if (switchPlayer == null || switchPlayer.CurrentPlayer == null)
        {
            return;
        }

        if (inventory != null && inventory.BlocksDialogueInput)
        {
            return;
        }

        // 先更新打字机，再处理这一帧的按键。
        if (state == PlaybackState.Typing)
        {
            UpdateTyping();
        }

        // 选项出现时，读取上下键并切换高亮。
        if (state == PlaybackState.Choosing)
        {
            if (switchPlayer.WasDialogueUpPressed())
            {
                MoveChoice(-1);
            }
            else if (switchPlayer.WasDialogueDownPressed())
            {
                MoveChoice(1);
            }
        }

        if (!switchPlayer.WasInteractPressed())
        {
            return;
        }

        // 空闲时 F 访问最近交互点，播放中 F 交给 Advance。
        if (!IsPlaying)
        {
            PlayerHit point = FindClosestPoint();
            if (point != null)
            {
                point.Interact();
            }
        }
        else if (Time.frameCount != openedFrame)
        {
            Advance();
        }
    }

    // 所有交互点共用头顶提示，由一个脚本在移动结束后更新。
    private void LateUpdate()
    {
        Transform player = null;
        if (!IsPlaying && FindClosestPoint() != null)
        {
            player = switchPlayer.CurrentPlayer;
        }

        PlayerHit.ShowPromptFor(prompts, player);
    }

    // 离开场景或停用脚本时，结束当前播放并释放控制权。
    private void OnDisable()
    {
        PlayerHit.ShowPromptFor(prompts, null);
        if (IsPlaying)
        {
            ClearPlayback();
        }
    }

    // 加载并检查剧情，检查通过后才接管角色输入。
    // file 是 Unity 中的剧情 JSON 文本资产。
    // entry 是指定入口 ID，留空时取 JSON 的 entry。
    // finished 是播完后执行的回调，可以传 null。
    public bool TryPlay(TextAsset file, string entry, Action finished)
    {
        // 播放中的对话和未配置完整的组件直接拒绝新的开始请求。
        if (IsPlaying || !isActiveAndEnabled || view == null || switchPlayer == null ||
            switchPlayer.IsGameplayLocked || file == null)
        {
            return false;
        }

        DialogueData dialogue;
        Dictionary<string, DialogueNode> indexedNodes;
        string error;
        if (!DialogueLoader.TryLoad(file.text, out dialogue, out indexedNodes, out error))
        {
            Debug.LogError(file.name + "：" + error, file);
            return false;
        }

        if (string.IsNullOrEmpty(entry))
        {
            entry = dialogue.entry;
        }

        // 核对触发器指定的入口也属于本文件。
        if (!indexedNodes.ContainsKey(entry))
        {
            Debug.LogError(file.name + " 找不到入口节点：" + entry, file);
            return false;
        }

        // 数据检查通过，保存上下文并锁定当前角色操作。
        nodes = indexedNodes;
        onFinished = finished;
        openedFrame = Time.frameCount;
        switchPlayer.SetGameplayLocked(true);
        EnterNode(entry);
        return true;
    }

    // 供区域检测判断进入者是否正由玩家操控。
    // player 是区域 Collider2D 对应的角色 Transform。
    public bool IsCurrentPlayer(Transform player)
    {
        return switchPlayer != null && switchPlayer.CurrentPlayer == player;
    }

    // 查询保存的剧情状态。key 是状态名，value 是要匹配的配置值。
    public bool HasStoryValue(string key, string value)
    {
        return story.Matches(key, value);
    }

    // 当两个范围重叠时，F 对最近的物体生效。
    private PlayerHit FindClosestPoint()
    {
        if (switchPlayer == null || switchPlayer.CurrentPlayer == null || switchPlayer.IsGameplayLocked)
        {
            return null;
        }

        Transform player = switchPlayer.CurrentPlayer;
        PlayerHit closest = null;
        float closestDistance = float.PositiveInfinity;

        // 过滤空引用和范围外的对象，再比较 XY 平面距离的平方。
        foreach (PlayerHit point in interactionPoints)
        {
            if (point == null || !point.IsInRange(player))
            {
                continue;
            }

            Vector2 offset = point.transform.position - player.position;
            float distance = offset.sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = point;
            }
        }

        return closest;
    }

    // 进入一句新台词，清空选项并从第一个字开始显示。
    // id 是本句节点 ID，空字符串或 null 表示本段结束。
    private void EnterNode(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            Finish();
            return;
        }

        // 切换到新节点并重置打字进度。
        currentNode = nodes[id];
        state = PlaybackState.Typing;
        characterStarts = StringInfo.ParseCombiningCharacters(currentNode.text);
        typingStarted = Time.unscaledTime;
        visibleCharacters = 0;
        view.ShowLine(currentNode.speaker);
    }

    // 按时间显示文本元素，避免把一个组合字符拆成两段。
    private void UpdateTyping()
    {
        // 经过秒数乘以每秒字数，向下取整得到应该显示多少。
        int count = Mathf.FloorToInt((Time.unscaledTime - typingStarted) * charactersPerSecond);
        if (count >= characterStarts.Length)
        {
            CompleteLine();
        }
        else if (count > visibleCharacters)
        {
            visibleCharacters = count;
            view.SetBody(currentNode.text.Substring(0, characterStarts[count]));
        }
    }

    // 写出完整台词，并等待玩家继续。
    private void CompleteLine()
    {
        view.SetBody(currentNode.text);
        state = PlaybackState.WaitingForAdvance;
    }

    // 打字时补全本句，显示完后才进入下一句或打开选项。
    private void Advance()
    {
        switch (state)
        {
            // 本句尚在打字，F 只补全当前句。
            case PlaybackState.Typing:
                CompleteLine();
                break;
            // 有选项就打开选项，无选项就按 next 跳转。
            case PlaybackState.WaitingForAdvance:
                if (currentNode.choices != null && currentNode.choices.Length > 0)
                {
                    selectedChoice = 0;
                    state = PlaybackState.Choosing;
                    view.ShowChoices(currentNode.choices, selectedChoice);
                }
                else
                {
                    EnterNode(currentNode.next);
                }
                break;
            // 确认高亮的选项。
            case PlaybackState.Choosing:
                Choose();
                break;
        }
    }

    // direction 用 -1 表示上一项，1 表示下一项。取余让首尾可以循环。
    private void MoveChoice(int direction)
    {
        int count = currentNode.choices.Length;
        selectedChoice = (selectedChoice + direction + count) % count;
        view.ShowChoices(currentNode.choices, selectedChoice);
    }

    // 先保存选择的影响，再跳到对应的台词。
    private void Choose()
    {
        // 读取高亮选项，按需保存状态后进入该选项指向的台词。
        DialogueChoice choice = currentNode.choices[selectedChoice];
        if (choice.set != null && !string.IsNullOrWhiteSpace(choice.set.key))
        {
            story.Set(choice.set.key, choice.set.value);
        }

        EnterNode(choice.next);
    }

    // 先结束播放和解锁，再执行回调，回调可以继续接其他事件。
    private void Finish()
    {
        Action callback = onFinished;
        string rewardItemId = currentNode.rewardItemId;
        ClearPlayback();

        if (!string.IsNullOrWhiteSpace(rewardItemId) &&
            (inventory == null || !inventory.TryGrant(rewardItemId)))
        {
            Debug.LogError("无法发放剧情物品，请检查场景背包的物品目录：" + rewardItemId, this);
        }

        if (callback != null)
        {
            callback();
        }
    }

    // 清空本段的临时数据、隐藏界面，并交还当前角色的控制权。
    private void ClearPlayback()
    {
        state = PlaybackState.Idle;
        nodes = null;
        currentNode = null;
        onFinished = null;
        characterStarts = null;

        if (view != null)
        {
            view.Hide();
        }

        if (switchPlayer != null)
        {
            switchPlayer.SetGameplayLocked(false);
        }
    }
}
