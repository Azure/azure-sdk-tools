using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Crank.Agent;
using NUnit.Framework;

namespace Azure.Sdk.Tools.PerfAutomation.Tests
{
    public class ProcessUtilTests
    {
        private static (string filename, string arguments) GetSleepCommand(int timeInSeconds)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return ("powershell", $"Start-Sleep -Seconds {timeInSeconds}");
            }
            return ("sleep", $"{timeInSeconds}");
        }

        [Test]
        public async Task RunAsync([Values(true, false)] bool stats)
        {
            (string filename, string arguments) = GetSleepCommand(1);

            ProcessResult result = await ProcessUtil.RunAsync(filename, arguments, trackStatistics: stats);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.ExitCode, Is.EqualTo(0));
        }

        [Test]
        public void RunAsync_Timeout([Values(true, false)] bool stats)
        {
            (string filename, string arguments) = GetSleepCommand(3);

            TimeSpan timeout = TimeSpan.FromMilliseconds(100);
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await ProcessUtil.RunAsync(filename, arguments, timeout: timeout, trackStatistics: stats);
            });
        }

        [Test]
        public void RunAsync_Cancellation([Values(true, false)] bool stats)
        {
            (string filename, string arguments) = GetSleepCommand(3);

            CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await ProcessUtil.RunAsync(filename, arguments, cancellationToken: cts.Token, trackStatistics: stats);
            });
        }
    }
}
