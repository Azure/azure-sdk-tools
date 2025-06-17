// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Text;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.TeamFoundation.Build.WebApi;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.ReleaseReadiness
{
    [Description("This class contains MCP tool to check release readiness status of a package")]
    [McpServerToolType]
    public class ReleaseReadinessTool(IDevOpsService devopsService,
        IOutputService output,
        ILogger<ReleaseReadinessTool> logger) : MCPTool
    {
        private readonly Option<string> packageNameOpt = new(["--package-name"], "SDK package name") { IsRequired = true };
        private readonly Option<string> languageOpt = new(["--language"], "SDK language from one of the following ['.NET', 'Python', 'Java', 'JavaScript', Go]") { IsRequired = true };

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

        [McpServerTool(Name = "CheckPackageReleaseReadiness"), Description("Checks the release readiness status of a specified SDK package for a language. This includes checking pipeline status, apiview status, change log status, namespace approval status")]
        public async Task<string> CheckPackageReleaseReadinessAsync(string packageName, string language, string packageVersion = "")
        {
            try
            {
                var package = await devopsService.GetPackageWorkItemAsync(packageName, language, packageVersion);
                if (package == null)
                {
                    logger.LogWarning("No package work item found for the specified package and language.");
                    return output.Format($"No work item found for package '{packageName}' in language '{language}'.");
                }

                StringBuilder outputBuilder = new StringBuilder();
                outputBuilder.AppendLine($"Name: {packageName}");
                outputBuilder.AppendLine($"Version: {package.Version}");
                outputBuilder.AppendLine($"Display name: {package.DisplayName}");
                outputBuilder.AppendLine($"Type: {package.PackageType}");
                outputBuilder.AppendLine($"Work item ID: {package.WorkItemId}");
                outputBuilder.AppendLine($"Language: {package.Language}");
                outputBuilder.AppendLine($"State: {package.State}");
                outputBuilder.AppendLine($"Repo path: {package.PackageRepoPath}");
                outputBuilder.AppendLine($"Latest pipeline run: {package.LatestPipelineRun}");
                outputBuilder.AppendLine($"Release pipeline URL: {package.PipelineDefinitionUrl}");

                bool isPackageReady = true;

                //Check release date for latest version in planned release
                var plannedRelDate = package.PlannedReleases.LastOrDefault()?.ReleaseDate;
                if (plannedRelDate != null && !plannedRelDate.Equals("Unknown"))
                {
                    outputBuilder.AppendLine($"Planned release date: {plannedRelDate}");
                }
                else
                {
                    isPackageReady = false;
                    outputBuilder.AppendLine($"No planned release date found in package details for current package version {package.Version}. Please check the package version and verify that change log file is correct.");
                }

                bool isPreviewRelease = package.PlannedReleases.LastOrDefault()?.ReleaseType.Equals("Beta") ?? false;
                bool isDataPlanePackage = !package.PackageType.Equals("mgmt");
                // Check for namespace approval if preview release for data plane
                if (isDataPlanePackage && isPreviewRelease)
                {
                    if (!package.IsPackageNameApproved)
                    {
                        isPackageReady = false;
                        outputBuilder.AppendLine($"Package name '{packageName}' is not approved for preview release.");
                        outputBuilder.AppendLine($"Package Name Approval Status: {package.PackageNameStatus}");
                        outputBuilder.AppendLine($"Package Name Approval Details: {package.PackageNameApprovalDetails}");
                    }
                    else if (!package.ReleasedVersions.Any())
                    {
                        outputBuilder.AppendLine($"Package name '{packageName}' is approved for preview release");
                    }
                    // no need to show package name approval status if package name is approved and has at least one version already released
                }

                // Check if API view is approved if stable version for data plane or .NET
                if ((isDataPlanePackage || language.Equals(".NET")) && !isPreviewRelease)
                {
                    
                    if (!package.IsApiViewApproved)
                    {
                        isPackageReady = false;
                        outputBuilder.AppendLine($"API View Status: {package.APIViewStatus}. API view is not approved for GA release of package '{packageName}'.");
                        outputBuilder.AppendLine($"API View Validation Details: {package.ApiViewValidationDetails}");
                    }
                    else
                    {
                        outputBuilder.AppendLine($"API view is approved for package '{packageName}'.");
                    }
                }
                
                //Check if change log verification is valid
                if (!package.IsChangeLogReady)
                {
                    isPackageReady = false;
                    outputBuilder.AppendLine($"Change Log Status: {package.changeLogStatus}");
                    outputBuilder.AppendLine($"Change Log Validation Details: {package.ChangeLogValidationDetails}. Change log must contain an entry for version {package.Version} with planned release date.");
                }
                else
                {
                    outputBuilder.AppendLine($"Change log verification is valid for package '{packageName}'.");
                }

                // check last pipeline run status for the package and verify it completed successfully
                var pipelineRunStatus = await GetPipelineRunDetails(package.LatestPipelineRun);
                if (string.IsNullOrEmpty(pipelineRunStatus))
                {
                    isPackageReady = false;
                    outputBuilder.AppendLine($"Latest pipeline run is not available for package '{packageName}'.");
                }
                else
                {
                    if (!pipelineRunStatus.Contains("succeeded"))
                    {
                        isPackageReady = false;
                    }
                    outputBuilder.AppendLine($"Last pipeline run details: {pipelineRunStatus}");
                }

                // Package release readiness status
                if (isPackageReady)
                {
                    outputBuilder.AppendLine($"Package '{packageName}' is ready for release.");                    
                }
                else
                {
                    outputBuilder.AppendLine($"Package '{packageName}' is not ready for release. Please address the issues mentioned above.");
                }
                return output.Format(outputBuilder.ToString());
            }
            catch (Exception ex)
            {
                logger.LogError("{exception}", ex.Message);
                return output.Format($"Failed to get package details. Error {ex.Message}");
            }
        }

        private async Task<string> GetPipelineRunDetails(string pipelineRunUrl)
        {
            try
            {
                if (!string.IsNullOrEmpty(pipelineRunUrl) && pipelineRunUrl.Contains("buildId="))
                {
                    var buildId = int.Parse(pipelineRunUrl.Split("buildId=").LastOrDefault());
                    var pipelineRun = await devopsService.GetPipelineRunAsync(buildId);
                    if (pipelineRun != null)
                    {
                        if (pipelineRun.Result != BuildResult.Succeeded && pipelineRun.Result != BuildResult.PartiallySucceeded)
                        {
                            return $"Latest pipeline run did not succeed. Status: {pipelineRun.Result?.ToString()}. For more details: {DevOpsService.GetPipelineUrl(buildId)}";
                        }
                        else
                        {
                            return $"Latest pipeline run succeeded. Status: {pipelineRun.Result?.ToString()}. For more details: {DevOpsService.GetPipelineUrl(buildId)}";
                        }
                    }
                }
                logger.LogWarning("Pipeline run URL is not valid or does not contain buildId.");
                return "Pipeline run URL is not valid or does not contain buildId to find latest pipeline run status.";
            }
            catch(Exception ex)
            {
                logger.LogError("Failed to get pipeline run details. Error: {exception}", ex.Message);
                return $"Failed to get pipeline run details. Error: {ex.Message}";
            }
        }
    }
}
