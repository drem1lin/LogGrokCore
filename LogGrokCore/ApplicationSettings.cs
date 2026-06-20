using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using LogGrokCore.Bootstrap;
using LogGrokCore.Colors.Configuration;
using LogGrokCore.Controls.ListControls;
using LogGrokCore.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace LogGrokCore
{
    public class ApplicationSettings
    {
        private static ApplicationSettings? instance;

        /// <summary>
        /// The live, per-user settings file. It lives under %LOCALAPPDATA% so each user has their
        /// own copy and it is always writable without administrator rights; on first run it is
        /// seeded from the read-only defaults template shipped next to the executable
        /// (see <see cref="SettingsMigration"/>).
        /// </summary>
        public static string SettingsFileName =>
            HomeDirectoryPathProvider.GetUserDataFilePath("appsettings.yaml");

        public DebugSettings DebugSettings { get; set; } = new();

        /// <summary>UI language code (e.g. "en", "ru", "pt-BR"). Defaults to English.</summary>
        public string Language { get; set; } = "en";

        public ColorSettings ColorSettings { get; private set; } = new();

        public ViewSettings ViewSettings { get; private set; } = new();

        public LogFormat[] LogFormats { get; set; } =
            Array.Empty<LogFormat>();

        private readonly Dictionary<string, ColumnSettings> _columnSettingsMap = new();
        public ColumnSettings GetColumnSettings(string logFormat)
        {
            if (!_columnSettingsMap.TryGetValue(logFormat, out var columnSettings))
            {
                columnSettings = new ColumnSettings();
                _columnSettingsMap[logFormat] = columnSettings;
            }

            return columnSettings;
        }

        public static ApplicationSettings Instance()
        {
            if (instance == null)
                instance = Load();
            return instance;
        }

        /// <summary>
        /// Updates the in-memory value and persists it back to appsettings.yaml with a
        /// targeted single-line replace, so the surrounding hand-written config (comments,
        /// formatting) is preserved. Best-effort: failures are logged, not thrown.
        /// </summary>
        public static void SetEnableCrashDumps(bool enabled)
        {
            Instance().DebugSettings.EnableCrashDumps = enabled;

            try
            {
                var path = SettingsFileName;
                if (!File.Exists(path))
                    return;

                var lines = File.ReadAllLines(path);
                var regex = new Regex(@"^(\s*EnableCrashDumps\s*:\s*)(?:true|false)\s*$",
                    RegexOptions.IgnoreCase);
                for (var i = 0; i < lines.Length; i++)
                {
                    var match = regex.Match(lines[i]);
                    if (!match.Success)
                        continue;

                    lines[i] = match.Groups[1].Value + (enabled ? "true" : "false");
                    File.WriteAllLines(path, lines);
                    return;
                }

                Trace.TraceWarning("EnableCrashDumps line not found in appsettings.yaml; value not persisted.");
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Failed to persist EnableCrashDumps: {ex.Message}");
            }
        }

        /// <summary>
        /// Persists the selected UI language back to appsettings.yaml. Updates the existing
        /// "Language:" line if present, otherwise inserts one right after "Settings:".
        /// Best-effort: failures are logged, not thrown.
        /// </summary>
        public static void SetLanguage(string languageCode)
        {
            Instance().Language = languageCode;

            try
            {
                var path = SettingsFileName;
                if (!File.Exists(path))
                    return;

                var lines = File.ReadAllLines(path);
                var languageRegex = new Regex(@"^(\s*Language\s*:\s*).*$", RegexOptions.IgnoreCase);
                for (var i = 0; i < lines.Length; i++)
                {
                    if (!languageRegex.IsMatch(lines[i]))
                        continue;

                    lines[i] = languageRegex.Replace(lines[i], "$1" + languageCode);
                    File.WriteAllLines(path, lines);
                    return;
                }

                // No existing line: add one directly under the top-level "Settings:" node.
                var settingsRegex = new Regex(@"^\s*Settings\s*:\s*$", RegexOptions.IgnoreCase);
                for (var i = 0; i < lines.Length; i++)
                {
                    if (!settingsRegex.IsMatch(lines[i]))
                        continue;

                    var newLines = new List<string>(lines);
                    newLines.Insert(i + 1, $"  Language: {languageCode}");
                    File.WriteAllLines(path, newLines);
                    return;
                }

                Trace.TraceWarning("Settings node not found in appsettings.yaml; language not persisted.");
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Failed to persist Language: {ex.Message}");
            }
        }

        private static ApplicationSettings Load()
        {
            var settings = BuildFromFile(SettingsFileName,
                error => ConfigurationLoadFailure.ReportAndExit(SettingsFileName, error),
                out var configuration);

            ChangeToken.OnChange(() => configuration.GetReloadToken(), () =>
            {
                var newSettings = new ApplicationSettings();
                configuration.GetSection("Settings").Bind(newSettings);
                settings.ColorSettings = newSettings.ColorSettings;
                settings.LogFormats = newSettings.LogFormats;
            });

            return settings;
        }

        /// <summary>
        /// Builds settings from a YAML file. A malformed file (e.g. bad YAML indentation
        /// or tabs) must not crash the process: the load/parse failure is routed to
        /// <paramref name="onLoadError"/> — both on initial load and on the reload-on-change
        /// path — instead of throwing. This seam is exposed for testing; production passes a
        /// handler that shows a localized message and exits.
        /// </summary>
        internal static ApplicationSettings BuildFromFile(string path,
            Action<Exception> onLoadError, out IConfigurationRoot configuration)
        {
            var builder = new ConfigurationBuilder().AddYamlFile(path, true, true);

            builder.SetFileLoadExceptionHandler(context =>
            {
                context.Ignore = true;
                onLoadError(context.Exception);
            });

            var settings = new ApplicationSettings();
            configuration = builder.Build();
            configuration.GetSection("Settings").Bind(settings);
            return settings;
        }

        private ApplicationSettings()
        {
        }
    }
}