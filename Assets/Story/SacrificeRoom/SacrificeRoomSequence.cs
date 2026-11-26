using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 一层献祭路线。对话分段保存，继续游戏时从已完成的段落恢复机关和队伍。
[DisallowMultipleComponent]
public sealed class SacrificeRoomSequence : MonoBehaviour
{
    [Header("路线入口")]
    [SerializeField] private SwitchPlayer party;
    [SerializeField] private PuzzleRoomSequence puzzleRoute;
    [SerializeField] private DialogueController dialogue;
    [SerializeField] private SceneSave sceneSave;
    [SerializeField] private DialogueTrigger doorsRaised;
    [SerializeField] private Collider2D passageEntry;
    [SerializeField] private Rect waitingArea;
    [SerializeField, Min(1f)] private float decisionTimeout = 45f;

    [Header("对话")]
    [SerializeField] private DialogueTrigger decision;
    [SerializeField] private DialogueTrigger abduction;
    [SerializeField] private DialogueTrigger inscription;
    [SerializeField] private DialogueTrigger stairs;

    [Header("角色与站位")]
    [SerializeField] private PlayerMove protagonist;
    [SerializeField] private PlayerMove childhoodFriend;
    [SerializeField] private PlayerMove brother;
    [SerializeField] private Transform doorMark;
    [SerializeField] private Transform leadMark;
    [SerializeField] private Transform friendMark;
    [SerializeField] private Transform brotherMark;
    [SerializeField] private Transform grabMark;
    [SerializeField] private Transform bendMark;
    [SerializeField] private Transform redTurnMark;
    [SerializeField] private Transform landingMark;
    [SerializeField] private Transform brotherLandingMark;
    [SerializeField] private Collider2D stairArea;
    [SerializeField, Min(.1f)] private float walkSpeed = 3.2f;

    [Header("机关与灯光")]
    [SerializeField] private PuzzleRoomVisibility visibility;
    [SerializeField] private Transform closedSlab;
    [SerializeField] private GameObject openShaft;
    [SerializeField] private Transform loomingShadow;
    [SerializeField] private GameObject redLight;
    [SerializeField] private Text wallWriting;

    [Header("音效")]
    [SerializeField] private AudioSource foley;
    [SerializeField] private AudioClip scrape;
    [SerializeField] private AudioClip mechanism;
    [SerializeField] private AudioClip impact;
    [SerializeField] private AudioClip breath;

    private const string RouteKey = "firstFloorRoute";
    private const string RouteValue = "sacrifice";
    private Vector3 slabClosedPosition;
    private Vector3 friendRestScale;
    private Font writingFont;
    private Coroutine pullingFriend;
    private bool routeChosen;
    private bool shaftSealed;
    private float decisionTime;

