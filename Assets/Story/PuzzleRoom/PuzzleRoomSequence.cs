using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// 一层解谜路线的进场、盲解、手电揭示、汇合和密码门。对话触发器负责记录检查点。
[DisallowMultipleComponent]
public sealed class PuzzleRoomSequence : MonoBehaviour
{
    private enum Phase { Approach, Staging, Trace, Gear, Collect, FindParty, Reunited, Password }
    private enum Stroke { Up, Down, Left, Right }

    [SerializeField] private SwitchPlayer party;
    [SerializeField] private PlayerMove protagonist;
    [SerializeField] private PlayerMove childhoodFriend;
    [SerializeField] private PlayerMove brother;
    [SerializeField] private DialogueController dialogue;
    [SerializeField] private PuzzleRoomVisibility visibility;
    [SerializeField] private PuzzleRoomOverlay overlay;

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

    [SerializeField] private AudioSource foley;
    [SerializeField] private AudioClip scrape;
    [SerializeField] private AudioClip dripping;
    [SerializeField] private AudioClip breath;
    [SerializeField] private AudioClip mechanism;
    [SerializeField] private AudioClip clickAndDrop;
    [SerializeField] private Transform distantSound;
    [SerializeField] private Transform closeSound;

    private static readonly Stroke[] OldStrokes = { Stroke.Up, Stroke.Down, Stroke.Up };
    private static readonly Stroke[] GearStrokes = { Stroke.Up, Stroke.Down, Stroke.Up, Stroke.Left, Stroke.Right, Stroke.Right };
    private static readonly string[] StrokeNames = { "上", "下", "左", "右" };
    private static readonly int GateOpenParameter = Animator.StringToHash("open");
    private static readonly int GateOpenState = Animator.StringToHash("DoorNSOpen");
    private const string PasswordCode = "472";

