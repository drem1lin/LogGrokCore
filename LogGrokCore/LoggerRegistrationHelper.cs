using DryIoc;
using NLog;
using NLog.Extensions.Logging;

namespace LogGrokCore
{
    public static class LoggerRegistrationHelper
    {
        private static readonly NLogLoggerProvider LoggerProvider;

        static LoggerRegistrationHelper()
        {
            // Resolve the log directory in code (with a writable fallback) and hand it to NLog,
            // so the config doesn't rely on a ProgramData path the current user may not be able
            // to create.
            GlobalDiagnosticsContext.Set("logDirectory",
                HomeDirectoryPathProvider.GetDiagnosticsDirectory("Logs"));

            var logConfigPath = PathHelpers.GetLocalFilePath("nlog.config");
            LogManager.Setup().LoadConfigurationFromFile(logConfigPath);
            LoggerProvider = new NLogLoggerProvider(new NLogProviderOptions(), LogManager.LogFactory);
        }

        public static void Register(Container container)
        {
            
            container.RegisterInstance(LoggerProvider);
            container.Register(Made.Of(
                r => ServiceInfo.Of<NLogLoggerProvider>(),
                (NLogLoggerProvider f) => f.CreateLogger(Arg.Index<string>(0)),
                request => request.Parent.ImplementationType.Name.ToString()));
        }
    }
}