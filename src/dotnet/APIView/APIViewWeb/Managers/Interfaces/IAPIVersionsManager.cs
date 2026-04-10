using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;

namespace APIViewWeb.Managers.Interfaces;

public interface IAPIVersionsManager
{
    Task<APIVersionModel> GetOrCreateVersionAsync(string reviewId, string packageVersion, int? pullRequestNo = null, string sourceBranch = null);
    Task<IEnumerable<APIVersionModel>> GetVersionsForReviewAsync(string reviewId);
    Task<APIVersionModel> GetVersionByIdAsync(string reviewId, string versionId);
    Task<APIVersionModel> GetVersionByIdentifierAsync(string reviewId, string versionIdentifier);
    Task SoftDeleteVersionAsync(string versionId, string reviewId, string userName);
}
