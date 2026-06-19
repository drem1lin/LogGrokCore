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