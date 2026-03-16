using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;
using APIViewWeb.Models;

namespace APIViewWeb.Managers.Interfaces;

public interface INamespaceManager
{
    Task<ProjectNamespaceInfo> GetNamespaceInfoAsync(string projectId);
    Task<NamespaceOperationResult> ApproveNamespaceAsync(string projectId, string language, ClaimsPrincipal user);
    Task<NamespaceOperationResult> RejectNamespaceAsync(string projectId, string language, string notes, ClaimsPrincipal user);
    Task<NamespaceOperationResult> WithdrawNamespaceAsync(string projectId, string language, ClaimsPrincipal user);
    ProjectNamespaceInfo BuildInitialNamespaceInfo(string userName, TypeSpecMetadata metadata, IReadOnlyList<ReviewListItemModel> reviews);
    ProjectNamespaceInfo ResolveTypeSpecNamespaceChange(string userName, ProjectNamespaceInfo currentInfo, string oldNamespace, string newNamespace);
    ProjectNamespaceInfo ResolvePackageNamespaceChanges(string userName, ProjectNamespaceInfo currentInfo, Dictionary<string, PackageInfo> oldPackages, Dictionary<string, PackageInfo> newPackages,
        IReadOnlyList<ReviewListItemModel> newReviews);
}
