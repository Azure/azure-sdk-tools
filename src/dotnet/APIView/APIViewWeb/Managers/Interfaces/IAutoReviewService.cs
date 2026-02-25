using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb.LeanModels;

namespace APIViewWeb.Managers.Interfaces;

public interface IAutoReviewService
{
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
