using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using LogGrokCore.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokCore.Tests
{
    [TestClass]
    public class ConverterTests
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        [TestMethod]
        public void StringShortener_ShortString_ReturnedUnchanged()
        {
            var converter = new StringShortenerConvereter();
            Assert.AreEqual("abc", converter.Convert("abc", typeof(string), "10", Culture));
        }

        [TestMethod]
        public void StringShortener_LongString_IsShortenedWithEllipsis()
        {
            var converter = new StringShortenerConvereter();
            // length 10, max 4 -> first 2 + ellipsis + last 2
            Assert.AreEqual("ab…ij", converter.Convert("abcdefghij", typeof(string), "4", Culture));
        }

        [TestMethod]
        public void StringShortener_UnsetValue_ReturnsBindingDoNothing()
        {
            var converter = new StringShortenerConvereter();
            // Regression: used to throw InvalidOperationException on UnsetValue,
            // breaking bindings while values are being resolved.
            Assert.AreSame(Binding.DoNothing,
                converter.Convert(DependencyProperty.UnsetValue, typeof(string), "10", Culture));
        }

        [TestMethod]
        public void StringShortener_ConvertBack_Throws()
        {
            var converter = new StringShortenerConvereter();
            Assert.ThrowsExactly<NotSupportedException>(
                () => converter.ConvertBack("x", typeof(string), "10", Culture));
        }

        [TestMethod]
        public void IsNullToVisibility_NullCollapsed_NonNullVisible()
        {
            var converter = new IsNullToVisibilityConverter();
            Assert.AreEqual(Visibility.Collapsed, converter.Convert(null, typeof(Visibility), null!, Culture));
            Assert.AreEqual(Visibility.Visible, converter.Convert("x", typeof(Visibility), null!, Culture));
        }

        [TestMethod]
        public void IsNullToVisibility_ConvertBack_Throws()
        {
            var converter = new IsNullToVisibilityConverter();
            Assert.ThrowsExactly<NotSupportedException>(
                () => converter.ConvertBack(Visibility.Visible, typeof(object), null!, Culture));
        }

        [TestMethod]
        public void ObjectToType_ReturnsRuntimeType_NullForNull()
        {
            var converter = new ObjectToTypeConverter();
            Assert.AreEqual(typeof(string), converter.Convert("x", typeof(Type), null!, Culture));
            Assert.IsNull(converter.Convert(null, typeof(Type), null!, Culture));
        }

        [TestMethod]
        public void ObjectToType_ConvertBack_Throws()
        {
            var converter = new ObjectToTypeConverter();
            Assert.ThrowsExactly<NotSupportedException>(
                () => converter.ConvertBack(typeof(string), typeof(object), null!, Culture));
        }

        [TestMethod]
        public void FormatTextMulti_FormatsWithSuppliedString()
        {
            var converter = new FormatTextMultiExtension();
            var result = converter.Convert(new object[] { "world", "Hello {0}" }, typeof(string), null!, Culture);
            Assert.AreEqual("Hello world", result);
        }

        [TestMethod]
        public void FormatTextMulti_FewerThanTwoValues_ReturnsFirstOrEmpty()
        {
            var converter = new FormatTextMultiExtension();
            Assert.AreEqual("only", converter.Convert(new object[] { "only" }, typeof(string), null!, Culture));
            Assert.AreEqual(string.Empty, converter.Convert(Array.Empty<object>(), typeof(string), null!, Culture));
        }

        [TestMethod]
        public void FormatTextMulti_ConvertBack_Throws()
        {
            var converter = new FormatTextMultiExtension();
            Assert.ThrowsExactly<NotSupportedException>(
                () => converter.ConvertBack("x", new[] { typeof(string) }, null!, Culture));
        }
    }
}
