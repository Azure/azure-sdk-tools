using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using Microsoft.TeamFoundation.Build.WebApi;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Cli.Tools.Package
{
    [McpServerToolType, Description("This type contains the tools to release SDK package")]
    public class SdkReleaseTool(IDevOpsService devopsService, ILogger<SdkReleaseTool> logger) : MCPTool
    {
        private const string ReleaseSdkToolName = "azsdk_release_sdk";
        private const string Pipeline_Success_Status = "Succeeded";
        
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

        private readonly Option<bool> dryRunOpt = new("--dry-run")
        {
            Description = "Verify package release readiness without triggering the release pipeline",
            Required = false,
        };
        public static readonly string[] ValidLanguages = [".NET", "Go", "Java", "JavaScript", "Python"];

        protected override Command GetCommand() =>
            new McpCommand(commandName, "Run the release pipeline for the package", ReleaseSdkToolName)
            {
                packageNameOpt, languageOpt, branchOpt, dryRunOpt,
            };

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            var packageName = parseResult.GetValue(packageNameOpt);
            var language = parseResult.GetValue(languageOpt);
            var branch = parseResult.GetValue(branchOpt);
            var dryRun = parseResult.GetValue(dryRunOpt);
            return await ReleasePackageAsync(packageName, language, branch, dryRun);
        }

        [McpServerTool(Name = ReleaseSdkToolName), Description("Releases the specified SDK package for a language. This includes checking if the package is ready for release and triggering the release pipeline. To ONLY check package release readiness pass dryRun as true.")]
        public async Task<SdkReleaseResponse> ReleasePackageAsync(string packageName, string language, string branch = "main", bool dryRun = false)
        {
            try
            {
                SdkReleaseResponse response = new()
                {
                    PackageName = packageName
                };
                response.SetLanguage(language);

                bool isValidParams = true;
                if (string.IsNullOrWhiteSpace(packageName))
                {
                    response.ReleaseStatusDetails = "Package name cannot be null or empty. ";
                    isValidParams = false;
                }
                if (string.IsNullOrWhiteSpace(language) || !ValidLanguages.Contains(language, StringComparer.OrdinalIgnoreCase))
                {
                    response.ReleaseStatusDetails += "Language must be one of the following: " + string.Join(", ", ValidLanguages);
                    isValidParams = false;
                }

                // Get the package work item from DevOps
                var package = await devopsService.GetPackageWorkItemAsync(packageName, language);
                if (package == null)
                {
                    response.ReleaseStatusDetails = $"No package work item found for package '{packageName}' in language '{language}'. Please check the package name and language and also make sure that SDK is merged to main branch in the specific language repo.";
                    response.ReleasePipelineStatus = "Failed";
                    isValidParams = false;
                }
                response.PackageType = package?.PackageType ?? SdkType.Unknown;
                if (string.IsNullOrEmpty(package?.PipelineDefinitionUrl))
                {
                    response.ReleaseStatusDetails += $"No release pipeline found for package '{packageName}' in language '{language}'. Please check the package name and language.";
                    response.ReleasePipelineStatus = "Failed";
                    isValidParams = false;
                }

                if (!isValidParams)
                {
                    response.ReleasePipelineStatus = "Failed";
                    logger.LogError("{details}", response.ReleaseStatusDetails);
                    return response;
                }

                // Check if the package is ready for release
                var releaseReadiness = await CheckPackageReleaseReadinessAsync(packageName, language);
                if (!releaseReadiness.IsPackageReady)
                {
                    response.ReleaseStatusDetails = $"Package is not ready for release. {releaseReadiness.PackageReadinessDetails}";
                    response.ReleasePipelineStatus = "Failed";
                    logger.LogError("{details}", response.ReleaseStatusDetails);
                    return response;
                }

                // If dry-run mode, return readiness check results without triggering release
                if (dryRun)
                {
                    response.ReleaseStatusDetails = releaseReadiness.PackageReadinessDetails;
                    logger.LogInformation("[DRY RUN] Package readiness check completed for {packageName} in {language}.", packageName, language);
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

        private async Task<PackageWorkitemResponse> CheckPackageReleaseReadinessAsync(string packageName, string language)
        {
            try
            {
                var package = await devopsService.GetPackageWorkItemAsync(packageName, language);
                if (package == null)
                {
                    package = new PackageWorkitemResponse
                    {
                        PackageName = packageName,   
                        ResponseError = $"No package work item found for package '{packageName}' in language '{language}'. Please check the package name and language."
                    };
                    package.SetLanguage(language);
                    return package;
                }

                package.IsPackageReady = package.IsChangeLogReady;

                //Check release date for latest version in planned release
                var plannedRelease = package.PlannedReleases.FirstOrDefault(r => r.Version.Equals(package.Version)) ?? package.PlannedReleases.LastOrDefault();
                package.PlannedReleaseDate = plannedRelease?.ReleaseDate ?? "Unknown";
                if (package.PlannedReleaseDate.Equals("Unknown"))
                {
                    package.IsPackageReady = false;
                    package.PackageReadinessDetails = $"No planned release date found in package details for current package version {package.Version}. Please check the package version and verify that change log file is correct. ";
                }

                var releaseType = plannedRelease?.ReleaseType ?? "Unknown";
                bool isPreviewRelease = releaseType.Equals("Beta");
                bool isDataPlanePackage = package.PackageType != SdkType.Management;
                // Check for namespace approval if preview release for data plane
                if (isDataPlanePackage && isPreviewRelease)
                {
                    if (!package.IsPackageNameApproved)
                    {
                        package.IsPackageReady = false;
                        package.PackageReadinessDetails += $"Package name '{packageName}' is not approved for preview release. ";
                    }
                    // no need to add extra package name approval status if package name is approved or has at least one version already released
                }
                else
                {
                    package.PackageNameStatus = "Not required";
                    package.PackageNameApprovalDetails = "Package name approval is not required for GA releases of data plane packages or for non-data plane packages.";
                }

                // Check if API view is approved if stable version for data plane or .NET
                if ((isDataPlanePackage || language.Equals(".NET")) && !isPreviewRelease)
                {

                    if (!package.IsApiViewApproved)
                    {
                        package.IsPackageReady = false;
                        package.PackageReadinessDetails += $"API view is not approved for GA release of package '{packageName}'. ";
                    }
                }
                else
                {
                    package.APIViewStatus = "Not required";
                    package.ApiViewValidationDetails = "API view is not required for preview releases of data plane packages or for non-data plane packages.";
                }

                // Check last pipeline run status for the package and verify it completed successfully
                package.LatestPipelineStatus = await GetPipelineRunDetails(package.LatestPipelineRun);
                bool hasPipelineWarning = string.IsNullOrEmpty(package.LatestPipelineStatus) || !package.LatestPipelineStatus.Contains(Pipeline_Success_Status);

                // Package release readiness status
                if (package.IsPackageReady)
                {
                    package.PackageReadinessDetails = $"Package '{packageName}' is ready for release. Queue a release pipeline run using the link {package.PipelineDefinitionUrl} to release the package.";
                    
                    // Add warning about pipeline status if not successful
                    if (hasPipelineWarning)
                    {
                        package.PackageReadinessDetails += $"\n\nWARNING: The last known CI pipeline status for this package has failed. This might cause issues when running the release pipeline if the error was not a transient failure. Please review the last pipeline run at {package.LatestPipelineRun} to verify the failure was transient before proceeding with the release.";
                    }
                }
                else
                {
                    package.PackageReadinessDetails += $"Package '{packageName}' is not ready for release. Please address the issues mentioned above.";
                }
                return package;
            }
            catch (Exception ex)
            {
                var package = new PackageWorkitemResponse
                {
                    PackageName = packageName,
                    IsPackageReady = false,
                    ResponseError = $"Failed to check package readiness for '{packageName}' in language '{language}'. Error {ex.Message}"
                };
                package.SetLanguage(language);
                return package;
            }
        }

        private async Task<string> GetPipelineRunDetails(string pipelineRunUrl)
        {
            try
            {
                logger.LogInformation("Getting pipeline run details for URL: {pipelineRunUrl}", pipelineRunUrl);
                if (!string.IsNullOrEmpty(pipelineRunUrl) && pipelineRunUrl.Contains("buildId="))
                {
                    var buildId = int.Parse(pipelineRunUrl.Split("buildId=").LastOrDefault());
                    logger.LogInformation("Extracted build ID: {buildId}", buildId);
                    var pipelineRun = await devopsService.GetPipelineRunAsync(buildId);
                    if (pipelineRun != null)
                    {
                        logger.LogInformation(
                            "Pipeline status: {PipelineStatus}, Result: {PipelineResult}",
                            pipelineRun.Status,
                            pipelineRun.Result);
                        var status = (pipelineRun.Status == BuildStatus.Completed ? pipelineRun.Result?.ToString() : pipelineRun.Status.ToString()) ?? "Unknown";
                        if (!status.Contains(Pipeline_Success_Status))
                        {
                            status = $"Pipeline run with ID {buildId} did not succeed. Status: {status}. Please check the pipeline run details at {DevOpsService.GetPipelineUrl(buildId)} for more information.";
                        }
                        return status;
                    }
                }
                return $"Failed to get pipeline run details. The pipeline run URL '{pipelineRunUrl}' is invalid or does not contain a valid build ID.";
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get pipeline run details for URL {PipelineRunUrl}", pipelineRunUrl);
                return $"Failed to get pipeline run details. Error: {ex.Message}";
            }
        }
    }
}
