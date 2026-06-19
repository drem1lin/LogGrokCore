using System;
using System.Threading.Tasks;

namespace LogGrokCore
{
    /// <summary>
    /// Drives the periodic "update while loading" loop used by <see cref="LogViewModel"/>.
    /// Extracted into its own type so the threading contract is testable: the loop must
    /// not use ConfigureAwait(false), so every <paramref name="update"/> callback resumes
    /// on the <see cref="System.Threading.SynchronizationContext"/> captured by the caller
    /// (the UI thread). Running an update on a thread-pool thread would touch the WPF visual
    /// tree off the UI thread and throw InvalidOperationException.
    /// </summary>
    public static class LoadingUpdater
    {
        public static async Task RunWhileAsync(
            Func<bool> isLoaded,
            Action update,
            Func<int, Task> delay,
            int initialDelayMs = 10,
            int maxDelayMs = 500)
        {
            var currentDelay = initialDelayMs;
            try
            {
                while (!isLoaded())
                {
                    update();
                    await delay(currentDelay);
                    if (currentDelay < maxDelayMs)
                        currentDelay *= 2;
                }
            }
            finally
            {
                // Always perform a final update so the view reflects the final count,
                // even if the loading loop is interrupted by an exception.
                update();
            }
        }
    }
}
