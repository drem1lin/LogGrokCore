using System;
using System.Linq;
using LogGrokCore.Bootstrap;
using LogGrokCore.Search;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokCore.Tests
{
    [TestClass]
    public class RemainingMinShowDelayTests
    {
        private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void BeforeMinElapsed_ReturnsRemainder()
        {
            var delay = SearchDocumentViewModel.RemainingMinShowDelay(Start, Start.AddMilliseconds(200), 500);
            Assert.AreEqual(300d, delay.TotalMilliseconds, 0.001);
        }

        [TestMethod]
        public void ExactlyMinElapsed_ReturnsZero()
        {
            var delay = SearchDocumentViewModel.RemainingMinShowDelay(Start, Start.AddMilliseconds(500), 500);
            Assert.AreEqual(TimeSpan.Zero, delay);
        }

        [TestMethod]
        public void PastMinElapsed_ClampsToZero_NeverNegative()
        {
            // Regression: a negative remainder used to flow into Task.Delay and throw.
            var delay = SearchDocumentViewModel.RemainingMinShowDelay(Start, Start.AddMilliseconds(800), 500);
            Assert.AreEqual(TimeSpan.Zero, delay);
            Assert.IsTrue(delay >= TimeSpan.Zero);
        }
    }

    [TestClass]
    public class ParseInstanceMessageTests
    {
        [TestMethod]
        public void SplitsOnSeparator()
        {
            CollectionAssert.AreEqual(
                new[] { "a", "b", "c" },
                SingleInstanceManager.ParseInstanceMessage("a|:|b|:|c").ToList());
        }

        [TestMethod]
        public void DropsEmptyAndWhitespaceEntries()
        {
            CollectionAssert.AreEqual(
                new[] { "a", "b" },
                SingleInstanceManager.ParseInstanceMessage("a|:||:|   |:|b").ToList());
        }

        [TestMethod]
        public void EmptyMessage_YieldsNothing()
        {
            Assert.AreEqual(0, SingleInstanceManager.ParseInstanceMessage("").Count());
            Assert.AreEqual(0, SingleInstanceManager.ParseInstanceMessage("   ").Count());
        }

        [TestMethod]
        public void PreservesPathsWithSpaces()
        {
            CollectionAssert.AreEqual(
                new[] { @"C:\some path\file.log" },
                SingleInstanceManager.ParseInstanceMessage(@"C:\some path\file.log").ToList());
        }
    }
}
