using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace LogGrokCore.Diagnostics
{
  public static class ExceptionsLogger
  {
      public static void Initialize()
      {
          AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;
          AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
          // Faults in fire-and-forget tasks would otherwise be lost (or tear down the process
          // at GC time with no log line).
          TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
      }

      /// <summary>Logs an exception that escaped to the WPF dispatcher (UI thread).</summary>
      public static void LogUnhandledDispatcherException(Exception exception)
      {
          OnException(exception, "dispatcher unhandled exception");
      }

      private static void OnFirstChanceException(object? _, FirstChanceExceptionEventArgs args)
      {
          OnException(args.Exception, "first chance exception");
      }

      private static void OnUnhandledException(object? _, UnhandledExceptionEventArgs args)
      {
          OnException(args.ExceptionObject, "unhandled exception");
      }

      private static void OnUnobservedTaskException(object? _, UnobservedTaskExceptionEventArgs args)
      {
          OnException(args.Exception, "unobserved task exception");
          args.SetObserved();
      }
      
      private static void OnException(object exceptionObj, string exceptionType)
      {
          var exception = GetException(exceptionObj);
          try
          {
              // Per-thread guard: a single global counter could be tripped by concurrent
              // exceptions on other threads and drop legitimate logs, so it is [ThreadStatic].
              if (++_isProcessingException >= MaxRecursionDeep ||
                  FirstChanceExceptionsFilter.IsKnown(exception)) return;
              Logger.Error("{0}: {1}", exceptionType, exception);
              Logger.Flush();
          }
          catch (Exception logException)
          {
               Debug.WriteLine(
                    "Failed to log {0}: {1}{2}(logException: {3})",
                    exceptionType,
                    exception,
                    Environment.NewLine,
                    logException);
          }
          finally
          {
            _isProcessingException--;
          }
      }
      
      private static Exception GetException(object exceptionObj)
      {
          return (exceptionObj as Exception) ?? UnknownException;
      }

      private const int MaxRecursionDeep = 3;

      private static readonly Exception UnknownException = new ApplicationException("An unknown exception occurred");
      
      private static readonly Logger Logger = Logger.Get(typeof(ExceptionsLogger).Namespace);

      [ThreadStatic] private static int _isProcessingException;
  } 
}
