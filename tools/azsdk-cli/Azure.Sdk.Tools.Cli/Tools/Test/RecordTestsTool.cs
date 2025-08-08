// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Microagents.Tools;
using Azure.Sdk.Tools.Cli.Services;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools;

[McpServerToolType, Description("Records tests and provides suggestions to fix test failures")]
public class RecordTestsTool(ITestRunnerService testService, IGitHelper gitHelper, ILogger<RecordTestsTool> logger, IMicroagentHostService agentHost) : MCPTool()
{
    private readonly Option<bool> skipInitialRun = new(["--skip-initial-run"], () => false, "Skip the initial sanity-check run of the live tests");

    public override Command GetCommand()
    {
        var summarizeFilesCommand = new Command("record-tests", "record tests agentically")
        {
            // pathOpt,
            // recordingPathOpt,
        };
        summarizeFilesCommand.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
        return summarizeFilesCommand;
    }

    private class TestFailure
    {
        [Description("The name of the test that failed")]
        public string TestName { get; set; }

        [Description("The full error message, verbatim, from the test run")]
        public string ErrorMessage { get; set; }

        [Description("A one-sentence human-readable summary of the failure reason")]
        public string HumanReadableError { get; set; }
    }

    public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
    {
        if (!ctx.ParseResult.GetValueForOption(skipInitialRun))
        {
            logger.LogInformation("Running live tests");
            var liveTestResults = await testService.RunAllTestsAsync(TestMode.Live, ct);
            if (liveTestResults.ExitCode != 0)
            {
                logger.LogInformation("Live tests are failing. Please fix live test failures before attempting to record tests.");
                var liveModeFailures = await agentHost.RunAgentToCompletionAsync(new Microagent<List<TestFailure>>(
                    SystemPrompt: $"Summarize the test failures from the provided test run output. If there are no failures that's okay, just return an empty list. Skipped tests are not failures. Output:\n{liveTestResults.Output}"
                ), ct);

                if (liveModeFailures.Count > 0)
                {
                    foreach (var failure in liveModeFailures)
                    {
                        logger.LogInformation("Test failed: {TestName}; Reason: {FailureReason}", failure.TestName, failure.HumanReadableError);
                    }
                }

                ctx.ExitCode = liveTestResults.ExitCode;
                return;
            }
        }

        var sanitizerConfigCode = await agentHost.RunAgentToCompletionAsync(new Microagent<string>(
            SystemPrompt: $"Find where the sanitizer options are set up in these tests and the environment variable playback setup and output the code",
            Tools: [
                new ListFilesTool(Path.Join(Environment.CurrentDirectory, "test")),
                new ReadFileTool(Path.Join(Environment.CurrentDirectory, "test")),
            ]
        ), ct);

        await RunAndFixRecordModeAsync(sanitizerConfigCode, ct);
        await RunAndFixPlaybackModeAsync(sanitizerConfigCode, ct);
    }

