using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UI;

// Subtitle timing comes from the actual voice clips, so Timeline edits stay in sync.
public sealed class DialogueSubtitles : MonoBehaviour
{
    [Serializable] public sealed class Line
    {
        public string audio;
        public string speaker;
        public string text;
    }
    [Serializable] public sealed class Catalogue { public Line[] lines; }
    private readonly List<(TimelineClip clip, Line line)> cues = new();
    private PlayableDirector director;
    private QTEManager manager;
    private GameObject panel;
    private TextMeshProUGUI label;

    public static void Attach(PlayableDirector timeline, QTEManager owner)
    {
        if (timeline == null || timeline.GetComponent<DialogueSubtitles>() != null) return;
        var subtitles = timeline.gameObject.AddComponent<DialogueSubtitles>();
        subtitles.director = timeline;
        subtitles.manager = owner;
    }

    private void Start()
    {
        var data = Resources.Load<TextAsset>("DialogueSubtitles_es");
        if (director == null || data == null || director.playableAsset is not TimelineAsset timeline) return;
        var catalogue = JsonUtility.FromJson<Catalogue>(data.text);
        if (catalogue?.lines == null) return;
        foreach (var track in timeline.GetOutputTracks())
        {
            if (track is not AudioTrack || track.mutedInHierarchy || !track.name.StartsWith("DIALOGOS_")) continue;
            foreach (var clip in track.GetClips())
            {
                if (clip.asset is not AudioPlayableAsset voice || voice.clip == null) continue;
                var line = Array.Find(catalogue.lines, entry => entry.audio == voice.clip.name);
                if (line != null && !string.IsNullOrWhiteSpace(line.text)) cues.Add((clip, line));
            }
        }
        if (cues.Count == 0) return;
        var canvasObject = new GameObject("Subtitulos", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        panel = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        var rect = (RectTransform)panel.transform;
        rect.anchorMin = new Vector2(0.1f, 0.04f);
        rect.anchorMax = new Vector2(0.9f, 0.18f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var background = panel.GetComponent<Image>();
        background.color = new Color(0, 0, 0, 0.78f);
        background.raycastTarget = false;
        var textObject = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panel.transform, false);
        label = textObject.GetComponent<TextMeshProUGUI>();
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(24, 10);
        label.rectTransform.offsetMax = new Vector2(-24, -10);
        label.fontSize = 34;
        label.enableAutoSizing = true;
        label.fontSizeMin = 24;
        label.fontSizeMax = 34;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.richText = false;
        label.raycastTarget = false;
        panel.SetActive(false);
    }

    private void LateUpdate()
    {
        if (panel == null) return;
        Line active = null;
        if (director != null && director.state == PlayState.Playing && !AudioListener.pause &&
            manager != null && manager.CanAdvanceCinematic)
        {
            foreach (var cue in cues)
                if (director.time >= cue.clip.start && director.time < cue.clip.end) { active = cue.line; break; }
        }
        if (active != null) label.text = active.speaker + ": " + active.text;
        panel.SetActive(active != null);
    }

    private void OnDisable() { if (panel != null) panel.SetActive(false); }
}
