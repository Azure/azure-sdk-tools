using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace APIViewWeb.Controllers;

public class AutoReviewController : Controller
{
    private readonly IAPIRevisionsManager _apiRevisionsManager;
    private readonly IReviewManager _reviewManager;

    public AutoReviewController(IReviewManager reviewManager, IAPIRevisionsManager apiRevisionManager)
    {
        _apiRevisionsManager = apiRevisionManager;
        _reviewManager = reviewManager;
    }

    public async Task<ActionResult> GetReviewStatus(string language, string packageName, string reviewId = null,
        bool? firstReleaseStatusOnly = null, string packageVersion = null)
    {
        // This API is used to get approval status of an API review revision. If a package version is passed then it will try to find a revision with exact package version match or revisions with same major and minor version.
        // If there is no matching revisions then it will return latest automatic revision details.
        // This is used by prepare release script and build pipeline to verify approval status.
        try
        {
            ReviewListItemModel review =
                await _reviewManager.GetReviewAsync(packageName: packageName, language: language, isClosed: null);

            if (review == null)
            {
                return StatusCode(StatusCodes.Status404NotFound, "Review is not found for package " + packageName);
            }

            APIRevisionListItemModel apiRevision = null;

            if (!string.IsNullOrEmpty(packageVersion))
            {
                IEnumerable<APIRevisionListItemModel> apiRevisions =
                    await _apiRevisionsManager.GetAPIRevisionsAsync(review.Id, packageVersion,
                        APIRevisionType.Automatic);
                if (apiRevisions.Any())
                {
                    apiRevision = apiRevisions.FirstOrDefault();
                }
            }

            if (apiRevision == null)
            {
                apiRevision =
                    await _apiRevisionsManager.GetLatestAPIRevisionsAsync(review.Id,
                        apiRevisionType: APIRevisionType.Automatic);
            }


            if (apiRevision == null)
            {
                return StatusCode(StatusCodes.Status404NotFound, "Review is not found for package " + packageName);
            }

            // Return 200 OK for approved review and 201 for review in pending status
            if (firstReleaseStatusOnly != true && apiRevision != null && apiRevision.IsApproved)
            {
                return Ok();
            }

            if (review.IsApproved)
            {
                return StatusCode(StatusCodes.Status201Created);
            }

            // Return 202 to indicate package name is not approved
            return StatusCode(StatusCodes.Status202Accepted);
        }
        catch (Exception e)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new
                {
                    error = "Failed to get review status",
                    message = e.Message,
                    exceptionType = e.GetType().Name,
                    details = new { packageName, language, packageVersion, reviewId }
                });
        }
    }
}
