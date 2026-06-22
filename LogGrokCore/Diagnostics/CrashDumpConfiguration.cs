using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using Microsoft.Win32;

namespace LogGrokCore.Diagnostics
{
    /// <summary>
    /// Configures Windows Error Reporting "LocalDumps" for this executable so that
    /// crashes (including fail-fast terminations) produce a full memory dump.
    /// The registry key lives under HKLM, so writing it requires elevation; use
    /// <see cref="RequestConfigureElevated"/> from a non-elevated process.
    /// </summary>
    public static class CrashDumpConfiguration
    {
        public const string EnableWerOnlyArgument = "EnableWerOnly";
        public const string DisableWerOnlyArgument = "DisableWerOnly";

        private static readonly string KeyPath =
            $@"SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\{Path.GetFileName(Environment.ProcessPath)}";

        public static bool IsEnabled()
        {
            // Reading HKLM is normally permitted without elevation, but a locked-down policy can
            // raise SecurityException; treat any read failure as "not configured" rather than
            // crashing startup.
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(KeyPath);
                return key != null;
            }
            catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
            {
                Trace.TraceWarning($"Cannot read WER configuration: {e.Message}");
                return false;
            }
        }

        public static bool SettingsChanged(int maxDumpsCount, string dumpFolder)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(KeyPath);
                if (key == null)
                    return false;

                var countDiffers = (int)(key.GetValue("DumpCount", -1) ?? -1) != maxDumpsCount;
                var typeDiffers = (int)(key.GetValue("DumpType", -1) ?? -1) != 2;
                // DumpFolder is stored as REG_EXPAND_SZ; read it raw so we compare against the
                // unexpanded template, otherwise it would always look "changed" and re-elevate.
                var storedFolder = key.GetValue("DumpFolder", null,
                    RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                var folderDiffers = !string.Equals(storedFolder, dumpFolder, StringComparison.OrdinalIgnoreCase);

                return countDiffers || typeDiffers || folderDiffers;
            }
            catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
            {
                Trace.TraceWarning($"Cannot read WER configuration: {e.Message}");
                return false;
            }
        }

        // Requires administrative privileges (HKLM write).
        public static void Enable(string dumpFolder, int maxDumpsCount)
        {
            using var key = Registry.LocalMachine.CreateSubKey(KeyPath);
            key.SetValue("DumpFolder", dumpFolder, RegistryValueKind.ExpandString);
            key.SetValue("DumpCount", maxDumpsCount, RegistryValueKind.DWord);
            key.SetValue("DumpType", 2, RegistryValueKind.DWord); // 2 = full dump
        }

        // Requires administrative privileges (HKLM write).
        public static void Disable()
        {
            Registry.LocalMachine.DeleteSubKey(KeyPath, throwOnMissingSubKey: false);
        }

        /// <summary>
        /// Relaunches this executable elevated to (un)configure WER, because HKLM is admin-only.
        /// Returns false if elevation was declined or otherwise failed.
        /// </summary>
        public static bool RequestConfigureElevated(bool enable)
        {
            var processPath = Environment.ProcessPath;
            if (processPath == null)
                return false;

            var info = new ProcessStartInfo(processPath)
            {
                UseShellExecute = true,
                Arguments = enable ? EnableWerOnlyArgument : DisableWerOnlyArgument,
                Verb = "runas"
            };

            try
            {
                // Fire-and-forget: the elevated helper just writes the registry and exits, and the
                // running app does not depend on it completing — so do not block startup waiting
                // for the UAC prompt / child process.
                using var process = Process.Start(info);
                return process != null;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Failed to start elevated process for WER setup: {ex.Message}");
                return false;
            }
        }
    }
}
