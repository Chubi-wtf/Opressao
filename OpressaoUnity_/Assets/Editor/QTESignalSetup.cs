using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public static class QTESignalSetup
{
    // Cada marcador coincide exactamente con el comienzo de un video que
    // debe detener Timeline y pedir un QTE. Los demás videos no llevan Signal.
    private static readonly double[] SignalTimes =
    {
        1.55d,
        6.216666666666656d,
        18.483333333333334d
    };

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

        // Este Timeline sólo usa una Signal Track. Eliminamos las antiguas,
        // que no estaban enlazadas a ningún Signal Receiver.
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
            string path = $"{folder}/QTE_{index + 1}.asset";
            SignalAsset asset = AssetDatabase.LoadAssetAtPath<SignalAsset>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<SignalAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }

            SignalEmitter marker = track.CreateMarker<SignalEmitter>(SignalTimes[index]);
            marker.asset = asset;
            // También se emite al llegar por un salto tras completar el QTE
            // anterior, para que el video nuevo pueda pausar Timeline.
            marker.retroactive = true;
            marker.emitOnce = true;
            signals.Add(asset);
        }

        foreach (QTEStartOnTimelineImage oldTrigger in Object.FindObjectsByType<QTEStartOnTimelineImage>(FindObjectsSortMode.None))
            Object.DestroyImmediate(oldTrigger);

        QTESignalReceiver receiver = manager.GetComponent<QTESignalReceiver>() ?? Undo.AddComponent<QTESignalReceiver>(manager.gameObject);
        receiver.Configure(manager, signals);
        director.SetGenericBinding(track, receiver);

        SerializedObject managerData = new(manager);
        managerData.FindProperty("startFirstQteWithScene").boolValue = false;
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
}
