using LogGrokCore.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokCore.Data.Tests
{
    [TestClass]
    public class LogMetaInformationTests
    {
        [TestMethod]
        public void CreateTextFileMetaInformation_HasSingleTextComponent()
        {
            var meta = LogMetaInformation.CreateTextFileMetaInformation();

            Assert.AreEqual(1, meta.ComponentCount);
            CollectionAssert.AreEqual(new[] { "Text" }, meta.FieldNames);
            Assert.AreEqual(0, meta.IndexedFieldNumbers.Length);
            Assert.IsFalse(meta.IsFieldIndexed("Text"));
        }

        [TestMethod]
        public void IsFieldIndexed_ReflectsIndexedFields()
        {
            var meta = new LogMetaInformation(new LogFormat
            {
                Regex = @"(?<Time>\w)(?<Level>\w)(?<Message>\w)",
                IndexedFields = new[] { "Level" }
            });

            Assert.IsTrue(meta.IsFieldIndexed("Level"));
            Assert.IsFalse(meta.IsFieldIndexed("Time"));
            Assert.IsFalse(meta.IsFieldIndexed("Unknown"));
        }

        [TestMethod]
        public void GetIndexedFieldIndexByName_ReturnsPositionWithinIndexedFields()
        {
            var meta = new LogMetaInformation(new LogFormat
            {
                Regex = @"(?<Time>\w)(?<Level>\w)(?<Message>\w)",
                IndexedFields = new[] { "Level", "Message" }
            });

            Assert.AreEqual(0, meta.GetIndexedFieldIndexByName("Level"));
            Assert.AreEqual(1, meta.GetIndexedFieldIndexByName("Message"));
            Assert.AreEqual(-1, meta.GetIndexedFieldIndexByName("Unknown"));
        }
    }
}
