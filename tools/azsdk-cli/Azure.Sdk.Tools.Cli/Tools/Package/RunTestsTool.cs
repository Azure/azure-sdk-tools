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
    /// This tool runs validation checks for SDK packages based on the specified check type.
    /// </summary>
    [Description("Run tests for SDK packages")]
    [McpServerToolType]
    public class RunTestsTool(
        ILogger<RunTestsTool> logger,
        ILanguageSpecificResolver languageServiceResolver
    ) : MCPTool
    {
        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package];

        private const string RunTestsCommandName = "run-tests";

        protected override Command GetCommand()
        {
            var command = new Command(RunTestsCommandName, "Run tests for SDK packages");
            // Add the package path option to the parent command so it can be used without subcommands
            command.AddOption(SharedOptions.PackagePath);
            command.SetHandler(ctx => HandleCommand(ctx, ctx.GetCancellationToken()));

            return command;
        }

        public override async Task<CommandResponse> HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            var packagePath = ctx.ParseResult.GetValueForOption(SharedOptions.PackagePath);

            return await RunPackageTests(packagePath, ct);
        }

        [McpServerTool(Name = "azsdk_package_run_tests"), Description("Run tests for SDK packages. Provide package path.")]
        public async Task<DefaultCommandResponse> RunPackageTests(string packagePath, CancellationToken ct = default)
        {
            try
            {
                logger.LogInformation("Starting tests for package at: {packagePath}", packagePath);
                var testRunner = await languageServiceResolver.Resolve<ITestRunner>(packagePath, ct);

                if(testRunner == null)
                {
                    logger.LogError("No test runner found for package at: {packagePath}", packagePath);
                    return new DefaultCommandResponse
                    {
                        ExitCode = 1,
                        Result = $"No test runner found for package at '{packagePath}'."
                    };
                }

                await testRunner.RunAllTests(packagePath, TestMode.Playback, ct);

                return new DefaultCommandResponse
                {
                    Result = $"Tests for package at '{packagePath}' completed successfully."
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception while running package tests");
                throw;
            }
        }
    }
}
