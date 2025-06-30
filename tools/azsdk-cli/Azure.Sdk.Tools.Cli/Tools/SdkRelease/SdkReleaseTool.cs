using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tools.ReleaseReadiness;
using Microsoft.AspNetCore.Mvc;
using Microsoft.TeamFoundation.Common;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.SdkRelease
{
    [McpServerToolType, Description("This type contains the tools to release SDK package")]
    public class SdkReleaseTool(IDevOpsService devopsService, ILogger<SdkReleaseTool> logger, ILogger<ReleaseReadinessTool> releaseReadinessLogger, IOutputService output) : MCPTool
    {
        private readonly string commandName = "sdk-release";
        private readonly Option<string> packageNameOpt = new(["--package"], "Package name") { IsRequired = true };
        private readonly Option<string> languageOpt = new(["--language"], "Language of the package") { IsRequired = true };
        private readonly Option<string> branchOpt = new(["--branch"],() => "main",  "Branch to release the package from") { IsRequired = false };
        public static readonly string[] ValidLanguages = { ".NET", "Python", "Java", "javaScript", "Go" };

        public override Command GetCommand()
        {
            var command = new Command(commandName, "Run the release pipeline for the package") { packageNameOpt, languageOpt, branchOpt };
            command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
            return command;
        }

        public async override Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            var packageName = ctx.ParseResult.GetValueForOption(packageNameOpt);
            var language = ctx.ParseResult.GetValueForOption(languageOpt);
            var branch = ctx.ParseResult.GetValueForOption(branchOpt);
            var result = await ReleasePackageAsync(packageName, language, branch);
            output.Output(result);
        }

        [McpServerTool(Name = "ReleasePackage"), Description("Releases the specified SDK package for a language. This includes checking if the package is ready for release and triggering the release pipeline. This tool calls CheckPackageReleaseReadiness")]
        public async Task<SdkReleaseResponse> ReleasePackageAsync(string packageName, string language, string branch = "main")
        {
            try
            {
                SdkReleaseResponse response = new()
                {
                    PackageName = packageName,
                    Language = language
                };

                bool isValidParams = true;
                if (string.IsNullOrWhiteSpace(packageName))
                {
                    response.ReleaseStatusDetails = "Package name cannot be null or empty. ";
                    isValidParams = false;
                }
                if (string.IsNullOrWhiteSpace(language) || !ValidLanguages.Contains(language))
                {
                    response.ReleaseStatusDetails += "Language must be one of the following: " + string.Join(", ", ValidLanguages);
                    isValidParams = false;
                }

                // Get the package work item from DevOps
                var package = await devopsService.GetPackageWorkItemAsync(packageName, language);
                if (package == null)
                {
                    response.ReleaseStatusDetails = $"No package work item found for package '{packageName}' in language '{language}'. Please check the package name and language and also make sure that SDK is merged to main branch";
                    response.ReleasePipelineStatus = "Failed";
                    isValidParams = false;
                }

                if (string.IsNullOrEmpty(package?.PipelineDefinitionUrl))
                {
                    response.ReleaseStatusDetails += $"No release pipeline found for package '{packageName}' in language '{language}'. Please check the package name and language.";
                    response.ReleasePipelineStatus = "Failed";
                    isValidParams = false;
                }

                if (!isValidParams)
                {
                    response.ReleasePipelineStatus = "Failed";
                    logger.LogError(response.ReleaseStatusDetails);
                    return response;
                }

                // Check if the package is ready for release
                var releaseReadinessTool = new ReleaseReadinessTool(devopsService, output, releaseReadinessLogger);
                var releaseReadiness = await releaseReadinessTool.CheckPackageReleaseReadinessAsync(packageName, language);
                if (!releaseReadiness.IsPackageReady)
                {
                    response.ReleaseStatusDetails = $"Package is not ready for release. {releaseReadiness.PackageReadinessDetails}";
                    response.ReleasePipelineStatus = "Failed";
                    logger.LogError(response.ReleaseStatusDetails);
                    return response;
                }

                var buildDefinitionId = package?.PipelineDefinitionUrl?.Split('=')?.LastOrDefault();
                logger.LogInformation($"Package {packageName} is ready for release in {language}.");
                logger.LogInformation($"Release pipeline: {package?.PipelineDefinitionUrl}");
                logger.LogInformation($"Triggering release pipeline for package {packageName} in {language}...");
               
                // Trigger the release pipeline
                if (buildDefinitionId != null)
                {
                    var releasePipelineRun = await devopsService.RunPipelineAsync(int.Parse(buildDefinitionId!), new Dictionary<string, string>(), branch);
                    if (releasePipelineRun != null)
                    {
                        response.ReleasePipelineRunUrl = DevOpsService.GetPipelineUrl(releasePipelineRun.Id);
                        response.PipelineBuildId = releasePipelineRun.Id;
                        response.ReleasePipelineStatus = releasePipelineRun.Status?.ToString() ?? "";
                        response.ReleaseStatusDetails = $"Release pipeline triggered successfully for package '{packageName}' in language '{language}'. Check the status of the pipeline after some time and approve the SDK release using the link to the pipeline run.";
                        logger.LogInformation(response.ReleaseStatusDetails);
                    }
                    else
                    {
                        response.ReleaseStatusDetails = $"Failed to trigger release pipeline for package '{packageName}' in language '{language}'.";
                        response.ReleasePipelineStatus = "Failed";
                        logger.LogError(response.ReleaseStatusDetails);                        
                    }
                }
                else
                {
                    response.ReleaseStatusDetails = $"Failed to trigger release pipeline for package '{packageName}' in language '{language}'. Build definition ID is not available in pipeline URL {package?.PipelineDefinitionUrl}.";
                    response.ReleasePipelineStatus = "Failed";
                    logger.LogError(response.ReleaseStatusDetails);                    
                }

                return response;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while releasing the package.");
                SdkReleaseResponse response = new()
                {
                    PackageName = packageName,
                    Language = language,
                    ReleasePipelineStatus = "Failed",
                    ReleaseStatusDetails = $"Error: {ex.Message}"
                };
                return response;
            }
        }
    }
}
