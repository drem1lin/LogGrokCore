using System.Collections.Concurrent;
using System.Reflection;
using NLog;

namespace LogGrokCore.Diagnostics
{
  public class Logger
  {
      private readonly string _component;
      private static readonly ConcurrentDictionary<string, Logger> LoggersCache = new();
      private static readonly NLog.Logger NLogLogger;      

      static Logger()
      {
          NLogLogger = LogManager.GetCurrentClassLogger();
          var entryAssembly = Assembly.GetEntryAssembly();
          GlobalDiagnosticsContext.Set("EntryAssembly", entryAssembly?.FullName);
          // Populate the log-header "Version:" field so crash reports show which build produced them.
          GlobalDiagnosticsContext.Set("DeploymentVersion",
              entryAssembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
              ?? entryAssembly?.GetName().Version?.ToString());
      }
      
      private Logger (string component)
      {
          _component = component;
      }
      
      public static Logger Get(string? component = null)
      {
          var comp = component ?? ComponentProvider.DetectCurrentComponent();
          return LoggersCache.GetOrAdd(comp, c => new Logger(c));
      }
      
      public static void FlushAll() => LogManager.Flush();

      // Bounded flush: avoids hanging process exit on a stuck async target (LogManager.Flush()
      // otherwise waits the full default async timeout).
      public static void FlushAll(System.TimeSpan timeout) => LogManager.Flush(timeout);
            
      public void Debug(string message,  params object[] args) => Log(LogLevel.Debug, message, args); 
     
      public void Info(string message,  params object[] args) => Log(LogLevel.Info, message, args);

      public void Warn(string message, params object[] args) => Log(LogLevel.Warn, message, args);
      
      public void Error(string message, params object[] args) => Log(LogLevel.Error, message, args);

      public void Flush() => LogManager.Flush();
      
      private void Log( LogLevel level, string message, object[] args) 
      {
          var logEvent = LogEventInfo.Create(level, NLogLogger.Name, null, message, args);
          
          logEvent.Properties["component"] = _component;
          logEvent.Properties["levelShort"] = LogLevelFormatter.Format(level);
          
          NLogLogger.Log(logEvent);                    
      }
  }
}
