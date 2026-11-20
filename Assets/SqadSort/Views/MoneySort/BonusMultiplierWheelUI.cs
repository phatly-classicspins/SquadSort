using TMPro;
using UnityEngine;

namespace Game.Views
{
    // Level complete bonus wheel (Figma "New rewarded ads placement" > Minigame bonus): a needle
    // swings across a half circle of multiplier segments, laid out left to right. Stopping it
    // picks the segment the needle points at.
    public sealed class BonusMultiplierWheelUI : MonoBehaviour
    {
        public void SetExternalAngle(float angle) { _swinging=false; if(_needle!=null)_needle.localRotation=Quaternion.Euler(0,0,angle); }
        [SerializeField] private RectTransform _needle;
        [Tooltip("One label per segment, left to right.")]
        [SerializeField] private TextMeshProUGUI[] _segmentLabels;
        [Tooltip("Needle angle at either end of the swing; the segments split -arc..arc evenly.")]
        [SerializeField, Range(10f, 90f)] private float _arcDegrees = 80f;
        [Tooltip("Full left-right-left swings per second.")]
        [SerializeField, Min(0.05f)] private float _swingsPerSecond = 0.8f;

        private int[] _multipliers = { 2, 3, 5, 3, 2 };
        private bool _swinging;
        private float _phase;

        public int SegmentCount => _multipliers.Length;
        public int CurrentMultiplier => _multipliers[CurrentSegment];

        // Needle rotation: 0 points up, positive turns it left (counter-clockwise).
        private float NeedleAngle => _needle != null ? Mathf.DeltaAngle(0f, _needle.localEulerAngles.z) : 0f;

        private int CurrentSegment
        {
            get
            {
                var fromLeft = Mathf.InverseLerp(_arcDegrees, -_arcDegrees, NeedleAngle);
                return Mathf.Clamp(Mathf.FloorToInt(fromLeft * _multipliers.Length), 0, _multipliers.Length - 1);
            }
        }

        // Extra values beyond the built segment labels are ignored, so the remote list
        // must match the wheel art's segment count.
        public void SetMultipliers(int[] multipliers)
        {
            if (multipliers != null && multipliers.Length > 0)
                _multipliers = multipliers;
            if (_segmentLabels == null)
                return;
            for (var i = 0; i < _segmentLabels.Length; i++)
            {
                if (_segmentLabels[i] != null)
                    _segmentLabels[i].text = $"x{_multipliers[Mathf.Min(i, _multipliers.Length - 1)]}";
            }
        }

        public void StartSwing()
        {
            _swinging = true;
            _phase = 0f;
        }

        public int StopSwing()
        {
            _swinging = false;
            return CurrentMultiplier;
        }

        public void ResumeSwing() => _swinging = true;

        private void Update()
        {
            if (!_swinging || _needle == null)
                return;
            // Unscaled: the level complete screen can show while gameplay time is paused.
            _phase += Time.unscaledDeltaTime * _swingsPerSecond;
            // Eased ping-pong so the needle lingers a little at each end like a pendulum.
            var t = 0.5f - 0.5f * Mathf.Cos(_phase * 2f * Mathf.PI);
            _needle.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(_arcDegrees, -_arcDegrees, t));
        }
    }
}
