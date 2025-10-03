using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.Tests;

public class JavaScriptTestRunner(IProcessHelper processHelper) : ITestRunner
{
    public SdkLanguage SupportedLanguage => SdkLanguage.JavaScript;

    public async Task RunAllTestsAsync(string packagePath, TestMode mode, CancellationToken ct = default)
    {
        var testMode = mode switch
        {
            TestMode.Live => "live",
            TestMode.Record => "record",
            _ => "playback",
        };

        await processHelper.Run(new ProcessOptions(
                unixCommand: "npm",
                unixArgs: ["run", "test"],
                windowsCommand: "npm.cmd",
                windowsArgs: ["run", "test"],
                workingDirectory: packagePath,
                environment: new Dictionary<string, string?> { ["TEST_MODE"] = testMode }
            ),
            ct
        );
    }
}