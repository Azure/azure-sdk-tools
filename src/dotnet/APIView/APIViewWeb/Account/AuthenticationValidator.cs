// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Security.Claims;
using APIView.Identity;
using Microsoft.Extensions.Logging;

namespace APIViewWeb.Account
{
    /// <summary>
    /// Provides authentication validation for both GitHub organization and Azure Managed Identity authentication
    /// </summary>
    public static class AuthenticationValidator
    {
        #region Managed Identity Validation

        public static bool IsValidManagedIdentity(ClaimsPrincipal user, ILogger logger = null)
        {
            logger?.LogInformation("Starting managed identity validation for user: {UserName}", user?.Identity?.Name ?? "Anonymous");
            
            if (user.Identity is { IsAuthenticated: false })
            {
                logger?.LogWarning("User is not authenticated");
                return false;
            }

            logger?.LogInformation("User is authenticated. AuthenticationType: {AuthType}", user.Identity.AuthenticationType);
            
            // Log all claims for debugging
            logger?.LogDebug("User claims: {Claims}", string.Join(", ", user.Claims.Select(c => $"{c.Type}={c.Value}")));

            if (!HasRequiredManagedIdentityClaims(user, logger))
            {
                logger?.LogWarning("User does not have required managed identity claims");
                return false;
            }

            if (!IsValidAzureAuthentication(user, logger))
            {
                logger?.LogWarning("User does not have valid Azure authentication");
                return false;
            }

            var isSystemAssigned = IsSystemAssignedManagedIdentity(user, logger);
            var isUserAssigned = IsUserAssignedManagedIdentity(user, logger);
            var result = isSystemAssigned || isUserAssigned;
            
            logger?.LogInformation("Managed identity validation result: {Result}. SystemAssigned: {SystemAssigned}, UserAssigned: {UserAssigned}", 
                result, isSystemAssigned, isUserAssigned);
            
            return result;
        }

        private static bool IsValidAzureAuthentication(ClaimsPrincipal user, ILogger logger = null)
        {
            var validAuthType = HasValidAuthenticationType(user, logger);
            var validIdp = HasValidIdentityProvider(user, logger);
            var result = validAuthType || validIdp;
            
            logger?.LogInformation("Azure authentication validation: {Result}. ValidAuthType: {ValidAuthType}, ValidIdp: {ValidIdp}", 
                result, validAuthType, validIdp);
            
            return result;
        }

        private static bool HasRequiredManagedIdentityClaims(ClaimsPrincipal user, ILogger logger = null)
        {
            var oidClaim = user.FindFirst("oid") ?? user.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier");
            var hasOid = oidClaim != null;
            
            logger?.LogInformation("Checking required managed identity claims. OID claim present: {HasOid}", hasOid);
            if (hasOid)
            {
                logger?.LogDebug("OID claim value: {OidValue}", oidClaim.Value);
            }
            
            return hasOid;
        }

        private static bool IsSystemAssignedManagedIdentity(ClaimsPrincipal user, ILogger logger = null)
        {
            var appIdClaim = user.FindFirst("appid") ?? user.FindFirst("azp");
            var isSystemAssigned = appIdClaim == null;
            
            logger?.LogInformation("Checking for system-assigned managed identity. AppId claim present: {HasAppId}, IsSystemAssigned: {IsSystemAssigned}", 
                appIdClaim != null, isSystemAssigned);
                
            return isSystemAssigned;
        }

        private static bool IsUserAssignedManagedIdentity(ClaimsPrincipal user, ILogger logger = null)
        {
            var appIdClaim = user.FindFirst("appid") ?? user.FindFirst("azp");
            var isUserAssigned = appIdClaim != null;
            
            logger?.LogInformation("Checking for user-assigned managed identity. AppId claim present: {HasAppId}, IsUserAssigned: {IsUserAssigned}", 
                appIdClaim != null, isUserAssigned);
                
            if (isUserAssigned)
            {
                logger?.LogDebug("AppId claim value: {AppIdValue}", appIdClaim.Value);
            }
            
            return isUserAssigned;
        }

