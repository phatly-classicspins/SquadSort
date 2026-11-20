using System;

namespace ClassicSpins.Rating
{
    /// <summary>
    /// When the prompt shows and where each answer goes. Every field has the Money Sort default; a game usually fills
    /// these from its remote config so a change needs no build.
    /// </summary>
    public sealed class RatingSettings
    {
        /// <summary>The prompt shows after the player wins this level or any later one, once per install. 0 or less turns
        /// the prompt off. (Money Sort: 15, and only on that exact level; "or later" also reaches players who were already
        /// past it when the prompt shipped.)</summary>
        public int PromptAfterLevel = 15;

        /// <summary>This many stars or more go to the store's review page; fewer go to <see cref="FeedbackUrl"/>.</summary>
        public int StoreMinStars = 4;

        /// <summary>The App Store id (digits only, e.g. "6804992035"). Empty: an iOS player who picks many stars gets the
        /// thank-you without leaving the game.</summary>
        public string IosAppId = "";

        /// <summary>The Google Play package name. Empty: the host's own app identifier is used.</summary>
        public string AndroidPackage = "";

        /// <summary>Where a player who picks few stars is sent (a support or survey page). Empty: thank-you only.</summary>
        public string FeedbackUrl = "";

        /// <summary>After a link is opened the flow waits this long for the game to lose focus. If it never does — the link
        /// did not open, or opened inside the game — the thank-you shows anyway instead of waiting forever.</summary>
        public double LeaveTimeoutSeconds = 3;

        public bool Enabled => PromptAfterLevel > 0;

        /// <summary>A copy with every value pulled into a range the flow can run on (a bad remote value degrades to a safe one).</summary>
        public RatingSettings Sanitized() => new RatingSettings
        {
            PromptAfterLevel = PromptAfterLevel,
            StoreMinStars = Math.Max(1, Math.Min(RatingPrompter.MaxStars, StoreMinStars)),
            IosAppId = (IosAppId ?? "").Trim(),
            AndroidPackage = (AndroidPackage ?? "").Trim(),
            FeedbackUrl = (FeedbackUrl ?? "").Trim(),
            LeaveTimeoutSeconds = double.IsNaN(LeaveTimeoutSeconds) ? 3 : Math.Max(0.5, Math.Min(30, LeaveTimeoutSeconds)),
        };
    }
}
