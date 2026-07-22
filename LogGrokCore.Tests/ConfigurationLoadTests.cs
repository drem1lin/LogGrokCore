using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LogGrokCore;
using LogGrokCore.Bootstrap;

namespace LogGrokCore.Tests
{
    [TestClass]
    public class ConfigurationLoadTests
    {
        private string _tempDir = string.Empty;

        [TestInitialize]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "loggrok-cfgtest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
        }

        private string WriteFile(string content)
        {
            var path = Path.Combine(_tempDir, "appsettings.yaml");
            File.WriteAllText(path, content);
            return path;
        }

        [TestMethod]
        public void BuildFromFile_MalformedYaml_RoutesToHandler_DoesNotThrow()
        {
            // 'IndexedFields' is indented inconsistently relative to 'Regex' — the exact
            // failure from the original bug report.
            var path = WriteFile(
                "Settings:\n" +
                "  LogFormats:\n" +
                "    - Regex: ^(?<Time>\\d{4})\\s-\\s(?<Message>.*)\n" +
                "       IndexedFields:\n" +
                "         - Level\n");

            Exception captured = null;
            var settings = ApplicationSettings.BuildFromFile(path, e => captured = e, out _);

            Assert.IsNotNull(captured, "Malformed YAML should route to the load-error handler instead of throwing.");
            Assert.IsNotNull(settings, "A settings instance should still be returned (with defaults).");
        }

        [TestMethod]
        public void BuildFromFile_ValidYaml_BindsSettings_NoError()
        {
            var path = WriteFile(
                "Settings:\n" +
                "  Language: ru\n" +
                "  LogFormats:\n" +
                "    - Regex: ^(?<Message>.*)\n" +
                "      IndexedFields:\n" +
                "        - Message\n");

            var errorRaised = false;
            var settings = ApplicationSettings.BuildFromFile(path, _ => errorRaised = true, out _);

            Assert.IsFalse(errorRaised, "Valid YAML must not trigger the load-error handler.");
            Assert.AreEqual("ru", settings.Language);
            Assert.AreEqual(1, settings.LogFormats.Length);
        }

        [TestMethod]
        public void BuildFromFile_ProfilesBindIndependentFormatsAndColors()
        {
            var path = WriteFile(
                "Settings:\n" +
                "  SelectedProfile: Json\n" +
                "  Profiles:\n" +
                "    - Name: Classic\n" +
                "      ColorSettings:\n" +
                "        Rules:\n" +
                "          - RegexString: ERROR\n" +
                "            ForegroundColor: Red\n" +
                "      LogFormats:\n" +
                "        - Regex: ^(?<Classic>.*)\n" +
                "    - Name: Json\n" +
                "      ColorSettings:\n" +
                "        Rules:\n" +
                "          - RegexString: warning\n" +
                "            ForegroundColor: Orange\n" +
                "      LogFormats:\n" +
                "        - Regex: ^(?<Json>.*)\n");

            var settings = ApplicationSettings.BuildFromFile(path, error => Assert.Fail(error.ToString()), out _);
            var profiles = settings.GetProfiles();

            Assert.AreEqual(2, profiles.Count);
            Assert.AreEqual("Classic", profiles[0].Name);
            Assert.AreEqual("ERROR", profiles[0].ColorSettings!.Rules[0].RegexString);
            Assert.AreEqual("Classic", profiles[0].LogFormats![0].FieldNames[0]);
            Assert.AreEqual("Json", settings.GetSelectedProfile().Name);
            Assert.AreEqual("warning", settings.GetSelectedProfile().ColorSettings!.Rules[0].RegexString);
        }

        [TestMethod]
        public void GetProfiles_LegacySettingsBecomeDefaultProfile()
        {
            var path = WriteFile(
                "Settings:\n" +
                "  ColorSettings:\n" +
                "    Rules:\n" +
                "      - RegexString: legacy-color\n" +
                "  LogFormats:\n" +
                "    - Regex: ^(?<Legacy>.*)\n");

            var settings = ApplicationSettings.BuildFromFile(path, error => Assert.Fail(error.ToString()), out _);
            var profile = settings.GetSelectedProfile();

            Assert.AreEqual(ApplicationSettings.LegacyProfileName, profile.Name);
            Assert.AreEqual("legacy-color", profile.ColorSettings!.Rules[0].RegexString);
            Assert.AreEqual("Legacy", profile.LogFormats![0].FieldNames[0]);
        }

