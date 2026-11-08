using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Estado da run "endless": Café -> Preparação -> Fight Club -> Recuperação -> dia seguinte.
// Vive entre cenas (DontDestroyOnLoad) e é criado sozinho se abrires qualquer cena diretamente no editor.
public class RunManager : MonoBehaviour
{
    public const string TitleScene = "TitleScreen";
    public const string IntroScene = "CoffeeVideo";
    public const string CafeScene = "CoffeeShop2";
    public const string HubScene = "NightHub";
    public const string FightScene = "Level1";
    public const string HospitalScene = "Hospital";

    public const int MaxHealth = 100;
    public const int MaxFirstAid = 3;
    public const int FirstAidHeal = 30;
    public const int FirstAidCost = 35;
    public const int PatchUpHeal = 25;
    public const int PatchUpCost = 20;
    public const int LightTreatmentHeal = 30;
    public const int LightTreatmentCost = 25;
    public const int OvernightRecovery = 15;
    public const int OrderReward = 20;
    public const int WrongOrderPenalty = 10;
    public const int TimeoutPenalty = 5;
    public const float ShiftDuration = 60f;
    private const string BestKey = "Mayhem_BestNights";
    private const string BestWaveKey = "Mayhem_BestWave";
    private const string BestCozyKey = "Mayhem_BestCozyMoney";

    public enum HubMode { Prep, Recovery, GameOver, EndlessOver }

    // Run = café + Fight Club (noites) · CozyCafe = só café, sem pressão · EndlessFight = só luta, vagas infinitas
    public enum GameMode { Run, CozyCafe, EndlessFight }
    public GameMode Mode { get; private set; } = GameMode.Run;
    public bool IsCozy => Mode == GameMode.CozyCafe;
    public bool IsEndlessFight => Mode == GameMode.EndlessFight;

    // Modo Só Luta
    public int Wave { get; private set; } = 1;
    public int BestWave => GetBest(BestWaveKey);
    public int WavesSurvived => Wave - 1;

    // Modo Café Cozy
    public int BestCozyMoney => GetBest(BestCozyKey);

    private static RunManager instance;
    public static RunManager Instance
    {
        get
        {
            if (instance == null && Application.isPlaying)
            {
                GameObject go = new GameObject("RunManager");
                instance = go.AddComponent<RunManager>();
            }
            return instance;
        }
    }

    // Estado persistente da run
    public int Night { get; private set; } = 1;
    public int Money { get; private set; }
    public int Health { get; private set; } = MaxHealth;
    public int FirstAid { get; private set; }
    public float ShiftStress { get; private set; }
    public HubMode NextHubMode { get; private set; } = HubMode.Prep;
    public int BestNights => GetBest(BestKey);
    public int NightsSurvived => Night - 1;

    // Recordes em cache: o PlayerPrefs só é lido uma vez por chave (no Windows vai ao registo).
    private readonly System.Collections.Generic.Dictionary<string, int> bestCache = new System.Collections.Generic.Dictionary<string, int>();

    private int GetBest(string key)
    {
        if (!bestCache.TryGetValue(key, out int value))
        {
            value = PlayerPrefs.GetInt(key, 0);
            bestCache[key] = value;
        }
        return value;
    }

    private void SetBest(string key, int value)
    {
        bestCache[key] = value;
        PlayerPrefs.SetInt(key, value);
    }

    // Relatório do dia
    public int ShiftEarned { get; private set; }
    public int ShiftOrders { get; private set; }
    public int ShiftMistakes { get; private set; }
    public int ShiftTips { get; private set; }
    public int SpentToday { get; private set; }
    public int HealthLostToday { get; private set; }
    public int EnemiesBeatenToday { get; private set; }
    public int TotalEnemiesBeaten { get; private set; }
    public bool NewRecord { get; private set; }

    public bool InFight { get; set; }
    public bool Paused { get; private set; }

