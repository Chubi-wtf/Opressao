using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class QTEPresentationSetup
{
    #region ui

    [MenuItem("Tools/Opressao/Construir feedback visual QTE")]
    private static void Build()
    {
        BuildPresentation();
    }

    public static void BuildForAutomation()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);
        if (!BuildPresentation())
            throw new System.InvalidOperationException("No se pudo construir el feedback visual QTE.");
        // Reload and build again to catch lost references and duplicate runtime objects.
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);
        if (!BuildPresentation())
            throw new System.InvalidOperationException("No se pudo verificar la escena guardada.");
        foreach (QTEManager manager in Object.FindObjectsByType<QTEManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            manager.ValidatePresentationInEditor();
    }

    private static bool BuildPresentation()
    {
        QTEManager[] managers = Resources.FindObjectsOfTypeAll<QTEManager>();
        int updated = 0;
        foreach (QTEManager manager in managers)
        {
            if (!manager.gameObject.scene.IsValid() || !manager.gameObject.scene.isLoaded)
                continue;

            foreach (GameObject root in manager.gameObject.scene.GetRootGameObjects())
                Undo.RegisterFullObjectHierarchyUndo(root, "Construir feedback visual QTE");
            manager.BakeQtePresentationInEditor();
            updated++;
        }

        if (updated > 0)
        {
            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"[QTE] Feedback visual construido y guardado en {updated} escena(s).");
            return true;
        }

        Debug.LogError("[QTE] No se encontró ningún QTEManager en las escenas abiertas.");
        return false;
    }
    #endregion
}
