using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Cli.Tests.Mocks.Services
{
    internal class MockDevOpsService : IDevOpsService
    {
        // Configurable properties for testing
        public Build? ConfiguredPipelineRun { get; set; }
        public ReleasePlanDetails? ConfiguredReleasePlanForWorkItem { get; set; }
        public string? ConfiguredSDKPullRequest { get; set; }
        public Build? ConfiguredRunSDKGenerationPipeline { get; set; }

        public Task<List<PackageWorkitemResponse>> ListPartialPackageWorkItemAsync(string packageName, string language)
        {
            throw new NotImplementedException();
        }
        
        Task<List<ReleasePlanDetails>> IDevOpsService.ListOverdueReleasePlansAsync()
        {
            return Task.FromResult(new List<ReleasePlanDetails>());
        }
        
        public Task<PackageWorkitemResponse> GetPackageWorkItemAsync(string packageName, string language, string packageVersion = "")
        {
            var sdkLanguage = SdkLanguageHelpers.GetSdkLanguage(language);
            var version = string.IsNullOrEmpty(packageVersion) ? "1.0.0" : packageVersion;
            
            return Task.FromResult(
                new PackageWorkitemResponse
                {
                    PackageName = packageName,
                    Language = sdkLanguage,
                    ResponseError = null,
                    PipelineDefinitionUrl = "https://dev.azure.com/fake-org/fake-project/_build?definitionId=1",
                    WorkItemId = 0,
                    changeLogStatus = "Approved",
                    APIViewStatus = "Approved",
                    PackageNameStatus = "Approved",
                    PackageRepoPath = "template",
                    LatestPipelineRun = "https://dev.azure.com/fake-org/fake-project/_build/results?buildId=1",
                    LatestPipelineStatus = "Succeeded",
                    WorkItemUrl = "https://dev.azure.com/fake-org/fake-project/_workitems/edit/12345",
                    State = "Active",
                    PlannedReleaseDate = "06/30/2025",
                    DisplayName = packageName,
                    Version = version,
                    PlannedReleases = new List<SDKReleaseInfo>
                    {
                        new() {
                            Version = version,
                            ReleaseDate = "06/30/2025",
                            ReleaseType = "GA"
                        }
                    },
                }
            );
        }

        public Task<Build> RunPipelineAsync(int pipelineDefinitionId, Dictionary<string, string> templateParams, string apiSpecBranchRef = "main")
        {
            return Task.FromResult(new Build
            {
                Id = 1,
                Status = BuildStatus.InProgress,
                Result = BuildResult.None,
                Url = "https://dev.azure.com/fake-org/fake-project/_build/results?buildId=1"
            });
        }

        Task<bool> IDevOpsService.AddSdkInfoInReleasePlanAsync(int workItemId, string language, string sdkGenerationPipelineUrl, string sdkPullRequestUrl, string generationStatus)
        {
            return Task.FromResult(true);
        }

        Task<WorkItem> IDevOpsService.CreateReleasePlanWorkItemAsync(ReleasePlanDetails releasePlan)
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

        Task<Build> IDevOpsService.GetPipelineRunAsync(int buildId)
        {
            return Task.FromResult(ConfiguredPipelineRun);
        }

        Task<ReleasePlanDetails> IDevOpsService.GetReleasePlanAsync(int releasePlanId)
        {
            var releasePlan = new ReleasePlanDetails
            {
                WorkItemId = 1,
                ReleasePlanId = releasePlanId,
                Title = "Mock Release Plan",
                Description = "This is a mock release plan for testing purposes."
            };
            return Task.FromResult(releasePlan);
        }

        Task<ReleasePlanDetails> IDevOpsService.GetReleasePlanAsync(string pullRequestUrl)
        {
            var releasePlan = new ReleasePlanDetails
            {
                WorkItemId = 0, // Release plan does not exists
                ReleasePlanId = 1,
                Title = "Mock Release Plan",
                Description = "This is a mock release plan for testing purposes.",
                SpecPullRequests = new List<string> { pullRequestUrl },
            };
            return Task.FromResult(releasePlan);
        }

        Task<List<ReleasePlanDetails>> IDevOpsService.GetReleasePlansForProductAsync(string productTreeId, string specApiVersion, string sdkReleaseType, bool isTestReleasePlan)
        {
            var releasePlans = new List<ReleasePlanDetails>();
            return Task.FromResult(releasePlans);
        }
        
        Task<ReleasePlanDetails> IDevOpsService.GetReleasePlanForWorkItemAsync(int workItemId)
        {
            if (ConfiguredReleasePlanForWorkItem != null)
            {
                return Task.FromResult(ConfiguredReleasePlanForWorkItem);
            }
            
            var releasePlan = new ReleasePlanDetails
            {
                WorkItemId = workItemId,
                ReleasePlanId = 1,
                Title = "Mock Release Plan",
                Description = "This is a mock release plan for testing purposes."
            };
            releasePlan.IsDataPlane = workItemId > 1000;
            releasePlan.IsManagementPlane = !releasePlan.IsDataPlane;
            return Task.FromResult(releasePlan);
        }

        Task<string> IDevOpsService.GetSDKPullRequestFromPipelineRunAsync(int buildId, string language, int workItemId)
        {
            if (ConfiguredSDKPullRequest != null)
            {
                return Task.FromResult(ConfiguredSDKPullRequest);
            }
            
            // Simulate fetching a pull request URL based on the build ID and language
            return Task.FromResult($"https://github.com/Azure/azure-sdk-for-{language}/pull/1");
        }

        Task<bool> IDevOpsService.LinkNamespaceApprovalIssueAsync(int releasePlanWorkItemId, string url)
        {
            return Task.FromResult(true);
        }

        Task<Build> IDevOpsService.RunSDKGenerationPipelineAsync(string apiSpecBranchRef, string typespecProjectRoot, string apiVersion, string sdkReleaseType, string language, int workItemId, string sdkRepoBranch)
        {
            if (ConfiguredRunSDKGenerationPipeline != null)
            {
                return Task.FromResult(ConfiguredRunSDKGenerationPipeline);
            }
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

        Task<Dictionary<string, List<string>>> IDevOpsService.GetPipelineLlmArtifacts(string project, int buildId)
        {
            return Task.FromResult(new Dictionary<string, List<string>>());
        }

        Task<WorkItem> IDevOpsService.UpdateWorkItemAsync(int workItemId, Dictionary<string, string> fields)
        {
            var workItem = new WorkItem
            {
                Id = workItemId,
                Fields = new Dictionary<string, object>
                {
                    { "System.Title", "Updated work item" },
                    { "System.State", "In Progress" }
                }
            };
            return Task.FromResult(workItem);
        }
    }
}
