using System.Collections;
using UnityEngine;

// 一层解谜路线的进场、齿轮选择、手电揭示、汇合、面包迷宫、密码门和楼梯演出。
[DisallowMultipleComponent]
public sealed class PuzzleRoomSequence : MonoBehaviour
{
    private enum Phase { Approach, Staging, Failed, Collect, FindParty, Reunited, Password, BreadTrial }

    [SerializeField] private SwitchPlayer party;
    [SerializeField] private PlayerMove protagonist;
    [SerializeField] private PlayerMove childhoodFriend;
    [SerializeField] private PlayerMove brother;
    [SerializeField] private DialogueController dialogue;
    [SerializeField] private InventoryController inventory;
    [SerializeField] private PuzzleRoomVisibility visibility;
    [SerializeField] private PuzzleRoomOverlay overlay;
    [SerializeField] private SceneSave sceneSave;
    [SerializeField] private BreadTrial breadTrial;
    [SerializeField] private SpriteRenderer[] trialAvatars;

    [SerializeField] private DialogueTrigger entry;
    [SerializeField] private DialogueTrigger call;
    [SerializeField] private DialogueTrigger scare;
    [SerializeField] private DialogueTrigger reveal;
    [SerializeField] private DialogueTrigger clues;
    [SerializeField] private DialogueTrigger reunion;
    [SerializeField] private DialogueTrigger missingPassword;
    [SerializeField] private DialogueTrigger thirdClue;
    [SerializeField] private DialogueTrigger gateOpened;
    [SerializeField] private DialogueTrigger landing;
    [SerializeField] private DialogueTrigger stairAbduction;
    [SerializeField] private PlayerHit cluesInteraction;
    [SerializeField] private PlayerHit thirdClueInteraction;
    [SerializeField] private PlayerHit gateInteraction;

    [SerializeField] private Collider2D routeEntryArea;
    [SerializeField] private Transform corridorEntrance;
    [SerializeField] private Transform[] corridorWaypoints;
    [SerializeField] private Transform blindMark;
    [SerializeField] private Transform firstBeam;
    [SerializeField] private Transform flashlightDrop;
    [SerializeField] private Transform writing;
    [SerializeField] private Transform playerReunion;
    [SerializeField] private Transform friendReunion;
    [SerializeField] private Transform brotherReunion;
    [SerializeField] private Transform protagonistSprite;
    [SerializeField] private Collider2D passageSeal;
    [SerializeField] private GameObject thirdClueSlip;
    [SerializeField] private Animator gateAnimator;
    [SerializeField] private Collider2D gateBlocker;
    [SerializeField] private Collider2D landingArea;
    [SerializeField] private Transform stairHunter;

    [SerializeField] private AudioSource foley;
    [SerializeField] private AudioSource gateAudio;
    [SerializeField] private AudioClip scrape;
    [SerializeField] private AudioClip dripping;
    [SerializeField] private AudioClip breath;
    [SerializeField] private AudioClip mechanism;
    [SerializeField] private AudioClip clickAndDrop;
    [SerializeField] private Transform distantSound;
    [SerializeField] private Transform closeSound;

    private static readonly int GateOpenParameter = Animator.StringToHash("open");
    private static readonly int GateOpenState = Animator.StringToHash("DoorNSOpen");
    private const string PasswordCode = "472";
    private const string FlashlightItemId = "flashlight";
    private const string BreadTrialStoryKey = "puzzleBreadTrial";
    private const string CompletedTrial = "cleared";

    private bool BreadTrialCleared => dialogue.HasStoryValue(BreadTrialStoryKey, CompletedTrial);

    private readonly char[] passwordDigits = new char[PasswordCode.Length];
    private Phase phase;
    internal bool HasEnteredRoute => phase != Phase.Approach || entry.HasCompleted;
    private int selectedDial;
    private Quaternion spriteRestRotation;
    private Coroutine recoil;
    private bool playingStairSnatch;
    private Vector2 flashlightDirection = Vector2.up;
    private Vector2 lastPosition;
    private bool flashlightOn;
    private int inputOpenedFrame;

    private void Awake()
    {
        spriteRestRotation = protagonistSprite.localRotation;
    }

