using System.Collections;
using UnityEngine;

// 二层安全房的开场对话和兄弟独自跑向探索门的演出。
[DisallowMultipleComponent]
public sealed class SecondFloorArrival : MonoBehaviour
{
    [SerializeField] private SwitchPlayer switchPlayer;
    [SerializeField] private DialogueTrigger openingDialogue;
    [SerializeField] private PlayerMove protagonist;
    [SerializeField] private PlayerMove brother;
    [SerializeField] private Transform protagonistAtStatue;
    [SerializeField] private Transform brotherAtStatue;
    [SerializeField] private Transform[] brotherRunMarks;
    [SerializeField] private Collider2D explorationDoorApproach;
    [SerializeField] private Collider2D sacrificeDoorApproach;
    [SerializeField, Min(0.1f)] private float arrivalSpeed = 5.5f;
    [SerializeField, Min(0.1f)] private float brotherRunSpeed = 6.8f;
    [SerializeField, Min(0f)] private float doorHoldSeconds = 0.35f;

    private const string ArrivalState = "second_floor_arrival";
    private const string BrotherAtDoor = "brother_at_exploration_door";
    internal const string RouteState = "secondFloorRoute";
    internal const string ExplorationRoute = "exploration";
    internal const string SacrificeRoute = "sacrifice";
    private Vector3 protagonistEntry;
    private Vector3 brotherEntry;
    private Coroutine brotherRun;

    // 在 SceneSave 恢复旧进度前记住楼梯上的初始站位。
    private void Awake()
    {
        protagonistEntry = protagonist.transform.position;
        brotherEntry = brother.transform.position;
        // 兄弟全程由演出脚本带走，关掉碰撞，避免被柱子顶回去卡在原地。
        brother.SetColliderEnabled(false);
    }

    // 继续游戏保留自由移动的位置，开场对话检查点则重播跑门演出。
    private void Start()
    {
        if (openingDialogue.Controller.HasStoryValue(ArrivalState, BrotherAtDoor) || HasChosenRoute())
        {
            return;
        }

        if (openingDialogue.HasCompleted)
        {
            StartBrotherRun();
        }
        else
        {
            StartCoroutine(EnterSanctuary());
        }
    }

    // 跑门演出结束后，由主角靠近哪扇门决定路线。
    private void Update()
    {
        if (!openingDialogue.Controller.HasStoryValue(ArrivalState, BrotherAtDoor) ||
            HasChosenRoute() || switchPlayer.IsGameplayLocked)
        {
            return;
        }

        Vector2 position = protagonist.SavePosition;
        if (sacrificeDoorApproach.OverlapPoint(position))
        {
            openingDialogue.Controller.SetStoryValue(RouteState, SacrificeRoute);
        }
        else if (explorationDoorApproach.OverlapPoint(position))
        {
            openingDialogue.Controller.SetStoryValue(RouteState, ExplorationRoute);
        }
    }

    private bool HasChosenRoute()
    {
        DialogueController dialogue = openingDialogue.Controller;
        return dialogue.HasStoryValue(RouteState, ExplorationRoute) ||
               dialogue.HasStoryValue(RouteState, SacrificeRoute);
    }

    // 两人走到圣女像前，再把输入交给对话播放器。
    private IEnumerator EnterSanctuary()
    {
        switchPlayer.SetGameplayLocked(true);
        switchPlayer.SelectPlayer(protagonist);
        protagonist.RestorePosition(protagonistEntry);
        brother.RestorePosition(brotherEntry);
        yield return null;

        bool protagonistArrived;
        bool brotherArrived;
        do
        {
            protagonistArrived = MoveStep(protagonist, protagonistAtStatue, arrivalSpeed);
            brotherArrived = MoveStep(brother, brotherAtStatue, arrivalSpeed);
            yield return null;
        } while (!protagonistArrived || !brotherArrived);

        switchPlayer.SetGameplayLocked(false);
        openingDialogue.Play();
    }

    // 最后一句对话播完后，同一帧锁住移动并切到兄弟视角。
    public void StartBrotherRun()
    {
        if (brotherRun == null)
        {
            brotherRun = StartCoroutine(RunToExplorationDoor());
        }
    }

    private IEnumerator RunToExplorationDoor()
    {
        switchPlayer.SetGameplayLocked(true);
        brother.RestorePosition(brotherAtStatue.position);
        switchPlayer.SetCameraFocus(brother.transform, snap: true);
        yield return null;

        foreach (Transform mark in brotherRunMarks)
        {
            while (!MoveStep(brother, mark, brotherRunSpeed))
            {
                yield return null;
            }
        }

        yield return new WaitForSeconds(doorHoldSeconds);
        openingDialogue.Controller.SetStoryValue(ArrivalState, BrotherAtDoor);
        switchPlayer.SelectPlayer(protagonist);
        switchPlayer.SetGameplayLocked(false);
        brotherRun = null;
    }

    // 演出站位使用已布置的路径点，刚体和画面保持同步。
    private static bool MoveStep(PlayerMove actor, Transform mark, float speed)
    {
        Vector3 position = Vector3.MoveTowards(actor.SavePosition, mark.position, speed * Time.deltaTime);
        actor.MoveForStory(position);
        return position == mark.position;
    }
}
