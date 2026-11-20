using System.Threading;
using ClassicSpins.PrototypeFramework.Views;
using Cysharp.Threading.Tasks;
using UnityEngine;
namespace Game.Views
{
    /// <summary>Adapts imported UIBase show/hide hooks to the existing dialog lifecycle.</summary>
    public sealed class MoneySortViewTransition : ViewTransition
    {
        private UIBase _ui;
        private UniTaskCompletionSource _completion;
        public override void Bind(GameObject root) { Stop(); _ui=root==null?null:root.GetComponentInChildren<UIBase>(true); }
        public override void SnapTo(bool shown) { Stop(); _ui?.Animation?.Snap(shown); }
        public override UniTask PlayShowAsync(CancellationToken ct)=>Play(true,ct);
        public override UniTask PlayHideAsync(CancellationToken ct)=>Play(false,ct);
        private async UniTask Play(bool shown,CancellationToken ct)
        {
            Stop(); ct.ThrowIfCancellationRequested(); if(_ui==null)return;
            var completion=_completion=new UniTaskCompletionSource();
            using(ct.Register(()=>{_ui?.Animation?.Stop();completion.TrySetCanceled(ct);}))
            {
                if(shown)_ui.ShowWithCompletion(()=>completion.TrySetResult());
                else _ui.HideWithCompletion(()=>completion.TrySetResult());
                try{await completion.Task;}finally{if(_completion==completion)_completion=null;}
            }
        }
        public override void Stop(){_ui?.Animation?.Stop();_completion?.TrySetCanceled();_completion=null;}
    }
}