    private void OnEnable()
    {
        dialogue.LineStarted += OnLineStarted;
    }

    private void OnDisable()
    {
        dialogue.LineStarted -= OnLineStarted;
        if (playingStairSnatch)
        {
            dialogue.CanAdvanceLine = null;
        }
    }

    private void Start()
    {
        UpdateInteractions();
        // 较早的存档可能已经播完捡起手电筒的演出，但尚未记录该道具。
        if (reveal.HasCompleted)
        {
            inventory.TryGrant(FlashlightItemId);
        }

        if (entry.HasCompleted)
        {
            ResumeAfterBeat();
        }

        if (gateOpened.HasCompleted)
        {
            gateAnimator.SetBool(GateOpenParameter, true);
            gateAnimator.Play(GateOpenState, 0, 0f);
            gateAnimator.Update(0f);
            gateBlocker.enabled = false;
        }

        if (stairAbduction.HasCompleted)
        {
            FinishStairAbduction();
        }
        else if (landing.HasCompleted)
        {
            // 楼梯口的旧存档及中途退出的存档会从新演出开始。
            StageStairParty();
            stairAbduction.Play();
        }
    }

    private void Update()
    {
        Transform player = party.CurrentPlayer;
        if (phase == Phase.Approach && player != null && !party.IsGameplayLocked)
        {
            if (!dialogue.HasStoryValue("firstFloorRoute", "sacrifice") && routeEntryArea.OverlapPoint(player.position))
            {
                EnterRoute();
            }
        }
        else if (phase == Phase.FindParty && player != null && !party.IsGameplayLocked &&
                 player.position.x >= playerReunion.position.x - 1f && player.position.y >= playerReunion.position.y - .38f)
        {
            StartReunion();
        }
        else if (phase == Phase.Failed && Time.frameCount != inputOpenedFrame && MenuInput.WasConfirmPressed())
        {
            RetryGear();
        }
        else if (phase == Phase.Password && Time.frameCount != inputOpenedFrame)
        {
            ReadPasswordInput();
        }

        if (flashlightOn)
        {
            Vector2 position = protagonist.transform.position;
            Vector2 movement = position - lastPosition;
            if (movement.sqrMagnitude > .0004f)
            {
                flashlightDirection = movement.normalized;
                lastPosition = position;
            }

            visibility.SetFlashlight(true, position, flashlightDirection);
        }

        if (gateOpened.HasCompleted && gateBlocker.enabled &&
            gateAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash == GateOpenState)
        {
            gateBlocker.enabled = false;
        }

        if (gateOpened.HasCompleted && !gateBlocker.enabled && !landing.HasCompleted &&
            player != null && !party.IsGameplayLocked && landingArea.OverlapPoint(player.position))
        {
            StageStairParty();
            landing.Play();
        }
    }

    // 如果玩家用其他角色走进解谜门，由主角接手，另一位角色回到主角原来的位置。
    private void EnterRoute()
    {
        int index = party.CurrentPlayerIndex;
        if (index != 0)
        {
            Vector3[] positions = party.CapturePositions();
            (positions[0], positions[index]) = (positions[index], positions[0]);
            party.RestoreParty(positions, 0);
        }

        protagonist.RestorePosition(corridorEntrance.position);
        party.SetSwitchingLocked(true);
        party.SetCameraFocus(protagonist.transform, snap: true);
        phase = Phase.Staging;
        visibility.SetBlackout(true);
        entry.Play();
    }

    // 在 DialogueTrigger 收尾后运行。这里更新的状态会一并进入现有存档检查点。
    public void ResumeAfterBeat()
    {
        if (reunion.HasCompleted)
        {
            phase = Phase.Reunited;
        }
        else if (clues.HasCompleted)
        {
            phase = Phase.FindParty;
        }
        else if (reveal.HasCompleted)
        {
            phase = Phase.Collect;
            RecoverFromRecoil();
        }
        else if (call.HasCompleted)
        {
            phase = Phase.Staging;
            protagonist.RestorePosition(blindMark.position);
            party.SetSwitchingLocked(true);
            party.SetCameraFocus(protagonist.transform, snap: true);
            visibility.SetBlackout(true);
            if (dialogue.HasStoryValue("puzzleGear", "solved"))
            {
                StartCoroutine(RevealChamber());
            }
            else
            {
                scare.Play();
            }
        }
        else
        {
            StartCoroutine(WalkIntoChamber());
        }

        if (reveal.HasCompleted)
        {
            flashlightOn = true;
            lastPosition = protagonist.SavePosition;
            visibility.SetBlackout(false);
            visibility.SetFlashlight(true, lastPosition, flashlightDirection);
            party.SetSwitchingLocked(!reunion.HasCompleted);
        }

        UpdateInteractions();
    }

