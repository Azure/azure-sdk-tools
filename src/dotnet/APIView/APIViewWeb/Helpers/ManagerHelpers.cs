
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;
using APIViewWeb.Models;
using System;
using System.Collections.Generic;

namespace APIViewWeb.Helpers
{
    public class ManagerHelpers
    {
        public static async Task AssertApprover<T>(ClaimsPrincipal user, T model, IAuthorizationService authorizationService)
        {
            var result = await authorizationService.AuthorizeAsync(
                user,
                model,
                new[] { ApproverRequirement.Instance });
            if (!result.Succeeded)
            {
                throw new AuthorizationFailedException();
            }
        }

        public static async Task AssertAutomaticAPIRevisionModifier(ClaimsPrincipal user, APIRevisionListItemModel apiRevision, IAuthorizationService authorizationService)
        {
            var result = await authorizationService.AuthorizeAsync(
                user,
                apiRevision,
                new[] { AutoAPIRevisionModifierRequirement.Instance });
            if (!result.Succeeded)
            {
                throw new AuthorizationFailedException();
            }
        }

        public static async Task AssertAPIRevisionOwner(ClaimsPrincipal user, APIRevisionListItemModel revisionModel, IAuthorizationService authorizationService)
        {
            var result = await authorizationService.AuthorizeAsync(
                user,
                revisionModel,
                new[] { RevisionOwnerRequirement.Instance });
            if (!result.Succeeded)
            {
                throw new AuthorizationFailedException();
            }
        }

        public static async Task AssertReviewOwnerAsync(ClaimsPrincipal user, ReviewListItemModel reviewModel, IAuthorizationService authorizationService)
        {
            var result = await authorizationService.AuthorizeAsync(user, reviewModel, new[] { ReviewOwnerRequirement.Instance });
            if (!result.Succeeded)
            {
                throw new AuthorizationFailedException();
            }
        }

        public static void AssertAPIRevisionDeletion(APIRevisionListItemModel apiRevision)
        {
            // We allow deletion of manual API review only.
            // Server side assertion to ensure we are not processing any requests to delete automatic and PR API review
            if (apiRevision.APIRevisionType != APIRevisionType.Manual)
            {
                throw new UnDeletableReviewException();
            }
        }

        public static string ResolveReviewUrl(PullRequestModel pullRequest, string hostName, IEnumerable<LanguageService> languageServices)
        {
            var legacyurl = $"https://{hostName}/Assemblies/Review/{pullRequest.ReviewId}";
            var url = $"https://spa.{hostName}/review/{pullRequest.ReviewId}?activeApiRevisionId={pullRequest.APIRevisionId}";
            if (!String.IsNullOrEmpty(pullRequest.APIRevisionId))
            {
                legacyurl += $"?revisionId={pullRequest.APIRevisionId}";
            }

            var language = LanguageServiceHelpers.MapLanguageAlias(pullRequest.Language);
            var languageService = LanguageServiceHelpers.GetLanguageService(language: language, languageServices: languageServices);
            if (languageService.UsesTreeStyleParser) // Languages using treestyle parser are also using the spa UI
            {
                return url;
            }
            else
            {
                return legacyurl;
            }
        }
    }
}
