using System.Windows.Media;
using LogGrokCore.Appearance;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokCore.Tests
{
    [TestClass]
    public class ThemeColorsTests
    {
        private static double Luminance(Color c) => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;

        [TestMethod]
        public void AdaptForegroundForDarkTheme_LightensDarkColor()
        {
            var darkRed = Color.FromRgb(0x8B, 0x00, 0x00);
            var adapted = ThemeColors.AdaptForegroundForDarkTheme(darkRed);

            Assert.IsTrue(Luminance(adapted) > Luminance(darkRed),
                "A dark foreground colour must be lightened for the dark theme.");
        }

        [TestMethod]
        public void AdaptForegroundForDarkTheme_LeavesBrightColorUnchanged()
        {
            var lightGray = Color.FromRgb(0xE0, 0xE0, 0xE0);
            Assert.AreEqual(lightGray, ThemeColors.AdaptForegroundForDarkTheme(lightGray));
        }
    }
}
