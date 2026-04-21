using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;

namespace APIViewWeb.Managers;

public class AutoReviewService : IAutoReviewService
{
    private readonly IReviewManager _reviewManager;
    private readonly IAPIRevisionsManager _apiRevisionsManager;
    private readonly ICommentsManager _commentsManager;
    private readonly IProjectsManager _projectsManager;
    private readonly ICodeFileManager _codeFileManager;
    private readonly IAPIVersionsManager _apiVersionsManager;

    public AutoReviewService(
        IReviewManager reviewManager,
        IAPIRevisionsManager apiRevisionsManager,
        ICommentsManager commentsManager,
        IProjectsManager projectsManager,
        ICodeFileManager codeFileManager,
        IAPIVersionsManager apiVersionsManager)
    {
        _reviewManager = reviewManager;
        _apiRevisionsManager = apiRevisionsManager;
        _commentsManager = commentsManager;
        _projectsManager = projectsManager;
        _codeFileManager = codeFileManager;
        _apiVersionsManager = apiVersionsManager;
    }

    public async Task<(ReviewListItemModel review, APIRevisionListItemModel apiRevision)> CreateAutomaticRevisionAsync(
        ClaimsPrincipal user,
        CodeFile codeFile,
        string label,
        string originalName,
        MemoryStream memoryStream,
        string packageType,
        bool compareAllRevisions = false,
        string sourceBranch = null)
    {
        // Parse package type once at the beginning
        var parsedPackageType = !string.IsNullOrEmpty(packageType) && Enum.TryParse<PackageType>(packageType, true, out var result) ? (PackageType?)result : null;
        var createNewRevision = true;
        var review = await _reviewManager.GetReviewAsync(packageName: codeFile.PackageName, language: codeFile.Language, isClosed: null);
        var apiRevision = default(APIRevisionListItemModel);
        var renderedCodeFile = new RenderedCodeFile(codeFile);
        IEnumerable<APIRevisionListItemModel> apiRevisions = new List<APIRevisionListItemModel>();
        string incomingContentHash = null;
        if (review != null)
        {
            // Update package type if provided from controller parameter and not already set
            if (parsedPackageType.HasValue && !review.PackageType.HasValue)
            {
                review.PackageType = parsedPackageType;
                review = await _reviewManager.UpdateReviewAsync(review);
            }

            apiRevisions = await _apiRevisionsManager.GetAPIRevisionsAsync(review.Id);
            if (apiRevisions.Any())
            {
                apiRevisions = apiRevisions.OrderByDescending(r => r.CreatedOn);
                incomingContentHash = await _codeFileManager.ComputeAPIContentHashAsync(codeFile);

                APIVersionModel incomingVersionModel = null;
                if (!string.IsNullOrEmpty(codeFile.PackageVersion))
                {
                    incomingVersionModel = await _apiVersionsManager.GetOrCreateVersionAsync(review.Id, codeFile.PackageVersion);
                }

                // Delete pending apiRevisions if it is not in approved state before adding new revision
                // This is to keep only one pending revision since last approval or from initial review revision.
                var automaticRevisions = apiRevisions
                    .Where(r => r.APIRevisionType == APIRevisionType.Automatic
                        && (incomingVersionModel == null|| r.APIVersionId == incomingVersionModel.Id || string.IsNullOrEmpty(r.APIVersionId)))
                    .ToList();
                if (automaticRevisions.Count > 0)
                {
                    var comments = await _commentsManager.GetCommentsAsync(review.Id);
                    var revisionIdsWithComments = comments.Select(c => c.APIRevisionId).ToHashSet();
                    APIRevisionListItemModel latestAutomaticAPIRevision = null;

                    foreach (var revision in automaticRevisions)
                    {
                        // Hard stop: this revision and everything older is protected — stop processing
                        if (revision.IsApproved || revision.IsReleased || revisionIdsWithComments.Contains(revision.Id))
                        {
                            latestAutomaticAPIRevision ??= revision;
                            break;
                        }

                        if (latestAutomaticAPIRevision == null)
                        {
                            // First (newest) unprotected revision: keep it as the candidate to reuse or replace
                            latestAutomaticAPIRevision = revision;
                        }
                        else
                        {
                            // Older unprotected revision: always delete
                            await _apiRevisionsManager.SoftDeleteAPIRevisionAsync(apiRevision: revision, notes: "Deleted by Automatic Review Creation...");
                        }
                    }

                    // For release pipeline runs, compare against all approved revisions first to catch hotfix API changes.
                    // Otherwise, check if the candidate revision (newest unprotected) matches the incoming content.
                    // If it matches, reuse it. If not, delete the candidate and create a new revision.

                    bool considerPackageVersion = !string.IsNullOrWhiteSpace(codeFile.PackageVersion);

                    if (compareAllRevisions)
                    {
                        foreach (var approvedAPIRevision in automaticRevisions.Where(r => r.IsApproved))
                        {
                            if (await _apiRevisionsManager.AreAPIRevisionsTheSame(approvedAPIRevision, renderedCodeFile, considerPackageVersion, incomingContentHash))
                            {
                                return (review, approvedAPIRevision);
                            }
                        }
                    }

                    // Only reuse latestAutomaticAPIRevision if one was kept
                    if (latestAutomaticAPIRevision != null &&
                        await _apiRevisionsManager.AreAPIRevisionsTheSame(latestAutomaticAPIRevision, renderedCodeFile, considerPackageVersion, incomingContentHash))
                    {
                        apiRevision = latestAutomaticAPIRevision;
                        createNewRevision = false;
                    }
                    else if (latestAutomaticAPIRevision != null &&
                             !latestAutomaticAPIRevision.IsApproved &&
                             !latestAutomaticAPIRevision.IsReleased &&
                             !revisionIdsWithComments.Contains(latestAutomaticAPIRevision.Id))
                    {
                        // Candidate doesn't match incoming content and is unprotected — delete it before creating the new revision
                        await _apiRevisionsManager.SoftDeleteAPIRevisionAsync(apiRevision: latestAutomaticAPIRevision, notes: "Deleted by Automatic Review Creation...");
                    }
                }
            }
        }
        else
        {
            review = await _reviewManager.CreateReviewAsync(packageName: codeFile.PackageName, language: codeFile.Language, isClosed: false, packageType: parsedPackageType, crossLanguagePackageId: codeFile.CrossLanguagePackageId);
        }
        
        if (createNewRevision)
        {
            apiRevision = await _apiRevisionsManager.CreateAPIRevisionAsync(userName: user.GetGitHubLogin(), reviewId: review.Id, apiRevisionType: APIRevisionType.Automatic, label: label, memoryStream: memoryStream, codeFile: codeFile, originalName: originalName, sourceBranch: sourceBranch);
        }

        await _projectsManager.TryLinkReviewToProjectAsync(user.GetGitHubLogin(), review);

        if (apiRevision != null && apiRevisions.Any())
        {
            // Only revisions carrying approval or auto-generated comments are worth comparing
            IEnumerable<APIRevisionListItemModel> candidates = apiRevisions.Where(r => r.Id != apiRevision.Id && (r.IsApproved || r.HasAutoGeneratedComments));
            foreach (var apiRev in candidates)
            {
                if (apiRevision.IsApproved && apiRevision.HasAutoGeneratedComments)
                    break;

                if (await _apiRevisionsManager.AreAPIRevisionsTheSame(apiRev, renderedCodeFile, incomingContentHash: incomingContentHash))
                    await _apiRevisionsManager.CarryForwardRevisionDataAsync(targetRevision: apiRevision, sourceRevision: apiRev);
            }
        }
        return (review, apiRevision);
    }
}
