using System;
using LogGrokCore.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokCore.Data.Tests
{
    [TestClass]
    public class RegexBasedLineParserTests
    {
        private static RegexBasedLineParser CreateParser(string regex) =>
            new(new LogMetaInformation(new LogFormat { Regex = regex }));

        [TestMethod]
        public void Parse_ExtractsAllComponents()
        {
            var parser = CreateParser(@"^(?<Time>\d+)\s(?<Level>\w+)\s(?<Message>.*)$");
            const string input = "123 INFO hello world";

            var components = parser.Parse(input).Get().ParsedLineComponents;

            Assert.AreEqual("123", Component(input, components, 0));
            Assert.AreEqual("INFO", Component(input, components, 1));
            Assert.AreEqual("hello world", Component(input, components, 2));
        }

        [TestMethod]
        public void Parse_OptionalMissingGroup_HasZeroLengthAndCarriesOffset()
        {
            // B is optional and absent -> it should collapse to an empty component, and the
            // following component must still be located correctly.
            var parser = CreateParser(@"^(?<A>\d+)(?<B>-)?(?<C>\w+)$");
            const string input = "123abc";

            var components = parser.Parse(input).Get().ParsedLineComponents;

            Assert.AreEqual("123", Component(input, components, 0));
            Assert.AreEqual(0, components.ComponentLength(1));
            Assert.AreEqual("abc", Component(input, components, 2));
        }

        [TestMethod]
        public void Parse_NonMatchingInput_Throws()
        {
            var parser = CreateParser(@"^(?<N>\d+)$");
            Assert.ThrowsExactly<InvalidOperationException>(() => parser.Parse("not a number"));
        }

        private static string Component(string input,
            LogGrokCore.Data.Monikers.ParsedLineComponents components, int index) =>
            input.Substring(components.ComponentStart(index), components.ComponentLength(index));
    }
}
