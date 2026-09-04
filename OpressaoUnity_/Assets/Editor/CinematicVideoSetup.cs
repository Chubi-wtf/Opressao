using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UI;
using UnityEngine.Video;

public static class CinematicVideoSetup
{
    private const string VideoFolder = "Assets/Videos";
    private const string GeneratedFolder = "Assets/Videos/Generated";
    private const string RootName = "CinematicVideos";
    private const string AnimaticName = "ANIMATIC PISADEIRA";

    [MenuItem("Tools/Opressao/Integrar videos en Timeline")]
    public static void IntegrateVideos()
    {
        PlayableDirector director = UnityEngine.Object.FindFirstObjectByType<PlayableDirector>();
        Canvas canvas = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate.name == "CanvasManager_");

        if (director == null || director.playableAsset is not TimelineAsset timeline)
            throw new InvalidOperationException("No se encontro un PlayableDirector con Timeline en la escena.");

        if (canvas == null)
            throw new InvalidOperationException("No se encontro el CanvasManager_ en la escena.");

        string[] videoPaths = AssetDatabase.FindAssets("t:VideoClip", new[] { VideoFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => string.Equals(Path.GetExtension(path), ".mp4", StringComparison.OrdinalIgnoreCase))
            .Where(path => string.Equals(Path.GetFileNameWithoutExtension(path), AnimaticName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (videoPaths.Length == 0)
            throw new InvalidOperationException($"No se encontró {AnimaticName}.mp4 en {VideoFolder}.");

        EnsureFolder(GeneratedFolder);

        Transform existingRoot = canvas.transform.Find(RootName);
        if (existingRoot != null)
            UnityEngine.Object.DestroyImmediate(existingRoot.gameObject);

        foreach (TrackAsset oldTrack in timeline.GetOutputTracks()
                     .Where(track => track.name.StartsWith("VIDEO_", StringComparison.Ordinal))
                     .ToArray())
        {
            timeline.DeleteTrack(oldTrack);
        }

        var root = new GameObject(RootName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, "Crear contenedor de videos cinematograficos");
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(canvas.transform, false);
        StretchToParent(rootRect);
        root.transform.SetAsLastSibling();

        double cursor = timeline.GetOutputTracks()
            .SelectMany(track => track.GetClips())
            .Select(clip => clip.end)
            .DefaultIfEmpty(0d)
            .Max();

        foreach (string videoPath in videoPaths)
        {
            VideoClip videoClip = AssetDatabase.LoadAssetAtPath<VideoClip>(videoPath);
            string safeName = SanitizeName(Path.GetFileNameWithoutExtension(videoPath));

            GameObject videoObject = CreateVideoObject(rootRect, videoClip, safeName);
            videoObject.SetActive(false);

            ActivationTrack track = timeline.CreateTrack<ActivationTrack>(null, $"VIDEO_{safeName}");
            track.postPlaybackState = ActivationTrack.PostPlaybackState.Inactive;

            TimelineClip timelineClip = track.CreateDefaultClip();
            timelineClip.displayName = videoClip.name;
            timelineClip.start = cursor;
            timelineClip.duration = Math.Max(0.1d, videoClip.length);
            director.SetGenericBinding(track, videoObject);

            cursor = timelineClip.end;
        }

        EditorUtility.SetDirty(timeline);
        EditorUtility.SetDirty(director);
        EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();

        Selection.activeGameObject = root;
        Debug.Log($"Cinematica configurada con {videoPaths.Length} videos. Duracion total: {cursor:0.00} s.");
    }

    private static GameObject CreateVideoObject(RectTransform parent, VideoClip clip, string safeName)
    {
        var videoObject = new GameObject(
            $"Video_{safeName}",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage),
            typeof(AspectRatioFitter),
            typeof(VideoPlayer),
            typeof(AudioSource),
            typeof(TimelineVideoPlayerBehaviour));

        RectTransform rect = videoObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        StretchToParent(rect);

        int width = clip.width > 0 ? Mathf.Clamp((int)clip.width, 16, 1920) : 1280;
        int height = clip.height > 0 ? Mathf.Clamp((int)clip.height, 16, 1080) : 720;
        string renderTexturePath = $"{GeneratedFolder}/{safeName}.renderTexture";
        RenderTexture renderTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(renderTexturePath);

        if (renderTexture == null)
        {
            renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = $"RT_{safeName}"
            };
            AssetDatabase.CreateAsset(renderTexture, renderTexturePath);
        }

        RawImage rawImage = videoObject.GetComponent<RawImage>();
        rawImage.texture = renderTexture;
        rawImage.raycastTarget = false;

        AspectRatioFitter fitter = videoObject.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = (float)width / height;

        AudioSource audioSource = videoObject.GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        VideoPlayer videoPlayer = videoObject.GetComponent<VideoPlayer>();
        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.clip = clip;
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = renderTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;

        if (clip.audioTrackCount > 0)
        {
            videoPlayer.controlledAudioTrackCount = 1;
            videoPlayer.EnableAudioTrack(0, true);
            videoPlayer.SetTargetAudioSource(0, audioSource);
        }
        else
        {
            audioSource.enabled = false;
        }

        return videoObject;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static string SanitizeName(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray())
            .Replace(' ', '_');
    }

    private static void EnsureFolder(string folder)
    {
        string current = "Assets";
        foreach (string part in folder.Split('/').Skip(1))
        {
            string next = $"{current}/{part}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, part);
            current = next;
        }
    }
}
