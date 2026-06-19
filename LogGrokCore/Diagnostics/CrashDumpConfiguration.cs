using System;
using System.Diagnostics;
using System.IO;
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
            using var key = Registry.LocalMachine.OpenSubKey(KeyPath);
            return key != null;
        }

        public static bool SettingsChanged(int maxDumpsCount)
        {
            using var key = Registry.LocalMachine.OpenSubKey(KeyPath);
            if (key == null)
                return false;
            return (int)(key.GetValue("DumpCount", -1) ?? -1) != maxDumpsCount;
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
                using var process = Process.Start(info);
                process?.WaitForExit(30000);
                return true;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Failed to start elevated process for WER setup: {ex.Message}");
                return false;
            }
        }
    }
}
