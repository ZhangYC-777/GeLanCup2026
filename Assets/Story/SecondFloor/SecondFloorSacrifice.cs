using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 二层右门后的黑影伏击、兄弟断后与主角逃往第三层。
[DefaultExecutionOrder(210)]
[DisallowMultipleComponent]
public sealed class SecondFloorSacrifice : MonoBehaviour
{
    [Header("队伍与对话")]
    [SerializeField] private SwitchPlayer party;
    [SerializeField] private PlayerMove protagonist;
    [SerializeField] private PlayerMove brother;
    [SerializeField] private DialogueController dialogue;
    [SerializeField] private SceneSave sceneSave;
    [SerializeField] private DialogueTrigger sightingBeat;
    [SerializeField] private DialogueTrigger encounterBeat;
    [SerializeField] private DialogueTrigger retreatBeat;

    [Header("走位")]
    [SerializeField] private Transform[] brotherApproachPath;
    [SerializeField] private Transform protagonistAtDoor;
    [SerializeField] private Collider2D doorBlocker;
    [SerializeField] private Transform[] passagePath;
    [SerializeField] private Transform encounterMark;
    [SerializeField] private Transform pushedBackMark;
    [SerializeField] private Transform shadowDistantMark;
    [SerializeField] private Transform interceptMark;
    [SerializeField] private Transform[] retreatPath;

    [Header("镜头、灯光与声音")]
    [SerializeField] private Transform mainCamera;
    [SerializeField] private PuzzleRoomVisibility visibility;
    [SerializeField] private SpriteRenderer shadow;
    [SerializeField] private Image fade;
    [SerializeField] private AudioSource foley;
    [SerializeField] private AudioClip scrape;
    [SerializeField] private AudioClip breath;
    [SerializeField] private AudioClip impact;

    private const float WalkSpeed = 4.8f;
    private const float RunSpeed = 7.2f;
    private const float FollowingGap = .85f;
    private bool intercepting;
    private float shakeRemaining;
    private Color shadowColor;

    private void Awake()
    {
        shadowColor = shadow.color;
        shadow.gameObject.SetActive(false);
        // 献祭路线里兄弟同样只由演出脚本带走，关掉碰撞免得卡在柱子上。
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
        if (intercepting)
        {
            dialogue.CanAdvanceLine = null;
            intercepting = false;
        }
    }

    private IEnumerator Start()
    {
        while (!dialogue.HasStoryValue(SecondFloorArrival.RouteState, SecondFloorArrival.SacrificeRoute))
        {
            if (dialogue.HasStoryValue(SecondFloorArrival.RouteState, SecondFloorArrival.ExplorationRoute))
            {
                yield break;
            }

            yield return null;
        }

        party.SetSwitchingLocked(true);
        party.SetGameplayLocked(true);
        party.SelectPlayer(protagonist);
        if (!sightingBeat.HasCompleted)
        {
            yield return EnterPassage();
            yield return GlimpseShadow();
            yield return PlayBeat(sightingBeat);
        }

        // 读档时从已完成的段落恢复站位，再继续下一段动作。
        if (sightingBeat.HasCompleted && !encounterBeat.HasCompleted)
        {
            PlaceAtEncounter();
            yield return Lunge();
            yield return PlayBeat(encounterBeat);
            while (intercepting)
            {
                yield return null;
            }
        }

        if (encounterBeat.HasCompleted)
        {
            RestoreAfterInterception();
            yield return PlayBeat(retreatBeat);
        }

        if (retreatBeat.HasCompleted)
        {
            yield return Flee();
        }
        else
        {
            party.SetGameplayLocked(false);
        }
    }

    // 兄弟先到门前，等石门完全打开后带主角走进通道。
    private IEnumerator EnterPassage()
    {
        party.SetCameraFocus(brother.transform);
        foreach (Transform mark in brotherApproachPath)
        {
            yield return Walk(brother, mark.position, RunSpeed);
        }

        yield return Walk(protagonist, protagonistAtDoor.position, WalkSpeed);
        party.SetCameraFocus(protagonist.transform);
        yield return new WaitUntil(() => !doorBlocker.enabled);

        foreach (Transform mark in passagePath)
        {
            Vector3 direction = (mark.position - brother.SavePosition).normalized;
            Vector3 protagonistTarget = mark.position - direction * FollowingGap;
            yield return WalkPair(mark.position, protagonistTarget, direction);
        }
    }

    private IEnumerator WalkPair(Vector3 brotherTarget, Vector3 protagonistTarget, Vector2 beamDirection)
    {
        Coroutine lead = StartCoroutine(Walk(brother, brotherTarget, WalkSpeed));
        yield return Walk(protagonist, protagonistTarget, WalkSpeed, beamDirection);
        yield return lead;
    }

    private void PlaceAtEncounter()
    {
        protagonist.RestorePosition(encounterMark.position);
        brother.RestorePosition(encounterMark.position + Vector3.up * FollowingGap);
        Vector2 towardShadow = (shadowDistantMark.position - protagonist.SavePosition).normalized;
        visibility.SetFlashlight(true, protagonist.SavePosition, towardShadow);
    }

