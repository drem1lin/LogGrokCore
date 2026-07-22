using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LogGrokCore.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokCore.Tests
{
    [TestClass]
    public class ProfileSettingsTests
    {
        private static ApplicationSettings LoadDefaultSettings()
        {
            var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                "..", "..", "..", "..", "LogGrokCore", "appsettings.default.yaml"));
            return ApplicationSettings.BuildFromFile(path,
                error => Assert.Fail(error.ToString()), out _);
        }

        [TestMethod]
        public void DefaultProfiles_OwnIndependentSettings_NoTopLevelFallbackNeeded()
        {
            var settings = LoadDefaultSettings();
            var profiles = settings.GetProfiles();

            var kaspersky = profiles.Single(profile => profile.Name == "Kaspersky");
            var venn = profiles.Single(profile => profile.Name == "Venn");
            var plainText = profiles.Single(profile => profile.Name == "Plain text");

            Assert.AreEqual(0, settings.ColorSettings.Rules.Length,
                "The default template must not keep profile colors at the top level.");
            Assert.AreEqual(0, settings.LogFormats.Length,
                "The default template must not keep profile formats at the top level.");
            Assert.AreNotSame(kaspersky.ColorSettings, venn.ColorSettings);
            Assert.AreNotSame(kaspersky.LogFormats, venn.LogFormats);
            Assert.AreEqual(8, kaspersky.ColorSettings!.Rules.Length);
            Assert.AreEqual(6, kaspersky.LogFormats!.Length);
            Assert.AreEqual(6, venn.ColorSettings!.Rules.Length);
            Assert.AreEqual(1, venn.LogFormats!.Length);
            Assert.AreEqual(0, plainText.ColorSettings!.Rules.Length);
            Assert.AreEqual(1, plainText.LogFormats!.Length);
        }

        [TestMethod]
        public void DefaultProfiles_AllParsingAndHighlightRegexesAreValid()
        {
            foreach (var profile in LoadDefaultSettings().GetProfiles())
            {
                foreach (var format in profile.LogFormats!)
                    Assert.IsTrue(format.IsCorrect(),
                        $"Invalid log format in profile '{profile.Name}': {format.Regex}");

                foreach (var rule in profile.ColorSettings!.Rules)
                {
                    try
                    {
                        _ = new Regex(rule.RegexString);
                    }
                    catch (Exception error)
                    {
                        Assert.Fail($"Invalid color regex in profile '{profile.Name}': " +
                                    $"{rule.RegexString}. {error.Message}");
                    }
                }
            }
        }

        [TestMethod]
        public void VennFormat_ParsesDriverLineAndExtractsIndexedFields()
        {
            var venn = LoadDefaultSettings().GetProfiles().Single(profile => profile.Name == "Venn");
            var format = venn.LogFormats!.Single();
            var regex = new Regex(format.Regex);
            const string line =
                "2026-07-22 9:01:02.123 - [ERRO] - [123:456:789:M:U] - " +
                "[agent.exe] - [irql 2] - [driver.cpp:42] - operation failed";

            var match = regex.Match(line);

            Assert.IsTrue(match.Success);
            Assert.AreEqual(line.Length, match.Length, "The format must consume the complete line.");
            Assert.AreEqual("2026-07-22 9:01:02.123", match.Groups["Time"].Value);
            Assert.AreEqual("ERRO", match.Groups["Level"].Value);
            Assert.AreEqual("123", match.Groups["Sid"].Value);
            Assert.AreEqual("456", match.Groups["Pid"].Value);
            Assert.AreEqual("789", match.Groups["Tid"].Value);
            Assert.AreEqual("agent.exe", match.Groups["Process"].Value);
            Assert.AreEqual("2", match.Groups["Irql"].Value);
            Assert.AreEqual("driver.cpp:42", match.Groups["Source"].Value);
            Assert.AreEqual("operation failed", match.Groups["Message"].Value);
            CollectionAssert.AreEqual(
                new[] { "Level", "Sid", "Pid", "Tid", "Process", "Source", "Irql" },
                format.IndexedFields);
        }

        [TestMethod]
        public void PlainTextProfile_MatchesWholeLineInSingleColumnWithoutHighlights()
        {
            var plainText = LoadDefaultSettings().GetProfiles()
                .Single(profile => profile.Name == "Plain text");
            var format = plainText.LogFormats!.Single();
            const string line = "arbitrary text [WARN] 123";

            var match = Regex.Match(line, format.Regex);

            Assert.IsTrue(match.Success);
            Assert.AreEqual(line, match.Groups["Text"].Value);
            CollectionAssert.AreEqual(new[] { "Text" }, format.FieldNames);
            Assert.AreEqual(0, plainText.ColorSettings!.Rules.Length);
        }

        [TestMethod]
        public void VennHighlightRules_ApplyExpectedForegroundAndBackgroundColors()
        {
            var venn = LoadDefaultSettings().GetProfiles().Single(profile => profile.Name == "Venn");
            var colors = new Colors.ColorSettings(venn.ColorSettings!);

            var critical = colors.Rules.Single(rule => rule.IsMatch("[CRIT] unrecoverable failure"));
            var error = colors.Rules.Single(rule => rule.IsMatch("[ERRO] request failed"));
            var warning = colors.Rules.Single(rule => rule.IsMatch("[WARN] retrying"));
            var trace = colors.Rules.Single(rule => rule.IsMatch("[TRAC] details"));

            Assert.IsNotNull(critical.Background);
            Assert.IsNull(critical.Foreground);
            Assert.IsNotNull(error.Foreground);
            Assert.IsNotNull(warning.Foreground);
            Assert.IsNotNull(trace.Foreground);
        }

        [TestMethod]
        public void UnknownSelectedProfile_FallsBackToFirstConfiguredProfile()
        {
            var settings = LoadDefaultSettings();
            settings.SelectedProfile = "does-not-exist";

            Assert.AreEqual("Kaspersky", settings.GetSelectedProfile().Name);
        }

        [TestMethod]
        public void DisabledLegacyInheritance_LeavesOmittedSectionsEmpty()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), "loggrok-profile-" + Guid.NewGuid() + ".yaml");
            try
            {
                File.WriteAllText(tempFile,
                    "Settings:\n" +
                    "  ColorSettings:\n" +
                    "    Rules:\n" +
                    "      - RegexString: inherited-color\n" +
                    "  LogFormats:\n" +
                    "    - Regex: ^(?<Inherited>.*)\n" +
                    "  Profiles:\n" +
                    "    - Name: Empty\n" +
                    "      InheritLegacySettings: false\n");

                var settings = ApplicationSettings.BuildFromFile(tempFile,
                    error => Assert.Fail(error.ToString()), out _);
                var profile = settings.GetProfiles().Single();

                Assert.AreEqual(0, profile.ColorSettings!.Rules.Length);
                Assert.AreEqual(0, profile.LogFormats!.Length);
            }
            finally
            {
                try { File.Delete(tempFile); } catch { /* best effort */ }
            }
        }
    }
}
