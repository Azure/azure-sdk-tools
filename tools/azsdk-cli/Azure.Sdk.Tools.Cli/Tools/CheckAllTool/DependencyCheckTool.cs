// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.CheckAllTool
{
    /// <summary>
    /// This tool runs dependency check for SDK projects.
    /// </summary>
    [Description("Run dependency check for SDK projects")]
    [McpServerToolType]
    public class DependencyCheckTool : MCPTool
    {
        private readonly ILogger<DependencyCheckTool> logger;
        private readonly IOutputService output;
        private readonly IGitHelper gitHelper;

        private readonly Option<string> packagePathOption = new(["--package-path", "-p"], "Path to the package directory to check") { IsRequired = true };

        public DependencyCheckTool(ILogger<DependencyCheckTool> logger, IOutputService output, IGitHelper gitHelper) : base()
        {
            this.logger = logger;
            this.output = output;
            this.gitHelper = gitHelper;
            CommandHierarchy = [SharedCommandGroups.Checks];
        }

        public override Command GetCommand()
        {
            Command command = new("dependency-check", "Run dependency check for SDK projects");
            command.AddOption(packagePathOption);
            command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
            return command;
        }

        public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            try
            {
                var packagePath = ctx.ParseResult.GetValueForOption(packagePathOption);
                var result = await RunDependencyCheck(packagePath);

                output.Output(result);
                ctx.ExitCode = ExitCode;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while running dependency check");
                SetFailure(1);
                output.Output(new DefaultCommandResponse
                {
                    ResponseError = $"Error occurred while running dependency check: {ex.Message}"
                });
                ctx.ExitCode = ExitCode;
            }
        }

        [McpServerTool(Name = "RunDependencyCheck"), Description("Run dependency check for SDK packages. Provide absolute path to package root as param.")]
        public async Task<ICLICheckResponse> RunDependencyCheck(string packagePath)
        {
            try
            {
                logger.LogInformation($"Starting dependency check for package at: {packagePath}");
                
                if (!Directory.Exists(packagePath))
                {
                    SetFailure(1);
                    return new FailureCLICheckResponse(1, "", $"Package path does not exist: {packagePath}");
                }

                // Use LanguageRepoService to detect language and run appropriate dependency analysis
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                ICLICheckResponse result;
                
                try
                {
                    // Discover the repository root from the project path
                    logger.LogInformation($"Discovering repository root from project path: {projectPath}");
                    var repoRootPath = gitHelper.DiscoverRepoRoot(projectPath);
                    logger.LogInformation($"Discovered repository root: {repoRootPath}");
                    
                    // Create language service using factory (detects language automatically)
                    logger.LogInformation($"Creating language service for repository at: {repoRootPath}");
                    var languageService = LanguageRepoServiceFactory.CreateService(repoRootPath, logger);
                    logger.LogInformation($"Created language service: {languageService.GetType().Name}");
                    
                    // Call AnalyzeDependencies method
                    result = await languageService.AnalyzeDependenciesAsync();
                    stopwatch.Stop();
                    
                    if (result.ExitCode != 0)
                    {
                        SetFailure(1);
                    }
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    logger.LogError(ex, "Error during language-specific dependency analysis");
                    SetFailure(1);
                    
                    result = new FailureCLICheckResponse(1, $"Error during dependency analysis: {ex.Message}");
                }

                // Create response with timing information, preserving the actual result type
                var responseData = new
                {
                    Message = result.Output ?? "Dependency check completed",
                    Duration = stopwatch.ElapsedMilliseconds,
                    OriginalOutput = result.Output
                };
                
                string serializedResponse = System.Text.Json.JsonSerializer.Serialize(responseData);
                
                // Return appropriate response type based on the actual result
                return result.ExitCode == 0 
                    ? new SuccessCLICheckResponse(result.ExitCode, serializedResponse)
                    : new FailureCLICheckResponse(result.ExitCode, serializedResponse, result is FailureCLICheckResponse failure ? failure.Error : "Check failed");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception while running dependency check");
                SetFailure(1);
                return new FailureCLICheckResponse(1, "", $"Unhandled exception: {ex.Message}");
            }
        }
    }
}