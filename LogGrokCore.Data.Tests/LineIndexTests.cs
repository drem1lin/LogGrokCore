using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokCore.Data.Tests
{
    [TestClass]
    public class LineIndexTests
    {
        [TestMethod]
        public void GetLine_SingleLine_ReturnsCorrectOffsetAndLength()
        {
            var lineIndex = new LineIndex();
            lineIndex.Add(0);
            lineIndex.Finish(100);

            var (offset, length) = lineIndex.GetLine(0);

            Assert.AreEqual(0L, offset);
            Assert.AreEqual(100, length);
        }

        [TestMethod]
        public void GetLine_MultipleLines_ReturnsCorrectLengths()
        {
            var lineIndex = new LineIndex();
            lineIndex.Add(0);
            lineIndex.Add(50);
            lineIndex.Add(120);
            lineIndex.Finish(30);

            var (offset0, length0) = lineIndex.GetLine(0);
            var (offset1, length1) = lineIndex.GetLine(1);
            var (offset2, length2) = lineIndex.GetLine(2);

            Assert.AreEqual(0L, offset0);
            Assert.AreEqual(50, length0);
            Assert.AreEqual(50L, offset1);
            Assert.AreEqual(70, length1);
            Assert.AreEqual(120L, offset2);
            Assert.AreEqual(30, length2);
        }

        [TestMethod]
        public void Count_BeforeFinish_ExcludesLastLine()
        {
            var lineIndex = new LineIndex();
            lineIndex.Add(0);
            lineIndex.Add(50);
            lineIndex.Add(120);

            // Before Finish, the last line start is not counted as a complete line
            Assert.AreEqual(2, lineIndex.Count);
        }

        [TestMethod]
        public void Count_AfterFinish_IncludesAllLines()
        {
            var lineIndex = new LineIndex();
            lineIndex.Add(0);
            lineIndex.Add(50);
            lineIndex.Add(120);
            lineIndex.Finish(30);

            Assert.AreEqual(3, lineIndex.Count);
        }

        [TestMethod]
        public void Count_Empty_ReturnsZero()
        {
            var lineIndex = new LineIndex();
            Assert.AreEqual(0, lineIndex.Count);
        }

        [TestMethod]
        public void IsFinished_BeforeFinish_ReturnsFalse()
        {
            var lineIndex = new LineIndex();
            lineIndex.Add(0);
            Assert.IsFalse(lineIndex.IsFinished);
        }

        [TestMethod]
        public void IsFinished_AfterFinish_ReturnsTrue()
        {
            var lineIndex = new LineIndex();
            lineIndex.Add(0);
            lineIndex.Finish(10);
            Assert.IsTrue(lineIndex.IsFinished);
        }

        [TestMethod]
        public void GetLine_LastLineBeforeFinish_Throws()
        {
            var lineIndex = new LineIndex();
            lineIndex.Add(0);
            lineIndex.Add(50);

            // Trying to get the last line before Finish should throw
            Assert.ThrowsException<IndexOutOfRangeException>(() => lineIndex.GetLine(1));
        }

        [TestMethod]
        public void Fetch_ReturnsCorrectValues()
        {
            var lineIndex = new LineIndex();
            lineIndex.Add(0);
            lineIndex.Add(100);
            lineIndex.Add(250);
            lineIndex.Finish(50);

            var values = new (long offset, int length)[3];
            lineIndex.Fetch(0, values.AsSpan());

            Assert.AreEqual((0L, 100), values[0]);
            Assert.AreEqual((100L, 150), values[1]);
            Assert.AreEqual((250L, 50), values[2]);
        }

        [TestMethod]
        public void ConcurrentAddAndRead_DoesNotCorrupt()
        {
            var lineIndex = new LineIndex();
            const int lineCount = 10000;
            var errors = new List<string>();

            // Writer thread
            var writerTask = Task.Run(() =>
            {
                for (int i = 0; i < lineCount; i++)
                {
                    lineIndex.Add(i * 100);
                }
                lineIndex.Finish(50);
            });

            // Reader thread
            var readerTask = Task.Run(() =>
            {
                var rng = new Random(42);
                for (int i = 0; i < 5000; i++)
                {
                    try
                    {
                        var count = lineIndex.Count;
                        if (count > 1)
                        {
                            var idx = rng.Next(0, count - 1);
                            var (offset, length) = lineIndex.GetLine(idx);
                            if (length <= 0)
                                errors.Add($"Invalid length {length} at index {idx}");
                        }
                    }
                    catch (IndexOutOfRangeException)
                    {
                        // Expected during concurrent access - count may change
                    }
                    Thread.SpinWait(10);
                }
            });

            Task.WaitAll(writerTask, readerTask);
            Assert.AreEqual(0, errors.Count, string.Join("; ", errors));
        }

        [TestMethod]
        public void ConcurrentFinishAndGetLine_ThreadSafe()
        {
            // Specifically tests that Finish() and GetLine() don't race on _lastLineLength
            for (int iteration = 0; iteration < 100; iteration++)
            {
                var lineIndex = new LineIndex();
                lineIndex.Add(0);
                lineIndex.Add(100);

                var barrier = new Barrier(2);
                Exception? caught = null;

                var finishTask = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    lineIndex.Finish(50);
                });

                var readTask = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    try
                    {
                        // This might throw IndexOutOfRangeException if not yet finished,
                        // but should never return corrupted data
                        var count = lineIndex.Count;
                        if (count >= 2)
                        {
                            var (offset, length) = lineIndex.GetLine(1);
                            Assert.AreEqual(100L, offset);
                            Assert.AreEqual(50, length);
                        }
                    }
                    catch (IndexOutOfRangeException)
                    {
                        // Expected - Finish not yet called
                    }
                    catch (Exception ex)
                    {
                        caught = ex;
                    }
                });

                Task.WaitAll(finishTask, readTask);
                if (caught != null)
                    Assert.Fail($"Iteration {iteration}: {caught}");
            }
        }
    }
}
