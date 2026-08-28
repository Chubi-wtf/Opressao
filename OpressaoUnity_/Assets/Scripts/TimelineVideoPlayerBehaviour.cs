using UnityEngine;
using UnityEngine.Video;

[RequireComponent(typeof(VideoPlayer))]
public sealed class TimelineVideoPlayerBehaviour : MonoBehaviour
{
    private VideoPlayer videoPlayer;

    private void Awake()
    {
        videoPlayer = GetComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
    }

    private void OnEnable()
    {
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
        if (videoPlayer != null && Application.isPlaying)
            videoPlayer.Stop();
    }
}
