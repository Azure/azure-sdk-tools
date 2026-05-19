using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using APIView;
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

                // Scope to automatic revisions for the same logical version as the incoming upload.
                var automaticRevisions = apiRevisions
                    .Where(r => r.APIRevisionType == APIRevisionType.Automatic
                        && (incomingVersionModel == null|| r.APIVersionId == incomingVersionModel.Id || string.IsNullOrEmpty(r.APIVersionId)))
                    .ToList();
                if (automaticRevisions.Count > 0)
                {
                    // For release pipeline runs, compare against all approved revisions first to catch hotfix API changes.
                    // Use the full package version in that comparison to distinguish stable releases from pre-release builds.
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

                    var comments = await _commentsManager.GetCommentsAsync(review.Id);
                    var revisionIdsWithComments = comments.Select(c => c.APIRevisionId).ToHashSet();

                    // Find the newest pending automatic revision to replace.
                    var latestAutomaticAPIRevision = automaticRevisions.FirstOrDefault(
                        r => !r.IsApproved && !r.IsReleased && !revisionIdsWithComments.Contains(r.Id)
                        && r.PackageVersion == codeFile.PackageVersion);

                    if (latestAutomaticAPIRevision != null)
                    {
                        await _apiRevisionsManager.SoftDeleteAPIRevisionAsync(apiRevision: latestAutomaticAPIRevision, notes: "Deleted by Automatic Review Creation...");
                    }
                }
            }
        }
        else
        {
            review = await _reviewManager.CreateReviewAsync(packageName: codeFile.PackageName, language: codeFile.Language, isClosed: false, packageType: parsedPackageType, crossLanguagePackageId: codeFile.CrossLanguagePackageId);
        }

        apiRevision = await _apiRevisionsManager.CreateAPIRevisionAsync(userName: user.GetGitHubLogin(), reviewId: review.Id, apiRevisionType: APIRevisionType.Automatic, label: label, memoryStream: memoryStream, codeFile: codeFile, originalName: originalName, sourceBranch: sourceBranch);

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
