// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using Microsoft.TeamFoundation.Build.WebApi;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Cli.Tools.Package
{
    [Description("This class contains an MCP tool that checks the release readiness status of a package")]
    [McpServerToolType]
    public class ReleaseReadinessTool(
        IDevOpsService devopsService,
        ILogger<ReleaseReadinessTool> logger
    ) : MCPTool
    {
        private const string CheckPackageReleaseReadinessToolName = "azsdk_check_package_release_readiness";
        
        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package];
        private readonly Option<string> packageNameOpt = new("--package-name")
        {
            Description = "SDK package name",
            Required = true,
        };

        private readonly Option<string> languageOpt = new("--language")
        {
            Description = "SDK language from one of the following ['.NET', 'Python', 'Java', 'JavaScript', Go]",
            Required = true,
        };
        private static readonly string Pipeline_Success_Status = "Succeeded";

        protected override Command GetCommand() =>
            new McpCommand("release-readiness", "Checks release readiness of a SDK package", CheckPackageReleaseReadinessToolName) { packageNameOpt, languageOpt };

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            var packageName = parseResult.GetValue(packageNameOpt);
            var language = parseResult.GetValue(languageOpt);
            logger.LogInformation("Running release readiness check for {packageName} in {language}", packageName, language);
            return await CheckPackageReleaseReadinessAsync(packageName, language);
        }

        [McpServerTool(Name = CheckPackageReleaseReadinessToolName), Description("Checks if SDK package is ready to release (release readiness). This includes checking pipeline status, apiview status, change log status, and namespace approval status.")]
        public async Task<PackageWorkitemResponse> CheckPackageReleaseReadinessAsync(string packageName, string language)
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
                if (string.IsNullOrEmpty(package.LatestPipelineStatus) || !package.LatestPipelineStatus.Contains(Pipeline_Success_Status))
                {
                    package.IsPackageReady = false;
                }

                // Package release readiness status
                if (package.IsPackageReady)
                {
                    package.PackageReadinessDetails = $"Package '{packageName}' is ready for release. Queue a release pipeline run using the link {package.PipelineDefinitionUrl} to release the package.";
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
