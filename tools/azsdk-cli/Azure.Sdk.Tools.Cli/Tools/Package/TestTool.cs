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
        IEnumerable<LanguageService> _languageServices,
        IEnvFileHelper _envFileHelper
    ) : LanguageMcpTool(_languageServices, gitHelper, _logger)
    {
        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package, SharedCommandGroups.PackageTest];

        private const string RunCommandName = "run";
        private const string RunPackageTestsToolName = "azsdk_package_run_tests";

        private static readonly Option<TestMode> TestModeOption = new("--mode", "-m")
        {
            Description = "Test mode - playback, record, or live (default: playback)",
            Required = false,
            DefaultValueFactory = _ => TestMode.Playback,
        };

        private static readonly Option<string?> TestEnvironmentOption = new("--test-environment")
        {
            Description = "Path to a .env file containing deployment environment variables for live/record test runs",
            Required = false,
        };

        private static readonly Option<int?> TimeoutOption = new("--timeout", "-t")
        {
            Description = "Maximum time in seconds to wait for the test run to complete",
            Required = false,
        };

        protected override Command GetCommand() => new McpCommand(RunCommandName, "Run tests for SDK packages", RunPackageTestsToolName)
        {
            SharedOptions.PackagePath,
            TestModeOption,
            TestEnvironmentOption,
            TimeoutOption,
        };

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            var packagePath = parseResult.GetValue(SharedOptions.PackagePath);
            var testMode = parseResult.GetValue(TestModeOption);
            var testEnvironmentPath = parseResult.GetValue(TestEnvironmentOption);
            var timeoutSeconds = parseResult.GetValue(TimeoutOption);

            return await RunPackageTests(packagePath, testMode, testEnvironmentPath, timeoutSeconds, ct);
        }

        [McpServerTool(Name = RunPackageTestsToolName), Description("Run tests for the specified SDK package. Provide package path.")]
        public async Task<TestRunResponse> RunPackageTests(
            string packagePath,
            TestMode testMode = TestMode.Playback,
            string? testEnvironmentPath = null,
            int? timeoutSeconds = null,
            CancellationToken ct = default)
        {
            try
            {
                IDictionary<string, string>? liveTestEnvironment = null;
                if (!string.IsNullOrEmpty(testEnvironmentPath))
                {
                    try
                    {
                        liveTestEnvironment = _envFileHelper.ParseEnvFile(testEnvironmentPath);
                        logger.LogInformation("Loaded {count} environment variables from {path}", liveTestEnvironment.Count, testEnvironmentPath);
                    }
                    catch (FileNotFoundException)
                    {
                        return new TestRunResponse(
                            exitCode: 1,
                            testRunOutput: null,
                            error: $"Test environment file not found: {testEnvironmentPath}")
                        {
                            NextSteps = ["Verify the path to the .env file is correct"],
                        };
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        logger.LogError(ex, "Unauthorized access while reading test environment file: {path}", testEnvironmentPath);
                        return new TestRunResponse(
                            exitCode: 1,
                            testRunOutput: null,
                            error: $"Insufficient permissions to read test environment file: {testEnvironmentPath}")
                        {
                            NextSteps =
                            [
                                "Verify that you have read access to the .env file",
                                "Check file system permissions or try running the command with appropriate privileges",
                            ],
                        };
                    }
                    catch (IOException ex)
                    {
                        logger.LogError(ex, "I/O error while reading test environment file: {path}", testEnvironmentPath);
                        return new TestRunResponse(
                            exitCode: 1,
                            testRunOutput: null,
                            error: $"Failed to read test environment file: {testEnvironmentPath}")
                        {
                            NextSteps =
                            [
                                "Verify that the .env file is accessible and not locked by another process",
                                "Check that the file is not corrupted and is located on an available disk",
                            ],
                        };
                    }
                }

                logger.LogInformation("Starting tests for package at: {packagePath} in {testMode} mode", packagePath, testMode);
                var languageService = await GetLanguageServiceAsync(packagePath, ct);
                if (languageService == null)
                {
                    logger.LogError("No language service found for package at: {packagePath}", packagePath);
                    return new TestRunResponse(
                        exitCode: 1,
                        testRunOutput: null,
                        error: $"Unsupported language or invalid package path: {packagePath}")
                    {
                        NextSteps = ["Verify the package path is correct and that the language is supported"],
                    };
                }
                var timeout = timeoutSeconds.HasValue ? TimeSpan.FromSeconds(timeoutSeconds.Value) : (TimeSpan?)null;
                var testResponse = await languageService.RunAllTests(packagePath, testMode, liveTestEnvironment, timeout, ct);

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
                var languageService = await GetLanguageServiceAsync(packagePath, ct);
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
