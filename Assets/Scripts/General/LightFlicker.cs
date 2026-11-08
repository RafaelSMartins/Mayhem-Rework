using UnityEngine;
using UnityEngine.Rendering.Universal;

// Lâmpada a tremeluzir (cave do Fight Club): ruído suave na intensidade + falhas ocasionais.
[RequireComponent(typeof(Light2D))]
public class LightFlicker : MonoBehaviour
{
    [SerializeField] private float baseIntensity = 1.3f;
    [SerializeField] private float noiseAmount = 0.18f;
    [SerializeField] private float noiseSpeed = 6f;
    [SerializeField] private float dropoutChancePerSecond = 0.08f;

    private Light2D light2D;
    private float seed;
    private float dropoutUntil;

    private void Awake()
    {
        light2D = GetComponent<Light2D>();
        seed = Random.value * 100f;
    }

    private void Update()
    {
        if (Time.time > dropoutUntil && Random.value < dropoutChancePerSecond * Time.deltaTime)
            dropoutUntil = Time.time + Random.Range(0.05f, 0.18f);

        float noise = (Mathf.PerlinNoise(seed, Time.time * noiseSpeed) - 0.5f) * 2f * noiseAmount;
        float intensity = baseIntensity * (1f + noise);
        if (Time.time < dropoutUntil) intensity *= 0.25f;
        light2D.intensity = intensity;
    }
}
