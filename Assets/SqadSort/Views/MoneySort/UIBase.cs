using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace Game.Views
{
    public abstract class UIBase : MonoBehaviour
    {
        [SerializeField] private UIAnimation _animation;
        [Serializable] public struct TextBinding { public string Id; public TMP_Text Text; }
        [Serializable] public struct ButtonBinding { public string Id; public Button Button; }
        [SerializeField] private TextBinding[] _sourceTextBindings = Array.Empty<TextBinding>();
        [SerializeField] private ButtonBinding[] _sourceButtonBindings = Array.Empty<ButtonBinding>();
        private Action _shown, _hidden;
        public void SetLabel(string id, string value) { foreach(var b in _sourceTextBindings) if(b.Id==id && b.Text!=null) b.Text.text=value??string.Empty; }
        public void ShowLabel(string id,bool shown){foreach(var b in _sourceTextBindings)if(b.Id==id&&b.Text!=null)b.Text.gameObject.SetActive(shown);}
        public Button Control(string id) { foreach(var b in _sourceButtonBindings) if(b.Id==id) return b.Button; return null; }
        public void OverrideButton(string id, Action action, bool enabled=true) { var b=Control(id); if(b==null)return; b.onClick.RemoveAllListeners(); if(action!=null)b.onClick.AddListener(()=>action()); b.interactable=enabled; }
        public void SetAd(string id, bool enabled) { var ad=RewardedAdButton.Bind(Control(id), "cardslot", true); if(ad!=null)ad.SetAvailability(enabled); }
        public void SetButtonLabel(string id,string label) {var b=Control(id);if(b==null)return; var t=b.GetComponentInChildren<TMP_Text>(true);if(t!=null){t.enableAutoSizing=true;t.fontSizeMax=t.fontSize;t.fontSizeMin=t.fontSize*0.6f;t.textWrappingMode=TextWrappingModes.NoWrap;t.text=label??string.Empty;}}
        public void SetLayout(float y=0f,float scale=1f) {var r=(RectTransform)transform;r.anchoredPosition=new Vector2(0,y);r.localScale=Vector3.one*scale;}
        public void ShowWithCompletion(Action completed) { _shown=completed; Show(); }
        public void HideWithCompletion(Action completed) { _hidden=completed; Hide(); }


        protected object Manager { get; private set; }
        public UIAnimation Animation => _animation;

        public virtual void Init(object manager)
        {
            Manager = manager;
            // Reference mockups stay available in Prefab Mode, including if an artist enables them to compare.
            foreach (var child in GetComponentsInChildren<Transform>(true))
                if (child != transform && child.CompareTag("EditorOnly"))
                    child.gameObject.SetActive(false);
        }
        public virtual void SetData(params object[] data) { }

        public virtual void Show()
        {
            gameObject.SetActive(true);
            if (_animation != null) _animation.PlayShow(() => { var done=_shown; _shown=null; done?.Invoke(); });
            else { var done=_shown; _shown=null; done?.Invoke(); }
        }

        public virtual void Hide()
        {
            if (_animation != null && gameObject.activeSelf)
                _animation.PlayHide(() => { gameObject.SetActive(false); var done=_hidden;_hidden=null;done?.Invoke(); });
            else { gameObject.SetActive(false); var done=_hidden; _hidden=null; done?.Invoke(); }
        }

        public void HideImmediate()
        {
            _animation?.SetHiddenImmediate();
            gameObject.SetActive(false);
        }
    }
}
