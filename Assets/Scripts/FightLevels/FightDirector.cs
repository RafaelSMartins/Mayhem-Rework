using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Controla uma noite no Fight Club: rondas de inimigos que entram pelos dois lados ("King of the Hill"),
// cada vez mais pressão a cada noite. Sobreviver a todas as rondas = noite ganha.
public class FightDirector : MonoBehaviour
{
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private float arenaMinX = -5f;
    [SerializeField] private float arenaMaxX = 100f;
    [SerializeField] private float spawnY = -4f;
    [SerializeField] private float spawnDistance = 16f;

    public static FightDirector Instance { get; private set; }

    private readonly List<EnemyScript> alive = new List<EnemyScript>();
    private int round;
    private int toSpawn;
    private int beaten;
    private bool finished;
    private int nextSide = 1;

    private TextMeshProUGUI roundText;
    private TextMeshProUGUI enemiesText;
    private TextMeshProUGUI banner;
    private TextMeshProUGUI hint;

    private void Awake()
    {
        Instance = this;
        // corpos atravessam-se: o combate fica fluido e ninguém fica preso atrás de ninguém
        Physics2D.IgnoreLayerCollision(LayerMask.NameToLayer("Player"), LayerMask.NameToLayer("Enemy"), true);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        RunManager run = RunManager.Instance;
        run.InFight = true;
        EnemyScript.MaxAttackers = run.MaxSimultaneousAttackers;
        EnemyScript.AggressionScale = run.EnemyAggressionScale;
        BuildHud();
        // colunas à frente dos lutadores ficam transparentes quando alguém passa por trás
        SpriteRenderer playerSprite = PlayerScript.Current != null ? PlayerScript.Current.GetComponent<SpriteRenderer>() : null;
        OccluderFade.AttachToForegroundParallax(playerSprite != null ? playerSprite.sortingOrder : 1);
        StartCoroutine(run.IsEndlessFight ? EndlessRoutine() : NightRoutine());
    }

    private void ShowTutorialHint()
    {
        hint.gameObject.SetActive(true);
        UIFactory.SetText(hint,
            "<color=#FF9926>!</color> orange = block (L / RB-R1) right on time to PARRY\n" +
            "<color=#FF2626>!!</color> red = UNBLOCKABLE, dodge it (H / B-Circle)\n" +
            "Parry or perfect dodge = COUNTER with double damage");
    }

    // Modo Só Luta: vagas infinitas, cada uma um pouco mais dura. Entre vagas recuperas um pouco de vida;
    // a cada 3 vagas ganhas um kit.
    private IEnumerator EndlessRoutine()
    {
        RunManager run = RunManager.Instance;
        yield return new WaitForSeconds(0.8f);
        ShowTutorialHint();

        while (true)
        {
            round = run.Wave;
            EnemyScript.MaxAttackers = run.MaxSimultaneousAttackers;
            EnemyScript.AggressionScale = run.EnemyAggressionScale;
            toSpawn = run.EnemiesInWave;
            UpdateHud();
            yield return ShowBanner("WAVE " + run.Wave, 1.2f);

            while (toSpawn > 0 || alive.Count > 0)
            {
                if (PlayerScript.Current == null) yield break;
                if (toSpawn > 0 && alive.Count < run.MaxEnemiesAlive)
                {
                    SpawnEnemy();
                    yield return new WaitForSeconds(Random.Range(0.4f, 0.9f));
                }
                else
                {
                    yield return null;
                }
            }

            if (hint.gameObject.activeSelf) hint.gameObject.SetActive(false);
            bool kit = run.EndlessWaveCleared();
            if (PlayerScript.Current != null) PlayerScript.Current.RestoreHealth(10);
            yield return ShowBanner(kit ? "+1 KIT" : "WAVE CLEARED", 1f);
        }
    }

    private IEnumerator NightRoutine()
    {
        RunManager run = RunManager.Instance;
        yield return new WaitForSeconds(0.8f);

        if (run.Night == 1) ShowTutorialHint();

        for (round = 1; round <= run.FightRounds; round++)
        {
            toSpawn = run.EnemiesInRound(round);
            UpdateHud();
            yield return ShowBanner(round == run.FightRounds ? "FINAL ROUND" : "ROUND " + round, 1.3f);

            while (toSpawn > 0 || alive.Count > 0)
            {
                if (PlayerScript.Current == null) yield break;
                if (toSpawn > 0 && alive.Count < run.MaxEnemiesAlive)
                {
                    SpawnEnemy();
                    yield return new WaitForSeconds(Random.Range(0.5f, 1.1f));
                }
                else
                {
                    yield return null;
                }
            }

            if (round == 1 && hint.gameObject.activeSelf) hint.gameObject.SetActive(false);
            if (round < run.FightRounds) yield return new WaitForSeconds(1.2f);
        }

        finished = true;
        yield return ShowBanner("YOU SURVIVED NIGHT " + run.Night, 2f);
        run.EndFight(PlayerScript.Current != null ? PlayerScript.Current.life : 1, beaten);
    }

