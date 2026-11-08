using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Atalho de desenvolvimento: F1 salta para a cena seguinte (só no editor / development build).
public class SceneSkipper : MonoBehaviour
{
    [SerializeField] private string nextSceneName;
    [SerializeField] private bool useSceneIndex = false;
    [SerializeField] private int nextSceneIndex = 0;

    private void Update()
    {
        if (!Debug.isDebugBuild) return;
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame) SkipScene();
    }

    public void SkipScene()
    {
        if (useSceneIndex)
        {
            if (nextSceneIndex < SceneManager.sceneCountInBuildSettings) SceneManager.LoadSceneAsync(nextSceneIndex);
        }
        else if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadSceneAsync(nextSceneName);
        }
    }

    public void SetNextScene(string sceneName)
    {
        nextSceneName = sceneName;
        useSceneIndex = false;
    }

    public void SetNextScene(int sceneIndex)
    {
        nextSceneIndex = sceneIndex;
        useSceneIndex = true;
    }
}