        [TestMethod]
        public void GetProfiles_ProfileMayInheritLegacySections()
        {
            var path = WriteFile(
                "Settings:\n" +
                "  ColorSettings:\n" +
                "    Rules:\n" +
                "      - RegexString: shared-color\n" +
                "  LogFormats:\n" +
                "    - Regex: ^(?<Shared>.*)\n" +
                "  Profiles:\n" +
                "    - Name: Default\n");

            var settings = ApplicationSettings.BuildFromFile(path, error => Assert.Fail(error.ToString()), out _);
            var profile = settings.GetSelectedProfile();

            Assert.AreEqual("shared-color", profile.ColorSettings!.Rules[0].RegexString);
            Assert.AreEqual("Shared", profile.LogFormats![0].FieldNames[0]);
        }

        [TestMethod]
        public void DefaultTemplate_ContainsKasperskyVennAndPlainTextProfiles()
        {
            var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                "..", "..", "..", "..", "LogGrokCore", "appsettings.default.yaml"));

            var settings = ApplicationSettings.BuildFromFile(path,
                error => Assert.Fail(error.ToString()), out _);
            var profiles = settings.GetProfiles();

            Assert.AreEqual(ApplicationSettings.CurrentSettingsVersion, settings.SettingsVersion);
            Assert.AreEqual(3, profiles.Count);
            Assert.AreEqual("Kaspersky", settings.GetSelectedProfile().Name);
            Assert.AreEqual(8, profiles[0].ColorSettings!.Rules.Length);
            Assert.AreEqual(6, profiles[0].LogFormats!.Length);
            Assert.AreEqual("Venn", profiles[1].Name);
            CollectionAssert.AreEqual(
                new[] { "Time", "Level", "Context", "Sid", "Pid", "Tid", "Process", "Irql", "Source", "Message" },
                profiles[1].LogFormats![0].FieldNames);
            CollectionAssert.AreEqual(
                new[] { "Level", "Sid", "Pid", "Tid", "Process", "Source", "Irql" },
                profiles[1].LogFormats[0].IndexedFields);
            Assert.AreEqual(6, profiles[1].ColorSettings!.Rules.Length);
            Assert.AreEqual("Plain text", profiles[2].Name);
            CollectionAssert.AreEqual(new[] { "Text" }, profiles[2].LogFormats![0].FieldNames);
            Assert.AreEqual(0, profiles[2].ColorSettings!.Rules.Length);
        }

        [TestMethod]
        public void BuildFromFile_MissingFile_NoError_UsesDefaults()
        {
            var path = Path.Combine(_tempDir, "does-not-exist.yaml");

            var errorRaised = false;
            var settings = ApplicationSettings.BuildFromFile(path, _ => errorRaised = true, out _);

            Assert.IsFalse(errorRaised, "A missing (optional) file is not a corruption error.");
            Assert.IsNotNull(settings);
        }

        [TestMethod]
        public void BuildReport_ContainsPath_RootCause_AndEverySupportedLanguage()
        {
            const string path = @"C:\some\appsettings.yaml";
            var error = new FormatException("outer",
                new InvalidOperationException("invalid mapping at line 4"));

            var (caption, message) = ConfigurationLoadFailure.BuildReport(path, error);

            Assert.IsFalse(string.IsNullOrWhiteSpace(caption));
            StringAssert.Contains(message, path);
            StringAssert.Contains(message, "invalid mapping at line 4", "Root-cause detail should be shown.");

            // The advice is shown in every supported language (the preferred one is unknown
            // because it lives in the file that failed to load).
            foreach (var language in LogGrokCore.Localization.TranslationSource.AvailableLanguages)
                StringAssert.Contains(message, language.DisplayName,
                    $"Message should include the {language.Code} block.");

            // Spot-check actual translated content, not just the labels.
            StringAssert.Contains(message, "spaces");      // English advice
            StringAssert.Contains(message, "пробелы");     // Russian advice
        }
    }
}
