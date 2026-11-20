using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    public sealed class RestorePurchaseUI : UIBase
    {
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _message;
        [SerializeField] private TMP_Text _okLabel;
        [SerializeField] private Button _okButton;

        public void Bind(string title, string message, string ok, Action close)
        {
            _title.text = title;
            _message.text = message;
            _okLabel.text = ok;
            _okButton.onClick.AddListener(() => close());
        }
    }
}
