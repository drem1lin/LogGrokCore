using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LogGrokCore.Data
{
    public class Loader : IDisposable
    {
        private readonly Task _loadingTask;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private const int BufferSize = 1024*1024;

        public Loader(
            LogFile logFile,
            ILineDataConsumer lineProcessor,
            ILogger logger)
        {
            var encoding = logFile.Encoding;
            var loaderImpl = new LoaderImpl(BufferSize, lineProcessor);
            _cancellationTokenSource = new CancellationTokenSource();
            
            var fileSize = logFile.FileSize;
            Trace.TraceInformation(
                $"Start loading {logFile.FilePath}, size: {fileSize:N0} bytes, encoding: {encoding.WebName}.");
            var stopwatch = Stopwatch.StartNew();
            _loadingTask = Task.Factory.StartNew(
                () =>
                {
                    using var stream = logFile.OpenForSequentialRead();
                    loaderImpl.Load(stream,
                        encoding.GetBytes("\r"), encoding.GetBytes("\n"),
                        _cancellationTokenSource.Token);
                })
                .ContinueWith(t =>
                {
                    var elapsed = stopwatch.Elapsed;
                    switch(t.Status)
                    {
                        case TaskStatus.RanToCompletion:
                            var mbPerSec = elapsed.TotalSeconds > 0
                                ? fileSize / (1024.0 * 1024.0) / elapsed.TotalSeconds
                                : 0;
                            Trace.TraceInformation(
                                $"Loaded {logFile.FilePath}, size: {fileSize:N0} bytes, " +
                                $"time spent: {elapsed}, throughput: {mbPerSec:N1} MB/s.");
                            break;
                        case TaskStatus.Canceled: logger.LogInformation($"Loading of {logFile.FilePath} was cancelled after {elapsed}.");
                            break;
                        default:
                            Trace.TraceError($"Unexpected loading result {t.Status} while loading {logFile.FilePath}: {t.Exception?.GetBaseException().Message}");
                            break;
                    }
                });
        }

        public bool IsLoading => !_loadingTask.IsCompleted;

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            if (!_loadingTask.Wait(TimeSpan.FromSeconds(10)))
            {
                Trace.TraceWarning("Loading task did not complete within timeout during Dispose.");
            }
            _loadingTask.Dispose();
            _cancellationTokenSource.Dispose();
        }
    }
}