
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;
using APIViewWeb.Models;
using System.Collections.Generic;
using APIViewWeb.Managers;
using Microsoft.ApplicationInsights;
using System;

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

        public static string ResolveReviewUrl(PullRequestModel pullRequest, string hostName)
        {
            var url = $"https://{hostName}/Assemblies/Review/{pullRequest.ReviewId}";
            if (!String.IsNullOrEmpty(pullRequest.APIRevisionId))
            {
                url += $"?revisionId={pullRequest.APIRevisionId}";
            }
            return url;
        }
    }
}
