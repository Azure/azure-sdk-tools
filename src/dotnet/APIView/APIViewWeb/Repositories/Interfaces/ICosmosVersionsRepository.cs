using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;

namespace APIViewWeb.Repositories;

public interface ICosmosVersionsRepository
{
    Task<APIVersionModel> GetVersionAsync(string reviewId, string versionId);
    Task<IEnumerable<APIVersionModel>> GetVersionsAsync(string reviewId);
    Task<APIVersionModel> GetVersionByIdentifierAsync(string reviewId, string versionIdentifier);
    Task<APIVersionModel> GetVersionByPullRequestAsync(string reviewId, int pullRequestNumber);
    Task<IEnumerable<APIVersionModel>> GetVersionsAsync(string reviewId, VersionKind versionKind);
    Task<IEnumerable<APIVersionModel>> GetVersionsEligibleForRetentionAsync(DateTime now);
    Task UpsertVersionAsync(APIVersionModel version);
    Task DeleteVersionAsync(string versionId, string reviewId);
}
