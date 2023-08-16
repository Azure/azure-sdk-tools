using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using ApiView;
using APIView.DIff;
using APIView.Model;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using APIViewWeb.Models;

namespace APIViewWeb.Managers
{
    public interface IReviewManager
    {
        public Task<ReviewModel> CreateReviewAsync(ClaimsPrincipal user, string originalName, string label, Stream fileStream, bool runAnalysis, string langauge, bool awaitComputeDiff = false);
        public Task<IEnumerable<ReviewModel>> GetReviewsAsync(bool closed, string language, string packageName = null, ReviewType filterType = ReviewType.Manual);
        public Task<IEnumerable<ReviewModel>> GetReviewsAsync(string ServiceName, string PackageName, IEnumerable<ReviewType> filterTypes);
        public Task<IEnumerable<string>> GetReviewPropertiesAsync(string propertyName);
        public Task<IEnumerable<ReviewModel>> GetRequestedReviews(string userName);
        public Task<(IEnumerable<ReviewModel> Reviews, int TotalCount, int TotalPages, int CurrentPage, int? PreviousPage, int? NextPage)> GetPagedReviewsAsync(
            IEnumerable<string> search, IEnumerable<string> languages, bool? isClosed, IEnumerable<int> filterTypes, bool? isApproved, int offset, int limit, string orderBy);
        public Task DeleteReviewAsync(ClaimsPrincipal user, string id);
        public Task<ReviewModel> GetReviewAsync(ClaimsPrincipal user, string id);
        public Task AddRevisionAsync(ClaimsPrincipal user, string reviewId, string name, string label, Stream fileStream, string language = "", bool awaitComputeDiff = false);
        public Task<CodeFile> CreateCodeFile(string originalName, Stream fileStream, bool runAnalysis, MemoryStream memoryStream, string language = null);
        public Task<ReviewCodeFileModel> CreateReviewCodeFileModel(string revisionId, MemoryStream memoryStream, CodeFile codeFile);
        public Task DeleteRevisionAsync(ClaimsPrincipal user, string id, string revisionId);
        public Task UpdateRevisionLabelAsync(ClaimsPrincipal user, string id, string revisionId, string label);
        public Task ToggleIsClosedAsync(ClaimsPrincipal user, string id);
        public Task ToggleApprovalAsync(ClaimsPrincipal user, string id, string revisionId);
        public Task ApprovePackageNameAsync(ClaimsPrincipal user, string id);
        public Task<bool> IsReviewSame(ReviewRevisionModel revision, RenderedCodeFile renderedCodeFile);
        public Task<ReviewRevisionModel> CreateMasterReviewAsync(ClaimsPrincipal user, string originalName, string label, Stream fileStream, bool compareAllRevisions);
        public Task UpdateReviewBackground(HashSet<string> updateDisabledLanguages, int backgroundBatchProcessCount);
        public Task<CodeFile> GetCodeFile(string repoName, string buildId, string artifactName, string packageName, string originalFileName, string codeFileName,
            MemoryStream originalFileStream, string baselineCodeFileName = "", MemoryStream baselineStream = null, string project = "public");
        public Task<ReviewRevisionModel> CreateApiReview(ClaimsPrincipal user, string buildId, string artifactName, string originalFileName, string label,
            string repoName, string packageName, string codeFileName, bool compareAllRevisions, string project);
        public Task AutoArchiveReviews(int archiveAfterMonths);
        public Task UpdateReviewCodeFiles(string repoName, string buildId, string artifact, string project);
        public Task RequestApproversAsync(ClaimsPrincipal User, string ReviewId, HashSet<string> reviewers);
        public Task GetLineNumbersOfHeadingsOfSectionsWithDiff(string reviewId, ReviewRevisionModel revision);
        public TreeNode<InlineDiffLine<CodeLine>> ComputeSectionDiff(TreeNode<CodeLine> before, TreeNode<CodeLine> after, RenderedCodeFile beforeFile, RenderedCodeFile afterFile);
        public Task<bool> IsApprovedForFirstRelease(string language, string packageName);
        public Task<int> GenerateAIReview(string reviewId, string revisionId);

        /// <summary>
        /// Retrieve Reviews from the Reviews container in CosmosDb after applying filter to the query.
        /// Uses lean reviewListModels to reduce the size of the return. Used for ClientSPA
        /// </summary>
        /// <param name="pageParams"></param> Contains paginationinfo
        /// <returns>PagedList<ReviewsListItemModel></returns>
        public Task<PagedList<ReviewsListItemModel>> GetReviewsAsync(PageParams pageParams);
    }
}
