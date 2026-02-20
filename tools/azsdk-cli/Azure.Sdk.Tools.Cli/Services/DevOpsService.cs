// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Core;
using Azure.Identity;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.VisualStudio.Services.OAuth;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using Microsoft.VisualStudio.Services.WebApi;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Models.Responses;
using System.Globalization;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;

namespace Azure.Sdk.Tools.Cli.Services
{
    public interface IDevOpsConnection
    {
        public BuildHttpClient GetBuildClient();
        public WorkItemTrackingHttpClient GetWorkItemClient();
        public ProjectHttpClient GetProjectClient();
    }

    public class DevOpsConnection(IAzureService azureService) : IDevOpsConnection
    {
        private BuildHttpClient _buildClient;
        private WorkItemTrackingHttpClient _workItemClient;
        private ProjectHttpClient _projectClient;
        private AccessToken? _token;

        private void RefreshConnection()
        {
            if (_token != null && _token?.ExpiresOn > DateTimeOffset.Now.AddMinutes(5))
            {
                return;
            }

            var credential = azureService.GetCredential();
            try
            {
                _token = credential.GetToken(new TokenRequestContext([Constants.AZURE_DEVOPS_TOKEN_SCOPE]), CancellationToken.None);
            }
            catch
            {
                credential = new InteractiveBrowserCredential(new InteractiveBrowserCredentialOptions { TenantId = null });
                // Retry with interactive browser credential if the initial credential fails
                _token = credential.GetToken(new TokenRequestContext([Constants.AZURE_DEVOPS_TOKEN_SCOPE]), CancellationToken.None);
            }
            // If we still don't have a token, throw an exception
            if (_token == null)
            {
                throw new Exception("Failed to get devops access token. " +
                                    "Ensure you have access to the azure-sdk devops project (http://aka.ms/azsdk/access)" +
                                    "and are logged in via az cli, az powershell, vs/vscode or interactive browser sign-in.");
            }

            var connection = new VssConnection(new Uri(Constants.AZURE_SDK_DEVOPS_BASE_URL), new VssOAuthAccessTokenCredential(_token?.Token));
            _buildClient = connection.GetClient<BuildHttpClient>();
            _workItemClient = connection.GetClient<WorkItemTrackingHttpClient>();
            _projectClient = connection.GetClient<ProjectHttpClient>();
        }

        public BuildHttpClient GetBuildClient()
        {
            RefreshConnection();
            return _buildClient;
        }

        public WorkItemTrackingHttpClient GetWorkItemClient()
        {
            RefreshConnection();
            return _workItemClient;
        }

        public ProjectHttpClient GetProjectClient()
        {
            RefreshConnection();
            return _projectClient;
        }
    }

    public interface IDevOpsService
    {
        public Task<List<ReleasePlanWorkItem>> ListOverdueReleasePlansAsync();
        public Task<ReleasePlanWorkItem> GetReleasePlanAsync(int releasePlanId);
        public Task<ReleasePlanWorkItem> GetReleasePlanForWorkItemAsync(int workItemId);
        public Task<ReleasePlanWorkItem> GetReleasePlanAsync(string pullRequestUrl);
        public Task<List<ReleasePlanWorkItem>> GetReleasePlansForProductAsync(string productTreeId, string specApiVersion, string sdkReleaseType, bool isTestReleasePlan = false);
        public Task<List<ReleasePlanWorkItem>> GetReleasePlansForPackageAsync(string packageName, string language, bool isTestReleasePlan = false);
        public Task<WorkItem> CreateReleasePlanWorkItemAsync(ReleasePlanWorkItem releasePlan);
        public Task<Build> RunSDKGenerationPipelineAsync(string apiSpecBranchRef, string typespecProjectRoot, string apiVersion, string sdkReleaseType, string language, int workItemId, string sdkRepoBranch = "");
        public Task<Build> GetPipelineRunAsync(int buildId);
        public Task<string> GetSDKPullRequestFromPipelineRunAsync(int buildId, string language, int workItemId);
        public Task<bool> AddSdkInfoInReleasePlanAsync(int workItemId, string language, string sdkGenerationPipelineUrl, string sdkPullRequestUrl, string generationStatus = "");
        public Task<bool> UpdateReleasePlanSDKDetailsAsync(int workItemId, List<SDKInfo> sdkLanguages);
        public Task<bool> UpdateApiSpecStatusAsync(int workItemId, string status);
        public Task<bool> UpdateSpecPullRequestAsync(int releasePlanWorkItemId, string specPullRequest);
        public Task<bool> LinkNamespaceApprovalIssueAsync(int releasePlanWorkItemId, string url);
        public Task<PackageWorkitemResponse> GetPackageWorkItemAsync(string packageName, string language, string packageVersion = "");
        public Task<List<PackageWorkitemResponse>> ListPartialPackageWorkItemAsync(string packageName, string language);
        public Task<Build> RunPipelineAsync(int pipelineDefinitionId, Dictionary<string, string> templateParams, string apiSpecBranchRef = "main");
        public Task<Dictionary<string, List<string>>> GetPipelineLlmArtifacts(string project, int buildId);
        public Task<WorkItem> UpdateWorkItemAsync(int workItemId, Dictionary<string, string> fields);
        public Task<List<GitHubLableWorkItem>> GetGitHubLableWorkItemsAsync();
        public Task<GitHubLableWorkItem> CreateGitHubLableWorkItemAsync(string label);
        public Task<ProductInfo?> GetProductInfoByTypeSpecProjectPathAsync(string typeSpecProjectPath);
        Task<List<WorkItem>> FetchWorkItemsPagedAsync(string query, int top = 100000, int batchSize = 200, WorkItemExpand expand = WorkItemExpand.All);
    }

    public partial class DevOpsService(ILogger<DevOpsService> logger, IDevOpsConnection connection) : IDevOpsService
    {
        private static readonly string RELEASE_PLANNER_APP_TEST = "Release Planner App Test";
        private List<WorkItemRelationType>? _cachedRelationTypes;

        private static readonly string[] SUPPORTED_SDK_LANGUAGES = { "Dotnet", "JavaScript", "Python", "Java", "Go" };

        [GeneratedRegex("\\|\\s(Beta|Stable|GA)\\s\\|\\s([\\S]+)\\s\\|\\s([\\S]+)\\s\\|")]
        private static partial Regex SdkReleaseDetailsRegex();

        private async Task<List<WorkItemRelationType>> GetCachedRelationTypes() =>
            _cachedRelationTypes ??= await connection.GetWorkItemClient().GetRelationTypesAsync();

        public async Task<List<ReleasePlanWorkItem>> ListOverdueReleasePlansAsync()
        {
            try
            {
                var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT}'";
                query += $" AND [System.Tags] NOT CONTAINS '{RELEASE_PLANNER_APP_TEST}'";
                query += " AND [System.WorkItemType] = 'Release Plan'";
                query += " AND [System.State] IN ('In Progress','Not Started','New')";
                query += " AND [Custom.SDKReleasemonth] <> ''";

                var releasePlanWorkItems = await FetchWorkItemsPagedAsync(query);
                var releasePlans = await Task.WhenAll(releasePlanWorkItems.Select(workItem => MapWorkItemToReleasePlanAsync(workItem)));

                var today = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var overduePlans = releasePlans.Where(releasePlan =>
                {
                    if (DateTime.TryParseExact(releasePlan.SDKReleaseMonth, "MMMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var releaseDate))
                    {
                        var normalizedReleaseDate = new DateTime(releaseDate.Year, releaseDate.Month, 1);
                        return normalizedReleaseDate < today;
                    }
                    return false;
                }).ToList();
                return overduePlans;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to list overdue release plans");
                throw new Exception("Failed to list overdue release plans. Error: {ex}", ex);
            }
        }

        public async Task<ReleasePlanWorkItem> GetReleasePlanForWorkItemAsync(int workItemId)
        {
            logger.LogInformation("Fetching release plan work with id {workItemId}", workItemId);
            var workItem = await connection.GetWorkItemClient().GetWorkItemAsync(workItemId, expand: WorkItemExpand.All);
            if (workItem?.Id == null)
            {
                throw new InvalidOperationException($"Work item {workItemId} not found.");
            }
            var releasePlan = await MapWorkItemToReleasePlanAsync(workItem);
            releasePlan.WorkItemUrl = workItem.Url;
            releasePlan.WorkItemId = workItem?.Id ?? 0;
            return releasePlan;
        }

        public async Task<ReleasePlanWorkItem> GetReleasePlanAsync(int releasePlanId)
        {
            // First find the API spec work item
            var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT}' AND [Custom.ReleasePlanID] = '{releasePlanId}' AND [System.WorkItemType] = 'Release Plan' AND [System.State] NOT IN ('Closed','Duplicate','Abandoned')";
            var releasePlanWorkItems = await FetchWorkItemsAsync(query);
            if (releasePlanWorkItems.Count == 0)
            {
                throw new Exception($"Failed to find release plan work item with release plan Id {releasePlanId}");
            }
            return await MapWorkItemToReleasePlanAsync(releasePlanWorkItems[0]);
        }

