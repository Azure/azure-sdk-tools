using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
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

        [McpServerTool, Description("Get release plan for a service, product and API spec pull request")]
        public async Task<List<string>> GetReleasePlan(string serviceTreeId, string productTreeId, string pullRequestLink)
        {
            List<string> releasePlanList = [];
            try
            {
                if (string.IsNullOrEmpty(serviceTreeId) || string.IsNullOrEmpty(pullRequestLink))
                {
                    releasePlanList.Add("Faield to get release plan. Service tree ID and pull request link are required to check if a release plan already exists.");
                    return releasePlanList;
                }
                Guid? productId = !string.IsNullOrEmpty(productTreeId) ? Guid.Parse(productTreeId) : null;
                var releasePlans = await devOpsService.GetReleasePlans(Guid.Parse(serviceTreeId), productId, pullRequestLink);
                if (releasePlans == null || releasePlans.Count == 0)
                {
                    releasePlanList.Add("Failed to get release plan details.");
                }
                else
                {
                    releasePlanList.Add($"Release Plan details for service: {serviceTreeId}, product: {productTreeId}, pull request: {pullRequestLink}");
                    foreach (var releasePlan in releasePlans)
                    {
                        releasePlanList.Add($"work Item ID: {releasePlan.WorkItemId}, URL: {releasePlan.WorkItemUrl}, Status: {releasePlan.Status}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to get release plan details: {ex.Message}");
                releasePlanList.Add($"Failed to get release plan details: {ex.Message}");
            }
            return releasePlanList;
        }

        [McpServerTool, Description("Get Release Plan: Get release plan work item details for a given work item id.")]
        public async Task<string> GetReleasePlanDetails(int workItemId)
        {
            try
            {
                var releasePlan = await devOpsService.GetReleasePlan(workItemId);
                return releasePlan != null ? JsonSerializer.Serialize(releasePlan) :
                       "Failed to get release plan details.";
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
                return workItem != null ? JsonSerializer.Serialize(workItem) : "Failed to create release plan work item.";
            }
            catch (Exception ex)
            {
                return $"Failed to create release plan work item: {ex.Message}";
            }
        }
    }
}
