using System;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

[InitializeOnLoad]
public static class MainMenuCreditsSetup
{
    static MainMenuCreditsSetup()
    {
        EditorApplication.delayCall += ConfigureActiveScene;
    }

    [MenuItem("Tools/Opressao/Configurar créditos del Main Menu")]
    public static void ConfigureActiveScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !SceneManager.GetActiveScene().name.Contains("Main", StringComparison.OrdinalIgnoreCase))
            return;

        GameObject creditsPanel = FindObject("credit");
        Button creditsButton = FindButton("credit");
        if (creditsPanel == null || creditsButton == null)
            return;

        GameObject menuPanel = FindObject("panelmenu") ?? FindObject("menu");
        Button backButton = FindButtonIn(creditsPanel, "volver", "regresar", "back", "return") ?? FirstButtonIn(creditsPanel);
        MainMenuCreditsController controller = GetOrCreateController(creditsPanel.transform.parent);
        controller.Configure(menuPanel, creditsPanel);

        AddPersistentListener(creditsButton, controller, controller.ShowCredits, nameof(MainMenuCreditsController.ShowCredits));
        if (backButton != null)
            AddPersistentListener(backButton, controller, controller.HideCredits, nameof(MainMenuCreditsController.HideCredits));

        creditsPanel.SetActive(false);
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(creditsButton);
        if (backButton != null)
            EditorUtility.SetDirty(backButton);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = creditsPanel;
    }

    private static MainMenuCreditsController GetOrCreateController(Transform parent)
    {
        MainMenuCreditsController existing = UnityEngine.Object.FindFirstObjectByType<MainMenuCreditsController>();
        if (existing != null)
            return existing;

        GameObject owner = new GameObject("MainMenuCreditsController");
        Undo.RegisterCreatedObjectUndo(owner, "Create main menu credits controller");
        owner.transform.SetParent(parent, false);
        return owner.AddComponent<MainMenuCreditsController>();
    }

    private static void AddPersistentListener(Button button, MainMenuCreditsController target,
        UnityEngine.Events.UnityAction action, string methodName)
    {
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            if (button.onClick.GetPersistentTarget(i) == target && button.onClick.GetPersistentMethodName(i) == methodName)
                return;
        }

        UnityEventTools.AddPersistentListener(button.onClick, action);
    }

    private static GameObject FindObject(string requiredTerm)
    {
        string term = Normalize(requiredTerm);
        foreach (GameObject item in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (item.scene.IsValid() && item.scene.isLoaded && Normalize(item.name).Contains(term))
                return item;
        }

        return null;
    }

    private static Button FindButton(string requiredTerm)
    {
        string term = Normalize(requiredTerm);
        foreach (Button button in Resources.FindObjectsOfTypeAll<Button>())
        {
            if (!button.gameObject.scene.IsValid() || !button.gameObject.scene.isLoaded)
                continue;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (Normalize(button.name).Contains(term) || (label != null && Normalize(label.text).Contains(term)))
                return button;
        }

        return null;
    }

    private static Button FindButtonIn(GameObject parent, params string[] terms)
    {
        foreach (Button button in parent.GetComponentsInChildren<Button>(true))
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            foreach (string term in terms)
            {
                if (Normalize(button.name).Contains(Normalize(term)) ||
                    (label != null && Normalize(label.text).Contains(Normalize(term))))
                    return button;
            }
        }

        return null;
    }

    private static Button FirstButtonIn(GameObject parent)
    {
        Button[] buttons = parent.GetComponentsInChildren<Button>(true);
        return buttons.Length > 0 ? buttons[0] : null;
    }

    private static string Normalize(string value)
    {
        return value.ToLowerInvariant()
            .Replace("á", "a")
            .Replace("é", "e")
            .Replace("í", "i")
            .Replace("ó", "o")
            .Replace("ú", "u");
    }
}
