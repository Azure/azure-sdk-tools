using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.Tests;

public class JavaScriptTestRunner(IProcessHelper processHelper) : ITestRunner
{
    public async Task RunAllTests(string packagePath, CancellationToken ct = default)
    {
        await processHelper.Run(new ProcessOptions(
                unixCommand: "npm",
                unixArgs: ["run", "test"],
                windowsCommand: "npm.cmd",
                windowsArgs: ["run", "test"],
                workingDirectory: packagePath
            ),
            ct
        );
    }
}