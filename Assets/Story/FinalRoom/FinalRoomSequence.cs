using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 三层终局房：圣女像前的最终试炼，两个结局的演出与结局卡。
[DisallowMultipleComponent]
public sealed class FinalRoomSequence : MonoBehaviour
{
    [Header("对话与队伍")]
    [SerializeField] private SwitchPlayer party;
    [SerializeField] private DialogueController dialogue;
    [SerializeField] private DialogueTrigger intro;

    [Header("角色与站位")]
    [SerializeField] private PlayerMove protagonist;
    [SerializeField] private Transform sacrificeMark;
    [SerializeField] private Transform doorMark;
    [SerializeField, Min(.1f)] private float walkSpeed = 2.4f;

    [Header("场景物件")]
    [SerializeField] private Transform statue;
    [SerializeField] private GameObject ritualCircle;
    [SerializeField] private GameObject escapeDoor;
    [SerializeField] private GameObject[] hunters = new GameObject[0];
    [SerializeField] private CanvasGroup fade;
    [SerializeField] private GameObject endingCard;
    [SerializeField] private Text endingTitle;
    [SerializeField] private Text endingQuote;

    [Header("音效")]
    [SerializeField] private AudioSource foley;
    [SerializeField] private AudioClip rumble;
    [SerializeField] private AudioClip heartbeat;

    private const string ChoiceKey = "finalChoice";
    private const string SelfValue = "self";

    private const string SelfEndingTitle = "结局一 · 团灭";
    private const string SelfEndingQuote = "你问谁会来救我们？黑暗中没有回答。这里既是荒岛，也是迷宫，更是我们的坟墓。";
    private const string OtherEndingTitle = "结局二 · 人格迷失";
    private const string OtherEndingQuote = "门后没有海风，只有无尽的深渊。你终于逃出了格兰岛，但你也把最真实的自己，留在了更深的迷宫里。";

    private Vector3 statueRestScale;
    private Vector3 statueRestPosition;
    private bool cardShown;

    private void Awake()
    {
        if (statue != null)
        {
            statueRestScale = statue.localScale;
            statueRestPosition = statue.localPosition;
        }

        SetHuntersVisible(false);
        if (ritualCircle != null)
        {
            ritualCircle.SetActive(false);
        }

        if (escapeDoor != null)
        {
            escapeDoor.SetActive(false);
        }

        if (endingCard != null)
        {
            endingCard.SetActive(false);
        }

        if (fade != null)
        {
            fade.alpha = 0f;
            fade.blocksRaycasts = false;
        }
    }

    private void OnEnable()
    {
        if (dialogue != null)
        {
            dialogue.LineStarted += OnLineStarted;
        }
    }

    private void OnDisable()
    {
        if (dialogue != null)
        {
            dialogue.LineStarted -= OnLineStarted;
        }
    }

    private void Start()
    {
        // 台词开始后由对话播放器接管输入，这里只确保演出开场时角色站在起始位置。
        if (protagonist != null)
        {
            protagonist.RestorePosition(protagonist.SavePosition);
        }
    }

    // 两个结局都播完后由 DialogueTrigger 的 onFinished 调用。
    public void OnDialogueFinished()
    {
        if (cardShown)
        {
            return;
        }

        cardShown = true;
        bool sacrificedSelf = dialogue.HasStoryValue(ChoiceKey, SelfValue);
        StartCoroutine(ShowEndingCard(sacrificedSelf));
    }

    private void OnLineStarted(string id)
    {
        switch (id)
        {
            case "final_friend_voice":
                PlayFoley(heartbeat, .8f);
                StartCoroutine(PulseStatue());
                break;
            case "final_footsteps":
                PlayFoley(rumble, .7f);
                StartCoroutine(ShakeCamera(.35f, .18f));
                break;
            case "final_hunters":
                StartCoroutine(RevealHunters());
                break;
            case "final_brother_voice":
                StartCoroutine(ShakeCamera(.5f, .1f));
                break;
            case "final_circle":
                if (ritualCircle != null)
                {
                    ritualCircle.SetActive(true);
                }
                PlayFoley(heartbeat, .9f);
                StartCoroutine(ShakeCamera(.8f, .08f));
                break;
            case "ending_self_2":
                StartCoroutine(WalkTo(sacrificeMark));
                break;
            case "ending_self_3":
                StartCoroutine(CollapseStatue());
                break;
            case "ending_self_4":
                StartCoroutine(FadeToBlack(1.2f));
                break;
            case "ending_other_3":
                StartCoroutine(SmashStatue());
                break;
            case "ending_other_4":
                StartCoroutine(WalkTo(doorMark));
                break;
            case "ending_other_5":
                StartCoroutine(FadeToBlack(1.2f));
                break;
        }
    }

