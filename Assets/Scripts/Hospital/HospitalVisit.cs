using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Hospital depois de cada noite no Fight Club (modo Run).
// O médico comenta o estado do William e o jogador decide:
//   1) tratamento (completo / leve / recusar e poupar)
//   2) amanhã: ir trabalhar para o café, ou faltar e voltar ao Fight Club
public class HospitalVisit : MonoBehaviour
{
    [SerializeField] private GameObject doctorPanel;
    [SerializeField] private GameObject playerPanel;
    [SerializeField] private TMP_Text doctorText;
    [SerializeField] private TMP_Text playerText;
    [SerializeField] private Image doctorImage;
    [SerializeField] private Image playerImage;
    [SerializeField] private float typewriterSpeed = 0.03f;
    [SerializeField] private float startDelay = 1.2f;

    private const string VisitsKey = "Mayhem_HospitalVisits";

    private RunManager run;
    private Transform choiceRoot;
    private TextMeshProUGUI statsText;
    private readonly List<Button> choices = new List<Button>();
    private int? picked;
    private int lastStatsKey = int.MinValue;
    private bool skipTyping;

    private void Start()
    {
        run = RunManager.Instance;
        doctorPanel.SetActive(false);
        playerPanel.SetActive(false);
        SetAlpha(doctorImage, 0f);
        SetAlpha(playerImage, 0f);
        BuildUi();
        switch (run.NextHubMode)
        {
            case RunManager.HubMode.EndlessOver: StartCoroutine(EndlessKnockOut()); break;
            case RunManager.HubMode.GameOver: StartCoroutine(RunKnockOut()); break;
            default: StartCoroutine(Visit()); break;
        }
    }

    private void Update()
    {
        if (GameInput.Player.Interact.WasPressedThisFrame()) skipTyping = true;

        // só reconstrói o texto quando algum valor mudou
        int key = run.Money * 7919 + run.Health * 131 + run.FirstAid * 17 + run.Night;
        if (key == lastStatsKey) return;
        lastStatsKey = key;

        string stats;
        if (run.NextHubMode == RunManager.HubMode.EndlessOver)
            stats = $"FIGHT ONLY   |   WAVE {run.Wave}   |   RECORD {run.BestWave}   |   {run.TotalEnemiesBeaten} FIGHTERS BEATEN";
        else if (run.NextHubMode == RunManager.HubMode.GameOver)
            stats = $"NIGHT {run.Night}   |   <color=#F2C240>${run.Money}</color>   |   RECORD {run.BestNights} {UIFactory.Plural(run.BestNights, "NIGHT", "NIGHTS")}";
        else
            stats = $"NIGHT {run.Night}   |   <color=#F2C240>${run.Money}</color>   |   HEALTH {run.Health}/{RunManager.MaxHealth}   |   KITS {run.FirstAid}/{RunManager.MaxFirstAid}";
        UIFactory.SetText(statsText, stats);
    }

    // ---------------- K.O. ----------------

    // Modo Fight Only: acordas no hospital depois de cair.
    private IEnumerator EndlessKnockOut()
    {
        yield return new WaitForSeconds(startDelay);
        int waves = run.WavesSurvived;
        int beaten = run.TotalEnemiesBeaten;

        yield return Doctor("Easy. Don't get up. They dragged you in off the basement floor.");
        yield return Player("How many did I take down?");
        yield return Doctor($"{beaten} {UIFactory.Plural(beaten, "guy", "guys")}. You lasted {waves} {UIFactory.Plural(waves, "wave", "waves")} before you dropped.");
        yield return Doctor(run.NewRecord
            ? "Your best night yet, apparently. Congratulations. I guess."
            : $"Your best is still {run.BestWave} {UIFactory.Plural(run.BestWave, "wave", "waves")}. You're slipping.");
        yield return Doctor("So. Are you going back down there?");

        yield return Choose(("Again. Right now.", true), ("I'm done for tonight.", true));
        if (picked == 0)
        {
            yield return Doctor("Of course you are.");
            run.StartEndless();
        }
        else
        {
            yield return Doctor("Smartest thing you've said all night.");
            run.GoToTitle();
        }
    }

    // Modo Run: a run acabou.
    private IEnumerator RunKnockOut()
    {
        yield return new WaitForSeconds(startDelay);
        int nights = run.NightsSurvived;

        yield return Doctor("You were out cold when they brought you in, William.");
        yield return Doctor($"{nights} {UIFactory.Plural(nights, "night", "nights")} in that basement. And {run.TotalEnemiesBeaten} {UIFactory.Plural(run.TotalEnemiesBeaten, "fighter", "fighters")} who look worse than you.");
        yield return Doctor(run.NightsSurvived >= run.BestNights && nights > 0
            ? "The nurses say it's a record. They also say you're an idiot."
            : $"Your best is still {run.BestNights} {UIFactory.Plural(run.BestNights, "night", "nights")}.");
        yield return Player("It's only after we've lost everything that we're free to do anything.");
        yield return Doctor("... Right. Do you want to start over?");

        yield return Choose(("Start over. New run.", true), ("Go home.", true));
        if (picked == 0)
        {
            run.NewRun();
            run.StartShift();
        }
        else
        {
            run.GoToTitle();
        }
    }

    // ---------------- Conversa ----------------

