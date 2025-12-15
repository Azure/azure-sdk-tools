// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.ComponentModel;
using System.Text;
using Microsoft.TeamFoundation.Build.WebApi;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;
using Azure.Sdk.Tools.Cli.Tools.Core;

namespace Azure.Sdk.Tools.Cli.Tools.ReleasePlan
{
    [Description("This type contains the MCP tool to run SDK generation using pipeline, check SDK generation pipeline status and to get generated SDK pull request details.")]
    [McpServerToolType]
    public class SpecWorkflowTool(IGitHubService githubService,
        IDevOpsService devopsService,
        ITypeSpecHelper typespecHelper,
        ILogger<SpecWorkflowTool> logger,
        IInputSanitizer inputSanitizer
    ) : MCPMultiCommandTool
    {
        public override CommandGroup[] CommandHierarchy { get; set; } = [new("spec-workflow", "TypeSpec SDK generation commands")];

        // Commands
        private const string generateSdkCommandName = "generate-sdk";
        private const string getSdkPullRequestCommandName = "get-sdk-pr";

        // MCP Tool Names
        private const string RunGenerateSdkToolName = "azsdk_run_generate_sdk";
        private const string GetSdkPullRequestLinkToolName = "azsdk_get_sdk_pull_request_link";

        // Options
        private readonly Option<string> typeSpecProjectPathOpt = new("--typespec-project")
        {
            Description = "Path to typespec project",
            Required = true,
        };

        private readonly Option<int> pullRequestNumberOpt = new("--pr")
        {
            Description = "Pull request number",
            Required = false,
        };

        private readonly Option<string> apiVersionOpt = new("--api-version")
        {
            Description = "API version",
            Required = true,
        };

        private readonly Option<string> sdkReleaseTypeOpt = new("--release-type")
        {
            Description = "SDK release type: beta or stable",
            Required = true,
        };

        private readonly Option<string> languageOpt = new("--language")
        {
            Description = "SDK language, Options[Python, .NET, JavaScript, Java, go]",
            Required = true,
        };

        private readonly Option<int> workItemIdOpt = new("--workitem-id")
        {
            Description = "SDK release plan work item id",
            Required = false,
        };

        private readonly Option<int> pipelineRunIdOpt = new("--pipeline-run")
        {
            Description = "SDK generation pipeline run id",
            Required = true,
        };

        private static readonly string PUBLIC_SPECS_REPO = "azure-rest-api-specs";
        private static readonly string REPO_OWNER = "Azure";
        public static readonly string ARM_SIGN_OFF_LABEL = "ARMSignedOff";

        public static readonly HashSet<string> SUPPORTED_LANGUAGES = new()
        {
            "python",
            ".net",
            "javascript",
            "java",
            "go"
        };

        protected override List<Command> GetCommands() =>
        [
            new McpCommand(generateSdkCommandName, "Generate SDK for a TypeSpec project", RunGenerateSdkToolName)
            {
                typeSpecProjectPathOpt, apiVersionOpt, sdkReleaseTypeOpt, languageOpt, pullRequestNumberOpt, workItemIdOpt,
            },
            new McpCommand(getSdkPullRequestCommandName, "Get SDK pull request link from SDK generation pipeline", GetSdkPullRequestLinkToolName)
            {
                languageOpt, pipelineRunIdOpt, workItemIdOpt,
            },
        ];

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            var command = parseResult.CommandResult.Command.Name;
            var commandParser = parseResult;
            return command switch
            {
                generateSdkCommandName => await RunGenerateSdkAsync(commandParser.GetValue(typeSpecProjectPathOpt),
                                        commandParser.GetValue(apiVersionOpt),
                                        commandParser.GetValue(sdkReleaseTypeOpt),
                                        commandParser.GetValue(languageOpt),
                                        commandParser.GetValue(pullRequestNumberOpt),
                                        commandParser.GetValue(workItemIdOpt)),
                getSdkPullRequestCommandName => await GetSDKPullRequestDetails(commandParser.GetValue(languageOpt), workItemId: commandParser.GetValue(workItemIdOpt), buildId: commandParser.GetValue(pipelineRunIdOpt)),
                _ => new DefaultCommandResponse { ResponseError = $"Unknown command: '{command}'" },
            };
        }

