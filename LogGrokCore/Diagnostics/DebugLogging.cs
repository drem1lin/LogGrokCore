using NLog;

namespace LogGrokCore.Diagnostics
{
    /// <summary>
    /// Toggles verbose logging at runtime by lowering/raising the minimum level of
    /// every NLog rule between Info (normal) and Trace (verbose).
    /// </summary>
    public static class DebugLogging
    {
        public static void SetVerbose(bool verbose)
        {
            var config = LogManager.Configuration;
            if (config == null)
                return;

            var minLevel = verbose ? LogLevel.Trace : LogLevel.Info;
            foreach (var rule in config.LoggingRules)
                rule.SetLoggingLevels(minLevel, LogLevel.Fatal);

            LogManager.ReconfigExistingLoggers();
        }
    }
}
