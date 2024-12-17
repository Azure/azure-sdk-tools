using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.ServiceEndpoints.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ServiceEndpoint = Microsoft.VisualStudio.Services.ServiceEndpoints.WebApi.ServiceEndpoint;

namespace PipelineGenerator
{
    public class PipelineGenerationContext
    {
        private string organization;
        private string project;
        private string endpoint;
        private string agentPool;
        private int[] variableGroups;
        private string devOpsPath;

        public PipelineGenerationContext(
            ILogger logger,
            string organization,
            string project,
            string endpoint,
            string repository,
            string branch,
            string agentPool,
            int[] variableGroups,
            string devOpsPath,
            string prefix,
            bool whatIf,
            bool noSchedule,
            bool setManagedVariables,
            bool overwriteTriggers)
        {
            this.logger = logger;
            this.organization = organization;
            this.project = project;
            this.endpoint = endpoint;
            this.Repository = repository;
            this.Branch = branch;
            this.agentPool = agentPool;
            this.variableGroups = variableGroups;
            this.devOpsPath = devOpsPath;
            this.Prefix = prefix;
            this.WhatIf = whatIf;
            this.NoSchedule = noSchedule;
            this.SetManagedVariables = setManagedVariables;
            this.OverwriteTriggers = overwriteTriggers;
        }

        public string Repository { get; }
        public string Branch { get; }
        public string Prefix { get; }
        public bool WhatIf { get; }
        public bool NoSchedule { get; }
        public bool OverwriteTriggers { get; }
        public bool SetManagedVariables { get; set; }
        public int[] VariableGroups => this.variableGroups;
        public string DevOpsPath => string.IsNullOrEmpty(this.devOpsPath) ? Prefix : this.devOpsPath;

        private VssConnection cachedConnection;

        private TokenCredential GetAzureCredentials()
        {
            return new ChainedTokenCredential(
                new AzureCliCredential(),
                new AzurePowerShellCredential()
            );
        }

        private async Task<VssConnection> GetConnectionAsync()
        {
            if (cachedConnection == null)
            {
                var azureCredential = GetAzureCredentials();
                var devopsCredential = new VssAzureIdentityCredential(azureCredential);
                cachedConnection = new VssConnection(new Uri(organization), devopsCredential);
                await cachedConnection.ConnectAsync();
            }

            return cachedConnection;
        }

        private ProjectHttpClient cachedProjectClient;

        private async Task<ProjectHttpClient> GetProjectClientAsync(CancellationToken cancellationToken)
        {
            if (cachedProjectClient == null)
            {
                var connection = await GetConnectionAsync();
                cachedProjectClient = await connection.GetClientAsync<ProjectHttpClient>(cancellationToken);
            }

            return cachedProjectClient;
        }

        private TeamProjectReference cachedProjectReference;

        public async Task<TeamProjectReference> GetProjectReferenceAsync(CancellationToken cancellationToken)
        {
            if (cachedProjectReference == null)
            {
                var projectClient = await GetProjectClientAsync(cancellationToken);

                this.logger.LogDebug("Getting projects from projectClient");

                var projects = await projectClient.GetProjects(ProjectState.WellFormed);

                this.logger.LogDebug("projectClient returned {Count} projects", projects.Count);

                cachedProjectReference = projects.Single(p => p.Name.Equals(project, StringComparison.OrdinalIgnoreCase));

                this.logger.LogDebug("Cached project {Name} with id {Id}", cachedProjectReference.Name, cachedProjectReference.Id);
            }

            return cachedProjectReference;
        }

        private ServiceEndpointHttpClient cachedServiceEndpointClient;

        public async Task<ServiceEndpointHttpClient> GetServiceEndpointClientAsync(CancellationToken cancellationToken)
        {
            if (cachedServiceEndpointClient == null)
            {
                var connection = await GetConnectionAsync();
                cachedServiceEndpointClient = await connection.GetClientAsync<ServiceEndpointHttpClient>(cancellationToken);
            }

            return cachedServiceEndpointClient;
        }

        private Microsoft.VisualStudio.Services.ServiceEndpoints.WebApi.ServiceEndpoint cachedServiceEndpoint;

        public async Task<ServiceEndpoint> GetServiceEndpointAsync(CancellationToken cancellationToken)
        {
            if (cachedServiceEndpoint == null)
            {
                var serviceEndpointClient = await GetServiceEndpointClientAsync(cancellationToken);
                var projectReference = await GetProjectReferenceAsync(cancellationToken);

                this.logger.LogDebug("Getting service endpoints from serviceEndpointClient with endpoint name {EndpointName}", endpoint);

                var serviceEndpoints = await serviceEndpointClient.GetServiceEndpointsByNamesAsync(
                    projectReference.Id.ToString(),
                    new [] { endpoint },
                    cancellationToken: cancellationToken
                    );

                this.logger.LogDebug("serviceEndpointClient returned {Count} service endpoints", serviceEndpoints.Count);

                cachedServiceEndpoint = serviceEndpoints.First();

                this.logger.LogDebug("Cached service endpoint {Name} with id {Id}", cachedServiceEndpoint.Name, cachedServiceEndpoint.Id);
            }

            return cachedServiceEndpoint;
        }

