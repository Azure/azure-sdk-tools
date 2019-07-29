using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
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
        private string repository;
        private string agentPool;
        private int[] variableGroups;
        private string devOpsPath;

        public PipelineGenerationContext(
            string organization, 
            string project, 
            string patvar, 
            string endpoint, 
            string repository, 
            string branch, 
            string agentPool, 
            string[] variableGroups,
            string devOpsPath,
            string prefix, 
            bool whatIf)
        {
            this.organization = organization;
            this.project = project;
            this.patvar = patvar;
            this.endpoint = endpoint;
            this.repository = repository;
            this.Branch = branch;
            this.agentPool = agentPool;
            this.variableGroups = ParseIntArray(variableGroups);
            this.devOpsPath = devOpsPath;
            this.Prefix = prefix;
            this.WhatIf = whatIf;
        }

        public string Branch { get; }
        public string Prefix { get; }
        public bool WhatIf { get; }
        public int[] VariableGroups => this.variableGroups;
        public string DevOpsPath => this.devOpsPath;

        private int[] ParseIntArray(string[] strs) 
            => strs.Select(str => int.Parse(str)).ToArray();

        private VssConnection cachedConnection;

        private VssConnection GetConnection()
        {
            if (cachedConnection == null)
            {
                var pat = Environment.GetEnvironmentVariable(patvar);
                var credentials = new VssBasicCredential("nobody", pat);
                cachedConnection = new VssConnection(new Uri(organization), credentials);
            }

            return cachedConnection;
        }

        private ProjectHttpClient cachedProjectClient;

        private async Task<ProjectHttpClient> GetProjectClientAsync(CancellationToken cancellationToken)
        {
            if (cachedProjectClient == null)
            {
                var connection = GetConnection();
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
                var projects = await projectClient.GetProjects(ProjectState.WellFormed);
                cachedProjectReference = projects.Single(p => p.Name.Equals(project, StringComparison.OrdinalIgnoreCase));
            }

            return cachedProjectReference;
        }

        private ServiceEndpointHttpClient cachedSeviceEndpointClient;

        private async Task<ServiceEndpointHttpClient> GetServiceEndpointClientAsync(CancellationToken cancellationToken)
        {
            if (cachedSeviceEndpointClient == null)
            {
                var connection = GetConnection();
                cachedSeviceEndpointClient = await connection.GetClientAsync<ServiceEndpointHttpClient>(cancellationToken);
            }

            return cachedSeviceEndpointClient;
        }

        private Microsoft.VisualStudio.Services.ServiceEndpoints.WebApi.ServiceEndpoint cachedServiceEndpoint;

        private async Task<Microsoft.VisualStudio.Services.ServiceEndpoints.WebApi.ServiceEndpoint> GetServiceEndpointAsync(CancellationToken cancellationToken)
        {
            if (cachedServiceEndpoint == null)
            {
                var serviceEndpointClient = await GetServiceEndpointClientAsync(cancellationToken);
                var projectReference = await GetProjectReferenceAsync(cancellationToken);
                var serviceEndpoints = await serviceEndpointClient.GetServiceEndpointsByNamesAsync(
                    projectReference.Id.ToString(),
                    new [] { endpoint },
                    cancellationToken: cancellationToken
                    );
                cachedServiceEndpoint = serviceEndpoints.First();
            }

            return cachedServiceEndpoint;
        }

        private BuildHttpClient cachedBuildClient;

        public async Task<BuildHttpClient> GetBuildHttpClientAsync(CancellationToken cancellationToken)
        {
            if (cachedBuildClient == null)
            {
                var connection = GetConnection();
                cachedBuildClient = await connection.GetClientAsync<BuildHttpClient>(cancellationToken);
            }

            return cachedBuildClient;
        }

        private SourceRepository cachedSourceRepository;

        public async Task<SourceRepository> GetSourceRepositoryAsync(CancellationToken cancellationToken)
        {
            if (cachedSourceRepository == null)
            {
                var buildClient = await GetBuildHttpClientAsync(cancellationToken);
                var projectReference = await GetProjectReferenceAsync(cancellationToken);
                var serviceEndpoint = await GetServiceEndpointAsync(cancellationToken);

                var sourceRepositories = await buildClient.ListRepositoriesAsync(
                    projectReference.Id,
                    "github",
                    serviceEndpointId: serviceEndpoint.Id,
                    repository: repository,
                    cancellationToken: cancellationToken
                );
                cachedSourceRepository = sourceRepositories.Repositories.Single();
            }

            return cachedSourceRepository;
        }

        private TaskAgentHttpClient cachedTaskAgentClient;

        private async Task<TaskAgentHttpClient> GetTaskAgentClientAsync(CancellationToken cancellationToken)
        {
            if (cachedTaskAgentClient == null)
            {
                var connection = GetConnection();
                cachedTaskAgentClient = await connection.GetClientAsync<TaskAgentHttpClient>(cancellationToken);
            }

            return cachedTaskAgentClient;
        }

        private AgentPoolQueue cachedAgentPoolQueue;

        public async Task<AgentPoolQueue> GetAgentPoolQueue(CancellationToken cancellationToken)
        {
            if (cachedAgentPoolQueue == null)
            {
                var projectReference = await GetProjectReferenceAsync(cancellationToken);
                var taskAgentClient = await GetTaskAgentClientAsync(cancellationToken);
                var agentQueues = await taskAgentClient.GetAgentQueuesAsync(
                    project: projectReference.Id,
                    queueName: agentPool,
                    cancellationToken: cancellationToken
                    );

                cachedAgentPoolQueue = new AgentPoolQueue()
                {
                    Id = agentQueues.First().Id
                };
            }

            return cachedAgentPoolQueue;
        }
    }
}
