using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using YamlDotNet.RepresentationModel;

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

        internal static readonly Version LastPreProfileVersion = new(1, 3, 5);

        /// <summary>
        /// Ensures the live settings file exists in its ProgramData location, migrating from
        /// the legacy Program Files location if needed. Must run before the settings are first
        /// read. <paramref name="promptReset"/> is invoked only when a legacy file is found, to
        /// ask the user whether to reset to defaults or keep their existing settings.
        /// </summary>
        public static void EnsureSettingsFilePresent(Func<PromptResult> promptReset)
        {
            var livePath = ApplicationSettings.SettingsFileName;
            var defaultsPath = PathHelpers.GetLocalFilePath(DefaultsFileName);
            Migrate(livePath, defaultsPath, PathHelpers.GetLocalFilePath(LegacyFileName), promptReset);
            UpgradeProfiles(livePath, defaultsPath);
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

        /// <summary>
        /// Upgrades settings written by application versions through 1.3.5. Their top-level
        /// ColorSettings and LogFormats become the independent, selected "Predefined" profile;
        /// profiles shipped by the new defaults are appended without replacing user values.
        /// A backup is kept next to the live file before the atomic replacement.
        /// </summary>
        internal static bool UpgradeProfiles(string livePath, string defaultsPath)
        {
            if (!File.Exists(livePath) || !File.Exists(defaultsPath))
                return false;

            var tempPath = livePath + ".migration.tmp";
            try
            {
                var liveYaml = LoadYaml(livePath);
                var liveSettings = GetSettings(liveYaml);
                if (!NeedsProfileUpgrade(liveSettings))
                    return false;

                var defaultsYaml = LoadYaml(defaultsPath);
                var defaultSettings = GetSettings(defaultsYaml);
                if (!TryGet(defaultSettings, "Profiles", out var defaultProfilesNode) ||
                    defaultProfilesNode is not YamlSequenceNode defaultProfiles)
                    throw new InvalidDataException("Default settings do not contain a Profiles sequence.");

                var migratedProfiles = new YamlSequenceNode();
                var predefined = new YamlMappingNode
                {
                    { "Name", "Predefined" },
                    { "InheritLegacySettings", "false" }
                };

                MoveSectionToProfile(liveSettings, predefined, "ColorSettings");
                MoveSectionToProfile(liveSettings, predefined, "LogFormats");
                migratedProfiles.Add(predefined);

                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Predefined" };
                foreach (var profile in defaultProfiles.Children.OfType<YamlMappingNode>())
                {
                    var name = GetScalar(profile, "Name");
                    if (!string.IsNullOrWhiteSpace(name) && names.Add(name))
                        migratedProfiles.Add(CloneNode(profile));
                }

                Set(liveSettings, "Profiles", migratedProfiles);
                Set(liveSettings, "SelectedProfile", new YamlScalarNode("Predefined"));
                Set(liveSettings, "SettingsVersion",
                    new YamlScalarNode(ApplicationSettings.CurrentSettingsVersion));

                WriteYaml(liveYaml, tempPath);
                _ = LoadYaml(tempPath); // Validate the complete generated document before replacing.

                var backupPath = livePath + $".pre-{ApplicationSettings.CurrentSettingsVersion}.bak";
                if (!File.Exists(backupPath))
                    File.Copy(livePath, backupPath);
                File.Move(tempPath, livePath, overwrite: true);
                Trace.TraceInformation($"Settings upgraded to {ApplicationSettings.CurrentSettingsVersion}; " +
                                       $"previous file saved as '{backupPath}'.");
                return true;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Profile settings migration failed: {ex.Message}");
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning($"Could not remove settings migration temp file '{tempPath}': {ex.Message}");
                }
            }
        }

        private static bool NeedsProfileUpgrade(YamlMappingNode settings)
        {
            if (!TryGet(settings, "SettingsVersion", out var versionNode))
            {
                // Profile-aware development builds did not yet stamp a schema version. Do not
                // reinterpret those files as <=1.3.5 merely because the marker is absent.
                return !TryGet(settings, "Profiles", out _);
            }

            var versionText = (versionNode as YamlScalarNode)?.Value;
            if (!Version.TryParse(versionText?.Split('-')[0], out var version))
                throw new InvalidDataException($"Invalid SettingsVersion '{versionText}'.");
            var normalizedVersion = new Version(version.Major, version.Minor,
                version.Build < 0 ? 0 : version.Build);
            return normalizedVersion <= LastPreProfileVersion;
        }

        private static void MoveSectionToProfile(YamlMappingNode settings, YamlMappingNode profile,
            string sectionName)
        {
            var key = FindKey(settings, sectionName);
            if (key == null)
                return;
            var value = settings.Children[key];
            settings.Children.Remove(key);
            profile.Add(sectionName, value);
        }

        private static YamlStream LoadYaml(string path)
        {
            using var reader = File.OpenText(path);
            var yaml = new YamlStream();
            yaml.Load(reader);
            if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode)
                throw new InvalidDataException($"Settings file '{path}' must contain one YAML mapping.");
            return yaml;
        }

        private static YamlMappingNode GetSettings(YamlStream yaml)
        {
            var root = (YamlMappingNode)yaml.Documents[0].RootNode;
            if (!TryGet(root, "Settings", out var settingsNode) || settingsNode is not YamlMappingNode settings)
                throw new InvalidDataException("Settings file does not contain a Settings mapping.");
            return settings;
        }

        private static YamlNode CloneNode(YamlNode node)
        {
            var stream = new YamlStream(new YamlDocument(node));
            using var writer = new StringWriter();
            stream.Save(writer, assignAnchors: false);
            using var reader = new StringReader(writer.ToString());
            var clone = new YamlStream();
            clone.Load(reader);
            return clone.Documents[0].RootNode;
        }

        private static void WriteYaml(YamlStream yaml, string path)
        {
            using var writer = File.CreateText(path);
            yaml.Save(writer, assignAnchors: false);
        }

        private static string? GetScalar(YamlMappingNode mapping, string name) =>
            TryGet(mapping, name, out var value) ? (value as YamlScalarNode)?.Value : null;

        private static bool TryGet(YamlMappingNode mapping, string name, out YamlNode value)
        {
            var key = FindKey(mapping, name);
            if (key != null)
            {
                value = mapping.Children[key];
                return true;
            }

            value = null!;
            return false;
        }

        private static YamlNode? FindKey(YamlMappingNode mapping, string name) =>
            mapping.Children.Keys.FirstOrDefault(key =>
                key is YamlScalarNode scalar &&
                string.Equals(scalar.Value, name, StringComparison.OrdinalIgnoreCase));

        private static void Set(YamlMappingNode mapping, string name, YamlNode value)
        {
            var existing = FindKey(mapping, name);
            if (existing != null)
                mapping.Children[existing] = value;
            else
                mapping.Add(name, value);
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