    private IEnumerator WalkIntoChamber()
    {
        phase = Phase.Staging;
        visibility.SetBlackout(true);
        party.SetSwitchingLocked(true);
        party.SetGameplayLocked(true);
        party.SetCameraFocus(protagonist.transform, snap: true);
        protagonist.RestorePosition(corridorEntrance.position);
        foreach (Transform waypoint in corridorWaypoints)
        {
            while (protagonist.SavePosition != waypoint.position)
            {
                protagonist.MoveForStory(Vector3.MoveTowards(protagonist.SavePosition, waypoint.position, 5f * Time.deltaTime));
                yield return null;
            }

            PlayFoley(scrape, .25f, -.25f);
        }

        party.SetGameplayLocked(false);
        call.Play();
    }

    // 选错时中止播放，检查点仍停在主角念出第一组刻痕之前。
    private void ShowGearFailure()
    {
        dialogue.ClearPlayback();
        party.SetGameplayLocked(true);
        phase = Phase.Failed;
        inputOpenedFrame = Time.frameCount;
        foley.Stop();
        PlayFoley(mechanism, .64f, -.12f);
        overlay.ShowFailure();
    }

    public void RetryGear()
    {
        if (phase == Phase.Failed)
        {
            sceneSave.RetryCheckpoint();
        }
    }

    private IEnumerator RevealChamber()
    {
        party.SetGameplayLocked(true);
        PlayFoley(mechanism, .78f, -.12f);
        yield return new WaitForSeconds(.72f);
        protagonist.RestorePosition(firstBeam.position);
        flashlightDirection = ((Vector2)(writing.position - firstBeam.position)).normalized;
        visibility.SetFlashlight(true, firstBeam.position, flashlightDirection);
        for (int flicker = 0; flicker < 2; flicker++)
        {
            PlayFoley(clickAndDrop, .34f, .1f);
            visibility.SetBlackout(false);
            yield return new WaitForSeconds(.11f);
            visibility.SetBlackout(true);
            yield return new WaitForSeconds(.14f);
        }

        visibility.SetBlackout(false);
        party.SetGameplayLocked(false);
        reveal.Play();
    }

    private void OnLineStarted(string node)
    {
        switch (node)
        {
            case "puzzle_no_answer":
                PlayFoley(breath, .25f, 0f);
                break;
            case "puzzle_read_old":
            case "puzzle_read_new":
                PlayFoley(scrape, .48f, -.16f);
                break;
            case "puzzle_gear_failed":
                ShowGearFailure();
                break;
            case "puzzle_distant_drips":
                PlayAt(dripping, .46f, distantSound);
                break;
            case "puzzle_behind":
                foley.Stop();
                PlayAt(breath, .88f, closeSound);
                break;
            case "puzzle_recoil":
                recoil = StartCoroutine(RecoilAndDrop());
                break;
            case "puzzle_gate_open":
                gateAnimator.speed = .25f;
                gateAnimator.SetBool(GateOpenParameter, true);
                gateAudio.Play();
                break;
            case "puzzle_stair_darkness":
                flashlightOn = false;
                visibility.SetFlashlight(false, Vector2.zero, Vector2.up);
                visibility.SetBlackout(true);
                break;
            case "puzzle_stair_ceiling_click":
                PlayFoley(scrape, .24f, .12f);
                break;
            case "puzzle_stair_slab_opens":
                PlayFoley(mechanism, .7f, .08f);
                break;
            case "puzzle_stair_lifted":
                PlayFoley(breath, .76f, -.2f);
                StartCoroutine(PlayStairSnatch());
                break;
            case "puzzle_stair_slab_shuts":
                foley.Stop();
                PlayFoley(clickAndDrop, .86f, 0f);
                break;
        }
    }

