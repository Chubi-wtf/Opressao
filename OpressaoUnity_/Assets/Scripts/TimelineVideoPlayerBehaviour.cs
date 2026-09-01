using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

[RequireComponent(typeof(VideoPlayer))]
public sealed class TimelineVideoPlayerBehaviour : MonoBehaviour
{
    private static readonly HashSet<TimelineVideoPlayerBehaviour> ActivePlayers = new();

    private VideoPlayer videoPlayer;

    private void Awake()
    {
        videoPlayer = GetComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = true;
    }

    private void OnEnable()
    {
        ActivePlayers.Add(this);

        if (!Application.isPlaying)
            return;

        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();

        videoPlayer.Stop();
        videoPlayer.frame = 0;
        videoPlayer.Play();
    }

    private void OnDisable()
    {
        ActivePlayers.Remove(this);

        if (videoPlayer != null && Application.isPlaying)
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
        if (videoPlayer != null && videoPlayer.isPlaying)
            videoPlayer.Pause();
    }

    private void ResumePlayback()
    {
        if (!isActiveAndEnabled)
            return;

        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();

        if (videoPlayer.clip != null && !videoPlayer.isPlaying)
            videoPlayer.Play();
    }

    private void StopPlayback()
    {
        if (videoPlayer != null)
            videoPlayer.Stop();
    }
}
