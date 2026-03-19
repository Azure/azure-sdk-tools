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

        if (!string.IsNullOrEmpty(normalizedLanguage) && project.Reviews.TryAdd(normalizedLanguage, review.Id))
        {
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

        review.ProjectId = project.Id;
        await _reviewsRepository.UpsertReviewAsync(review);

        return project;
    }

    private async Task<Project> CreateProjectFromMetadataAsync(string userName, TypeSpecMetadata metadata, ReviewListItemModel typeSpecReview)
    {
        Dictionary<string, PackageInfo> expectedPackages = BuildExpectedPackages(metadata);
        var project = new Project
        {
            Id = Guid.NewGuid().ToString(),
            CrossLanguagePackageId = typeSpecReview.CrossLanguagePackageId,
            DisplayName = metadata.TypeSpec.Namespace,
            Description = metadata.TypeSpec.Documentation,
            Namespace = metadata.TypeSpec.Namespace,
            ExpectedPackages = expectedPackages,
            Reviews = { [ApiViewConstants.TypeSpecLanguage] = typeSpecReview.Id },
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

        var packagesToSearch = expectedPackages.Where(ep => !string.IsNullOrEmpty(ep.Value?.PackageName));
        ReviewLinkChanges relatedReviews = await DiscoverReviewsForLinkingAsync(
            project.Id, userName, packagesToSearch, excludeReviewIds: [typeSpecReview.Id]);
        foreach (ReviewListItemModel review in relatedReviews.ReviewsToAdd)
        {
            project.Reviews[review.Language] = review.Id;
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
        var newExpectedPackages = BuildExpectedPackages(metadata);
        if (!AreExpectedPackagesEqual(project.ExpectedPackages, newExpectedPackages))
        {
            var oldExpectedPackages = project.ExpectedPackages;
            project.ExpectedPackages = newExpectedPackages;
            changes.Add("ExpectedPackages");

            var reconciled = await ReconcileReviewLinksAsync(userName, project, typeSpecReview.Id);

            project.ChangeHistory ??= [];

            foreach (string key in project.Reviews.Where(r => reconciled.ReviewsToRemove.Contains(r.Value))
                         .Select(r => r.Key).ToList())
            {
                project.Reviews.Remove(key);
            }

            foreach (ReviewListItemModel reviewListItemModel in reconciled.ReviewsToAdd)
            {
                project.Reviews[reviewListItemModel.Language] = reviewListItemModel.Id;
            }

            project.HistoricalReviewIds ??= [];
            project.HistoricalReviewIds.UnionWith(reconciled.HistoricalReviewIdsToAdd);
            project.ChangeHistory.AddRange(reconciled.ChangeHistoryEntries);
            reviewsToUpsert = reconciled.ReviewsToUpsert;
            var allLinkedReviews = reconciled.StillLinkedReviews.Concat(reconciled.ReviewsToAdd).ToList();
            project.NamespaceInfo = _namespaceManager.ResolvePackageNamespaceChanges(userName, project.NamespaceInfo, oldExpectedPackages, newExpectedPackages, allLinkedReviews);
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

        List<string> reviewIdsToCheck = project.Reviews.Values.Where(id => id != typeSpecReviewId).ToList();
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
                               && project.ExpectedPackages != null
                               && project.ExpectedPackages.TryGetValue(review.Language, out var expected)
                               && string.Equals(expected.PackageName, review.PackageName,
                                   StringComparison.OrdinalIgnoreCase);

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
        var uncoveredPackages = (project.ExpectedPackages ?? new Dictionary<string, PackageInfo>())
            .Where(ep => !coveredLanguages.Contains(ep.Key) && !string.IsNullOrEmpty(ep.Value?.PackageName));
        var discovered = await DiscoverReviewsForLinkingAsync(
            project.Id, userName, uncoveredPackages, excludeReviewIds: [typeSpecReviewId]);

        reviewsToUpsert.AddRange(discovered.ReviewsToAdd);
        changeEntries.AddRange(discovered.ChangeHistoryEntries);
        return new ReconciliationResult(reviewsToUpsert, discovered.ReviewsToAdd, stillLinkedReviews, reviewIdsToRemove, historicalIdsToAdd, changeEntries);
    }

    private async Task<ReviewLinkChanges> DiscoverReviewsForLinkingAsync(
        string projectId,
        string userName,
        IEnumerable<KeyValuePair<string, PackageInfo>> packagesToSearch,
        HashSet<string> excludeReviewIds)
    {
        var reviews = new List<ReviewListItemModel>();
        var changeEntries = new List<ProjectChangeHistory>();

        foreach (var (language, pkg) in packagesToSearch)
        {
            ReviewListItemModel candidate = await _reviewsRepository.GetReviewAsync(language, pkg.PackageName, false);
            if (candidate == null || excludeReviewIds.Contains(candidate.Id))
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
                ? $"Review {candidate.Id} ({candidate.Language}/{candidate.PackageName}) re-linked from project {previousProjectId}"
                : $"Review {candidate.Id} ({candidate.Language}/{candidate.PackageName}) linked to project";

            changeEntries.Add(new ProjectChangeHistory
            {
                ChangedOn = DateTime.UtcNow,
                ChangedBy = userName,
                ChangeAction = ProjectChangeAction.ReviewLinked,
                Notes = notes
            });
            reviews.Add(candidate);
        }

        return new ReviewLinkChanges(reviews, changeEntries);
    }

    private static Dictionary<string, PackageInfo> BuildExpectedPackages(TypeSpecMetadata metadata)
    {
        if (metadata?.Languages == null)
        {
            return new Dictionary<string, PackageInfo>(StringComparer.OrdinalIgnoreCase);
        }

        return metadata.Languages
            .Where(lang => !string.IsNullOrEmpty(lang.Value.Namespace) || !string.IsNullOrEmpty(lang.Value.PackageName))
            .ToDictionary(
                lang => LanguageServiceHelpers.MapLanguageAlias(lang.Key),
                lang => new PackageInfo { Namespace = lang.Value.Namespace, PackageName = lang.Value.PackageName },
                StringComparer.OrdinalIgnoreCase);
    }

    private static bool AreExpectedPackagesEqual(Dictionary<string, PackageInfo> current,
        Dictionary<string, PackageInfo> updated)
    {
        if (current == null && updated == null)
        {
            return true;
        }

        if (current == null || updated == null)
        {
            return false;
        }

        return current.Count == updated.Count &&
               current.All(kvp => updated.TryGetValue(kvp.Key, out var u) &&
                                  string.Equals(kvp.Value?.Namespace, u?.Namespace, StringComparison.OrdinalIgnoreCase) &&
                                  string.Equals(kvp.Value?.PackageName, u?.PackageName, StringComparison.OrdinalIgnoreCase));
    }
}
