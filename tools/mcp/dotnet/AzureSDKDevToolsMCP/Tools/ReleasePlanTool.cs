using System;
using System.Collections.Generic;
using System.ComponentModel;
using AzureSDKDSpecTools.Helpers;
using AzureSDKDSpecTools.Models;
using AzureSDKDSpecTools.Services;
using ModelContextProtocol.Server;

namespace AzureSDKDSpecTools.Tools
{
    [Description("Release Plan Tool type that contains tools to connect to Azure DevOps to get release plan work item")]
    [McpServerToolType]
    public class ReleasePlanTool(IDevOpsService devOpsService, ITypeSpecHelper helper)
    {
        private readonly IDevOpsService devOpsService = devOpsService;
        private readonly ITypeSpecHelper typeSpecHelper = helper;

        [McpServerTool, Description("Get release plan for a service, product and API spec pull request")]
        public async Task<List<string>> GetReleasePlan(string serviceTreeId, string productTreeId, string pullRequestLink)
        {
            List<string> releasePlanList = new List<string>();
            try
            {
                var releasePlans = await devOpsService.GetReleasePlans(serviceTreeId, productTreeId, pullRequestLink);
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
                Console.WriteLine($"Failed to get release plan details: {ex.Message}");
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
                if (releasePlan != null)
                {
                    return "{" +
                             $"URL: {releasePlan.WorkItemUrl}, " +
                             $"ID: {releasePlan.WorkItemId}," +
                             $"Title: {releasePlan.Title}," +
                             $"Service Tree ID: {releasePlan.ServiceTreeId}," +
                             $"Product Tree ID: {releasePlan.ProductTreeId}" +
                           "}";
                }
                return "Failed to get release plan details.";
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

                var specType = typeSpecHelper.IsTypeSpecProjectPath(typeSpecProjectPath)? "TypeSpec" : "OpenAPI";
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
                return workItem != null
                    ? $"Release plan work item created successfully with ID: {workItem.Id}"
                    : "Failed to create release plan work item.";
            }
            catch (Exception ex)
            {
                return $"Failed to create release plan work item: {ex.Message}";
            }
        }
    }
}
