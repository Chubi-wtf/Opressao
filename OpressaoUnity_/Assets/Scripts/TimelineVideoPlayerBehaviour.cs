using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

[RequireComponent(typeof(VideoPlayer))]
public sealed class TimelineVideoPlayerBehaviour : MonoBehaviour
{
    private static readonly HashSet<TimelineVideoPlayerBehaviour> ActivePlayers = new();

    [Range(0.1f, 3f)] [SerializeField] private float playbackSpeed = 1f;

    private VideoPlayer videoPlayer;
    private bool playWhenPrepared;

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

        playWhenPrepared = videoPlayer.clip != null && videoPlayer.time <= 0.01d;
        if (playWhenPrepared)
        {
            if (videoPlayer.isPrepared)
                videoPlayer.Play();
            else
                videoPlayer.Prepare();
        }
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

    public static void PauseAll()
    {
        foreach (TimelineVideoPlayerBehaviour player in ActivePlayers)
            player.PausePlayback();
    }

    public static void ResumeAll()
    {
        foreach (TimelineVideoPlayerBehaviour player in ActivePlayers)
            player.ResumePlayback();
    }

    public static void StopAll()
    {
        foreach (TimelineVideoPlayerBehaviour player in ActivePlayers)
            player.StopPlayback();
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

    private void OnVideoPrepared(VideoPlayer source)
    {
        if (isActiveAndEnabled && playWhenPrepared && !source.isPlaying)
            source.Play();
    }

    private static void OnVideoError(VideoPlayer source, string message)
    {
        Debug.LogError($"No se pudo reproducir el video '{source.clip?.name}': {message}", source);
    }
}
