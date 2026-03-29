using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;

namespace APIViewWeb.Repositories
{
    public interface ICosmosAPIRevisionsRepository
    {
        /// <summary>
        /// Add new ReviewRevision
        /// </summary>
        /// <param name="apiRevision"></param>
        /// <returns></returns>
        public Task UpsertAPIRevisionAsync(APIRevisionListItemModel apiRevision);

        /// <summary>
        /// Retrieve Revisions from the Revisions container in CosmosDb after applying filter to the query
        /// Used for ClientSPA
        /// </summary>
        /// <param name="user"></param>
        /// <param name="pageParams"></param> Contains paginationinfo
        /// <param name="filterAndSortParams"></param> Contains filter and sort parameters
        /// <returns></returns>
        public Task<PagedList<APIRevisionListItemModel>> GetAPIRevisionsAsync(ClaimsPrincipal user, PageParams pageParams, FilterAndSortParams filterAndSortParams);

        /// <summary>
        /// Retrieve Revisions from the Revisions container in CosmosDb for a given reviewId
        /// </summary>
        /// <param name="reviewId"></param> The reviewId
        /// <returns></returns>
        public Task<IEnumerable<APIRevisionListItemModel>> GetAPIRevisionsAsync(string reviewId);

        /// <summary>
        /// Retrieve Revisions from the APIRevisions container in CosmosDb for a given crossLanguageId and language
        /// </summary>
        /// <param name="crossLanguageId"></param> The identifier used to group related revisions across different programming languages.
        /// <param name="language"></param> The reviewId
        /// <param name="apiRevisionType"></param>
        /// <returns></returns>
        public Task<IEnumerable<APIRevisionListItemModel>> GetCrossLanguageAPIRevisionsAsync(string crossLanguageId, string language, APIRevisionType apiRevisionType = APIRevisionType.All);

        /// <summary>
        /// Retrieve Revisions from the Revisions container in CosmosDb
        /// </summary>
        /// <param name="apiRevisionId"></param> The revisionId
        /// <returns></returns>
        public Task<APIRevisionListItemModel> GetAPIRevisionAsync(string apiRevisionId);
        /// <summary>
        /// Get Revisions by LastUpdatedOn Date
        /// </summary>
        /// <param name="lastUpdatedOn"></param>
        /// <param name="apiRevisionType"></param>
        /// <returns></returns>
        public Task<IEnumerable<APIRevisionListItemModel>> GetAPIRevisionsAsync(DateTime lastUpdatedOn, APIRevisionType apiRevisionType = APIRevisionType.All);
        /// <summary>
        /// Get APIRevisions assigned to a user for review
        /// </summary>
        /// <param name="userName"></param>
        /// <returns></returns>
        public Task<IEnumerable<APIRevisionListItemModel>> GetAPIRevisionsAssignedToUser(string userName);
        /// <summary>
        /// Get ReviewIds for review that are linked by crossLanguagePackageId
        /// </summary>
        /// <param name="crossLanguagePackageId"></param>
        /// <returns></returns>
        public Task<IEnumerable<string>> GetReviewIdsOfLanguageCorrespondingReviewAsync(string crossLanguagePackageId);

        /// <summary>
        /// Get soft-deleted revisions that have been deleted before a specific date
        /// </summary>
        /// <param name="deletedBefore">Date before which revisions should have been soft-deleted</param>
        /// <param name="apiRevisionType">Type of revisions to retrieve (Manual, PullRequest, etc.)</param>
        /// <returns></returns>
        public Task<IEnumerable<APIRevisionListItemModel>> GetSoftDeletedAPIRevisionsAsync(DateTime deletedBefore, APIRevisionType apiRevisionType = APIRevisionType.All);

        /// <summary>
        /// Hard delete an API revision from Cosmos DB
        /// </summary>
        /// <param name="apiRevisionId">The ID of the revision to delete</param>
        /// <param name="reviewId">The review ID (partition key)</param>
        /// <returns></returns>
        public Task DeleteAPIRevisionAsync(string apiRevisionId, string reviewId);
    }
}
