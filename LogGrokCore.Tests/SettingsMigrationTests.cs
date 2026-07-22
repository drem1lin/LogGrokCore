using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LogGrokCore;

namespace LogGrokCore.Tests
{
    [TestClass]
    public class SettingsMigrationTests
    {
        private string _tempDir = string.Empty;
        private string _livePath = string.Empty;
        private string _defaultsPath = string.Empty;
        private string _legacyPath = string.Empty;
        private string BackupPath =>
            _livePath + $".pre-{ApplicationSettings.CurrentSettingsVersion}.bak";

        private const string DefaultsContent = "Settings:\n  Language: en\n";
        private const string LegacyContent = "Settings:\n  Language: ru\n";

        [TestInitialize]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "loggrok-migtest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            // Live file lives in a nested subdirectory that does not exist yet, mirroring the
            // real ProgramData layout that migration must create on demand.
            _livePath = Path.Combine(_tempDir, "live", "appsettings.yaml");
            _defaultsPath = Path.Combine(_tempDir, "appsettings.default.yaml");
            _legacyPath = Path.Combine(_tempDir, "appsettings.yaml");
            File.WriteAllText(_defaultsPath, DefaultsContent);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
        }

        private void Migrate(Func<SettingsMigration.PromptResult> prompt) =>
            SettingsMigration.Migrate(_livePath, _defaultsPath, _legacyPath, prompt);

        private static SettingsMigration.PromptResult ShouldNotPrompt() =>
            throw new AssertFailedException("The reset prompt must not be shown.");

        private void WriteProfileDefaults()
        {
            File.WriteAllText(_defaultsPath,
                "Settings:\n" +
                $"  SettingsVersion: {ApplicationSettings.CurrentSettingsVersion}\n" +
                "  SelectedProfile: Kaspersky\n" +
                "  Profiles:\n" +
                "    - Name: Kaspersky\n" +
                "      ColorSettings:\n" +
                "        Rules:\n" +
                "          - RegexString: kaspersky\n" +
                "      LogFormats:\n" +
                "        - Regex: ^(?<Kaspersky>.*)\n" +
                "    - Name: Venn\n" +
                "      ColorSettings:\n" +
                "        Rules:\n" +
                "          - RegexString: venn\n" +
                "      LogFormats:\n" +
                "        - Regex: ^(?<Venn>.*)\n" +
                "    - Name: Plain text\n" +
                "      InheritLegacySettings: false\n" +
                "      ColorSettings:\n" +
                "        Rules: []\n" +
                "      LogFormats:\n" +
                "        - Regex: ^(?<Text>.*)\n");
        }

