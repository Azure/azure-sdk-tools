// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Tests;

namespace Azure.Sdk.Tools.Cli.Tools.Package
{
    /// <summary>
    /// This tool runs tests for the specified SDK package.
    /// </summary>
    [Description("Run tests for SDK packages")]
    [McpServerToolType]
    public class TestTool(
        ILogger<TestTool> logger,
        ILanguageSpecificResolver<ITestRunner> testRunnerResolver
    ) : MCPTool
    {
        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package];

        private const string TestCommandName = "test";

        private Option<TestMode> TestModeOption = new Option<TestMode>("--test-mode", () => TestMode.Playback)
        {
            Description = "The mode in which to run the tests. Supported modes are: Playback, Record, Live",
            IsRequired = false
        };

        protected override Command GetCommand() => new Command(TestCommandName, "Run tests for SDK packages")
        {
            SharedOptions.PackagePath,
            TestModeOption
        };

        public override async Task<CommandResponse> HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            var packagePath = ctx.ParseResult.GetValueForOption(SharedOptions.PackagePath);
            var testMode = ctx.ParseResult.GetValueForOption(TestModeOption);

            return await RunPackageTests(packagePath, testMode, ct);
        }

        [McpServerTool(Name = "azsdk_package_run_tests"), Description("Run tests for SDK packages. Provide package path.")]
        public async Task<DefaultCommandResponse> RunPackageTests(string packagePath, TestMode testMode = TestMode.Playback, CancellationToken ct = default)
        {
            try
            {
                logger.LogInformation("Starting tests for package at: {packagePath}", packagePath);
                var testRunner = await testRunnerResolver.Resolve(packagePath, ct);

                if(testRunner == null)
                {
                    logger.LogError("No test runner found for package at: {packagePath}", packagePath);
                    return new DefaultCommandResponse
                    {
                        ExitCode = 1,
                        ResponseError = $"No test runner found for package at '{packagePath}'."
                    };
                }

                await testRunner.RunAllTests(packagePath, testMode, ct);

                return new DefaultCommandResponse
                {
                    Result = $"Tests for package at '{packagePath}' completed successfully."
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception while running package tests");
                return new DefaultCommandResponse
                {
                    ExitCode = 1,
                    ResponseError = $"An unexpected error occurred while running package tests: {ex.Message}"
                };
            }
        }
    }
}
