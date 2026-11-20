using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    public sealed class RatingOffer
    {
        public Action<int> Submit { get; }
        public Action Close { get; }

        public RatingOffer(Action<int> submit, Action close)
        {
            Submit = submit;
            Close = close;
        }
    }

    public sealed class RatingThankYouOffer
    {
        public string Message { get; }
        public Action Close { get; }

        public RatingThankYouOffer(string message, Action close)
        {
            Message = message;
            Close = close;
        }
    }

    public sealed class RatingUI : UIBase
    {
        private bool _moneySortInitialized;
        [SerializeField] private GameObject _ratingContent;
        [SerializeField] private GameObject _thankYouContent;
        [SerializeField] private TextMeshProUGUI _thankYouText;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private Button[] _starButtons;
        [SerializeField] private Image[] _starImages;
        [SerializeField] private Sprite _selectedStar;
        [SerializeField] private Sprite _unselectedStar;
        [SerializeField] private Button _submitButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Color _selectedColor = Color.white;
        [SerializeField] private Color _unselectedColor = Color.white;

        private RatingOffer _ratingOffer;
        private RatingThankYouOffer _thankYouOffer;
        private int _selectedRating;

        public override void Init(object manager)
        {
            if (_moneySortInitialized) return;
            _moneySortInitialized = true;
            base.Init(manager);
            for (var i = 0; i < _starButtons.Length; i++)
            {
                var rating = i + 1;
                _starButtons[i].onClick.AddListener(() => SelectRating(rating));
            }
            _submitButton.onClick.AddListener(OnSubmit);
            _closeButton.onClick.AddListener(OnClose);
        }

        public override void SetData(params object[] data)
        {
            _ratingOffer = data.Length > 0 ? data[0] as RatingOffer : null;
            _thankYouOffer = data.Length > 0 ? data[0] as RatingThankYouOffer : null;
            var isThankYou = _thankYouOffer != null;
            _ratingContent.SetActive(!isThankYou);
            _thankYouContent.SetActive(isThankYou);
            _titleText.text = isThankYou ? "Rating Submitted!" : "We'd like to hear from you";
            if (isThankYou)
                _thankYouText.text = _thankYouOffer.Message;
            else
                SelectRating(0);
        }

        public override void Hide()
        {
            _ratingOffer = null;
            _thankYouOffer = null;
            base.Hide();
        }

        private void SelectRating(int rating)
        {
            _selectedRating = Mathf.Clamp(rating, 0, _starImages.Length);
            for (var i = 0; i < _starImages.Length; i++)
            {
                var selected = i < _selectedRating;
                _starImages[i].sprite = selected ? _selectedStar : _unselectedStar;
                _starImages[i].color = selected ? _selectedColor : _unselectedColor;
            }
            _submitButton.interactable = _selectedRating > 0;
        }

        private void OnSubmit()
        {
            if (_selectedRating <= 0)
                return;
            var submit = _ratingOffer?.Submit;
            _ratingOffer = null;
            submit?.Invoke(_selectedRating);
        }

        private void OnClose()
        {
            var close = _thankYouOffer?.Close ?? _ratingOffer?.Close;
            _thankYouOffer = null;
            _ratingOffer = null;
            close?.Invoke();
        }

        private void OnDestroy()
        {
            _submitButton?.onClick.RemoveListener(OnSubmit);
            _closeButton?.onClick.RemoveListener(OnClose);
            if (_starButtons == null)
                return;
            foreach (var button in _starButtons)
                button?.onClick.RemoveAllListeners();
        }
    }
}
