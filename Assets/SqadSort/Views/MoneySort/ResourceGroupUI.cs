using TMPro;
using UnityEngine;

namespace Game.Views
{
    public sealed class ResourceGroupUI : MonoBehaviour
    {
        public void SetLabels(string money,string lives,string next){if(money!=null&&_moneyText!=null)_moneyText.text=money;if(lives!=null&&_livesText!=null)_livesText.text=lives;if(next!=null&&_lifeTimerText!=null)_lifeTimerText.text=next;}
        private const string InfinitySymbol = "∞";

        [SerializeField] private TextMeshProUGUI _moneyText;
        [SerializeField] private TextMeshProUGUI _livesText;
        [SerializeField] private TextMeshProUGUI _lifeTimerText;
        [Tooltip("Optional: shown instead of the lives count while infinite lives are active.")]
        [SerializeField] private GameObject _infiniteLivesIcon;

        public void SetMoney(int amount)
        {
            if (_moneyText != null)
                _moneyText.text = Mathf.Max(0, amount).ToString("N0");
        }

        public void SetLives(int current, int maximum, int refillSecondsRemaining, int infiniteSecondsRemaining = 0)
        {
            var infinite = infiniteSecondsRemaining > 0;
            if (_infiniteLivesIcon != null)
                _infiniteLivesIcon.SetActive(infinite);

            var currentLives = Mathf.Clamp(current, 0, Mathf.Max(1, maximum));
            var remaining = Mathf.Max(0, refillSecondsRemaining);
            var timerText = infinite
                ? FormatInfiniteTimer(infiniteSecondsRemaining)
                : currentLives >= maximum
                    ? "Max"
                    : $"{remaining / 60:00}:{remaining % 60:00}";
            var livesText = infinite
                ? _infiniteLivesIcon != null ? string.Empty : InfinitySymbol
                : currentLives.ToString();
            if (_livesText != null)
                _livesText.text = _lifeTimerText != null
                    ? livesText
                    : $"{livesText}  {timerText}";
            if (_lifeTimerText != null)
                _lifeTimerText.text = timerText;
        }

        // Shared by every infinite-lives countdown: "2d" from one day, "12h 23m" from one hour, else "15:12".
        public static string FormatInfiniteTimer(int seconds)
        {
            var total = Mathf.Max(0, seconds);
            var days = total / 86400;
            if (days > 0)
                return $"{days}d";
            var hours = total / 3600;
            if (hours > 0)
                return $"{hours}h {total % 3600 / 60:00}m";
            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}
