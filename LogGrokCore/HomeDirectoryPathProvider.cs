using System;
using System.Diagnostics;
using System.IO;

namespace LogGrokCore
{
    /// <summary>
    /// Resolves the folders LogGrok writes to.
    /// <para>
    /// Per-user data (settings, caches, window layout) lives under %LOCALAPPDATA%\LogGrok2 so
    /// every user has their own copy and it is always writable without administrator rights.
    /// </para>
    /// <para>
    /// Shared diagnostics (logs, crash dumps) live under %PROGRAMDATA%\LogGrok2\Users\&lt;user&gt;,
    /// i.e. a machine-wide root with a per-user subfolder, so users don't overwrite each other's
    /// files. If that location isn't writable (e.g. the installer-granted ACL is missing) it falls
    /// back to %LOCALAPPDATA% so logging never fails. <see cref="DumpFolderTemplate"/> keeps the
    /// variables unexpanded so Windows Error Reporting expands them per crashing user.
    /// </para>
    /// </summary>
    public static class HomeDirectoryPathProvider
    {
        private const string AppName = "LogGrok2";

        /// <summary>Per-user data file under %LOCALAPPDATA%\LogGrok2 (settings, caches, layout).</summary>
        public static string GetUserDataFilePath(string fileName) =>
            Path.Combine(EnsureDirectory(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName),
                fileName);

        /// <summary>
        /// Per-user diagnostics directory (e.g. "Dumps", "Logs"). Prefers the shared
        /// %PROGRAMDATA%\LogGrok2\Users\&lt;user&gt; location; falls back to %LOCALAPPDATA% if that
        /// can't be created, so diagnostics keep working without admin rights.
        /// </summary>
        public static string GetDiagnosticsDirectory(string directoryName)
        {
            var programData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                AppName, "Users", Environment.UserName, directoryName);
            if (TryCreateDirectory(programData))
                return programData;

            return EnsureDirectory(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppName, directoryName);
        }

        /// <summary>
        /// WER DumpFolder value, with environment variables left unexpanded. It is stored as a
        /// REG_EXPAND_SZ so Windows Error Reporting expands %USERNAME% in the crashing user's
        /// context, routing each user's dumps into their own subfolder under one machine-wide key.
        /// </summary>
        public static string DumpFolderTemplate =>
            $@"%PROGRAMDATA%\{AppName}\Users\%USERNAME%\Dumps";

        private static bool TryCreateDirectory(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                return true;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Diagnostics directory '{dir}' is unavailable: {ex.Message}");
                return false;
            }
        }

        private static string EnsureDirectory(params string[] parts)
        {
            var dir = Path.Combine(parts);
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
