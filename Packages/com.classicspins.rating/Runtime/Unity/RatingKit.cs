namespace ClassicSpins.Rating
{
    /// <summary>
    /// One-line set-up for a game with no UI or save system of its own to plug in: the default popup, the PlayerPrefs
    /// flag and the focus relay, all wired. A game that has its own dialogs, save or lifecycle hook builds a
    /// <see cref="RatingPrompter"/> directly with its own <see cref="IRatingPopup"/> / <see cref="IRatingState"/> and forwards
    /// focus itself.
    /// </summary>
    public static class RatingKit
    {
        /// <summary>
        /// A ready prompter. Call <see cref="RatingPrompter.TryPromptAsync"/> after each win (it does nothing until the
        /// level and the once-per-install rule say so). Keep the returned object for the session.
        /// </summary>
        /// <param name="settings">When to prompt and where answers go; null = Money Sort's defaults (level 15, 4+ stars to the store).</param>
        /// <param name="style">The popup's text and look; null = the defaults.</param>
        /// <param name="prefsKey">The PlayerPrefs key of the "offered" flag; null = <see cref="PlayerPrefsRatingState.DefaultKey"/>.</param>
        public static RatingPrompter Create(RatingSettings settings = null, RatingPopupStyle style = null, string prefsKey = null)
        {
            var prompter = new RatingPrompter(settings ?? new RatingSettings(), new PlayerPrefsRatingState(prefsKey),
                new RatingPopup(style ?? new RatingPopupStyle()), new UnityRatingHost());
            prompter.Log = UnityEngine.Debug.Log;
            RatingAppFocus.Instance.Changed += prompter.OnAppFocusChanged;
            return prompter;
        }
    }
}
