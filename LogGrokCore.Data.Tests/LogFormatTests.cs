using LogGrokCore.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokCore.Data.Tests
{
    [TestClass]
    public class LogFormatTests
    {
        [TestMethod]
        public void IsCorrect_ValidRegexAndTransformations_ReturnsTrue()
        {
            var format = new LogFormat
            {
                Regex = @"(?<A>\w)(?<B>\w)",
                Transformations = new[] { @"(?<X>\d+)" }
            };
            Assert.IsTrue(format.IsCorrect());
        }

        [TestMethod]
        public void IsCorrect_InvalidMainRegex_ReturnsFalse()
        {
            Assert.IsFalse(new LogFormat { Regex = "(" }.IsCorrect());
        }

        [TestMethod]
        public void IsCorrect_InvalidTransformationRegex_ReturnsFalse()
        {
            var format = new LogFormat
            {
                Regex = @"(?<A>\w)",
                Transformations = new[] { "(" }
            };
            Assert.IsFalse(format.IsCorrect());
        }

        [TestMethod]
        public void FieldNames_ExcludeGroupZero()
        {
            var format = new LogFormat { Regex = @"(?<A>\w)(?<B>\w)" };
            CollectionAssert.AreEqual(new[] { "A", "B" }, format.FieldNames);
        }

        [TestMethod]
        public void IndexedFieldNumbers_MapNamesToZeroBasedGroupNumbers()
        {
            var format = new LogFormat
            {
                Regex = @"(?<Time>\w)(?<Level>\w)(?<Message>\w)",
                IndexedFields = new[] { "Time", "Message" }
            };
            CollectionAssert.AreEqual(new[] { 0, 2 }, format.IndexedFieldNumbers);
        }
    }
}
