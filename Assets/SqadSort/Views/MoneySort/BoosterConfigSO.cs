using UnityEngine;
using UnityEngine.Video;

namespace Game.Views
{
    [CreateAssetMenu(fileName = "BoosterConfigSO", menuName = "Flick Sort/Booster Config")]
    public sealed class BoosterConfigSO : ScriptableObject
    {
        [Header("Icons")]
        [SerializeField] private Sprite shuffleIcon;
        [SerializeField] private Sprite hammerIcon;
        [SerializeField] private Sprite freeMoveIcon;

        [Header("Unlock Preview Videos")]
        [SerializeField] private VideoClip shuffleClip;
        [SerializeField] private VideoClip hammerClip;
        [SerializeField] private VideoClip freeMoveClip;

        public Sprite GetIcon(MoneySortBoosterType type) => type switch
        {
            MoneySortBoosterType.Shuffle => shuffleIcon,
            MoneySortBoosterType.Hammer => hammerIcon,
            MoneySortBoosterType.FreeMove => freeMoveIcon,
            _ => null
        };

        public VideoClip GetClip(MoneySortBoosterType type) => type switch
        {
            MoneySortBoosterType.Shuffle => shuffleClip,
            MoneySortBoosterType.Hammer => hammerClip,
            MoneySortBoosterType.FreeMove => freeMoveClip,
            _ => null
        };
    }
}