        public async Task<List<ReleasePlanWorkItem>> GetReleasePlansForProductAsync(string productTreeId, string specApiVersion, string sdkReleaseType, bool isTestReleasePlan=false)
        {
            try
            {
                var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT}'";
                query += $" AND [System.Tags] {(isTestReleasePlan ? "CONTAINS" : "NOT CONTAINS")} '{RELEASE_PLANNER_APP_TEST}'";
                query += $" AND [Custom.ProductServiceTreeID] = '{productTreeId}'";
                query += $" AND [Custom.SDKtypetobereleased] = '{sdkReleaseType}'";
                query += " AND [System.WorkItemType] = 'Release Plan'";
                query += " AND [System.State] IN ('New','Not Started','In Progress')";
                var releasePlanWorkItems = await FetchWorkItemsAsync(query);
                if (releasePlanWorkItems.Count == 0)
                {
                    logger.LogInformation("Release plan does not exist for the given product id {productTreeId}",productTreeId);
                    return new List<ReleasePlanWorkItem>();
                }

                var releasePlans = new List<ReleasePlanWorkItem>();

                foreach (var workItem in releasePlanWorkItems)
                {
                    var releasePlan = await MapWorkItemToReleasePlanAsync(workItem);
                    if (releasePlan.SpecAPIVersion == specApiVersion)
                    {
                        releasePlans.Add(releasePlan);
                    }
                }

                return releasePlans;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get release plans for product id {productTreeId}", productTreeId);
                throw new Exception($"Failed to get release plans for product id {productTreeId}. Error: {ex.Message}");
            }
        }

