using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Pequenos helpers para construir UI por código (hub entre noites, HUD da luta, pausa).
public static class UIFactory
{
    public static readonly Color Blood = new Color(0.72f, 0.09f, 0.09f);
    public static readonly Color Paper = new Color(0.93f, 0.89f, 0.80f);
    public static readonly Color Muted = new Color(0.62f, 0.58f, 0.52f);
    public static readonly Color Gold = new Color(0.95f, 0.76f, 0.25f);
    public static readonly Color Dark = new Color(0.07f, 0.05f, 0.05f, 0.96f);

    private static TMP_FontAsset font;
    public static TMP_FontAsset Font
    {
        get
        {
            if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts/ThaleahFat SDF");
            if (font == null) font = TMP_Settings.defaultFontAsset;
            return font;
        }
    }

    // A fonte pixel (Thaleah) não tem acentos nem alguns símbolos: normaliza o texto para ela.
    public static string Clean(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        s = s.Replace('·', '|').Replace('—', '-');
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (char c in s.Normalize(System.Text.NormalizationForm.FormD))
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }

    // Guarda o último texto de cada label: se não mudou, não volta a limpar nem a reconstruir o TMP.
    private static readonly System.Collections.Generic.Dictionary<int, string> lastText =
        new System.Collections.Generic.Dictionary<int, string>();

    public static void SetText(TMP_Text label, string text)
    {
        int id = label.GetInstanceID();
        if (lastText.TryGetValue(id, out string previous) && previous == text) return;
        lastText[id] = text;
        label.text = Clean(text);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() => lastText.Clear();

    public static Canvas CreateCanvas(string name, int sortOrder, Transform parent = null)
    {
        GameObject go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        if (parent != null) go.transform.SetParent(parent, false);
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortOrder;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    public static Image Stretch(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        Image img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    public static Image Panel(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
    {
        RectTransform rt = Rect(name, parent, anchor, pos, size);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    public static TextMeshProUGUI Text(Transform parent, string text, float size, Vector2 anchor, Vector2 pos,
        Vector2 box, TextAlignmentOptions align = TextAlignmentOptions.Center, Color? color = null)
    {
        RectTransform rt = Rect("Text", parent, anchor, pos, box);
        TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.font = Font;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = color ?? Paper;
        tmp.text = Clean(text);
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }

    public static Button Button(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, UnityAction onClick)
    {
        Image bg = Panel("Button", parent, anchor, pos, size, Color.white);
        Button button = bg.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.16f, 0.12f, 0.11f);
        colors.highlightedColor = new Color(0.45f, 0.10f, 0.10f);
        colors.selectedColor = Blood;
        colors.pressedColor = new Color(0.95f, 0.30f, 0.20f);
        colors.disabledColor = new Color(0.10f, 0.09f, 0.09f, 0.6f);
        colors.fadeDuration = 0.05f;
        button.colors = colors;
        Text(bg.transform, label, 40, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(30, 0));
        if (onClick != null) button.onClick.AddListener(onClick);
        return button;
    }

    public static void SetLabel(Button button, string label)
    {
        SetText(button.GetComponentInChildren<TextMeshProUGUI>(), label);
    }

    // Navegação vertical explícita (teclado/comando) entre botões.
    public static void ChainVertical(params Selectable[] items)
    {
        for (int i = 0; i < items.Length; i++)
        {
            Navigation nav = new Navigation { mode = Navigation.Mode.Explicit };
            nav.selectOnUp = items[(i - 1 + items.Length) % items.Length];
            nav.selectOnDown = items[(i + 1) % items.Length];
            items[i].navigation = nav;
        }
    }

    public static string Plural(int n, string one, string many) => n == 1 ? one : many;

    public static void Delay(MonoBehaviour host, float seconds, Action action)
    {
        host.StartCoroutine(DelayRoutine(seconds, action));
    }

    private static System.Collections.IEnumerator DelayRoutine(float seconds, Action action)
    {
        yield return new WaitForSecondsRealtime(seconds);
        action();
    }
}
