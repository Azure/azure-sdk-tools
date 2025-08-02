using System;
using System.Linq;
using System.Security.Claims;
using APIView.Identity;

namespace APIViewWeb.Account
{
    public static class AuthenticationValidator
    {
        #region Managed Identity Validation

        public static bool IsValidManagedIdentity(ClaimsPrincipal user)
        {
            if (!IsAuthenticated(user))
            {
                return false;
            }

            return HasRequiredManagedIdentityClaims(user) && IsValidAzureAuthentication(user);
        }

        private static bool IsValidAzureAuthentication(ClaimsPrincipal user)
        {
            return HasValidAuthenticationType(user) || HasValidIdentityProvider(user);
        }

        private static bool HasRequiredManagedIdentityClaims(ClaimsPrincipal user)
        {
            Claim oidClaim = user.FindFirst("oid") ?? user.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier");
            return oidClaim != null;
        }

        private static bool HasValidAuthenticationType(ClaimsPrincipal user)
        {
            var authType = user.Identity.AuthenticationType;
            return authType == "Bearer" || authType == "AuthenticationTypes.Federation";
        }

        private static bool HasValidIdentityProvider(ClaimsPrincipal user)
        {
            Claim identityProviderClaim = user.FindFirst("idp");
            return identityProviderClaim?.Value?.Contains("login.microsoftonline.com") == true;
        }

        #endregion

        #region GitHub Organization Validation

        public static bool HasOrganizationAccess(ClaimsPrincipal user, string[] requiredOrganizations)
        {
            if (!IsAuthenticated(user))
            {
                return false;
            }

            string[] userOrganizations = GetUserOrganizations(user);
            return userOrganizations.Any(userOrg =>
                requiredOrganizations.Contains(userOrg, StringComparer.OrdinalIgnoreCase));
        }

        public static bool HasAnyOrganizationAccess(ClaimsPrincipal user)
        {
            return IsAuthenticated(user) && GetUserOrganizations(user).Length > 0;
        }

        public static string[] GetUserOrganizations(ClaimsPrincipal user)
        {
            if (!IsAuthenticated(user))
            {
                return [];
            }

            Claim orgClaim = user.FindFirst(ClaimConstants.Orgs);
            return orgClaim?.Value?.Split(',') ?? [];
        }

        #endregion

        #region Combined Authentication Methods
        
        public static bool HasOrganizationOrManagedIdentityAccess(ClaimsPrincipal user, string[] requiredOrganizations)
        {
            if (!IsAuthenticated(user))
            {
                return false;
            }

            return IsValidManagedIdentity(user) || HasOrganizationAccess(user, requiredOrganizations);
        }
        
        public static string GetAuthenticationMethod(ClaimsPrincipal user)
        {
            if (IsValidManagedIdentity(user))
            {
                return "ManagedIdentity";
            }

            if (HasAnyOrganizationAccess(user))
            {
                return "GitHubOrganization";
            }

            return IsAuthenticated(user) ? "Other" : "Anonymous";
        }
        #endregion

        private static bool IsAuthenticated(ClaimsPrincipal user)
        {
            return user?.Identity?.IsAuthenticated == true;
        }
    }
}
