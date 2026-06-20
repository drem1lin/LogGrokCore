using LogGrokCore.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokCore.Data.Tests
{
    [TestClass]
    public class AlignTests
    {
        [TestMethod]
        public void Get_AlreadyAligned_ReturnsValue()
        {
            Assert.AreEqual(8, Align.Get(8, 8));
            Assert.AreEqual(0, Align.Get(0, 8));
        }

        [TestMethod]
        public void Get_NotAligned_RoundsUp()
        {
            Assert.AreEqual(8, Align.Get(5, 8));
            Assert.AreEqual(16, Align.Get(9, 8));
        }

        [TestMethod]
        public void Get_AlignmentOne_ReturnsValueUnchanged()
        {
            Assert.AreEqual(5, Align.Get(5, 1));
        }
    }
}
