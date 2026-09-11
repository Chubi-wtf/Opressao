using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public static class QTESignalSetup
{
    #region referencias

    private static readonly double[] SignalTimes =
    {
        4d,
        12d,
        21d,
        30d,
        34d,
        -1d, // Window signal is available for manual placement in the Timeline.
        38d
    };

    private static readonly string[] SignalNames =
    {
        "Forcejeo inicial",
        "Contener la respiración",
        "Forcejeo desesperado",
        "Moverse antes de que llegue",
        "Abrir puerta",
        "Abrir ventana",
        "Respiración final"
    };

    #endregion

    #region signals

    [MenuItem("Tools/Opressao/Configurar Signals de QTE")]
    public static void Configure()
    {
        QTEManager manager = Object.FindFirstObjectByType<QTEManager>();
        PlayableDirector director = Object.FindFirstObjectByType<PlayableDirector>();
        if (manager == null || director == null || director.playableAsset is not TimelineAsset timeline)
            throw new System.InvalidOperationException("Falta QTEManager o PlayableDirector con Timeline.");

        const string folder = "Assets/Timeline/QTESignals";
        EnsureFolder(folder);
        SignalTrack track = timeline.GetRootTracks().OfType<SignalTrack>().FirstOrDefault(t => t.name == "QTE Signals")
            ?? timeline.CreateTrack<SignalTrack>(null, "QTE Signals");

        foreach (SignalTrack candidate in timeline.GetRootTracks().OfType<SignalTrack>().ToArray())
        {
            if (candidate != track)
                timeline.DeleteTrack(candidate);
        }

        foreach (IMarker marker in track.GetMarkers().ToArray())
            track.DeleteMarker(marker);

        List<SignalAsset> signals = new();
        for (int index = 0; index < SignalTimes.Length; index++)
        {
            string assetName = index == 5 ? "AbrirVentana" : $"QTE_{(index == 6 ? 6 : index + 1)}";
            string path = $"{folder}/{assetName}.asset";
            SignalAsset asset = AssetDatabase.LoadAssetAtPath<SignalAsset>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<SignalAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.name = SignalNames[index];
            EditorUtility.SetDirty(asset);

            signals.Add(asset);
            if (SignalTimes[index] < 0d)
                continue;

            SignalEmitter marker = track.CreateMarker<SignalEmitter>(SignalTimes[index]);
            marker.name = SignalNames[index];
            marker.asset = asset;
            marker.retroactive = true;
            marker.emitOnce = true;
        }

        foreach (QTEStartOnTimelineImage oldTrigger in Object.FindObjectsByType<QTEStartOnTimelineImage>(FindObjectsSortMode.None))
            Object.DestroyImmediate(oldTrigger);

        QTESignalReceiver receiver = manager.GetComponent<QTESignalReceiver>() ?? Undo.AddComponent<QTESignalReceiver>(manager.gameObject);
        receiver.Configure(manager, signals);
        director.SetGenericBinding(track, receiver);

        SerializedObject managerData = new(manager);
        managerData.FindProperty("startFirstQteWithScene").boolValue = false;
        managerData.FindProperty("successVideoTimes").ClearArray();
        managerData.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(receiver);
        EditorUtility.SetDirty(timeline);
        EditorUtility.SetDirty(director);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = manager.gameObject;
    }

    #endregion

    #region utilidades

    private static void EnsureFolder(string path)
    {
        string current = "Assets";
        foreach (string part in path.Split('/').Skip(1))
        {
            string next = $"{current}/{part}";
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part);
            current = next;
        }
    }
    #endregion
}
