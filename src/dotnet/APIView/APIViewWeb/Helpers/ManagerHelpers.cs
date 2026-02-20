
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

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

        public static string ResolveReviewUrl(string reviewId, string apiRevisionId, string language, IConfiguration configuration, IEnumerable<LanguageService> languageServices, string diffRevisionId = null, string elementId = null)
        {
            var host = configuration["APIVIew-Host-Url"];
            var spaHost = configuration["APIVIew-SPA-Host-Url"];
            var reviewSpaUrlTemplate = $"{spaHost}/review/{reviewId}?activeApiRevisionId={apiRevisionId}";
            var reviewUrlTemplate = $"{host}/Assemblies/Review/{reviewId}?revisionId={apiRevisionId}";

            language = LanguageServiceHelpers.MapLanguageAlias(language);
            var languageService = LanguageServiceHelpers.GetLanguageService(language: language, languageServices: languageServices);
            if (languageService.UsesTreeStyleParser) // Languages using treestyle parser are also using the spa UI
            {
                if (!String.IsNullOrWhiteSpace(diffRevisionId))
                {
                    reviewSpaUrlTemplate += $"&diffApiRevisionId={diffRevisionId}";
                }
                if (!String.IsNullOrWhiteSpace(elementId))
                {
                    reviewSpaUrlTemplate += $"&nId={Uri.EscapeDataString(elementId)}";
                }
                return reviewSpaUrlTemplate;
            }
            else
            {
                if (!String.IsNullOrWhiteSpace(diffRevisionId))
                {
                    reviewUrlTemplate += $"&diffRevisionId={diffRevisionId}";
                }
                if (!String.IsNullOrWhiteSpace(elementId))
                {
                    reviewUrlTemplate += $"#{Uri.EscapeDataString(elementId)}";
                }
                return reviewUrlTemplate;
            }
        }
    }
}
