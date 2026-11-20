using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    public enum LoseButtonStyle { Restart, ReturnHome }

    public sealed class LosePresentation
    {
        public string Title { get; }
        public string Message { get; }
        public string ButtonLabel { get; }
        public Action Action { get; }
        public Action CloseAction { get; }
        public LoseButtonStyle ButtonStyle { get; }

        public LosePresentation(
            string title,
            string message,
            string buttonLabel,
            Action action,
            Action closeAction,
            LoseButtonStyle buttonStyle = LoseButtonStyle.Restart)
        {
            Title = title;
            Message = message;
            ButtonLabel = buttonLabel;
            Action = action;
            CloseAction = closeAction;
            ButtonStyle = buttonStyle;
        }
    }

    public sealed class LoseUI : UIBase
    {
        private bool _moneySortInitialized;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _messageText;
        [SerializeField] private Button _retryButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Sprite _restartButtonSprite;
        [SerializeField] private Sprite _returnHomeButtonSprite;
        // The label as authored matches the Restart button; Return Home swaps to the yellow CTA text style.
        [SerializeField] private Material _returnHomeLabelMaterial;
        [SerializeField] private TMP_ColorGradient _returnHomeLabelGradient;

        private Action _retryAction;
        private Action _closeAction;
        private TextMeshProUGUI _buttonLabel;
        private Material _restartLabelMaterial;
        private TMP_ColorGradient _restartLabelGradient;

        public override void Init(object manager)
        {
            if (_moneySortInitialized) return;
            _moneySortInitialized = true;
            base.Init(manager);
            _retryButton.onClick.AddListener(OnRetryClicked);
            if (_closeButton != null)
                _closeButton.onClick.AddListener(OnCloseClicked);
            _buttonLabel = _retryButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (_buttonLabel != null)
            {
                _restartLabelMaterial = _buttonLabel.fontSharedMaterial;
                _restartLabelGradient = _buttonLabel.colorGradientPreset;
            }
            if (_messageText == null)
            {
                foreach (var label in GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (label != _titleText && label != _buttonLabel &&
                        label.name != "BrokenHeartIcon")
                    {
                        _messageText = label;
                        break;
                    }
                }
            }
        }

        public override void SetData(params object[] data)
        {
            if (data.Length > 0 && data[0] is LosePresentation presentation)
            {
                _titleText.text = presentation.Title;
                if (_messageText != null) _messageText.text = presentation.Message;
                if (_buttonLabel != null) _buttonLabel.text = presentation.ButtonLabel;
                SetButtonStyle(presentation.ButtonStyle);
                _retryAction = presentation.Action;
                _closeAction = presentation.CloseAction;
                if (_closeButton != null)
                    _closeButton.gameObject.SetActive(_closeAction != null);
                return;
            }
            if (data.Length > 0 && data[0] is string title)
                _titleText.text = title;

            _retryAction = data.Length > 1 ? data[1] as Action : null;
            SetButtonStyle(LoseButtonStyle.Restart);
        }

        public override void Hide()
        {
            _retryAction = null;
            _closeAction = null;
            base.Hide();
        }

        public void UpdateMessage(string message)
        {
            if (_messageText != null)
                _messageText.text = message;
        }

        private void OnRetryClicked() => _retryAction?.Invoke();
        private void OnCloseClicked() => _closeAction?.Invoke();

        private void SetButtonStyle(LoseButtonStyle style)
        {
            var returnHome = style == LoseButtonStyle.ReturnHome;
            if (_buttonLabel != null)
            {
                var material = returnHome ? _returnHomeLabelMaterial : _restartLabelMaterial;
                if (material != null)
                    _buttonLabel.fontSharedMaterial = material;
                var gradient = returnHome ? _returnHomeLabelGradient : _restartLabelGradient;
                if (gradient != null)
                    _buttonLabel.colorGradientPreset = gradient;
            }

            if (_retryButton == null || _retryButton.image == null) return;
            var sprite = returnHome ? _returnHomeButtonSprite : _restartButtonSprite;
            if (sprite != null)
                _retryButton.image.sprite = sprite;
        }

        private void OnDestroy()
        {
            if (_retryButton != null)
                _retryButton.onClick.RemoveListener(OnRetryClicked);
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(OnCloseClicked);
        }
    }
}
