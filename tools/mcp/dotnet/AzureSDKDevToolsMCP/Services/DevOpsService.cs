using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using AzureSDKDSpecTools.Helpers;
using AzureSDKDSpecTools.Models;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Common;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.Work.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.OAuth;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;


namespace AzureSDKDSpecTools.Services
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
        private AccessToken _token;
        private static readonly string DEVOPS_SCOPE = "499b84ac-1321-427f-aa17-267ca6975798/.default";

        public DevOpsConnection()
        {
            _token = GetToken();
            var connection = new VssConnection(new Uri(DevOpsService.DEVOPS_URL), new VssOAuthAccessTokenCredential(_token.Token));
            _buildClient = connection.GetClient<BuildHttpClient>();
            _workItemClient = connection.GetClient<WorkItemTrackingHttpClient>();
            _projectClient = connection.GetClient<ProjectHttpClient>();
        }

        private static AccessToken GetToken()
        {
            return (new DefaultAzureCredential()).GetToken(new TokenRequestContext([DEVOPS_SCOPE]));
        }

        private void RefreshConnection()
        {
            if (_token.ExpiresOn < DateTimeOffset.Now.AddMinutes(5))
            {
                _token = GetToken();
                var connection = new VssConnection(new Uri(DevOpsService.DEVOPS_URL), new VssOAuthAccessTokenCredential(_token.Token));
                _buildClient = connection.GetClient<BuildHttpClient>();
                _workItemClient = connection.GetClient<WorkItemTrackingHttpClient>();
                _projectClient = connection.GetClient<ProjectHttpClient>();
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
        public Task<ReleasePlan> GetReleasePlan(int workItemId);
        public Task<ReleasePlan> GetReleasePlan(string pullRequestUrl);
        public Task<WorkItem> CreateReleasePlanWorkItem(ReleasePlan releasePlan);
        public Task<Build> RunSDKGenerationPipeline(string branchRef, string typespecProjectRoot, string apiVersion, string sdkReleaseType, string language, int workItemId);
        public Task<Build> GetPipelineRun(int buildId);
        public Task<string> GetSDKPullRequestFromPipelineRun(int buildId, string language, int workItemId);
    }

    public class DevOpsService(ILogger<DevOpsService> logger, IDevOpsConnection connection) : IDevOpsService
    {
        public static readonly string DEVOPS_URL = "https://dev.azure.com/azure-sdk";
        public static readonly string RELEASE_PROJECT = "release";
        public static readonly string INTERNAL_PROJECT = "internal";
        private ILogger<DevOpsService> _logger = logger;
        private IDevOpsConnection _connection = connection;

        public async Task<ReleasePlan> GetReleasePlan(int workItemId)
        {
            _logger.LogInformation($"Fetching release plan work with id {workItemId}");
            var workItem = await _connection.GetWorkItemClient().GetWorkItemAsync(workItemId);
            if (workItem?.Id == null)
                throw new InvalidOperationException($"Work item {workItemId} not found.");
            _logger.LogInformation($"Release plan work item: [{JsonSerializer.Serialize(workItem)}]");
            var releasePlan = MapWorkItemToReleasePlan(workItem);
            releasePlan.WorkItemUrl = workItem.Url;
            releasePlan.WorkItemId = workItem?.Id ?? 0;
            return releasePlan;
        }

        private static ReleasePlan MapWorkItemToReleasePlan(WorkItem workItem)
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
                IsDataPlane = workItem.Fields.TryGetValue("Custom.DataScope", out value) ? value?.ToString() == "Yes" : false
            };

            // Get sdk generation status if it was already run for current release plan
            var sdkGenPipelineFields = workItem.Fields.Keys.Where(k => k.StartsWith("Custom.SDKGenerationPipelineFor"));
            var sdkGenerationInfoList = releasePlan.SDKGenerationInfos;
            if (sdkGenPipelineFields.Any())
            {
                foreach (var fieldName in sdkGenPipelineFields)
                {
                    var sdkGenPipelineInfo = new SDKGenerationInfo()
                    {
                        GenerationPipelineUrl = workItem.Fields[fieldName]?.ToString() ?? string.Empty,
                        Language = MapLanguageIdToName(fieldName.Replace("Custom.SDKGenerationPipelineFor", ""))
                    };
                    sdkGenerationInfoList.Add(sdkGenPipelineInfo);
                }
            }

            // Get sdk pull request links
            var sdkPullRequestFields = workItem.Fields.Keys.Where(k => k.StartsWith("Custom.SDKPullRequestFor"));
            if (sdkPullRequestFields.Any())
            {
                foreach (var fieldName in sdkPullRequestFields)
                {
                    var language = MapLanguageIdToName(fieldName.Replace("Custom.SDKPullRequestFor", ""));
                    if (!sdkGenerationInfoList.Any(s => s.Language.Equals(language)))
                        sdkGenerationInfoList.Add(
                            new SDKGenerationInfo()
                            {
                                Language = language
                            }
                        );
                    //Update pull request link
                    sdkGenerationInfoList.First(s => s.Language.Equals(language)).SdkPullRequestUrl = workItem.Fields[fieldName]?.ToString() ?? string.Empty;
                }
            }
            return releasePlan;
        }

        public async Task<ReleasePlan> GetReleasePlan(string pullRequestUrl)
        {
            // First find the API sepc work item
            var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{RELEASE_PROJECT}' AND [Custom.RESTAPIReviews] CONTAINS WORDS '{pullRequestUrl}' AND [System.WorkItemType] = 'API Spec' AND [System.State] NOT IN ('Closed','Duplicate','Abandoned')";
            var apiSpecWorkItems = await FetchWorkItems(query);
            if (apiSpecWorkItems.Count == 0)
            {
                throw new Exception($"Failed to find API sepc work item for pull request URL {pullRequestUrl}");
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
                    var parentWorkItem = await _connection.GetWorkItemClient().GetWorkItemAsync(parentWorkItemId);
                    if (parentWorkItem == null || !parentWorkItem.Fields.TryGetValue("System.WorkItemType", out Object? parentType))
                        continue;
                    if (parentType.Equals("Release Plan"))
                        return MapWorkItemToReleasePlan(parentWorkItem);
                }
            }
            throw new Exception($"Failed to find a release plan with {pullRequestUrl} as spec pull request.");
        }

        public async Task<WorkItem> CreateReleasePlanWorkItem(ReleasePlan releasePlan)
        {
            int releasePlanWorkItemId = 0;
            int apiSpecWorkItemId = 0;
            var workItemClient = _connection.GetWorkItemClient();
            try
            {
                // Create release plan work item
                var releasePlanTitle = $"Release plan for {releasePlan.ProductName ?? releasePlan.ProductTreeId}";
                WorkItem releasePlanWorkItem = await CreateWorkItem(releasePlan, "Release Plan", releasePlanTitle);
                releasePlanWorkItemId = releasePlanWorkItem?.Id ?? 0;
                if (releasePlanWorkItemId == 0)
                {
                    throw new Exception("Failed to create release plan work item");
                }

                // Create API spec work item
                var apiSpecTitle = $"API spec for {releasePlan.ProductName ?? releasePlan.ProductTreeId} - version {releasePlan.SpecAPIVersion}";
                var apiSpecWorkItem = await CreateWorkItem(releasePlan, "API Spec", apiSpecTitle);
                apiSpecWorkItemId = apiSpecWorkItem.Id ?? 0;
                if (apiSpecWorkItemId == 0)
                {
                    throw new Exception("Failed to create API spec work item");
                }

                // Link API spec as child of release plan
                await LinkWorkItemAsChild(releasePlanWorkItemId, apiSpecWorkItem.Url);
                if (releasePlanWorkItem != null)
                    return releasePlanWorkItem;

                throw new Exception("Failed to create API spec work item");
            }
            catch (Exception ex) {
                var errorMesage = $"Failed to create release plan and API spec work items, Error:{ex.Message}";
                _logger.LogError(errorMesage);
                // Delete created work items if both release plan and API spec work items were not created and linked
                if (releasePlanWorkItemId != 0)
                    await workItemClient.DeleteWorkItemAsync(releasePlanWorkItemId);
                if (apiSpecWorkItemId != 0)
                    await workItemClient.DeleteWorkItemAsync(apiSpecWorkItemId);
                throw new Exception(errorMesage);
            }
        }

        private async Task<WorkItem> CreateWorkItem(ReleasePlan releasePlan, string workItemType, string title)
        {
            var specDocument = releasePlan.GetPatchDocument();
            specDocument.Add(new JsonPatchOperation
            {
                Operation = Operation.Add,
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
                _logger.LogInformation($"Adding pull request {prLinks} to API spec work item.");
                specDocument.Add(new JsonPatchOperation
                {
                    Operation = Operation.Add,
                    Path = "/fields/Custom.RESTAPIReviews",
                    Value = sb.ToString()
                });
            }

            _logger.LogInformation($"Creating {workItemType} work item");
            var workItem = await _connection.GetWorkItemClient().CreateWorkItemAsync(specDocument, RELEASE_PROJECT, workItemType);
            if (workItem == null)
            {
                throw new Exception("Failed to create Work Item");
            }
            return workItem;
        }

        private async Task LinkWorkItemAsChild(int parentId, string childUrl)
        {
            try
            {
                // Add work item as child of release plan work item
                var jsonLinkDocument = new JsonPatchDocument
                {
                     new JsonPatchOperation
                     {
                          Operation = Operation.Add,
                          Path = "/relations/-",
                          Value = new WorkItemRelation
                          {
                              Rel = "System.LinkTypes.Hierarchy-Forward",
                              Url = childUrl
                          }
                      }
                };
                await _connection.GetWorkItemClient().UpdateWorkItemAsync(jsonLinkDocument, parentId);
            }
            catch (Exception ex)
            {
                var errorMesage = $"Failed to link work item {childUrl} as child of {parentId}, Error: {ex.Message}";
                throw new Exception(errorMesage);
            }
        }

        private static string MapLanguageToId(string language)
        {
            return language switch
            {
                ".NET" => "Dotnet",
                _ => language
            };
        }

        private static string MapLanguageIdToName(string language)
        {
            return language switch
            {
                "Dotnet" => ".NET",
                _ => language
            };
        }

        private async Task<bool> AddSdkInfoInReleasePlan(int workItemId, string language, string sdkGenerationPipelineUrl, string sdkPullRequestUrl)
        {
            // Adds SDK generation and pull request link in release plan work item.
            try
            {
                if (string.IsNullOrEmpty(language) || workItemId == 0 || string.IsNullOrEmpty(sdkGenerationPipelineUrl))
                {
                    _logger.LogError("Language, work item ID and SDK generation pipeline URL are required to add SDK generation info to work item.");
                    return false;
                }
                // Add work item as child of release plan work item
                var jsonLinkDocument = new JsonPatchDocument
                {
                     new JsonPatchOperation
                     {
                          Operation = Operation.Add,
                          Path = $"/fields/Custom.SDKGenerationPipelineFor{language}",
                          Value = sdkGenerationPipelineUrl
                     }
                };
                if (!string.IsNullOrEmpty(sdkPullRequestUrl))
                {
                    jsonLinkDocument.Add(
                        new JsonPatchOperation
                        {
                            Operation = Operation.Add,
                            Path = $"/fields/Custom.SDKPullRequestFor{language}",
                            Value = sdkPullRequestUrl
                        });
                }

                await _connection.GetWorkItemClient().UpdateWorkItemAsync(jsonLinkDocument, workItemId);
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to update SDK generation details to work item [{workItemId}]. Error: {ex.Message}");
            }
        }

        private async Task<List<WorkItem>> FetchWorkItems(string query)
        {
            try
            {
                var workItemclient = _connection.GetWorkItemClient();
                var result = await workItemclient.QueryByWiqlAsync(new Wiql { Query = query });
                if (result != null && result.WorkItems != null)
                {
                    return await workItemclient.GetWorkItemsAsync(result.WorkItems.Select(wi => wi.Id), expand: WorkItemExpand.Relations);
                }
                else
                {
                    _logger.LogWarning("No work items found.");
                    return [];
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get release plan. Error: {ex.Message}");
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

        public async Task<Build> RunSDKGenerationPipeline(string branchRef, string typespecProjectRoot, string apiVersion, string sdkReleaseType, string language, int workItemId)
        {
            int pipelineDefinitionId = GetPipelineDefinitionId(language);
            if (pipelineDefinitionId == 0)
            {
                throw new Exception($"Failed to get SDK generation pipeline for {language}.");
            }

            var buildClient = _connection.GetBuildClient();
            var projectClient = _connection.GetProjectClient();
            // Run pipeline
            var definition = await buildClient.GetDefinitionAsync(INTERNAL_PROJECT, pipelineDefinitionId);
            var project = await projectClient.GetProject(INTERNAL_PROJECT);

            // Queue SDK generation pipeline
            _logger.LogInformation($"Queueing pipeline [{definition.Name}] to generate SDK for {language}.");
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

            _logger.LogInformation($"Started pipeline run {build.Url} to generate SDK.");
            if (workItemId != 0)
            {
                _logger.LogInformation("Adding SDK generation pipeline link to release plan");
                await AddSdkInfoInReleasePlan(workItemId, MapLanguageToId(language), GetPipelineUrl(build.Id), "");
            }
            
            return build;
        }

        public async Task<Build> GetPipelineRun(int buildId)
        {
            var buildClient = _connection.GetBuildClient();
            return await buildClient.GetBuildAsync(INTERNAL_PROJECT, buildId);
        }

        public async Task<string> GetSDKPullRequestFromPipelineRun(int buildId, string language, int workItemId)
        {
            var buildClient = _connection.GetBuildClient();
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
                    var pullrequestUrl = ParseSDKPullRequestUrl(content.ReadToEnd());
                    if (workItemId != 0)
                    {
                        _logger.LogInformation("Adding SDK pull request to release plan");
                        await AddSdkInfoInReleasePlan(workItemId, MapLanguageToId(language), GetPipelineUrl(buildId), pullrequestUrl);
                    }
                    return pullrequestUrl;
                }                
            }

            // Chck if there is any warning related to Generate SDK or create pr jobs. Ignore all 1ES jobs to avoid showing irrelevant warning.
            StringBuilder sb = new($"Failed to generate SDK pull request for {language}.");
            foreach(var job in timeLine.Records.Where( t => !t.Name.Contains("1ES")))
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

        private static string ParseSDKPullRequestUrl(string sdkGenerationSummary)
        {
            Regex regex = new Regex("https:\\/\\/github.com\\/Azure\\/azure-sdk-for-[a-z]+\\/pull\\/[0-9]+");
            var match = regex.Match(sdkGenerationSummary);
            return match.Success? match.Value : string.Empty;
        }
    }
}
