using System;
using System.Collections;
using TMPro;
using UnityEngine;

// Um cliente sentado. Entregar = tecla de interagir (J / Enter / Espaço / X·A) ao pé dele com comida na mão.
public class SilhouetteOrder : MonoBehaviour
{
    [SerializeField] private GameObject orderBubble;
    [SerializeField] private TextMeshProUGUI orderText;
    [SerializeField] private SpriteRenderer orderIcon;
    [SerializeField] private Sprite toastSprite;
    [SerializeField] private Sprite coffeeSprite;
    [SerializeField] private Sprite bothSprite;
    [SerializeField] private float fadeDuration = 1.5f;
    [SerializeField] private OrderType currentOrder;

    private const float CorrectStressRelief = 10f;
    private const float WrongStress = 15f;

    private Action<bool> onLeave;
    private bool orderFulfilled = false;
    private bool isDestroying = false;
    private Collider2D myCollider;
    private SpriteRenderer sr;
    private StressBarManager stressManager;
    private PlayerInventory inventory;
    private OrderDisplay orderDisplay;
    private int seatIndex = -1;

    public enum OrderType { Toast, Coffee, Both }

    private void Awake()
    {
        myCollider = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
        stressManager = FindFirstObjectByType<StressBarManager>();
        inventory = FindFirstObjectByType<PlayerInventory>();
    }

    public void SetSeatIndex(int index) => seatIndex = index;
    public void SetOrderDisplay(OrderDisplay display) => orderDisplay = display;

    // onLeave(true) = foi servido; onLeave(false) = desistiu de esperar.
    public void Initialize(float duration, Action<bool> onLeave)
    {
        this.onLeave = onLeave;
        currentOrder = (OrderType)UnityEngine.Random.Range(0, 3);
        ShowOrder();
        if (orderDisplay != null && seatIndex >= 0) orderDisplay.ShowOrder(seatIndex, currentOrder);
        StartCoroutine(WaitSequence(duration));
    }

    private void ShowOrder()
    {
        if (orderBubble != null) orderBubble.SetActive(true);
        Sprite icon = currentOrder == OrderType.Toast ? toastSprite : currentOrder == OrderType.Coffee ? coffeeSprite : bothSprite;
        if (orderIcon != null && icon != null) orderIcon.sprite = icon;
        if (orderText != null)
            orderText.text = currentOrder == OrderType.Toast ? "Tosta" : currentOrder == OrderType.Coffee ? "Café" : "Tosta & Café";
    }

    private void Update()
    {
        if (!isDestroying && GameInput.Player.Interact.WasPressedThisFrame()) TryDeliver();
    }

    private void TryDeliver()
    {
        if (orderFulfilled || inventory == null || !inventory.HasAnyItem() || !PlayerIsInside()) return;

        bool correct = inventory.CheckAndDeliverOrder(currentOrder);
        if (stressManager != null)
        {
            if (correct) stressManager.DecreaseStress(CorrectStressRelief);
            else stressManager.IncreaseStress(WrongStress);
        }

        orderFulfilled = true;
        if (orderDisplay != null && seatIndex >= 0) orderDisplay.HideOrder(seatIndex);
        isDestroying = true;
        StopAllCoroutines();
        StartCoroutine(LeaveSequence(0.6f));
    }

    private bool PlayerIsInside()
    {
        foreach (Collider2D col in Physics2D.OverlapBoxAll(myCollider.bounds.center, myCollider.bounds.size, 0))
            if (col.CompareTag("Player")) return true;
        return false;
    }

    private IEnumerator WaitSequence(float visibleTime)
    {
        yield return Fade(0, 1);
        float remaining = visibleTime - fadeDuration;
        while (remaining > 0 && !isDestroying)
        {
            // o cliente fica impaciente: pisca quando está quase a ir-se embora
            if (sr != null && remaining < 4f) sr.color = new Color(1f, 1f, 1f, Mathf.Repeat(Time.time * 3f, 1f) > 0.3f ? 1f : 0.4f);
            remaining -= Time.deltaTime;
            yield return null;
        }
        if (sr != null) sr.color = Color.white;
        isDestroying = true;
        if (orderDisplay != null && seatIndex >= 0) orderDisplay.HideOrder(seatIndex);
        yield return LeaveSequence(0f);
    }

    private IEnumerator LeaveSequence(float delay)
    {
        yield return new WaitForSeconds(delay);
        yield return Fade(1, 0);
        onLeave?.Invoke(orderFulfilled);
        Destroy(gameObject);
    }

    private IEnumerator Fade(float from, float to)
    {
        if (sr == null) yield break;
        float elapsed = 0;
        Color color = sr.color;
        while (elapsed < fadeDuration)
        {
            sr.color = new Color(color.r, color.g, color.b, Mathf.Lerp(from, to, elapsed / fadeDuration));
            elapsed += Time.deltaTime;
            yield return null;
        }
        sr.color = new Color(color.r, color.g, color.b, to);
    }

    public bool IsOrderFulfilled() => orderFulfilled;
    public OrderType GetCurrentOrder() => currentOrder;
}
