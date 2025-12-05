using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Cli.Tools.Package
{
    [McpServerToolType, Description("This type contains the tools to release SDK package")]
    public class SdkReleaseTool(
            IDevOpsService devopsService,
            ILogger<SdkReleaseTool> logger,
            ILogger<ReleaseReadinessTool> releaseReadinessLogger,
            IInputSanitizer inputSanitizer) : MCPTool
    {
        private const string ReleaseSdkToolName = "azsdk_release_sdk";
        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package];

        private readonly string commandName = "release";
        private readonly Option<string> packageNameOpt = new("--package-name")
        {
            Description = "Package name",
            Required = true,
        };

        private readonly Option<string> languageOpt = new("--language")
        {
            Description = "Language of the package",
            Required = true,
        };

        private readonly Option<string> branchOpt = new("--branch")
        {
            Description = "Branch to release the package from",
            Required = false,
            DefaultValueFactory = _ => "main",
        };
        public static readonly string[] ValidLanguages = [".NET", "Go", "Java", "JavaScript", "Python"];

        protected override Command GetCommand() =>
            new McpCommand(commandName, "Run the release pipeline for the package", ReleaseSdkToolName)
            {
                packageNameOpt, languageOpt, branchOpt,
            };

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            var packageName = parseResult.GetValue(packageNameOpt);
            var language = parseResult.GetValue(languageOpt);
            var branch = parseResult.GetValue(branchOpt);
            return await ReleasePackageAsync(packageName, language, branch);
        }

        [McpServerTool(Name = ReleaseSdkToolName), Description("Releases the specified SDK package for a language. This includes checking if the package is ready for release and triggering the release pipeline. This tool calls CheckPackageReleaseReadiness")]
        public async Task<SdkReleaseResponse> ReleasePackageAsync(string packageName, string language, string branch = "main")
        {
            try
            {
                SdkReleaseResponse response = new()
                {
                    PackageName = packageName
                };
                language = inputSanitizer.SanitizeLanguage(language);
                response.SetLanguage(language);

                if (string.IsNullOrWhiteSpace(packageName))
                {
                    response.ReleaseStatusDetails = "Package name cannot be null or empty.";
                    response.ReleasePipelineStatus = "Failed";
                    logger.LogError("{details}", response.ReleaseStatusDetails);
                    return response;
                }

                if (string.IsNullOrWhiteSpace(language) || !ValidLanguages.Contains(language, StringComparer.OrdinalIgnoreCase))
                {
                    response.ReleaseStatusDetails = "Language must be one of the following: " + string.Join(", ", ValidLanguages);
                    response.ReleasePipelineStatus = "Failed";
                    logger.LogError("{details}", response.ReleaseStatusDetails);
                    return response;
                }

                // Get the package work item from DevOps
                var package = await devopsService.GetPackageWorkItemAsync(packageName, language);
                if (package == null)
                {
                    logger.LogInformation("Exact package not found; falling back to partial matches");
                    var packages = await devopsService.ListPartialPackageWorkItemAsync(packageName, language);
                    if (packages == null || packages.Count == 0)
                    {
                        response.ReleaseStatusDetails = $"No package work item found for package '{packageName}' in language '{language}'. Please check the package name and language and also make sure that SDK is merged to main branch in the specific language repo.";
                        response.ReleasePipelineStatus = "Failed";
                        logger.LogError("{details}", response.ReleaseStatusDetails);
                        return response;
                    }
                    var packageNames = packages.Select(p => p.PackageName).Distinct().ToList();

                    response.ReleaseStatusDetails = $"The package {packageName} could not be found. Did you mean one of the following available packages? [{string.Join(", ", packageNames)}]";
                    response.NextSteps = ["Select the package and run SDK release tool"];
                    response.ReleasePipelineStatus = "Failed";
                    logger.LogError("{details}", response.ReleaseStatusDetails);
                    return response;
                }
                
                response.PackageType = package?.PackageType ?? SdkType.Unknown;
                if (string.IsNullOrEmpty(package?.PipelineDefinitionUrl))
                {
                    response.ReleaseStatusDetails += $"No release pipeline found for package '{packageName}' in language '{language}'. Please check the package name and language.";
                    response.ReleasePipelineStatus = "Failed";
                    logger.LogError("{details}", response.ReleaseStatusDetails);
                    return response;
                }

                // Check if the package is ready for release
                var releaseReadinessTool = new ReleaseReadinessTool(devopsService, releaseReadinessLogger);
                var releaseReadiness = await releaseReadinessTool.CheckPackageReleaseReadinessAsync(packageName, language);
                if (!releaseReadiness.IsPackageReady)
                {
                    response.ReleaseStatusDetails = $"Package is not ready for release. {releaseReadiness.PackageReadinessDetails}";
                    response.ReleasePipelineStatus = "Failed";
                    logger.LogError("{details}", response.ReleaseStatusDetails);
                    return response;
                }

                var buildDefinitionId = package?.PipelineDefinitionUrl?.Split('=')?.LastOrDefault();
                logger.LogInformation("Package {packageName} is ready for release in {language}.", packageName, language);
                logger.LogInformation("Release pipeline: {pipelineUrl}", package?.PipelineDefinitionUrl);
                logger.LogInformation("Triggering release pipeline for package {packageName} in {language}...", packageName, language);

                // Trigger the release pipeline
                if (buildDefinitionId != null)
                {
                    var releasePipelineRun = await devopsService.RunPipelineAsync(int.Parse(buildDefinitionId!), new Dictionary<string, string>(), branch);
                    if (releasePipelineRun != null)
                    {
                        response.ReleasePipelineRunUrl = DevOpsService.GetPipelineUrl(releasePipelineRun.Id);
                        response.PipelineBuildId = releasePipelineRun.Id;
                        response.ReleasePipelineStatus = releasePipelineRun.Status?.ToString() ?? "";
                        response.ReleaseStatusDetails = $"Release pipeline triggered successfully for package '{packageName}' in language '{language}'. Check the status of the pipeline after some time and approve the SDK release using the link to the pipeline run. You can find more information about release approval in https://aka.ms/azsdk/publishsdk";
                        logger.LogInformation("{details}", response.ReleaseStatusDetails);
                    }
                    else
                    {
                        response.ReleaseStatusDetails = $"Failed to trigger release pipeline for package '{packageName}' in language '{language}'. Please check your access permissions. You can find more information in https://aka.ms/azsdk/access";
                        response.ReleasePipelineStatus = "Failed";
                        logger.LogError("{details}", response.ReleaseStatusDetails);
                    }
                }
                else
                {
                    response.ReleaseStatusDetails = $"Failed to trigger release pipeline for package '{packageName}' in language '{language}'. Build definition ID is not available in pipeline URL {package?.PipelineDefinitionUrl}. Please check and make sure that SDK is present in the main branch of SDK repo.";
                    response.ReleasePipelineStatus = "Failed";
                    logger.LogError("{details}", response.ReleaseStatusDetails);
                }

                return response;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while releasing the package.");
                SdkReleaseResponse response = new()
                {
                    PackageName = packageName,
                    ReleasePipelineStatus = "Failed",
                    ResponseError = $"Error: {ex.Message}"
                };
                response.SetLanguage(language);
                return response;
            }
        }
    }
}
