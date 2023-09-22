using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.ServiceEndpoints.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PipelineGenerator
{
    public class PipelineGenerationContext
    {
        private string organization;
        private string project;
        private string patvar;
        private string endpoint;
        private string agentPool;
        private int[] variableGroups;
        private string devOpsPath;

        public PipelineGenerationContext(
            ILogger logger,
            string organization,
            string project,
            string patvar,
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
            this.patvar = patvar;
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

        private async Task<VssConnection> GetConnectionAsync()
        {
            if (cachedConnection == null)
            {
                VssCredentials credentials;
                if (string.IsNullOrWhiteSpace(patvar))
                {
                    var azureTokenProvider = new AzureServiceTokenProvider();
                    var authenticationResult = await azureTokenProvider.GetAuthenticationResultAsync("499b84ac-1321-427f-aa17-267ca6975798");
                    credentials = new VssAadCredential(new VssAadToken(authenticationResult.TokenType, authenticationResult.AccessToken));
                }
                else
                {
                    var pat = Environment.GetEnvironmentVariable(patvar);
                    credentials = new VssBasicCredential("nobody", pat);
                }

                cachedConnection = new VssConnection(new Uri(organization), credentials);
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

        public async Task<Microsoft.VisualStudio.Services.ServiceEndpoints.WebApi.ServiceEndpoint> GetServiceEndpointAsync(CancellationToken cancellationToken)
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
