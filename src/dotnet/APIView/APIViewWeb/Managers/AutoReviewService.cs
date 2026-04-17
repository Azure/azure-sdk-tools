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

    public AutoReviewService(
        IReviewManager reviewManager,
        IAPIRevisionsManager apiRevisionsManager,
        ICommentsManager commentsManager,
        IProjectsManager projectsManager,
        ICodeFileManager codeFileManager)
    {
        _reviewManager = reviewManager;
        _apiRevisionsManager = apiRevisionsManager;
        _commentsManager = commentsManager;
        _projectsManager = projectsManager;
        _codeFileManager = codeFileManager;
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

                // Delete pending apiRevisions if it is not in approved state before adding new revision
                // This is to keep only one pending revision since last approval or from initial review revision
                var automaticRevisions = apiRevisions.Where(r => r.APIRevisionType == APIRevisionType.Automatic).ToList();
                if (automaticRevisions.Count > 0)
                {
                    var automaticRevisionsQueue = new Queue<APIRevisionListItemModel>(automaticRevisions);
                    var comments = await _commentsManager.GetCommentsAsync(review.Id);
                    APIRevisionListItemModel latestAutomaticAPIRevision = null;
                    
                    while (automaticRevisionsQueue.Count > 0)
                    {
                        latestAutomaticAPIRevision = automaticRevisionsQueue.Dequeue();

                        // Check if we should keep this revision
                        if (latestAutomaticAPIRevision.IsApproved ||
                            latestAutomaticAPIRevision.IsReleased ||
                            await _apiRevisionsManager.AreAPIRevisionsTheSame(latestAutomaticAPIRevision, renderedCodeFile, incomingContentHash: incomingContentHash) ||
                            comments.Any(c => latestAutomaticAPIRevision.Id == c.APIRevisionId))
                        {
                            break;
                        }

                        // Delete this revision
                        await _apiRevisionsManager.SoftDeleteAPIRevisionAsync(apiRevision: latestAutomaticAPIRevision, notes: "Deleted by Automatic Review Creation...");
                        latestAutomaticAPIRevision = null;  // Mark as consumed
                    }

                    // We should compare against only latest revision when calling this API from scheduled CI runs
                    // But any manual pipeline run at release time should compare against all approved revisions to ensure hotfix release doesn't have API change
                    // If review surface doesn't match with any approved revisions then we will create new revision if it doesn't match pending latest revision

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
