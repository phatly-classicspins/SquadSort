namespace Game.Application
{
    /// <summary>What the running Gameplay screen lets the cheat menu do (D-048). Each returns false when it cannot right now
    /// (a dialog or ad is up).</summary>
    public interface IGameplayCheatTarget
    {
        bool CheatWin();
        bool CheatLose();
        /// <summary>Redraw HUD and booster bar after the save was changed from outside.</summary>
        void CheatRefresh();
    }

    /// <summary>
    /// Root-scope meeting point between the Gameplay screen (scene scope) and the cheat menu (root): the screen attaches
    /// itself while it is up, the menu calls through. Registered in every build; nothing calls it without the menu.
    /// </summary>
    public sealed class GameplayCheatHook
    {
        private IGameplayCheatTarget _target;

        public bool InGameplay => _target != null;

        public void Attach(IGameplayCheatTarget target) => _target = target;

        public void Detach(IGameplayCheatTarget target)
        {
            if (ReferenceEquals(_target, target)) _target = null;
        }

        public bool Win() => _target != null && _target.CheatWin();
        public bool Lose() => _target != null && _target.CheatLose();
        public void Refresh() => _target?.CheatRefresh();
    }
}
