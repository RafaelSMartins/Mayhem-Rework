using UnityEngine;
using UnityEngine.UI;

// Fúria: enche com golpes certeiros, parries e K.O.s (e começa com parte do stress do café).
// Com metade ou mais, Espaço/L1 gasta a barra numa explosão curta: mais dano, mais velocidade,
// e os golpes partem a defesa. Não volta a encher enquanto está ativa.
public class RageScript : MonoBehaviour
{
    [SerializeField] private int _rage;
    [SerializeField] private Image ragebar;
    [SerializeField] private int activationThreshold = 50;
    [SerializeField] private float drainPerSecond = 14f;

    private const int MAXRAGE = 100;
    private float drainAccumulator;

    public bool IsActive { get; private set; }
    public float DamageMultiplier => IsActive ? 1.5f : 1f;
    public float SpeedMultiplier => IsActive ? 1.2f : 1f;
    public bool CanActivate => !IsActive && _rage >= activationThreshold;

    public int rage
    {
        get { return _rage; }
        set
        {
            _rage = Mathf.Clamp(value, 0, MAXRAGE);
            UpdateBar();
        }
    }

    private void Start()
    {
        rage = RunManager.Instance.StartingFury;
    }

    public void AddFury(int amount)
    {
        if (IsActive) return;
        rage += amount;
    }

    public void TryActivate()
    {
        if (!CanActivate) return;
        IsActive = true;
        drainAccumulator = 0f;
        CameraFollow.Shake(0.3f, 0.3f);
        RunManager.Instance.HitStop(0.08f);
    }

    private void Update()
    {
        if (IsActive)
        {
            drainAccumulator += drainPerSecond * Time.deltaTime;
            int drain = Mathf.FloorToInt(drainAccumulator);
            if (drain > 0)
            {
                drainAccumulator -= drain;
                rage -= drain;
            }
            if (_rage <= 0) IsActive = false;
        }
        PostFXDirector.SetFury(IsActive);

        if (ragebar != null)
        {
            // pisca quando está pronta a ser usada
            bool pulse = IsActive || CanActivate;
            float k = pulse ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * (IsActive ? 20f : 6f)) : 1f;
            ragebar.color = new Color(1f, k, k);
        }
    }

    private void UpdateBar()
    {
        if (ragebar != null) ragebar.fillAmount = Mathf.Clamp01((float)_rage / MAXRAGE);
    }
}
