using UnityEngine;
using System.Collections;
using TMPro;

public class FoodPreparation : MonoBehaviour
{
    [SerializeField] private float preparationTime = 2f;
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private GameObject coffeeProgressIndicator;
    [SerializeField] private GameObject toastProgressIndicator;
    [SerializeField] private AudioSource audioSource;

    [SerializeField] private CoffeeMachineShake coffeeMachineShake;
    [SerializeField] private GameObject coffeeReadyCheckMark;
    [SerializeField] private GameObject coffeeSnowflake;
    [SerializeField] private GameObject toastSnowflake;

    [SerializeField] private GameObject coffeeAreaSnowflake;
    [SerializeField] private GameObject toastAreaSnowflake;

    [SerializeField] private Animator marioAnimator;
    [SerializeField] private AudioClip[] audios;

    [SerializeField] private float timeToGetCold = 6f;
    [SerializeField] private StressBarManager stressManager;
    [SerializeField] private GameObject coffeeHelpBalloon;
    [SerializeField] private GameObject toastHelpBalloon;
    [SerializeField] private float helpBalloonDuration = 5f;

    private bool coffeeReady = false;
    private bool toastReady = false;
    private bool coffeeCold = false;
    private bool toastCold = false;
    private bool isPreparingCoffee = false;
    private bool isPreparingToast = false;
    private bool playerInCoffeeArea = false;
    private bool playerInToastArea = false;
    private bool gameActive = false;
    private bool helpBalloonsShown = false;

    private Coroutine coffeeColdTimer;
    private Coroutine toastColdTimer;

    private static readonly int EntregandoParam = Animator.StringToHash("Entregando");
    private static readonly int ProntoParam = Animator.StringToHash("Pronto");

    void OnEnable()
    {
        CafeSceneManager.OnGameStarted += HandleGameStarted;
        CafeSceneManager.OnGameEnded += HandleGameEnded;
    }

    void OnDisable()
    {
        CafeSceneManager.OnGameStarted -= HandleGameStarted;
        CafeSceneManager.OnGameEnded -= HandleGameEnded;
    }

    void HandleGameStarted()
    {
        gameActive = true;

        if (!helpBalloonsShown)
        {
            StartCoroutine(ShowHelpBalloonsAtStart());
        }
    }

    void HandleGameEnded()
    {
        gameActive = false;
    }

    private IEnumerator ShowHelpBalloonsAtStart()
    {
        helpBalloonsShown = true;

        if (coffeeHelpBalloon != null)
            coffeeHelpBalloon.SetActive(true);
        if (toastHelpBalloon != null)
            toastHelpBalloon.SetActive(true);

        yield return new WaitForSeconds(helpBalloonDuration);
        if (coffeeHelpBalloon != null && !playerInCoffeeArea)
            coffeeHelpBalloon.SetActive(false);
        if (toastHelpBalloon != null && !playerInToastArea)
            toastHelpBalloon.SetActive(false);
    }

    public void SetPlayerInCoffeeArea(bool value)
    {
        playerInCoffeeArea = value;

        if (helpBalloonsShown && coffeeHelpBalloon != null)
        {
            coffeeHelpBalloon.SetActive(value);
        }
    }

    public void SetPlayerInToastArea(bool value)
    {
        playerInToastArea = value;

        if (helpBalloonsShown && toastHelpBalloon != null)
        {
            toastHelpBalloon.SetActive(value);
        }
    }

    private void Start()
    {
        if (playerInventory == null)
        {
            playerInventory = FindFirstObjectByType<PlayerInventory>();
        }
        timeToGetCold = RunManager.Instance.FoodColdTime;
        if (stressManager == null)
        {
            stressManager = FindFirstObjectByType<StressBarManager>();
        }

        if (coffeeProgressIndicator != null)
            coffeeProgressIndicator.SetActive(false);
        if (toastProgressIndicator != null)
            toastProgressIndicator.SetActive(false);

        if (coffeeReadyCheckMark != null)
            coffeeReadyCheckMark.SetActive(false);
        if (coffeeSnowflake != null)
            coffeeSnowflake.SetActive(false);
        if (toastSnowflake != null)
            toastSnowflake.SetActive(false);
        if (coffeeAreaSnowflake != null)
            coffeeAreaSnowflake.SetActive(false);
        if (toastAreaSnowflake != null)
            toastAreaSnowflake.SetActive(false);

        if (coffeeHelpBalloon != null)
            coffeeHelpBalloon.SetActive(false);
        if (toastHelpBalloon != null)
            toastHelpBalloon.SetActive(false);

        if (marioAnimator != null)
        {
            marioAnimator.SetBool(EntregandoParam, false);
            marioAnimator.SetBool(ProntoParam, false);
        }
    }