        private async Task<ReleaseWorkflowResponse> IsSdkDetailsPresentInReleasePlanAsync(int workItemId, string language)
        {
            var response = new ReleaseWorkflowResponse()
            {
                Status = "Failed",
                ResponseErrors = [],
            };

            try
            {
                if (workItemId == 0)
                {
                    response.ResponseErrors.Add("Work item ID is required to check if release plan is ready for SDK generation.");
                    return response;
                }

                var releasePlan = await devopsService.GetReleasePlanForWorkItemAsync(workItemId);

                var sdkInfoList = releasePlan?.SDKInfo;

                if (sdkInfoList == null || sdkInfoList.Count == 0)
                {
                    response.ResponseErrors.Add($"SDK details are not present in the release plan. Update the SDK details using the information in tspconfig.yaml");
                    return response;
                }

                var sdkInfo = sdkInfoList.FirstOrDefault(s => string.Equals(s.Language, language, StringComparison.OrdinalIgnoreCase));

                if (sdkInfo == null || string.IsNullOrWhiteSpace(sdkInfo.Language))
                {
                    response.ResponseErrors.Add($"Release plan work item with ID {workItemId} does not have a language specified. Update the SDK details using the information in tspconfig.yaml.");
                    return response;
                }

                if (string.IsNullOrWhiteSpace(sdkInfo.PackageName))
                {
                    response.ResponseErrors.Add($"Release plan work item with ID {workItemId} does not have a package name specified for {sdkInfo.Language}. Update the SDK details using the information in tspconfig.yaml.");
                    return response;
                }
                response.SetLanguage(sdkInfo.Language);
                if (releasePlan?.IsManagementPlane == true)
                {
                    response.PackageType = SdkType.Management;
                }
                else if (releasePlan?.IsDataPlane == true)
                {
                    response.PackageType = SdkType.Dataplane;
                }
                response.Details.Add($"SDK info for language '{sdkInfo.Language}' and package '{sdkInfo.PackageName}' is set correctly in the release plan.");
                response.Status = "Success";
                return response;
            }
            catch (Exception ex)
            {
                response.Status = "Failed";
                response.ResponseErrors.Add($"Failed to check if Release Plan is ready for SDK generation. Error: {ex.Message}");
                return response;
            }
        }

