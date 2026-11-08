using UnityEngine;

// O que o William tem nas mãos no café. Q/E (LT/RT) larga a tosta/café.
public class PlayerInventory : MonoBehaviour
{
    [SerializeField] private bool hasCoffee = false;
    [SerializeField] private bool hasToast = false;
    [SerializeField] private bool hasColdCoffee = false;
    [SerializeField] private bool hasColdToast = false;

    [SerializeField] private GameObject coffeeIndicator;
    [SerializeField] private GameObject toastIndicator;
    [SerializeField] private GameObject coldCoffeeIndicator;
    [SerializeField] private GameObject coldToastIndicator;
    [SerializeField] private MoneyManager moneyManager;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] audios;

    private void Start()
    {
        UpdateUI();
        if (moneyManager == null) moneyManager = FindFirstObjectByType<MoneyManager>();
    }

    private void Update()
    {
        if (GameInput.Player.DropToast.WasPressedThisFrame())
        {
            hasToast = false;
            hasColdToast = false;
            UpdateUI();
        }
        if (GameInput.Player.DropCoffee.WasPressedThisFrame())
        {
            hasCoffee = false;
            hasColdCoffee = false;
            UpdateUI();
        }
    }

    public bool HasCoffee() => hasCoffee;
    public bool HasToast() => hasToast;
    public bool HasColdCoffee() => hasColdCoffee;
    public bool HasColdToast() => hasColdToast;
    public bool HasAnyItem() => hasCoffee || hasToast || hasColdCoffee || hasColdToast;

    public void ToggleCoffee(bool value) { hasCoffee = value; UpdateUI(); }
    public void ToggleToast(bool value) { hasToast = value; UpdateUI(); }
    public void ToggleColdCoffee(bool value) { hasColdCoffee = value; UpdateUI(); }
    public void ToggleColdToast(bool value) { hasColdToast = value; UpdateUI(); }

    private void UpdateUI()
    {
        if (coffeeIndicator != null) coffeeIndicator.SetActive(hasCoffee);
        if (toastIndicator != null) toastIndicator.SetActive(hasToast);
        if (coldCoffeeIndicator != null) coldCoffeeIndicator.SetActive(hasColdCoffee);
        if (coldToastIndicator != null) coldToastIndicator.SetActive(hasColdToast);
    }

    // Entrega ao cliente. Trata do dinheiro e do som; o stress é tratado pelo SilhouetteOrder (uma só vez).
    public bool CheckAndDeliverOrder(SilhouetteOrder.OrderType orderType)
    {
        bool wantsCoffee = orderType != SilhouetteOrder.OrderType.Toast;
        bool wantsToast = orderType != SilhouetteOrder.OrderType.Coffee;

        bool correct = (!wantsCoffee || hasCoffee) && (!wantsToast || hasToast);

        // entrega sempre o que tens daquilo que foi pedido (quente ou frio)
        if (wantsCoffee) { hasCoffee = false; hasColdCoffee = false; }
        if (wantsToast) { hasToast = false; hasColdToast = false; }
        UpdateUI();

        if (correct)
        {
            audioSource.PlayOneShot(audios[0]);
            moneyManager?.RegisterCorrectOrder();
        }
        else
        {
            audioSource.PlayOneShot(audios[1]);
            moneyManager?.RegisterWrongOrder();
        }
        return correct;
    }
}
