using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine.SceneManagement;

public class HospitalDialogueSystem : MonoBehaviour
{
    public GameObject fadePanel;
    public GameObject hospitalBillPanel;
    public TMP_Text billText;
    public Button yesButton;
    public Button noButton;
    public int baseBillCost = 80;

    public GameObject doctorPanel;
    public GameObject playerPanel;
    public TMP_Text doctorText;
    public TMP_Text playerText;
    public Image doctorImage;
    public Image playerImage;

    public float typewriterSpeed = 0.05f;
    public float fadeDuration = 0.5f;

    private Languages language = Languages.English;
    private JArray dialogueData;
    private int currentDialogueIndex = 0;
    private bool isTyping = false;
    private bool canProceed = false;
    private Coroutine typingCoroutine;
    private bool billShown = false;
    [SerializeField] private string sceneName = "CoffeeShop2";

    void Start()
    {
        if (hospitalBillPanel != null)
        {
            hospitalBillPanel.SetActive(false);
        }

        LoadLanguage();
        LoadDialogueData();
        StartCoroutine(DelayedStartDialogue());
    }

    void LoadLanguage()
    {
        if (PlayerPrefs.HasKey("Language"))
        {
            string lan = PlayerPrefs.GetString("Language");

            if (System.Enum.TryParse(lan, out Languages parsedLanguage))
            {
                language = parsedLanguage;
            }
            else
            {
                language = Languages.English;
            }
        }
        else
        {
            language = Languages.English;
        }
    }

    void LoadDialogueData()
    {
        string fileName = "HospitalDialogue_" + language.ToString();

        TextAsset jsonFile = Resources.Load<TextAsset>(fileName);

        if (jsonFile != null)
        {
            dialogueData = JArray.Parse(jsonFile.text);
        }
    }

    IEnumerator DelayedStartDialogue()
    {
        yield return new WaitForSeconds(0.5f);
        StartDialogue();
    }

    void Update()
    {
        if (!billShown && GameInput.Player.Interact.WasPressedThisFrame())
        {
            HandleInput();
        }
    }

    void StartDialogue()
    {
        currentDialogueIndex = 0;
        ShowCurrentDialogue();
    }

    void HandleInput()
    {
        if (isTyping)
        {
            CompleteText();
        }
        else if (canProceed)
        {
            NextDialogue();
        }
    }

    void ShowCurrentDialogue()
    {
        if (currentDialogueIndex >= dialogueData.Count)
        {
            EndDialogue();
            return;
        }

        JObject currentLine = (JObject)dialogueData[currentDialogueIndex];
        string speaker = currentLine["speaker"].ToString();
        string text = currentLine["text"].ToString();

        canProceed = false;

        if (speaker == "doctor")
        {
            StartCoroutine(ShowDoctorDialogue(text));
        }
        else if (speaker == "player")
        {
            StartCoroutine(ShowPlayerDialogue(text));
        }
    }

    IEnumerator ShowDoctorDialogue(string text)
    {
        if (playerPanel.activeInHierarchy)
        {
            yield return StartCoroutine(FadeOutPanel(playerPanel, playerImage));
        }

        doctorText.text = "";

        yield return StartCoroutine(FadeInPanel(doctorPanel, doctorImage));

        typingCoroutine = StartCoroutine(TypeText(doctorText, text));
        yield return typingCoroutine;
    }

    IEnumerator ShowPlayerDialogue(string text)
    {
        if (doctorPanel.activeInHierarchy)
        {
            yield return StartCoroutine(FadeOutPanel(doctorPanel, doctorImage));
        }

        playerText.text = "";

        yield return StartCoroutine(FadeInPanel(playerPanel, playerImage));

        typingCoroutine = StartCoroutine(TypeText(playerText, text));
        yield return typingCoroutine;
    }

    IEnumerator FadeOutPanel(GameObject panel, Image characterImage)
    {
        float timer = 0f;
        Color imageColor = characterImage.color;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);
            imageColor.a = alpha;
            characterImage.color = imageColor;
            yield return null;
        }

        imageColor.a = 0f;
        characterImage.color = imageColor;
        panel.SetActive(false);
    }

    IEnumerator FadeInPanel(GameObject panel, Image characterImage)
    {
        panel.SetActive(true);

        float timer = 0f;
        Color imageColor = characterImage.color;
        imageColor.a = 0f;
        characterImage.color = imageColor;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, timer / fadeDuration);
            imageColor.a = alpha;
            characterImage.color = imageColor;
            yield return null;
        }

        imageColor.a = 1f;
        characterImage.color = imageColor;
    }

    IEnumerator TypeText(TMP_Text textComponent, string fullText)
    {
        isTyping = true;
        textComponent.text = "";

        for (int i = 0; i < fullText.Length; i++)
        {
            textComponent.text += fullText[i];
            yield return new WaitForSeconds(typewriterSpeed);
        }

        isTyping = false;
        canProceed = true;
    }

    void CompleteText()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        JObject currentLine = (JObject)dialogueData[currentDialogueIndex];
        string text = currentLine["text"].ToString();
        string speaker = currentLine["speaker"].ToString();

        if (speaker == "doctor")
        {
            doctorText.text = text;
        }
        else if (speaker == "player")
        {
            playerText.text = text;
        }

        isTyping = false;
        canProceed = true;
    }

    void NextDialogue()
    {
        currentDialogueIndex++;
        ShowCurrentDialogue();
    }

    void EndDialogue()
    {
        fadePanel.SetActive(false);
        StartCoroutine(EndDialogueSequence());
    }

    IEnumerator EndDialogueSequence()
    {
        if (doctorPanel.activeInHierarchy)
        {
            yield return StartCoroutine(FadeOutPanel(doctorPanel, doctorImage));
        }

        if (playerPanel.activeInHierarchy)
        {
            yield return StartCoroutine(FadeOutPanel(playerPanel, playerImage));
        }

        yield return new WaitForSeconds(0.5f);

        ShowHospitalBill();
    }

    void ShowHospitalBill()
    {
        billShown = true;

        int lifeLost = RunManager.MaxHealth - RunManager.Instance.Health;
        int billCost = (int)(baseBillCost + (lifeLost * 1.5));
        int playerMoney = RunManager.Instance.Money;


        if (hospitalBillPanel != null)
        {
            hospitalBillPanel.SetActive(true);
        }
        else
        {
            return;
        }

        if (billText != null)
        {
            billText.text = $"Hospital bill is${billCost}, you have${playerMoney}\nDo you want to pay?";
        }

        SetupButtons(billCost);
    }

    void SetupButtons(int billCost)
    {
        if (yesButton != null)
        {
            yesButton.onClick.RemoveAllListeners();
            yesButton.onClick.AddListener(() => OnYesClicked(billCost));
            yesButton.interactable = true;
        }

        if (noButton != null)
        {
            noButton.onClick.RemoveAllListeners();
            noButton.onClick.AddListener(() => OnNoClicked());
            noButton.interactable = true;
        }
    }

    void OnYesClicked(int billCost)
    {
        if (RunManager.Instance.TrySpend(billCost))
        {
            LoadScene(sceneName);
        }
        else
        {
            LoadScene("TitleScreen");
        }
    }

    void OnNoClicked()
    {
        LoadScene("TitleScreen");
    }

    void LoadScene(string sceneName)
    {
        if (yesButton != null) yesButton.interactable = false;
        if (noButton != null) noButton.interactable = false;

        StartCoroutine(FadeAndLoadScene(sceneName));
    }

    IEnumerator FadeAndLoadScene(string sceneName)
    {
        yield return new WaitForSeconds(0.5f);
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
}