using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Services.Tests;

public class JavaScriptTestRunner(IProcessHelper processHelper) : ITestRunner
{
    public async Task<bool> RunAllTests(string packagePath, CancellationToken ct = default)
    {
        var result = await processHelper.Run(new ProcessOptions(
                command: "npm",
                args: ["run", "test"],
                workingDirectory: packagePath
            ),
            ct
        );

        return result.ExitCode == 0;
    }
}