using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

// Cutscene em vídeo: passa à cena seguinte quando acaba, ou quando o jogador salta (interagir / pausa).
public class LoadingScript : MonoBehaviour
{
    [SerializeField] private string sceneName = "";
    private VideoPlayer videoPlayer;
    private bool leaving;

    private void Start()
    {
        videoPlayer = GetComponent<VideoPlayer>();
        videoPlayer.loopPointReached += EndReached;
    }

    private void Update()
    {
        if (GameInput.Player.Interact.WasPressedThisFrame() || GameInput.Player.Pause.WasPressedThisFrame())
            Leave();
    }

    private void EndReached(VideoPlayer vp) => Leave();

    private void Leave()
    {
        if (leaving) return;
        leaving = true;
        if (sceneName == RunManager.CafeScene || sceneName == "CoffeeShop") RunManager.Instance.StartShift();
        else SceneManager.LoadScene(sceneName);
    }
}
