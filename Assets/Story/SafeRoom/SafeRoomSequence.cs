using System.Collections;
using UnityEngine;

// 一层安全房的进场、纸条、入口关门和两座分流门升起。
[DisallowMultipleComponent]
public sealed class SafeRoomSequence : MonoBehaviour
{
    [SerializeField] private SwitchPlayer switchPlayer;
    [SerializeField] private DialogueController dialogue;
    [SerializeField] private DialogueTrigger arrivalDialogue;
    [SerializeField] private DialogueTrigger noteDialogue;
    [SerializeField] private DialogueTrigger doorsDialogue;

    [SerializeField] private PlayerMove[] party;
    [SerializeField] private Transform[] arrivalMarks;
    [SerializeField] private PlayerMove brother;
    [SerializeField] private Transform noteMark;
    [SerializeField] private GameObject floorNote;
    [SerializeField] private PlayerMove protagonist;
    [SerializeField] private Transform entranceCheckMark;
    [SerializeField, Min(0.1f)] private float walkSpeed = 3.2f;

    [SerializeField] private Animator entranceAnimator;
    [SerializeField] private Collider2D entranceCollider;
    [SerializeField] private ProximityStoneDoor[] routeDoors;
    [SerializeField] private Transform puzzleRouteLayout;
    [SerializeField] private Transform sacrificeDoor;

    internal const int RouteLayoutVersion = 1;
    private static readonly int OpenParameter = Animator.StringToHash("open");
    private static readonly int OpenState = Animator.StringToHash("DoorNSOpen");
    private static readonly int ClosedState = Animator.StringToHash("DoorNSClosed");
    private Vector3[] entryPositions;

    internal bool DoorsReadyForExploration
    {
        get
        {
            if (!doorsDialogue.HasCompleted)
            {
                return false;
            }
            foreach (ProximityStoneDoor door in routeDoors)
            {
                if (!door.IsRaised)
                {
                    return false;
                }
            }
            return true;
        }
    }

    private void Awake()
    {
        // 保存场景中的开场站位。旧存档缺少演出记录时也能从入口重播。
        entryPositions = new Vector3[party.Length];
        for (int i = 0; i < party.Length; i++)
        {
            entryPositions[i] = party[i].transform.position;
        }
    }

    private void OnEnable()
    {
        dialogue.LineStarted += OnLineStarted;
    }

    private void OnDisable()
    {
        dialogue.LineStarted -= OnLineStarted;
    }

    private void Start()
    {
        floorNote.SetActive(!noteDialogue.HasCompleted);
        if (doorsDialogue.HasCompleted)
        {
            SetEntrance(false, immediate: true);
            Physics2D.SyncTransforms();
            foreach (ProximityStoneDoor door in routeDoors)
            {
                door.RestoreRaised();
            }
            return;
        }

        SetEntrance(true, immediate: true);
        if (noteDialogue.HasCompleted)
        {
            CloseAndCheckEntrance();
        }
        else if (arrivalDialogue.HasCompleted)
        {
            FindNote();
        }
        else
        {
            StartCoroutine(WalkInside());
        }
    }

    public void FindNote()
    {
        StartCoroutine(WalkToNote());
    }

    public void CloseAndCheckEntrance()
    {
        StartCoroutine(CheckClosedEntrance());
    }

    // 旧版解谜路线在左手侧。保留房间内的相对站位，测试通道中的角色回到献祭门前。
    internal void RestoreRoutePositions(SceneProgress progress)
    {
        if (progress.safeRoomRouteLayoutVersion >= RouteLayoutVersion)
        {
            return;
        }

        for (int i = 0; i < progress.playerPositions.Length; i++)
        {
            Vector3 position = progress.playerPositions[i];
            if (position.y >= 14f || position.y > 2.52f && position.x < -3.12f)
            {
                position.x += puzzleRouteLayout.position.x;
            }
            else if (position.y > 2.52f)
            {
                position.x = sacrificeDoor.position.x + (i - 1) * 0.8f;
                position.y = sacrificeDoor.position.y - 1.25f;
            }

            progress.playerPositions[i] = position;
        }

        progress.safeRoomRouteLayoutVersion = RouteLayoutVersion;
    }

    private IEnumerator WalkInside()
    {
        switchPlayer.SetGameplayLocked(true);
        switchPlayer.SetCameraFocus(protagonist.transform, snap: true);
        for (int i = 0; i < party.Length; i++)
        {
            party[i].RestorePosition(entryPositions[i]);
        }
        yield return null;

        bool walking;
        do
        {
            walking = false;
            for (int i = 0; i < party.Length; i++)
            {
                walking |= !WalkStep(party[i], arrivalMarks[i]);
            }
            yield return null;
        } while (walking);

        PlayDialogue(arrivalDialogue);
    }

    private IEnumerator WalkToNote()
    {
        switchPlayer.SetGameplayLocked(true);
        while (!WalkStep(brother, noteMark))
        {
            yield return null;
        }
        floorNote.SetActive(false);
        PlayDialogue(noteDialogue);
    }

    private IEnumerator CheckClosedEntrance()
    {
        switchPlayer.SetGameplayLocked(true);
        switchPlayer.SetCameraFocus(protagonist.transform);
        SetEntrance(false, immediate: false);
        while (!WalkStep(protagonist, entranceCheckMark))
        {
            yield return null;
        }
        yield return new WaitForSeconds(.25f);
        while (!WalkStep(protagonist, arrivalMarks[0]))
        {
            yield return null;
        }
        PlayDialogue(doorsDialogue);
    }

    private bool WalkStep(PlayerMove actor, Transform mark)
    {
        Vector3 position = Vector3.MoveTowards(actor.SavePosition, mark.position, walkSpeed * Time.deltaTime);
        actor.MoveForStory(position);
        return position == mark.position;
    }

    private void PlayDialogue(DialogueTrigger trigger)
    {
        switchPlayer.SetCameraFocus(switchPlayer.CurrentPlayer);
        switchPlayer.SetGameplayLocked(false);
        trigger.Play();
    }

    private void SetEntrance(bool open, bool immediate)
    {
        entranceCollider.enabled = !open;
        entranceAnimator.SetBool(OpenParameter, open);
        if (immediate)
        {
            entranceAnimator.Play(open ? OpenState : ClosedState, 0, 0f);
        }
    }

    private void OnLineStarted(string nodeId)
    {
        if (nodeId != "route_doors_rise")
        {
            return;
        }

        foreach (ProximityStoneDoor door in routeDoors)
        {
            door.BeginRise();
        }
    }
}
