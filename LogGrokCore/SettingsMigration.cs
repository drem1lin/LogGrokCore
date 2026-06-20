using System;
using System.Diagnostics;
using System.IO;

namespace LogGrokCore
{
    /// <summary>
    /// The live settings file now lives per-user under %LOCALAPPDATA% (see
    /// <see cref="HomeDirectoryPathProvider"/>) so it can be edited without administrator rights.
    /// Older versions kept a user-editable <c>appsettings.yaml</c> next to the executable in
    /// Program Files. This performs the one-time move on first launch and seeds defaults for a
    /// fresh install.
    /// </summary>
    public static class SettingsMigration
    {
        public enum PromptResult
        {
            ResetToDefaults,
            KeepExisting
        }

        /// <summary>Read-only defaults template shipped next to the executable.</summary>
        public const string DefaultsFileName = "appsettings.default.yaml";

        /// <summary>Legacy live settings file location (next to the exe, pre-ProgramData).</summary>
        public const string LegacyFileName = "appsettings.yaml";

        /// <summary>
        /// Ensures the live settings file exists in its ProgramData location, migrating from
        /// the legacy Program Files location if needed. Must run before the settings are first
        /// read. <paramref name="promptReset"/> is invoked only when a legacy file is found, to
        /// ask the user whether to reset to defaults or keep their existing settings.
        /// </summary>
        public static void EnsureSettingsFilePresent(Func<PromptResult> promptReset)
        {
            Migrate(
                ApplicationSettings.SettingsFileName,
                PathHelpers.GetLocalFilePath(DefaultsFileName),
                PathHelpers.GetLocalFilePath(LegacyFileName),
                promptReset);
        }

        /// <summary>
        /// Pure migration core (file paths only) so it can be unit-tested without a UI.
        /// Best-effort: any failure is logged, never thrown — a missing live file is tolerated
        /// by the loader (it falls back to built-in defaults).
        /// </summary>
        internal static void Migrate(string livePath, string defaultsPath, string legacyPath,
            Func<PromptResult> promptReset)
        {
            try
            {
                if (File.Exists(livePath))
                    return; // already migrated / normal run

                var liveDir = Path.GetDirectoryName(livePath);
                if (!string.IsNullOrEmpty(liveDir))
                    Directory.CreateDirectory(liveDir);

                if (File.Exists(legacyPath))
                {
                    // Upgrade from a version that stored a user-editable file in Program Files.
                    var keepExisting = promptReset() == PromptResult.KeepExisting;
                    var source = keepExisting ? legacyPath : defaultsPath;
                    SeedFrom(source, defaultsPath, livePath);
                    TryDeleteLegacy(legacyPath);
                }
                else
                {
                    // Fresh install: seed defaults silently.
                    SeedFrom(defaultsPath, defaultsPath, livePath);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Settings migration failed: {ex.Message}");
            }
        }

        private static void SeedFrom(string source, string defaultsPath, string destination)
        {
            if (File.Exists(source))
                File.Copy(source, destination, overwrite: true);
            else if (File.Exists(defaultsPath))
                File.Copy(defaultsPath, destination, overwrite: true);
        }

        private static void TryDeleteLegacy(string legacyPath)
        {
            try
            {
                // Best-effort: Program Files is typically read-only without admin rights, in which
                // case we simply stop reading the legacy file rather than removing it.
                File.Delete(legacyPath);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Could not remove legacy settings file '{legacyPath}': {ex.Message}");
            }
        }
    }
}
