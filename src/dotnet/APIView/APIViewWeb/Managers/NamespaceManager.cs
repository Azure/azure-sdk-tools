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
    public const string AutoWithdrawalPackageRemoved = "Automatically withdrawn as package was removed from expected packages";
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
        return info.CurrentNamespaceStatus.TryGetValue(language, out var entries)
            && entries.Any(e => e.Status == NamespaceDecisionStatus.Approved);
    }

    // Allowed transitions: maps (currentStatus) → set of valid target statuses.
    private static readonly Dictionary<NamespaceDecisionStatus, HashSet<NamespaceDecisionStatus>> allowedManualTransitions = new()
    {
        [NamespaceDecisionStatus.Proposed]  = [NamespaceDecisionStatus.Approved, NamespaceDecisionStatus.Rejected],
        [NamespaceDecisionStatus.Approved]  = [NamespaceDecisionStatus.Rejected],
        [NamespaceDecisionStatus.Rejected]  = [NamespaceDecisionStatus.Approved]
    };

    public async Task<NamespaceOperationResult> UpdateNamespaceStatusAsync(
        string projectId, string language, string namespaceVal, NamespaceDecisionStatus newStatus, string notes, ClaimsPrincipal user)
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

        if (!project.NamespaceInfo.CurrentNamespaceStatus.TryGetValue(language, out var entries) || entries.Count == 0)
        {
            return NamespaceOperationResult.Failure(NamespaceOperationError.LanguageNotFound);
        }

        // Match by namespace (the stable identifier); fall back to the single entry when namespace is omitted.
        NamespaceDecisionEntry entry = string.IsNullOrEmpty(namespaceVal)
            ? entries.Count == 1 ? entries[0] : null
            : entries.FirstOrDefault(e => string.Equals(e.Namespace, namespaceVal, StringComparison.OrdinalIgnoreCase));

        if (entry == null)
        {
            return NamespaceOperationResult.Failure(NamespaceOperationError.NamespaceEntryNotFound);
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
        if (newStatus != NamespaceDecisionStatus.Proposed)
        {
            entry.DecidedBy = userName;
            entry.DecidedOn = DateTime.UtcNow;
        }
        entry.Notes = notes;

        RebuildApprovedNamespaces(project.NamespaceInfo);

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
            result.CurrentNamespaceStatus[ApiViewConstants.TypeSpecLanguage] = [entry];
        }

        if (metadata.Languages == null)
        {
            return result;
        }

        // Build a review lookup keyed by (language, packageName) for efficient matching.
        var reviewsByLangAndPackage = reviews
            .Where(r => !string.IsNullOrEmpty(r.Language) && !string.IsNullOrEmpty(r.PackageName))
            .GroupBy(r => LanguageServiceHelpers.MapLanguageAlias(r.Language), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(r => r.PackageName, r => r, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

        foreach ((string rawLanguage, List<LanguageConfig> configs) in metadata.Languages)
        {
            string language = LanguageServiceHelpers.MapLanguageAlias(rawLanguage);
            var entries = new List<NamespaceDecisionEntry>();

            foreach (LanguageConfig config in configs.Where(c => !string.IsNullOrEmpty(c.Namespace)))
            {
                var entry = new NamespaceDecisionEntry
                {
                    Language = language,
                    PackageName = config.PackageName,
                    Namespace = config.Namespace,
                    Status = NamespaceDecisionStatus.Proposed,
                    ProposedBy = userName,
                    ProposedOn = DateTime.UtcNow
                };

                if (reviewsByLangAndPackage.TryGetValue(language, out var pkgMap) &&
                    !string.IsNullOrEmpty(config.PackageName) &&
                    pkgMap.TryGetValue(config.PackageName, out var linkedReview) &&
                    linkedReview.IsApproved)
                {
                    ReviewChangeHistoryModel approvedAction = linkedReview.ChangeHistory
                        .FirstOrDefault(ch => ch.ChangeAction == ReviewChangeAction.Approved);
                    entry.Status = NamespaceDecisionStatus.Approved;
                    entry.DecidedBy = approvedAction?.ChangedBy ?? ApiViewConstants.AzureSdkBotName;
                    entry.Notes = $"{NamespaceManagerConstants.AutoApprovalNotes} (review {linkedReview.Id})";
                    entry.DecidedOn = approvedAction?.ChangedOn ?? DateTime.UtcNow;
                }

                entries.Add(entry);
            }

            if (entries.Count > 0)
            {
                result.CurrentNamespaceStatus[language] = entries;
            }
        }

        RebuildApprovedNamespaces(result);
        return result;
    }

    public ProjectNamespaceInfo ResolveTypeSpecNamespaceChange(string userName, ProjectNamespaceInfo currentInfo, string oldNamespace, string newNamespace)
    {
        if (string.Equals(oldNamespace, newNamespace))
        {
            return currentInfo;
        }

        // TypeSpec always has exactly one namespace entry.
        if (currentInfo.CurrentNamespaceStatus.TryGetValue(ApiViewConstants.TypeSpecLanguage, out var oldEntries) && oldEntries.Count > 0)
        {
            var oldEntry = oldEntries[0];
            oldEntry.Status = NamespaceDecisionStatus.Withdrawn;
            oldEntry.Notes += NamespaceManagerConstants.AutoWithdrawalNewNameSuggested;
            oldEntry.DecidedOn = DateTime.UtcNow;
            EnsureHistoryList(currentInfo, ApiViewConstants.TypeSpecLanguage).Add(oldEntry);
        }

        NamespaceDecisionEntry proposed = CreateProposedEntry(userName, ApiViewConstants.TypeSpecLanguage, null, newNamespace);
        currentInfo.CurrentNamespaceStatus[ApiViewConstants.TypeSpecLanguage] = [proposed];
        EnsureHistoryList(currentInfo, ApiViewConstants.TypeSpecLanguage).Add(proposed);

        return currentInfo;
    }

    public ProjectNamespaceInfo ResolvePackageNamespaceChanges(
        string userName,
        ProjectNamespaceInfo currentInfo,
        Dictionary<string, List<PackageInfo>> oldPackages,
        Dictionary<string, List<PackageInfo>> newPackages,
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


        // Removed language → withdraw all entries for that language and remove the key.
        foreach (string lang in oldLanguages.Except(newLanguages).ToList())
        {
            if (!currentInfo.CurrentNamespaceStatus.TryGetValue(lang, out var entries))
            {
                continue;
            }

            foreach (var entry in entries)
            {
                entry.Status = NamespaceDecisionStatus.Withdrawn;
                entry.Notes += NamespaceManagerConstants.AutoWithdrawalLanguageRemoved;
                entry.DecidedBy = userName;
                entry.DecidedOn = DateTime.UtcNow;
                EnsureHistoryList(currentInfo, lang).Add(entry);
            }
            currentInfo.CurrentNamespaceStatus.Remove(lang);
        }

        // Added language → propose one entry per package that has a namespace.
        foreach (string lang in newLanguages.Except(oldLanguages).ToList())
        {
            var proposedEntries = new List<NamespaceDecisionEntry>();
            foreach (PackageInfo pkg in (newPackages[lang] ?? []).Where(p => !string.IsNullOrEmpty(p.Namespace)))
            {
                NamespaceDecisionEntry proposed = CreateProposedEntry(userName, lang, pkg.PackageName, pkg.Namespace);
                if (!string.IsNullOrEmpty(proposed.PackageName) &&
                    approvedReviewsByPackage.TryGetValue(proposed.PackageName, out var approvedReview))
                {
                    ApplyAutoApproval(proposed, approvedReview);
                }
                proposedEntries.Add(proposed);
                EnsureHistoryList(currentInfo, lang).Add(proposed);
            }
            if (proposedEntries.Count > 0)
            {
                currentInfo.CurrentNamespaceStatus[lang] = proposedEntries;
            }
        }

        // Same language in both → diff per package: removed, added, namespace-changed.
        foreach (string lang in oldLanguages.Intersect(newLanguages).ToList())
        {
            var oldByName = (oldPackages[lang] ?? [])
                .Where(p => !string.IsNullOrEmpty(p.PackageName))
                .ToDictionary(p => p.PackageName, StringComparer.OrdinalIgnoreCase);
            var newByName = (newPackages[lang] ?? [])
                .Where(p => !string.IsNullOrEmpty(p.PackageName))
                .ToDictionary(p => p.PackageName, StringComparer.OrdinalIgnoreCase);

            var currentEntries = EnsureCurrentList(currentInfo, lang);

            // Packages removed from this language → withdraw.
            foreach (string removedPkg in oldByName.Keys.Except(newByName.Keys, StringComparer.OrdinalIgnoreCase).ToList())
            {
                NamespaceDecisionEntry existing = currentEntries.FirstOrDefault(e =>
                    string.Equals(e.PackageName, removedPkg, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    existing.Status = NamespaceDecisionStatus.Withdrawn;
                    existing.DecidedBy = userName;
                    existing.DecidedOn = DateTime.UtcNow;
                    existing.Notes += NamespaceManagerConstants.AutoWithdrawalPackageRemoved;
                    EnsureHistoryList(currentInfo, lang).Add(existing);
                    currentEntries.Remove(existing);
                }
            }

            // Packages added to this language → propose.
            foreach (string addedPkg in newByName.Keys.Except(oldByName.Keys, StringComparer.OrdinalIgnoreCase).ToList())
            {
                PackageInfo newPkgInfo = newByName[addedPkg];
                if (!string.IsNullOrEmpty(newPkgInfo.Namespace))
                {
                    NamespaceDecisionEntry proposed = CreateProposedEntry(userName, lang, newPkgInfo.PackageName, newPkgInfo.Namespace);
                    if (approvedReviewsByPackage.TryGetValue(proposed.PackageName, out var autoApproveReview))
                    {
                        ApplyAutoApproval(proposed, autoApproveReview);
                    }
                    currentEntries.Add(proposed);
                    EnsureHistoryList(currentInfo, lang).Add(proposed);
                }
            }

            // Packages in both but namespace changed → withdraw + propose.
            foreach (string sharedPkg in oldByName.Keys.Intersect(newByName.Keys, StringComparer.OrdinalIgnoreCase).ToList())
            {
                string oldNs = oldByName[sharedPkg].Namespace;
                PackageInfo newPkgInfo = newByName[sharedPkg];
                if (!string.IsNullOrEmpty(newPkgInfo.Namespace) &&
                    !string.Equals(oldNs, newPkgInfo.Namespace, StringComparison.OrdinalIgnoreCase))
                {
                    var existing = currentEntries.FirstOrDefault(e =>
                        string.Equals(e.PackageName, sharedPkg, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        existing.Status = NamespaceDecisionStatus.Withdrawn;
                        existing.DecidedBy = userName;
                        existing.DecidedOn = DateTime.UtcNow;
                        existing.Notes += NamespaceManagerConstants.AutoWithdrawalNewNameSuggested;
                        EnsureHistoryList(currentInfo, lang).Add(existing);
                        currentEntries.Remove(existing);
                    }

                    NamespaceDecisionEntry proposed = CreateProposedEntry(userName, lang, newPkgInfo.PackageName, newPkgInfo.Namespace);
                    if (approvedReviewsByPackage.TryGetValue(proposed.PackageName, out var autoApproveReview))
                    {
                        ApplyAutoApproval(proposed, autoApproveReview);
                    }
                    currentEntries.Add(proposed);
                    EnsureHistoryList(currentInfo, lang).Add(proposed);
                }
            }

            if (currentEntries.Count == 0)
            {
                currentInfo.CurrentNamespaceStatus.Remove(lang);
            }
        }

        RebuildApprovedNamespaces(currentInfo);

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

    private static List<NamespaceDecisionEntry> EnsureCurrentList(ProjectNamespaceInfo info, string language)
    {
        if (info.CurrentNamespaceStatus.TryGetValue(language, out var list))
        {
            return list;
        }

        list = [];
        info.CurrentNamespaceStatus[language] = list;
        return list;
    }

    private static void RebuildApprovedNamespaces(ProjectNamespaceInfo info)
    {
        info.ApprovedNamespaces = info.CurrentNamespaceStatus
            .Values
            .SelectMany(entries => entries)
            .Where(e => e.Status == NamespaceDecisionStatus.Approved)
            .ToList();
    }
}
