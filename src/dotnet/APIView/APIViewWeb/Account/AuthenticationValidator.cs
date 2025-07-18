// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Security.Claims;
using APIView.Identity;

namespace APIViewWeb.Account
{
    /// <summary>
    /// Provides authentication validation for both GitHub organization and Azure Managed Identity authentication
    /// </summary>
    public static class AuthenticationValidator
    {
        #region Managed Identity Validation

        public static bool IsValidManagedIdentity(ClaimsPrincipal user)
        {
            if (user.Identity is { IsAuthenticated: false })
                return false;

            if (!HasRequiredManagedIdentityClaims(user))
                return false;

            if (!IsValidAzureAuthentication(user))
                return false;

            return IsSystemAssignedManagedIdentity(user) || IsUserAssignedManagedIdentity(user);
        }

        private static bool IsValidAzureAuthentication(ClaimsPrincipal user)
        {
            return HasValidAuthenticationType(user) || HasValidIdentityProvider(user);
        }

        private static bool HasRequiredManagedIdentityClaims(ClaimsPrincipal user)
        {
            var oidClaim = user.FindFirst("oid");
            return oidClaim != null;
        }

        private static bool IsSystemAssignedManagedIdentity(ClaimsPrincipal user)
        {
            var appIdClaim = user.FindFirst("appid");
            return appIdClaim == null;
        }

        private static bool IsUserAssignedManagedIdentity(ClaimsPrincipal user)
        {
            var appIdClaim = user.FindFirst("appid");
            return appIdClaim != null;
        }

        private static bool HasValidAuthenticationType(ClaimsPrincipal user)
        {
            return user.Identity.AuthenticationType == "Bearer" ||
                   user.Identity.AuthenticationType == "AuthenticationTypes.Federation";
        }

        private static bool HasValidIdentityProvider(ClaimsPrincipal user)
        {
            var identityProviderClaim = user.FindFirst("idp");
            return identityProviderClaim?.Value?.Contains("login.microsoftonline.com") == true;
        }

        #endregion

        #region GitHub Organization Validation

        public static bool HasOrganizationAccess(ClaimsPrincipal user, string[] requiredOrganizations)
        {
            if (!user.Identity.IsAuthenticated)
                return false;

            var orgClaim = user.FindFirst(ClaimConstants.Orgs);
            if (orgClaim == null || string.IsNullOrEmpty(orgClaim.Value))
                return false;

            var userOrganizations = orgClaim.Value.Split(',');
            return userOrganizations.Any(userOrg =>
                requiredOrganizations.Contains(userOrg, StringComparer.OrdinalIgnoreCase));
        }

        public static bool HasAnyOrganizationAccess(ClaimsPrincipal user)
        {
            if (!user.Identity.IsAuthenticated)
                return false;

            var orgClaim = user.FindFirst(ClaimConstants.Orgs);
            return orgClaim != null && !string.IsNullOrEmpty(orgClaim.Value);
        }

        public static string[] GetUserOrganizations(ClaimsPrincipal user)
        {
            if (!user.Identity.IsAuthenticated)
                return Array.Empty<string>();

            var orgClaim = user.FindFirst(ClaimConstants.Orgs);
            return orgClaim?.Value?.Split(',') ?? Array.Empty<string>();
        }

        #endregion

        #region Combined Authentication Methods
        public static string GetAuthenticationMethod(ClaimsPrincipal user)
        {
            if (IsValidManagedIdentity(user))
                return "ManagedIdentity";

            if (HasAnyOrganizationAccess(user))
                return "GitHubOrganization";

            return user.Identity.IsAuthenticated ? "Other" : "Anonymous";
        }
        #endregion
    }
}