        [McpServerTool(Name = RunGenerateSdkToolName), Description("Generate SDK from a TypeSpec project using pipeline.")]
        public async Task<ReleaseWorkflowResponse> RunGenerateSdkAsync(string typespecProjectRoot, string apiVersion, string sdkReleaseType, string language, int pullRequestNumber = 0, int workItemId = 0)
        {
            try
            {
                var response = new ReleaseWorkflowResponse()
                {
                    Status = "Success",
                    ResponseErrors = []
                };
                language = inputSanitizer.SanitizeLanguage(language);
                logger.LogInformation(
                    "Generating SDK for TypeSpec project: {TypespecProjectRoot}, API Version: {ApiVersion}, SDK Release Type: {SdkReleaseType}, Language: {Language}, Pull Request Number: {PullRequestNumber}, Work Item ID: {WorkItemId}",
                    typespecProjectRoot,
                    apiVersion,
                    sdkReleaseType,
                    language,
                    pullRequestNumber,
                    workItemId);
                // Is language supported for SDK generation
                if (!DevOpsService.IsSDKGenerationSupported(language))
                {
                    response.ResponseErrors.Add($"SDK generation is currently not supported by agent for {language}");
                    response.Status = "Failed";
                }
                response.SetLanguage(language);
                // Is valid typespec project path
                if (!TypeSpecProject.IsValidTypeSpecProjectPath(typespecProjectRoot))
                {
                    response.ResponseErrors.Add($"Invalid TypeSpec project root path [{typespecProjectRoot}].");
                    response.Status = "Failed";
                }

                if (string.IsNullOrEmpty(apiVersion))
                {
                    response.ResponseErrors.Add("API version is required to generate SDK.");
                    response.Status = "Failed";
                }

                List<string> validReleaseTypes = ["beta", "stable"];
                sdkReleaseType = sdkReleaseType?.ToLower() ?? "";
                if (string.IsNullOrEmpty(sdkReleaseType) || !validReleaseTypes.Contains(sdkReleaseType))
                {
                    response.ResponseErrors.Add("SDK release type must be set as either beta or stable to generate SDK.");
                    response.Status = "Failed";
                }

                // Update SDK details in release plan if work item ID is provided
                if (workItemId > 0)
                {
                    var readiness = await IsSdkDetailsPresentInReleasePlanAsync(workItemId, language);
                    if (!readiness.Status.Equals("Success"))
                    {
                        response.ResponseErrors.AddRange(readiness.ResponseErrors);
                        response.Details.AddRange(readiness.Details);
                        response.Status = "Failed";
                    }
                    response.PackageType = readiness.PackageType;
                }

                // Return failure details in case of any failure
                if (response.Status.Equals("Failed"))
                {
                    var failureDetails = string.Join(",", response.ResponseErrors);
                    logger.LogInformation("SDK generation failed with details: [{FailureDetails}]", failureDetails);
                    return response;
                }

                string typeSpecProjectPath = typespecHelper.GetTypeSpecProjectRelativePath(typespecProjectRoot);
                string apiSpecBranchRef = "main";
                if (pullRequestNumber > 0)
                {
                    var pullRequest = await githubService.GetPullRequestAsync(REPO_OWNER, PUBLIC_SPECS_REPO, pullRequestNumber);
                    apiSpecBranchRef = (pullRequest?.Merged ?? false) ? pullRequest.Base.Ref : $"refs/pull/{pullRequestNumber}/merge";
                }

                string sdkRepoBranch = "";
                var releasePlan = workItemId != 0 ? await devopsService.GetReleasePlanForWorkItemAsync(workItemId) : null;
                var sdkPullRequestUrl = releasePlan?.SDKInfo.FirstOrDefault(s => s.Language == language)?.SdkPullRequestUrl;
                if (!string.IsNullOrEmpty(sdkPullRequestUrl))
                {
                    var parsedUrl = DevOpsService.ParseSDKPullRequestUrl(sdkPullRequestUrl);
                    var sdkPullRequest = await githubService.GetPullRequestAsync(parsedUrl.RepoOwner, parsedUrl.RepoName, parsedUrl.PrNumber);
                    if (sdkPullRequest is not null && sdkPullRequest.State != "closed" && sdkPullRequest.Merged == false)
                    {
                        sdkRepoBranch = sdkPullRequest.Head.Ref;
                    }
                }
                response.TypeSpecProject = typeSpecProjectPath;
                logger.LogInformation("Running SDK generation pipeline");
                var pipelineRun = await devopsService.RunSDKGenerationPipelineAsync(apiSpecBranchRef, typeSpecProjectPath, apiVersion, sdkReleaseType, language, workItemId, sdkRepoBranch);
                response.Status = "Success";
                response.Details.Add($"Azure DevOps pipeline {DevOpsService.GetPipelineUrl(pipelineRun.Id)} has been initiated to generate the SDK. Build ID is {pipelineRun.Id}. Once the pipeline job completes, an SDK pull request for {language} will be created.");
                return response;
            }
            catch (Exception ex)
            {
                var errorResponse = new ReleaseWorkflowResponse();
                errorResponse.ResponseError = $"Failed to run pipeline to generate SDK, Details: {ex.Message}";
                errorResponse.Status = "Failed";
                errorResponse.ExitCode = 1;
                return errorResponse;
            }
        }


