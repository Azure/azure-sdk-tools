using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;
using APIViewWeb.Models;

namespace APIViewWeb.Managers.Interfaces;

public interface INamespaceManager
{
    Task<ProjectNamespaceInfo> GetNamespaceInfoAsync(string projectId);
    Task<bool> IsNamespaceApprovedAsync(string projectId, string language);
    Task<NamespaceOperationResult> UpdateNamespaceStatusAsync(string projectId, string language, NamespaceDecisionStatus newStatus, string notes, ClaimsPrincipal user);
    ProjectNamespaceInfo BuildInitialNamespaceInfo(string userName, TypeSpecMetadata metadata, IReadOnlyList<ReviewListItemModel> reviews);
    ProjectNamespaceInfo ResolveTypeSpecNamespaceChange(string userName, ProjectNamespaceInfo currentInfo, string oldNamespace, string newNamespace);
    ProjectNamespaceInfo ResolvePackageNamespaceChanges(string userName, ProjectNamespaceInfo currentInfo, Dictionary<string, PackageInfo> oldPackages, Dictionary<string, PackageInfo> newPackages,
        IReadOnlyList<ReviewListItemModel> newReviews);
}
