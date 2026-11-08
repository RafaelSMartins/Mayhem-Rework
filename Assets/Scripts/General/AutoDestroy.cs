using UnityEngine;

// Destrói efeitos temporários (sangue, etc.) depois de um tempo.
public class AutoDestroy : MonoBehaviour
{
    [SerializeField] private float lifetime = 1.5f;

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }
}
