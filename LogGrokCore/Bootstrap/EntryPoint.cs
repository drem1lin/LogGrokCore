using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using LogGrokCore.Diagnostics;
using LogGrokCore.Localization;

namespace LogGrokCore.Bootstrap
{
    static class EntryPoint
    {
        [STAThread]
        public static void Main(string[] args)
        {
            var command = args.SingleOrDefault();

            // Make sure the live settings file exists in its writable ProgramData location
            // before anything reads the settings (ConfigureErrorReporting below does).
            SettingsMigration.EnsureSettingsFilePresent(PromptResetSettings);

            ConfigureErrorReporting(command);

            if (IsNeedStopExecution(command))
                return;

            var fullPaths = args.Select(Path.GetFullPath).ToArray();
            var manager = new SingleInstanceManager();
            manager.Run(fullPaths);
        }

        /// <summary>
        /// Asked only when upgrading from a version that stored settings next to the executable.
        /// The stored language isn't loaded yet, so the prompt uses the OS UI language.
        /// </summary>
        private static SettingsMigration.PromptResult PromptResetSettings()
        {
            TranslationSource.Instance.SetCulture(CultureInfo.CurrentUICulture.Name);
            var source = TranslationSource.Instance;

            var result = MessageBox.Show(
                source["Migration_ResetMessage"],
                source["Migration_ResetTitle"],
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            return result == MessageBoxResult.Yes
                ? SettingsMigration.PromptResult.ResetToDefaults
                : SettingsMigration.PromptResult.KeepExisting;
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
                // Ensure this user's dump folder exists, but register the unexpanded template so
                // WER routes each user's dumps into their own subfolder under one HKLM key.
                HomeDirectoryPathProvider.GetDiagnosticsDirectory("Dumps");
                EvaluateIfNeed(
                    () => CrashDumpConfiguration.Enable(
                        HomeDirectoryPathProvider.DumpFolderTemplate,
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
