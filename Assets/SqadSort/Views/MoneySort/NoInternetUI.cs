using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    // Blocking "No Internet" popup. It hides itself once the connection is back, either from
    // the periodic check or after Retry; the close button only retries, it can't dismiss it.
    public sealed class NoInternetUI : UIBase
    {
        private bool _moneySortInitialized;
        private const string IdleMessage = "No Internet, please check\nand try again!";
        private const string ReconnectingMessage = "Reconnecting";

        [SerializeField] private TextMeshProUGUI _messageText;
        [SerializeField] private Button _retryButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private CanvasGroup _retryGroup;
        [Tooltip("Ring that spins behind the icon while reconnecting.")]
        [SerializeField] private RectTransform _spinner;
        [SerializeField] private float _spinDegreesPerSecond = 360f;
        [SerializeField, Min(0.1f)] private float _dotsInterval = 0.35f;

        private string _idleMessage = IdleMessage, _reconnectingMessage = ReconnectingMessage;
        public void SetMessages(string idle, string reconnecting) { _idleMessage = idle; _reconnectingMessage = reconnecting; }
        private Action _retry;
        private bool _reconnecting;
        private float _dotsTimer;
        private int _dots;

        public bool IsReconnecting => _reconnecting;

        public override void Init(object manager)
        {
            if (_moneySortInitialized) return;
            _moneySortInitialized = true;
            base.Init(manager);
            var canvas = GetComponent<Canvas>();
            var parentCanvas = transform.parent != null ? transform.parent.GetComponentInParent<Canvas>() : null;
            if (canvas != null && parentCanvas != null)
            {
                canvas.worldCamera = parentCanvas.worldCamera;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 100; // Overlay UI band, above the global input blocker.
            }
            if (_retryButton != null)
                _retryButton.onClick.AddListener(OnRetry);
            if (_closeButton != null)
                _closeButton.onClick.AddListener(OnRetry);
        }

        public override void SetData(params object[] data)
        {
            _retry = data.Length > 0 ? data[0] as Action : null;
            SetReconnecting(false);
        }

        public void SetReconnecting(bool reconnecting)
        {
            _reconnecting = reconnecting;
            _dots = 0;
            _dotsTimer = 0f;
            if (_spinner != null)
            {
                _spinner.gameObject.SetActive(reconnecting);
                _spinner.localRotation = Quaternion.identity;
            }
            if (_retryButton != null)
                _retryButton.interactable = !reconnecting;
            if (_retryGroup != null)
                _retryGroup.alpha = reconnecting ? 0.5f : 1f;
            if (_messageText != null)
                _messageText.text = reconnecting ? _reconnectingMessage : _idleMessage;
        }

        private void Update()
        {
            if (!_reconnecting)
                return;
            // Unscaled: the popup can appear while gameplay time is paused.
            if (_spinner != null)
                _spinner.Rotate(0f, 0f, -_spinDegreesPerSecond * Time.unscaledDeltaTime);
            _dotsTimer += Time.unscaledDeltaTime;
            if (_dotsTimer < _dotsInterval || _messageText == null)
                return;
            _dotsTimer = 0f;
            _dots = (_dots + 1) % 4;
            _messageText.text = _reconnectingMessage + new string('.', _dots);
        }

        private void OnRetry()
        {
            if (!_reconnecting)
                _retry?.Invoke();
        }

        private void OnDestroy()
        {
            if (_retryButton != null)
                _retryButton.onClick.RemoveListener(OnRetry);
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(OnRetry);
        }
    }
}
