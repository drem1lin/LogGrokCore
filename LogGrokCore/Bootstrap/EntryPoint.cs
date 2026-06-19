using System;
using System.IO;
using System.Linq;
using LogGrokCore.Diagnostics;

namespace LogGrokCore.Bootstrap
{
    static class EntryPoint
    {
        [STAThread]
        public static void Main(string[] args)
        {
            var command = args.SingleOrDefault();

            ConfigureErrorReporting(command);

            if (IsNeedStopExecution(command))
                return;

            var fullPaths = args.Select(Path.GetFullPath).ToArray();
            var manager = new SingleInstanceManager();
            manager.Run(fullPaths);
        }

        private static bool IsNeedStopExecution(string? command)
        {
            return command == CrashDumpConfiguration.EnableWerOnlyArgument
                   || command == CrashDumpConfiguration.DisableWerOnlyArgument;
        }

        private static void ConfigureErrorReporting(string? command)
        {
            var settings = ApplicationSettings.Instance().DebugSettings;
            var isEnabled = CrashDumpConfiguration.IsEnabled();
            var settingsChanged = CrashDumpConfiguration.SettingsChanged(settings.MaxDumpsCount);

            var isNeedEnableWer = (settings.EnableCrashDumps && !isEnabled)
                                  || command == CrashDumpConfiguration.EnableWerOnlyArgument
                                  || settingsChanged;
            var isNeedDisableWer = (!settings.EnableCrashDumps && isEnabled)
                                   || command == CrashDumpConfiguration.DisableWerOnlyArgument;

            if (isNeedEnableWer)
            {
                EvaluateIfNeed(
                    () => CrashDumpConfiguration.Enable(
                        HomeDirectoryPathProvider.GetDirectoryFullPath("Dumps"),
                        settings.MaxDumpsCount),
                    enable: true,
                    command);
            }
            else if (isNeedDisableWer)
            {
                EvaluateIfNeed(CrashDumpConfiguration.Disable, enable: false, command);
            }
        }

        private static void EvaluateIfNeed(Action configure, bool enable, string? currentCommand)
        {
            try
            {
                configure();
            }
            catch (UnauthorizedAccessException)
            {
                if (IsNeedStopExecution(currentCommand))
                    return; // already running elevated; give up rather than loop

                CrashDumpConfiguration.RequestConfigureElevated(enable);
            }
        }
    }
}
