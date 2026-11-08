using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChangeLanguage : MonoBehaviour
{
    private Languages language;
    [SerializeField] private TMP_Dropdown dropdown;

    void Start()
    {
        LoadSavedLanguage();
    }

    void LoadSavedLanguage()
    {
        // o seletor de idioma foi removido da cena (o jogo está só em inglês)
        if (dropdown == null)
        {
            PlayerPrefs.SetString("Language", Languages.English.ToString());
            return;
        }
        if (PlayerPrefs.HasKey("Language"))
        {
            string savedLang = PlayerPrefs.GetString("Language");
            System.Enum.TryParse(savedLang, out Languages parsedLanguage);
            language = parsedLanguage;
            switch (language)
            {
                case Languages.English:
                    dropdown.value = 0;
                    break;
                case Languages.Portuguese:
                    dropdown.value = 1;
                    break;
                case Languages.Brainrot:
                    dropdown.value = 2;
                    break;
                default:
                    dropdown.value = 0;
                    break;
            }
        }
        else
        {
            language = Languages.English;
            dropdown.value = 0;
            PlayerPrefs.SetString("Language", language.ToString());
            PlayerPrefs.Save();
        }
    }

    public void ChangeLang()
    {
        if (dropdown.value == 0)
        {
            language = Languages.English;
        }
        else if (dropdown.value == 1)
        {
            language = Languages.Portuguese;
        }
        else if (dropdown.value == 2)
        {
            language = Languages.Brainrot;
        }

            PlayerPrefs.SetString("Language", language.ToString());
        PlayerPrefs.Save();
    }
}