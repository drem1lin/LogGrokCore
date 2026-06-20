using System;
using System.Diagnostics;
using System.Text;
using System.Windows;
using LogGrokCore.Localization;

namespace LogGrokCore.Bootstrap
{
    /// <summary>
    /// Handles a fatal failure to load <c>appsettings.yaml</c> (e.g. malformed YAML —
    /// wrong indentation, tabs). The user's language preference lives inside that very
    /// file, so it cannot be read here; the message is therefore shown in every
    /// supported language and the process exits cleanly instead of crashing.
    /// </summary>
    internal static class ConfigurationLoadFailure
    {
        public static void ReportAndExit(string settingsFilePath, Exception error)
        {
            try
            {
                Trace.TraceError($"Failed to load settings file '{settingsFilePath}': {error}");

                var (caption, message) = BuildReport(settingsFilePath, error);
                MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                // Never let error reporting itself prevent a clean exit.
                Trace.TraceError($"Failed to report configuration load failure: {ex}");
            }

            Environment.Exit(1);
        }

        /// <summary>
        /// Builds the (caption, body) shown to the user. Pure and side-effect-free so it
        /// can be unit-tested without a UI. The body lists the settings path, the parser's
        /// root-cause detail, and the advice in every supported language.
        /// </summary>
        internal static (string Caption, string Message) BuildReport(string settingsFilePath, Exception error)
        {
            var caption = TranslationSource.Instance["Config_CorruptedTitle"];

            var builder = new StringBuilder();
            builder.AppendLine(settingsFilePath);
            builder.AppendLine();
            builder.AppendLine(GetRootCauseMessage(error));
            builder.AppendLine();

            foreach (var (language, text) in
                TranslationSource.Instance.GetAllTranslations("Config_CorruptedMessage"))
            {
                builder.AppendLine($"[{language.DisplayName}] {text}");
                builder.AppendLine();
            }

            return (caption, builder.ToString());
        }

        private static string GetRootCauseMessage(Exception error)
        {
            var current = error;
            while (current.InnerException != null)
                current = current.InnerException;
            return current.Message;
        }
    }
}
