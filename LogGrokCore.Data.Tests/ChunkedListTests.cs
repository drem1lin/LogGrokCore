using System.Collections.Generic;
using System.Linq;
using LogGrokCore.Data.Index;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokCore.Data.Tests
{
    [TestClass]
    public class ChunkedListTests
    {
        [TestMethod]
        public void Clear_ResetsCount_AndAllowsReuse()
        {
            var list = new ChunkedList<int>(4);
            for (var i = 0; i < 10; i++)
                list.Add(i);
            Assert.AreEqual(10, list.Count);

            list.Clear();

            // Regression: Clear() left _count untouched, so the list looked non-empty and
            // indexing dangled into cleared chunks.
            Assert.AreEqual(0, list.Count);

            list.Add(42);
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(42, list[0]);
        }

        [TestMethod]
        public void Add_AcrossChunkBoundaries_IndexerReturnsValues()
        {
            var list = new ChunkedList<int>(4);
            for (var i = 0; i < 11; i++)
                list.Add(i * 10);

            Assert.AreEqual(11, list.Count);
            for (var i = 0; i < 11; i++)
                Assert.AreEqual(i * 10, list[i]);
        }

        [TestMethod]
        public void Enumerate_YieldsAllItemsInOrder()
        {
            var list = new ChunkedList<int>(4);
            for (var i = 0; i < 11; i++)
                list.Add(i);

            // Iterate via the enumerator (CopyTo/ToArray are intentionally unsupported).
            var result = new List<int>();
            foreach (var item in list)
                result.Add(item);

            CollectionAssert.AreEqual(Enumerable.Range(0, 11).ToArray(), result);
        }
    }
}
