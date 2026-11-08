using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Post-processing que reage ao jogo, por cima do Global Volume de cada cena.
// Cada efeito é um "canal" 0..1 que sobe com um evento e desce sozinho;
// os valores finais são somados aos valores base do perfil da cena (não os substituem).
//   Hit       - levar um soco: aberração cromática + vignette vermelha
//   Flash     - parry / contra-ataque: exposição + distorção de lente (um "soco" na câmara)
//   Fury      - fúria ativa: saturação, tom vermelho, bloom
//   LowHealth - pouca vida: vignette vermelha a pulsar como um coração, cor a desaparecer
//   Stress    - stress no café: a imagem perde cor e a vignette fecha
//   KO        - K.O.: passa a preto e branco
public class PostFXDirector : MonoBehaviour
{
    public static PostFXDirector Instance { get; private set; }

    private static readonly Color DeepRed = new Color(0.45f, 0f, 0f);
    private static readonly Color FuryFilter = new Color(1f, 0.8f, 0.8f);

    private Volume volume;
    private Vignette vignette;
    private ChromaticAberration chromatic;
    private ColorAdjustments color;
    private LensDistortion lens;
    private Bloom bloom;

    // valores base do perfil da cena
    private float baseVignette, baseChromatic, baseSaturation, baseExposure, baseBloom, baseLens;
    private Color baseVignetteColor = Color.black, baseFilter = Color.white;

    // canais
    private float hit, flash, fury, lowHealth, stress, ko;
    private bool furyOn, koOn;
    private float lowHealthTarget, stressTarget;
    private bool basementLook;

    public static void Hit(float amount = 1f) { if (Instance) Instance.hit = Mathf.Max(Instance.hit, amount); }
    public static void Flash(float amount = 1f) { if (Instance) Instance.flash = Mathf.Max(Instance.flash, amount); }
    public static void SetFury(bool on) { if (Instance) Instance.furyOn = on; }
    public static void SetHealth01(float health01) { if (Instance) Instance.lowHealthTarget = health01 < 0.35f ? 1f - health01 / 0.35f : 0f; }
    public static void SetStress01(float stress01) { if (Instance) Instance.stressTarget = Mathf.Clamp01(stress01); }
    public static void KnockOut() { if (Instance) Instance.koOn = true; }

    private void Awake()
    {
        Instance = this;
        volume = gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        vignette = profile.Add<Vignette>();
        chromatic = profile.Add<ChromaticAberration>();
        color = profile.Add<ColorAdjustments>();
        lens = profile.Add<LensDistortion>();
        bloom = profile.Add<Bloom>();
        volume.sharedProfile = profile;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ReadSceneBase();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        hit = flash = fury = lowHealth = stress = ko = 0f;
        furyOn = koOn = false;
        lowHealthTarget = stressTarget = 0f;
        ReadSceneBase();
    }

    // Lê os valores do Global Volume da cena para os efeitos partirem deles.
    private void ReadSceneBase()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        basementLook = sceneName == "Level1" || sceneName == "Level2" || sceneName == "FightClub";
        baseVignette = baseChromatic = baseSaturation = baseExposure = baseBloom = baseLens = 0f;
        baseVignetteColor = Color.black;
        baseFilter = Color.white;

