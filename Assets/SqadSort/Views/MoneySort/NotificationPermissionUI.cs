using System;
using TMPro;
using UnityEngine.UI;

namespace Game.Views
{
    public sealed class NotificationPermissionOffer
    {
        public Action Accept { get; }
        public Action Later { get; }

        public NotificationPermissionOffer(Action accept, Action later)
        {
            Accept = accept;
            Later = later;
        }
    }

    public sealed class NotificationPermissionUI : UIBase
    {
        private bool _moneySortInitialized;
        private const string Title = "Stay Updated!";
        private const string Message = "Get a reminder when\nit’s time to play.";

        [UnityEngine.SerializeField] private TextMeshProUGUI _titleText;
        [UnityEngine.SerializeField] private TextMeshProUGUI _messageText;
        [UnityEngine.SerializeField] private Button _yesButton;
        [UnityEngine.SerializeField] private Button _laterButton;
        [UnityEngine.SerializeField] private Button _closeButton;

        private NotificationPermissionOffer _offer;

        public override void Init(object manager)
        {
            if (_moneySortInitialized) return;
            _moneySortInitialized = true;
            base.Init(manager);
            _yesButton.onClick.AddListener(OnYesClicked);
            _laterButton.onClick.AddListener(OnLaterClicked);
            _closeButton.onClick.AddListener(OnLaterClicked);
        }

        public override void SetData(params object[] data)
        {
            _offer = data.Length > 0 ? data[0] as NotificationPermissionOffer : null;
            _titleText.text = Title;
            _messageText.text = Message;
        }

        public override void Hide()
        {
            _offer = null;
            base.Hide();
        }

        private void OnYesClicked()
        {
            var accept = _offer?.Accept;
            _offer = null;
            accept?.Invoke();
        }
        private void OnLaterClicked() => _offer?.Later?.Invoke();

        private void OnDestroy()
        {
            if (_yesButton != null) _yesButton.onClick.RemoveListener(OnYesClicked);
            if (_laterButton != null) _laterButton.onClick.RemoveListener(OnLaterClicked);
            if (_closeButton != null) _closeButton.onClick.RemoveListener(OnLaterClicked);
        }
    }
}
