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

namespace APIViewWeb.Managers.Interfaces
{
    public interface IAPIRevisionsManager
    {
        public Task<PagedList<APIRevisionListItemModel>> GetAPIRevisionsAsync(PageParams pageParams, APIRevisionsFilterAndSortParams filterAndSortParams);
        public Task<IEnumerable<APIRevisionListItemModel>> GetAPIRevisionsAsync(string reviewId);
        public Task<APIRevisionListItemModel> GetLatestAPIRevisionsAsync(string reviewId, IEnumerable<APIRevisionListItemModel> apiRevision = null, APIRevisionType apiRevisionType = APIRevisionType.All);
        public Task<APIRevisionListItemModel> GetAPIRevisionAsync(ClaimsPrincipal user, string revisionId);
        public Task<bool> ToggleAPIRevisionApprovalAsync(ClaimsPrincipal user, string id, string revisionId = null, APIRevisionListItemModel apiRevision = null, string notes = "");
        public Task AddAPIRevisionAsync(ClaimsPrincipal user, string reviewId, string name, string label, Stream fileStream, string language = "", bool awaitComputeDiff = false);
        public Task AddAPIRevisionAsync(ClaimsPrincipal user, ReviewListItemModel review, string name, string label, Stream fileStream, string language, bool awaitComputeDiff = false);
        public Task RunAPIRevisionGenerationPipeline(List<APIRevisionGenerationPipelineParamModel> reviewGenParams, string language);
        public Task SoftDeleteAPIRevisionAsync(ClaimsPrincipal user, string reviewId, string revisionId);
        public Task SoftDeleteAPIRevisionAsync(ClaimsPrincipal user, APIRevisionListItemModel apiRevision);
        public Task UpdateAPIRevisionLabelAsync(ClaimsPrincipal user, string revisionId, string label);
        public Task<bool> IsAPIRevisionTheSame(APIRevisionListItemModel apiRevision, RenderedCodeFile renderedCodeFile);
        public Task GetLineNumbersOfHeadingsOfSectionsWithDiff(string reviewId, APIRevisionListItemModel revision);
        public TreeNode<InlineDiffLine<CodeLine>> ComputeSectionDiff(TreeNode<CodeLine> before, TreeNode<CodeLine> after, RenderedCodeFile beforeFile, RenderedCodeFile afterFile);
        public Task<APIRevisionListItemModel> CreateAPIRevisionAsync(ClaimsPrincipal user, string reviewId, APIRevisionType apiRevisionType, string label, MemoryStream memoryStream, CodeFile codeFile, string originalName = null);
    }
}