    private void Update()
    {
        if (!gameActive) return;

        if (GameInput.Player.Interact.WasPressedThisFrame())
        {
            if (playerInCoffeeArea)
            {
                if (!isPreparingCoffee && !coffeeReady)
                {
                    StartCoroutine(PrepareCoffee());
                }
                else if (coffeeReady)
                {
                    TryCollectCoffee();
                }
            }
            if (playerInToastArea)
            {
                if (!isPreparingToast && !toastReady)
                {
                    StartCoroutine(PrepareToast());
                }
                else if (toastReady)
                {
                    TryCollectToast();
                }
            }
        }
    }

    private IEnumerator PrepareCoffee()
    {
        audioSource.PlayOneShot(audios[0]);

        isPreparingCoffee = true;

        if (coffeeProgressIndicator != null)
            coffeeProgressIndicator.SetActive(true);

        if (coffeeMachineShake != null)
            coffeeMachineShake.StartShaking();

        yield return new WaitForSeconds(preparationTime);

        if (coffeeMachineShake != null)
            coffeeMachineShake.StopShaking();
        if (coffeeReadyCheckMark != null)
            coffeeReadyCheckMark.SetActive(true);

        coffeeReady = true;
        isPreparingCoffee = false;

        coffeeColdTimer = StartCoroutine(MakeCoffeeCold());
    }

    private IEnumerator PrepareToast()
    {
        audioSource.PlayOneShot(audios[1]);

        isPreparingToast = true;

        if (toastProgressIndicator != null)
            toastProgressIndicator.SetActive(true);

        yield return new WaitForSeconds(preparationTime);

        if (marioAnimator != null)
        {
            marioAnimator.SetBool(EntregandoParam, true);

            // espera um frame para o Animator já estar na animação nova antes de ler a duração
            yield return null;
            AnimatorStateInfo stateInfo = marioAnimator.IsInTransition(0)
                ? marioAnimator.GetNextAnimatorStateInfo(0)
                : marioAnimator.GetCurrentAnimatorStateInfo(0);
            float animationLength = stateInfo.length;

            yield return new WaitForSeconds(animationLength);

            marioAnimator.SetBool(EntregandoParam, false);
            marioAnimator.SetBool(ProntoParam, true);
        }

        toastReady = true;
        isPreparingToast = false;

        toastColdTimer = StartCoroutine(MakeToastCold());
    }

    private IEnumerator MakeCoffeeCold()
    {
        yield return new WaitForSeconds(timeToGetCold);

        if (coffeeReady && !coffeeCold)
        {
            coffeeCold = true;

            if (coffeeReadyCheckMark != null)
                coffeeReadyCheckMark.SetActive(false);
            if (coffeeSnowflake != null)
                coffeeSnowflake.SetActive(true);
            if (coffeeAreaSnowflake != null)
                coffeeAreaSnowflake.SetActive(true);
        }
    }

    private IEnumerator MakeToastCold()
    {
        yield return new WaitForSeconds(timeToGetCold);

        if (toastReady && !toastCold)
        {
            toastCold = true;

            if (toastSnowflake != null)
                toastSnowflake.SetActive(true);
            if (toastAreaSnowflake != null)
                toastAreaSnowflake.SetActive(true);
        }
    }

    private void TryCollectCoffee()
    {
        bool hasCoffee = playerInventory.HasCoffee();
        bool hasColdCoffee = playerInventory.HasColdCoffee();

        if (!hasCoffee && !hasColdCoffee)
        {
            if (coffeeCold)
            {
                playerInventory.ToggleColdCoffee(true);
            }
            else
            {
                playerInventory.ToggleCoffee(true);
            }

            if (coffeeColdTimer != null)
            {
                StopCoroutine(coffeeColdTimer);
                coffeeColdTimer = null;
            }

            coffeeReady = false;
            coffeeCold = false;

            if (coffeeProgressIndicator != null)
                coffeeProgressIndicator.SetActive(false);
            if (coffeeReadyCheckMark != null)
                coffeeReadyCheckMark.SetActive(false);
            if (coffeeSnowflake != null)
                coffeeSnowflake.SetActive(false);
            if (coffeeAreaSnowflake != null)
                coffeeAreaSnowflake.SetActive(false);
        }
    }

    private void TryCollectToast()
    {
        bool hasToast = playerInventory.HasToast();
        bool hasColdToast = playerInventory.HasColdToast();

        if (!hasToast && !hasColdToast)
        {
            if (toastCold)
            {
                playerInventory.ToggleColdToast(true);
            }
            else
            {
                playerInventory.ToggleToast(true);
            }

            if (toastColdTimer != null)
            {
                StopCoroutine(toastColdTimer);
                toastColdTimer = null;
            }

            toastReady = false;
            toastCold = false;

            if (toastProgressIndicator != null)
                toastProgressIndicator.SetActive(false);
            if (toastSnowflake != null)
                toastSnowflake.SetActive(false);
            if (toastAreaSnowflake != null)
                toastAreaSnowflake.SetActive(false);

            if (marioAnimator != null)
            {
                marioAnimator.SetBool(ProntoParam, false);
            }
        }
    }
}