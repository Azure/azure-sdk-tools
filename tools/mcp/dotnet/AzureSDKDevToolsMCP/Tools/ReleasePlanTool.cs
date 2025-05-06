using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using AzureSDKDevToolsMCP.Services;
using AzureSDKDSpecTools.Helpers;
using AzureSDKDSpecTools.Models;
using AzureSDKDSpecTools.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AzureSDKDSpecTools.Tools
{
    [Description("Release Plan Tool type that contains tools to connect to Azure DevOps to get release plan work item")]
    [McpServerToolType]
    public class ReleasePlanTool(IDevOpsService _devOpsService, ITypeSpecHelper _helper, ILogger<ReleasePlanTool> _logger)
    {
        private readonly IDevOpsService devOpsService = _devOpsService;
        private readonly ITypeSpecHelper typeSpecHelper = _helper;
        private readonly ILogger<ReleasePlanTool> logger = _logger;

        [McpServerTool, Description("Get release plan for API spec pull request. This tool should be used only if work item Id is unknown.")]
        public async Task<string> GetReleasePlan(string pullRequestLink)
        {
            List<string> releasePlanList = [];
            try
            {
                var releasePlan = await devOpsService.GetReleasePlan(pullRequestLink);
                return releasePlan == null ? "Failed to get release plan details." :
                    $"Release Plan: {JsonSerializer.Serialize(releasePlan)}";
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to get release plan details: {ex.Message}");
                return $"Failed to get release plan details: {ex.Message}";
            }
        }

        [McpServerTool, Description("Get Release Plan: Get release plan work item details for a given work item id.")]
        public async Task<string> GetReleasePlanDetails(int workItemId)
        {
            try
            {
                var releasePlan = await devOpsService.GetReleasePlan(workItemId);
                var releasePlanText = releasePlan != null ? JsonSerializer.Serialize(releasePlan) :
                       "Failed to get release plan details.";
                logger.LogInformation($"Release plan details: {releasePlanText}");
                return releasePlanText;
            }
            catch (Exception ex)
            {
                return $"Failed to get release plan details: {ex.Message}";
            }
        }

        [McpServerTool, Description("Create Release Plan work item.")]
        public async Task<string> CreateReleasePlan(string typeSpecProjectPath, string targetReleaseMonthYear, string serviceTreeId, string productTreeId, string specApiVersion, string specPullRequestUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(typeSpecProjectPath))
                {
                    throw new Exception("TypeSpec project path is empty. Cannot create a release plan without a TypeSpec project root path");
                }

                var specType = typeSpecHelper.IsValidTypeSpecProjectPath(typeSpecProjectPath)? "TypeSpec" : "OpenAPI";
                var isMgmt = typeSpecHelper.IsTypeSpecProjectForMgmtPlane(typeSpecProjectPath);

                // Ensure a release plan is created only if the API specs pull request is in a public repository.
                if (!typeSpecHelper.IsRepoPathForPublicSpecRepo(typeSpecProjectPath))
                {
                    return """
                        SDK generation and release require the API specs pull request to be in the public azure-rest-api-specs repository.
                        Please create a pull request in the public Azure/azure-rest-api-specs repository to move your specs changes to public.
                        A release plan cannot be created for SDK generation using a pull request in a private repository.
                        """;
                }

                var releasePlan = new ReleasePlan
                {
                    SDKReleaseMonth = targetReleaseMonthYear,
                    ServiceTreeId = serviceTreeId,
                    ProductTreeId = productTreeId,
                    SpecAPIVersion = specApiVersion,
                    SpecType = specType,
                    IsManagementPlane = isMgmt,
                    IsDataPlane = !isMgmt,
                    SpecPullRequests = [specPullRequestUrl]
                };
                var workItem = await devOpsService.CreateReleasePlanWorkItem(releasePlan);
                return workItem != null? JsonSerializer.Serialize(workItem) : "Failed to create release plan work item.";
            }
            catch (Exception ex)
            {
                return $"Failed to create release plan work item: {ex.Message}";
            }
        }
    }
}
