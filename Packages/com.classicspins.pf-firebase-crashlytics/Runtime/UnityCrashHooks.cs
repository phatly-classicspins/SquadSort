using System;
using UnityEngine;
using ClassicSpins.PrototypeFramework.Application;

namespace ClassicSpins.PrototypeFramework.FirebaseCrashlytics
{
    /// <summary>
    /// The same unhandled-exception surfaces the framework's <c>CrashLogNode</c> hooks (that node lives in the framework's
    /// Infrastructure assembly, which a module may not reference, so its two hooks are restated here): an unhandled
    /// <see cref="AppDomain"/> exception and every <see cref="LogType.Exception"/> log, routed to <see cref="ILog.Error"/>.
    /// </summary>
    public sealed class UnityCrashHooks : ICrashHooks
    {
        private readonly ILog _log;
        private bool _hooked;

        public UnityCrashHooks(ILog log) => _log = log;

        public void Install()
        {
            if (_hooked) return;
            _hooked = true;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandled;
            UnityEngine.Application.logMessageReceivedThreaded += OnUnityLog;
        }

        private void OnDomainUnhandled(object sender, UnhandledExceptionEventArgs e) =>
            Emit("[CrashLog] Unhandled exception: " + (e.ExceptionObject as Exception));

        private void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception)
                Emit("[CrashLog] Captured exception: " + condition + "\n" + stackTrace);
        }

        private void Emit(string message)
        {
            if (_log != null) _log.Error(message);
            else Debug.LogError(message);
        }
    }
}
