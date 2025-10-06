namespace Azure.Sdk.Tools.Cli.Services.Tests;

public interface ITestRunner
{
    public Task RunAllTests(string packagePath, TestMode mode, CancellationToken ct = default);
}