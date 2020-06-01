using Azure.Cosmos;
using Azure.Identity;
using Azure.Sdk.Tools.PipelineWitness.Entities.AzurePipelines;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.Organization.Client;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.WindowsAzure.Storage.Blob.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PipelineWitness
{
    public class RunProcessor
    {
        public RunProcessor(ILogger<RunProcessor> logger, IMemoryCache cache, HttpClient httpClient)
        {
            this.logger = logger;
            this.cache = cache;
            this.httpClient = httpClient;
        }

        private ILogger<RunProcessor> logger;
        private IMemoryCache cache;
        private HttpClient httpClient;

        private AuthenticationHeaderValue GetAuthenticationHeader(string azureDevOpsPersonalAccessToken)
        {
            var usernameAndPassword = $"nobody:{azureDevOpsPersonalAccessToken}";
            var usernameAndPasswordBytes = Encoding.ASCII.GetBytes(usernameAndPassword);
            var encodedUsernameAndPassword = Convert.ToBase64String(usernameAndPasswordBytes);
            var header = new AuthenticationHeaderValue("Basic", encodedUsernameAndPassword);
            return header;
        }
        private bool IsValidAzureDevOpsUri(Uri uri)
        {
            // We want to throw if we start getting messages from other Azure DevOps
            // organizations since currently Pipeline Witness is designed to only work
            // against our instance.
            return uri.Host == "dev.azure.com" && uri.PathAndQuery.StartsWith("/azure-sdk/");
        }

        private Guid GetCollectionIdFromProject(TeamProject project)
        {
            var url = ((ReferenceLink)project.Links.Links["web"]).Href;
            var collectionIdAsString = url.Split("/").Last();
            var collectionId = Guid.Parse(collectionIdAsString);
            return collectionId;
        }

        public async Task ProcessRunAsync(Uri runUri)
        {
            try
            {
                if (!IsValidAzureDevOpsUri(runUri))
                {
                    throw new ArgumentOutOfRangeException("Run URI does not point to the Azure SDK instance of Azure DevOps");
                }

                var runUriPath = runUri.AbsolutePath;
                var runUriPathSegments = runUri.PathAndQuery.Split("/");
                var organization = runUriPathSegments[1];
                var projectGuid = Guid.Parse(runUriPathSegments[2]);
                var pipelineId = int.Parse(runUriPathSegments[5]);
                var runId = int.Parse(runUriPathSegments[7]);

                var azureDevOpsPersonalAccessToken = await GetAzureDevOpsPersonalAccessTokenAsync();
                var credentials = new VssBasicCredential("nobody", azureDevOpsPersonalAccessToken);

                var baseUri = new Uri($"https://dev.azure.com/{organization}");
                var connection = new VssConnection(baseUri, credentials);

                var buildClient = connection.GetClient<BuildHttpClient>();
                var projectClient = connection.GetClient<ProjectHttpClient>();
                var organizationClient = connection.GetClient<OrganizationHttpClient>();

                var build = await buildClient.GetBuildAsync(projectGuid, runId);
                var project = await projectClient.GetProject(projectGuid.ToString());
                var pipeline = await buildClient.GetDefinitionAsync(project.Id, build.Definition.Id);
                var timeline = await buildClient.GetBuildTimelineAsync(projectGuid, runId);

                double agentDurationInSeconds = 0;
                double queueDurationInSeconds = 0;

                if (timeline != null)
                {
                    agentDurationInSeconds = (from record in timeline.Records
                                              where record.RecordType == "Task"
                                              where (record.Name == "Initialize job" || record.Name == "Finalize Job") // Love consistency in capitalization!
                                              group record by record.ParentId into job
                                              let jobStartTime = job.Min(jobRecord => jobRecord.StartTime)
                                              let jobFinishTime = job.Max(jobRecord => jobRecord.FinishTime)
                                              let agentDuration = jobFinishTime - jobStartTime
                                              select agentDuration.Value.TotalSeconds).Sum();

                    queueDurationInSeconds = (from taskRecord in timeline.Records
                                              where taskRecord.RecordType == "Task"
                                              where taskRecord.Name == "Initialize job"
                                              join jobRecord in timeline.Records on taskRecord.ParentId equals jobRecord.Id
                                              let queueDuration = taskRecord.StartTime - jobRecord.StartTime
                                              select queueDuration.Value.TotalSeconds).Sum();
                }

                var run = new Run()
                {
                    RunId = build.Id,
                    RunName = build.BuildNumber,
                    RunUrl = new Uri(((ReferenceLink)build.Links.Links["web"]).Href),
                    PipelineId = pipeline.Id,
                    PipelineName = pipeline.Name,
                    PipelineUrl = new Uri(((ReferenceLink)pipeline.Links.Links["web"]).Href),
                    ProjectId = build.Project.Id,
                    ProjectName = build.Project.Name,
                    ProjectUrl = new Uri(((ReferenceLink)project.Links.Links["web"]).Href),
                    RepositoryId = build.Repository.Id,
                    Reason = build.Reason switch
                    {
                        BuildReason.Manual => RunReason.Manual,
                        BuildReason.IndividualCI => RunReason.ContinuousIntegration,
                        BuildReason.Schedule => RunReason.Scheduled,
                        BuildReason.PullRequest => RunReason.PullRequest,
                        _ => RunReason.Other
                    },
                    State = build.Status switch
                    {
                        BuildStatus.None => RunStatus.None,
                        BuildStatus.Completed => RunStatus.Completed,
                        BuildStatus.Cancelling => RunStatus.Cancelling,
                        BuildStatus.InProgress => RunStatus.InProgress,
                        BuildStatus.NotStarted => RunStatus.NotStarted,
                        BuildStatus.Postponed => RunStatus.Postponed,
                        _ => RunStatus.None
                    },
                    Result = build.Result switch
                    {
                        BuildResult.Canceled => RunResult.Cancelled,
                        BuildResult.Failed => RunResult.Failed,
                        BuildResult.None => RunResult.None,
                        BuildResult.PartiallySucceeded => RunResult.PartiallySucceded,
                        BuildResult.Succeeded => RunResult.Succeeded,
                        _ => RunResult.None
                    },
                    GitReference = build.SourceBranch,
                    GitCommitSha = build.SourceVersion,
                    StartTime = build.StartTime.Value,
                    FinishTime = build.FinishTime.Value,
                    AgentDurationInSeconds = agentDurationInSeconds,
                    QueueDurationInSeconds = queueDurationInSeconds
                };

                var container = await GetItemContainerAsync("azure-pipelines-runs");
                await container.UpsertItemAsync(run);
            }
            catch (ContentNotFoundException ex)
            {
                logger.LogWarning(ex, "Run information was not found, possibly a PR run that was cancelled and removed?");
                return;
            }
        }

        private async Task<CosmosClient> GetCosmosClientAsync()
        {
            var websiteResourceGroupEnvironmentVariable = GetWebsiteResourceGroupEnvironmentVariable();
            var accountEndpoint = $"https://{websiteResourceGroupEnvironmentVariable}.documents.azure.com";
            var cosmosDbPrimaryAuthorizationKey = await GetCosmosDbPrimaryAuthorizationKeyAsync();
            var cosmosClient = new CosmosClient(accountEndpoint, cosmosDbPrimaryAuthorizationKey);
            return cosmosClient;
        }

        private async Task<CosmosContainer> GetItemContainerAsync(string containerName)
        {
            var client = await GetCosmosClientAsync();
            var database = client.GetDatabase("records");
            var container = client.GetContainer(database.Id, containerName);
            return container;
        }

        private async Task<string> GetCosmosDbPrimaryAuthorizationKeyAsync()
        {
            var cosmosDbPrimaryAuthorizationKey = await GetSecretAsync("cosmosdb-primary-authorization-key");
            return cosmosDbPrimaryAuthorizationKey;
        }


        private string GetWebsiteResourceGroupEnvironmentVariable()
        {
            logger.LogInformation("Fetching WEBSITE_RESOURCE_GROUP environment variable.");
            var websiteResourceGroupEnvironmentVariable = Environment.GetEnvironmentVariable("WEBSITE_RESOURCE_GROUP");
            logger.LogInformation("WEBSITE_RESOURCE_GROUP environemnt variable was: {websiteResourceGroupEnvironmentVariable}", websiteResourceGroupEnvironmentVariable);
            return websiteResourceGroupEnvironmentVariable;
        }

        private async Task<string> GetAzureDevOpsPersonalAccessTokenAsync()
        {
            logger.LogInformation("Fetching Azure DevOps personal access token from KeyVault.");
            var azureDevOpsPersonalAccessToken = await GetSecretAsync("azure-devops-personal-access-token");
            return azureDevOpsPersonalAccessToken;
        }

        private async Task<string> GetSecretAsync(string secretName)
        {
            var secretCacheKey = $"{secretName}_secretCacheKey";

            var client = GetSecretClient();
            var secret = await cache.GetOrCreateAsync(secretCacheKey, async (entry) =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
                KeyVaultSecret secret = await client.GetSecretAsync(secretName);
                return secret.Value;
            });

            return secret;
        }

        private SecretClient GetSecretClient()
        {
            var secretClientCacheKey = "secretClientCacheKey";

            var secretClient = cache.GetOrCreate(secretClientCacheKey, (entry) =>
            {
                var websiteResourceGroupEnvironmentVariable = GetWebsiteResourceGroupEnvironmentVariable();
                var uri = new Uri($"https://{websiteResourceGroupEnvironmentVariable}.vault.azure.net/");
                var credential = new DefaultAzureCredential();
                var client = new SecretClient(uri, credential);
                return client;
            });

            return secretClient;
        }
    }
}
