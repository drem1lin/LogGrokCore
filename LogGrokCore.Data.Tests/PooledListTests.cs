using LogGrokCore.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokCore.Data.Tests
{
    [TestClass]
    public class PooledListTests
    {
        [TestMethod]
        public void Contains_ItemAtIndexZero_ReturnsTrue()
        {
            using var list = new PooledList<int> { 42, 7, 99 };

            // Regression: Contains used IndexOf(item) > 0, which missed index 0.
            Assert.IsTrue(list.Contains(42));
            Assert.AreEqual(0, list.IndexOf(42));
        }

        [TestMethod]
        public void Contains_PresentAndMissing()
        {
            using var list = new PooledList<int> { 1, 2, 3 };

            Assert.IsTrue(list.Contains(2));
            Assert.IsTrue(list.Contains(3));
            Assert.IsFalse(list.Contains(4));
        }

        [TestMethod]
        public void Add_GrowsBeyondInitialCapacity()
        {
            using var list = new PooledList<int>(2);
            for (var i = 0; i < 1000; i++)
                list.Add(i);

            Assert.AreEqual(1000, list.Count);
            Assert.AreEqual(0, list[0]);
            Assert.AreEqual(999, list[999]);
            Assert.IsTrue(list.Contains(0));
            Assert.IsTrue(list.Contains(999));
        }

        [TestMethod]
        public void IndexOf_MissingItem_ReturnsMinusOne()
        {
            using var list = new PooledList<int> { 10, 20 };
            Assert.AreEqual(-1, list.IndexOf(30));
        }

        [TestMethod]
        public void Contains_ReferenceType_NullHandling()
        {
            using var list = new PooledList<string?> { "a", null, "b" };

            Assert.IsTrue(list.Contains(null));
            Assert.AreEqual(1, list.IndexOf(null));
            Assert.IsTrue(list.Contains("a"));
            Assert.IsFalse(list.Contains("z"));
        }
    }
}
