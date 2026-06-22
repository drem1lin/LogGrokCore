using System.Collections.Generic;
using System.Linq;
using LogGrokCore.Data.Index;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokCore.Data.Tests
{
    [TestClass]
    public class CountIndexTests
    {
        private sealed class FakeIndex : IIndex<int>
        {
            public int Count { get; set; }
            public void Add(int value) => Count++;
            public IEnumerable<int> GetEnumerableFromValue(int value) => Enumerable.Empty<int>();
        }

        private static readonly IndexKeyNum Key = new() { KeyNum = 1 };

        private static (CountIndex<FakeIndex>, Dictionary<IndexKeyNum, FakeIndex>, FakeIndex) Make(int initialCount)
        {
            var index = new FakeIndex { Count = initialCount };
            var indices = new Dictionary<IndexKeyNum, FakeIndex> { [Key] = index };
            return (new CountIndex<FakeIndex>(indices), indices, index);
        }

        [TestMethod]
        public void Counts_DuringLoad_ReflectsCurrentPerKeyCounts()
        {
            var (countIndex, indices, _) = Make(5);
            countIndex.Add(1, indices);

            var counts = countIndex.Counts;
            Assert.AreEqual(1, counts.Count);             // a single live-tail snapshot
            Assert.AreEqual(5, counts[^1][0].Item2);      // count for the key
        }

        [TestMethod]
        public void Counts_ReusedWhileNoNewLineIndexed()
        {
            var (countIndex, indices, _) = Make(3);
            countIndex.Add(1, indices);

            var first = countIndex.Counts;
            var second = countIndex.Counts;

            // Regression: previously a fresh O(keys) snapshot was built on every read.
            Assert.AreSame(first, second);
        }

        [TestMethod]
        public void Counts_RebuiltAfterNewLineIndexed()
        {
            var (countIndex, indices, index) = Make(3);
            countIndex.Add(1, indices);
            var first = countIndex.Counts;

            index.Count = 4;
            countIndex.Add(2, indices); // new line -> cache invalidated

            var rebuilt = countIndex.Counts;
            Assert.AreNotSame(first, rebuilt);
            Assert.AreEqual(4, rebuilt[^1][0].Item2);
        }

        [TestMethod]
        public void Counts_AfterFinish_IsStableSnapshot()
        {
            var (countIndex, indices, _) = Make(7);
            countIndex.Add(1, indices);
            countIndex.Finish(indices);

            var a = countIndex.Counts;
            var b = countIndex.Counts;
            Assert.AreSame(a, b);
            Assert.AreEqual(7, a[^1][0].Item2);
        }
    }
}
