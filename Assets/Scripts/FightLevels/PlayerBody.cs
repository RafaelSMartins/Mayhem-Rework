using System.Collections;
using UnityEngine;

// Corpo que cai quando alguém é derrotado: dá um salto, faz o som de morte e desaparece aos poucos.
// (O que acontece depois de o jogador cair é tratado pelo FightDirector.)
public class PlayerBody : MonoBehaviour
{
    [SerializeField] private GruntScript hurtSound;
    [SerializeField] private bool PlayerBod;
    [SerializeField] private float fadeDelay = 1.5f;
    [SerializeField] private float fadeDuration = 1.5f;

    private void Start()
    {
        SpriteRenderer spr = GetComponent<SpriteRenderer>();
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        rb.AddForce(transform.up * 15, ForceMode2D.Impulse);
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.PlayOneShot(hurtSound.DeathSound);
        if (!PlayerBod) StartCoroutine(FadeBody(spr));
    }

    private IEnumerator FadeBody(SpriteRenderer spr)
    {
        yield return new WaitForSeconds(fadeDelay);
        Color c = spr.color;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            c.a = 1f - t / fadeDuration;
            spr.color = c;
            yield return null;
        }
        Destroy(gameObject);
    }
}
