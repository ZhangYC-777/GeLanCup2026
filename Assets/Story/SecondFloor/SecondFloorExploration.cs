using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.UI;

// 二层探索路线：左门、无光迷宫、补给箱、铁门、追猎者与兄弟的牺牲。
// 与一层一致，每段台词各自有触发器，读档时按已完成的段落恢复演出。
[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public sealed class SecondFloorExploration : MonoBehaviour
{
    private enum Phase
    {
        // 等待到达对话和兄弟跑到探索门。
        Waiting,
        // 正在播放剧情或走位。
        Busy,
        // 玩家在无光迷宫里自由探索。
        Exploring,
        // 兄弟发现了箱子，等玩家自己走过去。
        ChestApproach,
        // 玩家正走向铁门。
        GateApproach,
        // 已经看过铁门，等待玩家上前推门。
        GatePush,
        // 兄弟已经要过铁锤，等待玩家在背包里提交。
        AwaitingTool,
        // 探索路线结束。
        Finished
    }

    [Header("队伍与界面")]
    [SerializeField] private SwitchPlayer party;
    [SerializeField] private PlayerMove protagonist;
    [SerializeField] private PlayerMove brother;
    [SerializeField] private DialogueController dialogue;
    [SerializeField] private InventoryController inventory;
    [SerializeField] private PuzzleRoomVisibility visibility;
    [SerializeField] private SceneSave sceneSave;
    [SerializeField] private Transform mainCamera;

    [Header("到达与剧情段落")]
    [SerializeField] private DialogueTrigger doorBeat;
    [SerializeField] private DialogueTrigger vastBeat;
    [SerializeField] private DialogueTrigger lostBeat;
    [SerializeField] private DialogueTrigger punchBeat;
    [SerializeField] private DialogueTrigger chestCallBeat;
    [SerializeField] private DialogueTrigger chestOpenBeat;
    [SerializeField] private DialogueTrigger gateBeat;
    [SerializeField] private DialogueTrigger gatePushBeat;
    [SerializeField] private DialogueTrigger hammerBeat;
    [SerializeField] private DialogueTrigger escapeBeat;
    [SerializeField] private DialogueTrigger endBeat;

    [Header("交互与区域")]
    [SerializeField] private PlayerHit gateInteraction;
    [SerializeField] private InventorySubmission gateSubmission;
    [SerializeField] private Collider2D punchArea;
    [SerializeField] private Collider2D chestArea;
    [SerializeField] private Collider2D gateApproachArea;

    [Header("走位点")]
    [SerializeField] private Transform doorBrotherMark;
    [SerializeField] private Transform doorProtagonistMark;
    [SerializeField] private Transform[] insidePath = new Transform[0];
    [SerializeField] private Transform lostMark;
    [SerializeField] private Transform punchMark;
    [SerializeField] private Transform chestBrotherMark;
    [SerializeField] private Transform chestProtagonistMark;
    [SerializeField] private Transform gatePushMark;
    [SerializeField] private Transform gateBrotherMark;
    [SerializeField] private Transform pushedClearMark;
    [SerializeField] private Transform interceptMark;
    [SerializeField] private Transform handoffMark;

    [Header("铁门与木箱")]
    [SerializeField] private Transform gateLeaf;
    [SerializeField] private Collider2D gateBlocker;
    [SerializeField] private Transform[] gearWheels = new Transform[0];
    [SerializeField] private SpriteRenderer chestRenderer;
    [SerializeField] private Sprite[] chestFrames = new Sprite[0];

    [Header("追猎者")]
    [SerializeField] private Transform eastShadow;
    [SerializeField] private Transform westShadow;
    [SerializeField] private SpriteRenderer[] eyeGlows = new SpriteRenderer[0];
    [SerializeField] private Sprite sparkSprite;

    [Header("遮罩与音效")]
    [SerializeField] private Image fade;
    [SerializeField] private AudioSource foley;
    [SerializeField] private AudioSource gateAudio;
    [SerializeField] private AudioClip hammerClip;
    [SerializeField] private AudioClip gearClip;
    [SerializeField] private AudioClip scrapeClip;
    [SerializeField] private AudioClip breathClip;

    [Header("节奏")]
    [SerializeField] private float walkSpeed = 4.6f;
    [SerializeField] private float followSpeed = 6.4f;
    [SerializeField, Min(5f)] private float wanderTimeout = 53f;
    // 给镜头做推拉用的虚拟相机与移动节点。
    [SerializeField] private CinemachineVirtualCamera gameCamera;
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private float cameraPanOutSeconds = 1.2f;
    [SerializeField] private float cameraPanBackSeconds = 1f;
    // 第三层场景接入后填这里；为空时探索路线在安全点收束。
    [SerializeField] private string nextScenePath = "";

    private const string HammerItemId = "iron_hammer";
    private const string ExitState = "secondFloorExplorationExit";
    private const string ExitReady = "ready";
    private const float FollowDistance = 1.05f;
    private const float SampleSpacing = .075f;

    private struct TrailPoint
    {
        public Vector3 position;
        public float distance;
    }

    private readonly List<TrailPoint> trail = new List<TrailPoint>();
    private Phase phase = Phase.Waiting;
    internal bool BlocksTimedHunter => phase == Phase.Busy;
    private Coroutine activeRoutine;
    private bool flashlightOn;
    private Vector2 flashlightDirection = Vector2.up;
    private Vector2 lastFlashlightPosition;
    private float exploreTime;
    private bool brotherFollowing;
    private bool playingHunterCue;
    private float trailDistance;
    private Vector3 lastTrailSample;
    private float shakeTime;
    private float shakeAmount;
    private Vector3 gateLeafStart;
    private Vector3 eastShadowStart;
    private Vector3 westShadowStart;
    private Collider2D protagonistCollider;
    private Collider2D brotherCollider;

    private void Awake()
    {
        if (gateLeaf != null)
        {
            gateLeafStart = gateLeaf.localPosition;
        }
        if (eastShadow != null)
        {
            eastShadowStart = eastShadow.localPosition;
        }
        if (westShadow != null)
        {
            westShadowStart = westShadow.localPosition;
        }
        if (chestRenderer != null && chestFrames.Length > 0)
        {
            chestRenderer.sprite = chestFrames[0];
        }
        foreach (SpriteRenderer eye in eyeGlows)
        {
            if (eye != null)
            {
                SetAlpha(eye, 0f);
            }
        }
        // 两团黑影是伏击用的，等齿轮声引来了东西再露面。
        if (eastShadow != null)
        {
            eastShadow.gameObject.SetActive(false);
        }
        if (westShadow != null)
        {
            westShadow.gameObject.SetActive(false);
        }
        if (gateInteraction != null)
        {
            gateInteraction.enabled = false;
        }

        protagonistCollider = protagonist != null ? protagonist.GetComponent<Collider2D>() : null;
        brotherCollider = brother != null ? brother.GetComponent<Collider2D>() : null;
        if (protagonistCollider != null && brotherCollider != null)
        {
            Physics2D.IgnoreCollision(protagonistCollider, brotherCollider, true);
        }

        // 兄弟在这条路线上一直由脚本带着走，碰撞只会让他在柱子边打转。
        if (brother != null)
        {
            brother.SetColliderEnabled(false);
        }
    }

    private void OnEnable()
    {
        dialogue.LineStarted += OnLineStarted;
    }

    private void OnDisable()
    {
        dialogue.LineStarted -= OnLineStarted;
        if (playingHunterCue)
        {
            dialogue.CanAdvanceLine = null;
        }
        brotherFollowing = false;
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }
    }

    // 按最后一次完成的段落恢复。位置、背包和 SAN 已由 SceneSave 读回。
    private void Start()
    {
        if (dialogue.HasStoryValue(SecondFloorArrival.RouteState, SecondFloorArrival.SacrificeRoute))
        {
            return;
        }

        // 旧存档在离开探索路线前还没有路线值，沿用原有的段落检查点。
        if (doorBeat.HasCompleted)
        {
            dialogue.SetStoryValue(SecondFloorArrival.RouteState, SecondFloorArrival.ExplorationRoute);
        }

        if (endBeat.HasCompleted)
        {
            FinishStory();
            return;
        }

        // 探索路线全程由主角带队，读完档也先收回切换权。
        party.SetSwitchingLocked(true);

        if (escapeBeat.HasCompleted)
        {
            RestoreAtSafePoint();
            activeRoutine = StartCoroutine(ReplayEnding());
            return;
        }

        if (hammerBeat.HasCompleted)
        {
            SetGateOpened(true, immediate: true);
            RestoreBrotherIntercepted();
            activeRoutine = StartCoroutine(ReplayEscape());
            return;
        }

        if (gatePushBeat.HasCompleted)
        {
            SetGateOpened(false, immediate: true);
            GrantHammer();
            EnterToolMode();
            return;
        }

        if (chestOpenBeat.HasCompleted)
        {
            GrantHammer();
            EnterGateApproach();
            return;
        }

        if (chestCallBeat.HasCompleted)
        {
            EnterChestApproach();
            return;
        }

        if (punchBeat.HasCompleted)
        {
            activeRoutine = StartCoroutine(ChestCallSequence());
            return;
        }

        if (lostBeat.HasCompleted)
        {
            StartExploring();
            return;
        }

        if (vastBeat.HasCompleted)
        {
            activeRoutine = StartCoroutine(WalkToLostPoint());
            return;
        }

        if (doorBeat.HasCompleted)
        {
            activeRoutine = StartCoroutine(WalkInside());
            return;
        }

        activeRoutine = StartCoroutine(BeginWhenArrivalDone());
    }

    private void Update()
    {
        UpdateFlashlight();

        if (phase == Phase.Exploring && !party.IsGameplayLocked && !dialogue.IsPlaying && party.CurrentPlayer != null)
        {
            exploreTime += Time.deltaTime;
            if (punchArea != null && punchArea.OverlapPoint(party.CurrentPlayer.position))
            {
                BeginPunch();
            }
            else if (exploreTime >= wanderTimeout)
            {
                BeginPunch();
            }
        }
        else if (phase == Phase.ChestApproach && party.CurrentPlayer != null &&
                 chestArea != null && chestArea.OverlapPoint(party.CurrentPlayer.position))
        {
            activeRoutine = StartCoroutine(ChestOpenSequence());
        }
        else if (phase == Phase.GateApproach && party.CurrentPlayer != null &&
                 gateApproachArea != null && gateApproachArea.OverlapPoint(party.CurrentPlayer.position))
        {
            activeRoutine = StartCoroutine(GateSequence());
        }

        UpdateFollow();
    }

    // 镜头抖动必须排在 Cinemachine 定位之后，只在本帧叠加偏移。
    private void LateUpdate()
    {
        if (shakeTime <= 0f || mainCamera == null)
        {
            return;
        }

        shakeTime = Mathf.Max(0f, shakeTime - Time.unscaledDeltaTime);
        float strength = shakeAmount * Mathf.Clamp01(shakeTime / .28f);
        mainCamera.position += (Vector3)(Random.insideUnitCircle * strength);
    }

    // 推门交互由 PlayerHit 的 On Interact 调用。
    public void InteractGate()
    {
        if (phase == Phase.GatePush && !dialogue.IsPlaying)
        {
            activeRoutine = StartCoroutine(GatePushSequence());
        }
        else if (phase == Phase.AwaitingTool && gateSubmission != null)
        {
            gateSubmission.Open();
        }
    }

    // 背包提交铁锤后由 InventorySubmission 的 On Accepted 调用。
    public void OnHammerAccepted()
    {
        if (phase == Phase.AwaitingTool)
        {
            activeRoutine = StartCoroutine(HammerSequence());
        }
    }

    // 主角靠近左门选定探索路线后，两人一起进门。
    private IEnumerator BeginWhenArrivalDone()
    {
        while (!dialogue.HasStoryValue(SecondFloorArrival.RouteState, SecondFloorArrival.ExplorationRoute))
        {
            if (dialogue.HasStoryValue(SecondFloorArrival.RouteState, SecondFloorArrival.SacrificeRoute))
            {
                activeRoutine = null;
                yield break;
            }

            yield return null;
        }

        party.SelectPlayer(protagonist);
        party.SetSwitchingLocked(true);
        phase = Phase.Busy;
        yield return EnterDoorSequence();
    }

    private IEnumerator EnterDoorSequence()
    {
        party.SetGameplayLocked(true);
        party.SetCameraFocus(brother.transform, snap: true);
        protagonistCollider = protagonist.GetComponent<Collider2D>();
        brotherCollider = brother.GetComponent<Collider2D>();
        if (protagonistCollider != null && brotherCollider != null)
        {
            Physics2D.IgnoreCollision(protagonistCollider, brotherCollider, true);
        }

        bool brotherThere = false;
        bool protagonistThere = false;
        while (!brotherThere || !protagonistThere)
        {
            brotherThere = MoveStep(brother, doorBrotherMark.position, walkSpeed);
            protagonistThere = MoveStep(protagonist, doorProtagonistMark.position, walkSpeed);
            yield return null;
        }

        yield return PlayBeat(doorBeat);
        if (!doorBeat.HasCompleted)
        {
            activeRoutine = null;
            yield break;
        }

        yield return WalkInside();
    }

    // 石门升起后两人走进通道，在第一间石室里第一次照见这个巨大的空间。
    private IEnumerator WalkInside()
    {
        phase = Phase.Busy;
        party.SetGameplayLocked(true);
        party.SetCameraFocus(protagonist.transform, snap: true);
        flashlightOn = true;
        lastFlashlightPosition = protagonist.SavePosition;
        // 等石门动画完全升起，避免人物穿过还没打开的石门。
        yield return new WaitForSeconds(.5f);

        foreach (Transform waypoint in insidePath)
        {
            if (waypoint == null)
            {
                continue;
            }

            // 兄弟跟在主角身后一点，偏移方向沿着来路，避免被推进墙角。
            Vector3 leaderTarget = waypoint.position;
            Vector3 followTarget = leaderTarget + (protagonist.SavePosition - leaderTarget).normalized * .9f;
            bool protagonistThere = false;
            bool brotherThere = false;
            while (!protagonistThere || !brotherThere)
            {
                protagonistThere = MoveStep(protagonist, leaderTarget, walkSpeed);
                brotherThere = MoveStep(brother, followTarget, walkSpeed);
                yield return null;
            }
        }

        yield return PlayBeat(vastBeat);
        if (!vastBeat.HasCompleted)
        {
            activeRoutine = null;
            yield break;
        }

        yield return WalkToLostPoint();
    }

    // 一边往迷宫深处走，一边播放“时间被拉长”的旁白。
    private IEnumerator WalkToLostPoint()
    {
        phase = Phase.Busy;
        party.SetCameraFocus(protagonist.transform);
        flashlightOn = true;
        lastFlashlightPosition = protagonist.SavePosition;
        Coroutine walk = StartCoroutine(WalkToLostMark());
        yield return PlayBeat(lostBeat);
        yield return walk;
        if (!isActiveAndEnabled)
        {
            yield break;
        }

        StartExploring();
        activeRoutine = null;
    }

    private IEnumerator WalkToLostMark()
    {
        Vector3 leaderTarget = lostMark.position;
        Vector3 followTarget = leaderTarget + (protagonist.SavePosition - leaderTarget).normalized * .9f;
        bool protagonistThere = false;
        bool brotherThere = false;
        while (!protagonistThere || !brotherThere)
        {
            protagonistThere = MoveStep(protagonist, leaderTarget, walkSpeed);
            brotherThere = MoveStep(brother, followTarget, walkSpeed);
            yield return null;
        }
    }

    // 迷宫探索阶段：手电筒、兄弟跟随、被打转的焦虑计时。
    private void StartExploring()
    {
        phase = Phase.Exploring;
        exploreTime = 0f;
        flashlightOn = true;
        lastFlashlightPosition = protagonist.SavePosition;
        brotherFollowing = true;
        ResetTrail(protagonist.SavePosition);
        party.SetCameraFocus(protagonist.transform);
        party.SetGameplayLocked(false);
        if (protagonistCollider != null && brotherCollider != null)
        {
            Physics2D.IgnoreCollision(protagonistCollider, brotherCollider, true);
        }
    }

    private void BeginPunch()
    {
        if (phase != Phase.Exploring)
        {
            return;
        }

        activeRoutine = StartCoroutine(PunchSequence());
    }

    // 主角在石壁前爆发，砸墙的震动和声音之后转到兄弟发现的木箱。
    private IEnumerator PunchSequence()
    {
        phase = Phase.Busy;
        brotherFollowing = false;
        party.SetGameplayLocked(true);
        party.SetCameraFocus(protagonist.transform, snap: true);

        bool far = Vector3.Distance(protagonist.SavePosition, punchMark.position) > 3.5f;
        if (far)
        {
            yield return FadeTo(1f, .22f);
        }
        protagonist.RestorePosition(punchMark.position);
        PlaceBrother(punchMark.position + new Vector3(.9f, -.15f, 0f));
        yield return null;
        if (far)
        {
            yield return FadeTo(0f, .28f);
        }

        yield return PlayBeat(punchBeat);
        if (!punchBeat.HasCompleted)
        {
            activeRoutine = null;
            yield break;
        }

        yield return ChestCallSequence();
    }

    // 兄弟在木箱旁喊话。镜头先转到他身上标出位置，再回到主角，让玩家自己走过去。
    private IEnumerator ChestCallSequence()
    {
        phase = Phase.Busy;
        brotherFollowing = false;
        party.SetGameplayLocked(true);

        // 镜头缓慢推到木箱那侧，中途再把兄弟放过去，玩家不会看到他凭空消失。
        yield return PanCamera(protagonist.SavePosition, chestBrotherMark.position,
            cameraPanOutSeconds, () => brother.RestorePosition(chestBrotherMark.position));
        yield return null;

        yield return PlayBeat(chestCallBeat);
        if (!chestCallBeat.HasCompleted)
        {
            activeRoutine = null;
            yield break;
        }

        // 交代完位置后缓慢拉回主角，接下来的路由玩家自己走。
        yield return PanCamera(chestBrotherMark.position, protagonist.SavePosition,
            cameraPanBackSeconds, null);
        EnterChestApproach();
        activeRoutine = null;
    }

    // 镜头回到主角。木箱的位置已经标过，剩下的路由玩家自己走。
    private void EnterChestApproach()
    {
        phase = Phase.ChestApproach;
        brotherFollowing = false;
        brother.RestorePosition(chestBrotherMark.position);
        party.SetCameraFocus(protagonist.transform, snap: true);
        party.SetGameplayLocked(false);
        ResetTrail(protagonist.SavePosition);
    }

    // 玩家走到箱边，兄弟撬锁，铁锤入包。
    private IEnumerator ChestOpenSequence()
    {
        phase = Phase.Busy;
        brotherFollowing = false;
        party.SetGameplayLocked(true);
        party.SetCameraFocus(protagonist.transform, snap: true);

        bool protagonistThere = false;
        while (!protagonistThere)
        {
            protagonistThere = MoveStep(protagonist, chestProtagonistMark.position, walkSpeed);
            yield return null;
        }

        brother.RestorePosition(chestBrotherMark.position);
        party.SetCameraFocus(chestRenderer.transform, snap: true);
        yield return null;
        yield return PlayBeat(chestOpenBeat);

        if (chestOpenBeat.HasCompleted)
        {
            // 开箱特写结束后缓慢拉回主角再交还操作。
            yield return PanCamera(chestRenderer.transform.position, protagonist.SavePosition,
                cameraPanBackSeconds, null);
            EnterGateApproach();
        }
        activeRoutine = null;
    }

    // 用移动节点做匀速推拉，推拉期间取消跟随阻尼，避免镜头落在节点后面。
    private IEnumerator PanCamera(Vector3 from, Vector3 to, float duration, System.Action midpoint)
    {
        if (gameCamera == null || cameraPivot == null || duration <= 0f)
        {
            if (midpoint != null)
            {
                midpoint();
            }
            yield break;
        }

        cameraPivot.position = from;
        party.SetCameraFocus(cameraPivot, snap: true);

        CinemachineFramingTransposer body = gameCamera.GetCinemachineComponent<CinemachineFramingTransposer>();
        float dampingX = 0f;
        float dampingY = 0f;
        if (body != null)
        {
            dampingX = body.m_XDamping;
            dampingY = body.m_YDamping;
            body.m_XDamping = 0f;
            body.m_YDamping = 0f;
        }

        bool fired = false;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            cameraPivot.position = Vector3.Lerp(from, to, t);
            if (!fired && midpoint != null && t >= .45f)
            {
                midpoint();
                fired = true;
            }
            yield return null;
        }

        cameraPivot.position = to;
        if (!fired && midpoint != null)
        {
            midpoint();
        }

        if (body != null)
        {
            body.m_XDamping = dampingX;
            body.m_YDamping = dampingY;
        }
    }

    private void EnterGateApproach()
    {
        phase = Phase.GateApproach;
        gateInteraction.enabled = false;
        brotherFollowing = true;
        ResetTrail(protagonist.SavePosition);
        // 木箱演出把镜头交给了布景，这里必须还给主角，否则之后镜头不会再跟人。
        party.SetCameraFocus(protagonist.transform, snap: true);
        party.SetGameplayLocked(false);
    }

    // 读档后如果木箱还没开过，补发铁锤，保证后面的提交环节可用。
    private void GrantHammer()
    {
        if (inventory != null)
        {
            inventory.TryGrant(HammerItemId);
        }
    }

    // 走进铁门厅后的一段说明，之后把推门交还玩家。
    private IEnumerator GateSequence()
    {
        phase = Phase.Busy;
        brotherFollowing = false;
        party.SetGameplayLocked(true);
        party.SetCameraFocus(gateLeaf, snap: true);
        yield return PlayBeat(gateBeat);
        if (!gateBeat.HasCompleted)
        {
            activeRoutine = null;
            yield break;
        }

        phase = Phase.GatePush;
        gateInteraction.enabled = true;
        party.SetCameraFocus(protagonist.transform);
        party.SetGameplayLocked(false);
        brotherFollowing = true;
        ResetTrail(protagonist.SavePosition);
        activeRoutine = null;
    }

    // 主角推门失败，兄弟要过铁锤。
    private IEnumerator GatePushSequence()
    {
        phase = Phase.Busy;
        brotherFollowing = false;
        party.SetGameplayLocked(true);
        party.SetSwitchingLocked(true);

        bool protagonistThere = false;
        bool brotherThere = false;
        // 兄弟通常跟在身后；如果被拉开太远就直接落位，避免演出里穿墙。
        if (Vector3.Distance(brother.SavePosition, gateBrotherMark.position) > 3.5f)
        {
            brother.RestorePosition(gateBrotherMark.position);
            brotherThere = true;
        }
        while (!protagonistThere || !brotherThere)
        {
            protagonistThere = MoveStep(protagonist, gatePushMark.position, walkSpeed);
            brotherThere = MoveStep(brother, gateBrotherMark.position, walkSpeed);
            yield return null;
        }

        party.SetCameraFocus(gateLeaf, snap: true);
        yield return PlayBeat(gatePushBeat);
        if (!gatePushBeat.HasCompleted)
        {
            activeRoutine = null;
            yield break;
        }

        EnterToolMode();
        activeRoutine = null;
    }

    // 需要工具时先让玩家打开背包提交铁锤，提交成功后才开始砸门演出。
    private void EnterToolMode()
    {
        phase = Phase.AwaitingTool;
        gateInteraction.enabled = true;
        party.SetCameraFocus(protagonist.transform);
        party.SetGameplayLocked(false);
        brotherFollowing = true;
        ResetTrail(protagonist.SavePosition);
    }

    // 铁锤与齿轮：火花、撞击、红眼亮起，最后铁门被砸开、黑影扑向主角。
    private IEnumerator HammerSequence()
    {
        phase = Phase.Busy;
        brotherFollowing = false;
        party.SetGameplayLocked(true);
        party.SetSwitchingLocked(true);
        gateInteraction.enabled = false;
        party.SelectPlayer(protagonist);

        protagonist.RestorePosition(gatePushMark.position);
        brother.RestorePosition(gateBrotherMark.position);
        party.SetCameraFocus(gateBrotherMark, snap: true);
        yield return null;

        yield return PlayBeat(hammerBeat);
        if (!hammerBeat.HasCompleted)
        {
            activeRoutine = null;
            yield break;
        }

        // 等砸门和开门的分支动作收尾。
        yield return new WaitForSeconds(1.1f);
        yield return EscapeSequence();
    }

    // 黑影扑向主角，兄弟推开他并留下。台词结束后黑场转到安全点。
    private IEnumerator EscapeSequence()
    {
        phase = Phase.Busy;
        party.SetGameplayLocked(true);
        party.SetCameraFocus(brother.transform, snap: true);
        yield return PlayBeat(escapeBeat);
        if (!escapeBeat.HasCompleted)
        {
            activeRoutine = null;
            yield break;
        }

        yield return EndingSequence();
    }

    private IEnumerator ReplayEscape()
    {
        phase = Phase.Busy;
        party.SetGameplayLocked(true);
        party.SetCameraFocus(brother.transform, snap: true);
        yield return PlayBeat(escapeBeat);
        if (escapeBeat.HasCompleted)
        {
            yield return EndingSequence();
        }
    }

    // 只差最后一段台词时，从安全点继续。
    private IEnumerator ReplayEnding()
    {
        yield return PlayBeat(endBeat);
        FinishStory();
    }

    // 黑场后主角已经穿过了铁门，兄弟的惨叫声被隔绝在门后。
    private IEnumerator EndingSequence()
    {
        yield return FadeTo(1f, .55f);
        brotherFollowing = false;
        protagonist.RestorePosition(handoffMark.position);
        brother.gameObject.SetActive(false);
        party.SelectPlayer(protagonist);
        party.SetCameraFocus(protagonist.transform, snap: true);
        flashlightOn = true;
        lastFlashlightPosition = protagonist.SavePosition;
        HideShadows();
        SetGateOpened(true, immediate: true);
        yield return new WaitForSeconds(.25f);
        yield return FadeTo(0f, .6f);

        yield return PlayBeat(endBeat);
        FinishStory();
    }

    // 已经完成整条路线时，把场景恢复成兄弟已经不在的状态。
    private void FinishStory()
    {
        SetGateOpened(true, immediate: true);
        brotherFollowing = false;
        brother.gameObject.SetActive(false);
        HideShadows();
        gateInteraction.enabled = false;
        flashlightOn = true;
        party.SetSwitchingLocked(false);
        party.SetGameplayLocked(false);
        dialogue.SetStoryValue(SecondFloorArrival.RouteState, SecondFloorArrival.ExplorationRoute);
        dialogue.SetStoryValue(ExitState, ExitReady);
        phase = Phase.Finished;
        sceneSave.CaptureCheckpoint();
        activeRoutine = null;

        if (!string.IsNullOrEmpty(nextScenePath))
        {
            sceneSave.LoadNextPartyScene(nextScenePath);
        }
    }

    private void RestoreAtSafePoint()
    {
        SetGateOpened(true, immediate: true);
        brother.gameObject.SetActive(false);
        HideShadows();
        protagonist.RestorePosition(handoffMark.position);
        party.SelectPlayer(protagonist);
        party.SetCameraFocus(protagonist.transform, snap: true);
        flashlightOn = true;
        lastFlashlightPosition = protagonist.SavePosition;
        phase = Phase.Busy;
    }

    private void RestoreBrotherIntercepted()
    {
        protagonist.RestorePosition(pushedClearMark.position);
        PlaceBrother(interceptMark.position);
        if (eastShadow != null && interceptMark != null)
        {
            eastShadow.position = interceptMark.position + Vector3.right * .55f;
            eastShadow.gameObject.SetActive(true);
        }
        party.SelectPlayer(protagonist);
        party.SetCameraFocus(brother.transform, snap: true);
        SetEyesAlpha(1f);
    }

    // 台词节点到达时同步布置画面和声音。
    private void OnLineStarted(string nodeId)
    {
        switch (nodeId)
        {
            case "explore_punch":
                Shake(.16f, .1f);
                Play(hammerClip, .5f, .55f);
                break;
            case "explore_chest_open":
                StartCoroutine(OpenChest());
                Play(scrapeClip, .34f, .9f);
                break;
            case "explore_hammer":
                StartCoroutine(HammeringLoop());
                break;
            case "explore_hammer_scrape":
                StartCoroutine(PlayHunterCue(RevealHunters()));
                break;
            case "explore_hammer_final":
                Play(hammerClip, .9f, .42f);
                Shake(.22f, .18f);
                break;
            case "explore_gate_burst":
                StartCoroutine(PlayHunterCue(OpenGateAndLunge()));
                break;
            case "explore_hesitate":
                sceneSave.PartySan.TakeDamage(PartySan.StandardDamage);
                break;
            case "explore_inner":
                party.SetCameraFocus(protagonist.transform, snap: true);
                break;
        }
    }

    // 红眼亮起和扑击着地前，允许补全台词，但先等动作结束再进下一句。
    private IEnumerator PlayHunterCue(IEnumerator cue)
    {
        playingHunterCue = true;
        dialogue.CanAdvanceLine = () => !playingHunterCue;
        yield return cue;
        playingHunterCue = false;
        dialogue.CanAdvanceLine = null;
    }

    private IEnumerator HammeringLoop()
    {
        for (int hit = 0; hit < 3; hit++)
        {
            Play(hammerClip, .85f, .5f + hit * .03f);
            SpawnSparks(gateBrotherMark.position + new Vector3(-.25f, .35f, 0f), 7);
            Shake(.12f, .12f);
            foreach (Transform wheel in gearWheels)
            {
                if (wheel != null)
                {
                    wheel.localRotation *= Quaternion.Euler(0f, 0f, -18f);
                }
            }
            yield return new WaitForSeconds(.42f);
            Play(gearClip, .3f, .8f);
            yield return new WaitForSeconds(.28f);
        }
    }

    private IEnumerator OpenGateAndLunge()
    {
        gateAudio.Play();
        Shake(.3f, .28f);
        StartCoroutine(OpenGateAnimation());
        SpawnSparks(gateLeaf.position + new Vector3(0f, .6f, 0f), 12);

        // 黑影从东侧凹室扑出，兄弟推开主角。
        if (eastShadow != null)
        {
            eastShadow.gameObject.SetActive(true);
            Vector3 from = eastShadow.position;
            Vector3 to = interceptMark != null ? interceptMark.position + Vector3.right * .55f : from;
            for (float t = 0f; t < .45f; t += Time.deltaTime)
            {
                eastShadow.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / .45f));
                yield return null;
            }
            eastShadow.position = to;
        }

        if (protagonist != null && pushedClearMark != null)
        {
            protagonist.RestorePosition(pushedClearMark.position);
        }
        PlaceBrother(interceptMark != null ? interceptMark.position : brother.SavePosition);
        Play(breathClip, .8f, .6f);
        Shake(.2f, .2f);
        SetEyesAlpha(1f);
    }

    private IEnumerator OpenGateAnimation()
    {
        Vector3 start = gateLeaf.localPosition;
        Vector3 end = gateLeafStart + Vector3.up * 2.2f;
        SpriteRenderer[] pieces = gateLeaf.GetComponentsInChildren<SpriteRenderer>(true);
        for (float elapsed = 0f; elapsed < .9f; elapsed += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / .9f);
            gateLeaf.localPosition = Vector3.Lerp(start, end, t);
            foreach (SpriteRenderer piece in pieces)
            {
                SetAlpha(piece, 1f - t);
            }
            if (t > .55f && gateBlocker != null)
            {
                gateBlocker.enabled = false;
            }
            yield return null;
        }

        gateLeaf.gameObject.SetActive(false);
        if (gateBlocker != null)
        {
            gateBlocker.enabled = false;
        }
    }

    // 读档时直接把铁门恢复成已砸开或未砸开的样子。
    private void SetGateOpened(bool open, bool immediate)
    {
        if (gateLeaf == null)
        {
            return;
        }

        gateLeaf.gameObject.SetActive(!open);
        gateLeaf.localPosition = gateLeafStart;
        foreach (SpriteRenderer piece in gateLeaf.GetComponentsInChildren<SpriteRenderer>(true))
        {
            SetAlpha(piece, 1f);
        }
        if (gateBlocker != null)
        {
            gateBlocker.enabled = !open;
        }
        if (immediate && open)
        {
            foreach (Transform wheel in gearWheels)
            {
                if (wheel != null)
                {
                    wheel.localRotation *= Quaternion.Euler(0f, 0f, -54f);
                }
            }
        }
    }

    private IEnumerator RevealHunters()
    {
        Play(scrapeClip, .7f, .7f);
        Play(breathClip, .5f, .5f);
        SetEyesAlpha(0f);
        for (float elapsed = 0f; elapsed < 1.2f; elapsed += Time.deltaTime)
        {
            SetEyesAlpha(Mathf.Clamp01(elapsed / 1.2f));
            yield return null;
        }
        SetEyesAlpha(1f);
        if (westShadow != null)
        {
            westShadow.gameObject.SetActive(true);
        }
    }

    private void HideShadows()
    {
        if (eastShadow != null)
        {
            eastShadow.gameObject.SetActive(false);
            eastShadow.localPosition = eastShadowStart;
        }
        if (westShadow != null)
        {
            westShadow.gameObject.SetActive(false);
            westShadow.localPosition = westShadowStart;
        }
        SetEyesAlpha(0f);
    }

    private void SetEyesAlpha(float alpha)
    {
        foreach (SpriteRenderer eye in eyeGlows)
        {
            if (eye != null)
            {
                SetAlpha(eye, alpha);
            }
        }
    }

    private IEnumerator OpenChest()
    {
        if (chestRenderer == null || chestFrames.Length == 0)
        {
            yield break;
        }

        for (int frame = 0; frame < chestFrames.Length; frame++)
        {
            chestRenderer.sprite = chestFrames[frame];
            yield return new WaitForSeconds(.12f);
        }
    }

    // 手电筒跟随当前角色，照向最近的移动方向。
    private void UpdateFlashlight()
    {
        if (!flashlightOn || visibility == null || protagonist == null)
        {
            return;
        }

        Vector2 position = protagonist.transform.position;
        Vector2 movement = position - lastFlashlightPosition;
        if (movement.sqrMagnitude > .0004f)
        {
            flashlightDirection = movement.normalized;
            lastFlashlightPosition = position;
        }

        visibility.SetFlashlight(true, position, flashlightDirection);
    }

    // 兄弟沿着主角走过的轨迹跟进，绕过墙角时也不会穿墙。
    private void UpdateFollow()
    {
        if (!brotherFollowing || party.IsGameplayLocked || protagonist == null || brother == null)
        {
            return;
        }

        Vector3 leader = protagonist.SavePosition;
        float delta = Vector3.Distance(lastTrailSample, leader);
        if (delta >= SampleSpacing)
        {
            trailDistance += delta;
            trail.Add(new TrailPoint { position = leader, distance = trailDistance });
            lastTrailSample = leader;
        }

        float remaining = Time.deltaTime * followSpeed;
        Vector3 position = brother.SavePosition;
        while (remaining > 0f && trail.Count > 0 && trail[0].distance <= trailDistance - FollowDistance)
        {
            Vector3 target = trail[0].position;
            float distanceToTarget = Vector3.Distance(position, target);
            position = Vector3.MoveTowards(position, target, remaining);
            if (distanceToTarget > remaining)
            {
                break;
            }

            remaining -= distanceToTarget;
            trail.RemoveAt(0);
        }

        brother.MoveForStory(position);
    }

    private void ResetTrail(Vector3 start)
    {
        trail.Clear();
        trailDistance = 0f;
        lastTrailSample = start;
        trail.Add(new TrailPoint { position = start, distance = 0f });
    }

    // 兄弟在演出站位时直接落位，避免穿过布景。
    private void PlaceBrother(Vector3 position)
    {
        brother.RestorePosition(position);
        brotherFollowing = false;
        ResetTrail(protagonist.SavePosition);
    }

    private IEnumerator PlayBeat(DialogueTrigger beat)
    {
        if (beat == null || beat.HasCompleted)
        {
            yield break;
        }

        party.SetGameplayLocked(false);
        beat.Play();
        yield return null;
        if (!dialogue.IsPlaying && !beat.HasCompleted)
        {
            Debug.LogError("剧情段落未能播放：" + beat.name, beat);
            yield break;
        }

        while (!beat.HasCompleted)
        {
            yield return null;
        }
    }

    private bool MoveStep(PlayerMove actor, Vector3 target, float speed)
    {
        Vector3 next = Vector3.MoveTowards(actor.SavePosition, target, speed * Time.deltaTime);
        actor.MoveForStory(next);
        return next == target;
    }

    private IEnumerator FadeTo(float target, float duration)
    {
        if (fade == null)
        {
            yield break;
        }

        Color color = fade.color;
        float start = color.a;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            color.a = Mathf.Lerp(start, target, elapsed / duration);
            fade.color = color;
            yield return null;
        }

        color.a = target;
        fade.color = color;
    }

    private void Shake(float duration, float amount)
    {
        shakeTime = Mathf.Max(shakeTime, duration);
        shakeAmount = Mathf.Max(shakeAmount, amount);
    }

    private void Play(AudioClip clip, float volume, float pitch)
    {
        if (foley == null || clip == null)
        {
            return;
        }

        foley.pitch = pitch;
        foley.PlayOneShot(clip, volume);
    }

    // 火花用一个短命的 SpriteRenderer 表现，不留下运行时资源。
    private void SpawnSparks(Vector3 origin, int count)
    {
        if (sparkSprite == null)
        {
            return;
        }

        StartCoroutine(SparksRoutine(origin, count));
    }

    private IEnumerator SparksRoutine(Vector3 origin, int count)
    {
        var sparks = new List<SpriteRenderer>(count);
        for (int i = 0; i < count; i++)
        {
            var item = new GameObject("Hammer Spark");
            item.transform.SetParent(transform, false);
            item.transform.position = origin + (Vector3)(Random.insideUnitCircle * .35f);
            var renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = sparkSprite;
            renderer.color = new Color(1f, .72f, .34f, 1f);
            renderer.sortingOrder = -5900;
            item.transform.localScale = Vector3.one * Random.Range(.08f, .16f);
            sparks.Add(renderer);
        }

        for (float elapsed = 0f; elapsed < .4f; elapsed += Time.deltaTime)
        {
            float t = elapsed / .4f;
            foreach (SpriteRenderer spark in sparks)
            {
                if (spark == null)
                {
                    continue;
                }

                Color color = spark.color;
                color.a = 1f - t;
                spark.color = color;
                spark.transform.localScale *= 1f + Time.deltaTime * 1.4f;
                spark.transform.position += Vector3.up * Time.deltaTime * .5f;
            }
            yield return null;
        }

        foreach (SpriteRenderer spark in sparks)
        {
            if (spark != null)
            {
                Destroy(spark.gameObject);
            }
        }
    }

    private static void SetAlpha(SpriteRenderer renderer, float alpha)
    {
        Color color = renderer.color;
        color.a = alpha;
        renderer.color = color;
    }
}
