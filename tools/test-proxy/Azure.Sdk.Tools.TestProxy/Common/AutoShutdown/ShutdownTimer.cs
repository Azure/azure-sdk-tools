using System.Threading.Tasks;
using System.Threading;
using System;

namespace Azure.Sdk.Tools.TestProxy.Common.AutoShutdown
{
    public class ShutdownTimer
    {
        private readonly int _timeoutSeconds;
        private CancellationTokenSource _cts;
        private Task _shutdownTask;

        public ShutdownTimer(int timeoutSeconds = 300)
        {
            _timeoutSeconds = timeoutSeconds;
        }

        public void ResetTimer()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            _shutdownTask = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(_timeoutSeconds * 1000, _cts.Token);

                    System.Console.WriteLine("Server idle timeout reached. Shutting down...");
                    Environment.Exit(0);
                }
                catch (TaskCanceledException)
                {
                    // Timer was reset or canceled
                }
            });
        }

        public void StopTimer()
        {
            _cts?.Cancel();
        }
    }
}
