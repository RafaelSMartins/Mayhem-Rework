using UnityEngine;
using UnityEngine.UI;

// Barra de stress do café. Já não termina o turno sozinha (quem manda no tempo é o CafeSceneManager);
// no fim do turno, parte do stress passa a Fúria inicial na luta.
public class StressBarManager : MonoBehaviour
{
    [SerializeField] private Image StressBar;
    [SerializeField] private Animator animator;
    [SerializeField] private float maxStress = 100f;
    [SerializeField] public float actualStress = 0f;

    private bool gameActive = false;
    private static readonly int StressParam = Animator.StringToHash("Stress");

    private void OnEnable()
    {
        CafeSceneManager.OnGameStarted += HandleGameStarted;
        CafeSceneManager.OnGameEnded += HandleGameEnded;
    }

    private void OnDisable()
    {
        CafeSceneManager.OnGameStarted -= HandleGameStarted;
        CafeSceneManager.OnGameEnded -= HandleGameEnded;
    }

    private void HandleGameStarted() => gameActive = true;
    private void HandleGameEnded() => gameActive = false;

    private void Start()
    {
        if (animator == null) animator = GetComponent<Animator>();
        Refresh();
    }

    public void IncreaseStress(float quantity) => DefineStress(actualStress + quantity);
    public void DecreaseStress(float quantity) => DefineStress(actualStress - quantity);

    public void DefineStress(float value)
    {
        if (!gameActive) return;
        actualStress = Mathf.Clamp(value, 0, maxStress);
        Refresh();
    }

    private void Refresh()
    {
        if (StressBar != null) StressBar.fillAmount = actualStress / maxStress;
        if (animator != null) animator.SetInteger(StressParam, Mathf.RoundToInt(StressPercentage() * 100f));
        // quanto mais stress, mais a imagem perde cor e a vignette fecha
        PostFXDirector.SetStress01(StressPercentage());
    }

    public float ActualStress() => actualStress;
    public float StressPercentage() => actualStress / maxStress;
}
