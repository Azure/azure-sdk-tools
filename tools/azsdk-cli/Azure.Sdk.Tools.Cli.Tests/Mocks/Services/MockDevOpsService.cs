using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;

namespace Azure.Sdk.Tools.Cli.Tests.Mocks.Services
{
    internal class MockDevOpsService : IDevOpsService
    {
        // Configurable properties for testing
        public Build? ConfiguredPipelineRun { get; set; }
        public ReleasePlanWorkItem? ConfiguredReleasePlanForWorkItem { get; set; }
        public string? ConfiguredSDKPullRequest { get; set; }
        public Build? ConfiguredRunSDKGenerationPipeline { get; set; }

        public Task<List<PackageWorkitemResponse>> ListPartialPackageWorkItemAsync(string packageName, string language)
        {
            throw new NotImplementedException();
        }
        
        Task<List<ReleasePlanWorkItem>> IDevOpsService.ListOverdueReleasePlansAsync()
        {
            return Task.FromResult(new List<ReleasePlanWorkItem>());
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

        Task<WorkItem> IDevOpsService.CreateReleasePlanWorkItemAsync(ReleasePlanWorkItem releasePlan)
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

        Task<ReleasePlanWorkItem> IDevOpsService.GetReleasePlanAsync(int releasePlanId)
        {
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 1,
                ReleasePlanId = releasePlanId,
                Title = "Mock Release Plan",
                Description = "This is a mock release plan for testing purposes."
            };
            return Task.FromResult(releasePlan);
        }

        Task<ReleasePlanWorkItem> IDevOpsService.GetReleasePlanAsync(string pullRequestUrl)
        {
            var releasePlan = new ReleasePlanWorkItem
            {
                WorkItemId = 0, // Release plan does not exists
                ReleasePlanId = 1,
                Title = "Mock Release Plan",
                Description = "This is a mock release plan for testing purposes.",
                SpecPullRequests = new List<string> { pullRequestUrl },
            };
            return Task.FromResult(releasePlan);
        }

        Task<List<ReleasePlanWorkItem>> IDevOpsService.GetReleasePlansForProductAsync(string productTreeId, string specApiVersion, string sdkReleaseType, bool isTestReleasePlan)
        {
            var releasePlans = new List<ReleasePlanWorkItem>();
            return Task.FromResult(releasePlans);
        }

        Task<List<ReleasePlanWorkItem>> IDevOpsService.GetReleasePlansForPackageAsync(string packageName, string language, bool isTestReleasePlan)
        {
            var releasePlans = new List<ReleasePlanWorkItem>();
            return Task.FromResult(releasePlans);
        }
        
        Task<ReleasePlanWorkItem> IDevOpsService.GetReleasePlanForWorkItemAsync(int workItemId)
        {
            if (ConfiguredReleasePlanForWorkItem != null)
            {
                return Task.FromResult(ConfiguredReleasePlanForWorkItem);
            }
            
            var releasePlan = new ReleasePlanWorkItem
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

        Task<List<GitHubLableWorkItem>> IDevOpsService.GetGitHubLableWorkItemsAsync()
        {
            return Task.FromResult(new List<GitHubLableWorkItem>());
        }

        Task<GitHubLableWorkItem> IDevOpsService.CreateGitHubLableWorkItemAsync(string label)
        {
            return Task.FromResult(new GitHubLableWorkItem
            {
                Label = label,
                WorkItemId = 1,
                WorkItemUrl = $"https://dev.azure.com/azure-sdk/release/_workitems/edit/1"
            });
        }

        Task<ProductInfo?> IDevOpsService.GetProductInfoByTypeSpecProjectPathAsync(string typeSpecProjectPath)
        {
            // Return mock data for testing
            if (typeSpecProjectPath == "specification/testcontoso/Contoso.Management")
            {
                return Task.FromResult<ProductInfo?>(new ProductInfo
                {
                    WorkItemId = 12345,
                    Title = "Contoso Management Product",
                    ProductServiceTreeId = "12345678-1234-5678-9012-123456789012",
                    ServiceId = "87654321-4321-8765-1234-210987654321",
                    PackageDisplayName = "Contoso Management",
                    ProductServiceTreeLink = "https://servicetree.msftcloudes.com/main.html#/ServiceModel/Service/12345678-1234-5678-9012-123456789012"
                });
            }
            
            // Return null for paths without release plans
            return Task.FromResult<ProductInfo?>(null);
        }

        public Task<List<WorkItem>> FetchWorkItemsPagedAsync(string query, int top = 100000, int batchSize = 200, WorkItemExpand expand = WorkItemExpand.All)
        {
            return Task.FromResult(new List<WorkItem>());
        }
    }
}
