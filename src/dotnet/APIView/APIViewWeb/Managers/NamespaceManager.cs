using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.Extensions.Logging;

namespace APIViewWeb.Managers;

public static class NamespaceManagerConstants
{
    public const string AutoApprovalNotes = "Auto-approved: review was already approved at namespace proposal creation";
    public const string AutoWithdrawalLanguageRemoved = "Automatically withdrawn as language was removed from expected packages";
    public const string AutoWithdrawalNewNameSuggested = "Automatically withdrawn as new name was suggested";
}

public class NamespaceManager : INamespaceManager
{
    private readonly ILogger<NamespaceManager> _logger;
    private readonly IPermissionsManager _permissionsManager;
    private readonly ICosmosProjectRepository _projectsRepository;
    private readonly ICosmosReviewRepository _reviewsRepository;

    public NamespaceManager(ICosmosProjectRepository projectsRepository,
        ICosmosReviewRepository reviewsRepository,
        IPermissionsManager permissionsManager,
        ILogger<NamespaceManager> logger)
    {
        _projectsRepository = projectsRepository;
        _reviewsRepository = reviewsRepository;
        _permissionsManager = permissionsManager;
        _logger = logger;
    }

    public async Task<ProjectNamespaceInfo> GetNamespaceInfoAsync(string projectId)
    {
        var project = await _projectsRepository.GetProjectAsync(projectId);
        return project?.NamespaceInfo;
    }

    public async Task<bool> IsNamespaceApprovedAsync(string projectId, string language)
    {
        if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(language))
        {
            return false;
        }

        ProjectNamespaceInfo info = await GetNamespaceInfoAsync(projectId);
        if (info == null)
        {
            return false;
        }

