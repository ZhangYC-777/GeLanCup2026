using System.Collections;
using Cinemachine;
using UnityEngine;
using UnityEngine.UI;

// 第三层终局演出。台词触发画面，重要走位完成后才允许继续。
[DisallowMultipleComponent]
public sealed class FinalRoomSequence : MonoBehaviour
{
    [Header("对话与角色")]
    [SerializeField] private SwitchPlayer party;
    [SerializeField] private DialogueController dialogue;
    [SerializeField] private PlayerMove protagonist;
    [SerializeField] private Transform sacrificeMark;
    [SerializeField] private Transform statueMark;
    [SerializeField] private Transform doorApproach;
    [SerializeField] private Transform doorMark;

    [Header("镜头")]
    [SerializeField] private CinemachineVirtualCamera gameCamera;
    [SerializeField] private Transform cameraPivot;

    [Header("场景物件")]
    [SerializeField] private Transform statue;
    [SerializeField] private GameObject statueRubble;
    [SerializeField] private Collider2D statueFoundation;
    [SerializeField] private GameObject ritualCircle;
    [SerializeField] private Transform floorRift;
    [SerializeField] private Transform escapeDoor;
    [SerializeField] private Collider2D doorBlocker;
    [SerializeField] private GameObject exitLight;
    [SerializeField] private SpriteRenderer flashlight;
    [SerializeField] private SpriteRenderer flashlightGlow;
    [SerializeField] private SpriteRenderer hammer;
    [SerializeField] private SpriteRenderer[] hunters;
    [SerializeField] private PlayerWalkSprites hunterWalkSprites;
    [SerializeField] private SpriteRenderer[] hunterSmoke;

    [Header("界面")]
    [SerializeField] private CanvasGroup fade;
    [SerializeField] private GameObject endingCard;
    [SerializeField] private Text endingTitle;
    [SerializeField] private Text endingQuote;
    [SerializeField] private RawImage endingIllustration;
    [SerializeField] private Texture selfIllustration;
    [SerializeField] private Texture otherIllustration;

    [Header("音效")]
    [SerializeField] private AudioSource foley;
    [SerializeField] private AudioClip rumble;
    [SerializeField] private AudioClip escapeDoorSound;
    [SerializeField] private AudioClip footsteps;
    [SerializeField] private AudioClip breath;
    [SerializeField] private AudioClip clatter;
    [SerializeField] private AudioClip impact;
    [SerializeField] private StoryAudioBed music;
    [SerializeField] private StoryAudioBed pulse;
    [SerializeField] private AudioClip eerieMusic;
    [SerializeField] private AudioClip warmMusic;
    [SerializeField] private AudioClip despairMusic;
    [SerializeField] private AudioClip panicHeartbeat;

    private const string ChoiceKey = "finalChoice";
    private const string SelfValue = "self";

    private Vector3 cameraCenter;
    private Vector3 framedCenter;
    private float cameraSize;
    private float shakeUntil;
    private float shakeAmplitude;
    private bool blockingAdvance;
    private bool cardShown;
    private Font endingFont;
    private PartySan partySan;

    private void Awake()
    {
        cameraCenter = framedCenter = cameraPivot.position;
        cameraSize = gameCamera.m_Lens.OrthographicSize;
        foreach (SpriteRenderer hunter in hunters)
        {
            hunter.gameObject.SetActive(false);
        }

        foreach (SpriteRenderer smoke in hunterSmoke)
        {
            smoke.gameObject.SetActive(false);
        }

        ritualCircle.SetActive(false);
        floorRift.gameObject.SetActive(false);
        statueRubble.SetActive(false);
        exitLight.SetActive(false);
        flashlight.enabled = false;
        endingCard.SetActive(false);
        endingFont = MainMenu.ApplyMenuFont(endingCard);
        fade.alpha = 1f;
        fade.blocksRaycasts = false;

        // 结局房只演出不写档，退出游戏时保留第三层之前的进度。
        SceneSave save = FindObjectOfType<SceneSave>();
        if (save != null)
        {
            partySan = save.PartySan;
            save.BlockProgressWrite();
        }
    }

