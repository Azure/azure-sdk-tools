using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.Tests;

public class PythonTestRunner(IPythonHelper pythonHelper) : ITestRunner
{
    public async Task<bool> RunAllTests(string packagePath, CancellationToken ct = default)
    {
        await pythonHelper.EnsurePythonEnvironment(packagePath, null, ct);
        var venvPath = await pythonHelper.CreateVirtualEnvironment(packagePath, ct);
        var result = await pythonHelper.RunPytest(["tests"], packagePath, venvPath, ct);
        return result.ExitCode == 0;
    }
}