using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

// Jornal depois da luta (modo história): mostra uma manchete conforme a vida com que ficaste.
public class SceneHospital : MonoBehaviour
{
    [SerializeField] private GameObject good1;
    [SerializeField] private GameObject good2;
    [SerializeField] private GameObject bad1;
    [SerializeField] private GameObject bad2;
    [SerializeField] private string sceneName;
    [SerializeField] private string sceneSkipName;

    private void Start()
    {
        int life = RunManager.Instance.Health;
        if (life > 75) Instantiate(good1);
        else if (life > 50) Instantiate(good2);
        else if (life > 25) Instantiate(bad1);
        else Instantiate(bad2);
        StartCoroutine(LoadSceneAfterDelay(life));
    }

    private IEnumerator LoadSceneAfterDelay(int life)
    {
        yield return new WaitForSeconds(6f);
        SceneManager.LoadScene(life >= 90 ? sceneSkipName : sceneName);
    }
}
