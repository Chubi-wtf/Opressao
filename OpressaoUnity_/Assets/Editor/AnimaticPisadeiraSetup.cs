using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AnimaticPisadeiraSetup
{
    [MenuItem("Tools/Opressao/Configurar animatic Pisadeira")]
    public static void Configure()
    {
        CinematicVideoSetup.IntegrateVideos();

        QTEManager manager = Object.FindFirstObjectByType<QTEManager>();
        if (manager == null)
            throw new System.InvalidOperationException("No se encontró QTEManager en la escena.");

        SerializedObject data = new(manager);
        SerializedProperty qtes = data.FindProperty("qtes");
        qtes.arraySize = 5;

        ConfigureQte(qtes.GetArrayElementAtIndex(0), "Forcejeo inicial", QTEType.ButtonSequence, 6f, 4f);
        ConfigureQte(qtes.GetArrayElementAtIndex(1), "Control de respiración", QTEType.HoldButtons, 6.5f, 3.5f);
        ConfigureQte(qtes.GetArrayElementAtIndex(2), "Forcejeo desesperado", QTEType.AlternatingTriggers, 7f, 8f);
        ConfigureQte(qtes.GetArrayElementAtIndex(3), "Muévete antes de que llegue", QTEType.RightStickMovement, 6f, 2.5f);
        ConfigureQte(qtes.GetArrayElementAtIndex(4), "Control de respiración", QTEType.HoldButtons, 6.5f, 3.5f);

        data.FindProperty("successVideoTimes").ClearArray();
        data.FindProperty("startFirstQteWithScene").boolValue = false;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();

        QTESignalSetup.Configure();
        Selection.activeGameObject = manager.gameObject;
        Debug.Log("Animatic Pisadeira configurado: video único, cinco Signals y cinco QTEs.");
    }

    private static void ConfigureQte(SerializedProperty qte, string title, QTEType type, float timeLimit, float requiredAmount)
    {
        qte.FindPropertyRelative("title").stringValue = title;
        qte.FindPropertyRelative("type").enumValueIndex = (int)type;
        qte.FindPropertyRelative("timeLimit").floatValue = timeLimit;
        qte.FindPropertyRelative("requiredAmount").floatValue = requiredAmount;
    }
}
