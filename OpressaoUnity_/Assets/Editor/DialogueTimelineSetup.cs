using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public static class DialogueTimelineSetup
{
    private static readonly double[] Starts = { 3.45, 9.55, 14.25, 28.1, 33.1, 38.5, 48.3, 50.6, 57.5, 64.3 };
    private static readonly string[] Speakers = { "Hermano", "Claudio", "Hermano", "Pisadeira", "Pisadeira", "Pisadeira", "Hermano", "Claudio", "Hermano", "Claudio" };

    [MenuItem("Tools/Opressao/Integrar dialogos en Timeline")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Sal del modo Play primero.");
        var director = UnityEngine.Object.FindFirstObjectByType<PlayableDirector>();
        var manager = UnityEngine.Object.FindFirstObjectByType<QTEManager>();
        if (director == null || manager == null || director.playableAsset is not TimelineAsset timeline)
            throw new InvalidOperationException("Abre la escena Game.");
        var paths = Enumerable.Range(1, 10).Select(i => $"Assets/Audio/Dialogues_/{(i == 10 ? "Dialogos" : "Dialogo")} n_{i} ({Speakers[i-1]}).MP3").ToArray();
        var clips = paths.Select(AssetDatabase.LoadAssetAtPath<AudioClip>).ToArray();
        if (clips.Any(c => c == null)) throw new InvalidOperationException("Falta un archivo de dialogo.");
        Undo.RegisterCompleteObjectUndo(timeline, "Integrar dialogos");
        foreach (var old in timeline.GetOutputTracks().Where(t => t.name.StartsWith("DIALOGOS_")).ToArray())
        {
            director.ClearGenericBinding(old);
            timeline.DeleteTrack(old);
        }
        var root = director.transform.Find("Dialogues");
        if (root == null)
        {
            var go = new GameObject("Dialogues");
            Undo.RegisterCreatedObjectUndo(go, "Crear audio de dialogos");
            go.transform.SetParent(director.transform, false);
            root = go.transform;
        }
        foreach (string speaker in Speakers.Distinct())
        {
            var child = root.Find(speaker);
            if (child == null)
            {
                var go = new GameObject(speaker, typeof(AudioSource));
                Undo.RegisterCreatedObjectUndo(go, "Crear voz");
                go.transform.SetParent(root, false);
                child = go.transform;
            }
            var source = child.GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = 1f;
            var track = timeline.CreateTrack<AudioTrack>(null, "DIALOGOS_" + speaker);
            director.SetGenericBinding(track, source);
            for (int i = 0; i < 10; i++)
            {
                if (Speakers[i] != speaker) continue;
                var clip = track.CreateClip<AudioPlayableAsset>();
                var asset = (AudioPlayableAsset)clip.asset;
                asset.clip = clips[i];
                asset.loop = false;
                clip.start = Starts[i];
                clip.duration = (double)clips[i].samples / clips[i].frequency;
                clip.displayName = $"{i + 1:00} - {speaker}";
                clip.easeInDuration = 0.015;
                clip.easeOutDuration = 0.025;
            }
            EditorUtility.SetDirty(source);
        }
        var data = new SerializedObject(manager);
        data.FindProperty("waitForTimelineEndForCredits").boolValue = true;
        data.ApplyModifiedProperties();
        // Emit stopped at the end so credits follow the final conversation.
        director.extrapolationMode = DirectorWrapMode.None;
        EditorUtility.SetDirty(director);
        EditorUtility.SetDirty(timeline);
        EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(director.gameObject.scene);
        Validate();
        Selection.activeGameObject = director.gameObject;
    }

    [MenuItem("Tools/Opressao/Validar dialogos en Timeline")]
    public static void Validate()
    {
        var director = UnityEngine.Object.FindFirstObjectByType<PlayableDirector>();
        var timeline = (TimelineAsset)director.playableAsset;
        var tracks = timeline.GetOutputTracks().OfType<AudioTrack>().Where(t => t.name.StartsWith("DIALOGOS_")).ToArray();
        var clips = tracks.SelectMany(t => t.GetClips()).OrderBy(c => c.start).ToArray();
        if (tracks.Length != 3 || clips.Length != 10) throw new InvalidOperationException("Se esperaban tres pistas y diez dialogos.");
        var report = new StringBuilder("Dialogos: comprobacion de Unity\n");
        double previousEnd = 0;
        foreach (var track in tracks)
            if (director.GetGenericBinding(track) is not AudioSource) throw new InvalidOperationException("Pista sin AudioSource.");
        foreach (var clip in clips)
        {
            var asset = (AudioPlayableAsset)clip.asset;
            if (asset.clip == null || clip.start < previousEnd || clip.end > timeline.duration || clip.timeScale != 1d)
                throw new InvalidOperationException("Dialogo solapado, ausente o fuera del Timeline.");
            if (asset.clip.loadState == AudioDataLoadState.Unloaded) asset.clip.LoadAudioData();
            var samples = new float[asset.clip.samples * asset.clip.channels];
            if (!asset.clip.GetData(samples, 0) || !samples.Any(s => Mathf.Abs(s) > 0.001f))
                throw new InvalidOperationException("Audio sin muestras: " + clip.displayName);
            report.AppendLine($"{clip.displayName}: {clip.start:F3} - {clip.end:F3} s; audio OK");
            previousEnd = clip.end;
        }
        foreach (var signal in timeline.GetOutputTracks().OfType<SignalTrack>().SelectMany(t => t.GetMarkers()).OfType<SignalEmitter>())
            if (clips.Any(c => signal.time > c.start && signal.time < c.end))
                throw new InvalidOperationException("Un Signal interrumpe una frase: " + signal.time);
        report.AppendLine("PASS: 10 audios decodificados, 3 pistas conectadas, sin solapamientos ni Signals dentro de frases.");
        Directory.CreateDirectory("Temp");
        File.WriteAllText("Temp/DialogueValidation.txt", report.ToString());
        Debug.Log(report.ToString());
    }
}
