using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;
using ClassicSpins.PrototypeFramework.Infrastructure;
using ClassicSpins.PrototypeFramework.Presentation;

namespace Game.Infrastructure
{
    /// <summary>
    /// The framework's <see cref="HapticService"/> (settings gate, persistence) on <see cref="SqadSortHapticBackend"/>.
    /// Its own type so it can replace the framework's registration: VContainer refuses a second Singleton of the same
    /// implementation type for one contract, while a different type simply wins by later registration.
    /// </summary>
    public sealed class SqadSortHapticService : IHapticService
    {
        private readonly HapticService _inner;

        public SqadSortHapticService(IUserData userData, ILog log)
            => _inner = new HapticService(userData, log, new SqadSortHapticBackend());

        public bool IsEnabled => _inner.IsEnabled;
        public void Play(HapticType type) => _inner.Play(type);
        public void SetEnabled(bool on) => _inner.SetEnabled(on);
    }
}
