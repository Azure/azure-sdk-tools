using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.Extensions.Logging;

namespace APIViewWeb.Managers;

public class ProjectsManager : IProjectsManager
{
    private readonly ILogger<ProjectsManager> _logger;
    private readonly ICosmosProjectRepository _projectsRepository;
    private readonly ICosmosReviewRepository _reviewsRepository;
    private readonly INamespaceManager _namespaceManager;

    private sealed record ReviewLinkChanges(
        List<ReviewListItemModel> ReviewsToAdd,
        List<ProjectChangeHistory> ChangeHistoryEntries);

    private sealed record ReconciliationResult(
        List<ReviewListItemModel> ReviewsToUpsert,
        List<ReviewListItemModel> ReviewsToAdd,
        List<ReviewListItemModel> StillLinkedReviews,
        HashSet<string> ReviewsToRemove,
        HashSet<string> HistoricalReviewIdsToAdd,
        List<ProjectChangeHistory> ChangeHistoryEntries);

    public ProjectsManager(ICosmosProjectRepository projectsRepository,
        ICosmosReviewRepository reviewsRepository,
        INamespaceManager namespaceManager,
        ILogger<ProjectsManager> logger)
    {
        _projectsRepository = projectsRepository;
        _reviewsRepository = reviewsRepository;
        _namespaceManager = namespaceManager;
        _logger = logger;
    }

    public async Task<Project> UpsertProjectFromMetadataAsync(string userName, TypeSpecMetadata metadata, ReviewListItemModel typeSpecReview)
    {
        if (typeSpecReview.Language != ApiViewConstants.TypeSpecLanguage || metadata?.TypeSpec == null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(typeSpecReview.ProjectId))
        {
            return await UpdateProjectFromMetadataAsync(userName, metadata, typeSpecReview);
        }

        return await CreateProjectFromMetadataAsync(userName, metadata, typeSpecReview);
    }

    public async Task<Project> TryLinkReviewToProjectAsync(string userName, ReviewListItemModel review)
    {
        Project project = null;

        if (!string.IsNullOrEmpty(review.CrossLanguagePackageId))
        {
            project = await _projectsRepository.GetProjectByCrossLanguagePackageIdAsync(review.CrossLanguagePackageId);
        }

        string normalizedLanguage = !string.IsNullOrWhiteSpace(review.Language)
            ? LanguageServiceHelpers.MapLanguageAlias(review.Language)
            : null;

        if (project == null && !string.IsNullOrEmpty(normalizedLanguage) && !string.IsNullOrEmpty(review.PackageName))
        {
            project = await _projectsRepository.GetProjectByExpectedPackageAsync(normalizedLanguage, review.PackageName);
        }

        if (project == null)
        {
            _logger.LogDebug(
                "No project found for review {ReviewId} (language: {Language}, packageName: {PackageName}, crossLanguagePackageId: {CrossLanguagePackageId})",
                review.Id, review.Language, review.PackageName, review.CrossLanguagePackageId);
            return null;
        }

        project.ChangeHistory ??= [];

        if (!string.IsNullOrEmpty(normalizedLanguage))
        {
            if (!project.Reviews.TryGetValue(normalizedLanguage, out var ids))
            {
                ids = [];
                project.Reviews[normalizedLanguage] = ids;
            }

            if (!ids.Contains(review.Id, StringComparer.OrdinalIgnoreCase))
            {
                ids.Add(review.Id);
                project.ChangeHistory.Add(new ProjectChangeHistory
                {
                    ChangedOn = DateTime.UtcNow,
                    ChangedBy = userName,
                    ChangeAction = ProjectChangeAction.ReviewLinked,
                    Notes = $"Review {review.Id} ({review.Language}/{review.PackageName}) linked to project"
                });

                await _projectsRepository.UpsertProjectAsync(project);
                _logger.LogInformation("Linked review {ReviewId} to project {ProjectId} by {User}", review.Id, project.Id,
                    userName);
            }
        }

        review.ProjectId = project.Id;
        await _reviewsRepository.UpsertReviewAsync(review);

        return project;
    }

