using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using APIView;
using APIViewWeb.LeanModels;

namespace APIViewWeb.Managers.Interfaces;

public interface IAutoReviewService
{
    /// <summary>
    /// Creates or reuses an automatic revision. <paramref name="compareAllRevisions"/> is retained for endpoint
    /// compatibility; revision reuse is determined by package version and API surface, while approval carry-forward
    /// compares the selected revision with all eligible historical revisions.
    /// </summary>
    Task<(ReviewListItemModel review, APIRevisionListItemModel apiRevision)> CreateAutomaticRevisionAsync(
        ClaimsPrincipal user,
        CodeFile codeFile,
        string label,
        string originalName,
        MemoryStream memoryStream,
        string packageType,
        bool compareAllRevisions = false,
        string sourceBranch = null);
}