        language = LanguageServiceHelpers.MapLanguageAlias(language);
        return info.CurrentNamespaceStatus.TryGetValue(language, out var entry)
            && entry.Status == NamespaceDecisionStatus.Approved;
    }

    // Allowed transitions: maps (currentStatus) → set of valid target statuses.
    private static readonly Dictionary<NamespaceDecisionStatus, HashSet<NamespaceDecisionStatus>> allowedManualTransitions = new()
    {
        [NamespaceDecisionStatus.Proposed]  = [NamespaceDecisionStatus.Approved, NamespaceDecisionStatus.Rejected],
        [NamespaceDecisionStatus.Approved]  = [NamespaceDecisionStatus.Rejected],
        [NamespaceDecisionStatus.Rejected]  = [NamespaceDecisionStatus.Approved]
    };

    public async Task<NamespaceOperationResult> UpdateNamespaceStatusAsync(
        string projectId, string language, NamespaceDecisionStatus newStatus, string notes, ClaimsPrincipal user)
    {
        language = LanguageServiceHelpers.MapLanguageAlias(language);
        string userName = user.GetGitHubLogin();
        if (!await _permissionsManager.CanApproveAsync(userName, language))
        {
            return NamespaceOperationResult.Failure(NamespaceOperationError.Unauthorized);
        }

        Project project = await _projectsRepository.GetProjectAsync(projectId);
        if (project?.NamespaceInfo?.CurrentNamespaceStatus == null)
        {
            return NamespaceOperationResult.Failure(NamespaceOperationError.ProjectNotFound);
        }

        if (!project.NamespaceInfo.CurrentNamespaceStatus.TryGetValue(language, out var entry))
        {
            return NamespaceOperationResult.Failure(NamespaceOperationError.LanguageNotFound);
        }

        if (!allowedManualTransitions.TryGetValue(entry.Status, out var allowed) || !allowed.Contains(newStatus))
        {
            return NamespaceOperationResult.Failure(NamespaceOperationError.InvalidStateTransition);
        }

        EnsureHistoryList(project.NamespaceInfo, language).Add(new NamespaceDecisionEntry
        {
            Language = entry.Language,
            PackageName = entry.PackageName,
            Namespace = entry.Namespace,
            Status = entry.Status,
            Notes = entry.Notes,
            ProposedBy = entry.ProposedBy,
            ProposedOn = entry.ProposedOn,
            DecidedBy = entry.DecidedBy,
            DecidedOn = entry.DecidedOn
        });

        entry.Status = newStatus;
        if (newStatus != NamespaceDecisionStatus.Proposed)        {
            entry.DecidedBy = userName;
            entry.DecidedOn = DateTime.UtcNow;
        }
        entry.Notes = notes;

        project.NamespaceInfo.ApprovedNamespaces = project.NamespaceInfo.CurrentNamespaceStatus
            .Where(kvp => kvp.Value.Status == NamespaceDecisionStatus.Approved)
            .Select(kvp => kvp.Value)
            .ToList();

        project.ChangeHistory ??= [];
        project.ChangeHistory.Add(new ProjectChangeHistory
        {
            ChangedOn = DateTime.UtcNow,
            ChangedBy = userName,
            ChangeAction = ProjectChangeAction.NamespaceStatusChanged,
            Notes = $"Namespace '{entry.Namespace}' for {language} changed to {newStatus}"
        });
        project.LastUpdatedOn = DateTime.UtcNow;
        await _projectsRepository.UpsertProjectAsync(project);

        return NamespaceOperationResult.Success(project);
    }

    public ProjectNamespaceInfo BuildInitialNamespaceInfo(string userName, TypeSpecMetadata metadata, IReadOnlyList<ReviewListItemModel> reviews)
    {
        ProjectNamespaceInfo result = new();
        if (!string.IsNullOrEmpty(metadata.TypeSpec?.Namespace))
        {
            var entry = new NamespaceDecisionEntry
            {
                Language = ApiViewConstants.TypeSpecLanguage,
                Namespace = metadata.TypeSpec.Namespace,
                Status = NamespaceDecisionStatus.Proposed,
                ProposedBy = userName,
                ProposedOn = DateTime.UtcNow
            };
            result.CurrentNamespaceStatus[entry.Language] = entry;
        }

        if (metadata.Languages == null)
        {
            return result;
        }

        foreach ((string rawLanguage, LanguageConfig config) in metadata.Languages.Where(kv => !string.IsNullOrEmpty(kv.Value.Namespace)))
        {
            string language = LanguageServiceHelpers.MapLanguageAlias(rawLanguage);
            var entry = new NamespaceDecisionEntry
            {
                Language = language,
                PackageName = config.PackageName,
                Namespace = config.Namespace,
                Status = NamespaceDecisionStatus.Proposed,
                ProposedBy = userName,
                ProposedOn = DateTime.UtcNow
            };

            ReviewListItemModel languageReview = reviews.FirstOrDefault(r => string.Equals(r.Language, language, StringComparison.OrdinalIgnoreCase));
            if (languageReview is { IsApproved: true })
            {
                ReviewChangeHistoryModel approvedAction = languageReview.ChangeHistory.FirstOrDefault(ch => ch.ChangeAction == ReviewChangeAction.Approved);
                entry.Status = NamespaceDecisionStatus.Approved;
                entry.DecidedBy = approvedAction?.ChangedBy ?? ApiViewConstants.AzureSdkBotName;
                entry.Notes = $"Auto-approved: review was already approved at project creation as it was approved in review {languageReview.Id}";
                entry.DecidedOn = approvedAction?.ChangedOn ?? DateTime.UtcNow;
            }

            result.CurrentNamespaceStatus[entry.Language] = entry;
        }

        result.ApprovedNamespaces = result.CurrentNamespaceStatus
            .Where(ns => ns.Value.Status == NamespaceDecisionStatus.Approved).Select(n => n.Value).ToList();
        return result;
    }

    public ProjectNamespaceInfo ResolveTypeSpecNamespaceChange(string userName, ProjectNamespaceInfo currentInfo, string oldNamespace, string newNamespace)
    {
        if (string.Equals(oldNamespace, newNamespace))
        {
            return currentInfo;
        }

        if (currentInfo.CurrentNamespaceStatus.TryGetValue(ApiViewConstants.TypeSpecLanguage, out var oldEntry))
        {
            oldEntry.Status = NamespaceDecisionStatus.Withdrawn;
            oldEntry.Notes += "Automatically withdrawn as new name was suggested";
            oldEntry.DecidedOn = DateTime.UtcNow;
            EnsureHistoryList(currentInfo, ApiViewConstants.TypeSpecLanguage).Add(oldEntry);
        }

        NamespaceDecisionEntry proposed = CreateProposedEntry(userName, ApiViewConstants.TypeSpecLanguage, null, newNamespace);
        currentInfo.CurrentNamespaceStatus[ApiViewConstants.TypeSpecLanguage] = proposed;
        EnsureHistoryList(currentInfo, ApiViewConstants.TypeSpecLanguage).Add(proposed);

        return currentInfo;
    }

    public ProjectNamespaceInfo ResolvePackageNamespaceChanges(
        string userName,
        ProjectNamespaceInfo currentInfo,
        Dictionary<string, PackageInfo> oldPackages,
        Dictionary<string, PackageInfo> newPackages,
        IReadOnlyList<ReviewListItemModel> newReviews)
    {
        if (currentInfo == null || newPackages == null || oldPackages == null)
        {
            return currentInfo;
        }

        var oldLanguages = new HashSet<string>(oldPackages.Keys, StringComparer.OrdinalIgnoreCase);
        var newLanguages = new HashSet<string>(newPackages.Keys, StringComparer.OrdinalIgnoreCase);
        var approvedReviewsByPackage = newReviews
            .Where(r => r.IsApproved && !string.IsNullOrEmpty(r.PackageName))
            .ToDictionary(r => r.PackageName, r => r, StringComparer.OrdinalIgnoreCase);


        // Removed language → withdraw & remove language
        foreach (string lang in oldLanguages.Except(newLanguages))
        {
            if (!currentInfo.CurrentNamespaceStatus.TryGetValue(lang, out var entry))
            {
                continue;
            }

            entry.Status = NamespaceDecisionStatus.Withdrawn;
            entry.Notes += NamespaceManagerConstants.AutoWithdrawalLanguageRemoved;
            entry.DecidedBy = userName;
            entry.DecidedOn = DateTime.UtcNow;
            EnsureHistoryList(currentInfo, lang).Add(entry);
            currentInfo.CurrentNamespaceStatus.Remove(lang);
        }

        // Added → propose & add language
        foreach (string lang in newLanguages.Except(oldLanguages))
        {
            PackageInfo languagePackageInfo = newPackages[lang];
            if (!string.IsNullOrEmpty(languagePackageInfo?.Namespace))
            {
                NamespaceDecisionEntry proposed = CreateProposedEntry(userName, lang, languagePackageInfo.PackageName, languagePackageInfo.Namespace);
                if (approvedReviewsByPackage.TryGetValue(proposed.PackageName, out var approvedReview))
                {
                    ApplyAutoApproval(proposed, approvedReview);
                }

                currentInfo.CurrentNamespaceStatus[lang] = proposed;
                EnsureHistoryList(currentInfo, lang).Add(proposed);
            }
        }

        // Changed namespace → withdraw + propose
        foreach (string lang in oldLanguages.Intersect(newLanguages))
        {
            string oldNamespace = oldPackages[lang]?.Namespace;
            PackageInfo newPkg = newPackages[lang];
            string newNamespace = newPkg?.Namespace;

            if (!string.IsNullOrEmpty(newNamespace) && !string.Equals(oldNamespace, newNamespace))
            {
                if (currentInfo.CurrentNamespaceStatus.TryGetValue(lang, out var entry))
                {
                    entry.Status = NamespaceDecisionStatus.Withdrawn;
                    entry.DecidedBy = userName;
                    entry.DecidedOn = DateTime.UtcNow;
                    entry.Notes += NamespaceManagerConstants.AutoWithdrawalNewNameSuggested;

                    EnsureHistoryList(currentInfo, lang).Add(entry);
                }

                NamespaceDecisionEntry proposed = CreateProposedEntry(userName, lang, newPkg.PackageName, newNamespace);
                if (approvedReviewsByPackage.TryGetValue(proposed.PackageName, out var approvedReview))
                {
                    ApplyAutoApproval(proposed, approvedReview);
                }

                currentInfo.CurrentNamespaceStatus[lang] = proposed;
                EnsureHistoryList(currentInfo, lang).Add(proposed);
            }
        }

        currentInfo.ApprovedNamespaces = currentInfo.CurrentNamespaceStatus
            .Where(kvp => kvp.Value.Status == NamespaceDecisionStatus.Approved)
            .Select(kvp => kvp.Value)
            .ToList();

        return currentInfo;
    }

    private static void ApplyAutoApproval(NamespaceDecisionEntry entry, ReviewListItemModel approvedReview)
    {
        entry.Status = NamespaceDecisionStatus.Approved;
        ReviewChangeHistoryModel approvalAction = approvedReview.ChangeHistory?
            .LastOrDefault(ch => ch.ChangeAction == ReviewChangeAction.Approved && ch.ChangedOn > DateTime.MinValue);
        entry.DecidedBy = approvalAction?.ChangedBy ?? ApiViewConstants.AzureSdkBotName;
        entry.DecidedOn = approvalAction?.ChangedOn is { } d && d > DateTime.MinValue ? d : DateTime.UtcNow;
        entry.Notes = $" {NamespaceManagerConstants.AutoApprovalNotes} (review {approvedReview.Id}) ";
    }

    private static NamespaceDecisionEntry CreateProposedEntry(string userName, string language, string packageName, string namespaceName) => new()
    {
        Language = language,
        PackageName = packageName,
        Namespace = namespaceName,
        Status = NamespaceDecisionStatus.Proposed,
        ProposedBy = userName,
        ProposedOn = DateTime.UtcNow
    };

    private static List<NamespaceDecisionEntry> EnsureHistoryList(ProjectNamespaceInfo info, string language)
    {
        if (info.NamespaceHistory.TryGetValue(language, out var list))
        {
            return list;
        }

        list = [];
        info.NamespaceHistory[language] = list;
        return list;
    }
}
