using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness.Utilities;

internal sealed class ProcessRunner : IDisposable
{
    private readonly Process process;
    private readonly TimeSpan timeout;
    private readonly TaskCompletionSource<string> tcs;
    private readonly TaskCompletionSource<ICollection<string>> outputTcs;
    private readonly TaskCompletionSource<ICollection<string>> errorTcs;
    private readonly ICollection<string> outputData;
    private readonly ICollection<string> errorData;

    private readonly CancellationToken cancellationToken;
    private readonly CancellationTokenSource timeoutCts;
    private CancellationTokenRegistration ctRegistration;
    public int ExitCode => this.process.ExitCode;

    public ProcessRunner(Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        this.process = process;
        this.timeout = timeout;

        this.outputData = new List<string>();
        this.errorData = new List<string>();
        this.outputTcs = new TaskCompletionSource<ICollection<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        this.errorTcs = new TaskCompletionSource<ICollection<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        this.tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (timeout.TotalMilliseconds >= 0)
        {
            this.timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            this.cancellationToken = this.timeoutCts.Token;
        }
        else
        {
            this.cancellationToken = cancellationToken;
        }
    }

    public Task<string> RunAsync()
    {
        StartProcess();
        return this.tcs.Task;
    }

    public string Run()
    {
        StartProcess();
        return this.tcs.Task.GetAwaiter().GetResult();
    }

    private void StartProcess()
    {
        if (TrySetCanceled() || this.tcs.Task.IsCompleted)
        {
            return;
        }

        this.process.StartInfo.UseShellExecute = false;
        this.process.StartInfo.RedirectStandardOutput = true;
        this.process.StartInfo.RedirectStandardError = true;

        this.process.OutputDataReceived += (sender, args) => OnDataReceived(args, this.outputData, this.outputTcs);
        this.process.ErrorDataReceived += (sender, args) => OnDataReceived(args, this.errorData, this.errorTcs);
        this.process.Exited += (o, e) => _ = HandleExitAsync();

        this.timeoutCts?.CancelAfter(this.timeout);

        if (!this.process.Start())
        {
            TrySetException(new InvalidOperationException($"Failed to start process '{this.process.StartInfo.FileName}'"));
        }

        this.process.BeginOutputReadLine();
        this.process.BeginErrorReadLine();
        this.ctRegistration = this.cancellationToken.Register(HandleCancel, false);
    }

    private async ValueTask HandleExitAsync()
    {
        if (this.process.ExitCode == 0)
        {
            ICollection<string> output = await this.outputTcs.Task.ConfigureAwait(false);
            TrySetResult(string.Join(Environment.NewLine, output));
        }
        else
        {
            ICollection<string> error = await this.errorTcs.Task.ConfigureAwait(false);
            TrySetException(new InvalidOperationException(string.Join(Environment.NewLine, error)));
        }
    }

    private void HandleCancel()
    {
        if (this.tcs.Task.IsCompleted)
        {
            return;
        }

        if (!this.process.HasExited)
        {
            try
            {
                this.process.Kill();
            }
            catch (Exception ex)
            {
                TrySetException(ex);
                return;
            }
        }

        TrySetCanceled();
    }

    private static void OnDataReceived(DataReceivedEventArgs args, ICollection<string> data, TaskCompletionSource<ICollection<string>> tcs)
    {
        if (args.Data != null)
        {
            data.Add(args.Data);
        }
        else
        {
            tcs.SetResult(data);
        }
    }

    private void TrySetResult(string result)
    {
        this.tcs.TrySetResult(result);
    }

    private bool TrySetCanceled()
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            this.tcs.TrySetCanceled(this.cancellationToken);
        }

        return this.cancellationToken.IsCancellationRequested;
    }

    private void TrySetException(Exception exception)
    {
        this.tcs.TrySetException(exception);
    }

    public void Dispose()
    {
        this.tcs.TrySetCanceled();
        this.process.Dispose();
        this.ctRegistration.Dispose();
        this.timeoutCts?.Dispose();
    }
}
