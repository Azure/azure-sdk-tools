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

    public ProjectsManager(ICosmosProjectRepository projectsRepository,
        ICosmosReviewRepository reviewsRepository,
        ILogger<ProjectsManager> logger)
    {
        _projectsRepository = projectsRepository;
        _reviewsRepository = reviewsRepository;
        _logger = logger;
    }

    public async Task<Project> UpsertProjectFromMetadataAsync(string userName, TypeSpecMetadata metadata,
        ReviewListItemModel typeSpecReview)
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

    public async Task<Project> TryLinkReviewToProjectAsync(
        string userName,
        ReviewListItemModel review)
    {
        Project project = null;

        if (!string.IsNullOrEmpty(review.CrossLanguagePackageId))
        {
            project = await _projectsRepository.GetProjectByCrossLanguagePackageIdAsync(review.CrossLanguagePackageId);
        }

        if (project == null && !string.IsNullOrEmpty(review.Language) && !string.IsNullOrEmpty(review.PackageName))
        {
            project = await _projectsRepository.GetProjectByExpectedPackageAsync(review.Language, review.PackageName);
        }

        if (project == null)
        {
            _logger.LogDebug(
                "No project found for review {ReviewId} (language: {Language}, packageName: {PackageName}, crossLanguagePackageId: {CrossLanguagePackageId})",
                review.Id, review.Language, review.PackageName, review.CrossLanguagePackageId);
            return null;
        }

        if (project.ReviewIds.Add(review.Id))
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

    private async Task<Project> CreateProjectFromMetadataAsync(string userName, TypeSpecMetadata metadata,
        ReviewListItemModel typeSpecReview)
    {
        var project = new Project
        {
            Id = Guid.NewGuid().ToString(),
            CrossLanguagePackageId = typeSpecReview.CrossLanguagePackageId,
            DisplayName = metadata.TypeSpec.Namespace,
            Description = metadata.TypeSpec.Documentation,
            Namespace = metadata.TypeSpec.Namespace,
            ExpectedPackages = BuildExpectedPackages(metadata),
            Owners = [userName],
            ReviewIds = [typeSpecReview.Id],
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

        await _projectsRepository.UpsertProjectAsync(project);

        typeSpecReview.ProjectId = project.Id;
        await _reviewsRepository.UpsertReviewAsync(typeSpecReview);

        return project;
    }

    private async Task<Project> UpdateProjectFromMetadataAsync(string userName, TypeSpecMetadata metadata,
        ReviewListItemModel typeSpecReview)
    {
        Project project = await _projectsRepository.GetProjectAsync(typeSpecReview.ProjectId);
        if (project == null)
        {
            _logger.LogWarning("Project {ProjectId} not found for review {ReviewId}. Creating new project.",
                typeSpecReview.ProjectId, typeSpecReview.Id);
            typeSpecReview.ProjectId = null;
            return await CreateProjectFromMetadataAsync(userName, metadata, typeSpecReview);
        }

        var changes = new List<string>();

        if (!string.Equals(project.Namespace, metadata.TypeSpec.Namespace, StringComparison.OrdinalIgnoreCase))
        {
            project.Namespace = metadata.TypeSpec.Namespace;
            changes.Add("Namespace");
        }

        if (!string.Equals(project.CrossLanguagePackageId, typeSpecReview.CrossLanguagePackageId,
                StringComparison.OrdinalIgnoreCase))
        {
            project.CrossLanguagePackageId = typeSpecReview.CrossLanguagePackageId;
            changes.Add("CrossLanguagePackageId");
        }

        if (!string.Equals(project.Description, metadata.TypeSpec.Documentation))
        {
            project.Description = metadata.TypeSpec.Documentation;
            changes.Add("Description");
        }

        var newExpectedPackages = BuildExpectedPackages(metadata);
        if (!AreExpectedPackagesEqual(project.ExpectedPackages, newExpectedPackages))
        {
            project.ExpectedPackages = newExpectedPackages;
            changes.Add("ExpectedPackages");
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
        }

        return project;
    }

    private static Dictionary<string, PackageInfo> BuildExpectedPackages(TypeSpecMetadata metadata)
    {
        return metadata.Languages?.ToDictionary(
            lang => lang.Key,
            lang => new PackageInfo { Namespace = lang.Value.Namespace, PackageName = lang.Value.PackageName }
        ) ?? new Dictionary<string, PackageInfo>();
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
                                  kvp.Value?.Namespace == u?.Namespace &&
                                  kvp.Value?.PackageName == u?.PackageName);
    }
}