    private async Task<Project> CreateProjectFromMetadataAsync(string userName, TypeSpecMetadata metadata, ReviewListItemModel typeSpecReview)
    {
        Dictionary<string, List<PackageInfo>> packagesDict = BuildPackagesDict(metadata);
        var project = new Project
        {
            Id = Guid.NewGuid().ToString(),
            CrossLanguagePackageId = typeSpecReview.CrossLanguagePackageId,
            DisplayName = metadata.TypeSpec.Namespace,
            Description = metadata.TypeSpec.Documentation,
            Namespace = metadata.TypeSpec.Namespace,
            ExpectedPackages = BuildExpectedPackages(packagesDict),
            ExpectedNamespaces = BuildExpectedNamespaces(packagesDict),
            Reviews = { [ApiViewConstants.TypeSpecLanguage] = [typeSpecReview.Id] },
            Owners = [userName],
            ChangeHistory =
            [
                new ProjectChangeHistory
                {
                    ChangedOn = DateTime.UtcNow,
                    ChangedBy = userName,
                    ChangeAction = ProjectChangeAction.Created,
                    Notes = "Project created from TypeSpec metadata"
                }
            ],
            CreatedOn = DateTime.UtcNow,
            LastUpdatedOn = DateTime.UtcNow,
            IsDeleted = false
        };

        var packagesToSearch = packagesDict.Where(ep => ep.Value?.Any(p => !string.IsNullOrEmpty(p.PackageName)) == true);
        ReviewLinkChanges relatedReviews = await DiscoverReviewsForLinkingAsync(
            project.Id, userName, packagesToSearch, excludeReviewIds: [typeSpecReview.Id]);
        foreach (ReviewListItemModel review in relatedReviews.ReviewsToAdd)
        {
            if (!project.Reviews.TryGetValue(review.Language, out var ids))
            {
                ids = [];
                project.Reviews[review.Language] = ids;
            }
            if (!ids.Contains(review.Id, StringComparer.OrdinalIgnoreCase))
                ids.Add(review.Id);
        }

        project.NamespaceInfo = _namespaceManager.BuildInitialNamespaceInfo(userName, metadata, relatedReviews.ReviewsToAdd);
        project.ChangeHistory.AddRange(relatedReviews.ChangeHistoryEntries);
        await _projectsRepository.UpsertProjectAsync(project);

        typeSpecReview.ProjectId = project.Id;
        relatedReviews.ReviewsToAdd.Add(typeSpecReview);
        await _reviewsRepository.UpsertReviewsAsync(relatedReviews.ReviewsToAdd);

        return project;
    }

