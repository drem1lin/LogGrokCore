using System.Collections.Generic;
using System.Linq;
using LogGrokCore.Data.Index;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokCore.Data.Tests
{
    [TestClass]
    public class CollectionUtilsTests
    {
        private static List<int> Merge(params IEnumerable<int>[] sources) =>
            CollectionUtils.MergeSorted(sources).ToList();

        [TestMethod]
        public void MergeSorted_InterleavedSources_ReturnsAscending()
        {
            var result = Merge(new[] { 1, 3, 5 }, new[] { 2, 4, 6 });
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6 }, result);
        }

        [TestMethod]
        public void MergeSorted_SingleSource_ReturnsAllItems()
        {
            var result = Merge(new[] { 1, 2, 3, 4 });
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, result);
        }

        [TestMethod]
        public void MergeSorted_EmptySourcesAreSkipped()
        {
            var result = Merge(new int[0], new[] { 1, 2 }, new int[0]);
            CollectionAssert.AreEqual(new[] { 1, 2 }, result);
        }

        [TestMethod]
        public void MergeSorted_AllEmpty_ReturnsEmpty()
        {
            Assert.AreEqual(0, Merge(new int[0], new int[0]).Count);
        }
    }
}
