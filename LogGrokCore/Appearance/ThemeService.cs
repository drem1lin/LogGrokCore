using System;
using System.Windows;
using System.Windows.Media;
using ControlzEx.Theming;

namespace LogGrokCore.Appearance
{
    /// <summary>
    /// Applies the MahApps light/dark theme and keeps theme-aware resources/state in sync.
    /// In <see cref="ThemeMode.Auto"/> the theme follows the current Windows app theme.
    /// </summary>
    public static class ThemeService
    {
        /// <summary>Raised after the theme changes so theme-aware consumers can refresh.</summary>
        public static event Action? ThemeChanged;

        public static bool IsDark { get; private set; }

        public static void Apply(ThemeMode mode)
        {
            var app = Application.Current;
            if (app == null)
                return;

            var manager = ThemeManager.Current;
            if (mode == ThemeMode.Auto)
            {
                manager.ThemeSyncMode = ThemeSyncMode.SyncWithAppMode;
                manager.SyncTheme();
            }
            else
            {
                manager.ThemeSyncMode = ThemeSyncMode.DoNotSync;
                manager.ChangeTheme(app, mode == ThemeMode.Dark ? "Dark.Blue" : "Light.Blue");
            }

            var detected = manager.DetectTheme(app);
            IsDark = string.Equals(detected?.BaseColorScheme, ThemeManager.BaseColorDark, StringComparison.Ordinal);

            UpdateThemeAwareResources(app, IsDark);
            ThemeChanged?.Invoke();
        }

        private static void UpdateThemeAwareResources(Application app, bool isDark)
        {
            // AvalonDock.Themes.Metro resolves these via DynamicResource; flip with the theme.
            app.Resources["AvalonDock_ThemeMetro_BaseColor5"] =
                Frozen(isDark ? Color.FromRgb(0xE0, 0xE0, 0xE0) : Color.FromRgb(0x33, 0x33, 0x33));
        }

        private static SolidColorBrush Frozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
