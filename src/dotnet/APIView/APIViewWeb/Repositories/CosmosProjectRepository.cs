using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using APIViewWeb.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace APIViewWeb.Repositories
{
    public class CosmosProjectRepository : ICosmosProjectRepository
    {
        private readonly Container _projectsContainer;
        private readonly ILogger<CosmosProjectRepository> _logger;

        public CosmosProjectRepository(IConfiguration configuration, CosmosClient cosmosClient,
            ILogger<CosmosProjectRepository> logger)
        {
            _projectsContainer = cosmosClient.GetContainer(configuration["CosmosDBName"], "Projects");
            _logger = logger;
        }

        public async Task UpsertProjectAsync(Project project)
        {
            project.LastUpdatedOn = DateTime.UtcNow;
            await _projectsContainer.UpsertItemAsync(project, new PartitionKey(project.Id));
        }

        public async Task<Project> GetProjectAsync(string projectId)
        {
            try
            {
                return await _projectsContainer.ReadItemAsync<Project>(projectId, new PartitionKey(projectId));
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogDebug("Project {ProjectId} not found", projectId);
                return null;
            }
        }

        public async Task<Project> GetProjectByCrossLanguagePackageIdAsync(string crossLanguagePackageId)
        {
            var queryDefinition = new QueryDefinition(
                    "SELECT * FROM Projects p WHERE LOWER(p.CrossLanguagePackageId) = LOWER(@crossLanguagePackageId) AND p.IsDeleted = false")
                .WithParameter("@crossLanguagePackageId", crossLanguagePackageId);

            var itemQueryIterator = _projectsContainer.GetItemQueryIterator<Project>(queryDefinition);
            if (itemQueryIterator.HasMoreResults)
            {
                var result = await itemQueryIterator.ReadNextAsync();
                return result.Resource.FirstOrDefault();
            }

            return null;
        }

        public async Task<Project> GetProjectByExpectedPackageAsync(string language, string packageName)
        {
            string keyAzure   = $"{language}:azure";
            string keyAzurev2 = $"{language}:azurev2";
            var queryDefinition = new QueryDefinition(
                    "SELECT * FROM Projects p WHERE" +
                    " (" +
                    "  (IS_DEFINED(p.ExpectedPackages[@key])         AND LOWER(p.ExpectedPackages[@key].PackageName)         = LOWER(@packageName))" +
                    "  OR (IS_DEFINED(p.ExpectedPackages[@keyAzure])   AND LOWER(p.ExpectedPackages[@keyAzure].PackageName)   = LOWER(@packageName))" +
                    "  OR (IS_DEFINED(p.ExpectedPackages[@keyAzurev2]) AND LOWER(p.ExpectedPackages[@keyAzurev2].PackageName) = LOWER(@packageName))" +
                    " ) AND p.IsDeleted = false")
                .WithParameter("@key",        language)
                .WithParameter("@keyAzure",   keyAzure)
                .WithParameter("@keyAzurev2", keyAzurev2)
                .WithParameter("@packageName", packageName);

            var itemQueryIterator = _projectsContainer.GetItemQueryIterator<Project>(queryDefinition);
            if (itemQueryIterator.HasMoreResults)
            {
                var result = await itemQueryIterator.ReadNextAsync();
                return result.Resource.FirstOrDefault();
            }

            return null;
        }
    }
}
