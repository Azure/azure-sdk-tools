using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using AzureSDKDSpecTools.Models;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Work.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.OAuth;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using Newtonsoft.Json.Linq;


namespace AzureSDKDSpecTools.Services
{
    public interface IDevOpsService
    {
        public Task<ReleasePlan> GetReleasePlan(int workItemId);
        public Task<List<ReleasePlan>> GetReleasePlans(string serviceTreeId, string productTreeId, string pullRequest);
        public Task<WorkItem> CreateReleasePlanWorkItem(ReleasePlan releasePlan);
    }
    public class DevOpsService : IDevOpsService
    {
        private static readonly string devOpsUrl = "https://dev.azure.com/azure-sdk";
        private static readonly string releaseProject = "release";
        private static readonly string organization = "azure-sdk";

        private readonly BuildHttpClient buildClient;
        private readonly WorkItemTrackingHttpClient workItemClient;

        public DevOpsService()
        {
            // Connect to Azure DevOps using managed identity
            var token = (new DefaultAzureCredential()).GetToken(new TokenRequestContext(["499b84ac-1321-427f-aa17-267ca6975798/.default"]));
            var connection = new VssConnection(new Uri(devOpsUrl), new VssOAuthAccessTokenCredential(token.Token));
            buildClient = connection.GetClient<BuildHttpClient>();
            workItemClient = connection.GetClient<WorkItemTrackingHttpClient>();
        }

        public async Task<ReleasePlan> GetReleasePlan(int workItemId)
        {
            Console.WriteLine($"Fetching release plan work with id {workItemId}");
            var workItem = await workItemClient.GetWorkItemAsync(workItemId);
            if (workItem?.Id == null)
                throw new InvalidOperationException($"Work item {workItemId} not found.");
            var releasePlan = MapWorkItemToReleasePlan(workItem);
            releasePlan.WorkItemUrl = workItem.Url;
            releasePlan.WorkItemId = workItem?.Id ?? 0;
            return releasePlan;
        }

        private static ReleasePlan MapWorkItemToReleasePlan(WorkItem workItem)
        {
            Console.WriteLine($"Work item details: {workItem.Fields.ToString()}");
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
            return releasePlan;
        }

        public async Task<List<ReleasePlan>> GetReleasePlans(string serviceTreeId, string productTreeId, string pullRequest)
        {
            var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{releaseProject}' AND [System.WorkItemType] = 'Release Plan' AND [Custom.ServiceTreeID] = '{serviceTreeId}' AND [Custom.ProductServiceTreeID] = '{productTreeId}' AND [System.State] NOT IN ('Abandoned', 'Duplicate', 'Finished')";
            var workItems = await FetchWorkItems(query);
            List<ReleasePlan> releasePlans = new ();
            Console.WriteLine($"Fetched {workItems.Count} release plans for service and product.");
            foreach (var workItem in workItems)
            {
                if (workItem.Relations == null)
                    continue;
                // Find API spec work item
                var apiSpecRelations = workItem.Relations.Where(r => r.Rel.Equals("System.LinkTypes.Hierarchy-Forward"));
                foreach(var apiSpecRelation in apiSpecRelations)
                {
                    Console.WriteLine($"Found {apiSpecRelation.Title} as child work item");
                    var apiSpecWorkItemId = int.Parse(apiSpecRelation.Url.Split('/').Last());
                    var apiSpecWorkItem = await workItemClient.GetWorkItemAsync(apiSpecWorkItemId);
                    if (apiSpecWorkItem != null)
                    {
                        // Find all spec pull requests added in API spec work item.
                        if (apiSpecWorkItem.Fields.TryGetValue("Custom.RESTAPIReviews", out Object? value))
                        {
                            var restApiReviews = value?.ToString();
                            if (restApiReviews != null)
                            {
                                var pullRequests = ParsePullRequestLinks(restApiReviews);
                                if (pullRequests.Contains(pullRequest.ToLower()))
                                {
                                    var releasePlan = MapWorkItemToReleasePlan(workItem);
                                    releasePlan.SpecPullRequests.AddRange(pullRequests);
                                    releasePlans.Add(releasePlan);
                                }
                            }
                        }
                    }
                }
            }
            return releasePlans;
        }

        private static HashSet<string> ParsePullRequestLinks(string htmlText)
        {
            // This method parses pull requiest links from html text like
            // "<a href=\"https://github.com/Azure/azure-rest-api-specs/pull/33459\">https://github.com/Azure/azure-rest-api-specs/pull/33459</a><br><a href=\"https://github.com/Azure/azure-rest-api-specs/pull/32282\">https://github.com/Azure/azure-rest-api-specs/pull/32282</a><br"
            HashSet<string> links = new HashSet<string>();
            var regex = new Regex("https:\\/\\/github\\.com\\/[\\w-]+\\/[\\w-]+\\/pull\\/\\d+");
            links.AddRange(regex.Matches(htmlText).Select(m => m.Value.ToLower()));
            return links;
        }

        public async Task<WorkItem> CreateReleasePlanWorkItem(ReleasePlan releasePlan)
        {
            int releasePlanWorkItemId = 0;
            int apiSpecWorkItemId = 0;

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
                return releasePlanWorkItem;
            }
            catch (Exception ex) {
                var errorMesage = $"Failed to create release plan and API spec work items, Error:{ex.Message}";
                Console.WriteLine(errorMesage);
                // Delete created work items if both release plan and API spec work items were not created and linked
                if (releasePlanWorkItemId != 0)
                    await workItemClient.DeleteWorkItemAsync(releasePlanWorkItemId);
                if (apiSpecWorkItemId != 0)
                    await workItemClient.DeleteWorkItemAsync(apiSpecWorkItemId);
                throw new Exception (errorMesage);
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

            Console.WriteLine($"Creating {workItemType} work item");
            var workItem = await workItemClient.CreateWorkItemAsync(specDocument, releaseProject, workItemType);
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
                await workItemClient.UpdateWorkItemAsync(jsonLinkDocument, parentId);
            }
            catch (Exception ex)
            {
                var errorMesage = $"Failed to link work item {childUrl} as child of {parentId}, Error: {ex.Message}";
                throw new Exception (errorMesage);
            }            
        }

        private async Task<List<WorkItem>> FetchWorkItems(string query)
        {
            var result = await workItemClient.QueryByWiqlAsync(new Wiql { Query = query });
            if (result != null && result.WorkItems != null)
            {
                var workItems = await workItemClient.GetWorkItemsAsync(result.WorkItems.Select(wi => wi.Id), expand: WorkItemExpand.Relations);
                foreach (var workItem in workItems)
                {
                    Console.WriteLine($"Work Item ID: {workItem.Id}, Title: {workItem.Fields["System.Title"]}");
                }
                return workItems;
            }
            else
            {
                Console.WriteLine("No work items found.");
                return [];
            }
        }
    }
}
