using System;
using LogGrokCore.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokCore.Data.Tests
{
    [TestClass]
    public class TransformationPerformerTests
    {
        [TestMethod]
        public void NoTransformations_ReturnsSourceUnchanged()
        {
            var performer = new TransformationPerformer(Array.Empty<string>());
            Assert.AreEqual("hello world", performer.Transform("hello world"));
        }

        [TestMethod]
        public void Base64Decode_DecodesNamedGroup()
        {
            var performer = new TransformationPerformer(new[] { @"(?<Base64Decode>[A-Za-z0-9+/=]+)" });
            Assert.AreEqual("hello", performer.Transform("aGVsbG8="));
        }

        [TestMethod]
        public void Base64Decode_PreservesSurroundingText()
        {
            var performer = new TransformationPerformer(new[] { @"data:(?<Base64Decode>[A-Za-z0-9+/=]+);" });
            Assert.AreEqual("data:hello;", performer.Transform("data:aGVsbG8=;"));
        }

        [TestMethod]
        public void UnknownTransformName_LeavesGroupTextUnchanged()
        {
            var performer = new TransformationPerformer(new[] { @"(?<Unknown>[A-Za-z0-9+/=]+)" });
            Assert.AreEqual("aGVsbG8=", performer.Transform("aGVsbG8="));
        }

        [TestMethod]
        public void NonMatchingRegex_ReturnsSourceUnchanged()
        {
            var performer = new TransformationPerformer(new[] { @"(?<Base64Decode>\d+)" });
            Assert.AreEqual("no digits", performer.Transform("no digits"));
        }
    }
}
