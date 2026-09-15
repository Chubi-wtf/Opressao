using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

[RequireComponent(typeof(VideoPlayer))]
public sealed class TimelineVideoPlayerBehaviour : MonoBehaviour
{
    #region referencias

    private static readonly HashSet<TimelineVideoPlayerBehaviour> ActivePlayers = new();
    private static bool playbackRequested;

    [Range(0.1f, 3f)] [SerializeField] private float playbackSpeed = 1f;

    private VideoPlayer videoPlayer;
    private bool playWhenPrepared;

    #endregion

    #region inicio

    private void Awake()
    {
        videoPlayer = GetComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.playbackSpeed = playbackSpeed;
    }

    private void OnEnable()
    {
        ActivePlayers.Add(this);

        if (!Application.isPlaying)
            return;

        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();

        videoPlayer.playbackSpeed = playbackSpeed;
        videoPlayer.prepareCompleted -= OnVideoPrepared;
        videoPlayer.prepareCompleted += OnVideoPrepared;
        videoPlayer.errorReceived -= OnVideoError;
        videoPlayer.errorReceived += OnVideoError;

        // Timeline must not advance independently while the first video frame is
        // still being decoded. QTEManager starts both after preparation finishes.
        playWhenPrepared = playbackRequested;
        if (videoPlayer.clip != null && !videoPlayer.isPrepared)
            videoPlayer.Prepare();
        else if (playWhenPrepared && videoPlayer.clip != null && !videoPlayer.isPlaying)
            videoPlayer.Play();
    }

    private void OnDisable()
    {
        ActivePlayers.Remove(this);

        if (videoPlayer == null)
            return;

        videoPlayer.prepareCompleted -= OnVideoPrepared;
        videoPlayer.errorReceived -= OnVideoError;
        if (Application.isPlaying)
            videoPlayer.Stop();
    }

    private void OnDestroy()
    {
        ActivePlayers.Remove(this);
    }

    #endregion

    #region reproduccion

    public static void PauseAll()
    {
        playbackRequested = false;
        foreach (TimelineVideoPlayerBehaviour player in ActivePlayers)
            player.PausePlayback();
    }

    public static void ResumeAll()
    {
        playbackRequested = true;
        foreach (TimelineVideoPlayerBehaviour player in ActivePlayers)
            player.ResumePlayback();
    }

    public static void StopAll()
    {
        playbackRequested = false;
        foreach (TimelineVideoPlayerBehaviour player in ActivePlayers)
            player.StopPlayback();
    }

    public static void ResetForNewGame()
    {
        playbackRequested = false;
        // Include inactive Timeline objects: they can retain a preview frame too.
        foreach (var player in Object.FindObjectsByType<TimelineVideoPlayerBehaviour>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            player.videoPlayer ??= player.GetComponent<VideoPlayer>();
            player.StopPlayback();
            RenderTexture target = player.videoPlayer.targetTexture;
            if (target == null || !target.IsCreated()) continue;
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                GL.Clear(true, true, Color.black);
            }
            finally { RenderTexture.active = previous; }
        }
    }

    public static bool AreActivePlayersPrepared()
    {
        bool foundPlayer = false;
        foreach (TimelineVideoPlayerBehaviour player in ActivePlayers)
        {
            foundPlayer = true;
            if (player.videoPlayer == null || !player.videoPlayer.isPrepared)
                return false;
        }
        return foundPlayer;
    }

    private void PausePlayback()
    {
        playWhenPrepared = false;
        if (videoPlayer != null && videoPlayer.isPlaying)
            videoPlayer.Pause();
    }

    private void ResumePlayback()
    {
        if (!isActiveAndEnabled)
            return;

        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();

        videoPlayer.playbackSpeed = playbackSpeed;
        if (videoPlayer.clip == null || videoPlayer.time >= videoPlayer.clip.length - 0.05d)
            return;

        playWhenPrepared = true;
        if (videoPlayer.isPrepared)
            videoPlayer.Play();
        else
            videoPlayer.Prepare();
    }

    private void StopPlayback()
    {
        playWhenPrepared = false;
        if (videoPlayer != null)
            videoPlayer.Stop();
    }

    #endregion

    #region eventos

    private void OnVideoPrepared(VideoPlayer source)
    {
        if (isActiveAndEnabled && playWhenPrepared && !source.isPlaying)
            source.Play();
    }

    private static void OnVideoError(VideoPlayer source, string message)
    {
        Debug.LogError($"No se pudo reproducir el video '{source.clip?.name}': {message}", source);
    }
    #endregion
}