    // 任何一位角色走过密码门都能触发。三人在楼梯口会合，由主角接手。
    private void StageStairParty()
    {
        Vector3 landingPosition = landingArea.transform.position;
        protagonist.RestorePosition(landingPosition + Vector3.up * .52f);
        childhoodFriend.RestorePosition(landingPosition + Vector3.down * .18f);
        brother.RestorePosition(landingPosition + Vector3.down * .88f);
        party.SelectPlayer(protagonist);
        flashlightDirection = Vector2.up;
        lastPosition = protagonist.SavePosition;
    }

    // 石板后的黑影只在掳人的瞬间露出来，随青梅一起退回黑暗。
    private IEnumerator PlayStairSnatch()
    {
        if (stairHunter == null)
        {
            HideChildhoodFriend();
            yield break;
        }

        playingStairSnatch = true;
        dialogue.CanAdvanceLine = () => !playingStairSnatch;
        Vector3 friendStart = childhoodFriend.SavePosition;
        Vector3 friendScale = childhoodFriend.transform.localScale;
        Vector3 shaft = stairHunter.position;
        Vector3 grip = friendStart + new Vector3(-.46f, .15f, 0f);
        visibility.SetLightingScale(0f);
        visibility.SetBlackout(false);
        stairHunter.gameObject.SetActive(true);

        const float duration = .72f;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float t = elapsed / duration;
            float lunge = Mathf.SmoothStep(0f, 1f, t / .32f);
            float pull = Mathf.SmoothStep(0f, 1f, (t - .32f) / .68f);
            stairHunter.position = Vector3.Lerp(Vector3.Lerp(shaft, grip, lunge), shaft, pull);
            childhoodFriend.RestorePosition(Vector3.Lerp(friendStart, shaft, pull));
            childhoodFriend.transform.localScale = friendScale * Mathf.Lerp(1f, .36f, pull);
            visibility.SetLightingScale(.68f * Mathf.Sin(t * Mathf.PI));
            yield return null;
        }

