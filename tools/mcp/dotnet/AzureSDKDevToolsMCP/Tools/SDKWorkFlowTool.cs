using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Server;
using Octokit;

namespace AzureSDKDSpecTools.Tools
{
    public interface ISpecWorkflowTool
    {
        public Task<string> IsSpecReadyForSDKGeneration(string typeSpecProject, int pullrequestNumber);
    }

    [Description("This Type contains a set of tools to help with TypeSpec to SDK generation work flow.")]
    [McpServerToolType]
    public class SpecWorkflowTool(IMcpClient mcpClient, IGitHubService githubService, IDevOpsService devopsService, IGitHelper gitHelper, ITypeSpecHelper typespecHelper, ILogger<SpecWorkflowTool> logger)
    {
        private readonly IMcpClient _mcpClient = mcpClient;
        private readonly IGitHubService _githubService = githubService;
        private readonly IDevOpsService _devopsService = devopsService;
        private readonly IGitHelper _gitHelper = gitHelper;
        private readonly ITypeSpecHelper _typespecHelper = typespecHelper;
        private readonly ILogger<SpecWorkflowTool> _logger = logger;
        private readonly string PUBLIC_SPECS_REPO = "azure-rest-api-specs";
        private readonly string REPO_OWNER = "Azure";
        private readonly string ARM_SIGN_OFF_LABEL = "ARMSignedOff";
        private readonly string DEFAULT_BRANCH = "main";


        [McpServerTool, Description("Checks whether a TypeSpec API is ready to generate SDK. Provide a pull request number and path to TypeSpec project toot as params.")]
        public async Task<string> CheckApiReadyForSDKGeneration(string typeSpecProjectRoot, int pullrequestNumber = 0)
        {
            var response = await IsSpecReadyToGenerateSDK(typeSpecProjectRoot, pullrequestNumber);
            return JsonSerializer.Serialize(response);
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
                var repoRootPath = _gitHelper.GetRepoRootPath(typeSpecProjectRoot);
                var branchName = _gitHelper.GetBranchName(repoRootPath);
                
                // Check if current repo is private or public repo
                if (!_gitHelper.IsRepoPathForPublicSpecRepo(repoRootPath))
                {
                    response.Details.AddRange([
                        $"Current repo root path '{repoRootPath}' is not a GitHub clone of 'Azure/azure-rest-api-specs' repo. SDK can be generated only if your TypeSpec changes are in public Azure/azure-rest-api-specs repo. ",
                        "Create a pull request in public repo Azure/azure-rest-api-specs for your TypeSpec changes to get your TypeSpec ready."
                        ]);
                    return response;
                }
                await _mcpClient.SendNotificationAsync($"{repoRootPath} is a clone of azure-rest-api-specs");
                if (!_typespecHelper.IsValidTypeSpecProjectPath(typeSpecProjectRoot))
                {
                    response.Details.Add($"TypeSpec project path '{typeSpecProjectRoot}' is invalid. Provide a TypeSpec project path that contains tspconfig.yaml");
                    return response;
                }
                await _mcpClient.SendNotificationAsync($"Found a TypeSpec project in {typeSpecProjectRoot}");
                if (!_typespecHelper.IsTypeSpecProjectForMgmtPlane(typeSpecProjectRoot))
                {
                    response.Details.Add($"The TypeSpec project path '{typeSpecProjectRoot}' is not associated with the management plane. Currently, the Copilot agent supports SDK generation exclusively for the management plane.");
                    return response;
                }
                await _mcpClient.SendNotificationAsync($"TypeSpec project [{typeSpecProjectRoot}] is for management plane.");
                // if current branch name is main then ask user to provide pull request number if they have or switch to the branch they have created for TypeSpec changes.
                if (branchName.Equals(DEFAULT_BRANCH))
                {
                    response.Details.Add($"The current branch is '{DEFAULT_BRANCH}', which is not recommended for development. Please switch to a branch containing your TypeSpec project changes or create a new branch if none exists.");
                    _logger.LogWarning(response.Details.ToString());
                    return response;
                }

                // Get pull request details
                PullRequest? pullRequest = pullrequestNumber != 0 ? await _githubService.GetPullRequestAsync(REPO_OWNER, PUBLIC_SPECS_REPO, pullrequestNumber) :
                    await _githubService.GetPullRequestForBranchAsync(REPO_OWNER, PUBLIC_SPECS_REPO, branchName);
                if (pullRequest == null)
                {
                    response.Details.Add($"Pull request is not found in {REPO_OWNER}/{PUBLIC_SPECS_REPO} for your TypeSpec changes.");
                    if (pullrequestNumber == 0)
                        response.Details.Add("Do you have a pull request created for your TypeSpec changes? If not, make TypeSpec changes for your API specification and create a pull request.");
                    else
                        response.Details.Add($"Pull request {pullrequestNumber} is not valid. Please provide a valid pull requet number to check the status.");
                    _logger.LogWarning(response.Details.ToString());
                    return response;
                }

                // Pull request is not targetted to main branch
                if (pullRequest.Base?.Ref?.Equals(DEFAULT_BRANCH) == false)
                {
                    response.Details.Add($"Pull request {pullRequest.Number} merges changes to '{pullRequest.Base?.Ref}' branch. SDK can be generated only from a pull request with {DEFAULT_BRANCH} branch as target. Create a pull request for your changes with '{DEFAULT_BRANCH}' branch as target.");
                    _logger.LogWarning(response.Details.ToString());
                    return response;
                }

                // PR closed without merging changes
                if (pullRequest.State == ItemState.Closed && !pullRequest.Merged)
                {
                    response.Details.Add($"Pull request {pullRequest.Number} is in closed status without merging changes to main branch. SDK can not be generated from closed PR. Create a pull request for your changes with '{DEFAULT_BRANCH}' branch as target.");
                    _logger.LogWarning(response.Details.ToString());
                    return response;
                }

                if (pullRequest.Labels.Any(l => l.Name.Equals(ARM_SIGN_OFF_LABEL)) && (pullRequest.Merged || pullRequest.Mergeable == false))
                {
                    response.Details.Add($"Pull request {pullRequest.Number} has ARM approval or it is in merged status. Your API spec changes are ready to generate SDK. Please make sure you have a release plan created for the pull request.");
                    response.Status = "Success";
                    _logger.LogWarning(response.Details.ToString());
                    return response;
                }
                else
                {
                    response.Details.Add($"Pull request {pullRequest.Number} does not have ARM approval. Your API spec changes are not ready to generate SDK. Please check pull request details to get more information on next step for your pull request");
                    _logger.LogWarning(response.Details.ToString());
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


        /*[McpServerTool, Description("This tool runs pipeline to generate SDK for a TypeSpec project. This tool calls IsSpecReadyForSDKGeneration to make sure Spec is ready to generate SDK.")]
        public async Task<string> GenerateSDK(string typespecProjectRoot, string apiVersion, string sdkReleaseType, string language, int pullrequestNumber)
        {
            var response = new GenericResponse()
            {
                Status = "Failed"
            };

            if (!IsSDKGenerationSupported(language))
            {
                response.Details.Add($"SDK generation is currently not supported by agent for {language}");
                return JsonSerializer.Serialize(response);
            }


        }

        private bool IsSDKGenerationSupported(string language)
        {
            switch (language)
            {
                case "Python": return true;
                default: return false;
            }
        }

        private async Task RunSDKGenerationPipeline(string branchRef, string language)
        {

        }*/
    }
}
