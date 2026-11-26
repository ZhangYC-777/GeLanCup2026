using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 格兰岛外景的演出顺序、石门状态和到迷宫的出口。
[DisallowMultipleComponent]
public class IslandArrival : MonoBehaviour
{
    [SerializeField] private SwitchPlayer switchPlayer;
    [SerializeField] private DialogueController dialogue;
    [SerializeField] private DialogueTrigger arrivalDialogue;
    [SerializeField] private DialogueTrigger brotherCall;
    [SerializeField] private DialogueTrigger gateDialogue;

    [SerializeField] private PlayerMove brother;
    [SerializeField] private Transform brotherStart;
    [SerializeField] private Transform[] brotherWaypoints;
    [SerializeField, Min(0.1f)] private float brotherSpeed = 5.3f;
    [SerializeField] private PlayerMove protagonist;
    [SerializeField] private PlayerMove childhoodFriend;
    [SerializeField] private Collider2D gateDiscoveryArea;

    [SerializeField] private Animator gateAnimator;
    [SerializeField] private PlayerHit gateInteraction;
    [SerializeField, Min(0f)] private float gateOpeningSeconds = 0.44f;
    [SerializeField] private string nextScenePath = SceneSave.MainStoryDirectory + "Scene2.unity";

    private const float FollowDistance = 1.05f;
    private const float FollowSpeed = 6.5f;
    private const float SampleSpacing = 0.075f;
    private bool temporaryFollow;
    private Collider2D protagonistCollider;
    private Collider2D friendCollider;
    private bool transitioning;
    private bool gateOpening;
    private float gateReadyAt;

    private void OnEnable()
    {
        dialogue.LineStarted += OnLineStarted;
    }

    private void OnDisable()
    {
        dialogue.LineStarted -= OnLineStarted;
        StopFollowing();
    }

    // 从最新播完的对话恢复当前阶段，已过门前对话时保留读档位置。
    private void Start()
    {
        if (gateDialogue.HasCompleted)
        {
            gateAnimator.SetBool("open", true);
            gateAnimator.Play("DoorNSOpen", 0, 0f);
            gateInteraction.enabled = true;
        }
        else if (brotherCall.HasCompleted)
        {
            AllowApproach();
        }
        else if (arrivalDialogue.HasCompleted)
        {
            StartBrotherWalk();
        }
        else
        {
            arrivalDialogue.Play();
        }
    }

    // 登岛对话结束后，兄弟先独自沿泥路跑到石门。
    public void StartBrotherWalk()
    {
        StartCoroutine(WalkBrotherToGate());
    }

    private IEnumerator WalkBrotherToGate()
    {
        brother.RestorePosition(brotherStart.position);
        switchPlayer.SetCameraFocus(brother.transform);
        switchPlayer.SetGameplayLocked(true);
        yield return null;

        foreach (Transform waypoint in brotherWaypoints)
        {
            while (brother.SavePosition != waypoint.position)
            {
                brother.RestorePosition(Vector3.MoveTowards(brother.SavePosition, waypoint.position,
                    Time.deltaTime * brotherSpeed));
                yield return null;
            }
        }

        switchPlayer.SetGameplayLocked(false);
        if (!gateDiscoveryArea.OverlapPoint(brother.SavePosition))
        {
            Debug.LogError("兄弟的演出终点应位于石门发现区域内。", gateDiscoveryArea);
        }
        brotherCall.Play();
    }

    // 兄弟喊完后，主角由玩家操控。青梅的跟随只在这一段启用。
    public void AllowApproach()
    {
        if (temporaryFollow)
        {
            return;
        }

        temporaryFollow = true;
        switchPlayer.SetCameraFocus(protagonist.transform, snap: true);
        switchPlayer.SetSwitchingLocked(true);
        protagonistCollider = protagonist.GetComponent<Collider2D>();
        friendCollider = childhoodFriend.GetComponent<Collider2D>();
        Physics2D.IgnoreCollision(protagonistCollider, friendCollider, true);
        StartCoroutine(FollowUntilGate());
    }

    private IEnumerator FollowUntilGate()
    {
        var trail = new Queue<(Vector3 position, float distance)>();
        Vector3 lastSample = protagonist.SavePosition;
        float distanceWalked = 0f;
        trail.Enqueue((lastSample, 0f));

        while (true)
        {
            Vector3 leader = protagonist.SavePosition;
            float distance = Vector3.Distance(lastSample, leader);
            if (distance >= SampleSpacing)
            {
                distanceWalked += distance;
                trail.Enqueue((leader, distanceWalked));
                lastSample = leader;
            }

            float remaining = Time.deltaTime * FollowSpeed;
            Vector3 position = childhoodFriend.SavePosition;
            while (remaining > 0f && trail.Count > 0 && trail.Peek().distance <= distanceWalked - FollowDistance)
            {
                Vector3 target = trail.Peek().position;
                float distanceToTarget = Vector3.Distance(position, target);
                position = Vector3.MoveTowards(position, target, remaining);
                if (distanceToTarget > remaining)
                {
                    break;
                }
                remaining -= distanceToTarget;
                trail.Dequeue();
            }
            childhoodFriend.RestorePosition(position);

            // 进门前空地并等青梅赶上后，才接门前的对话。
            if (gateDiscoveryArea.OverlapPoint(leader) &&
                Vector3.SqrMagnitude(leader - position) <= 1.5f * 1.5f &&
                !switchPlayer.IsGameplayLocked)
            {
                StopFollowing();
                gateDialogue.Play();
                yield break;
            }

            yield return null;
        }
    }

    private void StopFollowing()
    {
        if (!temporaryFollow)
        {
            return;
        }

        temporaryFollow = false;
        switchPlayer.SetSwitchingLocked(false);
        if (protagonistCollider != null && friendCollider != null)
        {
            Physics2D.IgnoreCollision(protagonistCollider, friendCollider, false);
        }
    }

    // 最后一句开始时播放素材包里的石门开合动画。
    private void OnLineStarted(string nodeId)
    {
        if (nodeId == "push_the_gate" && !gateOpening)
        {
            gateOpening = true;
            gateReadyAt = Time.time + gateOpeningSeconds;
            gateAnimator.SetBool("open", true);
        }
    }

    // 对话结束由现有对话系统解锁移动，开门动画播完才显示进门提示。
    public void FinishGateDialogue()
    {
        StartCoroutine(EnableGateInteraction());
    }

    private IEnumerator EnableGateInteraction()
    {
        while (Time.time < gateReadyAt)
        {
            yield return null;
        }
        gateInteraction.enabled = true;
    }

    public void EnterMaze()
    {
        if (transitioning || !gateDialogue.HasCompleted || !gateInteraction.enabled)
        {
            return;
        }

        int sceneIndex = SceneUtility.GetBuildIndexByScenePath(nextScenePath);
        if (sceneIndex < 0)
        {
            Debug.LogError("迷宫入口场景未加入构建列表：" + nextScenePath, this);
            return;
        }

        transitioning = true;
        switchPlayer.SetGameplayLocked(true);
        SceneManager.LoadScene(sceneIndex);
    }
}
