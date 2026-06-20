using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
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

        public static string SettingsFileName => PathHelpers.GetLocalFilePath("appsettings.yaml");

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
            var builder = new ConfigurationBuilder()
                .AddYamlFile(SettingsFileName, true, true);

            var settings = new ApplicationSettings();

            var configuration = builder.Build();
            configuration.GetSection("Settings").Bind(settings);

            ChangeToken.OnChange(() => configuration.GetReloadToken(), () =>
            {
                var newSettings = new ApplicationSettings();
                configuration.GetSection("Settings").Bind(newSettings);
                settings.ColorSettings = newSettings.ColorSettings;
                settings.LogFormats = newSettings.LogFormats;
            });

            return settings;
        }

        private ApplicationSettings()
        {
        }
    }
}