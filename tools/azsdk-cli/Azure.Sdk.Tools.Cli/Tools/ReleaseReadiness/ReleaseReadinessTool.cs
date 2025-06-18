// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Text;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.TeamFoundation.Build.WebApi;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.ReleaseReadiness
{
    [Description("This class contains an MCP tool that checks the release readiness status of a package")]
    [McpServerToolType]
    public class ReleaseReadinessTool(IDevOpsService devopsService,
        IOutputService output,
        ILogger<ReleaseReadinessTool> logger) : MCPTool
    {
        private readonly Option<string> packageNameOpt = new(["--package-name"], "SDK package name") { IsRequired = true };
        private readonly Option<string> languageOpt = new(["--language"], "SDK language from one of the following ['.NET', 'Python', 'Java', 'JavaScript', Go]") { IsRequired = true };
        private static readonly string Pipeline_Success_Status = "Succeeded";

        public override Command GetCommand()
        {
            var command = new Command("releaseReadiness", "Checks release readiness of a SDK package.") { packageNameOpt, languageOpt };
            command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
            return command;
        }

        public async override Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            var cmd = ctx.ParseResult.CommandResult.Command.Name;
            var packageName = ctx.ParseResult.GetValueForOption(packageNameOpt);
            var language = ctx.ParseResult.GetValueForOption(languageOpt);
            logger.LogInformation($"Running release readiness check for {packageName} in {language}");
            var result = await CheckPackageReleaseReadinessAsync(packageName, language);
            output.Output(result);
        }

        [McpServerTool(Name = "CheckPackageReleaseReadiness"), Description("Checks the release readiness status of a specified SDK package for a language. This includes checking pipeline status, apiview status, change log status and namespace approval status.")]
        public async Task<PackageResponse> CheckPackageReleaseReadinessAsync(string packageName, string language)
        {
            try
            {
                var package = await devopsService.GetPackageWorkItemAsync(packageName, language);
                if (package == null)
                {
                    package = new PackageResponse
                    {
                        Name = packageName,
                        Language = language,
                        ResponseError = $"No package work item found for package '{packageName}' in language '{language}'. Please check the package name and language."
                    };
                    SetFailure();
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
                bool isDataPlanePackage = !package.PackageType.Equals("mgmt");
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
                var package = new PackageResponse
                {
                    Name = packageName,
                    Language = language,
                    IsPackageReady = false,
                    ResponseError = $"Failed to check package readiness for '{packageName}' in language '{language}'. Error {ex.Message}"
                };
                SetFailure();
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
                        logger.LogInformation($"Pipeline status: {pipelineRun.Status}, Result: {pipelineRun.Result}");
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
            catch(Exception ex)
            {
                logger.LogError("Failed to get pipeline run details. Error: {exception}", ex.Message);
                return $"Failed to get pipeline run details. Error: {ex.Message}";
            }
        }
    }
}
