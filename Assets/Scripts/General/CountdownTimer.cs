using UnityEngine;
using UnityEngine.UI;

// Relógio com dígitos em sprite. No café mostra o tempo do turno (CafeSceneManager);
// fora do café conta sozinho a partir de startMinutes:startSeconds.
public class CountdownTimer : MonoBehaviour
{
    public Image minuteDigit;
    public Image secondTens;
    public Image secondUnits;

    public Sprite[] digitSprites;
    public int startMinutes = 1;
    public int startSeconds = 30;

    private float remainingTime;

    private void Start()
    {
        remainingTime = startMinutes * 60 + startSeconds;
    }

    private void Update()
    {
        if (CafeSceneManager.Instance != null)
        {
            remainingTime = CafeSceneManager.Instance.RemainingTime;
        }
        else if (remainingTime > 0f)
        {
            remainingTime = Mathf.Max(0f, remainingTime - Time.deltaTime);
        }

        int totalSeconds = Mathf.CeilToInt(remainingTime);
        int minutes = Mathf.Clamp(totalSeconds / 60, 0, 9);
        int seconds = totalSeconds % 60;

        if (digitSprites.Length >= 10)
        {
            minuteDigit.sprite = digitSprites[minutes];
            secondTens.sprite = digitSprites[seconds / 10];
            secondUnits.sprite = digitSprites[seconds % 10];
        }
    }
}
