// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tools.Core;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Cli.Tools.Package
{
    /// <summary>
    /// This tool runs tests for the specified SDK package.
    /// </summary>
    [Description("Run tests for the specified SDK package")]
    [McpServerToolType]
    public class TestTool(
        ILogger<TestTool> _logger,
        IGitHelper gitHelper,
        IEnumerable<LanguageService> _languageServices
    ) : LanguageMcpTool(_languageServices, gitHelper, _logger)
    {
        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package, SharedCommandGroups.PackageTest];

        private const string RunCommandName = "run";
        private const string RunPackageTestsToolName = "azsdk_package_run_tests";

        protected override Command GetCommand() => new McpCommand(RunCommandName, "Run tests for SDK packages", RunPackageTestsToolName)
        {
            SharedOptions.PackagePath,
        };

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            var packagePath = parseResult.GetValue(SharedOptions.PackagePath);

            return await RunPackageTests(packagePath, ct);
        }

        [McpServerTool(Name = RunPackageTestsToolName), Description("Run tests for the specified SDK package. Provide package path.")]
        public async Task<TestRunResponse> RunPackageTests(string packagePath, CancellationToken ct = default)
        {
            try
            {
                logger.LogInformation("Starting tests for package at: {packagePath}", packagePath);
                var languageService = GetLanguageService(packagePath);
                var testResponse = await languageService.RunAllTests(packagePath, ct);

                await AddPackageDetailsInResponse(testResponse, packagePath, ct);
                return testResponse;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception while running package tests");
                var errorResponse = new TestRunResponse(exitCode: 1, testRunOutput: null, error: $"An unexpected error occurred while running package tests: {ex.Message}")
                {
                    NextSteps = ["Inspect the error message and attempt to resolve it"],
                };
                await AddPackageDetailsInResponse(errorResponse, packagePath, ct);
                return errorResponse;
            }
        }

        private async Task AddPackageDetailsInResponse(TestRunResponse response, string packagePath, CancellationToken ct)
        {
            try
            {
                var languageService = GetLanguageService(packagePath);
                if (languageService != null)
                {
                    var info = await languageService.GetPackageInfo(packagePath, ct);
                    response.PackageName = info.PackageName;
                    response.Version = info.PackageVersion;
                    response.PackageType = info.SdkType;
                    response.Language = info.Language;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in AddPackageDetailsInResponse");
            }
        }
    }
}