        private string WritePreProfileLiveSettings(string? settingsVersion = null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_livePath)!);
            var versionLine = settingsVersion == null ? string.Empty : $"  SettingsVersion: {settingsVersion}\n";
            var content =
                "Settings:\n" +
                versionLine +
                "  Language: ru\n" +
                "  DebugSettings:\n" +
                "    EnableCrashDumps: true\n" +
                "    MaxDumpsCount: 7\n" +
                "  ViewSettings:\n" +
                "    BigLine: break\n" +
                "    BigLineSize: 1234\n" +
                "  ColorSettings:\n" +
                "    Rules:\n" +
                "      - RegexString: user-highlight\n" +
                "        ForegroundColor: Blue\n" +
                "  LogFormats:\n" +
                "    - Regex: ^(?<UserField>.*)\n" +
                "      XorMask: 239\n" +
                "      IndexedFields:\n" +
                "        - UserField\n" +
                "      Transformations:\n" +
                "        - ^(?<Decoded>.*)\n";
            File.WriteAllText(_livePath, content);
            return content;
        }

        [TestMethod]
        public void LiveFileExists_LeftUntouched_NoPrompt()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_livePath)!);
            File.WriteAllText(_livePath, "existing");
            File.WriteAllText(_legacyPath, LegacyContent);

            Migrate(ShouldNotPrompt);

            Assert.AreEqual("existing", File.ReadAllText(_livePath), "An existing live file must not be overwritten.");
        }

        [TestMethod]
        public void FreshInstall_NoLiveNoLegacy_SeedsDefaults_NoPrompt()
        {
            Migrate(ShouldNotPrompt);

            Assert.IsTrue(File.Exists(_livePath));
            Assert.AreEqual(DefaultsContent, File.ReadAllText(_livePath));
        }

        [TestMethod]
        public void Upgrade_KeepExisting_MovesLegacyToLive_AndRemovesLegacy()
        {
            File.WriteAllText(_legacyPath, LegacyContent);

            Migrate(() => SettingsMigration.PromptResult.KeepExisting);

            Assert.AreEqual(LegacyContent, File.ReadAllText(_livePath), "User's legacy settings must be preserved.");
            Assert.IsFalse(File.Exists(_legacyPath), "Legacy file should be removed after migration (best effort).");
        }

        [TestMethod]
        public void Upgrade_ResetToDefaults_SeedsDefaults_AndRemovesLegacy()
        {
            File.WriteAllText(_legacyPath, LegacyContent);

            Migrate(() => SettingsMigration.PromptResult.ResetToDefaults);

            Assert.AreEqual(DefaultsContent, File.ReadAllText(_livePath), "Reset must replace settings with defaults.");
            Assert.IsFalse(File.Exists(_legacyPath));
        }

        [TestMethod]
        public void ProfileUpgrade_NoVersion_CopiesCurrentSettingsToSelectedPredefinedProfile()
        {
            WriteProfileDefaults();
            var original = WritePreProfileLiveSettings();

            var changed = SettingsMigration.UpgradeProfiles(_livePath, _defaultsPath);

            Assert.IsTrue(changed);
            Assert.AreEqual(original, File.ReadAllText(BackupPath));

            var settings = ApplicationSettings.BuildFromFile(_livePath,
                error => Assert.Fail(error.ToString()), out _);
            var profiles = settings.GetProfiles();
            Assert.AreEqual(ApplicationSettings.CurrentSettingsVersion, settings.SettingsVersion);
            Assert.AreEqual("Predefined", settings.SelectedProfile);
            Assert.AreEqual("Predefined", settings.GetSelectedProfile().Name);
            CollectionAssert.AreEqual(
                new[] { "Predefined", "Kaspersky", "Venn", "Plain text" },
                Array.ConvertAll(profiles.ToArray(), profile => profile.Name));

            var predefined = profiles[0];
            Assert.AreEqual("user-highlight", predefined.ColorSettings!.Rules[0].RegexString);
            Assert.AreEqual("Blue", predefined.ColorSettings.Rules[0].ForegroundColor);
            Assert.AreEqual("UserField", predefined.LogFormats![0].FieldNames[0]);
            CollectionAssert.AreEqual(new[] { "UserField" }, predefined.LogFormats[0].IndexedFields);
            Assert.AreEqual(239, predefined.LogFormats[0].XorMask);
            CollectionAssert.AreEqual(new[] { "^(?<Decoded>.*)" },
                predefined.LogFormats[0].Transformations);

            Assert.AreEqual("ru", settings.Language);
            Assert.IsTrue(settings.DebugSettings.EnableCrashDumps);
            Assert.AreEqual(7, settings.DebugSettings.MaxDumpsCount);
            Assert.AreEqual(ViewSettings.ViewBigLine.Break, settings.ViewSettings.BigLine);
            Assert.AreEqual(1234, settings.ViewSettings.BigLineSize);
            Assert.AreEqual(0, settings.ColorSettings.Rules.Length);
            Assert.AreEqual(0, settings.LogFormats.Length);
        }

        [TestMethod]
        public void ProfileUpgrade_ExactVersion135_IsMigratedAndIsIdempotent()
        {
            WriteProfileDefaults();
            _ = WritePreProfileLiveSettings("1.3.5");

            Assert.IsTrue(SettingsMigration.UpgradeProfiles(_livePath, _defaultsPath));
            var once = File.ReadAllText(_livePath);
            var backup = File.ReadAllText(BackupPath);

            Assert.IsFalse(SettingsMigration.UpgradeProfiles(_livePath, _defaultsPath));
            Assert.AreEqual(once, File.ReadAllText(_livePath));
            Assert.AreEqual(backup, File.ReadAllText(BackupPath));
        }

        [TestMethod]
        public void ProfileUpgrade_FourPartVersion1350_IsAlsoMigrated()
        {
            WriteProfileDefaults();
            _ = WritePreProfileLiveSettings("1.3.5.0");

            Assert.IsTrue(SettingsMigration.UpgradeProfiles(_livePath, _defaultsPath));
            var settings = ApplicationSettings.BuildFromFile(_livePath,
                error => Assert.Fail(error.ToString()), out _);
            Assert.AreEqual("Predefined", settings.GetSelectedProfile().Name);
        }

        [TestMethod]
        public void ProfileUpgrade_Version136_IsNotChanged()
        {
            WriteProfileDefaults();
            var original = WritePreProfileLiveSettings("1.3.6");

            var changed = SettingsMigration.UpgradeProfiles(_livePath, _defaultsPath);

            Assert.IsFalse(changed);
            Assert.AreEqual(original, File.ReadAllText(_livePath));
            Assert.IsFalse(File.Exists(BackupPath));
        }

        [TestMethod]
        public void ProfileUpgrade_UnversionedProfileAwareFile_IsNotReinterpretedAsLegacy()
        {
            WriteProfileDefaults();
            Directory.CreateDirectory(Path.GetDirectoryName(_livePath)!);
            const string original =
                "Settings:\n" +
                "  SelectedProfile: Custom\n" +
                "  Profiles:\n" +
                "    - Name: Custom\n" +
                "      LogFormats:\n" +
                "        - Regex: ^(?<Custom>.*)\n";
            File.WriteAllText(_livePath, original);

            Assert.IsFalse(SettingsMigration.UpgradeProfiles(_livePath, _defaultsPath));
            Assert.AreEqual(original, File.ReadAllText(_livePath));
        }

        [TestMethod]
        public void ProfileUpgrade_InvalidDefaults_LeavesLiveFileUntouchedWithoutBackup()
        {
            var original = WritePreProfileLiveSettings();
            File.WriteAllText(_defaultsPath, "Settings:\n  Profiles: not-a-list\n");

            Assert.IsFalse(SettingsMigration.UpgradeProfiles(_livePath, _defaultsPath));
            Assert.AreEqual(original, File.ReadAllText(_livePath));
            Assert.IsFalse(File.Exists(BackupPath));
            Assert.IsFalse(File.Exists(_livePath + ".migration.tmp"));
        }

        [TestMethod]
        public void ProfileUpgrade_InvalidLiveSettings_LeavesFileUntouchedWithoutBackup()
        {
            WriteProfileDefaults();
            Directory.CreateDirectory(Path.GetDirectoryName(_livePath)!);
            const string original = "Settings:\n  LogFormats:\n    - Regex: ok\n       InvalidIndent: true\n";
            File.WriteAllText(_livePath, original);

            Assert.IsFalse(SettingsMigration.UpgradeProfiles(_livePath, _defaultsPath));
            Assert.AreEqual(original, File.ReadAllText(_livePath));
            Assert.IsFalse(File.Exists(BackupPath));
            Assert.IsFalse(File.Exists(_livePath + ".migration.tmp"));
        }
    }
}
