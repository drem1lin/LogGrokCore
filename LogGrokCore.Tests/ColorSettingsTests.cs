using ConfigColorRule = LogGrokCore.Colors.Configuration.ColorRule;
using ConfigColorSettings = LogGrokCore.Colors.Configuration.ColorSettings;
using ColorSettings = LogGrokCore.Colors.ColorSettings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokCore.Tests
{
    [TestClass]
    public class ColorSettingsTests
    {
        [TestMethod]
        public void Rules_AreBuilt_AndMatchText()
        {
            var settings = new ColorSettings(new ConfigColorSettings
            {
                Rules = new[]
                {
                    new ConfigColorRule { RegexString = "ERROR", ForegroundColor = "Red", BackgroundColor = "" }
                }
            });

            Assert.AreEqual(1, settings.Rules.Count);
            Assert.IsTrue(settings.Rules[0].IsMatch("this is an ERROR line"));
            Assert.IsFalse(settings.Rules[0].IsMatch("all fine here"));
            Assert.IsNotNull(settings.Rules[0].Foreground);
        }

        [TestMethod]
        public void MalformedColor_DoesNotThrow_FallsBackToNull()
        {
            var settings = new ColorSettings(new ConfigColorSettings
            {
                Rules = new[]
                {
                    new ConfigColorRule { RegexString = "x", ForegroundColor = "definitely-not-a-color", BackgroundColor = "" }
                }
            });

            Assert.AreEqual(1, settings.Rules.Count);
            Assert.IsNull(settings.Rules[0].Foreground);
        }

        [TestMethod]
        public void EmptyConfiguration_ProducesNoRules()
        {
            var settings = new ColorSettings(new ConfigColorSettings());
            Assert.AreEqual(0, settings.Rules.Count);
        }
    }
}
