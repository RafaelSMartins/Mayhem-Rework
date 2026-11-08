using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Ecrã entre fases da run:
//  - Preparação (depois do café): gastar dinheiro em vida / kits antes da luta, ou guardar.
//  - Recuperação (depois da luta): resumo da noite, pagar tratamento ou poupar.
//  - Game Over (K.O.): noites sobrevividas e recorde.
public class NightHubUI : MonoBehaviour
{
    private RunManager run;
    private Transform root;
    private TextMeshProUGUI statsText;
    private TextMeshProUGUI feedbackText;
    private readonly List<(Button button, System.Func<string> label, System.Func<bool> available)> shopButtons =
        new List<(Button, System.Func<string>, System.Func<bool>)>();

    private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

    private void Start()
    {
        run = RunManager.Instance;
        Canvas canvas = UIFactory.CreateCanvas("HubCanvas", 10);
        root = canvas.transform;
        UIFactory.Stretch("Background", root, new Color(0.06f, 0.04f, 0.04f));
        UIFactory.Panel("Stripe", root, new Vector2(0.5f, 1f), new Vector2(0, -200), new Vector2(1920, 6), UIFactory.Blood);

        switch (run.NextHubMode)
        {
            case RunManager.HubMode.Prep: BuildPrep(); break;
            case RunManager.HubMode.Recovery: BuildRecovery(); break;
            case RunManager.HubMode.GameOver: BuildGameOver(); break;
            case RunManager.HubMode.EndlessOver: BuildEndlessOver(); break;
        }
        RefreshButtons();
    }

    // ---------------- Modos ----------------

    private void BuildPrep()
    {
        Title("NIGHT " + run.Night, "Before the fight");
        if (run.SkippedWork)
            Report("You skipped your shift at the café. No pay today.\n" +
                   $"But you go in angry: <color=#C0392B>{run.StartingFury} starting Fury</color>.");
        else Report(
            $"Café shift: {run.ShiftOrders} {UIFactory.Plural(run.ShiftOrders, "order", "orders")} · " +
            $"{run.ShiftMistakes} {UIFactory.Plural(run.ShiftMistakes, "mistake", "mistakes")} · tips ${run.ShiftTips}\n" +
            $"You earned <color=#F2C240>${run.ShiftEarned}</color>. Shift stress becomes <color=#C0392B>{run.StartingFury} starting Fury</color>.");

        Button patch = ShopButton(0,
            () => $"Patch up  +{RunManager.PatchUpHeal} health   -   ${RunManager.PatchUpCost}",
            () => run.Health < RunManager.MaxHealth && run.Money >= RunManager.PatchUpCost,
            () => Feedback(run.BuyPatchUp() ? "Bandages tightened." : null));
        Button kit = ShopButton(1,
            () => $"First aid kit ({run.FirstAid}/{RunManager.MaxFirstAid})   -   ${RunManager.FirstAidCost}",
            () => run.FirstAid < RunManager.MaxFirstAid && run.Money >= RunManager.FirstAidCost,
            () => Feedback(run.BuyFirstAid() ? $"Kit packed. Use it with U / A-Cross during the fight (+{RunManager.FirstAidHeal})." : null));
        Button fight = UIFactory.Button(root, "GO TO FIGHT CLUB  >", Center, new Vector2(0, -375), new Vector2(760, 100), run.StartFight);
        ColorFight(fight);
        Chain(fight, patch, kit, fight);
    }

    private void BuildRecovery()
    {
        Title("YOU SURVIVED NIGHT " + run.Night, run.NewRecord ? "NEW RECORD!" : "Record: " + run.BestNights + " " + UIFactory.Plural(run.BestNights, "night", "nights"));
        Report(
            $"You beat {run.EnemiesBeatenToday} {UIFactory.Plural(run.EnemiesBeatenToday, "fighter", "fighters")} · lost {run.HealthLostToday} health · spent ${run.SpentToday}\n" +
            $"A night's sleep restored +{RunManager.OvernightRecovery} health. Another café shift tomorrow.");

        Button light = ShopButton(0,
            () => $"Light treatment  +{RunManager.LightTreatmentHeal} health   -   ${RunManager.LightTreatmentCost}",
            () => run.Health < RunManager.MaxHealth && run.Money >= RunManager.LightTreatmentCost,
            () => Feedback(run.BuyLightTreatment() ? "The doctor didn't even ask." : null));
        Button full = ShopButton(1,
            () => run.Health >= RunManager.MaxHealth ? "Full treatment   -   you're in one piece" : $"Full treatment (health 100)   -   ${run.FullTreatmentCost}",
            () => run.Health < RunManager.MaxHealth && run.Money >= run.FullTreatmentCost,
            () => Feedback(run.BuyFullTreatment() ? "Good as new. More or less." : null));
        Button next = UIFactory.Button(root, "NEXT DAY  >", Center, new Vector2(0, -375), new Vector2(760, 100), run.NextDay);
        ColorFight(next);
        Chain(next, light, full, next);
    }