        private static bool HasValidAuthenticationType(ClaimsPrincipal user, ILogger logger = null)
        {
            var authType = user.Identity.AuthenticationType;
            var isValid = authType == "Bearer" || authType == "AuthenticationTypes.Federation";
            
            logger?.LogInformation("Checking authentication type. AuthType: {AuthType}, IsValid: {IsValid}", authType, isValid);
            
            return isValid;
        }

        private static bool HasValidIdentityProvider(ClaimsPrincipal user, ILogger logger = null)
        {
            var identityProviderClaim = user.FindFirst("idp");
            var idpValue = identityProviderClaim?.Value;
            var isValid = idpValue?.Contains("login.microsoftonline.com") == true;
            
            logger?.LogInformation("Checking identity provider. IDP: {IdpValue}, IsValid: {IsValid}", idpValue ?? "null", isValid);
            
            return isValid;
        }

        #endregion

        #region GitHub Organization Validation

        public static bool HasOrganizationAccess(ClaimsPrincipal user, string[] requiredOrganizations, ILogger logger = null)
        {
            logger?.LogInformation("Checking GitHub organization access for {OrgCount} required organizations", requiredOrganizations?.Length ?? 0);
            
            if (!user.Identity.IsAuthenticated)
            {
                logger?.LogWarning("User is not authenticated for organization check");
                return false;
            }

            var orgClaim = user.FindFirst(ClaimConstants.Orgs);
            if (orgClaim == null || string.IsNullOrEmpty(orgClaim.Value))
            {
                logger?.LogWarning("User has no organization claims");
                return false;
            }

            var userOrganizations = orgClaim.Value.Split(',');
            logger?.LogInformation("User organizations: {UserOrgs}", string.Join(", ", userOrganizations));
            logger?.LogInformation("Required organizations: {RequiredOrgs}", string.Join(", ", requiredOrganizations ?? Array.Empty<string>()));
            
            var hasAccess = userOrganizations.Any(userOrg =>
                requiredOrganizations.Contains(userOrg, StringComparer.OrdinalIgnoreCase));
                
            logger?.LogInformation("Organization access result: {HasAccess}", hasAccess);
            
            return hasAccess;
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
        
        /// <summary>
        /// Validates that a user has either GitHub organization access OR valid managed identity authentication
        /// </summary>
        /// <param name="user">The claims principal representing the authenticated user</param>
        /// <param name="requiredOrganizations">Array of required GitHub organizations</param>
        /// <param name="logger">Optional logger for debugging</param>
        /// <returns>True if user has valid authentication through either method</returns>
        public static bool HasOrganizationOrManagedIdentityAccess(ClaimsPrincipal user, string[] requiredOrganizations, ILogger logger = null)
        {
           // logger?.LogInformation("Starting combined authentication check (Organization OR Managed Identity)");
            
            if (!user.Identity.IsAuthenticated)
            {
                logger?.LogWarning("User is not authenticated for combined check");
                return false;
            }

            // Check if user has valid managed identity authentication
            logger?.LogInformation("Checking managed identity authentication...");
            if (IsValidManagedIdentity(user, logger))
            {
                logger?.LogInformation("User authenticated via valid managed identity");
                return true;
            }

            // Check if user has required GitHub organization access
            logger?.LogInformation("Checking GitHub organization access...");
            if (HasOrganizationAccess(user, requiredOrganizations, logger))
            {
                logger?.LogInformation("User authenticated via GitHub organization access");
                return true;
            }

            logger?.LogWarning("User failed both managed identity and organization authentication checks");
            return false;
        }
        
        public static string GetAuthenticationMethod(ClaimsPrincipal user, ILogger logger = null)
        {
            logger?.LogInformation("Determining authentication method for user");
            
            if (IsValidManagedIdentity(user, logger))
            {
                logger?.LogInformation("Authentication method: ManagedIdentity");
                return "ManagedIdentity";
            }

            if (HasAnyOrganizationAccess(user))
            {
                logger?.LogInformation("Authentication method: GitHubOrganization");
                return "GitHubOrganization";
            }

            var method = user.Identity.IsAuthenticated ? "Other" : "Anonymous";
            logger?.LogInformation("Authentication method: {Method}", method);
            return method;
        }
        #endregion
    }
}
