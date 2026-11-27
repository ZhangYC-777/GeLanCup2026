using UnityEngine;

// 场景内的环境声或配乐。两条音轨交叉淡化，场景卸载时一起停止。
public sealed class StoryAudioBed : MonoBehaviour
{
    [SerializeField] private AudioClip startingClip;
    [SerializeField, Range(0f, 1f)] private float startingVolume = .25f;

    private const float FadeSeconds = .55f;
    private AudioSource current;
    private AudioSource outgoing;
    private float targetVolume;
    private float currentFadeRate;
    private float outgoingFadeRate;

    private void Awake()
    {
        current = CreateSource();
        outgoing = CreateSource();
        if (startingClip != null)
            Play(startingClip, startingVolume);
    }

    private AudioSource CreateSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.loop = true;
        source.volume = 0f;
        return source;
    }

    public void Play(AudioClip clip, float volume)
    {
        if (clip == null)
            return;

        if (current.clip != clip)
        {
            AudioSource next = outgoing;
            outgoing = current;
            outgoingFadeRate = outgoing.volume / FadeSeconds;
            current = next;
            if (current.clip != clip)
            {
                current.Stop();
                current.clip = clip;
                current.volume = 0f;
            }
        }

        targetVolume = Mathf.Clamp01(volume);
        currentFadeRate = Mathf.Abs(targetVolume - current.volume) / FadeSeconds;
        if (!current.isPlaying)
            current.Play();
    }

    public void FadeOut()
    {
        targetVolume = 0f;
        currentFadeRate = current.volume / FadeSeconds;
    }

    private void Update()
    {
        Fade(current, targetVolume, currentFadeRate);
        Fade(outgoing, 0f, outgoingFadeRate);
    }

    private static void Fade(AudioSource source, float target, float rate)
    {
        source.volume = Mathf.MoveTowards(source.volume, target, rate * Time.unscaledDeltaTime);
        if (target == 0f && source.volume == 0f && source.isPlaying)
            source.Stop();
    }

    private void OnDisable()
    {
        current.Stop();
        outgoing.Stop();
        current.volume = 0f;
        outgoing.volume = 0f;
        targetVolume = 0f;
    }
}
