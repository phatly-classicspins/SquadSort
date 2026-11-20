namespace ClassicSpins.PrototypeFramework.Iap.Unity.Sdk
{
    /// <summary>
    /// The gated-assembly marker (the pf-module/1 <c>define:</c> fact). Its presence in a loaded assembly is
    /// how the Setup Wizard and Doctor check 16 confirm that <c>PF_UNITY_IAP</c> actually armed and this
    /// assembly compiled — the define is never read from scripting-define symbols. Declared here, in the
    /// <c>PF_UNITY_IAP</c>-constrained <c>Sdk</c> role, and nowhere else.
    /// </summary>
    internal static class PfModuleMarker_PF_UNITY_IAP
    {
    }
}
