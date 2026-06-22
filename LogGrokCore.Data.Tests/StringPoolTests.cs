using System;
using System.Linq;
using LogGrokCore.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokCore.Data.Tests
{
    [TestClass]
    public class StringPoolTests
    {
        [TestMethod]
        public void Rent_RoundsSizeUpToPowerOfTwo_WithMinimum32()
        {
            var pool = new StringPool();
            Assert.AreEqual(32, pool.Rent(1).Length);
            Assert.AreEqual(32, pool.Rent(32).Length);
            Assert.AreEqual(64, pool.Rent(33).Length);
            Assert.AreEqual(128, pool.Rent(100).Length);
        }

        [TestMethod]
        public void Rent_ReturnsZeroFilledString()
        {
            var pool = new StringPool();
            Assert.IsTrue(pool.Rent(10).All(c => c == '\0'));
        }

        [TestMethod]
        public void RentReturnRent_ReusesTheSameInstance()
        {
            var pool = new StringPool();
            var first = pool.Rent(50);   // bucket 64
            pool.Return(first);
            var second = pool.Rent(40);  // bucket 64 again
            Assert.AreSame(first, second);
        }

        [TestMethod]
        public void Return_StringWithNoMatchingBucket_Throws()
        {
            var pool = new StringPool();
            var notFromPool = new string('\0', 17); // 17 is never a bucket size
            Assert.ThrowsExactly<InvalidOperationException>(() => pool.Return(notFromPool));
        }

        [TestMethod]
        public void Return_BeyondCap_StopsGrowing()
        {
            var pool = new StringPool();
            _ = pool.Rent(50); // create the size-64 bucket

            for (var i = 0; i < 200; i++)
                pool.Return(new string('\0', 64));

            // Regression: the bag used to retain every returned string forever; now capped.
            Assert.AreEqual(64, pool.GetPooledCount(50));
        }

        [TestMethod]
        public void Rent_DecrementsPooledCount()
        {
            var pool = new StringPool();
            var rented = Enumerable.Range(0, 5).Select(_ => pool.Rent(50)).ToList();
            foreach (var s in rented) pool.Return(s);

            Assert.AreEqual(5, pool.GetPooledCount(50));
            _ = pool.Rent(50);
            Assert.AreEqual(4, pool.GetPooledCount(50));
        }
    }
}
