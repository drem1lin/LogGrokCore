using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LogGrokCore.Bootstrap
{
    public class SingleInstanceManager
    {
        private App? _app;
        private Mutex _singleInstanceMutex;
        private readonly bool _isFirstInstance;
        private const string SingleInstanceMutexName = "2A7759B1-AA14-4ABA-A05C-CFFEF9CE1D5A";
        private const string PipeName = "01DD1D8E-322A-4AA1-B9BB-E7B4A69C8986";
        private const string MessageSeparator = "|:|";
        
        public SingleInstanceManager()
        {
            _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out _isFirstInstance);
        }

        public void Run(string[] args)
        {
            if (_isFirstInstance)
            {
                using CancellationTokenSource exitCancellationTokenSource = new();
                _app = new App();
                StartListeningNextInstances(argsCommandLine => 
                    _app.OnNextInstanceStared(argsCommandLine), exitCancellationTokenSource.Token);
                _app.Run();
                exitCancellationTokenSource.Cancel();
                return;
            }

            if (args.Length > 0)
                TransferArgumentsToFirstInstance(args);
        }

        private async void StartListeningNextInstances(Action<IEnumerable<string>> onNextInstanceStarted, CancellationToken token)
        {
            // A failure handling one connection must not tear down the whole accept loop, otherwise
            // the first instance silently stops honoring "open in existing window" from then on.
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1,
                        PipeTransmissionMode.Message, PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync(token);
                    using var reader = new StreamReader(server);
                    var message = await reader.ReadToEndAsync(token);
                    Trace.TraceInformation($"Another instance started with parameters: {message}");
                    onNextInstanceStarted(ParseInstanceMessage(message));
                    server.Disconnect();
                }
                catch (Exception ex) when (ex is OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Trace.TraceError($"Error in pipe listener iteration, continuing to listen: {ex.Message}");
                }
            }
        }

        internal static IEnumerable<string> ParseInstanceMessage(string message) =>
            message.Split(MessageSeparator).Where(arg => !string.IsNullOrWhiteSpace(arg));

        private void TransferArgumentsToFirstInstance(string[] args)
        {
            Trace.TraceInformation($"Transfer parameters to another instance: {string.Join(", ", args)}");
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect();
            using var writer = new StreamWriter(client);
            writer.Write(string.Join(MessageSeparator, args));
        }
    }
}