    private void Awake()
    {
        slabClosedPosition = closedSlab.localPosition;
        friendRestScale = childhoodFriend.transform.localScale;
        if (wallWriting.font == null || !wallWriting.font.HasCharacter('赠'))
        {
            writingFont = DialogueView.FindDialogueFont();
            if (writingFont != null)
            {
                wallWriting.font = writingFont;
            }
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

    private void OnDestroy()
    {
        if (writingFont != null)
        {
            Destroy(writingFont);
        }
    }

    private void Start()
    {
        openShaft.SetActive(false);
        loomingShadow.gameObject.SetActive(false);
        redLight.SetActive(inscription.HasCompleted);
        wallWriting.gameObject.SetActive(inscription.HasCompleted);
        if (!decision.HasCompleted)
        {
            return;
        }

        routeChosen = true;
        if (abduction.HasCompleted)
        {
            SealShaft(playSound: false);
            if (inscription.HasCompleted)
            {
                visibility.SetLightingScale(1f);
                if (stairs.HasCompleted)
                {
                    RecordStairArrival();
                }
            }
            else
            {
                GoToLanding();
            }
        }
        else
        {
            WalkIntoPassage();
        }
    }

    private void Update()
    {
        Transform player = party.CurrentPlayer;
        if (player == null || party.IsGameplayLocked || dialogue.IsPlaying)
        {
            return;
        }

        if (!routeChosen && doorsRaised.HasCompleted && !puzzleRoute.HasEnteredRoute)
        {
            // 分流门演出结束后，只累计在安全房内可以自由行动的时间。
            if (waitingArea.Contains(player.position))
            {
                decisionTime += Time.deltaTime;
            }

            if (passageEntry.OverlapPoint(player.position) || decisionTime >= decisionTimeout)
            {
                EnterRoute();
            }
        }
        else if (inscription.HasCompleted && !stairs.HasCompleted && stairArea.OverlapPoint(player.position))
        {
            stairs.Play();
        }
    }

    private void EnterRoute()
    {
        routeChosen = true;
        dialogue.SetStoryValue(RouteKey, RouteValue);
        StageAtDoor();
        decision.Play();
    }

    private void StageAtDoor()
    {
        protagonist.RestorePosition(doorMark.position);
        childhoodFriend.RestorePosition(doorMark.position + new Vector3(.56f, -.55f, 0f));
        brother.RestorePosition(doorMark.position + new Vector3(-.56f, -1.14f, 0f));
        party.SelectPlayer(protagonist);
    }

    // 进门后暂时由演出带队。三人只在这段剧情里按顺序同行。
    public void WalkIntoPassage()
    {
        StartCoroutine(MarchToSlab());
    }

    private IEnumerator MarchToSlab()
    {
        party.SetGameplayLocked(true);
        StageAtDoor();
        PlayFoley(scrape, .31f);
        while (protagonist.SavePosition != leadMark.position ||
               childhoodFriend.SavePosition != friendMark.position ||
               brother.SavePosition != brotherMark.position)
        {
            MoveTo(protagonist, leadMark.position);
            MoveThroughDoor(childhoodFriend, friendMark.position);
            MoveThroughDoor(brother, brotherMark.position);
            yield return null;
        }

        PlayDialogue(abduction);
    }

    private void MoveThroughDoor(PlayerMove actor, Vector3 destination)
    {
        Vector3 position = actor.SavePosition;
        if (position.y < doorMark.position.y && position.x != doorMark.position.x)
        {
            MoveTo(actor, new Vector3(doorMark.position.x, position.y, position.z));
            return;
        }

        if (position.y < 4.15f)
        {
            MoveTo(actor, new Vector3(doorMark.position.x, 4.15f, position.z));
        }
        else
        {
            MoveTo(actor, destination);
        }
    }

    private void OnLineStarted(string id)
    {
        switch (id)
        {
            case "sacrifice_ceiling_click":
                visibility.SetLightingScale(.86f);
                PlayFoley(scrape, .48f, -.19f);
                break;
            case "sacrifice_friend_taken":
                pullingFriend = StartCoroutine(OpenShaftAndPull());
                break;
            case "sacrifice_call_out":
                StartCoroutine(ReachForFriend(protagonist, new Vector3(-.19f, .27f, 0f)));
                break;
            case "sacrifice_grab_my_hand":
                StartCoroutine(ReachForFriend(brother, new Vector3(-.09f, .42f, 0f)));
                break;
            case "sacrifice_shaft_shuts":
                SealShaft(playSound: true);
                break;
            case "sacrifice_silence":
                PlayFoley(breath, .24f);
                break;
            case "sacrifice_wall_message":
                wallWriting.gameObject.SetActive(true);
                wallWriting.canvasRenderer.SetAlpha(0f);
                wallWriting.CrossFadeAlpha(1f, .65f, false);
                break;
        }
    }

    private IEnumerator OpenShaftAndPull()
    {
        PlayFoley(mechanism, .72f);
        openShaft.SetActive(true);
        loomingShadow.gameObject.SetActive(true);
        Vector3 shadowScale = loomingShadow.localScale;
        Vector3 from = childhoodFriend.SavePosition;
        const float duration = .64f;
        for (float current = 0f; current < duration; current += Time.deltaTime)
        {
            float t = Mathf.Clamp01(current / duration);
            float opening = Mathf.SmoothStep(0f, 1f, t * 2.65f);
            float pull = Mathf.SmoothStep(0f, 1f, (t - .12f) / .88f);
            closedSlab.localPosition = slabClosedPosition + new Vector3(.95f, .15f, 0f) * opening;
            loomingShadow.localScale = shadowScale * Mathf.Lerp(.24f, 1f, opening);
            visibility.SetLightingScale(Mathf.Lerp(.86f, .43f, opening));
            childhoodFriend.RestorePosition(Vector3.Lerp(from, grabMark.position, pull));
            childhoodFriend.transform.localScale = friendRestScale * Mathf.Lerp(1f, .35f, pull);
            yield return null;
        }

        childhoodFriend.RestorePosition(grabMark.position);
        childhoodFriend.gameObject.SetActive(false);
        childhoodFriend.transform.localScale = friendRestScale;
        pullingFriend = null;
    }

    private IEnumerator ReachForFriend(PlayerMove actor, Vector3 offset)
    {
        Vector3 reaching = actor.SavePosition + offset;
        while (!shaftSealed && !MoveTo(actor, reaching))
        {
            yield return null;
        }
    }

    private void SealShaft(bool playSound)
    {
        if (shaftSealed)
        {
            return;
        }

        shaftSealed = true;
        if (pullingFriend != null)
        {
            StopCoroutine(pullingFriend);
            pullingFriend = null;
        }

        childhoodFriend.RestorePosition(grabMark.position);
        childhoodFriend.gameObject.SetActive(false);
        childhoodFriend.transform.localScale = friendRestScale;
        openShaft.SetActive(false);
        loomingShadow.gameObject.SetActive(false);
        closedSlab.localPosition = slabClosedPosition;
        visibility.SetLightingScale(.78f);
        dialogue.SetStoryValue("firstFloorCompanion", "taken");
        if (playSound)
        {
            foley.Stop();
            PlayFoley(impact, .84f);
        }
    }

    // 回档到掳走事件之后，仍从同一处拐角走向红光。
    public void GoToLanding()
    {
        StartCoroutine(WalkToLanding());
    }

    private IEnumerator WalkToLanding()
    {
        party.SetGameplayLocked(true);
        SealShaft(playSound: false);
        protagonist.RestorePosition(leadMark.position);
        brother.RestorePosition(brotherMark.position);
        yield return new WaitForSeconds(.35f);

        Vector3 bend = bendMark.position;
        yield return WalkPair(bend, new Vector3(leadMark.position.x, bend.y, bend.z));
        yield return WalkPair(redTurnMark.position, bend);

        redLight.SetActive(true);
        visibility.SetLightingScale(1f);
        yield return WalkPair(landingMark.position, brotherLandingMark.position);
        PlayDialogue(inscription);
    }

    private IEnumerator WalkPair(Vector3 lead, Vector3 follow)
    {
        while (true)
        {
            bool leadThere = MoveTo(protagonist, lead);
            bool brotherThere = MoveTo(brother, follow);
            if (leadThere && brotherThere)
            {
                yield break;
            }
            yield return null;
        }
    }

    public void RecordStairArrival()
    {
        dialogue.SetStoryValue("firstFloorSacrificeExit", "ready");
        sceneSave.LoadNextPartyScene(SceneSave.SecondFloorPath);
    }

    private bool MoveTo(PlayerMove actor, Vector3 target)
    {
        Vector3 next = Vector3.MoveTowards(actor.SavePosition, target, walkSpeed * Time.deltaTime);
        actor.RestorePosition(next);
        return next == target;
    }

    private void PlayDialogue(DialogueTrigger trigger)
    {
        party.SetGameplayLocked(false);
        trigger.Play();
    }

    private void PlayFoley(AudioClip clip, float volume, float pan = 0f)
    {
        foley.panStereo = pan;
        foley.PlayOneShot(clip, volume);
    }
}
