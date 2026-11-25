using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 序章只负责对话节点对应的画面、雨声和场景出口。
[DisallowMultipleComponent, RequireComponent(typeof(SceneSave))]
public class PrologueDirector : MonoBehaviour
{
    [SerializeField] private DialogueController dialogue;
    [SerializeField] private TextAsset dialogueFile;
    [SerializeField] private RawImage illustration;
    [SerializeField] private Texture departureIllustration;
    [SerializeField] private AudioSource rainAudio;
    [SerializeField] private Image blackout;
    // 格兰岛外部场景加入构建列表后，继续航行分支会从这里进入。
    [SerializeField] private string arrivalScenePath = SceneSave.MainStoryDirectory + "GameScene0.unity";

    private float targetRainVolume;
    private SceneSave sceneSave;

    private void OnEnable()
    {
        dialogue.LineStarted += OnLineStarted;
    }

    private void Start()
    {
        sceneSave = GetComponent<SceneSave>();
        if (sceneSave.HasRestoredProgress && dialogue.HasStoryValue("prologueRoute", "island"))
        {
            OpenArrival();
            return;
        }

        if (!dialogue.TryPlay(dialogueFile, null, FinishPrologue))
        {
            Debug.LogError("序章未能开始，请检查对话文件和场景引用。", this);
            ReturnToMenu();
        }
    }

    private void OnDisable()
    {
        dialogue.LineStarted -= OnLineStarted;
    }

    private void Update()
    {
        if (rainAudio.isPlaying)
        {
            rainAudio.volume = Mathf.MoveTowards(
                rainAudio.volume, targetRainVolume, Time.unscaledDeltaTime * 0.4f);
        }
    }

    // 画面和音效只在对应的演出节点改变，台词和选项仍由对话系统推进。
    private void OnLineStarted(string nodeId)
    {
        switch (nodeId)
        {
            case "departure":
                illustration.texture = departureIllustration;
                break;
            case "rain":
                rainAudio.volume = 0f;
                targetRainVolume = 0.5f;
                rainAudio.Play();
                break;
            case "return_home":
                illustration.enabled = false;
                rainAudio.Stop();
                break;
        }
    }

    private void FinishPrologue()
    {
        if (dialogue.HasStoryValue("prologueRoute", "island"))
        {
            sceneSave.CaptureCheckpoint();
            StartCoroutine(FadeToArrival());
        }
        else
        {
            ReturnToMenu();
        }
    }

    private IEnumerator FadeToArrival()
    {
        targetRainVolume = 0f;
        const float duration = 0.65f;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            blackout.color = new Color(0f, 0f, 0f, elapsed / duration);
            yield return null;
        }

        OpenArrival();
    }

    private void OpenArrival()
    {
        blackout.color = Color.black;
        int sceneIndex = SceneUtility.GetBuildIndexByScenePath(arrivalScenePath);
        if (sceneIndex >= 0 && SceneSave.IsMainStoryScene(arrivalScenePath))
        {
            SceneManager.LoadScene(sceneIndex);
            return;
        }

        // 外部地图尚未加入构建列表时，停在可确认的序章结尾。
        rainAudio.Stop();
        illustration.enabled = false;
        blackout.color = Color.clear;
        if (!dialogue.TryPlay(dialogueFile, "prologue_end", ReturnToMenu))
        {
            ReturnToMenu();
        }
    }

    private void ReturnToMenu()
    {
        SceneManager.LoadScene(SceneUtility.GetBuildIndexByScenePath(SceneSave.MainMenuPath));
    }
}