    private async Task<Project> UpdateProjectFromMetadataAsync(string userName, TypeSpecMetadata metadata, ReviewListItemModel typeSpecReview)
    {
        Project project = await _projectsRepository.GetProjectAsync(typeSpecReview.ProjectId);
        if (project == null)
        {
            _logger.LogWarning("Project {ProjectId} not found for review {ReviewId}. Creating new project.", typeSpecReview.ProjectId, typeSpecReview.Id);
            typeSpecReview.ProjectId = null;
            return await CreateProjectFromMetadataAsync(userName, metadata, typeSpecReview);
        }

        var changes = new List<string>();

        if (!string.Equals(project.Namespace, metadata.TypeSpec.Namespace, StringComparison.OrdinalIgnoreCase))
        {
            var oldNamespace = project.Namespace;
            project.Namespace = metadata.TypeSpec.Namespace;
            project.DisplayName = metadata.TypeSpec.Namespace;
            changes.Add("Namespace");
            changes.Add("DisplayName");

            project.NamespaceInfo = _namespaceManager.ResolveTypeSpecNamespaceChange(
                userName, project.NamespaceInfo, oldNamespace, metadata.TypeSpec.Namespace);
        }

        if (!string.Equals(project.CrossLanguagePackageId, typeSpecReview.CrossLanguagePackageId,
                StringComparison.OrdinalIgnoreCase))
        {
            project.CrossLanguagePackageId = typeSpecReview.CrossLanguagePackageId;
            changes.Add("CrossLanguagePackageId");
        }

        // Case-sensitive comparison is intentional: description is human-readable text where casing matters.
        if (!string.Equals(project.Description, metadata.TypeSpec.Documentation, StringComparison.Ordinal))
        {
            project.Description = metadata.TypeSpec.Documentation;
            changes.Add("Description");
        }

        var reviewsToUpsert = new List<ReviewListItemModel>();
        var packagesDict = BuildPackagesDict(metadata);
        var newExpectedPackages = BuildExpectedPackages(packagesDict);
        var newExpectedNamespaces = BuildExpectedNamespaces(packagesDict);
        if (!SetsEqual(project.ExpectedPackages, newExpectedPackages) || !SetsEqual(project.ExpectedNamespaces, newExpectedNamespaces))
        {
            var oldPackagesDict = BuildPackagesDictFromTokens(project.ExpectedPackages, project.ExpectedNamespaces);
            project.ExpectedPackages = newExpectedPackages;
            project.ExpectedNamespaces = newExpectedNamespaces;
            changes.Add("ExpectedPackages");

            var reconciled = await ReconcileReviewLinksAsync(userName, project, typeSpecReview.Id);

            project.ChangeHistory ??= [];

            foreach (var (key, ids) in project.Reviews.ToList())
            {
                ids.RemoveAll(id => reconciled.ReviewsToRemove.Contains(id));
                if (ids.Count == 0)
                    project.Reviews.Remove(key);
            }

            foreach (ReviewListItemModel reviewListItemModel in reconciled.ReviewsToAdd)
            {
                if (!project.Reviews.TryGetValue(reviewListItemModel.Language, out var ids))
                {
                    ids = [];
                    project.Reviews[reviewListItemModel.Language] = ids;
                }
                if (!ids.Contains(reviewListItemModel.Id, StringComparer.OrdinalIgnoreCase))
                    ids.Add(reviewListItemModel.Id);
            }

            project.HistoricalReviewIds ??= [];
            project.HistoricalReviewIds.UnionWith(reconciled.HistoricalReviewIdsToAdd);
            project.ChangeHistory.AddRange(reconciled.ChangeHistoryEntries);
            reviewsToUpsert = reconciled.ReviewsToUpsert;
            var allLinkedReviews = reconciled.StillLinkedReviews.Concat(reconciled.ReviewsToAdd).ToList();
            project.NamespaceInfo = _namespaceManager.ResolvePackageNamespaceChanges(userName, project.NamespaceInfo, oldPackagesDict, packagesDict, allLinkedReviews);
        }

        if (changes.Count > 0)
        {
            project.ChangeHistory.Add(new ProjectChangeHistory
            {
                ChangedOn = DateTime.UtcNow,
                ChangedBy = userName,
                ChangeAction = ProjectChangeAction.Edited,
                Notes = $"Updated: {string.Join(", ", changes)}"
            });
            project.LastUpdatedOn = DateTime.UtcNow;
            await _projectsRepository.UpsertProjectAsync(project);

            if (reviewsToUpsert.Count > 0)
            {
                await _reviewsRepository.UpsertReviewsAsync(reviewsToUpsert);
            }
        }

        return project;
    }

