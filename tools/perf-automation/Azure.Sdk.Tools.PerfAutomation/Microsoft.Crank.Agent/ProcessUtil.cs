// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Crank.Agent
{
    public static class ProcessUtil
    {
        [DllImport("libc", SetLastError = true, EntryPoint = "kill")]
        private static extern int sys_kill(int pid, int sig);

        public static async Task<ProcessResult> RunAsync(
            string filename,
            string arguments,
            TimeSpan? timeout = null,
            string workingDirectory = null,
            bool throwOnError = true,
            IDictionary<string, string> environmentVariables = null,
            Action<string> outputDataReceived = null,
            bool log = false,
            Action<int> onStart = null,
            Action<int> onStop = null,
            CancellationToken cancellationToken = default(CancellationToken),
            bool captureOutput = false,
            bool captureError = false,
            bool trackStatistics = false
        )
        {
            var logWorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory();

            if (log)
            {
                Log.WriteLine($"[{logWorkingDirectory}] {filename} {arguments}");
            }

            using var process = new Process()
            {
                StartInfo =
                {
                    FileName = filename,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
                EnableRaisingEvents = true
            };

            if (workingDirectory != null)
            {
                process.StartInfo.WorkingDirectory = workingDirectory;
            }

            if (environmentVariables != null)
            {
                foreach (var kvp in environmentVariables)
                {
                    process.StartInfo.Environment.Add(kvp);
                }
            }

            var outputBuilder = new StringBuilder();
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    if (captureOutput)
                    {
                        outputBuilder.AppendLine(e.Data);
                    }

                    if (outputDataReceived != null)
                    {
                        outputDataReceived.Invoke(e.Data);
                    }

                    if (log)
                    {
                        Log.WriteLine(e.Data);
                    }
                }
            };

            var errorBuilder = new StringBuilder();
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    if (captureError)
                    {
                        errorBuilder.AppendLine(e.Data);
                    }

                    if (outputDataReceived != null)
                    {
                        outputDataReceived.Invoke(e.Data);
                    }

                    Log.WriteLine("[STDERR] " + e.Data);
                }
            };

            var processLifetimeTask = new TaskCompletionSource<ProcessResult>();

            process.Exited += (_, e) =>
            {
                // Even though the Exited event has been raised, WaitForExit() must still be called to ensure the output buffers
                // have been flushed before the process is considered completely done.
                process.WaitForExit();

                if (throwOnError && process.ExitCode != 0)
                {
                    processLifetimeTask.TrySetException(new InvalidOperationException($"Command {filename} {arguments} returned exit code {process.ExitCode}"));
                }
                else
                {
                    processLifetimeTask.TrySetResult(new ProcessResult(process.ExitCode, outputBuilder.ToString(), errorBuilder.ToString()));
                }
            };

            process.Start();

            onStart?.Invoke(process.Id);

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var cancelledTcs = new TaskCompletionSource<object>();
            await using var _ = cancellationToken.Register(() => cancelledTcs.TrySetResult(null));
            Task timeoutTask = Task.Delay(timeout.HasValue ? (int)timeout.Value.TotalMilliseconds : -1);

            // For measuring statistics
            TimeSpan? lastCpuTime = null;
            DateTimeOffset lastSampleTime = DateTimeOffset.UtcNow;
            List<double> cpuSamples = [];
            List<long> memorySamples = [];

            while (!processLifetimeTask.Task.IsCompleted)
            {
                var delayTask = Task.Delay(1000);
                var completedTask = await Task.WhenAny(processLifetimeTask.Task, cancelledTcs.Task, timeoutTask, delayTask);

                if (completedTask == processLifetimeTask.Task)
                {
                    break;
                }
                else if (completedTask == cancelledTcs.Task || completedTask == timeoutTask)
                {
                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        sys_kill(process.Id, sig: 2); // SIGINT

                        var cancel = new CancellationTokenSource();

                        await Task.WhenAny(processLifetimeTask.Task, Task.Delay(TimeSpan.FromSeconds(5), cancel.Token));

                        cancel.Cancel();
                    }

                    if (!process.HasExited)
                    {
                        process.CloseMainWindow();

                        if (!process.HasExited)
                        {
                            process.Kill();
                        }
                    }
                    break;
                }

                if (trackStatistics && !process.HasExited)
                {
                    try
                    {
                        process.Refresh();
                        DateTimeOffset currentSampleTime = DateTimeOffset.UtcNow;
                        TimeSpan currentCpuTime = TimeSpan.Zero;
                        long currentMemory = 0;

                        // The python executable is called perstress. Perfstress spawns several
                        // Python processes so we need to find and track the resources on those instead.
                        if (process.ProcessName.Equals("perfstress", StringComparison.OrdinalIgnoreCase))
                        {
                            Process[] allProcesses = Process.GetProcesses();
                            foreach (Process proc in allProcesses)
                            {
                                try
                                {
                                    if (proc.ProcessName.Contains("python", StringComparison.CurrentCultureIgnoreCase))
                                    {
                                        proc.Refresh();
                                        currentCpuTime += proc.TotalProcessorTime;
                                        currentMemory += proc.WorkingSet64;
                                    }
                                }
                                catch
                                {
                                    // Process may have exited
                                }
                                finally
                                {
                                    proc.Dispose();
                                }
                            }
                        }
                        else
                        {
                            currentCpuTime = process.TotalProcessorTime;
                            currentMemory = process.WorkingSet64;
                        }

                        if (lastCpuTime.HasValue)
                        {
                            double elapsedMs = (currentSampleTime - lastSampleTime).TotalMilliseconds;
                            double cpuMs = (currentCpuTime - lastCpuTime.Value).TotalMilliseconds;

                            double cpuUsage = (cpuMs / elapsedMs / Environment.ProcessorCount) * 100.0;
                            cpuSamples.Add(cpuUsage);
                        }

                        lastCpuTime = currentCpuTime;
                        lastSampleTime = currentSampleTime;
                        memorySamples.Add(currentMemory);
                    }
                    catch (InvalidOperationException e)
                    {
                        // Process may have exited while checking stats, ignore
                        Log.WriteLine($"Exception when reading process statistics: {e.Message}");
                    }
                }
            }

            var processResult = await processLifetimeTask.Task;
            processResult.AverageCpu = cpuSamples.Count > 0 ? cpuSamples.Average() : -1;
            processResult.AverageMemory = memorySamples.Count > 0 ? (long)memorySamples.Average() : -1;

            onStop?.Invoke(processResult.ExitCode);
            return processResult;
        }

        public static async Task<T> RetryOnExceptionAsync<T>(int retries, Func<Task<T>> operation, CancellationToken cancellationToken = default)
        {
            var attempts = 0;
            do
            {
                try
                {
                    attempts++;
                    return await operation();
                }
                catch (Exception e)
                {
                    if (attempts == retries + 1 || cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }

                    Log.WriteLine($"Attempt {attempts} failed: {e.Message}");
                }
            } while (true);
        }

        public static Task RetryOnExceptionAsync(int retries, Func<Task> operation, CancellationToken cancellationToken = default)
        {
            return RetryOnExceptionAsync(retries, async () =>
            {
                await operation();
                return 0;
            }, cancellationToken);
        }
    }
}
