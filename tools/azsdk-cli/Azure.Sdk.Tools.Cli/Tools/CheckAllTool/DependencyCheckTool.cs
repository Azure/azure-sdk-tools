// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
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

        private readonly Option<string> projectPathOption = new(["--project-path", "-p"], "Path to the project directory to check") { IsRequired = true };

        public DependencyCheckTool(ILogger<DependencyCheckTool> logger, IOutputService output) : base()
        {
            this.logger = logger;
            this.output = output;
            CommandHierarchy = [SharedCommandGroups.Checks];
        }

        public override Command GetCommand()
        {
            Command command = new("dependencyCheck", "Run dependency check for SDK projects");
            command.AddOption(projectPathOption);
            command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
            return command;
        }

        public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            try
            {
                var projectPath = ctx.ParseResult.GetValueForOption(projectPathOption);
                var result = await RunDependencyCheck(projectPath);

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

        [McpServerTool(Name = "RunDependencyCheck"), Description("Run dependency check for SDK projects. Provide absolute path to project root as param.")]
        public async Task<DefaultCommandResponse> RunDependencyCheck(string projectPath)
        {
            try
            {
                logger.LogInformation($"Starting dependency check for project at: {projectPath}");
                
                if (!Directory.Exists(projectPath))
                {
                    SetFailure(1);
                    return new DefaultCommandResponse
                    {
                        ResponseError = $"Project path does not exist: {projectPath}"
                    };
                }

                // Use LanguageRepoService to detect language and run appropriate dependency analysis
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                CheckResult result;
                
                try
                {
                    // Create language service using factory (detects language automatically)
                    var languageService = LanguageRepoServiceFactory.CreateService(projectPath);
                    logger.LogInformation($"Created language service: {languageService.GetType().Name}");
                    
                    // Call AnalyzeDependencies method
                    var analysisResult = await languageService.AnalyzeDependenciesAsync();
                    stopwatch.Stop();
                    
                    // Process the result dictionary from LanguageRepoService
                    bool success = false;
                    string message = "Unknown result";
                    
                    if (analysisResult.ContainsKey("success") && analysisResult["success"].Equals(true))
                    {
                        success = true;
                        message = analysisResult.ContainsKey("response") ? analysisResult["response"].ToString() : "Dependency analysis completed successfully";
                    }
                    else if (analysisResult.ContainsKey("failure") && analysisResult["failure"].Equals(true))
                    {
                        success = false;
                        message = analysisResult.ContainsKey("response") ? analysisResult["response"].ToString() : "Dependency analysis failed";
                    }
                    else if (analysisResult.ContainsKey("cookbook"))
                    {
                        success = false;
                        var cookbook = analysisResult["cookbook"].ToString();
                        var response = analysisResult.ContainsKey("response") ? analysisResult["response"].ToString() : "";
                        message = $"See cookbook reference: {cookbook}. {response}";
                    }
                    
                    result = new CheckResult
                    {
                        CheckType = "Dependency Check",
                        Success = success,
                        Message = message,
                        Duration = stopwatch.ElapsedMilliseconds
                    };
                    
                    if (!success)
                    {
                        SetFailure(1);
                    }
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    logger.LogError(ex, "Error during language-specific dependency analysis");
                    SetFailure(1);
                    
                    result = new CheckResult
                    {
                        CheckType = "Dependency Check",
                        Success = false,
                        Message = $"Error during dependency analysis: {ex.Message}",
                        Duration = stopwatch.ElapsedMilliseconds
                    };
                }

                return new DefaultCommandResponse
                {
                    Message = result.Message,
                    Duration = result.Duration,
                    Result = result
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception while running dependency check");
                SetFailure(1);
                return new DefaultCommandResponse
                {
                    ResponseError = $"Unhandled exception: {ex.Message}"
                };
            }
        }
    }
}