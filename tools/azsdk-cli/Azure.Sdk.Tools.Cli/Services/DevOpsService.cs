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
using System.Text.RegularExpressions;
using System.Text;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses;

namespace Azure.Sdk.Tools.Cli.Services
{
    public interface IDevOpsConnection
    {
        public BuildHttpClient GetBuildClient();
        public WorkItemTrackingHttpClient GetWorkItemClient();
        public ProjectHttpClient GetProjectClient();
    }

    public class DevOpsConnection : IDevOpsConnection
    {
        private BuildHttpClient _buildClient;
        private WorkItemTrackingHttpClient _workItemClient;
        private ProjectHttpClient _projectClient;
        private AccessToken? _token;
        private static readonly string DEVOPS_SCOPE = "499b84ac-1321-427f-aa17-267ca6975798/.default"; 

        private void RefreshConnection()
        {
            if (_token != null && _token?.ExpiresOn > DateTimeOffset.Now.AddMinutes(5))
                return;

            try
            {
                _token = (new DefaultAzureCredential()).GetToken(new TokenRequestContext([DEVOPS_SCOPE]));
            }
            catch
            {
                _token = (new InteractiveBrowserCredential()).GetToken(new TokenRequestContext([DEVOPS_SCOPE]));
            }

            if (_token == null)
                throw new Exception("Failed to get DevOps access token. Please make sure you have access to Azure DevOps and you are logged in using az login.");


            try
            {
                var connection = new VssConnection(new Uri(DevOpsService.DEVOPS_URL), new VssOAuthAccessTokenCredential(_token?.Token));
                _buildClient = connection.GetClient<BuildHttpClient>();
                _workItemClient = connection.GetClient<WorkItemTrackingHttpClient>();
                _projectClient = connection.GetClient<ProjectHttpClient>();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to refresh DevOps connection. Error: {ex.Message}");
            }            
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
        public Task<ReleasePlan> GetReleasePlanAsync(int releasePlanId);
        public Task<ReleasePlan> GetReleasePlanForWorkItemAsync(int workItemId);
        public Task<ReleasePlan> GetReleasePlanAsync(string pullRequestUrl);
        public Task<WorkItem> CreateReleasePlanWorkItemAsync(ReleasePlan releasePlan);
        public Task<Build> RunSDKGenerationPipelineAsync(string branchRef, string typespecProjectRoot, string apiVersion, string sdkReleaseType, string language, int workItemId);
        public Task<Build> GetPipelineRunAsync(int buildId);
        public Task<string> GetSDKPullRequestFromPipelineRunAsync(int buildId, string language, int workItemId);
        public Task<bool> AddSdkInfoInReleasePlanAsync(int workItemId, string language, string sdkGenerationPipelineUrl, string sdkPullRequestUrl);
        public Task<bool> UpdateReleasePlanSDKDetailsAsync(int workItemId, List<SDKInfo> sdkLanguages);
        public Task<bool> UpdateApiSpecStatusAsync(int workItemId, string status);
        public Task<bool> UpdateSpecPullRequestAsync(int releasePlanWorkItemId, string specPullRequest);
        public Task<bool> LinkNamespaceApprovalIssueAsync(int releasePlanWorkItemId, string url);
        public Task<PackageResponse> GetPackageWorkItemAsync(string packageName, string language, string packageVersion = "");
    }

    public partial class DevOpsService(ILogger<DevOpsService> logger, IDevOpsConnection connection) : IDevOpsService
    {
        public static readonly string DEVOPS_URL = "https://dev.azure.com/azure-sdk";
        public static readonly string RELEASE_PROJECT = "release";
        public static readonly string INTERNAL_PROJECT = "internal";
        private static readonly string RELEASE_PLANER_APP_TEST = "Release Planner App Test";


        [GeneratedRegex("\\|\\s(Beta|Stable|GA)\\s\\|\\s([\\S]+)\\s\\|\\s([\\S]+)\\s\\|")]
        private static partial Regex SdkReleaseDetailsRegex();