    private async Task<ReconciliationResult> ReconcileReviewLinksAsync(string userName, Project project, string typeSpecReviewId)
    {
        var reviewIdsToRemove = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var historicalIdsToAdd = new HashSet<string>();
        var changeEntries = new List<ProjectChangeHistory>();
        var reviewsToUpsert = new List<ReviewListItemModel>();

        List<string> reviewIdsToCheck = project.Reviews.Values
            .SelectMany(ids => ids)
            .Where(id => id != typeSpecReviewId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var reviews = reviewIdsToCheck.Count > 0
            ? (await _reviewsRepository.GetReviewsAsync(reviewIdsToCheck)).ToList()
            : [];

        var foundIds = new HashSet<string>(reviews.Select(r => r.Id));
        var orphanedIds = reviewIdsToCheck.Where(id => !foundIds.Contains(id)).ToList();
        if (orphanedIds.Count > 0)
        {
            _logger.LogWarning(
                "Project {ProjectId} has review IDs not found in database: {OrphanedIds}. Removing from Reviews.",
                project.Id, string.Join(", ", orphanedIds));
            foreach (var orphanId in orphanedIds)
            {
                reviewIdsToRemove.Add(orphanId);
            }
        }

        var coveredLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stillLinkedReviews = new List<ReviewListItemModel>();
        foreach (var review in reviews)
        {
            bool stillMatches = !string.IsNullOrEmpty(review.Language)
                               && !string.IsNullOrEmpty(review.PackageName)
                               && (project.ExpectedPackages ?? []).Contains(
                                   $"{review.Language.ToLowerInvariant()}::{review.PackageName.ToLowerInvariant()}");

            if (stillMatches)
            {
                coveredLanguages.Add(review.Language);
                stillLinkedReviews.Add(review);
                continue;
            }

            reviewIdsToRemove.Add(review.Id);
            historicalIdsToAdd.Add(review.Id);
            review.ProjectId = null;
            reviewsToUpsert.Add(review);
        }

        if (reviewsToUpsert.Count > 0)
        {
            changeEntries.Add(new ProjectChangeHistory
            {
                ChangedOn = DateTime.UtcNow,
                ChangedBy = userName,
                ChangeAction = ProjectChangeAction.ReviewUnlinked,
                Notes = $"Reviews unlinked (ExpectedPackages changed): {string.Join(", ", reviewsToUpsert.Select(r => r.Id))}"
            });
        }

        // For every expected-package language without a linked review, try to find and link one.
        var coveredPackageTokens = new HashSet<string>(
            stillLinkedReviews
                .Where(r => !string.IsNullOrEmpty(r.Language) && !string.IsNullOrEmpty(r.PackageName))
                .Select(r => $"{r.Language.ToLowerInvariant()}::{r.PackageName.ToLowerInvariant()}"),
            StringComparer.OrdinalIgnoreCase);
        var uncoveredPackages = (project.ExpectedPackages ?? [])
            .Select(t => t.Split("::", 2))
            .Where(p => p.Length == 2 && !string.IsNullOrEmpty(p[1]))
            .Where(p => !coveredPackageTokens.Contains($"{p[0]}::{p[1]}"))
            .GroupBy(p => LanguageServiceHelpers.MapLanguageAlias(p[0]), StringComparer.OrdinalIgnoreCase)
            .Select(g => new KeyValuePair<string, List<PackageInfo>>(
                g.Key,
                g.Select(p => new PackageInfo { PackageName = p[1] }).ToList()))
            .Where(ep => ep.Value.Count > 0);
        var discovered = await DiscoverReviewsForLinkingAsync(
            project.Id, userName, uncoveredPackages, excludeReviewIds: [typeSpecReviewId]);

        reviewsToUpsert.AddRange(discovered.ReviewsToAdd);
        changeEntries.AddRange(discovered.ChangeHistoryEntries);
        return new ReconciliationResult(reviewsToUpsert, discovered.ReviewsToAdd, stillLinkedReviews, reviewIdsToRemove, historicalIdsToAdd, changeEntries);
    }

    private async Task<ReviewLinkChanges> DiscoverReviewsForLinkingAsync(
        string projectId,
        string userName,
        IEnumerable<KeyValuePair<string, List<PackageInfo>>> packagesToSearch,
        HashSet<string> excludeReviewIds)
    {
        var reviews = new List<ReviewListItemModel>();
        var addedReviewIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var changeEntries = new List<ProjectChangeHistory>();

        foreach (var (language, packages) in packagesToSearch)
        {
            foreach (var pkg in packages.Where(p => !string.IsNullOrEmpty(p.PackageName)))
            {
                ReviewListItemModel candidate = await _reviewsRepository.GetReviewAsync(language, pkg.PackageName, false);
                if (candidate == null || excludeReviewIds.Contains(candidate.Id) || !addedReviewIds.Add(candidate.Id))
                {
                    continue;
                }

                string previousProjectId = candidate.ProjectId;

                if (string.Equals(previousProjectId, projectId, StringComparison.Ordinal))
                {
                    continue;
                }

                candidate.ProjectId = projectId;

                string notes = !string.IsNullOrEmpty(previousProjectId)
                    ? $"Review {candidate.Id} ({language}/{pkg.PackageName}) re-linked from project {previousProjectId}"
                    : $"Review {candidate.Id} ({language}/{pkg.PackageName}) linked to project";

                changeEntries.Add(new ProjectChangeHistory
                {
                    ChangedOn = DateTime.UtcNow,
                    ChangedBy = userName,
                    ChangeAction = ProjectChangeAction.ReviewLinked,
                    Notes = notes
                });
                reviews.Add(candidate);
            }
        }

        return new ReviewLinkChanges(reviews, changeEntries);
    }

    private static Dictionary<string, List<PackageInfo>> BuildPackagesDict(TypeSpecMetadata metadata)
    {
        if (metadata?.Languages == null)
        {
            return new Dictionary<string, List<PackageInfo>>(StringComparer.OrdinalIgnoreCase);
        }

        var result = new Dictionary<string, List<PackageInfo>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, configs) in metadata.Languages)
        {
            var language = LanguageServiceHelpers.MapLanguageAlias(key);
            // Deduplicate by (PackageName, Namespace) — multiple emitters for the same package
            var packages = configs
                .Where(c => !string.IsNullOrEmpty(c.Namespace) || !string.IsNullOrEmpty(c.PackageName))
                .GroupBy(
                    c => $"{c.PackageName ?? ""}::{c.Namespace ?? ""}",
                    StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .Select(c => new PackageInfo { Namespace = c.Namespace, PackageName = c.PackageName })
                .ToList();
            if (packages.Count > 0)
                result[language] = packages;
        }
        return result;
    }


