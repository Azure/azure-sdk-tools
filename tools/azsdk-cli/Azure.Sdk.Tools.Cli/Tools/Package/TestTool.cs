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
    [Description("Run tests for the specified SDK package")]
    [McpServerToolType]
    public class TestTool(
        ILogger<TestTool> logger,
        ILanguageSpecificResolver<ITestRunner> testRunnerResolver
    ) : MCPTool
    {
        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package];

        private const string TestCommandName = "test";

        protected override Command GetCommand() => new Command(TestCommandName, "Run tests for SDK packages")
        {
            SharedOptions.PackagePath,
        };

        public override async Task<CommandResponse> HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            var packagePath = ctx.ParseResult.GetValueForOption(SharedOptions.PackagePath);

            return await RunPackageTests(packagePath, ct);
        }

        [McpServerTool(Name = "azsdk_package_run_tests"), Description("Run tests for the specified SDK package. Provide package path.")]
        public async Task<DefaultCommandResponse> RunPackageTests(string packagePath, CancellationToken ct = default)
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

                await testRunner.RunAllTests(packagePath, ct);

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