        Volume best = null;
        foreach (Volume v in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            if (v != volume && v.isGlobal && v.sharedProfile != null && (best == null || v.priority > best.priority)) best = v;
        if (best == null) return;

        VolumeProfile p = best.sharedProfile;
        if (p.TryGet(out Vignette v1))
        {
            if (v1.intensity.overrideState) baseVignette = v1.intensity.value;
            if (v1.color.overrideState) baseVignetteColor = v1.color.value;
        }
        if (p.TryGet(out ChromaticAberration c1) && c1.intensity.overrideState) baseChromatic = c1.intensity.value;
        if (p.TryGet(out ColorAdjustments a1))
        {
            if (a1.saturation.overrideState) baseSaturation = a1.saturation.value;
            if (a1.postExposure.overrideState) baseExposure = a1.postExposure.value;
            if (a1.colorFilter.overrideState) baseFilter = a1.colorFilter.value;
        }
        if (p.TryGet(out Bloom b1) && b1.intensity.overrideState) baseBloom = b1.intensity.value;
        if (p.TryGet(out LensDistortion l1) && l1.intensity.overrideState) baseLens = l1.intensity.value;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        hit = Mathf.MoveTowards(hit, 0f, dt / 0.35f);
        flash = Mathf.MoveTowards(flash, 0f, dt / 0.18f);
        fury = Mathf.MoveTowards(fury, furyOn ? 1f : 0f, dt * 4f);
        lowHealth = Mathf.MoveTowards(lowHealth, lowHealthTarget, dt * 2f);
        stress = Mathf.MoveTowards(stress, stressTarget, dt * 0.8f);
        ko = Mathf.MoveTowards(ko, koOn ? 1f : 0f, dt * 1.5f);

        // batimento cardíaco: mais rápido quanto menos vida
        float beat = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * (1.1f + lowHealth)), 3f);

        if (basementLook)
        {
            // Keep silhouettes readable when damage, low health and fury overlap.
            // Fury is a small warm shift; red belongs to injury feedback.
            float edge = hit * 0.09f + lowHealth * (0.05f + 0.04f * beat) + ko * 0.12f;
            Set(vignette.intensity, Mathf.Clamp(baseVignette + edge, 0f, 0.38f), edge);
            float injury = Mathf.Max(hit * 0.55f, lowHealth * 0.3f);
            Set(vignette.color, Color.Lerp(baseVignetteColor, DeepRed, injury), injury);

            float fringe = hit * 0.09f + flash * 0.05f;
            Set(chromatic.intensity, Mathf.Min(baseChromatic + fringe, 0.16f), fringe);
            float desaturation = -14f * lowHealth + 3f * fury - 100f * ko;
            Set(color.saturation, Mathf.Clamp(baseSaturation + desaturation, -100f, 100f), Mathf.Abs(desaturation));
            Set(color.postExposure, baseExposure + flash * 0.14f, flash);
            Set(color.colorFilter, Color.Lerp(baseFilter, new Color(1f, 0.96f, 0.88f), fury * 0.3f), fury);
            Set(lens.intensity, baseLens - 0.035f * flash, flash);
            Set(bloom.intensity, baseBloom + fury * 0.06f + flash * 0.14f, fury + flash);
            return;
        }

        float vig = hit * 0.22f + lowHealth * (0.16f + 0.14f * beat) + stress * 0.25f + fury * 0.1f + ko * 0.2f;
        Set(vignette.intensity, Mathf.Clamp(baseVignette + vig, 0f, 0.65f), vig);
        float red = Mathf.Max(hit, lowHealth * 0.9f, fury * 0.6f);
        Set(vignette.color, Color.Lerp(baseVignetteColor, DeepRed, red), red);

        float chrom = hit * 0.55f + fury * 0.25f + stress * 0.15f + flash * 0.4f;
        Set(chromatic.intensity, Mathf.Clamp01(baseChromatic + chrom), chrom);

        float sat = -45f * stress - 40f * lowHealth + 25f * fury - 100f * ko;
        Set(color.saturation, Mathf.Clamp(baseSaturation + sat, -100f, 100f), Mathf.Abs(sat));
        Set(color.postExposure, baseExposure + flash * 0.9f, flash);
        Set(color.colorFilter, Color.Lerp(baseFilter, FuryFilter, fury * 0.7f), fury);

        Set(lens.intensity, -0.28f * flash, flash);
        Set(bloom.intensity, baseBloom + fury * 0.8f + flash * 1.5f, fury + flash);
    }

    // Só sobrepõe o parâmetro enquanto o efeito está ativo; de resto fica o valor da cena.
    private static void Set<T>(VolumeParameter<T> p, T value, float amount)
    {
        bool on = amount > 0.001f;
        p.overrideState = on;
        if (on) p.value = value;
    }
}