        public async Task<ReleasePlan> GetReleasePlanForWorkItemAsync(int workItemId)
        {
            logger.LogInformation($"Fetching release plan work with id {workItemId}");
            var workItem = await connection.GetWorkItemClient().GetWorkItemAsync(workItemId, expand: WorkItemExpand.All);
            if (workItem?.Id == null)
                throw new InvalidOperationException($"Work item {workItemId} not found.");
            var releasePlan = await MapWorkItemToReleasePlanAsync(workItem);
            releasePlan.WorkItemUrl = workItem.Url;
            releasePlan.WorkItemId = workItem?.Id ?? 0;
            return releasePlan;
        }

        public async Task<ReleasePlan> GetReleasePlanAsync(int releasePlanId)
        {
            // First find the API spec work item
            var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{RELEASE_PROJECT}' AND [Custom.ReleasePlanID] = '{releasePlanId}' AND [System.WorkItemType] = 'Release Plan' AND [System.State] NOT IN ('Closed','Duplicate','Abandoned')";
            var releasePlanWorkItems = await FetchWorkItemsAsync(query);
            if (releasePlanWorkItems.Count == 0)
            {
                throw new Exception($"Failed to find release plan work item with release plan Id {releasePlanId}");
            }
            return await MapWorkItemToReleasePlanAsync(releasePlanWorkItems[0]);
        }

