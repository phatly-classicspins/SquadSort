using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.Views
{
    public enum MoneySortHomeTab
    {
        Shop,
        Main,
        Locked
    }

    // Bottom bar on Home: Shop / main game / a locked tab reserved for a later feature.
    public sealed class HomeFooterUI : MonoBehaviour
    {
        [Serializable]
        private sealed class TabView
        {
            public MoneySortHomeTab tab;
            public Button button;
            [Tooltip("Enabled while this tab is selected.")]
            public GameObject selected;
            [Tooltip("Enabled while this tab is not selected.")]
            public GameObject deselected;
            [Tooltip("Enabled while this tab is locked.")]
            public GameObject lockIcon;
            public bool locked;
            public TMP_Text label;
            [Tooltip("Optional caption kept with the deselected art, localized together with the selected caption.")]
            public TMP_Text unselectedLabel;
        }

        [SerializeField] private TabView[] _tabs = Array.Empty<TabView>();

        private Action<MoneySortHomeTab> _tapped;
        private bool _initialized;

        public void Init(Action<MoneySortHomeTab> tapped)
        {
            _tapped = tapped;
            if (_initialized) { RefreshLocks(); return; }
            _initialized = true;
            foreach (var view in _tabs)
            {
                if (view?.button == null)
                    continue;
                var tab = view.tab;
                view.button.onClick.AddListener(() => OnTabTapped(tab));
            }
            RefreshLocks();
        }

        public void SetLabels(string shop, string home)
        {
            foreach (var view in _tabs)
            {
                if (view == null) continue;
                string text = view.tab == MoneySortHomeTab.Shop ? shop : view.tab == MoneySortHomeTab.Main ? home : null;
                if (text != null)
                {
                    if (view.label != null) view.label.text = text;
                    if (view.unselectedLabel != null) view.unselectedLabel.text = text;
                }
            }
        }
        public void SetSelected(MoneySortHomeTab selectedTab)
        {
            foreach (var view in _tabs)
            {
                if (view == null)
                    continue;
                var isSelected = view.tab == selectedTab;
                if (view.selected != null)
                    view.selected.SetActive(isSelected);
                if (view.deselected != null)
                    view.deselected.SetActive(!isSelected);
            }
        }

        // Unlock the reserved tab here once its feature exists; the tap is then forwarded as usual.
        public void SetLocked(MoneySortHomeTab tab, bool locked)
        {
            foreach (var view in _tabs)
            {
                if (view != null && view.tab == tab)
                    view.locked = locked;
            }
            RefreshLocks();
        }

        public bool IsLocked(MoneySortHomeTab tab)
        {
            foreach (var view in _tabs)
            {
                if (view != null && view.tab == tab)
                    return view.locked;
            }
            return false;
        }

        private void OnTabTapped(MoneySortHomeTab tab)
        {
            if (IsLocked(tab))
                return;
            _tapped?.Invoke(tab);
        }

        private void RefreshLocks()
        {
            foreach (var view in _tabs)
            {
                if (view?.lockIcon != null)
                    view.lockIcon.SetActive(view.locked);
            }
        }

        private void OnDestroy()
        {
            foreach (var view in _tabs)
                view?.button?.onClick.RemoveAllListeners();
        }
    }
}
