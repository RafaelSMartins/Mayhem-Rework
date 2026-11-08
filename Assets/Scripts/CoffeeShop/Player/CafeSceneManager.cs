using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Controla o turno no café: fade in, liga os sistemas, conta o tempo (o único relógio do turno)
// e no fim entrega o resultado ao RunManager, que leva o jogador para a preparação da luta.
public class CafeSceneManager : MonoBehaviour
{
    public Image fadePanel;
    public float fadeDuration = 1.5f;

    [SerializeField] private SilhouetteManager silhouetteManager;
    [SerializeField] private StressBarManager stressManager;
    [SerializeField] private FoodPreparation foodPreparation;
    [SerializeField] private MoneyManager moneyManager;
    [SerializeField] private AudioClip backgroundMusic;
    [SerializeField] private AudioSource audioSource;

    public delegate void GameStateChanged();
    public static event GameStateChanged OnGameStarted;
    public static event GameStateChanged OnGameEnded;

    public static CafeSceneManager Instance { get; private set; }
    public float ShiftDuration => RunManager.ShiftDuration;
    public float RemainingTime { get; private set; }
    public bool ShiftRunning { get; private set; }

    private void Awake()
    {
        Instance = this;
        RemainingTime = ShiftDuration;

        if (silhouetteManager == null) silhouetteManager = FindFirstObjectByType<SilhouetteManager>();
        if (stressManager == null) stressManager = FindFirstObjectByType<StressBarManager>();
        if (foodPreparation == null) foodPreparation = FindFirstObjectByType<FoodPreparation>();
        if (moneyManager == null) moneyManager = FindFirstObjectByType<MoneyManager>();

        if (silhouetteManager != null) silhouetteManager.enabled = false;
        if (stressManager != null) stressManager.enabled = false;
        if (foodPreparation != null) foodPreparation.enabled = false;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (audioSource != null && backgroundMusic != null)
        {
            audioSource.clip = backgroundMusic;
            audioSource.loop = true;
            audioSource.Play();
        }

        if (fadePanel != null)
        {
            Color panelColor = fadePanel.color;
            fadePanel.color = new Color(panelColor.r, panelColor.g, panelColor.b, 1f);
        }
        StartCoroutine(GameSequence());
    }

    private IEnumerator GameSequence()
    {
        if (RunManager.Instance.IsCozy) SetupCozy();
        yield return Fade(1f, 0f);

        ActivateGameSystems();
        ShiftRunning = true;
        if (RunManager.Instance.IsCozy) yield break; // no modo cozy o turno não acaba: sai-se pela pausa
        while (RemainingTime > 0f)
        {
            RemainingTime = Mathf.Max(0f, RemainingTime - Time.deltaTime);
            yield return null;
        }
        ShiftRunning = false;

        OnGameEnded?.Invoke();
        yield return Fade(0f, 1f);

        RunManager.Instance.EndShift(
            moneyManager != null ? moneyManager.currentMoney : 0,
            moneyManager != null ? moneyManager.OrdersServed : 0,
            moneyManager != null ? moneyManager.Mistakes : 0,
            moneyManager != null ? moneyManager.Tips : 0,
            stressManager != null ? stressManager.actualStress : 0f);
    }

    // Café Cozy: sem relógio, sem stress, sem castigos. Só servir cafés com calma.
    private TMPro.TextMeshProUGUI cozyText;
    private int lastCozyKey = -1;

    private void SetupCozy()
    {
        CountdownTimer timer = FindFirstObjectByType<CountdownTimer>();
        if (timer != null) timer.gameObject.SetActive(false);
        if (stressManager != null) stressManager.gameObject.SetActive(false);
        StressBorders borders = FindFirstObjectByType<StressBorders>(FindObjectsInactive.Include);
        if (borders != null) borders.gameObject.SetActive(false);

        Canvas canvas = UIFactory.CreateCanvas("CozyHUD", 40);
        cozyText = UIFactory.Text(canvas.transform, "", 34, new Vector2(0f, 1f), new Vector2(40, -30), new Vector2(900, 110),
            TMPro.TextAlignmentOptions.TopLeft, UIFactory.Paper);
    }

    private void Update()
    {
        if (cozyText == null || moneyManager == null) return;
        // só reconstrói o texto quando os pedidos ou o dinheiro mudam
        int key = moneyManager.OrdersServed * 100003 + moneyManager.currentMoney;
        if (key == lastCozyKey) return;
        lastCozyKey = key;
        UIFactory.SetText(cozyText,
            "COZY CAFE\n" +
            moneyManager.OrdersServed + " orders served  |  best $" + Mathf.Max(RunManager.Instance.BestCozyMoney, moneyManager.currentMoney) + "\n" +
            "<size=24>Esc / Start to quit</size>");
    }

    private IEnumerator Fade(float from, float to)
    {
        if (fadePanel == null) yield break;
        Color panelColor = fadePanel.color;
        float time = 0f;
        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            fadePanel.color = new Color(panelColor.r, panelColor.g, panelColor.b, Mathf.Lerp(from, to, time / fadeDuration));
            yield return null;
        }
        fadePanel.color = new Color(panelColor.r, panelColor.g, panelColor.b, to);
    }

    private void ActivateGameSystems()
    {
        if (silhouetteManager != null) silhouetteManager.enabled = true;
        if (stressManager != null) stressManager.enabled = true;
        if (foodPreparation != null) foodPreparation.enabled = true;
        OnGameStarted?.Invoke();
    }
}