    private IEnumerator Visit()
    {
        int visits = PlayerPrefs.GetInt(VisitsKey, 0);
        PlayerPrefs.SetInt(VisitsKey, visits + 1);
        yield return new WaitForSeconds(startDelay);

        int hp = run.Health;
        yield return Doctor(visits == 0 ? "So you're William. Take a seat." :
                            run.Night > 3 ? "You again? You've got a reserved chair by now." : "You again, William?");
        yield return Player(run.EnemiesBeatenToday > 0
            ? $"Fell down the stairs. {run.EnemiesBeatenToday} {UIFactory.Plural(run.EnemiesBeatenToday, "time", "times")}."
            : "Slipped at the café.");
        yield return Doctor(hp >= 80 ? "Hm. You barely look beaten up. Almost." :
                            hp >= 50 ? "You're covered in bruises. That was no staircase." :
                            hp >= 25 ? "One more night like this and you're staying here." :
                                       "You're falling apart. You need treatment. Seriously.");

        // 1) Tratamento
        if (hp < RunManager.MaxHealth)
        {
            yield return Doctor("I can patch that up. It isn't free, though.");
            bool full = run.Money >= run.FullTreatmentCost;
            bool light = run.Money >= RunManager.LightTreatmentCost;
            yield return Choose(
                ($"Full treatment  (health 100)  -  ${run.FullTreatmentCost}", full),
                ($"Light treatment  (+{RunManager.LightTreatmentHeal} health)  -  ${RunManager.LightTreatmentCost}", light),
                ("No thanks. I need the money.", true));

            if (picked == 0 && run.BuyFullTreatment())
                yield return Doctor("There. Good as new. More or less.");
            else if (picked == 1 && run.BuyLightTreatment())
                yield return Doctor("A few stitches. Don't push it.");
            else
            {
                yield return Player("I can take it.");
                yield return Doctor(hp < 40 ? "It's your funeral." : "Suit yourself.");
            }
        }
        else
        {
            yield return Doctor("Nothing's broken. For now.");
        }

        // 2) Kits para a próxima noite
        if (run.FirstAid < RunManager.MaxFirstAid)
        {
            yield return Doctor("I can also sell you some supplies. For the next time you \"fall down the stairs\".");
            bool first = true;
            while (run.FirstAid < RunManager.MaxFirstAid)
            {
                bool canBuy = run.Money >= RunManager.FirstAidCost;
                yield return Choose(
                    ($"First aid kit  (+{RunManager.FirstAidHeal} health in a fight)  -  ${RunManager.FirstAidCost}   [{run.FirstAid}/{RunManager.MaxFirstAid}]", canBuy),
                    (first ? "No, I'm fine." : "That's enough.", true));
                if (picked != 0 || !run.BuyFirstAid()) break;
                first = false;
                yield return Doctor(run.FirstAid >= RunManager.MaxFirstAid ? "That's all I can give you." : "Anything else?");
            }
        }

        // 3) Amanhã
        yield return Doctor("And tomorrow? Going to work?");
        yield return Choose(
            ("I'll go to work. I need the paycheck.", true),
            ("I'm skipping it. Tonight I'm going back to the club.", true));

        if (picked == 0)
        {
            yield return Doctor("Good lad. Try not to bleed into the coffee.");
            run.NextDay();
        }
        else
        {
            yield return Player("Work can wait.");
            yield return Doctor("... I didn't hear any of that.");
            run.SkipWorkToFight();
        }
    }

    private IEnumerator Doctor(string line) => Say(doctorPanel, doctorImage, doctorText, playerPanel, playerImage, line);
    private IEnumerator Player(string line) => Say(playerPanel, playerImage, playerText, doctorPanel, doctorImage, line);

    private IEnumerator Say(GameObject panel, Image portrait, TMP_Text text, GameObject otherPanel, Image otherPortrait, string line)
    {
        otherPanel.SetActive(false);
        SetAlpha(otherPortrait, 0.35f);
        panel.SetActive(true);
        SetAlpha(portrait, 1f);

        skipTyping = false;
        text.text = "";
        foreach (char c in line)
        {
            if (skipTyping) break;
            text.text += c;
            yield return new WaitForSeconds(typewriterSpeed);
        }
        text.text = line;

        // espera por "continuar" (J / Enter / Espaço / X·A) ou uns segundos
        yield return null;
        skipTyping = false;
        float t = 0f;
        while (!skipTyping && t < 4f)
        {
            t += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator Choose(params (string label, bool available)[] options)
    {
        picked = null;
        foreach (Button b in choices) Destroy(b.gameObject);
        choices.Clear();

        for (int i = 0; i < options.Length; i++)
        {
            int index = i;
            Button b = UIFactory.Button(choiceRoot, options[i].label, new Vector2(0.5f, 0.5f),
                new Vector2(0, (options.Length - 1) * 55f - i * 110f), new Vector2(1100, 90), () => picked = index);
            b.interactable = options[i].available;
            b.GetComponentInChildren<TextMeshProUGUI>().fontSize = 34;
            choices.Add(b);
        }
        UIFactory.ChainVertical(choices.ToArray());
        choiceRoot.gameObject.SetActive(true);
        Button first = choices.Find(b => b.interactable);
        if (EventSystem.current != null && first != null) EventSystem.current.SetSelectedGameObject(first.gameObject);

        while (picked == null) yield return null;
        choiceRoot.gameObject.SetActive(false);
    }

    // ---------------- UI ----------------

    private void BuildUi()
    {
        Canvas canvas = UIFactory.CreateCanvas("HospitalChoices", 30);
        statsText = UIFactory.Text(canvas.transform, "", 36, new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(1600, 50));
        statsText.richText = true;
        choiceRoot = UIFactory.Rect("Choices", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(1200, 400));
        choiceRoot.gameObject.SetActive(false);
    }

    private static void SetAlpha(Image img, float a)
    {
        if (img == null) return;
        Color c = img.color;
        c.a = a;
        img.color = c;
    }
}
