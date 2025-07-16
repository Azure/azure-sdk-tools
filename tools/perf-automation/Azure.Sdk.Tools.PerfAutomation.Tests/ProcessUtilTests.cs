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

        private static (string filename, string arguments) GetLoadCommand(int timeInSeconds)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string loadCommandWin = $"$sw = [Diagnostics.Stopwatch]::StartNew(); while ($sw.Elapsed.TotalSeconds -lt {timeInSeconds}) {{ 1..10000 | ForEach-Object {{ [Math]::Sqrt($_) }} }}";
                return ("powershell", loadCommandWin);
            }
            string loadCommandUnix = $"-c 'dd if=/dev/zero of=/dev/null bs=1M count={timeInSeconds * 100} 2>/dev/null'";
            return ("sh", loadCommandUnix);
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

        [Test]
        public async Task RunAsync_Stats()
        {
            (string filename, string arguments) = GetSleepCommand(3);
            ProcessResult result = await ProcessUtil.RunAsync(filename, arguments, trackStatistics: true);
            Assert.That(result.AverageCpu, Is.GreaterThan(0));
            Assert.That(result.AverageMemory, Is.GreaterThan(0));
        }
    }
}
