using System;
using System.Collections.Generic;
using LogGrokCore.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokCore.Data.Tests
{
    [TestClass]
    public class ListExtensionsBinarySearchTests
    {
        private static readonly IReadOnlyList<int> List = new[] { 1, 3, 5, 7, 9 };
        private static readonly Func<int, int, int> Comparer = (element, value) => element.CompareTo(value);

        [TestMethod]
        public void BinarySearch_ExactMatch_ReturnsIndex()
        {
            Assert.AreEqual(0, List.BinarySearch(1, Comparer));
            Assert.AreEqual(2, List.BinarySearch(5, Comparer));
            Assert.AreEqual(4, List.BinarySearch(9, Comparer));
        }

        [TestMethod]
        public void BinarySearch_NotFound_ReturnsBitwiseComplementOfInsertionPoint()
        {
            Assert.AreEqual(~2, List.BinarySearch(4, Comparer)); // between 3 and 5
            Assert.AreEqual(~0, List.BinarySearch(0, Comparer)); // below all
            Assert.AreEqual(~5, List.BinarySearch(10, Comparer)); // above all
        }

        [TestMethod]
        public void BinarySearch_SubRange_SearchesOnlyWithinRange()
        {
            // indices 1..3 -> values {3,5,7}
            Assert.AreEqual(3, List.BinarySearch(1, 3, 7, Comparer));
        }
    }
}