    private Canvas overlay;
    private Image fade;
    private GameObject pausePanel;
    private Button resumeButton;
    private Button quitButton;
    private bool loading;
    private int hitStopDepth;
    private float slowMoScale = 1f;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        BuildOverlay();
        gameObject.AddComponent<PostFXDirector>();
    }

    // ---------------- Dificuldade por noite ----------------

    // Nível de dificuldade da luta: a noite (Run) ou, no Só Luta, sobe uma vez a cada 2 vagas.
    public int Level => IsEndlessFight ? 1 + (Wave - 1) / 2 : Night;

    public float CustomerPatience => IsCozy ? 40f : Mathf.Max(11f, 20f - (Night - 1) * 1.5f);
    public float FoodColdTime => IsCozy ? 20f : Mathf.Max(2.5f, 4.5f - (Night - 1) * 0.3f);
    public int MaxCustomers => IsCozy ? 3 : Mathf.Min(4, 2 + Night / 2);
    public int FightRounds => Mathf.Min(5, 2 + (Level - 1) / 2);
    public int EnemiesInRound(int round) => Mathf.Min(8, 2 + (Level - 1) + (round - 1));
    public int EnemiesInWave => Mathf.Min(12, 2 + (Wave - 1) * 2 / 3);
    public int MaxEnemiesAlive => Mathf.Min(5, 2 + (Level - 1) / 2);
    public int MaxSimultaneousAttackers => Level >= 7 ? 3 : Level >= 3 ? 2 : 1;
    public float EnemyAggressionScale => Mathf.Max(0.5f, 1f - 0.08f * (Level - 1));
    public float EnemyDamageScale => 1f + 0.08f * (Level - 1);
    public int StartingFury => IsEndlessFight ? 0 : Mathf.RoundToInt(ShiftStress * 0.5f);
    public int WrongOrderCost => IsCozy ? 0 : WrongOrderPenalty;
    public int TimeoutCost => IsCozy ? 0 : TimeoutPenalty;
    public int FullTreatmentCost => Mathf.CeilToInt((MaxHealth - Health) * 1.2f);

    // ---------------- Fluxo da run ----------------

    public void NewRun()
    {
        Mode = GameMode.Run;
        SkippedWork = false;
        Night = 1;
        Money = 0;
        Health = MaxHealth;
        FirstAid = 0;
        ShiftStress = 0;
        TotalEnemiesBeaten = 0;
        NewRecord = false;
        ResetDay();
    }

    private void ResetDay()
    {
        ShiftEarned = ShiftOrders = ShiftMistakes = ShiftTips = 0;
        SpentToday = HealthLostToday = EnemiesBeatenToday = 0;
    }

    public void StartShift() => LoadScene(CafeScene);

    public void EndShift(int earned, int orders, int mistakes, int tips, float stress)
    {
        ShiftEarned = earned;
        ShiftOrders = orders;
        ShiftMistakes = mistakes;
        ShiftTips = tips;
        ShiftStress = Mathf.Clamp(stress, 0f, 100f);
        Money += earned;
        NextHubMode = HubMode.Prep;
        LoadScene(HubScene);
    }

    public void StartFight() => LoadScene(FightScene);

    public void EndFight(int healthLeft, int beaten)
    {
        InFight = false;
        HealthLostToday += Mathf.Max(0, Health - healthLeft);
        Health = Mathf.Clamp(healthLeft, 1, MaxHealth);
        EnemiesBeatenToday = beaten;
        TotalEnemiesBeaten += beaten;
        Health = Mathf.Min(MaxHealth, Health + OvernightRecovery);
        if (Night > BestNights)
        {
            SetBest(BestKey, Night);
            PlayerPrefs.Save();
            NewRecord = true;
        }
        // o médico decide contigo o que fazer a seguir
        NextHubMode = HubMode.Recovery;
        LoadScene(HospitalScene);
    }

    // Faltar ao trabalho: sem salário nesse dia, mas vais para a luta com a raiva toda.
    public const int SkipWorkFury = 30;
    public bool SkippedWork { get; private set; }

    public void SkipWorkToFight()
    {
        Night++;
        ResetDay();
        SkippedWork = true;
        ShiftStress = SkipWorkFury * 2f;
        NextHubMode = HubMode.Prep;
        LoadScene(HubScene);
    }

    public void KnockedOut(int beaten)
    {
        InFight = false;
        EnemiesBeatenToday = beaten;
        TotalEnemiesBeaten += beaten;
        Health = 0;
        NextHubMode = HubMode.GameOver;
        LoadScene(HospitalScene);
    }

    public void NextDay()
    {
        Night++;
        ResetDay();
        SkippedWork = false;
        StartShift();
    }

    public void RecordHealth(int health)
    {
        Health = Mathf.Clamp(health, 0, MaxHealth);
    }

    // ---------------- Modo Café Cozy ----------------

    public void StartCozy()
    {
        NewRun();
        Mode = GameMode.CozyCafe;
        LoadScene(CafeScene);
    }

    public void RecordCozyMoney(int money)
    {
        if (money <= BestCozyMoney) return;
        SetBest(BestCozyKey, money);
        PlayerPrefs.Save();
    }

    // ---------------- Modo Só Luta ----------------

    public void StartEndless()
    {
        NewRun();
        Mode = GameMode.EndlessFight;
        Wave = 1;
        FirstAid = 1;
        LoadScene(FightScene);
    }

    // Chamado quando uma vaga fica limpa. Devolve true se ganhou um kit.
    public bool EndlessWaveCleared()
    {
        if (Wave > BestWave)
        {
            SetBest(BestWaveKey, Wave);
            PlayerPrefs.Save();
            NewRecord = true;
        }
        Wave++;
        if (Wave % 3 == 1 && FirstAid < MaxFirstAid)
        {
            FirstAid++;
            return true;
        }
        return false;
    }

    public void EndlessKnockedOut(int beaten)
    {
        InFight = false;
        TotalEnemiesBeaten += beaten;
        Health = 0;
        NextHubMode = HubMode.EndlessOver;
        LoadScene(HospitalScene);
    }

    // ---------------- Compras ----------------

    public bool TrySpend(int cost) => Spend(cost);

    private bool Spend(int cost)
    {
        if (Money < cost) return false;
        Money -= cost;
        SpentToday += cost;
        return true;
    }

    public bool BuyPatchUp()
    {
        if (Health >= MaxHealth || !Spend(PatchUpCost)) return false;
        Health = Mathf.Min(MaxHealth, Health + PatchUpHeal);
        return true;
    }

    public bool BuyFirstAid()
    {
        if (FirstAid >= MaxFirstAid || !Spend(FirstAidCost)) return false;
        FirstAid++;
        return true;
    }

    public bool BuyLightTreatment()
    {
        if (Health >= MaxHealth || !Spend(LightTreatmentCost)) return false;
        Health = Mathf.Min(MaxHealth, Health + LightTreatmentHeal);
        return true;
    }

    public bool BuyFullTreatment()
    {
        if (Health >= MaxHealth || !Spend(FullTreatmentCost)) return false;
        Health = MaxHealth;
        return true;
    }

    public bool UseFirstAid()
    {
        if (FirstAid <= 0) return false;
        FirstAid--;
        return true;
    }

    // ---------------- Cenas / fade ----------------

    public void GoToTitle()
    {
        LoadScene(TitleScene);
    }

    public void LoadScene(string sceneName, float fadeTime = 0.6f)
    {
        if (loading) return;
        SetPaused(false);
        StartCoroutine(LoadRoutine(sceneName, fadeTime));
    }

    private IEnumerator LoadRoutine(string sceneName, float fadeTime)
    {
        loading = true;
        yield return FadeTo(1f, fadeTime);
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone) yield return null;
        Time.timeScale = 1f;
        hitStopDepth = 0;
        slowMoScale = 1f;
        yield return FadeTo(0f, fadeTime);
        loading = false;
    }

    private IEnumerator FadeTo(float target, float duration)
    {
        fade.raycastTarget = true;
        float start = fade.color.a;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            SetFadeAlpha(Mathf.Lerp(start, target, t / duration));
            yield return null;
        }
        SetFadeAlpha(target);
        fade.raycastTarget = target > 0.5f;
    }

    private void SetFadeAlpha(float a)
    {
        Color c = fade.color;
        c.a = a;
        fade.color = c;
    }

    // ---------------- Game feel ----------------

    // Câmara lenta (K.O.): abranda o jogo durante uns segundos reais.
    public void SlowMotion(float scale, float realSeconds)
    {
        StartCoroutine(SlowMotionRoutine(scale, realSeconds));
    }

    private IEnumerator SlowMotionRoutine(float scale, float realSeconds)
    {
        slowMoScale = scale;
        if (!Paused && hitStopDepth <= 0) Time.timeScale = scale;
        yield return new WaitForSecondsRealtime(realSeconds);
        slowMoScale = 1f;
        if (!Paused && hitStopDepth <= 0) Time.timeScale = 1f;
    }

    // Congela o jogo por uns milissegundos quando um golpe acerta: dá peso ao impacto.
    public void HitStop(float duration)
    {
        if (Paused || loading) return;
        StartCoroutine(HitStopRoutine(duration));
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        hitStopDepth++;
        Time.timeScale = 0.02f;
        yield return new WaitForSecondsRealtime(duration);
        hitStopDepth--;
        if (hitStopDepth <= 0 && !Paused) Time.timeScale = slowMoScale;
    }

    // ---------------- Pausa ----------------

    private void Update()
    {
        if (loading) return;
        if (GameInput.Player.Pause.WasPressedThisFrame())
        {
            string scene = SceneManager.GetActiveScene().name;
            if (scene == CafeScene || scene == FightScene) SetPaused(!Paused);
        }
    }

    public void SetPaused(bool value)
    {
        Paused = value;
        pausePanel.SetActive(value);
        Time.timeScale = value ? 0f : slowMoScale;
        AudioListener.pause = value;
        if (value) UIFactory.SetLabel(quitButton, IsCozy ? "Quit to menu" : IsEndlessFight ? "Give up" : "Give up run");
        if (value && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
    }

    private void BuildOverlay()
    {
        overlay = UIFactory.CreateCanvas("Overlay", 1000, transform);
        pausePanel = UIFactory.Stretch("Pause", overlay.transform, new Color(0.03f, 0.02f, 0.02f, 0.94f)).gameObject;
        Vector2 c = new Vector2(0.5f, 0.5f);
        UIFactory.Text(pausePanel.transform, "PAUSED", 110, c, new Vector2(0, 220), new Vector2(900, 160), color: UIFactory.Blood);
        resumeButton = UIFactory.Button(pausePanel.transform, "Resume", c, new Vector2(0, 40), new Vector2(520, 90), () => SetPaused(false));
        quitButton = UIFactory.Button(pausePanel.transform, "Give up run", c, new Vector2(0, -80), new Vector2(520, 90), () =>
        {
            SetPaused(false);
            GoToTitle();
        });
        UIFactory.ChainVertical(resumeButton, quitButton);
        UIFactory.Text(pausePanel.transform, ControlsHelp, 30, c, new Vector2(0, -260), new Vector2(1700, 220), color: UIFactory.Muted);
        pausePanel.SetActive(false);

        fade = UIFactory.Stretch("Fade", overlay.transform, new Color(0, 0, 0, 0));
        fade.raycastTarget = false;
    }

    public const string ControlsHelp =
        "Move: A/D or arrows · Stick   |   Punch (combo): J · Left click · X/Square   |   Uppercut: K · Y/Triangle\n" +
        "Block: L · Right click · RB/R1 (right on time = PARRY)   |   Dodge: H · Shift · B/Circle   |   Fury: Space · LB/L1   |   Med kit: U · A/Cross\n" +
        "Café - Prepare/Pick up/Serve: J · Enter · Space · X/A   |   Drop toast/coffee: Q/E · LT/RT";
}