    private void SpawnEnemy()
    {
        RunManager run = RunManager.Instance;
        float px = PlayerScript.Current.transform.position.x;

        // alterna os lados; se o lado escolhido estiver fora da arena, usa o outro
        float x = px + nextSide * spawnDistance;
        if (x < arenaMinX || x > arenaMaxX) x = px - nextSide * spawnDistance;
        x = Mathf.Clamp(x, arenaMinX, arenaMaxX);
        nextSide = -nextSide;

        GameObject go = Instantiate(enemyPrefab, new Vector3(x, spawnY, 0f), Quaternion.identity);
        EnemyScript enemy = go.GetComponent<EnemyScript>();
        enemy.Setup(PickArchetype(run.Level), run.EnemyDamageScale);
        alive.Add(enemy);
        toSpawn--;
        UpdateHud();
    }

    // Nível 1: só Brawlers. Nível 2: entram os Heavy. Nível 3+: também os Swift.
    private EnemyScript.Archetype PickArchetype(int night)
    {
        float r = Random.value;
        if (night <= 1) return EnemyScript.Archetype.Brawler;
        if (night == 2) return r < 0.7f ? EnemyScript.Archetype.Brawler : EnemyScript.Archetype.Heavy;
        float heavy = Mathf.Min(0.35f, 0.2f + 0.03f * night);
        float swift = Mathf.Min(0.35f, 0.2f + 0.03f * night);
        if (r < heavy) return EnemyScript.Archetype.Heavy;
        if (r < heavy + swift) return EnemyScript.Archetype.Swift;
        return EnemyScript.Archetype.Brawler;
    }

    public void EnemyDied(EnemyScript enemy)
    {
        alive.Remove(enemy);
        beaten++;
        UpdateHud();
    }

    public void PlayerDied()
    {
        if (finished) return;
        finished = true;
        StopAllCoroutines();
        // K.O.: câmara lenta, a imagem perde a cor e a câmara aproxima-se
        PostFXDirector.KnockOut();
        CameraFollow.Punch(0.5f);
        RunManager.Instance.SlowMotion(0.3f, 1.4f);
        StartCoroutine(KnockOutRoutine());
    }

    private IEnumerator KnockOutRoutine()
    {
        yield return new WaitForSecondsRealtime(1.2f);
        yield return ShowBanner("K.O.", 2.2f);
        if (RunManager.Instance.IsEndlessFight) RunManager.Instance.EndlessKnockedOut(beaten);
        else RunManager.Instance.KnockedOut(beaten);
    }

    // Rótulos pré-calculados (evita criar strings novas em todos os frames)
    private static readonly string[] FuryLabels = BuildLabels(50);
    private static readonly string[] KitLabels = BuildLabels(RunManager.MaxFirstAid);
    private PlayerScript cachedPlayer;
    private RageScript cachedFury;

    private static string[] BuildLabels(int max)
    {
        string[] labels = new string[max + 1];
        for (int i = 0; i <= max; i++) labels[i] = i + "/" + max;
        return labels;
    }

