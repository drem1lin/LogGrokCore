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
            // Custom log-area palette (VS Code / terminal style) rather than the raw
            // MahApps near-black + accent-tinted selection. Consumed via DynamicResource.
            if (isDark)
            {
                Set(app, "LogGrok.Brushes.Background", Color.FromRgb(0x1E, 0x1E, 0x1E));
                Set(app, "LogGrok.Brushes.Foreground", Color.FromRgb(0xD4, 0xD4, 0xD4));
                Set(app, "LogGrok.Brushes.Selection", Color.FromRgb(0x26, 0x4F, 0x78));
                Set(app, "LogGrok.Brushes.Hover", Color.FromRgb(0x2A, 0x2D, 0x2E));
                Set(app, "AvalonDock_ThemeMetro_BaseColor5", Color.FromRgb(0xD4, 0xD4, 0xD4));
            }
            else
            {
                Set(app, "LogGrok.Brushes.Background", Color.FromRgb(0xFF, 0xFF, 0xFF));
                Set(app, "LogGrok.Brushes.Foreground", Color.FromRgb(0x1E, 0x1E, 0x1E));
                Set(app, "LogGrok.Brushes.Selection", Color.FromRgb(0xCC, 0xE8, 0xFF));
                Set(app, "LogGrok.Brushes.Hover", Color.FromRgb(0xE5, 0xF1, 0xFB));
                Set(app, "AvalonDock_ThemeMetro_BaseColor5", Color.FromRgb(0x33, 0x33, 0x33));
            }
        }

        private static void Set(Application app, string key, Color color) => app.Resources[key] = Frozen(color);

        private static SolidColorBrush Frozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
