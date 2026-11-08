using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Menu inicial. "Start" abre a escolha do modo de jogo:
//  - Run: café de dia, Fight Club à noite (o modo principal)
//  - Café Cozy: só o café, sem relógio nem castigos
//  - Só Luta: vagas infinitas cada vez mais duras
public class StartMenuController : MonoBehaviour
{
    private GameObject modePanel;
    private Button firstModeButton;
    private GameObject lastSelected;

    public void OnStartCick()
    {
        if (modePanel == null) BuildModePanel();
        lastSelected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        modePanel.SetActive(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(firstModeButton.gameObject);
    }

    public void OnExitClick()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
        Application.Quit();
    }

    private void Update()
    {
        if (modePanel != null && modePanel.activeSelf && GameInput.Player.Pause.WasPressedThisFrame()) CloseModePanel();
    }

    private void CloseModePanel()
    {
        modePanel.SetActive(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(lastSelected);
    }

    private void BuildModePanel()
    {
        RunManager run = RunManager.Instance;
        Canvas canvas = UIFactory.CreateCanvas("ModeSelect", 20);
        modePanel = canvas.gameObject;
        Transform root = canvas.transform;
        UIFactory.Stretch("Dim", root, new Color(0.04f, 0.03f, 0.03f, 0.92f));
        Vector2 top = new Vector2(0.5f, 1f);
        Vector2 center = new Vector2(0.5f, 0.5f);
        UIFactory.Text(root, "CHOOSE A MODE", 90, top, new Vector2(0, -70), new Vector2(1600, 110), color: UIFactory.Blood);

        firstModeButton = ModeCard(root, 0, "RUN", "Café by day, Fight Club by night. Manage your money and your health.",
            "Record: " + run.BestNights + " " + UIFactory.Plural(run.BestNights, "night", "nights"), new Color(0.45f, 0.07f, 0.07f), () =>
            {
                run.NewRun();
                run.LoadScene(RunManager.IntroScene);
            });
        Button cozy = ModeCard(root, 1, "COZY CAFÉ", "Just the café. No clock, no stress, no fines. Relax and serve.",
            "Best shift: $" + run.BestCozyMoney, new Color(0.35f, 0.22f, 0.1f), run.StartCozy);
        Button endless = ModeCard(root, 2, "FIGHT ONLY", "Endless waves of fighters, each one tougher. How long can you last?",
            "Record: " + run.BestWave + " " + UIFactory.Plural(run.BestWave, "wave", "waves"), new Color(0.25f, 0.05f, 0.05f), run.StartEndless);
        Button back = UIFactory.Button(root, "Back", center, new Vector2(0, -400), new Vector2(360, 80), CloseModePanel);
        UIFactory.ChainVertical(firstModeButton, cozy, endless, back);
        modePanel.SetActive(false);
    }

    private static Button ModeCard(Transform root, int index, string title, string description, string record, Color color, UnityEngine.Events.UnityAction onClick)
    {
        Vector2 center = new Vector2(0.5f, 0.5f);
        Vector2 size = new Vector2(1250, 170);
        Button b = UIFactory.Button(root, "", center, new Vector2(0, 230 - index * 195), size, onClick);
        ColorBlock c = b.colors;
        c.normalColor = color * 0.6f + new Color(0, 0, 0, 0.4f);
        c.highlightedColor = color;
        c.selectedColor = color * 1.6f;
        b.colors = c;

        TextMeshProUGUI label = b.GetComponentInChildren<TextMeshProUGUI>();
        label.gameObject.SetActive(false);
        Transform t = b.transform;
        UIFactory.Text(t, title, 60, new Vector2(0f, 1f), new Vector2(40, -18), new Vector2(700, 70), TextAlignmentOptions.Left, UIFactory.Paper);
        UIFactory.Text(t, record, 30, new Vector2(1f, 1f), new Vector2(-40, -32), new Vector2(500, 40), TextAlignmentOptions.Right, UIFactory.Gold);
        UIFactory.Text(t, description, 32, new Vector2(0f, 0f), new Vector2(40, 22), new Vector2(1170, 60), TextAlignmentOptions.Left, UIFactory.Muted);
        return b;
    }
}