        private async Task<ReleasePlan> MapWorkItemToReleasePlanAsync(WorkItem workItem)
        {
            var releasePlan = new ReleasePlan()
            {
                WorkItemId = workItem.Id ?? 0,
                WorkItemUrl = workItem.Url,
                Title = workItem.Fields.TryGetValue("System.Title", out object? value) ? value?.ToString() ?? string.Empty : string.Empty,
                Status = workItem.Fields.TryGetValue("System.State", out value) ? value?.ToString() ?? string.Empty : string.Empty,
                ServiceTreeId = workItem.Fields.TryGetValue("Custom.ServiceTreeID", out value) ? value?.ToString() ?? string.Empty : string.Empty,
                ProductTreeId = workItem.Fields.TryGetValue("Custom.ProductServiceTreeID", out value) ? value?.ToString() ?? string.Empty : string.Empty,
                SDKReleaseMonth = workItem.Fields.TryGetValue("Custom.SDKReleaseMonth", out value) ? value?.ToString() ?? string.Empty : string.Empty,
                IsManagementPlane = workItem.Fields.TryGetValue("Custom.MgmtScope", out value) ? value?.ToString() == "Yes" : false,
                IsDataPlane = workItem.Fields.TryGetValue("Custom.DataScope", out value) ? value?.ToString() == "Yes" : false,
                ReleasePlanLink = workItem.Fields.TryGetValue("Custom.ReleasePlanLink", out value) ? value?.ToString() ?? string.Empty : string.Empty,
                ReleasePlanId = workItem.Fields.TryGetValue("Custom.ReleasePlanID", out value) ? int.Parse(value?.ToString() ?? "0") : 0,
                SDKReleaseType = workItem.Fields.TryGetValue("Custom.SDKtypetobereleased", out value) ? value?.ToString() ?? string.Empty : string.Empty,
                IsCreatedByAgent = workItem.Fields.TryGetValue("Custom.IsCreatedByAgent", out value) && "Copilot".Equals(value?.ToString()),
                ReleasePlanSubmittedByEmail = workItem.Fields.TryGetValue("Custom.ReleasePlanSubmittedByEmail", out value) ? value?.ToString() ?? string.Empty : string.Empty,
                SDKLanguages = workItem.Fields.TryGetValue("Custom.SDKLanguages", out value) ? value?.ToString() ?? string.Empty : string.Empty,
                IsSpecApproved = workItem.Fields.TryGetValue("Custom.APISpecApprovalStatus", out value) && "Approved".Equals(value?.ToString())
            };

            var languages = new string[] { "Dotnet", "JavaScript", "Python", "Java", "Go" };
            foreach (var lang in languages)
            {
                var sdkGenPipelineUrl = workItem.Fields.TryGetValue($"Custom.SDKGenerationPipelineFor{lang}", out value) ? value?.ToString() ?? string.Empty : string.Empty;
                var sdkPullRequestUrl = workItem.Fields.TryGetValue($"Custom.SDKPullRequestFor{lang}", out value) ? value?.ToString() ?? string.Empty : string.Empty;
                var packageName = workItem.Fields.TryGetValue($"Custom.{lang}PackageName", out value) ? value?.ToString() ?? string.Empty : string.Empty;

                if (!string.IsNullOrEmpty(sdkGenPipelineUrl) || !string.IsNullOrEmpty(sdkPullRequestUrl) || !string.IsNullOrEmpty(packageName))
                {
                    releasePlan.SDKInfo.Add(
                        new SDKInfo()
                        {
                            Language = MapLanguageIdToName(lang),
                            GenerationPipelineUrl = sdkGenPipelineUrl,
                            SdkPullRequestUrl = sdkPullRequestUrl,
                            PackageName = packageName
                        }
                    );
                }
            }

            // Get details from API spec work item
            try
            {
                logger.LogInformation($"Fetching API spec work item for release plan work item {releasePlan.WorkItemId}");
                var apiSpecWorkItem = await GetApiSpecWorkItemAsync(releasePlan.WorkItemId);
                if (apiSpecWorkItem != null && apiSpecWorkItem.Fields != null)
                {
                    releasePlan.ActiveSpecPullRequest = apiSpecWorkItem.Fields.TryGetValue("Custom.ActiveSpecPullRequestUrl", out Object? specPr) ? specPr?.ToString() ?? string.Empty : string.Empty;
                    releasePlan.SpecAPIVersion = apiSpecWorkItem.Fields.TryGetValue("Custom.APISpecversion", out Object? apiVersion) ? apiVersion?.ToString() ?? string.Empty : string.Empty;
                    releasePlan.SpecType = apiSpecWorkItem.Fields.TryGetValue("Custom.APISpecDefinitionType", out Object? specType) ? specType?.ToString() ?? string.Empty : string.Empty;
                }
                else
                {
                    logger.LogWarning($"API spec work item not found for release plan work item {releasePlan.WorkItemId}");
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to get API spec work item for release plan work item {releasePlan.WorkItemId}. Error: {ex.Message}");
            }

            return releasePlan;
        }

        public async Task<ReleasePlan> GetReleasePlanAsync(string pullRequestUrl)
        {
            // First find the API spec work item
            try
            {
                var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{RELEASE_PROJECT}' AND [Custom.RESTAPIReviews] CONTAINS WORDS '{pullRequestUrl}' AND [System.WorkItemType] = 'API Spec' AND [System.State] NOT IN ('Closed','Duplicate','Abandoned')";
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
                            continue;
                        // Get parent work item and make sure it is release plan work item
                        var parentWorkItemId = int.Parse(parent.Url.Split('/').Last());
                        var parentWorkItem = await connection.GetWorkItemClient().GetWorkItemAsync(parentWorkItemId);
                        if (parentWorkItem == null || !parentWorkItem.Fields.TryGetValue("System.WorkItemType", out Object? parentType))
                            continue;
                        if (parentType.Equals("Release Plan"))
                            return await MapWorkItemToReleasePlanAsync(parentWorkItem);
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to get release plan for pull request URL {pullRequestUrl}. Error: {ex.Message}");
                throw new Exception($"Failed to get release plan for pull request URL {pullRequestUrl}. Error: {ex.Message}");
            }

        }

        public async Task<WorkItem> CreateReleasePlanWorkItemAsync(ReleasePlan releasePlan)
        {
            int releasePlanWorkItemId = 0;
            int apiSpecWorkItemId = 0;
            var workItemClient = connection.GetWorkItemClient();
            try
            {
                // Create release plan work item
                var releasePlanTitle = $"Release plan for {releasePlan.ProductName ?? releasePlan.ProductTreeId}";
                WorkItem releasePlanWorkItem = await CreateWorkItemAsync(releasePlan, "Release Plan", releasePlanTitle);
                releasePlanWorkItemId = releasePlanWorkItem?.Id ?? 0;
                if (releasePlanWorkItemId == 0)
                {
                    throw new Exception("Failed to create release plan work item");
                }

                // Create API spec work item
                var apiSpecTitle = $"API spec for {releasePlan.ProductName ?? releasePlan.ProductTreeId} - version {releasePlan.SpecAPIVersion}";
                var apiSpecWorkItem = await CreateWorkItemAsync(releasePlan, "API Spec", apiSpecTitle);
                apiSpecWorkItemId = apiSpecWorkItem.Id ?? 0;
                if (apiSpecWorkItemId == 0)
                {
                    throw new Exception("Failed to create API spec work item");
                }

                // Link API spec as child of release plan
                await LinkWorkItemAsChildAsync(releasePlanWorkItemId, apiSpecWorkItem.Url);
                if (releasePlanWorkItem != null)
                    return releasePlanWorkItem;

                throw new Exception("Failed to create API spec work item");
            }
            catch (Exception ex)
            {
                var errorMessage = $"Failed to create release plan and API spec work items, Error:{ex.Message}";
                logger.LogError(errorMessage);
                // Delete created work items if both release plan and API spec work items were not created and linked
                if (releasePlanWorkItemId != 0)
                    await workItemClient.DeleteWorkItemAsync(releasePlanWorkItemId);
                if (apiSpecWorkItemId != 0)
                    await workItemClient.DeleteWorkItemAsync(apiSpecWorkItemId);
                throw new Exception(errorMessage);
            }
        }

        private async Task<WorkItem> CreateWorkItemAsync(ReleasePlan releasePlan, string workItemType, string title)
        {
            var specDocument = releasePlan.GetPatchDocument();
            specDocument.Add(new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchOperation
            {
                Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                Path = "/fields/System.Title",
                Value = title
            });

            if (workItemType == "API Spec" && releasePlan.SpecPullRequests.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                foreach (var pr in releasePlan.SpecPullRequests)
                {
                    if (sb.Length > 0)
                        sb.Append("<br>");
                    sb.Append($"<a href=\"{pr}\">{pr}</a>");
                }
                var prLinks = sb.ToString();
                logger.LogInformation($"Adding pull request {prLinks} to API spec work item.");
                specDocument.Add(new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/Custom.RESTAPIReviews",
                    Value = sb.ToString()
                });

                var activeSpecPullRequest = releasePlan.ActiveSpecPullRequest;
                if (string.IsNullOrEmpty(activeSpecPullRequest))
                {
                    // If active spec pull request is not provided, use the first pull request from the list
                    activeSpecPullRequest = releasePlan.SpecPullRequests.FirstOrDefault() ?? string.Empty;
                }
                specDocument.Add(new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/Custom.ActiveSpecPullRequestUrl",
                    Value = activeSpecPullRequest
                });
            }

