using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LogGrokCore.Data.IndexTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokCore.Data.Tests
{
    [TestClass]
    public class IndexTreeRaceTests
    {
        private static IndexTree<int, SimpleLeaf<int>> NewTree() =>
            new(16, value => new SimpleLeaf<int>(value, 0));

        // Reproduction: one writer Add()-ing while readers enumerate / look up by value, mirroring
        // the loader thread growing the index while the UI filters a still-loading file. Readers are
        // confirmed to be spinning before the writer starts, and the writer yields periodically so
        // the two genuinely overlap.
        [TestMethod]
        public void ConcurrentAddAndRead_DoesNotThrowOrTear()
        {
            const int total = 2_000_000; // crosses many leaf (1024) and node (16) boundaries
            const int readerCount = 4;
            var tree = NewTree();
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
                        // index walk: values must stay strictly ascending
                        var prev = int.MinValue;
                        foreach (var v in tree.GetEnumerableFromIndex(0))
                        {
                            if (v < prev) Interlocked.Increment(ref torn);
                            prev = v;
                        }

                        // value lookup: exercises TreeNode._subNodes BinarySearch during Add
                        var count = tree.Count;
                        if (count > 0)
                        {
                            _ = tree.FindIndexByValue(count / 2);
                            _ = tree.GetEnumerableFromValue(count / 2).Take(64).Count();
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
                tree.Add(i);
                Thread.SpinWait(15); // stretch the write so it genuinely overlaps the readers
            }

            done = true;
            Task.WaitAll(readers);

            Assert.AreEqual(0, exceptions.Count,
                "readers threw during concurrent Add: " + string.Join(" | ",
                    exceptions.Take(3).Select(e => e.GetType().Name + ": " + e.Message)));
            Assert.AreEqual(0, torn, "readers observed out-of-order (torn) values during concurrent Add");
            Assert.AreEqual(total, tree.Count);
            Assert.AreEqual(total, tree.GetEnumerableFromIndex(0).Count());
        }
    }
}