    private async Task RunAndFixPlaybackModeAsync(string sanitizerConfigCode, CancellationToken ct)
    {
        while (true)
        {
            logger.LogInformation("Running tests in playback mode");
            var playbackTestResults = await testService.RunAllTestsAsync(TestMode.Playback, ct);

            if (playbackTestResults.ExitCode == 0)
            {
                logger.LogInformation("Tests passed in playback mode.");
                break;
            }

            var playbackTestFailures = await agentHost.RunAgentToCompletionAsync(new Microagent<List<TestFailure>>(
                SystemPrompt: $"Summarize the test failures from the provided test run output. If there are no failures that's okay, just return an empty list. Skipped tests are not failures. Output:\n{playbackTestResults.Output}"
            ), ct);

            var testFailure = playbackTestFailures[0];
            logger.LogInformation("Attempting to fix failure: {TestName}; failure reason: {FailureReason}", testFailure.TestName, testFailure.HumanReadableError);

            logger.LogInformation("Extracting test implementation");
            var implementation = await agentHost.RunAgentToCompletionAsync(new Microagent<string>(
                SystemPrompt: $"Find the implementation of this test and return the entire implementation without code fences: {testFailure.TestName}",
                Tools: [
                    new ListFilesTool(Path.Join(Environment.CurrentDirectory, "test")),
                    new ReadFileTool(Path.Join(Environment.CurrentDirectory, "test")),
                ]
            ), ct);

            var assetsDir = Path.Join(gitHelper.DiscoverRepoRoot(Environment.CurrentDirectory), ".assets");
            var recordingJsonFiles = Directory.GetFileSystemEntries(assetsDir, "*.json", SearchOption.AllDirectories);

            var recordingFilePath = await agentHost.RunAgentToCompletionAsync(new Microagent<string>($"""
                Out of these recording file paths, identify the file path that corresponds to the test named {testFailure.TestName}:

                {string.Join(Environment.NewLine, recordingJsonFiles)}
                """
            ), ct);

            var recordingFileContent = await File.ReadAllTextAsync(recordingFilePath, ct);

            // TODO: provide tools to give the agent context e.g. let it look up the source code, other recordings, see sanitizer implementation, etc.
            var recommendation = await agentHost.RunAgentToCompletionAsync(new Microagent<string>(
                SystemPrompt: $"""
                You are a TypeScript expert. Based on the following, what could be the issue causing the test failure?
                The issue may relate to:
                - Over-sanitization: test recordings are sanitized, and sometimes over-sanitization may lead to values in one request/response not matching subsequent request/responses.

                <TestFailureMessage>
                {testFailure.ErrorMessage}
                </TestFailureMessage>

                <TestImplementation>
                {implementation}
                </TestImplementation>

                <SanitizerConfiguration>
                {sanitizerConfigCode}
                </SanitizerConfiguration>

                <RecordingFileContent>
                {recordingFileContent}
                </RecordingFileContent>
                """
            ));

            logger.LogInformation("This is the recommendation to fix the test:");
            logger.LogInformation(recommendation);
            Console.ReadKey();

            await RunAndFixRecordModeAsync(sanitizerConfigCode, ct);
        }
    }


    private async Task RunAndFixRecordModeAsync(string sanitizerConfiguration, CancellationToken ct)
    {
        // Loop until tests are passing in record mode
        while (true)
        {
            logger.LogInformation("Running tests in record mode");
            var recordedTestResults = await testService.RunAllTestsAsync(TestMode.Record, ct);

            if (recordedTestResults.ExitCode == 0)
            {
                logger.LogInformation("Tests passed in record mode. Recording successful.");
                break;
            }

            var recordedTestFailures = await agentHost.RunAgentToCompletionAsync(new Microagent<List<TestFailure>>(
                SystemPrompt: $"Summarize the test failures from the provided test run output. If there are no failures that's okay, just return an empty list. Skipped tests are not failures. Output:\n{recordedTestResults.Output}"
            ), ct);

            if (recordedTestFailures.Count == 0)
            {
                logger.LogInformation("No failures reported, but exit code was non-zero. Investigate manually.");
                break;
            }

            var testFailure = recordedTestFailures[0];
            logger.LogInformation("Attempting to fix failure: {TestName}; failure reason: {FailureReason}", testFailure.TestName, testFailure.HumanReadableError);

            logger.LogInformation("Extracting test implementation");
            var implementation = await agentHost.RunAgentToCompletionAsync(new Microagent<string>(
                SystemPrompt: $"Find the implementation of this test and return the entire implementation without code fences: {testFailure.TestName}",
                Tools: [
                new ListFilesTool(Path.Join(Environment.CurrentDirectory, "test")),
            new ReadFileTool(Path.Join(Environment.CurrentDirectory, "test")),
                ]
            ), ct);

            var recommendation = await agentHost.RunAgentToCompletionAsync(new Microagent<string>(
                SystemPrompt: $"""
            You are a TypeScript expert. Based on the following, what could be the issue causing the test failure?
            The issue may relate to:
            - Over-sanitization: test recordings are sanitized, and sometimes over-sanitization may lead to values in one request/response not matching subsequent request/responses.

            <TestFailureMessage>
            {testFailure.ErrorMessage}
            </TestFailureMessage>

            <TestImplementation>
            {implementation}
            </TestImplementation>

            <SanitizerConfiguration>
            {sanitizerConfiguration}
            </SanitizerConfiguration>
            """
            ));

            logger.LogInformation("This is the recommendation to fix the test, try applying the recommendation and then press any key to continue:");
            logger.LogInformation(recommendation);
            Console.ReadKey();
        }
    }
}
