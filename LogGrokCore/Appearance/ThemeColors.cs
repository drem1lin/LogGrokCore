using System;
using System.Windows.Media;

namespace LogGrokCore.Appearance
{
    public static class ThemeColors
    {
        /// <summary>
        /// Lightens a foreground colour that would be too dark to read on a dark
        /// background, leaving already-bright colours untouched. Used to adapt the
        /// user-configured log colour rules when the dark theme is active.
        /// </summary>
        public static Color AdaptForegroundForDarkTheme(Color color)
        {
            var luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
            if (luminance >= 0.5)
                return color;

            // Blend toward white; the darker the source, the stronger the lift.
            var amount = Math.Clamp(0.6 - luminance, 0.0, 0.6);
            byte Lift(byte channel) => (byte)Math.Round(channel + (255 - channel) * amount);
            return Color.FromArgb(color.A, Lift(color.R), Lift(color.G), Lift(color.B));
        }
    }
}
