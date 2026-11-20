using System;
using System.Collections.Generic;
using ClassicSpins.PrototypeFramework.Domain;

namespace Game.Application
{
    /// <summary>
    /// D-076 (CR-013 H4, Money Sort <c>BonusMultiplierWheelUI</c>): the win bonus needle. It swings across a half circle of
    /// multiplier segments laid out left to right (×2 ×3 ×5 ×3 ×2); stopping it picks the segment it points at, and the rewarded ad
    /// then pays the win reward × that multiplier. Engine-free: advanced by a fixed dt passed in (rule #15); the only randomness is
    /// where along the swing it starts, drawn from the <see cref="IRandom"/> given by the composition point (rule #14).
    /// </summary>
    public sealed class WinBonusNeedle
    {
        /// <summary>The step every <see cref="Advance"/> is cut into, so the swing is the same at any frame rate.</summary>
        public const float FixedStep = 1f / 60f;

        private readonly int[] _multipliers;
        private readonly float _arc, _swingsPerSecond;
        private float _phase, _carry;

        /// <param name="arcDegrees">Needle angle at either end of the swing; the segments split −arc…arc evenly (Money Sort 84).</param>
        /// <param name="swingsPerSecond">Full left-right-left swings per second (Money Sort 0.8).</param>
        public WinBonusNeedle(IReadOnlyList<int> multipliers, float arcDegrees, float swingsPerSecond, IRandom random)
        {
            if (multipliers == null || multipliers.Count == 0) throw new ArgumentException("at least one segment", nameof(multipliers));
            _multipliers = new int[multipliers.Count];
            for (int i = 0; i < _multipliers.Length; i++) _multipliers[i] = Math.Max(1, multipliers[i]);
            _arc = Clamp(arcDegrees, 10f, 90f);
            _swingsPerSecond = Math.Max(0.05f, swingsPerSecond);
            _phase = random != null ? random.NextFloat() : 0f;
            Swinging = true;
        }

        public IReadOnlyList<int> Multipliers => _multipliers;
        public bool Swinging { get; private set; }

        /// <summary>Needle rotation in degrees: 0 points up, positive turns it left (counter-clockwise), like a uGUI z rotation.</summary>
        public float Angle
        {
            get
            {
                // eased ping-pong so the needle lingers a little at each end like a pendulum
                float t = 0.5f - 0.5f * (float)Math.Cos(_phase * 2.0 * Math.PI);
                return _arc + (-_arc - _arc) * t;
            }
        }

        /// <summary>The segment the needle points at, 0 = leftmost.</summary>
        public int Segment
        {
            get
            {
                float fromLeft = (_arc - Angle) / (2f * _arc);
                return Math.Max(0, Math.Min(_multipliers.Length - 1, (int)Math.Floor(fromLeft * _multipliers.Length)));
            }
        }

        public int Multiplier => _multipliers[Segment];

        /// <summary>What the ad adds on top of the already granted <paramref name="reward"/> at the current segment: ×3 on 40 ⇒ +80.</summary>
        public long BonusFor(int reward) => (long)Math.Max(0, reward) * (Multiplier - 1);

        /// <summary>Move the needle by <paramref name="dt"/> seconds of swing (nothing while stopped).</summary>
        public void Advance(float dt)
        {
            if (!Swinging || !(dt > 0f)) return;
            _carry += dt;
            while (_carry >= FixedStep)
            {
                _carry -= FixedStep;
                _phase += FixedStep * _swingsPerSecond;
                if (_phase >= 1f) _phase -= 1f;
            }
        }

        /// <summary>Stop the needle where it is; returns the picked multiplier.</summary>
        public int Stop()
        {
            Swinging = false;
            _carry = 0f;
            return Multiplier;
        }

        public void Resume() => Swinging = true;

        private static float Clamp(float v, float lo, float hi) => float.IsNaN(v) ? lo : Math.Max(lo, Math.Min(hi, v));
    }
}
