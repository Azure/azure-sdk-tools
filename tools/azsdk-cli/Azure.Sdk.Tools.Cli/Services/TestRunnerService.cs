using System.Diagnostics;

namespace Azure.Sdk.Tools.Cli.Services;

public record TestRunOutput(string Output, int ExitCode);

public enum TestMode
{
    Record,
    Playback,
    Live
}

public interface ITestRunnerService
{
    Task<TestRunOutput> RunAllTestsAsync(TestMode testMode, CancellationToken cancellationToken = default);
}

public class JavaScriptTestRunnerService : ITestRunnerService
{
    private readonly ILogger<JavaScriptTestRunnerService> logger;

    public JavaScriptTestRunnerService(ILogger<JavaScriptTestRunnerService> logger)
    {
        this.logger = logger;
    }

    public async Task<TestRunOutput> RunAllTestsAsync(TestMode testMode, CancellationToken cancellationToken = default)
    {
        var (output, exitCode) = await RunPnpmAsync("run test:node", testMode, cancellationToken);
        return new(output, exitCode);
    }

    private async Task<(string, int)> RunPnpmAsync(string args, TestMode testMode, CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "pnpm.cmd" : "pnpm",
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = false,
            UseShellExecute = false,
            CreateNoWindow = true,
            Environment = {
                ["TEST_MODE"] = testMode.ToString().ToLower()
            },
        };

        logger.LogDebug("Starting process {info}", psi);
        using var process = Process.Start(psi);

        if (process == null)
        {
            throw new InvalidOperationException("Failed to start process for pnpm script.");
        }

        using var reader = process.StandardOutput;
        await process.WaitForExitAsync(cancellationToken);
        process.OutputDataReceived += (sender, e) => Console.Write(e.Data);
        process.ErrorDataReceived += (sender, e) => Console.Write(e.Data);
        var output = await reader.ReadToEndAsync(cancellationToken);
        logger.LogDebug("Process completed with exit code {ExitCode} and output: {Output}", process.ExitCode, output);
        return (output, process.ExitCode);
    }
}