            logger.LogInformation($"Creating {workItemType} work item");
            var workItem = await connection.GetWorkItemClient().CreateWorkItemAsync(specDocument, RELEASE_PROJECT, workItemType);
            if (workItem == null)
            {
                throw new Exception("Failed to create Work Item");
            }
            return workItem;
        }

        private async Task LinkWorkItemAsChildAsync(int parentId, string childUrl)
        {
            try
            {
                // Add work item as child of release plan work item
                var jsonLinkDocument = new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument
                {
                     new JsonPatchOperation
                     {
                          Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                          Path = "/relations/-",
                          Value = new WorkItemRelation
                          {
                              Rel = "System.LinkTypes.Hierarchy-Forward",
                              Url = childUrl
                          }
                      }
                };
                await connection.GetWorkItemClient().UpdateWorkItemAsync(jsonLinkDocument, parentId);
            }
            catch (Exception ex)
            {
                var errorMessage = $"Failed to link work item {childUrl} as child of {parentId}, Error: {ex.Message}";
                throw new Exception(errorMessage);
            }
        }

        private static string MapLanguageToId(string language)
        {
            var lang = language.ToLower();
            return lang switch
            {
                ".net" => "Dotnet",
                "csharp" => "Dotnet",
                "js" => "JavaScript",
                "python" => "Python",
                "java" => "Java",
                "go" => "Go",
                _ => language
            };
        }

        private static string MapLanguageIdToName(string language)
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

