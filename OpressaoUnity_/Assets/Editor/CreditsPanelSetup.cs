using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[InitializeOnLoad]
public static class CreditsPanelSetup
{
    static CreditsPanelSetup()
    {
        EditorApplication.delayCall += EnsureCreditsPanel;
    }

    [MenuItem("Tools/Opressao/Crear panel de créditos")]
    public static void EnsureCreditsPanel()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || FindSceneObject("CreditsPanel") != null)
            return;

        GameObject qtePanel = FindSceneObject("QTEPanel");
        if (qtePanel == null || qtePanel.transform.parent == null)
            return;

        Transform parent = qtePanel.transform.parent;
        GameObject panel = new GameObject("CreditsPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(panel, "Create credits panel");
        panel.transform.SetParent(parent, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image background = panel.GetComponent<Image>();
        background.color = new Color(0.015f, 0.02f, 0.04f, 0.95f);
        background.raycastTarget = true;

        TextMeshProUGUI title = CreateText("Title", panel.transform, 54, FontStyles.Bold, TextAlignmentOptions.Center);
        title.text = "CRÉDITOS";
        SetAnchors(title.rectTransform, new Vector2(0.1f, 0.74f), new Vector2(0.9f, 0.88f));

        TextMeshProUGUI body = CreateText("CreditsText", panel.transform, 30, FontStyles.Normal, TextAlignmentOptions.Center);
        body.text = "ESCRIBE AQUÍ TUS CRÉDITOS";
        body.enableWordWrapping = true;
        SetAnchors(body.rectTransform, new Vector2(0.16f, 0.2f), new Vector2(0.84f, 0.7f));

        panel.transform.SetAsLastSibling();
        panel.SetActive(false);
        EditorSceneManager.MarkSceneDirty(panel.scene);
        Selection.activeGameObject = panel;
    }

    private static TextMeshProUGUI CreateText(string objectName, Transform parent, float fontSize,
        FontStyles style, TextAlignmentOptions alignment)
    {
        GameObject item = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        item.transform.SetParent(parent, false);
        TextMeshProUGUI text = item.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static GameObject FindSceneObject(string objectName)
    {
        foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (candidate.name == objectName && candidate.scene.IsValid() && candidate.scene.isLoaded)
                return candidate;
        }

        return null;
    }
}
