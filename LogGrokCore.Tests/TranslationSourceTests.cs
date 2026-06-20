using LogGrokCore.Localization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokCore.Tests
{
    [TestClass]
    public class TranslationSourceTests
    {
        // SetCulture mutates the process UI culture; restore the base language after each test.
        [TestCleanup]
        public void Cleanup() => TranslationSource.Instance.SetCulture("en");

        [TestMethod]
        public void SetCulture_ExactSupportedCode_IsApplied()
        {
            Assert.AreEqual("ru", TranslationSource.Instance.SetCulture("ru"));
            Assert.AreEqual("de", TranslationSource.Instance.SetCulture("de"));
        }

        [TestMethod]
        public void SetCulture_IsCaseInsensitive()
        {
            Assert.AreEqual("ru", TranslationSource.Instance.SetCulture("RU"));
        }

        [TestMethod]
        public void SetCulture_RegionSpecificCode_FallsBackToBaseLanguage()
        {
            Assert.AreEqual("de", TranslationSource.Instance.SetCulture("de-DE"));
            Assert.AreEqual("ru", TranslationSource.Instance.SetCulture("ru-RU"));
            Assert.AreEqual("en", TranslationSource.Instance.SetCulture("en-US"));
        }

        [TestMethod]
        public void SetCulture_SupportedRegionalCode_MatchesExactly()
        {
            Assert.AreEqual("pt-BR", TranslationSource.Instance.SetCulture("pt-BR"));
        }

        [TestMethod]
        public void SetCulture_UnknownOrEmpty_FallsBackToEnglish()
        {
            Assert.AreEqual("en", TranslationSource.Instance.SetCulture("zz"));
            Assert.AreEqual("en", TranslationSource.Instance.SetCulture(""));
            Assert.AreEqual("en", TranslationSource.Instance.SetCulture(null));
        }
    }
}
