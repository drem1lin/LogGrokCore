using System;
using System.IO;
using System.Threading.Tasks;
using LogGrokCore.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokCore.Tests
{
    [TestClass]
    public class FirstChanceExceptionsFilterTests
    {
        [TestMethod]
        public void TaskCanceled_IsKnown()
        {
            Assert.IsTrue(FirstChanceExceptionsFilter.IsKnown(new TaskCanceledException()));
        }

        [TestMethod]
        public void KnownSerializationAssembly_FileNotFound_IsKnown()
        {
            var ex = new FileNotFoundException("not found", "ControlzEx.XmlSerializers");
            Assert.IsTrue(FirstChanceExceptionsFilter.IsKnown(ex));
        }

        [TestMethod]
        public void UnrelatedFileNotFound_IsNotKnown()
        {
            var ex = new FileNotFoundException("not found", "SomeOther.dll");
            Assert.IsFalse(FirstChanceExceptionsFilter.IsKnown(ex));
        }

        [TestMethod]
        public void PlainException_IsNotKnown()
        {
            Assert.IsFalse(FirstChanceExceptionsFilter.IsKnown(new Exception("boom")));
        }

        [TestMethod]
        public void NeverThrownArgumentException_HasNoStackTrace_IsNotKnown()
        {
            // Not yet thrown -> StackTrace is null -> the method-name filter can't match.
            Assert.IsFalse(FirstChanceExceptionsFilter.IsKnown(new ArgumentException("x")));
        }
    }
}