        visibility.SetBlackout(true);
        visibility.SetLightingScale(1f);
        stairHunter.gameObject.SetActive(false);
        stairHunter.position = shaft;
        HideChildhoodFriend();
        childhoodFriend.transform.localScale = friendScale;
        playingStairSnatch = false;
        dialogue.CanAdvanceLine = null;
    }

    public void FinishStairAbduction()
    {
        HideChildhoodFriend();
        dialogue.SetStoryValue("firstFloorCompanion", "taken");
        dialogue.SetStoryValue("firstFloorPuzzleExit", "ready");
        visibility.SetBlackout(false);
        sceneSave.LoadNextPartyScene(SceneSave.SecondFloorPath);
    }

    private void HideChildhoodFriend()
    {
        if (party.CurrentPlayer == childhoodFriend.transform)
        {
            party.SelectPlayer(protagonist);
        }
        childhoodFriend.gameObject.SetActive(false);
    }

    private IEnumerator RecoilAndDrop()
    {
        Vector3 end = Vector3.Lerp(firstBeam.position, flashlightDrop.position, .80f);
        protagonistSprite.localRotation = Quaternion.Euler(0f, 0f, -63f) * spriteRestRotation;
        PlayFoley(clickAndDrop, .88f, .15f);
        visibility.SetFlashlight(true, flashlightDrop.position, flashlightDirection);
        while (protagonist.SavePosition != end)
        {
            protagonist.MoveForStory(Vector3.MoveTowards(protagonist.SavePosition, end, 6.7f * Time.deltaTime));
            yield return null;
        }
        recoil = null;
    }

    private void RecoverFromRecoil()
    {
        if (recoil != null)
        {
            StopCoroutine(recoil);
            recoil = null;
        }

        protagonistSprite.localRotation = spriteRestRotation;
    }

    public void InspectClues()
    {
        if (phase == Phase.Collect)
        {
            clues.Play();
        }
    }

    private void StartReunion()
    {
        phase = Phase.Staging;
        protagonist.RestorePosition(playerReunion.position);
        childhoodFriend.RestorePosition(friendReunion.position);
        brother.RestorePosition(brotherReunion.position);
        reunion.Play();
    }

    public void InspectPasswordGate()
    {
        if (phase != Phase.Reunited || gateOpened.HasCompleted)
        {
            return;
        }

        if (!thirdClue.HasCompleted)
        {
            missingPassword.Play();
            return;
        }

        phase = Phase.Password;
        party.SetGameplayLocked(true);
        inputOpenedFrame = Time.frameCount;
        selectedDial = 0;
        for (int i = 0; i < passwordDigits.Length; i++)
        {
            passwordDigits[i] = '0';
        }
        ShowPassword();
    }

    public void InspectBreadTrial()
    {
        if (phase != Phase.Reunited || thirdClue.HasCompleted || party.IsGameplayLocked)
        {
            return;
        }

        if (BreadTrialCleared)
        {
            thirdClue.Play();
            return;
        }

        SpriteRenderer avatar = trialAvatars[party.CurrentPlayerIndex];
        if (breadTrial.Begin(avatar, RecordBreadTrialWin, CloseBreadTrial))
        {
            phase = Phase.BreadTrial;
            party.SetGameplayLocked(true);
        }
    }

    private void RecordBreadTrialWin()
    {
        dialogue.SetStoryValue(BreadTrialStoryKey, CompletedTrial);
        sceneSave.CaptureCheckpoint();
    }

    private void CloseBreadTrial(bool won)
    {
        phase = Phase.Reunited;
        inputOpenedFrame = Time.frameCount;
        party.SetGameplayLocked(false);
        UpdateInteractions();
        if (won)
        {
            thirdClue.Play();
        }
    }

    // 输入由密码面板接管。输错可以继续拨动，退出后仍可再次交互。
    private void ReadPasswordInput()
    {
        if (party.WasInventoryCancelPressed())
        {
            ClosePassword();
            return;
        }

        if (MenuInput.WasConfirmPressed())
        {
            if (new string(passwordDigits) == PasswordCode)
            {
                ClosePassword();
                gateOpened.Play();
            }
            else
            {
                PlayFoley(scrape, .4f, 0f);
                ShowPassword("石门没有动。再核对一下三张纸条。");
            }
            return;
        }

        Vector2Int direction = party.ReadInventoryNavigation();
        if (direction == Vector2Int.zero)
        {
            return;
        }

        selectedDial = (selectedDial + direction.x + passwordDigits.Length) % passwordDigits.Length;
        if (direction.y != 0)
        {
            int number = passwordDigits[selectedDial] - '0';
            passwordDigits[selectedDial] = (char)('0' + (number - direction.y + 10) % 10);
            PlayFoley(scrape, .25f, 0f);
        }
        ShowPassword();
    }

    private void ShowPassword(string feedback = null)
    {
        overlay.ShowPassword(passwordDigits, selectedDial,
            feedback ?? "纸条上的数字    " + string.Join(" · ", PasswordCode.ToCharArray()), feedback != null);
    }

    private void ClosePassword()
    {
        overlay.HidePassword();
        phase = Phase.Reunited;
        party.SetGameplayLocked(false);
    }

    private void UpdateInteractions()
    {
        passageSeal.enabled = !clues.HasCompleted;
        cluesInteraction.enabled = !clues.HasCompleted && reveal.HasCompleted;
        thirdClueInteraction.enabled = reunion.HasCompleted && !thirdClue.HasCompleted;
        thirdClueSlip.SetActive(BreadTrialCleared && !thirdClue.HasCompleted);
        gateInteraction.enabled = reunion.HasCompleted && !gateOpened.HasCompleted;
    }

    private void PlayAt(AudioClip clip, float volume, Transform origin)
    {
        float horizontal = origin.position.x - protagonist.transform.position.x;
        PlayFoley(clip, volume, Mathf.Clamp(horizontal * .55f, -.85f, .85f));
    }

    private void PlayFoley(AudioClip clip, float volume, float pan)
    {
        foley.panStereo = pan;
        foley.PlayOneShot(clip, volume);
    }
}
