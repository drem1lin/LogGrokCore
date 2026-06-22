using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        // Mirrors IndexTreeRaceTests: the loader appends (growing the underlying list of chunks)
        // while readers index it, as the search pipeline does via Indexer.GetIndexKeyNum during load.
        [TestMethod]
        public void ConcurrentAddAndRead_DoesNotThrowOrTear()
        {
            const int total = 2_000_000;
            const int readerCount = 4;
            var list = new ChunkedList<int>(16384);
            var exceptions = new List<Exception>();
            var torn = 0;
            var readersReady = 0;
            var done = false;

            var readers = Enumerable.Range(0, readerCount).Select(_ => Task.Factory.StartNew(() =>
            {
                Interlocked.Increment(ref readersReady);
                try
                {
                    while (!Volatile.Read(ref done))
                    {
                        var count = list.Count;
                        var prev = int.MinValue;
                        for (var i = 0; i < count; i++)
                        {
                            var v = list[i];
                            if (v < prev) Interlocked.Increment(ref torn);
                            prev = v;
                        }
                    }
                }
                catch (Exception e)
                {
                    lock (exceptions) exceptions.Add(e);
                }
            }, TaskCreationOptions.LongRunning)).ToArray();

            while (Volatile.Read(ref readersReady) < readerCount)
                Thread.Yield();

            for (var i = 0; i < total; i++)
            {
                list.Add(i);
                Thread.SpinWait(15);
            }

            done = true;
            Task.WaitAll(readers);

            Assert.AreEqual(0, exceptions.Count,
                "readers threw during concurrent Add: " + string.Join(" | ",
                    exceptions.Take(3).Select(e => e.GetType().Name + ": " + e.Message)));
            Assert.AreEqual(0, torn, "readers observed out-of-order (torn) values during concurrent Add");
            Assert.AreEqual(total, list.Count);
        }
    }
}
