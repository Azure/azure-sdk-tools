using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure;
using AzureSDKDevToolsMCP.Helpers;
using AzureSDKDevToolsMCP.Services;
using AzureSDKDSpecTools.Helpers;
using AzureSDKDSpecTools.Models;
using AzureSDKDSpecTools.Services;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Server;

namespace AzureSDKDSpecTools.Tools
{
    [Description("This Type contains a set of tools to help with TypeSpec to SDK generation work flow.")]
    [McpServerToolType]
    public class SpecWorkflowTool(IGitHubService githubService,
        IDevOpsService devopsService, 
        IGitHelper gitHelper, 
        ITypeSpecHelper typespecHelper, 
        ILogger<SpecWorkflowTool> logger,
        ISpecPullRequestHelper specHelper)
    {
        private readonly IGitHubService _githubService = githubService;
        private readonly IDevOpsService _devopsService = devopsService;
        private readonly IGitHelper _gitHelper = gitHelper;
        private readonly ITypeSpecHelper _typespecHelper = typespecHelper;
        private readonly ILogger<SpecWorkflowTool> _logger = logger;
        private readonly ISpecPullRequestHelper _specHelper = specHelper;
        private readonly static string PUBLIC_SPECS_REPO = "azure-rest-api-specs";
        private readonly static string REPO_OWNER = "Azure";
        private readonly static string ARM_SIGN_OFF_LABEL = "ARMSignedOff";
        private readonly static string DEFAULT_BRANCH = "main";


        [McpServerTool, Description("Checks whether a TypeSpec API spec is ready to generate SDK. Provide a pull request number and path to TypeSpec project toot as params.")]
        public async Task<string> CheckApiReadyForSDKGeneration(string typeSpecProjectRoot, int pullrequestNumber = 0)
        {
            var response = await IsSpecReadyToGenerateSDK(typeSpecProjectRoot, pullrequestNumber);
            return response.ToString();
        }

        private async Task<GenericResponse> IsSpecReadyToGenerateSDK(string typeSpecProjectRoot, int pullrequestNumber)
        {
            GenericResponse response = new GenericResponse()
            {
                Status = "Failed"
            };

            try
            {
                if (string.IsNullOrEmpty(typeSpecProjectRoot) && pullrequestNumber == 0)
                {
                    response.Details.Add("Invalid value for both TypeSpec project root and pull request number. Provide atleast the TypeSpec project root path for modified project or provide a pull request number.");
                    return response;
                }

                // Get current branch name
                var repoRootPath = _typespecHelper.GetSpecRepoRootPath(typeSpecProjectRoot);
                var branchName = _gitHelper.GetBranchName(repoRootPath);
                
                // Check if current repo is private or public repo
                if (!_typespecHelper.IsRepoPathForPublicSpecRepo(repoRootPath))
                {
                    response.Details.AddRange([
                        $"Current repo root path '{repoRootPath}' is not a GitHub clone of 'Azure/azure-rest-api-specs' repo. SDK can be generated only if your TypeSpec changes are in public Azure/azure-rest-api-specs repo. ",
                        "Create a pull request in public repo Azure/azure-rest-api-specs for your TypeSpec changes to get your TypeSpec ready."
                        ]);
                    return response;
                }

                if (!_typespecHelper.IsValidTypeSpecProjectPath(typeSpecProjectRoot))
                {
                    response.Details.Add($"TypeSpec project path '{typeSpecProjectRoot}' is invalid. Provide a TypeSpec project path that contains tspconfig.yaml");
                    return response;
                }


                // if current branch name is main then ask user to provide pull request number if they have or switch to the branch they have created for TypeSpec changes.
                if (branchName.Equals(DEFAULT_BRANCH))
                {
                    response.Details.Add($"The current branch is '{DEFAULT_BRANCH}', which is not recommended for development. Please switch to a branch containing your TypeSpec project changes or create a new branch if none exists.");
                    return response;
                }

                // Get pull request details
                Octokit.PullRequest? pullRequest = pullrequestNumber != 0 ? await _githubService.GetPullRequestAsync(REPO_OWNER, PUBLIC_SPECS_REPO, pullrequestNumber) :
                    await _githubService.GetPullRequestForBranchAsync(REPO_OWNER, PUBLIC_SPECS_REPO, branchName);
                if (pullRequest == null)
                {
                    response.Details.Add($"Pull request is not found in {REPO_OWNER}/{PUBLIC_SPECS_REPO} for your TypeSpec changes.");
                    if (pullrequestNumber == 0)
                        response.Details.Add("Do you have a pull request created for your TypeSpec changes? If not, make TypeSpec changes for your API specification and create a pull request.");
                    else
                        response.Details.Add($"Pull request {pullrequestNumber} is not valid. Please provide a valid pull requet number to check the status.");
                    return response;
                }

                // Pull request is not targetted to main branch
                if (pullRequest.Base?.Ref?.Equals(DEFAULT_BRANCH) == false)
                {
                    response.Details.Add($"Pull request {pullRequest.Number} merges changes to '{pullRequest.Base?.Ref}' branch. SDK can be generated only from a pull request with {DEFAULT_BRANCH} branch as target. Create a pull request for your changes with '{DEFAULT_BRANCH}' branch as target.");
                    return response;
                }

                // PR closed without merging changes
                if (pullRequest.State == Octokit.ItemState.Closed && !pullRequest.Merged)
                {
                    response.Details.Add($"Pull request {pullRequest.Number} is in closed status without merging changes to main branch. SDK can not be generated from closed PR. Create a pull request for your changes with '{DEFAULT_BRANCH}' branch as target.");
                    return response;
                }

                var isMgmtPlane = _typespecHelper.IsTypeSpecProjectForMgmtPlane(typeSpecProjectRoot);
                if (isMgmtPlane && !pullRequest.Labels.Any(l => l.Name.Equals(ARM_SIGN_OFF_LABEL)))
                {
                    response.Details.Add($"Pull request {pullRequest.Number} does not have ARM approval. Your API spec changes are not ready to generate SDK. Please check pull request details to get more information on next step for your pull request");
                    return response;
                }

                if(isMgmtPlane)
                {
                    response.Details.Add($"Pull request {pullRequest.Number} has ARM approval or it is in merged status. Your API spec changes are ready to generate SDK. Please make sure you have a release plan created for the pull request.");
                    response.Status = "Success";
                    return response;
                }
                else
                {
                    response.Details.Add($"Your API spec changes are ready to generate SDK. Please make sure you have a release plan created for the pull request.");
                    response.Status = "Success";
                    return response;
                }
            }
            catch (Exception ex)
            {
                response.Status = "Failed";
                response.Details.Add($"Failed to check if TypeSpec is ready for SDK generation. Error: {ex.Message}");
                return response;
            }            
        }


        [McpServerTool, Description("This tool runs pipeline to generate SDK for a TypeSpec project. This tool calls IsSpecReadyForSDKGeneration to make sure Spec is ready to generate SDK.")]
        public async Task<string> GenerateSDK(string typespecProjectRoot, string apiVersion, string sdkReleaseType, string language, int pullrequestNumber, int workItemId)
        {
            var response = new GenericResponse()
            {
                Status = "Success"
            };

            try
            {
                if (!DevOpsService.IsSDKGenerationSupported(language))
                {
                    response.Details.Add($"SDK generation is currently not supported by agent for {language}");
                    response.Status = "Failed";
                }

                // Verify input
                if (!TypeSpecProject.IsValidTypeSpecProjectPath(typespecProjectRoot))
                {
                    response.Details.Add($"Invalid TypeSpec project root path [{typespecProjectRoot}].");
                    response.Status = "Failed";
                }

                if (string.IsNullOrEmpty(apiVersion))
                {
                    response.Details.Add("API version is required to generate SDK.");
                    response.Status = "Failed";
                }

                List<string> validReleaseTypes = ["beta", "stable"];
                sdkReleaseType = sdkReleaseType?.ToLower() ?? "";
                if (string.IsNullOrEmpty(sdkReleaseType) || !validReleaseTypes.Contains(sdkReleaseType))
                {
                    response.Details.Add("SDK release type must be set as either beta or stable to generate SDK.");
                    response.Status = "Failed";
                }

                // Verify if spec is read yto generate SDK
                var readiness = await IsSpecReadyToGenerateSDK(typespecProjectRoot, pullrequestNumber);
                if (!readiness.Status.Equals("Success"))
                {
                    response.Details.AddRange(readiness.Details);
                    response.Status = "Failed";
                }

                // Get Pull request details and check if pr is merged or not. if merged then run the pipeline against the target branch or against pr merge ref
                var pullRequest = await _githubService.GetPullRequestAsync(REPO_OWNER, PUBLIC_SPECS_REPO, pullrequestNumber);
                if (pullRequest == null)
                {
                    response.Details.Add($"Failed to get pull request details for {pullrequestNumber} in {REPO_OWNER}/{PUBLIC_SPECS_REPO}");
                    response.Status = "Failed";
                }

                // Return failure details in case of any failure
                if (response.Status.Equals("Failed"))
                    return response.ToString();

                string typeSpecProjectPath = _typespecHelper.GetTypeSpecProjectRelativePath(typespecProjectRoot);
                string branchRef = (pullRequest?.Merged ?? false) ? pullRequest.Base.Ref : $"refs/pull/{pullrequestNumber}/merge";
                var pipelineRun = await _devopsService.RunSDKGenerationPipeline(branchRef, typeSpecProjectPath, apiVersion, sdkReleaseType, language, workItemId);
                response = new GenericResponse()
                {
                    Status = "Success",
                    Details = [$"Azure DevOps pipeline {pipelineRun.Url} has been initiated to generate the SDK. Build ID is {pipelineRun.Id}. Once the pipeline job completes, an SDK pull request for {language} will be created."]
                };
                return response.ToString();
            }
            catch(Exception ex)
            {
                response.Details.Add($"Failed to run pipeline to generate SDK, Details: {ex.Message}");
                response.Status = "Failed";
                return response.ToString();
            }            
        }

        /// <summary>
        /// Get SDK generation pipeline run details and status for a given pipeline build ID
        /// </summary>
        /// <param name="buildId">Build ID for the pipeline run</param>
        /// <returns></returns>
        [McpServerTool, Description("Get SDK generation pipeline run details and status for a given pipeline build ID")]
        public async Task<string> GetPipelineRunStatus(int buildId)
        {
            var response = new GenericResponse();
            try
            {
                var pipeline = await _devopsService.GetPipelineRun(buildId);
                if (pipeline != null)
                {
                    response.Status = pipeline.Result?.ToString() ?? "Not available";
                    response.Details.Add($"Pipeline run link: {DevOpsService.GetPipelineUrl(pipeline.Id)}");
                }
            }
            catch (Exception ex)
            {
                response.Status = "Failed";
                response.Details.Add($"Failed to get pipeline run with id {buildId}. Error: {ex.Message}");
            }
            return response.ToString();
        }

        /// <summary>
        /// Get SDK pull request link from SDK generation pipeline.
        /// </summary>
        /// <param name="language">SDK Language</param>
        /// <param name="buildId">Build ID for the pipeline run</param>
        /// <param name="workItemId">Work item ID for the release plan</param>
        /// <returns></returns>
        [McpServerTool, Description("Get generated SDK pull request link from SDK generation pipeline run, Build ID of pipeline run is required to query pull request link.")]
        public async Task<string> GetSDKPullRequestDetails(string language, int buildId, int workItemId)
        {
            try
            {
                //Todo: If buildId is given as 0 then we should find all build triggered by current user and check for the latest build triggered for the language.
                var pipeline = await _devopsService.GetPipelineRun(buildId);
                if (pipeline == null)
                {
                    return $"Failed to get SDK generation pipeline run wiht build ID {buildId}";
                }

                if(pipeline.Status != BuildStatus.Completed)
                {
                    return $"SDK generation pipeline is not in completed status to get generated SDK pull request details, Status: {pipeline.Status.ToString()}. For more details: {DevOpsService.GetPipelineUrl(buildId)}";
                }

                if (pipeline.Result != BuildResult.Succeeded && pipeline.Result != BuildResult.PartiallySucceeded)
                {
                    return $"SDK generation pipeline did not succeed. Status: {pipeline.Result?.ToString()}. For more details: {DevOpsService.GetPipelineUrl(buildId)}";
                }

                return await _devopsService.GetSDKPullRequestFromPipelineRun(buildId, language, workItemId);
            }
            catch (Exception ex)
            {
                return $"Failed to get pull request details from SDK generation pipeline, Error: {ex.Message}";
            }
        }
    }
}