        /// <summary>
        /// Get SDK pull request link from SDK generation pipeline.
        /// </summary>
        /// <param name="language">SDK Language</param>
        /// <param name="buildId">Build ID for the pipeline run</param>
        /// <param name="workItemId">Work item ID for the release plan</param>
        /// <returns></returns>
        [McpServerTool(Name = GetSdkPullRequestLinkToolName), Description("Get SDK pull request link from SDK generation pipeline run or from work item. Build ID of pipeline run is required to query pull request link from SDK generation pipeline. This tool can get SDK pull request details if present in a work item.")]
        public async Task<ReleaseWorkflowResponse> GetSDKPullRequestDetails(string language, int workItemId, int buildId = 0)
        {
            try
            {
                var response = new ReleaseWorkflowResponse();
                language = inputSanitizer.SanitizeLanguage(language);
                if (!IsValidLanguage(language))
                {
                    response.ResponseError = $"Unsupported language to get pull request details. Supported languages: {string.Join(", ", SUPPORTED_LANGUAGES)}";
                    return response;
                }

                if (buildId == 0 && workItemId == 0)
                {
                    response.ResponseError = "Either build ID or release plan work item ID is required to get SDK pull request details.";
                    return response;
                }

                response.SetLanguage(language);
                // Get SDK details from work item
                if (buildId == 0)
                {
                    response.Details.Add("Build Id is not available. Checking for SDK pull request details in release plan work item.");
                    var releasePlan = await devopsService.GetReleasePlanForWorkItemAsync(workItemId);
                    var sdkInfo = releasePlan?.SDKInfo.FirstOrDefault(s => string.Equals(s.Language, language, StringComparison.OrdinalIgnoreCase));
                    if (sdkInfo != null && !string.IsNullOrEmpty(sdkInfo.SdkPullRequestUrl))
                    {
                        response.Details.Add($"SDK pull request details for {language}: {sdkInfo.SdkPullRequestUrl}");
                        return response;
                    }
                    else
                    {
                        response.ResponseError = $"No SDK pull request details found for {language} in release plan work item.";
                        return response;
                    }
                }

                // Find SDK details from build pipeline run
                var pipeline = await devopsService.GetPipelineRunAsync(buildId);
                if (pipeline == null)
                {
                    response.ResponseError = $"Failed to get SDK generation pipeline run with build ID {buildId}";
                    return response;
                }

                if (pipeline.Status != BuildStatus.Completed)
                {
                    response.Details.Add($"SDK generation pipeline is not in completed status to get generated SDK pull request details, Status: {pipeline.Status}. For more details: {DevOpsService.GetPipelineUrl(buildId)}");
                    return response;
                }

                if (pipeline.Result != BuildResult.Succeeded && pipeline.Result != BuildResult.PartiallySucceeded)
                {
                    response.ResponseError = $"SDK generation pipeline did not succeed. Status: {pipeline.Result?.ToString()}. For more details: {DevOpsService.GetPipelineUrl(buildId)}";
                    return response;
                }

                var pr = await devopsService.GetSDKPullRequestFromPipelineRunAsync(buildId, language, workItemId);
                response.Details.Add(pr != null ?
                    $"SDK pull request details for {language}: {pr}" :
                    $"No SDK pull request was created for {language} from SDK generation pipeline run with build ID {buildId}.");
                return response;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get SDK pull request details from SDK generation pipeline");
                return new() { ResponseError = $"Failed to get pull request details from SDK generation pipeline, Error: {ex.Message}" };
            }
        }

        public static bool IsValidLanguage(string language)
        {
            return SUPPORTED_LANGUAGES.Contains(language.ToLower());
        }
    }
}