    // 手电只在远处照到一瞬，等主角喝问后黑影才冲到两人跟前。
    private IEnumerator GlimpseShadow()
    {
        PlaceAtEncounter();
        shadow.transform.position = shadowDistantMark.position + Vector3.right * .35f;
        shadow.gameObject.SetActive(true);
        PlaySound(scrape, .35f, .77f);
        yield return MoveShadow(shadowDistantMark.position + Vector3.left * .42f, .36f, fadeOut: true);
        shadow.gameObject.SetActive(false);
    }

    private IEnumerator Lunge()
    {
        shadow.transform.position = shadowDistantMark.position;
        shadow.color = shadowColor;
        shadow.gameObject.SetActive(true);
        PlaySound(breath, .85f, .58f);
        yield return MoveShadow(interceptMark.position, .22f);
        Shake(.22f);
    }

    private void OnLineStarted(string node)
    {
        if (node == "sacrifice_2_brother_run")
        {
            StartCoroutine(InterceptShadow());
        }
    }

    private IEnumerator InterceptShadow()
    {
        intercepting = true;
        dialogue.CanAdvanceLine = () => !intercepting;
        PlaySound(impact, .75f, .65f);
        Shake(.25f);
        Coroutine pushed = StartCoroutine(Walk(protagonist, pushedBackMark.position, 10f));
        yield return Walk(brother, interceptMark.position, RunSpeed);
        yield return pushed;
        visibility.SetFlashlight(true, protagonist.SavePosition, Vector2.up);
        brother.gameObject.SetActive(false);
        yield return MoveShadow(interceptMark.position + Vector3.down * .15f, .28f, fadeOut: true);
        shadow.gameObject.SetActive(false);
        intercepting = false;
        dialogue.CanAdvanceLine = null;
    }

    private void RestoreAfterInterception()
    {
        protagonist.RestorePosition(pushedBackMark.position);
        brother.gameObject.SetActive(false);
        shadow.gameObject.SetActive(false);
        visibility.SetFlashlight(true, protagonist.SavePosition, Vector2.up);
    }

    // 主角沿来路逃回门口，在坠落感和黑场后交给第三层现有出生点。
    private IEnumerator Flee()
    {
        party.SetCameraFocus(protagonist.transform);
        PlaySound(breath, .48f, .85f);
        // 第一个返程点在门内的感应区，等门扇打开再穿过门洞。
        yield return Walk(protagonist, retreatPath[0].position, RunSpeed, Vector2.down);
        yield return new WaitUntil(() => !doorBlocker.enabled);
        for (int i = 1; i < retreatPath.Length; i++)
        {
            Vector3 target = retreatPath[i].position;
            Vector2 direction = (target - protagonist.SavePosition).normalized;
            yield return Walk(protagonist, target, RunSpeed, direction);
        }

        PlaySound(impact, .85f, .5f);
        Shake(.35f);
        Color color = fade.color;
        const float duration = .42f;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            color.a = Mathf.Clamp01(elapsed / duration);
            fade.color = color;
            yield return null;
        }

        color.a = 1f;
        fade.color = color;
        sceneSave.LoadNextPartyScene(SceneSave.ThirdFloorPath);
    }

    private IEnumerator PlayBeat(DialogueTrigger beat)
    {
        if (beat.HasCompleted)
        {
            yield break;
        }

        party.SetGameplayLocked(false);
        beat.Play();
        while (dialogue.IsPlaying && !beat.HasCompleted)
        {
            yield return null;
        }

        party.SetGameplayLocked(true);
        if (!beat.HasCompleted)
        {
            Debug.LogError("献祭路线未能播放剧情段落：" + beat.name, beat);
        }
    }

    private IEnumerator Walk(PlayerMove actor, Vector3 target, float speed, Vector2? beamDirection = null)
    {
        Vector3 start = actor.SavePosition;
        float duration = Vector3.Distance(start, target) / speed;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            actor.MoveForStory(Vector3.Lerp(start, target, elapsed / duration));
            if (beamDirection.HasValue)
            {
                visibility.SetFlashlight(true, actor.SavePosition, beamDirection.Value);
            }
            yield return null;
        }

        actor.MoveForStory(target);
        if (beamDirection.HasValue)
        {
            visibility.SetFlashlight(true, target, beamDirection.Value);
        }
    }

    private IEnumerator MoveShadow(Vector3 target, float duration, bool fadeOut = false)
    {
        Vector3 start = shadow.transform.position;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            shadow.transform.position = Vector3.Lerp(start, target, t);
            Color color = shadowColor;
            if (fadeOut)
            {
                color.a *= 1f - t;
            }
            shadow.color = color;
            yield return null;
        }

        shadow.transform.position = target;
    }

    private void Shake(float duration)
    {
        shakeRemaining = duration;
    }

    private void LateUpdate()
    {
        if (shakeRemaining > 0f)
        {
            shakeRemaining = Mathf.Max(0f, shakeRemaining - Time.deltaTime);
            mainCamera.position += (Vector3)(Random.insideUnitCircle * shakeRemaining * .54f);
        }
    }

    private void PlaySound(AudioClip clip, float volume, float pitch)
    {
        foley.pitch = pitch;
        foley.PlayOneShot(clip, volume);
    }
}
