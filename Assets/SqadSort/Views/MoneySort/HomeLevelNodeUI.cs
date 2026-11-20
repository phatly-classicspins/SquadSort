using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    public enum HomeLevelNodeState
    {
        Upcoming,
        Current,
        Completed
    }

    public sealed class HomeLevelNodeUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _levelText;
        [SerializeField] private Image _nodeImage;
        [SerializeField] private GameObject _currentHighlight;
        [SerializeField] private Color _upcomingColor = Color.white;
        [SerializeField] private Color _completedColor = new Color(0.75f, 0.75f, 0.75f, 1f);

        public void SetData(int level, HomeLevelNodeState state)
        {
            _levelText.text = Mathf.Max(1, level).ToString();
            _currentHighlight.SetActive(state == HomeLevelNodeState.Current);
            _nodeImage.color = state == HomeLevelNodeState.Completed
                ? _completedColor
                : _upcomingColor;
        }
    }
}
