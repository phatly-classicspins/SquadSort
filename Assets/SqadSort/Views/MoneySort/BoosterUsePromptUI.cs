using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    public sealed class BoosterUsePromptUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI instructionText;
        [SerializeField] private Button closeButton;
        [SerializeField] private BoosterUnlockPreviewVideo previewVideo;

        private Action _cancel;
        private RectTransform _rect;
        private float _authoredBottomY;

        private void Awake() => closeButton.onClick.AddListener(Cancel);

        // Lifts the prompt just clear of a bottom ad banner. The authored layout
        // already leaves a bottom margin, so only the overlap is added. The prompt
        // may cover the Deal button while lifted; it owns the input until it closes anyway.
        public void SetBottomInset(float canvasUnits)
        {
            if (_rect == null)
            {
                _rect = (RectTransform)transform;
                _authoredBottomY = _rect.anchoredPosition.y;
            }
            var authoredBottomEdge = _authoredBottomY - _rect.pivot.y * _rect.rect.height;
            var position = _rect.anchoredPosition;
            position.y = _authoredBottomY + Mathf.Max(0f, canvasUnits - authoredBottomEdge);
            _rect.anchoredPosition = position;
        }

        public void Show(MoneySortBoosterType type, Action cancel)
        {
            _cancel = cancel;
            instructionText.text = type switch
            {
                MoneySortBoosterType.Hammer => "Pick a slot to smash\nwith the hammer and\nclear the blocks!",
                MoneySortBoosterType.FreeMove => "Move any selected\nblock set to a\ndifferent slot.",
                _ => string.Empty
            };
            gameObject.SetActive(true);
            previewVideo?.Play(type);
        }

        public void Hide()
        {
            if (!gameObject.activeSelf)
                return;
            _cancel = null;
            previewVideo?.Stop();
            gameObject.SetActive(false);
        }

        private void Cancel()
        {
            var action = _cancel;
            Hide();
            action?.Invoke();
        }

        private void OnDisable() => previewVideo?.Stop();

        private void OnDestroy()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveListener(Cancel);
        }
    }
}