        public async Task<List<ReleasePlanWorkItem>> GetReleasePlansForPackageAsync(string packageName, string language, bool isTestReleasePlan = false)
        {
            try
            {
                var languageId = MapLanguageToId(language);
                var escapedPackageName = packageName?.Replace("'", "''");
                var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT}'";
                query += $" AND [System.Tags] {(isTestReleasePlan ? "CONTAINS" : "NOT CONTAINS")} '{RELEASE_PLANNER_APP_TEST}'";
                query += $" AND [Custom.{languageId}PackageName] = '{escapedPackageName}'";
                query += " AND [System.WorkItemType] = 'Release Plan'";
                query += " AND [System.State] = 'In Progress'";
                var releasePlanWorkItems = await FetchWorkItemsAsync(query);
                if (releasePlanWorkItems.Count == 0)
                {
                    logger.LogInformation("No in-progress release plans found for package {packageName} in {language}", packageName, language);
                    return [];
                }

                var releasePlans = new List<ReleasePlanWorkItem>();
                foreach (var workItem in releasePlanWorkItems)
                {
                    var releasePlan = await MapWorkItemToReleasePlanAsync(workItem);
                    releasePlans.Add(releasePlan);
                }

                return releasePlans;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get release plans for package {packageName} in {language}", packageName, language);
                throw new Exception($"Failed to get release plans for package {packageName} in {language}. Error: {ex.Message}", ex);
            }
        }

        private async Task<ReleasePlanWorkItem> MapWorkItemToReleasePlanAsync(WorkItem workItem)
        {
            var releasePlan = new ReleasePlanWorkItem()
            {
                WorkItemId = workItem.Id ?? 0,
                WorkItemUrl = workItem.Url,
                WorkItemHtmlUrl = workItem.Url?.Replace("_apis/wit/workItems", "_workitems/edit") ?? string.Empty,
                Title = workItem.Fields.TryGetValue("System.Title", out object? value) ? value?.ToString() ?? string.Empty : string.Empty,
                Status = workItem.Fields.TryGetValue("System.State", out value) ? value?.ToString() ?? string.Empty : string.Empty,
                CreatedDate = workItem.Fields.TryGetValue("System.CreatedDate", out value) && value is DateTime createdDate ? createdDate : default,
                ChangedDate = workItem.Fields.TryGetValue("System.ChangedDate", out value) && value is DateTime changedDate ? changedDate : default,
                ServiceTreeId = workItem.Fields.TryGetValue("Custom.ServiceTreeID", out value) ? value?.ToString() ?? string.Empty : string.Empty,
                ProductTreeId = workItem.Fields.TryGetValue("Custom.ProductServiceTreeID", out value) ? value?.ToString() ?? string.Empty : string.Empty,
                SDKReleaseMonth = workItem.Fields.TryGetValue("Custom.SDKReleasemonth", out value) ? value?.ToString() ?? string.Empty : string.Empty,
                IsManagementPlane = workItem.Fields.TryGetValue("Custom.MgmtScope", out value) ? value?.ToString() == "Yes" : false,
                IsDataPlane = workItem.Fields.TryGetValue("Custom.DataScope", out value) ? value?.ToString() == "Yes" : false,
                ReleasePlanLink = workItem.Fields.TryGetValue("Custom.ReleasePlanLink", out value) ? value?.ToString() ?? string.Empty : string.Empty,
                ReleasePlanId = workItem.Fields.TryGetValue("Custom.ReleasePlanID", out value) ? int.Parse(value?.ToString() ?? "0") : 0,
                SDKReleaseType = workItem.Fields.TryGetValue("Custom.SDKtypetobereleased", out value) ? value?.ToString() ?? string.Empty : string.Empty,
                IsCreatedByAgent = workItem.Fields.TryGetValue("Custom.IsCreatedByAgent", out value) && "Copilot".Equals(value?.ToString()),
                ReleasePlanSubmittedByEmail = workItem.Fields.TryGetValue("Custom.ReleasePlanSubmittedby", out value) ? value?.ToString() ?? string.Empty : string.Empty,
                SDKLanguages = workItem.Fields.TryGetValue("Custom.SDKLanguages", out value) ? value?.ToString() ?? string.Empty : string.Empty,
                IsSpecApproved = workItem.Fields.TryGetValue("Custom.APISpecApprovalStatus", out value) && "Approved".Equals(value?.ToString()),
                LanguageExclusionRequesterNote = workItem.Fields.TryGetValue("Custom.ReleaseExclusionRequestNote", out value) ? value?.ToString() ?? string.Empty : string.Empty,
                LanguageExclusionApproverNote = workItem.Fields.TryGetValue("Custom.ReleaseExclusionApprovalNote", out value) ? value?.ToString() ?? string.Empty : string.Empty,
                APISpecProjectPath = workItem.Fields.TryGetValue("Custom.ApiSpecProjectPath", out value) ? value?.ToString() ?? string.Empty : string.Empty,
                Owner = workItem.Fields.TryGetValue("Custom.PrimaryPM", out value) ? value?.ToString() ?? string.Empty : string.Empty,
            };

            foreach (var lang in SUPPORTED_SDK_LANGUAGES)
            {
                var sdkGenPipelineUrl = workItem.Fields.TryGetValue($"Custom.SDKGenerationPipelineFor{lang}", out value) ? value?.ToString() ?? string.Empty : string.Empty;
                var sdkPullRequestUrl = workItem.Fields.TryGetValue($"Custom.SDKPullRequestFor{lang}", out value) ? value?.ToString() ?? string.Empty : string.Empty;
                var packageName = workItem.Fields.TryGetValue($"Custom.{lang}PackageName", out value) ? value?.ToString() ?? string.Empty : string.Empty;
                var generationStatus = workItem.Fields.TryGetValue($"Custom.GenerationStatusFor{lang}", out value) ? value?.ToString() ?? string.Empty : string.Empty;
                var releaseStatus = workItem.Fields.TryGetValue($"Custom.ReleaseStatusFor{lang}", out value) ? value?.ToString() ?? string.Empty : string.Empty;
                var pullRequestStatus = workItem.Fields.TryGetValue($"Custom.SDKPullRequestStatusFor{lang}", out value) ? value?.ToString() ?? string.Empty : string.Empty;
                var exclusionStatus = workItem.Fields.TryGetValue($"Custom.ReleaseExclusionStatusFor{lang}", out value) ? value?.ToString() ?? string.Empty : string.Empty;

                releasePlan.SDKInfo.Add(
                    new SDKInfo()
                    {
                        Language = MapLanguageIdToName(lang),
                        GenerationPipelineUrl = sdkGenPipelineUrl,
                        SdkPullRequestUrl = sdkPullRequestUrl,
                        GenerationStatus = generationStatus,
                        ReleaseStatus = releaseStatus,
                        PullRequestStatus = pullRequestStatus,
                        PackageName = packageName,
                        ReleaseExclusionStatus = exclusionStatus
                    }
                );
            }

            // Get details from API spec work item
            try
            {
                logger.LogInformation("Fetching API spec work item for release plan work item {workItemId}", releasePlan.WorkItemId);
                var apiSpecWorkItem = await GetApiSpecWorkItemAsync(releasePlan.WorkItemId);
                if (apiSpecWorkItem != null && apiSpecWorkItem.Fields != null)
                {
                    releasePlan.ActiveSpecPullRequest = apiSpecWorkItem.Fields.TryGetValue("Custom.ActiveSpecPullRequestUrl", out Object? specPr) ? specPr?.ToString() ?? string.Empty : string.Empty;
                    releasePlan.SpecAPIVersion = apiSpecWorkItem.Fields.TryGetValue("Custom.APISpecversion", out Object? apiVersion) ? apiVersion?.ToString() ?? string.Empty : string.Empty;
                    releasePlan.SpecType = apiSpecWorkItem.Fields.TryGetValue("Custom.APISpecDefinitionType", out Object? specType) ? specType?.ToString() ?? string.Empty : string.Empty;
                }
                else
                {
                    logger.LogWarning("API spec work item not found for release plan work item {workItemId}", releasePlan.WorkItemId);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get API spec work item for release plan work item {WorkItemId}", releasePlan.WorkItemId);
            }

            return releasePlan;
        }

        public async Task<ReleasePlanWorkItem> GetReleasePlanAsync(string pullRequestUrl)
        {
            // First find the API spec work item
            try
            {
                var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT}' AND [Custom.ActiveSpecPullRequestUrl] = '{pullRequestUrl}' AND [System.WorkItemType] = 'API Spec' AND [System.State] NOT IN ('Closed','Duplicate','Abandoned','Finished')";
                var apiSpecWorkItems = await FetchWorkItemsAsync(query);
                if (apiSpecWorkItems.Count == 0)
                {
                    logger.LogInformation("Release plan does not exist for the given pull request URL.");
                    return null;
                }

                foreach (var workItem in apiSpecWorkItems)
                {
                    if (workItem.Relations.Any())
                    {
                        var parent = workItem.Relations.FirstOrDefault(w => w.Rel.Equals("System.LinkTypes.Hierarchy-Reverse"));
                        if (parent == null)
                        {
                            continue;
                        }
                        // Get parent work item and make sure it is release plan work item
                        var parentWorkItemId = int.Parse(parent.Url.Split('/').Last());
                        var parentWorkItem = await connection.GetWorkItemClient().GetWorkItemAsync(parentWorkItemId);
                        if (parentWorkItem == null || !parentWorkItem.Fields.TryGetValue("System.WorkItemType", out Object? parentType))
                        {
                            continue;
                        }
                        if (parentType.Equals("Release Plan"))
                        {
                            // Check if parent work item is in abandoned state
                            if (parentWorkItem.Fields.TryGetValue("System.State", out Object? parentState))
                            {
                                var state = parentState?.ToString();
                                if (state != null && (state.Equals("Abandoned", StringComparison.OrdinalIgnoreCase) ||
                                                      state.Equals("Closed", StringComparison.OrdinalIgnoreCase) ||
                                                      state.Equals("Duplicate", StringComparison.OrdinalIgnoreCase)))
                                {
                                    logger.LogInformation("Skipping release plan work item {WorkItemId} in {State} state", parentWorkItemId, state);
                                    continue;
                                }
                            }
                            return await MapWorkItemToReleasePlanAsync(parentWorkItem);
                        }
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get release plan for pull request URL {PullRequestUrl}", pullRequestUrl);
                throw new Exception($"Failed to get release plan for pull request URL {pullRequestUrl}.", ex);
            }

        }

        public async Task<WorkItem> CreateReleasePlanWorkItemAsync(ReleasePlanWorkItem releasePlan)
        {
            int releasePlanWorkItemId = 0;
            int apiSpecWorkItemId = 0;
            var workItemClient = connection.GetWorkItemClient();
            try
            {
                // Create release plan work item
                var releasePlanTitle = $"Release plan for {releasePlan.ProductName ?? releasePlan.ProductTreeId}";
                logger.LogInformation("Creating release plan with title: {releasePlanTitle}", releasePlanTitle);
                var releasePlanWorkItem = await CreateWorkItemAsync(releasePlan, "Release Plan", releasePlanTitle);
                releasePlanWorkItemId = releasePlanWorkItem?.Id ?? 0;
                if (releasePlanWorkItemId == 0)
                {
                    throw new Exception("Failed to create release plan work item");
                }

                // Create API spec work item
                var apiSpecTitle = $"API spec for {releasePlan.ProductName ?? releasePlan.ProductTreeId} - version {releasePlan.SpecAPIVersion}";
                logger.LogInformation("Creating api spec with title: {apiSpecTitle}", apiSpecTitle);
                var apiSpecWorkItem = await CreateWorkItemAsync(releasePlan.ToApiSpecWorkItem(), "API Spec", apiSpecTitle, parentId: releasePlanWorkItemId);
                apiSpecWorkItemId = apiSpecWorkItem.Id ?? 0;
                if (apiSpecWorkItemId == 0)
                {
                    throw new Exception("Failed to create API spec work item");
                }

                // Update release plan status to in progress
                releasePlanWorkItem = await UpdateWorkItemAsync(releasePlanWorkItemId, new Dictionary<string, string>
                {
                    { "System.State", "In Progress" }
                });

                if (releasePlanWorkItem != null)
                {
                    return releasePlanWorkItem;
                }

                throw new Exception("Failed to create API spec work item");
            }
            catch (Exception ex)
            {
                const string errorMessage = "Failed to create release plan and API spec work items";
                logger.LogError(ex, errorMessage);
                // Delete created work items if both release plan and API spec work items were not created and linked
                if (releasePlanWorkItemId != 0)
                {
                    await workItemClient.DeleteWorkItemAsync(releasePlanWorkItemId);
                }
                if (apiSpecWorkItemId != 0)
                {
                    await workItemClient.DeleteWorkItemAsync(apiSpecWorkItemId);
                }
                throw new Exception(errorMessage, ex);
            }
        }

        private async Task<WorkItem> CreateWorkItemAsync(WorkItemBase workItem, string workItemType, string title, int? parentId = null, int? relatedId = null)
        {
            workItem.Title = title;
            var workItemsFieldJson = JsonSerializer.Serialize(workItem);
            logger.LogDebug("Input work item json: {releasePlanJson}", workItemsFieldJson);
            var specDocument = workItem.GetPatchDocument();

            logger.LogInformation("Creating {workItemType} work item", workItemType);
            logger.LogDebug("Sending work item request to DevOps: {@specDocument}", specDocument);
            var createdWorkItem = await connection.GetWorkItemClient().CreateWorkItemAsync(specDocument, Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT, workItemType);
            if (createdWorkItem == null)
            {
                throw new Exception("Failed to create Work Item");
            }

            if (createdWorkItem.Id != null)
            {
                if (parentId != null)
                {
                    // Create parent-child relation
                    await CreateWorkItemRelationAsync(createdWorkItem.Id.Value, "parent", targetId: parentId);
                }

                if (relatedId != null)
                { 
                    await CreateWorkItemRelationAsync(createdWorkItem.Id.Value, "related", targetId: relatedId);
                }
            }

            return createdWorkItem;
        }

        private async Task<WorkItem> CreateWorkItemRelationAsync(int id, string relationType, int? targetId = null, string? targetUrl = null)
        {
            // Create generic work item relation(s) based on target ID and/or URL
            if (targetId == null && string.IsNullOrWhiteSpace(targetUrl))
            {
                throw new Exception("To create work item relation, either Target ID or Target URL must be provided.");
            }

            var workItemClient = connection.GetWorkItemClient();

            // Resolve relation type system name/reference
            // ex: Child, Parent, Related, etc map to the appropriate name.
            var relationTypeSystemName = await ResolveRelationTypeSystemName(relationType);

            var patchDocument = new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument();

            // Handle target ID
            if (targetId != null)
            {
                var targetWorkItem = await connection.GetWorkItemClient().GetWorkItemAsync(targetId.Value);

                // Ensure the target work item exists before creating the relation
                if (targetWorkItem == null)
                {
                    throw new Exception($"Work item with ID {targetId} does not exist.");
                }

                // targetWorkItem contains only Id + Url; URL is enough to create relation
                patchDocument.Add(new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/relations/-",
                    Value = new WorkItemRelation
                    {
                        Rel = relationTypeSystemName,
                        Url = targetWorkItem.Url
                    }
                });
            }
            // Handle target URLs (comma-separated)
            else if (!string.IsNullOrWhiteSpace(targetUrl))
            {
                patchDocument.Add(new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/relations/-",
                    Value = new WorkItemRelation
                    {
                        Rel = relationTypeSystemName,
                        Url = targetUrl
                    }
                });
            }

            return await workItemClient.UpdateWorkItemAsync(patchDocument, id);
        }

        private async Task RemoveWorkItemRelationAsync(int id, string relationType, int targetId)
        {
            var workItemClient = connection.GetWorkItemClient();

            var relationTypeSystemName = await ResolveRelationTypeSystemName(relationType);

            var workItem = await workItemClient.GetWorkItemAsync(id, expand: WorkItemExpand.Relations);
            var targetWorkItem = await workItemClient.GetWorkItemAsync(targetId);

            if (workItem == null)
            {
                throw new Exception($"Work item {id} not found.");
            }
            if (targetWorkItem == null)
            {
                throw new Exception($"Target work item {targetId} not found.");
            }
            if (workItem.Relations == null || !workItem.Relations.Any())
            {
                throw new Exception($"Work item {id} has no relations.");
            }

            var targetRelation = workItem.Relations.FirstOrDefault(r =>
                r.Rel.Equals(relationTypeSystemName, StringComparison.OrdinalIgnoreCase) &&
                r.Url.Equals(targetWorkItem.Url, StringComparison.OrdinalIgnoreCase)
            );

            if (targetRelation == null)
            {
                throw new Exception($"Relation of type '{relationType}' to target work item ID {targetId} not found in work item {id}.");
            }

            var patchDocument = new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument();
            var relationIndex = workItem.Relations.IndexOf(targetRelation);
            patchDocument.Add(new JsonPatchOperation
            {
                Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Remove,
                Path = $"/relations/{relationIndex}"
            });
            await workItemClient.UpdateWorkItemAsync(patchDocument, id);
        }

        private async Task<string> ResolveRelationTypeSystemName(string relationType)
        {
            var relationTypes = await GetCachedRelationTypes();

            // Match service-provided relation type by display name (case-insensitive)
            var match = relationTypes.FirstOrDefault(rt => string.Equals(rt.Name, relationType, StringComparison.OrdinalIgnoreCase));
            if (match != null && match.ReferenceName != null)
            {
                return match.ReferenceName;
            }

            throw new Exception($"Relation Type '{relationType}' is not valid.");
        }

        public static string MapLanguageToId(string language)
        {
            var lang = language.ToLower();
            return lang switch
            {
                ".net" => "Dotnet",
                "csharp" => "Dotnet",
                "js" => "JavaScript",
                "javascript" => "JavaScript",
                "python" => "Python",
                "java" => "Java",
                "go" => "Go",
                _ => language
            };
        }

        public static string MapLanguageIdToName(string language)
        {
            var lang = language.ToLower();
            return lang switch
            {
                "dotnet" => ".NET",
                "csharp" => ".NET",
                ".net" => ".NET",
                "typescript" => "JavaScript",
                "python" => "Python",
                "javascript" => "JavaScript",
                "java" => "Java",
                "go" => "Go",
                _ => language
            };
        }

        public async Task<bool> AddSdkInfoInReleasePlanAsync(int workItemId, string language, string sdkGenerationPipelineUrl, string sdkPullRequestUrl, string generationStatus = "")
        {
            // Adds SDK generation and pull request link in release plan work item.
            try
            {
                if (string.IsNullOrEmpty(language) || workItemId == 0 || (string.IsNullOrEmpty(sdkGenerationPipelineUrl) && string.IsNullOrEmpty(sdkPullRequestUrl)))
                {
                    logger.LogError("Please provide the language, work item ID, and either the SDK generation pipeline URL or the SDK pull request URL to add SDK info to a work item.");
                    return false;
                }

                var workItem = await connection.GetWorkItemClient().GetWorkItemAsync(workItemId);
                if (workItem == null)
                {
                    throw new Exception($"Work item {workItemId} not found.");
                }

                var jsonLinkDocument = new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument();
                // Add work item as child of release plan work item
                if (!string.IsNullOrEmpty(sdkGenerationPipelineUrl))
                {
                    jsonLinkDocument.Add(
                        new JsonPatchOperation
                        {
                            Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                            Path = $"/fields/Custom.SDKGenerationPipelineFor{MapLanguageToId(language)}",
                            Value = sdkGenerationPipelineUrl
                        });
                }
                if (!string.IsNullOrEmpty(generationStatus))
                {
                    jsonLinkDocument.Add(
                        new JsonPatchOperation
                        {
                            Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                            Path = $"/fields/Custom.GenerationStatusFor{MapLanguageToId(language)}",
                            Value = generationStatus
                        });
                }
                if (!string.IsNullOrEmpty(sdkPullRequestUrl))
                {
                    jsonLinkDocument.Add(
                        new JsonPatchOperation
                        {
                            Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                            Path = $"/fields/Custom.SDKPullRequestFor{MapLanguageToId(language)}",
                            Value = sdkPullRequestUrl
                        });
                }
                int maxTryCount = 5, retryCount = 0;

                while (retryCount < maxTryCount)
                {
                    try
                    {
                        // DevOps SDK internally caches the revision number of the work item and throws conflict error if it is outdated.
                        // Work around is to fetch the work item again before updating it.
                        await connection.GetWorkItemClient().GetWorkItemAsync(workItemId);
                        await connection.GetWorkItemClient().UpdateWorkItemAsync(jsonLinkDocument, workItemId);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        // Retry once if there is a conflict error
                        logger.LogWarning("Conflict error while updating work item {workItemId}, retrying update work item again.", workItemId);
                        retryCount++;
                        if (retryCount == maxTryCount)
                        {
                            throw new Exception($"Failed to update DevOps work item after multiple retries. Error: {ex.Message}");
                        }
                        await Task.Delay(1000 * retryCount); // Exponential backoff
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to update SDK generation details to work item [{workItemId}]. Error: {ex.Message}");
            }
        }
        private async Task<List<WorkItem>> FetchWorkItemsAsync(string query)
        {
            try
            {
                var workItemClient = connection.GetWorkItemClient();
                var result = await workItemClient.QueryByWiqlAsync(new Wiql { Query = query });
                logger.LogInformation("Work item query result: {result}", result);
                if (result != null && result.WorkItems != null && result.WorkItems.Any())
                {
                    var ids = result.WorkItems.Select(wi => wi.Id).ToList();
                    logger.LogInformation("Fetching work item details: {workItemIds}", string.Join(',', ids));
                    return await workItemClient.GetWorkItemsAsync(ids, expand: WorkItemExpand.All);
                }
                else
                {
                    logger.LogWarning("No work items found.");
                    return [];
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get work item. Error: {ex.Message}", ex);
            }
        }

        public async Task<List<WorkItem>> FetchWorkItemsPagedAsync(string query, int top = 100000, int batchSize = 200, WorkItemExpand expand = WorkItemExpand.All)
        {
            try
            {
                var workItemClient = connection.GetWorkItemClient();
                var result = await workItemClient.QueryByWiqlAsync(new Wiql { Query = query }, top: top);
                logger.LogInformation("Work item query result: {result}", result);
                if (result != null && result.WorkItems != null && result.WorkItems.Any())
                {
                    var ids = result.WorkItems.Select(wi => wi.Id).ToList();
                    logger.LogInformation("Work item query returned {workItemIdCount} ids", ids.Count);
                    logger.LogInformation("Fetching work item details: {workItemIds}", string.Join(',', ids));

                    var workItems = new List<WorkItem>();
                    for (int i = 0; i < ids.Count; i += batchSize)
                    {
                        var batchIds = ids.Skip(i).Take(batchSize).ToList();
                        var batch = await workItemClient.GetWorkItemsAsync(batchIds, expand: expand);
                        workItems.AddRange(batch);
                    }
                    return workItems;
                }
                else
                {
                    logger.LogWarning("No work items found.");
                    return [];
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get work item. Error: {ex.Message}", ex);
            }
        }

        private static int GetPipelineDefinitionId(string language)
        {
            language = MapLanguageToId(language);
            return language.ToLower() switch
            {
                "python" => 7423,
                "javascript" => 7422,
                "go" => 7426,
                "java" => 7421,
                "dotnet" => 7412,
                _ => 0,
            };
        }

        public static bool IsSDKGenerationSupported(string language)
        {
            language = MapLanguageToId(language);
            return language.ToLower() switch
            {
                "python" => true,
                "dotnet" => true,
                "javascript" => true,
                "java" => true,
                "go" => true,
                _ => false,
            };
        }

        public async Task<Build> RunSDKGenerationPipelineAsync(string apiSpecBranchRef, string typespecProjectRoot, string apiVersion, string sdkReleaseType, string language, int workItemId, string sdkRepoBranch = "")
        {
            int pipelineDefinitionId = GetPipelineDefinitionId(language);
            if (pipelineDefinitionId == 0)
            {
                throw new Exception($"Failed to get SDK generation pipeline for {language}.");
            }

            var templateParams = new Dictionary<string, string>
            {
                 { "ConfigType", "TypeSpec"},
                 { "ConfigPath", $"{typespecProjectRoot}/tspconfig.yaml" },
                 { "ApiVersion", apiVersion },
                 { "SdkReleaseType", sdkReleaseType },
                 { "CreatePullRequest", "true" },
                 { "ReleasePlanWorkItemId", $"{workItemId}"}
            };

            if (!string.IsNullOrEmpty(sdkRepoBranch))
            {
                templateParams["SdkRepoBranch"] = sdkRepoBranch;
            }

            var build = await RunPipelineAsync(pipelineDefinitionId, templateParams, apiSpecBranchRef);
            var pipelineRunUrl = GetPipelineUrl(build.Id);
            logger.LogInformation("Started pipeline run {pipelineRunUrl} to generate SDK.", pipelineRunUrl);
            if (workItemId != 0)
            {
                logger.LogInformation("Adding SDK generation pipeline link to release plan");
                await AddSdkInfoInReleasePlanAsync(workItemId, MapLanguageToId(language), pipelineRunUrl, "", "In progress");
            }

            return build;
        }

        public async Task<Build> RunPipelineAsync(int pipelineDefinitionId, Dictionary<string, string> templateParams, string apiSpecBranchRef = "main")
        {
            if (pipelineDefinitionId == 0)
            {
                throw new ArgumentException($"Invalid pipeline definition ID.");
            }

            var buildClient = connection.GetBuildClient();
            var projectClient = connection.GetProjectClient();
            var definition = await buildClient.GetDefinitionAsync(Constants.AZURE_SDK_DEVOPS_INTERNAL_PROJECT, pipelineDefinitionId);
            var project = await projectClient.GetProject(Constants.AZURE_SDK_DEVOPS_INTERNAL_PROJECT);

            // Queue SDK generation pipeline
            logger.LogInformation("Queueing pipeline {pipelineName}.", definition.Name);
            var build = await buildClient.QueueBuildAsync(new Build()
            {
                Definition = definition,
                Project = project,
                SourceBranch = apiSpecBranchRef,
                TemplateParameters = templateParams
            });
            return build;
        }


        public async Task<Build> GetPipelineRunAsync(int buildId)
        {
            var buildClient = connection.GetBuildClient();
            return await buildClient.GetBuildAsync(Constants.AZURE_SDK_DEVOPS_INTERNAL_PROJECT, buildId);
        }

        public async Task<string> GetSDKPullRequestFromPipelineRunAsync(int buildId, string language, int workItemId)
        {
            var buildClient = connection.GetBuildClient();
            var timeLine = await buildClient.GetBuildTimelineAsync(Constants.AZURE_SDK_DEVOPS_INTERNAL_PROJECT, buildId);
            var createPrJob = timeLine.Records.FirstOrDefault(r => r.Name == "Create pull request") ?? null;
            if (createPrJob == null)
            {
                return $"Failed to generate SDK. SDK pull request link is not available for pipeline run, Pipeline link {timeLine.Url}";
            }

            // Get SDK pull request from create pull request job attachment
            if (createPrJob.Result == TaskResult.Succeeded)
            {
                var contentStream = await buildClient.GetAttachmentAsync(Constants.AZURE_SDK_DEVOPS_INTERNAL_PROJECT, buildId, timeLine.Id, createPrJob.Id, "Distributedtask.Core.Summary", "Pull Request Created");
                if (contentStream != null)
                {
                    var content = new StreamReader(contentStream);
                    var pullRequestUrl = ParseSDKPullRequestUrl(content.ReadToEnd());
                    if (workItemId != 0)
                    {
                        logger.LogInformation("Adding SDK pull request to release plan");
                        await AddSdkInfoInReleasePlanAsync(workItemId, MapLanguageToId(language), GetPipelineUrl(buildId), pullRequestUrl.FullUrl, "Completed");
                    }
                    return pullRequestUrl.FullUrl;
                }
            }

            // Check if there is any warning related to Generate SDK or create pr jobs. Ignore all 1ES jobs to avoid showing irrelevant warning.
            StringBuilder sb = new($"Failed to generate SDK pull request for {language}.");
            foreach (var job in timeLine.Records.Where(t => !t.Name.Contains("1ES")))
            {
                if (job.Issues.Count > 0)
                {
                    job.Issues.ForEach(issue => sb.AppendLine(issue.Message));
                }
            }
            return sb.ToString();
        }

        public static string GetPipelineUrl(int buildId)
        {
            return $"{Constants.AZURE_SDK_DEVOPS_BASE_URL}/internal/_build/results?buildId={buildId}";
        }

        public static ParsedSdkPullRequest ParseSDKPullRequestUrl(string sdkGenerationSummary)
        {
            Regex regex = new Regex("https:\\/\\/github.com\\/([Aa]zure)\\/(azure-sdk-for-[a-z]+)\\/pull\\/([0-9]+)");
            var match = regex.Match(sdkGenerationSummary);
            if (match.Success)
            {
                return new ParsedSdkPullRequest
                {
                    RepoOwner = match.Groups[1].Value,
                    RepoName = match.Groups[2].Value,
                    PrNumber = int.Parse(match.Groups[3].Value),
                    FullUrl = match.Value
                };
            }
            return new ParsedSdkPullRequest();
        }

        /// <summary>
        /// Add the list of SDK languages to release plan. Release plan uses this language list to track SDK release.
        /// </summary>
        /// <param name="workItemId"></param>
        /// <param name="sdkLanguages"></param>
        /// <returns>bool</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="Exception"></exception>
        public async Task<bool> UpdateReleasePlanSDKDetailsAsync(int workItemId, List<SDKInfo> sdkLanguages)
        {
            // Adds SDK languages in release plan work item.
            try
            {
                if (workItemId == 0 || sdkLanguages == null || sdkLanguages.Count == 0)
                {
                    throw new ArgumentException("Please provide the work item ID and a list of languages to add SDK info to a work item.");
                }

                HashSet<string> languageNames = [.. sdkLanguages.Select(s => s.Language)];
                var releasePlan = await GetReleasePlanForWorkItemAsync(workItemId);
                if (releasePlan?.SDKInfo != null)
                {
                    languageNames.UnionWith(releasePlan.SDKInfo.Select(s => s.Language));
                }

                var languages = string.Join(",", languageNames);
                logger.LogInformation("Selected languages to generate SDK: {Languages}", languages);
                var jsonLinkDocument = new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument
                {
                    new JsonPatchOperation
                    {
                        Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                        Path = "/fields/Custom.SDKLanguages",
                        Value = languages
                    }
                };

                foreach (var sdk in sdkLanguages)
                {
                    // Add package name in release plan for each language
                    if (!string.IsNullOrEmpty(sdk.Language) && !string.IsNullOrEmpty(sdk.PackageName))
                    {
                        jsonLinkDocument.Add(
                            new JsonPatchOperation
                            {
                                Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                                Path = $"/fields/Custom.{MapLanguageToId(sdk.Language)}PackageName",
                                Value = sdk.PackageName
                            }
                        );
                    }
                }
                await connection.GetWorkItemClient().UpdateWorkItemAsync(jsonLinkDocument, workItemId);
                logger.LogInformation("Updated SDK languages to work item {WorkItemId}.", workItemId);
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to update SDK languages to work item [{workItemId}]. Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Update API specification pull request status in the release plan. Release planner uses this status to mark
        /// API readiness as completed when API specification pull request is approved.
        /// </summary>
        /// <param name="workItemId"></param>
        /// <param name="status"></param>
        /// <returns>bool</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="Exception"></exception>
        public async Task<bool> UpdateApiSpecStatusAsync(int workItemId, string status)
        {
            // Update API spec work item status in release plan
            try
            {
                var workItem = await connection.GetWorkItemClient().GetWorkItemAsync(workItemId);
                if (workItem == null)
                {
                    throw new ArgumentException($"release plan work item with id {workItemId} not found.");
                }

                if (string.IsNullOrEmpty(status))
                {
                    throw new ArgumentException("Please provide a status to update the work item.");
                }
                var jsonLinkDocument = new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument()
                {
                    new JsonPatchOperation
                    {
                        Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                        Path = "/fields/Custom.APISpecApprovalStatus",
                        Value = status
                    }
                };
                await connection.GetWorkItemClient().UpdateWorkItemAsync(jsonLinkDocument, workItemId);
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to update API spec status to work item [{workItemId}]. Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get API spec work item for a given release plan work item.
        /// </summary>
        private async Task<WorkItem> GetApiSpecWorkItemAsync(int releasePlanWorkItemId)
        {
            var releasePlanWorkItem = await connection.GetWorkItemClient().GetWorkItemAsync(releasePlanWorkItemId, expand: WorkItemExpand.All);
            if (releasePlanWorkItem?.Id == null)
            {
                throw new InvalidOperationException($"Work item {releasePlanWorkItemId} not found.");
            }

            if (releasePlanWorkItem.Relations == null || !releasePlanWorkItem.Relations.Any(r => r.Rel.Equals("System.LinkTypes.Hierarchy-Forward")))
            {
                throw new InvalidOperationException("Release plan work item does not have any child work item");
            }

            //Find API spec work item
            foreach (var relation in releasePlanWorkItem.Relations.Where(r => r.Rel.Equals("System.LinkTypes.Hierarchy-Forward")))
            {
                // Get parent work item and make sure it is release plan work item
                var childWorkItemId = int.Parse(relation.Url.Split('/').Last());
                var childWorkItem = await connection.GetWorkItemClient().GetWorkItemAsync(childWorkItemId);
                if (childWorkItem == null || !childWorkItem.Fields.TryGetValue("System.WorkItemType", out Object? workItemType))
                {
                    continue;
                }
                if (workItemType.Equals("API Spec"))
                {
                    return childWorkItem;
                }
            }
            throw new InvalidOperationException($"API spec work item not found for release plan work item {releasePlanWorkItemId}.");
        }

        /// <summary>
        /// Update the active spec pull request link in API spec work item.
        /// </summary>
        /// <param name="releasePlanWorkItemId"></param>
        /// <param name="specPullRequest"></param>
        /// <returns>bool</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="Exception"></exception>
        public async Task<bool> UpdateSpecPullRequestAsync(int releasePlanWorkItemId, string specPullRequest)
        {
            // Update Active spec PR and add link to spec pr list
            try
            {
                if (releasePlanWorkItemId == 0 || string.IsNullOrEmpty(specPullRequest))
                {
                    throw new ArgumentException("Please provide the work item ID and a spec pull request URL to update the work item.");
                }

                // Find API spec work item
                var apiSpecWorkItem = await GetApiSpecWorkItemAsync(releasePlanWorkItemId);
                int apiSpecWorkItemId = apiSpecWorkItem.Id ?? 0;
                if (apiSpecWorkItemId == 0)
                {
                    throw new Exception($"API spec work item not found for release plan work item {releasePlanWorkItemId}.");
                }

                // Get current REST API review links and append new spec pull request link
                var currentLinks = apiSpecWorkItem.Fields.TryGetValue("Custom.RESTAPIReviews", out Object? value) ? value?.ToString() ?? string.Empty : string.Empty;
                StringBuilder sb = new StringBuilder(currentLinks);
                if (sb.Length > 0)
                {
                    sb.Append("<br>");
                }
                sb.Append($"<a href=\"{specPullRequest}\">{specPullRequest}</a>");

                // Create DevOps patch document
                var jsonLinkDocument = new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument()
                {
                    new JsonPatchOperation
                    {
                        Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                        Path = "/fields/Custom.ActiveSpecPullRequestUrl",
                        Value = specPullRequest
                    },
                    new JsonPatchOperation
                    {
                        Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                        Path = "/fields/Custom.RESTAPIReviews",
                        Value = sb.ToString()
                    }
                };
                await connection.GetWorkItemClient().UpdateWorkItemAsync(jsonLinkDocument, apiSpecWorkItemId);

                // Reset SDK generation status for all languages to "In progress" in the release plan work item
                var releasePlanUpdateDocument = new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument();
                foreach (var lang in SUPPORTED_SDK_LANGUAGES)
                {
                    releasePlanUpdateDocument.Add(
                        new JsonPatchOperation
                        {
                            Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                            Path = $"/fields/Custom.GenerationStatusFor{lang}",
                            Value = "In progress"
                        }
                    );
                }
                await connection.GetWorkItemClient().UpdateWorkItemAsync(releasePlanUpdateDocument, releasePlanWorkItemId);

                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to update spec pull request to release plan [{releasePlanWorkItemId}]. Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Link namespace approval issue to release plan work item.
        /// </summary>
        /// <param name="releasePlanWorkItemId"></param>
        /// <param name="url"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="Exception"></exception>
        public async Task<bool> LinkNamespaceApprovalIssueAsync(int releasePlanWorkItemId, string url)
        {
            // Link namespace approval issue to release plan work item
            try
            {
                if (string.IsNullOrEmpty(url))
                {
                    throw new ArgumentException("Please provide the URL of the namespace approval issue to link.");
                }
                var jsonLinkDocument = new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument()
                {
                    new JsonPatchOperation
                    {
                        Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                        Path = "/fields/Custom.NamespaceApprovalIssue",
                        Value = url
                    }
                };
                await connection.GetWorkItemClient().UpdateWorkItemAsync(jsonLinkDocument, releasePlanWorkItemId);
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to link namespace approval issue. Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get package work item for a given package name and language.
        /// If package version is given, then it will find the package work item for that version.
        /// If package version is empty, then it will find the latest package work item for that package name and language.
        /// </summary>
        public async Task<PackageWorkitemResponse> GetPackageWorkItemAsync(string packageName, string language, string packageVersion = "")
        {
            language = MapLanguageIdToName(language);
            if (packageName.Contains(' ') || packageName.Contains('\'') || packageName.Contains('"') || language.Contains(' ') || language.Contains('\'') || language.Contains('"') || packageVersion.Contains(' ') || packageVersion.Contains('\'') || packageVersion.Contains('"'))
            {
                throw new ArgumentException("Invalid data in one of the parameters.");
            }

            var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT}' AND [Custom.Package] = '{packageName}' AND [Custom.Language] = '{language}' AND [System.WorkItemType] = 'Package' AND [System.State] NOT IN ('Closed','Duplicate','Abandoned') AND [System.Tags] NOT CONTAINS '{RELEASE_PLANNER_APP_TEST}'";
            if (!string.IsNullOrEmpty(packageVersion))
            {
                query += $" AND [Custom.PackageVersion] = '{packageVersion}'";
            }
            query += "  ORDER BY [System.Id] DESC"; // Order by package work item to find the most recently created
            logger.LogInformation("Fetching package work item with package name {packageName}, package version {packageVersion} and language {language}.", packageName, packageVersion, language);

            var packageWorkItems = await FetchWorkItemsAsync(query);
            if (packageWorkItems.Count == 0)
            {
                return null;
            }
            return MapPackageWorkItemToModel(packageWorkItems[0]); // Return the first package work item
        }

        // /// <summary>
        // /// List package work items for a language that at least partially matches the given package name.
        // /// </summary>
        public async Task<List<PackageWorkitemResponse>> ListPartialPackageWorkItemAsync(string packageName, string language)
        {
            language = MapLanguageIdToName(language);
            if (packageName.Contains(' ') || packageName.Contains('\'') || packageName.Contains('"') || language.Contains(' ') || language.Contains('\'') || language.Contains('"'))
            {
                throw new ArgumentException("Invalid data in one of the parameters.");
            }

            var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT}' AND [Custom.Package] CONTAINS '{packageName}' AND [Custom.Language] = '{language}' AND [System.WorkItemType] = 'Package' AND [System.State] NOT IN ('Closed','Duplicate','Abandoned') AND [System.Tags] NOT CONTAINS '{RELEASE_PLANNER_APP_TEST}'";
            query += "  ORDER BY [System.Id] DESC"; // Order by package work item to find the most recently created

            logger.LogInformation("Fetching package work item with package name {packageName} and language {language}.", packageName, language);
            var packageWorkItems = await FetchWorkItemsAsync(query);
            return packageWorkItems.Select(workItem => MapPackageWorkItemToModel(workItem)).ToList();
        }

        public static PackageWorkitemResponse MapPackageWorkItemToModel(WorkItem workItem)
        {
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem), "Work item cannot be null.");
            }
            PackageWorkitemResponse packageModel = new()
            {
                PackageName = GetWorkItemValue(workItem, "Custom.Package"),
                Version = GetWorkItemValue(workItem, "Custom.PackageVersion"),
                WorkItemId = workItem.Id ?? 0,
                WorkItemUrl = workItem.Url,
                State = GetWorkItemValue(workItem, "System.State"),
                DisplayName = GetWorkItemValue(workItem, "Custom.PackageDisplayName"),
                PackageRepoPath = GetWorkItemValue(workItem, "Custom.PackageRepoPath"),
                changeLogStatus = GetWorkItemValue(workItem, "Custom.ChangeLogStatus"),
                ChangeLogValidationDetails = GetWorkItemValue(workItem, "Custom.ChangeLogValidationDetails"),
                APIViewStatus = GetWorkItemValue(workItem, "Custom.APIReviewStatus"),
                ApiViewValidationDetails = GetWorkItemValue(workItem, "Custom.APIReviewStatusDetails"),
                PackageNameStatus = GetWorkItemValue(workItem, "Custom.PackageNameApprovalStatus"),
                PackageNameApprovalDetails = GetWorkItemValue(workItem, "Custom.PackageNameApprovalDetails"),
                PipelineDefinitionUrl = GetWorkItemValue(workItem, "Custom.PipelineDefinition"),
                LatestPipelineRun = GetWorkItemValue(workItem, "Custom.LatestPipelineRun")
            };
            packageModel.SetLanguage(GetWorkItemValue(workItem, "Custom.Language"));
            packageModel.SetPackageType(GetWorkItemValue(workItem, "Custom.PackageType"));
            var plannedPackages = GetWorkItemValue(workItem, "Custom.PlannedPackages");
            packageModel.PlannedReleases = ParseHtmlPackageData(plannedPackages);
            var releasedPackages = GetWorkItemValue(workItem, "Custom.ShippedPackages");
            packageModel.ReleasedVersions = ParseHtmlPackageData(releasedPackages);
            return packageModel;
        }
        private static string GetWorkItemValue(WorkItem workItem, string fieldName)
        {
            if (workItem.Fields.TryGetValue(fieldName, out object? value))
            {
                return value?.ToString() ?? string.Empty;
            }
            return string.Empty;
        }

        private static List<SDKReleaseInfo> ParseHtmlPackageData(string packageData)
        {
            List<SDKReleaseInfo> sdkReleaseInfo = [];
            var matches = SdkReleaseDetailsRegex().Matches(packageData);
            foreach (Match m in matches)
            {
                sdkReleaseInfo.Add(new SDKReleaseInfo
                {
                    ReleaseType = m.Groups[1].Value.Trim(),
                    Version = m.Groups[2].Value.Trim(),
                    ReleaseDate = m.Groups[3].Value.Trim()
                });
            }
            return sdkReleaseInfo;
        }

        private async Task<Dictionary<string, List<string>>> GetLlmArtifactsAuthenticated(string project, int buildId)
        {
            var buildClient = connection.GetBuildClient();
            var result = new Dictionary<string, List<string>>();
            var artifacts = await buildClient.GetArtifactsAsync(project, buildId, cancellationToken: default);
            foreach (var artifact in artifacts)
            {
                if (artifact.Name.StartsWith("LLM Artifacts", StringComparison.OrdinalIgnoreCase))
                {
                    var tempDir = Path.Combine(Path.GetTempPath(), $"{artifact.Name}_{Guid.NewGuid()}");
                    Directory.CreateDirectory(tempDir);

                    logger.LogDebug("Downloading artifact '{artifactName}' to '{tempDir}'", artifact.Name, tempDir);

                    using var stream = await buildClient.GetArtifactContentZipAsync(project, buildId, artifact.Name);
                    var zipPath = Path.Combine(tempDir, "artifact.zip");
                    using (var fileStream = File.Create(zipPath))
                    {
                        await stream.CopyToAsync(fileStream);
                    }

                    await Task.Factory.StartNew(() =>
                    {
                        System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tempDir);
                        File.Delete(zipPath);
                    });

                    var files = Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories).ToList();
                    result[artifact.Name] = files;
                }
            }
            return result;
        }

        private async Task<Dictionary<string, List<string>>> GetLlmArtifactsUnauthenticated(string project, int buildId)
        {
            var result = new Dictionary<string, List<string>>();
            using var httpClient = new HttpClient();
            var artifactsUrl = $"{Constants.AZURE_SDK_DEVOPS_BASE_URL}/{project}/_apis/build/builds/{buildId}/artifacts?api-version=7.1-preview.5";
            var artifactsResponse = await httpClient.GetAsync(artifactsUrl);
            // Devops will return a sign-in html page if the user is not authorized
            if (artifactsResponse.StatusCode == System.Net.HttpStatusCode.NonAuthoritativeInformation)
            {
                throw new Exception($"Not authorized to get artifacts from {artifactsUrl}");
            }
            artifactsResponse.EnsureSuccessStatusCode();
            var artifactsJson = await artifactsResponse.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(artifactsJson);
            var artifacts = doc.RootElement.GetProperty("value").EnumerateArray();

            var seenFiles = new HashSet<string>();
            var tempDir = Path.Combine(Path.GetTempPath(), buildId.ToString());
            if (Directory.Exists(tempDir))
            {
                await Task.Factory.StartNew(() =>
                {
                    Directory.Delete(tempDir, true);
                });
            }
            Directory.CreateDirectory(tempDir);

            List<JsonElement> mostRecentArtifacts = [];
            var mostRecentJobAttempt = 1;
            // Given an artifact name like "LLM Artifacts - Ubuntu2404_NET80_PackageRef_Debug - 1"
            // where '1' == the job attempt number
            // only find artifacts from the most recent job attempt.
            foreach (var artifact in artifacts)
            {
                var name = artifact.GetProperty("name").GetString();
                var jobAttempt = name?.Split('-').LastOrDefault()?.Trim();
                var jobAttemptNumber = int.TryParse(jobAttempt, out var attempt) ? attempt : 0;
                if (jobAttemptNumber == mostRecentJobAttempt)
                {
                    mostRecentArtifacts.Add(artifact);
                }
                else if (jobAttemptNumber > mostRecentJobAttempt)
                {
                    mostRecentArtifacts.Clear();
                    mostRecentArtifacts.Add(artifact);
                }
            }

            foreach (var artifact in mostRecentArtifacts)
            {
                var name = artifact.GetProperty("name").GetString();
                if (name == null || name.StartsWith("LLM Artifacts", StringComparison.OrdinalIgnoreCase) == false)
                {
                    continue;
                }

                var downloadUrl = artifact.GetProperty("resource").GetProperty("downloadUrl").GetString();
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    continue;
                }

                logger.LogDebug("Downloading artifact '{artifactName}' to '{tempDir}'", name, tempDir);

                var zipPath = Path.Combine(tempDir, "artifact.zip");

                using (var zipStream = await httpClient.GetStreamAsync(downloadUrl))
                using (var fileStream = File.Create(zipPath))
                {
                    await zipStream.CopyToAsync(fileStream);
                }

                await Task.Factory.StartNew(() =>
                {
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tempDir);
                    File.Delete(zipPath);
                });

                var files = Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories).ToList();
                var newFiles = files.Where(f => !seenFiles.Contains(f)).ToList();
                seenFiles.UnionWith(newFiles);

                // Given an artifact name like "LLM Artifacts - Ubuntu2404_NET80_PackageRef_Debug - 1"
                // create a key/platform name like "Ubuntu2404_NET80_PackageRef_Debug"
                var parts = name.Split(" - ", StringSplitOptions.RemoveEmptyEntries);
                var testPlatform = string.Join(" - ", parts[1..^1]);
                result[testPlatform] = newFiles;
            }

            return result;
        }

        public async Task<Dictionary<string, List<string>>> GetPipelineLlmArtifacts(string project, int buildId)
        {
            if (project == Constants.AZURE_SDK_DEVOPS_PUBLIC_PROJECT)
            {
                return await GetLlmArtifactsUnauthenticated(project, buildId);
            }
            return await GetLlmArtifactsAuthenticated(project, buildId);
        }

        public async Task<WorkItem> UpdateWorkItemAsync(int workItemId, Dictionary<string, string> fields)
        {
            var jsonLinkDocument = new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument();
            foreach (var item in fields)
            {
                logger.LogDebug("Updating field {field} to {value}", item.Key, item.Value);
                jsonLinkDocument.Add(
                    new JsonPatchOperation
                    {
                        Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                        Path = $"/fields/{item.Key}",
                        Value = item.Value
                    }
                );
            }
            var workItem = await connection.GetWorkItemClient().UpdateWorkItemAsync(jsonLinkDocument, workItemId);
            logger.LogDebug("Updated work item {workItemId}", workItem.Id);
            return workItem;
        }

        private async Task<WorkItem> FindOrCreateServiceParent(string serviceName, bool ignoreReleasePlannerTests = true, string? tag = null)
        {
            var serviceParent = await FindServiceWorkItem(serviceName, ignoreReleasePlannerTests, tag);
            if (serviceParent != null)
            {
                logger.LogDebug("Found existing service work item [{workItemId}]", serviceParent.Id);
                return serviceParent;
            }

            var serviceWorkItem = new EpicWorkItem
            {
                ServiceName = serviceName,
                EpicType = "Service"
            };

            if (!string.IsNullOrEmpty(tag))
            {
                serviceWorkItem.Tag = tag;
            }

            var workItem = await CreateWorkItemAsync(serviceWorkItem, "Epic", serviceName);

            logger.LogInformation("[{workItemId}] - Created service work item for {serviceName}", workItem.Id, serviceName);
            return workItem;
        }

        private async Task<WorkItem?> FindEpicWorkItem(string serviceName, string? packageDisplayName = null, bool ignoreReleasePlannerTests = true, string? tag = null)
        {
            var serviceCondition = new StringBuilder();

            if (!string.IsNullOrEmpty(serviceName))
            {
                serviceCondition.Append($"[Custom.ServiceName] = '{serviceName}'");

                if (!string.IsNullOrEmpty(packageDisplayName))
                {
                    serviceCondition.Append($" AND [Custom.PackageDisplayName] = '{packageDisplayName}'");
                }
                else
                {
                    serviceCondition.Append(" AND [Custom.PackageDisplayName] = ''");
                }
            }
            else
            {
                serviceCondition.Append("[Custom.ServiceName] <> ''");
            }

            if (!string.IsNullOrEmpty(tag))
            {
                serviceCondition.Append($" AND [System.Tags] CONTAINS '{tag}'");
            }

            if (ignoreReleasePlannerTests)
            {
                serviceCondition.Append($" AND [System.Tags] NOT CONTAINS '{RELEASE_PLANNER_APP_TEST}'");
            }

            var query = $"SELECT [System.Id], [Custom.ServiceName], [Custom.PackageDisplayName], [System.Parent], [System.Tags] FROM WorkItems WHERE [System.State] <> 'Duplicate' AND [System.WorkItemType] = 'Epic' AND {serviceCondition}";

            logger.LogDebug("Finding parent work item with query: {query}", query);

            var workItems = await FetchWorkItemsAsync(query);

            if (workItems.Count > 0)
            {
                if (workItems.Count > 1)
                {
                    logger.LogWarning("Found multiple parent work items matching criteria. Using first match with ID: {workItemId}", workItems[0].Id);
                }
                return workItems[0];
            }

            return null;
        }

        private async Task<WorkItem?> FindProductWorkItem(string serviceName, string packageDisplayName, bool ignoreReleasePlannerTests = true, string? tag = null)
            => await FindEpicWorkItem(serviceName, packageDisplayName, ignoreReleasePlannerTests, tag);

        private async Task<WorkItem?> FindServiceWorkItem(string serviceName, bool ignoreReleasePlannerTests = true, string? tag = null)
            => await FindEpicWorkItem(serviceName, null, ignoreReleasePlannerTests, tag);

        private async Task UpdateWorkItemParentAsync(WorkItemBase child, WorkItemBase parent)
        {
            if (child.ParentId == parent.WorkItemId)
            {
                return; // already the parent
            }

            // Remove existing parent link
            // Child must have existing parent link
            if (child.ParentId != 0)
            {
                await RemoveWorkItemRelationAsync(child.WorkItemId, "Parent", child.ParentId);
            }
            
            await CreateWorkItemRelationAsync(child.WorkItemId, "Parent", parent.WorkItemId);
        }

        /// <summary>
        /// Gets all Label work items from the Release project.
        /// </summary>
        /// <returns>List of GitHubLableWorkItem objects</returns>
        public async Task<List<GitHubLableWorkItem>> GetGitHubLableWorkItemsAsync()
        {
            var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT}' AND [System.WorkItemType] = 'Label' AND [System.State] NOT IN ('Closed','Duplicate','Abandoned')";
            var workItems = await FetchWorkItemsPagedAsync(query);

            return workItems.Select(wi => new GitHubLableWorkItem
            {
                Label = wi.Fields.TryGetValue("Custom.Label", out object? labelValue) ? labelValue?.ToString() ?? string.Empty : string.Empty,
                WorkItemId = wi.Id ?? 0,
                WorkItemUrl = GetWorkItemHtmlUrl(wi.Id ?? 0)
            }).ToList();
        }

        /// <summary>
        /// Creates a new Label work item in the Release project.
        /// </summary>
        /// <param name="label">The label name to create</param>
        /// <returns>The created GitHubLableWorkItem</returns>
        public async Task<GitHubLableWorkItem> CreateGitHubLableWorkItemAsync(string label)
        {
            var patchDocument = new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument
            {
                new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/System.Title",
                    Value = $"Label: {label}"
                },
                new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/Custom.Label",
                    Value = label
                },
                new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/System.History",
                    Value = "This work item was automatically created from the service labels CSV file (common-labels.csv) by the azsdk CLI tool."
                }
            };

            logger.LogInformation("Creating Label work item for '{label}'", label);
            var workItem = await connection.GetWorkItemClient().CreateWorkItemAsync(patchDocument, Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT, "Label");
            
            if (workItem == null || workItem.Id == null)
            {
                throw new Exception($"Failed to create Label work item for '{label}'");
            }

            logger.LogInformation("Created Label work item {workItemId} for '{label}'", workItem.Id, label);

            return new GitHubLableWorkItem
            {
                Label = label,
                WorkItemId = workItem.Id ?? 0,
                WorkItemUrl = GetWorkItemHtmlUrl(workItem.Id ?? 0)
            };
        }

        private static string GetWorkItemHtmlUrl(int workItemId)
        {
            return $"{Constants.AZURE_SDK_DEVOPS_BASE_URL}/{Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT}/_workitems/edit/{workItemId}";
        }

        public async Task<ProductInfo?> GetProductInfoByTypeSpecProjectPathAsync(string typeSpecProjectPath)
        {
            try
            {
                logger.LogInformation("Searching for release plan with TypeSpec project path: {typeSpecProjectPath}", typeSpecProjectPath);

                // Query for release plans with the given TypeSpec project path
                var escapedPath = typeSpecProjectPath?.Replace("'", "''");
                var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT}'";
                query += $" AND [Custom.ApiSpecProjectPath] = '{escapedPath}'";
                query += " AND [System.WorkItemType] = 'Release Plan'";
                query += " AND [System.State] NOT IN ('Closed','Duplicate','Abandoned')";
                query += $" AND [System.Tags] NOT CONTAINS '{RELEASE_PLANNER_APP_TEST}'";

                var releasePlanWorkItems = await FetchWorkItemsAsync(query);
                if (releasePlanWorkItems.Count == 0)
                {
                    logger.LogInformation("No release plan found for TypeSpec project path: {typeSpecProjectPath}", typeSpecProjectPath);
                    return null;
                }

                if (releasePlanWorkItems.Count > 1)
                {
                    logger.LogWarning(
                        "Multiple release plan work items ({count}) found for TypeSpec project path: {typeSpecProjectPath}. Using the first one.",
                        releasePlanWorkItems.Count,
                        typeSpecProjectPath);
                }
                // Get the first matching release plan
                var releasePlanWorkItem = releasePlanWorkItems[0];
                logger.LogInformation("Found release plan work item {workItemId}", releasePlanWorkItem.Id);

                // Get parent work item (Product/Epic work item)
                if (releasePlanWorkItem.Relations == null || !releasePlanWorkItem.Relations.Any())
                {
                    logger.LogWarning("Release plan {workItemId} has no relations", releasePlanWorkItem.Id);
                    return null;
                }

                var parentRelation = releasePlanWorkItem.Relations.FirstOrDefault(r => r.Rel.Equals("System.LinkTypes.Hierarchy-Reverse"));
                if (parentRelation == null)
                {
                    logger.LogWarning("Release plan {workItemId} has no parent work item", releasePlanWorkItem.Id);
                    return null;
                }

                // Extract parent work item ID from the URL
                var urlParts = parentRelation.Url.Split('/');
                if (!int.TryParse(urlParts.Last(), out int parentWorkItemId))
                {
                    logger.LogError("Failed to parse parent work item ID from URL: {url}", parentRelation.Url);
                    return null;
                }
                logger.LogInformation("Found parent work item {parentWorkItemId}", parentWorkItemId);

                // Get parent work item details
                var parentWorkItem = await connection.GetWorkItemClient().GetWorkItemAsync(parentWorkItemId, expand: WorkItemExpand.All);
                if (parentWorkItem == null || parentWorkItem.Id == null)
                {
                    logger.LogError("Failed to retrieve parent work item {parentWorkItemId}", parentWorkItemId);
                    return null;
                }

                // Extract product information from parent work item (Epic)
                var productInfo = new ProductInfo
                {
                    WorkItemId = parentWorkItem.Id ?? 0,
                    Title = parentWorkItem.Fields.TryGetValue("System.Title", out object? value) ? value?.ToString() ?? string.Empty : string.Empty,
                    ProductServiceTreeId = parentWorkItem.Fields.TryGetValue("Custom.ProductServiceTreeID", out value) ? value?.ToString() ?? string.Empty : string.Empty,
                    ServiceId = parentWorkItem.Fields.TryGetValue("Custom.AssociatedServiceServiceTreeID", out value) ? value?.ToString() ?? string.Empty : string.Empty,
                    PackageDisplayName = parentWorkItem.Fields.TryGetValue("Custom.PackageDisplayName", out value) ? value?.ToString() ?? string.Empty : string.Empty,
                    ProductServiceTreeLink = parentWorkItem.Fields.TryGetValue("Custom.ProductServiceTreeLink", out value) ? value?.ToString() ?? string.Empty : string.Empty
                };

                logger.LogInformation("Successfully retrieved product info from work item {workItemId}", productInfo.WorkItemId);
                return productInfo;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get product info for TypeSpec project path: {typeSpecProjectPath}", typeSpecProjectPath);
                throw new Exception($"Failed to get product info for TypeSpec project path '{typeSpecProjectPath}'. Error: {ex.Message}", ex);
            }
        }
    }
}
