
using System.Xml.Linq;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Microagents.Tools;

namespace Azure.Sdk.Tools.Cli.Services.Languages.Test;

public class JavaScriptTestRunner(IProcessHelper processHelper, IMicroagentHostService agentHost) : ITestRunner
{
    public string SupportedLanguage => "JavaScript";

    public async Task<string> GetTestImplementation(string packageDirectory, string testIdentifier, CancellationToken ct = default)
    {
        return await agentHost.RunAgentToCompletion(new Microagent<string>
        {
            Instructions = $"Find the implementation of this test and return the entire implementation without code fences: {testIdentifier}",
            Tools = [
                new ListFilesTool(Path.Join(packageDirectory, "test")),
                new ReadFileTool(Path.Join(packageDirectory, "test")),
            ]
        }, ct);
    }

    public async Task<TestRunResult> RunAllTests(string packageDirectory, TestMode testMode, CancellationToken ct = default)
    {
        var testModeString = testMode.ToString().ToLower();

        var options = new ProcessOptions(
            "pnpm",
            ["test"],
            logOutputStream: true,
            workingDirectory: packageDirectory,
            // Need a bit longer since recorded tests can take a while
            timeout: TimeSpan.FromMinutes(20),
            environmentVariables: new Dictionary<string, string>
            {
                ["TEST_MODE"] = testModeString,
            }
        );

        var result = await processHelper.Run(options, ct);
        var output = result.Output;

        if (result.ExitCode == 0)
        {
            return new TestRunResult(true, new List<TestFailure>());
        }

        // JS tests output results in JUnit XML format to test-results.xml in the package directory which we can parse
        var resultsFile = Path.Join(packageDirectory, "test-results.xml");
        using var fileStream = File.OpenRead(resultsFile);
        var doc = await XDocument.LoadAsync(fileStream, LoadOptions.None, ct);

        var failures =
            doc.Descendants("testcase")
                .Select(tc =>
                {
                    var failureEl = tc.Element("failure");
                    if (failureEl == null)
                    {
                        return null;
                    }

                    var className = (string?)tc.Attribute("classname");
                    var name = (string?)tc.Attribute("name");
                    var fullName = string.IsNullOrEmpty(className) ? name : $"{className} {name}";
                    var text = failureEl.Value?.Trim();
                    
                    return new TestFailure(fullName, text);
                })
                .Where(f => f != null)
                .Cast<TestFailure>()
                .ToList();

        return new TestRunResult(false, failures);
    }
}