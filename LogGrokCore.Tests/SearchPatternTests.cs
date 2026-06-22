using System.Text.RegularExpressions;
using LogGrokCore.Search;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokCore.Tests
{
    [TestClass]
    public class SearchPatternTests
    {
        [TestMethod]
        public void NonRegexPattern_IsEscaped()
        {
            var pattern = new SearchPattern("a.b", false, useRegex: false);
            var regex = pattern.GetRegex(RegexOptions.None);

            Assert.IsTrue(regex.IsMatch("a.b"));
            Assert.IsFalse(regex.IsMatch("axb"), "the dot must be treated literally");
        }

        [TestMethod]
        public void RegexPattern_IsUsedRaw()
        {
            var pattern = new SearchPattern("a.b", false, useRegex: true);
            Assert.IsTrue(pattern.GetRegex(RegexOptions.None).IsMatch("axb"));
        }

        [TestMethod]
        public void CaseInsensitive_MatchesRegardlessOfCase()
        {
            var pattern = new SearchPattern("abc", isCaseSensitive: false, useRegex: false);
            Assert.IsTrue(pattern.GetRegex(RegexOptions.None).IsMatch("ABC"));
        }

        [TestMethod]
        public void CaseSensitive_RespectsCase()
        {
            var pattern = new SearchPattern("abc", isCaseSensitive: true, useRegex: false);
            Assert.IsFalse(pattern.GetRegex(RegexOptions.None).IsMatch("ABC"));
            Assert.IsTrue(pattern.GetRegex(RegexOptions.None).IsMatch("abc"));
        }

        [TestMethod]
        public void InvalidRegex_IsNotValid_AndReportsError()
        {
            var pattern = new SearchPattern("(", false, useRegex: true);
            Assert.IsFalse(pattern.IsValid);
            Assert.IsFalse(string.IsNullOrEmpty(pattern.RegexParseError));
        }

        [TestMethod]
        public void ValidRegex_IsValid()
        {
            Assert.IsTrue(new SearchPattern("a+", false, useRegex: true).IsValid);
        }

        [TestMethod]
        public void Empty_IsEmpty()
        {
            Assert.IsTrue(SearchPattern.Empty.IsEmpty);
        }

        [TestMethod]
        public void Equality_DependsOnPatternAndFlags()
        {
            var a = new SearchPattern("x", false, true);
            var b = new SearchPattern("x", false, true);
            var c = new SearchPattern("y", false, true);

            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.AreNotEqual(a, c);
        }

        [TestMethod]
        public void GetRegex_SamePatternAndOptions_ReturnsCachedInstance()
        {
            var pattern = new SearchPattern("cache-me", false, useRegex: false);
            var first = pattern.GetRegex(RegexOptions.Compiled);
            var second = pattern.GetRegex(RegexOptions.Compiled);

            Assert.AreSame(first, second, "compiled regex must be cached, not rebuilt per call");
        }

        [TestMethod]
        public void GetRegex_EqualPatterns_ShareCachedInstance()
        {
            var a = new SearchPattern("shared", false, useRegex: false);
            var b = new SearchPattern("shared", false, useRegex: false);

            Assert.AreSame(a.GetRegex(RegexOptions.Compiled), b.GetRegex(RegexOptions.Compiled));
        }

        [TestMethod]
        public void GetRegex_DifferentOptions_ReturnsDifferentInstances()
        {
            var pattern = new SearchPattern("opts", false, useRegex: false);

            Assert.AreNotSame(
                pattern.GetRegex(RegexOptions.Compiled),
                pattern.GetRegex(RegexOptions.None));
        }

        [TestMethod]
        public void GetRegex_CaseSensitivity_NotConflatedInCache()
        {
            var insensitive = new SearchPattern("Abc", isCaseSensitive: false, useRegex: false);
            var sensitive = new SearchPattern("Abc", isCaseSensitive: true, useRegex: false);

            Assert.IsTrue(insensitive.GetRegex(RegexOptions.Compiled).IsMatch("abc"));
            Assert.IsFalse(sensitive.GetRegex(RegexOptions.Compiled).IsMatch("abc"));
        }
    }
}