    private void OnEnable()
    {
        dialogue.LineStarted += OnLineStarted;
        dialogue.CanAdvanceLine = () => !blockingAdvance;
    }

    private void OnDisable()
    {
        dialogue.LineStarted -= OnLineStarted;
        dialogue.CanAdvanceLine = null;
    }

    private void Start()
    {
        // 队伍初始化完成后，由演出控制 Cinemachine 的跟随点。
        party.SetCameraFocus(cameraPivot, snap: true);
    }

    private void OnDestroy()
    {
        if (endingFont != null)
        {
            Destroy(endingFont);
        }
    }

    private void LateUpdate()
    {
        float blend = 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime);
        framedCenter = Vector3.Lerp(framedCenter, cameraCenter, blend);
        float shake = Mathf.Clamp01((shakeUntil - Time.unscaledTime) * 2f) * shakeAmplitude;
        cameraPivot.position = framedCenter + (Vector3)(Random.insideUnitCircle * shake);
        gameCamera.m_Lens.OrthographicSize = Mathf.Lerp(gameCamera.m_Lens.OrthographicSize, cameraSize, blend);

        if (flashlightGlow.enabled)
        {
            flashlightGlow.transform.position = flashlight.enabled
                ? flashlight.transform.position + Vector3.left * .55f
                : protagonist.transform.position + Vector3.up * 1.25f;
        }
    }

    // DialogueTrigger 在最后一句结束后调用，立即收回刚交还的移动权。
    public void OnDialogueFinished()
    {
        if (cardShown)
        {
            return;
        }

        cardShown = true;
        party.SetGameplayLocked(true);
        StartCoroutine(ShowEndingCard(dialogue.HasStoryValue(ChoiceKey, SelfValue)));
    }

    private void OnLineStarted(string id)
    {
        switch (id)
        {
            case "final_sacrifice_arrival":
                Play(breath, .55f, .76f);
                StartCoroutine(PlayBeat(FadeTo(0f, .7f)));
                break;
            case "final_arrival":
                if (fade.alpha > 0f)
                {
                    StartCoroutine(PlayBeat(FadeTo(0f, .7f)));
                }
                break;
            case "final_memory":
                music.Play(warmMusic, .23f);
                break;
            case "final_friend_voice":
                music.Play(eerieMusic, .24f);
                Frame(new Vector2(0f, 2.8f), 5.2f);
                Play(breath, .45f, .8f);
                StartCoroutine(PlayBeat(PulseStatue()));
                break;
            case "final_footsteps":
                // 对话框占据画面下方，让南侧进场的追猎者完整出现在对话框上方。
                Frame(new Vector2(0f, -3.85f), 6.35f);
                StartCoroutine(PlayBeat(ApproachingFootsteps()));
                break;
            case "final_hunters":
                StartCoroutine(PlayBeat(RevealHunters()));
                break;
            case "final_brother_voice":
                Play(breath, .75f, .63f);
                Shake(.45f, .07f);
                break;
            case "final_circle":
                Frame(new Vector2(0f, .55f), 6.1f);
                StartCoroutine(PlayBeat(IgniteCircle()));
                break;
            case "final_choice":
                music.Play(despairMusic, .3f);
                pulse.Play(panicHeartbeat, .35f);
                break;
            case "ending_other_1":
                pulse.Play(panicHeartbeat, .42f);
                break;
            case "ending_self_1":
                pulse.FadeOut();
                break;
            case "ending_self_2":
                StartCoroutine(PlayBeat(LayDownFlashlight()));
                break;
            case "ending_self_3":
                StartCoroutine(PlayBeat(PerformSelfSacrifice()));
                break;
            case "ending_self_4":
                StartCoroutine(PlayBeat(FadeTo(1f, .8f)));
                break;
            case "ending_other_2":
                pulse.FadeOut();
                StartCoroutine(PlayBeat(WalkTo(protagonist.SavePosition + Vector3.down * .75f, 1.5f)));
                break;
            case "ending_other_3":
                StartCoroutine(PlayBeat(BreakStatue()));
                break;
            case "ending_other_4":
                StartCoroutine(PlayBeat(Escape()));
                break;
            case "ending_other_5":
                StartCoroutine(PlayBeat(DrainSanAndFade()));
                break;
        }
    }

    private IEnumerator PlayBeat(IEnumerator action)
    {
        blockingAdvance = true;
        yield return action;
        blockingAdvance = false;
    }

    private IEnumerator DrainSanAndFade()
    {
        if (partySan != null && partySan.CurrentSan > 0)
        {
            const float duration = 2.2f;
            int startingSan = partySan.CurrentSan;
            pulse.Play(panicHeartbeat, .42f);
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                partySan.SetEndingSan(Mathf.CeilToInt(Mathf.Lerp(startingSan, 0f, elapsed / duration)));
                yield return null;
            }
            partySan.SetEndingSan(0);
            pulse.FadeOut();
            yield return new WaitForSecondsRealtime(.45f);
        }

        yield return FadeTo(1f, .75f);
    }

    private IEnumerator PulseStatue()
    {
        Vector3 restScale = statue.localScale;
        for (float elapsed = 0f; elapsed < .85f; elapsed += Time.unscaledDeltaTime)
        {
            statue.localScale = restScale * (1f + Mathf.Sin(elapsed / .85f * Mathf.PI) * .032f);
            yield return null;
        }

        statue.localScale = restScale;
    }

    private IEnumerator ApproachingFootsteps()
    {
        for (int step = 0; step < 3; step++)
        {
            Play(footsteps, .34f + step * .15f, .64f + step * .09f);
            Shake(.21f, .055f + step * .035f);
            yield return new WaitForSecondsRealtime(.26f);
        }
    }

    private IEnumerator RevealHunters()
    {
        float duration = .63f + (hunters.Length - 1) * .12f;
        Vector3[] origins = System.Array.ConvertAll(hunters, hunter => hunter.transform.position);
        foreach (SpriteRenderer hunter in hunters)
        {
            SetOpacity(hunter, 0f);
            hunter.gameObject.SetActive(true);
        }

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            for (int index = 0; index < hunters.Length; index++)
            {
                float stepTime = Mathf.Clamp(elapsed - index * .12f, 0f, .63f);
                float progress = Mathf.SmoothStep(0f, 1f, stepTime / .63f);
                SpriteRenderer hunter = hunters[index];
                hunter.transform.position = origins[index] + Vector3.up * progress * 1.05f;
                SetHunterPose(hunter, origins[index].x, stepTime, walking: stepTime > 0f && stepTime < .63f);
                SetOpacity(hunter, progress);
            }

            yield return null;
        }

        for (int index = 0; index < hunters.Length; index++)
        {
            hunters[index].transform.position = origins[index] + Vector3.up * 1.05f;
            SetHunterPose(hunters[index], origins[index].x, 0f, walking: false);
            SetOpacity(hunters[index], 1f);
        }
    }

    private void SetHunterPose(SpriteRenderer hunter, float startX, float elapsed, bool walking)
    {
        if (hunterWalkSprites == null)
        {
            return;
        }

        PlayerWalkSprites.Facing facing = startX < 0f ? PlayerWalkSprites.Facing.Right : PlayerWalkSprites.Facing.Left;
        hunter.sprite = hunterWalkSprites.GetFrame(facing, walking, elapsed);
    }

    private IEnumerator IgniteCircle()
    {
        SpriteRenderer[] marks = ritualCircle.GetComponentsInChildren<SpriteRenderer>();
        float[] opacities = System.Array.ConvertAll(marks, mark => mark.color.a);
        foreach (SpriteRenderer mark in marks)
        {
            SetOpacity(mark, 0f);
        }

        ritualCircle.SetActive(true);
        Play(rumble, .5f, 1.15f);
        for (float elapsed = 0f; elapsed < .65f; elapsed += Time.unscaledDeltaTime)
        {
            for (int index = 0; index < marks.Length; index++)
            {
                SetOpacity(marks[index], opacities[index] * Mathf.SmoothStep(0f, 1f, elapsed / .65f));
            }

            yield return null;
        }

        for (int index = 0; index < marks.Length; index++)
        {
            SetOpacity(marks[index], opacities[index]);
        }
    }

    private IEnumerator LayDownFlashlight()
    {
        flashlight.transform.position = protagonist.SavePosition + new Vector3(-.65f, -.15f, 0f);
        flashlight.transform.localRotation = Quaternion.Euler(0f, 0f, 27f);
        flashlight.enabled = true;
        Play(clatter, .75f);
        Frame(new Vector2(0f, 2.25f), 5.2f);
        yield return WalkTo(statueMark.position, 2.8f);
        flashlightGlow.enabled = false;
    }

    private IEnumerator PerformSelfSacrifice()
    {
        Frame(new Vector2(0f, 1.3f), 5.65f);
        yield return WalkTo(sacrificeMark.position, 2.45f);

        Play(rumble, .9f, .74f);
        Shake(1.5f, .19f);
        floorRift.gameObject.SetActive(true);
        Vector3 riftScale = floorRift.localScale;
        Vector3 actorScale = protagonist.transform.localScale;
        Vector3[] hunterStarts = System.Array.ConvertAll(hunters, hunter => hunter.transform.position);
        Vector3 statueStart = statue.localPosition;
        Vector3 statueScale = statue.localScale;
        const float duration = 1.25f;

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            floorRift.localScale = riftScale * Mathf.Lerp(.12f, 1f, Mathf.Clamp01(progress * 2f));
            protagonist.transform.localScale = actorScale * Mathf.Lerp(1f, .1f, Mathf.SmoothStep(0f, 1f, progress));
            for (int index = 0; index < hunters.Length; index++)
            {
                Vector3 edge = sacrificeMark.position + (hunterStarts[index] - sacrificeMark.position).normalized * .42f;
                hunters[index].transform.position = Vector3.Lerp(hunterStarts[index], edge, Mathf.SmoothStep(0f, 1f, progress));
                SetHunterPose(hunters[index], hunterStarts[index].x, elapsed, walking: true);
            }

            float fall = Mathf.SmoothStep(0f, 1f, (progress - .38f) / .62f);
            statue.localPosition = statueStart + Vector3.down * (.72f * fall);
            statue.localScale = Vector3.Scale(statueScale, new Vector3(1f, 1f - fall * .6f, 1f));
            yield return null;
        }

        floorRift.localScale = riftScale;
        foreach (SpriteRenderer renderer in protagonist.GetComponentsInChildren<SpriteRenderer>())
        {
            renderer.enabled = false;
        }

        ReplaceStatueWithRubble();
        yield return DissolveHunters();
    }

    private IEnumerator BreakStatue()
    {
        Frame(new Vector2(0f, 2.65f), 5.1f);
        yield return WalkTo(hammer.transform.position + Vector3.down * .45f, 3.2f);
        hammer.enabled = false;
        yield return WalkTo(statueMark.position, 3.6f);

        hammer.transform.position = protagonist.SavePosition + new Vector3(.45f, .72f, 0f);
        hammer.enabled = true;
        for (float elapsed = 0f; elapsed < .3f; elapsed += Time.unscaledDeltaTime)
        {
            hammer.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(130f, -35f, elapsed / .3f));
            yield return null;
        }

        Play(impact, .9f, .81f);
        Play(rumble, .8f, .68f);
        Shake(1.1f, .18f);
        hammer.enabled = false;
        flashlightGlow.enabled = false;
        ReplaceStatueWithRubble();
        ritualCircle.SetActive(false);
        yield return DissolveHunters();

        Frame(new Vector2(2.8f, 4.3f), 5.45f);
        exitLight.SetActive(true);
        Play(escapeDoorSound, .78f, .95f);
        Vector3 closedPosition = escapeDoor.localPosition;
        for (float elapsed = 0f; elapsed < .62f; elapsed += Time.unscaledDeltaTime)
        {
            escapeDoor.localPosition = closedPosition + Vector3.up * (2.5f * Mathf.SmoothStep(0f, 1f, elapsed / .62f));
            yield return null;
        }

        escapeDoor.gameObject.SetActive(false);
        doorBlocker.enabled = false;
    }

    private void ReplaceStatueWithRubble()
    {
        statue.gameObject.SetActive(false);
        statueFoundation.enabled = false;
        statueRubble.SetActive(true);
    }

    private IEnumerator DissolveHunters()
    {
        Play(breath, .6f, 1.35f);
        for (int index = 0; index < hunters.Length; index++)
        {
            hunterSmoke[index].transform.position = hunters[index].transform.position;
            SetOpacity(hunterSmoke[index], 0f);
            hunterSmoke[index].gameObject.SetActive(true);
        }

        for (float elapsed = 0f; elapsed < .65f; elapsed += Time.unscaledDeltaTime)
        {
            float progress = elapsed / .65f;
            for (int index = 0; index < hunters.Length; index++)
            {
                SetOpacity(hunters[index], 1f - progress);
                SetOpacity(hunterSmoke[index], Mathf.Sin(progress * Mathf.PI) * .88f);
                hunterSmoke[index].transform.position += Vector3.up * Time.unscaledDeltaTime * .8f;
            }

            yield return null;
        }

        foreach (SpriteRenderer hunter in hunters)
        {
            hunter.gameObject.SetActive(false);
        }

        foreach (SpriteRenderer smoke in hunterSmoke)
        {
            smoke.gameObject.SetActive(false);
        }
    }

    private IEnumerator Escape()
    {
        Frame(new Vector2(3.2f, 5.4f), 5.2f);
        yield return WalkTo(doorApproach.position, 6.6f);
        yield return WalkTo(doorMark.position, 6.6f);
        foreach (SpriteRenderer renderer in protagonist.GetComponentsInChildren<SpriteRenderer>())
        {
            renderer.enabled = false;
        }
    }

    private IEnumerator WalkTo(Vector3 destination, float speed)
    {
        while (protagonist.SavePosition != destination)
        {
            protagonist.MoveForStory(Vector3.MoveTowards(
                protagonist.SavePosition, destination, speed * Time.unscaledDeltaTime));
            yield return null;
        }
    }

    private IEnumerator ShowEndingCard(bool sacrificedSelf)
    {
        yield return FadeTo(1f, .35f);
        endingTitle.text = sacrificedSelf ? "结局一 · 团灭" : "结局二 · 人格迷失";
        endingQuote.text = sacrificedSelf
            ? "你问谁会来救我们？黑暗中没有回答。\n这里既是荒岛，也是迷宫，更是我们的坟墓。"
            : "门后没有海风，只有无尽的深渊。\n你终于逃出了格兰岛，但你也把最真实的自己，留在了更深的迷宫里。";
        endingIllustration.texture = sacrificedSelf ? selfIllustration : otherIllustration;
        endingIllustration.GetComponent<AspectRatioFitter>().aspectRatio =
            (float)endingIllustration.texture.width / endingIllustration.texture.height;
        endingIllustration.color = sacrificedSelf ? new Color(.7f, .69f, .74f) : new Color(.66f, .73f, .8f);
        endingCard.SetActive(true);
    }

    private IEnumerator FadeTo(float alpha, float duration)
    {
        float from = fade.alpha;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            fade.alpha = Mathf.Lerp(from, alpha, elapsed / duration);
            yield return null;
        }

        fade.alpha = alpha;
    }

    private void Frame(Vector2 center, float size)
    {
        cameraCenter = center;
        cameraSize = size;
    }

    private void Shake(float duration, float magnitude)
    {
        shakeUntil = Time.unscaledTime + duration;
        shakeAmplitude = magnitude;
    }

    private void Play(AudioClip clip, float volume, float pitch = 1f)
    {
        foley.pitch = pitch;
        foley.PlayOneShot(clip, volume);
    }

    private static void SetOpacity(SpriteRenderer renderer, float opacity)
    {
        Color color = renderer.color;
        color.a = opacity;
        renderer.color = color;
    }
}
