using UnityEngine;

namespace Game.Views
{
    /// <summary>
    /// The SKU's shared, data-chosen look values (pf-visual-design rung 2). Only values a controller
    /// picks by data live here — the squad palette indexed by colour id. Everything a single prefab
    /// owns (ground, slot tones, sizes) stays on that prefab.
    /// One asset: <c>Assets/SqadSort/Content/UI/Look/LookLibrary.asset</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "SqadSort/Look Library", fileName = "LookLibrary")]
    public sealed class LookLibrary : ScriptableObject
    {
        [Tooltip("Buddy / squad / house colours, indexed by the domain colour id (Red, Orange, Yellow, Green, DarkGreen, Cyan, Blue, Purple, Pink, Brown, Gray, Black, White).")]
        [SerializeField] private Color[] _squadColors = new Color[0];

        [Tooltip("Body tint of a hidden buddy, before its colour is revealed.")]
        [SerializeField] private Color _hiddenBuddy;

        [SerializeField] private GameObject[] _buddyModels = new GameObject[0];
        [SerializeField] private GameObject _hiddenBuddyModel;
        public GameObject BuddyModel(int colorId) => colorId >= 0 && colorId < _buddyModels.Length ? _buddyModels[colorId] : null;
        public GameObject HiddenBuddyModel => _hiddenBuddyModel;
        public Color HiddenBuddy => _hiddenBuddy;

        /// <summary>The colour for a colour id; magenta when the palette has no entry, so a gap is visible.</summary>
        public Color SquadColor(int colorId)
            => colorId >= 0 && colorId < _squadColors.Length ? _squadColors[colorId] : Color.magenta;
    }
}
