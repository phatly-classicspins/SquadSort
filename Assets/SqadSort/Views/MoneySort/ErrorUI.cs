using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    public sealed class ErrorUI : UIBase
    {
        private bool _moneySortInitialized;
        private const string DefaultMessage =
            "No ads available at this time.\nPlease try again later.";

        [SerializeField] private TextMeshProUGUI _messageText;
        [SerializeField] private Button _closeButton;

        private Action _onClose;

        public override void Init(object manager)
        {
            if (_moneySortInitialized) return;
            _moneySortInitialized = true;
            base.Init(manager);
            if (_closeButton != null)
                _closeButton.onClick.AddListener(OnCloseClicked);
        }

        public override void SetData(params object[] data)
        {
            _messageText.text = data.Length > 0 && data[0] is string message
                ? message : DefaultMessage;
            _onClose = data.Length > 1 ? data[1] as Action : null;
        }

        public override void Hide()
        {
            _onClose = null;
            base.Hide();
        }

        private void OnCloseClicked()
        {
            var onClose = _onClose;
            Hide();
            onClose?.Invoke();
        }

        private void OnDestroy()
        {
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(OnCloseClicked);
        }
    }
}
