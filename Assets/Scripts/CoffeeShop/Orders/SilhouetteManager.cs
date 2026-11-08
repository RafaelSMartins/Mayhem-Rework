using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Faz aparecer clientes nas cadeiras livres. A paciência e o número de clientes dependem da noite.
public class SilhouetteManager : MonoBehaviour
{
    [SerializeField] private GameObject silhouettePrefab;
    [SerializeField] private Transform[] seatPositions;
    [SerializeField] private float minStartDelay = 1f;
    [SerializeField] private float maxStartDelay = 5f;
    [SerializeField] private float minDelayAtMaxStress = 1f;
    [SerializeField] private float maxDelayAtMaxStress = 2f;
    [SerializeField] private StressBarManager stressManager;
    [SerializeField] private OrderDisplay orderDisplay;
    [SerializeField] private MoneyManager moneyManager;

    private const float TimeoutStress = 15f;
    private readonly List<int> occupiedSeats = new List<int>();
    private bool running;

    private int MaxSilhouettes => Mathf.Min(RunManager.Instance.MaxCustomers, seatPositions.Length);

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

    private void Start()
    {
        if (stressManager == null) stressManager = FindFirstObjectByType<StressBarManager>();
        if (orderDisplay == null) orderDisplay = FindFirstObjectByType<OrderDisplay>();
        if (moneyManager == null) moneyManager = FindFirstObjectByType<MoneyManager>();
    }

    private void HandleGameStarted()
    {
        running = true;
        for (int i = 0; i < MaxSilhouettes; i++) StartCoroutine(SpawnSilhouetteWithDelay(GetRandomDelay() + i * 1.5f));
    }

    private void HandleGameEnded() => running = false;

    private float GetRandomDelay()
    {
        float stressPercent = stressManager != null ? stressManager.StressPercentage() : 0f;
        float minDelay = Mathf.Lerp(minStartDelay, minDelayAtMaxStress, stressPercent);
        float maxDelay = Mathf.Lerp(maxStartDelay, maxDelayAtMaxStress, stressPercent);
        return Random.Range(minDelay, maxDelay);
    }

    private IEnumerator SpawnSilhouetteWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (running) SpawnSilhouette();
    }

    private void SpawnSilhouette()
    {
        if (occupiedSeats.Count >= MaxSilhouettes) return;
        int seatIndex = GetRandomFreeSeat();
        if (seatIndex == -1) return;
        occupiedSeats.Add(seatIndex);

        GameObject silhouette = Instantiate(silhouettePrefab, seatPositions[seatIndex].position, Quaternion.identity, transform);
        silhouette.SetActive(true);
        SilhouetteOrder s = silhouette.GetComponent<SilhouetteOrder>();
        s.SetSeatIndex(seatIndex);
        s.SetOrderDisplay(orderDisplay);
        s.Initialize(RunManager.Instance.CustomerPatience, served =>
        {
            occupiedSeats.Remove(seatIndex);
            if (!served && running)
            {
                // cliente foi-se embora sem ser servido: castigo aplicado UMA vez
                if (stressManager != null) stressManager.IncreaseStress(TimeoutStress);
                if (moneyManager != null) moneyManager.RegisterTimeout();
            }
            if (running) StartCoroutine(SpawnSilhouetteWithDelay(GetRandomDelay()));
        });
    }

    private int GetRandomFreeSeat()
    {
        List<int> freeSeats = new List<int>();
        for (int i = 0; i < seatPositions.Length; i++)
            if (!occupiedSeats.Contains(i)) freeSeats.Add(i);
        return freeSeats.Count == 0 ? -1 : freeSeats[Random.Range(0, freeSeats.Count)];
    }
}
