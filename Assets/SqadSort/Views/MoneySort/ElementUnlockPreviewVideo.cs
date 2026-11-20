using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Game.Views
{
    public sealed class ElementUnlockPreviewVideo : MonoBehaviour
    {
        public VideoClip OverrideClip;
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private RawImage previewImage;
        [SerializeField] private GameObject fallbackPlaceholder;
        [SerializeField] private ElementUnlockVideoConfigSO videoConfig;

        public void Play(MoneySortElementType type)
        {
            var clip = OverrideClip != null ? OverrideClip : videoConfig != null ? videoConfig.GetClip(type) : null;
            var canPlay = videoPlayer != null && previewImage != null && clip != null;
            if (previewImage != null)
                previewImage.gameObject.SetActive(canPlay);
            if (fallbackPlaceholder != null)
                fallbackPlaceholder.SetActive(!canPlay);
            if (!canPlay)
                return;

            videoPlayer.Stop();
            videoPlayer.clip = clip;
            videoPlayer.isLooping = true;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            videoPlayer.Play();
        }

        public void Stop()
        {
            if (videoPlayer != null)
                videoPlayer.Stop();
        }

        private void OnDisable() => Stop();
    }
}
