using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Game.Views
{
    public sealed class BoosterUnlockPreviewVideo : MonoBehaviour
    {
        public Sprite Icon(MoneySortBoosterType type)=>boosterConfig!=null?boosterConfig.GetIcon(type):null;
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private RawImage previewImage;
        [SerializeField] private GameObject fallbackPlaceholder;
        [FormerlySerializedAs("videoConfig")]
        [SerializeField] private BoosterConfigSO boosterConfig;

        public void Play(MoneySortBoosterType type)
        {
            var clip = boosterConfig != null ? boosterConfig.GetClip(type) : null;
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
