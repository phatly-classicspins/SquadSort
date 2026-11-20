using System;
using UnityEngine;
namespace Game.Views
{
    /// <summary>CardSlot supplies the screen rects; MoneySort's tutorial draws its authored mask and hand.</summary>
    public sealed class MoneySortTutorialTargets : MonoBehaviour
    {
        public Func<int, Rect?> StackRect;
        public bool TryGetStackChipScreenRect(int index, out Vector2 min, out Vector2 max) => TryGetStackScreenRect(index, out min, out max);
        public bool TryGetStackScreenRect(int index, out Vector2 min, out Vector2 max)
        {
            var rect = StackRect?.Invoke(index);
            min = rect?.min ?? Vector2.zero; max = rect?.max ?? Vector2.zero;
            return rect.HasValue;
        }
    }
}
