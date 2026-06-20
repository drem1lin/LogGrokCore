using System;
using System.IO;
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
    }
}
