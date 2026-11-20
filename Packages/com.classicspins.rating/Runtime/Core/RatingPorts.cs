using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClassicSpins.Rating
{
    /// <summary>The popup. The package ships a code-built one (<c>RatingPopup</c>); a game with its own dialog system
    /// implements this instead, so the prompt looks like the rest of the game.</summary>
    public interface IRatingPopup
    {
        /// <summary>Show the star question and finish with the stars picked (1..5), or 0 when the player closed it.
        /// The popup is hidden when the task ends. Cancel <paramref name="ct"/> to take it down.</summary>
        Task<int> AskAsync(CancellationToken ct);

        /// <summary>Show the thank-you (<paramref name="positive"/>: the player picked many stars) and finish when it is closed.</summary>
        Task ThanksAsync(bool positive, CancellationToken ct);
    }

    /// <summary>Remembers that the prompt was offered. It is offered once per install, so this is all the state there is.</summary>
    public interface IRatingState
    {
        bool PromptShown { get; }

        /// <summary>Record — and persist — that the prompt is being offered. False if it could not be saved; the prompt is
        /// then skipped, because offering it without the record could offer it again.</summary>
        bool MarkPromptShown();
    }

    /// <summary>What the flow needs from the engine: which store, how to open a link, how to wait.</summary>
    public interface IRatingHost
    {
        RatingPlatform Platform { get; }

        /// <summary>True on a device, where the store app's own scheme opens the store app.</summary>
        bool NativeStoreLinks { get; }

        /// <summary>The running app's identifier (the Android package name when <see cref="RatingSettings.AndroidPackage"/> is empty).</summary>
        string AppIdentifier { get; }

        void OpenUrl(string url);

        Task Delay(TimeSpan time, CancellationToken ct);
    }

    /// <summary>How a prompt ended.</summary>
    public enum RatingResult
    {
        /// <summary>Not offered: off, not yet the level, already offered, or the record could not be saved.</summary>
        NotShown = 0,
        /// <summary>Offered and closed without picking stars. (Money Sort shows the level-end interstitial here.)</summary>
        Closed = 1,
        /// <summary>Stars picked; the store or feedback page was opened when there is one, then the thank-you was shown.</summary>
        Rated = 2,
    }

    public readonly struct RatingOutcome
    {
        public readonly RatingResult Result;
        /// <summary>1..5 when <see cref="Result"/> is <see cref="RatingResult.Rated"/>, else 0.</summary>
        public readonly int Stars;
        /// <summary>The page that was opened ("" when none).</summary>
        public readonly string OpenedUrl;

        public RatingOutcome(RatingResult result, int stars, string openedUrl)
        {
            Result = result; Stars = stars; OpenedUrl = openedUrl ?? "";
        }

        public bool Positive(int storeMinStars) => Result == RatingResult.Rated && Stars >= storeMinStars;

        public override string ToString() => Result == RatingResult.Rated ? $"Rated {Stars}" : Result.ToString();
    }
}
