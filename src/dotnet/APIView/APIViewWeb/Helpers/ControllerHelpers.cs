using System.Linq;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace APIViewWeb.Helpers;

public static class ControllerHelpers
{
    public static async Task<(ReviewListItemModel review, APIRevisionListItemModel revision, ActionResult errorResult)> GetReviewAndRevisionAsync(ICosmosReviewRepository cosmosReviewRepository, 
        IAPIRevisionsManager apiRevisionsManager, string packageName, string language, string version = "")
    {
        ReviewListItemModel review = await cosmosReviewRepository.GetReviewAsync(language, packageName);
        if (review == null)
        {
            LeanJsonResult errorResult = new($"Package name {packageName} for language {language} not found", StatusCodes.Status404NotFound);
            return (null, null, errorResult);
        }

        APIRevisionListItemModel revision = string.IsNullOrEmpty(version)
            ? await apiRevisionsManager.GetLatestAPIRevisionsAsync(review.Id)
            : (await apiRevisionsManager.GetAPIRevisionsAsync(review.Id, version))
            .OrderByDescending(r => r.LastUpdatedOn).FirstOrDefault();

        if (revision == null)
        {
            string versionText = string.IsNullOrEmpty(version) ? "latest" : version;
            LeanJsonResult errorResult = new(
                $"Package name {packageName} for language {language} with version {versionText} not found",
                StatusCodes.Status404NotFound);
            return (review, null, errorResult);
        }

        return (review, revision, null);
    }
}
