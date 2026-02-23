using APIView.DIff;
using ApiView;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using APIViewWeb.Models;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using APIView.Model;
using Microsoft.AspNetCore.Http;

namespace APIViewWeb.Managers.Interfaces
{
    public interface IAPIRevisionsManager
    {
        public Task<IEnumerable<APIRevisionListItemModel>> GetAPIRevisionsAsync(string reviewId, string packageVersion = "", APIRevisionType apiRevisionType = APIRevisionType.All);
        public Task<PagedList<APIRevisionListItemModel>> GetAPIRevisionsAsync(ClaimsPrincipal user, PageParams pageParams, FilterAndSortParams filterAndSortParams);
        public Task<APIRevisionListItemModel> GetLatestAPIRevisionsAsync(string reviewId = null, IEnumerable<APIRevisionListItemModel> apiRevisions = null, APIRevisionType apiRevisionType = APIRevisionType.All);
        public Task<IEnumerable<APIRevisionListItemModel>> GetCrossLanguageAPIRevisionsAsync(string crossLanguageId, string language, APIRevisionType apiRevisionType = APIRevisionType.All);
        public Task<APIRevisionListItemModel> GetAPIRevisionAsync(ClaimsPrincipal user, string apiRevisionId);
        public Task<APIRevisionListItemModel> GetAPIRevisionAsync(string apiRevisionId);
        public APIRevisionListItemModel GetNewAPIRevisionAsync(APIRevisionType apiRevisionType, string reviewId = null, string packageName = null, string language = null,
            string label = null, int? prNumber = null, string createdBy = ApiViewConstants.AzureSdkBotName, string sourceBranch = null);
        public Task<(bool updateReview, APIRevisionListItemModel apiRevision)> ToggleAPIRevisionApprovalAsync(ClaimsPrincipal user, string id, string revisionId = null, APIRevisionListItemModel apiRevision = null, string notes = "", string approver = "");
        public Task CarryForwardRevisionDataAsync(APIRevisionListItemModel targetRevision, APIRevisionListItemModel sourceRevision);
        public Task<APIRevisionListItemModel> AddAPIRevisionAsync(ClaimsPrincipal user, string reviewId, APIRevisionType apiRevisionType, string name, string label, Stream fileStream, string language = "", bool awaitComputeDiff = false);
        public Task<APIRevisionListItemModel> CreateAPIRevisionAsync(ClaimsPrincipal user, ReviewListItemModel review, IFormFile file, string filePath, string language, string label);
        public Task<APIRevisionListItemModel> AddAPIRevisionAsync(ClaimsPrincipal user, ReviewListItemModel review, APIRevisionType apiRevisionType, string name, string label, Stream fileStream, string language, bool awaitComputeDiff = false);
        public Task RunAPIRevisionGenerationPipeline(List<APIRevisionGenerationPipelineParamModel> reviewGenParams, string language);
        public Task SoftDeleteAPIRevisionAsync(ClaimsPrincipal user, string reviewId, string revisionId);
        public Task SoftDeleteAPIRevisionAsync(ClaimsPrincipal user, APIRevisionListItemModel apiRevision);
        public Task SoftDeleteAPIRevisionAsync(APIRevisionListItemModel apiRevision, string userName = ApiViewConstants.AzureSdkBotName, string notes = "");
        public Task RestoreAPIRevisionAsync(ClaimsPrincipal user, string reviewId, string revisionId);
        public Task UpdateAPIRevisionLabelAsync(ClaimsPrincipal user, string revisionId, string label);
        public Task<bool> AreAPIRevisionsTheSame(APIRevisionListItemModel apiRevision, RenderedCodeFile renderedCodeFile, bool considerPackageVersion = false);
        public Task UpdateAPIRevisionCodeFileAsync(string repoName, string buildId, string artifact, string project);
        public Task GetLineNumbersOfHeadingsOfSectionsWithDiff(string reviewId, APIRevisionListItemModel apiRevision, IEnumerable<APIRevisionListItemModel> apiRevisions = null);
        public TreeNode<InlineDiffLine<CodeLine>> ComputeSectionDiff(TreeNode<CodeLine> before, TreeNode<CodeLine> after, RenderedCodeFile beforeFile, RenderedCodeFile afterFile);
        public Task<APIRevisionListItemModel> CreateAPIRevisionAsync(string userName, string reviewId, APIRevisionType apiRevisionType, string label,
            MemoryStream memoryStream, CodeFile codeFile, string originalName = null, int? prNumber = null, string sourceBranch = null);
        public Task UpdateAPIRevisionAsync(APIRevisionListItemModel revision, LanguageService languageService, bool verifyUpgradabilityOnly);
        public Task UpdateAPIRevisionAsync(APIRevisionListItemModel revision);
        public Task AutoArchiveAPIRevisions(int archiveAfterMonths);
        public Task AutoPurgeAPIRevisions(int purgeAfterMonths);
        public Task AssignReviewersToAPIRevisionAsync(ClaimsPrincipal User, string apiRevisionId, HashSet<string> reviewers);
        public Task<IEnumerable<APIRevisionListItemModel>> GetAPIRevisionsAssignedToUser(string userName);
        public Task<APIRevisionListItemModel> UpdateRevisionMetadataAsync(APIRevisionListItemModel revision, string packageVersion, string label, bool setReleaseTag = false);
        public Task<IEnumerable<string>> GetReviewIdsOfLanguageCorrespondingReviewAsync(string crossLanguagePackageId);
        public Task<APIRevisionListItemModel> UpdateAPIRevisionReviewersAsync(ClaimsPrincipal User, string apiRevisionId, HashSet<string> reviewers);
        public Task<string> GetOutlineAPIRevisionsAsync(string activeApiRevisionId);
        public Task<string> GetApiRevisionText(APIRevisionListItemModel activeApiRevision);
    }
}
