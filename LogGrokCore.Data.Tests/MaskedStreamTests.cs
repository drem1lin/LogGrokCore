using System.IO;
using System.Linq;
using LogGrokCore.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokCore.Data.Tests
{
    [TestClass]
    public class MaskedStreamTests
    {
        private static byte[] Xor(byte[] data, byte mask) =>
            data.Select(b => (byte)(b ^ mask)).ToArray();

        private static byte[] ReadAll(byte[] source, byte mask, int count)
        {
            using var stream = new MaskedStream(new MemoryStream(source), mask);
            var buffer = new byte[count];
            var read = stream.Read(buffer, 0, count);
            Assert.AreEqual(count, read);
            return buffer;
        }

        [TestMethod]
        public void Read_ZeroMask_IsIdentity()
        {
            var data = Enumerable.Range(0, 20).Select(i => (byte)i).ToArray();
            CollectionAssert.AreEqual(data, ReadAll(data, 0, 20));
        }

        [TestMethod]
        public void Read_XorsEachByte_ForVariousLengths()
        {
            // Exercises the head / aligned-ulong / tail branches of the XOR loop.
            foreach (var length in new[] { 1, 3, 7, 8, 15, 16, 20 })
            {
                var data = Enumerable.Range(0, length).Select(i => (byte)(i * 7 + 1)).ToArray();
                CollectionAssert.AreEqual(Xor(data, 0xEF), ReadAll(data, 0xEF, length),
                    $"length {length}");
            }
        }

        [TestMethod]
        public void Read_RespectsBufferOffset()
        {
            var data = Enumerable.Range(0, 10).Select(i => (byte)i).ToArray();
            using var stream = new MaskedStream(new MemoryStream(data), 0xEF);

            var buffer = new byte[15];
            var read = stream.Read(buffer, 5, 10);

            Assert.AreEqual(10, read);
            for (var i = 0; i < 5; i++)
                Assert.AreEqual(0, buffer[i], "bytes before the offset must be untouched");
            CollectionAssert.AreEqual(Xor(data, 0xEF), buffer.Skip(5).ToArray());
        }

        [TestMethod]
        public void Read_PartialRead_XorsOnlyBytesActuallyRead()
        {
            var data = Enumerable.Range(0, 5).Select(i => (byte)i).ToArray();
            using var stream = new MaskedStream(new MemoryStream(data), 0xEF);

            var buffer = new byte[10];
            var read = stream.Read(buffer, 0, 10);

            Assert.AreEqual(5, read);
            CollectionAssert.AreEqual(Xor(data, 0xEF), buffer.Take(5).ToArray());
            for (var i = 5; i < 10; i++)
                Assert.AreEqual(0, buffer[i], "bytes past the data must not be masked");
        }
    }
}