    private void Update()
    {
        PlayerScript p = PlayerScript.Current;
        if (p == null || uppercutSlot == null) return;
        if (p != cachedPlayer)
        {
            cachedPlayer = p;
            cachedFury = p.GetComponent<RageScript>();
        }
        RageScript fury = cachedFury;
        bool denied = Time.time - p.LastDeniedAt < 0.25f;

        uppercutSlot.Set(1f - p.UppercutCooldown01, p.UppercutCooldown01 <= 0f ? "READY" : "", denied && p.UppercutCooldown01 > 0f);
        dodgeSlot.Set(1f - p.DodgeCooldown01, p.DodgeCooldown01 <= 0f ? "READY" : "", denied && p.DodgeCooldown01 > 0f);
        if (fury != null)
            furySlot.Set(fury.IsActive ? fury.rage / 100f : Mathf.Clamp01(fury.rage / 50f),
                fury.IsActive ? "ACTIVE!" : fury.CanActivate ? "READY" : FuryLabels[Mathf.Clamp(fury.rage, 0, 50)], false);
        int kits = Mathf.Clamp(RunManager.Instance.FirstAid, 0, RunManager.MaxFirstAid);
        kitsSlot.Set(kits / (float)RunManager.MaxFirstAid, KitLabels[kits], false);

        if (counterText.gameObject.activeSelf != p.CounterReady) counterText.gameObject.SetActive(p.CounterReady);
        if (p.CounterReady)
        {
            float s = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 20f);
            counterText.transform.localScale = new Vector3(s, s, 1f);
        }
    }

    // ---------------- HUD ----------------

    private CooldownSlot uppercutSlot, dodgeSlot, furySlot, kitsSlot;
    private TextMeshProUGUI counterText;

    // Caixa com barra de recarga: enche até ficar pronta, pisca a vermelho se tentares usar antes do tempo.
    private class CooldownSlot
    {
        private readonly RectTransform fill;
        private readonly Image fillImage;
        private readonly TextMeshProUGUI status;
        private readonly Image frame;
        private readonly Color readyColor;
        private readonly float width;

        public CooldownSlot(Transform parent, Vector2 pos, string title, string keys, Color readyColor)
        {
            this.readyColor = readyColor;
            width = 250f;
            frame = UIFactory.Panel("Slot", parent, new Vector2(0f, 0f), pos, new Vector2(width, 96), new Color(0.05f, 0.04f, 0.04f, 0.85f));
            fill = UIFactory.Rect("Fill", frame.transform, new Vector2(0f, 0f), Vector2.zero, new Vector2(width, 8));
            fillImage = fill.gameObject.AddComponent<Image>();
            UIFactory.Text(frame.transform, title, 30, new Vector2(0.5f, 1f), new Vector2(0, -8), new Vector2(width, 36));
            UIFactory.Text(frame.transform, keys, 20, new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(width, 24), color: UIFactory.Muted);
            status = UIFactory.Text(frame.transform, "", 24, new Vector2(0.5f, 0f), new Vector2(0, 12), new Vector2(width, 28));
        }

        private int lastFill = -1;
        private string lastLabel;
        private bool lastDenied;

        public void Set(float ready01, string label, bool denied)
        {
            ready01 = Mathf.Clamp01(ready01);
            // só mexe na UI quando algo mudou (evita reconstruir o Canvas a cada frame)
            int fillKey = Mathf.RoundToInt(ready01 * 250f);
            if (fillKey == lastFill && ReferenceEquals(label, lastLabel) && denied == lastDenied) return;
            lastFill = fillKey;
            lastLabel = label;
            lastDenied = denied;
            fill.sizeDelta = new Vector2(width * ready01, 8);
            bool ready = ready01 >= 0.999f;
            fillImage.color = ready ? readyColor : new Color(0.45f, 0.42f, 0.4f);
            UIFactory.SetText(status, label);
            status.color = ready ? readyColor : UIFactory.Muted;
            frame.color = denied ? new Color(0.6f, 0.08f, 0.08f, 0.9f) : new Color(0.05f, 0.04f, 0.04f, 0.85f);
        }
    }

    private void BuildHud()
    {
        Canvas canvas = UIFactory.CreateCanvas("FightHUD", 50);
        Transform t = canvas.transform;
        roundText = UIFactory.Text(t, "", 54, new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(900, 70), color: UIFactory.Paper);
        enemiesText = UIFactory.Text(t, "", 36, new Vector2(0.5f, 1f), new Vector2(0, -105), new Vector2(900, 50), color: UIFactory.Muted);
        banner = UIFactory.Text(t, "", 130, new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(1800, 200), color: UIFactory.Blood);
        banner.gameObject.SetActive(false);
        hint = UIFactory.Text(t, "", 32, new Vector2(0.5f, 0f), new Vector2(0, 150), new Vector2(1700, 150), color: UIFactory.Paper);
        hint.richText = true;
        hint.gameObject.SetActive(false);
        counterText = UIFactory.Text(t, "COUNTER READY!", 48, new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(1200, 70), color: UIFactory.Gold);
        counterText.gameObject.SetActive(false);

        float x = 40f, step = 266f, y = 30f;
        uppercutSlot = new CooldownSlot(t, new Vector2(x, y), "UPPERCUT", "K / Y-Triangle", new Color(1f, 0.55f, 0.2f));
        dodgeSlot = new CooldownSlot(t, new Vector2(x + step, y), "DODGE", "H / B-Circle", new Color(0.55f, 0.8f, 1f));
        furySlot = new CooldownSlot(t, new Vector2(x + step * 2, y), "FURY", "Space / LB-L1", new Color(1f, 0.25f, 0.2f));
        kitsSlot = new CooldownSlot(t, new Vector2(x + step * 3, y), "KITS", "U / A-Cross", new Color(0.45f, 1f, 0.55f));
    }

    private void UpdateHud()
    {
        RunManager run = RunManager.Instance;
        if (run.IsEndlessFight)
            UIFactory.SetText(roundText, "FIGHT ONLY  ·  WAVE " + run.Wave + "  ·  RECORD " + Mathf.Max(run.BestWave, run.Wave - 1));
        else
            UIFactory.SetText(roundText, "NIGHT " + run.Night + "  ·  ROUND " + Mathf.Min(round, run.FightRounds) + "/" + run.FightRounds);
        int left = toSpawn + alive.Count;
        UIFactory.SetText(enemiesText, left + UIFactory.Plural(left, " fighter left", " fighters left"));
    }

    private IEnumerator ShowBanner(string text, float duration)
    {
        UIFactory.SetText(banner, text);
        banner.gameObject.SetActive(true);
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float s = 1f + 0.25f * Mathf.Clamp01(1f - t * 6f); // "soco" de entrada
            banner.transform.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        banner.gameObject.SetActive(false);
    }
}