    private static List<string> BuildExpectedPackages(Dictionary<string, List<PackageInfo>> packagesDict)
    {
        return packagesDict
            .SelectMany(kvp => (kvp.Value ?? [])
                .Where(p => !string.IsNullOrEmpty(p.PackageName))
                .Select(p => $"{kvp.Key.ToLowerInvariant()}::{p.PackageName.ToLowerInvariant()}"))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static List<string> BuildExpectedNamespaces(Dictionary<string, List<PackageInfo>> packagesDict)
    {
        return packagesDict
            .SelectMany(kvp => (kvp.Value ?? [])
                .Where(p => !string.IsNullOrEmpty(p.Namespace))
                .Select(p => $"{kvp.Key.ToLowerInvariant()}::{p.Namespace.ToLowerInvariant()}"))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static bool SetsEqual(List<string> a, List<string> b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        if (a.Count != b.Count) return false;
        var aSet = new HashSet<string>(a, StringComparer.Ordinal);
        return b.All(x => aSet.Contains(x));
    }

    /// <summary>
    /// Rebuilds a language → PackageInfo dict by combining the flat ExpectedPackages and
    /// ExpectedNamespaces token lists.
    /// </summary>
    private static Dictionary<string, List<PackageInfo>> BuildPackagesDictFromTokens(
        List<string> expectedPackages,
        List<string> expectedNamespaces)
    {
        var nsByLang = (expectedNamespaces ?? [])
            .Select(t => t.Split("::", 2))
            .Where(p => p.Length == 2)
            .GroupBy(p => p[0], StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(p => p[1]).ToList(), StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<string, List<PackageInfo>>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in expectedPackages ?? [])
        {
            var parts = token.Split("::", 2);
            if (parts.Length != 2) continue;
            var lang = LanguageServiceHelpers.MapLanguageAlias(parts[0]);
            var pkgName = parts[1];
            if (!result.TryGetValue(lang, out var list))
            {
                list = [];
                result[lang] = list;
            }
            var ns = nsByLang.TryGetValue(parts[0], out var nsList) ? nsList.FirstOrDefault() : null;
            list.Add(new PackageInfo { PackageName = pkgName, Namespace = ns });
        }
        return result;
    }
}
