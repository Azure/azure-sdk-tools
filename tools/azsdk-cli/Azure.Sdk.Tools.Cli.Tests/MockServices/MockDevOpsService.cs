using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace Azure.Sdk.Tools.Cli.Tests.MockServices
{
    internal class MockDevOpsService : IDevOpsService
    {
        Task<bool> IDevOpsService.AddSdkInfoInReleasePlanAsync(int workItemId, string language, string sdkGenerationPipelineUrl, string sdkPullRequestUrl)
        {
            throw new NotImplementedException();
        }

        Task<WorkItem> IDevOpsService.CreateReleasePlanWorkItemAsync(ReleasePlan releasePlan)
        {
            var workItem = new WorkItem
            {
                Id = 1,
                Fields = new Dictionary<string, object>
                {
                    { "System.Title", releasePlan.Title },
                    { "System.Description", releasePlan.Description },
                    { "System.State", "New" }
                }
            };
            return Task.FromResult(workItem);
        }

        Task<PackageResponse> IDevOpsService.GetPackageWorkItemAsync(string packageName, string language, string packageVersion)
        {
            throw new NotImplementedException();
        }

        Task<Build> IDevOpsService.GetPipelineRunAsync(int buildId)
        {
            throw new NotImplementedException();
        }

        Task<ReleasePlan> IDevOpsService.GetReleasePlanAsync(int releasePlanId)
        {
            var releasePlan = new ReleasePlan
            {
                WorkItemId = 1,
                ReleasePlanId = releasePlanId,
                Title = "Mock Release Plan",
                Description = "This is a mock release plan for testing purposes."
            };
            return Task.FromResult(releasePlan);
        }

        Task<ReleasePlan> IDevOpsService.GetReleasePlanAsync(string pullRequestUrl)
        {
            var releasePlan = new ReleasePlan
            {
                WorkItemId = 0, // Release plan does not exists
                ReleasePlanId = 1,
                Title = "Mock Release Plan",
                Description = "This is a mock release plan for testing purposes.",
                SpecPullRequests = new List<string> { pullRequestUrl },
            };
            return Task.FromResult(releasePlan);
        }

        Task<ReleasePlan> IDevOpsService.GetReleasePlanForWorkItemAsync(int workItemId)
        {
            var releasePlan = new ReleasePlan
            {
                WorkItemId = workItemId,
                ReleasePlanId = 1,
                Title = "Mock Release Plan",
                Description = "This is a mock release plan for testing purposes."
            };
            return Task.FromResult(releasePlan);
        }

        Task<string> IDevOpsService.GetSDKPullRequestFromPipelineRunAsync(int buildId, string language, int workItemId)
        {
            // Simulate fetching a pull request URL based on the build ID and language
            return Task.FromResult($"https://github.com/Azure/azure-sdk-for-{language}/pull/1");
        }

        Task<bool> IDevOpsService.LinkNamespaceApprovalIssueAsync(int releasePlanWorkItemId, string url)
        {
            return Task.FromResult(true);
        }

        Task<Build> IDevOpsService.RunSDKGenerationPipelineAsync(string branchRef, string typespecProjectRoot, string apiVersion, string sdkReleaseType, string language, int workItemId)
        {
            throw new NotImplementedException();
        }

        Task<bool> IDevOpsService.UpdateApiSpecStatusAsync(int workItemId, string status)
        {
            return Task.FromResult(true);
        }

        Task<bool> IDevOpsService.UpdateReleasePlanSDKDetailsAsync(int workItemId, List<SDKInfo> sdkLanguages)
        {
            return Task.FromResult(true);
        }

        Task<bool> IDevOpsService.UpdateSpecPullRequestAsync(int releasePlanWorkItemId, string specPullRequest)
        {
            return Task.FromResult(true);
        }
    }
}