        public async Task<bool> AddSdkInfoInReleasePlanAsync(int workItemId, string language, string sdkGenerationPipelineUrl, string sdkPullRequestUrl)
        {
            // Adds SDK generation and pull request link in release plan work item.
            try
            {
                if (string.IsNullOrEmpty(language) || workItemId == 0 || (string.IsNullOrEmpty(sdkGenerationPipelineUrl) && string.IsNullOrEmpty(sdkPullRequestUrl)))
                {
                    logger.LogError("Please provide the language, work item ID, and either the SDK generation pipeline URL or the SDK pull request URL to add SDK info to a work item.");
                    return false;
                }

                var jsonLinkDocument = new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument();
                // Add work item as child of release plan work item
                if (!string.IsNullOrEmpty(sdkGenerationPipelineUrl))
                {
                    jsonLinkDocument.Add(
                        new JsonPatchOperation
                        {
                            Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                            Path = $"/fields/Custom.SDKGenerationPipelineFor{language}",
                            Value = sdkGenerationPipelineUrl
                        });
                }
                if (!string.IsNullOrEmpty(sdkPullRequestUrl))
                {
                    jsonLinkDocument.Add(
                        new JsonPatchOperation
                        {
                            Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                            Path = $"/fields/Custom.SDKPullRequestFor{language}",
                            Value = sdkPullRequestUrl
                        });
                }

                await connection.GetWorkItemClient().UpdateWorkItemAsync(jsonLinkDocument, workItemId);
                return true;
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
                if (result != null && result.WorkItems != null)
                {
                    return await workItemClient.GetWorkItemsAsync(result.WorkItems.Select(wi => wi.Id), expand: WorkItemExpand.Relations);
                }
                else
                {
                    logger.LogWarning("No work items found.");
                    return [];
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get work item. Error: {ex.Message}");
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

        public async Task<Build> RunSDKGenerationPipelineAsync(string branchRef, string typespecProjectRoot, string apiVersion, string sdkReleaseType, string language, int workItemId)
        {
            int pipelineDefinitionId = GetPipelineDefinitionId(language);
            if (pipelineDefinitionId == 0)
            {
                throw new Exception($"Failed to get SDK generation pipeline for {language}.");
            }

            var buildClient = connection.GetBuildClient();
            var projectClient = connection.GetProjectClient();
            // Run pipeline
            var definition = await buildClient.GetDefinitionAsync(INTERNAL_PROJECT, pipelineDefinitionId);
            var project = await projectClient.GetProject(INTERNAL_PROJECT);

            // Queue SDK generation pipeline
            logger.LogInformation($"Queueing pipeline [{definition.Name}] to generate SDK for {language}.");
            var build = await buildClient.QueueBuildAsync(new Build()
            {
                Definition = definition,
                Project = project,
                SourceBranch = branchRef,
                TemplateParameters = new Dictionary<string, string>
                            {
                                { "ConfigType", "TypeSpec"},
                                { "ConfigPath", $"{typespecProjectRoot}/tspconfig.yaml" },
                                { "ApiVersion", apiVersion },
                                { "sdkReleaseType", sdkReleaseType },
                                { "SkipPullRequestCreation", "false" }
                            }
            });

            var pipelineRunUrl = GetPipelineUrl(build.Id);
            logger.LogInformation($"Started pipeline run {pipelineRunUrl} to generate SDK.");
            if (workItemId != 0)
            {
                logger.LogInformation("Adding SDK generation pipeline link to release plan");
                await AddSdkInfoInReleasePlanAsync(workItemId, MapLanguageToId(language), pipelineRunUrl, "");
            }

            return build;
        }

        public async Task<Build> GetPipelineRunAsync(int buildId)
        {
            var buildClient = connection.GetBuildClient();
            return await buildClient.GetBuildAsync(INTERNAL_PROJECT, buildId);
        }

        public async Task<string> GetSDKPullRequestFromPipelineRunAsync(int buildId, string language, int workItemId)
        {
            var buildClient = connection.GetBuildClient();
            var timeLine = await buildClient.GetBuildTimelineAsync(INTERNAL_PROJECT, buildId);
            var createPrJob = timeLine.Records.FirstOrDefault(r => r.Name == "Create pull request") ?? null;
            if (createPrJob == null)
            {
                return $"Failed to generate SDK. SDK pull request link is not available for pipeline run, Pipeline link {timeLine.Url}";
            }

            // Get SDK pull request from create pull request job attachment
            if (createPrJob.Result == TaskResult.Succeeded)
            {
                var contentStream = await buildClient.GetAttachmentAsync(INTERNAL_PROJECT, buildId, timeLine.Id, createPrJob.Id, "Distributedtask.Core.Summary", "Pull Request Created");
                if (contentStream != null)
                {
                    var content = new StreamReader(contentStream);
                    var pullRequestUrl = ParseSDKPullRequestUrl(content.ReadToEnd());
                    if (workItemId != 0)
                    {
                        logger.LogInformation("Adding SDK pull request to release plan");
                        await AddSdkInfoInReleasePlanAsync(workItemId, MapLanguageToId(language), GetPipelineUrl(buildId), pullRequestUrl);
                    }
                    return pullRequestUrl;
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
            return $"https://dev.azure.com/azure-sdk/internal/_build/results?buildId={buildId}";
        }

        public static string ParseSDKPullRequestUrl(string sdkGenerationSummary)
        {
            Regex regex = new Regex("https:\\/\\/github.com\\/[Aa]zure\\/azure-sdk-for-[a-z]+\\/pull\\/[0-9]+");
            var match = regex.Match(sdkGenerationSummary);
            return match.Success ? match.Value : string.Empty;
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
                logger.LogInformation($"Selected languages to generate SDK: {languages}");
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
                logger.LogInformation($"Updated SDK languages to work item [{workItemId}].");
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
                if (workItemId == 0 || string.IsNullOrEmpty(status))
                {
                    throw new ArgumentException("Please provide the work item ID and a status to update the work item.");
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
                    continue;
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
        public async Task<PackageResponse> GetPackageWorkItemAsync(string packageName, string language, string packageVersion = "")
        {
            language = MapLanguageIdToName(language);
            if ( packageName.Contains(' ') || packageName.Contains('\'') || packageName.Contains('"') || language.Contains(' ') || language.Contains('\'') || language.Contains('"') || packageVersion.Contains(' ') || packageVersion.Contains('\'') || packageVersion.Contains('"'))
            {
                throw new ArgumentException("Invalid data in one of the parameters.");
            }

            var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{RELEASE_PROJECT}' AND [Custom.Package] = '{packageName}' AND [Custom.Language] = '{language}' AND [System.WorkItemType] = 'Package' AND [System.State] NOT IN ('Closed','Duplicate','Abandoned') AND [System.Tags] NOT CONTAINS '{RELEASE_PLANER_APP_TEST}'";
            if (!string.IsNullOrEmpty(packageVersion))
            {
                query += $" AND [Custom.PackageVersion] = '{packageVersion}'";
            }
            query += "  ORDER BY [System.Id] DESC"; // Order by package work item to find the most recently created
            logger.LogInformation($"Fetching package work item with package name {packageName}, package version {packageVersion} and language {language}.");

            var packageWorkItems = await FetchWorkItemsAsync(query);
            if (packageWorkItems.Count == 0)
            {
                throw new Exception($"Failed to find package work item with package name {packageName}. Please check package name, package version and language values.");
            }
            return MapPackageWorkItemToModel(packageWorkItems[0]); // Return the first package work item
        }

        public static PackageResponse MapPackageWorkItemToModel(WorkItem workItem)
        {
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem), "Work item cannot be null.");
            }

            PackageResponse packageModel = new()
            {
                Name = GetWorkItemValue(workItem, "Custom.Package"),
                Version = GetWorkItemValue(workItem, "Custom.PackageVersion"),
                Language = GetWorkItemValue(workItem, "Custom.Language"),
                WorkItemId = workItem.Id ?? 0,
                WorkItemUrl = workItem.Url,
                State = GetWorkItemValue(workItem, "System.State"),
                PackageType = GetWorkItemValue(workItem, "Custom.PackageType"),
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
            foreach(Match m in matches)
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
    }
}