    private IEnumerator RevealHunters()
    {
        foreach (GameObject hunter in hunters)
        {
            if (hunter == null)
            {
                continue;
            }

            hunter.SetActive(true);
            StartCoroutine(FadeInHunter(hunter));
            PlayFoley(heartbeat, .45f);
            yield return new WaitForSeconds(.28f);
        }
    }

    private IEnumerator FadeInHunter(GameObject hunter)
    {
        SpriteRenderer[] renderers = hunter.GetComponentsInChildren<SpriteRenderer>(true);
        Vector3 from = hunter.transform.localScale * .82f;
        hunter.transform.localScale = from;
        for (float t = 0f; t < .6f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / .6f);
            hunter.transform.localScale = Vector3.Lerp(from, Vector3.one, k);
            foreach (SpriteRenderer renderer in renderers)
            {
                Color color = renderer.color;
                color.a = k;
                renderer.color = color;
            }

            yield return null;
        }
    }

    private void SetHuntersVisible(bool visible)
    {
        foreach (GameObject hunter in hunters)
        {
            if (hunter != null)
            {
                hunter.SetActive(visible);
            }
        }
    }

    private IEnumerator PulseStatue()
    {
        if (statue == null)
        {
            yield break;
        }

        for (float t = 0f; t < 1.4f; t += Time.deltaTime)
        {
            float k = Mathf.Sin(t / 1.4f * Mathf.PI * 3f) * .04f;
            statue.localScale = statueRestScale * (1f + k);
            yield return null;
        }

        statue.localScale = statueRestScale;
    }

    private IEnumerator CollapseStatue()
    {
        PlayFoley(rumble, 1f);
        Vector3 flattened = Vector3.Scale(statueRestScale, new Vector3(1.06f, .55f, 1f));
        float duration = 1.1f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            statue.localScale = Vector3.Lerp(statueRestScale, flattened, k);
            statue.localPosition = statueRestPosition + Vector3.down * (k * .8f);
            yield return null;
        }

        if (statue != null)
        {
            statue.gameObject.SetActive(false);
        }
    }

    private IEnumerator SmashStatue()
    {
        PlayFoley(rumble, 1f);
        yield return ShakeCamera(.9f, .22f);
        if (statue != null)
        {
            statue.gameObject.SetActive(false);
        }

        foreach (GameObject hunter in hunters)
        {
            if (hunter != null)
            {
                hunter.SetActive(false);
            }
        }

        if (escapeDoor != null)
        {
            escapeDoor.SetActive(true);
        }

        yield return ShakeCamera(.5f, .12f);
    }

    private IEnumerator WalkTo(Transform mark)
    {
        if (mark == null || protagonist == null)
        {
            yield break;
        }

        while (true)
        {
            Vector3 next = Vector3.MoveTowards(protagonist.SavePosition, mark.position, walkSpeed * Time.deltaTime);
            protagonist.RestorePosition(next);
            if (next == mark.position)
            {
                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator FadeToBlack(float duration)
    {
        if (fade == null)
        {
            yield break;
        }

        fade.blocksRaycasts = true;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            fade.alpha = Mathf.Clamp01(t / duration);
            yield return null;
        }

        fade.alpha = 1f;
    }

    private IEnumerator ShowEndingCard(bool sacrificedSelf)
    {
        yield return FadeToBlack(.9f);
        yield return new WaitForSeconds(.6f);

        if (endingTitle != null)
        {
            endingTitle.text = sacrificedSelf ? SelfEndingTitle : OtherEndingTitle;
        }

        if (endingQuote != null)
        {
            endingQuote.text = sacrificedSelf ? SelfEndingQuote : OtherEndingQuote;
        }

        if (endingCard != null)
        {
            endingCard.SetActive(true);
        }
    }

    private IEnumerator ShakeCamera(float duration, float magnitude)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            yield break;
        }

        Transform view = camera.transform;
        Vector3 rest = view.localPosition;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float fadeOut = 1f - t / duration;
            view.localPosition = rest + new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f), 0f) * magnitude * fadeOut;
            yield return null;
        }

        view.localPosition = rest;
    }

    private void PlayFoley(AudioClip clip, float volume)
    {
        if (foley != null && clip != null)
        {
            foley.PlayOneShot(clip, volume);
        }
    }
}