        public async Task<IEnumerable<ServiceEndpoint>> GetServiceConnectionsAsync(IEnumerable<string> serviceConnections, CancellationToken cancellationToken)
        {
            var serviceEndpointClient = await GetServiceEndpointClientAsync(cancellationToken);
            var projectReference = await GetProjectReferenceAsync(cancellationToken);

            var allServiceConnections = await serviceEndpointClient.GetServiceEndpointsAsync(projectReference.Id.ToString(), cancellationToken: cancellationToken);

            this.logger.LogDebug("Returned a total of {Count} service endpoints", allServiceConnections.Count);
            
            List<ServiceEndpoint> endpoints = new List<ServiceEndpoint>();
            foreach (var serviceConnection in allServiceConnections)
            {
                if (serviceConnections.Contains(serviceConnection.Name))
                {
                    endpoints.Add(serviceConnection);
                }
            }
            return endpoints;
        }

        private HttpClient cachedRawHttpClient = null;

        private async Task<HttpClient> GetRawHttpClient(CancellationToken cancellationToken)
        {
            if (this.cachedRawHttpClient == null)
            {
                var credential = GetAzureCredentials();
                // Get token for Azure DevOps
                var tokenRequestContext = new TokenRequestContext(new[] { "499b84ac-1321-427f-aa17-267ca6975798/.default" });
                var accessToken = await credential.GetTokenAsync(tokenRequestContext, cancellationToken);
                var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
                this.cachedRawHttpClient = client;
            }
            return this.cachedRawHttpClient;
        }

        private string GetPipelinePermissionsUrlForServiceConnections(Guid serviceConnectionId)
        {
            var apiVersion = "7.1-preview.1";
            // https://learn.microsoft.com/en-us/rest/api/azure/devops/approvalsandchecks/pipeline-permissions/update-pipeline-permisions-for-resource?view=azure-devops-rest-7.1&tabs=HTTP
            return $"{this.organization}/{this.project}/_apis/pipelines/pipelinepermissions/endpoint/{serviceConnectionId}?api-version={apiVersion}";
        }
        
        public async Task<JsonNode> GetPipelinePermissionsAsync(Guid serviceConnectionId, CancellationToken cancellationToken)
        {
            var url = GetPipelinePermissionsUrlForServiceConnections(serviceConnectionId);
            var client = await GetRawHttpClient(cancellationToken);
            var response = await client.GetAsync(url, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return JsonNode.Parse(await response.Content.ReadAsStringAsync());
            }
            else
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new Exception($"GetPipelinePermissionsAsync throw an error [{response.StatusCode}]: {responseContent}");
            }
        }

        public async Task UpdatePipelinePermissionsAsync(Guid serviceConnectionId, JsonNode serviceConnectionPermissions, CancellationToken cancellationToken)
        {
            var url = GetPipelinePermissionsUrlForServiceConnections(serviceConnectionId);
            var client = await GetRawHttpClient(cancellationToken);
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
            { 
                Content = new StringContent(serviceConnectionPermissions.ToString(), Encoding.UTF8, "application/json")
            };

            var response = await client.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new Exception($"UpdatePipelinePermissionsAsync throw an error [{response.StatusCode}]: {responseContent}");
            }
        }

        private BuildHttpClient cachedBuildClient;

        public async Task<BuildHttpClient> GetBuildHttpClientAsync(CancellationToken cancellationToken)
        {
            if (cachedBuildClient == null)
            {
                var connection = await GetConnectionAsync();
                cachedBuildClient = await connection.GetClientAsync<BuildHttpClient>(cancellationToken);
            }

            return cachedBuildClient;
        }

        private TaskAgentHttpClient cachedTaskAgentClient;

        private async Task<TaskAgentHttpClient> GetTaskAgentClientAsync(CancellationToken cancellationToken)
        {
            if (cachedTaskAgentClient == null)
            {
                var connection = await GetConnectionAsync();
                cachedTaskAgentClient = await connection.GetClientAsync<TaskAgentHttpClient>(cancellationToken);
            }

            return cachedTaskAgentClient;
        }

        private AgentPoolQueue cachedAgentPoolQueue;
        private readonly ILogger logger;

        public async Task<AgentPoolQueue> GetAgentPoolQueue(CancellationToken cancellationToken)
        {
            if (cachedAgentPoolQueue == null)
            {
                var projectReference = await GetProjectReferenceAsync(cancellationToken);
                var taskAgentClient = await GetTaskAgentClientAsync(cancellationToken);

                this.logger.LogDebug("Getting agent queues from taskAgentClient with queue name {QueueName}", agentPool);

                var agentQueues = await taskAgentClient.GetAgentQueuesAsync(
                    project: projectReference.Id,
                    queueName: agentPool,
                    cancellationToken: cancellationToken
                    );

                this.logger.LogDebug("taskAgentClient returned {Count} agent queues", agentQueues.Count);

                cachedAgentPoolQueue = new AgentPoolQueue()
                {
                    Id = agentQueues.First().Id
                };

                this.logger.LogDebug("Cached agent queue with id {Id}", cachedAgentPoolQueue.Id);
            }

            return cachedAgentPoolQueue;
        }
    }
}
