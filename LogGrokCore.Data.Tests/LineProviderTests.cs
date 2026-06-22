using System;
using System.IO;
using LogGrokCore.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokCore.Data.Tests
{
    [TestClass]
    public class LineProviderTests
    {
        // Returns offsets whose span exceeds Int32.MaxValue, simulating a >2GB fetch range.
        private sealed class HugeRangeLineIndex : ILineIndex
        {
            public int Count => 100;

            public (long offset, int length) GetLine(int index) =>
                index == 0 ? (0L, 0) : (3_000_000_000L, 10);

            public void Fetch(int start, Span<(long offset, int length)> values) =>
                throw new InvalidOperationException("should not be reached");
        }

        [TestMethod]
        public void Fetch_RangeExceedingInt32_ThrowsInsteadOfOverflowing()
        {
            var temp = Path.GetTempFileName();
            try
            {
                File.WriteAllText(temp, "hello world");
                var provider = new LineProvider(new HugeRangeLineIndex(), new LogFile(temp, 0));

                // Regression: the size cast to int used to overflow silently (negative / wrong size).
                Assert.ThrowsExactly<InvalidOperationException>(
                    () => provider.Fetch(0, new (int, string)[2]));
            }
            finally
            {
                File.Delete(temp);
            }
        }
    }
}
