using DG.Tweening;
using UnityEngine;

namespace Game.Views
{
    public sealed class RotatingGlowUI : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float rotationDuration = 5f;

        private RectTransform _rectTransform;

        private void OnEnable()
        {
            _rectTransform = transform as RectTransform;
            if (_rectTransform == null)
                return;

            _rectTransform.localRotation = Quaternion.identity;
            _rectTransform
                .DORotate(new Vector3(0f, 0f, -360f), rotationDuration, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1);
        }

        private void OnDisable()
        {
            _rectTransform?.DOKill();
        }
    }
}
