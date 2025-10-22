using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.Tests;

public class PythonTestRunner(IProcessHelper processHelper) : ITestRunner
{
    public async Task<bool> RunAllTests(string packagePath, CancellationToken ct = default)
    {
        var result = await processHelper.Run(new ProcessOptions(
                command: "pytest",
                args: ["tests"],
                workingDirectory: packagePath
            ),
            ct
        );

        return result.ExitCode == 0;
    }
}