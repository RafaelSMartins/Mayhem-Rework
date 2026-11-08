using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public enum Type { Teleport, Linear, FeedbackLoop };
    [SerializeField] private Type type;
    [SerializeField] private Transform targetEntity;
    [SerializeField] private Vector3 offset;
    [SerializeField] private float maxSpeed;
    [SerializeField] private float lerpSpeed = 5f;

    private static CameraFollow current;
    private Vector3 basePosition;
    private float shakeTime;
    private float shakeDuration;
    private float shakeAmount;

    private Camera cam;
    private float baseSize;
    private float punch;

    private void Awake()
    {
        current = this;
        basePosition = transform.position;
        cam = GetComponent<Camera>();
        if (cam != null) baseSize = cam.orthographicSize;
    }

    // "Soco" na câmara: um zoom rápido que volta ao normal (golpe final, parry, K.O.).
    public static void Punch(float amount)
    {
        if (current == null) return;
        current.punch = Mathf.Max(current.punch, amount);
    }

    // Abana a câmara (golpes fortes, parries, K.O.s).
    public static void Shake(float amount, float duration)
    {
        if (current == null) return;
        float remaining = current.shakeDuration > 0f ? current.shakeAmount * current.shakeTime / current.shakeDuration : 0f;
        if (amount < remaining) return;
        current.shakeAmount = amount;
        current.shakeDuration = duration;
        current.shakeTime = duration;
    }

    // LateUpdate: segue o jogador depois de ele se mexer, sem tremer.
    private void LateUpdate()
    {
        if (targetEntity == null)
        {
            PlayerScript player = PlayerScript.Current;
            if (player != null) targetEntity = player.transform;
        }

        if (targetEntity != null)
        {
            Vector3 currentTarget = targetEntity.position + offset;
            currentTarget.z = basePosition.z;

            switch (type)
            {
                case Type.Teleport:
                    basePosition = currentTarget;
                    break;
                case Type.Linear:
                    basePosition = Vector3.Lerp(basePosition, currentTarget, lerpSpeed * Time.deltaTime);
                    break;
                case Type.FeedbackLoop:
                    Vector3 toTarget = currentTarget - basePosition;
                    basePosition = Vector3.Lerp(basePosition, basePosition + toTarget * maxSpeed, lerpSpeed * Time.deltaTime);
                    break;
            }
        }

        Vector3 shake = Vector3.zero;
        if (shakeTime > 0f)
        {
            shakeTime -= Time.unscaledDeltaTime;
            float strength = shakeAmount * Mathf.Clamp01(shakeTime / shakeDuration);
            shake = (Vector3)(Random.insideUnitCircle * strength);
        }
        transform.position = basePosition + shake;

        if (cam != null && cam.orthographic)
        {
            punch = Mathf.MoveTowards(punch, 0f, Time.unscaledDeltaTime * 2.5f);
            cam.orthographicSize = baseSize * (1f - punch * 0.12f);
        }
    }
}
