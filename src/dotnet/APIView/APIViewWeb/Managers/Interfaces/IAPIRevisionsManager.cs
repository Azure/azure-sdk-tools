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
using Microsoft.ApplicationInsights;

namespace APIViewWeb.Managers.Interfaces
{
    public interface IAPIRevisionsManager
    {
        public Task<PagedList<APIRevisionListItemModel>> GetAPIRevisionsAsync(PageParams pageParams, APIRevisionsFilterAndSortParams filterAndSortParams);
        public Task<IEnumerable<APIRevisionListItemModel>> GetAPIRevisionsAsync(string reviewId);
        public Task<APIRevisionListItemModel> GetLatestAPIRevisionsAsync(string reviewId = null, IEnumerable<APIRevisionListItemModel> apiRevisions = null, APIRevisionType apiRevisionType = APIRevisionType.All);
        public Task<APIRevisionListItemModel> GetAPIRevisionAsync(ClaimsPrincipal user, string apiRevisionId);
        public Task<APIRevisionListItemModel> GetAPIRevisionAsync(string apiRevisionId);
        public APIRevisionListItemModel GetNewAPIRevisionAsync(APIRevisionType apiRevisionType, string reviewId = null, string packageName = null, string language = null,
            string label = null, int? prNumber = null, string createdBy = "azure-sdk");
        public Task<bool> ToggleAPIRevisionApprovalAsync(ClaimsPrincipal user, string id, string revisionId = null, APIRevisionListItemModel apiRevision = null, string notes = "", string approver = "");
        public Task AddAPIRevisionAsync(ClaimsPrincipal user, string reviewId, APIRevisionType apiRevisionType, string name, string label, Stream fileStream, string language = "", bool awaitComputeDiff = false);
        public Task<APIRevisionListItemModel> AddAPIRevisionAsync(ClaimsPrincipal user, ReviewListItemModel review, APIRevisionType apiRevisionType, string name, string label, Stream fileStream, string language, bool awaitComputeDiff = false);
        public Task RunAPIRevisionGenerationPipeline(List<APIRevisionGenerationPipelineParamModel> reviewGenParams, string language);
        public Task SoftDeleteAPIRevisionAsync(ClaimsPrincipal user, string reviewId, string revisionId);
        public Task SoftDeleteAPIRevisionAsync(ClaimsPrincipal user, APIRevisionListItemModel apiRevision);
        public Task SoftDeleteAPIRevisionAsync(APIRevisionListItemModel apiRevision, string userName = "azure-sdk", string notes = "");
        public Task UpdateAPIRevisionLabelAsync(ClaimsPrincipal user, string revisionId, string label);
        public Task<bool> AreAPIRevisionsTheSame(APIRevisionListItemModel apiRevision, RenderedCodeFile renderedCodeFile);
        public Task UpdateAPIRevisionCodeFileAsync(string repoName, string buildId, string artifact, string project);
        public Task GetLineNumbersOfHeadingsOfSectionsWithDiff(string reviewId, APIRevisionListItemModel apiRevision, IEnumerable<APIRevisionListItemModel> apiRevisions = null);
        public TreeNode<InlineDiffLine<CodeLine>> ComputeSectionDiff(TreeNode<CodeLine> before, TreeNode<CodeLine> after, RenderedCodeFile beforeFile, RenderedCodeFile afterFile);
        public Task<APIRevisionListItemModel> CreateAPIRevisionAsync(string userName, string reviewId, APIRevisionType apiRevisionType, string label,
            MemoryStream memoryStream, CodeFile codeFile, string originalName = null, int? prNumber = null);
        public Task UpdateAPIRevisionAsync(APIRevisionListItemModel revision, LanguageService languageService, TelemetryClient telemetryClient);
        public Task UpdateAPIRevisionAsync(APIRevisionListItemModel revision);
        public Task AutoArchiveAPIRevisions(int archiveAfterMonths);
    }
}
