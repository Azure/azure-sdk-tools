using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;
using APIViewWeb.Repositories;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb;

public class CosmosVersionsRepository : ICosmosVersionsRepository
{
    private readonly Container _versionsContainer;

    public CosmosVersionsRepository(IConfiguration configuration, CosmosClient cosmosClient)
    {
        _versionsContainer = cosmosClient.GetContainer(configuration["CosmosDBName"], "APIVersions");
    }

    public async Task<APIVersionModel> GetVersionAsync(string reviewId, string versionId)
    {
        try
        {
            return await _versionsContainer.ReadItemAsync<APIVersionModel>(versionId, new PartitionKey(reviewId));
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IEnumerable<APIVersionModel>> GetVersionsAsync(string reviewId)
    {
        var queryDef = new QueryDefinition(
                "SELECT * FROM APIVersions v WHERE v.ReviewId = @reviewId AND v.IsDeleted = false")
            .WithParameter("@reviewId", reviewId);

        return await ExecuteQueryAsync(queryDef,
            new QueryRequestOptions { PartitionKey = new PartitionKey(reviewId) });
    }

    public async Task<APIVersionModel> GetVersionByIdentifierAsync(string reviewId, string versionIdentifier, VersionKind? kind = null)
    {
        var sql = "SELECT TOP 1 * FROM APIVersions v WHERE v.ReviewId = @reviewId AND v.VersionIdentifier = @versionIdentifier AND v.IsDeleted = false";
        if (kind.HasValue)
        {
            sql += " AND v.Kind = @kind";
        }

        var queryDef = new QueryDefinition(sql)
            .WithParameter("@reviewId", reviewId)
            .WithParameter("@versionIdentifier", versionIdentifier);
        if (kind.HasValue)
        {
            queryDef = queryDef.WithParameter("@kind", kind.Value.ToString());
        }

        List<APIVersionModel> results = await ExecuteQueryAsync(queryDef,
            new QueryRequestOptions { PartitionKey = new PartitionKey(reviewId) });
        return results.FirstOrDefault();
    }

    public async Task<APIVersionModel> GetVersionByPullRequestAsync(string reviewId, int pullRequestNumber)
    {
        var queryDef = new QueryDefinition(
                "SELECT TOP 1 * FROM APIVersions v WHERE v.ReviewId = @reviewId AND v.PullRequestNumber = @prNumber AND v.IsDeleted = false")
            .WithParameter("@reviewId", reviewId)
            .WithParameter("@prNumber", pullRequestNumber);

        List<APIVersionModel> results = await ExecuteQueryAsync(queryDef,
            new QueryRequestOptions { PartitionKey = new PartitionKey(reviewId) });
        return results.FirstOrDefault();
    }

    public async Task<IEnumerable<APIVersionModel>> GetVersionsAsync(string reviewId, VersionKind versionKind)
    {
        var queryDef = new QueryDefinition(
                "SELECT * FROM APIVersions v WHERE v.ReviewId = @reviewId AND v.Kind = @kind AND v.IsDeleted = false")
            .WithParameter("@reviewId", reviewId)
            .WithParameter("@kind", versionKind.ToString());

        return await ExecuteQueryAsync(queryDef,
            new QueryRequestOptions { PartitionKey = new PartitionKey(reviewId) });
    }

    public async Task<IEnumerable<APIVersionModel>> GetVersionsEligibleForSoftDeleteAsync(DateTime now)
    {
        var queryDef = new QueryDefinition(
                "SELECT * FROM APIVersions v WHERE IS_DEFINED(v.RetainUntil) AND v.RetainUntil != null AND v.RetainUntil <= @now AND v.IsDeleted = false")
            .WithParameter("@now", now);

        return await ExecuteQueryAsync(queryDef, requestOptions: null);
    }

    public async Task<IEnumerable<APIVersionModel>> GetVersionsEligibleForHardDeleteAsync(DateTime now)
    {
        var queryDef = new QueryDefinition(
                "SELECT * FROM APIVersions v WHERE IS_DEFINED(v.RetainUntil) AND v.RetainUntil != null AND v.RetainUntil <= @now AND v.IsDeleted = true")
            .WithParameter("@now", now);

        return await ExecuteQueryAsync(queryDef, requestOptions: null);
    }

    public async Task UpsertVersionAsync(APIVersionModel version)
    {
        version.LastUpdated = DateTime.UtcNow;
        await _versionsContainer.UpsertItemAsync(version, new PartitionKey(version.ReviewId));
    }

    public async Task DeleteVersionAsync(string versionId, string reviewId)
    {
        await _versionsContainer.DeleteItemAsync<APIVersionModel>(versionId, new PartitionKey(reviewId));
    }

    private async Task<List<APIVersionModel>> ExecuteQueryAsync(QueryDefinition queryDef,
        QueryRequestOptions requestOptions)
    {
        var results = new List<APIVersionModel>();
        var iterator = _versionsContainer.GetItemQueryIterator<APIVersionModel>(queryDef, requestOptions: requestOptions);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync();
            results.AddRange(page.Resource);
        }

        return results;
    }
}
