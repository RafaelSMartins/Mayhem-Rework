using TMPro;
using UnityEngine;

// Dinheiro ganho neste turno. Pedidos certos seguidos dão gorjeta; um erro quebra a sequência.
public class MoneyManager : MonoBehaviour
{
    public int currentMoney = 0;
    [SerializeField] private TextMeshProUGUI moneyText;

    public int OrdersServed { get; private set; }
    public int Mistakes { get; private set; }
    public int Tips { get; private set; }
    public int Streak { get; private set; }

    private void Start()
    {
        UpdateMoneyUI();
    }

    // Devolve a gorjeta ganha (0 se não houve).
    public int RegisterCorrectOrder()
    {
        Streak++;
        OrdersServed++;
        int tip = Streak >= 2 ? Mathf.Min(5 * (Streak - 1), 15) : 0;
        Tips += tip;
        currentMoney += RunManager.OrderReward + tip;
        UpdateMoneyUI();
        if (RunManager.Instance.IsCozy) RunManager.Instance.RecordCozyMoney(currentMoney);
        return tip;
    }

    public void RegisterWrongOrder()
    {
        Streak = 0;
        Mistakes++;
        SubtractMoney(RunManager.Instance.WrongOrderCost);
    }

    public void RegisterTimeout()
    {
        Streak = 0;
        Mistakes++;
        SubtractMoney(RunManager.Instance.TimeoutCost);
    }

    public void SubtractMoney(int amount)
    {
        currentMoney = Mathf.Max(0, currentMoney - amount);
        UpdateMoneyUI();
    }

    private void UpdateMoneyUI()
    {
        if (moneyText == null) return;
        moneyText.text = Streak >= 2 ? $"${currentMoney}  x{Streak}" : $"${currentMoney}";
    }

    public int GetCurrentMoney() => currentMoney;
}