    private readonly char[] passwordDigits = new char[PasswordCode.Length];
    private Phase phase;
    private int strokeIndex;
    private int selectedDial;
    private bool tracingPointer;
    private Vector2 pointerStart;
    private Quaternion spriteRestRotation;
    private Coroutine recoil;
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
    }

    private void Start()
    {
        UpdateInteractions();
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
    }

    private void Update()
    {
        Transform player = party.CurrentPlayer;
        if (phase == Phase.Approach && player != null && !party.IsGameplayLocked)
        {
            if (routeEntryArea.OverlapPoint(player.position))
            {
                EnterRoute();
            }
        }
        else if (phase == Phase.FindParty && player != null && !party.IsGameplayLocked &&
                 player.position.x >= playerReunion.position.x - 1f && player.position.y >= playerReunion.position.y - .38f)
        {
            StartReunion();
        }
        else if ((phase == Phase.Trace || phase == Phase.Gear) && Time.frameCount != inputOpenedFrame)
        {
            ReadStrokeInput();
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
            landing.Play();
        }
    }

    // 如果玩家用其他角色走进左门，由主角接手，另一位角色回到主角原来的位置。
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
        else if (scare.HasCompleted)
        {
            BeginGesture(Phase.Gear);
        }
        else if (call.HasCompleted)
        {
            BeginGesture(Phase.Trace);
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
                protagonist.RestorePosition(Vector3.MoveTowards(protagonist.SavePosition, waypoint.position, 5f * Time.deltaTime));
                yield return null;
            }

            PlayFoley(scrape, .25f, -.25f);
        }

        party.SetGameplayLocked(false);
        call.Play();
    }

    private void BeginGesture(Phase next)
    {
        phase = next;
        protagonist.RestorePosition(blindMark.position);
        party.SetSwitchingLocked(true);
        party.SetGameplayLocked(true);
        visibility.SetBlackout(true);
        strokeIndex = 0;
        tracingPointer = false;
        inputOpenedFrame = Time.frameCount;
        ShowGesture();
    }

    private void ReadStrokeInput()
    {
        var keys = Keyboard.current;
        if (keys != null)
        {
            if (keys.upArrowKey.wasPressedThisFrame || keys.wKey.wasPressedThisFrame) { ApplyStroke(Stroke.Up); return; }
            if (keys.downArrowKey.wasPressedThisFrame || keys.sKey.wasPressedThisFrame) { ApplyStroke(Stroke.Down); return; }
            if (keys.leftArrowKey.wasPressedThisFrame || keys.aKey.wasPressedThisFrame) { ApplyStroke(Stroke.Left); return; }
            if (keys.rightArrowKey.wasPressedThisFrame || keys.dKey.wasPressedThisFrame) { ApplyStroke(Stroke.Right); return; }
        }

        var pointer = Pointer.current;
        if (pointer == null)
        {
            return;
        }

        Vector2 position = pointer.position.ReadValue();
        if (pointer.press.wasPressedThisFrame)
        {
            pointerStart = position;
            tracingPointer = true;
        }

        Vector2 travel = position - pointerStart;
        if (tracingPointer && travel.sqrMagnitude >= 38f * 38f)
        {
            tracingPointer = false;
            ApplyStroke(Mathf.Abs(travel.y) >= Mathf.Abs(travel.x)
                ? (travel.y > 0f ? Stroke.Up : Stroke.Down)
                : (travel.x > 0f ? Stroke.Right : Stroke.Left));
        }
        else if (!pointer.press.isPressed)
        {
            tracingPointer = false;
        }
    }

    private void ApplyStroke(Stroke stroke)
    {
        bool gear = phase == Phase.Gear;
        Stroke[] sequence = gear ? GearStrokes : OldStrokes;
        PlayFoley(scrape, .58f, -.16f);
        if (sequence[strokeIndex] != stroke)
        {
            if (gear)
            {
                strokeIndex = 0;
            }
            ShowGesture(gear ? "齿轮弹回了。按旧刻痕、新刻痕的顺序再试。" : "指尖离开了凹槽。");
            return;
        }

        strokeIndex++;
        if (strokeIndex < sequence.Length)
        {
            ShowGesture();
            return;
        }

        overlay.Hide();
        phase = Phase.Staging;
        if (gear)
        {
            StartCoroutine(RevealChamber());
        }
        else
        {
            party.SetGameplayLocked(false);
            scare.Play();
        }
    }

    private void ShowGesture(string message = null)
    {
        bool gear = phase == Phase.Gear;
        string status = gear ? $"已拨动 {strokeIndex} / {GearStrokes.Length}"
            : $"指尖摸到一道朝「{StrokeNames[(int)OldStrokes[strokeIndex]]}」的凹槽";
        overlay.Show(gear ? "拨动墙上的齿轮" : "沿着刻痕摸索",
            gear ? "按先摸到的刻痕、再接上新刻痕 · 拖动或按方向键"
                : "按住拖动，或使用方向键",
            message == null ? status : $"{message}\n{status}");
    }

    private IEnumerator RevealChamber()
    {
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
                gateAnimator.SetBool(GateOpenParameter, true);
                PlayFoley(mechanism, .78f, 0f);
                break;
        }
    }

    private IEnumerator RecoilAndDrop()
    {
        Vector3 end = Vector3.Lerp(firstBeam.position, flashlightDrop.position, .80f);
        protagonistSprite.localRotation = Quaternion.Euler(0f, 0f, -63f) * spriteRestRotation;
        PlayFoley(clickAndDrop, .88f, .15f);
        visibility.SetFlashlight(true, flashlightDrop.position, flashlightDirection);
        while (protagonist.SavePosition != end)
        {
            protagonist.RestorePosition(Vector3.MoveTowards(protagonist.SavePosition, end, 6.7f * Time.deltaTime));
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

    public void InspectThirdClue()
    {
        if (phase == Phase.Reunited)
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

        if (party.WasInteractPressed())
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
        string[] dials = new string[passwordDigits.Length];
        for (int i = 0; i < passwordDigits.Length; i++)
        {
            dials[i] = i == selectedDial ? $"【{passwordDigits[i]}】" : $"　{passwordDigits[i]}　";
        }
        overlay.Show("三段密码门", "方向键选转盘、改数字 · F 确认 · Esc 返回",
            $"{string.Join("  ", dials)}\n{feedback ?? "纸条上的数字：" + string.Join("、", PasswordCode.ToCharArray())}");
    }

    private void ClosePassword()
    {
        overlay.Hide();
        phase = Phase.Reunited;
        party.SetGameplayLocked(false);
    }

    private void UpdateInteractions()
    {
        passageSeal.enabled = !clues.HasCompleted;
        cluesInteraction.enabled = !clues.HasCompleted && reveal.HasCompleted;
        thirdClueInteraction.enabled = reunion.HasCompleted && !thirdClue.HasCompleted;
        thirdClueSlip.SetActive(!thirdClue.HasCompleted);
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
