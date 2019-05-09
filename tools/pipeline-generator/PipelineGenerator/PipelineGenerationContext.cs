using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
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
        private string[] variableGroups;

        public PipelineGenerationContext(string organization, string project, string patvar, string endpoint, string repository, string[] variableGroups)
        {
            this.organization = organization;
            this.project = project;
            this.patvar = patvar;
            this.endpoint = endpoint;
            this.repository = repository;
            this.variableGroups = variableGroups;
        }

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
       
        private async Task<TeamProjectReference> GetProjectReferenceAsync(CancellationToken cancellationToken)
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

        private ServiceEndpoint cachedServiceEndpoint;

        private async Task<ServiceEndpoint> GetServiceEndpointAsync(CancellationToken cancellationToken)
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

        private async Task<BuildHttpClient> GetBuildHttpClientAsync(CancellationToken cancellationToken)
        {
            if (cachedBuildClient == null)
            {
                var connection = GetConnection();
                cachedBuildClient = await connection.GetClientAsync<BuildHttpClient>(cancellationToken);
            }

            return cachedBuildClient;
        }

        private SourceRepository cachedSourceRepository;

        private async Task<SourceRepository> GetSourceRepositoryAsync(CancellationToken cancellationToken)
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
    }
}
