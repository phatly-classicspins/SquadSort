using UnityEngine;

namespace Game.Views
{
    /// <summary>
    /// Fits this RectTransform to Screen.safeArea. Parent is expected to fill the whole screen.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        [SerializeField] private bool useTopSafeArea = true;
        [SerializeField] private bool useBottomSafeArea = true;

        private RectTransform _rect;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;
        private bool _lastUseTopSafeArea;
        private bool _lastUseBottomSafeArea;

        private void Awake()
        {
            _rect = (RectTransform)transform;
        }

        private void OnEnable()
        {
            Refresh(true);
        }

        private void Update()
        {
            Refresh(false);
        }

        private void Refresh(bool force)
        {
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            var safeArea = Screen.safeArea;
            if (!force && screenSize == _lastScreenSize && safeArea == _lastSafeArea &&
                useTopSafeArea == _lastUseTopSafeArea &&
                useBottomSafeArea == _lastUseBottomSafeArea)
                return;

            _lastScreenSize = screenSize;
            _lastSafeArea = safeArea;
            _lastUseTopSafeArea = useTopSafeArea;
            _lastUseBottomSafeArea = useBottomSafeArea;

            var width = Mathf.Max(1f, screenSize.x);
            var height = Mathf.Max(1f, screenSize.y);
            _rect.anchorMin = new Vector2(
                safeArea.xMin / width,
                useBottomSafeArea ? safeArea.yMin / height : 0f);
            _rect.anchorMax = new Vector2(
                safeArea.xMax / width,
                useTopSafeArea ? safeArea.yMax / height : 1f);
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
