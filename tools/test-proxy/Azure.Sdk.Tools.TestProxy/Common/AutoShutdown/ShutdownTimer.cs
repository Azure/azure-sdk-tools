using System.Threading.Tasks;
using System.Threading;
using System;

namespace Azure.Sdk.Tools.TestProxy.Common.AutoShutdown
{
    public class ShutdownTimer
    {
        private readonly ShutdownConfiguration _shutdownConfig;
        private CancellationTokenSource _cts;
        private Task _shutdownTask;

        public ShutdownTimer(ShutdownConfiguration shutdownConfiguration)
        {
            _shutdownConfig = shutdownConfiguration;
        }

        public void ResetTimer()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            _shutdownTask = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(_shutdownConfig.TimeoutInSeconds * 1000, _cts.Token);

                    if (_shutdownConfig.EnableAutoShutdown)
                    {
                        System.Console.WriteLine("Server idle timeout reached. Shutting down...");
                        Environment.Exit(0);
                    }
                }
                catch (TaskCanceledException)
                {
                    // Timer was reset or canceled
                }
            });
        }
    }
}
