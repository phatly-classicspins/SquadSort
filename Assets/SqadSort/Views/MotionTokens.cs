namespace Game.Views
{
    /// <summary>
    /// Durations, delays and amplitudes for every board animation (pf-visual-design: motion lives in
    /// one static class, never as literals inside a View). Seconds and world units.
    /// </summary>
    public static class MotionTokens
    {
        /// <summary>Fixed part of a buddy's walk.</summary>
        public const float MoveBase = 0.16f;

        /// <summary>Extra walk time per world unit travelled.</summary>
        public const float MovePerUnit = 0.00055f;

        /// <summary>Longest a single walk may take, however far.</summary>
        public const float MoveMax = 0.55f;

        /// <summary>Delay between consecutive buddies of one move.</summary>
        public const float Stagger = 0.06f;

        /// <summary>How high a walking buddy hops, in world units.</summary>
        public const float HopHeight = 26f;

        /// <summary>Squash on tap.</summary>
        public const float TapSquashDuration = 0.14f;
        public const float TapSquash = 0.12f;

        /// <summary>Picked-up buddy: how high it rises (world units) and how long the rise / drop takes.</summary>
        public const float SelectLift = 28f;
        public const float SelectDuration = 0.16f;

        /// <summary>Celebration hop and pop when a squad completes.</summary>
        public const float CelebrateHop = 0.22f;
        public const float CelebrateHopHeight = 40f;
        public const float CelebratePop = 0.18f;

        /// <summary>A tube leaving the board after its squad completes.</summary>
        public const float TubeFade = 0.25f;

        /// <summary>Reveal of a hidden buddy's colour.</summary>
        public const float Reveal = 0.2f;

        /// <summary>Rejected tap: the plaza flashes and the buddy winces.</summary>
        public const float RejectFlash = 0.35f;
        public const float WinceDuration = 0.3f;
        public const float WinceAmplitude = 10f;

        /// <summary>Pause after the last squad clears before the next level is laid out.</summary>
        public const float LevelEndHold = 0.9f;
    }
}