    private void BuildGameOver()
    {
        int nights = run.NightsSurvived;
        Title("K.O.", nights + " " + UIFactory.Plural(nights, "night survived", "nights survived") + "   ·   Record: " + run.BestNights);
        Report($"You went down on night {run.Night} with ${run.Money} in your pocket and {run.TotalEnemiesBeaten} {UIFactory.Plural(run.TotalEnemiesBeaten, "fighter", "fighters")} beaten.\n" +
               "\"It's only after we've lost everything that we're free to do anything.\"");
        statsText.gameObject.SetActive(false);

        Button again = UIFactory.Button(root, "NEW RUN", Center, new Vector2(0, -150), new Vector2(620, 100), () =>
        {
            run.NewRun();
            run.StartShift();
        });
        ColorFight(again);
        Button menu = UIFactory.Button(root, "Main menu", Center, new Vector2(0, -270), new Vector2(620, 90), run.GoToTitle);
        Chain(again, again, menu);
    }

    private void BuildEndlessOver()
    {
        int waves = run.WavesSurvived;
        Title("K.O. ON WAVE " + run.Wave, run.NewRecord
            ? "NEW RECORD: " + waves + " " + UIFactory.Plural(waves, "wave", "waves")
            : "Record: " + run.BestWave + " " + UIFactory.Plural(run.BestWave, "wave", "waves"));
        Report($"You lasted {waves} {UIFactory.Plural(waves, "wave", "waves")} and beat {run.TotalEnemiesBeaten} {UIFactory.Plural(run.TotalEnemiesBeaten, "fighter", "fighters")}.\n" +
               "Every wave brings more of them, hitting harder and more often.");
        statsText.gameObject.SetActive(false);

        Button again = UIFactory.Button(root, "TRY AGAIN", Center, new Vector2(0, -150), new Vector2(620, 100), run.StartEndless);
        ColorFight(again);
        Button menu = UIFactory.Button(root, "Main menu", Center, new Vector2(0, -270), new Vector2(620, 90), run.GoToTitle);
        Chain(again, again, menu);
    }

    // ---------------- Blocos de UI ----------------

    private void Title(string title, string subtitle)
    {
        UIFactory.Text(root, title, 120, new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(1800, 140), color: UIFactory.Blood);
        UIFactory.Text(root, subtitle, 44, new Vector2(0.5f, 1f), new Vector2(0, -215), new Vector2(1800, 60), color: UIFactory.Gold);
        statsText = UIFactory.Text(root, "", 46, new Vector2(0.5f, 1f), new Vector2(0, -300), new Vector2(1800, 60));
        feedbackText = UIFactory.Text(root, "", 34, Center, new Vector2(0, -268), new Vector2(1600, 50), color: UIFactory.Muted);
    }

    private void Report(string text)
    {
        TextMeshProUGUI t = UIFactory.Text(root, text, 36, new Vector2(0.5f, 1f), new Vector2(0, -380), new Vector2(1600, 120), color: UIFactory.Muted);
        t.richText = true;
    }

    private Button ShopButton(int index, System.Func<string> label, System.Func<bool> available, UnityEngine.Events.UnityAction onBuy)
    {
        Button b = UIFactory.Button(root, "", Center, new Vector2(0, -67 - index * 115), new Vector2(1100, 95), () =>
        {
            onBuy();
            RefreshButtons();
        });
        shopButtons.Add((b, label, available));
        return b;
    }

    private void Feedback(string text)
    {
        UIFactory.SetText(feedbackText, text ?? "Can't do that.");
    }

    private void RefreshButtons()
    {
        foreach (var entry in shopButtons)
        {
            UIFactory.SetLabel(entry.button, entry.label());
            bool ok = entry.available();
            entry.button.GetComponentInChildren<TextMeshProUGUI>().color = ok ? UIFactory.Paper : UIFactory.Muted * 0.8f;
        }
        if (statsText != null)
        {
            UIFactory.SetText(statsText, $"<color=#F2C240>${run.Money}</color>     HEALTH {run.Health}/{RunManager.MaxHealth}     KITS {run.FirstAid}/{RunManager.MaxFirstAid}");
        }
    }

    private static void ColorFight(Button b)
    {
        ColorBlock c = b.colors;
        c.normalColor = new Color(0.45f, 0.07f, 0.07f);
        c.selectedColor = new Color(0.85f, 0.15f, 0.12f);
        c.highlightedColor = new Color(0.7f, 0.12f, 0.1f);
        b.colors = c;
    }

    // Liga os botões para teclado/comando e seleciona o botão principal (o que avança).
    private void Chain(Button primary, params Button[] buttons)
    {
        UIFactory.ChainVertical(buttons);
        EventSystem es = EventSystem.current;
        if (es != null) es.SetSelectedGameObject(primary.gameObject);
    }
}
