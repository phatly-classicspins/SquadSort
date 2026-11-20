using UnityEngine;
using UnityEngine.Video;

namespace Game.Views
{
    [CreateAssetMenu(fileName = "ElementUnlockVideoConfigSO", menuName = "Flick Sort/Element Unlock Video Config")]
    public sealed class ElementUnlockVideoConfigSO : ScriptableObject
    {
        [Header("Icons")]
        [SerializeField] private Sprite rainbowIcon;
        [SerializeField] private Sprite hiddenMoneyIcon;

        [Header("Unlock Preview Videos")]
        [SerializeField] private VideoClip rainbowClip;
        [SerializeField] private VideoClip hiddenMoneyClip;

        public Sprite GetIcon(MoneySortElementType type) => type switch
        {
            MoneySortElementType.Rainbow => rainbowIcon,
            MoneySortElementType.HiddenMoney => hiddenMoneyIcon,
            _ => null
        };

        public VideoClip GetClip(MoneySortElementType type) => type switch
        {
            MoneySortElementType.Rainbow => rainbowClip,
            MoneySortElementType.HiddenMoney => hiddenMoneyClip,
            _ => null
        };
    }
}
