#nullable enable
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LogGrokCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokCore.Tests
{
    [TestClass]
    public class LoadingUpdaterTests
    {
        // Minimal single-threaded message pump, so the test can assert that awaited
        // continuations resume on the very thread that started the loop - the analogue
        // of the WPF UI thread.
        private sealed class SingleThreadSynchronizationContext : SynchronizationContext
        {
            private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();

            public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));

            public override void Send(SendOrPostCallback d, object? state) =>
                throw new NotSupportedException();

            public void Complete() => _queue.CompleteAdding();

            public void Pump()
            {
                foreach (var item in _queue.GetConsumingEnumerable())
                    item.Callback(item.State);
            }
        }

        [TestMethod]
        public void RunWhileAsync_InvokesEveryUpdateOnCapturedSynchronizationContext()
        {
            var updateThreadIds = new ConcurrentBag<int>();
            var sync = new SingleThreadSynchronizationContext();
            var pumpThreadId = 0;
            Exception? failure = null;

            var pump = new Thread(() =>
            {
                SynchronizationContext.SetSynchronizationContext(sync);
                pumpThreadId = Environment.CurrentManagedThreadId;

                var checks = 0;
                Func<bool> isLoaded = () => checks++ >= 3;
                Action update = () => updateThreadIds.Add(Environment.CurrentManagedThreadId);
                // A real delay so the await genuinely suspends and the continuation must be
                // posted back to the captured context (rather than completing synchronously).
                Func<int, Task> delay = ms => Task.Delay(ms);

                var task = LoadingUpdater.RunWhileAsync(isLoaded, update, delay);
                task.ContinueWith(
                    t =>
                    {
                        failure = t.Exception?.GetBaseException();
                        sync.Complete();
                    },
                    TaskScheduler.Default);

                sync.Pump();
            })
            {
                IsBackground = true
            };

            pump.Start();
            Assert.IsTrue(pump.Join(TimeSpan.FromSeconds(10)), "Loading loop did not finish in time.");

            Assert.IsNull(failure, $"RunWhileAsync threw: {failure}");
            Assert.IsTrue(updateThreadIds.Count > 1, "Expected several update callbacks across loop iterations.");
            Assert.IsTrue(
                updateThreadIds.All(id => id == pumpThreadId),
                "Every update must run on the captured (UI) thread. " +
                $"Saw thread ids: [{string.Join(",", updateThreadIds.Distinct())}], pump thread: {pumpThreadId}. " +
                "A non-matching id means a ConfigureAwait(false) leaked the continuation onto the thread pool.");
        }
    }
}
