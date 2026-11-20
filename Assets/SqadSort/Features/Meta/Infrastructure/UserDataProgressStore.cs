using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Infrastructure;
using Game.Application;

namespace Game.Infrastructure
{
    /// <summary>
    /// <see cref="IProgressStore"/> over the framework's <see cref="IUserData"/> envelope (D-023): the models
    /// are the live instances the save loaded (registered as <c>IUserModel</c> defaults at Root, loaded by the
    /// framework's UserDataLoad boot node).
    /// </summary>
    /// <remarks>
    /// <b><see cref="Save"/> writes now, not later (G18, G19).</b> <see cref="IUserData.Save"/> only requests the
    /// framework's debounced write, which can never fail where the caller sees it — so the progress service's
    /// roll-back-on-failure would never run, and a kill inside the debounce window would lose a purchase or a win.
    /// On the shipped <see cref="FileUserData"/> this store flushes straight after the request: the envelope is on
    /// disk (atomically) when <see cref="Save"/> returns, and a storage failure throws here, where
    /// <c>LevelProgressService</c> restores the previous state. The save is a small JSON file and these calls come
    /// from player actions (start, win, purchase, settings), never per frame. Any other <see cref="IUserData"/>
    /// (an in-memory one in tests) keeps its own behaviour.
    /// </remarks>
    public sealed class UserDataProgressStore : IProgressStore
    {
        private readonly IUserData _userData;

        public UserDataProgressStore(IUserData userData) { _userData = userData; }

        public ProgressModel Progress => _userData.Get<ProgressModel>();
        public SettingsModel Settings => _userData.Get<SettingsModel>();

        public void Save()
        {
            _userData.Save();                                     // marks the envelope dirty
            if (_userData is FileUserData file) file.Flush();     // … and writes it now: a failure throws to the caller
        }
    }
}
