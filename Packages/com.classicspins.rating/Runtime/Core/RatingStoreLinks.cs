namespace ClassicSpins.Rating
{
    /// <summary>Which store the running build belongs to.</summary>
    public enum RatingPlatform { Other = 0, Ios = 1, Android = 2 }

    /// <summary>The store's "write a review" address for each platform.</summary>
    public static class RatingStoreLinks
    {
        /// <summary>
        /// The review page of this app, or "" when the platform has no store or the id is missing. <paramref name="native"/>
        /// picks the store app's own scheme (<c>itms-apps://</c>, <c>market://</c>), which opens the store app directly on a
        /// device; the https form works everywhere else (the Editor, a desktop browser).
        /// </summary>
        public static string WriteReview(RatingPlatform platform, string iosAppId, string androidPackage, bool native)
        {
            switch (platform)
            {
                case RatingPlatform.Ios:
                    if (string.IsNullOrWhiteSpace(iosAppId)) return "";
                    string id = iosAppId.Trim();
                    if (id.StartsWith("id")) id = id.Substring(2);
                    return native
                        ? $"itms-apps://itunes.apple.com/app/id{id}?action=write-review"
                        : $"https://apps.apple.com/app/id{id}?action=write-review";
                case RatingPlatform.Android:
                    if (string.IsNullOrWhiteSpace(androidPackage)) return "";
                    string pkg = androidPackage.Trim();
                    return native
                        ? $"market://details?id={pkg}"
                        : $"https://play.google.com/store/apps/details?id={pkg}";
                default:
                    return "";
            }
        }
    }
}
